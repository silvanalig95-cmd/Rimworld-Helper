using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Personal stories: the pace (ArcMath: how many a year at each setting, the budget, the gap, how many at once, how a
/// held condition counts up, who is favoured and who is at odds in a story of three) and every story def (three to five
/// stages, lines in several voices, a gate on every stage but the last, talks, thoughts, quirks and names that exist, a
/// plain fizzle line to fall back on, a third person only in a story of three, and lines that follow STYLE.md's mechanics).
/// </summary>
public static class ArcTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = Pace(check);
        cases += Holding(check);
        cases += OfThree(check);
        cases += Defs(root, check);
        return cases;
    }

    // ---------------- The pace ----------------

    private static int Pace(Action<bool, string> check)
    {
        int cases = 0;
        int[] sizes = { 1, 2, 3, 6, 9, 12, 20 };
        foreach (int n in sizes)
        {
            cases++;
            check(ArcMath.PerYear(ArcMath.Off, n) == 0f && ArcMath.MaxActive(ArcMath.Off, n) == 0, $"off means no stories ({n} colonists)");
            check(ArcMath.PerYear(ArcMath.Rare, n) < ArcMath.PerYear(ArcMath.Normal, n) && ArcMath.PerYear(ArcMath.Normal, n) < ArcMath.PerYear(ArcMath.Frequent, n),
                $"rare < normal < frequent ({n} colonists)");
            for (int f = ArcMath.Rare; f <= ArcMath.Frequent; f++)
            {
                float perYear = ArcMath.PerYear(f, n);
                // "A few per colony per year": never more than about ten, never fewer than one.
                check(perYear >= 1.4f && perYear <= 10.5f, $"a few a year: {perYear} at setting {f}, {n} colonists");
                int max = ArcMath.MaxActive(f, n);
                check(max >= 1 && max <= 4 && max <= Math.Max(1, (n + 1) / 2), $"at most {max} at once is sensible for {n} colonists");
            }
        }
        for (int f = ArcMath.Rare; f <= ArcMath.Frequent; f++)
        {
            check(ArcMath.PerYear(f, 12) >= ArcMath.PerYear(f, 6) && ArcMath.PerYear(f, 6) >= ArcMath.PerYear(f, 2), $"bigger colonies see a few more (setting {f})");
            check(Math.Abs(ArcMath.PerYear(f, 6) - new[] { 0f, 2f, 4f, 7f }[f]) < 0.6f, $"about two, four or seven a year for six colonists (setting {f})");
        }
        // No chance inside the gap, over the budget, or with the most running.
        float py = ArcMath.PerYear(ArcMath.Normal, 6);
        check(ArcMath.DailyChance(py, 0, ArcMath.MinGapDays(py) - 0.1f, 0, 2) == 0f, "none within the gap");
        check(ArcMath.DailyChance(py, ArcMath.Budget(py), 100f, 0, 2) == 0f, "none over the year's budget");
        check(ArcMath.DailyChance(py, 0, 100f, 2, 2) == 0f, "none while the most are running");
        check(ArcMath.DailyChance(py, 0, ArcMath.MinGapDays(py) + 1f, 0, 2) > 0f, "some after the gap");
        check(ArcMath.DailyChance(py, 0, 200f, 0, 2) > ArcMath.DailyChance(py, 0, ArcMath.MinGapDays(py) + 1f, 0, 2), "an overdue story is likelier");
        cases += 5;

        // Ten simulated decades per setting and size: the stories come at about the promised pace, never break the
        // year's budget, and never come closer together than the gap. Each lasts a season (fifteen days).
        foreach (int f in new[] { ArcMath.Rare, ArcMath.Normal, ArcMath.Frequent })
        foreach (int n in new[] { 3, 6, 12 })
        {
            cases++;
            var rng = new Random(1000 * f + n);
            float perYear = ArcMath.PerYear(f, n);
            int max = ArcMath.MaxActive(f, n);
            var starts = new List<int>();
            var ends = new List<int>();
            int last = 0;
            int years = 100;
            int days = years * (int)ArcMath.DaysPerYear;
            bool budgetBroken = false, gapBroken = false;
            for (int day = 0; day < days; day++)
            {
                int running = 0;
                for (int i = 0; i < starts.Count; i++)
                    if (ends[i] > day) running++;
                int lastYear = starts.Count(s => day - s < ArcMath.DaysPerYear);
                float chance = ArcMath.DailyChance(perYear, lastYear, day - last, running, max);
                if (rng.NextDouble() >= chance) continue;
                if (starts.Count > 0 && day - last < ArcMath.MinGapDays(perYear)) gapBroken = true;
                if (lastYear + 1 > ArcMath.Budget(perYear)) budgetBroken = true;
                starts.Add(day);
                ends.Add(day + 15);
                last = day;
            }
            float average = starts.Count / (float)years;
            check(!budgetBroken, $"the year's budget holds (setting {f}, {n} colonists)");
            check(!gapBroken, $"the gap holds (setting {f}, {n} colonists)");
            check(average >= perYear * 0.7f && average <= perYear * 1.1f, $"about {perYear:0.0} a year, got {average:0.00} (setting {f}, {n} colonists)");
        }
        return cases;
    }

    private static int Holding(Action<bool, string> check)
    {
        float held = 0f;
        for (int h = 0; h < 24; h++) held = ArcMath.Held(held, true);
        check(held == 24f, "a day held is 24 hours");
        held = ArcMath.Held(held, false);
        check(held == 23.5f, "an hour that doesn't hold takes half an hour back");
        float none = 0f;
        for (int h = 0; h < 10; h++) none = ArcMath.Held(none, false);
        check(none == 0f, "never below nothing");
        // Mostly good days get there, more slowly; mostly bad ones don't.
        float mostly = 0f, rarely = 0f;
        for (int h = 0; h < 24 * 10; h++)
        {
            mostly = ArcMath.Held(mostly, h % 5 != 0);
            rarely = ArcMath.Held(rarely, h % 3 == 0);
        }
        check(mostly >= 72f, $"held four hours in five for ten days reaches three days, got {mostly}");
        check(rarely < 24f, $"held one hour in three never gets far, got {rarely}");
        check(ArcMath.CandidateWeight(1f, false, false) > ArcMath.CandidateWeight(1f, true, false), "someone who never had a story first");
        check(ArcMath.CandidateWeight(1f, true, true) < ArcMath.CandidateWeight(1f, true, false), "a story seen lately less likely");
        check(ArcMath.CandidateWeight(0f, false, false) == 0f, "no weight, no story");
        check(ArcMath.Progress(5f, 3f, 7f) == 0.5f && ArcMath.Progress(9f, 3f, 7f) == 1f && ArcMath.Progress(1f, 3f, 7f) == 0f, "progress");
        check(ArcMath.TimedOut(30f, 30f) && !ArcMath.TimedOut(29.9f, 30f) && !ArcMath.TimedOut(100f, 0f), "time out");
        return 10;
    }

    // ---------------- Stories of three ----------------

    private static int OfThree(Action<bool, string> check)
    {
        check(ArcMath.Favoured(60, 20, 15) == 1 && ArcMath.Favoured(20, 60, 15) == -1, "the clearly favoured one is chosen, either way round");
        check(ArcMath.Favoured(40, 35, 15) == 0 && ArcMath.Favoured(35, 40, 15) == 0, "too close to call is neither");
        check(ArcMath.Favoured(30, 30, 0) == 0, "a tie is neither, even with no margin");
        check(ArcMath.Favoured(1500, 900, 400) == 1 && ArcMath.Favoured(900, 1500, 400) == -1, "a student well ahead is ahead");
        check(ArcMath.AtOdds(40, 40, true, false) && ArcMath.AtOdds(40, 40, false, true), "rivals, or a sharp word lately, are at odds");
        check(ArcMath.AtOdds(3, 50, false, false) && ArcMath.AtOdds(50, -20, false, false), "either thinking little of the other is at odds");
        check(!ArcMath.AtOdds(30, 25, false, false), "two who like each other are not at odds");
        check(ArcMath.TriangleWeight(15, 60, -30) == 0f && ArcMath.TriangleWeight(60, 60, 20) == 0f, "not close to both, or the two not at odds: no story");
        check(ArcMath.TriangleWeight(70, 70, -40) > ArcMath.TriangleWeight(30, 30, -40), "closer to both, likelier");
        check(ArcMath.TriangleWeight(50, 50, -60) > ArcMath.TriangleWeight(50, 50, 0), "the further apart the two, likelier");
        check(ArcMath.TriangleWeight(100, 100, -100) <= 3f && ArcMath.TriangleWeight(20, 20, 5) >= 0.2f, "within a third and three times as likely");
        check(ArcMath.Both(0.4f, 0.9f) == 0.4f && ArcMath.Both(1.5f, 2f) == 1f && ArcMath.Both(-1f, 0.5f) == 0f, "both: the one further behind, 0 to 1");
        return 12;
    }

    // ---------------- The defs ----------------

    private static readonly string[] Tones = { "plain", "warm", "blunt", "cheerful", "gloomy", "cold" };

    /// <summary>Tags a story's lines may carry besides the moment's (Flavor): its own, its reasons, and relations.</summary>
    private static readonly HashSet<string> StoryTags = new HashSet<string>
    {
        "timeout", "otherdead", "otherleft", "gone", "fight", "cameout", "settled", "anger", "goalgone",
        "partner", "kin", "friend", "animal", "ex", "betrayed", "comrades", "young", "teen", "elder", "oldhand", "goodsecret",
        "nemesis", "quirk", "fear", "nightmare", "lostdream", "strange", "taken", "doubtful", "bitter",
        "child", "night", "snow", "rain", "storm", "winter", "summer",
        // Stories of people's lives (ArcWorkers_Lives.cs): how a story of three went, and why one ended early.
        "won", "lost", "neither", "elsewhere", "otherahead", "thirdahead", "even", "mentor", "grown",
        "thirddead", "thirdleft", "campfight", "officegone", "kindead", "babygone", "old",
        // A feud with an animal (Animals/AnimalArcs.cs) that began with a stolen meal.
        "robbed"
    };

    private static readonly Regex LowerStart = new Regex(@"(^(\[[^\]]*\]\s*)?|[.!?]\s+)\{[A-Z]+_(pronoun|possessive|objective|definite|indefinite)\}");
    private static readonly Regex SkillStart = new Regex(@"(^(\[[^\]]*\]\s*)?|[.!?]\s+)\{SKILL\}");

    private static int Defs(string root, Action<bool, string> check)
    {
        string dir = Path.Combine(root, "Defs", "ArcDefs");
        check(Directory.Exists(dir), "Defs/ArcDefs exists");
        if (!Directory.Exists(dir)) return 0;
        var all = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f)).ToList();
        var names = new HashSet<string>(all.SelectMany(d => d.Root!.Elements()).Select(e => e.Element("defName")?.Value).Where(n => n != null)!);
        var talks = new HashSet<string>(all.SelectMany(d => d.Root!.Elements("InteractionDef"))
            .Where(e => e.Attribute("ParentName")?.Value == "Council_ArcTalkBase").Select(e => e.Element("defName")!.Value));
        var arcs = Directory.GetFiles(dir, "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.ArcDef")).ToList();
        check(arcs.Count >= 15, $"about fifteen personal stories, found {arcs.Count}");
        int lines = 0;
        foreach (var arc in arcs)
        {
            string name = arc.Element("defName")?.Value ?? "?";
            var stages = arc.Element("stages")?.Elements("li").ToList() ?? new List<XElement>();
            check(stages.Count >= 3 && stages.Count <= 5, $"{name}: 3 to 5 stages, has {stages.Count}");
            bool withThird = arc.Element("withThird")?.Value == "true";
            bool mentionsThird = arc.Descendants("li").Any(l => !l.HasElements && l.Value.Contains("{THIRD"));
            check(withThird || !mentionsThird, $"{name}: {{THIRD}} only in a story of three (withThird)");
            if (withThird) check(mentionsThird && arc.Descendants("thirdJournal").Any(), $"{name}: a story of three names its third, and gives them a voice");
            check(!string.IsNullOrEmpty(arc.Element("summary")?.Value), $"{name}: a summary");
            for (int i = 0; i < stages.Count; i++)
            {
                var st = stages[i];
                string where = $"{name} stage {i}";
                var journal = st.Element("journal");
                int tones = Tones.Count(t => journal?.Element(t)?.Elements("li").Any() == true);
                int count = journal?.Descendants("li").Count() ?? 0;
                check(tones >= 4 && count >= 5, $"{where}: lines in several voices ({count} lines, {tones} tones)");
                check(st.Element("chronicle")?.Elements("li").Any() == true, $"{where}: a chronicle line");
                check(!string.IsNullOrEmpty(st.Element("label")?.Value), $"{where}: a label");
                bool last = i == stages.Count - 1;
                var gate = st.Element("gate");
                check(last == (gate == null), $"{where}: {(last ? "the last stage has no gate" : "a gate")}");
                if (!last) check(!string.IsNullOrEmpty(st.Element("waiting")?.Value), $"{where}: says what comes next");
                foreach (var g in gate == null ? Enumerable.Empty<XElement>() : new[] { gate }.Concat(gate.Element("or")?.Elements("li") ?? Enumerable.Empty<XElement>()))
                {
                    string kind = g.Element("kind")?.Value ?? "Time";
                    if (kind == "Talk") check(talks.Contains(g.Element("talk")?.Value ?? ""), $"{where}: talk {g.Element("talk")?.Value} is an arc talk");
                    if (kind == "Hold") check(!string.IsNullOrEmpty(g.Element("hold")?.Value), $"{where}: a Hold gate names its condition");
                }
                foreach (var e in st.Element("effects")?.Elements("li") ?? Enumerable.Empty<XElement>())
                    foreach (var r in e.Elements().Where(x => x.Name.LocalName is "thought" or "quirk" or "epithet"))
                        check(names.Contains(r.Value), $"{where}: {r.Name.LocalName} {r.Value} exists");
            }
            var fizzle = arc.Element("fizzle");
            check(fizzle?.Descendants("li").Any(l => !l.Value.TrimStart().StartsWith("[")) == true, $"{name}: an untagged fizzle line to fall back on");
            check(arc.Element("eulogy")?.Descendants("li").Any() == true, $"{name}: a eulogy line");
            foreach (var li in arc.Descendants("li").Where(l => !l.HasElements && l.Value.Contains(' ')))
            {
                string text = li.Value;
                lines++;
                check(text.Count(c => c == '!') <= 1, $"{name}: at most one ! in \"{text}\"");
                check(!text.Contains('—'), $"{name}: no em dash in \"{text}\"");
                check(!LowerStart.IsMatch(text), $"{name}: a sentence starts with a lowercase placeholder in \"{text}\"");
                check(!SkillStart.IsMatch(text), $"{name}: a sentence starts with {{SKILL}} in \"{text}\"");
                var m = Regex.Match(text, @"^\[([^\]]*)\]");
                if (m.Success)
                    foreach (var tag in m.Groups[1].Value.Split(',').SelectMany(t => t.Split('|')).Select(t => t.Trim().TrimStart('!')))
                        check(StoryTags.Contains(tag) || tag.StartsWith("arcskill:"), $"{name}: unknown tag [{tag}]");
            }
        }
        return arcs.Count + lines;
    }
}
