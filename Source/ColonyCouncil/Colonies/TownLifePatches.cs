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
    /// A town the colony was, its map open while the colony's fighters stand with it against a raid (Colonies/TownRaid.cs):
    /// the game counts a town on its own map "defeated" when nobody of its people is a threat to the colony, which a town at
    /// peace with the colony never is, and would destroy it at once. A guarded prefix on the game's defeat check, for a town
    /// the colony was that is not at war with the colony only; at war (the colony attacking it), the game decides as always.
    /// </summary>
    [HarmonyPatch(typeof(SettlementDefeatUtility), nameof(SettlementDefeatUtility.CheckDefeated))]
    public static class Patch_TownRaidNotDefeated
    {
        public static bool Prefix(Settlement factionBase)
        {
            if (factionBase == null || !factionBase.HasMap || !RimfolkPatches.On) return true;
            try
            {
                var f = factionBase.Faction;
                if (f == null || f.IsPlayer || f.HostileTo(Faction.OfPlayer) || !HandOverPeoples.IsOurs(f)) return true;
                return WorldLedger.FormerOf(factionBase.ID) == null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("town life: a town at peace on its own map", e);
                return true;
            }
        }
    }

    /// <summary>
    /// A raid of a people a colony became, at war with the colony (release 5, Colonies/TownLife.cs): the game may redress
    /// one of the colony's former colonists, a world pawn of that people, into the raiders; the raid's letter names them.
    /// A postfix on the game's raid letter, for these peoples only.
    /// </summary>
    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "GetLetterText")]
    public static class Patch_TownRaidFormers
    {
        public static void Postfix(IncidentParms parms, List<Pawn> pawns, ref string __result)
        {
            if (!RimfolkPatches.On || parms?.faction == null || pawns == null || !HandOverPeoples.IsOurs(parms.faction)) return;
            try
            {
                var rec = WorldLedger.FormerOf(parms.faction);
                if (rec == null || !TownLife.On) return;
                var ours = pawns.Where(p => p != null && rec.formers.Contains(p)).ToList();
                if (ours.Count == 0) return;
                string town = TownsAfter.TownName(rec);
                __result += "\n\n" + (ours.Count == 1 ? "CC_TownLife_RaidFormer" : "CC_TownLife_RaidFormers").Translate(Foundings.Names(ours).Named("NAMES"), town.Named("TOWN"),
                    (rec.colonyName ?? town).Named("COLONY")).Resolve();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("town life: a former colonist among the raiders", e);
            }
        }
    }
}
