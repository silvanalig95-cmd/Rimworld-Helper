using System.Globalization;
using System.Xml.Linq;

/// <summary>
/// Small moments that give something other than mood (Source/ColonyCouncil/Moods/RewardInstead.cs): a memory with a
/// RewardInstead gives no mood of its own, gives something (or is an opinion about someone, or its encounter writes the
/// journal line), stays modest, and feeds only feelings that exist.
/// </summary>
public static class RewardInsteadTests
{
    private static float F(XElement e, string name)
    {
        var v = e.Element(name)?.Value?.Trim();
        return string.IsNullOrEmpty(v) ? 0f : float.Parse(v, CultureInfo.InvariantCulture);
    }

    public static int Run(string root, Action<bool, string> check, out int converted, out int small)
    {
        var thoughts = new List<XElement>();
        var feelings = new HashSet<string>();
        var journaled = new HashSet<string>();
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
            thoughts.AddRange(doc.Root!.Elements("ThoughtDef"));
            foreach (var f in doc.Root.Elements("ColonyCouncil.FeelingDef"))
            {
                feelings.Add(f.Element("key")?.Value ?? "");
                feelings.Add(f.Element("defName")?.Value ?? "");
            }
            // Encounters that write their own journal line: the memories they give are written down already.
            foreach (var enc in doc.Root.Elements("ColonyCouncil.EncounterDef"))
                if (!string.IsNullOrEmpty(enc.Element("journal")?.Value))
                    foreach (var t in enc.Descendants("thought"))
                        journaled.Add(t.Value.Trim());
        }
        var named = new Dictionary<string, XElement>();
        foreach (var d in thoughts)
            if (d.Attribute("Name") != null)
                named[(string)d.Attribute("Name")] = d;
        bool Situational(XElement d)
        {
            for (int depth = 0; d != null && depth < 10; depth++)
            {
                if (!string.IsNullOrEmpty(d.Element("workerClass")?.Value)) return true;
                named.TryGetValue((string)d.Attribute("ParentName") ?? "", out d);
            }
            return false;
        }
        int checks = 0;
        converted = 0;
        small = 0;
        foreach (var d in thoughts)
        {
            string name = d.Element("defName")?.Value ?? "?";
            var stages = d.Element("stages")?.Elements("li").ToList() ?? new List<XElement>();
            var x = d.Element("modExtensions")?.Elements("li").FirstOrDefault(li => (string)li.Attribute("Class") == "ColonyCouncil.RewardInstead");
            float mood = stages.Count == 0 ? 0f : stages.Max(s => F(s, "baseMoodEffect"));
            bool opinion = stages.Any(s => F(s, "baseOpinionOffset") != 0f);
            if (x == null)
            {
                if ((string)d.Attribute("Abstract") != "True" && !Situational(d) && mood > 0f && mood <= 3f) small++;
                continue;
            }
            converted++;
            small++;
            checks += 8;
            check(!Situational(d), $"{name}: a reward instead of mood is for memories, not situational moods");
            check(stages.Count > 0 && stages.All(s => F(s, "baseMoodEffect") <= 0f), $"{name}: gives a reward instead of mood, but still gives mood");
            bool gives = F(x, "joy") > 0f || F(x, "rest") > 0f || F(x, "comfort") > 0f || F(x, "xp") > 0f || F(x, "inspiration") > 0f
                         || F(x, "trust") != 0f || F(x, "research") > 0f || x.Element("journal")?.Value == "true" || x.Element("feelings") != null;
            check(gives || opinion || journaled.Contains(name), $"{name}: gives no mood and nothing instead (no opinion, and no journal line from its encounter)");
            check(F(x, "felt") > 0f && F(x, "felt") <= 5f, $"{name}: felt {F(x, "felt")} should be the small moment's size (above 0, at most 5)");
            check(F(x, "joy") <= 0.2f && F(x, "rest") <= 0.3f && F(x, "comfort") <= 0.3f, $"{name}: a need gain instead of a small mood stays modest");
            check(F(x, "xp") <= 400f && F(x, "research") <= 150f && Math.Abs(F(x, "trust")) <= 3f, $"{name}: practice, research or trust instead of a small mood stays modest");
            check(F(x, "inspiration") <= 0.05f, $"{name}: an inspiration instead of mood stays rare (5% at most)");
            bool known = true;
            foreach (var f in x.Element("feelings")?.Elements("li") ?? Enumerable.Empty<XElement>())
                known &= feelings.Contains(f.Value.Trim());
            check(known, $"{name}: feeds a feeling that doesn't exist");
        }
        return checks;
    }
}
