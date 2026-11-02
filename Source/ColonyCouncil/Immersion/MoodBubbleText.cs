using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The game shows a small mood bubble over a colonist when they gain some moods (a meal eaten without a table, a
    /// corpse seen, a fine room), with only the mood's icon: what happened is only found by clicking them. With this,
    /// the mood's name floats up beside the bubble as well, green for a good one and red for a bad one, for whoever is
    /// in view. The bubble itself is the game's own; nothing else changes.
    /// </summary>
    [HarmonyPatch(typeof(MoteMaker), nameof(MoteMaker.MakeMoodThoughtBubble))]
    public static class Patch_MoodBubbleText
    {
        private static readonly Color Good = new Color(0.62f, 0.92f, 0.62f);
        private static readonly Color Bad = new Color(1f, 0.6f, 0.55f);

        public static void Postfix(Pawn pawn, Thought thought, MoteBubble __result)
        {
            if (__result == null || pawn == null || thought == null || !(ColonyCouncilMod.Settings?.moodBubbleText ?? true)) return;
            try
            {
                var map = pawn.Map;
                if (map == null || !pawn.Spawned || Find.CurrentMap != map) return;
                string label = thought.LabelCap;
                if (label.NullOrEmpty()) return;
                float mood = thought.MoodOffset();
                MoteMaker.ThrowText(pawn.DrawPos + new Vector3(0f, 0f, 1.15f), map, label, mood >= 0f ? Good : Bad, 3.5f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("mood bubble text", e);
            }
        }
    }
}
