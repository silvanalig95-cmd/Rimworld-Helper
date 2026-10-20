using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Something big finished while others were at the building work beside the builder: a memory of building it
    /// together (TalkHooks.Built). The frame's map, place and work are read before it turns into the building.
    /// </summary>
    [HarmonyPatch(typeof(Frame), nameof(Frame.CompleteConstruction))]
    public static class Patch_TalkBuilt
    {
        public static void Prefix(Frame __instance, out (Map map, IntVec3 pos, ThingDef def, float work) __state)
        {
            __state = default;
            if (!RimfolkPatches.On || !Persona.On || __instance == null || !__instance.Spawned) return;
            try
            {
                var def = __instance.def?.entityDefToBuild as ThingDef;
                if (def != null) __state = (__instance.Map, __instance.Position, def, __instance.WorkToBuild);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("built together hook", e);
            }
        }

        public static void Postfix(Pawn worker, (Map map, IntVec3 pos, ThingDef def, float work) __state)
        {
            if (__state.def == null || worker?.Faction != Faction.OfPlayer) return;
            TalkHooks.Built(worker, __state.map, __state.pos, __state.def, __state.work);
        }
    }

    /// <summary>Someone tended while they lay downed: the doctor and the patient remember it (TalkHooks.Tended).</summary>
    [HarmonyPatch(typeof(TendUtility), nameof(TendUtility.DoTend))]
    public static class Patch_TalkTended
    {
        public static void Postfix(Pawn doctor, Pawn patient)
        {
            if (!RimfolkPatches.On || !Persona.On || doctor == null || patient == null || doctor == patient) return;
            if (doctor.Faction != Faction.OfPlayer || !patient.Downed) return;
            TalkHooks.Tended(doctor, patient);
        }
    }
}
