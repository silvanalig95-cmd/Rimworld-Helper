using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A RimWorld of Magic's own patches under Harmony 3 (the player's log, 7 November 2026). Its patch for the healing
    /// rain (TorannMagic's WeatherWorker_Patch, a postfix on WeatherWorker.WeatherTick) names its instance a
    /// WeatherManager. Harmony 2 let that pass; Harmony 3 refuses it, and HugsLib's PatchAll for the mod stops there, so
    /// every one of its patch classes after that one is never applied (some eighty of its 144 in the 1.6 source: gizmos,
    /// needs, damage, AI, jobs, caravans). After HugsLib's try, this applies each of its patch classes not applied yet,
    /// one at a time under the mod's own Harmony id (HarmonyRescue), and runs the healing rain itself: the mod's own
    /// postfix, called with the map's weather manager (its body only reads the map). When the mod's patching went
    /// through (Harmony 2, or a fixed version), nothing of it is missing and this does nothing. It restores the mod's own
    /// behaviour, so the deeper-hooks setting doesn't switch it off. Checked against the real Harmony 3.0.0-rc.3 outside
    /// the game with a stand-in of the mod's classes (HANDOFF).
    /// </summary>
    public static class MagicPatchRescue
    {
        private static bool looked, rescued;
        private static ModContentPack mod;
        private static HashSet<Assembly> assemblies;
        private static Type broken;
        private static MethodInfo rainPostfix;
        private static Action<WeatherManager, Map> rain;

        /// <summary>The healing rain runs through Rimfolk's hook (its own patch couldn't be applied).</summary>
        public static bool RainOurs { get; private set; }

        public static bool Present()
        {
            if (looked) return mod != null;
            looked = true;
            try
            {
                if (!ModsConfig.IsActive(MagicGolems.PackageId)) return false;
                mod = LoadedModManager.RunningMods.FirstOrDefault(m => m?.PackageId == MagicGolems.PackageId);
                assemblies = new HashSet<Assembly>(mod?.assemblies?.loadedAssemblies ?? new List<Assembly>());
                if (assemblies.Count == 0)
                {
                    mod = null;
                    return false;
                }
                // The healing rain's patch: a WeatherWorker_Patch whose Postfix takes (WeatherManager, Map).
                foreach (var asm in assemblies)
                {
                    foreach (var t in SafeTypes(asm))
                    {
                        if (t.Name != "WeatherWorker_Patch") continue;
                        var m = t.GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static);
                        var ps = m?.GetParameters();
                        if (ps == null || ps.Length != 2 || ps[0].ParameterType != typeof(WeatherManager) || ps[1].ParameterType != typeof(Map)) continue;
                        broken = t;
                        rainPostfix = m;
                    }
                }
            }
            catch (Exception e)
            {
                mod = null;
                CouncilLog.ErrorOnce("A RimWorld of Magic's patches: looking for the mod", e);
            }
            return mod != null;
        }

        /// <summary>The mod's own healing-rain postfix, if it has the shape Harmony 3 refuses.</summary>
        public static MethodInfo RainMethod() => Present() ? rainPostfix : null;

        public static bool IsTheirs(Type t) => t != null && Present() && assemblies.Contains(t.Assembly);

        public static void Rain(Map map) => rain?.Invoke(map.weatherManager, map);

        /// <summary>
        /// Once HugsLib has applied the mod's patches (<paramref name="afterHugsLib"/>), or later when its id already owns
        /// some: the missing ones one at a time. Never throws (it runs inside HugsLib's start).
        /// </summary>
        public static void TryRescue(bool afterHugsLib)
        {
            try
            {
                Rescue(afterHugsLib);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("A RimWorld of Magic's patches: applying the missing ones", e);
            }
        }

        // Kept apart so that a Harmony without one of these calls fails here, inside TryRescue's catch.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Rescue(bool afterHugsLib)
        {
            if (rescued || !Present()) return;
            string id = mod.PackageIdPlayerFacing;
            if (string.IsNullOrEmpty(id)) return;
            // Before HugsLib has run, its id owns nothing yet: the postfix on its ApplyHarmonyPatches comes back.
            if (!afterHugsLib && HarmonyRescue.AppliedBy(id).Count == 0) return;
            rescued = true;
            var skip = broken != null ? new List<Type> { broken } : new List<Type>();
            var (applied, failed) = HarmonyRescue.ApplyMissing(id, HarmonyRescue.PatchClasses(assemblies), skip);
            if (rainPostfix != null && !TheirRainApplied())
            {
                rain = (Action<WeatherManager, Map>)Delegate.CreateDelegate(typeof(Action<WeatherManager, Map>), rainPostfix);
                RainOurs = true;
            }
            if (applied.Count > 0 || RainOurs)
                Log.Message("[Rimfolk] A RimWorld of Magic: its patching stopped at its healing-rain patch, which this Harmony refuses (the error above). Rimfolk applied "
                            + applied.Count + " of its patches that were left out, one at a time" + (RainOurs ? ", and runs the healing rain itself." : "."));
            if (failed.Count > 0)
                Log.Warning("[Rimfolk] A RimWorld of Magic: " + failed.Count + " of its patches couldn't be applied either: " + string.Join("; ", failed));
        }

        private static bool TheirRainApplied()
        {
            var info = Harmony.GetPatchInfo(AccessTools.Method(typeof(WeatherWorker), nameof(WeatherWorker.WeatherTick)));
            return info?.Postfixes != null && info.Postfixes.Any(p => p?.PatchMethod == rainPostfix);
        }

        private static IEnumerable<Type> SafeTypes(Assembly asm)
        {
            try
            {
                return asm.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }
    }

    /// <summary>
    /// After HugsLib applies a mod's patches (HugsLib.ModBase.ApplyHarmonyPatches, which logs and swallows the error):
    /// for A RimWorld of Magic, the ones it left out (Integrations/MagicPatchRescue.cs).
    /// </summary>
    [HarmonyPatch]
    public static class Patch_MagicPatchRescue
    {
        public static bool Prepare() => MagicPatchRescue.Present() && TargetMethod() != null;

        public static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("HugsLib.ModBase"), "ApplyHarmonyPatches");

        public static void Postfix(object __instance)
        {
            if (__instance != null && MagicPatchRescue.IsTheirs(__instance.GetType())) MagicPatchRescue.TryRescue(true);
        }
    }

    /// <summary>A RimWorld of Magic's healing rain, when its own patch couldn't be applied (Integrations/MagicPatchRescue.cs).</summary>
    [HarmonyPatch(typeof(WeatherWorker), nameof(WeatherWorker.WeatherTick))]
    public static class Patch_MagicHealingRain
    {
        public static bool Prepare() => MagicPatchRescue.RainMethod() != null;

        public static void Postfix(Map map)
        {
            if (!MagicPatchRescue.RainOurs || map?.weatherManager == null) return;
            try
            {
                MagicPatchRescue.Rain(map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("A RimWorld of Magic's healing rain", e);
            }
        }
    }

    /// <summary>If HugsLib applied the mod's patches before Rimfolk's hooks were in place: the rescue now.</summary>
    [StaticConstructorOnStartup]
    public static class MagicPatchRescueLate
    {
        static MagicPatchRescueLate()
        {
            if (MagicPatchRescue.Present()) MagicPatchRescue.TryRescue(false);
        }
    }
}
