using ColonyCouncil;

/// <summary>
/// Build priorities (Source/ColonyCouncil/Building/BuildPriorityMath.cs): the button steps through every level and back,
/// and work waits only for something at a higher level that can go ahead.
/// </summary>
public static class BuildPriorityTests
{
    public static int Run(Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "build priorities: " + what);
        }

        // A click steps normal → high → urgent → low → normal.
        int l = BuildPriorityMath.Normal;
        var seen = new HashSet<int>();
        for (int i = 0; i < 4; i++)
        {
            seen.Add(l);
            l = BuildPriorityMath.Next(l);
        }
        Check(l == BuildPriorityMath.Normal && seen.Count == 4, "four clicks visit every level once and come back to normal");
        Check(BuildPriorityMath.Levels.Length == 4 && BuildPriorityMath.Levels[0] == BuildPriorityMath.Urgent && BuildPriorityMath.Levels[3] == BuildPriorityMath.Low, "the menu lists the levels from the top");
        Check(BuildPriorityMath.Clamp(9) == BuildPriorityMath.Urgent && BuildPriorityMath.Clamp(-9) == BuildPriorityMath.Low, "a level out of range is kept to the ends");
        // Waiting: only for something higher that can go ahead.
        Check(!BuildPriorityMath.Waits(BuildPriorityMath.Normal, int.MinValue), "nothing waits when nothing set higher can go ahead");
        Check(BuildPriorityMath.Waits(BuildPriorityMath.Normal, BuildPriorityMath.High), "ordinary work waits for high work that can go ahead");
        Check(!BuildPriorityMath.Waits(BuildPriorityMath.Urgent, BuildPriorityMath.Urgent), "urgent work never waits for its own level");
        Check(BuildPriorityMath.Waits(BuildPriorityMath.Low, BuildPriorityMath.Normal) && !BuildPriorityMath.Waits(BuildPriorityMath.Low, BuildPriorityMath.Low), "low work waits for ordinary work, not for other low work");
        return cases;
    }
}
