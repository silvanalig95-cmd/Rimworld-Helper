using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

/// <summary>
/// Visitors do what their letters say (Source/ColonyCouncil/Visitors/VisitorWork.cs), read from the defs: every visit's
/// work is one the code knows, with its job; every answer whose words give a bed or a roof gives the stay one, and
/// every answer whose words keep them out by the wall doesn't.
/// </summary>
public static class VisitorWorkTests
{
    private static readonly string[] Roofed = { "under its roof", "under the colony's roof", "in the barn", "guest corner", "dry corner", "by the stove", "somewhere to sleep", "shown inside", "asked in for the night" };
    private static readonly string[] Outside = { "lee of the wall", "lee of the colony's wall", "sleeps by the gate", "sleep by the gate" };

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "visitors at their work: " + what);
        }
        string src = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Visitors/VisitorWork.cs"));
        var m = Regex.Match(src, @"public const string Prospect = ""([a-z]+)"", Glean = ""([a-z]+)"", Sing = ""([a-z]+)"", Hunt = ""([a-z]+)""");
        C(m.Success, "the kinds of work are read from the code");
        var kinds = Enumerable.Range(1, 4).Select(i => m.Groups[i].Value).ToList();
        foreach (var k in kinds) C(Regex.IsMatch(src, "case " + char.ToUpper(k[0]) + k.Substring(1) + @": return hour"), $"{k} has its hours");

        // Each kind has its job, with the work it's for.
        var misc = XDocument.Load(Path.Combine(root, "Defs/Misc/Rimfolk_Visitors.xml"));
        var jobWork = misc.Root!.Elements("JobDef").Select(j => j.Descendants("work").FirstOrDefault()?.Value).Where(w => w != null).ToList();
        foreach (var k in kinds) C(jobWork.Contains(k), $"{k} has a job that does it");
        C(misc.Root.Elements("DutyDef").Any(d => d.Element("defName")?.Value == "Rimfolk_VisitWork"), "the work has its duty");
        C(!misc.ToString().Contains("\"JobGiver_GetRest\""), "no visitor's duty lies down whenever it isn't fully rested");
        // Every visitor's evening by the fire and seat at the gate, and colonists who come to sit with them.
        foreach (var k in new[] { "fire", "sit" }) C(jobWork.Contains(k), $"{k} has a job that does it");
        C(misc.Root.Elements("JoyGiverDef").Any(j => j.Element("jobDef")?.Value == "Rimfolk_SitWithVisitors"), "colonists can go and sit with visitors by their fire");
        var waitDuty = misc.Root.Elements("DutyDef").FirstOrDefault(d => d.Element("defName")?.Value == "Rimfolk_VisitWait")?.ToString() ?? "";
        C(waitDuty.Contains("JobGiver_VisitFire") && waitDuty.Contains("JobGiver_VisitSit"), "those waiting or staying sit by their fire or on a seat");
        // What they say at their work: a find, a sack filling, the fire.
        var lines = XDocument.Load(Path.Combine(root, "Defs/FlavorPoolDefs/Rimfolk_VisitorLines.xml")).Descendants("li").Select(l => l.Value).ToList();
        foreach (var moment in new[] { "find", "glean", "fire" })
            C(lines.Any(l => Regex.IsMatch(l, @"^\[([a-zA-Z]+,)*" + moment + @"(,[a-zA-Z]+)*\]")), $"visitors have lines for {moment}");

        // Every visit's work is known; every roof the words give is given, and none they don't.
        int works = 0, roofs = 0;
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
            foreach (var e in doc.Descendants("li").Where(l => ((string)l.Attribute("Class") ?? "").StartsWith("ColonyCouncil.VisitEffect_")))
            {
                string w = e.Element("work")?.Value;
                if (w != null)
                {
                    works++;
                    C(kinds.Contains(w), $"{Path.GetFileName(file)}: work {w} is known");
                }
                if ((string)e.Attribute("Class") != "ColonyCouncil.VisitEffect_Stay") continue;
                string outcome = e.Parent?.Parent?.Element("outcome")?.Value;
                if (outcome == null) continue;
                bool inside = e.Element("inside")?.Value == "true";
                if (inside) roofs++;
                string label = e.Parent?.Parent?.Element("label")?.Value ?? "?";
                if (Roofed.Any(outcome.Contains)) C(inside, $"{Path.GetFileName(file)} \"{label}\": its words give a roof, and so does the stay");
                if (Outside.Any(outcome.Contains)) C(!inside, $"{Path.GetFileName(file)} \"{label}\": its words keep them by the wall, and so does the stay");
                string room = e.Element("room")?.Value;
                if (room != null) C(room == "Barn" && outcome.Contains("barn"), $"{Path.GetFileName(file)} \"{label}\": a room of the role the words name");
            }
        }
        C(works >= 6, $"visits with work to do are found ({works})");
        C(roofs >= 7, $"stays under a roof are found ({roofs})");
        return n;
    }
}
