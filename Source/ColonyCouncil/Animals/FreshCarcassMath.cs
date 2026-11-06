namespace ColonyCouncil
{
    /// <summary>
    /// When an animal's carcass that hasn't started to rot counts as lying out (the player's request, 6 November 2026: "can
    /// i have the option to be notified when an animal corpse that's not rotting lays around?"). The pure rule behind
    /// <see cref="Alert_FreshCarcass"/>.
    /// </summary>
    public static class FreshCarcassMath
    {
        /// <summary>An hour after the death: a hunter carries a kill in long before, so a fresh kill doesn't flash up.</summary>
        public const int LyingTicks = 2500;

        /// <summary>
        /// An animal's carcass, still fresh, seen (not under fog), not in a stockpile or on a shelf, nobody of the colony
        /// already on their way to it, and dead an hour or more.
        /// </summary>
        public static bool LyingOut(bool animal, bool fresh, bool fogged, bool stored, bool claimed, int ageTicks) =>
            animal && fresh && !fogged && !stored && !claimed && ageTicks >= LyingTicks;
    }
}
