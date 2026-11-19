using System;

namespace ColonyCouncil
{
    /// <summary>
    /// S11, the vault's sealed case (StorylineVault.cs; docs/quests-proposals.md). The Sunken Vault, or any spacer people:
    /// 1. components for the vault's failing power cells, collected by its runners (built on the vault's own power cells
    /// mission). 2. One of its engineers stays at the colony and works at the colony's bench, using a component a day from its
    /// stores. 3. They leave a sealed case in the colony's care, and a card asks whether to open it or keep it sealed and hand
    /// it back. Opened, it holds a sleeper who joins or runs, a machine that has to be fought, or old goods; handed back, the
    /// vault owes the colony a favour.
    /// </summary>
    public sealed class VaultFlow : StorylineFlow
    {
        public const string Key = "vault";

        public override string Kind => Key;

        public override int Number => 11;

        public override string[] Parts => new[] { "cells", "engineer", "case" };

        public override string[] Endings => new[] { "declined", "dark", "noengineer", "blamed", "opened", "favour", "lostcase", "refusedcase", "gone", "faded" };

        public override string[] Outcomes(string part)
        {
            switch (part)
            {
                case "cells": return new[] { "sent", "partly", "none" };
                case "engineer": return new[] { "worked", "short", "harmed" };
                case "case": return new[] { "opened", "returned", "lost" };
            }
            return new string[0];
        }

        public override bool Failed(string outcome) => outcome == "none" || outcome == "harmed" || outcome == "lost";

        public override StorylineMath.Step Next(string part, string outcome, int times, float roll)
        {
            switch (part)
            {
                case "cells":
                    if (outcome == "sent" || outcome == "partly") return To("engineer", 2f, 6f, outcome);
                    if (outcome == "none") return End("dark");
                    if (outcome == StorylineMath.Declined) return End("declined");
                    return End(StorylineMath.Faded);
                case "engineer":
                    if (outcome == "worked" || outcome == "short") return To("case", 2f, 6f, outcome);
                    if (outcome == "harmed") return End("blamed");
                    if (outcome == StorylineMath.Declined) return End("noengineer");
                    return End(StorylineMath.Faded);
                case "case":
                    if (outcome == "opened") return End("opened");
                    if (outcome == "returned") return End("favour");
                    if (outcome == "lost") return End("lostcase");
                    if (outcome == StorylineMath.Declined) return End("refusedcase");
                    return End(StorylineMath.Faded);
            }
            return End(StorylineMath.Faded);
        }

        // ================= The numbers =================

        /// <summary>Components asked for: six to twelve, by the colony's size.</summary>
        public static int Components(int colonists) => Clamp(4 + colonists, 6, 12);

        /// <summary>Days to have them ready, once agreed.</summary>
        public const float CellsMinDays = 6f, CellsMaxDays = 9f;

        /// <summary>What the vault pays: their worth and two fifths more (the vault's own mission's pay); part of them, their plain worth.</summary>
        public static int Pay(int count, float unit) => Math.Max(60, StorylineMath.Round10(Math.Max(0, count) * Safe(unit) * 1.4f));

        public static int PartPay(int count, float unit) => Math.Max(10, StorylineMath.Round10(Math.Max(0, count) * Safe(unit)));

        public static string CellsOutcome(int have, int count) => have >= count ? "sent" : have * 2 >= count ? "partly" : "none";

        /// <summary>Days the engineer stays and works.</summary>
        public const float EngineerMinDays = 4f, EngineerMaxDays = 6f;

        /// <summary>Hours of work at the bench each day, and a component used for each day's work.</summary>
        public const float WorkHoursPerDay = 4f;

        public const int ComponentsPerDay = 1;

        /// <summary>"worked" (every day's work done with a component), "short" (some days not).</summary>
        public static string EngineerOutcome(int days, int short_) => short_ == 0 && days > 0 ? "worked" : "short";

        /// <summary>Days the case is in the colony's care before their runner comes back for it, and hours before the card asks.</summary>
        public const float CaseMinDays = 5f, CaseMaxDays = 7f, CardAfterHours = 12f;

        /// <summary>What an opened case holds, by <paramref name="roll"/>: a sleeper, a machine, or old goods.</summary>
        public static string Contents(float roll)
        {
            roll = Safe(roll);
            return roll < 0.35f ? "sleeper" : roll < 0.65f ? "machine" : "goods";
        }

        /// <summary>The sleeper joins the colony this often; else they run.</summary>
        public const float SleeperJoins = 0.5f;

        /// <summary>The machine: this share of the threat the storyteller would send now.</summary>
        public const float MachineShare = 0.35f;

        /// <summary>Old goods worth this much: by the colony's wealth, 250 to 700 silver.</summary>
        public static float GoodsWorth(float wealthFactor) => Clamp(400f * Clamp(Safe(wealthFactor), 0.6f, 2f), 250f, 700f);

        /// <summary>Goodwill with the vault: opened, handed back.</summary>
        public const int OpenedGoodwill = -15, ReturnedGoodwill = 10;

        /// <summary>The favour the vault owes the colony for the case handed back sealed (the missions' deed, its size).</summary>
        public const int FavourWeight = 15;
    }
}
