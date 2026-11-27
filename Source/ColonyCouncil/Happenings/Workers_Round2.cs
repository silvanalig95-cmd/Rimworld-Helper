using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // The content update's second round, its storyteller events (docs/content-update-proposals-2.md, D16 to D23;
    // Defs/IncidentDefs/Rimfolk_Round2_Events.xml): ordinary happenings on the map in every age, each with a choice and its
    // play-out. Someone not in by dark in the fog or the snow, the fish running in spring and autumn, a bear asleep in the
    // rock a miner broke into, water carried to the fields in a drought, a wild animal acting wrong at dawn, a raider who hid
    // after the raid, an animal stuck in the bog, a pocket of bad air in the deep. What each keeps on the map while it waits
    // and after the answer is one of the things OnMapTimed keeps (PlayOut/OnMapTimed.cs: NightOut, FishRun, BearAsleep,
    // WaterChain, WrongBeast, Hidden, Bog, BadAir); the steps are here (Round2Map). The walled den waits for spring as one of
    // the storyteller's things on their way (Happenings/Workers_Storyteller.cs, a memo of its own kind). Every hurt goes
    // through the shared harm budget (the night's cold, the animal's madness, the bad air's gas), is named under the answer
    // before it's chosen and is shown where it happens. The rules without game types are in Round2EventsMath. Event cards, so
    // plain words; the spoken lines are tagged with their moment.

    /// <summary>The second round's part of what is kept on the map: the steps of each, and what is left when it ends.</summary>
    public static class Round2Map
    {
        /// <summary>The walled den's memo (Workers_Storyteller.cs): pawn the bear, def its cells, other who found it.</summary>
        public const string Den = "beardenwall";

        /// <summary>The feast kept late after a death (Dilemmas/DilemmaWorkers_Round2.cs): def the god's id, text the dead's name.</summary>
        public const string LateFeast = "latefeast";

        private static int Now => Find.TickManager.TicksGame;

        private static int Ticks(float hours) => Mathf.RoundToInt(hours * GenDate.TicksPerHour);

        private static float HoursSince(int tick) => (Now - tick) / (float)GenDate.TicksPerHour;

        /// <summary>A step's share of an hour (each step is one 250-tick pass).</summary>
        private const float StepOfHour = Sched.RareTicks / (float)Sched.HourTicks;

        private static int Hour(Map map) => GenLocalDate.HourOfDay(map);

        // ---------------- Kept on the map ----------------

        /// <summary>Kept from the moment the event is seen: until its letter's time is up and an hour more, at least <paramref name="hours"/>.</summary>
        public static TimedThing Keep(TimedKind kind, HappeningContext ctx, Pawn pawn, Pawn animal, float hours = 2f)
        {
            var timed = OnMapTimed.Instance;
            if (timed == null || ctx?.map == null) return null;
            float h = Mathf.Max(hours, (ctx.Ext?.answerDays ?? 0f) * 24f + 1f);
            var k = new TimedThing { kind = kind, map = ctx.map, cell = ctx.spot, pawn = pawn, animal = animal, where = ctx.incident?.defName, until = Now + Ticks(h) };
            timed.Add(k);
            return k;
        }

        /// <summary>The thing an answer is about, kept on the map (made again if a load or the cap lost it).</summary>
        public static TimedThing Of(TimedKind kind, DilemmaContext ctx)
        {
            var h = ctx as HappeningContext;
            var timed = OnMapTimed.Instance;
            if (h?.map == null || timed == null) return null;
            var k = timed.kept.FirstOrDefault(x => x.kind == kind && x.map == h.map && (h.a != null && x.pawn == h.a || h.stranger != null && x.animal == h.stranger || h.spot.IsValid && x.cell == h.spot));
            if (k != null) return k;
            Pawn animal = h.stranger != null && !(h.stranger.RaceProps?.Humanlike ?? false) ? h.stranger : null;
            Pawn pawn = kind == TimedKind.Hidden ? h.stranger : h.a;
            return Keep(kind, h, pawn, animal);
        }

        public static bool Step(TimedThing k, int now)
        {
            switch (k.kind)
            {
                case TimedKind.NightOut: return NightOut.Step(k, now);
                case TimedKind.FishRun: return FishRun.Step(k, now);
                case TimedKind.BearAsleep: return BearDen.Step(k, now);
                case TimedKind.WaterChain: return WaterChain.Step(k, now);
                case TimedKind.WrongBeast: return WrongBeast.Step(k, now);
                case TimedKind.Hidden: return Hidden.Step(k, now);
                case TimedKind.Bog: return Bog.Step(k, now);
                case TimedKind.BadAir: return BadAir.Step(k, now);
                case TimedKind.Ordeal: return OrdealFire.Step(k, now);
                default: return true;
            }
        }

        public static void End(TimedThing k, bool early)
        {
            switch (k.kind)
            {
                case TimedKind.NightOut: NightOut.End(k); break;
                case TimedKind.FishRun: FishRun.End(k); break;
                case TimedKind.WrongBeast: WrongBeast.End(k); break;
                case TimedKind.Hidden: Hidden.End(k); break;
                case TimedKind.Bog: Bog.End(k); break;
                case TimedKind.BadAir: BadAir.End(k); break;
            }
        }

        // ---------------- Shared ----------------

        /// <summary>One of the colony free to be sent: awake, not drafted, downed, in a break or pressed by a need, and not on work the player gave them.</summary>
        public static bool Free(Pawn p) =>
            p != null && p.Spawned && !p.Dead && !p.Downed && !p.Drafted && !p.InMentalState && p.Awake() && !p.DevelopmentalStage.Juvenile()
            && !PlayOuts.NeedsPress(p) && PlayOuts.Interruptible(p);

        /// <summary>A pawn still on the map and about: not dead, not gone, not downed.</summary>
        public static bool Here(Pawn p, Map map) => p != null && !p.Dead && p.Spawned && p.Map == map && !p.Downed;

        /// <summary>A standable cell near <paramref name="root"/> that <paramref name="p"/> can reach, at random; invalid when none.</summary>
        public static IntVec3 Near(Map map, IntVec3 root, float radius, Pawn p)
        {
            if (map == null || !root.IsValid) return IntVec3.Invalid;
            foreach (var c in GenRadial.RadialCellsAround(root, Mathf.Min(radius, GenRadial.MaxRadialPatternRadius - 1f), true).InRandomOrder().Take(40))
                if (c.InBounds(map) && c.Standable(map) && (p == null || p.CanReach(c, PathEndMode.OnCell, Danger.Some)))
                    return c;
            return IntVec3.Invalid;
        }

        /// <summary>Sends them to <paramref name="at"/> (at a walk, a jog or a run), unless they're on their way there already.</summary>
        public static void GoTo(Pawn p, IntVec3 at, LocomotionUrgency urgency = LocomotionUrgency.Walk)
        {
            if (p == null || !p.Spawned || !at.IsValid) return;
            if (p.CurJobDef == JobDefOf.Goto && p.CurJob.targetA.Cell == at) return;
            if (!p.CanReach(at, PathEndMode.OnCell, Danger.Some)) return;
            var job = JobMaker.MakeJob(JobDefOf.Goto, at);
            job.locomotionUrgency = urgency;
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>A short walk about <paramref name="root"/> (a lost one in the dark, an animal walking in circles).</summary>
        public static void Wander(Pawn p, IntVec3 root, float radius = 6f)
        {
            if (p == null || !p.Spawned || p.Downed || p.CurJobDef == JobDefOf.GotoWander) return;
            var to = Near(p.Map, root, radius, p);
            if (!to.IsValid) return;
            var job = JobMaker.MakeJob(JobDefOf.GotoWander, to);
            job.locomotionUrgency = LocomotionUrgency.Walk;
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>They stand (or sit) where they are for a while.</summary>
        public static void Hold(Pawn p, float hours)
        {
            if (p == null || !p.Spawned || p.Downed || p.CurJobDef == JobDefOf.Wait_MaintainPosture) return;
            var job = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture);
            job.expiryInterval = Ticks(hours);
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>Off the colony's land, by the game's own way out.</summary>
        public static void Leave(Pawn p, LocomotionUrgency urgency = LocomotionUrgency.Walk)
        {
            if (p == null || !p.Spawned || p.Dead || p.Downed) return;
            if (p.CurJob != null && p.CurJob.exitMapOnArrival) return;
            if (!RCellFinder.TryFindBestExitSpot(p, out var spot, TraverseMode.ByPawn)) return;
            var job = JobMaker.MakeJob(JobDefOf.Goto, spot);
            job.exitMapOnArrival = true;
            job.locomotionUrgency = urgency;
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>A cell near the middle of the colony to walk home to.</summary>
        public static IntVec3 Home(Map map, Pawn p) => Near(map, StorytellerEvents.Center(map), 8f, p);

        /// <summary>The thought, if the def is there.</summary>
        public static void Give(Pawn p, string thought, Pawn about = null)
        {
            // together: called for those who were just together on the map (the searchers who walked the lost one home).
            var def = DefDatabase<ThoughtDef>.GetNamedSilentFail(thought);
            if (p == null || p.Dead || def == null) return;
            CouncilGameComponent.GiveThought(p, def, about);
        }

        /// <summary>A plain message about the event.</summary>
        public static void Told(Map map, string text, LookTargets look, MessageTypeDef type = null) =>
            Tell.Message(NewsKind.Colony, Importance.Notable, map, text, look, type ?? MessageTypeDefOf.NeutralEvent);

        /// <summary>
        /// The night's cold for one out in it (D16): every two hours out in a frost, one chance in twenty of frostnip (in a
        /// bitter cold) or chilblains, the cold's own rate (Harm/ColdHurts.cs), through the shared budget; told at once.
        /// </summary>
        public static void Cold(TimedThing k, Pawn p)
        {
            if (p == null || !p.Spawned || !ColdHurts.ColdOut(p)) return;
            k.done += Sched.RareTicks;
            if (!Round2EventsMath.ColdDue(k.done / (float)GenDate.TicksPerHour)) return;
            k.done = 0;
            if (!Harm.Roll(p, HarmKinds.ColdNight, Round2EventsMath.ColdChance, HurtClass.Mild)) return;
            var def = HarmFour.Def(p.Map.mapTemperature.OutdoorTemp <= -20f ? "Council_Ail_Frostnip" : "Council_Ail_Chilblains");
            if (def == null || p.health.hediffSet.HasHediff(def)) return;
            var info = AilmentTracker.InfoOf(def);
            var h = Harm.Ail(p, def, info?.startSeverity.RandomInRange ?? 0.3f, HarmKinds.ColdNight, HurtClass.Mild);
            if (h == null) return;
            if (info != null) AilmentTracker.Instance?.Notify_Caught(p, h);
            Tell.Message(NewsKind.Health, Importance.Important, p.Map, "CC_R2_Lost_Cold".Translate(p.Named("PAWN"), AilmentTracker.NameOf(def).Named("ILLNESS")).Resolve(),
                new LookTargets(p), MessageTypeDefOf.NegativeHealthEvent);
        }

        /// <summary>Those of the colony who hold <paramref name="godKey"/>'s god at least leaning toward it.</summary>
        public static IEnumerable<Pawn> Holders(Map map, string godKey)
        {
            var g = Gods.On ? Gods.ByKey(godKey) : null;
            if (g == null || map == null) yield break;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
                if (PawnGods.HoldOf(p, g) >= PawnGodsMath.LeansFrom && !PawnGods.Dreads(p, g))
                    yield return p;
        }

        /// <summary>A wall's cheapest stuff the colony has enough of now, else wood.</summary>
        public static ThingDef WallStuff(Map map)
        {
            var wall = ThingDefOf.Wall;
            int cost = Math.Max(1, wall.costStuffCount);
            return map.resourceCounter.AllCountedAmounts.Where(kv => kv.Value >= cost && kv.Key.IsStuff && GenStuff.AllowedStuffsFor(wall).Contains(kv.Key))
                       .OrderBy(kv => kv.Key.BaseMarketValue).Select(kv => kv.Key).FirstOrDefault()
                   ?? ThingDefOf.WoodLog;
        }

        /// <summary>A wall's blueprint at <paramref name="cell"/> (a gap closed). False when it can't be placed.</summary>
        public static bool WallAt(Map map, IntVec3 cell)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map)) return false;
            var stuff = WallStuff(map);
            if (!GenConstruct.CanPlaceBlueprintAt(ThingDefOf.Wall, cell, Rot4.North, map, false, null, null, stuff).Accepted) return false;
            GenConstruct.PlaceBlueprintForBuild(ThingDefOf.Wall, cell, map, Rot4.North, Faction.OfPlayer, stuff);
            return true;
        }

        /// <summary>Two cells written as "x,z,x,z" (a memo's), read back.</summary>
        public static bool Cells(string s, out IntVec3 a, out IntVec3 b)
        {
            a = b = IntVec3.Invalid;
            var parts = (s ?? "").Split(',');
            if (parts.Length < 4 || !int.TryParse(parts[0], out int ax) || !int.TryParse(parts[1], out int az) || !int.TryParse(parts[2], out int bx) || !int.TryParse(parts[3], out int bz)) return false;
            a = new IntVec3(ax, 0, az);
            b = new IntVec3(bx, 0, bz);
            return true;
        }
    }

    // ===================================================================================================
    // D16. Not in by dark
    // ===================================================================================================

    /// <summary>
    /// At dusk in fog or falling snow, someone working or playing far out from the colony (A) hasn't come in: they've lost
    /// their way, and walk about where they were last seen. B is the one nearest who could go. {PLACE}: which way.
    /// </summary>
    public class HappeningWorker_NotInByDark : IncidentWorker_Happening
    {
        /// <summary>Fog (the game's, Odyssey's blind fog, Seasonal Weather's and ReGrowth's fog in the snow) or snow falling.</summary>
        public static bool Dark(Map map)
        {
            var w = map?.weatherManager?.curWeather;
            return w != null && (w.defName.IndexOf("Fog", StringComparison.OrdinalIgnoreCase) >= 0 || w.snowRate > 0.1f);
        }

        private static bool Out(Pawn p, Map map, IntVec3 center) =>
            p.Spawned && !p.Downed && !p.Drafted && !p.InMentalState && p.Awake() && !p.DevelopmentalStage.Baby() && p.GetLord() == null
            && ColdHurts.Outdoors(p) && !map.areaManager.Home[p.Position] && p.Position.DistanceTo(center) >= Round2EventsMath.LostFar && PlayOuts.Interruptible(p);

        public static Pawn Lost(Map map)
        {
            var c = StorytellerEvents.Center(map);
            return Colonists(map).Where(p => Out(p, map, c)).OrderByDescending(p => p.Position.DistanceToSquared(c)).FirstOrDefault();
        }

        private static Pawn Seeker(Map map, Pawn lost) =>
            StorytellerEvents.Adults(map).Where(p => p != lost && Round2Map.Free(p)).OrderBy(p => p.Position.DistanceToSquared(lost.Position)).FirstOrDefault();

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            !MapFacts.ThreatOn(map) && Round2EventsMath.AtDusk(GenLocalDate.HourOfDay(map)) && Dark(map) && Lost(map) is Pawn a && Seeker(map, a) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = Lost(ctx.map);
            if (ctx.a == null) return false;
            ctx.b = Seeker(ctx.map, ctx.a);
            if (ctx.b == null) return false;
            ctx.spot = ctx.a.Position;
            ctx.placeName = StorytellerEvents.Direction(ctx.map, ctx.a.Position);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var k = Round2Map.Keep(TimedKind.NightOut, ctx, ctx.a, null);
            if (k == null) return false;
            ctx.a.jobs.EndCurrentJob(JobCondition.InterruptForced);
            Round2Map.Wander(ctx.a, k.cell);
            MapDangers.Say(k, ctx.a, "lost");
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.a != null && ctx.a.DevelopmentalStage.Child()) tags.Add("child");
            var w = ctx.map?.weatherManager?.curWeather;
            if (w != null) tags.Add(w.defName.IndexOf("Fog", StringComparison.OrdinalIgnoreCase) >= 0 ? "fog" : "snow");
            Lands.AddTags(ctx.map, tags);
        }
    }

    /// <summary>
    /// What the council says about the one not in by dark: <c>search</c> (two or three go out with lights toward where they
    /// were last seen, calling, find them and walk home together), <c>light</c> (B stands at the edge of the colony with a
    /// light and a horn or bell until they walk in toward it, later and cold) or <c>morning</c> (they wait out the night
    /// where they are and find their own way at first light, colder).
    /// </summary>
    public class DilemmaEffect_NightOut : DilemmaEffect
    {
        public string mode = "search";

        public override bool ChanceEach => false;

        private static bool Frost(Map map) => map != null && map.mapTemperature.OutdoorTemp <= HarmFiveMath.HardFrost;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.a == null || ctx.map == null) return;
            switch (mode)
            {
                case "search":
                    sheet.Play("CC_R2_Impact_LostSearch".Translate(ctx.a.LabelShort).Resolve(), "CC_R2_Impact_LostSearchBrief".Translate().Resolve());
                    break;
                case "light":
                    sheet.Play("CC_R2_Impact_LostLight".Translate((ctx.b ?? ctx.a).LabelShort, ctx.a.LabelShort).Resolve(), "CC_R2_Impact_LostLightBrief".Translate().Resolve());
                    break;
                default:
                    sheet.Play("CC_R2_Impact_LostMorning".Translate(ctx.a.LabelShort).Resolve(), "CC_R2_Impact_LostMorningBrief".Translate().Resolve());
                    break;
            }
            if (mode != "search" && Harm.On && Frost(ctx.map))
                sheet.Add(ImpactKind.Health, -1, "CC_R2_Impact_LostCold".Translate(ctx.a.LabelShort).Resolve(), "CC_R2_Impact_LostColdBrief".Translate().Resolve(), "r2cold");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var k = Round2Map.Of(TimedKind.NightOut, ctx);
            if (k == null || ctx.a == null) return;
            int now = Find.TickManager.TicksGame;
            k.start = now;
            k.done = 0;
            k.text = mode;
            k.pawns.Clear();
            switch (mode)
            {
                case "search":
                    var go = StorytellerEvents.Adults(ctx.map).Where(p => p != ctx.a && Round2Map.Free(p)).OrderBy(p => p == ctx.b ? 0 : 1).ThenBy(p => p.Position.DistanceToSquared(ctx.a.Position)).Take(3).ToList();
                    k.pawns.AddRange(go);
                    k.count = k.pawns.Count > 0 ? 1 : 3;
                    k.until = now + Mathf.RoundToInt(Round2EventsMath.NightOutMaxHours * GenDate.TicksPerHour);
                    break;
                case "light":
                    var b = ctx.b != null && Round2Map.Free(ctx.b) ? ctx.b : StorytellerEvents.Adults(ctx.map).FirstOrDefault(p => p != ctx.a && Round2Map.Free(p));
                    var edge = Edge(ctx.map, ctx.a.Position, b);
                    if (b == null || !edge.IsValid)
                    {
                        k.count = 3;
                    }
                    else
                    {
                        k.pawns.Add(b);
                        k.cells.Clear();
                        k.cells.Add(edge);
                        k.count = 2;
                    }
                    k.until = now + Mathf.RoundToInt(Round2EventsMath.NightOutMaxHours * GenDate.TicksPerHour);
                    break;
                default:
                    k.count = 3;
                    k.until = now + Mathf.RoundToInt(Round2EventsMath.NightOutMaxHours * GenDate.TicksPerHour);
                    break;
            }
        }

        /// <summary>The edge of the colony's home ground nearest the lost one, where a light can be seen.</summary>
        private static IntVec3 Edge(Map map, IntVec3 lost, Pawn b)
        {
            var home = map.areaManager.Home;
            IntVec3 best = IntVec3.Invalid;
            float bestD = float.MaxValue;
            foreach (var c in home.ActiveCells)
            {
                float d = c.DistanceToSquared(lost);
                if (d >= bestD || !c.Standable(map)) continue;
                best = c;
                bestD = d;
            }
            if (best.IsValid && b != null && !b.CanReach(best, PathEndMode.OnCell, Danger.Some)) return IntVec3.Invalid;
            return best;
        }
    }

    /// <summary>The steps of one not in by dark (TimedKind.NightOut): k.count 0 waiting, 1 searched for, 2 the light at the edge, 3 a night out, 4 walking home.</summary>
    public static class NightOut
    {
        private static int Now => Find.TickManager.TicksGame;

        private static float HoursSince(int tick) => (Now - tick) / (float)GenDate.TicksPerHour;

        public static bool Step(TimedThing k, int now)
        {
            var a = k.pawn;
            var map = k.map;
            if (a == null || a.Dead || !a.Spawned || a.Map != map || a.Downed || a.InMentalState) return true;
            // The player has them now, a raid matters more, or a need of their own presses (they make their own way in).
            if (a.Drafted || MapFacts.ThreatOn(map) || PlayOuts.NeedsPress(a)) return true;
            k.pawns.RemoveAll(p => p == null || p.Dead || !p.Spawned || p.Map != map);
            switch (k.count)
            {
                case 0:
                    Round2Map.Wander(a, k.cell);
                    return now > k.until;
                case 1:
                    return Search(k, a, map, now);
                case 2:
                    return Light(k, a, map, now);
                case 3:
                    return Morning(k, a, map, now);
                default:
                    return Walk(k, a, map, now);
            }
        }

        private static bool Search(TimedThing k, Pawn a, Map map, int now)
        {
            var going = k.pawns.Where(p => !p.Downed && !p.Drafted && !p.InMentalState && !PlayOuts.NeedsPress(p)).ToList();
            if (going.Count == 0)
            {
                // Nobody left to look: they find their own way at first light.
                k.count = 3;
                return false;
            }
            if (going.Any(p => p.Position.DistanceTo(a.Position) <= Round2EventsMath.FoundWithin))
            {
                var finder = going.OrderBy(p => p.Position.DistanceToSquared(a.Position)).First();
                MapDangers.Say(k, finder, "found");
                k.on = true;
                k.count = 4;
                return false;
            }
            foreach (var p in going)
            {
                if (p.CurJobDef == JobDefOf.Goto && p.CurJob.targetA.Cell.DistanceTo(a.Position) <= 3f) continue;
                Round2Map.GoTo(p, a.Position, LocomotionUrgency.Jog);
            }
            if (Rand.Chance(0.12f)) MapDangers.Say(k, going.RandomElement(), "call");
            // Walking about where they are, a little.
            Round2Map.Wander(a, a.Position, 3f);
            if (HoursSince(k.start) >= Round2EventsMath.SearchLongHours) Round2Map.Cold(k, a);
            return now > k.until;
        }

        private static bool Light(TimedThing k, Pawn a, Map map, int now)
        {
            var b = k.pawns.FirstOrDefault();
            var edge = k.cells.FirstOrDefault();
            if (b == null || !edge.IsValid || b.Downed || b.Drafted || b.InMentalState)
            {
                k.count = 3;
                return false;
            }
            if (b.Position.DistanceTo(edge) > 2f) Round2Map.GoTo(b, edge, LocomotionUrgency.Jog);
            else
            {
                Round2Map.Hold(b, 2f);
                if (Rand.Chance(0.1f)) MapDangers.Say(k, b, "horn");
            }
            if (HoursSince(k.start) >= Round2EventsMath.HearsAfterHours) Round2Map.GoTo(a, edge);
            else Round2Map.Wander(a, a.Position, 3f);
            Round2Map.Cold(k, a);
            if (a.Position.DistanceTo(b.Position) <= Round2EventsMath.FoundWithin)
            {
                MapDangers.Say(k, a, "in");
                k.on = true;
                k.count = 4;
                return false;
            }
            return now > k.until;
        }

        private static bool Morning(TimedThing k, Pawn a, Map map, int now)
        {
            if (Round2EventsMath.StillNight(GenLocalDate.HourOfDay(map)) && now <= k.until)
            {
                Round2Map.Hold(a, 1f);
                Round2Map.Cold(k, a);
                return false;
            }
            if (!k.on)
            {
                k.on = true;
                MapDangers.Say(k, a, "dawn");
            }
            k.count = 4;
            return false;
        }

        private static bool Walk(TimedThing k, Pawn a, Map map, int now)
        {
            var home = StorytellerEvents.Center(map);
            if (map.areaManager.Home[a.Position] || a.Position.DistanceTo(home) <= 12f) return true;
            if (!(a.CurJobDef == JobDefOf.Goto && a.CurJob.targetA.Cell.DistanceTo(home) <= 10f))
            {
                var to = Round2Map.Home(map, a);
                Round2Map.GoTo(a, to);
                // together: the searchers found them and walk home beside them.
                foreach (var p in k.pawns)
                    if (!p.Downed && !p.Drafted && !p.InMentalState && p.Position.DistanceTo(a.Position) <= 10f)
                        Round2Map.GoTo(p, Round2Map.Near(map, to, 3f, p));
            }
            return now > k.until;
        }

        /// <summary>Home again: the lost one thinks better of those who came; the night out stays with them.</summary>
        public static void End(TimedThing k)
        {
            var a = k.pawn;
            if (a == null || a.Dead || !k.on) return;
            switch (k.text)
            {
                case "search":
                    foreach (var p in k.pawns.Where(p => p != null && !p.Dead))
                    {
                        // together: they found them out there and walked home beside them.
                        Round2Map.Give(a, "Rimfolk_R2_CameForMe", p);
                        Round2Map.Give(p, "Rimfolk_R2_BroughtHome");
                    }
                    break;
                case "light":
                    Round2Map.Give(a, "Rimfolk_R2_WalkedToTheLight");
                    var b = k.pawns.FirstOrDefault();
                    if (b != null && !b.Dead) Round2Map.Give(b, "Rimfolk_R2_KeptTheLight");
                    break;
                default:
                    Round2Map.Give(a, "Rimfolk_R2_NightOut");
                    break;
            }
        }
    }

    // ===================================================================================================
    // D17. The fish are running
    // ===================================================================================================

    /// <summary>
    /// In spring or autumn the water at the colony's fishing ground fills with fish for three days (FishRun keeps it: the
    /// splashing, and more fish for those at the water while it lasts). A is the one who saw it. One run in three, a bear
    /// comes down to the bank (X) and keeps to itself unless hunted. {THING}: the fish.
    /// </summary>
    public class HappeningWorker_FishRunning : IncidentWorker_Happening
    {
        private static readonly string[] FishNames = { "Fish_Salmon", "Fish_Cod", "VCEF_RawTrout", "VCEF_RawHerring" };

        public static Zone Ground(Map map) =>
            map.zoneManager.AllZones.Where(z => z != null && z.Cells.Count > 0 && z.GetType().Name.IndexOf("Fishing", StringComparison.Ordinal) >= 0)
                .OrderByDescending(z => z.Cells.Count).FirstOrDefault();

        private static ThingDef Fish() => FishNames.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null).RandomElementWithFallback();

        public static PawnKindDef Bear(Map map)
        {
            var names = map.mapTemperature.OutdoorTemp < -10f ? new[] { "Bear_Polar", "Bear_Grizzly" } : new[] { "Bear_Grizzly", "AEXP_BlackBear" };
            return names.Select(n => DefDatabase<PawnKindDef>.GetNamedSilentFail(n)).Where(k => k != null && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 5f))
                .RandomElementWithFallback();
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            !MapFacts.ThreatOn(map) && Fish() != null && Ground(map) != null && DilemmaWorker_CustomMore.Fishing(map)
            && !(OnMapTimed.Instance?.kept.Any(k => k.kind == TimedKind.FishRun && k.map == map) ?? false);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var z = Ground(ctx.map);
            ctx.thing = Fish();
            if (z == null || ctx.thing == null) return false;
            var cells = z.Cells;
            ctx.spot = cells[cells.Count / 2];
            ctx.a = StorytellerEvents.Adults(ctx.map).OrderBy(p => p.Position.DistanceToSquared(ctx.spot)).FirstOrDefault();
            if (ctx.a == null) return false;
            ctx.placeName = StorytellerEvents.Direction(ctx.map, ctx.spot);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var z = Ground(map);
            if (z == null) return false;
            var k = Round2Map.Keep(TimedKind.FishRun, ctx, null, null, Round2EventsMath.FishRunDays * 24f);
            if (k == null) return false;
            k.until = Find.TickManager.TicksGame + Mathf.RoundToInt(Round2EventsMath.FishRunDays * GenDate.TicksPerDay);
            k.text = ctx.thing.defName;
            k.cells.AddRange(z.Cells.InRandomOrder().Take(60));
            for (int i = 0; i < 4; i++) FishRun.Splash(k);
            // One run in three, a bear comes down to the bank too.
            var kind = Rand.Chance(Round2EventsMath.BearChance) ? Bear(map) : null;
            if (kind != null && CellFinder.TryFindRandomEdgeCellNearWith(ctx.spot, 40f, map, c => c.Standable(map) && !c.Fogged(map) && map.reachability.CanReach(c, ctx.spot, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors)), out var edge))
            {
                var bear = PawnGenerator.GeneratePawn(kind);
                GenSpawn.Spawn(bear, edge, map);
                k.animal = bear;
                ctx.stranger = bear;
            }
            MapDangers.Say(k, ctx.a, "run");
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.stranger != null) tags.Add("bear");
            if (ctx.map != null && Round2Map.Holders(ctx.map, "Murmur").Any()) tags.Add("murmur");
            if (HappeningWorker_FishRunning.CanPemmican(ctx.map)) tags.Add("pemmican");
            Lands.AddTags(ctx.map, tags);
        }

        /// <summary>A station that makes pemmican, the recipe known.</summary>
        public static bool CanPemmican(Map map) => Pemmican(map, out _) != null;

        public static RecipeDef Pemmican(Map map, out Building_WorkTable at)
        {
            at = null;
            var r = DefDatabase<RecipeDef>.GetNamedSilentFail("Make_Pemmican");
            if (map == null || r == null || !r.AvailableNow) return null;
            at = map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().FirstOrDefault(b => b.def.AllRecipes != null && b.def.AllRecipes.Contains(r) && b.CurrentlyUsableForBills());
            return at != null ? r : null;
        }
    }

    /// <summary>The few fish now and the rest put by for winter: a pemmican bill at the colony's stove (the game's own cooking).</summary>
    public class DilemmaEffect_PutBy : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var r = HappeningWorker_FishRunning.Pemmican(ctx?.map, out var at);
            if (r == null) return;
            sheet.Play("CC_R2_Impact_PutBy".Translate(Round2EventsMath.PemmicanBill, at.LabelShort).Resolve(), "CC_R2_Impact_PutByBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var r = HappeningWorker_FishRunning.Pemmican(ctx?.map, out var at);
            if (r == null) return;
            if (!(r.MakeNewBill() is Bill_Production bill)) return;
            bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
            bill.repeatCount = Round2EventsMath.PemmicanBill;
            at.BillStack.AddBill(bill);
            at.BillStack.Reorder(bill, -at.BillStack.Bills.IndexOf(bill));
            Round2Map.Told(ctx.map, "CC_R2_Fish_PutBy".Translate(Round2EventsMath.PemmicanBill, at.LabelShort).Resolve(), new LookTargets(at));
        }
    }

    /// <summary>The fish running (TimedKind.FishRun): the splashing at the water, more fish for those fishing, the bear by the bank.</summary>
    public static class FishRun
    {
        private const float StepOfHour = Sched.RareTicks / (float)Sched.HourTicks;

        public static void Splash(TimedThing k)
        {
            var map = k.map;
            if (k.cells.Count == 0) return;
            var c = k.cells.RandomElement();
            if (c.InBounds(map) && !c.Fogged(map)) FleckMaker.WaterSplash(c.ToVector3Shifted(), map, 1.2f, 2f);
        }

        public static bool Step(TimedThing k, int now)
        {
            var map = k.map;
            if (now > k.until) return true;
            if (Rand.Chance(0.5f)) Splash(k);
            var fish = DefDatabase<ThingDef>.GetNamedSilentFail(k.text ?? "");
            if (fish != null)
                foreach (var p in map.mapPawns.FreeColonistsSpawned)
                {
                    var job = p.CurJob;
                    if (job == null || job.def.defName.IndexOf("Fish", StringComparison.Ordinal) < 0) continue;
                    if (!k.cells.Any(c => c.DistanceTo(p.Position) <= 6f)) continue;
                    if (!Rand.Chance(Round2EventsMath.ExtraFishPerHour * StepOfHour)) continue;
                    var t = ThingMaker.MakeThing(fish);
                    t.stackCount = Rand.RangeInclusive(1, 3);
                    GenPlace.TryPlaceThing(t, p.Position, map, ThingPlaceMode.Near);
                    FleckMaker.WaterSplash(p.DrawPos, map, 1f, 2f);
                }
            Bear(k);
            return false;
        }

        /// <summary>The bear keeps to the bank and to itself, unless the colony goes after it.</summary>
        private static void Bear(TimedThing k)
        {
            var bear = k.animal;
            if (bear == null || bear.Dead || !bear.Spawned || bear.Map != k.map || bear.Downed || bear.InMentalState || bear.Faction != null) return;
            if (k.map.designationManager.DesignationOn(bear) != null) return;
            if (bear.Position.DistanceTo(k.cell) <= 12f || bear.CurJobDef == JobDefOf.Goto) return;
            var to = Round2Map.Near(k.map, k.cell, 10f, bear);
            if (to.IsValid) Round2Map.GoTo(bear, to);
        }

        /// <summary>The run is over: the bear goes back the way it came.</summary>
        public static void End(TimedThing k)
        {
            var bear = k.animal;
            if (bear == null || bear.Dead || !bear.Spawned || bear.Faction != null || bear.InMentalState || bear.Downed) return;
            if (k.map.designationManager.DesignationOn(bear) != null) return;
            Round2Map.Leave(bear);
        }
    }

    // ===================================================================================================
    // D18. A bear asleep in the rock
    // ===================================================================================================

    /// <summary>
    /// In winter, a miner (A) under a mountain's roof breaks into a hollow and finds a bear asleep in it (X). B is the
    /// colony's best with animals. {THING}: the rock; {PLACE}: which way. The bear sleeps on while the council decides.
    /// </summary>
    public class HappeningWorker_BearDen : IncidentWorker_Happening
    {
        private static bool Miner(Map map, out Pawn miner, out Thing face, out IntVec3 hollow)
        {
            miner = null;
            face = null;
            hollow = IntVec3.Invalid;
            foreach (var p in Colonists(map))
            {
                if (!StorytellerEvents.Able(p) || p.CurJobDef != JobDefOf.Mine || !(p.CurJob.targetA.Thing is Thing t) || !t.Spawned) continue;
                if (!(map.roofGrid.RoofAt(t.Position)?.isThickRoof ?? false)) continue;
                var h = Hollow(map, t.Position, p.Position);
                if (!h.IsValid) continue;
                miner = p;
                face = t;
                hollow = h;
                return true;
            }
            return false;
        }

        /// <summary>The cell behind the face, away from the miner: rock under the mountain, closed on every other side.</summary>
        private static IntVec3 Hollow(Map map, IntVec3 face, IntVec3 from)
        {
            var d = new IntVec3(Math.Sign(face.x - from.x), 0, Math.Sign(face.z - from.z));
            if (d == IntVec3.Zero) return IntVec3.Invalid;
            var h = face + d;
            var back = h + d;
            if (!h.InBounds(map) || !back.InBounds(map) || h.OnEdge(map) || back.OnEdge(map)) return IntVec3.Invalid;
            if (!StorytellerEvents.NaturalRock(h, map) || !StorytellerEvents.NaturalRock(back, map)) return IntVec3.Invalid;
            if (!(map.roofGrid.RoofAt(h)?.isThickRoof ?? false)) return IntVec3.Invalid;
            foreach (var c in GenAdj.CellsAdjacent8Way(new TargetInfo(h, map)))
                if (c != face && c.InBounds(map) && c.Walkable(map)) return IntVec3.Invalid;
            return h;
        }

        public static PawnKindDef Kind(Map map)
        {
            var names = map.mapTemperature.OutdoorTemp < -10f ? new[] { "Bear_Polar", "Bear_Grizzly" } : new[] { "Bear_Grizzly", "AEXP_BlackBear" };
            return names.Select(n => DefDatabase<PawnKindDef>.GetNamedSilentFail(n)).Where(k => k != null).RandomElementWithFallback();
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => !MapFacts.ThreatOn(map) && Kind(map) != null && Miner(map, out _, out _, out _);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            if (!Miner(ctx.map, out var miner, out var face, out var hollow)) return false;
            ctx.a = miner;
            ctx.spot = face.Position;
            ctx.thing = face.def;
            ctx.placeName = StorytellerEvents.Direction(ctx.map, face.Position);
            ctx.note = hollow.x + "," + hollow.z;
            var adults = StorytellerEvents.Adults(ctx.map).ToList();
            ctx.b = VarietyThree.Best(adults, SkillDefOf.Animals, WorkTypeDefOf.Handling, miner) ?? adults.FirstOrDefault(p => p != miner);
            return ctx.b != null;
        }

        /// <summary>The rock broken into, the bear asleep in the hollow, the miner stepping back.</summary>
        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var kind = Kind(map);
            var parts = ctx.note.Split(',');
            var hollow = new IntVec3(int.Parse(parts[0]), 0, int.Parse(parts[1]));
            var face = ctx.spot;
            if (kind == null || !StorytellerEvents.NaturalRock(face, map) || !StorytellerEvents.NaturalRock(hollow, map)) return false;
            var dm = map.designationManager;
            foreach (var c in GenRadial.RadialCellsAround(face, 2.9f, true).Where(c => c.InBounds(map)))
            {
                var d = dm.DesignationAt(c, DesignationDefOf.Mine) ?? dm.DesignationAt(c, DesignationDefOf.MineVein);
                if (d != null) dm.RemoveDesignation(d);
            }
            ctx.a.jobs.EndCurrentJob(JobCondition.InterruptForced);
            face.GetEdifice(map)?.Destroy(DestroyMode.Vanish);
            hollow.GetEdifice(map)?.Destroy(DestroyMode.Vanish);
            FloodFillerFog.FloodUnfog(face, map);
            FilthMaker.TryMakeFilth(face, map, ThingDefOf.Filth_RubbleRock, 2);
            FleckMaker.ThrowDustPuffThick(face.ToVector3Shifted(), map, 1.5f, new Color(0.75f, 0.7f, 0.6f));
            var bear = PawnGenerator.GeneratePawn(kind);
            GenSpawn.Spawn(bear, hollow, map);
            ctx.stranger = bear;
            BearDen.Sleep(bear);
            var back = MapDangersCells.StepBack(ctx.a, face, map);
            if (back.IsValid) ctx.a.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, back), JobTag.Misc, false);
            var k = Round2Map.Keep(TimedKind.BearAsleep, ctx, ctx.a, bear);
            if (k == null) return false;
            k.cells.Add(face);
            k.cells.Add(hollow);
            MapDangers.Say(k, ctx.a, "warn");
            return true;
        }

        protected override void Abort(HappeningContext ctx)
        {
            if (ctx.stranger != null && !ctx.stranger.Spawned) Find.WorldPawns.PassToWorld(ctx.stranger, PawnDiscardDecideMode.Discard);
            ctx.stranger = null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (Avail.IsPreIndustrial) tags.Add("lowtech");
            Lands.AddTags(ctx.map, tags);
        }

        /// <summary>
        /// Spring (the walled den's memo comes due): not yet warm, it waits; the wall across the gap is taken down first (the
        /// game's own deconstructing); then the bear walks out of the hollow and off the land.
        /// </summary>
        public static void Spring(Memo m)
        {
            var map = HappeningTracker.MapFor(m);
            var bear = m.pawn;
            if (map == null || bear == null || bear.Dead || bear.Discarded) return;
            if (!Round2Map.Cells(m.def, out var face, out var hollow) || !face.InBounds(map))
            {
                Find.WorldPawns.ForcefullyKeptPawns.Remove(bear);
                return;
            }
            var season = GenLocalDate.Season(map);
            bool warmSeason = season == Season.Spring || season == Season.Summer || season == Season.PermanentSummer;
            if (!Round2EventsMath.DenOpens(warmSeason, map.mapTemperature.OutdoorTemp))
            {
                Again(m, bear, map, Round2EventsMath.DenLookAgainDays);
                return;
            }
            var ed = face.GetEdifice(map);
            if (ed != null && ed.Faction == Faction.OfPlayer && ed.def.passability == Traversability.Impassable)
            {
                if (map.designationManager.DesignationOn(ed, DesignationDefOf.Deconstruct) == null)
                {
                    map.designationManager.AddDesignation(new Designation(ed, DesignationDefOf.Deconstruct));
                    Round2Map.Told(map, "CC_R2_Den_TakeDown".Translate(bear.LabelShort.Named("BEAR")).Resolve(), new LookTargets(ed));
                }
                Again(m, bear, map, 0.5f);
                return;
            }
            foreach (var t in face.GetThingList(map).ToList())
                if ((t is Blueprint || t is Frame) && t.Faction == Faction.OfPlayer)
                    t.Destroy(DestroyMode.Cancel);
            var at = hollow.InBounds(map) && hollow.Standable(map) ? hollow : face.Standable(map) ? face : IntVec3.Invalid;
            if (!at.IsValid)
            {
                Again(m, bear, map, Round2EventsMath.DenLookAgainDays);
                return;
            }
            Find.WorldPawns.ForcefullyKeptPawns.Remove(bear);
            if (Find.WorldPawns.Contains(bear)) Find.WorldPawns.RemovePawn(bear);
            GenSpawn.Spawn(bear, at, map);
            if (!bear.Spawned) return;
            Round2Map.Leave(bear);
            Round2Map.Told(map, "CC_R2_Den_WalksOut".Translate(bear.LabelShort.Named("BEAR")).Resolve(), new LookTargets(bear));
        }

        private static void Again(Memo m, Pawn bear, Map map, float days) =>
            StorytellerEvents.Schedule(Round2Map.Den, bear, null, m.def, m.text, m.other, days, map);
    }

    /// <summary>The bear asleep in the hollow while the council decides (TimedKind.BearAsleep).</summary>
    public static class BearDen
    {
        public static void Sleep(Pawn bear)
        {
            if (bear == null || !bear.Spawned || bear.Downed || bear.InMentalState) return;
            if (bear.needs?.rest != null) bear.needs.rest.CurLevel = 0.05f;
            if (bear.needs?.food != null) bear.needs.food.CurLevelPercentage = 1f;
            if (bear.CurJobDef == JobDefOf.LayDown) return;
            var job = JobMaker.MakeJob(JobDefOf.LayDown, bear.Position);
            job.forceSleep = true;
            bear.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
        }

        public static bool Step(TimedThing k, int now)
        {
            var bear = k.animal;
            if (bear == null || bear.Dead || !bear.Spawned || bear.Map != k.map || bear.Faction != null || bear.InMentalState) return true;
            // An answer that goes to it (the handler, the hunters): the game's own from here on.
            if (k.count != 0 || k.map.designationManager.DesignationOn(bear) != null) return true;
            Sleep(bear);
            return now > k.until;
        }
    }

    /// <summary>
    /// The bear walled up again: taken off the map to sleep out the winter in the hollow (a memo of the storyteller's,
    /// Round2Map.Den, comes due in spring), and a wall's blueprint across the gap. In the first warm days the wall is taken
    /// down and the bear walks out and off the land (HappeningWorker_BearDen.Spring).
    /// </summary>
    public class DilemmaEffect_WallDen : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx)
        {
            var x = ctx?.stranger;
            return x == null || x.Dead || !x.Spawned || x.Map != ctx.map || x.Faction != null || x.InMentalState ? "CC_DlmE_BeastGone".Translate(x?.LabelShort ?? "").Resolve() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var x = ctx?.stranger;
            if (x == null || !x.Spawned) return;
            sheet.Play("CC_R2_Impact_WallDen".Translate(x.LabelShort, Round2Map.WallStuff(ctx.map).label).Resolve(), "CC_R2_Impact_WallDenBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var bear = ctx?.stranger;
            var map = ctx?.map;
            if (bear == null || map == null || !bear.Spawned || bear.Map != map || bear.Faction != null || bear.InMentalState) return;
            var k = Round2Map.Of(TimedKind.BearAsleep, ctx);
            if (k == null || k.cells.Count < 2) return;
            var face = k.cells[0];
            var hollow = k.cells[1];
            k.count = 1;
            bear.DeSpawn();
            Find.WorldPawns.PassToWorld(bear, PawnDiscardDecideMode.KeepForever);
            StorytellerEvents.Schedule(Round2Map.Den, bear, null, face.x + "," + face.z + "," + hollow.x + "," + hollow.z, bear.LabelShort, ctx.a, Round2EventsMath.DenFirstLookDays, map);
            Round2Map.WallAt(map, face);
        }
    }

    // ===================================================================================================
    // D19. Water for the fields
    // ===================================================================================================

    /// <summary>
    /// A drought (Odyssey's, Vanilla Events Expanded's) with crops in the ground: the reeve (else the best grower, A) asks.
    /// {PLACE}: the field furthest out ({TALE} the biggest), the water within reach of the fields.
    /// </summary>
    public class HappeningWorker_WaterForFields : IncidentWorker_Happening
    {
        public static bool Drought(Map map) =>
            map.gameConditionManager.ActiveConditions.Any(c => c?.def != null && (c.def.defName == "Drought" || c.def.defName == "VEE_Drought"));

        public static List<Zone_Growing> Fields(Map map) =>
            map.zoneManager.AllZones.OfType<Zone_Growing>().Where(z => Growing(z, map).Count() >= Round2EventsMath.FieldAtLeast).ToList();

        public static IEnumerable<Plant> Growing(Zone_Growing z, Map map)
        {
            var want = z.GetPlantDefToGrow();
            foreach (var c in z.Cells)
                if (c.GetPlant(map) is Plant p && p.def == want && p.LifeStage == PlantLifeStage.Growing && p.def.plant.Sowable)
                    yield return p;
        }

        public static IntVec3 Center(Zone z) => z.Cells.Count == 0 ? IntVec3.Invalid : z.Cells[z.Cells.Count / 2];

        /// <summary>Somewhere to fill a pail: a standable cell in or beside water, nearest the field.</summary>
        public static IntVec3 Water(Map map, IntVec3 near)
        {
            foreach (var c in GenRadial.RadialCellsAround(near, Mathf.Min(Round2EventsMath.WaterWithin, GenRadial.MaxRadialPatternRadius - 1f), true))
            {
                if (!c.InBounds(map) || !c.Standable(map) || c.Fogged(map)) continue;
                if (c.GetTerrain(map).IsWater || GenAdj.CardinalDirections.Any(d => (c + d).InBounds(map) && (c + d).GetTerrain(map).IsWater)) return c;
            }
            return IntVec3.Invalid;
        }

        private static Pawn Asker(Map map) =>
            ColonyEvents.Holder(map, Interplay.Reeve) ?? ColonyEvents.Best(StorytellerEvents.Adults(map), SkillDefOf.Plants);

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            if (MapFacts.ThreatOn(map) || !Drought(map) || StorytellerEvents.Adults(map).Count() < 3 || Asker(map) == null) return false;
            var f = Fields(map);
            return f.Count > 0 && Water(map, Center(f.OrderByDescending(z => z.Cells.Count).First())).IsValid;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var fields = Fields(ctx.map);
            if (fields.Count == 0) return false;
            var big = fields.OrderByDescending(z => z.Cells.Count).First();
            ctx.a = Asker(ctx.map);
            if (ctx.a == null) return false;
            if (Interplay.Holds(ctx.a, Interplay.Reeve)) ctx.holder = ctx.a;
            ctx.spot = Center(big);
            ctx.tale = big.label;
            ctx.count = fields.Count;
            var home = StorytellerEvents.Center(ctx.map);
            ctx.placeName = fields.OrderByDescending(z => Center(z).DistanceToSquared(home)).First().label;
            return Water(ctx.map, ctx.spot).IsValid;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.holder != null) tags.Add("reeve");
            if (ctx.count > 1) tags.Add("fields");
            if (ctx.map != null && Round2Map.Holders(ctx.map, "Murmur").Any()) tags.Add("murmur");
            Lands.AddTags(ctx.map, tags);
        }
    }

    /// <summary>
    /// <c>chain</c>: a water chain at dawn: everyone free carries water from the river, lake or well to the biggest field for
    /// two hours, and the watered field grows that day as it would have in rain. <c>letgo</c>: the field furthest out is let
    /// go (its crop marked to be cut, brought in where it can be), and the rest get what water there is (half a day's growth).
    /// </summary>
    public class DilemmaEffect_FieldWater : DilemmaEffect
    {
        public string mode = "chain";

        public override bool ChanceEach => false;

        private static Zone_Growing Far(Map map)
        {
            var home = StorytellerEvents.Center(map);
            return HappeningWorker_WaterForFields.Fields(map).OrderByDescending(z => HappeningWorker_WaterForFields.Center(z).DistanceToSquared(home)).FirstOrDefault();
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            if (ctx?.map == null) return null;
            if (mode == "letgo" && HappeningWorker_WaterForFields.Fields(ctx.map).Count < 2) return "CC_R2_Water_OneField".Translate().Resolve();
            return null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null) return;
            if (mode == "chain")
                sheet.Play("CC_R2_Impact_WaterChain".Translate(Round2EventsMath.WaterChainHours).Resolve(), "CC_R2_Impact_WaterChainBrief".Translate().Resolve());
            else if (Far(ctx.map) is Zone_Growing z)
                sheet.Play("CC_R2_Impact_LetGo".Translate(z.label).Resolve(), "CC_R2_Impact_LetGoBrief".Translate(z.label).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null) return;
            if (mode == "chain")
            {
                var h = ctx as HappeningContext;
                var field = h != null && h.spot.IsValid ? h.spot : HappeningWorker_WaterForFields.Fields(map).Select(HappeningWorker_WaterForFields.Center).FirstOrDefault();
                var water = HappeningWorker_WaterForFields.Water(map, field);
                if (!field.IsValid || !water.IsValid || OnMapTimed.Instance == null) return;
                var k = new TimedThing { kind = TimedKind.WaterChain, map = map, cell = field, where = h?.incident?.defName, until = Find.TickManager.TicksGame + GenDate.TicksPerDay * 2 };
                k.cells.Add(water);
                OnMapTimed.Instance.Add(k);
                return;
            }
            var far = Far(map);
            if (far == null) return;
            var dm = map.designationManager;
            int marked = 0;
            foreach (var p in HappeningWorker_WaterForFields.Growing(far, map).ToList())
            {
                if (dm.DesignationOn(p) != null) continue;
                dm.AddDesignation(new Designation(p, p.HarvestableNow ? DesignationDefOf.HarvestPlant : DesignationDefOf.CutPlant));
                marked++;
            }
            far.allowSow = false;
            int watered = 0;
            foreach (var z in HappeningWorker_WaterForFields.Fields(map).Where(z => z != far))
                watered += WaterChain.Grow(z, map, Round2EventsMath.LetGoGrowthDays);
            Round2Map.Told(map, "CC_R2_Water_LetGo".Translate(far.label, marked).Resolve(), new LookTargets(HappeningWorker_WaterForFields.Center(far), map));
        }
    }

    /// <summary>The water chain (TimedKind.WaterChain): k.count 0 waiting for dawn, 1 carrying for two hours; k.cells[0] the water.</summary>
    public static class WaterChain
    {
        /// <summary>A field's growing plants given <paramref name="days"/> of growth at their full rate (as rain would). Returns how many.</summary>
        public static int Grow(Zone_Growing z, Map map, float days)
        {
            int n = 0;
            foreach (var p in HappeningWorker_WaterForFields.Growing(z, map).ToList())
            {
                float g = Round2EventsMath.DayGrowth(p.def.plant.growDays, days);
                if (g <= 0f) continue;
                p.Growth = Mathf.Min(1f, p.Growth + g);
                p.DirtyMapMesh(map);
                n++;
            }
            return n;
        }

        public static bool Step(TimedThing k, int now)
        {
            var map = k.map;
            var water = k.cells.FirstOrDefault();
            if (!water.IsValid || !(map.zoneManager.ZoneAt(k.cell) is Zone_Growing field)) return true;
            if (MapFacts.ThreatOn(map)) return now > k.until;
            if (k.count == 0)
            {
                int hour = GenLocalDate.HourOfDay(map);
                if (hour < Round2EventsMath.WaterFrom || hour > Round2EventsMath.WaterTo) return now > k.until;
                k.pawns.Clear();
                k.pawns.AddRange(map.mapPawns.FreeColonistsSpawned.Where(p => PlayOuts.OurPerson(p) && Round2Map.Free(p) && p.CanReach(water, PathEndMode.OnCell, Danger.Some)).Take(PlayOutMath.MaxGroup));
                if (k.pawns.Count == 0) return now > k.until;
                k.count = 1;
                k.start = now;
                MapDangers.Say(k, k.pawns[0], "chain");
            }
            if ((now - k.start) / (float)GenDate.TicksPerHour >= Round2EventsMath.WaterChainHours)
            {
                int n = Grow(field, map, Round2EventsMath.WaterGrowthDays);
                Round2Map.Told(map, "CC_R2_Water_Done".Translate(field.label, k.done).Resolve(), new LookTargets(k.cell, map), MessageTypeDefOf.PositiveEvent);
                foreach (var p in k.pawns.Where(p => p != null && !p.Dead)) Round2Map.Give(p, "Rimfolk_R2_WaterChain");
                k.on = n > 0;
                return true;
            }
            foreach (var p in k.pawns)
            {
                if (p == null || p.Dead || !p.Spawned || p.Map != map || p.Downed || p.Drafted || p.InMentalState || PlayOuts.NeedsPress(p)) continue;
                if (p.CurJobDef == JobDefOf.Goto && (p.CurJob.targetA.Cell == water || field.ContainsCell(p.CurJob.targetA.Cell))) continue;
                if (p.Position.DistanceTo(water) <= 2f)
                {
                    // Filled: to the field with it.
                    var to = field.Cells.Where(c => c.Standable(map)).InRandomOrder().FirstOrDefault();
                    Round2Map.GoTo(p, to.IsValid ? to : k.cell);
                }
                else if (field.ContainsCell(p.Position))
                {
                    // Poured out: back to the water.
                    FleckMaker.WaterSplash(p.DrawPos, map, 1f, 1.5f);
                    k.done++;
                    Round2Map.GoTo(p, water);
                }
                else Round2Map.GoTo(p, water);
            }
            return now > k.until;
        }
    }

    // ===================================================================================================
    // D20. The animal that's wrong
    // ===================================================================================================

    /// <summary>
    /// At dawn someone (A) sees a wild animal (X) at the edge of the colony acting wrong: walking in circles, foaming,
    /// snapping at nothing. B is the colony's best shot. With Sanguinary Animals Reborn, one in four is a sanguine deer.
    /// </summary>
    public class HappeningWorker_WrongAnimal : IncidentWorker_Happening
    {
        private static bool Wild(Pawn x, Map map, IntVec3 center) =>
            x.Faction == null && x.RaceProps.Animal && !x.RaceProps.Insect && !x.RaceProps.Dryad && !x.Downed && !x.InMentalState && x.Spawned
            && x.RaceProps.baseBodySize >= 0.3f && !x.Position.Fogged(map) && !map.areaManager.Home[x.Position]
            && x.Position.DistanceTo(center) >= 15f && x.Position.DistanceTo(center) <= 60f && map.designationManager.DesignationOn(x) == null;

        public static Pawn Beast(Map map)
        {
            var c = StorytellerEvents.Center(map);
            return map.mapPawns.AllPawnsSpawned.Where(x => Wild(x, map, c)).OrderBy(x => x.Position.DistanceToSquared(c)).FirstOrDefault();
        }

        private static PawnKindDef Sanguine => DefDatabase<PawnKindDef>.GetNamedSilentFail("pphhyy_SanguineDeer");

        private static Pawn Shot(Map map, Pawn not) =>
            StorytellerEvents.Adults(map).Where(p => p != not && !p.WorkTagIsDisabled(WorkTags.Violent)).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0).FirstOrDefault();

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            int hour = GenLocalDate.HourOfDay(map);
            return !MapFacts.ThreatOn(map) && hour >= Round2EventsMath.WrongFrom && hour <= Round2EventsMath.WrongTo && (Beast(map) != null || Sanguine != null)
                   && StorytellerEvents.Adults(map).Any(p => p.Awake());
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            Pawn x = null;
            if (Sanguine != null && (Rand.Chance(Round2EventsMath.SanguineChance) || Beast(map) == null)
                && CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && !c.Fogged(map) && c.DistanceTo(StorytellerEvents.Center(map)) <= 90f, map, CellFinder.EdgeRoadChance_Animal, out var edge))
            {
                x = PawnGenerator.GeneratePawn(Sanguine);
                GenSpawn.Spawn(x, edge, map);
                var near = Round2Map.Near(map, StorytellerEvents.Center(map) + (edge - StorytellerEvents.Center(map)) * 1 / 2, 8f, x);
                if (near.IsValid) Round2Map.GoTo(x, near);
                ctx.things.Add(x);
                ctx.spot = near;
            }
            x ??= Beast(map);
            if (x == null) return false;
            ctx.stranger = x;
            if (!ctx.spot.IsValid) ctx.spot = x.Position;
            ctx.a = StorytellerEvents.Adults(map).Where(p => p.Awake()).OrderBy(p => p.Position.DistanceToSquared(x.Position)).FirstOrDefault();
            if (ctx.a == null) return false;
            ctx.b = Shot(map, null) ?? ctx.a;
            ctx.placeName = StorytellerEvents.Direction(map, ctx.spot);
            return true;
        }

        protected override void Abort(HappeningContext ctx)
        {
            foreach (var t in ctx.things.OfType<Pawn>().Where(p => p.Spawned)) t.Destroy(DestroyMode.Vanish);
            ctx.stranger = null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var k = Round2Map.Keep(TimedKind.WrongBeast, ctx, null, ctx.stranger);
            if (k == null) return false;
            // Later that day it goes mad, if nothing is done.
            k.nights = Find.TickManager.TicksGame + Mathf.RoundToInt(Rand.Range(0.5f, 1f) * Round2EventsMath.WrongHours * GenDate.TicksPerHour);
            k.until = Find.TickManager.TicksGame + Mathf.RoundToInt((Round2EventsMath.WrongHours + 2f) * GenDate.TicksPerHour);
            MapDangers.Say(k, ctx.a, "warn");
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.stranger?.kindDef == Sanguine && Sanguine != null) tags.Add("sanguine");
            if (ctx.b == null || ctx.b == ctx.a) tags.Add("noshot");
            Lands.AddTags(ctx.map, tags);
        }
    }

    /// <summary>
    /// <c>pen</c>: the colony's animals brought into the pens (the answer's work party) and the children kept in, near the
    /// middle of the colony, until the animal has wandered off the land; <c>let</c>: left be. Hunted (MarkBeast) it is the
    /// hunters'; missed, or let be, later that day it goes mad as the game's own mad animal does (within the budget).
    /// </summary>
    public class DilemmaEffect_WrongBeast : DilemmaEffect
    {
        public string mode = "let";

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var x = ctx?.stranger;
            if (x == null || ctx.map == null) return;
            if (mode == "pen")
            {
                sheet.Play("CC_R2_Impact_KeepIn".Translate(x.LabelShort).Resolve(), "CC_R2_Impact_KeepInBrief".Translate().Resolve());
                return;
            }
            if (Harm.On && Harm.SystemOn(HarmKinds.TooClose.system))
                sheet.Add(ImpactKind.Health, -1, ("CC_R2_Impact_Mad_" + mode).Translate(x.LabelShort).Resolve(), "CC_R2_Impact_MadBrief".Translate().Resolve(), "r2mad");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var k = Round2Map.Of(TimedKind.WrongBeast, ctx);
            if (k == null) return;
            k.start = Find.TickManager.TicksGame;
            k.count = mode == "hunt" ? 1 : mode == "pen" ? 2 : 3;
        }
    }

    /// <summary>The animal acting wrong (TimedKind.WrongBeast): 0 waiting, 1 hunted, 2 the herd in and the children kept in, 3 let be.</summary>
    public static class WrongBeast
    {
        public static bool Step(TimedThing k, int now)
        {
            var x = k.animal;
            var map = k.map;
            if (x == null || x.Dead || !x.Spawned || x.Map != map || x.Downed) return true;
            if (x.InMentalState || x.Faction != null) return true;
            if (k.count == 1 && map.designationManager.DesignationOn(x) == null && now < k.nights) k.count = 3;
            if (now >= k.nights && k.count != 2)
            {
                Mad(k, x);
                return true;
            }
            if (k.count == 2)
            {
                // The children kept near the middle of the colony while it's about.
                var center = StorytellerEvents.Center(map);
                foreach (var c in map.mapPawns.FreeColonistsSpawned.Where(p => p.DevelopmentalStage.Child() && !p.Downed && !p.Drafted && !p.InMentalState && p.Awake()))
                    if (c.Position.DistanceTo(center) > 15f && !(c.CurJobDef == JobDefOf.Goto && c.CurJob.targetA.Cell.DistanceTo(center) <= 12f))
                        Round2Map.GoTo(c, Round2Map.Near(map, center, 10f, c));
                if ((now - k.start) / (float)GenDate.TicksPerHour >= Round2EventsMath.WrongLeavesHours)
                {
                    Round2Map.Leave(x);
                    Round2Map.Told(map, "CC_R2_Wrong_Gone".Translate(x.LabelShort.Named("X")).Resolve(), new LookTargets(x));
                    return true;
                }
            }
            // Walking in circles, snapping at nothing.
            if (x.CurJobDef != JobDefOf.Goto)
            {
                float angle = (k.done++ % 8) * 45f;
                var c = k.cell + (Vector3Utility.FromAngleFlat(angle) * 2.5f).ToIntVec3();
                if (c.InBounds(map) && c.Standable(map)) Round2Map.GoTo(x, c);
            }
            if (Rand.Chance(0.3f)) FleckMaker.ThrowDustPuff(x.Position, map, 0.5f);
            return now > k.until;
        }

        /// <summary>Later that day: the game's own mad animal, within the shared budget; else it wanders off the land.</summary>
        private static void Mad(TimedThing k, Pawn x)
        {
            var map = k.map;
            if (Harm.Roll(HarmKinds.TooClose, Round2EventsMath.MadChance)
                && x.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Manhunter, null, true, true))
            {
                // A hurt that comes the game's own way: counted in the shared budget, as a beast that turns on someone is.
                HarmBook.Instance?.Came(HarmKinds.TooClose, Find.TickManager.TicksGame);
                Tell.Message(NewsKind.Colony, Importance.Important, map, "CC_R2_Wrong_Mad".Translate(x.LabelShort.Named("X")).Resolve(), new LookTargets(x), MessageTypeDefOf.ThreatSmall);
                return;
            }
            Round2Map.Leave(x);
            Round2Map.Told(map, "CC_R2_Wrong_Gone".Translate(x.LabelShort.Named("X")).Resolve(), new LookTargets(x));
        }

        public static void End(TimedThing k)
        {
        }
    }

    // ===================================================================================================
    // D21. The one who hid
    // ===================================================================================================

    /// <summary>
    /// A day or so after a raid, one of the raiders (X) is found hiding at the far side of the land, hungry, in cover,
    /// their weapon gone: left behind by their people ({FACTION}). A is a hunter or woodcutter of the colony who found them,
    /// B the one who would go with them. While here they are nobody's (left by their own), so the colony can take them in,
    /// feed them or drive them off.
    /// </summary>
    public class HappeningWorker_OneWhoHid : IncidentWorker_Happening
    {
        public static Faction Raiders(Map map)
        {
            var mem = FactionMemory.Instance;
            if (mem == null) return null;
            int now = Find.TickManager.TicksGame;
            Faction best = null;
            int bestTick = -1;
            foreach (var d in mem.deeds)
            {
                if (d?.faction == null || d.kind != "raided" || d.tick <= bestTick) continue;
                if (!Round2EventsMath.RaidFits((now - d.tick) / (float)GenDate.TicksPerDay)) continue;
                if (d.faction.defeated || d.faction.IsPlayer || !(d.faction.def?.humanlikeFaction ?? false)) continue;
                best = d.faction;
                bestTick = d.tick;
            }
            return best != null && !Truces.Present(best, map) ? best : null;
        }

        /// <summary>Cover far out: beside a tree or a rock, out of the colony's home ground, where the colony can see.</summary>
        private static bool Cover(Map map, out IntVec3 cell)
        {
            var center = StorytellerEvents.Center(map);
            return CellFinderLoose.TryGetRandomCellWith(c => c.Standable(map) && !c.Fogged(map) && !map.areaManager.Home[c] && c.DistanceTo(center) >= Round2EventsMath.HidFar
                                                             && map.reachability.CanReach(center, c, PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors))
                                                             && GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)).Any(n => n.InBounds(map) && (n.GetPlant(map)?.def.plant.IsTree == true || StorytellerEvents.NaturalRock(n, map))),
                map, 2000, out cell);
        }

        private static Pawn Finder(Map map) =>
            StorytellerEvents.Adults(map).Where(p => p.CurJobDef == JobDefOf.Hunt || p.CurJobDef == JobDefOf.CutPlant).FirstOrDefault()
            ?? StorytellerEvents.Adults(map).Where(p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Hunting) || !p.WorkTypeIsDisabled(WorkTypeDefOf.PlantCutting)).RandomElementWithFallback();

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            !MapFacts.ThreatOn(map) && Raiders(map) != null && Finder(map) != null && StorytellerEvents.Adults(map).Count() >= 2 && Cover(map, out _);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            ctx.faction = Raiders(map);
            if (ctx.faction == null || !Cover(map, out var cell)) return false;
            ctx.a = Finder(map);
            if (ctx.a == null) return false;
            ctx.b = StorytellerEvents.Adults(map).Where(p => p != ctx.a && !p.WorkTagIsDisabled(WorkTags.Violent))
                .OrderByDescending(p => Math.Max(p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0, p.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0)).FirstOrDefault();
            if (ctx.b == null) return false;
            Pawn x;
            try
            {
                x = Arrivals.Make(Arrivals.PersonKind(ctx.faction), ctx.faction, new FloatRange(18f, 45f));
            }
            catch
            {
                return false;
            }
            if (x == null) return false;
            ctx.stranger = x;
            ctx.spot = cell;
            ctx.placeName = StorytellerEvents.Direction(map, cell);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var x = ctx.stranger;
            var map = ctx.map;
            if (x == null) return false;
            // Left behind: nobody's while here, their weapon gone, hungry.
            if (x.equipment?.Primary is ThingWithComps w && w.def.IsRangedWeapon) x.equipment.DestroyEquipment(w);
            x.SetFaction(null);
            if (x.needs?.food != null) x.needs.food.CurLevelPercentage = 0.12f;
            GenSpawn.Spawn(x, ctx.spot, map);
            if (!x.Spawned) return false;
            Round2Map.Hold(x, 4f);
            var k = Round2Map.Keep(TimedKind.Hidden, ctx, x, null, Round2EventsMath.HidSlipHours);
            if (k == null) return false;
            k.until += Mathf.RoundToInt(Round2EventsMath.HidSlipHours * GenDate.TicksPerHour);
            MapDangers.Say(k, ctx.a, "warn");
            return true;
        }

        protected override void Abort(HappeningContext ctx)
        {
            if (ctx.stranger != null && !ctx.stranger.Spawned) Arrivals.Discard(ctx.stranger);
            ctx.stranger = null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            // Their people hold gods (they cry them in a raid): the finder heard them praying, under their breath.
            if (Gods.On && ctx.faction != null && Gods.Of(ctx.faction).Any(g => g.kind != GodKind.Dead)) tags.Add("prayed");
            Lands.AddTags(ctx.map, tags);
        }
    }

    /// <summary>
    /// <c>take</c>: two go to bring them in as a prisoner (the game's own arrest by A, B beside them; whether they give
    /// themselves up or fight is the game's own); <c>feed</c>: food carried out to them (the answer's errand) and they walk
    /// home after; <c>drive</c>: B walks out with a weapon raised and shouts, and they run off the land.
    /// </summary>
    public class DilemmaEffect_Hidden : DilemmaEffect
    {
        public string mode = "drive";

        public override bool ChanceEach => false;

        private static bool There(Pawn x, Map map) => x != null && !x.Dead && x.Spawned && x.Map == map && !x.Downed && x.Faction == null && !x.IsPrisoner;

        /// <summary>The one who would make the arrest is here and up to it (not downed, in a break or gone from the map).</summary>
        private static bool Arrester(Pawn a, Map map) => a != null && !a.Dead && a.Spawned && a.Map == map && !a.Downed && !a.InMentalState;

        public override string Unavailable(DilemmaContext ctx)
        {
            var x = ctx?.stranger;
            if (!There(x, ctx?.map)) return "CC_R2_Hid_Gone".Translate().Resolve();
            if (mode == "take" && !Arrester(ctx.a, ctx.map)) return "CC_WeaponPick_Busy".Translate((ctx.a?.LabelShort ?? "").Named("PAWN")).Resolve();
            if (mode == "take" && RestUtility.FindBedFor(x, ctx.a, false, false, GuestStatus.Prisoner) == null) return "CC_R2_Hid_NoBed".Translate().Resolve();
            return null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var x = ctx?.stranger;
            if (!There(x, ctx?.map)) return;
            sheet.Play(("CC_R2_Impact_Hid_" + mode).Translate(x.LabelShort, (ctx.a ?? ctx.b)?.LabelShort ?? "", (ctx.b ?? ctx.a)?.LabelShort ?? "").Resolve(),
                ("CC_R2_Impact_Hid_" + mode + "Brief").Translate().Resolve());
            if (mode == "take") sheet.Add(ImpactKind.Health, -1, "CC_R2_Impact_HidFight".Translate(x.LabelShort).Resolve(), "CC_R2_Impact_HidFightBrief".Translate().Resolve(), "r2hidfight");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var x = ctx?.stranger;
            var map = ctx?.map;
            if (!There(x, map)) return;
            var k = Round2Map.Of(TimedKind.Hidden, ctx);
            if (k == null) return;
            int now = Find.TickManager.TicksGame;
            k.start = now;
            k.text = mode;
            k.pawns.Clear();
            switch (mode)
            {
                case "take":
                    var bed = !Arrester(ctx.a, map) ? null : RestUtility.FindBedFor(x, ctx.a, false, false, GuestStatus.Prisoner);
                    if (bed == null) return;
                    k.pawns.Add(ctx.a);
                    if (ctx.b != null && ctx.b != ctx.a) k.pawns.Add(ctx.b);
                    var job = JobMaker.MakeJob(JobDefOf.Arrest, x, bed);
                    job.count = 1;
                    ctx.a.jobs.TryTakeOrderedJob(job, JobTag.Misc, false);
                    k.count = 1;
                    k.until = now + GenDate.TicksPerDay / 2;
                    break;
                case "feed":
                    k.count = 2;
                    k.until = now + GenDate.TicksPerDay / 2;
                    break;
                default:
                    if (ctx.b != null) k.pawns.Add(ctx.b);
                    k.count = 3;
                    k.until = now + Mathf.RoundToInt(3f * GenDate.TicksPerHour);
                    break;
            }
        }
    }

    /// <summary>The one who hid (TimedKind.Hidden): 0 waiting in cover, 1 being taken in, 2 fed and then gone home, 3 driven off.</summary>
    public static class Hidden
    {
        public static bool Step(TimedThing k, int now)
        {
            var x = k.pawn;
            var map = k.map;
            if (x == null || x.Dead || !x.Spawned || x.Map != map || x.Downed || x.IsPrisoner || x.InMentalState || x.Faction != null) return true;
            switch (k.count)
            {
                case 0:
                    Round2Map.Hold(x, 2f);
                    if (now > k.until)
                    {
                        Round2Map.Leave(x);
                        return true;
                    }
                    return false;
                case 1:
                {
                    // The one making the arrest, and the one beside them.
                    var a = k.pawns.FirstOrDefault();
                    if (a == null || a.Dead || a.Downed || (a.CurJobDef != JobDefOf.Arrest && now - k.start > GenDate.TicksPerHour)) return true;
                    var b = k.pawns.Skip(1).FirstOrDefault();
                    if (b != null && !b.Downed && !b.Drafted && !b.InMentalState && b.Position.DistanceTo(x.Position) > 3f && !(b.CurJobDef == JobDefOf.Goto && b.CurJob.targetA.Cell.DistanceTo(x.Position) <= 3f))
                        Round2Map.GoTo(b, Round2Map.Near(map, x.Position, 2.5f, b), LocomotionUrgency.Jog);
                    return now > k.until;
                }
                case 2:
                {
                    // Fed by the errand: they eat what was brought (from their hands or where it was set down), then set off home.
                    if (k.on)
                    {
                        if (x.CurJobDef == JobDefOf.Ingest) return false;
                        k.count = 4;
                        return false;
                    }
                    var food = x.inventory?.innerContainer.FirstOrDefault(t => t.def.IsNutritionGivingIngestible)
                               ?? GenRadial.RadialDistinctThingsAround(x.Position, map, 2f, true).FirstOrDefault(t => t.def.IsNutritionGivingIngestible && t.def.category == ThingCategory.Item);
                    if (food != null)
                    {
                        k.on = true;
                        var eat = JobMaker.MakeJob(JobDefOf.Ingest, food);
                        eat.count = 1;
                        x.jobs.StartJob(eat, JobCondition.InterruptForced, null, false, true);
                        return false;
                    }
                    if (now - k.start > GenDate.TicksPerHour * 8)
                    {
                        k.count = 4;
                        return false;
                    }
                    Round2Map.Hold(x, 1f);
                    return now > k.until;
                }
                case 3:
                {
                    var b = k.pawns.FirstOrDefault();
                    if (b == null || b.Downed || b.Drafted || b.InMentalState || b.Position.DistanceTo(x.Position) <= 14f || now > k.until)
                    {
                        if (b != null && b.Spawned) MapDangers.Say(k, b, "drive");
                        MapDangers.Say(k, x, "run");
                        Round2Map.Leave(x, LocomotionUrgency.Sprint);
                        return true;
                    }
                    var to = Round2Map.Near(map, x.Position + (b.Position - x.Position) * 1 / 2, 3f, b);
                    if (!(b.CurJobDef == JobDefOf.Goto)) Round2Map.GoTo(b, to.IsValid ? to : x.Position, LocomotionUrgency.Jog);
                    return false;
                }
                default:
                    // On their way home.
                    MapDangers.Say(k, x, "thanks");
                    Round2Map.Leave(x);
                    return true;
            }
        }

        /// <summary>
        /// Over with them still about, nobody's and free (the arrest given up, the time out, the cap): they slip away off the land
        /// rather than stay on it for good.
        /// </summary>
        public static void End(TimedThing k)
        {
            var x = k.pawn;
            if (x == null || x.Dead || !x.Spawned || x.Map != k.map || x.Downed || x.IsPrisoner || x.InMentalState || x.Faction != null || x.GetLord() != null) return;
            if (x.CurJob != null && x.CurJob.exitMapOnArrival) return;
            Round2Map.Leave(x);
        }
    }

    // ===================================================================================================
    // D22. Stuck in the bog
    // ===================================================================================================

    /// <summary>
    /// After rain, one of the colony's animals (or a wild grazer) has sunk in the marsh or mud at the water's edge and can't
    /// get out (X; a condition of its own on its health tab, and it doesn't move). A is the colony's best with animals (or
    /// its master), B one more to pull.
    /// </summary>
    public class HappeningWorker_StuckInBog : IncidentWorker_Happening
    {
        public static HediffDef Stuck => DefDatabase<HediffDef>.GetNamedSilentFail("Rimfolk_R2_StuckInMud");

        public static bool Bog(IntVec3 c, Map map)
        {
            if (!c.InBounds(map)) return false;
            var t = c.GetTerrain(map);
            if (t == null || t.IsWater || (t.defName.IndexOf("Marsh", StringComparison.Ordinal) < 0 && t.defName.IndexOf("Mud", StringComparison.Ordinal) < 0)) return false;
            foreach (var n in GenRadial.RadialCellsAround(c, 2f, false))
                if (n.InBounds(map) && n.GetTerrain(map).IsWater)
                    return true;
            return false;
        }

        private static bool Fits(Pawn x, Map map) =>
            x.Spawned && !x.Dead && !x.Downed && !x.InMentalState && x.RaceProps.Animal && x.RaceProps.baseBodySize >= 0.5f && !x.Position.Fogged(map) && Bog(x.Position, map)
            && (x.Faction == Faction.OfPlayer || x.Faction == null && x.RaceProps.Eats(FoodTypeFlags.Plant) && !x.RaceProps.predator);

        public static Pawn Beast(Map map) =>
            map.mapPawns.SpawnedColonyAnimals.FirstOrDefault(x => Fits(x, map)) ?? map.mapPawns.AllPawnsSpawned.FirstOrDefault(x => x.Faction == null && Fits(x, map));

        protected override bool CanHappen(Map map, HappeningExtension ext) => !MapFacts.ThreatOn(map) && Stuck != null && Beast(map) != null && StorytellerEvents.Adults(map).Count() >= 2;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var x = Beast(ctx.map);
            if (x == null) return false;
            ctx.stranger = x;
            var adults = StorytellerEvents.Adults(ctx.map).ToList();
            ctx.a = x.playerSettings?.Master is Pawn m && adults.Contains(m) ? m : VarietyThree.Best(adults, SkillDefOf.Animals, WorkTypeDefOf.Handling) ?? adults.FirstOrDefault();
            if (ctx.a == null) return false;
            ctx.b = adults.Where(p => p != ctx.a).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0).FirstOrDefault();
            if (ctx.b == null) return false;
            ctx.spot = x.Position;
            ctx.placeName = StorytellerEvents.Direction(ctx.map, x.Position);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var x = ctx.stranger;
            if (x == null || Stuck == null) return false;
            x.health.AddHediff(HediffMaker.MakeHediff(Stuck, x));
            Round2Map.Hold(x, 2f);
            FleckMaker.ThrowDustPuff(x.Position, ctx.map, 0.8f);
            var k = Round2Map.Keep(TimedKind.Bog, ctx, null, x);
            if (k == null) return false;
            k.until = Find.TickManager.TicksGame + Mathf.RoundToInt(Round2EventsMath.BogFreeDays * GenDate.TicksPerDay);
            MapDangers.Say(k, ctx.a, "warn");
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            tags.Add(ctx.stranger?.Faction == Faction.OfPlayer ? "ours" : "wild");
            Lands.AddTags(ctx.map, tags);
        }
    }

    /// <summary><c>rope</c>: two pull it out with a rope (an hour, muddy, wanting a wash after); <c>boards</c>: boards laid out to it (wood used, quicker and cleaner); <c>leave</c>: a wild one left to free itself.</summary>
    public class DilemmaEffect_Bog : DilemmaEffect
    {
        public string mode = "rope";

        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx)
        {
            var x = ctx?.stranger;
            if (x == null || x.Dead || !x.Spawned || x.Map != ctx.map) return "CC_DlmE_BeastGone".Translate(x?.LabelShort ?? "").Resolve();
            if (mode == "leave" && x.Faction == Faction.OfPlayer) return "CC_R2_Bog_Ours".Translate(x.LabelShort).Resolve();
            if (mode == "boards" && PlayCarry.Have(ctx.map, "wood", null) < Round2EventsMath.BoardsWood) return "CC_Dilemma_NotEnough".Translate(Round2EventsMath.BoardsWood, ThingDefOf.WoodLog.label).ToString();
            return null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var x = ctx?.stranger;
            if (x == null || !x.Spawned) return;
            switch (mode)
            {
                case "rope":
                    sheet.Play("CC_R2_Impact_Rope".Translate(ctx.a?.LabelShort ?? "", ctx.b?.LabelShort ?? "", x.LabelShort).Resolve(), "CC_R2_Impact_RopeBrief".Translate().Resolve());
                    break;
                case "boards":
                    sheet.Goods(-1, ThingDefOf.WoodLog, Round2EventsMath.BoardsWood);
                    sheet.Play("CC_R2_Impact_Boards".Translate(ctx.a?.LabelShort ?? "", x.LabelShort).Resolve(), "CC_R2_Impact_BoardsBrief".Translate().Resolve());
                    break;
                default:
                    sheet.Play("CC_R2_Impact_BogLeave".Translate(x.LabelShort).Resolve(), "CC_R2_Impact_BogLeaveBrief".Translate().Resolve());
                    break;
            }
        }

        public override void Apply(DilemmaContext ctx)
        {
            var k = Round2Map.Of(TimedKind.Bog, ctx);
            if (k == null) return;
            k.pawns.Clear();
            k.done = 0;
            switch (mode)
            {
                case "rope":
                    if (ctx.a != null) k.pawns.Add(ctx.a);
                    if (ctx.b != null && ctx.b != ctx.a) k.pawns.Add(ctx.b);
                    k.count = 1;
                    break;
                case "boards":
                    if (PlayCarry.Take(ctx.map, "wood", null, Round2EventsMath.BoardsWood) < Round2EventsMath.BoardsWood) return;
                    if (ctx.a != null) k.pawns.Add(ctx.a);
                    k.count = 2;
                    break;
                default:
                    k.count = 3;
                    break;
            }
        }
    }

    /// <summary>The animal in the bog (TimedKind.Bog): 0 waiting, 1 the rope, 2 the boards, 3 left; it works itself free when its time is up.</summary>
    public static class Bog
    {
        private static Hediff StuckOn(Pawn x) => HappeningWorker_StuckInBog.Stuck is HediffDef d ? x?.health?.hediffSet?.GetFirstHediffOfDef(d) : null;

        public static bool Step(TimedThing k, int now)
        {
            var x = k.animal;
            var map = k.map;
            if (x == null || x.Dead || !x.Spawned || x.Map != map || StuckOn(x) == null) return true;
            if (now > k.until)
            {
                Free(k, x, "CC_R2_Bog_Self");
                return true;
            }
            Round2Map.Hold(x, 2f);
            if (k.count != 1 && k.count != 2) return false;
            float need = k.count == 1 ? Round2EventsMath.RopeHours : Round2EventsMath.BoardsHours;
            var at = k.pawns.Where(p => p != null && !p.Dead && p.Spawned && p.Map == map && !p.Downed && !p.Drafted && !p.InMentalState).ToList();
            if (at.Count == 0) return false;
            bool all = true;
            foreach (var p in at)
            {
                if (p.Position.DistanceTo(x.Position) <= 1.5f)
                {
                    Round2Map.Hold(p, 1f);
                    continue;
                }
                all = false;
                if (PlayOuts.NeedsPress(p)) continue;
                var c = MapDangersCells.Open(x.Position, map, p.Position);
                if (c.IsValid) Round2Map.GoTo(p, c, LocomotionUrgency.Jog);
            }
            if (!all) return false;
            k.done += Sched.RareTicks;
            if (Rand.Chance(0.25f)) FleckMaker.ThrowDustPuff(x.Position, map, 0.7f);
            if (Rand.Chance(0.08f)) MapDangers.Say(k, at.RandomElement(), k.count == 1 ? "pull" : "boards");
            if (k.done < need * GenDate.TicksPerHour) return false;
            if (k.count == 2)
            {
                var plank = DefDatabase<TerrainDef>.GetNamedSilentFail("WoodPlankFloor");
                if (plank != null)
                    foreach (var c in new[] { x.Position }.Concat(at.Select(p => p.Position)).Distinct())
                        if (HappeningWorker_StuckInBog.Bog(c, map))
                            map.terrainGrid.SetTerrain(c, plank);
            }
            if (k.count == 1)
                foreach (var p in at)
                {
                    // Muddy to the knees, and wanting a wash (Dubs Bad Hygiene's, where it is).
                    FilthMaker.TryMakeFilth(p.Position, map, ThingDefOf.Filth_Dirt, 2);
                    var hygiene = p.needs?.AllNeeds.FirstOrDefault(n => n.def.defName == "Hygiene");
                    if (hygiene != null) hygiene.CurLevel = Mathf.Max(0f, hygiene.CurLevel - 0.35f);
                    Round2Map.Give(p, "Rimfolk_R2_PulledOut");
                }
            Free(k, x, k.count == 1 ? "CC_R2_Bog_Rope" : "CC_R2_Bog_Boards");
            k.on = true;
            return true;
        }

        private static void Free(TimedThing k, Pawn x, string key)
        {
            var h = StuckOn(x);
            if (h != null) x.health.RemoveHediff(h);
            if (x.CurJobDef == JobDefOf.Wait_MaintainPosture) x.jobs.EndCurrentJob(JobCondition.InterruptForced);
            Round2Map.Told(k.map, key.Translate(x.LabelShort.Named("X")).Resolve(), new LookTargets(x));
        }

        public static void End(TimedThing k)
        {
            // Over without being freed (the cap, a load): it isn't left stuck.
            var x = k.animal;
            var h = StuckOn(x);
            if (h != null && !x.Dead) x.health.RemoveHediff(h);
        }
    }

    // ===================================================================================================
    // D23. Bad air in the deep
    // ===================================================================================================

    /// <summary>
    /// Deep under thick rock (no open sky near), a newly opened face lets out a pocket of bad air: the game's tox gas over a
    /// few cells (Biotech). The miner (A) coughs and comes out; the digging there stops until the council says. B is the
    /// colony's best builder. Those who hold the Smoulder say the deep breathed (their word, the god unnamed).
    /// </summary>
    public class HappeningWorker_BadAir : HappeningWorker_Danger
    {
        private static bool Deep(Map map, IntVec3 c)
        {
            if (!(map.roofGrid.RoofAt(c)?.isThickRoof ?? false)) return false;
            foreach (var n in GenRadial.RadialCellsAround(c, Round2EventsMath.DeepFromSky, true))
                if (n.InBounds(map) && !map.roofGrid.Roofed(n))
                    return false;
            return true;
        }

        public static Pawn Miner(Map map, out Thing face)
        {
            face = null;
            foreach (var p in Colonists(map))
            {
                if (!StorytellerEvents.Able(p) || p.CurJobDef != JobDefOf.Mine || !(p.CurJob.targetA.Thing is Thing t) || !t.Spawned) continue;
                if (!Deep(map, t.Position)) continue;
                face = t;
                return p;
            }
            return null;
        }

        protected override bool Found(Map map) => ModsConfig.BiotechActive && Miner(map, out _) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = Miner(ctx.map, out var face);
            if (ctx.a == null) return false;
            var adults = StorytellerEvents.Adults(ctx.map).ToList();
            ctx.b = VarietyThree.Best(adults, SkillDefOf.Construction, WorkTypeDefOf.Construction, ctx.a) ?? VarietyThree.Other(adults, ctx.a, SkillDefOf.Construction);
            if (ctx.b == null) return false;
            ctx.spot = face.Position;
            ctx.thing = face.def;
            ctx.placeName = StorytellerEvents.Direction(ctx.map, face.Position);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var dm = map.designationManager;
            var stopped = new List<IntVec3>();
            foreach (var c in GenRadial.RadialCellsAround(ctx.spot, 3.9f, true).Where(c => c.InBounds(map)))
            {
                var d = dm.DesignationAt(c, DesignationDefOf.Mine) ?? dm.DesignationAt(c, DesignationDefOf.MineVein);
                if (d == null) continue;
                stopped.Add(c);
                dm.RemoveDesignation(d);
            }
            var open = MapDangersCells.Open(ctx.spot, map, ctx.a.Position);
            if (!open.IsValid) return false;
            int gas = Round2EventsMath.GasFor(Harm.ScaleFor(HarmKinds.Dangers));
            foreach (var c in GenRadial.RadialCellsAround(open, 1.5f, true).Where(c => c.InBounds(map) && c.Standable(map)).Take(Round2EventsMath.GasCells))
                GasUtility.AddGas(c, map, GasType.ToxGas, gas);
            ctx.a.jobs.EndCurrentJob(JobCondition.InterruptForced);
            var back = Round2Map.Near(map, ctx.a.Position, 9f, ctx.a);
            foreach (var c in GenRadial.RadialCellsAround(ctx.a.Position, 9f, true).Where(c => c.InBounds(map) && c.Standable(map) && c.DistanceTo(open) >= 6f && ctx.a.CanReach(c, PathEndMode.OnCell, Danger.Some)))
            {
                back = c;
                break;
            }
            if (back.IsValid) ctx.a.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, back), JobTag.Misc, false);
            var k = MapDangers.Sign(TimedKind.BadAir, ctx, null, stopped);
            if (k == null) return false;
            k.nights = open.x * 1000 + open.z;
            MapDangers.Say(k, ctx.a, "warn");
            // One who holds the Smoulder, near enough to see: the deep breathed (their word).
            var believer = Round2Map.Holders(map, "Smoulder").Where(p => p != ctx.a && p.Position.DistanceTo(ctx.spot) <= 25f && p.Awake()).FirstOrDefault();
            if (believer != null) MapDangers.Say(k, believer, "believer");
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            if (ctx.map != null && Round2Map.Holders(ctx.map, "Smoulder").Any()) tags.Add("smoulder");
        }
    }

    /// <summary>
    /// <c>shaft</c>: a shaft opened to the air (the rock between the face and the nearest open sky marked for mining, the
    /// digging at the face put back once the air clears); <c>turns</c>: the digging put back now, and nobody stays in the bad
    /// air longer than a short turn; <c>wall</c>: a wall's blueprint across the way in, the face left.
    /// </summary>
    public class DilemmaEffect_BadAir : DilemmaEffect
    {
        public string mode = "wall";

        public override bool ChanceEach => false;

        public static IntVec3 Opening(TimedThing k) => k == null || k.nights <= 0 ? IntVec3.Invalid : new IntVec3(k.nights / 1000, 0, k.nights % 1000);

        /// <summary>The rock on the straight line from the opening toward the nearest open sky, when it's within reach.</summary>
        public static List<IntVec3> Shaft(Map map, IntVec3 from)
        {
            var list = new List<IntVec3>();
            if (map == null || !from.IsValid) return list;
            IntVec3 sky = IntVec3.Invalid;
            foreach (var c in GenRadial.RadialCellsAround(from, Mathf.Min(Round2EventsMath.ShaftMax + 1, GenRadial.MaxRadialPatternRadius - 1f), false))
                if (c.InBounds(map) && !map.roofGrid.Roofed(c))
                {
                    sky = c;
                    break;
                }
            if (!sky.IsValid) return list;
            foreach (var c in GenSight.PointsOnLineOfSight(from, sky))
            {
                if (!c.InBounds(map)) continue;
                var e = c.GetEdifice(map);
                if (e != null && e.def.mineable) list.Add(c);
            }
            return list.Count <= Round2EventsMath.ShaftMax ? list : new List<IntVec3>();
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            if (!(ctx is HappeningContext h) || h.map == null) return null;
            if (mode == "shaft" && Shaft(h.map, Opening(Round2Map.Of(TimedKind.BadAir, ctx))).Count == 0) return "CC_R2_Air_NoShaft".Translate(Round2EventsMath.ShaftMax).Resolve();
            return null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!(ctx is HappeningContext h) || h.map == null) return;
            sheet.Play(("CC_R2_Impact_Air_" + mode).Translate((ctx.b ?? ctx.a)?.LabelShort ?? "").Resolve(), ("CC_R2_Impact_Air_" + mode + "Brief").Translate().Resolve());
            if (mode == "turns" && MapDangers.On) sheet.Add(ImpactKind.Health, -1, "CC_R2_Impact_AirRisk".Translate().Resolve(), "CC_R2_Impact_AirRiskBrief".Translate().Resolve(), "r2air");
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (!(ctx is HappeningContext h) || h.map == null) return;
            var k = Round2Map.Of(TimedKind.BadAir, ctx);
            if (k == null) return;
            var map = h.map;
            var dm = map.designationManager;
            var open = Opening(k);
            k.start = Find.TickManager.TicksGame;
            k.until = k.start + GenDate.TicksPerDay * 2;
            switch (mode)
            {
                case "shaft":
                    foreach (var c in Shaft(map, open))
                        if (dm.DesignationAt(c, DesignationDefOf.Mine) == null)
                            dm.AddDesignation(new Designation(c, DesignationDefOf.Mine));
                    k.count = 1;
                    break;
                case "turns":
                    BadAir.PutBack(k);
                    k.count = 2;
                    break;
                default:
                    if (open.IsValid) Round2Map.WallAt(map, open);
                    k.count = 3;
                    break;
            }
        }
    }

    /// <summary>The bad air (TimedKind.BadAir): 0 waiting, 1 the shaft, 2 short turns, 3 walled off; over when the air clears.</summary>
    public static class BadAir
    {
        private static bool Gas(Map map, IntVec3 c) => c.IsValid && c.InBounds(map) && map.gasGrid.DensityAt(c, GasType.ToxGas) > 0;

        private static bool Any(TimedThing k)
        {
            var open = DilemmaEffect_BadAir.Opening(k);
            return open.IsValid && GenRadial.RadialCellsAround(open, 2.5f, true).Any(c => Gas(k.map, c));
        }

        /// <summary>The digging at the face marked again as it was.</summary>
        public static void PutBack(TimedThing k)
        {
            var map = k.map;
            var dm = map.designationManager;
            foreach (var c in k.cells)
                if (c.InBounds(map) && c.GetEdifice(map) is Building b && b.def.mineable && dm.DesignationAt(c, DesignationDefOf.Mine) == null)
                    dm.AddDesignation(new Designation(c, DesignationDefOf.Mine));
        }

        public static bool Step(TimedThing k, int now)
        {
            var map = k.map;
            if (k.count == 0) return now > k.until;
            if (k.count == 3) return true;
            if (!Any(k))
            {
                if (k.count == 1) PutBack(k);
                Round2Map.Told(map, "CC_R2_Air_Clear".Translate().Resolve(), new LookTargets(k.cell, map));
                return true;
            }
            if (k.count == 2)
            {
                // Short turns: one in the bad air too long is sent out to breathe.
                var inGas = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => Gas(map, p.Position) && !p.Downed && !p.Drafted);
                if (inGas == null) k.done = 0;
                else
                {
                    k.done += Sched.RareTicks;
                    if (k.done >= Round2EventsMath.TurnHours * GenDate.TicksPerHour)
                    {
                        k.done = 0;
                        MapDangers.Say(k, inGas, "turn");
                        inGas.jobs.EndCurrentJob(JobCondition.InterruptForced);
                        var back = GenRadial.RadialCellsAround(inGas.Position, 9f, true).Where(c => c.InBounds(map) && c.Standable(map) && !Gas(map, c) && c.DistanceTo(inGas.Position) >= 5f)
                            .DefaultIfEmpty(IntVec3.Invalid).First();
                        if (back.IsValid) inGas.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, back), JobTag.Misc, false);
                    }
                }
            }
            return now > k.until;
        }

        public static void End(TimedThing k)
        {
        }
    }
}
