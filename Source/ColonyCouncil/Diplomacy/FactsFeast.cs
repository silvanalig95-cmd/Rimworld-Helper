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
        /// FeastMath.TalkDays) that their people came to, as talk at their town has it (town:lately:dip:feast): the one
        /// spoken to was a guest at it (them:feastguest) and went home pleased (feast:welcomed) or without a plate
        /// (feast:slighted); their people's guests mostly pleased (feast:good) or not (feast:poor); the champion of the day
        /// was one of their people (feast:theywon) or one of the colony's (feast:wewon); a feast with contests
        /// (feast:contests); {FEAST} the colony's feast in its words, {CHAMPION} the champion of the day. Only reads.
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
                if (p.contests.Count > 0 && (p.stage == FeastStage.Set || p.OnMap)) f.Tag(FeastMath.ContestsTag);
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
                var me = s.listener != null ? theirs.FirstOrDefault(g => g.pawn == s.listener) : null;
                if (me != null)
                {
                    f.Tag(FeastMath.GuestTag);
                    if (me.ate && me.welcome >= FeastMath.RememberedAt) f.Tag(FeastMath.WelcomedTag);
                    if (!me.ate && p.held) f.Tag(FeastMath.SlightedTag);
                }
                float mean = theirs.Average(g => g.welcome);
                bool unfed = p.held && theirs.Any(g => !g.ate);
                if (FeastMath.WentWell(mean, unfed)) f.Tag(FeastMath.GoodTag);
                else if (FeastMath.WentPoorly(mean, unfed)) f.Tag(FeastMath.PoorTag);
                if (p.contests.Count > 0) f.Tag(FeastMath.ContestsTag);
                if (p.championName.NullOrEmpty()) continue;
                f.Value(FeastMath.ChampionKey, p.championName);
                var champ = p.champion != null ? p.GuestOf(p.champion) : null;
                if (champ != null && champ.people == people) f.Tag(FeastMath.TheyWonTag);
                else if (champ == null && p.results.Any(r => !r.off && r.ours && r.winnerName == p.championName)) f.Tag(FeastMath.WeWonTag);
            }
        }
    }
}
