using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Faith among the colony's people (Faith/FaithPeopleMath.cs): what a talk about faith does and how much, who comes out
/// of a friendly debate well, the limits on how often talk moves anyone, a view about faith kept in step with certainty,
/// how hard a crisis shakes and when one comes; and the defs (crisis marks with their shake, the talk lines' tags).
/// </summary>
public static class FaithPeopleTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "faith people: " + what);
        }

        // ---------- What a talk does ----------
        Check(FaithPeopleMath.Move(TalkOutcome.Agree, false, false) == FaithTalkMove.Sway, "agreeing across faiths lets the devout one's word land");
        Check(FaithPeopleMath.Move(TalkOutcome.Debate, false, true) == FaithTalkMove.Sway && FaithPeopleMath.Move(TalkOutcome.Debate, false, false) == FaithTalkMove.None,
            "a debate sways only when it's friendly");
        Check(FaithPeopleMath.Move(TalkOutcome.Argue, false, true) == FaithTalkMove.DigIn, "a heated argument sways no one: both dig in");
        Check(FaithPeopleMath.Move(TalkOutcome.Agree, true, false) == FaithTalkMove.Steady && FaithPeopleMath.Move(TalkOutcome.Debate, true, true) == FaithTalkMove.Steady,
            "two of one faith steady each other");
        Check(FaithPeopleMath.Move(TalkOutcome.Argue, true, false) == FaithTalkMove.None && FaithPeopleMath.Move(TalkOutcome.Tease, false, true) == FaithTalkMove.None,
            "a quarrel within a faith, or a tease, does nothing for it");
        Check(FaithPeopleMath.SwayBase(TalkOutcome.Debate) > FaithPeopleMath.SwayBase(TalkOutcome.Agree) && FaithPeopleMath.SwayBase(TalkOutcome.Agree) >= 0.03f
              && FaithPeopleMath.SwayBase(TalkOutcome.Debate) <= 0.05f && FaithPeopleMath.SwayBase(TalkOutcome.Argue) == 0f,
            "a talk's sway is 3 to 5%, a friendly debate livelier than agreeing");
        Check(FaithPeopleMath.DigIn > 0f && FaithPeopleMath.DigIn <= 0.02f && FaithPeopleMath.SameFaithSteady <= 0.02f, "digging in and steadying are small");

        // ---------- Who comes out of it well ----------
        float devoutLiked = FaithPeopleMath.Persuasion(1f, true, 60f), devoutPlain = FaithPeopleMath.Persuasion(1f, true, 0f), plain = FaithPeopleMath.Persuasion(1f, false, 0f);
        Check(devoutLiked > devoutPlain && devoutPlain > plain, "the devout and the liked are the more persuasive");
        Check(FaithPeopleMath.Persuasion(1.5f, true, 0f) > devoutPlain && FaithPeopleMath.Persuasion(0f, true, 100f) == 0f, "the game's conversion power counts; none, no persuasion");
        Check(Math.Abs(FaithPeopleMath.FirstWins(1f, 1f) - 0.5f) < 0.001f && FaithPeopleMath.FirstWins(3f, 1f) > 0.7f && FaithPeopleMath.FirstWins(0f, 0f) == 0.5f,
            "a debate is won by persuasion, with luck");

        // ---------- How often ----------
        int day = FaithPeopleMath.DayTicks;
        Check(FaithPeopleMath.Ready(-1, 100, day) && !FaithPeopleMath.Ready(10 * day, 10 * day + day / 2, day) && FaithPeopleMath.Ready(10 * day, 11 * day, day),
            "once a day means once a day");
        Check(FaithPeopleMath.PairGapTicks >= FaithPeopleMath.ListenerGapTicks && FaithPeopleMath.ListenerGapTicks >= day, "a pair waits longer than a listener, a listener a day");
        Check(FaithPeopleMath.Ready(5 * day, 2 * day, day), "a clock that went back (a new game) never blocks");

        // A season of deliberate effort: an open, wavering listener fond of a devout speaker, one good talk a day at most.
        float perTalk = FaithMath.Sway(FaithPeopleMath.DebateSway, 1f, 50f, 0f, 50f, false);
        int talks = (int)Math.Ceiling(0.55f / perTalk);
        Check(talks >= 6 && talks <= 15, $"a wavering, open listener who likes a devout speaker comes round over several good talks ({talks})");
        float proud = FaithMath.Sway(FaithPeopleMath.AgreeSway, 1f, -40f, 80f, 0f, false);
        Check(proud < 0.02f, $"a proud, closed believer barely moves ({proud:0.000} a talk)");

        // ---------- A view about faith beside certainty ----------
        Check(FaithPeopleMath.ViewWithCertainty(0.8f, 0.4f) < TalkMath.SideAt, "someone wavering never reads as holding firmly to the faith");
        Check(FaithPeopleMath.ViewWithCertainty(-0.7f, 0.95f) > -TalkMath.SideAt, "someone devout never reads as doubting it");
        Check(FaithPeopleMath.ViewWithCertainty(-0.7f, 0.4f) == -0.7f && FaithPeopleMath.ViewWithCertainty(0.6f, 0.95f) == 0.6f && FaithPeopleMath.ViewWithCertainty(0.5f, 0.7f) == 0.5f,
            "a view that already fits, or a steady believer's, is their own");

        // ---------- A crisis ----------
        Check(FaithPeopleMath.CrisisShake(0.2f, 1f) == 0.2f && FaithPeopleMath.CrisisShake(0.2f, 1.6f) == FaithPeopleMath.MaxShake && FaithPeopleMath.CrisisShake(0.12f, 0.4f) == FaithPeopleMath.MinShake,
            "a crisis shakes by nature, between 10 and 30%");
        Check(FaithPeopleMath.CrisisShake(0f, 1f) == 0f, "no shake, no crisis");
        Check(FaithPeopleMath.CrisisReady(-1, 0) && !FaithPeopleMath.CrisisReady(day, 10 * day) && FaithPeopleMath.CrisisReady(day, 22 * day), "no two crises within twenty days");
        Check(FaithPeopleMath.BurdenStep(3, true, true) == 4 && FaithPeopleMath.BurdenStep(3, true, false) == 1 && FaithPeopleMath.BurdenStep(3, false, false) == 0,
            "a faith's demand is counted while the same one weighs, day after day");
        Check(!FaithPeopleMath.BurdenDue(4, -8f) && FaithPeopleMath.BurdenDue(5, -4f) && !FaithPeopleMath.BurdenDue(9, -2f), "five days of a real weight, not a passing one");
        Check(FaithPeopleMath.NearDeath(0.5f, 0f, -1f) && FaithPeopleMath.NearDeath(0f, 0.8f, -1f) && FaithPeopleMath.NearDeath(0f, 0f, 4f), "close to death: blood, a lethal illness or wound, bleeding out");
        Check(!FaithPeopleMath.NearDeath(0.3f, 0.5f, 30f) && !FaithPeopleMath.NearDeath(0f, 0f, -1f), "a bad day isn't a brush with death");
        int due = FaithPeopleMath.DueAfter(1f, 3f, 0.5f);
        Check(due == 2 * day && FaithPeopleMath.DueAfter(1f, 3f, 2f) == 3 * day, "felt some days after, never later than said");
        Check(FaithPeopleMath.State(0.9f) == "devout" && FaithPeopleMath.State(0.4f) == "wavering" && FaithPeopleMath.State(0.7f) == "steady", "devout, wavering or steady");

        // ---------- The defs ----------
        var marks = XDocument.Load(Path.Combine(root, "Defs", "PersonaDefs", "Rimfolk_PersonaMarks_Faith.xml")).Root!.Elements("ColonyCouncil.PersonaMarkDef").ToList();
        Check(marks.Count >= 4, $"crises of faith ({marks.Count})");
        var families = new HashSet<string>();
        foreach (var m in marks)
        {
            string name = m.Element("defName")?.Value ?? "?";
            var lis = m.Element("modExtensions")?.Elements("li").ToList() ?? new List<XElement>();
            var mark = lis.FirstOrDefault(li => (string?)li.Attribute("Class") == "ColonyCouncil.PersonaMarkExtension");
            var shake = lis.FirstOrDefault(li => (string?)li.Attribute("Class") == "ColonyCouncil.FaithShake");
            families.Add(mark?.Element("family")?.Value ?? name);
            float s = float.TryParse(shake?.Element("shake")?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f;
            Check(s >= FaithPeopleMath.MinShake && s <= FaithPeopleMath.MaxShake, name + " shakes certainty between 10 and 30% before nature");
            Check(m.Element("major")?.Value == "true", name + " is a turning point");
            float chance = float.TryParse(mark?.Element("chance")?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float c) ? c : 1f;
            int repeat = int.TryParse(mark?.Element("repeatDays")?.Value, out int r) ? r : 0;
            Check(chance <= 0.5f && repeat >= 60, name + " is rare: half the time at most, and not again for two months");
            Check(!string.IsNullOrEmpty(shake?.Element("note")?.Value), name + " says what caused it on the faith pages");
        }
        // The families the code names are all there.
        string code = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Faith", "FaithCrisis.cs"));
        foreach (Match mt in Regex.Matches(code, @"(Loss|Rescued|Burden|NearDeath) = ""([A-Za-z]+)"""))
            Check(families.Contains(mt.Groups[2].Value), "the crisis family " + mt.Groups[2].Value + " has marks");

        // The talk lines: enough of them, each turn written for the moves.
        var pools = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Talk_Lines_Faith.xml")).Root!.Elements("ColonyCouncil.TalkLinesDef").ToList();
        var lines = pools.SelectMany(p => p.Element("lines")!.Elements().SelectMany(t => t.Elements("li")).Select(li => li.Value)).ToList();
        Check(lines.Count >= 60 && lines.Count <= 140, $"faith talk lines ({lines.Count})");
        foreach (var tag in new[] { "persuading", "persuaded", "unmoved", "preachedat", "digin", "steadied", "faith:wavering", "faith:devout", "faith:alone", "faith:minority" })
            Check(lines.Count(l => Regex.IsMatch(l, @"^\[[^\]]*\b" + Regex.Escape(tag) + @"\b")) >= 2, "lines written for [" + tag + "]");
        Check(lines.All(l => !l.Contains("{SUBJECT") || Regex.IsMatch(l, @"^\[[^\]]*\b(myfaith|theirfaith)\b")), "a line naming a faith says whose it is");
        Check(lines.All(l => !Regex.Replace(l, @"^\[[^\]]*\]\s*", "").Contains("!")), "no exclamation marks");
        return cases;
    }
}
