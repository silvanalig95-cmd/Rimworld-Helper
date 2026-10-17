using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>Where a step of a defense line stands: built, half built, next, not for now, or not for this line at all.</summary>
    public enum StepState
    {
        /// <summary>What it asks for stands on the map (planned with the helper or built by hand).</summary>
        Done,
        /// <summary>Some of it stands; the step says what's left.</summary>
        Partly,
        /// <summary>Nothing of it yet, and nothing stops it.</summary>
        Open,
        /// <summary>Not needed yet: raids are still small for it.</summary>
        Later,
        /// <summary>Wanted, but the colony can't do it yet (research, room); the step says why.</summary>
        NotYet,
        /// <summary>Doesn't fit this game or line (traps switched off in the difficulty settings).</summary>
        NotApplicable,
        /// <summary>The player said "not for this line".</summary>
        Skipped
    }

    /// <summary>
    /// The arithmetic of a defense line, with no game types (the line review fills it from the map; the tests from
    /// strings): which way the line faces, how far its row of wall runs from the gate and what it stops at, whether
    /// raiders can walk round it to the beds when the gate is shut (and where they would cross), how many doors stand
    /// between it and the beds, the distance the cover goes back, which step comes next, a rough strength to compare
    /// with the raids, and the lessons a raid leaves. Facings are 0 north, 1 east, 2 south, 3 west, the side raiders come
    /// from; "right" is the right hand of someone standing at the gate looking out.
    /// </summary>
    public static class DefenseLineMath
    {
        public static int Norm(int facing) => ((facing % 4) + 4) % 4;

        /// <summary>The step outward, toward the raiders.</summary>
        public static void Outward(int facing, out int ox, out int oz)
        {
            switch (Norm(facing))
            {
                case 0: ox = 0; oz = 1; break;
                case 1: ox = 1; oz = 0; break;
                case 2: ox = 0; oz = -1; break;
                default: ox = -1; oz = 0; break;
            }
        }

        /// <summary>The step to the right along the line (looking out from the gate).</summary>
        public static void Side(int facing, out int sx, out int sz)
        {
            Outward(facing, out int ox, out int oz);
            sx = oz;
            sz = -ox;
        }

        /// <summary>How far a cell lies in front of the gate (negative: behind it, on the colony's side).</summary>
        public static int Ahead(int dx, int dz, int facing)
        {
            Outward(facing, out int ox, out int oz);
            return dx * ox + dz * oz;
        }

        /// <summary>How far a cell lies to the right of the gate along the line (negative: to the left).</summary>
        public static int Lateral(int dx, int dz, int facing)
        {
            Side(facing, out int sx, out int sz);
            return dx * sx + dz * sz;
        }

        /// <summary>The facing that points along a vector, by its longer axis (north when it has none).</summary>
        public static int FacingOf(int dx, int dz)
        {
            if (dx == 0 && dz == 0) return 0;
            if (System.Math.Abs(dz) >= System.Math.Abs(dx)) return dz >= 0 ? 0 : 2;
            return dx >= 0 ? 1 : 3;
        }

        // ---------------- The line's row ----------------

        /// <summary>One side of the line's row, from the gate outward along the line.</summary>
        public struct RowSide
        {
            /// <summary>Cells of the colony's own line (walls, doors) running on from the gate.</summary>
            public int built;
            /// <summary>Open cells after them, until something closes the row.</summary>
            public int open;
            /// <summary>What closes it: Rock, Water, Wall, Door, Other, Unknown; Edge for the map's edge or nothing within reach.</summary>
            public byte end;
            /// <summary>Rock, water, a wall or a door closes the row (not the map's edge, and not too far).</summary>
            public bool closed;
            /// <summary>The first open cell after the built part (where a wall would carry on), or -1.</summary>
            public int firstOpen;
        }

        /// <summary>
        /// Along the line's row from the gate (sign -1 to the left, +1 to the right): the colony's walls and doors that run
        /// on from it, then the open ground until rock, water, a wall or a door closes it, at most <paramref name="max"/> cells.
        /// </summary>
        public static RowSide ScanRow(DefenseMath.MapGrid m, int gx, int gz, int facing, int sign, int max)
        {
            Side(facing, out int sx, out int sz);
            sx *= sign;
            sz *= sign;
            var r = new RowSide { end = DefenseMath.Edge, firstOpen = -1 };
            int k = 1;
            for (; k <= max; k++)
            {
                int x = gx + sx * k, z = gz + sz * k;
                if (!m.In(x, z)) return r;
                byte kind = m.kind[m.Index(x, z)];
                if (kind == DefenseMath.Wall || kind == DefenseMath.Door) r.built++;
                else break;
            }
            for (; k <= max; k++)
            {
                int x = gx + sx * k, z = gz + sz * k;
                if (!m.In(x, z)) return r;
                int i = m.Index(x, z);
                byte kind = m.kind[i];
                if (!m.Passable(i) || kind == DefenseMath.Door)
                {
                    r.end = kind;
                    r.closed = kind != DefenseMath.Edge;
                    return r;
                }
                if (r.firstOpen < 0) r.firstOpen = i;
                r.open++;
            }
            return r;
        }

        /// <summary>The gate and the colony's walls and doors that run on from it along the row on both sides.</summary>
        public static List<int> LineCells(DefenseMath.MapGrid m, int gx, int gz, int facing, int max)
        {
            var list = new List<int>();
            if (!m.In(gx, gz)) return list;
            list.Add(m.Index(gx, gz));
            Side(facing, out int sx, out int sz);
            for (int sign = -1; sign <= 1; sign += 2)
                for (int k = 1; k <= max; k++)
                {
                    int x = gx + sx * sign * k, z = gz + sz * sign * k;
                    if (!m.In(x, z)) break;
                    int i = m.Index(x, z);
                    byte kind = m.kind[i];
                    if (kind != DefenseMath.Wall && kind != DefenseMath.Door) break;
                    list.Add(i);
                }
            return list;
        }

        // ---------------- The way round ----------------

        /// <summary>Whether raiders at a line's gate can walk on to the beds another way, and where.</summary>
        public class Leak
        {
            /// <summary>The cell in front of the gate the test starts from, or -1 (nothing to stand on there).</summary>
            public int front = -1;
            /// <summary>The cost of the way on through the gate held open, and of the best way round with it shut (-1: none).</summary>
            public int direct = -1, detour = -1;
            /// <summary>A way round that's cheap enough that raiders would take it.</summary>
            public bool found;
            /// <summary>It's even cheaper than through the open gate: they'd go round with the gate open too.</summary>
            public bool evenOpen;
            /// <summary>The way round passes a door of the colony's (another gate), not open ground.</summary>
            public bool viaDoor;
            public int door = -1;
            /// <summary>Where the way round passes the line's row, and how far to the right of the gate (negative: left).</summary>
            public int cross = -1, lateral;
            /// <summary>The way round, from the front cell to the beds.</summary>
            public List<int> path = new List<int>();
            /// <summary>With the gate shut, nothing reaches the beds without breaking in: the line holds the way in alone.</summary>
            public bool sealedShut;
            /// <summary>Nothing reaches the beds from in front of the gate even with it open: the line isn't on a way in.</summary>
            public bool noWayOn;
        }

        /// <summary>How much longer a way round may be (in path cost) and still be the one raiders take when the gate is shut.</summary>
        public static int LeakSlack(int direct) => System.Math.Max(35 * DefenseMath.Ortho, direct / 2);

        /// <summary>Where people sleep, or the home area when nothing is known of that: what the ways in lead to.</summary>
        public static bool[] Targets(DefenseMath.MapGrid m)
        {
            int n = m.w * m.h;
            for (int i = 0; i < n; i++)
                if (m.core[i] && m.Passable(i)) return m.core;
            return m.home;
        }

        /// <summary>
        /// With the line's gate held open, and again with it (and every door in the line) shut: the cost from the cell in
        /// front of the gate to the beds. A way round when shut that costs no more than <see cref="LeakSlack"/> above the
        /// way through is where raiders go when they find the gate shut, or always, when it's even cheaper. The grid is
        /// changed while it works and put back afterwards.
        /// </summary>
        public static Leak FindLeak(DefenseMath.MapGrid m, int gx, int gz, int facing, int maxRow = 20)
        {
            var leak = new Leak();
            if (!m.In(gx, gz)) return leak;
            Outward(facing, out int ox, out int oz);
            for (int k = 1; k <= 3 && leak.front < 0; k++)
            {
                int x = gx + ox * k, z = gz + oz * k;
                if (!m.In(x, z)) break;
                int i = m.Index(x, z);
                if (m.Passable(i) && m.kind[i] != DefenseMath.Door) leak.front = i;
            }
            if (leak.front < 0) return leak;
            var targets = Targets(m);
            var cells = LineCells(m, gx, gz, facing, maxRow);
            var saved = new byte[cells.Count];
            for (int c = 0; c < cells.Count; c++) saved[c] = m.kind[cells[c]];
            try
            {
                // Held open: the gate and the doors in the line are open ground (the walls stay walls).
                for (int c = 0; c < cells.Count; c++)
                    if (saved[c] == DefenseMath.Door || c == 0 && m.Passable(cells[c])) m.kind[cells[c]] = DefenseMath.Open;
                var open = DefenseMath.Distances(m, targets);
                leak.direct = open[leak.front];
                // Shut: the whole line is wall.
                for (int c = 0; c < cells.Count; c++) m.kind[cells[c]] = DefenseMath.Wall;
                var shut = DefenseMath.Distances(m, targets);
                leak.detour = shut[leak.front];
                if (leak.direct < 0 && leak.detour < 0)
                {
                    leak.noWayOn = true;
                    return leak;
                }
                if (leak.detour < 0)
                {
                    leak.sealedShut = true;
                    return leak;
                }
                int slack = LeakSlack(System.Math.Max(0, leak.direct));
                leak.found = leak.direct < 0 || leak.detour - leak.direct <= slack;
                leak.evenOpen = leak.direct >= 0 && leak.detour <= leak.direct;
                if (!leak.found) return leak;
                int budget = m.w * m.h;
                var way = DefenseMath.Walk(m, shut, leak.front, ref budget);
                if (way != null) leak.path = way.cells;
                foreach (int c in leak.path)
                {
                    int x = c % m.w, z = c / m.w;
                    if (leak.cross < 0 && Ahead(x - gx, z - gz, facing) <= 0)
                    {
                        leak.cross = c;
                        leak.lateral = Lateral(x - gx, z - gz, facing);
                    }
                    if (leak.door < 0 && m.kind[c] == DefenseMath.Door && !NextToCore(m, c))
                    {
                        leak.door = c;
                        leak.viaDoor = true;
                    }
                }
            }
            finally
            {
                for (int c = 0; c < cells.Count; c++) m.kind[cells[c]] = saved[c];
            }
            return leak;
        }

        private static readonly int[] DX = { 0, 1, 0, -1 };
        private static readonly int[] DZ = { 1, 0, -1, 0 };

        private static bool NextToCore(DefenseMath.MapGrid m, int c)
        {
            int x = c % m.w, z = c / m.w;
            for (int d = 0; d < 4; d++)
            {
                int nx = x + DX[d], nz = z + DZ[d];
                if (m.In(nx, nz) && m.core[m.Index(nx, nz)]) return true;
            }
            return false;
        }

        /// <summary>
        /// Doors on the easiest way from just inside the gate to the beds, not counting the gate itself or a door into a
        /// room people sleep in: a line to fall back to when it's one or more. <paramref name="toCore"/> is the cost field
        /// to the beds (<see cref="DefenseMath.Distances"/> from <see cref="Targets"/>).
        /// </summary>
        public static int DoorsBehind(DefenseMath.MapGrid m, int[] toCore, int gx, int gz, int facing)
        {
            Outward(facing, out int ox, out int oz);
            int start = -1;
            for (int k = 1; k <= 3 && start < 0; k++)
            {
                int x = gx - ox * k, z = gz - oz * k;
                if (!m.In(x, z)) break;
                int i = m.Index(x, z);
                if (m.Passable(i) && m.kind[i] != DefenseMath.Door && toCore[i] >= 0) start = i;
            }
            if (start < 0) return 0;
            int budget = m.w * m.h;
            var way = DefenseMath.Walk(m, toCore, start, ref budget);
            if (way == null) return 0;
            int gate = m.Index(gx, gz), doors = 0;
            foreach (int c in way.cells)
                if (c != gate && m.kind[c] == DefenseMath.Door && !NextToCore(m, c)) doors++;
            return doors;
        }

        /// <summary>The cells across a gap through a cell, along the line (sideways to the facing): walkable cells until something closes it on each side, at most <paramref name="maxSide"/> each way.</summary>
        public static List<int> SpanAcross(DefenseMath.MapGrid m, int x, int z, int facing, int maxSide)
        {
            var list = new List<int>();
            if (!m.In(x, z)) return list;
            list.Add(m.Index(x, z));
            Side(facing, out int sx, out int sz);
            for (int sign = -1; sign <= 1; sign += 2)
                for (int k = 1; k <= maxSide; k++)
                {
                    int nx = x + sx * sign * k, nz = z + sz * sign * k;
                    if (!m.In(nx, nz)) break;
                    int i = m.Index(nx, nz);
                    if (!m.Passable(i)) break;
                    if (sign < 0) list.Insert(0, i);
                    else list.Add(i);
                }
            return list;
        }

        // ---------------- Distance and range ----------------

        public enum Reach
        {
            Good,
            /// <summary>Closer than five cells: a melee rush or a grenade thrown through the gate reaches the shooters.</summary>
            TooClose,
            /// <summary>Past medium range: hits fall off.</summary>
            Far,
            /// <summary>Past the reach of the colony's best weapon.</summary>
            TooFar
        }

        /// <summary>The distance the cover goes back from the gate: about a quarter of the best weapon's reach, six to ten cells.</summary>
        public static int CoverDistance(float bestRange)
        {
            if (bestRange <= 0f) return 6;
            int d = (int)System.Math.Round(bestRange * 0.28f);
            return System.Math.Max(6, System.Math.Min(10, d));
        }

        /// <summary>How a distance from the shooters to the gate suits the colony's weapons.</summary>
        public static Reach ReachOf(float distance, float bestRange)
        {
            if (distance < 5f) return Reach.TooClose;
            if (bestRange > 0f && distance > bestRange) return Reach.TooFar;
            if (distance > 25f || (bestRange > 0f && distance > bestRange * 0.75f)) return Reach.Far;
            return Reach.Good;
        }

        // ---------------- Steps ----------------

        /// <summary>A plan's progress: the share of its pieces that stand (0 when it has none).</summary>
        public static float Progress(int present, int total) => total <= 0 ? 0f : System.Math.Max(0f, System.Math.Min(1f, present / (float)total));

        /// <summary>Done from <paramref name="doneAt"/>, partly done from the first piece, else open.</summary>
        public static StepState Band(float progress, float doneAt = 0.85f)
        {
            if (progress >= doneAt) return StepState.Done;
            if (progress > 0.001f) return StepState.Partly;
            return StepState.Open;
        }

        /// <summary>The step the card shows first: the first open or half-done one, else the first that waits on something, else the first for later; -1 when all are done or set aside.</summary>
        public static int Current(IList<StepState> states)
        {
            for (int i = 0; i < states.Count; i++)
                if (states[i] == StepState.Open || states[i] == StepState.Partly) return i;
            for (int i = 0; i < states.Count; i++)
                if (states[i] == StepState.NotYet) return i;
            for (int i = 0; i < states.Count; i++)
                if (states[i] == StepState.Later) return i;
            return -1;
        }

        /// <summary>Steps done out of those that count (not skipped, not for this line).</summary>
        public static void Tally(IList<StepState> states, out int done, out int counted)
        {
            done = counted = 0;
            foreach (var s in states)
            {
                if (s == StepState.Skipped || s == StepState.NotApplicable) continue;
                counted++;
                if (s == StepState.Done) done++;
            }
        }

        // ---------------- Strength ----------------

        /// <summary>What a line has to hold with.</summary>
        public class StrengthIn
        {
            /// <summary>Each fighter's worth, as the advisor counts it (30 plus 8 a skill level, a third more with a ranged weapon).</summary>
            public List<float> fighters = new List<float>();
            /// <summary>Firing positions behind cover that see the gate.</summary>
            public int positions;
            /// <summary>Working turrets near the line and their worth together.</summary>
            public int turrets;
            public float turretWorth;
            public int traps;
            public bool killbox;
            /// <summary>The gap is closed with a gate in it and there's no cheap way round.</summary>
            public bool closed;
            public bool leak;
            /// <summary>Things in the field that raiders shelter behind.</summary>
            public int obstacles;
            public Reach reach = Reach.Good;
        }

        public class StrengthOut
        {
            public float total, people, covered, turrets, traps, factor = 1f;
            public int atPosts;
        }

        public const float CoverFactor = 1.6f, KillboxFactor = 1.5f, ClosedFactor = 1.2f, LeakFactor = 0.6f, TrapWorth = 25f;
        public const int MaxTraps = 20;

        /// <summary>
        /// A rough strength in raid points, simple enough to explain: the fighters (those at firing positions that see the
        /// gate count 1.6 times), the working turrets and up to twenty traps; then half again for a killbox, a fifth more for
        /// a line with no way round, 0.6 times with one; less for cover left in the field (2% a thing, at most a quarter) and
        /// for cover too close to the gate or too far back.
        /// </summary>
        public static StrengthOut Strength(StrengthIn s)
        {
            var o = new StrengthOut();
            var list = new List<float>(s.fighters);
            list.Sort((a, b) => b.CompareTo(a));
            o.atPosts = System.Math.Min(System.Math.Max(0, s.positions), list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                float f = System.Math.Max(0f, list[i]);
                if (i < o.atPosts)
                {
                    o.covered += f * CoverFactor;
                    o.people += f * CoverFactor;
                }
                else o.people += f;
            }
            o.turrets = System.Math.Max(0f, s.turretWorth);
            o.traps = System.Math.Min(System.Math.Max(0, s.traps), MaxTraps) * TrapWorth;
            float factor = 1f;
            if (s.killbox) factor *= KillboxFactor;
            if (s.leak) factor *= LeakFactor;
            else if (s.closed) factor *= ClosedFactor;
            factor *= 1f - System.Math.Min(0.25f, 0.02f * System.Math.Max(0, s.obstacles));
            switch (s.reach)
            {
                case Reach.TooClose: factor *= 0.85f; break;
                case Reach.Far: factor *= 0.9f; break;
                case Reach.TooFar: factor *= 0.7f; break;
            }
            o.factor = factor;
            o.total = (o.people + o.turrets + o.traps) * factor;
            return o;
        }

        /// <summary>A strength said aloud: to the nearest 50 points from 200 on, to the nearest 10 below.</summary>
        public static int Rounded(float points)
        {
            if (points <= 0f) return 0;
            if (points < 200f) return (int)System.Math.Round(points / 10f) * 10;
            return (int)System.Math.Round(points / 50f) * 50;
        }

        /// <summary>How the strength compares with the raids now: 2 holds with room to spare, 1 holds, 0 close, -1 would break.</summary>
        public static int Verdict(float strength, float raidPoints)
        {
            if (raidPoints <= 0f) return 2;
            float r = strength / raidPoints;
            if (r >= 1.5f) return 2;
            if (r >= 1f) return 1;
            if (r >= 0.75f) return 0;
            return -1;
        }

        // ---------------- After a raid ----------------

        /// <summary>How a raid met a line, counted while it lasted.</summary>
        public class RaidTally
        {
            /// <summary>Raiders who came in through the gate, round the line on its left or right, through the walls (sappers, breachers), or who were inside without passing it (drop pods).</summary>
            public int through, roundLeft, roundRight, broke, inside;
            /// <summary>Colonists downed in front of the line, at the gate, at the cover left and right of it, in the kill zone or among the buildings behind.</summary>
            public int downedFront, downedGate, downedCoverLeft, downedCoverRight, downedZone, downedBehind;

            public int Round => roundLeft + roundRight;
            public int Raiders => through + Round + broke + inside;
            public int Downed => downedFront + downedGate + downedCoverLeft + downedCoverRight + downedZone + downedBehind;
        }

        public enum Lesson
        {
            /// <summary>The line did its job: they came through the gate and nobody fell.</summary>
            Held,
            /// <summary>Some went round: close the way round.</summary>
            Leak,
            /// <summary>Some broke through the walls: two thick, stone, a second ring.</summary>
            Harden,
            /// <summary>People fell at the cover on one side: more cover there, or a turret, or fewer raiders reaching it.</summary>
            CoverLeft,
            CoverRight,
            /// <summary>People fell in front of the line: fight from behind it.</summary>
            StayBehind,
            /// <summary>Raiders got among the buildings, or people fell behind the line: a line to fall back to.</summary>
            Fallback
        }

        /// <summary>The one to three things a raid says about a line, the weightiest first.</summary>
        public static List<Lesson> Lessons(RaidTally t, int max = 3)
        {
            var found = new List<(Lesson lesson, float weight)>();
            if (t.Round > 0) found.Add((Lesson.Leak, 10f + t.Round * 2f));
            if (t.broke > 0) found.Add((Lesson.Harden, 8f + t.broke * 1.5f));
            if (t.downedCoverLeft > 0) found.Add((Lesson.CoverLeft, 6f + t.downedCoverLeft * 3f));
            if (t.downedCoverRight > 0) found.Add((Lesson.CoverRight, 6f + t.downedCoverRight * 3f));
            if (t.downedFront > 0) found.Add((Lesson.StayBehind, 5f + t.downedFront * 3f));
            if (t.inside > 0 || t.downedBehind > 0 || t.downedZone > 0) found.Add((Lesson.Fallback, 4f + t.inside + (t.downedBehind + t.downedZone) * 2f));
            if (found.Count == 0 && t.through > 0) found.Add((Lesson.Held, 1f));
            found.Sort((a, b) => b.weight.CompareTo(a.weight));
            var list = new List<Lesson>();
            foreach (var (lesson, _) in found)
            {
                if (list.Count >= max) break;
                list.Add(lesson);
            }
            return list;
        }

        /// <summary>Whether a raider's walk between two looks passed within <paramref name="radius"/> cells of a point (the gate).</summary>
        public static bool PassedNear(int ax, int az, int bx, int bz, int px, int pz, float radius)
        {
            float dx = bx - ax, dz = bz - az;
            float len2 = dx * dx + dz * dz;
            float t = len2 <= 0f ? 0f : ((px - ax) * dx + (pz - az) * dz) / len2;
            t = System.Math.Max(0f, System.Math.Min(1f, t));
            float cx = ax + t * dx - px, cz = az + t * dz - pz;
            return cx * cx + cz * cz <= radius * radius;
        }
    }
}
