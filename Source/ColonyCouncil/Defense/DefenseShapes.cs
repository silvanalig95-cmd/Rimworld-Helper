using System;
using System.Collections.Generic;
using System.Text;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's numbers a defense layout is drawn for (<see cref="DefenseShapes"/>): how many people shoot from it,
    /// how far their weapons reach, what turret the colony would put in it, whether the raiders it meets throw grenades,
    /// how big raids are. The player can add or take away shooters and depth in the planner.
    /// </summary>
    public class DefenseShapeSpec
    {
        /// <summary>Firing positions wanted: the colony's ranged fighters, at least four.</summary>
        public int shooters = 4;
        /// <summary>Rows of kill zone the player added (or took away, below 0) on top of what the numbers give.</summary>
        public int depth;
        /// <summary>The turret the colony would build: 0 none, 1 one cell (a mini-turret, a scorpio), 2 a 2x2 one (an autocannon).</summary>
        public int turretSize;
        public int turrets;
        /// <summary>The raiders it meets throw grenades (pirates and the empire, not tribes).</summary>
        public bool grenades;
        /// <summary>Bows and spears: barricades, palisades, one-thick walls.</summary>
        public bool lowTech;
        /// <summary>The longest reach among the colony's shooters.</summary>
        public float range = 30.9f;
        public float raidPoints = 500f;

        /// <summary>Raids of 3000 points and more: a wider gap in the line, a longer and slowed way in.</summary>
        public bool Big => raidPoints >= 3000f;

        public DefenseShapeSpec Copy() => (DefenseShapeSpec)MemberwiseClone();

        /// <summary>What tells two specs apart for the shapes they give (raid size only by its band).</summary>
        public string Key => $"{shooters}|{depth}|{turretSize}|{turrets}|{(grenades ? 1 : 0)}|{(lowTech ? 1 : 0)}|{(int)(range * 2f)}|{(Big ? 1 : 0)}|{TrapCount(raidPoints)}";

        /// <summary>Spike traps for a trap corridor: four for small raids, up to eight for the biggest.</summary>
        public static int TrapCount(float points) => Math.Max(4, Math.Min(8, 4 + (int)(points / 1000f)));

        public override string ToString() => Key;

        /// <summary>The spec as a placed plan keeps it in the save: every number, exactly.</summary>
        public string Save() => string.Join(",", shooters, depth, turretSize, turrets, grenades ? 1 : 0, lowTech ? 1 : 0,
            range.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), ((int)raidPoints).ToString(System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>A spec from <see cref="Save"/>, or null when there's none (a plan placed before layouts were drawn) or it can't be read.</summary>
        public static DefenseShapeSpec Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var parts = text.Split(',');
            if (parts.Length < 8) return null;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            try
            {
                return new DefenseShapeSpec
                {
                    shooters = int.Parse(parts[0], inv),
                    depth = int.Parse(parts[1], inv),
                    turretSize = int.Parse(parts[2], inv),
                    turrets = int.Parse(parts[3], inv),
                    grenades = parts[4] == "1",
                    lowTech = parts[5] == "1",
                    range = float.Parse(parts[6], inv),
                    raidPoints = float.Parse(parts[7], inv)
                };
            }
            catch (FormatException)
            {
                return null;
            }
            catch (OverflowException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// A defense template's own shape (its &lt;shape&gt; in the XML): which generator draws it and how. Everything left
    /// at 0 or "auto" is worked out from the colony's numbers.
    /// </summary>
    public class DefenseShapeOptions
    {
        /// <summary>box (killbox, tunnel, baffled gate, palisade yard), sideLines (lines off to the sides behind a gate), firingLine, post, nest, traps.</summary>
        public string family = "box";
        /// <summary>The way in is a gate in the wall line, held open in a raid, rather than an opening.</summary>
        public bool gate;
        /// <summary>An open row in front of the wall line, kept clear.</summary>
        public bool front;
        /// <summary>Rows of wall at the line (two for a perimeter wall).</summary>
        public int thick = 1;
        /// <summary>Box: cells down from the mouth before the way in turns, and cells across to the straight; 0 = automatic.</summary>
        public int drop, leg;
        /// <summary>Box: a long outer tunnel, slowed every other cell (a mountain's).</summary>
        public bool tunnel;
        /// <summary>pocket (wall in front, sandbags around), plain (a line of sandbags), embrasure (a wall of embrasures with a gate in a corner), or auto.</summary>
        public string cover = "auto";
        /// <summary>Side lines: gate, cell, gate through the wall, so both doors are never open together.</summary>
        public bool airlock;
        /// <summary>Side lines and firing lines: drawn without the gate they stand behind (placed behind one the colony has).</summary>
        public bool behindGate;
        /// <summary>Firing positions; 0 = from the colony's shooters.</summary>
        public int shooters;
    }

    /// <summary>A generated layout and what it was drawn for.</summary>
    public class DefenseShape
    {
        public DefenseLayout.Grid grid;
        public List<string> rows = new List<string>();
        public int lineFromTop = -1;
        /// <summary>Cells from the gate it stands behind to its front row (posts and lines placed behind a gate), or in front of the gap (a trap corridor).</summary>
        public int behind;
        /// <summary>Cells from the doorway (or gate) straight back to the firing positions, and their number.</summary>
        public int depth, shooters;
        /// <summary>The cells raiders walk straight down before the doorway (the box), and how far the firing positions are from it.</summary>
        public int straight;
        public float nearest;
        public DefenseShapeSpec spec;
    }

    /// <summary>
    /// Defense layouts drawn from the colony's numbers instead of fixed in the XML, by what the research in
    /// docs/defense-research found to hold in 1.6, and checked as they are drawn with <see cref="DefenseSight"/>:
    /// <list type="bullet">
    /// <item>A box (killbox, tunnel killbox, baffled gate, palisade yard): a way in that turns twice, then a straight of
    /// four or five cells so the corner before the last turn can't see the firing positions; a bare box as wide as the
    /// shooters need, no shooter within two cells of the axis, all of them within about 24° of it so the doorway gives
    /// a raider no cover; deep enough to cross under fire and, against grenades, more than a throw from the doorway;
    /// double walls once past bows; decoy turrets on the axis; traps and things that slow in the way in, never in the box.</item>
    /// <item>Side lines behind a gate: each half off to its side, far enough out that nobody outside the gate sees it,
    /// close enough in that a raider in the gate has little cover from its sides.</item>
    /// <item>A firing line and a post of pockets (a wall in front, sandbags on the front corners and beside), a turret
    /// nest spaced by the turret's blast, a trap corridor with a fence beside each trap.</item>
    /// </list>
    /// Pure, with no game types, so the tests draw every shape for a range of colonies and check each with the layout
    /// rules and the sight checker.
    /// </summary>
    public static class DefenseShapes
    {
        public const string Box = "box", SideLines = "sideLines", FiringLine = "firingLine", Post = "post", Nest = "nest", Traps = "traps";

        /// <summary>The widest a firing position may be off the axis, as a share of how far it is back: tan 24°, inside which the doorway gives no cover.</summary>
        public const float WithinCone = 0.45f;

        /// <summary>The tallest and widest a drawn layout may be.</summary>
        public const int MaxSize = 41;

        public static readonly string[] Families = { Box, SideLines, FiringLine, Post, Nest, Traps };

        /// <summary>Draws the shape the options ask for, sized for the colony.</summary>
        public static DefenseShape Build(DefenseShapeOptions o, DefenseShapeSpec s)
        {
            o ??= new DefenseShapeOptions();
            s ??= new DefenseShapeSpec();
            DefenseShape shape;
            switch (o.family)
            {
                case SideLines: shape = BuildSideLines(o, s); break;
                case FiringLine: shape = BuildFiringLine(o, s); break;
                case Post: shape = BuildPost(o, s); break;
                case Nest: shape = BuildNest(o, s); break;
                case Traps: shape = BuildTraps(o, s); break;
                default: shape = BuildBox(o, s); break;
            }
            shape.spec = s;
            return shape;
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        private static int PerSide(DefenseShapeOptions o, DefenseShapeSpec s, int lo, int hi) => Clamp(((o.shooters > 0 ? o.shooters : s.shooters) + 1) / 2, lo, hi);

        /// <summary>How far back a firing position <paramref name="offset"/> cells off the axis must stand to be out of a grenade's throw from the doorway.</summary>
        public static int GrenadeDepth(int offset) => (int)Math.Ceiling(Math.Sqrt(Math.Max(0f, DefenseSight.GrenadeReach * DefenseSight.GrenadeReach - offset * offset)));

        /// <summary>How far back a firing position <paramref name="offset"/> cells off the axis may stand and still reach the doorway with <paramref name="range"/>.</summary>
        public static int RangeDepth(int offset, float range) => (int)Math.Floor(Math.Sqrt(Math.Max(0f, (range - 0.5f) * (range - 0.5f) - offset * offset)));

        /// <summary>The fewest cells back a firing position <paramref name="offset"/> cells off the axis may stand, to stay inside the cone where the doorway gives no cover.</summary>
        public static int ConeDepth(int offset) => (int)Math.Ceiling(offset / WithinCone);

        private class Canvas
        {
            public readonly int W, H;
            public readonly char[,] m;

            public Canvas(int w, int h, char fill = DefenseLayout.Untouched)
            {
                W = w;
                H = h;
                m = new char[h, w];
                for (int r = 0; r < h; r++)
                    for (int c = 0; c < w; c++) m[r, c] = fill;
            }

            public bool In(int r, int c) => r >= 0 && c >= 0 && r < H && c < W;

            public char this[int r, int c]
            {
                get => In(r, c) ? m[r, c] : '\0';
                set
                {
                    if (In(r, c)) m[r, c] = value;
                }
            }

            public void Row(int r, char ch)
            {
                for (int c = 0; c < W; c++) m[r, c] = ch;
            }

            public List<string> Rows()
            {
                var list = new List<string>();
                var sb = new StringBuilder();
                for (int r = 0; r < H; r++)
                {
                    sb.Clear();
                    for (int c = 0; c < W; c++) sb.Append(m[r, c]);
                    list.Add(sb.ToString());
                }
                return list;
            }
        }

        private static DefenseShape Finish(Canvas cv, int lineFromTop, int doorRow, int doorCol)
        {
            var shape = new DefenseShape { rows = cv.Rows(), lineFromTop = lineFromTop };
            shape.grid = DefenseLayout.Parse(shape.rows, lineFromTop);
            if (doorRow >= 0)
            {
                shape.grid.doorX = doorCol;
                shape.grid.doorZ = cv.H - 1 - doorRow;
            }
            return shape;
        }

        // ---------------- The box ----------------

        /// <summary>
        /// A killbox, a tunnel killbox, a baffled gate or a palisade yard. From the top: an open row in front (a gate's),
        /// the wall line with the mouth, the way in (down from the mouth, across, then the straight to the doorway), the
        /// bare box, the line of cover and the firing positions, and one open row behind, toward the colony.
        /// </summary>
        private static DefenseShape BuildBox(DefenseShapeOptions o, DefenseShapeSpec s)
        {
            string style = o.cover == "auto" || string.IsNullOrEmpty(o.cover) ? (s.lowTech ? "plain" : "pocket") : o.cover;
            bool pocket = style == "pocket", emb = style == "embrasure";
            int perSide = PerSide(o, s, 2, 3);
            // A wider gap on the axis for big raids: room for the melee fighters and the haulers, and the corner stays blind.
            int first = s.Big && pocket ? 4 : 3;
            int maxOff = first + 2 * (perSide - 1);
            int hw = maxOff + (pocket ? 2 : 1);
            int wallT = s.lowTech ? 1 : 2;
            int W = 2 * hw + 1 + 2 * wallT, ax = W / 2;
            int drop = o.drop > 0 ? o.drop : o.tunnel || s.Big ? 4 : 2;
            int leg = Clamp(o.leg > 0 ? o.leg : s.Big ? 6 : 4, 2, ax - 1);
            int straight = (pocket || emb ? 5 : 4) + (s.Big ? 1 : 0);
            int minA = Math.Max(6, ConeDepth(maxOff));
            int a = Math.Max(s.lowTech ? 10 : 12, minA);
            if (s.grenades) a = Math.Max(a, GrenadeDepth(first));
            a = Math.Max(minA, a + s.depth);
            int maxA = Math.Max(minA, RangeDepth(maxOff, s.range));
            if (a > maxA) a = maxA;

            DefenseShape shape = null;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                shape = DrawBox(o, s, pocket, emb, perSide, first, hw, wallT, W, ax, drop, leg, straight, a);
                var reading = DefenseSight.Read(shape.grid);
                if (reading.worstCover <= DefenseSight.MaxRaiderCover) break;
                // Seen from the way in: a longer straight. From the box: a little further back (narrower angles).
                int worstRow = shape.grid.H - 1 - reading.worstZ;
                int doorRow = shape.grid.H - 1 - shape.grid.doorZ;
                if (worstRow < doorRow - 1 || a >= Math.Min(maxA + 2, MaxSize)) straight++;
                else a++;
            }
            return shape;
        }

        private static DefenseShape DrawBox(DefenseShapeOptions o, DefenseShapeSpec s, bool pocket, bool emb, int perSide, int first, int hw, int wallT, int W, int ax,
            int drop, int leg, int straight, int a)
        {
            int rl = o.front ? 1 : 0;
            int thick = Math.Max(1, o.thick);
            int rt = rl + Math.Max(1, drop);
            int rd = rt + straight;
            int rf = rd + a, rc = rf - 1;
            // A 2x2 turret stands behind the gap in the line, its corner (where its blast is measured) six rows behind the shooters.
            bool bigTurret = s.turretSize >= 2 && s.turrets > 0;
            int rb = rf + 6;
            int H = bigTurret ? rb + 2 : rf + 2;
            int mx = ax - leg;
            var cv = new Canvas(W, H);
            if (o.front) cv.Row(0, DefenseLayout.Open);

            // The way in: down from the mouth, across, down the straight to the doorway.
            var path = new List<(int r, int c)>();
            for (int r = rl; r <= rt; r++) path.Add((r, mx));
            for (int c = mx + 1; c <= ax; c++) path.Add((rt, c));
            for (int r = rt + 1; r <= rd; r++) path.Add((r, ax));
            var onPath = new HashSet<(int, int)>(path);

            // The block the way in runs through: wall line, sides, doorway wall (two thick past bows), every cell round the way in.
            for (int r = rl; r <= rd; r++)
            {
                cv[r, 0] = DefenseLayout.Wall;
                cv[r, W - 1] = DefenseLayout.Wall;
            }
            for (int r = rl; r < rl + thick && r <= rd; r++) cv.Row(r, DefenseLayout.Wall);
            cv.Row(rd, DefenseLayout.Wall);
            if (wallT > 1 && rd - 1 > rt) cv.Row(rd - 1, DefenseLayout.Wall);
            foreach (var (r, c) in path)
                for (int dr = -1; dr <= 1; dr++)
                    for (int dc = -1; dc <= 1; dc++)
                        if (r + dr >= rl && r + dr <= rd && cv[r + dr, c + dc] == DefenseLayout.Untouched) cv[r + dr, c + dc] = DefenseLayout.Wall;
            foreach (var (r, c) in path) cv[r, c] = DefenseLayout.Open;
            if (o.gate) cv[rl, mx] = DefenseLayout.Gate;

            // The box: bare, walled at the sides (two thick past bows).
            for (int r = rd + 1; r < H; r++)
                for (int c = 0; c < W; c++)
                    cv[r, c] = c < wallT || c >= W - wallT ? DefenseLayout.Wall : DefenseLayout.Open;

            // The line of cover and the firing positions behind it.
            for (int k = 0; k < perSide; k++)
            {
                int off = first + 2 * k;
                foreach (int side in new[] { -1, 1 })
                {
                    int c = ax + side * off;
                    cv[rf, c] = DefenseLayout.Firing;
                    if (pocket)
                    {
                        cv[rc, c] = DefenseLayout.Wall;
                        cv[rc, c - 1] = cv[rc, c + 1] = DefenseLayout.Cover;
                        cv[rf, c - 1] = cv[rf, c + 1] = DefenseLayout.Cover;
                    }
                }
            }
            if (!pocket)
            {
                int maxOff = first + 2 * (perSide - 1);
                for (int c = wallT; c < W - wallT; c++)
                {
                    int off = Math.Abs(c - ax);
                    if (emb) cv[rc, c] = cv[rf, c] == DefenseLayout.Firing ? DefenseLayout.Embrasure : DefenseLayout.Wall;
                    else if (off >= first - 1 && off <= maxOff) cv[rc, c] = DefenseLayout.Cover;
                }
                // A wall of embrasures: the way on is a gate in its corner, with melee fighters behind it.
                if (emb) cv[rc, ax + hw] = DefenseLayout.Gate;
            }

            // Decoy turrets on the axis, in front of the line: they draw the fire, and none stands near a shooter or the doorway.
            // A big one would give a raider beside it half cover, so it stands behind the gap and shoots through it.
            if (bigTurret)
            {
                cv[rb, ax - 1] = DefenseLayout.Turret;
                cv[rb, ax] = cv[rb - 1, ax - 1] = cv[rb - 1, ax] = DefenseLayout.TurretRoom;
            }
            else if (s.turretSize == 1)
                for (int i = 0; i < Math.Min(s.turrets, 2); i++)
                {
                    int r = rc - 6 - 4 * i;
                    if (r >= rd + 3) cv[r, ax] = DefenseLayout.Turret;
                }

            // The way in: things that slow every other cell in its outer part (a tunnel, big raids), and traps further in.
            // Not behind a gate: that's the colony's own way in and out.
            var walk = o.gate ? new List<(int r, int c)>() : path.FindAll(p => p.r > rl && !(p.c == ax && p.r > rt) && !(p.r == rt && p.c == ax));
            if (o.tunnel || s.Big)
                for (int i = 1; i < walk.Count - 2; i += 2) cv[walk[i].r, walk[i].c] = DefenseLayout.Slow;
            int traps = s.Big ? 3 : 2;
            for (int i = walk.Count - 1; i >= 1 && traps > 0; i--)
            {
                var (r, c) = walk[i];
                if (cv[r, c] != DefenseLayout.Open) continue;
                bool near = false;
                for (int dr = -1; dr <= 1; dr++)
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        char n = cv[r + dr, c + dc];
                        if (n == DefenseLayout.Trap || n == DefenseLayout.Gate || n == DefenseLayout.Firing) near = true;
                    }
                if (near) continue;
                cv[r, c] = DefenseLayout.Trap;
                traps--;
            }

            var shape = Finish(cv, rl, rd, ax);
            shape.depth = a;
            shape.shooters = 2 * perSide;
            shape.straight = straight;
            shape.nearest = (float)Math.Sqrt(a * a + first * first);
            return shape;
        }

        // ---------------- Side lines behind a gate ----------------

        /// <summary>
        /// Two short lines of cover off to the sides behind a gate (and the gate in its wall, unless the plan stands
        /// behind one the colony has): each half's firing positions in a row with cover in front, a wall at its outer end,
        /// the cover turned toward the gate at its inner end. Tried closest first, it is the first placement where nobody
        /// outside the gate can shoot at them from good cover, a raider in the gate has little, and, against grenades,
        /// they are more than a throw from the gate.
        /// </summary>
        private static DefenseShape BuildSideLines(DefenseShapeOptions o, DefenseShapeSpec s)
        {
            int perSide = PerSide(o, s, 2, 4);
            // Closest first: the least ground, then a little further back rather than wider. Reach and grenades are
            // counted before anything is drawn (the nearest firing position to the gate is the inner one of a half).
            int aMin = Clamp(6 + s.depth, 5, 14);
            var tries = new List<(int a, int o1, float score)>();
            for (int a = aMin; a <= 16; a++)
                for (int o1 = 2; o1 <= 12; o1++)
                {
                    float far = (float)Math.Sqrt(a * a + (o1 + perSide - 1) * (o1 + perSide - 1));
                    if (far > s.range - 0.5f) continue;
                    if (s.grenades && Math.Sqrt(a * a + o1 * o1) < DefenseSight.GrenadeReach) continue;
                    tries.Add((a, o1, (2 * (o1 + perSide) + 1) * (a + 5) + o1 * 0.5f));
                }
            tries.Sort((x, y) => x.score.CompareTo(y.score));
            DefenseShape best = null;
            int bestA = 6, bestO = 3;
            for (int pass = 0; pass < 2 && best == null; pass++)
            {
                float allowed = pass == 0 ? 0.3f : DefenseSight.MaxRaiderCover;
                foreach (var (a, o1, _) in tries)
                {
                    var shape = DrawSideLines(o, perSide, a, o1, true);
                    var reading = DefenseSight.Read(shape.grid);
                    if (reading.worstCover > allowed) continue;
                    best = shape;
                    best.nearest = reading.nearestDoor;
                    bestA = a;
                    bestO = o1;
                    break;
                }
            }
            // Nothing fits the colony's reach: the plain one, which the review then sizes up.
            if (best == null)
            {
                best = DrawSideLines(o, perSide, bestA, bestO, true);
                best.nearest = DefenseSight.Read(best.grid).nearestDoor;
            }
            var result = o.behindGate ? DrawSideLines(o, perSide, bestA, bestO, false) : best;
            result.depth = bestA;
            result.shooters = 2 * perSide;
            result.nearest = best.nearest;
            result.behind = o.behindGate ? bestA - 2 : 0;
            return result;
        }

        /// <summary>
        /// The gate (an airlock or a plain one, <paramref name="withGate"/>) and the side lines <paramref name="a"/> cells
        /// behind it, their inner firing positions <paramref name="o1"/> off the axis.
        /// </summary>
        private static DefenseShape DrawSideLines(DefenseShapeOptions o, int perSide, int a, int o1, bool withGate)
        {
            int capOff = o1 + perSide;
            int W = 2 * capOff + 1, ax = W / 2;
            bool airlock = o.airlock && !o.behindGate;
            int thick = o.behindGate ? 1 : Math.Max(1, o.thick);
            // A row in front of the gate, so the checks see the raiders who wait outside it.
            int rl = o.front || o.behindGate ? 1 : 0;
            // The gate's inner door: the plain gate, or an airlock's second door one cell in.
            int inner = airlock ? rl + 2 : rl;
            int rf = inner + a, rc = rf - 1;
            int H = rf + 2;
            var cv = new Canvas(W, H, DefenseLayout.Open);
            for (int r = rl; r < rl + thick; r++) cv.Row(r, DefenseLayout.Wall);
            cv[rl, ax] = DefenseLayout.Gate;
            if (airlock)
            {
                if (thick < 2) cv.Row(rl + 1, DefenseLayout.Wall);
                cv[rl + 1, ax] = DefenseLayout.Open;
                cv[rl + 1, ax - 1] = cv[rl + 1, ax + 1] = DefenseLayout.Wall;
                cv[rl + 2, ax - 1] = cv[rl + 2, ax + 1] = DefenseLayout.Wall;
                cv[rl + 2, ax] = DefenseLayout.Gate;
            }
            else
                for (int r = rl + 1; r < rl + thick; r++) cv[r, ax] = DefenseLayout.Gate;
            foreach (int side in new[] { -1, 1 })
            {
                for (int off = o1 - 1; off < capOff; off++) cv[rc, ax + side * off] = DefenseLayout.Cover;
                for (int off = o1; off < capOff; off++) cv[rf, ax + side * off] = DefenseLayout.Firing;
                cv[rc, ax + side * capOff] = cv[rf, ax + side * capOff] = DefenseLayout.Wall;
            }
            if (withGate || !o.behindGate)
            {
                var shape = Finish(cv, rl, inner, ax);
                return shape;
            }
            // Behind a gate the colony has: from one open row in front of the cover down.
            var cut = new Canvas(W, H - (rc - 1));
            for (int r = rc - 1; r < H; r++)
                for (int c = 0; c < W; c++) cut[r - (rc - 1), c] = cv[r, c];
            return Finish(cut, -1, -1, ax);
        }

        // ---------------- A firing line and a post of pockets ----------------

        /// <summary>
        /// Firing positions in pockets, a wall in front of each and cover on its front corners and both sides, two
        /// halves with a gap on the axis, for where raiders come straight on (a corridor's end, a straight gateway).
        /// Placed far enough behind the opening to be out of a grenade's throw from it when raiders throw them.
        /// </summary>
        private static DefenseShape BuildFiringLine(DefenseShapeOptions o, DefenseShapeSpec s)
        {
            int perSide = PerSide(o, s, 2, 4);
            int first = 2;
            int maxOff = first + 2 * (perSide - 1);
            int W = 2 * (maxOff + 1) + 1, ax = W / 2;
            var cv = new Canvas(W, 4, DefenseLayout.Open);
            for (int k = 0; k < perSide; k++)
                foreach (int side in new[] { -1, 1 })
                {
                    int c = ax + side * (first + 2 * k);
                    cv[2, c] = DefenseLayout.Firing;
                    cv[1, c] = DefenseLayout.Wall;
                    cv[1, c - 1] = cv[1, c + 1] = cv[2, c - 1] = cv[2, c + 1] = DefenseLayout.Cover;
                }
            var shape = Finish(cv, -1, -1, ax);
            int a = Math.Max(s.lowTech ? 8 : 9, ConeDepth(maxOff));
            if (s.grenades) a = Math.Max(a, GrenadeDepth(first));
            a = Clamp(a + s.depth, 5, Math.Max(5, RangeDepth(maxOff, s.range)));
            shape.depth = a;
            shape.behind = a - 2;
            shape.shooters = 2 * perSide;
            shape.nearest = (float)Math.Sqrt(a * a + first * first);
            return shape;
        }

        /// <summary>Three pockets side by side with a wall on each flank, for the longest guns or bows, well back.</summary>
        private static DefenseShape BuildPost(DefenseShapeOptions o, DefenseShapeSpec s)
        {
            int n = Clamp(o.shooters > 0 ? o.shooters : 3, 2, 5);
            int W = 2 * n + 1;
            var cv = new Canvas(W, 3, DefenseLayout.Open);
            for (int c = 0; c < W; c++)
            {
                bool f = c % 2 == 1;
                cv[0, c] = f ? DefenseLayout.Wall : DefenseLayout.Cover;
                cv[1, c] = f ? DefenseLayout.Firing : DefenseLayout.Cover;
            }
            cv[1, 0] = cv[1, W - 1] = cv[2, 0] = cv[2, W - 1] = DefenseLayout.Wall;
            var shape = Finish(cv, -1, -1, W / 2);
            int a = Math.Max(10, s.grenades ? GrenadeDepth(0) : 0) + s.depth;
            shape.depth = Clamp(a, 6, Math.Max(6, (int)(s.range * 0.75f)));
            shape.behind = shape.depth - 1;
            shape.shooters = n;
            return shape;
        }

        // ---------------- A turret nest ----------------

        /// <summary>
        /// Turrets side by side, each further from the next than its blast reaches (four cells for a one-cell turret,
        /// six for a 2x2, measured from its bottom-left cell, as the game does), cover in front of the small ones (it
        /// does nothing for a big one), a wall on each flank. Nobody stands within a blast of them.
        /// </summary>
        private static DefenseShape BuildNest(DefenseShapeOptions o, DefenseShapeSpec s)
        {
            bool big = s.turretSize >= 2;
            int n = Clamp(s.turrets > 0 ? s.turrets : 2, 2, 3);
            int size = big ? 2 : 1, step = big ? 7 : 4;
            int W = 1 + (n - 1) * step + size + 1;
            var cv = new Canvas(W, size + 2, DefenseLayout.Open);
            for (int i = 0; i < n; i++)
            {
                int c0 = 1 + i * step;
                if (!big) cv[0, c0] = DefenseLayout.Cover;
                for (int dc = 0; dc < size; dc++)
                    for (int dr = 0; dr < size; dr++) cv[1 + dr, c0 + dc] = DefenseLayout.TurretRoom;
                cv[size, c0] = DefenseLayout.Turret;
            }
            for (int r = 1; r <= size; r++) cv[r, 0] = cv[r, W - 1] = DefenseLayout.Wall;
            var shape = Finish(cv, -1, -1, W / 2);
            shape.depth = big ? 12 : 6;
            shape.behind = shape.depth;
            return shape;
        }

        // ---------------- A trap corridor ----------------

        /// <summary>
        /// A lane two wide, walled: a trap every other cell down one side, a fence beside each trap on the other, the
        /// cells between left open. Raiders who don't know a trap take the open side of the fence row, the trap's;
        /// those who know one (a smart raid knows a few) have to climb the fence to get round it, and the colony's own
        /// people, who know every trap, take the fence side. Four traps for small raids, up to eight for the biggest.
        /// </summary>
        private static DefenseShape BuildTraps(DefenseShapeOptions o, DefenseShapeSpec s)
        {
            int n = DefenseShapeSpec.TrapCount(s.raidPoints);
            int H = 2 * n + 1;
            var cv = new Canvas(4, H, DefenseLayout.Open);
            for (int r = 0; r < H; r++) cv[r, 0] = cv[r, 3] = DefenseLayout.Wall;
            for (int i = 0; i < n; i++)
            {
                cv[1 + 2 * i, 1] = DefenseLayout.Trap;
                cv[1 + 2 * i, 2] = DefenseLayout.Slow;
            }
            var shape = Finish(cv, -1, -1, 1);
            shape.behind = 2;
            return shape;
        }
    }
}
