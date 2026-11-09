using System;
using HarmonyLib;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A book always has its title. The game names a book with a quality when its quality is set (Book.PostQualitySet), so
    /// one made and handed over without setting it (a visitor's gift, a camp's book, a scholar's copy: Rimfolk makes things
    /// in many places) kept an empty title, and an empty title is the book's whole name: the player's trade window listed
    /// two textbooks as "(normal)". When such a book comes onto a map, including one already saved that way, it is named
    /// now, the game's own way (Book.GenerateBook).
    /// </summary>
    [HarmonyPatch(typeof(Book), nameof(Book.SpawnSetup))]
    public static class Patch_BookTitle
    {
        public static void Postfix(Book __instance)
        {
            try
            {
                if (__instance != null && __instance.Title.NullOrEmpty()) __instance.GenerateBook();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a book's title", e);
            }
        }
    }
}
