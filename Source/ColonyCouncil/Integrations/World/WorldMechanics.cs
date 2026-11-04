using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A settlement a people is building out in the world: where, whose, since when and when it is due.</summary>
    public sealed class BuildingSite
    {
        /// <summary>The site's world object id.</summary>
        public int id;
        public PlanetTile tile;
        /// <summary>The tile as a ref: where the history writes what happens there.</summary>
        public Ref place;
        public Faction people;
        public int started;
        /// <summary>When it is due to be finished (the tick).</summary>
        public int completes;

        public float Progress(int now) => WorldMath.Progress(started, completes, now);
    }

    public enum HoldingKind
    {
        /// <summary>A town that holds of the colony.</summary>
        Vassal,
        /// <summary>A camp of the colony's own people, out in the world.</summary>
        Outpost
    }

    /// <summary>One of the colony's holdings out in the world that isn't a home: a vassal town or an outpost.</summary>
    public sealed class Holding
    {
        public HoldingKind kind;
        public WorldObject thing;
        /// <summary>Its world object's id, and that id as a town: the history's place for it.</summary>
        public int id;
        public Ref place;
        public string name;
        public PlanetTile tile;
        /// <summary>Set by the adapter: the home it names itself (an outpost's delivery colony), by its key; else none.</summary>
        public int namedHome = HomesMath.None;
        /// <summary>The home it belongs to (the one it names while that is a home, else the nearest), as a ref and as the map.</summary>
        public Ref colony;
        public Map home;
        /// <summary>A vassal's own people (those who lived there before it was the colony's); null for an outpost.</summary>
        public Faction people;
        /// <summary>What an outpost does ("Mining", from its def); null for a vassal.</summary>
        public string what;
        /// <summary>How many are posted at an outpost; 0 for a vassal.</summary>
        public int posted;
    }

    /// <summary>A royal title someone holds: its word for them, the people who gave it, and how senior it is.</summary>
    public sealed class RoyalTitleView
    {
        public string label;
        public string defName;
        public Faction people;
        public int seniority;
    }

    /// <summary>
    /// One other mod's (or DLC's) world mechanics, as Rimfolk reads them: the small interface every adapter fills in what
    /// it can of. An adapter knows its own mod by its package id and its types by name, reaches them by reflection or by
    /// Harmony patches named the same way, and does nothing when its mod isn't there. <see cref="WorldMechanics"/> finds
    /// every adapter by its type, so the core never names a mod.
    /// </summary>
    public abstract class WorldAdapter
    {
        /// <summary>Its name, for the log.</summary>
        public abstract string Name { get; }

        /// <summary>Its mod is active and what it reads was found. Asked once a session.</summary>
        public abstract bool Ready();

        /// <summary>Who owns a tile by this mod's reckoning. False: not this mod's business; true with null: nobody, or contested.</summary>
        public virtual bool Owner(PlanetTile tile, out Faction owner)
        {
            owner = null;
            return false;
        }

        /// <summary>The settlements being built now.</summary>
        public virtual void Building(List<BuildingSite> into)
        {
        }

        /// <summary>
        /// The colony's holdings this mod keeps (vassals, outposts): what, where, whose people, what it names as its home.
        /// The home each belongs to is worked out by <see cref="WorldMechanics"/>.
        /// </summary>
        public virtual void Holdings(List<Holding> into)
        {
        }

        /// <summary>Ties this mod keeps between two parties, as views (<see cref="Tie.source"/> set). Vassals' fealty comes from the holdings.</summary>
        public virtual void Ties(List<Tie> into)
        {
        }

        /// <summary>The royal title someone holds, or null.</summary>
        public virtual RoyalTitleView Title(Pawn p) => null;
    }

    /// <summary>
    /// Other mods' world mechanics behind one facade (DESIGN.md, "Other mods' world mechanics"): who owns a tile
    /// (<see cref="TerritoryOwner"/>), the settlements being built (<see cref="SettlementsBeingBuilt"/>), the colony's
    /// vassals and holdings (<see cref="OurVassals"/>, <see cref="OurHoldings"/>), a person's royal title
    /// (<see cref="Title"/>), and the ties those mods keep, which the world ledger shows as views (<see cref="Ties"/>). The
    /// adapters' hooks write what those mods do to the world into the history through here (<see cref="Founded"/>,
    /// <see cref="Built"/>, <see cref="Razed"/>, <see cref="Taken"/>, <see cref="MadeVassal"/>, <see cref="Battle"/>,
    /// <see cref="Won"/>). Nothing here tells the player anything; with none of the mods every answer is empty.
    /// </summary>
    public static class WorldMechanics
    {
        private static List<WorldAdapter> found;
        private static List<WorldAdapter> ready;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The adapters whose mod is there (found by their type, asked once a session).</summary>
        public static IReadOnlyList<WorldAdapter> Ready
        {
            get
            {
                if (ready != null) return ready;
                found = new List<WorldAdapter>();
                ready = new List<WorldAdapter>();
                try
                {
                    foreach (var t in typeof(WorldAdapter).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(WorldAdapter).IsAssignableFrom(t)).OrderBy(t => t.Name))
                        found.Add((WorldAdapter)Activator.CreateInstance(t));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("world mechanics: the adapters", e);
                }
                foreach (var a in found)
                {
                    try
                    {
                        if (a.Ready()) ready.Add(a);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("world mechanics: " + a.Name, e);
                    }
                }
                return ready;
            }
        }

        // ---------------- Reading ----------------

        /// <summary>The people who own a tile, by the first adapter that keeps territories; null for nobody, a contested tile, or no such mod.</summary>
        public static Faction TerritoryOwner(PlanetTile tile)
        {
            if (!tile.Valid || Current.Game == null) return null;
            foreach (var a in Ready)
            {
                try
                {
                    if (a.Owner(tile, out var owner)) return owner;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("world mechanics: who owns a tile (" + a.Name + ")", e);
                }
            }
            return null;
        }

        private static Game keptGame;
        private static int buildingAt = Fresh.Never;
        private static List<BuildingSite> building = new List<BuildingSite>();
        private static int holdingsAt = Fresh.Never;
        private static List<Holding> holdings = new List<Holding>();

        private static void CheckGame()
        {
            if (keptGame == Current.Game) return;
            keptGame = Current.Game;
            buildingAt = Fresh.Never;
            holdingsAt = Fresh.Never;
            building = new List<BuildingSite>();
            holdings = new List<Holding>();
            razed.Clear();
        }

        /// <summary>The settlements being built now (read again each 250-tick stretch; a shared list, not to be changed).</summary>
        public static IReadOnlyList<BuildingSite> SettlementsBeingBuilt()
        {
            if (Current.Game == null) return new List<BuildingSite>();
            CheckGame();
            int now = Now;
            if (Fresh.Holds(buildingAt, now, Sched.RareTicks)) return building;
            buildingAt = now;
            var list = new List<BuildingSite>();
            foreach (var a in Ready)
            {
                try
                {
                    a.Building(list);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("world mechanics: settlements being built (" + a.Name + ")", e);
                }
            }
            building = list;
            return building;
        }

        /// <summary>Every holding of the colony's (read again each 250-tick stretch; a shared list, not to be changed).</summary>
        private static List<Holding> AllHoldings()
        {
            if (Current.Game == null) return new List<Holding>();
            CheckGame();
            int now = Now;
            if (Fresh.Holds(holdingsAt, now, Sched.RareTicks)) return holdings;
            holdingsAt = now;
            var list = new List<Holding>();
            foreach (var a in Ready)
            {
                try
                {
                    a.Holdings(list);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("world mechanics: the colony's holdings (" + a.Name + ")", e);
                }
            }
            var homes = Homes.All;
            var keys = homes.Select(Homes.KeyOf).ToList();
            foreach (var h in list)
            {
                h.place = Ref.Town(h.id);
                int i = WorldMath.HomeFor(h.namedHome, keys, Distances(homes, h.tile));
                h.home = i >= 0 ? homes[i] : null;
                h.colony = Refs.Colony(h.home);
            }
            holdings = list;
            return holdings;
        }

        /// <summary>The colony's vassal towns: those of this home (its own, or the nearest home's), or all with no home given.</summary>
        public static List<Holding> OurVassals(Map colony = null) => AllHoldings().Where(h => h.kind == HoldingKind.Vassal && (colony == null || h.home == colony)).ToList();

        /// <summary>The colony's holdings out in the world that aren't homes (vassals and outposts): of this home, or all with no home given.</summary>
        public static List<Holding> OurHoldings(Map colony = null) => AllHoldings().Where(h => colony == null || h.home == colony).ToList();

        /// <summary>The royal title someone holds (the first adapter that knows of one), or null.</summary>
        public static RoyalTitleView Title(Pawn p)
        {
            if (p == null) return null;
            foreach (var a in Ready)
            {
                try
                {
                    var t = a.Title(p);
                    if (t != null) return t;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("world mechanics: a title (" + a.Name + ")", e);
                }
            }
            return null;
        }

        /// <summary>
        /// The ties other mods keep, as views for the world ledger (WorldLedger.Today): each vassal's fealty to the colony
        /// it belongs to (since it was made, when the history knows), and what each adapter keeps of its own.
        /// </summary>
        public static void Ties(List<Tie> into)
        {
            if (into == null || Current.Game == null) return;
            foreach (var h in OurVassals())
            {
                if (h.colony.IsNone) continue;
                var made = History.Latest(WorldMath.Vassal, place: h.place);
                into.Add(new Tie
                {
                    kind = Tie.Kind.Fealty, from = h.place, to = h.colony, terms = new TieTerms { key = "vassal", about = Refs.Of(h.people) },
                    since = made?.tick ?? Tie.Never, until = Tie.Never, state = Tie.State.Open, source = "world:vassal"
                });
            }
            foreach (var a in Ready)
            {
                try
                {
                    a.Ties(into);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("world mechanics: ties (" + a.Name + ")", e);
                }
            }
        }

        // ---------------- Where ----------------

        /// <summary>How far each home is from a tile (a home on another layer of the planet counts as out of reach).</summary>
        private static List<float> Distances(IReadOnlyList<Map> homes, PlanetTile tile)
        {
            var list = new List<float>(homes.Count);
            foreach (var m in homes)
            {
                var t = m?.Tile ?? PlanetTile.Invalid;
                list.Add(tile.Valid && t.Valid && t.Layer == tile.Layer ? Find.WorldGrid.ApproxDistanceInTiles(t, tile) : float.MaxValue);
            }
            return list;
        }

        /// <summary>A tile within the region map's reach of one of the colony's homes.</summary>
        public static bool InReach(PlanetTile tile)
        {
            if (!tile.Valid || Find.WorldGrid == null) return false;
            return WorldMath.InReach(Distances(Homes.All, tile), ColonyCouncilMod.Settings?.regionMapRadius ?? 35);
        }

        /// <summary>The home nearest a tile (on its layer), else the seat.</summary>
        public static Map HomeNear(PlanetTile tile)
        {
            var homes = Homes.All;
            int i = WorldMath.HomeFor(HomesMath.None, homes.Select(Homes.KeyOf).ToList(), Distances(homes, tile));
            return i >= 0 && tile.Valid ? homes[i] : Homes.Seat;
        }

        // ---------------- Writing what they do into the history ----------------

        private static void Write(string kind, List<HistoryActor> actors, Ref place, bool ours, string name = null)
        {
            try
            {
                History.Add(kind, actors, place, WorldMath.Weight(ours), values: name.NullOrEmpty() ? null : new List<string> { name });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world mechanics: the history's " + kind, e);
            }
        }

        /// <summary>
        /// A people began building a settlement there (kept when it is within reach of a home), and the region gives it
        /// faces (World/Region/RegionFaces.cs): the site's world object id and its start and due ticks when the mod has them.
        /// </summary>
        public static void Founded(Faction people, PlanetTile tile, int site = -1, int started = -1, int completes = -1)
        {
            if (people == null || !tile.Valid || !WorldMath.Records(InReach(tile), false)) return;
            Write(WorldMath.Founded, new List<HistoryActor> { new HistoryActor(Refs.Of(people), HistoryRole.Did) }, Refs.Of(tile), false);
            RegionFaces.Begun(people, tile, site, started, completes);
        }

        /// <summary>
        /// A settlement a people built was finished: the new town, with the tile it was built on and the founder who led
        /// its settlers when the region knows them (kept within reach of a home); the region gives it its faces.
        /// </summary>
        public static void Built(Faction people, Settlement town, int site = -1)
        {
            if (people == null || town == null || !WorldMath.Records(InReach(town.Tile), false)) return;
            var actors = new List<HistoryActor> { new HistoryActor(Refs.Of(people), HistoryRole.Did), new HistoryActor(Refs.Of(town.Tile), HistoryRole.About) };
            var founder = RegionFaces.FounderAt(site, town.Tile);
            if (!founder.IsNone) actors.Insert(1, new HistoryActor(founder, HistoryRole.Did));
            Write(WorldMath.Built, actors, Refs.Of(town), false, town.HasName ? town.Name : null);
            RegionFaces.Built(people, town, site);
        }

        /// <summary>A settlement being built, attacked by the colony's people and destroyed: the colony nearest it did it.</summary>
        public static void Razed(Faction people, PlanetTile tile, int siteId)
        {
            if (people == null || !tile.Valid) return;
            CheckGame();
            if (!razed.Add(siteId)) return;
            var actors = new List<HistoryActor> { new HistoryActor(Refs.Colony(HomeNear(tile)), HistoryRole.Did), new HistoryActor(Refs.Of(people), HistoryRole.Against) };
            Write(WorldMath.Razed, actors, Refs.Of(tile), true);
        }

        /// <summary>Sites already written as razed this session (the mod asks whether one is beaten more than once before it goes).</summary>
        private static readonly HashSet<int> razed = new HashSet<int>();

        /// <summary>
        /// A town taken: <paramref name="by"/> took it from <paramref name="from"/> (or from the colony, a vassal of ours:
        /// <paramref name="fromUs"/>). The town as it is now is the place; the one it was (another world object, when the
        /// mod made it afresh) is about. Kept when it is ours or within reach of a home.
        /// </summary>
        public static void Taken(Faction by, Faction from, bool fromUs, Settlement town, int formerId, string name)
        {
            if (by == null || town == null || !fromUs && from == null) return;
            if (!WorldMath.Records(InReach(town.Tile), fromUs)) return;
            var loser = fromUs ? Refs.Colony(HomeNear(town.Tile)) : Refs.Of(from);
            var actors = new List<HistoryActor> { new HistoryActor(Refs.Of(by), HistoryRole.Did), new HistoryActor(loser, HistoryRole.Against) };
            if (formerId >= 0 && formerId != town.ID) actors.Add(new HistoryActor(Ref.Town(formerId), HistoryRole.About));
            Write(WorldMath.Taken, actors, Refs.Of(town), fromUs, name ?? (town.HasName ? town.Name : null));
            // The town starts again under its new people (World/Region/RegionFortunes.cs).
            try
            {
                RegionFortunes.Taken(town, by);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world mechanics: a town taken, its fortune", e);
            }
        }

        /// <summary>A town made a vassal of the colony nearest it: the colony did, its people were against, the town it was is about.</summary>
        public static void MadeVassal(Faction from, WorldObject vassal, int formerId, string name)
        {
            if (vassal == null) return;
            var actors = new List<HistoryActor> { new HistoryActor(Refs.Colony(HomeNear(vassal.Tile)), HistoryRole.Did) };
            if (from != null && !from.IsPlayer) actors.Add(new HistoryActor(Refs.Of(from), HistoryRole.Against));
            if (formerId >= 0 && formerId != vassal.ID) actors.Add(new HistoryActor(Ref.Town(formerId), HistoryRole.About));
            Write(WorldMath.Vassal, actors, Ref.Town(vassal.ID), true, name);
        }

        /// <summary>Two peoples came to fight each other on one of the colony's homes. Both are "with" until one is known to have won.</summary>
        public static void Battle(Faction a, Faction b, Map home)
        {
            if (a == null || b == null || a == b || !Homes.IsHome(home)) return;
            var actors = new List<HistoryActor> { new HistoryActor(Refs.Of(a), HistoryRole.With), new HistoryActor(Refs.Of(b), HistoryRole.With) };
            Write(WorldMath.Battle, actors, Refs.Colony(home), true);
        }

        /// <summary>
        /// One of them beat the other there: the open battle between them (begun in the last few days) gets its winner and
        /// loser; one decided already stays as it was; with none written (begun before), the battle is written now, decided.
        /// </summary>
        public static void Won(Faction winner, Faction loser, Map home)
        {
            if (winner == null || loser == null || winner == loser || !Homes.IsHome(home)) return;
            var book = WorldLedger.Instance?.Book;
            if (book == null) return;
            try
            {
                Ref w = Refs.Of(winner), l = Refs.Of(loser), place = Refs.Colony(home);
                int now = Now;
                var recent = History.At(place, new[] { WorldMath.Battle }, now - WorldMath.BattleDays * HistoryMath.DayTicks);
                var open = WorldMath.OpenBattle(recent, w, l, now);
                if (open != null)
                {
                    WorldMath.Decide(book, open, w, l);
                    return;
                }
                if (WorldMath.DecidedBattle(recent, w, l, now) != null) return;
                Write(WorldMath.Battle, new List<HistoryActor> { new HistoryActor(w, HistoryRole.Did), new HistoryActor(l, HistoryRole.Against) }, place, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world mechanics: a battle won", e);
            }
        }
    }
}
