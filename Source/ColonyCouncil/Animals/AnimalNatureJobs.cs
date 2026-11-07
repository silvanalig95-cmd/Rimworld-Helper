using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    [DefOf]
    public static class AnimalNatureDefOf
    {
        public static JobDef Rimfolk_AnimalStand;
        public static JobDef Rimfolk_AnimalLieBy;
        public static JobDef Rimfolk_AnimalShowOff;
        public static JobDef Rimfolk_AnimalWaitDoor;
        public static JobDef Rimfolk_AnimalWatch;
        public static JobDef Rimfolk_AnimalVigil;
        public static JobDef Rimfolk_AnimalKnockOver;

        static AnimalNatureDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(AnimalNatureDefOf));
    }

    /// <summary>How an animal stays where it went (Defs/Misc/Rimfolk_AnimalNature_Jobs.xml): lying down, or holding its ground.</summary>
    public class AnimalLingerExtension : DefModExtension
    {
        /// <summary>Lies down while it stays (by a bed, a grave, a patient).</summary>
        public bool lie;
        /// <summary>Holds its ground: fights whatever hostile comes at it, and is done when no danger is left near.</summary>
        public bool stand;
        /// <summary>Its report with whom it's about ({OTHER}), a translation key; the job's own words without one.</summary>
        public string report;
    }

    /// <summary>
    /// An animal's moment of staying (Animals/AnimalTempers.cs, AnimalBonds.cs): it walks to a cell (target A) and stays
    /// there a while (the job's count, in ticks) facing something (target B: a door, a grave, a bed, someone), standing,
    /// lying down, or holding its ground in a fight. Target C, when there is one, is whom it's about, by name in its report.
    /// </summary>
    public class JobDriver_AnimalLinger : JobDriver
    {
        private string about;

        private AnimalLingerExtension Ext => job.def.GetModExtension<AnimalLingerExtension>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (job.targetA.IsValid && pawn.Map != null) pawn.Map.pawnDestinationReservationManager.Reserve(pawn, job, job.targetA.Cell);
            return true;
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            about = (job.targetC.Thing as Pawn)?.LabelShort ?? (job.targetB.Thing as Pawn)?.LabelShort;
        }

        public override string GetReport()
        {
            var ext = Ext;
            if (ext?.report != null && !about.NullOrEmpty() && ext.report.CanTranslate()) return ext.report.Translate(about.Named("OTHER")).Resolve();
            return base.GetReport();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var ext = Ext;
            this.FailOn(() => pawn.Downed || pawn.InMentalState);
            // A danger breaking out ends a quiet moment (the game's own animals see to themselves); a stand is for danger.
            if (ext == null || !ext.stand) this.FailOn(() => pawn.Map != null && pawn.Map.dangerWatcher.DangerRating == StoryDanger.High);
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var stay = ToilMaker.MakeToil("AnimalLinger");
            stay.initAction = () =>
            {
                if (Ext?.lie == true) pawn.jobs.posture = PawnPosture.LayingOnGroundNormal;
            };
            stay.tickIntervalAction = Stay;
            stay.defaultCompleteMode = ToilCompleteMode.Delay;
            stay.defaultDuration = Math.Max(300, job.count);
            stay.handlingFacing = true;
            stay.AddFinishAction(() =>
            {
                if (Ext?.lie == true) pawn.jobs.posture = PawnPosture.Standing;
            });
            yield return stay;
        }

        private void Stay(int delta)
        {
            var face = job.targetB;
            if (face.HasThing)
            {
                if (!face.Thing.Spawned || face.Thing.Map != pawn.Map)
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
                pawn.rotationTracker.FaceTarget(face.Thing);
            }
            else if (face.IsValid) pawn.rotationTracker.FaceCell(face.Cell);
            if (Ext?.stand != true) return;
            // Holding its ground: whatever hostile comes at it, it fights; with no danger left near, it's done.
            var map = pawn.Map;
            Pawn close = null;
            bool any = false;
            foreach (var p in map.mapPawns.AllPawnsSpawned)
            {
                if (p.Dead || p.Downed || !p.HostileTo(pawn)) continue;
                float d = p.Position.DistanceToSquared(pawn.Position);
                if (d <= 30f * 30f) any = true;
                if (d <= 2.5f * 2.5f) close = p;
            }
            // What it faces (a wild predator that isn't hostile yet) keeps it there while it stays near.
            if (face.Thing is Pawn danger && !danger.Dead && !danger.Downed && danger.Position.InHorDistOf(pawn.Position, 30f)) any = true;
            if (close != null)
            {
                pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.AttackMelee, close), JobCondition.InterruptForced, null, false, true);
                return;
            }
            if (!any && pawn.jobs.curDriver.ticksLeftThisToil < job.count - 300) EndJobWith(JobCondition.Succeeded);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref about, "about");
        }
    }

    /// <summary>
    /// A mischievous one knocks a few things off a shelf (Animals/AnimalTempers.cs): it goes to the stack (target A),
    /// noses at it a moment, and a few of it fall onto the floor beside the shelf, out of the stores, for a hauler to put
    /// back. Only light things, and never more than a handful.
    /// </summary>
    public class JobDriver_AnimalKnockOver : JobDriver
    {
        private Thing Target => job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => pawn.Downed || pawn.InMentalState || !(Target?.IsInAnyStorage() ?? false));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var nose = ToilMaker.MakeToil("AnimalNose");
            nose.defaultCompleteMode = ToilCompleteMode.Delay;
            nose.defaultDuration = 90;
            nose.handlingFacing = true;
            nose.tickIntervalAction = _ =>
            {
                if (Target != null) pawn.rotationTracker.FaceTarget(Target);
            };
            yield return nose;
            var knock = ToilMaker.MakeToil("AnimalKnock");
            knock.defaultCompleteMode = ToilCompleteMode.Instant;
            knock.initAction = Knock;
            yield return knock;
        }

        private void Knock()
        {
            try
            {
                var t = Target;
                if (t == null || !t.Spawned) return;
                var map = t.Map;
                var cell = AnimalTempers.Spill(t);
                if (!cell.IsValid) return;
                float mass = Math.Max(0.01f, t.GetStatValue(StatDefOf.Mass));
                int n = Math.Max(1, Math.Min(t.stackCount, Math.Min(Rand.RangeInclusive(2, 8), (int)(3f / mass))));
                var fallen = t.SplitOff(n);
                if (fallen == null) return;
                if (!GenPlace.TryPlaceThing(fallen, cell, map, ThingPlaceMode.Direct) && !fallen.Spawned)
                    GenPlace.TryPlaceThing(fallen, cell, map, ThingPlaceMode.Near);
                FleckMaker.ThrowDustPuff(cell, map, 0.8f);
                AnimalTempers.KnockedOver(pawn, fallen);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("animal temperament: knocked over", e);
            }
        }
    }

    /// <summary>
    /// One line under a named or bonded animal's inspect text, built at most every four seconds of game time:
    /// "Temperament: loyal · sleeps beside Pepper · at odds with Whiskers", then its grief, its voice and what it did
    /// lately, each when there is one (Animals/AnimalTempers.cs, AnimalBonds.cs, AnimalVoices.cs).
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
    public static class Patch_AnimalNatureInspect
    {
        private static readonly Dictionary<int, (int tick, string text)> cache = new Dictionary<int, (int, string)>();
        private static Game game;

        public static void Postfix(Pawn __instance, ref string __result)
        {
            if (!RimfolkPatches.On || !AnimalTempers.On || __instance?.RaceProps == null || !__instance.RaceProps.Animal || __instance.Faction != Faction.OfPlayer) return;
            try
            {
                string line = Line(__instance);
                if (line.NullOrEmpty()) return;
                __result = __result.NullOrEmpty() ? line : __result.TrimEndNewlines() + "\n" + line;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("animal nature inspect line", e);
            }
        }

        private static string Line(Pawn a)
        {
            if (game != Current.Game)
            {
                game = Current.Game;
                cache.Clear();
            }
            int now = Find.TickManager.TicksGame;
            if (cache.TryGetValue(a.thingIDNumber, out var c) && now - c.tick < 250 && now >= c.tick) return c.text;
            string text = Build(a);
            if (cache.Count > 300) cache.Clear();
            cache[a.thingIDNumber] = (now, text);
            return text;
        }

        private static string Build(Pawn a)
        {
            var t = AnimalTempers.Of(a);
            if (t == null) return null;
            var parts = new List<string> { "CC_Temper_Inspect".Translate(AnimalTempers.Label(t.Value).Named("TEMPER")).Resolve() };
            parts.AddRange(AnimalBonds.InspectParts(a));
            var sb = new StringBuilder(string.Join(" · ", parts));
            string grief = AnimalBonds.GriefLine(a);
            if (!grief.NullOrEmpty()) sb.Append("\n").Append(grief);
            string voice = AnimalVoices.InspectLine(a);
            if (!voice.NullOrEmpty()) sb.Append("\n").Append(voice);
            string lately = AnimalTempers.Lately(a);
            if (!lately.NullOrEmpty()) sb.Append("\n").Append("CC_Temper_Lately".Translate(lately.Named("WHAT")).Resolve());
            return sb.ToString();
        }
    }
}
