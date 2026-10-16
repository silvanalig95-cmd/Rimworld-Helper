using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A tooltip on the heart's line in the inspector ("Strain: worn down · restless · lacks belonging"): what strain,
    /// the feelings that stand out and the heart's needs mean, and what would help. The game draws the whole inspect
    /// text as one scrolling label, so this finds where the heart's line sits in it (the same text at the same width)
    /// and puts the tooltip over that line only. The heights are measured again only when the text changes; the
    /// tooltip's text is built only while it's hovered.
    /// </summary>
    [HarmonyPatch(typeof(InspectPaneFiller), nameof(InspectPaneFiller.DrawInspectString))]
    public static class Patch_HeartInspectTip
    {
        /// <summary>The game keeps room for a scroll bar beside the inspect text, shown or not.</summary>
        private const float ScrollBarWidth = 16f;

        private static string measuredText, measuredLine;
        private static float measuredWidth, lineTop, lineHeight, textHeight;
        private static bool lineFound;

        private static Pawn tipPawn;
        private static Func<string> tipText;

        private static bool scrollLooked;
        private static FieldInfo scrollPos;

        public static void Postfix(string str, Rect rect)
        {
            if (!RimfolkPatches.On || str.NullOrEmpty() || Event.current == null) return;
            var s = ColonyCouncilMod.Settings;
            if (s == null || !s.heart || !s.heartInspector) return;
            try
            {
                if (!(Find.Selector?.SingleSelectedThing is Pawn p) || !p.IsFreeColonist) return;
                var h = Heart.Of(p);
                string line = h?.inspectLine;
                if (line.NullOrEmpty()) return;
                float width = rect.width - ScrollBarWidth;
                if (width <= 0f) return;
                if (width != measuredWidth || !ReferenceEquals(line, measuredLine) || !string.Equals(str, measuredText, StringComparison.Ordinal))
                    Measure(str, line, width);
                if (!lineFound) return;

                // Scrolled only when the text is taller than the pane.
                float scroll = 0f;
                if (textHeight > rect.height)
                {
                    if (!scrollLooked)
                    {
                        scrollLooked = true;
                        scrollPos = AccessTools.Field(typeof(InspectPaneFiller), "inspectStringScrollPos");
                        if (scrollPos != null && (!scrollPos.IsStatic || scrollPos.FieldType != typeof(Vector2))) scrollPos = null;
                    }
                    if (scrollPos != null) scroll = ((Vector2)scrollPos.GetValue(null)).y;
                }
                float top = Mathf.Max(rect.y, rect.y + lineTop - scroll);
                float bottom = Mathf.Min(rect.yMax, rect.y + lineTop + lineHeight - scroll);
                if (bottom <= top) return;
                var area = new Rect(rect.x, top, width, bottom - top);
                Widgets.DrawHighlightIfMouseover(area);
                if (tipPawn != p || tipText == null)
                {
                    tipPawn = p;
                    var pawn = p;
                    tipText = () =>
                    {
                        var heart = Heart.Of(pawn);
                        return heart == null ? null : HeartView.InspectTip(pawn, heart);
                    };
                }
                TooltipHandler.TipRegion(area, tipText, p.thingIDNumber ^ 0x5A17);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("heart inspect tooltip", e);
            }
        }

        /// <summary>Where the heart's line sits in the inspect text, at this width.</summary>
        private static void Measure(string str, string line, float width)
        {
            measuredText = str;
            measuredLine = line;
            measuredWidth = width;
            int at = str.IndexOf(line, StringComparison.Ordinal);
            lineFound = at >= 0;
            if (!lineFound) return;
            var font = Text.Font;
            Text.Font = GameFont.Small;
            try
            {
                string before = at > 0 ? str.Substring(0, at).TrimEnd('\n', '\r') : null;
                lineTop = before.NullOrEmpty() ? 0f : Text.CalcHeight(before, width);
                float through = Text.CalcHeight(str.Substring(0, at + line.Length), width);
                lineHeight = Mathf.Max(Text.LineHeight, through - lineTop);
                textHeight = Text.CalcHeight(str, width);
            }
            finally
            {
                Text.Font = font;
            }
        }
    }
}
