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
        Unmet,
        /// <summary>Something done, or made to do, against their nature (Diplomacy/Nature.cs, NatureMath): one act a day.</summary>
        Nature,
        /// <summary>
        /// Small letdowns (Moods/SmallMoments.cs): what Rimfolk's small letdowns weigh beyond their moment, and every small
        /// bad mood on their mind once they pile up, the game's and other mods' too (SmallMomentMath).
        /// </summary>
        Letdowns,
        /// <summary>
        /// Heavy things (Heart/HeartFelt.cs, HeartFeltMath): strain sinking toward the lasting weight of a loss, a downing, a
        /// fight going badly or a sickness in the house; bounded by the weight, not by a daily cap.
        /// </summary>
        Heavy
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
        Council,
        /// <summary>Something done with their nature that eases them (Diplomacy/Nature.cs: the open sent abroad for a season).</summary>
        Nature,
        /// <summary>Small joys (Moods/SmallMoments.cs): Rimfolk's beyond their moment, and every small good mood once they add up.</summary>
        Joys
    }

    /// <summary>
    /// The heart's good side (the player's request of 6 November 2026): steady, in good heart, in high spirits, each with
    /// its own good mood (the bands as the player approved them, 6 November 2026). Below steady there is nothing to speak
    /// of yet: the middle of the one heart, where strain's own steady band is. Never saved (always read from the value).
    /// </summary>
    public enum GoodBand
    {
        None,
        Steady,
        GoodHeart,
        HighSpirits
    }

    /// <summary>What fills the heart's good side. Saved by position in lists, so new kinds go at the end.</summary>
    public enum GoodSource
    {
        /// <summary>Parties, weddings, feasts, festivals and the colony's customs (what a gathering eases beyond the strain they have).</summary>
        Gathering,
        /// <summary>Deep talks, kind words and good moments with friends (the same, for good talks).</summary>
        Friends,
        /// <summary>A run of Rimfolk's small joys (StrainRelief.Joys beyond the strain they have).</summary>
        Joys,
        /// <summary>Friends close by, a partner, family, a group: the heart's need of belonging well met.</summary>
        Belonging,
        /// <summary>Work they love, a life goal moving, a seat on the council: the heart's need of purpose well met.</summary>
        Purpose,
        /// <summary>A deed of theirs the colony speaks well of (Social/Gossip/Deeds.cs).</summary>
        Deeds,
        /// <summary>The game's comforts, a fine room and a fine meal among them (every small good mood that adds up): only a little.</summary>
        Comforts
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
                // One act a day, up to NatureMath.MaxStrain.
                case StrainSource.Nature: return 12f;
                // Small letdowns, Rimfolk's and everyone's small bad moods together.
                case StrainSource.Letdowns: return 1.5f;
                // Heavy things sink in toward their weight (HeartFeltMath.SinkIn), never through this cap; from outside the hour, a little.
                case StrainSource.Heavy: return 3f;
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

        // ---------------- The good side ----------------
        //
        // One heart with two sides: strain on one, the good side on the other (0 to 100 each), both at once (the player's
        // change of 9 November 2026: "the good side should also be able to develop at the same time, not only when strain is
        // at 0"). Hardship spends part of the good side as a cushion and builds strain with the rest; good things ease strain
        // and fill the good side together, a share each (HeartFeltMath.Settle). It fills slowly, at any strain but slower
        // the more there is (HeartFeltMath.Growth), mostly from what can't be bought (friends and belonging, purpose, deeds
        // the colony honours, gatherings, a run of small joys), the game's comforts only a little, and fades slowly.

        public static readonly int GoodSourceCount = Enum.GetValues(typeof(GoodSource)).Length;

        /// <summary>
        /// From here a colonist is steady, from <see cref="GoodHeartAt"/> in good heart and from <see cref="HighSpiritsAt"/>
        /// in high spirits; below <see cref="SteadyAt"/>, nothing to speak of yet.
        /// </summary>
        public const float SteadyAt = 20f, GoodHeartAt = 45f, HighSpiritsAt = 65f;

        public static GoodBand GoodBandFor(float good)
        {
            if (good >= HighSpiritsAt) return GoodBand.HighSpirits;
            if (good >= GoodHeartAt) return GoodBand.GoodHeart;
            if (good >= SteadyAt) return GoodBand.Steady;
            return GoodBand.None;
        }

        /// <summary>The lowest value of a band of the good side (for meters and tooltips).</summary>
        public static float GoodBandStart(GoodBand band)
        {
            switch (band)
            {
                case GoodBand.Steady: return SteadyAt;
                case GoodBand.GoodHeart: return GoodHeartAt;
                case GoodBand.HighSpirits: return HighSpiritsAt;
                default: return 0f;
            }
        }

        /// <summary>
        /// The situational mood thought's size at each band of the good side (Rimfolk_Thoughts_GoodHeart.xml has the same,
        /// one stage a band from steady): +2, +3 and +5, the bands as the player approved them; nothing yet gives nothing.
        /// A mirror of strain's -3, -5 and -8, each smaller than strain's across from it.
        /// </summary>
        public static float GoodMood(GoodBand band)
        {
            switch (band)
            {
                case GoodBand.Steady: return 2f;
                case GoodBand.GoodHeart: return 3f;
                case GoodBand.HighSpirits: return 5f;
                default: return 0f;
            }
        }

        /// <summary>The most the good side can fill in one day, from everything together.</summary>
        public const float GoodDailyCap = 4f;

        /// <summary>The most one source can fill in a day.</summary>
        public static float GoodCap(GoodSource s)
        {
            switch (s)
            {
                case GoodSource.Gathering: return 2.5f;
                case GoodSource.Friends: return 1.5f;
                case GoodSource.Joys: return 1.2f;
                case GoodSource.Belonging: return 1.2f;
                case GoodSource.Purpose: return 1.2f;
                case GoodSource.Deeds: return 2f;
                // A fine room and a fine meal: only a little, so a rich colony doesn't simply buy it.
                case GoodSource.Comforts: return 0.4f;
                default: return 1f;
            }
        }

        /// <summary>
        /// How much of what a gathering, a talk or a joy eases beyond the strain they have fills the good side: what
        /// clears strain in a day fills the good side over many.
        /// </summary>
        public static float GoodShare(GoodSource s)
        {
            switch (s)
            {
                case GoodSource.Gathering: return 0.45f;
                case GoodSource.Friends: return 0.6f;
                case GoodSource.Joys: return 0.6f;
                case GoodSource.Comforts: return 0.15f;
                default: return 1f;
            }
        }

        /// <summary>
        /// The good side an hour from belonging or purpose well met (<paramref name="need"/>, 0 to 100): nothing below 60
        /// (HeartNeedMath.WellMet), 0.035 at 60 rising to 0.07 at 90 and more (within the source's daily cap).
        /// </summary>
        public static float NeedFillPerHour(float need)
        {
            if (float.IsNaN(need) || need < 60f) return 0f;
            return Lerp(0.035f, 0.07f, (need - 60f) / 30f);
        }

        /// <summary>A deed the colony speaks well of: this much for a deed of size 1, up to twice for a big one.</summary>
        public const float DeedFill = 1f;

        public static float DeedFillFor(float size) => float.IsNaN(size) || size <= 0f ? 0f : DeedFill * Math.Min(2f, size);

        /// <summary>A life goal that moved since yesterday: this much at the day's turn.</summary>
        public const float GoalMovedFill = 0.6f;

        /// <summary>
        /// Fills the good side from one source within its daily cap and the day's cap for everything. Returns what was
        /// actually added (never past <see cref="Max"/>).
        /// </summary>
        public static float Fill(float amount, ref float good, ref float sourceToday, float sourceCap, ref float totalToday, float totalCap)
        {
            if (float.IsNaN(amount) || amount <= 0f) return 0f;
            float room = Math.Min(Math.Max(0f, sourceCap - sourceToday), Math.Max(0f, totalCap - totalToday));
            float a = Math.Min(Math.Min(amount, room), Math.Max(0f, Max - good));
            if (a <= 0f) return 0f;
            sourceToday += a;
            totalToday += a;
            good += a;
            return a;
        }

        /// <summary>
        /// Someone in good heart takes a hard knock better: while there is any good side, this share of each point of
        /// hardship is cushioned by it, and the rest builds strain.
        /// </summary>
        public const float CushionShare = 0.35f;

        /// <summary>Each point of hardship the good side cushions spends this much of it.</summary>
        public const float SpendShare = 1f;

        /// <summary>
        /// Hardship spends part of the good side as a cushion: of <paramref name="hardship"/>, its cushioned share (as far
        /// as the good side goes, at <see cref="SpendShare"/> a point) comes off <paramref name="good"/>; the rest is returned,
        /// to build strain. <paramref name="spent"/> is what came off the good side.
        /// </summary>
        public static float Spend(float hardship, ref float good, out float spent)
        {
            spent = 0f;
            if (float.IsNaN(hardship) || hardship <= 0f) return 0f;
            if (good <= 0f || float.IsNaN(good)) return hardship;
            float cushion = hardship * CushionShare;
            float cost = cushion * SpendShare;
            if (cost > good)
            {
                cost = good;
                cushion = cost / SpendShare;
            }
            spent = cost;
            good -= cost;
            return hardship - cushion;
        }

        /// <summary>The good side fades slowly: about 3% a day (halving in about three weeks with nothing to fill it).</summary>
        public const float GoodFadePerHour = 0.99873f;

        /// <summary>The good side after <paramref name="hours"/> of fading.</summary>
        public static float Faded(float good, float hours) => good <= 0f || float.IsNaN(good) ? 0f : good * (float)Math.Pow(GoodFadePerHour, Math.Max(0f, hours));

        /// <summary>How high the good side has stood lately, halving in about three days (for bouncing back after a hard spell).</summary>
        public const float LatelyPerHour = 0.9904f;

        public static float Lately(float lately, float good, float hours)
        {
            float faded = lately <= 0f || float.IsNaN(lately) ? 0f : lately * (float)Math.Pow(LatelyPerHour, Math.Max(0f, hours));
            return Math.Max(faded, Math.Max(0f, good));
        }

        /// <summary>
        /// A quicker recovery from strain for someone in good heart lately (the good side spent by a hard spell, it stands
        /// as it stood): strain drains this much faster. Steady gives only its mood: the dreams and the recovery start in
        /// good heart.
        /// </summary>
        public static float RecoveryFactor(GoodBand band) => band == GoodBand.HighSpirits ? 1.3f : band == GoodBand.GoodHeart ? 1.15f : 1f;

        /// <summary>Brighter dreams: how much likelier a sweet dream is at each band of the good side.</summary>
        public static float SweetDreamFactor(GoodBand band) => band == GoodBand.HighSpirits ? 1.4f : band == GoodBand.GoodHeart ? 1.2f : 1f;

        /// <summary>And how much rarer a nightmare.</summary>
        public static float GoodNightmareFactor(GoodBand band) => band == GoodBand.HighSpirits ? 0.7f : band == GoodBand.GoodHeart ? 0.85f : 1f;

        // ---------------- The good side's release ----------------

        /// <summary>
        /// In high spirits and safe, now and then something good comes of it (one of the game's inspirations by their
        /// passions, or a kind word for someone close): the chance an hour, about once in four days held there.
        /// </summary>
        public const float ReleasePerHour = 1f / (24f * 4f);

        /// <summary>Once in so many days for each colonist, and once a day in the colony.</summary>
        public const int ReleasePawnDays = 10, ReleaseColonyDays = 1;

        /// <summary>What the release spends of the good side: an inspiration more, a kind word less.</summary>
        public const float InspirationSpends = 12f, KindWordSpends = 6f;

        /// <summary>The chance of a release this hour: none below high spirits, the hourly chance from there.</summary>
        public static float ReleaseChancePerHour(float good) => good >= HighSpiritsAt ? ReleasePerHour : 0f;

        /// <summary>A feeling's intensity as a share of full (0 to 1), for scaling its nudges.</summary>
        public static float Share(float intensity) => Math.Max(0f, Math.Min(1f, intensity / FullIntensity));

        /// <summary>A nudge at a share of its full size: 1 at nothing, <paramref name="full"/> at full intensity.</summary>
        public static float Nudge(float full, float share) => Lerp(1f, full, share);

        public static float Lerp(float a, float b, float t) => a + (b - a) * Math.Max(0f, Math.Min(1f, t));
    }
}
