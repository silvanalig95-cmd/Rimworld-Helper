using System;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Paints the on-screen panels as a background layer: beneath all of the game's own interface, above the map.
    ///
    /// The order this relies on, within one repaint of the game's interface (UIRoot_Play.UIRootOnGUI):
    /// MapInterface.MapInterfaceOnGUI_BeforeMainTabs draws the map's overlays first (ThingOverlays: the pawn labels and
    /// health bars), then every map component's MapComponentOnGUI (MapComponentUtility.MapComponentOnGUI: other mods' map
    /// labels and overlays, Rimfolk's defence labels), and only then the colonist bar, the drag box, the designators and
    /// targeter, the things' tooltips, the bottom-right controls with the letters, and the resource list; after it come
    /// the main buttons and the game's alert list, the tutor, and last the window stack (each window's ExtraOnGUI, where
    /// the inspect pane draws its tab buttons and gizmos, then every window). The panels are painted right after the map
    /// components (<see cref="Patch_HudPaint"/>, a postfix on MapComponentUtility.MapComponentOnGUI), so the map, its
    /// pawn labels and every map overlay are under them, and everything else the game draws is over them.
    ///
    /// The order is checked as the game runs: the resource list's own patch (<see cref="Patch_HudGameResources"/>) notes
    /// when it draws; if it ever drew before the panels in a frame, this version orders its interface differently, and the
    /// panels go back to painting in their windows for the session (one warning). The panels' windows also paint them
    /// whenever this pass didn't run in a frame: the hook couldn't be applied, the deeper game hooks are off, or the world
    /// map is up (the map components aren't drawn there). Screenshot mode paints nothing, as before.
    /// </summary>
    public static class HudPaint
    {
        private static bool broken;
        private static int gameUiFrame = -1;

        /// <summary>The panels are painted beneath the game's interface this session (else in their windows).</summary>
        public static bool Working => !broken && RimfolkPatches.On && RimfolkPatches.Applied.Contains(nameof(Patch_HudPaint));

        /// <summary>From the map components' OnGUI, every event; paints on repaint only.</summary>
        public static void Paint()
        {
            if (broken || !RimfolkPatches.On) return;
            var ev = Event.current;
            if (ev == null || ev.type != EventType.Repaint) return;
            try
            {
                if (gameUiFrame == Time.frameCount)
                {
                    broken = true;
                    Log.Warning("[Rimfolk] The game draws its resource list before the map's overlays in this version, so the on-screen panels paint in their own windows (over the game's interface) instead of beneath it.");
                    return;
                }
                if (Find.UIRoot?.screenshotMode?.FiltersCurrentEvent == true) return;
                var stack = Find.WindowStack;
                if (stack == null) return;
                // In the stack's order, so a panel above another stays above it.
                var list = stack.Windows;
                for (int i = 0; i < list.Count; i++)
                    if (list[i] is HudPanel p)
                        p.PaintEarly();
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("on-screen panels: painting beneath the game's interface (they paint in their windows for this session)", e);
            }
        }

        /// <summary>The game drew a part of its own map interface (the resource list): it must come after the panels.</summary>
        public static void SawGameInterface()
        {
            var ev = Event.current;
            if (ev != null && ev.type == EventType.Repaint) gameUiFrame = Time.frameCount;
        }
    }

    /// <summary>
    /// The early pass of the on-screen panels (<see cref="HudPaint"/>): right after every map component's OnGUI, before
    /// the colonist bar and the rest of the game's interface. Applied on its own through the patch bootstrap; if it can't
    /// be, the panels paint in their windows as before.
    /// </summary>
    [HarmonyPatch(typeof(MapComponentUtility), nameof(MapComponentUtility.MapComponentOnGUI))]
    public static class Patch_HudPaint
    {
        public static void Postfix() => HudPaint.Paint();
    }
}
