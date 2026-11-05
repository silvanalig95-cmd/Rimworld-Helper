using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// A feast's guests of one people as a group (Feasts.cs): they walk in to the gate and wait there, keep about where the
    /// day takes them (a contest, the feast's table, indoors for the night: <see cref="focus"/>, which the feast moves) and
    /// walk out once told. Hurt by the colony, or their people turned on it, they leave at once, defending themselves (a
    /// blow in a bout or a brawl isn't the colony harming them: Outsiders/OutsiderPatches.cs).
    /// </summary>
    public class LordJob_FeastGuests : LordJob
    {
        public int home = HomesMath.None;
        public IntVec3 focus = IntVec3.Invalid;
        public float radius = 6f;

        public const string MemoLeave = "FeastLeave";

        public LordJob_FeastGuests()
        {
        }

        public LordJob_FeastGuests(int home, IntVec3 gate)
        {
            this.home = home;
            focus = gate;
        }

        public override bool AddFleeToil => false;

        public override bool ShouldExistWithoutPawns => false;

        public override bool CanOpenAnyDoor(Pawn p) => true;

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var walk = new LordToil_Travel(focus);
            graph.StartingToil = walk;
            var stay = new LordToil_FeastGuests();
            graph.AddToil(stay);
            var leave = new LordToil_ExitMap(LocomotionUrgency.Walk, false, true);
            graph.AddToil(leave);
            var flee = new LordToil_ExitMapAndDefendSelf();
            graph.AddToil(flee);

            var there = new Transition(walk, stay);
            there.AddTrigger(new Trigger_Memo("TravelArrived"));
            there.AddTrigger(new Trigger_TicksPassed(GenDate.TicksPerHour * 6));
            graph.AddTransition(there);

            var goes = new Transition(walk, leave);
            goes.AddSource(stay);
            goes.AddTrigger(new Trigger_Memo(MemoLeave));
            goes.AddPostAction(new TransitionAction_WakeAll());
            goes.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(goes);

            var hurt = new Transition(walk, flee);
            hurt.AddSources(stay, leave);
            hurt.AddTrigger(new Trigger_PawnHarmed(1f, true, Faction.OfPlayer));
            hurt.AddPostAction(new TransitionAction_Custom(() =>
            {
                try
                {
                    Feasts.Harmed(home);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("feasts: harmed by the colony", e);
                }
            }));
            hurt.AddPostAction(new TransitionAction_WakeAll());
            hurt.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(hurt);

            var war = new Transition(walk, flee);
            war.AddSources(stay, leave);
            war.AddTrigger(new Trigger_BecamePlayerEnemy());
            war.AddPostAction(new TransitionAction_WakeAll());
            war.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(war);
            return graph;
        }

        public override void Notify_PawnLost(Pawn p, PawnLostCondition condition)
        {
            base.Notify_PawnLost(p, condition);
            try
            {
                // Walking off the map at the end is no loss to the feast.
                if (condition != PawnLostCondition.ExitedMap) Feasts.Lost(home, p, condition);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feasts: one of the guests lost", e);
            }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref focus, "focus", IntVec3.Invalid);
            Scribe_Values.Look(ref radius, "radius", 6f);
        }
    }

    /// <summary>
    /// The guests at the colony: about the place the day takes them (Rimfolk_FeastGuest), in danger indoors
    /// (Rimfolk_FeastIndoors); their long needs met (the feast's look sends them to eat and to bed). Every 250 ticks it
    /// gives the feast its look (Feasts.Rare).
    /// </summary>
    public class LordToil_FeastGuests : LordToil
    {
        [Unsaved] private bool indoors;
        [Unsaved] private IntVec3 inside = IntVec3.Invalid;

        private LordJob_FeastGuests Job => lord?.LordJob as LordJob_FeastGuests;

        public override IntVec3 FlagLoc => Job?.focus ?? IntVec3.Invalid;

        public override bool AllowSatisfyLongNeeds => true;

        public override bool AllowRestingInBed => true;

        private bool Danger => Map?.dangerWatcher != null && Map.dangerWatcher.DangerRating == StoryDanger.High;

        public override void UpdateAllDuties()
        {
            var job = Job;
            var focus = job != null && job.focus.IsValid ? job.focus : lord.ownedPawns.FirstOrDefault()?.Position ?? IntVec3.Invalid;
            var stayDuty = FeastDefOf.Rimfolk_FeastGuest ?? DutyDefOf.WanderClose;
            var inDuty = FeastDefOf.Rimfolk_FeastIndoors;
            if (indoors && !inside.IsValid) inside = Visitors.IndoorsCell(Map, lord.ownedPawns.ToList());
            foreach (var p in lord.ownedPawns)
                p.mindState.duty = indoors && inDuty != null
                    ? new PawnDuty(inDuty, inside.IsValid ? inside : focus, 4f)
                    : new PawnDuty(stayDuty, focus, job?.radius ?? 6f);
        }

        public override void LordToilTick()
        {
            if (lord.ticksInToil % 250 != 0) return;
            bool now = Danger;
            if (now != indoors)
            {
                indoors = now;
                if (!now) inside = IntVec3.Invalid;
                UpdateAllDuties();
            }
            var job = Job;
            if (job != null) Feasts.Rare(job.home);
        }
    }

    /// <summary>
    /// A runner in the feast's race: to their place on the line by the gate (target B), wait for the start, run to the
    /// marker (target A) and back as fast as they can; a stumble at the turn costs a moment.
    /// </summary>
    public class JobDriver_FeastRun : JobDriver
    {
        private bool started, stumbled;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() => FeastWords.P(started ? "CC_Feast_Report_Running" : "CC_Feast_Report_Line");

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref started, "started");
            Scribe_Values.Look(ref stumbled, "stumbled");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !FeastContests.Racing(pawn));
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating == StoryDanger.High);
            var toLine = Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            toLine.AddPreInitAction(() => job.locomotionUrgency = LocomotionUrgency.Walk);
            yield return toLine;
            var wait = ToilMaker.MakeToil("FeastRunLine");
            wait.defaultCompleteMode = ToilCompleteMode.Never;
            wait.handlingFacing = true;
            wait.initAction = () => FeastContests.AtLine(pawn);
            wait.tickIntervalAction = delta =>
            {
                pawn.rotationTracker.FaceCell(job.targetA.Cell);
                if (FeastContests.Go(pawn)) ReadyForNextToil();
            };
            yield return wait;
            var go = Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.Touch);
            go.AddPreInitAction(() =>
            {
                started = true;
                job.locomotionUrgency = LocomotionUrgency.Sprint;
            });
            yield return go;
            var turn = ToilMaker.MakeToil("FeastRunTurn");
            turn.initAction = () =>
            {
                stumbled = FeastContests.Stumbles(pawn);
                // together: a cry at the turn, among the runners.
                if (stumbled) Outsiders.Say(pawn, "feastrunstumble", null);
            };
            turn.defaultCompleteMode = ToilCompleteMode.Delay;
            turn.defaultDuration = 1;
            yield return turn;
            var down = ToilMaker.MakeToil("FeastRunStumble");
            down.defaultCompleteMode = ToilCompleteMode.Delay;
            down.initAction = () =>
            {
                if (!stumbled)
                {
                    ReadyForNextToil();
                    return;
                }
                ticksLeftThisToil = FeastMath.StumbleTicks(Rand.Value);
                FleckMaker.ThrowDustPuff(pawn.Position, pawn.Map, 1f);
            };
            down.defaultDuration = 120;
            yield return down;
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.Touch);
            var home = ToilMaker.MakeToil("FeastRunHome");
            home.initAction = () => FeastContests.Finished(pawn);
            home.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return home;
        }
    }

    /// <summary>
    /// A shooter at the feast's match: to their place (target B) facing the target (A), then a few shots, each aimed and
    /// loosed with the weapon's own sound and a puff where it lands, hits by the game's own chance at that range.
    /// </summary>
    public class JobDriver_FeastShoot : JobDriver
    {
        private int shots, hits, next;
        private float chance = -1f;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() => FeastWords.P("CC_Feast_Report_Shooting", (job.targetA.Thing?.LabelShort ?? "?").Named("TARGET"));

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref shots, "shots");
            Scribe_Values.Look(ref hits, "hits");
            Scribe_Values.Look(ref next, "next");
            Scribe_Values.Look(ref chance, "chance", -1f);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !FeastContests.Shooting(pawn) || pawn.equipment?.Primary == null || !pawn.equipment.Primary.def.IsRangedWeapon);
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating == StoryDanger.High);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            var shoot = ToilMaker.MakeToil("FeastShoot");
            shoot.defaultCompleteMode = ToilCompleteMode.Never;
            shoot.handlingFacing = true;
            shoot.initAction = () =>
            {
                next = FeastMath.ShotTicks;
                if (chance < 0f)
                {
                    try
                    {
                        var verb = pawn.equipment.PrimaryEq.PrimaryVerb;
                        chance = Mathf.Clamp(ShotReport.HitReportFor(pawn, verb, job.targetA).TotalEstimatedHitChance, 0.05f, 0.95f);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("feasts: a shot's chance", e);
                        chance = 0.4f;
                    }
                }
            };
            shoot.tickIntervalAction = delta =>
            {
                var target = job.targetA.Thing;
                if (target == null) return;
                pawn.rotationTracker.FaceTarget(target);
                next -= delta;
                if (next > 0) return;
                next = FeastMath.ShotTicks;
                Loose(target);
                if (shots >= FeastMath.Shots)
                {
                    FeastContests.ShotDone(pawn, hits);
                    ReadyForNextToil();
                }
            };
            yield return shoot;
        }

        private void Loose(Thing target)
        {
            shots++;
            bool hit = Rand.Chance(chance);
            if (hit) hits++;
            var map = pawn.Map;
            try
            {
                var props = pawn.equipment?.PrimaryEq?.PrimaryVerb?.verbProps;
                props?.soundCast?.PlayOneShot(new TargetInfo(pawn.Position, map));
                if (props != null && props.muzzleFlashScale > 0.01f) FleckMaker.Static(pawn.DrawPos, map, FleckDefOf.ShotFlash, props.muzzleFlashScale);
                var at = hit ? target.Position : target.Position + GenAdj.AdjacentCells[Rand.Range(0, 8)];
                if (at.InBounds(map))
                {
                    FleckMaker.ThrowDustPuff(at, map, hit ? 0.6f : 0.9f);
                    if (hit) FleckMaker.Static(at, map, FleckDefOf.ShotHit_Dirt);
                }
                pawn.skills?.Learn(SkillDefOf.Shooting, 20f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feasts: a shot", e);
            }
        }
    }

    /// <summary>
    /// The prize carried to the champion: taken from where it lies in the stores (target A), walked to them (B) and handed
    /// over face to face (Feasts.PrizeHanded); not handed over, it is set down where the carrier stands, still the colony's.
    /// </summary>
    public class JobDriver_FeastPrize : JobDriver
    {
        private bool done;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);

        public override string GetReport() =>
            FeastWords.P("CC_Feast_Report_Prize", (job.targetB.Thing?.LabelShort ?? "?").Named("CHAMPION"));

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref done, "done");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFinishAction(cond =>
            {
                try
                {
                    var carried = pawn.carryTracker?.CarriedThing;
                    if (!done && carried != null && pawn.Spawned && pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out var lying) && lying != null)
                        lying.SetForbidden(false, false);
                    if (!done && job.targetA.Thing is Thing t && t.Spawned) t.SetForbidden(false, false);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("feasts: the prize set down", e);
                }
            });
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOn(() => job.targetB.Thing is Pawn x && (x.Dead || !x.Spawned || x.Map != pawn.Map));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false, true);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            var hand = ToilMaker.MakeToil("FeastPrize");
            hand.initAction = () =>
            {
                try
                {
                    // together: the carrier has just walked over to the champion.
                    Feasts.PrizeHanded(pawn, job.targetB.Thing as Pawn);
                    done = true;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("feasts: the prize handed over", e);
                }
            };
            hand.defaultCompleteMode = ToilCompleteMode.Delay;
            hand.defaultDuration = 150;
            hand.handlingFacing = true;
            hand.tickIntervalAction = _ => pawn.rotationTracker.FaceTarget(job.targetB);
            yield return hand;
        }
    }
}
