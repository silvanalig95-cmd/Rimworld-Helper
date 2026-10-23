using ColonyCouncil;

/// <summary>
/// Leanings (Source/ColonyCouncil/Faith/LeaningMath.cs): how a colonist is drawn to the other ideoligions around them,
/// how fast, and when they take one up. The pace is checked against the game's own daily recovery of certainty for a
/// content colonist (about three points a day) and an unhappy one (about one).
/// </summary>
public static class LeaningTests
{
    public static int Run(Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "leanings: " + what);
        }

        // ---------------- The parts ----------------
        Check(LeaningMath.Closeness(-100f, false) == 0f && Math.Abs(LeaningMath.Closeness(0f, false) - 0.5f) < 0.001f
              && Math.Abs(LeaningMath.Closeness(50f, false) - 1f) < 0.001f && LeaningMath.Closeness(100f, false) == 1.5f,
            "a rival pulls nothing, a stranger half, a friend one, the dearest one and a half");
        Check(LeaningMath.Closeness(50f, true) > LeaningMath.Closeness(50f, false), "a lover or family pulls harder");
        Check(LeaningMath.Company(0f) == 0f && LeaningMath.Company(2f) < LeaningMath.Company(4f) && LeaningMath.Company(100f) < LeaningMath.BelieversMax,
            "more believers pull more, never past the most a day");
        Check(LeaningMath.Readiness(100f) > LeaningMath.Readiness(0f) && LeaningMath.Readiness(-100f) < LeaningMath.Readiness(0f),
            "the open lean more readily, the closed less");
        Check(LeaningMath.Feed(0f, true, 1f, false) == LeaningMath.Decree && LeaningMath.Feed(0f, true, 1f, true) < LeaningMath.Decree,
            "the decree feeds a leaning on its own, half as much for one whose ideoligion frowns on preaching");
        Check(LeaningMath.Day(0.5f, 0f) < 0.5f && LeaningMath.Day(0.5f, 0.05f) > LeaningMath.Day(0.5f, 0f) && LeaningMath.Day(0.99f, 1f) <= 1f,
            "a leaning fades unfed, grows fed, and never passes whole");
        Check(LeaningMath.Erosion(0.5f, true) < LeaningMath.Erosion(0.5f, false), "the devout are worn less");
        Check(!LeaningMath.Overtakes(0.6f, 0.58f) && LeaningMath.Overtakes(0.6f, 0.5f) && !LeaningMath.Overtakes(0.25f, 0.1f),
            "a leaning takes over only clearly past their certainty, and never while it's slight");
        Check(LeaningMath.Trend(0.4f, 0.41f) == 1 && LeaningMath.Trend(0.41f, 0.4f) == -1 && LeaningMath.Trend(0.4f, 0.4005f) == 0, "growing, fading, holding");

        // ---------------- The pace ----------------
        // Days until they take the ideoligion up (or -1 within two years): fed each day, worn against the game's recovery.
        int Days(float closeness, bool decree, float openness, float recovery)
        {
            float pull = 0f, certainty = 1f;
            for (int day = 1; day <= 240; day++)
            {
                bool devout = certainty >= 0.85f;
                pull = LeaningMath.Day(pull, LeaningMath.Feed(closeness, decree, LeaningMath.Readiness(openness), false));
                certainty = Math.Min(1f, Math.Max(0.01f, certainty - LeaningMath.Erosion(pull, devout) + recovery));
                if (LeaningMath.Overtakes(pull, certainty)) return day;
            }
            return -1;
        }
        const float Content = 0.03f, Unhappy = 0.01f;
        int decreeFriends = Days(4f, true, 0f, Content);
        Check(decreeFriends >= 15 && decreeFriends <= 40, $"with the decree and four friends among its believers, a content colonist comes round in about a month ({decreeFriends} days)");
        int decreeAlone = Days(0.5f, true, 0f, Content);
        Check(decreeAlone > decreeFriends && decreeAlone <= 60, $"with the decree and no friends among them, later ({decreeAlone} days)");
        int closedDecree = Days(4f, true, -100f, Content);
        Check(closedDecree == -1 || closedDecree > 2 * decreeFriends, $"the closed-minded hold out far longer ({closedDecree})");
        int friendsOnly = Days(4f, false, 0f, Content);
        Check(friendsOnly > 40 && friendsOnly <= 120, $"four friends and no decree: slowly, over seasons ({friendsOnly} days)");
        Check(Days(0.5f, false, 0f, Content) == -1, "a lone believer of a minority never pulls a content colonist over");
        Check(Days(4f, true, 0f, Unhappy) < decreeFriends, "the unhappy come round sooner");
        return cases;
    }
}
