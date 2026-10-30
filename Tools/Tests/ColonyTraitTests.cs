using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// What a colony grows into and the names of its years (ColonyTraitMath): the rolling average and how a name is held
/// and lost, the calendar's count of days kept year after year, which camps and deeds count as hosting, who founded
/// the colony and who is the last of them. And the defs that use them: every tally a trait counts is one Rimfolk keeps,
/// every name's condition is one it knows, and the new traits give no mood (what they do is in play).
/// </summary>
public static class ColonyTraitTests
{
    /// <summary>The tallies Rimfolk keeps (ColonyTraits.Tally and its daily tallies).</summary>
    private static readonly HashSet<string> Tallies = new HashSet<string>
    {
        "campHosted", "welcomed", "guestNight", "ritual", "chaplainDay", "guideDay",
        // What visitors notice about the colony (Immersion/VisitorNotices.cs).
        "seen:hall", "seen:art", "seen:graves", "seen:shrine", "seen:walls", "seen:beasts", "seen:fields"
    };

    private static readonly HashSet<string> Conditions = new HashSet<string> { "founder", "lastFounder", "firstborn" };

    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;

        // The rolling average: today's share, nothing for nothing, and a steady amount settles on itself.
        check(Math.Abs(ColonyTraitMath.Fold(0f, 1f, 0.025f) - 0.025f) < 1e-6f, "one day's amount is a fortieth");
        check(ColonyTraitMath.Fold(0.5f, 0f, 0f) == 0.5f && ColonyTraitMath.Fold(0.5f, 2f, 1f) == 2f, "no share keeps the rate, all of it takes today's");
        float rate = 0f;
        for (int d = 0; d < 400; d++) rate = ColonyTraitMath.Fold(rate, 0.08f, 1f / 40f);
        check(Math.Abs(rate - 0.08f) < 0.001f, $"a steady amount settles on itself ({rate:0.0000})");
        // A camp hosted every twenty days, and nothing else: a good host's name comes and stays.
        rate = 0f;
        bool known = false;
        int knownDay = -1;
        for (int d = 1; d <= 240; d++)
        {
            rate = ColonyTraitMath.Fold(rate, d % 20 == 0 ? 1f : 0f, 1f / 40f);
            bool now = ColonyTraitMath.Known(known, rate, 0.05f, 0.6f);
            if (now && !known && knownDay < 0) knownDay = d;
            check(!(known && !now), $"a steady host doesn't lose the name between camps (day {d}, {rate:0.000})");
            known = now;
            cases++;
        }
        check(knownDay > 0 && knownDay < 120, $"a camp every twenty days earns the name within two years ({knownDay})");
        // Hysteresis: known at the line, kept down to 60% of it, lost below.
        check(ColonyTraitMath.Known(false, 0.05f, 0.05f, 0.6f) && !ColonyTraitMath.Known(false, 0.049f, 0.05f, 0.6f), "known at the line");
        check(ColonyTraitMath.Known(true, 0.031f, 0.05f, 0.6f) && !ColonyTraitMath.Known(true, 0.029f, 0.05f, 0.6f), "kept down to 60%, lost below");
        // Hosting stops: the name goes within a season or two.
        int lostDay = -1;
        for (int d = 1; d <= 120 && lostDay < 0; d++)
        {
            rate = ColonyTraitMath.Fold(rate, 0f, 1f / 40f);
            if (!ColonyTraitMath.Known(true, rate, 0.05f, 0.6f)) lostDay = d;
        }
        check(lostDay > 0 && lostDay <= 60, $"a colony that stops hosting loses the name within a year ({lostDay})");

        // The calendar: days kept two years running count, traditions once more, a person's own day never.
        check(ColonyTraitMath.CalendarLevel(null) == 0f, "no calendar, nothing");
        var days = new List<(bool personal, int keptYears, bool tradition)>
        {
            (false, 2, false), (false, 1, false), (false, 3, true), (true, 5, false), (false, 0, true)
        };
        check(ColonyTraitMath.CalendarLevel(days) == 4f, $"two days kept two years and more, two traditions ({ColonyTraitMath.CalendarLevel(days)})");
        check(ColonyTraitMath.CalendarLevel(new[] { (false, 0, true), (false, 0, true) }) < 3f, "a scenario's two customs alone don't make the colony long-memoried");

