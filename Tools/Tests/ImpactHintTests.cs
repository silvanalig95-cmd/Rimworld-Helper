using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The hints in words under each answer (Dilemmas/ImpactHints.cs): sizes in words, chances in words, who by name or
/// group, the order things are told in, one line cut at a whole clause, and never a digit, in the line or its tooltip.
/// The words come from the mod's own Keyed files.
/// </summary>
public static class ImpactHintTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "answer hints: " + what);
        }

        var keyed = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var e in XDocument.Load(file).Root!.Elements())
                keyed[e.Name.LocalName] = e.Value;
        var w = new HintWords(k => keyed.TryGetValue(k, out var v) ? v : null);
        string M = ImpactMath.Mark;
        bool NoDigit(string s) => s != null && !s.Any(char.IsDigit);
        bool Clean(string s) => NoDigit(s) && !s.Contains('!') && !s.Contains('—') && !s.Contains("CC_");

        // Every word a hint uses is in the Keyed file.
        string src = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Dilemmas", "ImpactHints.cs"));
        foreach (Match m in Regex.Matches(src, "\"(CC_ImpactHint_\\w+?)\""))
            if (!m.Groups[1].Value.EndsWith("_"))
                C(keyed.ContainsKey(m.Groups[1].Value), $"{m.Groups[1].Value} is in the Keyed file");
        foreach (var prefix in new[] { "Scale_", "Much_", "Amount_", "Share_", "Strain_", "Days_", "Hours_", "Silver_", "Risk_", "RiskHead_" })
            for (int i = 0; i < (prefix == "Days_" || prefix == "Hours_" ? 4 : 3); i++)
                C(keyed.ContainsKey("CC_ImpactHint_" + prefix + i), $"CC_ImpactHint_{prefix}{i} is in the Keyed file");
        foreach (var t in new[] { "Mood", "Trust", "Standards", "Certainty" }) C(keyed.ContainsKey("CC_ImpactHint_Noun_" + t), $"a word for {t}");
        foreach (var t in new[] { "Night", "Evening", "Day", "Morning", "Midday", "Afternoon" }) C(keyed.ContainsKey("CC_ImpactHint_When_" + t), $"a word for {t}");
        foreach (var k in keyed.Where(k => k.Key.StartsWith("CC_ImpactHint_") || k.Key.StartsWith("CC_ImpactShows_Hints") || k.Key.StartsWith("CC_SetNote_ImpactShows_Hints")))
        {
            string words = Regex.Replace(k.Value, @"\{\d\}", "");
            C(NoDigit(words) && !words.Contains('!') && !words.Contains('—'), $"{k.Key} has no number, exclamation mark or em dash");
        }

        // Sizes in words: a little, noticeably, greatly.
        ImpactLine Mood(float v) => new ImpactLine { kind = ImpactKind.Mood, sign = v > 0 ? 1 : -1, text = "x", topic = HintTopic.Mood, who = "Alo", size = v };
        C(ImpactHints.Level(Mood(2f)) == 0 && ImpactHints.Level(Mood(4f)) == 1 && ImpactHints.Level(Mood(8f)) == 2, "mood: 2 is a little, 4 noticeably, 8 greatly");
        C(ImpactHints.Level(Mood(-8f)) == 2, "a big bad mood is big too");
        ImpactLine Trust(float v, string who = "Alo") => new ImpactLine
        {
            kind = ImpactKind.Trust, sign = 1, text = who + ": " + M + " trust", brief = who + " " + M + " trust", key = "trust|" + who, amount = v, sum = true,
            topic = HintTopic.Trust, who = who
        };
        C(ImpactHints.Level(Trust(1f)) == 0 && ImpactHints.Level(Trust(3f)) == 1 && ImpactHints.Level(Trust(6f)) == 2, "trust: 1 a little, 3 noticeably, 6 greatly");
        C(ImpactHints.Clause(Mood(2f), w, false) == "Alo's spirits rise a little", $"a little ({ImpactHints.Clause(Mood(2f), w, false)})");
        C(ImpactHints.Clause(Mood(4f), w, false) == "Alo's spirits rise noticeably", "noticeably");
        C(ImpactHints.Clause(Mood(-8f), w, false) == "Alo's spirits fall greatly", "greatly, and falling");
        C(ImpactHints.Clause(Trust(-3f), w, false) == "Alo's trust in the council falls noticeably", $"trust falls ({ImpactHints.Clause(Trust(-3f), w, false)})");
        ImpactLine Goods(int sign, int count, float stock) => new ImpactLine
        {
            kind = ImpactKind.Goods, sign = sign, text = M + " beer", key = "g", amount = count, sum = true, signed = false, topic = HintTopic.Goods, about = "beer", stock = stock
        };
        C(ImpactHints.Clause(Goods(-1, 2, 40f), w, false) == "uses a little of the stores' beer", "a small share of the stores is a little");
        C(ImpactHints.Clause(Goods(-1, 12, 40f), w, false) == "uses some of the stores' beer", "a third of the stores is some");
        C(ImpactHints.Clause(Goods(-1, 30, 40f), w, false) == "uses much of the stores' beer", "most of the stores is much");
        C(ImpactHints.Clause(Goods(-1, 30, -1f), w, false) == "uses some of the stores' beer", "stores not counted: some");
        C(ImpactHints.Clause(Goods(1, 50, 20f), w, false) == "brings in plenty of beer", "more than the stores hold is plenty");
        var silver = new ImpactLine { kind = ImpactKind.Goods, sign = 1, text = M + " silver", key = "s", amount = -600f, sum = true, topic = HintTopic.Silver };
        C(ImpactHints.Clause(silver, w, false) == "costs much silver", "much silver out");
        var joy = new ImpactLine { kind = ImpactKind.Needs, sign = 1, text = "x", topic = HintTopic.Joy, who = "everyone (10)", size = 0.25f };
        C(ImpactHints.Clause(joy, w, false) == "everyone gets plenty of recreation", $"recreation ({ImpactHints.Clause(joy, w, false)})");

        // Chances in words, and a chance for each of a group as "some".
        var maybe = Mood(4f);
        maybe.chance = 0.3f;
        C(ImpactHints.Clause(maybe, w, false) == "Alo's spirits may rise noticeably", "a low chance is may");
        maybe.chance = 0.6f;
        C(ImpactHints.Clause(maybe, w, false) == "Alo's spirits likely rise noticeably", "a fair chance is likely");
        maybe.chance = 0.9f;
        C(ImpactHints.Clause(maybe, w, false) == "Alo's spirits almost surely rise noticeably", "a high chance is almost surely");
        var each = Trust(1f, "everyone (10)");
        each.chance = 0.3f;
        each.each = true;
        C(ImpactHints.Clause(each, w, false) == "trust in the council may rise a little for some", $"a chance each is some ({ImpactHints.Clause(each, w, false)})");
        var trustOne = Trust(1f);
        C(ImpactHints.Clause(trustOne, w, false) == "Alo's trust in the council rises a little", "one noun: one verb");

        // Who: names, groups, everyone.
        var who = ImpactHints.WhoOf("everyone (10)", w);
        C(who.all && !who.names && !who.plural && who.words == "everyone", "everyone (10) is everyone");
        who = ImpactHints.WhoOf("Alo and Bea", w);
        C(who.names && who.plural && !who.all, "two names are several");
        who = ImpactHints.WhoOf("the devout (4)", w);
        C(!who.names && who.plural && who.words == "the devout", "a group without how many");

        // The screenshot's answers.
        var watch = new OptionImpact();
        watch.lines.Add(new ImpactLine { kind = ImpactKind.Map, text = "The high watch, in the evening, for 2 to 3 hours", brief = "The high watch", topic = HintTopic.Play, hint = "The high watch together in the evening", hours = 2.5f });
        watch.lines.Add(each);
        watch.lines.Add(Trust(3f));
        watch.lines.Add(new ImpactLine { kind = ImpactKind.Needs, sign = 1, text = "everyone (10): recreation +8%", brief = "everyone (10) recreation +8%", topic = HintTopic.Joy, who = "everyone (10)", size = 0.08f, each = true });
        watch.lines.Add(new ImpactLine { kind = ImpactKind.Ways, text = "The colony grows a little more traditional, a little more easygoing", brief = "a little more traditional, a little more easygoing", topic = HintTopic.Ways, about = "traditional" });
        string line = ImpactHints.Line(watch, w);
        C(line != null && line.StartsWith("The high watch together in the evening: "), $"what happens on the map comes first ({line})");
        C(line != null && line.Contains("trust in the council may rise a little for some, especially Alo's"), $"one who feels it more is said with everyone ({line})");
        C(line != null && line.Contains("everyone gets a little recreation"), "recreation for everyone");
        C(line != null && line.EndsWith(".") && line.Length <= ImpactHints.LineChars && Clean(line), "one clean line, a sentence");
        string wide = ImpactHints.Line(watch, w, 400);
        C(wide.Contains("the colony leans traditional") && wide.IndexOf("the colony leans", StringComparison.Ordinal) > wide.IndexOf("recreation", StringComparison.Ordinal),
            $"the colony's ways come last, when there's room ({wide})");
        var letDown = new OptionImpact();
        letDown.lines.Add(new ImpactLine { kind = ImpactKind.Mood, sign = -1, text = "Alo: -5 mood for 4 days (let down)", brief = "Alo -5 mood", topic = HintTopic.Mood, who = "Alo", size = -5f, days = 4f, about = "let down" });
        letDown.lines.Add(Trust(-2f));
        letDown.lines.Add(new ImpactLine { kind = ImpactKind.Ways, text = "The colony grows more hard-working", topic = HintTopic.Ways, about = "hard-working" });
        line = ImpactHints.Line(letDown, w);
        C(line == "Alo's spirits fall noticeably and trust in the council a little; the colony leans hard-working.", $"the same person's mood and trust together, biggest first ({line})");
        string tip = ImpactHints.Plain(letDown, w);
        C(tip.Contains("Alo's spirits fall noticeably for a few days (let down)") && tip.Contains("Mood") && tip.Contains("Trust in the council"),
            $"the tooltip: grouped by kind, how long and what the mood is ({tip})");

        // A gamble, costs and the order they're told in; the line cut at whole clauses.
        var feast = new OptionImpact { riskChance = 0.25f, riskSkill = "social", riskHelper = "Bea" };
        feast.lines.Add(new ImpactLine { kind = ImpactKind.Map, text = "The feast, in the day, for 2.5 to 3.5 hours, with everyone (9)", brief = "The feast", topic = HintTopic.Play, hint = "The feast together in the day", hours = 3f, workTime = true });
        feast.lines.Add(new ImpactLine { kind = ImpactKind.Mood, sign = 1, text = "everyone (9): +3 to +5 mood for 2 days (feasted)", brief = "everyone (9) +3 to +5 mood", topic = HintTopic.Mood, who = "everyone (9)", size = 5f, days = 2f, about = "feasted" });
        feast.lines.Add(new ImpactLine { kind = ImpactKind.Mood, sign = 1, text = "Tam: +1 mood (a small thing)", brief = "Tam +1 mood", topic = HintTopic.Mood, who = "Tam", size = 1f });
        feast.lines.Add(Goods(-1, 12, 20f));
        feast.lines.Add(new ImpactLine { kind = ImpactKind.Goods, sign = -1, text = "Up to 9 cooked meals eaten from the stores, one each (the stores hold 14)", brief = "9 meals eaten" });
        feast.lines.Add(new ImpactLine { kind = ImpactKind.Goodwill, sign = 1, text = "the Iron Hand: " + M + " goodwill", key = "gw", amount = 6f, sum = true, topic = HintTopic.Goodwill, about = "the Iron Hand" });
        feast.risk.Add(new ImpactLine { kind = ImpactKind.Health, sign = -1, text = "Alo and Bea come to blows", brief = "Alo and Bea fight" });
        line = ImpactHints.Line(feast, w);
        C(line.StartsWith("The feast together in the day: spirits rise noticeably"), $"the map, then how people feel ({line})");
        C(line.Contains("a gamble: Alo and Bea fight"), "the gamble and what goes wrong");
        C(line.IndexOf("spirits", StringComparison.Ordinal) < line.IndexOf("costs a few hours of work", StringComparison.Ordinal)
          && line.IndexOf("costs a few hours of work", StringComparison.Ordinal) < line.IndexOf("a gamble", StringComparison.Ordinal), "feelings, then costs, then the gamble");
        C(!line.Contains("Tam"), "a tiny mood is left to the tooltip");
        C(line.Length <= ImpactHints.LineChars && Clean(line), $"short and clean ({line.Length})");
        tip = ImpactHints.Plain(feast, w);
        C(tip.Contains("Tam's spirits rise a little"), "the tooltip keeps the small things");
        C(tip.Contains("Relations with the Iron Hand improve noticeably"), "other peoples");
        C(tip.Contains("A gamble: it may well go wrong (Bea's social makes it less likely), and then also:") && tip.Contains("Alo and Bea come to blows"), $"the gamble in the tooltip ({tip})");
        C(tip.Contains("Some cooked meals eaten from the stores, one each"), "a line's own words without its numbers");
        C(Clean(tip), $"no number in the tooltip ({tip})");
        string narrow = ImpactHints.Line(feast, w, 70);
        var clauses = ImpactHints.Line(feast, w, 1000).TrimEnd('.').Split(new[] { ": ", "; " }, StringSplitOptions.None);
        C(narrow.Length <= 70 && narrow.TrimEnd('.').Split(new[] { ": ", "; " }, StringSplitOptions.None).All(p => clauses.Contains(p)), $"cut at whole clauses, never mid-word ({narrow})");

        // Numbers into words.
        C(ImpactHints.Unnumber("The high watch, in the evening, for 2 to 3 hours", w) == "The high watch, in the evening, for a few hours", "hours in words");
        C(ImpactHints.Unnumber("The vigil, between 9h and 12h, for about an hour", w) == "The vigil, in the morning, for about an hour", "set hours in words");
        C(ImpactHints.Unnumber("everyone (10): a feast", w) == "everyone: a feast", "how many in brackets goes");
        C(ImpactHints.Unnumber("Things worth about 300 silver found", w) == "Things worth some silver found", "silver in words");
        C(ImpactHints.Unnumber("Tam: Hungover for 2.5 to 3.3 days (Global work speed -10%)", w) == "Tam: Hungover for a few days (Global work speed down)", "days and a stat in words");
        C(ImpactHints.Unnumber("1 wild animal(s) of the best eating", w) == "one wild animal of the best eating", "one");
        C(ImpactHints.Unnumber("3 wild animal(s) of the best eating", w) == "a few wild animals of the best eating", "a few");
        C(ImpactHints.Unnumber("Up to 12 cooked meals eaten", w) == "Some cooked meals eaten", "up to many is some");
        C(ImpactHints.Unnumber("Peace with the Iron Hand: their goodwill rises to 15", w) == "Peace with the Iron Hand: their goodwill rises", "a number it rises to goes");
        C(ImpactHints.Unnumber("Mara: flu eased by 30%, never cured outright", w) == "Mara: flu eased, never cured outright", "a share it's eased by goes");
        C(ImpactHints.Unnumber("no numbers here", w) == "no numbers here", "words without numbers stay as they are");

        // Every line the answers can show, its numbers filled in, comes out without a digit.
        var rnd = new Random(7);
        int filled = 0;
        foreach (var k in keyed.Where(k => k.Key.StartsWith("CC_Impact_") && Regex.IsMatch(k.Value, @"\{\d\}")))
        {
            foreach (var pick in new Func<int, string>[] { i => (2 + i * 5).ToString(), i => i == 0 ? "Alo" : (rnd.Next(1, 400) + (rnd.Next(2) == 0 ? "" : ".5")).ToString(), i => i % 2 == 0 ? "+" + (i + 3) + "%" : "Bea" })
            {
                string text = Regex.Replace(k.Value, @"\{(\d)\}", m => pick(int.Parse(m.Groups[1].Value)));
                var l = new ImpactLine { kind = ImpactKind.Other, text = text, brief = text, chance = rnd.Next(3) == 0 ? 0.5f : 1f };
                var r = new OptionImpact { lines = { l } };
                string hint = ImpactHints.Line(r, w) ?? "";
                string full = ImpactHints.Plain(r, w);
                C(Clean(hint) && Clean(full), $"{k.Key}: no digit left ({hint} | {full.Replace("\n", " / ")})");
                filled++;
            }
        }
        C(filled > 300, $"the impact lines were all tried ({filled})");

        // Many answers made up of everything at once: never a digit, one line within its length, the tooltip all words.
        var topics = (HintTopic[])Enum.GetValues(typeof(HintTopic));
        var whos = new[] { "Alo", "Alo and Bea", "everyone (10)", "everyone else (8)", "the devout (3)", "Alo's friends (4)", "those who dislike Alo (2)" };
        for (int round = 0; round < 300; round++)
        {
            var r = new OptionImpact { riskChance = rnd.Next(3) == 0 ? (float)rnd.NextDouble() : 0f, riskReplaces = rnd.Next(2) == 0, riskSkill = rnd.Next(2) == 0 ? "medicine" : null };
            int count = rnd.Next(1, 9);
            for (int i = 0; i < count; i++)
            {
                var t = topics[rnd.Next(topics.Length)];
                float v = (float)(rnd.NextDouble() * 20 - 10);
                var l = new ImpactLine
                {
                    kind = (ImpactKind)rnd.Next(12), sign = v >= 0 ? 1 : -1, topic = t, who = whos[rnd.Next(whos.Length)], about = rnd.Next(4) == 0 ? "beer" : "the Iron Hand",
                    text = "Alo: +" + rnd.Next(1, 30) + " mood for 3 days (a feast of 12)", brief = "Alo +" + rnd.Next(1, 9) + " mood", size = v, days = rnd.Next(0, 12),
                    chance = rnd.Next(3) == 0 ? (float)rnd.NextDouble() : 1f, each = rnd.Next(2) == 0, stock = rnd.Next(-1, 50), hours = rnd.Next(0, 9), workTime = rnd.Next(2) == 0
                };
                if (rnd.Next(2) == 0)
                {
                    l.sum = true;
                    l.amount = v * 30;
                }
                (rnd.Next(4) == 0 ? r.risk : r.lines).Add(l);
            }
            string hint = ImpactHints.Line(r, w);
            string full = ImpactHints.Plain(r, w);
            C(hint == null || (Clean(hint) && hint.Length <= ImpactHints.LineChars + 60 && !hint.Contains('\n')), $"made-up answer {round}: a clean line ({hint})");
            C(Clean(full), $"made-up answer {round}: a clean tooltip ({full.Replace("\n", " / ")})");
        }
        return n;
    }
}
