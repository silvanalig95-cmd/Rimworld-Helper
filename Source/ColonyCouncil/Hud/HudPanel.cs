using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// One on-screen panel. It is painted beneath all of the game's own interface, in an early pass of the frame
    /// (<see cref="HudPaint"/>: after the map and its pawn labels, before the colonist bar, the resource list, the alerts,
    /// the inspect pane's tabs, the letters and every window), so whatever the game draws stays on top and the panel
    /// can still be seen wherever nothing covers it. Its window in the game's interface layer is kept for input only
    /// (clicks, the drag, the right-click menu), so clicks on it don't fall through to the map; on repaint the window only
    /// hands over the tooltips, which so show only where no other window covers the panel. Where the game's own
    /// interface sits over the panel (<see cref="HudGameUi"/>), the window steps aside so that interface takes the mouse.
    /// If the early pass can't run, the window paints the panel itself, as before.
    ///
    /// No close button, no background of the game's, no shadow; it pauses nothing and doesn't stop the camera, and it
    /// never keeps the keyboard (the inventory's search box only while it's typed in). A thin bar shows over it while the
    /// mouse is on it: drag it there (or by any empty spot) to move the panel, which snaps to the screen's edges and the
    /// other panels; its buttons switch compact and full, step the background, open the panel's options, and hide it. A
    /// right-click anywhere on the panel opens its menu.
    ///
    /// The same drawing code runs in both passes: the panels draw through <see cref="Over"/>, <see cref="Clicked"/>,
    /// <see cref="Tip(Rect,string)"/> and <see cref="ImageButton"/>, which paint in the early pass and take input in the
    /// window. The panel gathers its data at most twice a second (<see cref="Refresh"/>), and only when the game has moved
    /// on (or every few seconds while paused); drawing only reads what was gathered. If a panel throws, it hides itself
    /// for the rest of the session with one line in the log (<see cref="HudManager.Fail"/>).
    /// </summary>
    public abstract class HudPanel : Window
    {
        public readonly string id;

        private Vector2 size = new Vector2(180f, 48f);
        private Rect body;
        /// <summary>Where the panel is drawn this frame: the body and, while it shows, the bar (screen coordinates).</summary>
        private Rect frame;
        private bool showHeader, headerAbove, hovered;
        /// <summary>The game's own interface is under the mouse over this panel: the window steps aside for it.</summary>
        private bool aside;
        /// <summary>In the early pass: paint only, no input; the tooltips under the mouse are kept for the window.</summary>
        private bool painting;
        /// <summary>While painting: the window under the mouse is this panel.</summary>
        private bool mouseHere;
        private int paintedFrame = -1;
        private readonly List<(Rect rect, TipSignal tip)> tips = new List<(Rect, TipSignal)>();
        private float hoverGrace;
        private bool dragging;
        private Vector2 dragOffset, dragFrom;
        private float nextRefresh = -1f;
        private int refreshedTick = -1;
        private bool dirty = true;

        protected HudPanel(string id)
        {
            this.id = id;
            layer = WindowLayer.GameUI;
            doCloseX = false;
            doCloseButton = false;
            closeOnAccept = false;
            closeOnCancel = false;
            closeOnClickedOutside = false;
            forceCatchAcceptAndCancelEventEvenIfUnfocused = false;
            preventCameraMotion = false;
            absorbInputAroundWindow = false;
            forcePause = false;
            doWindowBackground = false;
            drawShadow = false;
            focusWhenOpened = false;
            // Moved by its own bar (with snapping and the lock), not the game's drag-anywhere; see Layout.
            draggable = false;
            resizeable = false;
            soundAppear = null;
            soundClose = null;
            drawInScreenshotMode = false;
            preventSave = false;
            openMenuOnCancel = false;
        }

        protected static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>This panel's saved state (looked up each time: a reset makes new ones).</summary>
        public HudPanelState St => HudLayout.State(id);

        protected float Scale => St.scale;

        public bool Dragging => dragging;

        /// <summary>The panel's body on the screen (without the bar over it).</summary>
        public Rect Body => body;

        /// <summary>The bar's height at this panel's scale.</summary>
        protected float HeaderH => Mathf.Round(HudMath.HeaderHeight * Mathf.Clamp(Scale, 1f, 1.25f));

        protected override float Margin => 0f;

        public override Vector2 InitialSize => size;

        /// <summary>Gather what the panel shows (at most twice a second).</summary>
        protected abstract void Refresh();

        /// <summary>The body's size for what was gathered, at this scale.</summary>
        protected abstract Vector2 Measure(float scale);

        /// <summary>Draw the body in <paramref name="r"/> (the panel's own coordinates), through Over, Clicked and Tip.</summary>
        protected abstract void DrawBody(Rect r, float scale);

        /// <summary>The panel's own buttons on its bar, right to left from <paramref name="right"/>; returns where they end.</summary>
        protected virtual float HeaderButtons(Rect bar, float right) => right;

        /// <summary>
        /// A place worked out each frame while the player hasn't moved the panel (the alerts follow the letters, as the
        /// game's own list does); false to use the saved or shipped fractions.
        /// </summary>
        protected virtual bool AutoPlace(Vector2 size, float sw, float sh, out HudBox box)
        {
            box = default;
            return false;
        }

        /// <summary>The panel's own lines at the top of its right-click menu.</summary>
        protected virtual void MenuOptions(List<FloatMenuOption> options)
        {
        }

        /// <summary>Gather again at the next chance (an option changed).</summary>
        public void MarkDirty() => dirty = true;

        protected override void SetInitialSizeAndPosition()
        {
            try
            {
                RefreshNow();
                Layout(false);
            }
            catch (Exception e)
            {
                HudManager.Fail(id, e);
            }
            frame = body;
            windowRect = body;
        }

        private void RefreshNow()
        {
            nextRefresh = Time.realtimeSinceStartup + 0.5f;
            refreshedTick = Find.TickManager?.TicksGame ?? 0;
            dirty = false;
            Refresh();
            size = Measure(HudMath.ClampScale(Scale));
            size.x = Mathf.Max(60f, Mathf.Round(size.x));
            size.y = Mathf.Max(20f, Mathf.Round(size.y));
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            if (HudManager.Failed(id)) return;
            try
            {
                float now = Time.realtimeSinceStartup;
                int tick = Find.TickManager?.TicksGame ?? 0;
                // Twice a second while the game runs; while it's paused, only every three seconds or when something changed.
                if (dirty || (now >= nextRefresh && (tick != refreshedTick || now >= nextRefresh + 2.5f))) RefreshNow();
                Layout(true);
            }
            catch (Exception e)
            {
                HudManager.Fail(id, e);
            }
        }

        /// <summary>Where the panel is this frame: its saved place (or under the mouse while dragged), and the bar when shown.</summary>
        private void Layout(bool mouse)
        {
            float sw = UI.screenWidth, sh = UI.screenHeight;
            var m = UI.MousePositionOnUIInverted;
            if (dragging)
            {
                if (!Input.GetMouseButton(0))
                {
                    dragging = false;
                    // A click that didn't move it leaves its place (and the settings file) alone.
                    if ((body.position - dragFrom).sqrMagnitude >= 1f) SavePlace();
                }
                else
                {
                    var b = new HudBox(m.x - dragOffset.x, m.y - dragOffset.y, size.x, size.y);
                    b = HudMath.Snap(b, sw, sh, HudManager.OtherBoxes(this));
                    body = new Rect(b.x, b.y, b.w, b.h);
                }
            }
            if (!dragging)
            {
                var st = St;
                if (!st.Placed && AutoPlace(size, sw, sh, out var auto)) body = new Rect(auto.x, auto.y, auto.w, auto.h);
                else
                {
                    var place = st.Placed ? st : HudLayout.Default(id);
                    var b = HudMath.FromFraction(place.x, place.y, place.right, place.bottom, size.x, size.y, sw, sh);
                    body = new Rect(b.x, b.y, b.w, b.h);
                }
            }
            float hh = HeaderH;
            bool overFrame = mouse && frame.Contains(m);
            // The game's own interface (the colonist bar, the resource list, the inspect pane's tabs...) is drawn over the
            // panel there: the window steps aside, so that interface gets the mouse, its tooltips and its clicks.
            aside = overFrame && !dragging && !KeepsKeyboard && HudGameUi.Covers(m);
            // Under the mouse, and not behind another window there (none at all: the window stepped aside last frame).
            var under = overFrame ? Find.WindowStack?.GetWindowAt(m) : null;
            hovered = overFrame && !aside && !HudManager.AnyDragging && (dragging || under == this || under == null);
            float now = Time.realtimeSinceStartup;
            if (hovered) hoverGrace = now + 0.35f;
            showHeader = dragging || HudManager.editing || hovered || now < hoverGrace;
            headerAbove = HudMath.HeaderAbove(new HudBox(body.x, body.y, body.width, body.height), hh);
            if (!showHeader) frame = body;
            else if (headerAbove) frame = new Rect(body.x, body.y - hh, body.width, body.height + hh);
            else frame = new Rect(body.x, body.y, body.width, body.height + hh);
            // Stepped aside: no size for this frame, so the window is under the mouse nowhere (and nothing can pull it back on).
            windowRect = aside ? new Rect(frame.x, frame.y, 0f, 0f) : frame;
        }

        private void SavePlace()
        {
            var st = St;
            HudMath.ToFraction(new HudBox(body.x, body.y, body.width, body.height), UI.screenWidth, UI.screenHeight, out st.x, out st.y, out st.right,
                out st.bottom);
            HudLayout.Save();
        }

        private bool CanMove => !S.hudLock || HudManager.editing;

        private void StartDrag()
        {
            if (!CanMove) return;
            dragging = true;
            dragFrom = body.position;
            dragOffset = UI.MousePositionOnUIInverted - body.position;
        }

        public override void PostClose()
        {
            base.PostClose();
            if (dragging)
            {
                dragging = false;
                SavePlace();
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (HudManager.Failed(id) || aside) return;
            var cur = Event.current;
            // Painted earlier in the frame, beneath the game's interface: the window only hands over the tooltips.
            if (cur.type == EventType.Repaint && PaintedThisFrame)
            {
                for (int i = 0; i < tips.Count; i++) TooltipHandler.TipRegion(tips[i].rect, tips[i].tip);
                WindowExtras(true);
                return;
            }
            // Behind another window where the mouse is: the clicks, drags and wheel are that window's, never the panel's.
            if ((cur.isMouse || cur.type == EventType.ScrollWheel) && !dragging && Find.WindowStack.GetWindowAt(UI.MousePositionOnUIInverted) != this) return;
            Contents(false);
        }

        /// <summary>Whether the early pass painted this panel this frame (if not, the window paints it, as a fallback).</summary>
        public bool PaintedThisFrame => paintedFrame == Time.frameCount;

        /// <summary>
        /// The early pass (<see cref="HudPaint"/>, on repaint only): the panel painted at its place beneath the game's
        /// interface, with no input; the tooltips under the mouse are kept for the window to hand over.
        /// </summary>
        public void PaintEarly()
        {
            if (HudManager.Failed(id) || !IsOpen) return;
            tips.Clear();
            GUI.BeginGroup(frame);
            try
            {
                Contents(true);
            }
            finally
            {
                GUI.EndGroup();
            }
            paintedFrame = Time.frameCount;
        }

        /// <summary>The panel's body and bar in its own coordinates: painted (the early pass), or for input and as a fallback (the window).</summary>
        private void Contents(bool paint)
        {
            var oldFont = Text.Font;
            painting = paint;
            try
            {
                mouseHere = paint && !aside && Find.WindowStack.GetWindowAt(UI.MousePositionOnUIInverted) == this;
                float hh = HeaderH;
                var bodyLocal = BodyLocal;
                float scale = HudMath.ClampScale(Scale);
                HudKit.Panel(bodyLocal, St.opacity, hovered || dragging);
                DrawBody(bodyLocal, scale);
                if (HudManager.editing) Widgets.DrawBox(bodyLocal.ContractedBy(1f), 1, HudManager.EditLineTex);
                if (showHeader) DrawHeader(new Rect(0f, headerAbove ? 0f : body.height, body.width, hh));
                if (!paint) WindowExtras(false);
                // A click no row took: right for the menu, left to move the panel.
                var ev = Event.current;
                if (!paint && ev.type == EventType.MouseDown && Mouse.IsOver(bodyLocal))
                {
                    if (ev.button == 1)
                    {
                        OpenMenu();
                        ev.Use();
                    }
                    else if (ev.button == 0 && CanMove)
                    {
                        StartDrag();
                        ev.Use();
                    }
                }
            }
            catch (Exception e)
            {
                HudManager.Fail(id, e);
            }
            finally
            {
                painting = false;
                Text.Font = oldFont;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        /// <summary>The body in the panel's own coordinates (below the bar when the bar is above it).</summary>
        protected Rect BodyLocal => new Rect(0f, showHeader && headerAbove ? HeaderH : 0f, body.width, body.height);

        /// <summary>
        /// What only the window can draw (a text field, which needs the window's keyboard): every event of the window,
        /// on repaint too (<paramref name="repaintOnly"/> when the body was painted earlier). Nothing by default.
        /// </summary>
        protected virtual void WindowExtras(bool repaintOnly)
        {
        }

        // ---------------- Drawing that works in both passes ----------------

        /// <summary>The panel is being typed in (the inventory's search box): it keeps the keyboard and never steps aside.</summary>
        public virtual bool KeepsKeyboard => false;

        /// <summary>Whether this is the early, paint-only pass (nothing is clickable then).</summary>
        protected bool Painting => painting;

        /// <summary>The mouse is over <paramref name="r"/> on this panel (and no other window covers it there).</summary>
        protected bool Over(Rect r) => painting ? mouseHere && r.Contains(Event.current.mousePosition) : Mouse.IsOver(r);

        /// <summary>A click on <paramref name="r"/> (any button; Event.current.button says which). Never while painting.</summary>
        protected bool Clicked(Rect r) => !painting && Widgets.ButtonInvisible(r, false);

        /// <summary>
        /// The "and N more" line under a panel's rows: hover says why, and a click shows them all by raising the panel's
        /// row limit (its options set it back).
        /// </summary>
        protected void MoreLine(Rect r, string text, int hiddenCount)
        {
            HudKit.Label(r, text, Over(r) ? Color.white : UiKit.Faint, GameFont.Tiny);
            Tip(r, HudKit.T("CC_Pnl_MoreLinesTip"));
            if (!Clicked(r) || hiddenCount <= 0) return;
            var st = St;
            if (st != null && st.maxRows > 0) st.maxRows += hiddenCount;
            MarkDirty();
            HudLayout.Save();
        }

        /// <summary>The highlight while the mouse is over <paramref name="r"/>.</summary>
        protected void HighlightIfOver(Rect r)
        {
            if (Over(r)) Widgets.DrawHighlight(r);
        }

        protected void Tip(Rect r, string text)
        {
            if (!text.NullOrEmpty()) Tip(r, new TipSignal(text));
        }

        /// <summary>A tooltip worked out only while it shows.</summary>
        protected void Tip(Rect r, Func<string> text, int uniqueId) => Tip(r, new TipSignal(text, uniqueId));

        /// <summary>
        /// A tooltip for <paramref name="r"/>. While painting it's only kept (when the mouse is on it) for the window to
        /// hand over, so it shows only where the panel is the window under the mouse, never through another window.
        /// </summary>
        protected void Tip(Rect r, TipSignal tip)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!painting) TooltipHandler.TipRegion(r, tip);
            else if (r.Contains(Event.current.mousePosition)) tips.Add((r, tip));
        }

        /// <summary>A small picture button whose colour lights up under the mouse; true when clicked.</summary>
        protected bool ImageButton(Rect r, Texture2D tex, Color color, Color overColor, string tip)
        {
            if (tex != null && Event.current.type == EventType.Repaint)
            {
                GUI.color = Over(r) ? overColor : color;
                GUI.DrawTexture(r, tex);
                GUI.color = Color.white;
            }
            Tip(r, tip);
            return Clicked(r);
        }

        // ---------------- The bar ----------------

        private void DrawHeader(Rect bar)
        {
            Widgets.DrawBoxSolid(bar, new Color(0.06f, 0.065f, 0.07f, 0.92f));
            Widgets.DrawBoxSolid(new Rect(bar.x, headerAbove ? bar.yMax - 1f : bar.y, bar.width, 1f), UiKit.WithAlpha(UiKit.Gold, 0.45f));
            float bs = bar.height - 4f;
            float right = bar.xMax - 2f;
            // Hide
            var hide = new Rect(right - bs, bar.y + 2f, bs, bs);
            if (ImageButton(hide, TexButton.CloseXSmall, UiKit.Faint, Color.white, HudKit.T("CC_Pnl_HideTip")))
            {
                St.on = false;
                HudLayout.Save();
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            right = hide.x - 3f;
            // Options
            var gear = new Rect(right - bs, bar.y + 2f, bs, bs);
            if (ImageButton(gear, TexButton.OpenInspectSettings, UiKit.Faint, Color.white, HudKit.T("CC_Pnl_OptionsTip")))
                Dialog_HudPanel.Open(id);
            right = gear.x - 3f;
            // Background: steps down, then back to full.
            string op = St.opacity.ToStringPercent();
            float ow = HudKit.Width(op, GameFont.Tiny) + 6f;
            var opR = new Rect(right - ow, bar.y, ow, bar.height);
            HighlightIfOver(opR);
            HudKit.Label(opR, op, UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleCenter);
            Tip(opR, HudKit.T("CC_Pnl_OpacityTip"));
            if (Clicked(opR))
            {
                var st = St;
                st.opacity = st.opacity > 0.8f ? 0.7f : st.opacity > 0.5f ? 0.45f : st.opacity > 0.3f ? 0.25f : 1f;
                HudLayout.Save();
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            right = opR.x - 3f;
            // Compact or full
            var cf = new Rect(right - bs, bar.y + 2f, bs, bs);
            if (ImageButton(cf, St.compact ? TexButton.Reveal : TexButton.Collapse, UiKit.Faint, Color.white,
                    HudKit.T(St.compact ? "CC_Pnl_FullTip" : "CC_Pnl_CompactTip")))
            {
                St.compact = !St.compact;
                MarkDirty();
                HudLayout.Save();
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            right = cf.x - 3f;
            right = HeaderButtons(bar, right);
            // The grip and the name: drag here.
            var grip = new Rect(bar.x, bar.y, Mathf.Max(0f, right - bar.x), bar.height);
            if (CanMove && TexButton.DragHash != null)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.45f);
                GUI.DrawTexture(new Rect(bar.x + 3f, bar.y + 3f, bs - 2f, bs - 2f), TexButton.DragHash);
                GUI.color = Color.white;
            }
            HudKit.Label(new Rect(bar.x + bs + 5f, bar.y, Mathf.Max(0f, right - bar.x - bs - 7f), bar.height), HudLayout.Label(id), UiKit.HeaderColor,
                GameFont.Tiny);
            var ev = Event.current;
            if (!painting && ev.type == EventType.MouseDown && Mouse.IsOver(grip))
            {
                if (ev.button == 0 && CanMove) StartDrag();
                else if (ev.button == 1) OpenMenu();
                ev.Use();
            }
        }

        /// <summary>A small text button on the bar (the board's order, the graph's days); returns where it starts.</summary>
        protected float BarText(Rect bar, float right, string label, string tip, Action click)
        {
            float w = HudKit.Width(label, GameFont.Tiny) + 8f;
            var r = new Rect(right - w, bar.y, w, bar.height);
            HighlightIfOver(r);
            HudKit.Label(r, label, UiKit.Gold, GameFont.Tiny, TextAnchor.MiddleCenter);
            Tip(r, tip);
            if (Clicked(r))
            {
                click();
                MarkDirty();
                HudLayout.Save();
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            return r.x - 3f;
        }

        // ---------------- The menu ----------------

        public void OpenMenu() => OpenMenu(null);

        /// <summary>The panel's menu, with <paramref name="first"/> (what a row's right-click offers) at the top.</summary>
        public void OpenMenu(List<FloatMenuOption> first)
        {
            try
            {
                var options = new List<FloatMenuOption>();
                if (first != null) options.AddRange(first);
                MenuOptions(options);
                options.Add(new FloatMenuOption(HudKit.T(St.compact ? "CC_Pnl_Full" : "CC_Pnl_Compact"), () =>
                {
                    St.compact = !St.compact;
                    MarkDirty();
                    HudLayout.Save();
                }));
                options.Add(new FloatMenuOption(HudKit.T("CC_Pnl_Options"), () => Dialog_HudPanel.Open(id)));
                options.Add(new FloatMenuOption(HudKit.T("CC_Pnl_EditLayout"), () => HudManager.editing = true));
                options.Add(new FloatMenuOption(HudKit.T(S.hudLock ? "CC_Pnl_Unlock" : "CC_Pnl_Lock"), () =>
                {
                    S.hudLock = !S.hudLock;
                    HudLayout.Save();
                }));
                options.Add(new FloatMenuOption(HudKit.T("CC_Pnl_Hide"), () =>
                {
                    St.on = false;
                    HudLayout.Save();
                }));
                Find.WindowStack.Add(new FloatMenu(options));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("on-screen panel menu", e);
            }
        }
    }
}
