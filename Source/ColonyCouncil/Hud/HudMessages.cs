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
    /// is hard to read over a panel's own lines. Where a message lies over a panel, the panel is painted dark beneath it,
    /// fading with the message. The messages' places are worked out as the game lays them out (Messages.MessagesDoGUI:
    /// newest first from the top left, one line each); if this game version keeps them some other way, nothing is painted.
    /// </summary>
    public static class HudMessages
    {
        /// <summary>The game's step from one message to the next.</summary>
        private const float LineStep = 26f;

        private static readonly FieldInfo LiveField = AccessTools.Field(typeof(Messages), "liveMessages");
        private static readonly PropertyInfo AlphaProp = AccessTools.Property(typeof(Message), "Alpha");
        private static readonly Color Backing = new Color(0.06f, 0.06f, 0.07f, 0.94f);

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
        /// Darkens the part of a panel under any message, drawn in the panel's own coordinates (its top left at
        /// <paramref name="screen"/>'s), after the panel's contents and before the messages.
        /// </summary>
        public static void Under(Rect screen)
        {
            if (Event.current?.type != EventType.Repaint) return;
            var list = Shown();
            for (int i = 0; i < list.Count; i++)
            {
                var (r, alpha) = list[i];
                var back = r.ExpandedBy(5f, 2f);
                if (!back.Overlaps(screen) || alpha <= 0.01f) continue;
                back.position -= screen.position;
                var c = Backing;
                c.a *= Mathf.Clamp01(alpha);
                Widgets.DrawBoxSolid(back, c);
            }
        }
    }
}
