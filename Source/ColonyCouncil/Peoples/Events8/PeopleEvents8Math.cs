using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of the peoples' own events, round 8 (docs/peoples-events-proposals.md, items 71 to 82; Peoples/Events8/): the
    /// Last Republic's observer at the colony's vote, its medal for a colonist's deed, the deserter its Guard follows to the gate and
    /// its ration post in a famine; the Hollow Fleet's officers who log the colony's gravship, its sleeper in one of the game's old
    /// caskets, the order to disembark a colonist of rank gives at its hull and its landing party; the Helix Works' gene taken out,
    /// its whole xenogerm, its hybrid beast and its field lab. Shared with the tests (Tools/Tests/PeopleEvents8Tests.cs); nothing here
    /// knows the game.
    /// </summary>
    public static class PeopleEvents8Math
    {
        public const int DayTicks = 60000, HourTicks = 2500;

        public static int Round10(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0 : (int)Math.Round(v / 10f) * 10;

        private static int Clamp(int v, int lo, int hi) => Math.Max(lo, Math.Min(hi, v));

        private static float Clamp(float v, float lo, float hi) => float.IsNaN(v) ? lo : Math.Max(lo, Math.Min(hi, v));

        private static float Wealth(float wealthFactor) => float.IsNaN(wealthFactor) || float.IsInfinity(wealthFactor) ? 1f : wealthFactor;

        // ================= 71. An observer at the vote (the Last Republic) =================

        /// <summary>A clerk comes on polling day morning, once an election, at a chance, not soon after the last.</summary>
        public const float ObserverChance = 0.5f;

        public const int ObserverGapDays = 30;

        /// <summary>The clerk comes on the morning of polling day: from two hours before the polls open, until four hours before the count.</summary>
        public static bool ObserverDue(int now, int pollTick, int countTick) => pollTick > 0 && countTick > pollTick && now >= pollTick - 2 * HourTicks && now < countTick - 4 * HourTicks;

        /// <summary>With the Republic's clerk standing by, a heckle at the hustings comes to blows half as often.</summary>
        public const float ObservedBrawlFactor = 0.5f;

        public static float BrawlFactor(bool observed) => observed ? ObservedBrawlFactor : 1f;

        /// <summary>A quarter of an hour with each voter on the roll, eight at most.</summary>
        public const int RollTicks = 625, RollMax = 8;

        /// <summary>The Republic's thanks for a fair vote, and more for the result entered in its register.</summary>
        public const int ObserverGoodwill = 3, RegisterGoodwill = 2;

        /// <summary>The clerk waits for the count this long after it was set for (a count put off a day), then goes.</summary>
        public const int ObserverWaitHours = 36;

        // ================= 72. A medal (the Last Republic) =================

        /// <summary>The deeds a medal is for: a rescue under fire, the line held in a hard fight.</summary>
        public static readonly string[] MedalDeeds = { "Rimfolk_Deed_Rescue", "Rimfolk_Deed_HeldLine" };

        public static bool MedalDeed(string deedDefName) => deedDefName != null && Array.IndexOf(MedalDeeds, deedDefName) >= 0;

        /// <summary>A deed half a day to four days old is rolled for once, at a chance, not soon after the last medal.</summary>
        public const float MedalChance = 0.5f;

        public const int MedalAfterHours = 12, MedalFreshDays = 4, MedalGapDays = 30;

        public static bool MedalFresh(int now, int deedTick) => deedTick >= 0 && now - deedTick >= MedalAfterHours * HourTicks && now - deedTick <= MedalFreshDays * DayTicks;

        /// <summary>The citation read at the gate: an hour.</summary>
        public const int CitationTicks = 2500;

        public const int MedalGoodwill = 3;

        /// <summary>The Republic's newspaper comes with the medal from the industrial age on (Vanilla Books Expanded's).</summary>
        public static bool Newspaper(int peopleAge, bool booksLoaded) => booksLoaded && peopleAge >= 2;

        /// <summary>The deeds rolled for a medal, kept.</summary>
        public const int MaxMedalDeeds = 12;

        // ================= Meeting at the gate (the medal, and the Works' work when they can't get to the colonist) =================

        /// <summary>The one met has come when they stand this near the visitor at the meeting place: the work at them begins.</summary>
        public const float MeetRange = 4f;

        /// <summary>The visitor waits at the meeting place up to three hours for the one they are to meet; the visit's hour sends them again.</summary>
        public const int MeetWaitTicks = 3 * HourTicks;

        /// <summary>How many times the visit's hour sets the meeting going again (the colonist pulled away by a meal or a fight).</summary>
        public const int MeetSends = 4;

        /// <summary>What a visit's hour does while its work at someone of the colony is to be done.</summary>
        public enum MeetStep
        {
            /// <summary>Under way: leave it.</summary>
            Wait,
            /// <summary>Stopped with hours and sends left: set it going again.</summary>
            Again,
            /// <summary>The hours or the sends are out: it ends, said as it is, never done unseen.</summary>
            GiveUp,
            /// <summary>The visitor is gone (dead, away, down): nobody to do it.</summary>
            Gone
        }

        /// <summary>A visit's hour while the work at a colonist waits on a meeting: the visitor up, their work under way, the hours and the sends used.</summary>
        public static MeetStep Step(bool visitorUp, bool working, int now, int until, int sends)
        {
            if (!visitorUp) return MeetStep.Gone;
            if (now > until) return MeetStep.GiveUp;
            if (working) return MeetStep.Wait;
            return sends < MeetSends ? MeetStep.Again : MeetStep.GiveUp;
        }

        /// <summary>The one met has come near enough (both on the same map, the distance in cells).</summary>
        public static bool Met(float distance) => !float.IsNaN(distance) && distance <= MeetRange;

        // ================= 73. A fugitive of the Republic =================

        /// <summary>The deserter's fine: by the colony's wealth, to the nearest ten.</summary>
        public static int Fine(float wealthFactor) => Clamp(Round10(250f * Wealth(wealthFactor)), 150, 500);

        /// <summary>Handed over: the Republic's thanks; sheltered: its anger; the fine paid: a little thanks.</summary>
        public const int HandOverGoodwill = 8, ShelterGoodwill = -10, FineGoodwill = 2;

        /// <summary>Two or three of the Civic Guard follow the deserter to the gate.</summary>
        public const int DetailMin = 2, DetailMax = 3;

        // ================= 74. A ration post (the Last Republic) =================

        /// <summary>A famine in the region is rolled for once, within its first days, at a chance.</summary>
        public const float RationChance = 0.5f;

        public const int RationWithinDays = 5;

        /// <summary>A clerk and two guards, a week.</summary>
        public const int PostPeople = 3, PostDays = 7;

        /// <summary>The rations they bring for the road; the hungry take one each.</summary>
        public const int PostRations = 24;

        /// <summary>A hungry traveller (or two) walks in now and then by day, a few at a time.</summary>
        public const int HungryGapMin = 3, HungryGapMax = 6, HungryMax = 3;

        public static bool PostHour(int hour) => hour >= 8 && hour <= 19;

        /// <summary>A ration taken at their table: half an hour.</summary>
        public const int RationTicks = 1250;

        /// <summary>The Republic's fixed price for a meal at the post, and how many meals it buys at once.</summary>
        public const int RationPrice = 8, RationBuy = 20;

        public static int RationPay(int meals) => Math.Max(0, meals) * RationPrice;

        /// <summary>The ration cards a citizen colony is offered: a ration for each of its people, at least four.</summary>
        public static int Cards(int colonists) => Math.Max(4, Math.Min(12, colonists));

        /// <summary>One of the hungry asks to join, once a stay; the guards turn away one of a people the Republic is at war with.</summary>
        public const float HungryJoinChance = 0.15f, DenouncedChance = 0.3f;

        public const int PostGoodwill = 3;

        // ================= 75. The colony's gravship (the Hollow Fleet) =================

        /// <summary>The officers walk the decks: a quarter of an hour at each part, five parts at most.</summary>
        public const int DeckTicks = 625, DeckParts = 5;

        public const int GravGoodwill = 10;

        /// <summary>What the pilot learns from the officers (the ship's systems and its flying are the Intellectual skill).</summary>
        public const float PilotXp = 3000f;

        /// <summary>The part from the hull's stores, when it is a material: so many.</summary>
        public const int PanelCount = 25;

        /// <summary>Two officers come down.</summary>
        public const int Officers = 2;

        // ================= 76. A sleeper of theirs (the Hollow Fleet) =================

        /// <summary>What the Fleet pays for a sleeper handed over: by the colony's wealth, to the nearest ten.</summary>
        public static int SleeperPay(float wealthFactor) => Clamp(Round10(200f * Wealth(wealthFactor)), 150, 400);

        public const int SleeperGoodwill = 12, KeptGoodwill = -4;

        /// <summary>An hour at the casket to open it.</summary>
        public const int OpenTicks = 2500;

        // ================= 77. The order to disembark (the Hollow Fleet) =================

        /// <summary>A hull within this many tiles; a day or two there; three to five of the crew come home and join.</summary>
        public const float HullTiles = 20f;

        public const float OrderDays = 1.5f;

        public const int JoinersMin = 3, JoinersMax = 5;

        public static int Joiners(float roll) => JoinersMin + (int)Math.Floor(Clamp(roll, 0f, 0.999f) * (JoinersMax - JoinersMin + 1));

        public const int OrderGoodwill = 10;

        // ================= 78. A landing party (the Hollow Fleet) =================

        /// <summary>Four to six crew, five to seven days.</summary>
        public const int PartyMin = 4, PartyMax = 6, PartyMinDays = 5, PartyMaxDays = 7;

        /// <summary>They survey by day.</summary>
        public static bool SurveyHour(int hour) => hour >= 8 && hour <= 16;

        public const int SurveyTicks = 1250;

        /// <summary>The watch bell: the change of the watch every four hours, rung by day and in the evening.</summary>
        public static bool BellHour(int hour) => hour >= 8 && hour <= 20 && hour % 4 == 0;

        /// <summary>They buy a week's meals at their price; a sleeper they brought falls ill, once a stay.</summary>
        public const int PartyMeals = 12, PartyMealPrice = 12;

        public const float SleeperSickChance = 0.2f;

        /// <summary>A deckhand who disembarks against the officer's wish: the Fleet minds a little.</summary>
        public const int DeckhandGoodwill = -3, PartyGoodwill = 3;

        // ================= 79. A gene taken out (the Helix Works) =================

        /// <summary>A harmful gene: one the body gains by (metabolism above zero), not archite, not only for looks.</summary>
        public static bool Harmful(int metabolism, int archite) => metabolism >= 1 && archite <= 0;

        /// <summary>What taking it out costs: 160 silver, 70 for each point of metabolism, to the nearest ten, 600 at most.</summary>
        public static int OutPrice(int metabolism) => Math.Min(600, Round10(160f + 70f * Math.Max(1, metabolism)));

        /// <summary>Two hours' work, as gene work is.</summary>
        public const int OutTicks = 5000;

        public const int OutGoodwill = 2;

        // ================= 80. A whole xenogerm (the Helix Works) =================

        /// <summary>The kinds the Works grow a whole xenogerm of: Biotech's, Odyssey's, Alpha Genes' and the hussars' own, by name.</summary>
        public static readonly string[] Xenotypes =
        {
            "Genie", "Highmate", "Hussar", "Pigskin", "Neanderthal", "Impid", "Yttakin", "Dirtmole", "Waster", "Starjack", "AG_Helixien", "AG_Animusen", "AG_Fleetkind", "AG_Nereid",
            "VREH_Uhlan"
        };

        /// <summary>Its price: 300 silver, 15 a gene and 20 for each point of complexity, to the nearest ten, 400 to 1500.</summary>
        public static int XenogermPrice(int genes, int complexity) => Clamp(Round10(300f + 15f * Math.Max(0, genes) + 20f * Math.Max(0, complexity)), 400, 1500);

        /// <summary>Two of the colony's genepacks with the silver; without them, their worth in silver more.</summary>
        public const int Genepacks = 2, GenepackWorth = 150;

        public static int SilverOnly(int price) => price + Genepacks * GenepackWorth;

        /// <summary>Three hours' work.</summary>
        public const int XenogermTicks = 7500;

        public const int XenogermGoodwill = 3;

        // ================= 81. A hybrid beast (the Helix Works) =================

        /// <summary>The young hybrids a vat-keeper sells: Vanilla Genetics Expanded's beasts of the farm and the road, never its odd ones.</summary>
        public static readonly string[] Hybrids =
        {
            "GR_Muffalohorse", "GR_Muffalope", "GR_Muffalowolf", "GR_Muffalochicken", "GR_Thrumhorse", "GR_Thrumbalope", "GR_Wolfhorse", "GR_Chickenhorse", "GR_Bearffalo",
            "GR_Lizardochs", "GR_MeadowLizard", "GR_Wolfcat"
        };

        /// <summary>Its price: its worth and a fifth, to the nearest ten, 150 at least.</summary>
        public static int HybridPrice(float marketValue) => Math.Max(150, Round10(Clamp(marketValue, 0f, 100000f) * 1.2f));

        /// <summary>What the colony's handler learns with the second answer.</summary>
        public const float HandlerXp = 1500f;

        public const int HybridGoodwill = 2;

        // ================= 82. A field lab (the Helix Works) =================

        /// <summary>Two sequencers and a vat-keeper, a week.</summary>
        public const int LabPeople = 3, LabDays = 7;

        /// <summary>They sample the land's beasts and plants by day: half an hour at each.</summary>
        public static bool SampleHour(int hour) => hour >= 8 && hour <= 16;

        public const int SampleTicks = 1250;

        /// <summary>What they pay for a genepack.</summary>
        public const int GenepackPrice = 180;

        /// <summary>It can turn, once a stay each: a beast of theirs gets loose; a vat-grown of theirs falls ill.</summary>
        public const float LooseChance = 0.12f, VatSickChance = 0.15f;

        public const int LabGoodwill = 3;

        /// <summary>The lab's own work (a gene written, a sample drawn) waiting, at most.</summary>
        public const int MaxLabTasks = 3;
    }
}
