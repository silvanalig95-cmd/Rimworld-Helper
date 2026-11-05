using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Where marriage alliances listen to the game and to Rimfolk's own feasts (Matches.cs): the wedding's guests at the
    /// gate and gone home (Feasts.StageChanged), where they stand while the game's ceremony is under way (Feasts.HereFocus).
    /// The wedding itself is heard where every wedding is (Patch_Married), and a spouse's death here (Patch_MatchKilled).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class MatchHooks
    {
        static MatchHooks()
        {
            Feasts.StageChanged += Matches.OnStage;
            Feasts.HereFocus = Matches.WeddingSpot;
        }
    }

    /// <summary>
    /// A spouse of a marriage alliance (or one promised in a match under way) killed: by whose hand and where, read before
    /// the death, and what follows once they are dead (Matches.Died). With the deeper game hooks only; the alliance's day
    /// sees a death all the same.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_MatchKilled
    {
        public static void Prefix(Pawn __instance, DamageInfo? dinfo, out MatchDeath __state)
        {
            __state = null;
            if (!RimfolkPatches.On || __instance == null || __instance.Dead || WorldLedger.Instance == null) return;
            try
            {
                if (Matches.Concerns(__instance)) __state = Matches.Dying(__instance, dinfo);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("matches: a death, before", e);
            }
        }

        public static void Postfix(Pawn __instance, MatchDeath __state)
        {
            if (__state == null || __instance == null || !__instance.Dead) return;
            try
            {
                Matches.Died(__instance, __state);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("matches: a death", e);
            }
        }
    }
}
