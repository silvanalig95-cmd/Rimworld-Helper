using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>A wished-for thing still to be tried out (A12): who, what or which room, what they'll do, and the thanks to say there.</summary>
    public class TryOut : IExposable
    {
        public Pawn pawn;
        /// <summary>The thing (a chair, a bed, a work of art, a heater); null for a room.</summary>
        public Thing thing;
        /// <summary>A room's middle, where they stand and look round.</summary>
        public IntVec3 cell = IntVec3.Invalid;
        public TryAct act;
        public string thanks;
        /// <summary>What it is, for a friend's word ("armchair", "room").</summary>
        public string label;
        public int since;
        /// <summary>On their way to it now (a job of ours).</summary>
        public bool started;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref thing, "thing");
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref act, "act");
            Scribe_Values.Look(ref thanks, "thanks");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref since, "since");
            Scribe_Values.Look(ref started, "started");
        }

        public LocalTargetInfo Target => thing != null ? new LocalTargetInfo(thing) : new LocalTargetInfo(cell);
    }

    [DefOf]
    public static class LifeDefOf
    {
        public static JobDef Rimfolk_TryOut;

        static LifeDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(LifeDefOf));
    }

    /// <summary>
    /// A12: the wished-for thing, tried out (the content update, the player's go of 20 November 2026). When a wish for a
    /// thing or a room is met (Wishes/Kept.cs's lasting wishes: a thing, a bed, a room), the thanks were said wherever the
    /// one who asked happened to be. With "Answers play out on the map", they now go to it in their free time within the
    /// day (<see cref="TryOutMath.WithinTicks"/>): sit in the new chair, lie on the new bed a moment, warm their hands at the
    /// new heater, stand before the new art, walk into the new room and look round it, and say the thanks there; a friend
    /// nearby now and then admires it. If it can't be reached, or the day passes, the thanks are said where they are, as
    /// before.
    /// </summary>
    public static class TryOuts
    {
        private static readonly Color SpeechGood = new Color(0.6f, 1f, 0.6f);

        /// <summary>
        /// The wish is met: true when the one it was for will go and try it out (the thanks are said there), false to say
        /// the thanks now, as before.
        /// </summary>
        public static bool Begin(Wish w, Pawn who, string thanks)
        {
            var book = LifeBook.Instance;
            if (book == null || w?.def == null || who == null || thanks.NullOrEmpty() || !PlayOuts.On) return false;
            if (!who.Spawned || who.Map == null || !who.Map.IsPlayerHome || !PlayOuts.OurPerson(who)) return false;
            // A group's place has its own first evening there (GroupPlaces.cs).
            if (w.def.Worker is WishWorker_GroupPlace) return false;
            try
            {
                if (!w.Worker.Lasting(w)) return false;
                string kind = w.Worker.KeptKind(w);
                if (!TryOutMath.TriedOut(kind)) return false;
                var t = Find(w, who, kind);
                if (t == null) return false;
                t.pawn = who;
                t.thanks = thanks;
                t.since = Verse.Find.TickManager.TicksGame;
                book.tryOuts.RemoveAll(x => x.pawn == who);
                book.tryOuts.Add(t);
                while (book.tryOuts.Count > LifeBook.MaxTryOuts) book.tryOuts.RemoveAt(0);
                return true;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("a wished-for thing to try", e);
                return false;
            }
        }

        /// <summary>What to try out: for a room, a cell in its middle; for a thing, the newest of it that they can reach.</summary>
        private static TryOut Find(Wish w, Pawn who, string kind)
        {
            var map = who.Map;
            var things = w.Worker.KeptThings(w).Where(x => x != null && x.Spawned && x.Map == map && !x.IsForbidden(who)
                                                            && who.CanReach(x, PathEndMode.Touch, Danger.Some)).ToList();
            if (kind == "room" || kind == "ownroom")
            {
                var room = things.Select(x => x.GetRoom()).FirstOrDefault(r => r != null && !r.PsychologicallyOutdoors) ?? w.Worker.TheirRoom(w);
                var c = Middle(room, map, who);
                return c.IsValid ? new TryOut { cell = c, act = TryAct.Room, label = "CC_Life_TryOut_Room".Translate().Resolve() } : null;
            }
            var thing = things.OrderByDescending(x => x.thingIDNumber).FirstOrDefault();
            if (thing == null) return null;
            var act = TryOutMath.Act(false, thing is Building_Bed, thing.def.building != null && thing.def.building.isSittable,
                Avail.IsHeater(thing.def) || thing.TryGetComp<CompFireOverlay>() != null);
            // Lying on a bed someone is in, or sitting where someone sits: they'd only look at it.
            if (act == TryAct.Lie && thing is Building_Bed bed && bed.AnyOccupants) act = TryAct.Look;
            return new TryOut { thing = thing, act = act, label = thing.def.label };
        }

        /// <summary>A free cell near the middle of the room that they can reach.</summary>
        private static IntVec3 Middle(Room room, Map map, Pawn who)
        {
            if (room == null || room.PsychologicallyOutdoors || room.CellCount <= 0 || room.CellCount > 400) return IntVec3.Invalid;
            var cells = room.Cells.ToList();
            var mid = new IntVec3((int)cells.Average(c => c.x), 0, (int)cells.Average(c => c.z));
            foreach (var c in cells.OrderBy(c => c.DistanceToSquared(mid)).Take(12))
                if (c.Standable(map) && c.GetEdifice(map) == null && who.CanReach(c, PathEndMode.OnCell, Danger.Some)) return c;
            return IntVec3.Invalid;
        }

        /// <summary>Every 250 ticks: try-outs whose one is free go now; one that waited its day out says the thanks where they are.</summary>
        public static void Step(LifeBook book, int now)
        {
            foreach (var t in book.tryOuts.ToList())
            {
                var p = t.pawn;
                if (p == null || p.Dead || p.Destroyed)
                {
                    book.tryOuts.Remove(t);
                    continue;
                }
                bool gone = t.thing != null ? t.thing.Destroyed || !t.thing.Spawned || t.thing.Map != p.MapHeld : !t.cell.IsValid;
                if (gone || !TryOutMath.StillWaits(t.since, now) || !PlayOuts.On)
                {
                    if (!(p.CurJobDef == LifeDefOf.Rimfolk_TryOut))
                    {
                        book.tryOuts.Remove(t);
                        // apart: the thanks the wish always had, said where they are.
                        if (p.Spawned) CouncilGameComponent.Speak(p, t.thanks, SpeechGood);
                    }
                    continue;
                }
                if (p.CurJobDef == LifeDefOf.Rimfolk_TryOut) continue;
                t.started = false;
                if (!p.Spawned || !p.Map.IsPlayerHome || p.Map.dangerWatcher.DangerRating != StoryDanger.None) continue;
                if (!Together.FreeTime(p) || !PlayOuts.CanTakePart(p, p.Map) || !PlayOuts.Interruptible(p) || PlayOuts.NeedsPress(p)) continue;
                if (!p.CanReach(t.Target, t.thing != null ? PathEndMode.Touch : PathEndMode.OnCell, Danger.Some)) continue;
                // A seat or a bed someone else has just now: later.
                if (t.thing != null && (t.act == TryAct.Sit || t.act == TryAct.Lie) && !p.CanReserve(t.thing)) continue;
                var job = JobMaker.MakeJob(LifeDefOf.Rimfolk_TryOut, t.Target);
                job.count = TryOutMath.StayTicks(t.act);
                p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                t.started = p.CurJobDef == LifeDefOf.Rimfolk_TryOut;
            }
        }

        public static TryOut Of(Pawn p) => p == null ? null : LifeBook.Instance?.tryOuts.FirstOrDefault(t => t.pawn == p);

        /// <summary>They've reached it: the thanks said there, and a friend nearby may admire it.</summary>
        internal static void Arrived(Pawn p, TryOut t)
        {
            var book = LifeBook.Instance;
            book?.tryOuts.Remove(t);
            if (t == null || p == null) return;
            Speech.Say(p, t.thanks, null, SpeechGood);
            if (!Rand.Chance(TryOutMath.AdmireChance)) return;
            var friend = p.Map?.mapPawns.FreeColonistsSpawned.Where(o => o != p && Together.Can(p, o) && o.relations != null
                                                                          && o.relations.OpinionOf(p) >= TryOutMath.FriendOpinion).RandomElementWithFallback();
            if (friend == null) return;
            string line = Flavor.FromPool("Rimfolk_Pool_TryOut_Admire", friend, null, null, "act:" + t.act.ToString().ToLowerInvariant());
            // together: the friend is near them as they try it (Together.Can above).
            if (!line.NullOrEmpty()) Speech.Say(friend, line.Formatted(friend.Named("PAWN"), p.Named("OTHER"), (t.label ?? "").Named("THING")).Resolve(), p);
        }
    }

    /// <summary>
    /// Trying out the wished-for thing (A12): go to it (the seat, the bed's own place, beside the art or the heater, the
    /// room's middle), say the thanks there, and stay a moment sat in it, lying on it, facing it with hands to the warmth,
    /// or looking round the room. Ends at any danger, if the thing is gone, and like any job at the player's word.
    /// </summary>
    public class JobDriver_TryOut : JobDriver
    {
        private bool arrived;

        private TryOut Plan => TryOuts.Of(pawn);

        private TryAct Act => Plan?.act ?? actSaved;

        private TryAct actSaved = TryAct.Look;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            actSaved = Plan?.act ?? TryAct.Look;
            var t = job.targetA.Thing;
            if (t != null && (Act == TryAct.Sit || Act == TryAct.Lie)) return pawn.Reserve(t, job, 1, -1, null, errorOnFailed);
            if (t == null && job.targetA.IsValid) pawn.Map.pawnDestinationReservationManager.Reserve(pawn, job, job.targetA.Cell);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            if (job.targetA.HasThing) this.FailOnDespawnedOrNull(TargetIndex.A);
            var act = Act;
            switch (act)
            {
                case TryAct.Sit:
                    yield return Toils_Goto.GotoCell(job.targetA.Thing.Position, PathEndMode.OnCell);
                    break;
                case TryAct.Lie:
                    var bed = job.targetA.Thing as Building_Bed;
                    yield return Toils_Goto.GotoCell(bed != null ? bed.GetSleepingSlotPos(0) : job.targetA.Thing.Position, PathEndMode.OnCell);
                    break;
                case TryAct.Room:
                    yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
                    break;
                default:
                    yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
                    break;
            }
            var stay = ToilMaker.MakeToil("TryOut");
            stay.initAction = () =>
            {
                if (act == TryAct.Lie) pawn.jobs.posture = PawnPosture.LayingInBed;
                if (act == TryAct.Sit && job.targetA.Thing != null) pawn.Rotation = job.targetA.Thing.Rotation;
                Arrive();
            };
            stay.tickIntervalAction = delta =>
            {
                if (act == TryAct.Look || act == TryAct.Warm)
                {
                    if (job.targetA.HasThing) pawn.rotationTracker.FaceTarget(job.targetA.Thing);
                }
                else if (act == TryAct.Room && pawn.IsHashIntervalTick(120, delta)) pawn.Rotation = pawn.Rotation.Rotated(RotationDirection.Clockwise);
            };
            stay.AddFinishAction(() =>
            {
                if (act == TryAct.Lie) pawn.jobs.posture = PawnPosture.Standing;
            });
            stay.defaultCompleteMode = ToilCompleteMode.Delay;
            stay.defaultDuration = Mathf.Max(120, job.count);
            stay.handlingFacing = true;
            yield return stay;
        }

        private void Arrive()
        {
            if (arrived) return;
            arrived = true;
            try
            {
                var plan = Plan;
                if (plan != null) TryOuts.Arrived(pawn, plan);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("trying out a wished-for thing", e);
            }
        }

        public override string GetReport()
        {
            var t = Plan;
            string what = t?.label ?? job.targetA.Thing?.LabelShort ?? "CC_Life_TryOut_Room".Translate().Resolve();
            return ("CC_Life_TryOut_Report_" + Act).Translate(what.Named("THING")).Resolve();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref arrived, "arrived");
            Scribe_Values.Look(ref actSaved, "actSaved", TryAct.Look);
        }
    }
}
