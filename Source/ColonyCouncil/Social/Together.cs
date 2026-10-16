using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Moments between colonists happen where the two really are. A moment that needs two people together (a secret
    /// confided, a lesson, a word of thanks, the herdmaster sitting with someone whose animal died) is done at once when
    /// they are together (<see cref="Can"/>: on the same map, both up and about, and within talking range,
    /// <see cref="SocialLog.Near"/>), and otherwise waits for them to be (<see cref="When"/>): the council's hour looks
    /// again, for up to a day or two. A moment worth a walk lets one of them go and find the other once the chance
    /// meeting hasn't come: in their free time, when it is safe, they walk over and have a word
    /// (<see cref="JobDriver_CouncilTalk"/>), and the moment happens when they get there.
    /// <para>
    /// What waits isn't saved: after a load it is simply gone, and the system that wanted it can come round to it again.
    /// Nothing here ticks; the waiting list is looked through once an hour (CouncilGameComponent.HourlyPeople).
    /// </para>
    /// </summary>
    public static class Together
    {
        /// <summary>At most this many moments wait at once.</summary>
        public const int MaxWaiting = 40;

        private sealed class Moment
        {
            public Pawn a, b;
            public Action fire;
            /// <summary>Anything more it needs (a third person close by too); null: the two together is enough.</summary>
            public Func<bool> also;
            /// <summary>Waiting for company, not one person (b is null): what happens, with whoever they're with.</summary>
            public Action<Pawn> withAnyone;
            /// <summary>For company: anyone but this one (the person a secret is about).</summary>
            public Pawn except;
        }

        private static readonly PendingQueue<Moment> waiting = new PendingQueue<Moment>(MaxWaiting);

        private static int Now => Find.TickManager.TicksGame;

        // ---------------- Who is together ----------------

        /// <summary>Up and about: on a map, awake, not downed, drafted or in a mental state.</summary>
        public static bool Able(Pawn p) => p != null && !p.Dead && p.Spawned && !p.Downed && !p.Drafted && !p.InMentalState && p.Awake();

        /// <summary>Two people really together: both up and about, on the same map, and within talking range of each other.</summary>
        public static bool Can(Pawn a, Pawn b) => a != null && b != null && a != b && Able(a) && Able(b) && a.Map == b.Map && SocialLog.Near(a, b);

        /// <summary>Someone of the colony (other than <paramref name="except"/>) together with <paramref name="p"/> right now, or null.</summary>
        public static Pawn Company(Pawn p, Pawn except = null)
        {
            if (!Able(p)) return null;
            foreach (var o in p.Map.mapPawns.FreeColonistsSpawned)
                if (o != p && o != except && !o.DevelopmentalStage.Baby() && Can(p, o))
                    return o;
            return null;
        }

        /// <summary>
        /// Within earshot of someone speaking to a few people at once (a storyteller at the fire): up and about, on the
        /// same map, within <paramref name="range"/>, and in the same room or in sight (not through a wall).
        /// </summary>
        public static bool Hears(Pawn listener, Pawn speaker, float range)
        {
            if (!Able(listener) || speaker == null || !speaker.Spawned || speaker.Map != listener.Map || listener == speaker) return false;
            if (!listener.Position.InHorDistOf(speaker.Position, range)) return false;
            try
            {
                var room = listener.GetRoom();
                if (room != null && room == speaker.GetRoom()) return true;
                return GenSight.LineOfSight(listener.Position, speaker.Position, listener.Map, true);
            }
            catch
            {
                return false;
            }
        }

        // ---------------- Waiting for them to be together ----------------

        /// <summary>
        /// <paramref name="fire"/> when <paramref name="a"/> and <paramref name="b"/> are together: now if they are,
        /// otherwise looked at again each hour for up to <paramref name="hours"/>, then dropped. With
        /// <paramref name="seekAfter"/> (hours, 0 for at once), a moment that hasn't come by chance by then lets
        /// <paramref name="a"/> go and find <paramref name="b"/> in their free time. <paramref name="also"/>: anything
        /// more it needs at that moment. A key already waiting isn't added twice. True when it happened now.
        /// </summary>
        public static bool When(string key, Pawn a, Pawn b, Action fire, float hours = 24f, float seekAfter = -1f, Func<bool> also = null)
        {
            if (key == null || a == null || b == null || a == b || fire == null || Current.Game == null) return false;
            if (Can(a, b) && Also(also, key))
            {
                waiting.RemoveKey(key);
                Run(key, fire);
                return true;
            }
            if (waiting.Contains(key)) return false;
            int now = Now;
            waiting.Add(key, new Moment { a = a, b = b, fire = fire, also = also }, now, TogetherMath.Expires(now, hours), TogetherMath.SeekFrom(now, seekAfter));
            return false;
        }

        /// <summary>
        /// <paramref name="fire"/> when <paramref name="a"/> is in company (someone of the colony beside them, other than
        /// <paramref name="except"/>): now if they are, otherwise looked at again each hour for up to
        /// <paramref name="hours"/>, then dropped. For things said to whoever is there (a secret let slip, a confession).
        /// True when it happened now.
        /// </summary>
        public static bool WhenInCompany(string key, Pawn a, Action<Pawn> fire, float hours = 24f, Pawn except = null)
        {
            if (key == null || a == null || fire == null || Current.Game == null) return false;
            var with = Company(a, except);
            if (with != null)
            {
                waiting.RemoveKey(key);
                Run(key, () => fire(with));
                return true;
            }
            if (waiting.Contains(key)) return false;
            int now = Now;
            waiting.Add(key, new Moment { a = a, withAnyone = fire, except = except }, now, TogetherMath.Expires(now, hours));
            return false;
        }

        /// <summary>This moment is waiting for its two to be together.</summary>
        public static bool Waiting(string key) => waiting.Contains(key);

        /// <summary>It won't happen after all (the reason for it has gone).</summary>
        public static void Forget(string key) => waiting.RemoveKey(key);

        /// <summary>How many moments are waiting (for the self-check and the tests in game).</summary>
        public static int WaitingCount => waiting.Count;

        /// <summary>
        /// The council's hour: moments whose two are together now happen; those out of time are dropped; and for those
        /// worth a walk, one of the two may set out to find the other.
        /// </summary>
        public static void Hourly(CouncilGameComponent comp)
        {
            if (waiting.Count == 0) return;
            int now = Now;
            waiting.Expire(now);
            HashSet<Pawn> setOut = null;
            foreach (var e in waiting.Entries.ToList())
            {
                var m = e.item;
                if (Gone(m.a) || (m.withAnyone == null && Gone(m.b)))
                {
                    waiting.Remove(e);
                    continue;
                }
                if (m.withAnyone != null)
                {
                    var with = Company(m.a, m.except);
                    if (with == null) continue;
                    waiting.Remove(e);
                    Run(e.key, () => m.withAnyone(with));
                    continue;
                }
                if (Can(m.a, m.b) && Also(m.also, e.key))
                {
                    waiting.Remove(e);
                    Run(e.key, m.fire);
                    continue;
                }
                if (!TogetherMath.MaySeek(now, e.seekFrom, e.expires) || (setOut != null && setOut.Contains(m.a))) continue;
                if (TryGo(m.a, m.b, e.id)) (setOut ??= new HashSet<Pawn>()).Add(m.a);
            }
        }

        /// <summary>The walk over got there (JobDriver_CouncilTalk): what waited for the two of them happens now, if they're still both up to it.</summary>
        public static bool Arrived(Pawn a, Pawn b, int id)
        {
            var e = waiting.Find(id);
            if (e == null || e.item.withAnyone != null || e.item.a != a || e.item.b != b) return false;
            if (!Can(a, b) || !Also(e.item.also, e.key)) return false;
            waiting.Remove(e);
            Run(e.key, e.item.fire);
            return true;
        }

        /// <summary>A new game or a load: nothing waits over from the last one.</summary>
        public static void Reset() => waiting.Clear();

        private static bool Gone(Pawn p) => p == null || p.Dead || p.Destroyed || p.Discarded;

        private static bool Also(Func<bool> also, string key)
        {
            if (also == null) return true;
            try
            {
                return also();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("together check " + key, e);
                return false;
            }
        }

        private static void Run(string key, Action fire)
        {
            try
            {
                fire();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("together " + (key?.Split('|')[0] ?? "?"), e);
            }
        }

        // ---------------- Going to find someone ----------------

        private static JobDef talkJob;
        private static bool talkJobLooked;

        public static JobDef TalkJob
        {
            get
            {
                if (!talkJobLooked)
                {
                    talkJob = DefDatabase<JobDef>.GetNamedSilentFail("Council_TalkTo");
                    talkJobLooked = true;
                }
                return talkJob;
            }
        }

        /// <summary>
        /// Free time: not meant to be asleep, and doing nothing in particular (wandering about, waiting) or something
        /// for fun, nothing the player ordered.
        /// </summary>
        public static bool FreeTime(Pawn p)
        {
            if (p?.timetable != null && p.timetable.CurrentAssignment == TimeAssignmentDefOf.Sleep) return false;
            var job = p?.CurJob;
            if (job == null) return true;
            if (job.playerForced) return false;
            var d = job.def;
            return d != null && (d.joyKind != null || d == JobDefOf.Wait_Wander || d == JobDefOf.GotoWander || d == JobDefOf.Wait);
        }

        /// <summary>At a party, a feast, a ritual or a council meeting: not to be called away from it for a word.</summary>
        private static bool AtAGathering(Pawn p)
        {
            var job = p?.GetLord()?.LordJob;
            return job is LordJob_Joinable_Gathering || job is LordJob_Ritual || job is LordJob_CouncilMeeting || job is LordJob_PlayOut;
        }

        /// <summary>
        /// <paramref name="a"/> sets out to have a word with <paramref name="b"/>: only in their free time, when it is
        /// safe, with nothing more pressing (hunger, exhaustion, a sickbed, urgent work), and with <paramref name="b"/>
        /// up and about on the same map, reachable, and not at a gathering, meeting or ritual.
        /// </summary>
        private static bool TryGo(Pawn a, Pawn b, int id)
        {
            try
            {
                var def = TalkJob;
                if (def == null || !Able(a) || !Able(b) || a.Map != b.Map) return false;
                var map = a.Map;
                if (a.CurJobDef == def || !FreeTime(a) || a.GetLord() != null || AtAGathering(b)) return false;
                if (!PlayOuts.CanTakePart(a, map) || PlayOuts.WhyNotNow(map, false) != null) return false;
                if (PlayOutTracker.Instance?.RunningFor(a) != null) return false;
                if (!a.CanReach(b, PathEndMode.Touch, Danger.Some)) return false;
                var job = JobMaker.MakeJob(def, b);
                job.count = id;
                a.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                return a.CurJobDef == def;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("together walk", e);
                return false;
            }
        }
    }

    /// <summary>
    /// A colonist goes to have a word with someone (Together): walks over, and once beside them, turns to them and talks
    /// a moment. What was waiting for the two of them (and the line said) comes when they get there. Gives up at any
    /// danger, when the other is no longer up to it, or when the walk takes too long; like any job, when they're drafted,
    /// downed or need something urgently.
    /// </summary>
    public class JobDriver_CouncilTalk : JobDriver
    {
        private int started = -1;
        private bool arrived;

        private Pawn Other => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport()
        {
            var other = Other;
            if (other == null) return base.GetReport();
            return (arrived ? "CC_Together_ReportTalking" : "CC_Together_ReportGoing").Translate(other.Named("OTHER")).Resolve();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            this.FailOn(() => !Together.Able(Other));
            this.FailOn(() => !arrived && started >= 0 && Find.TickManager.TicksGame - started > TogetherMath.WalkTicks);
            var go = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            go.AddPreInitAction(() =>
            {
                if (started < 0) started = Find.TickManager.TicksGame;
            });
            yield return go;
            var talk = ToilMaker.MakeToil("CouncilTalk");
            talk.initAction = () =>
            {
                arrived = true;
                var other = Other;
                if (other == null) return;
                pawn.rotationTracker.FaceTarget(other);
                if (other.pather == null || !other.pather.Moving) other.rotationTracker.FaceTarget(pawn);
                if (!Together.Arrived(pawn, other, job.count)) return;
                pawn.needs?.joy?.GainJoy(0.03f, JoyKindDefOf.Social);
            };
            talk.tickIntervalAction = delta =>
            {
                var other = Other;
                if (other != null && !pawn.pather.Moving) pawn.rotationTracker.FaceTarget(other);
            };
            talk.defaultCompleteMode = ToilCompleteMode.Delay;
            talk.defaultDuration = TogetherMath.TalkTicks;
            talk.handlingFacing = true;
            yield return talk;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref started, "started", -1);
            Scribe_Values.Look(ref arrived, "arrived");
        }
    }
}
