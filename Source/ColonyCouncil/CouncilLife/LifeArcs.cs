using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // Three personal stories of the council's and the heart's life (the content update's A18, A19 and A20, the player's go of
    // 20 November 2026; Defs/ArcDefs/Rimfolk_Arcs_CouncilLife.xml). Each gate passes only on what happened:
    // - After the vote: someone voted out of a seat they held a year or more. A sour few days; the winner (or a friend)
    //   sits down with them about the seat; then they help the winner (an afternoon at their side, and the grudge of a seat
    //   lost eases) or keep it, and the story ends with a run at the next election in their journal.
    // - The one sent away: the colonist closest to someone the council banished. A hard day; a walk out to the edge of the
    //   colony's land, where the road comes in; a talk with the chaplain or a friend; then better days, or, if the one sent
    //   away comes back to the gate, they walk out to meet them.
    // - Worn thin: after the heart's own episode, or a week near breaking. A quiet day; the closest friend comes over and
    //   sits with them; a day off taken (the heart's day off or a rest day: the player's own button moves it on); back to
    //   themselves (strain at 30 or under for two days). Its end lifts some weight, and the friend has a small mark of it.
    // The rules are LifeArcMath.

    public static class LifeArcs
    {
        /// <summary>A holder voted out of their seat: values office, days held (-1 unknown), the winner's id, "by" for a by-election.</summary>
        public const string KindVotedOut = "office:votedout";

        /// <summary>A holder who stepped down by the council's leave (CouncilLife/StepDown.cs): values office.</summary>
        public const string KindStepped = "office:stepped";

        /// <summary>The count is in and someone lost their place on the council (OfficeTracker.Finish): kept in the history.</summary>
        public static void VotedOut(OfficeTracker council, OfficeDef office, Pawn who, Pawn to, float heldDays, bool byElection)
        {
            if (office == null || who == null) return;
            try
            {
                var actors = new List<HistoryActor> { new HistoryActor(Refs.Of(who), HistoryRole.About) };
                if (to != null) actors.Add(new HistoryActor(Refs.Of(to), HistoryRole.With));
                History.Add(KindVotedOut, actors, Refs.Colony(Homes.Of(who) ?? Homes.Seat), HistoryMath.Everyday, null, null,
                    new List<string> { office.defName, heldDays.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture), (to?.thingIDNumber ?? -1).ToString(), byElection ? "by" : "year" });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("after the vote: voted out", e);
            }
        }

        public static string Get(List<string> values, int i) => values != null && i >= 0 && i < values.Count ? values[i] : null;

        public static Pawn ById(IEnumerable<Pawn> among, string id) =>
            int.TryParse(id, out int n) && n >= 0 ? among?.FirstOrDefault(p => p != null && p.thingIDNumber == n) : null;
    }

    /// <summary>
    /// After the vote: voted out of a seat held a year or more, in the last days. {NAME} the seat. The other is the winner
    /// when they are at home (tag winner), else the colonist's closest friend (friend). Its key: the history entry, the
    /// winner's id. Before the third stage it settles whether they help the winner (helped) or keep the grudge (kept).
    /// </summary>
    public class ArcWorker_AfterVote : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (!ColonyCouncilMod.Settings.offices || p.DevelopmentalStage.Juvenile()) return null;
            int now = Find.TickManager.TicksGame;
            var e = History.Of(Refs.Of(p), new[] { LifeArcs.KindVotedOut }, now - (int)(LifeArcMath.VotedOutWithin * GenDate.TicksPerDay), 1).FirstOrDefault();
            if (e == null) return null;
            var office = DefDatabase<OfficeDef>.GetNamedSilentFail(LifeArcs.Get(e.values, 0));
            if (office == null || OfficeTracker.HeldBy(p) != null) return null;
            float held = float.TryParse(LifeArcs.Get(e.values, 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float h) ? h : -1f;
            // An older save's seat has no start on record: a yearly vote counts it as a year held.
            bool long_ = held >= 0f ? LifeArcMath.HeldLongEnough(held) : LifeArcs.Get(e.values, 3) == "year";
            if (!long_) return null;
            string winnerId = LifeArcs.Get(e.values, 2);
            var winner = LifeArcs.ById(home, winnerId);
            var other = winner != null && winner != p ? winner : ArcUtil.Closest(p, home, 10f);
            if (other == null) return null;
            return new ArcCandidate { other = other, name = office.Name, key = e.id + "|" + winnerId, tag = other == winner ? "winner" : "friend", weight = 1.5f };
        }

        private static Pawn Winner(Arc arc)
        {
            var parts = (arc.key ?? "").Split('|');
            return parts.Length > 1 ? LifeArcs.ById(PawnsFinder.AllMaps_FreeColonistsSpawned, parts[1]) : null;
        }

        /// <summary>Before the third stage: helping the winner, or keeping the grudge.</summary>
        public override void Settle(Arc arc)
        {
            if (arc.stage != 1) return;
            var w = Winner(arc);
            if (w == null)
            {
                arc.tag = "kept";
                return;
            }
            float rivalry = CouncilGameComponent.Instance?.social?.Strength(arc.pawn, w, TieKind.Rivals) ?? 0f;
            arc.tag = LifeArcMath.Helps(ArcUtil.Opinion(arc.pawn, w), rivalry) ? "helped" : "kept";
        }

        /// <summary>Back in a seat before it ends: the story is over without a word.</summary>
        public override string Broken(Arc arc) => arc.stage < arc.def.LastStage && OfficeTracker.HeldBy(arc.pawn) != null ? "seated" : null;
    }

    /// <summary>
    /// The one sent away: the colonist closest to someone the council banished, in the days after (the one who would stand by
    /// them, else the one who thinks best of them). {LOST} the one sent away; the other is the chaplain, else a friend. Its
    /// third stage moves on early if the one sent away is at the gate again (tag back), or one of the colony again (home).
    /// </summary>
    public class ArcWorker_SentAway : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            var t = BanishTracker.Instance;
            if (t == null || ArcUtil.Psychopath(p)) return null;
            int now = Find.TickManager.TicksGame;
            foreach (var r in t.records)
            {
                var gone = r?.pawn;
                if (gone == null || gone.Dead || r.banishedTick < 0 || r.cameBack || now - r.banishedTick > LifeArcMath.SentAwayWithin * GenDate.TicksPerDay) continue;
                // The closest of those at home: one who would stand by them first, then by what they think of them.
                var closest = home.Where(o => o != gone && !o.DevelopmentalStage.Baby())
                    .OrderByDescending(o => BanishMath.StandsBy(BanishTracker.Opinion(o, gone), BanishTracker.Close(o, gone)))
                    .ThenByDescending(o => BanishTracker.Opinion(o, gone)).FirstOrDefault();
                if (closest != p || BanishTracker.Opinion(p, gone) < LifeArcMath.CloseOpinion) continue;
                var chaplain = Interplay.Officer(Interplay.Chaplain, p.Map);
                var other = chaplain != null && chaplain != p && home.Contains(chaplain) ? chaplain : ArcUtil.Closest(p, home, 10f, gone);
                return new ArcCandidate { other = other, lostName = r.Name, key = r.id.ToString(), tag = other != null && other == chaplain ? "chaplain" : "friend", weight = 2f };
            }
            return null;
        }

        public static BanishRecord Record(Arc arc) =>
            int.TryParse(arc?.key, out int id) ? BanishTracker.Instance?.records.FirstOrDefault(r => r != null && r.id == id) : null;

        /// <summary>The one sent away is on a home map again: at the gate, or one of the colony.</summary>
        public override bool CustomGate(Arc arc, ArcGate gate)
        {
            var gone = Record(arc)?.pawn;
            return gone != null && !gone.Dead && gone.Spawned && gone.Map != null && gone.Map.IsPlayerHome;
        }

        public override void Settle(Arc arc)
        {
            if (arc.stage != 2) return;
            var gone = Record(arc)?.pawn;
            if (gone == null || gone.Dead || !gone.Spawned || !(gone.Map?.IsPlayerHome ?? false)) arc.tag = "better";
            else arc.tag = gone.Faction == Faction.OfPlayer ? "home" : "back";
        }
    }

    /// <summary>
    /// Worn thin: after the heart's own episode (worn out, wandering in their room) or a week near breaking; the other is the
    /// colonist's closest friend. Its second stage waits for a day off taken since it began: the heart's day off, or a rest
    /// day from the calendar.
    /// </summary>
    public class ArcWorker_WornThin : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            var h = Heart.Of(p);
            if (h == null || p.InMentalState) return null;
            int now = Find.TickManager.TicksGame;
            float sinceEpisode = h.lastEpisodeTick < 0 ? -1f : StepDownMath.DaysSince(h.lastEpisodeTick, now, GenDate.TicksPerDay);
            float breaking = StepDownMath.DaysSince(h.breakingSince, now, GenDate.TicksPerDay);
            if (!LifeArcMath.WornThin(sinceEpisode, !p.InMentalState, breaking)) return null;
            var friend = ArcUtil.Closest(p, home, 10f);
            if (friend == null) return null;
            return new ArcCandidate { other = friend, tag = breaking >= LifeArcMath.BreakingDays ? "week" : "episode", weight = 1.6f };
        }

        /// <summary>A day off taken since the stage began: the heart's, running or given, or the calendar's rest today.</summary>
        public override bool CustomGate(Arc arc, ArcGate gate)
        {
            var p = arc.pawn;
            var h = Heart.Of(p);
            if (h != null && (HeartTimeOff.Running(p) || h.lastDayOffTick >= arc.stageTick)) return true;
            return p.Spawned && (ColonyCalendar.For(p.Map)?.RestingToday(p.Map, p) ?? false);
        }
    }

    /// <summary>A small mark of character for A (Persona/Marks.cs), about B: <see cref="family"/>, at <see cref="scale"/> of its size.</summary>
    public class ArcEffect_Mark : DilemmaEffect
    {
        public string family;
        public float scale = 1f;

        public override bool ChanceEach => false;

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.a == null || family.NullOrEmpty()) return;
            try
            {
                PersonaMarks.Give(ctx.a, family, ctx.b, null, scale);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a story's mark " + family, e);
            }
        }
    }

    /// <summary>
    /// The one sent away is at the gate again (the one sent away's last stage, tag back): the colonist walks out to them and
    /// keeps them company a while (a visit), as long as they are still on the map.
    /// </summary>
    public class ArcEffect_MeetSentAway : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Apply(DilemmaContext ctx)
        {
            var arc = (ctx as ArcEffectContext)?.arc;
            var gone = ArcWorker_SentAway.Record(arc)?.pawn;
            if (arc?.pawn == null || gone == null || gone.Dead || !gone.Spawned || gone.Map != arc.pawn.Map) return;
            var c = new DilemmaContext { map = gone.Map, a = arc.pawn, b = gone, why = ctx.why };
            new DilemmaEffect_Visit
            {
                label = "CC_Life_MeetSentAwayLabel".Translate(arc.pawn.Named("PAWN"), gone.Named("OTHER")).Resolve(), who = PlayWho.A, whom = PlayWho.B, to = PlaySpot.Person,
                hours = new IntRange(6, 22), duration = new FloatRange(1f, 1.5f)
            }.Apply(c);
        }
    }
}
