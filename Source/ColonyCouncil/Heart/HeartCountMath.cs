using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>How strongly the heart's bands are felt in mood (the setting "How strongly the bands are felt").</summary>
    public enum BandStrength
    {
        /// <summary>As release 1 had them: strain -3, -5, -8; the good side +2, +3, +5.</summary>
        Mild,
        /// <summary>The default since the heart was made to count: strain -4, -8, -14; the good side +3, +6, +10.</summary>
        Felt,
        /// <summary>For a harder game: strain -5, -10, -18; the good side +4, +8, +13.</summary>
        Strong
    }

    /// <summary>What the heart does beyond mood (stat parts in Patches/Council_Heart.xml).</summary>
    public enum HeartEffect
    {
        Work,
        Learning,
        Rest,
        Healing,
        Immunity
    }

    /// <summary>Time off work under way (Heart/HeartTimeOff.cs). Saved by its number, so new kinds go at the end.</summary>
    public enum TimeOffKind
    {
        None,
        /// <summary>A day off the player gave them: a day of rest, recreation, walks and friends.</summary>
        DayOff,
        /// <summary>A bad afternoon: worn down, they leave their work for a few hours.</summary>
        BadDay
    }

    /// <summary>One piece of advice for a strained colonist (Heart/HeartAdvice.cs), ranked by what is wearing on them now.</summary>
    public enum AdviceKind
    {
        DayOff,
        Decree,
        Gathering,
        Friend,
        Meal,
        Bed,
        Recreation,
        Pain,
        Peace,
        Grieve,
        Light
    }

    /// <summary>
    /// The heart, made to count (docs/design/heart-felt.md, release 2; the player's words of 9 November 2026: "if the strain
    /// is not noticeable it doesn't make too much sense. also include recommendations and advice on potential actions i can
    /// take to reduce strain"), kept free of game types so the tests and the balance simulator run it as it is:
    /// <list type="bullet">
    /// <item>Stronger bands, by the setting's choice (<see cref="StrainMood"/>, <see cref="GoodMood"/>).</item>
    /// <item>Mild effects beyond mood, through stat parts: work from the first band, learning, sleep, healing and immunity
    /// (<see cref="Factor"/>), never more than a tenth either way, immunity never more than 5% slower.</item>
    /// <item>A bad afternoon from worn (<see cref="BadDayChancePerHour"/>) and a day off the player gives
    /// (<see cref="DayOffCooldownDays"/>), each easing a good share of strain.</item>
    /// <item>Hearts spread a little (<see cref="Spread"/>, <see cref="Lift"/>): never past <see cref="SpreadCeiling"/>, so one
    /// worn colonist can't wear the colony down alone.</item>
    /// <item>Advice ranked from what is wearing on them now (<see cref="Rank"/>).</item>
    /// </list>
    /// </summary>
    public static class HeartCountMath
    {
        // ---------------- Stronger bands ----------------

        /// <summary>Strain's mood at each band (strained, worn, near breaking) for the strength chosen.</summary>
        public static float StrainMood(StrainBand band, BandStrength strength)
        {
            int i = band == StrainBand.Strained ? 0 : band == StrainBand.Worn ? 1 : band == StrainBand.Breaking ? 2 : -1;
            if (i < 0) return 0f;
            switch (strength)
            {
                case BandStrength.Mild: return new[] { -3f, -5f, -8f }[i];
                case BandStrength.Strong: return new[] { -5f, -10f, -18f }[i];
                default: return new[] { -4f, -8f, -14f }[i];
            }
        }

        /// <summary>The good side's mood at each band (steady, in good heart, in high spirits) for the strength chosen.</summary>
        public static float GoodMood(GoodBand band, BandStrength strength)
        {
            int i = band == GoodBand.Steady ? 0 : band == GoodBand.GoodHeart ? 1 : band == GoodBand.HighSpirits ? 2 : -1;
            if (i < 0) return 0f;
            switch (strength)
            {
                case BandStrength.Mild: return new[] { 2f, 3f, 5f }[i];
                case BandStrength.Strong: return new[] { 4f, 8f, 13f }[i];
                default: return new[] { 3f, 6f, 10f }[i];
            }
        }

        // ---------------- Effects beyond mood ----------------

        /// <summary>
        /// Global work speed at each band: strained 3% slower, worn 7%, near breaking 12% (with the effects beyond mood); the
        /// old -5% and -10% from worn without them. The good side never touches work.
        /// </summary>
        public static float Work(StrainBand band, bool beyond)
        {
            if (!beyond) return band == StrainBand.Worn ? 0.95f : band == StrainBand.Breaking ? 0.9f : 1f;
            switch (band)
            {
                case StrainBand.Strained: return 0.97f;
                case StrainBand.Worn: return 0.93f;
                case StrainBand.Breaking: return 0.88f;
                default: return 1f;
            }
        }

        /// <summary>The slowest the heart ever makes healing, and immunity (the player lost colonists to the plague).</summary>
        public const float HealingSlowest = 0.9f, ImmunitySlowest = 0.95f;

        /// <summary>The fastest the good side ever makes learning, healing or immunity.</summary>
        public const float GoodFastest = 1.08f;

        /// <summary>Strain's side of an effect at its band (1: nothing).</summary>
        public static float StrainSide(HeartEffect e, StrainBand band)
        {
            bool worn = band == StrainBand.Worn, breaking = band == StrainBand.Breaking;
            switch (e)
            {
                case HeartEffect.Work: return Work(band, true);
                // They learn slower, need a little more sleep, heal and fight off illness a little slower.
                case HeartEffect.Learning: return worn ? 0.94f : breaking ? 0.9f : 1f;
                case HeartEffect.Rest: return worn ? 1.06f : breaking ? 1.1f : 1f;
                case HeartEffect.Healing: return worn ? 0.95f : breaking ? HealingSlowest : 1f;
                case HeartEffect.Immunity: return worn ? 0.97f : breaking ? ImmunitySlowest : 1f;
                default: return 1f;
            }
        }

        /// <summary>The good side's side of an effect: in good heart and in high spirits a little faster learning and recovery.</summary>
        public static float GoodSide(HeartEffect e, GoodBand band)
        {
            bool good = band == GoodBand.GoodHeart, high = band == GoodBand.HighSpirits;
            switch (e)
            {
                case HeartEffect.Learning: return good ? 1.04f : high ? GoodFastest : 1f;
                case HeartEffect.Healing: return good ? 1.03f : high ? 1.06f : 1f;
                case HeartEffect.Immunity: return good ? 1.02f : high ? 1.05f : 1f;
                default: return 1f;
            }
        }

        /// <summary>
        /// The whole effect on a stat: strain's side and the good side's together, kept within bounds (immunity never more than
        /// 5% slower, healing never more than 10%). Without the effects beyond mood, only work, as release 1 had it.
        /// </summary>
        public static float Factor(HeartEffect e, StrainBand band, GoodBand good, bool beyond)
        {
            if (e == HeartEffect.Work) return Work(band, beyond);
            if (!beyond) return 1f;
            float f = StrainSide(e, band) * GoodSide(e, good);
            switch (e)
            {
                case HeartEffect.Immunity: return Math.Max(ImmunitySlowest, Math.Min(GoodFastest, f));
                case HeartEffect.Healing: return Math.Max(HealingSlowest, Math.Min(GoodFastest, f));
                case HeartEffect.Learning: return Math.Max(0.9f, Math.Min(GoodFastest, f));
                case HeartEffect.Rest: return Math.Max(1f, Math.Min(1.1f, f));
                default: return f;
            }
        }

        // ---------------- A bad afternoon ----------------

        /// <summary>From worn down on, now and then they leave their work for an afternoon.</summary>
        public const float BadDayFrom = 55f;

        /// <summary>
        /// The chance an hour of a bad afternoon, in the hours it can begin (<see cref="BadDayStartHour"/> to
        /// <see cref="BadDayLastHour"/>): none below worn, 1 in 50 at worn rising to 1 in 25 from 85. Rare enough to read as
        /// an event: the cooldowns keep it to once a week for each colonist at most.
        /// </summary>
        public static float BadDayChancePerHour(float strain)
        {
            if (float.IsNaN(strain) || strain < BadDayFrom) return 0f;
            return HeartMath.Lerp(0.02f, 0.04f, (strain - BadDayFrom) / (HeartMath.EpisodeAt - BadDayFrom));
        }

        /// <summary>The hours of the day it can begin: the afternoon, after the midday meal.</summary>
        public const int BadDayStartHour = 12, BadDayLastHour = 16;

        /// <summary>How long it lasts: a few hours.</summary>
        public const int BadDayMinHours = 3, BadDayMaxHours = 5;

        /// <summary>Once in so many days for each colonist, and once in so many in the colony.</summary>
        public const float BadDayPawnDays = 7f, BadDayColonyDays = 2f;

        /// <summary>What a bad afternoon that ran its course eases: this share of their strain (half of it when cut short).</summary>
        public const float BadDayShare = 0.3f;

        /// <summary>The strain a bad afternoon eases.</summary>
        public static float BadDayRelief(float strain, bool full) => Clean(strain) * BadDayShare * (full ? 1f : 0.5f);

        // ---------------- A day off ----------------

        /// <summary>The player may give a day off from strained on.</summary>
        public const float DayOffFrom = 30f;

        /// <summary>It lasts a day; their first waking hours after it starts are slept in.</summary>
        public const int DayOffHours = 24, LieInHours = 2;

        /// <summary>At most once in so many days for each colonist.</summary>
        public const float DayOffCooldownDays = 5f;

        /// <summary>
        /// While it runs, rest eases this much faster, and time to themselves eases this much an hour besides; when it runs
        /// its course, this much more (half of it when cut short).
        /// </summary>
        public const float DayOffRestFactor = 1.25f, DayOffPerHour = 0.3f, DayOffRelief = 12f;

        /// <summary>What a day off eases at its end.</summary>
        public static float DayOffEnd(bool full) => full ? DayOffRelief : DayOffRelief * 0.5f;

        /// <summary>Days until a day off can be given again (0: now).</summary>
        public static float DayOffReadyIn(float daysSinceLast) => Math.Max(0f, DayOffCooldownDays - Math.Max(0f, daysSinceLast));

        // ---------------- Hearts spread a little ----------------

        /// <summary>
        /// How close two colonists are for the heart, 0 to 1: a partner 1, kin by blood 0.8, a friend by opinion 0.4 at 20
        /// rising to 0.8 at 80. The colony is the family: closeness is relationships, never a house.
        /// </summary>
        public static float Closeness(bool partner, bool kin, float opinion)
        {
            float c = 0f;
            if (partner) c = 1f;
            else if (kin) c = 0.8f;
            if (!float.IsNaN(opinion) && opinion >= 20f) c = Math.Max(c, HeartMath.Lerp(0.4f, 0.8f, (opinion - 20f) / 60f));
            return c;
        }

        /// <summary>Someone close worn down wears on them this much an hour at full closeness; near breaking, more.</summary>
        public const float SpreadWorn = 0.06f, SpreadBreaking = 0.1f;

        /// <summary>From everyone close together, at most this much an hour and this much a day (at 100% strength).</summary>
        public const float SpreadHourCap = 0.15f, SpreadDailyCap = 2.5f;

        /// <summary>
        /// Spreading never takes anyone past here, short of worn down: since only the worn spread, one worn colonist can't
        /// make another worn on their own, and the colony can't spiral from one.
        /// </summary>
        public const float SpreadCeiling = 45f;

        /// <summary>What someone at this band wears on another an hour, by how close they are.</summary>
        public static float SpreadFrom(StrainBand other, float closeness)
        {
            if (closeness <= 0f || float.IsNaN(closeness)) return 0f;
            float a = other == StrainBand.Breaking ? SpreadBreaking : other == StrainBand.Worn ? SpreadWorn : 0f;
            return a * Math.Min(1f, closeness);
        }

        /// <summary>The hour's spread from everyone close (summed), kept within its hour cap and under the ceiling.</summary>
        public static float Spread(float summed, float strain, float hours)
        {
            if (summed <= 0f || float.IsNaN(summed) || hours <= 0f) return 0f;
            float a = Math.Min(summed, SpreadHourCap) * hours;
            return Math.Max(0f, Math.Min(a, SpreadCeiling - Clean(strain)));
        }

        /// <summary>Someone close in high spirits lifts them this much an hour at full closeness (a good thing, as a talk is).</summary>
        public const float LiftHigh = 0.05f, LiftHourCap = 0.1f;

        public static float LiftFrom(GoodBand other, float closeness) =>
            other == GoodBand.HighSpirits && closeness > 0f && !float.IsNaN(closeness) ? LiftHigh * Math.Min(1f, closeness) : 0f;

        /// <summary>The hour's lift from everyone close (summed), within its hour cap.</summary>
        public static float Lift(float summed, float hours) => summed <= 0f || float.IsNaN(summed) || hours <= 0f ? 0f : Math.Min(summed, LiftHourCap) * hours;

        // ---------------- Advice ----------------

        /// <summary>What the advice is ranked from: what built their strain lately, how they are now, what the colony has.</summary>
        public sealed class AdviceFacts
        {
            public float strain;
            /// <summary>Strain built lately, by source (PawnHeart.recentBuild).</summary>
            public readonly float[] recent = new float[HeartMath.SourceCount];
            /// <summary>Now: hungry, in pain, short of recreation, sleeping badly (a bad bed or none), lonely (belonging missed).</summary>
            public bool hungry, inPain, joyless, badBed, lonely;
            /// <summary>The weight on them is a loss.</summary>
            public bool grieving;
            /// <summary>A day off can be offered (the setting on); a gathering can be called (somewhere to gather).</summary>
            public bool dayOff = true, gathering = true;

            public float R(StrainSource s) => (int)s < recent.Length ? Math.Max(0f, recent[(int)s]) : 0f;
        }

        /// <summary>Advice counts from here.</summary>
        public const float AdviceMin = 2f;

        /// <summary>
        /// How much each kind of advice would ease them, by what wears on them now (strain built lately, by source) and how
        /// they are (hungry, in pain...). A decree is weighed apart (<see cref="DecreeWeight"/>).
        /// </summary>
        public static float Weight(AdviceKind k, AdviceFacts f)
        {
            switch (k)
            {
                case AdviceKind.DayOff: return f.dayOff && f.strain >= DayOffFrom ? 0.45f * f.strain : 0f;
                case AdviceKind.Meal: return f.R(StrainSource.Hunger) + (f.hungry ? 6f : 0f);
                case AdviceKind.Bed: return f.R(StrainSource.BadSleep) + 0.5f * f.R(StrainSource.Exhaustion) + (f.badBed ? 3f : 0f);
                case AdviceKind.Recreation: return f.R(StrainSource.Joyless) + (f.joyless ? 5f : 0f);
                case AdviceKind.Pain: return f.R(StrainSource.Pain) + 0.5f * f.R(StrainSource.Downed) + (f.inPain ? 6f : 0f);
                case AdviceKind.Peace: return f.R(StrainSource.Combat) + f.R(StrainSource.Fear);
                case AdviceKind.Friend: return f.R(StrainSource.Unmet) + f.R(StrainSource.Despair) + 0.5f * f.R(StrainSource.Grief) + (f.lonely ? 6f : 0f);
                case AdviceKind.Gathering:
                    if (!f.gathering) return 0f;
                    return 0.5f * (f.R(StrainSource.LowMood) + f.R(StrainSource.Letdowns) + f.R(StrainSource.Joyless) + f.R(StrainSource.Unmet) + f.R(StrainSource.Loss))
                           + (f.strain >= DayOffFrom ? 3f : 0f);
                case AdviceKind.Grieve: return f.R(StrainSource.Loss) + f.R(StrainSource.Grief) + (f.grieving ? 8f : 0f);
                case AdviceKind.Light: return f.R(StrainSource.Dark);
                default: return 0f;
            }
        }

        /// <summary>
        /// A decree that eases these sources (none named: all of strain, as days of rest do), for this colonist: a little
        /// less than what they could be given themselves, as it is the whole colony's.
        /// </summary>
        public static float DecreeWeight(AdviceFacts f, IList<StrainSource> eases)
        {
            if (eases == null || eases.Count == 0) return 0.4f * f.strain;
            float w = 0f;
            foreach (var s in eases) w += f.R(s);
            return 0.6f * w;
        }

        /// <summary>The personal kinds of advice (the decrees are added by the game side, each with its own weight).</summary>
        public static readonly AdviceKind[] Personal =
        {
            AdviceKind.DayOff, AdviceKind.Gathering, AdviceKind.Friend, AdviceKind.Meal, AdviceKind.Bed, AdviceKind.Recreation, AdviceKind.Pain,
            AdviceKind.Peace, AdviceKind.Grieve, AdviceKind.Light
        };

        /// <summary>The personal advice worth giving, the most easing first (ties by the order of <see cref="Personal"/>).</summary>
        public static List<(AdviceKind kind, float weight)> Rank(AdviceFacts f)
        {
            var list = new List<(AdviceKind, float)>();
            foreach (var k in Personal)
            {
                float w = Weight(k, f);
                if (w >= AdviceMin) list.Add((k, w));
            }
            // A stable sort: biggest first, ties in the listed order.
            var ordered = new List<(AdviceKind kind, float weight)>(list.Count);
            foreach (var x in list)
            {
                int at = ordered.Count;
                while (at > 0 && ordered[at - 1].weight < x.Item2) at--;
                ordered.Insert(at, x);
            }
            return ordered;
        }

        private static float Clean(float v) => float.IsNaN(v) || v < 0f ? 0f : v;
    }
}
