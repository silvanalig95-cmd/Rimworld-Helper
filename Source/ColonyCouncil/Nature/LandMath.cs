using System;
using System.Collections.Generic;
using System.Globalization;

namespace ColonyCouncil
{
    /// <summary>What took a patch's cover: a fire, the colony's felling, a wilt that killed the trees, a rust that killed the grass.</summary>
    public enum LandCause
    {
        Burn,
        Felled,
        Wilt,
        Rust
    }

    /// <summary>How far a patch has come back: bare, grass, brush, young trees, woods (the last only where wood stood).</summary>
    public enum LandStage
    {
        Bare,
        Grass,
        Brush,
        Saplings,
        Woods
    }

    /// <summary>The three sicknesses of the wild plants: a wilt among trees of one kind, a rust on the grass, a rot in the berry bushes.</summary>
    public enum LandSickKind
    {
        Wilt,
        Rust,
        Rot
    }

    /// <summary>How wet the last weeks were for the still water: a dry spell, as usual for the land, or a wet spell.</summary>
    public enum LandWetness
    {
        Dry,
        Usual,
        Wet
    }

    /// <summary>
    /// A patch of land that lost its cover (a burn, a felled wood, trees a wilt killed, grass a rust killed), kept as the
    /// box round the cells it was lost from, and how far it has come back. The land counts only its growing days.
    /// </summary>
    public class LandPatch
    {
        public LandCause cause;
        public int x0, z0, x1, z1;
        /// <summary>Plants lost in it, and of those trees.</summary>
        public int lost, trees;
        /// <summary>The day it was opened, and the last day it was looked at (days since the game began).</summary>
        public int day, lastDay = -1;
        /// <summary>Growing days counted since it was opened.</summary>
        public int growDays;
        public LandStage stage;
        /// <summary>The last stage the news told of (as a number), so each is told once, the first day it sets something.</summary>
        public int told;
        /// <summary>The kind of tree that stood there (its defName), or "".</summary>
        public string tree = "";

        public bool Counts => LandMath.Counts(cause, lost, trees);

        public bool Wood => trees >= LandMath.WoodTrees;

        public int CenterX => (x0 + x1) / 2;

        public int CenterZ => (z0 + z1) / 2;

        public int Width => x1 - x0 + 1;

        public int Height => z1 - z0 + 1;
    }

    /// <summary>One plant sick in an outbreak: the cell it stands on (the map's cell index) and the day it fell sick.</summary>
    public struct LandSickPlant
    {
        public int cell, day;

        public LandSickPlant(int cell, int day)
        {
            this.cell = cell;
            this.day = day;
        }
    }

    /// <summary>A sickness of the wild plants going on on a map (one at a time).</summary>
    public class LandOutbreak
    {
        public LandSickKind kind;
        /// <summary>The kind of plant it is in (wilt and rot), or the first grass it was found in (rust).</summary>
        public string def = "";
        /// <summary>Where it began (cell index), and the day it began.</summary>
        public int start, day;
        /// <summary>Plants it has killed, plants cut or burned while sick, and how many fell sick in all.</summary>
        public int died, cut, ever;
        /// <summary>Cold days running (it dies back after a few).</summary>
        public int cold;
        /// <summary>The council asked about it; the grazers left (rust); the burn the council chose, waiting, and who sets it.</summary>
        public bool asked, grazersLeft, burnWaiting;
        public int burner = -1, burnSince = -1;
        public List<LandSickPlant> sick = new List<LandSickPlant>();
    }

    /// <summary>A cell the still water left or came over: its cell index, the terrain it was (defName), its ring out from the old edge, and which way it went.</summary>
    public class LandWaterCell
    {
        public int cell;
        public string was = "";
        public int ring;
        /// <summary>True when the water drew back from it (a drought), false when the water came over it (a wet spell).</summary>
        public bool dried;
    }

    /// <summary>A cell the colony's animals grazed bare: its cell index, what it was (defName), and the grazing block it is in.</summary>
    public class LandWornCell
    {
        public int cell, block;
        public string was = "";
    }

