using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What belongs to one colony's council: its offices and election, its decrees, its ambition and meeting, and its
    /// dilemmas with the traditions they made. One per colony, known by the colony's key (<see cref="Homes.KeyOf"/>),
    /// kept in the council component (<see cref="CouncilGameComponent.colonies"/>) and saved with it, since the council
    /// already runs everything it holds and the trackers stay what they were. <see cref="For"/> gives a home's (the
    /// seat's for a map that isn't a home), made when a home first needs one; <see cref="Of"/> someone's. An old save's
    /// single state is the seat's. A home that stops being ours keeps its state a while, then lets it go
    /// (<see cref="ColonyStateMath"/>); with no home left, its people carry it to the next home they found, and a new seat
    /// that is the only home takes the one just left (people who moved in one go). What stays shared (wishes, trust, the
    /// chronicle, names, the social web) is in the component itself. DESIGN.md, "Many homes".
    /// </summary>
    public class ColonyState : IExposable, IColonyEntry
    {
        /// <summary>The colony's key, or none while it has no home (an old save's, not yet placed; or carried by people with no home).</summary>
        public int home = HomesMath.None;
        /// <summary>When its home stopped being ours; -1 while it is.</summary>
        public int lostAt = ColonyStateMath.NotLost;

        public OfficeTracker offices = new OfficeTracker();
        public DecreeTracker decrees = new DecreeTracker();
        public AmbitionTracker ambitions = new AmbitionTracker();
        public DilemmaTracker dilemmas = new DilemmaTracker();

        public int Home
        {
            get => home;
            set => home = value;
        }

        public int LostAt
        {
            get => lostAt;
            set => lostAt = value;
        }

        public ColonyState()
        {
            Bind();
        }

        /// <summary>Each tracker knows the colony it belongs to.</summary>
        internal void Bind()
        {
            offices ??= new OfficeTracker();
            decrees ??= new DecreeTracker();
            ambitions ??= new AmbitionTracker();
            dilemmas ??= new DilemmaTracker();
            offices.colony = this;
            decrees.colony = this;
            ambitions.colony = this;
            dilemmas.colony = this;
        }

        /// <summary>The colony's home while it's ours; null while it has none.</summary>
        public Map Map => home == HomesMath.None ? null : Homes.MapOf(home);

        /// <summary>Its home on the ground; null for one in space, where nobody walks in (as HappeningTracker.HomeMap is for the seat).</summary>
        public Map Ground
        {
            get
            {
                var m = Map;
                return MapFacts.InVacuum(m) ? null : m;
            }
        }

        /// <summary>
        /// Someone belongs to this colony (<see cref="Homes.Of"/>). With no home at all, everyone belongs to the state their
        /// people carry; a colony whose home is lost has nobody.
        /// </summary>
        public bool Has(Pawn p)
        {
            if (p == null) return false;
            // One home (most games): everyone's is that one, without asking whose they are.
            var homes = Homes.All;
            return homes.Count == 1 ? Map == homes[0] : Homes.Of(p) == Map;
        }

        /// <summary>The settlement's name, for texts once there's more than one colony.</summary>
        public string Name => Homes.Name(Map);

        /// <summary>
        /// Its home is no longer ours (or its council is let go): a meeting called and an election held there, and an
        /// ambition being worked at there, end quietly.
        /// </summary>
        internal void HomeLost()
        {
            ambitions.HomeLost();
            offices.HomeLost();
        }

        // ---------------- Which colony ----------------

        /// <summary>A home's council (the seat's for a map that isn't a home; the one its people carry with no home at all). Null without a game.</summary>
        public static ColonyState For(Map map) => CouncilGameComponent.Instance?.Colony(map);

        /// <summary>The council of the colony someone belongs to (<see cref="Homes.Of"/>).</summary>
        public static ColonyState Of(Pawn p)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return null;
            // One home (most games): everyone's is that one, without asking whose they are.
            var homes = Homes.All;
            if (homes.Count == 1) return comp.Colony(homes[0]);
            return comp.Colony(p == null ? null : Homes.Of(p));
        }

        /// <summary>The council of the seat (<see cref="Homes.Seat"/>).</summary>
        public static ColonyState Seat => For(Homes.Seat);

        /// <summary>The colony in view: the map on screen when it's a home, else the seat. What the council's pages show.</summary>
        public static ColonyState InView => For(Find.CurrentMap);

        /// <summary>
        /// The councils that run this hour: each home's, in the order the homes were founded; with no home, the one its
        /// people carry. A new list each time.
        /// </summary>
        public static List<ColonyState> Ticking()
        {
            var comp = CouncilGameComponent.Instance;
            var list = new List<ColonyState>();
            if (comp == null) return list;
            var homes = Homes.All;
            if (homes.Count == 0) list.Add(comp.Colony(null));
            else
                foreach (var m in homes)
                    list.Add(comp.Colony(m));
            return list;
        }

        /// <summary>More than one colony: texts and pages name the settlement.</summary>
        public static bool Several => Homes.All.Count > 1;

        // ---------------- Saving ----------------

        public void ExposeData()
        {
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref lostAt, "lostAt", ColonyStateMath.NotLost);
            Scribe_Deep.Look(ref offices, "offices");
            Scribe_Deep.Look(ref decrees, "decrees");
            Scribe_Deep.Look(ref ambitions, "ambitions");
            Scribe_Deep.Look(ref dilemmas, "dilemmas");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) Bind();
        }

        /// <summary>An old save's council, from before there was one per colony: everything it has, for the seat (placed on first use).</summary>
        internal static ColonyState FromOldSave(OfficeTracker offices, DecreeTracker decrees, AmbitionTracker ambitions, DilemmaTracker dilemmas)
        {
            var c = new ColonyState
            {
                offices = offices ?? new OfficeTracker(),
                decrees = decrees ?? new DecreeTracker(),
                ambitions = ambitions ?? new AmbitionTracker(),
                dilemmas = dilemmas ?? new DilemmaTracker()
            };
            c.Bind();
            return c;
        }
    }

    /// <summary>The council component's side of <see cref="ColonyState"/>: the book of colonies, found, saved, and looked after.</summary>
    public partial class CouncilGameComponent
    {
        /// <summary>One council per colony (ColonyState); an old save's single one is the seat's.</summary>
        public List<ColonyState> colonies = new List<ColonyState>();

        /// <summary>The seat's offices: for what the colony as a whole has. Per colony: <see cref="ColonyState.For"/> or <see cref="ColonyState.Of"/>.</summary>
        public OfficeTracker offices => SeatColony.offices;

        /// <summary>The seat's decrees. Per colony: <see cref="ColonyState.For"/> or <see cref="ColonyState.Of"/>.</summary>
        public DecreeTracker decrees => SeatColony.decrees;

        /// <summary>The seat's ambition and meeting. Per colony: <see cref="ColonyState.For"/>.</summary>
        public AmbitionTracker ambitions => SeatColony.ambitions;

        /// <summary>The seat's dilemmas and traditions. Per colony: <see cref="ColonyState.For"/>.</summary>
        public DilemmaTracker dilemmas => SeatColony.dilemmas;

        /// <summary>The seat's council.</summary>
        public ColonyState SeatColony => Colony(Homes.Seat);

        /// <summary>
        /// A home's council, made when it first needs one (the seat takes an old save's); for a map that isn't a home, the
        /// seat's; with no home at all, the one its people carry.
        /// </summary>
        public ColonyState Colony(Map map)
        {
            colonies ??= new List<ColonyState>();
            return ColonyBook.Entry(colonies, map, () => new ColonyState());
        }

        /// <summary>The council of a colony by its key while that's a home, else null (not made).</summary>
        public ColonyState ColonyByKey(int key) => ColonyBook.ByKey(colonies, key);

        /// <summary>Homes founded or lost (Homes.Changed): a lost home's council is kept a while, or carried when no home is left.</summary>
        internal void HomesLost(IReadOnlyList<int> lost)
        {
            ColonyBook.Lost(colonies, lost, c => Safe("a colony's council, its home lost", () => c.HomeLost()));
            // The other books kept here per colony: the herd's year, the beast that stays, the encounters.
            ColonyBook.Lost(herds, lost, null);
            ColonyBook.Lost(beasts, lost, null);
            encounters?.HomesLost(lost);
        }

        /// <summary>Once a day: a home that came back is ours again, and councils of homes lost long enough ago are let go.</summary>
        private void ColoniesDaily()
        {
            ColonyBook.Daily(colonies, c => Safe("a colony's council let go", () => c.HomeLost()));
            ColonyBook.Daily(herds, null);
            ColonyBook.Daily(beasts, null);
            encounters?.Daily();
        }

        /// <summary>Saves the book; an old save's trackers become the seat's council.</summary>
        private void ExposeColonies()
        {
            if (Scribe.mode == LoadSaveMode.Saving) colonies?.RemoveAll(c => c == null);
            Scribe_Collections.Look(ref colonies, "colonies", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                // A save from before there was one council per colony: what it had is the seat's (Lean/ColonyStateMath.cs).
                OfficeTracker oldOffices = null;
                DecreeTracker oldDecrees = null;
                AmbitionTracker oldAmbitions = null;
                DilemmaTracker oldDilemmas = null;
                Scribe_Deep.Look(ref oldOffices, "offices");
                Scribe_Deep.Look(ref oldDecrees, "decrees");
                Scribe_Deep.Look(ref oldAmbitions, "ambitions");
                Scribe_Deep.Look(ref oldDilemmas, "dilemmas");
                if (colonies == null) colonies = new List<ColonyState> { ColonyState.FromOldSave(oldOffices, oldDecrees, oldAmbitions, oldDilemmas) };
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                colonies ??= new List<ColonyState>();
                colonies.RemoveAll(c => c == null);
                foreach (var c in colonies) c.Bind();
            }
        }
    }

    /// <summary>
    /// A book of colonies: one entry per colony (<see cref="IColonyEntry"/>: the council's <see cref="ColonyState"/>, the
    /// calendar's <see cref="ColonyCalendar"/>), found by the home's key, by the rules of <see cref="ColonyStateMath"/>: a
    /// home's own entry, made when it first needs one; the seat's for a map that isn't a home; an old save's single entry
    /// placed at the seat; with no home at all, the one its people carry; a lost home's kept a while, then let go.
    /// </summary>
    public static class ColonyBook
    {
        private static readonly HashSet<int> NoHomes = new HashSet<int>();

        /// <summary>A home's entry in <paramref name="book"/>, made with <paramref name="make"/> when it has none.</summary>
        public static T Entry<T>(List<T> book, Map map, Func<T> make) where T : class, IColonyEntry
        {
            // While a game loads or starts, the homes aren't known yet: the book is read, never rearranged.
            if (Scribe.mode != LoadSaveMode.Inactive || Current.ProgramState != ProgramState.Playing) return Settling(book, map, make);
            var homes = Homes.All;
            if (homes.Count == 0)
            {
                var carried = ColonyStateMath.Carried(book, NoHomes);
                if (carried == null)
                {
                    carried = make();
                    book.Add(carried);
                }
                return carried;
            }
            int seat = Homes.KeyOf(Homes.Seat);
            int key = map != null && Homes.IsHome(map) ? Homes.KeyOf(map) : seat;
            // The one or two colonies of most games: a plain look first.
            for (int i = 0; i < book.Count; i++)
            {
                var c = book[i];
                if (c != null && c.Home == key && c.LostAt == ColonyStateMath.NotLost) return c;
            }
            var found = ColonyStateMath.Find(book, key, seat, homes.Count == 1 ? new HashSet<int> { Homes.KeyOf(homes[0]) } : null);
            if (found != null) return found;
            var made = make();
            made.Home = key;
            book.Add(made);
            return made;
        }

        /// <summary>
        /// Before the game is under way: the entry kept under the map's key, else the first whose home wasn't lost, else
        /// a new one with no home yet (the seat takes it once the homes are known).
        /// </summary>
        private static T Settling<T>(List<T> book, Map map, Func<T> make) where T : class, IColonyEntry
        {
            int key = map?.Parent != null ? Homes.KeyOf(map) : HomesMath.None;
            for (int i = 0; i < book.Count; i++)
                if (key != HomesMath.None && book[i] != null && book[i].Home == key) return book[i];
            for (int i = 0; i < book.Count; i++)
                if (book[i] != null && book[i].LostAt == ColonyStateMath.NotLost) return book[i];
            var made = make();
            book.Add(made);
            return made;
        }

        /// <summary>The entry of a colony by its key, else null (not made).</summary>
        public static T ByKey<T>(List<T> book, int key) where T : class, IColonyEntry
        {
            if (book == null) return null;
            for (int i = 0; i < book.Count; i++)
                if (book[i] != null && book[i].Home == key) return book[i];
            return null;
        }

        /// <summary>Homes lost (Homes.Changed): each one's entry is kept a while, or carried when no home is left; <paramref name="lost"/> hears of each.</summary>
        public static void Lost<T>(List<T> book, IReadOnlyList<int> keys, Action<T> lost) where T : class, IColonyEntry
        {
            if (keys == null || keys.Count == 0 || book == null) return;
            var homes = new HashSet<int>(Homes.All.Select(Homes.KeyOf));
            int now = Find.TickManager?.TicksGame ?? 0;
            foreach (int key in keys)
            {
                var c = ByKey(book, key);
                ColonyStateMath.Lost(book, key, now, homes);
                if (c != null) lost?.Invoke(c);
            }
        }

        /// <summary>Once a day: a home that came back is ours again, and entries of homes lost long enough ago are let go (<paramref name="gone"/> hears of each).</summary>
        public static void Daily<T>(List<T> book, Action<T> gone) where T : class, IColonyEntry
        {
            if (book == null) return;
            var homes = new HashSet<int>(Homes.All.Select(Homes.KeyOf));
            int now = Find.TickManager.TicksGame;
            var going = book.Where(c => c != null && ColonyStateMath.Drops(c.LostAt, now, homes.Count > 0)).ToList();
            ColonyStateMath.Upkeep(book, now, homes);
            foreach (var c in going) gone?.Invoke(c);
        }
    }

    /// <summary>Listens for homes founded and lost (Homes.Changed) for the council's book of colonies.</summary>
    [StaticConstructorOnStartup]
    public static class ColonyStateListener
    {
        static ColonyStateListener()
        {
            Homes.Changed += (founded, lost) =>
            {
                CouncilGameComponent.Instance?.HomesLost(lost);
                CalendarTracker.Instance?.HomesLost(lost);
                CampTracker.Instance?.HomesLost(lost);
                RegionBook.Instance?.HomesLost(lost);
                DiplomacyBook.Instance?.HomesLost(lost);
            };
        }
    }
}
