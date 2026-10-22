using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Where the game's own interface outside windows is drawn over the panels (they paint beneath it): the main buttons
    /// along the bottom, the colonist bar, the resource list (unless the inventory panel stands in for it), the game's
    /// alert list (unless the alerts panel does), the letters with the date, time and play settings below them, and,
    /// while the inspect pane is open, its row of tab buttons and the gizmos beside it. Worked out at most once a frame,
    /// and only while the mouse is over a panel. A panel whose window is under the mouse in one of these places steps
    /// aside (<see cref="HudPanel"/>), so the game's interface there gets the mouse: its highlight, its tooltip, its click.
    /// Anything that can't be read is left out (the panel then takes the mouse there, as before); one error stops it for
    /// the session.
    /// </summary>
    public static class HudGameUi
    {
        private static int frame = -1;
        private static readonly List<Rect> rects = new List<Rect>();
        private static bool broken, looked;
        private static AccessTools.FieldRef<MapInterface, ResourceReadout> readoutRef;
        private static AccessTools.FieldRef<ResourceReadout, float> readoutHeightRef;

        /// <summary>The main buttons' bar along the bottom of the screen.</summary>
        public const float MainButtonsHeight = 35f;

        /// <summary>Whether the game's own interface is drawn at <paramref name="p"/> (UI coordinates).</summary>
        public static bool Covers(Vector2 p)
        {
            if (broken) return false;
            if (frame != Time.frameCount)
            {
                frame = Time.frameCount;
                rects.Clear();
                try
                {
                    Gather();
                }
                catch (Exception e)
                {
                    broken = true;
                    rects.Clear();
                    CouncilLog.ErrorOnce("on-screen panels: where the game's interface is (off for this session)", e);
                }
            }
            for (int i = 0; i < rects.Count; i++)
                if (rects[i].Contains(p))
                    return true;
            return false;
        }

        private static void Add(HudBox b)
        {
            if (b.w > 0f && b.h > 0f) rects.Add(new Rect(b.x, b.y, b.w, b.h));
        }

        private static void Gather()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.UIRoot?.screenshotMode?.Active == true) return;
            float sw = UI.screenWidth, sh = UI.screenHeight;
            Add(new HudBox(0f, sh - MainButtonsHeight, sw, MainButtonsHeight));
            ColonistBar();
            // The letters, and under them the weather, date, time and play settings, against the right edge.
            float lettersTop = Find.LetterStack?.LastTopY ?? sh;
            Add(HudMath.RightColumn(sw, sh, lettersTop, GlobalControls.Width));
            if (!HudAlerts.HidesGameList())
            {
                float h = Find.Alerts?.AlertsHeight ?? 0f;
                if (h > 0f) Add(new HudBox(sw - Alert.Width, lettersTop - h, Alert.Width, h));
            }
            if (WorldRendererUtility.WorldSelected || Find.CurrentMap == null) return;
            ResourceList(sh);
            Inspect(sw, sh);
        }

        private static void ColonistBar()
        {
            var bar = Find.ColonistBar;
            if (bar == null || (Find.PlaySettings != null && !Find.PlaySettings.showColonistBar)) return;
            var locs = bar.DrawLocs;
            if (locs == null || locs.Count == 0) return;
            var size = bar.Size;
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var l in locs)
            {
                x0 = Mathf.Min(x0, l.x);
                y0 = Mathf.Min(y0, l.y);
                x1 = Mathf.Max(x1, l.x + size.x);
                y1 = Mathf.Max(y1, l.y + size.y);
            }
            // The names under the portraits.
            Add(new HudBox(x0 - 4f, y0 - 4f, x1 - x0 + 8f, y1 - y0 + 8f + 16f * bar.Scale));
        }

        /// <summary>The game's resource list down the left, while it's drawn (the inventory panel can stand in for it).</summary>
        private static void ResourceList(float sh)
        {
            if (Patch_HudGameResources.HidesGameList()) return;
            if (!looked)
            {
                looked = true;
                try
                {
                    readoutRef = AccessTools.FieldRefAccess<MapInterface, ResourceReadout>("resourceReadout");
                    readoutHeightRef = AccessTools.FieldRefAccess<ResourceReadout, float>("lastDrawnHeight");
                }
                catch (Exception)
                {
                    readoutRef = null;
                    readoutHeightRef = null;
                }
            }
            if (readoutRef == null || readoutHeightRef == null || Find.MapUI == null) return;
            var readout = readoutRef(Find.MapUI);
            if (readout == null) return;
            float h = readoutHeightRef(readout);
            if (h <= 0f) return;
            Add(new HudBox(0f, 0f, HudMath.ResourceListWidth, Mathf.Min(h + 12f, sh - 200f)));
        }

        /// <summary>The inspect pane's tab buttons over it and the gizmos beside it (the pane itself is a window).</summary>
        private static void Inspect(float sw, float sh)
        {
            if (Find.MainTabsRoot?.OpenTab != MainButtonDefOf.Inspect || !(MainButtonDefOf.Inspect.TabWindow is MainTabWindow_Inspect pane)) return;
            float width = InspectPaneUtility.PaneWidthFor(pane);
            int tabs = 0;
            var cur = pane.CurTabs;
            if (cur != null)
                foreach (var t in cur)
                    if (t != null && t.IsVisible && !t.Hidden)
                        tabs++;
            // The whole row of tabs over the pane, whoever draws them: the game's run right to left inside the pane's width,
            // other mods' (RimHUD's) start at the left edge and can be wider, and a taller pane than the game's moves them up.
            float top = pane.PaneTopY;
            var wr = pane.windowRect;
            if (wr.height > 1f && wr.y < top) top = wr.y;
            Add(HudMath.TabRowAll(width, top, tabs, InspectPaneUtility.TabHeight, sw - GlobalControls.Width));
            float gizmos = GizmoGridDrawer.HeightDrawnRecently;
            if (gizmos > 0f) Add(new HudBox(width, sh - MainButtonsHeight - gizmos - 6f, Mathf.Max(0f, sw - 147f - width), gizmos + 6f));
        }
    }
}
