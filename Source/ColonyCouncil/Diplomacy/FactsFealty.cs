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
        /// With the scene's people: the colony sworn to them and its levies called or sent this season (fealty:levies),
        /// their band come to protect it this season (fealty:protected), tribute owed (fealty:short); a vassal of theirs
        /// under heavy tribute (vassal:heavy), its levies called this season (vassal:levy). Only reads.
        /// </summary>
        private static void FealtyFacts(FactScene s, FactSet f)
        {
            if (s?.people == null || s.people.IsPlayer || WorldLedger.Instance == null) return;
            int now = Find.TickManager?.TicksGame ?? 0;
            var t = Fealty.With(s.people);
            if (t != null)
            {
                bool levies = PactMath.ParseArms(t.terms.goods, out _, out _);
                if (!levies)
                    foreach (var e in History.Of(Refs.Of(s.people), new[] { DiplomacyMath.Levy }, now - (int)(FealtyMath.SeasonDays * FealtyMath.DayTicks)))
                    {
                        levies = true;
                        break;
                    }
                if (levies) f.Tag(FealtyMath.LeviesTag, 1.5f);
                if (!Fealty.FailedSeason(t) && t.terms.days > 0f && FealtyMath.ThisSeason((int)(t.terms.days * FealtyMath.DayTicks), now)) f.Tag(FealtyMath.ProtectedTag, 1.5f);
                if (Fealty.Owed(t)) f.Tag(FealtyMath.ShortTag, 2f);
            }
            foreach (var (_, v) in Vassals.All())
            {
                if (v.people != s.people) continue;
                if (v.level == TributeLevel.Heavy) f.Tag(FealtyMath.HeavyTag, 1.5f);
                if (FealtyMath.ThisSeason(v.leviesAt, now)) f.Tag(FealtyMath.VassalLevyTag, 1.5f);
            }
        }
    }
}
