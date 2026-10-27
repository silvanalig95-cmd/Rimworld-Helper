using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// A visit whose words have the two sit down together (<see cref="DilemmaEffect_Visit.sit"/>): both go to seats at
    /// one of the colony's tables (across it from each other, on chairs where there are chairs), or round a fire, and
    /// stay there facing each other until the visit is over. The one visited gets a job of their own to sit there
    /// (<see cref="JobDriver_PlayOutSitWith"/>). Someone in bed is kept company at the bedside as before, and with no
    /// seats to be had the visitor keeps them company wherever they are.
    /// </summary>
    public static class PlaySit
    {
        /// <summary>Free to sit down: one of ours as for anything an answer sets going; a visitor awake, calm and not leaving.</summary>
        public static bool Free(PlayOutTracker t, PlayOut p, Pawn o)
        {
            if (o == null || o.Dead || !o.Spawned || o.Downed || o.InMentalState || o.Drafted || !o.Awake() || o.InBed()) return false;
            if (PlayOuts.OurPerson(o)) return PlayOuts.CanTakePart(o, o.Map) && PlayOuts.Interruptible(o) && !PlayOuts.NeedsPress(o) && !t.BusyWith(o, p) && o.GetLord() == null;
            if (o.HostileTo(Faction.OfPlayer) || o.IsPrisoner || o.CurJobDef == JobDefOf.Ingest) return false;
            if (o.needs?.food != null && o.needs.food.CurCategory >= HungerCategory.UrgentlyHungry) return false;
            var duty = o.mindState?.duty?.def;
            return duty == null || !duty.defName.StartsWith("ExitMap");
        }

        /// <summary>
        /// Two seats for <paramref name="a"/> and <paramref name="b"/>: at the nearest of the colony's tables to
        /// <paramref name="b"/> with two free places (chairs first), as far apart round it as can be; or beside a fire.
        /// <paramref name="focus"/>: what they sit round.
        /// </summary>
        public static bool Seats(Map map, Pawn a, Pawn b, PlaySpot by, out IntVec3 seatA, out IntVec3 seatB, out IntVec3 focus)
        {
            seatA = seatB = focus = IntVec3.Invalid;
            if (map == null || a == null || b == null) return false;
            IEnumerable<Thing> round;
            if (by == PlaySpot.Fire)
            {
                var fire = PlaySpots.ColonyFire(map, b);
                round = fire != null ? new[] { fire } : Enumerable.Empty<Thing>();
            }
            else
                round = map.listerBuildings.allBuildingsColonist.Where(t => t.Spawned && t.def.IsTable && !(t is Frame) && !t.IsForbidden(a) && !t.Position.Fogged(map))
                    .OrderBy(t => t.Position.DistanceToSquared(b.Position)).Take(8);
            foreach (var c in round)
            {
                var cells = GenAdj.CellsAdjacent8Way(c).Where(x => Seat(x, map, a, b)).ToList();
                if (by != PlaySpot.Fire)
                {
                    var chairs = cells.Where(x => x.GetEdifice(map)?.def?.building?.isSittable ?? false).ToList();
                    if (chairs.Count >= 2) cells = chairs;
                }
                if (cells.Count < 2) continue;
                float best = -1f;
                foreach (var x in cells)
                    foreach (var y in cells)
                    {
                        if (x == y) continue;
                        float d = x.DistanceToSquared(y);
                        if (d > best)
                        {
                            best = d;
                            seatA = x;
                            seatB = y;
                        }
                    }
                // The one visited takes the seat nearer them.
                if (seatA.DistanceToSquared(b.Position) < seatB.DistanceToSquared(b.Position)) (seatA, seatB) = (seatB, seatA);
                focus = c.Position;
                return true;
            }
            return false;
        }

        private static bool Seat(IntVec3 x, Map map, Pawn a, Pawn b)
        {
            if (!x.InBounds(map) || !x.Standable(map) || x.GetFirstPawn(map) != null || x.Fogged(map)) return false;
            if (map.pawnDestinationReservationManager.IsReserved(x)) return false;
            return a.CanReach(x, PathEndMode.OnCell, Danger.Some) && b.CanReach(x, PathEndMode.OnCell, Danger.Some);
        }

        /// <summary>Still sitting with them: on their visit job, with the other the one they came for.</summary>
        public static bool Visiting(Pawn v, Pawn o) => v?.CurJob != null && v.CurJobDef == PlayOutDefOf.Council_PlayOutVisit && v.CurJob.targetC.Thing == o;
    }

    /// <summary>
    /// The one visited, sat down with the visitor (<see cref="PlaySit"/>): to their seat, then there facing the visitor
    /// until the visit is over or they're called away.
    /// </summary>
    public class JobDriver_PlayOutSitWith : JobDriver
    {
        private int sat;

        private Pawn Visitor => job.targetB.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() =>
            Visitor is Pawn v ? "CC_Play_Report_SitWith".Translate(v.Named("OTHER")).Resolve() : base.GetReport();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            this.FailOn(() => PlayOuts.OurPerson(pawn) && (!PlayOuts.CanJoin(pawn) || PlayOuts.NeedsPress(pawn)));
            this.FailOn(() => Visitor == null || Visitor.Dead || !Visitor.Spawned || Visitor.Map != pawn.Map);
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var sit = ToilMaker.MakeToil("PlayOutSitWith");
            sit.defaultCompleteMode = ToilCompleteMode.Never;
            sit.socialMode = RandomSocialMode.SuperActive;
            sit.handlingFacing = true;
            sit.tickIntervalAction = delta =>
            {
                sat += delta;
                var v = Visitor;
                if (v != null) pawn.rotationTracker.FaceCell(v.Position);
                PawnUtility.GainComfortFromCellIfPossible(pawn, delta);
                // The visit is over (or never sat down after all): up again.
                if ((sat > 900 && !PlaySit.Visiting(v, pawn)) || sat > job.count) ReadyForNextToil();
            };
            yield return sit;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref sat, "sat");
        }
    }
}
