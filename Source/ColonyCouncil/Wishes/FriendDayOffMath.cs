namespace ColonyCouncil
{
    /// <summary>
    /// The rules of a day off for a friend (A9 of docs/content-update-proposals.md, Wishes/FriendDayOff.cs), without game
    /// types, so Tools/Tests can check them: who may ask, for whom, when, how often it is asked, and what follows.
    /// </summary>
    public static class FriendDayOffMath
    {
        /// <summary>The one it is for has stood worn down or near breaking (strain 55 or more) at least this many days running.</summary>
        public const float WornDays = 2f;

        /// <summary>
        /// A close friend asks: the heart's closeness to them (HeartCountMath.Closeness: a partner 1, kin 0.8, a friend by
        /// opinion 0.4 at 20 rising to 0.8 at 80) at least this, which for a friend by opinion is about 50.
        /// </summary>
        public const float CloseFriend = 0.6f;

        /// <summary>A kind colonist asks for anyone they don't think badly of: their opinion of them at least this.</summary>
        public const int KindOpinion = 0;

        /// <summary>How often the one who asked keeps them company for an hour of it ("often").</summary>
        public const float CompanyChance = 0.7f;

        /// <summary>How much closer the two grow in the social web (a friendship's strength, 0 to 100) when it is met.</summary>
        public const float Closer = 6f;

        /// <summary>
        /// Kept apart from colony life's first variety build (CouncilLife/): not asked for someone whose "worn thin" story ended
        /// within this many days, or a seat holder asked about stepping down within this many days.
        /// </summary>
        public const float ApartDays = 5f;

        /// <summary>Worn down long enough for a friend to notice and ask.</summary>
        public static bool WornLongEnough(float wornDays) => wornDays >= WornDays;

        /// <summary>Who may ask: a kind colonist who doesn't think badly of them, or anyone close to them.</summary>
        public static bool MayAsk(bool kind, float closeness, int opinion) => closeness >= CloseFriend || kind && opinion >= KindOpinion;

        /// <summary>The candidate's weight: the closer, the likelier; a kind asker a little more; near breaking, half again.</summary>
        public static float Weight(float closeness, bool kind, bool breaking) =>
            (0.6f + (closeness < 0f ? 0f : closeness > 1f ? 1f : closeness)) * (kind ? 1.2f : 1f) * (breaking ? 1.5f : 1f);

        /// <summary>
        /// Met: given the heart's day off since it was asked (or one under way), or resting on the calendar's rest day while it
        /// waits. <paramref name="lastDayOffTick"/> the heart's last day off, <paramref name="askedTick"/> when it was asked.
        /// </summary>
        public static bool Met(bool dayOffRunning, int lastDayOffTick, int askedTick, bool restingToday) =>
            dayOffRunning || lastDayOffTick >= askedTick || restingToday;

        /// <summary>Whether something last done at <paramref name="tick"/> (-1 never) was within <paramref name="days"/> of now.</summary>
        public static bool Within(int tick, int now, float days, int dayTicks) => tick >= 0 && now >= tick && now - tick < days * dayTicks;
    }
}
