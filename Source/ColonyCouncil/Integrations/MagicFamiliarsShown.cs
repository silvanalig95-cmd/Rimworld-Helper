using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A mage's familiar shown where the player looks (the player, 28 November 2026, asked where the bond can be seen and
    /// then "yes, please" to a lasting label): the animal's selection panel says whose familiar it is ("Familiar of Myst"),
    /// and the mage's colonist card names their familiar ("Familiar: Mittens (cat)"), whether or not the two are near each
    /// other. Only while familiars are on (Integrations/MagicFamiliars.cs) and the bond stands.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class MagicFamiliarsShown
    {
        static MagicFamiliarsShown()
        {
            CardSections.RegisterLines(CardKind.Colonist, CardSlot.Now, 38f, ColonistLines, null, "familiar-colonist");
        }

        /// <summary>The mage's card: their familiar, by name and kind.</summary>
        private static List<string> ColonistLines(ThingRef r)
        {
            if (!MagicFamiliars.On) return null;
            var mage = ThingRefs.Pawn(r);
            var f = mage == null ? null : AnimalPowersBook.Instance?.FamiliarOf(mage);
            if (f?.animal == null || f.animal.Dead || f.animal.Destroyed) return null;
            return new List<string> { "CC_Fam_CardLine".Translate(f.animal.LabelShort.Named("NAME"), AnimalFacts.KindLabel(f.animal).Named("KIND")).Resolve() };
        }

        /// <summary>The animal's selection panel: whose familiar it is.</summary>
        public static string InspectLine(Pawn a)
        {
            if (a?.RaceProps == null || !a.RaceProps.Animal || !MagicFamiliars.On) return null;
            var f = AnimalPowersBook.Instance?.BondOfAnimal(a);
            if (f?.mage == null || f.mage.Dead) return null;
            return "CC_Fam_InspectLine".Translate(f.mage.LabelShort.Named("MAGE")).Resolve();
        }
    }

    /// <summary>An animal bound as a mage's familiar says so in its selection panel. Off with the deeper game hooks.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
    public static class Patch_FamiliarInspect
    {
        public static void Postfix(Pawn __instance, ref string __result)
        {
            if (!RimfolkPatches.On || __instance?.RaceProps == null || !__instance.RaceProps.Animal) return;
            try
            {
                string line = MagicFamiliarsShown.InspectLine(__instance);
                if (line.NullOrEmpty()) return;
                __result = __result.NullOrEmpty() ? line : __result.TrimEndNewlines() + "\n" + line;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a familiar's inspect line", e);
            }
        }
    }
}
