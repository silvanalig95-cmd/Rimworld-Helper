namespace ColonyCouncil
{
    /// <summary>
    /// The weather a camp comes in and stays through (CampTracker.Prepare, CampLife.BadWeather). The game hurts people
    /// only outside their safe range, their comfortable range and ten degrees either side: heatstroke above it,
    /// hypothermia below. Travellers stop to camp while each of them is within six degrees of comfortable, and pack up
    /// when most of them are past their safe range, so a camp that comes on a warm morning stays through the afternoon.
    /// Pure (no game types), so Tools/Tests can check it.
    /// </summary>
    public static class CampWeatherMath
    {
        /// <summary>The game's own: how far past comfortable a person can be before the weather starts to hurt them.</summary>
        public const float SafeMargin = 10f;

        /// <summary>How far past comfortable each traveller may be and still stop to camp.</summary>
        public const float ArriveMargin = 6f;

        /// <summary>One of them would stop here in this weather, dressed as they are.</summary>
        public static bool WouldStop(float temp, float comfyMin, float comfyMax) => temp >= comfyMin - ArriveMargin && temp <= comfyMax + ArriveMargin;

        /// <summary>The weather would start to hurt them, dressed as they are.</summary>
        public static bool Exposed(float temp, float comfyMin, float comfyMax) => temp < comfyMin - SafeMargin || temp > comfyMax + SafeMargin;

        /// <summary>Most of those at the camp are exposed: they pack up (one of them dressed for another season doesn't send them all off).</summary>
        public static bool PackUp(int exposed, int here) => here > 0 && exposed * 2 > here;
    }
}
