using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // A scenario's start for a new colony (Colonies/ScenarioStarts.cs): the parts that shape starting people (forced traits
    // and hediffs, the age filter, naked, needs) reach the newcomers through their own pawn hooks. The game asks its own
    // scenario's parts about every person it makes (Find.Scenario); while the newcomers' window is open with a scenario's
    // start, a person made as a starting colonist is asked of that scenario's shaping parts instead, and nothing else is
    // touched: every other person, and every person at any other time, goes to the game's own scenario as always.

    /// <summary>A starting colonist being made in the newcomers' window: the chosen scenario's shaping parts, before the rest of them is made.</summary>
    [HarmonyPatch(typeof(Scenario), nameof(Scenario.Notify_NewPawnGenerating))]
    public static class Patch_ScenarioStartShapeNew
    {
        public static bool Prefix(Scenario __instance, Pawn pawn, PawnGenerationContext context)
        {
            if (ScenarioStarts.Shaping == null || context != PawnGenerationContext.PlayerStarter || !RimfolkPatches.On || !ScenarioStarts.ShapingLive) return true;
            var s = ScenarioStarts.Shaping;
            if (s == __instance) return true;
            try
            {
                ScenarioStarts.NewPawnGenerating(s, pawn, context);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("scenario start: a person being made", e);
            }
            return false;
        }
    }

    /// <summary>A starting colonist made in the newcomers' window: the chosen scenario's shaping parts (a forced trait, naked, a need's level).</summary>
    [HarmonyPatch(typeof(Scenario), nameof(Scenario.Notify_PawnGenerated))]
    public static class Patch_ScenarioStartShapeDone
    {
        public static bool Prefix(Scenario __instance, Pawn pawn, PawnGenerationContext context, bool redressed)
        {
            if (ScenarioStarts.Shaping == null || context != PawnGenerationContext.PlayerStarter || !RimfolkPatches.On || !ScenarioStarts.ShapingLive) return true;
            var s = ScenarioStarts.Shaping;
            if (s == __instance) return true;
            try
            {
                ScenarioStarts.PawnGenerated(s, pawn, context, redressed);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("scenario start: a person made", e);
            }
            return false;
        }
    }

    /// <summary>Whether a starting colonist made in the newcomers' window may be one: the chosen scenario's (its ages, a hediff they must survive).</summary>
    [HarmonyPatch(typeof(Scenario), nameof(Scenario.AllowPlayerStartingPawn))]
    public static class Patch_ScenarioStartShapeAllow
    {
        public static bool Prefix(Scenario __instance, Pawn pawn, bool tryingToRedress, PawnGenerationRequest req, ref bool __result)
        {
            if (ScenarioStarts.Shaping == null || req.Context != PawnGenerationContext.PlayerStarter || !RimfolkPatches.On || !ScenarioStarts.ShapingLive) return true;
            var s = ScenarioStarts.Shaping;
            if (s == __instance) return true;
            try
            {
                __result = ScenarioStarts.Allow(s, pawn, tryingToRedress, req);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("scenario start: whether a person may start", e);
                __result = true;
            }
            return false;
        }
    }
}
