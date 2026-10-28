using System;
using ColonyCouncil;

/// <summary>
/// The weather camps come in and stay through (Camps/CampWeatherMath): any but the really extreme, dressed for the
/// stay's coldest night and hottest day, pitched for it, and packing up only when the weather would hurt most of them.
/// </summary>
public static class CampWeatherTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "camp weather: " + what);
        }

        // Someone in ordinary clothes: comfortable from 16 to 28 °C.
        const float lo = 16f, hi = 28f;

        // ---------------- Really extreme ----------------

        C(!CampWeatherMath.TooExtremeToCome(35f) && !CampWeatherMath.TooExtremeToCome(-15f), "a hot summer's day or a hard frost is no reason to stay away");
        C(CampWeatherMath.TooExtremeToCome(CampWeatherMath.ExtremeHeat + 1f) && CampWeatherMath.TooExtremeToCome(CampWeatherMath.ExtremeCold - 1f),
            "past 45 °C or below -30 °C nobody makes camp");
        C(!CampWeatherMath.TooExtremeToStay(CampWeatherMath.ExtremeHeat + 2f) && CampWeatherMath.TooExtremeToStay(CampWeatherMath.ExtremeHeat + CampWeatherMath.StayPast + 1f),
            "a camp that stands waits out a little worse than that before the heat alone sends it off");
        bool cameStays = true;
        for (float t = -60f; t <= 70f; t += 0.5f)
            if (!CampWeatherMath.TooExtremeToCome(t) && CampWeatherMath.TooExtremeToStay(t)) cameStays = false;
        C(cameStays, "weather a camp comes in never sends it straight off again");

        // ---------------- The stay's weather ----------------

        C(CampWeatherMath.StayLow(20f, 8f) == 8f - CampWeatherMath.Margin && CampWeatherMath.StayHigh(20f, 31f) == 31f + CampWeatherMath.Margin,
            "they dress for the stay's coldest and hottest hours, and a little past them");
        C(CampWeatherMath.StayLow(-5f, 3f) == -5f && CampWeatherMath.StayHigh(38f, 30f) == 38f, "and never for milder weather than they find now");
        C(CampWeatherMath.Lasting(41f, 30f, false) == 11f && CampWeatherMath.Lasting(41f, 30f, true) == 0f,
            "a heat wave running now is in the hot hours ahead, not the cold ones");
        C(CampWeatherMath.Lasting(-20f, -2f, true) == -18f && CampWeatherMath.Lasting(-20f, -2f, false) == 0f, "a cold snap is in the cold hours ahead");

        // ---------------- Dressing for it ----------------

        C(CampWeatherMath.Shortfall(lo, hi, 18f, 26f) == 0f, "comfortable through it: nothing to put on");
        C(CampWeatherMath.Shortfall(lo, hi, 0f, 26f) == 16f && CampWeatherMath.Shortfall(lo, hi, 18f, 36f) == 8f, "how far short of comfortable the cold or the heat leaves them");
        C(CampWeatherMath.Shortfall(lo - 20f, hi, 0f, 26f) < CampWeatherMath.Shortfall(lo, hi, 0f, 26f), "a warm layer brings them closer to comfortable on cold nights");
        C(CampWeatherMath.Shortfall(lo, hi, -4f, 34f) == 20f + 6f, "a stay of frosty nights and hot days counts both");

        // ---------------- Stopping, staying, packing up ----------------

        C(CampWeatherMath.WouldStop(8f, 36f, lo, hi), "dressed for ordinary days, cool nights and a warm afternoon don't keep them away");
        C(!CampWeatherMath.WouldStop(lo - CampWeatherMath.SafeMargin - 1f, 26f, lo, hi) && !CampWeatherMath.WouldStop(18f, hi + CampWeatherMath.SafeMargin + 1f, lo, hi),
            "a night or a day that would hurt one of them, dressed as they are, keeps them away");
        C(CampWeatherMath.WouldStop(-30f, 20f, -25f, 24f), "a parka takes them through nights that would turn a shirt away");
        C(!CampWeatherMath.Exposed(37f, lo, hi) && CampWeatherMath.Exposed(hi + CampWeatherMath.SafeMargin + 0.5f, lo, hi) && CampWeatherMath.Exposed(lo - CampWeatherMath.SafeMargin - 0.5f, lo, hi),
            "the weather hurts only past the game's own safe range, ten degrees either side of comfortable");
        bool stays = true;
        for (float a = -40f; a <= 50f; a += 1f)
            for (float t = a; t <= a + 20f; t += 0.5f)
                if (CampWeatherMath.WouldStop(a, a + 20f, lo, hi) && CampWeatherMath.Exposed(t, lo, hi)) stays = false;
        C(stays, "any weather within the stay they came for, they can stay in");
        C(!CampWeatherMath.PackUp(0, 0) && !CampWeatherMath.PackUp(1, 3) && !CampWeatherMath.PackUp(2, 4), "one or half of them hurting doesn't send them all off");
        C(CampWeatherMath.PackUp(2, 3) && CampWeatherMath.PackUp(3, 4) && CampWeatherMath.PackUp(1, 1), "most of them hurting, they go");

        // ---------------- Pitching for it ----------------

        C(CampWeatherMath.PitchForCold(-3f) && CampWeatherMath.PitchForCold(1f) && !CampWeatherMath.PitchForCold(8f), "frosty nights: beds drawn in round the fire");
        C(CampWeatherMath.PitchForHeat(34f) && !CampWeatherMath.PitchForHeat(27f), "hot days: a spot in the shade");

        return n;
    }
}
