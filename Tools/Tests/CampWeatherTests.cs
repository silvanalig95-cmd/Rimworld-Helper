using System;
using ColonyCouncil;

/// <summary>
/// The weather camps come in and stay through (Camps/CampWeatherMath): travellers stop on a warm or cool morning
/// dressed as they are, stay through the afternoon's heat, and pack up only when the weather would hurt most of them.
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

        // ---------------- Stopping to camp ----------------

        C(CampWeatherMath.WouldStop(22f, lo, hi), "a mild morning suits them");
        C(CampWeatherMath.WouldStop(30f, lo, hi), "a warm summer morning (30 °C) doesn't keep them away, a little past comfortable");
        C(CampWeatherMath.WouldStop(hi + CampWeatherMath.ArriveMargin, lo, hi) && !CampWeatherMath.WouldStop(hi + CampWeatherMath.ArriveMargin + 0.5f, lo, hi),
            "in heat they stop up to six degrees past comfortable, and no further");
        C(CampWeatherMath.WouldStop(lo - CampWeatherMath.ArriveMargin, lo, hi) && !CampWeatherMath.WouldStop(lo - CampWeatherMath.ArriveMargin - 0.5f, lo, hi),
            "in cold they stop down to six degrees past comfortable, and no further");
        C(CampWeatherMath.WouldStop(0f, -20f, 24f) && !CampWeatherMath.WouldStop(0f, lo, hi), "a parka takes them through a cold morning that sends a shirt on its way");

        // ---------------- Staying ----------------

        C(!CampWeatherMath.Exposed(37f, lo, hi), "the afternoon's heat after a warm morning (37 °C) doesn't hurt them yet");
        C(CampWeatherMath.Exposed(hi + CampWeatherMath.SafeMargin + 0.5f, lo, hi) && CampWeatherMath.Exposed(lo - CampWeatherMath.SafeMargin - 0.5f, lo, hi),
            "past the game's own safe range, ten degrees either side of comfortable, the weather hurts");
        C(!CampWeatherMath.Exposed(hi + CampWeatherMath.SafeMargin, lo, hi) && !CampWeatherMath.Exposed(lo - CampWeatherMath.SafeMargin, lo, hi), "at the edge of it, not yet");
        bool stricter = true;
        for (float t = -40f; t <= 60f; t += 0.5f)
            if (CampWeatherMath.WouldStop(t, lo, hi) && CampWeatherMath.Exposed(t, lo, hi)) stricter = false;
        C(stricter, "any weather they'd stop in, they can stay in: a camp never comes only to leave at once");

        // ---------------- Packing up ----------------

        C(!CampWeatherMath.PackUp(0, 0), "an empty camp has nobody to send off");
        C(!CampWeatherMath.PackUp(1, 3), "one of three dressed for another season doesn't send them all off");
        C(!CampWeatherMath.PackUp(2, 4), "half of them is not most of them");
        C(CampWeatherMath.PackUp(2, 3) && CampWeatherMath.PackUp(3, 4) && CampWeatherMath.PackUp(1, 1), "most of them hurting, they go");

        return n;
    }
}
