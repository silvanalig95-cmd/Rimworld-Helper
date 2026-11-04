using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The interface's first release, names that lead somewhere (docs/design/interface.md, UI 1): things and their text form
/// (ThingRef), links in text (LinkTextMath: the markers read back, the layout of runs for a width, the names a line
/// mentions, no marker in anything filled plain, for every keyed text), the way back (NavTrail: back, forward, the bound
/// of 50, a step dropped when its thing is gone), the order of a card's slots and sections, the drawer's place at the
/// screens and scales the game offers (DrawerMath), and the mod's side: the place card took the region map panel's
/// fourteen sections, the old lists stay as a shim, the windows tell the way back where they are.
/// </summary>
public static class CardsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "cards: " + what);
        }

        // ---------------- Things ----------------
        var samples = new[]
        {
            ThingRef.From(Ref.Person(12)), ThingRef.From(Ref.LedgerPerson(4)), ThingRef.From(Ref.Town(55)), ThingRef.From(Ref.People(3)),
            ThingRef.From(Ref.Colony(9)), ThingRef.From(Ref.House(2)), ThingRef.From(Ref.Tile(801)), ThingRef.From(Ref.Tile(5, 2)), ThingRef.Site(40),
            ThingRef.Tie(7), ThingRef.Event(12), ThingRef.Letter(0), ThingRef.Letter(3)
        };
        foreach (var r in samples)
        {
            C(!r.IsNone, $"{r} names something");
            C(ThingRef.Parse(r.ToString()) == r, $"{r} reads back from its text form");
        }
        C(samples.Select(r => r.ToString()).Distinct().Count() == samples.Length, "every sample has its own text form");
        C(ThingRef.From(Ref.Town(55)).ToString() == "t55" && ThingRef.From(Ref.LedgerPerson(4)).ToString() == "p-4" && ThingRef.Site(40).ToString() == "s40"
          && ThingRef.Tie(7).ToString() == "y7" && ThingRef.Event(12).ToString() == "e12" && ThingRef.Letter(3).ToString() == "l3",
            "the ledger's kinds write as the ledger writes them; sites, ties, events and letters have letters of their own");
        foreach (var r in samples.Take(8)) C(ThingRef.From(r.ToRef()) == r && r.ToRef().ToString() == r.ToString(), $"{r} is the ledger's ref and back");
        C(ThingRef.Site(4).ToRef().IsNone && ThingRef.Tie(1).ToRef().IsNone, "a site or a tie is no ledger ref");
        foreach (var bad in new[] { null, "", "-", "x5", "s", "s-4", "y0", "e-1", "y1.5", "t 5", "e1e" })
            C(!ThingRef.TryParse(bad, out var none) && none.IsNone, $"\"{bad}\" names nothing");
        C(ThingRef.None.ToString() == "-" && ThingRef.From(Ref.None).IsNone, "none is none");
        C(ThingRef.From(Ref.Town(1)) != ThingRef.From(Ref.People(1)) && ThingRef.Site(1) != ThingRef.From(Ref.Town(1)), "the same number of two kinds is two things");
        var sorted = samples.OrderBy(r => r).ToList();
        C(sorted.SequenceEqual(samples.OrderBy(r => r.kind).ThenBy(r => r.id)), "things sort by kind, then number");
        C(ThingRef.From(Ref.Town(1)).Card == CardKind.Place && ThingRef.From(Ref.Colony(1)).Card == CardKind.Place && ThingRef.Site(1).Card == CardKind.Place
          && ThingRef.From(Ref.Tile(1)).Card == CardKind.Place, "towns, colonies, sites and tiles open the place card");
        C(ThingRef.From(Ref.Person(1)).CardOf(true) == CardKind.Colonist && ThingRef.From(Ref.Person(1)).CardOf(false) == CardKind.Person,
            "a person opens a colonist's card when they are the colony's own, else a person's");
        C(ThingRef.From(Ref.People(1)).Card == CardKind.People && ThingRef.From(Ref.House(1)).Card == CardKind.House && ThingRef.Tie(1).Card == CardKind.Tie
          && ThingRef.Event(1).Card == CardKind.Event && ThingRef.Letter(1).Card == CardKind.Letter, "each other kind has its card");
        C(Enum.GetValues(typeof(CardKind)).Length == 9, "eight kinds of card (and none)");

        // ---------------- Links: the markers ----------------
        string town = LinkTextMath.Wrap("Tallbrook", "t55");
        string edda = LinkTextMath.Wrap("Edda Marsh", "p12");
        string line = edda + " of " + town + " thanked the colony.";
        var runs = LinkTextMath.Parse(line);
        C(runs.Count == 4 && runs[0].IsLink && runs[0].text == "Edda Marsh" && runs[0].thing == "p12" && !runs[1].IsLink && runs[1].text == " of "
          && runs[2].thing == "t55" && runs[3].text == " thanked the colony.", "a line reads into its runs: links with their things, plain words between");
        C(LinkTextMath.Plain(line) == "Edda Marsh of Tallbrook thanked the colony.", "the plain text keeps the links' words");
        C(ReferenceEquals(LinkTextMath.Plain("no links here"), "no links here") || LinkTextMath.Plain("no links here") == "no links here", "plain text stays as it is");
        C(LinkTextMath.Wrap("", "t1") == "" && LinkTextMath.Wrap("Tallbrook", null) == "Tallbrook" && LinkTextMath.Wrap("Tallbrook", "") == "Tallbrook",
            "no words or no thing: no link");
        C(LinkTextMath.Parse(LinkTextMath.Wrap(town, "f3")).Count == 1 && LinkTextMath.Parse(LinkTextMath.Wrap(town, "f3"))[0].thing == "f3", "links never nest");
        foreach (var broken in new[] { "t55Tallbrook", "t55Tallbrook", "Tallbrook", "a  b", "x" })
        {
            var br = LinkTextMath.Parse(broken);
            C(br.All(r => !r.IsLink) && !LinkTextMath.AnyMarker(string.Concat(br.Select(r => r.text))), $"a broken marker reads as plain words ({broken.Length} chars)");
            C(!LinkTextMath.AnyMarker(LinkTextMath.Plain(broken)), "a broken marker leaves nothing in the plain text");
        }
        C(LinkTextMath.Parse(null).Count == 0 && LinkTextMath.Parse("").Count == 0, "no text, no runs");

        // ---------------- Links: the names a line mentions ----------------
        var names = new List<KeyValuePair<string, string>>
        {
            new("Tallbrook", "t55"), new("Edda", "p13"), new("Edda Marsh", "p12"), new("Mira", "p1"), new("A", "p99")
        };
        string marked = LinkTextMath.MarkNames("Edda Marsh of Tallbrook thanked Mira, and Mira again. Miranda stayed.", names);
        var mr = LinkTextMath.Parse(marked);
        C(mr.Count(r => r.IsLink) == 3 && mr.Any(r => r.thing == "p12" && r.text == "Edda Marsh") && !mr.Any(r => r.thing == "p13"),
            "the longest name first, never two links over the same words");
        C(mr.Count(r => r.thing == "p1") == 1, "a name is a link once, at its first mention");
        C(LinkTextMath.Plain(marked) == "Edda Marsh of Tallbrook thanked Mira, and Mira again. Miranda stayed.", "marking names changes no word");
        C(!LinkTextMath.Parse(LinkTextMath.MarkNames("Miranda stayed.", names)).Any(r => r.IsLink), "only whole words: Mira is not in Miranda");
        C(!LinkTextMath.HasMarks(LinkTextMath.MarkNames("A day at the gate.", names)), "a one-letter name is never marked");
        var inside = LinkTextMath.Parse(LinkTextMath.MarkNames(edda + " came, and Marsh stayed.", new List<KeyValuePair<string, string>> { new("Marsh", "h1") }));
        C(inside.Count(r => r.thing == "h1") == 1 && inside.First(r => r.thing == "h1").text == "Marsh" && inside.Count(r => r.thing == "p12") == 1,
            "never inside a link already there: the next mention outside it is marked");
        C(LinkTextMath.MarkNames("nothing", null) == "nothing" && LinkTextMath.MarkNames(null, names) == null, "no names, no change");

        // ---------------- Links: the layout ----------------
        float Mono(string s) => s.Length * 7f;
        var lay = LinkTextMath.Lay(runs, 1000f, Mono, 7f);
        C(lay.lines == 1 && lay.links.SequenceEqual(new[] { "p12", "t55" }) && lay.things.SequenceEqual(new[] { ThingRef.From(Ref.Person(12)), ThingRef.From(Ref.Town(55)) }),
            "wide enough: one line, the links in order, read back as things");
        C(lay.pieces.Count == 4 && lay.pieces[0].text == "Edda Marsh" && lay.pieces[0].x == 0f && lay.pieces[0].link == 0 && lay.pieces[1].text == "of"
          && lay.pieces[2].link == 1 && lay.pieces[3].text == "thanked the colony.", "a run's words on a line are one piece");
        C(Math.Abs(lay.pieces[1].x - (Mono("Edda Marsh") + 7f)) < 0.01f && Math.Abs(lay.pieces[2].x - (Mono("Edda Marsh of") + 7f)) < 0.01f, "pieces sit after the words and spaces before them");
        var narrow = LinkTextMath.Lay(runs, 7f * 16f, Mono, 7f);
        C(narrow.lines > 1 && narrow.pieces.All(p => p.x + p.w <= 7f * 16f + 0.01f || p.x == 0f), "wrapped: no piece runs past the width unless it starts a line");
        string flat = string.Join(" ", narrow.pieces.OrderBy(p => p.line).ThenBy(p => p.x).Select(p => p.text));
        C(flat.Replace(" .", ".") == "Edda Marsh of Tallbrook thanked the colony.", "wrapped: the words in their order, none lost");
        var glued = LinkTextMath.Lay(LinkTextMath.Parse(LinkTextMath.Wrap("Tallbrook", "t1") + ". Then"), 7f * 10f, Mono, 7f);
        var dot = glued.pieces.First(p => p.text.StartsWith("."));
        C(dot.line == glued.pieces.First(p => p.link == 0).line, "a word glued to a link (its full stop) stays on the link's line");
        var two = LinkTextMath.Lay(LinkTextMath.Parse("one\ntwo\n\nfour"), 1000f, Mono, 7f);
        C(two.lines == 4 && two.pieces.Select(p => p.line).SequenceEqual(new[] { 0, 1, 3 }), "line breaks start new lines, an empty line kept");
        var longWord = LinkTextMath.Lay(LinkTextMath.Parse("a verylongwordindeed b"), 7f * 8f, Mono, 7f);
        C(longWord.pieces.Single(p => p.text == "verylongwordindeed").x == 0f, "a word longer than the width stands alone on its line");
        C(LinkTextMath.Lay(new List<LinkTextMath.Run>(), 100f, Mono, 7f).pieces.Count == 0, "no runs, nothing laid out");
        // Every width from narrow to wide keeps every word, in order.
        foreach (int chars in new[] { 3, 6, 9, 14, 20, 31, 60 })
        {
            var l = LinkTextMath.Lay(LinkTextMath.Parse(marked), chars * 7f, Mono, 7f);
            string words = string.Join(" ", l.pieces.OrderBy(p => p.line).ThenBy(p => p.x).Select(p => p.text));
            C(Regex.Replace(words, @"\s+", " ").Replace(" ,", ",").Replace(" .", ".") == "Edda Marsh of Tallbrook thanked Mira, and Mira again. Miranda stayed.",
                $"at {chars} letters wide every word is there in order");
        }

        // ---------------- Links: the fill, plain for the game ----------------
        string keyedDir = Path.Combine(root, "Languages", "English", "Keyed");
        int texts = 0, holes = 0;
        foreach (var file in Directory.GetFiles(keyedDir, "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements())
            {
                string text = el.Value;
                texts++;
                C(!LinkTextMath.AnyMarker(text), $"{el.Name} has no marker of its own");
                var values = new Dictionary<string, string>();
                var things = new Dictionary<string, string>();
                foreach (var h in Texts.Holes(text))
                {
                    values[h] = "Name " + h;
                    things[h] = "t" + (h.Length + 1);
                    holes++;
                }
                string plain = Texts.Fill(text, values);
                C(!LinkTextMath.AnyMarker(plain) && Texts.Fill(text, values, things, false) == plain, $"{el.Name}, filled for the game, has no marker");
                if (values.Count == 0) continue;
                string linked = Texts.Fill(text, values, things, true);
                C(LinkTextMath.Plain(linked) == plain, $"{el.Name}, filled with links, reads as the plain fill");
            }
        C(texts > 5000 && holes > 1000, $"every keyed text was filled both ways ({texts} texts, {holes} placeholders)");
        var fs = new FactSet();
        fs.Value("TOWN", "Tallbrook");
        fs.Thing("TOWN", "t55");
        fs.Thing("NOVALUE", "t1");
        C(fs.things.Count == 1 && LinkTextMath.Parse(Texts.Fill("News from {TOWN}.", fs.values, fs.things, true))[1].thing == "t55",
            "a fact names its thing; a linked fill makes it a link");
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        C(Code("UI/Tell.cs").Contains("LinkTextMath.Plain(text)") && Code("Immersion/Speech.cs").Contains("line = LinkTextMath.Plain(line);"),
            "messages, letters and bubbles take any stray marker out at the door to the game");

        // ---------------- The way back ----------------
        var t = new NavTrail();
        NavStep S(string page, ThingRef card = default) => new NavStep(RimfolkWindow.World, page, null, null, card);
        C(t.Push(S("Map")) && !t.Push(S("Map")) && t.Count == 1, "the same place twice is one step");
        t.Push(S("Peoples"));
        t.Push(S("Map", ThingRef.From(Ref.Town(5))));
        C(t.Count == 3 && t.Current.card == ThingRef.From(Ref.Town(5)), "a card opened is a step");
        C(t.Back(null, out var b1) && b1.page == "Peoples" && t.Back(null, out var b2) && b2.page == "Map" && b2.card.IsNone, "back, and back again");
        C(!t.CanBack(null) && !t.Back(null, out _), "nothing before the first step");
        C(t.Forward(null, out var f1) && f1.page == "Peoples" && t.CanForward(null), "forward");
        t.Push(S("Letters"));
        C(!t.CanForward(null) && t.Count == 3 && t.Current.page == "Letters", "going somewhere new after going back drops the steps ahead");
        for (int i = 0; i < 80; i++) t.Push(S("Page" + i));
        C(t.Count == NavTrail.Max && NavTrail.Max == 50 && t.Current.page == "Page79" && t.Steps[0].page == "Page30", "at most fifty steps, the oldest going first");
        var gone = ThingRef.From(Ref.Town(66));
        var t2 = new NavTrail();
        t2.Push(S("Map"));
        t2.Push(S("Map", gone));
        t2.Push(S("Peoples"));
        bool Alive(NavStep s) => s.card != gone;
        C(t2.Back(Alive, out var g1) && g1.page == "Map" && g1.card.IsNone && t2.Count == 2, "a step whose thing is gone is dropped on the way back");
        C(t2.Forward(Alive, out var g2) && g2.page == "Peoples", "and forward still leads on");
        var t3 = new NavTrail();
        t3.Push(S("Map", gone));
        t3.Push(S("Peoples"));
        C(!t3.CanBack(Alive) && !t3.Back(Alive, out _) && t3.Count == 2, "no step back when the only one behind is gone (nothing changed)");
        t3.Prune(Alive);
        C(t3.Count == 1 && t3.Current.page == "Peoples", "pruning drops gone steps and keeps where it stands");
        var t4 = new NavTrail();
        t4.Push(S("Map"));
        t4.Push(S("Peoples"));
        t4.Back(null, out _);
        t4.Replace(S("Peoples"));
        C(t4.Count == 1 && t4.Current.page == "Peoples", "a settled place the same as its neighbour is one step");
        C(new NavStep(RimfolkWindow.World, "WorldHistory", null, "f3||").Same(RimfolkWindow.World, "WorldHistory", "", "f3||", ThingRef.None)
          && !new NavStep(RimfolkWindow.World, "WorldHistory", null, "f3||").Same(RimfolkWindow.World, "WorldHistory", null, "f4||", ThingRef.None),
            "filters that matter tell two steps apart; an empty mode is none");
        // Every page a step can name is an address Pages.Open takes.
        foreach (var p in PagesMath.All) C(PagesMath.Resolve(p.name) == p.name, $"a step on {p.name} leads back there");

        // ---------------- Cards: slots and the order of sections ----------------
        C(Enum.GetValues(typeof(CardSlot)).Cast<CardSlot>().SequenceEqual(new[] { CardSlot.Head, CardSlot.Now, CardSlot.Who, CardSlot.Ties, CardSlot.Words, CardSlot.Story, CardSlot.Acts }),
            "every card's slots in one order: head, now, who, ties, words, story, acts");
        var reg = new CardRegistry<string>();
        reg.Add(CardKind.Place, CardSlot.Words, 10f, "heard", "heard");
        reg.Add(CardKind.Place, CardSlot.Now, 30f, "post", "post");
        reg.Add(CardKind.Place, CardSlot.Now, 20f, "fortune", "fortune");
        reg.Add(CardKind.Place, CardSlot.Now, 20f, "ruin", "ruin");
        reg.Add(CardKind.Person, CardSlot.Now, 0f, "letter", "letter");
        reg.Add(CardKind.Place, CardSlot.Story, 10f, "founding", "founding");
        C(reg.In(CardKind.Place, CardSlot.Now).Select(e => e.value).SequenceEqual(new[] { "fortune", "ruin", "post" }), "in a slot by order, then in the order added");
        C(reg.Of(CardKind.Place).Select(e => e.value).SequenceEqual(new[] { "fortune", "ruin", "post", "heard", "founding" }), "a card's sections slot by slot");
        reg.Add(CardKind.Place, CardSlot.Now, 25f, "fortune", "fortune2");
        C(reg.In(CardKind.Place, CardSlot.Now).Select(e => e.value).SequenceEqual(new[] { "ruin", "fortune2", "post" }) && reg.Count == 6, "added again by its id, a section replaces the first");
        C(reg.Add(CardKind.None, CardSlot.Now, 0f, "x", "x") == null && reg.In(CardKind.House, CardSlot.Now).Count == 0, "no kind, no section; a kind with none has none");

        // The place card took the region map panel's sections (the fourteen of 0.80.0), each into a slot.
        var placeSections = new (string file, string pattern)[]
        {
            ("World/NeighboursPanel.cs", @"CardSections\.Town\(CardSlot\.Who, 10f, DrawTown"),
            ("Outsiders/OutsiderHeard.cs", @"CardSections\.TownLines\(CardSlot\.Words, 10f, CardLines"),
            ("World/Region/RegionPanel.cs", @"CardSections\.TownLines\(CardSlot\.Now, 20f, .*FortuneLines"),
            ("World/Region/RegionPanel.cs", @"CardSections\.TownLines\(CardSlot\.Story, 10f, .*FoundingLines"),
            ("World/Region/RegionPanel.cs", @"CardSections\.PlaceLines\(CardSlot\.Now, 30f, .*SiteLines"),
            ("World/Region/RegionPanel.cs", @"CardSections\.HomeLines\(CardSlot\.Now, 40f, .*HomeLines"),
            ("World/Region/RegionPanel.cs", @"CardSections\.HomeLines\(CardSlot\.Words, 30f, .*FarNewsLines"),
            ("World/Region/HamletWords.cs", @"CardSections\.TownLines\(CardSlot\.Now, 30f, PanelLines"),
            ("World/Region/TradingPostWords.cs", @"CardSections\.TownLines\(CardSlot\.Now, 30f, PanelLines"),
            ("World/Region/RiseFallWords.cs", @"CardSections\.PlaceLines\(CardSlot\.Now, 20f, RuinPanel"),
            ("World/Region/RiseFallWords.cs", @"CardSections\.TownLines\(CardSlot\.Now, 10f, ClosingPanel"),
            ("World/Region/RaidersCampPage.cs", @"CardSections\.PlaceLines\(CardSlot\.Now, 10f, SitePanel"),
            ("World/Region/RaidersCampPage.cs", @"CardSections\.HomeLines\(CardSlot\.Now, 10f, HomePanel"),
            ("UI/JourneyDialog.cs", @"CardActs\.Register\(CardKind\.Place,")
        };
        foreach (var (file, pattern) in placeSections) C(Regex.IsMatch(Code(file), pattern), $"{file}: its panel section is the place card's ({pattern.Split('(')[0].Replace("\\", "")})");
        int oldAdds = 0;
        foreach (var file in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
            oldAdds += Regex.Matches(File.ReadAllText(file), @"NeighbourPanel\.(Sections|PlaceSections|HomeSections)\.Add\(").Count;
        C(oldAdds == 0, $"none of Rimfolk's own sections is added the old way any more ({oldAdds})");
        string shim = Code("World/NeighbourPanel.cs");
        C(shim.Contains("public static readonly List<Func<Rect, Settlement, float>> Sections") && shim.Contains("PlaceSections") && shim.Contains("HomeSections"),
            "the old lists stay, as a shim for a release");
        string view = Code("UI/Cards/CardView.cs");
        C(view.Contains("NeighbourPanel.Sections[i]") && view.Contains("NeighbourPanel.PlaceSections[i]") && view.Contains("NeighbourPanel.HomeSections[i]"),
            "what is still added the old way draws on the place card");
        C(Code("Diplomacy/DiplomacyLinks.cs").Contains("Cards.Open("), "diplomacy's links open cards");

        // ---------------- The windows ----------------
        foreach (var cls in new[] { "MainTabWindow_Council", "MainTabWindow_World", "MainTabWindow_Defenses", "MainTabWindow_Planning" })
            C(Code("UI/" + cls + ".cs").Contains("Nav.Observe("), $"{cls} tells the way back where it is");
        C(Code("UI/WindowTabs.cs").Contains("Nav.Arrows(") && Code("UI/WindowTabs.cs").Contains("Drawer.Cancel(Which)"), "back and forward in every corner; Escape closes the drawer first");
        C(Code("UI/MainTabWindow_World.cs").Contains("Drawer.Around(RimfolkWindow.World"), "the region window has the drawer, the map's panel");
        var keys = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_NavKeys.xml")).Root!.Elements("KeyBindingDef").Select(e => e.Element("defName")!.Value).ToList();
        C(keys.SequenceEqual(new[] { "Council_Key_Back", "Council_Key_Forward" }) && Code("UI/Cards/Nav.cs").Contains("\"Council_Key_Back\""), "two keys for back and forward");
        C(Code("UI/Cards/Nav.cs").Contains("Input.GetMouseButtonDown(3)") && Code("UI/Cards/Nav.cs").Contains("Input.GetMouseButtonDown(4)"), "the mouse's side buttons");
        string settings = Code("CouncilSettings.cs");
        string catalog = Code("UI/SettingsCatalog.cs");
        C(settings.Contains("public bool linkPreviews = true;") && settings.Contains("public float linkPreviewDelay = 0.35f;")
          && catalog.Contains("Toggle(\"linkPreviews\"") && catalog.Contains("Slider(\"linkPreviewDelay\""), "previews on at first, after 0.35 seconds, both in the settings");
        foreach (var show in new[] { "UI/Region/MapView.cs", "UI/Region/HereAndAwayView.cs", "UI/WeekModel.cs" })
            C(Code(show).Contains("Cards.ShowOnMap("), $"{show}: Show opens the place on the region map");

        // ---------------- The drawer's place ----------------
        DrawerMath.Split(0f, 950f, out float vx, out float vw, out float dx, out float dw, out bool over);
        C(!over && dw == 380f && Math.Abs(vw - 558f) < 0.01f && Math.Abs(dx - 570f) < 0.01f, "at 1080p and scale 1: the map 558 wide beside a 380 drawer");
        foreach (var (w, h) in new[] { (1280, 720), (1366, 768), (1600, 900), (1920, 1080), (2560, 1440), (3840, 2160) })
            foreach (float scale in new[] { 1f, 1.25f, 1.5f, 2f, 3f })
            {
                DrawerMath.WindowSize(w / scale, h / scale, out float ww, out float wh);
                float content = DrawerMath.ContentWidth(ww);
                if (content < 300f) continue;
                DrawerMath.Split(0f, content, out _, out float vw2, out float dx2, out float dw2, out bool over2);
                C(dx2 >= 0f && dx2 + dw2 <= content + 0.01f && dw2 >= Math.Min(DrawerMath.Narrowest, content) - 0.01f && dw2 <= DrawerMath.Width,
                    $"{w}x{h} at {scale}: the drawer fits the window ({dw2:0} of {content:0})");
                C(over2 == content < DrawerMath.BesideFrom, $"{w}x{h} at {scale}: beside from 900, over it below");
                if (!over2) C(vw2 + DrawerMath.Gap + dw2 <= content + 0.01f && vw2 >= 500f, $"{w}x{h} at {scale}: the view keeps room beside it ({vw2:0})");
            }
        DrawerMath.WindowSize(1920f / 1.5f, 1080f / 1.5f, out float w15, out _);
        C(DrawerMath.Beside(DrawerMath.ContentWidth(w15)), "at 1080p and scale 1.5 the drawer still sits beside the view");
        return n;
    }
}
