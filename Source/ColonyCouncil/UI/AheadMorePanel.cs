using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Phase 3 in the Making page's Ahead section (Building/AheadMore.cs): Colony Manager's targets beside the stock rows
    /// (each job, what it counts, what there is, its target and how it stands, and why the manager is idle when it is),
    /// and the shopping list under the fields (what the colony wants and can't make here, how much and why, its worth in
    /// silver, and what a trader on the map or in orbit has of it). The steps these lead to are cards with the others.
    /// </summary>
    public static class AheadMorePanel
    {
        private const float Pad = 8f;

        private static readonly string[] JobColumns = { "CC_AheadMore_Col_Job", "CC_AheadMore_Col_Counts", "CC_AheadMore_Col_Now", "CC_AheadMore_Col_Target", "CC_AheadMore_Col_State" };
        private static readonly string[] ShopColumns = { "CC_AheadMore_Col_Item", "CC_AheadMore_Col_Short", "CC_AheadMore_Col_Have", "CC_AheadMore_Col_Worth", "CC_AheadMore_Col_Trader" };

        /// <summary>A table's heads; the number columns (from <paramref name="numFrom"/> to <paramref name="numTo"/>) right-aligned, as their numbers are.</summary>
        private static float Header(float x, float y, float width, string[] columns, float[] share, int numFrom, int numTo)
        {
            float inner = width - 2 * Pad;
            Text.Font = GameFont.Tiny;
            GUI.color = UiKit.Faint;
            for (int i = 0; i < columns.Length; i++)
            {
                bool num = i >= numFrom && i <= numTo;
                var r = new Rect(x + Pad + share.Take(i).Sum() * inner, y, share[i] * inner - (num ? 10f : 4f), 18f);
                Text.Anchor = num ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
                Widgets.Label(r, columns[i].Translate());
                TooltipHandler.TipRegion(r, (columns[i] + "Tip").Translate());
            }
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = UiKit.Ink;
            return 18f;
        }

        // ---------------- Colony Manager ----------------

        private static string StateText(ManagerJobInfo i, out Color color)
        {
            if (!i.managed)
            {
                color = UiKit.Dim;
                return "CC_AheadMore_State_Unmanaged".Translate();
            }
            if (i.suspended)
            {
                color = AheadMoreMath.ShouldResume(i) ? UiKit.Soon : UiKit.Faint;
                return "CC_AheadMore_State_Suspended".Translate();
            }
            if (AheadMoreMath.ShouldRaise(i))
            {
                color = UiKit.Soon;
                return "CC_AheadMore_State_Wants".Translate(i.wanted);
            }
            if (i.completed)
            {
                color = UiKit.Done;
                return "CC_AheadMore_State_Done".Translate();
            }
            color = UiKit.Cool;
            return "CC_AheadMore_State_Working".Translate();
        }

        /// <summary>Colony Manager's jobs as rows under the stock rows; nothing without the mod or a job.</summary>
        public static float Manager(float x, float y, float width, Ahead.View view)
        {
            var more = view?.more;
            if (more == null || more.jobs.Count == 0) return 0f;
            float top = y;
            UiKit.HeadingLine(x, y, width, "CC_AheadMore_ManagerTitle".Translate(), more.jobs.Count, null, null, UiKit.Heading, false, false);
            TooltipHandler.TipRegion(new Rect(x, y, width, 22f), "CC_AheadMore_ManagerTip".Translate());
            y += UiKit.HeadingHeight;
            float[] share = { 0.27f, 0.25f, 0.11f, 0.12f, 0.25f };
            float inner = width - 2 * Pad;
            float Col(int i) => x + Pad + share.Take(i).Sum() * inner;
            y += Header(x, y, width, JobColumns, share, 2, 3);
            int index = 0;
            foreach (var j in more.jobs.OrderBy(j => System.Array.IndexOf(AheadMoreMath.ManagerKinds, j.info.kind)).ThenBy(j => j.index))
            {
                var i = j.info;
                var line = new Rect(x, y, width, 22f);
                UiKit.Zebra(line, index++);
                if (Mouse.IsOver(line)) Widgets.DrawHighlight(line);
                string name = ("CC_AheadMore_Job_" + i.kind).Translate(i.label).Resolve();
                UiKit.LabelFit(new Rect(Col(0), y, share[0] * inner - 4f, 22f), name, UiKit.Ink);
                UiKit.LabelFit(new Rect(Col(1), y, share[1] * inner - 4f, 22f), i.goods ?? "-", UiKit.Faint, GameFont.Tiny);
                UiKit.LabelFit(new Rect(Col(2), y, share[2] * inner - 10f, 22f), i.current.ToString(), UiKit.Ink, GameFont.Tiny, TextAnchor.MiddleRight);
                UiKit.LabelFit(new Rect(Col(3), y, share[3] * inner - 10f, 22f), i.target.ToString(), UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                string state = StateText(i, out var color);
                UiKit.LabelFit(new Rect(Col(4), y, share[4] * inner - 4f, 22f), state, color, GameFont.Tiny);
                var tip = new List<string>
                {
                    (i.threshold ? "CC_AheadMore_JobTip" : "CC_AheadMore_JobTipHerd").Translate(name.Named("JOB"), (i.goods ?? "-").Named("GOODS"), i.current.Named("NOW"), i.target.Named("TARGET")).Resolve()
                };
                if (i.max > 0 && i.threshold) tip.Add("CC_AheadMore_JobTipMax".Translate(i.max).Resolve());
                if (!i.wantedWhy.NullOrEmpty()) tip.Add(i.wantedWhy);
                TooltipHandler.TipRegion(line, string.Join("\n", tip));
                y += 22f;
            }
            if (more.stall != null)
            {
                y += 2f;
                y += UiKit.Paragraph(x + Pad, y, width - 2 * Pad, "CC_AheadMore_Idle".Translate(("CC_AheadMore_Stall_" + more.stall).Translate().Resolve()).Resolve(), UiKit.Soon, GameFont.Tiny);
            }
            Text.Font = GameFont.Small;
            return y - top;
        }

        // ---------------- The shopping list ----------------

        /// <summary>The shopping list under the fields, with what traders here have of it; nothing when there is nothing to buy.</summary>
        public static float Shopping(float x, float y, float width, Ahead.View view)
        {
            var more = view?.more;
            if (more == null || more.shop.Count == 0) return 0f;
            float top = y;
            UiKit.HeadingLine(x, y, width, "CC_AheadMore_ShopTitle".Translate(), more.shop.Count, null, null, UiKit.Heading, false, false);
            TooltipHandler.TipRegion(new Rect(x, y, width, 22f), "CC_AheadMore_ShopTip".Translate());
            y += UiKit.HeadingHeight;
            float[] share = { 0.3f, 0.12f, 0.16f, 0.14f, 0.28f };
            float inner = width - 2 * Pad;
            float Col(int i) => x + Pad + share.Take(i).Sum() * inner;
            y += Header(x, y, width, ShopColumns, share, 1, 3);
            int index = 0;
            foreach (var n in more.shop)
            {
                var line = new Rect(x, y, width, 22f);
                UiKit.Zebra(line, index++);
                if (Mouse.IsOver(line)) Widgets.DrawHighlight(line);
                const TextAnchor num = TextAnchor.MiddleRight;
                UiKit.LabelFit(new Rect(Col(0), y, share[0] * inner - 4f, 22f), n.label.CapitalizeFirst(), UiKit.Ink);
                UiKit.LabelFit(new Rect(Col(1), y, share[1] * inner - 10f, 22f), n.Short.ToString(), UiKit.Soon, GameFont.Tiny, num);
                UiKit.LabelFit(new Rect(Col(2), y, share[2] * inner - 10f, 22f), "CC_AheadMore_HaveOf".Translate(n.have, n.want).Resolve(), UiKit.Faint, GameFont.Tiny, num);
                UiKit.LabelFit(new Rect(Col(3), y, share[3] * inner - 10f, 22f), Mathf.RoundToInt(n.Short * n.price).ToString(), UiKit.Faint, GameFont.Tiny, num);
                string trader = n.traderHas > 0 ? "CC_AheadMore_TraderHas".Translate(n.traderHas).Resolve()
                    : n.managerMines ? "CC_AheadMore_ManagerMines".Translate().Resolve()
                    : n.deposit ? "CC_AheadMore_CanMine".Translate().Resolve() : "-";
                UiKit.LabelFit(new Rect(Col(4), y, share[4] * inner - 4f, 22f), trader, n.traderHas > 0 ? UiKit.Good : UiKit.Faint, GameFont.Tiny);
                var tip = new List<string> { AheadMore.ShopLine(n), "" };
                foreach (var (why, want) in n.reasons) tip.Add("CC_AheadMore_ShopTipReason".Translate(why.CapitalizeFirst().Named("WHY"), want.Named("USE")).Resolve());
                if (n.managerMines) tip.Add("CC_AheadMore_ShopTipManager".Translate().Resolve());
                else if (n.deposit) tip.Add("CC_AheadMore_ShopTipDeposit".Translate().Resolve());
                TooltipHandler.TipRegion(line, string.Join("\n", tip));
                y += 22f;
            }
            y += 2f;
            if (more.traders.Count == 0) y += UiKit.Paragraph(x + Pad, y, width - 2 * Pad, "CC_AheadMore_NoTrader".Translate(), UiKit.Faint, GameFont.Tiny);
            foreach (var t in more.traders) y += UiKit.Paragraph(x + Pad, y, width - 2 * Pad, t, UiKit.Faint, GameFont.Tiny) + 2f;
            Text.Font = GameFont.Small;
            return y - top;
        }
    }
}
