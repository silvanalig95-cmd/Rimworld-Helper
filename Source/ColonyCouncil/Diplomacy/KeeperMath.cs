using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What pressing a secret came to: they gave in (kept), or refused and it was told (broken).</summary>
    public enum PressAnswer
    {
        Complied,
        Refused
    }

    /// <summary>
    /// The keeper of secrets (docs/design/stage2-diplomacy.md, item 8; decision 13; release 2c.1), with no game types so
    /// Tools/Tests can check it: the office's edge (the colony's own secrets slip half as often while the seat is filled),
    /// the watch after dark (the keeper walks with the guests; release 2c.3's schemes are caught twice as often while
    /// they do), sounding out one of theirs face to face, the roll of Social and Intellectual against the notable's (a
    /// secret learned, nothing, or found out one time in ten), what their people's secrets are and how much each weighs,
    /// Secret ties (from whoever knows it to the one it is about: twenty live at most, for a year) and pressing one: they
    /// give in by their regard and how much it weighs, or refuse and it is told.
    /// </summary>
    public static class KeeperMath
    {
        public const int DayTicks = 60000;

        // ---------------- The office ----------------

        /// <summary>The office opens at six voters (decision 13).</summary>
        public const int Voters = 6;

        /// <summary>While the seat is filled, the colony's own secrets slip half as often.</summary>
        public static float SlipFactor(bool filled) => filled ? 0.5f : 1f;

        /// <summary>While the keeper walks with the guests after dark, their schemes are caught twice as often (release 2c.3 reads it).</summary>
        public static float CatchFactor(bool watching) => watching ? 2f : 1f;

        // ---------------- The watch ----------------

        /// <summary>After dark: from 20 o'clock to 2.</summary>
        public static bool AfterDark(int hour) => hour >= 20 || hour < 2;

        /// <summary>A walk with the guests lasts about an hour and a half.</summary>
        public const int WatchTicks = 3750;

        /// <summary>A walk with the guests may overhear something about a notable of their people (only when they have one known).</summary>
        public const float OverhearChance = 0.15f;

        // ---------------- Sounding out ----------------

        /// <summary>
        /// The chance a step learns something: 35% when the two sides are even in Social and Intellectual, 2% more for each
        /// level the colony's side is ahead, from a tenth to four in five; a visit to their town and a paid informer add to it.
        /// </summary>
        public static float LearnChance(int ourSocial, int ourIntellectual, int theirSocial, int theirIntellectual, float bonus = 0f)
        {
            float c = 0.35f + 0.02f * (ourSocial + ourIntellectual - theirSocial - theirIntellectual) + bonus;
            return c < 0.1f ? 0.1f : c > 0.8f ? 0.8f : c;
        }

        public const float VisitBonus = 0.15f, InformerBonus = 0.1f;

        /// <summary>Found out one step in ten, whatever the skill (the design's "bad roll").</summary>
        public const float FoundOutChance = 0.1f;

        /// <summary>One roll: the lowest tenth is found out; the rest learn by the chance.</summary>
        public static SecretRoll Roll(float chance, float roll)
        {
            roll = TaskMath.Clamp01(roll);
            if (roll < FoundOutChance) return SecretRoll.FoundOut;
            return (roll - FoundOutChance) / (1f - FoundOutChance) < chance ? SecretRoll.Learned : SecretRoll.Nothing;
        }

        /// <summary>Found out: the notable thinks this much less of the colony, and this much less of the keeper.</summary>
        public const float CaughtRegard = -15f, CaughtKeeperRegard = -25f;

        /// <summary>The notable's memory keeps what they caught (their hard words come at the next meeting): "task:caught", then "task:caughtsaid".</summary>
        public const string CaughtKey = "task:caught", CaughtSaidKey = "task:caughtsaid";

        /// <summary>A paid informer at the gate: their silver by the colony's age (60 for a tribe to 120 for a spacer colony).</summary>
        public static int InformerSilver(int age) => 60 + 20 * Math.Max(0, Math.Min(3, age));

        // ---------------- Their secrets ----------------

        /// <summary>
        /// What a notable of theirs may hide, each with how much it weighs (1 a small shame, 3 a ruin): a debt their town
        /// can't pay, a cut of every caravan, raiders paid to stay away, grain hidden from their own people in a hard year,
        /// an heir who isn't who the town thinks, a raider's past, a sickness they keep quiet, a lover the town doesn't know of.
        /// </summary>
        public static readonly string[] Kinds = { "debt", "bribe", "raiders", "stash", "heir", "past", "sick", "lover" };

        public static int Weight(string kind)
        {
            switch (kind)
            {
                case "raiders":
                case "heir":
                case "stash": return 3;
                case "bribe":
                case "past":
                case "debt": return 2;
                default: return 1;
            }
        }

        public static bool IsKind(string kind) => Array.IndexOf(Kinds, kind) >= 0;

        /// <summary>A kind picked by a roll (the same roll the same kind).</summary>
        public static string Pick(float roll) => Kinds[Math.Min(Kinds.Length - 1, (int)(TaskMath.Clamp01(roll) * Kinds.Length))];

        // ---------------- Secret ties ----------------

        /// <summary>Twenty live at most (the design's bound): a new one past it lets the oldest go.</summary>
        public const int MaxLive = 20;

        /// <summary>A secret matters for a year; then it ends (no one cares any more).</summary>
        public const int Days = 60;

        public const string Origin = "keeper";

        /// <summary>Of the oldest live ones, how many must go for one more to fit.</summary>
        public static int ToLetGo(int live) => Math.Max(0, live + 1 - MaxLive);

        // ---------------- Pressing it ----------------

        /// <summary>
        /// How likely they give in: the heavier the secret and the better they think of the colony, the likelier; a kind
        /// nature a little likelier (they'd rather settle quietly), a cold one less; from a tenth to nine in ten.
        /// </summary>
        public static float ComplyChance(int weight, int opinion, int warmth)
        {
            float c = 0.25f + 0.15f * Math.Max(1, Math.Min(3, weight)) + opinion / 250f + Math.Max(-2, Math.Min(2, warmth)) * 0.05f;
            return c < 0.1f ? 0.1f : c > 0.9f ? 0.9f : c;
        }

        public static PressAnswer Answer(int weight, int opinion, int warmth, float roll) =>
            TaskMath.Clamp01(roll) < ComplyChance(weight, opinion, warmth) ? PressAnswer.Complied : PressAnswer.Refused;

        /// <summary>What they send to keep it quiet: 100 silver's worth a weight.</summary>
        public static int Hush(int weight) => 100 * Math.Max(1, Math.Min(3, weight));

        /// <summary>Pressed at all: the notable thinks this much less of the colony; refused and told, this much less again.</summary>
        public const float PressedRegard = -10f, ToldRegard = -30f;

        /// <summary>Their people remember a secret told as the colony's scheme (the deed schemed).</summary>
        public const string ToldDeed = "schemed";

        /// <summary>
        /// The notable's memory keeps that they were pressed, for their talk (them:pressed), and how: by the colony's letter
        /// (them:pressed:letter) or face to face by the keeper's "Sound out" (them:pressed:face). A save from before keeps only
        /// that they were.
        /// </summary>
        public const string PressedKey = "task:pressed", PressedLetterKey = "task:pressedletter", PressedFaceKey = "task:pressedface";

        // ---------------- Facts ----------------

        /// <summary>
        /// Talk tags: the one spoken to caught the colony's keeper at it (them:caughtkeeper; :you when it is the keeper
        /// talking), was pressed by the colony (them:pressed, either way), by its letter (them:pressed:letter) or face to face
        /// (them:pressed:face), or was walked with after dark lately (keeper:watched).
        /// </summary>
        public const string CaughtTag = "them:caughtkeeper", CaughtYouTag = "them:caughtkeeper:you", PressedTag = "them:pressed", WatchedTag = "keeper:watched",
            PressedLetterTag = "them:pressed:letter", PressedFaceTag = "them:pressed:face";

        public static readonly string[] Tags = { CaughtTag, CaughtYouTag, PressedTag, WatchedTag, PressedLetterTag, PressedFaceTag };

        /// <summary>What the notable's memory keeps of a press: that they were, and how.</summary>
        public static string[] PressedKeys(bool face) => new[] { PressedKey, face ? PressedFaceKey : PressedLetterKey };

        /// <summary>The talk's tag for a key of the notable's memory about a press (null for any other).</summary>
        public static string PressedTagOf(string doneKey) =>
            doneKey == PressedKey ? PressedTag : doneKey == PressedLetterKey ? PressedLetterTag : doneKey == PressedFaceKey ? PressedFaceTag : null;

        /// <summary>A walk after dark is "lately" for two days.</summary>
        public const int WatchedDays = 2;
    }
}
