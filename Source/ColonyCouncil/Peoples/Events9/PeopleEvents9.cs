using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The peoples' own events, round 9 (docs/peoples-events-proposals.md, items 84 to 94; the defs in
    // Defs/IncidentDefs/Rimfolk_Events_PeopleEvents9.xml and Defs/CampDefs/Rimfolk_CampKinds_PeopleEvents9.xml): the Lattice's link for a
    // mechanitor of the colony's, its mech gone feral near the colony and its mech yard; the Choir of the Spire's hum deepened in a
    // colonist through a night, its warden of the quiet who sits with one shaken by the void, its listener who sits beside a mind that
    // shouts, and its listening ring; the Sunken Vault's broken seal and the fight below it, its last bionic for a colonist who lost a
    // limb or an eye, the overseer who talks with the colony over the radio (or by letters), and its surface scouts camped a week.
    // Each comes only from a people of its calling at peace with the colony, takes the form of the colony's age and theirs, and plays
    // out on the map, built on rounds 1 to 8's shapes: the visitor at the gate, the visitor who works on a colonist (PeopleWorkOn), the
    // camp kind with ways of its own, bands against a camp, round G's lost mech, listening and vault supply, the calls to arms' fight
    // beside a town, quests' sites and the places heard of, the events' visits ("pev:...", dispatched through rounds 1 to 8).

    /// <summary>Round 9's visits, by kind, its watch, its jobs' ends, and what its events share.</summary>
    public static class PeopleEvents9
    {
        public static int Now => Find.TickManager.TicksGame;

        public static JobDef RelinkJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv9_Relink");
        public static JobDef SitWithJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv9_SitWith");
        public static JobDef ListenEdgeJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv9_ListenEdge");
        public static JobDef TendMechJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv9_TendMech");
        public static JobDef CutRockJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv9_CutRock");
        public static JobDef MendJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv9_Mend");
        public static JobDef RingSitJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv9_RingSit");
        public static JobDef SingJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv9_Sing");
        public static JobDef ConsoleJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv9_Console");
        public static JobDef LessonJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv9_Lesson");
        public static JobDef SearchJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv9_Search");
        public static JobDef TalkOutJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv9_TalkOut");
        public static JobDef LookAtJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv9_LookAt");

        /// <summary>An hour of one of round 9's visits: false when it is over (or not one of theirs).</summary>
        public static bool Hour(Visit v, CouncilGameComponent comp, int now)
        {
            switch (v?.kind)
            {
                case FeralMech.Kind: return FeralMech.Hour(v, now);
                case VoidListens.Kind: return VoidListens.Hour(v, now);
                case MindShouts.Kind: return MindShouts.Hour(v, now);
                case ConsoleTalk.Kind: return ConsoleTalk.Hour(v, now);
            }
            // Round 10's (Peoples/Events10/): the hearings at the table and the gate, the days at the table, an art shown at the gate.
            return PeopleEvents10.Hour(v, comp, now);
        }

        /// <summary>
        /// The homes' hour (HappeningTracker.Hourly, through rounds 3 to 8's): the Lattice's links held or broken and their dreams, a mind
        /// in a long break the Choir may hear, the void's word kept, the colony's fighters on the road to the vault and the fight there.
        /// </summary>
        public static void Hourly(int now)
        {
            var homes = Homes.All;
            for (int i = 0; i < homes.Count; i++)
            {
                var map = homes[i];
                if (map == null || MapFacts.InVacuum(map)) continue;
                Safe("the minds that shout", () => MindShouts.Watch(map, now));
            }
            Safe("the Lattice's links", () => LatticeLinked.Watch(now));
            Safe("the void's word", () => VoidListens.Watch(now));
            Safe("the vault's seal", () => SealBroken.Watch(now));
            // Round 10's watch (Peoples/Events10/): the season's prices, the colony's people at a plague town.
            Safe("round 10", () => PeopleEvents10.Hourly(now));
        }

        /// <summary>A new game or a load: what is kept only between hours is forgotten.</summary>
        public static void Forget()
        {
        }

        /// <summary>One of round 9's jobs done at its target: what it was for.</summary>
        public static void WorkDone(Pawn pawn, JobDef job, LocalTargetInfo target)
        {
            if (pawn == null || job == null) return;
            try
            {
                if (job == RelinkJob) FeralMech.Relinked(pawn, target.Pawn);
                else if (job == SitWithJob)
                {
                    if (!MindShouts.Sat(pawn, target.Pawn)) VoidListens.Sat(pawn, target.Pawn);
                }
                else if (job == ListenEdgeJob) VoidListens.Listened(pawn);
                else if (job == TendMechJob) MechYard.Tended(pawn, target.Pawn);
                else if (job == CutRockJob) MechYard.Cut(pawn, target.Thing);
                else if (job == MendJob) MechYard.Mended(pawn, target.Thing);
                else if (job == RingSitJob) ListeningRing.SatWith(pawn);
                else if (job == SingJob) ListeningRing.Sang(pawn);
                else if (job == ConsoleJob) ConsoleTalk.Talked(pawn);
                else if (job == LessonJob) SurfaceScouts.Taught(pawn);
                else if (job == SearchJob) SurfaceScouts.Searched(pawn);
                else if (job == TalkOutJob) SurfaceScouts.TalkedOut(pawn, target.Pawn);
                else if (job == LookAtJob) SurfaceScouts.Looked(pawn, target.Pawn);
                // Round 10's work at a place is done with this driver too (Peoples/Events10/).
                else PeopleEvents10.WorkDone(pawn, job, target);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 9: work done", e);
            }
        }

        /// <summary>
        /// One of theirs (or one of the colony's) sent to work at a target for so long, at an errand's pace; false when it couldn't start.
        /// </summary>
        public static bool Send(Pawn p, JobDef job, LocalTargetInfo target, int ticks)
        {
            if (p == null || job == null || !target.IsValid || p.Dead || !p.Spawned || p.Downed || p.InMentalState || p.CurJobDef == job) return false;
            if (!p.CanReach(target, PathEndMode.Touch, Danger.Some)) return false;
            try
            {
                var j = JobMaker.MakeJob(job, target);
                j.count = Mathf.Max(60, ticks);
                j.locomotionUrgency = LocomotionUrgency.Walk;
                p.jobs.StartJob(j, JobCondition.InterruptForced, null, false, true);
                return p.CurJobDef == job;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 9: a job", e);
                return false;
            }
        }

        /// <summary>
        /// One of theirs at the gate sent off to work at a target, kept there by a lord of their own at the work's spot so their visit's
        /// duties don't call them off (round G's errand); false when it couldn't start.
        /// </summary>
        public static bool Errand(Pawn x, Map map, LocalTargetInfo target, JobDef job, int ticks, float hours)
        {
            if (!PeoplesG.Up(x, map) || job == null || !target.IsValid || !x.CanReach(target, PathEndMode.Touch, Danger.Some)) return false;
            Lords.Leave(x);
            Verse.AI.Group.LordMaker.MakeNewLord(x.Faction, new LordJob_VisitOnErrand(x.Faction, target.Cell, Mathf.RoundToInt(hours * GenDate.TicksPerHour)), map, new List<Pawn> { x });
            return Send(x, job, target, ticks);
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
                CouncilLog.ErrorOnce("peoples' events 9: " + what, e);
            }
        }

        /// <summary>A people at peace with the colony who may be paid or thanked (not the colony, not gone).</summary>
        public static bool Thanks(Faction f) => f != null && f.HasGoodwill && !f.IsPlayer && !f.defeated && !f.HostileTo(Faction.OfPlayer);

        /// <summary>Goodwill with a people at peace, for a reason.</summary>
        public static void Goodwill(Faction f, int amount, HistoryEventDef why)
        {
            if (Thanks(f) && amount != 0) WorldSituationWorker.Goodwill(f, amount, why);
        }

        /// <summary>A colonist's mood memory of round 9's own, if the def is there.</summary>
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

        /// <summary>The colony's grown people who are up and about at home.</summary>
        public static List<Pawn> Grown(Map map) => PeoplesG.Grown(map).ToList();
    }

    /// <summary>
    /// How one of round 9's jobs looks while it is done: "look", "quiet", "sparks", "cross" (the healing cross), "dust", "hum" (the
    /// meditation's mark); <see cref="follow"/>: with a person as the target, the worker keeps at their side as they move about.
    /// </summary>
    public class PEv9Work : DefModExtension
    {
        public string mark = "look";
        public bool follow;
    }

    /// <summary>
    /// One of round 9's jobs (TargetA a thing, a person or a cell): up to it, a while at it with a bar and its mark (PEv9Work), then what it
    /// was for (PeopleEvents9.WorkDone). With <see cref="PEv9Work.follow"/>, a person who walks off is followed and the work goes on at
    /// their side; what is left of the work is kept with the game.
    /// </summary>
    public class JobDriver_PEv9Work : JobDriver
    {
        private int left = -1;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        private PEv9Work Ext => job.def.GetModExtension<PEv9Work>();

        private string Mark => Ext?.mark ?? "look";

        private bool Follow => Ext?.follow ?? false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref left, "left", -1);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !job.targetA.IsValid || job.targetA.HasThing && (job.targetA.Thing.Destroyed || !job.targetA.Thing.Spawned));
            var start = ToilMaker.MakeToil("PEv9Start");
            start.initAction = () =>
            {
                if (left < 0) left = Mathf.Max(60, job.count);
            };
            start.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return start;
            var go = Toils_Goto.Goto(TargetIndex.A, PathEndMode.Touch);
            yield return go;
            var work = ToilMaker.MakeToil("PEv9Work");
            work.tickIntervalAction = delta =>
            {
                var at = job.targetA.HasThing ? job.targetA.Thing.PositionHeld : job.targetA.Cell;
                // Someone they keep beside who has walked on: after them.
                if (Follow && job.targetA.Pawn is Pawn other && !other.Position.InHorDistOf(pawn.Position, 3.5f))
                {
                    JumpToToil(go);
                    return;
                }
                pawn.rotationTracker.FaceCell(at);
                left -= delta;
                if (left <= 0)
                {
                    ReadyForNextToil();
                    return;
                }
                if (!pawn.IsHashIntervalTick(240, delta)) return;
                var map = pawn.Map;
                switch (Mark)
                {
                    case "sparks": FleckMaker.ThrowMicroSparks(at.ToVector3Shifted(), map); break;
                    case "cross": FleckMaker.ThrowMetaIcon(at, map, FleckDefOf.HealingCross); break;
                    case "dust": FleckMaker.ThrowDustPuff(at, map, 0.5f); break;
                    case "hum": FleckMaker.ThrowMetaIcon(pawn.Position, map, FleckDefOf.Meditating); break;
                    case "look":
                    case "quiet": break;
                }
            };
            work.defaultCompleteMode = ToilCompleteMode.Never;
            work.WithProgressBar(TargetIndex.A, () => 1f - Mathf.Clamp01(left / (float)Mathf.Max(60, job.count)));
            work.handlingFacing = true;
            yield return work;
            var done = ToilMaker.MakeToil("PEv9WorkDone");
            done.initAction = () => PeopleEvents9.WorkDone(pawn, job.def, job.targetA);
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }
}
