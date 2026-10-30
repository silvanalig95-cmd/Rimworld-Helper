using System;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A small carving a stranger makes and hands over (an encounter's words): a wooden sculpture of the quality the words
    /// say, signed by them (X), set down at A's feet as the colony's now. Nothing when the game has no small sculpture or
    /// wood can't make one.
    /// </summary>
    public class DilemmaEffect_Carving : DilemmaEffect
    {
        public QualityCategory quality = QualityCategory.Poor;

        public override void Apply(DilemmaContext ctx)
        {
            var at = ctx?.a != null && ctx.a.Spawned ? ctx.a : null;
            var carving = DefDatabase<ThingDef>.GetNamedSilentFail("SculptureSmall");
            var wood = ThingDefOf.WoodLog;
            if (at == null || carving == null || !carving.Minifiable || wood?.stuffProps == null || !wood.stuffProps.CanMake(carving)) return;
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
