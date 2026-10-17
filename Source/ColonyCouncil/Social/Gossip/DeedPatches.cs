using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// A memory given to someone about another (rescued me, harmed me, cheated on me, broke up with me, botched my
    /// surgery): a deed by the other, when a DeedDef lists that memory (Social/Gossip/Deeds.cs). One dictionary look per
    /// memory given.
    /// </summary>
    [HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory), new[] { typeof(Thought_Memory), typeof(Pawn) })]
    public static class Patch_DeedMemory
    {
        public static void Postfix(MemoryThoughtHandler __instance, Thought_Memory newThought, Pawn otherPawn)
        {
            if (!RimfolkPatches.On || newThought?.def == null || !Persona.On) return;
            try
            {
                Deeds.Notify_Memory(__instance?.pawn, newThought.def, otherPawn ?? newThought.otherPawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("deeds: memory", e);
            }
        }
    }

    /// <summary>
    /// A colonist starting a mental break: a tantrum, a berserk fit, running from a fight, a binge while food is short.
    /// Whether they were drafted is read before the break undrafts them (running with a weapon in hand counts for more).
    /// </summary>
    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    public static class Patch_DeedBreak
    {
        public static void Prefix(Pawn ___pawn, out bool __state)
        {
            __state = false;
            try
            {
                __state = ___pawn?.Drafted ?? false;
            }
            catch
            {
                // Some pawns have no drafter.
            }
        }

        public static void Postfix(bool __result, Pawn ___pawn, MentalStateDef stateDef, Pawn otherPawn, bool causedByPsycast, bool __state)
        {
            // A psycast's rage (a berserk pulse) is nobody's own doing.
            if (!__result || causedByPsycast || !RimfolkPatches.On || !Persona.On) return;
            try
            {
                Deeds.Notify_Break(___pawn, stateDef, otherPawn, __state);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("deeds: break", e);
            }
        }
    }

    /// <summary>A history event the game records with a colonist as its doer (an execution, an organ taken from someone held).</summary>
    [HarmonyPatch(typeof(HistoryEventsManager), nameof(HistoryEventsManager.RecordEvent))]
    public static class Patch_DeedHistory
    {
        public static void Postfix(HistoryEvent historyEvent)
        {
            if (!RimfolkPatches.On || historyEvent.def == null || !Persona.On) return;
            try
            {
                Deeds.Notify_History(historyEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("deeds: history event", e);
            }
        }
    }

    /// <summary>A social fight starting: the one whose tracker starts it threw the first punch.</summary>
    [HarmonyPatch(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.StartSocialFight))]
    public static class Patch_DeedSocialFight
    {
        public static void Postfix(Pawn ___pawn, Pawn otherPawn)
        {
            if (!RimfolkPatches.On || !Persona.On) return;
            try
            {
                Deeds.Notify_SocialFight(___pawn, otherPawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("deeds: social fight", e);
            }
        }
    }
}