    /// <summary>A block of the map the colony's animals graze: days overgrazed (falls by one a day of rest) and growing days rested since.</summary>
    public class LandGrazing
    {
        public int block, days, rest;
        public bool told;
    }

    /// <summary>
    /// The land that recovers and changes (docs/design/living-land-land.md): the pure rules, with no game types, so
    /// Tools/Tests checks them. B1: burned or cleared ground comes back in stages over growing days, grass, brush, young trees,
    /// woods; a felled wood left alone grows back; ground the colony's animals graze bare wears and comes back once rested. B3:
    /// a wilt, a rust and a rot of the wild plants, how often one comes, how it spreads and how it ends. B5: still water that
    /// draws back in a dry spell and swells in a wet one, a ring at a time, and goes back to the map's own edge. Every pass
    /// looks at a few hundred cells at most.
    /// </summary>
    public static class LandMath
    {
        // ================= Every pass =================

        /// <summary>The most cells any one of the land's passes looks at (the passes run once a day each, a few hours apart).</summary>
        public const int PassCap = 200;

        /// <summary>Cells the regrowth pass looks at, shared among the patches.</summary>
        public const int RegrowCells = 160;

        /// <summary>Grazing blocks looked at a day, and cells sampled in each.</summary>
        public const int GrazeBlocks = 4, GrazeSamples = 36;

        /// <summary>Plants of an outbreak looked at a day (every one, the most an outbreak has: <see cref="Cap"/>), and cells sampled round them for the spread.</summary>
        public const int SickLooks = 140, SpreadLooks = 60;

        /// <summary>Plants sampled to find where a sickness could begin.</summary>
        public const int StartSamples = 160;

        /// <summary>Cells the water pass finds new edges with, and edge cells it works on.</summary>
        public const int WaterFind = 60, WaterWork = 100;

        /// <summary>The cells looked at by each pass at its most, for the tests (each within <see cref="PassCap"/>).</summary>
        public static int[] PassCells => new[] { RegrowCells, GrazeBlocks * GrazeSamples, SickLooks + SpreadLooks, StartSamples, WaterFind + WaterWork };

        /// <summary>The four parts, one a day each: the water, the sickness, the patches coming back, the pasture.</summary>
        public const int PartWater = 0, PartSickness = 1, PartRegrowth = 2, PartPasture = 3, Parts = 4;

        /// <summary>The 250-tick cycles in a day (Sched.DayTicks / Sched.RareTicks).</summary>
        public const int CyclesADay = 240;

        /// <summary>
        /// Which part runs at this cycle of the day (0 to 239) on this map, or -1: the four a quarter of a day apart (at the
        /// 24th cycle of each quarter), each map a little later than the one before so two homes never share a cycle's work.
        /// </summary>
        public static int PartAt(int cycle, int mapId)
        {
            int shift = (mapId % 6 + 6) % 6 * 2;
            int c = ((cycle - shift) % CyclesADay + CyclesADay) % CyclesADay;
            int quarter = CyclesADay / Parts;
            return c % quarter == 24 ? c / quarter : -1;
        }

        /// <summary>A growing day: the day's mean was at least this (°C), the advisor's own measure of winter (WeatherMath.ColdBelow).</summary>
        public const float GrowingFrom = 6f;

        public static bool Growing(float mean) => mean >= GrowingFrom;

        // ================= B1: patches that come back =================

        /// <summary>Goodwill each Mossborn people gives when a wood the colony felled is let grow back.</summary>
        public const int MossbornGoodwill = 3;

        /// <summary>A lost plant joins a patch when it is this close to its box, and the box stays this wide at most.</summary>
        public const int JoinGap = 3, MaxSpan = 24;

        /// <summary>Patches kept a map, and days a patch has to count before it is let go.</summary>
        public const int MaxPatches = 10, PendingDays = 10;

        /// <summary>Trees lost in a patch for it to have been wood (it comes back as woods).</summary>
        public const int WoodTrees = 3;

        /// <summary>Growing days to grass, to brush, to young trees and to woods (where wood stood; elsewhere it closes at <see cref="SaplingDays"/>).</summary>
        public const int GrassDays = 2, BrushDays = 12, SaplingDays = 25, WoodsDays = 60;

