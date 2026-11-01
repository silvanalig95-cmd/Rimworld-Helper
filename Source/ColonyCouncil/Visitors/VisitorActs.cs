using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What visitors do with the colony's land and things beyond their own work: the singer at the colony's instrument
    /// (VisitorMusic), hunters taking real game (VisitorHunt), mourners carrying their dead (Borne), guests in
    /// Hospitality's guest beds (VisitorBeds). The visit's own clock (VisitorTracker) calls these as it moves along.
    /// </summary>
    public static class VisitorActs
    {
        /// <summary>What they say at these moments: play, kill, dress.</summary>
        public const string LinesPool = "Rimfolk_VisitorActLines";

        /// <summary>Each look while they come or stay: their hunt watched, their dead taken up again if it was put down.</summary>
        public static void Step(VisitorTrip t)
        {
            VisitorHunt.Watch(t, true);
            Borne.Watch(t);
        }

        /// <summary>
        /// A hunt under way or a kill not yet dressed holds them past <paramref name="due"/>, for a few hours at most.
        /// </summary>
        public static bool Holds(VisitorTrip t, int now, int due) => VisitorHunt.Busy(t) && now - due < VisitorActsMath.HoldHours * VisitorMath.TicksPerHour;

        /// <summary>They're going in their own time (or at a jog): hunts broken off, their dead and their kill carried off, guest beds given back.</summary>
        public static void Going(VisitorTrip t, bool hurry)
        {
            if (t == null) return;
            VisitorHunt.StopHunts(t);
            Borne.CarryOff(t, hurry);
            VisitorBeds.GiveBack(t);
        }

        /// <summary>On their way off the map: anything of theirs put down on the way is taken up and carried off with them.</summary>
        public static void Leaving(VisitorTrip t, bool hurry) => Borne.CarryOff(t, hurry);

        /// <summary>The visit ended otherwise (hurt and gone, never came, taken in): the guest beds go back.</summary>
        public static void Ended(VisitorTrip t) => VisitorBeds.GiveBack(t);
    }
}
