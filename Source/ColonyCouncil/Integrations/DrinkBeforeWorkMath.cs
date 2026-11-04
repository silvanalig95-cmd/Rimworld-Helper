using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The numbers and rules behind a drink or a smoke before work (Integrations/BeforeWork.cs), kept apart from the game
    /// so they can be tested: how often, how near, which work, how far from the colonist's tolerance limit, and which
    /// stages of the high are mild. Every row (the drink, the smoke) follows the same rules.
    /// </summary>
    public static class DrinkBeforeWorkMath
    {
        /// <summary>Once a work stretch: eight hours between one choice and the next.</summary>
        public const int CooldownTicks = 20000;

        /// <summary>Most of the time when everything allows it.</summary>
        public const float Chance = 0.7f;

        /// <summary>The drink is within this many cells of the colonist (a stockpile on the way, not across the map).</summary>
        public const float MaxDistance = 25f;

        /// <summary>And within this many cells of the work, so the walk to it isn't a detour.</summary>
        public const float MaxDetour = 35f;

        /// <summary>Only adults, by the years their body has lived.</summary>
        public const float MinAge = 18f;

        /// <summary>The drink leaves them in one of the hediff's first two stages (warm or tipsy), never drunk.</summary>
        public const int MildStages = 2;

        /// <summary>Their tolerance stays under this share of where addiction or harm can start.</summary>
        public const float ToleranceMargin = 0.6f;

        /// <summary>The highest tolerance they may reach with the drink, whatever the drink says.</summary>
        public const float ToleranceCap = 0.15f;

        /// <summary>Time for the next choice: none made yet, the wait is over, or the clock went back (another game).</summary>
        public static bool Due(int last, int now, int wait = CooldownTicks) => last < 0 || now < last || now - last >= wait;

        /// <summary>
        /// How high their tolerance may go with the drink: under <see cref="ToleranceMargin"/> of the lowest point where
        /// the drink can start an addiction (<paramref name="minToAddict"/>) or the tolerance starts doing harm
        /// (<paramref name="harmFrom"/>, a liver or brain effect), and never above <see cref="ToleranceCap"/>.
        /// Zero or less means unknown.
        /// </summary>
        public static float ToleranceLimit(float minToAddict, float harmFrom)
        {
            float lowest = float.MaxValue;
            if (minToAddict > 0f) lowest = Math.Min(lowest, minToAddict);
            if (harmFrom > 0f) lowest = Math.Min(lowest, harmFrom);
            return lowest == float.MaxValue ? ToleranceCap : Math.Min(ToleranceCap, lowest * ToleranceMargin);
        }

        /// <summary>The drink leaves their tolerance under the limit.</summary>
        public static bool ToleranceRoom(float current, float gain, float limit) => current + Math.Max(0f, gain) < limit;

        /// <summary>How strong one drink is in this body (smaller bodies feel it more), never dividing by nothing.</summary>
        public static float Strength(float severity, float bodySize) => severity / Math.Max(0.2f, bodySize);

        /// <summary>
        /// The work may be done a little drunk without anyone getting hurt: making, building, mining, growing,
        /// research and art; cooking only when the drink makes food poisoning no likelier (<paramref name="cookingSafe"/>);
        /// social work only as a talk with a prisoner (<paramref name="prisonerChat"/>). Never medicine (tending,
        /// surgery), shooting or melee (hunting) or animals (taming and training can turn an animal on its handler),
        /// nor a skill another mod adds.
        /// </summary>
        public static bool SafeSkill(string skill, bool cookingSafe, bool prisonerChat)
        {
            switch (skill)
            {
                case "Crafting":
                case "Construction":
                case "Mining":
                case "Plants":
                case "Intellectual":
                case "Artistic":
                    return true;
                case "Cooking":
                    return cookingSafe;
                case "Social":
                    return prisonerChat;
                default:
                    return false;
            }
        }

        /// <summary>The drink is near enough: <paramref name="toWork"/> below zero means the work has no place.</summary>
        public static bool Near(float fromPawn, float toWork) => fromPawn <= MaxDistance && (toWork < 0f || toWork <= MaxDetour);

        // ---------------- Every row: the drink, the smoke (Integrations/BeforeWork.cs) ----------------

        /// <summary>
        /// A mild stage makes a fight no likelier than one beer does in anyone (the game's warm stage, 1.5): one can still
        /// come of it, at the game's own rate, never more.
        /// </summary>
        public const float MaxFightFactor = 1.5f;

        /// <summary>The most a smoke's high may slow the mind or the hands: smokeleaf's own (consciousness down 30%).</summary>
        public const float SmokeMaxSlowing = 0.3f;

        /// <summary>A stage that makes a fight no likelier than one beer.</summary>
        public static bool Calm(float fightFactor) => fightFactor <= MaxFightFactor + 0.0001f;

        /// <summary>
        /// How much one of a stage's capacity changes takes away: what is left of the capacity after its offset, its factor
        /// and its cap (<paramref name="setMax"/> 1 or more is no cap), taken from 1.
        /// </summary>
        public static float Slowing(float offset, float postFactor, float setMax)
        {
            float left = (1f + offset) * postFactor;
            if (setMax < 1f) left = Math.Min(left, setMax);
            return Math.Max(0f, 1f - left);
        }

        /// <summary>The high slows them within the row's limit (<paramref name="cap"/> 1 or more is no limit).</summary>
        public static bool SlowsWithin(float slowing, float cap) => cap >= 1f || slowing <= cap + 0.0001f;

        /// <summary>
        /// Where one of a tolerance's effects starts doing harm. The game's own give a curve and nothing at or below its
        /// first point (<paramref name="curveStart"/>), and minSeverity counts for nothing then; older ones and some mods'
        /// give minSeverity. 0 is harm from the start.
        /// </summary>
        public static float HarmStarts(float minSeverity, bool byCurve, float curveStart) => Math.Max(0f, byCurve ? curveStart : minSeverity);
    }
}
