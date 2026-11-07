using System;

namespace ColonyCouncil
{
    /// <summary>
    /// How big a Hospitality guest group may be, by the colony's guest beds (Integrations/GuestGroupBeds.cs): the free guest
    /// beds, and now and then one or two more (the player's words: "i don't mind if sometimes one or two too many visitors
    /// arrive, but not like four"). With no guest bed at all, Hospitality's own rule stands (it asks whether to let them
    /// in, and can refuse guests until there are beds); with every guest bed taken by guests already here, only a group of
    /// the extra comes, or none.
    /// </summary>
    public static class GuestGroupBedsMath
    {
        /// <summary>
        /// The group's size: <paramref name="wanted"/>, at most the free guest bed places and <paramref name="extra"/> more;
        /// 0 means none come now.
        /// </summary>
        public static int Size(int wanted, int bedPlaces, int freePlaces, int extra = 0)
        {
            if (wanted <= 0) return 0;
            if (bedPlaces <= 0) return wanted;
            return Math.Max(0, Math.Min(wanted, Math.Max(0, freePlaces) + Math.Max(0, extra)));
        }

        /// <summary>
        /// How many guests beyond the free beds this group may bring, from a roll in [0, 1): half the groups none, the
        /// rest one up to <paramref name="most"/>, evenly.
        /// </summary>
        public static int Extra(float roll, int most)
        {
            if (most <= 0 || roll < 0.5f) return 0;
            int n = 1 + (int)((roll - 0.5f) * 2f * most);
            return Math.Min(most, Math.Max(1, n));
        }
    }
}
