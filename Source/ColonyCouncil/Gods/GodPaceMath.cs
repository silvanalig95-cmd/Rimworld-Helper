using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// How the gods' happenings are spaced (the player, 12 November 2026: "it just shouldn't feel like events are repeating
    /// unless there's an actual lore reason for it", and "if it's triggering on something where no other events really
    /// trigger, it shouldn't auto-trigger this event automatically when it's free, otherwise you can almost predict it").
    /// The shared rules every happening of the gods keeps, with no game types so Tools/Tests and Tools/Balance read them:
    /// <list type="bullet">
    /// <item>No blanket cap: each happening keeps its own gap, by what it is and why it comes.</item>
    /// <item>Once its gap is past, its chance starts small and grows over a long window (<see cref="Ramp"/>), so the first
    /// night it may come is the least likely, and nobody can tell when it will come or whether it will.</item>
    /// <item>A condition that is nearly always true is no trigger on its own (<see cref="Trigger"/>): only a strong or an
    /// unusual one is.</item>
    /// <item>When it comes back it leans to a different form (<see cref="Lean"/>), from a short memory of the last ones
    /// (<see cref="Note"/>); a repeat the world gives a reason for (the same cruelty done again) is the next step of its
    /// story, not the same card (<see cref="NextStep"/>).</item>
    /// </list>
    /// </summary>
    public static class GodPaceMath
    {
        /// <summary>How strongly the world calls for a happening: nothing (a condition nearly always true), an ordinary reason, a strong or unusual one.</summary>
        public enum Trigger
        {
            None,
            Ordinary,
            Strong
        }

        /// <summary>The share of a happening's chance once its gap is past: this much at the end of the gap, all of it a ramp later.</summary>
        public const float RampFrom = 0.25f;

        /// <summary>
        /// The share of its chance a happening has <paramref name="daysSince"/> days after the last one: none within its gap,
        /// a quarter at the end of it, rising to all of it <paramref name="rampDays"/> later.
        /// </summary>
        public static float Ramp(float daysSince, float gapDays, float rampDays)
        {
            if (float.IsNaN(daysSince) || daysSince < gapDays) return 0f;
            if (rampDays <= 0f) return 1f;
            return RampFrom + (1f - RampFrom) * Clamp01((daysSince - gapDays) / rampDays);
        }

        /// <summary>
        /// A day's chance of something that comes about every <paramref name="meanDays"/> days once free, never within
        /// <paramref name="gapDays"/> of the last, ramped over <paramref name="rampDays"/> after (<see cref="Ramp"/>).
        /// </summary>
        public static float DayChance(float daysSince, float gapDays, float meanDays, float rampDays)
        {
            if (float.IsNaN(meanDays) || meanDays <= 0f) return 0f;
            float r = Ramp(daysSince, gapDays, rampDays);
            return r <= 0f ? 0f : (float)(1.0 - Math.Exp(-r / meanDays));
        }

        /// <summary>A day's chance shared out over <paramref name="checks"/> looks in the day (an hour's chance from a day's: 24).</summary>
        public static float PerCheck(float dayChance, float checks)
        {
            if (float.IsNaN(dayChance) || dayChance <= 0f || checks <= 0f) return 0f;
            if (dayChance >= 1f) return 1f;
            return (float)(1.0 - Math.Pow(1.0 - dayChance, 1.0 / checks));
        }

        /// <summary>A trigger's weight on a chance: nothing for none, all of it for an ordinary one, more for a strong one.</summary>
        public static float Weight(Trigger t) => t == Trigger.Strong ? StrongFactor : t == Trigger.Ordinary ? 1f : 0f;

        /// <summary>A strong or unusual reason makes a happening this much likelier than an ordinary one.</summary>
        public const float StrongFactor = 2.2f;

        // ================= A short memory of the last ones =================

        /// <summary>The happenings remembered at most (the oldest let go first).</summary>
        public const int Memory = 12;

        /// <summary>One remembered happening: what it was, the form it took, and whom or what it was about ("return|Nightmare|1234").</summary>
        public static string Entry(string what, string form, string who = null) => (what ?? "") + "|" + (form ?? "") + "|" + (who ?? "");

        /// <summary>Remembers a happening, newest last, the oldest let go past <see cref="Memory"/>.</summary>
        public static void Note(List<string> lately, string what, string form, string who = null)
        {
            if (lately == null || string.IsNullOrEmpty(what)) return;
            lately.Add(Entry(what, form, who));
            while (lately.Count > Memory) lately.RemoveAt(0);
        }

        /// <summary>The forms a happening took lately, newest first (only those about <paramref name="who"/> when it is given).</summary>
        public static List<string> Forms(IList<string> lately, string what, string who = null)
        {
            var list = new List<string>();
            if (lately == null || string.IsNullOrEmpty(what)) return list;
            for (int i = lately.Count - 1; i >= 0; i--)
            {
                var parts = (lately[i] ?? "").Split('|');
                if (parts.Length < 2 || parts[0] != what) continue;
                if (who != null && (parts.Length < 3 || parts[2] != who)) continue;
                list.Add(parts[1]);
            }
            return list;
        }

        /// <summary>
        /// How much a form is wanted again, by the forms lately (newest first): the last one hardly (a different one wins
        /// whenever there is one), the one before it half, any other fully.
        /// </summary>
        public static float Lean(string form, IList<string> recent)
        {
            if (recent == null || recent.Count == 0 || form == null) return 1f;
            if (recent[0] == form) return LastLean;
            if (recent.Count > 1 && recent[1] == form) return 0.5f;
            return 1f;
        }

        /// <summary>The last form's weight against the others: a sliver, so it comes again only when nothing else can.</summary>
        public const float LastLean = 0.02f;

        /// <summary>
        /// Picks among forms by weight, each leaned away from the forms lately (<see cref="Lean"/>), with
        /// <paramref name="roll"/> in [0, 1). -1 when none has any weight.
        /// </summary>
        public static int Pick(IList<string> forms, IList<float> weights, IList<string> recent, float roll)
        {
            if (forms == null || weights == null) return -1;
            int n = Math.Min(forms.Count, weights.Count);
            float total = 0f;
            var w = new float[n];
            for (int i = 0; i < n; i++)
            {
                w[i] = float.IsNaN(weights[i]) || weights[i] <= 0f ? 0f : weights[i] * Lean(forms[i], recent);
                total += w[i];
            }
            if (total <= 0f) return -1;
            float x = Clamp01(float.IsNaN(roll) ? 0f : roll) * total;
            for (int i = 0; i < n; i++)
            {
                if (w[i] <= 0f) continue;
                if (x < w[i]) return i;
                x -= w[i];
            }
            for (int i = n - 1; i >= 0; i--)
                if (w[i] > 0f)
                    return i;
            return -1;
        }

        /// <summary>
        /// The next step of a story that comes back for a reason (the same cruelty done again): the form after
        /// <paramref name="last"/> in <paramref name="order"/> that can come now (<paramref name="can"/>), else the furthest
        /// one that can. Null when none can.
        /// </summary>
        public static string NextStep(IList<string> order, string last, Func<string, bool> can)
        {
            if (order == null || order.Count == 0 || can == null) return null;
            int from = last == null ? -1 : order.IndexOf(last);
            for (int i = from + 1; i < order.Count; i++)
                if (can(order[i]))
                    return order[i];
            for (int i = order.Count - 1; i >= 0; i--)
                if (can(order[i]))
                    return order[i];
            return null;
        }

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : Math.Max(0f, Math.Min(1f, v));
    }
}
