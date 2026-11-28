using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Content round 6 (docs/content-round-6.md; the player's go of 27 November 2026, "just more content to the already existing
/// mechanics", and of 28 November, content that has "impacts on the game and it's not just flavor"): fifteen dreams that change
/// something, seven decrees, five callers at the gate and three happenings of the weather on the land, all in the frames there
/// are. Each item's defs load and name what they use; each has an effect the game keeps; each frame's own rules hold (the
/// callers' as the fourth round of neighbours had them, the weather's record asked for, the decrees' four moods, practice,
/// stores and daily work, the dreams' voices); every age has its form; plain words, no countdown, no god; the game's and the
/// player's mods' defs by name from the active rows of docs/player-mods; no new code beyond two pairs of decrees kept apart.
/// </summary>
public static class Round6Tests
{
    public static int Run(Action<bool, string> check)
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root)!;
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "content round 6: " + what);
        }

        XElement Defs(string rel) => XDocument.Load(Path.Combine(root, "Defs", rel)).Root!;
        string Name(XElement e) => (string)e.Element("defName") ?? "";
        string Cls(XElement e) => ((string)e.Attribute("Class") ?? "").Replace("ColonyCouncil.", "");
        List<string> Lis(XElement e, string name) => e?.Element(name)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
        float F(string s, float d = 0f) => s == null ? d : float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        string[] Tones = { "plain", "warm", "blunt", "cheerful", "gloomy", "cold" };
        IEnumerable<(string tone, string line)> Lines(XElement e) =>
            e == null ? Enumerable.Empty<(string, string)>() : e.Elements().Where(t => Tones.Contains(t.Name.LocalName)).SelectMany(t => t.Elements("li").Select(l => (t.Name.LocalName, l.Value)));
        string Strip(string s, out string[] tags)
        {
            var m = Regex.Match(s ?? "", @"^\[([^\]]*)\]\s*");
            tags = m.Success ? m.Groups[1].Value.Split(',').Select(t => t.Trim()).ToArray() : new string[0];
            return m.Success ? s!.Substring(m.Length) : s ?? "";
        }
        bool Countdown(string s) => Regex.IsMatch(s, @"\b(in|within) (\d+|a|two|three|four|five|six) (days?|hours?)\b") || Regex.IsMatch(s, @"\{DAYS\}|\bdays? (left|more)\b");
        var godNames = new[] { "Hollin", "Smoulder", "Bellwether", "Morel", "Ennu", "Pip", "Cirrus", "Tamiu", "Helios", "Selene", "Murmur", "Revel", "Hush", "Ducat", "Undertow" };
        bool NoGod(string s) => !godNames.Any(g => Regex.IsMatch(s, @"\b" + g + @"\b")) && !Regex.IsMatch(s, @"\bgods?\b|\bspirits?\b", RegexOptions.IgnoreCase);
        bool Plain(string s) => !s.Contains('—') && !s.Contains('!') && !Countdown(s) && NoGod(s);
        // Said aloud: one whole sentence (as SpeechMath shows it).
        bool OneSentence(string shown)
        {
            for (int i = 12; i < shown.Length - 1; i++)
                if (".!?".IndexOf(shown[i]) >= 0 && ".!?\"'".IndexOf(shown[i + 1]) < 0)
                    return false;
            return true;
        }
        // Only placeholders the frame fills: the event's people and words.
        bool Placeholders(string s, bool b, bool x)
        {
            foreach (Match m in Regex.Matches(s, @"\{([A-Za-z_]+)\}"))
            {
                string p = m.Groups[1].Value;
                if (Regex.IsMatch(p, @"^A_(nameDef|pronoun|possessive|objective|labelShort)$")) continue;
                if (b && Regex.IsMatch(p, @"^B_(nameDef|pronoun|possessive|objective|labelShort)$")) continue;
                if (x && Regex.IsMatch(p, @"^X_(nameDef|pronoun|possessive|objective|labelShort)$")) continue;
                if (p == "FACTION" || p == "PLACE" || p == "COUNT") continue;
                return false;
            }
            return true;
        }

        var allDefs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f).Root!).ToList();
        var thoughtNames = allDefs.SelectMany(d => d.Elements("ThoughtDef")).Select(Name).ToHashSet();
        var hediffNames = allDefs.SelectMany(d => d.Elements("HediffDef")).Select(Name).ToHashSet();
        var wishNames = allDefs.SelectMany(d => d.Elements().Where(e => e.Name.LocalName == "ColonyCouncil.WishDef")).Select(Name).ToHashSet();
        var officeNames = allDefs.SelectMany(d => d.Elements().Where(e => e.Name.LocalName == "ColonyCouncil.OfficeDef")).Select(Name).ToHashSet();

        // The game's and the player's mods' defs, from the catalogue's active rows (docs/player-mods/defs.tsv.gz).
        var active = new HashSet<string>();
        using (var gz = new GZipStream(File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz")), CompressionMode.Decompress))
        using (var r = new StreamReader(gz))
        {
            string line;
            while ((line = r.ReadLine()!) != null)
            {
                var cols = line.Split('\t');
                if (cols.Length >= 4 && cols[3] == "yes") active.Add(cols[0] + "/" + cols[1]);
            }
        }
        var mods = File.ReadAllLines(Path.Combine(root, "docs", "player-mods", "mods.tsv")).Select(l => l.Split('\t')).Where(c => c.Length > 3 && c[3] == "yes")
            .Select(c => c[0]).ToHashSet();

        // What an answer does that the game keeps (Tools/impact_audit.py's game kinds), and what only tells of it.
        var game = new HashSet<string>
        {
            "DilemmaEffect_Consume", "DilemmaEffect_Spawn", "DilemmaEffect_SpawnAnimal", "DilemmaEffect_Learn", "DilemmaEffect_Lesson", "DilemmaEffect_Made",
            "DilemmaEffect_Hunt", "DilemmaEffect_CarryOut", "DilemmaEffect_EventGoodwill", "DilemmaEffect_Research", "DilemmaEffect_Joy",
            "DilemmaEffect_WorkParty", "DilemmaEffect_Inspiration", "DilemmaEffect_Hediff"
        };
        var playOut = new[] { "DilemmaEffect_Lesson", "DilemmaEffect_CarryOut", "DilemmaEffect_Visit", "DilemmaEffect_Spawn", "DilemmaEffect_SpawnAnimal", "DilemmaEffect_SowPatch",
            "DilemmaEffect_Plant", "DilemmaEffect_Made", "DilemmaEffect_Hunt", "DilemmaEffect_WorkParty", "DilemmaEffect_Gathering" };
        var gathers = new[] { "DilemmaEffect_Gathering", "DilemmaEffect_Feast", "DilemmaEffect_Bonfire", "DilemmaEffect_Toast", "DilemmaEffect_Words", "VisitEffect_WorkHere", "DilemmaEffect_Race",
            "DilemmaEffect_Contest", "DilemmaEffect_WorkParty", "DilemmaEffect_Procession", "DilemmaEffect_Vigil", "DilemmaEffect_Search", "DilemmaEffect_HuntParty" };
        var hurts = new[] { "DilemmaEffect_Hediff", "DilemmaEffect_Fight", "DilemmaEffect_HarmRisk", "DilemmaEffect_TooClose", "DilemmaEffect_MentalState" };

        // ================= The callers at the gate (R6-C1 to R6-C5) =================

        var callers = Defs("IncidentDefs/Rimfolk_Round6_Callers.xml").Elements("IncidentDef").ToList();
        C(callers.Count == 5, $"five callers ({callers.Count})");
        foreach (var e in callers)
        {
            string name = Name(e);
            C(name.StartsWith("Rimfolk_Evt_R6Caller_"), $"{name}: the round's name");
            C(((string)e.Element("workerClass") ?? "").EndsWith("HappeningWorker_Setting"), $"{name}: told from data");
            var ext = e.Descendants("li").First(x => Cls(x) == "HappeningExtension");
            var setting = e.Descendants("li").First(x => Cls(x) == "HappeningSetting");
            C((string)setting.Element("visitor") == "true" && (string)setting.Element("people") == "own", $"{name}: someone of a people of about the colony's age, at the gate");
            C(F((string)e.Element("baseChance")) <= 0.8f && int.Parse((string)e.Element("minRefireDays")) >= 35, $"{name}: an ordinary share of the pool, and not soon again");
            C((string)ext.Element("group") == "Visitors" && ext.Element("battle") == null, $"{name}: a visitor, never a fight");
            var texts = Lis(ext, "texts");
            foreach (var age in new[] { "tribal", "medieval", "industrial", "spacer" })
                C(texts.Any(t => Strip(t, out var tg) != null && tg.Contains(age)), $"{name}: an opening in the {age} age's form");
            C(texts.Any(t => !t.StartsWith("[")), $"{name}: an opening for any colony");
            C(Lis(ext, "titles").Count >= 2, $"{name}: titles");
            var options = ext.Element("options")!.Elements("li").ToList();
            int def = int.Parse((string)ext.Element("defaultOption") ?? "0");
            C(options.Count >= 3 && def == options.Count - 1, $"{name}: a real choice, saying nothing the last");
            C(options[def].Element("effects")!.Elements("li").All(x => Cls(x) == "DilemmaEffect_SendAway"), $"{name}: silence sends them on, and sets nothing going");
            C(options.Count(o => o.Element("effects")?.Elements("li").Any(x => playOut.Contains(Cls(x))) == true) >= 2, $"{name}: two answers play out on the map");
            C(options.Where((o, i) => i != def).All(o => o.Element("effects")?.Elements("li").Any(x => game.Contains(Cls(x))) == true), $"{name}: every answer but silence does something the game keeps");
            C(!options.Any(o => o.Element("effects")?.Elements("li").Any(x => gathers.Contains(Cls(x))) == true), $"{name}: nothing that gathers the colony");
            C(!options.Any(o => o.Element("effects")?.Elements("li").Any(x => hurts.Contains(Cls(x))) == true), $"{name}: nobody hurt");
            bool hasB = ext.Element("second") != null;
            var words = texts.Concat(Lis(ext, "titles")).Concat(options.Select(o => (string)o.Element("label") ?? "")).Concat(options.Select(o => (string)o.Element("outcome") ?? ""))
                .Concat(options.SelectMany(o => Lis(o, "outcomes"))).ToList();
            C(words.All(w => Plain(Strip(w, out _))), $"{name}: plain prose, no countdown, no god");
            C(words.All(w => Placeholders(w, hasB, true)), $"{name}: only the words the frame fills (B only with a second colonist)");
            C(options.All(o => !string.IsNullOrEmpty((string)o.Element("outcome"))), $"{name}: every answer says what happened");
            // Goods by name: the game's own, from the catalogue's active rows.
            foreach (var t in e.Descendants("thing").Select(x => x.Value))
                C(active.Contains("ThingDef/" + t), $"{name}: {t} is a thing of the game's");
            foreach (var t in e.Descendants("things").SelectMany(x => x.Value.Split('|')).Where(t => !t.StartsWith("@")))
                C(active.Contains("ThingDef/" + t), $"{name}: {t} is a thing of the game's or an active mod's");
        }
        var setText = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Happenings", "Workers_Setting.cs"));
        C(setText.Contains("case \"animals\": return Animals(map).Any();") && setText.Contains("n.option == option && !Met(ctx.map, n.need)"),
            "the carter's hay is greyed out with no animals to feed (the setting's own answer needs)");
        var madeCode = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Things", "ThingRolesMath.cs"));
        C(madeCode.Contains("Blade") && callers.Any(e => e.ToString().Contains("<things>@Blade</things>")),
            "the arms-master's blade is any active mod's forged blade the colony's age has (the things' role), the game's own first");

        // ================= The weather on the land (R6-W1 to R6-W3) =================

        var weather = Defs("IncidentDefs/Rimfolk_Round6_Weather.xml").Elements("IncidentDef").ToList();
        C(weather.Count == 3, $"three happenings of the weather ({weather.Count})");
        foreach (var e in weather)
        {
            string name = Name(e);
            C(name.StartsWith("Rimfolk_Evt_R6Weather_"), $"{name}: the round's name");
            C(((string)e.Element("workerClass") ?? "").EndsWith("HappeningWorker_Setting"), $"{name}: told from data");
            var ext = e.Descendants("li").First(x => Cls(x) == "HappeningExtension");
            C((string)ext.Element("group") == "Nature" && ext.Element("battle") == null, $"{name}: of the land, never a fight");
            var w = Lis(ext, "weather");
            C(w.Count > 0 && w.All(x => Regex.IsMatch(x, @"^(\[[a-z]+\])?((rain|snow|frost):\d+/\d+|(dry|storm):\d+)$")), $"{name}: asks the weather record for the days it speaks of ({string.Join(", ", w)})");
            C(F((string)e.Element("baseChance")) <= 0.8f && int.Parse((string)e.Element("minRefireDays")) >= 40, $"{name}: an ordinary share, not soon again");
            var options = ext.Element("options")!.Elements("li").ToList();
            int def = int.Parse((string)ext.Element("defaultOption") ?? "0");
            C(options.Count >= 3 && options[def].Element("effects") == null, $"{name}: a real choice, the default leaving things as they are");
            C(options.Count(o => o.Element("effects")?.Elements("li").Any(x => playOut.Contains(Cls(x))) == true) >= 2, $"{name}: two answers play out on the map");
            C(options.Where((o, i) => i != def).All(o => o.Element("effects")?.Elements("li").Any(x => game.Contains(Cls(x))) == true), $"{name}: every answer but the default does something the game keeps");
            C(!e.Descendants("li").Any(x => hurts.Contains(Cls(x))), $"{name}: nobody hurt");
            var texts = Lis(ext, "texts");
            C(texts.Any(t => !t.StartsWith("[")) && (texts.Any(t => Regex.IsMatch(t, @"^\[[^\]]*(lowtech|tribal|medieval)")) && texts.Any(t => Regex.IsMatch(t, @"^\[[^\]]*(!lowtech|industrial|spacer)"))),
                $"{name}: every age's form");
            bool hasB = ext.Element("second") != null;
            var words = texts.Concat(Lis(ext, "titles")).Concat(options.Select(o => (string)o.Element("label") ?? "")).Concat(options.Select(o => (string)o.Element("outcome") ?? ""))
                .Concat(options.SelectMany(o => Lis(o, "outcomes"))).ToList();
            C(words.All(x => Plain(Strip(x, out _))), $"{name}: plain prose, no countdown, no god");
            C(words.All(x => Placeholders(x, hasB, false)), $"{name}: only the words the frame fills");
        }
        var clear = weather.First(e => Name(e) == "Rimfolk_Evt_R6Weather_ClearNights");
        C(clear.ToString().Contains("<li>research</li>") && clear.ToString().Contains("DilemmaEffect_Research"), "clear nights: only with research in hand, and it moves on");
        var wind = weather.First(e => Name(e) == "Rimfolk_Evt_R6Weather_Windfall");
        var windExt = wind.Descendants("li").First(x => Cls(x) == "HappeningExtension");
        C(windExt.Element("effects")?.Elements("li").Any(x => Cls(x) == "DilemmaEffect_Felled" && (string)x.Element("near") == "Gate") == true,
            "the windfall: the trees really lie by the road when the card comes, felled outside the home area (Felled)");

        // ================= Dreams that change something (R6-D1 to R6-D15) =================

        var dreams = Defs("DreamDefs/Rimfolk_Round6_Dreams.xml").Elements("ColonyCouncil.DreamDef").ToList();
        C(dreams.Count == 15, $"fifteen dreams ({dreams.Count})");
        foreach (var d in dreams)
        {
            string name = Name(d);
            C(name.StartsWith("Rimfolk_R6_Dream_"), $"{name}: the round's name");
            var effects = d.Element("effects")?.Elements("li").ToList() ?? new List<XElement>();
            bool matters = d.Element("omen") != null || d.Element("wish") != null || effects.Any(x => Cls(x) != "DilemmaEffect_Thought");
            C(matters, $"{name}: changes something the game keeps (DreamDef.Matters)");
            C(d.Element("lines")?.Elements().Count(t => t.Elements("li").Any()) >= (d.Element("omen") != null ? 4 : 6), $"{name}: in the dreamers' voices");
            foreach (var (tone, line) in Lines(d.Element("lines")))
            {
                string body = Strip(line, out _);
                C(Plain(body) && body.StartsWith("I"), $"{name}: plain words, first person: \"{body}\"");
                C(Regex.Matches(body, @"[.?]\s+[A-Z]").Count <= 1, $"{name}: a line of a sentence or two: \"{body}\"");
            }
            foreach (var x in effects)
            {
                if (Cls(x) == "DilemmaEffect_Inspiration") C(active.Contains("InspirationDef/" + (string)x.Element("inspiration")), $"{name}: the inspiration is the game's");
                if (Cls(x) == "DilemmaEffect_Learn") C(x.Element("skill") != null && F((string)x.Element("xp")) <= 600f, $"{name}: a night's practice, no more");
            }
            var omen = d.Element("omen");
            if (omen != null)
            {
                C((string)d.Element("kind") == "Omen" && active.Contains("IncidentDef/" + (string)omen.Element("incident")), $"{name}: an omen of an event of the game's");
                C(F((string)d.Element("weight"), 1f) <= 0.2f, $"{name}: no likelier than the older omens");
            }
            var wish = (string)d.Element("wish");
            if (wish != null) C(wishNames.Contains(wish), $"{name}: the wish is the council's ({wish})");
            var office = (string)d.Element("dreamer")?.Element("office");
            if (office != null) C(officeNames.Contains(office), $"{name}: the seat is the council's ({office})");
        }
        C(dreams.Count(d => d.Element("omen") != null && (string)d.Element("omen")!.Element("good") == "false") == 1, "one omen of the fifteen is of something bad (the dry storm), the rest good");
        C(dreams.Count(d => (string)d.Element("kind") == "Nightmare") >= 1, "not every dream a good one");

        // ================= Decrees (R6-K1 to R6-K7) =================

        var decrees = Defs("DecreeDefs/Rimfolk_Round6_Decrees.xml").Elements("ColonyCouncil.DecreeDef").ToList();
        C(decrees.Count == 7, $"seven decrees ({decrees.Count})");
        var thoughts = Defs("ThoughtDefs/Rimfolk_Round6_Thoughts.xml").Elements("ThoughtDef").ToList();
        var allDecrees = allDefs.SelectMany(x => x.Elements("ColonyCouncil.DecreeDef")).ToList();
        var ours = decrees.Select(Name).ToHashSet();
        var otherOrders = allDecrees.Where(x => !ours.Contains(Name(x))).Select(x => (string)x.Element("order")).ToHashSet();
        C(decrees.Select(x => (string)x.Element("order")).Distinct().Count() == decrees.Count && decrees.All(x => !otherOrders.Contains((string)x.Element("order"))),
            "each in a place of its own on the Decrees page");
        string almanac = File.ReadAllText(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Council.xml"));
        foreach (var d in decrees)
        {
            string name = Name(d);
            C(name.StartsWith("Rimfolk_DecreeR6_"), $"{name}: the round's name");
            var th = thoughts.FirstOrDefault(t => Name(t) == (string)d.Element("thought"));
            C(th != null && th.Element("stages")!.Elements("li").Count() == 4 && (string)th.Element("workerClass") == "ColonyCouncil.ThoughtWorker_Decree",
                $"{name}: its mood in four stages (hate, dislike, like, love)");
            if (th != null)
            {
                var m = th.Element("stages")!.Elements("li").Select(s => F((string)s.Element("baseMoodEffect"))).ToList();
                C(m[0] < 0 && m[1] < 0 && m[2] > 0 && m[3] > 0 && m.All(v => Math.Abs(v) <= 4f), $"{name}: small moods, as the council's are");
            }
            var hediff = (string)d.Element("hediff");
            if (hediff != null) C(hediffNames.Contains(hediff), $"{name}: its effect on the body is a def");
            bool does = hediff != null || d.Element("practiceSkills") != null || d.Element("take") != null || d.Element("daily") != null;
            C(does, $"{name}: does something the game keeps (a stat, practice, the stores or work on the map)");
            string desc = (string)d.Element("description") ?? "";
            C(desc.Contains("tribe") && desc.Contains("village") && desc.Contains("industrial") && desc.Contains("spacer"), $"{name}: each age declares it in its own form");
            C(!desc.Contains('—') && !desc.Contains('!') && NoGod(desc), $"{name}: plain words on the card");
            var support = Lines(d.Element("support")).Select(x => x.line).ToList();
            C(support.Any(l => Regex.IsMatch(l, @"^\[(tribal|lowtech)")) && support.Any(l => Regex.IsMatch(l, @"^\[(spacer|!lowtech)")), $"{name}: asked for in every age's words");
            foreach (var l in support.Concat(Lines(d.Element("oppose")).Select(x => x.line)))
                C(OneSentence(Strip(l, out _)) && Plain(Strip(l, out _)), $"{name}: a line said is one plain sentence: \"{l}\"");
            C(almanac.Contains("<li>" + ((string)d.Element("label"))!.Substring(0, 1).ToUpper() + ((string)d.Element("label"))!.Substring(1) + " |"), $"{name}: in the Almanac's table of decrees");
            if (d.Element("take") != null) C((string)d.Element("goodwillTo") == "Situation" && Lis(d, "situationKinds").Count > 0, $"{name}: sends to the people a hard time hits, only while it lasts");
            // The game's DLCs are always there (docs/player-mods/README.md); a mod must be in the player's list.
            foreach (var mod in Lis(d, "mods")) C(mod.StartsWith("ludeon.rimworld.") || mods.Contains(mod), $"{name}: {mod} is a DLC or in the player's list");
        }
        C(DecreeMath.Excludes(DecreeMath.Bench, "Council_Decree_Building") && DecreeMath.Excludes("Council_Decree_Harvest", DecreeMath.Bench)
          && !DecreeMath.Excludes(DecreeMath.Bench, "Council_Decree_LongHours"), "evenings at the bench never with a building season or a harvest push");
        var hd = Defs("HediffDefs/Rimfolk_Round6_Hediffs.xml").Elements("HediffDef").ToList();
        foreach (var h in hd)
            foreach (var s in h.Descendants("statOffsets").Elements())
                C(active.Contains("StatDef/" + s.Name.LocalName) && Math.Abs(F(s.Value)) <= 0.2f, $"{Name(h)}: {s.Name.LocalName} is the game's, a small change");
        var situationKinds = allDefs.SelectMany(x => x.Elements().Where(e => e.Name.LocalName == "ColonyCouncil.WorldSituationDef")).Select(e => (string)e.Element("kind")).ToHashSet();
        foreach (var d in decrees)
            foreach (var k in Lis(d, "situationKinds"))
                C(situationKinds.Contains(k), $"{Name(d)}: a world event of the kind '{k}' exists");

        // ================= Nothing new to save, the list written =================

        var src = Directory.GetFiles(Path.Combine(root, "Source", "ColonyCouncil"), "*.cs", SearchOption.AllDirectories).Where(f => Regex.IsMatch(Path.GetFileName(f), "^(R6|Round6)")).ToList();
        C(src.Count == 0, "no code of the round's own: every item is data in a frame there is");
        var doc = File.ReadAllText(Path.Combine(root, "docs", "content-round-6.md"));
        foreach (var x in callers.Concat(weather).Concat(dreams).Concat(decrees))
            C(doc.Contains(Name(x)), $"the list names {Name(x)}");
        C(Regex.Matches(doc, "[Nn]ot a duplicate of").Count >= 30 && doc.Contains("## Mods that would help"), "the list says what each item is not a duplicate of, and names mods that would help");
        return n;
    }
}
