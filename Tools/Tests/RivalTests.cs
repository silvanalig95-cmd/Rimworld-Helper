using System;
using ColonyCouncil;

/// <summary>The other settlers' year (World/RivalMath.cs): growth, warmth and slights, when they can be taken in.</summary>
public static class RivalTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void Check(bool ok, string what)
        {
            n++;
            check(ok, what);
        }
        Check(RivalMath.Households(4, 0f, false) == 4, "the steading starts as it was founded");
        Check(RivalMath.Households(4, 9f, false) == 5, "a household more after nine days");
        Check(RivalMath.Households(4, 19f, true) == 6, "warmth adds nothing before the twentieth day");
        Check(RivalMath.Households(4, 20f, true) == 7, "a warm colony's neighbours grow a household more from the twentieth day");
        Check(RivalMath.Households(5, 300f, true) == RivalMath.Most, "the steading never grows past its most");
        Check(RivalMath.Households(1, -5f, false) == 1, "days before the start count as none");

        Check(!RivalMath.Sour(new[] { "gift" }), "a gift alone doesn't sour them");
        Check(RivalMath.Sour(new[] { "cold" }), "a cold word alone does");
        Check(!RivalMath.Sour(new[] { "cold", "gift", "shared" }), "two warm answers make up for one slight");
        Check(RivalMath.Sour(new[] { "cold", "warned", "gift" }), "two slights outweigh one warm answer");
        Check(!RivalMath.Sour(null) && !RivalMath.Sour(new string[0]), "nothing answered yet is nothing held against anyone");
        Check(RivalMath.Warm(new[] { "gift", "cold", "fed" }) && !RivalMath.Warm(new[] { "gift", "cold" }), "warm needs more warm answers than slights");
        Check(RivalMath.Warmth(new[] { "gift", "trade", "unknown" }) == 2 && RivalMath.Slights(new[] { "refused", "gate" }) == 1, "only the known flags count");

        Check(RivalMath.CanAbsorb(5) && !RivalMath.CanAbsorb(6), "five households can be taken in, six are too many");
        return n;
    }
}
