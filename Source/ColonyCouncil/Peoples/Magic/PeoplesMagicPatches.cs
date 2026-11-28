using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The mod's spells kept out of the peoples' hands (PeoplesMagicMath.KeptOut; Peoples/Magic/PeoplesMagic.cs): A RimWorld of
    /// Magic's own casting for those not of the colony asks TM_Calc.HasResourcesForAbility before it casts by itself (its
    /// "autocast" for them), and an ability's CanCastPowerCheck before its decision tree for fights (its aggressive casting)
    /// and before a cast asked for. For a pawn of Rimfolk's peoples not of the colony and a spell kept out, both say no, so
    /// the mod's AI picks another. Everything else the mod's AI and settings decide. Off with the deeper game hooks, and
    /// without the mod (nothing to patch).
    /// </summary>
    [HarmonyPatch]
    public static class Patch_PeoplesMagicKeptOut
    {
        public static bool Prepare() => TargetMethod() != null;

        public static MethodBase TargetMethod()
        {
            var t = AccessTools.TypeByName("TorannMagic.TM_Calc");
            return t == null ? null : AccessTools.Method(t, "HasResourcesForAbility");
        }

        public static bool Prefix(Pawn __0, Def __1, ref bool __result)
        {
            if (!RimfolkPatches.On) return true;
            try
            {
                if (!PeoplesMagic.KeptFrom(__0, __1)) return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' magic: a spell kept out", e);
                return true;
            }
            __result = false;
            return false;
        }
    }

    /// <summary>The same guard on the mod's own check before a spell or technique is cast (TorannMagic.MagicAbility, MightAbility).</summary>
    [HarmonyPatch]
    public static class Patch_PeoplesMagicKeptOutCheck
    {
        public static bool Prepare() => TargetMethods().Any();

        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string name in new[] { "TorannMagic.MagicAbility", "TorannMagic.MightAbility" })
            {
                var t = AccessTools.TypeByName(name);
                var m = t == null ? null : AccessTools.DeclaredMethod(t, "CanCastPowerCheck");
                if (m != null && m.ReturnType == typeof(bool) && m.GetParameters().Length == 2) yield return m;
            }
        }

        public static bool Prefix(object __instance, ref bool __result, ref string __1)
        {
            if (!RimfolkPatches.On) return true;
            try
            {
                var (pawn, def) = RomSpells.Of(__instance);
                if (!PeoplesMagic.KeptFrom(pawn, def)) return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' magic: a spell kept out", e);
                return true;
            }
            __1 = "";
            __result = false;
            return false;
        }
    }

    /// <summary>
    /// A spell Rimfolk asks of a visitor in peace (Peoples/Magic/PeoplesMagicWatch.cs) is the colony's request, cast by the
    /// mod's own job for those not of the colony. A RimWorld of Magic (Continued) on RimWorld 1.7 checks such a job as its
    /// own automatic casting (AbilityCastSafety.IsAutomaticCast, then CanAutocast): someone not of the colony may cast that
    /// way only in a fight, and only from where they already stand, so every healing, cure and rain asked of a visitor
    /// ended before it began. For that one cast, while Rimfolk follows it, the check reads it as asked for, as it reads a
    /// cast the player orders: the visitor walks into range by the mod's own job and pays with their own mana. Off with the
    /// deeper game hooks, and without that check (the original mod, the continuation on 1.6): there the casts were never
    /// held back.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_PeoplesMagicPeaceCast
    {
        private static bool looked;
        private static MethodBase target;

        public static bool Prepare() => TargetMethod() != null;

        public static string StandsDownWhy() => "A RimWorld of Magic (Continued)'s check of its own automatic casts isn't there (the original mod, or 1.6)";

        public static MethodBase TargetMethod()
        {
            if (looked) return target;
            looked = true;
            var t = AccessTools.TypeByName("TorannMagic.Compatibility.AbilityCastSafety");
            var m = t == null ? null : AccessTools.Method(t, "IsAutomaticCast", new[] { typeof(Verb) });
            target = m != null && m.ReturnType == typeof(bool) ? m : null;
            return target;
        }

        /// <summary>A visitor's spell in peace can be cast: the mod holds no such check, or Rimfolk's hook on it is in place.</summary>
        public static bool Open => TargetMethod() == null || RimfolkPatches.On && RimfolkPatches.Applied.Contains(nameof(Patch_PeoplesMagicPeaceCast));

        public static void Postfix(Verb __0, ref bool __result)
        {
            if (!__result || !RimfolkPatches.On) return;
            try
            {
                if (PeoplesMagicWatch.Asked(__0?.CasterPawn)) __result = false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' magic: a spell asked in peace", e);
            }
        }
    }
}
