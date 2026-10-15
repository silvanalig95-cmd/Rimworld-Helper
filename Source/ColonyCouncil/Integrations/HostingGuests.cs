using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Hospitality: someone sociable wants the colony to host its guests well, with a guest bed in a room worth
    /// sleeping in. With no guest bed yet, any guest bed indoors in a decent room will do; once there is one, the best
    /// guest room should reach the next step of impressiveness. Without Hospitality there are no guest beds and nobody
    /// asks. The guest room plan comes with it.
    /// </summary>
    public class WishWorker_GuestRoom : WishWorker
    {
        /// <summary>A first guest room counts once it reaches this (decent).</summary>
        private const float FirstRoom = 20f;
        /// <summary>Beyond this (very impressive), nobody asks for more.</summary>
        private const float Enough = 65f;

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || !People.ModActive(Guests.Mod) || pawn.DevelopmentalStage.Juvenile()) yield break;
            float w = 0f;
            string reason = null;
            var social = pawn.skills?.GetSkill(SkillDefOf.Social);
            if (social != null && !social.TotallyDisabled && People.Passionate(social))
            {
                w = 0.8f;
                reason = "CC_PM_Reason_Host".Translate(pawn.Named("PAWN"));
            }
            if (HasTrait(pawn, TraitDefOf.Kind))
            {
                w += 0.5f;
                reason ??= "CC_PM_Reason_HostKind".Translate(pawn.Named("PAWN"));
            }
            var office = ColonyCouncilMod.Settings.offices ? CouncilGameComponent.Instance?.offices?.OfficeOf(pawn) : null;
            if (office?.defName == "Council_Office_Steward" || office?.defName == "Council_Office_Speaker")
            {
                w += 0.7f;
                reason = "CC_PM_Reason_HostOffice".Translate(pawn.Named("PAWN"), office.label);
            }
            if (w <= 0f) yield break;
            float best = BestRoom(map, out bool any);
            float target = !any ? FirstRoom : RoomUtil.NextStageThreshold(RoomStatDefOf.Impressiveness, Mathf.Max(best, 0f));
            if (target <= 0f || (any && best >= Enough)) yield break;
            // Guests on the map now make it come to mind.
            if (Strangers.On(map, StrangerKind.Visitor).Any(Strangers.IsHospitalityGuest)) w *= 2f;
            var c = Candidate(w, reason);
            c.targetValue = target;
            c.baseline = any ? best : 0f;
            c.subjectLabel = RoomUtil.StageLabel(RoomStatDefOf.Impressiveness, target);
            yield return c;
        }

        /// <summary>The most impressive room with a guest bed in it (indoors). <paramref name="any"/>: there is one at all.</summary>
        public static float BestRoom(Map map, out bool any)
        {
            any = false;
            float best = float.MinValue;
            foreach (var bed in Guests.Beds(map))
            {
                var room = bed.GetRoom();
                if (room == null || room.PsychologicallyOutdoors) continue;
                any = true;
                best = Mathf.Max(best, RoomUtil.Impressiveness(room));
            }
            return any ? best : 0f;
        }

        public override bool IsFulfilled(Wish wish)
        {
            float best = BestRoom(HomeMap(wish.pawn), out bool any);
            return any && best >= wish.targetValue;
        }

        public override bool StillObtainable(Wish wish) => base.StillObtainable(wish) && People.ModActive(Guests.Mod);

        public override string ProgressText(Wish wish)
        {
            float best = BestRoom(HomeMap(wish.pawn), out bool any);
            return any ? "CC_PM_Progress_GuestRoom".Translate(best.ToString("0"), wish.targetValue.ToString("0")).Resolve() : "CC_PM_Progress_NoGuestBed".Translate().Resolve();
        }

        public override float Progress(Wish wish)
        {
            float best = BestRoom(HomeMap(wish.pawn), out bool any);
            if (!any) return 0f;
            if (wish.targetValue <= wish.baseline) return best >= wish.targetValue ? 1f : 0f;
            return Mathf.Clamp01((best - wish.baseline) / (wish.targetValue - wish.baseline));
        }
    }
}
