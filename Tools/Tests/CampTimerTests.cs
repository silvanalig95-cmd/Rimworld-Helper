using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// The camps' own clock (Camps/CampTimer.cs): the day's choice is made in the first open hour of the daytime window, not
/// only at ten (a question that came in the last hours shut most mornings, the player never saw a camp), and every day
/// goes into the camps' diary with a word for the player for each outcome.
/// </summary>
public static class CampTimerTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "camp timer: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil", "Camps");
        string timer = File.ReadAllText(Path.Combine(src, "CampTimer.cs"));
        string tracker = File.ReadAllText(Path.Combine(src, "CampTracker.cs"));
        C(!timer.Contains("!= Hour)") && timer.Contains("now < home.nextTryTick"), "a try at an hour of its own, not ten each morning");
        C(timer.Contains("CampStoryMath.PutOff(Passing.Contains(code), home.tryHours, Retries)"), "a passing reason only puts the try off by the hour, for up to a day");
        C(tracker.Contains("Scribe_Values.Look(ref nextTryTick"), "the next try is saved");
        // Which kind comes follows the hour (CampKindDef.arrives, CampStoryMath.ArrivalFit); the try hour itself is any hour alike.
        C(timer.Contains("Rand.RangeInclusive(0, 23)") && !timer.Contains("HourWeight"), "the try comes at any hour alike");
        C(ColonyCouncil.CampStoryMath.InHours(6, 4, 9) && !ColonyCouncil.CampStoryMath.InHours(12, 4, 9), "dawn hours");
        C(ColonyCouncil.CampStoryMath.InHours(23, 20, 3) && ColonyCouncil.CampStoryMath.InHours(2, 20, 3) && !ColonyCouncil.CampStoryMath.InHours(12, 20, 3), "night hours across midnight");
        C(ColonyCouncil.CampStoryMath.ArrivalFit(12, -1, -1) == 1f, "no hours of its own: any hour alike");
        C(ColonyCouncil.CampStoryMath.ArrivalFit(6, 4, 9) > 1f && ColonyCouncil.CampStoryMath.ArrivalFit(14, 4, 9) > 0f && ColonyCouncil.CampStoryMath.ArrivalFit(14, 4, 9) < 1f,
            "likelier in its hours, still possible outside");
        C(tracker.Contains("k.WeightNow(map) * k.HourFit(hourNow)"), "the kind is picked by the hour");
        // Every kind that keeps hours keeps sensible ones.
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Defs", "CampDefs"), "*.xml"))
            foreach (Match m in Regex.Matches(File.ReadAllText(f), @"<arrives>(\d+)~(\d+)</arrives>"))
                C(int.Parse(m.Groups[1].Value) <= 23 && int.Parse(m.Groups[2].Value) <= 23, $"arrival hours within the day ({m.Value})");
        C(Regex.Matches(timer, @"home\.Record\(").Count >= 5, "every way the day ends goes into the diary");
        C(tracker.Contains("Scribe_Collections.Look(ref diary, \"diary\"") && tracker.Contains("DiaryMax"), "the diary is saved and kept short");
        // Every code the diary can hold has a word for the player.
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Camps.xml"));
        var codes = new[] { "waiting", "lettergap", "checks", "failed", "off", "here", "full", "nohome", "early", "gap", "threat", "weather", "hazard", "era", "nofriends", "noroom" };
        foreach (var code in codes) C(keyed.Contains("<CC_Camp_Diary_" + code + ">"), $"the diary's word for {code}");
        var blockerCodes = Regex.Matches(tracker, @"return \(""([a-z]+)"",").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();
        foreach (var code in blockerCodes) C(codes.Contains(code), $"the blocker {code} has a diary word");
        C(blockerCodes.Count >= 10, $"found the blockers ({blockerCodes.Count})");
        return n;
    }
}
