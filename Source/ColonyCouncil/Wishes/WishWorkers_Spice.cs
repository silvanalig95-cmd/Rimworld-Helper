using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A prisoner someone wants dead: the bloodthirsty, the coldly practical, the grieving, and the short-tempered
    /// right after a raid. Never children, never someone they like.
    /// </summary>
    public class WishWorker_Justice : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || pawn.relations == null || HasTrait(pawn, TraitDefOf.Kind)) yield break;
            bool bloodlust = HasTrait(pawn, TraitDefOf.Bloodlust);
            bool psycho = HasTrait(pawn, TraitDefOf.Psychopath);
            bool grieving = Flavor.IsGrieving(pawn);
            int lastThreat = map.storyState.LastThreatBigTick;
            bool shaken = lastThreat > 0 && Find.TickManager.TicksGame - lastThreat < GenDate.TicksPerDay * 2 && HasTrait(pawn, TraitDefOf.Abrasive);
            if (!bloodlust && !psycho && !grieving && !shaken) yield break;

            foreach (var prisoner in map.mapPawns.PrisonersOfColonySpawned)
            {
                if (!prisoner.RaceProps.Humanlike || prisoner.DevelopmentalStage.Juvenile()) continue;
                if (prisoner.Faction != null && !prisoner.Faction.HostileTo(Faction.OfPlayer)) continue;
                int opinion = pawn.relations.OpinionOf(prisoner);
                if (opinion > (bloodlust || psycho ? 20 : 0)) continue;
                string key = bloodlust ? "CC_Reason_JusticeBlood" : grieving ? "CC_Reason_JusticeGrief" : psycho ? "CC_Reason_JusticeCold" : "CC_Reason_Justice";
                var c = Candidate(bloodlust ? 1f : grieving ? 0.8f : 0.5f, key.Translate(pawn.Named("PAWN"), prisoner.Named("OTHER")));
                c.other = prisoner;
                c.subjectKey = prisoner.ThingID;
                yield return c;
            }
        }

        public override bool IsFulfilled(Wish wish) => wish.other != null && wish.other.Dead;

        public override bool StillValid(Wish wish) => wish.other != null && (wish.other.Dead || wish.other.IsPrisonerOfColony);

        public override string ProgressText(Wish wish) => "CC_Progress_Justice".Translate(wish.other.Named("OTHER"));

        public override LookTargets LookAt(Wish wish) => wish.other != null && wish.other.Spawned ? new LookTargets(wish.other) : base.LookAt(wish);
    }

    /// <summary>
    /// A prisoner someone wants set free: a child behind bars, someone who'll never join, someone in a bad way,
    /// or simply someone they've come to like. Release or recruitment both count.
    /// </summary>
    public class WishWorker_Mercy : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || pawn.relations == null || HasTrait(pawn, TraitDefOf.Psychopath) || HasTrait(pawn, TraitDefOf.Bloodlust)) yield break;
            bool kind = HasTrait(pawn, TraitDefOf.Kind);
            foreach (var prisoner in map.mapPawns.PrisonersOfColonySpawned)
            {
                if (!prisoner.RaceProps.Humanlike) continue;
                int opinion = pawn.relations.OpinionOf(prisoner);
                bool child = prisoner.DevelopmentalStage.Juvenile();
                bool hopeless = prisoner.guest != null && !prisoner.guest.Recruitable;
                bool suffering = prisoner.health.summaryHealth.SummaryHealthPercent < 0.6f || prisoner.health.hediffSet.GetMissingPartsCommonAncestors().Count > 0;
                bool friend = opinion >= 25;
                if (opinion < 0 && !child) continue;
                if (!friend && !(kind && (child || hopeless || suffering))) continue;
                string key = child ? "CC_Reason_MercyChild" : suffering ? "CC_Reason_MercyHurt" : friend ? "CC_Reason_MercyFriend" : "CC_Reason_Mercy";
                var c = Candidate((kind ? 1f : 0.6f) * (child ? 1.5f : 1f), key.Translate(pawn.Named("PAWN"), prisoner.Named("OTHER")));
                c.other = prisoner;
                c.subjectKey = prisoner.ThingID;
                yield return c;
            }
        }

        public override bool IsFulfilled(Wish wish) => wish.other != null && !wish.other.Dead && !wish.other.IsPrisoner && !wish.other.IsSlaveOfColony;

        public override bool StillValid(Wish wish) => wish.other != null && !wish.other.Dead;

        public override string ProgressText(Wish wish) => "CC_Progress_Mercy".Translate(wish.other.Named("OTHER"));

        public override LookTargets LookAt(Wish wish) => wish.other != null && wish.other.Spawned ? new LookTargets(wish.other) : base.LookAt(wish);
    }

    /// <summary>
    /// "We need a party." Comes true when they get to one. Promising it calls one straight away, if there's
    /// somewhere to gather; out of patience, they throw it themselves.
    /// </summary>
    public class WishWorker_Party : WishWorker
    {
        private static bool HasSpot(Map map) => map.gatherSpotLister.activeSpots.Count > 0;

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || GatheringDefOf.Party == null || HasMemory(pawn, ThoughtDefOf.AttendedParty)) yield break;
            if (pawn.needs?.mood == null) yield break;
            float mood = pawn.needs.mood.CurLevelPercentage;
            var social = pawn.skills?.GetSkill(SkillDefOf.Social);
            bool socialite = social != null && !social.TotallyDisabled && social.passion != Passion.None;
            bool sanguine = pawn.story?.traits?.allTraits.Any(t => t.def.defName == "NaturalMood" && t.Degree > 0) ?? false;
            bool gloomyColony = map.mapPawns.FreeColonistsSpawned.Count >= 3
                                && map.mapPawns.FreeColonistsSpawned.Average(p => p.needs?.mood?.CurLevelPercentage ?? 0.5f) < 0.45f;
            if (HasTrait(pawn, DefDatabase<TraitDef>.GetNamedSilentFail("Recluse"))) yield break;
            if (!socialite && !sanguine && !gloomyColony) yield break;
            if (mood < 0.2f) yield break;
            var c = Candidate(gloomyColony ? 1.2f : 0.7f, (gloomyColony ? "CC_Reason_PartyGloom" : "CC_Reason_Party").Translate(pawn.Named("PAWN")));
            c.subjectKey = "colony";
            yield return c;
        }

        public override bool IsFulfilled(Wish wish)
        {
            int since = Find.TickManager.TicksGame - wish.createdTick;
            foreach (var p in wish.Supporters.Concat(new[] { wish.Beneficiary }))
            {
                var m = p?.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(ThoughtDefOf.AttendedParty);
                if (m != null && m.age <= since) return true;
            }
            return false;
        }

        public override string ProgressText(Wish wish) =>
            (HasSpot(wish.Beneficiary.MapHeld) ? "CC_Progress_Party" : "CC_Progress_NoGatherSpot").Translate();

        public override string OnPromised(Wish wish) => Throw(wish) ? "CC_Promise_Party" : null;

        public override string TrySelfHelp(Wish wish) => Throw(wish) ? "CC_SelfHelp_Party" : null;

        private static bool Throw(Wish wish)
        {
            var organizer = wish.pawn;
            var map = organizer?.MapHeld;
            if (map == null || !organizer.Spawned || GatheringDefOf.Party?.Worker == null) return false;
            return GatheringDefOf.Party.Worker.CanExecute(map, organizer) && GatheringDefOf.Party.Worker.TryExecute(map, organizer);
        }
    }

    /// <summary>Someone dear to them was carried off by raiders. They want them back.</summary>
    public class WishWorker_Rescue : WishWorker
    {
        public static Faction Holder(Pawn p)
        {
            foreach (var f in Find.FactionManager.AllFactionsListForReading)
                if (f.kidnapped != null && f.kidnapped.KidnappedPawnsListForReading.Contains(p)) return f;
            return null;
        }

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            if (pawn.relations == null || HomeMap(pawn) == null) yield break;
            foreach (var f in Find.FactionManager.AllFactionsListForReading)
            {
                if (f.kidnapped == null) continue;
                foreach (var k in f.kidnapped.KidnappedPawnsListForReading)
                {
                    if (k == null || k == pawn || k.Dead) continue;
                    bool family = pawn.relations.FamilyByBlood.Contains(k) || LovePartnerRelationUtility.LovePartnerRelationExists(pawn, k);
                    int opinion = pawn.relations.OpinionOf(k);
                    if (!family && opinion < 15) continue;
                    var c = Candidate(family ? 3f : 1.5f, "CC_Reason_Rescue".Translate(pawn.Named("PAWN"), k.Named("OTHER"), f.Name.Named("FACTION")));
                    c.other = k;
                    c.subjectKey = k.ThingID;
                    c.subjectLabel = f.Name;
                    yield return c;
                }
            }
        }

        public override bool IsFulfilled(Wish wish)
        {
            var k = wish.other;
            if (k == null || k.Dead || k.Faction != Faction.OfPlayer || k.IsPrisoner || Holder(k) != null) return false;
            return (k.MapHeld != null && k.MapHeld.IsPlayerHome) || k.IsCaravanMember();
        }

        public override bool StillValid(Wish wish) =>
            wish.other != null && !wish.other.Dead && (Holder(wish.other) != null || wish.other.Faction == Faction.OfPlayer);

        public override string ProgressText(Wish wish)
        {
            var f = wish.other != null ? Holder(wish.other) : null;
            return f != null ? "CC_Progress_Rescue".Translate(wish.other.Named("OTHER"), f.Name.Named("FACTION")) : null;
        }
    }

    /// <summary>A hunter, or someone with blood on the brain, wants to go out and bring something down.</summary>
    public class WishWorker_Hunt : WishWorker
    {
        private static IEnumerable<Pawn> Game(Map map) => map.mapPawns.AllPawnsSpawned.Where(a =>
            a.RaceProps.Animal && a.Faction == null && !a.Downed && !a.RaceProps.predator && !a.InMentalState
            && !map.fogGrid.IsFogged(a.Position) && a.RaceProps.baseBodySize >= 0.4f);

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || pawn.records == null || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hunting)) yield break;
            if (pawn.equipment?.Primary == null || !pawn.equipment.Primary.def.IsRangedWeapon) yield break;
            var shooting = pawn.skills?.GetSkill(SkillDefOf.Shooting);
            bool hunter = shooting != null && !shooting.TotallyDisabled && shooting.passion != Passion.None;
            bool bloodlust = HasTrait(pawn, TraitDefOf.Bloodlust);
            if (!hunter && !bloodlust) yield break;
            if (!Game(map).Any()) yield break;
            var c = Candidate(bloodlust ? 1f : 0.7f, (bloodlust ? "CC_Reason_HuntBlood" : "CC_Reason_Hunt").Translate(pawn.Named("PAWN")));
            c.subjectKey = "hunt";
            c.baseline = pawn.records.GetValue(RecordDefOf.KillsAnimals);
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => wish.Beneficiary.records != null && wish.Beneficiary.records.GetValue(RecordDefOf.KillsAnimals) >= wish.baseline + 1f;

        public override string ProgressText(Wish wish) => "CC_Progress_Hunt".Translate(wish.Beneficiary.Named("PAWN"));

        public override string OnPromised(Wish wish) => MarkPrey(wish, false) ? "CC_Promise_Hunt" : null;

        public override string TrySelfHelp(Wish wish) => MarkPrey(wish, true) ? "CC_SelfHelp_Hunt" : null;

        private static bool MarkPrey(Wish wish, bool switchOnWork)
        {
            var pawn = wish.Beneficiary;
            var map = pawn.MapHeld;
            if (map == null) return false;
            var prey = Game(map).Where(a => map.designationManager.DesignationOn(a) == null)
                .OrderByDescending(a => a.RaceProps.baseBodySize)
                .ThenBy(a => a.Position.DistanceTo(pawn.PositionHeld))
                .FirstOrDefault();
            if (prey == null) return false;
            map.designationManager.AddDesignation(new Designation(prey, DesignationDefOf.Hunt));
            wish.subjectLabel = prey.LabelShort;
            var ws = pawn.workSettings;
            if (switchOnWork && ws != null && ws.EverWork && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hunting) && ws.GetPriority(WorkTypeDefOf.Hunting) == 0)
                ws.SetPriority(WorkTypeDefOf.Hunting, 3);
            return true;
        }
    }
}
