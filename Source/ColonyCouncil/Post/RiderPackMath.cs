using System;

namespace ColonyCouncil
{
    /// <summary>How far the rider's supplies reach: with some to spare, the road only, short of it, or nothing at all.</summary>
    public enum RiderPackCover
    {
        Enough,
        NoMargin,
        Short,
        None
    }

    /// <summary>What the colony's own rider would set out with, against the road (for <see cref="RiderPackMath.Judge"/>).</summary>
    public struct RiderPackFacts
    {
        /// <summary>The road there in days, by the game's own caravan reckoning for the rider (and their beast) with this load.</summary>
        public float oneWayDays;

        /// <summary>The rider's nourishment a day; the food packed (with what they carry already) they may eat on the road; the most there is to hand.</summary>
        public float perDay, food, foodToHand;

        /// <summary>A beast goes with them: what it eats a day, whether it grazes, the share of the trip the land gives no grazing.</summary>
        public bool beast, grazes;

        public float beastPerDay, noGrazing;

        /// <summary>The beast's feed packed, and the most there is to hand.</summary>
        public float feed, feedToHand;

        /// <summary>What they would carry in all (gear, what they carry already and the supplies), against their room (the rider's and a beast's that carries).</summary>
        public float load, room;

        /// <summary>Medicine packed, and whether the stores had any to spare.</summary>
        public int medicine;
    }

    /// <summary>How the rider's supplies stand against the road (<see cref="RiderPackMath.Judge"/>).</summary>
    public struct RiderPackVerdict
    {
        /// <summary>There, a while at the town, and back.</summary>
        public float tripDays;

        /// <summary>How many days the food lasts the rider, and how far that is.</summary>
        public float foodDays;

        public RiderPackCover food;

        /// <summary>The days the beast needs feed (those it can't graze), how many the feed lasts it, and how far that is.</summary>
        public float feedNeedDays, feedDays;

        public RiderPackCover feed;

        /// <summary>More than they can carry: the game's caravan wouldn't move at all.</summary>
        public bool over;

        /// <summary>The stores can't cover the road (the food, the feed), whatever is packed.</summary>
        public bool storesShortFood, storesShortFeed;

        public bool noMedicine;

        /// <summary>Short of the road, the rider's food or the beast's feed.</summary>
        public bool Short => food >= RiderPackCover.Short || feed >= RiderPackCover.Short;
    }

    /// <summary>
    /// The colony's own rider's supplies for the road (the player, 10 November 2026: "sending a colonist off to send a letter
    /// makes them just go off map without any supplies or preparation. supply selection should be part of the initial
    /// decision"): food for there and back by the journey planner's rules (JourneyMath: the days with a quarter more and a
    /// spare day), a little medicine, the beast's feed for the days it can't graze, and whatever else the player adds; how
    /// far it reaches; when the send is greyed (the road not covered, unless the player lets them go short knowingly; more
    /// than they can carry, always). Pure (no game types), so Tools/Tests can check it.
    /// </summary>
    public static class RiderPackMath
    {
        /// <summary>At the town they wait for the answer this share of a reply's days (Post/OwnRiders.cs, Arrived).</summary>
        public const float TownShare = 0.5f;

        /// <summary>The wait at the town for the answer, by the roll the rider's arrival makes.</summary>
        public static float TownWaitDays(float roll) => NotableMath.ReplyDays(roll) * TownShare;

        /// <summary>The longest wait at the town: the supplies are packed for it.</summary>
        public static float TownDays => TownWaitDays(1f);

        /// <summary>The trip the supplies are for: the road there, the longest wait at the town, and the road back.</summary>
        public static float TripDays(float oneWayDays) => oneWayDays <= 0f ? 0f : 2f * oneWayDays + TownDays;

        /// <summary>The rider's food for the trip: the journey planner's (the days, a quarter more and a spare day).</summary>
        public static float FoodWanted(float perDay, float tripDays) => JourneyMath.FoodNutrition(perDay, tripDays);

        /// <summary>The days the beast needs feed: every day for one that doesn't graze, else the share the land gives no grazing.</summary>
        public static float FeedNeedDays(float tripDays, bool grazes, float noGrazing) =>
            tripDays <= 0f ? 0f : tripDays * (grazes ? Math.Max(0f, Math.Min(1f, noGrazing)) : 1f);

        /// <summary>The beast's feed for the trip, with the same margin as the food.</summary>
        public static float FeedWanted(float perDay, float tripDays, bool grazes, float noGrazing) =>
            JourneyMath.FoodNutrition(perDay, FeedNeedDays(tripDays, grazes, noGrazing));

        /// <summary>A little medicine: the journey planner's for one (one, and one more for every eight days on the road).</summary>
        public static int MedicineWanted(float tripDays) => JourneyMath.Medicine(1, tripDays);

