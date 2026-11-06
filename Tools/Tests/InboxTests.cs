using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The interface's third release, one inbox and search (docs/design/interface.md, UI 3): This week's sections and their
/// order, the one count (the sum of what Needs you's providers count, equal to 0.83's NeedsCount for the same state),
/// the order of a section's rows (provider by provider, each as This week always sorted it), the steps of what is under
/// way (never a time left), a day coming as a word or a date, following (its bound of twelve, the toggle, a saved list
/// cleaned, the colonists pinned before), the search (matching, grouping, Enter's first), and the mod's side: the
/// providers, the corner, the council button, the card's pin, the Following panel and the save model.
/// </summary>
public static class InboxTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "inbox: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));

        // ---------------- The sections ----------------
        C(InboxMath.Sections.SequenceEqual(new[] { InboxSection.Following, InboxSection.Needs, InboxSection.UnderWay, InboxSection.Coming, InboxSection.People, InboxSection.Plan }),
            "This week's sections in the design's order: following, needs you, under way, coming, how people are, the plan");
        C(InboxMath.Sections.Distinct().Count() == Enum.GetValues(typeof(InboxSection)).Length, "every section drawn once");
        C(InboxMath.Cap(InboxSection.Plan) == 2 && InboxMath.Cap(InboxSection.Needs) == InboxMath.MaxRows, "the plan shows the advisor's two most pressing steps; the rest six");
        foreach (var s in InboxMath.Sections)
            for (int rows = 0; rows <= 30; rows++)
            {
                int folded = InboxMath.Shown(s, rows, false), opened = InboxMath.Shown(s, rows, true);
                C(opened == rows, $"{s}, {rows} rows: opened in place, all show");
                C(folded <= rows && (rows == 0 || folded >= 1) && folded <= InboxMath.Cap(s) + 1, $"{s}, {rows} rows: folded, at most the cap (a single extra row shows itself)");
                C(rows <= InboxMath.Cap(s) + 1 ? folded == rows : folded == InboxMath.Cap(s), $"{s}, {rows} rows: \"and N more\" only for two or more");
            }

        // ---------------- The one count ----------------
        // 0.83's NeedsCount: every colony's petitions, the open wishes, Rimfolk's letters waiting, the promises at risk.
        // Needs you's providers count those same four, and the region's and diplomacy's asks besides.
        var rnd = new Random(31);
        for (int t = 0; t < 300; t++)
        {
            int petitions = rnd.Next(0, 4), wishes = rnd.Next(0, 9), letters = rnd.Next(0, 5), promises = rnd.Next(0, 3);
            int before = petitions + wishes + letters + promises;
            // As registered: wishes and petitions together, the letters, the asks, the favours, the settlers, the promises.
            var noAsks = new[] { petitions + wishes, letters, 0, 0, 0, promises };
            C(InboxMath.Count(noAsks) == before && InboxMath.Before(petitions, wishes, letters, promises) == before,
                $"with no new asks the count is today's NeedsCount ({before})");
            int asks = rnd.Next(1, 4), favours = rnd.Next(0, 3), settlers = rnd.Next(0, 2);
            C(InboxMath.Count(new[] { petitions + wishes, letters, asks, favours, settlers, promises }) == before + asks + favours + settlers,
                "the region's and diplomacy's asks add to it");
        }
        C(InboxMath.Count(new[] { 3, -2, 0 }) == 3 && InboxMath.Count(null) == 0, "a part's count below nothing counts as none");

        // ---------------- One list: the council panel and the badges read Needs you (decision 16) ----------------
        C(InboxMath.PanelParts.SequenceEqual(new[] { "wishes", "petitions", "promises", "questions", "asks" }), "the panel's kinds of what waits, in its order");
        C(InboxMath.PanelPart("petitions", "wishes") == "petitions" && InboxMath.PanelPart(null, "questions") == "questions" && InboxMath.PanelPart(null, null) == "asks"
          && InboxMath.PanelPart("", "") == "asks", "a row shows under its own kind, else its provider's, else the other asks");
        C(InboxMath.Rank(0f) == InboxMath.WaitingRank && InboxMath.Rank(270f) == 270f && InboxMath.WaitingRank >= 100f && InboxMath.WaitingRank < 200f,
            "a row with no rank of its own is worth knowing, on the panels' middle tier");
        C(InboxMath.MostPressing(new float[] { 0f, 230f, 270f, 270f }) == 2 && InboxMath.MostPressing(new float[] { 0f, 0f }) == 0 && InboxMath.MostPressing(new float[0]) == -1,
            "the most pressing row: the highest rank, the first among equals");
        C(InboxMath.PanelScore(new float[] { 110f, 0f }) == InboxMath.WaitingRank && InboxMath.PanelScore(new float[0]) == 0f, "a line's score is its most pressing row's");
        // The counts agree: each provider counts the rows it lists that wait (the news letters' row lists and asks nothing),
        // so the panel's lines, the badges' rows and the one count on the button and This week are the same things.
        var rndPanel = new Random(16);
        string[] providerParts = { "wishes", "questions", null, null, null, "promises" };
        for (int t = 0; t < 300; t++)
        {
            var counts = new List<int>();
            var byPart = new Dictionary<string, int>();
            int waitingRows = 0;
            foreach (var pp in providerParts)
            {
                int n = rndPanel.Next(0, 5), waits = 0;
                for (int i = 0; i < n; i++)
                {
                    bool news = pp == "questions" && i == n - 1 && rndPanel.Next(2) == 0;
                    if (news) continue;
                    string rowPart = pp == "wishes" && rndPanel.Next(3) == 0 ? "petitions" : null;
                    string part = InboxMath.PanelPart(rowPart, pp);
                    byPart[part] = byPart.GetValueOrDefault(part) + 1;
                    waits++;
                }
                counts.Add(waits);
                waitingRows += waits;
            }
            C(InboxMath.Count(counts) == waitingRows && byPart.Values.Sum() == waitingRows && byPart.Keys.All(InboxMath.PanelParts.Contains),
                "the panel's lines add up to the one count");
        }
        string hud = Code("Hud/HudCouncil.cs"), badges = Code("ColonistBar/BarBadges.cs");
        C(hud.Contains("var waiting = Inbox.Waiting;") && hud.Contains("InboxMath.PanelPart(r.part, p.provider.part)") && !hud.Contains("LetterStack") && !hud.Contains("comp.wishes")
          && !hud.Contains("kept?.watches") && !hud.Contains("decrees).requests"),
            "the council panel reads what waits from Needs you, never the letters, wishes, petitions or promises itself");
        C(Regex.Matches(badges, @"Inbox\.Waiting\.Of\(p\)").Count == 2 && !badges.Contains("comp.wishes") && badges.Contains("comp.PetitionOf(w).FirstOrDefault(x => x.pawn == p)") && !badges.Contains("kept.watches"),
            "the bar's wish and promise badges read the colonist's rows of Needs you");
        C(Code("Hud/HudAlerts.cs").Contains("HudCouncil.PartWishes") && Code("Hud/HudCouncil.cs").Contains("PartAsks"), "the game's alerts stay the game's; the panel's wishes still hide its wishes alert");
        string own = Code("UI/InboxProviders.cs");
        C(own.Contains("WishesCount, part: InboxMath.PartWishes") && own.Contains("AnswersCount, part: InboxMath.PartQuestions") && own.Contains("PromisesCount, part: InboxMath.PartPromises"),
            "the own providers say the panel's kind of their rows");
        C(own.Contains("about = w,") && own.Contains("about = r,") && own.Contains("about = l,") && own.Contains("part = InboxMath.PartPetitions") && own.Contains("waits = false"),
            "rows say what they are about (a wish, a petition, a letter, a promise), petitions their own kind, the news row waits on nothing");
        C(Regex.IsMatch(own, @"private static bool AtRisk\(GrantWatch w, int now\)[\s\S]*?KeptMath\.AmendsDays") && Regex.Matches(own, @"AtRisk\(w, now\)").Count == 2,
            "a promise taken back past making amends no longer waits, in the count and the rows alike");
        C(Regex.IsMatch(own, @"r\?\.def != null && r\.pawn != null\)\s*petitions\+\+") && own.Contains("w.state == WishState.Open && w.pawn != null && comp.LeadsPetition(w)") && own.Contains("others = others,"),
            "wishes and petitions counted as their rows list them, a petition of several once, its other names reading the row too");
        C(Code("UI/Inbox.cs").Contains("public static InboxWaiting Waiting") && Code("UI/Inbox.cs").Contains("now - waitingAt < 1f"), "what waits read at most once a second");
        string providers = Code("UI/InboxProviders.cs");
        string model = Code("UI/WeekModel.cs");
        string inbox = Code("UI/Inbox.cs");
        foreach (var (id, count) in new[] { ("wishes", "WishesCount"), ("answers", "AnswersCount"), ("asks", "AsksCount"), ("favours", "DiplomacyPanels.WeekCount"),
                     ("settlers", "HamletPage.WeekCount"), ("promises", "PromisesCount") })
            C(Regex.IsMatch(providers, @"Inbox\.Register\(""" + id + @""", InboxSection\.Needs, [\d.]+f, [\w.]+, " + Regex.Escape(count) + @"\)"), $"Needs you's {id} has its count ({count})");
        C(Regex.IsMatch(providers, @"WishesCount\(\)[\s\S]*?decrees\.requests\.Count[\s\S]*?WishState\.Open") && Regex.IsMatch(providers, @"AnswersCount\(\)[\s\S]*?WaitingKind\(l\) != null")
          && Regex.IsMatch(providers, @"PromisesCount\(\)[\s\S]*?KeptPhase\.Kept"), "today's four parts count what NeedsCount counted");
        C(model.Contains("public static int NeedsCount(CouncilGameComponent comp) => comp == null ? 0 : Inbox.Count;") && model.Contains("m.needs = Inbox.Count;"),
            "the tab, This week's stat and the old NeedsCount are the one count");
        // Needs you's own rows hold the news letters' row, which asks nothing: its heading says the one count, not its rows.
        C(Regex.IsMatch(model, @"m\.needs = Inbox\.Count;[\s\S]*?c\.section == InboxSection\.Needs\)\s*c\.count = m\.needs;"),
            "Needs you's heading says the one count, as the stat above it and the tab do");
        C(inbox.Contains("now - countAt < 0.5f") && Regex.IsMatch(inbox, @"foreach \(var e in In\(InboxSection\.Needs\)\)"), "counted from Needs you's providers, at most twice a second");
        C(Code("UI/WindowTabs.cs").Contains("Inbox.Count") && Code("UI/MainTabWindow_Council.cs").Contains("WeekModel.NeedsCount(comp)") && Code("UI/CouncilButton.cs").Contains("Inbox.Count"),
            "the same count in the corner, on This week's tab and on the council button");

        // ---------------- Rows sorted as This week sorts ----------------
        for (int t = 0; t < 200; t++)
        {
            int count = rnd.Next(0, 25);
            var prov = new List<float>();
            var row = new List<float>();
            for (int i = 0; i < count; i++)
            {
                prov.Add(rnd.Next(0, 4) * 10f);
                row.Add(rnd.Next(-3, 4));
            }
            var order = InboxMath.Order(prov, row);
            // This week sorted each card's rows by their order, equals as found (LINQ's stable OrderBy); the cards are the providers now.
            var expected = Enumerable.Range(0, count).GroupBy(i => prov[i]).OrderBy(g => g.Key).SelectMany(g => g.OrderBy(i => row[i])).ToArray();
            C(order.SequenceEqual(expected), $"rows in drawing order: provider by provider, each by its own order, equals as found ({count} rows)");
        }
        C(Regex.IsMatch(model, @"into\.AddRange\(rows\.OrderBy\(r => r\.order\)\)") && model.Contains("foreach (var e in Inbox.In(section))"),
            "the model sorts each provider's rows as each card did, provider by provider");
        var reg = new InboxRegistry<string>();
        reg.Add(InboxSection.Needs, 20f, "answers", "answers");
        reg.Add(InboxSection.Needs, 10f, "wishes", "wishes");
        reg.Add(InboxSection.Coming, 10f, "calendar", "calendar");
        reg.Add(InboxSection.Needs, 20f, "asks", "asks");
        C(reg.In(InboxSection.Needs).Select(e => e.value).SequenceEqual(new[] { "wishes", "answers", "asks" }), "providers by order, then in the order added");
        reg.Add(InboxSection.Needs, 20f, "answers", "answers2");
        C(reg.In(InboxSection.Needs).Select(e => e.value).SequenceEqual(new[] { "wishes", "answers2", "asks" }) && reg.Count == 4, "added again by its id, a provider replaces the first");
        C(reg.In(InboxSection.Plan).Count == 0, "a section with no provider has none");

        // ---------------- Steps: never a time left ----------------
        var keyed = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var el in XDocument.Load(f).Root!.Elements())
                keyed[el.Name.LocalName] = el.Value;
        var stepKeys = keyed.Keys.Where(k => k.StartsWith("CC_Inbox_Step_") || k.StartsWith("CC_Card_Step_")).ToList();
        C(stepKeys.Count >= 20, $"found the steps' words ({stepKeys.Count})");
        foreach (var k in stepKeys) C(!InboxMath.ShowsTimeLeft(keyed[k]), $"{k} (\"{keyed[k]}\") says where it is, never how long is left");
        C(InboxMath.ShowsTimeLeft("2 days left") && InboxMath.ShowsTimeLeft("in {0}") && InboxMath.ShowsTimeLeft("soon") && !InboxMath.ShowsTimeLeft("on the road"),
            "a number, a placeholder or a word of time is a time left");
        string underWay = Code("UI/InboxUnderWay.cs");
        foreach (var bad in new[] { "UiKit.Duration(", "\"CC_Week_Left\"", "\"CC_Week_In\"", "\"CC_Week_Within\"", "\"CC_Week_ToAccept\"", "DaysString(" })
            C(!underWay.Contains(bad), $"Under way's rows never count down ({bad})");
        var stepUses = Regex.Matches(underWay, @"Words\(([^)]*)\)").Select(m => m.Groups[1].Value).ToList();
        C(stepUses.Count >= 6, "each kind under way has its steps");
        foreach (Match m in Regex.Matches(underWay, @"""(CC_Inbox_Step_\w+)""")) C(keyed.ContainsKey(m.Groups[1].Value), $"{m.Groups[1].Value} is a key");
        for (int stage = -1; stage <= 4; stage++)
        {
            int j = InboxMath.JourneyStep(stage);
            C(j >= 0 && j < InboxMath.JourneySteps, $"a journey's stage {stage} is one of its steps");
            int ms = InboxMath.MissionStep(stage);
            C(ms >= 0 && ms < InboxMath.MissionSteps, $"a mission's stage {stage} is one of its steps");
        }
        foreach (bool moving in new[] { false, true })
        foreach (bool home in new[] { false, true })
        foreach (bool at in new[] { false, true })
        {
            int s = InboxMath.CaravanStep(moving, home, at);
            C(s >= 1 && s < InboxMath.CaravanSteps, $"a caravan out (moving {moving}, home {home}, at a place {at}) is on its steps past setting out");
        }
        C(InboxMath.CaravanStep(true, true, false) == 3 && InboxMath.CaravanStep(false, false, true) == 2 && InboxMath.CaravanStep(true, false, false) == 1,
            "home-bound is the last step, resting at a place is there, moving elsewhere is on the road");
        C(InboxMath.SettlerStep(HamletStage.Waiting) == 0 && InboxMath.SettlerStep(HamletStage.Escorted) == 1 && InboxMath.SettlerStep(HamletStage.Alone) == 1
          && InboxMath.SettlerStep(HamletStage.Standing) == -1 && InboxMath.SettlerStep(HamletStage.Asked) == -1,
            "settlers: at the gate (Needs you), on the road (under way), and not under way before or after");

        // ---------------- Coming: by date ----------------
        C(InboxMath.When(10, 10) == DueWord.Today && InboxMath.When(10, 9) == DueWord.Today && InboxMath.When(10, 11) == DueWord.Tomorrow && InboxMath.When(10, 15) == DueWord.Date,
            "a day coming is today, tomorrow or its date: never \"in 3 days\"");
        string coming = Regex.Match(providers, @"// ---------------- Coming:[\s\S]*?// ---------------- How people are").Value;
        C(coming.Length > 0 && !coming.Contains("UiKit.Duration(") && !coming.Contains("\"CC_Week_In\"") && !coming.Contains("\"CC_Week_ToAccept\""), "Coming says the day, not how long until it");

        // ---------------- Following ----------------
        var list = new List<string>();
        var things = Enumerable.Range(1, 13).Select(i => ThingRef.From(Ref.Town(i))).ToList();
        for (int i = 0; i < FollowMath.Max; i++) C(FollowMath.Toggle(list, things[i]) == FollowChange.Followed, $"thing {i + 1} followed");
        C(list.Count == 12 && FollowMath.Max == 12, "twelve followed at most");
        C(FollowMath.Toggle(list, things[12]) == FollowChange.Full && list.Count == 12 && !FollowMath.Has(list, things[12]), "a thirteenth is refused and nothing changes");
        C(FollowMath.Toggle(list, things[3]) == FollowChange.Unfollowed && list.Count == 11 && !FollowMath.Has(list, things[3]), "the pin again stops following");
        C(FollowMath.Toggle(list, things[12]) == FollowChange.Followed && list[^1] == things[12].ToString(), "with room again, the new one comes last");
        C(FollowMath.Toggle(list, ThingRef.None) == FollowChange.None && FollowMath.Toggle(null, things[0]) == FollowChange.None, "nothing follows nothing");
        var mixed = new[] { ThingRef.From(Ref.Person(5)), ThingRef.From(Ref.LedgerPerson(2)), ThingRef.From(Ref.People(3)), ThingRef.From(Ref.House(4)), ThingRef.Tie(9),
            ThingRef.Site(40), ThingRef.From(Ref.Colony(1)) };
        var saved = new List<string>();
        foreach (var r in mixed) FollowMath.Toggle(saved, r);
        // Saved and loaded: the same text forms come back as the same things.
        var loaded = new List<string>(saved);
        C(FollowMath.Clean(loaded) == 0 && loaded.Select(ThingRef.Parse).SequenceEqual(mixed), "any thing a card shows is kept with the game as its text form and read back");
        var messy = new List<string> { "t1", "x9", "t1", "", "p5", "y0" };
        messy.AddRange(Enumerable.Range(20, 15).Select(i => "t" + i));
        int dropped = FollowMath.Clean(messy);
        C(messy.Count == 12 && messy[0] == "t1" && messy[1] == "p5" && dropped == 21 - 12, $"a saved list made sound: no unreadable, no twice, at most twelve ({messy.Count}, {dropped} dropped)");
        var gone = new List<string> { "t1", "t2", "t3" };
        FollowMath.Clean(gone, r => r.id != 2);
        C(gone.SequenceEqual(new[] { "t1", "t3" }), "what is gone can be let go");
        C(FollowMath.FromThingId("Human1234") == ThingRef.From(Ref.Person(1234)) && FollowMath.FromThingId("Human") .IsNone && FollowMath.FromThingId("1234").IsNone
          && FollowMath.FromThingId(null).IsNone,
            "a colonist pinned before (their ThingID) is the person behind it");
        var follows = new List<string> { "t1" };
        C(FollowMath.TakeOldPins(follows, new List<string> { "Human7", "Human7", "bad", "Human9" }) == 2 && follows.SequenceEqual(new[] { "t1", "p7", "p9" }),
            "the colonists pinned before are followed, once each");
        string memory = Code("UI/WeekMemory.cs");
        C(memory.Contains("Scribe_Collections.Look(ref following, \"following\", LookMode.Value);") && memory.Contains("FollowMath.TakeOldPins(following, pinnedPawns)")
          && Regex.IsMatch(memory, @"LoadSaveMode\.LoadingVars\) Scribe_Collections\.Look\(ref pinnedPawns"), "following is saved; an older save's pins are read once and followed");
        string estimate = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(estimate.Contains("\"UI/WeekMemory.cs:following\""), "following has a model in the save estimate");
        C(Code("UI/Cards/CardView.cs").Contains("memory.ToggleFollow(m.thing)"), "every card's head has the pin");
        C(Code("Hud/HudMath.cs").Contains("public const string Following = \"following\";") && HudIds.All.Contains(HudIds.Following)
          && Regex.IsMatch(Code("Hud/HudLayout.cs"), @"case Following:[\s\S]*?st\.on = false;"), "the Following panel stands beside the others, off at first");
        C(Code("UI/InboxProviders.cs").Contains("Inbox.Register(\"following\", InboxSection.Following, 10f, FollowWords.Rows);"), "what is followed is at the top of This week");

        // ---------------- Search ----------------
        C(SearchMath.Fold("  Édda   Marsh's  ") == "edda marshs" && SearchMath.Fold("Map: Trade") == "map trade" && SearchMath.Fold(null) == "", "names fold: any case, no accents or apostrophes, marks as spaces");
        C(SearchMath.Fold(LinkTextMath.Wrap("Tallbrook", "t55")) == "tallbrook", "a link's markers are left out");
        C(SearchMath.Score("bonds", "bonds") == SearchMath.Exact && SearchMath.Score("bon", "bonds") == SearchMath.Start && SearchMath.Score("marsh", "edda marsh") == SearchMath.WordStart
          && SearchMath.Score("ed ma", "edda marsh") == SearchMath.AllWords && SearchMath.Score("arsh", "edda marsh") == SearchMath.Inside
          && SearchMath.Score("ar", "edda marsh") == 0 && SearchMath.Score("ashfall", "tallbrook", "ashfall clans") == SearchMath.AlsoWord,
            "a match scores by how it matches: whole, start, a word's start, every word, inside, other words");
        C(SearchMath.Score("", "x") == 0 && SearchMath.Score("x", "") == 0, "nothing matches nothing");
        var items = new List<SearchItem>
        {
            new SearchItem(SearchKind.Colonist, "Bondsworth", "p1"), new SearchItem(SearchKind.View, "Bonds", "Bonds", "People"),
            new SearchItem(SearchKind.Article, "Bonds between colonists", "Council_Alm_C_Social"), new SearchItem(SearchKind.Place, "Tallbrook", "t55", null, "Ashfall Clans"),
            new SearchItem(SearchKind.People, "The Ashfall Clans", "f3"), new SearchItem(SearchKind.Person, "Édda Marsh", "p-4", "head of Tallbrook", "Tallbrook")
        };
        for (int i = 0; i < 10; i++) items.Add(new SearchItem(SearchKind.Colonist, "Bon " + i, "p" + (100 + i)));
        SearchMath.Prepare(items);
        var found = SearchMath.Find("Bonds", items);
        C(found.Count == 3 && found[0].kind == SearchKind.View && SearchMath.First(found).key == "Bonds", "\"Bonds\" finds the view first, Enter takes it");
        C(found.Select(g => g.kind).SequenceEqual(new[] { SearchKind.View, SearchKind.Colonist, SearchKind.Article }), "grouped by kind, the best group first");
        var bon = SearchMath.Find("bon", items);
        var colonists = bon.First(g => g.kind == SearchKind.Colonist);
        C(colonists.items.Count == SearchMath.PerGroup && colonists.total == 11, "a few of each kind show, and how many matched");
        C(colonists.items[0].name.Length <= colonists.items[1].name.Length, "equals: the shorter name first");
        var edda = SearchMath.Find("edda", items);
        C(edda.Count == 1 && edda[0].kind == SearchKind.Person && edda[0].items[0].key == "p-4", "found without its accent");
        var ashfall = SearchMath.Find("ashfall", items);
        C(ashfall[0].kind == SearchKind.People && ashfall.Any(g => g.kind == SearchKind.Place), "a people by its name, a town by its people's name after it");
        C(SearchMath.Find("", items).Count == 0 && SearchMath.Find("zzz", items).Count == 0 && SearchMath.First(new List<SearchGroup>()) == null, "no query, no match, no first");
        string search = Code("UI/Search.cs");
        C(Regex.IsMatch(search, @"public Window_Search\(Rect anchor\)[\s\S]*?index = Search\.Index\(\);"), "the index is made when the field opens, never before");
        C(search.Contains("SearchMath.Find(query, index)") && Regex.IsMatch(search, @"if \(query != searched\)"), "filtered as the player types, only when the words change");
        C(search.Contains("public static void Register(Func<IEnumerable<SearchItem>> items)") && search.Contains("\"Council_Key_Search\""), "Search.Register for later kinds; a key");
        foreach (var kind in Enum.GetValues(typeof(SearchKind)).Cast<SearchKind>()) C(search.Contains("case SearchKind." + kind) || kind == SearchKind.Other, $"the {kind} kind has its heading");
        // The game hands Enter (its accept key) to the topmost window that takes it before any window draws: the search
        // takes it, or the window under it (the one whose corner opened it) closes instead.
        C(search.Contains("forceCatchAcceptAndCancelEventEvenIfUnfocused = true;") && Regex.IsMatch(search, @"public override void OnAcceptKeyPressed\(\)\s*\{\s*Event\.current\.Use\(\);\s*Take\(Lit\);"),
            "Enter takes the result lit, and never closes the window under the search");
        C(search.Contains("!portraits.TryGetValue(item.key, out pawn)") && Regex.IsMatch(search,@"public static List<SearchItem> Index\(\)\s*\{\s*portraits\.Clear\(\);"),
            "the results' portraits found once per index, not every frame");
        var searchKey = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_SearchKeys.xml")).Root!.Elements("KeyBindingDef").Select(e => e.Element("defName")!.Value).ToList();
        C(searchKey.SequenceEqual(new[] { "Council_Key_Search" }), "the search's key");

        // ---------------- The corner and the council button ----------------
        string tabs = Code("UI/WindowTabs.cs");
        C(tabs.Contains("Search.Toggle(") && tabs.Contains("InboxIcon(") && tabs.Contains("FollowingIcon(") && tabs.Contains("Nav.Arrows("),
            "every window's corner: back and forward, the search, the inbox, following");
        C(tabs.Contains("WindowLayoutMath.CornerWidth(WindowLayoutMath.CornerTools, PagesMath.CornerOrder.Length)") && WindowLayoutMath.CornerTools == 6,
            "the corner as wide as the layout test measures it (six tools, four windows)");
        var council = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Council_Misc.xml")).Root!.Elements("MainButtonDef").First(e => e.Element("defName")!.Value == "Council_MainButton");
        C(council.Element("workerClass")?.Value == "ColonyCouncil.MainButtonWorker_Council", "the council button carries the count");
        C(Code("CouncilSettings.cs").Contains("public bool inboxCountOnButton = true;") && Code("UI/SettingsCatalog.cs").Contains("Toggle(\"inboxCountOnButton\""),
            "the count on the council button is a setting, on at first");
        return n;
    }
}
