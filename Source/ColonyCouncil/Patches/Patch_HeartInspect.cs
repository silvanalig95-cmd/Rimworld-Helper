using HarmonyLib;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One short line under a colonist's inspect string: "Strain: worn · restless, proud · lacks belonging". Only for
    /// the colony's own people with a heart (Heart/), with the feature and its inspector setting on. The line is built
    /// when the heart changes (hourly), so this adds nothing per frame but a string join. Its tooltip comes from
    /// Patch_HeartInspectTip.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
    public static class Patch_HeartInspect
    {
        public static void Postfix(Pawn __instance, ref string __result)
        {
            if (!RimfolkPatches.On) return;
            var s = ColonyCouncilMod.Settings;
            if (s == null || !s.heart || !s.heartInspector || !__instance.IsFreeColonist) return;
            try
            {
                var h = Heart.ForView(__instance);
                string line = h?.inspectLine;
                if (line.NullOrEmpty()) return;
                __result = __result.NullOrEmpty() ? line : __result + "\n" + line;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("heart inspect line", e);
            }
        }
    }
}
