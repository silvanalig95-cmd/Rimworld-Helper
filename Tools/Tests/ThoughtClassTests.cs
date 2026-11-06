using System.Xml.Linq;

/// <summary>
/// Every memory in Rimfolk's defs must use one of Rimfolk's memory classes (ICouncilMood), which say what mood they give
/// on their own, so good things growing ordinary and the small letdowns and joys read them alike. A memory with the game's
/// plain class would escape them. Situational moods (with a worker) aren't memories and may use any class.
/// </summary>
public static class ThoughtClassTests
{
    /// <summary>The classes that implement ICouncilMood.</summary>
    private static readonly HashSet<string> Budgeted = new HashSet<string>
    {
        "ColonyCouncil.Thought_CouncilMemory",
        "ColonyCouncil.Thought_CouncilSocial",
        "ColonyCouncil.Thought_Dream",
        // A quiet trouble's memory (Setbacks/Troubles.cs): a Thought_CouncilMemory that also says who made it pass sooner.
        "ColonyCouncil.Thought_Trouble",
        // A small letdown's or joy's moment (Moods/SmallMoments.cs): its own size, in its side's row.
        "ColonyCouncil.Thought_SmallMoment"
    };

    public static int Run(string root, Action<bool, string> check)
    {
        var all = new List<XElement>();
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
            all.AddRange(doc.Root!.Elements("ThoughtDef"));
        }
        var named = new Dictionary<string, XElement>();
        foreach (var d in all)
            if (d.Attribute("Name") != null)
                named[(string)d.Attribute("Name")] = d;
        string Inherit(XElement d, string field)
        {
            for (int depth = 0; d != null && depth < 10; depth++)
            {
                var v = d.Element(field)?.Value?.Trim();
                if (!string.IsNullOrEmpty(v)) return v;
                named.TryGetValue((string)d.Attribute("ParentName") ?? "", out d);
            }
            return null;
        }
        int memories = 0;
        foreach (var d in all)
        {
            if ((string)d.Attribute("Abstract") == "True") continue;
            if (Inherit(d, "workerClass") != null) continue;
            string name = d.Element("defName")?.Value ?? "?";
            string cls = Inherit(d, "thoughtClass");
            memories++;
            check(cls != null && Budgeted.Contains(cls), $"memory {name} uses {cls ?? "the game's plain class"}, which escapes the rules for good moods");
        }
        return memories;
    }
}
