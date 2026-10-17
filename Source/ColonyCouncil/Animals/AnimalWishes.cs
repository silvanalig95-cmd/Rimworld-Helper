using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The animals' wishes (Defs/WishDefs/Rimfolk_Wishes_Animals.xml): an animal of one's own, a barn for the herd, and
    // something to ride (Giddy-Up 2).

    /// <summary>
    /// A grown colonist who loves animals (a passion, an animal lover from a trait mod, a kind soul, or a lonely one) and
    /// has none of their own wants one of the colony's grown animals that has no master yet. Promised, they're made its
    /// master. Fulfilled when an animal is theirs, by bond or as its master.
    /// </summary>
    public class WishWorker_OwnAnimal : WishWorker
    {
        private static bool Fond(Pawn p)
        {
            var animals = p.skills?.GetSkill(SkillDefOf.Animals);
            if (animals == null || animals.TotallyDisabled) return false;
            return People.Passionate(animals) || PeopleTags.Has(p, "animallover") || HasTrait(p, TraitDefOf.Kind) || MoreHabits.Feeling(p, "Loneliness") != null;
        }

        /// <summary>A grown, trainable animal of the colony's with no master and no bond, the calmest and nearest first.</summary>
        public static Pawn Unclaimed(Pawn p)
        {
            var map = p?.MapHeld;
            if (map == null) return null;
            Pawn best = null;
            float bestScore = float.MinValue;
            foreach (var a in map.mapPawns.SpawnedColonyAnimals)
            {
                if (!AnimalFacts.Ours(a) || AnimalFacts.Young(a) || a.playerSettings == null || a.playerSettings.Master != null) continue;
                if (a.RaceProps.trainability == null || (a.relations?.DirectRelations.Any(r => r.def == PawnRelationDefOf.Bond) ?? false)) continue;
                float score = a.RaceProps.petness * 2f + (a.Name != null ? 0.5f : 0f) - (p.Spawned ? a.Position.DistanceTo(p.Position) / 100f : 0f) + Rand.Value * 0.3f;
                if (score <= bestScore) continue;
                best = a;
                bestScore = score;
            }
            return best;
        }

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || pawn.DevelopmentalStage.Juvenile() || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Handling) || !Fond(pawn)) yield break;
            if (AnimalFacts.HasAnimal(pawn)) yield break;
            var a = Unclaimed(pawn);
            if (a == null) yield break;
            var c = Candidate(People.Burning(pawn.skills.GetSkill(SkillDefOf.Animals)) ? 1.3f : 0.8f, "CC_Reason_OwnAnimal".Translate(pawn.Named("PAWN"), a.Named("OTHER")));
            c.other = a;
            c.subjectKey = a.ThingID;
            c.subjectLabel = AnimalFacts.KindLabel(a);
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => AnimalFacts.HasAnimal(wish.Beneficiary);

        public override bool StillValid(Wish wish) => wish.other != null && !wish.other.Dead;

        public override LookTargets LookAt(Wish wish) => wish.other != null && wish.other.Spawned ? new LookTargets(wish.other) : base.LookAt(wish);

        /// <summary>Promised: they're made its master (if nobody has been since).</summary>
        public override string OnPromised(Wish wish)
        {
            var p = wish.Beneficiary;
            var a = wish.other;
            if (p == null || !AnimalFacts.Ours(a) || a.playerSettings == null || a.playerSettings.Master != null || a.MapHeld != p.MapHeld) return null;
            try
            {
                a.playerSettings.Master = p;
                return "CC_Promise_OwnAnimal";
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wish: an animal of one's own", e);
                return null;
            }
        }

        public override void OnGranted(Wish wish) => wish.other = AnimalFacts.AnimalsOf(wish.Beneficiary).FirstOrDefault() ?? wish.other;

        // Promises kept: the animal stays theirs. Dead isn't taken back; given to someone else, sold or released is.
        public override KeptCheck Holds(Wish wish)
        {
            var kept = PetKept(wish.other);
            if (kept != KeptCheck.Holds) return kept;
            return AnimalFacts.Theirs(wish.other, wish.Beneficiary) ? KeptCheck.Holds : KeptCheck.Gone;
        }

        public override IEnumerable<Thing> KeptThings(Wish wish)
        {
            yield break;
        }

        public override string KeptKind(Wish wish) => "pet";
    }

    /// <summary>
    /// The colony keeps a herd (three head or more) that sleeps out in the weather: someone who loves animals, the
    /// herdmaster, or a kind soul wants a barn, a room with animal beds or sleeping spots for them (more often in the cold
    /// seasons). Planned from the barn layout; fulfilled when a barn has beds for most of the herd.
    /// </summary>
    public class WishWorker_Barn : WishWorker
    {
        public static RoomRoleDef BarnRole => RoomUtil.Barn;

        /// <summary>The most animal beds in any one barn on the map.</summary>
        public static int BarnBeds(Map map)
        {
            var role = BarnRole;
            if (map == null || role == null) return 0;
            int best = 0;
            foreach (var room in RoomUtil.RoomsWithRole(map, role))
                best = Math.Max(best, RoomUtil.RoomCount(room, d => AnimalUtil.IsAnimalBed(d)));
            return best;
        }

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || BarnRole == null || pawn.DevelopmentalStage.Juvenile()) yield break;
            int herd = AnimalFacts.HerdCount(map);
            if (herd < 3) yield break;
            int target = AnimalMath.BarnBeds(herd);
            if (BarnBeds(map) >= target) yield break;
            var animals = pawn.skills?.GetSkill(SkillDefOf.Animals);
            bool herdmaster = AnimalFacts.Herdmaster == pawn;
            bool fond = herdmaster || (animals != null && !animals.TotallyDisabled && People.Passionate(animals)) || HasTrait(pawn, TraitDefOf.Kind);
            if (!fond) yield break;
            var bed = Avail.BestBuildable(map, AnimalUtil.IsAnimalBed, "AnimalBed", "AnimalSleepingSpot");
            if (bed == null) yield break;
            var season = GenLocalDate.Season(map);
            bool cold = season == Season.Winter || season == Season.Fall || season == Season.PermanentWinter || (map.mapTemperature?.OutdoorTemp ?? 15f) < 5f;
            var c = Candidate((herdmaster ? 1.4f : 0.8f) * (cold ? 1.5f : 1f), "CC_Reason_Barn".Translate(pawn.Named("PAWN"), herd.Named("COUNT")));
            c.thingDef = bed;
            c.targetValue = target;
            c.subjectKey = "colony";
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => BarnBeds(wish.Beneficiary?.MapHeld) >= wish.targetValue;

        public override bool StillValid(Wish wish) => AnimalFacts.HerdCount(wish.Beneficiary?.MapHeld) >= 2;

        public override string ProgressText(Wish wish) => "CC_Progress_Barn".Translate(BarnBeds(wish.Beneficiary?.MapHeld), wish.targetValue.ToString("0"));

        public override float Progress(Wish wish) => wish.targetValue <= 0f ? -1f : Math.Min(1f, BarnBeds(wish.Beneficiary?.MapHeld) / wish.targetValue);

        public override bool Concerns(Wish wish, BuildableDef d) => d is ThingDef td && AnimalUtil.IsAnimalBed(td);

        public override string KeptKind(Wish wish) => "thing";
    }

    /// <summary>
    /// Giddy-Up 2: something to ride. Someone who hunts, travels, or chafes at staying in one place wants an animal of
    /// their own they can ride. One of the colony's without a master is made theirs when promised; otherwise one is tamed
    /// for them. Fulfilled when one is theirs. Never without Giddy-Up, where riding isn't a thing.
    /// </summary>
    public class WishWorker_Mount : WishWorker
    {
        private static bool Fits(Pawn a) => AnimalFacts.Rideable(a);

        private static bool HasOne(Pawn p) => AnimalFacts.AnimalsOf(p).Any(Fits);

        private static Pawn Unclaimed(Map map) =>
            map?.mapPawns.SpawnedColonyAnimals.Where(a => AnimalFacts.Ours(a) && Fits(a) && a.playerSettings?.Master == null
                                                        && (a.relations == null || !a.relations.DirectRelations.Any(r => r.def == PawnRelationDefOf.Bond)))
                .OrderByDescending(a => a.GetStatValue(StatDefOf.MoveSpeed)).FirstOrDefault();

        private static Pawn WildOne(Map map) =>
            map?.mapPawns.AllPawnsSpawned.FirstOrDefault(a => AnimalFacts.GentleWild(a, map) && AnimalFacts.RideableKind(a.def));

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            if (!AnimalFacts.GiddyUp) yield break;
            var map = HomeMap(pawn);
            if (map == null || pawn.DevelopmentalStage.Juvenile() || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Handling) || HasOne(pawn)) yield break;
            bool hunter = pawn.workSettings != null && pawn.workSettings.WorkIsActive(WorkTypeDefOf.Hunting);
            // Restless: the heart's need for freedom running low.
            bool traveler = WishWorker_CampEvening.Lacks(pawn, HeartNeed.Freedom);
            var animals = pawn.skills?.GetSkill(SkillDefOf.Animals);
            bool fond = animals != null && !animals.TotallyDisabled && People.Passionate(animals);
            if (!hunter && !traveler && !fond) yield break;
            var free = Unclaimed(map);
            var example = free ?? WildOne(map);
            if (example == null) yield break;
            // {SUBJECT} is the kind with its article ("a horse", "an elephant"): the lines never put one before it.
            string kind = Find.ActiveLanguageWorker.WithIndefiniteArticle(AnimalFacts.KindLabel(example));
            var c = Candidate(free != null ? 1f : 0.6f, "CC_Reason_Mount".Translate(pawn.Named("PAWN"), kind.Named("KIND")));
            c.subjectLabel = kind;
            c.subjectKey = "mount";
            if (free != null) c.other = free;
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => HasOne(wish.Beneficiary);

        public override bool StillValid(Wish wish) => AnimalFacts.GiddyUp;

        public override string OnPromised(Wish wish)
        {
            var p = wish.Beneficiary;
            var a = wish.other;
            if (p == null || !AnimalFacts.Ours(a) || a.playerSettings == null || a.playerSettings.Master != null || a.MapHeld != p.MapHeld) return null;
            try
            {
                a.playerSettings.Master = p;
                return "CC_Promise_OwnAnimal";
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wish: a mount", e);
                return null;
            }
        }

        public override string TrySelfHelp(Wish wish)
        {
            var map = wish.Beneficiary?.MapHeld;
            var a = WildOne(map);
            if (a == null || map.designationManager.DesignationOn(a) != null) return null;
            map.designationManager.AddDesignation(new Designation(a, DesignationDefOf.Tame));
            return "CC_SelfHelp_Tame";
        }

        public override void OnGranted(Wish wish) => wish.other = AnimalFacts.AnimalsOf(wish.Beneficiary).FirstOrDefault(Fits) ?? wish.other;

        public override KeptCheck Holds(Wish wish)
        {
            var kept = PetKept(wish.other);
            if (kept != KeptCheck.Holds) return kept;
            return AnimalFacts.Theirs(wish.other, wish.Beneficiary) ? KeptCheck.Holds : KeptCheck.Gone;
        }

        public override IEnumerable<Thing> KeptThings(Wish wish)
        {
            yield break;
        }

        public override string KeptKind(Wish wish) => "pet";
    }
}
