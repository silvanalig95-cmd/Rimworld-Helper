using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Council → Ways: what the colony is known for, who's proud of it and who chafes, and what shaped it.</summary>
    public static class WaysPanel
    {
        private static readonly Color Pos = new Color(0.55f, 0.8f, 0.55f);
        private static readonly Color Neg = new Color(0.85f, 0.6f, 0.5f);
        private static Vector2 scroll;
        private static float height = 600f;

        public static void Draw(Rect rect, CouncilGameComponent comp)
        {
            if (!ColonyCouncilMod.Settings.colonyCharacter)
            {
                UiKit.EmptyState(rect, Icons.Ways, "CC_Ways_Off".Translate());
                return;
            }
            var ch = comp.character;
            string known = UiCache.Get(ch, 80, () => ch.Describe());
            string title = known != null ? "CC_Ways_KnownAs".Translate(known).Resolve() : "CC_Ways_NoName".Translate().ToString();
            float top = UiKit.PageHeader(rect, Icons.Ways, title, "CC_UI_Sub_Ways".Translate());
            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;
            float w = view.width;
            y += UiKit.Paragraph(0f, y, w, "CC_Ways_Intro".Translate(), UiKit.Faint) + 10f;

            // The five ways, as bars from one pole to the other.
            int i = 0;
            foreach (var way in CharacterTracker.All) y += DrawBar(ch, way, w, y, i++);
            y += 14f;

            // Who's proud of it, who chafes.
            var rated = UiCache.Get(ch, 81, () => Rated(ch));
            float colW = (w - 12f) / 2f;
            float y0 = y;
            float leftH = DrawPeople("CC_Ways_Proud".Translate(), rated.Where(x => x.score > 0f).ToList(), 0f, y0, colW, Pos, Icons.Heart);
            float rightH = DrawPeople("CC_Ways_Chafe".Translate(), rated.Where(x => x.score < 0f).Reverse().ToList(), colW + 12f, y0, colW, Neg, Icons.Warning);
            y += Mathf.Max(leftH, rightH) + 14f;

            // What shaped it.
            y += UiKit.SectionTitle(0f, y, w, "CC_Ways_History".Translate(), Icons.Chronicle);
            if (ch.recent.Count == 0) y += UiKit.Paragraph(4f, y, w, "CC_Ways_NoHistory".Translate(), UiKit.Faint) + 6f;
            int row = 0;
            foreach (var e in Enumerable.Reverse(ch.recent))
            {
                string line = "CC_Ways_HistoryLine".Translate("CC_DayN".Translate(e.tick / GenDate.TicksPerDay + 1), e.what, e.shift).Resolve();
                float h = Mathf.Max(24f, Text.CalcHeight(line, w - 28f) + 4f);
                UiKit.Zebra(new Rect(0f, y, w, h), row++);
                UiKit.Icon(new Rect(4f, y + 4f, 14f, 14f), Icons.Ways, UiKit.Faint);
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

        private static List<Rating> Rated(CharacterTracker ch)
        {
            var pawns = PawnsFinder.AllMaps_FreeColonists.Where(p => !p.IsQuestLodger() && People.HasInnerLife(p) && !p.DevelopmentalStage.Baby()).Distinct();
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
            Widgets.DrawHighlightIfMouseover(row);
            UiKit.LabelFit(new Rect(0f, y, labelW, rowH), CharacterTracker.PoleLabel(way, false).CapitalizeFirst(), tier < 0 ? Neg : UiKit.Faint, GameFont.Small, TextAnchor.MiddleRight);
            UiKit.LabelFit(new Rect(labelW + barW + 20f, y, labelW, rowH), CharacterTracker.PoleLabel(way, true).CapitalizeFirst(), tier > 0 ? Pos : UiKit.Faint);
            var bar = new Rect(labelW + 10f, y + 10f, barW, 10f);
            Widgets.DrawBoxSolidWithOutline(bar, new Color(0.15f, 0.15f, 0.15f, 0.8f), new Color(1f, 1f, 1f, 0.1f), 1);
            // The "known" zones at each end.
            float kw = barW * (100f - CharacterTracker.Known) / 200f;
            Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, kw, bar.height), new Color(Neg.r, Neg.g, Neg.b, 0.15f));
            Widgets.DrawBoxSolid(new Rect(bar.xMax - kw, bar.y, kw, bar.height), new Color(Pos.r, Pos.g, Pos.b, 0.15f));
            Widgets.DrawLineVertical(bar.center.x, bar.y - 3f, bar.height + 6f);
            float x = bar.x + (v + 100f) / 200f * barW;
            var fill = v >= 0f ? new Rect(bar.center.x, bar.y, x - bar.center.x, bar.height) : new Rect(x, bar.y, bar.center.x - x, bar.height);
            Widgets.DrawBoxSolid(fill, v >= 0f ? Pos : Neg);
            // Where the colony stands, as a small marker.
            Widgets.DrawBoxSolid(new Rect(x - 2f, bar.y - 3f, 4f, bar.height + 6f), Color.white);
            TooltipHandler.TipRegion(row, ("CC_Ways_Meaning_" + way + "_Pos").Translate() + "\n\n" + ("CC_Ways_Meaning_" + way + "_Neg").Translate()
                                          + "\n\n" + "CC_Ways_Value".Translate(v.ToString("0")));
            return rowH + 2f;
        }

        private static float DrawPeople(string header, List<Rating> list, float x, float y, float w, Color color, Texture2D icon)
        {
            float start = y;
            y += UiKit.SectionTitle(x, y, w, header, icon, color);
            if (list.Count == 0)
            {
                y += UiKit.Paragraph(x + 4f, y, w - 4f, "CC_Ways_Nobody".Translate(), UiKit.Faint) + 4f;
                return y - start;
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
                UiKit.PawnChip(x + 4f, y + 3f, r.p, 24f, true, w - 8f, color);
                if (th > 0f)
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = UiKit.Faint;
                    Widgets.Label(new Rect(x + 32f, y + 28f, w - 36f, th), r.reasons);
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                }
                y += h;
            }
            return y - start;
        }
    }
}
