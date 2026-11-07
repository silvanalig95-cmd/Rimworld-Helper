using System;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // Medicine by what is treated (Health/MedicineChoice.cs). Without Smart Medicine, Rimfolk chooses the medicine itself
    // (the first four classes); with it, Rimfolk fills in Smart Medicine's per-injury choice right before every choice of medicine
    // (the four Patch_SmartMedicine classes). The caravan's packs and the health tab's line work with either.

    /// <summary>The serious are tended first, then the minor, then the harmless; within a kind the game's own order.</summary>
    [HarmonyPatch(typeof(Hediff), nameof(Hediff.TendPriority), MethodType.Getter)]
    public static class Patch_MedicineTendOrder
    {
        public static bool Prepare() => !SmartMedicineBridge.Present;
        public static string StandsDownWhy() => SmartMedicineBridge.Present ? "Smart Medicine is loaded: it orders the tendings by its per-injury care, which Rimfolk fills in" : null;

        public static void Postfix(Hediff __instance, ref float __result)
        {
            if (!MedicineChoice.OwnOn || !MedicineChoice.Cared(__instance.pawn)) return;
            try
            {
                __result += MedicineChoiceMath.PriorityShift(MedicineChoice.Judge(__instance, out _));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("medicine by what is treated: the order of tending", e);
            }
        }
    }

    /// <summary>
    /// The medicine the doctor goes for is the one the first tending gets: the game's own search again with that care when
    /// its pick is better than the tending gets, none when it gets no medicine.
    /// </summary>
    [HarmonyPatch(typeof(HealthAIUtility), nameof(HealthAIUtility.FindBestMedicine))]
    public static class Patch_MedicineFirstTending
    {
        private static bool again;

        public static bool Prepare() => !SmartMedicineBridge.Present;
        public static string StandsDownWhy() => SmartMedicineBridge.Present ? "Smart Medicine is loaded: it finds the medicine by its per-injury care, which Rimfolk fills in" : null;

        public static void Postfix(Pawn healer, Pawn patient, bool onlyUseInventory, ref Thing __result)
        {
            if (again || !MedicineChoice.OwnOn || !MedicineChoice.Cared(patient)) return;
            try
            {
                int care = MedicineChoice.NextTendingCare(patient, true);
                // A serious condition coming due within the hour: the medicine fetched serves it too, should it come due on the
                // way (the tendings before it are done without, MedicineChoiceMath.WithCarried).
                if (care >= 0)
                {
                    int soon = MedicineChoice.SoonCare(patient, MedicineChoiceMath.SoonTicks);
                    if (soon > care) care = soon;
                }
                // Remembered for the job: a tending that comes due later and gets better ends it (MedicineChoice.AfterTending).
                MedicineChoice.Chose(healer, patient, care);
                if (__result == null || care < 0 || MedicineChoiceMath.Allows(care, MedicineChoice.Needs(__result.def))) return;
                if (care < MedicineChoiceMath.Herbal)
                {
                    __result = null;
                    return;
                }
                var settings = patient.playerSettings;
                var own = settings.medCare;
                again = true;
                try
                {
                    // The game's own search (the doctor's pockets, the stores, the pack animals), with the care this tending gets.
                    settings.medCare = (MedicalCareCategory)care;
                    __result = HealthAIUtility.FindBestMedicine(healer, patient, onlyUseInventory);
                }
                finally
                {
                    settings.medCare = own;
                    again = false;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("medicine by what is treated: the medicine for a tending", e);
            }
        }

        /// <summary>The game's search is running again for a lesser care: the other patches leave it to the game.</summary>
        public static bool Again => again;
    }

    /// <summary>The doctor takes only as many of the medicine as the tendings that get it, not one for every wound.</summary>
    [HarmonyPatch(typeof(Medicine), nameof(Medicine.GetMedicineCountToFullyHeal))]
    public static class Patch_MedicineCount
    {
        private static bool counting;

        public static bool Prepare() => !SmartMedicineBridge.Present;
        public static string StandsDownWhy() => SmartMedicineBridge.Present ? "Smart Medicine is loaded: it counts the medicine by its per-injury care, which Rimfolk fills in" : null;

        public static void Postfix(Pawn pawn, ref int __result)
        {
            if (counting || __result <= 0 || Patch_MedicineFirstTending.Again || !MedicineChoice.OwnOn || !MedicineChoice.Cared(pawn)) return;
            counting = true;
            try
            {
                // At least one while the game counts any: a job made before the patient changed still reserves its medicine
                // (the search itself finds none for a tending that gets none, Patch_MedicineFirstTending).
                int n = Math.Max(1, MedicineChoice.MedicineCount(pawn));
                if (n < __result) __result = n;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("medicine by what is treated: how much medicine", e);
            }
            finally
            {
                counting = false;
            }
        }
    }

    /// <summary>
    /// A tending whose conditions get less than the medicine the doctor carries is done without it (the medicine kept for
    /// the next); when the next tending gets other medicine than the doctor has in hand (a lesser one, or a better one
    /// that came due), the doctor goes for it first: the job ends after this tending, as the game's own self-tend does,
    /// and the next one fetches the right medicine.
    /// </summary>
    [HarmonyPatch(typeof(TendUtility), nameof(TendUtility.DoTend))]
    public static class Patch_MedicineAtTend
    {
        public static bool Prepare() => !SmartMedicineBridge.Present;
        public static string StandsDownWhy() => SmartMedicineBridge.Present ? "Smart Medicine is loaded: it tends by its per-injury care, which Rimfolk fills in" : null;

        public static void Prefix(Pawn doctor, Pawn patient, ref Medicine medicine)
        {
            if (medicine == null || medicine.Destroyed || !MedicineChoice.OwnOn || !MedicineChoice.Cared(patient)) return;
            // The game's own tend job, or a caravan's doctor; another mod's way of tending (a warcasket's dressing) is its own.
            if (doctor != null && doctor.Spawned && doctor.CurJob != null && doctor.CurJob.def != JobDefOf.TendPatient) return;
            try
            {
                int care = MedicineChoice.NextTendingCare(patient, true);
                if (care >= 0 && !MedicineChoiceMath.Allows(care, MedicineChoice.Needs(medicine.def))) medicine = null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("medicine by what is treated: a tending", e);
            }
        }

        public static void Postfix(Pawn doctor, Pawn patient)
        {
            if (doctor == null || !MedicineChoice.OwnOn || !MedicineChoice.Cared(patient)) return;
            try
            {
                MedicineChoice.AfterTending(doctor, patient, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("medicine by what is treated: after a tending", e);
            }
        }
    }

    /// <summary>In a caravan, the medicine from the packs is the one the first tending gets (with or without Smart Medicine).</summary>
    [HarmonyPatch(typeof(CaravanInventoryUtility), nameof(CaravanInventoryUtility.TryGetBestMedicine))]
    public static class Patch_MedicineCaravan
    {
        public static void Postfix(Caravan caravan, Pawn patient, ref Medicine medicine, ref Pawn owner, ref bool __result)
        {
            if (medicine == null || !MedicineChoice.SettingOn || !MedicineChoice.Cared(patient)) return;
            try
            {
                int care = MedicineChoice.NextTendingCare(patient, true);
                if (care < 0 || MedicineChoiceMath.Allows(care, MedicineChoice.Needs(medicine.def))) return;
                Medicine best = null;
                float bestPotency = -1f;
                if (care >= MedicineChoiceMath.Herbal)
                {
                    var items = CaravanInventoryUtility.AllInventoryItems(caravan);
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (!(items[i] is Medicine m) || !m.def.IsMedicine || !MedicineChoiceMath.Allows(care, MedicineChoice.Needs(m.def))) continue;
                        float potency = m.GetStatValue(StatDefOf.MedicalPotency);
                        if (potency > bestPotency)
                        {
                            bestPotency = potency;
                            best = m;
                        }
                    }
                }
                medicine = best;
                owner = best != null ? CaravanInventoryUtility.GetOwnerOf(caravan, best) : null;
                __result = best != null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("medicine by what is treated: a caravan's medicine", e);
            }
        }
    }

    /// <summary>Each condition's tooltip on the health tab says what medicine it gets and why.</summary>
    [HarmonyPatch(typeof(Hediff), nameof(Hediff.GetTooltip))]
    public static class Patch_MedicineTooltip
    {
        public static void Postfix(Hediff __instance, ref string __result)
        {
            if (!MedicineChoice.SettingOn) return;
            try
            {
                string line = MedicineChoice.LineFor(__instance);
                if (line.NullOrEmpty()) return;
                __result = __result.NullOrEmpty() ? line : __result.TrimEndNewlines() + "\n\n" + line;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("medicine by what is treated: the health tab's line", e);
            }
        }
    }

    // ================= With Smart Medicine: its per-injury choice filled in before each choice of medicine =================

    /// <summary>Before a doctor's tend job is made (Smart Medicine makes it in its own prefix, after this one).</summary>
    [HarmonyPatch(typeof(WorkGiver_Tend), nameof(WorkGiver_Tend.JobOnThing))]
    [HarmonyBefore(SmartMedicineBridge.HarmonyId)]
    [HarmonyPriority(Priority.First)]
    public static class Patch_SmartMedicineJob
    {
        public static bool Prepare() => SmartMedicineBridge.Ready;
        public static string StandsDownWhy() => SmartMedicineBridge.Ready ? null : "Smart Medicine isn't loaded, or isn't as Rimfolk knows it";

        public static void Prefix(Pawn pawn, Thing t)
        {
            if (!(t is Pawn p)) return;
            SmartMedicineBridge.Refresh(p);
            if (MedicineChoice.SettingOn && MedicineChoice.Cared(p)) MedicineChoice.Chose(pawn, p, MedicineChoice.NextTendingCare(p, true));
        }
    }

    /// <summary>Before every search for medicine (the doctor's next tending, a drafted tend).</summary>
    [HarmonyPatch(typeof(HealthAIUtility), nameof(HealthAIUtility.FindBestMedicine))]
    [HarmonyBefore(SmartMedicineBridge.HarmonyId)]
    [HarmonyPriority(Priority.First)]
    public static class Patch_SmartMedicineSearch
    {
        public static bool Prepare() => SmartMedicineBridge.Ready;
        public static string StandsDownWhy() => SmartMedicineBridge.Ready ? null : "Smart Medicine isn't loaded, or isn't as Rimfolk knows it";

        public static void Prefix(Pawn healer, Pawn patient)
        {
            SmartMedicineBridge.Refresh(patient);
            if (MedicineChoice.SettingOn && MedicineChoice.Cared(patient)) MedicineChoice.Chose(healer, patient, MedicineChoice.NextTendingCare(patient, true));
        }
    }

    /// <summary>
    /// Before each tending, so what one tending covers follows the patient as they are now; after it, the job ends when
    /// the next tending gets other medicine than the doctor has in hand, so Smart Medicine's next job fetches it.
    /// </summary>
    [HarmonyPatch(typeof(TendUtility), nameof(TendUtility.DoTend))]
    [HarmonyPriority(Priority.First)]
    public static class Patch_SmartMedicineTend
    {
        public static bool Prepare() => SmartMedicineBridge.Ready;
        public static string StandsDownWhy() => SmartMedicineBridge.Ready ? null : "Smart Medicine isn't loaded, or isn't as Rimfolk knows it";

        public static void Prefix(Pawn patient) => SmartMedicineBridge.Refresh(patient);

        public static void Postfix(Pawn doctor, Pawn patient)
        {
            if (doctor == null || !MedicineChoice.SettingOn || !MedicineChoice.Cared(patient)) return;
            try
            {
                MedicineChoice.AfterTending(doctor, patient, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("medicine by what is treated: after a tending with Smart Medicine", e);
            }
        }
    }

    /// <summary>When the health tab is drawn, so Smart Medicine's own icon shows each choice Rimfolk made.</summary>
    [HarmonyPatch(typeof(HealthCardUtility), nameof(HealthCardUtility.DrawPawnHealthCard))]
    public static class Patch_SmartMedicineHealthTab
    {
        public static bool Prepare() => SmartMedicineBridge.Ready;
        public static string StandsDownWhy() => SmartMedicineBridge.Ready ? null : "Smart Medicine isn't loaded, or isn't as Rimfolk knows it";

        public static void Postfix(Pawn pawn) => SmartMedicineBridge.Refresh(pawn);
    }
}
