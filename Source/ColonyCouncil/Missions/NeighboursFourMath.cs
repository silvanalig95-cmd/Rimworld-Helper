using System;

namespace ColonyCouncil
{
    /// <summary>How a store kept for a neighbour ends when their carts come back for it.</summary>
    public enum KeepEnd
    {
        /// <summary>All of it in the stores: they take it and pay the keeping.</summary>
        Kept,
        /// <summary>Short, and the colony pays for what is missing at its worth: no keeping paid.</summary>
        MadeGood,
        /// <summary>Short, and not enough silver to make it good: they take what there is and remember it.</summary>
        Short
    }

    /// <summary>
    /// The pure part of the fourth round of neighbours' requests (the player's request, 6 November 2026; the workers in
    /// Workers_MissionNeighbours4.cs): the missions' one draw, hands lent to a neighbour's town, a store kept for them, a
    /// young beast raised for them, and someone of theirs lost on the colony's land. No game types, so Tools/Tests can
    /// check it.
    /// </summary>
    public static class NeighboursFourMath
    {
        // ---------------- The missions' one draw ----------------

        /// <summary>
        /// What every mission offer weighed together in the events' draw before this round (thirteen kinds offered: 6.8 of
        /// base chance). New kinds take a share of it rather than adding to it: each mission's base chance is scaled by
        /// <see cref="PoolFactor"/> so the missions still weigh this much in all, in the storyteller's draw and in
        /// Rimfolk's own.
        /// </summary>
        public const float MissionPoolWeight = 6.8f;

        /// <summary>The factor on each mission's base chance when all of them weigh <paramref name="total"/>: never more than one.</summary>
        public static float PoolFactor(float total) => total <= MissionPoolWeight || total <= 0f ? 1f : MissionPoolWeight / total;

        // ---------------- Hands lent to a neighbour's town ----------------

        /// <summary>The share of the pay a stay earns: all of it for the whole stay, half of it for half the stay or more, else none.</summary>
        public static float HandsShare(int stayedTicks, int neededTicks)
        {
            if (neededTicks <= 0 || stayedTicks >= neededTicks) return 1f;
            return stayedTicks * 2 >= neededTicks ? 0.5f : 0f;
        }

        /// <summary>Silver for a share of the pay, rounded to tens, ten at least.</summary>
        public static int SharePay(int reward, float share) => Math.Max(10, (int)Math.Round(reward * share / 10f, MidpointRounding.AwayFromZero) * 10);

        /// <summary>Days the colony has, once it agrees: the road there and back, the stay, and three days to get ready.</summary>
        public static float HandsDays(float travelDays, int hours) => (float)Math.Ceiling(travelDays * 2f + hours / 24f + 3f);

        /// <summary>Enough of the colony's people who can do it, with someone left at home besides.</summary>
        public static bool CanSpare(int able, int adults, int hands) => hands > 0 && able >= hands && adults > hands;

        // ---------------- A store kept for them ----------------

        /// <summary>The keeping, as a share of what the goods are worth.</summary>
        public const float KeepFeeShare = 0.25f;

        /// <summary>What they pay for the keeping: a quarter of the goods' worth, rounded to tens, twenty at least.</summary>
        public static int KeepFee(float worth) => Math.Max(20, (int)Math.Round(Math.Max(0f, worth) * KeepFeeShare / 10f) * 10);

        /// <summary>Silver for what is missing, at its plain worth, rounded up to tens.</summary>
        public static int Shortfall(int missing, float unitValue) => missing <= 0 ? 0 : (int)Math.Ceiling(missing * Math.Max(0.01f, unitValue) / 10f) * 10;

        /// <summary>How it ends: all there, short and made good in silver, or short.</summary>
        public static KeepEnd KeepOutcome(int have, int count, int silver, int shortfall)
        {
            if (have >= count) return KeepEnd.Kept;
            return silver >= shortfall ? KeepEnd.MadeGood : KeepEnd.Short;
        }

        // ---------------- A young beast raised for them ----------------

        /// <summary>Malnutrition at this or more, and the beast is not kept well.</summary>
        public const float BeastThin = 0.2f;

        /// <summary>Well kept: fed, not sick, no wound left untended.</summary>
        public static bool KeptWell(float malnutrition, bool sick, bool untended) => malnutrition < BeastThin && !sick && !untended;

        /// <summary>The share of the pay: all of it kept well, half of it kept poorly.</summary>
        public static float BeastShare(bool well) => well ? 1f : 0.5f;
    }
}
