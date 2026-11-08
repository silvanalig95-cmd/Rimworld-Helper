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

    /// <summary>Whether a home has room for a new serious illness of Rimfolk's within the budget (<see cref="IllnessBudgetMath.Room"/>).</summary>
    public enum BudgetRoom
    {
        Open,
        /// <summary>Rimfolk's last case here still owes its skip: the game's next disease hasn't come to pay it.</summary>
        Owed,
        /// <summary>The budget held one of the game's diseases back lately: the game's own comes before Rimfolk takes another.</summary>
        GameTurn,
        /// <summary>The game's own disease came within the gap (thirty days).</summary>
        Waiting
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
        /// <summary>The game's diseases held back and Rimfolk's cases put off here, all told (the dev tools' log).</summary>
        public int held, putOff;
        /// <summary>The council's last record line here and when it was written (the advisor's sickness line shows it for a year).</summary>
        public string note;
        public int noteTick = IllnessBudgetMath.Never;
    }

    /// <summary>
    /// The shared illness budget (the player's choice, 8 November 2026: "just the shared illness budget please"). Rimfolk's
    /// serious illnesses and the game's own diseases draw on one clock, so in all serious illness comes about as often as in
    /// plain RimWorld, and some of the cases are Rimfolk's kinds.
    /// <list type="bullet">
    /// <item>The budget is the game's own rate: the storyteller's human diseases on a home, by the land's disease days and
    /// the storyteller's interval (<see cref="GameMeanDays"/>).</item>
    /// <item>Each serious case of Rimfolk's that starts owes a skip: the game's next counted disease aimed at that home is
    /// held back (<see cref="Hold"/>). A debt is kept until it's paid or for three of the game's usual gaps between its
    /// diseases there, two years at least and five at most (<see cref="KeepDays"/>).</item>
    /// <item>The game's diseases wait Rimfolk too: once the game's own has come, Rimfolk starts none there for the gap
    /// (thirty days, the serious illnesses' own gap).</item>
    /// <item>Rimfolk's share is capped (<see cref="Room"/>): one debt at a time, and after a skip the game's own disease
    /// comes before Rimfolk takes another; so at most about half of the cases are Rimfolk's (<see cref="MaxShare"/>), and
    /// the game's own still come.</item>
    /// </list>
    /// Crowding and dirt still make Rimfolk's catching kinds likelier, which only shifts the mix: each of them owes its
    /// skip like any other. Pure, so the tests and the balance model read the same rules.
    /// </summary>
    public static class IllnessBudgetMath
    {
        public const int DayTicks = 60000;
        public const int Never = int.MinValue;
        public const float YearDays = 60f;

        /// <summary>After the game's own disease, no new serious illness of Rimfolk's there for this long (AilmentTracker.SeriousGapDays).</summary>
        public const float WaitDays = 30f;

        /// <summary>A home owes at most this many skips before Rimfolk starts another case there.</summary>
        public const int MaxOwed = 1;

        /// <summary>A debt is kept for this many of the game's usual gaps between its diseases...</summary>
        public const float KeepGaps = 3f;

        /// <summary>... but at least two years and at most five.</summary>
        public const float MinKeepDays = 120f, MaxKeepDays = 300f;

        /// <summary>The game's mean days between its human diseases where it has none to send (diseases off, or no storyteller part for them).</summary>
        public const float NoGameDiseases = float.PositiveInfinity;

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
        /// The most of the cases that can be Rimfolk's when it would take every turn it may: one of Rimfolk's, the game's next
        /// held back, the game's own after it, then the gap, which starts again with each of the game's that comes within it
        /// (a thirty-day stretch without one comes with the chance e^(-30/mean)). About a third on a temperate map at the
        /// usual interval, two fifths at Adventure story's, never more than half.
        /// </summary>
        public static float MaxShare(float gameMeanDays)
        {
            if (float.IsNaN(gameMeanDays) || float.IsInfinity(gameMeanDays) || gameMeanDays <= 0f) return 0.5f;
            float quiet = (float)Math.Exp(-WaitDays / gameMeanDays);
            return 1f / (2f + (1f - quiet) / quiet);
        }

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
        /// The game's own counted disease is about to come here: held back if a debt is owed, paying the oldest (into
        /// <paramref name="paidFor"/>, what it was for). False: it comes, as it would have.
        /// </summary>
        public static bool Hold(IllnessBudgetBook b, int now, float keepDays, out string paidFor)
        {
            paidFor = null;
            if (b == null) return false;
            Prune(b, now, keepDays);
            if (b.owed.Count == 0) return false;
            paidFor = b.owedFor.Count > 0 ? b.owedFor[0] : "";
            b.owed.RemoveAt(0);
            if (b.owedFor.Count > 0) b.owedFor.RemoveAt(0);
            b.lastSkip = now;
            b.lastPaid = paidFor;
            b.held++;
            return true;
        }

        /// <summary>The game's own counted disease came here: Rimfolk's serious illnesses wait the gap.</summary>
        public static void GameCame(IllnessBudgetBook b, int now)
        {
            if (b != null) b.lastGame = now;
        }

        /// <summary>
        /// Whether this home has room for a new serious illness of Rimfolk's now: no skip owed, the game's own turn taken
        /// since the last skip, and the gap since the game's own disease past. The colony's own room (one kind at a time,
        /// thirty days from the last) is asked as well, as it always was.
        /// </summary>
        public static BudgetRoom Room(IllnessBudgetBook b, int now, float keepDays)
        {
            if (b == null) return BudgetRoom.Open;
            if (Owing(b, now, keepDays) >= MaxOwed) return BudgetRoom.Owed;
            // After a skip the game's own comes first: its turn, unless the skip is so old a debt from then would have lapsed.
            if (Within(b.lastSkip, now, keepDays) && (b.lastGame == Never || b.lastGame < b.lastSkip || b.lastGame > now)) return BudgetRoom.GameTurn;
            if (Within(b.lastGame, now, WaitDays)) return BudgetRoom.Waiting;
            return BudgetRoom.Open;
        }

        /// <summary>This case (as <see cref="Join"/> makes it) counts in the budget: still owed, or the last one a skip paid for.</summary>
        public static bool Counted(IllnessBudgetBook b, string what) => b != null && what != null && ((b.owedFor?.Contains(what) ?? false) || b.lastPaid == what);

        /// <summary>A put-off case is said at most once a gap a home (the rest are counted only).</summary>
        public static bool SayPutOff(IllnessBudgetBook b, int now) => b != null && !Within(b.lastPutOff, now, WaitDays);

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
