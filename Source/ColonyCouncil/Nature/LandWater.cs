using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// B5, water that rises and falls (docs/design/living-land-land.md). Only still water at its edge: shallow water and
    /// marsh, and the open ground beside them. Rivers, the sea, hot springs and any water joined to them are never touched
    /// (they are Living Waters and Waves' to make move), nor deep water. Once a day, by how wet the season has been
    /// (LandWet.SeasonWet, from the weather record):
    /// <list type="bullet">
    /// <item>after four days of a dry spell the water draws back: edge cells of shallow water and marsh turn to mud, and as
    /// the mud moves in, the outer mud to soil, up to three cells in from the old edge;</item>
    /// <item>after four days of a wet spell it swells: open ground beside it turns to mud, the ring next to the water then to
    /// marsh, up to two cells out; never into the home area, a zone, a floor or within two cells of a building;</item>
    /// <item>what the other spell left goes back first, a step at a time, to the map's own edge (slowly while the season is
    /// as usual, and all of it when the setting is off).</item>
    /// </list>
    /// The fish follow (Odyssey's fish stocks, which Vanilla Fishing Expanded's fishing uses): a pond the drought shrank holds
    /// no more fish than it can, and one the rain filled gains a few. The record keeps every cell changed and what it was, so
    /// the map's own water always comes back. The news says when the water draws back, swells and is back at its edge.
    /// </summary>
    public static class LandWater
    {
        public static bool On => (ColonyCouncilMod.Settings?.landWater ?? true);

        /// <summary>Edge cells known at most a map.</summary>
        public const int MaxShore = 600;

        private static readonly Dictionary<WaterBody, IntVec3[]> bodyCells = new Dictionary<WaterBody, IntVec3[]>();
        private static readonly HashSet<WaterBody> bodyOff = new HashSet<WaterBody>();

        /// <summary>A new game or a load: the water bodies looked at again.</summary>
        public static void Forget()
        {
            bodyCells.Clear();
            bodyOff.Clear();
        }

        private static string Name(Map map, IntVec3 c) => map.terrainGrid.TerrainAt(c)?.defName ?? "";

        private static readonly IntVec3[] Four = { IntVec3.North, IntVec3.East, IntVec3.South, IntVec3.West };

        internal static void Pass(Map map, LandBook b)
        {
            var state = LandMath.State(LandWet.SeasonWet(map));
            LandMath.Spell(state, ref b.dryRun, ref b.wetRun);
            if (b.dryRun == 0) b.toldDry = false;
            if (b.wetRun == 0) b.toldWet = false;
            bool dry = On && b.dryRun >= LandMath.SpellDays, wet = On && b.wetRun >= LandMath.SpellDays;
            var touched = new List<IntVec3>();
            // What the other spell left goes back first: all of it quickly with the setting off.
            int back = !On ? LandMath.RestorePerPass : LandMath.PerPass(dry || wet ? LandWetness.Dry : LandWetness.Usual, true);
            int driedBack = 0;
            if (!dry) driedBack = Restore(map, b, true, back, touched);
            if (!wet) Restore(map, b, false, back, touched);
            if (!On) return;
            if (dry || wet) Find(map, b);
            int before = touched.Count;
            int moved = dry ? Dry(map, b, LandMath.DryPerPass, touched) : wet ? Swell(map, b, LandMath.SwellPerPass, touched) : 0;
            if (touched.Count > 0) Fish(map, touched, dry);
            if (moved > 0 && dry && !b.toldDry)
            {
                b.toldDry = true;
                b.toldBack = false;
                var at = touched[before];
                Tell.Minor(NewsKind.Land, Importance.Notable, map, "CC_Land_WaterDrops".Translate(StorytellerEvents.Direction(map, at).Named("PLACE")).Resolve(),
                    new LookTargets(at, map), MessageTypeDefOf.NeutralEvent);
            }
            else if (moved > 0 && wet && !b.toldWet)
            {
                b.toldWet = true;
                var at = touched[before];
                Tell.Minor(NewsKind.Land, Importance.Notable, map, "CC_Land_WaterRises".Translate(StorytellerEvents.Direction(map, at).Named("PLACE")).Resolve(),
                    new LookTargets(at, map), MessageTypeDefOf.NeutralEvent);
            }
            if (driedBack > 0 && !b.toldBack && !b.Water.Any(w => w.dried))
            {
                b.toldBack = true;
                Tell.Minor(NewsKind.Land, Importance.Flavor, map, "CC_Land_WaterBack".Translate(StorytellerEvents.Direction(map, touched[0]).Named("PLACE")).Resolve(),
                    new LookTargets(touched[0], map), MessageTypeDefOf.PositiveEvent);
            }
        }

        // ================= The edge =================

        /// <summary>Shallow still water with open ground beside it (a 4-way neighbour that isn't water).</summary>
        private static bool Edge(Map map, IntVec3 c)
        {
            if (!c.InBounds(map) || !LandMath.StillShallow(Name(map, c))) return false;
            foreach (var d in Four)
            {
                var x = c + d;
                if (x.InBounds(map) && !(map.terrainGrid.TerrainAt(x)?.IsWater ?? true)) return true;
            }
            return false;
        }

        private static void Know(Map map, LandBook b, IntVec3 c)
        {
            int i = map.cellIndices.CellToIndex(c);
            if (b.shore.Contains(i) || !Edge(map, c)) return;
            if (b.shoreList.Count >= MaxShore)
            {
                b.shore.Remove(b.shoreList[0]);
                b.shoreList.RemoveAt(0);
            }
            b.shore.Add(i);
            b.shoreList.Add(i);
        }

        /// <summary>
        /// The still water's edge found a little more: the next cells of one of Odyssey's water bodies in turn (freshwater
        /// only, and none with a river, the sea or a hot spring in it), or cells looked at here and there without it.
        /// </summary>
        private static void Find(Map map, LandBook b)
        {
            var bodies = map.waterBodyTracker?.Bodies;
            if (bodies == null || bodies.Count == 0)
            {
                for (int i = 0; i < LandMath.WaterFind; i++) Know(map, b, CellFinder.RandomCell(map));
                return;
            }
            var body = bodies[b.bodyAt % bodies.Count];
            var cells = CellsOf(map, body);
            if (cells == null)
            {
                b.bodyAt++;
                b.bodyCellAt = 0;
                return;
            }
            for (int i = 0; i < LandMath.WaterFind; i++)
            {
                if (b.bodyCellAt >= cells.Length)
                {
                    b.bodyAt++;
                    b.bodyCellAt = 0;
                    return;
                }
                Know(map, b, cells[b.bodyCellAt++]);
            }
        }

        /// <summary>A water body's cells, or null for one the water never moves (not fresh, or joined to a river, the sea or a hot spring).</summary>
        private static IntVec3[] CellsOf(Map map, WaterBody body)
        {
            if (body == null || bodyOff.Contains(body)) return null;
            if (bodyCells.TryGetValue(body, out var cells)) return cells;
            if (body.waterBodyType != WaterBodyType.Freshwater || body.cells == null)
            {
                bodyOff.Add(body);
                return null;
            }
            cells = body.cells.ToArray();
            foreach (var c in cells)
            {
                var t = c.InBounds(map) ? map.terrainGrid.TerrainAt(c) : null;
                if (t == null || t.IsRiver || t.IsOcean || t.waterBodyType != WaterBodyType.Freshwater)
                {
                    bodyOff.Add(body);
                    return null;
                }
            }
            bodyCells[body] = cells;
            return cells;
        }

        /// <summary>Running water, the sea or other water not still (a hot spring) within two cells.</summary>
        private static bool NearRiver(Map map, IntVec3 c)
        {
            for (int dx = -2; dx <= 2; dx++)
                for (int dz = -2; dz <= 2; dz++)
                {
                    var x = new IntVec3(c.x + dx, 0, c.z + dz);
                    if (!x.InBounds(map)) continue;
                    var t = map.terrainGrid.TerrainAt(x);
                    if (t != null && t.IsWater && (t.IsRiver || t.IsOcean || t.waterBodyType != WaterBodyType.Freshwater)) return true;
                }
            return false;
        }

        // ================= A cell the water may change =================

        /// <summary>
        /// The water may change this cell: natural ground or water, nothing frozen over it, no bridge, no roof, no zone, nothing
        /// on it but plants and filth; swelling also never into the home area nor within two cells of a building.
        /// </summary>
        private static bool Free(Map map, IntVec3 c, bool swelling)
        {
            if (!c.InBounds(map) || c.Roofed(map) || c.GetZone(map) != null) return false;
            var grid = map.terrainGrid;
            if (grid.TempTerrainAt(c) != null || grid.FoundationAt(c) != null) return false;
            var t = grid.TerrainAt(c);
            if (t == null || !t.natural) return false;
            var things = c.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                var cat = things[i].def.category;
                if (cat != ThingCategory.Plant && cat != ThingCategory.Filth) return false;
            }
            if (!swelling) return true;
            if (map.areaManager.Home[c]) return false;
            for (int dx = -2; dx <= 2; dx++)
                for (int dz = -2; dz <= 2; dz++)
                {
                    var x = new IntVec3(c.x + dx, 0, c.z + dz);
                    if (x.InBounds(map) && x.GetEdifice(map) != null) return false;
                }
            return true;
        }

        /// <summary>The colony has made this cell its own since (built on it, floored it, zoned it): the water leaves it be.</summary>
        private static bool Taken(Map map, IntVec3 c) =>
            c.GetEdifice(map) != null || c.GetZone(map) != null || map.terrainGrid.FoundationAt(c) != null || !(map.terrainGrid.TerrainAt(c)?.natural ?? false);

        /// <summary>The cell set to a terrain by name; becoming water, the grass and brush on it drown (a tree there and it stays as it is).</summary>
        private static bool Set(Map map, IntVec3 c, string name)
        {
            var def = DefDatabase<TerrainDef>.GetNamedSilentFail(name);
            if (def == null) return false;
            if (def.IsWater || def.fertility <= 0f)
            {
                var plant = c.GetPlant(map);
                if (plant != null && plant.def.plant.IsTree) return false;
                if (def.IsWater) plant?.Destroy(DestroyMode.Vanish);
            }
            map.terrainGrid.SetTerrain(c, def);
            return true;
        }

        // ================= Back, dry, swell =================

        /// <summary>Cells the drought (<paramref name="dried"/>) or the wet spell left, a step back toward what they were, the furthest ring first; how many moved.</summary>
        private static int Restore(Map map, LandBook b, bool dried, int n, List<IntVec3> touched)
        {
            int done = 0;
            foreach (var w in b.Water.Where(x => x.dried == dried).OrderByDescending(x => x.ring).ToList())
            {
                if (done >= n) break;
                var c = map.cellIndices.IndexToCell(w.cell);
                if (!c.InBounds(map))
                {
                    b.Water.Remove(w);
                    continue;
                }
                string now = Name(map, c);
                bool ours = now == "Mud" || (dried ? now == "Soil" : now == "Marsh");
                if (!ours || Taken(map, c))
                {
                    // Back as it was, changed since by someone else, or the colony's ground now: left as it is.
                    b.Water.Remove(w);
                    continue;
                }
                string next = LandMath.RestoreNext(now, w.was);
                if (next == null)
                {
                    b.Water.Remove(w);
                    continue;
                }
                if (!Free(map, c, false) || !Set(map, c, next)) continue;
                done++;
                touched.Add(c);
                if (next == w.was)
                {
                    b.Water.Remove(w);
                    if (dried) Know(map, b, c);
                }
            }
            return done;
        }

        /// <summary>The nearest ring among a cell's 4-way neighbours of the kind asked (land for drying, still water for swelling); -1 when none.</summary>
        private static int Nearest(Map map, LandBook b, IntVec3 c, bool land)
        {
            int best = -1;
            foreach (var d in Four)
            {
                var x = c + d;
                if (!x.InBounds(map)) continue;
                var t = map.terrainGrid.TerrainAt(x);
                if (t == null) continue;
                var rec = b.WaterAt(map.cellIndices.CellToIndex(x));
                int ring;
                if (land)
                {
                    if (t.IsWater) continue;
                    ring = rec != null && rec.dried ? rec.ring : 0;
                }
                else
                {
                    if (rec != null && !rec.dried) ring = t.defName == "Marsh" ? rec.ring : -1;
                    else ring = LandMath.StillShallow(t.defName) || t.defName == "WaterDeep" ? 0 : -1;
                    if (ring < 0) continue;
                }
                if (best < 0 || ring < best) best = ring;
            }
            return best;
        }

        /// <summary>A dry spell's day: the mud moves in (the outer mud to soil), and edge water and marsh turn to mud, up to three rings in.</summary>
        private static int Dry(Map map, LandBook b, int n, List<IntVec3> touched)
        {
            int done = 0;
            int deepest = b.Water.Where(w => w.dried).Select(w => w.ring).DefaultIfEmpty(0).Max();
            foreach (var w in b.Water.Where(w => w.dried && (w.ring < deepest || deepest >= LandMath.MaxShrink)).OrderBy(w => w.ring).ToList())
            {
                if (done >= n / 2) break;
                var c = map.cellIndices.IndexToCell(w.cell);
                if (Name(map, c) != "Mud" || !Free(map, c, false)) continue;
                if (Set(map, c, LandMath.DryNext("Mud")))
                {
                    done++;
                    touched.Add(c);
                }
            }
            int looked = 0;
            while (done < n && looked < LandMath.WaterWork && b.shoreList.Count > 0 && b.Water.Count < LandMath.MaxWaterCells)
            {
                looked++;
                b.shoreAt %= b.shoreList.Count;
                int i = b.shoreList[b.shoreAt];
                var c = map.cellIndices.IndexToCell(i);
                if (!Edge(map, c))
                {
                    b.shore.Remove(i);
                    b.shoreList.RemoveAt(b.shoreAt);
                    continue;
                }
                b.shoreAt++;
                if (b.WaterAt(i) != null) continue;
                int ring = LandMath.RingFrom(Nearest(map, b, c, true));
                if (!LandMath.CanDry(ring) || !Free(map, c, false) || NearRiver(map, c)) continue;
                string was = Name(map, c);
                if (!Set(map, c, LandMath.DryNext(was))) continue;
                b.Water.Add(new LandWaterCell { cell = i, was = was, ring = ring, dried = true });
                done++;
                touched.Add(c);
                foreach (var d in Four) Know(map, b, c + d);
            }
            return done;
        }

        /// <summary>A wet spell's day: the ring next to the water from mud to marsh, and open ground beside the water to mud, up to two rings out.</summary>
        private static int Swell(Map map, LandBook b, int n, List<IntVec3> touched)
        {
            int done = 0;
            if (b.wetRun >= LandMath.SpellDays * 2)
                foreach (var w in b.Water.Where(w => !w.dried && w.ring == 1).ToList())
                {
                    if (done >= n / 2) break;
                    var c = map.cellIndices.IndexToCell(w.cell);
                    string next = LandMath.SwellNext(Name(map, c), w.ring);
                    if (next == null || !Free(map, c, true) || !Set(map, c, next)) continue;
                    done++;
                    touched.Add(c);
                    // The new marsh is the water's edge now: the next ring out is looked for from it.
                    Know(map, b, c);
                }
            int looked = 0;
            for (int k = 0; k < b.shoreList.Count && done < n && looked < LandMath.WaterWork && b.Water.Count < LandMath.MaxWaterCells; k++)
            {
                b.shoreAt %= b.shoreList.Count;
                var from = map.cellIndices.IndexToCell(b.shoreList[b.shoreAt++]);
                foreach (var d in Four)
                {
                    looked++;
                    var c = from + d;
                    if (!c.InBounds(map) || done >= n) continue;
                    int i = map.cellIndices.CellToIndex(c);
                    string was = Name(map, c);
                    if (b.WaterAt(i) != null || !LandMath.SwellGround(was)) continue;
                    int ring = LandMath.RingFrom(Nearest(map, b, c, false));
                    if (!LandMath.CanSwell(ring) || !Free(map, c, true) || NearRiver(map, c)) continue;
                    if (!Set(map, c, LandMath.SwellNext(was, ring))) continue;
                    b.Water.Add(new LandWaterCell { cell = i, was = was, ring = ring, dried = false });
                    done++;
                    touched.Add(c);
                }
            }
            return done;
        }

        // ================= The fish =================

        /// <summary>The ponds the water changed: a shrunk one holds no more fish than it can now, a swollen one gains a few (Odyssey's fish stocks).</summary>
        private static void Fish(Map map, List<IntVec3> touched, bool dry)
        {
            var tracker = map.waterBodyTracker;
            if (tracker == null) return;
            var seen = new HashSet<WaterBody>();
            foreach (var c in touched)
                foreach (var x in Four.Select(d => c + d).Concat(new[] { c }))
                {
                    if (!x.InBounds(map) || !tracker.TryGetWaterBodyAt(x, out var body) || body == null || !seen.Add(body)) continue;
                    try
                    {
                        float max = body.MaxPopulation;
                        if (max <= 0f) continue;
                        body.Population = dry ? Math.Min(body.Population, max) : LandMath.FishAfterSwell(body.Population, max);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("the land: the fish", e);
                    }
                }
        }

        // ================= Dev =================

        /// <summary>Dev: as if the last days had been a dry (or wet) spell long enough, and the day's water pass now.</summary>
        internal static string DevSpell(Map map, bool dry)
        {
            var b = LandBook.Of(map);
            if (b == null) return "no land record here.";
            for (int i = 0; i < 20 && b.shoreList.Count < 40; i++) Find(map, b);
            var touched = new List<IntVec3>();
            if (dry)
            {
                b.wetRun = 0;
                b.dryRun = Math.Max(b.dryRun, LandMath.SpellDays * 2);
                Restore(map, b, false, LandMath.RestorePerPass, touched);
                Dry(map, b, LandMath.DryPerPass, touched);
            }
            else
            {
                b.dryRun = 0;
                b.wetRun = Math.Max(b.wetRun, LandMath.SpellDays * 2);
                Restore(map, b, true, LandMath.RestorePerPass, touched);
                Swell(map, b, LandMath.SwellPerPass, touched);
            }
            if (touched.Count > 0) Fish(map, touched, dry);
            return touched.Count + " cell(s) changed. " + b.Describe().Split('\n')[2];
        }

        /// <summary>Dev: every cell the water left or came over put back as it was, at once.</summary>
        internal static string DevRestoreAll(Map map)
        {
            var b = LandBook.Of(map);
            if (b == null) return "no land record here.";
            int n = 0;
            foreach (var w in b.Water.ToList())
            {
                var c = map.cellIndices.IndexToCell(w.cell);
                if (c.InBounds(map) && !Taken(map, c) && Set(map, c, w.was)) n++;
                b.Water.Remove(w);
            }
            b.dryRun = b.wetRun = 0;
            return n + " cell(s) put back.";
        }
    }
}
