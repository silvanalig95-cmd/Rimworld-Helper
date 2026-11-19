using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of the peoples' own events, round 4 (docs/peoples-events-proposals.md, items 18, 21, 23, 24, 27, 28, 29,
    /// 30, 31, 32 and 33; Peoples/Events4/): the learned society's fellowship examination and its summer school, the spacer
    /// enclave's contract and its broker stranded by a pod crash, the river folk's fouled water, their lanterns on the water,
    /// their families flooded out and their boat-builders, and the Lantern Wardens' page in the book, their help at a manhunt
    /// and the hive they burn. Shared with the tests (Tools/Tests/PeopleEvents4Tests.cs); nothing here knows the game.
    /// </summary>
    public static class PeopleEvents4Math
    {
        public const int DayTicks = 60000, HourTicks = 2500;

        public static int Round10(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0 : (int)Math.Round(v / 10f) * 10;

        private static float Clamp(float v, float lo, float hi) => float.IsNaN(v) ? lo : Math.Max(lo, Math.Min(hi, v));

        private static int Clamp(int v, int lo, int hi) => Math.Max(lo, Math.Min(hi, v));

        /// <summary>The colony's wealth as the events read it (LivingEvents.WealthFactor), kept within reason.</summary>
        private static float Wealth(float f) => Clamp(f, 0.5f, 2.5f);

        // ================= 18. The fellowship examined (the learned society) =================

        /// <summary>A colonist at this Intellectual is examined; the summer school's best student at the lower level.</summary>
        public const int ExamLevel = 12, SchoolLevel = 10;

        /// <summary>Whoever reaches the level may be examined: the school's best student sooner.</summary>
        public static bool Examinable(int intellectual, bool bestStudent) => intellectual >= (bestStudent ? SchoolLevel : ExamLevel);

        /// <summary>Failed (or the examination declined), not examined again for this many days.</summary>
        public const int ExamGapDays = 60;

        /// <summary>
        /// The chance the examination goes against them, on the card: before the colony, Intellectual and Social both count
        /// (3.5% a level each, as answers' risks are); in private, Intellectual alone, and a fail is not seen by anyone.
        /// </summary>
        public static float ExamFail(int intellectual, int social, bool before) =>
            Clamp(0.95f - 0.035f * Math.Max(0, intellectual) - (before ? 0.035f * Math.Max(0, social) : 0f), 0.05f, 0.95f);

        /// <summary>What the examination teaches whatever the verdict (Intellectual).</summary>
        public const float ExamXp = 600f;

        /// <summary>A corresponding fellow is sent a book this often (a season), by the post.</summary>
        public const int BookEveryDays = 15;

        /// <summary>The society's thanks for a fellow named; the corresponding fellows the record keeps.</summary>
        public const int FellowGoodwill = 4, MaxFellows = 12;

        /// <summary>The books the society sends by the post (the game's own and the player's mods', by name), the textbook first; never Anomaly's tome.</summary>
        public static readonly string[] BookDefs = { "TextBook", "Schematic", "Novel", "VCEV_CookingBook" };

        /// <summary>How long the private examination takes (the fellow and the colonist, with a bar).</summary>
        public const int PrivateTicks = 3000;

        // ================= 21. The summer school (the learned society) =================

        /// <summary>Lessons each afternoon at their table, so many a day at most.</summary>
        public const int LessonFromHour = 13, LessonToHour = 18, LessonsPerDay = 2;

        public static bool LessonHour(int hour) => hour >= LessonFromHour && hour <= LessonToHour;

        /// <summary>A lesson's learning an hour: more with the colony's books on their table.</summary>
        public static float LessonXp(bool books) => books ? 225f : 150f;

        /// <summary>How many of its books the colony lends them at most.</summary>
        public const int LendBooks = 3;

        /// <summary>Once a stay, at this chance, two fellows fall into a disputation; it ends in a sulk if nobody ends it in this many hours.</summary>
        public const float DisputeChance = 0.35f;

        public const int DisputeHours = 4;

        /// <summary>The colony's talker ends the disputation by their Social.</summary>
        public static float SettleOdds(int social) => Clamp(0.35f + 0.04f * Math.Max(0, social), 0.35f, 0.9f);

        public const float SulkTension = 10f, SettledTension = -5f;

        /// <summary>The society's thanks after a stay with lessons taken; turned away, nothing.</summary>
        public const int SchoolGoodwill = 4;

        /// <summary>The best student: the one with the most lessons, at least this many.</summary>
        public static bool BestStudent(int lessons) => lessons >= 2;

        // ================= 23. A contract to the last digit (the spacer enclave) =================

        /// <summary>What a contract can be for: the colony's meals, cloth, beer, drugs, art or components.</summary>
        public static readonly string[] ContractKinds = { "meals", "cloth", "beer", "drugs", "art", "components" };

        public const int ContractMinDays = 15, ContractMaxDays = 30;

        /// <summary>A fifth over its worth; after a default, a tenth for a while ("cooler terms").</summary>
        public const float Premium = 1.2f, CoolPremium = 1.1f;

        /// <summary>A contract's worth: by the colony's wealth and how its last contracts went (a bigger one after one kept).</summary>
        public static int ContractWorth(float wealthFactor, float scale) => Clamp(Round10(500f * Wealth(wealthFactor) * Clamp(scale, 0.5f, 3f)), 300, 3000);

        /// <summary>How many of the thing make up that worth.</summary>
        public static int ContractCount(int worth, float unitValue) =>
            float.IsNaN(unitValue) || unitValue <= 0f ? 0 : Math.Max(1, (int)Math.Round(worth / unitValue));

        /// <summary>What they pay for it delivered in full.</summary>
        public static int ContractPrice(int count, float unitValue, bool cool) =>
            Math.Max(10, Round10(Math.Max(0, count) * Math.Max(0f, float.IsNaN(unitValue) ? 0f : unitValue) * (cool ? CoolPremium : Premium)));

        /// <summary>Delivered short: what was handed over at its worth, no premium, less the default's fee.</summary>
        public static int ShortPay(int delivered, float unitValue, int fee) =>
            Math.Max(0, Round10(Math.Max(0, delivered) * Math.Max(0f, float.IsNaN(unitValue) ? 0f : unitValue)) - Math.Max(0, fee));

        /// <summary>A default's fee: a tenth of the price, at least thirty.</summary>
        public static int DefaultFee(int price) => Math.Max(30, Round10(Math.Max(0, price) * 0.1f));

        /// <summary>The next contract's size: half again after one kept (three times at most), back to the first after a default.</summary>
        public static float NextScale(float scale, bool kept) => kept ? Math.Min(3f, Clamp(scale, 0.5f, 3f) * 1.5f) : 1f;

        public const int KeptGoodwill = 4, DefaultGoodwill = -6, CoolDays = 60;

        // ================= 24. Stranded with something to sell (the spacer enclave) =================

        /// <summary>The cargo: spacer goods, by name, the game's own.</summary>
        public static readonly string[] CargoDefs = { "Plasteel", "ComponentSpacer", "ComponentIndustrial", "MedicineIndustrial", "Uranium" };

        /// <summary>What the cargo is worth, by the colony's wealth.</summary>
        public static int CargoWorth(float wealthFactor) => Clamp(Round10(900f * Wealth(wealthFactor)), 500, 2500);

        /// <summary>Their pay for the rescue: a fifth of the cargo.</summary>
        public static int Share(int cargoWorth) => Math.Max(20, Round10(Math.Max(0, cargoWorth) / 5f));

        /// <summary>Their lander comes for the broker this many hours after a rescue; left lying, the next morning.</summary>
        public const int LanderMinHours = 48, LanderMaxHours = 72, LanderLeftHours = 14;

        /// <summary>How many crates the cargo comes down in.</summary>
        public static int CargoCrates(float roll) => 2 + (float.IsNaN(roll) || roll < 0.5f ? 0 : 1);

        public const int RescueGoodwill = 10, RobbedGoodwill = -20, RobbedWeight = 25;

        /// <summary>The cargo is all there when they come for it, else what is missing was taken.</summary>
        public static bool Robbed(int crates, int present) => present < crates;

        // ================= 27. Fouled water (the river folk) =================

        /// <summary>What fouls water: a body lying in it, the land poisoned beside it (Biotech), a wastepack by it.</summary>
        public static readonly string[] FoulKinds = { "corpse", "pollution", "wastepack" };

        /// <summary>A wastepack or poisoned ground this close to the water fouls it; a body this long in it.</summary>
        public const int FoulReach = 3;

        public const float CorpseDays = 0.5f;

        /// <summary>The work at each fouled spot (a bar), and how many spots a visit takes on.</summary>
        public const int CleanTicks = 1200, MaxSpots = 8;

        /// <summary>Cleaned with the colony's hands: +5; the warden alone: +2. Left: a wrong in their memory, the catches stopped.</summary>
        public const int CleanGoodwill = 5, AloneGoodwill = 2, LeftWeight = 8;

        /// <summary>The boat-trader and the nets stop coming until it is clean again, this long at most.</summary>
        public const int StopDays = 60;

        /// <summary>The spot is clean again: nothing of it left.</summary>
        public static bool Clean(int left) => left <= 0;

        // ================= 28. Lanterns on the water (the river folk) =================

        /// <summary>A lantern for everyone the colony lost or took in, three to twelve; at most six set on the water.</summary>
        public static int Lanterns(int lost, int takenIn) => Clamp(Math.Max(0, lost) + Math.Max(0, takenIn), 3, 12);

        public const int LanternsSet = 6;

        /// <summary>The knot night's dusk, when they come.</summary>
        public const int LanternFromHour = 16, LanternToHour = 19;

        public static bool LanternHour(int hour) => hour >= LanternFromHour && hour <= LanternToHour;

        public const int LanternGoodwill = 2;

        // ================= 29. Flooded out (the river folk) =================

        /// <summary>They ask for food until their catch comes in: the first days.</summary>
        public const int FoodDays = 3;

        /// <summary>Once a stay, at this chance, through the harm and illness budgets, a sickness comes home with them.</summary>
        public const float FloodSickChance = 0.06f;

        /// <summary>Their drying racks for the catch, from the player's mods by name (VFE Classical's, else Vanilla Furniture Expanded - Props and Decor's); none without them.</summary>
        public static readonly string[] RackDefs = { "VFEC_MeatDryingRack", "VFEPD_MeatDryingRack" };

        /// <summary>Their old and young taken in as guests (Hospitality): so many at most.</summary>
        public const int GuestsMax = 3;

        /// <summary>A knot in the net for the colony as they go; turned away, cooler.</summary>
        public const int KnotGoodwill = 12, FloodTurnedGoodwill = -3;

        // ================= 30. The boat-builders (the river folk) =================

        /// <summary>The hull's stage by its days of work: nothing, the keel, the ribs, the planked hull.</summary>
        public static int HullStage(float daysWorked) => float.IsNaN(daysWorked) || daysWorked < 1f ? 0 : daysWorked < 3f ? 1 : daysWorked < 5f ? 2 : 3;

        public const int HullStages = 3;

        /// <summary>Wild trees enough on the land for a hull.</summary>
        public const int MinTrees = 10;

        /// <summary>The finished dinghy (Vehicle Framework with Vanilla Vehicles Expanded): wood and silver.</summary>
        public const int DinghyWood = 60;

        public static int DinghySilver(float wealthFactor) => Clamp(Round10(350f * Wealth(wealthFactor)), 200, 800);

        /// <summary>Heavy rain lifts the water: a day of work lost, once a stay.</summary>
        public const float StormRain = 0.5f;

        public const int TreesRefusedGoodwill = -2, BoatGoodwill = 3;

        // ================= 31. A page in the book (the Lantern Wardens) =================

        /// <summary>The kills that make a page: a legend of the region, a beast nemesis, a pack's alpha.</summary>
        public static readonly string[] PageKinds = { "legend", "nemesis", "alpha" };

        /// <summary>A Warden comes some hours after the kill (the hide off by then), not after this many days.</summary>
        public const int PageMinHours = 6, PageExpireDays = 10;

        /// <summary>Each hour once a page waits, at this chance (the storytellers' lean on visitors).</summary>
        public const float PageChance = 0.15f;

        /// <summary>The pelt for their hall, at twice its worth.</summary>
        public static int PeltPrice(int count, float unitValue) => Math.Max(20, Round10(Math.Max(0, count) * Math.Max(0f, float.IsNaN(unitValue) ? 0f : unitValue) * 2f));

        /// <summary>How much of the pelt they want: the beast's hide as the game counts it, at most what the colony has.</summary>
        public static int PeltCount(float leatherAmount, int have) => Math.Max(0, Math.Min(Math.Max(0, have), (int)Math.Round(float.IsNaN(leatherAmount) ? 0f : leatherAmount)));

        public const int PageGoodwill = 6, MaxPages = 6;

        /// <summary>Without Vanilla Skills Expanded, a passion in the skill that made the kill.</summary>
        public static string KillSkill(bool ranged) => ranged ? "Shooting" : "Melee";

        /// <summary>How long the cord takes to tie at the gate.</summary>
        public const int CordTicks = 900;

        // ================= 32. Wardens at the manhunt (the Lantern Wardens) =================

        /// <summary>The colony is beset: a manhunting pack (three or more), or one mad beast of a size.</summary>
        public static bool Beset(int manhunters, float biggest) => manhunters >= 3 || manhunters >= 1 && !float.IsNaN(biggest) && biggest >= 1f;

        /// <summary>Two to four Wardens, by the size of the pack.</summary>
        public static int ManhuntWardens(int manhunters) => Clamp(2 + Math.Max(0, manhunters) / 4, 2, 4);

        /// <summary>Once a manhunt, at this chance, an hour or two after it began; not soon again.</summary>
        public const float ManhuntChance = 0.4f;

        public const int ManhuntMinHours = 1, ManhuntMaxHours = 2, ManhuntGapDays = 15, ManhuntGoodwill = 3;

        // ================= 33. The hive burned (the Lantern Wardens) =================

        /// <summary>The hives they burn: the game's, and Vanilla Factions Expanded - Insectoids 2's wild hives and burrows.</summary>
        public static readonly string[] HiveDefs =
        {
            "Hive", "VFEI2_ChelisHive", "VFEI2_KemianHive", "VFEI2_NuchadusHive", "VFEI2_XanidesHive", "VFEI2_SmallBurrow", "VFEI2_MediumBurrow", "VFEI2_LargeBurrow"
        };

        /// <summary>Their fire by their age: torches, molotovs, an incendiary launcher.</summary>
        public static string Fire(int age) => age <= 1 ? "torch" : age == 2 ? "molotov" : "incendiary";

        /// <summary>What they carry it in: nothing but the torch, the game's molotov, its incendiary launcher.</summary>
        public static string FireWeapon(int age) => age <= 1 ? null : age == 2 ? "Weapon_GrenadeMolotov" : "Gun_IncendiaryLauncher";

        /// <summary>Three to five Wardens, by the hives.</summary>
        public static int HiveWardens(int hives) => Clamp(3 + Math.Max(0, hives) / 3, 3, 5);

        /// <summary>Together, they wait this long at the edge for the colony's fighters; refused, they keep watch this long.</summary>
        public const int HiveWaitHours = 3, HiveWatchHours = 24, HiveDays = 2;

        /// <summary>The torch held to a hive.</summary>
        public const int BurnTicks = 300;

        public const int HiveGoodwill = 4;
    }
}
