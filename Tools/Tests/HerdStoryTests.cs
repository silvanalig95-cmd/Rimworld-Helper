using System;
using ColonyCouncil;

/// <summary>The herd's year and the beast that stays (Animals/HerdStoryMath.cs).</summary>
public static class HerdStoryTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void Check(bool ok, string what)
        {
            n++;
            check(ok, what);
        }

        Check(HerdStoryMath.LeaderScore(6f, 6, 8, false) > HerdStoryMath.LeaderScore(3f, 6, 8, false), "the elder leads");
        Check(HerdStoryMath.LeaderScore(4f, 6, 8, false) > HerdStoryMath.LeaderScore(4f, 1, 8, false), "one of the many leads before the lone goat");
        Check(HerdStoryMath.LeaderScore(2f, 2, 8, true) > HerdStoryMath.LeaderScore(7f, 6, 8, false), "the old leader's blood comes first");
        Check(HerdStoryMath.LeaderScore(-1f, 0, 0, false) == 0f, "nothing below nothing");
        Check(HerdStoryMath.Generation(1) == 2 && HerdStoryMath.Generation(0) == 2, "a newborn is one generation on from its mother, and never before the second");
        Check(HerdStoryMath.GenerationNews(2, 1) && !HerdStoryMath.GenerationNews(2, 2) && !HerdStoryMath.GenerationNews(1, 0), "a generation is news the first time it's reached, from the second on");
        Check(HerdStoryMath.SpringCalf(true, 5503, 5502) && !HerdStoryMath.SpringCalf(true, 5503, 5503) && !HerdStoryMath.SpringCalf(false, 5503, 5502), "one first calf a spring, in spring");

        Check(HerdStoryMath.BeastFits(0.8f, true, false, false), "a wolf fits");
        Check(!HerdStoryMath.BeastFits(0.3f, true, false, false), "a fox is too small to be the beast");
        Check(HerdStoryMath.BeastFits(2f, false, false, false) && !HerdStoryMath.BeastFits(1f, false, false, false), "a stag fits, a hare doesn't");
        Check(!HerdStoryMath.BeastFits(3f, true, false, true) && !HerdStoryMath.BeastFits(3f, false, true, false), "great beasts and insects are other stories");
        Check(!HerdStoryMath.AskDue(3f, 0, 0f) && HerdStoryMath.AskDue(4f, 0, 0f), "the first question four days after the naming");
        Check(!HerdStoryMath.AskDue(10f, 1, 5f) && HerdStoryMath.AskDue(10f, 1, 8f), "and again after a gap");
        Check(!HerdStoryMath.AskDue(40f, HerdStoryMath.MaxAsks, 40f), "three questions at most");
        Check(HerdStoryMath.Stance(1, true) == "fed" && HerdStoryMath.Stance(0, true) == "feared" && HerdStoryMath.Stance(0, false) == "watched", "fed outweighs feared, else watched");
        return n;
    }
}
