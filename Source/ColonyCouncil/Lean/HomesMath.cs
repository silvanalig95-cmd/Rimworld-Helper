using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The rules behind <see cref="Homes"/>, with no game types so Tools/Tests can check them: the order the homes are
    /// listed in, which one is the seat, which home someone belongs to, the last home they lived on, where a memo's
    /// follow-up goes, and which homes were founded or lost between two looks. A home is known by its settlement's id
    /// (the world object's ID, which outlives the map's own); <see cref="None"/> is no home.
    /// </summary>
    public static class HomesMath
    {
        public const int None = -1;

        /// <summary>A day on a home makes it the last home someone lived on.</summary>
        public const int DayTicks = 60000;

        /// <summary>
        /// The order homes are listed in: on the ground before in orbit, then the first founded first (world objects are
        /// numbered as they're made, so the lower id). A home with no settlement (a negative key) comes after the rest.
        /// </summary>
        public static int Compare(int keyA, bool groundA, int keyB, bool groundB)
        {
            if (groundA != groundB) return groundA ? -1 : 1;
            bool knownA = keyA >= 0, knownB = keyB >= 0;
            if (knownA != knownB) return knownA ? -1 : 1;
            return keyA.CompareTo(keyB);
        }

        /// <summary>The seat: the one stored while it's still a home, else the first in order; none without a home.</summary>
        public static int Seat(IReadOnlyList<int> ordered, int stored)
        {
            if (ordered == null || ordered.Count == 0) return None;
            if (stored != None)
                for (int i = 0; i < ordered.Count; i++)
                    if (ordered[i] == stored) return stored;
            return ordered[0];
        }

        /// <summary>
        /// The home someone belongs to: the home they're on now; else the home where they own a bed; else the last home
        /// they lived on; else the seat. Each counts only while it's still a home.
        /// </summary>
        public static int Of(int here, int bed, int last, int seat, ICollection<int> homes)
        {
            if (here != None && homes.Contains(here)) return here;
            if (bed != None && homes.Contains(bed)) return bed;
            if (last != None && homes.Contains(last)) return last;
            return seat;
        }

        /// <summary>
        /// One look at someone (once an hour): <paramref name="here"/> is the home they're on now, or <see cref="None"/>.
        /// <paramref name="at"/> and <paramref name="since"/> are the home they've been on without a break and from when;
        /// once that is a day, it's the last home they lived on (<paramref name="last"/>). Leaving a home breaks the stretch.
        /// </summary>
        public static void Note(int here, int now, ref int at, ref int since, ref int last)
        {
            if (here == None)
            {
                at = None;
                return;
            }
            if (at != here)
            {
                at = here;
                since = now;
            }
            if (now - since >= DayTicks) last = here;
        }

        /// <summary>Where a memo's follow-up goes: its colony while that's still a home, else the seat.</summary>
        public static int Deliver(int memoHome, ICollection<int> homes, int seat) =>
            memoHome != None && homes.Contains(memoHome) ? memoHome : seat;

        /// <summary>The homes in <paramref name="after"/> that weren't in <paramref name="before"/> (founded), and the other way round (lost).</summary>
        public static void Diff(IReadOnlyList<int> before, IReadOnlyList<int> after, List<int> founded, List<int> lost)
        {
            founded.Clear();
            lost.Clear();
            var was = new HashSet<int>(before);
            var now = new HashSet<int>(after);
            foreach (int k in after)
                if (!was.Contains(k)) founded.Add(k);
            foreach (int k in before)
                if (!now.Contains(k)) lost.Add(k);
        }
    }
}
