using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The on-screen panels in Rimfolk's own look (docs/design/rimfolk-look.md, stage 3, point 3; Source/ColonyCouncil/Hud/Look/):
/// every text colour at 4.5:1 or better on the card where the map shows through it (bright snow under the dark and darker
/// looks, the game's soil under the clear one), on a row's washes too; the cards, their title rows included, starting clear
/// of one another and of the game's own interface where Rimfolk places them at 1080p and 1440p; a colonist's row laid out
/// at every scale with nothing on top of anything else; the bars' shares; the card's solidity; the clock and its timing
/// run; and the seams (the setting and its words, the classic drawing kept and branched from, the look decided when a
/// panel gathers, the layout pass skipped, nothing made while drawing, the weapon kit read from the kept choice only,
/// the icons, saved layouts untouched).
/// </summary>
public static class HudLookTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the on-screen panels in Rimfolk's look: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Raw(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string Code(string rel) => Regex.Replace(Raw(rel), @"//[^\n]*", "");

        // ---------------- Contrast on the card over the map ----------------

        var inks = new (string name, uint rgb)[]
        {
            ("the ink", SkinMath.Ink), ("the faint ink", SkinMath.Faint), ("the heading", SkinMath.Heading), ("a link", SkinMath.Link),
            ("a link under the mouse", SkinMath.LinkHot), ("the accent", SkinMath.Accent), ("good", SkinMath.Ally), ("bad", SkinMath.Foe),
            ("soon", SkinMath.Soon), ("danger", SkinMath.Danger), ("cool (a day off)", SkinMath.Cool), ("done", SkinMath.Done)
        };
        foreach (var theme in new[] { HudTheme.Dark, HudTheme.Darker, HudTheme.Clear })
        {
            uint ground = theme == HudTheme.Clear ? HudLookMath.Soil : HudLookMath.Snow;
            string over = theme == HudTheme.Clear ? "soil" : "snow";
            foreach (bool hovered in new[] { false, true })
            {
                uint card = HudLookMath.Over(HudLookMath.CardColour(theme), HudLookMath.CardAlpha(theme, 1f, hovered), ground);
                foreach (var (name, rgb) in inks)
                    C(SkinMath.Contrast(rgb, card) >= SkinMath.TextContrast,
                        $"{name} on the {theme} card over {over}{(hovered ? ", under the mouse" : "")}: {SkinMath.Contrast(rgb, card):0.00}:1");
                // The row under the mouse (the card is fuller then, the mouse being on it): the look's raised hover over it.
                if (!hovered) continue;
                uint lit = HudLookMath.Over(SkinMath.RaisedHover, 0.55f, card);
                foreach (var (name, rgb) in inks)
                    C(SkinMath.Contrast(rgb, lit) >= SkinMath.TextContrast, $"{name} on the row under the mouse, {theme} card over {over}: {SkinMath.Contrast(rgb, lit):0.00}:1");
            }
            C(SkinMath.Contrast(SkinMath.Ink, HudLookMath.Over(HudLookMath.CardColour(theme), HudLookMath.CardAlpha(theme, 1f, false), HudLookMath.Soil)) >= 7.0,
                $"the {theme} card over the game's soil keeps the ink at 7:1 or better");
        }
        // A compact cell's initials on the mood's fill (at its strongest, under the major line's pulse) over the dark track.
        foreach (var (name, rgb) in new[] { ("good", SkinMath.Ally), ("soon", SkinMath.Soon), ("bad", SkinMath.Foe), ("danger", SkinMath.Danger) })
        {
            uint cell = HudLookMath.Over(rgb, 0.45f, SkinMath.WindowGround);
            C(SkinMath.Contrast(SkinMath.Ink, cell) >= SkinMath.TextContrast, $"a compact cell's initials on the {name} fill: {SkinMath.Contrast(SkinMath.Ink, cell):0.00}:1");
        }
        C(Code("Hud/Look/HudMoodBoardLook.cs").Contains("float a = h.now < h.major ? 0.3f + 0.15f * HudKit.Pulse : 0.4f;"), "the compact cells' fill is light enough under the initials");
        C(Code("Hud/Look/HudWorkLook.cs").Contains("HudLook.Label(chip, mark, UiKit.Ink, GameFont.Tiny, TextAnchor.MiddleCenter);"), "a work mark's word in ink on its chip");
        C(HudLookMath.Over(0xffffff, 0.5f, 0x000000) == 0x808080 && HudLookMath.Over(0x123456, 1f, 0xffffff) == 0x123456 && HudLookMath.Over(0x123456, 0f, 0xabcdef) == 0xabcdef,
            "a colour laid over a ground mixes as the eye sees it");
        C(Code("Hud/HudLook.cs").Contains("public static Color MoodInk(") && Code("Hud/Look/HudMoodBoardLook.cs").Contains("HudLook.MoodInk(")
          && Code("Hud/Look/HudHoverCardLook.cs").Contains("HudLook.MoodInk("),
            "a mood said in words is in the bar's colour at full strength (the bar's own good colour is see-through, too faint for words)");

        // ---------------- The card's solidity ----------------

        C(HudLookMath.CardAlpha(HudTheme.Darker) > HudLookMath.CardAlpha(HudTheme.Dark) && HudLookMath.CardAlpha(HudTheme.Dark) > HudLookMath.CardAlpha(HudTheme.Clear),
            "darker is more solid than dark, dark than clear");
        foreach (var theme in new[] { HudTheme.Dark, HudTheme.Darker, HudTheme.Clear })
        {
            float full = HudLookMath.CardAlpha(theme, 1f, false), faint = HudLookMath.CardAlpha(theme, 0.25f, false), lit = HudLookMath.CardAlpha(theme, 0.25f, true);
            C(Math.Abs(full - HudLookMath.CardAlpha(theme)) < 1e-5f && Math.Abs(faint - full * 0.25f) < 1e-5f, $"{theme}: the panel's own opacity makes it fainter");
            C(lit >= full && lit <= 0.97f, $"{theme}: fuller while the mouse is on it, never quite solid");
            C(HudLookMath.CardAlpha(theme, 5f, false) <= full + 1e-5f && HudLookMath.CardAlpha(theme, -1f, false) == 0f, $"{theme}: opacity kept between nothing and all");
        }
        C(HudLookMath.CardColour(HudTheme.Darker) == SkinMath.WindowGround && HudLookMath.CardColour(HudTheme.Dark) == SkinMath.Panel
          && HudLookMath.CardColour(HudTheme.Clear) == SkinMath.WindowGround, "the card in the look's own palette");
        string lookCode = Code("Hud/HudLook.cs");
        C(!lookCode.Contains("Wash(") && !Directory.GetFiles(Path.Combine(src, "Hud", "Look"), "*.cs").Any(f => File.ReadAllText(f).Contains("Wash(")),
            "an urgent row has no wash over its words (the danger colour's words would fall under 4.5:1 on a reddened row): its stripe, pulsing for life and limb, and its icon say it");
        C(Regex.IsMatch(lookCode, @"if \(level == HudLevel\.Life\) c = UiKit\.WithAlpha\(c, 0\.6f \+ 0\.4f \* HudKit\.Pulse\);"), "life and limb's stripe pulses");

        // ---------------- The cards start clear of one another ----------------

        foreach (var (sw, sh) in new[] { (1920f, 1080f), (2560f, 1440f) })
        {
            const float s = 1f;
            float title = HudLookMath.At(HudLookMath.TitleHeight, s);
            // The same typical full sizes as the classic panels' test (HudPanelTests), drawn in the look, each with its title row.
            var sizes = new Dictionary<string, (float w, float h)>
            {
                [HudIds.Moods] = (226f, HudLookMath.MoodsHeight(10, true, s) + title),
                [HudIds.Trend] = (224f, 100f + title),
                [HudIds.Vitals] = (232f, HudLookMath.LinesHeight(6, false, false, s) + title),
                [HudIds.Council] = (244f, HudLookMath.LinesHeight(6, true, false, s) + title),
                [HudIds.Alerts] = (248f, HudLookMath.AlertsHeight(8, true, false, s) + title),
                [HudIds.Health] = (252f, 8f + 4 * HudLookMath.HealthRow(true, 1, s) + 16f + title),
                [HudIds.Work] = (256f, HudLookMath.WorkHeight(3, 10, true, s) + title),
                [HudIds.Inventory] = (126f, 8f + 25 * 20f + 16f + title),
                [HudIds.Following] = (260f, HudLookMath.LinesHeight(8, true, false, s) + title)
            };
            float letters = sh - 262f - 3 * 42f;
            var boxes = new Dictionary<string, HudBox>();
            foreach (var id in HudIds.All)
            {
                HudMath.DefaultPlace(id, sw, sh, out float fx, out float fy, out bool right, out bool bottom, out bool auto);
                var (w, h) = sizes[id];
                boxes[id] = auto ? HudMath.AlertsBox(w, h, sw, sh, letters, 0f) : HudMath.FromFraction(fx, fy, right, bottom, w, h, sw, sh);
                var b = boxes[id];
                C(b.x >= 0f && b.y >= 0f && b.XMax <= sw + 0.01f && b.YMax <= sh + 0.01f, $"{id}'s card starts on a {sw}x{sh} screen");
            }
            var ids = HudIds.All;
            for (int i = 0; i < ids.Length; i++)
            for (int j = i + 1; j < ids.Length; j++)
                C(!HudMath.Overlap(boxes[ids[i]], boxes[ids[j]]), $"{ids[i]} and {ids[j]} start clear of each other in the look ({sw}x{sh})");
            C(boxes[HudIds.Alerts].YMax <= letters, $"the alerts' card starts above the letters ({sw}x{sh})");
            var bar = new HudBox(260f, 0f, sw - 520f, 140f);
            foreach (var id in ids) C(!HudMath.Overlap(boxes[id], bar), $"{id}'s card starts clear of the colonist bar ({sw}x{sh})");
            foreach (var id in new[] { HudIds.Moods, HudIds.Trend })
            {
                C(boxes[id].x >= HudMath.ResourceListWidth && !HudMath.Overlap(boxes[id], boxes[HudIds.Inventory]), $"{id}'s card starts clear of the inventory ({sw}x{sh})");
                C(boxes[id].YMax <= sh - 35f - 165f - 30f, $"{id}'s card starts above the inspect pane's tab row ({sw}x{sh})");
            }
        }
        C(HudLookMath.LinesHeight(6, false, false, 1f) + HudLookMath.At(HudLookMath.TitleHeight, 1f) <= 8f + 6 * 20f + 8f,
            "six vitals in the look, title row and all, are no more than eight taller than the classic six");

        // ---------------- A colonist's row at every scale ----------------

        foreach (float s in new[] { 0.75f, 1f, 1.25f, 1.5f })
        foreach (bool portrait in new[] { true, false })
        foreach (bool act in new[] { false, true })
        foreach (bool drag in new[] { true, false })
        foreach (int marks in new[] { 0, 1, 3, 6 })
        {
            float width = (float)Math.Round((226f - (portrait ? 0f : 30f) + (act ? 130f : 0f)) * s);
            const float number = 26f;
            var l = HudLookMath.LayMoodRow(width, s, portrait, marks, drag, number);
            float rowH = HudLookMath.At(HudLookMath.MoodRow, s);
            string at = $"(scale {s}, {(portrait ? "portrait" : "no portrait")}, {(drag ? "what drags them" : "no words after")}, {marks} marks, {width} wide)";
            C(l.nameX >= HudLookMath.At(HudLookMath.Stripe, s) + 1f, $"the row starts clear of the urgent stripe {at}");
            C(!portrait || l.portraitY >= 0f && l.portraitY + l.portraitSize <= rowH && l.portraitX + l.portraitSize < l.nameX, $"the portrait sits in the row, before the name {at}");
            C(l.nameY + l.nameH <= l.barY, $"the name's line is above the mood bar {at}");
            C(l.nameRight - l.nameX >= (marks <= 3 ? 50f * s : 12f), $"the name keeps room beside its marks {at}");
            C(l.barW >= HudLookMath.At(40f, s) && l.barX + l.barW + number <= width, $"the mood bar is at least forty wide and its number fits {at}");
            C(l.heartY >= l.barY + l.barH && l.heartY + l.heartH <= rowH - 1f, $"the heart's line is under the bar and inside the row {at}");
            C(!drag || l.dragX >= l.numberX + number && l.dragX + l.dragW <= width, $"what drags them down comes after the number, inside the row {at}");
            C(l.portraitSize == 0f || l.portraitSize >= 18f, $"the portrait stays large enough to tell faces apart {at}");
        }

        // ---------------- Bars for values with a range ----------------

        C(HudLookMath.FoodFill(10f, 10f) == 0.5f && HudLookMath.FoodFill(4f, 10f) == 0.2f && HudLookMath.FoodFill(25f, 10f) == 1f && HudLookMath.FoodFill(0f, 10f) == 0f,
            "days of food: the warning line at the middle, full from twice it");
        C(HudLookMath.FoodLine(4f, 10f) < HudLookMath.FoodLine(10f, 10f), "the bad line comes before the warning line");
        C(HudLookMath.FoodFill(3f, 0f) == 1f && HudLookMath.FoodFill(float.NaN, 10f) == 0f, "no warning line, or nothing known, still draws");
        C(HudLookMath.StockFill(12, 12) == 0.5f && HudLookMath.StockFill(30, 12) == 1f && HudLookMath.StockFill(0, 12) == 0f && HudLookMath.StockFill(3, 0) == 1f,
            "medicine: the advisor's line at the middle");
        HudLookMath.TrustBar(0f, out float f0, out float t0);
        HudLookMath.TrustBar(-100f, out float f1, out float t1);
        HudLookMath.TrustBar(60f, out float f2, out float t2);
        HudLookMath.TrustBar(400f, out float f3, out float t3);
        C(f0 == 0.5f && t0 == 0.5f && f1 == 0f && t1 == 0.5f && f2 == 0.5f && Math.Abs(t2 - 0.8f) < 1e-5f && t3 == 1f, "trust either side of nothing: distrust leftwards, trust rightwards");

        // ---------------- The clock ----------------

        var clock = new HudLookMath.Clock();
        C(clock.PerFrame(1000000) == 0.0 && clock.PerPanel(1000000) == 0.0, "an empty clock says nothing");
        clock.Add(1200, 4);
        clock.Add(1800, 5);
        clock.Frame();
        clock.Frame();
        clock.Frame();
        C(Math.Abs(clock.PerFrame(1000000) - 1000.0) < 1e-9 && Math.Abs(clock.PerPanel(1000000) - 3000.0 / 9.0) < 1e-9, "microseconds a frame and a panel");
        clock.Clear();
        C(clock.frames == 0 && clock.ticks == 0 && clock.panels == 0, "a timing run starts the clock again");
        C(HudLookMath.MeasureLook(0) == true && HudLookMath.MeasureLook(HudLookMath.MeasureFrames - 1) == true && HudLookMath.MeasureLook(HudLookMath.MeasureFrames) == false
          && HudLookMath.MeasureLook(HudLookMath.MeasureFrames * 2 - 1) == false && HudLookMath.MeasureLook(HudLookMath.MeasureFrames * 2) == null,
            "a timing run: refined first, then as many frames classic, then over");

        // ---------------- The setting and its words ----------------

        string settings = Code("CouncilSettings.cs");
        C(settings.Contains("public bool hudClassic = false;") && settings.Contains("Scribe_Values.Look(ref hudClassic, \"hudClassic\", false);"), "\"Classic on-screen panels\": off at first, kept in the settings");
        C(Code("UI/SettingsCatalog.cs").Contains("Toggle(\"hudClassic\", \"CC_Settings_HudClassic\", \"CC_SetNote_HudClassic\", \"hudShown\");"), "its row under the on-screen panels");
        C(Code("Hud/HudLayout.cs").Contains("s.hudClassic = d.hudClassic;"), "putting the panels back as Rimfolk ships them puts it back too");
        C(Regex.IsMatch(Code("Hud/HudLook.cs"), @"return s != null && s\.ownLook && !s\.hudClassic;"), "the look only with Rimfolk's own look on and the classic panels off");
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_HudLook.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (var key in new[] { "CC_Settings_HudClassic", "CC_Settings_HudClassic_Tip", "CC_SetNote_HudClassic", "CC_Pnl_Look_DayOff", "CC_Pnl_Look_BadDay", "CC_Pnl_Look_KitTip",
                     "CC_Pnl_Look_Avg", "CC_Pnl_Look_Low", "CC_Pnl_Look_RatingTip" })
            C(keyed.ContainsKey(key) && !keyed[key].Contains("—") && !keyed[key].Contains("!"), $"{key} is written, plainly");
        C(keyed["CC_Pnl_Look_KitTip"].Contains("{GAP}") && keyed["CC_Pnl_Look_RatingTip"].Contains("{RATING}"), "the marks' words name what they are about");
        var allCode = string.Join("\n", Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains("/bin/") && !f.Contains("/obj/")).Select(File.ReadAllText));
        foreach (var key in keyed.Keys.Where(k => k.StartsWith("CC_Pnl_Look_")))
            C(allCode.Contains("\"" + key + "\""), $"{key} is used");

        // ---------------- The classic drawing kept, the look branched from it ----------------

        var panels = new (string file, string look, string classic)[]
        {
            ("Hud/HudMoodBoard.cs", "Hud/Look/HudMoodBoardLook.cs", "HudKit.MoodBar(new Rect(x, by, barW, bh), h.now, h.heading, h.minor, h.major, h.extreme);"),
            ("Hud/HudLines.cs", "Hud/Look/HudLinesLook.cs", "if (l.icon != null) UiKit.Icon(new Rect(x, row.y + (rh - icon) / 2f, icon, icon), l.icon,"),
            ("Hud/HudAlerts.cs", "Hud/Look/HudAlertsLook.cs", "Widgets.DrawBoxSolid(new Rect(rr.x, rr.y + 2f, 2f, rr.height - 4f), row.priority >= 1 ? c : UiKit.WithAlpha(Color.white, 0.25f));"),
            ("Hud/HudHealth.cs", "Hud/Look/HudHealthLook.cs", "float nx = row.visual && row.health >= 0f ? DrawHealthMark(x, topY, topH, row, s) : x;"),
            ("Hud/HudWork.cs", "Hud/Look/HudWorkLook.cs", "right = Bars(rr, right, row, s, st);"),
            ("Hud/HudInventory.cs", "Hud/Look/HudInventoryLook.cs", "HudKit.Label(low, HudInventoryMath.ShortCount(it.e.count), it.e.count > 0 ? Color.white : UiKit.Dim, GameFont.Tiny, TextAnchor.MiddleCenter);"),
            ("Hud/HudMoodTrend.cs", "Hud/Look/HudMoodTrendLook.cs", "for (float x = graph.x; x < graph.xMax; x += 7f)")
        };
        foreach (var (file, lookFile, classic) in panels)
        {
            string code = Code(file);
            C(Regex.IsMatch(code, @"protected override void DrawBody\(Rect r, float s\)\s*\{\s*if \(Refined\)\s*\{\s*DrawLook\(r, s\);\s*return;\s*\}"), $"{file}: its drawing branches to the look first");
            C(code.Contains(classic), $"{file}: its classic drawing kept as it was");
            C(Regex.IsMatch(Code(lookFile), @"private void DrawLook\(Rect r, float s\)"), $"{lookFile}: the look's drawing");
            C(Regex.IsMatch(code, @"public (abstract )?partial class"), $"{file}: one class in two files, the classic and the look");
        }
        string panel = Code("Hud/HudPanel.cs");
        C(panel.Contains("HudKit.Panel(bodyLocal, St.opacity, hovered || dragging);") && panel.Contains("if (showHeader) DrawHeader(new Rect(0f, headerAbove ? 0f : body.height, body.width, hh));"),
            "the classic panel's background and bar kept");
        C(Regex.IsMatch(panel, @"private void Contents\(bool paint\)\s*\{\s*if \(look\)\s*\{\s*ContentsLook\(paint\);\s*return;\s*\}"), "a panel's contents branch to the look first");
        C(Regex.IsMatch(panel, @"look = HudLook\.Refined;\s*inline = look && !St\.compact;[\s\S]*?Refresh\(\);\s*size = Measure\("),
            "the look is decided when a panel gathers, so its size and its drawing always agree");
        C(panel.Contains("if (inline) size.y += TitleH;") && panel.Contains("if (!showHeader || inline) frame = body;"), "a full card's title row is inside it: nothing pops out over the panel");
        C(panel.Contains("if (look && cur.type == EventType.Layout)"), "in the look the window skips the layout pass (the classic drew everything for it too)");
        C(panel.Contains("long clock = HudLook.Start();") && panel.Contains("HudLook.Painted(look, clock);") && Code("Hud/HudPaint.cs").Contains("HudLook.Frame(frames.Count > 0);"),
            "every panel's painting is timed for the look it was drawn in");
        C(Regex.IsMatch(Code("Hud/HudHoverCard.cs"), @"if \(HudLook\.Refined\) return card = CardLook\(p, now\);") && Code("Hud/HudHoverCard.cs").Contains("c.draw ??= () => DrawLook(c, c.drawRect);"),
            "the hover card: the look's card built every half second, its drawing made once a card");
        C(Code("Hud/HudManager.cs").Contains("HudLook.Watch(windows.Values);"), "a change of look makes every panel gather and measure itself again");
        var saved = Regex.Matches(Code("Hud/HudPanelState.cs"), @"Scribe_(?:Values|Collections)\.Look\(ref \w+, ""(\w+)""").Select(m => m.Groups[1].Value).ToList();
        C(saved.SequenceEqual(new[] { "id", "on", "x", "y", "right", "bottom", "compact", "scale", "opacity", "maxRows", "who", "sort", "days", "hidden", "style", "filter", "columns", "pins",
                "folds", "alertsHidden", "alertsShown" }), $"a panel's saved state is the same in either look (the inventory's filter added after, with a default that changes nothing): saved layouts stay as they are ({string.Join(", ", saved)})");

        // ---------------- Nothing made while drawing ----------------

        var making = new Regex(@"\.Translate\(|\.ToString(Percent)?\(|string\.Format|\$""|""\s*\+|\+\s*""|\(\)\s*=>|new (List|Dictionary|HashSet|StringBuilder)\b|\.(Select|Where|ToList|ToArray|Any|OrderBy|OrderByDescending)\(|\.Resolve\(\)|\.Named\(");
        var gathering = new HashSet<string> { "RefreshLook", "CardLook", "Of", "HeartFacts", "Kit", "Rating", "CountText", "Percent", "MeasureLook", "HeightLook", "TitleButtons" };
        int drawn = 0;
        foreach (var f in Directory.GetFiles(Path.Combine(src, "Hud", "Look"), "*.cs"))
        {
            string code = Regex.Replace(File.ReadAllText(f), @"//[^\n]*", "");
            foreach (Match m in Regex.Matches(code, @"(?:private|protected|public)[^\n;=(]*?\b(\w+)\s*\([^)]*\)\s*\{"))
            {
                string name = m.Groups[1].Value;
                if (gathering.Contains(name)) continue;
                int start = m.Index + m.Length, depth = 1, i = start;
                while (i < code.Length && depth > 0)
                {
                    if (code[i] == '{') depth++;
                    else if (code[i] == '}') depth--;
                    i++;
                }
                string body = code.Substring(start, i - start);
                var hit = making.Match(body);
                drawn++;
                C(!hit.Success, $"{Path.GetFileName(f)}: {name} makes nothing while drawing{(hit.Success ? " (" + hit.Value + ")" : "")}");
            }
        }
        C(drawn >= 25, $"the look's drawing methods are read ({drawn})");
        string titleRow = Code("Hud/Look/HudPanelLook.cs");
        C(titleRow.Contains("if (opacityText == null || opacityOf != st.opacity)"), "the background's words made only when it changes");
        string look = Code("Hud/HudLook.cs");
        foreach (var part in new[] { "public static void Label(", "public static void Bar(", "public static void MoodBar(", "public static void HeartLine(", "public static void Card(",
                     "public static void Stripe(Rect row, HudLevel level", "public static void Badge(" })
        {
            int at = look.IndexOf(part, StringComparison.Ordinal);
            C(at >= 0 && Regex.IsMatch(look.Substring(at, Math.Min(400, look.Length - at)), @"Event\.current\.type != EventType\.Repaint"), $"{part.TrimEnd('(')}: drawn on repaint only");
        }
        C(look.Contains("if (d.TryGetValue(text, out float w)) return w;"), "a text is measured once and kept");

        // ---------------- The marks ----------------

        string facts = Code("Hud/Look/HudLookFacts.cs");
        C(facts.Contains("WeaponPick.Current(map)") && !facts.Contains("WeaponPick.For(") && facts.Contains("if (k.kitMade != picks.made)"),
            "an empty weapon slot read from the colony's kept choice, again only when it was made again (never made here)");
        C(facts.Contains("KitState.Missing") && facts.Contains("HeartTimeOff.KindOf(heart)") && facts.Contains("HeartTimeOff.UntilText("), "the kit's gaps, time off under way and when it ends");
        string board = Code("Hud/Look/HudMoodBoardLook.cs");
        foreach (var mark in new[] { "Icons.Warning, UiKit.Danger", "Icons.Heart, HeartView.BandColor(h.band)", "HudIcons.DayOff", "HudIcons.KitGap", "Icons.Wishes", "Icons.CatIdeology" })
            C(board.Contains(mark), $"the mood board's row has its mark: {mark}");
        C(board.Contains("HudLook.HeartLine(") && Code("Hud/Look/HudHoverCardLook.cs").Contains("HudLook.HeartLine("), "the heart's line on a colonist's row and on the hover card");
        C(look.Contains("HeartMeterMath.StrainTicks") && look.Contains("HeartView.BandColor(band)") && look.Contains("HeartView.GoodColor"), "in the heart's meters' colours and at their marks");
        C(Code("Hud/Look/HudInventoryLook.cs").Contains("HudLook.Badge(badge, it.ratingColor, it.ratingText);") && Code("Hud/Look/HudInventoryLook.cs").Contains("WeaponUi.RatingColour(kept.rating)"),
            "the inventory's weapons carry the rating's badge in its band's colour");
        string inv = Code("Hud/HudInventory.cs");
        string invLook = Code("Hud/Look/HudInventoryLook.cs");
        C(invLook.Contains("bool weapon = BadgeFor(def) && WeaponLive.On;") && invLook.Contains("bool apparel = !weapon && StoredApparelKind(def)")
          && inv.Contains("(!def.CountAsResource || (ThingCategoryDefOf.Weapons"),
            "a rating's badge only on real weapons and apparel, never on wood, a manufactured good or another counted resource a mod lets be swung");
        C(inv.Contains("def.IsWeapon && def.equipmentType == EquipmentType.Primary && !def.CountAsResource") && inv.Contains("def.IsApparel && !def.CountAsResource")
          && inv.Contains("ThingRequestGroup.Apparel : ThingRequestGroup.Weapon") && inv.Contains("t.IsInAnyStorage()"),
            "the weapons and apparel in storage listed from the map's own lists (never a counted resource twice)");
        C(inv.Contains("Kept(st, HudInventoryMath.TailWeapons,") && inv.Contains("Kept(st, HudInventoryMath.TailApparel,") && inv.Contains("PartWeapons, PartApparel")
          && inv.Contains("if (!st.Shows(PartFilter) || Style == StyleEssentials) return HudInventoryMath.FilterAll;"),
            "weapons and apparel are parts that can be hidden; the filter only while its row shows");
        C(inv.Contains("bool known = yesterday != null && yesterday.ContainsKey(HudInventoryMath.Mark(tail));")
          && Code("Hud/HudComponent.cs").Contains("HudInventory.StoredKinds(map, tail, kept);") && Code("Hud/HudComponent.cs").Contains("HudInventoryMath.Mark(tail), 1"),
            "the change since yesterday only from a snapshot that counted them");
        C(Code("Hud/HudPanelState.cs").Contains("Scribe_Values.Look(ref filter, \"filter\", \"\");") && inv.Contains("FilterRow(") && invLook.Contains("FilterRow(r, s, true);"),
            "the filter kept with the panel and its row drawn in both looks");
        C(inv.Contains("cat.icon != BaseContent.BadTex ? cat.icon : null") && inv.Contains("icon = CatIcon(cat),")
          && inv.Contains("icon = CatIcon(wc) ?? StandIn(apparel ? \"Apparel_BasicShirt\" : \"Gun_BoltActionRifle\", it.def)"),
            "no red cross on a group header: a category without its own icon gets none, the weapons and apparel a rifle and a shirt");
        C(Code("Hud/Look/HudHealthLook.cs").Contains("HudLookMath.At(HudLookMath.HealthBarQuiet, s)") && Code("Hud/Look/HudHealthLook.cs").Contains("UiKit.WithAlpha(row.healthColor, HudLookMath.HealthBarAlpha)")
          && Code("Hud/HudHealth.cs").Contains("private static GameFont LineFont(float s) => HudKit.Main(s);") && HudLookMath.HealthBarQuiet < HudLookMath.HealthBar,
            "the health panel: the illness in the main font with a wider bar, the summary health a thin muted line under the name");
        C(Code("Hud/Look/HudWorkLook.cs").Contains("CC_Pnl_Look_DayOff") && Code("Hud/Look/HudWorkLook.cs").Contains("NeedsLook("), "work and idle: time off explains the idling; food, rest and recreation as bars");

        // ---------------- Icons ----------------

        string icons = Code("Hud/HudIcons.cs");
        foreach (var name in new[] { "Trend", "Vitals", "Stock", "DayOff", "KitGap" })
        {
            C(File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Icons", name + ".png")), $"the icon {name} is drawn");
            C(icons.Contains("Load(\"" + name + "\")"), $"and loaded ({name})");
        }
        foreach (var id in new[] { "Moods", "Trend", "Vitals", "Council", "Alerts", "Health", "Work", "Inventory", "Following" })
            C(icons.Contains("case HudIds." + id + ":"), $"the {id} card has its title icon");
        C(look.Contains("GUI.DrawTexture(Snap(r), tex, ScaleMode.ScaleToFit);"), "icons on whole pixels and never stretched");
        C(panel.Contains("if (look) body.position = new Vector2(Mathf.Round(body.x), Mathf.Round(body.y));"), "a card in the look sits on whole pixels");

        // ---------------- The pure rules ----------------

        string math = Raw("Hud/HudLookMath.cs");
        C(!Regex.IsMatch(math, @"using (Verse|RimWorld|UnityEngine)"), "the look's rules have no game types");
        C(File.ReadAllText(Path.Combine(root, "Tools", "Tests", "Tests.csproj")).Contains("Hud/HudLookMath.cs"), "and are linked into the tests");
        C(look.Contains("[DebugAction(\"Rimfolk\", \"On-screen panels: time the classic and refined drawing (log)\""), "a timing run from the debug actions");
        return n;
    }
}
