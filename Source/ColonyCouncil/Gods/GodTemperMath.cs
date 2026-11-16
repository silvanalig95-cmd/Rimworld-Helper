using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of a god's temper as the keeper of the colony's book reads it (the player, 16 November 2026: "the book
    /// should track the offerings and in this case also the god's favour should be tracked in the book, but the wording
    /// should also be kept a bit ominous, so not with a numeric value or so"). The reading is the keeper's, from what the
    /// colony saw: late (a step at a time, days apart) and sometimes wrong (a step off, more so when nothing of the god was
    /// seen lately, and then said as "some say"); never a number, in five bands of words (angry, cold, silent, warm, glad).
    /// It reads the god's standing in its seven words (GodsMath.Word: 0 wrathful to 6 beloved). Free of game types.
    /// </summary>
    public static class GodTemperMath
    {
        /// <summary>The seven steps a reading moves along (a standing's words, wrathful to beloved), and the one it starts from: nothing known.</summary>
        public const int Steps = 7, Unknown = 2;

        /// <summary>The bands the words come in: angry, cold, silent, warm, glad (each god's temper lines, in this order).</summary>
        public const int Bands = 5;

        /// <summary>A step's band: wrathful angry, displeased cold, unknown silent, noticed and pleased warm, favoured and beloved glad.</summary>
        public static int Band(int step)
        {
            int s = Clamp(step);
            return s <= 0 ? 0 : s == 1 ? 1 : s == 2 ? 2 : s <= 4 ? 3 : 4;
        }

        /// <summary>The keeper reads a god again this many days after the last at the least, and up to <see cref="ReadMoreDays"/> more (the same for the same god and reading).</summary>
        public const float ReadDays = 4f, ReadMoreDays = 4f;

        public static bool ReadNow(float daysSince, float more01) => daysSince >= ReadDays + ReadMoreDays * WorshipMath.Clamp01(more01);

        /// <summary>Something of the god seen in this many days (a gift taken or found gone, a doing in the book) is something to read it by.</summary>
        public const float SignDays = 12f;

        /// <summary>A reading is a step off this often with something to read it by, and this often with nothing.</summary>
        public const float WrongChance = 0.15f, WrongUnseen = 0.35f;

        /// <summary>
        /// The keeper's next reading: from the last (<paramref name="shown"/>; -1 for none, read from nothing known) one step
        /// toward the god's true standing (<paramref name="truth"/>), or toward a step beside it when they misread it
        /// (<paramref name="rollWrong"/>; <paramref name="rollSide"/> which side). Unsure, said as some say, when there was
        /// nothing to read it by.
        /// </summary>
        public static (int step, bool unsure) Read(int shown, int truth, bool signs, float rollWrong, float rollSide)
        {
            int t = Clamp(truth), from = shown < 0 ? Unknown : Clamp(shown);
            int target = t;
            if (rollWrong < (signs ? WrongChance : WrongUnseen))
            {
                int side = rollSide < 0.5f ? -1 : 1;
                target = t + side;
                if (target < 0 || target >= Steps) target = t - side;
            }
            return (from + Math.Sign(target - from), !signs);
        }

        /// <summary>A reading that rose into warm or glad says what was given since, when a gift was taken within this many days.</summary>
        public const float SinceDays = 15f;

        public static bool SaysSince(int band, int before, int after) => band >= 3 && after >= before;

        public static int Clamp(int step) => Math.Max(0, Math.Min(Steps - 1, step));
    }
}
