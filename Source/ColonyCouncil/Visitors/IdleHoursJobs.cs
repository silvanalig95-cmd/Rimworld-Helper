using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>On an idle stint's JobDef: which doing it is, how readily they talk with whoever is near meanwhile, and the game's effect shown while it lasts.</summary>
    public class VisitIdleExtension : DefModExtension
    {
        public IdleDoing doing;
        public RandomSocialMode social = RandomSocialMode.Normal;
        public EffecterDef effecter;
    }

    /// <summary>
    /// A visitor's idle stint (IdleHours): the walk to the place (the fire's ring, the seat, the game, the art, the shrine,
    /// one of their own or one of the colony's people, their animal), then a while there facing it, with the game's own look
    /// of it: the horseshoes and stones thrown, the cards, the screen's glow, the instrument played, the prayer's sign, the
    /// food's or the drug's own effect and sound, a pipe's smoke, chips from a knife. Nothing is reserved; the moment one of
    /// the colony's people sets out for the game, the seat or the place, the visitor stands up and gives it to them. What
    /// they eat, drink or smoke comes out of their own pack at the end.
    /// </summary>
    public class JobDriver_VisitIdle : JobDriver
    {
        private int stint = -1;
        private bool playing;
        [Unsaved] private readonly IdleCheck check = new IdleCheck();
        private static FleckDef musicFleck;
        private static EffecterDef pokerEffecter;
        private static bool looked;

        private static readonly VisitIdleExtension Plain = new VisitIdleExtension();

        private VisitIdleExtension Ext => job.def.GetModExtension<VisitIdleExtension>() ?? Plain;

        public IdleDoing Doing => Ext.doing;

        private Pawn Other => job.targetA.Thing as Pawn;

        private Thing Place => job.targetB.Thing;

        /// <summary>Nothing reserved: the colony's people find everything free, and the visitor gives way.</summary>
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref stint, "stint", -1);
            Scribe_Values.Look(ref playing, "playing");
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            IdleHours.Started(pawn);
        }

        public override string GetReport()
        {
            try
            {
                var d = Doing;
                if ((d == IdleDoing.Game || d == IdleDoing.Music || d == IdleDoing.Stars) && IdleHours.JoyOf(Place?.def)?.job is JobDef j && !j.reportString.NullOrEmpty())
                    return ReportStringProcessed(j.reportString.Replace("TargetA", "TargetB"));
                if (d == IdleDoing.OwnDrug)
                    return (IdleHours.DrugKind(job.thingDefToCarry) == OwnDrugKind.Smoke ? "CC_Idle_Report_Smoke" : "CC_Idle_Report_Drink").Translate().Resolve();
            }
            catch
            {
                // The job's own words then.
            }
            return base.GetReport();
        }

        private static void Look()
        {
            if (looked) return;
            looked = true;
            musicFleck = DefDatabase<FleckDef>.GetNamedSilentFail("Music");
            pokerEffecter = DefDatabase<EffecterDef>.GetNamedSilentFail("PlayPoker");
        }

        private bool Still() => check.Holds(pawn, Doing, job);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Look();
            var ext = Ext;
            var d = ext.doing;
            if (stint < 0)
            {
                var (lo, hi) = IdleHoursMath.Stint(d);
                stint = Rand.RangeInclusive(lo, hi);
            }
            AddFinishAction(_ =>
            {
                try
                {
                    if (playing && Place is Building_MusicalInstrument m) m.StopPlaying();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("a visitor's playing ends", e);
                }
                playing = false;
                IdleHours.Ended(pawn);
            });
            this.FailOn(() => !Still());
            bool withSomeone = d == IdleDoing.Chat || d == IdleDoing.Animal || d == IdleDoing.Company;
            if (withSomeone)
            {
                this.FailOnDespawnedOrNull(TargetIndex.A);
                this.FailOn(() => Other == null || Other.Dead || Other.Downed || !Other.Awake());
                if (d == IdleDoing.Company) this.FailOn(() => !SmallHabits.CanListen(Other));
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            }
            else
            {
                if (job.targetB.HasThing) this.FailOn(() => Place == null || !Place.Spawned || Place.IsBurning());
                // Someone of the colony wants the game, the seat or the place: they give it up before getting there.
                var walk = Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
                walk.AddPreTickIntervalAction(delta =>
                {
                    if (pawn.IsHashIntervalTick(60, delta)) GiveWayIfWanted();
                });
                yield return walk;
            }
            Toil stay;
            if (d == IdleDoing.Chat || d == IdleDoing.Animal)
            {
                // The one walked over to waits with them, facing them: one of their own, or their animal being brushed down.
                stay = Toils_General.WaitWith(TargetIndex.A, stint, false, true, false, TargetIndex.A);
            }
            else
            {
                stay = ToilMaker.MakeToil("VisitIdle");
                stay.defaultCompleteMode = ToilCompleteMode.Delay;
                stay.defaultDuration = Mathf.Max(120, stint);
            }
            stay.socialMode = ext.social;
            stay.handlingFacing = true;
            stay.AddPreInitAction(() => Begin(d));
            stay.AddPreTickIntervalAction(delta => During(d, delta));
            var effecter = ext.effecter ?? EffecterOf(d);
            if (effecter != null) stay.WithEffect(effecter, TargetIndex.A);
            if (d == IdleDoing.Snack || d == IdleDoing.OwnDrug)
            {
                var sound = job.thingDefToCarry?.ingestible?.ingestSound;
                if (sound != null) stay.PlaySustainerOrSound(sound);
            }
            yield return stay;
            if (d == IdleDoing.Snack || d == IdleDoing.OwnDrug) yield return Toils_General.Do(Consume);
        }

        /// <summary>The game's own effect for it: the food's or the drug's own as it's eaten or had, the cards at a poker table, a screen's glow.</summary>
        private EffecterDef EffecterOf(IdleDoing d)
        {
            if (d == IdleDoing.Snack || d == IdleDoing.OwnDrug) return job.thingDefToCarry?.ingestible?.ingestEffect;
            if (d != IdleDoing.Game) return null;
            var place = Place;
            var joy = IdleHours.JoyOf(place?.def);
            if (joy?.job?.driverClass != null && joy.job.driverClass.Name == "JobDriver_PlayPoker") return pokerEffecter;
            return place?.def?.building?.effectWatching;
        }

        private void Begin(IdleDoing d)
        {
            try
            {
                if (d == IdleDoing.Music && Place is Building_MusicalInstrument m)
                {
                    if (m.IsBeingPlayed)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    m.StartPlaying(pawn);
                    playing = true;
                }
                if (!Rand.Chance(IdleHoursMath.LineChance(d))) return;
                bool drink = d == IdleDoing.OwnDrug && IdleHours.DrugKind(job.thingDefToCarry) == OwnDrugKind.Drink;
                IdleHours.Say(pawn, IdleHoursMath.Moment(d, drink), d == IdleDoing.Company ? Other : null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a visitor's idle stint begins", e);
            }
        }

        private void During(IdleDoing d, int delta)
        {
            var map = pawn.Map;
            if (map == null) return;
            if (d == IdleDoing.Company)
            {
                var c = Other;
                if (c != null) pawn.rotationTracker.FaceTarget(c);
                // Not followed about: once they've walked off, the visitor leaves them be.
                if (c == null || !c.Position.InHorDistOf(pawn.Position, IdleHoursMath.CompanyNear))
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
            }
            else if (job.targetB.IsValid && d != IdleDoing.Chat && d != IdleDoing.Animal)
            {
                var face = job.targetB.HasThing ? job.targetB.Thing.Position : job.targetB.Cell;
                if (face != pawn.Position) pawn.rotationTracker.FaceCell(face);
            }
            if (pawn.IsHashIntervalTick(60, delta) && GiveWayIfWanted()) return;
            if (d == IdleDoing.Music && playing && Place is Building_MusicalInstrument m && !m.IsBeingPlayed)
            {
                // The game stopped the playing (someone took the instrument over).
                playing = false;
                EndJobWith(JobCondition.Incompletable);
                return;
            }
            if (!VisualTouches.On) return;
            if (d == IdleDoing.Game && pawn.IsHashIntervalTick(400, delta) && Place != null)
            {
                switch (IdleHours.JoyOf(Place.def)?.toss ?? JoyThrow.None)
                {
                    case JoyThrow.Horseshoe:
                        FleckMaker.ThrowHorseshoe(pawn, Place.Position);
                        break;
                    case JoyThrow.Stone:
                        FleckMaker.ThrowStone(pawn, Place.Position);
                        break;
                }
            }
            if (d == IdleDoing.Music && musicFleck != null && Place != null && pawn.IsHashIntervalTick(240, delta)) FleckMaker.ThrowMetaIcon(Place.Position, map, musicFleck);
            if (d == IdleDoing.Pray && pawn.IsHashIntervalTick(300, delta)) FleckMaker.ThrowMetaIcon(pawn.Position, map, FleckDefOf.Meditating);
            // Brushing an animal down: a little dust off its coat now and then.
            if (d == IdleDoing.Animal && Other != null && pawn.IsHashIntervalTick(300, delta)) FleckMaker.ThrowDustPuff(Other.DrawPos, map, 0.4f);
        }

        /// <summary>
        /// One of the colony's people has set out for the game, the seat or the place the visitor is at or going to: the
        /// visitor gives it up (now and then with a word to them) and leaves it be for a few hours.
        /// </summary>
        private bool GiveWayIfWanted()
        {
            var d = Doing;
            if (d == IdleDoing.Chat || d == IdleDoing.Animal || d == IdleDoing.Company) return false;
            var cell = job.targetA.Cell;
            // The thing itself counts for a game, an instrument, the telescope, art and a shrine; at a fire, a seat or under a
            // roof only the place, since the colony's people come to a fire to cook at it or feed it.
            bool thing = d == IdleDoing.Game || d == IdleDoing.Music || d == IdleDoing.Stars || d == IdleDoing.Art || d == IdleDoing.Pray;
            if (!IdleHours.ColonyWho(pawn.Map, thing ? Place : null, cell, out var who)) return false;
            IdleHours.GaveWay(pawn, Place);
            if (who != null && Rand.Chance(IdleHoursMath.GiveWayLineChance)) IdleHours.Say(pawn, "giveway", who);
            EndJobWith(JobCondition.InterruptForced);
            return true;
        }

        /// <summary>What they ate, drank or smoked out of their own pack: the game's own taking of it (its food or its effect), one portion.</summary>
        private void Consume()
        {
            var def = job.thingDefToCarry;
            var inv = pawn.inventory?.innerContainer;
            if (def == null || inv == null) return;
            try
            {
                Thing have = null;
                foreach (var t in inv)
                    if (t?.def == def)
                    {
                        have = t;
                        break;
                    }
                if (have == null) return;
                var food = pawn.needs?.food;
                int count = 1;
                if (!def.IsDrug && food != null)
                    count = Mathf.Clamp(FoodUtility.WillIngestStackCountOf(pawn, def, have.GetStatValue(StatDefOf.Nutrition)), 1, have.stackCount);
                var taken = inv.Take(have, Mathf.Min(count, have.stackCount));
                if (taken == null) return;
                float nutrition = taken.Ingested(pawn, def.IsDrug ? 0f : food?.NutritionWanted ?? 0f);
                if (food != null && !pawn.Dead) food.CurLevel += nutrition;
                // A portion not finished goes back into the pack, never onto the colony's ground.
                if (!taken.Destroyed && taken.stackCount > 0) inv.TryAdd(taken);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a visitor eats from their pack", e);
            }
        }
    }

    /// <summary>A stint still holds (IdleHours.Still), looked at once a second rather than every tick.</summary>
    public sealed class IdleCheck
    {
        private int at = -1;
        private bool holds = true;

        public bool Holds(Pawn p, IdleDoing d, Job job)
        {
            int now = Find.TickManager.TicksGame;
            if (at < 0 || now - at >= 60 || now < at)
            {
                at = now;
                holds = IdleHours.Still(p, d, job);
            }
            return holds;
        }
    }

    /// <summary>A nap in the shade: the game's own lying down on the ground under a tree, for an hour or so (the job's expiry), in words of its own.</summary>
    public class JobDriver_VisitNap : JobDriver_LayDown
    {
        [Unsaved] private readonly IdleCheck check = new IdleCheck();

        public override string GetReport() => (asleep ? "CC_Idle_Report_Nap" : "CC_Idle_Report_Rest").Translate().Resolve();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !check.Holds(pawn, IdleDoing.Nap, job));
            AddFinishAction(_ => IdleHours.Ended(pawn));
            foreach (var t in base.MakeNewToils()) yield return t;
        }
    }

    /// <summary>A child of theirs at play: running from place to place near their people, stopping a moment at each.</summary>
    public class JobDriver_VisitIdlePlay : JobDriver
    {
        [Unsaved] private readonly IdleCheck check = new IdleCheck();

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !check.Holds(pawn, IdleDoing.Play, job));
            AddFinishAction(_ => IdleHours.Ended(pawn));
            var first = ToilMaker.MakeToil("VisitPlayBegin");
            first.initAction = () =>
            {
                if (Rand.Chance(IdleHoursMath.LineChance(IdleDoing.Play))) IdleHours.Say(pawn, IdleHoursMath.Moment(IdleDoing.Play, false));
            };
            first.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return first;
            var next = Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.A);
            yield return next;
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var pause = Toils_General.Wait(Rand.RangeInclusive(60, 200));
            pause.socialMode = RandomSocialMode.SuperActive;
            yield return pause;
            yield return Toils_Jump.JumpIfHaveTargetInQueue(TargetIndex.A, next);
        }
    }

    /// <summary>
    /// A visitor at the colony about to wander near their spot for want of anything to do may do something of their own
    /// instead (IdleHours.JobFor). Only ever in place of that idle wander, and after the market's turn
    /// (Patch_VisitorsShop): fighting, eating, sleeping and whatever their duty has for them come first, and their duty
    /// itself is left as it is. The job keeps the lord's own lord, source and duty tag.
    /// </summary>
    [HarmonyPatch(typeof(ThinkNode_Duty), nameof(ThinkNode_Duty.TryIssueJobPackage))]
    public static class Patch_VisitorsIdle
    {
        [HarmonyPriority(Priority.Low)]
        public static void Postfix(Pawn pawn, ref ThinkResult __result)
        {
            var job = __result.Job;
            if (job == null || job.def != JobDefOf.GotoWander && job.def != JobDefOf.Wait_Wander) return;
            if (pawn?.Faction == null || pawn.Faction.IsPlayer || !IdleHours.On) return;
            try
            {
                var idle = IdleHours.JobFor(pawn);
                if (idle == null) return;
                idle.lord = job.lord;
                idle.source = job.source;
                idle.dutyTag = job.dutyTag;
                __result = new ThinkResult(idle, __result.SourceNode, __result.Tag, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a visitor's idle hour", e);
            }
        }
    }
}
