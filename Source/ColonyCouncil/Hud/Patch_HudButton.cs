using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The on-screen panels' switch in the game's row of play settings (bottom right, beside the zones and beauty
    /// buttons): a click shows or hides the panels, a right-click opens their menu (each panel, the hover card, arrange,
    /// put back, every option). On the map only. Applied on its own through the patch bootstrap
    /// (Patches/RimfolkPatches.cs); if the game's method changes it's skipped with one warning, and the panels still
    /// answer to their key and the settings. With the deeper game hooks switched off it draws nothing.
    /// </summary>
    [HarmonyPatch(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))]
    public static class Patch_HudButton
    {
        private static bool broken;

        public static void Postfix(WidgetRow row, bool worldView)
        {
            if (broken || worldView || row == null || !RimfolkPatches.On) return;
            try
            {
                var s = ColonyCouncilMod.Settings;
                var tex = HudIcons.Panels ?? Icons.Overview;
                if (tex == null) return;
                var r = row.Icon(tex);
                bool on = s.hudShown;
                if (Mouse.IsOver(r)) Widgets.DrawHighlight(r);
                GUI.DrawTexture(new Rect(r.x + r.width / 2f, r.y, r.height / 2f, r.height / 2f), on ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex);
                MouseoverSounds.DoRegion(r, SoundDefOf.Mouseover_ButtonToggle);
                TooltipHandler.TipRegion(r, new TipSignal(HudManager.ButtonTip, 0x5A1E70));
                if (Widgets.ButtonInvisible(r, false))
                {
                    if (Event.current.button == 1) Find.WindowStack.Add(new FloatMenu(HudManager.Menu()));
                    else HudManager.Toggle();
                }
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("on-screen panels: play settings button", e);
            }
        }
    }
}
