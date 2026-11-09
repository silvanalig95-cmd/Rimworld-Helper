using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The mood trend in Rimfolk's own look: above the graph the latest average and lowest, each beside a dot in its line's
    /// colour (the values under the mouse instead while it is on the graph); the graph on the look's dark track with the
    /// part under the usual minor break line washed in the bad colour and the line itself drawn once, the day lines faint,
    /// the average in the accent and the lowest in the bad colour; the scale in a gutter; when it starts and now under it.
    /// Every word, the values under the mouse at each point included, made when the samples change (once an hour).
    /// </summary>
    public partial class HudMoodTrend
    {
        private int lookVersion = -1, lookDays = -1;
        private string avgText, lowText;
        private string[] atTexts;

        /// <summary>The look's words, made again only when the samples or the range changed.</summary>
        private void RefreshLook()
        {
            if (lookVersion == readVersion && lookDays == readDays && atTexts != null && atTexts.Length == hours.Count) return;
            lookVersion = readVersion;
            lookDays = readDays;
            axisLabels ??= new[] { AxisMarks[0].ToStringPercent(), AxisMarks[1].ToStringPercent(), AxisMarks[2].ToStringPercent() };
            avgText = avg.Count == 0 ? null : "CC_Pnl_Look_Avg".Translate(HudLookFacts.Percent(avg[avg.Count - 1])).Resolve();
            lowText = low.Count == 0 ? null : "CC_Pnl_Look_Low".Translate(HudLookFacts.Percent(low[low.Count - 1])).Resolve();
            atTexts = new string[hours.Count];
            for (int i = 0; i < hours.Count; i++)
            {
                string when = nowHour - hours[i] <= 0 ? HudKit.T("CC_Pnl_Now") : "CC_Pnl_Ago".Translate(UiKit.Duration((nowHour - hours[i]) * GenDate.TicksPerHour)).Resolve();
                atTexts[i] = "CC_Pnl_TrendAt".Translate(when, HudLookFacts.Percent(avg[i]), HudLookFacts.Percent(low[i])).Resolve();
            }
        }

        private void DrawLook(Rect r, float s)
        {
            var st = St;
            float pad = HudLookMath.At(HudLookMath.Pad, s);
            float top = HudLookMath.At(16f, s), bottom = st.compact ? 0f : HudLookMath.At(13f, s);
            var area = new Rect(r.x + pad, r.y + pad + top, r.width - pad * 2f, r.height - pad * 2f - top - bottom);
            int days = st.days == 7 ? 7 : 3;
            var head = new Rect(r.x + pad, r.y + pad - 1f, r.width - pad * 2f, top);
            if (hours.Count < 2 || atTexts == null || atTexts.Length != hours.Count)
            {
                HudLook.Label(head, rangeLabel ?? "", UiKit.Heading, GameFont.Tiny);
                if (Event.current.type == EventType.Repaint) Widgets.DrawBoxSolid(area, UiKit.MeterBack);
                Wrapped(area.ContractedBy(HudLookMath.At(4f, s)), HudKit.T("CC_Pnl_TrendEmpty"), UiKit.Faint);
                Clicks(area);
                return;
            }
            float gutter = st.compact ? 0f : HudLook.Width("100%", GameFont.Tiny) + 3f;
            var graph = new Rect(area.x + gutter, area.y, area.width - gutter, area.height);
            int at = -1;
            if (Over(graph))
            {
                float hourAtMouse = nowHour - days * 24 + (Event.current.mousePosition.x - graph.x) / Mathf.Max(1f, graph.width) * days * 24;
                at = HudMath.Nearest(hours, hourAtMouse);
            }
            if (at >= 0) HudLook.Label(head, atTexts[at], UiKit.Ink, GameFont.Tiny);
            else Key(head, s, st);
            float X(int hour) => graph.x + Mathf.Clamp01((hour - (nowHour - days * 24)) / (float)(days * 24)) * graph.width;
            float Y(float v) => HudMath.GraphY(v, graph.y, graph.height);
            if (gutter > 0f && axisLabels != null)
            {
                float lh = HudKit.LineHeight(GameFont.Tiny);
                for (int i = 0; i < AxisMarks.Length; i++)
                {
                    float y = Mathf.Clamp(Y(AxisMarks[i]) - lh / 2f, graph.y - 2f, graph.yMax - lh + 2f);
                    HudLook.Label(new Rect(area.x, y, gutter - 3f, lh), axisLabels[i], UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                }
            }
            if (Event.current.type == EventType.Repaint)
            {
                Widgets.DrawBoxSolid(graph, UiKit.MeterBack);
                for (int d = 1; d < days; d++)
                    Widgets.DrawBoxSolid(new Rect(Mathf.Round(graph.x + graph.width * d / days), graph.y, 1f, graph.height), UiKit.WithAlpha(UiKit.Ink, 0.06f));
                if (st.Shows(PartLine))
                {
                    // Under the usual minor break line: a faint wash, and the line once across.
                    float ly = Mathf.Round(Y(MinorLine));
                    Widgets.DrawBoxSolid(new Rect(graph.x, ly, graph.width, graph.yMax - ly), UiKit.WithAlpha(UiKit.Sour, 0.07f));
                    Widgets.DrawBoxSolid(new Rect(graph.x, ly, graph.width, 1f), UiKit.WithAlpha(UiKit.Sour, 0.5f));
                }
                if (st.Shows(PartLowest)) LineLook(low, graph, days, UiKit.Sour, s);
                if (st.Shows(PartAverage)) LineLook(avg, graph, days, UiKit.Gold, s);
                if (at >= 0)
                {
                    float hx = Mathf.Round(X(hours[at]));
                    Widgets.DrawBoxSolid(new Rect(hx, graph.y, 1f, graph.height), UiKit.WithAlpha(UiKit.Ink, 0.35f));
                    float dot = HudLookMath.At(5f, s);
                    if (st.Shows(PartLowest)) UiKit.DiscFill(new Rect(hx - dot / 2f, Y(low[at]) - dot / 2f, dot, dot), UiKit.Sour);
                    if (st.Shows(PartAverage)) UiKit.DiscFill(new Rect(hx - dot / 2f, Y(avg[at]) - dot / 2f, dot, dot), UiKit.Gold);
                }
            }
            if (bottom > 0f)
            {
                var foot = new Rect(graph.x, graph.yMax, graph.width, bottom);
                string ago = agoLabel ?? "", now = HudKit.T("CC_Pnl_Now");
                HudLook.Label(foot, ago, UiKit.Faint, GameFont.Tiny);
                if (HudLook.Width(ago, GameFont.Tiny) + HudLook.Width(now, GameFont.Tiny) + 8f <= foot.width) HudLook.Label(foot, now, UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
            }
            Tip(graph, HudKit.T("CC_Pnl_TrendTip"));
            Clicks(graph);
        }

        /// <summary>A series as a line, its points worked out here (no delegate made a frame); a gap of more than a few hours isn't joined.</summary>
        private void LineLook(System.Collections.Generic.List<float> values, Rect graph, int days, Color c, float s)
        {
            float w = Mathf.Max(1f, 1.5f * s);
            float from = nowHour - days * 24, span = days * 24f;
            for (int i = 1; i < values.Count; i++)
            {
                if (hours[i] - hours[i - 1] > 6) continue;
                var a = new Vector2(graph.x + Mathf.Clamp01((hours[i - 1] - from) / span) * graph.width, HudMath.GraphY(values[i - 1], graph.y, graph.height));
                var b = new Vector2(graph.x + Mathf.Clamp01((hours[i] - from) / span) * graph.width, HudMath.GraphY(values[i], graph.y, graph.height));
                Widgets.DrawLine(a, b, c, w);
            }
        }

        /// <summary>The latest values above the graph: the average and the lowest, each beside a dot in its line's colour.</summary>
        private void Key(Rect head, float s, HudPanelState st)
        {
            float dot = Mathf.Max(5f, HudLookMath.At(6f, s));
            float x = head.x;
            void One(string text, Color c)
            {
                if (text == null) return;
                if (Event.current.type == EventType.Repaint) UiKit.DiscFill(new Rect(Mathf.Round(x), Mathf.Round(head.y + (head.height - dot) / 2f), dot, dot), c);
                x += dot + 3f;
                float w = HudLook.Width(text, GameFont.Tiny);
                HudLook.Label(new Rect(x, head.y, Mathf.Max(0f, head.xMax - x), head.height), text, UiKit.Ink, GameFont.Tiny);
                x += w + HudLookMath.At(10f, s);
            }
            if (st.Shows(PartAverage)) One(avgText, UiKit.Gold);
            if (st.Shows(PartLowest)) One(lowText, UiKit.Sour);
        }
    }
}
