using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Answers that should have played out and didn't (the player's save, 6 November 2026: 9 of the last 30). The shearing
/// day was asked with the wool half grown and its party found nothing to shear; a sowing party asked only the forty
/// cells nearest its spot and missed the fields still waiting for seed; and the reasons told said "nowhere to hold it"
/// for the weather and "something else had everyone's attention" for a fight, the wounded or another gathering alike.
/// </summary>
public static class FellThroughTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "fell through: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var keyed = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var e in XDocument.Load(f).Root!.Elements())
                keyed[e.Name.LocalName] = e.Value;

        // The shearing day: a coat to shear.
        Check(AnimalMath.ReadyToGather(0.95f) && AnimalMath.ReadyToGather(1f) && !AnimalMath.ReadyToGather(0.5f) && !AnimalMath.ReadyToGather(0.09f),
            "a coat or an udder nearly full is ready; one sheared a day ago is not");
        string dlm = Read(Path.Combine("Animals", "AnimalDilemmas.cs"));
        Check(dlm.Contains("h.shearOrMilk && !AnimalFacts.ReadyToShear(a) && !AnimalFacts.ReadyToMilk(a)"), "the herd's customs ask for an animal ready for the work, not one that could be");

        // The work party's cells.
        string work = Read(Path.Combine("PlayOut", "PlayWork.cs"));
        Check(Regex.IsMatch(work, @"InScope\(t, c, map, center\) && MayNeed\(t, c, map\)\)\.OrderBy\(c => c\.DistanceToSquared\(focus\)\)\.Take\(40\)"),
            "the nearest forty cells are asked only of those that could hold the work (a field sown near the spot hid the rest)");
        Check(work.Contains("&& MayNeed(t, th))") && work.Contains("private static bool MayNeed(WorkTask t, IntVec3 c, Map map)"), "and so are the things (an animal ready for the shears)");
        Check(work.Contains("\"CC_Play_Why_Sown\"") && keyed.ContainsKey("CC_Play_Why_Sown"), "a sowing whose fields were already sown says so");

        // The reasons told.
        Check(PlayOutMath.GatherWhyKey(1) == "CC_Play_Why_Danger" && PlayOutMath.GatherWhyKey(2) == "CC_Play_Why_Gathering"
              && PlayOutMath.GatherWhyKey(3) == "CC_Play_Why_Drafted" && PlayOutMath.GatherWhyKey(4) == "CC_Play_Why_Bleeding", "what kept people from gathering, each in its words");
        foreach (var k in new[] { "CC_Play_Why_Danger", "CC_Play_Why_Gathering", "CC_Play_Why_Drafted", "CC_Play_Why_Bleeding", "CC_Play_Why_Weather", "CC_Play_Why_Place" })
            Check(keyed.TryGetValue(k, out var v) && v.Length > 0 && char.IsLower(v[0]) && !v.EndsWith("."), $"{k} fits \"never quite happened (...)\"");
        Check(PlayOutMath.PlaceWhyKey(true) == "CC_Play_Why_Weather" && PlayOutMath.PlaceWhyKey(false) == "CC_Play_Why_Place", "a place in the open the weather kept them from is the weather");
        string plays = Read(Path.Combine("PlayOut", "PlayOuts.cs"));
        Check(plays.Contains("PlayOutMath.GatherWhyKey(bar)") && !plays.Contains("MeetingAttendance.WhyNotNow(map) != null ? \"CC_Play_Why_Gathering\""), "the gathering's reason is the one that held");
        string tracker = Read(Path.Combine("PlayOut", "PlayOutTracker.cs"));
        Check(Regex.Matches(tracker, @"PlayOutMath\.PlaceWhyKey\(PlaySpots\.KeptByWeather").Count >= 2, "a gathering and a visit kept in by the weather say so");
        return cases;
    }
}
