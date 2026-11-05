using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// How the colony calls a defence pact in (Pacts.cs; docs/design/stage2-diplomacy.md, item 2), by its means: the caller
    /// at a powered comms console, when their people can answer one (a radio call, comms); else a signal fire laid and lit
    /// by the gate that their watchers see (the game's own campfire, carried out and lit as the bonfire play-out does; a
    /// fire on the hill, a beacon for their riders). Both are real jobs, done in the middle of the fight.
    /// </summary>
    public static class PactCalls
    {
        public enum Way
        {
            None,
            Comms,
            Fire
        }

        /// <summary>The way the colony can call them now, or None with why not.</summary>
        public static Way Means(Map home, Faction f, out string why)
        {
            why = null;
            if (home == null || f == null)
            {
                why = DipWords.P("CC_Dip_Why_NoHome");
                return Way.None;
            }
            if (NotableMath.Callable((int)f.def.techLevel) && NeighbourLetters.Console(home) != null && Caller(home, Way.Comms) != null) return Way.Comms;
            if (ThingDefOf.Campfire == null || !ThingDefOf.Campfire.IsResearchFinished)
            {
                why = PactWords.P("CC_Pact_Why_NoFireKnown");
                return Way.None;
            }
            if (home.resourceCounter.GetCount(ThingDefOf.WoodLog) < PlayFire.Cost)
            {
                why = PactWords.P("CC_Pact_Why_NoWood", PlayFire.Cost.Named("COUNT"));
                return Way.None;
            }
            if (Caller(home, Way.Fire) == null)
            {
                why = PactWords.P("CC_Pact_Why_NoCaller");
                return Way.None;
            }
            return Way.Fire;
        }

        /// <summary>Who makes the call: the one best with people who is up and about (not in a mental break, able to walk and work with their hands).</summary>
        public static Pawn Caller(Map home, Way way)
        {
            return home?.mapPawns.FreeColonistsSpawned
                .Where(p => p != null && !p.Dead && !p.Downed && !p.InMentalState && !p.DevelopmentalStage.Juvenile() && p.health.capacities.CapableOf(PawnCapacityDefOf.Moving)
                            && (way == Way.Comms ? p.health.capacities.CapableOf(PawnCapacityDefOf.Talking) : p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)))
                .OrderBy(p => p.Drafted ? 1 : 0).ThenByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).FirstOrDefault();
        }

        /// <summary>The call begins: the caller walks to the console, or someone carries wood out to lay a fire by the gate. The one making it, or null.</summary>
        public static Pawn Begin(Tie t, Map home, Faction f)
        {
            var way = Means(home, f, out _);
            var by = Caller(home, way);
            if (by == null) return null;
            Job job;
            if (way == Way.Comms)
            {
                var console = NeighbourLetters.Console(home, by);
                if (console == null || PactsDefOf.Rimfolk_PactCall == null) return null;
                job = JobMaker.MakeJob(PactsDefOf.Rimfolk_PactCall, console);
                job.count = t.id;
            }
            else
            {
                var cell = FireCell(home, by);
                var wood = Wood(home, by);
                if (!cell.IsValid || wood == null || PactsDefOf.Rimfolk_SignalFire == null) return null;
                job = JobMaker.MakeJob(PactsDefOf.Rimfolk_SignalFire, cell, wood);
                job.count = Mathf.Min(PlayFire.Cost, wood.stackCount);
            }
            if (by.Drafted) by.drafter.Drafted = false;
            // apart: the player's call; the caller goes to the console or out to the gate to lay the fire, on the map.
            by.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            if (by.CurJob != job) return null;
            // apart: told as the caller sets out; the call itself is made on the map.
            Tell.Message(NewsKind.World, Importance.Important, home, PactWords.P(way == Way.Comms ? "CC_Pact_Call_Comms" : "CC_Pact_Call_Fire", by.Named("PAWN"),
                PactWords.People(f).Named("PEOPLE"), (way == Way.Comms ? PactWords.CallByComms(home) : PactWords.CallByFire(home)).Named("MEANS")), new LookTargets(by),
                MessageTypeDefOf.NeutralEvent);
            return by;
        }

        /// <summary>Still at the call (walking to the console, at it, or laying the fire).</summary>
        public static bool AtIt(Pawn p) => p != null && p.Spawned && (p.CurJobDef == PactsDefOf.Rimfolk_PactCall || p.CurJobDef == PactsDefOf.Rimfolk_SignalFire);

        /// <summary>Wood from the colony's stores for the fire, nearest the one who carries it.</summary>
        private static Thing Wood(Map map, Pawn by)
        {
            var def = ThingDefOf.WoodLog;
            if (map.resourceCounter.GetCount(def) < PlayFire.Cost) return null;
            return map.listerThings.ThingsOfDef(def)
                .Where(t => t.Spawned && !t.IsForbidden(by) && (t.IsInAnyStorage() || map.areaManager.Home[t.Position]) && by.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Deadly))
                .OrderByDescending(t => t.stackCount >= PlayFire.Cost).ThenBy(t => t.Position.DistanceToSquared(by.Position)).FirstOrDefault();
        }

        /// <summary>Open ground by the gate where a campfire can go, under the sky where their watchers see it.</summary>
        public static IntVec3 FireCell(Map map, Pawn by)
        {
            var gate = DropCellFinder.TradeDropSpot(map);
            if (!gate.IsValid) return IntVec3.Invalid;
            foreach (var c in GenRadial.RadialCellsAround(gate, 9f, true))
            {
                if (!c.InBounds(map) || c.Roofed(map) || c.GetEdifice(map) != null || c.GetFirstItem(map) != null || (c.GetTerrain(map)?.IsWater ?? false)) continue;
                if (!c.Standable(map) || !by.CanReach(c, PathEndMode.Touch, Danger.Deadly)) continue;
                if (!GenConstruct.CanPlaceBlueprintAt(ThingDefOf.Campfire, c, Rot4.North, map).Accepted) continue;
                return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>The fire burns by the gate: every defence pact being called from this home has its answer.</summary>
        public static void FireLit(Map map, Pawn by)
        {
            foreach (var t in Pacts.Live())
                if (t.terms.goods == Pacts.Calling && Pacts.HomeOf(t) == map)
                    Pacts.CallMade(t, map, by, true);
        }
    }

    /// <summary>The caller at the comms console (target A), calling a defence pact in: a while on the line, then the call is made.</summary>
    public class JobDriver_PactCall : JobDriver
    {
        private Tie Pact => Pacts.Get(job.count);

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        public override string GetReport()
        {
            var f = Pacts.PeopleOf(Pact);
            return f != null ? PactWords.P("CC_Pact_Report_Call", PactWords.People(f).Named("PEOPLE")) : base.GetReport();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Pact == null || Pact.terms.goods != Pacts.Calling || !(job.targetA.Thing is Building_CommsConsole c) || !c.CanUseCommsNow);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            var talk = ToilMaker.MakeToil("PactCall");
            talk.defaultCompleteMode = ToilCompleteMode.Delay;
            talk.defaultDuration = 450;
            talk.handlingFacing = true;
            talk.tickIntervalAction = delta => pawn.rotationTracker.FaceTarget(job.targetA);
            talk.WithProgressBarToilDelay(TargetIndex.A);
            yield return talk;
            var done = ToilMaker.MakeToil("PactCallMade");
            done.initAction = () =>
            {
                try
                {
                    var t = Pact;
                    if (t != null) Pacts.CallMade(t, pawn.Map, pawn, false);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("pacts: the call made", e);
                }
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }

    /// <summary>
    /// A signal fire for a defence pact (target A the cell by the gate, B the wood): to the wood and up with it, out to the
    /// gate, a while on their knees coaxing it alight, and the campfire burns where their watchers see it. Done in danger
    /// too (unlike a gathering's fire): it is a call for help.
    /// </summary>
    public class JobDriver_SignalFire : JobDriver
    {
        private bool lit;

        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            !job.GetTarget(TargetIndex.B).HasThing || pawn.Reserve(job.GetTarget(TargetIndex.B), job, 1, job.count, null, errorOnFailed);

        public override string GetReport() => PactWords.P("CC_Pact_Report_Fire");

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFinishAction(cond =>
            {
                try
                {
                    if (!lit && pawn.carryTracker?.CarriedThing != null && pawn.Spawned)
                        pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("pacts: the fire's laying ends", e);
                }
            });
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, false, false, true);
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.Touch);
            var lay = ToilMaker.MakeToil("SignalFire");
            lay.defaultCompleteMode = ToilCompleteMode.Delay;
            lay.defaultDuration = 400;
            lay.handlingFacing = true;
            lay.tickIntervalAction = delta =>
            {
                var cell = job.GetTarget(TargetIndex.A).Cell;
                pawn.rotationTracker.FaceCell(cell);
                if (Rand.Chance(0.05f * delta)) FleckMaker.ThrowMicroSparks(cell.ToVector3Shifted(), pawn.Map);
            };
            lay.WithProgressBarToilDelay(TargetIndex.A);
            yield return lay;
            var done = ToilMaker.MakeToil("SignalFireLit");
            done.initAction = () =>
            {
                try
                {
                    var fire = PlayFire.Light(pawn.Map, job.GetTarget(TargetIndex.A).Cell, pawn);
                    lit = fire != null;
                    if (!lit) return;
                    if (pawn.carryTracker?.CarriedThing != null) pawn.carryTracker.DestroyCarriedThing();
                    FleckMaker.ThrowSmoke(job.GetTarget(TargetIndex.A).Cell.ToVector3Shifted(), pawn.Map, 2.5f);
                    Outsiders.Say(pawn, "signalfire", null);
                    PactCalls.FireLit(pawn.Map, pawn);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("pacts: the fire lit", e);
                }
            };
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lit, "lit");
        }
    }
}
