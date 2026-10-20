using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The game's own Needs tab: one line at the top of the mood list sorts the colonist's moods the way Rimfolk's Mood tab
    /// does ("You can fix -15 · Will pass -11 · Faith -4 · Lifting +22"), and a click opens that tab. The game draws the
    /// list in NeedsCardUtility.DrawThoughtListing(Rect listingRect, Pawn pawn, ref Vector2 thoughtScrollPosition); the
    /// prefix draws the line at the top of listingRect and hands the game a rect that starts below it, so the game's
    /// list and its scroll view simply begin a line lower. It never skips the game's method.
    /// <para>
    /// Fails safe: <see cref="Prepare"/> checks the method is there with that signature (a Rect named listingRect, then a
    /// Pawn named pawn); if not, the patch isn't applied at all and the tab is the game's own. Anything that goes wrong
    /// while drawing leaves the game's rect as it was. The tooltip lines on each row are a separate patch on
    /// DrawThoughtGroup (Patch_NeedsTabMoodHints), so the two never touch.
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(NeedsCardUtility), "DrawThoughtListing")]
    public static class Patch_NeedsTabMoodSummary
    {
        /// <summary>The line's height, and the gap under it.</summary>
        public const float LineHeight = 20f, Gap = 4f;

        /// <summary>Whether the game's method was found as expected (for "Rimfolk: check myself").</summary>
        public static bool Found;

        public static bool Prepare()
        {
            try
            {
                var m = AccessTools.Method(typeof(NeedsCardUtility), "DrawThoughtListing");
                var ps = m?.GetParameters();
                Found = m != null && m.IsStatic && ps.Length >= 2
                        && ps[0].ParameterType == typeof(Rect) && ps[0].Name == "listingRect"
                        && ps[1].ParameterType == typeof(Pawn) && ps[1].Name == "pawn";
            }
            catch
            {
                Found = false;
            }
            if (!Found) Log.Message("[Rimfolk] The Needs tab's mood list isn't drawn the way Rimfolk expects in this game version, so its summary line is off.");
            return Found;
        }

        public static void Prefix(ref Rect listingRect, Pawn pawn)
        {
            if (!Wanted(pawn) || listingRect.height < LineHeight * 4f) return;
            try
            {
                var line = MoodSummary.For(pawn);
                if (line == null) return;
                var r = new Rect(listingRect.x, listingRect.y, listingRect.width, LineHeight);
                // The game's own list goes on below the line (in the layout pass too, so both passes agree).
                listingRect.yMin += LineHeight + Gap;
                if (Event.current == null || Event.current.type == EventType.Layout) return;
                MoodSummary.Draw(r, line, pawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("needs tab mood summary", e);
            }
        }

        /// <summary>Colonists, prisoners and slaves of the colony, whose Mood tab exists, with the setting on.</summary>
        private static bool Wanted(Pawn pawn)
        {
            if (!RimfolkPatches.On || pawn?.needs?.mood?.thoughts == null || !pawn.RaceProps.Humanlike) return false;
            var s = ColonyCouncilMod.Settings;
            if (s == null || !s.moodInsight || !s.needsTabHints) return false;
            return pawn.IsColonist || pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony;
        }
    }
}
