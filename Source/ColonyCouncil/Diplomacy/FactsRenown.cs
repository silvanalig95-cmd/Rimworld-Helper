using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The facts of the colony's renown (docs/design/stage2-diplomacy.md, item 12, as reshaped 5 November 2026), read by the
    // ties' provider (FactsLedger.cs), so talk and letters can speak of the colony's name and of a colonist's own.
    public static partial class Facts
    {
        /// <summary>
        /// When one of the colony speaks (or writes): its name known in a town (colony:known), the region (colony:renowned) or
        /// among the peoples in reach (colony:wide), or for the wrong things (colony:infamous); known for something
        /// (colony:knownfor:KIND, {KNOWNFOR} in the colony's own age's words), and how far it is known by the ways of the one
        /// spoken to ({RENOWN}; the Empire's by the court's). The colonist speaking known by their own name to the people of
        /// the one spoken to (us:knownas, us:knownas:KIND, {KNOWNAS}: "the one who broke the raiders' camp that troubled the
        /// Lenlia"). Only reads; nothing when renown is off. Since 5 November 2026 nothing here speaks of houses: a grudge is
        /// their people's (tie:grudge) or against a colonist by name (you:foe).
        /// </summary>
        private static void RenownFacts(FactScene s, FactSet f)
        {
            if (!Renown.On || s == null) return;
            var c = s.speaker;
            if (c == null || !Renown.Ours(c)) return;
            var r = Renown.Read();
            if (r != null)
            {
                foreach (var t in RenownMath.ColonyTags(r.band, r.known)) f.Tag(t, 1.5f);
                string known = r.band >= RenownBand.Town ? RenownWords.For(r.known, Ages.Ours) : null;
                if (known != null) f.Value(RenownMath.KnownForKey, known);
                // In the ways of the one spoken to: the Empire's people by the court's, whatever their tech level.
                f.Value(RenownMath.RenownKey, RenownWords.Band(r.band, s.people != null ? Ages.Of(s.people) : Ages.Ours, Ages.IsEmpire(s.people)));
            }
            // The speaker's own name, where the one spoken to's people know it.
            var o = Renown.Own(c);
            if (o?.part == null || s.people == null) return;
            bool theyKnow = Renown.KnownAmong(o, s.people);
            var own = RenownMath.OwnTags(o.reading, theyKnow);
            if (own.Count == 0) return;
            foreach (var t in own) f.Tag(t, 2f);
            string name = RenownWords.KnownAs(o);
            if (name != null) f.Value(RenownMath.KnownAsKey, name);
        }
    }
}
