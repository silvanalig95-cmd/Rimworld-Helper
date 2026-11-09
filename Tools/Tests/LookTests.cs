using System.Text.RegularExpressions;

/// <summary>
/// One look across every window (docs/design/interface.md, "The look"; UI 5b): the colours are one set of tokens in
/// UiKit and no page makes a colour of its own; the grid's measures; the components drawn one way (every section heading
/// through one line, a tag as a state chip, acts greyed with why not, filter chips, the line of days); the pages draw
/// with them rather than with the game's bare buttons and tabs; names as links in the on-screen panels, the hover card,
/// This week and the council's pages; no bold in the keyed texts.
/// </summary>
public static class LookTests
{
    /// <summary>The design's tokens by meaning and the surfaces, each a field in UiKit's one block of tokens.</summary>
    public static readonly string[] Tokens =
    {
        "Ink", "Faint", "Dim", "Heading", "Gold", "Link", "LinkLine", "LinkHot", "Warm", "Cool", "Sour", "Soon", "Danger", "Done", "World",
        "CardFill", "CardHover", "CardSelected", "CardBorder", "ZebraFill", "DividerColor", "MeterBack", "MeterEdge", "Shade", "Ground", "PortraitBack",
        "DrawerFill", "PreviewFill", "PreviewEdge", "BadgeFill"
    };

    /// <summary>The pages Rimfolk draws outside UI/ (every file under UI/ is read too).</summary>
    private static readonly string[] PagesElsewhere =
    {
        "Moods/MoodView.cs", "Moods/MoodsPanel.cs", "Calendar/CalendarPanel.cs", "Heart/HeartView.cs", "Heart/HeartMeter.cs", "Almanac/AlmanacPanel.cs", "Almanac/Window_Almanac.cs",
        "Diplomacy/DiplomacyPanels.cs", "World/NeighboursPanel.cs", "World/FactionMemoryPanel.cs", "World/WorldSituationsPanel.cs", "Outsiders/OutsiderHeard.cs"
    };

    /// <summary>The council's and the planning window's pages and the defense page: drawn with the kit's acts, chips and modes.</summary>
    private static readonly string[] Pages =
    {
        "UI/WeekPanel.cs", "UI/PeopleTable.cs", "Moods/MoodsPanel.cs", "Moods/MoodCompact.cs", "UI/SocialPanel.cs", "UI/FaithsPanel.cs", "UI/RenownPage.cs",
        "UI/DecreePanel.cs", "UI/ElectionPanel.cs", "UI/AmbitionPanel.cs", "UI/WaysPanel.cs", "UI/WishesPanel.cs", "UI/KeptPanel.cs", "UI/DilemmasPanel.cs",
        "UI/OverviewPanel.cs", "UI/ChroniclePanel.cs", "UI/HistoryPanels.cs", "UI/NextStepsPanel.cs", "UI/AheadPanel.cs", "UI/AheadMorePanel.cs",
        "UI/BuildAheadPanel.cs", "UI/PrioritiesPanel.cs", "UI/BasePanel.cs", "UI/MakingPanel.cs", "UI/BuildPanel.cs", "UI/RoomsPanel.cs", "UI/DefensePanel.cs",
        "UI/DefenseLinesPanel.cs"
    };

    private static readonly Regex Made = new Regex(@"new Color\(|Color\.(red|green|blue|yellow|cyan|magenta|gray|grey|black)\b");

    /// <summary>The code without its comments (a colour named in a comment is no colour drawn).</summary>
    private static string Code(string text) => Regex.Replace(text, @"//[^\n]*", "");

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "one look across every window: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));

