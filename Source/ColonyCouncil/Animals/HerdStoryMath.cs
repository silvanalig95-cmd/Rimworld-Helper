using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The numbers of the herd's year and of a wild beast that stays (Animals/HerdStory.cs, Animals/ResidentBeast.cs):
    /// who leads the herd, how a line is counted, when the first calf of spring is one, how long a wild animal must
    /// hang about to be given a name and when its fate is decided. Pure, so Tools/Tests can check it.
    /// </summary>
    public static class HerdStoryMath
    {
        // ---------------- The herd ----------------

        /// <summary>A herd this small has no leader to speak of.</summary>
        public const int MinHerd = 4;

        /// <summary>
        /// How fit an adult is to lead: its years (the elders lead), its kind's share of the herd (the leader is one of the
        /// many, not the lone goat), and the old leader's own blood before any elder (<see cref="LineFirst"/>).
        /// </summary>
        public static float LeaderScore(float ageYears, int ofKind, int herd, bool ofTheLine)
        {
            float share = herd <= 0 ? 0f : Math.Max(0f, Math.Min(1f, ofKind / (float)herd));
            return Math.Max(0f, ageYears) + 4f * share + (ofTheLine ? LineFirst : 0f);
        }

        /// <summary>What the old leader's blood is worth in years when the herd picks a new leader.</summary>
        public const float LineFirst = 10f;

        /// <summary>The generation a newborn belongs to: its mother's, one on.</summary>
        public static int Generation(int motherGeneration) => Math.Max(1, motherGeneration) + 1;

        /// <summary>Generations worth telling of: the second and every one after it, the first time each is reached.</summary>
        public static bool GenerationNews(int generation, int toldUpTo) => generation >= 2 && generation > toldUpTo;

        /// <summary>The first calf of a spring is a moment once a year, in spring.</summary>
        public static bool SpringCalf(bool spring, int year, int markedYear) => spring && year != markedYear;

        // ---------------- A beast that stays ----------------

        /// <summary>A wild animal seen on the land this many days running is one the colony starts to know.</summary>
        public const int ResidentAfterDays = 3;
        /// <summary>The first question about it, days after it was named; and again every so often, this many times at most.</summary>
        public const float AskDays = 4f, AskEvery = 8f;
        public const int MaxAsks = 3;
        /// <summary>Still here after a season: it is part of the land, and the story closes.</summary>
        public const float FateAfterDays = 15f;
        /// <summary>Days before another beast can be the one the colony watches.</summary>
        public const float RestDays = 20f;

        /// <summary>Big enough to be noticed and a plain animal: a predator, or a sizeable grazer; not an insect, not a great beast.</summary>
        public static bool BeastFits(float bodySize, bool predator, bool insect, bool majestic) =>
            !insect && !majestic && (predator ? bodySize >= 0.6f : bodySize >= 1.4f);

        /// <summary>The question is due: named long enough, not asked too often, a gap since the last.</summary>
        public static bool AskDue(float daysNamed, int asks, float daysSinceAsk) =>
            asks < MaxAsks && daysNamed >= AskDays && (asks == 0 || daysSinceAsk >= AskEvery);

        /// <summary>What the colony has made of it, for its lines: fed, feared, or only watched.</summary>
        public static string Stance(int fed, bool feared) => fed > 0 ? "fed" : feared ? "feared" : "watched";

        // ---------------- Pokémon (with PokéWorld) ----------------

        /// <summary>A wild Pokémon big enough to be the one the colony watches: most are small, so the bar is low.</summary>
        public static bool PokemonFits(float bodySize) => bodySize >= 0.3f;

        /// <summary>A Pokémon not yet fed nor caught gets into the stores now and then, from its second day: this chance a day.</summary>
        public const float StealChance = 0.3f;
        public static bool StealDue(string stance, float daysNamed, float daysSinceSteal) =>
            (stance == "watched" || stance == "feared") && daysNamed >= 1f && daysSinceSteal >= 1f;

        /// <summary>A rumour of a shiny one holds this many days, and the shiny shows itself with this chance a day while it does.</summary>
        public const float RumourDays = 20f, RumourChance = 0.12f;

        /// <summary>
        /// A friendly contest between two trainers' Pokémon, as a show: the colony's chance to win by the two levels
        /// (even at the same level, never a sure thing either way).
        /// </summary>
        public static float ShowWinChance(int ourLevel, int theirLevel) => Math.Max(0.15f, Math.Min(0.85f, 0.5f + (ourLevel - theirLevel) / 60f));
    }
}
