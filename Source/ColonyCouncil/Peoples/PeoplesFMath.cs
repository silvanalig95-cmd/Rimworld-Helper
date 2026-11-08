using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of release F's four peoples (docs/design/peoples.md, "Release F"; docs/design/peoples-third.md, step
    /// 2), with no game types so Tools/Tests can check them: the Signal Line's relay mast (where it stands, what a storm
    /// does to it, when it goes quiet, how much sooner a letter arrives through it, how early it warns of a raid), its
    /// wire-trader's prices; the Deepwell Union's drill for a share (how long, what comes up, whose it is, the accident's
    /// chance), its goods and its strike; the Last Republic's census, its papers (the better terms and the duty), its
    /// conscript's season and its radio; the Hollow Fleet's shuttle, its prices for salvage and caskets, and the sleeper
    /// woken too late.
    /// </summary>
    public static class PeoplesFMath
    {
        public const int DayTicks = 60000, HourTicks = 2500;

        private static int Round5(float v) => (int)Math.Round(v / 5f, MidpointRounding.AwayFromZero) * 5;

        private static int Round10(float v) => (int)Math.Round(v / 10f, MidpointRounding.AwayFromZero) * 10;

        private static float Safe(float v, float or) => float.IsNaN(v) || float.IsInfinity(v) ? or : v;

        // ================= The Signal Line: the relay mast =================

        /// <summary>The mast stands within these cells of the colony's middle, on the highest ground there is: far enough to be clear of the roofs, near enough to keep.</summary>
        public const int MastMinCells = 8, MastMaxCells = 40;

        /// <summary>
        /// How good a cell is for the mast: the hillside first (natural rock within a few cells of it, the most rock the
        /// better, to four), rough ground next, and the nearer the colony the better. Higher is better; a cell no mast can
        /// stand on is never asked.
        /// </summary>
        public static float SiteScore(int rockNear, bool rough, float cells)
        {
            float d = Safe(cells, MastMaxCells);
            return Math.Min(4, Math.Max(0, rockNear)) * 3f + (rough ? 2f : 0f) - Math.Max(0f, d) / 20f;
        }

        /// <summary>The hit points a mast loses to a thunderstorm's wind and wet each quarter of an hour of it (the game's rare tick of 250).</summary>
        public const int StormWear = 1;

        /// <summary>Under this share of its hit points the mast is quiet: no faster letters, no warning.</summary>
        public const float QuietBelow = 0.5f;

        public static bool Quiet(int hitPoints, int maxHitPoints) => maxHitPoints <= 0 || hitPoints < maxHitPoints * QuietBelow;

        /// <summary>
        /// A letter's leg on the road (or its answer's) through the mast: the line relays it, so half the days, never under
        /// the half day it takes to hand it on at the start (LetterMath.LegStartDays).
        /// </summary>
        public static float Relayed(float days)
        {
            float d = Safe(days, 0.5f);
            return Math.Max(0.5f, d * 0.5f);
        }

        /// <summary>A raid the storyteller sends to a home with a singing mast comes this many hours after the line's warning.</summary>
        public const int WarnMinHours = 2, WarnMaxHours = 4;

        /// <summary>The game may try to bring a raid held for the warning again for this long, if it can't come at its hour.</summary>
        public const int WarnRetryHours = 6;

        public static int WarnHours(float roll)
        {
            if (float.IsNaN(roll) || roll < 0f) roll = 0f;
            return Math.Min(WarnMaxHours, WarnMinHours + (int)(roll * (WarnMaxHours - WarnMinHours + 1)));
        }

        /// <summary>
        /// The mast warns of a raid: a threat the storyteller sends (big or small) that is a raid, not a quest's, not one
        /// already coming back out of the game's queue.
        /// </summary>
        public static bool Warns(bool raid, bool threat, bool quest, bool queued) => raid && threat && !quest && !queued;

        // ================= The Signal Line: the wire-trader =================

        /// <summary>The wire-trader comes for the colony's components when it has this many, and buys this many at most.</summary>
        public const int WireMin = 4, WireMax = 20;

        /// <summary>What the Line pays for the components: a fifth over their worth, to the nearest five.</summary>
        public static int WirePrice(float worth) => Round5(Math.Max(0f, Safe(worth, 0f)) * 1.2f);

        /// <summary>The line's news of a caravan on the road (bidden to the colony): fifty silver, the price the Line asks of everyone.</summary>
        public const int NewsFee = 50;

        // ================= The Deepwell Union: the drill for a share =================

        /// <summary>The crew drills this many hours at the deep ground, with a bar for it.</summary>
        public const int DrillHours = 6;

        /// <summary>The union's share of what comes up: two in five, the rest left at the drill for the colony.</summary>
        public const float UnionShare = 0.4f;

        /// <summary>
        /// What comes up of a deep seam: as much as lies there, up to about 700 silver's worth (a richer colony's seam is worth
        /// a longer shift, to 1,400), never under one.
        /// </summary>
        public static int Yield(int deep, float unitWorth, float wealthFactor)
        {
            if (deep <= 0) return 0;
            float each = Math.Max(0.1f, Safe(unitWorth, 1f));
            float cap = 700f * Math.Max(1f, Math.Min(2f, Safe(wealthFactor, 1f)));
            return Math.Max(1, Math.Min(deep, (int)(cap / each)));
        }

        /// <summary>The colony's share of what came up: the rest after the union's two in five (rounded to the union).</summary>
        public static int ColonyShare(int total) => total <= 0 ? 0 : total - (int)Math.Round(total * UnionShare, MidpointRounding.AwayFromZero);

        /// <summary>
        /// One shift in twenty at the drill with the colony's hand on it, the hand is hurt (a bruise or a crushed foot, 3 to 8,
        /// through the harm setting and the shared budget: HarmKinds.UnionDrill): about the game's own chance of a mishap in a
        /// day of mining beside a working drill.
        /// </summary>
        public const float AccidentChance = 0.05f;

        public const float AccidentLow = 3f, AccidentHigh = 8f;

        // ================= The Deepwell Union: its goods =================

        /// <summary>A crate of steel and components, or of blasting goods: a tenth over what it is worth, to the nearest ten.</summary>
        public static int GoodsPrice(float worth) => Round10(Math.Max(10f, Safe(worth, 10f) * 1.1f));

        /// <summary>Steel and components in a crate, by the colony's wealth factor (0.6 to 2).</summary>
        public static int CrateSteel(float wealthFactor) => (int)Math.Round(60f * Math.Max(0.6f, Math.Min(2f, Safe(wealthFactor, 1f))) / 10f) * 10;

        public static int CrateComponents(float wealthFactor) => Math.Max(2, (int)Math.Round(4f * Math.Max(0.6f, Math.Min(2f, Safe(wealthFactor, 1f)))));

        // ================= The Deepwell Union: the strike =================

        /// <summary>Each Union people is looked at once a season (on its own day) for a strike: one look in seven calls one, so one in two years or so.</summary>
        public const int StrikeLookDays = 15;

        public const float StrikeChance = 1f / 7f;

        /// <summary>A Union people's day of the season for its look (by its id).</summary>
        public static bool StrikeDay(int day, int id) => day >= 0 && ((day + Math.Abs(id) * 7) % StrikeLookDays) == 0;

        /// <summary>Unsettled, a strike ends by the union's own vote after this long.</summary>
        public const int StrikeDays = 30;

        /// <summary>The shift-captain comes to the gate for help once the strike has lasted this long.</summary>
        public const int StrikeAskAfterDays = 2;

        /// <summary>A strike runs: begun, not settled, within its days.</summary>
        public static bool Striking(int since, int now, bool settled) => since >= 0 && !settled && now >= since && now - since < StrikeDays * DayTicks;

        /// <summary>The picket line's food the colony carries out: twenty for each of the colony's people the union feeds, 60 to 160.</summary>
        public static int PicketFood(int pickets) => Math.Max(60, Math.Min(160, 20 * Math.Max(0, pickets)));

        /// <summary>Settled with the colony's help, the union thinks this much the better of it.</summary>
        public const int SettledGoodwill = 8;

        // ================= The Last Republic: the census =================

        /// <summary>The census-taker writes each of the colony's people down for this many ticks where they sleep (or stand).</summary>
        public const int CensusTicks = 420;

        /// <summary>Counted, the Republic thinks the better of the colony; turned away, a little the worse.</summary>
        public const int CountedGoodwill = 3, UncountedGoodwill = -2;

        // ================= The Last Republic: citizenship papers =================

        /// <summary>Papers are good for a year; the Republic offers them again after.</summary>
        public const int PapersDays = 60;

        /// <summary>A citizen's terms with the Republic's traders: the game's own price improvement, this much more (both ways).</summary>
        public const float PapersTerms = 0.06f;

        /// <summary>Papers taken: the Republic thinks the better of the colony. The duty refused: the papers are struck, and it thinks less.</summary>
        public const int PapersGoodwill = 5, StruckGoodwill = -6;

        /// <summary>The papers hold: taken within the year, not struck.</summary>
        public static bool Holds(int since, int now, bool struck) => since >= 0 && !struck && now >= since && now - since < PapersDays * DayTicks;

        // ================= The Last Republic: a conscript for a season =================

        /// <summary>A conscript serves a season at the Republic's town, kept there (the roads both ways are the game's own).</summary>
        public const int ServiceDays = 15;

        /// <summary>
        /// The conscript's pay for the season, by the age the Republic lives in (its rifles and its rations are of its
        /// age): 150 silver in the tribal age to 420 in the spacer age.
        /// </summary>
        public static int ServicePay(int age) => 150 + 90 * Math.Max(0, Math.Min(3, age));

        /// <summary>What a season in the Civic Guard teaches, in shooting and in melee.</summary>
        public const float ServiceXp = 2400f;

        /// <summary>Served the season: the Republic thinks the better of the colony. Refused: less.</summary>
        public const int ServedGoodwill = 8, RefusedGoodwill = -5;

        /// <summary>The fee the Republic takes instead of a conscript: 120 to 480 silver by the colony's wealth, to the nearest ten.</summary>
        public static int Exemption(float wealth)
        {
            float w = Math.Max(0f, Safe(wealth, 0f));
            return Round10(Math.Max(120f, Math.Min(480f, 120f + w / 600f)));
        }

        // ================= The Last Republic: its radio =================

        /// <summary>Each Republic broadcasts of the colony once a season, on its own day.</summary>
        public const int RadioDays = 15;

        public static bool RadioDay(int day, int id) => day >= 0 && ((day + Math.Abs(id) * 11 + 5) % RadioDays) == 0;

        /// <summary>What its radio says of the colony: praise at this goodwill or with papers, a denunciation under none or at war.</summary>
        public const int PraiseAt = 40, DenounceBelow = 0;

        /// <summary>"praise", "denounce" or null (nothing worth saying this season).</summary>
        public static string Broadcast(int goodwill, bool papers, bool hostile)
        {
            if (hostile || goodwill < DenounceBelow) return "denounce";
            if (papers || goodwill >= PraiseAt) return "praise";
            return null;
        }

        // ================= The Hollow Fleet: the shuttle =================

        /// <summary>The quartermaster comes down with one to three of the ship's guard, by the colony's wealth factor (0.6 to 2).</summary>
        public static int ShuttleGuards(float wealthFactor)
        {
            float f = Math.Max(0f, Math.Min(1f, (Safe(wealthFactor, 1f) - 0.6f) / 1.4f));
            return 1 + (int)Math.Round(f * 2f);
        }

        /// <summary>The shuttle is for a colony of the industrial age or later, which knows what comes down out of the sky (as the enclave's lander).</summary>
        public static bool ShuttleAge(int colonyAge) => colonyAge >= 2;

        /// <summary>A cryptosleep casket from the hull, sold for a tenth over its worth, to the nearest ten.</summary>
        public static int CasketPrice(float worth) => Round10(Math.Max(100f, Safe(worth, 100f) * 1.1f));

        // ================= The Hollow Fleet: salvage =================

        /// <summary>What a ship's chunk on the colony's ground is worth to the Fleet (the plasteel, steel and parts in it).</summary>
        public const float ChunkWorth = 220f;

        /// <summary>The Fleet pays three tenths over the salvage's worth, to the nearest five: the ship needs it more than anyone.</summary>
        public static int SalvagePrice(float worth) => Round5(Math.Max(0f, Safe(worth, 0f)) * 1.3f);

        /// <summary>The deckhands cut a chunk apart this many hours where it lies before carrying it off, three chunks at most.</summary>
        public const int CutHours = 2, CutMax = 3;

        /// <summary>The salvage buyer comes for so much at least (a chunk, a core, or this many advanced components' worth).</summary>
        public const float SalvageMinWorth = 150f;

        // ================= The Hollow Fleet: the sleeper woken too late =================

        /// <summary>Years a late-woken sleeper spent in the hull's caskets: since before the ship came down, two hundred years ago or so.</summary>
        public const int SleptMinYears = 190, SleptMaxYears = 230;

        public static int SleptYears(float roll)
        {
            if (float.IsNaN(roll) || roll < 0f) roll = 0f;
            return Math.Min(SleptMaxYears, SleptMinYears + (int)(roll * (SleptMaxYears - SleptMinYears + 1)));
        }

        /// <summary>The sleeper asks shelter towards evening: from this hour to this one.</summary>
        public const int SleeperFrom = 15, SleeperTo = 21;

        public static bool SleeperHour(int hour) => hour >= SleeperFrom && hour <= SleeperTo;
    }
}
