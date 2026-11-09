using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The health panel in Rimfolk's own look: each colonist's row with the stripe and the wash when it matters (bleeding or
    /// soon deadly, untended), their name as a link and how they stand at the right in its colour, their summary health as a
    /// thin, muted line under the name in its colour (instead of the classic's small upright mark), and under it the
    /// conditions in the panel's main font with their bars, so the illness reads before the health line
    /// (the chips with the Health tab's icons and their little bars, or a line for each). The chips are laid out as the
    /// classic panel lays them (HudHealth.cs), once each time it gathers.
    /// </summary>
    public partial class HudHealth
    {
        private float BarRoom(float s) => HudLookMath.At(HudLookMath.HealthBar + 2f, s);

        private float HeightLook(Row row, float s)
        {
            if (!TwoLines) return RowH(s);
            if (LineEach) return NameH(s) + BarRoom(s) + Mathf.Max(1, row.lines.Count) * LineH(s) + HudLookMath.At(3f, s);
            return HudLookMath.HealthRow(true, row.measuredLines, s);
        }

        private Vector2 MeasureLook(float s)
        {
            float pad = HudLookMath.At(HudLookMath.Pad, s);
            float more = moreText != null ? HudLookMath.At(HudLookMath.MoreLine, s) : 0f;
            if (rows.Count == 0) return new Vector2(HudLookMath.At(150f, s), pad * 2f + HudLookMath.At(20f, s));
            bool chips = TwoLines && !LineEach;
            if (chipLines.Count > 200) chipLines.Clear();
            float h = 0f;
            foreach (var row in rows)
            {
                if (chips) row.measuredLines = chipLines.TryGetValue(row.pawn.thingIDNumber, out int n) ? n : 1;
                h += HeightLook(row, s);
            }
            return new Vector2(Width(s), pad * 2f + h + more);
        }

        private void DrawLook(Rect r, float s)
        {
            float pad = HudLookMath.At(HudLookMath.Pad, s);
            if (rows.Count == 0)
            {
                HudLook.Label(new Rect(r.x + pad, r.y + pad, r.width - pad * 2f, HudLookMath.At(20f, s)), HudKit.T("CC_Pnl_H_AllWell"), UiKit.Faint, HudKit.Minor(s));
                return;
            }
            var main = HudKit.Main(s);
            var minor = HudKit.Minor(s);
            bool each = TwoLines && LineEach;
            bool chips = TwoLines && !LineEach;
            float line1 = each ? NameH(s) : HudLookMath.At(18f, s);
            float barRoom = TwoLines ? BarRoom(s) : 0f;
            float y = r.y + pad;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (chips) FitChips(row, s);
                float rh = HeightLook(row, s);
                var rr = new Rect(r.x + 2f, y, r.width - 4f, rh);
                y += rh;
                var level = HudRank.LevelOf(row.score);
                if (level < HudLevel.Soon && Event.current.type == EventType.Repaint) UiKit.Zebra(rr, i);
                if (Over(rr)) HudLook.Hover(rr);
                HudLook.Stripe(rr, level, s);
                float x = rr.x + HudLookMath.At(HudLookMath.Stripe + 3f, s);
                float right = rr.xMax - HudLookMath.At(3f, s);
                float topH = TwoLines ? line1 : rh;
                float topY = rr.y + (TwoLines ? 1f : 0f);
                float sw = Mathf.Min(HudLook.Width(row.status, main) + 4f, rr.width * 0.5f);
                HudLook.Label(new Rect(right - sw, topY, sw, topH), row.status, row.statusColor, main, TextAnchor.MiddleRight);
                bool nameClick = NameLook(new Rect(x, topY, Mathf.Max(0f, right - sw - 4f - x), topH), row.name, ThingRefs.Of(row.pawn), main,
                    level >= HudLevel.Life ? UiKit.Danger : (Color?)null);
                // Their summary health as a quiet line under the name (along the row's foot when it has one line): thin and
                // muted, so the illnesses under it read first (the player: "the illness should have more prominence than the
                // health bar in this window").
                if (row.health >= 0f)
                {
                    float bh = Mathf.Max(1f, HudLookMath.At(HudLookMath.HealthBarQuiet, s));
                    float by = TwoLines ? rr.y + line1 + Mathf.Round((barRoom - bh) / 2f) : rr.yMax - bh - 1f;
                    float bw = TwoLines ? right - x : Mathf.Min(HudLookMath.At(60f, s), right - x);
                    HudLook.Bar(new Rect(x, by, bw, bh), row.health, UiKit.WithAlpha(row.healthColor, HudLookMath.HealthBarAlpha));
                }
                bool overItem = false;
                float below = rr.y + line1 + barRoom;
                if (each) overItem = DrawLines(new Rect(x, below, right - x, rh - line1 - barRoom), row, s);
                else if (chips && row.chips.Count > 0)
                    overItem = DrawChips(new Rect(rr.x + HudLookMath.At(4f, s), below - 1f, Mathf.Max(0f, right - rr.x - HudLookMath.At(4f, s)), rh - line1 - barRoom + 1f), row, s, minor);
                if (!overItem && Previews.Shown.IsNone) Tip(rr, row.tip);
                if (!nameClick && Clicked(rr)) Click(row.pawn);
            }
            if (moreText != null) MoreLook(new Rect(r.x + pad, y, r.width - pad * 2f, HudLookMath.At(HudLookMath.MoreLine, s)), moreText, hidden);
        }
    }
}
