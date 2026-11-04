using System;
using System.Collections.Generic;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// What a colony is (Lean/HomesMath.cs): the order homes are listed in, the seat, which home someone belongs to and the
/// day it takes to make a home theirs, where a memo's follow-up goes, and a home founded or lost.
/// </summary>
public static class HomesTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void Check(bool ok, string what)
        {
            n++;
            check(ok, "homes: " + what);
        }

        const int None = HomesMath.None;
        const int Day = HomesMath.DayTicks;
        const int Hour = 2500;

        // ---------------- The order ----------------

        List<int> Ordered(params (int key, bool ground)[] homes)
        {
            var list = homes.ToList();
            list.Sort((a, b) => HomesMath.Compare(a.key, a.ground, b.key, b.ground));
            return list.Select(h => h.key).ToList();
        }

        Check(Ordered((40, true), (12, true), (25, true)).SequenceEqual(new[] { 12, 25, 40 }), "the first founded (the lower id) comes first");
        Check(Ordered((5, false), (30, true)).SequenceEqual(new[] { 30, 5 }), "a home on the ground comes before one in orbit founded earlier");
        Check(Ordered((9, false), (3, false), (50, true), (20, true)).SequenceEqual(new[] { 20, 50, 3, 9 }), "the ground's in founding order, then orbit's in founding order");
        Check(Ordered((-7, true), (60, true), (2, false)).SequenceEqual(new[] { 60, -7, 2 }), "a home with no settlement comes after the ground's others, still before orbit");
        Check(HomesMath.Compare(14, true, 14, true) == 0, "a home is level with itself");
        foreach (var (a, b) in new[] { ((3, true), (8, true)), ((3, false), (8, true)), ((-4, true), (8, true)), ((3, true), (-4, false)) })
            Check(Math.Sign(HomesMath.Compare(a.Item1, a.Item2, b.Item1, b.Item2)) == -Math.Sign(HomesMath.Compare(b.Item1, b.Item2, a.Item1, a.Item2)),
                $"the order reads the same both ways ({a.Item1},{b.Item1})");

        // ---------------- The seat ----------------

        var two = new List<int> { 12, 40 };
        Check(HomesMath.Seat(two, None) == 12, "with no seat stored, the first founded");
        Check(HomesMath.Seat(two, 40) == 40, "the stored seat while it's a home");
        Check(HomesMath.Seat(two, 77) == 12, "a stored seat that's no longer a home: back to the first founded");
        Check(HomesMath.Seat(new List<int>(), 12) == None && HomesMath.Seat(null, None) == None, "no home, no seat");
        Check(HomesMath.Seat(new List<int> { 40 }, None) == 40 && HomesMath.Seat(new List<int> { 40 }, 12) == 40, "one home is the seat whatever was stored");
        // People moving don't move it: the seat reads only the homes and the stored one.
        Check(HomesMath.Seat(Ordered((40, true), (12, true)), None) == 12, "the seat is the first founded however the homes were listed");
        Check(HomesMath.Seat(Ordered((5, false), (30, true)), None) == 30, "the seat is on the ground when there's a home there");

        // ---------------- Whose home ----------------

        var homes = new HashSet<int> { 12, 40 };
        Check(HomesMath.Of(40, 12, 12, 12, homes) == 40, "on a home now: that one, before their bed and their last home");
        Check(HomesMath.Of(None, 40, 12, 12, homes) == 40, "away: the home where their bed is");
        Check(HomesMath.Of(None, None, 40, 12, homes) == 40, "no bed: the last home they lived on");
        Check(HomesMath.Of(None, None, None, 12, homes) == 12, "nothing else: the seat");
        Check(HomesMath.Of(77, None, None, 12, homes) == 12, "on a map that isn't a home: as if away");
        Check(HomesMath.Of(None, 77, 40, 12, homes) == 40, "a bed at a home given up doesn't count");
        Check(HomesMath.Of(None, None, 77, 12, homes) == 12, "a last home given up doesn't count");
        Check(HomesMath.Of(None, None, None, None, new HashSet<int>()) == None, "no home at all: none");
        // One colony: everyone belongs to it, wherever they are and whatever they remember.
        var one = new HashSet<int> { 12 };
        foreach (int here in new[] { None, 12, 77 })
        foreach (int bed in new[] { None, 12, 77 })
        foreach (int last in new[] { None, 12, 77 })
            Check(HomesMath.Of(here, bed, last, 12, one) == 12, $"with one colony everyone is of it (here {here}, bed {bed}, last {last})");

        // ---------------- A day makes it their home ----------------

        {
            int at = None, since = 0, last = None;
            for (int h = 0; h < 24; h++) HomesMath.Note(12, h * Hour, ref at, ref since, ref last);
            Check(last == None && at == 12, "23 hours on a home isn't yet a day");
            HomesMath.Note(12, 24 * Hour, ref at, ref since, ref last);
            Check(last == 12, "a full day on a home makes it the last home they lived on");
            // A caravan away: the last home stays; a new home takes a day of its own.
            HomesMath.Note(None, 25 * Hour, ref at, ref since, ref last);
            Check(last == 12 && at == None, "away, the last home stays and the stretch is broken");
            int arrive = 40 * Hour;
            for (int h = 0; h < 24; h++) HomesMath.Note(40, arrive + h * Hour, ref at, ref since, ref last);
            Check(last == 12, "a day less an hour at another home: still the old one");
            HomesMath.Note(40, arrive + Day, ref at, ref since, ref last);
            Check(last == 40, "a day at the other home: it's theirs now");
        }
        {
            // Half a day, away a while, half a day again: no day without a break.
            int at = None, since = 0, last = None;
            for (int h = 0; h <= 12; h++) HomesMath.Note(12, h * Hour, ref at, ref since, ref last);
            HomesMath.Note(None, 13 * Hour, ref at, ref since, ref last);
            for (int h = 14; h <= 26; h++) HomesMath.Note(12, h * Hour, ref at, ref since, ref last);
            Check(last == None, "two half days with a trip between aren't a day");
            // Going straight from one home to another restarts the count.
            int at2 = None, since2 = 0, last2 = None;
            for (int h = 0; h <= 20; h++) HomesMath.Note(12, h * Hour, ref at2, ref since2, ref last2);
            for (int h = 21; h <= 30; h++) HomesMath.Note(40, h * Hour, ref at2, ref since2, ref last2);
            Check(last2 == None && at2 == 40 && since2 == 21 * Hour, "moving between homes starts the day again at the new one");
        }

        // ---------------- Memos ----------------

        Check(HomesMath.Deliver(40, homes, 12) == 40, "a memo's follow-up comes to its colony while that's a home");
        Check(HomesMath.Deliver(77, homes, 12) == 12, "its colony given up: to the seat");
        Check(HomesMath.Deliver(None, homes, 12) == 12, "an older memo with no colony: to the seat");
        Check(HomesMath.Deliver(12, one, 12) == 12 && HomesMath.Deliver(None, one, 12) == 12 && HomesMath.Deliver(77, one, 12) == 12,
            "with one colony every follow-up comes there, as before");
        Check(HomesMath.Deliver(40, new HashSet<int>(), None) == None, "no home: nowhere yet");

        // ---------------- Founded or lost ----------------

        {
            var founded = new List<int>();
            var lost = new List<int>();
            HomesMath.Diff(new List<int> { 12 }, new List<int> { 12 }, founded, lost);
            Check(founded.Count == 0 && lost.Count == 0, "the same homes: nothing to tell");
            HomesMath.Diff(new List<int> { 12 }, new List<int> { 12, 40 }, founded, lost);
            Check(founded.SequenceEqual(new[] { 40 }) && lost.Count == 0, "a home founded");
            HomesMath.Diff(new List<int> { 12, 40 }, new List<int> { 40 }, founded, lost);
            Check(founded.Count == 0 && lost.SequenceEqual(new[] { 12 }), "a home lost");
            HomesMath.Diff(new List<int> { 12 }, new List<int> { 55 }, founded, lost);
            Check(founded.SequenceEqual(new[] { 55 }) && lost.SequenceEqual(new[] { 12 }), "a gravship's move: one home lost, one founded");
            HomesMath.Diff(new List<int> { 12, 40 }, new List<int> { 40, 12 }, founded, lost);
            Check(founded.Count == 0 && lost.Count == 0, "the order they're listed in doesn't count as a change");
        }

        return n;
    }
}
