using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The mood board: a row for each colonist with their portrait and name, a mood bar with their own break lines, an
    /// arrow and the number for where it's heading, and, faint under it, the mood dragging them down most ("-8 No
    /// bedroom"). Small marks for a mental break, high strain, an open wish and wavering faith. Most at risk first (the
    /// shared ranking) or in the colonist bar's order; rows go orange and red under their break lines, with a soft pulse
    /// under the major one. Click a row to jump to them, Shift-click (or the small button) for their Mood tab, hover for
    /// their five biggest moods. Compact: thin bars with initials in a grid, like raid frames.
    /// </summary>
    public class HudMoodBoard : HudPanel
    {
        // Parts that can be switched off (HudPanelState.hidden).
        public const string PartPortrait = "portrait", PartDrag = "drag", PartAct = "act", PartIcons = "icons", PartNumber = "number";
        public static readonly string[] Parts = { PartPortrait, PartNumber, PartDrag, PartAct, PartIcons };

        private readonly List<HudPawn> rows = new List<HudPawn>();
        private int hidden;
        private string moreText;

        public HudMoodBoard() : base(HudLayout.Moods)
        {
        }

        protected override void Refresh()
        {
            rows.Clear();
            var st = St;
            var people = HudMood.Who(st.who);
            var order = st.sort == HudSort.Bar ? HudMood.BarOrder() : null;
            var all = new List<HudPawn>(people.Count);
            foreach (var p in people)
            {
                var h = HudMood.For(p);
                if (h == null) continue;
                h.barOrder = order != null && order.TryGetValue(p, out int i) ? i : int.MaxValue;
                all.Add(h);
            }
            if (st.sort == HudSort.Bar) all = all.OrderBy(h => h.barOrder).ThenBy(h => h.pawn.thingIDNumber).ToList();
            else all = all.OrderByDescending(h => h.risk).ThenBy(h => h.now - h.minor).ThenBy(h => h.pawn.thingIDNumber).ToList();
            var keep = HudMath.Keep(all.Select(h => h.risk).ToList(), st.maxRows, HudRank.LifeFloor, out hidden);
            foreach (int i in keep) rows.Add(all[i]);
            moreText = hidden > 0 ? "CC_Pnl_AndMore".Translate(hidden).Resolve() : null;
        }

        // ---------------- Sizes ----------------

        private static float RowH(float s) => Mathf.Round(34f * s);
        private static float CellW(float s) => Mathf.Round(46f * s);
        private static float CellH(float s) => Mathf.Round(22f * s);
        private const int GridMaxColumns = 5;

        private float FullWidth(float s)
        {
            var st = St;
            float w = 214f;
            if (!st.Shows(PartPortrait)) w -= 30f;
            if (st.Shows(PartAct)) w += 130f;
            return Mathf.Round(w * s);
        }

        protected override Vector2 Measure(float s)
        {
            float pad = Mathf.Round(4f * s);
            float more = moreText != null ? Mathf.Round(16f * s) : 0f;
            if (rows.Count == 0) return new Vector2(Mathf.Round(170f * s), Mathf.Round(26f * s));
            if (St.compact)
            {
                int cols = HudMath.GridColumns(rows.Count, GridMaxColumns);
                int lines = (rows.Count + cols - 1) / cols;
                return new Vector2(pad * 2f + cols * CellW(s) + (cols - 1) * 2f, pad * 2f + lines * CellH(s) + (lines - 1) * 2f + more);
            }
            return new Vector2(FullWidth(s), pad * 2f + rows.Count * RowH(s) + more);
        }

        // ---------------- Drawing ----------------

        protected override void DrawBody(Rect r, float s)
        {
            float pad = Mathf.Round(4f * s);
            if (rows.Count == 0)
            {
                HudKit.Label(r.ContractedBy(pad), "CC_Pnl_NoOne".Translate(), UiKit.Faint, HudKit.Minor(s));
                return;
            }
            float y = r.y + pad;
            if (St.compact)
            {
                int cols = HudMath.GridColumns(rows.Count, GridMaxColumns);
                float cw = CellW(s), ch = CellH(s);
                for (int i = 0; i < rows.Count; i++)
                {
                    var cell = new Rect(r.x + pad + i % cols * (cw + 2f), y + i / cols * (ch + 2f), cw, ch);
                    DrawCell(cell, rows[i], s);
                }
                y += ((rows.Count + cols - 1) / cols) * (ch + 2f) - 2f;
            }
            else
            {
                float rh = RowH(s);
                for (int i = 0; i < rows.Count; i++) DrawRow(new Rect(r.x + 2f, y + i * rh, r.width - 4f, rh), rows[i], s, i);
                y += rows.Count * rh;
            }
            if (moreText != null)
            {
                var mr = new Rect(r.x + pad, y, r.width - pad * 2f, Mathf.Round(16f * s));
                HudKit.Label(mr, moreText, UiKit.Faint, GameFont.Tiny);
                TooltipHandler.TipRegion(mr, "CC_Pnl_MoreTip".Translate());
                if (Widgets.ButtonInvisible(mr, false))
                {
                    if (Event.current.button == 1) OpenMenu();
                    else MainTabWindow_Council.OpenTab("Moods");
                }
            }
        }

        /// <summary>The row's tint: orange under the minor line, red under the major (pulsing softly), red in a break.</summary>
        private static Color? Tint(HudPawn h)
        {
            if (h.inBreak) return UiKit.WithAlpha(UiKit.Bad, 0.26f);
            if (h.now < h.major) return UiKit.WithAlpha(UiKit.Bad, 0.14f + 0.16f * HudKit.Pulse);
            if (h.now < h.minor) return UiKit.WithAlpha(UiKit.Warn, 0.16f);
            if (h.risk >= HudRank.SoonFloor) return UiKit.WithAlpha(UiKit.Warn, 0.07f);
            return null;
        }

        private void DrawRow(Rect r, HudPawn h, float s, int index)
        {
            var st = St;
            var p = h.pawn;
            var tint = Tint(h);
            if (tint.HasValue) Widgets.DrawBoxSolid(r, tint.Value);
            else UiKit.Zebra(r, index);
            bool over = Mouse.IsOver(r);
            if (over) Widgets.DrawBoxSolid(r, UiKit.CardHover);
            float pad = Mathf.Round(3f * s);
            float x = r.x + pad;
            if (st.Shows(PartPortrait))
            {
                float ps = Mathf.Round(26f * s);
                var ring = h.inBreak || h.now < h.major ? UiKit.Bad : h.now < h.minor ? UiKit.Warn : UiKit.WithAlpha(Color.white, 0.25f);
                UiKit.Portrait(new Rect(x, r.y + (r.height - ps) / 2f, ps, ps), p, ring);
                x += ps + Mathf.Round(5f * s);
            }
            float right = r.xMax - pad;
            var main = HudKit.Main(s);
            var minorFont = HudKit.Minor(s);
            float line1 = Mathf.Round(18f * s);
            // Right of the first line: the marks, the number, the arrow.
            float icon = Mathf.Round(13f * s);
            if (over)
            {
                // The Mood tab button takes the marks' place while the mouse is on the row.
                var b = new Rect(right - icon - 1f, r.y + (line1 - icon) / 2f + 1f, icon + 2f, icon + 2f);
                if (Widgets.ButtonImage(b, Icons.Moods ?? TexButton.Info, UiKit.Gold, Color.white, false, "CC_Pnl_MoodTabTip".Translate(p.LabelShort)))
                {
                    MoodView.ShowMoodTab(p);
                    Event.current.Use();
                }
                right = b.x - 2f;
            }
            else if (st.Shows(PartIcons)) right = Marks(new Rect(x, r.y + 1f, right - x, line1), h, icon);
            if (st.Shows(PartNumber))
            {
                string num = h.now.ToStringPercent();
                float nw = Mathf.Round(34f * s);
                HudKit.Label(new Rect(right - nw, r.y + 1f, nw, line1), num, HudKit.MoodColor(h.now, h.minor, h.major, h.extreme), main, TextAnchor.MiddleRight);
                right -= nw + 1f;
                float aw = Mathf.Round(10f * s);
                HudKit.Heading(new Rect(right - aw, r.y + 1f + (line1 - aw) / 2f, aw, aw), h.now, h.heading);
                right -= aw + 3f;
            }
            // The name.
            var nameColor = h.inBreak ? UiKit.Bad : Color.white;
            HudKit.Label(new Rect(x, r.y + 1f, Mathf.Max(0f, right - x), line1), p.LabelShortCap, nameColor, main);
            // Second line: the bar, then what drags them down.
            float barW = Mathf.Round(72f * s);
            float by = r.y + line1 + Mathf.Round(4f * s);
            float bh = Mathf.Max(4f, Mathf.Round(5f * s));
            float barRight = r.xMax - pad;
            if (!st.Shows(PartDrag) && !st.Shows(PartAct)) barW = barRight - x;
            HudKit.MoodBar(new Rect(x, by, barW, bh), h.now, h.heading, h.minor, h.major, h.extreme);
            float dx = x + barW + Mathf.Round(5f * s);
            var d = h.top;
            if (d != null && (st.Shows(PartDrag) || st.Shows(PartAct)) && -d.mood >= 1f)
            {
                string text = st.Shows(PartAct) ? (st.Shows(PartDrag) ? d.WithAct : d.act ?? d.Short) : d.Short;
                // Faint while they're fine; in gold when it's what to fix for someone at risk.
                var c = h.risk >= HudRank.SoonFloor && d.fixable ? UiKit.Gold : UiKit.Faint;
                HudKit.Label(new Rect(dx, r.y + line1 - 1f, Mathf.Max(0f, barRight - dx), r.height - line1 + 1f), text, c, minorFont);
            }
            var pawn = p;
            var data = h;
            TooltipHandler.TipRegion(r, () => HudMood.Tip(data), p.thingIDNumber ^ 0x48D7);
            if (Widgets.ButtonInvisible(r, false)) Click(pawn);
        }

        /// <summary>The small marks at the right of the first line; returns where they begin.</summary>
        private static float Marks(Rect line, HudPawn h, float size)
        {
            float x = line.xMax;
            void Mark(Texture2D tex, Color c, string tip)
            {
                if (tex == null) return;
                x -= size + 1f;
                var r = new Rect(x, line.y + (line.height - size) / 2f, size, size);
                UiKit.Icon(r, tex, c);
                TooltipHandler.TipRegion(r, tip);
            }
            if (h.inBreak) Mark(Icons.Warning, UiKit.Bad, "CC_Pnl_C_InBreak".Translate(h.breakLabel ?? "?"));
            if (h.band >= StrainBand.Worn) Mark(Icons.Heart, HeartView.BandColor(h.band), HeartView.BandLabel(h.band));
            if (h.wish != null) Mark(Icons.Wishes, h.wish.stage >= 2 ? UiKit.Warn : UiKit.Gold, "CC_Pnl_WishMark".Translate(h.wish.Label));
            if (h.wavering) Mark(Icons.CatIdeology, UiKit.Faint, "CC_Pnl_C_Wavering".Translate());
            return x - 2f;
        }

        private void DrawCell(Rect r, HudPawn h, float s)
        {
            Widgets.DrawBoxSolid(r, UiKit.MeterBack);
            var fill = new Rect(r.x, r.y, Mathf.Max(1f, r.width * Mathf.Clamp01(h.now)), r.height);
            var c = HudKit.MoodColor(h.now, h.minor, h.major, h.extreme);
            float a = h.now < h.major ? 0.5f + 0.3f * HudKit.Pulse : 0.55f;
            Widgets.DrawBoxSolid(fill, UiKit.WithAlpha(c, a));
            Widgets.DrawBoxSolid(new Rect(r.x + r.width * h.minor - 1f, r.y, 1f, r.height), HudKit.MinorLine);
            Widgets.DrawBoxSolid(new Rect(r.x + r.width * h.major - 1f, r.y, 1f, r.height), HudKit.MajorLine);
            if (h.inBreak) Widgets.DrawBox(r, 2, BaseContent.WhiteTex);
            if (Mouse.IsOver(r)) Widgets.DrawBoxSolid(r, UiKit.CardHover);
            HudKit.Label(r, HudKit.Initials(h.pawn), Color.white, GameFont.Tiny, TextAnchor.MiddleCenter);
            if (Mathf.Abs(h.heading - h.now) >= 0.01f)
            {
                float aw = Mathf.Round(8f * s);
                HudKit.Heading(new Rect(r.xMax - aw - 1f, r.y + 1f, aw, aw), h.now, h.heading);
            }
            var data = h;
            TooltipHandler.TipRegion(r, () => HudMood.Tip(data), h.pawn.thingIDNumber ^ 0x48D8);
            if (Widgets.ButtonInvisible(r, false)) Click(h.pawn);
        }

        /// <summary>A row clicked: left to select and jump, Shift for the Mood tab, right for the menu.</summary>
        private void Click(Pawn p)
        {
            var ev = Event.current;
            if (ev.button == 1)
            {
                MenuFor(p);
                return;
            }
            if (ev.shift || ev.button == 2)
            {
                MoodView.ShowMoodTab(p);
                return;
            }
            try
            {
                CameraJumper.TryJumpAndSelect(p);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("mood board: jump", e);
            }
        }

        private void MenuFor(Pawn p)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CC_Pnl_MoodTabTip".Translate(p.LabelShort), () => MoodView.ShowMoodTab(p)),
                new FloatMenuOption("CC_Pnl_OpenPage".Translate(p.LabelShort), () => MainTabWindow_Council.OpenPerson(p))
            };
            Find.WindowStack.Add(new FloatMenu(options));
        }

        protected override float HeaderButtons(Rect bar, float right)
        {
            var st = St;
            bool risk = st.sort == HudSort.Risk;
            return BarText(bar, right, (risk ? "CC_Pnl_SortRiskShort" : "CC_Pnl_SortBarShort").Translate(),
                (risk ? "CC_Pnl_SortRiskTip" : "CC_Pnl_SortBarTip").Translate(), () => st.sort = risk ? HudSort.Bar : HudSort.Risk);
        }

        protected override void MenuOptions(List<FloatMenuOption> options)
        {
            var st = St;
            options.Add(new FloatMenuOption((st.sort == HudSort.Risk ? "CC_Pnl_SortBar" : "CC_Pnl_SortRisk").Translate(), () =>
            {
                st.sort = st.sort == HudSort.Risk ? HudSort.Bar : HudSort.Risk;
                MarkDirty();
                HudLayout.Save();
            }));
            options.Add(new FloatMenuOption("CC_Pnl_OpenMoods".Translate(), () => MainTabWindow_Council.OpenTab("Moods")));
        }
    }
}
