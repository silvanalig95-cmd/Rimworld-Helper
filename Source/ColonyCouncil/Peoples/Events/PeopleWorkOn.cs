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
    // The visitor who works on a colonist (docs/peoples-events-proposals.md, round 1's shared shape: the order's limb, the
    // Conclave's storm-sense and the Lattice's mechlink, and later the enclave's bionics, the beast-doctor's leg, the Works'
    // gene taken out and whole xenogerm, the Choir's hum deepened and the vault's last part). Release G's gene work
    // (JobDriver_PeoplesG) made into one shape for any people: the colonist is brought where the work is done (held where
    // they stand, laid in a bed, or walked to a place and kept there), one of theirs walks over and works on them a while
    // (a bar, and the mark of the work: the healing cross, sparks, the hum, the storm), and then what it was for, by the
    // job's own handler (PeopleWorkJob.done). The order waits as one of the events' visits, so a save keeps it: a job loaded
    // halfway starts again, a worker pulled away is sent again, and after its hours the order is given up and told.

    /// <summary>How one kind of work on a colonist is done (on its JobDef): where they are while it's done, its mark, and what it was for.</summary>
    public class PeopleWorkJob : DefModExtension
    {
        /// <summary>
        /// "here" (held where they stand), "bed" (laid in the bed, the order's place), "beside" (walked to the place and kept
        /// there), "sky" (walked out under the open sky by the place, or by themselves without one, and kept there).
        /// </summary>
        public string place = "here";
        /// <summary>"cross" (the healing cross), "sparks", "hum" (the psychic hum), "storm" (the hum and the rain), "sow" (earth turned).</summary>
        public string mark = "cross";
        /// <summary>
        /// The worker works at the place itself (a bench), its mark there, while the one it is for stands by and watches
        /// (round 2: the guild's rework of a colonist's weapon at the colony's bench); else at the colonist.
        /// </summary>
        public bool atPlace;
        /// <summary>A PeopleWorkDone: what the work was for.</summary>
        public Type done;

        [Unsaved] private PeopleWorkDone doneInt;

        public PeopleWorkDone Done
        {
            get
            {
                if (doneInt == null && done != null && typeof(PeopleWorkDone).IsAssignableFrom(done)) doneInt = (PeopleWorkDone)Activator.CreateInstance(done);
                return doneInt;
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (done == null || !typeof(PeopleWorkDone).IsAssignableFrom(done)) yield return "people's work has no done handler";
            if (place != "here" && place != "bed" && place != "beside" && place != "sky") yield return "people's work place must be here, bed, beside or sky";
        }
    }

    /// <summary>What a kind of work on a colonist was for (the limb fitted, the psylink woken, the mechlink in).</summary>
    public abstract class PeopleWorkDone
    {
        /// <summary>The work is done: <paramref name="worker"/> worked on <paramref name="on"/> for the order <paramref name="v"/> (its note, its place).</summary>
        public abstract void Done(Visit v, Pawn worker, Pawn on);

        /// <summary>The order given up (the colonist gone, its hours past): what is told, and anything owed given back.</summary>
        public virtual void GaveUp(Visit v)
        {
        }
    }

    public static class PeopleWorkOn
    {
        public const string Kind = "pev:work";

        /// <summary>How many times a worker pulled away is sent again before the order is given up.</summary>
        public const int Sends = 4;

        public static int Now => Find.TickManager.TicksGame;

        public static PeopleWorkJob Ext(JobDef def) => def?.GetModExtension<PeopleWorkJob>();

        /// <summary>The note an order carries (after the job's name).</summary>
        public static string Note(Visit v)
        {
            string t = v?.text ?? "";
            int bar = t.IndexOf('|');
            return bar < 0 ? null : t.Substring(bar + 1);
        }

        public static JobDef JobOf(Visit v)
        {
            string t = v?.text ?? "";
            int bar = t.IndexOf('|');
            return DefDatabase<JobDef>.GetNamedSilentFail(bar < 0 ? t : t.Substring(0, bar));
        }

        /// <summary>
        /// The work begins: <paramref name="worker"/> of the event's people goes to work on <paramref name="on"/> (at
        /// <paramref name="place"/>, when the work has one) for <paramref name="ticks"/>, the order kept as a visit of
        /// <paramref name="hours"/>; <paramref name="note"/> is what the handler needs (a gene, a recipe), <paramref name="stake"/>
        /// silver to give back should it be given up. With the play-outs off, done at once.
        /// </summary>
        public static bool Begin(HappeningContext ctx, Pawn worker, Pawn on, JobDef job, LocalTargetInfo place, string note, int stake, int ticks, float hours)
        {
            var map = ctx?.map;
            var tracker = CouncilGameComponent.Instance?.happenings;
            if (map == null || worker == null || on == null || job == null || tracker == null) return false;
            var v = tracker.AddVisit(Kind, ctx.incident, map, worker, on, PeoplesG.Party(ctx).Where(p => p != worker), Now + Mathf.RoundToInt(hours * GenDate.TicksPerHour));
            v.place = place.Thing;
            v.cell = place.HasThing || !place.IsValid ? IntVec3.Invalid : place.Cell;
            v.text = job.defName + (note.NullOrEmpty() ? "" : "|" + note);
            v.stake = stake;
            v.due = Mathf.Max(60, ticks);
            // The worker's way into the colonist's room counts from now (visitors keep out of bedrooms but for a doctor's work).
            KeepOut.Refresh(map);
            if (PlayOuts.On && Send(v, true)) return true;
            Finish(v, worker, on, job);
            return true;
        }

        /// <summary>Where the order's work is done: its thing (a bed, a rod), else its cell (a seed's spot), else nowhere in particular.</summary>
        public static LocalTargetInfo PlaceOf(Visit v) => v?.place != null ? new LocalTargetInfo(v.place) : v != null && v.cell.IsValid ? new LocalTargetInfo(v.cell) : LocalTargetInfo.Invalid;

        /// <summary>The worker sent to the colonist on an errand of the order's hours, the job started; false when it couldn't be.</summary>
        private static bool Send(Visit v, bool first)
        {
            var x = v.x;
            var on = v.a;
            var map = v.map;
            var job = JobOf(v);
            if (!PeoplesG.Up(x, map) || !PeoplesG.Up(on, map) || job == null || !x.CanReach(on, PathEndMode.Touch, Danger.Some)) return false;
            try
            {
                if (first || x.GetLord() == null)
                {
                    Lords.Leave(x);
                    var at = PlaceOf(v);
                    var spot = at.IsValid && (!at.HasThing || at.Thing.Spawned) ? at.Cell : on.Position;
                    LordMaker.MakeNewLord(x.Faction, new LordJob_VisitOnErrand(x.Faction, spot, Mathf.Max(GenDate.TicksPerHour, v.until - Now)), map, new List<Pawn> { x });
                }
                var j = JobMaker.MakeJob(job, on, PlaceOf(v));
                j.locomotionUrgency = Pace.Errand;
                j.count = Mathf.Max(60, v.due);
                x.jobs.StartJob(j, JobCondition.InterruptForced, null, false, true);
                return x.CurJobDef == job;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: a worker sent to a colonist", e);
                return false;
            }
        }

        /// <summary>The order's hour: done, given up (the colonist or the worker gone, its hours past, sent too often), or the worker sent again.</summary>
        public static bool Hour(Visit v, CouncilGameComponent comp, int now)
        {
            if (v.done) return false;
            var job = JobOf(v);
            bool workerHere = PeoplesG.Up(v.x, v.map), onHere = v.a != null && !v.a.Dead && v.a.Spawned && v.a.Map == v.map;
            if (job == null || !onHere || !workerHere || now > v.until)
            {
                GiveUp(v);
                return false;
            }
            if (v.x.CurJobDef == job) return true;
            // Pulled away (a fight, a load): sent again, a few times.
            if (v.map.dangerWatcher.DangerRating != StoryDanger.None) return true;
            // Each send is marked once in the order's touched list (the worker again), so the count survives a save.
            if (v.touched.Count >= Sends)
            {
                GiveUp(v);
                return false;
            }
            v.touched.Add(v.x);
            Send(v, false);
            return true;
        }

        /// <summary>The worker's job is done (JobDriver_PeopleWorkOn): what it was for, then the worker and their party home.</summary>
        public static void Done(Pawn worker, Pawn on, JobDef job)
        {
            var visits = CouncilGameComponent.Instance?.happenings?.visits;
            var v = visits?.FirstOrDefault(x => x.kind == Kind && !x.done && x.x == worker && x.a == on && JobOf(x) == job);
            if (v == null) return;
            Finish(v, worker, on, job);
        }

        private static void Finish(Visit v, Pawn worker, Pawn on, JobDef job)
        {
            v.done = true;
            try
            {
                Ext(job)?.Done?.Done(v, worker, on);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: work done " + job?.defName, e);
            }
            Release(on);
            PeoplesG.Home(v.group.Prepend(worker));
        }

        private static void GiveUp(Visit v)
        {
            v.done = true;
            try
            {
                Ext(JobOf(v))?.Done?.GaveUp(v);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: work given up", e);
            }
            // The silver paid for work never done goes back with whoever of theirs is here.
            if (v.stake > 0 && v.map != null)
                Couriers.PaySilver(v.map, v.x != null && v.x.Spawned && v.x.Map == v.map ? v.x.Position : IntVec3.Invalid, v.stake, v.x?.Faction, PeoplesG.Up(v.x, v.map) ? v.x : null);
            Release(v.a);
            PeoplesG.Home(v.group.Prepend(v.x));
        }

        /// <summary>The colonist let go of the still they were kept in (a forced wait, a bed, a place), back to their own day.</summary>
        public static void Release(Pawn on)
        {
            if (on == null || !on.Spawned || on.Dead || on.jobs == null) return;
            var cur = on.CurJobDef;
            if (cur == JobDefOf.Wait || cur == JobDefOf.LayDownAwake || cur == KeepJob) on.jobs.EndCurrentJob(JobCondition.Succeeded);
        }

        // ================= The colonist kept for the work =================

        public static JobDef KeepJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv_Keep");

        /// <summary>Where the colonist stands for the work beside a place: the open cell next to it nearest them.</summary>
        public static IntVec3 Beside(LocalTargetInfo place, Pawn on)
        {
            if (!place.IsValid || place.HasThing && !place.Thing.Spawned || on == null || !on.Spawned) return IntVec3.Invalid;
            var map = on.Map;
            var around = place.HasThing ? GenAdj.CellsAdjacent8Way(place.Thing) : GenAdj.CellsAdjacent8Way(new TargetInfo(place.Cell, map));
            var open = around.Where(c => c.InBounds(map) && c.Standable(map) && (c.GetFirstPawn(map) == null || c == on.Position) && on.CanReach(c, PathEndMode.OnCell, Danger.Some))
                .OrderBy(c => c.DistanceToSquared(on.Position)).ToList();
            return open.Count > 0 ? open[0] : IntVec3.Invalid;
        }

        /// <summary>
        /// An open cell under the sky near <paramref name="anchor"/> (the rod, else where they stand): standable, nothing built
        /// there, nobody on it, one they can reach; the nearest of those within 24 cells.
        /// </summary>
        public static IntVec3 SkyCell(Map map, IntVec3 anchor, Pawn on)
        {
            if (map == null || !anchor.IsValid || on == null) return IntVec3.Invalid;
            foreach (var c in GenRadial.RadialCellsAround(anchor, 24f, true))
            {
                if (!c.InBounds(map) || c.Roofed(map) || !c.Standable(map) || c.GetEdifice(map) != null || c.Fogged(map)) continue;
                if (c.GetFirstPawn(map) is Pawn other && other != on) continue;
                if (on.CanReach(c, PathEndMode.OnCell, Danger.Some)) return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>In place for the work: held where they stand, lying in the bed, beside the place, out under the sky by it.</summary>
        public static bool InPlace(Pawn on, LocalTargetInfo place, string how)
        {
            if (on == null || !on.Spawned) return false;
            switch (how)
            {
                case "bed": return place.Thing is Building_Bed bed && on.CurrentBed() == bed;
                case "beside": return !place.IsValid || place.HasThing && !place.Thing.Spawned || on.Position.InHorDistOf(place.Cell, 2.5f);
                case "sky": return on.CurJobDef == KeepJob && on.CurJob.targetA.Cell == on.Position && !on.Position.Roofed(on.Map);
                default: return true;
            }
        }

        /// <summary>The colonist brought where the work is done and kept there a while; false when they couldn't be (downed, drafted, in a mental state).</summary>
        public static bool Bring(Pawn on, LocalTargetInfo place, string how, int ticks)
        {
            if (on == null || on.Dead || !on.Spawned || on.Downed || on.Drafted || on.InMentalState || on.jobs == null) return false;
            try
            {
                switch (how)
                {
                    case "bed":
                    {
                        if (!(place.Thing is Building_Bed bed) || !bed.Spawned) return PeoplesG.Hold(on, ticks);
                        if (on.CurrentBed() == bed && on.CurJobDef == JobDefOf.LayDownAwake) return true;
                        var job = JobMaker.MakeJob(JobDefOf.LayDownAwake, bed);
                        job.expiryInterval = Mathf.Max(600, ticks);
                        job.checkOverrideOnExpire = true;
                        on.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                        return on.CurJobDef == JobDefOf.LayDownAwake;
                    }
                    case "beside":
                    {
                        var keep = KeepJob;
                        var cell = Beside(place, on);
                        if (keep == null || !cell.IsValid) return PeoplesG.Hold(on, ticks);
                        if (on.CurJobDef == keep) return true;
                        var job = JobMaker.MakeJob(keep, cell, place);
                        job.count = Mathf.Max(600, ticks);
                        job.locomotionUrgency = LocomotionUrgency.Jog;
                        on.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                        return on.CurJobDef == keep;
                    }
                    case "sky":
                    {
                        var keep = KeepJob;
                        if (keep == null) return PeoplesG.Hold(on, ticks);
                        if (on.CurJobDef == keep) return true;
                        bool there = place.IsValid && (!place.HasThing || place.Thing.Spawned);
                        var cell = SkyCell(on.Map, there ? place.Cell : on.Position, on);
                        if (!cell.IsValid) return PeoplesG.Hold(on, ticks);
                        var job = JobMaker.MakeJob(keep, cell, there ? place : LocalTargetInfo.Invalid);
                        job.count = Mathf.Max(600, ticks);
                        job.locomotionUrgency = LocomotionUrgency.Jog;
                        on.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                        return on.CurJobDef == keep;
                    }
                    default:
                        return on.CurJobDef == JobDefOf.Wait || PeoplesG.Hold(on, ticks);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events: a colonist brought for the work", e);
                return false;
            }
        }

        /// <summary>The order on this colonist under way now, if any.</summary>
        public static Visit OrderOn(Pawn on) => CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(v => v.kind == Kind && !v.done && v.a == on);
    }

    /// <summary>
    /// The worker's side: up to the colonist, wait until they are in place (laid in the bed, beside the place), the work with
    /// its bar and its mark, then what it was for (PeopleWorkOn.Done). TargetA the colonist, TargetB the place (if any),
    /// count the work's ticks. Its progress isn't kept with the game: a job loaded halfway starts again.
    /// </summary>
    public class JobDriver_PeopleWorkOn : JobDriver
    {
        private int waited;

        private Pawn On => job.targetA.Pawn;

        private LocalTargetInfo Place => job.targetB;

        private PeopleWorkJob Ext => PeopleWorkOn.Ext(job.def);

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        /// <summary>To where the work is done: the colonist, or the place itself (its interaction spot, a bench's) when the work is done there.</summary>
        private Toil GoWork(bool atPlace) =>
            atPlace ? Toils_Goto.Goto(TargetIndex.B, Place.HasThing && Place.Thing.def.hasInteractionCell ? PathEndMode.InteractionCell : PathEndMode.Touch)
                : Toils_Goto.Goto(TargetIndex.A, PathEndMode.Touch);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => On == null || On.Dead || !On.Spawned || On.Map != pawn.Map);
            string how = Ext?.place ?? "here";
            var bring = ToilMaker.MakeToil("PEvBring");
            bring.initAction = () =>
            {
                waited = 0;
                PeopleWorkOn.Bring(On, Place, how, job.count + 3000);
            };
            bring.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return bring;
            bool atPlace = Ext?.atPlace == true && Place.IsValid;
            yield return GoWork(atPlace);
            // Until they are in place: the bed lain in, the place reached; brought again if something called them off.
            var wait = ToilMaker.MakeToil("PEvWaitFor");
            wait.tickIntervalAction = delta =>
            {
                var on = On;
                if (on == null) return;
                pawn.rotationTracker.FaceCell(on.Position);
                waited += delta;
                if (PeopleWorkOn.InPlace(on, Place, how))
                {
                    ReadyForNextToil();
                    return;
                }
                if (pawn.IsHashIntervalTick(250, delta)) PeopleWorkOn.Bring(on, Place, how, job.count + 3000);
                if (waited > 2 * GenDate.TicksPerHour) EndJobWith(JobCondition.Incompletable);
            };
            wait.defaultCompleteMode = ToilCompleteMode.Never;
            wait.handlingFacing = true;
            yield return wait;
            yield return GoWork(atPlace);
            var work = ToilMaker.MakeToil("PEvWork");
            work.tickIntervalAction = delta =>
            {
                var on = On;
                if (on == null) return;
                var at = atPlace && Place.IsValid ? Place.Cell : on.Position;
                pawn.rotationTracker.FaceCell(at);
                if (pawn.IsHashIntervalTick(600, delta) && !PeopleWorkOn.InPlace(on, Place, how)) PeopleWorkOn.Bring(on, Place, how, job.count + 1500);
                if (!pawn.IsHashIntervalTick(240, delta)) return;
                var map = pawn.Map;
                switch (Ext?.mark)
                {
                    case "sparks": FleckMaker.ThrowMicroSparks(at.ToVector3Shifted(), map); break;
                    case "hum": FleckMaker.ThrowMetaIcon(on.Position, map, FleckDefOf.Meditating); break;
                    case "storm":
                        FleckMaker.ThrowMetaIcon(on.Position, map, FleckDefOf.Meditating);
                        FleckMaker.ThrowMetaIcon(pawn.Position, map, FleckDefOf.Meditating);
                        break;
                    case "sow": FleckMaker.ThrowDustPuff(Place.IsValid ? Place.Cell : on.Position, map, 0.5f); break;
                    default: FleckMaker.ThrowMetaIcon(on.Position, map, FleckDefOf.HealingCross); break;
                }
            };
            work.WithProgressBarToilDelay(atPlace ? TargetIndex.B : TargetIndex.A);
            work.handlingFacing = true;
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(60, job.count);
            yield return work;
            var done = ToilMaker.MakeToil("PEvDone");
            done.initAction = () => PeopleWorkOn.Done(pawn, On, job.def);
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }

    /// <summary>
    /// A colonist kept at a place for someone's work (the storm-sense at the rod, the seed planted beside them): there, then
    /// still a while facing it (TargetA the cell, TargetB what they face, count how long).
    /// </summary>
    public class JobDriver_PeopleKeep : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var keep = ToilMaker.MakeToil("PEvKeep");
            keep.tickIntervalAction = delta =>
            {
                var t = job.targetB;
                if (t.IsValid) pawn.rotationTracker.FaceCell(t.Cell);
            };
            keep.handlingFacing = true;
            keep.defaultCompleteMode = ToilCompleteMode.Delay;
            keep.defaultDuration = Mathf.Max(60, job.count);
            keep.socialMode = RandomSocialMode.Normal;
            yield return keep;
        }
    }
}
