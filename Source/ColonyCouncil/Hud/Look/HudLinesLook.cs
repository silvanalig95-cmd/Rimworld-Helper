using System;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The vitals, the council at a glance and Following in Rimfolk's own look: each line its icon at one size, a coloured
    /// stripe and a wash when it matters (red for life and limb, pulsing; amber for soon), its words, and at the right its
    /// value; a value with a range (days of food against the warning line, medicine against the advisor's, the research under
    /// way, the colony's trust either side of nothing) as a short bar with the number small beside it. Compact: two to a row,
    /// the icon and the value, the bar under it. Everything drawn was made when the panel gathered.
    /// </summary>
    public abstract partial class HudLinesPanel
    {
        private Vector2 MeasureLook(float s)
        {
            float w = shown.Count == 0 ? HudLookMath.At(190f, s) : St.compact ? HudLookMath.At(HudLookMath.Pad, s) * 2f + CellW(s) * 2f + 2f : Mathf.Round(FullWidth * s);
            return new Vector2(w, HudLookMath.LinesHeight(shown.Count, moreText != null, St.compact, s));
        }

        private void DrawLook(Rect r, float s)
        {
            float pad = HudLookMath.At(HudLookMath.Pad, s);
            float rh = HudLookMath.At(HudLookMath.LineRow, s);
            if (shown.Count == 0)
            {
                HudLook.Label(new Rect(r.x + pad, r.y + pad, r.width - pad * 2f, rh), Empty ?? "", UiKit.Faint, HudKit.Minor(s));
                return;
            }
            var main = HudKit.Main(s);
            float icon = HudLook.IconSize(s);
            float y = r.y + pad;
            bool compact = St.compact;
            for (int i = 0; i < shown.Count; i++)
            {
                var l = shown[i];
                Rect row = compact
                    ? new Rect(r.x + pad + (i % 2) * (CellW(s) + 2f), y + (i / 2) * rh, CellW(s), rh)
                    : new Rect(r.x + 2f, y + i * rh, r.width - 4f, rh);
                var level = l.Level;
                bool over = Over(row);
                if (over) HudLook.Hover(row);
                HudLook.Stripe(row, level, s);
                var c = level >= HudLevel.Soon ? HudLook.LevelColor(level) : l.quiet;
                float x = row.x + HudLookMath.At(HudLookMath.Stripe + 3f, s);
                if (l.icon != null) HudLook.Icon(HudLook.IconAt(x, row.y, rh, icon), l.icon, level >= HudLevel.Soon ? c : UiKit.WithAlpha(UiKit.Heading, 0.85f));
                x += icon + HudLookMath.At(5f, s);
                bool hasBar = l.fill >= 0f || l.spanFrom >= 0f;
                bool nameClick = false;
                if (compact)
                {
                    string shownText = l.value ?? l.text;
                    var cr = new Rect(x, row.y, Mathf.Max(0f, row.xMax - x - 2f), rh);
                    HudLook.Label(cr, shownText, l.value != null ? ValueColorLook(l, level) : level >= HudLevel.Soon ? c : UiKit.Ink, GameFont.Tiny);
                    if (hasBar) LineBar(new Rect(x, row.yMax - HudLookMath.At(4f, s), Mathf.Max(0f, row.xMax - x - 4f), Mathf.Max(2f, HudLookMath.At(2f, s))), l, level);
                }
                else
                {
                    float right = row.xMax - HudLookMath.At(3f, s);
                    if (!l.value.NullOrEmpty())
                    {
                        // A value with a bar is said small beside it; one without keeps the line's own size.
                        var vf = hasBar ? GameFont.Tiny : main;
                        float vw = Mathf.Min(HudLook.Width(l.value, vf) + 4f, row.width * 0.45f);
                        HudLook.Label(new Rect(right - vw, row.y, vw, rh), l.value, ValueColorLook(l, level), vf, TextAnchor.MiddleRight);
                        right -= vw + HudLookMath.At(3f, s);
                    }
                    if (hasBar)
                    {
                        float bw = HudLookMath.At(48f, s);
                        float bh = Mathf.Max(3f, HudLookMath.At(4f, s));
                        if (right - bw > x + HudLookMath.At(40f, s))
                        {
                            LineBar(new Rect(right - bw, Mathf.Round(row.y + (rh - bh) / 2f), bw, bh), l, level);
                            right -= bw + HudLookMath.At(5f, s);
                        }
                    }
                    var tr = new Rect(x, row.y, Mathf.Max(0f, right - x), rh);
                    if (!l.thing.IsNone && level < HudLevel.Soon) nameClick = NameLook(tr, l.text, l.thing, main);
                    else HudLook.Label(tr, l.text, level >= HudLevel.Soon ? c : l.quiet, main);
                }
                string full = compact ? l.compactTip ?? l.tip : l.tip;
                // A line that is a link shows its preview instead of its tooltip while the preview shows.
                if (!full.NullOrEmpty() && (l.thing.IsNone || Previews.Shown != l.thing)) Tip(row, full);
                if (nameClick || !Clicked(row)) continue;
                if (Event.current.button == 1) OpenMenu();
                else
                    try
                    {
                        l.click?.Invoke();
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("on-screen panel click", e);
                    }
            }
            if (moreText != null)
            {
                float my = compact ? y + (shown.Count + 1) / 2 * rh : y + shown.Count * rh;
                MoreLook(new Rect(r.x + pad, my, r.width - pad * 2f, HudLookMath.At(HudLookMath.MoreLine, s)), moreText, hidden);
            }
        }

        /// <summary>The value's colour: its own (the temperature's band), else the line's when it matters, else the ink.</summary>
        private static Color ValueColorLook(HudLine l, HudLevel level) => l.valueColor ?? (level >= HudLevel.Soon ? HudLook.LevelColor(level) : UiKit.Ink);

        /// <summary>A line's bar: its share (or its span either side of the middle) in its colour, the lines where something begins.</summary>
        private static void LineBar(Rect bar, HudLine l, HudLevel level)
        {
            var fc = l.fillColor ?? (level >= HudLevel.Soon ? HudLook.LevelColor(level) : UiKit.Good);
            if (l.spanFrom >= 0f) HudLook.Span(bar, l.spanFrom, l.spanTo, fc);
            else HudLook.Bar(bar, l.fill, fc);
            if (l.tickA >= 0f) HudLook.Tick(bar, l.tickA, UiKit.WithAlpha(UiKit.Ink, 0.7f));
            if (l.tickB >= 0f) HudLook.Tick(bar, l.tickB, UiKit.WithAlpha(UiKit.Sour, 0.9f));
        }
    }
}
