using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The second round of visitors (Source/ColonyCouncil/Visitors/VisitorWork2Math.cs, VisitorsMore2.cs and
/// Happenings/Workers_Visitors2.cs): each kind of work has its hours and its job, a visit held for its work covers
/// that many of its hours, the regulars' lives turn only when they're known well, and the events' words have the lines,
/// thoughts and keys they name.
/// </summary>
public static class VisitorsMore2Tests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "visitors for what the colony keeps: " + what);
        }

        // ---------- The hours ----------
        foreach (var k in VisitorWork2Math.Kinds) C(VisitorWork2Math.Hours(k, out int f, out int t) && f < t && VisitorWork2Math.InHours(k, f) && !VisitorWork2Math.InHours(k, t), $"{k} has its hours");
        C(!VisitorWork2Math.Known(VisitorWork2Math.Day) && VisitorWork2Math.Hours(VisitorWork2Math.Day, out _, out _), "kept by day is not a work, but has its hours");
        C(!VisitorWork2Math.Known("prospect") && !VisitorWork2Math.InHours("prospect", 12), "the first round's work is the first round's");
        C(VisitorWork2Math.InHours(VisitorWork2Math.Relic, 30) == VisitorWork2Math.InHours(VisitorWork2Math.Relic, 6), "hours past midnight wrap round");
        C(VisitorWork2Math.HoursToCover(VisitorWork2Math.Relic, 10, 5) == 6, $"answered in the morning: five hours of prayer and the walk ({VisitorWork2Math.HoursToCover(VisitorWork2Math.Relic, 10, 5)})");
        C(VisitorWork2Math.HoursToCover(VisitorWork2Math.Relic, 23, 4) == 12, $"answered at night: they wait for the morning ({VisitorWork2Math.HoursToCover(VisitorWork2Math.Relic, 23, 4)})");
        C(VisitorWork2Math.HoursToCover(VisitorWork2Math.Gawk, 20, 3) == 15, $"a look round the hull waits for daylight ({VisitorWork2Math.HoursToCover(VisitorWork2Math.Gawk, 20, 3)})");
        C(VisitorWork2Math.HoursToCover(VisitorWork2Math.Meditate, 12, 100) == VisitorWork2Math.MaxHours, "never held longer than the cap");
        C(VisitorWork2Math.HoursToCover("nothing", 12, 4) == 0 && VisitorWork2Math.HoursToCover(VisitorWork2Math.Read, 12, 0) == 0, "nothing to do holds nobody");
        for (int h = 0; h < 24; h++)
        {
            int cover = VisitorWork2Math.HoursToCover(VisitorWork2Math.Pray, h, 3);
            int worked = Enumerable.Range(0, cover).Count(i => VisitorWork2Math.InHours(VisitorWork2Math.Pray, h + i));
            C(worked >= 3, $"answered at {h}: three hours of prayer fit in the {cover} they stay");
        }

        // ---------- The regulars' lives ----------
        C(RegularLifeMath.Warm(3, 3) && !RegularLifeMath.Warm(2, 6) && !RegularLifeMath.Warm(5, 2), "a regular's life is news only after three visits and a warm bond");
        C(RegularLifeMath.Elder(50f) && !RegularLifeMath.Elder(49.5f), "the apprentice comes with someone of fifty or more");
        C(RegularLifeMath.Old(72f) && !RegularLifeMath.Old(71.9f), "death of age alone only at a great age");

        // ---------- The defs ----------
        var misc = XDocument.Load(Path.Combine(root, "Defs/Misc/Rimfolk_VisitorsMore2.xml"));
        var jobWork = misc.Root!.Elements("JobDef").Select(j => j.Descendants("work").FirstOrDefault()?.Value).Where(w => w != null).ToList();
        foreach (var k in VisitorWork2Math.Kinds) C(jobWork.Contains(k), $"{k} has a job that does it");
        string src = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Visitors/VisitorsMore2.cs"));
        foreach (var j in misc.Root.Elements("JobDef").Select(j => j.Element("defName")?.Value)) C(src.Contains("public static JobDef " + j + ";"), $"{j} is looked up by the code");
        string work = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Visitors/VisitorWork.cs"));
        C(work.Contains("VisitorWork2Math.InHours(work, hour)") && work.Contains("VisitorWork2.JobFor(p, work)") && work.Contains("VisitorWork2.Done(p, work, at)"), "the visit work hands these kinds on");

        var lines = XDocument.Load(Path.Combine(root, "Defs/FlavorPoolDefs/Rimfolk_VisitorLines2.xml")).Descendants("li").Select(l => l.Value).ToList();
        foreach (var k in VisitorWork2Math.Kinds) C(lines.Count(l => Regex.IsMatch(l, @"^\[([a-zA-Z]+,)*" + k + @"(,[a-zA-Z]+)*\]")) >= 3, $"visitors have lines for {k}");
        C(lines.All(l => l.StartsWith("[")), "every line is tagged with its moment, so none is said at the wrong one");

        var events = XDocument.Load(Path.Combine(root, "Defs/IncidentDefs/Rimfolk_Events_VisitorsMore2.xml"));
        var thoughts = XDocument.Load(Path.Combine(root, "Defs/ThoughtDefs/Rimfolk_Thoughts_VisitorsMore2.xml")).Root!.Elements("ThoughtDef").Select(t => t.Element("defName")?.Value).ToHashSet();
        string workers = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Happenings/Workers_Visitors2.cs"));
        var keyed = XDocument.Load(Path.Combine(root, "Languages/English/Keyed/Rimfolk_VisitorsMore2.xml")).Root!.Elements().Select(e => e.Name.LocalName).ToHashSet();
        foreach (var d in events.Root!.Elements("IncidentDef"))
        {
            string name = d.Element("defName")?.Value ?? "?";
            string worker = (d.Element("workerClass")?.Value ?? "").Replace("ColonyCouncil.", "");
            C(workers.Contains("public class " + worker + " "), $"{name}: its worker is in Workers_Visitors2.cs");
            foreach (var t in d.Descendants("thought")) C(thoughts.Contains(t.Value) || t.Value.StartsWith("Council_"), $"{name}: thought {t.Value} exists");
            foreach (var t in d.Descendants().Where(e => e.Name == "believers" || e.Name == "others")) C(thoughts.Contains(t.Value), $"{name}: thought {t.Value} exists");
            foreach (var w in d.Descendants("li").Where(l => (string)l.Attribute("Class") == "ColonyCouncil.VisitEffect_WorkHere"))
            {
                string kind = w.Element("work")?.Value ?? VisitorWork2Math.Day;
                C(keyed.Contains("CC_Impact_V2Work_" + kind), $"{name}: what {kind} does is told");
            }
            foreach (var o in d.Descendants("options").Elements("li"))
            {
                // An answer that sets visitors doing something with a colonist keeps them there for it.
                var cls = o.Descendants("li").Select(l => (string)l.Attribute("Class") ?? "").ToList();
                bool withThem = cls.Any(c => c.EndsWith("DilemmaEffect_Lesson")) || o.Descendants("to").Any(t => t.Value == "Stranger");
                bool held = cls.Any(c => c.EndsWith("VisitEffect_WorkHere") || c.EndsWith("VisitEffect_Stay") || c.EndsWith("DilemmaEffect_CarryOut"));
                if (withThem) C(held, $"{name} \"{o.Element("label")?.Value}\": the visitors stay for what the answer does with them");
                string words = string.Join(" ", o.Elements("outcome").Concat(o.Elements("label")).Select(e => e.Value));
                C(!words.Contains("—"), $"{name}: no em dashes");
            }
        }
        return n;
    }
}
