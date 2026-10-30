using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Laying a conduit to a piece that would be off the power (Building/RoomRearranger.cs), as plain rules: the
    /// shortest run of cells a conduit can go on, from beside the net to within a connector's reach of the piece.
    /// </summary>
    public static class ConduitMath
    {
        /// <summary>A powered piece connects to a conduit this far away (the game's PowerConnectionMaker.ConnectMaxDist).</summary>
        public const float Reach = 6f;
        /// <summary>No run longer than this: beyond it the piece is better left for the player's own conduit.</summary>
        public const int MaxRun = 40;

        /// <summary>
        /// The shortest run from any of <paramref name="sources"/> (cells beside the net) through cells a conduit can
        /// go on to a cell within reach of <paramref name="piece"/>, first cell beside the net, last cell within reach.
        /// Empty when there is none within <see cref="MaxRun"/> steps.
        /// </summary>
        public static List<(int x, int z)> Path(IEnumerable<(int x, int z)> sources, Func<(int x, int z), bool> passable, (int x, int z) piece, float reach = Reach, int maxRun = MaxRun)
        {
            var result = new List<(int x, int z)>();
            var parent = new Dictionary<(int x, int z), (int x, int z)>();
            var queue = new Queue<((int x, int z) cell, int steps)>();
            foreach (var s in sources)
            {
                if (parent.ContainsKey(s) || !passable(s)) continue;
                parent[s] = s;
                queue.Enqueue((s, 1));
            }
            while (queue.Count > 0)
            {
                var (c, steps) = queue.Dequeue();
                if (Within(c, piece, reach))
                {
                    // Walk back to the source.
                    var at = c;
                    while (true)
                    {
                        result.Add(at);
                        var p = parent[at];
                        if (p == at) break;
                        at = p;
                    }
                    result.Reverse();
                    return result;
                }
                if (steps >= maxRun) continue;
                foreach (var n in Beside(c))
                {
                    if (parent.ContainsKey(n) || !passable(n)) continue;
                    parent[n] = c;
                    queue.Enqueue((n, steps + 1));
                }
            }
            return result;
        }

        /// <summary>Within a connector's reach: the game's radial distance (Euclidean).</summary>
        public static bool Within((int x, int z) c, (int x, int z) piece, float reach)
        {
            int dx = c.x - piece.x, dz = c.z - piece.z;
            return dx * dx + dz * dz <= reach * reach;
        }

        private static IEnumerable<(int x, int z)> Beside((int x, int z) c)
        {
            yield return (c.x + 1, c.z);
            yield return (c.x - 1, c.z);
            yield return (c.x, c.z + 1);
            yield return (c.x, c.z - 1);
        }
    }
}
