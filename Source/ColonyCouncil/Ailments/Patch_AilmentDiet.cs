using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A colonist ate something: noted for ailments (fruit and vegetables against scurvy, sweets for toothache). Only
    /// reads what was eaten. Without this hook (deeper game hooks off) the diet isn't known: scurvy never starts, and
    /// toothache only comes with the years.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]
    public static class Patch_AilmentDiet
    {
        public static void Postfix(Thing __instance, Pawn ingester)
        {
            if (!RimfolkPatches.On || ingester == null || __instance?.def == null || ingester.Faction != Faction.OfPlayer || !ingester.RaceProps.Humanlike) return;
            try
            {
                AilmentTracker.Instance?.Notify_Ate(ingester, __instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ailment meal hook", e);
            }
        }
    }

    /// <summary>
    /// Once the defs are loaded: the appendix operation gets the game's surgery outcomes, and a doctor who has just taken
    /// on a serious case is likelier to ask the council for a stocked infirmary (the wish's own conditions still apply:
    /// it only comes up when medicine is short).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class AilmentStartup
    {
        static AilmentStartup()
        {
            try
            {
                // The appendix operation fails the way the game's own operations do.
                var appendix = DefDatabase<RecipeDef>.GetNamedSilentFail("Council_Surgery_RemoveAppendix");
                if (appendix != null && appendix.surgeryOutcomeEffect == null)
                    appendix.surgeryOutcomeEffect = DefDatabase<RecipeDef>.GetNamedSilentFail("RemoveBodyPart")?.surgeryOutcomeEffect;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ailments: appendix operation", e);
            }
            try
            {
                var wish = DefDatabase<WishDef>.GetNamedSilentFail("Council_Wish_Infirmary");
                if (wish == null || AilmentDefOf.Council_Ail_HardCase == null) return;
                wish.triggers ??= new List<WishTrigger>();
                wish.triggers.Add(new WishTrigger
                {
                    key = "illness",
                    thoughts = new List<string> { AilmentDefOf.Council_Ail_HardCase.defName },
                    freshHours = 72,
                    boost = 4f
                });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ailments: infirmary wish", e);
            }
        }
    }
}
