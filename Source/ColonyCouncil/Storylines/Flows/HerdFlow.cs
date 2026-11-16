using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// S3, the strayed herd (StorylineHerd.cs; docs/quests-proposals.md). A herding people's animals stray onto the colony's
    /// land: pen them until the herders come. Then the herders ask the colony to keep part of the herd over the winter, fed.
    /// In spring they take the herd home and leave some young with the colony; animals that died are paid for, or the colony
    /// buys the rest.
    /// </summary>
    public sealed class HerdFlow : StorylineFlow
    {
        public const string Key = "herd";

        public override string Kind => Key;

        public override int Number => 3;

        public override string[] Parts => new[] { "stray", "winter", "spring" };

        public override string[] Endings => new[] { "declined", "lost", "notkept", "young", "paid", "bought", "unpaid", "taken", "gone", "faded" };

        public override string[] Outcomes(string part)
        {
            switch (part)
            {
                case "stray": return new[] { "penned", "most", "lost" };
                case "winter": return new[] { "kept", "somedied" };
                case "spring": return new[] { "young", "paid", "bought", "unpaid" };
            }
            return new string[0];
        }

        public override bool Failed(string outcome) => outcome == "lost" || outcome == "unpaid";

        public override StorylineMath.Step Next(string part, string outcome, int times, float roll)
        {
            switch (part)
            {
                case "stray":
                    if (outcome == "penned" || outcome == "most") return To("winter", 2f, 4f, outcome);
                    if (outcome == "lost") return End("lost");
                    if (outcome == StorylineMath.Declined) return End("declined");
                    return End(StorylineMath.Faded);
                case "winter":
                    // The keeping itself lasts the winter: spring's collection follows within a day or two of its end.
                    if (outcome == "kept") return To("spring", 1f, 2f);
                    if (outcome == "somedied") return To("spring", 1f, 2f, "died");
                    if (outcome == StorylineMath.Declined) return End("notkept");
                    return End(StorylineMath.Faded);
                case "spring":
                    if (outcome == "young" || outcome == "paid" || outcome == "bought" || outcome == "unpaid") return End(outcome);
                    // Not answered: the herders come for their animals all the same, and leave nothing.
                    if (outcome == StorylineMath.Declined) return End("taken", 0.3f, 1f);
                    return End("taken", 0.3f, 1f);
            }
            return End(StorylineMath.Faded);
        }

        public override bool EndingHasLetter(string ending) => ending == "taken" || ending == StorylineMath.Gone;

        // ================= The numbers =================

        /// <summary>Five to nine strayed head, by the colony's size.</summary>
        public static int StrayCount(int colonists, float roll) => Clamp(5 + (int)Math.Floor(Clamp(Safe(roll), 0f, 0.999f) * 4f) + (colonists >= 8 ? 1 : 0), 5, 9);

        /// <summary>Days until the herders come for them, once the colony takes them in.</summary>
        public const float StrayMinDays = 3f, StrayMaxDays = 4f;

        /// <summary>"penned" (every one there and in a pen or a barn), "most" (half or more there), "lost".</summary>
        public static string StrayOutcome(int penned, int here, int total)
        {
            if (total <= 0) return "lost";
            if (penned >= total) return "penned";
            return here * 2 >= total ? "most" : "lost";
        }

        /// <summary>The part of the herd the colony keeps over the winter: three to five.</summary>
        public static int KeepCount(int herd) => Clamp(herd / 2, 3, 5);

        /// <summary>The keeping lasts until spring: never less than ten days or more than 32; a season with no seasons here.</summary>
        public static float KeepDays(float daysToSpring, bool seasons) => !seasons || float.IsNaN(daysToSpring) ? StorylineMath.SeasonDays : Clamp(daysToSpring, 10f, 32f);

        /// <summary>The herders are first asked in summer or autumn (so the keeping is a winter), or where there are no seasons.</summary>
        public static bool AskSeason(string season) => season == "summer" || season == "fall" || season == "none";

        /// <summary>Fodder for the days left: what the kept animals eat in a day, times the days (a fifth spare).</summary>
        public static float FodderNeed(float perDay, float days) => Safe(perDay) * Safe(days) * 1.2f;

        /// <summary>"kept" (every one alive and here), "somedied".</summary>
        public static string WinterOutcome(int alive, int total) => alive >= total ? "kept" : "somedied";

        /// <summary>The young they leave: one, two when five or more were kept well.</summary>
        public static int YoungCount(int kept) => kept >= 5 ? 2 : 1;

        /// <summary>What a dead animal is paid for: its worth.</summary>
        public static int DeadPay(float worth) => Math.Max(20, StorylineMath.Round10(worth));

        /// <summary>What the colony pays to keep the rest: nine tenths of their worth.</summary>
        public static int BuyPrice(float worth) => Math.Max(30, StorylineMath.Round10(worth * 0.9f));

        /// <summary>
        /// The herd's animals by the asking people's age, from the game and the player's mods (by name; whichever are loaded
        /// and can live here are drawn from). <paramref name="age"/>: 0 tribal, 1 medieval, 2 industrial, 3 spacer.
        /// </summary>
        public static IEnumerable<string> Kinds(int age)
        {
            switch (age)
            {
                case 0: return new[] { "Muffalo", "Goat", "Yak", "Dromedary", "Muskox", "Aurochs", "AA_Thunderox", "AA_GreyCoatedMouflon", "Sheep" };
                case 1: return new[] { "Sheep", "Goat", "Cow", "Yak", "AEXP_Camel", "Muffalo", "Aurochs" };
                default: return new[] { "Cow", "Sheep", "Alpaca", "Goat", "Yak" };
            }
        }
    }
}
