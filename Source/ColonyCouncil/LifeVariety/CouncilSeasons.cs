using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Social events and talk of the council's seasons (A25 of docs/content-update-proposals.md, the player's go of 20 November
    /// 2026; Defs/SocialEventDefs/Rimfolk_SocialEvents_CouncilSeasons.xml, Defs/TopicDefs/Rimfolk_Topics_Vote.xml). Which two a
    /// social event of the council's life is between (SocialEventDef.council): help with the speech (A a candidate in an
    /// election under way, B a friend), the old holder shows the new one the seat's things (A the new holder, B the one they
    /// took it from), a promise remembered (B a holder whose election promise the map shows going well, or badly, by the
    /// vote's own test), two who back rival candidates for one seat (by the vote's own lean), a petition talked over by two of
    /// its signers, an elder's word to a young holder. Each fills its words ({OFFICE}, {CANDIDATE}, {RIVAL}, {PROMISE}, {WISH}).
    /// Nothing here moves a vote.
    /// </summary>
    public static class CouncilSeasons
    {
        private static Election ElectionAt(Pawn p)
        {
            var e = p?.MapHeld == null ? null : OfficeTracker.For(p.MapHeld)?.election;
            return e == null || e.countDone || e.phase == ElectionPhase.Count ? null : e;
        }

        /// <summary>The race this candidate stands in.</summary>
        private static ElectionRace RaceOf(Election e, Pawn p) => e?.races.FirstOrDefault(r => r.EntryOf(p) != null);

        /// <summary>Whom this voter leans to in a contested race (the vote's own lean, without the luck of the day), and whether they're undecided.</summary>
        public static Pawn LeanIn(ElectionRace race, Pawn voter, out bool undecided)
        {
            undecided = false;
            if (race == null || voter == null || race.office == null) return null;
            var scores = race.Candidates.Where(c => c != null).Select(c => (c, OfficeTracker.Leaning(race.office, voter, c))).OrderByDescending(x => x.Item2).ToList();
            if (scores.Count == 0) return null;
            undecided = scores.Count > 1 && CouncilSeasonMath.IsUndecided(scores[0].Item2, scores[1].Item2);
            return scores[0].c;
        }

        private static bool Young(Pawn p) => p.RaceProps.Humanlike && CouncilSeasonMath.YoungHolder > Flavor.HumanAge(p);

        private static bool Elder(Pawn p) => EldersMath.IsElder(Flavor.HumanAge(p), Interplay.Holds(p, "Council_Office_Elder"));

        /// <summary>Whether this social event of the council's seasons can be between A and B now.</summary>
        public static bool Fits(string kind, Pawn a, Pawn b)
        {
            if (!ColonyCouncilMod.Settings.offices || a == null || b == null) return false;
            switch (kind)
            {
                case CouncilSeasonMath.Speech:
                {
                    var e = ElectionAt(a);
                    var race = RaceOf(e, a);
                    return race != null && race.Contested && race.EntryOf(b) == null;
                }
                case CouncilSeasonMath.Handover:
                {
                    var office = OfficeTracker.HeldBy(a);
                    return office != null && LifeChecks.HasThought(b, new List<string> { "Council_LostOffice" }, CouncilSeasonMath.HandoverDays, a);
                }
                case CouncilSeasonMath.PromiseWell:
                case CouncilSeasonMath.PromiseBadly:
                {
                    var council = OfficeTracker.For(b.MapHeld);
                    if (council == null || !council.PromiseStanding(b, out _, out _, out bool badly)) return false;
                    return badly == (kind == CouncilSeasonMath.PromiseBadly);
                }
                case CouncilSeasonMath.RivalBackers:
                {
                    var e = ElectionAt(a);
                    if (e == null) return false;
                    foreach (var race in e.races.Where(r => r.Contested))
                    {
                        if (race.EntryOf(a) != null || race.EntryOf(b) != null) continue;
                        var la = LeanIn(race, a, out bool ua);
                        var lb = LeanIn(race, b, out bool ub);
                        if (la != null && lb != null && la != lb && !ua && !ub) return true;
                    }
                    return false;
                }
                case CouncilSeasonMath.Petition:
                    return SharedPetition(a, b) != null;
                case CouncilSeasonMath.ElderWord:
                    return Elder(a) && !Elder(b) && Young(b) && OfficeTracker.HeldBy(b) != null;
                default:
                    return false;
            }
        }

        /// <summary>An open wish both of them asked for (one petition), or null.</summary>
        private static Wish SharedPetition(Pawn a, Pawn b)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return null;
            foreach (var w in comp.wishes)
            {
                if (w?.def == null || w.state != WishState.Open || !w.def.allowPetition) continue;
                bool hasA = w.Supporters.Contains(a) || comp.wishes.Any(o => o != w && o.state == WishState.Open && o.petition >= 0 && o.petition == w.petition && o.pawn == a);
                bool hasB = w.Supporters.Contains(b) || comp.wishes.Any(o => o != w && o.state == WishState.Open && o.petition >= 0 && o.petition == w.petition && o.pawn == b);
                if (hasA && hasB) return w;
            }
            return null;
        }

        /// <summary>The words of the event for these two.</summary>
        public static void Fill(string kind, SocialEventContext ctx)
        {
            var a = ctx.a;
            var b = ctx.b;
            switch (kind)
            {
                case CouncilSeasonMath.Speech:
                {
                    var race = RaceOf(ElectionAt(a), a);
                    ctx.office = race?.office?.Name;
                    ctx.candidate = a?.LabelShort;
                    break;
                }
                case CouncilSeasonMath.Handover:
                    ctx.office = OfficeTracker.HeldBy(a)?.Name;
                    break;
                case CouncilSeasonMath.PromiseWell:
                case CouncilSeasonMath.PromiseBadly:
                {
                    var council = OfficeTracker.For(b.MapHeld);
                    if (council != null && council.PromiseStanding(b, out var office, out string promise, out _))
                    {
                        ctx.office = office?.Name;
                        ctx.promise = promise;
                    }
                    break;
                }
                case CouncilSeasonMath.RivalBackers:
                {
                    var e = ElectionAt(a);
                    foreach (var race in e?.races.Where(r => r.Contested) ?? Enumerable.Empty<ElectionRace>())
                    {
                        if (race.EntryOf(a) != null || race.EntryOf(b) != null) continue;
                        var la = LeanIn(race, a, out _);
                        var lb = LeanIn(race, b, out _);
                        if (la == null || lb == null || la == lb) continue;
                        ctx.office = race.office?.Name;
                        ctx.candidate = la.LabelShort;
                        ctx.rival = lb.LabelShort;
                        break;
                    }
                    break;
                }
                case CouncilSeasonMath.Petition:
                    ctx.wishLabel = SharedPetition(a, b)?.LabelInline;
                    break;
                case CouncilSeasonMath.ElderWord:
                    ctx.office = OfficeTracker.HeldBy(b)?.Name;
                    break;
            }
        }
    }

    public partial class OfficeTracker
    {
        /// <summary>
        /// The election promise this holder made for their seat that is still to be judged, and whether the map shows it going
        /// badly by the vote's own test (as a promise halfway is raised, SeatHalfway.cs). False when they have none to judge.
        /// </summary>
        internal bool PromiseStanding(Pawn holder, out OfficeDef office, out string promise, out bool badly)
        {
            office = null;
            promise = null;
            badly = false;
            if (holder == null) return false;
            int now = Find.TickManager.TicksGame;
            foreach (var o in Offices)
                for (int seat = 0; seat < o.Seats; seat++)
                {
                    if (HolderOf(o, seat) != holder) continue;
                    var pr = PromiseOf(holder, o, seat);
                    if (pr == null) continue;
                    var t = RecordMath.TestOf(pr.values);
                    float before = RecordMath.Number(pr.values, RecordMath.ReadingAt);
                    float after = t == PromiseTest.None ? -1f : Reading(t, o, holder);
                    if (after < 0f) continue;
                    float days = (now - pr.tick) / (float)GenDate.TicksPerDay;
                    office = o;
                    promise = RecordMath.Get(pr.values, RecordMath.PromiseAt);
                    badly = SeatMath.GoingBadly(t, before, after, days);
                    return !promise.NullOrEmpty();
                }
            return false;
        }
    }

    public partial class SocialEventContext
    {
        /// <summary>The council's seasons (A25): the seat, a candidate and the one backed against them, a promise, a petition.</summary>
        public string office, candidate, rival, promise, wishLabel;

        public override string SeatName => office ?? base.SeatName;
    }

    /// <summary>
    /// "The vote" (A25): a talk topic only while an election is under way at their home, with lines on whom the speaker leans
    /// to, read from the vote's own lean so the words match (lean:self, lean:holder, lean:other, undecided); {SUBJECT} is the
    /// seat. Being for it is caring how the vote goes, against thinking it changes nothing. It never moves the vote.
    /// </summary>
    public class TopicWorker_Vote : TopicWorker
    {
        /// <summary>The contested race most talked of at this home: the one with the most standing.</summary>
        private static ElectionRace Race(Map map)
        {
            var e = map == null ? null : OfficeTracker.For(map)?.election;
            if (e == null || e.countDone || !ColonyCouncilMod.Settings.offices) return null;
            return e.races.Where(r => r.Contested && r.office != null).OrderByDescending(r => r.entries.Count).ThenBy(r => r.office.order).FirstOrDefault();
        }

        public override float Relevance(Pawn a, Pawn b, TalkScene s) => Race(s?.map ?? a?.MapHeld) != null ? def.relevance : 0f;

        public override float Hot(TalkScene s) => Race(s?.map) != null ? 0.7f : 0f;

        public override TalkSubject Subject(Pawn a, Pawn b, TalkScene s)
        {
            var race = Race(s?.map ?? a?.MapHeld);
            return race == null ? null : TalkSubject.Named(race.office.Name);
        }

        public override void AddTags(Pawn speaker, Pawn other, TalkScene s, TalkSubject subject, HashSet<string> tags)
        {
            var race = Race(s?.map ?? speaker?.MapHeld);
            if (race == null || speaker == null) return;
            var lean = CouncilSeasons.LeanIn(race, speaker, out bool undecided);
            tags.Add(CouncilSeasonMath.LeanTag(lean == speaker, lean != null && lean == race.holder, undecided));
            if (race.EntryOf(speaker) != null) tags.Add("standing");
            if (race.holder == speaker) tags.Add("holder");
        }
    }
}
