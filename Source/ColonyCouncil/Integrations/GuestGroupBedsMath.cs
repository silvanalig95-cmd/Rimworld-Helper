using System;

namespace ColonyCouncil
{
    /// <summary>
    /// How big a Hospitality guest group may be, by the colony's guest beds (Integrations/GuestGroupBeds.cs): no more
    /// guests than free guest beds, so nobody in the group sleeps on the floor. With no guest bed at all, Hospitality's own
    /// rule stands (it asks whether to let them in, and can refuse guests until there are beds); with every guest bed
    /// taken by guests already here, no new group comes for now.
    /// </summary>
    public static class GuestGroupBedsMath
    {
        /// <summary>The group's size: <paramref name="wanted"/>, at most the free guest bed places; 0 means none come now.</summary>
        public static int Size(int wanted, int bedPlaces, int freePlaces)
        {
            if (wanted <= 0) return 0;
            if (bedPlaces <= 0) return wanted;
            return Math.Max(0, Math.Min(wanted, freePlaces));
        }
    }
}
