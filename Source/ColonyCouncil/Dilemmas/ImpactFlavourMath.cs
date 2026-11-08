using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ColonyCouncil
{
    /// <summary>What a line is, for the flavour line, where its topic doesn't say (set by the effect that describes it).</summary>
    public enum FlavourCue
    {
        None,
        /// <summary>Two people come to blows: <see cref="ImpactLine.who"/> and <see cref="ImpactLine.with"/>.</summary>
        Fight,
        /// <summary>Two people grow closer, or a rivalry between them eases.</summary>
        Bond,
        /// <summary>Two people grow apart, or a rivalry between them grows.</summary>
        Rift,
        /// <summary>Someone joins the colony.</summary>
        Joins,
        /// <summary>Someone leaves the colony for good (<see cref="ImpactLine.who"/>).</summary>
        Leaves,
        /// <summary>A people attacks later (<see cref="ImpactLine.about"/>).</summary>
        Raid,
        /// <summary>A custom kept, or a step toward one (<see cref="ImpactLine.about"/>).</summary>
        CustomKept,
        /// <summary>A custom or tradition broken (<see cref="ImpactLine.about"/>).</summary>
        CustomBroken
    }

    /// <summary>
    /// The kinds of thing a flavour line says. Each is a family of words in Languages/English/Keyed/Rimfolk_DilemmaImpact.xml,
    /// "CC_ImpactFlavour_[kind]_1", "_2" and on, with "_Many_" for a group where a family has words of its own for one.
    /// </summary>
    public enum FlavourKind
    {
        MoodUp,
        MoodUpBig,
        MoodDown,
        MoodDownBig,
        TrustUp,
        TrustDown,
        Mixed,
        ThinkBetter,
        ThinkWorse,
        Bond,
        Rift,
        Fight,
        Hurt,
        Break,
        Raid,
        Gamble,
        GambleBig,
        GambleOn,
        Fizzle,
        Cost,
        CostBig,
        Gain,
        Good,
        Bad,
        Work,
        Gather,
        MapDone,
        Lean,
        ManyOverOne,
        OneOverMany,
        NeighboursUp,
        NeighboursDown,
        CampEasier,
        CampUneasy,
        Joins,
        Leaves,
        CustomKept,
        CustomBroken,
        HealthUp,
        Rested,
        Learns,
        Inspired,
        Worn,
        Eased,
        FaithUp,
        FaithDown,
        ExpectMore,
        ExpectLess
    }

    /// <summary>One thing the flavour line could say about an answer, and how much it matters.</summary>
    public sealed class FlavourBeat
    {
        public FlavourKind kind;
        /// <summary>About a group, not people by name (the "_Many_" words, where the family has them).</summary>
        public bool many;
        /// <summary>What goes in the words: {0} who it is about (or the thing, people or way), {1} the other one.</summary>
        public string a, b;
        public float weight;
        /// <summary>Which way it goes for those it names: 1 good, -1 bad, 0 neither or both.</summary>
        public int sign;
        /// <summary>It may not happen: "may" for "would", "may not" for "won't".</summary>
        public bool maybe;
        /// <summary>Someone may be hurt (a fight, a breakdown, an injury, an attack): always said, the harm rule's "shown".</summary>
        public bool harm;
        /// <summary>A real gamble (a chance of going wrong worth telling): said, beside the biggest thing.</summary>
        public bool gamble;
        /// <summary>For two things said in one (the many against one): the named person's own beat, when only one fits.</summary>
        public FlavourBeat single;
        /// <summary>The beats a two-in-one stands for.</summary>
        public readonly List<FlavourBeat> parts = new List<FlavourBeat>();
        /// <summary>The people it names, so the line doesn't say the same person twice when it needn't.</summary>
        public readonly List<string> names = new List<string>();
        public int order;

        public bool Two => single != null;
    }

    /// <summary>The flavour line for an answer, what it said, and the words it was made from.</summary>
    public sealed class FlavourLine
    {
        public string text;
        public readonly List<FlavourBeat> said = new List<FlavourBeat>();
        /// <summary>The Keyed words each part was made from ("CC_ImpactFlavour_MoodUp_3").</summary>
        public readonly List<string> keys = new List<string>();
    }

    /// <summary>
    /// What an answer does, told as one short line of flavour under it ("It would mean a lot to Avila; Guido won't forget
    /// it."): how it would land, without naming spirits, trust, opinion, a size or any mechanic, and never against the
    /// way things go. Made from the same lines as the hints and the numbers (<see cref="ImpactLine"/>). The biggest thing
    /// is said, and one more beside it: a fight, a breakdown, an injury or an attack always (the harm rule), else a real
    /// gamble, else the next biggest about someone else. Its words are picked from many in Keyed, the same for an answer
    /// each time its letter opens and different between the answers of one letter. No game types: tested in Tools/Tests
    /// (ImpactFlavourTests).
    /// </summary>
    public static class ImpactFlavourMath
    {
        /// <summary>The longest the line gets: about a line under an answer.</summary>
        public const int LineChars = 90;

        public const string Prefix = "CC_ImpactFlavour_";

        /// <summary>A gamble at least this likely to go wrong is always said.</summary>
        public const float RealGamble = 0.15f;

        // ---------------- The families ----------------

        /// <summary>The families with words of their own for a group ("_Many_").</summary>
        public static bool HasMany(FlavourKind k) =>
            k == FlavourKind.MoodUp || k == FlavourKind.MoodUpBig || k == FlavourKind.MoodDown || k == FlavourKind.MoodDownBig || k == FlavourKind.TrustUp
            || k == FlavourKind.TrustDown;

        /// <summary>The Keyed key a beat's words are numbered under.</summary>
        public static string KeyOf(FlavourKind k, bool many) => Prefix + k + (many && HasMany(k) ? "_Many" : "");

        public static string KeyOf(FlavourBeat b) => KeyOf(b.kind, b.many);

        /// <summary>Which way a family's words go: 1 good for those named, -1 bad, 0 neither (a gamble, the map, the colony's ways, both at once).</summary>
        public static int Direction(FlavourKind k)
        {
            switch (k)
            {
                case FlavourKind.MoodUp:
                case FlavourKind.MoodUpBig:
                case FlavourKind.TrustUp:
                case FlavourKind.ThinkBetter:
                case FlavourKind.Bond:
                case FlavourKind.Gain:
                case FlavourKind.Good:
                case FlavourKind.NeighboursUp:
                case FlavourKind.CampEasier:
                case FlavourKind.Joins:
                case FlavourKind.CustomKept:
                case FlavourKind.HealthUp:
                case FlavourKind.Rested:
                case FlavourKind.Learns:
                case FlavourKind.Inspired:
                case FlavourKind.Eased:
                case FlavourKind.FaithUp:
                case FlavourKind.ExpectLess:
                    return 1;
                case FlavourKind.MoodDown:
                case FlavourKind.MoodDownBig:
                case FlavourKind.TrustDown:
                case FlavourKind.ThinkWorse:
                case FlavourKind.Rift:
                case FlavourKind.Fight:
                case FlavourKind.Hurt:
                case FlavourKind.Break:
                case FlavourKind.Raid:
                case FlavourKind.GambleOn:
                case FlavourKind.Cost:
                case FlavourKind.CostBig:
                case FlavourKind.Bad:
                case FlavourKind.Work:
                case FlavourKind.NeighboursDown:
                case FlavourKind.CampUneasy:
                case FlavourKind.Leaves:
                case FlavourKind.CustomBroken:
                case FlavourKind.Worn:
                case FlavourKind.FaithDown:
                case FlavourKind.ExpectMore:
                    return -1;
                default:
                    return 0;
            }
        }

        /// <summary>The families that tell a gamble.</summary>
        public static bool IsGamble(FlavourKind k) => k == FlavourKind.Gamble || k == FlavourKind.GambleBig || k == FlavourKind.GambleOn || k == FlavourKind.Fizzle;

        private static bool IsPair(FlavourKind k) =>
            k == FlavourKind.ThinkBetter || k == FlavourKind.ThinkWorse || k == FlavourKind.Bond || k == FlavourKind.Rift || k == FlavourKind.Fight;

        private static bool IsPerson(FlavourKind k) =>
            k == FlavourKind.MoodUp || k == FlavourKind.MoodUpBig || k == FlavourKind.MoodDown || k == FlavourKind.MoodDownBig || k == FlavourKind.TrustUp
            || k == FlavourKind.TrustDown || k == FlavourKind.Mixed || k == FlavourKind.Rested || k == FlavourKind.Learns || k == FlavourKind.Inspired
            || k == FlavourKind.Worn || k == FlavourKind.Eased || k == FlavourKind.FaithUp || k == FlavourKind.FaithDown || k == FlavourKind.ExpectMore
            || k == FlavourKind.ExpectLess;

        private static bool IsMap(FlavourKind k) => k == FlavourKind.Work || k == FlavourKind.Gather || k == FlavourKind.MapDone;

        /// <summary>What it costs or brings, what happens on the map, the colony's ways: said after the people when they come second.</summary>
        private static bool IsThing(FlavourKind k) =>
            IsMap(k) || k == FlavourKind.Cost || k == FlavourKind.CostBig || k == FlavourKind.Gain || k == FlavourKind.Good || k == FlavourKind.Bad || k == FlavourKind.Lean;

        // ---------------- Who ----------------

        private sealed class Who
        {
            public string key;
            public string words;
            public bool many, all, quant;
            public readonly List<string> names = new List<string>();
        }

        /// <summary>"Alo, Bea and Cy": each name.</summary>
        public static List<string> SplitNames(string words, HintWords w)
        {
            var outList = new List<string>();
            if (string.IsNullOrWhiteSpace(words)) return outList;
            foreach (var part in words.Split(new[] { ", ", " " + w["CC_And"] + " " }, StringSplitOptions.RemoveEmptyEntries))
                if (part.Trim().Length > 0) outList.Add(part.Trim());
            return outList;
        }

        private static string JoinAnd(HintWords w, IList<string> parts)
        {
            if (parts == null || parts.Count == 0) return "";
            if (parts.Count == 1) return parts[0];
            return string.Join(", ", parts.Take(parts.Count - 1)) + " " + w["CC_And"] + " " + parts[parts.Count - 1];
        }

        /// <summary>
        /// Who a line is about, as the flavour names them: each name on its own (<paramref name="split"/>) or together; a
        /// group without how many or who is left out ("everyone else, but not the ascetic" is everyone else); a chance for
        /// each of a group as "some", "a few" or "most".
        /// </summary>
        private static List<Who> Subjects(ImpactLine l, HintWords w, bool split)
        {
            var outList = new List<Who>();
            var who = ImpactHints.WhoOf(l.who, w);
            if (string.IsNullOrEmpty(who.words)) return outList;
            if (who.names)
            {
                var names = SplitNames(who.words, w);
                if (names.Count == 0) return outList;
                if (split)
                    foreach (var n in names)
                    {
                        var one = new Who { key = "n|" + n, words = n };
                        one.names.Add(n);
                        outList.Add(one);
                    }
                else
                {
                    var all = new Who { key = "n|" + string.Join("|", names), words = JoinAnd(w, names) };
                    all.names.AddRange(names);
                    outList.Add(all);
                }
                return outList;
            }
            string words = who.words;
            int cut = words.IndexOf(", ", StringComparison.Ordinal);
            if (cut > 0) words = words.Substring(0, cut);
            var s = new Who { many = true, all = who.all, words = words };
            if (l.each && l.chance < 1f)
            {
                string q = w[l.chance < 0.25f ? "CC_ImpactHint_AFew" : l.chance < 0.6f ? "CC_ImpactHint_Some" : "CC_ImpactHint_Most"];
                bool everyone = words.StartsWith(w["CC_ImpactHint_Everyone"], StringComparison.OrdinalIgnoreCase);
                s.words = everyone ? q : w.F("CC_ImpactHint_Of", q, words);
                s.quant = true;
                s.all = false;
            }
            // A share of a group is a share for each effect: "some" glad and "some" put out are different people.
            s.key = "g|" + s.words + (s.quant ? "|" + Math.Round(l.chance, 2) : "");
            outList.Add(s);
            return outList;
        }

        // ---------------- Sizes ----------------

        private static bool Up(ImpactLine l) => l.topic == HintTopic.Goods ? l.sign > 0 : ImpactHints.Value(l) > 0f;

        /// <summary>How much a line counts by how sure it is; a share of a group already says it in its words.</summary>
        private static float Sure(ImpactLine l, bool quant) => quant || l.chance >= 1f ? 1f : 0.4f + 0.6f * Math.Max(0f, l.chance);

        /// <summary>A size as a weight: a little 1, noticeably 2, greatly 3; too small for the hint's line, half.</summary>
        private static float Size(ImpactLine l) => (ImpactHints.Level(l) + 1) * (ImpactHints.Tiny(l) ? 0.5f : 1f);

        // ---------------- What could be said ----------------

        private sealed class Side
        {
            public float weight, mood, trust, chance;
            public bool big = true;
            public int moods;
        }

        private sealed class Tally
        {
            public Who who;
            public readonly Side good = new Side(), bad = new Side();
        }

        /// <summary>Everything the flavour line could say about an answer, what matters most first.</summary>
        public static List<FlavourBeat> Beats(OptionImpact r, HintWords w)
        {
            var beats = new List<FlavourBeat>();
            if (r == null || r.Empty) return beats;
            int order = 0;
            void Add(FlavourBeat b)
            {
                if (b == null || b.weight <= 0f) return;
                b.order = order++;
                beats.Add(b);
            }

            Feelings(r.lines, w, Add);
            Pairs(r.lines, w, Add);
            Things(r.lines, w, Add);
            Gamble(r, w, Add);
            return beats.OrderByDescending(b => b.weight).ThenBy(b => b.order).ToList();
        }

        /// <summary>
        /// Spirits and trust, a beat for each person or group: which way it goes for them on balance, by trust or by mood
        /// whichever weighs more, both ways at once for one person said as mixed feelings; people by name who feel it the
        /// same way said together; the many against one when a group goes the other way from the one most touched.
        /// </summary>
        private static void Feelings(List<ImpactLine> lines, HintWords w, Action<FlavourBeat> add)
        {
            var tallies = new Dictionary<string, Tally>();
            var order = new List<Tally>();
            foreach (var l in lines)
            {
                if (l.topic != HintTopic.Mood && l.topic != HintTopic.Trust) continue;
                if (Math.Abs(ImpactHints.Value(l)) < 0.001f) continue;
                foreach (var s in Subjects(l, w, true))
                {
                    if (!tallies.TryGetValue(s.key, out var t))
                    {
                        tallies[s.key] = t = new Tally { who = s };
                        order.Add(t);
                    }
                    var side = Up(l) ? t.good : t.bad;
                    float v = Size(l) * Sure(l, s.quant);
                    side.weight += v;
                    side.chance = Math.Max(side.chance, l.chance);
                    if (l.topic == HintTopic.Trust) side.trust += v;
                    else
                    {
                        side.mood += v;
                        side.moods++;
                        side.big &= ImpactHints.Level(l) == 2;
                    }
                }
            }
            var people = new List<FlavourBeat>();
            foreach (var t in order)
            {
                var who = t.who;
                FlavourBeat b;
                if (!who.many && t.good.weight > 0f && t.bad.weight > 0f && Math.Min(t.good.weight, t.bad.weight) >= 0.6f * Math.Max(t.good.weight, t.bad.weight))
                    b = new FlavourBeat { kind = FlavourKind.Mixed, a = who.words, weight = (t.good.weight + t.bad.weight) * 0.7f, maybe = Math.Max(t.good.chance, t.bad.chance) < 0.6f };
                else
                {
                    bool up = t.good.weight >= t.bad.weight;
                    var side = up ? t.good : t.bad;
                    if (side.weight <= 0f) continue;
                    b = new FlavourBeat { kind = Family(side, up), a = who.words, sign = up ? 1 : -1, maybe = !who.many && side.chance < 0.6f };
                    b.weight = side.weight * (!who.many ? 1.15f : who.all ? 1.3f : who.quant ? 0.75f : 0.9f);
                }
                b.many = who.many;
                b.names.AddRange(who.names);
                people.Add(b);
            }
            Together(people, w, 1, tallies);
            Together(people, w, -1, tallies);

            // The many against one: the person most touched by name, and a group that goes the other way, when nobody else
            // by name goes that other way as much.
            var top = people.Where(b => !b.many && b.sign != 0).OrderByDescending(b => b.weight).FirstOrDefault();
            if (top != null && !people.Any(b => !b.many && b.sign == -top.sign && b.weight >= 0.5f * top.weight))
            {
                var group = people.Where(b => b.many && b.sign == -top.sign).OrderByDescending(b => b.weight).FirstOrDefault();
                if (group != null && group.weight >= 0.6f)
                {
                    var two = new FlavourBeat
                    {
                        kind = group.sign > 0 ? FlavourKind.ManyOverOne : FlavourKind.OneOverMany, a = top.a, b = group.a, weight = top.weight + 0.6f * group.weight,
                        maybe = top.maybe, single = top
                    };
                    two.parts.Add(top);
                    two.parts.Add(group);
                    two.names.AddRange(top.names);
                    add(two);
                }
            }
            foreach (var b in people) add(b);
        }

        private static FlavourKind Family(Side side, bool up)
        {
            if (side.trust > side.mood) return up ? FlavourKind.TrustUp : FlavourKind.TrustDown;
            bool big = side.moods > 0 && side.big;
            return up ? big ? FlavourKind.MoodUpBig : FlavourKind.MoodUp : big ? FlavourKind.MoodDownBig : FlavourKind.MoodDown;
        }

        /// <summary>People by name who feel it the same way, near as much as the one who feels it most, said together ("Guido and Avila would take it badly").</summary>
        private static void Together(List<FlavourBeat> people, HintWords w, int sign, Dictionary<string, Tally> tallies)
        {
            var same = people.Where(b => !b.many && b.sign == sign).OrderByDescending(b => b.weight).ToList();
            if (same.Count < 2) return;
            float most = same[0].weight;
            var with = same.Where(b => b.weight >= 0.5f * most).Take(3).ToList();
            if (with.Count < 2) return;
            // In the order the letter has them (its first person first).
            with = with.OrderBy(b => people.IndexOf(b)).ToList();
            var side = new Side();
            foreach (var b in with)
            {
                var t = tallies["n|" + b.a];
                var s = sign > 0 ? t.good : t.bad;
                side.weight += s.weight;
                side.mood += s.mood;
                side.trust += s.trust;
                side.moods += s.moods;
                side.big &= s.moods == 0 || s.big;
                side.chance = Math.Max(side.chance, s.chance);
            }
            var names = with.Select(b => b.a).ToList();
            var merged = new FlavourBeat
            {
                kind = Family(side, sign > 0), a = JoinAnd(w, names), sign = sign, weight = with.Sum(b => b.weight) * 0.85f, maybe = with.All(b => b.maybe)
            };
            merged.names.AddRange(names);
            int at = people.IndexOf(with[0]);
            foreach (var b in with) people.Remove(b);
            people.Insert(Math.Min(at, people.Count), merged);
        }

        /// <summary>Between two people: how one comes to see the other, a bond made or broken, a fight.</summary>
        private static void Pairs(List<ImpactLine> lines, HintWords w, Action<FlavourBeat> add)
        {
            var seen = new Dictionary<string, FlavourBeat>();
            void Keep(string key, FlavourBeat b)
            {
                if (seen.TryGetValue(key, out var had))
                {
                    had.weight = Math.Max(had.weight, b.weight);
                    return;
                }
                seen[key] = b;
                add(b);
            }
            foreach (var l in lines)
            {
                if (l.topic == HintTopic.Opinion && !string.IsNullOrWhiteSpace(l.about))
                {
                    var s = Subjects(l, w, false).FirstOrDefault();
                    if (s == null || Math.Abs(ImpactHints.Value(l)) < 0.001f) continue;
                    bool up = Up(l);
                    var b = new FlavourBeat
                    {
                        kind = up ? FlavourKind.ThinkBetter : FlavourKind.ThinkWorse, a = s.words, b = l.about.Trim(), many = s.many, sign = up ? 1 : -1,
                        weight = Size(l) * 0.8f * Sure(l, s.quant), maybe = !s.quant && l.chance < 0.6f
                    };
                    b.names.AddRange(s.names);
                    b.names.AddRange(SplitNames(b.b, w));
                    Keep(b.kind + "|" + b.a + "|" + b.b, b);
                }
                else if ((l.cue == FlavourCue.Bond || l.cue == FlavourCue.Rift || l.cue == FlavourCue.Fight) && !string.IsNullOrWhiteSpace(l.who) && !string.IsNullOrWhiteSpace(l.with))
                {
                    var b = new FlavourBeat { a = l.who.Trim(), b = l.with.Trim() };
                    if (l.cue == FlavourCue.Fight)
                    {
                        b.kind = FlavourKind.Fight;
                        b.sign = -1;
                        b.harm = true;
                        b.weight = 4f * Sure(l, false);
                        b.maybe = l.chance < 0.85f;
                    }
                    else
                    {
                        b.kind = l.cue == FlavourCue.Bond ? FlavourKind.Bond : FlavourKind.Rift;
                        b.sign = l.cue == FlavourCue.Bond ? 1 : -1;
                        b.weight = 1.8f * Sure(l, false);
                        b.maybe = l.chance < 0.6f;
                    }
                    b.names.Add(b.a);
                    b.names.Add(b.b);
                    Keep(b.kind + "|" + string.Join("|", b.names.OrderBy(n => n, StringComparer.Ordinal)), b);
                }
            }
        }

        /// <summary>Everything else: what happens on the map, goods, other peoples, the colony's ways, who comes and goes, the smaller things people get.</summary>
        private static void Things(List<ImpactLine> lines, HintWords w, Action<FlavourBeat> add)
        {
            FlavourBeat cost = null, gain = null, good = null, bad = null, hurt = null, map = null;
            var keyed = new Dictionary<string, FlavourBeat>();
            void Best(ref FlavourBeat slot, FlavourBeat b)
            {
                if (slot == null || b.weight > slot.weight) slot = b;
            }
            void Once(string key, FlavourBeat b)
            {
                if (keyed.TryGetValue(key, out var had))
                {
                    if (b.weight > had.weight) keyed[key] = b;
                    return;
                }
                keyed[key] = b;
            }
            FlavourBeat Person(ImpactLine l, FlavourKind kind, float weight, int sign)
            {
                var s = Subjects(l, w, false).FirstOrDefault();
                if (s == null) return null;
                var b = new FlavourBeat { kind = kind, a = s.words, many = s.many, sign = sign, weight = weight * Sure(l, s.quant), maybe = !s.quant && l.chance < 0.6f };
                b.names.AddRange(s.names);
                return b;
            }
            void PersonOnce(ImpactLine l, FlavourKind kind, float weight, int sign)
            {
                var b = Person(l, kind, weight, sign);
                if (b != null) Once(kind + "|" + b.a, b);
            }

            foreach (var l in lines)
            {
                switch (l.cue)
                {
                    case FlavourCue.Joins:
                        Once("joins", new FlavourBeat { kind = FlavourKind.Joins, sign = 1, weight = 3.5f * Sure(l, false), maybe = l.chance < 0.6f });
                        continue;
                    case FlavourCue.Leaves:
                    {
                        var s = Subjects(l, w, false).FirstOrDefault();
                        var b = new FlavourBeat { kind = s != null ? FlavourKind.Leaves : FlavourKind.Bad, a = s?.words, sign = -1, weight = 4f * Sure(l, false), maybe = l.chance < 0.6f };
                        if (s != null) b.names.AddRange(s.names);
                        Once("leaves|" + b.a, b);
                        continue;
                    }
                    case FlavourCue.Raid:
                        Once("raid|" + l.about, new FlavourBeat
                        {
                            kind = string.IsNullOrWhiteSpace(l.about) ? FlavourKind.Bad : FlavourKind.Raid, a = l.about?.Trim(), sign = -1, harm = true, weight = 4f * Sure(l, false),
                            maybe = l.chance < 0.85f
                        });
                        continue;
                    case FlavourCue.CustomKept:
                        Once("customKept", new FlavourBeat { kind = FlavourKind.CustomKept, a = l.about?.Trim(), sign = 1, weight = 1.5f });
                        continue;
                    case FlavourCue.CustomBroken:
                        Once("customBroken", new FlavourBeat { kind = FlavourKind.CustomBroken, a = l.about?.Trim(), sign = -1, weight = 2.2f });
                        continue;
                    case FlavourCue.Fight:
                    case FlavourCue.Bond:
                    case FlavourCue.Rift:
                        // Said with the pairs; one without its two names is only good or bad.
                        if (!string.IsNullOrWhiteSpace(l.who) && !string.IsNullOrWhiteSpace(l.with)) continue;
                        break;
                }
                int level = ImpactHints.Level(l);
                switch (l.topic)
                {
                    case HintTopic.Mood:
                    case HintTopic.Trust:
                    case HintTopic.Opinion:
                        continue;
                    case HintTopic.Play:
                        if (map != null && map.kind != FlavourKind.MapDone) continue;
                        string when = string.IsNullOrEmpty(l.when) ? null : w["CC_ImpactHint_When_" + l.when];
                        if (l.workTime && l.hours > 0f) map = new FlavourBeat { kind = FlavourKind.Work, sign = -1, weight = 1.2f + Math.Min(l.hours, 9f) / 9f };
                        else if (l.together) map = new FlavourBeat { kind = FlavourKind.Gather, a = when, weight = 1f };
                        else map = new FlavourBeat { kind = FlavourKind.MapDone, a = when, weight = 0.8f };
                        map.maybe = l.chance < 0.6f;
                        continue;
                    case HintTopic.Goods:
                    case HintTopic.Silver:
                        if (Up(l)) Best(ref gain, new FlavourBeat { kind = FlavourKind.Gain, sign = 1, weight = (level + 1) * 0.75f * Sure(l, false), maybe = l.chance < 0.6f });
                        else Best(ref cost, new FlavourBeat { kind = level >= 2 ? FlavourKind.CostBig : FlavourKind.Cost, sign = -1, weight = (level + 1) * 0.8f * Sure(l, false), maybe = l.chance < 0.6f });
                        continue;
                    case HintTopic.Goodwill:
                        if (string.IsNullOrWhiteSpace(l.about)) continue;
                        bool warmer = Up(l);
                        Once("people|" + l.about, new FlavourBeat
                        {
                            kind = warmer ? FlavourKind.NeighboursUp : FlavourKind.NeighboursDown, a = l.about.Trim(), sign = warmer ? 1 : -1, weight = (level + 1) * 0.9f * Sure(l, false),
                            maybe = l.chance < 0.6f
                        });
                        continue;
                    case HintTopic.Tension:
                        bool tenser = Up(l);
                        Once("camp", new FlavourBeat
                        {
                            kind = tenser ? FlavourKind.CampUneasy : FlavourKind.CampEasier, sign = tenser ? -1 : 1, weight = (level + 1) * 0.9f * Sure(l, false), maybe = l.chance < 0.6f
                        });
                        continue;
                    case HintTopic.Ways:
                        if (!string.IsNullOrWhiteSpace(l.about)) Once("lean", new FlavourBeat { kind = FlavourKind.Lean, a = l.about.Trim(), weight = 0.9f });
                        continue;
                    case HintTopic.Joy:
                    case HintTopic.Rest:
                    case HintTopic.Comfort:
                        PersonOnce(l, FlavourKind.Rested, (level + 1) * 0.6f, 1);
                        continue;
                    case HintTopic.Strain:
                        bool more = Up(l);
                        PersonOnce(l, more ? FlavourKind.Worn : FlavourKind.Eased, (level + 1) * 0.8f, more ? -1 : 1);
                        continue;
                    case HintTopic.Skill:
                        PersonOnce(l, FlavourKind.Learns, (level + 1) * 0.5f, 1);
                        continue;
                    case HintTopic.Inspire:
                        PersonOnce(l, FlavourKind.Inspired, 1.5f, 1);
                        continue;
                    case HintTopic.Standards:
                        bool higher = Up(l);
                        PersonOnce(l, higher ? FlavourKind.ExpectMore : FlavourKind.ExpectLess, (level + 1) * 0.5f, higher ? -1 : 1);
                        continue;
                    case HintTopic.Certainty:
                        bool surer = Up(l);
                        PersonOnce(l, surer ? FlavourKind.FaithUp : FlavourKind.FaithDown, (level + 1) * 0.7f, surer ? 1 : -1);
                        continue;
                    case HintTopic.Break:
                    {
                        var b = Person(l, FlavourKind.Break, 3.5f, -1);
                        if (b == null) continue;
                        b.harm = true;
                        b.maybe = l.chance < 0.85f;
                        Once("break|" + b.a, b);
                        continue;
                    }
                }
                // Lines in their own words: by what kind of thing they are and which way they go.
                int sign = l.Sign;
                if (sign == 0) continue;
                switch (l.kind)
                {
                    case ImpactKind.Map:
                        if (map == null) map = new FlavourBeat { kind = FlavourKind.MapDone, weight = 0.6f, maybe = l.chance < 0.6f };
                        continue;
                    case ImpactKind.Health:
                        if (sign < 0) Best(ref hurt, new FlavourBeat { kind = FlavourKind.Hurt, sign = -1, harm = true, maybe = true, weight = 3f * Sure(l, false) });
                        else Once("healthUp", new FlavourBeat { kind = FlavourKind.HealthUp, sign = 1, weight = 1.5f * Sure(l, false), maybe = l.chance < 0.6f });
                        continue;
                    case ImpactKind.Ways:
                        continue;
                }
                float weight = l.kind == ImpactKind.People ? sign > 0 ? 2.5f : 3f : sign > 0 ? 0.8f : 1f;
                if (sign > 0) Best(ref good, new FlavourBeat { kind = FlavourKind.Good, sign = 1, weight = weight * Sure(l, false), maybe = l.chance < 0.6f });
                else Best(ref bad, new FlavourBeat { kind = FlavourKind.Bad, sign = -1, weight = weight * Sure(l, false), maybe = l.chance < 0.6f });
            }
            foreach (var b in keyed.Values) add(b);
            foreach (var b in new[] { map, cost, gain, hurt, good, bad }) add(b);
        }

        /// <summary>
        /// The gamble: who comes to blows or may be hurt if it goes wrong (harm, always said), else who it would go badly
        /// for, else that it may come to nothing, else that it is a gamble, bigger the likelier it goes wrong.
        /// </summary>
        private static void Gamble(OptionImpact r, HintWords w, Action<FlavourBeat> add)
        {
            float p = r.riskChance;
            if (p <= 0f) return;
            bool real = p >= RealGamble, maybe = p < 0.85f;
            var fight = r.risk.FirstOrDefault(l => l.cue == FlavourCue.Fight && !string.IsNullOrWhiteSpace(l.who) && !string.IsNullOrWhiteSpace(l.with));
            if (fight != null)
            {
                var b = new FlavourBeat { kind = FlavourKind.Fight, a = fight.who.Trim(), b = fight.with.Trim(), sign = -1, harm = true, gamble = real, maybe = maybe, weight = 3f + 4f * p };
                b.names.Add(b.a);
                b.names.Add(b.b);
                add(b);
                return;
            }
            var broken = r.risk.FirstOrDefault(l => l.topic == HintTopic.Break && !string.IsNullOrWhiteSpace(l.who));
            if (broken != null)
            {
                var s = Subjects(broken, w, false).FirstOrDefault();
                if (s != null)
                {
                    var b = new FlavourBeat { kind = FlavourKind.Break, a = s.words, many = s.many, sign = -1, harm = true, gamble = real, maybe = maybe, weight = 3f + 3f * p };
                    b.names.AddRange(s.names);
                    add(b);
                    return;
                }
            }
            var raid = r.risk.FirstOrDefault(l => l.cue == FlavourCue.Raid && !string.IsNullOrWhiteSpace(l.about));
            if (raid != null)
            {
                add(new FlavourBeat { kind = FlavourKind.Raid, a = raid.about.Trim(), sign = -1, harm = true, gamble = real, maybe = maybe, weight = 3f + 4f * p });
                return;
            }
            if (r.risk.Any(l => l.kind == ImpactKind.Health && l.Sign < 0 && l.topic == HintTopic.None))
            {
                add(new FlavourBeat { kind = FlavourKind.Hurt, sign = -1, harm = true, gamble = real, maybe = true, weight = 2.5f + 3f * p });
                return;
            }
            // Who it would go worst for, by name before a group.
            Who worst = null;
            float most = 0f;
            foreach (var l in r.risk)
            {
                if (l.topic != HintTopic.Mood && l.topic != HintTopic.Trust && l.topic != HintTopic.Opinion) continue;
                if (Up(l) || Math.Abs(ImpactHints.Value(l)) < 0.001f) continue;
                var s = Subjects(l, w, false).FirstOrDefault();
                if (s == null) continue;
                float v = Size(l) * (s.many ? 0.7f : 1f);
                if (v > most)
                {
                    most = v;
                    worst = s;
                }
            }
            if (worst != null)
            {
                var b = new FlavourBeat { kind = FlavourKind.GambleOn, a = worst.words, sign = -1, gamble = real, maybe = maybe, weight = 1.5f + 4f * p };
                b.names.AddRange(worst.names);
                add(b);
                return;
            }
            if (r.riskReplaces && r.risk.Count == 0)
            {
                add(new FlavourBeat { kind = FlavourKind.Fizzle, gamble = real, maybe = maybe, weight = 1.2f + 4f * p });
                return;
            }
            add(new FlavourBeat { kind = p >= 0.5f ? FlavourKind.GambleBig : FlavourKind.Gamble, gamble = real, maybe = maybe, weight = 1.2f + 4f * p });
        }

        // ---------------- The line ----------------

        /// <summary>Two beats that would say the same thing, or the same people twice where something else could be said.</summary>
        private static bool Same(FlavourBeat a, FlavourBeat b)
        {
            if (a == null || b == null) return false;
            if (a == b || a.single == b || b.single == a || a.parts.Contains(b) || b.parts.Contains(a)) return true;
            if (a.kind == b.kind && a.many == b.many) return true;
            if (IsPair(a.kind) && IsPair(b.kind) && a.names.Count > 0 && new HashSet<string>(a.names).SetEquals(b.names)) return true;
            if (IsPerson(a.kind) && IsPerson(b.kind) && a.names.Intersect(b.names).Any()) return true;
            if ((IsGamble(a.kind) || a.gamble) && (IsGamble(b.kind) || b.gamble)) return true;
            return IsMap(a.kind) && IsMap(b.kind);
        }

        /// <summary>The best of the rest beside <paramref name="first"/>: someone else before the same people again.</summary>
        private static FlavourBeat Beside(List<FlavourBeat> beats, FlavourBeat first, float least)
        {
            FlavourBeat best = null;
            float score = 0f;
            foreach (var b in beats)
            {
                if (b == first || b.Two || b.harm || b.gamble || b.weight < least || Same(first, b)) continue;
                float s = b.weight * (b.names.Intersect(first.names).Any() ? 0.6f : 1f);
                if (s > score)
                {
                    score = s;
                    best = b;
                }
            }
            return best;
        }

        /// <summary>The flavour line under an answer, or null when there's nothing to say. <paramref name="answer"/> is its place in the letter.</summary>
        public static string Line(OptionImpact r, HintWords w, uint seed, int answer, int maxChars = LineChars) => Make(r, w, seed, answer, maxChars).text;

        /// <summary>The flavour line, with what it said and the words it was made from.</summary>
        public static FlavourLine Make(OptionImpact r, HintWords w, uint seed, int answer, int maxChars = LineChars)
        {
            var res = new FlavourLine();
            var beats = Beats(r, w);
            if (beats.Count == 0) return res;
            var top = beats[0];
            // Someone may be hurt: always said. Else a real gamble.
            var must = beats.FirstOrDefault(b => b.harm) ?? beats.FirstOrDefault(b => b.gamble);
            FlavourBeat first, second = null;
            if (must == null)
            {
                first = top;
                if (!top.Two) second = Beside(beats, top, 0.35f * top.weight);
                // People before things: "It would please everyone; the stores would feel it."
                if (second != null && IsThing(first.kind) && (IsPerson(second.kind) || IsPair(second.kind)))
                {
                    var t = first;
                    first = second;
                    second = t;
                }
            }
            else if (must == top)
            {
                first = Beside(beats.Select(b => b.Two ? b.single : b).Distinct().ToList(), must, 0.3f * top.weight);
                second = must;
                if (first == null)
                {
                    first = must;
                    second = null;
                }
            }
            else
            {
                first = top.Two ? top.single : top;
                second = must;
            }
            Say(res, w, seed, answer, maxChars, first, second, must);
            // The many against one with long names: the one alone.
            if (res.text != null && res.text.Length > maxChars && first.Two)
            {
                res = new FlavourLine();
                Say(res, w, seed, answer, maxChars, first.single, second, must);
            }
            return res;
        }

        private static void Say(FlavourLine res, HintWords w, uint seed, int answer, int maxChars, FlavourBeat first, FlavourBeat second, FlavourBeat must)
        {
            var o1 = Options(first, w, seed, answer, 0);
            var o2 = second == null ? null : Options(second, w, seed, answer, second.kind == first.kind ? 1 : 0);
            if (o1.Count == 0 && o2 != null && o2.Count > 0)
            {
                first = second;
                o1 = o2;
                second = null;
                o2 = null;
            }
            if (o1.Count == 0) return;
            string Join(string x, string y) => y == null ? Cap(x) + "." : Cap(w.F("CC_ImpactFlavour_Two", x, Lower(y, second))) + ".";
            if (o2 != null && o2.Count > 0)
                foreach (var x in o1)
                foreach (var y in o2)
                {
                    string t = Join(x.text, y.text);
                    if (t.Length > maxChars) continue;
                    Done(res, t, first, x.key, second, y.key);
                    return;
                }
            // No room for both: the one that must be said, else the first, in its shortest words if none fit.
            var keep = o2 != null && o2.Count > 0 && must == second ? (second, o2) : (first, o1);
            var fit = keep.Item2.FirstOrDefault(x => x.text.Length + 1 <= maxChars) ?? keep.Item2.OrderBy(x => x.text.Length).First();
            Done(res, Join(fit.text, null), keep.Item1, fit.key, null, null);
        }

        private static void Done(FlavourLine res, string text, FlavourBeat a, string ka, FlavourBeat b, string kb)
        {
            res.text = Clean(text);
            res.said.Add(a);
            res.keys.Add(ka);
            if (b == null) return;
            res.said.Add(b);
            res.keys.Add(kb);
        }

        private sealed class Option
        {
            public string text, key;
        }

        /// <summary>A beat's words in the order to try them: from the one this letter and answer pick, round the family.</summary>
        private static List<Option> Options(FlavourBeat b, HintWords w, uint seed, int answer, int nth)
        {
            var outList = new List<Option>();
            string key = KeyOf(b);
            int n = w.Variants(key);
            if (n == 0) return outList;
            // One step a place in the letter: the answers of one letter never start on the same words of a family that has as many.
            uint start = (Mix(seed, key) + (uint)Math.Max(0, answer) + (uint)(nth * ((n + 1) / 2))) % (uint)n;
            for (int i = 0; i < n; i++)
            {
                string k = key + "_" + ((start + i) % n + 1);
                string text = Fill(w[k], b, w);
                if (text != null) outList.Add(new Option { text = text, key = k });
            }
            return outList;
        }

        private static readonly Regex PlaceRx = new Regex(@"\{(\d)\}");

        /// <summary>A beat's words filled in, or null when they need something it hasn't got (who, the thing, when).</summary>
        public static string Fill(string template, FlavourBeat b, HintWords w)
        {
            if (string.IsNullOrWhiteSpace(template)) return null;
            var args = new[] { b.a, b.b };
            foreach (Match m in PlaceRx.Matches(template))
            {
                int i = m.Groups[1].Value[0] - '0';
                if (i >= args.Length || string.IsNullOrWhiteSpace(args[i])) return null;
            }
            string t = HintWords.Fill(template, args)
                .Replace("{WOULD}", w.Form("CC_ImpactFlavour_Would", b.maybe ? 1 : 0))
                .Replace("{WONT}", w.Form("CC_ImpactFlavour_Wont", b.maybe ? 1 : 0));
            if (t.IndexOf('{') >= 0) return null;
            return Regex.Replace(t, @"\s{2,}", " ").Trim().TrimEnd('.', ' ');
        }

        /// <summary>The second part after "; ": lower-cased unless it starts with a name or a people's name.</summary>
        private static string Lower(string s, FlavourBeat b)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (b != null && ((!string.IsNullOrEmpty(b.a) && s.StartsWith(b.a, StringComparison.Ordinal)) || (!string.IsNullOrEmpty(b.b) && s.StartsWith(b.b, StringComparison.Ordinal))))
                return s;
            return char.ToLowerInvariant(s[0]) + s.Substring(1);
        }

        private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        /// <summary>No exclamation mark and no em dash, whatever a name brings.</summary>
        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s.IndexOf('!') >= 0 || s.IndexOf('—') >= 0) s = Regex.Replace(s.Replace("—", ", ").Replace("!", ""), @" {2,}", " ").Trim();
            return s;
        }

        // ---------------- Variety ----------------

        /// <summary>A number for one letter from what it is about, the same every time it opens and in every game session.</summary>
        public static uint Seed(params string[] parts)
        {
            unchecked
            {
                uint h = 2166136261;
                if (parts != null)
                    foreach (var p in parts)
                    {
                        foreach (char c in p ?? "")
                        {
                            h ^= c;
                            h *= 16777619;
                        }
                        h ^= '|';
                        h *= 16777619;
                    }
                return h;
            }
        }

        private static uint Mix(uint seed, string key)
        {
            unchecked
            {
                uint h = seed ^ 0x9E3779B9u;
                foreach (char c in key)
                {
                    h ^= c;
                    h *= 16777619;
                }
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                return h;
            }
        }
    }
}
