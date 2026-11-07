using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A town of one of Rimfolk's peoples, when the game makes its map (an attack, a visit by caravan, a quest there): its
    /// people's plan (TownPlanDef: the league's walled waystation, the order's sickhouse round its herb garden, the herders'
    /// camp of round houses and pens) placed the way a town the colony was is placed (Colonies/TownLayout.cs, the same
    /// prefix on the game's settlement step, which stands down for that map), where the land suits it: near the map's
    /// middle, with little water, marsh or rough rock under it. Then its stores (the game's own loot and the people's own
    /// goods), its herd in the pens, its garden planted, and its people at home under the game's defend-the-town lord.
    /// Without the plan, where the land is wrong for it, or with the deeper game hooks off, the game makes its own base.
    /// </summary>
    public static class PeopleTowns
    {
        /// <summary>The game's own loot for a town, in silver (SymbolResolver_Settlement).</summary>
        private const float LootValue = 1800f;

        /// <summary>The town's map from its people's plan; false: not such a map, or the land won't take it (the game builds as always).</summary>
        public static bool PlaceTown(Map map)
        {
            if (!(map?.Parent is Settlement s) || s.Faction == null || s.Faction.IsPlayer) return false;
            var ext = Peoples.Of(s.Faction);
            var plan = ext?.plan;
            if (plan == null || plan.Width == 0 || plan.Height == 0 || plan.Width > map.Size.x - 12 || plan.Height > map.Size.z - 12) return false;
            var keys = plan.Words(plan.wallStuff?.FirstOrDefault(d => d != null));
            if (!Spot(map, plan, out int x, out int z)) return false;
            var marks = new Dictionary<string, List<int>>();
            var grid = PeoplesMath.Grid(plan.rows, keys, x, z, map.Size.x, map.Size.z, marks);
            if (grid == null) return false;
            var layout = new LedgerLayout { grid = grid };
            var rect = TownLayouts.Place(map, layout, s.Faction, false);
            if (rect == null) return false;
            TownLayouts.NotePlaced(map);
            try
            {
                MapGenerator.SetVar("SettlementRect", rect.Value);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples: the town's ground", e);
            }
            try
            {
                Stores(map, grid, Cells(map, grid, marks, "store"), rect.Value, ext, s.Faction);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples: the town's stores", e);
            }
            try
            {
                Herd(map, Cells(map, grid, marks, "pen"), ext, s.Faction);
                foreach (var kv in marks.Where(m => m.Key.StartsWith("plant:", StringComparison.Ordinal)))
                    Garden(map, Cells(map, grid, marks, kv.Key), kv.Key.Substring("plant:".Length));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples: the town's herd and garden", e);
            }
            try
            {
                TownLayouts.AtHome(map, s.ID, null, rect.Value, s.Faction);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples: the town's people at home", e);
            }
            if (Prefs.DevMode) Log.Message("[Rimfolk] " + s.Name + " of " + s.Faction.Name + " made from the plan " + plan.defName + " at " + rect.Value + ".");
            return true;
        }

        /// <summary>A cell the plan can't stand on: water, ground too soft for a floor, or ground nothing passes.</summary>
        private static bool Bad(IntVec3 c, Map map)
        {
            if (!c.InBounds(map)) return true;
            var t = c.GetTerrain(map);
            return t == null || t.IsWater || t.passability == Traversability.Impassable || !t.affordances.NullOrEmpty() && !t.affordances.Contains(TerrainAffordanceDefOf.Light);
        }

        /// <summary>
        /// Where the plan's corner goes: the map's middle, else the nearest of a few places round it, where at most a
        /// small share of the cells the plan uses is bad ground (PeoplesMath.LandFits). The land's rock is cleared where
        /// the plan stands, as mining would.
        /// </summary>
        private static bool Spot(Map map, TownPlanDef plan, out int x, out int z)
        {
            int w = plan.Width, h = plan.Height;
            var used = new List<(int, int)>();
            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                    if (!PeoplesMath.IsGround(plan.rows[r][c]))
                        used.Add((c, PeoplesMath.RowZ(r, h)));
            var centre = map.Center;
            for (int ring = 0; ring <= 4; ring++)
                for (int dx = -ring; dx <= ring; dx++)
                    for (int dz = -ring; dz <= ring; dz++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != ring) continue;
                        PeoplesMath.Corner(centre.x + dx * 6, centre.z + dz * 6, w, h, map.Size.x, map.Size.z, out x, out z);
                        if (x < 6 || z < 6 || x + w > map.Size.x - 6 || z + h > map.Size.z - 6) continue;
                        int bad = 0;
                        foreach (var (lx, lz) in used)
                            if (Bad(new IntVec3(x + lx, 0, z + lz), map))
                                bad++;
                        if (PeoplesMath.LandFits(bad, used.Count)) return true;
                    }
            x = z = 0;
            return false;
        }

        private static List<IntVec3> Cells(Map map, LayoutGrid g, Dictionary<string, List<int>> marks, string mark) =>
            marks.TryGetValue(mark, out var list) ? list.Select(i => TownLayouts.CellOf(map, g, i)).Where(c => c.InBounds(map)).ToList() : new List<IntVec3>();

        /// <summary>
        /// The game's own loot for a town (its stockpile maker, at the value it gives a town), and the people's own goods
        /// (the order's medicine, the herders' hay and wool, the league's cloth and silver), laid in the storehouse.
        /// </summary>
        private static void Stores(Map map, LayoutGrid g, List<IntVec3> store, CellRect rect, PeopleExtension ext, Faction f)
        {
            var things = new List<Thing>();
            var maker = ThingSetMakerDefOf.MapGen_DefaultStockpile;
            if (maker?.root != null)
            {
                var parms = new ThingSetMakerParams
                {
                    countRange = new IntRange(3, 10), techLevel = f.def.techLevel, makingFaction = f, totalMarketValueRange = new FloatRange(LootValue, LootValue)
                };
                things.AddRange(maker.root.Generate(parms));
            }
            foreach (var goods in ext.townGoods ?? new List<ThingDefCountRangeClass>())
            {
                if (goods?.thingDef == null) continue;
                int left = goods.countRange.RandomInRange;
                while (left > 0)
                {
                    var t = ThingMaker.MakeThing(goods.thingDef, GenStuff.DefaultStuffFor(goods.thingDef));
                    t.stackCount = Math.Min(left, Math.Max(1, goods.thingDef.stackLimit));
                    left -= t.stackCount;
                    things.Add(t);
                }
            }
            if (things.Count == 0) return;
            if (store.Count == 0)
            {
                MapGenUtility.GenerateLoot(map, rect, things);
                return;
            }
            int i = 0;
            foreach (var t in things)
            {
                var at = store[i++ % store.Count];
                if (!GenPlace.TryPlaceThing(t, at, map, ThingPlaceMode.Near) && !t.Destroyed) t.Destroy();
            }
        }

        /// <summary>The town's herd in its pens: beasts of the people's own kinds that suit the land's weather, as many as the pens hold with room.</summary>
        private static void Herd(Map map, List<IntVec3> pen, PeopleExtension ext, Faction f)
        {
            if (pen.Count == 0 || ext.herd.NullOrEmpty()) return;
            var kinds = ext.herd.Where(k => k?.race != null && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race)).ToList();
            if (kinds.Count == 0) kinds = ext.herd.Where(k => k?.race != null).ToList();
            int n = PeoplesMath.HerdCount(pen.Count, ext.herdCount.min, ext.herdCount.max, Rand.Value);
            for (int i = 0; i < n; i++)
            {
                var kind = kinds.RandomElement();
                var beast = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, f, PawnGenerationContext.NonPlayer));
                var cell = pen.Where(c => c.Standable(map) && c.GetFirstPawn(map) == null).RandomElementWithFallback(pen.RandomElement());
                GenSpawn.Spawn(beast, cell, map);
            }
        }

        /// <summary>The garden's beds sown with their plant, well grown, where the ground will bear it.</summary>
        private static void Garden(Map map, List<IntVec3> beds, string plantDef)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(plantDef);
            if (def?.plant == null) return;
            foreach (var c in beds)
            {
                if (!c.InBounds(map) || c.GetPlant(map) != null || c.GetEdifice(map) != null || c.GetTerrain(map).fertility < def.plant.fertilityMin) continue;
                if (GenSpawn.Spawn(def, c, map) is Plant p) p.Growth = Rand.Range(0.55f, 1f);
            }
        }
    }
}
