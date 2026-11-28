using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>A mark the ground keeps (Nature/GroundMarks.cs): what an earthquake, the rain or a wet season leaves on the land.</summary>
    public enum GroundMarkKind
    {
        None,
        /// <summary>A short hollow opened into a rock face, roofed with rock, loose stone on its floor.</summary>
        Cave,
        /// <summary>A small pool of water come up out of the ground (warm with Odyssey's hot spring water, now and then).</summary>
        Spring,
        /// <summary>A crack across open ground: a jagged line of bare rock with stones thrown up beside it.</summary>
        Fissure,
        /// <summary>Loose stone at the foot of a rock face: stony soil and chunks of the face's own rock.</summary>
        Scree,
        /// <summary>A hillside come down after days of heavy rain: rubble against the face, stones and mud below it.</summary>
        Landslide,
        /// <summary>A round hollow sunk into soft ground after a wet season, with water standing in it.</summary>
        Sinkhole
    }

    /// <summary>What shook the ground, which decides what mark it may leave.</summary>
    public enum QuakeSource
    {
        None,
        /// <summary>An earthquake: Seasonal Weather's, Vanilla Events Expanded's, Rimefeller's drill tremors, or any other mod's by name.</summary>
        Quake,
        /// <summary>Rimfolk's own rockslide (Happenings/Workers_Storyteller_Land.cs).</summary>
        Rockslide,
        /// <summary>Rimfolk's own warm vent after a night tremor (the same file), which already leaves a geyser.</summary>
        WarmVent
    }

    /// <summary>The land as the waking mountain reads it: none, a land of mountains, or a land of lava.</summary>
    public enum WakeLand
    {
        None,
        Mountain,
        Lava
    }

    /// <summary>The waking mountain's stages (Nature/GroundWaking.cs), in the order they come.</summary>
    public enum WakeStage
    {
        Quiet,
        /// <summary>Days of tremors; at their end, warm vents open.</summary>
        Tremors,
        /// <summary>Smoke over the mountain and ash coming down; the game's volcanic winter, Odyssey's ash where it is.</summary>
        Ash,
        /// <summary>The ash lying on the land, until the ground where it fell turns rich.</summary>
        Settling
    }

    /// <summary>
    /// The ground that stays and the mountain that wakes (docs/design/living-land-ground.md; the player's picks D1 and D2 of
    /// 28 November 2026): which quakes leave a mark and how often, how big each mark is, where it may go (never under or
    /// beside anything of the colony's), when rain brings a hillside down or a wet season opens a sinkhole, which lands
    /// have a mountain that may wake and how its stages run. Pure, so Tools/Tests checks it (LivingGroundTests).
    /// </summary>
    public static class GroundMath
    {
        public const int DayTicks = 60000, HourTicks = 2500;

        // ================= Marks after a quake =================

        /// <summary>The chance an earthquake leaves a mark; Rimfolk's rockslide and warm vent, which leave their own, less.</summary>
        public const float QuakeMarkChance = 0.3f, RockslideMarkChance = 0.2f, VentMarkChance = 0.15f;

        /// <summary>No two marks on a map within this many days, whatever left them: a season at least.</summary>
        public const int MarkGapDays = 60;

        /// <summary>A quake noted again within this many hours is the same quake (an incident and the condition it starts).</summary>
        public const int SameQuakeHours = 12;

        /// <summary>The mark comes this many hours after the shaking stopped, once someone could go and look.</summary>
        public const int MarkDelayHours = 1;

        /// <summary>The names the quakes go by: Seasonal Weather's incident and condition, Vanilla Events Expanded's, Rimefeller's drill tremors.</summary>
        public static readonly HashSet<string> QuakeNames = new HashSet<string>(StringComparer.Ordinal) { "Earthquake", "VEE_Earthquake", "DrillQuake" };

        public const string RockslideName = "Council_Evt_Rockslide", WarmVentName = "Council_Evt_WarmVent";

        /// <summary>
        /// What an event is, as a quake: Rimfolk's rockslide and warm vent by name, the known quakes by name, and any other
        /// mod's event whose name or worker says earthquake (Vanilla Psycasts Expanded's time quake is no earthquake).
        /// </summary>
        public static QuakeSource SourceOf(string defName, string worker)
        {
            if (defName == RockslideName) return QuakeSource.Rockslide;
            if (defName == WarmVentName) return QuakeSource.WarmVent;
            if (defName != null && QuakeNames.Contains(defName)) return QuakeSource.Quake;
            return Says(defName) || Says(worker) ? QuakeSource.Quake : QuakeSource.None;
        }

        /// <summary>A condition that is an earthquake going on (Seasonal Weather's, and any other by its name).</summary>
        public static bool IsQuakeCondition(string defName) => defName != null && (defName == "Earthquake" || Says(defName));

        private static bool Says(string name) =>
            !string.IsNullOrEmpty(name) && name.IndexOf("earthquake", StringComparison.OrdinalIgnoreCase) >= 0 && name.IndexOf("timequake", StringComparison.OrdinalIgnoreCase) < 0;

        public static float MarkChance(QuakeSource source)
        {
            switch (source)
            {
                case QuakeSource.Quake: return QuakeMarkChance;
                case QuakeSource.Rockslide: return RockslideMarkChance;
                case QuakeSource.WarmVent: return VentMarkChance;
                default: return 0f;
            }
        }

        /// <summary>Days since a tick, a large number for never (a negative tick) or a tick from a save played on.</summary>
        public static float DaysSince(int tick, int now) => tick < 0 || tick > now ? 99999f : (now - tick) / (float)DayTicks;

        /// <summary>The map may take another mark: none in the last <see cref="MarkGapDays"/>.</summary>
        public static bool MayMark(int lastMark, int now) => DaysSince(lastMark, now) >= MarkGapDays;

        /// <summary>
        /// The mark a quake leaves, by what shook the ground and what the land has near the quake (a rock face with rock
        /// behind it, open ground): an earthquake any of four; the rockslide a hollow behind the torn face or water out of
        /// it; the warm vent's tremor a spring or a crack. None when the land has nothing it fits.
        /// </summary>
        public static GroundMarkKind PickAfterQuake(QuakeSource source, bool rockFace, bool open, float roll)
        {
            var options = new List<(GroundMarkKind kind, float weight)>();
            void Add(GroundMarkKind k, float w, bool fits)
            {
                if (fits && w > 0f) options.Add((k, w));
            }
            switch (source)
            {
                case QuakeSource.Quake:
                    Add(GroundMarkKind.Cave, 3f, rockFace);
                    Add(GroundMarkKind.Scree, 3f, rockFace);
                    Add(GroundMarkKind.Fissure, 3f, open);
                    Add(GroundMarkKind.Spring, 2f, open);
                    break;
                case QuakeSource.Rockslide:
                    Add(GroundMarkKind.Cave, 2f, rockFace);
                    Add(GroundMarkKind.Spring, 1f, open);
                    break;
                case QuakeSource.WarmVent:
                    Add(GroundMarkKind.Spring, 2f, open);
                    Add(GroundMarkKind.Fissure, 1f, open);
                    break;
            }
            if (options.Count == 0) return GroundMarkKind.None;
            float total = 0f;
            foreach (var o in options) total += o.weight;
            float r = Clamp01(roll) * total;
            foreach (var o in options)
            {
                if (r < o.weight) return o.kind;
                r -= o.weight;
            }
            return options[options.Count - 1].kind;
        }

        // ================= Sizes: a few to a few dozen cells =================

        /// <summary>No mark, nor the ash that turns the soil, takes more cells than this.</summary>
        public const int MaxCells = 40;

        /// <summary>How many cells a mark takes, from a roll in [0, 1).</summary>
        public static int Cells(GroundMarkKind kind, float roll)
        {
            float r = Clamp01(roll);
            switch (kind)
            {
                case GroundMarkKind.Cave: return Between(3, 7, r);
                case GroundMarkKind.Spring: return Between(3, 7, r);
                case GroundMarkKind.Fissure: return Between(5, 9, r);
                case GroundMarkKind.Scree: return Between(6, 14, r);
                case GroundMarkKind.Landslide: return Between(10, 24, r);
                case GroundMarkKind.Sinkhole: return r < 0.6f ? 9 : 13;
                default: return 0;
            }
        }

        /// <summary>The sinkhole's radius: 9 cells across 1.5, 13 across 2.</summary>
        public static float SinkholeRadius(int cells) => cells >= 13 ? 2f : 1.5f;

        /// <summary>The fewest cells a mark is worth making with: else it isn't made at all.</summary>
        public static int Fewest(GroundMarkKind kind)
        {
            switch (kind)
            {
                case GroundMarkKind.Cave: return 3;
                case GroundMarkKind.Spring: return 3;
                case GroundMarkKind.Fissure: return 4;
                case GroundMarkKind.Scree: return 5;
                case GroundMarkKind.Landslide: return 8;
                case GroundMarkKind.Sinkhole: return 9;
                default: return 1;
            }
        }

        /// <summary>How far from where it starts a mark may reach (cells), for the gap kept to the colony.</summary>
        public static float Reach(GroundMarkKind kind)
        {
            switch (kind)
            {
                case GroundMarkKind.Cave: return 8f;
                case GroundMarkKind.Spring: return 2f;
                case GroundMarkKind.Fissure: return 9f;
                case GroundMarkKind.Scree: return 4f;
                case GroundMarkKind.Landslide: return 6f;
                case GroundMarkKind.Sinkhole: return 2f;
                default: return 0f;
            }
        }

        /// <summary>Of a landslide's cells, the nearest to the face this many are rubble that must be mined away; then stones.</summary>
        public static int SlideRubble(int cells) => Math.Max(2, Math.Min(5, cells / 5));

        /// <summary>One cell in this many of a scree slope or a landslide's lower part has a chunk of rock on it.</summary>
        public const int ChunkOneIn = 3;

        // ================= Never under or beside the colony's things =================

        /// <summary>Cells kept clear round anything of the colony's (a building, a blueprint, a frame): no mark comes nearer.</summary>
        public const float ColonyGap = 8f;

        /// <summary>
        /// A cell a mark may change: not within <see cref="ColonyGap"/> of the colony's things, not in its home area or a
        /// zone (a field, a stockpile), nothing built on it, nobody standing on it.
        /// </summary>
        public static bool CellAllowed(bool nearColony, bool home, bool zone, bool built, bool someone) => !nearColony && !home && !zone && !built && !someone;

        /// <summary>The place a mark starts is far enough from the colony's nearest thing for all of the mark to stay clear.</summary>
        public static bool FarEnough(float toColony, GroundMarkKind kind) => toColony >= ColonyGap + Reach(kind);

        /// <summary>
        /// A rock cell a cave may be carved through: it keeps at least this many cells of rock about it once carved, so the
        /// hollow stays a narrow one in solid rock and every roof over it has rock to rest on.
        /// </summary>
        public const int CaveRockAround = 3;

        public static bool CaveCellHolds(int rockAroundAfter) => rockAroundAfter >= CaveRockAround;

        /// <summary>A rock face worth a cave or scree: at least this many cells of rock within 4 cells of it.</summary>
        public const int RockMass = 20;

        /// <summary>Nobody of the colony within this many cells of a cave as it opens.</summary>
        public const float CaveClearOfPeople = 10f;

        // ================= Rain on a slope =================

        /// <summary>Days of rain running before a hillside may come down, and the chance a day of heavy rain then brings one.</summary>
        public const int SlideWetRun = 3;

        public const float SlideChance = 0.04f;

        /// <summary>A storm or a torrent of rain doubles it.</summary>
        public const float SlideStormFactor = 2f;

        /// <summary>
        /// A hillside comes down today: hilly land with a rock face to come down from, rain falling now after
        /// <see cref="SlideWetRun"/> wet days running, and the day's roll.
        /// </summary>
        public static bool SlideToday(bool hilly, bool rockFace, bool rainingNow, bool storm, int wetRun, float roll) =>
            hilly && rockFace && rainingNow && wetRun >= SlideWetRun && roll < SlideChance * (storm ? SlideStormFactor : 1f);

        /// <summary>The chance a colonist where the slide comes down is hurt by it (the shared budget's, before its scale).</summary>
        public const float SlideHurtChance = 0.6f;

        // ================= A wet season =================

        /// <summary>A season (a quadrum, 15 days) with this many days of rain or more was a wet one.</summary>
        public const int WetSeasonDays = 8;

        public static bool WetSeason(int wetDays) => wetDays >= WetSeasonDays;

        /// <summary>The chance a wet season ends with a sinkhole; twice it where the land's rock dissolves (limestone, marble, chalk).</summary>
        public const float SinkholeChance = 0.12f, KarstFactor = 2f;

        public static float SinkholeChanceFor(bool karst) => SinkholeChance * (karst ? KarstFactor : 1f);

        /// <summary>A rock that water dissolves into hollows, by its name.</summary>
        public static bool Karst(string rockDefName) =>
            !string.IsNullOrEmpty(rockDefName) && (rockDefName.IndexOf("Limestone", StringComparison.OrdinalIgnoreCase) >= 0
                                                  || rockDefName.IndexOf("Marble", StringComparison.OrdinalIgnoreCase) >= 0
                                                  || rockDefName.IndexOf("Chalk", StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>Hours between the ground sagging (the sign) and the sinkhole opening.</summary>
        public static int SinkDelayHours(float roll) => Between(8, 16, Clamp01(roll));

        /// <summary>The chance a colonist on the ground as it gives way is hurt by the fall.</summary>
        public const float SinkHurtChance = 0.5f;

        /// <summary>A season's key, to look at each finished season once.</summary>
        public static int SeasonKey(int year, int quadrum) => year * 4 + quadrum;

        // ================= The mountain that wakes =================

        /// <summary>The land's own name and its features' names that make it a land of lava.</summary>
        public static readonly HashSet<string> LavaBiomes = new HashSet<string>(StringComparer.Ordinal) { "LavaField", "AB_PyroclasticConflagration" };

        public static readonly HashSet<string> LavaFeatures = new HashSet<string>(StringComparer.Ordinal)
        {
            // Odyssey
            "LavaCaves", "LavaCrater", "LavaFlow", "LavaLake", "ObsidianDeposits",
            // Alpha Biomes
            "AB_MagmaVents", "AB_MagmaticQuagmire", "AB_GeothermalHotspots", "AB_ObsidianExpanse", "AB_ObsidianSpires",
            // Geological Landforms
            "GL_Caldera",
            // Vanilla Landmarks Expanded
            "VEE_ResurgentCaldera", "VEE_LushLavaFields", "VEE_VolcanicRichSoil", "VEE_Volcano", "VEE_ObsidianChunks", "VEE_VolcanicSandDesert"
        };

        /// <summary>A feature of the land that is lava, a crater of fire or a volcano's: by name, or a name that says so.</summary>
        public static bool LavaFeature(string defName) =>
            !string.IsNullOrEmpty(defName) && (LavaFeatures.Contains(defName) || defName.IndexOf("volcan", StringComparison.OrdinalIgnoreCase) >= 0
                                               || defName.IndexOf("lava", StringComparison.OrdinalIgnoreCase) >= 0
                                               || defName.IndexOf("magma", StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>A feature of the land that is a mountain (the game's own, Geological Landforms' lone mountain, Vanilla Landmarks Expanded's).</summary>
        public static bool MountainFeature(string defName) => !string.IsNullOrEmpty(defName) && defName.IndexOf("mountain", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Which land a map is, for the waking mountain: lava first, then mountains; flat land and hills never wake.</summary>
        public static WakeLand LandOf(bool mountainous, bool lavaBiome, bool lavaFeature, bool mountainFeature) =>
            lavaBiome || lavaFeature ? WakeLand.Lava : mountainous || mountainFeature ? WakeLand.Mountain : WakeLand.None;

        /// <summary>
        /// The day's chance, as one in this many years: a land of lava wakes sooner than a land of mountains. With the gap
        /// after a waking, a mountain wakes about once in 17 years and a land of lava about once in 11 (MeanYearsBetween).
        /// </summary>
        public const float MountainYears = 12f, LavaYears = 6f;

        /// <summary>A sleeper turned under the deep workings lately (Gods/OnMap/GodWaking.cs): the mountain wakes twice as readily.</summary>
        public const float StirredFactor = 2f;

        /// <summary>Days after the last time the sleeper turned that it still counts as stirred.</summary>
        public const int StirredDays = 60;

        /// <summary>Not before this day of the colony, and never within this many days of the last waking (five years).</summary>
        public const int WakeEarliestDay = 60, WakeGapDays = 300;

        public const int YearDays = 60;

        /// <summary>The chance a day it wakes.</summary>
        public static float WakeChancePerDay(WakeLand land, bool stirred)
        {
            if (land == WakeLand.None) return 0f;
            float years = land == WakeLand.Lava ? LavaYears : MountainYears;
            return 1f / (years * YearDays) * (stirred ? StirredFactor : 1f);
        }

        /// <summary>The years between two wakings on average: the gap, then the day's chance (stirred or not).</summary>
        public static float MeanYearsBetween(WakeLand land, bool stirred)
        {
            float p = WakeChancePerDay(land, stirred);
            return p <= 0f ? float.PositiveInfinity : (WakeGapDays + 1f / p) / YearDays;
        }

        /// <summary>It wakes today: a land that can, late enough in the colony's life, long enough since the last time, and the day's roll.</summary>
        public static bool Wakes(WakeLand land, int day, float daysSinceLast, bool stirred, float roll) =>
            land != WakeLand.None && day >= WakeEarliestDay && daysSinceLast >= WakeGapDays && roll < WakeChancePerDay(land, stirred);

        /// <summary>How long each stage lasts, in hours, from a roll in [0, 1): tremors three to five days, ash two to three, settling five to nine.</summary>
        public static int StageHours(WakeStage stage, float roll)
        {
            float r = Clamp01(roll);
            switch (stage)
            {
                case WakeStage.Tremors: return Between(72, 120, r);
                case WakeStage.Ash: return Between(48, 72, r);
                case WakeStage.Settling: return Between(120, 216, r);
                default: return 0;
            }
        }

        /// <summary>The stage after this one; Quiet after the settling.</summary>
        public static WakeStage Next(WakeStage stage)
        {
            switch (stage)
            {
                case WakeStage.Tremors: return WakeStage.Ash;
                case WakeStage.Ash: return WakeStage.Settling;
                default: return WakeStage.Quiet;
            }
        }

        /// <summary>Tremors a day while the mountain shakes, and the chance of one at each 250-tick look.</summary>
        public const float TremorsPerDay = 5f;

        public static float TremorChancePerLook(int lookTicks) => Clamp01(TremorsPerDay * lookTicks / DayTicks);

        /// <summary>The chance a stone off the rock hurts someone standing at the face in a tremor (the shared budget's, before its scale).</summary>
        public const float StoneHurtChance = 0.15f;

        /// <summary>Warm vents that open as the tremors end: one or two.</summary>
        public static int Vents(float roll) => Clamp01(roll) < 0.5f ? 1 : 2;

        /// <summary>The game's volcanic winter the ash brings: four to eight days.</summary>
        public static int WinterDays(float roll) => Between(4, 8, Clamp01(roll));

        /// <summary>Odyssey's ash in the air at the height of it: a day or two.</summary>
        public static int AshAirDays(float roll) => Between(1, 2, Clamp01(roll));

        /// <summary>The cells where the ash lay thickest, which turn rich: twenty to forty.</summary>
        public static int AshCells(float roll) => Between(20, MaxCells, Clamp01(roll));

        /// <summary>The patches the ash lies thickest in: two to four.</summary>
        public static int AshPatches(float roll) => Between(2, 4, Clamp01(roll));

        /// <summary>
        /// Natural ground the ash turns rich: a soil that grows something but less than rich soil does (not water, not a
        /// floor, not already rich).
        /// </summary>
        public static bool AshTurns(bool natural, bool water, bool floor, float fertility, float richFertility) =>
            natural && !water && !floor && fertility > 0.1f && fertility < richFertility;

        // ================= Helpers =================

        private static int Between(int low, int high, float r)
        {
            if (high <= low) return low;
            int n = low + (int)Math.Floor(r * (high - low + 1));
            return n > high ? high : n < low ? low : n;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v >= 1f ? 0.9999f : v;
    }
}
