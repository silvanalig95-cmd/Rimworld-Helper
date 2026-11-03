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
        C(timer.Contains("hour < FirstHour || hour > LastHour") && !timer.Contains("!= Hour)"), "a daytime window, not one hour");
        C(timer.Contains("tracker.rolledDay == day"), "one choice a day");
        C(timer.Contains("Passing.Contains(code) && hour < LastHour"), "a passing reason only puts the choice off to a later hour");
        C(Regex.Matches(timer, @"tracker\.Record\(").Count >= 5, "every way the day ends goes into the diary");
        C(tracker.Contains("Scribe_Collections.Look(ref diary, \"diary\"") && tracker.Contains("DiaryMax"), "the diary is saved and kept short");
        // Every code the diary can hold has a word for the player.
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Camps.xml"));
        var codes = new[] { "waiting", "lettergap", "checks", "failed", "off", "here", "nohome", "early", "gap", "threat", "weather", "hazard", "era", "nofriends", "noroom" };
        foreach (var code in codes) C(keyed.Contains("<CC_Camp_Diary_" + code + ">"), $"the diary's word for {code}");
        var blockerCodes = Regex.Matches(tracker, @"return \(""([a-z]+)"",").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();
        foreach (var code in blockerCodes) C(codes.Contains(code), $"the blocker {code} has a diary word");
        C(blockerCodes.Count >= 10, $"found the blockers ({blockerCodes.Count})");
        return n;
    }
}
