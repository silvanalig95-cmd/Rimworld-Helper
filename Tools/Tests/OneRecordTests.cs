using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Phase 1 of the review, one record: the chronicle as the colony's view of the history (what it shows, under which of
/// its kinds, a season's summary in words), every question letter writing how it went (the answers' log: its sources,
/// values, what came of it, the record in each letter's code), the year told once (its day, its parts, the founding
/// event's lines shared out with nothing twice), and the record's noise thinned (comrades, elections, anniversaries).
/// </summary>
public static class OneRecordTests
{
    private const int Day = HistoryMath.DayTicks;

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "one record: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var keyed = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var e in XDocument.Load(f).Root!.Elements())
                keyed[e.Name.LocalName] = e.Value;
        var col = Ref.Colony(1);

        // ---------------- What the chronicle shows ----------------

        for (int k = 0; k < HistoryKinds.Chronicle.Length; k++)
            C(ChronicleMath.KindOf(HistoryKinds.Chronicle[k]) == k, $"a {HistoryKinds.Chronicle[k]} line shows as itself");
        C(ChronicleMath.KindOf(HistoryKinds.WorldBegan) == (int)ChronicleKind.World && ChronicleMath.KindOf(HistoryKinds.WorldEnded) == (int)ChronicleKind.World,
            "a world situation's begun and ended lines are the chronicle's world lines");
        foreach (var region in new[] { "world:camp:found", "world:heard", "world:post:opened", "world:hamlet:joined", "world:ruined" })
            C(ChronicleMath.KindOf(region) < 0, $"the region's own {region} is not a line told for the chronicle");
        C(ChronicleMath.KindOf("deed:traded") < 0 && ChronicleMath.KindOf("tie:pact:made") < 0 && ChronicleMath.KindOf(null) < 0, "deeds and ties are not the chronicle's");
        C(ChronicleMath.KindOf("answer:dilemma") == (int)ChronicleKind.Council && ChronicleMath.KindOf("answer:event") == (int)ChronicleKind.World
          && ChronicleMath.KindOf("answer:meeting") == (int)ChronicleKind.Meeting && ChronicleMath.KindOf("answer:camp") == (int)ChronicleKind.Stranger,
            "an answer shows under its question's kind");
        C(ChronicleMath.KindOf("answer:event:minor") < 0 && ChronicleMath.KindOf("answer:election:minor") < 0, "a small answer is kept, not chronicled");

        HistoryEntry E(string kind, string line, int weight, Ref place, params HistoryActor[] actors) =>
            new HistoryEntry { tick = 5 * Day, first = 5 * Day, kind = kind, line = line, weight = weight, place = place, actors = actors.ToList() };
        var us = new HashSet<Ref> { col, Ref.Person(7) };
        var far = Ref.Town(99);
        C(ChronicleMath.Shows(E("life", "Ana married.", HistoryMath.Milestone, col), us.Contains), "a line told for the chronicle shows");
        C(ChronicleMath.Shows(E("world:camp:found", "Macy saw the brigands' hold.", HistoryMath.Milestone, col), us.Contains),
            "the colony's own milestone, written elsewhere with words, shows");
        C(!ChronicleMath.Shows(E("world:camp:raid", "They raided.", HistoryMath.Everyday, col), us.Contains), "an everyday line of the region's stays in the region's history");
        C(!ChronicleMath.Shows(E("world:ruined", "Far town fell.", HistoryMath.Milestone, far), us.Contains), "a far town's fall without the colony stays in the region's history");
        C(ChronicleMath.Shows(E("dip:pact", "A pact.", HistoryMath.Milestone, far, new HistoryActor(Ref.Person(7), HistoryRole.With)), us.Contains),
            "a milestone with one of the colony's people in it shows");
        C(!ChronicleMath.Shows(E("deed:traded", null, HistoryMath.Milestone, col), us.Contains), "a deed (no words of its own) never shows");
        C(!ChronicleMath.Shows(E("answer:event:minor", "Small: the council chose \"Yes\".", HistoryMath.Everyday, col), us.Contains), "a small answer doesn't show");
        C(ChronicleMath.ShownAs(E("world:camp:found", "x", HistoryMath.Milestone, col)) == ChronicleKind.World, "the colony's other milestones show as world events");

