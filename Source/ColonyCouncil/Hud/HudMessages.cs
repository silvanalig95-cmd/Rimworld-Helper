using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The game's messages (the lines at the top left) are drawn over everything as bare text with a faint shadow, which
    /// is hard to read over a panel's own lines. A message that touches a panel gets a box of its own behind the whole
    /// line (a dark fill, a thin light edge), fading with the message; one over the map alone is left as the game draws
    /// it. The messages' places are worked out as the game lays them out (Messages.MessagesDoGUI: newest first from the
    /// top left, one line each); if this game version keeps them some other way, nothing is painted.
    /// </summary>
    public static class HudMessages
    {
        /// <summary>The game's step from one message to the next.</summary>
        private const float LineStep = 26f;

        private static readonly FieldInfo LiveField = AccessTools.Field(typeof(Messages), "liveMessages");
        private static readonly PropertyInfo AlphaProp = AccessTools.Property(typeof(Message), "Alpha");
        private static readonly Color Backing = new Color(0.05f, 0.055f, 0.06f, 0.95f);
        private static readonly Color Edge = new Color(1f, 1f, 1f, 0.14f);

        private static int frame = -1;
        private static readonly List<(Rect rect, float alpha)> shown = new List<(Rect, float)>();
        private static bool failed;

        /// <summary>The messages on screen this frame, with their places and how far they've faded.</summary>
        private static List<(Rect rect, float alpha)> Shown()
        {
            if (frame == Time.frameCount) return shown;
            frame = Time.frameCount;
            shown.Clear();
            if (failed || LiveField == null) return shown;
            try
            {
                if (!(LiveField.GetValue(null) is List<Message> live) || live.Count == 0) return shown;
                var oldFont = Text.Font;
                Text.Font = GameFont.Small;
                float x = Messages.MessagesTopLeftStandard.x, y = Messages.MessagesTopLeftStandard.y;
                if (Current.Game != null && Find.ActiveLesson != null && Find.ActiveLesson.ActiveLessonVisible)
                    y += Find.ActiveLesson.Current?.MessagesYOffset ?? 0f;
                for (int i = live.Count - 1; i >= 0; i--)
                {
                    var m = live[i];
                    if (m == null) continue;
                    float alpha = 1f;
                    if (AlphaProp != null && AlphaProp.GetValue(m) is float a) alpha = a;
                    shown.Add((m.CalculateRect(x, y), alpha));
                    y += LineStep;
                }
                Text.Font = oldFont;
            }
            catch (Exception e)
            {
                failed = true;
                CouncilLog.ErrorOnce("message backing", e);
            }
            return shown;
        }

        /// <summary>
        /// A box behind each message that touches one of the panels (on screen, <paramref name="panels"/>): the whole line,
        /// a little padding, a dark fill and a thin light edge, fading with the message. Drawn on screen after the panels
        /// and before the messages (the early pass, HudPaint).
        /// </summary>
        public static void Boxes(List<Rect> panels)
        {
            if (Event.current?.type != EventType.Repaint || panels == null || panels.Count == 0) return;
            var list = Shown();
            for (int i = 0; i < list.Count; i++)
            {
                var (r, alpha) = list[i];
                var box = Box(r);
                if (alpha <= 0.01f || !Touches(box, panels)) continue;
                Draw(box, alpha);
            }
        }

        /// <summary>
        /// The same, for a panel painting in its own window (the fallback when the early pass can't run): only the part of
        /// the box over this panel, in its own coordinates, since a window can't draw outside itself.
        /// </summary>
        public static void Under(Rect screen)
        {
            if (Event.current?.type != EventType.Repaint) return;
            var list = Shown();
            for (int i = 0; i < list.Count; i++)
            {
                var (r, alpha) = list[i];
                var box = Box(r);
                if (alpha <= 0.01f || !box.Overlaps(screen)) continue;
                box.position -= screen.position;
                Draw(box, alpha);
            }
        }

        /// <summary>A message's line with its padding.</summary>
        private static Rect Box(Rect line) => new Rect(line.x - PadX, line.y - PadY, line.width + PadX * 2f, line.height + PadY * 2f);

        private const float PadX = 7f, PadY = 1f;

        private static bool Touches(Rect box, List<Rect> panels)
        {
            for (int i = 0; i < panels.Count; i++)
                if (box.Overlaps(panels[i])) return true;
            return false;
        }

        private static void Draw(Rect box, float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            var fill = Backing;
            fill.a *= alpha;
            var edge = Edge;
            edge.a *= alpha;
            Widgets.DrawBoxSolidWithOutline(box, fill, edge, 1);
        }
    }
}
