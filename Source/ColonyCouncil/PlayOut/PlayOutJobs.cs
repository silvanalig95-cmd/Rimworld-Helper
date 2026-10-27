using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// A colonist an answer sent somewhere for a while: to keep someone company (following them about as they go, or
    /// sitting by their bed), to a grave, to the camp's fire, on a long walk, or, for a lesson, to work at a teacher's
    /// side and learn by watching. Ends early at any danger, and like any job when they're drafted, downed or need
    /// something urgently. The plan it belongs to hears how long they stayed.
    /// </summary>
    public class JobDriver_PlayOutVisit : JobDriver
    {
        private int stayed;
        private bool talked;

        private PlayOut Play => PlayOutTracker.Instance?.RunningFor(pawn);

        private Pawn OtherPawn => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport()
        {
            var play = Play;
            var other = OtherPawn;
            if (other != null)
                return (play?.kind == PlayKind.Lesson ? "CC_Play_Report_Lesson" : "CC_Play_Report_Visit").Translate(other.Named("OTHER")).Resolve();
            switch (play?.to)
            {
                case PlaySpot.Grave: return "CC_Play_Report_Grave".Translate();
                case PlaySpot.Camp: return "CC_Play_Report_Camp".Translate();
                case PlaySpot.Fields: return "CC_Play_Report_Fields".Translate();
                case PlaySpot.Gate: return "CC_Play_Report_Gate".Translate();
                case PlaySpot.Water: return "CC_Play_Report_Water".Translate();
                case PlaySpot.Walk: return "CC_Play_Report_Walk".Translate();
                case PlaySpot.Outdoors:
                case PlaySpot.Fire:
                case PlaySpot.Place: return "CC_Play_Report_Alone".Translate();
                default: return "CC_Play_Report_Walk".Translate();
            }
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            // Hunger, tiredness, a wound or being called to arms end it: they see to that first.
            this.FailOn(() => !PlayOuts.CanJoin(pawn) || PlayOuts.NeedsPress(pawn));
            AddFinishAction(_ =>
            {
                try
                {
                    PlayOutTracker.Instance?.VisitEnded(pawn, stayed);
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("visit end", e);
                }
            });
            if (job.targetA.HasThing)
            {
                this.FailOnDespawnedOrNull(TargetIndex.A);
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            }
            else yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var stay = ToilMaker.MakeToil("PlayOutVisit");
            stay.initAction = Arrive;
            stay.defaultCompleteMode = ToilCompleteMode.Delay;
            stay.defaultDuration = Mathf.Max(600, job.count);
            stay.socialMode = RandomSocialMode.SuperActive;
            stay.handlingFacing = true;
            stay.tickIntervalAction = Stay;
            // At work on the place (breaking up a fallen star, stripping a wreck): the game's own effect for it.
            var fx = Play?.workEffect;
            if (fx != null && job.targetA.IsValid) stay.WithEffect(fx, TargetIndex.A);
            yield return stay;
        }

        private void Arrive()
        {
            try
            {
                var play = Play;
                if (play?.to == PlaySpot.Camp) CampTracker.Instance?.camp?.Visited(pawn);
                Talk(play);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("visit arrive", e);
            }
        }

        /// <summary>A word with whoever they came for, once they're close: a deep talk on a visit, a chat at a lesson.</summary>
        private void Talk(PlayOut play)
        {
            var other = OtherPawn;
            // together: the visit job, once they are a few steps apart at most.
            if (talked || other == null || !other.RaceProps.Humanlike || !other.Awake() || !pawn.Position.InHorDistOf(other.Position, 3f)) return;
            talked = true;
            var def = play?.kind == PlayKind.Lesson ? InteractionDefOf.Chitchat : InteractionDefOf.DeepTalk;
            if (def != null) pawn.interactions?.TryInteractWith(other, def);
        }

        private void Stay(int delta)
        {
            stayed += delta;
            var other = OtherPawn;
            var play = Play;
            if (other != null)
            {
                if (other.Dead || !other.Spawned || other.Map != pawn.Map)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                // Keeping someone company means going where they go.
                if (!pawn.Position.InHorDistOf(other.Position, 4f))
                {
                    if (!pawn.pather.Moving) pawn.pather.StartPath(other, PathEndMode.Touch);
                    return;
                }
                if (!pawn.pather.Moving) pawn.rotationTracker.FaceTarget(other);
                Talk(play);
                if (play?.kind == PlayKind.Lesson && play.skill != null && play.xpPerHour > 0f)
                {
                    var rec = pawn.skills?.GetSkill(play.skill);
                    if (rec != null && !rec.TotallyDisabled) rec.Learn(play.xpPerHour / PlayOutMath.TicksPerHour * delta);
                }
            }
            else if (job.targetB.IsValid) pawn.rotationTracker.FaceCell(job.targetB.Cell);
            // At a grave after dark, a low glow there while they stay (Immersion/Glows.cs).
            if (job.targetA.Thing is Building_Grave grave && play?.to == PlaySpot.Grave)
                VisualTouches.Candle(pawn.Map, grave.Position, Find.TickManager.TicksGame + 300);
            if (pawn.needs?.joy != null && job.def.joyKind != null) JoyUtility.JoyTickCheckEnd(pawn, delta, JoyTickFullJoyAction.None);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref stayed, "stayed");
            Scribe_Values.Look(ref talked, "talked");
        }
    }
}
