using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Council → Ways, the colony's one name (Character/ColonyName.cs): its card first (what it is known for and how far its
    /// name carries, the deeds behind it, what each people remembers), then the parts it is read from: the five ways, what the
    /// work has made it, who's proud of it and who chafes, and what shaped it.
    /// </summary>
    public static class WaysPanel
    {
        // The interface's tokens (UI/UiKit.cs): the proud end warm, the chafing end sour.
        private static Color Pos => UiKit.Warm;
        private static Color Neg => UiKit.Sour;
        private static Vector2 scroll;
        private static float height = 600f;
        private static List<string> card;
        private static float cardAt = -1f;

        public static void Draw(Rect rect, CouncilGameComponent comp)
        {
            if (!ColonyCouncilMod.Settings.colonyCharacter)
            {
                UiKit.EmptyState(rect, Icons.Ways, "CC_Ways_Off".Translate());
                return;
            }
            // The colony in view's own ways (each colony its own character, decision 13A).
            var home = ColonyState.Several ? ColonyView.Current?.Map : null;
            var ch = home != null ? CharacterTracker.For(home) ?? comp.character : comp.character;
            // The card, built at most once a second while it shows (and again when the colony in view changes).
            if (card == null || cardFor != ch || Time.realtimeSinceStartup - cardAt > 1f || Time.realtimeSinceStartup < cardAt)
            {
                cardAt = Time.realtimeSinceStartup;
                cardFor = ch;
                card = ColonyName.Lines(home);
            }
            float top = UiKit.PageHeader(rect, Icons.Ways, "CC_Name_PageTitle".Translate(), "CC_Name_PageSub".Translate());
            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;
            float w = view.width;
            // The colony's name, in one reading.
            for (int c = 0; c < card.Count; c++)
                y += UiKit.Paragraph(8f, y, w - 16f, card[c], c == 0 ? UiKit.Ink : UiKit.Faint) + 4f;
            y += UiKit.SectionGap;
            y += UiKit.SectionTitle(0f, y, w, "CC_Name_PageWays".Translate(), Icons.Ways);
            y += UiKit.Paragraph(0f, y, w, "CC_Ways_Intro".Translate(), UiKit.Faint) + 10f;

            // The five ways, as bars from one pole to the other.
            int i = 0;
            foreach (var way in CharacterTracker.All) y += DrawBar(ch, way, w, y, i++);
            y += 14f;

            // What the work has made the colony.
            y += DrawTraits(ch, w, y) + 14f;

            // Who's proud of it, who chafes.
            var rated = UiCache.Get(ch, 81, () => Rated(ch));
            float colW = (w - 12f) / 2f;
            float y0 = y;
            float leftH = DrawPeople("CC_Ways_Proud".Translate(), rated.Where(x => x.score > 0f).ToList(), 0f, y0, colW, Pos, Icons.Heart);
            float rightH = DrawPeople("CC_Ways_Chafe".Translate(), rated.Where(x => x.score < 0f).Reverse().ToList(), colW + 12f, y0, colW, Neg, Icons.Warning);
            y += Mathf.Max(leftH, rightH) + 14f;

            // What shaped it.
            y += UiKit.SectionTitle(0f, y, w, "CC_Ways_History".Translate(), Icons.Chronicle);
            if (ch.recent.Count == 0)
            {
                UiKit.EmptyLine(new Rect(0f, y, w, UiKit.RowDense + 4f), "CC_Ways_NoHistory".Translate());
                y += UiKit.RowDense + 8f;
            }
            int row = 0;
            foreach (var e in Enumerable.Reverse(ch.recent))
            {
                string line = "CC_Ways_HistoryLine".Translate("CC_DayN".Translate(e.tick / GenDate.TicksPerDay + 1), e.what, e.shift).Resolve();
                float h = Mathf.Max(24f, Text.CalcHeight(line, w - 28f) + 4f);
                UiKit.Zebra(new Rect(0f, y, w, h), row++);
                UiKit.Icon(new Rect(4f, y + 4f, UiKit.IconDense, UiKit.IconDense), Icons.Ways, UiKit.Faint);
                Widgets.Label(new Rect(24f, y + 2f, w - 28f, h - 4f), line);
                y += h;
            }
            height = Mathf.Max(y + 10f, 100f);
            Widgets.EndScrollView();
        }

        private class Rating
        {
            public Pawn p;
            public float score;
            public string reasons;
        }

        private static CharacterTracker cardFor;

        private static List<Rating> Rated(CharacterTracker ch)
        {
            // Those of the colony whose ways these are (everyone, with one colony).
            var home = ColonyState.Several ? ch.Map : null;
            var pawns = PawnsFinder.AllMaps_FreeColonists.Where(p => !p.IsQuestLodger() && People.HasInnerLife(p) && !p.DevelopmentalStage.Baby() && (home == null || Homes.Of(p) == home)).Distinct();
            var list = new List<Rating>();
            foreach (var p in pawns)
            {
                var reasons = new List<string>();
                float score = ch.Approval(p, reasons);
                if (Mathf.Abs(score) >= ThoughtWorker_ColonyWays.Mild) list.Add(new Rating { p = p, score = score, reasons = string.Join("; ", reasons) });
            }
            return list.OrderByDescending(x => x.score).ToList();
        }

        private static float DrawBar(CharacterTracker ch, Way way, float w, float y, int index)
        {
            const float rowH = 30f;
            float v = ch.Value(way);
            int tier = ch.Tier(way);
            float labelW = Mathf.Min(130f, w * 0.22f);
            float barW = w - labelW * 2f - 20f;
            var row = new Rect(0f, y, w, rowH);
            UiKit.Zebra(row, index);
            UiKit.RowHover(row);
            UiKit.LabelFit(new Rect(0f, y, labelW, rowH), CharacterTracker.PoleLabel(way, false).CapitalizeFirst(), tier < 0 ? Neg : UiKit.Faint, GameFont.Small, TextAnchor.MiddleRight);
            UiKit.LabelFit(new Rect(labelW + barW + 20f, y, labelW, rowH), CharacterTracker.PoleLabel(way, true).CapitalizeFirst(), tier > 0 ? Pos : UiKit.Faint);
            var bar = new Rect(labelW + 10f, y + 10f, barW, 10f);
            Widgets.DrawBoxSolidWithOutline(bar, UiKit.MeterBack, UiKit.MeterEdge, 1);
            // The "known" zones at each end.
            float kw = barW * (100f - CharacterTracker.Known) / 200f;
            Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, kw, bar.height), UiKit.WithAlpha(Neg, 0.15f));
            Widgets.DrawBoxSolid(new Rect(bar.xMax - kw, bar.y, kw, bar.height), UiKit.WithAlpha(Pos, 0.15f));
            Widgets.DrawLineVertical(bar.center.x, bar.y - 3f, bar.height + 6f);
            float x = bar.x + (v + 100f) / 200f * barW;
            var fill = v >= 0f ? new Rect(bar.center.x, bar.y, x - bar.center.x, bar.height) : new Rect(x, bar.y, bar.center.x - x, bar.height);
            Widgets.DrawBoxSolid(fill, v >= 0f ? Pos : Neg);
            // Where the colony stands, as a small marker.
            Widgets.DrawBoxSolid(new Rect(x - 2f, bar.y - 3f, 4f, bar.height + 6f), UiKit.Ink);
            TooltipHandler.TipRegion(row, ("CC_Ways_Meaning_" + way + "_Pos").Translate() + "\n\n" + ("CC_Ways_Meaning_" + way + "_Neg").Translate()
                                          + "\n\n" + "CC_Ways_Value".Translate(v.ToString("0")));
            return rowH + 2f;
        }

        /// <summary>The colony traits: each with how close the colony is to being known for it.</summary>
        private static float DrawTraits(CharacterTracker ch, float w, float y)
        {
            var defs = ColonyTraits.All.ToList();
            if (defs.Count == 0) return 0f;
            float start = y;
            y += UiKit.SectionTitle(0f, y, w, "CC_Ways_TraitsHeader".Translate(), Icons.Ways);
            y += UiKit.Paragraph(0f, y, w, "CC_Ways_TraitsIntro".Translate(), UiKit.Faint) + 4f;
            const float rowH = UiKit.RowStandard;
            int i = 0;
            foreach (var d in defs)
            {
                bool known = ch.traits.IsKnown(d);
                float progress = Mathf.Clamp01(ch.traits.Progress(d));
                var row = new Rect(0f, y, w, rowH);
                UiKit.Zebra(row, i++);
                UiKit.RowHover(row);
                float labelW = Mathf.Min(160f, w * 0.3f);
                UiKit.LabelFit(new Rect(6f, y, labelW, rowH), d.LabelCap, UiKit.Ink);
                var bar = new Rect(labelW + 12f, y + 9f, Mathf.Max(20f, w - labelW - 140f), 8f);
                Widgets.DrawBoxSolidWithOutline(bar, UiKit.MeterBack, UiKit.MeterEdge, 1);
                Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * progress, bar.height), known ? Pos : UiKit.Faint);
                string status = known ? "CC_Ways_TraitKnown".Translate().ToString() : "CC_Ways_TraitProgress".Translate(progress.ToStringPercent()).ToString();
                UiKit.LabelFit(new Rect(bar.xMax + 8f, y, w - bar.xMax - 10f, rowH), status, known ? Pos : UiKit.Faint, GameFont.Tiny);
                TooltipHandler.TipRegion(row, d.description + (d.skill != null ? "\n\n" + "CC_Ways_TraitPride".Translate(d.skill.label).Resolve() : ""));
                y += rowH;
            }
            return y - start;
        }

        private static float DrawPeople(string header, List<Rating> list, float x, float y, float w, Color color, Texture2D icon)
        {
            float start = y;
            y += UiKit.SectionTitle(x, y, w, header, icon, color);
            if (list.Count == 0)
            {
                UiKit.EmptyLine(new Rect(x, y, w, UiKit.RowDense + 4f), "CC_Ways_Nobody".Translate());
                return y - start + UiKit.RowDense + 8f;
            }
            int i = 0;
            foreach (var r in list.Take(8))
            {
                Text.Font = GameFont.Tiny;
                float th = r.reasons.NullOrEmpty() ? 0f : Text.CalcHeight(r.reasons, w - 36f);
                Text.Font = GameFont.Small;
                float h = 28f + th + 4f;
                var row = new Rect(x, y, w, h);
                UiKit.Zebra(row, i++);
                UiKit.PawnChip(x + 4f, y + 3f, r.p, 24f, true, w - 8f, color, link: true);
                if (th > 0f)
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = UiKit.Faint;
                    Widgets.Label(new Rect(x + 32f, y + 28f, w - 36f, th), r.reasons);
                    GUI.color = UiKit.Ink;
                    Text.Font = GameFont.Small;
                }
                y += h;
            }
            return y - start;
        }
    }
}