        /// <summary>Plants set at most a pass in a patch: grass, brush, young trees.</summary>
        public const int GrassSeeds = 14, BrushSeeds = 6, SaplingSeeds = 4;

        /// <summary>The growth each is set with (of the game's 0 to 1).</summary>
        public const float GrassGrowthMin = 0.25f, GrassGrowthMax = 0.6f, BrushGrowthMin = 0.15f, BrushGrowthMax = 0.35f, SaplingGrowthMin = 0.05f, SaplingGrowthMax = 0.15f;

        /// <summary>A patch counts once this much is lost in it: a burn a dozen plants, a felling six trees, a wilt three dead trees, a rust ten dead grasses.</summary>
        public static bool Counts(LandCause cause, int lost, int trees)
        {
            switch (cause)
            {
                case LandCause.Burn: return lost >= 12;
                case LandCause.Felled: return trees >= 6;
                case LandCause.Wilt: return trees >= 3;
                default: return lost >= 10;
            }
        }

        /// <summary>The cell (x, z) is close enough to the patch's box, and the box with it stays within <see cref="MaxSpan"/>.</summary>
        public static bool Joins(LandPatch p, int x, int z)
        {
            if (p == null) return false;
            if (x < p.x0 - JoinGap || x > p.x1 + JoinGap || z < p.z0 - JoinGap || z > p.z1 + JoinGap) return false;
            int w = Math.Max(p.x1, x) - Math.Min(p.x0, x) + 1, h = Math.Max(p.z1, z) - Math.Min(p.z0, z) + 1;
            return w <= MaxSpan && h <= MaxSpan;
        }

        /// <summary>A plant lost at (x, z) added to the patch: the box grows to hold it.</summary>
        public static void Add(LandPatch p, int x, int z, bool tree, string treeDef)
        {
            p.x0 = Math.Min(p.x0, x);
            p.x1 = Math.Max(p.x1, x);
            p.z0 = Math.Min(p.z0, z);
            p.z1 = Math.Max(p.z1, z);
            p.lost++;
            if (!tree) return;
            p.trees++;
            if (string.IsNullOrEmpty(p.tree) && !string.IsNullOrEmpty(treeDef)) p.tree = treeDef;
        }

        /// <summary>A new patch from one lost plant.</summary>
        public static LandPatch Open(LandCause cause, int x, int z, int day, bool tree, string treeDef)
        {
            var p = new LandPatch { cause = cause, x0 = x, x1 = x, z0 = z, z1 = z, day = day };
            Add(p, x, z, tree, treeDef);
            return p;
        }

        /// <summary>A patch burned or felled again while it was coming back starts over: its growing days and stage from the beginning.</summary>
        public static void StartOver(LandPatch p)
        {
            p.growDays = 0;
            p.stage = LandStage.Bare;
            p.told = 0;
        }

        /// <summary>A patch that didn't come to count within <see cref="PendingDays"/> is let go.</summary>
        public static bool Stale(LandPatch p, int today) => p != null && !p.Counts && today - p.day > PendingDays;

        /// <summary>The stage a patch has reached after so many growing days.</summary>
        public static LandStage StageFor(int growDays, bool wood)
        {
            if (wood && growDays >= WoodsDays) return LandStage.Woods;
            if (wood && growDays >= SaplingDays) return LandStage.Saplings;
            if (growDays >= BrushDays) return LandStage.Brush;
            if (growDays >= GrassDays) return LandStage.Grass;
            return LandStage.Bare;
        }

        /// <summary>The patch is done: woods again, or grass and brush again where no wood stood.</summary>
        public static bool Closed(int growDays, bool wood) => wood ? growDays >= WoodsDays : growDays >= SaplingDays;

        /// <summary>Plants set at most a pass at this stage (none while bare, nor once done).</summary>
        public static int Seeds(LandStage stage)
        {
            switch (stage)
            {
                case LandStage.Grass: return GrassSeeds;
                case LandStage.Brush: return BrushSeeds;
                case LandStage.Saplings: return SaplingSeeds;
                default: return 0;
            }
        }

