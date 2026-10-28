using System;
using ColonyCouncil;

/// <summary>
/// The price of keeping someone who asked to move on (Social/MovingOnMath): how much they wanted to go, the trust it
/// costs, a longing that grows and lasts, a promise that has to be kept, and a second asking that costs more.
/// </summary>
public static class MovingOnTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "moving on: " + what);
        }

        // ---------------- How much they want to go ----------------

        C(MovingOnMath.Family(100f, false) > MovingOnMath.Family(30f, false) && MovingOnMath.Family(100f, true) >= MovingOnMath.Family(100f, false),
            "the dearer the relative, the more they want to go; most of all to a child of theirs");
        C(MovingOnMath.Family(-50f, false) >= 0.3f && MovingOnMath.Family(100f, true) <= 1f, "never nothing, never more than whole");
        C(MovingOnMath.Love(true, false, 50f) > MovingOnMath.Love(false, true, 50f) && MovingOnMath.Love(false, true, 50f) > MovingOnMath.Love(false, false, 50f),
            "a spouse pulls hardest, then the betrothed, then a lover");
        C(MovingOnMath.Room(0.05f) > MovingOnMath.Room(0.3f) && MovingOnMath.Room(0.3f) >= 0.3f, "the more miserable without a room, the more they want to go");

        // ---------------- What keeping them costs ----------------

        C(MovingOnMath.TrustLoss(8f, 0f) == 4f && MovingOnMath.TrustLoss(8f, 1f) == 12f, "trust lost: half the answer's price for a mild wish, half again for a strong one");
        C(MovingOnMath.TrustLoss(12f, 0.5f) > MovingOnMath.TrustLoss(8f, 0.5f), "saying no a second time costs more");
        C(MovingOnMath.Longing(1f, 0f) == 0.5f && MovingOnMath.Longing(1f, MovingOnMath.RampDays) == 1f && MovingOnMath.Longing(1f, 200f) == 1f,
            "the longing starts at half and grows to its full weight over the first weeks");
        C(MovingOnMath.Stage(0.4f, 0f, false) == 0 && MovingOnMath.Stage(1f, 0f, false) == 1 && MovingOnMath.Stage(1f, 30f, false) == 2,
            "a mild wish weighs lightly; a strong one aches once it has had time");
        C(MovingOnMath.Stage(1f, 30f, true) == 0, "while a promised room isn't due, only its first stage");

        // ---------------- A promise ----------------

        C(MovingOnMath.PromiseOpen(true, 10f) && !MovingOnMath.PromiseBroken(true, 10f), "a promise has its fortnight");
        C(!MovingOnMath.PromiseOpen(true, MovingOnMath.PromiseDays) && MovingOnMath.PromiseBroken(true, MovingOnMath.PromiseDays), "and then it's broken");
        C(!MovingOnMath.PromiseOpen(false, 1f) && !MovingOnMath.PromiseBroken(false, 99f), "no promise, nothing to break");
        C(MovingOnMath.BrokenPromiseTrust > MovingOnMath.KeptPromiseTrust, "a broken promise costs more than a kept one gives back");

        // ---------------- Asking again, and peace ----------------

        C(!MovingOnMath.AskAgain(10f, false, 0.2f, 0.5f, false, false) && MovingOnMath.AskAgain(MovingOnMath.AgainDays, false, 0.2f, 0.5f, false, false),
            "unhappy, they ask a second time after a few weeks");
        C(MovingOnMath.AskAgain(MovingOnMath.AgainLoveDays, true, 0.2f, 0.5f, false, false), "a partner's, sooner: at their next visit");
        C(!MovingOnMath.AskAgain(30f, false, 0.7f, 0.5f, false, false) && MovingOnMath.AskAgain(30f, false, 0.7f, 0.9f, false, false),
            "content and only mildly wishing, they let it rest; wanting it badly, they ask anyway");
        C(!MovingOnMath.AskAgain(30f, false, 0.1f, 1f, true, false), "they ask twice at most");
        C(!MovingOnMath.AskAgain(30f, false, 0.1f, 1f, false, true), "not while a promise still has time");
        C(!MovingOnMath.MadePeace(MovingOnMath.PeaceDays - 1) && MovingOnMath.MadePeace(MovingOnMath.PeaceDays), "after a season they make their peace with it");

        return n;
    }
}
