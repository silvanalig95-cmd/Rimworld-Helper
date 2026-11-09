using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>Whether a firing incident counts in the shared illness budget, and if not why (<see cref="IllnessBudgetMath.Which"/>).</summary>
    public enum BudgetFiring
    {
        /// <summary>The storyteller's own human disease, aimed at a home: it counts.</summary>
        Counts,
        /// <summary>One of Rimfolk's own catching ailments (they are fired on Rimfolk's clock and owe their skip as they start).</summary>
        Rimfolk,
        /// <summary>Not in the game's human disease category: an animal disease, a mod's sickness filed elsewhere, anything else.</summary>
        NotHumanDisease,
        /// <summary>Not sent by the storyteller: a quest's outbreak, another mod's or Rimfolk's script, the trait that brings random illness.</summary>
        NotStoryteller,
        /// <summary>Forced (dev mode, a script that insists).</summary>
        Forced,
        /// <summary>Part of a quest.</summary>
        Quest,
        /// <summary>Aimed at a caravan, a camp or a site, not a home.</summary>
        NotHome
    }

    /// <summary>Whether a home has room for a new serious illness of Rimfolk's within the budget, and if not why (<see cref="IllnessBudgetMath.Room"/>).</summary>
    public enum BudgetRoom
    {
        Open,
        /// <summary>Rimfolk's last case here still owes its skip: the game's next disease hasn't come to pay it.</summary>
        Owed,
        /// <summary>The game's own disease came within the pause (<see cref="IllnessBudgetMath.PauseDays"/>): none of Rimfolk's start here.</summary>
        Waiting,
        /// <summary>After the pause: Rimfolk's chance here is lowered, and comes back over the following weeks (<see cref="IllnessBudgetMath.Eased"/>).</summary>
        Easing
    }

    /// <summary>
    /// One home's part of the shared illness budget, without game types: Rimfolk's serious cases that still owe a skip, and
    /// when the game's own disease last came and was last held back. The game's side saves it (Ailments/IllnessBudget.cs,
    /// <c>IllnessLedger</c>); Tools/Tests and Tools/Balance play it as it is.
    /// </summary>
    public class IllnessBudgetBook
    {
        /// <summary>Ticks of Rimfolk's serious cases here that still owe a skip, the oldest first.</summary>
        public List<int> owed = new List<int>();
        /// <summary>Beside <see cref="owed"/>: what each was ("illness defName|their name"), for the records' line.</summary>
        public List<string> owedFor = new List<string>();
        /// <summary>When the game's own counted disease last came here.</summary>
        public int lastGame = IllnessBudgetMath.Never;
        /// <summary>When the budget last held one of the game's diseases back here, and the case it paid for (as <see cref="owedFor"/>).</summary>
        public int lastSkip = IllnessBudgetMath.Never;
        public string lastPaid;
        /// <summary>When a case of Rimfolk's was last put off here and said so (one line a gap at most).</summary>
        public int lastPutOff = IllnessBudgetMath.Never;
        /// <summary>
        /// The game's diseases held back, those that came though a debt was owed (the debt let go), and Rimfolk's cases put
        /// off here, all told (the dev tools' log).
        /// </summary>
        public int held, cameAnyway, putOff;
        /// <summary>The council's last record line here and when it was written (the advisor's sickness line shows it for a year).</summary>
        public string note;
        public int noteTick = IllnessBudgetMath.Never;
    }

    /// <summary>
    /// The shared illness budget (the player's choice, 8 November 2026: "just the shared illness budget please"; made soft on
    /// 9 November 2026: "people should be able to suffer for different things, but it should just not be common"). Rimfolk's
    /// serious illnesses mostly take a turn of the game's own diseases instead of adding one, so serious illness in all comes a
    /// little more often than in plain RimWorld, and a good part of the cases are Rimfolk's kinds.
    /// <list type="bullet">
    /// <item>The budget is the game's own rate: the storyteller's human diseases on a home, by the land's disease days and
    /// the storyteller's interval (<see cref="GameMeanDays"/>).</item>
    /// <item>Each serious case of Rimfolk's that starts owes a skip: the game's next counted disease aimed at that home is
    /// held back three times in four (<see cref="Hold"/>, <see cref="HoldChance"/>); otherwise it comes and the debt is let
    /// go (<see cref="GameCame"/>). A debt is kept until then or for three of the game's usual gaps between its diseases
    /// there, two years at least and five at most (<see cref="KeepDays"/>).</item>
    /// <item>One debt at a time (<see cref="MaxOwed"/>): Rimfolk starts no new case on a home that still owes one.</item>
    /// <item>The game's diseases give Rimfolk's a rest: once the game's own has come, none of Rimfolk's start there for a
    /// short pause (<see cref="PauseDays"/>), then their chance comes back from a quarter to whole over four weeks
    /// (<see cref="Eased"/>).</item>
    /// <item>With the budget on, Rimfolk's serious illnesses come more readily (<see cref="Readiness"/>), since most of them
    /// take a turn rather than add one; and someone already fighting a serious illness, the game's or Rimfolk's, is far less
    /// likely to come down with another of Rimfolk's (<see cref="OverlapFactor"/>).</item>
    /// </list>
    /// So serious illness in all stays within a fifth or so of the game's own rate where the colony is kept well or fairly,
    /// and never more than a quarter above it however many of Rimfolk's would come (<see cref="MostInAll"/>). Crowding and
    /// dirt make Rimfolk's catching kinds likelier, which mostly shifts the mix. Pure, so the tests and the balance model
    /// read the same rules.
    /// </summary>
    public static class IllnessBudgetMath
    {
        public const int DayTicks = 60000;
        public const int Never = int.MinValue;
        public const float YearDays = 60f;

        /// <summary>The serious illnesses' own gap (AilmentTracker.SeriousGapDays): a case put off is said at most once a gap a home.</summary>
        public const float GapDays = 30f;

        /// <summary>The game's next disease on a home that owes a skip is held back this often; otherwise it comes and the debt is let go.</summary>
        public const float HoldChance = 0.75f;

        /// <summary>A home owes at most this many skips before Rimfolk starts another case there.</summary>
        public const int MaxOwed = 1;

        /// <summary>After the game's own disease, no new serious illness of Rimfolk's there for this long...</summary>
        public const float PauseDays = 8f;

        /// <summary>... then their chance there starts at this much of whole...</summary>
        public const float EaseFrom = 0.25f;

        /// <summary>... and comes back to whole over this many days.</summary>
        public const float EaseDays = 28f;

        /// <summary>
        /// With the budget on, Rimfolk's serious illnesses' own chances are times this at the storyteller's usual disease
        /// interval (<see cref="ReadinessAt"/>): most of them take a turn of the game's rather than add one.
        /// </summary>
        public const float Readiness = 2.75f;

        /// <summary>Someone already fighting a serious illness (the game's or Rimfolk's) comes down with another of Rimfolk's at this much of the usual chance.</summary>
        public const float OverlapFactor = 0.25f;

        /// <summary>A debt is kept for this many of the game's usual gaps between its diseases...</summary>
        public const float KeepGaps = 3f;

        /// <summary>... but at least two years and at most five.</summary>
        public const float MinKeepDays = 120f, MaxKeepDays = 300f;

        /// <summary>The game's mean days between its human diseases where it has none to send (diseases off, or no storyteller part for them).</summary>
        public const float NoGameDiseases = float.PositiveInfinity;

        // ---------------- What the balance model must show (Tools/Balance --illness; IllnessBudgetTests reads the tables) ----------------

        /// <summary>Rimfolk's share of the serious cases in a colony kept fairly (the average play style, six and twelve colonists).</summary>
        public const float TargetShareMin = 0.35f, TargetShareMax = 0.45f;

        /// <summary>Serious illness in all at most this much above the game's own rate where the colony is kept well or fairly.</summary>
        public const float TargetAbove = 0.20f;

        /// <summary>
        /// A colonist carrying two serious illnesses at once: at most this often a colony-year in any colony, most of it two of
        /// the game's own (plain RimWorld has those too)...
        /// </summary>
        public const float TargetTwoAtOnce = 0.2f;

        /// <summary>... and with one of the two Rimfolk's, at most this often.</summary>
        public const float TargetTwoAtOnceRimfolk = 0.05f;

        // ---------------- The game's own rate ----------------

        /// <summary>
        /// The game's mean days between its human diseases on a home: the land's disease days times the storyteller's interval
        /// (as StorytellerComp_Disease rolls them); none with the storyteller's diseases off or no part of it sending them.
        /// </summary>
        public static float GameMeanDays(float biomeMtbDays, float intervalFactor, bool storytellerSends, float cutoff)
        {
            if (!storytellerSends || intervalFactor >= cutoff) return NoGameDiseases;
            float biome = biomeMtbDays > 0f ? biomeMtbDays : 60f;
            float interval = intervalFactor > 0f ? intervalFactor : 1f;
            return biome * interval;
        }

        /// <summary>The game's own diseases a year at a mean of <paramref name="meanDays"/> between them.</summary>
        public static float GamePerYear(float meanDays) => meanDays > 0f && !float.IsInfinity(meanDays) && !float.IsNaN(meanDays) ? YearDays / meanDays : 0f;

        /// <summary>How long a debt is kept: three of the game's gaps, two years at least and five at most.</summary>
        public static float KeepDays(float gameMeanDays)
        {
            if (float.IsNaN(gameMeanDays) || float.IsInfinity(gameMeanDays) || gameMeanDays <= 0f) return MinKeepDays;
            float d = KeepGaps * gameMeanDays;
            return d < MinKeepDays ? MinKeepDays : d > MaxKeepDays ? MaxKeepDays : d;
        }

        /// <summary>
        /// The most serious illness there can be in all, as a multiple of the game's own rate, however many of Rimfolk's would
        /// come: with one debt at a time each of Rimfolk's cases waits for one of the game's to come along, and of those a
        /// quarter still come (two less the hold's chance: a quarter more). Debts that lapse unpaid add a little where the
        /// game's own are rare.
        /// </summary>
        public static float MostInAll => 2f - HoldChance;

        // ---------------- Which incidents count ----------------

        /// <summary>
        /// Whether an incident firing counts: the storyteller's own human disease aimed at a home, not forced, not a quest's,
        /// not one of Rimfolk's. Everything else is left alone: quests' and scripted outbreaks, mods' sicknesses filed under
        /// another category, animal diseases, and what the game rolls outside incidents (food poisoning, wound infections,
        /// the sickly trait's illness).
        /// </summary>
        public static BudgetFiring Which(bool fromStoryteller, bool humanDisease, bool rimfolk, bool forced, bool quest, bool home)
        {
            if (rimfolk) return BudgetFiring.Rimfolk;
            if (!humanDisease) return BudgetFiring.NotHumanDisease;
            if (!fromStoryteller) return BudgetFiring.NotStoryteller;
            if (forced) return BudgetFiring.Forced;
            if (quest) return BudgetFiring.Quest;
            if (!home) return BudgetFiring.NotHome;
            return BudgetFiring.Counts;
        }

        // ---------------- The debts ----------------

        /// <summary>A debt that has lapsed: kept its time unpaid, or from a tick ahead of now (a save played on elsewhere).</summary>
        public static bool Lapsed(int owedTick, int now, float keepDays) =>
            owedTick == Never || owedTick > now || now - (long)owedTick >= (long)(keepDays * DayTicks);

        /// <summary>A tick within the last <paramref name="days"/> (never, or one ahead of now, isn't).</summary>
        public static bool Within(int tick, int now, float days) => tick != Never && tick <= now && now - (long)tick < (long)(days * DayTicks);

        /// <summary>Lets go of the debts that have lapsed; how many went.</summary>
        public static int Prune(IllnessBudgetBook b, int now, float keepDays)
        {
            if (b == null) return 0;
            b.owed ??= new List<int>();
            b.owedFor ??= new List<string>();
            int gone = 0;
            for (int i = b.owed.Count - 1; i >= 0; i--)
            {
                if (!Lapsed(b.owed[i], now, keepDays)) continue;
                b.owed.RemoveAt(i);
                if (i < b.owedFor.Count) b.owedFor.RemoveAt(i);
                gone++;
            }
            // The two lists stay side by side (an older or hand-edited save).
            while (b.owedFor.Count > b.owed.Count) b.owedFor.RemoveAt(b.owedFor.Count - 1);
            while (b.owedFor.Count < b.owed.Count) b.owedFor.Add("");
            return gone;
        }

        /// <summary>The skips this home still owes now.</summary>
        public static int Owing(IllnessBudgetBook b, int now, float keepDays)
        {
            if (b?.owed == null) return 0;
            int n = 0;
            for (int i = 0; i < b.owed.Count; i++)
                if (!Lapsed(b.owed[i], now, keepDays)) n++;
            return n;
        }

        /// <summary>A serious case of Rimfolk's started here: it owes one skip.</summary>
        public static void Owe(IllnessBudgetBook b, int now, string what)
        {
            if (b == null) return;
            b.owed ??= new List<int>();
            b.owedFor ??= new List<string>();
            while (b.owedFor.Count < b.owed.Count) b.owedFor.Add("");
            b.owed.Add(now);
            b.owedFor.Add(what ?? "");
        }

        /// <summary>
        /// The game's own counted disease is about to come here. With a debt owed it's held back when the roll (0 to 1) falls
        /// under <see cref="HoldChance"/>, paying the oldest debt (into <paramref name="paidFor"/>, what it was for). False: it
        /// comes, as it would have; if a debt was owed it's let go as the disease lands (<see cref="GameCame"/>).
        /// </summary>
        public static bool Hold(IllnessBudgetBook b, int now, float keepDays, float roll, out string paidFor)
        {
            paidFor = null;
            if (b == null) return false;
            Prune(b, now, keepDays);
            if (b.owed.Count == 0 || !(roll < HoldChance)) return false;
            paidFor = b.owedFor.Count > 0 ? b.owedFor[0] : "";
            b.owed.RemoveAt(0);
            if (b.owedFor.Count > 0) b.owedFor.RemoveAt(0);
            b.lastSkip = now;
            b.lastPaid = paidFor;
            b.held++;
            return true;
        }

        /// <summary>
        /// The game's own counted disease came here: the oldest debt it didn't hold back for is let go (the hold's other
        /// quarter: the case still took a turn, just not this one), and Rimfolk's serious illnesses rest a while there.
        /// </summary>
        public static void GameCame(IllnessBudgetBook b, int now, float keepDays)
        {
            if (b == null) return;
            b.lastGame = now;
            Prune(b, now, keepDays);
            if (b.owed.Count == 0) return;
            b.owed.RemoveAt(0);
            if (b.owedFor.Count > 0) b.owedFor.RemoveAt(0);
            b.cameAnyway++;
        }

        // ---------------- Room for Rimfolk's ----------------

        /// <summary>
        /// How far Rimfolk's chance of a new serious illness on a home has come back this long after the game's own disease
        /// came there: nothing in the pause, then from <see cref="EaseFrom"/> up to whole over <see cref="EaseDays"/>. Whole
        /// for a time ahead of now or none (never came).
        /// </summary>
        public static float Eased(float daysSinceGame)
        {
            if (float.IsNaN(daysSinceGame) || daysSinceGame < 0f || float.IsInfinity(daysSinceGame)) return 1f;
            if (daysSinceGame < PauseDays) return 0f;
            float into = daysSinceGame - PauseDays;
            if (into >= EaseDays) return 1f;
            return EaseFrom + (1f - EaseFrom) * into / EaseDays;
        }

        /// <summary>
        /// Whether this home has room for a new serious illness of Rimfolk's now, and if not why: no more skips owed than
        /// <see cref="MaxOwed"/> allows, the pause after the game's own past, and the weeks after it easing. The colony's own
        /// room (one serious illness at a time, thirty days from the last) is asked as well, as it always was.
        /// </summary>
        public static BudgetRoom Room(IllnessBudgetBook b, int now, float keepDays)
        {
            if (b == null) return BudgetRoom.Open;
            if (Owing(b, now, keepDays) >= MaxOwed) return BudgetRoom.Owed;
            if (Within(b.lastGame, now, PauseDays)) return BudgetRoom.Waiting;
            if (Within(b.lastGame, now, PauseDays + EaseDays)) return BudgetRoom.Easing;
            return BudgetRoom.Open;
        }

        /// <summary>The chance (0 to 1) a serious case of Rimfolk's whose own chance has come up may start on this home now.</summary>
        public static float Chance(IllnessBudgetBook b, int now, float keepDays)
        {
            switch (Room(b, now, keepDays))
            {
                case BudgetRoom.Open: return 1f;
                case BudgetRoom.Easing: return Eased((now - (long)b.lastGame) / (float)DayTicks);
                default: return 0f;
            }
        }

        /// <summary>Whether a serious case of Rimfolk's whose own chance has come up starts here now: its room's chance against a roll (0 to 1).</summary>
        public static bool Allows(IllnessBudgetBook b, int now, float keepDays, float roll) => roll < Chance(b, now, keepDays);

        /// <summary>A new serious illness of Rimfolk's for someone already fighting one (the game's or Rimfolk's): its chance, times this.</summary>
        public static float Overlap(bool seriouslyIll) => seriouslyIll ? OverlapFactor : 1f;

        /// <summary>
        /// How much more readily Rimfolk's serious illnesses come with the budget on, at the storyteller's disease interval:
        /// <see cref="Readiness"/> at the usual one, less as the storyteller spaces its diseases out and so leaves fewer turns
        /// to take, more as it brings them closer. By one over the interval (a quarter to double, as the catching clock
        /// leans), and half that again, since the budget's pause and the colony's own thirty days between Rimfolk's serious
        /// illnesses don't stretch with it: at Adventure story's 1.5 one and a half times (1.5 to the power 1.5 is 1.84).
        /// </summary>
        public static float ReadinessAt(float intervalFactor)
        {
            float lean = IntervalLean(intervalFactor);
            return Readiness * lean * (float)Math.Sqrt(lean);
        }

        /// <summary>One over the storyteller's disease interval, a quarter to double (AilmentCauseMath.DiseaseLean without the cutoff).</summary>
        public static float IntervalLean(float intervalFactor)
        {
            if (float.IsNaN(intervalFactor) || intervalFactor <= 0f) return 1f;
            float f = 1f / intervalFactor;
            return f < 0.25f ? 0.25f : f > 2f ? 2f : f;
        }

        // ---------------- The records ----------------

        /// <summary>This case (as <see cref="Join"/> makes it) counts in the budget: still owed, or the last one a skip paid for.</summary>
        public static bool Counted(IllnessBudgetBook b, string what) => b != null && what != null && ((b.owedFor?.Contains(what) ?? false) || b.lastPaid == what);

        /// <summary>A put-off case is said at most once a gap a home (the rest are counted only).</summary>
        public static bool SayPutOff(IllnessBudgetBook b, int now) => b != null && !Within(b.lastPutOff, now, GapDays);

        /// <summary>Nothing left worth keeping: no debt, and every tick older than a debt would be kept.</summary>
        public static bool Forgettable(IllnessBudgetBook b, int now, float keepDays)
        {
            if (b == null) return true;
            return Owing(b, now, keepDays) == 0 && !Within(b.lastGame, now, keepDays) && !Within(b.lastSkip, now, keepDays)
                   && !Within(b.lastPutOff, now, keepDays) && !Within(b.noteTick, now, YearDays);
        }

        /// <summary>"defName|name" as owed; either part may be empty.</summary>
        public static void Split(string owedFor, out string illness, out string who)
        {
            illness = who = "";
            if (string.IsNullOrEmpty(owedFor)) return;
            int bar = owedFor.IndexOf('|');
            if (bar < 0)
            {
                illness = owedFor;
                return;
            }
            illness = owedFor.Substring(0, bar);
            who = owedFor.Substring(bar + 1);
        }

        public static string Join(string illness, string who) => (illness ?? "").Replace("|", "") + "|" + (who ?? "").Replace("|", "");
    }
}
