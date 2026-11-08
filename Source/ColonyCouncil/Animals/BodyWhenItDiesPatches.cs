using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A marked animal's death carried over to its body (Animals/BodyWhenItDies.cs). The game's Pawn.Kill puts the body down
    /// (forbidding it outside the home area unless a hunter killed it or it was marked for hunting), and only after that,
    /// destroying the animal, removes every order on it; so the prefix reads the mark while it is still there and the
    /// postfix, once the animal is really dead (another mod may stop a death), unforbids the body and gives it the haul order.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_BodyWhenItDiesKill
    {
        public static void Prefix(Pawn __instance, out Map __state)
        {
            __state = null;
            if (!RimfolkPatches.On || __instance == null) return;
            try
            {
                __state = BodyWhenItDies.MarkedMap(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the body when it dies: before the kill", e);
            }
        }

        public static void Postfix(Pawn __instance, Map __state)
        {
            if (__state == null || __instance == null || !__instance.Dead) return;
            try
            {
                BodyWhenItDies.Died(__instance, __state);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the body when it dies: the kill", e);
            }
        }
    }

    /// <summary>The button "Take the body when it dies" on a bleeding animal that isn't the colony's.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_BodyWhenItDiesGizmo
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (var g in __result) yield return g;
            if (!RimfolkPatches.On) yield break;
            Gizmo c = null;
            try
            {
                c = BodyWhenItDies.GizmoFor(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the body when it dies: the button", e);
            }
            if (c != null) yield return c;
        }
    }
}
