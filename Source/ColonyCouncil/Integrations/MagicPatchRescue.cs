using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A RimWorld of Magic's own patches under Harmony 3 (the player's logs, 7 and 17 November 2026). Two of its patch
    /// classes name a parameter by the wrong type: the healing rain (WeatherWorker_Patch, a postfix on
    /// WeatherWorker.WeatherTick, names its instance a WeatherManager) and a Wanderer's caravan travel
    /// (CostToMove_Caravan_Patch, a postfix on Caravan_PathFollower.CostToMove, names the start tile a PlanetLayer).
    /// Harmony 2 let that pass; Harmony 3 refuses both, and HugsLib's PatchAll for the mod stops at the first, so every one
    /// of its patch classes after it is never applied (gizmos, needs, damage, AI, jobs, caravans), with a red error in the
    /// log at every start.
    ///
    /// In HugsLib's place, for this mod only (<see cref="TryApplyInstead"/>): what HugsLib's ApplyHarmonyPatches does
    /// (HugsLib 12), with the mod's patch classes applied one at a time under its own Harmony id (HarmonyRescue) and those
    /// two left out, so no error comes. If HugsLib ran first after all, the same afterwards: each class not applied yet
    /// (<see cref="TryRescue"/>). Rimfolk then runs those two itself, each the mod's own postfix: the rain with the map's
    /// weather manager (its body only reads the map), the travel with no start layer (its body never reads the start;
    /// RWoM source, HarmonyPatches.cs). When the mod's patching goes through (Harmony 2, or a fixed version), nothing of it
    /// is missing and this does nothing. It restores the mod's own behaviour, so the deeper-hooks setting doesn't switch
    /// it off. The rescue was checked against the real Harmony 3.0.0-rc.3 outside the game with a stand-in of the mod's
    /// classes (HANDOFF).
    /// </summary>
    public static class MagicPatchRescue
    {
        private static bool looked, rescued;
        private static ModContentPack mod;
        private static HashSet<Assembly> assemblies;
        private static Type broken, caravanBroken;
        private static MethodInfo rainPostfix, caravanPostfix;
        private static Action<WeatherManager, Map> rain;

        private delegate void WandererCost(Caravan_PathFollower follower, Caravan caravan, PlanetLayer start, PlanetTile end, ref int result, int? ticksAbs);

        private static WandererCost wanderer;

        /// <summary>The healing rain runs through Rimfolk's hook (its own patch couldn't be applied).</summary>
        public static bool RainOurs { get; private set; }

        /// <summary>A Wanderer's caravan travel runs through Rimfolk's hook (its own patch couldn't be applied).</summary>
        public static bool CaravanOurs { get; private set; }

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
                foreach (var asm in assemblies)
                {
                    foreach (var t in SafeTypes(asm))
                    {
                        // The healing rain's patch: a WeatherWorker_Patch whose Postfix takes (WeatherManager, Map).
                        if (t.Name == "WeatherWorker_Patch")
                        {
                            var m = t.GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static);
                            var ps = m?.GetParameters();
                            if (ps == null || ps.Length != 2 || ps[0].ParameterType != typeof(WeatherManager) || ps[1].ParameterType != typeof(Map)) continue;
                            broken = t;
                            rainPostfix = m;
                        }
                        // A Wanderer's travel: a CostToMove_Caravan_Patch whose postfix takes the start tile as a PlanetLayer.
                        else if (t.Name == "CostToMove_Caravan_Patch")
                        {
                            var m = t.GetMethod("CostToMove_Caravan_Postfix", BindingFlags.Public | BindingFlags.Static);
                            var ps = m?.GetParameters();
                            if (ps == null || ps.Length != 6 || ps[0].ParameterType != typeof(Caravan_PathFollower) || ps[1].ParameterType != typeof(Caravan)
                                || ps[2].ParameterType != typeof(PlanetLayer) || ps[3].ParameterType != typeof(PlanetTile)
                                || ps[4].ParameterType != typeof(int).MakeByRefType() || ps[5].ParameterType != typeof(int?)) continue;
                            caravanBroken = t;
                            caravanPostfix = m;
                        }
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

        /// <summary>The mod's own Wanderer travel postfix, if it has the shape Harmony 3 refuses.</summary>
        public static MethodInfo CaravanMethod() => Present() ? caravanPostfix : null;

        public static bool IsTheirs(Type t) => t != null && Present() && assemblies.Contains(t.Assembly);

        public static void Rain(Map map) => rain?.Invoke(map.weatherManager, map);

        /// <summary>The mod's own postfix for a caravan's move cost: a caravan with a Wanderer crosses land without its difficulty.</summary>
        public static void WandererTravel(Caravan caravan, PlanetTile end, ref int result, int? ticksAbs) =>
            wanderer?.Invoke(null, caravan, null, end, ref result, ticksAbs);

        /// <summary>
        /// HugsLib's ApplyHarmonyPatches for this mod, done here instead (HugsLib 12: the HarmonyAutoPatch and
        /// ShouldHarmonyAutoPatch checks, HarmonyInst made with the mod's PackageIdPlayerFacing, then PatchAll), the classes
        /// applied one at a time and the refused ones left out. True when done here, so HugsLib's own step is skipped;
        /// false when nothing of the mod's patches is refused or anything isn't as expected, and HugsLib runs as before
        /// (with the rescue after it). Never throws.
        /// </summary>
        public static bool TryApplyInstead(object modBase)
        {
            try
            {
                return ApplyInstead(modBase);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("A RimWorld of Magic's patches: applying them in HugsLib's place", e);
                return rescued;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ApplyInstead(object modBase)
        {
            if (rescued || modBase == null || !Present() || broken == null && caravanBroken == null) return false;
            var baseType = AccessTools.TypeByName("HugsLib.ModBase");
            var controllerType = AccessTools.TypeByName("HugsLib.HugsLibController");
            if (baseType == null || controllerType == null || !baseType.IsInstanceOfType(modBase)) return false;
            var autoPatch = AccessTools.PropertyGetter(baseType, "HarmonyAutoPatch");
            var harmonyInst = AccessTools.Property(baseType, "HarmonyInst");
            var pack = AccessTools.PropertyGetter(baseType, "ModContentPack");
            var instance = AccessTools.PropertyGetter(controllerType, "Instance");
            var should = AccessTools.Method(controllerType, "ShouldHarmonyAutoPatch", new[] { typeof(Assembly), typeof(string) });
            if (autoPatch == null || harmonyInst?.GetSetMethod(true) == null || pack == null || instance == null || should == null) return false;
            // HugsLib does nothing either when the mod doesn't patch itself.
            if (!(autoPatch.Invoke(modBase, null) is bool auto) || !auto) return false;
            string id = (pack.Invoke(modBase, null) as ModContentPack)?.PackageIdPlayerFacing;
            object controller = instance.Invoke(null, null);
            if (string.IsNullOrEmpty(id) || id != mod.PackageIdPlayerFacing || controller == null) return false;
            // From here HugsLib's own bookkeeping is spoken for: after this answer it would skip the mod, so it is done here.
            if (!(should.Invoke(controller, new object[] { modBase.GetType().Assembly, id }) is bool go) || !go) return true;
            try
            {
                harmonyInst.SetValue(modBase, new Harmony(id));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("A RimWorld of Magic's patches: its Harmony instance", e);
            }
            Rescue(true, true);
            return true;
        }

        /// <summary>
        /// Once HugsLib has applied the mod's patches (<paramref name="afterHugsLib"/>), or later when its id already owns
        /// some: the missing ones one at a time. Never throws (it runs inside HugsLib's start).
        /// </summary>
        public static void TryRescue(bool afterHugsLib)
        {
            try
            {
                Rescue(afterHugsLib, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("A RimWorld of Magic's patches: applying the missing ones", e);
            }
        }

        // Kept apart so that a Harmony without one of these calls fails here, inside the callers' catch.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Rescue(bool afterHugsLib, bool instead)
        {
            if (rescued || !Present()) return;
            string id = mod.PackageIdPlayerFacing;
            if (string.IsNullOrEmpty(id)) return;
            // Before HugsLib has run, its id owns nothing yet: the postfix on its ApplyHarmonyPatches comes back.
            if (!afterHugsLib && HarmonyRescue.AppliedBy(id).Count == 0) return;
            rescued = true;
            var skip = new List<Type>();
            if (broken != null) skip.Add(broken);
            if (caravanBroken != null) skip.Add(caravanBroken);
            var (applied, failed) = HarmonyRescue.ApplyMissing(id, HarmonyRescue.PatchClasses(assemblies), skip);
            if (rainPostfix != null && !TheirsApplied(AccessTools.Method(typeof(WeatherWorker), nameof(WeatherWorker.WeatherTick)), rainPostfix))
            {
                rain = (Action<WeatherManager, Map>)Delegate.CreateDelegate(typeof(Action<WeatherManager, Map>), rainPostfix);
                RainOurs = true;
            }
            if (caravanPostfix != null && !TheirsApplied(Patch_MagicWandererTravel.TargetMethod(), caravanPostfix))
            {
                wanderer = (WandererCost)Delegate.CreateDelegate(typeof(WandererCost), caravanPostfix);
                CaravanOurs = true;
            }
            var ours = new List<string>();
            if (RainOurs) ours.Add("the healing rain");
            if (CaravanOurs) ours.Add("a Wanderer's caravan travel");
            string runs = ours.Count == 0 ? "" : ", and runs " + string.Join(" and ", ours) + " itself";
            if (instead)
                Log.Message("[Rimfolk] A RimWorld of Magic: this version of Harmony refuses " + (skip.Count == 1 ? "one of its patches" : skip.Count + " of its patches")
                            + ", and HugsLib's patching would stop there with an error. Rimfolk applied its other " + applied.Count + " patches one at a time" + runs + ".");
            else if (applied.Count > 0 || ours.Count > 0)
                Log.Message("[Rimfolk] A RimWorld of Magic: its patching stopped at a patch this version of Harmony refuses (the error above). Rimfolk applied "
                            + applied.Count + " of its patches that were left out, one at a time" + runs + ".");
            if (failed.Count > 0)
                Log.Warning("[Rimfolk] A RimWorld of Magic: " + failed.Count + " of its patches couldn't be applied either: " + string.Join("; ", failed));
        }

        private static bool TheirsApplied(MethodBase original, MethodInfo theirs)
        {
            if (original == null) return false;
            var info = Harmony.GetPatchInfo(original);
            return info?.Postfixes != null && info.Postfixes.Any(p => p?.PatchMethod == theirs);
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
    /// HugsLib applying a mod's patches (HugsLib.ModBase.ApplyHarmonyPatches, which logs and swallows the error): for A
    /// RimWorld of Magic, done in its place so the refused patches don't stop the rest (the prefix), or, if that couldn't
    /// be, the ones it left out afterwards (the postfix). Integrations/MagicPatchRescue.cs.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_MagicPatchRescue
    {
        public static bool Prepare() => MagicPatchRescue.Present() && TargetMethod() != null;

        public static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("HugsLib.ModBase"), "ApplyHarmonyPatches");

        public static bool Prefix(object __instance)
        {
            if (__instance == null || !MagicPatchRescue.IsTheirs(__instance.GetType())) return true;
            return !MagicPatchRescue.TryApplyInstead(__instance);
        }

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

    /// <summary>
    /// A RimWorld of Magic's Wanderer, when its own patch couldn't be applied (Integrations/MagicPatchRescue.cs): a caravan
    /// with a Wanderer (its TM_Wanderer trait) crosses land without the land's movement difficulty.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_MagicWandererTravel
    {
        public static bool Prepare() => MagicPatchRescue.CaravanMethod() != null && TargetMethod() != null;

        public static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(Caravan_PathFollower), "CostToMove", new[] { typeof(Caravan), typeof(PlanetTile), typeof(PlanetTile), typeof(int?) });

        public static void Postfix(Caravan caravan, PlanetTile end, ref int __result, int? ticksAbs)
        {
            if (!MagicPatchRescue.CaravanOurs || caravan == null) return;
            try
            {
                MagicPatchRescue.WandererTravel(caravan, end, ref __result, ticksAbs);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("A RimWorld of Magic's Wanderer travel", e);
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
