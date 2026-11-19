using System;

namespace ColonyCouncil
{
    /// <summary>How the cache in the old ruins stands.</summary>
    public enum CacheEnd
    {
        /// <summary>Still at the ruins, or on its way home.</summary>
        Away,
        /// <summary>On the colony's map: their people come for it.</summary>
        Home,
        /// <summary>Gone: destroyed, or the ruins left behind without it and the time up.</summary>
        Lost
    }

    /// <summary>
    /// The pure rules of Q14, a cache in old ruins (docs/quests-proposals.md; the quests' round 3; the worker in
    /// Workers_MissionHome5.cs): the one quest that sends the colony's people out, offered only when the ruins are within three
    /// tiles of the colony and rarer than the quests at home (rule 2 of the proposals: away only when it is close, and at most
    /// one quest in ten). Its reward by the asking people's age (the quests at home's own scale, HomeQuestsMath), its days by
    /// the distance, and what counts as done. No game types, so Tools/Tests can check it.
    /// </summary>
    public static class HomeQuests3Math
    {
        public const string CacheKind = "ruinscache";

        /// <summary>Its mission kinds (one).</summary>
        public static readonly string[] Kinds = { CacheKind };

        /// <summary>The ruins at most this many tiles from the colony (about a day on foot), at least one.</summary>
        public const int MinTiles = 1, MaxTiles = 3;

        public static bool WithinReach(float tiles) => tiles >= MinTiles && tiles <= MaxTiles;

        /// <summary>
        /// Its base chance: half the quests at home's (0.3), so with the thirteen at home it is under one quest in ten that sends
        /// the colony's people out, together with the storylines' away parts.
        /// </summary>
        public const float BaseChance = 0.15f;

        /// <summary>The same quest not again for this long (days).</summary>
        public const int RestDays = 90;

        /// <summary>The share of all the single quests' chances that send people away: never more than one in ten.</summary>
        public static float AwayShare(float awayChances, float allChances) => allChances <= 0f ? 0f : Math.Max(0f, awayChances) / allChances;

        /// <summary>Days the colony has: there and back with time to look around, two days a tile and four besides, six to ten.</summary>
        public static int Days(float tiles) => 4 + 2 * (int)Math.Ceiling(Math.Max(MinTiles, Math.Min(MaxTiles, tiles)));

        /// <summary>A few days more for a caravan on its way home with the cache when the time runs out.</summary>
        public const int HomewardDays = 3;

        /// <summary>The silver their people pay for the cache brought home, before their age and the colony's wealth.</summary>
        public const float RewardMin = 220f, RewardMax = 380f;

        /// <summary>The reward: the quest's range (by <paramref name="roll"/>) times the people's age and the colony's wealth, rounded to tens.</summary>
        public static int Reward(int age, bool empire, float wealth, float roll)
        {
            float r = Math.Max(0f, Math.Min(1f, roll));
            float w = Math.Max(0.6f, Math.Min(2f, wealth));
            return HomeQuestsMath.Tens(Math.Max(30f, (RewardMin + (RewardMax - RewardMin) * r) * HomeQuestsMath.AgeFactor(age, empire) * w));
        }

        /// <summary>What else lay with the cache, the colony's to keep: worth half the reward.</summary>
        public const float KeepShare = 0.5f;

        public static int KeepWorth(int reward) => HomeQuestsMath.Tens(Math.Max(0, reward) * KeepShare);

        public const int Goodwill = 12, Penalty = 6;

        /// <summary>Where the cache stands: on the colony's map, gone, or still away (at the ruins, or with the colony's caravan).</summary>
        public static CacheEnd Outcome(bool destroyed, bool onHomeMap, bool withCaravan, bool ruinsStand, bool timeUp)
        {
            if (destroyed) return CacheEnd.Lost;
            if (onHomeMap) return CacheEnd.Home;
            if (withCaravan) return CacheEnd.Away;
            return ruinsStand && !timeUp ? CacheEnd.Away : CacheEnd.Lost;
        }
    }
}
