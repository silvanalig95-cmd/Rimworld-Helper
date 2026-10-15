using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The mod settings window (Source/ColonyCouncil/UI/Settings*.cs) against CouncilSettings itself: every public field is
/// drawn on exactly one page (or listed as not a setting, with the reason), so a new setting can't be forgotten; each
/// field keeps its Scribe key, so saved settings load unchanged; every row's kind fits its field; the presets set only
/// fields the window shows, to values its sliders allow; every key exists; and the window fits small screens at big
/// interface scales.
/// </summary>
public static class SettingsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int checks = 0;
        void Check(bool ok, string what)
        {
            checks++;
            check(ok, "settings: " + what);
        }

        string sourceDir = Path.Combine(root, "Source", "ColonyCouncil");
        string src = File.ReadAllText(Path.Combine(sourceDir, "CouncilSettings.cs"));

        // ---------- The fields of CouncilSettings, with their types and defaults ----------
        var fields = new Dictionary<string, (string type, string def)>();
        var fieldRx = new Regex(@"^\s*public\s+(?!static\b|const\b|override\b|void\b|class\b|enum\b)([\w\.<>]+)\s+(\w+)\s*(=(?!>)\s*([^;]+))?;", RegexOptions.Multiline);
        foreach (Match m in fieldRx.Matches(src))
            fields[m.Groups[2].Value] = (m.Groups[1].Value, m.Groups[4].Success ? m.Groups[4].Value.Trim() : null);
        Check(fields.Count >= 100, $"found the settings' fields ({fields.Count})");

        // Every enum in the mod, with its members, for the choices and presets.
        var enums = new Dictionary<string, HashSet<string>>();
        foreach (var file in Directory.GetFiles(sourceDir, "*.cs", SearchOption.AllDirectories))
        {
            string text = Regex.Replace(File.ReadAllText(file), @"//[^\n]*|/\*.*?\*/", "", RegexOptions.Singleline);
            foreach (Match m in Regex.Matches(text, @"\benum\s+(\w+)\s*\{([^}]*)\}"))
                enums[m.Groups[1].Value] = m.Groups[2].Value.Split(',').Select(v => v.Split('=')[0].Trim()).Where(v => v.Length > 0).ToHashSet();
        }
        bool IsBool(string f) => fields.TryGetValue(f, out var t) && t.type == "bool";
        bool IsNumber(string f) => fields.TryGetValue(f, out var t) && (t.type == "float" || t.type == "int");
        HashSet<string> EnumOf(string f) => fields.TryGetValue(f, out var t) && enums.TryGetValue(t.type, out var e) ? e : null;

        // ---------- Saved settings load unchanged: each field is scribed under its own name ----------
        var scribed = new Dictionary<string, string>();
        foreach (Match m in Regex.Matches(src, @"Scribe_(?:Values|Collections)\.Look\(ref (\w+), ""(\w+)"""))
            scribed[m.Groups[1].Value] = m.Groups[2].Value;
        foreach (var f in fields.Keys)
        {
            Check(scribed.ContainsKey(f), $"{f} is saved");
            if (scribed.TryGetValue(f, out var key)) Check(key == f, $"{f} keeps its Scribe key (saved as {key})");
        }

        // ---------- Every field on exactly one page ----------
        var drawn = SettingsCatalog.DrawnFields.GroupBy(f => f).ToDictionary(g => g.Key, g => g.Count());
        foreach (var f in fields.Keys)
        {
            drawn.TryGetValue(f, out int n);
            bool skipped = SettingsCatalog.NotDrawn.ContainsKey(f);
            Check(skipped ? n == 0 : n == 1, skipped ? $"{f} is listed as not a setting but drawn {n} times" : $"{f} is drawn on exactly one page (drawn {n} times)");
        }
        foreach (var f in drawn.Keys) Check(fields.ContainsKey(f), $"the window draws {f}, which is a field of CouncilSettings");
        foreach (var f in SettingsCatalog.NotDrawn.Keys) Check(fields.ContainsKey(f), $"{f}, listed as not a setting, is a field");

        var all = SettingsCatalog.All;
        Check(all.Select(e => e.id).Distinct().Count() == all.Count, "every row has its own id");
        foreach (var e in all.Where(e => e.notAField)) Check(!fields.ContainsKey(e.id), $"{e.id}: a row that isn't a field doesn't share a field's name");
        foreach (var p in SettingsCatalog.Pages) Check(SettingsCatalog.On(p).Count() >= 3, $"page {p} has settings on it");
        // Each page's headings come in one run each, so a heading is never drawn twice on a page.
        foreach (var p in SettingsCatalog.Pages)
        {
            var runs = new List<string>();
            foreach (var e in SettingsCatalog.On(p))
                if (runs.Count == 0 || runs[^1] != e.section) runs.Add(e.section);
            Check(runs.Count == runs.Distinct().Count(), $"page {p}: each heading in one place");
        }

        // ---------- Each row fits its field ----------
        foreach (var e in all)
        {
            switch (e.kind)
            {
                case SettingKind.Toggle:
                    Check(IsBool(e.id), $"{e.id}: a switch for a bool");
                    break;
                case SettingKind.Slider:
                    Check(IsNumber(e.id), $"{e.id}: a slider for a number");
                    Check(e.min < e.max && e.step > 0f, $"{e.id}: a slider with a range and a step");
                    Check(Math.Abs((e.max - e.min) / e.step - Math.Round((e.max - e.min) / e.step)) < 0.001, $"{e.id}: its range is whole steps");
                    Check(new[] { "%", "0", "0.0", "0.#" }.Contains(e.format), $"{e.id}: a known format ({e.format})");
                    if (fields.TryGetValue(e.id, out var t) && t.def != null)
                    {
                        var v = float.Parse(t.def.TrimEnd('f'), CultureInfo.InvariantCulture);
                        Check(v >= e.min && v <= e.max, $"{e.id}: its default {v} is on the slider ({e.min} to {e.max})");
                        if (t.type == "int") Check(e.step == 1f, $"{e.id}: a whole-number slider steps by one");
                    }
                    break;
                case SettingKind.Choice:
                case SettingKind.Dropdown:
                    Check(e.options != null && e.options.Length >= 2 && e.optionPrefix != null, $"{e.id}: a choice with options");
                    var members = e.IsField ? EnumOf(e.id) : enums.GetValueOrDefault("EraMode");
                    Check(members != null, $"{e.id}: a choice for an enum");
                    if (members != null && e.options != null) Check(members.SetEquals(e.options), $"{e.id}: offers every option, and only those ({string.Join(", ", members)})");
                    break;
                case SettingKind.Action:
                    Check(e.notAField, $"{e.id}: an action isn't a field");
                    break;
            }
            foreach (var term in SettingsCatalog.ParseNeeds(e.needs))
            {
                Check(fields.ContainsKey(term.field), $"{e.id}: needs {term.field}, a field");
                if (term.values == null) Check(IsBool(term.field), $"{e.id}: needs {term.field} switched on or off, a bool");
                else
                {
                    var m = EnumOf(term.field);
                    Check(m != null && term.values.All(m.Contains), $"{e.id}: needs {term.field} to be {string.Join("|", term.values)}, all options of it");
                }
            }
            if (e.Parent != null) Check(SettingsCatalog.Find(e.Parent) != null, $"{e.id}: hangs under {e.Parent}, a row of its own");
        }

        // ---------- Every key exists, and each line is short ----------
        var keyed = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements())
                keyed[el.Name.LocalName] = el.Value;
        void Key(string key, string what)
        {
            Check(key != null && keyed.ContainsKey(key), $"{what}: key {key} exists");
        }
        void Line(string key, string what)
        {
            if (key == null || !keyed.TryGetValue(key, out var text)) return;
            Check(text.Length <= 120 && !text.Contains("\\n"), $"{what}: '{key}' is one short line ({text.Length} characters)");
            Check(!text.Contains('!'), $"{what}: '{key}' has no exclamation mark");
        }
        foreach (var p in SettingsCatalog.Pages)
        {
            Key(SettingsCatalog.PageKey(p), "page " + p);
            Key(SettingsCatalog.PageDescKey(p), "page " + p);
        }
        foreach (var e in all)
        {
            Key(e.label, e.id);
            Key(e.section, e.id);
            Key(SettingsCatalog.SectionNote(e.section) ?? e.section, e.id);
            // Switches and choices keep their tooltips; sliders without one of their own still get one with their default.
            if (e.kind == SettingKind.Toggle || e.kind == SettingKind.Choice || e.kind == SettingKind.Dropdown) Key(e.tip, e.id);
            else if (e.kind == SettingKind.Action && e.tip != null) Key(e.tip, e.id);
            if (e.whyOff != null) Key(e.whyOff, e.id);
            if (e.notePrefix == null)
            {
                Key(e.note, e.id);
                Line(e.note, e.id);
            }
            if (e.options != null)
                foreach (var o in e.options)
                {
                    Key(e.optionPrefix + o, e.id);
                    if (e.notePrefix != null)
                    {
                        Key(e.notePrefix + o, e.id);
                        Line(e.notePrefix + o, e.id);
                    }
                }
        }

        // ---------- The presets ----------
        var presets = SettingsCatalog.Presets;
        Check(presets.Select(p => p.id).Distinct().Count() == presets.Count, "every preset has its own id");
        foreach (var id in new[] { "lightTouch", "storyRich", "demanding", "default" })
            Check(SettingsCatalog.StylePresets.Any(p => p.id == id), $"there's a {id} preset");
        foreach (var p in presets)
        {
            Key(p.label, "preset " + p.id);
            Key(p.tip, "preset " + p.id);
            Check(p.values.Count > 0, $"preset {p.id} sets something");
            Check(p.values.Select(v => v.field).Distinct().Count() == p.values.Count, $"preset {p.id} sets each field once");
            foreach (var v in p.values)
            {
                string what = $"preset {p.id}, {v.field} = {v.value}";
                Check(drawn.ContainsKey(v.field), $"{what}: a field the window shows");
                if (!fields.TryGetValue(v.field, out var t)) continue;
                var members = EnumOf(v.field);
                if (v.value == SettingsCatalog.DefaultValue) continue;
                if (t.type == "bool") Check(v.value == "true" || v.value == "false", $"{what}: true or false");
                else if (t.type == "float" || t.type == "int")
                {
                    bool ok = float.TryParse(v.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f);
                    Check(ok, $"{what}: a number");
                    var slider = SettingsCatalog.Find(v.field);
                    if (ok && slider != null && slider.kind == SettingKind.Slider)
                    {
                        Check(f >= slider.min && f <= slider.max, $"{what}: on the slider ({slider.min} to {slider.max})");
                        double steps = (f - slider.min) / slider.step;
                        Check(Math.Abs(steps - Math.Round(steps)) < 0.001, $"{what}: on one of the slider's steps");
                    }
                }
                else Check(members != null && members.Contains(v.value), $"{what}: an option of {t.type}");
                if (v.onlyIfNow != null) Check(members != null && v.onlyIfNow.Split('|').All(members.Contains), $"{what}: only if now {v.onlyIfNow}, options of {t.type}");
            }
        }
        string Value(string presetId, string field) => SettingsCatalog.Preset(presetId)?.values.FirstOrDefault(v => v.field == field).value;
        // The documented intent of each.
        Check(Value("lightTouch", "stakes") == "Gentle" && Value("lightTouch", "lifePressure") == "Gentle", "light touch: gentle stakes and pressures");
        Check(Value("lightTouch", "seriousIllnesses") == "false" && Value("lightTouch", "worldEventBattles") == "false", "light touch: no deadly illnesses, no fights");
        Check(Value("lightTouch", "messageLevel") == "LettersOnly", "light touch: important messages only");
        Check(Value("storyRich", "arcFrequency") == "Frequent" && Value("storyRich", "messageLevel") == "Everything", "story-rich: frequent stories, every message");
        Check(new[] { "stakes", "lifePressure", "penaltyFactor", "rewardFactor", "frictions" }.All(f => Value("storyRich", f) == null), "story-rich leaves moods and pressures alone");
        Check(Value("demanding", "stakes") == "Demanding" && Value("demanding", "lifePressure") == "Demanding", "demanding: harder moods and pressures");
        Check(Value("demanding", "frictions") == "true" && float.Parse(Value("demanding", "rewardFactor") ?? "1", CultureInfo.InvariantCulture) < 1f,
            "demanding: frictions on, smaller rewards");
        var others = SettingsCatalog.StylePresets.Where(p => p.id != "default").SelectMany(p => p.Fields).ToHashSet();
        var normal = SettingsCatalog.Preset("default");
        Check(normal != null && normal.Fields.ToHashSet().SetEquals(others) && normal.values.All(v => v.value == SettingsCatalog.DefaultValue),
            "default puts back exactly what the other presets change");
        Check(SettingsCatalog.PeopleSystems.All(f => Value("everything", f) == "true"), "everything on: every people system on");
        Check(Value("advisorOnly", "stakes") == "AdvisorOnly" && Value("advisorOnly", "enableWishes") == "false", "advisor only: no wishes, no mood effects");

        // ---------- It fits: small screens, big interface scales ----------
        var screens = new (float w, float h)[] { (1280f, 720f), (1366f, 768f), (1600f, 900f), (1920f, 1080f), (2560f, 1440f), (3840f, 2160f) };
        float[] scales = { 1f, 1.25f, 1.5f, 1.75f, 2f };
        int pages = SettingsCatalog.Pages.Length, stylePresets = SettingsCatalog.StylePresets.Count();
        foreach (var (pw, ph) in screens)
        foreach (var scale in scales)
        {
            float sw = pw / scale, sh = ph / scale;
            string at = $"{pw}x{ph} at {scale}x";
            SettingsLayoutMath.DialogSize(sw, sh, out float dw, out float dh);
            Check(dw <= sw && dh <= sh, $"{at}: the dialog is on the screen ({dw}x{dh} on {sw:0}x{sh:0})");
            SettingsLayoutMath.ContentSize(dw, dh, out float cw, out float ch);
            float body = SettingsLayoutMath.BodyHeight(ch), row = SettingsLayoutMath.RowWidth(cw);
            Check(body >= 150f, $"{at}: room for a few rows under the search box ({body:0})");
            Check(row >= 260f, $"{at}: rows wide enough to read ({row:0})");
            Check(SettingsLayoutMath.LeftWidth(cw) >= SettingsLayoutMath.LeftMin, $"{at}: the page list is wide enough");
            Check(SettingsLayoutMath.ButtonWidth(row, 3) >= 100f, $"{at}: the quick setup buttons fit beside each other");
            if (sw >= 1024f && sh >= 768f)
            {
                // The game's own interface scales for this screen: nothing shrinks, nothing on the left scrolls.
                Check(dw == SettingsLayoutMath.DialogWidth && dh == SettingsLayoutMath.DialogHeight, $"{at}: the dialog keeps its size");
                Check(!SettingsLayoutMath.LeftScrolls(ch, pages, stylePresets), $"{at}: the page list and presets fit without scrolling");
                Check(body >= 500f && SettingsLayoutMath.SliderBeside(row), $"{at}: a tall page, sliders beside their labels");
            }
        }
        return checks;
    }
}
