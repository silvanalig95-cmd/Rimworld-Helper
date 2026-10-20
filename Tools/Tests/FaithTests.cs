using ColonyCouncil;

/// <summary>
/// Faith in the colony (Faith/FaithMath.cs): who counts as devout or wavering, how far a word from someone of another
/// faith moves a listener (openness, pride, opinion, a faith that frowns on preaching), never more than one moment's
/// worth, and how split a colony is.
/// </summary>
public static class FaithTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "faith: " + what);
        }

        Check(FaithMath.Devout(0.9f) && !FaithMath.Devout(0.8f) && FaithMath.Wavering(0.5f) && !FaithMath.Wavering(0.7f), "devout from 85%, wavering under 60%");

        float plain = FaithMath.Sway(0.05f, 1f, 0f, 0f, 0f, false);
        Check(Math.Abs(plain - 0.05f) < 0.0001f, "a plain listener takes the moment's base");
        Check(FaithMath.Sway(0.05f, 1f, 80f, 0f, 0f, false) > plain && FaithMath.Sway(0.05f, 1f, -80f, 0f, 0f, false) < plain, "the open are moved more, the closed less");
        Check(FaithMath.Sway(0.05f, 1f, 0f, 80f, 0f, false) < plain && FaithMath.Sway(0.05f, 1f, 0f, -80f, 0f, false) == plain, "pride gives less ground; humility doesn't add any");
        Check(FaithMath.Sway(0.05f, 1f, 0f, 0f, 60f, false) > plain && FaithMath.Sway(0.05f, 1f, 0f, 0f, -60f, false) < plain, "a friend is heard, a rival less");
        Check(Math.Abs(FaithMath.Sway(0.05f, 1f, 0f, 0f, 0f, true) - plain / 2f) < 0.0001f, "a faith that frowns on preaching halves it");
        Check(FaithMath.Sway(0.05f, 2f, 0f, 0f, 0f, false) > plain && FaithMath.Sway(0.05f, 0f, 0f, 0f, 0f, false) == 0f, "the speaker's conversion power counts; none, nothing");
        Check(FaithMath.Sway(1f, 3f, 100f, -100f, 100f, false) == FaithMath.MaxSway, "never more than one moment's worth");
        Check(FaithMath.Sway(-1f, 1f, 0f, 0f, 0f, false) == 0f, "nothing from nothing");

        Check(FaithMath.Split(1, 6) == 0f && FaithMath.Split(6, 6) == 1f && Math.Abs(FaithMath.Split(5, 6) - 0.8f) < 0.001f && FaithMath.Split(3, 1) == 0f, "how split a colony is");
        return cases;
    }
}
