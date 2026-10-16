using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Deeds Rimfolk counts that the game doesn't (Defs/Misc/Council_Records.xml): quarrels mended, trades struck,
    /// secrets kept, eulogies given. They show in the game's Records tab, and names are built on them.
    /// </summary>
    public static class CouncilRecords
    {
        public const string QuarrelsMended = "Council_QuarrelsMended";
        public const string TradesMade = "Council_TradesMade";
        public const string SecretsKept = "Council_SecretsKept";
        public const string EulogiesGiven = "Council_EulogiesGiven";
        // Beyond the walls (Defs/Misc/Rimfolk_Records_World.xml).
        public const string CampEvenings = "Council_CampEvenings";
        public const string CampsGreeted = "Council_CampsGreeted";
        public const string DaysKept = "Council_DaysKept";
        public const string WorldCallsAnswered = "Council_WorldCallsAnswered";

        /// <summary>One more of this deed for a colonist (quietly nothing if the record or the colonist isn't there).</summary>
        public static void Add(Pawn p, string record)
        {
            if (p?.records == null || !p.RaceProps.Humanlike) return;
            var def = DefDatabase<RecordDef>.GetNamedSilentFail(record);
            if (def == null) return;
            try
            {
                p.records.Increment(def);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("record " + record, e);
            }
        }
    }

    /// <summary>
    /// A trade went through: one more deal for whoever negotiated it (a name for the colony's best trader). Only counts;
    /// the trade itself is the game's own.
    /// </summary>
    [HarmonyPatch(typeof(TradeDeal), nameof(TradeDeal.TryExecute))]
    public static class Patch_Traded
    {
        public static void Postfix(bool __result, ref bool actuallyTraded)
        {
            if (!RimfolkPatches.On || !__result || !actuallyTraded) return;
            var negotiator = TradeSession.playerNegotiator;
            if (negotiator == null || negotiator.Faction != Faction.OfPlayer || TradeSession.giftMode) return;
            CouncilRecords.Add(negotiator, CouncilRecords.TradesMade);
        }
    }
}
