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
    /// condition holds: while it doesn't, the kind isn't kept on the shelf at all (barred). A stack that must go and that
    /// no other storage takes is set down beside the shelf while the shelf is short of room (<see cref="ShortOfRoom"/>);
    /// a colonist ordered to haul a reserved kind to a full shelf takes a stack off it first (<see cref="MayMakeRoom"/>,
    /// <see cref="RoomFrom"/>). Any of meals or of medicine is kept best first, in the order colonists pick their food
    /// (<see cref="RankedKinds"/>, <see cref="FoodRank"/>): the better stacks take the slots, a lesser kind waits while a
    /// better one would come, and a better kind that turns up takes a lesser stack's slot (<see cref="SwapDue"/>,
    /// <see cref="SwapOut"/>).
    /// </summary>
    public static class ShelfReserveMath
    {
        /// <summary>
        /// A stack on the shelf: what it is (its def), how many, which reserved slots it could fill, whether it is of a
        /// kind kept out for now (<see cref="barred"/>), which capped kinds it counts against (<see cref="groups"/>), and
        /// how well it ranks among the kinds of "any" of a group kept best first (<see cref="rank"/>; 0 for the rest).
        /// </summary>
        public struct Stack
        {
            public string key;
            public int count;
            public int[] covers;
            public bool barred;
            public int[] groups;
            public double rank;
        }

        /// <summary>
        /// Which stack fills each slot (-1: empty), as many slots filled as can be, the better ranked stacks taken first,
        /// then the bigger. A stack fills one slot at most.
        /// </summary>
        public static int[] Fill(int slots, IList<Stack> stacks)
        {
            var filledBy = new int[slots];
            for (int k = 0; k < slots; k++) filledBy[k] = -1;
            if (stacks == null || slots == 0) return filledBy;
            var order = new List<int>();
            for (int i = 0; i < stacks.Count; i++) order.Add(i);
            order.Sort((a, b) =>
                stacks[a].rank != stacks[b].rank ? stacks[b].rank.CompareTo(stacks[a].rank)
                : stacks[a].count != stacks[b].count ? stacks[b].count.CompareTo(stacks[a].count)
                : a.CompareTo(b));
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
        /// Whether room can be made on a shelf for a stack a colonist is ordered to haul there: the shelf is full, the
        /// stack would fill one of its empty reserved slots, the shelf's storage settings allow it, and the shelf is
        /// storage as good as where it lies or better (<paramref name="better"/>; a reserved kind comes from storage of
        /// the shelf's own priority too, never from higher).
        /// </summary>
        public static bool MayMakeRoom(int capacity, int held, bool fillsMissing, bool allowed, bool better) =>
            capacity > 0 && held >= capacity && fillsMissing && allowed && better;

        /// <summary>
        /// The stack to take off a full shelf so another can come in: of those the reserves mark to move out
        /// (<paramref name="marked"/>), the one of least worth; else of those of no reserved kind, the one of least worth.
        /// Never a stack of a reserved kind the reserves don't mark, so no reserved slot is emptied for another. Only those
        /// a colonist can take (<paramref name="movable"/>) count; -1 when none can go.
        /// </summary>
        public static int RoomFrom(IList<Stack> stacks, IList<bool> marked, IList<float> worth, IList<bool> movable)
        {
            if (stacks == null) return -1;
            float Worth(int i) => worth != null && i < worth.Count ? worth[i] : 0f;
            int Least(System.Func<int, bool> which)
            {
                int best = -1;
                for (int i = 0; i < stacks.Count; i++)
                {
                    if (movable != null && (i >= movable.Count || !movable[i]) || !which(i)) continue;
                    if (best < 0 || Worth(i) < Worth(best) || Worth(i) == Worth(best) && stacks[i].count < stacks[best].count) best = i;
                }
                return best;
            }
            int pick = Least(i => marked != null && i < marked.Count && marked[i]);
            if (pick >= 0) return pick;
            return Least(i => (stacks[i].covers == null || stacks[i].covers.Length == 0) && !stacks[i].barred);
        }

        /// <summary>
        /// How many stacks must still be set down off a full shelf for its waiting slots when nothing else will store them:
        /// the slots waiting that the usual hauling would fill (<paramref name="waiting"/>), less the free slots and the
        /// stacks already on their way out (<paramref name="leaving"/>).
        /// </summary>
        public static int ShortOfRoom(int capacity, int held, int waiting, int leaving) =>
            System.Math.Max(0, waiting - System.Math.Max(0, capacity - held) - System.Math.Max(0, leaving));

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
        /// (<paramref name="ranks"/>: a meal by how much colonists like it, <see cref="FoodRank"/>; medicine by its potency).
        /// Empty when it has none.
        /// </summary>
        public static List<int> Best(IList<double> ranks, IList<bool> present)
        {
            var best = new List<int>();
            if (ranks == null || present == null) return best;
            double top = double.MinValue;
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

        // ---------------- Best first ----------------

        /// <summary>
        /// The rank of a kind of "any" of a group kept best first that colonists don't eat as a meal (a drink, a dessert,
        /// food eaten only when starving): below every ranked kind, so it comes last.
        /// </summary>
        public const double Unranked = -1d;

        /// <summary>No kind would come: nothing waits for a better one.</summary>
        public const double NoneComing = double.NegativeInfinity;

        /// <summary>
        /// The kinds of a group that are ranked, for "the best at hand" and for filling "any" of it best first: every kind
        /// when all are medicine; when none is and at least half are kinds a colonist eats as a meal (<paramref name="eaten"/>:
        /// no drug, raw food or better), those, the rest (drinks, desserts, what is eaten only when starving) left out of
        /// the ranking; none for a group of medicine and other things, or one that is mostly not food (Alpha Crafts'
        /// artisan goods, with popcorn among the oils and cloth), whose "any" stays whichever comes first.
        /// </summary>
        public static List<int> RankedKinds(IList<bool> medicine, IList<bool> eaten)
        {
            var ranked = new List<int>();
            if (medicine == null || eaten == null) return ranked;
            int n = System.Math.Min(medicine.Count, eaten.Count), meals = 0;
            bool all = n > 0, any = false;
            for (int i = 0; i < n; i++)
            {
                if (medicine[i]) any = true;
                else all = false;
                if (!medicine[i] && eaten[i]) meals++;
            }
            if (!all && (any || meals * 2 < n)) return ranked;
            for (int i = 0; i < n; i++)
                if (all ? medicine[i] : eaten[i]) ranked.Add(i);
            return ranked;
        }

        /// <summary>"The best at hand" is offered for a group with two or more ranked kinds the shelf may hold.</summary>
        public static bool OffersBest(IList<bool> medicine, IList<bool> eaten) => RankedKinds(medicine, eaten).Count >= 2;

        /// <summary>
        /// How much colonists like a food, in the order they pick it (the game's FoodUtility.FoodOptimality, less the
        /// distance and what depends on the eater): a meal (<paramref name="meal"/>: awful or better) before raw food and
        /// terrible meals, which they look for only when urgently hungry; then the mood its taste gives, through the game's
        /// own curve (<see cref="TasteOptimality"/>), with the def's own offset for people (a simple meal's 16, a survival
        /// pack's -5); then its preferability; then its worth. So a lavish meal comes before a fine one, a fine before a
        /// simple meal, and a simple meal before a survival pack, though the pack is worth more.
        /// </summary>
        public static double FoodRank(bool meal, int preferability, float tasteMood, float offset, float worth)
        {
            double optimality = System.Math.Round((double)TasteOptimality(tasteMood) + offset);
            optimality = System.Math.Max(-9999d, System.Math.Min(9999d, optimality));
            double pref = System.Math.Max(0, System.Math.Min(99, preferability));
            return (meal ? 1e12 : 0d) + (optimality + 10000d) * 1e8 + pref * 1e4 + System.Math.Max(0d, System.Math.Min(9999.99d, worth));
        }

        private static readonly float[] MoodX = { -100f, -10f, -5f, -1f, 0f, 100f };
        private static readonly float[] MoodY = { -600f, -100f, -70f, -50f, 0f, 800f };

        /// <summary>What the mood of a food's taste adds to how much colonists want it (the game's FoodOptimalityEffectFromMoodCurve).</summary>
        public static float TasteOptimality(float mood)
        {
            if (mood <= MoodX[0]) return MoodY[0];
            for (int i = 1; i < MoodX.Length; i++)
                if (mood <= MoodX[i])
                    return MoodY[i - 1] + (MoodY[i] - MoodY[i - 1]) * (mood - MoodX[i - 1]) / (MoodX[i] - MoodX[i - 1]);
            return MoodY[MoodY.Length - 1];
        }

        /// <summary>
        /// An empty slot of "any" kept best first takes a kind ranked <paramref name="rank"/> only when it is as good as
        /// the best that would come (<paramref name="best"/>; anything when nothing would): a lesser kind never takes the
        /// slot a better one is on its way to, so nothing comes in that would at once be carried out again.
        /// </summary>
        public static bool TakesEmptySlot(double rank, double best) => rank >= best;

        /// <summary>
        /// For "any" of a group kept best first whose slots (<paramref name="slots"/>, by their place in the list) are all
        /// filled: the stack filling one of them that ranks lowest (the smaller first), when a better kind would come
        /// (<paramref name="best"/>: the best rank of the kinds that would come, strictly above it); else -1. Kinds ranked
        /// alike never swap.
        /// </summary>
        public static int SwapDue(IList<Stack> stacks, IList<int> filledBy, IList<int> slots, double best)
        {
            if (stacks == null || filledBy == null || slots == null || slots.Count == 0) return -1;
            int low = -1;
            foreach (int k in slots)
            {
                if (k < 0 || k >= filledBy.Count) return -1;
                int s = filledBy[k];
                if (s < 0 || s >= stacks.Count) return -1;
                if (low < 0 || Lower(stacks[s], stacks[low])) low = s;
            }
            return low >= 0 && best > stacks[low].rank ? low : -1;
        }

        /// <summary>
        /// The lesser stack carried back to the store so a better kind can take a slot (<see cref="SwapDue"/>) when the
        /// shelf has no free slot for it: of the stacks of the group filling no slot and ranked below the better kind, the
        /// lowest (the smaller first); else the slot's own stack (<paramref name="due"/>). Never one already on its way out
        /// (<paramref name="taken"/>); -1 when none can go.
        /// </summary>
        public static int SwapOut(IList<Stack> stacks, IList<int> filledBy, IList<int> slots, double best, int due, ICollection<int> taken)
        {
            if (stacks == null || slots == null) return -1;
            var filling = new HashSet<int>();
            if (filledBy != null)
                foreach (int s in filledBy)
                    if (s >= 0) filling.Add(s);
            int pick = -1;
            for (int i = 0; i < stacks.Count; i++)
            {
                if (filling.Contains(i) || taken != null && taken.Contains(i) || stacks[i].rank >= best || !CoversAny(stacks[i], slots)) continue;
                if (pick < 0 || Lower(stacks[i], stacks[pick])) pick = i;
            }
            if (pick >= 0) return pick;
            return due >= 0 && due < stacks.Count && (taken == null || !taken.Contains(due)) ? due : -1;
        }

        /// <summary>
        /// How long an empty slot of "any" kept best first waits for a better kind that would come before a lesser one may
        /// fill it after all (three hours), and how long that better kind is then left out of it (a day): a kind that
        /// can't be fetched (out of reach, say) never keeps the slot empty for good, nor swaps a stack back and forth.
        /// </summary>
        public const int GateTicks = 7500, RestTicks = 60000;

        /// <summary>Whether an empty slot waiting since <paramref name="since"/> (-1: not waiting) for a better kind that hasn't come gives up on it for now.</summary>
        public static bool GivesUp(int since, int now) => since >= 0 && now - since > GateTicks;

        private static bool Lower(Stack a, Stack b) => a.rank != b.rank ? a.rank < b.rank : a.count < b.count;

        private static bool CoversAny(Stack s, IList<int> slots)
        {
            if (s.covers == null) return false;
            foreach (int k in s.covers)
                if (slots.Contains(k)) return true;
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
