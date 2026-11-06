using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The rules without game types behind the fourth round of the council and the colony's year (the player's request,
    /// 6 November 2026): the council's questions about the fields, the snow, the building plans, the trees by the wall, the
    /// herd's winter feed, an animal's training, the travellers home from the road, a doubter of the faith and the faith
    /// without a guide (Dilemmas/DilemmaWorkers_YearFour.cs); the decrees of the land's year and of the power and the faiths
    /// (Decrees/DecreeWork.cs); and the advisor's checks that go with them (Advisor/AdvisorSignals_YearFour.cs). Kept apart
    /// so the tests can check them (Tools/Tests/YearFourTests.cs).
    /// </summary>
    public static class YearFourMath
    {
        // ---------------- The empty field ----------------

        /// <summary>A field worth the council's time: this many cells at least.</summary>
        public const int FieldCells = 20;

        /// <summary>Under this share of a field sown in the growing season, it lies empty.</summary>
        public const float EmptyShare = 0.4f;

        public static bool FieldEmpty(int sown, int cells) => cells >= FieldCells && sown < cells * EmptyShare;

        // ---------------- What the field grows ----------------

        /// <summary>Medicine short: under one dose a colonist (and under this many in all).</summary>
        public const int MedicineFew = 8;

        public static bool MedicineShort(int medicine, int colonists) => colonists > 0 && medicine < colonists && medicine < MedicineFew;

        /// <summary>Cloth short: under this much a colonist, enough for a shirt or two each.</summary>
        public const int ClothEach = 25;

        public static bool ClothShort(int cloth, int colonists) => colonists > 0 && cloth < colonists * ClothEach;

        /// <summary>Food plenty enough to give a field to something else: this many days at least.</summary>
        public const float FoodPlentyDays = 20f;

        // ---------------- Snow on the paths ----------------

        /// <summary>Snow this deep is hard going (the game's own depth, 0 to 1).</summary>
        public const float DeepSnow = 0.5f;

        /// <summary>The share of the home area's open ground under deep snow for it to be the council's business.</summary>
        public const float SnowShare = 0.3f;

        /// <summary>A snow-clearing area this small or smaller is as good as none.</summary>
        public const int SnowAreaSmall = 20;

        /// <summary>The paths: open cells this close to a door to the outside are cleared first.</summary>
        public const float PathReach = 3.9f;

        /// <summary>At most this many cells are added to the snow-clearing area at once.</summary>
        public const int SnowCellsMost = 400;

        public static bool Snowed(int deep, int open, int clearArea) => open > 0 && deep >= open * SnowShare && clearArea <= SnowAreaSmall;

        /// <summary>Above this outdoor temperature the snow is melting by itself: nobody asks for it cleared.</summary>
        public const float SnowMelts = 5f;

        // ---------------- The unfinished work ----------------

        /// <summary>This many plans standing (blueprints and frames) at least.</summary>
        public const int Plans = 8;

        public static bool ManyPlans(int plans) => plans >= Plans;

        // ---------------- The trees by the wall ----------------

        /// <summary>A grown tree this close to the colony's outer wall gives an attacker cover.</summary>
        public const float TreeReach = 6.9f;

        /// <summary>This many such trees at least.</summary>
        public const int Trees = 5;

        /// <summary>A tree is grown enough to hide behind (the game's growth, 0 to 1).</summary>
        public const float TreeGrown = 0.5f;

        public static bool TreeLine(int trees) => trees >= Trees;

        /// <summary>
        /// The trees by the wall and the snow on the paths are also a colonist's wish (Rimfolk_Wish_TreesByWalls,
        /// Rimfolk_Wish_SnowWays, 0.121.0) and an advisor's check. The question and the wish take turns: the question
        /// waits while the wish is open, and the wish waits this long after the question was asked.
        /// </summary>
        public const int QuietAfterAskedDays = 30;

        public static bool AskedLately(int lastAsked, int now) => lastAsked >= 0 && now - lastAsked < QuietAfterAskedDays * 60000;

        /// <summary>The nearest half, rounded up.</summary>
        public static int Half(int n) => n <= 0 ? 0 : (n + 1) / 2;

        // ---------------- Too many in the pens ----------------

        /// <summary>The herd that needs feeding through the winter: this many grazing animals at least.</summary>
        public const int Herd = 6;

        /// <summary>The cold that keeps the grass from growing is this close, or here.</summary>
        public const float WinterWithinDays = 10f;

        /// <summary>The feed in store lasts under this share of the winter: the herd is too big for it.</summary>
        public const float FeedShort = 0.6f;

        /// <summary>At most this many are marked at once, and never more than a third of the herd.</summary>
        public const int CullMost = 4;

        /// <summary>Days the feed in store lasts the herd (nutrition in store over what the herd eats in a day).</summary>
        public static float FeedDays(float feed, float herdPerDay) => herdPerDay <= 0f ? 9999f : Math.Max(0f, feed) / herdPerDay;

        public static bool HerdTooBig(int herd, float feedDays, float winterDays) => herd >= Herd && winterDays > 0f && feedDays < winterDays * FeedShort;

        /// <summary>
        /// How many to mark so the feed lasts the winter: the share it falls short by, of the herd, at least one, at most
        /// <see cref="CullMost"/> and a third of the herd.
        /// </summary>
        public static int ToCull(int herd, float feedDays, float winterDays)
        {
            if (herd <= 0 || winterDays <= 0f) return 0;
            float lasts = Math.Max(0f, Math.Min(1f, feedDays / winterDays));
            int n = (int)Math.Ceiling(herd * (1f - lasts));
            return Math.Max(1, Math.Min(n, Math.Min(CullMost, Math.Max(1, herd / 3))));
        }

        // ---------------- Home from the road ----------------

        /// <summary>Away this long at least (the calendar's homecoming).</summary>
        public const float RoadDays = 5f;

        /// <summary>Home this recently at most, for the council to ask.</summary>
        public const float HomeWithinDays = 1.5f;

        public static bool FreshHome(float daysAway, float daysHome) => daysAway >= RoadDays && daysHome >= 0f && daysHome <= HomeWithinDays;

        // ---------------- A doubter of the faith ----------------

        /// <summary>Under this certainty, someone doubts the faith they hold.</summary>
        public const float Doubting = 0.35f;

        public static bool Doubts(float certainty) => certainty < Doubting;

        /// <summary>Certainty an hour with the faith's guide gives back, and an hour alone at its altar.</summary>
        public const float GuideSteadies = 0.08f, AltarSteadies = 0.05f;

        // ---------------- Decrees of the land's year ----------------

        /// <summary>A hunting season marks this many head of safe game a morning.</summary>
        public const int HuntADay = 2;

        /// <summary>Foraging days mark this many wild plants ripe for picking a morning, within this reach of the colony.</summary>
        public const int ForageADay = 12;

        public const float ForageReach = 45f;

        /// <summary>Wood for the winter: this many trees marked a morning, until the stores hold this much wood a colonist.</summary>
        public const int TreesADay = 6;

        public const int WoodEachLowTech = 150, WoodEach = 80;

        public static int WoodWanted(int colonists, bool lowTech) => Math.Max(1, colonists) * (lowTech ? WoodEachLowTech : WoodEach);

        /// <summary>Trees to mark today: none once the stores hold what's wanted, else up to a morning's, at about this much wood a tree.</summary>
        public static int TreesToday(int wood, int wanted, float woodATree, int markedAlready)
        {
            if (wood >= wanted) return 0;
            float each = woodATree <= 0f ? 25f : woodATree;
            int need = (int)Math.Ceiling((wanted - wood) / each) - Math.Max(0, markedAlready);
            return Math.Max(0, Math.Min(TreesADay, need));
        }

        // ---------------- Saving the power ----------------

        /// <summary>The lamps go out at this hour and come on again at this one.</summary>
        public const int LightsOutFrom = 23, LightsOutTo = 5;

        public static bool LightsOut(int hour) => hour >= LightsOutFrom || hour < LightsOutTo;

        /// <summary>A lamp: a light that draws this much power or less (a standing lamp, not a sun lamp over a field).</summary>
        public const float LampWatts = 150f;

        /// <summary>Power short: the grid's batteries under this share full at dusk.</summary>
        public const float StoredLow = 0.3f;

        public static bool PowerShort(float stored, float capacity) => capacity > 0f && stored < capacity * StoredLow;

        // ---------------- Every faith its evening ----------------

        /// <summary>
        /// Trust a morning for someone of another faith than the colony's while every faith has its evening: more the more
        /// open they are (a quarter to a half of a point).
        /// </summary>
        public static float ToleranceTrust(float openness) => 0.25f + 0.25f * Math.Max(0f, Math.Min(1f, openness));

        /// <summary>Trust a morning for the devout of the colony's own faith, who would rather it led.</summary>
        public const float DevoutMinds = -0.25f;

        // ---------------- The advisor ----------------

        /// <summary>A generator with fuel for under this many hours and none in the stores to feed it.</summary>
        public const float FuelHours = 12f;

        public static bool FuelLow(float fuel, float perHour, int inStores) => perHour > 0f && fuel / perHour < FuelHours && inStores <= 0;

        /// <summary>Plans waiting on stuff the stores don't hold: this many at least.</summary>
        public const int Waiting = 4;

        /// <summary>An animal out in the cold: this many degrees below the coldest it can bear, outdoors.</summary>
        public const float ColdMargin = 0f;

        public static bool TooCold(float outdoor, float comfortMin) => outdoor < comfortMin - ColdMargin;
    }
}
