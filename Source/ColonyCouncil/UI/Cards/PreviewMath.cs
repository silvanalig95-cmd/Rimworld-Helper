using System;

namespace ColonyCouncil
{
    /// <summary>A rect on the screen in the interface's units, free of game types (a preview, the link it hangs from).</summary>
    public readonly struct ScreenBox
    {
        public readonly float x, y, w, h;

        public ScreenBox(float x, float y, float w, float h)
        {
            this.x = x;
            this.y = y;
            this.w = w;
            this.h = h;
        }

        public float XMax => x + w;
        public float YMax => y + h;

        public bool IsEmpty => w <= 0f || h <= 0f;

        public bool Contains(float px, float py) => px >= x && px < x + w && py >= y && py < y + h;

        public bool Overlaps(ScreenBox o) => !IsEmpty && !o.IsEmpty && x < o.XMax && o.x < XMax && y < o.YMax && o.y < YMax;

        public ScreenBox WithHeight(float height) => new ScreenBox(x, y, w, height);

        public ScreenBox WithY(float top) => new ScreenBox(x, top, w, h);
    }

    /// <summary>
    /// Where a name's preview goes (docs/design/interface.md, "Previews"): the first under the link where there is room,
    /// else above it; the second, from a link inside the first, beside the first (right where there is room, else left),
    /// level with that link, so the two never cover each other; under the link only when neither side has room. Never off
    /// the screen. Pure: Tools/Tests checks it.
    /// </summary>
    public static class PreviewMath
    {
        /// <summary>Two levels at most: a link inside the second opens its card on a click, and shows no preview.</summary>
        public const int Levels = 2;
        public const float Width = 286f;
        /// <summary>How long a preview stays after the mouse left it and its link.</summary>
        public const float Stay = 0.5f;
        public const float Gap = 6f, Edge = 4f;

        /// <summary>The first preview, for a link at <paramref name="anchor"/>: under it where there is room, else above.</summary>
        public static ScreenBox Place(ScreenBox anchor, float height, float screenW, float screenH)
        {
            float x = Clamp(anchor.x - 12f, Edge, screenW - Width - Edge);
            float y = anchor.YMax + Gap;
            if (y + height > screenH - Edge) y = anchor.y - Gap - height;
            if (y < Edge) y = Math.Max(Edge, screenH - Edge - height);
            return new ScreenBox(x, y, Width, height);
        }

        /// <summary>
        /// The second preview, for a link at <paramref name="anchor"/> inside the first (<paramref name="parent"/>): beside
        /// the first, level with the link, right where there is room, else left; under the link when neither side has room.
        /// </summary>
        public static ScreenBox Beside(ScreenBox parent, ScreenBox anchor, float height, float screenW, float screenH)
        {
            float x;
            if (parent.XMax + Gap + Width <= screenW - Edge) x = parent.XMax + Gap;
            else if (parent.x - Gap - Width >= Edge) x = parent.x - Gap - Width;
            else return Place(anchor, height, screenW, screenH);
            float y = Clamp(anchor.y - 10f, Edge, Math.Max(Edge, screenH - Edge - height));
            return new ScreenBox(x, y, Width, height);
        }

        /// <summary>A preview whose height changed, kept on the screen.</summary>
        public static ScreenBox Fit(ScreenBox b, float height, float screenH)
        {
            var r = b.WithHeight(height);
            if (r.YMax > screenH - Edge) r = r.WithY(Math.Max(Edge, screenH - Edge - height));
            return r;
        }

        private static float Clamp(float v, float min, float max) => max < min ? min : v < min ? min : v > max ? max : v;
    }

    /// <summary>
    /// The previews showing and the ones about to (UI/Cards/Previews.cs draws them): one per level, two levels at most. A
    /// link drawn in Rimfolk's windows or the game's panes asks for the first level; a link drawn inside the first preview
    /// asks for the second; a link inside the second asks for nothing (a click still opens its card). A preview shows after
    /// the mouse rested on its link for the pause, stays while the mouse is on its link or inside it and half a second
    /// after; the first stays as long as the second does, and a new first takes the second away. Nothing shows while
    /// dragging or scrolling (<c>busy</c>). Pure: it is told the time, the frame and where the mouse is.
    /// </summary>
    public sealed class PreviewStack
    {
        private readonly ThingRef[] shown = new ThingRef[PreviewMath.Levels + 1];
        private readonly ScreenBox[] rects = new ScreenBox[PreviewMath.Levels + 1];
        private readonly float[] lastIn = new float[PreviewMath.Levels + 1];
        private readonly ThingRef[] pending = new ThingRef[PreviewMath.Levels + 1];
        private readonly float[] pendingSince = new float[PreviewMath.Levels + 1];
        private readonly ScreenBox[] anchors = new ScreenBox[PreviewMath.Levels + 1];
        private readonly int[] hoverFrame = { -10, -10, -10 };
        /// <summary>A name whose card was just opened from it: no preview again until the mouse leaves it.</summary>
        private readonly ThingRef[] quiet = new ThingRef[PreviewMath.Levels + 1];

