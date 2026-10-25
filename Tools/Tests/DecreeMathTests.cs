using ColonyCouncil;

/// <summary>
/// The game-free decree rules (Source/ColonyCouncil/Decrees/DecreeMath.cs): when many count as hurt, how much a decree
/// is needed and wanted and what that lets off its rest and a repeat's resentment (never all of either), and a
/// colonist's work order during a clean sweep (essentials, then cleaning, then the rest, each in its own order).
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

        // ---------- Needed and wanted ----------
        Check(DecreeMath.Need(0) == 0f && DecreeMath.Need(1) > 0.3f && DecreeMath.Need(1) < 0.4f && DecreeMath.Need(3) == 1f && DecreeMath.Need(9) == 1f,
            "one moment is a third of the need, three's worth is all of it");
        Check(DecreeMath.Want(3, 3, 1, 0, 0, false) > 0.6f && DecreeMath.Want(0, 0, 2, 4, 3, false) == 0f && DecreeMath.Want(0, 0, 0, 0, 0, false) == 0f,
            "wanted when most like it, not when most are against it");
        Check(DecreeMath.Want(1, 1, 2, 2, 0, true) > DecreeMath.Want(1, 1, 2, 2, 0, false) && DecreeMath.Want(5, 0, 0, 0, 0, true) <= 1f, "a petition adds, within bounds");
        Check(DecreeMath.Understanding(0f, 1f, 1) == 0f, "wanted but not needed: no sooner");
        float full = DecreeMath.Understanding(1f, 1f, 1), noOne = DecreeMath.Understanding(1f, 0f, 1);
        Check(Math.Abs(full - 1f) < 1e-4f && noOne > 0.3f && noOne < 0.5f, "needed and wanted: all of it; needed and wanted by nobody: some");
        Check(DecreeMath.Understanding(1f, 1f, 2) < full * 0.6f && DecreeMath.Understanding(1f, 1f, 1) == full && DecreeMath.Understanding(1f, 1f, 0) == full,
            "a second time is understood; from the third in a month, patience wears thin");
        const int Day = 60000;
        Check(DecreeMath.CooldownTicks(8f, 0f, Day) == 8 * Day, "not needed: its own rest");
        int sooner = DecreeMath.CooldownTicks(8f, 1f, Day);
        Check(sooner < 3 * Day && sooner >= Day, $"a clean sweep after fight on fight, filth and wounds, wanted by most: ready in under three days ({sooner / (float)Day:0.0}), never under one");
        Check(DecreeMath.CooldownTicks(2f, 1f, Day) == Day && DecreeMath.CooldownTicks(0.5f, 1f, Day) == Day / 2, "never shorter than a day, nor longer than its own rest");
        bool never = true;
        for (int s2 = 0; s2 <= 6; s2++)
            for (int t = 1; t <= 5; t++)
                for (float w = 0f; w <= 1f; w += 0.25f)
                {
                    float u = DecreeMath.Understanding(DecreeMath.Need(s2), w, t);
                    int c = DecreeMath.CooldownTicks(10f, u, Day);
                    if (c < Day || c > 10 * Day || c < 10 * Day * (1f - DecreeMath.MostCooldownLetOff) - 1 || DecreeMath.BacklashFactor(u) < 1f - DecreeMath.MostBacklashLetOff - 1e-4f) never = false;
                }
        Check(never, "however needed, a decree keeps at least a third of its rest and more than a third of a repeat's resentment: it can't be declared over and over");
        Check(DecreeMath.BacklashFactor(0f) == 1f && DecreeMath.BacklashFactor(1f) < 0.5f, "a needed repeat is minded less");

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
