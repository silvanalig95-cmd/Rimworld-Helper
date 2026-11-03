using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The jobs of talking with people from outside the colony (Outsiders/Outsiders.cs): the colonist's walk over and
    // talk, the outsider stopping to listen, a gift fetched and handed over, a lesson, a bout, those who watch a duel,
    // and a favour brought on the next visit. Every one ends at danger.

    /// <summary>
    /// The colonist walks over to the outsider (A), who stops and faces them; they talk a while, a line now and then
    /// over each head; then what comes of it (Outsiders.Talked), and a moment for the last words to be read. The count
    /// is the thing talked about (<see cref="OutsiderAct"/>) and, a hundred times over, its terms.
    /// </summary>
    public class JobDriver_OutsiderTalk : JobDriver
    {
        private bool met, ending;
        private int said;

        private Pawn Other => job.targetA.Thing as Pawn;

        private OutsiderAct Act => (OutsiderAct)(job.count % 100);

        private int Extra => job.count / 100;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() =>
            Other != null ? (met ? "CC_Out_Report_Talking" : "CC_Out_Report_Going").Translate(Other.Named("OTHER")).Resolve() : base.GetReport();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref met, "met");
            Scribe_Values.Look(ref ending, "ending");
            Scribe_Values.Look(ref said, "said");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            this.FailOn(() => !ending && !Outsiders.IsOutsider(Other));
            this.FailOn(() => met && !ending && !Outsiders.Holding(Other, pawn));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var meet = ToilMaker.MakeToil("OutsiderMeet");
            meet.initAction = () =>
            {
                var x = Other;
                string why = Outsiders.Unwilling(x, pawn, Act);
                if (why != null)
                {
                    Outsiders.Say(x, "notnow", pawn);
                    // together: the colonist walked over and was turned away face to face.
                    Messages.Message("CC_Out_Msg_NotNow".Translate(x.Named("OTHER"), why.Named("WHY")).Resolve(), x, MessageTypeDefOf.RejectInput, false);
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                if (!Outsiders.Hold(x, pawn, OutsiderMath.TalkTicks(Act) + 400))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                met = true;
                pawn.rotationTracker.FaceTarget(x);
                Outsiders.Say(pawn, Outsiders.Opening(Act), x);
            };
            meet.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return meet;
            var talk = ToilMaker.MakeToil("OutsiderTalk");
            talk.defaultCompleteMode = ToilCompleteMode.Delay;
            talk.defaultDuration = OutsiderMath.TalkTicks(Act);
            talk.handlingFacing = true;
            talk.socialMode = RandomSocialMode.Off;
            talk.tickIntervalAction = delta =>
            {
                var x = Other;
                if (x == null) return;
                pawn.rotationTracker.FaceTarget(x);
                int total = OutsiderMath.TalkTicks(Act);
                int done = total - ticksLeftThisToil;
                if (said < 2 && done > (said + 1) * total / 3)
                {
                    said++;
                    if (said % 2 == 1) Outsiders.Say(x, Outsiders.During(Act), pawn);
                    else Outsiders.Say(pawn, Outsiders.During(Act), x);
                }
            };
            yield return talk;
            var end = ToilMaker.MakeToil("OutsiderTalkEnd");
            end.initAction = () =>
            {
                ending = true;
                Job next = null;
                try
                {
                    // together: the talk job brought the two face to face.
                    next = Outsiders.Talked(pawn, Other, Act, Extra, job.thingDefToCarry);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("outsider talk end", e);
                }
                if (next == null) return;
                pawn.jobs.jobQueue.EnqueueFirst(next, JobTag.Misc);
                EndJobWith(JobCondition.Succeeded);
            };
            end.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return end;
            var linger = ToilMaker.MakeToil("OutsiderTalkLinger");
            linger.defaultCompleteMode = ToilCompleteMode.Delay;
            linger.defaultDuration = 150;
            linger.handlingFacing = true;
            linger.tickIntervalAction = delta =>
            {
                var x = Other;
                if (x != null && x.Spawned) pawn.rotationTracker.FaceTarget(x);
            };
            yield return linger;
        }
    }

    /// <summary>The outsider stops for the colonist (A): stands where they are and faces them while they talk, until the colonist's job with them ends.</summary>
    public class JobDriver_OutsiderListen : JobDriver
    {
        private Pawn Other => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() => Other != null ? "CC_Out_Report_Listen".Translate(Other.Named("OTHER")).Resolve() : base.GetReport();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !Outsiders.WithMe(Other, pawn));
            var stand = ToilMaker.MakeToil("OutsiderListen");
            stand.defaultCompleteMode = ToilCompleteMode.Delay;
            stand.defaultDuration = Mathf.Max(300, job.count);
            stand.handlingFacing = true;
            stand.socialMode = RandomSocialMode.Off;
            stand.initAction = () => pawn.pather?.StopDead();
            stand.tickIntervalAction = delta =>
            {
                var c = Other;
                if (c != null && c.Spawned) pawn.rotationTracker.FaceTarget(c);
            };
            yield return stand;
        }
    }

    /// <summary>
    /// A gift: the colonist goes to the thing in the stores (B), takes up the share picked (the count), carries it to
    /// the outsider (A), who stops for them, and hands it over with a word. Not handed over, it's set down where they stand.
    /// </summary>
    public class JobDriver_OutsiderGift : JobDriver
    {
        private bool handed, met;

        private Pawn Other => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed);

        public override string GetReport() => Other != null ? "CC_Out_Report_Gift".Translate(Other.Named("OTHER")).Resolve() : base.GetReport();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref handed, "handed");
            Scribe_Values.Look(ref met, "met");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            this.FailOn(() => !handed && !Outsiders.IsOutsider(Other));
            this.FailOn(() => met && !handed && !Outsiders.Holding(Other, pawn));
            AddFinishAction(cond =>
            {
                try
                {
                    // Not handed over: it stays the colony's, set down where they stand.
                    if (!handed && pawn.Spawned && pawn.carryTracker?.CarriedThing != null) pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("outsider gift set down", e);
                }
            });
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, true, false);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var meet = ToilMaker.MakeToil("OutsiderGiftMeet");
            meet.initAction = () =>
            {
                var x = Other;
                string why = Outsiders.Unwilling(x, pawn, OutsiderAct.Gift);
                if (why != null || !Outsiders.Hold(x, pawn, OutsiderMath.TalkTicks(OutsiderAct.Gift) + 300))
                {
                    Outsiders.Say(x, "notnow", pawn);
                    if (why != null)
                        // together: the colonist walked over and was turned away face to face.
                        Messages.Message("CC_Out_Msg_NotNow".Translate(x.Named("OTHER"), why.Named("WHY")).Resolve(), x, MessageTypeDefOf.RejectInput, false);
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                met = true;
                Outsiders.Say(pawn, "gift", x);
            };
            meet.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return meet;
            var show = ToilMaker.MakeToil("OutsiderGiftShow");
            show.defaultCompleteMode = ToilCompleteMode.Delay;
            show.defaultDuration = OutsiderMath.TalkTicks(OutsiderAct.Gift);
            show.handlingFacing = true;
            show.socialMode = RandomSocialMode.Off;
            show.tickIntervalAction = delta =>
            {
                var x = Other;
                if (x != null) pawn.rotationTracker.FaceTarget(x);
            };
            yield return show;
            var hand = ToilMaker.MakeToil("OutsiderGiftHand");
            hand.initAction = () =>
            {
                var carried = pawn.carryTracker?.CarriedThing;
                if (carried == null) return;
                try
                {
                    // together: handed over face to face.
                    handed = Outsiders.GiftHanded(pawn, Other, carried);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("outsider gift handed", e);
                }
            };
            hand.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return hand;
            yield return Toils_General.Wait(120, TargetIndex.A);
        }
    }

    /// <summary>
    /// A lesson: the colonist (the learner) goes to where it's held (B: a bench of the skill's work, or a spot), the
    /// outsider (A) comes to stand beside them, and for an hour they work at it together, a word now and then; the
    /// colonist learns the skill (the count: its place among the skills) at the teacher's pace.
    /// </summary>
    public class JobDriver_OutsiderLesson : JobDriver
    {
        private int taught;
        private bool called, begun;

        private Pawn Teacher => job.targetA.Thing as Pawn;

        private SkillDef Skill
        {
            get
            {
                var all = DefDatabase<SkillDef>.AllDefsListForReading;
                return job.count >= 0 && job.count < all.Count ? all[job.count] : null;
            }
        }

        private IntVec3 Spot => job.targetB.Thing is Building b && b.def.hasInteractionCell ? b.InteractionCell : job.targetB.Cell;

        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            !(job.targetB.Thing is Building) || pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed);

        public override string GetReport() =>
            Teacher != null && Skill != null ? "CC_Out_Report_Lesson".Translate(Skill.label.Named("SKILL"), Teacher.Named("OTHER")).Resolve() : base.GetReport();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref taught, "taught");
            Scribe_Values.Look(ref called, "called");
            Scribe_Values.Look(ref begun, "begun");
        }

        private bool Teaching()
        {
            var t = Teacher;
            return t != null && t.Spawned && t.CurJobDef == OutsiderDefOf.Council_OutsiderTeach && t.CurJob.targetA.Thing == pawn;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Teacher == null || Teacher.Dead || !Teacher.Spawned || Teacher.Map != pawn.Map || Skill == null);
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            this.FailOn(() => called && !Teaching());
            AddFinishAction(cond =>
            {
                if (!begun) return;
                try
                {
                    // together: the lesson was side by side; this is its end, however it came.
                    Outsiders.LessonOver(pawn, Teacher, Skill, taught);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("outsider lesson over", e);
                }
            });
            var go = ToilMaker.MakeToil("OutsiderLessonGo");
            go.initAction = () => pawn.pather.StartPath(Spot, PathEndMode.OnCell);
            go.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            yield return go;
            var call = ToilMaker.MakeToil("OutsiderLessonCall");
            call.initAction = () =>
            {
                var t = Teacher;
                var map = pawn.Map;
                var beside = IntVec3.Invalid;
                foreach (var cell in GenAdj.CellsAdjacent8Way(new TargetInfo(Spot, map)))
                    if (cell.InBounds(map) && cell.Standable(map) && cell.GetFirstPawn(map) == null && t.CanReach(cell, PathEndMode.OnCell, Danger.Some))
                    {
                        beside = cell;
                        break;
                    }
                if (!beside.IsValid || Outsiders.Unwilling(t, pawn, OutsiderAct.Teach) != null)
                {
                    Outsiders.Say(t, "notnow", pawn);
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                var job2 = JobMaker.MakeJob(OutsiderDefOf.Council_OutsiderTeach, pawn, beside);
                t.jobs.StartJob(job2, JobCondition.InterruptForced, null, false, true);
                called = true;
                if (!Teaching()) EndJobWith(JobCondition.Incompletable);
            };
            call.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return call;
            var wait = ToilMaker.MakeToil("OutsiderLessonWait");
            wait.defaultCompleteMode = ToilCompleteMode.Never;
            wait.handlingFacing = true;
            int waited = 0;
            wait.initAction = () => waited = 0;
            wait.tickIntervalAction = delta =>
            {
                var t = Teacher;
                waited += delta;
                if (t != null && t.Position.InHorDistOf(pawn.Position, 2.5f) && !t.pather.Moving) ReadyForNextToil();
                else if (waited > 2000) EndJobWith(JobCondition.Incompletable);
            };
            yield return wait;
            var lesson = ToilMaker.MakeToil("OutsiderLesson");
            lesson.defaultCompleteMode = ToilCompleteMode.Delay;
            lesson.defaultDuration = OutsiderMath.TeachTicks;
            lesson.handlingFacing = true;
            lesson.socialMode = RandomSocialMode.Off;
            lesson.initAction = () =>
            {
                begun = true;
                Outsiders.Say(Teacher, "lesson", pawn);
            };
            lesson.tickIntervalAction = delta =>
            {
                var t = Teacher;
                var skill = Skill;
                if (t == null || skill == null) return;
                if (job.targetB.Thing is Building b) pawn.rotationTracker.FaceTarget(b);
                else pawn.rotationTracker.FaceTarget(t);
                int tl = t.skills?.GetSkill(skill)?.Level ?? 0;
                int ll = pawn.skills?.GetSkill(skill)?.Level ?? 0;
                pawn.skills?.Learn(skill, OutsiderMath.TeachXpPerTick(tl, ll) * delta);
                int before = taught;
                taught += delta;
                if (before / 800 != taught / 800) Outsiders.Say(taught / 800 % 2 == 1 ? pawn : t, taught / 800 % 2 == 1 ? "lessonask" : "lesson", taught / 800 % 2 == 1 ? t : pawn);
            };
            lesson.WithProgressBarToilDelay(TargetIndex.A);
            lesson.activeSkill = () => Skill;
            yield return lesson;
        }
    }

    /// <summary>
    /// A meal or a drink shared with an outsider, for each of the two: their portion from the stores (A), to their chair
    /// (B) at the table the two share, and eaten the game's own way (its ingest toils, as friends drinking together do).
    /// </summary>
    public class JobDriver_OutsiderMeal : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(job.targetA, job, 10, 1, null, errorOnFailed)) return false;
            var chair = job.targetB.Cell.IsValid && job.targetB.Cell.InBounds(pawn.Map) ? job.targetB.Cell.GetEdifice(pawn.Map) : null;
            return chair == null || pawn.Reserve(chair, job, 1, -1, null, errorOnFailed);
        }

        public override string GetReport()
        {
            var food = job.targetA.Thing;
            return food != null ? "CC_Out_Report_Meal".Translate(food.LabelNoCount.Named("FOOD")).Resolve() : base.GetReport();
        }

        public override bool ModifyCarriedThingDrawPos(ref Vector3 drawPos, ref bool flip)
        {
            var cell = job.GetTarget(TargetIndex.C).Cell;
            return cell.IsValid && JobDriver_Ingest.ModifyCarriedThingDrawPosWorker(ref drawPos, ref flip, cell, pawn);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            this.FailOnDestroyedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            yield return Toils_Ingest.PickupIngestible(TargetIndex.A, pawn);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            yield return Toils_Ingest.FindAdjacentEatSurface(TargetIndex.C, TargetIndex.A);
            yield return Toils_Ingest.ChewIngestible(pawn, 1f, TargetIndex.A, TargetIndex.C);
            yield return Toils_Ingest.FinalizeIngest(pawn, TargetIndex.A);
        }
    }

    /// <summary>The outsider teaching: to the place beside the learner (B), then standing with them (A), facing them, until the lesson ends.</summary>
    public class JobDriver_OutsiderTeach : JobDriver
    {
        private Pawn Learner => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() => Learner != null ? "CC_Out_Report_Teach".Translate(Learner.Named("OTHER")).Resolve() : base.GetReport();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Learner.CurJobDef != OutsiderDefOf.Council_OutsiderLesson || Learner.CurJob.targetA.Thing != pawn);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            var stand = ToilMaker.MakeToil("OutsiderTeach");
            stand.defaultCompleteMode = ToilCompleteMode.Delay;
            stand.defaultDuration = OutsiderMath.TeachTicks + 1500;
            stand.handlingFacing = true;
            stand.socialMode = RandomSocialMode.Off;
            stand.tickIntervalAction = delta =>
            {
                var l = Learner;
                if (l != null) pawn.rotationTracker.FaceTarget(l);
            };
            yield return stand;
        }
    }

    /// <summary>
    /// A bout, for both fighters: to the ring (B), waiting for the other (A) to stand opposite, a moment to face each
    /// other, then the fight: practice swings, or (a duel) the game's own melee with their own weapons; the colonist's
    /// job is the referee who calls it by its terms. The count is the kind of bout.
    /// </summary>
    public class JobDriver_OutsiderBout : JobDriver
    {
        private int swingIn = 60, refereeIn, waited;

        private Pawn Opponent => job.targetA.Thing as Pawn;

        private OutsiderBout Bout => Outsiders.BoutOf(pawn);

        private BoutKind Kind => (BoutKind)Mathf.Clamp(job.count, 0, 2);

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() =>
            Opponent != null ? (Kind == BoutKind.Duel ? "CC_Out_Report_Duel" : "CC_Out_Report_Spar").Translate(Opponent.Named("OTHER")).Resolve() : base.GetReport();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref swingIn, "swingIn", 60);
            Scribe_Values.Look(ref waited, "waited");
        }

        private bool Referee => Bout != null && Bout.a == pawn;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Bout == null || Bout.b != pawn && Bout.a != pawn);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            var wait = ToilMaker.MakeToil("OutsiderBoutWait");
            wait.defaultCompleteMode = ToilCompleteMode.Never;
            wait.handlingFacing = true;
            wait.tickIntervalAction = delta =>
            {
                var o = Opponent;
                var bout = Bout;
                if (o == null || bout == null) return;
                pawn.rotationTracker.FaceTarget(o);
                waited += delta;
                bool ready = o.Position.InHorDistOf(pawn.Position, 1.5f) && o.CurJobDef == OutsiderDefOf.Council_OutsiderBout && !o.pather.Moving;
                if (ready) ReadyForNextToil();
                else if (bout.over || waited > 2500)
                {
                    Outsiders.CallOff(bout);
                    EndJobWith(JobCondition.Incompletable);
                }
            };
            yield return wait;
            var salute = ToilMaker.MakeToil("OutsiderBoutSalute");
            salute.defaultCompleteMode = ToilCompleteMode.Delay;
            salute.defaultDuration = 150;
            salute.handlingFacing = true;
            salute.initAction = () =>
            {
                if (Referee) Outsiders.Say(pawn, Kind == BoutKind.Duel ? "duelsalute" : "sparbegin", Opponent);
            };
            salute.tickIntervalAction = delta =>
            {
                if (Opponent != null) pawn.rotationTracker.FaceTarget(Opponent);
            };
            yield return salute;
            var fight = ToilMaker.MakeToil("OutsiderBoutFight");
            fight.defaultCompleteMode = ToilCompleteMode.Never;
            fight.handlingFacing = true;
            fight.socialMode = RandomSocialMode.Off;
            fight.initAction = () =>
            {
                var bout = Bout;
                if (bout != null && bout.fightStart < 0) bout.fightStart = Find.TickManager.TicksGame;
            };
            fight.tickIntervalAction = delta =>
            {
                var bout = Bout;
                var o = Opponent;
                if (bout == null || o == null || bout.over)
                {
                    ReadyForNextToil();
                    return;
                }
                if (Referee)
                {
                    refereeIn -= delta;
                    if (refereeIn <= 0)
                    {
                        refereeIn = 30;
                        Outsiders.Referee(bout);
                        if (bout.over)
                        {
                            ReadyForNextToil();
                            return;
                        }
                    }
                }
                if (!pawn.Position.AdjacentTo8WayOrInside(o.Position))
                {
                    if (!pawn.pather.Moving) pawn.pather.StartPath(o, PathEndMode.Touch);
                    return;
                }
                if (pawn.pather.Moving) pawn.pather.StopDead();
                pawn.rotationTracker.FaceTarget(o);
                if (Kind == BoutKind.Spar)
                {
                    swingIn -= delta;
                    if (swingIn > 0) return;
                    swingIn = OutsiderMath.SparSwingTicks + Rand.Range(0, 40);
                    Outsiders.Swing(bout, pawn, o);
                    return;
                }
                // A duel: the game's own melee, each with their own weapon.
                if (!pawn.stances.FullBodyBusy) pawn.meleeVerbs.TryMeleeAttack(o, null, false);
            };
            yield return fight;
            var after = ToilMaker.MakeToil("OutsiderBoutAfter");
            after.defaultCompleteMode = ToilCompleteMode.Delay;
            after.defaultDuration = 150;
            after.handlingFacing = true;
            after.tickIntervalAction = delta =>
            {
                var o = Opponent;
                if (o != null && o.Spawned) pawn.rotationTracker.FaceTarget(o);
            };
            yield return after;
        }
    }

    /// <summary>Someone watching a duel: to a place round the ring (B), facing the fight (A's), until it ends; a word now and then.</summary>
    public class JobDriver_OutsiderWatch : JobDriver
    {
        private int cheer = 300;

        private Pawn Duellist => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() => "CC_Out_Report_Watch".Translate().Resolve();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Outsiders.BoutOf(Duellist) == null);
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            var watch = ToilMaker.MakeToil("OutsiderWatch");
            watch.defaultCompleteMode = ToilCompleteMode.Delay;
            watch.defaultDuration = OutsiderMath.DuelMaxTicks + 3000;
            watch.handlingFacing = true;
            watch.socialMode = RandomSocialMode.Off;
            watch.tickIntervalAction = delta =>
            {
                var bout = Outsiders.BoutOf(Duellist);
                if (bout == null || bout.over)
                {
                    ReadyForNextToil();
                    return;
                }
                pawn.rotationTracker.FaceCell(bout.ring);
                pawn.needs?.joy?.GainJoy(0.00012f * delta, JoyKindDefOf.Social);
                cheer -= delta;
                if (cheer > 0 || bout.fightStart < 0) return;
                cheer = Rand.Range(700, 1400);
                if (Rand.Chance(0.4f)) Outsiders.Say(pawn, "watch", null);
            };
            yield return watch;
        }
    }

    /// <summary>A favour brought on the next visit: one of their people walks over to the colonist who asked (A, or a spot by the gate) and sets it down.</summary>
    public class JobDriver_OutsiderBring : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() => "CC_Out_Report_Bring".Translate().Resolve();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            if (job.targetA.HasThing)
            {
                this.FailOnDespawnedOrNull(TargetIndex.A);
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            }
            else yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            yield return Toils_General.Wait(60, job.targetA.HasThing ? TargetIndex.A : TargetIndex.None);
            var set = ToilMaker.MakeToil("OutsiderBringDown");
            set.initAction = () =>
            {
                try
                {
                    // together: they walked over to the colonist (or the gate) with it.
                    Outsiders.Brought(pawn, job.targetA.Thing as Pawn, job.thingDefToCarry, job.count);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("outsider favour set down", e);
                }
            };
            set.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return set;
        }
    }
}
