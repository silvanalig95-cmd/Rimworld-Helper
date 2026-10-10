using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Engaged colonists want the wedding. The game marries fiancés on its own eventually; a marriage spot helps.</summary>
    public class WishWorker_Wedding : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            if (pawn.relations == null || !pawn.DevelopmentalStage.Adult()) yield break;
            foreach (var rel in pawn.relations.DirectRelations)
            {
                if (rel.def != PawnRelationDefOf.Fiance) continue;
                var other = rel.otherPawn;
                if (other == null || other.Dead || !other.IsFreeColonist || other.MapHeld != pawn.MapHeld) continue;
                var c = Candidate(2.5f, "CC_Reason_Wedding".Translate(pawn.Named("PAWN"), other.Named("OTHER")));
                c.other = other;
                c.subjectKey = WishWorker_ShareBed.PairKey(pawn, other);
                c.thingDef = DefDatabase<ThingDef>.GetNamedSilentFail("MarriageSpot");
                yield return c;
            }
        }

        public override bool IsFulfilled(Wish wish) => wish.other != null && wish.Beneficiary.relations != null
                                                        && wish.Beneficiary.relations.DirectRelationExists(PawnRelationDefOf.Spouse, wish.other);

        public override bool StillValid(Wish wish)
        {
            var o = wish.other;
            return o != null && !o.Dead && wish.Beneficiary.relations != null
                   && (wish.Beneficiary.relations.DirectRelationExists(PawnRelationDefOf.Fiance, o) || wish.Beneficiary.relations.DirectRelationExists(PawnRelationDefOf.Spouse, o));
        }

        /// <summary>They mark out a marriage spot themselves, somewhere pleasant outside.</summary>
        public override string TrySelfHelp(Wish wish)
        {
            var spot = DefDatabase<ThingDef>.GetNamedSilentFail("MarriageSpot");
            var map = wish.Beneficiary.MapHeld;
            if (spot == null || map == null || Avail.ColonyHas(map, d => d == spot) || !Avail.CanBuild(spot, map)) return null;
            foreach (var c in GenRadial.RadialCellsAround(TemplatePlanner.ColonyCenter(map), 15f, true))
            {
                if (!c.InBounds(map) || c.Fogged(map) || c.Roofed(map) || c.GetZone(map) != null || !c.Standable(map)) continue;
                if (!GenConstruct.CanPlaceBlueprintAt(spot, c, Rot4.North, map).Accepted) continue;
                GenConstruct.PlaceBlueprintForBuild(spot, c, map, Rot4.North, Faction.OfPlayer, null);
                return "CC_SelfHelp_Wedding";
            }
            return null;
        }
    }

    /// <summary>A birthday coming up: they'd like a feast, or at least a party.</summary>
    public class WishWorker_Birthday : WishWorker
    {
        private static readonly string[] Treats = { "AteFineMeal", "AteLavishMeal", "AttendedParty", "AteInImpressiveDiningRoom" };

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || pawn.ageTracker == null) yield break;
            int today = GenLocalDate.DayOfYear(map);
            int bday = pawn.ageTracker.BirthDayOfYear;
            int until = (bday - today + GenDate.DaysPerYear) % GenDate.DaysPerYear;
            if (until > 2) yield break;
            bool fine = WishWorker_BetterFood.CanCook(map, FoodPreferability.MealFine);
            var c = Candidate(3f, "CC_Reason_Birthday".Translate(pawn.Named("PAWN"), (pawn.ageTracker.AgeBiologicalYears + (until == 0 ? 0 : 1)).Named("AGE")));
            c.subjectKey = Find.TickManager.TicksGame / GenDate.TicksPerYear + "y";
            c.subjectLabel = fine ? "CC_Birthday_Feast".Translate().ToString() : "CC_Birthday_Party".Translate().ToString();
            yield return c;
        }

        public override bool IsFulfilled(Wish wish)
        {
            var mem = wish.Beneficiary.needs?.mood?.thoughts?.memories;
            if (mem == null) return false;
            int since = Find.TickManager.TicksGame - wish.createdTick;
            return mem.Memories.Any(m => Treats.Contains(m.def.defName) && m.age <= since);
        }
    }

    /// <summary>Art in their own bedroom: artists and the greedy want something beautiful to wake up to.</summary>
    public class WishWorker_ArtInRoom : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var room = RoomUtil.PrivateRoom(pawn);
            var map = HomeMap(pawn);
            if (room == null || map == null || HasTrait(pawn, TraitDefOf.Ascetic)) yield break;
            if (RoomUtil.RoomContains(room, d => d.IsArt)) yield break;
            bool canMakeArt = Avail.ColonyHas(map, d => Avail.IsWorkstationFor(d, SkillDefOf.Artistic));
            bool haveArt = map.listerThings.ThingsInGroup(ThingRequestGroup.Art).Any(t => !t.Spawned || t.Position.GetRoom(map) == null || t.Position.GetRoom(map).PsychologicallyOutdoors || t.MapHeld == map);
            if (!canMakeArt && !haveArt) yield break;
            var art = pawn.skills?.GetSkill(SkillDefOf.Artistic);
            bool artist = art != null && !art.TotallyDisabled && art.passion != Passion.None;
            bool greedy = HasTrait(pawn, TraitDefOf.Greedy);
            if (!artist && !greedy && RoomUtil.ExpectationOrder(pawn) < 3) yield break;
            yield return Candidate(artist ? 1.2f : 0.7f, artist
                ? "CC_Reason_ArtInRoom_Artist".Translate(pawn.Named("PAWN"))
                : "CC_Reason_ArtInRoom".Translate(pawn.Named("PAWN")));
        }

        public override bool IsFulfilled(Wish wish) => RoomUtil.RoomContains(RoomUtil.PrivateRoom(wish.Beneficiary), d => d.IsArt);

        public override bool StillValid(Wish wish) => RoomUtil.PrivateRoom(wish.Beneficiary) != null;
    }

    /// <summary>Worn out, bored, or simply lazy: a bit more recreation in their schedule.</summary>
    public class WishWorker_DayOff : WishWorker
    {
        public const int WantedHours = 4;

        public static int JoyHours(Pawn p) => p.timetable?.times?.Count(t => t == TimeAssignmentDefOf.Joy || t == TimeAssignmentDefOf.Anything) ?? 24;

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            if (pawn.timetable == null || pawn.needs?.joy == null) yield break;
            int hours = pawn.timetable.times.Count(t => t == TimeAssignmentDefOf.Joy);
            if (hours >= WantedHours) yield break;
            int lazy = -TraitDegreeOf(pawn, TraitDefOf.Industriousness);
            bool tired = pawn.needs.joy.CurLevelPercentage < 0.3f;
            if (!tired && lazy <= 0) yield break;
            var c = Candidate(tired ? 1.5f : 0.8f, tired
                ? "CC_Reason_DayOff_Tired".Translate(pawn.Named("PAWN"))
                : "CC_Reason_DayOff_Lazy".Translate(pawn.Named("PAWN")));
            c.targetValue = WantedHours;
            yield return c;
        }

        private static int TraitDegreeOf(Pawn p, TraitDef t) => t != null && p.story?.traits != null && p.story.traits.HasTrait(t) ? p.story.traits.DegreeOfTrait(t) : 0;

        public override bool IsFulfilled(Wish wish) => wish.Beneficiary.timetable != null
                                                       && wish.Beneficiary.timetable.times.Count(t => t == TimeAssignmentDefOf.Joy) >= wish.targetValue;

        public override string ProgressText(Wish wish) =>
            "CC_Progress_JoyHours".Translate(wish.Beneficiary.timetable?.times.Count(t => t == TimeAssignmentDefOf.Joy) ?? 0, wish.targetValue.ToString("0"));
    }

    /// <summary>Those with a taste for chemicals want something in stock: beer, smokeleaf, whatever the colony makes.</summary>
    public class WishWorker_LittleSomething : WishWorker
    {
        public const int Wanted = 10;

        private static IEnumerable<ThingDef> SocialDrugs => DefDatabase<ThingDef>.AllDefsListForReading
            .Where(d => d.IsDrug && d.ingestible != null && d.ingestible.drugCategory == DrugCategory.Social);

        public static int Stock(Map map) => SocialDrugs.Sum(d => map.resourceCounter.GetCount(d));

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || TraitDefOf.DrugDesire == null || !HasTrait(pawn, TraitDefOf.DrugDesire)) yield break;
            if (pawn.story.traits.DegreeOfTrait(TraitDefOf.DrugDesire) <= 0) yield break;
            if (Stock(map) >= Wanted) yield break;
            bool canMake = Avail.AnyBuildable(map, Avail.IsFermenter) || Avail.ColonyHas(map, Avail.IsFermenter)
                           || Avail.AnyBuildable(map, d => d.AllRecipes != null && d.AllRecipes.Any(r => r.ProducedThingDef != null && SocialDrugs.Contains(r.ProducedThingDef)));
            if (!canMake) yield break;
            var c = Candidate(1.3f, "CC_Reason_LittleSomething".Translate(pawn.Named("PAWN")));
            c.targetValue = Wanted;
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => Stock(wish.Beneficiary.MapHeld) >= wish.targetValue;

        public override string ProgressText(Wish wish) => "CC_Progress_Stock".Translate(Stock(wish.Beneficiary.MapHeld), wish.targetValue.ToString("0"));
    }

    /// <summary>A kind or lonely colonist wants a particular prisoner to join the colony.</summary>
    public class WishWorker_Recruit : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || pawn.relations == null) yield break;
            bool kind = HasTrait(pawn, TraitDefOf.Kind);
            var social = pawn.skills?.GetSkill(SkillDefOf.Social);
            bool talker = social != null && !social.TotallyDisabled && social.passion != Passion.None;
            if (!kind && !talker) yield break;
            foreach (var prisoner in map.mapPawns.PrisonersOfColonySpawned)
            {
                if (prisoner.guest == null || !prisoner.guest.Recruitable || !prisoner.RaceProps.Humanlike) continue;
                int opinion = pawn.relations.OpinionOf(prisoner);
                if (opinion < 0) continue;
                var c = Candidate(kind ? 1.4f : 0.8f, "CC_Reason_Recruit".Translate(pawn.Named("PAWN"), prisoner.Named("OTHER")));
                c.other = prisoner;
                c.subjectKey = prisoner.ThingID;
                yield return c;
            }
        }

        public override bool IsFulfilled(Wish wish) => wish.other != null && wish.other.IsColonist && wish.other.Faction == Faction.OfPlayer;

        public override bool StillValid(Wish wish) => wish.other != null && !wish.other.Dead && (wish.other.IsPrisonerOfColony || wish.other.IsColonist);

        public override LookTargets LookAt(Wish wish) => wish.other != null && wish.other.Spawned ? new LookTargets(wish.other) : base.LookAt(wish);
    }
}
