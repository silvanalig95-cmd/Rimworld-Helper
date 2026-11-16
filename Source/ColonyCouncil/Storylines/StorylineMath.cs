using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of storylines (docs/quests-proposals.md, "How the new quests work"; Storylines.cs runs them): when one
    /// may start (one at a time, a second only after the first has run a season), which part or ending follows how a part
    /// ended, how long until it comes, and the numbers each storyline plays with. No game types, so Tools/Tests checks them.
    /// </summary>
    public static class StorylineMath
    {
        // ================= The storylines =================

        /// <summary>S1, the factor's goods; S4, the beast coming down the valley; S8, the children from the burned village.</summary>
        public const string Factor = "factor", Beast = "beast", Children = "children";

        /// <summary>Every storyline, by its number: each one's rules are a <see cref="StorylineFlow"/> of its own (Storylines/Flows/).</summary>
        public static string[] Kinds => StorylineFlows.All.Select(f => f.Kind).ToArray();

        public static StorylineFlow Flow(string kind) => StorylineFlows.Of(kind);

        /// <summary>The first part of each storyline.</summary>
        public static string FirstPart(string kind) => Flow(kind)?.FirstPart;

        /// <summary>Every part of a storyline, in the order they can come.</summary>
        public static string[] Parts(string kind) => Flow(kind)?.Parts ?? new string[0];

        /// <summary>Every ending of a storyline (gone: its people are gone or at war; faded: it could not go on).</summary>
        public static string[] Endings(string kind) => Flow(kind)?.Endings ?? new string[0];

        /// <summary>The outcomes a part can end with besides declined, gone and ended.</summary>
        public static string[] Outcomes(string kind, string part) => Flow(kind)?.Outcomes(part) ?? new string[0];

        // ================= Outcomes shared by every part =================

        /// <summary>Turned down, or the offer ran out.</summary>
        public const string Declined = "declined";
        /// <summary>The people who asked are at war with the colony now, or gone.</summary>
        public const string Gone = "gone";
        /// <summary>Something it relied on broke, or the colony left: it simply stops.</summary>
        public const string Ended = "ended";
        /// <summary>The ending of a storyline that could not go on (a part never found a moment to come).</summary>
        public const string Faded = "faded";

        /// <summary>A mission's own words for how it ended, as a storyline's outcome: "cancelled" means the people broke with the colony.</summary>
        public static string OutcomeOf(string missionOutcome, bool declined)
        {
            if (declined && (missionOutcome == null || missionOutcome == "declined")) return Declined;
            if (missionOutcome == "cancelled") return Gone;
            if (string.IsNullOrEmpty(missionOutcome) || missionOutcome == "ended") return Ended;
            return missionOutcome;
        }

        // ================= Pace =================

        public const int DayTicks = 60000;

        /// <summary>A season of the game (a quadrum).</summary>
        public const float SeasonDays = 15f;

        /// <summary>At most two at once: the second only after the first has run a season.</summary>
        public const int MaxRunning = 2;

        /// <summary>A storyline of the same kind does not start again for this long after one ended.</summary>
        public const float KindRestDays = 60f;

        /// <summary>A part that can't come yet (another mission open, a fight on) is tried again; after this long the storyline fades.</summary>
        public const float GiveUpDays = 20f;

        /// <summary>A storyline as the pace sees it.</summary>
        public struct Run
        {
            public string kind;
            public int started;
            public bool running;
            /// <summary>Waiting a season or more for its next part (a beast that comes back, a child grown): not counted against the pace.</summary>
            public bool quiet;
            public int ended;
        }

        /// <summary>
        /// Whether a storyline of <paramref name="kind"/> may start now: none of its kind running, none of its kind ended in
        /// the last <see cref="KindRestDays"/>, and either none running or one that has run a season already. One waiting
        /// quietly for its next part a season off doesn't count.
        /// </summary>
        public static bool CanStart(string kind, int now, IEnumerable<Run> all)
        {
            var list = all?.ToList() ?? new List<Run>();
            if (list.Any(r => r.running && r.kind == kind)) return false;
            if (list.Any(r => !r.running && r.kind == kind && r.ended >= 0 && now - r.ended < KindRestDays * DayTicks)) return false;
            var active = list.Where(r => r.running && !r.quiet).ToList();
            if (active.Count >= MaxRunning) return false;
            if (active.Count == 1 && now - active[0].started < SeasonDays * DayTicks) return false;
            return true;
        }

        /// <summary>Whether a storyline whose people are gone, at war or destroyed must end now (it can't wait on them).</summary>
        public static bool GiverGone(bool exists, bool defeated, bool hostile) => !exists || defeated || hostile;

        // ================= What comes next =================

        /// <summary>What follows a part: the next part, or an ending, and how many days until it comes.</summary>
        public struct Step
        {
            public string part;
            public string ending;
            public float min, max;
            /// <summary>A long wait (a season or more): not counted against the pace while it lasts.</summary>
            public bool quiet;
            /// <summary>A flag the storyline keeps from now on ("ready", "foundout").</summary>
            public string flag;

            public bool IsEnd => ending != null;

            public override string ToString() => (part != null ? "part " + part : "end " + ending) + " in " + min + "-" + max + " days" + (quiet ? " (quiet)" : "") + (flag != null ? " +" + flag : "");
        }

        private static Step End(string ending, float min = 0f, float max = 0f, string flag = null) => new Step { ending = ending, min = min, max = max, flag = flag };

        /// <summary>
        /// The storyline's own rules: after <paramref name="part"/> ended with <paramref name="outcome"/>, what comes next.
        /// <paramref name="times"/>: how many times this part has been played (the beast that comes back); <paramref name="roll"/>
        /// a random number in [0, 1) for the one thing left to chance (whether the factor finds out about a sale).
        /// Every part and outcome leads somewhere: nothing is left without a next step.
        /// </summary>
        public static Step Next(string kind, string part, string outcome, int times, float roll)
        {
            if (outcome == Gone) return End(Gone);
            var flow = Flow(kind);
            return flow != null ? flow.Next(part, outcome, times, roll) : End(Faded);
        }

        /// <summary>Endings that show a letter of their own (the rest were told by the part that led to them, and go to the chronicle only).</summary>
        public static bool EndingHasLetter(string kind, string ending) => Flow(kind)?.EndingHasLetter(ending) ?? false;

        // ================= S1, the factor's goods =================

        /// <summary>How many crates: five to eight.</summary>
        public static int FactorCrates(float roll) => 5 + Clamp((int)Math.Floor(Safe(roll) * 4f), 0, 3);

        /// <summary>Days the crates are kept in part 1 (the clerk counts them at the end), once taken on.</summary>
        public const float KeepMinDays = 8f, KeepMaxDays = 10f;

        /// <summary>Hours in the rain, unroofed, before a crate's goods are spoiled.</summary>
        public const int WetHoursToSpoil = 12;

        /// <summary>What the factor pays for the season's keeping, when she comes for the crates: an eighth of their worth, 40 at least.</summary>
        public static int FactorFee(float worth) => Math.Max(40, Round10(Safe(worth) * 0.125f));

        /// <summary>What the rival offers for the lot: well over its worth, so the choice is a real one.</summary>
        public static int RivalOffer(float worth) => Math.Max(100, Round10(Safe(worth) * 1.3f));

        /// <summary>What the colony owes when crates were lost: their worth.</summary>
        public static int LostDebt(float lostWorth) => Math.Max(20, Round10(Safe(lostWorth)));

        /// <summary>What she asks once she knows the crates were sold: their whole worth.</summary>
        public static int SoldDebt(float worth) => Math.Max(50, Round10(Safe(worth)));

        /// <summary>The chance she hears who bought her crates.</summary>
        public const float FoundOutChance = 0.7f;

        /// <summary>Days to make good what was lost, and to pay the debt.</summary>
        public const float MakeGoodDays = 10f, DebtDays = 8f;

        /// <summary>Her share of the trade each season, for a year: a little more when the colony told her of the rival.</summary>
        public static int SeasonShare(float worth, bool told) => Math.Max(30, Round10(Safe(worth) * (told ? 0.1f : 0.07f)));

        /// <summary>Seasons she comes by with a share: a year.</summary>
        public const int Shares = 4;

        /// <summary>Days with no traders from her people after the colony didn't pay: a year.</summary>
        public const float NoTradeDays = 60f;

        /// <summary>The fee less what is missing when she comes (crates lost after the count), never below nothing.</summary>
        public static int FeeLess(int fee, float missingWorth) => Math.Max(0, Round10(fee - Safe(missingWorth)));

        // ================= S4, the beast coming down the valley =================

        /// <summary>Days the colony has to get ready, once taken on.</summary>
        public const float ReadyMinDays = 6f, ReadyMaxDays = 8f;

        /// <summary>Traps to build: three to six by the colony's size.</summary>
        public static int TrapsGoal(int colonists) => Clamp(2 + colonists / 3, 3, 6);

        /// <summary>Lengths of wall or fence to build: 24 to 60 by the colony's size.</summary>
        public static int WallGoal(int colonists) => Clamp(16 + colonists * 4, 24, 60);

        /// <summary>Animals kept in: three in four of the colony's animals in a pen or a room by the date.</summary>
        public const float KeptInShare = 0.75f;

        public static int KeptInNeeded(int animals) => animals <= 0 ? 0 : (int)Math.Ceiling(animals * KeptInShare);

        /// <summary>"ready" (defences and the animals in), "partly" (one of the two), "unready" (neither).</summary>
        public static string Readiness(int traps, int trapsGoal, int walls, int wallGoal, int animalsIn, int animals)
        {
            bool defended = traps >= trapsGoal || walls >= wallGoal;
            bool kept = animalsIn >= KeptInNeeded(animals);
            return defended && kept ? "ready" : defended || kept ? "partly" : "unready";
        }

        /// <summary>Days the colony has to go and look at the signs once they are found at the edge of the land.</summary>
        public const float SignsDays = 2f;

        /// <summary>How close someone of the colony must come to the signs to have seen them.</summary>
        public const float SeenWithin = 6f;

        /// <summary>The hunters come to help when the colony stands this well with them (and they are never sent when it doesn't).</summary>
        public const int HelpGoodwill = 25;

        /// <summary>How many hunters come: two, three when the colony got ready, four when it is a friend.</summary>
        public static int Hunters(int goodwill, bool ready) => goodwill < HelpGoodwill ? 0 : Clamp(2 + (ready ? 1 : 0) + (goodwill >= 75 ? 1 : 0), 2, 4);

        /// <summary>The beast's strength, as a share of the threat the storyteller would send now (it is a fight, scaled to the colony).</summary>
        public const float BeastPointsShare = 0.85f;

        /// <summary>
        /// Which beast of the region's legends: the strongest whose combat power fits within the points, else the weakest.
        /// Returns its index in <paramref name="powers"/>, or -1 for none.
        /// </summary>
        public static int BeastPick(IList<float> powers, float points)
        {
            if (powers == null || powers.Count == 0) return -1;
            float cap = Safe(points) * BeastPointsShare;
            int best = -1, weakest = 0;
            for (int i = 0; i < powers.Count; i++)
            {
                if (powers[i] < powers[weakest]) weakest = i;
                if (powers[i] <= cap && (best < 0 || powers[i] > powers[best])) best = i;
            }
            return best >= 0 ? best : weakest;
        }

        /// <summary>The beast turns and flees below this much of its health.</summary>
        public const float FleeBelow = 0.35f;

        /// <summary>A beast still here this long after it came goes off on its own (and may come back).</summary>
        public const float LeaveAfterDays = 1.5f;

        /// <summary>It comes back at most twice.</summary>
        public const int MaxReturns = 2;

        // ================= S8, the children from the burned village =================

        /// <summary>Two to four children, by the colony's size.</summary>
        public static int ChildCount(int colonists, float roll) => Clamp(2 + (int)Math.Floor(Safe(roll) * 2f) + (colonists >= 8 ? 1 : 0), 2, 4);

        /// <summary>Days they stay once they come.</summary>
        public const float StayMinDays = 3f, StayMaxDays = 4f;

        /// <summary>The eldest's age when they come, so they are grown (13, the game's adult) when they come back.</summary>
        public const float EldestMinAge = 12.4f, EldestMaxAge = 12.9f;

        public const float GrownAge = 13f;

        /// <summary>Meals each child is given when the colony sends them on.</summary>
        public const int MealsEach = 3;

        /// <summary>Days until the eldest comes back grown: two to four seasons.</summary>
        public const float ReturnMinDays = 30f, ReturnMaxDays = 60f;

        /// <summary>How the eldest comes back: placed with neighbours, more often to visit or to join; sent on, more often to trade.</summary>
        public static string ReturnForm(string branch, float roll)
        {
            roll = Safe(roll);
            if (branch == "place") return roll < 0.5f ? "visitor" : roll < 0.8f ? "joiner" : "trader";
            return roll < 0.4f ? "trader" : roll < 0.8f ? "visitor" : "joiner";
        }

        // ================= Small helpers =================

        public static int Round10(float v) => (int)Math.Round(v / 10f, MidpointRounding.AwayFromZero) * 10;

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        private static float Safe(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0f : Math.Max(0f, v);
    }
}
