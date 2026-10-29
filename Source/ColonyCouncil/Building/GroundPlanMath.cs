using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The ground plan (Building/GroundPlan.cs) as plain rules: which rooms are drawn first (the ones in the plan,
    /// then the proposals, the most pressing first), the cells a room keeps for itself so the next one is found
    /// clear of it, and a colour for each.
    /// </summary>
    public static class GroundPlanMath
    {
        /// <summary>At most this many rooms are drawn: beyond that the spots get worse than guesses.</summary>
        public const int MaxRooms = 6;
        /// <summary>A room keeps this many cells round itself free, for a path and the next room's wall.</summary>
        public const int Margin = 1;

        /// <summary>The order to find spots in: planned rooms first, in their order; then proposals, highest score first; at most <see cref="MaxRooms"/>.</summary>
        public static List<int> Order(IList<bool> planned, IList<float> scores)
        {
            var order = new List<int>();
            for (int i = 0; i < planned.Count; i++) if (planned[i]) order.Add(i);
            var rest = new List<int>();
            for (int i = 0; i < planned.Count; i++) if (!planned[i]) rest.Add(i);
            rest.Sort((a, b) => scores[b] != scores[a] ? scores[b].CompareTo(scores[a]) : a.CompareTo(b));
            order.AddRange(rest);
            if (order.Count > MaxRooms) order.RemoveRange(MaxRooms, order.Count - MaxRooms);
            return order;
        }

        /// <summary>The cells a placed room of <paramref name="w"/> by <paramref name="h"/> (after turning) at anchor keeps, with the margin round it.</summary>
        public static List<(int x, int z)> Kept(int anchorX, int anchorZ, int w, int h, int margin = Margin)
        {
            var list = new List<(int, int)>();
            for (int x = anchorX - margin; x < anchorX + w + margin; x++)
                for (int z = anchorZ - margin; z < anchorZ + h + margin; z++)
                    list.Add((x, z));
            return list;
        }

        /// <summary>The edge of a placed room's footprint, for drawing.</summary>
        public static List<(int x, int z)> Edge(int anchorX, int anchorZ, int w, int h)
        {
            var list = new List<(int, int)>();
            for (int x = anchorX; x < anchorX + w; x++)
                for (int z = anchorZ; z < anchorZ + h; z++)
                    if (x == anchorX || z == anchorZ || x == anchorX + w - 1 || z == anchorZ + h - 1) list.Add((x, z));
            return list;
        }

        /// <summary>Six colours apart from one another, as (r, g, b), one per room in turn.</summary>
        public static readonly (float r, float g, float b)[] Palette =
        {
            (0.45f, 0.9f, 0.5f), (0.4f, 0.75f, 1f), (1f, 0.82f, 0.3f), (1f, 0.55f, 0.75f), (0.75f, 0.6f, 1f), (1f, 0.6f, 0.3f)
        };

        public static (float r, float g, float b) ColorOf(int index) => Palette[((index % Palette.Length) + Palette.Length) % Palette.Length];
    }
}
