using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>The game-free rules behind some decrees, kept apart so the tests can check them (Tools/Tests).</summary>
    public static class DecreeMath
    {
        /// <summary>
        /// "Many are hurt" (the Wounded moment): two colonists or more, and at least a third of them, hurt.
        /// </summary>
        public static bool ManyHurt(int hurt, int colonists) => hurt >= 2 && hurt * 3 >= colonists;

        // ---------------- Needed and wanted: a decree again sooner ----------------

        /// <summary>A moment that holds counts this much toward a decree being needed, and this much when it holds strongly.</summary>
        public const int Holds = 1, HoldsStrongly = 2;

        /// <summary>The fewest days between a decree's end and declaring it again, however much it's needed.</summary>
        public const float MinCooldownDays = 1f;

        /// <summary>At most this share of a decree's rest is let off when it's needed and wanted, and of the resentment of a repeat.</summary>
        public const float MostCooldownLetOff = 0.65f, MostBacklashLetOff = 0.6f;

        /// <summary>
        /// How much the colony needs a decree now, 0 to 1, from its moments that hold (each <see cref="Holds"/>, or
        /// <see cref="HoldsStrongly"/>): three's worth is full need (a filthy home and many hurt after fight on fight).
        /// </summary>
        public static float Need(int strength) => Clamp01(strength / 3f);

        /// <summary>
        /// How much the colony wants it, 0 to 1: those who love it count most and those who hate it against it, those who
        /// don't mind it count for nothing; someone asking the council for it adds.
        /// </summary>
        public static float Want(int love, int like, int neutral, int dislike, int hate, bool asked)
        {
            int all = love + like + neutral + dislike + hate;
            float w = all > 0 ? (love + 0.75f * like - 0.5f * dislike - 0.75f * hate) / all : 0f;
            return Clamp01(w + (asked ? 0.3f : 0f));
        }

        /// <summary>
        /// The colony's understanding for a decree declared sooner and again, 0 to 1: none unless it's needed, more the
        /// more it's wanted. Patience wears thin: once it has been declared twice in the last month, it's halved.
        /// </summary>
        public static float Understanding(float need, float want, int timesThisMonth)
        {
            if (need <= 0f) return 0f;
            float u = Clamp01(need) * (0.4f + 0.6f * Clamp01(want));
            return timesThisMonth >= 2 ? u * 0.5f : u;
        }

        /// <summary>A decree's rest after it ends (ticks), with the colony's understanding: never shorter than a day, nor longer than its own.</summary>
        public static int CooldownTicks(float cooldownDays, float understanding, int ticksPerDay)
        {
            float full = cooldownDays * ticksPerDay;
            float let = full * (1f - MostCooldownLetOff * Clamp01(understanding));
            return (int)Math.Round(Math.Min(full, Math.Max(MinCooldownDays * ticksPerDay, let)));
        }

        /// <summary>How much of a repeat's resentment (lost trust, the mood against it, the chance someone snaps) still comes, with the colony's understanding.</summary>
        public static float BacklashFactor(float understanding) => 1f - MostBacklashLetOff * Clamp01(understanding);

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>
        /// A colonist's work during a clean sweep, into <paramref name="into"/>: their essential work first, in their own
        /// order; then every kind of cleaning (<paramref name="cleaning"/>, whether or not they were set to clean); then
        /// the rest of their work as they had it.
        /// </summary>
        public static void SweepOrder<T>(IList<T> own, IList<T> cleaning, Func<T, bool> essential, Func<T, bool> isCleaning, List<T> into)
        {
            into.Clear();
            foreach (var w in own)
                if (essential(w)) into.Add(w);
            foreach (var w in cleaning) into.Add(w);
            foreach (var w in own)
                if (!essential(w) && !isCleaning(w)) into.Add(w);
        }

        // ---------------- Decrees that mean distinct things (decision 13 of the review, 6 November 2026) ----------------

        // In the player's save drills and militia drills were declared the same day and both ran. Where two decrees are
        // one idea they are one decree in two forms, only one of which is listed: the drills by the colony's age (the
        // muster of a village for a tribal or medieval colony, drills for an industrial or spacer one), and rationing by
        // the moment (famine bread while a famine lies over the region and the colony's own stores aren't low, else plain
        // rationing). Pairs that overlap without being one idea can't run together: festival days and a feast day (both
        // celebrations that raise standards), quiet hours and days of rest (both rest), on guard and a curfew (sleep in
        // shifts, or everyone abed by ten). Rest when ill and quarantine stay apart and may run together: one heals the
        // sick, the other keeps the well from catching it.

        public const string Drills = "Council_Decree_Drills", Militia = "Council_Decree_Militia";
        public const string Rationing = "Council_Decree_Rationing", FamineBread = "Council_Decree_FamineBread";

        /// <summary>The two forms of one idea: the drills by age, rationing by the moment.</summary>
        public static readonly string[][] Ideas = { new[] { Militia, Drills }, new[] { FamineBread, Rationing } };

        /// <summary>Pairs that overlap and can't run together.</summary>
        public static readonly string[][] Apart =
        {
            new[] { "Council_Decree_Festival", "Council_Decree_FeastDay" },
            new[] { "Council_Decree_QuietHours", "Council_Decree_Rest" },
            new[] { "Council_Decree_OnGuard", "Council_Decree_Curfew" },
            // One faith for the colony and every faith its evening are the two ways of the council with its faiths.
            new[] { OneFaith, EveryFaith }
        };

        public const string OneFaith = "Council_Decree_OneFaith", EveryFaith = "Rimfolk_Decree_EveryFaith";

        /// <summary>The other form of the idea a decree is one form of, or null for a decree of its own.</summary>
        public static string OtherForm(string defName)
        {
            foreach (var idea in Ideas)
            {
                if (idea[0] == defName) return idea[1];
                if (idea[1] == defName) return idea[0];
            }
            return null;
        }

        /// <summary>
        /// The form of its idea a decree takes now: the drills by the colony's age (<paramref name="lowTech"/>: tribal or
        /// medieval, the muster), rationing by the moment (<paramref name="famine"/> in the region and the stores not low:
        /// famine bread). A decree of its own is itself.
        /// </summary>
        public static string FormNow(string defName, bool lowTech, bool famine, bool foodLow)
        {
            if (defName == Drills || defName == Militia) return lowTech ? Militia : Drills;
            if (defName == Rationing || defName == FamineBread) return famine && !foodLow ? FamineBread : Rationing;
            return defName;
        }

        /// <summary>A decree in force that keeps another from being declared: the other form of its idea, or the other of an overlapping pair.</summary>
        public static bool Excludes(string a, string b)
        {
            if (a == null || b == null || a == b) return false;
            if (OtherForm(a) == b) return true;
            foreach (var pair in Apart)
                if (pair[0] == a && pair[1] == b || pair[0] == b && pair[1] == a)
                    return true;
            return false;
        }

        /// <summary>
        /// An older save's decree in force that is an idea's form no longer listed (militia drills in an industrial colony):
        /// it carries on as the form listed now, for what was left of it and never longer than that form runs
        /// (<paramref name="formDays"/>). Ticks: when it would end now.
        /// </summary>
        public static int CarriedEnd(int now, int endTick, float formDays, int ticksPerDay) =>
            Math.Min(endTick, now + (int)Math.Round(formDays * ticksPerDay));
    }
}
