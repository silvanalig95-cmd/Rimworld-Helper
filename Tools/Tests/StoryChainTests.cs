using System.Xml.Linq;

/// <summary>
/// Rimfolk's story chains told from data (HappeningWorker_StoryFlow, Defs/IncidentDefs/Council_Events_Stories*.xml) and
/// the event variants (LetterVariantsDef): nothing at load time checks where a route leads, so this does. Every chain
/// has one first stage; every route leads to a stage of the same chain or to an ending the first stage defines, from
/// an answer the stage has; every flag a route asks for is one the chain can set; every ending has its letter, its
/// title and its line in the chronicle; later stages never fire by chance. Variants add outcomes only to answers that
/// exist.
/// </summary>
public static class StoryChainTests
{
    private class Stage
    {
        public string defName;
        public string chain;
        public string key;
        public bool start;
        public int options;
        public float baseChance;
        public XElement flow;
        public XElement text;
        public string file;
    }

    private static XElement Ext(XElement def, string cls) =>
        def.Element("modExtensions")?.Elements("li").FirstOrDefault(li => ((string)li.Attribute("Class") ?? "").EndsWith(cls));

    private static IEnumerable<string> Lis(XElement el, string name) =>
        el?.Element(name)?.Elements("li").Select(li => li.Value.Trim()) ?? Enumerable.Empty<string>();