        // Camps: hosted to the end, or not.
        foreach (var e in new[] { "time", "weather", "raid", null })
        {
            cases++;
            check(ColonyTraitMath.HostedWell(e, true), $"a camp that left for '{e}' was hosted");
            check(!ColonyTraitMath.HostedWell(e, false), $"a camp turned back before it camped ('{e}') wasn't");
        }
        foreach (var e in new[] { "forced", "harmed", "arrest", "fight", "fallout", "empty", "asked", "crowded", "hunger", "lost" })
        {
            cases++;
            check(!ColonyTraitMath.HostedWell(e, true), $"a camp that ended '{e}' wasn't hosted well");
        }
        check(ColonyTraitMath.Hosting("welcomed") && ColonyTraitMath.Hosting("refugees") && ColonyTraitMath.Hosting("tended"), "taking people in counts");
        check(!ColonyTraitMath.Hosting("campHosted") && !ColonyTraitMath.Hosting("campHelped"), "camps count once, when they leave");
        check(!ColonyTraitMath.Hosting("robbed") && !ColonyTraitMath.Hosting("released") && !ColonyTraitMath.Hosting(null), "other deeds don't");

        // Founders: here in the first two days.
        const int day = 60000;
        check(ColonyTraitMath.Founder(0, day) && ColonyTraitMath.Founder(2 * day, day), "the first two days found the colony");
        check(!ColonyTraitMath.Founder(2 * day + 1, day) && !ColonyTraitMath.Founder(30 * day, day), "later comers don't");
        check(ColonyTraitMath.LastOfTheFirst(4, 1, 2f), "one of four left, two years on");
        check(!ColonyTraitMath.LastOfTheFirst(1, 1, 5f), "a colony founded by one has no last of the first");
        check(!ColonyTraitMath.LastOfTheFirst(3, 2, 5f) && !ColonyTraitMath.LastOfTheFirst(3, 0, 5f), "two left, or none, isn't the last");
        check(!ColonyTraitMath.LastOfTheFirst(3, 1, 0.5f), "not before the colony's first year");

        // The defs: the traits' tallies and levels, the names' conditions.
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text, @"<ColonyCouncil\.ColonyTraitDef>(.*?)</ColonyCouncil\.ColonyTraitDef>", RegexOptions.Singleline))
            {
                cases++;
                string body = m.Groups[1].Value;
                string name = Regex.Match(body, "<defName>(.*?)</defName>").Groups[1].Value;
                foreach (Match k in Regex.Matches(body, "<key>(.*?)</key>"))
                    check(Tallies.Contains(k.Groups[1].Value), $"{name}: tally '{k.Groups[1].Value}' is one Rimfolk keeps");
                var level = Regex.Match(body, "<level>(.*?)</level>");
                if (level.Success) check(level.Groups[1].Value == "calendar", $"{name}: level '{level.Groups[1].Value}' is known");
                bool counted = body.Contains("<records>") || body.Contains("<tallies>") || level.Success;
                check(counted, $"{name}: counted from something");
                // A trait counted for the colony as a whole has no skill to take pride in: no mood, its effects are in play.
                if (body.Contains("<perColony>true</perColony>") || level.Success)
                    check(!body.Contains("<skill>"), $"{name}: a colony-wide trait gives no pride mood");
                if (body.Contains("<tallies>") || level.Success)
                    check(body.Contains("<tag>") && (body.Contains("<lean>") || body.Contains("<campGap>")), $"{name}: a grown trait has a tag and an effect in play");
            }
            foreach (Match m in Regex.Matches(text, @"<ColonyCouncil\.EpithetDef>(.*?)</ColonyCouncil\.EpithetDef>", RegexOptions.Singleline))
            {
                string body = m.Groups[1].Value;
                var only = Regex.Match(body, "<only>(.*?)</only>");
                if (!only.Success) continue;
                cases++;
                string name = Regex.Match(body, "<defName>(.*?)</defName>").Groups[1].Value;
                check(Conditions.Contains(only.Groups[1].Value), $"{name}: condition '{only.Groups[1].Value}' is known");
                check(body.Contains("<record>"), $"{name}: a name with a condition still counts a record");
            }
        }
        return cases;
    }
}
