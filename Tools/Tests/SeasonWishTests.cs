using ColonyCouncil;

/// <summary>
/// The wishes of the land's year (SeasonWishMath): the season to dress for, how far one new piece of clothing is asked to
/// take someone, when it's covered or the season has passed; when a room is hot enough to be a sauna (a heater set high,
/// a campfire as warm as it gets); hot country; and the day the fields open after a cold season.
/// </summary>
public static class SeasonWishTests
{
    public static int Run(Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "the land's year: " + what);
        }

        bool Near(float a, float b) => Math.Abs(a - b) < 1e-3f;

        // A temperate year: cold winters (twelfths 9 to 11 and 0 below zero), warm summers.
        float[] temperate = { -6f, -2f, 3f, 8f, 13f, 18f, 21f, 20f, 15f, 8f, 1f, -5f };
        // A desert: hot most of the year.
        float[] desert = { 18f, 21f, 25f, 29f, 33f, 36f, 37f, 35f, 31f, 27f, 22f, 19f };
        // A tropical year: warm all year, never hot enough to be a desert.
        float[] tropical = { 24f, 24f, 25f, 25f, 26f, 26f, 26f, 26f, 25f, 25f, 24f, 24f };

        // ---------- The season to dress for ----------
        Check(Near(SeasonWishMath.SeasonTemp(temperate, 9, false), -6f - SeasonWishMath.DayDrift), "in autumn, the cold coming: the coldest of the next twelfths, a cool evening's worth below");
        Check(Near(SeasonWishMath.SeasonTemp(temperate, 10, false), -6f - SeasonWishMath.DayDrift), "the year wraps round into the new year's cold");
        Check(Near(SeasonWishMath.SeasonTemp(temperate, 4, false), 13f - SeasonWishMath.DayDrift), "in summer there's no cold coming");
        Check(Near(SeasonWishMath.SeasonTemp(desert, 3, true), 37f + SeasonWishMath.DayDrift), "the heat coming: the hottest twelfth ahead, a warm afternoon's worth above");
        Check(SeasonWishMath.SeasonTemp(null, 0, false) == float.MaxValue && SeasonWishMath.SeasonTemp(null, 0, true) == float.MinValue, "no year known: nothing to dress for");

        // ---------- How far to ask: the season, or as far as one piece goes ----------
        // Comfortable to 10 °C, winter brings -10: a parka (+30 over what it replaces) covers it all, give or take the leeway.
        Check(Near(SeasonWishMath.ClothesTarget(10f, -10f, 30f, false), -8f), "one good piece covers the season (to the season, with leeway)");
        // A thin cloak only (+6): ask for what it can do, not the impossible.
        Check(Near(SeasonWishMath.ClothesTarget(10f, -10f, 6f, false), 4f), "a small piece: only as far as it goes");
        Check(float.IsNaN(SeasonWishMath.ClothesTarget(10f, 8f, 30f, false)), "already dressed near enough: no wish");
        Check(float.IsNaN(SeasonWishMath.ClothesTarget(10f, -10f, 2f, false)), "nothing they could have helps enough: no wish");
        Check(float.IsNaN(SeasonWishMath.ClothesTarget(float.NaN, -10f, 30f, false)), "nothing known: no wish");
        // Heat: comfortable to 26 °C, summer afternoons at 41: a duster (+18) takes them most of the way.
        Check(Near(SeasonWishMath.ClothesTarget(26f, 41f, 18f, true), 39f), "the heat: to the season, with leeway");
        Check(Near(SeasonWishMath.ClothesTarget(26f, 41f, 8f, true), 34f), "the heat: as far as one piece goes");
        Check(float.IsNaN(SeasonWishMath.ClothesTarget(38f, 40f, 18f, true)), "the heat: dressed near enough already");
        for (float comfy = -20f; comfy <= 20f; comfy += 5f)
            for (float gain = 0f; gain <= 40f; gain += 4f)
            {
                float t = SeasonWishMath.ClothesTarget(comfy, -15f, gain, false);
                if (float.IsNaN(t)) continue;
                Check(t < comfy && comfy - t <= gain + 0.5f && t >= -15f + SeasonWishMath.Leeway - 0.5f,
                    $"cold target {t} from {comfy} with {gain}: warmer than now, no further than the piece, no colder than the season needs");
            }
        Check(SeasonWishMath.Covered(-8f, -8f, false) && SeasonWishMath.Covered(-8.4f, -8f, false) && !SeasonWishMath.Covered(-7f, -8f, false), "covered: down to the target, within half a degree");
        Check(SeasonWishMath.Covered(39f, 39f, true) && !SeasonWishMath.Covered(37f, 39f, true), "covered for the heat: up to the target");
        Check(SeasonWishMath.SeasonPassed(5f, 9f, false) && !SeasonWishMath.SeasonPassed(5f, -10f, false), "the cold has passed once the season's evenings are milder than their limit");
        Check(SeasonWishMath.SeasonPassed(30f, 28f, true) && !SeasonWishMath.SeasonPassed(30f, 40f, true), "the heat has passed once the afternoons are cooler than their limit");

        // ---------- A sauna ----------
        Check(SeasonWishMath.SaunaHotEnough(28f, 99f) && !SeasonWishMath.SaunaHotEnough(24f, 99f), "with a heater: 28 °C");
        Check(SeasonWishMath.SaunaHotEnough(21.5f, 23f) && !SeasonWishMath.SaunaHotEnough(19f, 23f), "with a campfire (stops at 23 °C): as warm as it gets, give or take two degrees");
        Check(!SeasonWishMath.SaunaHotEnough(17f, 10f), "never below 18 °C, whatever the heat");
        Check(!SeasonWishMath.SaunaHotEnough(float.NaN, 99f), "no temperature known: no sauna");

        // ---------- Hot country ----------
        Check(SeasonWishMath.HotLand(desert, false), "a desert is hot country all year");
        Check(SeasonWishMath.HotLand(tropical, false), "the tropics too");
        Check(!SeasonWishMath.HotLand(temperate, false) && !SeasonWishMath.HotLand(temperate, true), "a temperate year isn't, not even in summer (21 °C at most)");
        float[] hotSummer = { -10f, -5f, 2f, 10f, 18f, 24f, 28f, 27f, 19f, 10f, 0f, -8f };
        Check(SeasonWishMath.HotLand(hotSummer, true) && !SeasonWishMath.HotLand(hotSummer, false), "a land with fierce summers: in summer only");
        Check(!SeasonWishMath.HotLand(null, true) && !SeasonWishMath.HotLand(new float[0], true), "no year known: not hot");

        // ---------- The fields opening ----------
        bool[] grows = temperate.Select(t => t >= 6f).ToArray();
        Check(SeasonWishMath.HasColdSeason(grows), "a temperate year has a cold season");
        Check(!SeasonWishMath.HasColdSeason(desert.Select(t => t >= 6f).ToArray()), "a desert grows all year: no planting day");
        Check(!SeasonWishMath.HasColdSeason(new bool[12]), "an ice sheet never grows: no planting day");
        int opens = Array.FindIndex(grows, g => g) ;
        Check(opens == 3 && SeasonWishMath.OpensGrowing(grows, 3), "the fields open in the first twelfth warm enough (the fourth)");
        Check(!SeasonWishMath.OpensGrowing(grows, 4) && !SeasonWishMath.OpensGrowing(grows, 2), "not the twelfth after, nor the cold one before");
        int openings = Enumerable.Range(0, 12).Count(i => SeasonWishMath.OpensGrowing(grows, i));
        Check(openings == 1, $"one opening a year in a temperate year (got {openings})");
        Check(SeasonWishMath.PlantingWindow(grows, 3, 4), "the whole opening twelfth is the window");
        Check(SeasonWishMath.PlantingWindow(grows, 4, 1) && !SeasonWishMath.PlantingWindow(grows, 4, 2), "and the first two days of the next");
        Check(!SeasonWishMath.PlantingWindow(grows, 8, 0), "not in the middle of summer");
        Check(SeasonWishMath.OpensGrowing(grows, 15), "a twelfth counted past the year's end wraps round");
        Check(SeasonWishMath.FullySown(90, 100) && !SeasonWishMath.FullySown(89, 100) && !SeasonWishMath.FullySown(0, 0), "fully sown at nine cells in ten");

        return cases;
    }
}
