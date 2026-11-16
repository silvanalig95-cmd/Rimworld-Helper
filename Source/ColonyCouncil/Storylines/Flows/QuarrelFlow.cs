using System;

namespace ColonyCouncil
{
    /// <summary>
    /// S7, two towns' quarrel (StorylineQuarrel.cs; docs/quests-proposals.md). Two peoples at peace with the colony and at
    /// odds with each other make a truce at the colony, with someone of the colony as their witness. One side leaves someone
    /// at the colony as a pledge for some days. Then fighters of the other side cross the colony's land to strike the first:
    /// the colony lets them pass (the two go to war), warns the first (the strike fails, the first's goodwill toward the
    /// other falls, and the colony is thanked by one and resented by the other), or stops them at the gate (the peace holds;
    /// a fight only if they force it).
    /// </summary>
    public sealed class QuarrelFlow : StorylineFlow
    {
        public const string Key = "quarrel";

        public override string Kind => Key;

        public override int Number => 7;

        public override string[] Parts => new[] { "truce", "pledge", "fighters" };

        public override string[] Endings => new[] { "declined", "nopeace", "blamed", "war", "warned", "peace", "fought", "gone", "faded" };

        public override string[] Outcomes(string part)
        {
            switch (part)
            {
                case "truce": return new[] { "witnessed", "missed" };
                case "pledge": return new[] { "kept", "poorly", "harmed" };
                case "fighters": return new[] { "passed", "warned", "stopped", "fought" };
            }
            return new string[0];
        }

        public override bool Failed(string outcome) => outcome == "missed" || outcome == "harmed";

        public override StorylineMath.Step Next(string part, string outcome, int times, float roll)
        {
            switch (part)
            {
                case "truce":
                    if (outcome == "witnessed") return To("pledge", 2f, 4f);
                    if (outcome == "missed") return End("nopeace");
                    if (outcome == StorylineMath.Declined) return End("declined");
                    return End(StorylineMath.Faded);
                case "pledge":
                    if (outcome == "kept" || outcome == "poorly") return To("fighters", 3f, 8f, outcome);
                    if (outcome == "harmed") return End("blamed");
                    // The colony would not keep the pledge: it stays with another town, and the truce goes on without it.
                    if (outcome == StorylineMath.Declined) return To("fighters", 3f, 8f, "nopledge");
                    return End(StorylineMath.Faded);
                case "fighters":
                    if (outcome == "warned") return End("warned");
                    if (outcome == "stopped") return End("peace");
                    if (outcome == "fought") return End("fought");
                    // Let pass, or the colony gave no answer: they strike, and the two go to war a day or two later.
                    if (outcome == "passed" || outcome == StorylineMath.Declined) return End("war", 1f, 2f);
                    return End(StorylineMath.Faded);
            }
            return End(StorylineMath.Faded);
        }

        public override bool EndingHasLetter(string ending) => ending == "war" || ending == StorylineMath.Gone;

        // ================= The numbers =================

        /// <summary>Two peoples at odds: at war with each other, or their goodwill below nothing.</summary>
        public static bool AtOdds(int goodwill, bool hostile) => hostile || goodwill < 0;

        /// <summary>Their goodwill toward each other after the truce: at least neutral, ten above where it was or nothing.</summary>
        public static int TruceGoodwill(int goodwill) => Math.Max(goodwill, 0) + 10 > 100 ? 100 : Math.Max(goodwill, 0) + 10;

        /// <summary>Goodwill with the colony for its witness, from each side.</summary>
        public const int WitnessGoodwill = 6;

        /// <summary>Hours the witness must spend with the two envoys for the truce to be made.</summary>
        public const float WitnessHours = 2f;

        /// <summary>Days the envoys wait at the colony for its witness.</summary>
        public const float TruceWaitDays = 1f;

        /// <summary>Days the pledge stays.</summary>
        public const float PledgeMinDays = 4f, PledgeMaxDays = 6f;

        /// <summary>"kept" (every day fed and housed), "poorly" (some days short).</summary>
        public static string PledgeOutcome(int good, int short_) => short_ == 0 && good > 0 ? "kept" : "poorly";

        /// <summary>The fighters: three to six, by the threat the storyteller would send now (about 100 points each).</summary>
        public static int Fighters(float points) => Clamp((int)Math.Round(Safe(points) / 120f), 3, 6);

        /// <summary>
        /// Stopped at the gate, they force their way through this often: likelier the fewer of the colony's fighters stand
        /// at home against them, never more than half the time or less than one time in twenty.
        /// </summary>
        public static float ForceChance(int ours, int theirs) => Clamp(0.15f + 0.08f * (theirs - ours), 0.05f, 0.5f);

        /// <summary>Hours they stand at the gate before they turn back or force it.</summary>
        public const float GateHours = 2f;

        /// <summary>The first side's goodwill toward the other when the colony warned them: their strike known, it falls by this.</summary>
        public const int WarnedLoss = 30;

        /// <summary>Let pass, the strike starts a war: their goodwill toward each other falls to this (the game's war is -75 and below).</summary>
        public const int WarGoodwill = -80;

        /// <summary>The change that brings goodwill <paramref name="from"/> to <paramref name="to"/>.</summary>
        public static int ChangeTo(int from, int to) => to - from;

        /// <summary>Goodwill with the colony at each ending: (the first side, the other side).</summary>
        public static (int first, int other) Thanks(string ending)
        {
            switch (ending)
            {
                case "warned": return (12, -10);
                case "peace": return (10, -5);
                case "fought": return (12, -8);
                case "blamed": return (-10, 0);
                case "war": return (-5, 0);
            }
            return (0, 0);
        }

        /// <summary>The pledge harmed in the colony's care: the truce weakens, their goodwill toward each other falls by this.</summary>
        public const int BlamedLoss = 20;
    }
}
