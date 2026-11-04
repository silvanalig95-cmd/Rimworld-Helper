using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// How good an open-air cell is for the colony to gather at (PlayYard.cs reads the map; this weighs it). People meet
    /// where the colony makes the outdoors pleasant: a terrace with a floor, seats and tables out in the open, a party
    /// spot or a fire, something good to look at; not beside the batteries and the coolers, on a stockpile, in a field,
    /// across a doorway, or far out at the edge of the home ground. Pure, so it can be tested.
    /// </summary>
    public static class YardMath
    {
        /// <summary>Free open cells around the spot below this and a group doesn't fit: not a place to gather.</summary>
        public const int MinRoom = 12;

        public struct Cell
        {
            /// <summary>The game's perceived beauty at the cell.</summary>
            public float beauty;
            /// <summary>A built floor (a terrace, a path, a patio).</summary>
            public bool floor;
            /// <summary>Seats, tables and things to enjoy out in the open nearby.</summary>
            public int seats;
            /// <summary>A party spot, a gathering spot or a campfire close by.</summary>
            public bool gatherNear;
            /// <summary>Generators, batteries, coolers, tanks, turrets and the like close by.</summary>
            public int works;
            public bool stockpile, field, byDoor;
            /// <summary>Free open cells around it (room for a group).</summary>
            public int room;
            /// <summary>Distance from the middle of the colony.</summary>
            public float distance;
        }

        /// <summary>The cell's worth as a gathering place; lower than <see cref="Unfit"/> when it isn't one.</summary>
        public static float Score(Cell c)
        {
            if (c.room < MinRoom) return Unfit;
            float s = Math.Max(-10f, Math.Min(10f, c.beauty));
            if (c.floor) s += 2f;
            s += Math.Min(4, c.seats) * 1.5f;
            if (c.gatherNear) s += 4f;
            s -= Math.Min(3, c.works) * 3f;
            if (c.stockpile) s -= 3f;
            if (c.field) s -= 2f;
            if (c.byDoor) s -= 2f;
            s += Math.Min(25, c.room) * 0.08f;
            s -= Math.Max(0f, c.distance - 6f) * 0.08f;
            return s;
        }

        public const float Unfit = -1000f;

        // ---------------- More than one place ----------------

        /// <summary>Two good cells this far apart or more are two places to gather, not one.</summary>
        public const float Apart = 8f;
        /// <summary>At most this many places are weighed for a gathering.</summary>
        public const int MaxPlaces = 6;
        /// <summary>A place this far below the best still has a fair chance; twice as far below, none.</summary>
        public const float Spread = 6f;
        /// <summary>How long one choice holds (a gathering and its walk keep their place), in hours.</summary>
        public const int HoldHours = 3;
        /// <summary>How many places used lately are remembered.</summary>
        public const int Remembered = 3;

        /// <summary>
        /// The distinct places among cells ranked best first (x, z, score): a cell starts a place when no place already
        /// taken lies within <see cref="Apart"/>, so a terrace's forty good cells are one place and the yard across the
        /// colony is another. Returns the indexes of the cells that start a place, best first.
        /// </summary>
        public static List<int> Places(IList<(int x, int z, float s)> ranked)
        {
            var starts = new List<int>();
            for (int i = 0; i < ranked.Count && starts.Count < MaxPlaces; i++)
            {
                bool near = false;
                foreach (int j in starts)
                {
                    float dx = ranked[i].x - ranked[j].x, dz = ranked[i].z - ranked[j].z;
                    if (dx * dx + dz * dz < Apart * Apart) { near = true; break; }
                }
                if (!near) starts.Add(i);
            }
            return starts;
        }

        /// <summary>
        /// How likely a place is chosen: the nearer its score to the best, the likelier; none twice <see cref="Spread"/>
        /// below it; less if it was used lately (<paramref name="usedAgo"/>: 0 the last place used, 1 the one before, -1 not
        /// lately), so the colony doesn't always meet on the same patch while it has other good ones.
        /// </summary>
        public static float Weight(float score, float best, int usedAgo)
        {
            if (score < best - 2f * Spread) return 0f;
            float w = 0.25f + Math.Max(0f, score - (best - Spread));
            if (usedAgo == 0) w *= 0.15f;
            else if (usedAgo == 1) w *= 0.4f;
            else if (usedAgo == 2) w *= 0.7f;
            return w;
        }

        /// <summary>The index chosen by weight for a roll in [0, 1), or -1 when every weight is nothing.</summary>
        public static int Pick(IList<float> weights, float roll)
        {
            float total = 0f;
            foreach (float w in weights) total += Math.Max(0f, w);
            if (total <= 0f) return -1;
            float at = roll * total;
            for (int i = 0; i < weights.Count; i++)
            {
                float w = Math.Max(0f, weights[i]);
                if (w <= 0f) continue;
                if (at < w) return i;
                at -= w;
            }
            for (int i = weights.Count - 1; i >= 0; i--)
                if (weights[i] > 0f) return i;
            return -1;
        }
    }
}
