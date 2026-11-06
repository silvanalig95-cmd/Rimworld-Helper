using Verse;

namespace ColonyCouncil
{
    // The region's provider (Text/Facts/FactsRegion.cs) asks here for the towns the colony was (Colonies/TownsAfter.cs), as
    // their records stand.
    public static partial class Facts
    {
        /// <summary>
        /// A town the colony was: town:former when their town is one the colony was, still its people's (current in its
        /// first year); them:former when the one spoken to was one of the colony's own and stayed there; {OLDCOLONY} the
        /// colony it was, {YEARSSINCE} the years since the hand-over in words ("two years", never a countdown).
        /// </summary>
        private static void FormerTags(FactScene s, FactSet f)
        {
            if (WorldLedger.Formers.Count == 0) return;
            var rec = s.place != null ? WorldLedger.FormerOf(s.place.ID) : null;
            if (rec == null && s.people != null)
            {
                rec = WorldLedger.FormerOf(s.people);
                // Someone of theirs from another town (Faction Territories' new towns): their people's, not their town's.
                if (rec != null && s.place != null && s.place.ID != rec.key) rec = null;
            }
            if (rec == null || !rec.Standing || rec.end != TownEnd.None) return;
            f.Tag("town:former", Fresh(Days(rec.handedAt), 60f, 1.5f));
            f.Value("OLDCOLONY", rec.colonyName ?? s.place?.Name ?? "");
            f.Value("YEARSSINCE", TownsAfter.SinceWords(rec.handedAt, Now));
            if (s.listener != null && rec.formers.Contains(s.listener)) f.Tag("them:former", 2f);
        }
    }
}
