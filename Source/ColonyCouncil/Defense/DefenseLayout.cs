using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Pure integer rules for defense templates, with no game types, so the tests check every template the way the game
    /// places it. A template is a grid of characters, written in the XML from the top row down; the top row faces the
    /// threat (north, +z, before the template is turned). Each character is a piece or a kind of ground:
    /// <list type="bullet">
    /// <item><c>W</c> wall, <c>G</c> gate (a door in a wall line), <c>E</c> embrasure (a wall that can be shot over).</item>
    /// <item><c>S</c> cover (sandbags, barricades), <c>T</c> trap, <c>F</c> something that slows raiders (a fence).</item>
    /// <item><c>U</c> a turret, <c>M</c> a mortar: the bottom-left cell of a block whose other cells are <c>u</c> or
    /// <c>m</c> (the room it may take: a 2x2 block holds a mini-turret or an autocannon alike).</item>
    /// <item><c>H</c> a shelf (weapons by the beds, shells away from the mortars).</item>
    /// <item><c>f</c> a firing position: kept clear, with cover in front. <c>.</c> open ground kept clear (a kill zone, a
    /// walkway). <c>_</c> ground the plan doesn't touch.</item>
    /// </list>
    /// Coordinates are template cells: (0,0) is the bottom-left (inner) corner. Facings are 0 = north, 1 = east, 2 = south,
    /// 3 = west, like Rot4; a template's outward facing is north until it is turned.
    /// </summary>
    public static class DefenseLayout
    {
        public const char Wall = 'W', Gate = 'G', Embrasure = 'E', Cover = 'S', Trap = 'T', Slow = 'F', Turret = 'U', TurretRoom = 'u',
            Mortar = 'M', MortarRoom = 'm', Shelf = 'H', Firing = 'f', Open = '.', Untouched = '_';

        private const string Known = "WGESTFUuMmHf._";

        /// <summary>The role a piece character stands for (the def's picks are by role), or null for ground.</summary>
        public static string RoleOf(char c)
        {
            switch (c)
            {
                case Wall: return "wall";
                case Gate: return "gate";
                case Embrasure: return "embrasure";
                case Cover: return "cover";
                case Trap: return "trap";
                case Slow: return "slow";
                case Turret: return "turret";
                case Mortar: return "mortar";
                case Shelf: return "shelf";
                default: return null;
            }
        }

        /// <summary>Every role a template can use, in the order the planner lists them.</summary>
        public static readonly string[] Roles = { "wall", "gate", "embrasure", "cover", "slow", "trap", "turret", "mortar", "shelf" };

        /// <summary>Raiders and defenders can't walk through it (walls, gates when closed, turrets, mortars, shelves).</summary>
        public static bool Solid(char c) => c == Wall || c == Embrasure || c == Turret || c == TurretRoom || c == Mortar || c == MortarRoom || c == Shelf;

        /// <summary>Part of a wall line: a wall, an embrasure or a gate in it.</summary>
        public static bool InLine(char c) => c == Wall || c == Embrasure || c == Gate;

        /// <summary>One piece of a template: its role, where its footprint is, and how big a building fits there.</summary>
        public class Piece
        {
            public char key;
            public string role;
            /// <summary>Bottom-left of the footprint, and its size (1x1, or the block a turret or a mortar may take).</summary>
            public int x, z, w = 1, h = 1;

            public bool Contains(int cx, int cz) => cx >= x && cx < x + w && cz >= z && cz < z + h;
        }

        /// <summary>A template's grid, read once: its cells and pieces.</summary>
        public class Grid
        {
            public int W, H;
            /// <summary>[x, z], z = 0 the bottom (inner) row.</summary>
            public char[,] cells;
            public List<Piece> pieces = new List<Piece>();
            /// <summary>The wall line that "close the gap" carries on to either side (z), or -1.</summary>
            public int line = -1;
            /// <summary>The doorway raiders come into the kill zone through (a killbox's, a gate yard's), or -1: grenade reach and turret ranges are measured from it.</summary>
            public int doorX = -1, doorZ = -1;
            public List<string> errors = new List<string>();

            public char At(int x, int z) => x >= 0 && z >= 0 && x < W && z < H ? cells[x, z] : '\0';

            public bool Inside(int x, int z) => x >= 0 && z >= 0 && x < W && z < H;

            public Piece PieceAt(int x, int z)
            {
                foreach (var p in pieces)
                    if (p.Contains(x, z)) return p;
                return null;
            }

            public int Count(string role)
            {
                int n = 0;
                foreach (var p in pieces)
                    if (p.role == role) n++;
                return n;
            }
        }

        /// <summary>
        /// Reads a template's rows (top row first, the side that faces the threat). <paramref name="lineFromTop"/> is the
        /// wall line counted from the top row (0 = the first), or -1 for none. Mistakes go into <see cref="Grid.errors"/>.
        /// </summary>
        public static Grid Parse(IList<string> rows, int lineFromTop = -1)
        {
            var g = new Grid();
            if (rows == null || rows.Count == 0)
            {
                g.errors.Add("the layout has no rows");
                g.cells = new char[0, 0];
                return g;
            }
            g.H = rows.Count;
            g.W = rows[0]?.Length ?? 0;
            g.cells = new char[g.W, g.H];
            for (int r = 0; r < rows.Count; r++)
            {
                string row = rows[r] ?? "";
                if (row.Length != g.W) g.errors.Add($"row {r} is {row.Length} wide, the first is {g.W}");
                int z = g.H - 1 - r;
                for (int x = 0; x < g.W; x++)
                {
                    char c = x < row.Length ? row[x] : Untouched;
                    if (Known.IndexOf(c) < 0)
                    {
                        g.errors.Add($"unknown character '{c}' at row {r}, column {x}");
                        c = Untouched;
                    }
                    g.cells[x, z] = c;
                }
            }
            if (lineFromTop >= 0)
            {
                if (lineFromTop >= g.H) g.errors.Add($"the line row {lineFromTop} is outside the layout");
                else g.line = g.H - 1 - lineFromTop;
            }
            // Pieces: one per cell, except turrets and mortars, whose block is the root and the room marks above and right of it.
            var claimed = new bool[g.W, g.H];
            for (int z = 0; z < g.H; z++)
            {
                for (int x = 0; x < g.W; x++)
                {
                    char c = g.cells[x, z];
                    string role = RoleOf(c);
                    if (role == null) continue;
                    var p = new Piece { key = c, role = role, x = x, z = z };
                    if (c == Turret || c == Mortar)
                    {
                        char room = c == Turret ? TurretRoom : MortarRoom;
                        int w = 1, h = 1;
                        while (g.At(x + w, z) == room) w++;
                        while (true)
                        {
                            bool rowOk = true;
                            for (int i = 0; i < w; i++)
                                if (g.At(x + i, z + h) != room) rowOk = false;
                            if (!rowOk) break;
                            h++;
                        }
                        p.w = w;
                        p.h = h;
                    }
                    for (int i = p.x; i < p.x + p.w; i++)
                        for (int j = p.z; j < p.z + p.h; j++)
                        {
                            if (claimed[i, j]) g.errors.Add($"the {role} at ({x},{z}) overlaps another piece at ({i},{j})");
                            claimed[i, j] = true;
                        }
                    g.pieces.Add(p);
                }
            }
            for (int z = 0; z < g.H; z++)
                for (int x = 0; x < g.W; x++)
                {
                    char c = g.cells[x, z];
                    if ((c == TurretRoom || c == MortarRoom) && !claimed[x, z])
                        g.errors.Add($"'{c}' at ({x},{z}) belongs to no {(c == TurretRoom ? "turret" : "mortar")}: put the {(c == TurretRoom ? 'U' : 'M')} at the bottom-left of its block");
                }
            if (g.pieces.Count == 0) g.errors.Add("the layout has no pieces");
            return g;
        }

        // ---------------- The rules ----------------

        /// <summary>
        /// What's wrong with a template, by the rules every defense follows: gates stand in a wall line with a way through;
        /// every firing position has cover toward the threat, and cover that shelters someone can be shot over; nobody
        /// has to climb anything to reach a firing position; traps lie on the raiders' way, never beside each other, a gate
        /// or a firing position; turrets stand three cells apart with a clear line out; mortars and their shells apart.
        /// A template that closes a gap has a wall line across its whole width, open only at its gates; one that lets
        /// raiders in (a killbox, a trap corridor) has a way through without opening a gate. Empty: nothing wrong.
        /// </summary>
        public static List<string> Problems(Grid g, string kind)
        {
            var list = new List<string>(g.errors);
            if (g.W < 1 || g.H < 1) return list;
            if (g.W > MaxSize || g.H > MaxSize) list.Add($"the layout is {g.W}x{g.H}; keep it within {MaxSize}x{MaxSize}");

            for (int x = 0; x < g.W; x++)
            {
                for (int z = 0; z < g.H; z++)
                {
                    char c = g.cells[x, z];
                    char n = g.At(x, z + 1), s = g.At(x, z - 1), e = g.At(x + 1, z), w = g.At(x - 1, z);
                    switch (c)
                    {
                        case Gate:
                        {
                            bool eastWest = InLine(e) && InLine(w) && Walkable(n) && Walkable(s);
                            bool northSouth = InLine(n) && InLine(s) && Walkable(e) && Walkable(w);
                            if (!eastWest && !northSouth) list.Add($"the gate at ({x},{z}) isn't in a wall line with open ground on both sides");
                            break;
                        }
                        case Firing:
                            if (n != Cover && n != Embrasure && !(n == Wall && Pocket(g, x, z))) list.Add($"the firing position at ({x},{z}) has no cover toward the threat");
                            break;
                        case Cover:
                        case Embrasure:
                            // Cover that shelters someone (a firing position or a turret behind it) must have a free line out.
                            if (s == Firing || s == Turret || s == TurretRoom || s == Mortar || s == MortarRoom)
                            {
                                if (!(n == '\0' || n == Open || n == Untouched || n == Trap || n == Slow))
                                    list.Add($"the {(c == Cover ? "cover" : "embrasure")} at ({x},{z}) faces '{n}' instead of open ground");
                            }
                            if (c == Embrasure && s != Firing) list.Add($"the embrasure at ({x},{z}) has no firing position behind it");
                            if (c == Embrasure && !(InLine(e) && InLine(w)) && !(InLine(n) && InLine(s))) list.Add($"the embrasure at ({x},{z}) isn't in a wall line");
                            break;
                        case Trap:
                            for (int dx = -1; dx <= 1; dx++)
                                for (int dz = -1; dz <= 1; dz++)
                                {
                                    if (dx == 0 && dz == 0) continue;
                                    char o = g.At(x + dx, z + dz);
                                    if (o == Trap && (dx > 0 || (dx == 0 && dz > 0))) list.Add($"traps at ({x},{z}) and ({x + dx},{z + dz}) are next to each other");
                                    if (o == Gate || o == Firing) list.Add($"the trap at ({x},{z}) is beside a {(o == Gate ? "gate" : "firing position")}, where our own people walk");
                                }
                            break;
                    }
                }
            }

            // Turrets: three cells apart, so one that blows up doesn't take the next with it, and a line out.
            var turrets = g.pieces.FindAll(p => p.role == "turret");
            for (int i = 0; i < turrets.Count; i++)
            {
                var a = turrets[i];
                for (int j = i + 1; j < turrets.Count; j++)
                    if (Gap(a, turrets[j]) < 3) list.Add($"the turrets at ({a.x},{a.z}) and ({turrets[j].x},{turrets[j].z}) are closer than three cells");
                bool clear = false;
                for (int i2 = a.x; i2 < a.x + a.w; i2++)
                {
                    char front = g.At(i2, a.z + a.h);
                    if (front == '\0' || front == Open || front == Untouched || front == Cover || front == Trap || front == Slow) clear = true;
                }
                if (!clear) list.Add($"the turret at ({a.x},{a.z}) has no line out toward the threat");
            }
            // Mortars: apart from each other, and their shells further off.
            var mortars = g.pieces.FindAll(p => p.role == "mortar");
            var shelves = g.pieces.FindAll(p => p.role == "shelf");
            for (int i = 0; i < mortars.Count; i++)
            {
                for (int j = i + 1; j < mortars.Count; j++)
                    if (Gap(mortars[i], mortars[j]) < 2) list.Add($"the mortars at ({mortars[i].x},{mortars[i].z}) and ({mortars[j].x},{mortars[j].z}) stand side by side");
                if (kind == "mortar")
                    foreach (var sh in shelves)
                        if (Gap(mortars[i], sh) < 3) list.Add($"the shell shelf at ({sh.x},{sh.z}) is closer than three cells to a mortar");
            }

            // Defenders reach every firing position from the inner side (in a keep, from its door) without climbing over
            // anything or crossing a trap.
            System.Func<int, int, bool> inner = kind == "keep" ? (x, z) => g.cells[x, z] == Gate : (System.Func<int, int, bool>)((x, z) => z == 0);
            var fromInside = Flood(g, inner, c => c == Open || c == Firing || c == Untouched || c == Gate || c == Slow);
            for (int x = 0; x < g.W; x++)
                for (int z = 0; z < g.H; z++)
                    if (g.cells[x, z] == Firing && !fromInside[x, z]) list.Add($"the firing position at ({x},{z}) can't be reached from inside");

            // Traps lie where raiders walk: reachable from the outer side without opening a gate.
            var fromOutside = Flood(g, (x, z) => z == g.H - 1, RaiderWalks);
            foreach (var p in g.pieces)
                if (p.role == "trap" && !fromOutside[p.x, p.z]) list.Add($"the trap at ({p.x},{p.z}) is out of the raiders' way");

            bool throughOpen = Through(g, RaiderWalks);
            bool throughGates = Through(g, c => RaiderWalks(c) || c == Gate);
            switch (kind)
            {
                case "gate":
                case "wall":
                case "palisade":
                case "fallback":
                    if (g.line < 0) list.Add("a template that closes a gap needs its wall line");
                    if (throughOpen) list.Add("raiders can get through without passing a gate");
                    if (g.Count("gate") > 0 && !throughGates) list.Add("its gate leads nowhere: no way through with the gates open");
                    break;
                case "killbox":
                case "traps":
                    if (!throughOpen) list.Add("raiders can't get through: a killbox or trap corridor must let them in, or they break walls elsewhere");
                    break;
                case "keep":
                    for (int x = 0; x < g.W; x++)
                        for (int z = 0; z < g.H; z++)
                            if ((x == 0 || z == 0 || x == g.W - 1 || z == g.H - 1) && !InLine(g.cells[x, z]))
                                list.Add($"the keep's outer ring is open at ({x},{z})");
                    if (g.Count("gate") == 0) list.Add("the keep has no door");
                    break;
            }
            if (g.line >= 0)
            {
                // A line that closes the gap is wall all the way across, open only at its gates; a killbox's has its mouth.
                if (kind != "killbox")
                    for (int x = 0; x < g.W; x++)
                        if (!InLine(g.cells[x, g.line])) list.Add($"the wall line (row z={g.line}) is open at x={x}");
                if (g.W > 0 && (g.cells[0, g.line] != Wall || g.cells[g.W - 1, g.line] != Wall)) list.Add("the wall line must end in walls at both sides, to carry on into the gap");
            }
            return list;
        }

        /// <summary>The tallest and widest a layout may be (a killbox for big raids runs to about 35 rows).</summary>
        public const int MaxSize = 41;

        /// <summary>
        /// A firing position with a wall in front is a pocket when it can lean out past it: on one side the cell beside it
        /// and the cell ahead of that can both be seen over (cover or open ground). Someone there keeps the wall's
        /// three-quarters cover while shooting from beside it.
        /// </summary>
        public static bool Pocket(Grid g, int x, int z)
        {
            for (int d = -1; d <= 1; d += 2)
                if (LeanInto(g.At(x + d, z)) && LeanInto(g.At(x + d, z + 1))) return true;
            return false;
        }

        /// <summary>A cell someone can lean out into, or see past while leaning: cover or open ground, off the layout included.</summary>
        private static bool LeanInto(char c) => c == '\0' || !Solid(c) && c != Gate;

        /// <summary>Ground a gate opens onto: open, a firing position, untouched, off the template, or a second gate behind it (a double gate).</summary>
        private static bool Walkable(char c) => c == '\0' || c == Open || c == Firing || c == Untouched || c == Gate;

        /// <summary>Where raiders walk with the gates shut: anything but walls, gates and the big pieces (they climb cover and fences).</summary>
        public static bool RaiderWalks(char c) => c != '\0' && !Solid(c) && c != Gate;

        /// <summary>Cells between two footprints (0 when they touch or overlap).</summary>
        public static int Gap(Piece a, Piece b)
        {
            int dx = System.Math.Max(0, System.Math.Max(b.x - (a.x + a.w - 1), a.x - (b.x + b.w - 1)) - 1);
            int dz = System.Math.Max(0, System.Math.Max(b.z - (a.z + a.h - 1), a.z - (b.z + b.h - 1)) - 1);
            return System.Math.Max(dx, dz);
        }

        /// <summary>Cells reached from the start cells through cells that pass, four ways.</summary>
        public static bool[,] Flood(Grid g, System.Func<int, int, bool> start, System.Func<char, bool> passes)
        {
            var seen = new bool[g.W, g.H];
            var queue = new Queue<(int, int)>();
            for (int x = 0; x < g.W; x++)
                for (int z = 0; z < g.H; z++)
                    if (start(x, z) && passes(g.cells[x, z]))
                    {
                        seen[x, z] = true;
                        queue.Enqueue((x, z));
                    }
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
                if (!g.Inside(x, z) || seen[x, z] || !passes(g.cells[x, z])) return;
                seen[x, z] = true;
                queue.Enqueue((x, z));
            }
            return seen;
        }

        /// <summary>A way from the outer row to the inner row through cells that pass.</summary>
        public static bool Through(Grid g, System.Func<char, bool> passes)
        {
            var seen = Flood(g, (x, z) => z == g.H - 1, passes);
            for (int x = 0; x < g.W; x++)
                if (seen[x, 0]) return true;
            return false;
        }

        // ---------------- Placing it in the world ----------------

        /// <summary>A template cell once the template is mirrored (west to east) and turned: its offset in the turned rect.</summary>
        public static void ToWorld(Grid g, int x, int z, bool mirror, int turn, out int wx, out int wz) =>
            TemplateMath.Transform(x, z, g.W, g.H, mirror, turn, out wx, out wz);

        /// <summary>The way the template faces once turned (mirroring keeps north north).</summary>
        public static int Outward(bool mirror, int turn) => TemplateMath.TransformFacing(0, mirror, turn);

        /// <summary>
        /// The turn that makes the template face <paramref name="threatFacing"/> (the side raiders come from), and the
        /// anchor (bottom-left of the turned rect) that puts template cell (<paramref name="cx"/>, <paramref name="cz"/>) on
        /// the world cell (<paramref name="worldX"/>, <paramref name="worldZ"/>).
        /// </summary>
        public static void FaceAt(Grid g, int cx, int cz, bool mirror, int threatFacing, int worldX, int worldZ, out int turn, out int anchorX, out int anchorZ)
        {
            turn = TemplateMath.Norm(threatFacing);
            TemplateMath.Transform(cx, cz, g.W, g.H, mirror, turn, out int ox, out int oz);
            anchorX = worldX - ox;
            anchorZ = worldZ - oz;
        }

        /// <summary>
        /// How far a wall line carries on beyond the template's edge, cell by cell outward: the walls to add, and why it
        /// stopped. <paramref name="state"/> answers for the i-th cell beyond the edge (from 0): <see cref="GapOpen"/> a wall can
        /// go there, <see cref="GapClosed"/> rock or a wall already closes it, <see cref="GapGround"/> water or marsh that
        /// takes no wall, <see cref="GapBlocked"/> something else stands there. Stops at <paramref name="max"/> cells.
        /// </summary>
        public static int CarryLine(System.Func<int, int> state, int max, out int stop)
        {
            for (int i = 0; i < max; i++)
            {
                int s = state(i);
                if (s != GapOpen)
                {
                    stop = s;
                    return i;
                }
            }
            stop = GapTooWide;
            return max;
        }

        public const int GapOpen = 0, GapClosed = 1, GapGround = 2, GapBlocked = 3, GapTooWide = 4;
    }
}
