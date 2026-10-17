using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The numbers behind convalescence and the names doctors and midwives earn, with no game types, so the tests can
    /// check what the Almanac promises: a recovery of days to a couple of weeks, a setback that only lengthens it, and a
    /// reputation that a run of losses can take away again.
    /// </summary>
    public static class HealthMath
    {
        // ---------------- Convalescence (Council_Ail_Convalescent) ----------------

        /// <summary>Severity lost per day out of bed, and the factor in bed (the hediff's AilmentRecovery comp).</summary>
        public const float RecoveryPerDay = 0.06f;
        public const float BedFactor = 1.5f;

        /// <summary>From here the hediff is at its "weak" stage: up and about this early is up too early.</summary>
        public const float WeakFrom = 0.4f;

        /// <summary>No source, setbacks included, takes it past this (15 days out of bed).</summary>
        public const float MaxSeverity = 0.9f;

        public static float DaysUp(float severity) => Math.Max(0f, severity) / RecoveryPerDay;

        public static float DaysInBed(float severity) => Math.Max(0f, severity) / (RecoveryPerDay * BedFactor);

        /// <summary>Days laid up in bed before getting up again leaves a colonist convalescing.</summary>
        public const float BedDaysBefore = 4f;

        /// <summary>After this many days laid up in bed: 0.15 at four days, a little more for each day after, at most 0.6.</summary>
        public static float AfterBedRest(float days) => days < BedDaysBefore ? 0f : Clamp(0.15f + 0.05f * (days - BedDaysBefore), 0.15f, 0.6f);

        /// <summary>
        /// After a major operation: on an organ or the brain (inside the body), or on a large part (a limb). The brain
        /// takes longest; a limb or an organ a few days less.
        /// </summary>
        public static float AfterSurgery(bool brain, bool inside, float coverage)
        {
            if (brain) return 0.4f;
            if (inside) return 0.35f;
            return Clamp(0.25f + coverage, 0.25f, 0.4f);
        }

        /// <summary>A part counts as major for an operation or a loss: inside the body, or at least this share of it.</summary>
        public const float MajorCoverage = 0.04f;

        public static bool MajorPart(bool inside, float coverage) => inside || coverage >= MajorCoverage;

        /// <summary>After losing a limb (or another large outer part): the bigger the part, the longer.</summary>
        public static float AfterLimbLost(float coverage) => Clamp(0.35f + coverage * 1.5f, 0.4f, 0.6f);

        /// <summary>An illness counts as serious once it has reached this severity.</summary>
        public const float SeriousPeak = 0.5f;

        /// <summary>After a dangerous illness pulled through: 0.3 for one that just reached half, up to 0.7 for a close call.</summary>
        public static float AfterIllness(float peak) => peak < SeriousPeak ? 0f : Clamp(0.3f + 0.8f * (peak - SeriousPeak), 0.3f, 0.7f);

        /// <summary>Hunger counts once it went as far as this (the game's malnutrition, moderate).</summary>
        public const float HungerFrom = 0.4f;

        /// <summary>After going hungry that long: 0.2 from moderate malnutrition, up to 0.45 for a close call.</summary>
        public static float AfterHunger(float peak) => peak < HungerFrom ? 0f : Clamp(0.2f + 0.5f * (peak - HungerFrom), 0.2f, 0.45f);

        /// <summary>Cold or heat counts once it went as far as this (the game's serious hypothermia or heatstroke).</summary>
        public const float ExposureFrom = 0.35f;

        /// <summary>After being brought in from the cold or the heat: 0.15 from serious, up to 0.4 for a close call.</summary>
        public static float AfterExposure(float peak) => peak < ExposureFrom ? 0f : Clamp(0.15f + 0.5f * (peak - ExposureFrom), 0.15f, 0.4f);

        /// <summary>Convalescence never adds up: a second cause raises it to the larger of the two.</summary>
        public static float Combine(float current, float added) => Math.Min(MaxSeverity, Math.Max(current, added));

        // ---------------- Setbacks ----------------

        /// <summary>The chance, per hour of work while still weak, that it sets the recovery back.</summary>
        public const float SetbackChancePerHour = 0.01f;

        /// <summary>A setback adds this much (two days out of bed): the recovery takes longer, and that is all.</summary>
        public const float SetbackSeverity = 0.12f;

        public const int MaxSetbacks = 2;
        public const float SetbackGapDays = 2f;

        public static float AfterSetback(float severity) => Math.Min(MaxSeverity, severity + SetbackSeverity);

        /// <summary>The chance of at least one setback in a day with this many hours of work while weak.</summary>
        public static float SetbackChancePerDay(float hours) => 1f - (float)Math.Pow(1f - SetbackChancePerHour, Math.Max(0f, hours));

        // ---------------- Reputation ----------------

        /// <summary>
        /// A doctor's standing from patients pulled through (illnesses and major operations) against patients lost: 0
        /// none yet, 1 trusted (3 saved, at least twice as many as lost), 2 renowned (8 saved, at least three times as many).
        /// </summary>
        public static int DoctorLevel(int saved, int lost)
        {
            if (saved >= 8 && saved >= lost * 3) return 2;
            if (saved >= 3 && saved >= lost * 2) return 1;
            return 0;
        }

        /// <summary>
        /// The hospital's name, from everyone it did right by (pulled through, operated on well, delivered, seen from
        /// outside) against the patients it lost: 0 not known yet, 1 well spoken of (6, at least three times as many as
        /// lost), 2 known across the region (15, at least four times as many).
        /// </summary>
        public static int HospitalLevel(int good, int lost)
        {
            if (good >= 15 && good >= lost * 4) return 2;
            if (good >= 6 && good >= lost * 3) return 1;
            return 0;
        }

        /// <summary>A sick traveller's visit is likelier from a hospital with a bigger name (the event's weight).</summary>
        public static float PatientWeight(int hospitalLevel) => hospitalLevel >= 2 ? 2f : hospitalLevel == 1 ? 1f : 0f;

        /// <summary>A midwife's standing from babies delivered alive: 1 from two, 2 from five.</summary>
        public static int MidwifeLevel(int births) => births >= 5 ? 2 : births >= 2 ? 1 : 0;

        /// <summary>Losses within this many days of each other are a run, which weighs heavier.</summary>
        public const float RunDays = 10f;
        public const int RunCount = 2;

        /// <summary>A death counts against the doctor who tended them within this many hours.</summary>
        public const float LostWithinHours = 24f;

        private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }
}
