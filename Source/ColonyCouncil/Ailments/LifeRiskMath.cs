using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The numbers behind the realistic ways to die (Ailments/LifeRisks.cs, Health/LastSeason.cs): how much a crowded,
    /// dirty colony raises the chance of a serious illness, how much harder the frail fight an illness off, when the
    /// end of a long life begins and how fast it comes, and how much a wound left untended in the dirt is likelier to
    /// turn bad. No game types, so Tools/Tests checks it.
    /// </summary>
    public static class LifeRiskMath
    {
        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        // ---------------- Sickness in a crowded, dirty colony ----------------

        /// <summary>A crowded, dirty colony catches serious illness at most this many times as often as a well-kept one.</summary>
        public const float MaxSicknessFactor = 2.5f;

        /// <summary>From this factor on, the advisor says so, with the reasons.</summary>
        public const float SicknessWorthSaying = 1.3f;

        /// <summary>Sleeping crowded (in barracks, or with no bed at all): up to 60% more when everyone does.</summary>
        public static float CrowdFactor(int crowded, int people) => people <= 0 ? 1f : 1f + 0.6f * Clamp(crowded / (float)people, 0f, 1f);

        /// <summary>The dirtiest kitchen's cleanliness: nothing down to -1, then more, up to 35% more from -3.</summary>
        public static float KitchenFactor(float cleanliness) =>
            cleanliness >= -1f ? 1f : cleanliness <= -3f ? 1.35f : 1f + 0.175f * (-1f - cleanliness);

        /// <summary>More people, more for an illness to go round: gently, by the square root of the colony's size against eight.</summary>
        public static float SizeFactor(int people) => people <= 0 ? 1f : Clamp((float)Math.Sqrt(people / 8f), 0.8f, 1.6f);

        /// <summary>How many times as often a serious illness is caught here (the catching clock's days are divided by it).</summary>
        public static float SicknessFactor(int people, int crowded, float? kitchenCleanliness) =>
            Math.Min(MaxSicknessFactor, CrowdFactor(crowded, people) * (kitchenCleanliness.HasValue ? KitchenFactor(kitchenCleanliness.Value) : 1f) * SizeFactor(people));

        // ---------------- The frail ----------------

        /// <summary>However frail, the body still fights at this share of its strength.</summary>
        public const float MinFrailty = 0.45f;

        /// <summary>Old age (in human years): full strength to 60, then a tenth less each ten years, down to 70% from 90.</summary>
        public static float AgeFrailty(float age) => age <= 60f ? 1f : age >= 90f ? 0.7f : 1f - 0.01f * (age - 60f);

        /// <summary>A small child: 80% under three, 90% under six.</summary>
        public static float ChildFrailty(float age) => age < 3f ? 0.8f : age < 6f ? 0.9f : 1f;

        /// <summary>Malnutrition at this severity (0 to 1): up to 35% less.</summary>
        public static float HungerFrailty(float malnutrition) => 1f - 0.35f * Clamp(malnutrition, 0f, 1f);

        /// <summary>Badly hurt (health under half): 15% less.</summary>
        public static float HurtFrailty(float healthShare) => healthShare < 0.5f ? 0.85f : 1f;

        /// <summary>How fast their body builds immunity, as a share of a healthy adult's.</summary>
        public static float Frailty(float age, float malnutrition, float healthShare) =>
            Math.Max(MinFrailty, AgeFrailty(age) * ChildFrailty(age) * HungerFrailty(malnutrition) * HurtFrailty(healthShare));

        // ---------------- The end of a long life ----------------

        /// <summary>The age (in human years) from which a life may begin to close.</summary>
        public const float LastSeasonFrom = 78f;

        private static readonly (float age, float chance)[] Hazard = { (78f, 0f), (80f, 0.1f), (85f, 0.3f), (90f, 0.55f), (95f, 0.8f), (100f, 0.95f) };

        /// <summary>The chance in a year that the end begins, at this age: none before 78, a tenth at 80, more than half at 90.</summary>
        public static float YearlyChance(float age)
        {
            if (age <= Hazard[0].age) return 0f;
            for (int i = 1; i < Hazard.Length; i++)
            {
                if (age > Hazard[i].age) continue;
                var (a0, c0) = Hazard[i - 1];
                var (a1, c1) = Hazard[i];
                return c0 + (c1 - c0) * (age - a0) / (a1 - a0);
            }
            return Hazard[Hazard.Length - 1].chance;
        }

        /// <summary>A yearly chance as a chance per day, over a year of <paramref name="daysPerYear"/> days.</summary>
        public static float DailyChance(float yearly, int daysPerYear = 60)
        {
            if (yearly <= 0f) return 0f;
            if (yearly >= 1f) return 1f;
            return (float)(1.0 - Math.Pow(1.0 - yearly, 1.0 / Math.Max(1, daysPerYear)));
        }

        /// <summary>The days the decline takes, start to end, with nothing to slow or hasten it.</summary>
        public const float DeclineDays = 18f;

        /// <summary>
        /// How far the decline goes in a day: slower when they're happy, have a room of their own and have had company
        /// of their own; faster when they're miserable.
        /// </summary>
        public static float DeclinePerDay(float mood, bool ownRoom, bool company)
        {
            float r = 1f / DeclineDays;
            if (mood >= 0.7f) r *= 0.75f;
            else if (mood <= 0.3f) r *= 1.3f;
            if (ownRoom) r *= 0.9f;
            if (company) r *= 0.85f;
            return r;
        }

        /// <summary>Made young enough again (an age reversal): the decline turns back, by this much a day.</summary>
        public const float RecoverPerDay = 0.1f;

        public static bool TurnsBack(float age) => age < LastSeasonFrom - 1f;

        /// <summary>From this far into the decline, the end is near: the family is told, and comes.</summary>
        public const float NearTheEnd = 0.85f;

        // ---------------- Wounds left untended ----------------

        /// <summary>Where someone lies with an untended wound: clean is as the game has it, dirt up to half as likely again, out in the open a third.</summary>
        public static float DirtFactor(float cleanliness, bool outdoors)
        {
            if (outdoors) return 1.35f;
            if (cleanliness >= 0f) return 1f;
            return cleanliness <= -3f ? 1.5f : 1f - cleanliness / 6f;
        }

        /// <summary>
        /// The game's own roll for an infection (at <paramref name="baseChance"/>) has failed: the further chance that
        /// makes the whole come to <paramref name="baseChance"/> times <paramref name="dirt"/> (never more than certain).
        /// </summary>
        public static float ExtraInfectionChance(float baseChance, float dirt)
        {
            if (baseChance <= 0f || baseChance >= 1f || dirt <= 1f) return 0f;
            float total = Math.Min(1f, baseChance * dirt);
            return Clamp((total - baseChance) / (1f - baseChance), 0f, 1f);
        }
    }
}
