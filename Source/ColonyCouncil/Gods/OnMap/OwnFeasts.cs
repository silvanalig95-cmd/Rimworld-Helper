using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// The festival that plays out today (OwnHappeningsBook.feast; Gods/OnMap/OwnFeasts.cs): whose, where, how far along,
    /// who was sent and who has done their part ("id|id"), what was counted, the lamps lit and doors shut for the night (put
    /// back at dawn), and Hollin's count of the trees felled since her last Greening.
    /// </summary>
    public class FeastState : IExposable
    {
        public string god;
        public Map map;
        /// <summary>The day it was kept (GenDate.DaysPassed), the step it is at (0 waiting for its hour), when the step ends.</summary>
        public int day = -1, stage, until = -1;
        public IntVec3 at = IntVec3.Invalid;
        public Pawn lead, other;
        public Thing thing;
        public string sent, done, sat, what, what2;
        public int count, count2, count3, ate;
        /// <summary>The night's round: lamps switched on and torches topped up; the machines walked round were work benches; the night told.</summary>
        public int lamps, torches;
        public bool fallback, benches, told;
        /// <summary>Lamps switched on and doors let shut for the night, put back at dawn if nobody changed them since.</summary>
        public List<Thing> lit = new List<Thing>(), doors = new List<Thing>();
        /// <summary>Trees the colony's people felled since Hollin's last Greening.</summary>
        public int felled;

        public bool On => god != null;

        public void Clear()
        {
            god = null;
            map = null;
            day = until = -1;
            stage = count = count2 = count3 = ate = lamps = torches = 0;
            benches = told = false;
            at = IntVec3.Invalid;
            lead = other = null;
            thing = null;
            sent = done = sat = what = what2 = null;
            fallback = false;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref god, "god");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref day, "day", -1);
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref until, "until", -1);
            Scribe_Values.Look(ref at, "at", IntVec3.Invalid);
            Scribe_References.Look(ref lead, "lead");
            Scribe_References.Look(ref other, "other");
            Scribe_References.Look(ref thing, "thing");
            Scribe_Values.Look(ref sent, "sent");
            Scribe_Values.Look(ref done, "done");
            Scribe_Values.Look(ref sat, "sat");
            Scribe_Values.Look(ref ate, "ate");
            Scribe_Values.Look(ref what, "what");
            Scribe_Values.Look(ref what2, "what2");
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref count2, "count2");
            Scribe_Values.Look(ref count3, "count3");
            Scribe_Values.Look(ref fallback, "fallback");
            Scribe_Values.Look(ref lamps, "lamps");
            Scribe_Values.Look(ref torches, "torches");
            Scribe_Values.Look(ref benches, "benches");
            Scribe_Values.Look(ref told, "told");
            Scribe_Collections.Look(ref lit, "lit", LookMode.Reference);
            Scribe_Collections.Look(ref doors, "doors", LookMode.Reference);
            Scribe_Values.Look(ref felled, "felled");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                lit ??= new List<Thing>();
                doors ??= new List<Thing>();
                lit.RemoveAll(t => t == null || t.Destroyed);
                doors.RemoveAll(t => t == null || t.Destroyed);
                if (god != null && map == null) Clear();
            }
        }
    }

    /// <summary>
    /// Each god's festival kept as its words say (docs/content-update-proposals.md, C1; the player's go of 20 November 2026).
    /// The eight festivals that kept only the shared feast now play out on the map, each as its words say (festivalDishes):
    /// <list type="bullet">
    /// <item>Hollin's Greening: a sapling set by hand for each tree the colony felled since the last Greening, and at noon
    /// a meal eaten together under the colony's oldest tree.</item>
    /// <item>Smoulder's Deep Silver: silver carried from the stores and dropped down the deepest shaft, or a crack in the rock
    /// where the colony has no mine, and the mountain's names said over it.</item>
    /// <item>Bellwether's Letting Pass: the herd walked out to the long grass at the colony's edge and left to graze it, and at
    /// dusk the eldest tells the old story to the children there.</item>
    /// <item>Morel's Turning Under: hay or raw scraps carried out to the field and dug under (a few squares turn rich soil),
    /// and a pot of fungus meals cooked from the stores' mushrooms for the feast.</item>
    /// <item>Pip's First Spark: a broken machine fixed (the game's own repair), the machines walked round one by one (the work
    /// benches where there are none), and at dusk every lamp lit and left burning till dawn.</item>
    /// <item>Hush's Stillness: a silent supper of the plainest meal in the stores, the sick and the old seated nearest the
    /// fire.</item>
    /// <item>Undertow's Shuttered Night: at dusk someone goes round shutting the doors held open and lighting every lamp,
    /// torches topped up with fuel, all put back at dawn.</item>
    /// <item>Selene's Longest Night: a watch kept on the walls from dusk till dawn (the night watch's own play-out, with its
    /// cold), and the dreams told over breakfast.</item>
    /// </list>
    /// Every age has its own form: torches topped up where there are no lamps, a crack in the rock where there is no mine,
    /// the work benches where there are no machines. What happened is told when it is done, as it was seen (a message, and the
    /// colony's book under the god it was kept for); the festival's god is named only as its festival is (the calendar's
    /// own name). Nobody is hurt; the watch's cold is the night watch's (Harm/ColdHurts.cs).
    /// </summary>
    public static class OwnFeasts
    {
        public const string Pool = "Rimfolk_Pool_GodFeast_Say";

        private static int Now => Find.TickManager.TicksGame;

        private static FeastState S => OwnHappenings.Book?.feast;

        public static bool Keeps(God g) => g != null && OwnFeastsMath.Keeps(g.Key);

        public static JobDef PlantJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_Feast_Plant");
        public static JobDef DigJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_Feast_Dig");
        public static JobDef CookJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_Feast_Cook");
        public static JobDef MachineJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_Feast_Machine");
        public static JobDef LampJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_Feast_Lamp");

        // ================= Kept =================

        /// <summary>The festival kept today at a home (OwnHappenings.FestivalKept): its custom waits for its hour.</summary>
        public static void Begin(OwnHappeningsBook b, God g, Map map)
        {
            var s = b?.feast;
            if (s == null || g == null || map == null) return;
            // One at a time: a custom still under way (the night's lamps) is put back first.
            if (s.On) Finish(b, s, false);
            s.Clear();
            s.god = g.Key;
            s.map = map;
            s.day = GenDate.DaysPassed;
            s.stage = 0;
            b.Due(Now + 250);
        }

        /// <summary>A tree felled by the colony's people (OwnHappenings.Felled): counted for the next Greening.</summary>
        internal static void Felled(OwnHappeningsBook b)
        {
            var s = b?.feast;
            if (s != null && s.felled < 9999) s.felled++;
        }

        // ================= The hour and the steps =================

        /// <summary>The gods' hour (OwnHappenings.Hourly): a custom waiting for its hour begins; the night's parts at dusk and dawn.</summary>
        internal static void Hourly(OwnHappeningsBook b, int now)
        {
            var s = b.feast;
            if (s == null || !s.On) return;
            var map = s.map;
            var g = OwnHappenings.God(s.god);
            if (map == null || !Homes.IsHome(map) || g == null)
            {
                Finish(b, s, false);
                return;
            }
            int hour = GenLocalDate.HourOfDay(map);
            int today = GenDate.DaysPassed;
            if (s.stage == 0)
            {
                // Kept too late in the day for its custom: held the next day at its hour; after that it is let be.
                if (!OwnFeastsMath.StillWaiting(s.day, today))
                {
                    s.Clear();
                    return;
                }
                if (!OwnFeastsMath.BeginNow(s.god, hour) || MapFacts.ThreatOn(map)) return;
                bool begun = false;
                // Its day is the day the custom began, for the night's parts and the morning after.
                s.day = today;
                OwnHappenings.Safe("a festival's custom", () => begun = Start(b, s, g, map, now));
                if (!begun) s.Clear();
                else b.Due(now + 250);
                return;
            }
            OwnHappenings.Safe("a festival's hour", () => HourOf(b, s, g, map, hour, today, now));
        }

        /// <summary>The steps between the hours (OwnHappenings.Step).</summary>
        internal static void Step(OwnHappeningsBook b, int now)
        {
            var s = b.feast;
            if (s == null || !s.On || s.stage == 0) return;
            var g = OwnHappenings.God(s.god);
            if (s.map == null || g == null) return;
            OwnHappenings.Safe("a festival's steps", () => StepOf(b, s, g, s.map, now));
            if (s.On) b.Due(now + 250);
        }

        private static bool Start(OwnHappeningsBook b, FeastState s, God g, Map map, int now)
        {
            switch (s.god)
            {
                case "Hollin": return HollinPlant(s, g, map, now);
                case "Smoulder": return SmoulderCarry(s, g, map, now);
                case "Bellwether": return BellwetherOut(s, g, map, now);
                case "Morel": return MorelCarry(s, g, map, now);
                case "Pip": return PipMend(s, g, map, now);
                case "Hush": return HushSupper(s, g, map, now);
                case "Undertow": return Round(s, g, map, now, true);
                case "Selene": return SeleneWatch(s, g, map, now);
                default: return false;
            }
        }

        private static void HourOf(OwnHappeningsBook b, FeastState s, God g, Map map, int hour, int today, int now)
        {
            switch (s.god)
            {
                case "Hollin":
                    if (s.stage == 2 && hour >= OwnFeastsMath.MealHour) HollinMeal(b, s, g, map, now);
                    break;
                case "Bellwether":
                    if (s.stage == 1 && hour >= OwnFeastsMath.StoryHour) BellwetherStory(s, g, map, now);
                    break;
                case "Pip":
                    if (s.stage == 3 && hour >= OwnFeastsMath.DuskHour) Round(s, g, map, now, false);
                    else if (s.stage == 4 && today > s.day && hour >= OwnFeastsMath.DawnHour) Dawn(b, s, g, map);
                    break;
                case "Undertow":
                    if (s.stage == 4 && !s.told && (hour >= 22 || today > s.day)) UndertowNight(s, g, map);
                    else if (s.stage == 4 && today > s.day && hour >= OwnFeastsMath.DawnHour) Dawn(b, s, g, map);
                    break;
                case "Selene":
                    if (s.stage == 1 && s.fallback && (hour >= 20 || hour < 5)) SeleneStand(s, map);
                    if (s.stage == 1 && today > s.day && OwnFeastsMath.Breakfast(hour))
                    {
                        s.stage = 2;
                        s.until = now + GenDate.TicksPerHour * 5;
                        s.done = null;
                    }
                    break;
            }
            // Nothing is left waiting past its day and the next.
            if (s.On && today > s.day + 1) Finish(b, s, true);
        }

        private static void StepOf(OwnHappeningsBook b, FeastState s, God g, Map map, int now)
        {
            switch (s.god)
            {
                case "Hollin":
                    if (s.stage == 1 && (now > s.until || !Busy(s, map, PlantJob))) s.stage = 2;
                    else if (s.stage == 3) MealStep(b, s, g, map, now);
                    break;
                case "Smoulder":
                    SmoulderStep(b, s, g, map, now);
                    break;
                case "Bellwether":
                    if (s.stage == 2) StoryStep(b, s, g, map, now);
                    break;
                case "Morel":
                    MorelStep(b, s, g, map, now);
                    break;
                case "Pip":
                    PipStep(s, g, map, now);
                    break;
                case "Hush":
                    if (s.stage == 1) SupperStep(b, s, g, map, now);
                    break;
                case "Selene":
                    if (s.stage == 2) Dreams(b, s, g, map, now);
                    break;
            }
        }

        /// <summary>It is over: the night's lamps and doors put back (a dawn that came), the state let go.</summary>
        private static void Finish(OwnHappeningsBook b, FeastState s, bool putBack)
        {
            if (putBack || s.lit.Count > 0 || s.doors.Count > 0) PutBack(s);
            int felled = s.felled;
            s.Clear();
            s.felled = felled;
        }

        // ================= Who =================

        /// <summary>Someone who holds this god: the god they pray to, or one they hold at least plainly (not a scoffer).</summary>
        public static bool Holds(Pawn p, God g)
        {
            if (p == null || g == null || !Worship.Ours(p) || Worship.Scoffer(p)) return false;
            if (Worship.GodOf(p) == g) return true;
            var w = PawnGods.WordOf(p, g);
            return w == BeliefWord.Holds || w == BeliefWord.Devoted || w == BeliefWord.Fervent;
        }

        /// <summary>Who takes part: those free now who hold the god first (the priest among them), then anyone free; grown only where <paramref name="grown"/>.</summary>
        private static List<Pawn> Takers(Map map, God g, int n, bool grown, Func<Pawn, float> best = null)
        {
            var free = map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Free(p) && !p.DevelopmentalStage.Baby() && (!grown || !p.DevelopmentalStage.Juvenile())).ToList();
            return free.OrderByDescending(p => Holds(p, g) ? 2 : Worship.IsPriest(p) ? 1 : 0).ThenByDescending(p => best?.Invoke(p) ?? 0f).Take(n).ToList();
        }

        private static float Skill(Pawn p, SkillDef s) => p.skills?.GetSkill(s)?.Level ?? 0;

        /// <summary>Anyone sent still at the job.</summary>
        private static bool Busy(FeastState s, Map map, JobDef job) =>
            job != null && map.mapPawns.FreeColonistsSpawned.Any(p => HeliosState.Has(s.sent, p) && p.CurJobDef == job);

        private static List<Pawn> Sent(FeastState s, Map map) => map.mapPawns.FreeColonistsSpawned.Where(p => HeliosState.Has(s.sent, p)).ToList();

        private static string Names(IEnumerable<Pawn> pawns) => TextUtil.JoinAnd(pawns.Where(p => p != null).Select(p => p.LabelShort).ToList());

        // ================= On the map =================

        /// <summary>A job of the feast's own at a target and those after it, a while at each (Job.count ticks).</summary>
        private static bool Work(Pawn p, JobDef def, List<LocalTargetInfo> targets, int ticks)
        {
            if (p == null || def == null || targets == null || targets.Count == 0 || !p.Spawned) return false;
            var job = JobMaker.MakeJob(def, targets[0]);
            job.count = Mathf.Max(60, ticks);
            job.locomotionUrgency = LocomotionUrgency.Walk;
            if (targets.Count > 1) job.targetQueueA = targets.Skip(1).ToList();
            return OwnHappenings.Job(p, job);
        }

        /// <summary>One of a stack from the stores into their hand, then <paramref name="then"/>.</summary>
        private static bool TakeThen(Pawn p, Thing stack, int count, Job then)
        {
            if (p == null || then == null) return false;
            try
            {
                if (stack != null && stack.Spawned && count > 0 && p.CanReserveAndReach(stack, PathEndMode.ClosestTouch, Danger.Some))
                {
                    var take = JobMaker.MakeJob(JobDefOf.TakeInventory, stack);
                    take.count = Mathf.Min(count, stack.stackCount);
                    p.jobs.StartJob(take, JobCondition.InterruptForced, null, true);
                    p.jobs.jobQueue.EnqueueLast(then);
                    return true;
                }
                return OwnHappenings.Job(p, then);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods' festivals: something carried", e);
                return false;
            }
        }

        private static Job GoJob(IntVec3 to, int ticks)
        {
            var go = JobMaker.MakeJob(JobDefOf.Goto, to);
            go.locomotionUrgency = LocomotionUrgency.Walk;
            go.expiryInterval = ticks;
            return go;
        }

        /// <summary>The plainest meal in the stores (a simple meal before paste, before anything finer), else fresh fruit or vegetables; null for none.</summary>
        internal static Thing Plain(Map map) => Plains(map).FirstOrDefault();

        /// <summary>The stores' stacks of the plainest kind there is (Plain's kind), the biggest first.</summary>
        private static List<Thing> Plains(Map map)
        {
            var all = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(t => t.Spawned && !(t is Corpse) && t.def.ingestible != null && t.def.ingestible.HumanEdible && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer)
                            && (t.def.ingestible.IsMeal || t.def.ingestible.preferability == FoodPreferability.RawTasty) && !t.def.IsDrug)
                .OrderBy(t => t.def.ingestible.preferability == FoodPreferability.MealSimple ? 0
                    : t.def.ingestible.preferability == FoodPreferability.MealAwful ? 1
                    : t.def.ingestible.IsMeal ? 2 : 3)
                .ThenByDescending(t => t.stackCount).ToList();
            var kind = all.FirstOrDefault()?.def;
            return all.Where(t => t.def == kind).ToList();
        }

        /// <summary>One of the stacks into their hand (the first they can take one from beside the others), then <paramref name="then"/>; true when they took one.</summary>
        private static bool TakeOne(Pawn p, List<Thing> stacks, Job then)
        {
            var stack = stacks.FirstOrDefault(t => t.Spawned && p.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some, 10, 1));
            if (stack != null && TakeThen(p, stack, 1, then)) return true;
            OwnHappenings.Job(p, then);
            return false;
        }

        /// <summary>The meal in their hand eaten there and then (the game's own eating sound and effect); false with nothing in their hand.</summary>
        private static bool Eat(Pawn p, ThingDef def)
        {
            var held = p.inventory?.innerContainer?.FirstOrDefault(t => t.def == def) ?? p.inventory?.innerContainer?.FirstOrDefault(t => t.def.ingestible?.IsMeal == true);
            if (held == null || def == null) return false;
            try
            {
                var one = held.stackCount > 1 ? held.SplitOff(1) : held;
                if (one.holdingOwner != null) one.holdingOwner.Remove(one);
                var effect = one.def.ingestible.ingestEffect;
                var sound = one.def.ingestible.ingestSound;
                float food = one.Ingested(p, p.needs?.food?.NutritionWanted ?? 0f);
                if (p.needs?.food != null) p.needs.food.CurLevel += food;
                if (effect != null)
                {
                    var e = effect.Spawn();
                    e.Trigger(new TargetInfo(p), new TargetInfo(p));
                    e.Cleanup();
                }
                if (sound != null) Verse.Sound.SoundStarter.PlayOneShot(sound, new TargetInfo(p.Position, p.Map));
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods' festivals: a meal eaten", e);
                return false;
            }
        }

        /// <summary>The ones sent take a meal from the stores each (while there are any) and walk to a place near <paramref name="spot"/>; the meal's kind, or null.</summary>
        private static ThingDef SendToEat(FeastState s, Map map, List<Pawn> who, IntVec3 spot, float radius)
        {
            var food = Plains(map);
            s.sent = null;
            s.done = null;
            foreach (var p in who)
            {
                var to = CellFinder.RandomClosewalkCellNear(spot, map, Mathf.RoundToInt(radius), c => c.Standable(map) && !c.Fogged(map));
                if (!to.IsValid || !p.CanReach(to, PathEndMode.OnCell, Danger.Some)) continue;
                TakeOne(p, food, GoJob(to, GenDate.TicksPerHour * 2));
                s.sent = HeliosState.With(s.sent, p);
            }
            return food.FirstOrDefault()?.def;
        }

        private static readonly Color Green = new Color(0.55f, 0.85f, 0.45f), Ember = new Color(1f, 0.6f, 0.3f), Silver = new Color(0.85f, 0.88f, 0.95f);

        private static void Thought(Pawn p, string god) => OwnHappenings.Memory(p, "Rimfolk_Thought_Feast_" + god);

        private static string K(string god, string rest) => "CC_Feast_" + god + "_" + rest;

        /// <summary>What was done, told as it was seen (a message), and the colony's book under the god whose festival it was.</summary>
        private static void Told(God g, Map map, IntVec3 at, string text, string book, Pawn also = null)
        {
            if (text.NullOrEmpty()) return;
            OwnHappenings.Line(map, text, at.IsValid ? new LookTargets(new TargetInfo(at, map)) : null);
            if (g != null && !book.NullOrEmpty()) Worship.Booked(g, map, at, book, also);
        }

        // ================= Hollin: the Greening =================

        private static bool HollinPlant(FeastState s, God g, Map map, int now)
        {
            int felled = s.felled;
            s.count2 = felled;
            s.felled = 0;
            int owed = OwnFeastsMath.Saplings(felled);
            var b = OwnHappenings.Book;
            var near = b?.felledMap == map && b.felledAt.IsValid ? b.felledAt : OwnHollin.Oldest(map)?.Position ?? OwnHappenings.Middle(map);
            s.at = near;
            s.stage = 1;
            s.until = now + OwnFeastsMath.PlantingHours * GenDate.TicksPerHour;
            if (owed == 0) return true;
            var cells = GenRadial.RadialCellsAround(near, 16f, true).Where(c => PeoplesE.SaplingCell(map, c) && !c.InHorDistOf(near, 1.5f)).Take(owed * 3).InRandomOrder().Take(owed)
                .OrderBy(c => c.DistanceToSquared(near)).ToList();
            var planters = Takers(map, g, OwnFeastsMath.PlantersMost, false, p => Skill(p, SkillDefOf.Plants));
            var share = OwnFeastsMath.Share(cells.Count, planters.Count);
            int i = 0;
            for (int k = 0; k < share.Count; k++)
            {
                var mine = cells.Skip(i).Take(share[k]).Select(c => new LocalTargetInfo(c)).ToList();
                i += share[k];
                if (Work(planters[k], PlantJob, mine, 300)) s.sent = HeliosState.With(s.sent, planters[k]);
            }
            return true;
        }

        private static void Planted(Pawn p, IntVec3 c)
        {
            var s = S;
            var map = p.Map;
            if (s == null || s.god != "Hollin" || map == null) return;
            var tree = PeoplesE.SaplingFor(map, c);
            if (tree == null || PeoplesE.Sapling(map, c, tree) == null) return;
            s.count++;
            s.what2 = tree.label;
            s.done = HeliosState.Has(s.done, p) ? s.done : HeliosState.With(s.done, p);
            OwnHappenings.Glow(map, c, Green, 3, 0.5f, 0.3f);
            if (s.count == 1) OwnHappenings.SayFrom(p, Pool, OwnHappenings.God("Hollin"), "hollin", "plant");
        }

        private static void HollinMeal(OwnHappeningsBook b, FeastState s, God g, Map map, int now)
        {
            var tree = OwnHollin.Oldest(map);
            s.thing = tree;
            var spot = tree != null ? CellFinder.RandomClosewalkCellNear(tree.Position, map, 2, c => c.Standable(map)) : s.at;
            if (!spot.IsValid) spot = OwnHappenings.Middle(map);
            // The planters (done) are kept for the words; the meal's own come in "sent" and "sat".
            string planters = s.done;
            var eaters = map.mapPawns.FreeColonistsSpawned.Where(OwnHappenings.Free).Take(10).ToList();
            var food = SendToEat(s, map, eaters, spot, 3f);
            s.done = planters;
            s.sat = null;
            s.what = food?.defName;
            s.at = spot;
            s.stage = 3;
            s.count3 = s.ate = 0;
            s.until = now + GenDate.TicksPerHour * 3;
            b.Due(now + 250);
        }

        private static void MealStep(OwnHappeningsBook b, FeastState s, God g, Map map, int now)
        {
            var food = s.what == null ? null : DefDatabase<ThingDef>.GetNamedSilentFail(s.what);
            foreach (var p in Sent(s, map).Where(p => !HeliosState.Has(s.sat, p) && p.Position.InHorDistOf(s.at, 5f) && p.CurJobDef != JobDefOf.Goto && p.CurJobDef != JobDefOf.TakeInventory).ToList())
            {
                s.sat = HeliosState.With(s.sat, p);
                bool firstToSit = s.count3 == 0;
                OwnHappenings.Stand(p, s.thing?.Position ?? s.at, 700);
                if (food != null && Eat(p, food)) s.ate++;
                s.count3++;
                Thought(p, "Hollin");
                if (firstToSit) OwnHappenings.SayFrom(p, Pool, g, "hollin", "meal");
            }
            if (now <= s.until) return;
            var home = Homes.Name(map).Named("HOME");
            string tree = s.thing?.def.label;
            string where = tree != null ? "CC_Feast_Where_Tree".Translate(tree.Named("TREE")).Resolve() : "CC_Feast_Where_Open".Translate().Resolve();
            string first = s.count > 0
                ? K("Hollin", "Planted").Translate(HeliosState.Count(s.done).Named("PLANTERS"), s.count.Named("SAPLINGS"), s.count2.Named("FELLED"), home).Resolve()
                : s.count2 > 0 ? K("Hollin", "NoGround").Translate(s.count2.Named("FELLED"), home).Resolve() : K("Hollin", "NoneFelled").Translate(home).Resolve();
            string meal = s.count3 == 0 ? null
                : s.ate > 0 && food != null ? K("Hollin", s.ate >= s.count3 ? "Meal" : "MealSome").Translate(s.count3.Named("EATERS"), s.ate.Named("ATE"), food.label.Named("FOOD"), where.Named("WHERE")).Resolve()
                : K("Hollin", "MealNoFood").Translate(s.count3.Named("EATERS"), where.Named("WHERE")).Resolve();
            string book = s.count > 0 ? K("Hollin", "Book").Translate(s.count.Named("SAPLINGS"), s.count2.Named("FELLED")).Resolve() : K("Hollin", "BookNone").Translate().Resolve();
            Told(g, map, s.at, first + (meal != null ? " " + meal : ""), book);
            Finish(b, s, false);
        }

        // ================= Smoulder: the Deep Silver =================

        /// <summary>A crack in the rock: open ground beside a face of natural rock, nearest the colony's middle; Invalid for none.</summary>
        private static IntVec3 Crack(Map map, Pawn by)
        {
            var mid = OwnHappenings.Middle(map);
            foreach (var c in GenRadial.RadialCellsAround(mid, 50f, true))
            {
                if (!c.InBounds(map) || !c.Standable(map) || c.Fogged(map) || c.GetFirstItem(map) != null) continue;
                if (!GenAdj.CellsAdjacentCardinal(c, Rot4.North, IntVec2.One).Any(n => n.InBounds(map) && n.GetEdifice(map) is Building e && e.def.building?.isNaturalRock == true)) continue;
                if (by != null && !by.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                return c;
            }
            return IntVec3.Invalid;
        }

        private static bool SmoulderCarry(FeastState s, God g, Map map, int now)
        {
            var carrier = Takers(map, g, 1, true).FirstOrDefault();
            if (carrier == null) return false;
            var place = OwnSmoulder.Shaft(map, carrier);
            s.what2 = place.IsValid ? "Shaft" : "Crack";
            if (!place.IsValid) place = Crack(map, carrier);
            if (!place.IsValid) return false;
            s.at = place;
            s.lead = carrier;
            var silver = map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t => t.Spawned && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer)).ToList();
            int n = OwnFeastsMath.Silver(silver.Sum(t => t.stackCount));
            var stack = silver.Where(t => t.stackCount >= n).OrderBy(t => t.Position.DistanceToSquared(carrier.Position)).FirstOrDefault()
                        ?? silver.OrderByDescending(t => t.stackCount).FirstOrDefault();
            if (n > 0 && stack != null && OwnHappenings.Carry(carrier, stack, place, Mathf.Min(n, stack.stackCount)))
            {
                s.stage = 1;
                s.count2 = Mathf.Min(n, stack.stackCount);
                s.until = now + GenDate.TicksPerHour * 6;
                return true;
            }
            s.stage = 2;
            s.until = now + GenDate.TicksPerHour * 3;
            return OwnHappenings.GoTo(carrier, place, GenDate.TicksPerHour * 3);
        }

        private static void SmoulderStep(OwnHappeningsBook b, FeastState s, God g, Map map, int now)
        {
            var carrier = s.lead;
            var home = Homes.Name(map).Named("HOME");
            string place = K("Smoulder", s.what2 ?? "Crack").Translate().Resolve();
            if (s.stage == 1)
            {
                var down = GenRadial.RadialCellsAround(s.at, 1.5f, true).Where(c => c.InBounds(map)).SelectMany(c => c.GetThingList(map))
                    .FirstOrDefault(t => t.def == ThingDefOf.Silver && !t.IsInAnyStorage());
                if (down == null)
                {
                    if (now > s.until || carrier == null || carrier.Dead || !carrier.Spawned || carrier.CurJobDef != JobDefOf.HaulToCell && !carrier.Position.InHorDistOf(s.at, 3f))
                        Finish(b, s, false);
                    return;
                }
                int n = down.stackCount;
                var at = down.Position;
                down.Destroy();
                s.count = n;
                FleckMaker.ThrowHeatGlow(at, map, 2f);
                for (int i = 0; i < 5; i++) FleckMaker.ThrowAirPuffUp(at.ToVector3Shifted() + new Vector3(Rand.Range(-0.5f, 0.5f), 0f, Rand.Range(-0.5f, 0.5f)), map);
                OwnHappenings.Glow(map, at, Silver, 5, 0.6f, 0.35f);
                if (carrier != null && carrier.Spawned)
                {
                    OwnHappenings.Stand(carrier, at, 400);
                    OwnHappenings.SayFrom(carrier, Pool, g, "smoulder", "down");
                    Thought(carrier, "Smoulder");
                }
                Told(g, map, at, K("Smoulder", "Text").Translate((carrier?.LabelShort ?? "").Named("PAWN"), n.Named("COUNT"), place.Named("PLACE"), home).Resolve(),
                    K("Smoulder", "Book").Translate(n.Named("COUNT"), place.Named("PLACE")).Resolve(), carrier);
                Finish(b, s, false);
                return;
            }
            if (s.stage != 2) return;
            if (carrier == null || carrier.Dead || !carrier.Spawned || now > s.until)
            {
                Finish(b, s, false);
                return;
            }
            if (!carrier.Position.InHorDistOf(s.at, 2.5f))
            {
                if (carrier.CurJobDef != JobDefOf.Goto) OwnHappenings.GoTo(carrier, s.at, GenDate.TicksPerHour * 2);
                return;
            }
            OwnHappenings.Stand(carrier, s.at, 400);
            OwnHappenings.SayFrom(carrier, Pool, g, "smoulder", "empty");
            Thought(carrier, "Smoulder");
            Told(g, map, s.at, K("Smoulder", "TextNone").Translate(carrier.LabelShort.Named("PAWN"), place.Named("PLACE"), home).Resolve(),
                K("Smoulder", "BookNone").Translate(place.Named("PLACE")).Resolve(), carrier);
            Finish(b, s, false);
        }

        // ================= Bellwether: the Letting Pass =================

        /// <summary>The long grass at the colony's edge: open ground under the sky, out of the fields, with the most wild grass round it.</summary>
        private static IntVec3 Edge(Map map)
        {
            var mid = OwnHappenings.Middle(map);
            IntVec3 best = IntVec3.Invalid;
            int most = -1;
            for (int i = 0; i < 160; i++)
            {
                float angle = Rand.Range(0f, 360f), r = Rand.Range(OwnFeastsMath.EdgeFrom, OwnFeastsMath.EdgeTo);
                var c = (mid.ToVector3() + Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * r).ToIntVec3();
                if (!c.InBounds(map) || !c.Standable(map) || c.Roofed(map) || c.Fogged(map) || c.GetZone(map) != null || c.GetFirstBuilding(map) != null) continue;
                int grass = GenRadial.RadialCellsAround(c, 2.5f, true).Count(x => x.InBounds(map) && x.GetPlant(map) is Plant p && p.def.plant != null && !p.def.plant.IsTree
                                                                                    && p.def.ingestible != null && (p.def.ingestible.foodType & FoodTypeFlags.Plant) != 0);
                if (grass > most)
                {
                    most = grass;
                    best = c;
                }
            }
            return best;
        }

        private static bool BellwetherOut(FeastState s, God g, Map map, int now)
        {
            var herder = Takers(map, g, 1, true, p => Skill(p, SkillDefOf.Animals)).FirstOrDefault();
            var edge = Edge(map);
            if (herder == null || !edge.IsValid) return false;
            s.lead = herder;
            s.at = edge;
            OwnHappenings.GoTo(herder, edge, GenDate.TicksPerHour * 2);
            var herd = map.mapPawns.SpawnedColonyAnimals
                .Where(a => !a.Dead && !a.Downed && !a.InMentalState && a.Awake() && !(a.roping?.IsRoped ?? false) && !AnimalPenUtility.NeedsToBeManagedByRope(a)
                            && a.RaceProps.Eats(FoodTypeFlags.Plant) && OwnHappenings.Yields(a) && a.CanReach(edge, PathEndMode.Touch, Danger.Some))
                .OrderBy(a => a.Position.DistanceToSquared(herder.Position)).Take(OwnFeastsMath.HerdMost).ToList();
            foreach (var a in herd)
            {
                var to = CellFinder.RandomClosewalkCellNear(edge, map, 4, c => c.Standable(map));
                OwnHappenings.Lead(a, to.IsValid ? to : edge, GenDate.TicksPerHour * 2);
            }
            s.count = herd.Count;
            s.stage = 1;
            s.until = now + GenDate.TicksPerHour * 4;
            OwnHappenings.SayFrom(herder, Pool, g, "bellwether", herd.Count > 0 ? "herd" : "alone");
            return true;
        }

        private static void BellwetherStory(FeastState s, God g, Map map, int now)
        {
            var grown = map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Free(p) && !p.DevelopmentalStage.Juvenile()).ToList();
            var teller = grown.OrderByDescending(p => Holds(p, g) ? 1 : 0).ThenByDescending(p => p.ageTracker?.AgeBiologicalYears ?? 0).FirstOrDefault();
            if (teller == null)
            {
                BellwetherTold(OwnHappenings.Book, s, g, map);
                return;
            }
            var children = map.mapPawns.FreeColonistsSpawned.Where(p => p != teller && p.DevelopmentalStage.Child() && OwnHappenings.Free(p)).Take(6).ToList();
            var listeners = children.Count > 0 ? children : grown.Where(p => p != teller).Take(2).ToList();
            if (s.lead != null && s.lead != teller && s.lead.Spawned && !listeners.Contains(s.lead) && children.Count == 0) listeners.Insert(0, s.lead);
            s.other = teller;
            s.count2 = children.Count;
            s.sent = null;
            s.done = null;
            var spot = CellFinder.RandomClosewalkCellNear(s.at, map, 2, c => c.Standable(map));
            if (!spot.IsValid) spot = s.at;
            OwnHappenings.GoTo(teller, spot, GenDate.TicksPerHour * 2);
            foreach (var p in listeners)
            {
                var to = CellFinder.RandomClosewalkCellNear(spot, map, 3, c => c.Standable(map) && c != spot);
                if (to.IsValid && OwnHappenings.GoTo(p, to, GenDate.TicksPerHour * 2)) s.sent = HeliosState.With(s.sent, p);
            }
            s.at = spot;
            s.stage = 2;
            s.until = now + GenDate.TicksPerHour * 2;
        }

        private static void StoryStep(OwnHappeningsBook b, FeastState s, God g, Map map, int now)
        {
            var teller = s.other;
            if (teller != null && teller.Spawned && teller.Position.InHorDistOf(s.at, 3f) && teller.CurJobDef != JobDefOf.Goto && s.count3 == 0)
            {
                var near = Sent(s, map).Where(p => p.Position.InHorDistOf(s.at, 6f)).ToList();
                if (near.Count > 0 || now > s.until - GenDate.TicksPerHour)
                {
                    s.count3 = 1;
                    OwnHappenings.Stand(teller, near.FirstOrDefault()?.Position ?? s.at, 1200);
                    OwnHappenings.SayFrom(teller, Pool, g, "bellwether", "story", s.count2 > 0 ? "children" : null);
                    Thought(teller, "Bellwether");
                    foreach (var p in near)
                    {
                        OwnHappenings.Stand(p, teller.Position, 1200);
                        s.done = HeliosState.With(s.done, p);
                        Thought(p, "Bellwether");
                    }
                }
            }
            if (now <= s.until && s.count3 == 0) return;
            BellwetherTold(b, s, g, map);
        }

        /// <summary>The Letting Pass told: the herd walked out, and the story if it was told (the teller never got there: the herd only).</summary>
        private static void BellwetherTold(OwnHappeningsBook b, FeastState s, God g, Map map)
        {
            var teller = s.count3 > 0 ? s.other : null;
            var home = Homes.Name(map).Named("HOME");
            var herder = (s.lead?.LabelShort ?? "").Named("HERDER");
            string first = s.count > 0 ? K("Bellwether", "Herd").Translate(herder, s.count.Named("COUNT"), home).Resolve() : K("Bellwether", "Alone").Translate(herder, home).Resolve();
            var heard = map.mapPawns.FreeColonistsSpawned.Where(p => HeliosState.Has(s.done, p)).ToList();
            string story = teller == null ? null
                : heard.Count == 0 ? K("Bellwether", "StoryNobody").Translate(teller.LabelShort.Named("TELLER")).Resolve()
                : K("Bellwether", s.count2 > 0 ? "StoryChildren" : "Story").Translate(teller.LabelShort.Named("TELLER"), Names(heard).Named("LISTENERS")).Resolve();
            string book = teller == null ? K("Bellwether", "BookNoStory").Translate(s.count.Named("COUNT")).Resolve()
                : K("Bellwether", "Book").Translate(s.count.Named("COUNT"), teller.LabelShort.Named("TELLER")).Resolve();
            Told(g, map, s.at, first + (story != null ? " " + story : ""), book, teller ?? s.lead);
            Finish(b, s, false);
        }

        // ================= Morel: the Turning Under =================

        /// <summary>The field: natural soil not yet rich in the colony's biggest growing zone, under the sky; else open soil near the middle.</summary>
        private static IntVec3 Field(Map map)
        {
            bool Fits(IntVec3 c) => c.InBounds(map) && c.Standable(map) && !c.Roofed(map) && c.GetTerrain(map) is TerrainDef t && t.natural && !t.IsWater && t.defName != "SoilRich"
                                    && t.fertility >= 0.7f && t.fertility < 1.4f;
            foreach (var z in map.zoneManager.AllZones.OfType<Zone_Growing>().OrderByDescending(z => z.CellCount))
            {
                var c = z.Cells.Where(Fits).InRandomOrder().FirstOrDefault();
                if (c.IsValid && Fits(c)) return c;
            }
            var mid = OwnHappenings.Middle(map);
            return GenRadial.RadialCellsAround(mid, 30f, true).Where(c => Fits(c) && c.GetFirstBuilding(map) == null).FirstOrDefault(c => c.IsValid);
        }

        /// <summary>The scraps the stores can give back: hay first, else the plainest raw vegetables.</summary>
        private static Thing Scraps(Map map, out int n)
        {
            n = 0;
            var hay = DefDatabase<ThingDef>.GetNamedSilentFail("Hay");
            Thing pick = hay == null ? null : map.listerThings.ThingsOfDef(hay).Where(t => t.Spawned && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer)).OrderByDescending(t => t.stackCount).FirstOrDefault();
            if (pick != null)
            {
                n = Mathf.Min(OwnFeastsMath.HayMost, pick.stackCount);
                return pick;
            }
            pick = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(t => t.Spawned && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer) && t.def.ingestible != null && (t.def.ingestible.foodType & FoodTypeFlags.VegetableOrFruit) != 0
                            && t.def.ingestible.preferability == FoodPreferability.RawBad)
                .OrderByDescending(t => t.stackCount).FirstOrDefault();
            if (pick != null) n = Mathf.Min(OwnFeastsMath.ScrapsMost, pick.stackCount);
            return pick;
        }

        private static bool MorelCarry(FeastState s, God g, Map map, int now)
        {
            var digger = Takers(map, g, 1, true, p => Skill(p, SkillDefOf.Plants)).FirstOrDefault();
            var field = Field(map);
            if (digger == null || !field.IsValid) return false;
            s.lead = digger;
            s.at = field;
            var scraps = Scraps(map, out int n);
            s.until = now + GenDate.TicksPerHour * 5;
            if (scraps != null && n > 0 && OwnHappenings.Carry(digger, scraps, field, n))
            {
                s.what = scraps.def.defName;
                s.count = n;
                s.stage = 1;
                return true;
            }
            s.stage = 2;
            return Work(digger, DigJob, new List<LocalTargetInfo> { field }, 900);
        }

        private static void MorelStep(OwnHappeningsBook b, FeastState s, God g, Map map, int now)
        {
            var digger = s.lead;
            if (s.stage == 1)
            {
                var laid = Laid(s, map);
                if (laid != null && digger != null && digger.Spawned && digger.CurJobDef != JobDefOf.HaulToCell)
                {
                    s.stage = 2;
                    Work(digger, DigJob, new List<LocalTargetInfo> { s.at }, 900);
                }
                else if (now > s.until) Finish(b, s, false);
                return;
            }
            if (s.stage == 2)
            {
                if (now > s.until || digger == null || digger.Dead || !digger.Spawned || digger.CurJobDef != DigJob && s.count2 == 0 && now > s.until - GenDate.TicksPerHour * 3)
                    Stew(b, s, g, map, now);
                return;
            }
            if (s.stage == 4 && (now > s.until || s.other == null || !s.other.Spawned || s.other.CurJobDef != CookJob && s.other.CurJobDef != JobDefOf.TakeInventory))
                MorelTold(b, s, g, map);
        }

        /// <summary>The scraps set down at the field, if they lie there.</summary>
        private static Thing Laid(FeastState s, Map map) =>
            s.what == null ? null : GenRadial.RadialCellsAround(s.at, 1.5f, true).Where(c => c.InBounds(map)).SelectMany(c => c.GetThingList(map))
                .FirstOrDefault(t => t.def.defName == s.what && !t.IsInAnyStorage());

        private static void Dug(Pawn p, IntVec3 at)
        {
            var s = S;
            var map = p.Map;
            if (s == null || s.god != "Morel" || map == null || s.stage != 2) return;
            var laid = Laid(s, map);
            if (laid != null)
            {
                s.what2 = laid.def.label;
                laid.Destroy();
            }
            else s.count = 0;
            var rich = DefDatabase<TerrainDef>.GetNamedSilentFail("SoilRich");
            int n = 0;
            if (rich != null)
                foreach (var c in GenRadial.RadialCellsAround(at, 1.5f, true).Where(c => c.InBounds(map)).ToList())
                {
                    if (n >= OwnFeastsMath.RichCells) break;
                    var t = c.GetTerrain(map);
                    if (t == null || !t.natural || t.IsWater || t == rich || t.fertility <= 0.1f || t.fertility >= rich.fertility) continue;
                    map.terrainGrid.SetTerrain(c, rich);
                    FleckMaker.ThrowDustPuff(c, map, 0.5f);
                    n++;
                }
            s.count2 = n;
            OwnHappenings.Glow(map, at, new Color(0.45f, 0.35f, 0.25f), 4, 1f, 0.35f);
            OwnHappenings.SayFrom(p, Pool, OwnHappenings.God("Morel"), "morel", "dig");
            Thought(p, "Morel");
            // The dig done: the stew next, at the step.
            s.until = Now;
        }

        private static void Stew(OwnHappeningsBook b, FeastState s, God g, Map map, int now)
        {
            var fungi = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(t => t.Spawned && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer) && FungusMeals.IsRawFungus(t.def)).ToList();
            int meals = OwnFeastsMath.StewMeals(fungi.Sum(t => t.stackCount));
            var stack = fungi.OrderByDescending(t => t.stackCount).FirstOrDefault();
            meals = Mathf.Min(meals, stack == null ? 0 : stack.stackCount / OwnFeastsMath.FungusPerMeal);
            var cooking = DefDatabase<WorkTypeDef>.GetNamedSilentFail("Cooking");
            var cook = Takers(map, g, 3, true, p => Skill(p, SkillDefOf.Cooking)).FirstOrDefault(p => cooking == null || !p.WorkTypeIsDisabled(cooking));
            var stove = cook == null ? null : map.listerBuildings.allBuildingsColonist.Where(x => x.Spawned && x.def.AllRecipes != null && x.def.AllRecipes.Any(r => r.defName == "CookMealSimple")
                                                                                            && cook.CanReach(x, PathEndMode.InteractionCell, Danger.Some))
                .OrderBy(x => x.Position.DistanceToSquared(cook.Position)).FirstOrDefault();
            if (meals <= 0 || cook == null || stove == null)
            {
                MorelTold(b, s, g, map);
                return;
            }
            var cookAt = JobMaker.MakeJob(CookJob, stove);
            cookAt.count = 1200;
            cookAt.locomotionUrgency = LocomotionUrgency.Walk;
            if (!TakeThen(cook, stack, meals * OwnFeastsMath.FungusPerMeal, cookAt))
            {
                MorelTold(b, s, g, map);
                return;
            }
            s.other = cook;
            s.thing = stove;
            s.count3 = meals;
            s.stage = 4;
            s.until = now + GenDate.TicksPerHour * 4;
        }

        private static void Cooked(Pawn p, Thing stove)
        {
            var s = S;
            if (s == null || s.god != "Morel" || s.stage != 4 || stove == null || p.Map == null) return;
            var held = p.inventory?.innerContainer?.Where(t => FungusMeals.IsRawFungus(t.def)).ToList() ?? new List<Thing>();
            int have = held.Sum(t => t.stackCount);
            int meals = Mathf.Min(s.count3, have / OwnFeastsMath.FungusPerMeal);
            if (meals <= 0)
            {
                s.count3 = 0;
                return;
            }
            var fungus = held[0].def;
            int left = meals * OwnFeastsMath.FungusPerMeal;
            foreach (var t in held)
            {
                if (left <= 0) break;
                int take = Mathf.Min(left, t.stackCount);
                if (take >= t.stackCount)
                {
                    t.holdingOwner?.Remove(t);
                    t.Destroy();
                }
                else t.SplitOff(take).Destroy();
                left -= take;
            }
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(FungusMeals.MealName) ?? ThingDefOf.MealSimple;
            var made = ThingMaker.MakeThing(def);
            made.stackCount = meals;
            made.TryGetComp<CompIngredients>()?.RegisterIngredient(fungus);
            GenPlace.TryPlaceThing(made, stove.InteractionCell.IsValid ? stove.InteractionCell : p.Position, p.Map, ThingPlaceMode.Near);
            s.count3 = meals;
            s.what2 = s.what2 ?? "";
            s.thing = made;
            FleckMaker.ThrowMicroSparks(stove.TrueCenter(), p.Map);
            OwnHappenings.SayFrom(p, Pool, OwnHappenings.God("Morel"), "morel", "stew");
            Thought(p, "Morel");
            s.until = Now;
            s.done = fungus.label;
        }

        private static void MorelTold(OwnHappeningsBook b, FeastState s, God g, Map map)
        {
            var home = Homes.Name(map).Named("HOME");
            var pawn = (s.lead?.LabelShort ?? "").Named("PAWN");
            string first = s.count > 0 && !s.what2.NullOrEmpty()
                ? K("Morel", s.count2 > 0 ? "Dug" : "DugNoRich").Translate(pawn, s.count.Named("COUNT"), s.what2.Named("SCRAPS"), s.count2.Named("CELLS"), home).Resolve()
                : K("Morel", s.count2 > 0 ? "Turned" : "TurnedNoRich").Translate(pawn, s.count2.Named("CELLS"), home).Resolve();
            bool stew = s.stage == 4 && s.count3 > 0 && s.done != null && s.other != null;
            string second = stew ? K("Morel", "Stew").Translate(s.other.LabelShort.Named("COOK"), s.count3.Named("MEALS"), s.done.Named("FUNGUS")).Resolve() : K("Morel", "NoStew").Translate().Resolve();
            Told(g, map, s.at, first + " " + second, K("Morel", stew ? "Book" : "BookNoStew").Translate(s.count2.Named("CELLS"), s.count3.Named("MEALS")).Resolve(), s.lead);
            Finish(b, s, false);
        }

        // ================= Pip: the First Spark =================

        private static bool PipMend(FeastState s, God g, Map map, int now)
        {
            var mender = Takers(map, g, 1, true, p => Skill(p, SkillDefOf.Construction)).FirstOrDefault(p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction));
            if (mender == null) return false;
            s.lead = mender;
            s.stage = 1;
            s.until = now + GenDate.TicksPerHour * 4;
            var mid = OwnHappenings.Middle(map);
            var component = map.listerThings.ThingsOfDef(ThingDefOf.ComponentIndustrial).Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer) && mender.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some))
                .OrderBy(t => t.Position.DistanceToSquared(mender.Position)).FirstOrDefault();
            var broken = component == null ? null : map.listerBuildings.allBuildingsColonist
                .Where(x => x.Spawned && x.TryGetComp<CompBreakdownable>()?.BrokenDown == true && mender.CanReserveAndReach(x, PathEndMode.Touch, Danger.Some))
                .OrderBy(x => x.Position.DistanceToSquared(mid)).FirstOrDefault();
            Job job = null;
            if (broken != null)
            {
                job = JobMaker.MakeJob(JobDefOf.FixBrokenDownBuilding, broken, component);
                job.count = 1;
                s.what2 = "broken";
            }
            else
            {
                broken = map.listerBuildings.allBuildingsColonist
                    .Where(x => x.Spawned && x.def.useHitPoints && x.HitPoints < x.MaxHitPoints && !x.IsBurning() && map.areaManager.Home[x.Position] && x.def.building?.repairable != false
                                && mender.CanReserveAndReach(x, PathEndMode.Touch, Danger.Some))
                    .OrderBy(x => x.Position.DistanceToSquared(mid)).FirstOrDefault();
                if (broken != null)
                {
                    job = JobMaker.MakeJob(JobDefOf.Repair, broken);
                    s.what2 = "damaged";
                }
            }
            s.thing = broken;
            s.what = broken?.LabelNoCount;
            if (job == null || !OwnHappenings.Job(mender, job))
            {
                s.thing = null;
                s.what = null;
                Machines(s, g, map, now);
            }
            return true;
        }

        private static bool Mended(FeastState s) =>
            s.thing != null && !s.thing.Destroyed && (s.what2 == "broken" ? s.thing.TryGetComp<CompBreakdownable>()?.BrokenDown == false : s.thing.HitPoints >= s.thing.MaxHitPoints);

        private static void PipStep(FeastState s, God g, Map map, int now)
        {
            if (s.stage == 1)
            {
                if (Mended(s)) s.count2 = 1;
                bool working = s.lead != null && s.lead.Spawned && (s.lead.CurJobDef == JobDefOf.FixBrokenDownBuilding || s.lead.CurJobDef == JobDefOf.Repair);
                if (s.count2 == 1 || !working || now > s.until)
                {
                    if (s.count2 == 1) OwnHappenings.SayFrom(s.lead, Pool, g, "pip", "mended");
                    Machines(s, g, map, now);
                }
                return;
            }
            if (s.stage == 2 && (now > s.until || s.lead == null || !s.lead.Spawned || s.lead.CurJobDef != MachineJob)) s.stage = 3;
        }

        /// <summary>The machines walked round one by one (the colony's powered buildings; the work benches where there are none).</summary>
        private static void Machines(FeastState s, God g, Map map, int now)
        {
            s.stage = 2;
            s.until = now + GenDate.TicksPerHour * 3;
            var p = s.lead;
            if (p == null || !p.Spawned)
            {
                s.stage = 3;
                return;
            }
            var all = map.listerBuildings.allBuildingsColonist.Where(x => x.Spawned && x.TryGetComp<CompPowerTrader>() != null && !(x is Building_Door) && x.def.building?.isEdifice != false
                                                                          && p.CanReach(x, PathEndMode.Touch, Danger.Some)).ToList();
            s.benches = all.Count == 0;
            if (all.Count == 0)
                all = map.listerBuildings.allBuildingsColonist.Where(x => x.Spawned && x is Building_WorkTable && p.CanReach(x, PathEndMode.Touch, Danger.Some)).ToList();
            var order = new List<Building>();
            var from = p.Position;
            while (order.Count < OwnFeastsMath.MachinesMost && all.Count > 0)
            {
                var at = from;
                var n = all.OrderBy(x => x.Position.DistanceToSquared(at)).First();
                order.Add(n);
                all.Remove(n);
                from = n.Position;
            }
            s.count = 0;
            if (order.Count == 0 || !Work(p, MachineJob, order.Select(x => new LocalTargetInfo(x)).ToList(), 240)) s.stage = 3;
        }

        // ================= Lamps and doors at dusk (Pip, Undertow), put back at dawn =================

        private static readonly AccessTools.FieldRef<Building_Door, bool> HoldOpenField = AccessTools.FieldRefAccess<Building_Door, bool>("holdOpenInt");

        /// <summary>A door of the colony's to the outside (a side of it under the open sky or outdoors).</summary>
        private static bool OuterDoor(Building_Door d, Map map) =>
            d.Faction == Faction.OfPlayer && GenAdj.CellsAdjacentCardinal(d).Any(c => c.InBounds(map) && c.GetRoom(map)?.PsychologicallyOutdoors == true);

        /// <summary>A lamp of the colony's switched off (a light that can be switched).</summary>
        private static bool LampOff(Thing t) => t.TryGetComp<CompGlower>() != null && t.TryGetComp<CompFlickable>() is CompFlickable f && !f.SwitchIsOn;

        /// <summary>
        /// The round at dusk: the doors to the outside (Undertow's) and the lamps switched off, one after another, nearest next;
        /// then each torch or brazier low on fuel topped up (the game's own refuelling), which is a lamp lit in an age without
        /// switches.
        /// </summary>
        private static bool Round(FeastState s, God g, Map map, int now, bool shut)
        {
            var p = s.lead != null && s.lead.Spawned && OwnHappenings.Free(s.lead) ? s.lead : Takers(map, g, 1, true).FirstOrDefault();
            if (p == null)
            {
                if (s.god == "Pip") s.stage = 4;
                return s.god == "Pip";
            }
            s.lead = p;
            s.stage = 4;
            s.lamps = s.torches = 0;
            if (shut) s.count = 0;
            var targets = new List<Thing>();
            if (shut)
                targets.AddRange(map.listerBuildings.allBuildingsColonist.OfType<Building_Door>().Where(d => d.Spawned && OuterDoor(d, map) && p.CanReach(d, PathEndMode.Touch, Danger.Some))
                    .OrderBy(d => d.Position.DistanceToSquared(p.Position)).Take(OwnFeastsMath.DoorsMost));
            targets.AddRange(map.listerBuildings.allBuildingsColonist.Where(x => x.Spawned && LampOff(x) && p.CanReach(x, PathEndMode.Touch, Danger.Some))
                .OrderBy(x => x.Position.DistanceToSquared(p.Position)).Take(OwnFeastsMath.LampsMost));
            // Nearest next, from where they stand.
            var order = new List<Thing>();
            var from = p.Position;
            while (targets.Count > 0)
            {
                var at = from;
                var n = targets.OrderBy(x => x.Position.DistanceToSquared(at)).First();
                order.Add(n);
                targets.Remove(n);
                from = n.Position;
            }
            if (order.Count > 0) Work(p, LampJob, order.Select(x => new LocalTargetInfo(x)).ToList(), 150);
            // Torches and braziers low on fuel, topped up after the round.
            int torches = 0;
            foreach (var t in map.listerBuildings.allBuildingsColonist.Where(x => x.Spawned && x.TryGetComp<CompGlower>() != null && x.TryGetComp<CompRefuelable>() is CompRefuelable r
                                                                                && r.FuelPercentOfMax < 0.6f && x.TryGetComp<CompFlickable>() == null).Take(OwnFeastsMath.LampsMost).ToList())
            {
                try
                {
                    if (!RefuelWorkGiverUtility.CanRefuel(p, t, true)) continue;
                    var job = RefuelWorkGiverUtility.RefuelJob(p, t, true);
                    if (job == null) continue;
                    if (order.Count == 0 && torches == 0) OwnHappenings.Job(p, job);
                    else p.jobs.jobQueue.EnqueueLast(job);
                    torches++;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods' festivals: a torch topped up", e);
                }
            }
            s.torches = torches;
            if (order.Count == 0 && torches == 0)
            {
                // Everything is already shut and burning: they walk the round all the same, and say so.
                OwnHappenings.SayFrom(p, Pool, g, s.god.ToLowerInvariant(), "alllit");
            }
            return true;
        }

        private static void Lamp(Pawn p, Thing t)
        {
            var s = S;
            if (s == null || t == null || t.Destroyed || p.Map == null) return;
            if (t is Building_Door d)
            {
                if (d.HoldOpen)
                {
                    HoldOpenField(d) = false;
                    if (!s.doors.Contains(d)) s.doors.Add(d);
                }
                s.count++;
                if (s.count == 1) OwnHappenings.SayFrom(p, Pool, OwnHappenings.God(s.god), "undertow", "shut");
                return;
            }
            var f = t.TryGetComp<CompFlickable>();
            if (f == null || f.SwitchIsOn) return;
            f.SwitchIsOn = true;
            if (!s.lit.Contains(t)) s.lit.Add(t);
            s.lamps++;
            FleckMaker.ThrowLightningGlow(t.TrueCenter(), p.Map, 0.5f);
            if (s.lamps == 1) OwnHappenings.SayFrom(p, Pool, OwnHappenings.God(s.god), s.god.ToLowerInvariant(), "lamp");
        }

        /// <summary>The colony's lights burning now.</summary>
        private static int Burning(Map map) => map.listerBuildings.allBuildingsColonist.Count(x => x.Spawned && x.TryGetComp<CompGlower>() is CompGlower gl && gl.Glows);

        /// <summary>Undertow's night: the shuttered night told once the lamps are lit (at ten), the round's own count.</summary>
        private static void UndertowNight(FeastState s, God g, Map map)
        {
            int lit = Burning(map);
            s.told = true;
            var home = Homes.Name(map).Named("HOME");
            var pawn = (s.lead?.LabelShort ?? "").Named("PAWN");
            string text = K("Undertow", s.count > 0 ? "Text" : "TextNoDoors").Translate(pawn, s.count.Named("DOORS"), lit.Named("LIT"), home).Resolve();
            if (s.torches > 0) text += " " + K("Undertow", "Torches").Translate(pawn, s.torches.Named("COUNT")).Resolve();
            Told(g, map, s.lead?.Position ?? OwnHappenings.Middle(map), text, K("Undertow", "Book").Translate(lit.Named("LIT")).Resolve(), s.lead);
            if (s.lead != null) Thought(s.lead, "Undertow");
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(p => p != s.lead && Holds(p, g))) Thought(p, "Undertow");
        }

        /// <summary>Dawn after the night of lamps: Pip's told, and what was lit or shut for the night put back.</summary>
        private static void Dawn(OwnHappeningsBook b, FeastState s, God g, Map map)
        {
            if (s.god == "Pip")
            {
                int lit = Burning(map);
                var home = Homes.Name(map).Named("HOME");
                var pawn = (s.lead?.LabelShort ?? "").Named("PAWN");
                var parts = new List<string>
                {
                    s.count2 == 1 ? K("Pip", "Mended").Translate(pawn, (s.what ?? "").Named("BROKEN"), home).Resolve()
                    : s.what != null ? K("Pip", "NotMended").Translate(pawn, s.what.Named("BROKEN"), home).Resolve()
                    : K("Pip", "NothingBroken").Translate(pawn, home).Resolve()
                };
                if (s.count > 0) parts.Add(K("Pip", s.benches ? "Benches" : "Machines").Translate(pawn, s.count.Named("COUNT")).Resolve());
                parts.Add(K("Pip", "Lights").Translate(lit.Named("LIT")).Resolve());
                Told(g, map, s.thing?.PositionHeld ?? OwnHappenings.Middle(map), string.Join(" ", parts),
                    K("Pip", s.count2 == 1 ? "Book" : "BookNothing").Translate((s.what ?? "").Named("BROKEN")).Resolve(), s.lead);
                if (s.lead != null) Thought(s.lead, "Pip");
            }
            Finish(b, s, true);
        }

        /// <summary>What the night's round changed put back at dawn: lamps switched off again, doors held open again, unless someone changed them since.</summary>
        private static void PutBack(FeastState s)
        {
            foreach (var t in s.lit.ToList())
            {
                var f = t?.TryGetComp<CompFlickable>();
                if (t == null || t.Destroyed || f == null) continue;
                if (OwnFeastsMath.PutBack(true, f.SwitchIsOn)) f.SwitchIsOn = false;
            }
            foreach (var t in s.doors.ToList())
            {
                if (!(t is Building_Door d) || d.Destroyed) continue;
                if (OwnFeastsMath.PutBack(true, !d.HoldOpen)) HoldOpenField(d) = true;
            }
            s.lit.Clear();
            s.doors.Clear();
        }

        // ================= Hush: the Stillness =================

        /// <summary>The fire: a fire of the colony's (a campfire, a hearth, a brazier: heat and light, fed with fuel), else any heater; null for none.</summary>
        private static Building Fire(Map map)
        {
            var heat = map.listerBuildings.allBuildingsColonist.Where(x => x.Spawned && x.TryGetComp<CompHeatPusher>() != null).ToList();
            return heat.Where(x => x.TryGetComp<CompRefuelable>() != null && x.TryGetComp<CompGlower>() != null).OrderBy(x => x.Position.DistanceToSquared(OwnHappenings.Middle(map))).FirstOrDefault()
                   ?? heat.OrderBy(x => x.Position.DistanceToSquared(OwnHappenings.Middle(map))).FirstOrDefault();
        }

        private static bool Sick(Pawn p) => Prayers.Grave(p) != null || p.health?.hediffSet?.hediffs?.Any(h => h.def.makesSickThought && h.Visible) == true;

        private static bool HushSupper(FeastState s, God g, Map map, int now)
        {
            var fire = Fire(map);
            var middle = fire?.Position ?? map.listerBuildings.allBuildingsColonist.FirstOrDefault(x => x.def.surfaceType == SurfaceType.Eat)?.Position ?? OwnHappenings.Middle(map);
            var diners = map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Free(p) && !p.DevelopmentalStage.Baby()).Take(OwnFeastsMath.SupperMost).ToList();
            if (diners.Count == 0) return false;
            var order = OwnFeastsMath.SeatOrder(diners.Select(p => (Sick(p), p.ageTracker?.AgeBiologicalYears ?? 0, p.thingIDNumber)).ToList());
            var seats = map.listerBuildings.allBuildingsColonist.Where(x => x.Spawned && x.def.building?.isSittable == true && x.Position.InHorDistOf(middle, 12f) && !x.IsForbidden(Faction.OfPlayer))
                .Select(x => x.Position).OrderBy(c => c.DistanceToSquared(middle)).ToList();
            var open = GenRadial.RadialCellsAround(middle, 6f, false).Where(c => c.InBounds(map) && c.Standable(map) && !seats.Contains(c) && c.GetFirstBuilding(map) == null)
                .OrderBy(c => c.DistanceToSquared(middle)).ToList();
            var food = Plains(map);
            s.thing = fire;
            s.at = middle;
            s.what = food.FirstOrDefault()?.def.defName;
            s.sent = null;
            s.done = null;
            var nearest = new List<Pawn>();
            int si = 0, oi = 0;
            foreach (int id in order)
            {
                var p = diners.First(x => x.thingIDNumber == id);
                IntVec3 seat = si < seats.Count ? seats[si++] : oi < open.Count ? open[oi++] : IntVec3.Invalid;
                if (!seat.IsValid || !p.CanReach(seat, PathEndMode.OnCell, Danger.Some)) continue;
                TakeOne(p, food, GoJob(seat, GenDate.TicksPerHour * 2));
                s.sent = HeliosState.With(s.sent, p);
                if (nearest.Count < 2 && (Sick(p) || (p.ageTracker?.AgeBiologicalYears ?? 0) >= OwnFeastsMath.OldFrom)) nearest.Add(p);
            }
            if (s.sent == null) return false;
            s.what2 = nearest.Count > 0 ? Names(nearest) : null;
            s.stage = 1;
            s.until = now + GenDate.TicksPerHour * 3;
            return true;
        }

        private static void SupperStep(OwnHappeningsBook b, FeastState s, God g, Map map, int now)
        {
            var food = s.what == null ? null : DefDatabase<ThingDef>.GetNamedSilentFail(s.what);
            foreach (var p in Sent(s, map).Where(p => !HeliosState.Has(s.done, p) && p.CurJobDef != JobDefOf.Goto && p.CurJobDef != JobDefOf.TakeInventory && p.Position.InHorDistOf(s.at, 13f)).ToList())
            {
                s.done = HeliosState.With(s.done, p);
                OwnHappenings.Stand(p, s.at, 900);
                if (food != null && Eat(p, food)) s.count++;
                s.count3++;
                Thought(p, "Hush");
            }
            if (now <= s.until) return;
            if (s.count3 > 0)
            {
                var home = Homes.Name(map).Named("HOME");
                string where = s.thing != null ? "CC_Feast_Where_Fire".Translate(s.thing.def.label.Named("FIRE")).Resolve() : "CC_Feast_Where_Table".Translate().Resolve();
                string text = s.count > 0 && food != null
                    ? K("Hush", s.count >= s.count3 ? "Text" : "TextSome").Translate(s.count3.Named("COUNT"), s.count.Named("ATE"), food.label.Named("FOOD"), where.Named("WHERE"), home).Resolve()
                    : K("Hush", "TextNoFood").Translate(s.count3.Named("COUNT"), where.Named("WHERE"), home).Resolve();
                if (s.what2 != null) text += " " + K("Hush", s.thing != null ? "Nearest" : "NearestTable").Translate(s.what2.Named("NAMES")).Resolve();
                Told(g, map, s.at, text, K("Hush", s.what2 != null && s.thing != null ? "Book" : "BookPlain").Translate(s.count3.Named("COUNT")).Resolve());
            }
            Finish(b, s, false);
        }

        // ================= Selene: the Longest Night =================

        private static bool SeleneWatch(FeastState s, God g, Map map, int now)
        {
            var watchers = Takers(map, g, OwnFeastsMath.WatchersMost, true, p => Skill(p, SkillDefOf.Shooting));
            if (watchers.Count == 0) return false;
            foreach (var p in watchers) s.sent = HeliosState.With(s.sent, p);
            s.count = watchers.Count;
            s.lead = watchers[0];
            s.other = watchers.Count > 1 ? watchers[1] : null;
            s.stage = 1;
            s.what = Names(watchers);
            PlayOut plan = null;
            try
            {
                var watch = new DilemmaEffect_Vigil
                {
                    label = "CC_Feast_Selene_WatchLabel".Translate().Resolve(), target = watchers.Count > 1 ? DilemmaTarget.AAndB : DilemmaTarget.A, organizer = PlayWho.A,
                    count = watchers.Count, to = PlaySpot.Gate, patrol = true, untilDawn = true, hours = new IntRange(20, 23), duration = new FloatRange(8f, 9f)
                };
                plan = watch.Begin(new DilemmaContext { map = map, a = watchers[0], b = watchers.Count > 1 ? watchers[1] : null, why = Festivals.Name(g) });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods' festivals: the watch on the walls", e);
            }
            // With answers not played out on the map, they keep it their own way: out at the colony's edge, standing watch till dawn.
            s.fallback = plan == null;
            if (s.fallback) SeleneStand(s, map);
            OwnHappenings.SayFrom(s.lead, Pool, g, "selene", "watch");
            foreach (var p in watchers) Thought(p, "Selene");
            return true;
        }

        private static void SeleneStand(FeastState s, Map map)
        {
            foreach (var p in Sent(s, map).Where(OwnHappenings.Free))
            {
                var gate = PlaySpots.GateCell(map, p);
                var to = CellFinder.RandomClosewalkCellNear(gate.IsValid ? gate : OwnHappenings.Middle(map), map, 4, c => c.Standable(map));
                if (!to.IsValid) continue;
                if (!p.Position.InHorDistOf(to, 6f)) OwnHappenings.GoTo(p, to, GenDate.TicksPerHour);
                else OwnHappenings.Stand(p, OwnHappenings.Middle(map), GenDate.TicksPerHour);
            }
        }

        private static void Dreams(OwnHappeningsBook b, FeastState s, God g, Map map, int now)
        {
            if (s.count2 < OwnFeastsMath.DreamsMost)
            {
                var teller = map.mapPawns.FreeColonistsSpawned.Where(p => !HeliosState.Has(s.done, p) && p.CurJobDef == JobDefOf.Ingest && Together.Able(p) && People.HasInnerLife(p))
                    .OrderByDescending(p => HeliosState.Has(s.sent, p) ? 1 : 0).FirstOrDefault(p => Together.Company(p) != null);
                var listener = teller == null ? null : Together.Company(teller);
                if (teller != null && listener != null)
                {
                    string line = OwnHappenings.LineFrom(teller, Pool, g, null, "selene", "dream", HeliosState.Has(s.sent, teller) ? "watched" : null);
                    // together: Together.Company found them side by side at the table, and Speech.Say checks it again.
                    if (!line.NullOrEmpty() && Speech.Say(teller, line, listener))
                    {
                        s.done = HeliosState.With(s.done, teller);
                        s.count2++;
                    }
                }
            }
            if (s.count2 < OwnFeastsMath.DreamsMost && now <= s.until) return;
            var home = Homes.Name(map).Named("HOME");
            string text = K("Selene", s.count2 > 0 ? "Text" : "TextNoDreams").Translate((s.what ?? "").Named("WATCHERS"), s.count2.Named("DREAMS"), home).Resolve();
            Told(g, map, OwnHappenings.Middle(map), text, K("Selene", "Book").Translate((s.what ?? "").Named("WATCHERS"), s.count2.Named("DREAMS")).Resolve(), s.lead);
            Finish(b, s, false);
        }

        // ================= The rite: the believers behind the priest =================

        /// <summary>
        /// The festival's rite begun at the god's place (Festivals.Hourly): those of the colony who hold the god and are free
        /// walk over and stand in rows behind the one leading it, facing the place, for as long as the rite lasts.
        /// </summary>
        public static void Gather(Pawn leader, Thing shrine, God g, int ticks)
        {
            var map = shrine?.Map;
            if (leader == null || map == null || g == null) return;
            try
            {
                var who = map.mapPawns.FreeColonistsSpawned.Where(p => p != leader && Holds(p, g) && OwnHappenings.Free(p) && !p.DevelopmentalStage.Baby())
                    .OrderBy(p => p.Position.DistanceToSquared(shrine.Position)).Take(OwnFeastsMath.GatheringMost).ToList();
                if (who.Count == 0) return;
                // Behind the leader as they will stand: the leader at the place's front, the rows further from it.
                var front = shrine.InteractionCell.IsValid && shrine.def.hasInteractionCell ? shrine.InteractionCell : CellFinder.StandableCellNear(shrine.Position, map, 2f);
                var away = front - shrine.Position;
                var back = new IntVec3(Math.Sign(away.x), 0, Math.Sign(away.z));
                if (back == IntVec3.Zero) back = IntVec3.South;
                var across = new IntVec3(back.z, 0, back.x);
                var spots = OwnFeastsMath.Behind(who.Count);
                for (int i = 0; i < who.Count; i++)
                {
                    var (a, bk) = spots[i];
                    var c = front + back * bk + across * a;
                    if (!c.InBounds(map) || !c.Standable(map)) c = CellFinder.StandableCellNear(c, map, 2f);
                    if (!c.IsValid || !who[i].CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                    var go = GoJob(c, GenDate.TicksPerHour);
                    var stand = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture);
                    stand.expiryInterval = ticks;
                    stand.overrideFacing = Rot4.FromIntVec3(shrine.Position - c);
                    if (!OwnHappenings.Job(who[i], go)) continue;
                    who[i].jobs.jobQueue.EnqueueLast(stand);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods' festivals: the believers at the rite", e);
            }
        }

        // ================= Dev =================

        /// <summary>Dev: the festival kept now, its custom begun at once whatever the hour. Its words, or why it couldn't.</summary>
        internal static string DevStart(God g, Map map)
        {
            var b = OwnHappenings.Book;
            if (b == null || g == null || map == null || !Keeps(g)) return "no such festival here.";
            if (g.Key == "Hollin" && b.feast.felled == 0) b.feast.felled = 5;
            Begin(b, g, map);
            bool begun = Start(b, b.feast, g, map, Now);
            if (!begun)
            {
                b.feast.Clear();
                return "nobody free to keep it, or nowhere to keep it.";
            }
            b.Due(Now + 60);
            return Festivals.Name(g) + " begun (step " + b.feast.stage + ").";
        }

        /// <summary>Dev: the festival under way moved on to its next part (the meal at noon, the story at dusk, the lamps at dusk, dawn, breakfast).</summary>
        internal static string DevNext(Map map)
        {
            var b = OwnHappenings.Book;
            var s = b?.feast;
            var g = s == null ? null : OwnHappenings.God(s.god);
            if (s == null || !s.On || g == null || s.map != map) return "no festival under way here.";
            int now = Now;
            switch (s.god)
            {
                case "Hollin" when s.stage <= 2:
                    s.stage = 2;
                    HollinMeal(b, s, g, map, now);
                    break;
                case "Bellwether" when s.stage == 1:
                    BellwetherStory(s, g, map, now);
                    break;
                case "Pip" when s.stage < 4:
                    Round(s, g, map, now, false);
                    break;
                case "Pip":
                case "Undertow" when s.stage == 4 && s.told:
                    Dawn(b, s, g, map);
                    break;
                case "Undertow" when s.stage == 4:
                    UndertowNight(s, g, map);
                    break;
                case "Selene" when s.stage == 1:
                    s.stage = 2;
                    s.until = now + GenDate.TicksPerHour * 5;
                    s.done = null;
                    break;
                default:
                    s.until = now;
                    break;
            }
            b.Due(now + 60);
            return s.On ? s.god + " at step " + s.stage + "." : "it ended.";
        }

        // ================= The feast's own jobs =================

        /// <summary>A feast's job done at its target (JobDriver_FeastWork): what it was for.</summary>
        internal static void WorkDone(Pawn p, JobDef job, LocalTargetInfo target)
        {
            if (p == null || job == null) return;
            try
            {
                if (job == PlantJob) Planted(p, target.Cell);
                else if (job == DigJob) Dug(p, target.Cell);
                else if (job == CookJob) Cooked(p, target.Thing);
                else if (job == MachineJob) Machine(p, target.Thing);
                else if (job == LampJob) Lamp(p, target.Thing);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods' festivals: work done", e);
            }
        }

        private static void Machine(Pawn p, Thing t)
        {
            var s = S;
            if (s == null || s.god != "Pip" || t == null) return;
            s.count++;
            FleckMaker.ThrowMicroSparks(t.TrueCenter(), p.Map);
            if (s.count == 1) OwnHappenings.SayFrom(p, Pool, OwnHappenings.God("Pip"), "pip", s.benches ? "bench" : "machine");
        }
    }

    /// <summary>
    /// A feast's own work at a cell or a thing, or a queue of them (Gods/OnMap/OwnFeasts.cs): there, a while at it (dust for
    /// the earth, sparks for a machine or the stove, nothing for a lamp or a door), then what it was for
    /// (OwnFeasts.WorkDone); the next in the queue, if any. A job loaded halfway starts its current target again.
    /// </summary>
    public class JobDriver_FeastWork : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var next = ToilMaker.MakeToil("FeastNext");
            next.initAction = () =>
            {
                if (job.targetA.IsValid) return;
                if (!job.targetQueueA.NullOrEmpty())
                {
                    job.targetA = job.targetQueueA[0];
                    job.targetQueueA.RemoveAt(0);
                }
                else EndJobWith(JobCondition.Succeeded);
            };
            next.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return next;
            if (job.def == OwnFeasts.CookJob) yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            else yield return Toils_Goto.Goto(TargetIndex.A, PathEndMode.Touch);
            var work = ToilMaker.MakeToil("FeastWork");
            work.tickIntervalAction = delta =>
            {
                var t = job.targetA;
                if (!t.IsValid) return;
                pawn.rotationTracker.FaceCell(t.Cell);
                if (!pawn.IsHashIntervalTick(200, delta)) return;
                if (job.def == OwnFeasts.MachineJob || job.def == OwnFeasts.CookJob) FleckMaker.ThrowMicroSparks(t.Cell.ToVector3Shifted(), pawn.Map);
                else if (job.def == OwnFeasts.PlantJob || job.def == OwnFeasts.DigJob) FleckMaker.ThrowDustPuff(t.Cell, pawn.Map, 0.4f);
            };
            work.WithProgressBarToilDelay(TargetIndex.A);
            work.handlingFacing = true;
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(60, job.count);
            yield return work;
            var done = ToilMaker.MakeToil("FeastDone");
            done.initAction = () =>
            {
                OwnFeasts.WorkDone(pawn, job.def, job.targetA);
                job.targetA = LocalTargetInfo.Invalid;
                if (!job.targetQueueA.NullOrEmpty()) JumpToToil(next);
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }
}
