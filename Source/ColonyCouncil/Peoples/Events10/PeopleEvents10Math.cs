using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of the peoples' own events, round 10, the last (docs/peoples-events-proposals.md, items 96 to 100; Peoples/Events10/):
    /// the moot over the old wood (the Deepwell Union and the Mossborn), a toll on the road (the road league and the Wheel), the lamp and
    /// the bell in a plague year (the hospitaller order and the Quiet Bell), learning traded through the colony (the learned society and
    /// the Sunken Vault), and every people's art handed on. Shared with the tests (Tools/Tests/PeopleEvents10Tests.cs); nothing here knows
    /// the game.
    /// </summary>
    public static class PeopleEvents10Math
    {
        public const int DayTicks = 60000, HourTicks = 2500;

        public static int Round10(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0 : (int)Math.Round(v / 10f) * 10;

        private static int Clamp(int v, int lo, int hi) => Math.Max(lo, Math.Min(hi, v));

        private static float Clamp(float v, float lo, float hi) => float.IsNaN(v) ? lo : Math.Max(lo, Math.Min(hi, v));

        private static float Wealth(float wealthFactor) => float.IsNaN(wealthFactor) || float.IsInfinity(wealthFactor) ? 1f : wealthFactor;

        // ================= 96. The moot over the old wood =================

        /// <summary>The colony's best talker hears both envoys at its table this long, then gives the ruling.</summary>
        public const int HearTicks = 2 * HourTicks;

        /// <summary>The envoys wait at most this many hours for the hearing to begin; past that, the ruling goes to them by word.</summary>
        public const int HearWaitHours = 8;

        /// <summary>The colony's three rulings, in the card's order: for the shaft, for the wood, a bargain.</summary>
        public static readonly string[] Rulings = { "shaft", "wood", "bargain" };

        /// <summary>The side ruled for thinks better of the colony, the side ruled against worse (the moot's own sizes).</summary>
        public const int MootGain = 6, MootLoss = -6;

        /// <summary>A bargain struck: both a little better; a bargain that fails: both a little worse.</summary>
        public const int BargainGain = 3, BargainLoss = -2;

        /// <summary>A bargain's chance by the talker's Social: a quarter with none, four points a level, at most 85 in a hundred.</summary>
        public static float BargainChance(int social) => Clamp(0.25f + 0.04f * Math.Max(0, social), 0.25f, 0.85f);

        public static bool Bargained(float roll, int social) => !float.IsNaN(roll) && roll < BargainChance(social);

        /// <summary>What the talker learns at the hearing.</summary>
        public const float HearSocialXp = 500f;

        /// <summary>A season: the prices the ruling moves hold this many days.</summary>
        public const int SeasonDays = 15;

        /// <summary>
        /// The shaft sunk: steel a fifth cheaper with every trader for a season; the wood kept: the Mossborn's herbal medicine a fifth cheaper
        /// (the old wood's herbs); the bargain's shaft at the wood's edge: steel a tenth cheaper.
        /// </summary>
        public const float SteelFactor = 0.8f, HerbFactor = 0.8f, BargainSteelFactor = 0.9f;

        /// <summary>The thing whose price a ruling moves (a ThingDef's name), and by how much, or null for none.</summary>
        public static string PricedThing(string ruling, bool bargainStruck) =>
            ruling == "shaft" || ruling == "bargain" && bargainStruck ? "Steel" : ruling == "wood" ? "MedicineHerbal" : null;

        public static float PriceFactor(string ruling, bool bargainStruck) =>
            ruling == "shaft" ? SteelFactor : ruling == "wood" ? HerbFactor : ruling == "bargain" && bargainStruck ? BargainSteelFactor : 1f;

        /// <summary>A price with a season's factor on it (both the colony's buying and selling price move, so the spread between them holds).</summary>
        public static float Priced(float price, float factor) => float.IsNaN(price) ? price : price * Clamp(float.IsNaN(factor) ? 1f : factor, 0.5f, 1.5f);

        /// <summary>The season's prices kept at once, at most.</summary>
        public const int MaxPrices = 4;

        // ================= 97. A toll on the road =================

        /// <summary>The side the colony takes thinks better of it, the side passed over worse; a fair toll set, both a little better.</summary>
        public const int TollGain = 5, TollLoss = -5, FairGain = 3;

        /// <summary>The fair toll is set at the gate: the colony's best talker with both, this long.</summary>
        public const int FairTicks = 2 * HourTicks;

        /// <summary>The Wheel's cart train: a wheelwright, one or two hands, two or three pack beasts loaded with salvage.</summary>
        public const int WheelHandsMin = 1, WheelHandsMax = 2, WheelBeastsMin = 2, WheelBeastsMax = 3;

        /// <summary>The salvage in each pack beast's packs (steel), so their packs show.</summary>
        public const int CartLoad = 25;

        /// <summary>The fair toll the Wheel pays the league: 35 silver a pack beast, to the nearest ten.</summary>
        public static int Toll(int beasts) => Round10(Math.Max(1, beasts) * 35f);

        /// <summary>What the talker learns setting the toll.</summary>
        public const float FairSocialXp = 400f;

        // ================= 98. The lamp and the bell in a plague year =================

        /// <summary>The plague town is within this many tiles of the home.</summary>
        public const float PlagueTiles = 15f;

        /// <summary>Four to six days at the plague town.</summary>
        public const int PlagueMinDays = 4, PlagueMaxDays = 6;

        public static int PlagueDays(float roll) => PlagueMinDays + (int)Math.Floor(Clamp(roll, 0f, 0.999f) * (PlagueMaxDays - PlagueMinDays + 1));

        /// <summary>A day's work there: the doctor on the order's wards, the diggers at the Bell's graves.</summary>
        public const float DoctorXpPerDay = 800f, DiggerXpPerDay = 500f;

        /// <summary>
        /// The thanks as each comes home, their days served: the order +8 for its doctor, the Bell +4 for each digger (8 for the two). Sending
        /// both is remembered by both peoples as a deed of its own (<see cref="PlagueDeed"/>, FactionMemory: a heavy one, slow to fade).
        /// </summary>
        public const int DoctorGoodwill = 8, DiggerGoodwill = 4;

        public const string PlagueDeed = "plagueYear";

        /// <summary>The hours they lodged at the plague town, for the illness budget's own sickness brought home (Harm/SickHome.cs).</summary>
        public static float LodgedHours(float days) => float.IsNaN(days) || days <= 0f ? 0f : days * 24f;

        /// <summary>Both peoples asked: the order's doctor and the Bell's diggers.</summary>
        public const int Diggers = 2;

        // ================= 99. Learning traded through the colony =================

        /// <summary>Three days at the colony's table.</summary>
        public const int HostDays = 3;

        /// <summary>Each morning the copyist copies four hours with the technician beside; each afternoon the crafter teaches them two.</summary>
        public const int CopyTicks = 4 * HourTicks, LessonTicks = 2 * HourTicks;

        public static bool CopyHour(int hour) => hour >= 8 && hour <= 11;

        public static bool LessonHour(int hour) => hour >= 13 && hour <= 16;

        /// <summary>Which day of their stay it is (0 the first).</summary>
        public static int DayOf(int start, int now) => start < 0 || now < start ? 0 : (now - start) / DayTicks;

        /// <summary>The copy is whole after a morning for each day: then the society leaves the colony a book of it.</summary>
        public static bool CopyWhole(int mornings) => mornings >= HostDays;

        /// <summary>What the crafter learns teaching each afternoon, and the visitors from them.</summary>
        public const float LessonSocialXp = 250f, LessonCraftXp = 150f, VisitorCraftXp = 800f;

        /// <summary>The vault's craft learned again, as supply that holds its age (round G's craft's worth).</summary>
        public const float CraftWorth = 300f;

        /// <summary>Thanks as they go: with the lessons +4 each; the copying only, +2 each.</summary>
        public const int LearnGoodwill = 4, CopyGoodwill = 2;

        // ================= 100. Their art handed on =================

        /// <summary>A people at good terms with the colony (its goodwill at least this) may hand on its art, once a game.</summary>
        public const float ArtGoodwill = 50f;

        /// <summary>The afternoon one of theirs spends showing the colonist their art.</summary>
        public const int ArtTicks = 3 * HourTicks;

        /// <summary>A colonist whose passion already burns (or is a mod's own) is given a long lesson in it instead.</summary>
        public const float GreatLessonXp = 6000f;

        /// <summary>A passion one step up: none an interest, an interest a burning passion; -1: it can't go further (or is a mod's).</summary>
        public static int PassionAfter(int passion) => passion == 0 ? 1 : passion == 1 ? 2 : -1;

        /// <summary>Their thanks as the art is handed on, and more with the colony's gift in return.</summary>
        public const int ArtGoodwillGain = 4, ReturnGiftGoodwill = 4;

        /// <summary>The colony's gift in return: 250 silver by its wealth, 150 to 500.</summary>
        public static int ReturnGift(float wealthFactor) => Clamp(Round10(250f * Wealth(wealthFactor)), 150, 500);

        /// <summary>The latent traits of A RimWorld of Magic a class book needs: a mage's tome the magically gifted, a fighter's book the physically adept.</summary>
        public const string MageGift = "TM_Gifted", FighterGift = "PhysicalProdigy";

        /// <summary>
        /// Each calling's art: its class book of A RimWorld of Magic (the people's own class among its kinds, or the one the proposal names:
        /// the order the Priest's, the herders the Nature tome, the Wardens the Ranger's, the Bell the Earth tome, the Banner the Gladiator's,
        /// the Guild the Enchanter's, the Lattice Technomancy, the Choir the Bard's), the class trait it teaches and whether it is a fighter's,
        /// the skill a passion is found in without the mod, and the kind of person of theirs who comes to hand it on.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, (string tome, string trait, bool fighter, string skill, string teacher)> Arts =
            new Dictionary<string, (string, string, bool, string, string)>
            {
                ["traders"] = ("BookOfCommander", "TM_Commander", true, "Social", "Rimfolk_League_Master"),
                ["healers"] = ("BookOfPriest", "Priest", false, "Medicine", "Rimfolk_Order_Healer"),
                ["herders"] = ("BookOfDruid", "Druid", false, "Animals", "Rimfolk_Herders_Elder"),
                ["pilgrims"] = ("BookOfValiant", "Paladin", false, "Social", "Rimfolk_Pilgrims_Keeper"),
                ["scholars"] = ("BookOfArcanist", "Arcanist", false, "Intellectual", "Rimfolk_Scholars_Don"),
                ["brokers"] = ("BookOfPsionic", "TM_Psionic", true, "Social", "Rimfolk_Enclave_Factor"),
                ["fishers"] = ("BookOfDruid", "Druid", false, "Animals", "Rimfolk_RiverFolk_Master"),
                ["hunters"] = ("BookOfRanger", "Ranger", true, "Shooting", "Rimfolk_Wardens_Master"),
                ["sextons"] = ("BookOfEarth", "Geomancer", false, "Construction", "Rimfolk_QuietBell_Keeper"),
                ["tamers"] = ("BookOfDruid", "Druid", false, "Animals", "Rimfolk_Tamers_Tamer"),
                ["salvagers"] = ("BookOfMagitech", "Technomancer", false, "Crafting", "Rimfolk_Wheel_Wheelwright"),
                ["seers"] = ("BookOfStormBorn", "StormBorn", false, "Intellectual", "Rimfolk_Stormglass_Reader"),
                ["grovekeepers"] = ("BookOfDruid", "Druid", false, "Plants", "Rimfolk_Mossborn_Tender"),
                ["mercenaries"] = ("BookOfGladiator", "Gladiator", true, "Melee", "Rimfolk_AshenBanner_Sergeant"),
                ["artisans"] = ("BookOfEnchanter", "Enchanter", false, "Artistic", "Rimfolk_KilnGuild_Journeyman"),
                ["linesmen"] = ("BookOfMagitech", "Technomancer", false, "Construction", "Rimfolk_SignalLine_RelayKeeper"),
                ["drillers"] = ("BookOfEarth", "Geomancer", false, "Mining", "Rimfolk_Deepwell_ShotFirer"),
                ["citizens"] = ("BookOfBard", "TM_Bard", false, "Social", "Rimfolk_Republic_Clerk"),
                ["crew"] = ("BookOfCommander", "TM_Commander", true, "Intellectual", "Rimfolk_HollowFleet_Officer"),
                ["editors"] = ("BookOfChronomancer", "Chronomancer", false, "Medicine", "Rimfolk_HelixWorks_Sequencer"),
                ["mechanitors"] = ("BookOfMagitech", "Technomancer", false, "Crafting", "Rimfolk_Lattice_MechTender"),
                ["listeners"] = ("BookOfBard", "TM_Bard", false, "Artistic", "Rimfolk_Choir_Cantor"),
                ["vaultfolk"] = ("BookOfSuperSoldier", "TM_SuperSoldier", true, "Crafting", "Rimfolk_SunkenVault_Technician")
            };

        /// <summary>The latent trait this calling's book needs, or null for a calling without one.</summary>
        public static string GiftFor(string calling) => calling != null && Arts.TryGetValue(calling, out var a) ? a.fighter ? FighterGift : MageGift : null;

        /// <summary>
        /// What the people hands on: "tome" (the mod is there, the class is on, the colonist has the gift its book needs), "passion" (the
        /// skill can still grow a passion), "lesson" (it burns already, or is a mod's own), or null (the skill is closed to them).
        /// </summary>
        public static string ArtKind(bool tomeReadable, bool skillOpen, int passion) =>
            tomeReadable ? "tome" : !skillOpen ? null : PassionAfter(passion) >= 0 ? "passion" : "lesson";
    }
}
