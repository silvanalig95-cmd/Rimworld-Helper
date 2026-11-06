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
        // The player's settings file carries over untouched: every field of the settings as they stood before the five
        // pages (Tools/Tests/SettingsKeys.txt, from 4cc8cb8) is still saved under its key, with its type and default.
        // Moving a row is never renaming its key.
        int before = 0;
        foreach (var line in File.ReadAllLines(Path.Combine(root, "Tools", "Tests", "SettingsKeys.txt")))
        {
            if (line.StartsWith("#") || line.Trim().Length == 0) continue;
            var parts = line.Split('\t');
            string f = parts[0], key = parts[1], type = parts[2], def = parts.Length > 3 ? parts[3] : "";
            before++;
            Check(fields.TryGetValue(f, out var now), $"{f}, a setting before the five pages, is still a field");
            Check(scribed.TryGetValue(f, out var k) && k == key, $"{f} is still saved under its key {key}");
            Check(now.type == type && (now.def ?? "") == def, $"{f} keeps its type and default ({type} {def})");
        }
        Check(before >= 270, $"the settings before the five pages are all listed ({before})");

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
        // Five pages by what a player thinks about (decision 15): how much happens, how hard, what's on, how you're told, your other mods.
        Check(SettingsCatalog.Pages.SequenceEqual(new[] { SettingsPage.HowMuch, SettingsPage.HowHard, SettingsPage.WhatsOn, SettingsPage.HowTold, SettingsPage.OtherMods }),
            "five pages, in the player's order");
        int Count(SettingsPage p) => SettingsCatalog.On(p).Count();
        Check(Count(SettingsPage.HowMuch) >= 15 && Count(SettingsPage.HowHard) >= 20 && Count(SettingsPage.WhatsOn) >= 100 && Count(SettingsPage.HowTold) >= 60
              && Count(SettingsPage.OtherMods) >= 10, $"each page holds its part ({string.Join(", ", SettingsCatalog.Pages.Select(p => p + " " + Count(p)))})");
        // How much happens holds the rates: every slider there is a rate of something happening, and every rate is there.
        var rates = new[]
        {
            "gatheringsAWeek", "daysBetweenWishes", "maxOpenWishes", "daysBetweenDilemmas", "daysBetweenMeetings", "momentFrequency", "socialEventFrequency",
            "dreamFrequency", "arcFrequency", "daysBetweenPersonalTouches", "outsiderChatterFrequency", "encounterFrequency", "storytellerEventWeight",
            "daysBetweenWorldEvents", "daysBetweenWorldSituations", "remoteTravellers", "raidersCamps"
        };
        foreach (var r in rates) Check(SettingsCatalog.Find(r)?.page == SettingsPage.HowMuch, $"the rate {r} is on How much happens");
        Check(SettingsCatalog.On(SettingsPage.HowMuch).All(e => rates.Contains(e.id)), "How much happens holds only the rates");
        var knobs = new[]
        {
            "stakes", "penaltyFactor", "rewardFactor", "wishDurationFactor", "ambitionDeadlineFactor", "lifePressure", "risingStandards", "goodGrowsOrdinary",
            "quietTroubles", "frictions", "strainSpeed", "heartEpisodes", "heartGoodSide", "ailmentFrequency", "seriousIllnesses", "everydayHurts", "scenarioThreat"
        };
        foreach (var r in knobs) Check(SettingsCatalog.Find(r)?.page == SettingsPage.HowHard, $"{r} is on How hard");
        // Other mods' switches are on their own page, each under its mod, shown only with the mod loaded.
        var modded = new[]
        {
            "guestWill", "guestStories", "guestCheck", "marketStallWindow", "marketStallShips", "marketCampersBuy", "marketGuestsShop", "drinkBeforeWork",
            "smokeBeforeWork", "coveredLooks", "magicGolemRepair", "magicNeedRules", "territoryNewsQuiet", "medicalTriage"
        };
        foreach (var r in modded)
        {
            var e = SettingsCatalog.Find(r);
            Check(e?.page == SettingsPage.OtherMods && e.mods != null && e.mods.Length > 0 && e.mods.All(SettingsCatalog.ModSections.ContainsKey),
                $"{r}: on Your other mods, shown only with its mod loaded");
        }
        Check(SettingsCatalog.Find("marketGuestsShop")?.mods?.Length == 2, "guests shopping at the market needs both RimMarket and Hospitality");
        Check(SettingsCatalog.All.Where(e => e.mods != null).All(e => e.page == SettingsPage.OtherMods), "only the other mods' page has rows for other mods");
        Check(SettingsCatalog.On(SettingsPage.OtherMods).All(e => e.mods != null || e.id == "faithTurretsHere"), "every row there is for a mod (a faith's precept from any mod aside)");
        foreach (var kv in SettingsCatalog.ModSections)
            Check(SettingsCatalog.On(SettingsPage.OtherMods).Any(e => e.section == kv.Value && e.mods != null && e.mods.Contains(kv.Key)), $"{kv.Key}: its heading has its rows");
        // Fealty and vassals are a kind of pact (decision 14): their switch hangs under pacts and truces, on the same page.
        var fealty = SettingsCatalog.Find("diplomacyFealty");
        Check(fealty != null && fealty.Parent == "diplomacyPacts" && fealty.Indented && fealty.section == "CC_SetSec_Peoples", "fealty and vassals hang under pacts, in the bonds with other peoples");
        var peoples = SettingsCatalog.On(SettingsPage.WhatsOn).Where(e => e.section == "CC_SetSec_Peoples").Select(e => e.id).ToList();
        Check(peoples.IndexOf("diplomacyFealty") == peoples.IndexOf("diplomacyPacts") + 1, "fealty right under pacts");
        Check(Regex.IsMatch(File.ReadAllText(Path.Combine(sourceDir, "Diplomacy", "Fealty.cs")),
            @"public static bool On => Pacts\.On && \(ColonyCouncilMod\.Settings\?\.diplomacyFealty \?\? true\)"), "pacts off, no new pact of fealty either");
        // A row hangs indented only under a parent on its own page; elsewhere it stands alone.
        foreach (var e in all.Where(e => e.Parent != null))
            Check(e.Indented == (SettingsCatalog.Find(e.Parent)?.page == e.page), $"{e.id}: indented only under a parent on its page");
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
                    Check(new[] { "%", "0", "0.0", "0.#", "0.00" }.Contains(e.format), $"{e.id}: a known format ({e.format})");
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
        // The presets reach every page's sliders that are about play: every rate on How much happens and every knob on
        // How hard is set by light touch, story-rich or demanding (and so put back by default). The tools' sliders, the
        // panels' and the hover card's are the player's own.
        foreach (var e in all.Where(e => (e.page == SettingsPage.HowMuch || e.page == SettingsPage.HowHard) && (e.kind == SettingKind.Slider || e.kind == SettingKind.Choice)))
            Check(others.Contains(e.id), $"{e.id} ({e.page}): a preset reaches it");
        foreach (var r in rates)
            Check(Value("lightTouch", r) != null || Value("storyRich", r) != null, $"{r}: light touch or story-rich sets its pace");
        // Every part, the newest third among them: the region, the bonds with other peoples, the heart and its good side,
        // harm, the gatherings a week, how you're told.
        foreach (var f in new[] { "remoteTravellers", "raidersCamps", "diplomacyNature", "strainSpeed", "heartEpisodes", "heartGoodSide", "everydayHurts", "scenarioThreat", "gatheringsAWeek", "messagesADay" })
            Check(Value("lightTouch", f) != null, $"light touch reaches {f}");
        foreach (var f in new[] { "regionFortunes", "regionChange", "tradingPosts", "hamlets", "diplomacyFavours", "diplomacyPacts", "diplomacyFealty", "diplomacyFeasts", "goodsComeAndGo", "heartGoodSide", "gatheringsAWeek" })
            Check(Value("storyRich", f) != null, $"story-rich reaches {f}");
        Check(Value("lightTouch", "gatheringsAWeek") == "1" && Value("storyRich", "gatheringsAWeek") == "3", "the gatherings a week: one in light touch, three in story-rich");
        Check(Value("lightTouch", "everydayHurts") == "0.5" && Value("demanding", "everydayHurts") == "1.25", "hurts at half in light touch, a quarter more in demanding");
        Check(Value("lightTouch", "heartGoodSide") == "true" && Value("demanding", "strainSpeed") == "1.25", "the heart: its good side on in light touch, strain faster in demanding");
        // Story-rich switches on only what is on by default: it never turns on something the player wouldn't have had.
        foreach (var v in SettingsCatalog.Preset("storyRich").values.Where(v => v.value == "true"))
            Check(fields.TryGetValue(v.field, out var t) && t.def == "true", $"story-rich turns on {v.field}, which is on by default");

        // ---------- What's on: the quick setup reaches every part of the colony's life and its world ----------
        var systems = SettingsCatalog.Systems.ToList();
        Check(systems.Count >= 85, $"the setups reach every system ({systems.Count} switches)");
        foreach (var s in SettingsCatalog.SystemSections)
            Check(systems.Any(e => e.section == s), $"the setups reach the section {s}");
        foreach (var f in new[] { "regionFortunes", "tradingPosts", "hamlets", "diplomacyPacts", "diplomacyFealty", "diplomacyRenown", "goodsComeAndGo", "answersPlayOut", "heart" })
            Check(systems.Any(e => e.id == f), $"the setups reach {f}");
        Check(systems.All(e => Value("everything", e.id) == "true"), "everything on: every system on, the switches under them too");
        Check(systems.Where(e => SettingsCatalog.CouncilSections.Contains(e.section)).All(e => Value("councilOnly", e.id) == "true"), "council only: the council and the story on");
        Check(systems.Where(e => !SettingsCatalog.CouncilSections.Contains(e.section)).All(e => e.Indented ? Value("councilOnly", e.id) == null : Value("councilOnly", e.id) == "false"),
            "council only: every other part's own switch off, the switches under them left as they are");
        Check(SettingsCatalog.AdvisorSystems.All(f => Value("advisorOnly", f) == "true")
              && systems.Where(e => !SettingsCatalog.AdvisorSystems.Contains(e.id)).All(e => e.Indented ? Value("advisorOnly", e.id) == null : Value("advisorOnly", e.id) == "false"),
            "advisor only: the advisor and its plan, every other part's own switch off");
        Check(Value("advisorOnly", "stakes") == "AdvisorOnly" && Value("advisorOnly", "enableWishes") == "false", "advisor only: no wishes, no mood effects");
        Check(systems.All(e => fields.TryGetValue(e.id, out var t) && t.def == "true"), "every system the setups reach is on by default");

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
