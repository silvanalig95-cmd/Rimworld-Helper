using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The base looked over (Building/BaseReview.cs) as plain rules, with no game types: which rooms belong near one
    /// another and which apart, and how far is too far; doors that separate nothing; stubs of wall left over from
    /// rebuilding; runs of conduit or pipe that lead nowhere; a freezer door that opens into the warm; long walks to
    /// meals. Rooms are named by role ("Kitchen", "DiningRoom", "Freezer", ...), cells by (x, z).
    /// </summary>
    public static class BaseReviewMath
    {
        // ---------------- Rooms near and apart ----------------

        /// <summary>Two roles that belong together, and how far apart (between the rooms' middles) is too far.</summary>
        public struct NearRule
        {
            public string a, b;
            public float far;
            /// <summary>The key of the finding, and how much it matters (0..1).</summary>
            public string key;
            public float weight;

            public NearRule(string a, string b, float far, string key, float weight)
            {
                this.a = a;
                this.b = b;
                this.far = far;
                this.key = key;
                this.weight = weight;
            }
        }

        /// <summary>Two roles that belong apart, and how close (between the rooms' middles) is too close.</summary>
        public struct ApartRule
        {
            public string a, b;
            public float close;
            public string key;
            public float weight;

            public ApartRule(string a, string b, float close, string key, float weight)
            {
                this.a = a;
                this.b = b;
                this.close = close;
                this.key = key;
                this.weight = weight;
            }
        }

        /// <summary>What goes with what: meals travel from the freezer through the kitchen to the table, materials from the store to the bench.</summary>
        public static readonly NearRule[] NearRules =
        {
            new NearRule("Kitchen", "Freezer", 20f, "kitchenfreezer", 0.4f),
            new NearRule("Kitchen", "DiningRoom", 22f, "kitchendining", 0.35f),
            new NearRule("DiningRoom", "RecRoom", 30f, "diningrec", 0.15f),
            new NearRule("Workshop", "StoreRoom", 24f, "workshopstore", 0.25f),
            new NearRule("Hospital", "Gate", 40f, "hospitalgate", 0.2f),
            new NearRule("Barn", "Fields", 30f, "barnfields", 0.12f)
        };

        /// <summary>What keeps apart: sleepers from noise, the sick from the kitchen, prisoners from the bedrooms.</summary>
        public static readonly ApartRule[] ApartRules =
        {
            new ApartRule("Bedroom", "Workshop", 9f, "bedroomworkshop", 0.25f),
            new ApartRule("Bedroom", "Barn", 9f, "bedroombarn", 0.2f),
            new ApartRule("Hospital", "Workshop", 9f, "hospitalworkshop", 0.15f),
            new ApartRule("PrisonCell", "Bedroom", 8f, "prisonbedroom", 0.15f)
        };

        /// <summary>The nearest of <paramref name="others"/> to <paramref name="from"/>: its index and distance (-1 and 0 for none).</summary>
        public static int Nearest((float x, float z) from, IList<(float x, float z)> others, out float distance)
        {
            distance = 0f;
            int best = -1;
            for (int i = 0; i < others.Count; i++)
            {
                float dx = others[i].x - from.x, dz = others[i].z - from.z;
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                if (best < 0 || d < distance)
                {
                    best = i;
                    distance = d;
                }
            }
            return best;
        }

        /// <summary>
        /// For a near rule: the rooms of role a whose nearest room of role b is too far, each with that distance. Nothing
        /// when either role is missing: a room the colony lacks is the advisor's business, not the review's.
        /// </summary>
        public static List<(int a, int b, float distance)> TooFar(NearRule rule, IList<(float x, float z)> roomsA, IList<(float x, float z)> roomsB)
        {
            var list = new List<(int, int, float)>();
            if (roomsA == null || roomsB == null || roomsA.Count == 0 || roomsB.Count == 0) return list;
            for (int i = 0; i < roomsA.Count; i++)
            {
                int j = Nearest(roomsA[i], roomsB, out float d);
                if (j >= 0 && d > rule.far) list.Add((i, j, d));
            }
            return list;
        }

        /// <summary>For an apart rule: the pairs of rooms (one of each role) too close to one another, with the distance.</summary>
        public static List<(int a, int b, float distance)> TooClose(ApartRule rule, IList<(float x, float z)> roomsA, IList<(float x, float z)> roomsB)
        {
            var list = new List<(int, int, float)>();
            if (roomsA == null || roomsB == null) return list;
            for (int i = 0; i < roomsA.Count; i++)
                for (int j = 0; j < roomsB.Count; j++)
                {
                    float dx = roomsA[i].x - roomsB[j].x, dz = roomsA[i].z - roomsB[j].z;
                    float d = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (d < rule.close) list.Add((i, j, d));
                }
            return list;
        }

        /// <summary>
        /// For a near rule by walking: the rooms of role a whose walk to the nearest room of role b (the walk from each, and
        /// which b it reached first) is too far. A room no walk reaches is left out: that is a door or a wall to look at, not
        /// a distance.
        /// </summary>
        public static List<(int a, int b, float distance)> TooFarWalking(NearRule rule, IList<float> walks, IList<int> nearest)
        {
            var list = new List<(int, int, float)>();
            if (walks == null || nearest == null) return list;
            for (int i = 0; i < walks.Count && i < nearest.Count; i++)
                if (walks[i] >= 0f && nearest[i] >= 0 && walks[i] > rule.far) list.Add((i, nearest[i], walks[i]));
            return list;
        }

        /// <summary>How much a too-far finding matters: the rule's weight, more the farther apart, up to twice.</summary>
        public static float FarScore(NearRule rule, float distance) => rule.weight * Math.Min(2f, Math.Max(1f, distance / rule.far));

        /// <summary>How much a too-close finding matters: the rule's weight, more for each pair, up to twice.</summary>
        public static float CloseScore(ApartRule rule, int pairs) => rule.weight * Math.Min(2f, 1f + (pairs - 1) * 0.3f);

        // ---------------- Walks ----------------

        /// <summary>Beyond this average walk (cells, as the crow flies) from the bedrooms to the nearest dining room, meals are a trek.</summary>
        public const float LongMealWalk = 35f;

        /// <summary>The average distance from each bedroom to its nearest dining room; -1 without both.</summary>
        public static float MealWalk(IList<(float x, float z)> bedrooms, IList<(float x, float z)> dining)
        {
            if (bedrooms == null || dining == null || bedrooms.Count == 0 || dining.Count == 0) return -1f;
            float sum = 0f;
            foreach (var b in bedrooms)
            {
                Nearest(b, dining, out float d);
                sum += d;
            }
            return sum / bedrooms.Count;
        }

        /// <summary>The average walk from each bedroom to its nearest dining room, of those a walk reaches; -1 when none is reached.</summary>
        public static float MealWalkOf(IList<float> walks)
        {
            if (walks == null) return -1f;
            float sum = 0f;
            int n = 0;
            foreach (var w in walks)
                if (w >= 0f)
                {
                    sum += w;
                    n++;
                }
            return n == 0 ? -1f : sum / n;
        }

        // ---------------- Walking ----------------

        /// <summary>
        /// Walks over the map's cells as people walk them: a step to a side costs one cell, a step on the slant the
        /// square root of two, never cutting a corner past a wall or a door (the game's own rule). Cells are numbered
        /// z x width + x, as the game numbers them. Searched from the sources outwards until every target is reached, so a
        /// compact base costs little; the arrays are kept between walks.
        /// </summary>
        public sealed class WalkGrid
        {
            private readonly int width, height;
            private readonly bool[] walkable, blocksDiagonal;
            private readonly int[] cost, from, stamp;
            private int generation;

            public const int Straight = 10, Slant = 14;

            public WalkGrid(int width, int height, bool[] walkable, bool[] blocksDiagonal)
            {
                this.width = width;
                this.height = height;
                this.walkable = walkable;
                this.blocksDiagonal = blocksDiagonal ?? walkable;
                int n = width * height;
                cost = new int[n];
                from = new int[n];
                stamp = new int[n];
            }

            private bool Blocks(int i) => blocksDiagonal == walkable ? !walkable[i] : blocksDiagonal[i];

            /// <summary>
            /// The walk in cells from the nearest source to each target (-1 where none reaches it), and which source's id
            /// that was. A source or target off the grid or in a wall is skipped.
            /// </summary>
            public float[] Walk(IList<int> sources, IList<int> ids, IList<int> targets, out int[] nearest)
            {
                generation++;
                int n = width * height;
                var heap = new List<(int c, int i)>();
                void Push(int c, int i)
                {
                    heap.Add((c, i));
                    int k = heap.Count - 1;
                    while (k > 0)
                    {
                        int p = (k - 1) / 2;
                        if (heap[p].c <= heap[k].c) break;
                        (heap[p], heap[k]) = (heap[k], heap[p]);
                        k = p;
                    }
                }
                (int c, int i) Pop()
                {
                    var top = heap[0];
                    int last = heap.Count - 1;
                    heap[0] = heap[last];
                    heap.RemoveAt(last);
                    int k = 0;
                    while (true)
                    {
                        int l = 2 * k + 1, r = l + 1, m = k;
                        if (l < heap.Count && heap[l].c < heap[m].c) m = l;
                        if (r < heap.Count && heap[r].c < heap[m].c) m = r;
                        if (m == k) break;
                        (heap[m], heap[k]) = (heap[k], heap[m]);
                        k = m;
                    }
                    return top;
                }
                for (int s = 0; s < sources.Count; s++)
                {
                    int i = sources[s];
                    if (i < 0 || i >= n || !walkable[i]) continue;
                    if (stamp[i] == generation && cost[i] <= 0) continue;
                    stamp[i] = generation;
                    cost[i] = 0;
                    from[i] = ids != null && s < ids.Count ? ids[s] : s;
                    Push(0, i);
                }
                var wanted = new HashSet<int>();
                foreach (var t in targets)
                    if (t >= 0 && t < n && walkable[t]) wanted.Add(t);
                var settled = new HashSet<int>();
                int left = wanted.Count;
                while (heap.Count > 0 && left > 0)
                {
                    var (c, i) = Pop();
                    if (c > cost[i] || !settled.Add(i)) continue;
                    if (wanted.Contains(i)) left--;
                    int x = i % width, z = i / width;
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            if (dx == 0 && dz == 0) continue;
                            int nx = x + dx, nz = z + dz;
                            if (nx < 0 || nz < 0 || nx >= width || nz >= height) continue;
                            int j = nz * width + nx;
                            if (!walkable[j]) continue;
                            bool slant = dx != 0 && dz != 0;
                            if (slant && (Blocks(z * width + nx) || Blocks(nz * width + x))) continue;
                            int nc = c + (slant ? Slant : Straight);
                            if (stamp[j] == generation && cost[j] <= nc) continue;
                            stamp[j] = generation;
                            cost[j] = nc;
                            from[j] = from[i];
                            Push(nc, j);
                        }
                }
                var result = new float[targets.Count];
                nearest = new int[targets.Count];
                for (int t = 0; t < targets.Count; t++)
                {
                    int i = targets[t];
                    bool reached = i >= 0 && i < n && stamp[i] == generation && settled.Contains(i);
                    result[t] = reached ? cost[i] / (float)Straight : -1f;
                    nearest[t] = reached ? from[i] : -1;
                }
                return result;
            }
        }

        // ---------------- Doors ----------------

        /// <summary>
        /// A door that separates nothing: the two rooms either side are one and the same indoors room (a door left in a
        /// wall that came down), or there is no wall on either side of it.
        /// </summary>
        public static bool DoorPointless(int roomA, int roomB, bool indoorsA, bool indoorsB, int openSides) =>
            openSides >= 3 || (roomA == roomB && roomA >= 0 && indoorsA && indoorsB);

        // ---------------- Walls ----------------

        /// <summary>A stub this long or shorter, hanging off a proper wall, is a leftover; longer ones may be a partition or a roof's support.</summary>
        public const int MaxStub = 3;

        /// <summary>
        /// Stubs of wall: runs of wall cells that separate nothing (the same room, or the open, on every side), at most
        /// <see cref="MaxStub"/> cells, that hang off a wall that does separate something. A free-standing pillar isn't
        /// one: it may hold the roof up.
        /// </summary>
        public static List<List<(int x, int z)>> Stubs(ICollection<(int x, int z)> walls, Func<(int x, int z), bool> separates)
        {
            var result = new List<List<(int x, int z)>>();
            var wallSet = new HashSet<(int x, int z)>(walls);
            var seen = new HashSet<(int x, int z)>();
            foreach (var start in walls)
            {
                if (seen.Contains(start) || separates(start)) continue;
                // The run of non-separating wall this cell belongs to.
                var run = new List<(int x, int z)>();
                var queue = new Queue<(int x, int z)>();
                queue.Enqueue(start);
                seen.Add(start);
                bool touchesProper = false;
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    run.Add(c);
                    foreach (var n in Beside(c))
                    {
                        if (!wallSet.Contains(n)) continue;
                        if (separates(n))
                        {
                            touchesProper = true;
                            continue;
                        }
                        if (seen.Add(n)) queue.Enqueue(n);
                    }
                }
                if (touchesProper && run.Count <= MaxStub) result.Add(run);
            }
            return result;
        }

        // ---------------- Conduits and pipes ----------------

        /// <summary>
        /// Runs that lead nowhere: from each end of the network (a cell with one neighbour) back along the line to the
        /// first fork or the first cell that is used (feeds something, or stands under it), the cells in between when
        /// none of them is used. A lone cell counts too. Once those are gone a fork may be an end itself, so it goes
        /// round again until nothing more falls away. A ring with nothing on it is caught by the caller as a whole
        /// network with no users.
        /// </summary>
        public static List<List<(int x, int z)>> DeadEnds(ICollection<(int x, int z)> cells, Func<(int x, int z), bool> used)
        {
            var result = new List<List<(int x, int z)>>();
            var left = new HashSet<(int x, int z)>(cells);
            for (int guard = 0; guard < 64; guard++)
            {
                var found = DeadEndsOnce(left, used);
                if (found.Count == 0) break;
                foreach (var run in found)
                {
                    result.Add(run);
                    foreach (var c in run) left.Remove(c);
                }
            }
            return result;
        }

        private static List<List<(int x, int z)>> DeadEndsOnce(HashSet<(int x, int z)> set, Func<(int x, int z), bool> used)
        {
            var result = new List<List<(int x, int z)>>();
            var cells = new List<(int x, int z)>(set);
            var taken = new HashSet<(int x, int z)>();
            foreach (var start in cells)
            {
                if (taken.Contains(start) || Degree(start, set) > 1) continue;
                var run = new List<(int x, int z)>();
                var c = start;
                var prev = (int.MinValue, int.MinValue);
                bool dead = true;
                while (true)
                {
                    if (used(c))
                    {
                        dead = run.Count > 0;
                        break;
                    }
                    run.Add(c);
                    int degree = Degree(c, set);
                    if (degree >= 3) { run.RemoveAt(run.Count - 1); break; }
                    (int x, int z) next = (int.MinValue, int.MinValue);
                    foreach (var n in Beside(c))
                        if (set.Contains(n) && n != prev) next = n;
                    if (next.x == int.MinValue) break;
                    // Into a fork: the fork itself stays, the branch up to it goes.
                    if (Degree(next, set) >= 3) break;
                    prev = c;
                    c = next;
                }
                if (dead && run.Count > 0)
                {
                    foreach (var r in run) taken.Add(r);
                    result.Add(run);
                }
            }
            return result;
        }

        private static int Degree((int x, int z) c, HashSet<(int x, int z)> set)
        {
            int n = 0;
            foreach (var b in Beside(c)) if (set.Contains(b)) n++;
            return n;
        }

        private static IEnumerable<(int x, int z)> Beside((int x, int z) c)
        {
            yield return (c.x + 1, c.z);
            yield return (c.x - 1, c.z);
            yield return (c.x, c.z + 1);
            yield return (c.x, c.z - 1);
        }

        // ---------------- Freezers ----------------

        /// <summary>A freezer: this cold or colder, with storage in it.</summary>
        public const float FreezerTemp = 1f;
        /// <summary>A room this warm beyond the freezer's door lets heat in with every opening.</summary>
        public const float WarmBeyond = 10f;
        /// <summary>An airlock: a little room with two doors between the freezer and the warm.</summary>
        public const int LobbyCells = 6;

        public static bool AirlockWanted(float freezerTemp, float beyondTemp, int beyondCells, int beyondDoors, bool beyondOutdoors) =>
            freezerTemp <= FreezerTemp && (beyondOutdoors || beyondTemp >= WarmBeyond) && !(beyondCells <= LobbyCells && beyondDoors >= 2);

        /// <summary>
        /// An airlock outside a freezer door that opens towards (dx, dz): the cell beyond the door kept as a lobby, a
        /// second door one cell further, a wall either side of the lobby, the cells either side of the second door (left
        /// open, so people walk round it), and the cell beyond the second door (open, so the door leads somewhere).
        /// </summary>
        public struct Airlock
        {
            public (int x, int z) lobby, door, wallA, wallB, roundA, roundB, beyond;
        }

        public static Airlock AirlockAt((int x, int z) freezerDoor, (int dx, int dz) dir)
        {
            var (x, z) = freezerDoor;
            var (dx, dz) = dir;
            // At right angles to the way out.
            int px = -dz, pz = dx;
            var a = new Airlock
            {
                lobby = (x + dx, z + dz),
                door = (x + 2 * dx, z + 2 * dz),
                beyond = (x + 3 * dx, z + 3 * dz)
            };
            a.wallA = (a.lobby.x + px, a.lobby.z + pz);
            a.wallB = (a.lobby.x - px, a.lobby.z - pz);
            a.roundA = (a.door.x + px, a.door.z + pz);
            a.roundB = (a.door.x - px, a.door.z - pz);
            return a;
        }

        // ---------------- Scores ----------------

        /// <summary>How much a tidy-up finding matters by how much there is of it: a little for one, more for many, never pressing.</summary>
        public static float TidyScore(int count, float each, float cap = 0.3f) => Math.Min(cap, count * each);
    }
}
