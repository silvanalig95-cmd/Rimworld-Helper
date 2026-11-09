using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Role clothes the colony can reach (Faith/RoleClothes.cs), around the game's own choice of a role's clothes:
    /// Precept_Role.GenerateNewApparelRequirements(FactionDef generatingFor) draws from Precept_Role.AllPossibleRequirements,
    /// first without and then, only if it is still short, with its "desperate" flag. This class marks the choice as the
    /// colony's while it runs (the player's faction is the one it is made for, or the ideoligion is the colony's);
    /// Patch_RoleClothesPool leaves what is out of reach out of the first draw only. The factions' own ideoligions are
    /// left as the game makes them: the colony's research says nothing about what they can make.
    /// <para>
    /// Stands down safely: <see cref="Prepare"/> checks the method is there as expected, or nothing is patched and the game
    /// chooses as before; the finalizer puts the mark back whatever happens; without this class the pool's patch never
    /// acts, since nothing marks a choice.
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(Precept_Role), nameof(Precept_Role.GenerateNewApparelRequirements))]
    public static class Patch_RoleClothesChoose
    {
        /// <summary>Whether the game's method was found as expected.</summary>
        public static bool Found;

        public static bool Prepare()
        {
            try
            {
                var m = AccessTools.Method(typeof(Precept_Role), nameof(Precept_Role.GenerateNewApparelRequirements), new[] { typeof(FactionDef) });
                Found = m != null && !m.IsStatic && m.DeclaringType == typeof(Precept_Role) && m.ReturnType == typeof(List<PreceptApparelRequirement>);
            }
            catch
            {
                Found = false;
            }
            if (!Found) Log.Message("[Rimfolk] The game doesn't choose a role's clothes the way Rimfolk expects in this version, so role clothes the colony can reach is off.");
            return Found;
        }

        public static void Prefix(Precept_Role __instance, FactionDef generatingFor, out Ideo __state)
        {
            // Kept to put back afterwards (a choice inside another leaves the outer one as it was).
            __state = RoleClothes.choosingFor;
            try
            {
                RoleClothes.choosingFor = RoleClothes.On && RoleClothes.ForColony(__instance, generatingFor) ? __instance.ideo : null;
            }
            catch (Exception e)
            {
                RoleClothes.choosingFor = null;
                CouncilLog.ErrorOnce("role clothes: whose role", e);
            }
        }

        public static void Finalizer(Ideo __state)
        {
            RoleClothes.choosingFor = __state;
        }
    }

    /// <summary>
    /// The first draw of a role's clothes for the colony (Precept_Role.AllPossibleRequirements, not desperate) without what
    /// is out of reach (Faith/RoleClothesMath.cs). The game's desperate draw, and its editor's list (which asks for the
    /// desperate pool), are left whole, so a role is never left short and the player can still pick anything.
    /// </summary>
    [HarmonyPatch(typeof(Precept_Role), nameof(Precept_Role.AllPossibleRequirements))]
    public static class Patch_RoleClothesPool
    {
        /// <summary>Whether the game's method was found as expected.</summary>
        public static bool Found;

        public static bool Prepare()
        {
            try
            {
                var m = AccessTools.Method(typeof(Precept_Role), nameof(Precept_Role.AllPossibleRequirements), new[] { typeof(Ideo), typeof(PreceptDef), typeof(bool) });
                var ps = m?.GetParameters();
                Found = m != null && m.IsStatic && m.ReturnType == typeof(List<PreceptApparelRequirement>) && ps.Length == 3
                        && ps[0].Name == "ideo" && ps[1].Name == "def" && ps[2].Name == "desperate";
            }
            catch
            {
                Found = false;
            }
            if (!Found) Log.Message("[Rimfolk] The game's pool of role clothes isn't the way Rimfolk expects in this version, so role clothes the colony can reach is off.");
            return Found;
        }

        public static void Postfix(Ideo ideo, PreceptDef def, bool desperate, List<PreceptApparelRequirement> __result)
        {
            if (desperate || __result == null || __result.Count == 0 || ideo == null) return;
            if (RoleClothes.choosingFor != ideo || !RoleClothes.On) return;
            try
            {
                RoleClothes.FilterPool(ideo, def, __result);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("role clothes: the pool", e);
            }
        }
    }
}
