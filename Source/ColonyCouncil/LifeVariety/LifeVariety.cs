namespace ColonyCouncil
{
    /// <summary>
    /// Colony life's variety, part 2 (the content update, the player's go of 20 November 2026): its passes. Once a day the
    /// seats' own work sets people on the seats' life goals (A16, <see cref="SeatSparks"/>); once an hour and between the
    /// hours, the giving day under way (A26, <see cref="GivingDay"/>).
    /// </summary>
    public static class LifeVariety
    {
        /// <summary>The day's second half (CouncilDynamics.DailySecond).</summary>
        public static void Daily(CouncilGameComponent comp)
        {
            LifeVarietyBook.Safe("the seats' sparks", () => SeatSparks.Daily(comp));
        }

        /// <summary>The council's hour of life (CouncilGameComponent.HourlyLife).</summary>
        public static void Hourly(CouncilGameComponent comp)
        {
            var book = comp?.lifeVariety;
            if (book?.giving != null) LifeVarietyBook.Safe("the giving day", () => GivingDay.Hourly(book));
        }

        /// <summary>Every 250 ticks with the answers' own pass (PlayOutTracker): the giving day's walks, while one is under way.</summary>
        public static void Tick(int now)
        {
            var book = LifeVarietyBook.Instance;
            if (book?.giving == null) return;
            LifeVarietyBook.Safe("the giving day's steps", () => GivingDay.Step(book, now));
        }
    }
}
