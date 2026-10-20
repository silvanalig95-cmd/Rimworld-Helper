using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        /// <summary>Why each failed patch class couldn't be applied (for "Rimfolk: check myself").</summary>
        public static readonly Dictionary<string, string> FailedWhy = new Dictionary<string, string>();
        /// <summary>Why no patch was tried at all (Harmony missing, the classes couldn't be listed), or null.</summary>
        public static string NotStarted;

        /// <summary>Whether the patches should do anything right now.</summary>
        public static bool On => ColonyCouncilMod.Settings?.deepHooks ?? true;

        private static bool applied;

        public static void ApplyAll()
        {
            if (applied) return;
            applied = true;
            Harmony harmony;
            try
            {
                harmony = new Harmony(ColonyCouncilMod.PackageId);
            }
            catch (Exception e)
            {
                Log.Warning("[Rimfolk] Harmony isn't available, so Rimfolk's deeper game hooks are off: " + e.Message);
                NotStarted = "Harmony isn't available: " + e.Message;
                return;
            }
            List<Type> types;
            try
            {
                types = typeof(RimfolkPatches).Assembly.GetTypes()
                    .Where(t => t.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0)
                    .OrderBy(t => t.Name).ToList();
            }
            catch (Exception e)
            {
                Log.Warning("[Rimfolk] Couldn't list Rimfolk's game hooks, so they are off: " + e.Message);
                NotStarted = "couldn't list the hooks: " + e.Message;
                return;
            }
            foreach (var type in types)
            {
                try
                {
                    var done = harmony.CreateClassProcessor(type).Patch();
                    // Nothing patched: the class's Prepare found the game's method other than it expects and stood down.
                    if ((done == null || done.Count == 0) && type.GetMethod("Prepare", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) != null)
                    {
                        Failed.Add(type.Name);
                        FailedWhy[type.Name] = "skipped: the game's method isn't the one it expects in this version";
                        continue;
                    }
                    Applied.Add(type.Name);
                }
                catch (Exception e)
                {
                    Failed.Add(type.Name);
                    FailedWhy[type.Name] = (e.InnerException ?? e).Message;
                    Log.Warning("[Rimfolk] Couldn't apply " + type.Name + ", so that small feature is off: " + e.Message);
                }
            }
        }
    }

    /// <summary>
    /// Applies the game hooks once every def has loaded. Patching a method runs its class's static setup, and some of
    /// the game's classes build tables from defs there (PregnancyUtility keys one by pawn relations). Patched from the
    /// mod's constructor, before the defs exist, that setup failed for the rest of the session: no pawn could be
    /// generated, so world generation stopped after the first faction.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RimfolkPatchesStartup
    {
        static RimfolkPatchesStartup()
        {
            try
            {
                RimfolkPatches.ApplyAll();
            }
            catch (Exception e)
            {
                Log.Warning("[Rimfolk] The deeper game hooks couldn't start (is Harmony loaded before Rimfolk?): " + e.Message);
            }
        }
    }
}
