using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The facts of ransom and the envoy (docs/design/stage2-diplomacy.md, items 3 and 4), read by the ties' provider (FactsLedger.cs).
    public static partial class Facts
    {
        /// <summary>
        /// Captives and envoys between the colony and the scene's people: one of theirs in the colony's cells (ransom:theirs),
        /// one of the colony's they hold (ransom:held), the one spoken to was once the colony's captive (them:wascaptive, with
        /// {CAPTOR} the home that held them); their envoy staying at the colony (envoy:here, {ENVOY}), the one spoken to being
        /// that envoy (them:envoy), the colony's envoy living at their town (envoy:ours, {OURENVOY}). Only reads.
        /// </summary>
        private static void RansomEnvoyFacts(FactScene s, FactSet f)
        {
            if (s?.people == null || s.people.IsPlayer || DiplomacyBook.Instance == null) return;
            var people = s.people;
            foreach (var home in Homes.All)
                if (home?.mapPawns != null && home.mapPawns.PrisonersOfColonySpawned.Any(p => p != null && p.Faction == people))
                {
                    f.Tag(RansomMath.TheirsTag);
                    break;
                }
            if (FavourCalls.Held(people).Any(p => p.Faction == Faction.OfPlayer)) f.Tag(RansomMath.HeldTag);
            if (s.listener != null)
            {
                var r = Refs.Of(s.listener);
                var e = History.Of(r, new[] { DiplomacyMath.Ransomed, DiplomacyMath.Exchanged }, int.MinValue, 1).FirstOrDefault();
                if (e != null && e.actors.Any(a => a.who == r && a.role == HistoryRole.About))
                {
                    f.Tag(RansomMath.WasCaptiveTag);
                    f.Value(RansomMath.CaptorKey, DipWords.NameOf(e.place.kind == RefKind.Colony ? e.place : Refs.Us));
                }
            }
            var stay = Envoys.With(people, out _);
            if (stay != null && stay.stage == EnvoyStage.Staying && stay.envoy != null)
            {
                f.Tag(EnvoyMath.HereTag);
                f.Value(EnvoyMath.EnvoyKey, stay.envoy.LabelShort);
                if (s.listener == stay.envoy) f.Tag(EnvoyMath.ThemTag);
            }
            var away = EnvoysAbroad.With(people);
            if (away != null && away.stage == EnvoyStage.There && away.colonist != null)
            {
                f.Tag(EnvoyMath.OursTag);
                f.Value(EnvoyMath.OurEnvoyKey, away.colonist.LabelShort);
            }
        }
    }
}
