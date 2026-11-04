using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Which home someone lives at: the last home they lived on for a day, and the home they're on now and since when.</summary>
    public class HomeTie : IExposable
    {
        public Pawn pawn;
        /// <summary>The last home they lived on (a day or more without a break): its settlement's id.</summary>
        public int last = HomesMath.None;
        /// <summary>The home they've been on since the last time they left one, and from when.</summary>
        public int at = HomesMath.None;
        public int since;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref last, "last", HomesMath.None);
            Scribe_Values.Look(ref at, "at", HomesMath.None);
            Scribe_Values.Look(ref since, "since");
        }
    }

    /// <summary>
    /// What "a colony" is for Rimfolk: a player home map, known by its settlement's id (<c>map.Parent.ID</c>, which
    /// outlives the map's own). <see cref="All"/> lists the homes, <see cref="Seat"/> is the one the council sits at (the
    /// first founded, unless a seat is stored), <see cref="Of"/> says which home someone belongs to, <see cref="People"/>
    /// who belongs to a home, <see cref="Name"/> what the settlement is called, and <see cref="Changed"/> tells of a home
    /// founded or lost. With one colony every answer is that colony. The rules are in <see cref="HomesMath"/>; the little
    /// this keeps (each colonist's last home, the stored seat, the homes at the last look) is saved here, and looked
    /// after in the council's hour (<see cref="Hourly"/>).
    /// </summary>
    public class Homes : GameComponent
    {
        /// <summary>The seat the player chose (a settlement's id), or none: then the first home founded.</summary>
        public int seat = HomesMath.None;
        /// <summary>One per colonist who has been on a home: the last home they lived on.</summary>
        private List<HomeTie> ties = new List<HomeTie>();
        /// <summary>The homes at the last hourly look, to tell one founded or lost.</summary>
        private List<int> known = new List<int>();
        private bool looked;

        [Unsaved] private Dictionary<Pawn, HomeTie> byPawn;

        /// <summary>
        /// A home was founded or lost (abandoned, taken, handed over) since the last hourly look: the settlements' ids,
        /// founded first, then lost. For the systems that keep something per colony.
        /// </summary>
        public static event Action<IReadOnlyList<int>, IReadOnlyList<int>> Changed;

        private static Homes cached;
        private static Game cachedGame;

        public static Homes Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<Homes>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public Homes(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            byPawn = null;
            Clear();
        }

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ---------------- The homes ----------------

        private static Game game;
        private static int allAt = Fresh.Never;
        private static List<Map> all = new List<Map>();
        private static List<int> order = new List<int>();
        private static HashSet<int> keys = new HashSet<int>();
        private static Dictionary<int, Map> byKey = new Dictionary<int, Map>();

        /// <summary>Forget what was worked out (a new game, a load).</summary>
        public static void Clear()
        {
            allAt = Fresh.Never;
            all = new List<Map>();
            order = new List<int>();
            keys = new HashSet<int>();
            byKey = new Dictionary<int, Map>();
            busiestAt = Fresh.Never;
            busiest = null;
        }

        private static void CheckGame()
        {
            if (Current.Game == game) return;
            game = Current.Game;
            Clear();
        }

        /// <summary>
        /// The homes, worked out again each 250-tick stretch (as the busiest home always was), or at once when one of them
        /// is gone. A new list each time, so a loop over the old one is safe.
        /// </summary>
        private static void Refresh()
        {
            CheckGame();
            int now = Now;
            if (Fresh.Holds(allAt, now, Sched.RareTicks) && !AnyGone()) return;
            allAt = now;
            var list = new List<Map>();
            var maps = Current.Game?.Maps;
            if (maps != null)
                for (int i = 0; i < maps.Count; i++)
                    if (maps[i] != null && maps[i].IsPlayerHome) list.Add(maps[i]);
            list.Sort((a, b) => HomesMath.Compare(KeyOf(a), !MapFacts.InVacuum(a), KeyOf(b), !MapFacts.InVacuum(b)));
            all = list;
            order = list.Select(KeyOf).ToList();
            keys = new HashSet<int>(order);
            byKey = new Dictionary<int, Map>();
            for (int i = 0; i < list.Count; i++) byKey[order[i]] = list[i];
        }

        private static bool AnyGone()
        {
            var maps = Current.Game?.Maps;
            for (int i = 0; i < all.Count; i++)
                if (maps == null || !maps.Contains(all[i])) return true;
            return false;
        }

        /// <summary>The player's homes in the order they were founded, those on the ground before those in orbit.</summary>
        public static IReadOnlyList<Map> All
        {
            get
            {
                Refresh();
                return all;
            }
        }

        /// <summary>A home's key: its settlement's id (a home with no settlement, never expected, gets a negative one of its own).</summary>
        public static int KeyOf(Map map) => map == null ? HomesMath.None : map.Parent != null ? map.Parent.ID : -2 - map.uniqueID;

        /// <summary>The home with this key, while it's still a home.</summary>
        public static Map MapOf(int key)
        {
            Refresh();
            return key != HomesMath.None && byKey.TryGetValue(key, out var m) ? m : null;
        }

        /// <summary>One of the player's homes.</summary>
        public static bool IsHome(Map map)
        {
            if (map == null) return false;
            Refresh();
            return all.Contains(map);
        }

        private static int KeyIfHome(Map map) => IsHome(map) ? KeyOf(map) : HomesMath.None;

        private static int SeatKey
        {
            get
            {
                Refresh();
                return HomesMath.Seat(order, Instance?.seat ?? HomesMath.None);
            }
        }

        /// <summary>
        /// The colony the council sits at: the first home founded (on the ground before orbit), unless a seat is stored
        /// and still a home. It doesn't move as people do. Null without a home.
        /// </summary>
        public static Map Seat => MapOf(SeatKey);

        /// <summary>Stores the seat (no button yet); null or a map that isn't a home goes back to the first founded.</summary>
        public static void SetSeat(Map map)
        {
            var h = Instance;
            if (h != null) h.seat = KeyIfHome(map);
        }

        // ---------------- Whose home ----------------

        /// <summary>
        /// The colony someone belongs to: the home they're on now (held there too, as in a casket); else the home where
        /// they own a bed; else the last home they lived on for a day; else the seat. Null without a home.
        /// </summary>
        public static Map Of(Pawn p)
        {
            if (p == null) return null;
            Refresh();
            if (all.Count == 0) return null;
            int here = KeyIfHome(p.MapHeld);
            int bed = KeyIfHome(p.ownership?.OwnedBed?.MapHeld);
            int last = Instance?.TieOf(p, false)?.last ?? HomesMath.None;
            return MapOf(HomesMath.Of(here, bed, last, SeatKey, keys));
        }

        /// <summary>The last home someone lived on for a day without a break, while it's still a home; null if none.</summary>
        public static Map LastHome(Pawn p)
        {
            if (p == null) return null;
            Refresh();
            var t = Instance?.TieOf(p, false);
            return t == null ? null : MapOf(t.last);
        }

        /// <summary>
        /// Everyone who may be one of the colony's free people, wherever they are: on a map, in a caravan or a transporter
        /// in flight, or where other mods keep them (an outpost, a vehicle). Not yet sorted by home.
        /// </summary>
        public static HashSet<Pawn> FreeColonistsAnywhere()
        {
            var found = new HashSet<Pawn>();
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoLodgers.ToList()) found.Add(p);
            try
            {
                foreach (var p in ColonyCouncil.People.OffMapColonists()) found.Add(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("homes: people off the map", e);
            }
            found.Remove(null);
            return found;
        }

        /// <summary>
        /// The free colonists who belong to this home (<see cref="Of"/>): those living here and those of its people who
        /// are away, by ID. A meeting's members are those of them with a say (MeetingAttendance.Members).
        /// </summary>
        public static List<Pawn> People(Map map)
        {
            var list = new List<Pawn>();
            if (map == null) return list;
            foreach (var p in FreeColonistsAnywhere())
                if (p.IsFreeColonist && ColonyCouncil.People.StillOurs(p) && Of(p) == map) list.Add(p);
            list.Sort((a, b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
            return list;
        }

        /// <summary>The settlement's name, for texts once there's more than one colony; the colony's own name without one.</summary>
        public static string Name(Map map)
        {
            var parent = map?.Parent;
            if (parent is Settlement s && s.HasName) return s.Name;
            if (parent != null) return parent.LabelCap;
            return Faction.OfPlayerSilentFail?.Name ?? "";
        }

        /// <summary>The colony a memo belongs to, made on this map: that map while it's a home, else the seat.</summary>
        public static int KeyForMemo(Map map) => IsHome(map) ? KeyOf(map) : SeatKey;

        /// <summary>Where a memo's follow-up goes: its colony while that's still a home, else the seat (an old memo has none).</summary>
        public static Map ForMemo(int key)
        {
            Refresh();
            return MapOf(HomesMath.Deliver(key, keys, SeatKey));
        }

        // ---------------- Where most people are ----------------

        private static int busiestAt = Fresh.Never;
        private static Map busiest;

        /// <summary>
        /// Where most of the colony's people are right now: the home with the most free colonists on it (on the ground
        /// before in orbit; the first such in the game's order), as of this 250-tick stretch. It moves as people do, so it
        /// is only for what follows people about (a social event between two who are there); "the colony" is
        /// <see cref="Seat"/>.
        /// </summary>
        public static Map Busiest
        {
            get
            {
                int now = Now;
                CheckGame();
                if (!Fresh.Holds(busiestAt, now, Sched.RareTicks) || (busiest != null && !Find.Maps.Contains(busiest)))
                {
                    busiestAt = now;
                    busiest = null;
                    int most = -1;
                    bool ground = false;
                    var maps = Find.Maps;
                    for (int i = 0; i < maps.Count; i++)
                    {
                        var m = maps[i];
                        if (!m.IsPlayerHome) continue;
                        // A home on the ground before one in space (an orbital base), however many are up there.
                        bool onGround = !MapFacts.InVacuum(m);
                        int n = m.mapPawns.FreeColonistsSpawnedCount;
                        if ((onGround && !ground) || (onGround == ground && n > most))
                        {
                            most = n;
                            busiest = m;
                            ground = onGround;
                        }
                    }
                }
                return busiest;
            }
        }

        // ---------------- Once an hour ----------------

        private HomeTie TieOf(Pawn p, bool create)
        {
            if (p == null) return null;
            if (byPawn == null)
            {
                byPawn = new Dictionary<Pawn, HomeTie>();
                foreach (var t in ties)
                    if (t?.pawn != null) byPawn[t.pawn] = t;
            }
            if (byPawn.TryGetValue(p, out var found) || !create) return found;
            found = new HomeTie { pawn = p };
            ties.Add(found);
            byPawn[p] = found;
            return found;
        }

        /// <summary>In the council's hour, with "who is home": who lives where, and a home founded or lost.</summary>
        public static void Hourly() => Instance?.Look(Now);

        private void Look(int now)
        {
            var homes = All;
            // A day on a home without a break makes it the last home someone lived on; being away breaks the stretch.
            var seen = new HashSet<Pawn>();
            foreach (var map in homes)
            {
                int key = KeyOf(map);
                foreach (var p in map.mapPawns.FreeColonists)
                {
                    if (p == null || p.Dead || !seen.Add(p)) continue;
                    var t = TieOf(p, true);
                    HomesMath.Note(key, now, ref t.at, ref t.since, ref t.last);
                }
            }
            foreach (var t in ties)
                if (t?.pawn != null && !seen.Contains(t.pawn)) HomesMath.Note(HomesMath.None, now, ref t.at, ref t.since, ref t.last);
            // Kept in bounds: the dead, the discarded and those no longer the colony's are forgotten.
            if (ties.RemoveAll(t => t == null || !ColonyCouncil.People.StillOurs(t.pawn)) > 0) byPawn = null;
            Notice(homes.Select(KeyOf).ToList());
        }

        /// <summary>Compares the homes with the last look's, and tells of any founded or lost.</summary>
        private void Notice(List<int> now)
        {
            if (!looked)
            {
                // The first look (a new game, or a save from before Homes): what's there is where things start.
                known = now;
                looked = true;
                return;
            }
            var founded = new List<int>();
            var lost = new List<int>();
            HomesMath.Diff(known, now, founded, lost);
            if (founded.Count == 0 && lost.Count == 0) return;
            known = now;
            try
            {
                Changed?.Invoke(founded, lost);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("homes: a home founded or lost", e);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            // Nobody gone since the last hour is written down (a reference to a pawn the game let go of saves as nothing).
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                try
                {
                    if (ties.RemoveAll(t => t == null || !ColonyCouncil.People.StillOurs(t.pawn)) > 0) byPawn = null;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("homes: clean-up before saving", e);
                }
            }
            Scribe_Values.Look(ref seat, "seat", HomesMath.None);
            Scribe_Collections.Look(ref ties, "ties", LookMode.Deep);
            Scribe_Collections.Look(ref known, "known", LookMode.Value);
            Scribe_Values.Look(ref looked, "looked");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ties ??= new List<HomeTie>();
                ties.RemoveAll(t => t == null || t.pawn == null);
                known ??= new List<int>();
                byPawn = null;
            }
        }
    }
}
