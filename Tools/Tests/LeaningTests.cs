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
        // With resists, the decree presses one it can't sway: they dig in (LeaningMath.Defiance). With cycle, the decree
        // holds fifteen days of every thirty-five (its length and cooldown), declared again each time it may be.
        int Days(float closeness, bool decree, float openness, float recovery, bool resists = false, bool cycle = false)
        {
            float pull = 0f, certainty = 1f, defiance = 0f;
            for (int day = 1; day <= 240; day++)
            {
                bool on = decree && (!cycle || (day - 1) % 35 < 15);
                if (resists) defiance = LeaningMath.Defiance(defiance, on, false);
                bool devout = certainty >= 0.85f;
                pull = LeaningMath.Day(pull, LeaningMath.Feed(closeness, on, LeaningMath.Readiness(openness), false, pull, defiance));
                certainty = Math.Min(1f, Math.Max(0.01f, certainty - LeaningMath.Erosion(pull, devout) + recovery + LeaningMath.Steady(defiance)));
                if (LeaningMath.Overtakes(pull, certainty)) return day;
            }
            return -1;
        }
        const float Content = 0.03f, Unhappy = 0.01f;
        int decreeFriends = Days(4f, true, 0f, Content);
        int friendsOnly = Days(4f, false, 0f, Content);
        Check(decreeFriends >= 40 && decreeFriends <= 80, $"with the decree and four friends among its believers, a content colonist comes round in about two months ({decreeFriends} days)");
        Check(friendsOnly > decreeFriends && friendsOnly <= 120, $"four friends and no decree: slower still, over seasons ({friendsOnly} days)");
        Check(Days(0.5f, true, 0f, Content) == -1 && Days(0.5f, true, 100f, Content) == -1, "the decree alone never brings a content colonist over, however open-minded, with nobody of its believers close");
        Check(Days(1f, true, 0f, Content) == -1, "nor with a single friend among them");
        Check(Days(0.5f, true, 0f, Content, false, true) == -1, "declaring it again and again changes nothing");
        int openFriends = Days(2f, true, 100f, Content);
        Check(openFriends > 0 && openFriends < Days(2f, true, 0f, Content), $"the open-minded with friends among its believers come round sooner ({openFriends} days)");
        Check(Days(4f, true, -100f, Content, true) == -1 && Days(4f, true, 0f, Content, true) == -1, "those it can't sway dig in, even among friends of that faith");
        int unhappyAlone = Days(0.5f, true, 0f, Unhappy);
        Check(unhappyAlone > 0 && unhappyAlone <= 45, $"the unhappy can come round by the decree alone ({unhappyAlone} days)");
        Check(Days(0.5f, false, 0f, Content) == -1, "a lone believer of a minority never pulls a content colonist over");
        Check(Days(4f, true, 0f, Unhappy) < decreeFriends, "the unhappy come round sooner");

        // ---------------- The decree's limits ----------------
        Check(LeaningMath.Feed(0f, true, 1f, false, LeaningMath.DecreeCeiling) == 0f && LeaningMath.Feed(2f, true, 1f, false, LeaningMath.DecreeCeiling) == LeaningMath.Company(2f),
            "past its ceiling the decree adds nothing; the believers' company still counts");
        Check(LeaningMath.Feed(4f, true, 1f, false, 0f, 1f) == 0f && LeaningMath.Feed(4f, true, 1f, false, 0f, 0.5f) < LeaningMath.Feed(4f, true, 1f, false),
            "defiance takes the pull of the pressed faith down with it");
        Check(LeaningMath.Resists(-50f, 0f, false) && LeaningMath.Resists(0f, 60f, false) && LeaningMath.Resists(0f, 0f, true) && !LeaningMath.Resists(30f, 10f, false),
            "the set in their ways, the proud and those whose faith frowns on preaching resist; the rest can be swayed");
        float d = 0f;
        for (int i = 0; i < 15; i++) d = LeaningMath.Defiance(d, true, false);
        float dd = 0f;
        for (int i = 0; i < 15; i++) dd = LeaningMath.Defiance(dd, true, true);
        Check(d > 0.5f && d < 1f && dd > d, $"fifteen days pressed, they're dug in ({d:0.00}), more so if their faith frowns on preaching ({dd:0.00})");
        float after = d;
        for (int i = 0; i < 23; i++) after = LeaningMath.Defiance(after, false, false);
        Check(after > d * 0.4f && after < d * 0.6f, "it wears off slowly: about half gone in three weeks");
        for (int i = 0; i < 300; i++) after = LeaningMath.Defiance(after, false, false);
        Check(after == 0f, "and in the end it's forgotten");
        Check(LeaningMath.Steady(1f) == LeaningMath.DefianceSteady && LeaningMath.Steady(0f) == 0f, "the dug in hold to their own the harder");
        Check(LeaningMath.ReturnPull(1f, true) > LeaningMath.ReturnPull(1f) && LeaningMath.ReturnPull(1f, true) <= LeaningMath.PressedReturnMax,
            "who came over under the decree stays drawn back to the faith they left");

        // ---------------- Torn between two ----------------
        Check(LeaningMath.ReturnPull(1f) <= LeaningMath.ReturnMax && LeaningMath.ReturnPull(0.2f) < LeaningMath.ReturnPull(0.4f), "the ideoligion left behind stays only a small leaning");
        Check(!LeaningMath.Settled(0, 10 * 60000, 60000) && LeaningMath.Settled(0, LeaningMath.SettleDays * 60000, 60000) && LeaningMath.Settled(-1, 0, 60000),
            "a year to settle into a new ideoligion");
        Check(!LeaningMath.Overtakes(0.6f, 0.4f, -1) && LeaningMath.Overtakes(0.6f, 0.4f, 0), "a fading leaning never takes over");
        // Friends on both sides, as close: the days they switch over ten years, playing the day's pass (FaithLeanings.Day).
        List<int> Switches(float closeness, float recovery)
        {
            var days = new List<int>();
            float certainty = 1f, toward = 0f;
            int trend = 0, last = -1;
            for (int day = 1; day <= 600; day++)
            {
                float before = toward;
                toward = LeaningMath.Day(toward, LeaningMath.Feed(closeness, false, 1f, false));
                trend = LeaningMath.Trend(before, toward);
                bool settled = LeaningMath.Settled(last, day, 1);
                float wear = settled ? LeaningMath.Erosion(toward, certainty >= 0.85f) : 0f;
                certainty = Math.Min(1f, Math.Max(0.01f, certainty - wear + recovery));
                if (settled && LeaningMath.Overtakes(toward, certainty, trend))
                {
                    float left = certainty;
                    certainty = Math.Max(toward, LeaningMath.MinSwitch);
                    toward = LeaningMath.ReturnPull(left);
                    last = day;
                    days.Add(day);
                }
            }
            return days;
        }
        foreach (var (recovery, who) in new[] { (Content, "content"), (Unhappy, "unhappy") })
        {
            var torn = Switches(3f, recovery);
            bool spaced = true;
            for (int i = 1; i < torn.Count; i++) spaced &= torn[i] - torn[i - 1] >= LeaningMath.SettleDays;
            Check(spaced && torn.Count <= 10, $"a {who} colonist torn between friends of two ideoligions switches at most once a year, not back and forth ({torn.Count} times in ten years)");
        }
        return cases;
    }
}
