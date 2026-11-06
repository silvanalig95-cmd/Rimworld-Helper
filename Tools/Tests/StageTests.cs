using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// The colony's pace (PlayOut/StageMath.cs, the player's decision 1 of 6 November 2026): the week's room for the
/// colony's own doings with the player's choices counted, a day and a half between, the wait and the let-go, the floor
/// where the quiet-days setback starts, calendar days kept together or at supper, one custom a season, a life goal's
/// toast at the next gathering; and in the source, where each rule is read.
/// </summary>
public static class StageTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "pace: " + what);
        }

        const int H = StageMath.TicksPerHour, D = StageMath.TicksPerDay;
        int now = 40 * D + 18 * H;
        int far = int.MaxValue;

        // ---------------- The week's room ----------------

        var held = new List<int>();
        C(StageMath.Room(held, now, 2, far), "a colony with no gathering has room");
        held.Add(now - 2 * D);
        C(StageMath.Room(held, now, 2, 2 * D), "one gathering two days ago leaves room for one of the colony's own");
        held.Add(now - 3 * D);
        C(!StageMath.Room(held, now, 2, 2 * D), "two in the last seven days (the player's count): the colony's own wait");
        C(StageMath.Room(held, now, 3, 2 * D), "a week of three lets a third come");
        C(StageMath.InWeek(held, now) == 2 && StageMath.InWeek(held, now + 5 * D - 1) == 1 && StageMath.InWeek(held, now + 5 * D) == 0,
            "the week is the last seven days, sliding");
        held.Clear();
        held.Add(now - 30 * H);
        C(!StageMath.Room(held, now, 7, 30 * H), "never within a day and a half of another gathering, however many a week");
        C(StageMath.Room(held, now + 6 * H, 7, 36 * H), "a day and a half after, there's room");
        C(StageMath.Since(held, now) == 30 * H && StageMath.Since(new List<int>(), now) == int.MaxValue, "the time since the last");
        held.Clear();
        held.Add(now + H);
        C(StageMath.Room(held, now, 2, far) && StageMath.InWeek(held, now) == 0, "a gathering ahead of the clock (a game loaded) doesn't count");
        C(StageMath.AWeek(0) == StageMath.MinAWeek && StageMath.AWeek(99) == StageMath.MaxAWeek && StageMath.AWeek(2) == 2, "the setting kept in its range");
        C(StageMath.DefaultAWeek == 2 && StageMath.GapHours == 36 && StageMath.WaitDays == 3, "the player's numbers: two in seven days, a day and a half apart, three days' wait");

        // ---------------- Waiting, and let go ----------------

        int planned = now;
        C(StageMath.RoutineDeadline(planned) == planned + 3 * D, "one of the colony's own waits three days for room");
        held.Clear();
        held.Add(now - 6 * D);
        held.Add(now - 5 * D);
        int t = now;
        while (t < StageMath.RoutineDeadline(planned) && !StageMath.Room(held, t, 2, StageMath.Since(held, t))) t += H;
        C(t < StageMath.RoutineDeadline(planned) && t == now + D, $"it comes once the week has room (after {(t - now) / (float)D:0.0} days)");
        held.Clear();
        for (int i = 0; i < 4; i++) held.Add(now - i * D / 2);
        t = now;
        while (t < StageMath.RoutineDeadline(planned) && !StageMath.Room(held, t, 2, StageMath.Since(held, t))) t += H;
        C(t >= StageMath.RoutineDeadline(planned), "a week that stays full past three days: it is let go");

        // ---------------- The floor ----------------

        C(StageMath.FloorDays == SetbackMath.QuietDays, "the floor is where the quiet-days setback starts");
        C(!StageMath.FloorOpen((int)(6.9f * D)) && StageMath.FloorOpen(7 * D) && StageMath.FloorOpen(9 * D), "from the seventh day without one, the colony's own come at once");
        held.Clear();
        held.Add(now - 7 * D);
        held.Add(now - 7 * D - H);
        held.Add(now - 7 * D - 2 * H);
        C(StageMath.Room(held, now, 1, 7 * D), "the floor open, room whatever the week held");
        C(!StageMath.FloorEvening(7 * D) && StageMath.FloorEvening(8 * D), "from the eighth with nothing on the way, the colony keeps an evening itself");
        C(!StageMath.FloorOpen(far) && !StageMath.FloorEvening(far), "a colony never seen to gather (just begun) isn't on the floor");
        C(StageMath.FloorEveningDays < StageMath.FloorDays && StageMath.FloorOpenDays < StageMath.FloorEveningDays, "the floor comes before the setback does");

        // ---------------- Kept for a while ----------------

        var keep = new List<int> { now - 11 * D, now - 9 * D, now - D, now + D };
        StageMath.Prune(keep, now);
        C(keep.Count == 2 && keep.Contains(now - 9 * D) && keep.Contains(now - D), "the last ten days are kept, what's ahead of the clock forgotten");

        // ---------------- The calendar ----------------

        C(StageMath.DaysApart(43, 47, 60) == 4 && StageMath.DaysApart(58, 1, 60) == 3 && StageMath.DaysApart(1, 58, 60) == 3, "days apart, round the year");
        C(StageMath.KeptTogether(1) && StageMath.KeptTogether(3) && !StageMath.KeptTogether(4) && !StageMath.KeptTogether(0),
            "a day within three days ahead is kept together with today's");
        C(StageMath.AtSupper(D) && StageMath.AtSupper(2 * D - 1) && !StageMath.AtSupper(2 * D) && !StageMath.AtSupper(far),
            "within two days of another gathering a day is kept at supper");
        // The player's calendar: five days between the 43rd and the 47th of the year.
        int[] days = { 43, 44, 45, 46, 47 };
        int kept = 0, last = -99;
        foreach (int d in days)
        {
            if (last >= 0 && StageMath.KeptTogether(d - last)) continue;
            kept++;
            last = d;
        }
        C(kept == 2, $"the player's five days from the 43rd to the 47th come round as {kept} (43 with 44 to 46, then 47)");

        // ---------------- The customs ----------------

        C(StageMath.SeasonOf(0) == 0 && StageMath.SeasonOf(14) == 0 && StageMath.SeasonOf(15) == 1 && StageMath.SeasonOf(59) == 3, "four seasons of fifteen days");
        C(StageMath.SeasonKey(5502, 20) == 5502 * 4 + 1, "a season of the game");
        C(StageMath.CustomMayCome(-1, 10) && StageMath.CustomMayCome(9, 10) && !StageMath.CustomMayCome(10, 10), "one seasonal custom a season");

        // ---------------- A life goal's toast ----------------

        C(!StageMath.CupAtSupper(now, now + D) && StageMath.CupAtSupper(now, now + 2 * D), "a toast waits two days for the next gathering, then it's raised at supper");
        C(!StageMath.CupLetGo(now, now + 4 * D) && StageMath.CupLetGo(now, now + 5 * D), "and nobody to raise it after the colony's own wait: let go");

        // ---------------- In the source ----------------

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string stage = Read(Path.Combine("PlayOut", "Stage.cs"));
        string tracker = Read(Path.Combine("PlayOut", "PlayOutTracker.cs"));
        C(stage.Contains("LordJob_Joinable_Gathering") && stage.Contains("LordJob_Joinable_MarriageCeremony"),
            "every gathering on a home counts, the game's parties, rituals and weddings too");
        string feasts = Read(Path.Combine("Diplomacy", "Feasts.cs"));
        C(!stage.Contains("job is LordJob_FeastGuests") && feasts.Contains("Stage.Held(map);"),
            "a feast with guests counts once, as its feast begins, not again for each people's group at the gate");
        C(tracker.Contains("Stage.Room(p.map") && tracker.Contains("p.routine"), "a play-out of the colony's own asks for room before it starts");
        C(Regex.IsMatch(tracker, @"OrderBy\(x => x\.routine\)"), "the player's plans take the evening first");
        string groups = Read(Path.Combine("Social", "Groups", "GroupHabits.cs"));
        string family = Read(Path.Combine("Social", "FamilyBonds.cs"));
        string life = Read(Path.Combine("Social", "FamilyLife.cs"));
        string camps = Read(Path.Combine("Camps", "CampKinds.cs"));
        C(groups.Contains("routine = true") && family.Contains("routine = true") && life.Contains("routine = true"),
            "a group's evening, a family supper and a birthday's toast are the colony's own, and wait for room");
        C(Regex.Matches(camps, @"PlayOuts\.On && !Stage\.Room\(map, Find\.TickManager\.TicksGame\)\) return false;").Count >= 2,
            "a camp's show and song are offered only on an evening with room (their letter says tonight)");
        string fire = Read(Path.Combine("PlayOut", "PlayFire.cs"));
        C(fire.Contains("public static void Ahead(PlayOut p)") && fire.Contains("GiveUpHours") && tracker.Contains("PlayFire.Ahead(p)"),
            "a fire is laid in the hour before the evening, and one still not lit near the end is let go for a gathering without it");
        C(tracker.Contains("if (off) GiveAll(p);") && !File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PlayOut.xml")).Contains("took heart from it all the same"),
            "a plan that falls through gives nobody its moods and says it never came about");
        C(tracker.Contains("p.collided && !p.movedOn"), "a gathering kept from its evening by another takes the next evening, once");
        string goals = Read(Path.Combine("Aspirations", "GoalRewards.cs"));
        C(goals.Contains("Stage.Cup(") && !goals.Contains("new DilemmaEffect_Toast"), "a life goal's toast is raised at the next gathering, not on an evening of its own");
        string dilemmas = Read(Path.Combine("Dilemmas", "Dilemmas.cs"));
        C(dilemmas.Contains("Stage.CustomMayCome("), "one seasonal custom a season");
        string cal = Read(Path.Combine("Calendar", "CalendarKeeping.cs"));
        C(cal.Contains("StageMath.KeptTogether(") && cal.Contains("Stage.AtSupper("), "calendar days kept together, and at supper beside another gathering");
        string election = Read(Path.Combine("Offices", "OfficesElection.cs"));
        C(election.Contains("e.speakAtCount = true") && election.Contains("e.speakAtCount"), "the hustings join the count");
        string settings = Read("CouncilSettings.cs");
        C(settings.Contains("public int gatheringsAWeek = " + StageMath.DefaultAWeek + ";"), "the setting defaults to the player's two");
        return n;
    }
}
