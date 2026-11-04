using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The facts of pacts and truces (docs/design/stage2-diplomacy.md, item 2), read by the ties' provider (FactsLedger.cs).
    public static partial class Facts
    {
        /// <summary>
        /// A pact or a truce between the colony and the scene's people (the one at the speaker's own home first, else the
        /// newest): tie:pact or tie:truce (more current when new), :ours or :theirs (who offered it), :due (a pact's call under
        /// way either way, a truce's end near), and the people it is with ({PACTWITH}, {TRUCEWITH}). A truce still to be sworn
        /// at the gate isn't spoken of yet. Only reads.
        /// </summary>
        private static void PactTies(FactScene s, FactSet f, int now)
        {
            if (s?.people == null || s.people.IsPlayer) return;
            var people = Refs.Of(s.people);
            var home = s.speaker != null && KinMemory.Ours(s.speaker) ? Homes.Of(s.speaker) : s.colony;
            float day = now / (float)PactMath.DayTicks;
            foreach (bool pact in new[] { true, false })
            {
                Tie best = null;
                foreach (var t in WorldLedger.OwnTies(pact ? Tie.Kind.Pact : Tie.Kind.Truce))
                {
                    if (t == null || !t.Live || t.origin != (pact ? PactMath.PactOrigin : PactMath.TruceOrigin) || !t.Involves(people)) continue;
                    if (!pact && !Truces.Sworn(t)) continue;
                    if (t.from.kind != RefKind.Colony && t.to.kind != RefKind.Colony) continue;
                    bool atHome = home != null && (pact ? Pacts.HomeOf(t) : Truces.HomeOf(t)) == home;
                    bool bestAtHome = best != null && home != null && (pact ? Pacts.HomeOf(best) : Truces.HomeOf(best)) == home;
                    if (best == null || atHome && !bestAtHome || atHome == bestAtHome && t.since > best.since) best = t;
                }
                if (best == null) continue;
                float days = best.since >= 0 && best.since <= now ? (now - best.since) / (float)GenDate.TicksPerDay : float.MaxValue;
                bool due = pact ? Pacts.CallUnderWay(best) : PactMath.Due(best.terms.number, day);
                foreach (var (tag, heat) in PactMath.Tags(pact, best.from.kind == RefKind.Colony, due, days)) f.Tag(tag, heat);
                f.Value(PactMath.WithKey(pact), s.people.Name);
            }
        }
    }
}
