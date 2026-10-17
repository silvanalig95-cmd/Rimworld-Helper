using System;
using System.Collections.Generic;
using System.Globalization;

namespace ColonyCouncil
{
    /// <summary>One condition on a person's nature, read from a mark's "when" ("nerve&gt;=10").</summary>
    public struct MarkCond
    {
        public int axis;
        public bool atLeast;
        public float value;

        public bool Holds(float[] values) =>
            values != null && axis >= 0 && axis < values.Length && (atLeast ? values[axis] >= value : values[axis] <= value);
    }

    /// <summary>How much a person's nature makes a mark weigh on them: an axis and how strongly it counts ("nerve:-0.6").</summary>
    public struct MarkLean
    {
        public int axis;
        public float k;
    }

    /// <summary>A mark a person already carries, as the rate limits see it.</summary>
    public struct MarkRecord
    {
        public string def;
        /// <summary>The happening it belongs to (several marks can tell one death); one happening, one mark.</summary>
        public string story;
        /// <summary>Who or what it was about (the other person's id, else the label).</summary>
        public string about;
        public bool major;
        public int tick;
    }

    /// <summary>
    /// The arithmetic of marks (Persona/Marks.cs), with no game types, so the tests can check it: reading a mark's
    /// conditions and leans, choosing the mark that fits a person's nature, how much it moves them, and the limits
    /// that keep marks rare enough to mean something.
    /// </summary>
    public static class MarksMath
    {
        public const int DayTicks = 60000;
        /// <summary>At most one small mark per person in this many days.</summary>
        public const float MinorGapDays = 3f;
        /// <summary>At most this many turning points per person in a day (a bad raid is one story, not five).</summary>
        public const int MajorsPerDay = 2;
        /// <summary>One happening (a death, a betrayal) leaves one mark of its kind, however many ways the game tells it.</summary>
        public const float SameStoryDays = 10f;
        /// <summary>How far a person's nature can scale a mark: the timid shaken half again as much, the bold hardly at all.</summary>
        public const float MinScale = 0.4f, MaxScale = 1.6f;
        /// <summary>A small mark doesn't come again for this long unless its def says otherwise.</summary>
        public const int MinorRepeatDays = 20;

        public static readonly string[] AxisNames = { "warmth", "nerve", "temper", "humour", "trust", "pride", "openness", "outlook" };

        public static int AxisIndex(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            string n = name.Trim().ToLowerInvariant();
            if (n == "humor") n = "humour";
            return Array.IndexOf(AxisNames, n);
        }

        /// <summary>
        /// Reads "nerve&gt;=10, warmth&lt;=-20" (all must hold; &gt;= and &lt;= only, or &gt; and &lt; read the same). Empty
        /// is no condition. False, with the reason, for anything it can't read.
        /// </summary>
        public static bool TryParseWhen(string text, out List<MarkCond> conds, out string error)
        {
            conds = new List<MarkCond>();
            error = null;
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0) return true;
            foreach (var raw in text.Split(','))
            {
                string part = raw.Trim();
                if (part.Length == 0) continue;
                int op = part.IndexOfAny(new[] { '>', '<' });
                if (op <= 0)
                {
                    error = "'" + part + "' has no > or <";
                    return false;
                }
                bool atLeast = part[op] == '>';
                int axis = AxisIndex(part.Substring(0, op));
                if (axis < 0)
                {
                    error = "'" + part.Substring(0, op).Trim() + "' is not a side of a person";
                    return false;
                }
                string num = part.Substring(op + 1).TrimStart('=').Trim();
                if (!float.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                {
                    error = "'" + num + "' is not a number";
                    return false;
                }
                conds.Add(new MarkCond { axis = axis, atLeast = atLeast, value = v });
            }
            return true;
        }

        /// <summary>Reads "nerve:-0.6, warmth:0.3". Empty is no lean.</summary>
        public static bool TryParseLean(string text, out List<MarkLean> lean, out string error)
        {
            lean = new List<MarkLean>();
            error = null;
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0) return true;
            foreach (var raw in text.Split(','))
            {
                string part = raw.Trim();
                if (part.Length == 0) continue;
                int colon = part.IndexOf(':');
                int axis = colon > 0 ? AxisIndex(part.Substring(0, colon)) : -1;
                if (axis < 0)
                {
                    error = "'" + part + "' should read side:number";
                    return false;
                }
                if (!float.TryParse(part.Substring(colon + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float k) || Math.Abs(k) > 2f)
                {
                    error = "'" + part + "' needs a number from -2 to 2";
                    return false;
                }
                lean.Add(new MarkLean { axis = axis, k = k });
            }
            return true;
        }

