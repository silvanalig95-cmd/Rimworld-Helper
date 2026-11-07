using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Rimfolk's own new towns within a home's reach (docs/design/stage1-region.md, item 2; decision 2): a town
    /// flourishing for 60 days may send settlers out to found a town 6 to 15 tiles from it, and a ruin 120 days old may be
    /// settled again by a people at peace with the colony that does well nearby (its own people first, under its old
    /// name). Both beside another mod's builders (Faction Territories' new towns count for nothing here, and its sites and
    /// territories are left alone), at Rimfolk's own pace: one town founded per 90 days and one ruin settled per 120 in a
    /// home's reach, founded only while fewer than 10 towns stand in it. Each new town gets its faces from the region (a
    /// founder, founding families, where its settlers came from and why: RegionFaces), its line in the colony news by the
    /// colony's means, and its place in the history. The tile is the game's own choice of where a town may stand
    /// (TileFinder, each biome's settlement weight), searched only when a roll comes up, with straight-line checks inside
    /// the search.
    /// </summary>
    public static class NewTowns
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>Since when each town in detail has been flourishing (kept on its record).</summary>
        internal static void NoteFlourishing(List<Settlement> towns, int now)
        {
            foreach (var t in towns)
            {
                var rec = WorldLedger.TownRecord(t.ID);
                if (rec == null || !rec.Seeded) continue;
                rec.highSince = RiseFallMath.HighSince(rec.prosperity, rec.highSince, now);
            }
        }

        /// <summary>
        /// A people that may found a town: standing, theirs a name for towns, not the player's, hidden or defeated. Never a
        /// people a colony became: its town grows only at Faction Territories' own pace (stage 3, the player's decision 15B).
        /// </summary>
        private static bool CanFound(Faction f) =>
            f != null && !f.IsPlayer && !f.Hidden && !f.defeated && !f.temporary && f.def != null && f.def.humanlikeFaction && f.def.settlementNameMaker != null
            && !HandOverPeoples.IsOurs(f);

        // ---------------- Settlers from a flourishing town ----------------

        /// <summary>A town flourishing 60 days, on a 15% roll every 15 days, sends settlers out: one a day at most.</summary>
        internal static void Foundings(RegionBook book, List<Settlement> towns, int day, int now)
        {
            foreach (var mother in towns)
            {
                if (!RiseFallMath.RollDay(day, mother.ID, RiseFallMath.FoundEveryDays) || !CanFound(mother.Faction)) continue;
                var rec = WorldLedger.TownRecord(mother.ID);
                if (rec == null || !rec.Seeded || rec.closing >= 0 || !RiseFallMath.MaySendSettlers(rec.prosperity, rec.highSince, now)) continue;
                if (!Rand.Chance(RiseFallMath.FoundChance) || !AnyHomeAllows(book, mother)) continue;
                if (!TryFoundingTile(book, mother, out var tile)) continue;
                Found(book, mother, rec, tile, now);
                return;
            }
        }

        /// <summary>
        /// Before searching: some home within reach of where the settlers could go has had no founding in its gap and has
        /// room for a town (the search itself checks every home a tile lies in).
        /// </summary>
        private static bool AnyHomeAllows(RegionBook book, Settlement mother)
        {
            float far = RegionTowns.ReachTiles + RiseFallMath.FoundMaxTiles;
            int now = Now;
            foreach (var m in Homes.All)
            {
                if (m == null || !m.Tile.Valid || m.Tile.Layer != mother.Tile.Layer || Find.WorldGrid.ApproxDistanceInTiles(m.Tile, mother.Tile) > far) continue;
                var h = book.HomeOf(m);
                if (h != null && RiseFallMath.GapPassed(h.foundedAt, now, RiseFallMath.FoundGapDays) && RiseFallMath.RoomInReach(RegionTowns.Of(m)?.towns.Count ?? 0)) return true;
            }
            return false;
        }

        /// <summary>
        /// A tile for settlers from <paramref name="mother"/>: 6 to 15 tiles from it, valid for a town and with nothing on
        /// it, within a home's reach and clear of every home, not in another people's territory nor next to a town being
        /// built, where the pace and the room in every home's reach allow; picked by the biome's settlement weight.
        /// </summary>
        public static bool TryFoundingTile(RegionBook book, Settlement mother, out PlanetTile tile)
        {
            tile = PlanetTile.Invalid;
            if (mother == null || !mother.Tile.Valid) return false;
            var people = mother.Faction;
            var layer = mother.Tile.Layer;
            int goodwill = RiseFall.GoodwillTiles;
            float reach = RegionTowns.ReachTiles;
            var homes = Homes.All.Where(m => m != null && m.Tile.Valid && m.Tile.Layer == layer).Select(m => m.Tile).ToList();
            if (homes.Count == 0) return false;
            var ours = Find.WorldObjects.Settlements.Where(s => s != null && s.Faction != null && s.Faction.IsPlayer && s.Tile.Layer == layer).Select(s => s.Tile).ToList();
            var sites = WorldMechanics.SettlementsBeingBuilt().Where(b => b != null && b.tile.Valid && b.tile.Layer == layer).Select(b => b.tile).ToList();
            var grid = Find.WorldGrid;
            var found = new List<PlanetTile>();
            var weights = new List<float>();
            // Straight-line distances only inside the search: a walking distance here would start a second flood fill inside the first.
            TileFinder.TryFindPassableTileWithTraversalDistance(mother.Tile, (int)RiseFallMath.FoundMinTiles, (int)RiseFallMath.FoundMaxTiles, out _, t =>
            {
                if (t.Layer != layer || Find.WorldObjects.AnyWorldObjectAt(t)) return false;
                float nearestHome = Nearest(grid, homes, t);
                float nearestOurs = Nearest(grid, ours, t);
                float w = RegionFortunes.BiomeWeight(t);
                if (!RiseFallMath.FoundingTile(true, grid.ApproxDistanceInTiles(mother.Tile, t), nearestHome, reach, goodwill, w)) return false;
                if (!RiseFallMath.ClearOfHomes(nearestOurs, goodwill)) return false;
                if (sites.Any(s => grid.ApproxDistanceInTiles(s, t) <= 1.5f)) return false;
                if (!TileFinder.IsValidTileForNewSettlement(t)) return false;
                var owner = WorldMechanics.TerritoryOwner(t);
                if (owner != null && owner != people) return false;
                found.Add(t);
                weights.Add(w);
                return false;
            });
            if (found.Count == 0) return false;
            // The pace and the room in each home's reach the tile lies in.
            var room = new Dictionary<Map, bool>();
            for (int i = 0; i < found.Count; i++)
            {
                if (weights[i] <= 0f) continue;
                bool ok = RiseFall.PaceAllows(book, found[i], RiseFall.Change.Found);
                if (ok)
                    foreach (var home in RiseFall.HomesReaching(found[i]))
                    {
                        if (!room.TryGetValue(home, out bool r)) room[home] = r = RiseFallMath.RoomInReach(RegionTowns.Of(home)?.towns.Count ?? 0);
                        if (!r) ok = false;
                    }
                if (!ok) weights[i] = 0f;
            }
            int pick = RiseFallMath.PickTile(weights, Rand.Value);
            if (pick < 0) return false;
            tile = found[pick];
            return true;
        }

        private static float Nearest(WorldGrid grid, List<PlanetTile> tiles, PlanetTile t)
        {
            float best = -1f;
            foreach (var x in tiles)
            {
                float d = grid.ApproxDistanceInTiles(x, t);
                if (best < 0f || d < best) best = d;
            }
            return best;
        }

        /// <summary>The town stands: made the way the game makes a town (its people's naming rule), with its faces to come in the region's hour.</summary>
        private static void Found(RegionBook book, Settlement mother, LedgerTown motherRec, PlanetTile tile, int now)
        {
            var people = mother.Faction;
            var town = MakeTown(people, tile, null);
            if (town == null) return;
            // The young families have gone: the town may send settlers again after another season of plenty.
            motherRec.highSince = now;
            RiseFall.Stamp(book, tile, RiseFall.Change.Found);
            Faces(town, mother, "mother");
            var home = RegionTowns.NearestHome(tile, out float tiles);
            if (home != null) RiseFallWords.TellDaughter(home, town, mother, tiles);
        }

        /// <summary>A town of the game's own def for a people on a tile, under a name or one from their naming rule; null when it can't be made.</summary>
        private static Settlement MakeTown(Faction people, PlanetTile tile, string name, WorldObject replaces = null)
        {
            try
            {
                var town = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                town.SetFaction(people);
                town.Tile = tile;
                bool taken = !name.NullOrEmpty() && Find.WorldObjects.Settlements.Any(s => s != null && s.Name == name);
                town.Name = name.NullOrEmpty() || taken ? SettlementNameGenerator.GenerateSettlementName(town, people.def.settlementNameMaker) : name;
                replaces?.Destroy();
                Find.WorldObjects.Add(town);
                return town;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("rise and fall: a town made", e);
                return null;
            }
        }

        /// <summary>
        /// The new town's faces from the region (World/Region/RegionFaces.cs): a founder made in the region's hour, two
        /// founding families, the town its settlers came from (<paramref name="mother"/>) and why (<paramref name="why"/>).
        /// It counts as one of Rimfolk's own: told here, not again by the region.
        /// </summary>
        private static void Faces(Settlement town, Settlement mother, string why)
        {
            try
            {
                RegionFaces.Founded(town, null, why);
                var f = WorldLedger.FoundingAt(town.ID);
                if (f == null || mother == null || mother.Destroyed) return;
                f.mother = Refs.Of(mother);
                f.motherName = mother.HasName ? mother.Name : null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("rise and fall: a new town's faces", e);
            }
        }

        // ---------------- A ruin settled again ----------------

        /// <summary>A ruin 120 days old, on a 10% roll every 15 days, with a people to settle it and the pace allowing: one a day at most.</summary>
        internal static void Resettles(RegionBook book, int day, int now)
        {
            var l = WorldLedger.Instance;
            if (l == null || l.Ruins.Count == 0) return;
            foreach (var r in l.Ruins.ToList())
            {
                if (r.Waiting || !RiseFallMath.RollDay(day, r.town, RiseFallMath.ResettleEveryDays) || !RiseFallMath.RuinReady(r.since, now)) continue;
                var o = RiseFall.WorldObjectOf(r.ruin);
                if (o == null || !RiseFall.PaceAllows(book, o.Tile, RiseFall.Change.Resettle) || !RiseFall.RuinSafe(o)) continue;
                var people = Settlers(r, o.Tile, out var mother);
                if (people == null || !Rand.Chance(RiseFallMath.ResettleChance) || !TileHoldsTown(o.Tile, o, people)) continue;
                Resettle(book, r, o, people, mother, now);
                return;
            }
        }

        /// <summary>
        /// Who settles a ruin, and from which town: of the peoples at peace with the colony with a town in detail within
        /// 25 tiles, the ruin's own people when their best town there stands at 50 or more, else the one whose town stands
        /// best at 60 or more. Null for nobody.
        /// </summary>
        private static Faction Settlers(LedgerRuin r, PlanetTile tile, out Settlement from)
        {
            from = null;
            var best = new Dictionary<Faction, (float p, Settlement town)>();
            var player = Faction.OfPlayerSilentFail;
            foreach (var t in RiseFall.TownsInDetail())
            {
                var f = t.Faction;
                if (!CanFound(f) && (f != r.people || HandOverPeoples.IsOurs(f)) || f == null || player == null || f.HostileTo(player) || f.defeated) continue;
                if (t.Tile.Layer != tile.Layer || Find.WorldGrid.ApproxDistanceInTiles(t.Tile, tile) > RiseFallMath.ResettleTiles) continue;
                var rec = WorldLedger.TownRecord(t.ID);
                if (rec == null || !rec.Seeded || rec.closing >= 0) continue;
                if (!best.TryGetValue(f, out var b) || rec.prosperity > b.p) best[f] = (rec.prosperity, t);
            }
            var peoples = best.Keys.ToList();
            var may = peoples.Select(f => RiseFallMath.MayResettle(true, f == r.people, best[f].p)).ToList();
            var own = peoples.Select(f => f == r.people).ToList();
            var tops = peoples.Select(f => best[f].p).ToList();
            int i = RiseFallMath.Settlers(may, own, tops);
            if (i < 0) return null;
            from = best[peoples[i]].town;
            return peoples[i];
        }

        /// <summary>
        /// The ruin's tile may hold a town again: its land still takes one, no town stands on it or next to it, nothing but
        /// the ruin is there, no map of it is open, it lies within a home's reach and clear of every home, and no other
        /// people's territory covers it.
        /// </summary>
        private static bool TileHoldsTown(PlanetTile tile, WorldObject ruin, Faction people)
        {
            var t = Find.WorldGrid[tile];
            var biome = t?.PrimaryBiome;
            if (biome == null || !biome.canBuildBase || !biome.implemented || t.hilliness == Hilliness.Impassable) return false;
            if (Find.WorldObjects.AnySettlementBaseAtOrAdjacent(tile) || Current.Game.FindMap(tile) != null) return false;
            foreach (var o in Find.WorldObjects.ObjectsAt(tile))
                if (o != ruin)
                    return false;
            var nearest = RegionTowns.NearestHome(tile, out float tiles);
            float ours = -1f;
            foreach (var s in Find.WorldObjects.Settlements)
                if (s != null && s.Faction != null && s.Faction.IsPlayer && s.Tile.Layer == tile.Layer)
                {
                    float d = Find.WorldGrid.ApproxDistanceInTiles(s.Tile, tile);
                    if (ours < 0f || d < ours) ours = d;
                }
            if (nearest == null || !RiseFallMath.ResettleTile(true, tiles, RegionTowns.ReachTiles, RiseFall.GoodwillTiles) || !RiseFallMath.ClearOfHomes(ours, RiseFall.GoodwillTiles)) return false;
            var owner = WorldMechanics.TerritoryOwner(tile);
            return owner == null || owner == people;
        }

        /// <summary>
        /// Families come back to the old walls: the ruin goes and a town of theirs stands on its tile, under its old name
        /// (newcomers give their own half the time), with its faces from the region, its line in the news and the history's
        /// world:resettled.
        /// </summary>
        private static void Resettle(RegionBook book, LedgerRuin r, WorldObject ruin, Faction people, Settlement from, int now)
        {
            bool own = people == r.people;
            string name = own || !Rand.Chance(RiseFallMath.NewNameChance) ? r.name : null;
            var tile = ruin.Tile;
            var town = MakeTown(people, tile, name, ruin);
            if (town == null) return;
            WorldLedger.RemoveRuin(r);
            RiseFall.Stamp(book, tile, RiseFall.Change.Resettle);
            Faces(town, from, "ruin");
            var actors = new List<HistoryActor> { new HistoryActor(Refs.Of(people), HistoryRole.Did), new HistoryActor(Ref.Town(r.town), HistoryRole.About) };
            if (!own && r.people != null) actors.Add(new HistoryActor(Refs.Of(r.people), HistoryRole.About));
            History.Add(RiseFallWords.ResettledKind, actors, Refs.Of(town), WorldMath.Weight(false), RiseFallWords.HistoryResettled(people, r.name, town.Name),
                values: new List<string> { r.name ?? "", town.Name });
            var home = RegionTowns.NearestHome(tile, out float tiles);
            if (home != null) RiseFallWords.TellResettled(home, town, r, own, tiles);
        }
    }
}
