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
            ColonyNews(s, f);
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

        /// <summary>
        /// The colony's own news of many colonies as the world hears it, lately (FactsMath.LatelyDays): colony:founded when the
        /// home the scene is at was founded from another of the colony's homes ({MOTHERCOLONY} its mother); colony:handedover
        /// when the colony handed a home over to its own people and those spoken or written to are another people
        /// ({HANDEDOVER} the colony it was, {HANDEDPEOPLE} the people it became); colony:tookback when a town the colony was
        /// came back to it by its people's word, and those spoken to are another people ({TOOKBACK} the town). Stage 3's
        /// content pass (Rimfolk_OutsiderTalks_StageThreeNews.xml, the head's letter).
        /// </summary>
        private static void ColonyNews(FactScene s, FactSet f)
        {
            var home = s.colony != null ? WorldLedger.ColonyRecord(Homes.KeyOf(s.colony)) : null;
            if (home != null && home.Standing && home.mother != HomesMath.None && home.founded >= 0 && !home.motherName.NullOrEmpty()
                && Days(home.founded) <= FactsMath.LatelyDays)
            {
                f.Tag("colony:founded", Fresh(Days(home.founded), FactsMath.LatelyDays, 1.5f));
                f.Value("MOTHERCOLONY", home.motherName);
            }
            LedgerFormer handed = null, back = null;
            foreach (var r in WorldLedger.Formers)
            {
                // Their own town is town:former's; a people let go once its town came back is none of those spoken to.
                if (r == null || r.colonyName.NullOrEmpty() || r.people != null && r.people == s.people) continue;
                if (r.people != null && r.end == TownEnd.None && r.handedAt >= 0 && Days(r.handedAt) <= FactsMath.LatelyDays
                    && (handed == null || r.handedAt > handed.handedAt)) handed = r;
                if (r.end == TownEnd.TakenBack && r.endedAt >= 0 && Days(r.endedAt) <= FactsMath.LatelyDays && (back == null || r.endedAt > back.endedAt)) back = r;
            }
            if (handed != null)
            {
                f.Tag("colony:handedover", Fresh(Days(handed.handedAt), FactsMath.LatelyDays, 1.5f));
                f.Value("HANDEDOVER", handed.colonyName);
                f.Value("HANDEDPEOPLE", handed.people.Name);
            }
            if (back != null)
            {
                f.Tag("colony:tookback", Fresh(Days(back.endedAt), FactsMath.LatelyDays, 1.5f));
                f.Value("TOOKBACK", TownsAfter.TownName(back));
            }
        }
    }
}
