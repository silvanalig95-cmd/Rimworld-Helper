using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The weather a camp comes in and stays through (CampTracker, CampDress, CampLife.BadWeather). Travellers come in
    /// any weather but the really extreme: they dress for the days and nights of their stay and pitch their camp for
    /// it. The game hurts people only outside their safe range, their comfortable range and ten degrees either side
    /// (heatstroke above it, hypothermia below), so a camp comes only when, dressed as they are, none of them would be
    /// hurt by what the stay is likely to bring, and packs up early only when the weather starts to hurt most of them.
    /// Pure (no game types), so Tools/Tests can check it.
    /// </summary>
    public static class CampWeatherMath
    {
        /// <summary>The game's own: how far past comfortable a person can be before the weather starts to hurt them.</summary>
        public const float SafeMargin = 10f;

        /// <summary>Weather this cold or hot is really extreme: no camp comes in it.</summary>
        public const float ExtremeCold = -30f, ExtremeHeat = 45f;

        /// <summary>A camp already standing waits out this much worse before it packs up for the cold or heat alone.</summary>
        public const float StayPast = 5f;

        /// <summary>A few degrees the land's own reckoning of its hours doesn't show (wind, rain, a bad day), either way.</summary>
        public const float Margin = 3f;

        /// <summary>Nights this cold or colder: they pitch for the cold (beds drawn in round the fire, behind a windbreak).</summary>
        public const float ColdNights = 2f;

        /// <summary>Days this hot or hotter: they pitch for the heat (in the shade of trees, where there are any).</summary>
        public const float HotDays = 30f;

        public static bool TooExtremeToCome(float temp) => temp < ExtremeCold || temp > ExtremeHeat;

        public static bool TooExtremeToStay(float temp) => temp < ExtremeCold - StayPast || temp > ExtremeHeat + StayPast;

        /// <summary>The coldest the stay is likely to bring: its coldest hour on the land's own reckoning, a little colder, or now.</summary>
        public static float StayLow(float now, float coldestHour) => Math.Min(now, coldestHour - Margin);

        /// <summary>The hottest the stay is likely to bring.</summary>
        public static float StayHigh(float now, float warmestHour) => Math.Max(now, warmestHour + Margin);

        /// <summary>
        /// What a running condition adds to the hours ahead (a heat wave, a cold snap): the difference between now and
        /// the land's own reckoning of this hour, kept only on its own side, so they dress for what they see.
        /// </summary>
        public static float Lasting(float now, float reckonedNow, bool forCold) => forCold ? Math.Min(0f, now - reckonedNow) : Math.Max(0f, now - reckonedNow);

        /// <summary>How far, in degrees, someone comfortable from <paramref name="comfyMin"/> to <paramref name="comfyMax"/> falls short of the stay's weather.</summary>
        public static float Shortfall(float comfyMin, float comfyMax, float low, float high) => Math.Max(0f, comfyMin - low) + Math.Max(0f, high - comfyMax);

        /// <summary>The weather would start to hurt them, dressed as they are.</summary>
        public static bool Exposed(float temp, float comfyMin, float comfyMax) => temp < comfyMin - SafeMargin || temp > comfyMax + SafeMargin;

        /// <summary>One of them would stop here: nothing the stay is likely to bring would hurt them, dressed as they are.</summary>
        public static bool WouldStop(float low, float high, float comfyMin, float comfyMax) => !Exposed(low, comfyMin, comfyMax) && !Exposed(high, comfyMin, comfyMax);

        /// <summary>Most of those at the camp are exposed: they pack up (one of them dressed for another season doesn't send them all off).</summary>
        public static bool PackUp(int exposed, int here) => here > 0 && exposed * 2 > here;

        public static bool PitchForCold(float low) => low < ColdNights;

        public static bool PitchForHeat(float high) => high > HotDays;
    }
}
