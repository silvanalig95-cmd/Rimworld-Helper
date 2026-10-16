using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// One person on a walk together, a vigil or a search: where they're to be now comes from the plan (PlayGroups.Where),
    /// so the walk's stages move everyone on together, a vigil holds them at their place round the grave or the bed, and
    /// a searcher goes from place to place on their own way. Ends at once at any danger, when they're drafted or downed,
    /// or when hunger, tiredness or a sickbed calls them; the plan hears it by looking.
    /// </summary>
    public class JobDriver_PlayOutGroup : JobDriver
    {
        private bool spoke;

        private PlayOut Play => PlayOutTracker.Instance?.GroupFor(pawn);

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport() => PlayWords.Report(Play, pawn) ?? base.GetReport();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            this.FailOn(() => !PlayOuts.CanJoin(pawn) || PlayOuts.NeedsPress(pawn));
            var toil = ToilMaker.MakeToil("PlayOutGroup");
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.handlingFacing = true;
            toil.socialMode = job.def == PlayOutDefOf.Council_PlayOutVigil ? RandomSocialMode.Quiet : RandomSocialMode.Normal;
            toil.tickIntervalAction = Tick;
            yield return toil;
        }

        private void Tick(int delta)
        {
            PlayOut p;
            try
            {
                p = Play;
                if (p == null || p.state != PlayState.Running)
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                var h = p.Hand(pawn);
                if (h == null || h.left || !PlayGroups.Where(p, pawn, h, out var cell, out var face))
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                if (pawn.Position != cell)
                {
                    if (!pawn.pather.Moving || pawn.pather.Destination.Cell != cell)
                    {
                        if (!pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some))
                        {
                            PlayGroups.CantReach(p, pawn, h);
                            return;
                        }
                        pawn.pather.StartPath(cell, PathEndMode.OnCell);
                    }
                    return;
                }
                if (face.IsValid) pawn.rotationTracker.FaceTarget(face);
                PlayGroups.Arrived(p, pawn, h, delta);
                if (!spoke && Rand.Chance(0.004f * delta / 15f))
                {
                    spoke = true;
                    PlayWords.During(p, pawn);
                }
                if (pawn.needs?.joy != null && job.def.joyKind != null) JoyUtility.JoyTickCheckEnd(pawn, delta, JoyTickFullJoyAction.None);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("play out group job", e);
                EndJobWith(JobCondition.Errored);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref spoke, "spoke");
        }
    }

    /// <summary>
    /// An errand: the carrier takes a stack from the stores (as much as they can carry), walks it out to the visitor or
    /// to the gate, and hands it over. Danger, drafting or need ends it, and whatever they were carrying is set down
    /// again, still the colony's.
    /// </summary>
    public class JobDriver_PlayOutCarry : JobDriver
    {
        private bool handed;

        private PlayOut Play => PlayOutTracker.Instance?.GroupFor(pawn);

        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);

        public override string GetReport()
        {
            var p = Play;
            if (p == null) return base.GetReport();
            string what = PlayCarry.Label(p.what, p.thing);
            return job.targetB.Thing is Pawn x ? "CC_Play_Report_CarryTo".Translate(what, x.Named("OTHER")).Resolve() : "CC_Play_Report_Carry".Translate(what).Resolve();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            this.FailOn(() => !PlayOuts.CanJoin(pawn));
            AddFinishAction(cond =>
            {
                try
                {
                    // Not handed over: what they carry stays the colony's, set down where they stand.
                    if (!handed && pawn.carryTracker?.CarriedThing != null && pawn.Spawned)
                        pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("errand end", e);
                }
            });
            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false, true);
            if (job.targetB.HasThing)
            {
                this.FailOn(() => job.targetB.Thing is Pawn x && (x.Dead || !x.Spawned || x.Map != pawn.Map));
                yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            }
            else yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            var hand = ToilMaker.MakeToil("PlayOutHandOver");
            hand.initAction = () =>
            {
                try
                {
                    var carried = pawn.carryTracker?.CarriedThing;
                    if (carried == null) return;
                    handed = PlayGroups.HandOver(Play, pawn, carried, job.targetB.Thing as Pawn);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("errand hand over", e);
                }
            };
            hand.defaultCompleteMode = ToilCompleteMode.Delay;
            hand.defaultDuration = 90;
            hand.handlingFacing = true;
            hand.tickIntervalAction = _ =>
            {
                if (job.targetB.HasThing) pawn.rotationTracker.FaceTarget(job.targetB);
            };
            yield return hand;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref handed, "handed");
        }
    }
}
