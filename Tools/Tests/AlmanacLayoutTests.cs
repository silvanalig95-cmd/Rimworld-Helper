using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The Almanac's layout (Source/ColonyCouncil/UI/AlmanacLayoutMath.cs): its text sizes step up evenly; its three columns
/// fill the council window on 768 and 1080 pixel screens at interface scales 1 to 2, and give the page more room than
/// before; and every table on every page, at every text size and screen, either keeps each column wide enough for its
/// longest word, or turns into cards that do. Text is measured with a simple font model (a letter about 7 units wide at
/// the normal size), close enough to the game's small font for the layout's decisions.
/// </summary>
public static class AlmanacLayoutTests
{
    private const float Letter = 7f;

    private static float Width(string s, float scale) => s.Length * Letter * scale;

    /// <summary>One table, as the panel measures it.</summary>
    private sealed class Measured
    {
        public string page;
        public string[][] cells;
        public float[] word, full, typical;
        public float label, valueWord;
    }

    private static Measured Measure(string page, List<string> rows, float scale)
    {
        var t = new Measured { page = page, cells = rows.Select(r => r.Split('|').Select(c => c.Trim()).ToArray()).ToArray() };
        int cols = t.cells.Max(r => r.Length);
        t.word = new float[cols];
        t.full = new float[cols];
        t.typical = new float[cols];
        var counted = new int[cols];
        for (int i = 0; i < t.cells.Length; i++)
            for (int c = 0; c < t.cells[i].Length; c++)
            {
                string s = t.cells[i][c];
                if (s.Length == 0) continue;
                float full = Width(s, scale);
                float word = s.Split(' ').Where(x => x.Length > 0).Select(x => Width(x, scale)).DefaultIfEmpty(0f).Max();
                t.full[c] = Math.Max(t.full[c], full);
                t.word[c] = Math.Max(t.word[c], word);
                if (i == 0)
                {
                    if (c > 0) t.label = Math.Max(t.label, full);
                    continue;
                }
                t.typical[c] += full;
                counted[c]++;
                if (c > 0) t.valueWord = Math.Max(t.valueWord, word);
            }
        for (int c = 0; c < cols; c++) t.typical[c] /= Math.Max(1, counted[c]);
        return t;
    }

