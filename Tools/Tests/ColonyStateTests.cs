using System;
using System.Collections.Generic;
using ColonyCouncil;

/// <summary>
/// A council per colony (Lean/ColonyStateMath.cs): which council a colony reads, an old save's single council going to
/// the seat, a home lost (kept a while, carried by its people when no home is left, taken by the new home when they move
/// in one go, back again), each colony's own people and its last-few guard, and an officer who moves to another colony.
/// </summary>
public static class ColonyStateTests
{
    private sealed class Entry : IColonyEntry
    {
        public int Home { get; set; }
        public int LostAt { get; set; } = ColonyStateMath.NotLost;

        public Entry(int home = HomesMath.None)
        {
            Home = home;
        }
    }

    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void Check(bool ok, string what)
        {
            n++;
            check(ok, "colony councils: " + what);
        }

        const int None = HomesMath.None;
        const int NotLost = ColonyStateMath.NotLost;
        int keep = ColonyStateMath.KeepLostDays * HomesMath.DayTicks;

        // ---------------- An old save: its one council is the seat's ----------------

        var old = new Entry();
        var book = new List<Entry> { old };
        Check(ColonyStateMath.Find(book, 40, 12) == null && old.Home == None, "a colony that isn't the seat doesn't take an old save's council");
        Check(ColonyStateMath.Find(book, 12, 12) == old && old.Home == 12, "the seat takes an old save's council, all it had");
        Check(ColonyStateMath.Find(book, 12, 12) == old && book.Count == 1, "and goes on reading it");
        Check(ColonyStateMath.Find(book, None, 12) == null, "no colony, no council");
        // A second home has one of its own (the caller makes it); the seat's stays the seat's.
        var second = new Entry(40);
        book.Add(second);
        Check(ColonyStateMath.Find(book, 40, 12) == second && ColonyStateMath.Find(book, 12, 12) == old, "each colony reads its own");
        // One colony: what it reads is the one council it ever had.
        var one = new List<Entry> { new Entry() };
        Check(ColonyStateMath.Find(one, 7, 7) == one[0] && one.Count == 1 && one[0].Home == 7, "with one colony there is one council");

        // ---------------- A home lost, while another remains ----------------

        var homes = new HashSet<int> { 12 };
        ColonyStateMath.Lost(book, 40, 1000, homes);
        Check(second.LostAt == 1000 && second.Home == 40, "a lost home's council is kept, from when it was lost");
        Check(ColonyStateMath.Find(book, 12, 12) == old && old.LostAt == NotLost, "the other colony's is untouched");
        Check(!ColonyStateMath.Drops(second.LostAt, 1000 + keep - 1, true), "kept for a while");
        Check(ColonyStateMath.Drops(second.LostAt, 1000 + keep, true), "then let go");
        Check(!ColonyStateMath.Drops(second.LostAt, 1000 + keep * 5, false), "never let go while there's no home at all");
        Check(!ColonyStateMath.Drops(NotLost, 1000 + keep * 5, true), "a council whose home is ours is never let go");
        // The home comes back (handed back, taken back) before its time.
        Check(ColonyStateMath.Find(book, 40, 12) == second && second.LostAt == NotLost, "a home that comes back has its council again");
        // Lost again, and noticed only in the day's look; then its time runs out.
        Check(ColonyStateMath.Upkeep(book, 2000, homes) == 0 && second.LostAt == 2000, "a home lost without being noticed is kept from the day's look");
        Check(ColonyStateMath.Upkeep(book, 2000 + keep - 1, homes) == 0 && book.Contains(second), "not let go before its time");
        Check(ColonyStateMath.Upkeep(book, 2000 + keep, homes) == 1 && !book.Contains(second) && book.Contains(old), "its time up, it goes; the others stay");
        var returning = new List<Entry> { new Entry(12) { LostAt = 500 } };
        ColonyStateMath.Upkeep(returning, 600, homes);
        Check(returning[0].LostAt == NotLost && returning.Count == 1, "the day's look finds a lost home ours again");

        // ---------------- No home left: its people carry it ----------------

