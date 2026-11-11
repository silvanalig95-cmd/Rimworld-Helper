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
    /// The omens' build-up (brainstorm 23; docs/design/gods.md, "Weight"): days of signs before a god comes, each one on the
    /// map and remarked on aloud: the land's birds leaving it all at once, a strange calm (the wind dies for a few hours),
    /// two colonists waking from the same dream (Rimfolk's dreams: the dream book, the journal, the vivid dream's memory,
    /// and the two of them saying so when next together), a lamp or a torch in the colony that won't stay lit, and the
    /// animals restless (they start, run a few steps and stand looking one way). Each goes through the presence part's
    /// omen (GodPresenceSeam, PresenceKind.Omen: a soft sound, a glimmer, someone looking up, a line in the news). In
    /// Believed no glimmer: only the signs the colony reads into chance. <see cref="Before"/> is what the other parts call.
    /// </summary>
    public static class GodOmens
    {
        public const string RemarkPool = "Rimfolk_Pool_GodOmenRemark";
        public const string DreamPool = "Rimfolk_Pool_GodDream";
        public const string DreamTalkPool = "Rimfolk_Pool_GodDreamTalk";

        private static int Now => Find.TickManager.TicksGame;

        // ================= Planning =================

        /// <summary>
        /// Omens of <paramref name="god"/> over the next <paramref name="days"/> days at <paramref name="map"/>, before a coming
        /// (the stranger, the sacred animal, the worship's and the world's own): one a day, at most four, each of a kind that
        /// can show there, the last on the coming's eve. Returns how many were planned.
        /// </summary>
        public static int Before(God god, Map map, int days)
        {
            var b = GodsOnMapBook.Instance;
            if (b == null || god == null || map == null || days <= 0 || !Gods.On) return 0;
            int seed = GodsMath.Seed(god.id, Now / GenDate.TicksPerDay, map.uniqueID);
            var kinds = GodsOnMapMath.Omens(days, k => Can(map, k), seed);
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

        /// <summary>Whether an omen of this kind can show on the map at all (birds on the land, a light in the colony, two who sleep there).</summary>
        public static bool Can(Map map, OmenKind k)
        {
            switch (k)
            {
                case OmenKind.Birds: return Birds(map).Count > 0;
                case OmenKind.Candle: return Lights(map).Count > 0;
                case OmenKind.Dream: return GodsOnMapMath.DreamCan(Homes.People(map).Count(p => People.HasInnerLife(p) && !p.DevelopmentalStage.Baby()));
                case OmenKind.Restless: return map.mapPawns.AllPawnsSpawned.Count(p => p.RaceProps.Animal && !p.Downed) >= 2;
                default: return true;
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
                if (now < o.at) continue;
                bool shown = false;
                try
                {
                    shown = Show(o);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: an omen", e);
                }
                // An omen that can't show now (nobody awake to see it, no birds today) tries for a few hours, then lets go.
                if (shown || ++o.tries > 6) b.omens.RemoveAt(i);
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
                default:
                {
                    var animals = map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Animal && !p.Downed && !p.Dead && p.Awake() && !p.InMentalState && !GodAvatar.Is(p)).ToList();
                    if (animals.Count < 2) return false;
                    var edge = CellFinder.RandomEdgeCell(map);
                    at = animals.OrderByDescending(a => a.Faction == Faction.OfPlayer ? 1 : 0).First().Position;
                    foreach (var a in animals.OrderBy(a => a.Position.DistanceToSquared(at)).Take(8)) Restless(a, edge);
                    speaker = Witness(map, at, 25f);
                    break;
                }
            }
            Seen(god, map, at, o.kind, speaker);
            return true;
        }

        /// <summary>The presence's small sign where it shows (not in Believed), a line in the news, and a colonist who saw it says so.</summary>
        private static void Seen(God god, Map map, IntVec3 at, OmenKind kind, Pawn speaker)
        {
            // The sign put to its god at the step of how real the gods are ("Some say it is a sign of Ceren").
            string news = ("CC_GodM_Omen_" + kind).Translate(Homes.Name(map).Named("HOME")).Resolve() + " " + GodsOnMapWords.Sign(god);
            if (Gods.Realness != GodRealness.Believed)
            {
                var spec = PresenceSpec.For(god, map, PresenceKind.Omen, at);
                spec.text = news;
                if (!GodPresenceSeam.Call(spec)) Tell.Message(NewsKind.Land, Importance.Notable, map, news, new LookTargets(at, map), MessageTypeDefOf.NeutralEvent);
            }
            else Tell.Message(NewsKind.Land, Importance.Flavor, map, news, new LookTargets(at, map), MessageTypeDefOf.NeutralEvent);
            if (speaker == null) return;
            string line = Flavor.FromPool(RemarkPool, speaker, null, null, kind.ToString().ToLowerInvariant(), god.domain.ToString().ToLowerInvariant());
            if (!line.NullOrEmpty()) Speech.Say(speaker, line);
        }

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
            string raw = Flavor.FromPool(DreamPool, a, null, null, tag);
            if (raw.NullOrEmpty()) return false;
            foreach (var p in woke) Dreamt(comp, p, raw.Formatted(p.Named("PAWN")).Resolve());
            string text = "CC_GodM_Omen_Dream".Translate(a.Named("PAWN"), b.Named("OTHER"), raw.Formatted(a.Named("PAWN")).Resolve().Named("DREAM")).Resolve() + " "
                          + GodsOnMapWords.Sign(god);
            // apart: two dreams had the same night, each in their own bed; they speak of it below, together.
            Tell.Message(NewsKind.People, Importance.Notable, map, text, new LookTargets(new[] { a, b }.Select(x => new GlobalTargetInfo(x))), MessageTypeDefOf.NeutralEvent);
            if (Gods.Realness != GodRealness.Believed) GodPresenceSeam.Call(PresenceSpec.For(god, map, PresenceKind.Omen, a.Position));
            Together.When("godDream|" + a.thingIDNumber + "|" + b.thingIDNumber, a, b, () =>
            {
                string said = Flavor.FromPool(DreamTalkPool, a, null, null, tag);
                if (!said.NullOrEmpty()) Speech.Say(a, said.Formatted(b.Named("OTHER")).Resolve(), b);
            }, 18f, 6f);
            return true;
        }

        private static List<ThoughtDef> dreamThoughts;

        /// <summary>One dreamer's part: the dream book, the untold dream they will tell, the journal, the vivid dream's memory (one dream memory at a time).</summary>
        private static void Dreamt(CouncilGameComponent comp, Pawn p, string text)
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

        /// <summary>The lights guttering now: each goes out and comes back, by the tick it changes next and the tick it stops.</summary>
        private static readonly Dictionary<CompGlower, (Map map, int next, int until, bool lit)> guttering = new Dictionary<CompGlower, (Map, int, int, bool)>();
        private static Game gutterGame;

        /// <summary>A light that guttered out just now (the glower and the fire it draws are off). Read by the two hooks; nothing else.</summary>
        internal static bool Out(CompGlower c) => guttering.Count > 0 && c != null && guttering.TryGetValue(c, out var g) && !g.lit;

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
        private static int Gutter(Map map, ThingWithComps light, int ticks)
        {
            var c = light?.GetComp<CompGlower>();
            if (c == null || !c.Glows) return 0;
            if (gutterGame != Current.Game)
            {
                guttering.Clear();
                gutterGame = Current.Game;
            }
            int now = Now;
            guttering[c] = (map, now + Rand.Range(900, 1600), now + ticks, false);
            Toggle(c, map, false);
            GodsOnMapBook.Instance?.Due(now + 250);
            return 1;
        }

        private static void Toggle(CompGlower c, Map map, bool lit)
        {
            try
            {
                if (c.parent == null || !c.parent.Spawned) return;
                c.UpdateLit(map);
                map.mapDrawer.MapMeshDirty(c.parent.Position, MapMeshFlagDefOf.Things);
                if (!lit) FleckMaker.ThrowSmoke(c.parent.DrawPos, map, 0.8f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a light guttering", e);
            }
        }

        /// <summary>The guttering lights' steps: out, back, out again, until their time is over.</summary>
        internal static void Step(GodsOnMapBook b, int now)
        {
            if (guttering.Count == 0) return;
            if (gutterGame != Current.Game)
            {
                guttering.Clear();
                return;
            }
            foreach (var c in guttering.Keys.ToList())
            {
                var g = guttering[c];
                if (c.parent == null || c.parent.Destroyed || !c.parent.Spawned || now >= g.until)
                {
                    guttering.Remove(c);
                    if (c.parent != null && c.parent.Spawned) Toggle(c, g.map, true);
                    continue;
                }
                if (now < g.next) continue;
                bool lit = !g.lit;
                guttering[c] = (g.map, now + (lit ? Rand.Range(1200, 2400) : Rand.Range(700, 1400)), g.until, lit);
                Toggle(c, g.map, lit);
            }
            if (guttering.Count > 0) b.Due(now + 250);
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
