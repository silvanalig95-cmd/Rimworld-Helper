using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>One ambition pitched at a meeting, and who backed it.</summary>
    public class MeetingPitch : IExposable
    {
        public AmbitionDef def;
        public Pawn proposer;
        public string line;
        /// <summary>The proposer made the case in person.</summary>
        public bool spoken;
        /// <summary>The proposer wasn't there, so someone (usually the chair) read the idea out for them.</summary>
        public bool readOut;
        public Pawn reader;
        public List<Pawn> voters = new List<Pawn>();

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_References.Look(ref proposer, "proposer");
            Scribe_Values.Look(ref line, "line");
            Scribe_Values.Look(ref spoken, "spoken");
            Scribe_Values.Look(ref readOut, "readOut");
            Scribe_References.Look(ref reader, "reader");
            Scribe_Collections.Look(ref voters, "voters", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (voters == null) voters = new List<Pawn>();
                voters.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>
    /// A council meeting from the moment it's called until the decision letter: the pitches, the venue, the time it's
    /// set for, who was called and who came, and what was said. It's announced in advance; at the set time the chair
    /// calls everyone together (a vanilla gathering with its own lord job), they talk it over, and only when the
    /// meeting ends does the letter arrive.
    /// </summary>
    public class CouncilMeeting : IExposable
    {
        public Map map;
        public IntVec3 spot = IntVec3.Invalid;
        public string venue;
        public Pawn chair;
        public List<MeetingPitch> pitches = new List<MeetingPitch>();
        /// <summary>Everyone who got there, in the order they arrived.</summary>
        public List<Pawn> attendees = new List<Pawn>();
        public List<string> remarks = new List<string>();
        public int calledTick;
        /// <summary>When the chair will call everyone together.</summary>
        public int scheduledTick = -1;
        public int startedTick = -1;
        public bool calledByPlayer;
        public int talkCount;
        /// <summary>The chair (or whoever stood in) has opened the meeting.</summary>
        public bool opened;
        /// <summary>The "in about an hour" reminder has gone out.</summary>
        public bool reminded;
        /// <summary>Put off once already because too few could come; next time it's decided anyway.</summary>
        public bool thinPostponed;
        /// <summary>Evenings in a row the council couldn't sit down (danger, nowhere to meet).</summary>
        public int failedEvenings;
        /// <summary>Who was called and how it went for each, from the moment the chair called everyone.</summary>
        public List<MeetingInvite> invites = new List<MeetingInvite>();

        /// <summary>Why it hasn't started although it's time (for the council window). Not saved.</summary>
        public string waitReason;
        /// <summary>Decided by fewer than half (set when it ends).</summary>
        public bool thin;

        private Lord lordCache;

        public bool Started => startedTick >= 0;

        public float Elapsed => Started ? (Find.TickManager.TicksGame - startedTick) / (float)LordJob_CouncilMeeting.MeetingTicks : 0f;

        /// <summary>The gathering's lord, while the meeting runs.</summary>
        public Lord Lord
        {
            get
            {
                if (map == null || !Started) return null;
                var lords = map.lordManager.lords;
                if (lordCache != null && lords.Contains(lordCache) && lordCache.LordJob is LordJob_CouncilMeeting) return lordCache;
                lordCache = lords.FirstOrDefault(l => l.LordJob is LordJob_CouncilMeeting);
                return lordCache;
            }
            set => lordCache = value;
        }

        public MeetingInvite InviteOf(Pawn p)
        {
            if (p == null) return null;
            for (int i = 0; i < invites.Count; i++)
                if (invites[i].pawn == p) return invites[i];
            return null;
        }

        public bool Present(Pawn p) => p != null && attendees.Contains(p);

        /// <summary>Came after the first third of the meeting, or had to be woken for it.</summary>
        public bool IsLate(MeetingInvite inv) =>
            inv != null && inv.Arrived && Started
            && (inv.status == MeetingStatus.Woken || MeetingMath.Late(inv.arrivedTick - startedTick));

        public bool IsLate(Pawn p) => IsLate(InviteOf(p));

        /// <summary>Clears what happened, so the meeting can be held again another evening.</summary>
        public void ResetForAnotherTry()
        {
            startedTick = -1;
            talkCount = 0;
            opened = false;
            reminded = false;
            invites.Clear();
            attendees.Clear();
            remarks.Clear();
            waitReason = null;
            lordCache = null;
            foreach (var p in pitches)
            {
                p.spoken = false;
                p.readOut = false;
                p.reader = null;
                p.voters.Clear();
            }
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref spot, "spot", IntVec3.Invalid);
            Scribe_Values.Look(ref venue, "venue");
            Scribe_References.Look(ref chair, "chair");
            Scribe_Collections.Look(ref pitches, "pitches", LookMode.Deep);
            Scribe_Collections.Look(ref attendees, "attendees", LookMode.Reference);
            Scribe_Collections.Look(ref remarks, "remarks", LookMode.Value);
            Scribe_Values.Look(ref calledTick, "calledTick");
            Scribe_Values.Look(ref scheduledTick, "scheduledTick", -1);
            Scribe_Values.Look(ref startedTick, "startedTick", -1);
            Scribe_Values.Look(ref calledByPlayer, "calledByPlayer");
            Scribe_Values.Look(ref talkCount, "talkCount");
            Scribe_Values.Look(ref opened, "opened");
            Scribe_Values.Look(ref reminded, "reminded");
            Scribe_Values.Look(ref thinPostponed, "thinPostponed");
            Scribe_Values.Look(ref failedEvenings, "failedEvenings");
            Scribe_Collections.Look(ref invites, "invites", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (pitches == null) pitches = new List<MeetingPitch>();
                if (attendees == null) attendees = new List<Pawn>();
                if (remarks == null) remarks = new List<string>();
                if (invites == null) invites = new List<MeetingInvite>();
                pitches.RemoveAll(p => p == null || p.def == null || p.proposer == null);
                attendees.RemoveAll(p => p == null);
                invites.RemoveAll(i => i == null || i.pawn == null);
                foreach (var p in pitches)
                    if (p.reader != null && p.reader.Dead) p.reader = null;
                // A meeting begun before meetings were announced has already been opened.
                if (Started && talkCount > 0) opened = true;
            }
        }

        public static GatheringDef GatheringDef => DefDatabase<GatheringDef>.GetNamedSilentFail("Council_MeetingGathering");

        // ---------------- The venue ----------------

        /// <summary>
        /// Where a colony would hold a meeting: around the dining table, else the rec room, else a gather spot
        /// (campfire, party spot), else under the sky near the beds. Returns the venue's name for the letter.
        /// </summary>
        public static bool FindVenue(Map map, Pawn chair, out IntVec3 spot, out string venue)
        {
            // First with the game's own checks for a gathering spot; if those are too picky, without them.
            return FindVenue(map, chair, true, out spot, out venue) || FindVenue(map, chair, false, out spot, out venue);
        }

        private static bool FindVenue(Map map, Pawn chair, bool strict, out IntVec3 spot, out string venue)
        {
            spot = IntVec3.Invalid;
            venue = "CC_Venue_Outside".Translate();
            var gd = GatheringDef;
            bool Ok(IntVec3 c) => c.IsValid && c.InBounds(map) && c.Standable(map) && (chair == null || chair.CanReach(c, Verse.AI.PathEndMode.Touch, Danger.None))
                                  && (!strict || gd == null || GatheringsUtility.ValidateGatheringSpot(c, gd, chair, true, true));

            foreach (var room in RoomUtil.RoomsWithRole(map, RoomUtil.DiningRoom).OrderByDescending(r => r.CellCount))
            {
                var table = room.ContainedAndAdjacentThings.FirstOrDefault(t => t.def.surfaceType == SurfaceType.Eat && t.Faction == Faction.OfPlayer);
                var c = table != null ? FreeCellNear(table.Position, map) : room.Cells.FirstOrDefault(x => x.Standable(map));
                if (Ok(c))
                {
                    spot = c;
                    venue = "CC_Venue_Dining".Translate();
                    return true;
                }
            }
            foreach (var room in RoomUtil.RoomsWithRole(map, RoomUtil.RecRoom).OrderByDescending(r => r.CellCount))
            {
                var c = room.Cells.Where(x => x.Standable(map)).OrderBy(x => x.DistanceToSquared(room.Cells.First())).Skip(room.CellCount / 2).FirstOrDefault();
                if (Ok(c))
                {
                    spot = c;
                    venue = "CC_Venue_RecRoom".Translate();
                    return true;
                }
            }
            var gather = map.gatherSpotLister.activeSpots.Select(s => s.parent).Where(t => t.Faction == Faction.OfPlayer).OrderBy(t => t.def.defName == "Campfire" ? 0 : 1).FirstOrDefault();
            if (gather != null)
            {
                var c = FreeCellNear(gather.Position, map);
                if (Ok(c))
                {
                    spot = c;
                    venue = gather.def.defName.Contains("Campfire") ? "CC_Venue_Campfire".Translate() : "CC_Venue_GatherSpot".Translate(gather.LabelShort);
                    return true;
                }
            }
            var center = TemplatePlanner.ColonyCenter(map);
            foreach (var c in GenRadial.RadialCellsAround(center, 12f, true))
            {
                if (Ok(c) && !c.Roofed(map))
                {
                    spot = c;
                    venue = "CC_Venue_Outside".Translate();
                    return true;
                }
            }
            return false;
        }

        private static IntVec3 FreeCellNear(IntVec3 c, Map map)
        {
            foreach (var x in GenRadial.RadialCellsAround(c, 3f, true))
            {
                if (x.InBounds(map) && x.Standable(map) && x.GetEdifice(map) == null) return x;
            }
            return IntVec3.Invalid;
        }

        // ---------------- Talking ----------------

        /// <summary>
        /// Called every few seconds while the meeting runs: who has arrived, then a pitch, an argument, a joke. The chair
        /// opens; proposers make their case when they get there (late ones too); the chair reads out the ideas of those
        /// who aren't coming, or haven't come by a third of the way in.
        /// </summary>
        public void Talk(Lord lord)
        {
            int now = Find.TickManager.TicksGame;
            var here = lord.ownedPawns.Where(p => p.Spawned && !p.Downed && !p.Drafted && !p.InMentalState && p.Map == map && MeetingAttendance.IsMember(p)
                                                   && GatheringsUtility.InGatheringArea(p.Position, spot, map)).ToList();
            foreach (var p in here) Arrive(p, now);
            if (here.Count == 0) return;
            talkCount++;

            // The chair opens, once there; if the chair is held up, whoever is there gets things going.
            if (!opened)
            {
                var opener = chair != null && here.Contains(chair) ? chair : Elapsed >= MeetingAttendance.LateShare / 2f ? here.RandomElement() : null;
                if (opener == null) return;
                opened = true;
                VisualTouches.MeetingOpens(map, spot);
                string open = Line("Council_Pool_MeetingOpen", opener, null, null);
                if (open != null) CouncilGameComponent.Speak(opener, open, SpeechColor);
                return;
            }

            // Then every proposer makes their case, if they've turned up.
            var next = pitches.FirstOrDefault(p => !p.spoken && here.Contains(p.proposer));
            if (next != null)
            {
                next.spoken = true;
                CouncilGameComponent.Speak(next.proposer, next.line, SpeechColor);
                return;
            }

            // An idea whose proposer isn't coming (or is very late) is read out for them.
            var absent = pitches.FirstOrDefault(p => !p.spoken && !p.readOut && !here.Contains(p.proposer) && (Elapsed >= MeetingAttendance.LateShare || !MayStillCome(p.proposer)));
            if (absent != null)
            {
                var reader = chair != null && here.Contains(chair) && chair != absent.proposer ? chair : here.Where(p => p != absent.proposer).RandomElementWithFallback();
                if (reader != null)
                {
                    absent.readOut = true;
                    absent.reader = reader;
                    string read = Line("Council_Pool_MeetingOnBehalf", reader, absent.proposer, absent.def);
                    if (read != null) CouncilGameComponent.Speak(reader, read, SpeechColor);
                    return;
                }
            }

            // Now and then a confided secret sits too close to the surface (Interplay/).
            if (Interplay.MeetingSecret(this, here)) return;

            // And then everyone has opinions, about the ideas that have been put to the meeting (made in person or read
            // out): mostly ones whose proposer is sitting there to hear it.
            var put = pitches.Where(p => p.spoken || p.readOut).ToList();
            if (put.Count == 0) return;
            var present = put.Where(p => here.Contains(p.proposer)).ToList();
            // Someone responds to an idea that isn't their own (a proposer backing themselves would read oddly).
            var target = present.Count > 0 && (present.Count == put.Count || Rand.Chance(0.75f)) ? present.RandomElement() : put.RandomElement();
            var speaker = here.Where(p => p != target.proposer).RandomElementWithFallback();
            if (speaker == null) return;
            int opinion = speaker.relations != null && target.proposer != null ? speaker.relations.OpinionOf(target.proposer) : 0;
            // Bitter colonists heckle; devoted ones rally behind whoever is speaking.
            var trust = CouncilGameComponent.Instance?.standings.LevelOf(speaker) ?? TrustLevel.Neutral;
            float doubt = trust == TrustLevel.Bitter ? 0.7f : trust == TrustLevel.Doubtful ? 0.5f : trust == TrustLevel.Devoted ? 0.1f : 0.35f;
            string pool;
            if (opinion <= -15 || (opinion < 10 && Rand.Chance(doubt))) pool = "Council_Pool_MeetingDoubt";
            else if (Rand.Chance(0.3f)) pool = "Council_Pool_MeetingAside";
            else pool = "Council_Pool_MeetingSupport";
            string line = Line(pool, speaker, target.proposer, target.def);
            if (line == null) return;
            CouncilGameComponent.Speak(speaker, line, SpeechColor);
            if (remarks.Count < 4 && Rand.Chance(0.5f))
                remarks.Add("CC_Meeting_Remark".Translate(speaker.LabelShort, line).Resolve());
            // Arguing about it brings people closer, or not.
            if (target.proposer != null && target.proposer != speaker && SocialLog.Near(speaker, target.proposer) && Rand.Chance(0.25f))
            {
                bool abrasive = speaker.story?.traits?.HasTrait(TraitDefOf.Abrasive) ?? false;
                var interaction = pool == "Council_Pool_MeetingDoubt" && abrasive ? DefDatabase<InteractionDef>.GetNamedSilentFail("Insult") : InteractionDefOf.Chitchat;
                // Never anything that could start a fist fight: nobody gets hurt over a meeting.
                if (interaction != null && interaction.socialFightBaseChance > 0f) interaction = InteractionDefOf.Chitchat;
                if (interaction != null) speaker.interactions?.TryInteractWith(target.proposer, interaction);
            }
        }

        /// <summary>Someone reached the meeting: noted once, with the time.</summary>
        private void Arrive(Pawn p, int now)
        {
            if (!attendees.Contains(p)) attendees.Add(p);
            var inv = InviteOf(p);
            if (inv == null)
            {
                // Not called (a meeting begun in an older version, or someone who joined the colony since): they came anyway.
                inv = new MeetingInvite { pawn = p, status = MeetingStatus.Coming };
                invites.Add(inv);
            }
            if (!inv.Arrived) inv.arrivedTick = now;
        }

        /// <summary>Called and not here yet, but could still turn up.</summary>
        private bool MayStillCome(Pawn p)
        {
            var inv = InviteOf(p);
            if (inv == null) return invites.Count == 0;
            if (inv.Arrived || inv.released) return false;
            if (!inv.Expected) return inv.status == MeetingStatus.Busy && inv.excused == false && p.Spawned && p.Map == map;
            return p.Spawned && p.Map == map && !p.Downed && !p.Drafted;
        }

        private static readonly Color SpeechColor = new Color(0.7f, 0.85f, 1f);

        private string Line(string pool, Pawn speaker, Pawn proposer, AmbitionDef def)
        {
            string line = Flavor.FromPool(pool, speaker);
            if (line.NullOrEmpty()) return null;
            return line.Formatted(speaker.Named("PAWN"), (proposer ?? speaker).Named("OTHER"), (def?.label ?? "").Named("AMBITION"),
                (venue ?? "").Named("VENUE")).Resolve();
        }

        // ---------------- Voting ----------------

        /// <summary>
        /// Everyone who came votes (and only they): proposers for their own idea, the rest by what they care about
        /// (skills, traits, the colony's troubles) and how much they like the one who proposed it. An idea whose
        /// proposer wasn't there to argue for it gets less support.
        /// </summary>
        public void Vote()
        {
            foreach (var p in pitches) p.voters.Clear();
            var advice = CouncilMapComponent.For(map)?.Signals ?? new List<AdvisorSignal>();
            foreach (var voter in attendees.Where(p => p != null && !p.Dead && p.IsColonist))
            {
                MeetingPitch best = null;
                float bestScore = float.MinValue;
                foreach (var pitch in pitches)
                {
                    float score = Rand.Range(0f, 1.2f);
                    if (pitch.proposer == voter) score += 6f;
                    else if (!Present(pitch.proposer)) score -= 0.8f;
                    if (voter.relations != null && pitch.proposer != null) score += voter.relations.OpinionOf(pitch.proposer) / 35f;
                    foreach (var sk in pitch.def.proposerSkills)
                    {
                        var rec = voter.skills?.GetSkill(sk);
                        if (rec != null && !rec.TotallyDisabled) score += (int)rec.passion * 0.6f;
                    }
                    foreach (var tr in pitch.def.proposerTraits)
                        if (voter.story?.traits?.HasTrait(tr) ?? false) score += 1f;
                    score += advice.Where(s => pitch.def.relevantCategories.Contains(s.def.category)).Sum(s => s.Score) * 0.8f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = pitch;
                    }
                }
                best?.voters.Add(voter);
            }
        }

        public MeetingPitch Favorite => pitches.OrderByDescending(p => p.voters.Count).FirstOrDefault();

        public bool Tied
        {
            get
            {
                var sorted = pitches.OrderByDescending(p => p.voters.Count).ToList();
                return sorted.Count > 1 && sorted[0].voters.Count == sorted[1].voters.Count;
            }
        }
    }

    /// <summary>The gathering itself: colonists drift over, stand around the table and talk it out.</summary>
    public class LordJob_CouncilMeeting : LordJob_Joinable_Gathering
    {
        public const int MeetingTicks = MeetingMath.MeetingTicks;

        public LordJob_CouncilMeeting()
        {
        }

        public LordJob_CouncilMeeting(IntVec3 spot, Pawn organizer, GatheringDef gatheringDef) : base(spot, organizer, gatheringDef)
        {
            durationTicks = MeetingTicks;
        }

        public override bool AllowStartNewGatherings => false;

        protected override LordToil CreateGatheringToil(IntVec3 spot, Pawn organizer, GatheringDef gatheringDef) => new LordToil_Party(spot, gatheringDef, 1.5E-05f);

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var meet = CreateGatheringToil(spot, organizer, gatheringDef);
            graph.AddToil(meet);
            var end = new LordToil_End();
            graph.AddToil(end);

            // A threat, or nobody left to keep order. (The chair stepping out isn't enough: someone else takes over.)
            var calledOff = new Transition(meet, end);
            calledOff.AddTrigger(new Trigger_TickCondition(() => ShouldBeCalledOff(), 60));
            calledOff.AddTrigger(new Trigger_PawnKilled());
            // Third, where the chair leaving used to be, so saves made mid-meeting load their timer into the right trigger.
            calledOff.AddTrigger(new Trigger_Memo(CallOffMemo));
            calledOff.AddPreAction(new TransitionAction_Custom(() => CouncilGameComponent.Instance?.ambitions.EndMeetingSafely(false)));
            graph.AddTransition(calledOff);

            timeoutTrigger = GetTimeoutTrigger();
            var done = new Transition(meet, end);
            done.AddTrigger(timeoutTrigger);
            done.AddPreAction(new TransitionAction_Custom(() => CouncilGameComponent.Instance?.ambitions.EndMeetingSafely(true)));
            graph.AddTransition(done);
            return graph;
        }

        public override void LordJobTick()
        {
            base.LordJobTick();
            if (Find.TickManager.TicksGame % 300 != 0) return;
            try
            {
                CouncilGameComponent.Instance?.ambitions.meeting?.Talk(lord);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("meeting talk", e);
            }
        }

        public override string GetReport(Pawn pawn) => "CC_Meeting_Report".Translate();

        /// <summary>
        /// When the meeting ends, only its own standing about is stopped. Someone called to it who is still finishing
        /// something of their own (a load in their arms, ordinary work they weren't pulled from) carries on, rather than
        /// dropping it where they stand.
        /// </summary>
        public override bool EndPawnJobOnCleanup(Pawn p)
        {
            var job = p?.CurJob;
            if (job != null && (p.carryTracker?.CarriedThing != null || job.workGiverDef != null || job.playerForced)) return false;
            return base.EndPawnJobOnCleanup(p);
        }

        /// <summary>Sent to the lord to end the meeting from outside (it's then wrapped up like any meeting called off).</summary>
        public const string CallOffMemo = "CouncilMeetingCalledOff";

        /// <summary>
        /// Called off by any danger (vanilla gatherings wait for a serious one; nobody stands about talking with a threat
        /// on the map), or when there's nobody left to keep order. A chair who is drafted, ordered elsewhere or leaves
        /// hands over to someone at the meeting. A fault here calls it off rather than recurring every second.
        /// </summary>
        protected override bool ShouldBeCalledOff()
        {
            try
            {
                var map = Map;
                if (map == null) return true;
                if (map.dangerWatcher.DangerRating != StoryDanger.None || !GatheringsUtility.AcceptableGameConditionsToContinueGathering(map)) return true;
                if (ChairCanGoOn(organizer)) return false;
                return !HandOverChair();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("meeting call-off check", e);
                return true;
            }
        }

        private bool ChairCanGoOn(Pawn p) =>
            p != null && !p.Dead && p.Spawned && p.Map == Map && lord != null && lord.ownedPawns.Contains(p) && !p.Drafted && !p.Downed
            && !p.InMentalState && GatheringsUtility.PawnCanStartOrContinueGathering(p);

        private bool HandOverChair()
        {
            if (lord == null) return false;
            var next = lord.ownedPawns.Where(ChairCanGoOn)
                .OrderByDescending(p => GatheringsUtility.InGatheringArea(p.Position, spot, Map) ? 1 : 0)
                .ThenByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0)
                .FirstOrDefault();
            if (next == null) return false;
            organizer = next;
            var m = CouncilGameComponent.Instance?.ambitions.meeting;
            if (m != null) m.chair = next;
            News.Post("CC_Meeting_NewChair".Translate(next.Named("PAWN")).Resolve(), new LookTargets(next), NewsKind.Colony, MessageTypeDefOf.SilentInput);
            return true;
        }

        /// <summary>
        /// Who drifts over on their own: not those who didn't bother or were too far off to be called, not those the
        /// player drafted or sent elsewhere during it, and not anyone without a say. Everyone else may still come when
        /// they're free (late, but they come).
        /// </summary>
        public override float VoluntaryJoinPriorityFor(Pawn p)
        {
            // Asked by every colonist's think tree whenever they look for something to do: a fault is logged once and
            // they're judged as for any gathering.
            try
            {
                var m = CouncilGameComponent.Instance?.ambitions.meeting;
                if (m != null && m.invites.Count > 0)
                {
                    var inv = m.InviteOf(p);
                    if (inv == null ? !MeetingAttendance.IsMember(p) : inv.released || inv.status == MeetingStatus.Skipped || inv.status == MeetingStatus.TooFar)
                        return 0f;
                }
                else
                {
                    // A meeting begun before attendance was kept: bitter colonists mostly don't bother turning up.
                    var comp = CouncilGameComponent.Instance;
                    if (comp != null && comp.standings.LevelOf(p) == TrustLevel.Bitter && (p.thingIDNumber + durationTicks) % 3 != 0) return 0f;
                }
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("meeting join", e);
            }
            return base.VoluntaryJoinPriorityFor(p);
        }
    }

    /// <summary>Required by the gathering def; council meetings are only ever started by the council.</summary>
    public class GatheringWorker_CouncilMeeting : GatheringWorker
    {
        public override bool CanExecute(Map map, Pawn organizer = null) => false;

        protected override bool TryFindGatherSpot(Pawn organizer, out IntVec3 spot) =>
            CouncilMeeting.FindVenue(organizer.Map, organizer, out spot, out _);

        protected override LordJob CreateLordJob(IntVec3 spot, Pawn organizer) => new LordJob_CouncilMeeting(spot, organizer, def);
    }
}
