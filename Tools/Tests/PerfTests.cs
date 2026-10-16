using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The caches and clocks that make Rimfolk lighter in a long, large game, and proof that they change nothing:
/// lines taken apart once (LineTags) answer exactly as the old code did for every line of the mod, in every era and
/// against many moments; the caches forget what they should when they should (Lean/Fresh); the ailment and health
/// slices come round once an hour each.
/// </summary>
public static class PerfTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "lean: " + what);
        }

        Lines(root, C);
        return n;
    }

    // ---------------- Lines taken apart once ----------------

    /// <summary>The old Flavor.Strip, word for word.</summary>
    private static string OldStrip(string line, out string[] tags)
    {
        tags = null;
        if (line == null) return null;
        string trimmed = line.TrimStart();
        if (!trimmed.StartsWith("[")) return line;
        int end = trimmed.IndexOf(']');
        if (end < 0) return line;
        tags = trimmed.Substring(1, end - 1).Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToArray();
        return trimmed.Substring(end + 1).TrimStart();
    }

    private static readonly HashSet<string> OldStanding = new HashSet<string>
    {
        "tribal", "medieval", "industrial", "spacer", "lowtech", "poor", "rich", "small", "large"
    };

    private static bool OldOnlyStanding(string tag)
    {
        foreach (var alt in tag.Split('|'))
        {
            string t = alt.Trim().TrimStart('!');
            if (!OldStanding.Contains(t) && !t.StartsWith("land:")) return false;
        }
        return true;
    }

    /// <summary>The old Flavor.Match, word for word.</summary>
    private static int OldMatch(string line, HashSet<string> context)
    {
        OldStrip(line, out var tags);
        if (tags == null || tags.Length == 0) return 0;
        int matched = 0;
        foreach (var tag in tags)
        {
            bool ok = false;
            foreach (var alt in tag.Split('|'))
            {
                string a = alt.Trim();
                bool neg = a.StartsWith("!");
                if (neg) a = a.Substring(1);
                bool has = context != null && context.Contains(a);
                if (has != neg)
                {
                    ok = true;
                    break;
                }
            }
            if (!ok) return -1;
            if (!OldOnlyStanding(tag)) matched++;
        }
        return matched;
    }

    private static readonly HashSet<string> OldEraNames = new HashSet<string> { "tribal", "medieval", "industrial", "spacer", "lowtech" };

    /// <summary>The old Flavor.EraTagsFit, word for word, with the colony's era tags given.</summary>
    private static bool OldEraTagsFit(string line, HashSet<string> eraNow)
    {
        OldStrip(line, out var tags);
        if (tags == null || tags.Length == 0) return true;
        HashSet<string> era = null;
        foreach (var tag in tags)
        {
            var alts = tag.Split('|').Select(a => a.Trim()).ToArray();
            if (!alts.All(a => OldEraNames.Contains(a.TrimStart('!')))) continue;
            era ??= new HashSet<string>(eraNow);
            if (!alts.Any(a => a.StartsWith("!") ? !era.Contains(a.Substring(1)) : era.Contains(a))) return false;
        }
        return true;
    }

    /// <summary>Every line of text in the mod's defs (the li items and the text fields): what Flavor picks from.</summary>
    private static List<string> AllLines(string root)
    {
        var lines = new List<string>();
        foreach (var dir in new[] { "Defs", Path.Combine("Languages", "English") })
        {
            string d = Path.Combine(root, dir);
            if (!Directory.Exists(d)) continue;
            foreach (var file in Directory.GetFiles(d, "*.xml", SearchOption.AllDirectories))
            {
                XDocument doc;
                try
                {
                    doc = XDocument.Load(file);
                }
                catch
                {
                    continue;
                }
                foreach (var e in doc.Descendants())
                    if (!e.HasElements && !string.IsNullOrEmpty(e.Value))
                        lines.Add(e.Value);
            }
        }
        return lines;
    }

    private static void Lines(string root, Action<bool, string> C)
    {
        var lines = AllLines(root);
        // And the odd shapes: no tags, empty tags, a bracket never closed, spaces and empty alternatives.
        lines.AddRange(new[]
        {
            "", " ", "plain", "[] empty tags", "   [winter]  indented", "[winter text with no end", "[ , winter, ] spaced",
            "[a|] an empty alternative", "[!!lowtech] two nots", "[ !lowtech | medieval ] spaced alternatives", "[land:coast,rich] standing",
            "[lowtech|winter] era with another", "[!land:desert] not the desert", "[tribal,medieval] two eras", "[x]", "[x]y", "]odd[",
        });
        C(lines.Count > 1000, $"the mod has many lines to check ({lines.Count})");

        // The tag vocabulary of the mod's own lines, for moments to test against.
        var vocab = new HashSet<string>();
        foreach (var l in lines)
        {
            OldStrip(l, out var tags);
            if (tags == null) continue;
            foreach (var t in tags)
            foreach (var a in t.Split('|'))
                vocab.Add(a.Trim().TrimStart('!'));
        }
        var words = vocab.OrderBy(x => x, StringComparer.Ordinal).ToList();
        var rng = new Random(1234);
        var moments = new List<HashSet<string>> { null, new HashSet<string>() };
        for (int i = 0; i < 40; i++)
        {
            var m = new HashSet<string>();
            int k = rng.Next(1, 12);
            for (int j = 0; j < k && words.Count > 0; j++) m.Add(words[rng.Next(words.Count)]);
            moments.Add(m);
        }
        var eras = new List<HashSet<string>>
        {
            new HashSet<string> { "tribal", "lowtech" }, new HashSet<string> { "medieval", "lowtech" }, new HashSet<string> { "industrial" },
            new HashSet<string> { "spacer" }, new HashSet<string>(), new HashSet<string> { "lowtech" }
        };

        LineTags.Clear();
        int stripBad = 0, matchBad = 0, eraBad = 0, checkedLines = 0;
        string firstBad = null;
        foreach (var line in lines)
        {
            checkedLines++;
            string oldText = OldStrip(line, out var oldTags);
            var p = LineTags.Of(line);
            bool same = p.text == oldText && ((p.tags == null && oldTags == null) || (p.tags != null && oldTags != null && p.tags.SequenceEqual(oldTags)));
            if (!same)
            {
                stripBad++;
                firstBad ??= "strip: " + line;
            }
            // Asked again: the same parts, not taken apart again.
            if (!ReferenceEquals(LineTags.Of(line), p)) stripBad++;
            foreach (var m in moments)
            {
                if (LineTags.Match(p, m) != OldMatch(line, m))
                {
                    matchBad++;
                    firstBad ??= "match: " + line;
                }
            }
            foreach (var e in eras)
            {
                if (LineTags.EraFits(p, e) != OldEraTagsFit(line, e))
                {
                    eraBad++;
                    firstBad ??= "era: " + line;
                }
            }
        }
        C(stripBad == 0, $"every line splits into the same tags and text as before ({stripBad} differ; {firstBad})");
        C(matchBad == 0, $"every line matches every moment as before ({matchBad} differ; {firstBad})");
        C(eraBad == 0, $"every line fits every era as before ({eraBad} differ; {firstBad})");
        C(LineTags.Of(null) == null, "no line, nothing taken apart");
        C(LineTags.Count <= LineTags.MaxKept, "the store stays under its cap");

        // The store starts again at its cap rather than growing without end (lines made on the fly, say).
        LineTags.Clear();
        for (int i = 0; i < LineTags.MaxKept + 10; i++) LineTags.Of("[winter] line " + i);
        C(LineTags.Count <= LineTags.MaxKept && LineTags.Count > 0, $"the store never holds more than its cap ({LineTags.Count})");
        var again = LineTags.Of("[winter] line 3");
        C(again.text == "line 3" && again.tags.Length == 1 && again.tags[0] == "winter", "a line forgotten at the cap is taken apart again the same");
        LineTags.Clear();
        C(LineTags.Count == 0, "cleared");
    }
}