        // A season's summary in words: a key for every small kind, with and without someone.
        foreach (var key in ChronicleMath.FoldKeys())
        {
            C(keyed.TryGetValue(key, out var v) && v.Contains("{COUNT}") && (!key.EndsWith("_Of") || v.Contains("{NAME}")) && v.EndsWith("."),
                $"{key}: a sentence with the count{(key.EndsWith("_Of") ? " and the name" : "")}");
        }
        C(ChronicleMath.FoldKey(new HistoryEntry { kind = "wishMade", folded = true }, true) == "CC_ChronFold_wishMade_Of"
          && ChronicleMath.FoldKey(new HistoryEntry { kind = "answer:pact", folded = true }, false) == "CC_ChronFold_answer"
          && ChronicleMath.FoldKey(new HistoryEntry { kind = "deed:traded", folded = true }, false) == null, "a summary's words by its kind");
        C(keyed.ContainsKey("CC_ChronFold_Span") && keyed.ContainsKey("CC_ChronFold_SpanDay"), "a summary's days");

        // The view over a season and more: the first day still there, small lines folded and counted, answers with them.
        var book = new HistoryBook();
        for (int d = 0; d < 128; d++)
        {
            book.Add(HistoryMath.FromChronicle(d * Day + 100, 0, "Ana wished for a bed " + d + ".", "Human1", col));
            if (d % 10 == 0) book.Add(HistoryMath.FromChronicle(d * Day + 200, 16, "Someone died " + d + ".", "Human2", col));
            if (d % 4 == 0)
                book.Add(new HistoryEntry
                {
                    tick = d * Day + 300, kind = AnswerMath.KindFor("event"), line = "Q" + d + ": the council chose \"A\".", place = col,
                    values = AnswerMath.Values("Q" + d, "A", "It went.", false, false, false)
                });
            book.Add(new HistoryEntry { tick = d * Day + 400, kind = "deed:traded", actors = { new HistoryActor(Ref.People(3), HistoryRole.For) } }, HistoryMath.MergeTicks);
            book.Fold(d * Day + Day / 2, 0);
        }
        var shown = book.events.Where(e => ChronicleMath.Shows(e, us.Contains)).ToList();
        C(shown.Min(e => e.first) < Day, "the chronicle reaches the first day again (the save's lost everything before day 41)");
        C(shown.Where(e => e.kind == "loss").Count() == 13, "every milestone is there");
        C(shown.Where(e => e.kind == "wishMade").Sum(e => e.count) == 128, "every small line is there, as itself or counted in its season's summary");
        C(shown.Count(e => e.kind == "wishMade" && !e.folded) >= 60, "the last sixty days as written");
        C(shown.Count(e => HistoryKinds.Matches(e.kind, AnswerMath.Family)) > 0 && shown.All(e => !HistoryKinds.Matches(e.kind, HistoryKinds.Deed)), "answers are in it, deeds not");

        // ---------------- The answers' log ----------------

