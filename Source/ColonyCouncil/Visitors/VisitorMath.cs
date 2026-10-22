using System;

namespace ColonyCouncil
{
    /// <summary>What a group of visitors (Visitors/) is on the map for.</summary>
    public enum VisitKind
    {
        /// <summary>At the gate while the colony answers their letter (a rider, a family asking to stay), or for a few hours when nothing is asked.</summary>
        Wait,
        /// <summary>Guests: they stay the night (or a few), and go on at first light.</summary>
        Stay,
        /// <summary>Passing by: mourners on the road, a band, a herd on its way. They may stop a while on the way through.</summary>
        Pass
    }

    public enum VisitState
    {
        /// <summary>Not on the map yet: they come at their hour, when it's safe.</summary>
        Pending,
        /// <summary>Walking in from the edge to their spot.</summary>
        Coming,
        /// <summary>At their spot: waiting, staying the night, resting the herd.</summary>
        Here,
        /// <summary>Walking off the map (back the way they came, or on past the colony).</summary>
        Leaving,
        /// <summary>Over: kept a little while, so what they carry off is cleared when they're gone.</summary>
        Done
    }

    /// <summary>How a visit came to an end.</summary>
    public enum VisitEnd
    {
        Done,
        /// <summary>They never came: no way in, never safe, too many on the map already.</summary>
        NoWay,
        /// <summary>The colony hurt, downed or arrested one of them.</summary>
        Harmed,
        /// <summary>Their people turned on the colony.</summary>
        Hostile,
        /// <summary>Raiders or worse came while they were here.</summary>
        Danger,
        /// <summary>The cold, the heat or hunger got to them before their business was done.</summary>
        Weather,
        MapGone,
        SettingOff,
        /// <summary>They were gone without a word (a mod, an old save).</summary>
        Lost,
        /// <summary>The colony took them in: they're colonists now.</summary>
        Joined,
        /// <summary>They waited as long as anyone would.</summary>
        TimedOut
    }

    /// <summary>What a pending visit does on its turn.</summary>
    public enum VisitWait
    {
        Wait,
        Go,
        /// <summary>They won't come after all (the map gone, the setting off, too long): the letter alone tells it.</summary>
        Drop
    }

    /// <summary>
    /// The pure rules of visitors (Visitors/): how many may be on the map, when a waiting group comes, when guests go on,
    /// when someone at the gate has waited long enough, and which way passers-by leave. No game types, so Tools/Tests can
    /// check it.
    /// </summary>
    public static class VisitorMath
    {
        public const int TicksPerHour = 2500;
        public const int TicksPerDay = 60000;

        /// <summary>At most this many groups of visitors on one map at once (couriers and the game's own visitors aside).</summary>
        public const int MaxHere = 3;

        /// <summary>At most this many visits kept at once, waiting or under way (the oldest still waiting is dropped).</summary>
        public const int MaxTrips = 8;

        /// <summary>At most this many people in a group, and animals with them.</summary>
        public const int MaxPeople = 6;
        public const int MaxAnimals = 8;

        /// <summary>Guests go on at first light: between these hours.</summary>
        public const int DawnFrom = 6;
        public const int DawnTo = 9;

        /// <summary>Nights a guest stays at most, whatever the text hopes.</summary>
        public const int MaxNights = 3;

        /// <summary>A group that can't come within this long after its hour doesn't come at all.</summary>
        public const int GiveUpTicks = TicksPerDay * 3 / 2;

        /// <summary>Someone at the gate waits at most this long for an answer.</summary>
        public const int MaxWaitTicks = TicksPerDay * 5 / 2;

        /// <summary>After the answer, how long they wait for what a colonist is bringing out to them.</summary>
        public const int ErrandWaitTicks = TicksPerHour * 8;

        /// <summary>How long they stay on once their business is done, before they walk off.</summary>
        public const int LingerTicks = TicksPerHour / 2;

        /// <summary>Records of visits that are over, kept a while for watching them walk off.</summary>
        public const int MaxKeepTicks = TicksPerDay;

        // ---------------- How many ----------------

        /// <summary>A group's size, within what one visit may bring.</summary>
        public static int People(int wanted) => Math.Max(1, Math.Min(MaxPeople, wanted));

        public static int Animals(int wanted) => Math.Max(0, Math.Min(MaxAnimals, wanted));

        /// <summary>Another group may come to this map now: fewer than <see cref="MaxHere"/> there already.</summary>
        public static bool Room(int onMap) => onMap < MaxHere;

        // ---------------- When they come ----------------

        /// <summary>The hour is in the window [from, to), which may run past midnight; an empty window (from == to) is any hour.</summary>
        public static bool InHours(int hour, int from, int to)
        {
            if (from == to) return true;
            return from < to ? hour >= from && hour < to : hour >= from || hour < to;
        }

        /// <summary>What a waiting visit does now.</summary>
        public static VisitWait Verdict(int now, int due, int giveUp, int hour, int from, int to, bool safe, bool settingOn, bool mapThere)
        {
            if (!mapThere || !settingOn || now > giveUp) return VisitWait.Drop;
            if (now < due || !InHours(hour, from, to) || !safe) return VisitWait.Wait;
            return VisitWait.Go;
        }

        // ---------------- How long they stay ----------------

        /// <summary>
        /// Hours from now (at <paramref name="hour"/>) until guests go on: first light after <paramref name="nights"/>
        /// nights. Answered in the small hours, the rest of that night counts as one, unless dawn is under two hours off.
        /// </summary>
        public static int HoursToDawn(int hour, int nights)
        {
            nights = Math.Max(1, Math.Min(MaxNights, nights));
            int h = DawnFrom - ((hour % 24) + 24) % 24;
            if (h < 2) h += 24;
            return h + 24 * (nights - 1);
        }

        /// <summary>
        /// Someone at the gate with a letter is done waiting: the letter answered (or gone) and nothing still being brought
        /// out to them, a little after; or they've waited as long as anyone would. Without a letter, after their hours.
        /// </summary>
        public static bool WaitOver(int now, int arrived, bool hasLetter, bool letterOpen, int answeredAt, bool errandComing, int waitTicks)
        {
            if (arrived < 0) return false;
            if (now - arrived >= MaxWaitTicks) return true;
            if (!hasLetter) return now - arrived >= Math.Max(0, waitTicks);
            if (letterOpen || answeredAt < 0) return false;
            if (errandComing && now - answeredAt < ErrandWaitTicks) return false;
            return now - answeredAt >= LingerTicks;
        }

        // ---------------- Which way they go ----------------

        /// <summary>
        /// Where on the far side of the map someone who came in at (<paramref name="x"/>, <paramref name="z"/>) heads to
        /// go on past the colony: the point opposite through the middle, pushed out to the edge.
        /// </summary>
        public static void Opposite(int x, int z, int sizeX, int sizeZ, out int ox, out int oz)
        {
            ox = sizeX - 1 - x;
            oz = sizeZ - 1 - z;
            if (sizeX <= 1 || sizeZ <= 1) return;
            // Out to the nearest edge from that point.
            int toLeft = ox, toRight = sizeX - 1 - ox, toBottom = oz, toTop = sizeZ - 1 - oz;
            int min = Math.Min(Math.Min(toLeft, toRight), Math.Min(toBottom, toTop));
            if (min == toLeft) ox = 0;
            else if (min == toRight) ox = sizeX - 1;
            else if (min == toBottom) oz = 0;
            else oz = sizeZ - 1;
        }
    }
}
