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
        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color Pos = new Color(0.55f, 0.8f, 0.55f);
        private static readonly Color Neg = new Color(0.85f, 0.6f, 0.5f);
        private static Vector2 scroll;
        private static float height = 600f;

        public static void Draw(Rect rect, CouncilGameComponent comp)
        {
            if (!ColonyCouncilMod.Settings.colonyCharacter)
            {
                Widgets.Label(rect, "CC_Ways_Off".Translate());
                return;
            }
            var ch = comp.character;
            var outRect = rect;
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;
            float w = view.width;

            Text.Font = GameFont.Medium;
            string known = ch.Describe();
            Widgets.Label(new Rect(0f, y, w, 32f), known != null ? "CC_Ways_KnownAs".Translate(known).Resolve() : "CC_Ways_NoName".Translate().ToString());
            Text.Font = GameFont.Small;
            y += 34f;
            GUI.color = Faint;
            string intro = "CC_Ways_Intro".Translate();
            float ih = Text.CalcHeight(intro, w);
            Widgets.Label(new Rect(0f, y, w, ih), intro);
            GUI.color = Color.white;
            y += ih + 10f;

            // The five ways, as bars from one pole to the other.
            foreach (var way in CharacterTracker.All) y += DrawBar(ch, way, w, y) + 6f;
            y += 8f;

            // Who's proud of it, who chafes.
            var pawns = PawnsFinder.AllMaps_FreeColonists.Where(p => !p.IsQuestLodger() && People.HasInnerLife(p) && !p.DevelopmentalStage.Baby()).Distinct().ToList();
            var rated = pawns.Select(p => (p, score: ch.Approval(p))).Where(x => Mathf.Abs(x.score) >= ThoughtWorker_ColonyWays.Mild).OrderByDescending(x => x.score).ToList();
            float colW = (w - 12f) / 2f;
            float y0 = y;
            float leftH = DrawPeople("CC_Ways_Proud".Translate(), rated.Where(x => x.score > 0f).ToList(), ch, 0f, y0, colW, Pos);
            float rightH = DrawPeople("CC_Ways_Chafe".Translate(), rated.Where(x => x.score < 0f).Reverse().ToList(), ch, colW + 12f, y0, colW, Neg);
            y += Mathf.Max(leftH, rightH) + 12f;

            // What shaped it.
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, w, 30f), "CC_Ways_History".Translate());
            Text.Font = GameFont.Small;
            y += 32f;
            if (ch.recent.Count == 0)
            {
                GUI.color = Faint;
                Widgets.Label(new Rect(0f, y, w, 24f), "CC_Ways_NoHistory".Translate());
                GUI.color = Color.white;
                y += 26f;
            }
            foreach (var e in Enumerable.Reverse(ch.recent))
            {
                string line = "CC_Ways_HistoryLine".Translate("CC_DayN".Translate(e.tick / GenDate.TicksPerDay + 1), e.what, e.shift).Resolve();
                float h = Text.CalcHeight(line, w);
                Widgets.Label(new Rect(0f, y, w, h), line);
                y += h + 4f;
            }
            height = Mathf.Max(y + 10f, 100f);
            Widgets.EndScrollView();
        }

        private static float DrawBar(CharacterTracker ch, Way way, float w, float y)
        {
            float v = ch.Value(way);
            int tier = ch.Tier(way);
            const float labelW = 120f;
            float barW = w - labelW * 2f - 20f;
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = tier < 0 ? Neg : Faint;
            Widgets.Label(new Rect(0f, y, labelW, 26f), CharacterTracker.PoleLabel(way, false).CapitalizeFirst());
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = tier > 0 ? Pos : Faint;
            Widgets.Label(new Rect(labelW + barW + 20f, y, labelW, 26f), CharacterTracker.PoleLabel(way, true).CapitalizeFirst());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            var bar = new Rect(labelW + 10f, y + 8f, barW, 10f);
            Widgets.DrawBoxSolid(bar, new Color(0.15f, 0.15f, 0.15f, 0.8f));
            // The "known" zones at each end.
            float kw = barW * (100f - CharacterTracker.Known) / 200f;
            Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, kw, bar.height), new Color(Neg.r, Neg.g, Neg.b, 0.15f));
            Widgets.DrawBoxSolid(new Rect(bar.xMax - kw, bar.y, kw, bar.height), new Color(Pos.r, Pos.g, Pos.b, 0.15f));
            Widgets.DrawLineVertical(bar.center.x, bar.y - 2f, bar.height + 4f);
            float x = bar.x + (v + 100f) / 200f * barW;
            var fill = v >= 0f ? new Rect(bar.center.x, bar.y, x - bar.center.x, bar.height) : new Rect(x, bar.y, bar.center.x - x, bar.height);
            Widgets.DrawBoxSolid(fill, v >= 0f ? Pos : Neg);
            var row = new Rect(0f, y, w, 26f);
            TooltipHandler.TipRegion(row, ("CC_Ways_Meaning_" + way + "_Pos").Translate() + "\n\n" + ("CC_Ways_Meaning_" + way + "_Neg").Translate()
                                          + "\n\n" + "CC_Ways_Value".Translate(v.ToString("0")));
            return 26f;
        }

        private static float DrawPeople(string header, List<(Pawn p, float score)> list, CharacterTracker ch, float x, float y, float w, Color color)
        {
            float start = y;
            GUI.color = color;
            Widgets.Label(new Rect(x, y, w, 24f), header);
            GUI.color = Color.white;
            y += 26f;
            if (list.Count == 0)
            {
                GUI.color = Faint;
                Widgets.Label(new Rect(x, y, w, 24f), "CC_Ways_Nobody".Translate());
                GUI.color = Color.white;
                return 52f;
            }
            foreach (var (p, _) in list.Take(8))
            {
                var reasons = new List<string>();
                ch.Approval(p, reasons);
                string line = p.LabelShort + ": " + string.Join("; ", reasons);
                float h = Text.CalcHeight(line, w);
                var r = new Rect(x, y, w, h);
                Widgets.Label(r, line);
                Widgets.DrawHighlightIfMouseover(r);
                if (Widgets.ButtonInvisible(r)) MainTabWindow_Council.OpenPerson(p);
                y += h + 2f;
            }
            return y - start;
        }
    }
}
