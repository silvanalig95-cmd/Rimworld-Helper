using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A seat's mark in the health tab ("speaker of the council", "a careful reeve") says the seat by its name in the
    /// colony's age, as every other place that names a seat does (Offices/OfficeWords.cs).
    /// </summary>
    [HarmonyPatch(typeof(Hediff), nameof(Hediff.LabelBase), MethodType.Getter)]
    public static class Patch_OfficeHediffLabel
    {
        private static HashSet<HediffDef> marks;

        private static HashSet<HediffDef> Marks
        {
            get
            {
                if (marks != null) return marks;
                var set = new HashSet<HediffDef>();
                foreach (var o in DefDatabase<OfficeDef>.AllDefsListForReading)
                {
                    if (o.hediff != null) set.Add(o.hediff);
                    if (o.colonyHediff != null) set.Add(o.colonyHediff);
                }
                marks = set;
                return marks;
            }
        }

        public static void Postfix(Hediff __instance, ref string __result)
        {
            if (!RimfolkPatches.On || __instance?.def == null || __result.NullOrEmpty() || !Marks.Contains(__instance.def)) return;
            __result = OfficeNames.InAge(__result);
        }
    }
}
