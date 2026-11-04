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

        private static readonly HashSet<int> NoHomes = new HashSet<int>();

        /// <summary>
        /// A home's council, made when it first needs one (the seat takes an old save's); for a map that isn't a home, the
        /// seat's; with no home at all, the one its people carry.
        /// </summary>
        public ColonyState Colony(Map map)
        {
            colonies ??= new List<ColonyState>();
            // While a game loads or starts, the homes aren't known yet: the book is read, never rearranged.
            if (Scribe.mode != LoadSaveMode.Inactive || Current.ProgramState != ProgramState.Playing) return Settling(map);
            var homes = Homes.All;
            if (homes.Count == 0)
            {
                var carried = ColonyStateMath.Carried(colonies, NoHomes);
                if (carried == null)
                {
                    carried = new ColonyState();
                    colonies.Add(carried);
                }
                return carried;
            }
            int seat = Homes.KeyOf(Homes.Seat);
            int key = map != null && Homes.IsHome(map) ? Homes.KeyOf(map) : seat;
            // The one or two colonies of most games: a plain look first.
            for (int i = 0; i < colonies.Count; i++)
            {
                var c = colonies[i];
                if (c.home == key && c.lostAt == ColonyStateMath.NotLost) return c;
            }
            var found = ColonyStateMath.Find(colonies, key, seat, homes.Count == 1 ? new HashSet<int> { Homes.KeyOf(homes[0]) } : null);
            if (found != null) return found;
            var made = new ColonyState { home = key };
            colonies.Add(made);
            return made;
        }

        /// <summary>
        /// Before the game is under way: the council kept under the map's key, else the first whose home wasn't lost, else
        /// a new one with no home yet (the seat takes it once the homes are known).
        /// </summary>
        private ColonyState Settling(Map map)
        {
            int key = map?.Parent != null ? Homes.KeyOf(map) : HomesMath.None;
            for (int i = 0; i < colonies.Count; i++)
                if (key != HomesMath.None && colonies[i].home == key) return colonies[i];
            for (int i = 0; i < colonies.Count; i++)
                if (colonies[i].lostAt == ColonyStateMath.NotLost) return colonies[i];
            var made = new ColonyState();
            colonies.Add(made);
            return made;
        }

        /// <summary>The council of a colony by its key while that's a home, else null (not made).</summary>
        public ColonyState ColonyByKey(int key)
        {
            if (colonies == null) return null;
            for (int i = 0; i < colonies.Count; i++)
                if (colonies[i].home == key) return colonies[i];
            return null;
        }

        /// <summary>Homes founded or lost (Homes.Changed): a lost home's council is kept a while, or carried when no home is left.</summary>
        internal void HomesLost(IReadOnlyList<int> lost)
        {
            if (lost == null || lost.Count == 0 || colonies == null) return;
            var homes = new HashSet<int>(Homes.All.Select(Homes.KeyOf));
            int now = Find.TickManager?.TicksGame ?? 0;
            foreach (int key in lost)
            {
                var c = ColonyByKey(key);
                ColonyStateMath.Lost(colonies, key, now, homes);
                if (c != null) Safe("a colony's council, its home lost", () => c.HomeLost());
            }
        }

        /// <summary>Once a day: a home that came back is ours again, and councils of homes lost long enough ago are let go.</summary>
        private void ColoniesDaily()
        {
            if (colonies == null) return;
            var homes = new HashSet<int>(Homes.All.Select(Homes.KeyOf));
            int now = Find.TickManager.TicksGame;
            var gone = colonies.Where(c => ColonyStateMath.Drops(c.lostAt, now, homes.Count > 0)).ToList();
            ColonyStateMath.Upkeep(colonies, now, homes);
            foreach (var c in gone) Safe("a colony's council let go", () => c.HomeLost());
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

    /// <summary>Listens for homes founded and lost (Homes.Changed) for the council's book of colonies.</summary>
    [StaticConstructorOnStartup]
    public static class ColonyStateListener
    {
        static ColonyStateListener()
        {
            Homes.Changed += (founded, lost) => CouncilGameComponent.Instance?.HomesLost(lost);
        }
    }
}
