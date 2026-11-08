using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // Bed rest only when it makes sense (Health/BedRest.cs). The game's answer to "should they seek medical rest" is changed
    // where the game gives it, so everything that asks follows (going to bed for rest, staying in a medical bed, being fed
    // and visited there, meditating, which bed they sleep in): a postfix on HealthAIUtility.ShouldSeekMedicalRest. Not the
    // two HediffSet checks it uses: HasTendedAndHealingInjury also decides, every 600 ticks, whether tended wounds heal at
    // all, and HasImmunizableNotImmuneHediff whether a caravan gives the sick a bedroll, and both should stay as they are.

    /// <summary>The rule's answer in place of the game's.</summary>
    [HarmonyPatch(typeof(HealthAIUtility), nameof(HealthAIUtility.ShouldSeekMedicalRest))]
    public static class Patch_BedRest
    {
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (!BedRest.On) return;
            try
            {
                __result = BedRest.Verdict(pawn, __result);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("bed rest only when it makes sense: who rests", e);
            }
        }
    }

    /// <summary>
    /// The game tells of a full recovery ("Ada is fully healed") once a tended wound has healed and nothing more keeps them
    /// in bed. Under the rule a healing scratch no longer does, so the notice would come every 600 ticks while it heals: the
    /// one call there asks for the game's own answer. If it isn't found, the rule stays off.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.HealthTickInterval))]
    public static class Patch_BedRestHealedNotice
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            var game = AccessTools.Method(typeof(HealthAIUtility), nameof(HealthAIUtility.ShouldSeekMedicalRest), new[] { typeof(Pawn) });
            var ours = AccessTools.Method(typeof(BedRest), nameof(BedRest.GameRest), new[] { typeof(Pawn) });
            int found = 0;
            if (game != null && ours != null)
                foreach (var ci in list)
                    if (ci.Calls(game))
                    {
                        ci.operand = ours;
                        found++;
                    }
            BedRest.NoticeKept = found > 0;
            if (found == 0)
                Log.Warning("[Rimfolk] The game tells of a full recovery differently in this version, so bed rest only when it makes sense is off.");
            return list;
        }
    }

    /// <summary>The medical bed's "Use medical bed": the player may order rest for anything the game would rest for.</summary>
    [HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.GetBedRestFloatMenuOption))]
    public static class Patch_BedRestOrder
    {
        public static void Prefix() => BedRest.AskStart();

        public static Exception Finalizer(Exception __exception)
        {
            BedRest.AskEnd();
            return __exception;
        }
    }

    /// <summary>Each condition's tooltip on the health tab says whether it keeps them working or sends them to bed, after medicine by what is treated's line.</summary>
    [HarmonyPatch(typeof(Hediff), nameof(Hediff.GetTooltip))]
    [HarmonyPriority(Priority.Low)]
    public static class Patch_BedRestTooltip
    {
        public static void Postfix(Hediff __instance, ref string __result)
        {
            if (!BedRest.On) return;
            try
            {
                string line = BedRest.LineFor(__instance);
                if (line.NullOrEmpty()) return;
                if (__result.NullOrEmpty())
                {
                    __result = line;
                    return;
                }
                // Beside the medicine line when there is one, as one paragraph; else a paragraph of its own.
                string med = MedicineChoice.SettingOn ? MedicineChoice.LineFor(__instance) : null;
                string trimmed = __result.TrimEndNewlines();
                __result = trimmed + (!med.NullOrEmpty() && trimmed.EndsWith(med, StringComparison.Ordinal) ? "\n" : "\n\n") + line;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("bed rest only when it makes sense: the health tab's line", e);
            }
        }
    }

    /// <summary>A colonist resting in bed for a condition says which ("resting: flu.") where the game says only "resting.".</summary>
    [HarmonyPatch(typeof(JobDriver_LayDown), nameof(JobDriver_LayDown.GetReport))]
    public static class Patch_BedRestReport
    {
        public static void Postfix(JobDriver_LayDown __instance, ref string __result)
        {
            if (!BedRest.On) return;
            try
            {
                string r = BedRest.ReportFor(__instance.pawn, __result);
                if (!r.NullOrEmpty()) __result = r;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("bed rest only when it makes sense: what a resting colonist is doing", e);
            }
        }
    }
}