        /// <summary>
        /// What a pass sets at a stage: grass first; from brush on, brush and some grass; from young trees on, young trees,
        /// brush and grass. <paramref name="roll"/> is a random 0 to 1. 0 grass, 1 brush, 2 tree.
        /// </summary>
        public static int Pick(LandStage stage, float roll)
        {
            switch (stage)
            {
                case LandStage.Grass: return 0;
                case LandStage.Brush: return roll < 0.6f ? 1 : 0;
                case LandStage.Saplings: return roll < 0.5f ? 2 : roll < 0.8f ? 1 : 0;
                default: return -1;
            }
        }

        /// <summary>The cells looked at in each patch a pass: the pass's cells shared out, at least a few each.</summary>
        public static int CellsEach(int patches) => patches <= 0 ? 0 : Math.Max(8, RegrowCells / patches);

        /// <summary>A day counted for a patch: a growing day, never the same day twice.</summary>
        public static bool CountDay(LandPatch p, int today, bool growing)
        {
            if (p.lastDay == today) return false;
            p.lastDay = today;
            if (growing) p.growDays++;
            return growing;
        }

        // ================= B1: ground grazed bare =================

        /// <summary>A grazing block is this many cells each way.</summary>
        public const int Block = 16;

        /// <summary>A grazer this big (body size 1) needs about this many cells of grass to keep the grass up.</summary>
        public const float CellsPerBody = 12f;

        /// <summary>Overgrazed when the grass covers less than this share of the good ground.</summary>
        public const float BareShare = 0.35f;

        /// <summary>Overgrazed days before the ground wears; ground worn a pass, at most; and the share of a block's good ground that may wear.</summary>
        public const int WornAfter = 6, WearPerPass = 5;
        public const float WearShare = 0.25f;

        /// <summary>Growing days rested before the worn ground comes back, and cells brought back a pass.</summary>
        public const int RestDays = 20, PastureBackPerPass = 8;

        /// <summary>Worn cells and grazing blocks kept a map.</summary>
        public const int MaxWorn = 240, MaxBlocks = 12;

        /// <summary>Grazers in a block for it to be looked at: three animals, or body size four.</summary>
        public const int MinGrazers = 3;
        public const float MinBody = 4f;

        /// <summary>The block (its number) a cell is in, on a map this many cells wide.</summary>
        public static int BlockOf(int x, int z, int mapWidth) => z / Block * ((mapWidth + Block - 1) / Block) + x / Block;

        /// <summary>
        /// Overgrazed: the grazers' body size needs more grass than the block's good ground (<paramref name="fertile"/> of
        /// <paramref name="samples"/> cells sampled) can keep up, and the grass covers less than <see cref="BareShare"/> of it.
        /// </summary>
        public static bool Overgrazed(float body, int fertile, int covered, int samples)
        {
            if (samples <= 0 || fertile < Math.Max(4, samples / 6)) return false;
            float ground = fertile * (float)(Block * Block) / samples;
            return body * CellsPerBody >= ground && covered < fertile * BareShare;
        }

        /// <summary>A day for a block: overgrazed it counts up and the rest starts over, else it counts down and a growing day of rest is added.</summary>
        public static void Day(LandGrazing g, bool overgrazed, bool growing)
        {
            if (overgrazed)
            {
                g.days++;
                g.rest = 0;
                return;
            }
            if (g.days > 0) g.days--;
            if (growing) g.rest++;
        }

        public static bool Wears(LandGrazing g) => g != null && g.days >= WornAfter;

        public static bool Rested(LandGrazing g) => g != null && g.days == 0 && g.rest >= RestDays;

        /// <summary>How many of a block's cells may wear: a quarter of its good ground (scaled from the sample).</summary>
        public static int WearCap(int fertile, int samples) => samples <= 0 ? 0 : (int)(fertile * (float)(Block * Block) / samples * WearShare);

