using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// An election that plays out on the map (Offices/ElectionMath.cs): the poll is the vote math without the luck of the
/// day and adds up to the voters; a candidate's visits move a voter by their Social and the voter's liking, within a cap;
/// a showing at the hustings follows Social, mood and nature; the council's backing follows trust; the count reads every
/// vote exactly once, in handfuls when there are many; ranked ballots go to the next choice still standing; the days come
/// in order. And the defs: every office has planks, each plank's lines exist for the canvass and the hustings, the
/// words are said in one sentence, and the new pools and setting are wired.
/// </summary>
public static class ElectionTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "election: " + what);
        }

        bool Near(float a, float b, float eps = 0.02f) => Math.Abs(a - b) <= eps;

        // ---------- The poll: shares of one voter ----------
        var even = ElectionMath.Shares(new[] { 3f, 3f });
        Check(Near(even[0], 0.5f) && Near(even[1], 0.5f), "two equal leans split evenly");
        var three = ElectionMath.Shares(new[] { 5f, 5f, 5f });
        Check(three.All(x => Near(x, 1f / 3f)), "three equal leans: a third each");
        var far = ElectionMath.Shares(new[] { 10f, 2f });
        Check(Near(far[0], 1f) && Near(far[1], 0f), "a lead larger than the luck of the day is certain");
        // Two candidates, a lead of d under uniform noise 0..N: the leader wins 1 - (N - d)^2 / (2 N^2).
        float n = ElectionMath.VoteNoise, d = 1f;
        var close = ElectionMath.Shares(new[] { 4f + d, 4f });
        Check(Near(close[0], 1f - (n - d) * (n - d) / (2f * n * n)), $"a small lead wins as the arithmetic says ({close[0]:0.000})");
        for (int k = 0; k < 200; k++)
        {
            var leans = Enumerable.Range(0, 1 + k % 3).Select(i => (float)((k * 7 + i * 13) % 11) / 2f).ToArray();
            var s = ElectionMath.Shares(leans);
            Check(Near(s.Sum(), 1f, 0.001f) && s.All(x => x >= 0f && x <= 1.0001f), $"shares are chances that add to one ({string.Join(",", leans)})");
            int lead = ElectionMath.Leader(leans);
            Check(s[lead] >= s.Max() - 0.0001f, "the one leaned to most has the best chance");
        }
        Check(ElectionMath.Shares(new float[0]).Length == 0 && ElectionMath.Shares(new[] { 1f })[0] == 1f, "nobody, or one alone");

        // ---------- The poll over voters, and in whole votes ----------
        var poll = ElectionMath.Poll(new List<float[]> { new[] { 9f, 1f }, new[] { 1f, 9f }, new[] { 9f, 1f } }, 2);
        Check(Near(poll[0], 2f) && Near(poll[1], 1f), "the poll counts each voter's likely vote");
        Check(ElectionMath.Poll(new List<float[]> { new[] { 1f } }, 2).Sum() == 0f, "a voter with the wrong number of leans is left out");
        for (int v = 1; v < 30; v += 3)
        {
            var whole = ElectionMath.Rounded(new[] { v * 0.37f, v * 0.41f, v * 0.22f }, v);
            Check(whole.Sum() == v, $"the poll in whole votes adds up to the {v} voters");
        }
        Check(ElectionMath.Rounded(new[] { 2.6f, 1.4f }, 4).SequenceEqual(new[] { 3, 1 }), "rounded by the largest remainder");

        // ---------- The campaign ----------
        Check(ElectionMath.CanvassNudge(15, 0f) > ElectionMath.CanvassNudge(3, 0f), "a good talker moves a voter more");
        Check(ElectionMath.CanvassNudge(8, 60f) > ElectionMath.CanvassNudge(8, 0f), "a friend moves a voter more");
        Check(ElectionMath.CanvassNudge(8, -60f) < 0f, "someone the voter dislikes puts them off");
        Check(ElectionMath.CanvassNudge(20, 100f) <= ElectionMath.CanvassCap && ElectionMath.CanvassNudge(0, 0f) > 0f, "a visit is something, and never past the cap");
        float held = 0f;
        for (int k = 0; k < 20; k++) held = ElectionMath.AddNudge(held, ElectionMath.CanvassNudge(20, 100f));
        Check(Near(held, ElectionMath.CanvassCap, 0.0001f), "visit after visit stops at the cap");
        held = 0f;
        for (int k = 0; k < 20; k++) held = ElectionMath.AddNudge(held, ElectionMath.CanvassNudge(20, -100f));
        Check(Near(held, -ElectionMath.CanvassCap, 0.0001f), "and putting off stops at the cap too");
        Check(ElectionMath.CanvassCap <= ElectionMath.VoteNoise, "a campaign moves votes about as much as the luck of the day, no more");

        // ---------- The hustings ----------
        Check(ElectionMath.Showing(16, 0.8f, 0.4f, 0.6f) > ElectionMath.Showing(4, 0.3f, -0.6f, 0.4f), "Social, mood and nature make a better showing");
        Check(ElectionMath.Good(ElectionMath.Showing(18, 0.9f, 0.4f, 0.9f)), "a gifted speaker in good spirits does well");
        Check(ElectionMath.Poor(ElectionMath.Showing(1, 0.15f, -0.6f, 0.1f)), "a poor speaker, low and abrasive, struggles");
        for (int k = 0; k < 100; k++)
        {
            float sh = ElectionMath.Showing(k % 21, (k % 10) / 9f, (k % 5 - 2) * 0.4f, (k % 7) / 6f);
            Check(Math.Abs(sh) <= ElectionMath.ShowingCap + 0.0001f, "a showing stays within its cap");
            Check(!(ElectionMath.Good(sh) && ElectionMath.Poor(sh)), "never both good and poor");
        }

        // ---------- The council's backing ----------
        Check(ElectionMath.EndorseLean(80f) > ElectionMath.EndorseLean(10f) && ElectionMath.EndorseLean(10f) > ElectionMath.EndorseLean(-30f), "the more they trust the council, the more its backing counts");
        Check(ElectionMath.EndorseLean(-80f) < 0f, "the bitter vote against the council's choice");
        Check(ElectionMath.EndorseLean(100f) <= 2f + 0.0001f, "backing is worth at most two");
        Check(ElectionMath.EndorseTrustCost > 0f && ElectionMath.AppointTrustCost > ElectionMath.EndorseTrustCost, "calling a vote off costs more than backing a candidate");

        // ---------- The days ----------
        Check(ElectionMath.CampaignDays(false) == 3 && ElectionMath.CampaignDays(true) == 2, "three days' campaign, two for an empty seat");
        Check(ElectionMath.CandidatesPerSeat(4) == 2 && ElectionMath.CandidatesPerSeat(9) == 3, "two or three stand for a seat");
        Check(ElectionMath.PollsOpenHour < ElectionMath.AfterWorkHour && ElectionMath.AfterWorkHour < ElectionMath.EveningHour, "polls open, then after work, then the evening");
        Check(!MeetingMath.IsNight(ElectionMath.EveningHour) && !MeetingMath.IsNight(ElectionMath.PollsOpenHour), "nothing is set for the night");
        Check(ElectionMath.CallsThisHour(0, 5) == 0 && ElectionMath.CallsThisHour(3, 10) == 2 && ElectionMath.CallsThisHour(40, 2) == 4, "a few called to the polls at a time");
        for (int left = 1; left < 25; left++)
        {
            // Called each hour until none are left: everyone gets called before the polls close.
            int remaining = left, hours = 11;
            while (hours > 0 && remaining > 0)
            {
                remaining -= ElectionMath.CallsThisHour(remaining, hours);
                hours--;
            }
            Check(remaining <= 0 || left > 44, $"{left} voters are all called within polling day");
        }

        // ---------- The count ----------
        var ballots = new List<int> { 1, 2, 1, 1, 3, 2, 1 };
        var byVote = ElectionMath.ReadOut(ballots, 12);
        Check(byVote.Count == ballots.Count && byVote.All(c => c.votes == 1), "few ballots: read vote by vote");
        Check(byVote.Select(c => c.candidate).SequenceEqual(ballots), "in the order they were cast");
        var many = Enumerable.Range(0, 40).Select(i => i % 3 == 0 ? 7 : i % 5 == 0 ? 9 : 8).ToList();
        foreach (int max in new[] { 2, 5, 10, 13 })
        {
            var calls = ElectionMath.ReadOut(many, max);
            foreach (int cand in many.Distinct())
                Check(calls.Where(c => c.candidate == cand).Sum(c => c.votes) == many.Count(b => b == cand), $"read in {max}s: every vote for {cand} read exactly once");
            Check(calls.Count <= max * 3, $"read in about {max} handfuls");
        }
        Check(ElectionMath.ReadOut(new List<int>(), 5).Count == 0, "no ballots, nothing read");
        Check(ElectionMath.CallsPerSeat(1) == ElectionMath.CallBudget && ElectionMath.CallsPerSeat(30) == 2, "the calls are shared between the seats with a race");
        Check(ElectionMath.GatheringTicks(ElectionMath.CountLines(ElectionMath.CallBudget, 12)) <= 15000, "even a big count fits an evening");
        int lines = ElectionMath.CountLines(ElectionMath.CallBudget, 6);
        Check(ElectionMath.GatheringTicks(lines) >= 2500 + lines * ElectionMath.LineTicks, "the gathering lasts long enough for its lines");

        // ---------- Ranked ballots ----------
        var b = ElectionMath.Split(ElectionMath.Join(new[] { 12, 4, 9 }));
        Check(b.SequenceEqual(new[] { 12, 4, 9 }), "a ranked ballot keeps its order");
        Check(ElectionMath.Split("").Count == 0 && ElectionMath.Split("x>3").SequenceEqual(new[] { 3 }), "a broken ballot keeps what it can");
        Check(ElectionMath.FirstStanding(b, new HashSet<int> { 4, 9 }) == 4, "a seated first choice passes to the next");
        Check(ElectionMath.FirstStanding(b, new HashSet<int> { 12, 9 }) == 12, "a first choice still standing keeps the vote");
        Check(ElectionMath.FirstStanding(b, new HashSet<int> { 5 }) == -1, "nobody left on the ballot: no vote");
        Check(ElectionMath.Winner(new[] { 3, 5, 5 }, new[] { 9f, 4f, 8f }) == 2, "a tie goes to the more able");
        Check(ElectionMath.Winner(new[] { 6, 5 }, new[] { 1f, 9f }) == 0, "the most votes win over ability");
        Check(ElectionMath.Winner(new int[0], new float[0]) == -1, "nobody, no winner");

        // ---------- The defs ----------
        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f).Root!).ToList();
        var offices = defs.SelectMany(d => d.Elements("ColonyCouncil.OfficeDef")).ToList();
        Check(offices.Count >= 12, $"the offices are found ({offices.Count})");
        var whens = new HashSet<string>(Enum.GetNames(typeof(PlankWhen)));
        var keys = new HashSet<string>();
        foreach (var o in offices)
        {
            string name = o.Element("defName")?.Value ?? "?";
            var planks = o.Element("planks")?.Elements("li").ToList() ?? new List<XElement>();
            Check(planks.Count >= 2, $"{name} has planks to run on");
            Check(planks.Any(p => (p.Element("when")?.Value ?? "Always") == "Always"), $"{name} always has a plank that fits");
            foreach (var p in planks)
            {
                string key = p.Element("key")?.Value ?? "", when = p.Element("when")?.Value ?? "Always", text = p.Element("text")?.Value ?? "";
                keys.Add(key);
                Check(key.Length > 0 && whens.Contains(when), $"{name}: plank {key} has a key and a known moment ({when})");
                Check(text.StartsWith("{PAWN_nameDef}") && text.EndsWith(".") && text.IndexOf(". ", StringComparison.Ordinal) < 0, $"{name}: plank {key} is one sentence about the candidate");
                Check(!text.Contains('!') && !text.Contains('—'), $"{name}: plank {key} has no exclamation mark or em dash");
            }
        }
        var pools = defs.SelectMany(d => d.Elements("ColonyCouncil.FlavorPoolDef")).ToDictionary(p => p.Element("defName")!.Value, p => p.Descendants("li").Select(li => li.Value).ToList());
        foreach (var pool in new[] { "Rimfolk_Pool_ElectionCanvass", "Rimfolk_Pool_HustingsGood" })
            foreach (var key in keys)
                Check(pools.TryGetValue(pool, out var l) && l.Any(x => x.Contains("[plank:" + key + "]")), $"{pool} has a line for the plank {key}");
        foreach (var pool in new[] { "Rimfolk_Pool_HustingsPoor", "Rimfolk_Pool_CanvassWon", "Rimfolk_Pool_CanvassUnmoved", "Rimfolk_Pool_CountOne", "Rimfolk_Pool_CountSome" })
            Check(pools.TryGetValue(pool, out var l) && l.All(x => !x.TrimStart().StartsWith("[")), $"{pool} has no tagged lines (it must always fit what happened)");
        Check(pools.TryGetValue("Rimfolk_Pool_CountSome", out var some) && some.All(x => x.Contains("{COUNT}") && x.Contains("{OTHER_nameDef}")), "a handful's call names the count and the candidate");
        Check(pools.TryGetValue("Rimfolk_Pool_CountOne", out var one) && one.All(x => x.Contains("{OTHER_nameDef}") && !x.Contains("{COUNT}")), "a single vote's call names the candidate");

        var misc = defs.SelectMany(d => d.Elements()).ToList();
        Check(misc.Any(x => x.Name.LocalName == "GatheringDef" && (string?)x.Element("defName") == "Rimfolk_ElectionGathering"), "the election's gathering exists");
        Check(misc.Any(x => x.Name.LocalName == "JobDef" && (string?)x.Element("defName") == "Rimfolk_CastVote"), "the vote's job exists");
        Check(misc.Any(x => x.Name.LocalName == "LetterDef" && (string?)x.Element("defName") == "Rimfolk_ElectionLetter"), "the announcement's letter exists");

        // ---------- The code: one sentence a bubble, the vote's term, no clock of its own ----------
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string offices1 = File.ReadAllText(Path.Combine(src, "Offices", "Offices.cs"));
        Check(offices1.Contains("Leaning(office, voter, candidate) + Rand.Range(0f, ElectionMath.VoteNoise)"), "the vote is the lean plus the luck of the day");
        Check(offices1.Contains("+ CampaignLean(office, voter, candidate)"), "the campaign enters the vote as one term");
        string run = File.ReadAllText(Path.Combine(src, "Offices", "OfficesElection.cs"));
        Check(!run.Contains("Sched."), "the election ticks on the council's hour and its gatherings, no clock of its own");
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Election.xml"));
        Check(!System.Text.RegularExpressions.Regex.IsMatch(keyed, @"\bin \{?\d|\bin (two|three|four|five) days"), "no countdowns in the election's words");
        return cases;
    }
}
