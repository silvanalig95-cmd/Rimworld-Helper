using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Camps that come for something on the land (Camps/CampLandMath.cs, Defs/CampDefs/Rimfolk_CampKinds_Land.xml): whether
/// the growers are behind with the harvest, the reapers' sheaf in ten adding up over small yields, a day's fishing and
/// stripping, half a haul, a beast's price; and every land kind names a need and a work the code knows, every ask of
/// theirs a need the code checks, and the run brings the fishers.
/// </summary>
public static class CampLandTests
{
    private static readonly string[] Needs = { "fish", "ruins", "harvest", "edge", "monolith" };
    private static readonly string[] Works = { "fish", "strip", "reap", "hull", "watch" };

    private static readonly string[] AskNeeds =
    {
        "theirCatch", "farWater", "theirHaul", "nearRuin", "notHired", "ourBeast", "children", "study", "hull", "storm",
        // The older asks' own (Camps/CampAsks.cs).
        "sick", "evening", "theirMeat", "cart", "theirWounded", "ourPokemon", "herdMilk"
    };

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void Check(bool ok, string what)
        {
            n++;
            check(ok, "land camps: " + what);
        }

        // ---------- The harvest ----------
        Check(CampLandMath.HarvestBehind(60f, 300, 2f, 10f), "60 hours of reaping against 2 growers' 30 spare hours over 10 days: behind");
        Check(!CampLandMath.HarvestBehind(20f, 300, 2f, 10f), "20 hours against 30 spare: the growers manage");
        Check(!CampLandMath.HarvestBehind(60f, 300, 2f, 30f), "the season's end a month off: no hurry yet");
        Check(!CampLandMath.HarvestBehind(60f, 300, 2f, 0f), "the season already over: too late");
        Check(!CampLandMath.HarvestBehind(60f, 20, 0f, 10f), "a handful of plants is no work for a crew");
        Check(CampLandMath.HarvestBehind(5f, 50, 0f, 10f), "nobody on growing work: any real crop is behind");
        Check(Math.Abs(CampLandMath.RipeHours(2500f) - 1.5f) < 0.001f, "a working hour of plant work takes an hour and a half with the walking");
        Check(CampLandMath.RipeHours(-5f) == 0f, "no work is no hours");

        // ---------- The sheaf in ten ----------
        float carry = 0f;
        int shared = 0, yielded = 0;
        for (int i = 0; i < 100; i++)
        {
            int s = CampLandMath.Share(6, CampLandMath.Sheaf, ref carry);
            Check(s >= 0 && s <= 6, $"plant {i}: a share of 0 to 6 ({s})");
            shared += s;
            yielded += 6;
        }
        Check(shared == 60, $"a hundred plants of 6 give 60 sheaves in all, one in ten ({shared})");
        carry = 0f;
        Check(CampLandMath.Share(25, CampLandMath.Sheaf, ref carry) == 2 && Math.Abs(carry - 0.5f) < 0.001f, "25 gives 2 now and half a sheaf carried over");
        Check(CampLandMath.Share(5, CampLandMath.Sheaf, ref carry) == 1 && carry < 0.001f, "the next 5 make up the sheaf");
        Check(CampLandMath.Share(0, CampLandMath.Sheaf, ref carry) == 0 && CampLandMath.Share(10, 0f, ref carry) == 0, "nothing yielded or no share: nothing taken");
        carry = 5f;
        Check(CampLandMath.Share(3, CampLandMath.Sheaf, ref carry) == 3, "never more than the plant gave");

        // ---------- Fishing, stripping, splitting, selling ----------
        Check(CampLandMath.FishTrips(3) == 6 && CampLandMath.FishTrips(0) == 0 && CampLandMath.FishTrips(-1) == 0, "two trips a day for each grown fisher");
        Check(CampLandMath.StripsADay(0) == 0 && CampLandMath.StripsADay(1) == 3 && CampLandMath.StripsADay(4) == 4 && CampLandMath.StripsADay(5) == 5, "a few pieces a day, more with more hands");
        Check(CampLandMath.Half(1) == 0 && CampLandMath.Half(2) == 1 && CampLandMath.Half(75) == 37, "half the haul, rounded down; a single piece isn't split");
        Check(CampLandMath.BeastPrice(0f) == 0, "a beast worth nothing fetches nothing");
        Check(CampLandMath.BeastPrice(300f) == 210, "a beast worth 300 fetches 210 (seven tenths, in tens)");
        Check(CampLandMath.BeastPrice(5f) == 10, "never less than ten");
        Check(CampLandMath.BeastPrice(1234f) % 10 == 0, "always in tens");