        /// <summary>What a grazed-bare cell becomes: rich soil thins to soil, soil and moss wear to packed dirt, marshy ground churns to mud; null for other ground.</summary>
        public static string WornTo(string def)
        {
            switch (def)
            {
                case "SoilRich": return "Soil";
                case "Soil":
                case "MossyTerrain":
                case "GrasslandSoil":
                    return "PackedDirt";
                case "MarshyTerrain": return "Mud";
                default: return null;
            }
        }

        // ================= B3: sicknesses of the wild plants =================

        /// <summary>The chance a day of a sickness beginning, in a growing day with none going on.</summary>
        public const float SickPerDay = 1f / 90f;

        /// <summary>None in a colony's first days, and none this soon after the last ended.</summary>
        public const int SickFirstDays = 20, SickGapDays = 30;

        /// <summary>The region's blight makes one three times as likely; a wet, warm spell half again (rust and rot are moulds).</summary>
        public const float RegionBlightFactor = 3f, WetWarmFactor = 1.5f;

        /// <summary>A day's chance of a sickness beginning.</summary>
        public static float SickChance(bool growing, int colonyDays, int daysSinceLast, bool regionBlight, bool wetWarm)
        {
            if (!growing || colonyDays < SickFirstDays || daysSinceLast < SickGapDays) return 0f;
            float p = SickPerDay;
            if (regionBlight) p *= RegionBlightFactor;
            if (wetWarm) p *= WetWarmFactor;
            return p;
        }

        /// <summary>Plants of its kind on the map for a sickness to begin: fifteen trees of one kind, sixty grasses, six berry bushes.</summary>
        public static int MinHosts(LandSickKind k) => k == LandSickKind.Wilt ? 15 : k == LandSickKind.Rust ? 60 : 6;

        /// <summary>How likely each is chosen among those the land has hosts for.</summary>
        public static float Weight(LandSickKind k) => k == LandSickKind.Wilt ? 4f : k == LandSickKind.Rust ? 3.5f : 2.5f;

        /// <summary>
        /// Which sickness begins: among those with enough hosts (<paramref name="hosts"/> by kind, counted on the map), by
        /// weight; <paramref name="roll"/> a random 0 to 1. -1 when none has.
        /// </summary>
        public static int PickKind(int[] hosts, float roll)
        {
            float total = 0f;
            for (int k = 0; k < 3; k++)
                if (hosts != null && k < hosts.Length && hosts[k] >= MinHosts((LandSickKind)k)) total += Weight((LandSickKind)k);
            if (total <= 0f) return -1;
            float at = roll * total;
            for (int k = 0; k < 3; k++)
            {
                if (hosts[k] < MinHosts((LandSickKind)k)) continue;
                at -= Weight((LandSickKind)k);
                if (at < 0f) return k;
            }
            for (int k = 2; k >= 0; k--)
                if (hosts[k] >= MinHosts((LandSickKind)k)) return k;
            return -1;
        }

        /// <summary>How far it reaches from a sick plant (cells): a wilt through the roots, a rust on the wind, a rot from bush to bush.</summary>
        public static int Reach(LandSickKind k) => k == LandSickKind.Wilt ? 6 : k == LandSickKind.Rust ? 3 : 5;

        /// <summary>The chance a day that a sick plant passes it to one of its kind within reach.</summary>
        public static float SpreadChance(LandSickKind k, bool wet, bool cold)
        {
            if (cold) return 0f;
            float p = k == LandSickKind.Wilt ? 0.2f : k == LandSickKind.Rust ? 0.35f : 0.25f;
            if (wet && k != LandSickKind.Wilt) p *= 1.4f;
            return Math.Min(0.9f, p);
        }

        /// <summary>Cells looked at round each sick plant a pass.</summary>
        public const int TriesEach = 3;

        /// <summary>Plants that fall sick at most a day.</summary>
        public static int NewPerDay(LandSickKind k) => k == LandSickKind.Wilt ? 5 : k == LandSickKind.Rust ? 16 : 3;

        /// <summary>Plants that fall sick at most in one outbreak.</summary>
        public static int Cap(LandSickKind k) => k == LandSickKind.Wilt ? 40 : k == LandSickKind.Rust ? 140 : 20;

