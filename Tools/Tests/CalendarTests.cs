using ColonyCouncil;

/// <summary>
/// The colony calendar's arithmetic (CalendarMath): dates in the sixty-day year and when a day is due (also across the
/// turn of the year), what each kind of day means, which day makes way when the calendar is full, how a kept day grows
/// into a tradition and lapses, and reading an older chronicle line back into its parts.
/// </summary>
public static class CalendarTests
{
    public static int Run(Action<bool, string> check)
    {
        int cases = 0;

        // Dates.
        check(CalendarMath.Wrap(-1) == 59 && CalendarMath.Wrap(60) == 0 && CalendarMath.Wrap(125) == 5, "wrap into the year");
        check(CalendarMath.DaysUntil(58, 1) == 3 && CalendarMath.DaysUntil(10, 10) == 0 && CalendarMath.DaysUntil(10, 9) == 59, "days until");
        check(CalendarMath.DaysSince(1, 58) == 3 && CalendarMath.DaysSince(10, 10) == 0, "days since");
        for (int d = 0; d < CalendarMath.DaysPerYear; d++)
        {
            cases++;
            CalendarMath.Split(d, out int q, out int dq);
            check(q >= 0 && q <= 3 && dq >= 1 && dq <= 15 && CalendarMath.Join(q, dq) == d, $"split and join day {d}");
        }

        // Due: on the day, and the grace day after, once a year.
        check(CalendarMath.Due(20, 5500, 20, 5499, out int occ) && occ == 5500, "due on its day");
        check(!CalendarMath.Due(20, 5500, 20, 5500, out _), "not twice in a year");
        check(CalendarMath.Due(21, 5500, 20, 5499, out occ) && occ == 5500, "due the day after");
        check(!CalendarMath.Due(22, 5500, 20, 5499, out _), "gone two days after");
        check(!CalendarMath.Due(19, 5500, 20, 5499, out _), "not before its day");
        // The last day of a year, come round on the first day of the next.
        check(CalendarMath.Due(0, 5501, 59, 5499, out occ) && occ == 5500, "the year's last day comes on the next year's first");
        check(!CalendarMath.Due(0, 5501, 59, 5500, out _), "and only once");
        check(CalendarMath.Due(59, 5501, 59, 5500, out occ) && occ == 5501, "and again on its day the next year");

        // A new day's first year: an event that just happened, or a date already past, waits for next year.
        check(CalendarMath.InitialLastYear(30, 5500, 10, false) == 5500, "a date already past waits");
        check(CalendarMath.InitialLastYear(30, 5500, 40, false) == 5499, "a later date comes this year");
        check(CalendarMath.InitialLastYear(30, 5500, 30, false) == 5500, "today's date from an old save waits");
        check(CalendarMath.InitialLastYear(30, 5500, 30, true) == 5499, "a day named for today comes today");
        check(CalendarMath.Due(30, 5500, 30, CalendarMath.InitialLastYear(30, 5500, 30, true), out _), "and is due");

        // What days mean.
        check(CalendarMath.BattleWeight(10, 0) > CalendarMath.BattleWeight(6, 0) && CalendarMath.BattleWeight(6, 1) > CalendarMath.BattleWeight(20, 0),
            "a battle that cost us means more than any clean one");
        check(CalendarMath.NemesisReturnWeight < CalendarMath.NemesisWeight && CalendarMath.NemesisReturnWeight > CalendarMath.BattleWeight(6, 0),
            "a nemesis come back means less than the day they fell, more than a small clean win");
        check(CalendarMath.DeathWeight(3, 1f, false) > CalendarMath.DeathWeight(1, 1f, false) && CalendarMath.DeathWeight(1, 1f, true) > CalendarMath.DeathWeight(1, 1f, false),
            "a death means more for each who loved them, and for family");
        check(CalendarMath.BirthdayWeight(6f) > CalendarMath.BirthdayWeight(2f) && CalendarMath.BirthdayWeight(2f) < CalendarMath.HarvestWeight, "birthdays mean less than the colony's own days");
        check(CalendarMath.Standing(20f, true, 0) > CalendarMath.Standing(20f, false, 0) && CalendarMath.Standing(20f, false, 2) > CalendarMath.Standing(20f, false, 0),
            "a tradition, and every year kept, count for more");
        check(CalendarMath.HomecomingWeight(12f, 2) > CalendarMath.HomecomingWeight(6f, 2) && CalendarMath.HomecomingWeight(6f, 4) > CalendarMath.HomecomingWeight(6f, 1)
              && CalendarMath.HomecomingWeight(400f, 50) == CalendarMath.HomecomingWeight(20f, 5), "a longer journey, and more of ours on it, mean more (up to a point)");
        check(CalendarMath.HuntWeight(12) > CalendarMath.HuntWeight(CalendarMath.GreatHuntKills) && CalendarMath.HuntWeight(999) < CalendarMath.FoundingWeight,
            "a bigger hunt means more, never as much as the founding");
        check(CalendarMath.FirstChildWeight > CalendarMath.HarvestWeight && CalendarMath.NewCropWeight < CalendarMath.HarvestWeight && CalendarMath.FirstTradeWeight < CalendarMath.WallsWeight,
            "the first child means more than a harvest; a new crop less than the first harvest; the first trade less than the walls");

        // Making room.
        var w = new List<float> { 30f, 20f, 50f };
        var fixedDays = new List<bool> { false, false, false };
        check(CalendarMath.MakeRoom(w, fixedDays, 10f, 4) == -1, "room left");
        check(CalendarMath.MakeRoom(w, fixedDays, 25f, 3) == 1, "the weakest makes way");
        check(CalendarMath.MakeRoom(w, fixedDays, 15f, 3) == -2, "a weaker day isn't written down");
        check(CalendarMath.MakeRoom(w, new List<bool> { false, true, false }, 35f, 3) == 0, "a fixed day never makes way");
        check(CalendarMath.MakeRoom(w, new List<bool> { true, true, true }, 99f, 3) == -2, "all fixed: no room");

        // Traditions.
        int kept = 0, breaks = 0;
        bool tradition = false;
        check(CalendarMath.Keep(ref kept, ref tradition, ref breaks) == CalendarMath.KeepResult.Kept, "kept once");
        check(CalendarMath.Keep(ref kept, ref tradition, ref breaks) == CalendarMath.KeepResult.Kept, "kept twice");
        check(CalendarMath.YearsToTradition(kept, tradition) == 1, "one more year to go");
        check(CalendarMath.Keep(ref kept, ref tradition, ref breaks) == CalendarMath.KeepResult.Born && tradition, $"kept {CalendarMath.TraditionAfter} years: a tradition");
        check(CalendarMath.Keep(ref kept, ref tradition, ref breaks) == CalendarMath.KeepResult.KeptTradition, "a tradition kept");
        check(CalendarMath.Pass(ref kept, ref tradition, ref breaks) == CalendarMath.PassResult.Broken && tradition, "let pass once: broken, still a tradition");
        check(CalendarMath.Keep(ref kept, ref tradition, ref breaks) == CalendarMath.KeepResult.KeptTradition && breaks == 0, "kept again: the break is forgiven");
        CalendarMath.Pass(ref kept, ref tradition, ref breaks);
        check(CalendarMath.Pass(ref kept, ref tradition, ref breaks) == CalendarMath.PassResult.Lapsed && !tradition && kept == 0, "let pass twice in a row: it lapses");
        check(CalendarMath.Pass(ref kept, ref tradition, ref breaks) == CalendarMath.PassResult.Passed, "a day nobody expects passes without blame");
        int k2 = 2, b2 = 0;
        bool t2 = false;
        CalendarMath.Pass(ref k2, ref t2, ref b2);
        check(k2 == 0, "a year let pass starts the count again");

        // Reading the chronicle back.
        var clean = CalendarMath.TemplateRegex("The colony saw off {0}: {1} of them fell, and none of ours.", false);
        var parts = CalendarMath.Parse(clean, "The colony saw off <color=#D09B61FF>the Ash Clan</color>: 7 of them fell, and none of ours. Ana brought down 3 of them, more than anyone.");
        check(parts != null && parts.Count == 2 && parts[0] == "the Ash Clan" && parts[1] == "7", $"a clean victory read back ({string.Join("|", parts ?? new List<string>())})");
        var wedding = CalendarMath.TemplateRegex("{A_nameDef} and {B_nameDef} were married.", true);
        parts = CalendarMath.Parse(wedding, "<color=#FFF>Ana</color> and <color=#FFF>Bo</color> were married.");
        check(parts != null && parts[0] == "Ana" && parts[1] == "Bo", "a wedding read back");
        check(CalendarMath.Parse(wedding, "Ana and Bo were married, and then divorced.") == null, "a whole template must match whole");
        var eulogy = CalendarMath.TemplateRegex("At {OTHER_nameDef}'s grave, {PAWN_nameDef} spoke:\n\n\"{SPEECH}\"", false);
        parts = CalendarMath.Parse(eulogy, "At Tynan's grave, Ana spoke:\n\n\"He was a good cook (sometimes).\"");
        check(parts != null && parts[0] == "Tynan" && parts[1] == "Ana", "a eulogy read back");
        check(CalendarMath.Parse(null, "anything") == null && CalendarMath.Parse(clean, null) == null, "nothing to read");
        return cases;
    }
}
