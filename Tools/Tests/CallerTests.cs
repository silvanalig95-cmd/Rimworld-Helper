using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Visitors who come to do something (Visitors/CallerMath.cs, Visitors/Callers.cs): the prices and amounts the letters
/// name, who wants a cut, and from the defs: each event's jobs exist, every answer that sets work going names a known
/// effect, and the lines said at the work are one short sentence each.
/// </summary>
public static class CallerTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "callers: " + what);
        }

        // ---------- Prices ----------
        C(CallerMath.SalePrice(100f) == 160 && CallerMath.SalePrice(10f) == 50, $"the collector offers well above a piece's worth, never under 50 ({CallerMath.SalePrice(100f)})");
        C(CallerMath.SalePrice(333f) % 5 == 0, "offers are in fives");
        C(CallerMath.CommissionPrice(500f) == 700 && CallerMath.CommissionPrice(5f) == 40, "a commission is paid by its worth, at least 40");
        C(CallerMath.CommissionPrice(200f) < CallerMath.SalePrice(200f), "a commission pays less over worth than buying a treasured piece");
        C(CallerMath.FeastFood(3) == 60 && CallerMath.FeastFood(14) == 140, "a feast takes a meal's raw food for each who eats, at least sixty");
        C(CallerMath.FeastSilver(140) == 110 && CallerMath.FeastSilver(10) == 20, "the families pay about the food's worth");
        C(CallerMath.GraveFee(4) == 80 && CallerMath.GraveFee(1) == 40, "the undertakers ask by the grave, at least 40");
        C(CallerMath.Ransom(500f) == 300 && CallerMath.Ransom(20f) == 60 && CallerMath.Ransom(5000f) == 800, "kin bring a share of the captive's worth, between 60 and 800");
        C(CallerMath.CutPrice(true) > CallerMath.CutPrice(false), "a cut costs more past the old ways");
        C(CallerMath.VetFee(0, true) == 30 && CallerMath.VetFee(3, true) == 90 && CallerMath.VetFee(2, false) == 40, "the animal doctor asks by the animal, and for the training");
        C(CallerMath.FeedFor(60, 1.2f) == 60 && CallerMath.FeedFor(30, 0.3f) == 120 && CallerMath.FeedFor(30, 0f) > 0, "the fee in feed buys a fifth more feed than the silver would");
        C(CallerMath.CutTicks(true) > CallerMath.CutTicks(false), "a beard takes longer");

        // ---------- Who wants a cut ----------
        int want = 0;
        for (int id = 0; id < 1000; id++)
            if (CallerMath.WantsCut(id, 77)) want++;
        C(want > 500 && want < 700, $"about three in five want a cut on a day ({want} of 1000)");
        C(Enumerable.Range(0, 50).All(id => CallerMath.WantsCut(id, 9) == CallerMath.WantsCut(id, 9)), "the same person on the same day wants the same");
        C(Enumerable.Range(0, 50).Any(id => CallerMath.WantsCut(id, 9) != CallerMath.WantsCut(id, 10)), "the next day is another matter");
        C(Enumerable.Range(-20, 40).All(id => CallerMath.WantsCut(id * 1000003, 5) || !CallerMath.WantsCut(id * 1000003, 5)), "any id is safe");

        // ---------- The defs ----------
        var jobs = XDocument.Load(Path.Combine(root, "Defs/Misc/Rimfolk_Callers.xml")).Root!.Elements("JobDef").Select(j => j.Element("defName")?.Value).ToList();
        string src = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Visitors/Callers.cs"));
        foreach (Match m in Regex.Matches(src, @"public static JobDef (\w+);"))
            C(jobs.Contains(m.Groups[1].Value), $"the job {m.Groups[1].Value} has its def");
        string effects = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Happenings/Workers_Callers.cs"));
        var events = XDocument.Load(Path.Combine(root, "Defs/IncidentDefs/Rimfolk_Events_Callers.xml"));
        foreach (var e in events.Descendants("li").Where(l => ((string)l.Attribute("Class") ?? "").StartsWith("ColonyCouncil.DilemmaEffect_")))
        {
            string cls = ((string)e.Attribute("Class")).Substring("ColonyCouncil.".Length);
            if (!effects.Contains("class " + cls + " ")) continue;
            // Each of this file's own effects says what it does.
            int at = effects.IndexOf("class " + cls + " ", StringComparison.Ordinal);
            int next = effects.IndexOf("\n    public class ", at + 10, StringComparison.Ordinal);
            string body = next < 0 ? effects.Substring(at) : effects.Substring(at, next - at);
            C(body.Contains("override void Describe("), $"{cls} says what it does");
        }
        foreach (var line in events.Descendants("lines").SelectMany(l => l.Elements("li")).Select(l => l.Value))
        {
            string body = Regex.Replace(line, @"^\[[^\]]*\]\s*", "");
            C(body.Length <= 90 && body.Count(c => c == '!') <= 1 && !body.StartsWith("\""), $"'{body}' is short");
            C(Regex.Matches(body, @"[.?!](\s|$)").Count <= 1, $"'{body}' is one sentence");
        }
        return n;
    }
}
