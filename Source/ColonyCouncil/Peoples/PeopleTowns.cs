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
    /// camp of round houses and pens, the pilgrims' shrine and hostel, the scholars' archive, the enclave's landing-pad
    /// market, the river folk's landing) placed the way a town the colony was is placed (Colonies/TownLayout.cs, the same
    /// prefix on the game's settlement step, which stands down for that map), where the land suits it: near the map's
    /// middle, with little water, marsh or rough rock under it; a plan with docks where most of them reach into the water
    /// (a jetty laid over it), else on dry ground (a landing of planks). Then its stores (the game's own loot and the
    /// people's own goods, the river folk's fish), its herd in the pens, its garden planted, and its people at home under
    /// the game's defend-the-town lord. Without the plan, where the land is wrong for it, or with the deeper game hooks
    /// off, the game makes its own base.
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
            if (!Spot(map, plan, keys, out int x, out int z)) return false;
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
                LayDocks(map, Cells(map, grid, marks, "dock"));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples: the town's docks", e);
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
        /// small share of the cells the plan uses is bad ground (PeoplesMath.LandFits). A plan with docks (the river
        /// folk's landing) looks wider, off its docks the same, and takes the place where most of its docks reach into the
        /// water (PeoplesMath.DockSpot), the nearest the middle on a tie; with no water near, the first place that fits.
        /// The land's rock is cleared where the plan stands, as mining would.
        /// </summary>
        private static bool Spot(Map map, TownPlanDef plan, List<PlanKey> keys, out int x, out int z)
        {
            int w = plan.Width, h = plan.Height;
            var docks = new HashSet<char>(keys.Where(k => k.mark == "dock").Select(k => k.key));
            var used = new List<(int, int)>();
            var dock = new List<(int, int)>();
            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                {
                    char ch = plan.rows[r][c];
                    if (PeoplesMath.IsGround(ch)) continue;
                    (docks.Contains(ch) ? dock : used).Add((c, PeoplesMath.RowZ(r, h)));
                }
            var centre = map.Center;
            int rings = dock.Count > 0 ? 9 : 4, step = dock.Count > 0 ? 5 : 6;
            int best = -1, bx = 0, bz = 0;
            for (int ring = 0; ring <= rings; ring++)
                for (int dx = -ring; dx <= ring; dx++)
                    for (int dz = -ring; dz <= ring; dz++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != ring) continue;
                        PeoplesMath.Corner(centre.x + dx * step, centre.z + dz * step, w, h, map.Size.x, map.Size.z, out x, out z);
                        if (x < 6 || z < 6 || x + w > map.Size.x - 6 || z + h > map.Size.z - 6) continue;
                        int bad = 0;
                        foreach (var (lx, lz) in used)
                            if (Bad(new IntVec3(x + lx, 0, z + lz), map))
                                bad++;
                        if (dock.Count == 0)
                        {
                            if (PeoplesMath.LandFits(bad, used.Count)) return true;
                            continue;
                        }
                        int water = 0;
                        foreach (var (lx, lz) in dock)
                        {
                            var c = new IntVec3(x + lx, 0, z + lz);
                            if (c.InBounds(map) && c.GetTerrain(map)?.IsWater == true) water++;
                        }
                        int worth = PeoplesMath.DockSpot(water, bad, used.Count);
                        if (worth <= best) continue;
                        best = worth;
                        bx = x;
                        bz = z;
                    }
            x = bx;
            z = bz;
            return best >= 0;
        }

        /// <summary>
        /// The landing's docks once its plan is laid: a jetty (the game's bridge) over water a bridge can stand in, a
        /// floor of planks on dry ground that bears one; deep water and rough ground left as they are. The river folk's
        /// crossing on the colony's river lays its cells one by one the same way, with the bridge of their age
        /// (<paramref name="bridgeDef"/>: Peoples/Events2/RiverCrossing.cs).
        /// </summary>
        public static void LayDocks(Map map, List<IntVec3> cells, TerrainDef bridgeDef = null)
        {
            if (map == null || cells.NullOrEmpty()) return;
            var bridge = bridgeDef ?? TerrainDefOf.Bridge;
            var planks = DefDatabase<TerrainDef>.GetNamedSilentFail("WoodPlankFloor");
            foreach (var c in cells)
            {
                if (!c.InBounds(map) || c.GetEdifice(map) != null) continue;
                var t = map.terrainGrid.TerrainAt(c);
                if (t == null) continue;
                if (t.IsWater)
                {
                    if (bridge != null && map.terrainGrid.FoundationAt(c) == null && t.affordances != null && t.affordances.Contains(TerrainAffordanceDefOf.Bridgeable))
                        map.terrainGrid.SetFoundation(c, bridge);
                }
                else if (planks != null && t != planks && t.affordances != null && t.affordances.Contains(TerrainAffordanceDefOf.Light))
                    map.terrainGrid.SetTerrain(c, planks);
            }
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
                    countRange = new IntRange(3, 10), techLevel = Peoples.TechOf(f), makingFaction = f, totalMarketValueRange = new FloatRange(LootValue, LootValue)
                };
                things.AddRange(maker.root.Generate(parms));
            }
            // The river folk's catch: fish of the land's own waters (Odyssey's), in its racks.
            if (ext.fish.max > 0)
                foreach (var t in Fish(map, ext.fish.RandomInRange))
                    things.Add(t);
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

        /// <summary>
        /// Fresh fish, this many in all: the kinds the map's own fresh waters hold (its biome's, Odyssey's), else any of
        /// the game's fish. None without Odyssey.
        /// </summary>
        private static List<Thing> Fish(Map map, int count)
        {
            var things = new List<Thing>();
            if (count <= 0) return things;
            var kinds = map?.Biome?.fishTypes?.freshwater_Common?.Where(f => f?.fishDef != null).Select(f => f.fishDef).ToList() ?? new List<ThingDef>();
            if (kinds.Count == 0 && ThingCategoryDefOf.Fish != null)
                kinds = DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.thingCategories != null && d.thingCategories.Contains(ThingCategoryDefOf.Fish) && d.ingestible != null).ToList();
            if (kinds.Count == 0) return things;
            var kind = kinds.RandomElement();
            while (count > 0)
            {
                var t = ThingMaker.MakeThing(kind);
                t.stackCount = Math.Min(count, Math.Max(1, kind.stackLimit));
                count -= t.stackCount;
                things.Add(t);
            }
            return things;
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
    
        // ================= The river folk's landings by a river =================

        /// <summary>
        /// A new world's river folk (any people whose towns stand by the water): each of their towns the game put where no
        /// river runs moves to a tile a river runs through within twelve tiles, when one will take a town (clear of the
        /// colony's homes, on no other people's land); else it stays where it is, a landing on a lake or a stream too small
        /// for the world map, and its map is laid on dry ground. Once, as a new game starts (ArisingBook.StartedNewGame).
        /// </summary>
        public static void ToTheRivers()
        {
            if (Find.WorldObjects == null) return;
            foreach (var s in Find.WorldObjects.Settlements.ToList())
            {
                if (s?.Faction == null || s.Faction.IsPlayer || !Peoples.ByWater(s.Faction.def) || Peoples.HasRiver(s.Tile)) continue;
                try
                {
                    var kin = new HashSet<Faction> { s.Faction };
                    if (Arising.TryTownTile(s.Tile, 1, 12, kin, null, out var tile, true))
                    {
                        if (Prefs.DevMode) Log.Message("[Rimfolk] " + s.Name + " of " + s.Faction.Name + " moved to a river, " + Find.WorldGrid.ApproxDistanceInTiles(s.Tile, tile).ToString("0") + " tiles off.");
                        s.Tile = tile;
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples: a landing to the river", e);
                }
            }
        }
    }
}
