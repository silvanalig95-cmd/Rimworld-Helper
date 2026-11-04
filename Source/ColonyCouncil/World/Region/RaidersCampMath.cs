using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>How a raiders' camp near a home ended (or why it is gone): each has its history kind and its words.</summary>
    public enum CampEnd : byte
    {
        /// <summary>The colony attacked it and cleared it.</summary>
        Broken,
        /// <summary>The colony paid their price, and they gave their word to keep away for a year.</summary>
        Paid,
        /// <summary>Their people made peace with the colony.</summary>
        Peace,
        /// <summary>An ally of the colony burned it.</summary>
        Burned,
        /// <summary>Their raids failed, or the months went by with nothing to show: they moved on.</summary>
        GaveUp,
        /// <summary>Gone without a story (the home lost, their people gone, the camp never found): nothing is told.</summary>
        Gone
    }

    /// <summary>Where a raiders' camp stands in its life.</summary>
    public enum CampStage : byte
    {
        /// <summary>Set up, not yet seen: no marker on the world map, no raids.</summary>
        Hidden,
        /// <summary>Seen: the game's own bandit camp site stands on the world map, and its raids come.</summary>
        Found,
        /// <summary>The colony is paying their price: the silver is on its way out to them, and no raid comes meanwhile.</summary>
        Paying
    }

    /// <summary>
    /// A raiders' camp's small rules (docs/design/stage1-region.md, item 4; decision 7): when one is set up near a home
    /// (a rich colony, a people that can raid it and is on bad terms with it, the rate, the rest between camps), where
    /// (not within 5 tiles of any home, on land that can hold it, hills and forest first), how soon it is found, how often
    /// it raids (the game's own raid-source curve, at the rate), the give-up rule, what it is after an attack it survived,
    /// its price, its word and the ally that burns it. Pure (no game types): Tools/Tests (RaidersCampTests) checks them.
    /// </summary>
    public static class RaidersCampMath
    {
        public const int DayTicks = 60000;
        public const int HourTicks = 2500;

        // ---------------- When one is set up ----------------

        /// <summary>A home whose threat points (the game's measure of its wealth and strength) are this many or more: about a mid-game colony.</summary>
        public const float MinThreatPoints = 1000f;
        /// <summary>Never before this day of the game.</summary>
        public const int FirstDay = 60;
        /// <summary>Each home is looked at every so many days.</summary>
        public const int LookDays = 10;
        /// <summary>The chance at each look when a people qualifies (a camp in about 100 days), times the rate.</summary>
        public const float LookChance = 0.1f;
        /// <summary>No new camp at a home within so many days after the last one there ended.</summary>
        public const int RestDays = 60;
        /// <summary>A hostile people with a town this near the home (tiles) can set up a camp near it.</summary>
        public const float TownTiles = 60f;
        /// <summary>Or one whose territory (Faction Territories) reaches this near.</summary>
        public const float TerritoryTiles = 30f;
        /// <summary>Or one holding a grudge at least this big, wherever its towns are.</summary>
        public const float GrudgeAt = 20f;
        /// <summary>A grudge: the colony's wrongs they remember (their strength), each of their dead held against it, a vendetta sworn.</summary>
        public const float DeadWeight = 15f, VendettaWeight = 20f;

        /// <summary>A look is due at this home: never looked at, or <see cref="LookDays"/> since the last.</summary>
        public static bool LookDue(int lookedAt, int now) => lookedAt < 0 || now - lookedAt >= (long)LookDays * DayTicks;

        /// <summary>
        /// The colony itself is one a camp would be set up near: rich enough, the game far enough along, no camp standing
        /// at this home, and the rest after the last camp over (<paramref name="lastEnded"/> -1 for none).
        /// </summary>
        public static bool HomeFits(float threatPoints, int now, bool standing, int lastEnded) =>
            !standing && threatPoints >= MinThreatPoints && now >= (long)FirstDay * DayTicks && (lastEnded < 0 || now - lastEnded >= (long)RestDays * DayTicks);

        /// <summary>The chance at a look, by the rate setting (0 to 2): 10% at 100%, nothing at 0.</summary>
        public static float Chance(float rate) => Clamp01(LookChance * Rate(rate));

        /// <summary>The setting read safely: a broken number as the default, a negative one as off, at most 2.</summary>
        public static float Rate(float rate) => float.IsNaN(rate) || float.IsInfinity(rate) ? 1f : Math.Max(0f, Math.Min(2f, rate));

        /// <summary>How big a grudge a people holds: the strength of the wrongs they remember, their dead held against the colony, a vendetta.</summary>
        public static float Grudge(float wrongs, int dead, bool vendetta) => Math.Max(0f, wrongs) + DeadWeight * Math.Max(0, dead) + (vendetta ? VendettaWeight : 0f);

        /// <summary>
        /// A people can set up a camp near this home: one that can raid it, with a town within <see cref="TownTiles"/> or
        /// territory within <see cref="TerritoryTiles"/> (a negative number: none), or holding a grudge; never while a
        /// world event about them runs, nor while their word to keep away holds (unless they mean to break it).
        /// </summary>
        public static bool PeopleFits(bool canRaid, float townTiles, float territoryTiles, float grudge, bool eventAbout, bool promised)
        {
            if (!canRaid || eventAbout || promised) return false;
            bool near = townTiles >= 0f && townTiles <= TownTiles || territoryTiles >= 0f && territoryTiles <= TerritoryTiles;
            return near || grudge >= GrudgeAt;
        }

        /// <summary>
        /// Which people sets up the camp, of those that qualify (their grudges and how near their nearest town or territory
        /// lies, a negative number for none): the biggest grudge first, then the nearest. -1 for none.
        /// </summary>
        public static int Pick(IList<float> grudges, IList<float> tiles)
        {
            int n = grudges?.Count ?? 0, best = -1;
            for (int i = 0; i < n; i++)
            {
                if (best < 0)
                {
                    best = i;
                    continue;
                }
                float g = grudges[i], bg = grudges[best];
                if (Math.Abs(g - bg) > 0.001f)
                {
                    if (g > bg) best = i;
                    continue;
                }
                float d = Near(tiles, i), bd = Near(tiles, best);
                if (d < bd) best = i;
            }
            return best;
        }

        private static float Near(IList<float> tiles, int i) => tiles == null || i >= tiles.Count || tiles[i] < 0f ? float.MaxValue : tiles[i];

        // ---------------- Where ----------------

        /// <summary>Never within this many tiles of a home (any of the colony's homes): the game holds a camp that close against nobody, but it would sit on the colony's doorstep.</summary>
        public const float NoNearer = 5f;
        /// <summary>From the home it threatens: more than <see cref="NoNearer"/> tiles, at most this many (two or three days' walk).</summary>
        public const float MaxTiles = 10f;
        /// <summary>At least this far from any town of a people not hostile to the colony.</summary>
        public const float FriendlyTownTiles = 3f;

        /// <summary>
        /// A tile can hold the camp: land that can hold it (<paramref name="land"/>: a base can stand there, nothing else is
        /// on it), more than <see cref="NoNearer"/> tiles from every home and at most <see cref="MaxTiles"/> from the one
        /// it threatens, and <see cref="FriendlyTownTiles"/> or more from any friendly town (a negative number: none).
        /// </summary>
        public static bool PlaceFits(float fromHome, float nearestHome, float nearestFriendlyTown, bool land) =>
            land && fromHome > NoNearer && fromHome <= MaxTiles && nearestHome > NoNearer && (nearestFriendlyTown < 0f || nearestFriendlyTown >= FriendlyTownTiles);

        /// <summary>How much a fitting tile is wanted: nearer the home a little more, hills twice as much, forest half as much again.</summary>
        public static float PlaceWeight(float fromHome, bool hills, bool forest)
        {
            float w = 1f + Clamp01((MaxTiles - fromHome) / MaxTiles);
            if (hills) w *= 2f;
            if (forest) w *= 1.5f;
            return w;
        }

        // ---------------- Found ----------------

        /// <summary>The chance a day that someone working or hunting beyond the walls sees it.</summary>
        public const float FindPerDay = 0.3f;
        /// <summary>From this many days after it is set up, anyone out of doors sees its smoke.</summary>
        public const int FindByDays = 3;
        /// <summary>The hours of the day it can be seen (the day's light, 6 to 18).</summary>
        public const int LightFrom = 6, LightTo = 18;

        /// <summary>The chance an hour of daylight that it is seen: <see cref="FindPerDay"/> over the day's light, certain from <see cref="FindByDays"/> on.</summary>
        public static float FindChance(float daysSinceSet)
        {
            if (daysSinceSet >= FindByDays) return 1f;
            int hours = LightTo - LightFrom;
            return (float)(1.0 - Math.Pow(1.0 - FindPerDay, 1.0 / hours));
        }

        public static bool Daylight(int hour) => hour >= LightFrom && hour < LightTo;

        // ---------------- Raids ----------------

        /// <summary>The game's raid-source curve (QuestTuning.PointsToRaidSourceRaidsMTBDaysCurve): a raid in 25 days on average at 400 points, 10 at 1,500, 5 at 5,000.</summary>
        public static readonly float[] CurvePoints = { 400f, 1500f, 5000f };
        public static readonly float[] CurveDays = { 25f, 10f, 5f };
        /// <summary>A raid's points: this share of the home's threat points (QuestTuning.RaidSourceRaidThreatPointsFactor).</summary>
        public const float RaidFactor = 0.6f;
        /// <summary>No two raids from the camp within this many days (QuestTuning.MinDaysBetweenRaidSourceRaids).</summary>
        public const float MinDaysBetween = 1.5f;
        /// <summary>The first raid no sooner than this many days after the camp is set up...</summary>
        public const float FirstRaidDays = 2f;
        /// <summary>...and this many after it was found, so the letter that names the danger always comes first.</summary>
        public const float AfterFoundDays = 1f;

        /// <summary>The mean days between raids for a camp of so many threat points (the curve, straight between its points, flat beyond).</summary>
        public static float MtbDays(float points)
        {
            if (float.IsNaN(points) || points <= CurvePoints[0]) return CurveDays[0];
            for (int i = 1; i < CurvePoints.Length; i++)
                if (points <= CurvePoints[i])
                {
                    float t = (points - CurvePoints[i - 1]) / (CurvePoints[i] - CurvePoints[i - 1]);
                    return CurveDays[i - 1] + (CurveDays[i] - CurveDays[i - 1]) * t;
                }
            return CurveDays[CurveDays.Length - 1];
        }

        /// <summary>The mean days between raids at the rate setting: the curve's at 100%, twice as often at 200%; never at 0 (infinity).</summary>
        public static float RaidMtb(float curveDays, float rate)
        {
            float r = Rate(rate);
            return r <= 0f || curveDays <= 0f ? float.PositiveInfinity : curveDays / r;
        }

        /// <summary>The chance an hour's roll sends a raid (the game's MTB rule over one hour).</summary>
        public static float HourChance(float mtbDays) =>
            float.IsInfinity(mtbDays) || mtbDays <= 0f ? (mtbDays <= 0f ? 1f : 0f) : (float)Math.Min(1.0, (double)HourTicks / (mtbDays * (double)DayTicks));

        /// <summary>
        /// A raid may come now: found, not being paid off, <see cref="FirstRaidDays"/> after it was set up,
        /// <see cref="AfterFoundDays"/> after it was found and <see cref="MinDaysBetween"/> after its last raid.
        /// </summary>
        public static bool MayRaid(CampStage stage, int setAt, int foundAt, int lastRaid, int now)
        {
            if (stage != CampStage.Found || foundAt < 0) return false;
            if (now - setAt < FirstRaidDays * DayTicks || now - foundAt < AfterFoundDays * DayTicks) return false;
            return lastRaid < 0 || now - lastRaid >= MinDaysBetween * DayTicks;
        }

        /// <summary>A raid is beaten off when half its fighters or more fell (dead, down or taken), or they fled in a panic.</summary>
        public static bool Beaten(int size, int down, bool fled) => fled || size > 0 && down * 2 >= size;

        /// <summary>A raid the colony never saw end (a load, its raiders lost track of) is given up on after so many days.</summary>
        public const int RaidWatchDays = 3;

        // ---------------- Giving up ----------------

        /// <summary>Two raids beaten off and they move on.</summary>
        public const int BeatenToGiveUp = 2;
        /// <summary>Or after this many days with the camp standing.</summary>
        public const int GiveUpDays = 60;

        /// <summary>They give up: <see cref="BeatenToGiveUp"/> raids beaten off, or <see cref="GiveUpDays"/> since the camp was set up.</summary>
        public static bool GivesUp(int beaten, int setAt, int now) => beaten >= BeatenToGiveUp || setAt >= 0 && now - setAt >= (long)GiveUpDays * DayTicks;

        // ---------------- Attacked ----------------

        /// <summary>Fewer than this share of its fighters still standing when the colony leaves, and the camp is broken all the same.</summary>
        public const float MinShare = 0.25f;

        /// <summary>The share of its fighters still standing (1 when it never knew how many there were).</summary>
        public static float Standing(int fighters, int standing) => fighters <= 0 ? 1f : Clamp01((float)Math.Max(0, standing) / fighters);

        /// <summary>
        /// The colony left the camp's map: broken when no threat is left there or fewer than <see cref="MinShare"/> of its
        /// fighters still stand; otherwise it is put back.
        /// </summary>
        public static bool Broken(bool threatLeft, int fighters, int standing) => !threatLeft || standing <= 0 || Standing(fighters, standing) < MinShare;

        /// <summary>The camp put back after an attack it survived: its points less those who fell.</summary>
        public static float PointsAfter(float points, int fighters, int standing) => Math.Max(0f, points) * Standing(fighters, standing);

        // ---------------- Their price, and their word ----------------

        /// <summary>Their price is about half their raid's points in silver.</summary>
        public const float PriceShare = 0.5f;
        public const int MinPrice = 100;
        /// <summary>Their terms come no sooner than this many days after the camp is found.</summary>
        public const float TermsAfterDays = 1f;
        /// <summary>Paid, they keep away from the home for a year...</summary>
        public const int PromiseDays = 60;
        /// <summary>...and one in ten break their word (they may come back once the rest after a camp is over).</summary>
        public const float BreakWord = 0.1f;

        /// <summary>Their price in silver for a raid of so many points: half of it, rounded to tens, at least <see cref="MinPrice"/>.</summary>
        public static int Price(float raidPoints)
        {
            if (float.IsNaN(raidPoints) || raidPoints <= 0f) return MinPrice;
            int p = (int)Math.Round(raidPoints * PriceShare / 10f, MidpointRounding.AwayFromZero) * 10;
            return Math.Max(MinPrice, p);
        }

        /// <summary>Their terms may come now: found, not already offered, a day after it was found.</summary>
        public static bool TermsDue(CampStage stage, int foundAt, bool offered, int now) =>
            stage == CampStage.Found && !offered && foundAt >= 0 && now - foundAt >= TermsAfterDays * DayTicks;

        /// <summary>
        /// Their word to keep away holds against a new camp at this home: paid within a year (<paramref name="paidAt"/> -1:
        /// never), unless they mean to break it (then only the rest after a camp, <see cref="RestDays"/>, holds them back).
        /// </summary>
        public static bool WordHolds(int paidAt, bool breaks, int now) => paidAt >= 0 && now - paidAt < (long)PromiseDays * DayTicks && !breaks;

        /// <summary>A year has passed since they gave their word, and it was kept.</summary>
        public static bool WordKept(int paidAt, int now) => paidAt >= 0 && now - paidAt >= (long)PromiseDays * DayTicks;

        // ---------------- An ally burns it ----------------

        /// <summary>An ally of the colony with a town this near the camp (tiles) may burn it...</summary>
        public const float AllyTiles = 30f;
        /// <summary>...at a look every so many days...</summary>
        public const int AllyEveryDays = 5;
        /// <summary>...with this chance.</summary>
        public const float AllyChance = 0.1f;

        public static bool AllyDue(int lastLook, int now) => lastLook < 0 || now - lastLook >= (long)AllyEveryDays * DayTicks;

        // ---------------- Words and history ----------------

        /// <summary>The history's kinds (the family "world:camp"): found, raided, put back after an attack, and each ending.</summary>
        public const string KindFound = "world:camp:found", KindRaid = "world:camp:raid", KindBack = "world:camp:back";

        /// <summary>The history kind of an ending: world:camp:broken, :paid, :peace, :burned, :gaveUp; null for one that isn't told.</summary>
        public static string KindOf(CampEnd end)
        {
            switch (end)
            {
                case CampEnd.Broken: return "world:camp:broken";
                case CampEnd.Paid: return "world:camp:paid";
                case CampEnd.Peace: return "world:camp:peace";
                case CampEnd.Burned: return "world:camp:burned";
                case CampEnd.GaveUp: return "world:camp:gaveUp";
                default: return null;
            }
        }

        /// <summary>What their people remember of an ending (a deed of FactionMemory.Kinds), of the camp's own people unless <paramref name="ally"/>.</summary>
        public static string DeedOf(CampEnd end, out bool ally)
        {
            ally = end == CampEnd.Burned;
            switch (end)
            {
                case CampEnd.Broken: return "campBroken";
                case CampEnd.Paid: return "paidOff";
                case CampEnd.Peace: return "campStruck";
                case CampEnd.Burned: return "burnedCamp";
                case CampEnd.GaveUp: return "campFailed";
                default: return null;
            }
        }

        /// <summary>The deed their people's memory keeps when the camp is found: their wrong against the colony.</summary>
        public const string DeedCamped = "camped";

        /// <summary>The camp's kind by its people's age: a war camp, a brigands' hold, a bandit camp, a raider base.</summary>
        public static string KindKey(WorldAge age) => "CC_RaidCamp_Kind_" + age;

        /// <summary>Who of an ally burns it, by the ally's age: warriors, riders, a patrol, a strike team.</summary>
        public static string AllyKey(WorldAge age) => "CC_RaidCamp_Allies_" + age;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
