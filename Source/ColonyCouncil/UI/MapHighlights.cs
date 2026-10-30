using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Something a page points at on the map: cells to outline, a second set in another colour, a line between two points, a label.</summary>
    public class MapHighlight
    {
        public string key;
        public Map map;
        public List<IntVec3> cells = new List<IntVec3>();
        public List<IntVec3> other = new List<IntVec3>();
        public Color color = Color.white;
        public Color otherColor = Color.white;
        public IntVec3 from = IntVec3.Invalid, to = IntVec3.Invalid;
        public SimpleColor lineColor = SimpleColor.White;
        public string label;
        public IntVec3 labelAt = IntVec3.Invalid;
        /// <summary>Hovered or chosen: drawn in full; the rest fainter, and without a label unless they ask for one.</summary>
        public bool bright;
        public bool labelAlways;
        public int frame;
    }

    /// <summary>
    /// What the council's pages point at on the map, drawn by <see cref="MapHighlightOverlay"/>: a page adds its
    /// highlights every frame it's drawn (a hovered row brightest), and they fade the moment it stops. Lines are the
    /// game's own (GenDraw), outlines the field edges it draws for zones, labels the ones it draws over things.
    /// </summary>
    public static class MapHighlights
    {
        private static readonly Dictionary<string, MapHighlight> all = new Dictionary<string, MapHighlight>();
        private static readonly List<string> stale = new List<string>();

        public static void Add(MapHighlight h)
        {
            if (h?.map == null || h.key == null) return;
            h.frame = Time.frameCount;
            all[h.key] = h;
        }

        /// <summary>A plain outline with a label.</summary>
        public static void Cells(string key, Map map, IEnumerable<IntVec3> cells, Color color, string label = null, IntVec3? labelAt = null, bool bright = true)
        {
            var h = new MapHighlight { key = key, map = map, color = color, label = label, bright = bright };
            foreach (var c in cells) if (c.IsValid && c.InBounds(map)) h.cells.Add(c);
            if (h.cells.Count == 0) return;
            h.labelAt = labelAt ?? h.cells[0];
            Add(h);
        }

        /// <summary>The highlights still current (added within the last couple of frames) on this map.</summary>
        public static IEnumerable<MapHighlight> Current(Map map)
        {
            int now = Time.frameCount;
            stale.Clear();
            foreach (var kv in all)
                if (now - kv.Value.frame > 2) stale.Add(kv.Key);
            foreach (var k in stale) all.Remove(k);
            foreach (var h in all.Values)
                if (h.map == map) yield return h;
        }
    }

    /// <summary>Draws the current highlights: outlines and lines in the map's update, labels over the map in its GUI pass.</summary>
    public class MapHighlightOverlay : MapComponent
    {
        public MapHighlightOverlay(Map map) : base(map) { }

        private static readonly List<MapHighlight> drawn = new List<MapHighlight>();

        public override void MapComponentUpdate()
        {
            if (map != Find.CurrentMap) return;
            BaseOverlay.Draw(map);
            drawn.Clear();
            drawn.AddRange(MapHighlights.Current(map));
            foreach (var h in drawn)
            {
                float a = h.bright ? 1f : 0.45f;
                if (h.cells.Count > 0) GenDraw.DrawFieldEdges(h.cells, new Color(h.color.r, h.color.g, h.color.b, a));
                if (h.other.Count > 0) GenDraw.DrawFieldEdges(h.other, new Color(h.otherColor.r, h.otherColor.g, h.otherColor.b, a));
                if (h.from.IsValid && h.to.IsValid && h.from.InBounds(map) && h.to.InBounds(map))
                    GenDraw.DrawLineBetween(h.from.ToVector3Shifted(), h.to.ToVector3Shifted(), h.lineColor, h.bright ? 0.3f : 0.15f);
            }
        }

        public override void MapComponentOnGUI()
        {
            if (map != Find.CurrentMap || Find.CameraDriver.CurrentZoom > CameraZoomRange.Far) return;
            foreach (var h in MapHighlights.Current(map))
            {
                if (h.label.NullOrEmpty() || !(h.bright || h.labelAlways) || !h.labelAt.IsValid || !h.labelAt.InBounds(map)) continue;
                var color = h.bright ? Color.white : new Color(h.color.r, h.color.g, h.color.b, 0.85f);
                GenMapUI.DrawThingLabel(h.labelAt.ToVector3Shifted().MapToUIPosition(), h.label, color);
            }
        }
    }
}
