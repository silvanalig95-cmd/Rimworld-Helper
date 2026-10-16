using System;
using HarmonyLib;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A lucky thing says whose it is, or was, under its inspect text: "Macy's lucky charm", "Once Hector's lucky charm"
    /// (Quirks/LuckyItems.cs). The game asks every frame, so this is a dictionary lookup by thing id, and nothing at
    /// all while no thing is lucky. Apparel and weapons reach this through their own inspect strings.
    /// </summary>
    [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.GetInspectString))]
    public static class Patch_LuckyInspect
    {
        public static void Postfix(ThingWithComps __instance, ref string __result)
        {
            if (!LuckyItems.Any || !RimfolkPatches.On || __instance is Pawn) return;
            try
            {
                string line = LuckyItems.InspectLine(__instance);
                if (line.NullOrEmpty()) return;
                __result = __result.NullOrEmpty() ? line : __result.TrimEndNewlines() + "\n" + line;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("lucky things: inspect", e);
            }
        }
    }

    /// <summary>
    /// A lucky thing's tooltip in the gear tab (and anywhere else the game shows a thing's detailed description): whose
    /// lucky charm it is, and whose it was.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.DescriptionDetailed), MethodType.Getter)]
    public static class Patch_LuckyDescriptionDetailed
    {
        public static void Postfix(Thing __instance, ref string __result)
        {
            if (!LuckyItems.Any || !RimfolkPatches.On) return;
            LuckyItems.AppendDescription(__instance, ref __result);
        }
    }

    /// <summary>A lucky thing's info card description: whose lucky charm it is, and whose it was.</summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.DescriptionFlavor), MethodType.Getter)]
    public static class Patch_LuckyDescriptionFlavor
    {
        public static void Postfix(Thing __instance, ref string __result)
        {
            if (!LuckyItems.Any || !RimfolkPatches.On) return;
            LuckyItems.AppendDescription(__instance, ref __result);
        }
    }

    /// <summary>
    /// The same for things with comps (apparel and weapons), whose description adds their comps' parts to the plain
    /// one. Added only if it isn't there already, so it shows once either way.
    /// </summary>
    [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.DescriptionFlavor), MethodType.Getter)]
    public static class Patch_LuckyDescriptionFlavorComps
    {
        public static void Postfix(ThingWithComps __instance, ref string __result)
        {
            if (!LuckyItems.Any || !RimfolkPatches.On) return;
            LuckyItems.AppendDescription(__instance, ref __result);
        }
    }

    /// <summary>
    /// A lucky thing destroyed (burnt, smelted, worn to rags) takes its memories with it, and whoever counted it as lucky
    /// may settle on another of its kind. Only the destroyed thing's id is looked up, and only while any thing is lucky.
    /// Bookkeeping, so it runs even with the deeper hooks switched off.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.Destroy))]
    public static class Patch_LuckyDestroyed
    {
        public static void Postfix(Thing __instance)
        {
            if (!LuckyItems.Any || __instance == null || !__instance.Destroyed || __instance is Pawn) return;
            try
            {
                LuckyItems.Notify_Destroyed(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("lucky things: a thing destroyed", e);
            }
        }
    }
}
