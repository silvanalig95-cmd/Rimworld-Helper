using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Talking with outsiders the way people do (Outsiders/OutsiderTalkMath): patience on a visit, asking again, a pushy
/// colonist, gifts in a row, how often colonists start a talk themselves, the pace of a scene; the conversation pieces'
/// rules (conditions, placeholders, a cool talk's pieces, children, callbacks bound to one earlier talk, what's current
/// first, never the same to the same person while another fits) and the marks; and the library itself
/// (Defs/OutsiderTalkDefs): every piece well formed, every line one short sentence, every condition and placeholder one
/// the game fills, every callback with a piece that leaves its mark, and every piece able to be said.
/// </summary>
public static class OutsiderTalkTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "outsider talk: " + what);
        }

        // ---------------- Patience ----------------

        C(OutsiderTalkMath.Patience(0, 0, false) == 3, "three things a visit, to begin with");
        C(OutsiderTalkMath.Patience(30, 0, false) == 4 && OutsiderTalkMath.Patience(60, 0, false) == 5 && OutsiderTalkMath.Patience(60, 0, true) == 6,
            "more for one who thinks well of the colony, holds it dear, has a friend here");
        C(OutsiderTalkMath.Patience(100, 2, true) == OutsiderTalkMath.PatienceMax && OutsiderTalkMath.Patience(-100, -2, false) == OutsiderTalkMath.PatienceMin,
            "between one and six");
        C(OutsiderTalkMath.Patience(-5, 0, false) < OutsiderTalkMath.Patience(5, 0, false) && OutsiderTalkMath.Patience(10, 1, false) > OutsiderTalkMath.Patience(10, -1, false),
            "fewer when they think ill of it, more for a kind nature than a cold one");
        C(OutsiderTalkMath.Left(3, 0f) == 3 && OutsiderTalkMath.Left(3, 2.5f) == 0 && OutsiderTalkMath.Left(3, 2f) == 1 && OutsiderTalkMath.Left(3, 9f) == 0, "things left, never below none");
        C(Math.Abs(OutsiderTalkMath.Rested(3f, 12f) - 2f) < 0.001f && OutsiderTalkMath.Rested(1f, 48f) == 0f && OutsiderTalkMath.Rested(2f, 0f) == 2f,
            "patience comes back one thing in twelve hours");
        C(OutsiderTalkMath.Cost(OutsiderAct.Provoke) == 0f && OutsiderTalkMath.Cost(OutsiderAct.Duel) == 0f && OutsiderTalkMath.Cost(OutsiderAct.Letter) == 0f
          && OutsiderTalkMath.Cost(OutsiderAct.Chat) == 1f && OutsiderTalkMath.Cost(OutsiderAct.Gift) == 1f, "hard words, a challenge and a letter ask no patience; the rest one each");
        var asks = Enum.GetValues(typeof(OutsiderAct)).Cast<OutsiderAct>().Where(OutsiderTalkMath.Asks).ToList();
        C(asks.SequenceEqual(new[] { OutsiderAct.AskHome, OutsiderAct.GoodWord, OutsiderAct.Teach, OutsiderAct.Bring, OutsiderAct.Invite, OutsiderAct.Befriend, OutsiderAct.Court, OutsiderAct.Faith }),
            "the asks: news, a good word, a lesson, a favour, an invitation, getting to know them, courting, faith");
        C(!OutsiderTalkMath.Asks(OutsiderAct.Gift) && !OutsiderTalkMath.Asks(OutsiderAct.Meal) && !OutsiderTalkMath.Asks(OutsiderAct.Chat), "a gift, a meal and a chat are offers, not asks");
        C(!OutsiderTalkMath.Pushy(0) && !OutsiderTalkMath.Pushy(1) && OutsiderTalkMath.Pushy(2) && OutsiderTalkMath.Pushy(5), "the third ask in a row is pushy");
        C(OutsiderTalkMath.PushyWarmth < 0f && OutsiderTalkMath.PushyRegard < 0f && OutsiderTalkMath.PushyRegard > -5f, "pushy costs a little regard, not much");
        C(OutsiderTalkMath.GiftFactor(0) == 1f && Math.Abs(OutsiderTalkMath.GiftFactor(1) - 0.5f) < 0.001f && OutsiderTalkMath.GiftFactor(3) < OutsiderTalkMath.GiftFactor(2)
          && OutsiderTalkMath.GiftFactor(3) < OutsiderTalkMath.GiftMemoryFloor, "gifts in a row count for less and less, the fourth leaves no memory");
        C(new[] { 5, 3, 2, 1, 0 }.Select(OutsiderTalkMath.PatienceWord).SequenceEqual(new[] { "Plenty", "Plenty", "Some", "Last", "None" }), "patience in words");
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Outsiders.xml"));
        foreach (var w in new[] { "Plenty", "Some", "Last", "None" }) C(keyed.Contains("<CC_Out_Patience_" + w + ">"), $"the words for {w} exist");

        // ---------------- Talks they start themselves ----------------

        var basic = new OutsiderTalkMath.ChatterInput { social = 6, frequency = 1f, lingering = true };
        float p0 = OutsiderTalkMath.ChatterChance(basic);
        C(p0 > 0.02f && p0 < 0.08f, $"an ordinary colonist lingering near an outsider: a few percent a look ({p0:0.000})");
        C(OutsiderTalkMath.ChatterChance(basic with { social = 15, passion = 2 }) > p0 && OutsiderTalkMath.ChatterChance(basic with { kind = true }) > p0
          && OutsiderTalkMath.ChatterChance(basic with { warmth = 80f }) > p0, "the sociable, the kind and the warm more");
        C(OutsiderTalkMath.ChatterChance(basic with { shy = true }) < p0 && OutsiderTalkMath.ChatterChance(basic with { psychopath = true }) < p0
          && OutsiderTalkMath.ChatterChance(basic with { warmth = -80f }) < p0, "the shy, the cold and the distant less");
        C(OutsiderTalkMath.ChatterChance(basic with { lingering = false }) < p0 * 0.5f, "in passing far less than lingering");
        C(OutsiderTalkMath.ChatterChance(basic with { ofColonist = -40 }) < p0 && OutsiderTalkMath.ChatterChance(basic with { ofColonist = 40 }) > p0, "how they regard the colonist");
        C(OutsiderTalkMath.ChatterChance(basic with { frequency = 0f }) == 0f && Math.Abs(OutsiderTalkMath.ChatterChance(basic with { frequency = 2f }) - 2f * p0) < 0.0001f,
            "the setting scales it, off at zero");
        C(OutsiderTalkMath.ChatterChance(new OutsiderTalkMath.ChatterInput { social = 20, passion = 2, warmth = 100f, kind = true, lingering = true, theyKind = true, ofColonist = 90, frequency = 3f })
          <= 0.25f, "a quarter at most");
        // An hour side by side: likely, but not certain, to start one.
        float hour = 1f - (float)Math.Pow(1f - p0, 10);
        C(hour > 0.2f && hour < 0.6f, $"an hour lingering side by side: a talk now and then ({hour:0.00})");
        int h = OutsiderTalkMath.TicksPerHour;
        C(OutsiderTalkMath.ChatterWaited(-1, -1, 0) && !OutsiderTalkMath.ChatterWaited(0, -1, 3 * h) && OutsiderTalkMath.ChatterWaited(0, -1, 4 * h)
          && !OutsiderTalkMath.ChatterWaited(-1, 0, 11 * h), "an outsider talks this way once in four hours, the same two once in twelve");
        C(OutsiderTalkMath.ChatterMaxLines(false) == 3 && OutsiderTalkMath.ChatterMaxLines(true) == 6, "three lines in passing, six lingering");

        // ---------------- The pace of a scene ----------------

        C(OutsiderTalkMath.LineGap(0) == 170 && OutsiderTalkMath.LineGap(90) == 320 && OutsiderTalkMath.LineGap(40) > OutsiderTalkMath.LineGap(10),
            "a line's time to read: 170 to 320 ticks, longer for longer lines");
        var six = Enumerable.Repeat(new string('x', 90), 6).ToList();
        int scene = OutsiderTalkMath.SceneTicks(six);
        C(scene <= 2400 && scene >= 1500, $"six long lines fit in a talk's longest scene ({scene} of 2400 ticks)");
        C(OutsiderTalkMath.SceneTicks(new List<string>()) == 0, "an empty scene takes no time");

        // ---------------- Pieces ----------------

        var errs = new List<string>();
        var greet = OutsiderTalkMath.Parse("greet", "greeting", null, 0f, "first", new[] { "[us] Hello there.", "[them,their:tribal] We greet you.", "[them] Hello.", "[us] Welcome." },
            null, null, null, errs);
        C(errs.Count == 0 && greet.turns.Count == 3 && greet.turns[1].Count == 2 && greet.turns[0][0].us && !greet.turns[1][0].us, "lines in a row by one speaker are one turn's choices");
        C(greet.uses.SequenceEqual(new[] { "chat" }) && greet.weight == 1f, "a chat by default, weight 1");
        errs.Clear();
        OutsiderTalkMath.Parse("bad", "x", null, 1f, "", new[] { "No speaker here." }, null, null, null, errs);
        C(errs.Any(e => e.Contains("[us] or [them]")) && errs.Any(e => e.Contains("turns")), "a line without a speaker, and a piece of fewer than two turns, are errors");
        errs.Clear();
        OutsiderTalkMath.Parse("long", "x", null, 1f, "", Enumerable.Range(0, 7).Select(i => i % 2 == 0 ? "[us] A." : "[them] B.").ToList(), null, null, null, errs);
        C(errs.Any(e => e.Contains("at most")), "seven turns is too many");
        errs.Clear();
        var cb = OutsiderTalkMath.Parse("cb", "callback", null, 1f, "talked:war,then:over", new[] { "[them] The war with {THEN} is over.", "[us] Good." }, null, null, null, errs);
        C(cb.callback && errs.Count == 0, "a talked: condition makes a callback");
        errs.Clear();
        OutsiderTalkMath.Parse("cb2", "callback", null, 1f, "then:over", new[] { "[them] It's over.", "[us] Good." }, null, null, null, errs);
        C(errs.Any(e => e.Contains("talked:")), "a then: condition needs its talked:");
        C(OutsiderTalkMath.Holes("{TOWN} and {WAR} and {TOWN}").SequenceEqual(new[] { "TOWN", "WAR" }) && OutsiderTalkMath.Holes("none").Length == 0, "placeholders found once each");

        TalkFacts Facts(params string[] tags)
        {
            var f = new TalkFacts();
            foreach (var t in tags) f.Tag(t);
            f.Tag("tone:plain");
            return f;
        }

        var f1 = Facts("first");
        C(OutsiderTalkMath.Fits(greet, f1, null, "chat", 6, false), "a greeting for strangers fits strangers");
        C(!OutsiderTalkMath.Fits(greet, Facts("met"), null, "chat", 6, false), "and not two who met");
        C(!OutsiderTalkMath.Fits(greet, f1, null, "home", 6, false), "nor when asked about home");
        C(!OutsiderTalkMath.Fits(greet, f1, null, "chat", 2, false), "nor a three-line piece in a two-line talk");
        var town = OutsiderTalkMath.Parse("town", "town", "chat,home", 1f, "town", new[] { "[us] How is {TOWN}?", "[them] Quiet." }, "{TOWN} is quiet.", null, null, null);
        var ft = Facts("town");
        C(!OutsiderTalkMath.Fits(town, ft, null, "home", 6, false), "a placeholder without a fact: not said");
        ft.Value("TOWN", "Kettleford");
        C(OutsiderTalkMath.Fits(town, ft, null, "home", 6, false), "with the fact: said");
        var curt = OutsiderTalkMath.Parse("curt", "x", null, 1f, "tone:cool|tone:sour", new[] { "[us] Fine day.", "[them] If you say so." }, null, null, null, null);
        var plainChat = OutsiderTalkMath.Parse("plain", "x", null, 1f, "", new[] { "[us] Fine day.", "[them] Lovely." }, null, null, null, null);
        var cool = new TalkFacts();
        cool.Tag("tone:cool");
        C(OutsiderTalkMath.Fits(curt, cool, null, "chat", 3, true) && !OutsiderTalkMath.Fits(plainChat, cool, null, "chat", 3, true), "a cool talk only with a piece written for it");
        C(!OutsiderTalkMath.Fits(curt, Facts(), null, "chat", 6, false), "a piece written for a cool talk is never said in a plain one");
        var kid = Facts("them:young");
        C(!OutsiderTalkMath.Fits(plainChat, kid, null, "chat", 6, false), "a child only gets a piece written for a child");
        var kidPiece = OutsiderTalkMath.Parse("kid", "x", null, 1f, "them:young", new[] { "[us] Hello there.", "[them] Hi." }, null, null, null, null);
        C(OutsiderTalkMath.Fits(kidPiece, kid, null, "chat", 6, false), "and gets that one");

        // Callbacks are weighed with one earlier talk at a time.
        var fc = Facts();
        var mWar = new MarkFacts { key = "war" };
        mWar.tags.Add("talked:war");
        mWar.tags.Add("then:still");
        mWar.values["THEN"] = "the Iron Hand";
        var mSick = new MarkFacts { key = "sickness" };
        mSick.tags.Add("talked:sickness");
        mSick.tags.Add("then:over");
        mSick.values["THEN"] = "the gray cough";
        fc.marks.Add(mWar);
        fc.marks.Add(mSick);
        fc.tags.Add("talked:war");
        fc.tags.Add("talked:sickness");
        C(!OutsiderTalkMath.Fits(cb, fc, null, "chat", 6, false), "a callback isn't weighed without an earlier talk");
        C(!OutsiderTalkMath.Fits(cb, fc, mWar, "chat", 6, false), "the war still on: the war-over callback doesn't fit");
        C(!OutsiderTalkMath.Fits(cb, fc, mSick, "chat", 6, false), "nor does the sickness's being over make the war over");
        mWar.tags.Remove("then:still");
        mWar.tags.Add("then:over");
        C(OutsiderTalkMath.Fits(cb, fc, mWar, "chat", 6, false), "the war over: it fits");
        var ch = OutsiderTalkMath.Choose(new List<TalkPiece> { cb }, fc, "chat", 6, false, null, null, null, () => 0.5f);
        C(ch != null && ch.mark == mWar && ch.lines[0].text == "The war with the Iron Hand is over.", "and is filled with what that talk was about");
        C(!OutsiderTalkMath.Fits(plainChat, fc, mWar, "chat", 6, false), "an ordinary piece isn't weighed with a mark");

        // What's current first; never the same to the same person while another fits.
        var news = OutsiderTalkMath.Parse("news", "war", null, 1f, "their:war", new[] { "[us] How goes the war?", "[them] Badly." }, null, null, null, null);
        var fresh = Facts("their:war");
        fresh.Tag("their:war", 2.5f);
        C(OutsiderTalkMath.Score(news, fresh, null) > 3f * OutsiderTalkMath.Score(plainChat, fresh, null), "fresh news weighs well over plain talk");
        C(OutsiderTalkMath.Score(news, Facts("their:war"), null) < OutsiderTalkMath.Score(news, fresh, null), "the fresher the more");
        var rng = new Random(7);
        int picksNews = 0, picksSaid = 0;
        var pool = new List<TalkPiece> { news, plainChat };
        for (int i = 0; i < 400; i++)
        {
            var c = OutsiderTalkMath.Choose(pool, fresh, "chat", 6, false, null, null, null, () => (float)rng.NextDouble());
            if (c?.piece == news) picksNews++;
            var c2 = OutsiderTalkMath.Choose(pool, fresh, "chat", 6, false, new List<string> { "news" }, null, null, () => (float)rng.NextDouble());
            if (c2?.piece == news) picksSaid++;
        }
        C(picksNews > 300, $"the fresh news is told most of the time ({picksNews} of 400)");
        C(picksSaid < 60, $"but rarely again to the same person ({picksSaid} of 400)");
        var choice = OutsiderTalkMath.Choose(new List<TalkPiece> { town }, ft, "home", 6, false, null, null, null, () => 0.1f);
        C(choice != null && choice.heard == "Kettleford is quiet." && choice.lines[0].text == "How is Kettleford?", "lines and news filled with the facts");
        var specific = OutsiderTalkMath.Parse("voice", "x", null, 1f, "", new[] { "[us] Hi.", "[them,their:tribal] We greet you.", "[them] Hello." }, null, null, null, null);
        int tribal = 0;
        var ftr = Facts("their:tribal");
        for (int i = 0; i < 200; i++)
            if (OutsiderTalkMath.Choose(new List<TalkPiece> { specific }, ftr, "chat", 6, false, null, null, null, () => (float)rng.NextDouble())?.lines[1].text == "We greet you.")
                tribal++;
        C(tribal > 140, $"a people's own line is chosen over the plain one ({tribal} of 200)");
        C(OutsiderTalkMath.Choose(new List<TalkPiece> { specific }, Facts(), "chat", 6, false, null, null, null, () => 0.01f)?.lines[1].text == "Hello.", "others get the plain one");
        C(OutsiderTalkMath.Fill("{SITE} lies near {TOWN}.", ft, null) == "{SITE} lies near Kettleford.".Replace("{SITE}", "") && OutsiderTalkMath.Capitalize("the gray cough") == "The gray cough",
            "filling, and a line starting with a fact capitalized");

        // ---------------- Marks ----------------

        var mark = OutsiderTalkMath.DecodeMark(OutsiderTalkMath.EncodeMark("war", 1234, "the Iron|Hand", "war:Faction_5"));
        C(mark != null && mark.key == "war" && mark.tick == 1234 && mark.subject == "the Iron/Hand" && mark.fact == "war:Faction_5", "a mark round trips (a bar in a name kept apart)");
        C(OutsiderTalkMath.DecodeMark("") == null && OutsiderTalkMath.DecodeMark("x") == null && OutsiderTalkMath.DecodeMark("war|abc") == null, "a broken mark reads as none");
        var marks = new List<string>();
        for (int i = 0; i < 14; i++) OutsiderTalkMath.AddMark(marks, "k" + i, i, null, null);
        OutsiderTalkMath.AddMark(marks, "k13", 99, "new", null);
        C(marks.Count == OutsiderTalkMath.MaxMarks && marks.Count(s => OutsiderTalkMath.DecodeMark(s)!.key == "k13") == 1 && OutsiderTalkMath.DecodeMark(marks[^1])!.tick == 99,
            "ten marks kept, the newest, one per key");
        var mOver = new MarkFacts { key = "k13", subject = "new" };
        mOver.tags.Add("then:over");
        OutsiderTalkMath.PickedUp(marks, mOver, 200);
        C(marks.All(s => OutsiderTalkMath.DecodeMark(s)!.key != "k13"), "a callback about a change settles its mark");
        var mStill = new MarkFacts { key = "k12", subject = "x", fact = "ws:1" };
        mStill.tags.Add("then:still");
        OutsiderTalkMath.PickedUp(marks, mStill, 300);
        var kept = marks.Select(OutsiderTalkMath.DecodeMark).Single(m => m!.key == "k12");
        C(kept!.tick == 300 && kept.fact == "ws:1", "one still so is brought up to date");
        C(OutsiderTalkMath.AgoTag(0, OutsiderTalkMath.TicksPerDay) == "then:recent" && OutsiderTalkMath.AgoTag(0, 25 * OutsiderTalkMath.TicksPerDay) == "then:long"
          && OutsiderTalkMath.AgoTag(0, 10 * OutsiderTalkMath.TicksPerDay) == null, "how long ago, in tags");
        var said = new List<string>();
        for (int i = 0; i < 20; i++) OutsiderTalkMath.Remember(said, "p" + (i % 18), OutsiderTalkMath.MaxSaid);
        C(said.Count == OutsiderTalkMath.MaxSaid && said[^1] == "p1" && said.Distinct().Count() == said.Count, "pieces said kept newest last, once each");

        // ---------------- The library ----------------

        n += Library(root, C);
        return n;
    }

    /// <summary>Every tag a condition may name (Outsiders/OutsiderFacts.cs sets them), and the families open to any name after the prefix.</summary>
    private static readonly HashSet<string> Words = new HashSet<string>
    {
        "spring", "summer", "fall", "winter", "morning", "day", "evening", "night", "rain", "snow", "fog", "storm", "clear", "hot", "cold", "tribal", "medieval",
        "industrial", "spacer", "lowtech", "poor", "rich", "small", "large", "first", "met", "returning", "known", "close", "friends", "sweethearts", "lovers",
        "likesyou", "coolyou", "likesus", "wary", "kin", "kin:you", "town", "samefaith", "otherfaith", "situation", "condition", "byyou", "old", "teen", "hurt",
        "grieving", "lowmood", "highmood", "talks:3", "talks:8", "world:war", "battle:here", "battle:theirs"
    }.Concat(Expand("tone:", "warm good plain cool sour"))
        .Concat(Expand("did:", "gift meal teach spar duel provoke faith court letter bring"))
        .Concat(Expand("role:", "trader guest lodger visitor notable head heir envoy"))
        .Concat(Expand("them:", "young old hurt kind abrasive leader married widowed spousehere parent smallchild childhere"))
        .Concat(Expand("us:", "young old hurt newcomer leader speaker noble married widowed spousehere parent smallchild childhere"))
        .Concat(Expand("their:", "tribal medieval industrial spacer lowtech hightech empire noble leader war peace friend"))
        .Concat(Expand("home:", "desert frozen jungle swamp forest plains mountains hills coast cold hot mild"))
        .Concat(Expand("town:", "near far head notable newhead headdied loss ill letter wrote site quest caravan visitors short:food short:medicine short:hands short:fuel"))
        .Concat(Expand("war:", "new long losing winning fought ourfoe ourfriend"))
        .Concat(Expand("situation:", "theirs bad good new long helped ended sickness hunger cold war trade faith politics empire disaster beasts"))
        .Concat(Expand("ended:", "theirs helped sickness hunger cold war trade faith politics empire disaster beasts"))
        .Concat(Expand("colony:", "raid raidby raid:them raid:theirfoe raid:fallen raid:mechs raid:insects raid:animals raid:tribal raid:people death death:battle birth "
                                  + "wedding newcomer decree election camp harvest hungry plenty disaster research project day daytoday daysoon daypast traders young old "
                                  + "children animals prisoners expecting"))
        .Concat(Expand("byyou:", "freed tended helped"))
        .Concat(Expand("then:", "still over dead changed recent long"))
        .ToHashSet();

    private static IEnumerable<string> Expand(string prefix, string words) => words.Split(' ').Select(w => prefix + w);

    private static readonly string[] Families = { "ws:", "condition:", "deed:", "deedbest:", "wrongbest:", "talked:", "last:", "trait:", "skill:", "them:best:", "us:best:" };

    /// <summary>The placeholders the game fills (Outsiders/OutsiderFacts.cs and OutsiderTalk.cs).</summary>
    private static readonly HashSet<string> Placeholders = new()
    {
        "COLONIST", "OUTSIDER", "COLONY", "PEOPLE", "TOWN", "HOMELAND", "SEASON", "WEATHER", "LEADER", "LEADERTITLE", "THEIRTITLE", "OURTITLE", "THEIRROLE",
        "THEIRFAITH", "OURFAITH", "THEIRSKILL", "OURSKILL", "THEIRSPOUSE", "THEIRCHILD", "OURSPOUSE", "OURCHILD", "KIN", "KINWORD", "HEAD", "HEADTITLE",
        "NOTABLE", "NOTABLETITLE", "ILL", "ILLTITLE", "LOST", "LOSTTITLE", "OLDHEAD", "WROTE", "WRITER", "SITE", "QUEST", "WAR", "PEACEWITH", "WARA", "WARB",
        "BATTLEA", "BATTLEB", "SITUATION", "SITUATIONPLACE", "ENDED", "CONDITION", "COOK", "ARTIST", "OURLEADER", "SPEAKER", "DISASTER", "CAMP", "TRADERS",
        "NEWCOMER", "DAY", "RAIDERS", "DEAD", "BORN", "WEDDING", "DECREE", "RESEARCH", "PROJECT", "DEEDNAME", "DEEDBY", "WRONGNAME", "YOURDEEDNAME", "FRIEND",
        "THEN", "NOW"
    };

    private static int Library(string root, Action<bool, string> C0)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            C0(ok, what);
        }

        string dir = Path.Combine(root, "Defs", "OutsiderTalkDefs");
        var defs = Directory.GetFiles(dir, "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.OutsiderTalkDef")).ToList();
        C(defs.Count >= 150, $"a library of conversation pieces ({defs.Count})");
        var names = defs.Select(d => d.Element("defName")?.Value ?? "").ToList();
        C(names.Distinct().Count() == names.Count && names.All(x => x.StartsWith("Rimfolk_OT_")), "each piece has its own name, Rimfolk_OT_");
        var pieces = new List<TalkPiece>();
        string facts = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Outsiders", "OutsiderFacts.cs"));
        foreach (var d in defs)
        {
            string name = d.Element("defName")!.Value;
            var lines = d.Element("lines")?.Elements("li").Select(li => li.Value).ToList() ?? new List<string>();
            float w = float.TryParse(d.Element("weight")?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var wv) ? wv : 1f;
            var errs = new List<string>();
            var p = OutsiderTalkMath.Parse(name, d.Element("topic")?.Value, d.Element("uses")?.Value, w, d.Element("when")?.Value, lines, d.Element("heard")?.Value,
                d.Element("mark")?.Value, d.Element("about")?.Value, errs);
            C(errs.Count == 0, $"{name} is well formed ({string.Join("; ", errs)})");
            C(!string.IsNullOrEmpty(p.topic), $"{name} has a topic");
            C(p.uses.All(u => u == "chat" || u == "home" || u == "know"), $"{name}: uses are chat, home or know");
            pieces.Add(p);
            foreach (var raw in lines)
            {
                string body = Strip(raw);
                C(!body.StartsWith("\"") && !body.EndsWith("\""), $"{name}: '{body}' has no quotes around it");
                C(FirstSentence(body) == body, $"{name}: '{body}' is one sentence");
                C(body.Length <= 90, $"{name}: '{body}' is 90 characters at most ({body.Length})");
                C(body.Count(ch => ch == '!') <= 1, $"{name}: '{body}' has one '!' at most");
                C(!body.Contains('—') && !body.Contains(" - "), $"{name}: '{body}' has no dashes");
            }
            // Conditions name things the game says.
            var tags = new List<string>();
            void AddTags(LineTags.Parsed? parsed)
            {
                if (parsed?.alts == null) return;
                foreach (var alts in parsed.alts)
                    foreach (var a in alts)
                        tags.Add(a.name);
            }
            AddTags(p.when);
            foreach (var t in p.turns)
                foreach (var l in t)
                    AddTags(l.tags);
            foreach (var t in tags)
                C(Words.Contains(t) || Families.Any(fam => t.StartsWith(fam) && t.Length > fam.Length), $"{name}: the condition '{t}' is one the game sets");
            // Placeholders are ones the game fills, and the right ones for what the piece is about.
            var holes = p.turns.SelectMany(t => t).SelectMany(l => l.holes).Concat(p.heardHoles).Distinct().ToList();
            foreach (var hName in holes) C(Placeholders.Contains(hName), $"{name}: {{{hName}}} is a placeholder the game fills");
            C(p.about == null || Placeholders.Contains(p.about), $"{name}: <about> names a placeholder");
            string when = d.Element("when")?.Value ?? "";
            bool LineSays(string tag) => when.Contains(tag) || p.turns.SelectMany(t => t).Any(l => l.holes.Length > 0 && l.tags.tags != null && l.tags.tags.Any(x => x.Contains(tag)));
            if (holes.Contains("DEEDNAME") || holes.Contains("DEEDBY")) C(LineSays("deedbest:"), $"{name}: {{DEEDNAME}} is said with the deed it comes from (deedbest:)");
            if (holes.Contains("WRONGNAME")) C(LineSays("wrongbest:"), $"{name}: {{WRONGNAME}} is said with the wrong it comes from (wrongbest:)");
            if (holes.Contains("THEN")) C(p.callback, $"{name}: {{THEN}} only in a callback");
            if (holes.Contains("NOW")) C(when.Contains("then:changed"), $"{name}: {{NOW}} only when someone new leads (then:changed)");
            if (holes.Contains("YOURDEEDNAME")) C(when.Contains("byyou"), $"{name}: {{YOURDEEDNAME}} with byyou");
            // A piece can be said: in a moment that has its conditions (and, for a turn written only for particular
            // moments, the first of them) and a value for every placeholder, it fits.
            C(Sayable(p), $"{name} can be said when its conditions hold");
        }

        // The library as a whole.
        var marked = pieces.Where(p => p.mark != null).Select(p => p.mark).ToHashSet();
        foreach (var p in pieces.Where(p => p.callback))
            foreach (var t in p.when.tags!.Where(t => t.StartsWith("talked:")))
                C(marked.Contains(t.Substring(7)), $"{p.name}: something leaves the mark {t.Substring(7)} for it to pick up");
        foreach (var key in marked) C(pieces.Any(p => p.callback && p.when.tags!.Contains("talked:" + key)), $"the mark '{key}' is picked up by a callback");
        foreach (var p in pieces.Where(p => p.about != null))
            C(facts.Contains("\"" + p.about + "\""), $"{p.name}: the fact behind {{{p.about}}} is read (OutsiderFacts)");
        int home = pieces.Count(p => p.For("home")), know = pieces.Count(p => p.For("know"));
        C(home >= 40, $"plenty to tell when asked about home ({home})");
        C(know >= 25, $"plenty for getting to know them ({know})");
        C(pieces.Count(p => p.toned && p.when.tags!.Any(t => t.Contains("tone:cool") || t.Contains("tone:sour"))) >= 6, "pieces for a talk that goes cool or sour");
        C(pieces.Count(p => p.forYoungThem) >= 2 && pieces.Count(p => p.forYoungUs) >= 2, "pieces for children on either side");
        C(pieces.Count(p => p.callback) >= 15, $"callbacks ({pieces.Count(p => p.callback)})");
        int longOnes = pieces.Count(p => p.turns.Count >= 5);
        C(longOnes >= 40, $"longer exchanges of five or six lines ({longOnes})");
        C(pieces.Any(p => p.turns.Count == 6), "some of six lines");
        foreach (var topic in new[] { "greeting", "road", "weather", "season", "land", "town", "war", "situation", "colony", "trade", "work", "family", "faith", "deeds", "callback", "hardship" })
            C(pieces.Count(p => p.topic == topic) >= 3, $"three or more pieces about {topic}");
        string all = string.Join("\n", defs.Select(d => d.ToString()));
        foreach (var voice in new[] { "their:tribal", "their:medieval", "their:empire", "their:noble", "their:hightech", "their:lowtech", "their:spacer" })
            C(Regex.Matches(all, Regex.Escape(voice)).Count >= (voice == "their:spacer" ? 1 : 3), $"lines in the voice of {voice}");
        foreach (var fact in new[] { "war:new", "war:losing", "war:winning", "their:peace", "world:war", "battle:here", "situation:new", "situation:ended", "town:newhead",
                     "town:loss", "town:ill", "town:site", "colony:raidby", "colony:death", "colony:birth", "colony:wedding", "colony:election", "deed:released",
                     "condition", "kin", "then:changed", "then:dead" })
            C(all.Contains(fact), $"the library speaks of {fact}");
        return n;
    }

    private static bool Sayable(TalkPiece p)
    {
        var f = new TalkFacts();
        MarkFacts? m = null;
        void Positive(LineTags.Parsed parsed, Action<string> add)
        {
            if (parsed?.alts == null) return;
            foreach (var alts in parsed.alts)
            {
                var first = alts.FirstOrDefault(a => !a.neg);
                if (first.raw != null) add(first.name);
            }
        }
        if (p.callback)
        {
            m = new MarkFacts { key = "k" };
            Positive(p.when, t =>
            {
                if (t.StartsWith("talked:") || t.StartsWith("then:")) m.tags.Add(t);
                else f.tags.Add(t);
            });
            m.values["THEN"] = "x";
            m.values["NOW"] = "x";
        }
        else Positive(p.when, t => f.tags.Add(t));
        if (!p.toned) f.tags.Add("tone:plain");
        foreach (var hName in Placeholders) f.values[hName] = "x";
        if (OutsiderTalkMath.Fits(p, f, m, null, 6, false)) return true;
        // A turn written only for particular moments: the first of them.
        foreach (var turn in p.turns)
        {
            var l = turn[0];
            Positive(l.tags, t => (l.us ? f.usTags : f.themTags).Add(t));
        }
        return OutsiderTalkMath.Fits(p, f, m, null, 6, false);
    }

    private static string Strip(string line)
    {
        string t = line.TrimStart();
        if (!t.StartsWith("[")) return t.Trim();
        int end = t.IndexOf(']');
        return end < 0 ? t.Trim() : t.Substring(end + 1).Trim();
    }

    /// <summary>The first sentence: up to the first '.', '!' or '?' from the 13th character on (as ImmersionTests reads it).</summary>
    private static string FirstSentence(string s)
    {
        for (int i = 12; i < s.Length; i++)
            if (s[i] == '.' || s[i] == '!' || s[i] == '?')
                return s.Substring(0, i + 1);
        return s;
    }
}
