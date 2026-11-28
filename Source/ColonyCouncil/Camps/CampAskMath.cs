using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The camps' asks, the pure part (Camps/CampAsks.cs, CampRequests.cs, CampCarry.cs): what a camp pays out of its
    /// trader's purse, and what of goods the colony bought is set out for it when the one carrying them over can't.
    /// </summary>
    public static class CampAskMath
    {
        /// <summary>
        /// Of silver a camp pays the colony, how much comes out of its trader's purse (<paramref name="have"/>): as much of it
        /// as the purse holds. The rest comes from the camp's own, so the whole sum is paid either way.
        /// </summary>
        public static int FromPurse(int have, int count) => Math.Max(0, Math.Min(have, count));

        /// <summary>
        /// Goods the colony paid for, on their way over in one of the camp's hands, when that carry is let go (the bearer
        /// down or gone, no way to the gate, too long about it, the camp leaving): how many are set out at the camp's edge
        /// for the colony. Not yet taken up, all of them (they would have been taken from the camp's stores); taken up, what
        /// is still in the bearer's hands and pack (what fell from their hands lies on the map already). Nothing for what
        /// wasn't paid for (a good turn, a gift, a sale paid on arrival).
        /// </summary>
        public static int SetOutWhenLetGo(bool paidFor, bool taken, int count, int stillWithThem) =>
            !paidFor || count <= 0 ? 0 : !taken ? count : Math.Max(0, Math.Min(count, stillWithThem));
    }
}
