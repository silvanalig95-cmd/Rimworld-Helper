using System.Collections.Generic;
using RimWorld;

namespace ColonyCouncil
{
    /// <summary>
    /// The gods' short memory of their last happenings, kept with the gods on the map (GodsOnMapBook.lately), for the
    /// shared rules of GodPaceMath: what each happening took lately, so the next leans to a different form, and the story
    /// that comes back for a reason takes its next step.
    /// </summary>
    public static class GodPace
    {
        private static List<string> Lately => GodsOnMapBook.Instance?.lately;

        /// <summary>A happening came: what it was, the form it took, and whom or what it was about.</summary>
        public static void Note(string what, string form, string who = null) => GodPaceMath.Note(Lately, what, form, who);

        /// <summary>The forms a happening took lately, newest first (only those about <paramref name="who"/> when given).</summary>
        public static List<string> Forms(string what, string who = null) => GodPaceMath.Forms(Lately, what, who);

        /// <summary>Whole days since <paramref name="tick"/>; a long time for never (below -900000).</summary>
        public static int DaysSince(int tick, int now) => tick < -900000 ? 99999 : (now - tick) / GenDate.TicksPerDay;
    }
}
