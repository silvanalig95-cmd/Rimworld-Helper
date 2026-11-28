using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// B1, overgrazed land thins (docs/design/living-land-land.md). Once a day the colony's grazing animals are counted by
    /// block of the map (16 cells each way); in the few most crowded, a sample of the ground shows whether the grass keeps
    /// up (LandMath.Overgrazed: about 12 cells of grass to a grazer the size of a deer, and the grass covering under a third
    /// of the good ground). A block overgrazed for six days wears: a few bare cells a day turn to packed dirt (rich soil to
    /// soil, marshy ground to mud), up to a quarter of its good ground. Rested twenty growing days with no crowding, the worn
    /// ground comes back, a few cells a day, as it was. Only the colony's own animals; nothing on a floor, in a zone or under
    /// a building. The news says when a pasture wears and when it has come back.
    /// </summary>
    public static class LandPasture
    {
        public static bool On => (ColonyCouncilMod.Settings?.landRecovers ?? true);

        private static bool Grazer(Pawn a) => a != null && a.Spawned && !a.Dead && a.RaceProps?.Animal == true && a.RaceProps.Eats(FoodTypeFlags.Plant) && !a.Position.Roofed(a.Map);

        internal static void Pass(Map map, LandBook b)
        {
            bool growing = LandBook.GrowingToday(map);
            int width = map.Size.x;
            var crowd = new Dictionary<int, (int n, float body)>();
            if (On)
                foreach (var a in map.mapPawns.SpawnedColonyAnimals)
                {
                    if (!Grazer(a)) continue;
                    int block = LandMath.BlockOf(a.Position.x, a.Position.z, width);
                    crowd.TryGetValue(block, out var v);
                    crowd[block] = (v.n + 1, v.body + a.BodySize);
                }
            var busy = crowd.Where(kv => kv.Value.n >= LandMath.MinGrazers || kv.Value.body >= LandMath.MinBody).OrderByDescending(kv => kv.Value.body).Take(LandMath.GrazeBlocks).ToList();
            var looked = new HashSet<int>();
            foreach (var kv in busy)
            {
                looked.Add(kv.Key);
                Block(map, b, kv.Key, kv.Value.body, growing);
            }
            // The blocks not crowded today rest; rested long enough, the worn ground comes back.
            foreach (var g in b.Grazing.ToList())
            {
                if (looked.Contains(g.block)) continue;
                LandMath.Day(g, false, growing);
                if (!LandMath.Rested(g) && On) continue;
                int back = Restore(map, b, g.block, LandMath.PastureBackPerPass);
                if (b.Worn.Any(w => w.block == g.block)) continue;
                if (back > 0 && g.told)
                {
                    var at = Center(map, g.block);
                    Tell.Minor(NewsKind.Land, Importance.Flavor, map, "CC_Land_PastureBack".Translate(StorytellerEvents.Direction(map, at).Named("PLACE")).Resolve(), new LookTargets(at, map),
                        MessageTypeDefOf.PositiveEvent);
                }
                if (g.days == 0) b.Grazing.Remove(g);
            }
        }

        private static IntVec3 Center(Map map, int block)
        {
            int per = (map.Size.x + LandMath.Block - 1) / LandMath.Block;
            return new IntVec3(block % per * LandMath.Block + LandMath.Block / 2, 0, block / per * LandMath.Block + LandMath.Block / 2);
        }

        /// <summary>A crowded block's day: the ground sampled, the day counted, and once overgrazed long enough a few bare cells worn.</summary>
        private static void Block(Map map, LandBook b, int block, float body, bool growing)
        {
            int per = (map.Size.x + LandMath.Block - 1) / LandMath.Block;
            int x0 = block % per * LandMath.Block, z0 = block / per * LandMath.Block;
            int side = (int)Math.Sqrt(LandMath.GrazeSamples);
            int fertile = 0, covered = 0, samples = 0;
            var bare = new List<IntVec3>();
            for (int i = 0; i < side; i++)
                for (int j = 0; j < side; j++)
                {
                    var c = new IntVec3(x0 + (i * LandMath.Block + LandMath.Block / 2) / side, 0, z0 + (j * LandMath.Block + LandMath.Block / 2) / side);
                    if (!c.InBounds(map)) continue;
                    samples++;
                    var t = c.GetTerrain(map);
                    if (t == null || !t.natural || t.IsWater || t.fertility < 0.5f) continue;
                    fertile++;
                    var plant = c.GetPlant(map);
                    if (plant != null)
                    {
                        if (plant.def.ingestible != null && plant.Growth >= 0.25f) covered++;
                        continue;
                    }
                    if (LandMath.WornTo(t.defName) != null && Open(map, c)) bare.Add(c);
                }
            bool over = LandMath.Overgrazed(body, fertile, covered, samples);
            var g = b.GrazingOf(block, over);
            if (g == null) return;
            LandMath.Day(g, over, growing);
            if (!LandMath.Wears(g) || !growing) return;
            int cap = LandMath.WearCap(fertile, samples) - b.Worn.Count(w => w.block == block);
            int made = 0;
            foreach (var c in bare.InRandomOrder())
            {
                if (made >= Math.Min(LandMath.WearPerPass, cap) || b.Worn.Count >= LandMath.MaxWorn) break;
                string was = c.GetTerrain(map).defName;
                var to = DefDatabase<TerrainDef>.GetNamedSilentFail(LandMath.WornTo(was));
                if (to == null) continue;
                map.terrainGrid.SetTerrain(c, to);
                b.Worn.Add(new LandWornCell { cell = map.cellIndices.CellToIndex(c), was = was, block = block });
                made++;
            }
            if (made > 0 && !g.told)
            {
                g.told = true;
                Tell.Minor(NewsKind.Land, Importance.Notable, map, "CC_Land_PastureWorn".Translate(StorytellerEvents.Direction(map, bare[0]).Named("PLACE")).Resolve(),
                    new LookTargets(bare[0], map), MessageTypeDefOf.NeutralEvent);
            }
        }

        /// <summary>Open ground the grazing may wear: no zone, no floor or bridge, nothing on it but filth.</summary>
        private static bool Open(Map map, IntVec3 c)
        {
            if (c.GetZone(map) != null || map.terrainGrid.FoundationAt(c) != null || map.terrainGrid.TempTerrainAt(c) != null) return false;
            var things = c.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
                if (things[i].def.category != ThingCategory.Filth && things[i].def.category != ThingCategory.Pawn)
                    return false;
            return true;
        }

        /// <summary>Worn cells of a block put back as they were, a few a day; ground the colony has since made its own is left (and forgotten).</summary>
        private static int Restore(Map map, LandBook b, int block, int n)
        {
            int done = 0;
            foreach (var w in b.Worn.Where(x => x.block == block).ToList())
            {
                if (done >= n) break;
                var c = map.cellIndices.IndexToCell(w.cell);
                var t = c.InBounds(map) ? c.GetTerrain(map) : null;
                var was = DefDatabase<TerrainDef>.GetNamedSilentFail(w.was);
                b.Worn.Remove(w);
                if (t == null || was == null || t.defName != LandMath.WornTo(w.was) || c.GetZone(map) != null || c.GetEdifice(map) != null || map.terrainGrid.FoundationAt(c) != null) continue;
                map.terrainGrid.SetTerrain(c, was);
                done++;
            }
            return done;
        }

        /// <summary>Dev: the busiest block here counted as overgrazed long enough, and worn now.</summary>
        internal static string DevWear(Map map)
        {
            var b = LandBook.Of(map);
            if (b == null) return "no land record here.";
            var a = map.mapPawns.SpawnedColonyAnimals.Where(Grazer).GroupBy(x => LandMath.BlockOf(x.Position.x, x.Position.z, map.Size.x)).OrderByDescending(g => g.Count()).FirstOrDefault();
            if (a == null) return "none of the colony's grazing animals is out on the land.";
            var g = b.GrazingOf(a.Key, true);
            if (g == null) return "no room in the record for another block.";
            g.days = Math.Max(g.days, LandMath.WornAfter);
            Block(map, b, a.Key, 99f, true);
            return b.Describe().Split('\n')[3];
        }

        /// <summary>Dev: every block here rested long enough, and its worn ground put back now.</summary>
        internal static string DevRest(Map map)
        {
            var b = LandBook.Of(map);
            if (b == null) return "no land record here.";
            int n = 0;
            foreach (var g in b.Grazing.ToList())
            {
                g.days = 0;
                g.rest = LandMath.RestDays;
                n += Restore(map, b, g.block, LandMath.MaxWorn);
            }
            return n + " cell(s) put back. " + b.Describe().Split('\n')[3];
        }
    }
}
