using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The mood trend: a small graph of the colony's average mood and its lowest colonist's over the last three or seven
    /// days (from <see cref="HudComponent"/>'s hourly samples, saved with the game), with the line where minor breaks
    /// begin for most people (35%). Hover it for the time and the values at that point.
    /// </summary>
    public class HudMoodTrend : HudPanel
    {
        public const string PartAverage = "average", PartLowest = "lowest", PartLine = "line";
        public static readonly string[] Parts = { PartAverage, PartLowest, PartLine };

        /// <summary>The usual minor break line, drawn across the graph.</summary>
        private const float MinorLine = 0.35f;

        private readonly List<int> hours = new List<int>();
        private readonly List<float> avg = new List<float>();
        private readonly List<float> low = new List<float>();
        private int readVersion = -1, readDays = -1, nowHour;
        private string summary, rangeLabel, agoLabel, barLabel, barTip;

        private static readonly Color AvgColor = new Color(1f, 0.86f, 0.55f);
        private static readonly Color LowColor = new Color(0.93f, 0.55f, 0.35f);

        public HudMoodTrend() : base(HudLayout.Trend)
        {
        }

        protected override void Refresh()
        {
            var comp = HudComponent.Instance;
            nowHour = (Find.TickManager?.TicksGame ?? 0) / GenDate.TicksPerHour;
            if (comp == null) return;
            int days = St.days == 7 ? 7 : 3;
            if (comp.Version == readVersion && days == readDays) return;
            readVersion = comp.Version;
            readDays = days;
            int other = days == 7 ? 3 : 7;
            rangeLabel = "CC_Pnl_TrendDays".Translate(days).Resolve();
            agoLabel = "CC_Pnl_DaysAgo".Translate(days).Resolve();
            barLabel = "CC_Pnl_DaysShort".Translate(days).Resolve();
            barTip = "CC_Pnl_DaysTip".Translate(other).Resolve();
            hours.Clear();
            avg.Clear();
            low.Clear();
            int from = nowHour - days * 24;
            var samples = comp.Samples;
            // At most about a hundred points: a week is drawn every second hour.
            int step = days * 24 > 100 ? 2 : 1;
            for (int i = 0; i < samples.Count; i++)
            {
                HudMath.Unpack(samples[i], out int h, out float a, out float l);
                if (h < from) continue;
                if (step > 1 && i % step != 0 && i != samples.Count - 1) continue;
                hours.Add(h);
                avg.Add(a);
                low.Add(l);
            }
            summary = avg.Count == 0 ? null
                : "CC_Pnl_TrendNow".Translate(avg[avg.Count - 1].ToStringPercent(), low[low.Count - 1].ToStringPercent()).Resolve();
        }

        protected override Vector2 Measure(float s) => new Vector2(Mathf.Round(224f * s), Mathf.Round((St.compact ? 62f : 100f) * s));

        /// <summary>The y-axis' marks, drawn in a gutter inside the panel.</summary>
        private static readonly float[] AxisMarks = { 1f, 0.5f, 0f };
        private static string[] axisLabels;

        protected override void DrawBody(Rect r, float s)
        {
            var st = St;
            float pad = Mathf.Round(4f * s);
            float top = Mathf.Round(16f * s), bottom = st.compact ? 0f : Mathf.Round(13f * s);
            var area = new Rect(r.x + pad, r.y + pad + top, r.width - pad * 2f, r.height - pad * 2f - top - bottom);
            int days = st.days == 7 ? 7 : 3;
            var head = new Rect(r.x + pad, r.y + pad - 1f, r.width - pad * 2f, top);
            // Until there are two points there's no line to draw: say so instead of an empty graph.
            if (hours.Count < 2)
            {
                HudKit.Label(head, rangeLabel ?? "", UiKit.HeaderColor, GameFont.Tiny);
                Widgets.DrawBoxSolid(area, new Color(0f, 0f, 0f, 0.22f));
                Wrapped(area.ContractedBy(Mathf.Round(4f * s)), HudKit.T("CC_Pnl_TrendEmpty"), UiKit.Faint);
                Clicks(area);
                return;
            }
            // The y-axis' numbers in a gutter of their own, inside the panel; the compact graph goes without.
            float gutter = st.compact ? 0f : HudKit.Width("100%", GameFont.Tiny) + 3f;
            var graph = new Rect(area.x + gutter, area.y, area.width - gutter, area.height);
            // Top line: the range and the latest values, or the values under the mouse; never one over the other.
            int at = -1;
            if (Mouse.IsOver(graph))
            {
                float hourAtMouse = nowHour - days * 24 + (Event.current.mousePosition.x - graph.x) / Mathf.Max(1f, graph.width) * days * 24;
                at = HudMath.Nearest(hours, hourAtMouse);
            }
            if (at >= 0)
            {
                string when = nowHour - hours[at] <= 0 ? HudKit.T("CC_Pnl_Now") : "CC_Pnl_Ago".Translate(UiKit.Duration((nowHour - hours[at]) * GenDate.TicksPerHour)).Resolve();
                HudKit.Label(head, "CC_Pnl_TrendAt".Translate(when, avg[at].ToStringPercent(), low[at].ToStringPercent()).Resolve(), Color.white, GameFont.Tiny);
            }
            else
            {
                string label = rangeLabel ?? "";
                HudMath.FitThree(head.width, HudKit.Width(label, GameFont.Tiny), 0f, HudKit.Width(summary, GameFont.Tiny), 8f, out bool showRange, out _,
                    out bool showSummary);
                if (showRange) HudKit.Label(head, label, UiKit.HeaderColor, GameFont.Tiny);
                if (showSummary) HudKit.Label(head, summary, UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
            }
            Widgets.DrawBoxSolid(graph, new Color(0f, 0f, 0f, 0.22f));
            float X(int hour) => graph.x + Mathf.Clamp01((hour - (nowHour - days * 24)) / (float)(days * 24)) * graph.width;
            float Y(float v) => HudMath.GraphY(v, graph.y, graph.height);
            if (gutter > 0f)
            {
                float lh = HudKit.LineHeight(GameFont.Tiny);
                axisLabels ??= new[] { AxisMarks[0].ToStringPercent(), AxisMarks[1].ToStringPercent(), AxisMarks[2].ToStringPercent() };
                for (int i = 0; i < AxisMarks.Length; i++)
                {
                    float v = AxisMarks[i];
                    // Each number beside its line, kept inside the graph's height so none sits outside the panel.
                    float y = Mathf.Clamp(Y(v) - lh / 2f, graph.y - 2f, graph.yMax - lh + 2f);
                    HudKit.Label(new Rect(area.x, y, gutter - 3f, lh), axisLabels[i], UiKit.Dim, GameFont.Tiny, TextAnchor.MiddleRight);
                }
            }
            if (Event.current.type == EventType.Repaint)
            {
                // Faint day lines, and the minor break line dashed.
                for (int d = 1; d < days; d++)
                {
                    float dx = graph.x + graph.width * d / days;
                    Widgets.DrawBoxSolid(new Rect(dx, graph.y, 1f, graph.height), new Color(1f, 1f, 1f, 0.05f));
                }
                if (st.Shows(PartLine))
                {
                    float ly = Y(MinorLine);
                    for (float x = graph.x; x < graph.xMax; x += 7f)
                        Widgets.DrawBoxSolid(new Rect(x, ly, Mathf.Min(4f, graph.xMax - x), 1f), UiKit.WithAlpha(UiKit.Bad, 0.55f));
                }
                if (st.Shows(PartLowest)) Line(low, X, Y, LowColor, s);
                if (st.Shows(PartAverage)) Line(avg, X, Y, AvgColor, s);
                if (at >= 0)
                {
                    float hx = X(hours[at]);
                    Widgets.DrawBoxSolid(new Rect(hx, graph.y, 1f, graph.height), new Color(1f, 1f, 1f, 0.35f));
                    float dot = Mathf.Round(5f * s);
                    if (st.Shows(PartLowest)) UiKit.DiscFill(new Rect(hx - dot / 2f, Y(low[at]) - dot / 2f, dot, dot), LowColor);
                    if (st.Shows(PartAverage)) UiKit.DiscFill(new Rect(hx - dot / 2f, Y(avg[at]) - dot / 2f, dot, dot), AvgColor);
                }
            }
            if (bottom > 0f)
            {
                // Under the graph: where it starts, the key in the middle when there's room for it, and now.
                var foot = new Rect(graph.x, graph.yMax, graph.width, bottom);
                string ago = agoLabel ?? "", key = HudKit.T("CC_Pnl_TrendKey"), now = HudKit.T("CC_Pnl_Now");
                HudMath.FitThree(foot.width, HudKit.Width(ago, GameFont.Tiny), HudKit.Width(key, GameFont.Tiny), HudKit.Width(now, GameFont.Tiny), 6f,
                    out bool showAgo, out bool showKey, out bool showNow);
                if (showAgo) HudKit.Label(foot, ago, UiKit.Dim, GameFont.Tiny);
                if (showNow) HudKit.Label(foot, now, UiKit.Dim, GameFont.Tiny, TextAnchor.MiddleRight);
                if (showKey) HudKit.Label(foot, key, UiKit.Dim, GameFont.Tiny, TextAnchor.MiddleCenter);
            }
            TooltipHandler.TipRegion(graph, HudKit.T("CC_Pnl_TrendTip"));
            Clicks(graph);
        }

        private void Clicks(Rect r)
        {
            if (!Widgets.ButtonInvisible(r, false)) return;
            if (Event.current.button == 1) OpenMenu();
            else MainTabWindow_Council.OpenTab("Moods");
        }

        /// <summary>A short note wrapped over as many lines as it needs, centred in <paramref name="r"/>.</summary>
        private static void Wrapped(Rect r, string text, Color color)
        {
            var oldFont = Text.Font;
            var oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.WordWrap = true;
            GUI.color = color;
            Widgets.Label(r, text);
            GUI.color = Color.white;
            Text.WordWrap = oldWrap;
            Text.Anchor = oldAnchor;
            Text.Font = oldFont;
        }

        private void Line(List<float> values, System.Func<int, float> x, System.Func<float, float> y, Color c, float s)
        {
            float w = Mathf.Max(1f, 1.5f * s);
            for (int i = 1; i < values.Count; i++)
            {
                // A gap of more than a few hours (the colony was away, or the game skipped ahead) isn't joined up.
                if (hours[i] - hours[i - 1] > 6) continue;
                Widgets.DrawLine(new Vector2(x(hours[i - 1]), y(values[i - 1])), new Vector2(x(hours[i]), y(values[i])), c, w);
            }
        }

        protected override float HeaderButtons(Rect bar, float right)
        {
            var st = St;
            int other = st.days == 7 ? 3 : 7;
            return BarText(bar, right, barLabel ?? "", barTip,
                () => st.days = other);
        }

        protected override void MenuOptions(List<FloatMenuOption> options)
        {
            var st = St;
            int other = st.days == 7 ? 3 : 7;
            options.Add(new FloatMenuOption("CC_Pnl_ShowDays".Translate(other).Resolve(), () =>
            {
                st.days = other;
                MarkDirty();
                HudLayout.Save();
            }));
        }
    }
}
