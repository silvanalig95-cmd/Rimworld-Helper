using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colonist bar's badges (ColonistBar/BarBadges.cs), drawn after the game has drawn each portrait: its mood
    /// background, the colonist, the name and its own icons along the bottom. The game calls DrawColonist only to paint,
    /// once per colonist per frame; the badges read a cache worked out at most once a second.
    /// </summary>
    [HarmonyPatch(typeof(ColonistBarColonistDrawer), nameof(ColonistBarColonistDrawer.DrawColonist))]
    public static class Patch_ColonistBarBadges
    {
        public static void Postfix(Rect rect, Pawn colonist, Map pawnMap, bool reordering)
        {
            if (!RimfolkPatches.On) return;
            BarBadges.Draw(rect, colonist, pawnMap, reordering);
        }
    }

    /// <summary>
    /// A left click on one of the colonist bar's badges opens where it can be dealt with (the Mood tab, the council's
    /// Wishes page, the Culture tab). The game handles the bar's clicks here before it paints, every event: the prefix
    /// takes a mouse-down that lands on a badge (so the bar neither selects nor starts a drag from it) and leaves every
    /// other event to the game. Applied on its own: if it can't be, the badges still show and explain, without clicks.
    /// </summary>
    [HarmonyPatch(typeof(ColonistBarColonistDrawer), nameof(ColonistBarColonistDrawer.HandleClicks))]
    public static class Patch_ColonistBarBadgeClicks
    {
        public static void Prefix(Rect rect, Pawn colonist)
        {
            if (!RimfolkPatches.On) return;
            BarBadges.Click(rect, colonist);
        }
    }
}
