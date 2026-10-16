using System;

namespace ColonyCouncil
{
    /// <summary>How worn down a colonist is, from their strain (0 to 100).</summary>
    public enum StrainBand
    {
        Steady,
        Strained,
        Worn,
        Breaking
    }

    /// <summary>What builds strain. Saved by position in lists, so new sources go at the end.</summary>
    public enum StrainSource
    {
        Combat,
        Pain,
        Downed,
        Loss,
        Exhaustion,
        Joyless,
        BadSleep,
        Dark,
        Hunger,
        LowMood,
        Council,
        Fear,
        Grief,
        Dread,
        Despair,
        /// <summary>A need of the heart badly lacking for days (HeartNeedMath).</summary>
        Unmet
    }

    /// <summary>What drains strain. Saved by position in lists, so new kinds go at the end.</summary>
    public enum StrainRelief
    {
        Quiet,
        Sleep,
        Recreation,
        Gathering,
        Friends,
        Catharsis,
        Episode,
        Council
    }

    /// <summary>The traits that change how strain builds and drains, as flags so the math needs no game types.</summary>
    [Flags]
    public enum StrainTraits
    {
        None = 0,
        IronWilled = 1,
        Steadfast = 2,
        Nervous = 4,
        Volatile = 8,
        Psychopath = 16,
        Bloodlust = 32,
        Wimp = 64,
        Tough = 128,
        Masochist = 256,
        Undergrounder = 512,
        Ascetic = 1024,
        Brawler = 2048,
        Sanguine = 4096,
        Optimist = 8192,
        Pessimist = 16384,
        Depressive = 32768
    }

    /// <summary>
    /// The numbers behind strain, kept free of game types so the tests can check them: the bands, how much each source
    /// may add in a day, how traits scale it, and what it does at each band. Nothing here touches vanilla mood.
    /// </summary>
    public static class HeartMath
    {
        public const float Max = 100f;

        /// <summary>The most strain that can build in one day, from everything together (at 100% strength).</summary>
        public const float DailyCap = 25f;

        /// <summary>From this strain on, a colonist may need a day off (an episode).</summary>
        public const float EpisodeAt = 85f;

        /// <summary>What an episode takes off when it runs its course.</summary>
        public const float EpisodeRelief = 40f;

        /// <summary>A feeling's intensity counts in full from here (its nudges reach their limit).</summary>
        public const float FullIntensity = 20f;

        /// <summary>Recent totals halve in about two days (per hour: 0.5^(1/48)).</summary>
        public const float RecentDecayPerHour = 0.98566f;

        public static readonly int SourceCount = Enum.GetValues(typeof(StrainSource)).Length;
        public static readonly int ReliefCount = Enum.GetValues(typeof(StrainRelief)).Length;

        public static StrainBand BandFor(float strain)
        {
            if (strain >= 75f) return StrainBand.Breaking;
            if (strain >= 55f) return StrainBand.Worn;
            if (strain >= 30f) return StrainBand.Strained;
            return StrainBand.Steady;
        }

        /// <summary>The lowest strain of a band (for meters and tooltips).</summary>
        public static float BandStart(StrainBand band)
        {
            switch (band)
            {
                case StrainBand.Strained: return 30f;
                case StrainBand.Worn: return 55f;
                case StrainBand.Breaking: return 75f;
                default: return 0f;
            }
        }

        /// <summary>The situational mood thought's size at each band.</summary>
        public static float MoodOffset(StrainBand band)
        {
            switch (band)
            {
                case StrainBand.Strained: return -3f;
                case StrainBand.Worn: return -5f;
                case StrainBand.Breaking: return -8f;
                default: return 0f;
            }
        }

        /// <summary>Global work speed at each band.</summary>
        public static float WorkFactor(StrainBand band)
        {
            switch (band)
            {
                case StrainBand.Worn: return 0.95f;
                case StrainBand.Breaking: return 0.9f;
                default: return 1f;
            }
        }

        /// <summary>How much more often dreams turn to nightmares at each band.</summary>
        public static float NightmareFactor(StrainBand band)
        {
            switch (band)
            {
                case StrainBand.Strained: return 1.2f;
                case StrainBand.Worn: return 1.6f;
                case StrainBand.Breaking: return 2.2f;
                default: return 1f;
            }
        }

        /// <summary>The most one source can add in a day (at 100% strength).</summary>
        public static float SourceCap(StrainSource s)
        {
            switch (s)
            {
                case StrainSource.Combat: return 12f;
                case StrainSource.Pain: return 6f;
                case StrainSource.Downed: return 8f;
                case StrainSource.Loss: return 14f;
                case StrainSource.Exhaustion: return 4f;
                case StrainSource.Joyless: return 3f;
                case StrainSource.BadSleep: return 4f;
                case StrainSource.Dark: return 2f;
                case StrainSource.Hunger: return 4f;
                case StrainSource.LowMood: return 6f;
                case StrainSource.Council: return 4f;
                case StrainSource.Fear: return 3f;
                case StrainSource.Grief: return 3f;
                case StrainSource.Dread: return 2f;
                case StrainSource.Despair: return 2f;
                case StrainSource.Unmet: return 3f;
                default: return 3f;
            }
        }