        var noHome = new HashSet<int>();
        var nomads = new List<Entry> { new Entry(5) };
        ColonyStateMath.Lost(nomads, 5, 3000, noHome);
        Check(nomads[0].Home == None && nomads[0].LostAt == NotLost, "the last home abandoned: its people carry its council");
        Check(ColonyStateMath.Carried(nomads, noHome) == nomads[0] && nomads.Count == 1, "it's the one read with no home");
        Check(ColonyStateMath.Find(nomads, 9, 9) == nomads[0] && nomads[0].Home == 9, "the next home founded (the seat) takes it, all it had");
        // Two colonies lost at once with none left: the first's is carried, the other's kept.
        var two = new List<Entry> { new Entry(5), new Entry(8) };
        ColonyStateMath.Lost(two, 5, 4000, noHome);
        ColonyStateMath.Lost(two, 8, 4000, noHome);
        Check(two[0].Home == None && two[1].Home == 8 && two[1].LostAt == 4000, "two lost at once: one carried, the other kept");
        // Asked about before the loss is noticed (no home, nothing marked yet): the oldest colony's is carried then.
        var early = new List<Entry> { new Entry(5), new Entry(8) };
        Check(ColonyStateMath.Carried(early, noHome) == early[0] && early[0].Home == None, "asked before the loss is noticed: the oldest colony's is carried");
        ColonyStateMath.Lost(early, 5, 5000, noHome);
        ColonyStateMath.Lost(early, 8, 5000, noHome);
        Check(early[0].Home == None && early[1].LostAt == 5000, "and the loss, noticed later, keeps the other");
        Check(ColonyStateMath.Carried(new List<Entry>(), noHome) == null, "with nothing to carry, the caller makes a new one");
        var lostLong = new List<Entry> { new Entry(5) { LostAt = 100 }, new Entry(8) { LostAt = 300 } };
        Check(ColonyStateMath.Carried(lostLong, noHome) == lostLong[1], "with every loss noticed, the one lost last is carried");
        // Moved in one go (a gravship landed, or a caravan settled after giving up the old home within the hour): the new
        // seat, the only home and with no council yet, takes the one just left, noticed or not.
        var gravship = new List<Entry> { new Entry(5) };
        Check(ColonyStateMath.Find(gravship, 9, 9, new HashSet<int> { 9 }) == gravship[0] && gravship[0].Home == 9 && gravship.Count == 1,
            "moved in one go, the loss not yet noticed: the new home takes the council");
        var landed = new List<Entry> { new Entry(5) };
        ColonyStateMath.Lost(landed, 5, 6000, new HashSet<int> { 9 });
        Check(ColonyStateMath.Find(landed, 9, 9, new HashSet<int> { 9 }) == landed[0] && landed[0].LostAt == NotLost, "and once the loss is noticed too");
        var lostBefore = new List<Entry> { new Entry(8) { LostAt = 100 }, new Entry(5) };
        Check(ColonyStateMath.Find(lostBefore, 9, 9, new HashSet<int> { 9 }) == lostBefore[1], "the council of the home just left, not one lost long before");
        var stillTwo = new List<Entry> { new Entry(5) };
        Check(ColonyStateMath.Find(stillTwo, 9, 5, new HashSet<int> { 5, 9 }) == null && stillTwo[0].Home == 5, "a second colony founded beside the first has a council of its own");
        var keptOwn = new List<Entry> { new Entry(5), new Entry(9) };
        ColonyStateMath.Lost(keptOwn, 5, 7000, new HashSet<int> { 9 });
        Check(ColonyStateMath.Find(keptOwn, 9, 9, new HashSet<int> { 9 }) == keptOwn[1] && keptOwn[0].LostAt == 7000,
            "people moving to a colony with a council of its own: it keeps its own, the other's is kept a while");
        // Those kept count down only once there's a home again.
        Check(ColonyStateMath.Upkeep(early, 5000 + keep * 3, noHome) == 0 && early.Count == 2, "with no home, nothing is let go");
        Check(ColonyStateMath.Upkeep(early, 5000 + keep * 3, new HashSet<int> { 9 }) == 1 && early.Count == 1 && early[0].Home == None,
            "a home again: what was kept past its time goes, the carried one stays for the new seat");

        // ---------------- Each colony's own people ----------------

        var both = new HashSet<int> { 12, 40 };
        // Where each person belongs (HomesMath.Of: on a home now, else their bed, else the last home they lived on, else the seat).
        var people = new List<int>
        {
            HomesMath.Of(12, None, None, 12, both),
            HomesMath.Of(12, 12, 12, 12, both),
            HomesMath.Of(None, 12, 12, 12, both),
            HomesMath.Of(40, None, None, 12, both),
            HomesMath.Of(None, 40, 12, 12, both),
            HomesMath.Of(None, None, 40, 12, both),
            HomesMath.Of(None, None, None, 12, both)
        };
        Check(ColonyStateMath.CountIn(people, 12) == 4 && ColonyStateMath.CountIn(people, 40) == 3, "each colony counts its own people, those away too");
        Check(ColonyStateMath.CountIn(people, 12) + ColonyStateMath.CountIn(people, 40) == people.Count, "everyone belongs to exactly one colony");
        Check(ColonyStateMath.MayLeave(ColonyStateMath.CountIn(people, 12)) && !ColonyStateMath.MayLeave(ColonyStateMath.CountIn(people, 40)),
            "the last-few guard counts per colony: four may lose one, three may not (seven together could have)");
        // With one colony everyone is its own, and the guard is what it was.
        var lone = new HashSet<int> { 12 };
        var all = new List<int> { HomesMath.Of(12, None, None, 12, lone), HomesMath.Of(None, None, None, 12, lone), HomesMath.Of(None, 40, 40, 12, lone) };
        Check(ColonyStateMath.CountIn(all, 12) == all.Count, "one colony: everyone is its own");
        for (int k = 0; k < 8; k++) Check(ColonyStateMath.MayLeave(k) == k >= 4, "the guard as it always was, at " + k);

        // ---------------- An officer who moves ----------------

        Check(!ColonyStateMath.MovedAway(12, 12, 12), "at home: keeps the seat");
        Check(!ColonyStateMath.MovedAway(12, 12, 40), "back home from the other colony: keeps the seat");
        Check(!ColonyStateMath.MovedAway(12, 40, 12), "visiting the other colony, less than a day: keeps the seat");
        Check(ColonyStateMath.MovedAway(12, 40, 40), "lived at the other colony a day: steps down");
        Check(!ColonyStateMath.MovedAway(12, 40, None), "never lived anywhere a day: keeps the seat");
        Check(!ColonyStateMath.MovedAway(None, 40, 40), "no colony, nothing to step down from");
        return n;
    }
}
