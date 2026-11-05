using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The facts of marriage alliances (docs/design/stage2-diplomacy.md, item 6), read by the ties' provider (FactsLedger.cs).
    public static partial class Facts
    {
        /// <summary>
        /// A marriage of Rimfolk's between the colony and the scene's people (the newest, the speaker's own first): tie:marriage
        /// (more current when new, and town:lately:dip:wedding within MatchMath.TalkDays of the wedding), one of the colony's
        /// living among them (tie:marriage:ours) or one of theirs at the colony (tie:marriage:theirs), the speaker one of the
        /// couple (tie:marriage:you), held through a child after a death (match:child), {INLAW} their spouse and {SPOUSEOF}
        /// the colony's; a match with them agreed and the wedding to come (match:agreed); a spouse from among them wronged by
        /// the colony, a grudge still held (match:wronged). Only reads.
        /// </summary>
        private static void MatchFacts(FactScene s, FactSet f)
        {
            if (s?.people == null || s.people.IsPlayer || WorldLedger.Instance == null) return;
            var people = s.people;
            int now = Now;
            Tie best = null;
            bool bestYou = false;
            foreach (var t in Matches.Live())
            {
                if (Matches.PeopleOf(t) != people) continue;
                var ours = Matches.OursOf(t);
                var theirs = Matches.TheirsOf(t);
                bool you = s.speaker != null && (s.speaker == ours || s.speaker == theirs);
                if (best == null || you && !bestYou || you == bestYou && t.since > best.since)
                {
                    best = t;
                    bestYou = you;
                }
            }
            if (best != null)
            {
                float days = best.since >= 0 && best.since <= now ? (now - best.since) / (float)MatchMath.DayTicks : float.MaxValue;
                f.Tag(MatchMath.Tag, days <= MatchMath.TalkDays ? FactsMath.Fresh(days, MatchMath.TalkDays, 2f) : 1f);
                f.Tag(Matches.LivesOf(best) == MatchHome.Theirs ? MatchMath.OursTag : MatchMath.TheirsTag);
                if (bestYou) f.Tag(MatchMath.YouTag, 2f);
                if (best.terms.count > 0) f.Tag(MatchMath.ChildTag);
                if (days <= MatchMath.TalkDays) f.Tag("town:lately:" + DiplomacyMath.Wedding, FactsMath.Fresh(days, MatchMath.TalkDays, 1.5f));
                var theirsP = Matches.TheirsOf(best);
                var oursP = Matches.OursOf(best);
                f.Value(MatchMath.InlawKey, theirsP?.LabelShort ?? (MatchMath.ParseSpouse(best.terms.key, out string r) ? DipWords.NameOf(Ref.Parse(r)) : null));
                f.Value(MatchMath.SpouseOfKey, oursP?.LabelShort ?? DipWords.NameOf(best.terms.about));
            }
            foreach (var (_, p) in Matches.All())
                if (p.people == people && p.stage != MatchStage.Asking)
                {
                    f.Tag(MatchMath.AgreedTag, 1.5f);
                    if (best == null)
                    {
                        f.Value(MatchMath.InlawKey, MatchWords.Name(p.theirs, p.theirsName));
                        f.Value(MatchMath.SpouseOfKey, MatchWords.Name(p.ours, p.oursName));
                    }
                    break;
                }
            foreach (var g in Matches.Grudges())
                if (g.terms.count == people.loadID)
                {
                    f.Tag(MatchMath.WrongedTag);
                    break;
                }
        }
    }
}
