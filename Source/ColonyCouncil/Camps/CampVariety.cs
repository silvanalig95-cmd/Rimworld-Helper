using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The content update's camps (docs/content-update-proposals.md, part B; Defs/CampDefs/Rimfolk_CampKinds_Variety4.xml):
    // the ways of the beekeepers (B20), the mushroom pickers (B21) and the star-watchers. The other new kinds need no code.

    /// <summary>
    /// Beekeepers on the move (B20): they come only with Alpha Bees, whose beehouse is their hive set at the edge of the
    /// camp (a kind's own piece, built and taken down again by them), in spring and summer. Their honey, wax and mead are
    /// Alpha Bees' (or VFE Medieval 2's honey and mead), sold at their stall and on their asks; their queen only once the
    /// colony has learned to keep bees itself (Alpha Bees' basic beekeeping).
    /// </summary>
    public class CampWorker_Beekeepers : CampWorker
    {
        public const string Hive = "RB_Beehouse", Research = "RB_BasicBeekeeping";

        public static ThingDef HiveDef => DefDatabase<ThingDef>.GetNamedSilentFail(Hive);

        public override bool FitsMap(Map map) => HiveDef != null;

        /// <summary>One of the camp's hives standing, nearest the colonist, or null.</summary>
        public static Building HiveOf(Camp c, Pawn near = null)
        {
            if (c?.plans == null || c.map == null) return null;
            var hives = c.plans.Where(p => p != null && p.role == "spot").Select(p => p.Built(c.map, c.faction)).Where(b => b != null && b.Spawned).ToList();
            if (hives.Count == 0) return null;
            return near == null ? hives[0] : hives.OrderBy(b => b.Position.DistanceToSquared(near.Position)).First();
        }

        /// <summary>The colony has learned to keep bees (Alpha Bees' basic beekeeping, finished).</summary>
        public static bool ColonyKeepsBees()
        {
            var r = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(Research);
            return r != null && r.IsFinished;
        }
    }

    /// <summary>
    /// A colonist (A) helps at the beekeepers' hives for a couple of hours: beside one of their beehouses, learning how
    /// they handle it (Animals). A sting now and then is the game's own bee-sting ailment's, by its rule at any beehouse
    /// (Ailments/AilmentChecks.cs, the Beekeeping trigger: the job is at the hive).
    /// </summary>
    public class DilemmaEffect_HelpAtHives : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c == null || CampWorker_Beekeepers.HiveOf(c) == null) return "CC_CampVariety_NoHive".Translate().Resolve();
            if (ctx?.a == null || ctx.a.WorkTypeIsDisabled(WorkTypeDefOf.Handling)) return "CC_CampVariety_NoHandler".Translate().Resolve();
            return null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.a == null || CampOnMap.For(ctx) == null) return;
            sheet.Play("CC_CampVariety_Hives_Impact".Translate(ctx.a.LabelShort).Resolve(), "CC_CampVariety_Hives_ImpactBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            var a = ctx?.a;
            var hive = CampWorker_Beekeepers.HiveOf(c, a);
            var job = DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_HelpAtHives");
            if (hive == null || a == null || job == null || !a.Spawned || a.Downed) return;
            try
            {
                a.jobs.TryTakeOrderedJob(JobMaker.MakeJob(job, hive), JobTag.Misc);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("beekeepers: helping at the hives", e);
            }
        }
    }

    /// <summary>The colonist beside a beehouse for a couple of hours (a lift of the frames, the smoke), learning Animals.</summary>
    public class JobDriver_HelpAtHives : JobDriver
    {
        public const int Ticks = 2 * 2500;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var work = ToilMaker.MakeToil("HelpAtHives");
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Ticks;
            work.handlingFacing = true;
            work.tickAction = () =>
            {
                pawn.rotationTracker.FaceTarget(job.targetA);
                pawn.skills?.Learn(SkillDefOf.Animals, 0.06f);
            };
            work.WithProgressBarToilDelay(TargetIndex.A);
            yield return work;
        }
    }

    /// <summary>Goods one of the camp carries to the colony's gate: the first of <see cref="things"/> that exists (a mod's, by name).</summary>
    public class DilemmaEffect_CampBringsAny : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public List<string> things = new List<string>();
        public int count;

        protected virtual ThingDef Thing(DilemmaContext ctx) => things?.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(d => d != null);

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var d = Thing(ctx);
            if (CampOnMap.For(ctx) == null || d == null || count <= 0) return;
            sheet.Goods(1, d, count);
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c == null || Thing(ctx) == null) return "CC_CampAsk_NotNow".Translate().ToString();
            return c.CanBring ? null : "CC_CampErrand_No_gate".Translate().ToString();
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            var d = Thing(ctx);
            if (c == null || d == null || count <= 0) return;
            try
            {
                // apart: an answer to the camp; one of their people carries the goods over themselves.
                if (!c.StartBring(d, count)) c.GiveGoods(d.defName, count, d.defName);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp brings " + d.defName, e);
            }
        }
    }

    /// <summary>
    /// A young queen of the beekeepers' (Alpha Bees'), carried to the gate in her box: a woodland queen to a home among
    /// trees, else a temperate one. Only once the colony keeps bees itself.
    /// </summary>
    public class DilemmaEffect_BeeQueen : DilemmaEffect_CampBringsAny
    {
        public const string Temperate = "RB_Bee_Temperate_Queen", Woodland = "RB_Bee_Woodland_Queen";

        protected override ThingDef Thing(DilemmaContext ctx)
        {
            var map = ctx?.map ?? CampOnMap.For(ctx)?.map;
            bool woods = map != null && Lands.Fits(map, new List<string> { "forest", "boreal", "jungle" });
            return DefDatabase<ThingDef>.GetNamedSilentFail(woods ? Woodland : Temperate) ?? DefDatabase<ThingDef>.GetNamedSilentFail(Temperate);
        }

        public override string Unavailable(DilemmaContext ctx) =>
            !CampWorker_Beekeepers.ColonyKeepsBees() ? "CC_CampVariety_NoBeekeeping".Translate().Resolve() : base.Unavailable(ctx);

        public override void Apply(DilemmaContext ctx)
        {
            count = 1;
            base.Apply(ctx);
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            count = 1;
            base.Describe(ctx, sheet);
        }
    }

    /// <summary>
    /// Mushroom and truffle pickers (B21): they pitch by the woods (the kind's need) in the fall, or in any season within a
    /// few days of the colony's own mushrooms after rain; they gather the land's wild mushrooms first (CampKindDef.gathers:
    /// Vanilla Plants Expanded - Mushrooms' caps, the caves' agarilux and bryolux), else what grows wild. Once in a stay, now
    /// and then, one of them eats a cap that looked right and wasn't: the game's food poisoning, on them, told, and the camp
    /// asks for the colony's doctor (their ask "badcap", the camps' own tend at their fire).
    /// </summary>
    public class CampWorker_MushroomPickers : CampWorker
    {
        public const string Flush = "Council_Evt_MushroomFlush", BadCap = "badcap";

        /// <summary>The colony's own mushrooms after rain came this lately (game days).</summary>
        public const float AfterFlushDays = 6f;

        public override bool FitsMap(Map map)
        {
            if (map == null) return false;
            var season = GenLocalDate.Season(map);
            if (season == Season.Fall) return true;
            try
            {
                var flush = DefDatabase<IncidentDef>.GetNamedSilentFail(Flush);
                var last = map.StoryState?.lastFireTicks;
                if (flush != null && last != null && last.TryGetValue(flush, out int t))
                    return VarietyMath.AfterFlush((Find.TickManager.TicksGame - t) / (float)GenDate.TicksPerDay, AfterFlushDays);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("mushroom pickers: after the rain", e);
            }
            return false;
        }

        public override string AskBlocked(Camp c, CampAsk a)
        {
            if (a?.key != BadCap) return null;
            return c?.sick == null || c.sick.Dead || !c.sick.Spawned ? "CC_CampTrouble_NobodySick".Translate().Resolve() : null;
        }

        private class State : CampWorkState
        {
            public bool rolled;
            public int due = -1;

            public override void ExposeData()
            {
                Scribe_Values.Look(ref rolled, "rolled");
                Scribe_Values.Look(ref due, "due", -1);
            }
        }

        public override void Started(Camp c)
        {
            if (c != null && !(c.workState is State)) c.workState = new State();
        }

        public override void Hourly(Camp c, int hour)
        {
            if (c?.map == null || !c.Settled || c.Hostile || c.sick != null) return;
            if (!(c.workState is State s)) c.workState = s = new State();
            int now = Find.TickManager.TicksGame;
            if (!s.rolled)
            {
                // Rolled once a stay: one stay in four, on one of its evenings.
                s.rolled = true;
                if (Rand.Chance(VarietyMath.BadCapChance)) s.due = now + Mathf.RoundToInt(Rand.Range(0.5f, 2f) * GenDate.TicksPerDay);
                return;
            }
            if (s.due < 0 || now < s.due || hour < 16 || hour > 21) return;
            s.due = -1;
            var poison = DefDatabase<HediffDef>.GetNamedSilentFail("FoodPoisoning");
            var who = c.Here.Where(p => p != c.leader && p.RaceProps.Humanlike && !p.Downed && !p.DevelopmentalStage.Juvenile() && p.health != null).RandomElementWithFallback();
            if (poison == null || who == null) return;
            try
            {
                var h = HediffMaker.MakeHediff(poison, who);
                h.Severity = 0.3f;
                who.health.AddHediff(h);
                c.sick = who;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("mushroom pickers: a bad cap", e);
                return;
            }
            // apart: told as it happens on the map; the camp asks for the colony's doctor by letter.
            Tell.Message(NewsKind.World, Importance.Notable, c.map, "CC_CampVariety_BadCap".Translate(who.LabelShort, c.Label).Resolve(), new LookTargets(who),
                MessageTypeDefOf.NeutralEvent);
            var ask = c.AskByKey(BadCap);
            if (ask != null && ColonyCouncil.Tell.CanAsk) c.Request(ask);
        }
    }

    /// <summary>
    /// Star-watchers (come for falling stars or a comet, or a dark sky): by night they lie out under the open sky near
    /// their fire, watching it (their own job), and sleep by day.
    /// </summary>
    public class CampWorker_StarWatchers : CampWorker
    {
        public override Job NightJob(Camp c, Pawn p, int hour)
        {
            if (c?.map == null || p == null || !VarietyMath.StarHour(hour)) return null;
            var job = DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_CampWatchSky");
            if (job == null) return null;
            var map = c.map;
            var from = c.FireCell.IsValid ? c.FireCell : c.center;
            if (!from.IsValid) return null;
            if (!CellFinder.TryFindRandomCellNear(from, map, 8, x => x.Standable(map) && !x.Roofed(map) && x.GetFirstPawn(map) == null && p.CanReach(x, PathEndMode.OnCell, Danger.Some), out var cell))
                return null;
            return JobMaker.MakeJob(job, cell);
        }
    }
}
