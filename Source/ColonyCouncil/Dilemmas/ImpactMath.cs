using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>The kinds of thing an answer does, in the order they're listed.</summary>
    public enum ImpactKind
    {
        Map,
        Mood,
        Opinion,
        Trust,
        Needs,
        Health,
        Skill,
        Goods,
        People,
        Goodwill,
        Other,
        Ways
    }

    /// <summary>
    /// One thing an answer does, in words: who it touches, what happens to them, and, for amounts that add up (trust,
    /// goods, silver), the amount itself, written into the text where it says <see cref="ImpactMath.Mark"/>.
    /// </summary>
    public sealed class ImpactLine
    {
        public ImpactKind kind;
        /// <summary>
        /// Good for the colony (1), a cost (-1), or neither (0). For an amount that adds up, what a positive amount is
        /// (trust gained: 1); <see cref="Sign"/> turns it round for a negative total.
        /// </summary>
        public int sign;
        /// <summary>The full line ("Mara: +5 mood for 4 days (forgiven)"), with <see cref="ImpactMath.Mark"/> for the amount when it adds up.</summary>
        public string text;
        /// <summary>The short form for the line under the answer ("Mara +5 mood").</summary>
        public string brief;
        /// <summary>Lines with the same key are one thing: amounts add up, else the second is dropped. Null: never merged.</summary>
        public string key;
        public float amount;
        /// <summary>The amount adds up with the same key's other lines (and a total of nothing drops the line).</summary>
        public bool sum;
        /// <summary>The amount is written with its sign ("+6 trust"); off for a count ("12 beer").</summary>
        public bool signed = true;
        /// <summary>Below 1: it may not happen ("1 in 3: ...").</summary>
        public float chance = 1f;
        /// <summary>The chance is rolled for each person it could touch.</summary>
        public bool each;

        // For the hints in words (Dilemmas/ImpactHints.cs), set where a line's own words carry numbers.

        /// <summary>What the line is about, for telling it in words; None: its own words with the numbers taken out.</summary>
        public HintTopic topic;
        /// <summary>Who it touches, as the exact line names them ("everyone (10)", "Mara and Tam").</summary>
        public string who;
        /// <summary>The thing, person, people, skill or way it is about ("beer", "Tam", "the Iron Hand", "cooking").</summary>
        public string about;
        /// <summary>Its size when it doesn't add up (a mood's points, recreation as a share of the bar), signed.</summary>
        public float size;
        /// <summary>How many the stores hold, for goods (below 0: not known).</summary>
        public float stock = -1f;
        /// <summary>How many days a mood lasts (0: while it lasts).</summary>
        public float days;
        /// <summary>The short hint in words, when the line's own short form has numbers in it.</summary>
        public string hint;
        /// <summary>The hint in words for the tooltip, when the line's own words have numbers in it.</summary>
        public string hintFull;
        /// <summary>Something on the map: how many hours it takes.</summary>
        public float hours;
        /// <summary>Something on the map in working hours that takes people from their work.</summary>
        public bool workTime;

        /// <summary>Good (1), a cost (-1) or neither (0), as it stands with its amount.</summary>
        public int Sign => sum && amount < 0f ? -sign : sign;

        public string Text => ImpactMath.Fill(text, amount, sum && signed);
        public string Brief => ImpactMath.Fill(brief ?? text, amount, sum && signed);
    }

    /// <summary>All an answer does: its lines, what more happens when its gamble goes wrong, and its live odds.</summary>
    public sealed class OptionImpact
    {
        public List<ImpactLine> lines = new List<ImpactLine>();
        /// <summary>What happens when the risk comes true: all of it when it replaces the plain effects, else what it adds.</summary>
        public List<ImpactLine> risk = new List<ImpactLine>();
        public float riskChance;
        public bool riskReplaces;
        /// <summary>"Mara's Social brings it down from 1 in 4", or null.</summary>
        public string riskNote;
        /// <summary>The skill that makes the gamble less likely to go wrong ("social"), or null.</summary>
        public string riskSkill;
        /// <summary>Who brings the risk down with that skill now, or null.</summary>
        public string riskHelper;

        public bool Empty => lines.Count == 0 && risk.Count == 0 && riskChance <= 0f;
    }

    /// <summary>
    /// The plain arithmetic of telling what an answer does (Dilemmas/DilemmaImpact.cs words it): chances as small
    /// fractions, lines merged and ordered, signed numbers, the short form cut to fit. No game types: tested in
    /// Tools/Tests (ImpactTests).
    /// </summary>
    public static class ImpactMath
    {
        /// <summary>Where an amount goes in a line's words (filled by <see cref="Fill"/>).</summary>
        public const string Mark = "%n%";

        /// <summary>
        /// A chance as "n in d": the simplest fraction within half a percent (exact), else the nearest with ten or fewer
        /// (not exact: "about 1 in 3"). Below one in ten, one in however many; above nine in ten, one short of however many.
        /// </summary>
        public static (int n, int d, bool exact) Odds(float p)
        {
            if (p >= 1f) return (1, 1, true);
            if (p <= 0f) return (0, 1, true);
            const float close = 0.005f;
            if (p < 0.1f)
            {
                int d = Math.Max(2, (int)Math.Round(1f / p));
                return (1, d, Math.Abs(1f / d - p) < close);
            }
            if (p > 0.9f)
            {
                int d = Math.Max(2, (int)Math.Round(1f / (1f - p)));
                return (d - 1, d, Math.Abs((d - 1f) / d - p) < close);
            }
            for (int d = 2; d <= 10; d++)
            {
                int n = (int)Math.Round(p * d);
                if (n > 0 && n < d && Math.Abs((float)n / d - p) < close) return Reduce(n, d, true);
            }
            int bestN = 1, bestD = 2;
            float bestErr = float.MaxValue;
            for (int d = 2; d <= 10; d++)
            {
                int n = Math.Min(d - 1, Math.Max(1, (int)Math.Round(p * d)));
                float err = Math.Abs((float)n / d - p);
                // A smaller denominator wins unless a larger one is clearly closer.
                if (err < bestErr - 0.01f)
                {
                    bestErr = err;
                    bestN = n;
                    bestD = d;
                }
            }
            return Reduce(bestN, bestD, false);
        }

        private static (int, int, bool) Reduce(int n, int d, bool exact)
        {
            int a = n, b = d;
            while (b != 0)
            {
                int t = a % b;
                a = b;
                b = t;
            }
            return (n / a, d / a, exact);
        }

        /// <summary>"+6", "-4", "+0.5": whole numbers without decimals, small ones with one.</summary>
        public static string Signed(float v)
        {
            string s = Number(Math.Abs(v));
            return (v < 0f ? "-" : "+") + s;
        }

        /// <summary>"6", "0.5", "12": one decimal at most, none when it's whole.</summary>
        public static string Number(float v)
        {
            float r = (float)Math.Round(v, 1);
            return Math.Abs(r - Math.Round(r)) < 0.05f ? ((int)Math.Round(r)).ToString(CultureInfo.InvariantCulture) : r.ToString("0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>"+3", or "+3 to +5" when people feel it differently (<paramref name="to"/> joins the two).</summary>
        public static string SignedRange(float lo, float hi, string to)
        {
            string a = Signed(lo), b = Signed(hi);
            return a == b ? a : Signed(Math.Min(lo, hi)) + to + Signed(Math.Max(lo, hi));
        }

        /// <summary>The amount written into a line where it says <see cref="Mark"/>: signed when it adds up, plain otherwise.</summary>
        public static string Fill(string template, float amount, bool signed)
        {
            if (string.IsNullOrEmpty(template) || template.IndexOf(Mark, StringComparison.Ordinal) < 0) return template ?? "";
            return template.Replace(Mark, signed ? Signed(amount) : Number(Math.Abs(amount)));
        }

        /// <summary>
        /// One line for each thing: the same key once (amounts added; a total of nothing dropped), the same words once,
        /// in the order of <see cref="ImpactKind"/>, and in the order they came within a kind.
        /// </summary>
        public static List<ImpactLine> Merge(IEnumerable<ImpactLine> lines)
        {
            var outList = new List<ImpactLine>();
            var byKey = new Dictionary<string, ImpactLine>();
            var seen = new HashSet<string>();
            if (lines != null)
                foreach (var l in lines)
                {
                    if (l == null || string.IsNullOrEmpty(l.text)) continue;
                    if (l.key != null && byKey.TryGetValue(l.key, out var had))
                    {
                        if (l.sum && had.sum) had.amount += l.amount;
                        // The surer of two lines about the same thing stands.
                        had.chance = Math.Max(had.chance, l.chance);
                        continue;
                    }
                    var copy = new ImpactLine
                    {
                        kind = l.kind, sign = l.sign, text = l.text, brief = l.brief, key = l.key, amount = l.amount, sum = l.sum,
                        signed = l.signed, chance = l.chance, each = l.each, topic = l.topic, who = l.who, about = l.about, size = l.size,
                        stock = l.stock, days = l.days, hint = l.hint, hintFull = l.hintFull, hours = l.hours, workTime = l.workTime
                    };
                    if (copy.key != null) byKey[copy.key] = copy;
                    else if (!seen.Add((int)copy.kind + "|" + copy.text + "|" + copy.chance.ToString(CultureInfo.InvariantCulture))) continue;
                    outList.Add(copy);
                }
            outList.RemoveAll(l => l.sum && Math.Abs(l.amount) < 0.01f);
            return outList.Select((l, i) => (l, i)).OrderBy(x => (int)x.l.kind).ThenBy(x => x.i).Select(x => x.l).ToList();
        }

        /// <summary>The lines of <paramref name="risk"/> that aren't already among <paramref name="plain"/> (by key, else by words).</summary>
        public static List<ImpactLine> Besides(List<ImpactLine> risk, List<ImpactLine> plain)
        {
            var keys = new HashSet<string>(plain.Where(l => l.key != null).Select(l => l.key + "|" + l.Text));
            var texts = new HashSet<string>(plain.Select(l => l.Text));
            return risk.Where(l => l.key != null ? !keys.Contains(l.key + "|" + l.Text) : !texts.Contains(l.Text)).ToList();
        }

        /// <summary>
        /// The short form: the parts in order, joined by "; ", as many as fit in <paramref name="maxChars"/>; when some are
        /// left out, <paramref name="more"/> says so (it counts toward the length). Nothing to say: empty.
        /// </summary>
        public static string Compact(IList<string> parts, int maxChars, string more)
        {
            var kept = new List<string>();
            int len = 0;
            var list = parts?.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).Distinct().ToList() ?? new List<string>();
            for (int i = 0; i < list.Count; i++)
            {
                int add = (kept.Count > 0 ? 2 : 0) + list[i].Length;
                bool last = i == list.Count - 1;
                int tail = last || string.IsNullOrEmpty(more) ? 0 : 2 + more.Length;
                if (kept.Count > 0 && len + add + tail > maxChars)
                {
                    if (!string.IsNullOrEmpty(more)) kept.Add(more);
                    return string.Join("; ", kept);
                }
                kept.Add(list[i]);
                len += add;
            }
            return string.Join("; ", kept);
        }
    }
}
