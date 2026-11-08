using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The frame of Rimfolk's windows (docs/design/rimfolk-look.md, stage 1): a title bar of its own with the window's
    /// icon and title, whatever tools the window puts there, a button to make it large and back and a close button;
    /// dragged by its title bar, sized from its edges and corners down to the window's least, large (about 92% of the
    /// screen) with the button or a double-click on the title; its place and size remembered by its key in the mod's
    /// settings between openings and games, and fitted onto the screen it opens on. A window holds one and calls it from
    /// its own hooks (<see cref="FramedWindow"/> does it for a plain window; the region window's tab does it too, fixed in
    /// place). With "Rimfolk's own look" on, the frame and the contents are drawn in it (UiKit.BeginOwn); with "Rimfolk's
    /// windows move and resize" on, the window moves and sizes. With both off, the window is as it was before. The rules
    /// are WindowFrameMath's; the moving follows the mouse from the window's WindowOnGUI, outside the game's own window
    /// function, so the window never lags the mouse or loses it at its edge.
    /// </summary>
    public sealed class WindowFrame
    {
        public readonly string key;
        public readonly float minW, minH, bottom;

        private enum Mode
        {
            None,
            Move,
            Size
        }

        private Mode mode;
        private FrameEdge edges;
        private FrameRect startRect;
        private Vector2 startMouse;
        private FramePlace place;
        private bool known, changed;

        /// <param name="key">The window's name in the settings ("Region", "Trade").</param>
        /// <param name="minW">Its least width and height.</param>
        /// <param name="bottom">The bottom bar it keeps clear (<see cref="WindowFrameMath.BottomBar"/>), or 0 for a dialog that may cover it.</param>
        public WindowFrame(string key, float minW, float minH, float bottom)
        {
            this.key = key;
            this.minW = minW;
            this.minH = minH;
            this.bottom = bottom;
        }

        // ---------------- The settings ----------------

        /// <summary>"Rimfolk's own look": the frame and the contents in Rimfolk's style (on at first).</summary>
        public static bool OwnLookOn => ColonyCouncilMod.Settings?.ownLook ?? true;

        /// <summary>"Rimfolk's windows move and resize" (on at first).</summary>
        public static bool MovesOn => ColonyCouncilMod.Settings?.windowsMove ?? true;

        /// <summary>The frame is drawn at all: either setting on. With both off the window is as it was before Rimfolk's frame.</summary>
        public static bool Shown => OwnLookOn || MovesOn;

        /// <summary>Whether the window is large now.</summary>
        public bool Large => Load().large;

        // ---------------- Where it opens ----------------

        /// <summary>Whether this window has a place of its own yet (it was moved, sized or closed somewhere).</summary>
        public bool Known
        {
            get
            {
                Load();
                return known;
            }
        }

        /// <summary>
        /// Where the window opens on this screen: its remembered place fitted to the screen (large again at this screen's
        /// large size), or the first time <paramref name="first"/> (the window's own first place, kept on the screen) or
        /// <paramref name="want"/> in the middle of the screen.
        /// </summary>
        public Rect Opening(Vector2 want, Rect? first = null)
        {
            Load();
            FrameRect r = known
                ? WindowFrameMath.Opening(place, UI.screenWidth, UI.screenHeight, minW, minH, bottom)
                : first.HasValue
                    ? WindowFrameMath.Clamp(F(first.Value), UI.screenWidth, UI.screenHeight, minW, minH, bottom)
                    : WindowFrameMath.Centred(want.x, want.y, UI.screenWidth, UI.screenHeight, minW, minH, bottom);
            if (!known)
            {
                place.rect = r;
                place.large = false;
            }
            return R(r);
        }

        /// <summary>The window kept on the screen after the screen changed (another resolution or interface scale).</summary>
        public Rect Refit(Rect now)
        {
            Load();
            if (place.large) return R(WindowFrameMath.Large(UI.screenWidth, UI.screenHeight, minW, minH, bottom));
            return R(WindowFrameMath.Clamp(F(now), UI.screenWidth, UI.screenHeight, minW, minH, bottom));
        }

        // ---------------- Following the mouse ----------------

        /// <summary>
        /// From the window's WindowOnGUI, before the game draws it: while the title bar is held the window follows the
        /// mouse, while an edge is held it sizes; let go, and its place is remembered.
        /// </summary>
        public void Follow(Window w)
        {
            if (mode == Mode.None) return;
            if (!Input.GetMouseButton(0) || !MovesOn)
            {
                End(w);
                return;
            }
            var m = UI.MousePositionOnUIInverted;
            float dx = m.x - startMouse.x, dy = m.y - startMouse.y;
            var r = mode == Mode.Move
                ? WindowFrameMath.Move(startRect, dx, dy, UI.screenWidth, UI.screenHeight, minW, minH, bottom)
                : WindowFrameMath.Resize(startRect, edges, dx, dy, UI.screenWidth, UI.screenHeight, minW, minH, bottom);
            w.windowRect = R(r);
        }

        private void Begin(Window w, Mode m, FrameEdge e)
        {
            mode = m;
            edges = e;
            startRect = F(w.windowRect);
            startMouse = UI.MousePositionOnUIInverted;
        }

        private void End(Window w)
        {
            if (mode == Mode.None) return;
            var moved = F(w.windowRect);
            mode = Mode.None;
            if (moved.Near(startRect, 0.5f)) return;
            // Moved or sized by hand: no longer large; its place is where it was let go.
            Load();
            place.rect = moved;
            place.large = false;
            changed = true;
            Remember();
        }

        /// <summary>The button or a double-click on the title: large (about 92% of the screen) or back where it was.</summary>
        public void ToggleLarge(Window w)
        {
            Load();
            place.rect = F(w.windowRect);
            place = WindowFrameMath.Toggle(place, UI.screenWidth, UI.screenHeight, minW, minH, bottom);
            w.windowRect = R(place.rect);
            changed = true;
            Remember();
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }

        /// <summary>The window closed: its place kept (if it moved since it was last written).</summary>
        public void Closed(Window w)
        {
            if (mode != Mode.None) End(w);
            mode = Mode.None;
            if (!MovesOn) return;
            Load();
            if (!place.large) place.rect = F(w.windowRect);
            Remember();
        }

        // ---------------- Drawing ----------------

        private static string largeTip, backTip, closeTip;

        /// <summary>
        /// The frame in the window's own rect (<paramref name="inRect"/>, with the window's margin 0): its ground and edge in
        /// Rimfolk's own look, the title bar with the icon, the title, the window's tools (drawn by
        /// <paramref name="tools"/> in a rect <paramref name="toolsW"/> wide at the bar's right, before the window's own
        /// buttons), the large button where the window moves, and the close button; the presses that move and size the
        /// window. Returns the body under the title bar, inset from the edges.
        /// </summary>
        public Rect Draw(Window w, Rect inRect, string title, Texture2D icon, float toolsW = 0f, Action<Rect> tools = null, bool closable = true, bool movable = true)
        {
            bool moves = movable && MovesOn;
            bool ownLook = UiKit.OwnLook;
            var e = Event.current;
            // The edges first, so a press at the window's very edge sizes it before anything under it takes the press.
            var hover = moves && mode == Mode.None ? WindowFrameMath.EdgesAt(e.mousePosition.x - inRect.x, e.mousePosition.y - inRect.y, inRect.width, inRect.height) : FrameEdge.None;
            if (hover != FrameEdge.None && e.type == EventType.MouseDown && e.button == 0)
            {
                Begin(w, Mode.Size, hover);
                e.Use();
            }
            if (ownLook) UiKit.RoundBox(inRect, UiKit.WindowGround, UiKit.Edge, SkinMath.RadiusWindow);
            var bar = new Rect(inRect.x, inRect.y, inRect.width, WindowFrameMath.TitleH);
            float pad = ownLook ? 12f : 10f;
            float x = bar.xMax - pad;
            const float b = WindowFrameMath.TitleButton, gap = WindowFrameMath.TitleGap;
            float by = bar.y + (bar.height - b) / 2f;
            if (closable)
            {
                x -= b;
                closeTip ??= "CC_Frame_CloseTip".Translate().Resolve();
                if (FrameButton(new Rect(x, by, b, b), FrameGlyph.Close, closeTip, ownLook))
                {
                    w.Close();
                    e.Use();
                }
                x -= gap;
            }
            if (moves)
            {
                x -= b;
                bool large = Large;
                largeTip ??= "CC_Frame_LargeTip".Translate().Resolve();
                backTip ??= "CC_Frame_BackTip".Translate().Resolve();
                if (FrameButton(new Rect(x, by, b, b), large ? FrameGlyph.Back : FrameGlyph.Large, large ? backTip : largeTip, ownLook)) ToggleLarge(w);
                x -= gap;
            }
            if (tools != null && toolsW > 0f)
            {
                x -= 8f;
                if (ownLook) Widgets.DrawBoxSolid(new Rect(x + 3f, bar.y + 10f, 1f, bar.height - 20f), UiKit.Edge);
                float tw = Mathf.Min(toolsW, x - bar.x - 160f);
                if (tw > 40f)
                {
                    tools(new Rect(x - tw, bar.y, tw, bar.height));
                    x -= tw;
                }
            }
            // The icon and the title.
            float tx = bar.x + pad;
            if (icon != null)
            {
                UiKit.Icon(new Rect(tx, bar.center.y - 9f, 18f, 18f), icon, ownLook ? UiKit.Accent : UiKit.Gold);
                tx += 26f;
            }
            UiKit.LabelFit(new Rect(tx, bar.y, Mathf.Max(0f, x - tx - 8f), bar.height), title, UiKit.Ink);
            // The title bar held: the window follows the mouse; a double-click makes it large or back.
            if (moves && e.type == EventType.MouseDown && e.button == 0 && bar.Contains(e.mousePosition) && mode == Mode.None)
            {
                if (e.clickCount >= 2) ToggleLarge(w);
                else Begin(w, Mode.Move, FrameEdge.None);
                e.Use();
            }
            if (moves) Grips(inRect, hover, ownLook);
            // The body under the title bar, inset from the sides and the foot (WindowFrameMath).
            return new Rect(inRect.x + (ownLook ? WindowFrameMath.BodyInsetOwn : WindowFrameMath.BodyInsetGame), bar.yMax + (ownLook ? 0f : 4f),
                WindowFrameMath.BodyWidth(inRect.width, ownLook), WindowFrameMath.BodyHeight(inRect.height, ownLook));
        }

        /// <summary>
        /// The window's edges answer the mouse: the edge or corner under it lit while it would size the window (the game
        /// shows no other cursor), and the corner's grip always.
        /// </summary>
        private void Grips(Rect r, FrameEdge hover, bool ownLook)
        {
            if (Event.current.type != EventType.Repaint) return;
            var lit = mode == Mode.Size ? edges : hover;
            var c = ownLook ? UiKit.WithAlpha(UiKit.Accent, 0.8f) : UiKit.WithAlpha(UiKit.Ink, 0.5f);
            if ((lit & FrameEdge.Left) != 0) Widgets.DrawBoxSolid(new Rect(r.x + 1f, r.y + 10f, 2f, r.height - 20f), c);
            if ((lit & FrameEdge.Right) != 0) Widgets.DrawBoxSolid(new Rect(r.xMax - 3f, r.y + 10f, 2f, r.height - 20f), c);
            if ((lit & FrameEdge.Top) != 0) Widgets.DrawBoxSolid(new Rect(r.x + 10f, r.y + 1f, r.width - 20f, 2f), c);
            if ((lit & FrameEdge.Bottom) != 0) Widgets.DrawBoxSolid(new Rect(r.x + 10f, r.yMax - 3f, r.width - 20f, 2f), c);
            if (!ownLook)
            {
                if (TexUI.WinExpandWidget != null) UiKit.Icon(new Rect(r.xMax - 20f, r.yMax - 20f, 18f, 18f), TexUI.WinExpandWidget, UiKit.WithAlpha(UiKit.Ink, 0.6f));
                return;
            }
            // Rimfolk's grip: three short slants in the corner.
            var g = UiKit.WithAlpha(UiKit.Faint, lit == (FrameEdge.Right | FrameEdge.Bottom) ? 0.95f : 0.45f);
            for (int i = 0; i < 3; i++)
            {
                float o = 4f + i * 4f;
                Widgets.DrawLine(new Vector2(r.xMax - o - 2f, r.yMax - 3f), new Vector2(r.xMax - 3f, r.yMax - o - 2f), g, 1f);
            }
        }

        private enum FrameGlyph
        {
            Close,
            Large,
            Back
        }

        /// <summary>One of the title bar's own buttons: close, large, back. Its glyph is drawn, not a texture (a square, two squares).</summary>
        private static bool FrameButton(Rect r, FrameGlyph glyph, string tip, bool ownLook)
        {
            bool over = Mouse.IsOver(r);
            bool held = over && Input.GetMouseButton(0);
            if (ownLook && over) UiKit.RoundBox(r, held ? UiKit.Pressed : UiKit.RaisedHover, UiKit.Edge, SkinMath.RadiusMedium);
            else if (!ownLook && over) Widgets.DrawHighlight(r);
            var c = over ? UiKit.Ink : UiKit.Faint;
            if (glyph == FrameGlyph.Close) UiKit.Icon(r.ContractedBy(6f), TexButton.CloseXSmall, c);
            else if (Event.current.type == EventType.Repaint)
            {
                var g = r.ContractedBy(7f);
                if (glyph == FrameGlyph.Large) Outline(g, c, 1.5f);
                else
                {
                    Outline(new Rect(g.x + 3f, g.y, g.width - 3f, g.height - 3f), UiKit.WithAlpha(c, 0.7f), 1f);
                    var front = new Rect(g.x, g.y + 3f, g.width - 3f, g.height - 3f);
                    Widgets.DrawBoxSolid(front, ownLook ? UiKit.WindowGround : UiKit.Shade);
                    Outline(front, c, 1.5f);
                }
            }
            TooltipHandler.TipRegion(r, tip);
            if (!Widgets.ButtonInvisible(r, false)) return false;
            SoundDefOf.Click.PlayOneShotOnCamera();
            return true;
        }

        private static void Outline(Rect r, Color c, float t)
        {
            Widgets.DrawBoxSolid(new Rect(r.x, r.y, r.width, t), c);
            Widgets.DrawBoxSolid(new Rect(r.x, r.yMax - t, r.width, t), c);
            Widgets.DrawBoxSolid(new Rect(r.x, r.y, t, r.height), c);
            Widgets.DrawBoxSolid(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        // ---------------- Remembered ----------------

        private static Dictionary<string, FramePlace> places;

        /// <summary>Every window's remembered place, read from the settings once.</summary>
        private static Dictionary<string, FramePlace> Places
        {
            get
            {
                if (places != null) return places;
                places = new Dictionary<string, FramePlace>();
                var lines = ColonyCouncilMod.Settings?.windowPlaces;
                if (lines != null)
                    foreach (var line in lines)
                        if (WindowFrameMath.TryLine(line, out var k, out var p))
                            places[k] = p;
                return places;
            }
        }

        private FramePlace Load()
        {
            if (!known && Places.TryGetValue(key, out var p))
            {
                place = p;
                known = true;
            }
            return place;
        }

        /// <summary>Writes this window's place into the settings and the settings file (on a let-go, a toggle, a close).</summary>
        private void Remember()
        {
            if (!changed && known) return;
            var s = ColonyCouncilMod.Settings;
            if (s == null || !WindowFrameMath.Valid(place.rect)) return;
            changed = false;
            known = true;
            Places[key] = place;
            var list = new List<string>(Places.Count);
            foreach (var kv in Places) list.Add(WindowFrameMath.Line(kv.Key, kv.Value));
            list.Sort(StringComparer.Ordinal);
            s.windowPlaces = list;
            CouncilSettings.SaveNow();
        }

        /// <summary>Every window back where it first opens (the settings' "Put Rimfolk's windows back").</summary>
        public static void Forget()
        {
            places = new Dictionary<string, FramePlace>();
            var s = ColonyCouncilMod.Settings;
            if (s != null) s.windowPlaces = new List<string>();
            foreach (var f in all) f.known = false;
        }

        private static readonly List<WindowFrame> all = new List<WindowFrame>();

        /// <summary>
        /// The frame of the windows of one key, made the first time and kept (a trade window is made anew each trade; its
        /// frame, and where it was left, is the same one), known to <see cref="Forget"/>.
        /// </summary>
        public static WindowFrame For(string key, float minW, float minH, float bottom)
        {
            foreach (var f in all)
                if (f.key == key)
                    return f;
            var made = new WindowFrame(key, minW, minH, bottom);
            all.Add(made);
            return made;
        }

        private static FrameRect F(Rect r) => new FrameRect(r.x, r.y, r.width, r.height);

        private static Rect R(FrameRect r) => new Rect(r.x, r.y, r.w, r.h);
    }

    /// <summary>
    /// A plain window in Rimfolk's frame (UI/WindowFrame.cs): placed where it was left, its title bar drawn, moved and
    /// sized, its contents drawn in Rimfolk's own look while that is on. A window gives its key (its least size and bottom
    /// bar from <see cref="FramedWindowsMath.Keys"/>, or given), the size it first asks for, its title and icon, and draws
    /// its contents in <see cref="DoFramedContents"/>. The game's own close cross, close button, dragging and resizing that a
    /// window asked for give way to the frame's while the frame shows (the close button drawn in the kit where it was), and
    /// come back with both of the frame's settings off, when it is a window of the game's as before.
    /// </summary>
    public abstract class FramedWindow : Window
    {
        protected readonly WindowFrame frame;

        protected FramedWindow(string key, float minW, float minH, float bottom)
        {
            frame = WindowFrame.For(key, minW, minH, bottom);
        }

        /// <summary>A window of a key in <see cref="FramedWindowsMath.Keys"/>: its least size and bottom bar from there.</summary>
        protected FramedWindow(string key)
        {
            FramedWindowsMath.TryKey(key, out var k);
            frame = WindowFrame.For(key, k.leastW, k.leastH, k.Bottom);
        }

        /// <summary>The title in the title bar.</summary>
        protected abstract string FrameTitle { get; }

        protected virtual Texture2D FrameIcon => null;

        /// <summary>The width of the window's own tools in the title bar (drawn by <see cref="DrawFrameTools"/>), 0 for none.</summary>
        protected virtual float FrameToolsWidth => 0f;

        protected virtual void DrawFrameTools(Rect r)
        {
        }

        private Action<Rect> toolsDrawer;

        /// <summary>The window's contents in the body under the title bar (or in the whole window, the frame off).</summary>
        protected abstract void DoFramedContents(Rect inRect);

        /// <summary>The frame draws the title in its bar: a window that draws its own title at the top of its contents leaves it out.</summary>
        protected static bool TitleInBar => WindowFrame.Shown;

        /// <summary>The window's margin without the frame: the game's, unless the window had one of its own.</summary>
        protected virtual float UnframedMargin => StandardMargin;

        protected sealed override float Margin => WindowFrame.Shown ? 0f : UnframedMargin;

        /// <summary>
        /// The window places itself the first time (beside what it belongs to, under the mouse) rather than in the middle of
        /// the screen: <see cref="PlaceUnframed"/> is that place, kept on the screen and remembered afterwards as any other.
        /// </summary>
        protected virtual bool OwnFirstPlace => false;

        /// <summary>Where the window opens without the frame moving it (the game's place, unless the window has its own).</summary>
        protected virtual void PlaceUnframed() => base.SetInitialSizeAndPosition();

        /// <summary>Another window's place, for this one's first opening (a floating window turning the page from another).</summary>
        internal Rect? handedPlace;

        protected sealed override void SetInitialSizeAndPosition()
        {
            PlaceUnframed();
            Rect? first = handedPlace ?? (OwnFirstPlace ? windowRect : (Rect?)null);
            handedPlace = null;
            if (WindowFrame.MovesOn) windowRect = frame.Opening(InitialSize, first);
        }

        public override void Notify_ResolutionChanged()
        {
            if (WindowFrame.MovesOn) windowRect = frame.Refit(windowRect);
            else base.Notify_ResolutionChanged();
        }

        // The game's own close cross, close button, dragging and resizing as the window asked for them, read once.
        private bool flagsRead, gameCloseX, gameCloseButton, gameDrag, gameResize;

        public override void WindowOnGUI()
        {
            if (!flagsRead)
            {
                flagsRead = true;
                gameCloseX = doCloseX;
                gameCloseButton = doCloseButton;
                gameDrag = draggable;
                gameResize = resizeable;
            }
            bool shown = WindowFrame.Shown;
            doCloseX = gameCloseX && !shown;
            doCloseButton = gameCloseButton && !shown;
            draggable = gameDrag && !WindowFrame.MovesOn;
            resizeable = gameResize && !WindowFrame.MovesOn;
            doWindowBackground = !(shown && WindowFrame.OwnLookOn);
            frame.Follow(this);
            base.WindowOnGUI();
        }

        public override void PostClose()
        {
            base.PostClose();
            frame.Closed(this);
        }

        private static string closeLabel;

        public sealed override void DoWindowContents(Rect inRect)
        {
            if (!WindowFrame.Shown)
            {
                DoFramedContents(inRect);
                return;
            }
            bool ownLook = WindowFrame.OwnLookOn;
            bool was = ownLook && UiKit.BeginOwn();
            try
            {
                toolsDrawer ??= DrawFrameTools;
                var body = frame.Draw(this, inRect, FrameTitle, FrameIcon, FrameToolsWidth, FrameToolsWidth > 0f ? toolsDrawer : null);
                // The contents in a group of their own, at zero as the game gives a window's contents.
                Widgets.BeginGroup(body);
                try
                {
                    DoFramedContents(body.AtZero());
                    // The game's close button where the window asked for one: in the kit, at the middle of its foot as it was.
                    if (gameCloseButton)
                    {
                        closeLabel ??= "CloseButton".Translate().Resolve();
                        var b = new Rect(body.width / 2f - CloseButSize.x / 2f, body.height - CloseButSize.y, CloseButSize.x, CloseButSize.y);
                        if (UiKit.Button(b, closeLabel)) Close();
                    }
                }
                finally
                {
                    Widgets.EndGroup();
                }
            }
            finally
            {
                if (ownLook) UiKit.EndOwn(was);
            }
        }
    }
}