    public static int Run(string root, Action<bool, string> check)
    {
        int checks = 0;
        void Check(bool ok, string what)
        {
            checks++;
            check(ok, "almanac layout: " + what);
        }

        // ---------- Text sizes: Normal is the game's own, each step larger, each a different size of the small font ----------
        var sizes = (AlmanacTextSize[])Enum.GetValues(typeof(AlmanacTextSize));
        Check(sizes.Length == 4, "four text sizes");
        Check(AlmanacLayoutMath.Scale(AlmanacTextSize.Normal) == 1f, "Normal is the game's own size");
        Check(AlmanacLayoutMath.FontSize(13, 1f) == 13 && AlmanacLayoutMath.FontSize(10, 1f) == 10, "Normal keeps the fonts' sizes");
        for (int i = 1; i < sizes.Length; i++)
        {
            Check(AlmanacLayoutMath.Scale(sizes[i]) > AlmanacLayoutMath.Scale(sizes[i - 1]), $"{sizes[i]} is larger than {sizes[i - 1]}");
            foreach (int font in new[] { 10, 13, 18 })
                Check(AlmanacLayoutMath.FontSize(font, AlmanacLayoutMath.Scale(sizes[i])) > AlmanacLayoutMath.FontSize(font, AlmanacLayoutMath.Scale(sizes[i - 1])),
                    $"a {font}-point font is drawn larger at {sizes[i]} than at {sizes[i - 1]}");
        }
        Check(sizes.All(s => AlmanacLayoutMath.Scale(s) >= 0.8f && AlmanacLayoutMath.Scale(s) <= 1.5f), "no size is tiny or huge");

        // ---------- The three columns ----------
        var screens = new (float w, float h)[] { (1366f, 768f), (1920f, 1080f) };
        float[] scales = { 1f, 1.25f, 1.5f, 1.75f, 2f };
        var tabs = new List<(string at, float tab)>();
        foreach (var (pw, ph) in screens)
        foreach (var scale in scales)
        {
            string at = $"{pw}x{ph} at {scale}x";
            float tab = AlmanacLayoutMath.TabWidth(pw / scale);
            tabs.Add((at, tab));
            foreach (bool folded in new[] { false, true })
            {
                AlmanacLayoutMath.Columns(tab, folded, out float cat, out float list, out float page);
                string how = at + (folded ? ", folded" : "");
                float used = folded ? cat + AlmanacLayoutMath.PageGap + page : cat + AlmanacLayoutMath.CategoryGap + list + AlmanacLayoutMath.PageGap + page;
                Check(Math.Abs(used - tab) < 0.01f, $"{how}: the columns fill the tab ({used:0.#} of {tab:0.#})");
                Check(cat > 0f && page > 0f && list >= 0f, $"{how}: every column has a width");
                if (folded) Check(page >= tab - 45f, $"{how}: folded, the page gets nearly all of the tab ({page:0} of {tab:0})");
                else
                {
                    Check(cat >= AlmanacLayoutMath.CategoryMin && list >= AlmanacLayoutMath.ListMin, $"{how}: the categories and the list stay readable");
                    Check(page >= 280f, $"{how}: the page is at least 280 wide ({page:0})");
                    // Before: the categories took up to 190, the list up to 290.
                    float before = tab - Math.Min(190f, tab * 0.22f) - 8f - Math.Min(290f, tab * 0.3f) - 10f;
                    Check(page > before + (tab >= 700f ? 60f : 15f), $"{how}: the page is wider than before ({page:0}, was {before:0})");
                    if (tab >= 940f) Check(page >= 530f && page > tab * 0.55f, $"{how}: on a full-size window the page gets most of it ({page:0})");
                }
            }
        }

        // ---------- Every table in the Almanac ----------
        var tables = new List<(string page, List<string> rows)>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "AlmanacArticleDefs"), "*.xml"))
            foreach (var def in XDocument.Load(file).Root!.Elements())
            {
                string name = def.Element("defName")?.Value ?? Path.GetFileName(file);
                foreach (var table in def.Descendants("table"))
                {
                    var rows = table.Elements("li").Select(li => li.Value).ToList();
                    if (rows.Count > 0) tables.Add((name, rows));
                }
            }
        Check(tables.Count >= 100, $"found the Almanac's tables ({tables.Count})");

        var cardsAt = new Dictionary<string, int>();
        foreach (var size in sizes)
        {
            float scale = AlmanacLayoutMath.Scale(size);
            float charWidth = Letter * scale;
            var measured = tables.Select(t => Measure(t.page, t.rows, scale)).ToList();
            foreach (var (at, tab) in tabs)
            foreach (bool folded in new[] { false, true })
            {
                AlmanacLayoutMath.Columns(tab, folded, out _, out _, out float page);
                float w = AlmanacLayoutMath.TextWidth(page);
                string how = $"{at}{(folded ? ", folded" : "")}, {size}";
                int cards = 0, broken = 0, tooWide = 0, cardWords = 0;
                string firstBroken = null;
                foreach (var t in measured)
                {
                    int cols = t.full.Length;
                    var widths = new float[cols];
                    var mode = AlmanacLayoutMath.Widths(t.word, t.full, t.typical, charWidth, w, widths);
                    if (mode == TableMode.Cards && (cols < 2 || t.cells.Length < 2))
                    {
                        // As the panel does: a table of one column, or a lone header row, shares the page evenly.
                        mode = TableMode.Shared;
                        for (int c = 0; c < cols; c++) widths[c] = w / cols;
                    }
                    if (mode == TableMode.Cards)
                    {
                        cards++;
                        // In a card the values sit beside their labels only where their longest word fits; else on one line.
                        float inside = w - AlmanacLayoutMath.CardPad;
                        bool beside = AlmanacLayoutMath.LabelsBeside(w, t.label, t.valueWord);
                        float room = beside ? inside - t.label - AlmanacLayoutMath.LabelGap : inside;
                        if (t.valueWord + AlmanacLayoutMath.WordSlack > room || t.word[0] > inside) cardWords++;
                        continue;
                    }
                    float sum = widths.Sum();
                    if (sum > w + 0.5f) tooWide++;
                    for (int c = 0; c < cols; c++)
                    {
                        if (widths[c] + 0.01f < t.word[c] + AlmanacLayoutMath.WordSlack + AlmanacLayoutMath.CellPad)
                        {
                            broken++;
                            firstBroken ??= $"{t.page} column {c + 1}";
                        }
                        if (mode == TableMode.Shared && AlmanacLayoutMath.Squeezed(t.typical[c], widths[c], charWidth)) broken++;
                    }
                    if (mode == TableMode.Natural) Check(Enumerable.Range(0, cols).All(c => widths[c] >= t.full[c] + AlmanacLayoutMath.CellPad - 0.01f), $"{how}: {t.page}: nothing wraps");
                }
                Check(broken == 0, $"{how}: no table breaks a word or squeezes a column ({broken}, first {firstBroken})");
                Check(tooWide == 0, $"{how}: no table is wider than the page ({tooWide})");
                Check(cardWords == 0, $"{how}: every card's words fit ({cardWords})");
                cardsAt[how] = cards;
            }

            // The ailments page's four-column table, which broke "What helps" into "Wha / help / s".
            var ailments = measured.FirstOrDefault(t => t.page == "Council_Alm_4_25_Ailments" && t.cells[0].Length == 4 && t.cells[0][0] == "Ailment");
            Check(ailments != null, "found the ailments table");
            if (ailments == null) continue;
            var aw = new float[4];
            AlmanacLayoutMath.Columns(950f, false, out _, out _, out float fullPage);
            var normalMode = AlmanacLayoutMath.Widths(ailments.word, ailments.full, ailments.typical, charWidth, AlmanacLayoutMath.TextWidth(fullPage), aw);
            if (size <= AlmanacTextSize.Normal) Check(normalMode == TableMode.Shared, $"{size}: the ailments table is a table on a full-size window ({normalMode})");
            if (normalMode != TableMode.Cards)
                Check(aw[3] >= ailments.word[3] + AlmanacLayoutMath.CellPad && aw[1] > aw[0] && aw[1] > aw[2], $"{size}: \"What helps\" keeps its words, \"Comes from\" gets the most room");
            AlmanacLayoutMath.Columns(950f, true, out _, out _, out float foldedPage);
            Check(AlmanacLayoutMath.Widths(ailments.word, ailments.full, ailments.typical, charWidth, AlmanacLayoutMath.TextWidth(foldedPage), aw) == TableMode.Shared,
                $"{size}: folded, the ailments table is a table");
            AlmanacLayoutMath.Columns(AlmanacLayoutMath.TabWidth(1366f / 2f), false, out _, out _, out float smallPage);
            Check(AlmanacLayoutMath.Widths(ailments.word, ailments.full, ailments.typical, charWidth, AlmanacLayoutMath.TextWidth(smallPage), aw) == TableMode.Cards,
                $"{size}: on a 768 pixel screen at 2x, the ailments table turns into cards");
        }

        // Most tables stay tables where there's room: cards are for the few wide ones.
        string normal = "1920x1080 at 1x, Normal";
        Check(cardsAt[normal] * 10 <= tables.Count, $"at 1080p and Normal size, at most one table in ten turns into cards ({cardsAt[normal]} of {tables.Count})");
        Check(cardsAt["1920x1080 at 1x, folded, Normal"] * 40 <= tables.Count, $"folded, almost every table is a table ({cardsAt["1920x1080 at 1x, folded, Normal"]})");
        foreach (var at in tabs.Select(t => t.at))
            Check(cardsAt[at + ", folded, Normal"] <= cardsAt[at + ", Normal"] && cardsAt[at + ", Normal"] <= cardsAt[at + ", Larger"],
                $"{at}: more room means fewer cards, larger text more");

        // ---------- The rules on small cases ----------
        float[] word = { 50f, 60f }, full = { 80f, 900f }, typical = { 60f, 600f };
        var ws = new float[2];
        Check(AlmanacLayoutMath.Widths(word, full, typical, 7f, 2000f, ws) == TableMode.Natural && ws[1] == 910f, "a table that fits keeps its natural widths");
        Check(AlmanacLayoutMath.Widths(word, full, typical, 7f, 500f, ws) == TableMode.Shared && Math.Abs(ws.Sum() - 500f) < 0.5f && ws[0] == 90f,
            "a short first column keeps its width, the long one takes the rest");
        Check(AlmanacLayoutMath.Widths(new[] { 300f, 300f }, new[] { 400f, 400f }, new[] { 350f, 350f }, 7f, 500f, ws) == TableMode.Cards,
            "two long words side by side on a narrow page: cards");
        Check(AlmanacLayoutMath.LabelsBeside(500f, 80f, 90f) && !AlmanacLayoutMath.LabelsBeside(200f, 80f, 90f), "labels beside their values only where they leave room");
        return checks;
    }
}