        /// <summary>The thing previewed at a level (1 or 2), or none.</summary>
        public ThingRef Shown(int level) => level >= 1 && level <= PreviewMath.Levels ? shown[level] : ThingRef.None;

        public ScreenBox RectOf(int level) => level >= 1 && level <= PreviewMath.Levels ? rects[level] : default;

        public bool Any => !shown[1].IsNone;

        /// <summary>The mouse is inside a preview showing.</summary>
        public bool Inside(float mx, float my)
        {
            for (int l = 1; l <= PreviewMath.Levels; l++)
                if (!shown[l].IsNone && rects[l].Contains(mx, my))
                    return true;
            return false;
        }

        /// <summary>
        /// The mouse is on a link to <paramref name="r"/> at <paramref name="anchor"/>, drawn at level <paramref name="from"/>
        /// (0 in a window or pane, 1 inside the first preview, 2 inside the second). Returns the level it asks for, or 0.
        /// </summary>
        public int Hover(int from, ThingRef r, ScreenBox anchor, float now, int frame)
        {
            int level = from + 1;
            if (r.IsNone || level < 1 || level > PreviewMath.Levels) return 0;
            // The second only from inside a first that shows.
            if (level == 2 && shown[1].IsNone) return 0;
            hoverFrame[level] = frame;
            if (r == quiet[level]) return level;
            if (r == shown[level])
            {
                lastIn[level] = now;
                return level;
            }
            if (r != pending[level])
            {
                pending[level] = r;
                pendingSince[level] = now;
            }
            anchors[level] = anchor;
            return level;
        }

        /// <summary>
        /// Once a frame before the windows draw: shows what waited long enough, keeps what the mouse is on, lets go of the
        /// rest. <paramref name="heightOf"/> gives a preview's height when it first shows.
        /// </summary>
        public void Tick(float now, int frame, bool busy, float delay, float mx, float my, float screenW, float screenH, Func<ThingRef, float> heightOf)
        {
            for (int l = 1; l <= PreviewMath.Levels; l++)
            {
                // The links were drawn last frame (the windows draw after this).
                if (hoverFrame[l] >= frame - 2) continue;
                pending[l] = ThingRef.None;
                quiet[l] = ThingRef.None;
            }
            for (int l = 1; l <= PreviewMath.Levels; l++)
            {
                if (pending[l].IsNone || pending[l] == shown[l] || busy || now - pendingSince[l] < delay) continue;
                if (l == 2 && shown[1].IsNone) continue;
                float h = heightOf == null ? 120f : heightOf(pending[l]);
                shown[l] = pending[l];
                lastIn[l] = now;
                rects[l] = l == 1 ? PreviewMath.Place(anchors[l], h, screenW, screenH) : PreviewMath.Beside(rects[1], anchors[l], h, screenW, screenH);
                // A new first takes the second with it.
                if (l == 1) Drop(2);
            }
            // The second keeps the first: the mouse in either keeps both.
            if (!shown[2].IsNone && rects[2].Contains(mx, my))
            {
                lastIn[2] = now;
                lastIn[1] = now;
            }
            if (!shown[1].IsNone && rects[1].Contains(mx, my)) lastIn[1] = now;
            if (!shown[2].IsNone && now - lastIn[2] > PreviewMath.Stay) Drop(2);
            if (!shown[2].IsNone && lastIn[1] < lastIn[2]) lastIn[1] = lastIn[2];
            if (!shown[1].IsNone && now - lastIn[1] > PreviewMath.Stay)
            {
                Drop(1);
                Drop(2);
            }
        }

        /// <summary>A preview's height changed (its model did): kept on the screen.</summary>
        public void Fit(int level, float height, float screenH)
        {
            if (level < 1 || level > PreviewMath.Levels || shown[level].IsNone) return;
            rects[level] = PreviewMath.Fit(rects[level], height, screenH);
        }

        /// <summary>No preview now (a card opened, a window closed); none again for a name under the mouse until it leaves it.</summary>
        public void Hide()
        {
            for (int l = 1; l <= PreviewMath.Levels; l++)
            {
                if (!pending[l].IsNone) quiet[l] = pending[l];
                else if (!shown[l].IsNone) quiet[l] = shown[l];
                shown[l] = ThingRef.None;
                pending[l] = ThingRef.None;
            }
        }

        private void Drop(int level)
        {
            shown[level] = ThingRef.None;
            pending[level] = ThingRef.None;
        }
    }
}
