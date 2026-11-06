using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // A Rimfolk event that grows out of what is on the map right now (Defs/IncidentDefs/Rimfolk_Events_Variety2.xml): a
    // herd of wild animals crossing the colony's land (the game's own herd migration). Take one, take a few, or let them
    // cross: the hunt is the game's own, and so is the risk that a hunted beast turns on its hunter, named before the
    // answer.

    // ===================== The herd crossing =====================

    /// <summary>
    /// A herd of one kind crossing the land together toward an edge (the game's herd migration: wild animals under one
    /// group making for the map's edge), at least <see cref="VarietyTwoMath.MinHerd"/> of them, while the colony has a
    /// hunter armed for it and no threat is on. A is the colony's best hunter, X the biggest grown one of the herd,
    /// {BEAST} the kind (plural), {COUNT} how many there are. Tags "big" (ten or more) for the lines.
    /// </summary>
    public class HappeningWorker_HerdCrossing : IncidentWorker_Happening
    {
        /// <summary>The largest herd of one kind crossing the land now (empty when none is big enough).</summary>
        public static List<Pawn> Herd(Map map)
        {
            if (map == null) return new List<Pawn>();
            var crossing = map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == null && p.RaceProps != null && p.RaceProps.Animal && !p.Dead && !p.Downed && !p.InMentalState
                                                                   && !p.Position.Fogged(map) && p.GetLord()?.LordJob is LordJob_ExitMapNear);
            var biggest = crossing.GroupBy(p => p.kindDef).OrderByDescending(g => g.Count()).FirstOrDefault()?.ToList() ?? new List<Pawn>();
            return VarietyTwoMath.HerdBigEnough(biggest.Count) ? biggest : new List<Pawn>();
        }

        /// <summary>The herd <paramref name="x"/> walks with (the same kind under the same group), still crossing.</summary>
        public static List<Pawn> HerdOf(Pawn x)
        {
            var lord = x?.GetLord();
            if (x == null || x.Dead || !x.Spawned || lord == null) return new List<Pawn>();
            return lord.ownedPawns.Where(p => p != null && p.Spawned && !p.Dead && !p.Downed && p.kindDef == x.kindDef).ToList();
        }

        /// <summary>Grown ones, the biggest first: what the hunters would go for.</summary>
        public static List<Pawn> Grown(IEnumerable<Pawn> herd) =>
            herd.Where(p => p.ageTracker != null && p.RaceProps.lifeStageAges != null && p.ageTracker.CurLifeStageIndex >= p.RaceProps.lifeStageAges.Count - 1)
                .OrderByDescending(p => p.BodySize).ToList();

        private static Pawn Hunter(Map map) =>
            Colonists(map).Where(p => GamePrey.CanHunt(p) && p.workSettings != null && p.workSettings.WorkIsActive(WorkTypeDefOf.Hunting))
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0).FirstOrDefault();

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            !MapFacts.ThreatOn(map) && Hunter(map) != null && Grown(Herd(map)).Count >= 2;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var herd = Herd(ctx.map);
            var grown = Grown(herd);
            ctx.a = Hunter(ctx.map);
            if (ctx.a == null || grown.Count < 2) return false;
            ctx.stranger = grown[0];
            ctx.count = herd.Count;
            ctx.thing = ctx.stranger.def;
            ctx.beast = ctx.stranger.kindDef.GetLabelPlural();
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.count >= 10) tags.Add("big");
        }
    }

    /// <summary>
    /// Grown ones of the herd X walks with marked for the colony's hunters (the game's own hunt designation): with
    /// <see cref="count"/> 1 just X, the biggest; with 0 a few of them (<see cref="VarietyTwoMath.FewOf"/>). Its line
    /// names the game's own risk that a hunted beast turns on its hunter, when the kind has it. Can't be chosen once the
    /// herd has gone.
    /// </summary>
    public class DilemmaEffect_HuntHerd : DilemmaEffect
    {
        public int count;

        public override bool ChanceEach => false;

        private List<Pawn> Marks(DilemmaContext ctx)
        {
            var x = ctx?.stranger;
            var herd = HappeningWorker_HerdCrossing.HerdOf(x);
            var map = ctx?.map;
            if (map == null || herd.Count == 0) return new List<Pawn>();
            var grown = HappeningWorker_HerdCrossing.Grown(herd).Where(p => map.designationManager.DesignationOn(p) == null).ToList();
            if (count == 1) return grown.Contains(x) ? new List<Pawn> { x } : grown.Take(1).ToList();
            return grown.Take(VarietyTwoMath.FewOf(herd.Count)).ToList();
        }

        public override string Unavailable(DilemmaContext ctx) =>
            ctx?.stranger != null && Marks(ctx).Count == 0 ? "CC_Evt_HerdGone".Translate().ToString() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var marks = Marks(ctx);
            if (marks.Count == 0) return;
            string kind = marks.Count == 1 ? marks[0].KindLabel : marks[0].kindDef.GetLabelPlural();
            sheet.Play((marks.Count == 1 ? "CC_Impact_HuntHerdOne" : "CC_Impact_HuntHerdFew").Translate(kind).Resolve(), "CC_Impact_HuntHerdBrief".Translate(kind).Resolve());
            float turn = 0f;
            try
            {
                turn = marks.Max(p => PawnUtility.GetManhunterOnDamageChance(p, ctx.a));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("herd hunt risk", e);
            }
            if (turn >= 0.01f)
                sheet.Add(ImpactKind.Health, -1, "CC_Impact_HuntHerdRisk".Translate(kind).Resolve(), "CC_Impact_HuntHerdRiskBrief".Translate().Resolve(), "huntrisk");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (map == null) return;
            try
            {
                foreach (var p in Marks(ctx)) map.designationManager.AddDesignation(new Designation(p, DesignationDefOf.Hunt));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("herd hunt", e);
            }
        }
    }
}
