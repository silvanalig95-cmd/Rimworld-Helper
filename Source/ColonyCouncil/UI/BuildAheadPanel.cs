using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The top of the Plan page: building ahead (Building/BuildAhead.cs). The rooms the colony needs against what is built
    /// and planned (beds, cold store places, seats, hospital beds, workshop bench places, store places), the rooms being
    /// built and their stage, the next one or two rooms to build and every other step (a stage to build first, a
    /// stockpile by a site, what holds a room up, a blueprint where a room was to grow), each with its numbers and
    /// Do it / Show / Not this, then the steps done lately with Undo and whether each worked. The cards are the Making
    /// page's (UI/AheadPanel.cs).
    /// </summary>
    public static class BuildAheadPanel
    {
        private static readonly Folds folds = new Folds("aside");
        private const float Pad = 8f;

        private static readonly string[] Columns = { "CC_Build_Col_Room", "CC_Build_Col_Need", "CC_Build_Col_Built", "CC_Build_Col_Planned", "CC_Build_Col_State" };

        /// <summary>Draws the section; returns its height.</summary>
        public static float Draw(float x, float y, float width, Map map)
        {
            if (map == null || !map.IsPlayerHome) return 0f;
            float top = y;
            var view = BuildAhead.For(map);
            var n = view.needs;
            if (n?.food == null) return 0f;
            var input = n.input;
            y += UiKit.SectionTitle(x, y, width, "CC_Build_Section".Translate(), Icons.Plan);
            TooltipHandler.TipRegion(new Rect(x, y - UiKit.SectionHeight, width, 24f), "CC_Build_SectionTip".Translate());
            string builders = input.builders == 0
                ? "CC_Build_BuildersNone".Translate().Resolve()
                : "CC_Build_Builders".Translate(input.builders.Named("BUILDERS"), Mathf.RoundToInt(input.builderHoursADay).Named("ADAY"), Mathf.RoundToInt(input.queuedHours).Named("QUEUED")).Resolve();
            y += UiKit.Paragraph(x + Pad, y, width - 2 * Pad, "CC_Build_Intro".Translate(Ahead.Who(n.food)).Resolve() + " " + builders, UiKit.Faint, GameFont.Tiny) + 6f;

            y += Rows(x, y, width, n) + 6f;
            y += Projects(x, y, width, n) + 4f;
            if (!view.heldBack.NullOrEmpty()) y += UiKit.Paragraph(x + Pad, y, width - 2 * Pad, view.heldBack, UiKit.Soon, GameFont.Tiny) + 6f;

            var open = BuildAhead.Open(map);
            if (open.Count == 0)
            {
                UiKit.EmptyLine(new Rect(x, y, width, UiKit.RowDense + 4f), "CC_Build_None".Translate());
                y += UiKit.RowDense + 10f;
            }
            foreach (var s in open) y += AheadPanel.DrawStep(s, x, y, width) + 4f;

            y += AheadPanel.Done(x, y + 4f, width, map, BuildAhead.Source) + 4f;
            y += AheadPanel.Aside(x, y, width, BuildAhead.Source, folds) + 8f;
            return y - top;
        }

        private static Color StateColor(GrowthRow r) =>
            r.Short > 0 ? r.level >= 3 ? UiKit.Sour : UiKit.Soon : r.SoonShort > 0 ? UiKit.Gold : UiKit.Warm;

        private static float Rows(float x, float y, float width, GrowthNeeds n)
        {
            var rows = n.plan.rows.Where(r => r.need > 0 || r.built > 0 || r.planned > 0).ToList();
            if (rows.Count == 0) return 0f;
            float[] share = { 0.3f, 0.14f, 0.12f, 0.12f, 0.32f };
            float inner = width - 2 * Pad;
            float Col(int i) => x + Pad + share.Take(i).Sum() * inner;
            float top = y;
            Text.Font = GameFont.Tiny;
            GUI.color = UiKit.Faint;
            for (int i = 0; i < Columns.Length; i++)
            {
                // The number columns (need, built, planned) right-aligned, as their numbers are.
                bool num = i >= 1 && i <= 3;
                var r = new Rect(Col(i), y, share[i] * inner - (num ? 10f : 4f), 18f);
                Text.Anchor = num ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
                Widgets.Label(r, Columns[i].Translate());
                TooltipHandler.TipRegion(r, (Columns[i] + "Tip").Translate());
            }
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = UiKit.Ink;
            y += 18f;
            int index = 0;
            foreach (var row in rows)
            {
                var line = new Rect(x, y, width, 22f);
                UiKit.Zebra(line, index++);
                if (Mouse.IsOver(line)) Widgets.DrawHighlight(line);
                const TextAnchor num = TextAnchor.MiddleRight;
                UiKit.LabelFit(new Rect(Col(0), y, share[0] * inner - 4f, 22f), ("CC_Build_Row_" + row.kind).Translate().Resolve(), UiKit.Ink);
                string need = row.soon > row.need ? "CC_Build_NeedSoon".Translate(row.need, row.soon).Resolve() : row.need.ToString();
                UiKit.LabelFit(new Rect(Col(1), y, share[1] * inner - 10f, 22f), need, UiKit.Ink, GameFont.Tiny, num);
                UiKit.LabelFit(new Rect(Col(2), y, share[2] * inner - 10f, 22f), row.built.ToString(), UiKit.Faint, GameFont.Tiny, num);
                UiKit.LabelFit(new Rect(Col(3), y, share[3] * inner - 10f, 22f), row.planned > 0 ? row.planned.ToString() : "-", UiKit.Faint, GameFont.Tiny, num);
                UiKit.LabelFit(new Rect(Col(4), y, share[4] * inner - 4f, 22f), BuildAhead.RowState(row), StateColor(row), GameFont.Tiny);
                TooltipHandler.TipRegion(line, BuildAhead.RowReason(row, n));
                y += 22f;
            }
            Text.Font = GameFont.Small;
            return y - top;
        }

        /// <summary>The rooms being built, each in a line with its stage; and how many rooms keep a side free to grow into.</summary>
        private static float Projects(float x, float y, float width, GrowthNeeds n)
        {
            float top = y;
            var building = n.projects.Where(s => !s.project.Done && s.AnythingLeft).ToList();
            foreach (var st in building)
            {
                string line = BuildAhead.ProjectLine(st);
                if (st.project.KeepsSide) line += " " + "CC_Build_Project_Keeps".Translate(("CC_Build_Side_" + st.project.growSide).Translate().Resolve()).Resolve();
                float h = UiKit.Paragraph(x + Pad, y, width - 2 * Pad, line, UiKit.Cool, GameFont.Tiny);
                var rect = new Rect(x, y, width, h);
                var tip = new List<string>();
                for (int i = 0; i < GrowthMath.Finished; i++)
                    tip.Add("CC_Build_Project_TipStage".Translate(("CC_Build_Stage_" + GrowthMath.Stages[i]).Translate().Resolve().CapitalizeFirst().Named("STAGE"), st.left[i].Named("LEFT")).Resolve());
                TooltipHandler.TipRegion(rect, string.Join("\n", tip));
                if (Mouse.IsOver(rect) && st.project.map == Find.CurrentMap)
                {
                    MapHighlights.Cells("build-project:" + st.project.id, st.project.map, st.project.Rect.ClipInsideMap(st.project.map).EdgeCells, UiKit.Info, st.project.Label);
                    if (st.project.KeepsSide)
                        MapHighlights.Cells("build-grow:" + st.project.id, st.project.map, st.project.StripRect.ClipInsideMap(st.project.map).Cells, UiKit.Faint, "CC_Build_KeptLabel".Translate(st.project.Label).Resolve());
                }
                y += h + 2f;
            }
            int keeping = n.projects.Count(s => s.project.KeepsSide);
            if (keeping > 0) y += UiKit.Paragraph(x + Pad, y, width - 2 * Pad, "CC_Build_Kept".Translate(keeping).Resolve(), UiKit.Faint, GameFont.Tiny) + 2f;
            return y - top;
        }
    }
}
