using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What the land near a home has that draws travellers, read from the map and the region (TravelMath's reasons): a
    /// relic of the colony's faith, a landmark, a sacred tree, ancient ruins on the land or a ruined town in the region, the
    /// colony's books and its history, ore in sight or a rush, grass in its season, a road, the distance from every
    /// market. Each with its words ("the old ruins north of the colony") and what to look at. Read when a camp could come
    /// (once a day a home at most) and kept until the day turns; nothing is saved.
    /// </summary>
    public sealed class LandReasons
    {
        public readonly HashSet<string> holds = new HashSet<string>();
        /// <summary>Each reason's words, as they sit in a sentence ("the old ruins north of the colony").</summary>
        public readonly Dictionary<string, string> what = new Dictionary<string, string>();
        /// <summary>What each reason's thing is on the map (a cell or a thing), or out in the world (a ruin).</summary>
        public readonly Dictionary<string, GlobalTargetInfo> look = new Dictionary<string, GlobalTargetInfo>();

        public bool Has(string reason) => reason != null && holds.Contains(reason);

        public string What(string reason) => reason != null && what.TryGetValue(reason, out var w) ? w : null;

        public GlobalTargetInfo Look(string reason) => reason != null && look.TryGetValue(reason, out var l) ? l : GlobalTargetInfo.Invalid;
    }

    /// <summary>
    /// Travellers who seek out the remote (docs/design/stage1-region.md, item 5): the rates by a home's remoteness (the
    /// camps' daily chance and the rest after one, Rimfolk's visitors among its events) and the land's reasons. The rates
    /// sit behind the "Travellers to the remote" setting and "Systems talk to each other"; the reasons are words and are
    /// always read. Applied per home through the existing clocks (CampTimer, Rimfolk's event timer), never per tick.
    /// </summary>
    public static class TravelLand
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The setting (0 to 2): how much a home's remoteness draws travellers.</summary>
        public static float Rate => TravelMath.Rate(S?.remoteTravellers ?? 1f);

        /// <summary>Remoteness counts toward the rates: the setting above 0, and the systems talking to each other.</summary>
        public static bool On => Rate > 0f && Interplay.On;

        /// <summary>Travellers bring news of far towns (the setting above 0).</summary>
        public static bool NewsOn => Rate > 0f;

        /// <summary>A home's remoteness as the rates read it (0 with the rates off, or for a map that isn't a home).</summary>
        public static float Remote(Map home) => On && home != null ? RegionTowns.Remoteness(home) : 0f;

        /// <summary>A camp's daily chance at this home, from the setting's level ("How many camps", CampCrowds).</summary>
        public static float CampChance(Map home) => TravelMath.CampChance(CampCrowds.DailyChance, Remote(home), Rate) * StoryDirection.Factor(StoryWaysMath.Camps);

        /// <summary>The rest after a camp at this home, as a share of its usual length.</summary>
        public static float GapFactor(Map home) => TravelMath.GapFactor(Remote(home), Rate);

        /// <summary>One of Rimfolk's visitor events' weight at this home.</summary>
        public static float VisitorFactor(Map home) => TravelMath.VisitorFactor(Remote(home), Rate);

        /// <summary>The home's next wait between Rimfolk's own events, as a share of its usual (its visitors' share given).</summary>
        public static float IntervalFactor(Map home, float visitorShare) => TravelMath.IntervalFactor(Remote(home), Rate, visitorShare);

        /// <summary>A kind's weight among the camps that may come to this home: more when it seeks what the land has.</summary>
        public static float SeekWeight(CampKindDef kind, Map home, LandReasons land) =>
            kind?.seeks == null || kind.seeks.Count == 0 ? 1f : TravelMath.SeekWeight(Remote(home), Rate, land != null && TravelMath.AnyHolds(kind.seeks, land.holds));

        // ---------------- Reading the land ----------------

        private static readonly Dictionary<int, (int day, LandReasons land)> cache = new Dictionary<int, (int, LandReasons)>();
        private static Game cacheGame;

        /// <summary>What the land near this home has that draws travellers, read once a day at most.</summary>
        public static LandReasons Read(Map map)
        {
            if (map == null) return new LandReasons();
            if (cacheGame != Current.Game)
            {
                cache.Clear();
                cacheGame = Current.Game;
            }
            int day = GenDate.DaysPassed;
            if (cache.TryGetValue(map.uniqueID, out var c) && c.day == day && c.land != null) return c.land;
            var land = new LandReasons();
            Each(land, TravelMath.Relic, () => ReadRelic(land, map));
            Each(land, TravelMath.Wonder, () => ReadWonder(land, map));
            Each(land, TravelMath.Trees, () => ReadTrees(land, map));
            Each(land, TravelMath.Ruins, () => ReadRuins(land, map));
            Each(land, TravelMath.RuinRegion, () => ReadRuinRegion(land, map));
            Each(land, TravelMath.Library, () => ReadLibrary(land, map));
            Each(land, TravelMath.History, () => ReadHistory(land, map));
            Each(land, TravelMath.Ore, () => ReadOre(land, map));
            Each(land, TravelMath.Rush, () => ReadRush(land));
            Each(land, TravelMath.Grazing, () => ReadGrazing(land, map));
            Each(land, TravelMath.Road, () => ReadRoad(land, map));
            Each(land, TravelMath.Far, () => ReadFar(land, map));
            if (cache.Count > 8) cache.Clear();
            cache[map.uniqueID] = (day, land);
            return land;
        }

        /// <summary>Read again on the next ask (a relic installed, a bookcase filled: the dev's button).</summary>
        public static void Forget() => cache.Clear();

        private static void Each(LandReasons land, string reason, Action read)
        {
            try
            {
                read();
            }
            catch (Exception e)
            {
                land.holds.Remove(reason);
                CouncilLog.ErrorOnce("travellers: the land's " + reason, e);
            }
        }

        private static void Hold(LandReasons land, string reason, string what, GlobalTargetInfo look)
        {
            land.holds.Add(reason);
            if (!what.NullOrEmpty()) land.what[reason] = what;
            if (look.IsValid) land.look[reason] = look;
        }

        private static string W(string key, params NamedArgument[] args) => key.Translate(args).Resolve();

        /// <summary>A relic of the colony's faith kept on this map (Ideology): the thing itself, or what holds it.</summary>
        private static void ReadRelic(LandReasons land, Map map)
        {
            if (!ModsConfig.IdeologyActive || Faction.OfPlayer?.ideos == null) return;
            foreach (var ideo in Faction.OfPlayer.ideos.AllIdeos)
                foreach (var p in ideo?.PreceptsListForReading?.OfType<Precept_Relic>() ?? Enumerable.Empty<Precept_Relic>())
                {
                    var t = p.GeneratedRelic;
                    if (t == null || t.Destroyed || t.MapHeld != map) continue;
                    var at = t.SpawnedParentOrMe;
                    if (at == null || !at.Spawned) continue;
                    Hold(land, TravelMath.Relic, W("CC_Travel_What_relic", t.LabelShort.Named("RELIC")), new GlobalTargetInfo(at));
                    return;
                }
        }

        /// <summary>The colony's land is a landmark of its own (the game's landmarks).</summary>
        private static void ReadWonder(LandReasons land, Map map)
        {
            string name = map.Tile.Valid ? Find.WorldGrid?[map.Tile]?.Landmark?.name : null;
            if (name.NullOrEmpty()) return;
            Hold(land, TravelMath.Wonder, W("CC_Travel_What_wonder", name.Named("NAME")), GlobalTargetInfo.Invalid);
        }

        /// <summary>A Gauranlen tree (Ideology) or an anima tree (Royalty) standing on this map, in sight.</summary>
        private static void ReadTrees(LandReasons land, Map map)
        {
            var defs = new List<ThingDef>();
            if (ModsConfig.RoyaltyActive) defs.Add(DefDatabase<ThingDef>.GetNamedSilentFail("Plant_TreeAnima"));
            if (ModsConfig.IdeologyActive) defs.Add(DefDatabase<ThingDef>.GetNamedSilentFail("Plant_TreeGauranlen"));
            foreach (var d in defs.Where(d => d != null))
            {
                var tree = map.listerThings.ThingsOfDef(d).FirstOrDefault(t => t.Spawned && !t.Position.Fogged(map));
                if (tree == null) continue;
                Hold(land, TravelMath.Trees, W("CC_Travel_What_trees", tree.def.label.Named("TREE"), CampSite.Direction(tree.Position, CampSite.ColonyCenter(map)).Named("DIR")),
                    new GlobalTargetInfo(tree));
                return;
            }
        }

        /// <summary>Ancient ruins on the colony's land, outside its home area (where the salvagers would strip).</summary>
        private static void ReadRuins(LandReasons land, Map map)
        {
            var at = CampLand.RuinTargets(map);
            if (at.Count == 0) return;
            Hold(land, TravelMath.Ruins, W("CC_Travel_What_ruins", CampSite.Direction(at[0], CampSite.ColonyCenter(map)).Named("DIR")), new GlobalTargetInfo(at[0], map));
        }

        /// <summary>An abandoned or destroyed town within the region map's reach of this home: the nearest.</summary>
        private static void ReadRuinRegion(LandReasons land, Map map)
        {
            if (!map.Tile.Valid || Find.WorldObjects == null) return;
            WorldObject best = null;
            float bestTiles = float.MaxValue;
            int reach = RegionTowns.ReachTiles;
            foreach (var o in Find.WorldObjects.AllWorldObjects)
            {
                if (!(o is AbandonedSettlement) && !(o is DestroyedSettlement)) continue;
                if (o.Destroyed || !o.Tile.Valid || o.Tile.Layer != map.Tile.Layer) continue;
                float d = Find.WorldGrid.ApproxDistanceInTiles(map.Tile, o.Tile);
                if (d > reach || d >= bestTiles) continue;
                best = o;
                bestTiles = d;
            }
            if (best == null) return;
            Hold(land, TravelMath.RuinRegion, W("CC_Travel_What_ruinRegion", RegionWords.Where(map, best.Tile, bestTiles).Named("WHERE")), new GlobalTargetInfo(best));
        }

        /// <summary>The colony's books: its bookcases on this map hold <see cref="TravelMath.MinBooks"/> or more.</summary>
        private static void ReadLibrary(LandReasons land, Map map)
        {
            int books = 0;
            Building_Bookcase fullest = null;
            foreach (var b in map.listerBuildings.AllBuildingsColonistOfClass<Building_Bookcase>())
            {
                if (b == null || !b.Spawned) continue;
                int n = b.HeldBooks?.Count ?? 0;
                books += n;
                if (fullest == null || n > (fullest.HeldBooks?.Count ?? 0)) fullest = b;
            }
            if (!TravelMath.LibraryHolds(books) || fullest == null) return;
            Hold(land, TravelMath.Library, W("CC_Travel_What_library", books.Named("COUNT")), new GlobalTargetInfo(fullest));
        }

        /// <summary>The colony's history: a year old or more, with milestones enough in its chronicle.</summary>
        private static void ReadHistory(LandReasons land, Map map)
        {
            int milestones = Chronicle.Lines.Count(e => e.Milestone);
            int founded = map.Parent?.creationGameTicks ?? 0;
            int daysOld = Math.Max(0, (Find.TickManager.TicksGame - Math.Max(0, founded)) / GenDate.TicksPerDay);
            if (!TravelMath.HistoryHolds(daysOld, milestones)) return;
            Hold(land, TravelMath.History, W("CC_Travel_What_history", Homes.Name(map).Named("COLONY"), (daysOld / GenDate.DaysPerYear).Named("YEARS")), GlobalTargetInfo.Invalid);
        }

        private static List<ThingDef> oreDefs;

        /// <summary>The rocks that hold ore (the game's resource rocks).</summary>
        private static List<ThingDef> OreDefs => oreDefs ??= DefDatabase<ThingDef>.AllDefsListForReading
            .Where(d => d?.building != null && d.building.isResourceRock && d.building.mineableThing != null && d.mineable).ToList();

        /// <summary>Ore in sight in the colony's rock: a rich ore (anything but plain steel), or a great deal of steel. Looks at the rich ore nearest the colony.</summary>
        private static void ReadOre(LandReasons land, Map map)
        {
            int rich = 0, steel = 0;
            Thing best = null;
            ThingDef bestOre = null;
            var colony = CampSite.ColonyCenter(map);
            float bestDist = float.MaxValue;
            foreach (var d in OreDefs)
            {
                bool plain = d.building.mineableThing == ThingDefOf.Steel;
                foreach (var t in map.listerThings.ThingsOfDef(d))
                {
                    if (t == null || !t.Spawned || t.Position.Fogged(map)) continue;
                    if (plain)
                    {
                        steel++;
                        continue;
                    }
                    rich++;
                    float dist = t.Position.DistanceToSquared(colony);
                    if (dist >= bestDist) continue;
                    bestDist = dist;
                    best = t;
                    bestOre = d.building.mineableThing;
                }
            }
            if (!TravelMath.OreHolds(rich, steel)) return;
            if (best == null)
            {
                var anySteel = OreDefs.Where(d => d.building.mineableThing == ThingDefOf.Steel).SelectMany(d => map.listerThings.ThingsOfDef(d))
                    .FirstOrDefault(t => t.Spawned && !t.Position.Fogged(map));
                best = anySteel;
                bestOre = ThingDefOf.Steel;
            }
            if (best == null) return;
            Hold(land, TravelMath.Ore, W("CC_Travel_What_ore", bestOre.label.Named("ORE"), CampSite.Direction(best.Position, colony).Named("DIR")), new GlobalTargetInfo(best));
        }

        /// <summary>A rush in the region: a world event of the kind "rush" running.</summary>
        private static void ReadRush(LandReasons land)
        {
            var s = WorldSituations.Instance?.Running.FirstOrDefault(x => x?.def?.kind == "rush");
            if (s == null) return;
            Hold(land, TravelMath.Rush, W("CC_Travel_What_rush", CampWords.SituationName(s).Named("RUSH")), GlobalTargetInfo.Invalid);
        }

        /// <summary>Wild grass in its growing season: the land's plants dense enough, the grass growing now.</summary>
        private static void ReadGrazing(LandReasons land, Map map)
        {
            var grass = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Grass");
            float density = map.Biome?.plantDensity ?? 0f;
            bool season = grass != null && PlantUtility.GrowthSeasonNow(map, grass);
            if (!TravelMath.GrazingHolds(density, season)) return;
            Hold(land, TravelMath.Grazing, W("CC_Travel_What_grazing"), GlobalTargetInfo.Invalid);
        }

        /// <summary>A road through the colony's land (its world tile).</summary>
        private static void ReadRoad(LandReasons land, Map map)
        {
            if (!map.Tile.Valid || !(Find.WorldGrid?[map.Tile] is SurfaceTile st) || st.Roads.NullOrEmpty()) return;
            Hold(land, TravelMath.Road, W("CC_Travel_What_road"), GlobalTargetInfo.Invalid);
        }

        /// <summary>Far from every market: rather remote or more (whatever the setting; this is words).</summary>
        private static void ReadFar(LandReasons land, Map map)
        {
            float r = RegionTowns.Remoteness(map);
            if (!TravelMath.FarHolds(r)) return;
            Hold(land, TravelMath.Far, W("CC_Travel_What_far"), GlobalTargetInfo.Invalid);
        }
    }
}
