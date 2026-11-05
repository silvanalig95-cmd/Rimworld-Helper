using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The facts of feasts with guests (docs/design/stage2-diplomacy.md, item 5), read by the ties' provider (FactsLedger.cs).
    public static partial class Facts
    {
        /// <summary>
        /// A feast with the scene's people: the one spoken to invited to the colony's feast and not yet come (feast:invited),
        /// their people's guests coming on a day set (feast:soon) or at the colony now (feast:here); a feast lately (within
        /// FeastMath.TalkDays) that their people came to: the one spoken to was a guest at it (them:feastguest), one of
        /// their people won a contest there (feast:theywon), one of the colony's did (feast:wewon), and the history's own
        /// town:lately:dip:feast for talk at their town; {FEAST} the colony's feast in its words, {CHAMPION} who won the
        /// most. Only reads.
        /// </summary>
        private static void FeastFacts(FactScene s, FactSet f)
        {
            if (s?.people == null || s.people.IsPlayer || DiplomacyBook.Instance == null) return;
            var people = s.people;
            int now = Now;
            foreach (var (h, p) in Feasts.All())
            {
                var theirs = p.guests.Where(g => g.people == people).ToList();
                if (theirs.Count == 0 || p.purpose != FeastPurpose.Feast) continue;
                f.Value(FeastMath.FeastKey, FeastWords.Name(h.Map));
                if (p.stage == FeastStage.Inviting && s.listener != null && theirs.Any(g => g.pawn == s.listener)) f.Tag(FeastMath.InvitedTag);
                if (p.stage == FeastStage.Set && theirs.Any(g => g.state == GuestState.Coming)) f.Tag(FeastMath.SoonTag);
                if (p.OnMap && theirs.Any(g => g.state == GuestState.Here)) f.Tag(FeastMath.HereTag);
            }
            foreach (var h in DiplomacyBook.Instance.Live())
            {
                var p = h.lastFeast;
                if (p == null || p.purpose != FeastPurpose.Feast || p.arrived < 0 || p.ended < 0 || now - p.ended > FeastMath.TalkDays * FeastMath.DayTicks) continue;
                var theirs = p.guests.Where(g => g.people == people && g.state == GuestState.Gone).ToList();
                if (theirs.Count == 0) continue;
                float days = (now - p.ended) / (float)FeastMath.DayTicks;
                f.Value(FeastMath.FeastKey, FeastWords.Name(h.Map));
                f.Tag("town:lately:" + DiplomacyMath.Feast, FactsMath.Fresh(days, FeastMath.TalkDays, 1.5f));
                if (s.listener != null && theirs.Any(g => g.pawn == s.listener)) f.Tag(FeastMath.GuestTag);
                var won = p.results.Where(r => !r.off && !r.winnerName.NullOrEmpty()).ToList();
                if (won.Any(r => r.people == people)) f.Tag(FeastMath.TheyWonTag);
                if (won.Any(r => r.ours)) f.Tag(FeastMath.WeWonTag);
                if (!p.championName.NullOrEmpty()) f.Value(FeastMath.ChampionKey, p.championName);
            }
        }
    }
}
