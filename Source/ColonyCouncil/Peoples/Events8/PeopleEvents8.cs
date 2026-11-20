using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // The peoples' own events, round 8 (docs/peoples-events-proposals.md, items 71 to 82; the defs in
    // Defs/IncidentDefs/Rimfolk_Events_PeopleEvents8.xml and Defs/CampDefs/Rimfolk_CampKinds_PeopleEvents8.xml): the Last Republic's
    // clerk who keeps the roll and stands by the count at the colony's election, its medal read at the gate for a colonist's deed in a
    // fight, the deserter its Civic Guard follows to the gate, and its ration post by the colony's road in a famine; the Hollow
    // Fleet's officers who walk and log the colony's gravship, its sleeper in one of the game's old caskets on the colony's land, the
    // order to disembark a colonist of the Empire's rank gives at its hull, and its landing party camped by the colony; the Helix
    // Works' gene taken out of a colonist, a whole xenogerm of one of its kinds, a young hybrid from its vats, and its field lab.
    // Each comes only from a people of its calling at peace with the colony (the sleeper's runner and the fugitive's detail too),
    // takes the form of the colony's age and theirs, and plays out on the map, built on rounds 1 to 7's shapes: the visitor at the
    // gate, the visitor who works on a colonist (the gene work's hold-and-work job), the season away (a caravan of one), the camp kind
    // with ways of its own, arrivals to a camp, the election and its hustings, the deeds and the peoples' memory, the Republic's
    // radio and papers, the Fleet's shuttle, release B's arising, the events' visits ("pev:...", dispatched through rounds 1 to 7).

    /// <summary>Round 8's visits, by kind, its watch, its jobs' ends, and what its events share.</summary>
    public static class PeopleEvents8
    {
        public static int Now => Find.TickManager.TicksGame;

        public static JobDef RollJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv8_Roll");
        public static JobDef StandByJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv8_StandBy");
        public static JobDef CitationJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv8_Citation");
        public static JobDef RationJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv8_Ration");
        public static JobDef DeckJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv8_Deck");
        public static JobDef OpenCasketJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv8_OpenCasket");
        public static JobDef SurveyJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv8_Survey");
        public static JobDef GeneOutJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv8_GeneOut");
        public static JobDef XenogermJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv8_Xenogerm");
        public static JobDef LabGeneJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv8_LabGene");
        public static JobDef LabSampleJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv8_LabSample");
        public static JobDef FieldSampleJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv8_FieldSample");

        /// <summary>An hour of one of round 8's visits: false when it is over (or not one of theirs).</summary>
        public static bool Hour(Visit v, CouncilGameComponent comp, int now)
        {
            switch (v?.kind)
            {
                case VoteObserver.Kind: return VoteObserver.Hour(v, now);
                case RepublicMedal.Kind: return RepublicMedal.Hour(v, now);
                case GravshipLog.Kind: return GravshipLog.Hour(v, now);
                case SleeperOfTheirs.Kind: return SleeperOfTheirs.Hour(v, now);
                case GeneTakenOut.Kind: return GeneTakenOut.Hour(v, now);
                case WholeXenogerm.Kind: return WholeXenogerm.Hour(v, now);
            }
            // Round 9's (Peoples/Events9/): the feral mech and its wardens, the warden of the quiet, the listener at a break, the evenings with the overseer.
            return PeopleEvents9.Hour(v, comp, now);
        }

        /// <summary>
        /// The homes' hour (HappeningTracker.Hourly, through rounds 3 to 7's): an election the Republic may observe, a deed it may honour,
        /// a famine its ration post may come for, a sleeper of the Fleet's woken at the colony.
        /// </summary>
        public static void Hourly(int now)
        {
            var homes = Homes.All;
            for (int i = 0; i < homes.Count; i++)
            {
                var map = homes[i];
                if (map == null || MapFacts.InVacuum(map)) continue;
                Safe("the vote", () => VoteObserver.Watch(map, now));
            }
            Safe("the deeds", () => RepublicMedal.Watch(now));
            Safe("the famines", () => RationPost.Watch(now));
            Safe("the sleepers woken", () => SleeperOfTheirs.Watch(now));
            // Round 9's watch (Peoples/Events9/): the Lattice's links and dreams, minds in a long break, the void's word, the vault's call.
            Safe("round 9", () => PeopleEvents9.Hourly(now));
        }

        /// <summary>A new game or a load: what is kept only between hours is forgotten.</summary>
        public static void Forget()
        {
        }

        /// <summary>One of round 8's jobs done at its target: what it was for.</summary>
        public static void WorkDone(Pawn pawn, JobDef job, LocalTargetInfo target)
        {
            if (pawn == null || job == null) return;
            try
            {
                if (job == RollJob) VoteObserver.Rolled(pawn, target.Pawn);
                else if (job == CitationJob) RepublicMedal.Read(pawn);
                else if (job == RationJob) RationPost.Taken(pawn);
                else if (job == DeckJob) GravshipLog.Walked(pawn, target.Thing);
                else if (job == OpenCasketJob) SleeperOfTheirs.Opened(pawn, target.Thing);
                else if (job == SurveyJob) LandingParty.Surveyed(pawn, target.Cell);
                else if (job == GeneOutJob) GeneTakenOut.Done(pawn, target.Pawn);
                else if (job == XenogermJob) WholeXenogerm.Done(pawn, target.Pawn);
                else if (job == LabGeneJob) FieldLab.GeneDone(pawn, target.Pawn);
                else if (job == LabSampleJob) FieldLab.SampleDone(pawn, target.Pawn);
                else if (job == FieldSampleJob) FieldLab.Sampled(pawn, target);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 8: work done", e);
            }
        }

        /// <summary>
        /// One of theirs (or one of the colony's) sent to work at a target (and then the rest of a queue, if any) for so long each, at
        /// an errand's pace; false when it couldn't start.
        /// </summary>
        public static bool Send(Pawn p, JobDef job, LocalTargetInfo target, int ticks, List<LocalTargetInfo> rest = null)
        {
            if (p == null || job == null || !target.IsValid || p.Dead || !p.Spawned || p.Downed || p.InMentalState || p.CurJobDef == job) return false;
            if (!p.CanReach(target, PathEndMode.Touch, Danger.Some)) return false;
            return Start(p, job, target, ticks, rest, IntVec3.Invalid);
        }

        /// <summary>The job started (with a meeting place first when <paramref name="meet"/> is valid: JobDriver_PEv8Work); false when it didn't take.</summary>
        private static bool Start(Pawn p, JobDef job, LocalTargetInfo target, int ticks, List<LocalTargetInfo> rest, IntVec3 meet)
        {
            try
            {
                var j = JobMaker.MakeJob(job, target);
                if (meet.IsValid) j.targetB = meet;
                j.count = Mathf.Max(60, ticks);
                j.locomotionUrgency = LocomotionUrgency.Walk;
                if (!rest.NullOrEmpty()) j.targetQueueA = rest.ToList();
                p.jobs.StartJob(j, JobCondition.InterruptForced, null, false, true);
                return p.CurJobDef == job;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 8: a job", e);
                return false;
            }
        }

        // ---------------- Meeting at the gate ----------------
        // The player's report of 20 November 2026: the medal was handed over at once, unseen, though the answer said the colony would
        // gather at the gate. The clerk's errand was to walk to the colonist wherever they were, and Rimfolk's visitors keep out of the
        // colony's bedrooms and cold stores (Visitors/KeepOut.cs: their doors are barred to visitors), so with the colonist in such a room
        // the clerk had no way to them and the citation was given there and then. Now the colonist walks out to the visitor at the gate,
        // and the visitor never needs a way into the colony for it.

        /// <summary>
        /// Where one of theirs and one of the colony meet: the gate spot, where a visit's people wait; else the spot their visit was
        /// taking them to; else where the visitor stands. Never a place inside the colony the visitor would have to be let into.
        /// </summary>
        public static IntVec3 MeetSpot(Pawn x, Map map)
        {
            if (!PeoplesG.Up(x, map)) return IntVec3.Invalid;
            var gate = PeoplesD.GateSpot(map);
            if (gate.HasValue && x.CanReach(gate.Value, PathEndMode.OnCell, Danger.Some)) return gate.Value;
            var focus = x.mindState?.duty?.focus ?? LocalTargetInfo.Invalid;
            if (focus.IsValid && !focus.HasThing && focus.Cell.InBounds(map) && focus.Cell.Standable(map) && x.CanReach(focus.Cell, PathEndMode.OnCell, Danger.Some)) return focus.Cell;
            return x.Position;
        }

        /// <summary>The <paramref name="i"/>th place to stand round the meeting place (two or three cells from it), not one already taken.</summary>
        public static IntVec3 Beside(Map map, IntVec3 spot, int i, ICollection<IntVec3> taken = null)
        {
            if (map == null || !spot.IsValid) return IntVec3.Invalid;
            for (int k = 0; k < PlayOutMath.RingCount; k++)
            {
                PlayOutMath.RingOffset(i + k, out int dx, out int dz);
                var c = new IntVec3(spot.x + dx, 0, spot.z + dz);
                if (!c.InBounds(map) || !c.Standable(map) || c.GetEdifice(map) != null || c.Fogged(map) || (c.GetTerrain(map)?.IsWater ?? false)) continue;
                if (taken != null && taken.Contains(c)) continue;
                return c;
            }
            return CellFinder.StandableCellNear(spot, map, 3f);
        }

        /// <summary>
        /// One of the colony sent out to stand beside the visit's meeting place (its cell) until the visit is done, kept in its touched
        /// list so <see cref="Release"/> lets them go; false when they can't go now (drafted, in a break, down, no way out to it).
        /// </summary>
        public static bool SendOut(Visit v, Pawn p, int i, ICollection<IntVec3> taken = null)
        {
            var map = v?.map;
            if (map == null || !v.cell.IsValid || !PeoplesG.Up(p, map) || p.Drafted || p.InMentalState) return false;
            if (p.CurJobDef == StandByJob && p.CurJob.targetA.Cell.InHorDistOf(v.cell, PeopleEvents8Math.MeetRange)) return true;
            var c = Beside(map, v.cell, i, taken);
            if (!c.IsValid || !Send(p, StandByJob, c, Mathf.Max(GenDate.TicksPerHour, v.until - Now))) return false;
            taken?.Add(c);
            if (!v.touched.Contains(p)) v.touched.Add(p);
            return true;
        }

        /// <summary>Those of the colony sent out to stand by at the meeting place go back to their day.</summary>
        public static void Release(Visit v)
        {
            if (v?.touched == null || !v.cell.IsValid) return;
            foreach (var p in v.touched)
                if (p != null && p.Spawned && p.CurJobDef == StandByJob && p.CurJob.targetA.Cell.InHorDistOf(v.cell, PeopleEvents8Math.MeetRange + 2f))
                    p.jobs.EndCurrentJob(JobCondition.Succeeded);
        }

        /// <summary>
        /// The meeting set going (again): the visitor, on an errand of the visit's hours, to the meeting place (the visit's cell, found if
        /// it has none), there to wait for the colonist (v.a) and then do the work at them; the colonist sent out to stand beside it if
        /// they can go now (the visit's hour sends them again if not). False when the visitor's part couldn't start.
        /// </summary>
        public static bool Meet(Visit v, JobDef job, int ticks)
        {
            var x = v?.x;
            var map = v?.map;
            if (job == null || v.a == null || !PeoplesG.Up(x, map)) return false;
            if (x.CurJobDef != job)
            {
                if (!v.cell.IsValid || !x.CanReach(v.cell, PathEndMode.OnCell, Danger.Some)) v.cell = MeetSpot(x, map);
                if (!v.cell.IsValid) return false;
                Lords.Leave(x);
                LordMaker.MakeNewLord(x.Faction, new LordJob_VisitOnErrand(x.Faction, v.cell, Mathf.Max(GenDate.TicksPerHour, v.until - Now)), map, new List<Pawn> { x });
                if (!Start(x, job, v.a, ticks, null, v.cell)) return false;
            }
            SendOut(v, v.a, 0);
            return true;
        }

        /// <summary>
        /// One of theirs set to work at someone of the colony (v.a): straight to them where they are when nothing bars the way, else
        /// the colonist walks out to them and the work is done at the meeting place (<see cref="Meet"/>). False when neither could start.
        /// </summary>
        public static bool WorkOn(Visit v, JobDef job, int ticks)
        {
            var x = v?.x;
            var a = v?.a;
            var map = v?.map;
            if (job == null || !PeoplesG.Up(x, map) || !PeoplesG.Up(a, map)) return false;
            if (x.CurJobDef == job) return true;
            float hours = Mathf.Max(1f, (v.until - Now) / (float)GenDate.TicksPerHour);
            if (x.CanReach(a, PathEndMode.Touch, Danger.Some) && Errand(x, map, a, null, job, ticks, hours)) return true;
            return Meet(v, job, ticks);
        }

        /// <summary>
        /// The Works' work at a colonist (the gene taken out, the whole xenogerm) that couldn't be done in the visit's hours: the
        /// sequencer leaves what the colony paid at the gate and goes home, said as it is.
        /// </summary>
        public static void Undone(Visit v)
        {
            if (v == null || v.done) return;
            v.done = true;
            Release(v);
            var x = v.x;
            bool paid = LeaveBack(x);
            if (v.map != null)
                Tell.Message(NewsKind.People, Importance.Notable, v.map, (paid ? "CC_PEv8_WorkUndonePaid" : "CC_PEv8_WorkUndone")
                    .Translate((x?.LabelShort ?? "?").Named("SEQ"), (v.a?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                    x != null && x.Spawned ? new LookTargets(x) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            if (x != null) PeoplesG.Home(new List<Pawn> { x });
        }

        /// <summary>
        /// What the colony paid one of theirs for work that wasn't done, set down where they stand for the colony: the silver and
        /// genepacks in their pack. Returns whether anything was set down.
        /// </summary>
        public static bool LeaveBack(Pawn x)
        {
            var held = x?.inventory?.innerContainer;
            if (held == null || !x.Spawned) return false;
            bool any = false;
            foreach (var t in held.Where(t => t != null && (t.def == ThingDefOf.Silver || ModsConfig.BiotechActive && t is Genepack)).ToList())
            {
                if (held.TryDrop(t, x.Position, x.Map, ThingPlaceMode.Near, out var dropped))
                {
                    dropped?.SetForbidden(false, false);
                    any = true;
                }
            }
            return any;
        }

        /// <summary>
        /// One of theirs at the gate sent off to work at a target, kept there by a lord of their own at the work's spot so their visit's
        /// duties don't call them off (round G's errand); false when it couldn't start.
        /// </summary>
        public static bool Errand(Pawn x, Map map, LocalTargetInfo first, List<LocalTargetInfo> rest, JobDef job, int ticks, float hours)
        {
            if (!PeoplesG.Up(x, map) || job == null || !first.IsValid || !x.CanReach(first, PathEndMode.Touch, Danger.Some)) return false;
            Lords.Leave(x);
            LordMaker.MakeNewLord(x.Faction, new LordJob_VisitOnErrand(x.Faction, first.Cell, Mathf.RoundToInt(hours * GenDate.TicksPerHour)), map, new List<Pawn> { x });
            return Send(x, job, first, ticks, rest);
        }

        /// <summary>A job for one of a camp's people (its DayJob or NightJob): the same work at a target, for so long.</summary>
        public static Job Make(JobDef job, LocalTargetInfo target, int ticks)
        {
            if (job == null || !target.IsValid) return null;
            var j = JobMaker.MakeJob(job, target);
            j.count = Mathf.Max(60, ticks);
            return j;
        }

        public static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 8: " + what, e);
            }
        }

        /// <summary>A people at peace with the colony who may be paid or thanked (not the colony, not gone).</summary>
        public static bool Thanks(Faction f) => f != null && f.HasGoodwill && !f.IsPlayer && !f.defeated && !f.HostileTo(Faction.OfPlayer);

        /// <summary>Goodwill with a people at peace, for a reason.</summary>
        public static void Goodwill(Faction f, int amount, HistoryEventDef why)
        {
            if (Thanks(f) && amount != 0) WorldSituationWorker.Goodwill(f, amount, why);
        }

        /// <summary>A colonist's mood memory of round 8's own, if the def is there.</summary>
        public static void Thought(Pawn p, string def)
        {
            var t = DefDatabase<ThoughtDef>.GetNamedSilentFail(def);
            if (t != null && p != null && !p.Dead && p.needs?.mood != null) CouncilGameComponent.GiveThought(p, t, null);
        }

        /// <summary>The visit's people: its text starts with their id (PeoplesF.PeopleOf), else whoever came.</summary>
        public static Faction PeopleOf(Visit v) => PeopleEvents5.PeopleOf(v);

        /// <summary>The visit of this kind under way with this pawn as its visitor.</summary>
        public static Visit Of(string kind, Pawn x) =>
            x == null ? null : CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(v => v != null && v.kind == kind && !v.done && v.x == x);
    }

    /// <summary>How one of round 8's jobs looks while it is done: "look", "quiet", "sparks", "cross" (the healing cross, the one worked on kept still), "dust".</summary>
    public class PEv8Work : DefModExtension
    {
        public string mark = "look";
    }

    /// <summary>
    /// One of round 8's jobs (TargetA a thing, a person or a cell, and a queue after it): up to it, a while at it with a bar and its mark
    /// (PEv8Work), then what it was for (PeopleEvents8.WorkDone); the next of the queue, if any. With a meeting place (TargetB a cell,
    /// TargetA someone of the colony: PeopleEvents8.Meet), first to the place, and a wait there until they have come. A target out of
    /// reach (behind a door barred to visitors: a bedroom, a cold store) is passed over. With the healing cross, someone of the colony
    /// it works on keeps still while it is done (round G's hold). Its place in the queue isn't kept with the game: a job loaded halfway
    /// starts its current target again.
    /// </summary>
    public class JobDriver_PEv8Work : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        private string Mark => job.def.GetModExtension<PEv8Work>()?.mark ?? "look";

        /// <summary>The one to meet, when the job has a meeting place.</summary>
        private Pawn Met => job.targetB.IsValid && !job.targetB.HasThing ? job.targetA.Pawn : null;

        private bool Here(Pawn other) =>
            other != null && !other.Dead && other.Spawned && other.Map == pawn.Map && PeopleEvents8Math.Met(other.Position.DistanceTo(pawn.Position));

        protected override IEnumerable<Toil> MakeNewToils()
        {
            if (Met != null)
            {
                // The meeting place first (the gate): there, the visitor waits for the colonist to come out to them.
                yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
                var wait = ToilMaker.MakeToil("PEv8Meet");
                wait.tickIntervalAction = delta =>
                {
                    var other = Met;
                    if (other == null || other.Dead || !other.Spawned || other.Map != pawn.Map)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    pawn.rotationTracker.FaceCell(other.Position);
                    if (Here(other)) ReadyForNextToil();
                };
                wait.defaultCompleteMode = ToilCompleteMode.Delay;
                wait.defaultDuration = PeopleEvents8Math.MeetWaitTicks;
                wait.handlingFacing = true;
                yield return wait;
                var met = ToilMaker.MakeToil("PEv8Met");
                met.initAction = () =>
                {
                    // The wait ran out and they never came: the visit's hour sends them again, or says why it couldn't be done.
                    if (!Here(Met)) EndJobWith(JobCondition.Incompletable);
                };
                met.defaultCompleteMode = ToilCompleteMode.Instant;
                yield return met;
            }
            var next = ToilMaker.MakeToil("PEv8Next");
            next.initAction = () =>
            {
                // The target gone (a voter away, a part taken down) or out of reach behind a barred door: on to the next of the queue.
                while (!job.targetA.IsValid || job.targetA.HasThing && (job.targetA.Thing.Destroyed || !job.targetA.Thing.Spawned)
                       || !pawn.CanReach(job.targetA, PathEndMode.Touch, Danger.Some))
                {
                    if (job.targetQueueA.NullOrEmpty())
                    {
                        EndJobWith(JobCondition.Succeeded);
                        return;
                    }
                    job.targetA = job.targetQueueA[0];
                    job.targetQueueA.RemoveAt(0);
                }
                // The one worked on keeps still while the Works' instruments are on them.
                var on = job.targetA.Pawn;
                if (Mark == "cross" && on != null && on != pawn && on.Faction != null && on.Faction.IsPlayer) PeoplesG.Hold(on, job.count + 1500);
            };
            next.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return next;
            yield return Toils_Goto.Goto(TargetIndex.A, PathEndMode.Touch);
            var work = ToilMaker.MakeToil("PEv8Work");
            work.tickIntervalAction = delta =>
            {
                var at = job.targetA.HasThing ? job.targetA.Thing.PositionHeld : job.targetA.Cell;
                pawn.rotationTracker.FaceCell(at);
                if (!pawn.IsHashIntervalTick(240, delta)) return;
                var map = pawn.Map;
                switch (Mark)
                {
                    case "sparks": FleckMaker.ThrowMicroSparks(at.ToVector3Shifted(), map); break;
                    case "cross": FleckMaker.ThrowMetaIcon(at, map, FleckDefOf.HealingCross); break;
                    case "dust": FleckMaker.ThrowDustPuff(at, map, 0.5f); break;
                    case "look":
                    case "quiet": break;
                }
            };
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(60, job.count);
            work.WithProgressBarToilDelay(TargetIndex.A);
            work.handlingFacing = true;
            yield return work;
            var done = ToilMaker.MakeToil("PEv8WorkDone");
            done.initAction = () =>
            {
                PeopleEvents8.WorkDone(pawn, job.def, job.targetA);
                job.targetA = LocalTargetInfo.Invalid;
                if (!job.targetQueueA.NullOrEmpty()) JumpToToil(next);
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }
}
