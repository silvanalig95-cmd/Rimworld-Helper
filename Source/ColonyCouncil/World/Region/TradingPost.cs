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
    /// A trading post a people opened near one of the colony's homes (docs/design/stage1-region.md, item 3): which town
    /// it is (the game's own settlement of theirs, whose record on the world ledger is of kind Post), whose, when it
    /// opened, how sour relations have been and whether its keeper has warned, a closing that waits for the post to be
    /// safe to take off the map, and its last sack. The rules are <see cref="TradingPostMath"/>'s;
    /// <see cref="TradingPosts"/> runs it.
    /// </summary>
    public class TradingPost : IExposable
    {
        /// <summary>The town's world object id.</summary>
        public int town = -1;
        public Faction people;
        /// <summary>Its name, kept for when it is gone.</summary>
        public string name;
        /// <summary>Its tile as a ref.</summary>
        public Ref tile;
        public int opened = -1;
        /// <summary>Since when their goodwill has been under 0, or -1.</summary>
        public int sourSince = -1;
        /// <summary>When its keeper wrote that it will close unless things mend, or -1 (taken back when they mend).</summary>
        public int warnedAt = -1;
        /// <summary>It is to close (why), but the colony has business there: since when it waits, or -1.</summary>
        public int closing = -1;
        public PostEnd closeWhy;
        /// <summary>When raiders last sacked it, or -1.</summary>
        public int sackedAt = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref town, "town", -1);
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref name, "name");
            Refs.Look(ref tile, "tile");
            Scribe_Values.Look(ref opened, "opened", -1);
            Scribe_Values.Look(ref sourSince, "sourSince", -1);
            Scribe_Values.Look(ref warnedAt, "warnedAt", -1);
            Scribe_Values.Look(ref closing, "closing", -1);
            Scribe_Values.Look(ref closeWhy, "closeWhy");
            Scribe_Values.Look(ref sackedAt, "sackedAt", -1);
        }
    }

    /// <summary>
    /// One home's part in the trading posts (kept on its <see cref="RegionHome"/>): the posts standing near it (two at
    /// most), when it was last looked at for a new one, when one last opened or closed there, and how many it has seen.
    /// </summary>
    public class TradingPostHome : IExposable
    {
        public List<TradingPost> posts = new List<TradingPost>();
        public int lookedAt = -1;
        public int changedAt = -1;
        public int opened;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref posts, "posts", LookMode.Deep);
            Scribe_Values.Look(ref lookedAt, "lookedAt", -1);
            Scribe_Values.Look(ref changedAt, "changedAt", -1);
            Scribe_Values.Look(ref opened, "opened");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                posts ??= new List<TradingPost>();
                posts.RemoveAll(p => p == null || p.town < 0 || p.people == null);
            }
        }
    }

    /// <summary>
    /// Trading posts (docs/design/stage1-region.md, item 3; decision 6: a real town of theirs on the world map). A people
    /// at peace with the colony, on good terms (goodwill 40, or allied), that trades with it often (three times in 120
    /// days) and whose towns are far (15 tiles or more) may open a post a few days off: the game's own settlement of
    /// theirs, 6 to 12 tiles from the home toward their nearest town, on a road or a river by preference, named by their
    /// age ("Ashford Market"), with its keeper and its trader among the notables at once and a letter that looks at it.
    /// It trades in the game's own trade window like any of their towns; while it stands their trader caravans come half
    /// again as often and the bandit road is twice as likely. When relations sour (goodwill under 0 for ten days) its
    /// keeper writes that it will close unless things mend; ten more days and it closes through the region's giving up (a
    /// ruin). War with the colony closes it within a day, raiders of a people at war with them may sack it, and a post
    /// the colony razes is a heavy wrong their people remember. Looked at once a day in the region's day
    /// (<see cref="Pass.DailyRegion"/>); nothing per tick, no pass of its own. Off, none opens and those standing are
    /// left to their people as ordinary towns while their records are kept.
    /// </summary>
    public static partial class TradingPosts
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;
        private const int Day = TradingPostMath.DayTicks;

        /// <summary>The setting "Trading posts".</summary>
        public static bool On => (ColonyCouncilMod.Settings?.tradingPosts ?? true) && Current.Game != null;

        /// <summary>The posts' say in caravans and the world's events: with the setting and "Systems talk to each other".</summary>
        public static bool LinksOn => On && Interplay.On;

        /// <summary>Their caravans' odds (a hook on the game's caravan arrival): the links and the deeper game hooks.</summary>
        public static bool CaravansOn => LinksOn && RimfolkPatches.On;

        // ---------------- Reading ----------------

        /// <summary>A home's part (made when it has none); null for a map that isn't a home or without a game.</summary>
        public static TradingPostHome Of(Map home)
        {
            if (home == null || !Homes.IsHome(home)) return null;
            var rh = RegionBook.Instance?.HomeOf(home);
            if (rh == null) return null;
            return rh.posts ??= new TradingPostHome();
        }

        /// <summary>The posts standing near a home (a shared list: read it, don't change it); empty for none.</summary>
        public static IReadOnlyList<TradingPost> PostsOf(Map home) => Of(home)?.posts ?? (IReadOnlyList<TradingPost>)Array.Empty<TradingPost>();

        /// <summary>Every post with its home, in the homes' order.</summary>
        public static List<(Map home, TradingPost post)> All()
        {
            var list = new List<(Map, TradingPost)>();
            if (RegionBook.Instance == null) return list;
            foreach (var m in Homes.All)
                foreach (var p in PostsOf(m))
                    list.Add((m, p));
            return list;
        }

        /// <summary>The post a town is, with its home; null when it is none.</summary>
        public static TradingPost PostAt(int town, out Map home)
        {
            home = null;
            if (town < 0 || RegionBook.Instance == null) return null;
            foreach (var m in Homes.All)
                foreach (var p in PostsOf(m))
                    if (p.town == town)
                    {
                        home = m;
                        return p;
                    }
            return null;
        }

        /// <summary>A town is a trading post the ledger keeps (its record's kind).</summary>
        public static bool IsPost(int town)
        {
            // The ledger's index (the notables' titles ask this as their panels draw).
            var rec = town >= 0 ? WorldLedger.TownFor(town, null, null, false) : null;
            return rec != null && rec.kind == TownKind.Post && rec.gone < 0;
        }

        /// <summary>The town a post is, while it stands; null once it is gone.</summary>
        public static Settlement TownOf(TradingPost p)
        {
            var t = p != null ? NotableBook.TownOf(p.town) : null;
            return t != null && !t.Destroyed ? t : null;
        }

        /// <summary>The post's keeper: the head notable of its town while one is known, else null.</summary>
        public static Notable KeeperOf(TradingPost p)
        {
            var book = NotableBook.Instance;
            if (p == null || book == null) return null;
            foreach (var n in book.notables)
                if (n != null && n.town == p.town && n.role == NotableRole.Head && n.Alive)
                    return n;
            return null;
        }

        /// <summary>
        /// What a post's keeper is called, for the notables' own titles (NotableBook.TitleOf): the head of a town that is a
        /// post, by their people's age (trader, factor, manager, quartermaster). Null for anyone else.
        /// </summary>
        public static string KeeperTitle(Notable n)
        {
            if (n == null || n.role != NotableRole.Head || !IsPost(n.town)) return null;
            return TradingPostWords.KeeperWord(n.faction);
        }

        // ---------------- The day ----------------

        /// <summary>
        /// The region's day: each home's posts looked after (gone, at war, souring, warned, mended, closing, sacked), then
        /// a look for a new one; and ledger records of kind Post that no home's post holds any more become ordinary towns.
        /// </summary>
        internal static void Daily(RegionBook book, int now)
        {
            if (book == null) return;
            int day = now / Day;
            var held = new HashSet<int>();
            foreach (var home in Homes.All.ToList())
            {
                var ph = Of(home);
                if (ph == null) continue;
                try
                {
                    foreach (var p in ph.posts.ToList())
                    {
                        Keep(home, ph, p, day, now);
                        if (ph.posts.Contains(p)) held.Add(p.town);
                    }
                    Look(home, ph, now);
                    foreach (var p in ph.posts) held.Add(p.town);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("trading posts: the day", e);
                }
            }
            try
            {
                Strays(held);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trading posts: records no post holds", e);
            }
        }

        /// <summary>A town of kind Post that no home's post holds (the home lost and its part let go): an ordinary town of theirs again.</summary>
        private static void Strays(HashSet<int> held)
        {
            var l = WorldLedger.Instance;
            if (l == null) return;
            bool changed = false;
            foreach (var t in l.Towns)
                if (t != null && t.kind == TownKind.Post && !held.Contains(t.id))
                {
                    t.kind = TownKind.Town;
                    changed = true;
                }
            if (changed) WorldLedger.TownChanged();
        }

        /// <summary>One post's day.</summary>
        private static void Keep(Map home, TradingPostHome ph, TradingPost p, int day, int now)
        {
            var t = TownOf(p);
            // Gone some other way (taken in an invasion, made the colony's vassal, given up for hard times, another mod):
            // its record ends without a word of its own (what took it told it). Given up for hard times (a ruin the region
            // keeps), the post's history still closes with one line.
            if (t == null || t.Faction != p.people || p.people == null || p.people.defeated)
            {
                var ruin = t == null ? WorldLedger.RuinOfTown(p.town) : null;
                End(home, ph, p, ruin != null && ruin.why == LedgerRuin.GivenUp ? PostEnd.HardTimes : PostEnd.Gone, null, null);
                return;
            }
            // Its home's records came with the colony to a far place (a gravship landed elsewhere, the colony settled anew):
            // no longer this home's post, it is an ordinary town of theirs, and the home has room for one of its own again.
            bool sameLayer = home.Tile.Valid && t.Tile.Layer == home.Tile.Layer;
            if (!TradingPostMath.StaysNear(sameLayer, sameLayer ? Find.WorldGrid.ApproxDistanceInTiles(home.Tile, t.Tile) : -1f, RegionTowns.ReachTiles))
            {
                End(home, ph, p, PostEnd.Gone, null, null);
                return;
            }
            var rec = WorldLedger.TownFor(t.ID, t.HasName ? t.Name : p.name, t.Faction, true);
            if (rec != null && rec.kind != TownKind.Post)
            {
                rec.kind = TownKind.Post;
                WorldLedger.TownChanged();
            }
            if (t.HasName) p.name = t.Name;
            // Off: no new life for it; it stands as any town of theirs.
            if (!On) return;
            var player = Faction.OfPlayer;
            if (p.people.HostileTo(player))
            {
                Close(home, ph, p, t, PostEnd.War, null);
                return;
            }
            int goodwill = p.people.HasGoodwill ? p.people.PlayerGoodwill : 0;
            p.sourSince = TradingPostMath.SourSince(goodwill, p.sourSince, now);
            // Waiting to close while the colony had business there: still sour, it closes once it can. Mended meanwhile (the
            // keeper says so below), or at peace again after a war (the souring clock goes on from here), it stays.
            if (p.closing >= 0)
            {
                if (p.closeWhy == PostEnd.Soured && p.sourSince < 0 || p.closeWhy == PostEnd.War) p.closing = -1;
                else
                {
                    Close(home, ph, p, t, p.closeWhy, null);
                    return;
                }
            }
            if (TradingPostMath.Mended(p.sourSince, p.warnedAt)) Mend(home, p, t, now);
            else if (TradingPostMath.WarnDue(p.sourSince, p.warnedAt, now)) Warn(home, p, t, now);
            else if (TradingPostMath.CloseDue(p.sourSince, p.warnedAt, now))
            {
                Close(home, ph, p, t, PostEnd.Soured, null);
                return;
            }
            if (RegionBook.FortunesOn && RiseFallMath.RollDay(day, t.ID, TradingPostMath.SackEveryDays))
            {
                var enemy = EnemyNear(t, out float tiles);
                if (enemy != null && TradingPostMath.SackRisk(tiles) && Rand.Chance(TradingPostMath.SackChance)) Sack(home, ph, p, t, enemy, now);
            }
        }

        /// <summary>The keeper writes that the post will close unless things mend.</summary>
        internal static void Warn(Map home, TradingPost p, Settlement t, int now)
        {
            p.warnedAt = now;
            TradingPostWords.TellWarned(home, p, t, KeeperOf(p));
            HistoryOf(TradingPostMath.KindWarned, home, p, TradingPostWords.HistoryWarned(p));
        }

        /// <summary>Things mended after the warning: it stays open, and the keeper says so.</summary>
        private static void Mend(Map home, TradingPost p, Settlement t, int now)
        {
            p.warnedAt = -1;
            TradingPostWords.TellMended(home, p, t, KeeperOf(p));
            HistoryOf(TradingPostMath.KindMended, home, p, TradingPostWords.HistoryMended(p));
        }

        /// <summary>A people at war with the post's own, with a town nearest it, and how far; null for none.</summary>
        private static Faction EnemyNear(Settlement post, out float tiles)
        {
            tiles = -1f;
            Faction best = null;
            var grid = Find.WorldGrid;
            foreach (var s in Find.WorldObjects.Settlements)
            {
                var f = s?.Faction;
                if (f == null || s.Destroyed || f == post.Faction || f.IsPlayer || s.Tile.Layer != post.Tile.Layer || !Facts.Counts(f) || !f.HostileTo(post.Faction)) continue;
                float d = grid.ApproxDistanceInTiles(s.Tile, post.Tile);
                if (best != null && d >= tiles) continue;
                best = f;
                tiles = d;
            }
            return best;
        }

        // ---------------- A new post ----------------

        /// <summary>
        /// The look at a home every 15 days: room for a post (two at most, none opened or closed there in 60 days), a people
        /// that qualifies, the 20% chance, then the people that trades most (then the best goodwill) and a place for it;
        /// the next people when no place fits the first.
        /// </summary>
        private static void Look(Map home, TradingPostHome ph, int now)
        {
            if (!On || !TradingPostMath.LookDue(ph.lookedAt, now)) return;
            ph.lookedAt = now;
            if (!TradingPostMath.HomeAllows(ph.posts.Count, ph.changedAt, now)) return;
            var peoples = Candidates(home, ph, now, out var trades, out var goodwill);
            if (peoples.Count == 0 || !Rand.Chance(TradingPostMath.Chance)) return;
            while (peoples.Count > 0)
            {
                int i = TradingPostMath.Pick(trades, goodwill);
                if (i < 0) return;
                if (Open(home, ph, peoples[i], now) != null) return;
                peoples.RemoveAt(i);
                trades.RemoveAt(i);
                goodwill.RemoveAt(i);
            }
        }

        /// <summary>A people that can open a post at all: one of the world's real humanlike peoples (the Empire too), with a naming rule for towns and town traders.</summary>
        public static bool CanOpen(Faction f) =>
            f != null && !f.IsPlayer && !f.Hidden && !f.defeated && !f.temporary && f.def != null && f.def.humanlikeFaction && f.def.settlementNameMaker != null
            && !f.def.baseTraderKinds.NullOrEmpty();

        /// <summary>The peoples that qualify for a post near this home now, with their trades with the colony in the last 120 days and their goodwill.</summary>
        private static List<Faction> Candidates(Map home, TradingPostHome ph, int now, out List<int> trades, out List<int> goodwill)
        {
            var list = new List<Faction>();
            trades = new List<int>();
            goodwill = new List<int>();
            var player = Faction.OfPlayer;
            int since = now - TradingPostMath.TradeDays * Day;
            string traded = HistoryKinds.OfDeed("traded");
            foreach (var f in Find.FactionManager.AllFactionsListForReading)
            {
                if (!CanOpen(f)) continue;
                int gw = f.HasGoodwill ? f.PlayerGoodwill : 0;
                bool ally = f.PlayerRelationKind == FactionRelationKind.Ally;
                bool hostile = f.HostileTo(player);
                if (hostile || gw < TradingPostMath.MinGoodwill && !ally) continue;
                int n = History.Count(traded, Refs.Of(f), default, since);
                var town = NearestTown(home, f, out float tiles);
                bool has = ph.posts.Any(p => p.people == f);
                if (!TradingPostMath.PeopleFits(true, hostile, gw, ally, n, town != null ? tiles : -1f, true, has)) continue;
                list.Add(f);
                trades.Add(n);
                goodwill.Add(gw);
            }
            return list;
        }

        /// <summary>A people's nearest standing town to the home on its layer, with how far; null for none.</summary>
        private static Settlement NearestTown(Map home, Faction f, out float tiles)
        {
            tiles = -1f;
            Settlement best = null;
            foreach (var s in Find.WorldObjects.Settlements)
            {
                if (s == null || s.Destroyed || s.Faction != f || s.Tile.Layer != home.Tile.Layer) continue;
                float d = Find.WorldGrid.ApproxDistanceInTiles(home.Tile, s.Tile);
                if (best != null && d >= tiles) continue;
                best = s;
                tiles = d;
            }
            return best;
        }

        /// <summary>
        /// A post of this people near this home, opened now: a place for it, the town (named by their age), its record on
        /// the ledger, its keeper and trader among the notables, the history and the letter. Null when no place fits or
        /// the town can't be made (the rules of who and when are the caller's; the dev action skips them).
        /// </summary>
        internal static TradingPost Open(Map home, TradingPostHome ph, Faction people, int now)
        {
            if (home == null || ph == null || !CanOpen(people) || ph.posts.Any(p => p.people == people)) return null;
            var from = NearestTown(home, people, out _);
            var tile = FindTile(home, people, from);
            if (!tile.Valid) return null;
            var town = MakeTown(people, tile);
            if (town == null) return null;
            var post = new TradingPost { town = town.ID, people = people, name = town.Name, tile = Refs.Of(tile), opened = now };
            ph.posts.Add(post);
            ph.changedAt = now;
            ph.opened++;
            var rec = WorldLedger.TownFor(town.ID, town.Name, people, true);
            if (rec != null)
            {
                rec.kind = TownKind.Post;
                if (RegionBook.FortunesOn)
                {
                    rec.prosperity = TradingPostMath.Start;
                    rec.movedAt = now;
                    rec.cause = "CC_Post_Cause_Opened".Translate().Resolve();
                    rec.causeDir = 0;
                    rec.causeAt = now;
                    rec.lowSince = -1;
                }
                WorldLedger.TownChanged();
            }
            RegionTowns.Invalidate();
            Pawn keeper = null;
            try
            {
                keeper = NotableBook.StaffPost(town, TradingPostWords.NoteOpened(town));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trading posts: its keeper and trader", e);
            }
            var actors = new List<HistoryActor> { new HistoryActor(Refs.Of(people), HistoryRole.Did), new HistoryActor(Refs.Of(town), HistoryRole.About) };
            if (keeper != null) actors.Insert(1, new HistoryActor(WorldLedger.PersonOf(keeper), HistoryRole.Did));
            History.Add(TradingPostMath.KindOpened, actors, Refs.Colony(home), HistoryMath.Milestone, TradingPostWords.HistoryOpened(post, home));
            TradingPostWords.TellOpened(home, post, town, keeper, from);
            return post;
        }

        /// <summary>
        /// Where the post stands: 6 to 12 tiles from the home over passable land, on a tile the game holds valid for a town
        /// with nothing on it, more than the game's goodwill reach from every one of the colony's settlements, 3 tiles or
        /// more from other towns, not beside a town being built nor in another people's territory; toward their nearest
        /// town (<paramref name="toward"/>, within 60 degrees) where it can, by the land's settlement weight, a road
        /// three times and a river twice. Straight-line distances only inside the flood fill. Invalid for none.
        /// </summary>
        public static PlanetTile FindTile(Map home, Faction people, Settlement toward)
        {
            if (home == null || !home.Tile.Valid) return PlanetTile.Invalid;
            try
            {
                var root = home.Tile;
                var grid = Find.WorldGrid;
                var layer = root.Layer;
                int goodwillTiles = RiseFall.GoodwillTiles;
                float near = TradingPostMath.MaxTiles + TradingPostMath.TownGap + 1f;
                var ours = Find.WorldObjects.Settlements.Where(s => s != null && !s.Destroyed && s.Faction != null && s.Faction.IsPlayer && s.Tile.Layer == layer)
                    .Select(s => s.Tile).ToList();
                var others = Find.WorldObjects.Settlements.Where(s => s != null && !s.Destroyed && (s.Faction == null || !s.Faction.IsPlayer) && s.Tile.Layer == layer
                                                                        && grid.ApproxDistanceInTiles(root, s.Tile) <= near).Select(s => s.Tile).ToList();
                var sites = WorldMechanics.SettlementsBeingBuilt().Where(b => b != null && b.tile.Valid && b.tile.Layer == layer).Select(b => b.tile).ToList();
                Vector3 hc = grid.GetTileCenter(root);
                bool facing = toward != null && toward.Tile.Valid && toward.Tile.Layer == layer;
                Vector3 tc = facing ? grid.GetTileCenter(toward.Tile) : Vector3.zero;
                var tiles = new List<PlanetTile>();
                var weights = new List<float>();
                var towards = new List<bool>();
                Action<PlanetTile, int> each = (t, _) =>
                {
                    float d = grid.ApproxDistanceInTiles(root, t);
                    if (d < TradingPostMath.MinTiles || Find.WorldObjects.AnyWorldObjectAt(t)) return;
                    float nearestOurs = Nearest(grid, ours, t), nearestTown = Nearest(grid, others, t);
                    float w = RegionFortunes.BiomeWeight(t);
                    if (!TradingPostMath.PlaceFits(true, d, nearestOurs, goodwillTiles, nearestTown, w)) return;
                    if (sites.Any(s => grid.ApproxDistanceInTiles(s, t) <= 1.5f)) return;
                    if (!TileFinder.IsValidTileForNewSettlement(t)) return;
                    var owner = WorldMechanics.TerritoryOwner(t);
                    if (owner != null && owner != people) return;
                    bool road = false, river = false;
                    if (grid[t] is SurfaceTile st)
                    {
                        road = !st.Roads.NullOrEmpty();
                        river = !st.Rivers.NullOrEmpty();
                    }
                    float angle = -1f;
                    if (facing)
                    {
                        var c = grid.GetTileCenter(t);
                        angle = TradingPostMath.AngleAt(hc.x, hc.y, hc.z, c.x, c.y, c.z, tc.x, tc.y, tc.z);
                    }
                    tiles.Add(t);
                    weights.Add(TradingPostMath.PlaceWeight(road, river, w));
                    towards.Add(TradingPostMath.Toward(angle));
                };
                layer.Filler.FloodFill(root, t => !Find.World.Impassable(t) && grid.ApproxDistanceInTiles(root, t) <= TradingPostMath.MaxTiles, each);
                int i = TradingPostMath.PickTile(towards, weights, Rand.Value);
                return i < 0 ? PlanetTile.Invalid : tiles[i];
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trading posts: a place for one", e);
                return PlanetTile.Invalid;
            }
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

        /// <summary>The post as the game's own town of their people on the tile, named by their naming rule and their age ("Ashford Market"); null when it can't be made.</summary>
        private static Settlement MakeTown(Faction people, PlanetTile tile)
        {
            try
            {
                var town = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                town.SetFaction(people);
                town.Tile = tile;
                string root = SettlementNameGenerator.GenerateSettlementName(town, people.def.settlementNameMaker);
                string named = TradingPostWords.PostName(root, people);
                bool taken = Find.WorldObjects.Settlements.Any(s => s != null && s.HasName && s.Name == named);
                town.Name = taken || named.NullOrEmpty() ? root : named;
                Find.WorldObjects.Add(town);
                return town;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trading posts: its town made", e);
                return null;
            }
        }

        // ---------------- Closing ----------------

        /// <summary>
        /// A post may come off the map now without breaking anything: for every people the giving-up rules
        /// (SituationWorker_Abandoned.Safe: no map of it, no quest about it, no caravan or pods on their way, no caravan
        /// request open, its people keeping another town). A post of the Empire's (whose own towns are never given up)
        /// by the same rules but that one: the Empire opened it, and it may close it.
        /// </summary>
        public static bool SafeToClose(Settlement t)
        {
            if (t == null || t.Destroyed || t.Faction == null) return false;
            if (!Ages.IsEmpire(t.Faction)) return SituationWorker_Abandoned.Safe(t);
            if (t.Faction.IsPlayer || t.Faction.defeated || t.def != WorldObjectDefOf.Settlement || t.HasMap || !t.questTags.NullOrEmpty()) return false;
            if (Find.WorldObjects.Settlements.Count(x => x.Faction == t.Faction && !x.Destroyed) < 2) return false;
            if (t.GetComponent<TradeRequestComp>()?.ActiveRequest ?? false) return false;
            foreach (var c in Find.WorldObjects.Caravans)
                if (c.Tile == t.Tile || c.pather != null && c.pather.Moving && c.pather.Destination == t.Tile)
                    return false;
            foreach (var pods in Find.WorldObjects.TravellingTransporters)
                if (pods.destinationTile == t.Tile)
                    return false;
            foreach (var q in Find.QuestManager.QuestsListForReading)
            {
                if (q.State != QuestState.Ongoing && q.State != QuestState.NotYetAccepted) continue;
                if (q.QuestLookTargets.Any(l => l.HasWorldObject && l.WorldObject == t)) return false;
            }
            return true;
        }

        /// <summary>
        /// The post closes (soured, at war, sacked beyond bearing): when safe to take off the map, through the region's
        /// giving up (its notables to their people's nearest town, the game's abandoned settlement in its place, a ruin the
        /// ledger keeps), told and written; else it waits for the colony's business there to be done.
        /// </summary>
        internal static void Close(Map home, TradingPostHome ph, TradingPost p, Settlement t, PostEnd why, Faction enemy)
        {
            if (t == null || !SafeToClose(t))
            {
                if (p.closing < 0 || p.closeWhy != why)
                {
                    p.closing = Now;
                    p.closeWhy = why;
                }
                return;
            }
            var ruin = RiseFall.MakeRuin(t, WorldLedger.TownRecord(t.ID), false);
            if (ruin == null)
            {
                p.closing = p.closing < 0 ? Now : p.closing;
                p.closeWhy = why;
                return;
            }
            if (why == PostEnd.Sacked) Refugees(home, p.people);
            End(home, ph, p, why, ruin, enemy);
        }

        /// <summary>Families of a post given up after a sack: a refugee family of theirs may make camp at the gate (the camps' refugee family, as for a town given up).</summary>
        private static void Refugees(Map home, Faction people)
        {
            var kind = DefDatabase<CampKindDef>.GetNamedSilentFail(RiseFallEffects.RefugeeCamp);
            var camps = CampTracker.Instance;
            if (kind == null || camps == null || home == null || !RiseFallEffects.SendsOwn(kind, people)) return;
            try
            {
                camps.StartKind(home, kind, people);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trading posts: refugees at the gate", e);
            }
        }

        /// <summary>
        /// The post's record ends: its home's clock starts again (no new post for 60 days), and how it ended is told and
        /// written (a closing, a razing; a post simply gone says nothing). A town still standing is an ordinary town of
        /// theirs again.
        /// </summary>
        internal static void End(Map home, TradingPostHome ph, TradingPost p, PostEnd why, WorldObject ruin, Faction enemy)
        {
            if (ph == null || p == null || !ph.posts.Remove(p)) return;
            int now = Now;
            ph.changedAt = now;
            var t = TownOf(p);
            var rec = WorldLedger.TownRecord(p.town);
            if (t != null && rec != null && rec.kind == TownKind.Post)
            {
                rec.kind = TownKind.Town;
                WorldLedger.TownChanged();
            }
            string kind = TradingPostMath.EndKind(why);
            if (kind != null && home != null) HistoryOf(kind, home, p, TradingPostWords.HistoryEnded(p, why, enemy), enemy);
            if (home != null) TradingPostWords.TellEnded(home, p, why, ruin, enemy);
        }

        /// <summary>A post's own event in the history: at its home, its people and its town named.</summary>
        private static void HistoryOf(string kind, Map home, TradingPost p, string line, Faction enemy = null)
        {
            if (home == null || p == null) return;
            var actors = new List<HistoryActor> { new HistoryActor(Refs.Of(p.people), HistoryRole.Did), new HistoryActor(Ref.Town(p.town), HistoryRole.About) };
            if (kind == TradingPostMath.KindRazed)
            {
                actors[0] = new HistoryActor(Refs.Of(p.people), HistoryRole.Against);
                actors.Insert(0, new HistoryActor(Refs.Colony(home), HistoryRole.Did));
            }
            if (enemy != null) actors.Add(new HistoryActor(Refs.Of(enemy), HistoryRole.Against));
            History.Add(kind, actors, Refs.Colony(home), TradingPostMath.Milestone(kind) ? HistoryMath.Milestone : HistoryMath.Everyday, line);
        }

        // ---------------- Sacked ----------------

        /// <summary>
        /// Raiders of a people at war with the post's own sacked it: it loses 30 of its fortune, or, when it can't take
        /// that (under 10 after it) and can come off the map, its people give it up (a ruin, its families on the road, a
        /// refugee family of theirs at the colony's gate).
        /// </summary>
        internal static void Sack(Map home, TradingPostHome ph, TradingPost p, Settlement t, Faction enemy, int now)
        {
            if (t == null || enemy == null) return;
            var rec = WorldLedger.TownRecord(t.ID);
            float prosperity = rec != null && rec.Seeded ? rec.prosperity : TradingPostMath.Start;
            p.sackedAt = now;
            if (TradingPostMath.SackGivesUp(prosperity) && SafeToClose(t))
            {
                Close(home, ph, p, t, PostEnd.Sacked, enemy);
                if (!ph.posts.Contains(p)) return;
                // It couldn't come off the map after all: it stands, battered.
                p.closing = -1;
            }
            if (rec != null)
            {
                rec.prosperity = TradingPostMath.AfterSack(prosperity);
                rec.movedAt = now;
                rec.cause = "CC_Post_Cause_Sacked".Translate(enemy.Name.Named("ENEMY")).Resolve();
                rec.causeDir = -1;
                rec.causeAt = now;
                rec.lowSince = RegionMath.LowSince(rec.prosperity, rec.lowSince, now);
                WorldLedger.TownChanged();
            }
            HistoryOf(TradingPostMath.KindSacked, home, p, TradingPostWords.HistorySacked(p, enemy), enemy);
            TradingPostWords.TellSacked(home, p, t, enemy);
        }

        // ---------------- Hooks ----------------

        /// <summary>
        /// A post the colony's people attacked and destroyed (the game's defeat of a town, Patch_TradingPostRazed): their
        /// people remember a post razed, a heavy wrong beside the game's own for a town destroyed; the history and the
        /// colony news have it.
        /// </summary>
        public static void Razed(int town, Faction people, string name)
        {
            var p = PostAt(town, out var home);
            if (p == null) return;
            if (!name.NullOrEmpty()) p.name = name;
            FactionMemory.Note(people ?? p.people, TradingPostMath.DeedRazed, p.name);
            End(home, Of(home), p, PostEnd.Razed, null, null);
        }

        /// <summary>How likely a people's trader caravan is to be the one that comes to a home: half again as likely while their post stands near it.</summary>
        public static float CaravanFactor(Faction f, Map home)
        {
            if (!LinksOn || f == null || home == null) return 1f;
            foreach (var p in PostsOf(home))
                if (p.people == f && p.closing < 0)
                    return TradingPostMath.Caravans(true);
            return 1f;
        }

        /// <summary>How likely a world event is to be the one that starts at a home: the bandit road twice as likely while a post stands near it.</summary>
        public static float EventFactor(WorldSituationDef d, Map home)
        {
            if (!LinksOn || d == null || home == null || d.defName != TradingPostMath.BanditRoad) return 1f;
            try
            {
                return TradingPostMath.EventFactor(d.defName, PostsOf(home).Count > 0);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trading posts: a world event's odds", e);
                return 1f;
            }
        }
    }
}
