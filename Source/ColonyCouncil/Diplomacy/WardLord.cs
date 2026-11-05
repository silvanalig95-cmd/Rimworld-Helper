using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Their kin at the gate (Wards.cs): bringing a ward or a hostage of theirs to the colony, or come to take a ward home.
    /// One or two of their people walk in from the edge to the colony's gate and wait there; bringing, the ward joins the
    /// colony at the gate (the hour sees <see cref="arrived"/>) and the kin go home; fetching, the ward walks out to them and
    /// goes home with them. They leave when told (<see cref="done"/>, a flag the hour sets: never a memo sent from inside the
    /// lord's own transition), or once the wait is long; hurt by the colony, they run. A people at war with the colony walks
    /// in as guests of a people at peace with both (<see cref="escort"/>), as their envoy at a parley does. The lord is the
    /// game's own (saved with the map).
    /// </summary>
    public class LordJob_WardParty : LordJob
    {
        /// <summary>The ward or hostage record (WardStay.id).</summary>
        public int ward = -1;
        public int home = HomesMath.None;
        public IntVec3 gate;
        /// <summary>Come to take the ward home (else bringing them).</summary>
        public bool fetch;
        /// <summary>The people at peace with both whose guests they are, for a people at war with the colony; or none.</summary>
        public Faction escort;
        public int arrived = -1;
        public bool harmed, done;

        public const float WaitHours = WardMath.KinWaitHours;

        public LordJob_WardParty()
        {
        }

        public LordJob_WardParty(int ward, int home, IntVec3 gate, bool fetch, Faction escort)
        {
            this.ward = ward;
            this.home = home;
            this.gate = gate;
            this.fetch = fetch;
            this.escort = escort;
        }

        public override bool AddFleeToil => false;

        public override bool ShouldExistWithoutPawns => false;

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var walk = new LordToil_Travel(gate);
            graph.StartingToil = walk;
            var wait = new LordToil_DefendPoint(gate, 3f, 3f);
            graph.AddToil(wait);
            var leave = new LordToil_ExitMap(LocomotionUrgency.Walk, false, true);
            graph.AddToil(leave);
            var flee = new LordToil_ExitMapAndDefendSelf();
            graph.AddToil(flee);

            var there = new Transition(walk, wait);
            there.AddTrigger(new Trigger_Memo("TravelArrived"));
            there.AddPostAction(new TransitionAction_Custom(() =>
            {
                if (arrived < 0) arrived = Find.TickManager.TicksGame;
            }));
            graph.AddTransition(there);

            var goes = new Transition(walk, leave);
            goes.AddSource(wait);
            goes.AddTrigger(new Trigger_TicksPassed(Mathf.RoundToInt((WaitHours + 12f) * GenDate.TicksPerHour)));
            goes.AddTrigger(new Trigger_Custom(s => s.type == TriggerSignalType.Tick && done));
            goes.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(goes);

            var hurt = new Transition(walk, flee);
            hurt.AddSources(wait, leave);
            hurt.AddTrigger(new Trigger_PawnHarmed(1f, true, Faction.OfPlayer));
            hurt.AddPostAction(new TransitionAction_Custom(() => harmed = true));
            hurt.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(hurt);

            var war = new Transition(walk, flee);
            war.AddSources(wait, leave);
            war.AddTrigger(new Trigger_BecamePlayerEnemy());
            war.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(war);
            return graph;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref ward, "ward", -1);
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref gate, "gate");
            Scribe_Values.Look(ref fetch, "fetch");
            Scribe_References.Look(ref escort, "escort");
            Scribe_Values.Look(ref arrived, "arrived", -1);
            Scribe_Values.Look(ref harmed, "harmed");
            Scribe_Values.Look(ref done, "done");
        }
    }

    /// <summary>
    /// The day's lesson, for the ward (the learner): to their guardian (A), who comes to stand with them, and for an hour
    /// they work at it together, a word now and then; the ward learns the skill (the count: its place among the skills) at
    /// the guardian's pace (WardMath.LessonXpPerTick), the game's learning factors and passions on top.
    /// </summary>
    public class JobDriver_WardLesson : JobDriver
    {
        private int taught;
        private bool called, begun;

        private Pawn Guardian => job.targetA.Thing as Pawn;

        private SkillDef Skill
        {
            get
            {
                var all = DefDatabase<SkillDef>.AllDefsListForReading;
                return job.count >= 0 && job.count < all.Count ? all[job.count] : null;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() =>
            Guardian != null && Skill != null ? WardWords.P("CC_Ward_Report_Lesson", Skill.label.Named("SKILL"), Guardian.Named("OTHER")) : base.GetReport();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref taught, "taught");
            Scribe_Values.Look(ref called, "called");
            Scribe_Values.Look(ref begun, "begun");
        }

        private bool Teaching()
        {
            var g = Guardian;
            return g != null && g.Spawned && g.CurJobDef == WardDefOf.Rimfolk_WardTeach && g.CurJob.targetA.Thing == pawn;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Guardian == null || Guardian.Dead || !Guardian.Spawned || Guardian.Map != pawn.Map || Skill == null);
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            this.FailOn(() => called && !Teaching());
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var call = ToilMaker.MakeToil("WardLessonCall");
            call.initAction = () =>
            {
                var g = Guardian;
                var map = pawn.Map;
                var beside = IntVec3.Invalid;
                foreach (var cell in GenAdj.CellsAdjacent8Way(pawn))
                    if (cell.InBounds(map) && cell.Standable(map) && (cell.GetFirstPawn(map) == null || cell == g.Position) && g.CanReach(cell, PathEndMode.OnCell, Danger.Some))
                    {
                        beside = cell;
                        break;
                    }
                if (!beside.IsValid || g.Drafted || g.InMentalState || g.Downed)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                // together: the ward asked their guardian, standing beside them; the guardian comes to stand with them.
                g.jobs.StartJob(JobMaker.MakeJob(WardDefOf.Rimfolk_WardTeach, pawn, beside), JobCondition.InterruptForced, null, false, true);
                called = true;
                if (!Teaching()) EndJobWith(JobCondition.Incompletable);
            };
            call.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return call;
            var wait = ToilMaker.MakeToil("WardLessonWait");
            wait.defaultCompleteMode = ToilCompleteMode.Never;
            wait.handlingFacing = true;
            int waited = 0;
            wait.initAction = () => waited = 0;
            wait.tickIntervalAction = delta =>
            {
                var g = Guardian;
                waited += delta;
                if (g != null && g.Position.InHorDistOf(pawn.Position, 2.5f) && !g.pather.Moving) ReadyForNextToil();
                else if (waited > 2000) EndJobWith(JobCondition.Incompletable);
            };
            yield return wait;
            var lesson = ToilMaker.MakeToil("WardLesson");
            lesson.defaultCompleteMode = ToilCompleteMode.Delay;
            lesson.defaultDuration = WardMath.LessonTicks;
            lesson.handlingFacing = true;
            lesson.socialMode = RandomSocialMode.Off;
            lesson.initAction = () =>
            {
                begun = true;
                Wards.LessonBegun(pawn);
                Outsiders.Say(Guardian, "wardlesson", pawn);
            };
            lesson.tickIntervalAction = delta =>
            {
                var g = Guardian;
                var skill = Skill;
                if (g == null || skill == null) return;
                pawn.rotationTracker.FaceTarget(g);
                int gl = g.skills?.GetSkill(skill)?.Level ?? 0;
                int wl = pawn.skills?.GetSkill(skill)?.Level ?? 0;
                pawn.skills?.Learn(skill, WardMath.LessonXpPerTick(gl, wl) * delta);
                int before = taught;
                taught += delta;
                if (before / 800 != taught / 800)
                {
                    bool ask = taught / 800 % 2 == 1;
                    Outsiders.Say(ask ? pawn : g, ask ? "wardlearn" : "wardlesson", ask ? g : pawn);
                }
            };
            lesson.WithProgressBarToilDelay(TargetIndex.A);
            lesson.activeSkill = () => Skill;
            yield return lesson;
        }
    }

    /// <summary>The guardian teaching: to the place beside the ward (B), then standing with them (A), facing them, until the lesson ends.</summary>
    public class JobDriver_WardTeach : JobDriver
    {
        private Pawn Ward => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() => Ward != null ? WardWords.P("CC_Ward_Report_Teach", Ward.Named("OTHER")) : base.GetReport();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Ward.CurJobDef != WardDefOf.Rimfolk_WardLesson || Ward.CurJob.targetA.Thing != pawn);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            var stand = ToilMaker.MakeToil("WardTeach");
            stand.defaultCompleteMode = ToilCompleteMode.Delay;
            stand.defaultDuration = WardMath.LessonTicks + 1500;
            stand.handlingFacing = true;
            stand.socialMode = RandomSocialMode.Off;
            stand.tickIntervalAction = delta =>
            {
                var w = Ward;
                if (w != null) pawn.rotationTracker.FaceTarget(w);
            };
            yield return stand;
        }
    }

    /// <summary>The ward walks out to their kin at the gate (A) and, face to face, goes home with them (Wards.MetKin).</summary>
    public class JobDriver_WardToKin : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() => WardWords.P("CC_Ward_Report_ToKin");

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var meet = ToilMaker.MakeToil("WardMeetKin");
            meet.initAction = () => Wards.MetKin(pawn, job.targetA.Thing as Pawn);
            meet.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return meet;
        }
    }

    /// <summary>A ward or hostage of theirs walks out to the edge of the colony's land (A) and goes home from there (Wards.AtEdge).</summary>
    public class JobDriver_WardLeave : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() => WardWords.P("CC_Ward_Report_Leave");

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var go = ToilMaker.MakeToil("WardLeave");
            go.initAction = () => Wards.AtEdge(pawn);
            go.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return go;
        }
    }

    /// <summary>The colony's own ward or hostage walks to the map's edge (A) and sets out for their town as a caravan of their own (WardsAbroad.AtEdge).</summary>
    public class JobDriver_WardSetOut : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() => WardWords.P("CC_Ward_Report_SetOut");

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var go = ToilMaker.MakeToil("WardSetOut");
            go.initAction = () => WardsAbroad.AtEdge(pawn);
            go.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return go;
        }
    }

    /// <summary>A people at peace offers a youth of theirs to live at the colony for a year: accept, decline, or leave it a while.</summary>
    public class ChoiceLetter_WardOffer : ChoiceLetter
    {
        public Faction people;
        public int home = HomesMath.None;
        public int notable = -1;
        public string skill;

        private bool Valid(out Map map, out Notable n)
        {
            map = Homes.MapOf(home) ?? Homes.Seat;
            n = NotableBook.Instance?.Get(notable);
            return people != null && !people.defeated && !Diplomacy.Hostile(people) && map != null && Wards.TheirsAt(map) == null && n != null && n.Alive;
        }

        public bool Waiting => !ArchivedOnly && Valid(out _, out _);

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly || !Valid(out _, out _))
                {
                    yield return Option_Close;
                    yield break;
                }
                yield return new DiaOption(WardWords.P("CC_Ward_Opt_Accept"))
                {
                    action = () =>
                    {
                        Find.LetterStack.RemoveLetter(this);
                        if (!Valid(out var map, out var n)) return;
                        var w = Wards.Agree(map, people, n, skill, false, WardMath.ComeDays(Rand.Value));
                        // apart: the colony's answer to their letter; the ward comes later.
                        if (w != null)
                            Tell.Message(NewsKind.World, Importance.Notable, map, WardWords.P("CC_Ward_Msg_Accepted", w.pawn.Named("WARD"), WardWords.People(people).Named("PEOPLE")),
                                LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
                    },
                    resolveTree = true
                };
                yield return new DiaOption(WardWords.P("CC_Ward_Opt_Decline"))
                {
                    action = () => Find.LetterStack.RemoveLetter(this),
                    resolveTree = true
                };
                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref notable, "notable", -1);
            Scribe_Values.Look(ref skill, "skill");
        }
    }

    /// <summary>
    /// Their side broke the terms their hostage was given for: the hostage is the colony's to decide about. Send them home
    /// anyway (the deed "forgave"), keep them (the warden arrests them by the game's own arrest, and ransom applies), or ask
    /// them to stay (only if they would join). Left unanswered, they slip away home after a few days.
    /// </summary>
    public class ChoiceLetter_HostageForfeit : ChoiceLetter
    {
        public int ward = -1;

        private WardStay Record(out Map map)
        {
            var w = Wards.Get(ward, out var h);
            map = h?.Map;
            return w != null && w.stage == WardStage.Forfeit && map != null && w.pawn != null && !w.pawn.Dead ? w : null;
        }

        public bool Waiting => !ArchivedOnly && Record(out _) != null;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                var w = Record(out var map);
                if (ArchivedOnly || w == null)
                {
                    yield return Option_Close;
                    yield break;
                }
                yield return new DiaOption(WardWords.P("CC_Hostage_Opt_Home"))
                {
                    action = () =>
                    {
                        Find.LetterStack.RemoveLetter(this);
                        var r = Record(out var m);
                        if (r != null) Hostages.SendHome(r, m);
                    },
                    resolveTree = true
                };
                string arrestWhy = Hostages.ArrestWhyNot(w, map);
                var arrest = new DiaOption(WardWords.P("CC_Hostage_Opt_Arrest"))
                {
                    action = () =>
                    {
                        Find.LetterStack.RemoveLetter(this);
                        var r = Record(out var m);
                        if (r != null) Hostages.Arrest(r, m);
                    },
                    resolveTree = true
                };
                if (arrestWhy != null) arrest.Disable(arrestWhy);
                yield return arrest;
                string stayWhy = Hostages.StayWhyNot(w);
                var stay = new DiaOption(WardWords.P("CC_Hostage_Opt_Stay"))
                {
                    action = () =>
                    {
                        Find.LetterStack.RemoveLetter(this);
                        var r = Record(out var m);
                        if (r != null) Hostages.TakeIn(r, m);
                    },
                    resolveTree = true
                };
                if (stayWhy != null) stay.Disable(stayWhy);
                yield return stay;
                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ward, "ward", -1);
        }
    }

    /// <summary>
    /// A ward or hostage of theirs killed (the deeper game hooks): whether it was at a colonist's hand, for the grief their
    /// people feel (a feud). Read before the death, while the killer is known.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_WardKilled
    {
        public static void Prefix(Pawn __instance, DamageInfo? dinfo)
        {
            if (!RimfolkPatches.On || __instance == null || __instance.Dead || !__instance.IsColonist) return;
            try
            {
                Wards.Killed(__instance, dinfo);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wards: a ward killed", e);
            }
        }
    }

    [DefOf]
    public static class WardDefOf
    {
        public static LetterDef Rimfolk_WardOffer;
        public static LetterDef Rimfolk_HostageForfeit;
        public static JobDef Rimfolk_WardLesson;
        public static JobDef Rimfolk_WardTeach;
        public static JobDef Rimfolk_WardToKin;
        public static JobDef Rimfolk_WardLeave;
        public static JobDef Rimfolk_WardSetOut;

        static WardDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(WardDefOf));
        }
    }
}
