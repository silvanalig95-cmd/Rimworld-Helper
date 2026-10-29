using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Reserved slots on a shelf (Building/ShelfReserves.cs), as plain numbers: a shelf of <c>capacity</c> stacks keeps
    /// the slots reserved on it (each for a kind: an item, or any of a group like meals; a kind can have several) while
    /// the colony has some of that kind on the map. Each slot needs a stack of its own: the stacks are matched to the
    /// slots they can fill so as many are filled as can be (<see cref="Fill"/>). A stack of anything else takes a free
    /// slot only while a slot is left for every reserved one still empty; when one is empty and the shelf is too full
    /// for it, a stack filling no slot is moved out, of what the shelf holds most of, the smallest first. A kind can
    /// also be kept to its slots and no more (a cap), held to a number of items per stack, and kept only while its
    /// condition holds: while it doesn't, the kind isn't kept on the shelf at all (barred).
    /// </summary>
    public static class ShelfReserveMath
    {
        /// <summary>
        /// A stack on the shelf: what it is (its def), how many, which reserved slots it could fill, whether it is of a
        /// kind kept out for now (<see cref="barred"/>), and which capped kinds it counts against (<see cref="groups"/>).
        /// </summary>
        public struct Stack
        {
            public string key;
            public int count;
            public int[] covers;
            public bool barred;
            public int[] groups;
        }

        /// <summary>
        /// Which stack fills each slot (-1: empty), as many slots filled as can be, the bigger stacks taken first. A stack
        /// fills one slot at most.
        /// </summary>
        public static int[] Fill(int slots, IList<Stack> stacks)
        {
            var filledBy = new int[slots];
            for (int k = 0; k < slots; k++) filledBy[k] = -1;
            if (stacks == null || slots == 0) return filledBy;
            var order = new List<int>();
            for (int i = 0; i < stacks.Count; i++) order.Add(i);
            order.Sort((a, b) => stacks[a].count != stacks[b].count ? stacks[b].count.CompareTo(stacks[a].count) : a.CompareTo(b));
            bool Take(int s, bool[] tried)
            {
                var covers = stacks[s].covers;
                if (covers == null) return false;
                foreach (int k in covers)
                {
                    if (k < 0 || k >= slots || tried[k]) continue;
                    tried[k] = true;
                    if (filledBy[k] < 0 || Take(filledBy[k], tried))
                    {
                        filledBy[k] = s;
                        return true;
                    }
                }
                return false;
            }
            foreach (int s in order) Take(s, new bool[slots]);
            return filledBy;
        }

        /// <summary>The reserved slots no stack on the shelf fills, by their place in the list.</summary>
        public static List<int> Missing(int slots, IList<Stack> stacks)
        {
            var filledBy = Fill(slots, stacks);
            var missing = new List<int>();
            for (int k = 0; k < slots; k++)
                if (filledBy[k] < 0) missing.Add(k);
            return missing;
        }

        /// <summary>
        /// Whether a stack may take a free slot: always if it fills an empty reserved slot, otherwise only if a free slot
        /// is still left for each empty one the colony has some for (<paramref name="missingWaiting"/>).
        /// </summary>
        public static bool MayTakeSlot(int capacity, int stacks, bool fillsMissing, int missingWaiting)
        {
            int free = capacity - stacks;
            if (free <= 0) return false;
            if (fillsMissing) return true;
            return free - 1 >= missingWaiting;
        }

        /// <summary>
        /// The stacks to move out so each empty slot the colony has some for finds room: as many as are short, from the
        /// stacks filling no slot, of what the shelf holds most of, the smallest first.
        /// </summary>
        public static List<int> MoveOut(int capacity, IList<Stack> stacks, int slots, int missingWaiting) =>
            MoveOut(capacity, stacks, slots, missingWaiting, null);

        /// <summary>
        /// The same, first moving out every stack of a kind kept out for now, and the stacks of a capped kind past its
        /// cap (<paramref name="caps"/>, by group), of those filling no slot, the smallest first.
        /// </summary>
        public static List<int> MoveOut(int capacity, IList<Stack> stacks, int slots, int missingWaiting, IList<int> caps)
        {
            var result = new List<int>();
            if (stacks == null) return result;
            var used = new bool[stacks.Count];
            foreach (int s in Fill(slots, stacks))
                if (s >= 0) used[s] = true;
            var perKey = new Dictionary<string, int>();
            foreach (var s in stacks)
            {
                string key = s.key ?? "";
                perKey[key] = perKey.TryGetValue(key, out int n) ? n + 1 : 1;
            }
            void Out(int i)
            {
                used[i] = true;
                result.Add(i);
                perKey[stacks[i].key ?? ""]--;
            }
            int Pick(System.Func<int, bool> which)
            {
                int best = -1;
                for (int i = 0; i < stacks.Count; i++)
                {
                    if (used[i] || !which(i)) continue;
                    if (best < 0 || Better(stacks[i], stacks[best], perKey)) best = i;
                }
                return best;
            }
            // Kept out for now: every stack of it that fills no slot.
            for (int i = 0; i < stacks.Count; i++)
                if (!used[i] && stacks[i].barred) Out(i);
            // Past a cap: down to it, from the stacks filling no slot.
            if (caps != null)
                for (int g = 0; g < caps.Count; g++)
                {
                    if (caps[g] < 0) continue;
                    bool Member(int i) => stacks[i].groups != null && System.Array.IndexOf(stacks[i].groups, g) >= 0;
                    int count = 0;
                    for (int i = 0; i < stacks.Count; i++)
                        if (Member(i) && !result.Contains(i)) count++;
                    while (count > caps[g])
                    {
                        int pick = Pick(Member);
                        if (pick < 0) break;
                        Out(pick);
                        count--;
                    }
                }
            // Room for the slots waiting, counting what's already on its way out.
            int need = missingWaiting - (capacity - (stacks.Count - result.Count));
            while (need > 0)
            {
                int pick = Pick(_ => true);
                if (pick < 0) break;
                Out(pick);
                need--;
            }
            return result;
        }

        /// <summary>
        /// How many more of a kind held to <paramref name="limit"/> per stack (0: a full stack) a cell can take: the room
        /// left in its stack there (<paramref name="existing"/>, -1 for none), or a new stack's worth.
        /// </summary>
        public static int SpaceFor(int limit, int stackLimit, int existing)
        {
            int most = limit > 0 ? System.Math.Min(limit, stackLimit) : stackLimit;
            return existing < 0 ? most : System.Math.Max(0, most - existing);
        }

        /// <summary>
        /// "The best at hand" of a group: of its kinds the colony has (<paramref name="present"/>), those ranked highest
        /// (<paramref name="ranks"/>: a meal by how much it's liked, medicine by its potency). Empty when it has none.
        /// </summary>
        public static List<int> Best(IList<float> ranks, IList<bool> present)
        {
            var best = new List<int>();
            if (ranks == null || present == null) return best;
            float top = float.MinValue;
            for (int i = 0; i < ranks.Count && i < present.Count; i++)
            {
                if (!present[i]) continue;
                if (ranks[i] > top)
                {
                    top = ranks[i];
                    best.Clear();
                }
                if (ranks[i] >= top) best.Add(i);
            }
            return best;
        }

        private static bool Better(Stack a, Stack b, Dictionary<string, int> perKey)
        {
            int na = perKey[a.key ?? ""], nb = perKey[b.key ?? ""];
            if (na != nb) return na > nb;
            return a.count < b.count;
        }
    }

    /// <summary>
    /// Where a drink, a smoke or a snack is had (Building/ChewSpots.cs): not standing in a freezer, nor in a store room
    /// with nowhere to sit, when a seat or a spot outside it can be found.
    /// </summary>
    public static class ChewSpotMath
    {
        /// <summary>Below this a room is a freezer (°C).</summary>
        public const float FreezerBelow = 0f;

        /// <summary>A room to keep things in, not to stay in: stores kept below freezing, or stores and no seat at all.</summary>
        public static bool StoreRoom(bool outdoors, float temperature, int storageCells, int seats) =>
            !outdoors && storageCells > 0 && (temperature < FreezerBelow || seats == 0);
    }
}
