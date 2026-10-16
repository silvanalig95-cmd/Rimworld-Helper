using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The game's own moments that setbacks follow (Setbacks/SetbackEvents.cs). Each only reads what just happened and
    // hands it on; nothing in the game's own outcome changes. With the deeper game hooks off, these setbacks never come
    // up. Each is applied on its own (RimfolkPatches), so one the game or another mod changed is simply skipped.

    /// <summary>A colonist finished building something: who built it is remembered (if it's of note), and its quality minded.</summary>
    [HarmonyPatch(typeof(Frame), nameof(Frame.CompleteConstruction))]
    public static class Patch_SetbackBuilt
    {
        public class Site
        {
            public Map map;
            public IntVec3 cell;
            public ThingDef def;
        }

        public static void Prefix(Frame __instance, out Site __state)
        {
            __state = null;
            try
            {
                if (RimfolkPatches.On && __instance?.Map != null && __instance.def?.entityDefToBuild is ThingDef td)
                    __state = new Site { map = __instance.Map, cell = __instance.Position, def = td };
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setback built (before)", e);
            }
        }

        public static void Postfix(Pawn worker, Site __state)
        {
            if (__state?.map == null || worker == null) return;
            try
            {
                var list = __state.map.thingGrid.ThingsListAtFast(__state.cell);
                for (int i = 0; i < list.Count; i++)
                    if (list[i].def == __state.def)
                    {
                        Setbacks.Notify_Built(worker, list[i]);
                        return;
                    }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setback built", e);
            }
        }
    }

    /// <summary>A construction failed (materials wasted).</summary>
    [HarmonyPatch(typeof(Frame), nameof(Frame.FailConstruction))]
    public static class Patch_SetbackBuildFailed
    {
        public static void Postfix(Frame __instance, Pawn worker)
        {
            if (!RimfolkPatches.On || worker == null) return;
            try
            {
                Setbacks.Notify_BuildFailed(worker, __instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setback build failed", e);
            }
        }
    }

    /// <summary>A building is about to be destroyed: wrecked (by damage or fire) or taken down. Only a lookup unless one of ours built it.</summary>
    [HarmonyPatch(typeof(Building), nameof(Building.Destroy))]
    public static class Patch_SetbackBuildingGone
    {
        public static void Prefix(Building __instance, DestroyMode mode)
        {
            if (!RimfolkPatches.On || (mode != DestroyMode.KillFinalize && mode != DestroyMode.Deconstruct)) return;
            try
            {
                Setbacks.Notify_Gone(__instance, mode);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setback building gone", e);
            }
        }
    }

    /// <summary>A record went up: sowing is noted for the field it was sown in.</summary>
    [HarmonyPatch(typeof(Pawn_RecordsTracker), nameof(Pawn_RecordsTracker.Increment))]
    public static class Patch_SetbackSown
    {
        public static void Postfix(Pawn_RecordsTracker __instance, RecordDef def)
        {
            if (def != RecordDefOf.PlantsSown || !RimfolkPatches.On) return;
            try
            {
                Setbacks.Notify_Sown(__instance?.pawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setback sown", e);
            }
        }
    }

    /// <summary>A sown crop caught the blight.</summary>
    [HarmonyPatch(typeof(Plant), nameof(Plant.CropBlighted))]
    public static class Patch_SetbackBlight
    {
        public static void Prefix(Plant __instance)
        {
            if (!RimfolkPatches.On || __instance == null || !__instance.sown || __instance.Blighted) return;
            try
            {
                Setbacks.Notify_CropLost(__instance, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setback blight", e);
            }
        }
    }

    /// <summary>A sown crop that can't survive losing its leaves was caught by the cold.</summary>
    [HarmonyPatch(typeof(Plant), nameof(Plant.MakeLeafless))]
    public static class Patch_SetbackFrost
    {
        public static void Prefix(Plant __instance, Plant.LeaflessCause cause)
        {
            if (!RimfolkPatches.On || cause != Plant.LeaflessCause.Cold || __instance == null || !__instance.sown) return;
            try
            {
                if (__instance.def?.plant == null || !__instance.def.plant.dieIfLeafless || __instance.LeaflessNow) return;
                Setbacks.Notify_CropLost(__instance, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setback frost", e);
            }
        }
    }

    /// <summary>Something came off a workbench: its quality is minded by a maker who cares about the craft.</summary>
    [HarmonyPatch(typeof(GenRecipe), "PostProcessProduct")]
    public static class Patch_SetbackMade
    {
        public static void Postfix(Thing __result, RecipeDef recipeDef, Pawn worker)
        {
            if (!RimfolkPatches.On || __result == null || worker == null) return;
            try
            {
                Setbacks.Notify_Made(worker, __result, recipeDef?.workSkill);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setback made", e);
            }
        }
    }

    /// <summary>A bill was finished: done at a makeshift spot when a proper bench could have been built?</summary>
    [HarmonyPatch(typeof(GenRecipe), nameof(GenRecipe.MakeRecipeProducts))]
    public static class Patch_SetbackBillDone
    {
        public static void Prefix(RecipeDef recipeDef, Pawn worker, IBillGiver billGiver)
        {
            if (!RimfolkPatches.On || worker == null || !(billGiver is Thing bench)) return;
            try
            {
                Setbacks.Notify_BillDone(worker, recipeDef, bench);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setback bill", e);
            }
        }
    }

    /// <summary>An operation failed in a colonist's hands.</summary>
    [HarmonyPatch(typeof(Recipe_Surgery), "CheckSurgeryFail")]
    public static class Patch_SetbackSurgery
    {
        public static void Postfix(bool __result, Pawn surgeon, Pawn patient)
        {
            if (!__result || !RimfolkPatches.On) return;
            try
            {
                Setbacks.Notify_SurgeryFailed(surgeon, patient);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setback surgery", e);
            }
        }
    }

    /// <summary>Sold to a trader: a work of art (things with comps).</summary>
    [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.PreTraded))]
    public static class Patch_SetbackSoldThing
    {
        public static void Prefix(ThingWithComps __instance, TradeAction action, ITrader trader)
        {
            if (action != TradeAction.PlayerSells || !RimfolkPatches.On) return;
            try
            {
                Setbacks.Notify_Sold(__instance, trader);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setback sold", e);
            }
        }
    }

    /// <summary>Sold to a trader: a work of art packed up for moving.</summary>
    [HarmonyPatch(typeof(MinifiedThing), nameof(MinifiedThing.PreTraded))]
    public static class Patch_SetbackSoldMinified
    {
        public static void Prefix(MinifiedThing __instance, TradeAction action, ITrader trader)
        {
            if (action != TradeAction.PlayerSells || !RimfolkPatches.On) return;
            try
            {
                Setbacks.Notify_Sold(__instance, trader);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setback sold", e);
            }
        }
    }

    /// <summary>Sold to a trader: an animal one of ours looked after.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreTraded))]
    public static class Patch_SetbackSoldAnimal
    {
        public static void Prefix(Pawn __instance, TradeAction action, ITrader trader)
        {
            if (action != TradeAction.PlayerSells || !RimfolkPatches.On || __instance?.RaceProps == null || !__instance.RaceProps.Animal) return;
            try
            {
                Setbacks.Notify_Sold(__instance, trader);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setback sold", e);
            }
        }
    }

    /// <summary>A meal eaten: out in the cold or the rain?</summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]
    public static class Patch_SetbackMeal
    {
        public static void Postfix(Thing __instance, Pawn ingester)
        {
            if (!RimfolkPatches.On || ingester == null || ingester.Faction != Faction.OfPlayer || !ingester.RaceProps.Humanlike) return;
            try
            {
                Setbacks.Notify_Ate(ingester, __instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setback meal", e);
            }
        }
    }
}