        public static bool Holds(List<MarkCond> conds, float[] values)
        {
            if (conds == null) return true;
            for (int i = 0; i < conds.Count; i++)
                if (!conds[i].Holds(values)) return false;
            return true;
        }

        /// <summary>
        /// How much a mark moves this person: 1, plus each lean times how far they are on its side (a lean of -0.6 on
        /// nerve makes a timid person at -60 take it 1.36 times as hard, a bold one at +60 0.64 times).
        /// </summary>
        public static float Scale(List<MarkLean> lean, float[] values)
        {
            float s = 1f;
            if (lean != null && values != null)
                for (int i = 0; i < lean.Count; i++)
                {
                    var l = lean[i];
                    if (l.axis >= 0 && l.axis < values.Length) s += l.k * values[l.axis] / 100f;
                }
            return s < MinScale ? MinScale : s > MaxScale ? MaxScale : s;
        }

        /// <summary>The first of the candidates (in their order) whose conditions fit this person, or -1.</summary>
        public static int Choose(IList<List<MarkCond>> candidates, float[] values)
        {
            if (candidates == null) return -1;
            for (int i = 0; i < candidates.Count; i++)
                if (Holds(candidates[i], values)) return i;
            return -1;
        }

        /// <summary>
        /// Why this mark can't be given now, or null if it can:
        /// <list type="bullet">
        /// <item>"same": the same mark about the same thing is still on them;</item>
        /// <item>"repeat": the same mark came within its repeat window (<paramref name="repeatDays"/>);</item>
        /// <item>"story": this happening already left a mark of its kind (<see cref="SameStoryDays"/>);</item>
        /// <item>"minor": a small mark came within <see cref="MinorGapDays"/>;</item>
        /// <item>"day": they've had <see cref="MajorsPerDay"/> turning points today already.</item>
        /// </list>
        /// </summary>
        public static string Blocked(IList<MarkRecord> history, MarkRecord next, int repeatDays, int now)
        {
            if (history == null) return null;
            int majorsToday = 0;
            for (int i = 0; i < history.Count; i++)
            {
                var h = history[i];
                int age = now - h.tick;
                if (h.def == next.def && h.about == next.about) return "same";
                if (h.def == next.def && repeatDays > 0 && age < (long)repeatDays * DayTicks) return "repeat";
                if (!string.IsNullOrEmpty(next.story) && h.story == next.story && h.about == next.about && age < SameStoryDays * DayTicks) return "story";
                if (!next.major && !h.major && age < MinorGapDays * DayTicks) return "minor";
                if (h.major && age < DayTicks) majorsToday++;
            }
            if (next.major && majorsToday >= MajorsPerDay) return "day";
            return null;
        }

        /// <summary>How many of these moments (ticks) fall within the last <paramref name="windowDays"/> days.</summary>
        public static int CountWithin(IList<int> ticks, int now, float windowDays)
        {
            if (ticks == null) return 0;
            int n = 0;
            for (int i = 0; i < ticks.Count; i++)
                if (now - ticks[i] >= 0 && now - ticks[i] < windowDays * DayTicks) n++;
            return n;
        }

        // ---------------- How character shows elsewhere (soft, small) ----------------

        /// <summary>
        /// How much likelier a person is to start a moment with someone, from their character: the warm and the
        /// playful start more warm ones, the hot-tempered more frictions, and the warm comfort more in grief. 0.7 to 1.35.
        /// </summary>
        public static float MomentLean(float warmth, float humour, float temper, bool friction, bool grief)
        {
            float f;
            if (friction) f = 1f + 0.25f * temper / 100f - 0.08f * warmth / 100f;
            else
            {
                f = 1f + 0.15f * warmth / 100f + 0.1f * humour / 100f;
                if (grief) f *= 1f + 0.2f * warmth / 100f;
            }
            return f < 0.7f ? 0.7f : f > 1.35f ? 1.35f : f;
        }

        /// <summary>How readily someone confides a secret in a friend, from how trusting they are: 0.4 to 1.6.</summary>
        public static float ConfideFactor(float trust)
        {
            float f = 1f + 0.6f * trust / 100f;
            return f < 0.4f ? 0.4f : f > 1.6f ? 1.6f : f;
        }

        /// <summary>How likely a skilled colonist is to take on a student, from their warmth: 0.5 to 1.6.</summary>
        public static float MentorWeight(float warmth)
        {
            float f = 1f + 0.6f * warmth / 100f;
            return f < 0.5f ? 0.5f : f > 1.6f ? 1.6f : f;
        }
    }
}
