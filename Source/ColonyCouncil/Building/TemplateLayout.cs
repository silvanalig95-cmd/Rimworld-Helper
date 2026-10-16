using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Pure integer layout rules for room templates, with no game types, so the tests can check every template the
    /// same way the game places it: footprints, work spots, nudging a piece back inside the room, turning a piece whose
    /// work spot would face a wall, reachability from the door, where extra decor can go, and where every piece lands
    /// once the room is mirrored and turned.
    ///
    /// Coordinates are interior cells: (0,0) is the bottom-left floor cell, the door is in the south wall at doorX.
    /// Pieces built into the wall ring use full coordinates ((0,0) is the bottom-left wall corner).
    /// Facings are 0 = north, 1 = east, 2 = south, 3 = west, like Rot4.
    /// </summary>
    public static class TemplateLayout
    {
        // ---------------- Styles ----------------

        /// <summary>Spartan, comfortable, lavish (the order of RoomStyle).</summary>
        public const int Spartan = 0, Comfortable = 1, Lavish = 2;

        private static readonly HashSet<string> decorRoles = new HashSet<string> { "plantpot", "endtable", "dresser", "column", "drape", "decor", "pedestal", "rug" };

        /// <summary>
        /// Which styles include a piece: "core" (always), "decor" (comfortable and lavish), "lavish" (lavish only). Empty:
        /// decor for plant pots, bedside tables, dressers, columns, drapes, pedestals and rugs, core for everything else.
        /// </summary>
        public static string TierOf(string tier, string role) =>
            !string.IsNullOrEmpty(tier) ? tier : role != null && decorRoles.Contains(role) ? "decor" : "core";

        /// <summary>A piece is part of a room in this style. Pieces the room can't do without are always in.</summary>
        public static bool InStyle(string tier, string role, bool optional, int style)
        {
            if (!optional) return true;
            switch (TierOf(tier, role))
            {
                case "lavish": return style == Lavish;
                case "decor": return style != Spartan;
                default: return true;
            }
        }

        // ---------------- Footprints ----------------

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

        /// <summary>One step in a facing's direction: north is +z, east is +x.</summary>
        public static void Step(int facing, out int dx, out int dz)
        {
            RotateOffset(0, 1, facing, out dx, out dz);
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

        // ---------------- Arranging a template ----------------

        /// <summary>One piece of a template, as the game resolved it (its def's size, facing rules and work spots).</summary>
        public class Piece
        {
            /// <summary>Where it came from (the template's thing index; -1 for added decor).</summary>
            public int slot = -1;
            /// <summary>Position and facing from the template.</summary>
            public int x, z, rot;
            public int sizeX = 1, sizeZ = 1;
            /// <summary>False: the game only builds it facing north.</summary>
            public bool rotatable = true;
            /// <summary>People walk over or sit in it (a chair, a sleeping spot, a door in a partition).</summary>
            public bool passable;
            public bool optional = true;
            public bool inWall;
            public bool isDoor;
            /// <summary>A wall inside the room (a partition): it splits the room's light, and a wall lamp can hang on it.</summary>
            public bool isWall;
            /// <summary>Its work spots (interaction cells) for a north facing, relative to the root.</summary>
            public List<(int x, int z)> spots = new List<(int x, int z)>();
            public string role;

            // Results
            public int minX, minZ, w, h;
            public int facing;
            public bool dropped, turned;
            public string note;

            public bool Contains(int cx, int cz) => cx >= minX && cx < minX + w && cz >= minZ && cz < minZ + h;

            /// <summary>The root cell for the arranged footprint and facing.</summary>
            public void Root(out int rx, out int rz) => RootFor(minX, minZ, w, h, facing, sizeX, sizeZ, out rx, out rz);

            /// <summary>The work spots for the arranged footprint and facing.</summary>
            public List<(int x, int z)> SpotCells()
            {
                var result = new List<(int, int)>();
                if (spots == null || spots.Count == 0) return result;
                Root(out int rx, out int rz);
                foreach (var (ox, oz) in spots)
                {
                    RotateOffset(ox, oz, facing, out int sx, out int sz);
                    result.Add((rx + sx, rz + sz));
                }
                return result;
            }

            /// <summary>
            /// The work spots in the plain room and, mapped back, in the mirrored one. A piece two cells wide has its root
            /// in its left cell, so mirroring the room moves its work spot over by one: both places have to work.
            /// </summary>
            public List<(int x, int z)> SpotCellsBothWays(int areaW)
            {
                var result = SpotCells();
                if (spots == null || spots.Count == 0 || areaW <= 0) return result;
                int mMinX = areaW - (minX + w);
                int mFacing = TemplateMath.MirrorFacing(facing);
                if (!RootFor(mMinX, minZ, w, h, mFacing, sizeX, sizeZ, out int rx, out int rz)) return result;
                foreach (var (ox, oz) in spots)
                {
                    RotateOffset(ox, oz, mFacing, out int sx, out int sz);
                    var back = (areaW - 1 - (rx + sx), rz + sz);
                    if (!result.Contains(back)) result.Add(back);
                }
                return result;
            }
        }

        private static bool Footprint(Piece p, int facing, int areaW, int areaH, out int minX, out int minZ, out int w, out int h)
        {
            OccupiedRect(p.x, p.z, facing, p.sizeX, p.sizeZ, out minX, out minZ, out w, out h);
            if (minX >= 0 && minZ >= 0 && minX + w <= areaW && minZ + h <= areaH) return true;
            if (!NudgeInside(minX, minZ, w, h, areaW, areaH, out int dx, out int dz)) return false;
            minX += dx;
            minZ += dz;
            return true;
        }

        /// <summary>
        /// Fits every piece into a <paramref name="areaW"/> x <paramref name="areaH"/> room with its door at
        /// <paramref name="doorX"/>, and makes the layout work: each piece inside the room (nudged by a cell if a bigger
        /// variant sticks out), nothing on top of anything else, the cell inside the door free, and every work spot inside
        /// the room and not under furniture. A piece whose work spot would end up in a wall or under something is turned
        /// to a facing where it works; one that fits nowhere is left out if the room can do without it. Last, pieces that
        /// can't be reached from the door are left out. Pieces the room can't do without go first. Returns what changed.
        /// </summary>
        public static List<string> Arrange(int areaW, int areaH, int doorX, List<Piece> pieces)
        {
            var notes = new List<string>();
            var accepted = new List<Piece>();
            var order = new List<Piece>();
            foreach (var p in pieces) if (!p.optional) order.Add(p);
            foreach (var p in pieces) if (p.optional) order.Add(p);
            int fullW = areaW + 2, fullH = areaH + 2;
            foreach (var p in order)
            {
                p.dropped = false;
                p.turned = false;
                p.note = null;
                // A door keeps the facing of the wall it stands in; anything else the game can't turn faces north.
                int wanted = p.rotatable || p.isDoor ? TemplateMath.Norm(p.rot) : 0;
                if (p.inWall)
                {
                    OccupiedRect(p.x, p.z, wanted, p.sizeX, p.sizeZ, out p.minX, out p.minZ, out p.w, out p.h);
                    p.facing = wanted;
                    bool inside = p.minX >= 0 && p.minZ >= 0 && p.minX + p.w <= fullW && p.minZ + p.h <= fullH;
                    if (!inside)
                    {
                        p.dropped = true;
                        p.note = "doesn't fit in the wall";
                        notes.Add($"slot {p.slot} ({p.role}): {p.note}");
                    }
                    continue;
                }
                bool placed = false;
                var tries = new List<int> { wanted };
                if (p.rotatable && !p.isDoor)
                {
                    tries.Add(TemplateMath.Norm(wanted + 2));
                    tries.Add(TemplateMath.Norm(wanted + 1));
                    tries.Add(TemplateMath.Norm(wanted + 3));
                }
                string why = null;
                foreach (int f in tries)
                {
                    if (!Footprint(p, f, areaW, areaH, out int mx, out int mz, out int w, out int h))
                    {
                        why ??= "doesn't fit inside";
                        continue;
                    }
                    p.minX = mx;
                    p.minZ = mz;
                    p.w = w;
                    p.h = h;
                    p.facing = f;
                    string problem = Problem(p, accepted, areaW, areaH, doorX);
                    if (problem == null)
                    {
                        placed = true;
                        p.turned = f != wanted;
                        if (p.turned) notes.Add($"slot {p.slot} ({p.role}): turned from {wanted} to {f} ({why})");
                        break;
                    }
                    why ??= problem;
                }
                if (placed)
                {
                    accepted.Add(p);
                    continue;
                }
                if (p.optional || !Footprint(p, wanted, areaW, areaH, out _, out _, out _, out _))
                {
                    p.dropped = true;
                    p.note = why ?? "doesn't fit";
                    notes.Add($"slot {p.slot} ({p.role}): left out, {p.note}");
                    continue;
                }
                // The room can't do without it: keep it as the template says, and say what's wrong.
                Footprint(p, wanted, areaW, areaH, out p.minX, out p.minZ, out p.w, out p.h);
                p.facing = wanted;
                p.note = why;
                notes.Add($"slot {p.slot} ({p.role}): kept although {why}");
                accepted.Add(p);
            }

            // Everything has to be reachable from the door. Leave out the optional pieces that wall others in.
            for (int guard = 0; guard < 20; guard++)
            {
                int bad = Unreachable(areaW, areaH, doorX, accepted, null, out string problem);
                if (bad == 0) break;
                Piece worst = null;
                int best = bad;
                for (int i = accepted.Count - 1; i >= 0; i--)
                {
                    var p = accepted[i];
                    if (!p.optional) continue;
                    accepted.RemoveAt(i);
                    int left = Unreachable(areaW, areaH, doorX, accepted, null, out _);
                    accepted.Insert(i, p);
                    if (left < best)
                    {
                        best = left;
                        worst = p;
                    }
                }
                if (worst == null)
                {
                    notes.Add("unreachable: " + problem);
                    break;
                }
                worst.dropped = true;
                worst.note = "would wall something in";
                notes.Add($"slot {worst.slot} ({worst.role}): left out, {worst.note}");
                accepted.Remove(worst);
            }
            return notes;
        }

        /// <summary>What's wrong with a piece at its current footprint and facing, given the pieces already placed; null if nothing.</summary>
        private static string Problem(Piece p, List<Piece> accepted, int areaW, int areaH, int doorX)
        {
            // The cell inside the door stays free (a door in a partition right there is fine).
            if (p.Contains(doorX, 0) && !p.isDoor) return "blocks the door";
            foreach (var a in accepted)
            {
                if (a.inWall) continue;
                if (Overlaps(p, a)) return "overlaps another piece";
                foreach (var (sx, sz) in a.SpotCellsBothWays(areaW))
                    if (p.Contains(sx, sz) && !p.passable) return "covers another piece's work spot";
            }
            foreach (var (sx, sz) in p.SpotCellsBothWays(areaW))
            {
                if (sx < 0 || sz < 0 || sx >= areaW || sz >= areaH) return "its work spot is in the wall";
                if (p.Contains(sx, sz)) return "its work spot is under itself";
                foreach (var a in accepted)
                    if (!a.inWall && a.Contains(sx, sz) && !a.passable) return "its work spot is under another piece";
            }
            return null;
        }

        private static bool Overlaps(Piece a, Piece b) =>
            a.minX < b.minX + b.w && b.minX < a.minX + a.w && a.minZ < b.minZ + b.h && b.minZ < a.minZ + a.h;

        /// <summary>
        /// Walks the free floor from the cell inside the door and counts the pieces that can't be used: a work spot nobody
        /// can reach, or a piece with no reachable cell next to it. <paramref name="extraBlocked"/> marks more taken cells.
        /// </summary>
        public static int Unreachable(int w, int h, int doorX, List<Piece> pieces, bool[,] extraBlocked, out string problem)
        {
            problem = null;
            var blocked = new bool[w, h];
            foreach (var p in pieces)
            {
                if (p.inWall || p.dropped) continue;
                for (int x = p.minX; x < p.minX + p.w; x++)
                    for (int z = p.minZ; z < p.minZ + p.h; z++)
                        if (x >= 0 && z >= 0 && x < w && z < h && !p.passable) blocked[x, z] = true;
            }
            if (extraBlocked != null)
                for (int x = 0; x < w; x++)
                    for (int z = 0; z < h; z++)
                        if (extraBlocked[x, z]) blocked[x, z] = true;
            if (doorX < 0 || doorX >= w || h < 1 || blocked[doorX, 0])
            {
                problem = "the cell inside the door is taken";
                return pieces.Count + 1;
            }
            var reached = new bool[w, h];
            var queue = new Queue<(int, int)>();
            reached[doorX, 0] = true;
            queue.Enqueue((doorX, 0));
            while (queue.Count > 0)
            {
                var (x, z) = queue.Dequeue();
                Visit(x + 1, z);
                Visit(x - 1, z);
                Visit(x, z + 1);
                Visit(x, z - 1);
            }
            void Visit(int x, int z)
            {
                if (x < 0 || z < 0 || x >= w || z >= h || reached[x, z] || blocked[x, z]) return;
                reached[x, z] = true;
                queue.Enqueue((x, z));
            }
            int bad = 0;
            foreach (var p in pieces)
            {
                if (p.inWall || p.dropped) continue;
                var spots = p.SpotCellsBothWays(w);
                if (spots.Count > 0)
                {
                    foreach (var (sx, sz) in spots)
                    {
                        if (sx < 0 || sz < 0 || sx >= w || sz >= h || !reached[sx, sz])
                        {
                            problem ??= $"the work spot ({sx},{sz}) of the piece at ({p.minX},{p.minZ}) can't be reached";
                            bad++;
                            break;
                        }
                    }
                    continue;
                }
                bool ok = false;
                for (int x = p.minX - 1; x <= p.minX + p.w && !ok; x++)
                {
                    for (int z = p.minZ - 1; z <= p.minZ + p.h && !ok; z++)
                    {
                        bool inside = p.Contains(x, z);
                        bool edge = !inside && (x == p.minX - 1 || x == p.minX + p.w) != (z == p.minZ - 1 || z == p.minZ + p.h);
                        if (!(edge || (inside && p.passable))) continue;
                        if (x >= 0 && z >= 0 && x < w && z < h && reached[x, z]) ok = true;
                    }
                }
                if (!ok)
                {
                    problem ??= $"the piece at ({p.minX},{p.minZ}) can't be reached";
                    bad++;
                }
            }
            return bad;
        }

        /// <summary>
        /// Free cells along the walls where small decor (a plant pot) can go without blocking the door, a work spot or the
        /// way to any piece: corners first, then the cells farthest from the door. At most <paramref name="max"/>.
        /// </summary>
        public static List<(int x, int z)> DecorSpots(int w, int h, int doorX, List<Piece> pieces, int max)
        {
            var result = new List<(int x, int z)>();
            if (max <= 0 || w < 2 || h < 2) return result;
            var live = pieces.FindAll(p => !p.dropped && !p.inWall);
            var taken = new bool[w, h];
            foreach (var p in live)
            {
                for (int x = p.minX; x < p.minX + p.w; x++)
                    for (int z = p.minZ; z < p.minZ + p.h; z++)
                        if (x >= 0 && z >= 0 && x < w && z < h) taken[x, z] = true;
                foreach (var (sx, sz) in p.SpotCellsBothWays(w))
                    if (sx >= 0 && sz >= 0 && sx < w && sz < h) taken[sx, sz] = true;
            }
            if (Unreachable(w, h, doorX, live, null, out _) > 0) return result;
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
            // The pots go in as pieces too, so each one needs a reachable cell next to it (to build and water it).
            var withPots = new List<Piece>(live);
            foreach (var c in candidates)
            {
                if (result.Count >= max) break;
                // Not right next to another decor spot: pots in a row read as clutter.
                bool crowded = false;
                foreach (var r in result)
                    if ((r.x - c.x) * (r.x - c.x) + (r.z - c.z) * (r.z - c.z) <= 1) crowded = true;
                if (crowded) continue;
                var pot = new Piece { x = c.x, z = c.z, minX = c.x, minZ = c.z, w = 1, h = 1, role = "decor" };
                withPots.Add(pot);
                if (Unreachable(w, h, doorX, withPots, null, out _) == 0) result.Add((c.x, c.z));
                else withPots.Remove(pot);
            }
            return result;
        }

        /// <summary>
        /// A free open rectangle of <paramref name="rw"/> x <paramref name="rh"/> floor cells for a rug, away from the
        /// walls if it can be, not covering a work spot. False if there's none.
        /// </summary>
        public static bool RugSpot(int w, int h, int doorX, List<Piece> pieces, int rw, int rh, out int rx, out int rz)
        {
            rx = rz = -1;
            if (rw > w - 2 || rh > h - 2) return false;
            var live = pieces.FindAll(p => !p.dropped && !p.inWall);
            var taken = new bool[w, h];
            foreach (var p in live)
            {
                for (int x = p.minX; x < p.minX + p.w; x++)
                    for (int z = p.minZ; z < p.minZ + p.h; z++)
                        if (x >= 0 && z >= 0 && x < w && z < h) taken[x, z] = true;
                foreach (var (sx, sz) in p.SpotCellsBothWays(w))
                    if (sx >= 0 && sz >= 0 && sx < w && sz < h) taken[sx, sz] = true;
            }
            taken[doorX, 0] = true;
            int bestScore = int.MaxValue;
            for (int x = 1; x + rw <= w - 1; x++)
            {
                for (int z = 1; z + rh <= h - 1; z++)
                {
                    bool free = true;
                    for (int i = x; i < x + rw && free; i++)
                        for (int j = z; j < z + rh && free; j++)
                            if (taken[i, j]) free = false;
                    if (!free) continue;
                    // As central as possible.
                    int score = System.Math.Abs(2 * x + rw - w) + System.Math.Abs(2 * z + rh - h);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        rx = x;
                        rz = z;
                    }
                }
            }
            return rx >= 0;
        }

        // ---------------- Placing it in the world ----------------

        /// <summary>
        /// Where a piece lands once the room is mirrored (west to east) and turned <paramref name="turn"/> times clockwise:
        /// its footprint in the turned full rect, its facing, and the root cell the game needs for that facing and size.
        /// <paramref name="minX"/>..<paramref name="h"/> is the piece's footprint in full template coordinates (interior + 1).
        /// A piece the game only builds facing north stays facing north; a door faces the way of the wall it stands in.
        /// False if the footprint can't be made with the facing the piece ends up with (it can't be turned).
        /// </summary>
        public static bool ToWorld(int fullW, int fullH, bool mirror, int turn, int minX, int minZ, int w, int h, int facing,
            bool rotatable, bool isDoor, int sizeX, int sizeZ,
            out int wMinX, out int wMinZ, out int ww, out int wh, out int worldFacing, out int rootX, out int rootZ)
        {
            TemplateMath.TransformRect(minX, minZ, w, h, fullW, fullH, mirror, turn, out wMinX, out wMinZ, out ww, out wh);
            worldFacing = TemplateMath.TransformFacing(facing, mirror, turn);
            if (isDoor) worldFacing = worldFacing % 2 == 1 ? 1 : 0;
            else if (!rotatable) worldFacing = 0;
            return RootFor(wMinX, wMinZ, ww, wh, worldFacing, sizeX, sizeZ, out rootX, out rootZ);
        }
    }
}
