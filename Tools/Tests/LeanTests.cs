using ColonyCouncil;

/// <summary>
/// Rimfolk's clock (Sched) and its caps (Caps): every hourly pass has a tick of the hour to itself, well apart from the
/// others and from the short 250-tick passes; the council's hour keeps its old order; the heart's slices come round once
/// an hour each; and the caps keep the newest and the most meaningful entries.
/// </summary>
public static class LeanTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "lean: " + what);
        }

        // ---------------- The hour's layout ----------------

        var hourly = Sched.HourlyOffsets;
        C(hourly.Distinct().Count() == hourly.Length, "every hourly pass has a tick of the hour of its own");
        foreach (int o in hourly) C(o >= 0 && o < Sched.HourTicks, $"hourly offset {o} is inside the hour");
        int minGap = int.MaxValue;
        for (int i = 0; i < hourly.Length; i++)
        for (int j = 0; j < hourly.Length; j++)
        {
            if (i == j) continue;
            int d = Sched.Mod(hourly[j] - hourly[i], Sched.HourTicks);
            minGap = Math.Min(minGap, Math.Min(d, Sched.HourTicks - d));
        }
        C(minGap >= 90, $"hourly passes are at least 90 ticks apart (closest: {minGap})");
        foreach (int o in hourly)
        {
            if (Sched.HourlyInsideRare.Contains(o)) continue;
            C(!Sched.RareOffsets.Contains(o % Sched.RareTicks), $"hourly offset {o} misses every 250-tick pass");
        }
        C(Sched.RareOffsets.Distinct().Count() == Sched.RareOffsets.Length, "every 250-tick pass has a tick of its own");
        foreach (int o in Sched.RareOffsets) C(o >= 0 && o < Sched.RareTicks, $"250-tick offset {o} is inside the cycle");
        // The people log every 1000 ticks lands on five different ticks of the hour over five hours.
        for (int h = 0; h < 5; h++)
        for (int tick = h * Sched.HourTicks; tick < (h + 1) * Sched.HourTicks; tick++)
            if (Sched.Every(tick, Sched.PeopleLogPeriod, Sched.PeopleLog))
                C(!hourly.Contains(tick % Sched.HourTicks), $"the people log at tick {tick % Sched.HourTicks} of the hour misses every hourly pass");

        // The council's hour runs in its old order: fights and wishes, the day (two halves), life, people, events.
        int[] council = { Sched.CouncilWishes, Sched.CouncilDayFirst, Sched.CouncilDaySecond, Sched.CouncilLife, Sched.CouncilPeople, Sched.CouncilEvents };
        for (int i = 1; i < council.Length; i++) C(council[i - 1] < council[i], $"council part {i} runs after part {i - 1} within the hour");

        // Over two days: each hourly pass once an hour, never two on one tick, the day's halves once a day each.
        var fired = new Dictionary<int, int>();
        int dayFirst = 0, daySecond = 0, crowded = 0;
        for (int tick = 0; tick < Sched.DayTicks * 2; tick++)
        {
            int here = 0;
            foreach (int o in hourly)
            {
                if (!Sched.Hourly(tick, o)) continue;
                fired[o] = (fired.TryGetValue(o, out int k) ? k : 0) + 1;
                here++;
            }
            if (here > 1) crowded++;
            if (Sched.Hourly(tick, Sched.CouncilDayFirst) && Sched.FirstHourOfDay(tick)) dayFirst++;
            if (Sched.Hourly(tick, Sched.CouncilDaySecond) && Sched.FirstHourOfDay(tick)) daySecond++;
        }
        C(crowded == 0, $"no tick carries two hourly passes ({crowded} did)");
        C(hourly.All(o => fired.TryGetValue(o, out int k) && k == 48), "each hourly pass runs 24 times a day");
        C(dayFirst == 2 && daySecond == 2, $"the council's day halves run once a day each ({dayFirst}, {daySecond})");
        C(Sched.DailyWealth < Sched.HourTicks && Sched.DailyBestFriends < Sched.HourTicks, "the daily samples run in the first hour of the day");

        // ---------------- Arithmetic ----------------

        C(Sched.Mod(-1, 250) == 249 && Sched.Mod(250, 250) == 0 && Sched.Mod(7, 250) == 7, "mod never goes negative");
        C(Sched.SliceOf(-3, 10) == 7 && Sched.SliceOf(23, 10) == 3, "slice of an id");
        C(Sched.SliceTurn(0, 2500, 0, 0) == -1, "no slices, no turns");

        // The heart: ten slices an hour at tick 71 of each 250, the same turns as before the clock had a name.
        var seen = new int[10];
        for (int tick = 0; tick < Sched.HourTicks * 3; tick++)
        {
            int slice = Sched.SliceTurn(tick, Sched.HourTicks, 10, Sched.RareHeart);
            int old = tick % 250 != 71 ? -1 : tick / 250 % 10;
            if (slice != old)
            {
                C(false, $"heart slice at tick {tick}: {slice}, was {old}");
                break;
            }
            if (slice >= 0) seen[slice]++;
        }
        C(seen.All(k => k == 3), "each heart slice comes round once an hour");

        // Ailments and health stories: a tenth of the colony every 250 ticks, each slice once an hour; the ailments' colony
        // part keeps its old tick of the hour, which is one of its slices' turns; no slice lands on another hourly pass.
        foreach (var (name, offset) in new[] { ("ailments", Sched.RareAilments), ("health", Sched.RareHealth) })
        {
            var turns = new int[Sched.Slices];
            var landed = new List<int>();
            for (int tick = 0; tick < Sched.HourTicks * 2; tick++)
            {
                int slice = Sched.SliceTurn(tick, Sched.HourTicks, Sched.Slices, offset);
                if (slice < 0) continue;
                turns[slice]++;
                if (tick < Sched.HourTicks) landed.Add(tick);
            }
            C(turns.All(k => k == 2), $"each {name} slice comes round once an hour ({string.Join(",", turns)})");
            C(landed.Count == Sched.Slices, $"{name}: {Sched.Slices} turns an hour");
            foreach (int t in landed)
            {
                if (name == "ailments" && t == Sched.Ailments) continue;
                C(!hourly.Contains(t), $"{name} slice at tick {t} misses every hourly pass");
            }
        }
        C(Sched.SliceTurn(Sched.Ailments, Sched.HourTicks, Sched.Slices, Sched.RareAilments) >= 0, "the ailments' colony part rides on one of its slices");
        C(Sched.SliceTurn(Sched.Health, Sched.HourTicks, Sched.Slices, Sched.RareHealth) < 0, "the health stories' colony part has a tick of its own");
        // The passes that only run while something waits keep off the hourly passes too.
        foreach (int o in new[] { Sched.RareNews, Sched.RarePlayOut, Sched.RareWeekMemory, Sched.RareCouriers })
        for (int tick = o; tick < Sched.HourTicks; tick += Sched.RareTicks)
            C(!hourly.Contains(tick), $"the 250-tick pass at {o} misses every hourly pass (tick {tick})");

        // ---------------- Caps ----------------

        // Over the cap, the oldest go first.
        var list = Enumerable.Range(0, 10).ToList();
        C(Caps.TrimOldest(list, 6) == 4 && list.SequenceEqual(new[] { 4, 5, 6, 7, 8, 9 }), "trim oldest keeps the newest six");
        C(Caps.TrimOldest(list, 6) == 0 && list.Count == 6, "nothing to trim at the cap");
        C(Caps.TrimOldest(list, -1) == 6 && list.Count == 0, "a cap below zero empties the list");

        // Keeping the meaningful: the newest few always stay; among the older ones the ordinary go first, oldest first.
        var entries = Enumerable.Range(0, 12).ToList();
        bool Big(int x) => x % 4 == 0;
        int dropped = Caps.TrimKeeping(entries, 8, Big, 4);
        C(dropped == 4, $"trim keeping drops four ({dropped})");
        C(entries.SequenceEqual(new[] { 0, 4, 6, 7, 8, 9, 10, 11 }), "the oldest ordinary entries go, milestones and the newest four stay: " + string.Join(",", entries));
        // Only milestones and the newest left: then the oldest milestones go.
        var big = new List<int> { 0, 4, 8, 12, 13, 14 };
        Caps.TrimKeeping(big, 4, Big, 2);
        C(big.SequenceEqual(new[] { 8, 12, 13, 14 }), "then the oldest milestones: " + string.Join(",", big));
        // The newest are kept even when they're all ordinary and the milestones are older.
        var mixed = new List<int> { 0, 4, 1, 2, 3, 5 };
        Caps.TrimKeeping(mixed, 4, Big, 4);
        C(mixed.SequenceEqual(new[] { 1, 2, 3, 5 }), "the newest four stay over older milestones: " + string.Join(",", mixed));
        C(Caps.TrimKeeping(new List<int> { 1, 2 }, 5, Big, 1) == 0, "nothing to trim under the cap");

        // Keeping the newest by a time of their own (journals by their last line).
        var journals = new List<(string who, int last)> { ("a", 50), ("b", 10), ("c", 70), ("d", 30), ("e", 60) };
        int gone = Caps.TrimByAge(journals, 3, j => j.last, j => j.who != "e");
        C(gone == 2 && journals.Select(j => j.who).SequenceEqual(new[] { "a", "c", "e" }), "trim by age drops the two oldest that may go: " + string.Join(",", journals.Select(j => j.who)));
        var keepAll = new List<(string who, int last)> { ("a", 1), ("b", 2) };
        C(Caps.TrimByAge(keepAll, 0, j => j.last, j => false) == 0 && keepAll.Count == 2, "nothing goes that may not go");

        // Dictionaries of times: what's older than the window goes, and anything from the future (an older save).
        var times = new Dictionary<string, int> { ["a"] = 100, ["b"] = 900, ["c"] = 500, ["d"] = 1000, ["e"] = 2000 };
        C(Caps.DropOlderThan(times, 1000, 600) == 2 && !times.ContainsKey("a") && !times.ContainsKey("e"), "drop older than the window, and the future");
        C(times.Count == 3 && times.ContainsKey("c"), "what is within the window stays");
        C(Caps.DropOlderThan(times, 1000, 600) == 0, "nothing more to drop");
        C(Caps.DropOlderThan(new Dictionary<int, int>(), 5, 1) == 0, "an empty dictionary");

        return n;
    }
}
