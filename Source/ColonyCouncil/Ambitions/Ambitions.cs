using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>A bigger colony goal that a colonist pitches at a council meeting.</summary>
    public class AmbitionDef : Def
    {
        public List<AmbitionGoal> goals = new List<AmbitionGoal>();
        public float days = 20f;
        public float weight = 1f;
        /// <summary>Colonists passionate about these skills like to pitch this.</summary>
        public List<SkillDef> proposerSkills = new List<SkillDef>();
        public List<TraitDef> proposerTraits = new List<TraitDef>();
        /// <summary>Advisor categories that make this more likely to come up.</summary>
        public List<AdvisorCategory> relevantCategories = new List<AdvisorCategory>();
        public ToneLines pitch = new ToneLines();
        public ToneLines achieved = new ToneLines();
        public RoomTemplateDef template;
        /// <summary>
        /// The goals are stages, reached one after another: only the current one counts, and each one reached stays
        /// reached. A goal that counts new work starts counting when its stage begins.
        /// </summary>
        public bool staged;
        /// <summary>Only for a colony of at least / at most this many people (a small band, a crowded town).</summary>
        public int minColonists;
        public int maxColonists = 9999;
        /// <summary>Only in these eras, named as the line tags name them (tribal, medieval, industrial, spacer, lowtech, !lowtech).</summary>
        public List<string> eras;

        public bool Possible(Map map)
        {
            if (goals.Count == 0 || !WishParams.EraFits(eras)) return false;
            if (minColonists > 0 || maxColonists < 9999)
            {
                int people = RoomUtil.ColonistCount(map);
                if (people < minColonists || people > maxColonists) return false;
            }
            return goals.All(g => g.Possible(map));
        }

        /// <summary>True when there's nothing left to do (goals counting new work never start out met).</summary>
        public bool AlreadyMet(Map map) => goals.All(g => g.Met(map, g.Baseline(map)));

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (goals == null || goals.Count == 0) yield return "ambition has no goals";
            if (pitch == null || !pitch.Any) yield return "ambition has no pitch lines";
        }
    }

    /// <summary>The ambition the colony is currently working on.</summary>
    public class AmbitionState : IExposable
    {
        public AmbitionDef def;
        public Pawn proposer;
        public Map map;
        public int startTick;
        public int deadlineTick;
        public List<float> baselines = new List<float>();
        /// <summary>How far along it already was when the colony chose it (0 to 1; -1 = not known, an older save).</summary>
        public float startProgress = -1f;
        public bool halfwaySaid;
        public bool deadlineSaid;
        /// <summary>A staged ambition: the stage being worked on (the ones before it are done).</summary>
        public int stage;

        public float Baseline(int i) => baselines != null && i < baselines.Count ? baselines[i] : 0f;

        /// <summary>How far along one goal is: for a staged ambition, done stages are full and later ones empty.</summary>
        public float GoalProgress(int i)
        {
            if (def == null || map == null || i < 0 || i >= def.goals.Count) return 0f;
            if (def.staged)
            {
                if (i < stage) return 1f;
                if (i > stage) return 0f;
            }
            return def.goals[i].Progress(map, Baseline(i));
        }

        /// <summary>A later stage of a staged ambition, not begun yet.</summary>
        public bool Later(int i) => def != null && def.staged && i > stage;

        public float Progress
        {
            get
            {
                if (def == null || map == null) return 0f;
                float sum = 0f;
                for (int i = 0; i < def.goals.Count; i++) sum += Mathf.Clamp01(GoalProgress(i));
                return def.goals.Count == 0 ? 0f : sum / def.goals.Count;
            }
        }

        public bool AllMet
        {
            get
            {
                if (def == null || map == null) return false;
                if (def.staged) return stage >= def.goals.Count;
                for (int i = 0; i < def.goals.Count; i++)
                    if (!def.goals[i].Met(map, Baseline(i))) return false;
                return true;
            }
        }

        /// <summary>
        /// Hourly: goals that keep their own count (a streak of calm days) update it, and a staged ambition moves on to its
        /// next stage once the current one is reached. Returns the stage just reached, or -1.
        /// </summary>
        public int Hourly()
        {
            if (def == null || map == null) return -1;
            if (baselines == null) baselines = new List<float>();
            while (baselines.Count < def.goals.Count) baselines.Add(def.goals[baselines.Count].Baseline(map));
            for (int i = 0; i < def.goals.Count; i++)
            {
                if (def.staged && i != stage) continue;
                baselines[i] = def.goals[i].Update(map, baselines[i]);
            }
            if (!def.staged || stage >= def.goals.Count || !def.goals[stage].Met(map, Baseline(stage))) return -1;
            int reached = stage;
            stage++;
            // The next stage counts new work from now.
            if (stage < def.goals.Count) baselines[stage] = def.goals[stage].Baseline(map);
            return reached;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_References.Look(ref proposer, "proposer");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref deadlineTick, "deadlineTick");
            Scribe_Collections.Look(ref baselines, "baselines", LookMode.Value);
            Scribe_Values.Look(ref startProgress, "startProgress", -1f);
            Scribe_Values.Look(ref halfwaySaid, "halfwaySaid");
            Scribe_Values.Look(ref deadlineSaid, "deadlineSaid");
            Scribe_Values.Look(ref stage, "stage");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && baselines == null) baselines = new List<float>();
        }
    }

    /// <summary>Schedules council meetings and tracks the active ambition.</summary>
    public class AmbitionTracker : IExposable
    {
        public AmbitionState active;
        /// <summary>A meeting that has been called and hasn't ended yet.</summary>
        public CouncilMeeting meeting;
        private int interruptions;
        public int nextMeetingTick = -1;
        public int lastMeetingTick = -999999;
        public int meetingsHeld;
        private Dictionary<string, int> lastOffered = new Dictionary<string, int>();
        /// <summary>When a meeting due to start last looked for somewhere to meet and found nowhere. Not saved.</summary>
        private int venueSearchTick = -999999;
        private const int VenueSearchInterval = 600;
        private List<string> tmpK;
        private List<int> tmpV;

        public void ExposeData()
        {
            Scribe_Deep.Look(ref active, "active");
            Scribe_Deep.Look(ref meeting, "meeting");
            Scribe_Values.Look(ref interruptions, "interruptions");
            Scribe_Values.Look(ref nextMeetingTick, "nextMeetingTick", -1);
            Scribe_Values.Look(ref lastMeetingTick, "lastMeetingTick", -999999);
            Scribe_Values.Look(ref meetingsHeld, "meetingsHeld");
            Scribe_Collections.Look(ref lastOffered, "lastOffered", LookMode.Value, LookMode.Value, ref tmpK, ref tmpV);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (lastOffered == null) lastOffered = new Dictionary<string, int>();
                if (active != null && (active.def == null || active.map == null)) active = null;
                if (meeting != null && (meeting.map == null || meeting.pitches.Count == 0)) meeting = null;
            }
        }

        private static CouncilSettings Settings => ColonyCouncilMod.Settings;

        public int TicksUntilMeeting => nextMeetingTick < 0 ? -1 : nextMeetingTick - Find.TickManager.TicksGame;

        public bool CanCallMeetingNow(out string reason)
        {
            reason = null;
            if (active != null)
            {
                reason = "CC_Meeting_BusyAmbition".Translate();
                return false;
            }
            if (meeting != null)
            {
                reason = "CC_Meeting_AlreadyCalled".Translate();
                return false;
            }
            int since = Find.TickManager.TicksGame - lastMeetingTick;
            if (since < GenDate.TicksPerDay * 2)
            {
                reason = "CC_Meeting_TooSoon".Translate(TextUtil.DaysString(GenDate.TicksPerDay * 2 - since));
                return false;
            }
            if (MeetingMap() == null)
            {
                reason = "CC_Meeting_NoOne".Translate();
                return false;
            }
            return true;
        }

        public void HourlyTick(CouncilGameComponent comp)
        {
            int now = Find.TickManager.TicksGame;
            if (nextMeetingTick < 0)
                nextMeetingTick = now + Mathf.RoundToInt(GenDate.TicksPerDay * Mathf.Min(4f, Settings.daysBetweenMeetings));

            if (active != null)
            {
                if (active.map != null && Find.Maps.Contains(active.map)) StageCheck(active);
                if (active.map == null || !Find.Maps.Contains(active.map))
                {
                    active = null;
                }
                else if (active.AllMet)
                {
                    Complete(comp);
                }
                else if (now > active.deadlineTick)
                {
                    Fail(comp);
                }
                else
                {
                    CheckIn(active, now);
                }
                if (active != null && now >= nextMeetingTick) nextMeetingTick = now + GenDate.TicksPerDay;
                return;
            }

            if (meeting != null)
            {
                // The rest (start time, reminder, waking) runs every few seconds in MeetingTick.
                if (meeting.map == null || !Find.Maps.Contains(meeting.map)) meeting = null;
                return;
            }
            if (now < nextMeetingTick) return;
            // Nobody calls a meeting in the middle of the night: it waits for the morning.
            var home = MeetingMap();
            if (home != null && MeetingMath.IsNight(GenLocalDate.HourOfDay(home))) return;
            HoldMeeting(comp, false);
        }

        /// <summary>Streaks counted, and a staged ambition's stage reached: a message names the next one.</summary>
        private static void StageCheck(AmbitionState a)
        {
            int reached;
            try
            {
                reached = a.Hourly();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("ambition hourly " + a.def?.defName, e);
                return;
            }
            if (reached < 0 || a.def == null || a.stage >= a.def.goals.Count) return;
            string next = a.def.goals[a.stage].Describe(a.map, a.Baseline(a.stage));
            News.Post("CC_Ambition_StageReached".Translate(a.def.LabelCap.ToString().Named("AMBITION"), (reached + 1).Named("STAGE"),
                a.def.goals.Count.Named("COUNT"), next.Named("NEXT")).Resolve(), new LookTargets(a.proposer), NewsKind.Colony, MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>The proposer cheers at halfway and frets when time is short and the work isn't.</summary>
        private void CheckIn(AmbitionState a, int now)
        {
            var p = a.proposer;
            if (p == null || p.Dead || !p.Spawned || p.Map != a.map) return;
            float progress = a.Progress;
            string pool = null;
            if (!a.halfwaySaid && progress >= 0.5f)
            {
                a.halfwaySaid = true;
                pool = "Council_Pool_AmbitionHalfway";
            }
            else if (!a.deadlineSaid && a.deadlineTick - now < GenDate.TicksPerDay * 3 && progress < 0.75f)
            {
                a.deadlineSaid = true;
                pool = "Council_Pool_AmbitionDeadline";
            }
            if (pool == null) return;
            string line = Flavor.FromPool(pool, p);
            if (line.NullOrEmpty()) return;
            line = line.Formatted(p.Named("PAWN"), a.def.label.Named("AMBITION")).Resolve();
            CouncilGameComponent.Speak(p, line, new Color(0.7f, 0.85f, 1f));
            News.Post("CC_Ambition_CheckIn".Translate(p.Named("PAWN"), a.def.label.Named("AMBITION"), progress.ToStringPercent().Named("PROGRESS")).Resolve() + " \"" + TextUtil.ShortSpeech(line, 120) + "\"",
                new LookTargets(p), NewsKind.Colony, MessageTypeDefOf.SilentInput);
        }

        public static Map MeetingMap()
        {
            Map best = null;
            int bestCount = 0;
            foreach (var m in Find.Maps)
            {
                if (!m.IsPlayerHome) continue;
                int n = m.mapPawns.FreeColonistsSpawned.Count(CouncilGameComponent.CanVoiceWishes);
                if (n > bestCount)
                {
                    best = m;
                    bestCount = n;
                }
            }
            return best;
        }

        private struct Pitch
        {
            public AmbitionDef def;
            public Pawn proposer;
        }

        public void HoldMeeting(CouncilGameComponent comp, bool calledByPlayer)
        {
            int now = Find.TickManager.TicksGame;
            nextMeetingTick = now + Mathf.RoundToInt(GenDate.TicksPerDay * Settings.daysBetweenMeetings);
            var map = MeetingMap();
            if (map == null) return;
            var speakers = map.mapPawns.FreeColonistsSpawned.Where(CouncilGameComponent.CanVoiceWishes).ToList();
            if (speakers.Count == 0) return;

            var advice = CouncilMapComponent.For(map)?.Signals ?? new List<AdvisorSignal>();
            var pool = new List<(AmbitionDef def, float w)>();
            foreach (var def in DefDatabase<AmbitionDef>.AllDefsListForReading)
            {
                bool possible;
                bool met;
                try
                {
                    possible = def.Possible(map);
                    met = possible && def.AlreadyMet(map);
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("ambition " + def.defName, e);
                    continue;
                }
                if (!possible || met) continue;
                float relevance = advice.Where(s => def.relevantCategories.Contains(s.def.category)).Sum(s => s.Score);
                float w = def.weight * (1f + 3f * relevance);
                if (lastOffered.TryGetValue(def.defName, out int when) && now - when < GenDate.TicksPerDay * 30) w *= 0.25f;
                pool.Add((def, w));
            }
            if (pool.Count == 0)
            {
                if (calledByPlayer) Messages.Message("CC_Meeting_NothingToPropose".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            var pitches = new List<Pitch>();
            var usedSpeakers = new HashSet<Pawn>();
            while (pitches.Count < 3 && pool.Count > 0)
            {
                var pick = pool.RandomElementByWeight(p => p.w);
                pool.Remove(pick);
                var proposer = BestProposer(pick.def, speakers, usedSpeakers);
                if (proposer == null) continue;
                usedSpeakers.Add(proposer);
                pitches.Add(new Pitch { def = pick.def, proposer = proposer });
                lastOffered[pick.def.defName] = now;
            }
            if (pitches.Count == 0) return;

            lastMeetingTick = now;
            interruptions = 0;
            meeting = new CouncilMeeting
            {
                map = map,
                calledTick = now,
                calledByPlayer = calledByPlayer,
                chair = PickChair(speakers.Where(MeetingAttendance.CanChair).ToList(), comp) ?? PickChair(speakers, comp),
                pitches = pitches.Select(p => new MeetingPitch
                {
                    def = p.def,
                    proposer = p.proposer,
                    line = (p.def.pitch.Pick(ToneUtility.ToneFor(p.proposer), Flavor.TagsFor(p.proposer)) ?? p.def.LabelCap.ToString()).Formatted(p.proposer.Named("PAWN")).Resolve()
                }).ToList()
            };
            // Only one colonist to speak, or no way to gather: the council "meets" in passing and the letter comes at once.
            if (speakers.Count < 2 || CouncilMeeting.GatheringDef == null)
            {
                meeting.attendees.AddRange(speakers);
                Conclude(comp, true);
                return;
            }
            // Otherwise it's set for a time and announced: the next evening, or in an hour or two when called from the window
            // (the next evening if that would be at night).
            meeting.scheduledTick = calledByPlayer
                ? now + Mathf.RoundToInt(MeetingMath.HoursToCalledMeeting(GenLocalDate.HourFloat(map), Rand.Range(1f, 2f)) * GenDate.TicksPerHour)
                : MeetingAttendance.NextEvening(map, now, GenDate.TicksPerHour * 2);
            if (meeting.chair != null && CouncilMeeting.FindVenue(map, meeting.chair, out var spot, out var venue))
            {
                meeting.spot = spot;
                meeting.venue = venue;
            }
            Announce(meeting);
        }

        /// <summary>"Mira has called a council meeting around the dining table, this evening. On the table: ..."</summary>
        private static void Announce(CouncilMeeting m)
        {
            int now = Find.TickManager.TicksGame;
            string text = AnnouncementText(m, now);
            var look = m.spot.IsValid ? new LookTargets(new TargetInfo(m.spot, m.map)) : new LookTargets(m.chair);
            Messages.Message(text, look, MessageTypeDefOf.NeutralEvent, false);
        }

        public static string AnnouncementText(CouncilMeeting m, int now)
        {
            string when = MeetingAttendance.When(m.map, now, m.scheduledTick);
            string topics = TextUtil.JoinAnd(m.pitches.Select(p => p.def.label).ToList());
            var chair = m.chair;
            if (chair == null) return "CC_Meeting_AnnouncedNoChair".Translate(when.Named("WHEN"), topics.Named("TOPICS")).Resolve();
            return (m.venue.NullOrEmpty() ? "CC_Meeting_AnnouncedNoVenue" : "CC_Meeting_Announced").Translate(chair.Named("CHAIR"), (m.venue ?? "").Named("VENUE"),
                when.Named("WHEN"), topics.Named("TOPICS")).Resolve();
        }

        /// <summary>Who runs the meeting: the best talker, helped by how much the others trust them.</summary>
        private static Pawn PickChair(List<Pawn> speakers, CouncilGameComponent comp)
        {
            var speaker = comp.offices.Speaker;
            if (speaker != null && speakers.Contains(speaker)) return speaker;
            return speakers.OrderByDescending(p => (p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0) + comp.standings.TrustOf(p) / 20f + Rand.Value).FirstOrDefault();
        }

        /// <summary>
        /// Every second or so while a meeting is called (from the game component): the reminder, the start at the set
        /// time, sleepers being fetched, colonists drafted away, and the gathering's end if it vanished.
        /// </summary>
        public void MeetingTick(CouncilGameComponent comp)
        {
            var m = meeting;
            if (m == null) return;
            if (m.map == null || !Find.Maps.Contains(m.map))
            {
                meeting = null;
                return;
            }
            int now = Find.TickManager.TicksGame;
            // An ambition was chosen meanwhile (from an older letter): there's nothing left to meet about.
            if (active != null && !m.Started)
            {
                meeting = null;
                return;
            }
            if (m.Started)
            {
                MeetingAttendance.ReleaseCalledAway(m);
                // Letting someone go can end the meeting there and then: nobody is woken for one that's over.
                if (meeting != m || !m.Started) return;
                MeetingAttendance.WakeUpkeep(m, now);
                // The gathering vanished without ending (all its people gone): wrap up, or try again another evening.
                if (meeting == m && m.Lord == null && now - m.startedTick > 300) EndMeetingSafely(false);
                return;
            }
            if (m.scheduledTick < 0)
            {
                // Called before meetings were announced: set it for the next evening now.
                m.scheduledTick = MeetingAttendance.NextEvening(m.map, now, GenDate.TicksPerHour * 2);
                Announce(m);
            }
            if (!m.reminded && now < m.scheduledTick && m.scheduledTick - now <= GenDate.TicksPerHour && m.scheduledTick - m.calledTick > GenDate.TicksPerHour * 3)
            {
                m.reminded = true;
                string text = m.venue.NullOrEmpty() ? "CC_Meeting_ReminderNoVenue".Translate().Resolve() : "CC_Meeting_Reminder".Translate(m.venue.Named("VENUE")).Resolve();
                var look = m.spot.IsValid ? new LookTargets(new TargetInfo(m.spot, m.map)) : LookTargets.Invalid;
                Messages.Message(text, look, MessageTypeDefOf.SilentInput, false);
            }
            if (now >= m.scheduledTick) TryStartMeeting(comp);
        }

        /// <summary>From the council window: call everyone together right away (not at night, with the colony asleep).</summary>
        public void HoldNow()
        {
            if (!CanHoldNow) return;
            meeting.scheduledTick = Find.TickManager.TicksGame;
            meeting.reminded = true;
        }

        public bool CanHoldNow =>
            meeting != null && !meeting.Started && meeting.map != null && Find.TickManager.TicksGame < meeting.scheduledTick
            && !MeetingMath.IsNight(GenLocalDate.HourFloat(meeting.map));

        /// <summary>From the council window: the same time tomorrow (never to the night: then the next evening).</summary>
        public void PutOffADay()
        {
            if (!CanPutOffADay) return;
            int now = Find.TickManager.TicksGame;
            int when = Mathf.Max(meeting.scheduledTick, now) + GenDate.TicksPerDay;
            if (MeetingAttendance.AtNight(meeting.map, now, when)) when = MeetingAttendance.NextEvening(meeting.map, now, GenDate.TicksPerHour * 8);
            meeting.scheduledTick = when;
            meeting.reminded = false;
            meeting.waitReason = null;
            Messages.Message("CC_Meeting_PutOffDay".Translate(), MessageTypeDefOf.SilentInput, false);
        }

        /// <summary>Not running, and not already a day or more off (so a double click doesn't put it off twice).</summary>
        public bool CanPutOffADay =>
            meeting != null && !meeting.Started && meeting.map != null && meeting.scheduledTick - Find.TickManager.TicksGame < GenDate.TicksPerDay;

        /// <summary>
        /// At the set time: if it's safe and there's a chair and a place, the chair calls everyone. Each colonist's part
        /// is decided once (<see cref="MeetingAttendance.Classify"/>); if too few can come, it's put off once to the next
        /// evening. If it can't be held all evening, it's tried the next; after three such evenings the chair settles it alone.
        /// </summary>
        private void TryStartMeeting(CouncilGameComponent comp)
        {
            var m = meeting;
            var map = m.map;
            var gd = CouncilMeeting.GatheringDef;
            int now = Find.TickManager.TicksGame;
            // Ideas whose proposer has died or left are dropped; with none left there's nothing to meet about.
            m.pitches.RemoveAll(p => p.proposer == null || !MeetingAttendance.IsMember(p.proposer));
            if (m.pitches.Count == 0 || gd == null)
            {
                meeting = null;
                nextMeetingTick = now + GenDate.TicksPerDay;
                Messages.Message("CC_Meeting_Cancelled".Translate(), MessageTypeDefOf.NeutralEvent, false);
                return;
            }
            string wait = MeetingAttendance.WhyNotNow(map);
            Pawn chair = null;
            if (wait == null)
            {
                chair = m.chair != null && m.chair.Map == map && MeetingAttendance.CanChair(m.chair)
                    ? m.chair
                    : PickChair(map.mapPawns.FreeColonistsSpawned.Where(MeetingAttendance.CanChair).ToList(), comp);
                if (chair == null) wait = "CC_MeetWait_NoChair".Translate();
            }
            var spot = m.spot;
            string venue = m.venue;
            if (wait == null && !VenueStillGood(m, chair))
            {
                // Searching the whole colony is costly: while there's nowhere to meet, it's tried again every ten seconds or so.
                bool found = now - venueSearchTick >= VenueSearchInterval && CouncilMeeting.FindVenue(map, chair, out spot, out venue);
                if (!found)
                {
                    if (now - venueSearchTick >= VenueSearchInterval) venueSearchTick = now;
                    wait = "CC_MeetWait_NoVenue".Translate();
                }
            }
            if (wait != null)
            {
                m.waitReason = wait;
                if (EveningOver(m, now)) MissedEvening(comp, wait);
                return;
            }
            m.waitReason = null;
            m.chair = chair;
            m.spot = spot;
            m.venue = venue;

            // Who comes, decided once for everyone with a say.
            bool callFromWork = Settings.meetingsCallFromWork;
            var members = MeetingAttendance.Members(map);
            if (!members.Contains(chair)) members.Insert(0, chair);
            m.invites = members.Select(p => MeetingAttendance.ClassifySafely(p, m, callFromWork, p == chair)).ToList();
            var expected = m.invites.Where(i => i.Expected).ToList();
            if (expected.Count < MeetingAttendance.Quorum(m.invites.Count) && !m.thinPostponed)
            {
                m.thinPostponed = true;
                string names = TextUtil.JoinAnd(expected.Select(i => i.pawn.LabelShort).ToList());
                Reschedule(m, now);
                Messages.Message("CC_Meeting_PutOffThin".Translate(names.Named("NAMES"), chair.Named("CHAIR"), MeetingAttendance.When(map, now, m.scheduledTick).Named("WHEN")).Resolve(),
                    new LookTargets(chair), MessageTypeDefOf.NeutralEvent, false);
                return;
            }

            m.startedTick = now;
            m.talkCount = 0;
            m.opened = false;
            m.failedEvenings = 0;
            var starting = new List<Pawn> { chair };
            if (callFromWork)
                starting.AddRange(m.invites.Where(i => i.status == MeetingStatus.Coming && i.pawn != chair && i.pawn.GetLord() == null).Select(i => i.pawn));
            var lord = LordMaker.MakeNewLord(Faction.OfPlayer, new LordJob_CouncilMeeting(spot, chair, gd), map, starting);
            m.Lord = lord;
            foreach (var p in starting) MeetingAttendance.CallFromWork(p);
            if (callFromWork) MeetingAttendance.ArrangeWaking(m, lord);
            Messages.Message(SummonsText(m, callFromWork), new LookTargets(new TargetInfo(spot, map)), MessageTypeDefOf.PositiveEvent, false);
            meetingsHeld++;
        }

        private static bool VenueStillGood(CouncilMeeting m, Pawn chair) =>
            m.spot.IsValid && !m.venue.NullOrEmpty() && m.spot.InBounds(m.map) && m.spot.Standable(m.map) && chair != null
            && chair.CanReach(m.spot, Verse.AI.PathEndMode.Touch, Danger.None);

        /// <summary>The evening is gone: four hours past the set time, or night has come (the clock never starts one at night).</summary>
        private static bool EveningOver(CouncilMeeting m, int now) =>
            now - m.scheduledTick > GenDate.TicksPerHour * 4 || (now > m.scheduledTick && MeetingMath.IsNight(GenLocalDate.HourFloat(m.map)));

        private void MissedEvening(CouncilGameComponent comp, string why)
        {
            var m = meeting;
            m.failedEvenings++;
            if (m.failedEvenings >= 3)
            {
                SettleAlone(comp);
                return;
            }
            int now = Find.TickManager.TicksGame;
            Reschedule(m, now);
            Messages.Message("CC_Meeting_PutOffEvening".Translate(why.Named("REASON"), MeetingAttendance.When(m.map, now, m.scheduledTick).Named("WHEN")).Resolve(),
                MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>To the next evening (at least eight hours off, so never the same evening), starting afresh.</summary>
        private static void Reschedule(CouncilMeeting m, int now)
        {
            m.ResetForAnotherTry();
            m.scheduledTick = MeetingAttendance.NextEvening(m.map, now, GenDate.TicksPerHour * 8);
        }

        /// <summary>Three evenings without a calm moment: the chair looks over the proposals alone. It costs a little trust.</summary>
        private void SettleAlone(CouncilGameComponent comp)
        {
            var m = meeting;
            var members = MeetingAttendance.Members(m.map);
            var chair = m.chair != null && MeetingAttendance.IsMember(m.chair) ? m.chair
                : members.Count > 0 ? PickChair(members, comp) : m.pitches.Select(p => p.proposer).FirstOrDefault(p => p != null && !p.Dead);
            m.chair = chair;
            m.attendees.Clear();
            if (chair != null) m.attendees.Add(chair);
            if (Settings.Negatives)
                foreach (var p in members)
                    if (p != chair) comp.standings.Change(p, -1f, "CC_Log_SettledAlone".Translate());
            Conclude(comp, false, true);
        }

        private static string SummonsText(CouncilMeeting m, bool callFromWork)
        {
            string topics = TextUtil.JoinAnd(m.pitches.Select(p => p.def.label).ToList());
            string text = (callFromWork ? "CC_Meeting_Summoned" : "CC_Meeting_Gathering").Translate(m.chair.Named("PAWN"), (m.venue ?? "").Named("VENUE"), topics.Named("TOPICS")).Resolve();
            var woken = m.invites.Where(i => i.status == MeetingStatus.Woken).Select(i => i.pawn.LabelShort).ToList();
            if (woken.Count > 0) text += " " + (woken.Count == 1 ? "CC_Meeting_WakingOne" : "CC_Meeting_WakingMany").Translate(TextUtil.JoinAnd(woken)).Resolve();
            var cant = m.invites.Where(i => !i.Expected && i.status != MeetingStatus.Skipped).Select(i => i.pawn.LabelShort).ToList();
            if (cant.Count > 0) text += " " + "CC_Meeting_CantCome".Translate(TextUtil.JoinAnd(cant)).Resolve();
            return text;
        }

        /// <summary>
        /// <see cref="EndMeeting"/> from the gathering's own transitions (or when it has vanished): a fault while wrapping
        /// up is logged once and the meeting dropped, so it can't throw again on every tick or hang "in session".
        /// </summary>
        public void EndMeetingSafely(bool finished)
        {
            var m = meeting;
            try
            {
                EndMeeting(finished);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("meeting end", e);
                if (meeting == m) meeting = null;
            }
        }

        /// <summary>Called by the gathering when it ends: finished, or broken up early (then tried again the next evening).</summary>
        public void EndMeeting(bool finished)
        {
            var comp = CouncilGameComponent.Instance;
            var m = meeting;
            if (comp == null || m == null || !m.Started) return;
            if (!finished && m.Elapsed < 0.5f && interruptions < 2)
            {
                interruptions++;
                int now = Find.TickManager.TicksGame;
                Reschedule(m, now);
                Messages.Message("CC_Meeting_BrokenUpAgain".Translate(MeetingAttendance.When(m.map, now, m.scheduledTick).Named("WHEN")).Resolve(), MessageTypeDefOf.NeutralEvent, false);
                return;
            }
            Conclude(comp, false);
        }

        private void Conclude(CouncilGameComponent comp, bool inPassing, bool alone = false)
        {
            using var _cause = Causes.Push("CC_Cause_Meeting".Translate().Resolve());
            var m = meeting;
            meeting = null;
            if (m == null || m.map == null) return;
            m.attendees.RemoveAll(p => p == null || p.Dead);
            if (m.attendees.Count == 0)
            {
                if (m.chair != null && !m.chair.Dead) m.attendees.Add(m.chair);
                else m.attendees.AddRange(m.pitches.Select(p => p.proposer).Where(p => p != null && !p.Dead));
            }
            bool gathered = !inPassing && !alone && m.invites.Count > 0;
            m.thin = gathered && m.attendees.Count < MeetingAttendance.Quorum(m.invites.Count);
            m.Vote();
            if (!alone)
            {
                foreach (var p in m.attendees)
                {
                    bool late = gathered && m.IsLate(p);
                    if (p.needs?.mood != null && Rand.Chance(0.8f))
                        CouncilGameComponent.GiveThought(p, late ? CouncilDefOf.Council_HeardAtMeetingLate : CouncilDefOf.Council_HeardAtMeeting);
                    comp.standings.Change(p, m.thin ? 0.5f : 1f, null);
                }
            }
            if (gathered) MeetingAttendance.AbsenteeEffects(m);
            SendMeetingLetter(comp, m, inPassing, alone);
        }

        private static Pawn BestProposer(AmbitionDef def, List<Pawn> speakers, HashSet<Pawn> used)
        {
            Pawn best = null;
            float bestScore = -1f;
            foreach (var p in speakers)
            {
                float score = Rand.Range(0f, 1f);
                if (used.Contains(p)) score -= 3f;
                foreach (var sk in def.proposerSkills)
                {
                    var rec = p.skills?.GetSkill(sk);
                    if (rec == null || rec.TotallyDisabled) continue;
                    score += (int)rec.passion * 1.5f + rec.Level * 0.1f;
                }
                foreach (var tr in def.proposerTraits)
                    if (p.story?.traits?.HasTrait(tr) ?? false) score += 2f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }
            return best;
        }

        private void SendMeetingLetter(CouncilGameComponent comp, CouncilMeeting m, bool inPassing, bool alone)
        {
            var map = m.map;
            var sb = new StringBuilder();
            string venue = m.venue ?? MeetingVenue(map);
            bool gathered = !inPassing && !alone && m.invites.Count > 0;
            string chairName = m.chair?.LabelShort ?? m.attendees.FirstOrDefault()?.LabelShort ?? "?";
            if (alone)
                sb.AppendLine("CC_Meeting_IntroAlone".Translate(chairName).Resolve());
            else if (inPassing && m.attendees.Count <= 1)
                sb.AppendLine("CC_Meeting_IntroSolo".Translate(chairName).Resolve());
            else if (inPassing || !m.Started && m.attendees.Count <= 1)
                sb.AppendLine("CC_Meeting_Intro".Translate(venue).Resolve());
            else
            {
                // Where and who kept order, who came and who didn't (and why), then the ideas.
                sb.AppendLine("CC_Meeting_IntroMet".Translate(venue.Named("VENUE"), chairName.Named("CHAIR")).Resolve());
                string who = gathered ? MeetingAttendance.Paragraph(m, m.thin) : "CC_Meeting_CameNames".Translate(TextUtil.JoinAnd(m.attendees.Select(p => p.LabelShort).ToList())).Resolve();
                if (!who.NullOrEmpty())
                {
                    sb.AppendLine();
                    sb.AppendLine(who);
                }
                sb.AppendLine();
                sb.AppendLine("CC_Meeting_Proposals".Translate().Resolve());
            }
            sb.AppendLine();
            int i = 1;
            foreach (var p in m.pitches)
            {
                string by;
                if (!gathered || m.Present(p.proposer)) by = "CC_Meeting_ProposedBy".Translate(p.proposer.LabelShort);
                else if (p.readOut && p.reader != null) by = "CC_Meeting_ProposedByAbsent".Translate(p.proposer.LabelShort, p.reader.LabelShort);
                else by = "CC_Meeting_ProposedByMissing".Translate(p.proposer.LabelShort);
                sb.AppendLine("<b>" + i + ". " + p.def.LabelCap + "</b> (" + by + ")");
                sb.AppendLine("\"" + p.line + "\"");
                if (p.def.staged) sb.AppendLine("  " + "CC_Ambition_InStages".Translate());
                int n = 1;
                foreach (var g in p.def.goals) sb.AppendLine("  " + (p.def.staged ? n++ + "." : "-") + " " + g.Describe(map, g.Baseline(map)));
                sb.AppendLine("  " + "CC_Meeting_Deadline".Translate((p.def.days * Settings.ambitionDeadlineFactor).ToString("0")));
                if (m.attendees.Count > 1)
                    sb.AppendLine("  " + "CC_Meeting_Votes".Translate(p.voters.Count, TextUtil.JoinAnd(p.voters.Select(v => v.LabelShort).ToList())));
                sb.AppendLine();
                i++;
            }
            foreach (var r in m.remarks) sb.AppendLine(r);
            if (m.remarks.Count == 0)
            {
                string heckle = Heckle(m.attendees, m.pitches);
                if (!heckle.NullOrEmpty()) sb.AppendLine(heckle);
            }
            sb.AppendLine();
            var fav = m.Favorite;
            if (m.attendees.Count > 1 && fav != null)
                sb.AppendLine(m.Tied ? "CC_Meeting_Split".Translate().Resolve() : "CC_Meeting_Leans".Translate(fav.def.label.Named("AMBITION"), fav.proposer.Named("PAWN")).Resolve());
            sb.Append("CC_Meeting_Outro".Translate());

            var letterDef = DefDatabase<LetterDef>.GetNamedSilentFail("Council_MeetingLetter");
            if (letterDef == null) return;
            var letter = (ChoiceLetter_CouncilMeeting)LetterMaker.MakeLetter("CC_Meeting_Title".Translate(), sb.ToString().TrimEnd(), letterDef, new LookTargets(m.pitches.Select(p => p.proposer)));
            letter.map = map;
            letter.options = m.pitches.Select(p => p.def).ToList();
            letter.proposers = m.pitches.Select(p => p.proposer).ToList();
            letter.thin = m.thin;
            // Proposers who weren't there (and didn't choose to stay away) will take it hard if their idea is turned down.
            letter.absentProposers = gathered
                ? m.pitches.Select(p => p.proposer).Where(p => p != null && !m.Present(p) && m.InviteOf(p)?.status != MeetingStatus.Skipped).ToList()
                : new List<Pawn>();
            letter.voterPawns = new List<Pawn>();
            letter.voterChoice = new List<int>();
            for (int k = 0; k < m.pitches.Count; k++)
            {
                foreach (var v in m.pitches[k].voters)
                {
                    letter.voterPawns.Add(v);
                    letter.voterChoice.Add(k);
                }
            }
            Find.LetterStack.ReceiveLetter(letter);

            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Meeting,
                "CC_Chronicle_Meeting".Translate(TextUtil.JoinAnd(m.pitches.Select(p => p.def.label + " (" + p.proposer.LabelShort + ")").ToList())).Resolve(),
                null));
        }

        private static string MeetingVenue(Map map)
        {
            if (map == null) return "CC_Venue_Outside".Translate();
            if (RoomUtil.ColonyRooms(map).Any(r => r.Role == RoomUtil.DiningRoom)) return "CC_Venue_Dining".Translate();
            if (Avail.ColonyHas(map, d => d.defName == "Campfire")) return "CC_Venue_Campfire".Translate();
            return "CC_Venue_Outside".Translate();
        }

        /// <summary>Someone always has an opinion.</summary>
        private static string Heckle(List<Pawn> speakers, List<MeetingPitch> pitches)
        {
            var proposers = new HashSet<Pawn>(pitches.Select(p => p.proposer));
            var others = speakers.Where(p => !proposers.Contains(p)).ToList();
            if (others.Count == 0 || !Rand.Chance(0.75f)) return null;
            var p = others.RandomElement();
            var target = pitches.RandomElement();
            var tone = ToneUtility.ToneFor(p);
            string fromPool = Flavor.FromPool("Council_Pool_Heckle", p, null, tone);
            if (!fromPool.NullOrEmpty())
                return fromPool.Formatted(p.Named("PAWN"), target.proposer.Named("OTHER"), target.def.label.Named("AMBITION")).Resolve();
            string key = "CC_Heckle_" + tone;
            if (!key.CanTranslate()) key = "CC_Heckle_Plain";
            return key.Translate(p.Named("PAWN"), target.proposer.Named("OTHER"), target.def.label.Named("AMBITION")).Resolve();
        }

        /// <summary>
        /// The decision. Backing the colony's favorite pleases its voters; overruling a clear majority costs a
        /// little trust with them (when downsides are on). The proposer of the chosen idea is delighted either way.
        /// </summary>
        public void Choose(AmbitionDef def, Pawn proposer, Map map, ChoiceLetter_CouncilMeeting letter = null)
        {
            using var _cause = Causes.Push("CC_Cause_AmbitionChosen".Translate(def.label).Resolve());
            var comp = CouncilGameComponent.Instance;
            if (comp == null || def == null || map == null) return;
            if (letter != null && letter.voterPawns != null)
            {
                int chosen = letter.options.IndexOf(def);
                var counts = letter.options.Select((o, k) => letter.voterChoice.Count(c => c == k)).ToList();
                int max = counts.Count > 0 ? counts.Max() : 0;
                bool clearMajority = counts.Count(c => c == max) == 1 && max >= 2;
                int favorite = counts.IndexOf(max);
                for (int k = 0; k < letter.voterPawns.Count; k++)
                {
                    var v = letter.voterPawns[k];
                    if (v == null || v.Dead) continue;
                    // A thin meeting's decision carries less weight with those who made it.
                    if (letter.voterChoice[k] == chosen) comp.standings.Change(v, letter.thin ? 1f : 2f, "CC_Log_VoteWon".Translate(def.label));
                    else if (clearMajority && letter.voterChoice[k] == favorite && Settings.Negatives)
                        comp.standings.Change(v, -2f, "CC_Log_Overruled".Translate(letter.options[favorite].label));
                }
                DecidedWithoutThem(letter, proposer);
            }
            if (proposer != null) comp.standings.Change(proposer, 5f, "CC_Log_IdeaChosen".Translate(def.label));
            // A life goal may count it: seeing the council take up their idea.
            if (proposer != null) comp.aspirations.Notify(proposer, "pitched");
            int now = Find.TickManager.TicksGame;
            active = new AmbitionState
            {
                def = def,
                proposer = proposer,
                map = map,
                startTick = now,
                deadlineTick = now + Mathf.RoundToInt(def.days * Settings.ambitionDeadlineFactor * GenDate.TicksPerDay),
                baselines = def.goals.Select(g => g.Baseline(map)).ToList()
            };
            // What was already done counts for less when it's reached: the reward is for the work the colony puts in.
            try
            {
                active.startProgress = Mathf.Clamp01(active.Progress);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("ambition start progress", e);
            }
            Messages.Message("CC_Ambition_Chosen".Translate(def.LabelCap.ToString(), proposer?.LabelShort ?? "?"), new LookTargets(proposer), MessageTypeDefOf.PositiveEvent, false);
            if (proposer != null)
            {
                CouncilGameComponent.Speak(proposer, "CC_Ambition_ChosenSpeech".Translate(), new Color(0.6f, 1f, 0.6f));
                foreach (var p in map.mapPawns.FreeColonistsSpawned)
                    if (p != proposer && p.needs?.mood != null && Rand.Chance(0.5f)) CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_HeardAtMeeting);
            }
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.AmbitionChosen,
                "CC_Chronicle_AmbitionChosen".Translate(def.label, proposer?.LabelShort ?? "?").Resolve(), proposer));
        }

        /// <summary>
        /// Proposers who missed the meeting and whose idea was turned down: it was decided without them. A stronger thought
        /// than missing it, and a little trust.
        /// </summary>
        private static void DecidedWithoutThem(ChoiceLetter_CouncilMeeting letter, Pawn chosenProposer)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || letter?.absentProposers == null) return;
            foreach (var p in letter.absentProposers)
            {
                if (p == null || p.Dead || p == chosenProposer || !p.IsColonist) continue;
                if (p.needs?.mood != null) CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_DecidedWithoutMe);
                if (Settings.Negatives) comp.standings.Change(p, -2f, "CC_Log_DecidedWithoutMe".Translate());
            }
        }

        public void Postpone(ChoiceLetter_CouncilMeeting letter = null)
        {
            if (letter != null) DecidedWithoutThem(letter, null);
            if (letter != null && Settings.Negatives)
            {
                var comp = CouncilGameComponent.Instance;
                foreach (var p in letter.proposers)
                    if (p != null && !p.Dead) comp?.standings.Change(p, -1f, "CC_Log_NoneChosen".Translate());
            }
            nextMeetingTick = Find.TickManager.TicksGame + GenDate.TicksPerDay * 5;
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Note, "CC_Chronicle_MeetingPostponed".Translate().Resolve()));
        }

        private void Complete(CouncilGameComponent comp)
        {
            // Reaching an ambition that was mostly done when it was chosen is worth less than one built from scratch.
            float effort = PressureMath.EffortFactor(active?.startProgress ?? -1f);
            string cause = active?.def == null ? null
                : effort < 1f ? "CC_Cause_AmbitionDoneEarly".Translate(active.def.label, Mathf.Clamp01(active.startProgress).ToStringPercent()).Resolve()
                : "CC_Cause_AmbitionDone".Translate(active.def.label).Resolve();
            using var _cause = Causes.Push(cause);
            var a = active;
            active = null;
            var map = a.map;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                CouncilGameComponent.GiveThought(p, p == a.proposer ? CouncilDefOf.Council_MyAmbitionAchieved : CouncilDefOf.Council_AmbitionAchieved, null,
                    Settings.rewardFactor * effort);
                comp.standings.Change(p, p == a.proposer ? 12f : 5f, "CC_Log_AmbitionAchieved".Translate(a.def.label));
            }
            if (a.proposer != null && !a.proposer.Dead && Settings.MoodEffects && Rand.Chance(0.35f))
            {
                var h = a.proposer.mindState?.inspirationHandler;
                var insp = h?.GetRandomAvailableInspirationDef();
                if (insp != null && !h.Inspired) h.TryStartInspiration(insp, "CC_Ambition_Inspired".Translate(a.proposer.Named("PAWN")).Resolve());
            }
            string line = a.proposer != null ? (a.def.achieved.Pick(ToneUtility.ToneFor(a.proposer)) ?? "").Formatted(a.proposer.Named("PAWN")).Resolve() : "";
            string text = "CC_Ambition_AchievedText".Translate(a.def.label, ((Find.TickManager.TicksGame - a.startTick) / (float)GenDate.TicksPerDay).ToString("0.#")).Resolve();
            if (!line.NullOrEmpty()) text = "\"" + line + "\"\n\n" + text;
            Find.LetterStack.ReceiveLetter("CC_Ambition_AchievedTitle".Translate(a.def.LabelCap.ToString()), text, LetterDefOf.PositiveEvent, new LookTargets(a.proposer));
            if (a.proposer != null) CouncilGameComponent.Speak(a.proposer, line, new Color(0.6f, 1f, 0.6f));
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.AmbitionAchieved,
                "CC_Chronicle_AmbitionAchieved".Translate(a.def.label).Resolve(), a.proposer));
            nextMeetingTick = Mathf.Max(nextMeetingTick, Find.TickManager.TicksGame + GenDate.TicksPerDay * 3);
        }

        private void Fail(CouncilGameComponent comp)
        {
            using var _cause = Causes.Push(active?.def != null ? "CC_Cause_AmbitionFailed".Translate(active.def.label).Resolve() : null);
            var a = active;
            active = null;
            foreach (var p in a.map.mapPawns.FreeColonistsSpawned)
            {
                comp.standings.Change(p, p == a.proposer ? -8f : -3f, "CC_Log_AmbitionFailed".Translate(a.def.label));
                if (Settings.Negatives) CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_AmbitionFailed, null, Settings.PenaltyScale);
            }
            News.Post("CC_Ambition_Failed".Translate(a.def.LabelCap.ToString(), a.Progress.ToStringPercent()), new LookTargets(a.proposer), NewsKind.Colony, MessageTypeDefOf.NeutralEvent);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.AmbitionFailed,
                "CC_Chronicle_AmbitionFailed".Translate(a.def.label, a.Progress.ToStringPercent()).Resolve(), a.proposer));
            nextMeetingTick = Mathf.Min(nextMeetingTick, Find.TickManager.TicksGame + GenDate.TicksPerDay * 2);
        }

        public void Abandon()
        {
            if (active == null) return;
            var a = active;
            active = null;
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.AmbitionAbandoned,
                "CC_Chronicle_AmbitionAbandoned".Translate(a.def.label).Resolve(), a.proposer));
            nextMeetingTick = Find.TickManager.TicksGame + GenDate.TicksPerDay * 3;
        }

        /// <summary>A granted wish that serves the colony's current ambition pleases whoever proposed it.</summary>
        public void Notify_WishGranted(Wish w)
        {
            if (active?.def?.template == null || w == null) return;
            RoomTemplateDef wishTemplate = null;
            try
            {
                wishTemplate = w.Worker.SuggestedTemplate(w);
            }
            catch
            {
            }
            if (wishTemplate != active.def.template || active.proposer == null || active.proposer.Dead) return;
            CouncilGameComponent.Instance?.standings.Change(active.proposer, 3f, "CC_Log_AmbitionHelped".Translate(active.def.label));
            CouncilGameComponent.Speak(active.proposer, "CC_Ambition_WishHelped".Translate(active.def.label), new Color(0.6f, 1f, 0.6f));
        }
    }

    /// <summary>The meeting letter: pick one of the pitched ambitions, or none.</summary>
    public class ChoiceLetter_CouncilMeeting : ChoiceLetter
    {
        public Map map;
        public List<AmbitionDef> options = new List<AmbitionDef>();
        public List<Pawn> proposers = new List<Pawn>();
        public List<Pawn> voterPawns = new List<Pawn>();
        public List<int> voterChoice = new List<int>();
        /// <summary>Proposers who weren't at the meeting (not by choice).</summary>
        public List<Pawn> absentProposers = new List<Pawn>();
        /// <summary>Decided by fewer than half of those with a say.</summary>
        public bool thin;

        public override bool CanDismissWithRightClick => false;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly)
                {
                    yield return Option_Close;
                    yield break;
                }
                var tracker = CouncilGameComponent.Instance?.ambitions;
                for (int i = 0; i < options.Count; i++)
                {
                    var def = options[i];
                    var proposer = i < proposers.Count ? proposers[i] : null;
                    var opt = new DiaOption("CC_Meeting_Choose".Translate(def.LabelCap.ToString(), proposer?.LabelShort ?? "?"));
                    if (def == null || map == null || tracker == null || tracker.active != null) opt.Disable("CC_Meeting_Unavailable".Translate());
                    opt.action = () =>
                    {
                        tracker?.Choose(def, proposer, map, this);
                        Find.LetterStack.RemoveLetter(this);
                    };
                    opt.resolveTree = true;
                    yield return opt;
                }
                var none = new DiaOption("CC_Meeting_None".Translate());
                none.action = () =>
                {
                    tracker?.Postpone(this);
                    Find.LetterStack.RemoveLetter(this);
                };
                none.resolveTree = true;
                yield return none;
                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref map, "map");
            Scribe_Collections.Look(ref options, "options", LookMode.Def);
            Scribe_Collections.Look(ref proposers, "proposers", LookMode.Reference);
            Scribe_Collections.Look(ref voterPawns, "voterPawns", LookMode.Reference);
            Scribe_Collections.Look(ref voterChoice, "voterChoice", LookMode.Value);
            Scribe_Collections.Look(ref absentProposers, "absentProposers", LookMode.Reference);
            Scribe_Values.Look(ref thin, "thin");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (options == null) options = new List<AmbitionDef>();
                if (proposers == null) proposers = new List<Pawn>();
                if (absentProposers == null) absentProposers = new List<Pawn>();
                absentProposers.RemoveAll(p => p == null);
                if (voterPawns == null || voterChoice == null || voterPawns.Count != voterChoice.Count)
                {
                    voterPawns = new List<Pawn>();
                    voterChoice = new List<int>();
                }
            }
        }
    }
}
