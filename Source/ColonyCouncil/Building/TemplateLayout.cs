using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Pure integer layout rules for room templates, with no game types, so the tests can check every template the
    /// same way the game places it: footprints, interaction spots, nudging a piece back inside the room, reachability
    /// from the door, and where extra decor can go without blocking anything.
    ///
    /// Coordinates are interior cells: (0,0) is the bottom-left floor cell, the door is in the south wall at doorX.
    /// Facings are 0 = north, 1 = east, 2 = south, 3 = west, like Rot4.
    /// </summary>
    public static class TemplateLayout
    {
        /// <summary>The game's footprint (GenAdj.OccupiedRect) of a thing of size <paramref name="sizeX"/> x <paramref name="sizeZ"/> rooted at (x, z).</summary>
        public static void OccupiedRect(int x, int z, int rot, int sizeX, int sizeZ, out int minX, out int minZ, out int w, out int h)
        {
            rot = TemplateMath.Norm(rot);
            if (!(sizeX == 1 && sizeZ == 1))
            {
                if (rot == 1 || rot == 3)
                {
                    int t = sizeX;
                    sizeX = sizeZ;
                    sizeZ = t;
                }
                switch (rot)
                {
                    case 1:
                        if (sizeZ % 2 == 0) z--;
                        break;
                    case 2:
                        if (sizeX % 2 == 0) x--;
                        if (sizeZ % 2 == 0) z--;
                        break;
                    case 3:
                        if (sizeX % 2 == 0) x--;
                        break;
                }
            }
            minX = x - (sizeX - 1) / 2;
            minZ = z - (sizeZ - 1) / 2;
            w = sizeX;
            h = sizeZ;
        }

        /// <summary>An offset turned with the thing (IntVec3.RotatedBy).</summary>
        public static void RotateOffset(int ox, int oz, int rot, out int rx, out int rz)
        {
            switch (TemplateMath.Norm(rot))
            {
                case 1:
                    rx = oz;
                    rz = -ox;
                    break;
                case 2:
                    rx = -ox;
                    rz = -oz;
                    break;
                case 3:
                    rx = -oz;
                    rz = ox;
                    break;
                default:
                    rx = ox;
                    rz = oz;
                    break;
            }
        }

        /// <summary>
        /// How far a footprint has to move to sit inside a <paramref name="areaW"/> x <paramref name="areaH"/> area. False if it
        /// is bigger than the area or would have to move more than one cell: then the piece is left out instead.
        /// </summary>
        public static bool NudgeInside(int minX, int minZ, int w, int h, int areaW, int areaH, out int dx, out int dz)
        {
            dx = 0;
            dz = 0;
            if (w > areaW || h > areaH) return false;
            if (minX < 0) dx = -minX;
            else if (minX + w > areaW) dx = areaW - (minX + w);
            if (minZ < 0) dz = -minZ;
            else if (minZ + h > areaH) dz = areaH - (minZ + h);
            return dx >= -1 && dx <= 1 && dz >= -1 && dz <= 1;
        }

        /// <summary>The root cell whose footprint (for this facing and size) is exactly the given rect.</summary>
        public static bool RootFor(int minX, int minZ, int w, int h, int rot, int sizeX, int sizeZ, out int rx, out int rz)
        {
            for (int x = minX; x < minX + w; x++)
            {
                for (int z = minZ; z < minZ + h; z++)
                {
                    OccupiedRect(x, z, rot, sizeX, sizeZ, out int mx, out int mz, out int ww, out int hh);
                    if (mx == minX && mz == minZ && ww == w && hh == h)
                    {
                        rx = x;
                        rz = z;
                        return true;
                    }
                }
            }
            rx = minX;
            rz = minZ;
            return false;
        }

        /// <summary>One placed piece, for the reachability and decor checks.</summary>
        public struct Box
        {
            public int minX, minZ, w, h;
            /// <summary>The spot a worker stands on, or -1.</summary>
            public int ix, iz;
            /// <summary>People walk through it (a chair, a door in a partition, a sleeping spot).</summary>
            public bool passable;

            public Box(int minX, int minZ, int w, int h, bool passable = false, int ix = -1, int iz = -1)
            {
                this.minX = minX;
                this.minZ = minZ;
                this.w = w;
                this.h = h;
                this.passable = passable;
                this.ix = ix;
                this.iz = iz;
            }

            public bool HasSpot => ix >= 0 && iz >= 0;
            public bool Contains(int x, int z) => x >= minX && x < minX + w && z >= minZ && z < minZ + h;
        }

        /// <summary>
        /// Walks the free floor from the cell inside the door. Every work spot must be reachable, and every piece must
        /// have a reachable cell next to it. <paramref name="problem"/> says what isn't.
        /// </summary>
        public static bool AllReachable(int w, int h, int doorX, List<Box> boxes, bool[,] extraBlocked, out string problem)
        {
            problem = null;
            var blocked = new bool[w, h];
            var walk = new bool[w, h];
            for (int i = 0; i < boxes.Count; i++)
            {
                var b = boxes[i];
                for (int x = b.minX; x < b.minX + b.w; x++)
                    for (int z = b.minZ; z < b.minZ + b.h; z++)
                        if (x >= 0 && z >= 0 && x < w && z < h)
                        {
                            if (b.passable) walk[x, z] = true;
                            else blocked[x, z] = true;
                        }
            }
            if (extraBlocked != null)
                for (int x = 0; x < w; x++)
                    for (int z = 0; z < h; z++)
                        if (extraBlocked[x, z]) blocked[x, z] = true;
            if (doorX < 0 || doorX >= w || h < 1 || blocked[doorX, 0])
            {
                problem = "the cell inside the door is taken";
                return false;
            }
            var reached = new bool[w, h];
            var queue = new Queue<(int, int)>();
            reached[doorX, 0] = true;
            queue.Enqueue((doorX, 0));
            while (queue.Count > 0)
            {
                var (x, z) = queue.Dequeue();
                Step(x + 1, z);
                Step(x - 1, z);
                Step(x, z + 1);
                Step(x, z - 1);
            }
            void Step(int x, int z)
            {
                if (x < 0 || z < 0 || x >= w || z >= h || reached[x, z] || blocked[x, z]) return;
                reached[x, z] = true;
                queue.Enqueue((x, z));
            }
            for (int i = 0; i < boxes.Count; i++)
            {
                var b = boxes[i];
                if (b.HasSpot)
                {
                    if (b.ix < 0 || b.iz < 0 || b.ix >= w || b.iz >= h || !reached[b.ix, b.iz])
                    {
                        problem = $"the work spot ({b.ix},{b.iz}) of the piece at ({b.minX},{b.minZ}) can't be reached";
                        return false;
                    }
                    continue;
                }
                bool ok = false;
                for (int x = b.minX - 1; x <= b.minX + b.w && !ok; x++)
                {
                    for (int z = b.minZ - 1; z <= b.minZ + b.h && !ok; z++)
                    {
                        bool inside = b.Contains(x, z);
                        bool edge = !inside && (x == b.minX - 1 || x == b.minX + b.w) != (z == b.minZ - 1 || z == b.minZ + b.h);
                        if (!(edge || (inside && b.passable))) continue;
                        if (x >= 0 && z >= 0 && x < w && z < h && reached[x, z]) ok = true;
                    }
                }
                if (!ok)
                {
                    problem = $"the piece at ({b.minX},{b.minZ}) can't be reached";
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Free cells along the walls where small decor (a plant pot) can go without blocking the door, a work spot or the
        /// way to any piece: corners first, then the cells farthest from the door. At most <paramref name="max"/>.
        /// </summary>
        public static List<(int x, int z)> DecorSpots(int w, int h, int doorX, List<Box> boxes, int max)
        {
            var result = new List<(int x, int z)>();
            if (max <= 0 || w < 2 || h < 2) return result;
            var taken = new bool[w, h];
            foreach (var b in boxes)
            {
                for (int x = b.minX; x < b.minX + b.w; x++)
                    for (int z = b.minZ; z < b.minZ + b.h; z++)
                        if (x >= 0 && z >= 0 && x < w && z < h) taken[x, z] = true;
                if (b.HasSpot && b.ix >= 0 && b.iz >= 0 && b.ix < w && b.iz < h) taken[b.ix, b.iz] = true;
            }
            if (!AllReachable(w, h, doorX, boxes, null, out _)) return result;
            var candidates = new List<(int x, int z, int score)>();
            for (int x = 0; x < w; x++)
            {
                for (int z = 0; z < h; z++)
                {
                    bool edge = x == 0 || z == 0 || x == w - 1 || z == h - 1;
                    if (!edge || taken[x, z]) continue;
                    // Keep the doorway clear: nothing in the two rows inside the door, one cell either side.
                    if (z <= 1 && x >= doorX - 1 && x <= doorX + 1) continue;
                    bool corner = (x == 0 || x == w - 1) && (z == 0 || z == h - 1);
                    int dist = (x > doorX ? x - doorX : doorX - x) + z;
                    candidates.Add((x, z, (corner ? 1000 : 0) + dist * 10 + x));
                }
            }
            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            var blocked = new bool[w, h];
            foreach (var c in candidates)
            {
                if (result.Count >= max) break;
                // Not right next to another decor spot: pots in a row read as clutter.
                bool crowded = false;
                foreach (var r in result)
                    if ((r.x - c.x) * (r.x - c.x) + (r.z - c.z) * (r.z - c.z) <= 1) crowded = true;
                if (crowded) continue;
                blocked[c.x, c.z] = true;
                if (AllReachable(w, h, doorX, boxes, blocked, out _)) result.Add((c.x, c.z));
                else blocked[c.x, c.z] = false;
            }
            return result;
        }
    }
}
