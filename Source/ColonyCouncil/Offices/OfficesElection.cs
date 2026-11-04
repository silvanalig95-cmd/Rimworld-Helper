using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// The election as something that happens in the colony (setting "Elections play out on the map"): a letter names
    /// election day by its date and lists who stands for each seat and on what; in the days before, the candidates walk
    /// over to people in their free time and make their case; on the evening before, they speak at the hustings; on the
    /// day, everyone at home walks to the meeting place and casts a vote; that evening the outgoing speaker (or the
    /// eldest) counts the votes aloud, and the results letter follows. A colony with no seat worth a race gets a short
    /// ceremony instead. The vote is the same as ever (<see cref="Leaning"/>), with what the campaign did added in
    /// (<see cref="CampaignLean"/>), and everything that hangs off the result is the instant vote's (<see cref="Settle"/>).
    /// Nothing here ticks on its own: the council's hour (CouncilGameComponent.HourlyPeople) and the gatherings' own
    /// lord jobs move it on.
    /// </summary>
    public partial class OfficeTracker
    {
        private static readonly Color SpeechColor = new Color(0.78f, 0.88f, 1f);

        public static GatheringDef ElectionGathering => DefDatabase<GatheringDef>.GetNamedSilentFail("Rimfolk_ElectionGathering");

        private static JobDef voteJob;

        public static JobDef VoteJob => voteJob ??= DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_CastVote");

        private static int Now => Find.TickManager.TicksGame;

        // ---------------- The vote's extra term ----------------

        /// <summary>
        /// What an election under way adds to a voter's lean toward a candidate: the candidate's visits to them, how the
        /// candidate did at the hustings if they were there to hear it, and the council's endorsement (weighed by the
        /// voter's trust in the council). Nothing outside an election.
        /// </summary>
        private static float CampaignLean(OfficeDef office, Pawn voter, Pawn candidate)
        {
            var comp = CouncilGameComponent.Instance;
            // The election of the voter's own colony.
            var e = voter == null ? null : ColonyState.Of(voter)?.offices.election;
            if (e == null || comp == null || voter == null || candidate == null || voter == candidate) return 0f;
            float s = e.CampaignerOf(candidate)?.LeanOf(voter) ?? 0f;
            for (int i = 0; i < e.races.Count; i++)
            {
                var r = e.races[i];
                if (r.office != office || r.endorsed != candidate) continue;
                s += ElectionMath.EndorseLean(comp.standings.TrustOf(voter));
                break;
            }
            return s;
        }

        // ---------------- Who has a say ----------------

        /// <summary>Everyone with a say whose home is this map, here or away (those away miss polling day).</summary>
        private static List<Pawn> Electorate(Map map) =>
            map == null ? new List<Pawn>() : MeetingAttendance.Members(map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.IsSlave).ToList();

        private static string DateOf(int tick, Map map)
        {
            float longitude = map != null ? Find.WorldGrid.LongLatOf(map.Tile).x : 0f;
            return GenDate.QuadrumDateStringAt(GenDate.TickGameToAbs(tick), longitude);
        }

        /// <summary>Election day's date (polling day, or the ceremony's evening).</summary>
        public static string ElectionDay(Election e) => e == null ? "" : DateOf(e.ceremony ? e.countTick : e.pollTick, e.map);

        private static string Venue(Election e) => e.venue.NullOrEmpty() ? "CC_Elect_VenueAny".Translate().ToString() : e.venue;

        private static string Phrase(Election e, string key, params NamedArgument[] args)
        {
            var all = new List<NamedArgument>(args) { Venue(e).Named("VENUE"), ElectionDay(e).Named("DATE") };
            return key.Translate(all.ToArray()).Resolve();
        }

        /// <summary>
        /// Where the election gathers, speaks and votes: the council meeting's own places (the dining table, the rec room,
        /// a gather spot), and in the open air the yard the colony's gatherings use (PlaySpots.OpenCell, PlayOut/PlayYard.cs),
        /// never a bare cell by the colony's middle.
        /// </summary>
        private static bool FindPlace(Map map, Pawn chair, out IntVec3 spot, out string venue)
        {
            string outside = "CC_Venue_Outside".Translate();
            if (CouncilMeeting.FindVenue(map, chair, out spot, out venue) && venue != outside) return true;
            spot = PlaySpots.OpenCell(map, chair);
            venue = outside;
            return spot.IsValid;
        }

        // ---------------- The announcement ----------------

        /// <summary>
        /// Calls the election: who stands for each seat (the instant vote's rules: those who can do the work, the seats few
        /// can fill first, the likely winner of one seat out of the running for the next, nobody on more than two lists),
        /// what each runs on, the dates, and the letter. False when it can't be held this way (too few, a ruler names the
        /// officers, nowhere to hold it, nothing to vote on): the caller votes at once instead.
        /// </summary>
        private bool Announce(CouncilGameComponent comp, int year, bool byElection)
        {
            if (election != null) return true;
            // Held at the colony's own home.
            var map = HomeMap;
            var voters = Eligible();
            if (map == null || voters.Count < 2 || ElectionGathering == null || VoteJob == null) return false;
            if (CouncilCustoms.Appointer(voters) != null) return false;
            var e = new Election { year = year, byElection = byElection, map = map, announcedTick = Now, voterCount = voters.Count };
            var open = Offices.Where(o => o.OpenFor(voters.Count)).ToList();
            var taken = new HashSet<Pawn>();
            foreach (var office in Offices.Where(o => !o.OpenFor(voters.Count)))
                foreach (var h in HoldersOf(office)) taken.Add(h);
            if (byElection)
                foreach (var office in open)
                    foreach (var h in HoldersOf(office)) taken.Add(h);
            var frontrunners = new HashSet<Pawn>();
            var lists = new Dictionary<Pawn, int>();
            int perSeat = ElectionMath.CandidatesPerSeat(voters.Count);
            foreach (var (office, seat) in SeatOrder(open, voters).ToList())
            {
                if (byElection && HolderOf(office, seat) != null) continue;
                var pool = Standing(office, voters.Where(p => !taken.Contains(p) && !frontrunners.Contains(p) && (lists.TryGetValue(p, out int n) ? n : 0) < 2));
                if (pool.Count == 0) continue;
                // The likely winners stand: the same vote math, without the luck of the day.
                var expected = ElectionMath.Poll(voters.Select(v => pool.Select(c => Leaning(office, v, c)).ToArray()).ToList(), pool.Count);
                var order = pool.Select((p, i) => (p, x: expected[i])).OrderByDescending(t => t.x).ThenByDescending(t => office.Competence(t.p)).Select(t => t.p).ToList();
                var chosen = order.Take(perSeat).ToList();
                // Whoever holds the seat and can still do the work stands for it again.
                var holder = HolderOf(office, seat);
                if (holder != null && order.Contains(holder) && !chosen.Contains(holder)) chosen[chosen.Count - 1] = holder;
                var planks = ElectionPlanks.Deal(office, chosen.Count, map, comp);
                var race = new ElectionRace { office = office, seat = seat };
                for (int i = 0; i < chosen.Count; i++)
                {
                    var plank = i < planks.Count ? planks[i] : null;
                    race.entries.Add(new ElectionEntry
                    {
                        pawn = chosen[i],
                        plank = plank?.key,
                        platform = plank?.text.NullOrEmpty() == false ? plank.text.Formatted(chosen[i].Named("PAWN")).Resolve() : null
                    });
                    lists[chosen[i]] = (lists.TryGetValue(chosen[i], out int n) ? n : 0) + 1;
                }
                frontrunners.Add(chosen[0]);
                e.races.Add(race);
            }
            if (e.races.Count == 0) return false;
            e.ceremony = !e.Contested;
            Schedule(e, map);
            var chair = Chair(e, map);
            if (chair != null && FindPlace(map, chair, out var spot, out var venue))
            {
                e.spot = spot;
                e.venue = venue;
            }
            election = e;
            lastElectionYear = year;
            NotePoll(e);
            SendAnnouncement(e);
            return true;
        }

        /// <summary>
        /// The dates: the campaign's days (three, or two for an empty seat), the hustings on the last evening, polling
        /// day after it from seven until six in the evening, and the count that evening. A ceremony is set for the next
        /// evening slot at least six hours off.
        /// </summary>
        private static void Schedule(Election e, Map map)
        {
            int now = Now;
            int dayStart = now - Mathf.FloorToInt(GenLocalDate.HourFloat(map) * GenDate.TicksPerHour);
            if (e.ceremony)
            {
                e.phase = ElectionPhase.Count;
                e.countTick = MeetingAttendance.NextEvening(map, now, GenDate.TicksPerHour * 6);
                return;
            }
            int days = ElectionMath.CampaignDays(e.byElection);
            e.phase = ElectionPhase.Campaign;
            e.hustingsTick = dayStart + (days - 1) * GenDate.TicksPerDay + ElectionMath.EveningHour * GenDate.TicksPerHour;
            e.pollTick = dayStart + days * GenDate.TicksPerDay + ElectionMath.PollsOpenHour * GenDate.TicksPerHour;
            e.countTick = dayStart + days * GenDate.TicksPerDay + ElectionMath.EveningHour * GenDate.TicksPerHour;
        }

        /// <summary>Everything still ahead moves a day later.</summary>
        private static void PutOffADay(Election e, bool hustings, bool polls)
        {
            if (hustings && e.hustingsTick >= 0) e.hustingsTick += GenDate.TicksPerDay;
            if (polls && e.pollTick >= 0) e.pollTick += GenDate.TicksPerDay;
            e.countTick += GenDate.TicksPerDay;
        }

        /// <summary>
        /// Who chairs the hustings and counts the votes: the outgoing speaker, else the eldest who could chair (not a
        /// candidate if anyone else can), else anyone who could.
        /// </summary>
        private Pawn Chair(Election e, Map map)
        {
            var speaker = Speaker;
            if (speaker != null && speaker.Map == map && MeetingAttendance.CanChair(speaker)) return speaker;
            var able = map.mapPawns.FreeColonistsSpawned.Where(MeetingAttendance.CanChair).ToList();
            return able.Where(p => !e.IsCandidate(p)).OrderByDescending(Flavor.HumanAge).FirstOrDefault()
                   ?? able.OrderByDescending(Flavor.HumanAge).FirstOrDefault();
        }

        private void SendAnnouncement(Election e)
        {
            // apart: the election's letter, read by the player.
            var text = new StringBuilder();
            var unopposed = e.races.Where(r => !r.Contested).ToList();
            if (e.ceremony)
            {
                text.Append(Phrase(e, e.byElection ? "CC_Elect_CeremonyByText" : "CC_Elect_CeremonyText", ChairName(e, e.chair ?? Chair(e, e.map)).Named("CHAIR")));
                text.Append("\n\n").Append("CC_Elect_Standing".Translate(UnopposedList(unopposed)).Resolve());
            }
            else
            {
                text.Append(Phrase(e, e.byElection ? "CC_Elect_AnnounceByText" : "CC_Elect_AnnounceText"));
                foreach (var r in e.races.Where(r => r.Contested))
                {
                    text.Append("\n\n").Append(SeatName(r)).Append(": ");
                    text.Append(string.Join(" ", r.entries.Select(en => en.platform.NullOrEmpty()
                        ? "CC_Elect_NoPlatform".Translate(en.pawn.Named("PAWN")).Resolve() : en.platform)));
                }
                if (unopposed.Count > 0) text.Append("\n\n").Append("CC_Elect_Unopposed".Translate(UnopposedList(unopposed)).Resolve());
                text.Append("\n\n").Append("CC_Elect_AnnounceHand".Translate().Resolve());
            }
            string title = (e.ceremony ? "CC_Elect_CeremonyTitle" : e.byElection ? "CC_Elect_AnnounceByTitle" : "CC_Elect_AnnounceTitle").Translate(e.year).Resolve();
            var def = DefDatabase<LetterDef>.GetNamedSilentFail("Rimfolk_ElectionLetter") ?? LetterDefOf.NeutralEvent;
            var look = e.spot.IsValid ? new LookTargets(new TargetInfo(e.spot, e.map)) : LookTargets.Invalid;
            var letter = LetterMaker.MakeLetter(title, text.ToString(), def, look);
            if (letter is ChoiceLetter_Election ce)
            {
                ce.year = e.year;
                ce.map = e.map;
            }
            Tell.Ask(letter, e.map);
        }

        /// <summary>Who chairs, by name; with nobody able, the eldest of those with a say.</summary>
        private static string ChairName(Election e, Pawn chair) =>
            chair?.LabelShort ?? Electorate(e.map).OrderByDescending(Flavor.HumanAge).FirstOrDefault()?.LabelShort ?? "CC_Elect_TheCouncil".Translate().ToString();

        private static string SeatName(ElectionRace r) =>
            r.office.Seats > 1 ? "CC_Office_Seat".Translate(r.office.LabelCap, r.seat + 1).Resolve() : r.office.LabelCap.ToString();

        private static string UnopposedList(List<ElectionRace> races) =>
            TextUtil.JoinAnd(races.Select(r => "CC_Elect_ForSeat".Translate(r.entries[0].pawn.LabelShort, r.office.label).Resolve()).ToList());

        // ---------------- Each day and each hour ----------------

        /// <summary>The day's look while an election is under way (from <see cref="Daily"/>): the poll is noted for the page.</summary>
        private void ElectionDaily(CouncilGameComponent comp)
        {
            var e = election;
            if (e != null) NotePoll(e);
        }

        /// <summary>
        /// The council's hour (CouncilGameComponent.HourlyPeople): those gone drop out, the candidates make their case, the
        /// hustings, the polls and the count start when their time comes, and anything that can't happen in danger waits.
        /// </summary>
        public void ElectionHourly(CouncilGameComponent comp)
        {
            var e = election;
            if (e == null) return;
            if (!S.offices)
            {
                election = null;
                return;
            }
            // Held at its own colony's home; a home no longer ours ends it (not merely a map gone).
            if (e.map == null || e.map != HomeMap || !Homes.IsHome(e.map))
            {
                e.map = HomeMap;
                if (e.map == null)
                {
                    election = null;
                    return;
                }
                e.spot = IntVec3.Invalid;
            }
            // The setting turned off before anything was under way: the colony votes at once, as it used to.
            if (!S.electionEvent && !e.gathering)
            {
                election = null;
                Hold(comp, e.year, e.byElection);
                return;
            }
            Prune(e);
            if (election != e) return;
            // Every race fell away (called off, candidates gone) but seats with one name are left: no vote, a ceremony on
            // election day's evening instead.
            if (!e.ceremony && !e.Contested && !e.gathering && e.phase != ElectionPhase.Count)
            {
                e.ceremony = true;
                e.phase = ElectionPhase.Count;
                Tell.Message(NewsKind.Colony, Importance.Notable, e.map, Phrase(e, "CC_Elect_NowCeremony"), null, MessageTypeDefOf.NeutralEvent);
            }
            NotePoll(e);
            int now = Now;
            switch (e.phase)
            {
                case ElectionPhase.Campaign:
                    Canvass(e);
                    if (now >= e.hustingsTick) e.phase = ElectionPhase.Hustings;
                    if (e.phase == ElectionPhase.Hustings) TryHustings(comp, e);
                    break;
                case ElectionPhase.Hustings:
                    if (e.gathering) KeepGathering(e);
                    else
                    {
                        Canvass(e);
                        TryHustings(comp, e);
                    }
                    break;
                case ElectionPhase.Polling:
                    Canvass(e);
                    if (now >= e.countTick) ClosePolls(e);
                    else if (now >= e.pollTick) CallToPolls(e);
                    // The polls close and the count starts in the same hour.
                    if (e.phase == ElectionPhase.Count) TryCount(comp, e);
                    break;
                case ElectionPhase.Count:
                    if (e.gathering) KeepGathering(e);
                    else if (now >= e.countTick) TryCount(comp, e);
                    break;
            }
        }

        /// <summary>
        /// Candidates who died, left, or can no longer do the seat's work drop out (a message says so); a seat with nobody
        /// left standing is let go until another election.
        /// </summary>
        private void Prune(Election e)
        {
            // A count under way reads out what was cast (the gone are passed over as it settles each seat).
            if (e.gathering && e.phase == ElectionPhase.Count) return;
            foreach (var r in e.races.ToList())
            {
                foreach (var en in r.entries.ToList())
                {
                    var p = en.pawn;
                    // One who has gone to live at another colony drops out (a short visit doesn't count).
                    if (p != null && MeetingAttendance.IsMember(p) && !MovedAway(p) && !p.DevelopmentalStage.Juvenile() && r.office.CanHold(p)) continue;
                    r.entries.Remove(en);
                    if (r.endorsed == p) r.endorsed = null;
                    if (p != null)
                        Tell.Minor(NewsKind.Colony, null, e.map, "CC_Elect_Dropped".Translate(p.Named("PAWN"), r.office.label.Named("OFFICE")).Resolve(), null, MessageTypeDefOf.NeutralEvent);
                }
                if (r.entries.Count > 0) continue;
                e.races.Remove(r);
                Tell.Minor(NewsKind.Colony, null, e.map, "CC_Elect_SeatLetGo".Translate(r.office.label.Named("OFFICE")).Resolve(), null, MessageTypeDefOf.NeutralEvent);
            }
            e.campaigners.RemoveAll(c => c.pawn == null || !e.IsCandidate(c.pawn));
            if (e.races.Count == 0) EndWithout(e);
        }

        /// <summary>Its home is no longer ours: an election held there ends quietly (the seats keep whoever holds them).</summary>
        internal void HomeLost()
        {
            var e = election;
            if (e == null || Homes.IsHome(e.map)) return;
            election = null;
            if (e.gathering) EndGatheringLord(e);
        }

        /// <summary>Nothing left to vote on: the election ends quietly (the seats keep whoever holds them).</summary>
        private void EndWithout(Election e)
        {
            if (election != e) return;
            bool gathering = e.gathering;
            e.gathering = false;
            election = null;
            lastElectionTick = Now;
            // Its gathering, if one was under way, simply ends (nothing is left to say at it).
            if (gathering) EndGatheringLord(e);
        }

        // ---------------- The poll ----------------

        [Unsaved] private readonly Dictionary<ElectionRace, (int tick, float[] poll)> pollCache = new Dictionary<ElectionRace, (int, float[])>();

        /// <summary>The votes each candidate can expect if everyone with a say voted now (the vote math without the luck of the day).</summary>
        public float[] PollOf(ElectionRace race)
        {
            var e = election;
            if (race == null || e == null) return new float[0];
            int now = Now;
            if (pollCache.TryGetValue(race, out var c) && now - c.tick < 250 && c.poll.Length == race.entries.Count) return c.poll;
            var voters = Electorate(e.map);
            var poll = ElectionMath.Poll(voters.Select(v => race.entries.Select(en => Leaning(race.office, v, en.pawn)).ToArray()).ToList(), race.entries.Count);
            if (pollCache.Count > 40) pollCache.Clear();
            pollCache[race] = (now, poll);
            return poll;
        }

        /// <summary>Who among those with a say leans to this candidate most, in this race.</summary>
        public List<Pawn> Supporters(ElectionRace race, Pawn candidate)
        {
            var list = new List<Pawn>();
            var e = election;
            if (race == null || e == null || candidate == null) return list;
            foreach (var v in Electorate(e.map))
            {
                var leans = race.entries.Select(en => Leaning(race.office, v, en.pawn)).ToList();
                int lead = ElectionMath.Leader(leans);
                if (lead >= 0 && race.entries[lead].pawn == candidate) list.Add(v);
            }
            return list;
        }

        /// <summary>The poll's leader in a race (the one who would win today), or null.</summary>
        public Pawn PollLeader(ElectionRace race)
        {
            var poll = PollOf(race);
            int lead = ElectionMath.Leader(poll);
            return lead >= 0 && lead < race.entries.Count ? race.entries[lead].pawn : null;
        }

        /// <summary>Once a day, the poll in whole votes, for the page's day-by-day line (six days kept).</summary>
        private void NotePoll(Election e)
        {
            if (e.map == null) return;
            int day = GenLocalDate.DayOfYear(e.map);
            if (e.pollNotedDay == day || e.phase == ElectionPhase.Count) return;
            e.pollNotedDay = day;
            int voters = Electorate(e.map).Count;
            foreach (var r in e.races.Where(r => r.Contested))
            {
                var whole = ElectionMath.Rounded(PollOf(r), voters);
                r.pollDays.Add(string.Join(",", r.entries.Select((en, i) => en.pawn.thingIDNumber + ":" + whole[i])));
                while (r.pollDays.Count > 6) r.pollDays.RemoveAt(0);
            }
        }

        // ---------------- The council's hand ----------------

        /// <summary>The council can still back a candidate or call a vote off: until the polls open.</summary>
        public bool CouncilMayStepIn => election != null && !election.ceremony && (election.phase == ElectionPhase.Campaign || (election.phase == ElectionPhase.Hustings && !election.gathering));

        public ElectionRace RaceFor(OfficeDef office, int seat) => election?.races.FirstOrDefault(r => r.office == office && r.seat == seat);

        /// <summary>
        /// The council backs a candidate: from now on it counts on every ballot in this race (as much as the voter trusts
        /// the council), and those who lean to someone else trust the council a little less.
        /// </summary>
        public void Endorse(ElectionRace race, Pawn candidate, CouncilGameComponent comp)
        {
            // apart: the council's decision, told in a message.
            var e = election;
            if (e == null || race == null || !e.races.Contains(race) || race.endorsed != null || race.EntryOf(candidate) == null || !CouncilMayStepIn) return;
            var fans = Supporters(race, candidate);
            var against = Electorate(e.map).Where(v => !fans.Contains(v)).ToList();
            race.endorsed = candidate;
            pollCache.Remove(race);
            foreach (var v in against)
                comp.standings.Change(v, -ElectionMath.EndorseTrustCost, "CC_Log_ElectionEndorsed".Translate(candidate.LabelShort, race.office.label));
            string text = against.Count == 0
                ? "CC_Elect_EndorsedNobody".Translate(candidate.Named("PAWN"), race.office.label.Named("OFFICE")).Resolve()
                : "CC_Elect_Endorsed".Translate(candidate.Named("PAWN"), race.office.label.Named("OFFICE"), TextUtil.JoinAnd(against.Select(p => p.LabelShort).ToList()).Named("WHO")).Resolve();
            // direct: the answer to the player's click (the election letter's or panel's "endorse").
            Messages.Message(text, new LookTargets(candidate), MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>How many with a say lean to someone else in this race (each trusts the council a little less if it backs this candidate).</summary>
        public int EndorseCost(ElectionRace race, Pawn candidate)
        {
            var e = election;
            if (e == null || race == null) return 0;
            var fans = Supporters(race, candidate);
            return Electorate(e.map).Count(v => !fans.Contains(v));
        }

        /// <summary>
        /// The council calls a seat's vote off and appoints someone (the council's own appointment, with its cost to an
        /// overruled holder); if that isn't the one who would have won, those who'd have voted for the winner trust the
        /// council less. The appointee is struck from any other list they were on.
        /// </summary>
        public void CallOff(ElectionRace race, Pawn appointee, CouncilGameComponent comp)
        {
            // apart: the council's decision, told in a message.
            var e = election;
            if (e == null || race == null || !e.races.Contains(race) || appointee == null || !race.office.CanHold(appointee)) return;
            var leader = PollLeader(race);
            var let = leader != null && leader != appointee ? Supporters(race, leader).Where(v => v != appointee).ToList() : new List<Pawn>();
            e.races.Remove(race);
            Appoint(race.office, appointee, comp, race.seat);
            ElectionSeated(race.office, race.seat, appointee);
            foreach (var v in let)
                comp.standings.Change(v, -ElectionMath.AppointTrustCost, "CC_Log_ElectionCalledOff".Translate(race.office.label, leader.LabelShort));
            string text = let.Count == 0
                ? "CC_Elect_CalledOffNobody".Translate(race.office.label.Named("OFFICE")).Resolve()
                : "CC_Elect_CalledOff".Translate(race.office.label.Named("OFFICE"), leader.Named("PAWN"), TextUtil.JoinAnd(let.Select(p => p.LabelShort).ToList()).Named("WHO")).Resolve();
            // direct: the answer to the player's click (the election letter's or panel's "call off").
            Messages.Message(text, new LookTargets(appointee), MessageTypeDefOf.NeutralEvent, false);
            if (election == e && e.races.Count == 0) EndWithout(e);
        }

        /// <summary>Someone was put in a seat while an election is under way (CallOff, the Decrees page): that seat's vote is off and they stand for nothing else.</summary>
        private void ElectionSeated(OfficeDef office, int seat, Pawn p)
        {
            var e = election;
            // A count under way settles its seats as read out.
            if (e == null || (e.gathering && e.phase == ElectionPhase.Count)) return;
            e.races.RemoveAll(r => r.office == office && r.seat == seat);
            foreach (var r in e.races)
            {
                r.entries.RemoveAll(en => en.pawn == p);
                if (r.endorsed == p) r.endorsed = null;
            }
            e.races.RemoveAll(r => r.entries.Count == 0);
            pollCache.Clear();
            if (e.races.Count == 0) EndWithout(e);
        }

        // ---------------- The campaign ----------------

        /// <summary>
        /// The candidates make their case: in their free time, between eight in the morning and nine at night, a candidate
        /// walks over to someone who hasn't voted and has a word (Together: the walk, then the moment). A few visits a day
        /// each; nobody is sought out in danger.
        /// </summary>
        private void Canvass(Election e)
        {
            var map = e.map;
            if (e.ceremony || map == null || PlayOuts.WhyNotNow(map, false) != null) return;
            int hour = GenLocalDate.HourOfDay(map);
            if (hour < 8 || hour >= 21) return;
            int day = GenLocalDate.DayOfYear(map);
            var voters = Electorate(map).Where(p => p.Spawned && p.Map == map && !e.voted.Contains(p)).ToList();
            foreach (var c in e.AllCandidates.ToList())
            {
                if (c == null || !c.Spawned || c.Map != map) continue;
                var rec = e.For(c);
                if (rec.visitDay != day)
                {
                    rec.visitDay = day;
                    rec.visitsToday = 0;
                }
                string key = "canvass|" + c.thingIDNumber;
                if (rec.visitsToday >= ElectionMath.CanvassPerDay || Together.Waiting(key)) continue;
                if (!Together.FreeTime(c) || !PlayOuts.CanTakePart(c, map) || !Rand.Chance(0.5f)) continue;
                // Not a rival in the same race; first those they haven't spoken to, those who like them a little more.
                var rivals = new HashSet<Pawn>(e.races.Where(r => r.EntryOf(c) != null).SelectMany(r => r.Candidates));
                var pick = voters.Where(v => v != c && !rivals.Contains(v))
                    .Select(v => (v, w: (rec.canvassed.ContainsKey(v) ? 1f : 3f) * Mathf.Max(0.2f, 1f + (v.relations?.OpinionOf(c) ?? 0) / 50f)))
                    .ToList();
                if (pick.Count == 0) continue;
                var voter = pick.RandomElementByWeight(x => x.w).v;
                Together.When(key, c, voter, () => CanvassMoment(c, voter), 4f, 0f);
            }
        }

        /// <summary>The race a candidate talks about: the first they stand in (the one they're likeliest to win).</summary>
        private static ElectionRace MainRace(Election e, Pawn c) => e.races.FirstOrDefault(r => r.EntryOf(c) != null);

        /// <summary>
        /// A candidate makes their case to someone: a line about what they stand for, the other's answer, a word in the
        /// social log. How far it moves them comes from the candidate's Social and how much the listener likes them; a
        /// candidate they dislike only puts them off.
        /// </summary>
        private void CanvassMoment(Pawn c, Pawn v)
        {
            // together: Together.When runs this once the candidate has walked over and the two are side by side.
            var e = election;
            if (e == null || !e.IsCandidate(c) || e.voted.Contains(v) || e.ceremony) return;
            var race = MainRace(e, c);
            if (race == null) return;
            var rec = e.For(c);
            rec.visitsToday++;
            int social = c.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            float nudge = ElectionMath.CanvassNudge(social, v.relations?.OpinionOf(c) ?? 0);
            rec.canvassed[v] = ElectionMath.AddNudge(rec.canvassed.TryGetValue(v, out float held) ? held : 0f, nudge);
            pollCache.Remove(race);
            var entry = race.EntryOf(c);
            string line = Flavor.FromPool("Rimfolk_Pool_ElectionCanvass", c, null, null, entry?.plank.NullOrEmpty() == false ? "plank:" + entry.plank : null);
            if (!line.NullOrEmpty()) Speech.Say(c, Fill(line, c, v, race.office), v, SpeechColor);
            string reply = Flavor.FromPool(nudge > 0f ? "Rimfolk_Pool_CanvassWon" : "Rimfolk_Pool_CanvassUnmoved", v);
            if (!reply.NullOrEmpty()) Speech.Say(v, Fill(reply, v, c, race.office), c, SpeechColor);
            SocialLog.Add(c, v, "CC_Elect_LogCanvass".Translate(c.Named("PAWN"), v.Named("OTHER"), race.office.label.Named("OFFICE")).Resolve(), false);
            c.skills?.Learn(SkillDefOf.Social, 60f);
        }

        private static string Fill(string line, Pawn speaker, Pawn other, OfficeDef office, string count = null) =>
            line.Formatted(speaker.Named("PAWN"), (other ?? speaker).Named("OTHER"), (office?.label ?? "").Named("OFFICE"), (count ?? "").Named("COUNT")).Resolve();

        // ---------------- Gatherings: the hustings and the count ----------------

        /// <summary>The gathering under way, if its lord is still there.</summary>
        public static Lord LordOf(Election e) => e?.map?.lordManager?.lords.FirstOrDefault(l => l.LordJob is LordJob_Election);

        /// <summary>Why the colony can't gather now, or null: danger, another gathering, half the colony under arms, nobody to chair, nowhere to go.</summary>
        private string WhyNotGather(Election e, out Pawn chair)
        {
            chair = null;
            var map = e.map;
            string wait = MeetingAttendance.WhyNotNow(map);
            if (wait != null) return wait;
            chair = Chair(e, map);
            if (chair == null) return "CC_MeetWait_NoChair".Translate();
            bool good = e.spot.IsValid && e.spot.InBounds(map) && e.spot.Standable(map) && chair.CanReach(e.spot, PathEndMode.Touch, Danger.None) && PlaySafety.Safe(chair, e.spot, map);
            if (!good)
            {
                if (!FindPlace(map, chair, out var spot, out var venue)) return "CC_MeetWait_NoVenue".Translate();
                e.spot = spot;
                e.venue = venue;
            }
            return null;
        }

        /// <summary>The evening is gone: past nine at night, or more than four hours past the set time.</summary>
        private static bool EveningOver(Election e, int set) =>
            Now - set > GenDate.TicksPerHour * 4 || (Now > set && GenLocalDate.HourOfDay(e.map) >= 21) || MeetingMath.IsNight(GenLocalDate.HourFloat(e.map)) && Now > set;

        private void StartGathering(Election e, Pawn chair, bool hustings, int lines)
        {
            var map = e.map;
            var starting = new List<Pawn> { chair };
            // The candidates are called to the hustings (they're to speak); everyone else comes when free.
            if (hustings)
                foreach (var c in e.AllCandidates)
                    if (c != chair && c.Spawned && c.Map == map && PlayOuts.CanTakePart(c, map) && PlayOuts.Interruptible(c) && !PlayOuts.NeedsPress(c) && c.GetLord() == null)
                        starting.Add(c);
            e.gathering = true;
            e.gatheringTick = Now;
            e.chair = chair;
            e.opened = false;
            e.step = 0;
            e.raceStep = 0;
            e.callStep = 0;
            e.countDone = false;
            e.countStarted = false;
            LordMaker.MakeNewLord(Faction.OfPlayer, new LordJob_Election(e.spot, chair, ElectionGathering, hustings, ElectionMath.GatheringTicks(lines)), map, starting);
            foreach (var p in starting) MeetingAttendance.CallFromWork(p);
        }

        /// <summary>A gathering whose lord vanished (everyone gone, a load) is wrapped up as broken off.</summary>
        private void KeepGathering(Election e)
        {
            if (LordOf(e) != null || Now - e.gatheringTick < 600) return;
            GatheringOver(e.phase == ElectionPhase.Hustings, true);
        }

        private static void EndGatheringLord(Election e)
        {
            var lord = LordOf(e);
            if (lord != null) lord.ReceiveMemo(LordJob_Election.DoneMemo);
        }

        /// <summary>From the gathering's lord job when it ends: finished, or broken off (danger, everyone gone).</summary>
        public void GatheringOver(bool hustings, bool early)
        {
            var e = election;
            if (e == null || !e.gathering) return;
            e.gathering = false;
            var comp = CouncilGameComponent.Instance;
            if (hustings) HustingsOver(e, early);
            else if (e.countStarted || e.countDone || e.ceremony && e.opened) ApplyResults(comp, e, e.countDone ? null : "CC_Elect_Why_BrokenOff".Translate().ToString());
            else MissedCountEvening(comp, e, "CC_Elect_Why_BrokenOff".Translate());
        }

        // ---------------- The hustings ----------------

        private void TryHustings(CouncilGameComponent comp, Election e)
        {
            if (e.gathering || e.hustingsDone) return;
            if (Now < e.hustingsTick) return;
            string wait = WhyNotGather(e, out var chair);
            if (wait == null)
            {
                foreach (var c in e.campaigners)
                {
                    c.spoke = false;
                    c.heard.Clear();
                }
                StartGathering(e, chair, true, 2 + 2 * e.AllCandidates.Count());
                Tell.Message(NewsKind.Colony, Importance.Notable, e.map, Phrase(e, "CC_Elect_HustingsStart", chair.Named("CHAIR")), new LookTargets(new TargetInfo(e.spot, e.map)), MessageTypeDefOf.PositiveEvent);
                return;
            }
            if (!EveningOver(e, e.hustingsTick)) return;
            if (e.hustingsPutOffs < ElectionMath.MaxPutOffs)
            {
                e.hustingsPutOffs++;
                PutOffADay(e, true, true);
                e.phase = ElectionPhase.Campaign;
                Tell.Message(NewsKind.Colony, Importance.Notable, e.map, Phrase(e, "CC_Elect_HustingsPutOff", wait.Named("REASON")), null, MessageTypeDefOf.NeutralEvent);
                return;
            }
            e.hustingsDone = true;
            e.phase = ElectionPhase.Polling;
            Tell.Message(NewsKind.Colony, Importance.Notable, e.map, Phrase(e, "CC_Elect_HustingsLetGo", wait.Named("REASON")), null, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>
        /// The hustings' talk, every few seconds (LordJob_Election): the chair opens; each candidate who is there speaks in
        /// turn, and how it goes (Social, mood, nature, the moment) moves everyone listening; after each speech someone
        /// answers it, a supporter's cheer or a rival's heckle. When all who came have spoken, it's over.
        /// </summary>
        public void HustingsTalk(Lord lord)
        {
            // together: said at the hustings itself, by people standing at the gathering.
            var e = election;
            if (e == null || lord == null || !e.gathering) return;
            var here = Here(e, lord);
            if (here.Count == 0) return;
            float elapsed = Elapsed(lord);
            if (!e.opened && !Gathered(e, here, elapsed)) return;
            if (!e.opened)
            {
                var opener = e.chair != null && here.Contains(e.chair) ? e.chair : elapsed >= 0.25f ? here.OrderByDescending(Flavor.HumanAge).First() : null;
                if (opener == null) return;
                e.opened = true;
                e.chair = opener;
                VisualTouches.MeetingOpens(e.map, e.spot);
                Say(opener, "Rimfolk_Pool_HustingsOpen", null, null);
                return;
            }
            // Every other step answers the last speech.
            if (e.step % 2 == 1)
            {
                e.step++;
                var last = e.CampaignerOf(lastSpeaker);
                if (last != null && last.spoke) React(e, last, here);
                return;
            }
            var speakers = e.AllCandidates.ToList();
            var next = speakers.FirstOrDefault(c => !(e.CampaignerOf(c)?.spoke ?? false) && here.Contains(c));
            if (next != null)
            {
                Speak(e, next, here);
                e.step++;
                return;
            }
            // Everyone who came has spoken; those still missing past the middle aren't waited for.
            bool waiting = speakers.Any(c => !(e.CampaignerOf(c)?.spoke ?? false) && c.Spawned && c.Map == e.map && !c.Downed);
            if (!waiting || elapsed >= 0.6f) lord.ReceiveMemo(LordJob_Election.DoneMemo);
        }

        [Unsaved] private Pawn lastSpeaker;

        private void Speak(Election e, Pawn c, List<Pawn> here)
        {
            // together: the candidate speaks to those standing at the hustings.
            var rec = e.For(c);
            int social = c.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            float mood = c.needs?.mood?.CurLevelPercentage ?? 0.5f;
            rec.showing = ElectionMath.Showing(social, mood, NatureOnStage(c), Rand.Value);
            rec.spoke = true;
            rec.heard = here.Where(p => p != c).ToList();
            lastSpeaker = c;
            pollCache.Clear();
            var race = MainRace(e, c);
            var entry = race?.EntryOf(c);
            string pool = ElectionMath.Poor(rec.showing) ? "Rimfolk_Pool_HustingsPoor" : "Rimfolk_Pool_HustingsGood";
            string line = Flavor.FromPool(pool, c, null, null, entry?.plank.NullOrEmpty() == false ? "plank:" + entry.plank : null);
            if (!line.NullOrEmpty()) Speech.Say(c, Fill(line, c, null, race?.office), null, SpeechColor);
            if (ElectionMath.Good(rec.showing)) c.skills?.Learn(SkillDefOf.Social, 150f);
        }

        /// <summary>What a candidate's nature does on their feet: the kind and the steady go down well, the abrasive and the nervous less so.</summary>
        private static float NatureOnStage(Pawn c)
        {
            var t = c.story?.traits;
            if (t == null) return 0f;
            float s = 0f;
            if (t.HasTrait(TraitDefOf.Kind)) s += 0.4f;
            if (t.HasTrait(TraitDefOf.Abrasive)) s -= 0.6f;
            var nerves = DefDatabase<TraitDef>.GetNamedSilentFail("Nerves");
            if (nerves != null && t.HasTrait(nerves)) s += Mathf.Clamp(t.DegreeOfTrait(nerves) * 0.25f, -0.5f, 0.5f);
            return s;
        }

        /// <summary>Someone answers a speech: a supporter cheers a good one, a rival's supporter heckles a poor one (never anything that starts a fight).</summary>
        private void React(Election e, ElectionCampaigner rec, List<Pawn> here)
        {
            // together: a cheer or a heckle from someone standing at the hustings.
            var race = MainRace(e, rec.pawn);
            if (race == null) return;
            var listeners = here.Where(p => p != rec.pawn).ToList();
            if (listeners.Count == 0) return;
            var fans = Supporters(race, rec.pawn).Where(listeners.Contains).ToList();
            var others = listeners.Where(p => !fans.Contains(p) && !race.Candidates.Contains(p)).ToList();
            bool cheer = ElectionMath.Good(rec.showing) || (!ElectionMath.Poor(rec.showing) && Rand.Chance(0.6f));
            Pawn who = cheer ? fans.RandomElementWithFallback() ?? (ElectionMath.Good(rec.showing) ? listeners.RandomElement() : null)
                : others.RandomElementWithFallback();
            if (who == null) return;
            Say(who, cheer ? "Rimfolk_Pool_HustingsCheer" : "Rimfolk_Pool_HustingsHeckle", rec.pawn, race.office);
        }

        private void HustingsOver(Election e, bool early)
        {
            // apart: how the hustings went, told in a message.
            var spoke = e.campaigners.Where(c => c.spoke).ToList();
            int standing = e.AllCandidates.Count();
            if (early && spoke.Count * 2 < standing && e.hustingsPutOffs < ElectionMath.MaxPutOffs)
            {
                foreach (var c in spoke)
                {
                    c.spoke = false;
                    c.heard.Clear();
                }
                pollCache.Clear();
                e.hustingsPutOffs++;
                PutOffADay(e, true, true);
                e.phase = ElectionPhase.Campaign;
                Tell.Message(NewsKind.Colony, Importance.Notable, e.map, Phrase(e, "CC_Elect_HustingsBrokenOff"), null, MessageTypeDefOf.NeutralEvent);
                return;
            }
            e.hustingsDone = true;
            e.hustingsHeld = spoke.Count > 0;
            e.phase = ElectionPhase.Polling;
            var good = spoke.Where(c => ElectionMath.Good(c.showing)).Select(c => c.pawn.LabelShort).ToList();
            var poor = spoke.Where(c => ElectionMath.Poor(c.showing)).Select(c => c.pawn.LabelShort).ToList();
            var missing = e.AllCandidates.Where(c => !spoke.Any(s => s.pawn == c)).Select(c => c.LabelShort).ToList();
            var text = new StringBuilder(Phrase(e, early ? "CC_Elect_HustingsOverEarly" : "CC_Elect_HustingsOver"));
            if (good.Count > 0) text.Append(" ").Append("CC_Elect_SpokeWell".Translate(TextUtil.JoinAnd(good)).Resolve());
            if (poor.Count > 0) text.Append(" ").Append("CC_Elect_SpokePoorly".Translate(TextUtil.JoinAnd(poor)).Resolve());
            if (missing.Count > 0) text.Append(" ").Append("CC_Elect_DidNotSpeak".Translate(TextUtil.JoinAnd(missing)).Resolve());
            News.Post(text.ToString(), new LookTargets(new TargetInfo(e.spot, e.map)), NewsKind.Colony, MessageTypeDefOf.NeutralEvent, Importance.Notable);
        }

        // ---------------- Polling day ----------------

        /// <summary>
        /// Polling day's hour: a few at a time walk to the meeting place and cast their vote, in their free time, and from
        /// two in the afternoon from ordinary work too. Nobody is called in danger, from a sickbed, from urgent work or from
        /// what the player ordered; those away can't come.
        /// </summary>
        private void CallToPolls(Election e)
        {
            var map = e.map;
            if (PlayOuts.WhyNotNow(map, false) != null) return;
            if (!e.spot.IsValid || !e.spot.InBounds(map) || !e.spot.Standable(map))
            {
                var finder = Chair(e, map) ?? map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
                if (finder == null || !FindPlace(map, finder, out var spot, out var venue)) return;
                e.spot = spot;
                e.venue = venue;
            }
            if (!pollsAnnounced)
            {
                pollsAnnounced = true;
                Tell.Message(NewsKind.Colony, Importance.Notable, e.map, Phrase(e, "CC_Elect_PollsOpen"), new LookTargets(new TargetInfo(e.spot, map)), MessageTypeDefOf.NeutralEvent);
            }
            int hour = GenLocalDate.HourOfDay(map);
            var left = Electorate(map).Where(p => p.Spawned && p.Map == map && !e.voted.Contains(p)).InRandomOrder().ToList();
            int walking = left.Count(p => p.CurJobDef == VoteJob);
            int hoursLeft = Mathf.Max(1, Mathf.CeilToInt((e.countTick - Now) / (float)GenDate.TicksPerHour));
            int calls = ElectionMath.CallsThisHour(left.Count - walking, hoursLeft) - walking;
            foreach (var p in left.OrderByDescending(p => Together.FreeTime(p) ? 1 : 0))
            {
                if (calls <= 0) break;
                if (p.CurJobDef == VoteJob || !PlayOuts.CanTakePart(p, map) || PlayOuts.NeedsPress(p) || p.GetLord() != null) continue;
                if (PlayOutTracker.Instance?.RunningFor(p) != null) continue;
                bool free = Together.FreeTime(p);
                if (!free && !(hour >= ElectionMath.AfterWorkHour && PlayOuts.Interruptible(p))) continue;
                if (!p.CanReach(e.spot, PathEndMode.Touch, Danger.Some)) continue;
                var job = JobMaker.MakeJob(VoteJob, e.spot);
                p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                if (p.CurJobDef == VoteJob) calls--;
            }
        }

        /// <summary>The polls are announced once a polling day (not saved: after a load, the next hour says it again).</summary>
        [Unsaved] private bool pollsAnnounced;

        /// <summary>
        /// At the ballot table (JobDriver_CastVote): the voter's ranked choice for each seat with a race, with the luck of
        /// the day on each candidate, as the instant vote has it. A word over their head.
        /// </summary>
        public bool CastVote(Pawn p)
        {
            var e = election;
            if (e == null || p == null || e.phase != ElectionPhase.Polling || e.voted.Contains(p)) return false;
            foreach (var race in e.races.Where(r => r.Contested))
            {
                var ranking = race.entries.Select(en => en.pawn).OrderByDescending(c => Score(race.office, p, c)).Select(c => c.thingIDNumber).ToList();
                race.ballots.Add(ElectionMath.Join(ranking));
            }
            e.voted.Add(p);
            // apart: the voter's own word as they step away from the ballot table, said to nobody in particular (Speech: a
            // bubble, or floating text without bubbles).
            string line = Flavor.FromPool("Rimfolk_Pool_ElectionVote", p);
            if (!line.NullOrEmpty()) Speech.Say(p, Fill(line, p, null, null), null, SpeechColor);
            return true;
        }

        /// <summary>
        /// Polling day's evening: on to the count, unless too few of those at home could vote (a raid kept them away), when
        /// the polls stay open another day (twice at most).
        /// </summary>
        private void ClosePolls(Election e)
        {
            var home = Electorate(e.map).Where(p => p.Spawned && p.Map == e.map).ToList();
            int voted = e.voted.Count(home.Contains);
            if (home.Count > 0 && voted < home.Count * ElectionMath.EnoughVoted && e.pollPutOffs < ElectionMath.MaxPutOffs)
            {
                e.pollPutOffs++;
                pollsAnnounced = false;
                e.pollTick = e.countTick - (ElectionMath.EveningHour - ElectionMath.PollsOpenHour) * GenDate.TicksPerHour;
                PutOffADay(e, false, true);
                Tell.Message(NewsKind.Colony, Importance.Notable, e.map, Phrase(e, "CC_Elect_PollsKeptOpen"), null, MessageTypeDefOf.NeutralEvent);
                return;
            }
            pollsAnnounced = false;
            e.phase = ElectionPhase.Count;
        }

        // ---------------- The count ----------------

        /// <summary>One seat at the count: the calls the counter reads, the tally, the winner.</summary>
        public sealed class SeatCount
        {
            public ElectionRace race;
            public List<(Pawn who, int votes)> calls = new List<(Pawn, int)>();
            public List<KeyValuePair<Pawn, int>> ranked = new List<KeyValuePair<Pawn, int>>();
            public Pawn winner;
            public int counted;
            public bool unopposed;
            public bool hands;
            /// <summary>Some ballots went to their next choice (their first was seated in an earlier seat, or is gone).</summary>
            public bool moved;
        }

        [Unsaved] private List<SeatCount> countCache;
        [Unsaved] private int countCacheTick = -1;

        /// <summary>
        /// The count, worked out from the ballots: seat by seat in the order they were set, each ballot going to its first
        /// choice still standing (not seated in an earlier seat, not gone, not holding a seat this election doesn't vote
        /// on); a seat whose ballots were all lost goes to a show of hands; a seat with one name is theirs unopposed.
        /// </summary>
        public List<SeatCount> CountOf(Election e)
        {
            int key = CountKey(e);
            if (countCache != null && countCacheTick == key) return countCache;
            var list = new List<SeatCount>();
            var raced = new HashSet<string>(e.races.Select(r => Key(r.office, r.seat)));
            var taken = new HashSet<Pawn>(holders.Where(kv => !raced.Contains(kv.Key) && kv.Value != null).Select(kv => kv.Value));
            foreach (var race in e.races)
            {
                var sc = new SeatCount { race = race };
                list.Add(sc);
                var standing = race.entries.Select(en => en.pawn).Where(p => p != null && !taken.Contains(p) && !p.Dead).ToList();
                if (standing.Count == 0) continue;
                var votes = new int[standing.Count];
                if (!race.Contested)
                    sc.unopposed = true;
                else if (race.ballots.Count == 0)
                {
                    // Nobody's ballot reached the table: a show of hands among those with a say.
                    sc.hands = true;
                    foreach (var v in Electorate(e.map))
                    {
                        int best = 0;
                        float bestScore = float.MinValue;
                        for (int i = 0; i < standing.Count; i++)
                        {
                            float s = Score(race.office, v, standing[i]);
                            if (s > bestScore)
                            {
                                bestScore = s;
                                best = i;
                            }
                        }
                        votes[best]++;
                    }
                }
                else
                {
                    var ids = standing.Select(p => p.thingIDNumber).ToList();
                    var idSet = new HashSet<int>(ids);
                    var effective = new List<int>();
                    foreach (var b in race.ballots)
                    {
                        var ranking = ElectionMath.Split(b);
                        int vote = ElectionMath.FirstStanding(ranking, idSet);
                        if (vote < 0) continue;
                        if (ranking.Count > 0 && ranking[0] != vote) sc.moved = true;
                        effective.Add(vote);
                        votes[ids.IndexOf(vote)]++;
                    }
                    foreach (var (id, n) in ElectionMath.ReadOut(effective, ElectionMath.CallsPerSeat(e.races.Count(r => r.Contested))))
                        sc.calls.Add((standing[ids.IndexOf(id)], n));
                    if (sc.calls.Count == 0) sc.unopposed = standing.Count == 1;
                }
                sc.counted = votes.Sum();
                int w = ElectionMath.Winner(votes, standing.Select(p => race.office.Competence(p)).ToList());
                sc.winner = standing[w];
                sc.ranked = standing.Select((p, i) => new KeyValuePair<Pawn, int>(p, votes[i]))
                    .OrderByDescending(kv => kv.Key == sc.winner ? 1 : 0).ThenByDescending(kv => kv.Value).ThenByDescending(kv => race.office.Competence(kv.Key)).ToList();
                taken.Add(sc.winner);
            }
            countCache = list;
            countCacheTick = key;
            return list;
        }

        /// <summary>What the count depends on: the seats, who stands, the ballots (a change to any works it out again).</summary>
        private static int CountKey(Election e)
        {
            unchecked
            {
                int k = e.announcedTick;
                foreach (var r in e.races)
                {
                    k = k * 31 + r.office.shortHash + r.seat;
                    foreach (var en in r.entries) k = k * 31 + (en.pawn?.thingIDNumber ?? 0);
                    k = k * 31 + r.ballots.Count;
                }
                return k;
            }
        }

        private void TryCount(CouncilGameComponent comp, Election e)
        {
            string wait = WhyNotGather(e, out var chair);
            if (wait == null)
            {
                var count = CountOf(e);
                StartGathering(e, chair, false, ElectionMath.CountLines(count.Sum(c => c.calls.Count), count.Count));
                Tell.Message(NewsKind.Colony, Importance.Notable, e.map, Phrase(e, e.ceremony ? "CC_Elect_CeremonyStart" : "CC_Elect_CountStart", chair.Named("CHAIR")),
                    new LookTargets(new TargetInfo(e.spot, e.map)), MessageTypeDefOf.PositiveEvent);
                return;
            }
            if (EveningOver(e, e.countTick)) MissedCountEvening(comp, e, wait);
        }

        /// <summary>No calm evening for the count: the next evening (twice at most), then the counter counts at the table alone.</summary>
        private void MissedCountEvening(CouncilGameComponent comp, Election e, string why)
        {
            if (e.countPutOffs < ElectionMath.MaxPutOffs)
            {
                e.countPutOffs++;
                PutOffADay(e, false, false);
                Tell.Message(NewsKind.Colony, Importance.Notable, e.map, Phrase(e, e.ceremony ? "CC_Elect_CeremonyPutOff" : "CC_Elect_CountPutOff", why.Named("REASON"),
                    DateOf(e.countTick, e.map).Named("WHEN")), null, MessageTypeDefOf.NeutralEvent);
                return;
            }
            ApplyResults(comp, e, why);
        }

        /// <summary>
        /// The count's talk, every few seconds (LordJob_Election): the counter opens; then seat by seat they read the
        /// ballots out (vote by vote, or in handfuls when there are many) and name the winner; the room cheers, a loser
        /// takes it well or doesn't; at the end, a toast to the new council. At a ceremony, the counter names each officer.
        /// </summary>
        public void CountTalk(Lord lord)
        {
            // together: said at the count itself, by people standing at the gathering.
            var e = election;
            if (e == null || lord == null || !e.gathering || e.countDone) return;
            var here = Here(e, lord);
            if (here.Count == 0) return;
            var counter = e.chair != null && here.Contains(e.chair) ? e.chair : null;
            if (counter == null)
            {
                // The counter was called away: the eldest there takes over, once a third of the evening has gone.
                if (Elapsed(lord) < 0.33f && e.chair != null && e.chair.Spawned && !e.chair.Downed) return;
                counter = here.OrderByDescending(Flavor.HumanAge).First();
                e.chair = counter;
            }
            if (!e.opened)
            {
                if (!Gathered(e, here, Elapsed(lord))) return;
                e.opened = true;
                VisualTouches.MeetingOpens(e.map, e.spot);
                Say(counter, e.ceremony ? "Rimfolk_Pool_CeremonyOpen" : "Rimfolk_Pool_CountOpen", null, null);
                return;
            }
            var count = CountOf(e);
            if (e.raceStep >= count.Count)
            {
                e.countDone = true;
                Say(counter, "Rimfolk_Pool_ElectionToast", null, null);
                VisualTouches.RaiseCups(here);
                lord.ReceiveMemo(LordJob_Election.DoneMemo);
                return;
            }
            var sc = count[e.raceStep];
            if (sc.winner == null)
            {
                e.raceStep++;
                e.callStep = 0;
                return;
            }
            e.countStarted = true;
            int calls = sc.calls.Count;
            if (e.callStep < calls)
            {
                var (who, n) = sc.calls[e.callStep];
                Say(counter, n == 1 ? "Rimfolk_Pool_CountOne" : "Rimfolk_Pool_CountSome", who, sc.race.office, Number(n));
                e.callStep++;
                return;
            }
            int after = e.callStep - calls;
            e.callStep++;
            if (after == 0)
            {
                Say(counter, sc.unopposed || sc.counted == 0 ? "Rimfolk_Pool_CountUnopposed" : "Rimfolk_Pool_CountWinner", sc.winner, sc.race.office);
                return;
            }
            if (after == 1)
            {
                var cheer = here.Where(p => p != sc.winner && p != counter).RandomElementWithFallback();
                if (cheer != null) Say(cheer, "Rimfolk_Pool_ElectionCheer", sc.winner, sc.race.office);
                return;
            }
            // A loser who is there takes it well, unless it's in their nature to mind (they'll have their mood about it).
            var loser = sc.ranked.Skip(1).Select(kv => kv.Key).FirstOrDefault(p => here.Contains(p) && !Minds(p));
            if (loser != null) Say(loser, "Rimfolk_Pool_ElectionConcede", sc.winner, sc.race.office);
            e.raceStep++;
            e.callStep = 0;
        }

        /// <summary>The greedy, the jealous and the abrasive mind losing (LostElection): they say nothing gracious.</summary>
        private static bool Minds(Pawn p)
        {
            var t = p.story?.traits;
            return t != null && (t.HasTrait(TraitDefOf.Greedy) || t.HasTrait(TraitDefOf.Jealous) || t.HasTrait(TraitDefOf.Abrasive));
        }

        private static string Number(int n)
        {
            var words = "CC_Elect_Numbers".Translate().ToString().Split(',');
            return n >= 0 && n < words.Length ? words[n].Trim() : n.ToString();
        }

        /// <summary>
        /// The result: every seat settled from the count (the instant vote's <see cref="Settle"/>, so the moods, journals,
        /// deeds, names, setbacks and chronicle follow as ever), and the results letter with the tallies, who counted,
        /// and who missed the vote and why. <paramref name="atTable"/>: why there was no gathering to hear it, or null.
        /// </summary>
        private void ApplyResults(CouncilGameComponent comp, Election e, string atTable)
        {
            using var _cause = Causes.Push("CC_Cause_Election".Translate().Resolve());
            // apart: the election's result, told in its letter.
            if (election != e) return;
            var count = CountOf(e);
            var voters = Electorate(e.map);
            var round = new SeatingRound { voters = voters, open = Offices.Where(o => o.OpenFor(e.voterCount)).ToList() };
            // Who ran for which seat, and who campaigned for it: they wanted it (Offices.LostElection).
            foreach (var race in e.races)
                foreach (var en in race.entries)
                    if (en.pawn != null) round.stood.Add((race.office, race.seat, en.pawn));
            foreach (var c in e.campaigners)
                if (c?.pawn != null && (c.spoke || c.canvassed.Count > 0)) round.campaigned.Add(c.pawn);
            election = null;
            lastElectionYear = Mathf.Max(lastElectionYear, e.year);
            lastElectionTick = Now;
            string counter = ChairName(e, e.chair ?? Chair(e, e.map));
            var text = new StringBuilder();
            string when = DateOf(Now, e.map);
            if (e.ceremony)
                text.Append((atTable == null ? "CC_Elect_CeremonyResult" : "CC_Elect_CeremonyResultTable").Translate(counter.Named("CHAIR"), Venue(e).Named("VENUE"),
                    when.Named("DATE"), (atTable ?? "").Named("REASON")).Resolve());
            else
            {
                string head = atTable == null ? "CC_Elect_ResultText" : e.countStarted ? "CC_Elect_ResultBrokenOff" : "CC_Elect_ResultTable";
                text.Append(head.Translate(counter.Named("CHAIR"), Venue(e).Named("VENUE"),
                    when.Named("DATE"), (atTable ?? "").Named("REASON"), e.voted.Count.Named("VOTED"), voters.Count.Named("VOTERS")).Resolve());
                var missed = voters.Where(p => !e.voted.Contains(p)).Select(p => "CC_Elect_MissedOne".Translate(p.LabelShort, MissedWhy(p, e.map)).Resolve()).ToList();
                if (missed.Count > 0) text.Append(" ").Append("CC_Elect_Missed".Translate(TextUtil.JoinAnd(missed)).Resolve());
                if (e.races.Any(r => r.endorsed != null))
                    text.Append(" ").Append("CC_Elect_ResultEndorsed".Translate(TextUtil.JoinAnd(e.races.Where(r => r.endorsed != null)
                        .Select(r => "CC_Elect_ForSeat".Translate(r.endorsed.LabelShort, r.office.label).Resolve()).ToList())).Resolve());
            }
            var seated = new List<Pawn>();
            foreach (var sc in count)
            {
                if (sc.winner == null || sc.winner.Dead) continue;
                string line = sc.unopposed ? "CC_Elect_LineUnopposed".Translate(SeatName(sc.race), sc.winner.Named("PAWN")).Resolve()
                    : sc.hands ? "CC_Elect_LineHands".Translate(SeatName(sc.race), sc.winner.Named("PAWN"), sc.ranked[0].Value, sc.counted).Resolve()
                    : "CC_Election_Line".Translate(SeatName(sc.race), sc.winner.Named("PAWN"), sc.ranked[0].Value, sc.counted).Resolve();
                if (sc.moved) line += " " + "CC_Elect_Moved".Translate().Resolve();
                Settle(comp, round, sc.race.office, sc.race.seat, sc.ranked, line, text);
                seated.Add(sc.winner);
            }
            countCache = null;
            pollCache.Clear();
            string title = (e.byElection ? "CC_Election_ByTitle" : "CC_Election_Title").Translate(e.year).Resolve();
            Finish(comp, round, title, text.ToString(), seated.Count > 0 ? new LookTargets(seated) : LookTargets.Invalid);
        }

        /// <summary>Why someone didn't vote: away, in a sickbed, or simply never got there.</summary>
        private static string MissedWhy(Pawn p, Map map)
        {
            if (!p.Spawned || p.Map != map) return "CC_Elect_Why_Away".Translate();
            if (PlayOuts.MedicalRest(p) || p.Downed) return "CC_Elect_Why_Sick".Translate();
            return "CC_Elect_Why_Busy".Translate();
        }

        // ---------------- Shared ----------------

        /// <summary>Enough have come to begin: a third of those at home (two at least), or a fifth of the evening has gone.</summary>
        private static bool Gathered(Election e, List<Pawn> here, float elapsed) =>
            elapsed >= 0.2f || here.Count >= Mathf.Max(2, e.map.mapPawns.FreeColonistsSpawnedCount / 3);

        private static List<Pawn> Here(Election e, Lord lord) =>
            lord.ownedPawns.Where(p => p != null && p.Spawned && p.Map == e.map && !p.Downed && !p.Drafted && !p.InMentalState && p.Awake()
                                       && GatheringsUtility.InGatheringArea(p.Position, e.spot, e.map)).ToList();

        private static float Elapsed(Lord lord) =>
            lord.LordJob is LordJob_Election job && job.Duration > 0 ? lord.ticksInToil / (float)job.Duration : 1f;

        private static void Say(Pawn speaker, string pool, Pawn other, OfficeDef office, string count = null)
        {
            // together: a line said at an election's gathering, to everyone there.
            string line = Flavor.FromPool(pool, speaker);
            if (line.NullOrEmpty()) return;
            Speech.Say(speaker, Fill(line, speaker, other, office, count).CapitalizeFirst(), null, SpeechColor);
        }
    }
}
