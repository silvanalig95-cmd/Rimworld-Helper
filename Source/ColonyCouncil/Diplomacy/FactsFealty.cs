using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The facts of fealty and vassals (docs/design/stage2-diplomacy.md, items 9 and 10), read by the ties' provider
    // (FactsLedger.cs). The ties themselves speak through the ledger's own facts (tie:fealty, :ours for the colony's oath,
    // :theirs for a vassal's, :due for tribute owed, {FEALTYTO}).
    public static partial class Facts
    {
        /// <summary>
        /// With the scene's people: the colony sworn to them (fealty:oath, its weight higher for ten days), its levies sent to
        /// their call this season (fealty:levies and fealty:levies:sent) or refused (fealty:levies:refused), each by the
        /// history's own record of the call (a call still open says neither), their band come to protect it this season
        /// (fealty:protected), tribute owed (fealty:short); a town of theirs the colony's own vassal (vassal:town), under
        /// heavy tribute (vassal:heavy), its levies called this season (vassal:levy). Only reads.
        /// </summary>
        private static void FealtyFacts(FactScene s, FactSet f)
        {
            if (s?.people == null || s.people.IsPlayer || WorldLedger.Instance == null) return;
            int now = Find.TickManager?.TicksGame ?? 0;
            var t = Fealty.With(s.people);
            if (t != null)
            {
                f.Tag(FealtyMath.OathTag, t.since >= 0 && now - t.since < 10 * FealtyMath.DayTicks ? 2f : 1f);
                // Their calls this season, by how each ended: the levies came (their people For it) or were refused (Against).
                var them = Refs.Of(s.people);
                bool sent = false, refused = false;
                foreach (var e in History.Of(them, new[] { DiplomacyMath.Levy }, now - (int)(FealtyMath.SeasonDays * FealtyMath.DayTicks)))
                    FealtyMath.LevyOutcome(e.actors.Exists(a => a.who == them && a.role == HistoryRole.For), e.actors.Exists(a => a.who == them && a.role == HistoryRole.Against),
                        ref sent, ref refused);
                if (sent)
                {
                    f.Tag(FealtyMath.LeviesTag, 1.5f);
                    f.Tag(FealtyMath.LeviesSentTag, 1.5f);
                }
                if (refused) f.Tag(FealtyMath.LeviesRefusedTag, 1.5f);
                if (!Fealty.FailedSeason(t) && t.terms.days > 0f && FealtyMath.ThisSeason((int)(t.terms.days * FealtyMath.DayTicks), now)) f.Tag(FealtyMath.ProtectedTag, 1.5f);
                if (Fealty.Owed(t)) f.Tag(FealtyMath.ShortTag, 2f);
            }
            foreach (var (_, v) in Vassals.All())
            {
                if (v.people != s.people) continue;
                f.Tag(FealtyMath.VassalTag);
                if (v.level == TributeLevel.Heavy) f.Tag(FealtyMath.HeavyTag, 1.5f);
                if (FealtyMath.ThisSeason(v.leviesAt, now)) f.Tag(FealtyMath.VassalLevyTag, 1.5f);
            }
        }
    }
}
