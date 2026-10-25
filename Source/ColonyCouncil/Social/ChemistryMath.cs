using System;

namespace ColonyCouncil
{
    /// <summary>
    /// How two people get on (Social/Chemistry.cs): the arithmetic, from the game's own compatibility between them (a
    /// fixed number for each pair, from about -3 to 4, most between -1 and 2, lower with a big age gap). Time together
    /// doesn't make everyone friends: what passes between two who get on easily counts for more, and between two who
    /// don't click for less, with their frictions felt more; and after they have lived side by side a while, the way
    /// they get on is a lasting feeling of its own. No game types, so Tools/Tests checks it.
    /// </summary>
    public static class ChemistryMath
    {
        /// <summary>The stages of the lasting feeling (Council_Chemistry): three for two who don't click, two for two who get on.</summary>
        public const int NoClick = 0, WrongWay = 1, OnNerves = 2, EasyCompany = 3, SameWavelength = 4, None = -1;

        /// <summary>Compatibility at or below which two don't click, rub each other the wrong way, get on each other's nerves.</summary>
        public const float PoorAt = -0.6f, WorseAt = -1.2f, WorstAt = -1.8f;

        /// <summary>Compatibility at or above which two are easy company, and on the same wavelength.</summary>
        public const float GoodAt = 1.5f, BestAt = 2.5f;

        /// <summary>Days lived side by side before each stage can show: the first, the second, the third.</summary>
        public const int FirstDays = 5, SecondDays = 15, ThirdDays = 30;

        /// <summary>
        /// The lasting feeling between two who have lived side by side for <paramref name="daysTogether"/> days, or
        /// <see cref="None"/>: it deepens with time, as far as their compatibility goes.
        /// </summary>
        public static int Stage(float compatibility, int daysTogether)
        {
            if (daysTogether < FirstDays) return None;
            int time = daysTogether >= ThirdDays ? 3 : daysTogether >= SecondDays ? 2 : 1;
            if (compatibility <= PoorAt)
            {
                int depth = compatibility <= WorstAt ? 3 : compatibility <= WorseAt ? 2 : 1;
                return NoClick + Math.Min(depth, time) - 1;
            }
            if (compatibility >= GoodAt)
            {
                int depth = compatibility >= BestAt ? 2 : 1;
                return EasyCompany + Math.Min(depth, time) - 1;
            }
            return None;
        }

        /// <summary>
        /// How much a good word between them counts (its opinion, as it's given): a third of it between two who
        /// really don't click, all of it from a compatibility of 0.5, a little more between two who get on easily.
        /// </summary>
        public static float WarmFactor(float c) => Curve(c, -1.5f, 0.35f, -0.5f, 0.65f, 0.5f, 1f, 1.5f, 1.15f);

        /// <summary>How much a cross word between them stings (its opinion, as it's given): more between two who don't click, less between two who get on.</summary>
        public static float SoreFactor(float c) => Curve(c, -1.5f, 1.35f, -0.5f, 1.15f, 0.5f, 1f, 1.5f, 0.85f);

        /// <summary>How likely one of Rimfolk's warm moments, or a friction, comes up between them, against an ordinary pair.</summary>
        public static float MomentFactor(float c, bool friction) =>
            friction ? Curve(c, -1.5f, 2f, -0.5f, 1.4f, 0.5f, 1f, 1.5f, 0.6f) : Curve(c, -1.5f, 0.4f, -0.5f, 0.7f, 0.5f, 1f, 1.5f, 1.2f);

        /// <summary>A memory's opinion as it's given between them: a good one by <see cref="WarmFactor"/>, a bad one by <see cref="SoreFactor"/>.</summary>
        public static float Scaled(float opinion, float c) => opinion > 0f ? opinion * WarmFactor(c) : opinion < 0f ? opinion * SoreFactor(c) : 0f;

        /// <summary>Straight lines between four points, flat beyond the ends.</summary>
        private static float Curve(float x, float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3)
        {
            if (x <= x0) return y0;
            if (x <= x1) return Lerp(y0, y1, (x - x0) / (x1 - x0));
            if (x <= x2) return Lerp(y1, y2, (x - x1) / (x2 - x1));
            if (x <= x3) return Lerp(y2, y3, (x - x2) / (x3 - x2));
            return y3;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
