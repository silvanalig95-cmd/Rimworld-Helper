using System;
using System.Collections.Generic;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// Many homes, the rest of it (stage 0, step 7b): memo pick-ups and unanswered letters by colony (Lean/HomesMath.cs).
/// </summary>
public static class ManyHomesTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void Check(bool ok, string what)
        {
            n++;
            check(ok, "many homes: " + what);
        }

        const int None = HomesMath.None;
        var two = new HashSet<int> { 12, 40 };
        var one = new HashSet<int> { 12 };

        // ---------------- Memo pick-ups ----------------

        Check(HomesMath.MemoAt(40, 40, two, 12), "a memo is picked up at its own colony");
        Check(!HomesMath.MemoAt(40, 12, two, 12), "not at the seat when it is another colony's");
        Check(!HomesMath.MemoAt(12, 40, two, 12), "nor the seat's at another colony");
        Check(HomesMath.MemoAt(None, 12, two, 12) && !HomesMath.MemoAt(None, 40, two, 12), "an old memo with no colony is the seat's");
        Check(HomesMath.MemoAt(77, 12, two, 12) && !HomesMath.MemoAt(77, 40, two, 12), "a memo of a home given up is the seat's");
        foreach (int memo in new[] { 12, None, 77 })
            Check(HomesMath.MemoAt(memo, 12, one, 12), "one colony: every memo is picked up there (" + memo + ")");
        {
            // The same memos in the same order with one colony: a filter that keeps all of them.
            var memos = new[] { 12, None, 77, 12 };
            Check(memos.Where(m => HomesMath.MemoAt(m, 12, one, 12)).Count() == memos.Length, "one colony: the filter keeps every memo, in order");
            var atSeat = memos.Where(m => HomesMath.MemoAt(m, 12, two, 12)).ToList();
            var atNew = new[] { 40, 12, None }.Where(m => HomesMath.MemoAt(m, 40, two, 12)).ToList();
            Check(atSeat.Count == 4 && atNew.SequenceEqual(new[] { 40 }), "two colonies: each picks up its own");
        }

        // ---------------- An unanswered letter ----------------

        Check(HomesMath.HoldsBack(40, 40, two), "a letter about this colony holds it back");
        Check(!HomesMath.HoldsBack(40, 12, two) && !HomesMath.HoldsBack(12, 40, two), "a letter about another colony doesn't");
        Check(HomesMath.HoldsBack(None, 40, two) && HomesMath.HoldsBack(None, 12, two), "a letter about no colony holds every colony back");
        Check(HomesMath.HoldsBack(77, 40, two) && HomesMath.HoldsBack(77, 12, two), "a letter about a home given up holds every colony back");
        Check(HomesMath.HoldsBack(40, None, two), "asked for no colony, any letter holds back");
        Check(HomesMath.HoldsBack(12, 12, one) && HomesMath.HoldsBack(None, 12, one), "one colony: every letter holds it back");

        // ---------------- A calendar per colony ----------------

        const int Day = CalendarMath.TicksPerDay;
        const int Year = CalendarMath.TicksPerYear;
        int now = Year * 3;
        Check(CalendarMath.FoundedTick(0, now) == 0, "the first colony is founded with the game");
        Check(CalendarMath.FoundedTick(Day, now) == 0 && CalendarMath.FoundedTick(Day * CalendarMath.FirstDays, now) == 0, "a colony settled in the game's first days too");
        Check(CalendarMath.FoundedTick(-1, now) == 0, "when the game doesn't say: the game's start, as always");
        Check(CalendarMath.FoundedTick(Year * 2 + 500, now) == Year * 2 + 500, "a colony founded later has its own founding day");
        Check(CalendarMath.FoundedTick(now + Day, now) == 0, "a settlement dated in the future isn't believed");
        // One of the first members: as before for the first colony, from its own founding for a later one.
        for (int joined = 0; joined <= Year + Day * 3; joined += Day / 2)
            Check(CalendarMath.FirstMember(joined, 0) == (joined > Day * 2 && joined <= Year), "the first colony's first members, as before (" + joined + ")");
        int later = Year * 2;
        Check(!CalendarMath.FirstMember(Year / 2, later), "someone who joined before a later colony was founded isn't one of its first");
        Check(CalendarMath.FirstMember(later + Day * 10, later) && !CalendarMath.FirstMember(later + Day, later), "its first year's newcomers are, from after its first days");
        Check(!CalendarMath.FirstMember(later + Year + Day, later), "not after its first year");
        {
            // The calendars' book follows the council's rules: an old save's one calendar is the seat's, untouched.
            var old = new Cal(None);
            var book = new List<Cal> { old };
            Check(ColonyStateMath.Find(book, 40, 12) == null && old.Home == None, "an old save's calendar isn't taken by another colony");
            Check(ColonyStateMath.Find(book, 12, 12) == old && old.Home == 12 && book.Count == 1, "the seat takes an old save's calendar");
            var second = new Cal(40);
            book.Add(second);
            Check(ColonyStateMath.Find(book, 40, 12) == second && ColonyStateMath.Find(book, 12, 12) == old, "each colony keeps its own calendar");
            // Gravship: the old home given up, the new one founded within the hour: the calendar moves with its people.
            var moving = new List<Cal> { new Cal(12) };
            Check(ColonyStateMath.Find(moving, 55, 55, new HashSet<int> { 55 }) == moving[0] && moving[0].Home == 55, "a colony that moves keeps its calendar");
        }

        // ---------------- Camps per home ----------------

        {
            // Two homes, each with its own clock: the seat's last camp left yesterday, the new home has never had one.
            int at = Day * 100;
            var seat = (lastEnded: at - Day, nextTry: at - 10, tryHours: 0);
            var other = (lastEnded: -9999999, nextTry: -1, tryHours: 3);
            Check(CampStoryMath.InGap(at, seat.lastEnded, 15f) && !CampStoryMath.InGap(at, other.lastEnded, 15f), "one home's last camp keeps only that home waiting");
            Check(!CampStoryMath.RepickTry(seat.nextTry, at) && CampStoryMath.RepickTry(other.nextTry, at), "each home picks its own day to try");
            Check(CampStoryMath.RepickTry(at + Day * 4, at) && !CampStoryMath.RepickTry(at + Day * 3, at), "a try further off than a few days is picked again");
            Check(CampStoryMath.PutOff(true, other.tryHours, 23) && !CampStoryMath.PutOff(true, 23, 23) && !CampStoryMath.PutOff(false, 0, 23),
                "a passing reason puts a home's try off by the hour, for up to a day");
            Check(!CampStoryMath.InGap(at, at - Day * 15, 15f) && CampStoryMath.InGap(at, at - Day * 15 + 1, 15f), "the gap is the days given, to the tick");
            // An old save's camps and clock are the seat's (the camps' book follows the council's rules).
            var old = new Cal(None);
            var camps = new List<Cal> { old };
            Check(ColonyStateMath.Find(camps, 40, 12) == null && ColonyStateMath.Find(camps, 12, 12) == old && old.Home == 12, "an old save's camp and clock go to the seat");
        }

        return n;
    }

    private sealed class Cal : IColonyEntry
    {
        public int Home { get; set; }
        public int LostAt { get; set; } = ColonyStateMath.NotLost;

        public Cal(int home)
        {
            Home = home;
        }
    }
}
