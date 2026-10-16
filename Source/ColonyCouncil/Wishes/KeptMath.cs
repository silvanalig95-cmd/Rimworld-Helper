using System;

namespace ColonyCouncil
{
    /// <summary>How long a colonist waits before a granted wish that has gone missing counts as taken back (the setting).</summary>
    public enum KeptGrace
    {
        /// <summary>No grace: they mind the next time the council looks.</summary>
        Off,
        /// <summary>Half a day.</summary>
        Short,
        /// <summary>A day (the default).</summary>
        Normal,
        /// <summary>Three days.</summary>
        Long
    }

    /// <summary>Where a watched wish stands.</summary>
    public enum KeptPhase
    {
        /// <summary>It's there, as the council gave it.</summary>
        Kept,
        /// <summary>It's gone, and the grace period is running.</summary>
        Missing,
        /// <summary>It stayed gone past the grace period: they've taken it as the council going back on its word.</summary>
        TakenBack
    }

    /// <summary>What a worker's check says about a granted lasting wish right now.</summary>
    public enum KeptCheck
    {
        /// <summary>Still there (or being moved, reinstalled or rebuilt).</summary>
        Holds,
        /// <summary>Gone.</summary>
        Gone,
        /// <summary>Can't tell now (they're away, their room is gone and the room's own watch covers that): nothing changes.</summary>
        Unclear,
        /// <summary>It no longer makes sense to watch (the partner left, the pet died, the mod was removed): stop, quietly.</summary>
        Over
    }

    /// <summary>What the watch does next.</summary>
    public enum KeptStep
    {
        Nothing,
        /// <summary>It has just gone missing: the grace period starts (and the player is told).</summary>
        StartGrace,
        /// <summary>It came back within the grace period: nothing happens.</summary>
        BackInTime,
        /// <summary>The grace period ran out with it still gone.</summary>
        TakeBack,
        /// <summary>It came back within a few days of being taken back: the council made it right.</summary>
        MadeRight,
        /// <summary>The watch ends: the colony has lived with it long enough, or the chance to make it right has passed.</summary>
        Close
    }

    /// <summary>
    /// Promises kept (Wishes/Kept.cs): the numbers behind watching a granted lasting wish, with no game types, so the
    /// tests can check the grace period and the timing: how long a wish is watched, how long the grace lasts, how much
    /// taking it back stings (more the sooner it happens), and what each check leads to.
    /// </summary>
    public static class KeptMath
    {
        public const int TicksPerHour = 2500;
        public const int TicksPerDay = 60000;

        /// <summary>The watched wishes are checked every this many hours, never every tick.</summary>
        public const int CheckHours = 2;

        /// <summary>How long a granted lasting wish is watched when its def doesn't say (rooms say more).</summary>
        public const float DefaultWatchDays = 60f;

        /// <summary>After being taken back, putting it back within this many days still makes it right.</summary>
        public const float AmendsDays = 5f;

        /// <summary>A plan or frame of the same thing being built counts as still there for at most this long.</summary>
        public const float MovingDays = 7f;

        /// <summary>For this long after a raid or other big threat, nothing goes missing: the colony gets time to rebuild.</summary>
        public const float ThreatDays = 2f;

        /// <summary>A thing destroyed by fire, a raid or a collapse in the last few days was nobody's choice: the watch ends.</summary>
        public const float MisfortuneDays = 3f;

        /// <summary>After it's taken back, they may bring the same wish up again this soon.</summary>
        public const float RenewDays = 2f;

        /// <summary>The mood's share when taken back at once, and at the very end of the watch: -6 down to -3.</summary>
        public const float MoodSoon = 1f;
        public const float MoodLate = 0.5f;

        /// <summary>Trust lost, the same as a broken promise (before the colonist's own temper).</summary>
        public const float TrustLoss = 18f;

        /// <summary>The share of the lost trust that comes back when the council makes it right.</summary>
        public const float TrustBackShare = 0.5f;

        /// <summary>
        /// Rooms: how impressive or roomy it must stay, as a share of what was asked for, before it counts as lost (a
        /// little dirt or a chair moved isn't taking it back).
        /// </summary>
        public const float RoomHoldShare = 0.85f;