    /// <summary>The tags of a line's leading block ("[joined,piece] ..." gives joined and piece; "a|b" gives both).</summary>
    private static IEnumerable<string> Tags(string line)
    {
        if (!line.StartsWith("[")) yield break;
        int end = line.IndexOf(']');
        if (end < 0) yield break;
        foreach (var part in line.Substring(1, end - 1).Split(','))
            foreach (var alt in part.Split('|'))
                yield return alt.Trim().TrimStart('!');
    }

    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "story chains: " + what);
        }

        var stages = new List<Stage>();
        var optionCounts = new Dictionary<string, int>();
        var variants = new List<(string def, string incident, int option, string file)>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories))
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
            string rel = Path.GetFileName(file);
            foreach (var d in doc.Root.Elements())
            {
                if (d.Name.LocalName == "IncidentDef")
                {
                    var ext = Ext(d, "HappeningExtension");
                    if (ext == null) continue;
                    string name = (string)d.Element("defName");
                    int opts = ext.Element("options")?.Elements("li").Count() ?? 0;
                    if (name != null) optionCounts[name] = opts;
                    var text = Ext(d, "StoryText");
                    if (text == null) continue;
                    stages.Add(new Stage
                    {
                        defName = name, chain = (string)text.Element("chain"), key = (string)text.Element("key"),
                        start = (string)text.Element("start") == "true", options = opts,
                        baseChance = float.TryParse((string)d.Element("baseChance"), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var bc) ? bc : 1f,
                        flow = Ext(d, "StoryFlow"), text = text, file = rel
                    });
                }
                else if (d.Name.LocalName == "ColonyCouncil.LetterVariantsDef")
                {
                    string inc = (string)d.Element("incident");
                    if (inc == null) continue;
                    foreach (var o in d.Element("outcomes")?.Elements("li") ?? Enumerable.Empty<XElement>())
                        if (int.TryParse((string)o.Element("option"), out int i)) variants.Add(((string)d.Element("defName"), inc, i, rel));
                }
            }
        }

        // ---------- Variants add outcomes only to answers that exist ----------
        foreach (var v in variants)
            Check(optionCounts.TryGetValue(v.incident, out int n) && v.option >= 0 && v.option < n, $"{v.file}: {v.def} adds to answer {v.option} of {v.incident}, which has it");

        // ---------- Chains ----------
        foreach (var chain in stages.Where(s => s.chain != null).GroupBy(s => s.chain))
        {
            var all = chain.ToList();
            var starts = all.Where(s => s.start).ToList();
            Check(starts.Count == 1, $"chain {chain.Key} has one first stage ({starts.Count})");
            if (starts.Count != 1) continue;
            var home = starts[0];
            Check(home.baseChance > 0f, $"chain {chain.Key}: its first stage can be drawn ({home.defName})");
            foreach (var s in all.Where(s => !s.start))
                Check(s.baseChance <= 0f, $"chain {chain.Key}: {s.defName} only comes as part of the chain");
            Check(all.Select(s => s.key).Distinct().Count() == all.Count, $"chain {chain.Key}: every stage has its own key");

            // Only the chains told from data have routes to read.
            if (home.flow == null || all.Any(s => s.flow == null)) continue;

            var endings = home.flow.Element("endings")?.Elements("li").Select(e => (string)e.Element("key")).Where(k => k != null).ToHashSet() ?? new HashSet<string>();
            Check(endings.Count > 0, $"chain {chain.Key} has endings");

            // Every flag the chain can set: its luck, the routes' flags, and what the colony is known for.
            var flags = new HashSet<string>();
            var luck = new HashSet<string>();
            foreach (var r in home.flow.Element("rolls")?.Elements("li") ?? Enumerable.Empty<XElement>())
            {
                flags.Add((string)r.Element("flag"));
                luck.Add((string)r.Element("flag"));
            }
            foreach (var s in all)
                foreach (var r in s.flow.Element("routes")?.Elements("li") ?? Enumerable.Empty<XElement>())
                    foreach (var f in Lis(r, "flags"))
                        flags.Add(f);
            bool colonyFlags = (string)home.flow.Element("colonyFlags") == "true";

            var byName = all.ToDictionary(s => s.defName, s => s);
            var reached = new HashSet<string> { home.defName };
            var endsReached = new HashSet<string>();
            foreach (var s in all)
            {
                var routes = s.flow.Element("routes")?.Elements("li").ToList() ?? new List<XElement>();
                Check(routes.Count > 0, $"{s.defName}: has somewhere to go");
                foreach (var r in routes)
                {
                    string next = (string)r.Element("next");
                    Check(!string.IsNullOrEmpty(next), $"{s.defName}: every route leads somewhere");
                    if (string.IsNullOrEmpty(next)) continue;
                    if (next.StartsWith("end:"))
                    {
                        string key = next.Substring(4);
                        Check(endings.Contains(key), $"{s.defName}: the ending {key} is one {home.defName} defines");
                        endsReached.Add(key);
                    }
                    else
                    {
                        Check(byName.ContainsKey(next), $"{s.defName}: {next} is a stage of chain {chain.Key}");
                        reached.Add(next);
                    }
                    if (int.TryParse((string)r.Element("option"), out int opt) && opt >= 0)
                        Check(opt < s.options, $"{s.defName}: a route from answer {opt}, which the stage has ({s.options})");
                    foreach (var field in new[] { "requires", "forbids" })
                    {
                        string need = (string)r.Element(field);
                        if (string.IsNullOrEmpty(need)) continue;
                        foreach (var f in need.Split('|'))
                            Check(flags.Contains(f) || (colonyFlags && f.StartsWith("colony:")), $"{s.defName}: the flag {f} a route asks for is one the chain sets");
                    }
                }
                // Every answer goes somewhere (a later route without an answer catches the rest).
                bool catchAll = routes.Any(r => !int.TryParse((string)r.Element("option"), out int o) || o < 0);
                for (int i = 0; i < s.options; i++)
                    Check(catchAll || routes.Any(r => (string)r.Element("option") == i.ToString()), $"{s.defName}: answer {i} leads somewhere");
            }
            foreach (var s in all)
                Check(reached.Contains(s.defName), $"chain {chain.Key}: {s.defName} can be reached");
            string fallback = (string)home.flow.Element("fallback");
            if (fallback != null)
            {
                Check(endings.Contains(fallback), $"chain {chain.Key}: its fallback {fallback} is an ending");
                endsReached.Add(fallback);
            }
            foreach (var k in endings)
                Check(endsReached.Contains(k), $"chain {chain.Key}: the ending {k} can be reached");

            // Every ending is told: a letter, a title and a line in the chronicle.
            var text = home.text;
            foreach (var k in endings)
            {
                Check(Lis(text, "endings").Any(l => Tags(l).Contains(k)), $"chain {chain.Key}: the ending {k} has its letter");
                Check(Lis(text, "endingTitles").Any(l => Tags(l).Contains(k)), $"chain {chain.Key}: the ending {k} has its title");
                Check(Lis(text, "summaries").Any(l => Tags(l).Contains(k)), $"chain {chain.Key}: the ending {k} has its line in the chronicle");
                // A letter for any way the luck fell: one that needs nothing else, or one for each way it can fall.
                var tagSets = Lis(text, "endings").Select(l => Tags(l).ToHashSet()).Where(t => t.Contains(k)).ToList();
                bool plain = tagSets.Any(t => t.Count == 1);
                bool everyLuck = luck.Count > 0 && luck.All(f => tagSets.Any(t => t.Count == 2 && t.Contains(f)));
                Check(plain || everyLuck, $"chain {chain.Key}: the ending {k} has a letter however the luck fell");
            }
        }
        Check(stages.Count(s => s.flow != null) >= 9, $"found the stages told from data ({stages.Count(s => s.flow != null)})");
        return cases;
    }
}
