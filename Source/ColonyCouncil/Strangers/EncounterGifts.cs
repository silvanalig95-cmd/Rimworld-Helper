using System;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A small carving a stranger makes and hands over (an encounter's words): a wooden piece of the quality the words
    /// say, signed by them (X), set down at A's feet as the colony's now. A carving of their people's age
    /// (Things/ThingRoles.cs: a small sculpture, or a mod's small statue or pottery), the same in the words and in A's
    /// hands. Nothing when there is none wood can make.
    /// </summary>
    public class DilemmaEffect_Carving : DilemmaEffect
    {
        public override bool ChanceEach => false;

        /// <summary>The carving for this encounter: of wood, of the stranger's people's age.</summary>
        public static ThingDef Pick(DilemmaContext ctx)
        {
            var wood = ThingDefOf.WoodLog;
            if (ctx == null || wood?.stuffProps == null) return null;
            return ThingRoles.From(ThingRole.Carving, Couriers.PeopleOf(ctx), ctx.map, ThingRoles.Seed(ctx, "carving"), d => d.Minifiable && wood.stuffProps.CanMake(d));
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var carving = Pick(ctx);
            if (ctx?.a == null || !ctx.a.Spawned || carving == null) return;
            sheet.Add(ImpactKind.Goods, 1, "CC_Impact_Carving".Translate(carving.label, quality.GetLabel()).Resolve(), "CC_Impact_MadeBrief".Translate(carving.label).Resolve(), "carving");
        }

        public QualityCategory quality = QualityCategory.Poor;

        public override void Apply(DilemmaContext ctx)
        {
            var at = ctx?.a != null && ctx.a.Spawned ? ctx.a : null;
            var carving = Pick(ctx);
            var wood = ThingDefOf.WoodLog;
            if (at == null || carving == null) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            try
            {
                var art = ThingMaker.MakeThing(carving, wood);
                // together: X made it and hands it to A.
                if (ctx.stranger != null) art.TryGetComp<CompArt>()?.JustCreatedBy(ctx.stranger);
                art.TryGetComp<CompQuality>()?.SetQuality(quality, ArtGenerationContext.Outsider);
                GenPlace.TryPlaceThing(MinifyUtility.MakeMinified(art), at.Position, at.Map, ThingPlaceMode.Near);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a stranger's carving", e);
            }
        }
    }
}
