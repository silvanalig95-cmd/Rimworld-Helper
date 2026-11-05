using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The facts of house renown (docs/design/stage2-diplomacy.md, item 12), read by the ties' provider (FactsLedger.cs), so
    // talk and letters can name a house by its renown.
    public static partial class Facts
    {
        /// <summary>
        /// Of the colonist speaking (or writing): one of a house of the colony (house:ours, {HOUSE} its name), known in a
        /// town (house:known), the region (house:renowned) or among the peoples in reach (house:wide), or for the wrong things
        /// (house:infamous); known for something (house:knownfor:KIND, {KNOWNFOR} in the house's own age's words), and how far
        /// it is known by the ways of the one spoken to ({RENOWN}). Of the one spoken to: the head of a house of theirs the
        /// colony knows (them:house, {THEIRHOUSE}), renowned (them:house:renowned), holding a grudge against the colony or
        /// about one of the speaker's house (them:house:feud). Only reads; nothing when renown is off.
        /// </summary>
        private static void RenownFacts(FactScene s, FactSet f)
        {
            if (!Renown.On || s == null) return;
            var c = s.speaker;
            var ours = c != null && KinMemory.Ours(c) ? Renown.HouseOf(c) : null;
            var r = Renown.Read(ours);
            if (ours != null && r != null)
            {
                foreach (var t in RenownMath.OurTags(r.band, r.known)) f.Tag(t, t == RenownMath.OursTag ? 1f : 1.5f);
                f.Value(RenownMath.HouseKey, RenownWords.Name(ours));
                string known = r.band >= RenownBand.Town ? RenownWords.For(r.known, RenownWords.AgeOf(ours)) : null;
                if (known != null) f.Value(RenownMath.KnownForKey, known);
                f.Value(RenownMath.RenownKey, RenownWords.Band(r.band, s.people != null ? Ages.Of(s.people) : Ages.Ours));
            }
            var x = s.listener;
            var n = x != null ? Outsiders.NotableOf(x) : null;
            if (n == null) return;
            foreach (var h in WorldLedger.Instance.Houses)
            {
                if (h == null || h.ours || h.head.IsNone || h.head != n.person) continue;
                f.Tag(RenownMath.TheirsTag);
                f.Value(RenownMath.TheirHouseKey, RenownWords.Name(h));
                var tr = Renown.Read(h);
                if (tr != null && tr.band >= RenownBand.Region) f.Tag(RenownMath.TheirsRenownedTag, 1.5f);
                break;
            }
            // A feud: a grudge the one spoken to holds against the colony, or one of their people's about the speaker's house.
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Grudge))
            {
                if (t == null || !t.Live) continue;
                bool theirs = t.from == n.person;
                bool aboutUs = ours != null && t.terms != null && ours.folk.Contains(t.terms.about) && n.faction != null && (t.from == Refs.Of(n.faction) || t.from == n.person);
                if (!theirs && !aboutUs) continue;
                f.Tag(RenownMath.FeudTag, 2f);
                break;
            }
        }
    }
}