        /// <summary>How far <paramref name="have"/> reaches an eater of <paramref name="perDay"/> over <paramref name="needDays"/>.</summary>
        public static RiderPackCover Cover(float have, float perDay, float needDays)
        {
            if (needDays <= 0f || perDay <= 0f) return RiderPackCover.Enough;
            if (have <= 0f) return RiderPackCover.None;
            switch (JourneyMath.Covers(JourneyMath.Lasts(have, perDay), needDays))
            {
                case JourneyMath.Cover.Margin: return RiderPackCover.Enough;
                case JourneyMath.Cover.Road: return RiderPackCover.NoMargin;
                default: return RiderPackCover.Short;
            }
        }

        /// <summary>The supplies against the road: how far the food and the feed reach, whether the stores could cover it, and the load.</summary>
        public static RiderPackVerdict Judge(RiderPackFacts f)
        {
            var v = new RiderPackVerdict { tripDays = TripDays(f.oneWayDays) };
            v.foodDays = JourneyMath.Lasts(f.food, f.perDay);
            v.food = Cover(f.food, f.perDay, v.tripDays);
            v.storesShortFood = f.perDay > 0f && v.tripDays > 0f && f.foodToHand < f.perDay * v.tripDays - 0.01f;
            if (f.beast)
            {
                v.feedNeedDays = FeedNeedDays(v.tripDays, f.grazes, f.noGrazing);
                v.feedDays = JourneyMath.Lasts(f.feed, f.beastPerDay);
                v.feed = Cover(f.feed, f.beastPerDay, v.feedNeedDays);
                v.storesShortFeed = f.beastPerDay > 0f && v.feedNeedDays > 0f && f.feedToHand < f.beastPerDay * v.feedNeedDays - 0.01f;
            }
            v.over = f.room <= 0f ? f.load > 0f : f.load > f.room + 0.01f;
            v.noMedicine = f.medicine <= 0;
            return v;
        }

        /// <summary>
        /// The rider may be sent: never with more than they can carry (the caravan couldn't move); short of the road only when
        /// the player lets them go short knowingly.
        /// </summary>
        public static bool MaySend(RiderPackVerdict v, bool knowingly) => !v.over && (!v.Short || knowingly);

        /// <summary>Why the send is greyed (a key), or null: too much to carry, the beast's feed short, the stores short, the supplies short.</summary>
        public static string WhyKey(RiderPackVerdict v, bool knowingly)
        {
            if (v.over) return "CC_RiderPack_Why_Over";
            if (knowingly || !v.Short) return null;
            if (v.food >= RiderPackCover.Short) return v.storesShortFood ? "CC_RiderPack_Why_Stores" : "CC_RiderPack_Why_Short";
            return "CC_RiderPack_Why_Feed";
        }

        /// <summary>The line on the food (a key): with some to spare, the road only, short, none packed.</summary>
        public static string FoodKey(RiderPackCover c)
        {
            switch (c)
            {
                case RiderPackCover.Enough: return "CC_RiderPack_Food_Enough";
                case RiderPackCover.NoMargin: return "CC_RiderPack_Food_NoMargin";
                case RiderPackCover.Short: return "CC_RiderPack_Food_Short";
                default: return "CC_RiderPack_Food_None";
            }
        }

        /// <summary>The line on the beast's feed (a key): it grazes all the way, enough, the road only, short, none packed.</summary>
        public static string FeedKey(RiderPackCover c, float needDays)
        {
            if (needDays <= 0f) return "CC_RiderPack_Feed_Graze";
            switch (c)
            {
                case RiderPackCover.Enough: return "CC_RiderPack_Feed_Enough";
                case RiderPackCover.NoMargin: return "CC_RiderPack_Feed_NoMargin";
                case RiderPackCover.Short: return "CC_RiderPack_Feed_Short";
                default: return "CC_RiderPack_Feed_None";
            }
        }

        /// <summary>
        /// The message as they set out (a key): food for the road with some to spare or just the road, short of it, or none. By
        /// the game's own days of food in the caravan against the trip; 0 or less when the trip isn't known.
        /// </summary>
        public static string OutKey(float foodDays, float tripDays)
        {
            if (tripDays <= 0f) return "CC_Mail_Msg_RiderOut";
            if (foodDays <= 0.05f) return "CC_Mail_Msg_RiderOutNoFood";
            return foodDays >= tripDays - 0.01f ? "CC_Mail_Msg_RiderOutFed" : "CC_Mail_Msg_RiderOutShort";
        }

        /// <summary>Days in words for the supplies (the planner's: one decimal under ten).</summary>
        public static string Days(float d) => float.IsPositiveInfinity(d) || d >= 99f ? "99+" : Math.Max(0f, d).ToString(d < 10f ? "0.#" : "0");

        /// <summary>A count stepped by the player: never past what is to hand (a count already over stays), never below none.</summary>
        public static int Step(int count, int step, int toHand) => JourneyMath.StepCount(count, step, toHand);
    }
}
