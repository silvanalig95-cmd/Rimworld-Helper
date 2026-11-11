using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// The omens' build-up (brainstorm 23; docs/design/gods.md, "Weight"): days of signs before a god comes or acts, each
    /// one on the map and remarked on aloud, and each god's own (GodShapeDef.omens; the five any god may send otherwise):
    /// <list type="bullet">
    /// <item>any god's: the land's birds leaving it all at once, a strange calm (the wind dies), two colonists waking from
    /// the same dream (Rimfolk's dreams: the dream book, the journal, the vivid dream's memory, and the two of them saying
    /// so when next together), a light that won't stay lit, the animals restless;</item>
    /// <item>Hollin's: leaves and seeds drifting toward one place, an anima tree humming, her dryads standing still;
    /// Smoulder's: a vast winged shadow at noon with a wind under it, steam from the ground, the rock rumbling where the
    /// colony mines deep; Morel's: a ring of mushrooms overnight, a smell of earth where no rain fell, white threads on a
    /// floor; Pip's: lights flickering in a pattern, a comms console's static, a light glowing with no power; Cirrus's: a
    /// shadow too large at noon, a star that moves the wrong way; Hush's: frost in lines like writing, a silence, a white
    /// figure at the edge of the night; Undertow's: a dark a light doesn't reach, a sweet voice to the most desperate.</item>
    /// </list>
    /// The player: the gods are an ominous presence, never explained. So an omen is only seen and said: no line in the
    /// news, no god named; the presence part's small sign where it shows (GodPresenceSeam, PresenceKind.Omen: a soft
    /// sound, a glimmer, someone looking up), never in Believed. <see cref="Before"/> is what the other parts call.
    /// </summary>
    public static partial class GodOmens
    {
        public const string RemarkPool = "Rimfolk_Pool_GodOmenRemark";
        public const string DreamPool = "Rimfolk_Pool_GodDream";
        public const string DreamTalkPool = "Rimfolk_Pool_GodDreamTalk";

        private static int Now => Find.TickManager.TicksGame;

        // ================= Planning =================

        /// <summary>
        /// Omens of <paramref name="god"/> over the next <paramref name="days"/> days at <paramref name="map"/>, before a coming
        /// (the stranger, the sacred animal, a rising, the worship's and the world's own): one a day, at most four, each of
        /// its own kinds (its shape's) that can show there, the last on the coming's eve. Returns how many were planned.
        /// </summary>
        public static int Before(God god, Map map, int days)
        {
            var b = GodsOnMapBook.Instance;
            if (b == null || god == null || map == null || days <= 0 || !Gods.On) return 0;
            int seed = GodsMath.Seed(god.id, Now / GenDate.TicksPerDay, map.uniqueID);
            var kinds = GodsOnMapMath.Omens(days, k => Can(map, k), seed, GodShapes.Omens(god));
            int start = Now - Now % GenDate.TicksPerDay;
            int n = 0;
            for (int i = 0; i < kinds.Count && b.omens.Count < GodsOnMapBook.MaxOmens; i++)
            {
                int at = start + GodsOnMapMath.OmenAt(i, kinds.Count, days, kinds[i], seed);
                if (at < Now) at += GenDate.TicksPerDay;
                b.omens.Add(new GodOmenNote { god = god.id, map = map, kind = kinds[i], at = at });
                n++;
            }
            return n;
        }

        /// <summary>Whether an omen of this kind can show on the map at all (birds on the land, a light in the colony, two who sleep there, an anima tree...).</summary>
        public static bool Can(Map map, OmenKind k)
        {
            switch (k)
            {
                case OmenKind.Birds: return Birds(map).Count > 0;
                case OmenKind.Candle: return Lights(map).Count > 0;
                case OmenKind.Dream: return GodsOnMapMath.DreamCan(Homes.People(map).Count(p => People.HasInnerLife(p) && !p.DevelopmentalStage.Baby()));
                case OmenKind.Restless: return map.mapPawns.AllPawnsSpawned.Count(p => p.RaceProps.Animal && !p.Downed) >= 2;
                case OmenKind.Calm: return true;
                default: return CanOwn(map, k);
            }
        }

        // ================= Their hour =================

        internal static void Hourly(GodsOnMapBook b, int now)
        {
            for (int i = b.omens.Count - 1; i >= 0; i--)
            {
                var o = b.omens[i];
                if (o == null || o.map == null || !Homes.IsHome(o.map) || Gods.ById(o.god) == null)
                {
                    b.omens.RemoveAt(i);
                    continue;
                }
                // On a night with no moon, the void's signs come a little more easily (a little early).
                if (now < o.at && !(Undertow(o.kind) && GodOmens.MoonTonight == GodsOnMapMath.MoonFace.Gone && now >= o.at - GodsOnMapMath.DarkNightEarlyHours * GenDate.TicksPerHour))
                    continue;
                bool shown = false;
                try
                {
                    shown = Show(o);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: an omen", e);
                }
                // An omen that can't show now (nobody awake to see it, no birds today, not yet dark) tries for a few hours, then lets go.
                if (shown || ++o.tries > 6) b.omens.RemoveAt(i);
            }
            Safe(() => Deep(b, now));
        }

        private static void Safe(Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a sign", e);
            }
        }

        /// <summary>Shows an omen now. False when it can't show this hour.</summary>
        public static bool Show(GodOmenNote o)
        {
            var god = Gods.ById(o.god);
            var map = o.map;
            if (god == null || map == null || MapFacts.ThreatOn(map)) return false;
            IntVec3 at;
            Pawn speaker;
            switch (o.kind)
            {
                case OmenKind.Birds:
                {
                    var birds = Birds(map);
                    if (birds.Count == 0) return false;
                    at = birds[0].Position;
                    if (Leave(map, a => birds.Contains(a), 12) == 0) return false;
                    speaker = Witness(map, at, 30f);
                    break;
                }
                case OmenKind.Calm:
                {
                    if (!Calm(map)) return false;
                    speaker = map.mapPawns.FreeColonistsSpawned.Where(p => Awake(p) && !p.Position.Roofed(map)).RandomElementWithFallback();
                    if (speaker == null) return false;
                    at = speaker.Position;
                    break;
                }
                case OmenKind.Dream:
                    return Dream(god, map);
                case OmenKind.Candle:
                {
                    var lit = Lights(map).OrderByDescending(l => Colonists(map, l.Position, 12f)).FirstOrDefault();
                    if (lit == null || Gutter(map, lit, GenDate.TicksPerHour * 3) == 0) return false;
                    at = lit.Position;
                    speaker = Witness(map, at, 14f);
                    break;
                }
                case OmenKind.Restless:
                {
                    var animals = map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Animal && !p.Downed && !p.Dead && p.Awake() && !p.InMentalState && !GodAvatar.Is(p)).ToList();
                    if (animals.Count < 2) return false;
                    var edge = CellFinder.RandomEdgeCell(map);
                    at = animals.OrderByDescending(a => a.Faction == Faction.OfPlayer ? 1 : 0).First().Position;
                    foreach (var a in animals.OrderBy(a => a.Position.DistanceToSquared(at)).Take(8)) Restless(a, edge);
                    speaker = Witness(map, at, 25f);
                    break;
                }
                default:
                    if (!ShowOwn(god, map, o.kind, out at, out speaker)) return false;
                    break;
            }
            Seen(god, map, at, o.kind, speaker);
            return true;
        }

        /// <summary>
        /// The presence's small sign where it shows (not in Believed, and never a line in the news), and a colonist who saw
        /// it says so in a bubble: what they make of it, never whose it was.
        /// </summary>
        internal static void Seen(God god, Map map, IntVec3 at, OmenKind kind, Pawn speaker)
        {
            if (Gods.Realness != GodRealness.Believed && at.IsValid) GodPresenceSeam.Call(new PresenceSpec { god = god, map = map, cell = at, kind = PresenceKind.Omen, veiled = true });
            // The colony's book keeps it in the words of who saw it (or of someone at home, for a sign nobody stood near).
            GodsOnMapWords.Saw(god, map, at, GodActKind.Omen, speaker, "CC_GodM_Saw_" + kind);
            if (speaker == null) return;
            // At night, the moon's face is in what they say of it.
            string face = map.skyManager.CurSkyGlow < 0.3f ? GodReturn.FaceTag() : null;
            string line = Flavor.FromPool(RemarkPool, speaker, null, null, kind.ToString().ToLowerInvariant(), god.domain.ToString().ToLowerInvariant(), GodShapes.LineTag(god), face);
            if (!line.NullOrEmpty()) Speech.Say(speaker, line);
        }

        private static bool Undertow(OmenKind k) => k == OmenKind.Dark || k == OmenKind.Whisper;

        private static bool Awake(Pawn p) => p != null && p.Awake() && !p.Downed && !p.InMentalState && !p.Drafted && People.HasInnerLife(p);

        /// <summary>A colonist awake within <paramref name="range"/> who can see the place.</summary>
        private static Pawn Witness(Map map, IntVec3 at, float range) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => Awake(p) && p.Position.InHorDistOf(at, range) && GenSight.LineOfSight(p.Position, at, map, true))
                .OrderBy(p => p.Position.DistanceToSquared(at)).FirstOrDefault();

        private static int Colonists(Map map, IntVec3 at, float range) => map.mapPawns.FreeColonistsSpawned.Count(p => p.Position.InHorDistOf(at, range));

        // ================= The birds =================

        /// <summary>The land's wild birds, up and about (the game's and the mods' birds: a bird's body).</summary>
        private static List<Pawn> Birds(Map map) =>
            map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == null && p.RaceProps.Animal && !p.Downed && !p.Dead && !p.InMentalState && !GodAvatar.Is(p)
                                                    && (p.RaceProps.body?.defName ?? "").IndexOf("Bird", StringComparison.OrdinalIgnoreCase) >= 0).ToList();

        /// <summary>Up to <paramref name="most"/> wild animals that <paramref name="which"/> takes leave the map together, at a run. How many go.</summary>
        public static int Leave(Map map, Func<Pawn, bool> which, int most)
        {
            if (map == null) return 0;
            var going = map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == null && p.RaceProps.Animal && !p.Downed && !p.Dead && !p.InMentalState && !GodAvatar.Is(p)
                                                                && p.GetLord() == null && which(p)).Take(most).ToList();
            if (going.Count == 0) return 0;
            try
            {
                LordMaker.MakeNewLord(null, new LordJob_ExitMapBest(LocomotionUrgency.Jog, false, false), map, going);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the animals leaving", e);
                return 0;
            }
            return going.Count;
        }

        // ================= The calm =================

        /// <summary>A strange calm: the wind dies for a few hours, the plants stop swaying ("a strange stillness", the presence's own sky barely tinted).</summary>
        private static bool Calm(Map map)
        {
            var def = DefDatabase<GameConditionDef>.GetNamedSilentFail(GameCondition_GodPresence.DefName);
            if (def == null || map.gameConditionManager.GetActiveCondition<GameCondition_GodPresence>() != null) return false;
            var cond = (GameCondition_GodPresence)GameConditionMaker.MakeCondition(def, GenDate.TicksPerHour * Rand.RangeInclusive(4, 6));
            cond.tint = Color.white;
            cond.tintStrength = 0.06f;
            cond.darken = 0f;
            cond.wind = 0.05f;
            cond.veiled = true;
            map.gameConditionManager.RegisterCondition(cond);
            return true;
        }

        // ================= The dream =================

        /// <summary>
        /// Two colonists who woke from a night's sleep this morning had the same dream of the god (its domain's images): each
        /// in the dream book and their journal with the vivid dream's memory, one line in the news for both, and the two of
        /// them saying so when next together. False when two haven't woken yet.
        /// </summary>
        private static bool Dream(God god, Map map)
        {
            var comp = CouncilGameComponent.Instance;
            var dreams = comp?.dreams;
            if (dreams == null) return false;
            var woke = Homes.People(map).Where(p => p.Spawned && Awake(p) && dreams.WokeFromNight(p)).OrderBy(_ => Rand.Value).Take(2).ToList();
            if (woke.Count < 2) return false;
            var a = woke[0];
            var b = woke[1];
            string tag = god.domain.ToString().ToLowerInvariant();
            string own = GodShapes.LineTag(god);
            // On a full moon the dreams are truest: its own lines then.
            string raw = Flavor.FromPool(DreamPool, a, null, null, tag, own, GodReturn.FaceTag());
            if (raw.NullOrEmpty()) return false;
            foreach (var p in woke) Dreamt(comp, p, raw.Formatted(p.Named("PAWN")).Resolve());
            string text = "CC_GodM_Omen_Dream".Translate(a.Named("PAWN"), b.Named("OTHER"), raw.Formatted(a.Named("PAWN")).Resolve().Named("DREAM")).Resolve();
            // apart: two dreams had the same night, each in their own bed; they speak of it below, together.
            Tell.Message(NewsKind.People, Importance.Notable, map, text, new LookTargets(new[] { a, b }.Select(x => new GlobalTargetInfo(x))), MessageTypeDefOf.NeutralEvent);
            if (Gods.Realness != GodRealness.Believed) GodPresenceSeam.Call(new PresenceSpec { god = god, map = map, cell = a.Position, kind = PresenceKind.Omen, veiled = true });
            GodsOnMapWords.Saw(god, map, a.Position, GodActKind.Omen, a, "CC_GodM_Saw_Dream", b.LabelShort.Named("OTHER"));
            Together.When("godDream|" + a.thingIDNumber + "|" + b.thingIDNumber, a, b, () =>
            {
                string said = Flavor.FromPool(DreamTalkPool, a, null, null, tag, own);
                if (!said.NullOrEmpty()) Speech.Say(a, said.Formatted(b.Named("OTHER")).Resolve(), b);
            }, 18f, 6f);
            return true;
        }

        private static List<ThoughtDef> dreamThoughts;

        /// <summary>One dreamer's part: the dream book, the untold dream they will tell, the journal, the vivid dream's memory (one dream memory at a time).</summary>
        internal static void Dreamt(CouncilGameComponent comp, Pawn p, string text)
        {
            var dreams = comp.dreams;
            int now = Now;
            dreams.recent.Add(new DreamRecord { pawn = p, name = p.LabelShort, text = text, kind = DreamKind.Omen, tick = now });
            while (dreams.recent.Count > 80) dreams.recent.RemoveAt(0);
            dreams.untold[p] = new DreamTracker.DreamMemo { kind = DreamKind.Omen, tick = now };
            comp.journals?.WriteText(p, text);
            var td = CouncilDefOf.Council_Dream_Omen;
            var memories = p.needs?.mood?.thoughts?.memories;
            if (td == null || memories == null || !(ColonyCouncilMod.Settings?.MoodEffects ?? true)) return;
            dreamThoughts ??= DefDatabase<ThoughtDef>.AllDefsListForReading.Where(d => d.ThoughtClass == typeof(Thought_Dream)).ToList();
            foreach (var d in dreamThoughts) memories.RemoveMemoriesOfDef(d);
            if (!(ThoughtMaker.MakeThought(td) is Thought_Memory m)) return;
            if (m is Thought_Dream dream) dream.dream = text;
            memories.TryGainMemory(m, null);
        }

        // ================= The light that won't stay lit =================

        /// <summary>
        /// The lights a god plays with now: each goes out and comes back (guttering, or Pip's fast flicker in a pattern), or
        /// glows with no power (Pip, to show it can); by the tick it changes next and the tick it stops.
        /// </summary>
        private static readonly Dictionary<CompGlower, (Map map, int next, int until, bool lit, bool fast, bool forced)> guttering =
            new Dictionary<CompGlower, (Map, int, int, bool, bool, bool)>();
        private static Game gutterGame;

        /// <summary>A light that guttered out just now (the glower and the fire it draws are off). Read by the two hooks; nothing else.</summary>
        internal static bool Out(CompGlower c) => guttering.Count > 0 && c != null && guttering.TryGetValue(c, out var g) && !g.lit && !g.forced;

        /// <summary>A light that glows with no power because a god wants it to (Pip). Read by the glower's hook.</summary>
        internal static bool Lit(CompGlower c) => guttering.Count > 0 && c != null && guttering.TryGetValue(c, out var g) && g.forced;

        internal static bool Out(ThingWithComps t)
        {
            if (guttering.Count == 0 || t == null) return false;
            var c = t.GetComp<CompGlower>();
            return c != null && Out(c);
        }

        /// <summary>The colony's lit lights on the home ground (a lamp, a torch, a candle of a mod's): what has a glower and glows.</summary>
        private static List<ThingWithComps> Lights(Map map) =>
            map.listerBuildings.allBuildingsColonist.OfType<ThingWithComps>().Where(b => b.Spawned && map.areaManager.Home[b.Position] && b.GetComp<CompGlower>() is CompGlower g && g.Glows)
                .ToList();

        /// <summary>Up to <paramref name="most"/> of the colony's lights gutter for <paramref name="ticks"/> (a god's hearth withheld). How many.</summary>
        public static int Gutter(Map map, int most, int ticks)
        {
            if (map == null) return 0;
            int n = 0;
            foreach (var l in Lights(map).OrderBy(_ => Rand.Value).Take(most)) n += Gutter(map, l, ticks);
            return n;
        }

        /// <summary>One light goes out with a puff of smoke, comes back a little later, and goes out again, for <paramref name="ticks"/>.</summary>
        private static int Gutter(Map map, ThingWithComps light, int ticks) => Play(map, light, ticks, false, false);

        /// <summary>
        /// A light played with for <paramref name="ticks"/>: guttering out and back, flickering fast in a pattern
        /// (<paramref name="fast"/>), or glowing with no power (<paramref name="forced"/>, a light that is out). 1 when it is.
        /// </summary>
        private static int Play(Map map, ThingWithComps light, int ticks, bool fast, bool forced)
        {
            var c = light?.GetComp<CompGlower>();
            if (c == null || c.Glows == forced) return 0;
            if (gutterGame != Current.Game)
            {
                guttering.Clear();
                gutterGame = Current.Game;
            }
            int now = Now;
            int next = forced ? now + ticks : fast ? now + 60 : now + Rand.Range(900, 1600);
            guttering[c] = (map, next, now + ticks, false, fast, forced);
            Toggle(c, map, forced);
            GodsOnMapBook.Instance?.Due(Math.Min(next, now + 250));
            return 1;
        }

        private static void Toggle(CompGlower c, Map map, bool lit)
        {
            try
            {
                if (c.parent == null || !c.parent.Spawned) return;
                c.UpdateLit(map);
                map.mapDrawer.MapMeshDirty(c.parent.Position, MapMeshFlagDefOf.Things);
                if (!lit && c.parent.GetComp<CompRefuelable>() != null) FleckMaker.ThrowSmoke(c.parent.DrawPos, map, 0.8f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a light guttering", e);
            }
        }

        /// <summary>The lights' steps: out, back, out again (fast for a flicker), until their time is over; a forced one goes dark again.</summary>
        internal static void Step(GodsOnMapBook b, int now)
        {
            if (guttering.Count == 0) return;
            if (gutterGame != Current.Game)
            {
                guttering.Clear();
                return;
            }
            int soonest = int.MaxValue;
            foreach (var c in guttering.Keys.ToList())
            {
                var g = guttering[c];
                if (c.parent == null || c.parent.Destroyed || !c.parent.Spawned || now >= g.until)
                {
                    guttering.Remove(c);
                    if (c.parent != null && c.parent.Spawned) Toggle(c, g.map, true);
                    continue;
                }
                if (now >= g.next && !g.forced)
                {
                    bool lit = !g.lit;
                    int wait = g.fast ? (lit ? Rand.Range(40, 140) : Rand.Range(30, 90)) : lit ? Rand.Range(1200, 2400) : Rand.Range(700, 1400);
                    guttering[c] = (g.map, now + wait, g.until, lit, g.fast, false);
                    Toggle(c, g.map, lit);
                    g = guttering[c];
                }
                soonest = Math.Min(soonest, g.forced ? g.until : Math.Min(g.next, g.until));
            }
            if (guttering.Count > 0) b.Due(Math.Max(now + 1, soonest));
        }

        // ================= The animals restless =================

        /// <summary>An animal starts, runs a few steps, and stands looking one way (toward where the god will come from).</summary>
        private static void Restless(Pawn a, IntVec3 toward)
        {
            try
            {
                if (a.CurJob != null && !a.CurJob.def.casualInterruptible || a.roping?.IsRoped == true) return;
                var flee = CellFinder.RandomClosewalkCellNear(a.Position, a.Map, 5);
                var run = JobMaker.MakeJob(JobDefOf.Goto, flee);
                run.locomotionUrgency = LocomotionUrgency.Sprint;
                run.expiryInterval = 300;
                a.jobs.StartJob(run, JobCondition.InterruptForced);
                var look = DefDatabase<JobDef>.GetNamedSilentFail(GodReactions.BeholdJob);
                if (look == null) return;
                var stand = JobMaker.MakeJob(look, toward);
                stand.count = Rand.Range(300, 600);
                stand.expiryInterval = stand.count + 120;
                a.jobs.jobQueue.EnqueueLast(stand);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: an animal restless", e);
            }
        }
    }
}