        // ---------- The defs ----------
        var file = Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKinds_Land.xml");
        Check(File.Exists(file), "the land camps' defs exist");
        if (!File.Exists(file)) return n;
        var doc = XDocument.Load(file).Root!;
        var kinds = doc.Elements("ColonyCouncil.CampKindDef").ToList();
        Check(kinds.Count == 6, $"six kinds of land camp ({kinds.Count})");
        var jobs = new HashSet<string>(doc.Elements("JobDef").Select(j => j.Element("defName")?.Value ?? ""));
        foreach (var j in new[] { "Rimfolk_CampFish", "Rimfolk_CampStrip", "Rimfolk_CampReap", "Rimfolk_CampTakeUp", "Rimfolk_CampHull", "Rimfolk_CampWatch" })
            Check(jobs.Contains(j), $"the job {j} exists for CampLandDefOf");
        foreach (var k in kinds)
        {
            string name = k.Element("defName")?.Value ?? "?";
            string need = k.Element("needs")?.Value;
            string work = k.Element("work")?.Value;
            Check(need == null || Needs.Contains(need), $"{name}: needs '{need}' is one the code knows");
            Check(work == null || Works.Contains(work), $"{name}: work '{work}' is one the code knows");
            foreach (var a in k.Element("asks")?.Elements("li") ?? Enumerable.Empty<XElement>())
            {
                string key = a.Element("key")?.Value ?? "?";
                string an = a.Element("needs")?.Value;
                Check(an == null || AskNeeds.Contains(an), $"{name} ask {key}: needs '{an}' is one the code checks");
                bool theyAsk = a.Element("theyAsk")?.Value == "true";
                Check(theyAsk ? a.Element("askTexts")?.Elements("li").Any() == true : a.Element("label") != null, $"{name} ask {key}: has its words");
                Check(a.Element("outcomes")?.Elements("li").Any() == true, $"{name} ask {key}: says what happened");
                Check(a.Element("effects")?.Elements("li").Any() == true, $"{name} ask {key}: does something");
            }
            foreach (var l in (k.Element("lines")?.Elements("li") ?? Enumerable.Empty<XElement>()).Concat(k.Element("momentLines")?.Elements("li") ?? Enumerable.Empty<XElement>()))
            {
                string body = l.Value;
                int close = body.StartsWith("[") ? body.IndexOf(']') : -1;
                if (close >= 0) body = body.Substring(close + 1).Trim();
                Check(body.Length <= 90 && body.Count(c => c == '.' || c == '?' || c == '!') <= 1 && !body.Contains('"'), $"{name}: '{body}' is one short sentence");
            }
        }
        var harvest = kinds.FirstOrDefault(k => k.Element("work")?.Value == "reap");
        var offer = harvest?.Element("asks")?.Elements("li").FirstOrDefault(a => a.Element("key")?.Value == "work");
        Check(offer != null && offer.Element("theyAsk")?.Value == "true", "the harvest hands offer their work by letter (the ask keyed work)");

        var run = Path.Combine(root, "Defs", "WorldSituationDefs", "Rimfolk_WS_Run.xml");
        Check(File.Exists(run), "the run's def exists");
        if (File.Exists(run))
        {
            var ws = XDocument.Load(run).Root!.Element("ColonyCouncil.WorldSituationDef");
            Check(ws?.Element("kind")?.Value == "run", "the run is of kind run");
            var fishers = kinds.FirstOrDefault(k => k.Element("work")?.Value == "fish");
            Check(fishers?.Element("situationKinds")?.Elements("li").Any(l => l.Value == "run") == true, "fishers come more often while the run lasts");
            Check(ws?.Descendants("kind").Any(e => e.Value == "Rimfolk_Camp_Fishers") == true, "a beat of the run brings the fishers' camp");
            var keys = ws?.Element("beats")?.Elements("li").Select(b => b.Element("key")?.Value).ToList() ?? new List<string>();
            foreach (var k in new[] { "camp", "catch", "quarrel", "thinning" }) Check(keys.Contains(k), $"the run has its {k} beat (its worker reads the key)");
        }
        return n;
    }
}
