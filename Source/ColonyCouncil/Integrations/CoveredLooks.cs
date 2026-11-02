using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Alpha Genes' looks that others feel without a word said: the repulsive gene's "disgusting colonist" and angelic
    /// beauty's awe. Each is an aura of the Vanilla Expanded Framework (a hediff's HediffComp_ThoughtEffecter) that gives
    /// its thought to everyone within six cells every few seconds. What nobody can see gives them nothing to react to:
    /// while the carrier wears something over the whole head and clothes over the body and legs (a burka, a full helmet
    /// with clothes on), the aura is still. Found by name; without Alpha Genes or the framework nothing changes.
    /// </summary>
    public static class CoveredLooks
    {
        /// <summary>The looks' thoughts (the aura's thoughtDef) that a covering hides.</summary>
        public static readonly HashSet<string> Auras = new HashSet<string> { "AG_Disgust", "AG_Awe" };

        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.coveredLooks ?? true);

        /// <summary>The pawn's face and body are out of sight: the whole head under one piece, and the body and legs clothed.</summary>
        public static bool Covered(Pawn p)
        {
            var worn = p?.apparel?.WornApparel;
            if (worn == null || worn.Count == 0) return false;
            bool head = false, torso = false, legs = false;
            foreach (var a in worn)
            {
                var groups = a?.def?.apparel?.bodyPartGroups;
                if (groups == null) continue;
                if (groups.Contains(BodyPartGroupDefOf.FullHead)) head = true;
                if (groups.Contains(BodyPartGroupDefOf.Torso)) torso = true;
                if (groups.Contains(BodyPartGroupDefOf.Legs)) legs = true;
            }
            return head && torso && legs;
        }
    }

    /// <summary>A covered carrier's looks reach nobody (Integrations/CoveredLooks.cs).</summary>
    [HarmonyPatch]
    public static class Patch_CoveredLooks
    {
        private static FieldInfo thoughtField;

        public static bool Prepare()
        {
            if (!ModsConfig.IsActive("sarg.alphagenes")) return false;
            var comp = AccessTools.TypeByName("VEF.AnimalBehaviours.HediffComp_ThoughtEffecter");
            var props = AccessTools.TypeByName("VEF.AnimalBehaviours.HediffCompProperties_ThoughtEffecter");
            thoughtField = props != null ? AccessTools.Field(props, "thoughtDef") : null;
            return comp != null && thoughtField != null && thoughtField.FieldType == typeof(string) && TargetMethod() != null;
        }

        public static MethodBase TargetMethod() =>
            AccessTools.Method(AccessTools.TypeByName("VEF.AnimalBehaviours.HediffComp_ThoughtEffecter"), "CompPostTickInterval");

        public static bool Prefix(HediffComp __instance)
        {
            if (!CoveredLooks.On || __instance?.props == null) return true;
            try
            {
                if (!(thoughtField.GetValue(__instance.props) is string thought) || !CoveredLooks.Auras.Contains(thought)) return true;
                return !CoveredLooks.Covered(__instance.parent?.pawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("covered looks", e);
                return true;
            }
        }
    }
}
