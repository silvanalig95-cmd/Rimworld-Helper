using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>The topics and kinds of shared memory the code names, looked up once (Defs/TopicDefs, Defs/SharedMemoryDefs).</summary>
    public static class TalkDefs
    {
        public static SharedMemoryDef Mem(string name) => DefRefs.Named<SharedMemoryDef>(name);

        public static TopicDef Topic(string name) => DefRefs.Named<TopicDef>("Rimfolk_Topic_" + name);

        public static SharedMemoryDef Battle => Mem("Rimfolk_Mem_Battle");
        public static SharedMemoryDef Tended => Mem("Rimfolk_Mem_Tended");
        public static SharedMemoryDef Funeral => Mem("Rimfolk_Mem_Funeral");
        public static SharedMemoryDef Feast => Mem("Rimfolk_Mem_Feast");
        public static SharedMemoryDef Disaster => Mem("Rimfolk_Mem_Disaster");
        public static SharedMemoryDef Caravan => Mem("Rimfolk_Mem_Caravan");
        public static SharedMemoryDef Built => Mem("Rimfolk_Mem_Built");
        public static SharedMemoryDef NightShift => Mem("Rimfolk_Mem_NightShift");
        public static SharedMemoryDef Taught => Mem("Rimfolk_Mem_Taught");
        public static SharedMemoryDef Mended => Mem("Rimfolk_Mem_Mended");
        public static SharedMemoryDef Confided => Mem("Rimfolk_Mem_Confided");
        public static SharedMemoryDef Stew => Mem("Rimfolk_Mem_Stew");
        public static SharedMemoryDef Boom => Mem("Rimfolk_Mem_Boom");
        public static SharedMemoryDef Tame => Mem("Rimfolk_Mem_Tame");
        public static SharedMemoryDef Fire => Mem("Rimfolk_Mem_Fire");
        public static SharedMemoryDef Binge => Mem("Rimfolk_Mem_Binge");
    }

    /// <summary>
    /// Where shared memories and the colony's news come from (Social/Talk/): what people go through together, noticed as
    /// it happens or in the hour's look.
    /// <list type="bullet">
    /// <item>From the game's events, one line each in the hooks that were already there: a fight over (who fought side by
    /// side), a death, a wedding, a birth, a caravan home, a short binge; and two small hooks of its own: someone patched
    /// up while down, something big built with others at work beside them.</item>
    /// <item>Once an hour (<see cref="TalkTracker"/>): two still at work in the small hours, a disaster come through, a
    /// party, feast or funeral ended, a fire put out together, a bad stew, a tame that went wrong, and the news people
    /// will talk about: someone new, a prisoner, traders, a decree, a finished study.</item>
    /// <item>Once a day: a teacher and their student, a secret confided.</item>
    /// </list>
    /// Mishaps become in-jokes only once it's clear nobody came to real harm (an hour later, everyone in it alive and up).
    /// Nothing here is saved but the memories and the news; what waits is forgotten on a load.
    /// </summary>
    public static class TalkHooks
    {
        private static bool On => Persona.On && Current.Game != null;

        private static int Now => Find.TickManager.TicksGame;

        public static void Reset()
        {
            pending.Clear();
            night.Clear();
            disasters.Clear();
            gatherings.Clear();
            seenHediffs.Clear();
            seenPrisoners = null;
            finished = null;
            lastLogId = -1;
            noted.Clear();
            lastFood.Clear();
        }

        private static void Safe(string what, Action act)
        {
            try
            {
                act();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("talk: " + what, e);
            }
        }

        // ---------------- Words for it ----------------

        private static float Longitude(Map map) => map != null ? Find.WorldGrid.LongLatOf(map.Tile).x : 0f;

        /// <summary>Today's date as the lines say it ("5th of Aprimay").</summary>
        public static string Date(Map map) => GenDate.QuadrumDateStringAt(Find.TickManager.TicksAbs, Longitude(map ?? Find.AnyPlayerHomeMap));

        /// <summary>This quadrum's name ("Aprimay").</summary>
        public static string Quadrum(Map map) => GenDate.Quadrum(Find.TickManager.TicksAbs, Longitude(map ?? Find.AnyPlayerHomeMap)).Label();

        private static string The(string label) => label.NullOrEmpty() ? label : Find.ActiveLanguageWorker.WithDefiniteArticle(label);

        /// <summary>What a fight is called afterwards: the fight with a people, the mechanoids, the hive, or a date.</summary>
        public static string FightLabel(Faction foe, Map map)
        {
            if (foe == null) return "CC_TalkLabel_FightOn".Translate(Date(map)).Resolve();
            if (foe == Faction.OfMechanoids) return "CC_TalkLabel_Mechs".Translate().Resolve();
            if (foe == Faction.OfInsects) return "CC_TalkLabel_Insects".Translate().Resolve();
            return "CC_TalkLabel_FightWith".Translate(foe.Name).Resolve();
        }

        /// <summary>A disaster the colony comes through together (Defs: the conditions named here, if the game has them).</summary>
        private static readonly HashSet<string> disasterNames = new HashSet<string>
        {
            "ToxicFallout", "ColdSnap", "HeatWave", "VolcanicWinter", "Eclipse", "Flashstorm", "SolarFlare", "NoxiousHaze", "Drought",
            "Flood", "DeathPall", "BloodRain", "GrayPall", "UnnaturalDarkness", "UnnaturalHeat", "PsychicDrone"
        };

        public static bool IsDisaster(GameConditionDef def) => def != null && disasterNames.Contains(def.defName);

        private static IEnumerable<GameCondition> Conditions(Map map)
        {
            var own = map?.gameConditionManager?.ActiveConditions;
            if (own != null)
                foreach (var c in own)
                    yield return c;
            var world = Find.World?.gameConditionManager?.ActiveConditions;
            if (world != null)
                foreach (var c in world)
                    yield return c;
        }

        // ---------------- Memories between several people ----------------

        /// <summary>
        /// Everyone in it shares the memory with the ones they're closest to among the others (at most
        /// <paramref name="each"/> each), so a crowd doesn't fill everyone's memories.
        /// </summary>
        private static void AmongThem(List<Pawn> people, SharedMemoryDef def, string label, float weight, int each)
        {
            if (def == null || people == null) return;
            var list = people.Where(Talk.Eligible).Distinct().ToList();
            if (list.Count < 2) return;
            foreach (var p in list)
            {
                var closest = list.Where(o => o != p).OrderByDescending(o => p.relations?.OpinionOf(o) ?? 0).Take(each).ToList();
                foreach (var o in closest) AddOnce(p, o, def, label, weight);
            }
        }

        /// <summary>A memory, unless the two already have this very one (the same kind and label).</summary>
        public static void AddOnce(Pawn a, Pawn b, SharedMemoryDef def, string label, float weight = 1f)
        {
            if (def == null || a == null || b == null || a == b) return;
            int now = Now;
            if (SharedMemories.Of(a, b).Any(m => m.def == def && m.label == label && now - m.tick >= GenDate.TicksPerDay)) return;
            SharedMemories.Add(a, b, def, label, weight);
        }

        // ---------------- Mishaps waiting to be funny ----------------

        private sealed class Pending
        {
            public SharedMemoryDef def;
            public string label;
            public List<Pawn> people;
            public int tick;
            /// <summary>Anything more it needs before it's a joke (the binge is over).</summary>
            public Func<bool> ready;
        }

        private static readonly List<Pending> pending = new List<Pending>();

        private static void Mishap(SharedMemoryDef def, string label, List<Pawn> people, Func<bool> ready = null)
        {
            if (def == null || people == null || people.Count < 2) return;
            if (pending.Count >= 20) pending.RemoveAt(0);
            pending.Add(new Pending { def = def, label = label, people = people.Distinct().ToList(), tick = Now, ready = ready });
        }

        /// <summary>An hour or more on, with everyone in it alive and up: it's a joke now (and the first two in it share it).</summary>
        private static void ConfirmMishaps()
        {
            int now = Now;
            foreach (var m in pending.ToList())
            {
                if (now - m.tick > GenDate.TicksPerDay)
                {
                    pending.Remove(m);
                    continue;
                }
                if (now - m.tick < GenDate.TicksPerHour) continue;
                if (m.people.Any(p => p == null || p.Dead || p.Downed)) continue;
                if (m.ready != null && !m.ready()) continue;
                pending.Remove(m);
                // Nobody of ours died meanwhile: then it isn't a laughing matter for a while.
                if (TalkTracker.Instance?.Latest("Rimfolk_Topic_Dead", 0.5f) != null) continue;
                var first = m.people[0];
                foreach (var o in m.people.Skip(1).Take(4)) AddOnce(first, o, m.def, m.label);
                if (m.people.Count > 2)
                    for (int i = 1; i < m.people.Count && i < 4; i++)
                        for (int j = i + 1; j < m.people.Count && j < 4; j++)
                            AddOnce(m.people[i], m.people[j], m.def, m.label);
            }
        }

        // ---------------- From the game's events ----------------

        /// <summary>A fight on a home map is over (SocialWeb): those who fought side by side remember it, and the colony talks of it.</summary>
        public static void Battle(List<Pawn> fighters, Faction foe)
        {
            if (!On || fighters == null) return;
            Safe("battle", () =>
            {
                var map = fighters.FirstOrDefault(p => p?.MapHeld != null)?.MapHeld ?? Find.AnyPlayerHomeMap;
                string label = FightLabel(foe, map);
                var tr = TalkTracker.Instance;
                bool fallen = tr?.Recent("Rimfolk_Topic_Dead", 0.5f).Any(n => n.tag == "battle") ?? false;
                string kind = foe == null ? "animals" : foe == Faction.OfMechanoids ? "mechs" : foe == Faction.OfInsects ? "insects" : foe.def.techLevel <= TechLevel.Neolithic ? "tribal" : "people";
                tr?.Note("Rimfolk_Topic_Raid", label, -1, (fallen ? "fallen" : "won") + "," + kind);
                AmongThem(fighters, TalkDefs.Battle, label, 1.5f, 5);
                // A fight won makes people bolder about the next one; losing someone makes the timid want walls.
                var raid = TalkDefs.Topic("Raid");
                foreach (var p in fighters.Where(Talk.Eligible))
                {
                    float nerve = Persona.Get(p, PersonaAxis.Nerve);
                    Stances.Shift(p, raid, fallen ? (nerve >= 0f ? 0.05f : -0.06f) : 0.04f);
                }
            });
        }

        /// <summary>Someone died (Pawn.Kill, every death): one of ours, or an exploding beast beside our people.</summary>
        public static void Killed(Pawn p, Map map)
        {
            if (!On || p == null || !RimfolkPatches.On) return;
            Safe("death", () =>
            {
                if (p.RaceProps.Humanlike)
                {
                    if (p.Faction != Faction.OfPlayer || !p.Dead) return;
                    var tr = TalkTracker.Instance;
                    var home = map ?? p.MapHeld;
                    tr?.exchanges.DropWith(p);
                    tr?.Note("Rimfolk_Topic_Dead", p.LabelShort, p.thingIDNumber, home != null && MapFacts.ThreatOn(home) ? "battle" : null);
                    var future = TalkDefs.Topic("Future");
                    foreach (var o in PawnsFinder.AllMaps_FreeColonists.ToList())
                        if (o != p && (o.relations?.OpinionOf(p) ?? 0) >= 20) Stances.Shift(o, future, -0.05f);
                    return;
                }
                var where = map ?? p.MapHeld;
                if (where == null || !where.IsPlayerHome || p.RaceProps.DeathActionWorker == null || !p.RaceProps.DeathActionWorker.DangerousInMelee) return;
                var pos = p.PositionHeld;
                var near = where.mapPawns.FreeColonistsSpawned.Where(c => Talk.Eligible(c) && c.Position.InHorDistOf(pos, 9f)).ToList();
                if (near.Count >= 2) Mishap(TalkDefs.Boom, The(p.kindDef?.label ?? p.def.label), near);
            });
        }

        public static void Wedding(Pawn a, Pawn b)
        {
            if (!On || a == null || b == null) return;
            Safe("wedding", () => TalkTracker.Instance?.Note("Rimfolk_Topic_Love", "CC_TalkLabel_Couple".Translate(a.LabelShort, b.LabelShort).Resolve(), -1, "wedding"));
        }

        public static void Birth(Pawn baby, Pawn mother, Pawn father)
        {
            if (!On || baby == null) return;
            Safe("birth", () => TalkTracker.Instance?.Note("Rimfolk_Topic_Children", baby.LabelShort, baby.thingIDNumber, "birth"));
        }

        /// <summary>A caravan home: those who travelled together remember the road.</summary>
        public static void Homecoming(List<Pawn> caravan)
        {
            if (!On || caravan == null) return;
            Safe("homecoming", () =>
            {
                var people = caravan.Where(Talk.Eligible).ToList();
                if (people.Count < 2) return;
                string label = "CC_TalkLabel_Road".Translate(Quadrum(people[0].MapHeld)).Resolve();
                AmongThem(people, TalkDefs.Caravan, label, 1.2f, 4);
                TalkTracker.Instance?.Note("Rimfolk_Topic_Outside", null, -1, "caravan");
            });
        }

        /// <summary>A short binge (food or a mild drug) becomes a joke with a friend who saw it, once it's over and they're fine.</summary>
        public static void Breakdown(Pawn p, MentalStateDef state)
        {
            if (!On || p == null || state == null || !Talk.Eligible(p)) return;
            bool food = state.defName == "Binging_Food", drink = state.defName == "Binging_DrugMild";
            if (!food && !drink) return;
            Safe("binge", () =>
            {
                TalkTracker.Instance?.Note("Rimfolk_Topic_Drugs", p.LabelShort, p.thingIDNumber, "binge");
                if (p.MapHeld == null) return;
                var friend = p.MapHeld.mapPawns.FreeColonistsSpawned.Where(o => o != p && Talk.Eligible(o) && (o.relations?.OpinionOf(p) ?? 0) >= 15)
                    .OrderByDescending(o => o.relations.OpinionOf(p)).FirstOrDefault();
                if (friend == null) return;
                string label = (food ? "CC_TalkLabel_Feast" : "CC_TalkLabel_Binge").Translate().Resolve();
                Mishap(TalkDefs.Binge, label, new List<Pawn> { p, friend }, () => !p.InMentalState);
            });
        }

        /// <summary>Something big finished (Patch_TalkBuilt): the builder and those at the work beside them built it together.</summary>
        public static void Built(Pawn worker, Map map, IntVec3 pos, ThingDef def, float work)
        {
            if (!On || worker == null || map == null || def == null || work < 3000f || !Talk.Eligible(worker)) return;
            Safe("built", () =>
            {
                var helpers = map.mapPawns.FreeColonistsSpawned
                    .Where(o => o != worker && Talk.Eligible(o) && o.CurJobDef == JobDefOf.FinishFrame && o.Position.InHorDistOf(pos, 14f)).ToList();
                if (helpers.Count == 0) return;
                string label = The(def.label);
                var all = new List<Pawn> { worker };
                all.AddRange(helpers.Take(4));
                AmongThem(all, TalkDefs.Built, label, 1f, 4);
            });
        }

        /// <summary>Someone patched up while down (Patch_TalkTended): the doctor and the one they tended remember it.</summary>
        public static void Tended(Pawn doctor, Pawn patient)
        {
            if (!On || doctor == null || patient == null || doctor == patient || !patient.Downed) return;
            if (!Talk.Eligible(doctor) || !Talk.Eligible(patient)) return;
            Safe("tended", () =>
            {
                var raid = TalkTracker.Instance?.Latest("Rimfolk_Topic_Raid", 1.5f);
                string label = raid?.subject ?? (patient.MapHeld != null && MapFacts.ThreatOn(patient.MapHeld)
                    ? FightLabel(null, patient.MapHeld)
                    : "CC_TalkLabel_Day".Translate(Date(patient.MapHeld)).Resolve());
                SharedMemories.Add(doctor, patient, TalkDefs.Tended, label, 1.2f);
            });
        }

        // ---------------- The hour's look ----------------

        public static void Hourly(TalkTracker tr)
        {
            if (!On || tr == null) return;
            Safe("mishaps", ConfirmMishaps);
            foreach (var map in Find.Maps.Where(m => m.IsPlayerHome).ToList())
            {
                Safe("night shift", () => NightShift(map));
                Safe("disasters", () => Disasters(map, tr));
                Safe("gatherings", () => Gatherings(map, tr));
                Safe("fires", () => Fires(map));
                Safe("food poisoning", () => FoodPoisoning(map));
                Safe("news", () => News(map, tr));
            }
            Safe("tames", TameLog);
            Safe("research", () => Research(tr));
            Safe("council", () => Council(tr));
        }

        private static readonly Dictionary<(long pair, int day), int> night = new Dictionary<(long, int), int>();

        /// <summary>Two still at work together in the small hours, three hours of a night: a long night they'll remember.</summary>
        private static void NightShift(Map map)
        {
            int hour = GenLocalDate.HourOfDay(map);
            if (hour < 1 || hour > 4) return;
            int day = GenLocalDate.DayOfYear(map) + GenLocalDate.Year(map) * 60;
            if (night.Count > 400) night.Clear();
            var awake = map.mapPawns.FreeColonistsSpawned.Where(p => Talk.Eligible(p) && p.Awake() && !p.InBed() && p.CurJob?.workGiverDef != null).ToList();
            for (int i = 0; i < awake.Count; i++)
            {
                for (int j = i + 1; j < awake.Count; j++)
                {
                    if (!Together.Can(awake[i], awake[j])) continue;
                    var key = (PersonaMath.PairKey(awake[i].thingIDNumber, awake[j].thingIDNumber), day);
                    night.TryGetValue(key, out int n);
                    night[key] = ++n;
                    if (n == 3) AddOnce(awake[i], awake[j], TalkDefs.NightShift, "CC_TalkLabel_Night".Translate(Date(map)).Resolve(), 0.8f);
                }
            }
        }

        private sealed class Seen
        {
            public Map map;
            public string label, kind;
            public int start;
            public readonly HashSet<Pawn> people = new HashSet<Pawn>();
        }

        private static readonly Dictionary<string, Seen> disasters = new Dictionary<string, Seen>();

        /// <summary>A disaster come through (a cold snap, toxic fallout, an eclipse): once it lifts, those who lived through it remember it.</summary>
        private static void Disasters(Map map, TalkTracker tr)
        {
            var active = new HashSet<string>();
            foreach (var c in Conditions(map))
            {
                if (c?.def == null || !IsDisaster(c.def)) continue;
                string key = map.uniqueID + "|" + c.def.defName + "|" + c.startTick;
                active.Add(key);
                if (!disasters.TryGetValue(key, out var s))
                {
                    disasters[key] = s = new Seen { map = map, label = "CC_TalkLabel_Disaster".Translate(c.Label, Quadrum(map)).Resolve(), start = c.startTick };
                    tr.Note("Rimfolk_Topic_Weather", c.Label, -1, "disaster");
                }
                foreach (var p in map.mapPawns.FreeColonistsSpawned)
                    if (Talk.Eligible(p)) s.people.Add(p);
            }
            foreach (var key in disasters.Keys.ToList())
            {
                var s = disasters[key];
                if (s.map != map || active.Contains(key)) continue;
                disasters.Remove(key);
                if (Now - s.start < GenDate.TicksPerDay / 2) continue;
                AmongThem(s.people.Where(p => !p.Dead).ToList(), TalkDefs.Disaster, s.label, 1.2f, 2);
            }
        }

        private static readonly Dictionary<int, Seen> gatherings = new Dictionary<int, Seen>();

        /// <summary>A party, feast, wedding, ritual or funeral ended: those who were there together remember it.</summary>
        private static void Gatherings(Map map, TalkTracker tr)
        {
            var lords = map.lordManager?.lords;
            var alive = new HashSet<int>();
            if (lords != null)
            {
                foreach (var lord in lords)
                {
                    var lj = lord?.LordJob;
                    if (lj == null) continue;
                    string kind = null, label = null;
                    if (lj is LordJob_Ritual ritual)
                    {
                        string name = ritual.Ritual?.def?.defName ?? "";
                        if (name.IndexOf("Funeral", StringComparison.OrdinalIgnoreCase) >= 0) kind = "funeral";
                        else
                        {
                            kind = "feast";
                            label = The(ritual.RitualLabel);
                        }
                    }
                    else if (lj is LordJob_Joinable_MarriageCeremony) kind = "wedding";
                    else if (lj is LordJob_Joinable_Gathering || lj is LordJob_VoluntarilyJoinable) kind = "feast";
                    if (kind == null) continue;
                    alive.Add(lord.loadID);
                    if (!gatherings.TryGetValue(lord.loadID, out var s))
                        gatherings[lord.loadID] = s = new Seen { map = map, kind = kind, label = label, start = Now };
                    foreach (var p in lord.ownedPawns)
                        if (p != null && Talk.Eligible(p)) s.people.Add(p);
                }
            }
            foreach (var id in gatherings.Keys.ToList())
            {
                var s = gatherings[id];
                if (s.map != map || alive.Contains(id)) continue;
                gatherings.Remove(id);
                var people = s.people.Where(p => !p.Dead).ToList();
                if (people.Count < 2) continue;
                switch (s.kind)
                {
                    case "funeral":
                    {
                        var dead = tr.Latest("Rimfolk_Topic_Dead", 6f);
                        AmongThem(people, TalkDefs.Funeral, dead?.subject ?? "CC_TalkLabel_Funeral".Translate().Resolve(), 1.5f, 3);
                        break;
                    }
                    case "wedding":
                    {
                        var w = tr.Latest("Rimfolk_Topic_Love", 1f);
                        string label = w?.subject != null ? "CC_TalkLabel_Wedding".Translate(w.subject).Resolve() : "CC_TalkLabel_WeddingDay".Translate(Date(map)).Resolve();
                        AmongThem(people, TalkDefs.Feast, label, 1f, 3);
                        break;
                    }
                    default:
                        AmongThem(people, TalkDefs.Feast, s.label ?? "CC_TalkLabel_Party".Translate(Date(map)).Resolve(), 0.8f, 3);
                        break;
                }
            }
            if (gatherings.Count > 40) gatherings.Clear();
        }

        /// <summary>Two or more beating out a small fire together: once all is well, it's "the little fire".</summary>
        private static void Fires(Map map)
        {
            var fires = map.listerThings.ThingsOfDef(ThingDefOf.Fire);
            if (fires == null || fires.Count == 0 || fires.Count > 40) return;
            var crew = map.mapPawns.FreeColonistsSpawned.Where(p => Talk.Eligible(p) && p.CurJobDef == JobDefOf.BeatFire).ToList();
            if (crew.Count < 2) return;
            if (pending.Any(m => m.def == TalkDefs.Fire && Now - m.tick < GenDate.TicksPerHour * 6)) return;
            string room = fires[0].GetRoom()?.Role?.label;
            string label = room.NullOrEmpty() || fires[0].GetRoom().PsychologicallyOutdoors
                ? "CC_TalkLabel_Fire".Translate().Resolve()
                : "CC_TalkLabel_FireIn".Translate(room).Resolve();
            Mishap(TalkDefs.Fire, label, crew.Take(4).ToList());
        }

        private static readonly HashSet<int> seenHediffs = new HashSet<int>();

        /// <summary>A bad stew: those who fell ill of it together, and the cook, laugh about it later.</summary>
        private static void FoodPoisoning(Map map)
        {
            var def = HediffDefOf.FoodPoisoning;
            if (def == null) return;
            if (seenHediffs.Count > 2000) seenHediffs.Clear();
            var ill = new List<Pawn>();
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                var h = p.health?.hediffSet?.GetFirstHediffOfDef(def);
                if (h == null || h.ageTicks > GenDate.TicksPerHour * 4 || !seenHediffs.Add(h.loadID)) continue;
                if (Talk.Eligible(p)) ill.Add(p);
            }
            if (ill.Count == 0) return;
            string label = "CC_TalkLabel_Stew".Translate().Resolve();
            if (ill.Count >= 2) Mishap(TalkDefs.Stew, label, ill.Take(4).ToList());
            var cook = TalkScene.Of(map)?.bestCook;
            if (cook == null || ill.Contains(cook) || !Talk.Eligible(cook)) return;
            foreach (var p in ill.Take(2))
                if ((p.relations?.OpinionOf(cook) ?? 0) >= 0) Mishap(TalkDefs.Stew, label, new List<Pawn> { p, cook });
        }

        private static readonly Dictionary<string, int> noted = new Dictionary<string, int>();
        private static HashSet<int> seenPrisoners;
        private static readonly Dictionary<int, float> lastFood = new Dictionary<int, float>();

        private static bool NoteOnce(string key, float days)
        {
            int now = Now;
            if (noted.TryGetValue(key, out int t) && t <= now && now - t < days * GenDate.TicksPerDay) return false;
            noted[key] = now;
            if (noted.Count > 500) noted.Clear();
            return true;
        }

        /// <summary>What the colony will talk about: someone new, a new prisoner, traders at the door, a good harvest.</summary>
        private static void News(Map map, TalkTracker tr)
        {
            var scene = TalkScene.Of(map);
            if (scene == null) return;
            foreach (var (p, days) in scene.newcomers)
                if (days < 2f && NoteOnce("new|" + p.thingIDNumber, 30f))
                    tr.Note("Rimfolk_Topic_Newcomer", p.LabelShort, p.thingIDNumber, "arrived");
            var ids = new HashSet<int>(scene.prisoners.Concat(scene.slaves).Select(p => p.thingIDNumber));
            if (seenPrisoners != null)
                foreach (var p in scene.prisoners.Concat(scene.slaves))
                    if (!seenPrisoners.Contains(p.thingIDNumber) && NoteOnce("prisoner|" + p.thingIDNumber, 30f))
                        tr.Note("Rimfolk_Topic_Prisoners", p.LabelShort, p.thingIDNumber, p.IsSlave ? "slave" : "prisoner");
            seenPrisoners ??= new HashSet<int>();
            seenPrisoners.UnionWith(ids);
            if (seenPrisoners.Count > 500) seenPrisoners = ids;
            if (scene.traders && NoteOnce("traders|" + map.uniqueID, 2f)) tr.Note("Rimfolk_Topic_Outside", null, -1, "traders");
            // A harvest: the stores swell by a good few days at once.
            float food = scene.foodDays;
            if (lastFood.TryGetValue(map.uniqueID, out float was) && food >= was + 4f && food >= was * 1.2f && NoteOnce("harvest|" + map.uniqueID, 5f))
                tr.Note("Rimfolk_Topic_Food", null, -1, "harvest");
            if (scene.foodDays < 3f && NoteOnce("lowfood|" + map.uniqueID, 3f)) tr.Note("Rimfolk_Topic_Food", null, -1, "lowfood");
            lastFood[map.uniqueID] = food;
        }

        private static HashSet<ResearchProjectDef> finished;

        /// <summary>A study finished: the colony talks about the new and the old ways.</summary>
        private static void Research(TalkTracker tr)
        {
            var done = new HashSet<ResearchProjectDef>(DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(r => r.IsFinished));
            if (finished != null)
                foreach (var r in done)
                    if (!finished.Contains(r))
                    {
                        tr.Note("Rimfolk_Topic_Tech", r.LabelCap, -1, "research");
                        break;
                    }
            finished = done;
        }

        /// <summary>A decree enacted, a meeting held, a project chosen: the council is the talk of the colony.</summary>
        private static void Council(TalkTracker tr)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            int now = Now;
            foreach (var d in comp.decrees.active)
                if (d?.def != null && now - d.startTick < GenDate.TicksPerHour * 2 && NoteOnce("decree|" + d.def.defName + "|" + d.startTick, 30f))
                    tr.Note("Rimfolk_Topic_Council", d.def.label, -1, "decree");
            if (now - comp.ambitions.lastMeetingTick < GenDate.TicksPerHour * 2 && NoteOnce("meeting|" + comp.ambitions.lastMeetingTick, 30f))
                tr.Note("Rimfolk_Topic_Council", null, -1, "meeting");
            var amb = comp.ambitions.active;
            if (amb?.def != null && now - amb.startTick < GenDate.TicksPerHour * 2 && NoteOnce("project|" + amb.startTick, 30f))
                tr.Note("Rimfolk_Topic_Project", amb.def.label, -1, "chosen");
        }

        // A tame that ended with the animal turning on its tamer, read from the social log.
        private static int lastLogId = -1;
        private static FieldInfo fIntDef, fInitiator, fRecipient, fPacks;
        private static bool fieldsLooked;

        private static void TameLog()
        {
            var entries = Find.PlayLog?.AllEntries;
            if (entries == null) return;
            if (!fieldsLooked)
            {
                fieldsLooked = true;
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var t = typeof(PlayLogEntry_Interaction);
                fIntDef = t.GetField("intDef", flags);
                fInitiator = t.GetField("initiator", flags);
                fRecipient = t.GetField("recipient", flags);
                fPacks = t.GetField("extraSentencePacks", flags);
            }
            int max = lastLogId;
            bool first = lastLogId < 0;
            foreach (var e in entries)
            {
                if (e == null) continue;
                if (e.LogID > max) max = e.LogID;
                if (first || e.LogID <= lastLogId || !(e is PlayLogEntry_Interaction pi) || pi is PlayLogEntry_CouncilEvent) continue;
                if (fIntDef == null || fInitiator == null || fRecipient == null) break;
                if (fIntDef.GetValue(pi) as InteractionDef != InteractionDefOf.TameAttempt) continue;
                var tamer = fInitiator.GetValue(pi) as Pawn;
                var animal = fRecipient.GetValue(pi) as Pawn;
                var packs = fPacks?.GetValue(pi) as List<RulePackDef>;
                if (tamer == null || animal == null || !Talk.Eligible(tamer) || packs == null || !packs.Contains(RulePackDefOf.Sentence_RecruitAttemptRejected)) continue;
                var state = animal.MentalStateDef;
                if (state != MentalStateDefOf.Manhunter && state != MentalStateDefOf.ManhunterPermanent) continue;
                var map = tamer.MapHeld;
                var witness = map?.mapPawns.FreeColonistsSpawned.Where(o => o != tamer && Talk.Eligible(o) && o.Position.InHorDistOf(tamer.Position, 20f))
                    .OrderByDescending(o => o.relations?.OpinionOf(tamer) ?? 0).FirstOrDefault();
                if (witness != null) Mishap(TalkDefs.Tame, The(animal.kindDef?.label ?? animal.def.label), new List<Pawn> { tamer, witness });
            }
            lastLogId = max;
        }

        // ---------------- The day's look ----------------

        public static void Daily(TalkTracker tr)
        {
            if (!On) return;
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            // A teacher and their student, once the lessons have taken.
            Safe("mentors", () =>
            {
                foreach (var t in comp.social.ties.Where(t => t.kind == TieKind.Mentor && t.strength >= 30f && t.skill != null).ToList())
                    AddOnce(t.a, t.b, TalkDefs.Taught, t.skill.label, 1.5f);
            });
            // A secret confided and kept.
            Safe("secrets", () =>
            {
                foreach (var s in comp.secrets.secrets.Where(s => s != null && !s.revealed && s.owner != null && !s.owner.Dead).ToList())
                    foreach (var k in s.knowers.Where(k => k != null && !k.Dead).ToList())
                        AddOnce(s.owner, k, TalkDefs.Confided, "CC_TalkLabel_Secret".Translate().Resolve(), 1.5f);
            });
        }
    }

    /// <summary>A dilemma's answer or a social event that leaves a shared memory between A and B (an in-joke, a scare survived).</summary>
    public class DilemmaEffect_SharedMemory : DilemmaEffect
    {
        public SharedMemoryDef memory;
        [MustTranslate] public string label;
        public float weight = 1f;

        public override void Apply(DilemmaContext ctx)
        {
            // together: a social event between two who are near each other, or an answer that brings them together.
            if (memory == null || ctx?.a == null || ctx.b == null || !Persona.On) return;
            SharedMemories.Add(ctx.a, ctx.b, memory, label ?? memory.label, weight);
        }
    }
}
