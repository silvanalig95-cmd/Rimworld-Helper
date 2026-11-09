using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The mood board in Rimfolk's own look: a colonist's row is their portrait (ringed in the colour of the line their mood
    /// is under), their name as a link, and at its end the marks (a break, a heart worn down or near breaking, a wish, faith
    /// wavering, a day off or a bad afternoon under way, an empty weapon slot), all at one size; under the name the mood as
    /// a bar with their own break lines and a mark where it is heading, the number small at its end, the heart's line under
    /// it (strain in its band's colour with the band lines, the good side in its green), and what drags them down after it.
    /// An urgent row has the stripe and the wash. Compact: cells like raid frames, the mood filled, the break lines, the
    /// heart's band along the foot. Everything drawn was made when the board gathered.
    /// </summary>
    public partial class HudMoodBoard
    {
        private float numberWidth = -1f;
        private GameFont numberFont;

        /// <summary>The rows' facts in the look, and what drags each down as this board's parts say it.</summary>
        private void RefreshLook()
        {
            var st = St;
            bool drag = st.Shows(PartDrag), act = st.Shows(PartAct);
            foreach (var h in rows)
            {
                HudLookFacts.Of(h);
                var d = h.top;
                h.dragLook = d != null && (drag || act) && -d.mood >= 1f ? act ? drag ? d.WithAct : d.act ?? d.Short : d.Short : null;
            }
        }

        private Vector2 MeasureLook(float s)
        {
            float pad = HudLookMath.At(HudLookMath.Pad, s);
            bool more = moreText != null;
            if (rows.Count == 0) return new Vector2(HudLookMath.At(170f, s), HudLookMath.At(26f, s));
            if (St.compact)
            {
                int cols = HudMath.GridColumns(rows.Count, GridMaxColumns);
                int lines = (rows.Count + cols - 1) / cols;
                return new Vector2(pad * 2f + cols * CellW(s) + (cols - 1) * 2f,
                    pad * 2f + lines * CellH(s) + (lines - 1) * 2f + (more ? HudLookMath.At(HudLookMath.MoreLine, s) : 0f));
            }
            return new Vector2(FullWidth(s), HudLookMath.MoodsHeight(rows.Count, more, s));
        }

        private void DrawLook(Rect r, float s)
        {
            float pad = HudLookMath.At(HudLookMath.Pad, s);
            if (rows.Count == 0)
            {
                HudLook.Label(r.ContractedBy(pad), HudKit.T("CC_Pnl_NoOne"), UiKit.Faint, HudKit.Minor(s));
                return;
            }
            float y = r.y + pad;
            if (St.compact)
            {
                int cols = HudMath.GridColumns(rows.Count, GridMaxColumns);
                float cw = CellW(s), ch = CellH(s);
                for (int i = 0; i < rows.Count; i++) DrawCellLook(new Rect(r.x + pad + i % cols * (cw + 2f), y + i / cols * (ch + 2f), cw, ch), rows[i], s);
                y += ((rows.Count + cols - 1) / cols) * (ch + 2f) - 2f;
            }
            else
            {
                float rh = HudLookMath.At(HudLookMath.MoodRow, s);
                for (int i = 0; i < rows.Count; i++) DrawRowLook(new Rect(r.x + 2f, y + i * rh, r.width - 4f, rh), rows[i], s, i);
                y += rows.Count * rh;
            }
            if (moreText == null) return;
            var mr = new Rect(r.x + pad, y, r.width - pad * 2f, HudLookMath.At(HudLookMath.MoreLine, s));
            HudLook.Label(mr, moreText, Over(mr) ? UiKit.Ink : UiKit.Faint, GameFont.Tiny);
            Tip(mr, HudKit.T("CC_Pnl_MoreTip"));
            if (!Clicked(mr)) return;
            if (Event.current.button == 1) OpenMenu();
            else MainTabWindow_Council.OpenTab("Moods");
        }

        /// <summary>How much a colonist's row matters: a break or under the major line is life and limb, else the shared ranking's.</summary>
        private static HudLevel LevelOf(HudPawn h) => h.inBreak || h.now < h.major ? HudLevel.Life : HudRank.LevelOf(h.risk);

        private void DrawRowLook(Rect r, HudPawn h, float s, int index)
        {
            var st = St;
            var level = LevelOf(h);
            if (level < HudLevel.Soon && Event.current.type == EventType.Repaint) UiKit.Zebra(r, index);
            bool over = Over(r);
            if (over) HudLook.Hover(r);
            HudLook.Stripe(r, level, s);
            var main = HudKit.Main(s);
            bool icons = st.Shows(PartIcons);
            int marks = over ? 1 : icons ? MarkCount(h) : 0;
            float numW = 0f;
            if (st.Shows(PartNumber))
            {
                if (numberWidth < 0f || numberFont != GameFont.Tiny)
                {
                    numberFont = GameFont.Tiny;
                    numberWidth = HudLook.Width("100%", GameFont.Tiny) + 2f;
                }
                numW = numberWidth;
            }
            var l = HudLookMath.LayMoodRow(r.width, s, st.Shows(PartPortrait), marks, h.dragLook != null, numW);
            if (l.portraitSize > 0f)
            {
                var ring = h.inBreak || h.now < h.major ? UiKit.Danger : h.now < h.minor ? UiKit.Soon : UiKit.WithAlpha(UiKit.Ink, 0.25f);
                UiKit.Portrait(HudLook.Snap(new Rect(r.x + l.portraitX, r.y + l.portraitY, l.portraitSize, l.portraitSize)), h.pawn, ring);
            }
            // The first line: the name as a link (the alarm's colour in a break), and the marks or the Mood tab's button at its end.
            var nameRect = new Rect(r.x + l.nameX, r.y + l.nameY, Mathf.Max(0f, l.nameRight - l.nameX), l.nameH);
            float mark = HudLook.MarkSize(s);
            if (over)
            {
                var b = HudLook.IconAt(r.x + l.nameRight + 2f, nameRect.y, nameRect.height, mark + 2f);
                if (ImageButtonLook(b, Icons.Moods ?? TexButton.Info, h.moodTabTip))
                {
                    MoodView.ShowMoodTab(h.pawn);
                    Event.current.Use();
                }
            }
            else if (icons) MarksLook(new Rect(r.x + l.nameRight, nameRect.y, r.xMax - (r.x + l.nameRight) - HudLookMath.At(3f, s), nameRect.height), h, mark);
            bool nameClick = NameLook(nameRect, h.name, h.thing, main, h.inBreak ? UiKit.Danger : (Color?)null);
            // The second line: the mood bar, its number, the heart's line under it, what drags them down.
            var bar = new Rect(r.x + l.barX, r.y + l.barY, l.barW, l.barH);
            HudLook.MoodBar(bar, h.now, h.heading, h.minor, h.major, h.extreme);
            if (numW > 0f)
                HudLook.Label(new Rect(r.x + l.numberX, bar.y - HudLookMath.At(5f, s), numW, bar.height + HudLookMath.At(10f, s)), h.nowText,
                    HudLook.MoodInk(h.now, h.minor, h.major, h.extreme), GameFont.Tiny);
            if (h.heartOn) HudLook.HeartLine(new Rect(bar.x, r.y + l.heartY, bar.width, l.heartH), h.band, h.strain, h.good, h.goodOn);
            if (h.dragLook != null && l.dragW > 4f)
            {
                var c = h.risk >= HudRank.SoonFloor && h.top != null && h.top.fixable ? UiKit.Gold : UiKit.Faint;
                HudLook.Label(new Rect(r.x + l.dragX, bar.y - HudLookMath.At(5f, s), l.dragW, bar.height + HudLookMath.At(10f, s)), h.dragLook, c, HudKit.Minor(s));
            }
            if (h.heartOn && !h.heartTip.NullOrEmpty())
                Tip(new Rect(bar.x, bar.y - 2f, bar.width, l.heartY + l.heartH - l.barY + 4f), h.heartTip);
            if (Previews.Shown.IsNone && h.tipLook != null) Tip(r, new TipSignal(h.tipLook, h.pawn.thingIDNumber ^ 0x48D7));
            if (!nameClick && Clicked(r)) Click(h.pawn);
        }

        /// <summary>How many marks a colonist's row carries now.</summary>
        private static int MarkCount(HudPawn h)
        {
            int n = 0;
            if (h.inBreak) n++;
            if (h.band >= StrainBand.Worn) n++;
            if (h.wish != null) n++;
            if (h.wavering) n++;
            if (h.off != TimeOffKind.None) n++;
            if (h.kitGap) n++;
            return n;
        }

        /// <summary>The marks at the end of the name's line, right to left in the order they matter, each with its words.</summary>
        private void MarksLook(Rect line, HudPawn h, float size)
        {
            float x = line.xMax;
            void Mark(Texture2D tex, Color c, string tip)
            {
                if (tex == null) return;
                x -= size + 1f;
                var mr = HudLook.IconAt(x, line.y, line.height, size);
                HudLook.Icon(mr, tex, c);
                Tip(mr, tip);
            }
            if (h.inBreak) Mark(Icons.Warning, UiKit.Danger, h.breakTip);
            if (h.band >= StrainBand.Worn) Mark(Icons.Heart, HeartView.BandColor(h.band), h.bandTip);
            if (h.off != TimeOffKind.None) Mark(HudIcons.DayOff ?? Icons.Heart, h.off == TimeOffKind.BadDay ? UiKit.Soon : UiKit.Cool, h.offTip);
            if (h.kitGap) Mark(HudIcons.KitGap ?? Icons.Defenses, UiKit.Soon, h.kitTip);
            if (h.wish != null) Mark(Icons.Wishes, h.wish.stage >= 2 ? UiKit.Soon : UiKit.Gold, h.wishTip);
            if (h.wavering) Mark(Icons.CatIdeology, UiKit.Faint, HudKit.T("CC_Pnl_C_Wavering"));
        }

        /// <summary>A compact cell: the mood filled in its colour, the break lines, the heart's band along its foot, the initials.</summary>
        private void DrawCellLook(Rect r, HudPawn h, float s)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Widgets.DrawBoxSolid(r, UiKit.MeterBack);
                var c = HudKit.MoodColor(h.now, h.minor, h.major, h.extreme);
                // Light enough under the initials (white on amber at half strength falls under 4.5:1); the major line's pulse kept.
                float a = h.now < h.major ? 0.3f + 0.15f * HudKit.Pulse : 0.4f;
                Widgets.DrawBoxSolid(new Rect(r.x, r.y, Mathf.Max(1f, Mathf.Round(r.width * Mathf.Clamp01(h.now))), r.height), UiKit.WithAlpha(c, a));
                Widgets.DrawBoxSolid(new Rect(Mathf.Round(r.x + r.width * h.minor), r.y, 1f, r.height), HudKit.MinorLine);
                Widgets.DrawBoxSolid(new Rect(Mathf.Round(r.x + r.width * h.major), r.y, 1f, r.height), HudKit.MajorLine);
                if (Mathf.Abs(h.heading - h.now) >= 0.01f)
                    Widgets.DrawBoxSolid(new Rect(Mathf.Round(r.x + r.width * Mathf.Clamp01(h.heading)) - 1f, r.y, 2f, r.height), h.heading > h.now ? UiKit.Good : UiKit.Bad);
                if (h.heartOn && h.band >= StrainBand.Strained)
                    Widgets.DrawBoxSolid(new Rect(r.x, r.yMax - 2f, r.width, 2f), HeartView.BandColor(h.band));
                if (h.inBreak) UiKit.RoundEdge(r, UiKit.Danger, SkinMath.RadiusSmall);
            }
            if (Over(r)) HudLook.Hover(r);
            HudLook.Label(r, h.initials, UiKit.Ink, GameFont.Tiny, TextAnchor.MiddleCenter);
            if (h.tipLook != null) Tip(r, new TipSignal(h.tipLook, h.pawn.thingIDNumber ^ 0x48D8));
            if (Clicked(r)) Click(h.pawn);
        }
    }
}
