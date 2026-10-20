using System;
using HarmonyLib;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// While the hover card shows for a colonist, the game's own tooltip for that colonist ("Penguin, Taster / Steel
    /// hatchet / Healthy") stays away, so it doesn't draw over the card. The game asks a pawn for that tooltip
    /// (Pawn.GetTooltip) only for the thing under the mouse, from TooltipGiverList.DispenseAllThingTooltips, on repaint;
    /// the postfix empties it for the one pawn the card was drawn for this frame or the last, and never for anyone else
    /// or when the card isn't up (the hover card's "Hide the game's tooltip for that pawn", on by default). Applied on its
    /// own through the patch bootstrap: if it can't be, both tooltips show as before.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetTooltip))]
    public static class Patch_HudGameTooltip
    {
        private static bool broken;

        public static void Postfix(Pawn __instance, ref TipSignal __result)
        {
            if (broken || !RimfolkPatches.On) return;
            try
            {
                if (!HudHoverCard.ShowingFor(__instance)) return;
                // No text, and a wait it never reaches, in case something adds a line to it on the way (the shot report).
                __result = new TipSignal(string.Empty, __result.uniqueId) { delay = 9999f };
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("hover card: hiding the game's tooltip", e);
            }
        }
    }
}
