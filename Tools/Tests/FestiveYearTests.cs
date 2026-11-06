using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// One festive year (phase 5 of the review, the player's decision 7 of 6 November 2026): the council's customs on the
/// colony calendar (CalendarMath: a custom's day and its grace days, a step toward a tradition once a year, three years
/// running for days and customs alike, off the calendar when let pass before then, an older save's counts carried over);
/// and in the source, one tradition rule and one feast engine (only PlayOut/PlayFeast.cs makes a feast, every feast path
/// goes through it, the host's cup said in one sentence).
/// </summary>
public static class FestiveYearTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "festive year: " + what);
        }

        // ---------------- A custom's day ----------------

        int year = 5502;
        C(CalendarMath.CustomDue(20, year, 20, year - 1, out int occ) && occ == year, "a custom's question is due on its day");
        C(CalendarMath.CustomDue(20 + CalendarMath.CustomGraceDays, year, 20, year - 1, out _), "and on the last of its grace days, for its weather");
        C(!CalendarMath.CustomDue(21 + CalendarMath.CustomGraceDays, year, 20, year - 1, out _), "not after them");
        C(!CalendarMath.CustomDue(22, year, 20, year, out _), "once a year: not when it came round this year");
        C(!CalendarMath.CustomDue(19, year, 20, year - 1, out _), "not the day before");
        C(CalendarMath.CustomDue(2, year, 58, year - 2, out occ) && occ == year - 1, "a day at the year's end may still come early the next year");
        C(CalendarMath.CustomMissed(20 + CalendarMath.CustomGraceDays + 1, 20) && !CalendarMath.CustomMissed(20 + CalendarMath.CustomGraceDays, 20),
            "its year is spent the day after its grace days");
        C(CalendarMath.CustomGraceDays >= 3 && CalendarMath.CustomGraceDays <= CalendarMath.DaysPerQuadrum / 2, "the grace days stay within its season, mostly");

        // ---------------- One tradition rule ----------------

        C(CalendarMath.TraditionAfter == 3, "three years running make a tradition");
        int kept = 0, breaks = 0, last = -99999;
        bool tradition = false;
        C(CalendarMath.KeepInYear(ref kept, ref tradition, ref breaks, ref last, year) == CalendarMath.KeepResult.Kept && kept == 1, "kept the first year");
        C(CalendarMath.KeepInYear(ref kept, ref tradition, ref breaks, ref last, year) == CalendarMath.KeepResult.Kept && kept == 1,
            "kept again the same year (a custom with its occasion): no second year");
        C(CalendarMath.KeepInYear(ref kept, ref tradition, ref breaks, ref last, year + 1) == CalendarMath.KeepResult.Kept && kept == 2 && !tradition, "two years: not yet");
        C(CalendarMath.KeepInYear(ref kept, ref tradition, ref breaks, ref last, year + 2) == CalendarMath.KeepResult.Born && tradition, "the third year: a tradition");
        C(CalendarMath.KeepInYear(ref kept, ref tradition, ref breaks, ref last, year + 2) == CalendarMath.KeepResult.KeptTradition, "kept again that year, still felt");
        C(CalendarMath.KeepInYear(ref kept, ref tradition, ref breaks, ref last, year + 3) == CalendarMath.KeepResult.KeptTradition && kept == 4, "and the next year");

        // Running: a year let pass starts the count again.
        int k2 = 0, b2 = 0, l2 = -99999;
        bool t2 = false;
        CalendarMath.KeepInYear(ref k2, ref t2, ref b2, ref l2, year);
        CalendarMath.KeepInYear(ref k2, ref t2, ref b2, ref l2, year + 1);
        var passed = CalendarMath.Pass(ref k2, ref t2, ref b2);
        C(passed == CalendarMath.PassResult.Passed && k2 == 0 && !t2, "let pass before it is a tradition: the years start again");
        C(CalendarMath.OffCalendar(passed), "and the custom leaves the calendar, a question again");
        // A tradition broken costs, and lapses the second time in a row.
        int k3 = 5, b3 = 0;
        bool t3 = true;
        var broken = CalendarMath.Pass(ref k3, ref t3, ref b3);
        C(broken == CalendarMath.PassResult.Broken && t3 && !CalendarMath.OffCalendar(broken), "a tradition let pass once is broken and stays on the calendar");
        var lapsed = CalendarMath.Pass(ref k3, ref t3, ref b3);
        C(lapsed == CalendarMath.PassResult.Lapsed && !t3 && CalendarMath.OffCalendar(lapsed), "twice in a row it lapses and leaves");
        int l3 = -99999;
        k3 = 3;
        t3 = true;
        b3 = 1;
        CalendarMath.KeepInYear(ref k3, ref t3, ref b3, ref l3, year);
        C(b3 == 0, "kept again after a break: the break is forgiven");

        // ---------------- An older save ----------------

        CalendarMath.CarryCustom(2, true, 1, out int ok, out bool ot, out int ob);
        C(ok == 2 && ot && ob == 1, "a custom already a tradition stays one, with its break");
        CalendarMath.CarryCustom(1, false, 0, out ok, out ot, out ob);
        C(ok == 1 && !ot && ob == 0, "one counted under the old two-year rule keeps its count");
        int lk = -99999;
        C(CalendarMath.KeepInYear(ref ok, ref ot, ref ob, ref lk, year) == CalendarMath.KeepResult.Kept, "kept a second year it is not a tradition yet");
        C(CalendarMath.KeepInYear(ref ok, ref ot, ref ob, ref lk, year + 1) == CalendarMath.KeepResult.Born, "it needs the third");
        CalendarMath.CarryCustom(1, true, 5, out _, out _, out ob);
        C(ob < CalendarMath.LapseAfter, "an older break count never lapses a tradition by itself");

        // ---------------- In the source: one rule, one feast engine ----------------

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var all = Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories);
        C(!all.Any(f => File.ReadAllText(f).Contains("traditionAfter")), "no custom keeps a tradition rule of its own");
        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories);
        C(!defs.Any(f => File.ReadAllText(f).Contains("<traditionAfter>")), "no def sets one either");
        var makers = all.Where(f => Regex.IsMatch(File.ReadAllText(f), @"kind\s*=\s*PlayKind\.Feast")).Select(Path.GetFileName).ToList();
        C(makers.Count == 1 && makers[0] == "PlayFeast.cs", $"only the feast engine makes a feast ({string.Join(", ", makers)})");
        string effects = Read("PlayOut/PlayOutEffects.cs");
        C(effects.Contains("PlayFeast.Stage(order, p => Fill(p, ctx))"), "an answered feast goes through the engine");
        C(Read("Calendar/CalendarKeeping.cs").Contains("PlayFeast.Stage(new FeastOrder") && Read("Calendar/CalendarKeeping.cs").Contains("why = FeastWhy.Day"),
            "a calendar feast goes through the engine");
        C(Read("Diplomacy/Feasts.cs").Contains("why = FeastWhy.Guests"), "a feast with guests goes through the engine");
        C(Read("Social/WeddingPlans.cs").Contains("why = FeastWhy.Wedding") && Read("Social/FamilyBonds.cs").Contains("why = FeastWhy.Supper"),
            "a wedding's feast and a family's supper go through it too");
        C(Read("Immersion/GatheringTouches.cs").Contains("PlayFeast.ToastWords(play, host)"), "the host's cup is raised at the feast on the map");
        string dilemmas = Read("Dilemmas/Dilemmas.cs");
        C(dilemmas.Contains("CalendarMath.KeepInYear") && dilemmas.Contains("CalendarMath.Pass("), "the council's customs keep the calendar's rule");
        C(dilemmas.Contains("CustomSeasonHeld") && dilemmas.Contains("BringsCustom"), "a season holding a custom's day is asked no other, and a dated custom comes on its day");
        int keepAt = dilemmas.IndexOf("CalendarMath.KeepInYear("), recordAt = dilemmas.IndexOf("CouncilRecords.Add(keeper, CouncilRecords.DaysKept)");
        C(keepAt >= 0 && recordAt > keepAt && recordAt - keepAt < 1200 && dilemmas.Contains(".KeeperOfDays"),
            "a custom kept is a day on the calendar kept: on the keeper of days' record, as the promises judged by days kept read it");

        // The host's words, and the custom's lines, said over heads: one sentence each.
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_FestiveYear.xml")).Root!;
        foreach (var key in new[] { "CC_Fest_Toast_Day", "CC_Fest_Toast_Guests" })
        {
            string line = (string?)keyed.Element(key) ?? "";
            C(line.Length > 0 && line.Count(c => c == '.' || c == '!' || c == '?') == 1 && line.TrimEnd().EndsWith("."), $"{key} is one sentence");
        }
        foreach (var key in new[] { "CC_Fest_Intro", "CC_Fest_Origin_Custom", "CC_Fest_CustomOn", "CC_Fest_CustomOff", "CC_Cal_Way_Custom", "CC_Cal_Way_Custom_Desc" })
            C(keyed.Element(key) != null, $"{key} is written");
        C(((string?)keyed.Element("CC_Fest_Intro") ?? "").Contains("{YEARS}"), "the page says how many years make a tradition");
        return n;
    }
}
