using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// Rimfolk's clock (Lean/PassClockMath.cs): a pass's runs added up by the in-game hour and day, the windows rolled on by
/// game ticks (the next window, a gap, the clock going back), what the window shows at a later tick, the worst run, the
/// conversions; the table of passes; and every pass in the table timed somewhere in the mod.
/// </summary>
public static class PassClockTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "clock: " + what);
        }

        const int H = Sched.HourTicks, D = Sched.DayTicks;

        // ---------------- Windows ----------------

        C(PassClockMath.Index(0, H) == 0 && PassClockMath.Index(H - 1, H) == 0 && PassClockMath.Index(H, H) == 1, "hours start at 0, 2500, 5000");
        C(PassClockMath.Index(D * 3 + 5, D) == 3, "days by the game's day");

        var cur = PassWindow.Empty(PassWindow.None);
        var prev = PassWindow.Empty(PassWindow.None);
        PassClockMath.Add(ref cur, ref prev, 10, 100);
        C(cur.index == 10 && cur.runs == 1 && cur.time == 100, "the first run opens its window");
        C(prev.index == 9 && prev.runs == 0, "and the window before it is empty");
        PassClockMath.Add(ref cur, ref prev, 10, 50);
        C(cur.runs == 2 && cur.time == 150, "runs in the same window add up");
        PassClockMath.Add(ref cur, ref prev, 11, 7);
        C(cur.index == 11 && cur.runs == 1 && cur.time == 7, "the next window starts afresh");
        C(prev.index == 10 && prev.runs == 2 && prev.time == 150, "and the one before is kept whole");
        PassClockMath.Add(ref cur, ref prev, 14, 3);
        C(cur.index == 14 && cur.runs == 1 && prev.index == 13 && prev.runs == 0 && prev.time == 0, "after a gap the window before is empty");
        PassClockMath.Add(ref cur, ref prev, 5, 9);
        C(cur.index == 5 && cur.runs == 1 && cur.time == 9 && prev.index == 4 && prev.runs == 0, "the clock going back starts both again");

        // What the window shows at a later tick: this window so far and the one before, from what was kept.
        var kept = new PassWindow { index = 20, runs = 3, time = 30 };
        var before = new PassWindow { index = 19, runs = 2, time = 20 };
        C(PassClockMath.Current(kept, 20).runs == 3 && PassClockMath.Previous(kept, before, 20).runs == 2, "read in the same window");
        C(PassClockMath.Current(kept, 21).runs == 0 && PassClockMath.Previous(kept, before, 21).runs == 3, "read in the next window: the kept one is the one before");
        C(PassClockMath.Current(kept, 23).runs == 0 && PassClockMath.Previous(kept, before, 23).runs == 0, "read later: nothing ran in either");
        C(PassClockMath.Previous(kept, new PassWindow { index = 15, runs = 9 }, 20).runs == 0, "an older kept window isn't the one before");
        C(PassClockMath.Current(kept, 18).runs == 0 && PassClockMath.Previous(kept, before, 18).runs == 0, "read before the kept window (the clock went back): nothing");
        C(PassClockMath.Previous(PassWindow.Empty(PassWindow.None), PassWindow.Empty(PassWindow.None), 3).index == 2, "nothing run yet: an empty window before");

        // ---------------- A pass's figures ----------------

        var f = new PassFigures();
        C(f.runs == 0 && f.worst == 0 && f.worstTick == PassWindow.None && f.HourAt(100).runs == 0, "a new pass has nothing");
        f.Add(100, 40);
        f.Add(300, 90);
        f.Add(H + 5, 20);
        C(f.runs == 3 && f.time == 150, "every run counted since loading");
        C(f.worst == 90 && f.worstTick == 300, "the worst run and its tick");
        C(f.HourAt(H + 10).runs == 1 && f.HourAt(H + 10).time == 20, "this hour so far");
        C(f.LastHourAt(H + 10).runs == 2 && f.LastHourAt(H + 10).time == 130, "the hour before, whole");
        C(f.LastHourAt(2 * H + 1).runs == 1 && f.HourAt(2 * H + 1).runs == 0, "an hour on, the hour before is the last one that ran");
        C(f.DayAt(H + 10).runs == 3 && f.DayAt(H + 10).time == 150 && f.LastDayAt(H + 10).runs == 0, "the day so far holds every hour");
        f.Add(D + 1, 5);
        C(f.LastDayAt(D + 2).runs == 3 && f.DayAt(D + 2).runs == 1, "the day rolls on with the game's day");
        C(f.LastHourAt(D + 2).runs == 0, "and the hour before a new day's first run is empty");
        f.Add(D + 2, -4);
        C(f.runs == 5 && f.time == 155 && f.worst == 90, "a stopwatch going backwards counts as nothing");
        f.Clear();
        C(f.runs == 0 && f.time == 0 && f.worst == 0 && f.DayAt(D + 2).runs == 0 && f.LastDayAt(D + 2).runs == 0, "cleared for a new game");
        var first = new PassFigures();
        first.Add(50, 0);
        C(first.worstTick == 50 && first.worst == 0, "a first run of no time is still the worst so far");

        // ---------------- Whole windows and conversions ----------------

        C(!PassClockMath.SawWholePrevious(PassWindow.None, 10 * H, H), "nothing timed: no whole hour");
        C(PassClockMath.SawWholePrevious(3 * H, 4 * H + 7, H), "timing since an hour's start: that hour is whole");
        C(!PassClockMath.SawWholePrevious(3 * H + 1, 4 * H + 7, H), "timing from a little into it: not whole");
        C(PassClockMath.SawWholePrevious(3 * H + 1, 5 * H, H), "an hour later it is");
        C(!PassClockMath.SawWholePrevious(0, 7, H), "in the first hour of the game there is no hour before");
        C(PassClockMath.SawWholePrevious(0, D, D) && !PassClockMath.SawWholePrevious(5, D, D), "days the same");
        C(Math.Abs(PassClockMath.Ms(10_000, 10_000_000) - 1.0) < 1e-9, "stopwatch ticks to milliseconds");
        C(PassClockMath.Ms(5, 0) == 0.0, "no stopwatch, no time");
        C(PassClockMath.PerRun(90, 3) == 30.0 && PassClockMath.PerRun(90, 0) == 0.0, "the average run");

        // ---------------- The table ----------------

        var names = Enum.GetValues(typeof(Pass)).Cast<Pass>().ToList();
        C(PassTable.Count == names.Count, $"one table entry per pass ({PassTable.Count}, {names.Count})");
        for (int i = 0; i < PassTable.Count; i++) C((int)PassTable.All[i].pass == i, $"the table's entry {i} is {(Pass)i}");
        C(PassTable.All.Select(p => p.key).Distinct().Count() == PassTable.Count, "every pass has a name of its own");
        C(PassTable.All.All(p => p.key != null && p.key.StartsWith("CC_Clock_Pass_" + p.pass)), "names are keyed by the pass");
        C(PassTable.All.All(p => p.period == Sched.HourTicks || p.period == Sched.RareTicks || p.period == Sched.DayTicks || p.period == Sched.PeopleLogPeriod),
            "every pass comes round on one of Sched's rhythms");

        // ---------------- Every pass is timed in the mod ----------------

        var src = Path.Combine(root, "Source", "ColonyCouncil");
        var used = new HashSet<string>();
        foreach (var file in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Replace('\\', '/').EndsWith("Lean/PassClockMath.cs")) continue;
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"\bPass\.([A-Za-z]+)\b")) used.Add(m.Groups[1].Value);
        }
        foreach (var p in names) C(used.Contains(p.ToString()), $"the pass {p} is timed where it runs");
        // The clock is read and stopped in one place, never by hand around a pass with the stopwatch.
        var clock = File.ReadAllText(Path.Combine(src, "Lean", "PassClock.cs"));
        C(clock.Contains("Stopwatch.GetTimestamp()") && !clock.Contains("Rand.") && !clock.Contains("new List"), "the clock reads the stopwatch, draws no random numbers, allocates nothing per run");
        C(!clock.Contains("Scribe_"), "the clock saves nothing");

        return n;
    }
}
