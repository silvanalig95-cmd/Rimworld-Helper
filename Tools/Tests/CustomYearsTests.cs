using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Customs kept year after year (CustomYearsMath): the tags a custom's letter and answers get from how often it was kept
/// ("again", "years", "tradition", "missed"), that every line written for them can come true on the custom it belongs to,
/// and that what is said over heads at the seasonal customs is one short sentence.
/// </summary>
public static class CustomYearsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "customs kept year after year: " + what);
        }

        // ---------- The tags ----------
        Check(CustomYearsMath.Tags(0, 0, false).Count == 0, "never kept: no tags");
        Check(CustomYearsMath.Tags(1, 0, false).SequenceEqual(new[] { "again" }), "kept once: again");
        Check(CustomYearsMath.Tags(2, 0, true).SequenceEqual(new[] { "again", "tradition" }), "kept twice and a tradition: again, tradition");
        Check(CustomYearsMath.Tags(3, 0, true).SequenceEqual(new[] { "again", "years", "tradition" }), "kept three times: years");
        Check(CustomYearsMath.Tags(9, 1, true).Contains("missed"), "a tradition let go last time: missed");
        Check(!CustomYearsMath.Tags(1, 1, false).Contains("missed"), "only a tradition can be missed");
        Check(!CustomYearsMath.Tags(0, 0, false).Contains("years") && CustomYearsMath.Years >= 2, "years takes more than one keeping");
        for (int kept = 0; kept <= 8; kept++)
            foreach (var t in new[] { false, true })
                foreach (var tag in CustomYearsMath.Tags(kept, 1, t))
                    Check(CustomYearsMath.All.Contains(tag), $"{tag} is one of the custom's tags");

        // ---------- Every line written for a custom's years can come true ----------
        var dilemmas = new Dictionary<string, XElement>();
        var variants = new List<XElement>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "DilemmaDefs"), "*.xml"))
            foreach (var d in XDocument.Load(file).Root!.Elements())
            {
                if (d.Name.LocalName == "ColonyCouncil.DilemmaDef" && d.Element("defName") != null) dilemmas[d.Element("defName")!.Value] = d;
                else if (d.Name.LocalName == "ColonyCouncil.LetterVariantsDef" && d.Element("dilemma") != null) variants.Add(d);
            }
        Check(dilemmas.Count >= 100, $"found the dilemmas ({dilemmas.Count})");

        // The states a custom can be in: kept so often, a tradition only once kept often enough, let go last time or not.
        List<HashSet<string>> States(XElement d)
        {
            int after = CalendarMath.TraditionAfter;
            var states = new List<HashSet<string>>();
            for (int kept = 0; kept <= 8; kept++)
                foreach (var t in new[] { false, true })
                {
                    if (t && kept < after) continue;
                    for (int breaks = 0; breaks <= 1; breaks++) states.Add(new HashSet<string>(CustomYearsMath.Tags(kept, breaks, t)));
                }
            return states;
        }

        int written = 0;
        void Lines(string dilemma, IEnumerable<string> lines, string where)
        {
            foreach (var line in lines)
            {
                var tags = Tags(line);
                if (!tags.Any(t => t.Split('|').Any(a => CustomYearsMath.All.Contains(a.TrimStart('!'))))) continue;
                written++;
                bool custom = dilemmas.TryGetValue(dilemma, out var d) && int.TryParse((string?)d.Element("traditionOption"), out int o) && o >= 0;
                Check(custom, $"{dilemma} {where}: a line for a custom's years on a custom ({Short(line)})");
                if (!custom) continue;
                // Only the custom's own tags are weighed here; the others are the moment's.
                bool fits = States(d!).Any(s => tags.All(t =>
                {
                    var alts = t.Split('|').Select(x => x.Trim()).ToList();
                    if (!alts.All(x => CustomYearsMath.All.Contains(x.TrimStart('!')))) return true;
                    return alts.Any(x => x.StartsWith("!") ? !s.Contains(x.Substring(1)) : s.Contains(x));
                }));
                Check(fits, $"{dilemma} {where}: a line that can come true ({Short(line)})");
            }
        }
        foreach (var (name, d) in dilemmas)
        {
            Lines(name, Lis(d, "texts"), "letter");
            int i = 0;
            foreach (var o in d.Element("options")?.Elements("li") ?? Enumerable.Empty<XElement>())
            {
                Lines(name, new[] { (string?)o.Element("outcome") ?? "" }.Concat(Lis(o, "outcomes")), "answer " + i);
                i++;
            }
        }
        foreach (var v in variants)
        {
            string name = v.Element("dilemma")!.Value;
            Lines(name, Lis(v, "texts"), "letter");
            foreach (var o in v.Element("outcomes")?.Elements("li") ?? Enumerable.Empty<XElement>())
                Lines(name, Lis(o, "lines"), "answer " + (string?)o.Element("option"));
        }
        Check(written >= 40, $"customs have lines for the years they were kept ({written})");

        // ---------- Said over heads at the seasonal customs: one short sentence ----------
        foreach (var file in new[] { "Council_Dilemmas_Customs.xml", "Council_Dilemmas_Customs2.xml", "Council_Dilemmas_Seasons.xml", "Rimfolk_Dilemmas_Customs_New.xml", "Rimfolk_Dilemmas_New.xml" })
        {
            string path = Path.Combine(root, "Defs", "DilemmaDefs", file);
            if (!File.Exists(path)) continue;
            foreach (var block in XDocument.Load(path).Descendants("lines"))
                foreach (var li in block.Elements("li"))
                {
                    string body = li.Value.Trim();
                    Check(!body.StartsWith("["), $"{file}: '{body}' has no tags (said as written)");
                    Check(!body.StartsWith("\"") && !body.EndsWith("\""), $"{file}: '{body}' doesn't start or end with a quote");
                    Check(FirstSentence(body) == body, $"{file}: '{body}' is one sentence");
                    Check(body.Length <= 90, $"{file}: '{body}' is 90 characters at most ({body.Length})");
                    Check(body.Count(c => c == '!') <= 1, $"{file}: '{body}' has one '!' at most");
                }
        }
        return cases;
    }

    private static IEnumerable<string> Lis(XElement e, string name) => e.Element(name)?.Elements("li").Select(li => li.Value) ?? Enumerable.Empty<string>();

    private static string[] Tags(string line)
    {
        string t = line.TrimStart();
        if (!t.StartsWith("[")) return Array.Empty<string>();
        int end = t.IndexOf(']');
        return end < 0 ? Array.Empty<string>() : t.Substring(1, end - 1).Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
    }

    private static string Short(string line) => line.Length <= 60 ? line : line.Substring(0, 60) + "...";

    /// <summary>The first sentence: up to the first '.', '!' or '?' from the 13th character on (what always shows; SpeechMath.Shorten).</summary>
    private static string FirstSentence(string s)
    {
        for (int i = 12; i < s.Length; i++)
            if (s[i] == '.' || s[i] == '!' || s[i] == '?')
                return s.Substring(0, i + 1);
        return s;
    }
}
