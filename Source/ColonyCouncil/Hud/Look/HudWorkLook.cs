using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Work and idle in Rimfolk's own look: the colony's lines on top with the stripe and their icon in the colour of how
    /// much they matter; then a row each, the stripe and the wash when it matters, their name as a link, what they are doing
    /// faint after it, and at the right their mark as a small chip (idle and for how long in amber, drafted in the accent,
    /// down in amber, a break in the danger colour; a day off or a bad afternoon under way in its place, so their idling is
    /// explained) and food, rest and recreation as three short bars, one over the other, a low one in the danger colour.
    /// </summary>
    public partial class HudWork
    {
        private void RefreshLook()
        {
            foreach (var row in rows)
            {
                row.off = TimeOffKind.None;
                try
                {
                    var heart = Heart.ForView(row.pawn);
                    row.off = HeartTimeOff.KindOf(heart);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("work panel: time off", e);
                }
            }
        }

        private void DrawLook(Rect r, float s)
        {
            var st = St;
            float pad = HudLookMath.At(HudLookMath.Pad, s);
            float y = r.y + pad;
            var main = HudKit.Main(s);
            var minor = HudKit.Minor(s);
            float icon = HudLook.IconSize(s);
            if (colony.Count > 0)
            {
                float lh = HudLookMath.At(HudLookMath.WorkLine, s);
                foreach (var l in colony)
                {
                    var lr = new Rect(r.x + 2f, y, r.width - 4f, lh);
                    var level = HudRank.LevelOf(l.score);
                    var c = level >= HudLevel.Soon ? HudLook.LevelColor(level) : UiKit.Soon;
                    if (Over(lr)) HudLook.Hover(lr);
                    HudLook.Stripe(lr, level >= HudLevel.Soon ? level : HudLevel.Soon, s);
                    float x = lr.x + HudLookMath.At(HudLookMath.Stripe + 3f, s);
                    if (l.icon != null) HudLook.Icon(HudLook.IconAt(x, lr.y, lh, icon), l.icon, c);
                    HudLook.Label(new Rect(x + icon + HudLookMath.At(5f, s), lr.y, Mathf.Max(0f, lr.xMax - x - icon - 8f), lh), l.text, c, minor);
                    Tip(lr, l.tip);
                    if (Clicked(lr))
                    {
                        if (Event.current.button == 1) OpenMenu();
                        else
                            try
                            {
                                l.click?.Invoke();
                            }
                            catch (Exception e)
                            {
                                CouncilLog.ErrorOnce("work panel: click", e);
                            }
                    }
                    y += lh;
                }
                if (Event.current.type == EventType.Repaint) UiKit.Divider(r.x + pad, y + HudLookMath.At(1f, s), r.width - pad * 2f);
                y += HudLookMath.At(4f, s);
            }
            if (rows.Count == 0)
            {
                HudLook.Label(new Rect(r.x + pad, y, r.width - pad * 2f, HudLookMath.At(20f, s)), HudKit.T("CC_Pnl_NoOne"), UiKit.Faint, minor);
                return;
            }
            float rh = HudLookMath.At(HudLookMath.WorkRow, s);
            bool doing = !st.compact && st.Shows(PartDoing);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var rr = new Rect(r.x + 2f, y + i * rh, r.width - 4f, rh);
                var level = HudRank.LevelOf(row.score);
                if (level < HudLevel.Soon && Event.current.type == EventType.Repaint) UiKit.Zebra(rr, i);
                if (Over(rr)) HudLook.Hover(rr);
                HudLook.Stripe(rr, level, s);
                float x = rr.x + HudLookMath.At(HudLookMath.Stripe + 3f, s);
                float right = rr.xMax - HudLookMath.At(3f, s);
                right = NeedsLook(rr, right, row, s, st);
                // The mark as a chip: time off under way in the place of idle, else idle, drafted, down or a break.
                string mark = row.off == TimeOffKind.DayOff ? HudKit.T("CC_Pnl_Look_DayOff") : row.off == TimeOffKind.BadDay ? HudKit.T("CC_Pnl_Look_BadDay") : row.mark;
                if (!mark.NullOrEmpty())
                {
                    var mc = row.off == TimeOffKind.DayOff ? UiKit.Cool : row.off == TimeOffKind.BadDay || row.markColor == BarLow ? UiKit.Soon : row.markColor;
                    float mw = Mathf.Min(HudLook.Width(mark, GameFont.Tiny) + HudLookMath.At(8f, s), rr.width * 0.34f);
                    float ch = Mathf.Min(rh - 4f, HudLookMath.At(15f, s));
                    var chip = new Rect(right - mw, Mathf.Round(rr.y + (rh - ch) / 2f), mw, ch);
                    // The chip in the mark's colour, its word in ink on it (a coloured word on its own tint would fall under 4.5:1).
                    if (Event.current.type == EventType.Repaint) UiKit.Round(chip, UiKit.WithAlpha(mc, 0.24f), SkinMath.RadiusSmall);
                    HudLook.Label(chip, mark, UiKit.Ink, GameFont.Tiny, TextAnchor.MiddleCenter);
                    right -= mw + HudLookMath.At(4f, s);
                }
                float nameW = doing ? Mathf.Min(HudLookMath.At(66f, s), HudLook.Width(row.name, main) + 4f) : right - x;
                bool nameClick = NameLook(new Rect(x, rr.y, nameW, rh), row.name, ThingRefs.Of(row.pawn), main, level >= HudLevel.Life ? UiKit.Danger : (Color?)null);
                if (doing && !row.doing.NullOrEmpty())
                {
                    float dx = x + nameW + HudLookMath.At(4f, s);
                    HudLook.Label(new Rect(dx, rr.y, Mathf.Max(0f, right - dx - 2f), rh), row.doing, row.idle && row.off == TimeOffKind.None ? UiKit.Soon : UiKit.Faint, minor);
                }
                if (Previews.Shown.IsNone) Tip(rr, row.tip);
                if (nameClick || !Clicked(rr)) continue;
                if (Event.current.button == 1) OpenMenu();
                else
                {
                    HudPeople.Go(row.pawn);
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
            }
            if (moreText != null) MoreLook(new Rect(r.x + pad, y + rows.Count * rh, r.width - pad * 2f, HudLookMath.At(HudLookMath.MoreLine, s)), moreText, hidden);
        }

        /// <summary>Food, rest and recreation as three short bars one over the other, a low one in the danger colour; returns where they begin.</summary>
        private static float NeedsLook(Rect rr, float right, Row row, float s, HudPanelState st)
        {
            int n = (st.Shows(PartFood) && row.food >= 0f ? 1 : 0) + (st.Shows(PartRest) && row.rest >= 0f ? 1 : 0) + (st.Shows(PartJoy) && row.joy >= 0f ? 1 : 0);
            if (n == 0) return right;
            float bw = HudLookMath.At(20f, s), bh = Mathf.Max(2f, HudLookMath.At(3f, s)), gap = Mathf.Max(1f, HudLookMath.At(2f, s));
            float total = n * bh + (n - 1) * gap;
            float y = Mathf.Round(rr.y + (rr.height - total) / 2f);
            float x = right - bw;
            void One(float level, bool low)
            {
                HudLook.Bar(new Rect(x, y, bw, bh), level, low ? UiKit.Danger : UiKit.WithAlpha(UiKit.Good, 0.85f));
                y += bh + gap;
            }
            if (st.Shows(PartFood) && row.food >= 0f) One(row.food, row.foodLow);
            if (st.Shows(PartRest) && row.rest >= 0f) One(row.rest, row.restLow);
            if (st.Shows(PartJoy) && row.joy >= 0f) One(row.joy, row.joyLow);
            return x - HudLookMath.At(5f, s);
        }
    }
}
