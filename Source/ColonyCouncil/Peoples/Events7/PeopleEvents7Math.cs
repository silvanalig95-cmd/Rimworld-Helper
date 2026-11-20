using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of the peoples' own events, round 7 (docs/peoples-events-proposals.md, items 60, 61, 62, 64, 65, 66, 67, 68,
    /// 69 and 70; Peoples/Events7/): the Kiln Guild's master who judges a masterwork, the journeyman who comes back a master and
    /// the great work three journeymen build; the Signal Line's call a season, its mast at a friend's town and its line crew; the
    /// Deepwell Union's cave-in at a pit, the oil or the seam under the colony, the organizer's evening and the picket by the road.
    /// Shared with the tests (Tools/Tests/PeopleEvents7Tests.cs); nothing here knows the game.
    /// </summary>
    public static class PeopleEvents7Math
    {
        public const int DayTicks = 60000, HourTicks = 2500;

        public static int Round10(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0 : (int)Math.Round(v / 10f) * 10;

        private static int Clamp(int v, int lo, int hi) => Math.Max(lo, Math.Min(hi, v));

        private static float Clamp(float v, float lo, float hi) => float.IsNaN(v) ? lo : Math.Max(lo, Math.Min(hi, v));

        // ================= 60. The master's mark (the Kiln Guild) =================

        /// <summary>The game's quality levels for a masterwork and a legendary piece (QualityCategory).</summary>
        public const int Masterwork = 5, Legendary = 6, Excellent = 4, Good = 3;

        /// <summary>A piece the guild comes to judge: a masterwork or a legend.</summary>
        public static bool Judged(int quality) => quality >= Masterwork;

        /// <summary>A master comes within so many days of the piece being made, at a chance, not again for so many days.</summary>
        public const int MarkKeepDays = 10, MarkGapDays = 20;

        public const float MarkChance = 0.6f;

        /// <summary>
        /// The chance the master finds a fault hidden in it ("bad work is a lie told in stone"): one masterwork in five, one legend
        /// in twelve; nothing below a masterwork is judged.
        /// </summary>
        public static float FaultChance(int quality) => quality >= Legendary ? 1f / 12f : quality == Masterwork ? 0.2f : 0f;

        public static bool Fault(int quality, float roll) => !float.IsNaN(roll) && roll < FaultChance(quality);

        /// <summary>An hour at the piece; what the maker learns standing by with the second answer.</summary>
        public const int JudgeTicks = 2500;

        public const float JudgeXp = 800f;

        public const int MarkGoodwill = 2;

        /// <summary>Vanilla Skills Expanded's quality expertise, given with a true mark on a piece made at a bench (Crafting).</summary>
        public const string QualityExpertise = "QualityExpert";

        // ================= 61. Come back better (the Kiln Guild) =================

        /// <summary>A year after the wander-year (sixty days), the journeyman comes back a master.</summary>
        public const int ComeBackDays = 60;

        /// <summary>A guild at war with the colony: the visit waits a season; never after two years.</summary>
        public const int ComeBackRetryDays = 15, ComeBackForgetDays = 240;

        /// <summary>The master's craft: at least this good at their bench, a year on.</summary>
        public const int MasterSkill = 14;

        public const int ComeBackGoodwill = 4;

        /// <summary>The morning at the bench (the second answer): what the teacher learns from the one they taught.</summary>
        public const float BenchXp = 1500f;

        /// <summary>The journeymen kept on record for their return, at most.</summary>
        public const int MaxComeBacks = 6;

        // ================= 62. A work that stays (the Kiln Guild) =================

        /// <summary>What they build: a carved bridge over the colony's river (marked like the one at Hardwater, first), a grand sculpture in its hall, a fountain where it gathers.</summary>
        public const string Bridge = "bridge", Sculpture = "sculpture", Fountain = "fountain";

        public static readonly string[] Works = { Bridge, Sculpture, Fountain };

        /// <summary>Three journeymen, five to eight days.</summary>
        public const int Journeymen = 3, WorkMinDays = 5, WorkMaxDays = 8;

        /// <summary>The crossing they bridge: water this many cells across at most, two cells wide.</summary>
        public const int BridgeMin = 2, BridgeMax = 12, BridgeWidth = 2;

        /// <summary>How far from the colony's middle they look for the river and the hall.</summary>
        public const float WorkReach = 45f;

        /// <summary>The bridge's stone or wood: so much a cell where the bridge's own cost can't be read.</summary>
        public const int BridgePerCell = 12;

        /// <summary>The material the work takes: the bridge by its cells, the others by their own cost; never under fifty.</summary>
        public static int Material(string work, int cells, int perCell, int cost) =>
            Math.Max(50, work == Bridge ? Math.Max(1, cells) * Math.Max(1, perCell) : Math.Max(1, cost));

        /// <summary>Their hours at the work each day.</summary>
        public static bool WorkHour(int hour) => hour >= 8 && hour <= 17;

        public const int CarveTicks = 2500;

        /// <summary>
        /// The work's days at it before it stands: two thirds of their stay at the least, and it stands once the days of work are
        /// done (a day torn down and a day waiting for stone come on top).
        /// </summary>
        public static int WorkDays(int stayDays) => Math.Max(3, (int)Math.Ceiling(Math.Max(1, stayDays) * 2f / 3f));

        /// <summary>It can turn, once a stay each: the stone runs short (they need a quarter again), a journeyman's day is judged bad and torn down.</summary>
        public const float ShortChance = 0.3f, TornChance = 0.2f;

        public static int MoreNeeded(int material) => Math.Max(10, (int)Math.Ceiling(Math.Max(0, material) * 0.25f));

        /// <summary>The quality it stands at: excellent, and good when they had to make do without the stone they asked for.</summary>
        public static int WorkQuality(bool madeDo) => madeDo ? Good : Excellent;

        public const int WorkGoodwill = 4;

        // ================= 64. A voice for the colony (the Signal Line) =================

        /// <summary>What the colony can ask for on the Line's masts, once a season.</summary>
        public const string CallGoods = "goods", CallArms = "arms", CallHands = "hands", CallWord = "word";

        /// <summary>A trader kind's goods by its name: bulk goods, arms, or neither (slavers, pirates, tribute collectors are never called).</summary>
        public static string TradeKind(string traderKindDefName)
        {
            if (string.IsNullOrEmpty(traderKindDefName)) return null;
            string n = traderKindDefName.ToLowerInvariant();
            if (n.Contains("slaver") || n.Contains("pirate") || n.Contains("tribute") || n.Contains("visitor") || n.Contains("base_") || n.Contains("orbital")) return null;
            if (n.Contains("combat") || n.Contains("war")) return CallArms;
            if (n.Contains("bulk") || n.Contains("goods") || n.Contains("wool") || n.Contains("general")) return CallGoods;
            return null;
        }

        /// <summary>The Line's price a call (its price for news, the same for everyone), and three times it for hands.</summary>
        public const int CallFee = 50, HandsFee = 150;

        /// <summary>A good word on the Line's masts: this much better with the people spoken to.</summary>
        public const int GoodWord = 6;

        /// <summary>On the colony's radio or the Line's own mast, the call is answered within hours; carried by a runner, in a day or two.</summary>
        public const int RadioMinHours = 3, RadioMaxHours = 6, RunnerMinHours = 24, RunnerMaxHours = 48;

        public static int CallHours(bool radio, float roll)
        {
            int lo = radio ? RadioMinHours : RunnerMinHours, hi = radio ? RadioMaxHours : RunnerMaxHours;
            return lo + (int)Math.Floor(Clamp(roll, 0f, 0.999f) * (hi - lo + 1));
        }

        /// <summary>An hour at the radio or the mast for the call.</summary>
        public const int CallTicks = 2500;

        // ================= 65. A mast at a friend's town (the Signal Line) =================

        /// <summary>The fee for a mast at a town so many tiles off: the mast and the walk, to the nearest ten.</summary>
        public static int MastFee(float tiles) => Clamp(Round10(150f + 10f * Clamp(tiles, 0f, 200f)), 200, 450);

        /// <summary>Paid in components instead: as many as the fee is worth (the Line buys them at their worth).</summary>
        public static int MastComponents(int fee, float componentValue) => componentValue <= 0.01f || float.IsNaN(componentValue) ? 0 : Math.Max(2, (int)Math.Ceiling(fee / componentValue));

        /// <summary>The mast stands once the lineman has walked there and spent a day raising it.</summary>
        public static float MastDays(float roadDays) => Clamp(roadDays, 0.5f, 30f) + 1f;

        public const int MaxTownMasts = 6, MastGoodwill = 2;

        // ================= 66. The line crew (the Signal Line) =================

        /// <summary>Three or four linesmen, a week.</summary>
        public const int CrewMin = 3, CrewMax = 4, CrewDays = 7;

        /// <summary>Two hours to raise their mast; an hour on the wire.</summary>
        public const int RaiseTicks = 5000, StringTicks = 2500;

        /// <summary>The radio at their fire at night plays the region's news, once a night.</summary>
        public static bool RadioHour(int hour) => hour >= 19 && hour <= 21;

        /// <summary>They work the wire by day.</summary>
        public static bool WireHour(int hour) => hour >= 8 && hour <= 16;

        /// <summary>The mast kept as the colony's own relay mast when they go: by the colony's wealth, to the nearest ten.</summary>
        public static int KeepFee(float wealthFactor) => Clamp(Round10(300f * (float.IsNaN(wealthFactor) ? 1f : wealthFactor)), 200, 600);

        /// <summary>It can turn, once a stay each, in a storm: a lineman falls from the mast (the dangers on the map), the lightning takes the mast.</summary>
        public const float FallChance = 0.15f, MastStruckChance = 0.25f;

        public const float FallLow = 4f, FallHigh = 10f;

        public const int CrewGoodwill = 3;

        // ================= 67. The pit's cave-in (the Deepwell Union) =================

        /// <summary>A union pit within this many tiles.</summary>
        public const float CaveInTiles = 12f;

        /// <summary>Four to six days digging, a shift a day.</summary>
        public const int CaveInMinDays = 4, CaveInMaxDays = 6;

        public static int CaveInDays(float roll) => CaveInMinDays + (int)Math.Floor(Clamp(roll, 0f, 0.999f) * (CaveInMaxDays - CaveInMinDays + 1));

        /// <summary>One shift in twenty brings a crush (the drill rig's own chance, PeoplesFMath.AccidentChance).</summary>
        public const float CrushChance = 0.05f;

        public const float CrushLow = 3f, CrushHigh = 9f;

        /// <summary>The union's pay a day of digging, each.</summary>
        public const int CaveInPayPerDay = 40;

        public static int CaveInPay(int days) => Math.Max(0, days) * CaveInPayPerDay;

        /// <summary>What they learn a shift: the miner at the face, the doctor with the hurt.</summary>
        public const float ShiftXp = 450f;

        /// <summary>Back from it: the union thinks this much better of the colony (twelve, both sent; half alone).</summary>
        public const int CaveInGoodwill = 12, CaveInAloneGoodwill = 6;

        // ================= 68. Oil in the ground (the Deepwell Union) =================

        /// <summary>Rimefeller's wells, the shallow first; the deposit is Rimefeller's own (its wells' placing says where).</summary>
        public static readonly string[] Wells = { "OilWell", "DeepOilWell" };

        /// <summary>How far from the colony's middle the assayer looks.</summary>
        public const float OilReach = 50f;

        /// <summary>The well sunk for silver: by the colony's wealth, to the nearest ten.</summary>
        public static int WellPrice(float wealthFactor) => Clamp(Round10(450f * (float.IsNaN(wealthFactor) ? 1f : wealthFactor)), 300, 900);

        /// <summary>Its year's share: so much chemfuel a season, for four seasons (else its worth in silver).</summary>
        public const int ShareChemfuel = 30, ShareSeasons = 4, ShareSeasonDays = 15;

        /// <summary>Six hours to sink the well, an hour to mark a deep seam; the assayer's fee for the mark.</summary>
        public const int SinkTicks = 15000, MarkTicks = 2500, SeamFee = 60;

        /// <summary>The drill crew comes for a marked seam a day or so after.</summary>
        public const float DrillAfterDays = 1f;

        public const float SinkXp = 900f;

        public const int OilGoodwill = 3;

        // ================= 69. The organizer's evening (the Deepwell Union) =================

        /// <summary>The colony's people worn down: their strain at least this (the heart's), or the long hours by decree.</summary>
        public const float WornStrain = 40f;

        /// <summary>A colonist under strain, stung by a refusal: at least this.</summary>
        public const float StrainedOne = 30f;

        public static bool Worn(float colonyStrain, bool longHours) => longHours || !float.IsNaN(colonyStrain) && colonyStrain >= WornStrain;

        /// <summary>The shift-captain comes at dusk, at a chance, not again for so many days.</summary>
        public static bool Dusk(int hour) => hour >= 16 && hour <= 18;

        public const float EveningChance = 0.4f;

        public const int EveningGapDays = 30;

        /// <summary>The petition is made the next morning.</summary>
        public static bool Morning(int hour) => hour >= 7 && hour <= 10;

        /// <summary>The petition answered within the council's own days (a decree petition lapses after five), and a day for the last look.</summary>
        public const int PetitionDays = 6;

        public const int HeardGoodwill = 4, SupperGoodwill = 1;

        // ================= 70. The picket (the Deepwell Union) =================

        /// <summary>A strike called brings a picket by the colony's road, at a chance.</summary>
        public const float PicketChance = 0.5f;

        /// <summary>They stay while the strike lasts, at most its days (PeoplesFMath.StrikeDays).</summary>
        public const int PicketMaxDays = 30;

        /// <summary>Food for the picket line: meals for the strikers, which settles the strike.</summary>
        public const int PicketMeals = 15;

        /// <summary>They sing by their fire in the evening.</summary>
        public static bool Sing(int hour) => hour >= 19 && hour <= 21;

        /// <summary>It can turn, once a stay: strikebreakers of another people come against the camp.</summary>
        public const float BreakersChance = 0.15f;

        public const int PicketGoodwill = 3;
    }
}
