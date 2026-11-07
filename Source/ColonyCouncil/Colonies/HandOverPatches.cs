using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// "Hand it over" on a home's world buttons, beside the game's own Abandon (Colonies/HandOvers.cs): a postfix on the
    /// settlement's gizmos, for a player settlement that is one of the colony's homes; greyed with why when it can't be.
    /// </summary>
    [HarmonyPatch(typeof(Settlement), nameof(Settlement.GetGizmos))]
    public static class Patch_HandOverGizmo
    {
        public static void Postfix(Settlement __instance, ref IEnumerable<Gizmo> __result)
        {
            if (!RimfolkPatches.On || !HandOvers.On || __instance == null || __instance.Faction != Faction.OfPlayer || !__instance.HasMap) return;
            var map = __instance.Map;
            if (!Homes.IsHome(map) || HandOvers.At(map) != null) return;
            __result = __result.Concat(new[] { Button(map) });
        }

        private static Gizmo Button(Map map)
        {
            var c = new Command_Action
            {
                defaultLabel = "CC_HandOver_Act".Translate(),
                defaultDesc = "CC_HandOver_ActTip".Translate(),
                icon = Icons.Colony,
                action = () => HandOvers.Open(map)
            };
            string why = HandOvers.WhyNot(map);
            if (why != null) c.Disable(why);
            return c;
        }
    }

    /// <summary>
    /// A people the colony became (Rimfolk's own, Colonies/HandOverPeoples.cs) keeps its own heads: while it is being
    /// made the game makes no stranger to lead it (the head leads); when its head dies or is lost, the head's second leads
    /// (HandOvers.NextLeader), so the game's own letter of the new leader names them. With nobody of theirs left, the
    /// game makes its own as for any people. A people arising during play (Peoples/Arising.cs) gets no leader while the
    /// game makes it either: its leader comes once its faith is set, or is the one who gathered it.
    /// </summary>
    [HarmonyPatch(typeof(Faction), nameof(Faction.TryGenerateNewLeader))]
    public static class Patch_HandOverLeader
    {
        public static bool Prefix(Faction __instance, ref bool __result)
        {
            try
            {
                if (Arising.Making && Peoples.IsOurs(__instance))
                {
                    __instance.leader = null;
                    __result = false;
                    return false;
                }
                if (!HandOverPeoples.IsOurs(__instance)) return true;
                if (HandOverPeoples.Making)
                {
                    __instance.leader = null;
                    __result = false;
                    return false;
                }
                if (!RimfolkPatches.On) return true;
                var next = HandOvers.NextLeader(__instance);
                if (next == null) return true;
                __instance.leader = next;
                __result = true;
                return false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hand-over: the people's next leader", e);
                return true;
            }
        }
    }

    /// <summary>
    /// The gravship takes off without a grav anchor from a home being handed over (Odyssey): once the ceremony is over and
    /// those who stay are on the ground, the town is made instead of the game's abandon (Colonies/HandOvers.cs), for that
    /// map only; otherwise the game abandons it as always.
    /// </summary>
    [HarmonyPatch(typeof(GravshipUtility), nameof(GravshipUtility.AbandonMap))]
    public static class Patch_HandOverGravship
    {
        public static bool Prefix(Map map)
        {
            try
            {
                if (!RimfolkPatches.On || map == null || HandOvers.At(map) == null) return true;
                return !HandOvers.TakesOverAbandon(map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hand-over: the gravship's takeoff", e);
                return true;
            }
        }
    }
}
