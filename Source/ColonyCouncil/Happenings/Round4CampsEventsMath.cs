using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The rules of the content round 4 without game types (docs/content-round-4-camps-events.md): when its six camps and eight
    /// map events may come, how long what they keep on the map lasts, how many come and how often a risk named under an answer
    /// happens, before the shared harm budget's scale. The camps' ways are Camps/CampRound4.cs, the events'
    /// Happenings/Workers_Round4.cs.
    /// </summary>
    public static class Round4CampsEventsMath
    {
        // ================= R4-C1. Woodcarvers =================

        /// <summary>Wild trees on the land, clear of the colony, for a carvers' camp.</summary>
        public const int CarversTreesFrom = 20;

        /// <summary>The hour {A} sits with the carver before the carving is done.</summary>
        public const float SitHours = 1f;

        /// <summary>How long they wait for the one sent to sit with them before the silver goes back.</summary>
        public const float SitWaitDays = 0.5f;

        /// <summary>The quality of a carving of theirs by a roll: mostly normal, a good one now and then, never poor.</summary>
        public static int CarvingQuality(float roll) => roll < 0.65f ? 2 : roll < 0.95f ? 3 : 4;

        // ================= R4-C2. Fire-watchers =================

        /// <summary>The heat a fire-watch comes in, outdoors now.</summary>
        public const float FireHeatFrom = 24f;

        /// <summary>A fire in the open this near their fire sends them running.</summary>
        public const float FireReach = 60f;

        /// <summary>How many of them run to a fire.</summary>
        public const int Beaters = 2;

        /// <summary>The firebreak: this wide outside the field, at most this many plants cut, by the working hours.</summary>
        public const int BreakWidth = 2, BreakMost = 40, BreakFrom = 8, BreakTo = 17;

        /// <summary>A fire-watch comes: hot, dry now, on dry land.</summary>
        public static bool FireWatchFits(float outdoorTemp, bool raining, bool wetLand) => outdoorTemp >= FireHeatFrom && !raining && !wetLand;

        /// <summary>The working hours of the firebreak.</summary>
        public static bool BreakHour(int hour) => hour >= BreakFrom && hour <= BreakTo;

        // ================= R4-C3. Wildfowlers =================

        /// <summary>The flock that comes in with them.</summary>
        public const int FlockMin = 4, FlockMax = 6;

        /// <summary>Open water clear of the colony they need: this many cells near one another.</summary>
        public const int WaterCellsFrom = 30;

        /// <summary>They stay about the water this near the spot.</summary>
        public const float FlockRange = 10f;

        /// <summary>Dawn, when one of them goes out after a bird; at most this many birds a day.</summary>
        public const int DawnFrom = 5, DawnTo = 8, BirdsADay = 1;

        public static bool Dawn(int hour) => hour >= DawnFrom && hour <= DawnTo;

        // ================= R4-C4. Snow-carvers =================

        /// <summary>Snow this deep (of the game's 0 to 1) at the colony's middle, and freezing.</summary>
        public const float SnowFrom = 0.3f;

        /// <summary>At most this many figures in a stay; a figure takes this many turns of work.</summary>
        public const int FiguresMost = 5, TurnsPerFigure = 3;

        /// <summary>{A} works with them this long before the figure stands.</summary>
        public const float ShapeHours = 1f;

        public static bool SnowFits(float snowDepth, float outdoorTemp) => snowDepth >= SnowFrom && outdoorTemp <= 0f;

        // ================= R4-C5. Cheesemakers =================

        /// <summary>Once a day, this share of the milk in their store becomes cheese (at most this much milk).</summary>
        public const float CheeseShare = 0.5f;
        public const int CheeseMilkMost = 30;

        /// <summary>The cheese a day's milk makes: half of it (rounded down), at most half the most.</summary>
        public static int CheeseOf(int milk) => milk <= 1 ? 0 : (int)(Math.Min(milk, CheeseMilkMost) * CheeseShare);

        // ================= R4-C6. Instrument-makers =================

        /// <summary>
        /// The instruments of an age, first first (Ideology's drum, Royalty's harp, harpsichord and piano, Vanilla Furniture
        /// Expanded's upright piano), the nearest ages' after so each has one where any exists.
        /// </summary>
        public static string[] Instruments(string ageTag)
        {
            switch (ageTag)
            {
                case "tribal": return new[] { "Drum", "Harp" };
                case "medieval": return new[] { "Harp", "Drum", "Harpsichord" };
                case "industrial": return new[] { "Joy_Piano", "Piano", "Harp" };
                default: return new[] { "Harpsichord", "Piano", "Joy_Piano" };
            }
        }

        /// <summary>The evening hours one of them plays.</summary>
        public static bool PlayHour(int hour) => hour >= 18 && hour <= 21;

        // ================= R4-E1. Crows on the new field =================

        /// <summary>Young plants in the field (below this growth) for it to count as freshly sown, and how many.</summary>
        public const float YoungBelow = 0.25f;
        public const int SeedlingsFrom = 12;

        /// <summary>The crows that come; how many the hunters take.</summary>
        public const int CrowsMin = 5, CrowsMax = 8, CrowsShot = 2;

        /// <summary>Every this many hours they pull up a seedling or two, at most this many in all; let be, they go at dusk.</summary>
        public const float PeckEveryHours = 2f;
        public const int PeckMost = 10, CrowsLeaveHour = 18;

        public static bool CrowsFit(bool spring, int seedlings, bool kindCanLive) => spring && seedlings >= SeedlingsFrom && kindCanLive;

        /// <summary>Seedlings they pull up at one peck: one, two now and then, never past the most.</summary>
        public static int Peck(int lostSoFar, float roll) => lostSoFar >= PeckMost ? 0 : Math.Min(PeckMost - lostSoFar, roll < 0.3f ? 2 : 1);

        // ================= R4-E2. Meltwater in the deep rooms =================

        /// <summary>Above this, the snow melts; the water runs in this long, a new puddle or two every this many hours.</summary>
        public const float MeltFrom = 2f, MeltHours = 36f, MeltEveryHours = 3f;

        /// <summary>The puddles reach this far into the room from the door, at most this many at once.</summary>
        public const float MeltReach = 4f;
        public const int MeltMost = 12;

        public static bool MeltFits(bool spring, float outdoorTemp, bool snowyWinter) => spring && outdoorTemp >= MeltFrom && snowyWinter;

        // ================= R4-E3. Midges at dusk =================

        /// <summary>Warm enough for midges; wet ground this near a field, this many cells of it.</summary>
        public const float MidgeHeatFrom = 16f, WetReach = 12f;
        public const int WetCellsFrom = 10;

        /// <summary>Two evenings of it; dusk from this hour to this; walked in from this hour.</summary>
        public const int Evenings = 2, DuskFrom = 18, DuskTo = 21, InHour = 17;

        /// <summary>Bitten at dusk: one chance in fifteen for each of those out there of an ailment, once an evening, before the budget's scale.</summary>
        public const float BiteChance = 0.066f;

        public static bool MidgesFit(bool summer, float outdoorTemp, int wetCells) => summer && outdoorTemp >= MidgeHeatFrom && wetCells >= WetCellsFrom;

        public static bool Dusk(int hour) => hour >= DuskFrom && hour <= DuskTo;

        // ================= R4-E4. What the storm threw up =================

        /// <summary>The heaps it leaves, and their size.</summary>
        public const int HeapsMin = 5, HeapsMax = 8, HeapFrom = 4, HeapTo = 10;

        /// <summary>Left, the sea takes it back at this hour (or this long after it was found, at most).</summary>
        public const int SeaTakesHour = 21;
        public const float SeaTakesMostHours = 30f;

        /// <summary>Only the nearest: the nearer half of the heaps, one at least.</summary>
        public static int Nearest(int heaps) => heaps <= 0 ? 0 : Math.Max(1, heaps / 2);

        // ================= R4-E5. Ice on the paths =================

        /// <summary>Freezing hard enough for ice, and it lasts at most this long.</summary>
        public const float IceBelow = -2f, IceMostHours = 36f;

        /// <summary>The two doors used most, for the doors only.</summary>
        public const int BusyDoors = 2;

        /// <summary>Left icy: for one walking out on the colony's ground, one chance in two hundred an hour of a sprained ankle, before the budget's scale.</summary>
        public const float SlipChancePerHour = 0.005f;

        public static bool IceFits(bool winter, float outdoorTemp, bool wetLately, int outerDoors) => winter && outdoorTemp <= IceBelow && wetLately && outerDoors >= 2;

        /// <summary>The ice is gone: thawed, or its time is up.</summary>
        public static bool IceGone(float outdoorTemp, float hoursSince) => outdoorTemp > 0f || hoursSince >= IceMostHours;

        // ================= R4-E6. The old tree by the house =================

        /// <summary>The colony's wood is short below this much a person; the tree this near its buildings, this big.</summary>
        public const int WoodShortPerPerson = 40;
        public const float TreeNear = 8f;
        public const int TreeYieldFrom = 40;

        public static bool WoodShort(int logs, int people) => people > 0 && logs < WoodShortPerPerson * people;

        // ================= R4-E7. Stones in the field =================

        /// <summary>A field this big at least; the stones the frost pushes up.</summary>
        public const int FieldFrom = 40, StonesMin = 3, StonesMax = 6;

        /// <summary>A winter that freezes: the coldest of the year's months below this.</summary>
        public const float FrostBelow = -2f;

        public static bool StonesFit(bool spring, float coldestMonth, int fieldCells, int openCells) =>
            spring && coldestMonth < FrostBelow && fieldCells >= FieldFrom && openCells >= StonesMax;

        // ================= R4-E8. Washing day =================

        /// <summary>A warm, dry morning.</summary>
        public const float WashWarmFrom = 16f;
        public const int WashFrom = 7, WashTo = 11;

        public static bool WashingFits(float outdoorTemp, bool raining, int hour) => outdoorTemp >= WashWarmFrom && !raining && hour >= WashFrom && hour <= WashTo;
    }
}
