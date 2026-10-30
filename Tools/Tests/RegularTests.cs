using System;
using ColonyCouncil;

/// <summary>Visitors who come back (Happenings/RegularMath): when they're due, how the bond moves, when the thread turns, who makes way.</summary>
public static class RegularTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "regulars: " + what);
        }

        C(!RegularMath.Due(10f) && RegularMath.Due(25f), "a regular is due back after a few weeks");
        C(!RegularMath.Due(RegularMath.FadeDays + 1f), "and forgotten after half a year away");
        C(RegularMath.BondChange(1f) == 1 && RegularMath.BondChange(0f) == 1 && RegularMath.BondChange(-1f) == -1, "a warm answer adds to the bond, a cold one takes from it");
        C(!RegularMath.Turning(3, 3) && !RegularMath.Turning(4, 2) && RegularMath.Turning(4, 3), "the thread turns after three visits with the bond at four");
        C(RegularMath.Compare(5, 30f, 2, 60f) < 0, "the closer bond comes first");
        C(RegularMath.Compare(3, 60f, 3, 30f) < 0, "between equals, the one longest away");
        C(RegularMath.Weakest(new[] { 3, 1, 2 }, new[] { 10f, 20f, 30f }) == 1, "the faintest bond makes way");
        C(RegularMath.Weakest(new[] { 2, 2 }, new[] { 10f, 40f }) == 1, "between faint equals, the one longest unseen");
        C(RegularMath.Weakest(new int[0], new float[0]) == -1, "nobody from nobody");
        return n;
    }
}