        /// <summary>Days a plant lives sick: a tree ten, a grass five, a bush seven.</summary>
        public static int DaysToDie(LandSickKind k) => k == LandSickKind.Wilt ? 10 : k == LandSickKind.Rust ? 5 : 7;

        /// <summary>A wilting tree stands bare from this many days sick.</summary>
        public const int BareAfter = 2;

        /// <summary>An outbreak ends after this many days at most, or this many cold days running.</summary>
        public const int SickMaxDays = 45, ColdEnds = 3;

        /// <summary>The council is asked once this many are sick (and for a few days after, while a question waits).</summary>
        public const int AskAt = 4, AskWithinDays = 6;

        /// <summary>Wild grazers leave a rusted grassland once this many grasses are sick; this many at most.</summary>
        public const int GrazersLeaveAt = 30, GrazersLeaving = 4;

        /// <summary>A burn is set only this far from the colony's buildings, fields and home area (cells), and given up after these days.</summary>
        public const float BurnClear = 15f;
        public const int BurnGiveUpDays = 3, BurnCells = 3, BurnTicks = 300;

        /// <summary>The plant has been sick long enough to die.</summary>
        public static bool Dies(LandSickKind k, int daySick, int today) => today - daySick >= DaysToDie(k);

        /// <summary>The plant stands bare (a wilting tree only).</summary>
        public static bool Bare(LandSickKind k, int daySick, int today) => k == LandSickKind.Wilt && today - daySick >= BareAfter;

        /// <summary>The outbreak is over: nothing sick left, too long, or the cold has killed it back.</summary>
        public static bool Over(int living, int days, int coldRun) => living == 0 || days >= SickMaxDays || coldRun >= ColdEnds;

        /// <summary>A cold day adds to the run; a growing one starts it over.</summary>
        public static int ColdRun(int run, bool growing) => growing ? 0 : run + 1;

        /// <summary>Room for more sick plants today: within the day's and the outbreak's caps.</summary>
        public static int Room(LandSickKind k, int newToday, int ever) => Math.Max(0, Math.Min(NewPerDay(k) - newToday, Cap(k) - ever));

        /// <summary>The council is asked now: enough sick, not asked yet, still early.</summary>
        public static bool Ask(int living, bool asked, int days) => !asked && living >= AskAt && days <= AskWithinDays;

        // ================= B5: still water that rises and falls =================

        /// <summary>The share of days that are wet on a land with this much rain a year (mm): a desert about one in ten, a temperate land about three in ten.</summary>
        public static float UsualShare(float rainfall) => Clamp(0.05f + rainfall / 4000f, 0.05f, 0.6f);

        /// <summary>Days of the record for a reading; fewer and the season counts as usual.</summary>
        public const int WetReadDays = 7;

        /// <summary>A dry run this long in the days read makes it a dry spell, whatever else fell.</summary>
        public const int DroughtRun = 10;

        /// <summary>
        /// How wet the last weeks were, 0 (a drought) to 1 (very wet), 0.5 as usual for the land: wet days (snow as half) against
        /// the land's usual share, a long dry run, and any drought or flood going on. The seam LandWet.SeasonWet gives the
        /// numbers from the weather record.
        /// </summary>
        public static float Wetness(int days, int wetDays, int snowDays, int longestDry, float rainfall, bool drought, bool flood)
        {
            float w = 0.5f;
            if (days >= WetReadDays)
            {
                float usual = UsualShare(rainfall);
                float share = (wetDays + snowDays * 0.5f) / days;
                w = Clamp(0.5f + (share - usual) / Math.Max(usual, 0.15f) * 0.5f, 0f, 1f);
                if (longestDry >= DroughtRun) w = Math.Min(w, 0.2f);
            }
            if (drought) w = Math.Min(w, 0.15f);
            if (flood) w = Math.Max(w, 0.85f);
            return w;
        }

        /// <summary>Below this a dry spell, above that a wet one.</summary>
        public const float DryBelow = 0.25f, WetAbove = 0.75f;

