using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Rimfolk's changes to how the game itself behaves, through Harmony. Each patch class is applied on its own: if
    /// one can't be applied (the game or another mod changed that method), it's skipped with one warning and the rest
    /// still work. The "deeper game hooks" setting switches their effects off without a restart.
    /// </summary>
    public static class RimfolkPatches
    {
        public static readonly List<string> Applied = new List<string>();
        public static readonly List<string> Failed = new List<string>();

        /// <summary>Whether the patches should do anything right now.</summary>
        public static bool On => ColonyCouncilMod.Settings?.deepHooks ?? true;

        public static void ApplyAll()
        {
            Harmony harmony;
            try
            {
                harmony = new Harmony(ColonyCouncilMod.PackageId);
            }
            catch (Exception e)
            {
                Log.Warning("[Rimfolk] Harmony isn't available, so Rimfolk's deeper game hooks are off: " + e.Message);
                return;
            }
            var types = typeof(RimfolkPatches).Assembly.GetTypes()
                .Where(t => t.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0)
                .OrderBy(t => t.Name);
            foreach (var type in types)
            {
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                    Applied.Add(type.Name);
                }
                catch (Exception e)
                {
                    Failed.Add(type.Name);
                    Log.Warning("[Rimfolk] Couldn't apply " + type.Name + ", so that small feature is off: " + e.Message);
                }
            }
        }
    }
}