        /// <summary>
        /// How much of a source's strain this person takes. Iron will and a steady nature shrug off part of everything;
        /// nerves and a gloomy nature take more. Psychopaths take nothing from deaths, the bloodthirsty little from
        /// fighting, masochists nothing from pain, undergrounders nothing from the dark.
        /// </summary>
        public static float TraitFactor(StrainSource s, StrainTraits t)
        {
            float f = 1f;
            if ((t & StrainTraits.IronWilled) != 0) f *= 0.6f;
            else if ((t & StrainTraits.Steadfast) != 0) f *= 0.8f;
            else if ((t & StrainTraits.Volatile) != 0) f *= 1.4f;
            else if ((t & StrainTraits.Nervous) != 0) f *= 1.25f;
            if ((t & StrainTraits.Sanguine) != 0) f *= 0.85f;
            else if ((t & StrainTraits.Optimist) != 0) f *= 0.92f;
            else if ((t & StrainTraits.Depressive) != 0) f *= 1.15f;
            else if ((t & StrainTraits.Pessimist) != 0) f *= 1.08f;
            switch (s)
            {
                case StrainSource.Loss:
                case StrainSource.Grief:
                    if ((t & StrainTraits.Psychopath) != 0) return 0f;
                    break;
                case StrainSource.Combat:
                    if ((t & StrainTraits.Bloodlust) != 0) f *= 0.3f;
                    else if ((t & StrainTraits.Brawler) != 0) f *= 0.7f;
                    if ((t & StrainTraits.Wimp) != 0) f *= 1.3f;
                    break;
                case StrainSource.Pain:
                    if ((t & StrainTraits.Masochist) != 0) return 0f;
                    if ((t & StrainTraits.Wimp) != 0) f *= 1.5f;
                    else if ((t & StrainTraits.Tough) != 0) f *= 0.7f;
                    break;
                case StrainSource.Downed:
                    if ((t & StrainTraits.Wimp) != 0) f *= 1.3f;
                    else if ((t & StrainTraits.Tough) != 0) f *= 0.8f;
                    break;
                case StrainSource.Dark:
                    if ((t & StrainTraits.Undergrounder) != 0) return 0f;
                    break;
                case StrainSource.BadSleep:
                    if ((t & StrainTraits.Ascetic) != 0) f *= 0.5f;
                    break;
                case StrainSource.Fear:
                    if ((t & StrainTraits.Bloodlust) != 0) f *= 0.5f;
                    break;
            }
            return f;
        }

        /// <summary>How fast strain drains for this nature: a sunny one lets go sooner, a gloomy one later.</summary>
        public static float ReliefTraitFactor(StrainTraits t)
        {
            if ((t & StrainTraits.Sanguine) != 0) return 1.15f;
            if ((t & StrainTraits.Optimist) != 0) return 1.07f;
            if ((t & StrainTraits.Depressive) != 0) return 0.85f;
            if ((t & StrainTraits.Pessimist) != 0) return 0.93f;
            return 1f;
        }

        /// <summary>
        /// Adds strain from one source within its daily cap and the daily cap for everything, both scaled by the strength
        /// setting. Returns what was actually added.
        /// </summary>
        public static float Add(float raw, float speed, float traitFactor, ref float sourceToday, float sourceCap, ref float totalToday, float totalCap)
        {
            if (raw <= 0f || speed <= 0f || traitFactor <= 0f) return 0f;
            float amount = raw * speed * traitFactor;
            float roomSource = Math.Max(0f, sourceCap * speed - sourceToday);
            float roomTotal = Math.Max(0f, totalCap * speed - totalToday);
            amount = Math.Min(amount, Math.Min(roomSource, roomTotal));
            if (amount <= 0f) return 0f;
            sourceToday += amount;
            totalToday += amount;
            return amount;
        }

        /// <summary>Strain after an hour's change, kept within 0 to 100.</summary>
        public static float Clamp(float strain) => Math.Max(0f, Math.Min(Max, strain));

        /// <summary>
        /// The chance of an episode this hour: none below 85, then about one in 36 hours at 85, rising to one in 24 at 100
        /// (so roughly one within a day or two at that level).
        /// </summary>
        public static float EpisodeChancePerHour(float strain)
        {
            if (strain < EpisodeAt) return 0f;
            float t = (Math.Min(strain, Max) - EpisodeAt) / (Max - EpisodeAt);
            return Lerp(1f / 36f, 1f / 24f, t);
        }

        /// <summary>A feeling's intensity as a share of full (0 to 1), for scaling its nudges.</summary>
        public static float Share(float intensity) => Math.Max(0f, Math.Min(1f, intensity / FullIntensity));

        /// <summary>A nudge at a share of its full size: 1 at nothing, <paramref name="full"/> at full intensity.</summary>
        public static float Nudge(float full, float share) => Lerp(1f, full, share);

        public static float Lerp(float a, float b, float t) => a + (b - a) * Math.Max(0f, Math.Min(1f, t));
    }
}
