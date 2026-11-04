using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>How a trading post ended (or why it is to end): each has its words and its history kind.</summary>
    public enum PostEnd : byte
    {
        /// <summary>Relations soured: goodwill under 0 for ten days brought the keeper's warning, and ten more closed it.</summary>
        Soured,
        /// <summary>Their people went to war with the colony.</summary>
        War,
        /// <summary>Raiders of a people at war with them sacked it, and they gave it up.</summary>
        Sacked,
        /// <summary>The colony's own people attacked it and destroyed it.</summary>
        Razed,
        /// <summary>Gone without a story of its own (taken, made a vassal, given up for hard times, the home lost): its record ends.</summary>
        Gone
    }

    /// <summary>
    /// A trading post's small rules (docs/design/stage1-region.md, item 3; decision 6): which people opens one for a far
    /// colony (at peace and on good terms, trading with it often, its towns far, keeping town traders), when (a look every
    /// 15 days per home, two posts at most, one per people, none within 60 days of another opening or closing there),
    /// where (6 to 12 tiles from the home, toward their nearest town where it can, a road or a river first, clear of every
    /// home and town), what it is called and who keeps it by their age, the souring clock (the keeper's warning after ten
    /// days under 0 goodwill, the closing ten days later), its sacking, and its effects on caravans and the bandit road.
    /// Pure (no game types): Tools/Tests (TradingPostTests) checks them.
    /// </summary>
    public static class TradingPostMath
    {
        public const int DayTicks = 60000;

        // ---------------- Who opens one ----------------

        /// <summary>A people with at least this goodwill (or allied with the colony) may open a post.</summary>
        public const int MinGoodwill = 40;
        /// <summary>Its memory holds trade with the colony this many times or more in the last <see cref="TradeDays"/>.</summary>
        public const int MinTrades = 3;
        public const int TradeDays = 120;
        /// <summary>Its nearest town is at least this far from the home (tiles): a far colony, which a post brings near.</summary>
        public const float FarTiles = 15f;

        /// <summary>
        /// A people may open a post near this home: humanlike, not at war with the colony, goodwill 40 or more (or an
        /// ally), trade with the colony three times or more in the last 120 days, its nearest town 15 tiles or more away
        /// (<paramref name="nearestTown"/> negative: no town on the home's layer, so nothing to face toward), keeping
        /// town traders, and no post of theirs at this home yet.
        /// </summary>
        public static bool PeopleFits(bool humanlike, bool hostile, int goodwill, bool ally, int trades, float nearestTown, bool townTraders, bool hasPostHere) =>
            humanlike && !hostile && (goodwill >= MinGoodwill || ally) && trades >= MinTrades && nearestTown >= FarTiles && townTraders && !hasPostHere;

        /// <summary>Which people opens the post, of those that qualify: the most trade first, then the best goodwill, then the first. -1 for none.</summary>
        public static int Pick(IList<int> trades, IList<int> goodwill)
        {
            int n = trades?.Count ?? 0, best = -1;
            for (int i = 0; i < n; i++)
            {
                if (best < 0)
                {
                    best = i;
                    continue;
                }
                if (trades[i] != trades[best])
                {
                    if (trades[i] > trades[best]) best = i;
                    continue;
                }
                int g = goodwill != null && i < goodwill.Count ? goodwill[i] : 0, bg = goodwill != null && best < goodwill.Count ? goodwill[best] : 0;
                if (g > bg) best = i;
            }
            return best;
        }

        // ---------------- When ----------------

        /// <summary>Each home is looked at every so many days.</summary>
        public const int LookDays = 15;
        /// <summary>The chance at a look when a people qualifies (a post in about 75 days of qualifying).</summary>
        public const float Chance = 0.2f;
        /// <summary>Posts per home at most (and one per people).</summary>
        public const int MaxPerHome = 2;
        /// <summary>None opens within so many days of another opening or closing at that home.</summary>
        public const int GapDays = 60;

        public static bool LookDue(int lookedAt, int now) => lookedAt < 0 || now - lookedAt >= (long)LookDays * DayTicks;

        /// <summary>The home has room for a post: fewer than two, and none opened or closed there in the last 60 days (<paramref name="changedAt"/> -1: never).</summary>
        public static bool HomeAllows(int posts, int changedAt, int now) => posts < MaxPerHome && (changedAt < 0 || now - changedAt >= (long)GapDays * DayTicks);

        // ---------------- Where ----------------

        /// <summary>The post stands this far from the home (tiles, in a straight line).</summary>
        public const float MinTiles = 6f, MaxTiles = 12f;
        /// <summary>Toward their nearest town: within so many degrees of its heading from the home.</summary>
        public const float HeadingDegrees = 60f;
        /// <summary>At least this far from any other town.</summary>
        public const float TownGap = 3f;
        /// <summary>A tile on a road weighs this much more, one on a river this much (goods go by road and water).</summary>
        public const float RoadWeight = 3f, RiverWeight = 2f;

        /// <summary>
        /// A tile a post may stand on: the game holds it valid for a town and nothing else is on it (<paramref name="valid"/>),
        /// 6 to 12 tiles from the home, clear of every one of the colony's settlements by the game's own goodwill reach
        /// (RiseFallMath.ClearOfHomes: more than 6.5 tiles), 3 tiles or more from any other town (<paramref name="nearestTown"/>
        /// negative: none), on land where the game would put a town at all (its biome's settlement weight above 0).
        /// </summary>
        public static bool PlaceFits(bool valid, float fromHome, float nearestOurs, int goodwillTiles, float nearestTown, float settlementWeight) =>
            valid && fromHome >= MinTiles && fromHome <= MaxTiles && RiseFallMath.ClearOfHomes(nearestOurs, goodwillTiles)
            && (nearestTown < 0f || nearestTown >= TownGap) && settlementWeight > 0f;

        /// <summary>
        /// A post is still near its home: on the home's layer of the world and within the region map's reach of it
        /// (<paramref name="reach"/>). Its home's records carried far away (a gravship landed elsewhere, the colony settled
        /// anew), it is no longer that home's post.
        /// </summary>
        public static bool StaysNear(bool sameLayer, float tiles, float reach) => sameLayer && tiles >= 0f && tiles <= reach;

        /// <summary>A tile whose heading from the home is within <see cref="HeadingDegrees"/> of their nearest town's (an angle of 0 to 180).</summary>
        public static bool Toward(float degrees) => degrees >= 0f && degrees <= HeadingDegrees;

        /// <summary>How much a tile weighs: its biome's settlement weight (the game's sense of where towns stand), three times on a road, twice on a river.</summary>
        public static float PlaceWeight(bool road, bool river, float settlementWeight) => Math.Max(0f, settlementWeight) * (road ? RoadWeight : river ? RiverWeight : 1f);

        /// <summary>
        /// The tile the post goes on, by a roll of 0 to 1: among those toward their nearest town when there are any, else
        /// among all, by weight (RiseFallMath.PickTile). -1 when none weighs anything: no post.
        /// </summary>
        public static int PickTile(IList<bool> toward, IList<float> weights, float roll)
        {
            int n = weights?.Count ?? 0;
            if (n == 0) return -1;
            bool anyToward = false;
            for (int i = 0; i < n; i++)
                if (toward != null && i < toward.Count && toward[i] && weights[i] > 0f)
                    anyToward = true;
            if (!anyToward) return RiseFallMath.PickTile(weights, roll);
            var only = new List<float>(n);
            for (int i = 0; i < n; i++) only.Add(toward != null && i < toward.Count && toward[i] ? weights[i] : 0f);
            return RiseFallMath.PickTile(only, roll);
        }

        /// <summary>
        /// The angle in degrees (0 to 180) at <paramref name="h"/> between the ways to <paramref name="a"/> and to
        /// <paramref name="b"/>, three points on a sphere centred on the origin (tile centres): each way is taken on the
        /// ground at <paramref name="h"/> (its part along the sphere's normal there left out), as a compass would read it.
        /// -1 when a way has no direction (a point on top of <paramref name="h"/>, or <paramref name="h"/> at the centre).
        /// </summary>
        public static float AngleAt(float hx, float hy, float hz, float ax, float ay, float az, float bx, float by, float bz)
        {
            double hl = Math.Sqrt(hx * (double)hx + hy * (double)hy + hz * (double)hz);
            if (hl < 1e-9) return -1f;
            double nx = hx / hl, ny = hy / hl, nz = hz / hl;
            double ux = ax - hx, uy = ay - hy, uz = az - hz;
            double vx = bx - hx, vy = by - hy, vz = bz - hz;
            double du = ux * nx + uy * ny + uz * nz, dv = vx * nx + vy * ny + vz * nz;
            ux -= du * nx;
            uy -= du * ny;
            uz -= du * nz;
            vx -= dv * nx;
            vy -= dv * ny;
            vz -= dv * nz;
            double lu = Math.Sqrt(ux * ux + uy * uy + uz * uz), lv = Math.Sqrt(vx * vx + vy * vy + vz * vz);
            if (lu < 1e-9 || lv < 1e-9) return -1f;
            double c = (ux * vx + uy * vy + uz * vz) / (lu * lv);
            c = Math.Max(-1.0, Math.Min(1.0, c));
            return (float)(Math.Acos(c) * 180.0 / Math.PI);
        }

        // ---------------- What it is ----------------

        /// <summary>A post's prosperity starts here (the region's fortunes), and stays at or under RegionMath.PostCap (70).</summary>
        public const float Start = 40f;

        public static float Cap => RegionMath.PostCap;

        /// <summary>Its name by their age: "{TOWN} Trading Camp", "{TOWN} Market", "{TOWN} Depot", "{TOWN} Ground Station".</summary>
        public static string NameKey(WorldAge age) => "CC_Post_Name_" + age;

        /// <summary>Its keeper's title by their age: trader, factor, manager, quartermaster.</summary>
        public static string KeeperKey(WorldAge age) => "CC_Post_Keeper_" + age;

        // ---------------- Souring ----------------

        /// <summary>Goodwill under 0 for this many days brings the keeper's warning.</summary>
        public const int WarnDays = 10;
        /// <summary>Still under 0 this many days after the warning, the post closes.</summary>
        public const int CloseDays = 10;

        /// <summary>Since when relations have been sour (goodwill under 0): kept while they stay so, from now when they first sour, -1 once they don't.</summary>
        public static int SourSince(int goodwill, int was, int now) => goodwill < 0 ? (was >= 0 ? was : now) : -1;

        /// <summary>The keeper's warning is due: sour for ten days, and not warned yet.</summary>
        public static bool WarnDue(int sourSince, int warnedAt, int now) => sourSince >= 0 && warnedAt < 0 && now - sourSince >= (long)WarnDays * DayTicks;

        /// <summary>The post closes: warned, and still sour ten days after the warning.</summary>
        public static bool CloseDue(int sourSince, int warnedAt, int now) => sourSince >= 0 && warnedAt >= 0 && now - warnedAt >= (long)CloseDays * DayTicks;

        /// <summary>Things mended after a warning: relations no longer sour, so the warning is taken back.</summary>
        public static bool Mended(int sourSince, int warnedAt) => warnedAt >= 0 && sourSince < 0;

        // ---------------- What it does ----------------

        /// <summary>Their trader caravans come this much more often while their post stands near the home.</summary>
        public const float CaravanFactor = 1.5f;
        /// <summary>The bandit road world event is this much likelier while a post stands near the home: goods on the road draw robbers.</summary>
        public const float BanditRoadFactor = 2f;
        public const string BanditRoad = "Council_WS_BanditRoad";

        /// <summary>How likely their trader caravan is to be the one that comes: half again as likely while their post stands near the home.</summary>
        public static float Caravans(bool postNear) => postNear ? CaravanFactor : 1f;

        /// <summary>How likely a world event is to be the one that starts at a home: the bandit road twice as likely while a post stands near it.</summary>
        public static float EventFactor(string defName, bool postNear) => postNear && defName == BanditRoad ? BanditRoadFactor : 1f;

        // ---------------- Sacked ----------------

        /// <summary>A post whose people are at war with a people whose town is this near it (tiles) may be sacked.</summary>
        public const float SackTiles = 20f;
        /// <summary>Looked at every so many days, sacked on this roll.</summary>
        public const int SackEveryDays = 15;
        public const float SackChance = 0.05f;
        /// <summary>A sack costs it this much prosperity; one it can't take (under <see cref="SackGivesUpUnder"/> after it) makes its people give it up.</summary>
        public const float SackLoss = 30f;
        public const float SackGivesUpUnder = 10f;

        /// <summary>At risk of a sack: a town of a people its own is at war with within <see cref="SackTiles"/> (negative: none).</summary>
        public static bool SackRisk(float nearestEnemyTown) => nearestEnemyTown >= 0f && nearestEnemyTown <= SackTiles;

        /// <summary>Its prosperity after a sack (never under 0).</summary>
        public static float AfterSack(float prosperity) => Math.Max(0f, prosperity - SackLoss);

        /// <summary>A sack it can't take: under 10 after it (a post with no fortune of its own, the fortunes off, counts from where it started).</summary>
        public static bool SackGivesUp(float prosperity) => AfterSack(prosperity < 0f ? Start : prosperity) < SackGivesUpUnder;

        // ---------------- Memory and history ----------------

        /// <summary>The deed their people remember when the colony razes a post they opened for it: a heavy wrong.</summary>
        public const string DeedRazed = "postRazed";
        public const int RazedWeight = 30;

        /// <summary>The history's kinds, of the family world:post: opened (a milestone), the keeper's warning, mended, closed, sacked, razed.</summary>
        public const string Family = "world:post";
        public const string KindOpened = "world:post:opened", KindWarned = "world:post:warned", KindMended = "world:post:mended", KindClosed = "world:post:closed",
            KindSacked = "world:post:sacked", KindRazed = "world:post:razed";

        public static readonly string[] Kinds = { KindOpened, KindWarned, KindMended, KindClosed, KindSacked, KindRazed };

        /// <summary>How a post's end goes into the history: closed for souring, war and a sack it couldn't take; razed by the colony; nothing for one simply gone.</summary>
        public static string EndKind(PostEnd e)
        {
            switch (e)
            {
                case PostEnd.Soured:
                case PostEnd.War:
                case PostEnd.Sacked:
                    return KindClosed;
                case PostEnd.Razed:
                    return KindRazed;
                default:
                    return null;
            }
        }

        /// <summary>A post's own events are milestones of the colony's (it was opened for the colony); the everyday ones are the keeper's warning and things mended.</summary>
        public static bool Milestone(string kind) => kind == KindOpened || kind == KindClosed || kind == KindSacked || kind == KindRazed;
    }
}
