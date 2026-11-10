using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Rimfolk's own look, stage 2 (docs/design/rimfolk-look.md; Source/ColonyCouncil/UI/FramedWindowsMath.cs,
/// FloatingWindows.cs, WindowFrame.cs): the floating windows' one-at-a-time rule for every set of open windows, every
/// window's key and least size on the screens the game runs on, the council's and the dialogs' layouts from their least
/// to a window made large, and the seams (the registry Pages asks, the button worker of all four, every jump to the map
/// through LeaveForMap, every Rimfolk window framed but those that must stay as they are, the kit's controls drawing the
/// game's own in its look, the cached colours as properties).
/// </summary>
public static class RimfolkLookStageTwoTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "Rimfolk's own look, stage 2: " + what);
        }

        OneAtATime(C);
        Keys(C);
        Layouts(C);
        Seams(root, C);
        return n;
    }

    private static readonly RimfolkWindow[] Four = { RimfolkWindow.Council, RimfolkWindow.World, RimfolkWindow.Defense, RimfolkWindow.Planning };

    // ---------------- One of the four at a time ----------------

    private static void OneAtATime(Action<bool, string> C)
    {
        C(FramedWindowsMath.Floating.Length == 4 && Four.All(w => FramedWindowsMath.Floating.Contains(w)), "the registry holds the four windows");
        C(FramedWindowsMath.Floating.SequenceEqual(PagesMath.CornerOrder), "in the corner's order");
        var keys = Four.Select(FramedWindowsMath.KeyOf).ToList();
        C(keys.Distinct().Count() == 4 && FramedWindowsMath.KeyOf(RimfolkWindow.World) == "Region" && FramedWindowsMath.KeyOf(RimfolkWindow.Council) == "Council"
          && FramedWindowsMath.KeyOf(RimfolkWindow.Planning) == "Planning" && FramedWindowsMath.KeyOf(RimfolkWindow.Defense) == "Defense",
            "each floats under its own key: Region, Council, Planning, Defense");
        // Every window opening, with every set of the four open before (sixteen sets): the others close, that one alone stays.
        int sets = 0;
        foreach (var opening in Four)
            for (int mask = 0; mask < 16; mask++)
            {
                var open = Four.Where((w, i) => (mask & (1 << i)) != 0).ToList();
                var closes = FramedWindowsMath.ToClose(opening, open);
                var after = FramedWindowsMath.AfterOpening(opening, open);
                sets++;
                C(closes.All(w => w != opening && open.Contains(w)) && open.Where(w => w != opening).All(closes.Contains) && closes.Distinct().Count() == closes.Count,
                    $"{opening} opening with {string.Join(",", open)} open closes exactly the others ({string.Join(",", closes)})");
                C(after.Count == 1 && after[0] == opening, $"{opening} opening with {string.Join(",", open)} open: it alone is open after");
            }
        C(sets == 64, $"every window opening with every set open ({sets})");
        C(FramedWindowsMath.ToClose(RimfolkWindow.World, null).Count == 0 && FramedWindowsMath.AfterOpening(RimfolkWindow.World, null).SequenceEqual(new[] { RimfolkWindow.World }),
            "with nothing open nothing closes");
        C(FramedWindowsMath.ToClose(RimfolkWindow.Council, new[] { RimfolkWindow.World, RimfolkWindow.World }).Count == 1, "a window listed twice closes once");
    }

    // ---------------- Every key and its least size ----------------

    private static void Keys(Action<bool, string> C)
    {
        var all = FramedWindowsMath.Keys;
        C(all.Select(k => k.key).Distinct().Count() == all.Length, $"every framed window's key is its own ({all.Length} keys)");
        C(all.Length >= 35, "the four windows, the Almanac, the card, the trade windows and every dialog have their keys");
        foreach (var name in new[] { "Region", "Council", "Planning", "Defense", "Almanac", "Card", "Trade", "Market", "Stall", "Letter", "Journey", "JourneyPick", "Camp", "Founding" })
            C(FramedWindowsMath.TryKey(name, out _), $"{name} has its key");
        C(!FramedWindowsMath.TryKey("Nobody", out var none) && none.leastW == WindowFrameMath.LeastW && none.leastH == WindowFrameMath.LeastH, "an unknown key gets the frame's least");
        var screens = new[] { (1024f, 768f), (1366f, 768f), (1280f, 800f), (1920f, 1080f), (2560f, 1440f), (3840f, 2160f) };
        var scales = new[] { 1f, 1.5f, 2f };
        foreach (var k in all)
        {
            C(k.leastW >= WindowFrameMath.LeastW && k.leastH >= WindowFrameMath.LeastH, $"{k.key}: its least ({k.leastW:0}x{k.leastH:0}) no smaller than the frame's");
            C(k.leastW <= 1024f && k.leastH <= 768f - k.Bottom, $"{k.key}: its least fits a 1024 by 768 screen at scale 1");
            C(k.leastH + WindowFrameMath.TitleH <= 1000f && k.leastW > k.leastH * 0.3f, $"{k.key}: a least within reason");
            C(k.keepsBar == (new[] { "Region", "Council", "Planning", "Defense", "Almanac", "Card", "ColonistCard" }.Contains(k.key)), $"{k.key}: {(k.keepsBar ? "stands beside play, above the bottom bar" : "a dialog, which may cover it")}");
            foreach (var (pw, ph) in screens)
            foreach (var s in scales)
            {
                float sw = (float)Math.Floor(pw / s), sh = (float)Math.Floor(ph / s);
                var first = WindowFrameMath.Centred(k.leastW + 200f, k.leastH + 100f, sw, sh, k.leastW, k.leastH, k.Bottom);
                WindowFrameMath.Least(k.leastW, k.leastH, sw, sh, k.Bottom, out float lw, out float lh);
                C(first.x >= 0f && first.y >= 0f && first.XMax <= sw + 0.5f && first.YMax <= sh - k.Bottom + 0.5f && first.w >= lw - 0.5f && first.h >= lh - 0.5f,
                    $"{k.key} on {pw}x{ph} at {s}: opens on the screen, no smaller than its least there ({first})");
                var large = WindowFrameMath.Large(sw, sh, k.leastW, k.leastH, k.Bottom);
                C(large.w >= lw - 0.5f && large.h >= lh - 0.5f && large.x >= 0f && large.YMax <= sh - k.Bottom + 0.5f, $"{k.key} on {pw}x{ph} at {s}: large keeps its least, on the screen");
            }
        }
        // The four windows: room for the title bar's title, the corner, large and close in their least.
        foreach (var w in Four)
        {
            FramedWindowsMath.TryKey(FramedWindowsMath.KeyOf(w), out var k);
            float bar = 12f + 26f + 120f + 8f + WindowLayoutMath.CornerWidth() + 8f + 2f * (WindowFrameMath.TitleButton + WindowFrameMath.TitleGap) + 12f;
            C(bar <= k.leastW, $"{w}: the title bar holds the title, the corner and its buttons at its least ({bar:0} of {k.leastW:0})");
            C(k.leastW >= 640f && k.leastH >= 440f, $"{w}: its least keeps room for a view's header, its modes and a page");
        }
        C(WindowFrameMath.LeastH >= WindowFrameMath.TitleH + 32f + 36f + 38f, "the frame's smallest window holds its title bar, a field, a line and its OK");
    }

    // ---------------- The layouts from the least to large ----------------

    private static void Layouts(Action<bool, string> C)
    {
        // This week: one column, two, three on a window made large; each column keeps room for its rows.
        C(FramedWindowsMath.WeekColumns(600f) == 1 && FramedWindowsMath.WeekColumns(CouncilLayoutMath.TwoColumnsFrom) == 2 && FramedWindowsMath.WeekColumns(1499f) == 2
          && FramedWindowsMath.WeekColumns(FramedWindowsMath.WeekThreeFrom) == 3, "This week in one, two, then three columns");
        FramedWindowsMath.TryKey("Council", out var council);
        foreach (var (w, label) in new[] { (council.leastW, "the least"), (1010f, "the standard"), (1280f, "the old large"), (1766f, "1920 made large"), (3532f, "4K made large") })
        {
            float content = WindowFrameMath.BodyWidth(w, true) - 2f * 12f;
            DrawerMath.Split(0f, content, out _, out float viewW, out _, out float drawerW, out bool over);
            C(viewW >= 300f && drawerW >= DrawerMath.Narrowest - 0.5f, $"the council at {label} ({w:0}): a view {viewW:0} wide beside or under the drawer {drawerW:0}");
            float page = viewW - 16f;
            int cols = FramedWindowsMath.WeekColumns(page);
            C(CouncilLayoutMath.ColumnWidth(page, cols) >= 300f, $"the council at {label}: This week's {cols} columns keep room for their rows");
            if (w >= 1766f) C(cols >= 2, $"the council at {label}: This week in more than one column");
            if (w >= 3000f) C(cols == 3, $"the council at {label}: This week in three columns");
            C(over == (content < DrawerMath.BesideFrom), $"the council at {label}: the drawer {(over ? "over" : "beside")} the view");
        }
        // The journey planner and the founding window: the list of who goes grows with the window, the details keep room.
        FramedWindowsMath.TryKey("Journey", out var journey);
        FramedWindowsMath.TryKey("Founding", out var founding);
        foreach (var (w, label) in new[] { (journey.leastW, "the least"), (1120f, "as it opened"), (1766f, "1920 made large"), (3532f, "4K made large") })
        {
            float inner = WindowFrameMath.BodyWidth(w, true);
            float who = FramedWindowsMath.JourneyWhoWidth(inner);
            C(who >= 280f && inner - who - 14f >= 300f, $"the journey planner at {label}: who goes {who:0}, the details {inner - who - 14f:0}");
            if (w >= 1766f) C(who > 500f, $"the journey planner at {label}: who goes takes more of it");
        }
        C(Math.Abs(FramedWindowsMath.JourneyWhoWidth(1084f) - Math.Max(280f, Math.Min(1084f * 0.45f, 500f))) < 0.01f, "the journey planner as it was below its wide width");
        foreach (var w in new[] { founding.leastW, 1000f, 1766f })
        {
            float inner = WindowFrameMath.BodyWidth(w, true), who = FramedWindowsMath.FoundingWhoWidth(inner);
            C(who >= 300f && inner - who - 14f >= 300f, $"the founding window at {w:0}: who goes {who:0}, the right {inner - who - 14f:0}");
        }
        C(Math.Abs(FramedWindowsMath.FoundingWhoWidth(964f) - Mathf_Clamp(964f * 0.46f, 300f, 460f)) < 0.01f, "the founding window as it was below its wide width");
        // A dialog's acts: as wide as they were, wider only in a dialog made at least twice as wide.
        C(FramedWindowsMath.DialogActWidth(604f, 360f) == 360f && FramedWindowsMath.DialogActWidth(719f, 360f) == 360f, "the camp's acts as they were in its usual width");
        C(FramedWindowsMath.DialogActWidth(1200f, 360f) == 540f && FramedWindowsMath.DialogActWidth(4000f, 360f) == 540f && FramedWindowsMath.DialogActWidth(1200f, 460f) == 600f,
            "and up to half again as wide, never past half the dialog, in a large one");
        // The frame's body in either look, for the dialogs' own measures.
        foreach (var k in FramedWindowsMath.Keys)
            C(WindowFrameMath.BodyWidth(k.leastW, true) > 300f && WindowFrameMath.BodyHeight(k.leastH, true) >= 100f && WindowFrameMath.BodyHeight(k.leastH, false) >= 90f,
                $"{k.key}: a body to draw in at its least, in either look");
    }

    private static float Mathf_Clamp(float v, float lo, float hi) => Math.Max(lo, Math.Min(hi, v));

    // ---------------- The seams in the game ----------------

    private static void Seams(string root, Action<bool, string> C)
    {
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => Regex.Replace(File.ReadAllText(Path.Combine(src, rel)), @"//[^\n]*", "");
        string floating = Code("UI/FloatingWindows.cs"), pages = Code("UI/Pages.cs"), tabs = Code("UI/WindowTabs.cs"), frame = Code("UI/WindowFrame.cs");
        // The registry and Pages.
        C(floating.Contains("public static class FloatingWindows") && floating.Contains("FramedWindowsMath.ToClose(w, OpenNow())") && floating.Contains("new Window_Council()")
          && floating.Contains("new Window_Region()") && floating.Contains("new Window_Planning()") && floating.Contains("new Window_Defenses()"),
            "the registry makes each of the four once and closes the other three as one opens");
        C(floating.Contains("win.handedPlace = from") && floating.Contains("!win.FrameKnown"), "a window opening the first time takes the place of the one it turns the page from");
        foreach (var part in new[] { "public static bool Showing(RimfolkWindow w) => FloatingWindows.Showing(w) || TabShowing(w);", "FloatingWindows.Shut(w);", "FloatingWindows.ShutAll(false);",
                     "FloatingWindows.Open(w);", "Close(RimfolkWindow.Council);" })
            C(pages.Contains(part), $"Pages asks the registry: {part}");
        C(!pages.Contains("Window_Region"), "Pages names no window of the four (the registry does)");
        C(Regex.IsMatch(pages, @"public static void LeaveForMap\(\)\s*\{\s*FloatingWindows\.ShutAll\(false\);") && pages.Contains("root.OpenTab != MainButtonDefOf.Inspect"),
            "off to the map closes the floating windows and the tab, the inspect pane kept");
        C(tabs.Contains("FloatingWindows.ShutAll(false);") && tabs.Contains("public sealed override void DoWindowContents(Rect inRect)") && tabs.Contains("protected abstract void DrawContents(Rect inRect, bool cornerInTitle);"),
            "a tab of the four closes the floating ones, and draws the same contents in either look");
        foreach (var (cls, draw) in new[] { ("UI/MainTabWindow_Council.cs", "DrawCouncil"), ("UI/MainTabWindow_Planning.cs", "DrawPlanning"), ("UI/MainTabWindow_Defenses.cs", "DrawDefenses"),
                     ("UI/MainTabWindow_World.cs", "DrawRegion") })
        {
            string text = Code(cls);
            C(text.Contains("internal static void " + draw + "(Rect inRect, bool cornerInTitle)") && floating.Contains(draw + "(inRect, true)") || cls.EndsWith("World.cs") && text.Contains("internal static void DrawRegion(Rect inRect, bool cornerInTitle)"),
                $"{cls}: one static draw for the tab and the floating window");
            C(!Regex.IsMatch(text, @"private readonly List<WindowTab>"), $"{cls}: its tabs kept statically, shared by both");
        }
        string council = Code("UI/MainTabWindow_Council.cs");
        C(council.Contains("internal static void Opening()") && council.Contains("internal static void Closing()") && floating.Contains("MainTabWindow_Council.Opening();")
          && floating.Contains("MainTabWindow_Council.Closing();"), "the council opens on the page last looked at and keeps it, floating or as the tab");
        // The bottom bar's buttons.
        C(floating.Contains("class MainButtonWorker_RimfolkWindow : MainButtonWorker_ToggleTab") && floating.Contains("Pages.WindowOfButton(def)") && floating.Contains("base.Activate();"),
            "one worker for the four buttons: floating, or the tab as before");
        C(Code("UI/CouncilButton.cs").Contains("class MainButtonWorker_Council : MainButtonWorker_RimfolkWindow"), "the council's button keeps its count and floats");
        var misc = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Council_Misc.xml")).Root!.Elements("MainButtonDef").ToDictionary(e => e.Element("defName")!.Value, e => e);
        foreach (var (def, worker) in new[] { ("Council_MainButton", "MainButtonWorker_Council"), ("Rimfolk_WorldButton", "MainButtonWorker_Region"), ("Council_DefenseButton", "MainButtonWorker_RimfolkWindow"),
                     ("Council_RoomsButton", "MainButtonWorker_RimfolkWindow") })
            C(misc[def].Element("workerClass")?.Value == "ColonyCouncil." + worker && misc[def].Element("tabWindowClass") != null, $"{def}: its worker {worker}, its tab kept");
        // Every jump to the map leaves the floating windows.
        var escapes = new List<string>();
        foreach (var f in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(src, f).Replace('\\', '/');
            if (rel == "UI/Pages.cs" || rel.StartsWith("bin/") || rel.StartsWith("obj/")) continue;
            if (Regex.Replace(File.ReadAllText(f), @"//[^\n]*", "").Contains("EscapeCurrentTab")) escapes.Add(rel);
        }
        C(escapes.Count == 0, $"no jump closes only the tab: every one leaves for the map through Pages ({string.Join(", ", escapes)})");
        int leaves = Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Sum(f => Regex.Matches(File.ReadAllText(f), @"Pages\.LeaveForMap\(\)").Count);
        C(leaves >= 25, $"the jumps, plans and picks on the map leave through Pages ({leaves})");
        C(Code("UI/MakingPanel.cs").Contains("Pages.GoToGameTab(MainButtonDefOf.Research)") && Code("Building/NextSteps.cs").Contains("Pages.GoToGameTab(MainButtonDefOf.Research)")
          && Code("Missions/Missions.cs").Contains("Pages.GoToGameTab(MainButtonDefOf.Quests)"), "a page sending the player to the game's research or quests gives way to it, as the tab did");
        C(Code("Hud/HudManager.cs").Contains("FloatingWindows.Shown"), "the on-screen panels step aside from any big floating window");
        // Every window of Rimfolk's framed, but those that stay as they are.
        var stays = new Dictionary<string, string>
        {
            ["Hud/HudPanel.cs"] = "the on-screen panels: the map's layer",
            ["Hud/HudManager.cs"] = "the panels' edit bar: the map's layer",
            ["UI/Search.cs"] = "the search: a popup under its corner icon (in the look when its window is)",
            ["Building/Window_RoomPlanner.cs"] = "the room planner: docked above the architect menu, sized to its content each frame",
            ["Defense/Window_DefensePlanner.cs"] = "the defense planner: docked above the architect menu, sized to its content each frame"
        };
        var plain = new List<string>();
        var framed = new List<string>();
        foreach (var f in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(src, f).Replace('\\', '/');
            if (rel.StartsWith("bin/") || rel.StartsWith("obj/")) continue;
            string text = File.ReadAllText(f);
            foreach (Match m in Regex.Matches(text, @"class (\w+) : Window\b"))
                if (!stays.ContainsKey(rel) && !(rel == "UI/WindowFrame.cs" && m.Groups[1].Value == "FramedWindow")) plain.Add(rel + ":" + m.Groups[1].Value);
            foreach (Match m in Regex.Matches(text, @"class (\w+) : (FramedWindow|RimfolkFloatingWindow)\b"))
                framed.Add(m.Groups[1].Value);
        }
        C(plain.Count == 0, $"every window of Rimfolk's is in the frame but those that must stay ({string.Join(", ", plain)})");
        C(framed.Count >= 35, $"framed windows: {framed.Count}");
        foreach (var kv in stays) C(File.Exists(Path.Combine(src, kv.Key)), $"stays as it is: {kv.Value}");
        // Each framed window's key is in the table.
        var keys = FramedWindowsMath.Keys.Select(k => k.key).ToHashSet();
        foreach (var f in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(f);
            foreach (Match m in Regex.Matches(text, @": base\(""(\w+)""(?:\)|,)"))
                if (Regex.IsMatch(text, @"class \w+ : FramedWindow\b"))
                    C(keys.Contains(m.Groups[1].Value), $"{Path.GetFileName(f)}: the key {m.Groups[1].Value} has its least in the table");
        }
        // The frame's handling of the game's own buttons and handles.
        C(frame.Contains("doCloseX = gameCloseX && !shown;") && frame.Contains("doCloseButton = gameCloseButton && !shown;") && frame.Contains("draggable = gameDrag && !WindowFrame.MovesOn;")
          && frame.Contains("resizeable = gameResize && !WindowFrame.MovesOn;"), "the game's close cross, close button, dragging and sizing give way to the frame's and come back without it");
        C(frame.Contains("if (UiKit.Button(b, closeLabel)) Close();") && frame.Contains("protected sealed override float Margin") && frame.Contains("protected sealed override void SetInitialSizeAndPosition()"),
            "the close button drawn in the kit; the margin and the first place the frame's");
        C(frame.Contains("protected static float TitleTop(") && frame.Contains("protected static float TitleAndLine(") && Code("UI/UiKitViews.cs").Contains("public static float DialogHeader("),
            "a dialog's title in the title bar, not said twice");
        // The Almanac.
        string almanac = Code("Almanac/Window_Almanac.cs");
        C(almanac.Contains("class Window_Almanac : FramedWindow") && almanac.Contains("base(\"Almanac\")") && almanac.Contains("AlmanacPanel.Draw(inRect, TitleInBar ? 0f : 30f)")
          && almanac.Contains("protected override bool OwnFirstPlace => true;"), "the Almanac in the frame, first beside Rimfolk's windows, its pages at their own text sizes");
        C(!Code("Almanac/AlmanacPanel.cs").Contains("TextScale.Push(1") && Code("Almanac/AlmanacPanel.cs").Contains("TextScale.Push(AlmanacLayoutMath.Scale(step))"), "the Almanac's text sizes kept");
        // The pages and dialogs draw with the kit.
        var widgets = new Regex(@"Widgets\.(ButtonText|TextField|CheckboxLabeled|DrawHighlightIfMouseover)\(");
        var gameOwned = new HashSet<string>
        {
            "UI/SettingsWindow.cs", "Scenarios/Scenarios.cs", "Trade/TradeScreen.cs", "Trade/TradeMarket.cs", "UI/ITab_Pawn_Story.cs", "Colonies/LandingPatches.cs", "Hud/HudManager.cs",
            "Hud/HudSettingsRows.cs", "UI/BarMore.cs", "Integrations/GuestCheck.cs", "SlowTime.cs", "Patches/Patch_HeartInspectTip.cs", "UI/Region/PeoplesList.cs",
            "Building/Window_RoomPlanner.cs", "Defense/Window_DefensePlanner.cs",
            // Its game look drawn on purpose beside Rimfolk's (stage 1: the sellable items button).
            "Trade/Window_RimfolkTrade.cs",
            // Drawn inside Hospitality's own guest tab, beside the recruiting checklist: the game's look, as the tab's.
            "Integrations/GuestHelpTab.cs"
        };
        var left = new List<string>();
        foreach (var f in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(src, f).Replace('\\', '/');
            if (rel.StartsWith("UI/UiKit") || rel.StartsWith("bin/") || rel.StartsWith("obj/") || gameOwned.Contains(rel)) continue;
            if (widgets.IsMatch(Regex.Replace(File.ReadAllText(f), @"//[^\n]*", ""))) left.Add(rel);
        }
        C(left.Count == 0, $"Rimfolk's pages and dialogs draw buttons, fields, switches and row hovers with the kit ({string.Join(", ", left)})");
        string bar = Code("UI/BarMore.cs");
        C(!Regex.IsMatch(Regex.Match(bar, @"class Dialog_ArrangeBar.*", RegexOptions.Singleline).Value, @"Widgets\.(ButtonText|CheckboxLabeled)\("), "the bar's arranger with the kit (the bar's own pin stays the game's)");
        C(!Code("Integrations/GuestCheck.cs").Contains("class Window_GuestCheck : Window"), "the guest check's window framed (its button in the guest tab stays the game's)");
        string skin = Code("UI/UiKitSkin.cs");
        C(skin.Contains("if (validator != null) return Widgets.TextField(r, text, maxLength > 0 ? maxLength : int.MaxValue, validator);") && skin.Contains("if (maxLength > 0) return Widgets.TextField(r, text, maxLength);"),
            "the kit's field draws the game's own overload in its look");
        C(skin.Contains("if (disabled || nearText) Widgets.CheckboxLabeled(r, label, ref on, disabled, null, null, nearText);") && skin.Contains("else Widgets.CheckboxLabeled(r, label, ref on);"),
            "the kit's switch draws the game's own checkbox in its look");
        C(skin.Contains("Widgets.ButtonText(r, label, true, mouseoverSound, active)"), "the kit's button draws the game's own, its sound as the page asked");
        foreach (var part in new[] { "public static void ListRow(", "public static void Section(" })
        {
            string body = Regex.Match(skin, Regex.Escape(part) + @".*?\n        \}", RegexOptions.Singleline).Value;
            C(body.Contains("if (!own"), $"{part.TrimEnd('(')}: the game's look when Rimfolk's is off");
        }
        // The cached colours follow the look.
        var cached = new List<string>();
        foreach (var f in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(src, f).Replace('\\', '/');
            if (rel.StartsWith("UI/UiKit") || rel.StartsWith("bin/") || rel.StartsWith("obj/")) continue;
            if (Regex.IsMatch(File.ReadAllText(f), @"static readonly Color \w+ = UiKit\.")) cached.Add(rel);
        }
        C(cached.Count == 0, $"no copy of a token kept from load: each a property that follows the look ({string.Join(", ", cached)})");
        foreach (var (file, prop) in new[] { ("Moods/MoodView.cs", "public static Color GoodColor => UiKit.Warm;"), ("Heart/HeartView.cs", "public static Color SteadyColor => UiKit.Warm;"),
                     ("UI/DecreePanel.cs", "private static Color Good => UiKit.Warm;"), ("Almanac/AlmanacPanel.cs", "private static Color Header => UiKit.Heading;"),
                     ("UI/DefenseLinesPanel.cs", "private static Color NowColor => UiKit.Gold;") })
            C(Code(file).Contains(prop), $"{file}: {prop}");
        // Previews and the search take the look of the window their link is in; a card's model kept for each look.
        string previews = Code("UI/Cards/Previews.cs");
        C(previews.Contains("ownAt[level] = UiKit.OwnLook;") && previews.Contains("bool wasOwn = own && UiKit.BeginOwn();") && previews.Contains("if (own) UiKit.EndOwn(wasOwn);"),
            "a preview takes the look of the window its link is in, and gives it back");
        C(Code("UI/Search.cs").Contains("own = UiKit.OwnLook && WindowFrame.OwnLookOn;") && Code("UI/Search.cs").Contains("UiKit.EndOwn(was);"), "the corner's search too");
        C(Code("UI/Cards/Cards.cs").Contains("var built = UiKit.OwnLook ? ownModels : models;"), "a card's model is built for the look it is drawn in");
        C(Code("UI/SelfCheck.cs").Contains("if (UiKit.Button(row, \"Check again\")) again = true;"), "the self check's report made outside the look, its count of what needs a look right in either");
        // The rules are pure and linked.
        string math = File.ReadAllText(Path.Combine(src, "UI", "FramedWindowsMath.cs"));
        C(!Regex.IsMatch(math, @"using (Verse|RimWorld|UnityEngine)"), "the rules have no game types");
        C(File.ReadAllText(Path.Combine(root, "Tools", "Tests", "Tests.csproj")).Contains("UI/FramedWindowsMath.cs"), "and are linked into the tests");
        // The settings say what floats now.
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_OwnLook.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        C(keyed["CC_Settings_WindowsMove_Tip"].Contains("one of the four at a time") && !keyed["CC_Settings_OwnLook_Tip"].Contains("So far"), "the settings' words name every window now");
    }
}