        var letters = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
            foreach (Match m in Regex.Matches(File.ReadAllText(f), @"public class (ChoiceLetter_\w+) : ChoiceLetter"))
                letters[m.Groups[1].Value] = f;
        var notQuestions = new[] { "ChoiceLetter_News", "ChoiceLetter_Welcome", "ChoiceLetter_WhatsNew" };
        var questions = letters.Keys.Where(k => !notQuestions.Contains(k)).OrderBy(k => k).ToList();
        C(questions.Count == AnswerMath.Sources.Length && questions.SequenceEqual(AnswerMath.Sources.Select(s => s.letter).OrderBy(k => k)),
            $"every kind of question letter has its source in the answers' log ({questions.Count}: the review counted 21, the code has {AnswerMath.Sources.Length})");
        C(AnswerMath.Sources.Select(s => s.source).Distinct().Count() == AnswerMath.Sources.Length, "each source once");
        string all = string.Join("\n", Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        foreach (var (source, _, letter) in AnswerMath.Sources)
            C(Regex.IsMatch(all, @"Answers\.Record\(""" + source + @"""|\bthis, """ + source + @""""), $"{letter}'s answers are recorded as \"{source}\"");
        C(!all.Contains("\"CC_Chronicle_Dilemma\".Translate(ctx.def.label") && !all.Contains("\"CC_Chronicle_EventChoice\".Translate("),
            "the answer's record is its chronicle line: no second \"chose\" line beside it");

        C(AnswerMath.KindFor("pact") == "answer:pact" && AnswerMath.SourceOf("answer:pact") == "pact" && AnswerMath.SourceOf("answer:event:minor") == "event"
          && AnswerMath.SourceOf("loss") == null && AnswerMath.IsMinor("answer:event:minor") && !AnswerMath.IsMinor("answer:event"), "an answer's kind, its source, a small one");
        var vals = AnswerMath.Values("A guest's complaint", "Give back the fee", "They paid it back. The guest smiled.", false, false, true);
        C(vals.Count == AnswerMath.Count && AnswerMath.Went(vals) == AnswerWent.Waiting && AnswerMath.Get(vals, AnswerMath.Outcome) == "They paid it back."
          && !AnswerMath.ByDefault(vals), "an answer that set something going waits for it; its follow-up's first sentence kept");
        C(AnswerMath.Started(vals) && AnswerMath.Went(vals) == AnswerWent.UnderWay && !AnswerMath.Started(vals), "seen under way, once");
        C(AnswerMath.Settle(vals, 1, 0, "the feast: 12 came") && AnswerMath.Went(vals) == AnswerWent.TookPlace && AnswerMath.Get(vals, AnswerMath.NoteAt) == "the feast: 12 came",
            "what came of it: it took place, with a word on it");
        var fell = AnswerMath.Values("Q", "A", null, true, false, true);
        C(AnswerMath.ByDefault(fell) && AnswerMath.Settle(fell, 0, 1, null) && AnswerMath.Went(fell) == AnswerWent.FellThrough, "nothing came of it: never came about");
        var wrong = AnswerMath.Values("Q", "A", null, false, true, true);
        C(AnswerMath.Went(wrong) == AnswerWent.WentWrong && !AnswerMath.Settle(wrong, 1, 0, null) && AnswerMath.Went(wrong) == AnswerWent.WentWrong, "a gamble gone wrong stays so");
        C(AnswerMath.Went(AnswerMath.Values("Q", "A", null, false, false, false)) == AnswerWent.AsAnswered, "nothing set going: as answered");
        string longText = string.Join(" ", Enumerable.Repeat("word", 80));
        C(AnswerMath.Short(longText).Length <= AnswerMath.OutcomeMax + 3 && AnswerMath.Short(null) == "", "a long follow-up is cut at a word");
        var dilemmaWent = Regex.Match(Read(Path.Combine("UI", "DilemmaLog.cs")), @"public enum DilemmaWent\s*\{(.*?)\}", RegexOptions.Singleline).Groups[1].Value;
        var wentNames = Regex.Matches(Regex.Replace(dilemmaWent, @"///.*", ""), @"\b([A-Z]\w*)\b").Select(m => m.Groups[1].Value).ToList();
        C(wentNames.SequenceEqual(Enum.GetNames(typeof(AnswerWent))), "an answer goes the same ways a dilemma does, in the same order (the Dilemmas page shows both)");
        foreach (var w in Enum.GetNames(typeof(AnswerWent))) C(keyed.ContainsKey("CC_Dil_Went_" + w), $"how it went has words: {w}");
        foreach (var k in new[] { "CC_Answer_Line", "CC_Answer_LineDefault", "CC_Answer_Came_TookPlace", "CC_Answer_Came_TookPlaceNote", "CC_Answer_Came_FellThrough", "CC_Answer_Came_WentWrong" })
            C(keyed.ContainsKey(k) && !keyed[k].Contains("—") && !keyed[k].Contains("!"), $"{k} in plain words");
        string answers = Read(Path.Combine("Dilemmas", "Answers.cs"));
        C(answers.Contains("catch (Exception ex)") && answers.Contains("History.Add(AnswerMath.KindFor(source, minor)"), "a record never throws into its question, and goes into the history");
        C(Read(Path.Combine("UI", "WeekMemory.cs")).Contains("if (Answers.AnyWatched(answers)) Answers.Watch(answers);"), "what an answer set going is watched only while something is");
        C(Read(Path.Combine("UI", "DilemmasPanel.cs")).Contains("book.OfKind(AnswerMath.Family)"), "the Dilemmas page lists every question's answer");

        // ---------------- The year, told once ----------------

        C(!YearLetterMath.IsYearDay(59) && YearLetterMath.IsYearDay(60) && YearLetterMath.IsYearDay(62) && !YearLetterMath.IsYearDay(63) && YearLetterMath.IsYearDay(120),
            "the year's day: the landing's anniversary, within its first days");
        C(YearLetterMath.YearOf(61) == 1 && YearLetterMath.YearOf(121) == 2 && YearLetterMath.LandingTick(2) == 60 * Day, "which year it tells, from which landing day");
        C(!YearLetterMath.Partial(false, 60 * Day + 100, 60 * Day) && YearLetterMath.Partial(false, 90 * Day, 60 * Day) && YearLetterMath.Partial(true, 60 * Day, 60 * Day),
            "counted from part of the way through only when it began late (an older save's calendar year) or midway");
        C(YearLetterMath.Parts.SequenceEqual(new[] { "opening", "people", "land", "founding", "lastWord" }), "its parts: the people, the land and the world, the founding");
        var founding = XDocument.Load(Path.Combine(root, "Defs", "IncidentDefs", "Council_Events_Living.xml")).Descendants("defName").First(d => d.Value == "Council_Evt_Founding").Parent!;
        var extraTags = founding.Descendants("extra").Elements("li").Select(l => Regex.Match(l.Value, @"^\[(\w+)\]").Groups[1].Value).ToList();
        C(extraTags.OrderBy(t => t).SequenceEqual(YearLetterMath.YearTags.Concat(YearLetterMath.DayTags).OrderBy(t => t)) && !YearLetterMath.YearTags.Intersect(YearLetterMath.DayTags).Any(),
            "the founding event's lines are shared out: the year's facts to the letter, who keeps the day tonight to the day's letter, none twice");
        string living = Read(Path.Combine("Happenings", "Workers_Living.cs"));
        living = living.Substring(Math.Max(0, living.IndexOf("class HappeningWorker_Founding", StringComparison.Ordinal)));
        string extra = Regex.Match(living, @"protected override string Extra\(HappeningContext ctx\)(.*?)\n        \}", RegexOptions.Singleline).Groups[1].Value;
        string facts = Regex.Match(living, @"public static string YearFacts\(Map map\)(.*?)\n        \}", RegexOptions.Singleline).Groups[1].Value;
        var tagsIn = new Func<string, List<string>>(body => Regex.Matches(body, @"Part\(lines, (?:ctx|null), ""(\w+)""").Select(m => m.Groups[1].Value).ToList());
        C(tagsIn(extra).OrderBy(t => t).SequenceEqual(YearLetterMath.DayTags.OrderBy(t => t)), "the founding day's letter keeps only who keeps the day: " + string.Join(",", tagsIn(extra)));
        C(tagsIn(facts).OrderBy(t => t).SequenceEqual(YearLetterMath.YearTags.OrderBy(t => t)), "the year letter has the founding facts: " + string.Join(",", tagsIn(facts)));
        string year = Read(Path.Combine("World", "YearLetter.cs"));
        string write = Regex.Match(year, @"private static void Write\(CouncilGameComponent comp, Map map, int year\)(.*?)\n        \}", RegexOptions.Singleline).Groups[1].Value;
        int iPeople = write.IndexOf("annals.PeoplePart(", StringComparison.Ordinal), iLand = write.IndexOf("Local(comp, map", StringComparison.Ordinal),
            iFound = write.IndexOf("HappeningWorker_Founding.YearFacts(", StringComparison.Ordinal), iLast = write.IndexOf("annals.LastWord(", StringComparison.Ordinal);
        C(iPeople > 0 && iLand > iPeople && iFound > iLand && iLast > iFound, "the letter: the annals' figures, then the land and the world, then the founding, then the author's last word");
        C(Regex.Matches(write, @"Tell\.Letter\(").Count == 1 && Regex.Matches(write, @"AddChronicle\(").Count == 1 && write.Contains("annals.Keep("),
            "one letter, one line in the chronicle, kept for the Chronicle's header");
        C(!write.Contains("\"CC_Year_Close\""), "it no longer sends the reader to the annals for the rest");
        string annals = Read("Annals.cs");
        C(!annals.Contains("Tell.Letter(") && !annals.Contains("AddChronicle(") && !annals.Contains("\"CC_Chronicle_Annals\""), "the annals no longer tell the year themselves");
        C(annals.Contains("Chronicle.Since(yearStartTick)") && annals.Contains("Sum(e => e.entry.count)"), "the annals count the year from the chronicle's view, a summary for all it stands for");
        foreach (var k in new[] { "CC_YearOne_Opening", "CC_YearOne_OpeningAnnalist", "CC_YearOne_OpeningPartial", "CC_YearOne_OpeningNobody", "CC_YearOne_PartPeople", "CC_YearOne_PartLand",
                     "CC_YearOne_PartFounding", "CC_YearOne_Chronicle", "CC_YearOne_Button" })
            C(keyed.ContainsKey(k), $"{k} has words");
        C(keyed["CC_YearOne_OpeningPartial"].Contains("{SINCE}"), "a year counted from part of the way through says since when");

        // ---------------- Less noise ----------------

        string social = Read(Path.Combine("Social", "SocialWeb.cs"));
        string comrades = Regex.Match(social, @"private void Comrades\((.*?)\n        \}", RegexOptions.Singleline).Groups[1].Value;
        C(!comrades.Contains("\"CC_Tie_Battle\"") && comrades.Contains("if (parts.Count == 0) return;") && comrades.Contains("\"CC_Tie_BattleCarried\""),
            "a fight's comrades are told only when something changed (new comrades, someone carried out), not the roster after every raid");
        string offices = Read(Path.Combine("Offices", "Offices.cs"));
        string settle = Regex.Match(offices, @"private void Settle\((.*?)\n        \}", RegexOptions.Singleline).Groups[1].Value;
        C(!settle.Contains("AddChronicle(") && offices.Contains("ChronicleSeats(comp, round);"), "an election is one line in the chronicle, every seat in it");
        string dyn = Read("CouncilDynamics.cs");
        string anniv = Regex.Match(dyn, @"private void Anniversaries\(\)(.*?)\n        \}", RegexOptions.Singleline).Groups[1].Value;
        C(anniv.Contains("marked.GroupBy(m => m.years)"), "those who mark the same years on the same day are one line");
        string cal = Read(Path.Combine("Calendar", "CalendarKeeping.cs"));
        C(cal.Contains("else if (!quiet) comp?.AddChronicle(") && cal.Contains("line: kept, weight: HistoryMath.Milestone"),
            "a day kept is one line: its letter's answer, or the founding event's");
        foreach (var k in new[] { "CC_Tie_BattleNew", "CC_Tie_BattleNewMany", "CC_Tie_BattlePair", "CC_Tie_BattleCarried", "CC_Chronicle_SeatAs", "CC_Chronicle_ElectedAll", "CC_Chronicle_ElectedBy", "CC_PM_Chronicle_NamedAll" })
            C(keyed.ContainsKey(k), $"{k} has words");

        return n;
    }
}
