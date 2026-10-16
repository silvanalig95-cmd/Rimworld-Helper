using System.Text.RegularExpressions;
using System.Xml.Linq;

/// <summary>
/// What's new (Source/ColonyCouncil/UI/WhatsNew.cs): the Almanac's "What's new" page and CHANGELOG.md list the same
/// rounds, in the same order, with the same dates, versions and points (Tools/changelog.py writes one from the other);
/// the newest round has a version and it is About.xml's modVersion, so the letter after an update comes when it should;
/// and the page is the first in the Almanac.
/// </summary>
public static class WhatsNewTests
{
    private record Round(string Title, string When, List<string> Points);

    public static int Run(string root, Action<bool, string> check)
    {
        int checks = 0;
        void Check(bool ok, string what)
        {
            checks++;
            check(ok, "what's new: " + what);
        }

        // ---------- The page ----------
        string pageFile = Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_WhatsNew.xml");
        Check(File.Exists(pageFile), "the What's new page exists");
        if (!File.Exists(pageFile)) return checks;
        var page = XDocument.Load(pageFile).Root!.Elements().FirstOrDefault(e => e.Element("defName")?.Value == "Council_Alm_WhatsNew");
        Check(page != null && page.Name.LocalName == "ColonyCouncil.WhatsNewPageDef", "Council_Alm_WhatsNew is a WhatsNewPageDef");
        if (page == null) return checks;
        var fromPage = new List<Round>();
        var versions = new List<string>();
        foreach (var li in page.Element("rounds")?.Elements("li") ?? Enumerable.Empty<XElement>())
        {
            string version = li.Element("version")?.Value.Trim() ?? "";
            string date = li.Element("date")?.Value.Trim() ?? "";
            versions.Add(version);
            fromPage.Add(new Round(li.Element("title")?.Value.Trim() ?? "", date + (version.Length > 0 ? ", version " + version : ""),
                li.Element("points")?.Elements("li").Select(p => p.Value.Trim()).ToList() ?? new List<string>()));
        }
        Check(fromPage.Count >= 5, $"the page has its rounds ({fromPage.Count})");
        foreach (var r in fromPage)
        {
            Check(r.Title.Length > 0 && r.When.Length > 0, $"round '{r.Title}' has a title and a date");
            Check(r.Points.Count >= 2 && r.Points.Count <= 8, $"round '{r.Title}' has a few points ({r.Points.Count})");
            foreach (var p in r.Points)
            {
                Check(!p.Contains('—') && p.Count(c => c == '!') == 0, $"round '{r.Title}': a plain point without em dashes or exclamations: {p}");
                Check(!p.Contains('{') && !p.Contains('}'), $"round '{r.Title}': no placeholders in a point: {p}");
            }
        }
        Check(fromPage.Select(r => r.Title).Distinct().Count() == fromPage.Count, "every round has its own title");

        // ---------- The newest version is the mod's ----------
        string about = XDocument.Load(Path.Combine(root, "About", "About.xml")).Root!.Element("modVersion")?.Value.Trim() ?? "";
        Check(versions.Count > 0 && versions[0].Length > 0, "the newest round has a version");
        Check(versions.Count > 0 && versions[0] == about, $"the newest round's version ({(versions.Count > 0 ? versions[0] : "none")}) is About.xml's modVersion ({about})");
        var known = versions.Where(v => v.Length > 0).ToList();
        Check(known.Distinct().Count() == known.Count, "no version is listed twice");
        for (int i = 1; i < known.Count; i++)
            Check(Compare(known[i - 1], known[i]) > 0, $"versions go down the page from newest to oldest ({known[i - 1]} after {known[i]})");

        // ---------- CHANGELOG.md says the same ----------
        string logFile = Path.Combine(root, "CHANGELOG.md");
        Check(File.Exists(logFile), "CHANGELOG.md exists");
        if (File.Exists(logFile))
        {
            var fromLog = new List<Round>();
            Round current = null;
            foreach (var raw in File.ReadAllLines(logFile))
            {
                string line = raw.TrimEnd();
                if (line.StartsWith("## "))
                {
                    current = new Round(line.Substring(3).Trim(), "", new List<string>());
                    fromLog.Add(current);
                }
                else if (current != null && Regex.IsMatch(line, @"^\*[^*].*\*$") && current.When.Length == 0)
                {
                    fromLog[^1] = current = current with { When = line.Trim('*') };
                }
                else if (current != null && line.StartsWith("- ")) current.Points.Add(line.Substring(2).Trim());
            }
            Check(fromLog.Count == fromPage.Count, $"CHANGELOG.md has the page's {fromPage.Count} rounds ({fromLog.Count}); run python3 Tools/changelog.py");
            for (int i = 0; i < Math.Min(fromLog.Count, fromPage.Count); i++)
            {
                var a = fromPage[i];
                var b = fromLog[i];
                Check(a.Title == b.Title, $"round {i + 1} has the same title in both ('{a.Title}' / '{b.Title}')");
                Check(a.When == b.When, $"round '{a.Title}' has the same date and version in both ('{a.When}' / '{b.When}')");
                Check(a.Points.SequenceEqual(b.Points), $"round '{a.Title}' has the same points in both; run python3 Tools/changelog.py");
            }
        }

        // ---------- First in the Almanac ----------
        int order = int.Parse(page.Element("order")?.Value ?? "100");
        Check(page.Element("category")?.Value == "Basics", "the page is in Basics, the Almanac's first part");
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "AlmanacArticleDefs"), "*.xml"))
            foreach (var other in XDocument.Load(file).Root!.Elements())
            {
                string otherName = other.Element("defName")?.Value;
                if (otherName == null || otherName == "Council_Alm_WhatsNew") continue;
                string cat = other.Element("category")?.Value ?? "Basics";
                if (cat != "Basics") continue;
                int o = int.TryParse(other.Element("order")?.Value, out int x) ? x : 100;
                Check(o > order, $"What's new comes before {otherName} in the Almanac");
            }
        return checks;
    }

    private static int Compare(string a, string b)
    {
        var pa = a.Split('.').Select(s => int.TryParse(s, out int n) ? n : 0).ToArray();
        var pb = b.Split('.').Select(s => int.TryParse(s, out int n) ? n : 0).ToArray();
        for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            int x = i < pa.Length ? pa[i] : 0, y = i < pb.Length ? pb[i] : 0;
            if (x != y) return x.CompareTo(y);
        }
        return 0;
    }
}
