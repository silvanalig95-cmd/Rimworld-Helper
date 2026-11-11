using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of the peoples' own events, round 1 (docs/peoples-events-proposals.md, items 10, 5, 47, 83, 51, 1,
    /// 19, 17, 34 and 95; Peoples/Events/): how often a people's gift comes, what it costs, what a reading of an old place
    /// says, when goods held in trust come home in good ink, and the rates of what can turn at a hostel or a lantern post.
    /// Shared with the tests (Tools/Tests/PeopleEventsTests.cs); nothing here knows the game.
    /// </summary>
    public static class PeopleEventsMath
    {
        public const int DayTicks = 60000, HourTicks = 2500;

        public static int Round10(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0 : (int)Math.Round(v / 10f) * 10;

        // ================= Gifts: once a colonist, and spaced out =================

        /// <summary>Days between any two of the peoples' gifts to a colonist, whichever people gives it.</summary>
        public const int GiftGapDays = 20;

        /// <summary>How many gifts the colony's record keeps (one a colonist a kind; the oldest let go past it).</summary>
        public const int MaxGifts = 120;

        /// <summary>A gift may come now: none yet, or the last one <see cref="GiftGapDays"/> or more ago.</summary>
        public static bool GiftOpen(int now, int lastGift) => lastGift < 0 || now - lastGift >= GiftGapDays * DayTicks;

        /// <summary>The record's key for one kind of gift to one colonist ("beast:4512").</summary>
        public static string GiftKey(string kind, int id) => (kind ?? "") + ":" + id;

        // ================= 10. A beast for a colonist (the herding folk) =================

        /// <summary>A beast the elder could choose for someone: an animal of a riding or pack size, tame enough, no predator or insect.</summary>
        public static bool BeastFits(bool animal, float bodySize, float wildness, bool predator, bool insect, bool dryad) =>
            animal && !predator && !insect && !dryad && !float.IsNaN(bodySize) && bodySize >= 0.9f && bodySize <= 4.5f && !float.IsNaN(wildness) && wildness <= 0.75f;

        /// <summary>How much likelier a beast is: one the colonist could ride (with Giddy-Up) twice over, by the land's own commonality.</summary>
        public static float BeastWeight(bool ridable, bool giddyUp, float commonality) =>
            (float.IsNaN(commonality) ? 0.05f : Math.Max(0.05f, commonality)) * (giddyUp && ridable ? 2f : 1f);

        /// <summary>The herders' fair price: nine tenths of its worth, rounded to ten, never under 60.</summary>
        public static int BeastPrice(float worth) => Math.Max(60, Round10(Math.Max(0f, float.IsNaN(worth) ? 0f : worth) * 0.9f));

        /// <summary>One of the colony's own young beasts is worth trading for theirs: at least half its worth.</summary>
        public static bool TradeEnough(float ours, float theirs) => !float.IsNaN(ours) && !float.IsNaN(theirs) && theirs > 0f && ours >= theirs * 0.5f;

        /// <summary>The bond's first tasks: tameness and obedience; the rest is the colony's to teach.</summary>
        public static readonly string[] FirstTasks = { "Tameness", "Obedience" };

        public const float BeastGoodwill = 3f, TradeGoodwill = 4f;

        // ================= 5. A limb fitted (the hospitaller order) =================

        /// <summary>What a missing part is, by the body part's def: "leg", "foot", "hand", "arm"; null for anything else.</summary>
        public static string PartKind(string bodyPart)
        {
            switch (bodyPart)
            {
                case "Leg": return "leg";
                case "Foot": return "foot";
                case "Hand": return "hand";
                case "Arm":
                case "Shoulder": return "arm";
            }
            return null;
        }

        /// <summary>
        /// The order's limb for a missing part in its age (0 tribal to 3 spacer): a peg leg or a wooden hand from the colony's
        /// wood before the industrial age, the game's simple prostheses after; never bionics. A whole arm before the
        /// industrial age has none.
        /// </summary>
        public static string LimbRecipe(string kind, int age)
        {
            bool industrial = age >= 2;
            switch (kind)
            {
                case "leg":
                case "foot": return industrial ? "InstallSimpleProstheticLeg" : "InstallPegLeg";
                case "hand": return industrial ? "InstallSimpleProstheticArm" : "InstallWoodenHand";
                case "arm": return industrial ? "InstallSimpleProstheticArm" : null;
            }
            return null;
        }

        /// <summary>The limb is made of the colony's wood (the peg leg, the wooden hand), not brought.</summary>
        public static bool Wooden(string recipe) => recipe == "InstallPegLeg" || recipe == "InstallWoodenHand";

        /// <summary>The surgeon's hand: Medicine 10 to 16.</summary>
        public const int SurgeonMin = 10, SurgeonMax = 16;

        public const int FitTicks = 3 * HourTicks / 2;
        public const int MedicineBack = 8;
        public const int AssistXp = 600;
        /// <summary>The colony owes the order for it: a good turn of theirs big enough to be a debt of honour.</summary>
        public const int SparedWeight = 12;

        /// <summary>The success the card shows: the game's own surgery quality, as a whole percent within 1 and 99.</summary>
        public static int ChancePercent(float quality) => float.IsNaN(quality) ? 0 : Math.Max(1, Math.Min(99, (int)Math.Round(quality * 100f)));

        // ================= 47. Storm-sense (the Stormglass Conclave) =================

        /// <summary>Their price: 350 silver by the colony's wealth, within 250 and 700.</summary>
        public static int StormPrice(float wealthFactor) => Math.Max(250, Math.Min(700, Round10(350f * (float.IsNaN(wealthFactor) ? 1f : wealthFactor))));

        /// <summary>Sensitive enough to hear the storm: half the usual psychic sensitivity or more.</summary>
        public static bool Hears(float sensitivity) => !float.IsNaN(sensitivity) && sensitivity >= 0.5f;

        public const int StormTicks = HourTicks;
        public const int StormHours = 3;

        /// <summary>Vanilla Psycasts Expanded's paths the storm opens, as the mod names them.</summary>
        public static readonly string[] StormPaths = { "VPE_Staticlord", "VPE_Frostshaper" };

        /// <summary>A RimWorld of Magic's Lightning tome, without Royalty.</summary>
        public const string StormTome = "BookOfStormBorn";

        public const float StormGoodwill = 2f, VialGoodwill = 3f;

        // ================= 83. A mechlink (the Lattice) =================

        /// <summary>Their price: three quarters of the mechlink's and the mech's worth, within 400 and 1200.</summary>
        public static int MechlinkPrice(float linkWorth, float mechWorth)
        {
            float sum = (float.IsNaN(linkWorth) ? 0f : Math.Max(0f, linkWorth)) + (float.IsNaN(mechWorth) ? 0f : Math.Max(0f, mechWorth));
            return Math.Max(400, Math.Min(1200, Round10(sum * 0.75f)));
        }

        public const int SubcoresPay = 2;
        public const int MechlinkTicks = 2 * HourTicks;

        /// <summary>The mech linked first: a lifter or a constructoid, or with Alpha Mechs one of its light workers.</summary>
        public static readonly string[] StarterMechs = { "Mech_Lifter", "Mech_Constructoid", "AM_Mech_MasterChef", "AM_Mech_TurboCleaner" };

        // ================= 51. A Gauranlen seed (the Mossborn) =================

        /// <summary>The ledger has warmed to the colony: their goodwill 30 or more, and no more felled than planted this season.</summary>
        public static bool LedgerWarm(int goodwill, int felled, int planted) => goodwill >= 30 && felled <= planted;

        /// <summary>The promise at a season's reckoning: 1 kept (trees felled, as many planted), -1 broken (more felled), 0 nothing felled.</summary>
        public static int Promise(int felled, int planted) => felled <= 0 ? 0 : planted >= felled ? 1 : -1;

        public const int PromiseKeptGoodwill = 2, PromiseBrokenGoodwill = -6;
        public const int PlantTicks = HourTicks;
        /// <summary>Without Ideology, a healroot grove of so many plants.</summary>
        public const int Grove = 9;

        // ================= 1. Crates held in trust (the road league) =================

        public const int CratesMin = 6, CratesMax = 10;
        public const int DueMinDays = 20, DueMaxDays = 30;

        /// <summary>How many crates: six to ten by a roll in [0, 1).</summary>
        public static int Crates(float roll) => CratesMin + Math.Max(0, Math.Min(CratesMax - CratesMin, (int)Math.Floor((float.IsNaN(roll) ? 0f : roll) * (CratesMax - CratesMin + 1))));

        /// <summary>A crate's goods by the league's age and its place in the load: (defName, count), the base game's.</summary>
        public static (string def, int count) CrateGoods(int age, int i)
        {
            var table = age <= 0 ? Tribal : age == 1 ? Medieval : age == 2 ? Industrial : Spacer;
            return table[((i % table.Length) + table.Length) % table.Length];
        }

        private static readonly (string, int)[] Tribal = { ("Cloth", 50), ("Pemmican", 40), ("MedicineHerbal", 8), ("Leather_Plain", 40), ("WoodLog", 60) };
        private static readonly (string, int)[] Medieval = { ("Cloth", 60), ("Steel", 50), ("Beer", 20), ("MedicineHerbal", 10), ("Leather_Plain", 50) };
        private static readonly (string, int)[] Industrial = { ("Steel", 80), ("ComponentIndustrial", 6), ("MedicineIndustrial", 6), ("Chemfuel", 40), ("Cloth", 60) };
        private static readonly (string, int)[] Spacer = { ("Plasteel", 30), ("ComponentIndustrial", 10), ("MedicineIndustrial", 8), ("ComponentSpacer", 2), ("Uranium", 20) };

        /// <summary>The fee at the fetch: a tenth of what the crates hold, never under 20.</summary>
        public static int TrustFee(float worth) => Math.Max(20, Round10((float.IsNaN(worth) ? 0f : Math.Max(0f, worth)) / 10f));

        /// <summary>Good ink: every league's traders give a few points better prices for a year.</summary>
        public const float GoodInkTerms = 0.03f;
        public const int GoodInkDays = 60;
        /// <summary>Red ink: no league caravan for a season, and a grudge.</summary>
        public const int RedInkDays = 15;
        public const int RedInkGoodwill = -15, GoodInkGoodwill = 6, FreeGoodwill = 4;

        /// <summary>How the fetch went.</summary>
        public enum Ink { Good, Red, None }

        /// <summary>
        /// The book's verdict: red ink for any crate opened, sold or gone; a crate burned in a raid is forgiven; good ink when
        /// every other crate is there. None when there were no crates.
        /// </summary>
        public static Ink Verdict(int total, int present, int burned, int opened, int sold)
        {
            if (total <= 0) return Ink.None;
            if (opened > 0 || sold > 0) return Ink.Red;
            return present + burned >= total ? Ink.Good : Ink.Red;
        }

        /// <summary>How each crate's going is noted: still there, opened, sold, burned (destroyed by harm), gone.</summary>
        public const string Opened = "opened", Sold = "sold", Burned = "burned", Gone = "gone";

        /// <summary>Their crates' way here and back, by the league's age: a lame pack beast, a broken truck, a lander short of lift.</summary>
        public static string Means(int age) => age <= 1 ? "beast" : age == 2 ? "truck" : "lander";

        public const int MaxTrusts = 4;

        // ================= 19. What lies in the old place (the learned society) =================

        public const float MeasureDays = 4f;
        public const float ResearchShare = 0.3f, ResearchMax = 1200f;
        public const int MaxRead = 30;

        /// <summary>
        /// The reading's parts, in the order told: sleeping mechanoids, fleshbeasts, hives (with their insects), caskets
        /// (and how many hold someone), a sealed crate; or nothing at all.
        /// </summary>
        public static List<string> ReadingParts(int mechs, int beasts, int hives, int caskets, int crates)
        {
            var parts = new List<string>();
            if (mechs > 0) parts.Add("mechs");
            if (beasts > 0) parts.Add("fleshbeasts");
            if (hives > 0) parts.Add("hives");
            if (caskets > 0) parts.Add("caskets");
            if (crates > 0) parts.Add("crates");
            if (parts.Count == 0) parts.Add("nothing");
            return parts;
        }

        /// <summary>Something in it wakes when it is opened: sleeping mechs, fleshbeasts, hives, or sleepers in caskets.</summary>
        public static bool Dangerous(int mechs, int beasts, int hives, int sleepers) => mechs + beasts + hives + sleepers > 0;

        // ================= 17. A hostel on the road (the pilgrim fellowship) =================

        public const int TravellerGapMin = 8, TravellerGapMax = 16;
        public const int TravellersMax = 4;
        public const int TravellerStayMin = 18, TravellerStayMax = 40;
        /// <summary>The cough going round the crowded hut, each who comes to the blessing (through the harm budget).</summary>
        public const float HostelCough = 0.05f;
        /// <summary>Two peoples feuding (goodwill between them this low, not at war): their travellers may quarrel.</summary>
        public const int FeudGoodwill = -20;
        public const int BreadHour = 7;

        /// <summary>Food for the morning: one for each at the hostel, at least three.</summary>
        public static int Bread(int people) => Math.Max(3, people);

        /// <summary>Peoples feuding: goodwill between them below <see cref="FeudGoodwill"/> but not at war.</summary>
        public static bool Feuding(int goodwill, bool atWar) => !atWar && goodwill <= FeudGoodwill;

        // ================= 34. The lantern post (the Lantern Wardens) =================

        public const float PostDays = 10f;
        public const float NearCells = 30f;
        public const int WarnGapHours = 12;
        /// <summary>A night the beast comes to their fire (once a stay at most).</summary>
        public const float FireVisitChance = 0.12f;
        /// <summary>A day a hound is lost on the trail (once a stay at most).</summary>
        public const float HoundLostChance = 0.06f;
        public const int TrailHours = 20;
        public const int HoundsMeat = 30;

        // ================= 95. Kill it or win it over (the Wardens and the Circuit) =================

        /// <summary>The Warden's chance in a bout with the tamer: even, four points a level of difference, within 0.15 and 0.85.</summary>
        public static float WardenWins(int warden, int tamer) => Math.Max(0.15f, Math.Min(0.85f, 0.5f + (warden - tamer) * 0.04f));

        /// <summary>The game's own chance of a tamer's try (TameAnimalChance by the beast's wildness: twice at none, once at half, nothing at all).</summary>
        public static float TameChance(float tamerChance, float wildness)
        {
            if (float.IsNaN(tamerChance) || float.IsNaN(wildness)) return 0f;
            float w = Math.Max(0f, Math.Min(1f, wildness));
            return Math.Max(0f, Math.Min(1f, tamerChance * (2f - 2f * w)));
        }

        public const int TameTries = 6;
        public const int TameTryHours = 2;
        public const int SidedGoodwill = 4, PassedOverGoodwill = -5, BoutLoserGoodwill = -2;
        public const int BoutTicks = 2 * HourTicks / 3;
    }
}
