using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A name's preview (docs/design/interface.md, "Links and previews"): resting the mouse on a link for a short pause
    /// (the setting "Pause before a preview", 0.35 seconds at first) shows a small card under it: the picture, the name,
    /// what it is, its state and the two or three lines that matter now. It stays while the mouse is on the link or on
    /// the preview, and half a second after it leaves; a click on it opens the card. One level deep for now. None while
    /// dragging or scrolling, none with the setting off. It is Rimfolk's own small immediate window on the layer the
    /// game's tooltips use (WindowStack.ImmediateWindow), asked for every frame while it shows, so it goes the moment
    /// nothing asks; buttons and icons keep the game's own plain tooltips.
    /// </summary>
    public static class Previews
    {
        private const int WindowId = 0x5F0A1D;
        private const float Width = 286f;
        private const float Stay = 0.5f;

        private static ThingRef pending;
        private static float pendingSince;
        private static Rect pendingAnchor;
        private static int hoverFrame = -10;

        private static ThingRef shown;
        private static Rect shownRect;
        private static float lastIn;
        private static float scrolledAt = -10f;
        private static readonly Action drawWindow = DrawWindow;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => S == null || S.linkPreviews;

        private static float Delay => S != null ? Mathf.Clamp(S.linkPreviewDelay, 0.05f, 3f) : 0.35f;

        /// <summary>The thing whose preview shows now, or none.</summary>
        public static ThingRef Shown => shown;

        /// <summary>From a link or a chip the mouse is on, while drawing (its rect in the drawing's own coordinates).</summary>
        public static void Hover(ThingRef r, Rect local)
        {
            if (r.IsNone || !On || Event.current == null || Event.current.type != EventType.Repaint) return;
            float now = Time.realtimeSinceStartup;
            hoverFrame = Time.frameCount;
            if (r == shown)
            {
                lastIn = now;
                return;
            }
            if (r != pending)
            {
                pending = r;
                pendingSince = now;
            }
            try
            {
                pendingAnchor = UI.GUIToScreenRect(local);
            }
            catch (Exception)
            {
                pending = ThingRef.None;
            }
        }

        /// <summary>No preview now (a card opened, a window closed).</summary>
        public static void Hide()
        {
            shown = ThingRef.None;
            pending = ThingRef.None;
        }

        /// <summary>From the game's OnGUI every event (UI/WeekMemory.cs): shows, keeps or lets go of the preview, and asks for its window.</summary>
        public static void OnGUI()
        {
            var e = Event.current;
            if (e == null) return;
            float now = Time.realtimeSinceStartup;
            if (e.type == EventType.ScrollWheel)
            {
                scrolledAt = now;
                if (!shown.IsNone && !shownRect.Contains(UI.MousePositionOnUIInverted)) Hide();
                return;
            }
            if (e.type != EventType.Repaint) return;
            if (!On || Current.ProgramState != ProgramState.Playing)
            {
                Hide();
                return;
            }
            // The links were drawn last frame (the windows draw after this).
            bool hovered = hoverFrame >= Time.frameCount - 2;
            if (!hovered) pending = ThingRef.None;
            bool busy = Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2) || now - scrolledAt < 0.3f;
            if (!pending.IsNone && pending != shown && !busy && now - pendingSince >= Delay)
            {
                shown = pending;
                lastIn = now;
                shownRect = Place(pendingAnchor, Height(Cards.Model(shown)));
            }
            if (shown.IsNone) return;
            if (shownRect.Contains(UI.MousePositionOnUIInverted)) lastIn = now;
            if (now - lastIn > Stay)
            {
                Hide();
                return;
            }
            var m = Cards.Model(shown);
            shownRect.height = Height(m);
            if (shownRect.yMax > UI.screenHeight - 4f) shownRect.y = Mathf.Max(4f, UI.screenHeight - 4f - shownRect.height);
            Find.WindowStack.ImmediateWindow(WindowId, shownRect, WindowLayer.Super, drawWindow, doBackground: false);
        }

        /// <summary>Under the link where there is room, else above it; never off the screen.</summary>
        private static Rect Place(Rect anchor, float height)
        {
            float x = Mathf.Clamp(anchor.x - 12f, 4f, UI.screenWidth - Width - 4f);
            float y = anchor.yMax + 6f;
            if (y + height > UI.screenHeight - 4f) y = anchor.y - 6f - height;
            if (y < 4f) y = Mathf.Max(4f, UI.screenHeight - 4f - height);
            return new Rect(x, y, Width, height);
        }

        private const float Pad = 10f, Pic = 34f;

        private static float Height(CardModel m)
        {
            float w = Width - Pad * 2f;
            float h = Pad + Pic + 4f;
            if (m.chips.Count > 0) h += UiKit.ChipHeight + 6f;
            foreach (var g in m.glance) h += LinkText.Height(g, w, GameFont.Small) + 2f;
            h += 6f + 18f + Pad;
            return Mathf.Ceil(h);
        }

        private static void DrawWindow()
        {
            var thing = shown;
            if (thing.IsNone) return;
            var rect = shownRect.AtZero();
            var m = Cards.Model(thing);
            Widgets.DrawBoxSolidWithOutline(rect, UiKit.PreviewFill, UiKit.PreviewEdge, 1);
            float x = Pad, y = Pad, w = rect.width - Pad * 2f;
            var head = new Rect(x, y, w, Pic);
            if (Mouse.IsOver(head)) Widgets.DrawBoxSolid(head.ExpandedBy(3f), UiKit.CardHover);
            UiKit.Picture(new Rect(x, y, Pic, Pic), m.pawn, m.faction, m.icon, m.iconColor, m.ring);
            float tx = x + Pic + 8f, tw = w - Pic - 8f;
            UiKit.LabelFit(new Rect(tx, y - 1f, tw, 20f), m.title, UiKit.HeaderColor);
            LinkText.Line(new Rect(tx, y + 17f, tw, 18f), m.subtitle, UiKit.Faint, GameFont.Tiny);
            y += Pic + 4f;
            if (m.chips.Count > 0)
            {
                float cx = x;
                foreach (var c in m.chips)
                {
                    if (!c.thing.IsNone) continue;
                    float cw = UiKit.StateChipWidth(c.text, w - (cx - x));
                    if (cx + cw > x + w) break;
                    cx += UiKit.StateChip(cx, y, c.text, c.color, w - (cx - x)) + 4f;
                }
                y += UiKit.ChipHeight + 6f;
            }
            foreach (var g in m.glance) y += LinkText.Draw(new Rect(x, y, w, 999f), g, Color.white) + 2f;
            y += 4f;
            UiKit.Divider(x, y, w);
            UiKit.LabelFit(new Rect(x, y + 2f, w, 18f), "CC_Card_PreviewHint".Translate(), UiKit.Dim, GameFont.Tiny);
            if (Widgets.ButtonInvisible(head, false))
            {
                Cards.Click();
                Cards.Open(thing);
            }
        }
    }
}
