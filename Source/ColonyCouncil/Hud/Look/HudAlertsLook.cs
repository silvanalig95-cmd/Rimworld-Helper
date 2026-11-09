using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The alerts in Rimfolk's own look: each row a stripe in its priority's colour, the warning icon on the critical (in the
    /// danger colour, the row's wash pulsing as the game's critical alerts do) and the high ones (in amber), the label, and
    /// the count small at the right; a new high or critical alert flashes for a moment as before. The count's words and the
    /// tooltip's maker are made when the panel gathers; the explanation itself only while its tooltip shows (as before).
    /// </summary>
    public partial class HudAlerts
    {
        private void RefreshLook()
        {
            foreach (var row in rows)
            {
                row.countText = row.showCount ? "×" + row.count : null;
                var alert = row.alert;
                row.tip = () => Explain(alert);
            }
        }

        private Vector2 MeasureLook(float s)
        {
            bool compact = St.compact;
            float w = rows.Count == 0 ? HudLookMath.At(readOk ? 150f : 220f, s) : Width(s);
            return new Vector2(w, HudLookMath.AlertsHeight(rows.Count, moreText != null, compact, s));
        }

        private static Color ColorLook(int priority) => priority >= 2 ? UiKit.Danger : priority == 1 ? UiKit.Soon : UiKit.Ink;

        private void DrawLook(Rect r, float s)
        {
            float pad = HudLookMath.At(HudLookMath.Pad, s);
            bool compact = St.compact;
            float rh = HudLookMath.At(compact ? HudLookMath.AlertRowCompact : HudLookMath.AlertRow, s);
            if (rows.Count == 0)
            {
                HudLook.Label(new Rect(r.x + pad, r.y + pad, r.width - pad * 2f, rh), HudKit.T(readOk ? "CC_Pnl_NoAlerts" : "CC_Pnl_AlertsUnreadable"), UiKit.Faint,
                    HudKit.Minor(s));
                return;
            }
            var font = compact ? GameFont.Tiny : HudKit.Main(s);
            float icon = compact ? HudLook.MarkSize(s) : HudLook.IconSize(s);
            float now = Time.realtimeSinceStartup;
            float y = r.y + pad;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var rr = new Rect(r.x + 2f, y + i * rh, r.width - 4f, rh);
                var c = ColorLook(row.priority);
                if (Event.current.type == EventType.Repaint)
                {
                    // New: a flash that fades, as the game's alert bounces in (a moment, then the plain card under its words).
                    float age = now - row.seen;
                    if (row.priority >= 1 && row.seen > 0f && age >= 0f && age < FlashFor) Widgets.DrawBoxSolid(rr, UiKit.WithAlpha(c, 0.4f * (1f - age / FlashFor)));
                }
                if (Over(rr)) HudLook.Hover(rr);
                // The stripe in the priority's colour, pulsing for a critical one as the game's own critical alerts do.
                HudLook.Stripe(rr, row.priority >= 2 ? UiKit.WithAlpha(c, 0.6f + 0.4f * HudKit.Pulse) : row.priority == 1 ? c : UiKit.WithAlpha(UiKit.Ink, 0.22f), s);
                float x = rr.x + HudLookMath.At(HudLookMath.Stripe + 3f, s);
                if (row.priority >= 1)
                {
                    HudLook.Icon(HudLook.IconAt(x, rr.y, rh, icon), Icons.Warning, c);
                    x += icon + HudLookMath.At(4f, s);
                }
                float right = rr.xMax - HudLookMath.At(3f, s);
                if (row.countText != null)
                {
                    float nw = HudLook.Width(row.countText, GameFont.Tiny) + 4f;
                    HudLook.Label(new Rect(right - nw, rr.y, nw, rh), row.countText, UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                    right -= nw + 2f;
                }
                HudLook.Label(new Rect(x, rr.y, Mathf.Max(0f, right - x), rh), row.label, c, font);
                if (row.tip != null) Tip(rr, new TipSignal(row.tip, Gen.HashCombineInt(0x5A1E7A, row.order ^ (row.alert.GetHashCode() & 0xFFFF))));
                if (Clicked(rr))
                {
                    if (Event.current.button == 1) RowMenu(row.alert);
                    else Click(row.alert);
                }
            }
            if (moreText == null) return;
            var mr = new Rect(r.x + pad, y + rows.Count * rh, r.width - pad * 2f, HudLookMath.At(HudLookMath.MoreLine, s));
            HudLook.Label(mr, moreText, UiKit.Faint, GameFont.Tiny);
            Tip(mr, HudKit.T("CC_Pnl_MoreAlertsTip"));
        }
    }
}
