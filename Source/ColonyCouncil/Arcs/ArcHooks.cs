using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A conversation that belongs to a personal story (a word between rivals, a confession to a friend, the master
    /// looking over the work). It is an ordinary interaction: it goes into the social log and shows as a bubble. It only
    /// comes up between the two the story is waiting on, once its stage is ready for it, and then fairly often (chitchat
    /// is 1, this 0.8), so it happens when they next spend time near each other.
    /// </summary>
    public class InteractionWorker_ArcTalk : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            try
            {
                if (initiator == null || recipient == null || !initiator.RaceProps.Humanlike || !recipient.RaceProps.Humanlike) return 0f;
                return ArcTracker.Instance?.TalkWeight(interaction, initiator, recipient) ?? 0f;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("arc talk weight " + interaction?.defName, e);
                return 0f;
            }
        }

        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
            try
            {
                ArcTracker.Instance?.Notify_Talked(interaction, initiator, recipient);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("arc talk " + interaction?.defName, e);
            }
        }
    }

    /// <summary>
    /// Something with a quality was made (a crafted piece, a sculpture, a building): a personal story waiting for its
    /// colonist to make something good hears of it, and a fine piece counts for a life goal that wants one. Without this
    /// hook (deeper game hooks off, or the method changed), enough work in the story's skill counts instead.
    /// </summary>
    [HarmonyPatch(typeof(QualityUtility), nameof(QualityUtility.SendCraftNotification))]
    public static class Patch_ArcMade
    {
        public static bool Works => RimfolkPatches.On && RimfolkPatches.Applied.Contains(nameof(Patch_ArcMade));

        public static void Postfix(Thing thing, Pawn worker)
        {
            if (!RimfolkPatches.On || thing == null || worker == null || worker.Faction != Faction.OfPlayer) return;
            try
            {
                ArcTracker.Instance?.Notify_Made(worker, thing);
                // Something fine made: counted for the life goals that want one (an elder's last good piece).
                if (thing.TryGetQuality(out var q) && q >= QualityCategory.Excellent) CouncilGameComponent.Instance?.aspirations?.Notify(worker, "madeFine");
                PersonaMarks.Notify_Made(worker, thing);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("arc made", e);
            }
        }
    }
}
