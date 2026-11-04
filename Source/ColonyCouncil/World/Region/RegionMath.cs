using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>A town's fortune in words: the bands of its prosperity (0 to 100).</summary>
    public enum Fortune
    {
        /// <summary>Under 20.</summary>
        Struggling,
        /// <summary>20 to 40.</summary>
        Poor,
        /// <summary>40 to 60.</summary>
        Steady,
        /// <summary>60 to 80.</summary>
        Thriving,
        /// <summary>80 and above.</summary>
        Flourishing
    }

    /// <summary>What a town on the world ledger is: an ordinary town, a ruin, a trading post or a hamlet (the last three come in later releases).</summary>
    public enum TownKind : byte
    {
        Town,
        Ruin,
        Post,
        Hamlet
    }

    /// <summary>What becomes of a founding record at the day's look (RegionMath.FoundingDay).</summary>
    public enum FoundingFate : byte
    {
        Keep,
        Built,
        LetGo
    }

    /// <summary>
    /// The region's small rules (docs/design/stage1-region.md; DESIGN.md, "The region"): which towns are kept in detail
    /// (the reach), how remote a home is, a town's prosperity (its bands, its seed, what a world event does to it, the
    /// drift back, a war near it), the new towns' founding (why settlers chose the place, the founder's letter, their
    /// first year) and the bounds of what is kept. Pure (no game types): Tools/Tests (RegionTests) checks them.
    /// </summary>
    public static class RegionMath
    {
        public const int DayTicks = 60000;

        // ---------------- The reach in detail ----------------

        /// <summary>The towns kept in detail for each home: the nearest so many within the region map's reach.</summary>
        public const int MaxTownsPerHome = 40;

        /// <summary>The region map's reach when the setting can't be read (CouncilSettings.regionMapRadius).</summary>
        public const float DefaultReach = 35f;

        /// <summary>
        /// The towns a home keeps in detail, as indices into the lists, nearest first: those that <paramref name="fit"/>
        /// (a town of a humanlike people that isn't the player's) within <paramref name="reach"/> tiles, the nearest
        /// <paramref name="max"/> of them. A town at the same distance as another keeps the order it came in.
        /// </summary>
        public static List<int> InReach(IList<float> distances, IList<bool> fit, float reach, int max = MaxTownsPerHome)
        {
            var result = new List<int>();
            int n = distances?.Count ?? 0;
            if (n == 0 || max <= 0) return result;
            var order = new List<int>();
            for (int i = 0; i < n; i++)
                if ((fit == null || i < fit.Count && fit[i]) && distances[i] <= reach)
                    order.Add(i);
            order.Sort((a, b) => distances[a] != distances[b] ? distances[a].CompareTo(distances[b]) : a.CompareTo(b));
            for (int i = 0; i < order.Count && result.Count < max; i++) result.Add(order[i]);
            return result;
        }

        /// <summary>Kept in detail for any home: the union of each home's towns, each once, in the order first seen.</summary>
        public static List<int> Union(IEnumerable<IList<int>> perHome)
        {
            var seen = new HashSet<int>();
            var all = new List<int>();
            if (perHome == null) return all;
            foreach (var list in perHome)
                if (list != null)
                    foreach (int id in list)
                        if (seen.Add(id))
                            all.Add(id);
            return all;
        }

        // ---------------- Remoteness ----------------

        /// <summary>A home with a town of a people at peace this near (tiles) is not remote at all.</summary>
        public const float RemoteNear = 12f;
        /// <summary>From this far (tiles) to its nearest, a home is as remote as distance makes it.</summary>
        public const float RemoteFar = 40f;
        /// <summary>Added when fewer than <see cref="LoneTowns"/> towns of peoples at peace stand within <see cref="RemoteFar"/> tiles.</summary>
        public const float LoneStep = 0.2f;
        public const int LoneTowns = 2;

        /// <summary>
        /// How remote a home is, 0 to 1: 0 with a town of a humanlike people at peace within <see cref="RemoteNear"/> tiles,
        /// rising evenly to 1 at <see cref="RemoteFar"/>, plus <see cref="LoneStep"/> with fewer than <see cref="LoneTowns"/>
        /// such towns within <see cref="RemoteFar"/>, at most 1. <paramref name="nearest"/>: the tiles to the nearest such
        /// town (a negative number or infinity for none).
        /// </summary>
        public static float Remoteness(float nearest, int within)
        {
            float r = nearest < 0f || float.IsInfinity(nearest) || float.IsNaN(nearest) ? 1f : Clamp01((nearest - RemoteNear) / (RemoteFar - RemoteNear));
            if (within < LoneTowns) r += LoneStep;
            return Math.Min(1f, r);
        }

        /// <summary>How remote in words (0 to 4): close to its neighbours, a little out of the way, rather remote, remote, far from everyone.</summary>
        public static int RemoteLevel(float remoteness)
        {
            if (remoteness < 0.1f) return 0;
            if (remoteness < 0.35f) return 1;
            if (remoteness < 0.6f) return 2;
            if (remoteness < 0.9f) return 3;
            return 4;
        }

        // ---------------- Prosperity ----------------

        public const float MinProsperity = 0f;
        public const float MaxProsperity = 100f;
        /// <summary>A town first seen starts here (less on poor land and in hard times).</summary>
        public const float Start = 50f;
        /// <summary>A town just founded starts here.</summary>
        public const float NewTownStart = 30f;
        /// <summary>A town taken in an invasion starts again here under its new people.</summary>
        public const float TakenStart = 25f;
        /// <summary>A town on the poorest land starts this much lower (its biome's settlement weight 0; nothing off at 1 or more).</summary>
        public const float PoorLandStart = 15f;
        /// <summary>A town whose people are in hard times (a bad world event about them) starts this much lower.</summary>
        public const float HardTimesStart = 10f;
        /// <summary>Land whose biome's settlement weight is under this is poor land: its towns drift back to <see cref="PoorTarget"/>.</summary>
        public const float PoorLand = 0.5f;
        /// <summary>Every town drifts back toward this, by <see cref="DriftPerDay"/> a day.</summary>
        public const float Target = 50f;
        public const float PoorTarget = 45f;
        public const float DriftPerDay = 0.3f;
        /// <summary>A world event's move comes in over its first so many days, a share each day, so it is seen while it runs.</summary>
        public const int SpreadDays = 5;
        /// <summary>What a world event's people's other towns in detail take of its move (the event's own town takes it all).</summary>
        public const float OtherTowns = 0.5f;
        /// <summary>A town within <see cref="WarTiles"/> of a town of a people its own is at war with loses this much a day.</summary>
        public const float WarPerDay = 0.3f;
        public const float WarTiles = 20f;
        /// <summary>Under this a town is low, and the ledger keeps since when (the next release gives such a town up after a while).</summary>
        public const float Low = 15f;

        /// <summary>The band a prosperity falls in: struggling under 20, poor to 40, steady to 60, thriving to 80, flourishing above.</summary>
        public static Fortune Band(float p)
        {
            if (p < 20f) return Fortune.Struggling;
            if (p < 40f) return Fortune.Poor;
            if (p < 60f) return Fortune.Steady;
            if (p < 80f) return Fortune.Thriving;
            return Fortune.Flourishing;
        }

        /// <summary>The band's tag, as the lines are tagged: town:struggling, town:poor, town:steady, town:thriving, town:flourishing.</summary>
        public static string Tag(Fortune f) => "town:" + f.ToString().ToLowerInvariant();

        /// <summary>Within 0 and <paramref name="cap"/> (100, or a trading post's 70).</summary>
        public static float Clamp(float p, float cap = MaxProsperity) => Math.Max(MinProsperity, Math.Min(Math.Min(cap, MaxProsperity), p));

        /// <summary>
        /// Where a town first seen starts: <see cref="Start"/>, less on poor land (by its biome's settlement weight, the
        /// game's own sense of where towns stand: up to <see cref="PoorLandStart"/> less at 0) and while its people are in
        /// hard times.
        /// </summary>
        public static float Seed(float settlementWeight, bool hardTimes)
        {
            float w = Clamp01(settlementWeight);
            float p = Start - PoorLandStart * (1f - w);
            if (hardTimes) p -= HardTimesStart;
            return Clamp(p);
        }

        /// <summary>Poor land: its biome's settlement weight under <see cref="PoorLand"/>.</summary>
        public static bool IsPoorLand(float settlementWeight) => settlementWeight < PoorLand;

        /// <summary>What a town drifts back toward: 50, or 45 on poor land.</summary>
        public static float DriftTarget(float settlementWeight) => IsPoorLand(settlementWeight) ? PoorTarget : Target;

        /// <summary>A prosperity after <paramref name="days"/> of drifting toward <paramref name="target"/>, never past it.</summary>
        public static float Drift(float p, float target, float days)
        {
            if (days <= 0f) return p;
            float step = DriftPerDay * days;
            if (p < target) return Math.Min(target, p + step);
            if (p > target) return Math.Max(target, p - step);
            return p;
        }

        /// <summary>What a town takes of a world event's move: all of it at the event's own town, half at its people's other towns.</summary>
        public static float Share(float total, bool atEventTown) => atEventTown ? total : total * OtherTowns;

        /// <summary>
        /// How many days' shares of a world event's move are due: one for each day begun (<paramref name="daysIn"/> since it
        /// started), at most <see cref="SpreadDays"/>, less the <paramref name="applied"/> ones given already. Never negative.
        /// </summary>
        public static int DaysDue(float daysIn, int applied)
        {
            if (daysIn < 0f) return 0;
            int due = Math.Min(SpreadDays, (int)Math.Floor(daysIn) + 1);
            return Math.Max(0, due - Math.Max(0, applied));
        }

        /// <summary>One day's share of a move spread over <see cref="SpreadDays"/>.</summary>
        public static float DayShare(float total) => total / SpreadDays;

        /// <summary>A town low (under <see cref="Low"/>) since when: kept while it stays low, from now when it first goes low, -1 once it is not.</summary>
        public static int LowSince(float p, int was, int now) => p < Low ? (was >= 0 ? was : now) : -1;

        /// <summary>A day's move from a war: a town near a town of a people its own is at war with loses <see cref="WarPerDay"/>.</summary>
        public static float WarMove(float nearestEnemyTown) => nearestEnemyTown >= 0f && nearestEnemyTown <= WarTiles ? -WarPerDay : 0f;

        /// <summary>A trading post's prosperity stays at or under this (the trading posts' release).</summary>
        public const float PostCap = 70f;

        /// <summary>What moved a town last is named in its panel this long after.</summary>
        public const int CauseDays = 60;

        /// <summary>
        /// The cause is named beside the band only when it fits the way the town now stands: a blow (way -1) while it is
        /// steady or worse, a boon (way 1) while it is steady or better, and a neutral one (way 0, its founding) always.
        /// </summary>
        public static bool CauseFits(Fortune band, int way) => way == 0 || (way < 0 ? band <= Fortune.Steady : band >= Fortune.Steady);

        // ---------------- What it sells (decision 3) ----------------

        /// <summary>What a town's restocked trade goods are, for its fortune's say in them.</summary>
        public enum StockKind
        {
            Other,
            Food,
            Medicine,
            Silver
        }

        /// <summary>A struggling town has a quarter less food, medicine and silver to trade.</summary>
        public const float StrugglingStock = 0.75f;
        /// <summary>A thriving town a little more silver, a flourishing one more again.</summary>
        public const float ThrivingSilver = 1.1f, FlourishingSilver = 1.2f;

        /// <summary>How much of a kind of goods a town of this fortune has after the game restocks it (1: as the game made it).</summary>
        public static float StockFactor(Fortune band, StockKind kind)
        {
            if (kind == StockKind.Other) return 1f;
            if (band == Fortune.Struggling) return StrugglingStock;
            if (kind != StockKind.Silver) return 1f;
            if (band == Fortune.Thriving) return ThrivingSilver;
            if (band == Fortune.Flourishing) return FlourishingSilver;
            return 1f;
        }

        /// <summary>How many of a good (all its stacks) the town has of <paramref name="total"/>: rounded, a half up, never below 0 (a lone item stays).</summary>
        public static int StockTarget(int total, float factor)
        {
            if (total <= 0 || factor == 1f) return Math.Max(0, total);
            return Math.Max(0, (int)Math.Round(total * factor, MidpointRounding.AwayFromZero));
        }

        // ---------------- New towns ----------------

        /// <summary>A town is new for its first year (60 days): its notables are kept beside the nearest four, and its people speak of its founding.</summary>
        public const int NewTownDays = 60;

        /// <summary>Founded (a tick, -1 for not known) within <see cref="NewTownDays"/> of <paramref name="now"/>.</summary>
        public static bool IsNew(int founded, int now) => founded >= 0 && now >= founded && now - founded < (long)NewTownDays * DayTicks;

        /// <summary>Founders made at most this many a day (making people is slow).</summary>
        public const int FoundersADay = 2;
        /// <summary>Founding records kept at most (a site being built within reach), the oldest going first.</summary>
        public const int MaxFoundings = 6;

        /// <summary>
        /// A founding record at the day's look: kept (<see cref="FoundingFate.Keep"/>), its town found standing
        /// (<see cref="FoundingFate.Built"/>, its faces come in the region's hour), or let go with its founder
        /// (<see cref="FoundingFate.LetGo"/>). Built already, it waits for its faces only while the new towns' faces are on
        /// (off, the hour that gives them stands down); still being built, while its site stands within reach; finished
        /// meanwhile, it is built when its town stands within reach with the faces on.
        /// </summary>
        public static FoundingFate FoundingDay(bool built, bool facesOn, bool building, bool inReach, bool townThere)
        {
            if (built) return facesOn ? FoundingFate.Keep : FoundingFate.LetGo;
            if (building && inReach) return FoundingFate.Keep;
            return townThere && facesOn && inReach ? FoundingFate.Built : FoundingFate.LetGo;
        }
        /// <summary>Families (houses) kept per town in detail: the founder's line and one more founding family.</summary>
        public const int HousesPerTown = 2;
        /// <summary>Members known by name per house.</summary>
        public const int MembersPerHouse = 6;

        /// <summary>A founder is of an age that leads settlers out: 30 to 55.</summary>
        public const float FounderMinAge = 30f, FounderMaxAge = 55f;

        /// <summary>A new town this near a home (tiles) sends its founder's first letter.</summary>
        public const float LetterTiles = 15f;
        /// <summary>At most one founder's letter a season (15 days) per home.</summary>
        public const int LetterGapDays = 15;

        /// <summary>A new town's founder writes to this home: near enough, and no founder's letter to it this season.</summary>
        public static bool LetterDue(float tiles, int lastLetter, int now) =>
            tiles >= 0f && tiles <= LetterTiles && (lastLetter < 0 || now - lastLetter >= (long)LetterGapDays * DayTicks);

        /// <summary>The full welcome (a runner at the gate, a gift hoped for) comes to a home at most once a year (60 days).</summary>
        public const int WelcomeGapDays = 60;

        /// <summary>
        /// A new town this near gets the full welcome instead of its founder's letter: within <see cref="LetterTiles"/>,
        /// and no full welcome at this home in the last year. (When no world event's slot is free, the letter comes.)
        /// </summary>
        public static bool WelcomeDue(float tiles, int lastWelcome, int now) =>
            tiles >= 0f && tiles <= LetterTiles && (lastWelcome < 0 || now - lastWelcome >= (long)WelcomeGapDays * DayTicks);

        /// <summary>A town of theirs given up this lately sends its people out as settlers (the reason "refugees").</summary>
        public const int GivenUpDays = 120;

        /// <summary>
        /// Why settlers chose a place, read from the world, strongest first: the town they left given up lately
        /// ("refugees"), the old walls of a ruin ("ruin"), their mother town flourishing ("mother"), a road, a river, the
        /// coast, the hills. With none, the land itself ("land").
        /// </summary>
        public static readonly string[] Reasons = { "refugees", "ruin", "mother", "road", "river", "coast", "hills" };

        public const string AnyLand = "land";

        /// <summary>
        /// The reason a founding gives, from those that hold (<paramref name="holds"/>, any order): the strongest
        /// (<see cref="Reasons"/>' order) when <paramref name="roll"/> is under 0.6, else one of the others by the roll;
        /// <see cref="AnyLand"/> when none holds.
        /// </summary>
        public static string WhyFounded(ICollection<string> holds, float roll)
        {
            var have = new List<string>();
            if (holds != null)
                foreach (var r in Reasons)
                    if (holds.Contains(r))
                        have.Add(r);
            if (have.Count == 0) return AnyLand;
            if (have.Count == 1 || roll < 0.6f) return have[0];
            int i = 1 + (int)Math.Floor((Clamp01(roll) - 0.6f) / 0.4f * (have.Count - 1));
            return have[Math.Min(have.Count - 1, Math.Max(1, i))];
        }

        // ---------------- Bounds ----------------

        /// <summary>Over <paramref name="max"/>, the oldest go first (the list in the order they came). Returns how many went.</summary>
        public static int TrimOldest<T>(List<T> list, int max)
        {
            if (list == null || list.Count <= max) return 0;
            int n = list.Count - Math.Max(0, max);
            list.RemoveRange(0, n);
            return n;
        }

        /// <summary>
        /// A town record stays in the ledger while a notable lives there, while it stands within the reach of a home (in
        /// detail), and a gone one <see cref="LedgerMath.GoneTownDays"/> days from when it was missed. A standing town out
        /// of every home's reach with nobody there is let go: beyond the reach Rimfolk keeps nothing.
        /// </summary>
        public static bool KeepTown(bool notableThere, bool stands, bool inDetail, int gone, int now, int dayTicks = DayTicks) =>
            notableThere || stands && inDetail || LedgerMath.KeepTown(false, stands, gone, now, dayTicks);

        // ---------------- Words ----------------

        /// <summary>Days on the road for each tile, at a caravan's walking pace over open ground, as the news reckons a distance.</summary>
        public const float DaysPerTile = 0.33f;

        /// <summary>About how many days a walk of so many tiles takes.</summary>
        public static float WalkDays(float tiles) => Math.Max(0f, tiles) * DaysPerTile;

        /// <summary>How far in days of travel as words (0 to 3): less than a day, about a day, two days, or more (then the days are counted).</summary>
        public static int DaysLevel(float days)
        {
            if (days < 0.75f) return 0;
            if (days < 1.5f) return 1;
            if (days < 2.5f) return 2;
            return 3;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
