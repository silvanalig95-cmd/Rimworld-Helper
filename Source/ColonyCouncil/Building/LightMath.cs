using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Pure rules for lighting a planned room, with no game types, so the tests run them the way the game does.
    ///
    /// How a wall lamp hangs (RimWorld 1.6 wall attachments: <c>building.isAttachment</c> and
    /// <c>Placeworker_AttachedToWall</c>): the lamp stands on the floor cell in front of its wall, and its rotation points
    /// at that wall. The game looks for the wall at <c>cell + rot.FacingCell</c> (<c>GenConstruct.GetWallAttachedTo</c>),
    /// draws the lamp on it, and the light falls back into the room. So a lamp on the north wall of a room stands in the
    /// top row of floor and faces north. The wall has to take attachments (<c>building.supportsWallAttachments</c>): a
    /// built or planned wall does; a door, a cooler or vent set in the wall, open ground or water don't. Two attachments
    /// can't share a cell facing the same way.
    ///
    /// Which light a room gets, and how many (every threshold is here):
    /// <list type="bullet">
    /// <item>Small rooms (up to <see cref="SmallRoomMaxCells"/> floor cells) and medium ones (up to
    /// <see cref="MediumRoomMaxCells"/>) hang wall lamps when the colony can build one.</item>
    /// <item>Large rooms keep the plan's standing lamps out on the floor, which light the middle the walls can't reach.
    /// They hang wall lamps, several of them round the walls, only when no standing lamp can be built or the player picks
    /// a wall lamp in the planner.</item>
    /// <item>A lamp lights a cell well out to <see cref="WellLitShare"/> of its glow radius. Wall lamps go up, one at a
    /// time where they light the most still-dark floor, until every cell is lit well, up to <see cref="MaxWallLamps"/>.</item>
    /// <item>A room split by partition walls or inner doors (a cell block, a suite with its own bathroom) is lit area by
    /// area: each area the plan gives a light gets its own lamps.</item>
    /// </list>
    /// Coordinates are a template's interior cells ((0,0) is the bottom-left floor cell, the door in the south wall at
    /// doorX). Facings are 0 = north, 1 = east, 2 = south, 3 = west, like Rot4.
    /// </summary>
    public static class LightMath
    {
        // ---------------- Thresholds ----------------

        /// <summary>Up to this many floor cells a room is small (a bedroom, a one-person workshop, a cell).</summary>
        public const int SmallRoomMaxCells = 20;

        /// <summary>Up to this many it is medium (a dining room for six, a barracks, a workshop). Above it, large.</summary>
        public const int MediumRoomMaxCells = 48;

        /// <summary>A lamp lights a cell well enough to work and read by out to this share of its glow radius.</summary>
        public const float WellLitShare = 0.5f;

        /// <summary>The most wall lamps an area of each size gets, however weak the lamp.</summary>
        public const int MaxLampsSmall = 2, MaxLampsMedium = 4, MaxLampsLarge = 8;

        public enum RoomSize
        {
            Small,
            Medium,
            Large
        }

        public static RoomSize SizeOf(int cells) =>
            cells <= SmallRoomMaxCells ? RoomSize.Small : cells <= MediumRoomMaxCells ? RoomSize.Medium : RoomSize.Large;

        /// <summary>The most wall lamps an area of this many cells gets.</summary>
        public static int MaxWallLamps(int cells)
        {
            switch (SizeOf(cells))
            {
                case RoomSize.Small: return MaxLampsSmall;
                case RoomSize.Medium: return MaxLampsMedium;
                default: return MaxLampsLarge;
            }
        }

        /// <summary>How far a lamp with this glow radius lights the floor well.</summary>
        public static float Reach(float glowRadius) => glowRadius > 0f ? glowRadius * WellLitShare : 0f;

        // ---------------- Wall or floor ----------------

        /// <summary>What the planner was told about the room's lights: nothing (by the room's size), wall lamps, or a standing lamp.</summary>
        public const int Auto = 0, AskWall = 1, AskFloor = 2;

        /// <summary>
        /// The room hangs wall lamps instead of its floor lights. Never without a wall lamp the colony can build. Asked
        /// for in the planner: as asked (a standing lamp that doesn't fit anywhere still gives way to wall lamps).
        /// Otherwise by size: small and medium rooms hang them; a large room only when it has no standing lamp.
        /// </summary>
        public static bool UseWallLamps(int cells, bool wallLampBuildable, bool floorLampPlaced, int asked)
        {
            if (!wallLampBuildable) return false;
            if (asked == AskWall) return true;
            if (asked == AskFloor) return !floorLampPlaced;
            return SizeOf(cells) != RoomSize.Large || !floorLampPlaced;
        }

        // ---------------- Where lamps hang ----------------

        /// <summary>A floor cell a lamp can stand on, the facing that points at its wall, and the lit area it's in.</summary>
        public struct Spot
        {
            public int x, z, facing, area;

            public override string ToString() => $"({x},{z}) facing {facing}";
        }

        /// <summary>The wall lamps of a planned room: every place one could hang, and which were chosen.</summary>
        public class WallLampPlan
        {
            public int w, h;
            public float reach;
            public List<Spot> candidates = new List<Spot>();
            /// <summary>Indices into <see cref="candidates"/>, one per lamp.</summary>
            public List<int> chosen = new List<int>();
            /// <summary>The floor cells of each area of the room, to light.</summary>
            public List<List<(int x, int z)>> areas = new List<List<(int x, int z)>>();
            /// <summary>The area of each floor cell; -1 under a partition wall or an inner door.</summary>
            public int[,] areaOf;
            /// <summary>Areas that got at least one lamp.</summary>
            public HashSet<int> lit = new HashSet<int>();

            public int AreaAt(int x, int z) => areaOf != null && x >= 0 && z >= 0 && x < w && z < h ? areaOf[x, z] : -1;

            public IEnumerable<Spot> Lamps
            {
                get
                {
                    foreach (int i in chosen) yield return candidates[i];
                }
            }
        }

        /// <summary>
        /// Splits the floor into the areas partition walls and inner doors close off (light doesn't pass a closed door).
        /// Cells under a wall or door piece are -1.
        /// </summary>
        public static int[,] Areas(int w, int h, List<TemplateLayout.Piece> pieces, out int count)
        {
            var area = new int[w, h];
            for (int x = 0; x < w; x++)
                for (int z = 0; z < h; z++)
                    area[x, z] = -2;
            foreach (var p in pieces)
            {
                if (p.dropped || p.inWall || !(p.isWall || p.isDoor)) continue;
                for (int x = p.minX; x < p.minX + p.w; x++)
                    for (int z = p.minZ; z < p.minZ + p.h; z++)
                        if (x >= 0 && z >= 0 && x < w && z < h) area[x, z] = -1;
            }
            count = 0;
            var queue = new Queue<(int, int)>();
            for (int sx = 0; sx < w; sx++)
            {
                for (int sz = 0; sz < h; sz++)
                {
                    if (area[sx, sz] != -2) continue;
                    int id = count++;
                    area[sx, sz] = id;
                    queue.Enqueue((sx, sz));
                    while (queue.Count > 0)
                    {
                        var (x, z) = queue.Dequeue();
                        for (int f = 0; f < 4; f++)
                        {
                            TemplateLayout.Step(f, out int dx, out int dz);
                            int nx = x + dx, nz = z + dz;
                            if (nx < 0 || nz < 0 || nx >= w || nz >= h || area[nx, nz] != -2) continue;
                            area[nx, nz] = id;
                            queue.Enqueue((nx, nz));
                        }
                    }
                }
            }
            return area;
        }

        /// <summary>
        /// Every place a wall lamp could hang in a laid-out room: a free floor cell (no piece on it, no work spot, not
        /// beside a door, and one people can walk to from the door) next to a wall it can hang on, facing that wall. The
        /// walls are the room's own (never the door
        /// or a piece set in the wall, like a cooler or vent) and its partition walls (never an inner door).
        /// </summary>
        public static List<Spot> WallSpots(int w, int h, int doorX, List<TemplateLayout.Piece> pieces, int[,] area)
        {
            var spots = new List<Spot>();
            if (w <= 0 || h <= 0) return spots;
            var live = pieces.FindAll(p => !p.dropped);
            var taken = new bool[w, h];
            var partition = new bool[w, h];
            var innerDoor = new bool[w, h];
            int fullW = w + 2, fullH = h + 2;
            var inWall = new bool[fullW, fullH];
            foreach (var p in live)
            {
                for (int x = p.minX; x < p.minX + p.w; x++)
                {
                    for (int z = p.minZ; z < p.minZ + p.h; z++)
                    {
                        if (p.inWall)
                        {
                            if (x >= 0 && z >= 0 && x < fullW && z < fullH) inWall[x, z] = true;
                            continue;
                        }
                        if (x < 0 || z < 0 || x >= w || z >= h) continue;
                        taken[x, z] = true;
                        if (p.isDoor) innerDoor[x, z] = true;
                        else if (p.isWall) partition[x, z] = true;
                    }
                }
                if (p.inWall) continue;
                foreach (var (sx, sz) in p.SpotCellsBothWays(w))
                    if (sx >= 0 && sz >= 0 && sx < w && sz < h) taken[sx, sz] = true;
            }
            // Only where someone can walk from the door: a lamp in a pocket behind the furniture could never be built.
            var reached = new bool[w, h];
            var blocked = new bool[w, h];
            foreach (var p in live)
            {
                if (p.inWall || p.passable) continue;
                for (int x = p.minX; x < p.minX + p.w; x++)
                    for (int z = p.minZ; z < p.minZ + p.h; z++)
                        if (x >= 0 && z >= 0 && x < w && z < h) blocked[x, z] = true;
            }
            if (doorX >= 0 && doorX < w && !blocked[doorX, 0])
            {
                var queue = new Queue<(int, int)>();
                reached[doorX, 0] = true;
                queue.Enqueue((doorX, 0));
                while (queue.Count > 0)
                {
                    var (x, z) = queue.Dequeue();
                    for (int f = 0; f < 4; f++)
                    {
                        TemplateLayout.Step(f, out int dx, out int dz);
                        int nx = x + dx, nz = z + dz;
                        if (nx < 0 || nz < 0 || nx >= w || nz >= h || reached[nx, nz] || blocked[nx, nz]) continue;
                        reached[nx, nz] = true;
                        queue.Enqueue((nx, nz));
                    }
                }
            }
            bool DoorAt(int x, int z) =>
                (z == -1 && x == doorX) || (x >= 0 && z >= 0 && x < w && z < h && innerDoor[x, z]) || InWallDoor(x, z);
            // A door built into the wall ring (a saved room's second door) is a piece in the wall too.
            bool InWallDoor(int x, int z)
            {
                foreach (var p in live)
                    if (p.inWall && p.isDoor && p.Contains(x + 1, z + 1)) return true;
                return false;
            }
            for (int x = 0; x < w; x++)
            {
                for (int z = 0; z < h; z++)
                {
                    if (taken[x, z] || !reached[x, z] || area == null || area[x, z] < 0) continue;
                    bool nearDoor = false;
                    for (int f = 0; f < 4; f++)
                    {
                        TemplateLayout.Step(f, out int dx, out int dz);
                        if (DoorAt(x + dx, z + dz)) nearDoor = true;
                    }
                    if (nearDoor) continue;
                    for (int f = 0; f < 4; f++)
                    {
                        TemplateLayout.Step(f, out int dx, out int dz);
                        int nx = x + dx, nz = z + dz;
                        bool wall;
                        if (nx < 0 || nz < 0 || nx >= w || nz >= h)
                        {
                            // The ring: full coordinates are interior + 1. Corners can't be reached from a floor cell.
                            int fx = nx + 1, fz = nz + 1;
                            bool corner = (fx == 0 || fx == fullW - 1) && (fz == 0 || fz == fullH - 1);
                            bool door = fz == 0 && fx == doorX + 1;
                            wall = !corner && !door && !inWall[fx, fz];
                        }
                        else wall = partition[nx, nz];
                        if (wall) spots.Add(new Spot { x = x, z = z, facing = f, area = area[x, z] });
                    }
                }
            }
            return spots;
        }

        private static int D2((int x, int z) a, (int x, int z) b) => (a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z);

        /// <summary>
        /// Picks lamp spots one at a time: each where it lights the most floor still dark (within <paramref name="reach"/>),
        /// on a tie the farthest from the lamps already up, then the most central, then the first. Stops when every cell
        /// is lit, at <paramref name="max"/> lamps, or when no spot is left. <paramref name="lit"/> are lights already in
        /// the room. Never two lamps on one cell. Returns indices into <paramref name="candidates"/>.
        /// </summary>
        public static List<int> Spread(IList<(int x, int z)> candidates, IList<(int x, int z)> cells, float reach, int max, IList<(int x, int z)> lit = null)
        {
            var chosen = new List<int>();
            if (candidates == null || cells == null || candidates.Count == 0 || cells.Count == 0 || max <= 0) return chosen;
            float r2 = reach * reach;
            var covered = new bool[cells.Count];
            int dark = cells.Count;
            var lights = new List<(int x, int z)>();
            var used = new HashSet<(int, int)>();
            void Light((int x, int z) at)
            {
                lights.Add(at);
                used.Add(at);
                for (int j = 0; j < cells.Count; j++)
                {
                    if (covered[j] || D2(at, cells[j]) > r2) continue;
                    covered[j] = true;
                    dark--;
                }
            }
            if (lit != null)
                foreach (var l in lit)
                    Light(l);
            while (chosen.Count < max && dark > 0)
            {
                int best = -1, bestGain = 0, bestApart = -1;
                long bestCentral = long.MaxValue;
                for (int i = 0; i < candidates.Count; i++)
                {
                    var c = candidates[i];
                    if (used.Contains(c)) continue;
                    int gain = 0;
                    long central = 0;
                    for (int j = 0; j < cells.Count; j++)
                    {
                        int d = D2(c, cells[j]);
                        central += d;
                        if (!covered[j] && d <= r2) gain++;
                    }
                    if (gain == 0) continue;
                    int apart = int.MaxValue;
                    foreach (var l in lights) apart = Math.Min(apart, D2(c, l));
                    bool better = gain > bestGain
                                  || (gain == bestGain && apart > bestApart)
                                  || (gain == bestGain && apart == bestApart && central < bestCentral);
                    if (!better) continue;
                    best = i;
                    bestGain = gain;
                    bestApart = apart;
                    bestCentral = central;
                }
                if (best < 0) break;
                chosen.Add(best);
                Light(candidates[best]);
            }
            return chosen;
        }

        /// <summary>
        /// Hangs wall lamps in a laid-out room: every area that has one of <paramref name="lightAt"/> (where the plan puts
        /// its lights) gets lamps on its walls, as many as its size needs to be lit well, up to <see cref="MaxWallLamps"/>.
        /// <paramref name="pieces"/> are the room's other pieces (not the lights the lamps replace).
        /// </summary>
        public static WallLampPlan HangLamps(int w, int h, int doorX, List<TemplateLayout.Piece> pieces, IEnumerable<(int x, int z)> lightAt, float reach)
        {
            var plan = new WallLampPlan { w = w, h = h, reach = reach };
            if (w <= 0 || h <= 0 || reach <= 0f) return plan;
            plan.areaOf = Areas(w, h, pieces, out int count);
            for (int a = 0; a < count; a++) plan.areas.Add(new List<(int x, int z)>());
            for (int x = 0; x < w; x++)
                for (int z = 0; z < h; z++)
                    if (plan.areaOf[x, z] >= 0) plan.areas[plan.areaOf[x, z]].Add((x, z));
            plan.candidates = WallSpots(w, h, doorX, pieces, plan.areaOf);
            var wanted = new HashSet<int>();
            foreach (var (x, z) in lightAt)
            {
                int a = plan.AreaAt(x, z);
                if (a >= 0) wanted.Add(a);
            }
            for (int a = 0; a < count; a++)
            {
                if (!wanted.Contains(a)) continue;
                var idx = new List<int>();
                var at = new List<(int x, int z)>();
                for (int i = 0; i < plan.candidates.Count; i++)
                {
                    if (plan.candidates[i].area != a) continue;
                    idx.Add(i);
                    at.Add((plan.candidates[i].x, plan.candidates[i].z));
                }
                foreach (int k in Spread(at, plan.areas[a], reach, MaxWallLamps(plan.areas[a].Count)))
                    plan.chosen.Add(idx[k]);
            }
            foreach (int i in plan.chosen) plan.lit.Add(plan.candidates[i].area);
            return plan;
        }

        /// <summary>
        /// Lights a laid-out room with wall lamps in place of its floor lights: the floor lights in every area that got
        /// lamps are left out (a light in an area with no free wall stays where it is). <paramref name="lights"/> are the
        /// room's lights that may move to the wall: the piece the layout placed (null if the colony can build none for the
        /// floor) and where the plan has it.
        /// </summary>
        public static WallLampPlan LightWithWallLamps(int w, int h, int doorX, List<TemplateLayout.Piece> pieces, List<(TemplateLayout.Piece piece, int x, int z)> lights, float reach)
        {
            var moving = new HashSet<TemplateLayout.Piece>();
            var at = new List<(int x, int z)>();
            foreach (var (piece, x, z) in lights)
            {
                if (piece != null && !piece.dropped)
                {
                    moving.Add(piece);
                    at.Add((piece.minX, piece.minZ));
                }
                else at.Add((x, z));
            }
            var others = pieces.FindAll(p => !p.dropped && !moving.Contains(p));
            var plan = HangLamps(w, h, doorX, others, at, reach);
            foreach (var p in moving)
            {
                if (!plan.lit.Contains(plan.AreaAt(p.minX, p.minZ))) continue;
                p.dropped = true;
                p.note = "hangs on the wall instead";
            }
            return plan;
        }

        /// <summary>A lamp that isn't needed any more (the others light its area) or that has nowhere left to hang.</summary>
        public const int NotNeeded = -1, Nowhere = -2;

        /// <summary>
        /// Moves the lamps whose spot turned out unusable on the map (the wall isn't there or won't take a lamp, something
        /// stands in the way) to the best usable spots left in the same area. Lamps whose spot works stay where they are.
        /// Returns the spot for each of <see cref="WallLampPlan.chosen"/>: its own, another, <see cref="NotNeeded"/> when
        /// the lamps left light the area, or <see cref="Nowhere"/> when part of it stays dark and no spot is left.
        /// </summary>
        public static List<int> Relocate(WallLampPlan plan, Func<int, bool> usable)
        {
            var result = new List<int>(plan.chosen);
            if (plan.chosen.Count == 0) return result;
            var ok = new Dictionary<int, bool>();
            bool Usable(int i)
            {
                if (!ok.TryGetValue(i, out bool u)) ok[i] = u = usable(i);
                return u;
            }
            foreach (int a in plan.lit)
            {
                var kept = new List<(int x, int z)>();
                var bad = new List<int>();
                for (int k = 0; k < plan.chosen.Count; k++)
                {
                    var s = plan.candidates[plan.chosen[k]];
                    if (s.area != a) continue;
                    if (Usable(plan.chosen[k])) kept.Add((s.x, s.z));
                    else bad.Add(k);
                }
                if (bad.Count == 0) continue;
                var idx = new List<int>();
                var at = new List<(int x, int z)>();
                for (int i = 0; i < plan.candidates.Count; i++)
                {
                    var s = plan.candidates[i];
                    if (s.area != a || !Usable(i) || kept.Contains((s.x, s.z))) continue;
                    idx.Add(i);
                    at.Add((s.x, s.z));
                }
                var cells = a < plan.areas.Count ? plan.areas[a] : new List<(int x, int z)>();
                var picks = Spread(at, cells, plan.reach, bad.Count, kept);
                // What the kept and moved lamps leave dark: a lamp that found no spot is only missed if there's some.
                float r2 = plan.reach * plan.reach;
                var lights = new List<(int x, int z)>(kept);
                foreach (int p in picks) lights.Add(at[p]);
                bool darkLeft = false;
                foreach (var c in cells)
                {
                    bool seen = false;
                    foreach (var l in lights)
                        if (D2(l, c) <= r2) seen = true;
                    if (!seen) darkLeft = true;
                }
                for (int b = 0; b < bad.Count; b++)
                    result[bad[b]] = b < picks.Count ? idx[picks[b]] : darkLeft ? Nowhere : NotNeeded;
            }
            return result;
        }
    }
}
