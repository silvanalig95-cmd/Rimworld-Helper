using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ColonyCouncil
{
    /// <summary>How the letters show what each answer does (the setting "What each answer does").</summary>
    public enum ImpactShow
    {
        /// <summary>A line of flavour under each answer, how it would land without naming a mechanic, and the hints on hover (the default; Dilemmas/ImpactFlavourMath.cs).</summary>
        Flavour,
        /// <summary>A hint in plain words under each answer and on hover, without numbers.</summary>
        Hints,
        /// <summary>Every number: sizes, days, counts and odds.</summary>
        Exact,
        /// <summary>Nothing under the answers: the letters as the game makes them.</summary>
        Off
    }

    /// <summary>What an answer's line is about, for telling it in words (<see cref="ImpactHints"/>).</summary>
    public enum HintTopic
    {
        /// <summary>Its own words, with any numbers taken out.</summary>
        None,
        /// <summary>Something that happens on the map: its name and when (<see cref="ImpactLine.hint"/>), and its hours.</summary>
        Play,
        Mood,
        Trust,
        Standards,
        Certainty,
        Opinion,
        Joy,
        Rest,
        Comfort,
        Strain,
        Skill,
        Inspire,
        Break,
        Goods,
        Silver,
        Goodwill,
        Tension,
        Ways
    }

    /// <summary>
    /// The words hints are made of, by key ("CC_ImpactHint_" in Languages/English/Keyed/Rimfolk_DilemmaImpact.xml): the
    /// game's translations in the mod, the file itself in Tools/Tests. A key may hold a verb's forms as "for one|for
    /// several|after may" ("rises|rise"); the last form stands in for any left out.
    /// </summary>
    public sealed class HintWords
    {
        private readonly Func<string, string> raw;
        private readonly Func<string, bool> has;
        private readonly Dictionary<string, int> counts = new Dictionary<string, int>();

        /// <param name="has">Whether a key exists (for words in numbered variants, "..._1", "..._2"); without it, a key has words when they aren't the key itself.</param>
        public HintWords(Func<string, string> raw, Func<string, bool> has = null)
        {
            this.raw = raw;
            this.has = has;
        }

        public string this[string key] => raw?.Invoke(key) ?? key;

        public bool Has(string key)
        {
            if (has != null) return has(key);
            string v = raw?.Invoke(key);
            return v != null && v != key;
        }

        /// <summary>How many numbered variants a key has: "{key}_1", "{key}_2" and on, up to the first one missing.</summary>
        public int Variants(string key)
        {
            if (counts.TryGetValue(key, out int n)) return n;
            n = 0;
            while (n < 40 && Has(key + "_" + (n + 1))) n++;
            counts[key] = n;
            return n;
        }

        public string F(string key, params string[] args) => Fill(this[key], args);

        /// <summary>One of a key's forms: 0 for one, 1 for several, 2 after "may".</summary>
        public string Form(string key, int form, params string[] args)
        {
            var forms = this[key].Split('|');
            return Fill(forms[Math.Min(form, forms.Length - 1)], args);
        }

        public static string Fill(string template, params string[] args)
        {
            if (template == null) return "";
            for (int i = 0; i < args.Length; i++) template = template.Replace("{" + i + "}", args[i] ?? "");
            return template;
        }
    }

    /// <summary>A row of the hint's tooltip: a heading, the gamble's heading, or a thing the answer does.</summary>
    public sealed class HintRow
    {
        public string text;
        /// <summary>Good for the colony (1), a cost (-1), or neither (0).</summary>
        public int sign;
        public bool heading;
        public bool risk;
    }

    /// <summary>
    /// What an answer does told as a hint in plain words, without a number in it: which way things go and roughly how
    /// far ("a little", "noticeably", "greatly"), who it touches by name or group, what it costs, what happens on the map
    /// and whether it's a gamble. Made from the same lines as the exact list (<see cref="ImpactLine"/>, set by each
    /// effect's Describe), so it stays true to what happens. One line under the answer (whole clauses only, the biggest
    /// things first) and a fuller tooltip grouped by kind. No game types: tested in Tools/Tests (ImpactHintTests).
    /// </summary>
    public static class ImpactHints
    {
        /// <summary>The longest the hint under an answer gets before the smaller things are left to the tooltip.</summary>
        public const int LineChars = 150;

        // ---------------- Sizes ----------------

        /// <summary>A line's size, signed: what adds up (trust, goods, silver) or its own size (a mood's points).</summary>
        public static float Value(ImpactLine l) => l.sum ? l.amount : l.size;

        /// <summary>Where the words change: up to a is small, up to b middling, more is big; below tiny it is left off the short line.</summary>
        private static (float a, float b, float tiny) Steps(HintTopic t)
        {
            switch (t)
            {
                case HintTopic.Mood: return (2.5f, 5.5f, 1.5f);
                case HintTopic.Trust: return (2f, 4f, 1f);
                case HintTopic.Standards:
                case HintTopic.Certainty: return (2f, 5f, 1f);
                case HintTopic.Opinion: return (8f, 20f, 4f);
                case HintTopic.Joy:
                case HintTopic.Rest:
                case HintTopic.Comfort: return (0.1f, 0.2f, 0.04f);
                case HintTopic.Strain: return (3f, 6f, 1.5f);
                case HintTopic.Skill: return (200f, 500f, 80f);
                case HintTopic.Silver: return (100f, 500f, 10f);
                case HintTopic.Goodwill: return (3f, 8f, 1.5f);
                case HintTopic.Tension: return (5f, 15f, 1.5f);
                default: return (-1f, float.MaxValue, 0f);
            }
        }

        /// <summary>0 small, 1 middling, 2 big. Goods by their share of the stores (some, when the stores aren't known).</summary>
        public static int Level(ImpactLine l)
        {
            float v = Math.Abs(Value(l));
            if (l.topic == HintTopic.Goods)
            {
                if (l.stock <= 0f) return 1;
                float share = v / l.stock;
                if (l.sign > 0) return share < 0.25f ? 0 : share < 1f ? 1 : 2;
                return share < 0.2f ? 0 : share < 0.5f ? 1 : 2;
            }
            var (a, b, _) = Steps(l.topic);
            return v <= a ? 0 : v <= b ? 1 : 2;
        }

        /// <summary>Too small to take room in the short line (it stays in the tooltip).</summary>
        public static bool Tiny(ImpactLine l) => l.topic != HintTopic.Goods && Math.Abs(Value(l)) < Steps(l.topic).tiny;

        private static bool Up(ImpactLine l) => l.topic == HintTopic.Goods ? l.sign > 0 : Value(l) > 0f;

        private static int RiskLevel(float p) => p < 0.2f ? 0 : p < 0.5f ? 1 : 2;

        /// <summary>"about an hour", "a few hours", "much of a day", "a day or more".</summary>
        public static string HoursWords(HintWords w, float hours) =>
            w["CC_ImpactHint_Hours_" + (hours <= 1.25f ? 0 : hours <= 4f ? 1 : hours <= 9f ? 2 : 3)];

        /// <summary>"a day", "a few days", "some days", "many days".</summary>
        public static string DaysWords(HintWords w, float days) => w["CC_ImpactHint_Days_" + (days <= 1f ? 0 : days <= 4f ? 1 : days <= 8f ? 2 : 3)];

        /// <summary>When in the day something is set for, as the game's hours read: Night, Evening, Day, Morning, Midday or Afternoon.</summary>
        public static string WhenKind(int from, int to)
        {
            if (from >= 19 && (to < from || to >= 23)) return "Night";
            if (from >= 15 && (to < from || to >= 20)) return "Evening";
            if (from <= 8 && to >= 18 && to > from) return "Day";
            float mid = to >= from ? (from + to) / 2f : (from + to + 24) / 2f % 24f;
            return mid < 11f ? "Morning" : mid < 14f ? "Midday" : mid < 17f ? "Afternoon" : mid < 21f ? "Evening" : "Night";
        }

        /// <summary>Within working hours (it takes people from their work), as <see cref="WhenKind"/> reads it.</summary>
        public static bool InWorkHours(string whenKind) => whenKind == "Day" || whenKind == "Morning" || whenKind == "Midday" || whenKind == "Afternoon";

        // ---------------- Who ----------------

        public struct Who
        {
            /// <summary>Their names or the group, without how many ("everyone", "Alo and Bea", "the devout").</summary>
            public string words;
            /// <summary>Named one by one (no count after them).</summary>
            public bool names;
            public bool plural;
            /// <summary>The whole colony.</summary>
            public bool all;
        }

        private static readonly Regex CountRx = new Regex(@"\s*\((\d+)\)");

        /// <summary>Who an exact line names, without how many: "everyone (10)" is everyone.</summary>
        public static Who WhoOf(string text, HintWords w)
        {
            var r = new Who { words = "", names = true };
            if (string.IsNullOrWhiteSpace(text)) return r;
            r.names = !CountRx.IsMatch(text);
            r.words = Tidy(CountRx.Replace(text, ""));
            string everyone = w["CC_ImpactHint_Everyone"];
            r.all = !r.names && string.Equals(r.words, everyone, StringComparison.OrdinalIgnoreCase);
            bool one = r.words.StartsWith(everyone, StringComparison.OrdinalIgnoreCase);
            r.plural = r.names ? r.words.Contains(", ") || r.words.Contains(" " + w["CC_And"] + " ") : !one;
            return r;
        }

        private sealed class Subject
        {
            public string text;
            public bool plural, quant, all, names, fromAll;
        }

        /// <summary>Who a line is about as the subject of its words: a chance for each of a group reads "some" (of them).</summary>
        private static Subject SubjectOf(ImpactLine l, HintWords w)
        {
            var who = WhoOf(l.who, w);
            var s = new Subject { text = who.words, plural = who.plural, all = who.all, names = who.names };
            if (l.each && l.chance < 1f && !who.names)
            {
                string q = w[l.chance < 0.25f ? "CC_ImpactHint_AFew" : l.chance < 0.6f ? "CC_ImpactHint_Some" : "CC_ImpactHint_Most"];
                s.text = who.all ? q : w.F("CC_ImpactHint_Of", q, who.words);
                s.plural = true;
                s.quant = true;
                s.fromAll = who.all;
                s.all = false;
            }
            return s;
        }

        // ---------------- Words for one line ----------------

        /// <summary>A verb as sure as the line is: "rises", "may rise", "likely rises", "almost surely rises" (a share of a group: "may", or as it is).</summary>
        private static string Sure(HintWords w, float chance, bool quant, string finite, string bare)
        {
            if (quant) return chance < 0.6f ? w.F("CC_ImpactHint_May", bare) : finite;
            if (chance >= 1f) return finite;
            if (chance < 0.4f) return w.F("CC_ImpactHint_May", bare);
            return w.F(chance < 0.85f ? "CC_ImpactHint_Likely" : "CC_ImpactHint_Surely", finite);
        }

        private static int Bucket(float chance, bool quant) => quant ? (chance < 0.6f ? 1 : 0) : chance >= 1f ? 0 : chance < 0.4f ? 1 : chance < 0.85f ? 2 : 3;

        private static string Pred(HintWords w, string key, bool plural, float chance, bool quant, params string[] args) =>
            Sure(w, chance, quant, w.Form(key, plural ? 1 : 0, args), w.Form(key, 2, args));

        private static bool IsNoun(HintTopic t) => t == HintTopic.Mood || t == HintTopic.Trust || t == HintTopic.Standards || t == HintTopic.Certainty;

        private static bool IsFeeling(HintTopic t) => IsNoun(t) || t == HintTopic.Opinion || t == HintTopic.Joy || t == HintTopic.Rest || t == HintTopic.Comfort
                                                      || t == HintTopic.Strain || t == HintTopic.Skill || t == HintTopic.Inspire;

        /// <summary>"spirits" (several: the key ends "|pl"), "trust in the council".</summary>
        private static (string word, bool plural) NounOf(HintWords w, HintTopic t)
        {
            string raw = w["CC_ImpactHint_Noun_" + t];
            bool pl = raw.EndsWith("|pl", StringComparison.Ordinal);
            return (pl ? raw.Substring(0, raw.Length - 3) : raw, pl);
        }

        private sealed class NounPart
        {
            public HintTopic topic;
            public int level;
        }

        /// <summary>
        /// "Alo's spirits rise noticeably and trust in the council a little", "spirits may rise a little for some": one
        /// person's or group's moods, trust, standards or faith going the same way, biggest first, a size said once.
        /// </summary>
        private static string NounClause(HintWords w, Subject s, float chance, bool up, List<NounPart> parts, bool full)
        {
            var groups = parts.OrderByDescending(p => p.level).GroupBy(p => p.level).ToList();
            var pieces = new List<string>();
            for (int i = 0; i < groups.Count; i++)
            {
                var nouns = groups[i].Select(p => NounOf(w, p.topic)).ToList();
                string words = JoinAnd(w, nouns.Select(n => n.word).ToList());
                string scale = w["CC_ImpactHint_Scale_" + groups[i].Key];
                if (i > 0)
                {
                    pieces.Add(words + " " + scale);
                    continue;
                }
                bool plural = nouns.Count > 1 || nouns[0].plural;
                string key = up ? "CC_ImpactHint_Rise" : "CC_ImpactHint_Fall";
                pieces.Add(words + " " + Sure(w, chance, s.quant, w.Form(key, plural ? 1 : 0), w.Form(key, 2)) + " " + scale);
            }
            string text = JoinAnd(w, pieces);
            if (s.names || (full && s.all)) return w.F("CC_ImpactHint_Poss", s.text) + " " + text;
            return s.all ? text : w.F("CC_ImpactHint_For", text, s.text);
        }

        /// <summary>" for a few days (forgiven)": how long a mood lasts and what it is, for the tooltip.</summary>
        private static string Lasting(ImpactLine l, HintWords w, bool label)
        {
            string t = " " + (l.days > 0f ? w.F("CC_ImpactHint_ForDays", DaysWords(w, l.days)) : w["CC_ImpactHint_WhileItLasts"]);
            if (label && !string.IsNullOrWhiteSpace(l.about)) t += " (" + l.about.Trim() + ")";
            return t;
        }

        /// <summary>One line in words: who, which way, how much, how sure. Full (the tooltip): everyone named, how long a mood lasts and what it is.</summary>
        public static string Clause(ImpactLine l, HintWords w, bool full)
        {
            switch (l.topic)
            {
                case HintTopic.Mood:
                case HintTopic.Trust:
                case HintTopic.Standards:
                case HintTopic.Certainty:
                {
                    var s = SubjectOf(l, w);
                    if (string.IsNullOrEmpty(s.text)) return null;
                    string text = NounClause(w, s, l.chance, Up(l), new List<NounPart> { new NounPart { topic = l.topic, level = Level(l) } }, full);
                    return full && l.topic == HintTopic.Mood ? text + Lasting(l, w, true) : text;
                }
                case HintTopic.Opinion:
                {
                    var s = SubjectOf(l, w);
                    if (string.IsNullOrEmpty(s.text)) return null;
                    string text = s.text + " " + Pred(w, Up(l) ? "CC_ImpactHint_ThinkBetter" : "CC_ImpactHint_ThinkWorse", s.plural, l.chance, s.quant,
                        w["CC_ImpactHint_Much_" + Level(l)], l.about ?? "");
                    return full ? text + Lasting(l, w, false) : text;
                }
                case HintTopic.Joy:
                case HintTopic.Rest:
                case HintTopic.Comfort:
                case HintTopic.Strain:
                case HintTopic.Skill:
                case HintTopic.Inspire:
                case HintTopic.Break:
                {
                    var s = SubjectOf(l, w);
                    if (string.IsNullOrEmpty(s.text)) return null;
                    return s.text + " " + VerbPred(l, s, w, full);
                }
                case HintTopic.Goods:
                    if (string.IsNullOrWhiteSpace(l.about)) return null;
                    return Up(l)
                        ? Pred(w, "CC_ImpactHint_Brings", false, l.chance, false, w["CC_ImpactHint_Amount_" + Level(l)], l.about)
                        : Pred(w, "CC_ImpactHint_Uses", false, l.chance, false, w["CC_ImpactHint_Share_" + Level(l)], l.about);
                case HintTopic.Silver:
                    return Pred(w, Up(l) ? "CC_ImpactHint_BringsSilver" : "CC_ImpactHint_CostsSilver", false, l.chance, false, w["CC_ImpactHint_Share_" + Level(l)]);
                case HintTopic.Goodwill:
                    if (string.IsNullOrWhiteSpace(l.about)) return null;
                    return w.F("CC_ImpactHint_Relations", l.about) + " "
                           + Pred(w, Up(l) ? "CC_ImpactHint_Improve" : "CC_ImpactHint_Worsen", true, l.chance, false, w["CC_ImpactHint_Scale_" + Level(l)]);
                case HintTopic.Tension:
                    return w["CC_ImpactHint_TheCamp"] + " " + (Up(l)
                        ? Pred(w, "CC_ImpactHint_TenseUp", false, l.chance, false, w["CC_ImpactHint_Much_" + Level(l)])
                        : Pred(w, "CC_ImpactHint_TenseDown", false, l.chance, false, w["CC_ImpactHint_Scale_" + Level(l)]));
                case HintTopic.Ways:
                    if (full) return Unnumber(l.Text, w);
                    return string.IsNullOrWhiteSpace(l.about) ? null : w.F("CC_ImpactHint_Ways", l.about);
                case HintTopic.Play:
                    return full ? Free(l, w, true) : LeadOf(l, w);
                default:
                    return Free(l, w, full);
            }
        }

        /// <summary>What happens to one person or group, after their name: "gets some recreation", "may learn a little cooking".</summary>
        private static string VerbPred(ImpactLine l, Subject s, HintWords w, bool full)
        {
            int level = Level(l);
            switch (l.topic)
            {
                case HintTopic.Opinion:
                    return Pred(w, Up(l) ? "CC_ImpactHint_ThinkBetter" : "CC_ImpactHint_ThinkWorse", s.plural, l.chance, s.quant, w["CC_ImpactHint_Much_" + level], l.about ?? "");
                case HintTopic.Strain:
                    return Up(l)
                        ? Pred(w, "CC_ImpactHint_StrainUp", s.plural, l.chance, s.quant, w["CC_ImpactHint_Strain_" + level])
                        : Pred(w, "CC_ImpactHint_StrainDown", s.plural, l.chance, s.quant, w["CC_ImpactHint_Much_" + level]);
                case HintTopic.Skill:
                    return Pred(w, "CC_ImpactHint_Skill", s.plural, l.chance, s.quant, w["CC_ImpactHint_Share_" + level], l.about ?? "");
                case HintTopic.Inspire:
                    return Pred(w, "CC_ImpactHint_Inspire", s.plural, l.chance, s.quant);
                case HintTopic.Break:
                    string b = Pred(w, "CC_ImpactHint_Break", s.plural, l.chance, s.quant);
                    return full && !string.IsNullOrWhiteSpace(l.about) ? b + " (" + l.about.Trim() + ")" : b;
                default:
                    return Pred(w, "CC_ImpactHint_" + l.topic, s.plural, l.chance, s.quant, w["CC_ImpactHint_Amount_" + level]);
            }
        }

        /// <summary>A line in its own words with the numbers taken out, as sure as it is ("perhaps Alo and Bea fight").</summary>
        private static string Free(ImpactLine l, HintWords w, bool full)
        {
            string t = full ? l.hintFull ?? Unnumber(l.Text, w) : l.hint ?? Unnumber(l.Brief, w);
            if (string.IsNullOrWhiteSpace(t)) return null;
            if (l.chance >= 1f) return t;
            return w.F(l.chance < 0.4f ? "CC_ImpactHint_Perhaps" : l.chance < 0.85f ? "CC_ImpactHint_LikelyThat" : "CC_ImpactHint_SurelyThat", LowerSmall(t, w));
        }

        /// <summary>What happens on the map, to start the hint with: "The high watch in the evening, with everyone".</summary>
        private static string LeadOf(ImpactLine l, HintWords w)
        {
            string t = l.hint ?? Unnumber(l.Brief, w);
            if (string.IsNullOrWhiteSpace(t)) return null;
            if (!string.IsNullOrWhiteSpace(l.who))
            {
                string who = WhoOf(l.who, w).words;
                if (who.Length > 0) t = w.F("CC_ImpactHint_With", t, who);
            }
            return l.chance >= 1f ? t : w.F(l.chance < 0.4f ? "CC_ImpactHint_Perhaps" : l.chance < 0.85f ? "CC_ImpactHint_LikelyThat" : "CC_ImpactHint_SurelyThat", LowerSmall(t, w));
        }

        // ---------------- The line under an answer ----------------

        private sealed class Part
        {
            public int order, prio, seq;
            public string text;
        }

        // The order things are told in, and which go first when there isn't room for all.
        private const int OPeople = 0, OFeelings = 1, OHealth = 2, OCosts = 3, OPeoples = 4, OBesides = 5, OMap = 6, OGamble = 7, OWays = 8;

        private static int OrderOf(ImpactLine l)
        {
            switch (l.topic)
            {
                case HintTopic.Break: return OHealth;
                case HintTopic.Goods:
                case HintTopic.Silver: return OCosts;
                case HintTopic.Goodwill:
                case HintTopic.Tension: return OPeoples;
                case HintTopic.Ways: return OWays;
                case HintTopic.Play: return OMap;
            }
            switch (l.kind)
            {
                case ImpactKind.Map: return OMap;
                case ImpactKind.People: return OPeople;
                case ImpactKind.Health: return OHealth;
                case ImpactKind.Goods: return OCosts;
                case ImpactKind.Goodwill: return OPeoples;
                case ImpactKind.Other: return OBesides;
                case ImpactKind.Ways: return OWays;
                default: return OFeelings;
            }
        }

        private static int PrioOf(int order)
        {
            switch (order)
            {
                case OPeople:
                case OGamble: return 1;
                case OFeelings:
                case OHealth: return 2;
                case OCosts: return 3;
                case OPeoples: return 4;
                case OWays: return 6;
                default: return 5;
            }
        }

        /// <summary>
        /// The hint under an answer, one line: what happens on the map first, then who feels what, what it costs, the
        /// gamble and the colony's ways, cut at a whole clause (the biggest things kept), never a number. Null: nothing to say.
        /// </summary>
        public static string Line(OptionImpact r, HintWords w, int maxChars = LineChars)
        {
            if (r == null || r.Empty) return null;
            var parts = new List<Part>();
            var tiny = new List<Part>();
            int seq = 0;
            void Add(List<Part> to, int order, string text)
            {
                text = Clean(text, w);
                if (!string.IsNullOrWhiteSpace(text)) to.Add(new Part { order = order, prio = PrioOf(order), seq = seq++, text = text });
            }

            string lead = null;
            ImpactLine leadLine = null;
            foreach (var l in r.lines)
            {
                if (l.kind != ImpactKind.Map) continue;
                lead = Clean(LeadOf(l, w), w);
                if (string.IsNullOrWhiteSpace(lead)) continue;
                leadLine = l;
                break;
            }
            if (leadLine == null) lead = null;
            else if (leadLine.workTime && leadLine.hours > 0f) Add(parts, OCosts, w.F("CC_ImpactHint_WorkTime", HoursWords(w, leadLine.hours)));

            foreach (var t in FeelingParts(r.lines.Where(l => IsFeeling(l.topic) && !Tiny(l)).ToList(), w)) Add(parts, OFeelings, t);
            foreach (var l in r.lines)
            {
                if (l == leadLine) continue;
                if (IsFeeling(l.topic))
                {
                    if (Tiny(l)) Add(tiny, OFeelings, Clause(l, w, false));
                    continue;
                }
                Add(l.topic != HintTopic.None && l.topic != HintTopic.Play && l.topic != HintTopic.Ways && Tiny(l) ? tiny : parts, OrderOf(l), Clause(l, w, false));
            }
            if (r.riskChance > 0f)
            {
                string head = w["CC_ImpactHint_Risk_" + RiskLevel(r.riskChance)];
                string what = r.risk.Select(l => Clause(l, w, false)).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
                Add(parts, OGamble, what == null ? head : w.F("CC_ImpactHint_RiskWhat", head, what));
            }

            int room = maxChars - 1 - (lead != null ? lead.Length + 2 : 0);
            var chosen = Fit(parts, room, lead != null);
            if (chosen.Count == 0 && lead == null) chosen = Fit(tiny, room, false);
            string body = string.Join("; ", chosen.OrderBy(p => p.order).ThenBy(p => p.seq).Select(p => p.text));
            string text = lead == null ? body : body.Length == 0 ? lead : lead + ": " + body;
            if (string.IsNullOrWhiteSpace(text)) return null;
            return Cap(text.TrimEnd('.', ' ')) + ".";
        }

        /// <summary>The parts that fit, the first ones first (the first always, unless the hint has a start of its own).</summary>
        private static List<Part> Fit(List<Part> parts, int room, bool hasLead)
        {
            var chosen = new List<Part>();
            int used = 0;
            foreach (var p in parts.OrderBy(p => p.prio).ThenBy(p => p.order).ThenBy(p => p.seq))
            {
                if (chosen.Any(c => c.text == p.text)) continue;
                int add = (chosen.Count > 0 ? 2 : 0) + p.text.Length;
                if ((chosen.Count > 0 || hasLead) && used + add > room) continue;
                chosen.Add(p);
                used += add;
            }
            return chosen;
        }

        private sealed class NounEntry
        {
            public Subject s;
            public bool up;
            public float chance;
            public readonly List<NounPart> parts = new List<NounPart>();
            public string especially;
            public bool folded;
        }

        /// <summary>
        /// Who feels what, a clause per person or group: their moods, trust, standards and faith together when they go
        /// the same way ("Alo's spirits and trust in the council fall noticeably"), then what else happens to them ("everyone
        /// gets a little recreation"). One person who feels it more than everyone is folded in ("..., especially Alo's").
        /// </summary>
        private static List<string> FeelingParts(List<ImpactLine> lines, HintWords w)
        {
            var order = new List<string>();
            var subjects = new Dictionary<string, Subject>();
            var nouns = new Dictionary<string, List<NounEntry>>();
            var verbs = new Dictionary<string, List<string>>();
            foreach (var l in lines)
            {
                var s = SubjectOf(l, w);
                if (string.IsNullOrEmpty(s.text)) continue;
                string key = s.text;
                if (!subjects.ContainsKey(key))
                {
                    subjects[key] = s;
                    nouns[key] = new List<NounEntry>();
                    verbs[key] = new List<string>();
                    order.Add(key);
                }
                if (IsNoun(l.topic))
                {
                    bool up = Up(l);
                    var e = nouns[key].FirstOrDefault(x => x.up == up && Bucket(x.chance, x.s.quant) == Bucket(l.chance, s.quant));
                    if (e == null) nouns[key].Add(e = new NounEntry { s = s, up = up, chance = l.chance });
                    if (e.parts.All(p => p.topic != l.topic)) e.parts.Add(new NounPart { topic = l.topic, level = Level(l) });
                }
                else
                {
                    string v = VerbPred(l, s, w, false);
                    if (!verbs[key].Contains(v)) verbs[key].Add(v);
                }
            }
            // One person who feels it more than everyone around them is said with them.
            var all = order.SelectMany(k => nouns[k]).ToList();
            foreach (var g in all.Where(e => e.s.all || e.s.fromAll))
                foreach (var n in all)
                {
                    if (n.folded || n == g || !n.s.names || n.s.plural || n.up != g.up) continue;
                    if (!n.parts.All(p => g.parts.Any(q => q.topic == p.topic))) continue;
                    if (n.parts.Max(p => p.level) < g.parts.Max(p => p.level)) continue;
                    g.especially = w.F("CC_ImpactHint_Poss", n.s.text);
                    n.folded = true;
                    break;
                }
            var outList = new List<string>();
            foreach (var k in order)
            {
                foreach (var e in nouns[k].Where(e => !e.folded))
                {
                    string t = NounClause(w, e.s, e.chance, e.up, e.parts, false);
                    if (e.especially != null) t = w.F("CC_ImpactHint_Especially", t, e.especially);
                    outList.Add(t);
                }
                if (verbs[k].Count > 0) outList.Add(subjects[k].text + " " + JoinAnd(w, verbs[k]));
            }
            return outList;
        }

        // ---------------- The tooltip ----------------

        /// <summary>One line in full words for the tooltip, or null.</summary>
        public static string Full(ImpactLine l, HintWords w)
        {
            string t = Clause(l, w, true);
            if (t != null && l.topic == HintTopic.Play && l.workTime && l.hours > 0f) t = w.F("CC_ImpactHint_PlayWork", t, HoursWords(w, l.hours));
            t = Clean(t, w);
            return string.IsNullOrWhiteSpace(t) ? null : Cap(t);
        }

        /// <summary>The tooltip in words: the things it does grouped by kind under a heading each, then the gamble, without a number.</summary>
        public static List<HintRow> Rows(OptionImpact r, HintWords w)
        {
            var rows = new List<HintRow>();
            if (r == null || r.Empty)
            {
                rows.Add(new HintRow { text = w["CC_Impact_Nothing"] });
                return rows;
            }
            rows.Add(new HintRow { text = w["CC_Impact_Header"], heading = true });
            ImpactKind? kind = null;
            foreach (var l in r.lines)
            {
                string t = Full(l, w);
                if (t == null) continue;
                if (kind != l.kind)
                {
                    kind = l.kind;
                    rows.Add(new HintRow { text = w["CC_Impact_Kind_" + l.kind], heading = true });
                }
                rows.Add(new HintRow { text = t, sign = l.Sign });
            }
            if (r.riskChance > 0f)
            {
                string note = !string.IsNullOrWhiteSpace(r.riskHelper) && !string.IsNullOrWhiteSpace(r.riskSkill) ? w.F("CC_ImpactHint_RiskLowered", r.riskHelper, r.riskSkill)
                    : !string.IsNullOrWhiteSpace(r.riskSkill) ? w.F("CC_ImpactHint_RiskSkill", r.riskSkill) : "";
                string head = w.F(r.riskReplaces ? "CC_ImpactHint_RiskInstead" : "CC_ImpactHint_RiskAlso", w["CC_ImpactHint_RiskHead_" + RiskLevel(r.riskChance)], note);
                rows.Add(new HintRow { text = Clean(head, w), risk = true });
                int before = rows.Count;
                foreach (var l in r.risk)
                {
                    string t = Full(l, w);
                    if (t != null) rows.Add(new HintRow { text = t, sign = l.Sign });
                }
                if (rows.Count == before) rows.Add(new HintRow { text = w[r.riskReplaces ? "CC_Impact_RiskNothingAtAll" : "CC_Impact_RiskNothingMore"] });
            }
            return rows;
        }

        /// <summary>The tooltip as plain text, a row a line.</summary>
        public static string Plain(OptionImpact r, HintWords w) => string.Join("\n", Rows(r, w).Select(x => x.text));

        // ---------------- Numbers into words ----------------

        // These read the English lines of Rimfolk_DilemmaImpact.xml; anything with a digit they miss is dropped by Clean.
        private static readonly Regex OddsRx = new Regex(@"\b(?:about )?\d+ in \d+(?: each)?:?\s*");
        private static readonly Regex AtHoursRx = new Regex(@"\bbetween (\d+)h and (\d+)h\b");
        private static readonly Regex SpanRx = new Regex(@"\b(?:(?:about|up to) )?(\d+(?:\.\d+)?)(?: to (\d+(?:\.\d+)?))? (hours?|days?|nights?)(?:\(s\))?(?![A-Za-z])");
        private static readonly Regex SilverRx = new Regex(@"\b(?:about )?(\d+(?:\.\d+)?)(?: to (\d+(?:\.\d+)?))? silver\b");
        private static readonly Regex TimesRx = new Regex(@"\b(\d+(?:\.\d+)?) times as fast\b");
        private static readonly Regex FactorRx = new Regex(@"(?<![\w.])x(\d+(?:\.\d+)?)\b");
        private static readonly Regex ToNumberRx = new Regex(@"\s+to [+-]?\d+(?:\.\d+)?(?=$|[,;:.)])");
        private static readonly Regex SignedRx = new Regex(@"(?<![\w.%])([+-])(\d+(?:\.\d+)?)%?");
        private static readonly Regex ShareOfRx = new Regex(@"\b(?:about )?\d+(?:\.\d+)?% of\b");
        private static readonly Regex PercentRx = new Regex(@"\s*\b(?:by )?\d+(?:\.\d+)?%");
        private static readonly Regex UpToRx = new Regex(@"\b([Uu])p to (\d+)\b");
        private static readonly Regex OfRx = new Regex(@"\b(?:about )?(\d+) of\b");
        private static readonly Regex CountWordRx = new Regex(@"\b(\d+(?:\.\d+)?)(?: to (\d+(?:\.\d+)?))? (?=[^\W\d_])");
        private static readonly Regex OnePluralRx = new Regex(@"\b(\S+?)\(s\)");
        private static readonly Regex DigitParenRx = new Regex(@"\s*\([^()]*\d[^()]*\)");
        private static readonly Regex DigitTokenRx = new Regex(@"\S*\d\S*");

        private static float Num(Group a, Group b) =>
            float.Parse(b.Success ? b.Value : a.Value, NumberStyles.Float, CultureInfo.InvariantCulture);

        /// <summary>
        /// A line's own words without numbers: counts in brackets dropped, hours and days as "a few hours" or "some days",
        /// silver as "some silver", "+10%" as "up", other counts as "one", "a few" or "some"; whatever still has a digit goes.
        /// </summary>
        public static string Unnumber(string s, HintWords w)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            if (!s.Any(char.IsDigit)) return s;
            s = CountRx.Replace(s, "");
            s = OddsRx.Replace(s, "");
            s = AtHoursRx.Replace(s, m => w["CC_ImpactHint_When_" + WhenKind(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture))]);
            s = SpanRx.Replace(s, m =>
            {
                float n = Num(m.Groups[1], m.Groups[2]);
                string unit = m.Groups[3].Value;
                if (unit.StartsWith("hour", StringComparison.Ordinal)) return HoursWords(w, n);
                if (unit.StartsWith("day", StringComparison.Ordinal)) return DaysWords(w, n);
                return w["CC_ImpactHint_Nights_" + (n <= 1f ? 0 : 1)];
            });
            s = SilverRx.Replace(s, m =>
            {
                float n = Num(m.Groups[1], m.Groups[2]);
                return w["CC_ImpactHint_Silver_" + (n <= 100f ? 0 : n <= 500f ? 1 : 2)];
            });
            s = TimesRx.Replace(s, m => w[Num(m.Groups[1], m.Groups[2]) >= 1f ? "CC_ImpactHint_Faster" : "CC_ImpactHint_Slower"]);
            s = FactorRx.Replace(s, m => w[Num(m.Groups[1], m.Groups[2]) >= 1f ? "CC_ImpactHint_Up" : "CC_ImpactHint_Down"]);
            s = ToNumberRx.Replace(s, "");
            s = SignedRx.Replace(s, m => w[m.Groups[1].Value == "+" ? "CC_ImpactHint_Up" : "CC_ImpactHint_Down"]);
            s = ShareOfRx.Replace(s, w["CC_ImpactHint_Count_2"] + " " + w["CC_ImpactHint_OfWord"]);
            s = PercentRx.Replace(s, "");
            s = UpToRx.Replace(s, m =>
            {
                string q = w[m.Groups[2].Value == "1" ? "CC_ImpactHint_Count_1" : "CC_ImpactHint_Count_2"];
                return m.Groups[1].Value == "U" ? Cap(q) : q;
            });
            s = OfRx.Replace(s, m => w[m.Groups[1].Value == "1" ? "CC_ImpactHint_Count_1" : "CC_ImpactHint_Count_2"] + " " + w["CC_ImpactHint_OfWord"]);
            s = CountWordRx.Replace(s, m =>
            {
                float n = Num(m.Groups[1], m.Groups[2]);
                return w[n <= 1f ? "CC_ImpactHint_Count_1" : n <= 4f ? "CC_ImpactHint_Count_Few" : "CC_ImpactHint_Count_2"] + " ";
            });
            // "animal(s)": one animal, else animals.
            var one = new Regex(@"\b" + Regex.Escape(w["CC_ImpactHint_Count_1"]) + @"\b");
            string was = s;
            s = OnePluralRx.Replace(s, m =>
            {
                int from = Math.Max(0, m.Index - 24);
                return m.Groups[1].Value + (one.IsMatch(was.Substring(from, m.Index - from)) ? "" : "s");
            });
            s = DigitParenRx.Replace(s, "");
            s = DigitTokenRx.Replace(s, "");
            return Tidy(s);
        }

        /// <summary>The last word on a hint: no digit, no exclamation mark, no em dash.</summary>
        public static string Clean(string s, HintWords w)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            if (s.IndexOf('!') >= 0 || s.IndexOf('—') >= 0) s = Regex.Replace(s.Replace("—", ", ").Replace("!", ""), @" {2,}", " ").Trim();
            return s.Any(char.IsDigit) ? Unnumber(s, w) : s;
        }

        private static string Tidy(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = Regex.Replace(s, @"\(\s*\)", "");
            s = Regex.Replace(s, @"\s+([,;:.)])", "$1");
            s = Regex.Replace(s, @"([,;:])(\s*[,;:])+", "$1");
            s = Regex.Replace(s, @"\(\s+", "(");
            s = Regex.Replace(s, @"\s{2,}", " ");
            return s.Trim().Trim(',', ';', ':').Trim();
        }

        // ---------------- Small words ----------------

        private static string JoinAnd(HintWords w, IList<string> parts)
        {
            if (parts == null || parts.Count == 0) return "";
            if (parts.Count == 1) return parts[0];
            return string.Join(", ", parts.Take(parts.Count - 1)) + " " + w["CC_And"] + " " + parts[parts.Count - 1];
        }

        private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        /// <summary>"The camp..." after "perhaps" reads "perhaps the camp...": only the small words, never a name.</summary>
        private static string LowerSmall(string s, HintWords w)
        {
            int sp = s.IndexOf(' ');
            string first = sp < 0 ? s : s.Substring(0, sp);
            var small = w["CC_ImpactHint_SmallWords"].Split(' ');
            return small.Any(x => string.Equals(x, first, StringComparison.OrdinalIgnoreCase)) ? char.ToLowerInvariant(s[0]) + s.Substring(1) : s;
        }
    }
}
