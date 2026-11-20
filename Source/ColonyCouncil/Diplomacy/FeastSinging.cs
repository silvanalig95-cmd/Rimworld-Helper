using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// A singer's turn in a feast's singing contest (FeastContests, the content update's B28): to the colony's instrument (its
    /// interaction spot; the instrument's own music plays while they sing, as when it is played) or beside a fire, a word as
    /// they start (Rimfolk_Pool_OutsiderSays, "feastsing"), the song (notes over their head), and their score kept at the end.
    /// </summary>
    public class JobDriver_FeastSing : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        private Building_MusicalInstrument Instrument => job.targetA.Thing as Building_MusicalInstrument;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !FeastContests.Singing(pawn));
            if (Instrument != null) yield return Toils_Goto.GotoCell(Instrument.InteractionCell, PathEndMode.OnCell);
            else yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            bool playing = false;
            var sing = ToilMaker.MakeToil("FeastSing");
            sing.defaultCompleteMode = ToilCompleteMode.Delay;
            sing.defaultDuration = FeastMath.SingTicks;
            sing.handlingFacing = true;
            sing.initAction = () =>
            {
                // together: said at the instrument or the fire, with the others listening.
                Outsiders.Say(pawn, "feastsing", null);
                try
                {
                    var mi = Instrument;
                    if (mi != null && !mi.IsBeingPlayed)
                    {
                        mi.StartPlaying(pawn);
                        playing = true;
                    }
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("feasts: the singer's instrument", e);
                }
            };
            sing.tickAction = () =>
            {
                pawn.rotationTracker.FaceTarget(job.targetA);
                if (Instrument == null && pawn.IsHashIntervalTick(200) && VisualTouches.On)
                {
                    var note = DefDatabase<FleckDef>.GetNamedSilentFail("Music");
                    if (note != null) FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, note);
                }
            };
            sing.AddFinishAction(() =>
            {
                try
                {
                    var mi = Instrument;
                    if (playing && mi != null && mi.IsBeingPlayed) mi.StopPlaying();
                    playing = false;
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("feasts: the singer's instrument stopped", e);
                }
            });
            yield return sing;
            yield return Toils_General.Do(() => FeastContests.Sang(pawn));
        }
    }
}
