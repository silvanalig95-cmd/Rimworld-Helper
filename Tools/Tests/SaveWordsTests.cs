using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The words the player's save (Atia, day 128, 6 November 2026) showed untrue or broken, kept from coming back: story text
/// counted in words, not "(s)"; a life goal's spark said in words, not a memory's label quoted; no wish label that can't
/// follow "wished for"; chronicle lines that begin with a capital and are sentences, not a letter's label; a conversion
/// told once; a world event's end in words; the officers' "elected" lines that say what they mean to do, not acts nobody
/// sees.
/// </summary>
public static class SaveWordsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "save words: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var keyed = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var e in XDocument.Load(f).Root!.Elements())
                keyed[e.Name.LocalName] = e.Value;
        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f).Root!).ToList();

        // Counted in words.
        Check(Texts.CountKey("CC_X", 1) == "CC_X_One" && Texts.CountKey("CC_X", 2) == "CC_X_Many" && Texts.CountKey("CC_X", 0) == "CC_X_Many", "one, and any other number");
        var countKeys = new HashSet<string>();
        foreach (var f in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
            foreach (Match m in Regex.Matches(File.ReadAllText(f), @"Texts\.CountKey\(""([A-Za-z0-9_]+)"""))
                countKeys.Add(m.Groups[1].Value);
        Check(countKeys.Count >= 9, $"the story's counts use the words ({countKeys.Count})");
        foreach (var k in countKeys)
        {
            Check(keyed.ContainsKey(k + "_One") && keyed.ContainsKey(k + "_Many"), $"{k} has words for one and for many");
            if (keyed.TryGetValue(k + "_One", out var one) && keyed.TryGetValue(k + "_Many", out var many))
                Check(!one.Contains("(s)") && !many.Contains("(s)") && !one.Contains("(es)") && !many.Contains("(es)"), $"{k}: no (s)");
        }
        foreach (var k in new[] { "CC_Chronicle_Anniversary", "CC_Annals_Raids", "CC_Annals_Wishes", "CC_Annals_Dilemmas", "CC_Chronicle_PassedOver", "CC_Year_BeastStole", "CC_Year_Road" })
            Check(countKeys.Contains(k), $"{k} is counted in words");

        // A life goal's spark in words.
        var sparks = defs.SelectMany(d => d.Descendants("li")).Select(li => li.Value.Trim()).Where(v => v.StartsWith("thought:")).Select(v => v.Substring(8)).Distinct().ToList();
        Check(sparks.Count >= 20, $"the memories that spark goals are found ({sparks.Count})");
        foreach (var t in sparks)
            Check(keyed.TryGetValue("CC_Asp_Spark_thought_" + t, out var w) && w.StartsWith("after ") && !w.Contains('"'), $"the spark {t} is said in words");
        Check(!Read(Path.Combine("Aspirations", "Aspirations.cs")).Contains("\"CC_Asp_Spark_thoughtNamed\""), "a memory's label is never quoted as the spark");

        // Wishes in mid-sentence.
        var wishLabels = defs.SelectMany(d => d.Elements("ColonyCouncil.WishDef")).Select(w => w.Element("label")?.Value ?? "").ToList();
        Check(wishLabels.Count > 50 && wishLabels.All(l => !l.StartsWith("to ")), "every wish is a thing to wish for (\"wished for to see\" was the save's)");
        string council = Read("CouncilGameComponent.cs");
        Check(council.Contains("\"CC_Chronicle_WishMade\".Translate(w.pawn.LabelShort, w.LabelInline") && council.Contains("\"CC_Chronicle_Petition\".Translate(p.LabelShort, w.pawn.LabelShort, w.LabelInline"),
            "a wish in the middle of a chronicle line isn't capitalized");

        // The chronicle's lines.
        string chron = Read("Chronicle.cs");
        Check(chron.Contains("this.text = text.CapitalizeFirst();"), "a chronicle line begins with a capital, whatever label leads it");
        Check(!Read(Path.Combine("Character", "Character.cs")).Contains("new ChronicleEntry(ChronicleKind.Council, label, null)")
              && !Read(Path.Combine("Character", "ColonyTraits.cs")).Contains("new ChronicleEntry(ChronicleKind.Council, label, null)"), "a letter's label never stands as a chronicle line");
        Check(keyed.TryGetValue("CC_Ways_KnownChronicle", out var known) && known.EndsWith(".") && known.Contains("{0}"), "the colony's name in a sentence");
        string marks = Read(Path.Combine("Persona", "Marks.cs"));
        Check(marks.Contains("Faiths.ToldConversion(p)") && Read(Path.Combine("Faith", "Faiths.cs")).Contains("public static bool ToldConversion(Pawn p)"),
            "a conversion the faith told isn't told again by the mark it leaves");

        // The year's letter.
        string year = Read(Path.Combine("World", "YearLetter.cs"));
        Check(!year.Contains(": key;") && year.Contains("\"CC_Year_WorldOver\""), "a world event's end is never its ending's key (\"ended in over\")");
        Check(year.Contains("\"CC_Year_HerdNoneBorn\"") && year.Contains("\"CC_Year_ChronicleSome\""), "none of the herd born, and the chronicle's lines without a count");

        // The officers' lines in the results letter: what they mean to do, not acts nobody sees.
        var acts = new Regex(@"\b(went|walked|spent the evening|took the seat|lit a|counted the|washed|asked everyone|has already|the same afternoon|that same evening|has started|has named|has swept|has propped|has announced)\b");
        foreach (var o in defs.SelectMany(d => d.Elements("ColonyCouncil.OfficeDef")))
            foreach (var li in o.Element("elected")?.Elements("li") ?? Enumerable.Empty<XElement>())
                Check(!acts.IsMatch(li.Value), $"{o.Element("defName")?.Value}: \"{li.Value}\" claims no act the map doesn't show");
        return cases;
    }
}
