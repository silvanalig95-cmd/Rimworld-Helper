using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Reserved slots on a shelf (Building/ShelfReserves.cs), as plain numbers: a shelf of <c>capacity</c> stacks keeps
    /// one for each reserved kind (an item, or any of a group like meals) that the colony has on the map. A stack of
    /// anything else takes a free slot only while a slot is left for every reserved kind still missing; when one is
    /// missing and the shelf is too full for it, the stack of what the shelf holds most of (the smallest of them) is
    /// moved out, never the last of a reserved kind.
    /// </summary>
    public static class ShelfReserveMath
    {
        /// <summary>A stack on the shelf: what it is (its def), how many, and which reserved kinds it counts for.</summary>
        public struct Stack
        {
            public string key;
            public int count;
            public int[] covers;
        }

        /// <summary>The reserved kinds no stack on the shelf counts for, by their place in the list.</summary>
        public static List<int> Missing(int kinds, IList<Stack> stacks)
        {
            var have = new bool[kinds];
            if (stacks != null)
                foreach (var s in stacks)
                    if (s.covers != null)
                        foreach (int k in s.covers)
                            if (k >= 0 && k < kinds) have[k] = true;
            var missing = new List<int>();
            for (int k = 0; k < kinds; k++)
                if (!have[k]) missing.Add(k);
            return missing;
        }

        /// <summary>
        /// Whether a stack may take a free slot: always if it is a missing reserved kind, otherwise only if a free slot
        /// is still left for each missing kind the colony has (<paramref name="missingWaiting"/>).
        /// </summary>
        public static bool MayTakeSlot(int capacity, int stacks, bool fillsMissing, int missingWaiting)
        {
            int free = capacity - stacks;
            if (free <= 0) return false;
            if (fillsMissing) return true;
            return free - 1 >= missingWaiting;
        }

        /// <summary>
        /// The stacks to move out so each missing kind the colony has finds a free slot: as many as are short, from
        /// what the shelf holds most of, the smallest stack first; a stack that is the last of a reserved kind stays.
        /// </summary>
        public static List<int> MoveOut(int capacity, IList<Stack> stacks, int kinds, int missingWaiting)
        {
            var result = new List<int>();
            if (stacks == null) return result;
            int need = missingWaiting - (capacity - stacks.Count);
            if (need <= 0) return result;
            var coveredBy = new int[kinds];
            foreach (var s in stacks)
                if (s.covers != null)
                    foreach (int k in s.covers)
                        if (k >= 0 && k < kinds) coveredBy[k]++;
            var perKey = new Dictionary<string, int>();
            foreach (var s in stacks)
            {
                string key = s.key ?? "";
                perKey[key] = perKey.TryGetValue(key, out int n) ? n + 1 : 1;
            }
            var taken = new bool[stacks.Count];
            while (need > 0)
            {
                int best = -1;
                for (int i = 0; i < stacks.Count; i++)
                {
                    if (taken[i] || IsLast(stacks[i], coveredBy, kinds)) continue;
                    if (best < 0 || Better(stacks[i], stacks[best], perKey)) best = i;
                }
                if (best < 0) break;
                taken[best] = true;
                result.Add(best);
                need--;
                var s = stacks[best];
                string key = s.key ?? "";
                perKey[key]--;
                if (s.covers != null)
                    foreach (int k in s.covers)
                        if (k >= 0 && k < kinds) coveredBy[k]--;
            }
            return result;
        }

        private static bool IsLast(Stack s, int[] coveredBy, int kinds)
        {
            if (s.covers == null) return false;
            foreach (int k in s.covers)
                if (k >= 0 && k < kinds && coveredBy[k] <= 1) return true;
            return false;
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
