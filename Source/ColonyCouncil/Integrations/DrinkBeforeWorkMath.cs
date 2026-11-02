using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The numbers and rules behind a drink before work (Integrations/DrinkBeforeWork.cs), kept apart from the game so
    /// they can be tested: how often, how near, which work, and how far from the colonist's tolerance limit.
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
    }
}
