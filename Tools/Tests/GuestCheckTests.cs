using System;
using ColonyCouncil;
using V = ColonyCouncil.GuestCheckMath.Verdict;

/// <summary>
/// Whether a Hospitality guest can be recruited, and how (Integrations/GuestCheckMath): goodwill, a death acidifier
/// and a relative here, a title, settling in, friends enough and Rimfolk's will to stay.
/// </summary>
public static class GuestCheckTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "recruiting a guest: " + what);
        }

        // goodwill, acidifier, related, titled, settled, enough friends, willing
        C(GuestCheckMath.Of(true, false, false, false, true, true, true) == V.Ready, "friends enough and willing: ready to recruit");
        C(GuestCheckMath.Of(true, false, false, false, true, false, true) == V.ForceOnly, "too few friends: only by force for now");
        C(GuestCheckMath.Of(true, false, false, false, true, true, false) == V.ForceOnly, "no will to stay: only by force for now");
        C(GuestCheckMath.Of(false, false, false, false, true, true, true) == V.Never, "their people keep no goodwill: never");
        C(GuestCheckMath.Of(true, true, false, false, true, true, true) == V.ForceOnly, "a death acidifier and no relative here: only by force, however friendly");
        C(GuestCheckMath.Of(true, true, true, false, true, true, true) == V.Ready, "a death acidifier but a relative here: may join");
        C(GuestCheckMath.Of(true, true, false, true, true, true, true) == V.NeverUnlessRelated, "titled with a death acidifier and no relative: never, unless that changes");
        C(GuestCheckMath.Of(true, true, true, true, true, true, true) == V.Ready, "titled with an acidifier but a relative here, ranks enough: ready");
        C(GuestCheckMath.Of(true, false, false, true, true, false, true) == V.NotYet, "titled, too few royal friends: not by force, not yet");
        C(GuestCheckMath.Of(true, false, false, false, false, true, true) == V.NotNow, "still settling in: not right now");
        C(GuestCheckMath.Of(false, true, false, true, false, false, false) == V.Never, "no goodwill comes before everything else");
        C(GuestCheckMath.ForceOpen(true, false) && !GuestCheckMath.ForceOpen(true, true) && !GuestCheckMath.ForceOpen(false, false),
            "by force: open only to the untitled of people who deal in goodwill");
        return n;
    }
}
