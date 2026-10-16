using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    internal static class AnimalUtil
    {
        /// <summary>Colony animals bonded to this colonist, alive and at home.</summary>
        public static IEnumerable<Pawn> BondedAnimals(Pawn p)
        {
            if (p.relations == null) yield break;
            foreach (var r in p.relations.DirectRelations)
            {
                var a = r.otherPawn;
                if (r.def == PawnRelationDefOf.Bond && a != null && !a.Dead && a.RaceProps.Animal && a.Faction == Faction.OfPlayer && a.MapHeld == p.MapHeld)
                    yield return a;
            }
        }

        public static bool IsAnimalBed(ThingDef d) => d.IsBed && d.building != null && !d.building.bed_humanlike;
    }

    /// <summary>A colonist bonded to a small or medium animal wants it sleeping in their room.</summary>
    public class WishWorker_PetBed : WishWorker
    {
        public static bool HasAnimalBed(Room room) => room != null && room.ContainedAndAdjacentThings.Any(t => t.def != null && AnimalUtil.IsAnimalBed(t.def));

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            var room = RoomUtil.PrivateRoom(pawn);
            if (map == null || room == null || HasAnimalBed(room)) yield break;
            var pet = AnimalUtil.BondedAnimals(pawn).Where(a => a.BodySize <= 1.2f).OrderBy(a => a.BodySize).FirstOrDefault();
            if (pet == null) yield break;
            var bed = Avail.BestBuildable(map, AnimalUtil.IsAnimalBed, "AnimalBed", "AnimalSleepingSpot");
            if (bed == null) yield break;
            var c = Candidate(1.3f, "CC_Reason_PetBed".Translate(pawn.Named("PAWN"), pet.Named("OTHER")));
            c.other = pet;
            c.thingDef = bed;
            c.subjectKey = pet.ThingID;
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => HasAnimalBed(RoomUtil.PrivateRoom(wish.Beneficiary));

        public override bool StillValid(Wish wish) => wish.other != null && !wish.other.Dead && RoomUtil.PrivateRoom(wish.Beneficiary) != null;

        public override string TrySelfHelp(Wish wish) => PlanInOwnRoom(wish.Beneficiary, wish.thingDef) ? "CC_SelfHelp_Plan" : null;

        public override LookTargets LookAt(Wish wish) => wish.other != null && wish.other.Spawned ? new LookTargets(wish.other) : base.LookAt(wish);

        // Promises kept: the pet's bed stays in their room, as long as there's a pet to sleep in it.
        public override bool InTheirRoom => true;

        public override bool Concerns(Wish wish, BuildableDef d) => d is ThingDef td && AnimalUtil.IsAnimalBed(td);

        public override KeptCheck Holds(Wish wish)
        {
            var pet = wish.other;
            if (pet == null || pet.Dead || pet.Faction != Faction.OfPlayer) return KeptCheck.Over;
            if (RoomUtil.PrivateRoom(wish.Beneficiary) == null) return KeptCheck.Unclear;
            return IsFulfilled(wish) ? KeptCheck.Holds : KeptCheck.Gone;
        }

        public override string KeptKind(Wish wish) => "pet";
    }

    /// <summary>A bonded animal is hurt: its person wants it tended and on its feet again.</summary>
    public class WishWorker_HurtAnimal : WishWorker
    {
        private static bool Hurt(Pawn a) => a.health != null && (a.health.HasHediffsNeedingTend() || a.health.summaryHealth.SummaryHealthPercent < 0.7f);

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            if (HomeMap(pawn) == null) yield break;
            foreach (var a in AnimalUtil.BondedAnimals(pawn))
            {
                if (!Hurt(a)) continue;
                var c = Candidate(3f, "CC_Reason_HurtAnimal".Translate(pawn.Named("PAWN"), a.Named("OTHER")));
                c.other = a;
                c.subjectKey = a.ThingID;
                yield return c;
            }
        }

        public override bool IsFulfilled(Wish wish) => wish.other != null && !wish.other.Dead && !wish.other.health.HasHediffsNeedingTend() && wish.other.health.summaryHealth.SummaryHealthPercent >= 0.85f;

        public override bool StillValid(Wish wish) => wish.other != null && !wish.other.Dead;

        public override float Progress(Wish wish) => wish.other == null ? -1f : wish.other.health.summaryHealth.SummaryHealthPercent;

        public override string ProgressText(Wish wish) => "CC_Progress_HurtAnimal".Translate(wish.other.Named("OTHER"), wish.other.health.summaryHealth.SummaryHealthPercent.ToStringPercent());

        public override LookTargets LookAt(Wish wish) => wish.other != null && wish.other.Spawned ? new LookTargets(wish.other) : base.LookAt(wish);
    }
}
