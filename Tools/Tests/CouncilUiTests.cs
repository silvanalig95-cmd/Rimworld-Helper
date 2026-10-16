using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The council window's newer pages (Source/ColonyCouncil/UI): This week's cards fit their columns at every width, its
/// cards go into the shorter column, the people table and the dilemma table keep their columns on the page at every
/// width (hiding the ones that give way first, never the ones that must stay), the mood balance bar splits sensibly;
/// and every mood hint's "Go to" button leads to a real page, the page its text names. The page shortcuts each have a
/// key binding, and every page the window has is reachable from a section.
/// </summary>
public static class CouncilUiTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int checks = 0;
        void Check(bool ok, string what)
        {
            checks++;
            check(ok, "council window: " + what);
        }

        // ---------- This week: columns and cards ----------
        for (float w = 280f; w <= 2400f; w += 7f)
        {
            int cols = CouncilLayoutMath.Columns(w);
            float cw = CouncilLayoutMath.ColumnWidth(w, cols);
            Check(cols == 1 || cols == 2, $"one or two columns at {w}");
            Check(cols == 1 || cw >= 300f, $"two columns only when each is wide enough ({cw:0} at {w})");
            Check(cw * cols + CouncilLayoutMath.ColumnGap * (cols - 1) <= w + 0.01f, $"the columns fit the width ({w})");
        }
        for (int rows = 0; rows <= 40; rows++)
        {
            int shown = CouncilLayoutMath.ShownRows(rows);
            Check(shown <= rows && shown <= CouncilLayoutMath.MaxRows + 1, $"{rows} rows: no more than the cap show ({shown})");
            Check(rows == 0 || shown >= 1, $"{rows} rows: at least one shows");
            Check(rows <= CouncilLayoutMath.MaxRows + 1 ? shown == rows : shown == CouncilLayoutMath.MaxRows, $"{rows} rows: a single extra row shows itself, more fold away");
            float h = CouncilLayoutMath.CardHeight(rows);
            int lines = rows == 0 ? 1 : shown + (rows > shown ? 1 : 0);
            Check(Math.Abs(h - (CouncilLayoutMath.CardHeader + lines * CouncilLayoutMath.RowHeight + CouncilLayoutMath.CardPad)) < 0.01f, $"{rows} rows: the card is as tall as its lines");
            Check(rows == 0 || CouncilLayoutMath.CardHeight(rows) >= CouncilLayoutMath.CardHeight(rows - 1), $"{rows} rows: more rows never make a card shorter");
        }
        // Placing: each card into the shorter column, so the columns end within one card of each other.
        var rnd = new Random(7);
        for (int t = 0; t < 200; t++)
        {
            int n = rnd.Next(1, 14);
            var heights = new float[n];
            for (int i = 0; i < n; i++) heights[i] = CouncilLayoutMath.CardHeight(rnd.Next(0, 12));
            foreach (int cols in new[] { 1, 2 })
            {
                var into = CouncilLayoutMath.Place(heights, cols);
                Check(into.Length == n && into.All(c => c >= 0 && c < cols), $"placing {n} cards in {cols}: every card in a column");
                var used = new float[cols];
                for (int i = 0; i < n; i++) used[into[i]] += heights[i] + CouncilLayoutMath.ColumnGap;
                if (cols == 2) Check(Math.Abs(used[0] - used[1]) <= heights.Max() + CouncilLayoutMath.ColumnGap + 0.01f, $"placing {n} cards: the columns end close together");
                Check(into[0] == 0, "the first card goes left");
            }
        }
        Check(Math.Abs(CouncilLayoutMath.GoodShare(0f, 0f) - 0.5f) < 0.001f, "an empty balance sits in the middle");
        Check(CouncilLayoutMath.GoodShare(3f, 0f) == 1f && CouncilLayoutMath.GoodShare(0f, -3f) == 0f, "all good fills the bar; all bad empties it");
        Check(Math.Abs(CouncilLayoutMath.GoodShare(3f, -1f) - 0.75f) < 0.001f && Math.Abs(CouncilLayoutMath.GoodShare(3f, 1f) - 0.75f) < 0.001f, "bad counts by size, whatever its sign");

        // ---------- The tables: People → Everyone and Council → Dilemmas ----------
        TableChecks(Check, "people table", CouncilLayoutMath.PeopleMin, CouncilLayoutMath.PeopleWeight, CouncilLayoutMath.PeoplePriority);
        TableChecks(Check, "dilemma table", CouncilLayoutMath.DilemmaMin, CouncilLayoutMath.DilemmaWeight, CouncilLayoutMath.DilemmaPriority);
        // At the council window's usual width every column of both tables shows.
        foreach (var (name, min, weight, prio) in new[]
                 {
                     ("people", CouncilLayoutMath.PeopleMin, CouncilLayoutMath.PeopleWeight, CouncilLayoutMath.PeoplePriority),
                     ("dilemma", CouncilLayoutMath.DilemmaMin, CouncilLayoutMath.DilemmaWeight, CouncilLayoutMath.DilemmaPriority)
                 })
        {
            int n = min.Length;
            float[] x = new float[n], w = new float[n];
            bool[] shown = new bool[n];
            CouncilLayoutMath.Layout(CouncilLayoutMath.UsualTableWidth, min, weight, prio, x, w, shown);
            Check(shown.All(s => s), $"the {name} table shows every column in the usual window");
        }
        // The people table's columns match its enum (Name, Mood, Rimfolk, Trust, Strain, Weighing, Wish, Story, Health, Quirks).
        string table = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "UI", "PeopleTable.cs"));
        var colEnum = Regex.Match(table, @"public enum Col\s*\{([^}]*)\}").Groups[1].Value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        Check(colEnum.Count == CouncilLayoutMath.PeopleMin.Length, $"one width per people table column ({colEnum.Count} columns)");
        Check(colEnum.Count > 0 && colEnum[0] == "Name" && CouncilLayoutMath.PeoplePriority[0] == 0, "the name column always shows");

        // ---------- The council window's pages ----------
        string ui = Path.Combine(root, "Source", "ColonyCouncil", "UI");
        string window = File.ReadAllText(Path.Combine(ui, "MainTabWindow_Council.cs"));
        var tabMatch = Regex.Match(window, @"private enum Tab\s*\{([^}]*)\}");
        Check(tabMatch.Success, "found the window's pages");
        var pages = tabMatch.Groups[1].Value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToHashSet();
        Check(pages.Contains("Week") && pages.Contains("Everyone") && pages.Contains("Dilemmas"), "the new pages are pages");
        // Every page is in a section's list.
        var listed = new HashSet<string>();
        foreach (Match m in Regex.Matches(window, @"Tab\[\] \w+Tabs = \{([^}]*)\}"))
            foreach (var t in m.Groups[1].Value.Split(','))
                listed.Add(t.Trim().Replace("Tab.", ""));
        foreach (var p in pages) Check(listed.Contains(p), $"page {p} is in a section");
        Check(Regex.IsMatch(window, @"ColonyTabs = \{ Tab\.Week,"), "This week is the Colony section's first page");

        var keyed = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements())
                keyed[el.Name.LocalName] = el.Value;
        foreach (var p in pages) Check(keyed.ContainsKey("CC_Tab_" + p) || p == "Wishes" || p == "Decrees", $"page {p} has a name (CC_Tab_{p})");

        // ---------- Mood hints: "Go to ..." leads to the page the text names ----------
        var named = new Dictionary<string, string>
        {
            ["Wishes page"] = "Wishes", ["Decrees page"] = "Decrees", ["World page"] = "World", ["Ambition page"] = "Ambition",
            ["Moods page"] = "Moods", ["Dilemmas page"] = "Dilemmas", ["Calendar page"] = "Calendar", ["Bonds page"] = "Bonds"
        };
        int hints = 0, linked = 0;
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "MoodHintDefs"), "*.xml"))
        {
            foreach (var d in XDocument.Load(file).Root!.Elements("ColonyCouncil.MoodHintDef"))
            {
                hints++;
                string name = d.Element("defName")?.Value ?? "?";
                string tab = d.Element("councilTab")?.Value;
                string fix = d.Element("fix")?.Value ?? "";
                if (tab != null)
                {
                    linked++;
                    Check(pages.Contains(tab), $"{name}: its page {tab} is a page of the council window");
                }
                foreach (var kv in named)
                    if (fix.Contains(kv.Key))
                        Check(tab == kv.Value, $"{name}: its text names the {kv.Key}, and its button goes there (goes to {tab ?? "nothing"})");
                if (Regex.IsMatch(fix, @"\b(dilemma|decree)s?\b", RegexOptions.IgnoreCase) && name.StartsWith("Council_Mood_RF_"))
                    Check(tab != null || d.Element("article") != null, $"{name}: a hint about the council links somewhere");
            }
        }
        Check(hints >= 150, $"found the mood hints ({hints})");
        Check(linked >= 45, $"most of Rimfolk's council hints lead to a page ({linked})");

        // ---------- Shortcuts: each page key has a binding; each binding names a page ----------
        string keys = File.ReadAllText(Path.Combine(ui, "CouncilKeys.cs"));
        var keyPages = Regex.Match(keys, @"Pages = \{([^}]*)\}").Groups[1].Value.Split(',').Select(s => s.Trim().Trim('"')).Where(s => s.Length > 0).ToList();
        Check(keyPages.Count >= 6, "found the shortcut pages");
        var bindingsXml = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Council_KeyBindings.xml"));
        var bindings = bindingsXml.Root!.Elements("KeyBindingDef").Select(e => e.Element("defName")?.Value).Where(n => n != null).ToHashSet();
        foreach (var p in keyPages)
        {
            Check(pages.Contains(p), $"shortcut page {p} is a page");
            Check(bindings.Contains("Council_Key_" + p), $"page {p} has a key binding");
        }
        Check(bindings.Contains("Council_Key_NextPage") && bindings.Contains("Council_Key_PreviousPage"), "next and previous page keys exist");
        foreach (var e in bindingsXml.Root!.Elements("KeyBindingDef"))
            Check(e.Element("defaultKeyCodeA") == null && e.Element("defaultKeyCodeB") == null, $"{e.Element("defName")?.Value ?? "the base"}: no key until the player picks one");
        return checks;
    }

    private static void TableChecks(Action<bool, string> Check, string what, float[] min, float[] weight, int[] priority)
    {
        int n = min.Length;
        Check(weight.Length == n && priority.Length == n, $"{what}: one width, share and priority per column");
        float[] x = new float[n], w = new float[n];
        bool[] shown = new bool[n];
        float mustStay = 0f;
        int staying = 0;
        for (int i = 0; i < n; i++)
            if (priority[i] == 0)
            {
                mustStay += min[i];
                staying++;
            }
        mustStay += CouncilLayoutMath.CellGap * Math.Max(0, staying - 1);
        for (float width = 200f; width <= 2400f; width += 5f)
        {
            bool fits = CouncilLayoutMath.Layout(width, min, weight, priority, x, w, shown);
            string at = $"{what} at {width}";
            float right = 0f;
            for (int i = 0; i < n; i++)
            {
                if (priority[i] == 0) Check(shown[i], $"{at}: column {i} always shows");
                if (!shown[i]) continue;
                Check(w[i] > 0f, $"{at}: column {i} has a width");
                if (fits) Check(w[i] >= min[i] - 0.01f, $"{at}: column {i} at least its smallest width");
                right = Math.Max(right, x[i] + w[i]);
            }
            Check(right <= width + 0.05f, $"{at}: the table ends inside the page ({right:0.0})");
            // Left to right, never overlapping.
            float last = -1f;
            for (int i = 0; i < n; i++)
            {
                if (!shown[i]) continue;
                Check(x[i] >= last - 0.01f, $"{at}: column {i} starts after the one before");
                last = x[i] + w[i];
            }
            // A column hidden means every column that gives way earlier is hidden too.
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    if (!shown[i] && priority[j] > priority[i])
                        Check(!shown[j], $"{at}: column {j} gives way before column {i}");
            // Hidden only when needed: the shown ones plus the next to come back wouldn't fit.
            if (fits && shown.Any(s => !s))
            {
                int back = -1;
                for (int i = 0; i < n; i++)
                    if (!shown[i] && (back < 0 || priority[i] < priority[back]))
                        back = i;
                float need = CouncilLayoutMath.CellGap * shown.Count(s => s);
                for (int i = 0; i < n; i++)
                    if (shown[i] || i == back)
                        need += min[i];
                Check(need > width - 0.01f, $"{at}: column {back} is hidden only when it wouldn't fit");
            }
            if (width >= mustStay) Check(fits, $"{at}: the columns that must stay fit");
            // With room to spare, the table fills the width.
            if (fits && weight.Where((_, i) => shown[i]).Sum() > 0f) Check(Math.Abs(right - width) < 0.05f, $"{at}: the table fills the width ({right:0.0})");
        }
    }
}
