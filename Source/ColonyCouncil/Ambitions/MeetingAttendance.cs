using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>Where a colonist stood when the chair called the council together.</summary>
    public enum MeetingStatus : byte
    {
        /// <summary>Called from whatever they were doing, and on the way.</summary>
        Coming,
        /// <summary>Asleep; someone goes to wake them, and they come late.</summary>
        Woken,
        /// <summary>Free to come, but not pulled from work (meetings don't call people from their work).</summary>
        Invited,
        /// <summary>Not on the meeting's map: a caravan, an outpost, another map, in the air.</summary>
        Away,
        /// <summary>Asleep and left to sleep: children, the sick, the worn out.</summary>
        Asleep,
        /// <summary>Drafted, laid up, out of their mind, or in the middle of something that can't wait.</summary>
        Busy,
        /// <summary>Couldn't get there before most of it was over.</summary>
        TooFar,
        /// <summary>Didn't bother (bitter colonists).</summary>
        Skipped
    }

    /// <summary>One colonist's part in a meeting: called or not, why not, and when they got there.</summary>
    public class MeetingInvite : IExposable
    {
        public Pawn pawn;
        public MeetingStatus status;
        /// <summary>What kept them, or what they were doing when called ("cutting wood on the far side of the map").</summary>
        public string reason;
        /// <summary>Kept away by nothing they chose or worked at (asleep, sick, laid up): no thought for missing it.</summary>
        public bool excused;
        /// <summary>Estimated ticks from the call to their arrival.</summary>
        public int eta = -1;
        public int arrivedTick = -1;
        /// <summary>Who went to wake them (null: woken without anyone walking over).</summary>
        public Pawn waker;
        public bool wakeDone;

        public bool Arrived => arrivedTick >= 0;

        /// <summary>Counted on to come when the meeting starts.</summary>
        public bool Expected => status == MeetingStatus.Coming || status == MeetingStatus.Woken || status == MeetingStatus.Invited;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref status, "status");
            Scribe_Values.Look(ref reason, "reason");
            Scribe_Values.Look(ref excused, "excused");
            Scribe_Values.Look(ref eta, "eta", -1);
            Scribe_Values.Look(ref arrivedTick, "arrivedTick", -1);
            Scribe_References.Look(ref waker, "waker");
            Scribe_Values.Look(ref wakeDone, "wakeDone");
        }
    }

    /// <summary>
    /// Who comes to a council meeting and who doesn't. Worked out once, when the chair calls everyone: each colonist with a
    /// say is away, asleep, busy, too far off, not bothering, or on the way (pulled from ordinary work, like party guests).
    /// Arrivals are noted as the meeting talks (<see cref="CouncilMeeting.Talk"/>); the letter tells it, and those who
    /// missed it feel it.
    /// </summary>
    public static class MeetingAttendance
    {
        /// <summary>Getting up, dressing and setting off after being woken.</summary>
        public const int WakeUpTicks = 400;
        /// <summary>Arriving after this share of the meeting is arriving late (see <see cref="MeetingMath"/>).</summary>
        public const float LateShare = MeetingMath.LateShare;
        /// <summary>A sleeper nobody has reached by now is woken anyway (someone calls from the doorway).</summary>
        public const int WakeFallbackTicks = 1500;
        /// <summary>A waker walks at most this far to fetch someone.</summary>
        private const float MaxWakeWalk = 80f;

        private static CouncilSettings Settings => ColonyCouncilMod.Settings;

        public static JobDef WakeJob => DefDatabase<JobDef>.GetNamedSilentFail("Council_WakeForMeeting");

        // ---------------- Who has a say ----------------

        /// <summary>A colonist with a voice in the council (the rules of <see cref="CouncilGameComponent.CanVoiceWishes"/>, wherever they are).</summary>
        public static bool IsMember(Pawn p) =>
            p != null && !p.Dead && !p.Destroyed && People.StillOurs(p) && p.IsFreeNonSlaveColonist && !p.IsQuestLodger()
            && People.HasInnerLife(p) && !p.DevelopmentalStage.Baby();

        /// <summary>
        /// Everyone with a say whose home is this map: here, or away with a caravan, at an outpost, on another (not home)
        /// map or in the air. Those who live at another of the player's settlements, or sleep in a casket here, aren't counted.
        /// </summary>
        public static List<Pawn> Members(Map map)
        {
            var found = new HashSet<Pawn>();
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoLodgers) found.Add(p);
            try
            {
                foreach (var p in People.OffMapColonists()) found.Add(p);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("meeting members off map", e);
            }
            return found.Where(p => IsMember(p) && HomeIs(p, map)).OrderBy(p => p.thingIDNumber).ToList();
        }

        private static bool HomeIs(Pawn p, Map map)
        {
            var held = p.MapHeld;
            if (held == null) return true;
            if (held == map) return p.Spawned;
            return !held.IsPlayerHome;
        }

        /// <summary>Fewer than this many present makes a thin meeting: half of those with a say, and never fewer than two.</summary>
        public static int Quorum(int eligible) => MeetingMath.Quorum(eligible);

        // ---------------- When it can start ----------------

        /// <summary>Why the council can't sit down right now (danger, another gathering, half the colony under arms), or null.</summary>
        public static string WhyNotNow(Map map)
        {
            if (map.dangerWatcher.DangerRating != StoryDanger.None || !GatheringsUtility.AcceptableGameConditionsToContinueGathering(map))
                return "CC_MeetWait_Danger".Translate();
            if (GatheringsUtility.AnyLordJobPreventsNewGatherings(map)) return "CC_MeetWait_Gathering".Translate();
            var free = map.mapPawns.FreeColonistsSpawned;
            int drafted = free.Count(p => p.Drafted);
            if (free.Count > 0 && drafted * 2 >= free.Count) return "CC_MeetWait_Drafted".Translate();
            if (free.Any(p => !p.Dead && p.health?.hediffSet != null && p.health.hediffSet.BleedRateTotal > 0f)) return "CC_MeetWait_Bleeding".Translate();
            return null;
        }

        /// <summary>Someone who could call the meeting to order right now.</summary>
        public static bool CanChair(Pawn p) =>
            CouncilGameComponent.CanVoiceWishes(p) && p.Awake() && p.GetLord() == null && !p.DevelopmentalStage.Juvenile()
            && GatheringsUtility.PawnCanStartOrContinueGathering(p) && UrgentWork(p) == null && !(p.CurJob?.playerForced ?? false);

        // ---------------- The call ----------------

        /// <summary>The walk to the meeting, in ticks, from where they stand.</summary>
        public static int EtaTicks(Pawn p, IntVec3 spot)
        {
            if (p == null || !p.Spawned || !spot.IsValid) return int.MaxValue;
            return MeetingMath.EtaTicks(p.Position.DistanceTo(spot), p.TicksPerMoveCardinal);
        }

        /// <summary>Decides, once, whether this colonist comes and if not, why.</summary>
        public static MeetingInvite Classify(Pawn p, CouncilMeeting m, bool callFromWork, bool isChair)
        {
            var inv = new MeetingInvite { pawn = p };
            var map = m.map;
            if (!p.Spawned || p.Map != map)
            {
                inv.status = MeetingStatus.Away;
                inv.reason = AwayReason(p);
                return inv;
            }
            if (isChair)
            {
                inv.status = MeetingStatus.Coming;
                inv.eta = EtaTicks(p, m.spot);
                return inv;
            }
            bool awake = p.Awake();
            if (p.Drafted) return Busy(inv, "CC_MeetWhy_Drafted".Translate(), false);
            if (p.Downed) return Busy(inv, "CC_MeetWhy_Downed".Translate(), true);
            if (p.InMentalState)
            {
                if (p.MentalStateDef?.defName == "Council_Heart_WornOut")
                    return awake ? Busy(inv, "CC_MeetWhy_DayOff".Translate(), true) : Sleeping(inv, "CC_MeetWhy_AsleepWornOut".Translate());
                return Busy(inv, "CC_MeetWhy_Mental".Translate(), true);
            }
            if (MedicalRest(p)) return awake ? Busy(inv, "CC_MeetWhy_Recovering".Translate(), true) : Sleeping(inv, "CC_MeetWhy_AsleepSick".Translate());
            if (!awake)
            {
                string why = null;
                if (!callFromWork || !CanBeWoken(p, out why)) return Sleeping(inv, why ?? "CC_MeetWhy_Asleep".Translate());
                if (WouldSkip(p)) return Skip(inv);
                inv.status = MeetingStatus.Woken;
                inv.reason = "CC_MeetWhy_Asleep".Translate();
                inv.eta = EtaTicks(p, m.spot) + WakeUpTicks;
                return inv;
            }
            string urgent = UrgentWork(p) ?? OtherLord(p);
            // A patient on the way to bed is excused, like one already lying there.
            if (urgent != null) return Busy(inv, urgent, PatientJob(p.CurJob));
            if (p.CurJob != null && p.CurJob.playerForced) return Busy(inv, "CC_MeetWhy_Ordered".Translate(), false);
            if (WouldSkip(p)) return Skip(inv);
            if (m.spot.IsValid && m.spot.IsForbidden(p)) return Busy(inv, "CC_MeetWhy_Restricted".Translate(), false);
            if (m.spot.IsValid && !p.CanReach(m.spot, PathEndMode.Touch, Danger.Deadly)) return Busy(inv, "CC_MeetWhy_CutOff".Translate(), false);
            inv.eta = EtaTicks(p, m.spot);
            if (MeetingMath.TooFar(inv.eta))
            {
                inv.status = MeetingStatus.TooFar;
                inv.reason = Whereabouts(p, m.spot, map);
                return inv;
            }
            inv.status = callFromWork ? MeetingStatus.Coming : MeetingStatus.Invited;
            // Remembered for the letter, in case they keep at it and never come.
            inv.reason = Activity(p);
            return inv;
        }

        private static MeetingInvite Busy(MeetingInvite inv, string reason, bool excused)
        {
            inv.status = MeetingStatus.Busy;
            inv.reason = reason;
            inv.excused = excused;
            return inv;
        }

        private static MeetingInvite Sleeping(MeetingInvite inv, string reason)
        {
            inv.status = MeetingStatus.Asleep;
            inv.reason = reason;
            inv.excused = true;
            return inv;
        }

        private static MeetingInvite Skip(MeetingInvite inv)
        {
            inv.status = MeetingStatus.Skipped;
            return inv;
        }

        /// <summary>Bitter colonists mostly don't bother turning up.</summary>
        private static bool WouldSkip(Pawn p)
        {
            var comp = CouncilGameComponent.Instance;
            return comp != null && comp.standings.LevelOf(p) == TrustLevel.Bitter && Rand.Chance(0.67f);
        }

        private static bool MedicalRest(Pawn p)
        {
            if (!p.InBed()) return false;
            var bed = RestUtility.CurrentBed(p);
            return (bed != null && bed.Medical) || HealthAIUtility.ShouldSeekMedicalRest(p) || p.CurJobDef == JobDefOf.LayDownResting;
        }

        /// <summary>
        /// Ordinary sleep that someone may wake them from: not a child, not sick, not in a mental state, not deathrest or
        /// anything else that only looks like sleep, and not so tired that staying up through the meeting would leave them
        /// exhausted (the meeting comes before sleep for those it has called, as a party does). Checked when they're
        /// called and again when someone reaches them.
        /// </summary>
        private static bool CanBeWoken(Pawn p, out string why)
        {
            why = null;
            if (p.DevelopmentalStage.Juvenile() || p.InMentalState) return false;
            if (p.CurJobDef != JobDefOf.LayDown) return false;
            var bed = RestUtility.CurrentBed(p);
            if ((bed != null && bed.Medical) || HealthAIUtility.ShouldSeekMedicalRest(p))
            {
                why = "CC_MeetWhy_AsleepSick".Translate();
                return false;
            }
            var rest = p.needs?.rest;
            if (rest != null && rest.CurCategory >= RestCategory.VeryTired)
            {
                why = "CC_MeetWhy_AsleepExhausted".Translate();
                return false;
            }
            return true;
        }

        private static string AwayReason(Pawn p)
        {
            if (p.IsCaravanMember()) return "CC_MeetWhy_Caravan".Translate();
            var post = OutpostLife.PostOf(p);
            if (post != null && !post.Label.NullOrEmpty()) return "CC_MeetWhy_Outpost".Translate(post.Label);
            return "CC_MeetWhy_Away".Translate();
        }

        /// <summary>A colonist or animal by name ("Kai", "the muffalo").</summary>
        private static string NameOf(Pawn p)
        {
            if (p == null) return "CC_MeetWho_Someone".Translate();
            if (p.Name != null) return p.LabelShort;
            return Find.ActiveLanguageWorker.WithDefiniteArticle(p.LabelShort);
        }

        /// <summary>Work that must not be interrupted: tending, rescuing, surgery, fighting fire, feeding, carrying someone.</summary>
        public static string UrgentWork(Pawn p)
        {
            var job = p.CurJob;
            var carried = p.carryTracker?.CarriedThing as Pawn;
            if (job == null) return carried != null ? "CC_MeetWhy_Carrying".Translate(NameOf(carried)).ToString() : null;
            var def = job.def;
            var target = job.targetA.Thing as Pawn ?? job.targetB.Thing as Pawn;
            if (def == JobDefOf.TendPatient || def == JobDefOf.TendEntity)
                return target == null || target == p ? "CC_MeetWhy_TendingSelf".Translate().ToString() : "CC_MeetWhy_Tending".Translate(NameOf(target)).ToString();
            if (def == JobDefOf.Rescue) return "CC_MeetWhy_Rescue".Translate(NameOf(target ?? carried));
            if (job.bill?.recipe != null && job.bill.recipe.IsSurgery) return "CC_MeetWhy_Surgery".Translate(NameOf(job.targetA.Thing as Pawn));
            if (def == JobDefOf.BeatFire || def == JobDefOf.ExtinguishFiresNearby || def == JobDefOf.ExtinguishSelf
                || job.workGiverDef?.workType?.defName == "Firefighter") return "CC_MeetWhy_Fire".Translate();
            if (def == JobDefOf.FeedPatient || def == JobDefOf.Breastfeed || def == JobDefOf.BottleFeedBaby || def == JobDefOf.BreastfeedCarryToMom
                || def == JobDefOf.CarryToMomAfterBirth) return "CC_MeetWhy_Feeding".Translate(NameOf(target ?? carried));
            if (carried != null) return "CC_MeetWhy_Carrying".Translate(NameOf(carried));
            // On the way to bed for treatment or bed rest.
            if (PatientJob(job)) return "CC_MeetWhy_Recovering".Translate();
            // Taking a baby out of the cold or heat, or anything else the game treats as an emergency.
            if (def == JobDefOf.BringBabyToSafety || def == JobDefOf.BringBabyToSafetyUnforced || (job.workGiverDef?.emergency ?? false))
                return "CC_MeetWhy_Elsewhere".Translate();
            return null;
        }

        /// <summary>A job from the patient work types: going to bed to be treated or to recover.</summary>
        private static bool PatientJob(Job job)
        {
            string wt = job?.workGiverDef?.workType?.defName;
            return wt == "Patient" || wt == "PatientBedRest";
        }

        /// <summary>Already taken up by another lord: a ritual, a caravan forming, another gathering.</summary>
        private static string OtherLord(Pawn p)
        {
            var lord = p.GetLord();
            if (lord == null) return null;
            var job = lord.LordJob;
            if (job is LordJob_CouncilMeeting) return null;
            if (job is LordJob_FormAndSendCaravan) return "CC_MeetWhy_Caravaning".Translate();
            if (job is LordJob_Ritual) return "CC_MeetWhy_Ritual".Translate();
            if (job is LordJob_VoluntarilyJoinable) return "CC_MeetWhy_Gathering".Translate();
            return "CC_MeetWhy_Elsewhere".Translate();
        }

        /// <summary>What they're doing, in a few words ("cutting wood", "at the forge"), or null when idle.</summary>
        public static string Activity(Pawn p)
        {
            var job = p?.CurJob;
            if (job == null) return null;
            var wt = job.workGiverDef?.workType;
            if (wt != null)
            {
                if (wt.defName == "PlantCutting")
                    return (job.targetA.Thing is Plant pl && (pl.def.plant?.IsTree ?? false) ? "CC_MeetDo_Wood" : "CC_MeetDo_Clearing").Translate();
                string key = "CC_MeetDo_" + wt.defName;
                if (key.CanTranslate()) return key.Translate();
                if (!wt.gerundLabel.NullOrEmpty()) return wt.gerundLabel.ToLower();
                return "CC_MeetDo_Working".Translate();
            }
            if (job.def.joyKind != null) return "CC_MeetDo_Relaxing".Translate();
            if (job.def == JobDefOf.Ingest) return "CC_MeetDo_Eating".Translate();
            return null;
        }

        /// <summary>What they were doing and where ("cutting wood on the far side of the map").</summary>
        private static string Whereabouts(Pawn p, IntVec3 spot, Map map)
        {
            string act = Activity(p) ?? "CC_MeetDo_Out".Translate();
            string place = null;
            float size = Mathf.Max(map.Size.x, map.Size.z);
            if (p.Position.DistanceTo(spot) > size * 0.45f) place = "CC_MeetWhere_FarSide".Translate();
            else if (map.areaManager?.Home != null && !map.areaManager.Home[p.Position]) place = "CC_MeetWhere_Outside".Translate();
            return place == null ? act : act + " " + place;
        }

        // ---------------- Sending for them ----------------

        /// <summary>
        /// Pulls someone called to the meeting from ordinary work, recreation or idling, so they set off now (they're
        /// already in the meeting's lord, so the next thing they do is walk over). Anything else (eating, a load in their
        /// arms, a job they can't drop, an order) they finish first.
        /// </summary>
        public static void CallFromWork(Pawn p)
        {
            var job = p?.CurJob;
            if (job == null || p.jobs == null || job.playerForced) return;
            if (!p.jobs.IsCurrentJobPlayerInterruptible()) return;
            // Someone with a load in their arms puts it where it goes first.
            if (p.carryTracker?.CarriedThing != null) return;
            bool idle = job.def == JobDefOf.Wait_Wander || job.def == JobDefOf.GotoWander || job.def == JobDefOf.Wait;
            if (job.workGiverDef == null && job.def.joyKind == null && !idle) return;
            p.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        /// <summary>
        /// Sends the nearest colonist on their way to the meeting to wake each sleeper. When nobody can be spared, the
        /// sleeper is woken at once (someone shouts down the hall).
        /// </summary>
        public static void ArrangeWaking(CouncilMeeting m, Lord lord)
        {
            var jobDef = WakeJob;
            var busyWakers = new HashSet<Pawn>();
            foreach (var inv in m.invites.Where(i => i.status == MeetingStatus.Woken && !i.wakeDone).ToList())
            {
                var sleeper = inv.pawn;
                Pawn waker = null;
                if (jobDef != null && lord != null && sleeper != null && sleeper.Spawned)
                {
                    waker = lord.ownedPawns
                        .Where(w => w != sleeper && !busyWakers.Contains(w) && CanWake(w, sleeper))
                        .OrderBy(w => w.Position.DistanceToSquared(sleeper.Position))
                        .FirstOrDefault();
                }
                if (waker != null)
                {
                    try
                    {
                        waker.jobs.StartJob(JobMaker.MakeJob(jobDef, sleeper), JobCondition.InterruptForced);
                        inv.waker = waker;
                        busyWakers.Add(waker);
                        continue;
                    }
                    catch (System.Exception e)
                    {
                        CouncilLog.ErrorOnce("meeting wake job", e);
                    }
                }
                Wake(m, inv, null);
            }
        }

        private static bool CanWake(Pawn w, Pawn sleeper) =>
            w.Spawned && w.Map == sleeper.Map && w.Awake() && !w.Downed && !w.Drafted && !w.InMentalState && w.jobs != null
            && FreeToFetch(w) && w.Position.DistanceTo(sleeper.Position) <= MaxWakeWalk && w.CanReach(sleeper, PathEndMode.Touch, Danger.Some);

        /// <summary>
        /// Free to go and fetch someone: not carrying anything (it would be dropped where they stand), not eating, not in
        /// bed, not under orders and not doing anything urgent.
        /// </summary>
        private static bool FreeToFetch(Pawn w)
        {
            if (w.carryTracker?.CarriedThing != null || w.InBed()) return false;
            var job = w.CurJob;
            if (job == null) return true;
            if (job.playerForced || job.def == JobDefOf.Ingest || !w.jobs.IsCurrentJobPlayerInterruptible()) return false;
            return UrgentWork(w) == null;
        }

        /// <summary>The wake job is still worth finishing: the meeting is on and they're still asleep.</summary>
        public static bool WakeStillWanted(Pawn waker, Pawn sleeper)
        {
            var m = CouncilGameComponent.Instance?.ambitions?.meeting;
            if (m == null || !m.Started || sleeper == null || waker == null) return false;
            // No one is woken while there's danger about.
            if (m.map == null || m.map.dangerWatcher.DangerRating != StoryDanger.None) return false;
            var inv = m.InviteOf(sleeper);
            return inv != null && inv.status == MeetingStatus.Woken && !inv.wakeDone && sleeper.Spawned && sleeper.Map == waker.Map
                   && !sleeper.Awake() && !sleeper.Downed && !sleeper.InMentalState && sleeper.CurJobDef == JobDefOf.LayDown;
        }

        /// <summary>The waker reached the bed.</summary>
        public static void WakeByHand(Pawn waker, Pawn sleeper)
        {
            if (!WakeStillWanted(waker, sleeper)) return;
            var m = CouncilGameComponent.Instance.ambitions.meeting;
            Wake(m, m.InviteOf(sleeper), waker);
        }

        /// <summary>Wakes a sleeper for the meeting and sends them over: a small grumble, and they'll be late.</summary>
        public static void Wake(CouncilMeeting m, MeetingInvite inv, Pawn waker)
        {
            if (m == null || inv == null || !m.Started) return;
            inv.wakeDone = true;
            var p = inv.pawn;
            if (p == null || p.Dead || !p.Spawned || p.Map != m.map) return;
            // The gathering is gone (the meeting is ending): nobody is woken for it.
            var lord = m.Lord;
            if (lord == null) return;
            bool asleep = !p.Awake();
            // Only from ordinary sleep: not if they've since gone to a sickbed, into deathrest or a mental state.
            if (asleep && !CanBeWoken(p, out string why))
            {
                inv.status = MeetingStatus.Asleep;
                inv.excused = true;
                inv.reason = why ?? "CC_MeetWhy_Asleep".Translate();
                return;
            }
            try
            {
                if (p.GetLord() == null && !p.Drafted && !p.InMentalState && lord.CanAddPawn(p)) lord.AddPawn(p);
                if (!asleep) return;
                RestUtility.WakeUp(p);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("meeting wake", e);
                return;
            }
            if (waker != null) inv.waker = waker;
            var comp = CouncilGameComponent.Instance;
            if (comp != null && p.needs?.mood != null)
                CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_WokenForMeeting, null, WokenFactor(p, comp) * Settings.PenaltyScale);
            if (waker == null) return;
            string line = Flavor.FromPool("Council_Pool_MeetingWake", waker);
            if (!line.NullOrEmpty())
                CouncilGameComponent.Speak(waker, line.Formatted(waker.Named("PAWN"), p.Named("OTHER")).Resolve(), new Color(0.7f, 0.85f, 1f));
            SocialLog.Add(waker, p, "CC_Meeting_WokeLog".Translate(waker.Named("PAWN"), p.Named("OTHER")).Resolve());
        }

        /// <summary>
        /// How much being woken stings: little for the devoted, more for the bitter, more again for the tired and for
        /// night owls. (The very tired aren't woken at all.)
        /// </summary>
        private static float WokenFactor(Pawn p, CouncilGameComponent comp)
        {
            float f;
            switch (comp.standings.LevelOf(p))
            {
                case TrustLevel.Devoted: f = 0.5f; break;
                case TrustLevel.Trusting: f = 0.65f; break;
                case TrustLevel.Doubtful: f = 1f; break;
                case TrustLevel.Bitter: f = 1.25f; break;
                default: f = 0.75f; break;
            }
            var rest = p.needs?.rest;
            if (rest != null && rest.CurCategory >= RestCategory.Tired) f *= 1.4f;
            var owl = DefDatabase<TraitDef>.GetNamedSilentFail("NightOwl");
            if (owl != null && (p.story?.traits?.HasTrait(owl) ?? false)) f *= 1.3f;
            return Mathf.Clamp(f, 0.5f, 1.25f);
        }

        /// <summary>
        /// Every few seconds while the meeting runs: anyone whose waker gave up, or who nobody has reached in time, is
        /// woken where they lie; anyone who got up on their own just comes.
        /// </summary>
        public static void WakeUpkeep(CouncilMeeting m, int now)
        {
            // No one is woken while there's danger about; they sleep on.
            if (m.map == null || m.map.dangerWatcher.DangerRating != StoryDanger.None) return;
            var jobDef = WakeJob;
            foreach (var inv in m.invites)
            {
                if (inv.status != MeetingStatus.Woken || inv.wakeDone) continue;
                var p = inv.pawn;
                if (p == null || p.Dead || !p.Spawned || p.Map != m.map || p.Downed)
                {
                    inv.wakeDone = true;
                    continue;
                }
                if (p.Awake())
                {
                    // Up on their own before anyone got there: they just come, like anyone else.
                    inv.wakeDone = true;
                    inv.status = MeetingStatus.Coming;
                    inv.waker = null;
                    var lord = m.Lord;
                    if (lord != null && p.GetLord() == null && !p.Drafted && !p.InMentalState && lord.CanAddPawn(p)) lord.AddPawn(p);
                    continue;
                }
                var w = inv.waker;
                bool onIt = w != null && w.Spawned && jobDef != null && w.CurJobDef == jobDef && w.CurJob.targetA.Thing == p;
                // The waker gave up, or is taking too long: someone calls them from the doorway instead.
                if (!onIt || now - m.startedTick > WakeFallbackTicks) Wake(m, inv, null);
            }
        }

        /// <summary>
        /// Drafted colonists and those given an order leave the meeting at once: the player always comes first. (The game
        /// already lets drafted colonists go from gatherings; this makes sure of it.)
        /// </summary>
        public static void ReleaseCalledAway(CouncilMeeting m)
        {
            var lord = m?.Lord;
            if (lord == null) return;
            foreach (var p in lord.ownedPawns.ToList())
            {
                if (p == null) continue;
                if (p.Drafted) lord.Notify_PawnLost(p, PawnLostCondition.Drafted);
                else if (p.CurJob != null && p.CurJob.playerForced) lord.Notify_PawnLost(p, PawnLostCondition.ForcedByPlayerAction);
            }
        }

        // ---------------- Afterwards ----------------

        /// <summary>
        /// Those who missed it through work or travel feel left out (the sick, the sleeping and those who didn't bother
        /// don't). Their own turned-down ideas are dealt with when the letter is answered.
        /// </summary>
        public static void AbsenteeEffects(CouncilMeeting m)
        {
            foreach (var inv in m.invites)
            {
                var p = inv.pawn;
                if (inv.Arrived || p == null || p.Dead || inv.status == MeetingStatus.Skipped || inv.excused) continue;
                if (p.needs?.mood != null) CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_MissedMeeting);
            }
        }

        /// <summary>
        /// Who came, who was late, who missed it and why, as a short paragraph: "Bo and Kai were there from the start, and
        /// Tom came in late. Ana was away with a caravan and Jo was tending to Kai."
        /// </summary>
        public static string Paragraph(CouncilMeeting m, bool thin)
        {
            if (m.invites.Count == 0) return null;
            var onTime = new List<string>();
            var late = new List<string>();
            var drowsy = new List<string>();
            var skipped = new List<string>();
            var neverCame = new List<string>();
            var stayedAtWork = new List<string>();
            var missed = new List<KeyValuePair<string, List<string>>>();
            var tooFar = new List<KeyValuePair<string, List<string>>>();
            // The chair first, then the order they were called in.
            foreach (var inv in m.invites.OrderBy(i => i.pawn == m.chair ? 0 : 1))
            {
                var p = inv.pawn;
                if (p == null) continue;
                string name = p.LabelShort;
                if (inv.Arrived)
                {
                    if (inv.status == MeetingStatus.Woken) drowsy.Add(name);
                    else if (m.IsLate(inv)) late.Add(name);
                    else onTime.Add(name);
                    continue;
                }
                switch (inv.status)
                {
                    case MeetingStatus.Skipped: skipped.Add(name); break;
                    case MeetingStatus.Invited: stayedAtWork.Add(name); break;
                    case MeetingStatus.Coming:
                    case MeetingStatus.Woken: neverCame.Add(name); break;
                    case MeetingStatus.TooFar: Group(tooFar, inv.reason ?? "CC_MeetDo_Out".Translate(), name); break;
                    default: Group(missed, inv.reason ?? "CC_MeetWhy_Away".Translate(), name); break;
                }
            }
            var parts = new List<string>();
            string present = null;
            if (onTime.Count > 0) present = (onTime.Count == 1 ? "CC_Meeting_Att_OnTimeOne" : "CC_Meeting_Att_OnTimeMany").Translate(TextUtil.JoinAnd(onTime));
            if (late.Count > 0)
            {
                string l = "CC_Meeting_Att_Late".Translate(TextUtil.JoinAnd(late));
                present = present == null ? l : "CC_Meeting_Att_AndLate".Translate(present.TrimEnd('.'), TextUtil.JoinAnd(late)).ToString();
            }
            if (present != null) parts.Add(present);
            if (drowsy.Count > 0) parts.Add("CC_Meeting_Att_Drowsy".Translate(TextUtil.JoinAnd(drowsy)));
            var clauses = missed.Select(g => (g.Value.Count == 1 ? "CC_Meeting_Att_Was" : "CC_Meeting_Att_Were").Translate(TextUtil.JoinAnd(g.Value), g.Key).ToString()).ToList();
            if (clauses.Count > 0 && clauses.Count <= 3) parts.Add(TextUtil.JoinAnd(clauses) + ".");
            else foreach (var c in clauses) parts.Add(c + ".");
            foreach (var g in tooFar)
                parts.Add((g.Value.Count == 1 ? "CC_Meeting_Att_TooFarOne" : "CC_Meeting_Att_TooFarMany").Translate(TextUtil.JoinAnd(g.Value), g.Key));
            if (neverCame.Count > 0) parts.Add("CC_Meeting_Att_NeverCame".Translate(TextUtil.JoinAnd(neverCame)));
            if (stayedAtWork.Count > 0) parts.Add("CC_Meeting_Att_StayedAtWork".Translate(TextUtil.JoinAnd(stayedAtWork)));
            if (skipped.Count > 0) parts.Add("CC_Meeting_Att_Skipped".Translate(TextUtil.JoinAnd(skipped)));
            if (thin)
                parts.Add((m.thinPostponed ? "CC_Meeting_Att_ThinAgain" : "CC_Meeting_Att_Thin").Translate(m.attendees.Count, m.invites.Count));
            return parts.Count == 0 ? null : string.Join(" ", parts);
        }

        private static void Group(List<KeyValuePair<string, List<string>>> groups, string reason, string name)
        {
            foreach (var g in groups)
            {
                if (g.Key != reason) continue;
                g.Value.Add(name);
                return;
            }
            groups.Add(new KeyValuePair<string, List<string>>(reason, new List<string> { name }));
        }

        // ---------------- When ----------------

        /// <summary>
        /// The next evening slot at least <paramref name="lead"/> ticks away: six in the evening, or a later full hour up
        /// to eight; past that, six the next evening.
        /// </summary>
        public static int NextEvening(Map map, int now, int lead) =>
            now + Mathf.RoundToInt(MeetingMath.HoursToEvening(GenLocalDate.HourFloat(map), lead / (float)GenDate.TicksPerHour) * GenDate.TicksPerHour);

        /// <summary>"this evening", "tomorrow evening", "in about 2 hours".</summary>
        public static string When(Map map, int now, int tick)
        {
            int ticks = tick - now;
            if (ticks <= GenDate.TicksPerHour / 2) return "CC_When_Now".Translate();
            float hours = ticks / (float)GenDate.TicksPerHour;
            if (hours < 1.5f) return "CC_When_AnHour".Translate();
            if (hours < 3.5f) return "CC_When_Hours".Translate(Mathf.RoundToInt(hours));
            float hourNow = GenLocalDate.HourFloat(map);
            float at = hourNow + hours;
            bool evening = at % 24f >= 16f && at % 24f < 22f;
            if (at < 24f) return (evening ? "CC_When_ThisEvening" : "CC_When_LaterToday").Translate();
            if (at < 48f) return (evening ? "CC_When_TomorrowEvening" : "CC_When_Tomorrow").Translate();
            return "CC_When_InDays".Translate(TextUtil.DaysString(ticks));
        }
    }

    /// <summary>
    /// On the way to the meeting, a colonist stops at a sleeper's bed and shakes them awake: walk over, a moment at the
    /// bedside, then up they get. Ends quietly if the meeting is off or the sleeper is already up.
    /// </summary>
    public class JobDriver_WakeForMeeting : JobDriver
    {
        private Pawn Sleeper => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !MeetingAttendance.WakeStillWanted(pawn, Sleeper));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(90, TargetIndex.A);
            yield return Toils_General.Do(() => MeetingAttendance.WakeByHand(pawn, Sleeper));
        }
    }
}
