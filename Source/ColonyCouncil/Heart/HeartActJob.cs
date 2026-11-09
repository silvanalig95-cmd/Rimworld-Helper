using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// One of the heart's small acts (Heart/HeartActs.cs): go there (a chair, a spot outside, a partner's side, the far side
    /// of a door, beside their own bed on a bad afternoon), say the line on arriving, and stay a while facing what it is about, or pace up and down, or dance where
    /// they stand, or let the door slam. Ends at any danger, when its setting goes off, when whom it is with walks off, and
    /// like any job at the player's word or a need that can't wait.
    /// </summary>
    public class JobDriver_HeartAct : JobDriver
    {
        private int stayed;
        private bool arrived;

        private HeartActExtension Ext => job.def.GetModExtension<HeartActExtension>();

        private HeartActKind Kind => Ext?.kind ?? HeartActKind.Sit;

        /// <summary>Whom it is with: a partner, a table mate, someone indoors.</summary>
        private Pawn Other => job.targetB.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn.Map == null) return false;
            if (job.targetA.IsValid && !job.targetA.HasThing && Kind != HeartActKind.Door && Kind != HeartActKind.Pace)
            {
                if (Ext?.seated == true && !pawn.ReserveSittableOrSpot(job.targetA.Cell, job, errorOnFailed)) return false;
                pawn.Map.pawnDestinationReservationManager.Reserve(pawn, job, job.targetA.Cell);
            }
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var ext = Ext;
            var kind = Kind;
            // Part of their time off (a day off, a bad afternoon): it runs while that does, whatever the acts' own settings.
            bool timeOff = HeartActs.NoteOf(job)?.timeOff ?? false;
            this.FailOn(() => !(timeOff ? HeartTimeOff.Running(pawn) : StillOn(kind)) || pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            if (Other != null) this.FailOn(() => Other.Dead || !Other.Spawned || Other.Map != pawn.Map);
            if (kind == HeartActKind.HomeFire) this.FailOn(() => job.targetB.Thing == null || !job.targetB.Thing.Spawned);
            switch (kind)
            {
                case HeartActKind.Linger:
                case HeartActKind.Dance:
                    break;
                case HeartActKind.Pace:
                    // Their line as they start, then up and down the room: there and back twice, a beat at each end.
                    yield return Toils_General.Do(Arrive);
                    for (int i = 0; i < 2; i++)
                    {
                        yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
                        yield return Toils_General.Wait(60);
                        yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
                        yield return Toils_General.Wait(60);
                    }
                    break;
                case HeartActKind.Door:
                    // Through the door to the far side; it closes behind them, with a slam.
                    yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
                    yield return Toils_Goto.GotoCell(TargetIndex.C, PathEndMode.OnCell);
                    break;
                default:
                    yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
                    break;
            }
            var stay = ToilMaker.MakeToil("HeartAct");
            stay.initAction = Arrive;
            stay.tickIntervalAction = Stay;
            stay.defaultCompleteMode = ToilCompleteMode.Delay;
            stay.defaultDuration = Mathf.Max(kind == HeartActKind.Door ? 60 : kind == HeartActKind.Pace ? 120 : 300, job.count);
            stay.socialMode = ext != null && ext.social ? RandomSocialMode.SuperActive : RandomSocialMode.Off;
            stay.handlingFacing = true;
            yield return stay;
        }

        /// <summary>Whether the act's setting is still on.</summary>
        private static bool StillOn(HeartActKind kind)
        {
            switch (kind)
            {
                case HeartActKind.LostBed:
                case HeartActKind.Indoors:
                case HeartActKind.Door:
                case HeartActKind.SitWith:
                case HeartActKind.HomeFire: return HeartActs.FeelingsOn;
                // Sit serves grief too when there is no grave or bed: either setting keeps it.
                case HeartActKind.Sit: return HeartActs.BandsOn || HeartActs.FeelingsOn;
                // A bad afternoon's own.
                case HeartActKind.Cry: return HeartTimeOff.BadDaysOn;
                default: return HeartActs.BandsOn;
            }
        }

        private void Arrive()
        {
            if (arrived) return;
            arrived = true;
            try
            {
                var note = HeartActs.NoteOf(job);
                var other = Other;
                switch (Kind)
                {
                    case HeartActKind.Door:
                        Slam();
                        break;
                    case HeartActKind.Linger:
                        // The meal is done: whoever they ate with, if still at the table.
                        if (other == null || !Together.Can(pawn, other))
                        {
                            EndJobWith(JobCondition.Succeeded);
                            return;
                        }
                        break;
                    case HeartActKind.SitWith:
                        if (other != null && Together.Can(pawn, other)) pawn.interactions?.TryInteractWith(other, InteractionDefOf.Chitchat);
                        break;
                }
                bool to = other != null && (Kind == HeartActKind.SitWith || Kind == HeartActKind.Linger || Kind == HeartActKind.Indoors);
                string act = "act:" + Kind.ToString().ToLowerInvariant();
                if (!HeartActs.Say(pawn, to ? other : null, act, note?.tag, note?.lost)) HeartActs.Say(pawn, to ? other : null, act, null, note?.lost);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("heart act arrive", e);
            }
        }

        /// <summary>The door slams behind them: its sound, a puff of dust at it.</summary>
        private void Slam()
        {
            var door = job.targetA.Thing ?? job.targetB.Thing;
            if (door == null || !door.Spawned) return;
            var sound = door.def.building?.soundDoorCloseManual ?? SoundDefOf.Door_CloseManual;
            sound?.PlayOneShot(new TargetInfo(door.Position, door.Map));
            FleckMaker.ThrowDustPuff(door.Position, door.Map, 1.2f);
        }

        private void Stay(int delta)
        {
            stayed += delta;
            var other = Other;
            switch (Kind)
            {
                case HeartActKind.Dance:
                    // Turning where they stand, a note now and then.
                    if (stayed / 30 != (stayed - delta) / 30) pawn.Rotation = pawn.Rotation.Rotated(RotationDirection.Clockwise);
                    if (stayed / 90 != (stayed - delta) / 90) VisualTouches.Notes(pawn, 1);
                    break;
                case HeartActKind.SitWith:
                case HeartActKind.Linger:
                case HeartActKind.Indoors:
                    if (other == null) break;
                    // Whom they're with walked off: it's over.
                    if (!pawn.Position.InHorDistOf(other.Position, 6f))
                    {
                        EndJobWith(JobCondition.Succeeded);
                        return;
                    }
                    pawn.rotationTracker.FaceTarget(other);
                    break;
                case HeartActKind.Sit:
                    if (job.targetB.Thing is Building seat && seat.def.building != null && seat.def.building.isSittable)
                        pawn.rotationTracker.FaceCell(seat.Position + seat.Rotation.FacingCell);
                    else if (job.targetB.IsValid) pawn.rotationTracker.FaceCell(job.targetB.Cell);
                    break;
                case HeartActKind.LostBed:
                case HeartActKind.Cry:
                    if (job.targetB.IsValid) pawn.rotationTracker.FaceCell(job.targetB.Cell);
                    break;
                case HeartActKind.StareOut:
                case HeartActKind.HomeFire:
                    if (job.targetB.IsValid) pawn.rotationTracker.FaceCell(job.targetB.Cell);
                    break;
            }
            if (job.def.joyKind != null && pawn.needs?.joy != null) JoyUtility.JoyTickCheckEnd(pawn, delta, JoyTickFullJoyAction.None);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref stayed, "stayed");
            Scribe_Values.Look(ref arrived, "arrived");
        }
    }
}