        public static LandWetness State(float wetness) => wetness <= DryBelow ? LandWetness.Dry : wetness >= WetAbove ? LandWetness.Wet : LandWetness.Usual;

        /// <summary>Days of a spell before the water moves.</summary>
        public const int SpellDays = 4;

        /// <summary>The water draws back at most this many cells from its old edge, and comes over at most this many.</summary>
        public const int MaxShrink = 3, MaxSwell = 2;

        /// <summary>Cells changed a pass: drying, swelling, and going back to the map's own.</summary>
        public const int DryPerPass = 10, SwellPerPass = 12, RestorePerPass = 20, RestoreUsualPerPass = 6;

        /// <summary>Cells the record keeps a map.</summary>
        public const int MaxWaterCells = 360;

        /// <summary>A spell's day: its own count goes up, the other's starts over; as usual both start over.</summary>
        public static void Spell(LandWetness state, ref int dryRun, ref int wetRun)
        {
            if (state == LandWetness.Dry)
            {
                dryRun++;
                wetRun = 0;
            }
            else if (state == LandWetness.Wet)
            {
                wetRun++;
                dryRun = 0;
            }
            else dryRun = wetRun = 0;
        }

        /// <summary>The still water the land's water moves: shallow water and marsh (deep water stays; rivers and the sea are never touched).</summary>
        public static bool StillShallow(string def) => def == "WaterShallow" || def == "Marsh";

        /// <summary>Ground the swelling water may come over: the open natural soils (not rock, not ice, not a floor).</summary>
        public static bool SwellGround(string def) =>
            def == "Soil" || def == "SoilRich" || def == "MossyTerrain" || def == "MarshyTerrain" || def == "Gravel" || def == "GrasslandSoil" || def == "Sand";

        /// <summary>The next step of a cell the water draws back from: water and marsh to mud, mud to soil; null when it goes no further.</summary>
        public static string DryNext(string now) => StillShallow(now) ? "Mud" : now == "Mud" ? "Soil" : null;

        /// <summary>The next step of a cell the water comes over: the ground to mud, and next to the old edge (ring 1) mud to marsh; null when it goes no further.</summary>
        public static string SwellNext(string now, int ring)
        {
            if (SwellGround(now)) return "Mud";
            if (now == "Mud" && ring == 1) return "Marsh";
            return null;
        }

        /// <summary>The next step back to what the cell was: dried ground through mud to its water, swelled water through mud to its ground; null once it is.</summary>
        public static string RestoreNext(string now, string was)
        {
            if (string.IsNullOrEmpty(was) || now == was) return null;
            if (now == "Mud") return was;
            return "Mud";
        }

        /// <summary>A ring out from the old edge: one more than the nearest neighbour's (0 for the map's own edge).</summary>
        public static int RingFrom(int nearest) => nearest + 1;

        public static bool CanDry(int ring) => ring >= 1 && ring <= MaxShrink;

        public static bool CanSwell(int ring) => ring >= 1 && ring <= MaxSwell;

        /// <summary>Cells to change this pass by the spell: in a dry spell first what the wet spell left, in a wet one first what the drought left.</summary>
        public static int PerPass(LandWetness state, bool restoring)
        {
            if (restoring) return state == LandWetness.Usual ? RestoreUsualPerPass : RestorePerPass;
            return state == LandWetness.Dry ? DryPerPass : state == LandWetness.Wet ? SwellPerPass : 0;
        }

        /// <summary>Fish in a pond the wet spell grew: a twentieth of what it can hold more, never past it.</summary>
        public static float FishAfterSwell(float population, float max) => Math.Min(max, population + max * 0.05f);

        // ================= The record, as short lines =================

        private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);

        private static int N(string s, int fallback = 0) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

        public static string Pack(LandPatch p) =>
            string.Join(",", (int)p.cause, I(p.x0), I(p.z0), I(p.x1), I(p.z1), I(p.lost), I(p.trees), I(p.day), I(p.lastDay), I(p.growDays), (int)p.stage, p.tree ?? "", I(p.told));