        // ---------------- The tokens: one set, in UiKit ----------------
        string kit = Read("UI/UiKit.cs");
        const string open = "// ---------------- Tokens ----------------", shut = "// ---------------- End of the tokens ----------------";
        int a = kit.IndexOf(open, StringComparison.Ordinal), b = kit.IndexOf(shut, StringComparison.Ordinal);
        C(a > 0 && b > a && kit.IndexOf(open, a + 1, StringComparison.Ordinal) < 0, "UiKit has one block of tokens");
        string block = a > 0 && b > a ? kit.Substring(a, b - a) : "";
        // Each token has two values since Rimfolk's own look (docs/design/rimfolk-look.md): the game's look as before, and
        // Rimfolk's own, chosen by whether a window in Rimfolk's frame draws in it.
        foreach (var t in Tokens)
        {
            C(Regex.IsMatch(block, @"public static (readonly )?Color " + t + @"\b"), $"the token {t} is in the block");
            C(Regex.IsMatch(block, @"public static Color " + t + @" => own \? own_" + t + @" : game_" + t + ";")
              && Regex.IsMatch(block, @"private static readonly Color game_" + t + @" = ") && Regex.IsMatch(block, @"private static readonly Color own_" + t + @" = "),
                $"the token {t} has the game's value and Rimfolk's own");
        }
        // The names the pages used before are tokens now, so any code that reads them draws the same set.
        foreach (var (old, token) in new[]
                 {
                     ("Good", "Warm"), ("Bad", "Sour"), ("Warn", "Soon"), ("Info", "Cool"), ("Neutral", "Faint"), ("Quote", "Ink"), ("HeaderColor", "Heading"),
                     ("DreamColor", "Faint"), ("LossColor", "Faint"), ("StrangerColor", "Cool"), ("WorldColor", "World")
                 })
            C(block.Contains($"public static readonly Color {old} = {token};") || block.Contains($"public static Color {old} => {token};"), $"the old name {old} is the token {token}");
        // Nothing else in the kit makes a colour: outside the block, every colour is a token or a token made fainter.
        string outside = a > 0 && b > a ? kit.Substring(0, a) + kit.Substring(b) : kit;
        var kitFiles = new List<(string name, string code)> { ("UI/UiKit.cs (outside the tokens)", Code(outside)) };
        foreach (var f in Directory.GetFiles(Path.Combine(src, "UI"), "UiKit*.cs"))
            if (Path.GetFileName(f) != "UiKit.cs")
                kitFiles.Add(("UI/" + Path.GetFileName(f), Code(File.ReadAllText(f))));
        foreach (var (name, code) in kitFiles) C(!Made.IsMatch(code), $"{name} makes no colour of its own");
        C(!kit.Contains("public static Color WithAlpha(Color c, float a) => new Color("), "WithAlpha keeps the token, only its alpha changes");

