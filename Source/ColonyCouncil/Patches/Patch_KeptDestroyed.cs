using System;
using HarmonyLib;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Promises kept (Wishes/Kept.cs): a building a granted wish rests on (a guest bed, a telescope) that burns, or is
    /// wrecked in a raid or a collapse, wasn't taken away by the council, so its watch ends without blame. Only the
    /// destroyed thing's id is looked up, and only while something is being watched at all.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.Destroy))]
    public static class Patch_KeptDestroyed
    {
        public static void Prefix(Thing __instance, DestroyMode mode)
        {
            if (!KeptTracker.AnyThings || !RimfolkPatches.On) return;
            try
            {
                KeptTracker.Notify_Destroyed(__instance, mode);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("promises kept: a thing destroyed", e);
            }
        }
    }
}
