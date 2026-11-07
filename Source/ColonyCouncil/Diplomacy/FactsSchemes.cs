using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The facts of a scheme against the colony found out (Diplomacy/Schemes.cs), read by the ties' provider (FactsLedger.cs).
    public static partial class Facts
    {
        /// <summary>
        /// When one of the colony speaks with someone of a people whose scheme against the colony was found out (while the
        /// ledger keeps it, sixty days): tie:scheme:theirs:broken, the hotter the newer; and them:caught when the one spoken
        /// to was the one caught at it. A scheme not found out says nothing: the colony doesn't know of it.
        /// </summary>
        private static void SchemeFacts(FactScene s, FactSet f)
        {
            if (s?.people == null || s.people.IsPlayer || WorldLedger.Instance == null) return;
            int now = Find.TickManager?.TicksGame ?? 0;
            var listener = s.listener != null ? Refs.Of(s.listener) : Ref.None;
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Scheme))
            {
                if (!Schemes.Theirs(t) || t.state != Tie.State.Broken || Schemes.PeopleOf(t) != s.people) continue;
                bool fresh = t.closed >= 0 && now - t.closed < 10 * SchemeMath.DayTicks;
                f.Tag(SchemeMath.FoundOutTag, fresh ? 2f : 1f);
                if (!listener.IsNone && t.terms.about == listener) f.Tag(SchemeMath.CaughtTag, 2f);
            }
        }
    }
}
