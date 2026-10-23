using ColonyCouncil;

/// <summary>
/// The game-free decree rules (Source/ColonyCouncil/Decrees/DecreeMath.cs): when many count as hurt, and a colonist's
/// work order during a clean sweep (essentials, then cleaning, then the rest, each in its own order).
/// </summary>
public static class DecreeMathTests
{
    public static int Run(Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "decrees: " + what);
        }

        // ---------- Many are hurt ----------
        Check(!DecreeMath.ManyHurt(1, 2), "one hurt is never many, even in a colony of two");
        Check(DecreeMath.ManyHurt(2, 3), "two of three");
        Check(DecreeMath.ManyHurt(3, 9) && !DecreeMath.ManyHurt(2, 9), "a third of nine is three");
        Check(DecreeMath.ManyHurt(5, 8), "five of eight, as after a hard raid");
        Check(!DecreeMath.ManyHurt(0, 0), "nobody at all");

        // ---------- A clean sweep's work order ----------
        var own = new List<string> { "build", "doctor", "clean-filth", "research", "cook", "haul" };
        var cleaning = new List<string> { "clean-filth", "clean-snow" };
        var into = new List<string> { "left over" };
        DecreeMath.SweepOrder(own, cleaning, w => w == "doctor" || w == "cook", w => w.StartsWith("clean"), into);
        Check(string.Join(",", into) == "doctor,cook,clean-filth,clean-snow,build,research,haul",
            "essentials first in their own order, then all cleaning, then the rest as set: " + string.Join(",", into));
        Check(into.Count(w => w == "clean-filth") == 1, "cleaning they were already set to isn't listed twice");
        DecreeMath.SweepOrder(new List<string> { "research", "build" }, cleaning, w => false, w => w.StartsWith("clean"), into);
        Check(string.Join(",", into) == "clean-filth,clean-snow,research,build", "those not set to clean clean first too");
        DecreeMath.SweepOrder(new List<string>(), new List<string>(), w => false, w => false, into);
        Check(into.Count == 0, "the list is refilled, not added to");
        return cases;
    }
}
