using System;

namespace ColonyCouncil
{
    /// <summary>
    /// Callers: visitors who come to the colony to do something (Visitors/Callers.cs), as plain numbers
    /// (Tools/Tests/CallerTests.cs): what the art collector offers and pays for a commission, what a wedding feast eats
    /// and what the couple's families pay for it, what the undertakers ask, what kin bring for a captive, the barber's
    /// and the animal doctor's prices, and who of the colony wants a new cut.
    /// </summary>
    public static class CallerMath
    {
        /// <summary>Works of Good quality or better the colony must have before a collector hears of it.</summary>
        public const int MinGoodArt = 3;
        /// <summary>A collector offers this much over a piece's worth, and pays this much over a commissioned piece's.</summary>
        public const float SaleFactor = 1.6f, CommissionFactor = 1.4f;
        /// <summary>How long the sculptor works at a commission (a day's work, in hours).</summary>
        public const float CommissionHours = 6f;
        /// <summary>A commission not collected within this many days stays the colony's.</summary>
        public const float CommissionKeepDays = 60f;
        /// <summary>The collector comes back for a finished piece no sooner than this many days after it's done.</summary>
        public const float CommissionReturnDays = 3f;

        /// <summary>Goodwill a people must have with the colony before a couple of theirs asks to marry there.</summary>
        public const int WeddingGoodwill = 20;

        /// <summary>Enemy dead lying out, and for how long, before the undertakers come; graves they dig at most.</summary>
        public const int MinDead = 4, MaxGraves = 8;
        public const float DeadDays = 1f;

        /// <summary>Days a prisoner is held (a slave kept) before their kin come for them.</summary>
        public const float PrisonerDays = 5f, SlaveDays = 30f;

        /// <summary>What a talk through the cell door with their family takes off a prisoner's resistance and will; what turning the family away adds.</summary>
        public const float TalkResistance = 6f, TalkWill = 1f, RefuseResistance = 3f;

        /// <summary>Animals the animal doctor sees to at most.</summary>
        public const int MaxPatients = 8;

        public static int Round5(float v) => (int)Math.Round(v / 5f, MidpointRounding.AwayFromZero) * 5;

        public static int Round10(float v) => (int)Math.Round(v / 10f, MidpointRounding.AwayFromZero) * 10;

        /// <summary>The collector's offer for a piece worth <paramref name="value"/>: well above it, never under 50.</summary>
        public static int SalePrice(float value) => Math.Max(50, Round5(value * SaleFactor));

        /// <summary>What the collector pays for a commissioned piece worth <paramref name="value"/> once it's made: its quality decides.</summary>
        public static int CommissionPrice(float value) => Math.Max(40, Round5(value * CommissionFactor));

        /// <summary>Raw food for a wedding feast: a meal's worth (ten pieces) for each who eats, at least sixty.</summary>
        public static int FeastFood(int eaters) => Math.Max(60, Round10(10f * Math.Max(0, eaters)));

        /// <summary>What the families pay for the colony's food at the feast: about its worth.</summary>
        public static int FeastSilver(int rawFood) => Math.Max(20, Round5(rawFood * 0.8f));

        /// <summary>The undertakers' fee for so many graves.</summary>
        public static int GraveFee(int graves) => Math.Max(40, Round5(20f * graves));

        /// <summary>What kin bring for a captive worth <paramref name="value"/> to the game's slavers: a share of it, between 60 and 800.</summary>
        public static int Ransom(float value) => Math.Min(800, Math.Max(60, Round10(value * 0.6f)));

        /// <summary>The barber's price a head: more in a colony past the old ways.</summary>
        public static int CutPrice(bool industrial) => industrial ? 15 : 8;

        /// <summary>
        /// Whether a colonist who doesn't mind a cut wants one today: about three in five, by who they are and the day,
        /// so the letter's count and the answer agree.
        /// </summary>
        public static bool WantsCut(int id, int day) => (Math.Abs(id * 7 + day * 3) % 5) < 3;

        /// <summary>The animal doctor's fee: for each animal seen to, and the training session.</summary>
        public static int VetFee(int patients, bool training) => Math.Max(30, Round5(20f * Math.Max(0, patients) + (training ? 30f : 0f)));

        /// <summary>The fee in feed instead: as much feed as the silver would buy, and a fifth more, by tens.</summary>
        public static int FeedFor(int fee, float unitValue) => Math.Max(10, Round10(fee * 1.2f / Math.Max(0.05f, unitValue)));

        /// <summary>Ticks a colonist stands for a cut, by how long their hair is (a beard as well takes longer).</summary>
        public static int CutTicks(bool beard) => beard ? 900 : 650;
    }
}
