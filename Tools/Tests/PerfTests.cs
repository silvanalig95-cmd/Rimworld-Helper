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
        Kept(C);
        return n;
    }

    // ---------------- Answers kept for a while ----------------

    private static void Kept(Action<bool, string> C)
    {
        // Stretches of time: the same answer within one, worked out again in the next, never across a step back.
        C(Fresh.Bucket(0, 250) == 0 && Fresh.Bucket(249, 250) == 0 && Fresh.Bucket(250, 250) == 1, "250-tick stretches start at 0, 250, 500");
        C(Fresh.Bucket(-1, 250) == -1 && Fresh.Bucket(-250, 250) == -1 && Fresh.Bucket(-251, 250) == -2, "stretches round down below zero");
        C(Fresh.Bucket(7, 1) == 7 && Fresh.Bucket(7, 0) == 7, "a stretch of one tick is the tick");
        C(!Fresh.Holds(Fresh.Never, 0, 2500), "never worked out never holds");
        C(Fresh.Holds(2500, 4999, 2500) && !Fresh.Holds(2500, 5000, 2500), "an hour's answer holds to the end of the hour, not into the next");
        C(Fresh.Holds(71, 249, 250) && !Fresh.Holds(71, 250, 250), "a 250-tick answer holds to the end of its stretch");
        C(!Fresh.Holds(3000, 2600, 250) && Fresh.Holds(3000, 3100, 250), "the clock going back (an older save) means working it out again");
        C(Fresh.Holds(100, 4, 200, 4, 250) && !Fresh.Holds(100, 4, 200, 5, 250), "a changed source means working it out again");

        // The heart's map context and the map facts agree on the threat flag at the heart's own turns: every slice (71 + 250k)
        // is in a stretch of its own, so the flag is worked out afresh at each, as the heart's rolling 250 ticks had it.
        int lastAt = Fresh.Never, worked = 0;
        for (int tick = 0; tick < Sched.HourTicks * 2; tick++)
        {
            if (Sched.SliceTurn(tick, Sched.HourTicks, Sched.Slices, Sched.RareHeart) < 0) continue;
            if (!Fresh.Holds(lastAt, tick, 250))
            {
                worked++;
                lastAt = tick;
            }
        }
        C(worked == 20, $"the threat flag is worked out at every one of the heart's 20 turns in two hours ({worked})");

        // Kept answers per colonist: within the stretch and the same version, the kept one; otherwise worked out again.
        var kept = new KeptAnswers<int>(250);
        C(!kept.TryGet(5, 100, 0, out _), "nothing kept at first");
        kept.Set(5, 100, 0, 42);
        C(kept.TryGet(5, 249, 0, out int v) && v == 42, "kept within its stretch");
        C(!kept.TryGet(5, 250, 0, out _), "worked out again in the next stretch");
        C(!kept.TryGet(5, 120, 1, out _), "worked out again when its source changed");
        C(!kept.TryGet(6, 120, 0, out _), "another colonist's answer is their own");
        C(kept.TryGet(5, 99, 0, out _) && !kept.TryGet(5, -1, 0, out _), "a step back within the stretch keeps it, out of the stretch doesn't");
        kept.Set(5, 300, 1, 7);
        C(kept.TryGet(5, 310, 1, out v) && v == 7, "set again, the new answer is kept");
        for (int i = 0; i < KeptAnswers<int>.MaxKeys + 5; i++) kept.Set(1000 + i, 300, 1, i);
        C(kept.Count <= KeptAnswers<int>.MaxKeys, $"never more than {KeptAnswers<int>.MaxKeys} kept ({kept.Count})");
        kept.Clear();
        C(kept.Count == 0 && !kept.TryGet(1001, 300, 1, out _), "cleared");
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
