using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>How hard life's everyday pressures press (the "Life's ups and downs" setting).</summary>
    public enum LifePressure
    {
        Gentle,
        Normal,
        Demanding
    }

    /// <summary>
    /// The arithmetic of everyday pressures (Pressures.cs), free of game types so the tests can cover it (Tools/Tests):
    /// when the same meal or the same work starts to wear, how long someone can be away before they're missed, when a
    /// room is too full, how much half-built work is an eyesore, and how the setting scales it all.
    /// </summary>
    public static class PressureMath
    {
        // ---------------- The setting ----------------

        /// <summary>Gentle halves the pressures, demanding makes them half again as strong.</summary>
        public static float Factor(LifePressure mode) => mode == LifePressure.Gentle ? 0.5f : mode == LifePressure.Demanding ? 1.5f : 1f;

        /// <summary>
        /// A pressure's mood at a given strength (the setting times the colonist's standards). A pressure that shows is
        /// always felt as at least a whole point, so the gentle setting never leaves a "0" in the Needs tab.
        /// </summary>
        public static float Scale(float offset, float factor)
        {
            if (offset >= 0f) return offset;
            if (factor <= 0f) return 0f;
            float v = offset * factor;
            return v > -1f ? -1f : v;
        }

        // ---------------- Meals ----------------

        /// <summary>How many meals each colonist's history keeps, and for how long.</summary>
        public const int MealsKept = 12;
        public const float MealWindowDays = 7f;
        /// <summary>A varied table: this many different foods within this many days.</summary>
        public const int VarietyKinds = 3;
        public const float VarietyWindowDays = 2f;

        /// <summary>How often the most common kind comes up among the last <paramref name="window"/> entries.</summary>
        public static int Dominant(IList<string> kinds, int window)
        {
            if (kinds == null || kinds.Count == 0 || window <= 0) return 0;
            int from = Math.Max(0, kinds.Count - window);
            int best = 0;
            for (int i = from; i < kinds.Count; i++)
            {
                int n = 0;
                for (int j = from; j < kinds.Count; j++)
                    if (kinds[j] == kinds[i]) n++;
                if (n > best) best = n;
            }
            return best;
        }

        /// <summary>
        /// The same meal again (0 = fine, 1 to 3), from the meals of the last week, oldest first. A colony that can
        /// cook only one kind of meal feels it least (one step, and only when nothing else was eaten for eight meals);
        /// with two to choose from it stops at two steps; the third step needs three choices.
        /// </summary>
        public static int FoodStage(IList<string> kinds, int options)
        {
            int n = kinds?.Count ?? 0;
            if (n < 6) return 0;
            if (options <= 1) return n >= 8 && Dominant(kinds, 8) >= 8 ? 1 : 0;
            int stage = Dominant(kinds, 8) >= 6 ? 1 : 0;
            if (n >= 12)
            {
                int d12 = Dominant(kinds, 12);
                if (d12 >= 10) stage = 2;
                if (d12 >= 12 && options >= 3) stage = 3;
            }
            return stage;
        }

        /// <summary>Gourmands take it a step harder once it's there, when the colony could cook something else.</summary>
        public static int Picky(int stage, int options)
        {
            if (stage <= 0 || options <= 1) return stage;
            return Math.Min(stage + 1, options >= 3 ? 3 : 2);
        }

        /// <summary>At least <see cref="VarietyKinds"/> different foods among these (the last two days' meals).</summary>
        public static bool VariedTable(IList<string> recent)
        {
            if (recent == null || recent.Count < VarietyKinds) return false;
            var seen = new HashSet<string>();
            foreach (var k in recent)
                if (k != null) seen.Add(k);
            return seen.Count >= VarietyKinds;
        }

        // ---------------- Work ----------------

        /// <summary>A day needs this many hours of work seen, and one kind of work this share of them, to count as the same work all day.</summary>
        public const int MinWorkSamples = 6;
        public const float SameWorkShare = 0.7f;

        public static bool SameWorkDay(int top, int total) => total >= MinWorkSamples && top >= total * SameWorkShare;

        /// <summary>
        /// The run of days spent on one kind of work, after a day ends: one more if it was the same work as before, a new
        /// run of one if it was other work all day, and none after a varied day or a day mostly off work.
        /// </summary>
        public static int NextStreak(int streak, bool sameWorkDay, bool sameKindAsBefore)
        {
            if (!sameWorkDay) return 0;
            return sameKindAsBefore ? streak + 1 : 1;
        }

        /// <summary>Three days running is one step, five two, eight three.</summary>
        public static int WorkStage(int streakDays) => streakDays >= 8 ? 3 : streakDays >= 5 ? 2 : streakDays >= 3 ? 1 : 0;

        // ---------------- Missing someone ----------------

        /// <summary>A partner is missed after two days, more after five and ten; a close friend after three and eight.</summary>
        public static int MissingStage(float daysApart, bool partner)
        {
            if (partner) return daysApart >= 10f ? 3 : daysApart >= 5f ? 2 : daysApart >= 2f ? 1 : 0;
            return daysApart >= 8f ? 2 : daysApart >= 3f ? 1 : 0;
        }

        // ---------------- Crowding ----------------

        /// <summary>A room is only crowded with at least this many people in it.</summary>
        public const int CrowdMinPeople = 4;

        /// <summary>
        /// How crowded a shared room is (0 = fine, 1 = elbow to elbow, 2 = packed): space per person, and in a dining
        /// room, people eating against the seats there (seats below 0: seats don't matter, as in a rec room).
        /// </summary>
        public static int CrowdLevel(int people, int cells, int diners, int seats)
        {
            if (people < CrowdMinPeople || cells <= 0) return 0;
            float per = cells / (float)people;
            int lacking = seats >= 0 ? diners - seats : 0;
            if (per < 2.5f || (seats >= 0 && lacking >= Math.Max(3, (diners + 1) / 2))) return 2;
            if (per < 4f || (seats >= 0 && lacking >= 2)) return 1;
            return 0;
        }

        // ---------------- Unfinished building ----------------

        /// <summary>A frame nobody has worked on for this long, or a plan waiting this long, has been left.</summary>
        public const float FrameStaleDays = 2f;
        public const float BlueprintStaleDays = 4f;

        /// <summary>
        /// Half-built frames count in full; plans that were never started a fifth each, and at most 6 together, so old
        /// plans alone are never more than one step.
        /// </summary>
        public static float UnfinishedScore(int staleFrames, int staleBlueprints) => staleFrames + Math.Min(6f, staleBlueprints / 5f);

        public static int UnfinishedStage(float score) => score >= 25f ? 3 : score >= 10f ? 2 : score >= 3f ? 1 : 0;

        // ---------------- Rewards earned ----------------

        /// <summary>
        /// How much of an ambition's reward is earned: in full when at least half of it was still to do when the colony
        /// chose it, down to 60% when it was nearly done already (-1: not known, an older save).
        /// </summary>
        public static float EffortFactor(float startProgress)
        {
            if (startProgress < 0f) return 1f;
            return Math.Min(1f, Math.Max(0.6f, 1.5f - startProgress));
        }
    }
}
