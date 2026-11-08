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
    /// the preview, and half a second after it leaves; a click on it opens the card. A link inside a preview shows one more
    /// preview beside it, two levels at most (PreviewStack); the first stays while the second does. None while dragging or
    /// scrolling, none with the setting off. Each is Rimfolk's own small immediate window on the layer the game's tooltips
    /// use (WindowStack.ImmediateWindow), asked for every frame while it shows, so it goes the moment nothing asks; the
    /// mouse inside one belongs to it (the window under it sees none, a click on its empty part goes no further), and
    /// when it goes the window under it has the mouse back. Buttons and icons keep the game's own plain tooltips. A preview
    /// takes Rimfolk's own look when the window its link is in draws in it (docs/design/rimfolk-look.md, stage 2): rounded,
    /// in the look's palette; from the game's panes and the on-screen panels it is the game's as before.
    /// </summary>
    public static class Previews
    {
        private const int WindowId = 0x5F0A1D;

        private static readonly PreviewStack stack = new PreviewStack();
        private static float scrolledAt = -10f;
        /// <summary>The preview being drawn (1 or 2), 0 outside them: a link drawn there asks for the level after it.</summary>
        private static int drawing;
        private static readonly Action[] drawWindows = { null, () => DrawWindow(1), () => DrawWindow(2) };
        private static readonly Func<ThingRef, float> heightOf = r => Height(Cards.Model(r));
        /// <summary>Whether each level's link was drawn in Rimfolk's own look (its window's), so the preview takes it too.</summary>
        private static readonly bool[] ownAt = new bool[PreviewMath.Levels + 2];

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => S == null || S.linkPreviews;

        private static float Delay => S != null ? Mathf.Clamp(S.linkPreviewDelay, 0.05f, 3f) : 0.35f;

        /// <summary>The thing whose first preview shows now, or none.</summary>
        public static ThingRef Shown => stack.Shown(1);

        /// <summary>From a link or a chip the mouse is on, while drawing (its rect in the drawing's own coordinates).</summary>
        public static void Hover(ThingRef r, Rect local)
        {
            if (r.IsNone || !On || Event.current == null || Event.current.type != EventType.Repaint) return;
            ScreenBox anchor;
            try
            {
                var s = UI.GUIToScreenRect(local);
                anchor = new ScreenBox(s.x, s.y, s.width, s.height);
            }
            catch (Exception)
            {
                return;
            }
            stack.Hover(drawing, r, anchor, Time.realtimeSinceStartup, Time.frameCount);
            // A link drawn at this level asks for the level after it.
            int level = drawing + 1;
            if (level < ownAt.Length) ownAt[level] = UiKit.OwnLook;
        }

        /// <summary>No preview now (a card opened, a window closed); none again for the name under the mouse until it leaves it.</summary>
        public static void Hide() => stack.Hide();

        /// <summary>From the game's OnGUI every event (UI/WeekMemory.cs): shows, keeps or lets go of the previews, and asks for their windows.</summary>
        public static void OnGUI()
        {
            var e = Event.current;
            if (e == null) return;
            float now = Time.realtimeSinceStartup;
            var mouse = UI.MousePositionOnUIInverted;
            if (e.type == EventType.ScrollWheel)
            {
                scrolledAt = now;
                if (stack.Any && !stack.Inside(mouse.x, mouse.y)) Hide();
                return;
            }
            if (e.type != EventType.Repaint) return;
            if (!On || Current.ProgramState != ProgramState.Playing)
            {
                Hide();
                return;
            }
            bool busy = Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2) || now - scrolledAt < 0.3f;
            stack.Tick(now, Time.frameCount, busy, Delay, mouse.x, mouse.y, UI.screenWidth, UI.screenHeight, heightOf);
            // The first under the second: asked for in that order every frame, so the second stays on top.
            for (int level = 1; level <= PreviewMath.Levels; level++)
            {
                var thing = stack.Shown(level);
                if (thing.IsNone) continue;
                stack.Fit(level, Height(Cards.Model(thing)), UI.screenHeight);
                var b = stack.RectOf(level);
                Find.WindowStack.ImmediateWindow(WindowId + level, new Rect(b.x, b.y, b.w, b.h), WindowLayer.Super, drawWindows[level], doBackground: false);
            }
        }

        private const float Pad = 10f, Pic = 34f;

        private static float Height(CardModel m)
        {
            float w = PreviewMath.Width - Pad * 2f;
            float h = Pad + Pic + 4f;
            if (m.chips.Count > 0) h += UiKit.ChipHeight + 6f;
            foreach (var g in m.glance) h += LinkText.Height(g, w, GameFont.Small) + 2f;
            h += 6f + 18f + Pad;
            return Mathf.Ceil(h);
        }

        private static void DrawWindow(int level)
        {
            var thing = stack.Shown(level);
            if (thing.IsNone) return;
            var b = stack.RectOf(level);
            var rect = new Rect(0f, 0f, b.w, b.h);
            int was = drawing;
            drawing = level;
            bool own = ownAt[level] && WindowFrame.OwnLookOn;
            bool wasOwn = own && UiKit.BeginOwn();
            try
            {
                var m = Cards.Model(thing);
                if (UiKit.OwnLook) UiKit.RoundBox(rect, UiKit.PreviewFill, UiKit.PreviewEdge, SkinMath.RadiusCard);
                else Widgets.DrawBoxSolidWithOutline(rect, UiKit.PreviewFill, UiKit.PreviewEdge, 1);
                float x = Pad, y = Pad, w = rect.width - Pad * 2f;
                var head = new Rect(x, y, w, Pic);
                if (Mouse.IsOver(head))
                {
                    if (UiKit.OwnLook) UiKit.Round(head.ExpandedBy(3f), UiKit.CardHover, SkinMath.RadiusMedium);
                    else Widgets.DrawBoxSolid(head.ExpandedBy(3f), UiKit.CardHover);
                }
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
                // A click on the preview's empty part stays in it, so nothing under it is clicked through it.
                GenUI.AbsorbClicksInRect(rect);
            }
            finally
            {
                drawing = was;
                if (own) UiKit.EndOwn(wasOwn);
            }
        }
    }
}
