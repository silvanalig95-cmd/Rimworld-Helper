using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Hospitality, read by type name. Its guests are other factions' pawns, so they never count as colonists, voice
    /// wishes or sit on the council (see <see cref="Strangers.IsHospitalityGuest"/> for how encounters treat them). What
    /// needs care is its guest beds: ordinary-looking beds that colonists can't sleep in.
    /// </summary>
    public static class GuestBeds
    {
        /// <summary>A bed switched to "for guests" (Hospitality turns it into a Building_GuestBed).</summary>
        public static bool IsGuestBed(Thing t) => t != null && HomeLife.ClassIs(t.GetType(), "Hospitality", "Building_GuestBed");
    }
}
