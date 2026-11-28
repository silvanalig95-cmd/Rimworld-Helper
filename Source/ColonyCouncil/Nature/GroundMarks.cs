using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The ground that stays (the player's pick D1, 28 November 2026; docs/design/living-land-ground.md). Marks left on a
    /// home map that remain, each a few to a few dozen cells, always out on the land, never on or within
    /// <see cref="GroundMath.ColonyGap"/> cells of anything of the colony's, never in its home area or a zone, never where
    /// someone stands (GroundMath.CellAllowed):
    /// <list type="bullet">
    /// <item>after an earthquake (Seasonal Weather's, Vanilla Events Expanded's, Rimefeller's drill tremors, any other mod's
    /// by its name) and after Rimfolk's own rockslide and warm vent: a cave opened into a rock face, a spring, a fissure
    /// across open ground, or a scree slope at the foot of the rock (GroundMath.PickAfterQuake), of the map's own rock;</item>
    /// <item>after days of heavy rain on hilly land: a landslide at the foot of a rock face, rubble against the face (the
    /// game's collapsed rocks, mined away) and stones and mud below;</item>
    /// <item>after a wet season: a sinkhole, the ground sagging first (the sign, kept on the map) and giving way some hours
    /// later.</item>
    /// </list>
    /// Each is shown where it happens (dust, the shake, a splash), told in a plain message with the place to look at,
    /// written into the chronicle, and remarked on by whoever of the colony is near. A colonist caught where a landslide
    /// comes down or the ground gives way may be hurt (the shared harm budget, HarmKinds.Ground); nobody is buried. No
    /// mark comes within <see cref="GroundMath.MarkGapDays"/> days of the last one.
    /// </summary>
    public static class GroundMarks
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The setting "The ground keeps the marks of quakes, landslides and sinkholes".</summary>
        public static bool On => S?.groundMarks ?? true;

        private static int Now => Find.TickManager.TicksGame;

        private const int HourTicks = GroundMath.HourTicks;

        // ================= Quakes =================

        /// <summary>One of the game's events happened at a home: if it is a quake, it is noted, and its mark may come once the shaking is over.</summary>
        internal static void Noted(Map map, string defName, string worker)
        {
            if (!On || map == null || !Homes.IsHome(map)) return;
            var source = GroundMath.SourceOf(defName, worker);
            if (source == QuakeSource.None) return;
            var g = GroundMap.Of(map);
            if (g != null) Note(g, source, Epicentre(map), Now);
        }

        /// <summary>A quake noted: the same quake within a few hours (an incident and the condition it starts) only waits longer.</summary>
        private static void Note(GroundMap g, QuakeSource source, IntVec3 at, int now)
        {
            int due = now + GroundMath.MarkDelayHours * HourTicks;
            if (g.quakeNoted >= 0 && now >= g.quakeNoted && now - g.quakeNoted < GroundMath.SameQuakeHours * HourTicks && g.quakeDue >= 0)
            {
                if (!g.quakeAt.IsValid) g.quakeAt = at;
                if (source == QuakeSource.Quake) g.quakeSource = source;
                g.quakeDue = Math.Max(g.quakeDue, due);
                return;
            }
            g.quakeNoted = now;
            g.quakeSource = source;
            g.quakeAt = at;
            g.quakeDue = due;
        }

        /// <summary>Where Vanilla Events Expanded's earthquake is centred while it shakes, else Invalid.</summary>
        private static IntVec3 Epicentre(Map map)
        {
            try
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail("VEE_EarthquakeEpicenter");
                if (def == null) return IntVec3.Invalid;
                var list = map.listerThings.ThingsOfDef(def);
                return list.Count > 0 ? list[list.Count - 1].Position : IntVec3.Invalid;
            }
            catch
            {
                return IntVec3.Invalid;
            }
        }

        /// <summary>A quake condition is going on here (Seasonal Weather's earthquake, or any other by its name).</summary>
        public static bool QuakeOn(Map map)
        {
            var conditions = map?.gameConditionManager?.ActiveConditions;
            if (conditions == null) return false;
            for (int i = 0; i < conditions.Count; i++)
                if (GroundMath.IsQuakeCondition(conditions[i]?.def?.defName)) return true;
            return false;
        }

        // ================= The hour, the rain, the day =================

        /// <summary>The hour's look: a quake going on, a quake's mark once it's over, a sinkhole whose time has come.</summary>
        internal static void Hourly(GroundMap g, Map map, int now)
        {
            if (!On)
            {
                // Switched off: nothing is left half done.
                g.quakeDue = -1;
                g.sinkDue = -1;
                g.sinkAt = IntVec3.Invalid;
                return;
            }
            bool on = QuakeOn(map);
            if (on)
            {
                if (!g.quakeOn) Note(g, QuakeSource.Quake, IntVec3.Invalid, now);
                g.quakeDue = Math.Max(g.quakeDue, now + GroundMath.MarkDelayHours * HourTicks);
            }
            g.quakeOn = on;
            if (g.quakeDue >= 0 && now >= g.quakeDue && !on)
            {
                var source = g.quakeSource;
                var at = g.quakeAt;
                g.quakeDue = -1;
                g.quakeAt = IntVec3.Invalid;
                if (GroundMath.MayMark(g.lastMark, now) && Rand.Chance(GroundMath.MarkChance(source)) && AfterQuake(map, source, at)) g.lastMark = now;
            }
            if (g.sinkDue >= 0 && now >= g.sinkDue) OpenSinkhole(g, map, now);
        }

        /// <summary>
        /// The rain's hour: the first hour of a day that it rains on hilly land after <see cref="GroundMath.SlideWetRun"/> wet
        /// days running has the day's roll for a landslide.
        /// </summary>
        internal static void RainHour(GroundMap g, Map map, int now)
        {
            if (!On) return;
            int day = now / GroundMath.DayTicks;
            if (g.slideDay == day) return;
            var w = map.weatherManager?.curWeather;
            if (w == null || map.weatherManager.SnowRate >= 0.05f || map.weatherManager.RainRate < 0.5f) return;
            var tile = map.TileInfo;
            bool hilly = tile != null && tile.hilliness >= Hilliness.SmallHills;
            var book = WeatherBook.Of(map);
            if (!hilly || book == null) return;
            WeatherMath.RunsWithToday(book.DaysBefore, book.Today, out int wet, out _, out _);
            if (wet < GroundMath.SlideWetRun) return;
            g.slideDay = day;
            if (!GroundMath.MayMark(g.lastMark, now) || !GroundMath.SlideToday(true, true, true, Flavor.IsStorm(w) || w.defName.IndexOf("Torrential", StringComparison.OrdinalIgnoreCase) >= 0, wet, Rand.Value))
                return;
            if (Landslide(map)) g.lastMark = now;
        }

        /// <summary>The day's look: the season that ended, and whether it was wet enough to leave a sinkhole.</summary>
        internal static void Daily(GroundMap g, Map map, int now)
        {
            if (!On) return;
            var quarters = WeatherBook.Of(map)?.Quarters;
            if (quarters == null || quarters.Count == 0) return;
            var last = quarters[quarters.Count - 1];
            int key = GroundMath.SeasonKey(last.year, last.quadrum);
            // The first look starts from now: a season ended before the ground was watched isn't asked about.
            if (g.seasonSeen < 0 || key <= g.seasonSeen)
            {
                if (g.seasonSeen < 0) g.seasonSeen = key;
                return;
            }
            g.seasonSeen = key;
            if (!GroundMath.WetSeason(last.wetDays) || g.sinkDue >= 0 || !GroundMath.MayMark(g.lastMark, now)) return;
            if (Rand.Chance(GroundMath.SinkholeChanceFor(Karst(map))) && SinkholeSign(g, map, now)) g.lastMark = now;
        }

        /// <summary>The land's rock dissolves in water (limestone, marble, chalk).</summary>
        private static bool Karst(Map map)
        {
            try
            {
                return Find.World.NaturalRockTypesIn(map.Tile).Any(r => GroundMath.Karst(r?.defName));
            }
            catch
            {
                return false;
            }
        }

        // ================= Where a mark may go =================

        /// <summary>The cells near anything of the colony's (built, a blueprint, a frame), worked out once for each mark.</summary>
        internal sealed class Clearance
        {
            private readonly Map map;
            private readonly BoolGrid near;

            internal Clearance(Map map)
            {
                this.map = map;
                near = new BoolGrid(map);
                int gap = Mathf.CeilToInt(GroundMath.ColonyGap);
                foreach (var b in map.listerBuildings.allBuildingsColonist) Mark(b, gap);
                foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint)) if (t.Faction == Faction.OfPlayer) Mark(t, gap);
                foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame)) if (t.Faction == Faction.OfPlayer) Mark(t, gap);
            }

            private void Mark(Thing t, int gap)
            {
                if (t == null || !t.Spawned) return;
                var rect = t.OccupiedRect().ExpandedBy(gap).ClipInsideMap(map);
                foreach (var c in rect) near[c] = true;
            }

            /// <summary>A cell a mark may change (GroundMath.CellAllowed); <paramref name="rock"/>: one of natural rock, to be carved.</summary>
            internal bool Allowed(IntVec3 c, bool rock = false, bool fallsIn = false)
            {
                if (!c.InBounds(map)) return false;
                bool built = rock ? !StorytellerEvents.NaturalRock(c, map) : c.GetEdifice(map) != null;
                // fallsIn: a sinkhole opening where its sign was, under whoever happens to stand there now.
                bool someone = !fallsIn && c.GetFirstPawn(map) != null;
                return GroundMath.CellAllowed(near[c], map.areaManager.Home[c], c.GetZone(map) != null, built, someone);
            }

            /// <summary>Open natural ground a mark may lie on: allowed, seen, under the sky, no water or floor, nothing lying there, no tree.</summary>
            internal bool Open(IntVec3 c)
            {
                if (!Allowed(c) || !c.Standable(map) || c.Fogged(map) || c.Roofed(map) || c.GetFirstItem(map) != null) return false;
                if (!Natural(c.GetTerrain(map))) return false;
                return c.GetPlant(map)?.def.plant?.IsTree != true;
            }
        }

        /// <summary>Natural ground: not water, not a floor, not molten lava (cooled lava is ground like any other).</summary>
        internal static bool Natural(TerrainDef t) =>
            t != null && t.natural && !t.IsWater && !t.IsFloor
            && (t.defName == null || !t.defName.StartsWith("Lava", StringComparison.Ordinal) && t.defName.IndexOf("LiquidLava", StringComparison.Ordinal) < 0);

        /// <summary>
        /// A rock face: natural rock of the map's own (not ore), seen, with open ground beside it on one side and plenty of
        /// rock behind it (GroundMath.RockMass within 4 cells). <paramref name="open"/> is the open cell before it.
        /// </summary>
        private static bool Face(Map map, Clearance clear, IntVec3 rock, out IntVec3 open)
        {
            open = IntVec3.Invalid;
            if (!StorytellerEvents.NaturalRock(rock, map) || rock.Fogged(map)) return false;
            var def = rock.GetEdifice(map).def;
            if (def.building.isResourceRock || def.building.mineableThing == null) return false;
            foreach (var d in GenAdj.CardinalDirections.InRandomOrder())
            {
                var o = rock + d;
                if (!clear.Open(o)) continue;
                open = o;
                break;
            }
            if (!open.IsValid) return false;
            int mass = 0;
            foreach (var c in GenRadial.RadialCellsAround(rock, 4f, false))
                if (StorytellerEvents.NaturalRock(c, map) && ++mass >= GroundMath.RockMass) return true;
            return false;
        }

        /// <summary>A rock face, near <paramref name="near"/> first when it's known.</summary>
        private static bool TryFace(Map map, Clearance clear, IntVec3 near, out IntVec3 rock, out IntVec3 open)
        {
            IntVec3 o = IntVec3.Invalid;
            bool Fits(IntVec3 c) => Face(map, clear, c, out o);
            if (near.IsValid && CellFinder.TryFindRandomCellNear(near, map, 40, Fits, out rock) && Face(map, clear, rock, out open)) return true;
            if (CellFinderLoose.TryGetRandomCellWith(Fits, map, 3000, out rock) && Face(map, clear, rock, out open)) return true;
            rock = open = IntVec3.Invalid;
            return false;
        }

        /// <summary>Open ground for a mark that needs nothing but itself, near <paramref name="near"/> first when it's known.</summary>
        private static bool TryOpen(Map map, Clearance clear, IntVec3 near, Predicate<IntVec3> more, out IntVec3 cell)
        {
            bool Fits(IntVec3 c) => clear.Open(c) && (more == null || more(c));
            if (near.IsValid && CellFinder.TryFindRandomCellNear(near, map, 40, Fits, out cell)) return true;
            return CellFinderLoose.TryGetRandomCellWith(Fits, map, 1500, out cell);
        }

        /// <summary>The chunk of a rock (its own, else the land's first), and its rough ground.</summary>
        private static ThingDef ChunkOf(Map map, ThingDef rock)
        {
            var chunk = rock?.building?.mineableThing;
            if (chunk != null && chunk.category == ThingCategory.Item) return chunk;
            return LandRock(map)?.building?.mineableThing ?? DefDatabase<ThingDef>.GetNamedSilentFail("ChunkGranite");
        }

        private static TerrainDef RoughOf(Map map, ThingDef rock) =>
            rock?.building?.naturalTerrain ?? LandRock(map)?.building?.naturalTerrain ?? DefDatabase<TerrainDef>.GetNamedSilentFail("Gravel");

        /// <summary>The land's own rock: the first of the tile's natural rock types.</summary>
        private static ThingDef LandRock(Map map)
        {
            try
            {
                return Find.World.NaturalRockTypesIn(map.Tile).FirstOrDefault(r => r?.building?.mineableThing != null);
            }
            catch
            {
                return null;
            }
        }

        private static void Rubble(Map map, IntVec3 c, float chance = 0.5f)
        {
            if (c.InBounds(map) && c.Standable(map) && Rand.Chance(chance)) FilthMaker.TryMakeFilth(c, map, ThingDefOf.Filth_RubbleRock);
        }

        private static void Dust(Map map, IntVec3 c, float size = 1.6f) =>
            FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(), map, size, new Color(0.62f, 0.58f, 0.52f, 0.8f));

        private static void Shake(Map map, float how)
        {
            try
            {
                if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(how);
            }
            catch
            {
                // The camera isn't there (a test run, a world view): nothing to shake.
            }
        }

        /// <summary>Plants on a cell cleared away (not trees: no mark goes where a tree stands).</summary>
        private static void ClearPlants(Map map, IntVec3 c)
        {
            var plant = c.GetPlant(map);
            if (plant != null && plant.def.plant?.IsTree != true) plant.Destroy(DestroyMode.Vanish);
        }

        private static void Chunk(Map map, ThingDef chunk, IntVec3 c)
        {
            if (chunk == null || !c.InBounds(map) || c.GetFirstItem(map) != null || !c.Standable(map)) return;
            GenSpawn.Spawn(ThingMaker.MakeThing(chunk), c, map);
        }

        // ================= After a quake =================

        /// <summary>The mark a quake leaves: the kind picked for what's near, else any other kind the land has room for. True when one was left.</summary>
        private static bool AfterQuake(Map map, QuakeSource source, IntVec3 at)
        {
            var clear = new Clearance(map);
            bool face = TryFace(map, clear, at, out var rock, out var open);
            bool ground = TryOpen(map, clear, at, null, out var spot);
            var first = GroundMath.PickAfterQuake(source, face, ground, Rand.Value);
            if (first == GroundMarkKind.None) return false;
            // The kind picked first; if the land has no room for it after all, another this quake may leave.
            var order = new List<GroundMarkKind> { first };
            foreach (var k in new[] { GroundMarkKind.Cave, GroundMarkKind.Scree, GroundMarkKind.Fissure, GroundMarkKind.Spring }.InRandomOrder())
                if (k != first && Allowed(source, k)) order.Add(k);
            foreach (var kind in order)
            {
                bool done;
                switch (kind)
                {
                    case GroundMarkKind.Cave: done = face && Cave(map, clear, rock, open, source); break;
                    case GroundMarkKind.Scree: done = face && Scree(map, clear, rock, open, source); break;
                    case GroundMarkKind.Fissure: done = ground && Fissure(map, clear, spot, source); break;
                    case GroundMarkKind.Spring: done = ground && Spring(map, clear, spot, source); break;
                    default: done = false; break;
                }
                if (done) return true;
            }
            return false;
        }

        /// <summary>The kinds a source may leave (the same as GroundMath.PickAfterQuake's).</summary>
        private static bool Allowed(QuakeSource source, GroundMarkKind kind)
        {
            switch (source)
            {
                case QuakeSource.Quake: return kind == GroundMarkKind.Cave || kind == GroundMarkKind.Scree || kind == GroundMarkKind.Fissure || kind == GroundMarkKind.Spring;
                case QuakeSource.Rockslide: return kind == GroundMarkKind.Cave || kind == GroundMarkKind.Spring;
                case QuakeSource.WarmVent: return kind == GroundMarkKind.Spring || kind == GroundMarkKind.Fissure;
                default: return false;
            }
        }

        private static string What(QuakeSource source) => ("CC_Ground_What_" + source).Translate().Resolve();

        /// <summary>
        /// A cave: a short hollow carved into the rock face from the open cell before it, each cell keeping rock about it
        /// (GroundMath.CaveCellHolds), the rock's own roof kept over it (thin rock where there was none), loose stone of the
        /// rock on its floor; one time in three a seam of ore shows in its wall. Nobody of the colony near as it opens.
        /// </summary>
        internal static bool Cave(Map map, Clearance clear, IntVec3 face, IntVec3 open, QuakeSource source)
        {
            if (map.mapPawns.FreeColonistsSpawned.Any(p => p.Position.InHorDistOf(face, GroundMath.CaveClearOfPeople))) return false;
            var rockDef = face.GetEdifice(map)?.def;
            if (rockDef == null) return false;
            int want = GroundMath.Cells(GroundMarkKind.Cave, Rand.Value);
            var into = face - open;
            var side = new IntVec3(into.z, 0, into.x);
            var otherSide = IntVec3.Zero - side;
            var cells = new List<IntVec3>();
            var cur = face;
            for (int i = 0; i < want + 4 && cells.Count < want; i++)
            {
                if (!clear.Allowed(cur, true) || cur.GetEdifice(map)?.def != rockDef) break;
                int around = 0;
                foreach (var d in GenAdj.AdjacentCells)
                    if (!cells.Contains(cur + d) && StorytellerEvents.NaturalRock(cur + d, map)) around++;
                // One of those may be carved next: what is left once it is must still hold.
                if (!GroundMath.CaveCellHolds(around - 1)) break;
                cells.Add(cur);
                // On, mostly straight in, now and then a step aside.
                var next = cur + into;
                if (Rand.Chance(0.3f))
                {
                    var aside = cur + (Rand.Bool ? side : otherSide);
                    if (StorytellerEvents.NaturalRock(aside, map) && !cells.Contains(aside)) next = aside;
                }
                cur = next;
            }
            if (cells.Count < GroundMath.Fewest(GroundMarkKind.Cave)) return false;
            var chunk = ChunkOf(map, rockDef);
            Shake(map, 1f);
            foreach (var c in cells)
            {
                var rock = c.GetEdifice(map);
                rock?.Destroy(DestroyMode.Vanish);
                if (c.GetRoof(map) == null) map.roofGrid.SetRoof(c, RoofDefOf.RoofRockThin);
                Rubble(map, c, 0.7f);
                Dust(map, c);
            }
            foreach (var c in cells.Skip(1).InRandomOrder().Take(Rand.RangeInclusive(1, 2))) Chunk(map, chunk, c);
            string ore = null;
            if (Rand.Chance(0.33f))
            {
                var seam = StorytellerEvents.Seam(map, cells[cells.Count - 1], StorytellerEvents.Ore(false), Rand.RangeInclusive(1, 3));
                if (seam.Count > 0) ore = seam[0].def.building?.mineableThing?.label ?? seam[0].LabelNoCount;
            }
            string place = StorytellerEvents.Direction(map, face);
            string text = ore == null
                ? "CC_Ground_Cave".Translate(What(source).Named("WHAT"), place.Named("PLACE")).Resolve()
                : "CC_Ground_CaveSeam".Translate(What(source).Named("WHAT"), place.Named("PLACE"), ore.Named("ORE")).Resolve();
            Told(map, GroundMarkKind.Cave, text, new LookTargets(new TargetInfo(face, map)), face, place);
            return true;
        }

        /// <summary>A scree slope: stony soil and chunks of the face's own rock fanned out at its foot, the nearest cells first.</summary>
        internal static bool Scree(Map map, Clearance clear, IntVec3 face, IntVec3 open, QuakeSource source)
        {
            var rockDef = face.GetEdifice(map)?.def;
            var cells = Fan(map, clear, face, open, 1, 3.5f, GroundMath.Cells(GroundMarkKind.Scree, Rand.Value));
            if (cells.Count < GroundMath.Fewest(GroundMarkKind.Scree)) return false;
            var gravel = DefDatabase<TerrainDef>.GetNamedSilentFail("Gravel");
            var chunk = ChunkOf(map, rockDef);
            Shake(map, 0.8f);
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                ClearPlants(map, c);
                if (gravel != null && c.GetTerrain(map) != gravel && Natural(c.GetTerrain(map))) map.terrainGrid.SetTerrain(c, gravel);
                if (i % GroundMath.ChunkOneIn == 0) Chunk(map, chunk, c);
                Rubble(map, c);
                if (i % 2 == 0) Dust(map, c);
            }
            string place = StorytellerEvents.Direction(map, open);
            Told(map, GroundMarkKind.Scree, "CC_Ground_Scree".Translate(What(source).Named("WHAT"), place.Named("PLACE")).Resolve(), new LookTargets(new TargetInfo(open, map)), open, place);
            return true;
        }

        /// <summary>
        /// Cells fanned out from a rock face over open ground: round a spot <paramref name="out"/> cells out from the face,
        /// within <paramref name="radius"/>, on the open side of it, nearest the face first.
        /// </summary>
        private static List<IntVec3> Fan(Map map, Clearance clear, IntVec3 face, IntVec3 open, int @out, float radius, int count)
        {
            var away = open - face;
            var centre = open + new IntVec3(away.x * @out, 0, away.z * @out);
            return GenRadial.RadialCellsAround(centre, radius, true)
                .Where(c => c.InBounds(map) && clear.Open(c) && (c - face).x * away.x + (c - face).z * away.z > 0)
                .OrderBy(c => c.DistanceToSquared(face)).Take(count).ToList();
        }

        /// <summary>A fissure: a jagged line across open ground turned the map's own rough rock, stones thrown up beside it.</summary>
        internal static bool Fissure(Map map, Clearance clear, IntVec3 start, QuakeSource source)
        {
            int want = GroundMath.Cells(GroundMarkKind.Fissure, Rand.Value);
            var dir = GenAdj.AdjacentCells[Rand.Range(0, 8)];
            var side = new IntVec3(-dir.z, 0, dir.x);
            var otherSide = IntVec3.Zero - side;
            var cells = new List<IntVec3>();
            var cur = start;
            for (int i = 0; i < want + 4 && cells.Count < want; i++)
            {
                if (!clear.Open(cur) || cells.Contains(cur)) break;
                cells.Add(cur);
                var next = cur + dir;
                if (Rand.Chance(0.3f)) next += Rand.Bool ? side : otherSide;
                cur = next;
            }
            if (cells.Count < GroundMath.Fewest(GroundMarkKind.Fissure)) return false;
            var rough = RoughOf(map, null);
            var chunk = ChunkOf(map, null);
            Shake(map, 0.8f);
            foreach (var c in cells)
            {
                ClearPlants(map, c);
                if (rough != null) map.terrainGrid.SetTerrain(c, rough);
                Rubble(map, c, 0.8f);
                Dust(map, c, 1.2f);
            }
            int stones = Rand.RangeInclusive(2, 4);
            foreach (var c in cells.SelectMany(x => GenAdj.CardinalDirections.Select(d => x + d)).Distinct().Where(c => !cells.Contains(c) && clear.Open(c)).InRandomOrder())
            {
                if (stones-- <= 0) break;
                Chunk(map, chunk, c);
            }
            var mid = cells[cells.Count / 2];
            string place = StorytellerEvents.Direction(map, mid);
            Told(map, GroundMarkKind.Fissure, "CC_Ground_Fissure".Translate(What(source).Named("WHAT"), place.Named("PLACE")).Resolve(), new LookTargets(new TargetInfo(mid, map)), mid, place);
            return true;
        }

        /// <summary>
        /// A spring: a small pool of the game's shallow water come up out of open ground; with Odyssey, warm water now and then
        /// (always after the warm vent's tremor).
        /// </summary>
        internal static bool Spring(Map map, Clearance clear, IntVec3 centre, QuakeSource source)
        {
            int want = GroundMath.Cells(GroundMarkKind.Spring, Rand.Value);
            var cells = GenRadial.RadialCellsAround(centre, 2.5f, true).Where(c => c.InBounds(map) && clear.Open(c)).Take(want).ToList();
            if (cells.Count < GroundMath.Fewest(GroundMarkKind.Spring) || !cells.Contains(centre)) return false;
            var hot = DefDatabase<TerrainDef>.GetNamedSilentFail("HotSpring");
            bool warm = hot != null && (source == QuakeSource.WarmVent || Rand.Chance(0.33f));
            var water = warm ? hot : TerrainDefOf.WaterShallow;
            foreach (var c in cells)
            {
                ClearPlants(map, c);
                map.terrainGrid.SetTerrain(c, water);
                FleckMaker.WaterSplash(c.ToVector3Shifted(), map, 1.6f, 1.5f);
            }
            string place = StorytellerEvents.Direction(map, centre);
            Told(map, GroundMarkKind.Spring, (warm ? "CC_Ground_SpringWarm" : "CC_Ground_Spring").Translate(What(source).Named("WHAT"), place.Named("PLACE")).Resolve(),
                new LookTargets(new TargetInfo(centre, map)), centre, place);
            return true;
        }

        // ================= A landslide =================

        /// <summary>
        /// After days of heavy rain, a hillside comes down at a rock face: the game's collapsed rocks against the face (never
        /// on anyone or anything), chunks of the rock and mud below. Whoever of the colony was on that ground may be hurt; the
        /// others there get clear. True when it came down.
        /// </summary>
        public static bool Landslide(Map map)
        {
            var clear = new Clearance(map);
            if (!TryFace(map, clear, IntVec3.Invalid, out var face, out var open)) return false;
            var rockDef = face.GetEdifice(map)?.def;
            int want = GroundMath.Cells(GroundMarkKind.Landslide, Rand.Value);
            // The slide comes down over people too: the cells are open ground as for the rest, but someone standing there is
            // caught at its edge rather than keeping it away.
            var cells = Fan(map, clear, face, open, 2, 5f, want);
            if (cells.Count < GroundMath.Fewest(GroundMarkKind.Landslide)) return false;
            var caught = map.mapPawns.FreeColonistsSpawned.Where(p => !p.Downed && cells.Any(c => p.Position.InHorDistOf(c, 1.5f))).ToList();
            int rubble = GroundMath.SlideRubble(cells.Count);
            var chunk = ChunkOf(map, rockDef);
            var mud = TerrainDefOf.Mud;
            Shake(map, 1.4f);
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                ClearPlants(map, c);
                Dust(map, c, 2f);
                if (i < rubble && c.GetFirstPawn(map) == null && c.GetFirstItem(map) == null)
                {
                    GenSpawn.Spawn(ThingDefOf.CollapsedRocks, c, map, WipeMode.Vanish);
                    continue;
                }
                if (mud != null && Natural(c.GetTerrain(map))) map.terrainGrid.SetTerrain(c, mud);
                if (i % GroundMath.ChunkOneIn == 0) Chunk(map, chunk, c);
                Rubble(map, c, 0.7f);
            }
            string place = StorytellerEvents.Direction(map, open);
            Told(map, GroundMarkKind.Landslide, "CC_Ground_Landslide".Translate(place.Named("PLACE")).Resolve(), new LookTargets(new TargetInfo(open, map)), open, place);
            foreach (var p in caught) Caught(p, HurtKind.Crush, HurtWhere.Leg, 3f, 8f, GroundMath.SlideHurtChance, "CC_Ground_HurtSlide", "CC_Ground_ClearSlide");
            SomeSay(map, "Smoulder", HandKind.Wrath, "CC_Ground_Hand_Landslide", 0.5f);
            return true;
        }

        // ================= A sinkhole =================

        /// <summary>The cells of a sinkhole round <paramref name="at"/>: all of them open ground, else none.</summary>
        private static List<IntVec3> SinkCells(Map map, Clearance clear, IntVec3 at, int count, bool forOpening)
        {
            float r = GroundMath.SinkholeRadius(count);
            var cells = GenRadial.RadialCellsAround(at, r, true).ToList();
            foreach (var c in cells)
            {
                if (!c.InBounds(map)) return new List<IntVec3>();
                // At the opening someone may be standing there (they fall), and things left lying there since stay where they are.
                bool ok = forOpening ? clear.Allowed(c, false, true) && !c.Roofed(map) : clear.Open(c);
                if (!ok || !Natural(c.GetTerrain(map)) || c.GetPlant(map)?.def.plant?.IsTree == true) return new List<IntVec3>();
            }
            return cells;
        }

        /// <summary>The sign: after a wet season the ground sags and cracks over the place, kept until it gives way some hours later.</summary>
        public static bool SinkholeSign(GroundMap g, Map map, int now)
        {
            var clear = new Clearance(map);
            int count = GroundMath.Cells(GroundMarkKind.Sinkhole, Rand.Value);
            if (!TryOpen(map, clear, IntVec3.Invalid, c => c.GetTerrain(map).fertility > 0f && SinkCells(map, clear, c, count, false).Count > 0, out var at)) return false;
            foreach (var c in SinkCells(map, clear, at, count, false))
            {
                FilthMaker.TryMakeFilth(c, map, ThingDefOf.Filth_LooseGround, 1);
                if (Rand.Chance(0.3f)) Dust(map, c, 1f);
            }
            g.sinkAt = at;
            g.sinkCells = count;
            g.sinkDue = now + GroundMath.SinkDelayHours(Rand.Value) * HourTicks;
            string place = StorytellerEvents.Direction(map, at);
            Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_Ground_SinkSign".Translate(place.Named("PLACE")).Resolve(), new LookTargets(new TargetInfo(at, map)),
                MessageTypeDefOf.CautionInput);
            return true;
        }

        /// <summary>
        /// The ground gives way where it sagged: water in the middle (deep in a large one's very middle, where nobody stands),
        /// mud round its edge. Whoever stood there falls and may be hurt. Something of the colony's put there since keeps it
        /// from opening.
        /// </summary>
        public static bool OpenSinkhole(GroundMap g, Map map, int now)
        {
            var at = g.sinkAt;
            int count = g.sinkCells;
            g.sinkDue = -1;
            g.sinkAt = IntVec3.Invalid;
            if (!at.IsValid || !at.InBounds(map)) return false;
            var clear = new Clearance(map);
            var cells = SinkCells(map, clear, at, count, true);
            if (cells.Count == 0) return false;
            var caught = map.mapPawns.FreeColonistsSpawned.Where(p => !p.Downed && cells.Contains(p.Position)).ToList();
            bool large = count >= 13;
            var deep = TerrainDefOf.WaterDeep;
            var shallow = TerrainDefOf.WaterShallow;
            var mud = TerrainDefOf.Mud;
            Shake(map, 0.9f);
            foreach (var c in cells)
            {
                ClearPlants(map, c);
                float d = c.DistanceTo(at);
                bool empty = c.GetFirstPawn(map) == null && c.GetFirstItem(map) == null;
                TerrainDef t = d < 0.5f && large && empty ? deep : d <= (large ? 1.5f : 1f) ? shallow : mud;
                if (t != null) map.terrainGrid.SetTerrain(c, t);
                Dust(map, c, 1.4f);
                if (t != mud) FleckMaker.WaterSplash(c.ToVector3Shifted(), map, 1.4f, 1.2f);
            }
            string place = StorytellerEvents.Direction(map, at);
            Told(map, GroundMarkKind.Sinkhole, "CC_Ground_SinkOpen".Translate(place.Named("PLACE")).Resolve(), new LookTargets(new TargetInfo(at, map)), at, place);
            foreach (var p in caught) Caught(p, HurtKind.Bruise, HurtWhere.Leg, 2f, 6f, GroundMath.SinkHurtChance, "CC_Ground_HurtSink", "CC_Ground_ClearSink");
            SomeSay(map, "Murmur", HandKind.Sign, "CC_Ground_Hand_Sinkhole", 0.4f);
            return true;
        }

        // ================= Told =================

        /// <summary>
        /// Someone of the colony caught where the ground moved: within the shared budget, one of the game's wounds there and
        /// a message; else they got clear, and say so.
        /// </summary>
        internal static void Caught(Pawn p, HurtKind kind, HurtWhere where, float low, float high, float chance, string hurtKey, string clearKey)
        {
            if (p?.Map == null) return;
            var map = p.Map;
            Dust(map, p.Position, 1.8f);
            if (Harm.Roll(p, HarmKinds.Ground, chance) && Harm.Injure(p, kind, where, low, high, HarmKinds.Ground) != null)
            {
                Tell.Message(NewsKind.Land, Importance.Important, map, hurtKey.Translate(p.Named("PAWN")).Resolve(), new LookTargets(p), MessageTypeDefOf.NegativeHealthEvent);
                return;
            }
            Tell.Message(NewsKind.Land, Importance.Notable, map, clearKey.Translate(p.Named("PAWN")).Resolve(), new LookTargets(p), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>A mark told: a message with the place to look at, a line in the chronicle, and a word from whoever of the colony is nearest.</summary>
        internal static void Told(Map map, GroundMarkKind kind, string text, LookTargets look, IntVec3 at, string place)
        {
            Tell.Message(NewsKind.Land, Importance.Notable, map, text, look, MessageTypeDefOf.NeutralEvent);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Note, ("CC_Ground_Chron_" + kind).Translate(place.Named("PLACE")).Resolve()));
            SayNear(map, at, "CC_Ground_Say_" + kind);
        }

        /// <summary>One line from the colonist nearest to <paramref name="at"/> who is up and about, within sight of it.</summary>
        internal static void SayNear(Map map, IntVec3 at, string key)
        {
            try
            {
                var p = map.mapPawns.FreeColonistsSpawned
                    .Where(x => !x.Downed && !x.Drafted && !x.InMentalState && x.Awake() && x.Position.InHorDistOf(at, 40f) && People.HasInnerLife(x))
                    .OrderBy(x => x.Position.DistanceToSquared(at)).FirstOrDefault();
                if (p != null) Speech.Say(p, TextVariants.Vary(key).Translate().Resolve());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the ground: a word", e);
            }
        }

        /// <summary>
        /// A believer of the colony may take it for a god's doing and say so (Gods/GodHands.cs: their words only, maybe wrong,
        /// and a scoffer's answer); as rarely as the hands are remarked on.
        /// </summary>
        internal static void SomeSay(Map map, string godKey, HandKind kind, string labelKey, float chance)
        {
            try
            {
                if (!Gods.On) return;
                var god = Gods.ByKey(godKey);
                var lore = GodLore.Instance;
                if (god == null || lore == null) return;
                int now = Now;
                int lastGod = lore.lastHand.TryGetValue(godKey, out int lg) ? lg : -1;
                int lastAny = lore.lastHandAt.TryGetValue(Homes.KeyOf(map), out int la) ? la : -1;
                if (!GodHandsMath.Remarked(lastGod, lastAny, now, chance, Rand.Value, GenDate.TicksPerDay)) return;
                GodHands.Show(map, god, kind, labelKey.Translate().Resolve(), now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the ground: some say", e);
            }
        }

        // ================= For the dev tools =================

        /// <summary>Leaves a mark of a kind now, as a quake would (dev mode). What happened, in words.</summary>
        internal static string DevMark(Map map, GroundMarkKind kind)
        {
            var g = GroundMap.Of(map);
            if (g == null) return "no ground record on this map.";
            var clear = new Clearance(map);
            bool done;
            switch (kind)
            {
                case GroundMarkKind.Cave:
                case GroundMarkKind.Scree:
                    if (!TryFace(map, clear, IntVec3.Invalid, out var rock, out var open)) return "no rock face out on the land with rock behind it, clear of the colony.";
                    done = kind == GroundMarkKind.Cave ? Cave(map, clear, rock, open, QuakeSource.Quake) : Scree(map, clear, rock, open, QuakeSource.Quake);
                    break;
                case GroundMarkKind.Fissure:
                case GroundMarkKind.Spring:
                    if (!TryOpen(map, clear, IntVec3.Invalid, null, out var spot)) return "no open ground clear of the colony.";
                    done = kind == GroundMarkKind.Fissure ? Fissure(map, clear, spot, QuakeSource.Quake) : Spring(map, clear, spot, QuakeSource.Quake);
                    break;
                case GroundMarkKind.Landslide:
                    done = Landslide(map);
                    break;
                case GroundMarkKind.Sinkhole:
                    done = SinkholeSign(g, map, Now);
                    if (done) g.sinkDue = Now + HourTicks;
                    break;
                default:
                    return "nothing.";
            }
            if (done) g.lastMark = Now;
            return done ? kind + " made" + (kind == GroundMarkKind.Sinkhole ? " (the sign now; it opens within the hour)." : ".") : "no room for it here this time (try again).";
        }

        /// <summary>A quake noted now, as if one had just shaken the map (dev mode): its mark is rolled within the hour, past the gap.</summary>
        internal static string DevQuake(Map map, QuakeSource source)
        {
            var g = GroundMap.Of(map);
            if (g == null) return "no ground record on this map.";
            g.lastMark = -1;
            g.quakeNoted = -1;
            Note(g, source, IntVec3.Invalid, Now);
            return source + " noted; its mark is rolled at the next hour (" + (GroundMath.MarkChance(source) * 100f).ToString("0") + "%).";
        }
    }
}
