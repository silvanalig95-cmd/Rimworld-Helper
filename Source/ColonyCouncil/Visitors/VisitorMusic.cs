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
    /// <summary>
    /// Visiting singers (a visit's work "sing", VisitorWork) and the colony's instrument: when the colony has built one (a
    /// harp, a harpsichord, a piano, any of the game's own instruments), one of them walks over after dark and plays it,
    /// the game's own playing, so the music is heard and those who hear it are glad of it the game's own way. The rest
    /// sing at their camp as before. Only an instrument the colony built and keeps open (not forbidden, not in someone's
    /// bedroom, a cell or a sickroom), one of them at a time, and never again soon after it didn't work out.
    /// </summary>
    public static class VisitorMusic
    {
        /// <summary>How far they walk to play.</summary>
        private const float Range = 90f;

        private static readonly Dictionary<int, int> nextTry = new Dictionary<int, int>();
        private static Game tryGame;

        private static int Now => Find.TickManager.TicksGame;

        private static bool Waiting(Pawn p)
        {
            if (tryGame != Current.Game)
            {
                tryGame = Current.Game;
                nextTry.Clear();
            }
            return nextTry.TryGetValue(p.thingIDNumber, out int next) && Now < next && next - Now <= GenDate.TicksPerHour * 6;
        }

        /// <summary>Not to try the colony's instrument again for a while (it was taken, or the playing didn't hold).</summary>
        public static void Rest(Pawn p, int hours)
        {
            if (p == null) return;
            if (nextTry.Count > 200) nextTry.Clear();
            nextTry[p.thingIDNumber] = Now + GenDate.TicksPerHour * hours;
        }

        /// <summary>The walk over to the colony's instrument and a while playing it, or null when there's none for them now.</summary>
        public static Job JobFor(Pawn p, VisitorTrip t)
        {
            if (p?.Map == null || t == null || !p.RaceProps.Humanlike || p.DevelopmentalStage.Juvenile() || Waiting(p)) return null;
            if (!(p.health?.capacities?.CapableOf(PawnCapacityDefOf.Manipulation) ?? false)) return null;
            // One of them plays; the others sing at their fire.
            if (t.Here.Any(o => o != p && o.CurJobDef == VisitorActsDefOf.Rimfolk_VisitPlay)) return null;
            if (!Instrument(p, t, out var instrument, out var seat))
            {
                Rest(p, 2);
                return null;
            }
            return JobMaker.MakeJob(VisitorActsDefOf.Rimfolk_VisitPlay, instrument, seat);
        }

        /// <summary>The colony's own instrument nearest their spot that this singer may play, and where to sit or stand to play it.</summary>
        public static bool Instrument(Pawn p, VisitorTrip t, out Building_MusicalInstrument instrument, out IntVec3 seat)
        {
            instrument = null;
            seat = IntVec3.Invalid;
            var map = p.Map;
            var from = t.spot.IsValid ? t.spot : p.Position;
            float best = float.MaxValue;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (!(b is Building_MusicalInstrument m) || !m.Spawned || m.Faction != Faction.OfPlayer) continue;
                float d = m.Position.DistanceToSquared(from);
                if (d >= best || d > Range * Range || m.IsBeingPlayed || m.IsBurning() || m.IsBrokenDown() || m.IsForbidden(Faction.OfPlayer)) continue;
                var room = m.GetRoom();
                if (room != null && (room.IsPrisonCell || room.Role == RoomRoleDefOf.Bedroom || room.Role == RoomRoleDefOf.Barracks
                                     || room.Role == RoomRoleDefOf.Hospital || room.Role == RoomRoleDefOf.PrisonBarracks)) continue;
                if (!p.CanReserve(m)) continue;
                var at = Seat(p, m);
                if (!at.IsValid) continue;
                instrument = m;
                seat = at;
                best = d;
            }
            return instrument != null;
        }

        /// <summary>Where the game's own players sit or stand to play it (its interaction spot), else beside it.</summary>
        private static IntVec3 Seat(Pawn p, Building m)
        {
            var map = m.Map;
            if (m.def.hasInteractionCell)
            {
                var c = m.InteractionCell;
                return c.InBounds(map) && c.Standable(map) && p.CanReserveAndReach(c, PathEndMode.OnCell, Danger.Some) ? c : IntVec3.Invalid;
            }
            foreach (var c in GenAdj.CellsAdjacentCardinal(m))
                if (c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map) == null && p.CanReserveAndReach(c, PathEndMode.OnCell, Danger.Some))
                    return c;
            return IntVec3.Invalid;
        }
    }

    /// <summary>
    /// A visiting singer at the colony's instrument: the walk to its seat, then a while playing it with the game's own
    /// playing (its music heard about it), facing it, until the stint or the evening is over or their visit moves on.
    /// </summary>
    public class JobDriver_VisitPlay : JobDriver
    {
        private bool playing;
        private static FleckDef musicFleck;
        private static bool looked;

        private Building_MusicalInstrument Instrument => job.targetA.Thing as Building_MusicalInstrument;

        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed) && pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref playing, "playing");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var ext = job.def.GetModExtension<VisitWorkExtension>() ?? new VisitWorkExtension();
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Instrument == null || Instrument.IsBurning() || !VisitorWork.InHours(VisitorWork.Sing, GenLocalDate.HourOfDay(pawn.Map)));
            // Their visit has moved on (leaving, or gone indoors for the night): so do they.
            this.FailOn(() => !(pawn.GetLord()?.CurLordToil is LordToil_VisitHere));
            AddFinishAction(_ =>
            {
                try
                {
                    if (playing) Instrument?.StopPlaying();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("visitor playing ends", e);
                }
                playing = false;
            });
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            var play = ToilMaker.MakeToil("VisitPlay");
            play.initAction = () =>
            {
                var m = Instrument;
                if (m == null || m.IsBeingPlayed)
                {
                    VisitorMusic.Rest(pawn, 3);
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                m.StartPlaying(pawn);
                playing = true;
                if (Rand.Chance(0.35f)) VisitorWords.Say(pawn, VisitorTracker.Instance?.TripOf(pawn), "play", VisitorActs.LinesPool);
            };
            play.defaultCompleteMode = ToilCompleteMode.Delay;
            play.defaultDuration = Mathf.Max(600, ext.ticks.RandomInRange);
            play.handlingFacing = true;
            play.socialMode = ext.social;
            play.tickIntervalAction = delta =>
            {
                var m = Instrument;
                if (m == null) return;
                pawn.rotationTracker.FaceTarget(m);
                // The game stopped the playing (someone else took the instrument over): they leave it be for a while.
                if (playing && !m.IsBeingPlayed)
                {
                    playing = false;
                    VisitorMusic.Rest(pawn, 6);
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                if (VisualTouches.On && pawn.IsHashIntervalTick(240, delta))
                {
                    if (!looked)
                    {
                        looked = true;
                        musicFleck = DefDatabase<FleckDef>.GetNamedSilentFail("Music");
                    }
                    if (musicFleck != null) FleckMaker.ThrowMetaIcon(m.Position, m.Map, musicFleck);
                }
            };
            yield return play;
        }
    }
}
