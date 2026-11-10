using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Whom a letter goes to (the player, 10 November 2026: "it should make sense what kind of messages you can send whom, and
/// which kind of messages are sent to the colony as a whole ... this should be differentiated"; World/LetterAddressMath.cs)
/// and the places heard of (the same day: "if other colonies tell me about things that are nearby on their map, will these
/// also be revealed on my own map for reference?"; World/HeardPlaceMath.cs): every purpose's scope, who answers it for every
/// kind of addressee (a head, the heir, an envoy, a trader, a face met, their people's leader, a town with no head known),
/// how it goes there, what passing it on costs and what the go-between's word is worth, the words, an older save's letter,
/// the places' keys, keep time and fading, the map links, and the seams.
/// </summary>
public static class LetterRouteTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void C(bool ok, string what)
        {
            cases++;
            check(ok, "whom a letter goes to: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var keys = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var e in XDocument.Load(f).Root!.Elements())
                keys[e.Name.LocalName] = e.Value;
        Scopes(C);
        Answerers(C);
        Ways(C);
        Passing(C);
        Words(C, keys);
        OlderSave(C);
        Places(C);
        MapLinks(C);
        Seams(root, Code, C, keys);
        return cases;
    }

    private static readonly LetterPurpose[] All = (LetterPurpose[])Enum.GetValues(typeof(LetterPurpose));

    private static readonly LetterPurpose[] Personal = { LetterPurpose.Greetings, LetterPurpose.Condolences, LetterPurpose.News, LetterPurpose.Invitation, LetterPurpose.Match };

    private static readonly LetterPurpose[] Town =
    {
        LetterPurpose.Warning, LetterPurpose.Help, LetterPurpose.Ransom, LetterPurpose.Envoy, LetterPurpose.Ward, LetterPurpose.TakeBack
    };

    private static readonly LetterPurpose[] People = { LetterPurpose.Pact, LetterPurpose.Truce, LetterPurpose.Fealty };

    // ---------------- Whose matter ----------------

    private static void Scopes(Action<bool, string> C)
    {
        foreach (var p in Personal)
            foreach (bool t in new[] { false, true })
                C(LetterAddressMath.ScopeOf(p, t) == LetterScope.Personal, $"{p} is a personal letter, to anyone");
        foreach (var p in Town)
            foreach (bool t in new[] { false, true })
                C(LetterAddressMath.ScopeOf(p, t) == LetterScope.Town, $"{p} is the town's matter, for its head");
        foreach (var p in People)
            foreach (bool t in new[] { false, true })
                C(LetterAddressMath.ScopeOf(p, t) == LetterScope.People, $"{p} is the whole people's matter, for their leader");
        C(LetterAddressMath.ScopeOf(LetterPurpose.Favour, false) == LetterScope.Town && LetterAddressMath.ScopeOf(LetterPurpose.Favour, true) == LetterScope.Personal,
            "a debt: their people's is the town's matter, one owed in person is personal");
        C(LetterAddressMath.ScopeOf(LetterPurpose.Dealing, false) == LetterScope.Town && LetterAddressMath.ScopeOf(LetterPurpose.Dealing, true) == LetterScope.Personal,
            "a dealing: their council's is the town's matter, an answer to their own letter is personal");
        C(LetterAddressMath.ScopeOf(LetterPurpose.Task, false) == LetterScope.Personal, "the retired council's business never goes to a head");
        foreach (var p in All)
            C(LetterAddressMath.Mixed(p) == (p == LetterPurpose.Favour || p == LetterPurpose.Dealing), $"{p}: its topic decides its scope only for a debt and a dealing");
        C(Personal.Concat(Town).Concat(People).Concat(new[] { LetterPurpose.Favour, LetterPurpose.Dealing, LetterPurpose.Task }).Distinct().Count() == All.Length,
            "every purpose is in one of the lists");
        C(LetterAddressMath.ReplyTopic("reply:3") && !LetterAddressMath.ReplyTopic("deal:supply:0:12") && !LetterAddressMath.ReplyTopic(null), "a reply's topic is known by its prefix");
        C(LetterAddressMath.PersonalTopic(LetterPurpose.Favour, "tie:4", true) && !LetterAddressMath.PersonalTopic(LetterPurpose.Favour, "tie:4", false)
          && !LetterAddressMath.PersonalTopic(LetterPurpose.Favour, "side:4", false), "a debt's topic is personal only when owed in person");
        C(LetterAddressMath.PersonalTopic(LetterPurpose.Dealing, "reply:9", false) && !LetterAddressMath.PersonalTopic(LetterPurpose.Dealing, "deal:x", false),
            "a dealing's topic is personal only as an answer to their letter");
        C(!LetterAddressMath.PersonalTopic(LetterPurpose.Warning, "reply:9", true), "any other purpose's topic never makes it personal");
        foreach (var sc in new[] { LetterScope.Personal, LetterScope.Town, LetterScope.People })
            C(LetterAddressMath.TownSection(sc) == (sc != LetterScope.Personal), $"{sc}: the window's section is 'To the town' for the town's and people's matters");
    }

    // ---------------- Who answers ----------------

    private static AddressFacts Kind(string kind)
    {
        switch (kind)
        {
            case "head": return new AddressFacts { role = NotableRole.Head, townHead = true, leaderKnown = true, speakerKnown = true };
            case "leader": return new AddressFacts { role = NotableRole.Head, leader = true, townHead = true, leaderKnown = true, speakerKnown = true, receiver = true };
            case "heir": return new AddressFacts { role = NotableRole.Heir, townHead = true, leaderKnown = true, speakerKnown = true };
            case "envoy": return new AddressFacts { role = NotableRole.Envoy, townHead = true, leaderKnown = true, speakerKnown = true };
            case "trader": return new AddressFacts { role = NotableRole.Trader, townHead = true, leaderKnown = true, speakerKnown = true };
            case "face": return new AddressFacts { role = NotableRole.Acquaintance, townHead = true, leaderKnown = true, speakerKnown = true };
            case "no head": return new AddressFacts { role = NotableRole.Trader, townHead = false, leaderKnown = false, speakerKnown = false };
            case "no head, a speaker": return new AddressFacts { role = NotableRole.Trader, townHead = false, leaderKnown = false, speakerKnown = true };
            case "nearest head": return new AddressFacts { role = NotableRole.Head, townHead = true, nearestHead = true, speakerKnown = true, receiver = true };
            case "other head": return new AddressFacts { role = NotableRole.Head, townHead = true, speakerKnown = true };
            default: throw new ArgumentException(kind);
        }
    }

    private static readonly string[] Kinds = { "head", "leader", "heir", "envoy", "trader", "face", "no head", "no head, a speaker", "nearest head", "other head" };

    private static void Answerers(Action<bool, string> C)
    {
        foreach (var kind in Kinds)
        {
            var f = Kind(kind);
            bool head = f.role == NotableRole.Head;
            foreach (var p in Personal)
                C(LetterAddressMath.WhoAnswers(p, LetterScope.Personal, f) == LetterAnswerer.Self, $"{p} to a {kind}: they answer it themselves");
            foreach (var p in Town)
            {
                var a = LetterAddressMath.WhoAnswers(p, LetterScope.Town, f);
                var want = head ? LetterAnswerer.Self : f.townHead ? LetterAnswerer.TownHead : LetterAnswerer.None;
                C(a == want, $"{p} to a {kind}: {want}");
            }
            foreach (var p in People)
            {
                var a = LetterAddressMath.WhoAnswers(p, LetterScope.People, f);
                var want = f.leader ? LetterAnswerer.Self : f.leaderKnown ? LetterAnswerer.Speaker : head && f.nearestHead ? LetterAnswerer.Self
                    : f.speakerKnown ? LetterAnswerer.Speaker : LetterAnswerer.None;
                C(a == want, $"{p} to a {kind}: {want}");
            }
            foreach (bool personal in new[] { false, true })
            {
                var sc = LetterAddressMath.ScopeOf(LetterPurpose.Favour, personal);
                var a = LetterAddressMath.WhoAnswers(LetterPurpose.Favour, sc, f);
                C(personal ? a == LetterAnswerer.Self : a == (head ? LetterAnswerer.Self : f.townHead ? LetterAnswerer.TownHead : LetterAnswerer.None),
                    $"a debt {(personal ? "owed in person" : "of their people")} to a {kind}: {a}");
                var d = LetterAddressMath.WhoAnswers(LetterPurpose.Dealing, LetterAddressMath.ScopeOf(LetterPurpose.Dealing, personal), f);
                C(personal ? d == LetterAnswerer.Self : d == (f.receiver ? LetterAnswerer.Self : f.townHead ? LetterAnswerer.TownHead : LetterAnswerer.None),
                    $"a dealing {(personal ? "answering their letter" : "with their council")} to a {kind}: {d}");
            }
        }
        C(LetterAddressMath.WhoAnswers(LetterPurpose.Pact, LetterScope.People, Kind("other head")) == LetterAnswerer.Speaker,
            "a pact to another town's head, their leader unknown: the nearest town's head speaks for the people");
        C(LetterAddressMath.WhoAnswers(LetterPurpose.Truce, LetterScope.People, Kind("head")) == LetterAnswerer.Speaker,
            "a truce to a town's head with their leader known: it goes on to the leader");
        C(LetterAddressMath.WhoAnswers(LetterPurpose.Warning, LetterScope.Town, Kind("leader")) == LetterAnswerer.Self,
            "the leader heads their seat: the town's matters too");
    }

    // ---------------- How it goes ----------------

    private static void Ways(Action<bool, string> C)
    {
        foreach (var p in All)
        {
            C(LetterAddressMath.Way(p, LetterAnswerer.Self) == LetterWay.Direct, $"{p} to the one who answers it: direct");
            C(LetterAddressMath.Way(p, LetterAnswerer.None) == LetterWay.NoHead, $"{p} with nobody known to answer it: none");
            var want = p == LetterPurpose.Dealing ? LetterWay.HeadOnly : LetterWay.PassOn;
            C(LetterAddressMath.Way(p, LetterAnswerer.TownHead) == want && LetterAddressMath.Way(p, LetterAnswerer.Speaker) == want,
                $"{p} to someone else: {want}");
            C(LetterAddressMath.Passable(p) == (p != LetterPurpose.Dealing), $"{p}: passed on unless it is a council's dealing");
        }
        // Every purpose and kind of addressee, end to end: never a personal letter passed on, a town's matter never read by a
        // trader with a head known, nothing sent with nobody to answer it.
        foreach (var kind in Kinds)
            foreach (var p in All)
                foreach (bool personal in new[] { false, true })
                {
                    var f = Kind(kind);
                    var sc = LetterAddressMath.ScopeOf(p, personal);
                    var way = LetterAddressMath.Way(p, LetterAddressMath.WhoAnswers(p, sc, f));
                    if (sc == LetterScope.Personal) C(way == LetterWay.Direct, $"{p} to a {kind}, personal: direct");
                    else if (sc == LetterScope.Town && f.role != NotableRole.Head && f.townHead && p != LetterPurpose.Dealing)
                        C(way == LetterWay.PassOn, $"{p} to a {kind} with a head known: passed on");
                    else if (sc == LetterScope.Town && !f.townHead && f.role != NotableRole.Head && !(p == LetterPurpose.Dealing && f.receiver))
                        C(way == LetterWay.NoHead, $"{p} to a {kind} with no head known: says so");
                    else C(way != LetterWay.PassOn || LetterAddressMath.Passable(p), $"{p} to a {kind}: never passed on when it can't be");
                }
    }

    // ---------------- Passing it on ----------------

    private static void Passing(Action<bool, string> C)
    {
        C(LetterAddressMath.PassDays(true, 9f) == 1f, "within their town: a day more, whatever the road");
        C(LetterAddressMath.PassDays(false, 3.5f) == 3.5f, "to another town: the leg between the towns");
        C(LetterAddressMath.PassDays(false, 0.2f) == 1f && LetterAddressMath.PassDays(false, -1f) == 1f, "never less than a day");
        for (float leg = 0f; leg < 12f; leg += 0.5f)
            C(LetterAddressMath.PassDays(false, leg) >= LetterAddressMath.PassDays(true, leg) - 0.0001f || leg < 1f, $"another town is never quicker than the same one ({leg} days)");
        for (float r = 0f; r <= 1f; r += 0.1f)
        {
            float same = LetterAddressMath.PassCallHours(true, r), far = LetterAddressMath.PassCallHours(false, r);
            C(same >= 3f && same <= 8f && far >= 8f && far <= 22f && far >= same, $"a call passed on: some hours within their town, more from another ({r:0.0})");
        }
        C(LetterAddressMath.PassCallHours(true, -5f) == 3f && LetterAddressMath.PassCallHours(false, 5f) == 22f, "the roll is held to 0..1");
    }

    // ---------------- The go-between's word ----------------

    private static void Words(Action<bool, string> C, Dictionary<string, string> keys)
    {
        var roles = new[] { NotableRole.Head, NotableRole.Heir, NotableRole.Envoy, NotableRole.Trader, NotableRole.Acquaintance };
        foreach (var role in roles)
        {
            float last = float.MinValue;
            for (int op = -100; op <= 100; op += 5)
            {
                float w = LetterAddressMath.Word(role, op);
                C(w >= last, $"{role}'s word never falls as they think better of the colony ({op})");
                last = w;
                int level = NotableMath.Level(op);
                bool weighty = role == NotableRole.Heir || role == NotableRole.Envoy;
                if (level <= 2) C(w > 0f, $"{role} who thinks well of the colony ({op}): their word helps");
                if (level >= 4) C(w < 0f, $"{role} who thinks poorly of the colony ({op}): their word tells against it");
                if (level == 3) C(weighty ? w == 2f : w == 0f, $"{role}, civil ({op}): {(weighty ? "their place lends it a little" : "no word for or against")}");
                if (weighty && level <= 2) C(w == 2f * LetterAddressMath.Word(NotableRole.Trader, op), $"{role}'s good word counts double ({op})");
                if (weighty && level >= 4) C(w == LetterAddressMath.Word(NotableRole.Trader, op), $"{role}'s poor word is no worse than anyone's ({op})");
                C(w <= 12f && w >= -9f, $"{role}'s word stays small beside the head's own regard ({op})");
                string key = LetterAddressMath.WordKey(role, op);
                C(keys.ContainsKey(key), $"{key} is a key");
                C(key == (w < 0f ? "CC_Route_Word_Poor" : w == 0f ? "CC_Route_Word_Plain" : weighty ? "CC_Route_Word_Weighty" : "CC_Route_Word_Warm"), $"{role} ({op}): the words match the word's weight");
            }
        }
        // The word moves how the letter lands, and nothing else.
        var x = new LetterInput { purpose = LetterPurpose.Warning, opinion = 20, ofWriter = 10, social = 8, warmth = 0 };
        float plain = NotableMath.Score(x);
        x.word = 6f;
        C(Math.Abs(NotableMath.Score(x) - plain - 6f) < 0.001f, "the word adds to the score as it is");
        x.word = 0f;
        C(Math.Abs(NotableMath.Score(x) - plain) < 0.001f, "a letter not passed on scores as it always did");
        foreach (bool call in new[] { false, true })
            foreach (bool spoken in new[] { false, true })
                foreach (bool olde in new[] { false, true })
                {
                    string key = LetterAddressMath.PassLineKey(call, spoken, olde);
                    C(keys.TryGetValue(key, out var text) && text.StartsWith("{NAME} ") && text.EndsWith(".") && text.Count(ch => ch == '.') == 1 && !text.Contains("!"),
                        $"{key}: the head's line says who brought it, in one plain sentence");
                    if (olde) C(Regex.IsMatch(text ?? "", @"\b(hath|thy)\b"), $"{key}: a medieval hand's own words");
                }
        foreach (var t in new[] { LetterAnswerer.Self, LetterAnswerer.TownHead, LetterAnswerer.None })
            foreach (bool leader in new[] { false, true })
                C(keys.ContainsKey(LetterAddressMath.HeaderKey(t, leader)), $"the header for {t}{(leader ? ", the leader" : "")} is a key");
        C(LetterAddressMath.HeaderKey(LetterAnswerer.Self, true) == "CC_Route_Header_Leader" && LetterAddressMath.HeaderKey(LetterAnswerer.TownHead, false) == "CC_Route_Header_Other"
          && LetterAddressMath.HeaderKey(LetterAnswerer.None, false) == "CC_Route_Header_NoHead" && LetterAddressMath.HeaderKey(LetterAnswerer.Self, false) == "CC_Route_Header_Head",
            "the header by who they are to their town");
        C(keys["CC_Route_Header_Other"].Contains("personal letters") && keys["CC_Route_Header_Other"].Contains("For the town's matters, write to {HEAD}"),
            "someone who isn't the head: personal letters, and the town's matters to the head, as the player's example");
        C(keys["CC_Route_Header_NoHead"].Contains("Nobody") && keys["CC_Route_Header_NoHead"].Contains("meeting their people") && keys["CC_Route_Why_NoHead"].Contains("their news"),
            "no head known: says so, with how one comes to be known");
        C(keys["CC_Route_Why_HeadOnly"].StartsWith("Only the town's head can answer this"), "a council's dealing to anyone else: only the town's head can answer this");
        C(keys["CC_Route_Section_Town"] == "To the town" && keys["CC_Route_Section_Person"] == "To {NAME}" && keys["CC_Route_Section_TownVia"].Contains("ask {NAME} to pass it on"),
            "the window's two sections");
        foreach (bool same in new[] { false, true })
            foreach (bool call in new[] { false, true })
                C(keys.ContainsKey(LetterAddressMath.RouteKey(same, call)), $"the route line ({(call ? "call" : same ? "same town" : "another town")}) is a key");
        foreach (var p in new[] { LetterPurpose.Favour, LetterPurpose.Dealing })
            foreach (var sc in new[] { LetterScope.Town, LetterScope.Personal })
                C(keys.ContainsKey("CC_Route_Why_Nothing_" + p + "_" + sc), $"nothing to write of for {p} ({sc}) says so");
        foreach (var k in keys.Where(k => k.Key.StartsWith("CC_Route_") || k.Key.StartsWith("CC_Heard_")))
            C(!k.Value.Contains("!") && !k.Value.Contains("—"), $"{k.Key}: no exclamation marks or em dashes");
    }

    // ---------------- An older save ----------------

    private static void OlderSave(Action<bool, string> C)
    {
        // A letter written before this to a trader about a warning (to anyone, then): the trader's town has a head, so it is
        // read as passed on; a letter to the head itself, or a personal one, is read as it was.
        var trader = Kind("trader");
        C(LetterAddressMath.Way(LetterPurpose.Warning, LetterAddressMath.WhoAnswers(LetterPurpose.Warning, LetterScope.Town, trader)) == LetterWay.PassOn,
            "an older save's warning to a trader: passed on to the head");
        C(LetterAddressMath.Way(LetterPurpose.Pact, LetterAddressMath.WhoAnswers(LetterPurpose.Pact, LetterScope.People, Kind("envoy"))) == LetterWay.PassOn,
            "an older save's pact to an envoy: passed on to their leader");
        C(LetterAddressMath.Way(LetterPurpose.Greetings, LetterAddressMath.WhoAnswers(LetterPurpose.Greetings, LetterScope.Personal, trader)) == LetterWay.Direct,
            "an older save's greetings: read by the one written to");
        C(LetterAddressMath.Way(LetterPurpose.Help, LetterAddressMath.WhoAnswers(LetterPurpose.Help, LetterScope.Town, Kind("no head"))) == LetterWay.NoHead,
            "an older save's request with nobody known at the head of the town: no one to pass it to, read as it was (LetterRoutes.Passes)");
    }

    // ---------------- Places heard of ----------------

    private static void Places(Action<bool, string> C)
    {
        C(HeardPlaceMath.NamesPlace("site:40") && HeardPlaceMath.NamesPlace("ws:7") && !HeardPlaceMath.NamesPlace("ill:3") && !HeardPlaceMath.NamesPlace("head:1:2")
          && !HeardPlaceMath.NamesPlace(null), "a site and a world event name a place; a notable doesn't");
        C(HeardPlaceMath.ParseFact("site:40", out bool site, out int id) && site && id == 40, "a site by its world object");
        C(HeardPlaceMath.ParseFact("ws:7", out site, out id) && !site && id == 7, "a world event by its id");
        C(!HeardPlaceMath.ParseFact("ws:Council_WS_Locusts", out _, out _) && !HeardPlaceMath.ParseFact("site:", out _, out _) && !HeardPlaceMath.ParseFact("site:-3", out _, out _),
            "a world event's kind tag or a broken fact names no place");
        int day = HeardPlaceMath.TicksPerDay;
        C(!HeardPlaceMath.Gone(0, 59 * day, HeardPlaceMath.KeepDays) && HeardPlaceMath.Gone(0, 61 * day, HeardPlaceMath.KeepDays), "kept the news's own time, then gone");
        C(HeardPlaceMath.Gone(10 * day, 5 * day, HeardPlaceMath.KeepDays), "heard in a time that hasn't come: gone");
        C(HeardPlaceMath.KeepDays == OutsiderTalkMath.HeardDays, "kept as long as the news heard is");
        float last = 2f;
        for (int d = 0; d <= 60; d++)
        {
            float a = HeardPlaceMath.Alpha(0, d * day, HeardPlaceMath.KeepDays);
            C(a <= last && a >= HeardPlaceMath.MinAlpha && a <= 1f, $"fades evenly and never below its faintest (day {d})");
            if (d <= 48) C(a == 1f, $"whole for the first four fifths of its time (day {d})");
            last = a;
        }
        C(Math.Abs(HeardPlaceMath.Alpha(0, 60 * day, HeardPlaceMath.KeepDays) - HeardPlaceMath.MinAlpha) < 0.001f, "at its faintest as it goes");
        var list = new List<HeardPlaceFacts> { new HeardPlaceFacts { key = "s40" }, new HeardPlaceFacts { key = "w123" } };
        C(HeardPlaceMath.Same(list, "w123") == 1 && HeardPlaceMath.Same(list, "t55") == -1 && HeardPlaceMath.Same(null, "s40") == -1, "the same place heard again is one mark");
        C(HeardPlaceMath.Over(0) == 0 && HeardPlaceMath.Over(HeardPlaceMath.Max - 1) == 0 && HeardPlaceMath.Over(HeardPlaceMath.Max) == 1 && HeardPlaceMath.Over(HeardPlaceMath.Max + 4) == 5,
            "the oldest go so a new one fits under the cap");
        // A long game: never more than the cap.
        var keys = new List<string>();
        var rnd = new Random(7);
        for (int i = 0; i < 2000; i++)
        {
            string k = "s" + rnd.Next(0, 80);
            int same = HeardPlaceMath.Same(keys.Select(x => new HeardPlaceFacts { key = x }).ToList(), k);
            if (same >= 0) keys.RemoveAt(same);
            int over = HeardPlaceMath.Over(keys.Count);
            if (over > 0) keys.RemoveRange(0, over);
            keys.Add(k);
            if (keys.Count > HeardPlaceMath.Max || keys.Distinct().Count() != keys.Count)
            {
                C(false, $"never over the cap, never one place twice (after {i})");
                break;
            }
        }
        C(keys.Count <= HeardPlaceMath.Max, "two thousand places heard of: still under the cap");
    }

    // ---------------- "Show on map" ----------------

    private static void MapLinks(Action<bool, string> C)
    {
        string link = LinkTextMath.WrapOnMap("Show on map", "s40");
        var runs = LinkTextMath.Parse("A camp. " + link);
        C(runs.Count == 2 && runs[1].thing == "@s40" && runs[1].text == "Show on map", "the link keeps its place with the map's mark");
        C(LinkTextMath.Plain("A camp. " + link) == "A camp. Show on map", "plain, the words only");
        C(LinkTextMath.OnMap("@s40", out var bare) && bare == "s40" && !LinkTextMath.OnMap("s40", out bare) && bare == "s40", "the mark read back");
        var lay = LinkTextMath.Lay(LinkTextMath.Parse("Edda " + LinkTextMath.Wrap("Tallbrook", "t55") + " " + link), 400f, s => s.Length * 7f, 4f);
        C(lay.things.Count == 2 && lay.onMap.Count == 2 && !lay.onMap[0] && lay.onMap[1] && lay.things[1] == ThingRef.Site(40) && lay.things[0].kind == ThingKind.Town,
            "laid out: a card's link opens its card, the map's shows it on the map");
        C(LinkTextMath.WrapOnMap("Show on map", null) == "Show on map" && LinkTextMath.WrapOnMap("Show on map", "") == "Show on map", "no place: no link");
    }

    // ---------------- Seams ----------------

    private static void Seams(string root, Func<string, string> Code, Action<bool, string> C, Dictionary<string, string> keys)
    {
        string routes = Code("World/LetterRoutes.cs");
        C(routes.Contains("LetterAddressMath.WhoAnswers(p, scope, FactsOf(n, home))") && routes.Contains("LetterAddressMath.Way(p, a)"),
            "the game asks the pure rules who answers and how it goes");
        C(routes.Contains("LetterAddressMath.PassDays(same, leg)") && routes.Contains("LetterMath.LegDays(LivingEvents.Distance(from.Tile, to.Tile), LetterMath.MeansOf(Ages.Of(n.faction)))"),
            "passing it on takes the leg to the head's town by their people's means");
        C(routes.Contains("LetterAddressMath.Word(via.role, NotableBook.Opinion(via))"), "the go-between's word by their role and regard");
        C(routes.Contains("FavourCalls.HeadOf(f, home") && routes.Contains("LeaderOf(f) ?? FavourCalls.HeadOf"), "the people's speaker: their leader known, else the nearest town's head");
        C(routes.Contains("NeighbourLetters.GeneralWhyNot(n, p)") && routes.Contains("HeadWhyNot(who, p)") && routes.Contains("NeighbourLetters.PurposeWhyNot(who, p)")
          && routes.Contains("LetterOccasions.Held(who, p)"), "the checks: the one written to, the head it goes to, and the purpose's own on the head");
        string letters = Code("World/NeighbourLetters.cs");
        C(letters.Contains("passTo = who != n ? who.id : -1") && letters.Contains("LetterRoutes.ToVia(who, n)"), "a letter sent to someone else for the head names both");
        C(letters.Contains("if (now >= l.due) Arrive(l, n, l.writer, now);") && letters.Contains("if (LetterRoutes.Passes(l, n, out var who))"), "a letter reaching them is passed on or read");
        C(Regex.Matches(letters, @"LetterRoutes\.Passes\(").Count >= 3, "passed on by a letter, the colony's rider and a call alike");
        C(letters.Contains("x.word = LetterRoutes.WordOf(l, n);"), "the head reads it with the go-between's word");
        C(letters.Contains("Scribe_Values.Look(ref passTo, \"passTo\", -1);") && letters.Contains("Scribe_Values.Look(ref passed, \"passed\");"), "the letter keeps whom it is passed on to");
        C(letters.Contains("l.notable == n.id || l.passTo == n.id"), "one letter at a time to the head too");
        C(letters.Contains("public static string WhyNot(Notable n, LetterPurpose p) => LetterRoutes.WhyNot(n, p, false);"), "every caller's checks go by whom the letter goes to");
        C(Code("World/LetterOccasions.cs").Contains("LetterRoutes.WhyNot(n, p, true)"), "the occasions' checks too");
        string panel = Code("World/NeighboursPanel.cs");
        C(panel.Contains("LetterRoutes.Header(to, out other, map)") && panel.Contains("LetterRoutes.Route(to, cur.who, channel)") && panel.Contains("\"CC_Route_Section_Town\"")
          && panel.Contains("\"CC_Route_Section_Person\"") && panel.Contains("\"CC_Route_WriteTo\""), "the window: the header, the two sections, the route, the button to the head");
        C(panel.Contains("LetterRoutes.WhyNot(to, p, sc, true)"), "the window greys each purpose for its own reason");
        C(panel.Contains("LetterRoutes.SpeaksFor(s)") && Code("UI/Region/PeoplePage.cs").Contains("LetterRoutes.SpeaksFor(t.s)"), "the town's card and the people's page name who speaks for it");
        string form = Code("Letters/LetterForm.cs");
        C(form.Contains("LetterRoutes.PassLine(l, n, speech == LetterFormMath.Olde)") && form.Contains("var n = NeighbourLetters.ForWhom(l);") && form.Contains("CC_Route_OursNote"),
            "the letters' form: the head's line, the colony's letter to the head, given to the go-between");
        C(Code("World/Notables.cs").Contains("Scribe_Collections.Look(ref places, \"heardPlaces\", LookMode.Deep);") && Code("World/Notables.cs").Contains("HeardPlaces.Daily(now);"),
            "places heard of are kept in the neighbours' book and let go daily");
        C(Code("Outsiders/OutsiderTalk.cs").Contains("HeardPlaces.Heard(facts, text,") && Code("Outsiders/OutsiderBook.cs").Contains("Scribe_Values.Look(ref place, \"place\");"),
            "news told in talk marks its places and keeps the link");
        C(letters.Contains("HeardPlaces.AnswerIn(l);") && letters.Contains("HeardPlaces.FromLetter(l, n, facts, NewsShown(tone));"), "a letter's news marks its places once the answer is in");
        string news = Code("Text/Facts/FactsNews.cs");
        C(news.Contains("\"site:\" + site.ID") && news.Contains("\"ws:\" + s.id"), "the news names its places as the facts do");
        string heard = Code("World/HeardPlaces.cs");
        C(heard.Contains("[HarmonyPatch(typeof(WorldInterface), nameof(WorldInterface.WorldInterfaceOnGUI))]") && heard.Contains("if (broken || !RimfolkPatches.On) return;"),
            "the world map's marks are a patch behind the deeper hooks");
        C(heard.Contains("HeardPlaceMath.Alpha(h.tick, now, HeardPlaceMath.KeepDays)") && heard.Contains("HeardPlaceMath.Over(list.Count)") && heard.Contains("HeardPlaceMath.Same("),
            "the game keeps and fades the marks by the pure rules");
        C(Code("UI/RegionMapView.cs").Contains("DrawHeard(w, h);") && Code("UI/RegionMapView.cs").Contains("HeardPlaces.TipsAt(m.obj)"), "the region map draws them and tells of them");
        C(Code("Outsiders/OutsiderHeard.cs").Contains("HeardPlaces.ShowOnMap(h.place)") && Code("UI/Cards/LinkText.cs").Contains("Cards.ShowOnMap(thing)"),
            "heard lines' Show on map leads to the region map");
        C(keys.ContainsKey("CC_Heard_ShowOnMap") && keys.ContainsKey("CC_Heard_Tip_Talk") && keys.ContainsKey("CC_Heard_Tip_Letter") && keys.ContainsKey("CC_Heard_Tip_LetterVia"),
            "the marks' words");
        C(keys["CC_Heard_Tip_LetterVia"].Contains("{TELLER}") && keys["CC_Heard_Tip_LetterVia"].Contains("{PAWN}") && keys["CC_Heard_Tip_LetterVia"].Contains("{VIA}")
          && keys["CC_Heard_Tip_LetterVia"].Contains("{WHEN}"), "the tooltip says who told the colony, through whom, and when");
        string tests = File.ReadAllText(Path.Combine(root, "Tools", "Tests", "Tests.csproj"));
        C(tests.Contains("World/LetterAddressMath.cs") && tests.Contains("World/HeardPlaceMath.cs"), "the pure rules are linked here");
        C(File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py")).Contains("\"World/Notables.cs:places\""), "the saved places are in the save's estimate");
        string dealing = Code("Dealings/DealingLetters.cs");
        C(dealing.Contains("ReplyPrefix = \"" + LetterAddressMath.ReplyPrefix + "\""), "a reply's prefix is the dealings' own");
        string owed = Code("Post/OwnRiders.cs");
        C(owed.Contains("CC_Route_Msg_RiderHome"), "the colony's rider home after passing a letter on says so");
        C(Code("World/NotableMath.cs").Contains("+ x.word;"), "the score takes the word");
    }
}