        /// <summary>The grace period in ticks for the setting: none, half a day, a day, three days.</summary>
        public static int GraceTicks(KeptGrace grace)
        {
            switch (grace)
            {
                case KeptGrace.Off: return 0;
                case KeptGrace.Short: return TicksPerDay / 2;
                case KeptGrace.Long: return TicksPerDay * 3;
                default: return TicksPerDay;
            }
        }

        /// <summary>How long a wish is watched after it's granted, in ticks (the def's days, or the default).</summary>
        public static int WatchTicks(float days) => (int)((days > 0f ? days : DefaultWatchDays) * TicksPerDay);

        /// <summary>Whether this hour is one the watched wishes are checked on.</summary>
        public static bool CheckDue(int tick) => tick >= 0 && tick / TicksPerHour % CheckHours == 0;

        /// <summary>
        /// The share of the full "taken back" mood: all of it when taken back right after it was granted, half when the
        /// watch was nearly over.
        /// </summary>
        public static float MoodShare(int grantedTick, int takenTick, int watchTicks)
        {
            if (watchTicks <= 0) return MoodLate;
            float f = Clamp01((takenTick - grantedTick) / (float)watchTicks);
            return MoodSoon + (MoodLate - MoodSoon) * f;
        }

        /// <summary>
        /// How much of a stock must stay for it to count as kept: the wish's amount, or a share of it for stores that are
        /// meant to be used (medicine, healroot) or spent (silver). Always at least one.
        /// </summary>
        public static int Needed(float target, float share)
        {
            if (target <= 0f) return 0;
            if (share <= 0f || share >= 1f) return (int)Math.Ceiling(target - 0.001f);
            return Math.Max(1, (int)Math.Ceiling(target * share - 0.001f));
        }

        /// <summary>A room stat that must stay at <see cref="RoomHoldShare"/> of what was asked for.</summary>
        public static bool RoomHolds(float value, float target) => value >= target * RoomHoldShare;

        /// <summary>The trust given back when it's made right: half of what was lost, never more than was lost.</summary>
        public static float TrustBack(float lost) => Math.Max(0f, lost) * TrustBackShare;

        /// <summary>A plan or frame being built still counts as there: until it has been waiting <see cref="MovingDays"/>.</summary>
        public static bool PlanStillCounts(int plannedSince, int now) => plannedSince < 0 || now - plannedSince <= MovingDays * TicksPerDay;

        /// <summary>
        /// What one check leads to, given where the watch stands. While the grace runs, an unclear check (they're away)
        /// changes nothing; the caller pushes the end of the grace back by the time it couldn't look.
        /// </summary>
        public static KeptStep Next(KeptPhase phase, KeptCheck check, int now, int watchUntil, int graceUntil, int takenBackTick)
        {
            int amends = (int)(AmendsDays * TicksPerDay);
            switch (check)
            {
                case KeptCheck.Over:
                    return KeptStep.Close;
                case KeptCheck.Unclear:
                    // Nothing changes while it can't be told, but no watch outlives its time: away past the end of the
                    // watch (or past the chance to make it right), it's let go.
                    if (phase == KeptPhase.TakenBack) return now - takenBackTick > amends ? KeptStep.Close : KeptStep.Nothing;
                    if (phase == KeptPhase.Kept) return now >= watchUntil ? KeptStep.Close : KeptStep.Nothing;
                    return now >= watchUntil + amends ? KeptStep.Close : KeptStep.Nothing;
                case KeptCheck.Holds:
                    if (phase == KeptPhase.Missing) return KeptStep.BackInTime;
                    if (phase == KeptPhase.TakenBack) return now - takenBackTick <= amends ? KeptStep.MadeRight : KeptStep.Close;
                    return now >= watchUntil ? KeptStep.Close : KeptStep.Nothing;
                default:
                    if (phase == KeptPhase.Kept) return now >= watchUntil ? KeptStep.Close : KeptStep.StartGrace;
                    if (phase == KeptPhase.Missing) return now >= graceUntil ? KeptStep.TakeBack : KeptStep.Nothing;
                    return now - takenBackTick > amends ? KeptStep.Close : KeptStep.Nothing;
            }
        }

        private static float Clamp01(float f) => f < 0f ? 0f : f > 1f ? 1f : f;
    }
}