        // ---------------- No page makes a colour of its own ----------------
        var pages = new List<string>();
        foreach (var f in Directory.GetFiles(Path.Combine(src, "UI"), "*.cs", SearchOption.AllDirectories))
            if (!Path.GetFileName(f).StartsWith("UiKit", StringComparison.Ordinal))
                pages.Add(Path.GetRelativePath(src, f).Replace('\\', '/'));
        pages.AddRange(PagesElsewhere);
        C(pages.Count > 100, $"every file under UI/ is read ({pages.Count} with the pages elsewhere)");
        foreach (var rel in pages)
        {
            var lines = Read(rel).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = Code(lines[i]);
                if (Made.IsMatch(line)) C(false, $"{rel}:{i + 1} makes a colour outside the tokens: {lines[i].Trim()}");
            }
        }
        // The page accents gave way: the Peoples cards' violet, the world's tan, the journeys' sage.
        C(Read("Diplomacy/DiplomacyPanels.cs").Contains("Color Accent => UiKit.Link;"), "the Peoples cards' names are in link ink, not violet");
        C(Read("World/WorldSituationsPanel.cs").Contains("Color Accent => UiKit.World;") && Read("World/FactionMemoryPanel.cs").Contains("Color Accent => UiKit.World;"),
            "the world's panels in the world's colour, not tan or violet");

        // ---------------- The grid ----------------
        foreach (var (name, value) in new[]
                 {
                     ("Padding", "12f"), ("SectionGap", "16f"), ("CardGap", "8f"), ("CardInset", "10f"), ("RowDense", "22f"), ("RowStandard", "28f"),
                     ("RowPortrait", "40f"), ("CardHeadHeight", "56f")
                 })
            C(Regex.IsMatch(kit, $@"public const float {name} = {Regex.Escape(value)};"), $"the grid's {name} is {value}");
        C(Regex.IsMatch(kit, @"public const float IconDense = 14f, IconRow = 18f, IconHead = 24f;"), "icons 14 in a dense row, 18 in a row, 24 on a head");
        string layout = Read("UI/CouncilLayoutMath.cs");
        C(layout.Contains("public const float ColumnGap = 8f;") && layout.Contains("public const float RowHeight = 28f;") && layout.Contains("public const float CardHeader = 32f;"),
            "This week's cards 8 apart, rows of 28, a heading of 32");

        // ---------------- The components, drawn one way ----------------
        string look = Read("UI/UiKitLook.cs"), cards = Read("UI/UiKitCards.cs");
        foreach (var sig in new[]
                 {
                     "internal static void HeadingLine(", "public static bool Row(", "public static bool RowAct(", "public static float WhyNotLine(", "public static void DaysLine(",
                     "public static int DaysLineWithWord(", "public static void EmptyLine(", "public static float NameLink(", "public static bool FilterChip(Rect r, string label, int count",
                     "public static void DashedBox("
                 })
            C(look.Contains(sig), $"the kit has {sig.TrimEnd('(')}");
        // Every section heading through the one line: SectionTitle, FoldTitle and SectionHeading alike.
        C(Regex.Matches(kit, @"\bHeadingLine\(").Count >= 3 && Regex.Matches(cards, @"\bHeadingLine\(").Count >= 1, "section titles, folds and headings draw the same line");
        C(kit.Contains("Chip(x, y, label, color, icon, tip, maxWidth, PillHeight, false)") && kit.Contains("CardFill, CardBorder"), "a tag is drawn as a state chip: a frame, a dot or icon in its colour, the word in ink");
        C(cards.Contains("var c = i < at ? Done : i == at ? Gold : Dim;"), "steps: done in done's colour, the one it is at gold, the rest quiet");
        C(kit.Contains("DashedBox(") && kit.Contains("Icon(new Rect(x + width / 2f - iconSize / 2f, top, iconSize, iconSize), icon, Dim);"), "an empty state: its icon quiet, its sentence in a dashed frame");
        C(Read("UI/Cards/CardView.cs").Contains("UiKit.WhyNotLine("), "a card says why not in a line under its acts");

        // ---------------- The pages draw with them ----------------
        foreach (var rel in Pages)
        {
            string code = Code(Read(rel));
            C(!code.Contains("Widgets.ButtonText("), $"{rel}: its acts through the kit (greyed with why not), not the game's bare button");
            C(!code.Contains("UiKit.TabButton("), $"{rel}: filters and views as filter chips and modes, not tabs");
        }
        C(Read("UI/PeopleTable.cs").Contains("UiKit.FilterChip(") && Read("UI/DilemmasPanel.cs").Contains("UiKit.FilterChip(") && Read("UI/ChroniclePanel.cs").Contains("UiKit.FilterChip(")
          && Read("UI/DecreePanel.cs").Contains("UiKit.FilterChip("), "the People table's, the dilemmas', the chronicle's and the decrees' filters are filter chips");
        C(Read("UI/SocialPanel.cs").Contains("UiKit.Modes(") && Read("Moods/MoodCompact.cs").Contains("UiKit.Modes("), "List or Web, Compact or Details: the modes switch");
        C(Read("UI/OverviewPanel.cs").Contains("UiKit.DaysLine(") && Read("Moods/MoodsPanel.cs").Contains("UiKit.DaysLineWithWord("), "the overview's tiles and the colony's mood as lines of days");
        C(Read("UI/AheadPanel.cs").Contains("TextAnchor num = TextAnchor.MiddleRight") && Read("UI/BuildAheadPanel.cs").Contains("TextAnchor num = TextAnchor.MiddleRight")
          && Read("UI/PeopleTable.cs").Contains("cell.number ? TextAnchor.MiddleRight"), "numbers right-aligned in their columns");
        C(Read("UI/WeekPanel.cs").Contains("UiKit.RowAct(") && Read("UI/WeekPanel.cs").Contains("UiKit.HeadingLine("), "This week's sections and acts in the kit's grammar");
        foreach (var rel in new[] { "UI/NextStepsPanel.cs", "UI/PrioritiesPanel.cs", "UI/BasePanel.cs", "UI/DefensePanel.cs" })
            C(Read(rel).Contains("UiKit.LevelColor("), $"{rel}: an advice's level by its token");

        // ---------------- Names as links where Rimfolk draws them ----------------
        string panel = Read("Hud/HudPanel.cs");
        C(panel.Contains("protected bool NameLink(") && panel.Contains("Previews.Hover(") && panel.Contains("Cards.Open(thing)"), "the on-screen panels draw a name as a link: a preview, its card on a click");
        foreach (var rel in new[] { "Hud/HudMoodBoard.cs", "Hud/HudHealth.cs", "Hud/HudWork.cs" }) C(Read(rel).Contains("NameLink("), $"{rel}: the colonist's name is a link");
        C(Read("Hud/HudFollowing.cs").Contains("line.thing = thing") && Read("Hud/HudLines.cs").Contains("LinkWords("), "the Following panel's lines are links");
        C(Read("Hud/HudHoverCard.cs").Contains("Cards.Open(ThingRefs.Of(p))") && Read("Hud/HudHoverCard.cs").Contains("HoverPlace.Corner"),
            "the hover card's name is a link where the card can be reached (in the corner)");
        C(Read("UI/InboxUnderWay.cs").Contains("PaneLinks.Mark(text, plan.travellers)") && Read("UI/InboxUnderWay.cs").Contains("PaneLinks.Mark(label, who)"),
            "This week's journey and what plays out on the map name their people as links");
        C(Read("UI/DecreePanel.cs").Contains("PaneLinks.Mark(l.note, req.pawn)") && Read("UI/DecreePanel.cs").Contains("UiKit.PawnChip(186f, y + 4f, holder, 24f, true, 180f, link: true)"),
            "the decrees' asker and the offices' holders are links");
        C(Read("UI/WishesPanel.cs").Contains("l.headerShown = PaneLinks.Mark(l.header"), "a wish's asker is a link");
        C(Read("UI/DilemmasPanel.cs").Contains("PaneLinks.Mark(r.who, colonists)"), "who answered a dilemma is a link");
        C(Read("UI/ChroniclePanel.cs").Contains("internal static string TextOf(") && Read("UI/AmbitionPanel.cs").Contains("ChroniclePanel.TextOf("),
            "the chronicle's lines name the colony's people as links, on the Chronicle and the council's pages alike");
        foreach (var rel in new[] { "UI/SocialPanel.cs", "UI/FaithsPanel.cs", "UI/ElectionPanel.cs", "UI/AmbitionPanel.cs", "UI/WaysPanel.cs", "UI/KeptPanel.cs", "UI/WishesPanel.cs" })
            C(Regex.IsMatch(Read(rel), @"UiKit\.PawnChip\([^;]*link: true"), $"{rel}: a colonist's chip is a link");

        // ---------------- Type: no bold in running text ----------------
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
        {
            string text = File.ReadAllText(f);
            C(!text.Contains("&lt;b&gt;") && !text.Contains("<b>"), $"{Path.GetFileName(f)}: no bold in running text");
        }
        return n;
    }
}
