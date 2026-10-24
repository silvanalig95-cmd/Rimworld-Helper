using ColonyCouncil;

/// <summary>
/// Practice and the habits that grow from it (Source/ColonyCouncil/Quirks/PracticeMath.cs): steady work settles where
/// it should, habits that come only from their work never come without it, a habit lived doesn't fade and deepens in
/// time, and only good moods grow.
/// </summary>
public static class PracticeTests
{
    public static int Run(Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "practice: " + what);
        }

        // Steady work settles at about 20 hours per daily hour; what stops fades by half in about two weeks.
        float h = 0f;
        for (int day = 0; day < 200; day++) h = h * PracticeMath.KeepPerDay + 2f;
        Check(Math.Abs(h * PracticeMath.KeepPerDay + 2f - h) < 0.01f && Math.Abs(h - PracticeMath.Steady(2f)) < 1f,
            $"two hours a day settles near {PracticeMath.Steady(2f):0} ({h:0})");
        int half = 0;
        for (float v = 1f; v > 0.5f; v *= PracticeMath.KeepPerDay) half++;
        Check(half >= 10 && half <= 16, $"what stops fades by half in about two weeks ({half} days)");

        // The chance a habit comes.
        Check(PracticeMath.Earned(0f, 40f) == 1f && PracticeMath.Earned(40f, 40f) == PracticeMath.MaxEarned && PracticeMath.Earned(400f, 40f) == PracticeMath.MaxEarned,
            "by nature: its own chance without the work, up to ten times with it, never more");
        Check(PracticeMath.Earned(20f, 40f) > 1f && PracticeMath.Earned(20f, 40f) < PracticeMath.MaxEarned, "halfway there, somewhere between");
        Check(PracticeMath.Earned(0f, 40f, true) == 0f && PracticeMath.Earned(40f, 40f, true) == PracticeMath.MaxEarned,
            "only from its work: never without it, ten times its chance at the full mark");
        Check(PracticeMath.Earned(10f, 40f, true) < PracticeMath.Earned(30f, 40f, true), "the more of the work, the likelier");
        Check(PracticeMath.Earned(10f, 0f) == 1f && PracticeMath.Earned(10f, 0f, true) == 0f, "no full mark, no help from practice");

        // Lived, kept and deepened.
        Check(!PracticeMath.Lived(10f, 40f) && PracticeMath.Lived(16f, 40f) && !PracticeMath.Lived(16f, 0f), "lived from two fifths of its full mark");
        int day0 = 100 * 60000;
        Check(PracticeMath.KeptUntil(day0 + 5 * 60000, day0, 60000) == day0 + PracticeMath.KeepDays * 60000, "lived today, it fades no sooner than its keep");
        Check(PracticeMath.KeptUntil(day0 + 200 * 60000, day0, 60000) == day0 + 200 * 60000, "and never sooner than it would have");
        Check(PracticeMath.KeptUntil(0, day0, 60000) == 0, "a habit for good stays for good");
        Check(!PracticeMath.Deep(PracticeMath.DeepDays - 1) && PracticeMath.Deep(PracticeMath.DeepDays), "deep-rooted after its days lived");
        Check(PracticeMath.Deep(4, 4) && !PracticeMath.Deep(3, 4), "a habit lived only on rare days takes root sooner");
        Check(PracticeMath.KeptUntil(day0 + 5 * 60000, day0, 60000, true) == day0 + PracticeMath.DeepKeepDays * 60000 && PracticeMath.DeepKeepDays > PracticeMath.KeepDays,
            "a deep-rooted habit, lived, lasts longer once they stop");
        Check(PracticeMath.Mood(2f, true) == 3f && PracticeMath.Mood(2f, false) == 2f && PracticeMath.Mood(-2f, true) == -2f,
            "a deep-rooted habit's good mood grows; a bad one never does");
        // Passed on between people close to each other (QuirkPassMath).
        var bonds = new[] { QuirkPassMath.Bond.Friend, QuirkPassMath.Bond.Family, QuirkPassMath.Bond.Lover, QuirkPassMath.Bond.Mentor, QuirkPassMath.Bond.Parent };
        for (int i = 1; i < bonds.Length; i++)
            Check(QuirkPassMath.Chance(bonds[i], false) > QuirkPassMath.Chance(bonds[i - 1], false), $"{bonds[i]} passes a habit sooner than {bonds[i - 1]}");
        Check(QuirkPassMath.Chance(QuirkPassMath.Bond.None, true) == 0f, "nothing passes to someone not close");
        Check(QuirkPassMath.Chance(QuirkPassMath.Bond.Friend, true) > QuirkPassMath.Chance(QuirkPassMath.Bond.Friend, false), "a deep-rooted habit passes more readily");
        // A parent's habit in a season or two (about 60 to 400 days on average), a friend's in a year or more.
        float parentDays = 1f / QuirkPassMath.Chance(QuirkPassMath.Bond.Parent, false), friendDays = 1f / QuirkPassMath.Chance(QuirkPassMath.Bond.Friend, false);
        Check(parentDays >= 60f && parentDays <= 400f, $"a parent's habit passes in a season or two ({parentDays:0} days)");
        Check(friendDays >= 300f, $"a friend's takes a year or more ({friendDays:0} days)");
        Check(QuirkPassMath.MinOpinion(QuirkPassMath.Bond.Friend) > QuirkPassMath.MinOpinion(QuirkPassMath.Bond.Parent), "a friend has to be liked more than family");
        Check(!QuirkPassMath.AtTheWork(5f, 40f) && QuirkPassMath.AtTheWork(10f, 40f), "a work habit passes only to someone at that work too");
        return cases;
    }
}
