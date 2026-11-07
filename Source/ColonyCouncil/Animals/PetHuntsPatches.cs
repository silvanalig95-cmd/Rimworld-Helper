using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// One of the colony's animals looking for food (JobGiver_GetFood): with nothing found in its allowed area, a hunter
    /// goes after prey by Rimfolk's rules (Animals/PetHunts.cs); the game's own last-resort hunt for a flagged predator is
    /// replaced by the same choice; an animal kept home never hunts. Food the game found always stands. Off with the
    /// setting "Pets hunt for themselves", the game's own behaviour is untouched.
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_GetFood), "TryGiveJob")]
    public static class Patch_PetHuntsHungry
    {
        private static bool broken;
        private static readonly AccessTools.FieldRef<JobGiver_GetFood, HungerCategory> MinCategory = Field<HungerCategory>("minCategory");
        private static readonly AccessTools.FieldRef<JobGiver_GetFood, float> MaxLevel = Field<float>("maxLevelPercentage");

        private static AccessTools.FieldRef<JobGiver_GetFood, T> Field<T>(string name)
        {
            try
            {
                return AccessTools.FieldRefAccess<JobGiver_GetFood, T>(name);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("pets hunting: the game's field JobGiver_GetFood." + name, e);
                return null;
            }
        }

        /// <summary>It wants to eat now, as the game's food search itself asks: past its kind's wish to eat, and within what this node looks after.</summary>
        private static bool Wants(JobGiver_GetFood giver, Pawn pawn)
        {
            var food = pawn.needs?.food;
            if (food == null || food.CurLevelPercentage >= pawn.RaceProps.FoodLevelPercentageWantEat) return false;
            if (MinCategory != null && food.CurCategory < MinCategory(giver)) return false;
            return MaxLevel == null || food.CurLevelPercentage <= MaxLevel(giver);
        }

        public static void Postfix(JobGiver_GetFood __instance, Pawn pawn, ref Job __result)
        {
            if (broken || !RimfolkPatches.On || pawn == null || pawn.Faction != Faction.OfPlayer || pawn.RaceProps == null || !pawn.RaceProps.Animal) return;
            if (__result != null && __result.def != JobDefOf.PredatorHunt) return;
            if (!PetHunts.On || !pawn.Spawned) return;
            try
            {
                if (__result == null && !Wants(__instance, pawn)) return;
                __result = PetHunts.Hungry(pawn, __result);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("pets hunting: the hungry hunt", e);
            }
        }
    }

    /// <summary>A wild animal killed by one of the colony's animals on a hunt (only once it is really dead: another mod may stop a death).</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_PetHuntsKill
    {
        public static void Postfix(Pawn __instance, DamageInfo? dinfo)
        {
            if (!RimfolkPatches.On || !PetHunts.On || __instance == null || !__instance.Dead || __instance.Faction != null) return;
            if (!(dinfo?.Instigator is Pawn hunter) || hunter.Faction != Faction.OfPlayer || hunter.RaceProps == null || !hunter.RaceProps.Animal) return;
            if (__instance.RaceProps == null || !__instance.RaceProps.Animal) return;
            try
            {
                PetHunts.Notify_Killed(__instance, hunter);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("pets hunting: the kill", e);
            }
        }
    }

    /// <summary>The "May hunt" button on each of the colony's animals whose kind hunts.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_PetHuntsGizmo
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (var g in __result) yield return g;
            if (!RimfolkPatches.On) yield break;
            Gizmo c = null;
            try
            {
                c = PetHunts.GizmoFor(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("pets hunting: the button", e);
            }
            if (c != null) yield return c;
        }
    }

    /// <summary>
    /// An alert while one of the colony's animals is hungry, has nothing it may eat inside its allowed area and can't hunt
    /// for it (or may not): the game's own alert list, never a letter or a message. It names each one and says why (the area
    /// leaves out where the food is, the food inside it is forbidden, or there is nothing it eats on the map) and how to put it
    /// right; a click selects them. Looked at in the wild's short pass (Animals/PetHunts.cs), from the second look in a row so a
    /// moment between meals never shows; the penned and hitched have the game's own alerts. The setting "An animal with no
    /// food in reach" (How you're told, Alerts) turns it off.
    /// </summary>
    public class Alert_NoFoodInReach : Alert
    {
        private readonly List<Pawn> culprits = new List<Pawn>();

        public Alert_NoFoodInReach()
        {
            defaultLabel = "CC_NoFood_Alert".Translate();
            defaultPriority = AlertPriority.Medium;
        }

        public override string GetLabel() =>
            culprits.Count > 1 ? "CC_NoFood_AlertMany".Translate(culprits.Count).Resolve() : "CC_NoFood_Alert".Translate().Resolve();

        public override TaggedString GetExplanation()
        {
            var parts = new List<string> { "CC_NoFood_AlertWhat".Translate().Resolve(), string.Join("\n", PetHunts.StrandedLines) };
            var fixes = new List<string>();
            foreach (var why in new[] { NoFoodWhy.Area, NoFoodWhy.Forbidden, NoFoodWhy.None })
                foreach (var w in PetHunts.StrandedWhys)
                    if (w == why)
                    {
                        fixes.Add(("CC_NoFood_Fix_" + why).Translate().Resolve());
                        break;
                    }
            if (fixes.Count > 0) parts.Add(string.Join("\n", fixes));
            return string.Join("\n\n", parts);
        }

        public override AlertReport GetReport()
        {
            culprits.Clear();
            if (!PetHunts.AlertOn) return AlertReport.Inactive;
            foreach (var a in PetHunts.Stranded)
                if (a != null && a.Spawned && !a.Dead)
                    culprits.Add(a);
            return culprits.Count == 0 ? AlertReport.Inactive : AlertReport.CulpritsAre(culprits);
        }
    }
}
