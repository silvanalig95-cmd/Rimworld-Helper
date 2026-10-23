using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// During a clean sweep colonists clean before other work (Decrees/CleanSweepWork.cs). The game's work giver reads
    /// the colonist's work list once, through Pawn_WorkSettings.WorkGiversInOrderNormal; the transpiler sends that one
    /// read through <see cref="CleanSweepWork.WorkGivers"/>, with the colonist the giver was asked about. The getter
    /// itself is too small to patch safely (the game's runtime may copy it into its callers). If another game version
    /// reads the list some other way, the call isn't found and the work order stays the player's own.
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage))]
    public static class Patch_CleanSweepWork
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var list = instructions.ToList();
            var getter = AccessTools.PropertyGetter(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.WorkGiversInOrderNormal));
            var ours = AccessTools.Method(typeof(CleanSweepWork), nameof(CleanSweepWork.WorkGivers));
            var ps = original?.GetParameters();
            int pawnArg = ps == null ? -1 : Array.FindIndex(ps, p => p.ParameterType == typeof(Pawn));
            if (getter == null || ours == null || pawnArg < 0) return list;
            // A static method's arguments start at 0; an instance method's at 1 (this).
            int offset = original.IsStatic ? 0 : 1;
            bool found = false;
            var result = new List<CodeInstruction>(list.Count + 2);
            foreach (var ci in list)
            {
                if (ci.Calls(getter))
                {
                    // On the stack: the work settings. Add the colonist, and read the list through ours instead.
                    var loadPawn = CodeInstruction.LoadArgument(pawnArg + offset);
                    ci.MoveLabelsTo(loadPawn);
                    ci.MoveBlocksTo(loadPawn);
                    result.Add(loadPawn);
                    result.Add(new CodeInstruction(System.Reflection.Emit.OpCodes.Call, ours));
                    found = true;
                    continue;
                }
                result.Add(ci);
            }
            if (!found) Log.Warning("[Rimfolk] The game looks for work differently in this version, so a clean sweep doesn't put cleaning first.");
            return result;
        }
    }
}
