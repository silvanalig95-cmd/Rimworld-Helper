using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>One colony's page in the council's book (ColonyState): the home it belongs to, and when that home stopped being ours.</summary>
    public interface IColonyEntry
    {
        /// <summary>
        /// The colony's key (<see cref="Homes.KeyOf"/>), or <see cref="HomesMath.None"/> while it has no home: an old save's
        /// single state not yet placed, or one carried by people who have no home left.
        /// </summary>
        int Home { get; set; }

        /// <summary>When its home stopped being ours (lost, abandoned, handed over); -1 while it is ours.</summary>
        int LostAt { get; set; }
    }

    /// <summary>
    /// The rules behind the council's book of colonies (ColonyState), with no game types so Tools/Tests can check them:
    /// which entry a colony reads, where an old save's single state goes (the seat), what happens to a colony's entry
    /// when its home is lost (carried by its people when no home is left, else kept a while and then let go), and a home
    /// that comes back. Keys are the homes' settlement ids (HomesMath); <see cref="HomesMath.None"/> is no home.
    /// </summary>
    public static class ColonyStateMath
    {
        public const int None = HomesMath.None;

        /// <summary>A home that stops being ours keeps its colony's council this long, in case it comes back, then lets it go.</summary>
        public const int KeepLostDays = 30;

        public const int NotLost = -1;

        /// <summary>
        /// The entry a colony reads, or null when it has none yet (the caller makes one): its own; else, for the seat, the
        /// entry with no home (an old save's single state, or one its people carried while they had no home), which
        /// becomes the seat's; else, when the seat is the only home (<paramref name="homes"/>), the entry of a home just
        /// left, as when the people moved in one go (a gravship landed, a caravan settled after giving up the old home
        /// within the hour): <see cref="Carried"/>'s choice. A home that came back is ours again.
        /// </summary>
        public static T Find<T>(IList<T> book, int key, int seat, ICollection<int> homes = null) where T : class, IColonyEntry
        {
            if (book == null || key == None) return null;
            for (int i = 0; i < book.Count; i++)
            {
                var e = book[i];
                if (e == null || e.Home != key) continue;
                e.LostAt = NotLost;
                return e;
            }
            if (key != seat) return null;
            for (int i = 0; i < book.Count; i++)
            {
                var e = book[i];
                if (e == null || e.Home != None) continue;
                e.Home = key;
                e.LostAt = NotLost;
                return e;
            }
            if (homes == null || homes.Count != 1 || !homes.Contains(key)) return null;
            var moved = Carried(book, homes);
            if (moved != null) moved.Home = key;
            return moved;
        }

        /// <summary>
        /// With no home at all, the entry the colony's people carry with them, or null (the caller makes one): the entry
        /// with no home; else the first whose home was lost and not yet noticed (in the book's order, the oldest colony's);
        /// else the one lost last. The next home founded is the seat, and takes it (<see cref="Find"/>).
        /// </summary>
        public static T Carried<T>(IList<T> book, ICollection<int> homes) where T : class, IColonyEntry
        {
            if (book == null) return null;
            for (int i = 0; i < book.Count; i++)
                if (book[i] != null && book[i].Home == None) return book[i];
            T pick = null;
            for (int i = 0; i < book.Count && pick == null; i++)
            {
                var e = book[i];
                if (e != null && e.LostAt == NotLost && (homes == null || !homes.Contains(e.Home))) pick = e;
            }
            if (pick == null)
                for (int i = 0; i < book.Count; i++)
                {
                    var e = book[i];
                    if (e != null && e.LostAt != NotLost && (pick == null || e.LostAt > pick.LostAt)) pick = e;
                }
            if (pick == null) return null;
            pick.Home = None;
            pick.LostAt = NotLost;
            return pick;
        }

        /// <summary>
        /// A home is lost (<paramref name="key"/>). With no home left its people carry its entry with them (unless they
        /// carry one already); otherwise it is kept from now, a while, in case the home comes back.
        /// </summary>
        public static void Lost<T>(IList<T> book, int key, int now, ICollection<int> homes) where T : class, IColonyEntry
        {
            if (book == null || key == None) return;
            for (int i = 0; i < book.Count; i++)
            {
                var e = book[i];
                if (e == null || e.Home != key || e.LostAt != NotLost) continue;
                if (homes == null || homes.Count == 0)
                {
                    if (!AnyHomeless(book))
                    {
                        e.Home = None;
                        return;
                    }
                }
                e.LostAt = now;
                return;
            }
        }

        private static bool AnyHomeless<T>(IList<T> book) where T : class, IColonyEntry
        {
            for (int i = 0; i < book.Count; i++)
                if (book[i] != null && book[i].Home == None) return true;
            return false;
        }

        /// <summary>Whether a kept entry goes now: its home lost <see cref="KeepLostDays"/> ago, while there is a home (with none, nothing goes).</summary>
        public static bool Drops(int lostAt, int now, bool anyHome) =>
            lostAt != NotLost && anyHome && now - lostAt >= KeepLostDays * HomesMath.DayTicks;

        /// <summary>
        /// Once a day: a home that came back is ours again; one lost without being noticed is kept from now; and kept
        /// entries whose time is up go (returns how many went). The entry with no home stays.
        /// </summary>
        public static int Upkeep<T>(List<T> book, int now, ICollection<int> homes) where T : class, IColonyEntry
        {
            if (book == null) return 0;
            bool any = homes != null && homes.Count > 0;
            foreach (var e in book)
            {
                if (e == null || e.Home == None) continue;
                bool home = homes != null && homes.Contains(e.Home);
                if (home) e.LostAt = NotLost;
                else if (e.LostAt == NotLost) Lost(book, e.Home, now, homes);
            }
            return book.RemoveAll(e => e == null || Drops(e.LostAt, now, any));
        }

        // ---------------- Each colony's own people ----------------

        /// <summary>A colony's people never walk out below this many: that would end its life (counted per colony).</summary>
        public const int FewestLeft = 4;

        /// <summary>How many of these people (each by the colony they belong to, HomesMath.Of) belong to <paramref name="colony"/>.</summary>
        public static int CountIn(IEnumerable<int> homesOfPeople, int colony)
        {
            int n = 0;
            if (homesOfPeople == null) return 0;
            foreach (int h in homesOfPeople)
                if (h == colony) n++;
            return n;
        }

        /// <summary>Someone may walk out of a colony with this many people (lodgers aside), or it would be left with too few.</summary>
        public static bool MayLeave(int peopleOfColony) => peopleOfColony >= FewestLeft;

        /// <summary>
        /// An officer of <paramref name="colony"/> who has gone to live at another: they belong elsewhere now
        /// (<paramref name="ofNow"/>, HomesMath.Of) and the last home they lived on for a day (<paramref name="lastHome"/>) is
        /// another too. Away on a caravan (still theirs by their bed) or on a short visit, they keep their seat.
        /// </summary>
        public static bool MovedAway(int colony, int ofNow, int lastHome) =>
            colony != None && ofNow != colony && lastHome != None && lastHome != colony;
    }
}
