using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The game's health events for HealthTracker (convalescence, doctors and midwives). Each class is its own small hook:
    // one that can't be applied is skipped alone (RimfolkPatches), and the "deeper game hooks" setting switches them off.

    /// <summary>A colony doctor tended someone: who, and whom.</summary>
    [HarmonyPatch(typeof(TendUtility), nameof(TendUtility.DoTend))]
    public static class Patch_HealthTended
    {
        public static void Postfix(Pawn doctor, Pawn patient)
        {
            if (!RimfolkPatches.On || doctor == null || patient == null || doctor == patient) return;
            try
            {
                HealthTracker.Instance?.Notify_Tended(doctor, patient);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("health: tending hook", e);
            }
        }
    }

    /// <summary>
    /// An operation's own check for failure, which every surgery of the game (and most mods') makes before it does
    /// anything: false means it went well. While it runs, a death on the table is the operation's.
    /// </summary>
    [HarmonyPatch(typeof(Recipe_Surgery), "CheckSurgeryFail")]
    public static class Patch_HealthSurgery
    {
        public static void Prefix(Pawn patient)
        {
            HealthTracker.underKnife = patient;
        }

        public static void Postfix(Recipe_Surgery __instance, bool __result, Pawn surgeon, Pawn patient, BodyPartRecord part, Bill bill)
        {
            HealthTracker.underKnife = null;
            if (!RimfolkPatches.On) return;
            try
            {
                HealthTracker.Instance?.Notify_Surgery(surgeon, patient, __instance?.recipe ?? bill?.recipe, part, __result);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("health: surgery hook", e);
            }
        }

        /// <summary>Whatever happened in the check, nobody is on the table any more.</summary>
        public static Exception Finalizer(Exception __exception)
        {
            HealthTracker.underKnife = null;
            return __exception;
        }
    }

    /// <summary>
    /// One of the colony's people dying of a condition (not killed outright): the doctor who tended them in the last day
    /// lost a patient. Checked before (they're still ours) and confirmed after (another mod may have stopped it).
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_HealthLost
    {
        public static void Prefix(Pawn __instance, Hediff exactCulprit, out Pawn __state)
        {
            __state = null;
            if (!RimfolkPatches.On || __instance == null || __instance.Dead || exactCulprit == null) return;
            try
            {
                __state = HealthTracker.Instance?.DoctorToBlame(__instance, exactCulprit);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("health: death hook", e);
            }
        }

        public static void Postfix(Pawn __instance, Hediff exactCulprit, Pawn __state)
        {
            if (__state == null || __instance == null || !__instance.Dead) return;
            try
            {
                HealthTracker.Instance?.AfterDeath(__instance, __state, exactCulprit);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("health: death hook", e);
            }
        }
    }

    /// <summary>
    /// The game's own visit to a sickbed (a friend sitting with someone laid up): when it ends, HealthTracker hears how
    /// long it lasted.
    /// </summary>
    [HarmonyPatch(typeof(JobDriver_VisitSickPawn), "MakeNewToils")]
    public static class Patch_HealthVisit
    {
        public static void Postfix(JobDriver_VisitSickPawn __instance)
        {
            if (!RimfolkPatches.On || __instance?.job == null) return;
            try
            {
                var visitor = __instance.pawn;
                var patient = __instance.job.targetA.Thing as Pawn;
                if (visitor == null || patient == null) return;
                int start = Find.TickManager.TicksGame;
                __instance.AddFinishAction(condition =>
                {
                    if (condition == JobCondition.Errored) return;
                    try
                    {
                        HealthTracker.Instance?.Notify_VisitEnded(visitor, patient, Find.TickManager.TicksGame - start);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("health: visit hook", e);
                    }
                });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("health: visit hook", e);
            }
        }
    }

    /// <summary>
    /// A child born (Biotech's birth, however it came about): whoever attended as the doctor delivered it. The same
    /// method WorldReactions hears births from; this only adds the midwife.
    /// </summary>
    [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.ApplyBirthOutcome))]
    public static class Patch_HealthMidwife
    {
        public static void Postfix(Thing __result, Pawn geneticMother, Pawn doctor)
        {
            if (!RimfolkPatches.On || doctor == null) return;
            try
            {
                HealthTracker.Instance?.Notify_Birth(__result, geneticMother, doctor);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("health: birth hook", e);
            }
        }
    }
}
