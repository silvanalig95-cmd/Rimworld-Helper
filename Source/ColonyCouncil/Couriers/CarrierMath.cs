using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>Where a carrier does their business on the map: the colony's mailbag, else its gate, else where traders leave goods.</summary>
    public enum CarrierSpot
    {
        None,
        Mailbag,
        Gate,
        TradeSpot
    }

    /// <summary>What a rider does about their mount as they leave (<see cref="CarrierMath.Remount"/>).</summary>
    public enum RemountStep
    {
        /// <summary>Nothing to do: no Giddy-Up, no mount, or already in the saddle.</summary>
        None,
        /// <summary>The mount is beside them: they get on now.</summary>
        MountNow,
        /// <summary>The mount is a little way off (parked, or waiting by the bag): they walk over to it first.</summary>
        WalkToMount,
        /// <summary>The mount is gone (dead, downed, fled, out of reach) or it has taken too long: they leave on foot.</summary>
        OnFoot
    }

    /// <summary>One kind of person a people might send to carry, as the kind pool sees it (<see cref="CarrierMath.KindPool"/>).</summary>
    public struct CarrierKindOption
    {
        public string name;
        /// <summary>0: a caravan's guards (the game's Trader groups); 1: their visitors (Peaceful groups); 2: their ordinary member, used only when the others give nobody.</summary>
        public int source;
        /// <summary>Which group maker it comes from (options of one group share the group's chance).</summary>
        public int group;
        /// <summary>The group maker's commonality, as the game weighs one group against another.</summary>
        public float groupCommonality;
        /// <summary>Its selection weight within the group, as the game picks it.</summary>
        public float selectionWeight;
        /// <summary>The kind's combat power (below 0: unknown, counted light).</summary>
        public float power;
        public bool person;
        public bool trader;
        /// <summary>Grown: not a kind made only as children (the game's pawnGroupDevelopmentStage).</summary>
        public bool adult;
        /// <summary>Made with a royal title (the Empire's nobles keep their own errands).</summary>
        public bool titled;
        /// <summary>A people's leader kind.</summary>
        public bool leader;
    }

    /// <summary>One beast a people might ride in on, as the mount pool sees it (<see cref="CarrierMath.MountPool"/>).</summary>
    public struct CarrierMountOption
    {
        public string name;
        /// <summary>Its selection weight among their caravans' beasts.</summary>
        public float weight;
        public float bodySize;
        public bool animal;
        public bool predator;
        /// <summary>Giddy-Up lets it be ridden (its mount list where it can be read; else true).</summary>
        public bool ridable;
        /// <summary>The land allows it: a pack animal of this land, a beast that lives wild here, or one that is wild nowhere (a farm beast).</summary>
        public bool landOk;
        /// <summary>The season and the weather outside suit it.</summary>
        public bool seasonOk;
    }

    /// <summary>
    /// The pure rules of carriers on the map (the player, 10 November 2026: "the couriers that bring letters and material
    /// take an awkward routine around my base before delivering the message a bit away from the base. if anything they
    /// should always walk to the mailbag directly, drop/pick up the message or items. for the message they unmount and
    /// then they walk off map without remounting ... the courier and mount looks always exactly the same"): where they do
    /// their business, where each of them stands and where goods are set down, which way in is the most direct, who a
    /// people sends and on what beast, and when a rider gets back in the saddle. No game types, so Tools/Tests can check it.
    /// </summary>
    public static class CarrierMath
    {
        /// <summary>A kind this heavy or lighter is someone sent to carry (a caravan hand, a villager, a light guard), not a trooper.</summary>
        public const float LightPower = 75f;

        /// <summary>A people with nobody that light sends its lightest, and those within this much of them.</summary>
        public const float LightSlack = 30f;

        /// <summary>A beast this big or bigger can be ridden (Giddy-Up's own default).</summary>
        public const float MinMountSize = 1.2f;

        /// <summary>The kind or beast a people sent last time counts this much less the next time, where they have another.</summary>
        public const float RepeatFactor = 0.5f;

        /// <summary>A way in along a road counts as if it were this many times nearer than a way across country.</summary>
        public const float RoadFactor = 1.2f;

        /// <summary>A rider spends at most this long (an hour) getting back to their mount before they set off on foot.</summary>
        public const int RemountTicks = 2500;

        /// <summary>A mount farther off than this (cells) is left to make its own way: the rider goes on foot.</summary>
        public const float RemountReach = 60f;

        /// <summary>Close enough to get straight on (cells: beside them, or under them).</summary>
        public const float MountNowDistance = 1.5f;

        // ---------------- Where ----------------

        /// <summary>Where they do their business: the colony's mailbag when there is one, else its gate, else where traders leave goods.</summary>
        public static CarrierSpot SpotOf(bool mailbag, bool gate, bool tradeSpot) =>
            mailbag ? CarrierSpot.Mailbag : gate ? CarrierSpot.Gate : tradeSpot ? CarrierSpot.TradeSpot : CarrierSpot.None;

        private static readonly int[] ringX, ringZ;

        static CarrierMath()
        {
            // The cells round the spot, the nearest first (beside it, then the corners, then the next ring), never the spot itself.
            var cells = new List<(int x, int z)>();
            for (int r = 1; r <= 3; r++)
                for (int x = -r; x <= r; x++)
                    for (int z = -r; z <= r; z++)
                        if (Math.Max(Math.Abs(x), Math.Abs(z)) == r) cells.Add((x, z));
            cells.Sort((a, b) =>
            {
                int ra = Math.Max(Math.Abs(a.x), Math.Abs(a.z)), rb = Math.Max(Math.Abs(b.x), Math.Abs(b.z));
                if (ra != rb) return ra.CompareTo(rb);
                int da = a.x * a.x + a.z * a.z, db = b.x * b.x + b.z * b.z;
                if (da != db) return da.CompareTo(db);
                // The side toward the map's south (the front, as the game draws it) first, then round.
                if (a.z != b.z) return a.z.CompareTo(b.z);
                return b.x.CompareTo(a.x);
            });
            ringX = new int[cells.Count];
            ringZ = new int[cells.Count];
            for (int i = 0; i < cells.Count; i++)
            {
                ringX[i] = cells[i].x;
                ringZ[i] = cells[i].z;
            }
        }

        /// <summary>How many places round a spot <see cref="RingOffset"/> knows (three rings).</summary>
        public static int RingCount => ringX.Length;

        /// <summary>
        /// The <paramref name="i"/>th place round the spot, the nearest first: where the <paramref name="i"/>th of a group
        /// stands while their business is done, and the order goods are set down in beside a mailbag. Never the spot itself;
        /// past the third ring it goes round again.
        /// </summary>
        public static void RingOffset(int i, out int dx, out int dz)
        {
            int k = ((i % ringX.Length) + ringX.Length) % ringX.Length;
            dx = ringX[k];
            dz = ringZ[k];
        }

        /// <summary>How near a way in counts: its distance to the spot, a road's a little nearer.</summary>
        public static float EntryKey(float distance, bool road) => Math.Max(0f, distance) / (road ? RoadFactor : 1f);

        /// <summary>
        /// The most direct way in to the spot (an index into <paramref name="ways"/>, -1 for none): of those they can come in
        /// by, the one nearest to it, a road a little preferred; ties the first. No more coming in at a random edge and
        /// walking round the colony to the gate.
        /// </summary>
        public static int BestEntry(IList<(float distance, bool road, bool ok)> ways)
        {
            if (ways == null) return -1;
            int best = -1;
            float key = float.MaxValue;
            for (int i = 0; i < ways.Count; i++)
            {
                if (!ways[i].ok) continue;
                float k = EntryKey(ways[i].distance, ways[i].road);
                if (k < key)
                {
                    key = k;
                    best = i;
                }
            }
            return best;
        }

        // ---------------- Who comes ----------------

        /// <summary>
        /// The people a people sends to carry, with how likely each is: their caravan hands and traders' guards (Trader
        /// groups) and their visitors, villagers and settlers (Peaceful groups), half and half where they have both; within
        /// each, group against group by the groups' commonality and kind against kind by its selection weight, as the game
        /// weighs them. Only grown people who are no trader, no leader, no noble and light (<see cref="LightPower"/>; a
        /// people with nobody that light sends its lightest). Their ordinary member only when those give nobody. A kind in
        /// more than one group counts once, its chances added. Empty: nobody to send.
        /// </summary>
        public static List<KeyValuePair<string, float>> KindPool(IList<CarrierKindOption> options)
        {
            var pool = new List<KeyValuePair<string, float>>();
            if (options == null || options.Count == 0) return pool;
            bool Eligible(CarrierKindOption o) =>
                !string.IsNullOrEmpty(o.name) && o.person && !o.trader && o.adult && !o.titled && !o.leader && o.selectionWeight > 0f;
            foreach (int pass in new[] { 0, 1 })
            {
                var cands = new List<CarrierKindOption>();
                foreach (var o in options)
                    if ((pass == 0 ? o.source != 2 : o.source == 2) && Eligible(o)) cands.Add(o);
                if (cands.Count == 0) continue;
                // Light ones; else the lightest and those near them.
                float min = float.MaxValue;
                bool anyLight = false;
                foreach (var o in cands)
                {
                    if (o.power <= LightPower) anyLight = true;
                    min = Math.Min(min, Math.Max(0f, o.power));
                }
                var keep = cands.FindAll(o => anyLight ? o.power <= LightPower : o.power <= min + LightSlack);
                // Each source's share, each group's within it, each kind's within its group.
                var sources = new List<int>();
                foreach (var o in keep)
                    if (!sources.Contains(o.source)) sources.Add(o.source);
                foreach (int s in sources)
                {
                    var groups = new Dictionary<int, float>();
                    var groupSums = new Dictionary<int, float>();
                    foreach (var o in keep)
                    {
                        if (o.source != s) continue;
                        groups[o.group] = Math.Max(0.01f, o.groupCommonality);
                        groupSums[o.group] = (groupSums.TryGetValue(o.group, out var g) ? g : 0f) + o.selectionWeight;
                    }
                    float allGroups = 0f;
                    foreach (var g in groups.Values) allGroups += g;
                    foreach (var o in keep)
                    {
                        if (o.source != s) continue;
                        float w = 1f / sources.Count * (groups[o.group] / allGroups) * (o.selectionWeight / groupSums[o.group]);
                        Add(pool, o.name, w);
                    }
                }
                if (pool.Count > 0) return pool;
            }
            return pool;
        }

        /// <summary>
        /// The beasts a people's carriers ride in on, with how likely each is: those of their caravans big enough to ride
        /// (<see cref="MinMountSize"/>) that Giddy-Up lets be ridden, never a predator, that the land and the season allow,
        /// by their caravans' own weights; a beast named twice counts once. Empty: the horse, if the season allows
        /// (<see cref="Fallback"/>), else on foot.
        /// </summary>
        public static List<KeyValuePair<string, float>> MountPool(IList<CarrierMountOption> options)
        {
            var pool = new List<KeyValuePair<string, float>>();
            if (options == null) return pool;
            foreach (var o in options)
                if (!string.IsNullOrEmpty(o.name) && o.animal && !o.predator && o.ridable && o.bodySize >= MinMountSize && o.landOk && o.seasonOk && o.weight > 0f)
                    Add(pool, o.name, o.weight);
            return pool;
        }

        /// <summary>No beast of theirs fits: the horse, where the season suits it; else they come on foot (null).</summary>
        public static string Fallback(bool horseSeasonOk, bool horseKnown) => horseKnown && horseSeasonOk ? "Horse" : null;

        private static void Add(List<KeyValuePair<string, float>> pool, string name, float w)
        {
            for (int i = 0; i < pool.Count; i++)
                if (pool[i].Key == name)
                {
                    pool[i] = new KeyValuePair<string, float>(name, pool[i].Value + w);
                    return;
                }
            pool.Add(new KeyValuePair<string, float>(name, w));
        }

        /// <summary>
        /// One of the pool, by <paramref name="r"/> (0 to 1, the game's random value): by their chances, the one sent last
        /// time (<paramref name="last"/>) counting <see cref="RepeatFactor"/> as much where there is another, so the same
        /// face and beast don't come every time. Null for an empty pool.
        /// </summary>
        public static string Pick(IList<KeyValuePair<string, float>> pool, double r, string last = null)
        {
            if (pool == null || pool.Count == 0) return null;
            if (pool.Count == 1) return pool[0].Key;
            double total = 0;
            foreach (var kv in pool) total += Weight(kv, last);
            if (total <= 0) return pool[0].Key;
            double at = Math.Max(0, Math.Min(0.999999, r)) * total;
            foreach (var kv in pool)
            {
                at -= Weight(kv, last);
                if (at < 0) return kv.Key;
            }
            return pool[pool.Count - 1].Key;
        }

        private static double Weight(KeyValuePair<string, float> kv, string last) => Math.Max(0f, kv.Value) * (kv.Key == last ? RepeatFactor : 1f);

        // ---------------- On the way out ----------------

        /// <summary>
        /// Business done on foot: going into the colony's stores, or to fetch someone handed over, a rider gets off at the
        /// mailbag first and their mount waits there. Setting goods or mail down, taking letters and waiting are done from
        /// the saddle where Giddy-Up lets them.
        /// </summary>
        public static bool OnFoot(bool intoStores, bool fetching) => intoStores || fetching;

        /// <summary>
        /// What a rider does about their mount as they leave: nothing without Giddy-Up, a mount, or when already in the saddle;
        /// on foot when the mount is gone (dead, downed, fled), out of reach, too far, or it has taken too long; on at once
        /// when it is beside them; else they walk over to it (it waits for them).
        /// </summary>
        public static RemountStep Remount(bool giddyUp, bool hasMount, bool riding, bool mountOk, bool reachable, float distance, int ticksLeaving)
        {
            if (!giddyUp || !hasMount || riding) return RemountStep.None;
            if (!mountOk || !reachable || distance > RemountReach || ticksLeaving > RemountTicks) return RemountStep.OnFoot;
            return distance <= MountNowDistance ? RemountStep.MountNow : RemountStep.WalkToMount;
        }

        /// <summary>The mount waits for its rider (rather than set off on its own) while the rider is on their way to it.</summary>
        public static bool MountWaits(RemountStep riderStep) => riderStep == RemountStep.MountNow || riderStep == RemountStep.WalkToMount;
    }
}
