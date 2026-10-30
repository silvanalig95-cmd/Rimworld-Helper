using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The numbers of the wardens' post as a trading stop (Camps/RoadPost.cs): what the carters pay for a load sent down
    /// the road, when they come back through, and the tags for the word they bring. Pure, so Tools/Tests can check it.
    /// </summary>
    public static class RoadPostMath
    {
        /// <summary>The carters' price: so much of the goods' market value, rounded to tens, ten silver at least.</summary>
        public const float PriceShare = 0.8f;
        /// <summary>The carters come back through the post, with the silver, after this many days.</summary>
        public const int DueDaysMin = 3, DueDaysMax = 5;
        /// <summary>How many lines of word from the road the wardens bring up to the gate at most.</summary>
        public const int WordLines = 3;

        public static int Price(int count, float marketValue)
        {
            if (count <= 0 || marketValue <= 0f) return 0;
            return Math.Max(10, (int)Math.Round(count * marketValue * PriceShare / 10.0) * 10);
        }

        /// <summary>The tags for a line of word from the road: the world event it is about ("ws:war"), or "quiet" when none runs.</summary>
        public static IEnumerable<string> WordTags(string situationKind)
        {
            if (string.IsNullOrEmpty(situationKind)) yield return "quiet";
            else yield return "ws:" + situationKind;
        }
    }
}
