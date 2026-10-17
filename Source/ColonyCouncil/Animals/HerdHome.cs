using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // Bringing the herd home (Defs/DilemmaDefs/Rimfolk_Dilemmas_Animals2.xml): a custom of the herd's year. Before the
    // winter, in a cold snap or after a raid, the colony walks the herd into the barn or the pen together. It plays out
    // on the map as a work party whose work is the herd itself (WorkTask.Herd, PlayOut/PlayWork.cs): each of them goes
    // out to an animal grazing in the open, puts a rope on it the game's own way (Toils_Rope), and walks it home.

    [DefOf]
    public static class HerdHomeDefOf
    {
        public static JobDef Council_PlayOutHerd;

        static HerdHomeDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(HerdHomeDefOf));
    }

    /// <summary>Where the herd goes home to (a barn, or the pen it belongs in), and which of it is still out.</summary>
    public static class HerdHome
    {
        /// <summary>A pen or a barn for the herd on this map.</summary>
        public static bool HasShelter(Map map) => map != null && (Barns(map).Any() || Pens(map).Any());

        private static IEnumerable<Room> Barns(Map map)
        {
            var role = RoomUtil.Barn;
            return role == null ? Enumerable.Empty<Room>() : RoomUtil.RoomsWithRole(map, role).Where(r => !r.PsychologicallyOutdoors);
        }

        private static IEnumerable<CompAnimalPenMarker> Pens(Map map)
        {
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                var pen = b.TryGetComp<CompAnimalPenMarker>();
                if (pen != null) yield return pen;
            }
        }

        /// <summary>Penned animals (the game ropes those) belong in a pen; the rest in a barn.</summary>
        private static bool Penned(Pawn a)
        {
            try
            {
                return AnimalPenUtility.NeedsToBeManagedByRope(a);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Under the barn's roof, or in its pen.</summary>
        public static bool Home(Pawn a)
        {
            if (a == null || !a.Spawned) return true;
            if (Penned(a))
            {
                try
                {
                    if (AnimalPenUtility.GetCurrentPenOf(a, false) != null) return true;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("herd home: pens", e);
                }
            }
            var room = a.GetRoom();
            return room != null && RoomUtil.Barn != null && room.Role == RoomUtil.Barn;
        }

        /// <summary>One of the herd that can be walked home now: out in the open, calm, awake enough, not already on a rope or with a caravan.</summary>
        public static bool Herdable(Pawn a) =>
            a != null && AnimalFacts.Livestock(a) && a.Spawned && !a.Downed && !a.InMentalState && a.GetLord() == null && !(a.roping?.IsRoped ?? false) && !Home(a);

        public static List<Pawn> Out(Map map) => AnimalFacts.Herd(map).Where(Herdable).ToList();

        /// <summary>Where to take it: a free cell in its pen, or in the nearest barn. Invalid when there's nowhere.</summary>
        public static IntVec3 Destination(Pawn roper, Pawn a)
        {
            var map = a?.Map;
            if (map == null) return IntVec3.Invalid;
            try
            {
                if (Penned(a))
                {
                    var pen = AnimalPenUtility.GetPenAnimalShouldBeTakenTo(roper, a, out _, false, true, false, true);
                    if (pen != null)
                    {
                        var c = AnimalPenUtility.FindPlaceInPenToStand(pen, roper);
                        if (c.IsValid) return c;
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("herd home: pen destination", e);
            }
            foreach (var room in Barns(map).OrderBy(r => r.Cells.FirstOrDefault().DistanceToSquared(a.Position)))
            {
                foreach (var c in room.Cells.InRandomOrder())
                    if (c.Standable(map) && c.GetFirstPawn(map) == null && roper.CanReach(c, PathEndMode.OnCell, Danger.Some)) return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>The work party's next animal for this one to bring in: the nearest one still out that nobody has a hand on.</summary>
        public static Job NextJob(Pawn roper, out Thing target)
        {
            target = null;
            var map = roper?.Map;
            if (map == null || roper.roping == null) return null;
            foreach (var a in Out(map).OrderBy(x => x.Position.DistanceToSquared(roper.Position)).Take(12))
            {
                if (a.IsForbidden(roper) || !roper.CanReserve(a) || !roper.CanReach(a, PathEndMode.Touch, Danger.Some)) continue;
                var cell = Destination(roper, a);
                if (!cell.IsValid) continue;
                target = a;
                var job = JobMaker.MakeJob(HerdHomeDefOf.Council_PlayOutHerd, a, cell);
                job.locomotionUrgency = LocomotionUrgency.Walk;
                return job;
            }
            return null;
        }

        /// <summary>Able to herd: the handling work isn't closed to them, and they have hands.</summary>
        public static bool CanHerd(Pawn x) =>
            x != null && !x.WorkTypeIsDisabled(WorkTypeDefOf.Handling) && (x.health?.capacities?.CapableOf(PawnCapacityDefOf.Manipulation) ?? false);
    }

    /// <summary>
    /// Bringing one of the herd home: walk out to it, put a rope on it (the game's own toils, so it follows on the rope
    /// as a roped animal does), walk it into the barn or the pen, and let it go there. Any rope left in hand when the job
    /// ends for any reason is dropped.
    /// </summary>
    public class JobDriver_PlayOutHerd : JobDriver
    {
        private Pawn Animal => job.targetA.Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => Animal == null || Animal.Downed || Animal.InMentalState || Animal.Faction != pawn.Faction || pawn.roping == null);
            AddFinishAction(_ =>
            {
                try
                {
                    pawn.roping?.DropRopes();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("herd home: drop ropes", e);
                }
            });
            yield return Toils_Rope.GotoRopeAttachmentInteractionCell(TargetIndex.A);
            yield return Toils_Rope.RopePawn(TargetIndex.A);
            var walk = Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            walk.FailOn(() => !(pawn.roping?.IsRopingOthers ?? false));
            yield return walk;
            var letGo = ToilMaker.MakeToil("HerdHomeLetGo");
            letGo.initAction = () =>
            {
                try
                {
                    var a = Animal;
                    if (a != null && pawn.roping != null && pawn.roping.Ropees.Contains(a)) pawn.roping.DropRope(a);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("herd home: let go", e);
                }
            };
            letGo.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return letGo;
        }

        public override string GetReport()
        {
            var a = Animal;
            return a != null ? "CC_Herd_Report".Translate(a.LabelShort.Named("ANIMAL")).Resolve() : base.GetReport();
        }
    }

    /// <summary>
    /// Bringing the herd home: a herd of three or more, a barn or a pen to bring it into, two or more of it out in the open,
    /// and the moment for it: the cold coming in the fall, a cold snap, or the day after a raid. {PET} is one of those
    /// still out. Asked when the moment is (DilemmaDef.timely), after the usual gap between questions.
    /// </summary>
    public class DilemmaWorker_HerdHome : DilemmaWorker_HerdCustom
    {
        public static bool Moment(Map map)
        {
            if (map == null) return false;
            int now = Find.TickManager.TicksGame;
            int threat = map.storyState?.LastThreatBigTick ?? 0;
            if (threat > 0 && now - threat > GenDate.TicksPerHour * 6 && now - threat < GenDate.TicksPerDay * 3 / 2) return true;
            var snap = GameConditionDefOf.ColdSnap;
            if (snap != null && map.gameConditionManager != null && map.gameConditionManager.ConditionIsActive(snap)) return true;
            var s = GenLocalDate.Season(map);
            return s == Season.Fall && Goal_Woodpile.ColdComing(map) && (map.mapTemperature?.OutdoorTemp ?? 15f) <= 8f;
        }

        public override bool CanHappenNow(Map map) =>
            base.CanHappenNow(map) && Moment(map) && HerdHome.HasShelter(map) && HerdHome.Out(map).Count >= 2;

        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx)) return false;
            ctx.pet = HerdHome.Out(ctx.map).RandomElementWithFallback() ?? ctx.pet;
            return ctx.pet != null;
        }
    }
}
