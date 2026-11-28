using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The land's record for one home map (docs/design/living-land-land.md), and its four small passes a day, a few hours
    /// apart, each looking at a few hundred cells at most:
    /// <list type="bullet">
    /// <item>the water (Nature/LandWater.cs): still water that draws back in a dry spell and swells in a wet one;</item>
    /// <item>the sickness of the wild plants (Nature/LandSickness.cs): one beginning, spreading, ending;</item>
    /// <item>the patches that come back (Nature/LandRegrowth.cs): burns, felled woods, what a wilt or a rust killed;</item>
    /// <item>the pasture (Nature/LandPasture.cs): ground the colony's animals graze bare, and back once rested.</item>
    /// </list>
    /// What it keeps is small and capped: the patches (LandMath.MaxPatches), the sickness going on and its sick plants by cell
    /// (LandMath.Cap), the cells the water left or came over with what each was (LandMath.MaxWaterCells), the cells grazed
    /// bare with what each was (LandMath.MaxWorn) and the grazing blocks (LandMath.MaxBlocks), so everything changed can be put
    /// back as the map's own. Saved as short lines (LandMath.Pack), worked on unpacked.
    /// </summary>
    public class LandBook : MapComponent
    {
        private List<string> patches = new List<string>();
        private string outbreak;
        private List<string> sickCells = new List<string>();
        private List<string> water = new List<string>();
        private List<string> worn = new List<string>();
        private List<string> grazing = new List<string>();

        /// <summary>Days of a dry and of a wet spell running (LandMath.Spell); the day the last sickness ended; the first day the land was looked at.</summary>
        internal int dryRun, wetRun, lastSickEnd = -99999, firstDay = -1;
        /// <summary>This spell's news told: the water drawn back, the water come over, the water back at its old edge.</summary>
        internal bool toldDry, toldWet, toldBack;

        [Unsaved] internal List<LandPatch> Patches = new List<LandPatch>();
        [Unsaved] internal LandOutbreak Outbreak;
        [Unsaved] internal List<LandWaterCell> Water = new List<LandWaterCell>();
        [Unsaved] internal List<LandWornCell> Worn = new List<LandWornCell>();
        [Unsaved] internal List<LandGrazing> Grazing = new List<LandGrazing>();

        /// <summary>The still water's edge as far as it has been found (LandWater.Find), not saved: found again after a load.</summary>
        [Unsaved] internal readonly HashSet<int> shore = new HashSet<int>();
        [Unsaved] internal readonly List<int> shoreList = new List<int>();
        [Unsaved] internal int shoreAt, bodyAt, bodyCellAt;

        public LandBook(Map map) : base(map)
        {
        }

        public static LandBook Of(Map map) => map?.GetComponent<LandBook>();

        private static Game lastGame;

        /// <summary>A map made or loaded: in a new game (or a load), what the land worked out and kept unsaved is worked out again.</summary>
        public override void FinalizeInit()
        {
            base.FinalizeInit();
            if (lastGame == Current.Game) return;
            lastGame = Current.Game;
            LandPlants.Forget();
            LandWater.Forget();
        }

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>Any of the land's parts is on.</summary>
        public static bool AnyOn => (S?.landRecovers ?? true) || (S?.landSickness ?? true) || (S?.landWater ?? true);

        /// <summary>The land's day: the local year's days counted on (as the weather record counts them).</summary>
        public int Today => GenLocalDate.Year(map) * WeatherMath.YearDays + GenLocalDate.DayOfYear(map);

        /// <summary>
        /// Today is a growing day here: yesterday's mean in the weather record was 6 °C or more (LandMath.Growing), or, before
        /// the record has a day, the land's usual for the time of year.
        /// </summary>
        public static bool GrowingToday(Map map)
        {
            var book = WeatherBook.Of(map);
            var d = book?.DaysBefore?.LastOrDefault(x => x != null && x.Counted);
            int today = GenLocalDate.Year(map) * WeatherMath.YearDays + GenLocalDate.DayOfYear(map);
            if (d != null && today - d.day <= 2) return LandMath.Growing(d.Mean);
            return LandMath.Growing(GenTemperature.AverageTemperatureAtTileForTwelfth(map.Tile, GenLocalDate.Twelfth(map)));
        }

        /// <summary>A wet day in the last three days of the weather record.</summary>
        public static bool WetLately(Map map)
        {
            var days = WeatherBook.Of(map)?.Days;
            if (days == null || days.Count == 0) return false;
            return days.Skip(Math.Max(0, days.Count - 3)).Any(d => d.Wet);
        }

        public override void MapComponentTick()
        {
            int tick = Find.TickManager.TicksGame;
            if (!Sched.Every(tick, Sched.RareTicks, Sched.RareLand)) return;
            // Switched off, the passes still run while the water or the grazing has ground to give back, or a sickness to end.
            if (!AnyOn && Water.Count == 0 && Worn.Count == 0 && Outbreak == null) return;
            if (!map.IsPlayerHome || MapFacts.InVacuum(map)) return;
            int part = LandMath.PartAt(Sched.Mod(tick, Sched.DayTicks) / Sched.RareTicks, map.uniqueID);
            if (part < 0) return;
            using var clock = PassClock.Time(Pass.RareLand);
            if (firstDay < 0) firstDay = Today;
            Run(part);
        }

        /// <summary>One of the land's parts (LandMath.PartAt), and the burn the council chose, if one waits for its hour.</summary>
        internal void Run(int part)
        {
            switch (part)
            {
                case LandMath.PartWater:
                    Safe("the water", () => LandWater.Pass(map, this));
                    break;
                case LandMath.PartSickness:
                    Safe("the sickness", () => LandSickness.Pass(map, this));
                    break;
                case LandMath.PartRegrowth:
                    Safe("the land coming back", () => LandRegrowth.Pass(map, this));
                    break;
                default:
                    Safe("the pasture", () => LandPasture.Pass(map, this));
                    break;
            }
            if (Outbreak != null) Safe("the wilting trees", () => LandSickness.Keep(map, this));
            if (Outbreak != null && Outbreak.burnWaiting) Safe("the burn", () => LandSickness.BurnHour(map, this));
        }

        private static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the land: " + what, e);
            }
        }

        // ---------------- The patches ----------------

        /// <summary>
        /// A plant lost to a cause at a cell: it joins a patch of that cause close by, or opens a new one (the patches that
        /// never come to count are let go: LandMath.Stale). Cheap: called as each plant burns or is felled.
        /// </summary>
        internal void Lost(LandCause cause, IntVec3 c, bool tree, ThingDef def)
        {
            for (int i = 0; i < Patches.Count; i++)
            {
                var p = Patches[i];
                if (p.cause != cause || !LandMath.Joins(p, c.x, c.z)) continue;
                LandMath.Add(p, c.x, c.z, tree, tree ? def?.defName : null);
                // Burned or felled again while it was coming back: it starts over once it is left alone.
                if (p.Counts) LandMath.StartOver(p);
                return;
            }
            if (Patches.Count >= LandMath.MaxPatches)
            {
                // Room for a new one: the oldest that hasn't come to count goes, else none is opened.
                int pending = Patches.FindIndex(x => !x.Counts);
                if (pending < 0) return;
                Patches.RemoveAt(pending);
            }
            Patches.Add(LandMath.Open(cause, c.x, c.z, Today, tree, tree ? def?.defName : null));
        }

        // ---------------- The water and the pasture by cell ----------------

        internal LandWaterCell WaterAt(int cell)
        {
            for (int i = 0; i < Water.Count; i++)
                if (Water[i].cell == cell)
                    return Water[i];
            return null;
        }

        internal LandGrazing GrazingOf(int block, bool make)
        {
            var g = Grazing.FirstOrDefault(x => x.block == block);
            if (g != null || !make) return g;
            if (Grazing.Count >= LandMath.MaxBlocks)
            {
                // The block resting longest with nothing worn goes to make room.
                var spare = Grazing.Where(x => x.days == 0 && !Worn.Any(w => w.block == x.block)).OrderByDescending(x => x.rest).FirstOrDefault();
                if (spare == null) return null;
                Grazing.Remove(spare);
            }
            g = new LandGrazing { block = block };
            Grazing.Add(g);
            return g;
        }

        /// <summary>A line for the dev actions: what the record holds now.</summary>
        internal string Describe()
        {
            var parts = new List<string>
            {
                Patches.Count + " patch(es): " + string.Join("; ", Patches.Select(p => p.cause + " at " + p.CenterX + "," + p.CenterZ + " lost " + p.lost + " (" + p.trees + " trees), " + p.growDays + " growing days, " + p.stage + (p.Counts ? "" : ", not counting yet"))),
                Outbreak == null ? "no sickness" : Outbreak.kind + " in " + Outbreak.def + ": " + Outbreak.sick.Count + " sick, " + Outbreak.died + " died, " + Outbreak.cut + " gone, " + Outbreak.ever + " in all, since day " + Outbreak.day + (Outbreak.asked ? ", asked" : "") + (Outbreak.burnWaiting ? ", a burn waits" : ""),
                "water: " + Water.Count(w => w.dried) + " cells dried, " + Water.Count(w => !w.dried) + " come over, dry run " + dryRun + ", wet run " + wetRun + ", wetness " + LandWet.SeasonWet(map).ToString("0.00") + ", edge cells known " + shoreList.Count,
                "pasture: " + Grazing.Count + " block(s) (" + string.Join(", ", Grazing.Select(g => g.block + ": " + g.days + " overgrazed, " + g.rest + " rested")) + "), " + Worn.Count + " cells worn"
            };
            return string.Join("\n", parts);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                patches = Patches.Select(LandMath.Pack).ToList();
                outbreak = Outbreak != null ? LandMath.PackHead(Outbreak) : null;
                sickCells = Outbreak != null ? Outbreak.sick.Select(LandMath.Pack).ToList() : new List<string>();
                water = Water.Select(LandMath.Pack).ToList();
                worn = Worn.Select(LandMath.Pack).ToList();
                grazing = Grazing.Select(LandMath.Pack).ToList();
            }
            Scribe_Collections.Look(ref patches, "patches", LookMode.Value);
            Scribe_Values.Look(ref outbreak, "outbreak");
            Scribe_Collections.Look(ref sickCells, "sickCells", LookMode.Value);
            Scribe_Collections.Look(ref water, "water", LookMode.Value);
            Scribe_Collections.Look(ref worn, "worn", LookMode.Value);
            Scribe_Collections.Look(ref grazing, "grazing", LookMode.Value);
            Scribe_Values.Look(ref dryRun, "dryRun");
            Scribe_Values.Look(ref wetRun, "wetRun");
            Scribe_Values.Look(ref lastSickEnd, "lastSickEnd", -99999);
            Scribe_Values.Look(ref firstDay, "firstDay", -1);
            Scribe_Values.Look(ref toldDry, "toldDry");
            Scribe_Values.Look(ref toldWet, "toldWet");
            Scribe_Values.Look(ref toldBack, "toldBack");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Patches = (patches ?? new List<string>()).Select(LandMath.UnpackPatch).Where(p => p != null).ToList();
                LandMath.Trim(Patches, LandMath.MaxPatches);
                Outbreak = outbreak != null ? LandMath.UnpackHead(outbreak) : null;
                if (Outbreak != null)
                {
                    foreach (var s in sickCells ?? new List<string>())
                        if (LandMath.UnpackSick(s, out var p))
                            Outbreak.sick.Add(p);
                    LandMath.Trim(Outbreak.sick, LandMath.Cap(Outbreak.kind));
                }
                Water = (water ?? new List<string>()).Select(LandMath.UnpackWater).Where(w => w != null).ToList();
                LandMath.Trim(Water, LandMath.MaxWaterCells);
                Worn = (worn ?? new List<string>()).Select(LandMath.UnpackWorn).Where(w => w != null).ToList();
                LandMath.Trim(Worn, LandMath.MaxWorn);
                Grazing = (grazing ?? new List<string>()).Select(LandMath.UnpackGrazing).Where(g => g != null).ToList();
                LandMath.Trim(Grazing, LandMath.MaxBlocks);
            }
            if (Scribe.mode == LoadSaveMode.Saving || Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Only the lists in memory are kept between saves.
                patches = new List<string>();
                sickCells = new List<string>();
                water = new List<string>();
                worn = new List<string>();
                grazing = new List<string>();
            }
        }
    }
}
