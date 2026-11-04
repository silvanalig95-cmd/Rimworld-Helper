using System;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A trading post defeated by the colony's people (the game's <c>SettlementDefeatUtility.CheckDefeated</c>, which puts
    /// its destroyed settlement in its place and takes the town away): its people remember a post razed, a heavy wrong,
    /// and its record ends (World/Region/TradingPost.cs). Notes it only; nothing of the game's own is changed.
    /// </summary>
    [HarmonyPatch(typeof(SettlementDefeatUtility), nameof(SettlementDefeatUtility.CheckDefeated))]
    public static class Patch_TradingPostRazed
    {
        public static void Prefix(Settlement factionBase, out (int id, Faction people, string name) __state)
        {
            __state = (-1, null, null);
            if (factionBase == null || factionBase.Faction == null || factionBase.Faction.IsPlayer || !RimfolkPatches.On) return;
            try
            {
                if (TradingPosts.PostAt(factionBase.ID, out _) == null) return;
                __state = (factionBase.ID, factionBase.Faction, factionBase.HasName ? factionBase.Name : null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trading posts: a post attacked", e);
            }
        }

        public static void Postfix(Settlement factionBase, (int id, Faction people, string name) __state)
        {
            if (__state.id < 0 || factionBase == null || !factionBase.Destroyed) return;
            try
            {
                TradingPosts.Razed(__state.id, __state.people, __state.name);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trading posts: a post razed", e);
            }
        }
    }
}
