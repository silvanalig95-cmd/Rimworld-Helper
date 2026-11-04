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

        return n;
    }
}
