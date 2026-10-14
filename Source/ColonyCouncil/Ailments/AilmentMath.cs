using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure arithmetic behind ailments and illnesses, with no game types, so the tests can check the claims the
    /// Almanac makes ("fatal untreated, beatable with good care") against the numbers in the defs.
    /// </summary>
    public static class AilmentMath
    {
        /// <summary>
        /// The kind of medicine a treatment used, from the medicine's best possible quality (its MedicalQualityMax): 0
        /// for herbal medicine or none (70%), 1 for industrial medicine (100%), 2 for glitterworld medicine (130%).
        /// Modded medicines fall where their best quality puts them.
        /// </summary>
        public static int TierOf(float maxQuality) => maxQuality >= 1.2f ? 2 : maxQuality >= 0.99f ? 1 : 0;

        /// <summary>The factor a treatment's effect gets for the medicine used.</summary>
        public static float TierFactor(int tier, float basic, float industrial, float ultra) =>
            tier >= 2 ? ultra : tier == 1 ? industrial : basic;

        /// <summary>Clamps a frequency setting to its slider's range.</summary>
        public static float Frequency(float f) => f < 0.25f ? 0.25f : f > 2f ? 2f : f;

        /// <summary>
        /// The mean days between two catching ailments on a map: the base, scaled by the biome's own disease rate (60
        /// days is the game's usual), the storyteller's disease interval and the frequency setting.
        /// </summary>
        public static float CatchingMtbDays(float baseDays, float biomeMtbDays, float difficultyFactor, float frequency, float situationFactor)
        {
            float biome = biomeMtbDays > 0f ? biomeMtbDays / 60f : 1f;
            biome = biome < 0.5f ? 0.5f : biome > 2f ? 2f : biome;
            float difficulty = difficultyFactor > 0f ? difficultyFactor : 1f;
            return baseDays * biome * difficulty * situationFactor / Frequency(frequency);
        }

        /// <summary>One illness's numbers, as its HediffDef gives them, for <see cref="Simulate"/>.</summary>
        public class Course
        {
            public float severityPerDayNotImmune;
            public float immunityPerDaySick;
            public float severityPerDayImmune;
            /// <summary>Severity per day while tended, at quality 1 and a medicine factor of 1 (negative).</summary>
            public float severityPerDayTended;
            /// <summary>Extra immunity per day while tended (tuberculosis), times quality and the medicine factor.</summary>
            public float treatedImmunityPerDay;
            /// <summary>Immunity lost per day while untended after treatment began (tuberculosis).</summary>
            public float untendedImmunityLossPerDay;
            public float startSeverity = 0.05f;
            /// <summary>0 or less: never lethal.</summary>
            public float lethalSeverity = 1f;
            public float maxSeverity = 1f;
        }

        public struct Outcome
        {
            public bool died;
            public float peak;
            public float days;
            public float daysToImmune;
        }

        /// <summary>
        /// Runs an illness hour by hour: severity against immunity, as the game's immunity race does. Tending covers
        /// <paramref name="tendedShare"/> of the hours (1 = always tended) at <paramref name="quality"/>, with the
        /// medicine factor <paramref name="medicine"/>; treatment stops after <paramref name="stopTendingDay"/> days
        /// (negative: never). <paramref name="immunityGain"/> is the colonist's immunity gain speed (1 for a healthy
        /// adult), <paramref name="severityFactor"/> the illness's own random factor (0.8 to 1.2 in the game).
        /// </summary>
        public static Outcome Simulate(Course c, float quality, float medicine, float tendedShare, float immunityGain = 1f,
            float severityFactor = 1f, float stopTendingDay = -1f, int maxDays = 200)
        {
            const float step = 1f / 24f;
            float severity = c.startSeverity, immunity = 0f, peak = severity;
            bool everTended = false;
            float immuneAt = -1f;
            int hours = maxDays * 24;
            for (int h = 0; h < hours; h++)
            {
                float day = h * step;
                bool tending = quality > 0f && tendedShare > 0f && (stopTendingDay < 0f || day < stopTendingDay)
                               && (tendedShare >= 1f || (h % 100) < tendedShare * 100f);
                if (tending) everTended = true;
                bool immune = immunity >= 1f;
                if (immune && immuneAt < 0f) immuneAt = day;
                float ds = immune ? c.severityPerDayImmune : c.severityPerDayNotImmune * severityFactor;
                if (tending) ds += c.severityPerDayTended * quality * medicine;
                float di = immune ? 0f : c.immunityPerDaySick * immunityGain;
                if (tending) di += c.treatedImmunityPerDay * quality * medicine;
                else if (everTended) di -= c.untendedImmunityLossPerDay;
                severity = Math.Min(c.maxSeverity, severity + ds * step);
                immunity = Math.Min(1f, Math.Max(0f, immunity + di * step));
                if (severity > peak) peak = severity;
                if (c.lethalSeverity > 0f && severity >= c.lethalSeverity)
                    return new Outcome { died = true, peak = severity, days = day, daysToImmune = immuneAt };
                if (severity <= 0f) return new Outcome { died = false, peak = peak, days = day, daysToImmune = immuneAt };
            }
            return new Outcome { died = false, peak = peak, days = maxDays, daysToImmune = immuneAt };
        }

        /// <summary>
        /// Days a diet illness (scurvy) takes from its first signs to the lethal point while no fresh food is eaten, and
        /// back to nothing from a given severity once it is.
        /// </summary>
        public static float DaysToLethal(float start, float growPerDay, float lethal = 1f) => growPerDay <= 0f ? float.MaxValue : (lethal - start) / growPerDay;

        public static float DaysToClear(float from, float recoverPerDay) => recoverPerDay >= 0f ? float.MaxValue : from / -recoverPerDay;
    }
}
