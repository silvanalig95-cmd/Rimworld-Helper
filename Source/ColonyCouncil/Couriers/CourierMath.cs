using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What a courier trip does when it gets to the colony.</summary>
    public enum CourierKind
    {
        /// <summary>Brings things (silver, goods, a gift, a payment owed) and sets them down by the gate.</summary>
        Deliver,
        /// <summary>Comes for goods the colony gave, sold or paid, and takes them from the stores.</summary>
        Collect,
        /// <summary>Both in one visit: sets down the payment, then takes the goods.</summary>
        Exchange,
        /// <summary>Waits by the gate for what a colonist carries out to them (an errand, PlayOut), and takes it away.</summary>
        Await
    }

    /// <summary>How a trip came to an end.</summary>
    public enum CourierEnd
    {
        /// <summary>Its business was done.</summary>
        Done,
        /// <summary>It ran out of time: the stores out of reach, the gate shut, nobody came out.</summary>
        TimedOut,
        /// <summary>No way to send anyone: no people at peace with the colony, no way in from the edge, never safe to come.</summary>
        NoWay,
        /// <summary>The colony hurt, downed, arrested or killed one of them.</summary>
        Harmed,
        /// <summary>Their people turned hostile to the colony meanwhile (without the colony hurting them).</summary>
        Hostile,
        /// <summary>Danger came while they were here (raiders, a manhunter pack), and they left.</summary>
        Danger,
        /// <summary>The map is gone (abandoned, a caravan's map closed).</summary>
        MapGone,
        /// <summary>The setting was turned off.</summary>
        SettingOff,
        /// <summary>They are gone from the map without a word (a mod, an old save): what's left is settled.</summary>
        Lost
    }

    /// <summary>What a pending trip does on its turn.</summary>
    public enum CourierWait
    {
        /// <summary>Not yet: too early, after dark, or not safe.</summary>
        Wait,
        /// <summary>They come now.</summary>
        Go,
        /// <summary>It's been too long, or the setting is off: it is settled the old way, at once.</summary>
        Settle,
        /// <summary>The map is gone: nothing to settle.</summary>
        Drop
    }

    /// <summary>One people that might send a courier, as the faction choice sees it.</summary>
    public struct CourierCandidate
    {
        public int id;
        /// <summary>At peace with the colony, humanlike, still standing, not the colony's own, with people to send.</summary>
        public bool usable;
        /// <summary>Their tech level (the game's TechLevel as a number: 2 neolithic, 3 medieval, 4 industrial, 5 spacer).</summary>
        public int tech;
        /// <summary>World tiles to their nearest town (a large number when they have none).</summary>
        public float distance;
    }

    /// <summary>
    /// The pure rules of couriers (Couriers/): which people sends one, how many come and whether a pack animal comes
    /// with them, how a load is shared out, when a waiting trip comes, and what happens to the rest of a deal when a
    /// trip ends early. No game types, so Tools/Tests can check it.
    /// </summary>
    public static class CourierMath
    {
        public const int TicksPerHour = 2500;
        public const int TicksPerDay = 60000;

        /// <summary>The game's TechLevel.Medieval as a number.</summary>
        public const int Medieval = 3;

        /// <summary>At most this many people come on one trip.</summary>
        public const int MaxPeople = 3;

        /// <summary>What one porter carries comfortably, in "bulk" (kilograms, plus room for each stack).</summary>
        public const float PersonLoad = 35f;

        /// <summary>How much room a full stack takes besides its weight (a sack of corn is light but big).</summary>
        public const float StackBulk = 10f;

        /// <summary>They arrive between these hours (by day), and only start work at the stores before dusk.</summary>
        public const int DayFrom = 7;
        public const int DayTo = 18;

        /// <summary>More than this many trips at once and the oldest waiting one is settled the old way.</summary>
        public const int MaxTrips = 12;

        /// <summary>A trip that can't come within this long (never safe, no way in) is settled the old way.</summary>
        public const int GiveUpTicks = TicksPerDay * 3;

        /// <summary>How long they may take over their business once at the gate.</summary>
        public const int WorkTicks = TicksPerHour * 6;

        /// <summary>How long one of them waits at the gate for an errand.</summary>
        public const int AwaitTicks = TicksPerHour * 10;

        /// <summary>How long they stay on after their business, before they walk off.</summary>
        public const int LingerTicks = TicksPerHour / 2;

        /// <summary>Records of trips that are over, kept a while for watching the couriers walk off.</summary>
        public const int MaxKeepTicks = TicksPerDay * 2;

        // ---------------- Who sends them ----------------

        /// <summary>
        /// A people's tech fits the colony's: a pre-industrial colony deals with tribes and medieval folk, anyone else
        /// with peoples a step either side of its own.
        /// </summary>
        public static bool TechFits(int factionTech, int colonyTech, bool preIndustrial) =>
            preIndustrial ? factionTech <= Medieval : Math.Abs(factionTech - colonyTech) <= 1;

        /// <summary>
        /// The people that sends the courier (an index into <paramref name="c"/>, -1 for none): the event's own
        /// (<paramref name="eventId"/>) when it can; else one of a fitting tech level, the nearest first; else, the
        /// nearest of the closest tech. Never one that can't (hostile, defeated, the colony's own).
        /// </summary>
        public static int ChooseFaction(IList<CourierCandidate> c, int eventId, int colonyTech, bool preIndustrial)
        {
            if (c == null || c.Count == 0) return -1;
            if (eventId >= 0)
                for (int i = 0; i < c.Count; i++)
                    if (c[i].id == eventId && c[i].usable)
                        return i;
            int best = -1;
            for (int i = 0; i < c.Count; i++)
            {
                if (!c[i].usable) continue;
                if (best < 0 || Better(c[i], c[best], colonyTech, preIndustrial)) best = i;
            }
            return best;
        }

        private static bool Better(CourierCandidate a, CourierCandidate b, int colonyTech, bool preIndustrial)
        {
            bool fa = TechFits(a.tech, colonyTech, preIndustrial), fb = TechFits(b.tech, colonyTech, preIndustrial);
            if (fa != fb) return fa;
            if (!fa)
            {
                int ga = Math.Abs(a.tech - colonyTech), gb = Math.Abs(b.tech - colonyTech);
                if (ga != gb) return ga < gb;
            }
            if (a.distance != b.distance) return a.distance < b.distance;
            return a.id < b.id;
        }

        // ---------------- How many come ----------------

        /// <summary>How much room a load takes: its weight, plus a little for each stack of it.</summary>
        public static float Bulk(float massKg, float stacks) => Math.Max(0f, massKg) + Math.Max(0f, stacks) * StackBulk;

        /// <summary>How many people come for a load of this bulk: one for a purse, up to three for a cart's worth.</summary>
        public static int People(float bulk)
        {
            if (bulk <= PersonLoad) return 1;
            int n = (int)Math.Ceiling(bulk / PersonLoad);
            return Math.Max(1, Math.Min(MaxPeople, n));
        }

        /// <summary>
        /// A pack animal comes when their caravans use them and the load is more than one person carries; never for a
        /// purse of silver or a letter's worth.
        /// </summary>
        public static bool PackAnimal(float bulk, bool usesCarriers) => usesCarriers && bulk > PersonLoad;

        /// <summary>
        /// With a pack animal, one person fewer is needed for the same load (the animal carries the most); never fewer
        /// than one, and a big load still brings two to lead and load.
        /// </summary>
        public static int PeopleWithAnimal(float bulk, bool animal)
        {
            int n = People(bulk);
            if (!animal) return n;
            return Math.Max(bulk > PersonLoad * 2f ? 2 : 1, n - 1);
        }

        /// <summary>
        /// Shares <paramref name="count"/> out by each bearer's room: whole units, as close to their share as can be,
        /// adding up to the count exactly. A bearer with no room gets nothing (unless nobody has room: then the first).
        /// </summary>
        public static int[] Split(int count, IList<float> room)
        {
            int n = room?.Count ?? 0;
            var result = new int[n];
            if (n == 0 || count <= 0) return result;
            float total = 0f;
            for (int i = 0; i < n; i++) total += Math.Max(0f, room[i]);
            if (total <= 0f)
            {
                result[0] = count;
                return result;
            }
            int given = 0;
            var rest = new float[n];
            for (int i = 0; i < n; i++)
            {
                float exact = count * Math.Max(0f, room[i]) / total;
                result[i] = (int)Math.Floor(exact);
                rest[i] = exact - result[i];
                given += result[i];
            }
            // What rounding left over goes to those with the biggest remainders (the first of equals).
            while (given < count)
            {
                int pick = 0;
                for (int i = 1; i < n; i++)
                    if (rest[i] > rest[pick]) pick = i;
                result[pick]++;
                rest[pick] = -1f;
                given++;
            }
            return result;
        }

        // ---------------- When they come ----------------

        /// <summary>By day, when they set out to be here.</summary>
        public static bool Daytime(int hour) => hour >= DayFrom && hour < DayTo;

        /// <summary>What a waiting trip does now.</summary>
        public static CourierWait Verdict(int now, int due, int giveUp, int hour, bool safe, bool settingOn, bool mapThere)
        {
            if (!mapThere) return CourierWait.Drop;
            if (!settingOn) return CourierWait.Settle;
            if (now > giveUp) return CourierWait.Settle;
            if (now < due || !Daytime(hour) || !safe) return CourierWait.Wait;
            return CourierWait.Go;
        }

        // ---------------- When it ends ----------------

        /// <summary>
        /// Goods still owed to the colony when a trip ends this way are settled the old way (set down where traders
        /// leave goods): always, unless the colony hurt them (then what they still carry goes where they go, or lies
        /// with them) or the map is gone.
        /// </summary>
        public static bool SettleIncoming(CourierEnd end) => end != CourierEnd.Harmed && end != CourierEnd.MapGone;

        /// <summary>
        /// Goods the colony still owes when a trip ends this way leave the stores the old way: unless the colony hurt
        /// them, their people turned hostile (the deal holds as far as it got), or the map is gone.
        /// </summary>
        public static bool SettleOutgoing(CourierEnd end) => end != CourierEnd.Harmed && end != CourierEnd.Hostile && end != CourierEnd.MapGone;

        /// <summary>What's still to hand over: never below nothing.</summary>
        public static int Left(int amount, int done) => Math.Max(0, amount - Math.Max(0, done));

        /// <summary>
        /// Collecting: whether there is anything more to wait for. Done once all is taken, or when the colony has none
        /// of it left at all (used up meanwhile: they take what there was, and the deal stands).
        /// </summary>
        public static bool CollectDone(int amount, int taken, int stillInStores) => Left(amount, taken) == 0 || stillInStores <= 0;

        /// <summary>A trip's business is over (or it has run out of time), and they can leave.</summary>
        public static bool WorkOver(int now, int arrived, int limit) => arrived >= 0 && now - arrived >= limit;
    }
}
