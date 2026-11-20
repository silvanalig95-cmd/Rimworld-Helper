using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The council's integrations, part 1 (the content update's A1, A2, A5, A6 and A21, 20 November 2026;
/// Offices/SeatMath.cs): the speaker's hour and whom it is for, the quartermaster's count and what is shortest, the reeve's
/// and the marshal's days, a promise going badly halfway by the vote's own test and the hands each seat's work takes, the
/// meeting's ayes, the weight of office's hard call and its quiet end, the questions of the seat's work in a promise's
/// values; and the defs (every seat a question names, its holder's words for every seat, the [seat] variants, the
/// halfway question's tags, the story's stages) and the seams in the code.
/// </summary>
public static class SeatWorkTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "council seats: " + what);
        }

        Rules(Check);
        Halfway(Check);
        Records(Check);
        Defs(root, Check);
        Seams(root, Check);
        return cases;
    }

    private static void Rules(Action<bool, string> Check)
    {
        // The speaker's hour.
        Check(SeatMath.HeardTrust(0) == 1f && SeatMath.HeardTrust(20) == 2f && Math.Abs(SeatMath.HeardTrust(10) - 1.5f) < 0.001f && SeatMath.HeardTrust(30) == 2f,
            "an hour with the speaker gives back one to two points of trust, by the speaker's Social");
        Check(SeatMath.Lowest(new[] { 30f, -20f, 5f }, null) == 1, "the speaker sits with whoever trusts the council least");
        Check(SeatMath.Lowest(new[] { 30f, -20f, 5f }, new[] { true, false, true }) == 2, "someone heard lately isn't heard again yet: the next lowest");
        Check(SeatMath.Lowest(new[] { 30f, 12f, SeatMath.HearBelow }, null) == -1, "nobody under the mark: no sitting");
        Check(SeatMath.Lowest(new float[0], null) == -1, "nobody at all");

        // The quartermaster's count.
        Check(SeatMath.Shortest(30f, 3f, 4f) == StoresShort.None, "every store at its mark: nothing is short");
        Check(SeatMath.Shortest(30f, 0.5f, 4f) == StoresShort.Medicine, "half a dose each is the shortest");
        Check(SeatMath.Shortest(3f, 3f, 4f) == StoresShort.Food, "three days of food is the shortest");
        Check(SeatMath.Shortest(30f, 3f, 0.2f) == StoresShort.Meals, "a fifth of a meal each is the shortest");
        Check(SeatMath.Shortest(5f, 1f, 1f) == StoresShort.Food, "the store furthest under its mark is named (a third of the food's mark against half)");
        Check(SeatMath.CountEvery == 7f, "a count every week, as the seat's own promise says");

        // The reeve and the marshal.
        Check(!SeatMath.ReeveDue(false, false, 99f), "out of the growing season and no blight: no walk");
        Check(!SeatMath.ReeveDue(true, false, 1f) && SeatMath.ReeveDue(true, false, SeatMath.ReeveEvery), "in the growing season, every other day");
        Check(SeatMath.ReeveDue(false, true, 0.6f) && !SeatMath.ReeveDue(true, true, 0.2f), "blight not yet marked: the next morning, even out of season");
        Check(SeatMath.MarshalDue(0.5f, 99f) && SeatMath.MarshalDue(2f, 1f), "the two days after an attack, an evening each");
        Check(!SeatMath.MarshalDue(2.5f, 99f) && !SeatMath.MarshalDue(1f, 0.5f) && !SeatMath.MarshalDue(-1f, 99f), "not after two days, not twice an evening, not with no attack");

        // Any other business.
        Check(SeatMath.AyeToDecree(2, false) && SeatMath.AyeToDecree(1, false) && !SeatMath.AyeToDecree(0, false) && !SeatMath.AyeToDecree(-1, false),
            "for a decree: those who like it say aye");
        Check(SeatMath.AyeToDecree(-2, true) && !SeatMath.AyeToDecree(0, true) && !SeatMath.AyeToDecree(1, true), "to lift one: those who dislike it say aye");
        Check(SeatMath.AyeToWish(true, false, -50) && SeatMath.AyeToWish(false, true, -50) && SeatMath.AyeToWish(false, false, 20) && !SeatMath.AyeToWish(false, false, 19),
            "a petition of wishes: those in it, those who want the same, and friends of the asker");
        Check(SeatMath.Backed(4, 7) && !SeatMath.Backed(3, 6) && !SeatMath.Backed(0, 0), "backed: more ayes than not");

        // The weight of office's hard call.
        Check(SeatMath.IsCall(SeatMath.CallQuestion) && SeatMath.IsCall(SeatMath.CallDecree) && SeatMath.IsCall(SeatMath.CallSupper) && !SeatMath.IsCall(null) && !SeatMath.IsCall("won"),
            "the three real calls");
        Check(SeatMath.WaitsForCall(SeatMath.CallStage, null) && !SeatMath.WaitsForCall(SeatMath.CallStage, SeatMath.CallDecree) && !SeatMath.WaitsForCall(0, null),
            "the doubtful days wait for a call, and only they");
        Check(SeatMath.EndsQuietly(SeatMath.CallStage, null, 20f) && !SeatMath.EndsQuietly(SeatMath.CallStage, null, 19.9f) && !SeatMath.EndsQuietly(SeatMath.CallStage, SeatMath.CallQuestion, 30f),
            "no call in twenty days ends it quietly; a call came, it goes on");
    }

    private static void Halfway(Action<bool, string> Check)
    {
        Check(SeatMath.HandsFor(PromiseTest.Lost) == SeatMath.Hands.Watch && SeatMath.HandsFor(PromiseTest.Food) == SeatMath.Hands.Food
              && SeatMath.HandsFor(PromiseTest.Medicine) == SeatMath.Hands.Sick && SeatMath.HandsFor(PromiseTest.Sickbeds) == SeatMath.Hands.Sick
              && SeatMath.HandsFor(PromiseTest.Herd) == SeatMath.Hands.Herd && SeatMath.HandsFor(PromiseTest.Mood) == SeatMath.Hands.Evening,
            "each seat's promise has its own hands: the walls, the food, the sick, the herd, an evening");
        foreach (var t in new[] { PromiseTest.None, PromiseTest.Trust, PromiseTest.Quarrels, PromiseTest.Days, PromiseTest.Faith, PromiseTest.Work })
            Check(SeatMath.HandsFor(t) == SeatMath.Hands.None && !SeatMath.GoingBadly(t, 10f, -50f, 40f), $"a {t} promise isn't raised halfway (hands can't help it)");
        // "A proper watch at night", and someone lost since: raised halfway, not before.
        Check(SeatMath.GoingBadly(PromiseTest.Lost, 3f, 4f, 30f) && !SeatMath.GoingBadly(PromiseTest.Lost, 3f, 4f, 20f), "someone lost after a watch was promised: raised from 25 days on");
        Check(!SeatMath.GoingBadly(PromiseTest.Lost, 3f, 3f, 30f), "nobody lost: nothing to raise");
        // "Meals put by", and the food running out, by the vote's own test.
        Check(SeatMath.GoingBadly(PromiseTest.Food, 8f, 2f, 30f) && !SeatMath.GoingBadly(PromiseTest.Food, 8f, 4f, 30f) && !SeatMath.GoingBadly(PromiseTest.Food, 1f, 6f, 30f),
            "the food under three days is going badly; four days says nothing yet; better than then is kept");
        Check(SeatMath.GoingBadly(PromiseTest.Herd, 9f, 6f, 26f) && !SeatMath.GoingBadly(PromiseTest.Herd, 9f, 8.5f, 26f), "the herd clearly shrunk, not a beast less");
        Check(SeatMath.GoingBadly(PromiseTest.Mood, 0.5f, 0.25f, 26f) && SeatMath.GoingBadly(PromiseTest.Medicine, 2f, 0.5f, 26f) && SeatMath.GoingBadly(PromiseTest.Sickbeds, 1f, 0f, 26f),
            "low spirits, the medicine short, no sickbed: all raised halfway");
        // Each going badly is exactly what the vote would call broken.
        foreach (var t in new[] { PromiseTest.Lost, PromiseTest.Food, PromiseTest.Medicine, PromiseTest.Sickbeds, PromiseTest.Herd, PromiseTest.Mood })
            for (float before = 0f; before <= 10f; before += 2.5f)
            for (float after = 0f; after <= 10f; after += 1.25f)
                Check(SeatMath.GoingBadly(t, before, after, 30f) == (RecordMath.Judge(t, before, after, 30f) == PromiseVerdict.Broken),
                    $"halfway uses the vote's own test ({t} {before} -> {after})");
    }

    private static void Records(Action<bool, string> Check)
    {
        var v = RecordMath.Values("Council_Office_Reeve", 0, "seed", PromiseTest.Work, 40f, 40f, "to walk the fields every morning", 3f, "12,45");
        Check(RecordMath.Number(v, RecordMath.CallsAt) == 3f && RecordMath.RivalsOf(v).SequenceEqual(new[] { 12, 45 }) && RecordMath.Get(v, RecordMath.PromiseAt) == "to walk the fields every morning",
            "a promise keeps the questions spoken in then, and who stood against them");
        var old = RecordMath.Values("Council_Office_Reeve", 0, "seed", PromiseTest.Work, 40f, 40f, "to walk the fields every morning").Take(7).ToList();
        Check(RecordMath.Number(old, RecordMath.CallsAt) == 0f && RecordMath.RivalsOf(old).Count == 0, "an older promise: none spoken in then, nobody known to have stood against them");
        Check(SeatMath.CallsSince(5f, 3f) == 2 && SeatMath.CallsSince(3f, 5f) == 0, "the questions since the promise");
        Check(RecordMath.Record(true, 0 + 2, 0, false, false) == RecordMath.RecordKind.InSeat && RecordMath.WorkLeanOf(true, 0 + 2) == RecordMath.WorkLean,
            "a seat with no work counted runs on the questions it spoke in, and leans voters as work does");
    }

    // ---------------- The defs ----------------

    private static readonly string[] Seats = { "Physician", "Reeve", "Herdmaster", "Peacemaker", "Chaplain", "Warden", "Quartermaster", "Keeper" };

    private static void Defs(string root, Action<bool, string> Check)
    {
        var files = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).ToDictionary(f => f, f => XDocument.Load(f).Root!);
        var all = files.Values.SelectMany(r => r.Elements()).ToList();
        var offices = all.Where(e => e.Name.LocalName == "ColonyCouncil.OfficeDef").Select(e => e.Element("defName")?.Value).ToHashSet();
        var dilemmas = all.Where(e => e.Name.LocalName == "ColonyCouncil.DilemmaDef").ToList();
        var withSeat = dilemmas.Where(d => d.Element("seat") != null).ToList();
        Check(withSeat.Count >= 30, $"thirty or more council questions name the seat whose work they are about ({withSeat.Count})");
        foreach (var d in withSeat)
            Check(offices.Contains(d.Element("seat")!.Value), $"{d.Element("defName")?.Value}: its seat {d.Element("seat")!.Value} exists");
        foreach (var s in Seats.Where(s => s != "Keeper"))
            Check(withSeat.Count(d => d.Element("seat")!.Value == "Council_Office_" + s) >= 3, $"three or more questions of the {s}'s work");

        // The holder's word for every seat a question names (and the keeper's, for the customs).
        var word = all.FirstOrDefault(e => e.Element("defName")?.Value == "Rimfolk_Pool_SeatWord");
        var wordLines = word?.Descendants("li").Select(l => l.Value).ToList() ?? new List<string>();
        foreach (var s in Seats)
            Check(wordLines.Any(l => l.StartsWith("[seat:" + s + "]")), $"the {s} has a word of their own for a question of the seat's work");
        Check(wordLines.Any(l => !l.StartsWith("[")), "and a word that fits any seat");
        foreach (var l in wordLines)
            Check(l.Contains("{HOLDER_nameDef}") && l.Contains("{OFFICE}") && !Regex.IsMatch(l, @"\{(A|B|X)_"), $"the holder's word names the holder and the seat, nobody else's part: '{Short(l)}'");

        // The [seat] variants: only for questions with a seat; each opening and outcome names the holder and the seat.
        var seated = withSeat.Select(d => d.Element("defName")!.Value).ToHashSet();
        var variants = all.Where(e => e.Name.LocalName == "ColonyCouncil.LetterVariantsDef" && e.Descendants("li").Any(l => l.Value.StartsWith("[seat"))).ToList();
        Check(variants.Count >= 24, $"two dozen questions have openings and outcomes of their own for the seat ({variants.Count})");
        int lines = 0;
        foreach (var v in variants)
        {
            string target = v.Element("dilemma")?.Value ?? "";
            Check(seated.Contains(target), $"{v.Element("defName")?.Value}: [seat] lines only for a question with a seat ({target})");
            foreach (var l in v.Descendants("li").Where(l => !l.HasElements && l.Value.StartsWith("[seat")))
            {
                lines++;
                bool opening = l.Parent?.Name.LocalName == "texts";
                Check(l.Value.Contains("{HOLDER_nameDef}") && (!opening || l.Value.Contains("{OFFICE}")),
                    $"{target}: a [seat] line names the holder (an opening the seat too): '{Short(l.Value)}'");
            }
        }
        Check(lines + wordLines.Count >= 60, $"about sixty openings and outcomes give the seats a voice ({lines + wordLines.Count})");

        // The halfway question: a text for each kind of hands, and for one who stood against them; its answers' effects.
        var half = dilemmas.FirstOrDefault(d => d.Element("defName")?.Value == "Rimfolk_Dilemma_SeatHalfway");
        Check(half != null && half.Element("workerClass")?.Value == "ColonyCouncil.DilemmaWorker_SeatHalfway", "the halfway question exists with its worker");
        var texts = half?.Element("texts")?.Elements("li").Select(l => l.Value).ToList() ?? new List<string>();
        foreach (var h in Enum.GetValues<SeatMath.Hands>().Where(h => h != SeatMath.Hands.None))
        {
            string tag = "[hands:" + h.ToString().ToLowerInvariant() + "]";
            Check(texts.Any(t => t.StartsWith(tag)), $"the halfway question opens for {tag}");
            Check(half?.Descendants("li").Any(l => !l.HasElements && l.Value.StartsWith(tag) && l.Parent?.Name.LocalName == "outcomes") ?? false, $"and its hands answer tells {tag}");
        }
        Check(texts.Any(t => t.StartsWith("[stood]")) && texts.Any(t => !t.StartsWith("[")), "it opens for one who stood against the holder, and for anyone");
        Check(texts.All(t => t.Contains("{PROMISE}") && t.Contains("{EVIDENCE}") && t.Contains("{OFFICE}")), "every opening says the promise, what the map shows of it and the seat");
        var opts = half?.Element("options")?.Elements("li").ToList() ?? new List<XElement>();
        Check(opts.Count == 3 && half!.Element("defaultOption")?.Value == "2", "three answers, leaving it to the vote by default");
        Check(opts.Count == 3 && opts[0].Descendants("li").Any(e => (string?)e.Attribute("Class") == "ColonyCouncil.DilemmaEffect_SeatHands")
              && opts[1].Descendants("li").Any(e => (string?)e.Attribute("Class") == "ColonyCouncil.DilemmaEffect_SeatSupper"),
            "hands and the supper are set going on the map");

        // The weight of office: the doubtful days wait for a real call, the hard call names it, a quiet end, the lost seat's mood.
        var arc = all.FirstOrDefault(e => e.Element("defName")?.Value == "Council_Arc_WeightOfOffice");
        var stages = arc?.Element("stages")?.Elements("li").ToList() ?? new List<XElement>();
        Check(stages.Count == 4 && stages[SeatMath.CallStage].Element("gate")?.Element("kind")?.Value == "Custom", "the doubtful days wait for the call (a Custom gate)");
        var hard = stages.Count > 2 ? stages[2] : null;
        foreach (var k in new[] { SeatMath.CallQuestion, SeatMath.CallDecree, SeatMath.CallSupper })
        {
            Check(hard?.Element("journal")?.Descendants("li").Any(l => l.Value.StartsWith("[" + k + "]")) ?? false, $"the hard call's journal for {k}");
            Check(hard?.Element("chronicle")?.Elements("li").Any(l => l.Value.StartsWith("[" + k + "]")) ?? false, $"and its chronicle");
        }
        Check(hard?.Descendants("li").Where(l => !l.HasElements).All(l => l.Value.Contains("{CALL}")) ?? false, "every line of the hard call names it");
        Check(hard?.Descendants("li").Where(l => !l.HasElements).All(l => !Regex.IsMatch(l.Value, @"(^|\]\s*)\{CALL\}")) ?? false, "no line begins with the call's words");
        Check(arc?.Element("fizzle")?.Descendants("li").Any(l => l.Value.StartsWith("[" + SeatMath.NoCall + "]")) ?? false, "a quiet end of its own");
        Check(arc?.Element("fizzleChronicle")?.Elements("li").Any(l => l.Value.StartsWith("[" + SeatMath.NoCall + "]")) ?? false, "and its chronicle line");
        var fe = arc?.Element("fizzleEffects")?.Elements("li").ToList() ?? new List<XElement>();
        Check(fe.Count == 1 && (string?)fe[0].Attribute("Class") == "ColonyCouncil.ArcEffect_Cast" && fe[0].Element("ifTag")?.Value == SeatMath.OfficeGone,
            "the mood of a seat lost too soon only when the seat was lost");
    }

    private static string Short(string s) => s.Length <= 60 ? s : s.Substring(0, 60) + "...";

    // ---------------- The seams ----------------

    private static void Seams(string root, Action<bool, string> Check)
    {
        string Src(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        Check(Src("Interplay/Interplay.cs").Contains("SeatsDaily(comp)") && Src("Interplay/Interplay.cs").Contains("SeatsHourly"), "the seats' acts ride on the council's day and hour");
        Check(Src("PlayOut/OnMapPlays.cs").Contains("Interplay.SeatWorkOver(p, what, happened)") && Src("PlayOut/OnMapPlays.cs").Contains("Interplay.AtLookOut(p, h.pawn)"),
            "a seat's walk ends in its own words, and the marshal speaks at the look-outs");
        Check(Src("PlayOut/PlayRoutes.cs").Contains("Interplay.AtField(") && Src("PlayOut/PlayRoutes.cs").Contains("Stores = \"stores\", Fields = \"fields\""),
            "the rounds of the stores and the fields, the blight marked at each field");
        Check(Src("PlayOut/PlayGroups.cs").Contains("!Interplay.IsSeatWork(p)) Stage.Held") && Src("PlayOut/PlayOutTracker.cs").Contains("!Interplay.IsSeatWork(p) && !Stage.Room"),
            "a seat at its work is no gathering and waits for none");
        Check(Src("PlayOut/PlayWords.cs").Contains("Interplay.SeatBeginText"), "a seat's walk begins in words for one person");
        string dil = Src("Dilemmas/Dilemmas.cs");
        Check(dil.Contains("ctx.holder = SeatCalls.HolderFor(ctx)") && dil.Contains("SeatCalls.Answered(ctx)") && dil.Contains("Scribe_References.Look(ref holder, \"holder\")"),
            "a question picks its seat's holder, saves them with the letter, and counts the answer toward their record");
        Check(Src("Dilemmas/DilemmaChoices.cs").Contains("SeatCalls.AddTags(ctx, tags)"), "the [seat] tags for openings and outcomes");
        Check(Src("Decrees/Decrees.cs").Contains("SeatCalls.Notify_Decree(this, def, asker)"), "a decree backed over someone's objection is a hard call");
        Check(Src("Ambitions/CouncilMeeting.cs").Contains("MeetingBusiness.Next(this, here)") && Src("Ambitions/Ambitions.cs").Contains("MeetingBusiness.LetterParagraph(m)"),
            "any other business at the meeting and in its letter");
        Check(Src("UI/DecreePanel.cs").Contains("MeetingBusiness.Tally(req)") && Src("UI/InboxProviders.cs").Contains("MeetingBusiness.Tally(r)")
              && Src("UI/InboxProviders.cs").Contains("MeetingBusiness.Tally(w)") && Src("UI/InboxProviders.cs").Contains("\"stores-count\""),
            "the tally on the Decrees page and This week, and the stores' count on This week");
        Check(Src("Offices/SeatRecords.cs").Contains("SeatCalls.Count(winner)") && Src("Offices/SeatRecords.cs").Contains("WorkLeanOf(true, work + calls)"),
            "the next election reads the questions spoken in");
        Check(Src("Arcs/ArcWorkers_Lives.cs").Contains("SeatMath.IsCall(arc.tag)") && Src("Arcs/ArcText.cs").Contains("Named(\"CALL\")"), "the story's gate and its {CALL}");
    }
}
