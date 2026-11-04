using Verse;

namespace ColonyCouncil
{
    // The region's provider (Text/Facts/FactsRegion.cs) asks here for the colony's hamlets (World/Region/Hamlet.cs), as
    // their records stand.
    public static partial class Facts
    {
        /// <summary>
        /// Hamlets: town:hamlet when their town is the colony's hamlet ({HAMLETWORD} what their age calls it; current in its
        /// first months), town:hamlet:sworn once it has sworn fealty; their:hamlet when their people keep a hamlet under the
        /// colony's protection and their town is another ({HAMLET}, {HAMLETWORD}); else region:hamlet for another people's
        /// hamlet near the colony ({HAMLET}, {HAMLETPEOPLE}).
        /// </summary>
        private static void HamletTags(FactScene s, FactSet f)
        {
            if (RegionBook.Instance == null) return;
            if (s.place != null)
            {
                var own = Hamlets.HamletAt(s.place.ID, out _);
                if (own != null)
                {
                    f.Tag("town:hamlet", Fresh(Days(own.founded), 60f, 1.5f));
                    f.Value("HAMLETWORD", HamletWords.Word(own.people));
                    if (own.stage == HamletStage.Sworn) f.Tag("town:hamlet:sworn", 1.5f);
                    return;
                }
            }
            var home = s.colony ?? s.speaker?.MapHeld;
            var h = home != null ? Hamlets.Peek(home) : null;
            if (h == null || !HamletMath.Stands(h.stage) || h.name.NullOrEmpty() || s.place != null && s.place.ID == h.town) return;
            bool theirs = s.people != null && h.people == s.people;
            f.Tag(theirs ? "their:hamlet" : "region:hamlet", Fresh(Days(h.founded), 60f, 1.2f));
            f.Value("HAMLET", h.name);
            if (theirs) f.Value("HAMLETWORD", HamletWords.Word(h.people));
            else if (h.people != null) f.Value("HAMLETPEOPLE", h.people.Name);
        }
    }
}
