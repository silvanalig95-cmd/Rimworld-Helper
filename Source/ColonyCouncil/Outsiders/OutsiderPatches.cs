using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// A duel of honour or a fistfight after hard words (Outsiders/OutsiderOutcomes.cs) is fought between two who agreed
    /// to it, or came to blows over words: their people don't count each blow as the colony attacking one of theirs
    /// (the game's goodwill for an attacked member, which would soon turn them hostile). What their people think of it
    /// is Rimfolk's own: the honour of a duel, the memory of a brawl. A death is still the game's own matter.
    /// </summary>
    [HarmonyPatch(typeof(Faction), nameof(Faction.Notify_MemberTookDamage))]
    public static class Patch_OutsiderBoutGoodwill
    {
        public static bool Prefix(Pawn member, DamageInfo dinfo)
        {
            if (!RimfolkPatches.On) return true;
            try
            {
                return !Outsiders.InBout(member, dinfo.Instigator as Pawn);
            }
            catch
            {
                return true;
            }
        }
    }

    /// <summary>The same for their group on the map: a blow in a bout isn't the colony harming a guest (traders and visitors would leave fighting).</summary>
    [HarmonyPatch(typeof(Lord), nameof(Lord.Notify_PawnDamaged))]
    public static class Patch_OutsiderBoutLord
    {
        public static bool Prefix(Pawn victim, DamageInfo dinfo)
        {
            if (!RimfolkPatches.On) return true;
            try
            {
                return !Outsiders.InBout(victim, dinfo.Instigator as Pawn);
            }
            catch
            {
                return true;
            }
        }
    }
}
