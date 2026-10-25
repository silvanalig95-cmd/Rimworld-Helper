using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// What raiders and defenders can see and hit in a defense layout, and the cover each has, worked out the way RimWorld
    /// 1.6 does it, with no game types, so the tests can check every plan the helper draws (<see cref="DefenseShapes"/>) and
    /// every fixed one. It follows the game's own code line for line: GenSight.LineOfSight (a line steps sideways or forward,
    /// never diagonally, and only a full wall stops it), ShootLeanUtility (someone beside a wall leans one cell out past its
    /// corner when the cell diagonally ahead is open, and shoots over cover right beside them) and CoverUtility (cover comes
    /// from the eight cells around the target, full against a shot from straight ahead and less the wider the angle, a
    /// diagonal cell counting as if the angle were 1.75 times wider, a third as much when the shooter stands right at it and
    /// two thirds one cell off, several combining as 1 - (1 - a)(1 - b)). Cover is always reckoned at the target's own cell,
    /// even while it leans out to fire.
    /// <para>
    /// Layout characters as in <see cref="DefenseLayout"/>. A gate is taken as held open (raiders walk through and see
    /// through it), as it should be in a raid. Ground the plan leaves alone, and anything off the layout, is taken as open
    /// ground (a check that nobody can see through it then errs on the safe side), except that a wall line carries on
    /// past the layout's sides, as "close the gap" builds it.
    /// </para>
    /// </summary>
    public static class DefenseSight
    {
        /// <summary>Grenades and molotovs reach 12.9 cells; a little more, so a step forward doesn't bring a firing position in.</summary>
        public const float GrenadeReach = 13.4f;

        /// <summary>The most cover a raider may have while able to shoot at a firing position (a decoy turret beside him gives about 0.3).</summary>
        public const float MaxRaiderCover = 0.45f;

        /// <summary>Destruction blast of a 1x1 turret (the mini-turret), and of a 2x2 one (autocannon, uranium slug), from its bottom-left cell.</summary>
        public const float SmallBlast = 3.9f, BigBlast = 5.9f;

        /// <summary>An autocannon can't hit anything closer than this.</summary>
        public const float BigMinRange = 8.9f;

        // N, E, S, W, SE, NE, NW, SW: the game's GenAdj.AdjacentCells order.
        private static readonly int[] AdjX = { 0, 1, 0, -1, 1, 1, -1, -1 };
        private static readonly int[] AdjZ = { 1, 0, -1, 0, -1, 1, 1, -1 };

        /// <summary>The cover a cell's piece gives (its fill), 0 for none.</summary>
        public static float CoverOf(char c)
        {
            switch (c)
            {
                case DefenseLayout.Wall: return 0.75f;
                // A palisade embrasure: three-quarters (a castle's gives a little more).
                case DefenseLayout.Embrasure: return 0.75f;
                case DefenseLayout.Cover: return 0.55f;
                // Something that slows: a fence (0.25) where the colony has fences, else sandbags; the worse case for us.
                case DefenseLayout.Slow: return 0.55f;
                case DefenseLayout.Turret:
                case DefenseLayout.TurretRoom: return 0.4f;
                case DefenseLayout.Mortar:
                case DefenseLayout.MortarRoom: return 0.5f;
                case DefenseLayout.Shelf: return 0.4f;
                default: return 0f;
            }
        }

        /// <summary>Only a full wall stops sight (a held-open gate, cover, turrets and people don't).</summary>
        public static bool SeeOver(char c) => c != DefenseLayout.Wall;

        /// <summary>A cell of the layout; beyond its sides on the wall line, the wall carries on to the rock (the gap is closed); anywhere else off it, open ground.</summary>
        private static char At(DefenseLayout.Grid g, int x, int z)
        {
            if (g.Inside(x, z)) return g.cells[x, z];
            return g.line >= 0 && z == g.line && (x < 0 || x >= g.W) ? DefenseLayout.Wall : DefenseLayout.Open;
        }

        private static bool SeeOver(DefenseLayout.Grid g, int x, int z) => SeeOver(At(g, x, z));

        /// <summary>The game's AngleFlat: 0 north (+z), 90 east (+x).</summary>
        public static float AngleFlat(int dx, int dz)
        {
            if (dx == 0 && dz == 0) return 0f;
            float a = (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
            return a < 0f ? a + 360f : a;
        }

        private static float AngleDifference(float a, float b)
        {
            float d = Math.Abs(a - b) % 360f;
            return d > 180f ? 360f - d : d;
        }

        /// <summary>GenSight.LineOfSight from one cell to another (the first cell not looked at when <paramref name="skipFirst"/>).</summary>
        public static bool LineOfSight(DefenseLayout.Grid g, int x0, int z0, int x1, int z1, bool skipFirst = true)
        {
            bool flag = x0 != x1 ? x0 < x1 : z0 < z1;
            int dx = Math.Abs(x1 - x0), dz = Math.Abs(z1 - z0);
            int n = 1 + dx + dz;
            int sx = x1 > x0 ? 1 : -1, sz = z1 > z0 ? 1 : -1;
            dx *= 4;
            dz *= 4;
            int err = dx / 2 - dz / 2;
            int x = x0, z = z0;
            while (n > 1)
            {
                if ((!skipFirst || x != x0 || z != z0) && !SeeOver(g, x, z)) return false;
                if (err > 0 || (err == 0 && flag))
                {
                    x += sx;
                    err -= dz;
                }
                else
                {
                    z += sz;
                    err += dx;
                }
                n--;
            }
            return true;
        }

        /// <summary>ShootLeanUtility.LeanShootingSourcesFromTo: the cells someone at (x, z) can shoot (or be shot) from toward (tx, tz).</summary>
        public static void LeanSources(DefenseLayout.Grid g, int x, int z, int tx, int tz, List<(int, int)> into)
        {
            into.Clear();
            float a = AngleFlat(tx - x, tz - z);
            bool north = a > 270f || a < 90f, south = a > 90f && a < 270f, west = a > 180f, east = a < 180f;
            var b = new bool[8];
            for (int i = 0; i < 8; i++) b[i] = !SeeOver(g, x + AdjX[i], z + AdjZ[i]);
            if (!b[1] && ((b[0] && !b[5] && north) || (b[2] && !b[4] && south))) into.Add((x + 1, z));
            if (!b[3] && ((b[0] && !b[6] && north) || (b[2] && !b[7] && south))) into.Add((x - 1, z));
            if (!b[2] && ((b[3] && !b[7] && west) || (b[1] && !b[4] && east))) into.Add((x, z - 1));
            if (!b[0] && ((b[3] && !b[6] && west) || (b[1] && !b[5] && east))) into.Add((x, z + 1));
            if (SeeOver(g, x, z)) into.Add((x, z));
            for (int j = 0; j < 4; j++)
            {
                if (b[j]) continue;
                bool facing = j == 0 ? north : j == 1 ? east : j == 2 ? south : west;
                if (!facing) continue;
                int cx = x + AdjX[j], cz = z + AdjZ[j];
                if (CoverOf(At(g, cx, cz)) > 0f) into.Add((cx, cz));
            }
        }

        [ThreadStatic] private static List<(int, int)> fromCells, toCells;

        /// <summary>Whether someone at the first cell can shoot someone at the second (Verb.TryFindShootLineFromTo for a person at a person, range aside).</summary>
        public static bool CanHit(DefenseLayout.Grid g, int sx, int sz, int tx, int tz)
        {
            fromCells ??= new List<(int, int)>();
            toCells ??= new List<(int, int)>();
            LeanSources(g, tx, tz, sx, sz, toCells);
            LeanSources(g, sx, sz, tx, tz, fromCells);
            fromCells.Insert(0, (sx, sz));
            foreach (var (fx, fz) in fromCells)
                foreach (var (dx, dz) in toCells)
                    if (LineOfSight(g, fx, fz, dx, dz)) return true;
            return false;
        }

        /// <summary>A turret (which never leans) at one of its cells shooting at someone: CanHit from that cell only.</summary>
        public static bool TurretCanHit(DefenseLayout.Grid g, int sx, int sz, int tx, int tz)
        {
            toCells ??= new List<(int, int)>();
            LeanSources(g, tx, tz, sx, sz, toCells);
            foreach (var (dx, dz) in toCells)
                if (LineOfSight(g, sx, sz, dx, dz)) return true;
            return false;
        }

        /// <summary>CoverUtility.CalculateOverallBlockChance: the chance a shot from (sx, sz) at someone on (tx, tz) is stopped by cover around them.</summary>
        public static float Cover(DefenseLayout.Grid g, int tx, int tz, int sx, int sz)
        {
            if (tx == sx && tz == sz) return 0f;
            float total = 0f;
            float toShooter = AngleFlat(sx - tx, sz - tz);
            for (int i = 0; i < 8; i++)
            {
                int cx = tx + AdjX[i], cz = tz + AdjZ[i];
                if (cx == sx && cz == sz) continue;
                float block = CoverOf(At(g, cx, cz));
                if (block <= 0f) continue;
                float d = AngleDifference(AngleFlat(AdjX[i], AdjZ[i]), toShooter);
                if (i >= 4) d *= 1.75f;
                if (d < 15f) block *= 1f;
                else if (d < 27f) block *= 0.8f;
                else if (d < 40f) block *= 0.6f;
                else if (d < 52f) block *= 0.4f;
                else if (d < 65f) block *= 0.2f;
                else continue;
                float dist = (float)Math.Sqrt((sx - cx) * (sx - cx) + (sz - cz) * (sz - cz));
                if (dist < 1.9f) block *= 0.3333f;
                else if (dist < 2.9f) block *= 0.66666f;
                total += (1f - total) * block;
            }
            return total;
        }

        public static float Distance(int ax, int az, int bx, int bz) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

        // ---------------- Who stands where ----------------

        /// <summary>Where raiders can walk with every gate held open: from the outer (top) row inward, over cover and fences.</summary>
        public static bool[,] RaiderCells(DefenseLayout.Grid g) =>
            DefenseLayout.Flood(g, (x, z) => z == g.H - 1, c => DefenseLayout.RaiderWalks(c) || c == DefenseLayout.Gate);

        public static List<(int x, int z)> Firing(DefenseLayout.Grid g)
        {
            var list = new List<(int, int)>();
            for (int x = 0; x < g.W; x++)
                for (int z = 0; z < g.H; z++)
                    if (g.cells[x, z] == DefenseLayout.Firing) list.Add((x, z));
            return list;
        }

        /// <summary>
        /// How well a layout holds up, the way the game plays it: the worst cover any raider has while able to shoot at a
        /// firing position (and where, and at whom), and the share of the cells raiders cross, before they reach the firing
        /// positions, that at least one and at least two firing positions can shoot at. Cells right at the firing positions
        /// and behind all of them (where a raider has already reached the line or got past it) are left out.
        /// </summary>
        public class Reading
        {
            public float worstCover;
            public int worstX = -1, worstZ = -1, worstFx = -1, worstFz = -1;
            public int crossed, seenByOne, seenByTwo;
            public float nearestDoor = float.MaxValue;
        }

        public static Reading Read(DefenseLayout.Grid g)
        {
            var r = new Reading();
            var raider = RaiderCells(g);
            var firing = Firing(g);
            if (firing.Count == 0) return r;
            int rearmost = int.MaxValue;
            foreach (var (_, fz) in firing) rearmost = Math.Min(rearmost, fz);
            for (int x = 0; x < g.W; x++)
            {
                for (int z = 0; z < g.H; z++)
                {
                    if (!raider[x, z] || z < rearmost) continue;
                    bool atLine = false;
                    foreach (var (fx, fz) in firing)
                        if (Math.Abs(fx - x) <= 2 && Math.Abs(fz - z) <= 2) atLine = true;
                    if (atLine) continue;
                    int seen = 0;
                    foreach (var (fx, fz) in firing)
                    {
                        if (!CanHit(g, x, z, fx, fz)) continue;
                        seen++;
                        float c = Cover(g, x, z, fx, fz);
                        if (c > r.worstCover)
                        {
                            r.worstCover = c;
                            r.worstX = x;
                            r.worstZ = z;
                            r.worstFx = fx;
                            r.worstFz = fz;
                        }
                    }
                    r.crossed++;
                    if (seen >= 1) r.seenByOne++;
                    if (seen >= 2) r.seenByTwo++;
                }
            }
            if (g.doorX >= 0)
                foreach (var (fx, fz) in firing)
                    r.nearestDoor = Math.Min(r.nearestDoor, Distance(fx, fz, g.doorX, g.doorZ));
            return r;
        }

        /// <summary>
        /// What's wrong with a layout by how raids really play out: raiders who can shoot at a firing position from good
        /// cover (a corner they lean round, the walls of a doorway), firing positions within a grenade's throw of the
        /// doorway when <paramref name="grenades"/>, turrets that would take each other or the shooters with them when they
        /// blow, and a big turret too close to the doorway to fire at it. Empty: nothing wrong.
        /// </summary>
        public static List<string> Problems(DefenseLayout.Grid g, bool grenades)
        {
            var list = new List<string>();
            var reading = Read(g);
            if (reading.worstCover > MaxRaiderCover)
                list.Add($"a raider at ({reading.worstX},{reading.worstZ}) can shoot at the firing position ({reading.worstFx},{reading.worstFz}) from {reading.worstCover * 100f:0}% cover");
            if (grenades && g.doorX >= 0 && reading.nearestDoor < GrenadeReach)
                list.Add($"a firing position is {reading.nearestDoor:0.0} cells from the doorway, within a grenade's throw");
            var turrets = g.pieces.FindAll(p => p.role == "turret");
            var firing = Firing(g);
            for (int i = 0; i < turrets.Count; i++)
            {
                var t = turrets[i];
                float blast = t.w > 1 || t.h > 1 ? BigBlast : SmallBlast;
                foreach (var (fx, fz) in firing)
                    if (Distance(t.x, t.z, fx, fz) < blast)
                        list.Add($"the firing position ({fx},{fz}) is within the blast of the turret at ({t.x},{t.z})");
                for (int j = i + 1; j < turrets.Count; j++)
                {
                    var o = turrets[j];
                    float b = Math.Max(blast, o.w > 1 || o.h > 1 ? BigBlast : SmallBlast);
                    if (Distance(t.x, t.z, o.x, o.z) < b) list.Add($"the turrets at ({t.x},{t.z}) and ({o.x},{o.z}) would take each other with them");
                }
                if ((t.w > 1 || t.h > 1) && g.doorX >= 0 && Distance(t.x, t.z, g.doorX, g.doorZ) < BigMinRange + 0.5f)
                    list.Add($"the big turret at ({t.x},{t.z}) is too close to the doorway to fire at it");
            }
            return list;
        }
    }
}
