using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Conversations (Social/Talk/TalkMath.cs, ExchangeQueue.cs, and the defs): topics come up by how much they matter but
/// never take more than their share of the talk, how a talk goes follows both views and both characters, persuasion
/// stays within bounds, the lines of a talk are said in turn and dropped when the two part, and every topic and kind of
/// memory has its lines, each one sentence short enough to show.
/// </summary>
public static class TalkTests
{
    private sealed class P
    {
        public string name;
        public P(string n) { name = n; }
    }

    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "talk: " + what);
        }

        // ---------- Picking a topic ----------
        Check(TalkMath.Salience(0f, 1f, 1f, 1f, 2f, 2f, 1f) == 0f, "a topic that means nothing here never comes up, however hot");
        float calm = TalkMath.Salience(1f, 0f, 0.2f, 0.2f, 1f, 1f, 1f);
        Check(TalkMath.Salience(1f, 1f, 0.2f, 0.2f, 1f, 1f, 1f) > calm * 2.5f, "news makes a topic much likelier");
        Check(TalkMath.Salience(1f, 0f, 0.9f, 0.2f, 1f, 1f, 1f) > calm, "a strong view gets aired");
        Check(TalkMath.Salience(1f, 0f, 0.9f, 0.2f, 1f, 1f, 1f) > TalkMath.Salience(1f, 0f, 0.2f, 0.9f, 1f, 1f, 1f), "the speaker's view counts more than the listener's");
        Check(TalkMath.Salience(1f, 0f, 0.2f, 0.2f, 1f, 1f, 0.2f) < calm * 0.25f, "a topic the two just had is much rarer");
        Check(TalkMath.TieFactor(false, true, -0.8f, 0.8f) > TalkMath.TieFactor(false, true, 0.5f, 0.6f), "rivals pick what they disagree on");
        Check(TalkMath.TieFactor(true, false, 0f, 0f) > 1f, "friends talk about anything a little more");
        var sides = new float[PersonaMath.Axes];
        var interest = new float[PersonaMath.Axes];
        interest[(int)PersonaAxis.Temper] = 30f;
        sides[(int)PersonaAxis.Temper] = 80f;
        float hot = TalkMath.Interest(sides, interest);
        sides[(int)PersonaAxis.Temper] = -80f;
        Check(hot > 1f && TalkMath.Interest(sides, interest) < 1f, "the hot-tempered bring up a fight more, the calm less");
        sides[(int)PersonaAxis.Temper] = 100000f;
        Check(TalkMath.Interest(sides, interest) <= 2f, "interest has a cap");
        var shares = TalkMath.Shares(new[] { 1f, 3f, 0f, 6f }, 0.1f);
        Check(Math.Abs(shares.Sum() - 0.1f) < 1e-5f && shares[2] == 0f && Math.Abs(shares[3] - 0.06f) < 1e-5f, "the talk's budget is split by salience, never grown by it");
        Check(TalkMath.Shares(new[] { 0f, 0f }, 0.1f).All(s => s == 0f), "nothing to talk about, no talk");
        var many = TalkMath.Shares(Enumerable.Repeat(5f, 22).ToArray(), 0.1f);
        Check(Math.Abs(many.Sum() - 0.1f) < 1e-5f, "a busy week means livelier talk, not more of it");
        Check(TalkMath.PickIndex(new[] { 0f, 1f, 0f, 3f }, 0f) == 1 && TalkMath.PickIndex(new[] { 0f, 1f, 0f, 3f }, 0.99f) == 3, "picking by weight skips the weightless");
        Check(TalkMath.PickIndex(new[] { 0f, 0f }, 0.5f) == -1 && TalkMath.PickIndex(null, 0.5f) == -1, "nothing to pick");
        int[] hits = new int[2];
        for (int i = 0; i < 1000; i++) hits[TalkMath.PickIndex(new[] { 1f, 3f }, i / 1000f)]++;
        Check(hits[1] > hits[0] * 2.5f && hits[1] < hits[0] * 3.5f, "picks follow the weights");

        // ---------- Views ----------
        var w = new float[PersonaMath.Axes];
        w[(int)PersonaAxis.Nerve] = 55f;
        var bold = new float[PersonaMath.Axes];
        bold[(int)PersonaAxis.Nerve] = 80f;
        var timid = new float[PersonaMath.Axes];
        timid[(int)PersonaAxis.Nerve] = -80f;
        Check(TalkMath.Seed(bold, w, 0f) > 0.3f && TalkMath.Seed(timid, w, 0f) < -0.3f, "a bold colonist is for fighting back, a timid one for walls");
        Check(TalkMath.Seed(bold, w, 5f) <= TalkMath.SeedMax && TalkMath.Seed(timid, w, -5f) >= -TalkMath.SeedMax, "a first view leaves room to move");
        Check(TalkMath.Side(0.3f) == "for" && TalkMath.Side(-0.3f) == "against" && TalkMath.Side(0.1f) == "mixed", "sides of a view");
        Check(Math.Abs(TalkMath.Drift(0f, 1f) - TalkMath.DailyDrift) < 1e-5f && TalkMath.Drift(0.5f, 0.5f) == 0.5f, "views drift slowly toward what life suggests");
        Check(TalkMath.Shift(0.95f, 0.5f) == 1f && TalkMath.Shift(-0.95f, -0.5f) == -1f, "events never push a view past its ends");

        // ---------- Persuasion ----------
        float maxPull = 0f, minPull = 1f;
        foreach (int op in new[] { -100, -30, 0, 50, 100 })
        foreach (float trust in new[] { -100f, 0f, 100f })
        foreach (float pride in new[] { -100f, 0f, 100f })
        foreach (float open in new[] { -100f, 0f, 100f })
        foreach (TalkOutcome o in Enum.GetValues(typeof(TalkOutcome)))
        {
            float p = TalkMath.Pull(op, trust, pride, open, o);
            maxPull = Math.Max(maxPull, p);
            minPull = Math.Min(minPull, p);
        }
        Check(minPull >= 0f && maxPull <= TalkMath.MaxPull, $"persuasion stays within 0..{TalkMath.MaxPull} ({minPull:0.000}..{maxPull:0.000})");
        Check(TalkMath.Pull(80, 0f, 0f, 0f, TalkOutcome.Debate) > TalkMath.Pull(-60, 0f, 0f, 0f, TalkOutcome.Debate), "a liked speaker persuades more");
        Check(TalkMath.Pull(0, 80f, 0f, 0f, TalkOutcome.Debate) > TalkMath.Pull(0, -80f, 0f, 0f, TalkOutcome.Debate), "the trusting are persuaded more");
        Check(TalkMath.Pull(0, 0f, 80f, 0f, TalkOutcome.Debate) > TalkMath.Pull(0, 0f, -80f, 0f, TalkOutcome.Debate), "a proud speaker persuades more");
        Check(TalkMath.Pull(0, 0f, 0f, 80f, TalkOutcome.Debate) > TalkMath.Pull(0, 0f, 0f, -80f, TalkOutcome.Debate), "the open are persuaded more");
        Check(TalkMath.Pull(30, 0f, 0f, 0f, TalkOutcome.Debate) > TalkMath.Pull(30, 0f, 0f, 0f, TalkOutcome.Argue), "a friendly debate moves people more than an argument");
        float moved = PersonaMath.Persuaded(-0.6f, 0.8f, TalkMath.Pull(100, 100f, 100f, 100f, TalkOutcome.Debate));
        Check(moved > -0.6f && moved < 0.8f && moved - -0.6f <= 1.4f * TalkMath.MaxPull + 1e-5f, "one talk moves a view part of the way, never past the other's");
        Check(TalkMath.Hardened(0.4f, -0.5f) > 0.4f && TalkMath.Hardened(-0.4f, 0.5f) < -0.4f && TalkMath.Hardened(0.99f, -1f) <= 1f, "an argument hardens a view away from the other's");
        float c1 = TalkMath.Confirmed(0.4f, 0.5f, 0.05f);
        Check(c1 > 0.4f && c1 <= 1f && TalkMath.Confirmed(-0.4f, -0.5f, 0.05f) < -0.4f, "agreeing makes both a little surer");

        // ---------- How it goes ----------
        OutcomeInput Base() => new OutcomeInput { frictionsOn = true, opinionAB = 10, opinionBA = 10 };
        var x = Base();
        x.stanceA = 0.6f;
        x.stanceB = 0.5f;
        Check(TalkMath.Outcome(x, 0.5f, 0.99f) == TalkOutcome.Agree, "close views agree");
        x = Base();
        x.stanceA = 0.8f;
        x.stanceB = -0.7f;
        x.temperA = x.temperB = 70f;
        x.opinionAB = x.opinionBA = -40;
        Check(TalkMath.Outcome(x, 0.1f, 0.99f) == TalkOutcome.Argue, "two hotheads with opposed views and little liking argue");
        x.frictionsOn = false;
        Check(TalkMath.Outcome(x, 1f, 0.99f) == TalkOutcome.Debate, "with frictions off, nobody argues");
        x = Base();
        x.stanceA = 0.8f;
        x.stanceB = -0.7f;
        x.friends = true;
        x.warmthA = x.warmthB = 50f;
        x.opinionAB = x.opinionBA = 60;
        Check(TalkMath.Outcome(x, 1f, 0.99f) == TalkOutcome.Debate, "warm friends with opposed views debate");
        x.humourA = x.humourB = 60f;
        x.friends = false;
        x.warmthA = x.warmthB = 0f;
        x.opinionAB = x.opinionBA = 20;
        Check(TalkMath.Outcome(x, 0.5f, 0.99f) == TalkOutcome.Debate, "two jokers usually keep it light");
        x = Base();
        x.stanceA = 0.8f;
        x.stanceB = -0.7f;
        x.opinionAB = x.opinionBA = 0;
        var sore = x;
        sore.sore = true;
        Check(TalkMath.Heat(sore) > TalkMath.Heat(x), "a spat not yet made up for makes a talk hotter");
        var warmLately = x;
        warmLately.warmLately = true;
        Check(TalkMath.Heat(warmLately) < TalkMath.Heat(x), "a warm moment lately cools it");
        var rivals = x;
        rivals.rivals = true;
        Check(TalkMath.Heat(rivals) > TalkMath.Heat(x), "rivals run hotter");
        x = Base();
        x.humourA = 80f;
        x.opinionAB = 40;
        Check(TalkMath.Outcome(x, 0.5f, 0.01f) == TalkOutcome.Tease, "a joker who likes the other teases");
        var low = x;
        low.otherLow = true;
        var noTease = x;
        noTease.noTease = true;
        var disliked = x;
        disliked.opinionAB = -20;
        Check(TalkMath.Outcome(low, 0.5f, 0.01f) != TalkOutcome.Tease && TalkMath.Outcome(noTease, 0.5f, 0.01f) != TalkOutcome.Tease
              && TalkMath.Outcome(disliked, 0.5f, 0.01f) != TalkOutcome.Tease, "nobody teases someone low, about the dead, or someone they dislike");
        Check(TalkMath.TeaseChance(20f, true) == 0f && TalkMath.TeaseChance(100f, true) <= 0.6f && TalkMath.TeaseChance(60f, false) > 0f, "only jokers tease, and not every time");
        foreach (TalkOutcome o in Enum.GetValues(typeof(TalkOutcome)))
            foreach (float r in new[] { 0f, 0.3f, 0.6f, 1f })
            {
                int t = TalkMath.Turns(o, r);
                Check(t >= 2 && t <= 4, $"{o}: 2 to 4 lines ({t})");
            }
        Check(TalkMath.TurnAt(0) == TalkTurn.Open && TalkMath.TurnAt(1) == TalkTurn.Reply && TalkMath.TurnAt(2) == TalkTurn.Close && TalkMath.TurnAt(3) == TalkTurn.Last, "the turns in order");
        Check(TalkMath.ByFirst(0) && !TalkMath.ByFirst(1) && TalkMath.ByFirst(2) && !TalkMath.ByFirst(3), "the speakers take turns");

        // ---------- Voices ----------
        Check(TalkMath.Voice(TalkMath.Plain, 70f, 0f, 0f, 0f, TalkOutcome.Argue, true) == TalkMath.Blunt, "a hothead snaps in an argument");
        Check(TalkMath.Voice(TalkMath.Plain, 0f, -70f, 0f, 0f, TalkOutcome.Argue, true) == TalkMath.Cold, "the cold go curt");
        Check(TalkMath.Voice(TalkMath.Plain, 0f, 0f, 0f, -60f, TalkOutcome.Argue, false) == TalkMath.Gloomy, "the bleak see the worst");
        Check(TalkMath.Voice(TalkMath.Plain, 0f, 0f, 70f, 0f, TalkOutcome.Tease, true) == TalkMath.Cheerful, "a joker teasing jokes");
        Check(TalkMath.Voice(TalkMath.Plain, 0f, 60f, 0f, 0f, TalkOutcome.Comfort, true) == TalkMath.Warm, "the warm comfort warmly");
        Check(TalkMath.Voice(TalkMath.Cold, 0f, -60f, 0f, 0f, TalkOutcome.Comfort, true) == TalkMath.Cold, "and the cold comfort curtly");
        Check(TalkMath.Voice(TalkMath.Warm, 0f, 0f, 0f, 0f, TalkOutcome.Agree, true) == TalkMath.Warm, "otherwise people sound like themselves");
        Check(TalkMath.Worn(4) == false && TalkMath.Worn(5), "a joke wears thin after four tellings");

        // ---------- The lines said in turn ----------
        var q = new ExchangeQueue<P>(3);
        P a = new P("a"), b = new P("b"), c = new P("c"), d = new P("d");
        int id = q.Add(a, b, new[] { "one", "two", "", "three" }, 100, 45, 165);
        Check(id > 0 && q.Count == 1 && q.Busy(a) && q.Busy(b) && !q.Busy(c), "a talk under way keeps its two busy");
        Check(q.Add(a, c, new[] { "x" }, 100, 45, 165) == 0 && q.Add(c, b, new[] { "x" }, 100, 45, 165) == 0, "someone already talking isn't given a second talk");
        Check(q.Add(c, d, new string[0], 100, 45, 165) == 0 && q.Add(c, c, new[] { "x" }, 100, 45, 165) == 0, "nothing to say, or nobody to say it to");
        Check(q.Due(144).Count == 0, "nothing before the first gap");
        var first = q.Due(145);
        Check(first.Count == 1 && first[0].line.text == "one" && first[0].line.speaker == a && first[0].line.listener == b, "the first speaker opens");
        var second = q.Due(310);
        Check(second.Count == 1 && second[0].line.text == "two" && second[0].line.speaker == b, "the other replies after a gap");
        var third = q.Due(10000);
        Check(third.Count == 1 && third[0].line.text == "three" && third[0].line.speaker == a, "empty lines are skipped, and the turns go on with the next");
        Check(q.Count == 0 && !q.Busy(a), "a finished talk frees its two");
        id = q.Add(a, b, new[] { "one", "two", "three" }, 0, 10, 10);
        q.Due(10);
        q.Drop(id);
        Check(q.Count == 0 && q.Due(1000).Count == 0, "when the two part, the rest isn't said");
        q.Add(a, b, new[] { "x", "y" }, 0, 10, 10);
        q.Add(c, d, new[] { "x", "y" }, 0, 10, 10);
        q.DropWith(a);
        Check(q.Count == 1 && !q.Busy(a) && q.Busy(c), "someone who dies or leaves drops only their own talks");
        var e = new P("e");
        var f = new P("f");
        var g = new P("g");
        var h = new P("h");
        q.Add(a, b, new[] { "x" }, 0, 10, 10);
        q.Add(e, f, new[] { "x" }, 0, 10, 10);
        q.Add(g, h, new[] { "x" }, 0, 10, 10);
        Check(q.Count == 3 && !q.Busy(c), "past its cap, the oldest talk gives way");
        Check(q.NextDue == 10, "the next line due");
        q.Clear();
        Check(q.Count == 0 && q.NextDue == int.MaxValue, "a load forgets talks under way");
        // A story told, the listener's answer, and the one it was about answering for themselves: three people, in turn.
        int told = q.AddTurns(new List<(P, P, string)> { (a, b, "story"), (b, a, "answer"), (c, a, "that's not how it was") }, 0, 10, 10);
        Check(told > 0 && q.Busy(a) && q.Busy(b) && q.Busy(c) && !q.Busy(d), "everyone in a story told is busy until it's said");
        Check(q.AddTurns(new List<(P, P, string)> { (c, d, "x") }, 0, 10, 10) == 0, "nobody already in one starts another");
        var turns = q.Due(1000);
        Check(turns.Count == 3 && turns[2].line.speaker == c && turns[2].line.listener == a, "each line said by its own speaker, in order");
        q.Clear();

        // ---------- Who remembers a fight together ----------
        var near = new Dictionary<(int a, int b), int> { [(0, 1)] = 6, [(0, 2)] = 5, [(0, 3)] = 4, [(0, 4)] = 3, [(1, 2)] = 1, [(3, 4)] = 2 };
        var fought = TalkMath.BattlePairs(6, near, 8, new[] { 1, 0, 0, 4, 3, 0 });
        Check(fought.Count(p => p.a == 0 || p.b == 0) == TalkMath.BattlePartners, "a fighter remembers it with the few they stood beside longest");
        Check(fought.Contains((0, 1)) && fought.Contains((3, 4)) && !fought.Contains((1, 2)), "only pairs really seen side by side (more than once, in a long fight)");
        Check(!fought.Any(p => p.a == 5 || p.b == 5), "someone seen beside nobody doesn't share it");
        var brief = TalkMath.BattlePairs(3, new Dictionary<(int a, int b), int> { [(1, 2)] = 1 }, 2, new[] { 1, 0, 0 });
        Check(brief.Count == 1 && brief[0] == (1, 2), "in a short fight, once side by side is enough");
        var unseen = TalkMath.BattlePairs(4, new Dictionary<(int a, int b), int>(), 0, new[] { 1, 0, 3, 2 });
        Check(unseen.Count == 2 && unseen.Contains((0, 1)) && unseen.Contains((2, 3)), "a fight nobody saw: each with the one they're closest to");
        Check(TalkMath.BattlePairs(20, new Dictionary<(int a, int b), int>(), 0, Enumerable.Range(0, 20).Select(i => (i + 1) % 20).ToArray()).Count <= 20,
            "a big fight makes at most one memory per fighter when nobody was seen");

        // ---------- The defs ----------
        var all = new List<(string file, XElement el)>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories))
            foreach (var el in XDocument.Load(file).Root!.Elements())
                all.Add((Path.GetFileName(file), el));
        string Name(XElement el) => (string)el.Element("defName") ?? "";
        var topics = all.Where(x2 => x2.el.Name.LocalName == "ColonyCouncil.TopicDef").Select(x2 => x2.el).ToList();
        var interactions = all.Where(x2 => x2.el.Name.LocalName == "InteractionDef").ToDictionary(x2 => Name(x2.el) == "" ? Guid.NewGuid().ToString() : Name(x2.el), x2 => x2.el);
        var lines = all.Where(x2 => x2.el.Name.LocalName == "ColonyCouncil.TalkLinesDef").Select(x2 => x2.el).ToList();
        var memories = all.Where(x2 => x2.el.Name.LocalName == "ColonyCouncil.SharedMemoryDef").Select(x2 => x2.el).ToList();
        Check(topics.Count >= 15 && topics.Count <= 25, $"15 to 25 topics ({topics.Count})");
        Check(memories.Count >= 15, $"enough kinds of shared memory ({memories.Count})");
        bool Has(string topicOrMemory, string field, string turn, string outcome = null) => lines.Any(l =>
            (string)l.Element(field) == topicOrMemory && (string)l.Element("turn") == turn
            && (outcome == null || l.Element("outcomes") == null || l.Element("outcomes")!.Elements("li").Any(o => o.Value == outcome)));
        foreach (var t in topics)
        {
            string n = Name(t);
            string inter = (string)t.Element("interaction");
            Check(inter != null && interactions.TryGetValue(inter, out var idef) && (string)idef.Attribute("ParentName") == "Rimfolk_TalkBase", $"{n}: its talk is in the game's picker");
            foreach (var turn in new[] { "Open", "Reply", "Close" })
                Check(Has(n, "topic", turn, "Argue"), $"{n}: lines to {turn.ToLowerInvariant()} with");
            int own = lines.Where(l => (string)l.Element("topic") == n).Sum(l => l.Descendants("li").Count(li => li.Parent?.Parent?.Name.LocalName == "lines"));
            Check(own >= 40, $"{n}: 40 lines or more of its own ({own})");
            var openLines = lines.Where(l => (string)l.Element("topic") == n && (string)l.Element("turn") == "Open" && (l.Element("outcomes")?.Elements("li").Any(o => o.Value == "Debate") ?? true))
                .SelectMany(l => l.Descendants("li").Where(li => li.Parent?.Parent?.Name.LocalName == "lines")).Select(li => li.Value).ToList();
            Check(openLines.Count(s2 => s2.Contains("[for")) >= 8 && openLines.Count(s2 => s2.Contains("[against")) >= 8, $"{n}: openings for both sides");
        }
        // A child's talk (growing up here): every line says whether a child or a grown-up says it.
        var forChildren = topics.Where(t => (string)t.Element("forChildren") == "true").Select(Name).ToList();
        Check(forChildren.Count >= 1, "a topic for children");
        foreach (var n in forChildren)
            foreach (var li in lines.Where(l => (string)l.Element("topic") == n).SelectMany(l => l.Descendants("li").Where(x => x.Parent?.Parent?.Name.LocalName == "lines")))
            {
                var tagged = Regex.Match(li.Value, @"^\[([^\]]*)\]");
                var tags = tagged.Success ? tagged.Groups[1].Value.Split(',').Select(x => x.Trim()).ToList() : new List<string>();
                Check(tags.Contains("child") || tags.Contains("!child"), $"{n}: says whether a child or a grown-up speaks: {li.Value}");
            }
        foreach (var m in memories)
        {
            string n = Name(m);
            string outcome = (string)m.Element("joke") == "true" ? "Joke" : "Reminisce";
            foreach (var turn in new[] { "Open", "Reply", "Close" })
                Check(Has(n, "memory", turn, outcome), $"{n}: lines to {turn.ToLowerInvariant()} with");
            Check(((string)m.Element("text") ?? "").Contains("{LABEL}") || ((string)m.Element("text") ?? "").Contains("{A}"), $"{n}: its text says what it was");
        }
        foreach (var turn in new[] { "Open", "Reply", "Close" })
        {
            bool Generic(string outcome) => lines.Any(l => l.Element("topic") == null && l.Element("memory") == null && (string)l.Element("turn") == turn
                && (l.Element("outcomes")?.Elements("li").Any(o => o.Value == outcome) ?? true));
            Check(Generic("Comfort"), $"comfort has lines to {turn.ToLowerInvariant()} with");
            Check(Generic("Tease"), $"teasing has lines to {turn.ToLowerInvariant()} with");
            foreach (var o in new[] { "Agree", "Debate", "Argue" })
                Check(Generic(o), $"{o.ToLowerInvariant()}: lines for any topic to {turn.ToLowerInvariant()} with");
        }
        int count = 0;
        var known = new HashSet<string> { "OTHER", "PAWN", "SUBJECT", "LABEL", "TOPIC", "OTHER_pronoun", "OTHER_objective", "OTHER_possessive", "PAWN_pronoun", "PAWN_possessive", "PAWN_objective" };
        var seen = new HashSet<string>();
        foreach (var l in lines)
        {
            string n = Name(l);
            foreach (var li in l.Descendants("li").Where(li => li.Parent?.Parent?.Name.LocalName == "lines"))
            {
                count++;
                string text = Regex.Replace(li.Value, @"^\[[^\]]*\]\s*", "");
                Check(seen.Add(n + "|" + li.Value), $"{n}: a line only once: {text}");
                foreach (Match m in Regex.Matches(text, @"\{([A-Za-z_]+)\}"))
                    Check(known.Contains(m.Groups[1].Value), $"{n}: known placeholder {{{m.Groups[1].Value}}}");
                string shown = text.Replace("{OTHER}", "Margaret").Replace("{PAWN}", "Margaret").Replace("{SUBJECT}", "the fight with Iron Hand")
                    .Replace("{LABEL}", "the fight with Iron Hand").Replace("{TOPIC}", "the colony's project");
                // Talk lines are one sentence: speech always shows the first whole (SpeechMath.Shorten, from the 13th character).
                int cut = -1;
                for (int i = 12; i < shown.Length - 1; i++)
                    if (".!?".IndexOf(shown[i]) >= 0 && ".!?\"'".IndexOf(shown[i + 1]) < 0)
                    {
                        cut = i;
                        break;
                    }
                Check(cut < 0, $"{n}: one sentence, or speech cuts it short: {text}");
                Check(shown.Length <= 90, $"{n}: short enough to show ({shown.Length}): {text}");
                Check(text.Count(ch => ch == '!') <= 1, $"{n}: no exclamation spam: {text}");
            }
        }
        Check(count >= 1200, $"at least 1,200 lines ({count})");
        return cases;
    }
}
