using System;

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
    }
}
