using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The game's own Needs tab: hovering a mood also shows why it's there, when it ends, which mod it comes from and
    /// what to do about it (the same as Rimfolk's Mood tab), in a second tooltip under the game's own.
    /// </summary>
    [HarmonyPatch(typeof(NeedsCardUtility), "DrawThoughtGroup")]
    public static class Patch_NeedsTabMoodHints
    {
        public static void Postfix(Rect rect, Thought group, Pawn pawn)
        {
            if (!RimfolkPatches.On || group?.def == null || pawn == null) return;
            var s = ColonyCouncilMod.Settings;
            if (s == null || !s.moodInsight || !s.needsTabHints) return;
            try
            {
                var thought = group;
                var who = pawn;
                TooltipHandler.TipRegion(rect, () => MoodInsight.HintTooltip(who, thought), Gen.HashCombineInt(thought.def.shortHash, who.thingIDNumber ^ 0x7A11));
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("needs tab hint", e);
            }
        }
    }
}
