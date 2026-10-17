using System.Text.RegularExpressions;
using System.Xml.Linq;

/// <summary>
/// Log lines (Source/ColonyCouncil/Social/LogLines.cs): more lines for a moment, another interaction or a rule pack,
/// kept in separate files. Each names exactly one target that exists; every rule is "keyword->text"; and every symbol a
/// line uses can be resolved, so a typo can't show raw brackets in the social log: it is a rule of the target or of a
/// file adding to it, a person's symbol (INITIATOR_..., RECIPIENT_...), or one the target's own lines already use
/// without defining (the code fills those in).
/// </summary>
public static class LogLinesTests
{
    private static readonly Regex Rule = new Regex(@"^([A-Za-z_][A-Za-z0-9_]*)(\(.*\))?->(.*)$");
    private static readonly Regex Symbol = new Regex(@"\[([A-Za-z_][A-Za-z0-9_]*)\]");

    public static int Run(string root, Action<bool, string> check, out int lines)
    {
        int checks = 0;
        lines = 0;
        void Check(bool ok, string what)
        {
            checks++;
            check(ok, "log lines: " + what);
        }

        // Every interaction's and rule pack's own rules, by defName.
        var own = new Dictionary<string, List<string>>();
        var extensions = new List<(string file, XElement def)>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories))
            foreach (var el in XDocument.Load(file).Root!.Elements())
            {
                string kind = el.Name.LocalName;
                string name = el.Element("defName")?.Value;
                if (name == null) continue;
                if (kind == "ColonyCouncil.LogLinesDef") extensions.Add((Path.GetFileName(file), el));
                else if (kind == "InteractionDef")
                    own[name] = el.Element("logRulesInitiator")?.Element("rulesStrings")?.Elements("li").Select(li => li.Value.Trim()).ToList();
                else if (kind == "RulePackDef")
                    own[name] = el.Element("rulePack")?.Element("rulesStrings")?.Elements("li").Select(li => li.Value.Trim()).ToList();
            }
        Check(extensions.Count > 0, "found the log lines");

        // The lines each target gets from other files.
        var added = new Dictionary<string, List<string>>();
        foreach (var (file, el) in extensions)
        {
            string name = el.Element("defName")!.Value;
            string interaction = el.Element("interaction")?.Value, pack = el.Element("pack")?.Value;
            Check((interaction == null) != (pack == null), $"{name} names exactly one target");
            string target = interaction ?? pack;
            if (target == null) continue;
            Check(own.TryGetValue(target, out var targetRules) && targetRules != null, $"{name}: its target {target} exists and has lines of its own");
            var rules = el.Element("rulePack")?.Element("rulesStrings")?.Elements("li").Select(li => li.Value.Trim()).ToList() ?? new List<string>();
            Check(rules.Count > 0, $"{name} has lines");
            foreach (var r in rules) Check(Rule.IsMatch(r), $"{name}: '{r}' is a rule (keyword->text)");
            if (!added.TryGetValue(target, out var list)) added[target] = list = new List<string>();
            list.AddRange(rules);
            lines += rules.Count;
        }

        foreach (var (target, extra) in added)
        {
            if (!own.TryGetValue(target, out var mine) || mine == null) continue;
            var defined = new HashSet<string>();
            var usedByOwn = new HashSet<string>();
            foreach (var r in mine.Concat(extra))
            {
                var m = Rule.Match(r);
                if (m.Success) defined.Add(m.Groups[1].Value);
            }
            foreach (var r in mine)
            {
                var m = Rule.Match(r);
                if (!m.Success) continue;
                foreach (Match s in Symbol.Matches(m.Groups[3].Value)) usedByOwn.Add(s.Groups[1].Value);
            }
            // The root the target's lines are told by (r_logentry for interactions), so the new lines can be found.
            string rootKey = mine.Select(r => Rule.Match(r)).Where(m => m.Success).Select(m => m.Groups[1].Value).FirstOrDefault();
            foreach (var r in extra)
            {
                var m = Rule.Match(r);
                if (!m.Success) continue;
                foreach (Match s in Symbol.Matches(m.Groups[3].Value))
                {
                    string sym = s.Groups[1].Value;
                    bool person = sym.StartsWith("INITIATOR_") || sym.StartsWith("RECIPIENT_");
                    Check(person || defined.Contains(sym) || usedByOwn.Contains(sym), $"{target}: [{sym}] in '{r}' can be resolved");
                }
                Check(m.Groups[3].Value.Trim().Length > 0, $"{target}: '{r}' says something");
                Check(!m.Groups[3].Value.Contains('—'), $"{target}: no em dash in '{r}'");
            }
            Check(rootKey != null && defined.Contains(rootKey), $"{target}: its root rule {rootKey} is there");
            var allLines = mine.Concat(extra).ToList();
            Check(allLines.Distinct().Count() == allLines.Count, $"{target}: no line is listed twice ({allLines.GroupBy(x => x).FirstOrDefault(g => g.Count() > 1)?.Key})");
        }
        return checks;
    }
}