        public static LandPatch UnpackPatch(string s)
        {
            var a = (s ?? "").Split(',');
            if (a.Length < 12) return null;
            int cause = N(a[0], -1), stage = N(a[10], -1);
            if (cause < 0 || cause > 3 || stage < 0 || stage > 4) return null;
            var p = new LandPatch
            {
                cause = (LandCause)cause, x0 = N(a[1]), z0 = N(a[2]), x1 = N(a[3]), z1 = N(a[4]), lost = N(a[5]), trees = N(a[6]), day = N(a[7]), lastDay = N(a[8], -1),
                growDays = N(a[9]), stage = (LandStage)stage, tree = a[11], told = a.Length > 12 ? N(a[12]) : stage
            };
            return p.x1 < p.x0 || p.z1 < p.z0 ? null : p;
        }

        public static string PackHead(LandOutbreak o) =>
            string.Join(",", (int)o.kind, o.def ?? "", I(o.start), I(o.day), I(o.died), I(o.cut), I(o.ever), I(o.cold), o.asked ? "1" : "0", o.grazersLeft ? "1" : "0",
                o.burnWaiting ? "1" : "0", I(o.burner), I(o.burnSince));

        public static LandOutbreak UnpackHead(string s)
        {
            var a = (s ?? "").Split(',');
            if (a.Length < 13) return null;
            int kind = N(a[0], -1);
            if (kind < 0 || kind > 2 || a[1].Length == 0) return null;
            return new LandOutbreak
            {
                kind = (LandSickKind)kind, def = a[1], start = N(a[2]), day = N(a[3]), died = N(a[4]), cut = N(a[5]), ever = N(a[6]), cold = N(a[7]), asked = a[8] == "1",
                grazersLeft = a[9] == "1", burnWaiting = a[10] == "1", burner = N(a[11], -1), burnSince = N(a[12], -1)
            };
        }

        public static string Pack(LandSickPlant p) => I(p.cell) + ":" + I(p.day);

        public static bool UnpackSick(string s, out LandSickPlant p)
        {
            p = default;
            var a = (s ?? "").Split(':');
            if (a.Length != 2 || !int.TryParse(a[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int cell) || cell < 0) return false;
            p = new LandSickPlant(cell, N(a[1]));
            return true;
        }

        public static string Pack(LandWaterCell c) => I(c.cell) + ":" + c.was + ":" + I(c.ring) + ":" + (c.dried ? "d" : "s");

        public static LandWaterCell UnpackWater(string s)
        {
            var a = (s ?? "").Split(':');
            if (a.Length != 4 || a[1].Length == 0 || (a[3] != "d" && a[3] != "s")) return null;
            int cell = N(a[0], -1), ring = N(a[2], -1);
            if (cell < 0 || ring < 1 || ring > Math.Max(MaxShrink, MaxSwell)) return null;
            return new LandWaterCell { cell = cell, was = a[1], ring = ring, dried = a[3] == "d" };
        }

        public static string Pack(LandWornCell c) => I(c.cell) + ":" + c.was + ":" + I(c.block);

        public static LandWornCell UnpackWorn(string s)
        {
            var a = (s ?? "").Split(':');
            if (a.Length != 3 || a[1].Length == 0) return null;
            int cell = N(a[0], -1);
            return cell < 0 ? null : new LandWornCell { cell = cell, was = a[1], block = N(a[2]) };
        }

        public static string Pack(LandGrazing g) => I(g.block) + ":" + I(g.days) + ":" + I(g.rest) + ":" + (g.told ? "1" : "0");

        public static LandGrazing UnpackGrazing(string s)
        {
            var a = (s ?? "").Split(':');
            if (a.Length != 4) return null;
            int block = N(a[0], -1);
            return block < 0 ? null : new LandGrazing { block = block, days = Math.Max(0, N(a[1])), rest = Math.Max(0, N(a[2])), told = a[3] == "1" };
        }

        /// <summary>The list cut to its last <paramref name="max"/> entries (the oldest go first).</summary>
        public static void Trim<T>(List<T> list, int max)
        {
            if (list != null && list.Count > max) list.RemoveRange(0, list.Count - max);
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
