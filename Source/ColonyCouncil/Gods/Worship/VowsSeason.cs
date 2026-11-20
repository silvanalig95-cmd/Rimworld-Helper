using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Four more vows, each for a season (C5 of docs/content-update-proposals.md; the player's go of 20 November 2026), set as
    /// a life goal like the pilgrimage and the temple (Rimfolk_Worship_Goals.xml), only to the gods their words are for
    /// (WorshipMath.VowFits), and each kept on the map by the vower's own walk and broken by what anyone can see:
    /// <list type="bullet">
    /// <item>A lamp kept lit at the god's place (Pip, Selene, Helios, or the Hunger's shuttered lamps): each day the vower walks
    /// to the light nearest its place and fills it from the stores when it burns low, or stands and checks it; broken the
    /// night it goes out or is gone.</item>
    /// <item>A tree a week (Hollin): each week the vower sets a sapling in the ground by hand; broken if they fell a tree, or
    /// a week goes by with none planted.</item>
    /// <item>Plain bread (Hush and Morel): simple meals only; each day's plain meal said over their head, broken by a fine or a
    /// lavish one eaten.</item>
    /// <item>Every caller fed (Ennu and Ducat): when callers come to the gate (visitors or traders), the vower takes a meal from
    /// the stores and sets it down by one of them; broken if callers leave unfed.</item>
    /// </list>
    /// Kept when the season is out (VowDays). No harm.
    /// </summary>
    public static class VowsSeason
    {
        public const string KeepPool = "Rimfolk_Pool_Worship_VowKeep";

        public static JobDef KeepJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_VowKeep");

        private static int Now => Find.TickManager.TicksGame;

        private static int Today(Map map) => GenDate.DaysPassed;

        /// <summary>The light nearest a place of the god's at a home (a lamp, a torch, a brazier), or null.</summary>
        public static Thing LampFor(Map map, God g)
        {
            if (map == null || g == null) return null;
            foreach (var s in Shrines.Of(map, g))
            {
                var at = s.thing?.Position ?? IntVec3.Invalid;
                if (!at.IsValid) continue;
                var lamp = map.listerBuildings.allBuildingsColonist.Where(b => b.Spawned && b.TryGetComp<CompGlower>() != null && b.Position.InHorDistOf(at, WorshipMath.LampNear))
                    .OrderBy(b => b.Position.DistanceToSquared(at)).FirstOrDefault();
                if (lamp != null) return lamp;
            }
            return null;
        }

        /// <summary>The lamp is out: gone, out of fuel, or without power.</summary>
        public static bool Out(Thing lamp)
        {
            if (lamp == null || lamp.Destroyed || !lamp.Spawned) return true;
            var fuel = lamp.TryGetComp<CompRefuelable>();
            if (fuel != null && !fuel.HasFuel) return true;
            var power = lamp.TryGetComp<CompPowerTrader>();
            return power != null && !power.PowerOn;
        }

        /// <summary>A vow of the season may be set as a life goal: the vow's own gods, and for the lamp a light at its place.</summary>
        public static bool CanVow(Pawn p, VowKind k)
        {
            if (!Vows.CanVow(p, k)) return false;
            var g = Worship.GodOf(p);
            if (g == null || !WorshipMath.VowFits(k, g.Key)) return false;
            return k != VowKind.Lamp || LampFor(p.MapHeld, g) != null;
        }

        /// <summary>A vow of the season made: the lamp it keeps is noted.</summary>
        internal static void Made(VowRecord r)
        {
            if (r?.kind == VowKind.Lamp) r.thing = LampFor(r.pawn?.MapHeld, Gods.ById(r.god));
            if (r != null) r.lastDay = -1;
        }

        // ================= The hour =================

        /// <summary>A vow of the season's hour at its home (Vows.Hourly): kept, broken, or seen to.</summary>
        internal static void Hourly(VowRecord r, Map map, int now)
        {
            if (r.until > 0 && now >= r.until)
            {
                Vows.End(r, true);
                return;
            }
            var p = r.pawn;
            var g = Gods.ById(r.god);
            int hour = GenLocalDate.HourOfDay(map);
            switch (r.kind)
            {
                case VowKind.Lamp:
                    if (Out(r.thing))
                    {
                        Vows.End(r, false, "CC_Wsh_How_LampOut".Translate((r.thing?.LabelNoCount ?? "").Named("LAMP")).Resolve());
                        return;
                    }
                    if (hour >= 8 && hour < 20 && r.lastDay != Today(map) && Free(p, map)) SeeToLamp(r, p);
                    break;
                case VowKind.Trees:
                    int days = (now - r.since) / GenDate.TicksPerDay;
                    if (WorshipMath.WeekMissed(days, r.count))
                    {
                        Vows.End(r, false, "CC_Wsh_How_NoTree".Translate().Resolve());
                        return;
                    }
                    if (hour >= 8 && hour < 17 && r.count <= WorshipMath.TreeWeek(days) && r.lastDay != Today(map) && Free(p, map)) Plant(r, p, g);
                    break;
                case VowKind.Visitors:
                    Callers(r, p, map);
                    break;
            }
        }

        private static bool Free(Pawn p, Map map) => p != null && p.Spawned && p.Map == map && WorshipJobs.Free(p, false) && p.CurJobDef != KeepJob;

        /// <summary>A job of the vow's own at a target, a while there (Job.count ticks), then VowsSeason.Done.</summary>
        private static bool Keep(Pawn p, LocalTargetInfo at, int ticks)
        {
            var def = KeepJob;
            if (def == null || p == null || !at.IsValid) return false;
            var job = JobMaker.MakeJob(def, at);
            job.count = ticks;
            job.locomotionUrgency = LocomotionUrgency.Walk;
            return OwnHappenings.Job(p, job);
        }

        /// <summary>The day's look at the lamp: filled from the stores when it burns low, else a while beside it.</summary>
        private static void SeeToLamp(VowRecord r, Pawn p)
        {
            var fuel = r.thing.TryGetComp<CompRefuelable>();
            if (fuel != null && fuel.FuelPercentOfMax < 0.6f)
            {
                var refuel = RefuelWorkGiverUtility.RefuelJob(p, r.thing, true);
                if (refuel != null && OwnHappenings.Job(p, refuel))
                {
                    r.lastDay = GenDate.DaysPassed;
                    Worship.Say(p, KeepPool, Gods.ById(r.god), null, "lamp");
                    return;
                }
            }
            if (Keep(p, r.thing, 240)) r.lastDay = GenDate.DaysPassed;
        }

        /// <summary>A sapling set in the ground by hand, near the god's place (else near the colony).</summary>
        private static void Plant(VowRecord r, Pawn p, God g)
        {
            var map = p.Map;
            var near = Shrines.Of(map, g).FirstOrDefault()?.thing?.Position ?? OwnHappenings.Middle(map);
            var cell = GenRadial.RadialCellsAround(near, 25f, true).Where(c => PeoplesE.SaplingCell(map, c) && p.CanReach(c, PathEndMode.Touch, Danger.Some)).Take(30).RandomElementWithFallback(IntVec3.Invalid);
            if (!cell.IsValid || PeoplesE.SaplingFor(map, cell) == null) return;
            if (Keep(p, cell, 400)) r.lastDay = GenDate.DaysPassed;
        }

        /// <summary>Callers at the gate: each party fed once; a party that left unfed breaks the vow.</summary>
        private static void Callers(VowRecord r, Pawn p, Map map)
        {
            var parties = map.mapPawns.AllPawnsSpawned.Where(x => x.RaceProps?.Humanlike == true && x.Faction != null && !x.Faction.IsPlayer && !x.HostileTo(Faction.OfPlayer)
                                                                   && x.GetLord() != null && !x.IsPrisoner)
                .GroupBy(x => x.GetLord()).ToList();
            // The party waited on before: gone without being fed, the vow is broken.
            if (!r.pending.NullOrEmpty() && r.pending.StartsWith("wait|"))
            {
                int id = int.TryParse(r.pending.Substring(5), out int v) ? v : -1;
                if (!parties.Any(pg => pg.Key.loadID == id))
                {
                    Vows.End(r, false, "CC_Wsh_How_Unfed".Translate().Resolve());
                    return;
                }
            }
            var unfed = parties.FirstOrDefault(pg => r.pending != "fed|" + pg.Key.loadID);
            if (unfed == null) return;
            if (r.pending.NullOrEmpty() || !r.pending.StartsWith("wait|")) r.pending = "wait|" + unfed.Key.loadID;
            if (!Free(p, map)) return;
            var guest = unfed.OrderBy(x => x.Position.DistanceToSquared(p.Position)).First();
            var meal = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(t => t.Spawned && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer) && t.def.IsNutritionGivingIngestible && t.def.ingestible.IsMeal && p.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some))
                .OrderBy(t => t.def.ingestible.preferability).FirstOrDefault();
            if (meal == null) return;
            try
            {
                var take = JobMaker.MakeJob(JobDefOf.TakeInventory, meal);
                take.count = 1;
                if (!OwnHappenings.Job(p, take)) return;
                var go = JobMaker.MakeJob(KeepJob, guest);
                go.count = 200;
                go.locomotionUrgency = LocomotionUrgency.Walk;
                p.jobs.jobQueue.EnqueueLast(go);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: a meal for the callers", e);
            }
        }

        // ================= Done at the place =================

        /// <summary>The vow's job done at its target (JobDriver_VowKeep): the lamp seen to, the sapling set, the meal set down.</summary>
        internal static void Done(Pawn p, LocalTargetInfo target)
        {
            var r = Worship.Book?.vows.FirstOrDefault(v => v != null && !v.done && v.pawn == p && WorshipMath.SeasonVow(v.kind));
            if (r == null) return;
            var g = Gods.ById(r.god);
            var map = p.Map;
            switch (r.kind)
            {
                case VowKind.Lamp:
                    Worship.Say(p, KeepPool, g, null, "lamp");
                    Worship.Glow(map, target.Cell, g);
                    break;
                case VowKind.Trees:
                    var def = PeoplesE.SaplingFor(map, target.Cell);
                    if (def == null || PeoplesE.Sapling(map, target.Cell, def) == null) return;
                    r.count++;
                    Worship.Say(p, KeepPool, g, null, "trees");
                    break;
                case VowKind.Visitors:
                    var guest = target.Thing as Pawn;
                    var meal = p.inventory?.innerContainer?.FirstOrDefault(t => t.def.ingestible?.IsMeal == true);
                    if (guest == null || meal == null) return;
                    p.inventory.innerContainer.TryDrop(meal, guest.Position, map, ThingPlaceMode.Near, 1, out _);
                    r.count++;
                    r.pending = "fed|" + (guest.GetLord()?.loadID ?? -1);
                    // together: the vower walked up to the caller to set the meal down.
                    Worship.Say(p, KeepPool, g, guest, "visitors");
                    break;
            }
        }

        /// <summary>Someone of the colony ate: plain bread kept (a word once a day), or broken by a fine meal.</summary>
        internal static void Ate(Pawn p, Thing food)
        {
            var r = Vows.Open(p, VowKind.PlainBread);
            if (r == null || food?.def?.ingestible == null || !food.def.ingestible.IsMeal) return;
            if (WorshipMath.TooFine((int)food.def.ingestible.preferability, (int)FoodPreferability.MealFine))
            {
                Vows.End(r, false, "CC_Wsh_How_FineMeal".Translate(food.LabelNoCount.Named("FOOD")).Resolve());
                return;
            }
            if (r.lastDay == GenDate.DaysPassed) return;
            r.lastDay = GenDate.DaysPassed;
            r.count++;
            if (p.Spawned) Worship.Say(p, KeepPool, Gods.ById(r.god), null, "plainbread");
        }

        /// <summary>A tree felled by the vower: a vow of a tree a week is broken.</summary>
        internal static void Felled(Pawn by)
        {
            var r = Vows.Open(by, VowKind.Trees);
            if (r != null) Vows.End(r, false, "CC_Wsh_How_Felled".Translate(by.Named("PAWN")).Resolve());
        }
    }

    /// <summary>A vow of the season's own work at a cell or a thing (the lamp, the sapling's place, the caller): there, a while, then VowsSeason.Done.</summary>
    public class JobDriver_VowKeep : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !job.targetA.IsValid || job.targetA.HasThing && (job.targetA.Thing.Destroyed || !job.targetA.Thing.Spawned));
            yield return Toils_Goto.Goto(TargetIndex.A, PathEndMode.Touch);
            var work = ToilMaker.MakeToil("VowKeep");
            work.tickIntervalAction = delta =>
            {
                pawn.rotationTracker.FaceCell(job.targetA.Cell);
                if (pawn.IsHashIntervalTick(200, delta) && !job.targetA.HasThing) FleckMaker.ThrowDustPuff(job.targetA.Cell, pawn.Map, 0.4f);
            };
            work.handlingFacing = true;
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(60, job.count);
            yield return work;
            yield return Toils_General.Do(() =>
            {
                try
                {
                    VowsSeason.Done(pawn, job.targetA);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("worship: a vow kept at its place", e);
                }
            });
        }
    }

    // ================= The life goals =================

    /// <summary>A life goal that is a vow of a season: kept when the season is out (a mark in the worship book), only to its own gods.</summary>
    public abstract class AspirationWorker_SeasonVow : AspirationWorker_GodVow
    {
        public override bool CanHave(Pawn p) => VowsSeason.CanVow(p, Kind);

        public override bool Init(Aspiration a)
        {
            if (!base.Init(a)) return false;
            VowsSeason.Made(Vows.Open(a.pawn, Kind));
            return true;
        }

        public override float Progress(Aspiration a)
        {
            var g = GodOf(a);
            var b = Worship.Book;
            if (g == null || b == null) return 0f;
            if (b.Count(a.pawn, "vowkept:" + Kind, a.startedTick, g.id) > 0) return 1f;
            var r = Vows.Open(a.pawn, Kind);
            if (r == null || r.until <= r.since) return 0f;
            return Mathf.Clamp01((Find.TickManager.TicksGame - r.since) / (float)(r.until - r.since)) * 0.95f;
        }

        public override string ProgressText(Aspiration a) =>
            ("CC_Wsh_Goal_Season" + (Progress(a) >= 1f ? "Done_" : "_") + Kind).Translate(Worship.Called(GodOf(a)).Named("GOD"), Progress(a).ToStringPercent().Named("PERCENT")).Resolve();
    }

    public class AspirationWorker_VowLamp : AspirationWorker_SeasonVow
    {
        public override VowKind Kind => VowKind.Lamp;
    }

    public class AspirationWorker_VowTrees : AspirationWorker_SeasonVow
    {
        public override VowKind Kind => VowKind.Trees;
    }

    public class AspirationWorker_VowPlainBread : AspirationWorker_SeasonVow
    {
        public override VowKind Kind => VowKind.PlainBread;
    }

    public class AspirationWorker_VowVisitors : AspirationWorker_SeasonVow
    {
        public override VowKind Kind => VowKind.Visitors;
    }
}
