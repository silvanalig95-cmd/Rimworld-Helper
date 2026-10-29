using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Colony, then Making: what the colony could make (Building/Making.cs). What could be made now from what's in
    /// stock with no bill for it, bench by bench, each with a bill ready to set up; harvests and finds no bench here
    /// puts to its own use, and what would; benches standing without bills.
    /// </summary>
    public static class MakingPanel
    {
        private static Vector2 scroll;
        private static float height = 600f;

        private const float ButtonsW = 160f;

        public static void Draw(Rect rect, Map map)
        {
            if (map == null || !map.IsPlayerHome)
            {
                UiKit.EmptyState(rect, Icons.Colony, "CC_NoHomeMap".Translate());
                return;
            }
            var r = Making.For(map);
            string stat = r.could.Count > 0 ? "CC_Making_Stat".Translate(r.could.Count).Resolve() : null;
            float top = UiKit.PageHeader(rect, Icons.Making, "CC_Making_Header".Translate(), "CC_Making_Sub".Translate(), stat, UiKit.Gold, 170f, "CC_Making_HeaderTip".Translate());
            if (Widgets.ButtonText(new Rect(rect.xMax - 160f, rect.y + 6f, 160f, 30f), "CC_Base_LookAgain".Translate()))
            {
                Making.For(map, true);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 18f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;

            y += UiKit.SectionTitle(0f, y, view.width, "CC_Making_Could".Translate(), null, UiKit.HeaderColor) + 4f;
            if (r.could.Count == 0) y += UiKit.Paragraph(8f, y, view.width - 16f, "CC_Making_CouldNone".Translate(), UiKit.Faint) + 8f;
            ThingDef lastBench = null;
            foreach (var o in r.could)
            {
                if (o.benchDef != lastBench)
                {
                    lastBench = o.benchDef;
                    Text.Font = GameFont.Small;
                    GUI.color = UiKit.Faint;
                    Widgets.Label(new Rect(8f, y + 4f, view.width - 16f, 22f), o.benchDef.LabelCap);
                    GUI.color = Color.white;
                    y += 26f;
                }
                y += DrawOption(o, 0f, y, view.width) + 4f;
            }
            y += 10f;

            y += UiKit.SectionTitle(0f, y, view.width, "CC_Making_Unused".Translate(), null, UiKit.HeaderColor) + 4f;
            if (r.unused.Count == 0) y += UiKit.Paragraph(8f, y, view.width - 16f, "CC_Making_UnusedNone".Translate(), UiKit.Faint) + 8f;
            foreach (var u in r.unused) y += DrawUnused(u, 0f, y, view.width, map) + 4f;
            y += 10f;

            if (r.idle.Count > 0)
            {
                y += UiKit.SectionTitle(0f, y, view.width, "CC_Making_Idle".Translate(r.idle.Count), null, UiKit.HeaderColor) + 4f;
                string names = string.Join(", ", r.idle.Take(8).Select(i => i.bench.LabelCap.ToString()));
                if (r.idle.Count > 8) names += " ...";
                y += UiKit.Paragraph(8f, y, view.width - 16f, "CC_Making_IdleLine".Translate(names), UiKit.Faint) + 8f;
            }
            height = Mathf.Max(y + 10f, 100f);
            Widgets.EndScrollView();
        }

        private static float DrawOption(MakingOption o, float x, float y, float width)
        {
            Text.Font = GameFont.Small;
            float textW = width - ButtonsW - 60f;
            string line = "CC_Making_From".Translate(o.PartsLine.Named("PARTS")).Resolve();
            Text.Font = GameFont.Tiny;
            float lineH = Text.CalcHeight(line, textW);
            Text.Font = GameFont.Small;
            float h = Mathf.Max(44f, 24f + lineH + 8f);
            var row = new Rect(x, y, width, h);
            UiKit.Card(row, UiKit.Good);
            Widgets.ThingIcon(new Rect(x + 10f, y + 6f, 28f, 28f), o.product);
            string title = o.product.LabelCap + (o.productStock > 0 ? " " + "CC_Making_Have".Translate(o.productStock).Resolve() : "");
            UiKit.LabelFit(new Rect(x + 46f, y + 4f, textW, 22f), title, Color.white);
            Text.Font = GameFont.Tiny;
            GUI.color = UiKit.Faint;
            Widgets.Label(new Rect(x + 46f, y + 24f, textW, lineH), line);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(new Rect(x, y, width - ButtonsW, h), o.recipe.LabelCap + "\n" + (o.recipe.description ?? ""));
            var br = new Rect(x + width - ButtonsW + 4f, y + 8f, ButtonsW - 12f, 26f);
            if (Widgets.ButtonText(br, "CC_Making_SetBill".Translate(o.target)))
            {
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
                Making.SetUpBill(o);
            }
            TooltipHandler.TipRegion(br, "CC_Making_SetBillTip".Translate(o.bench.LabelCap.ToString()));
            return h;
        }

        private static float DrawUnused(UnusedStock u, float x, float y, float width, Map map)
        {
            Text.Font = GameFont.Small;
            float textW = width - ButtonsW - 60f;
            string product = (u.recipe.ProducedThingDef ?? u.recipe.products[0].thingDef)?.label ?? u.recipe.label;
            string line = (u.benchBuilt ? "CC_Making_WouldUse_Built" : "CC_Making_WouldUse").Translate(u.benchDef.label.Named("BENCH"), product.Named("PRODUCT")).Resolve();
            if (u.research != null) line += " " + "CC_Making_NeedsResearch".Translate(u.research.LabelCap.ToString()).Resolve();
            Text.Font = GameFont.Tiny;
            float lineH = Text.CalcHeight(line, textW);
            Text.Font = GameFont.Small;
            float h = Mathf.Max(44f, 24f + lineH + 8f);
            var row = new Rect(x, y, width, h);
            UiKit.Card(row, UiKit.Info);
            Widgets.ThingIcon(new Rect(x + 10f, y + 6f, 28f, 28f), u.def);
            UiKit.LabelFit(new Rect(x + 46f, y + 4f, textW, 22f), u.def.LabelCap + " " + "CC_Making_Have".Translate(u.stock).Resolve(), Color.white);
            Text.Font = GameFont.Tiny;
            GUI.color = UiKit.Faint;
            Widgets.Label(new Rect(x + 46f, y + 24f, textW, lineH), line);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            var br = new Rect(x + width - ButtonsW + 4f, y + 8f, ButtonsW - 12f, 26f);
            if (u.research != null)
            {
                if (Widgets.ButtonText(br, "CC_Plan_OpenResearch".Translate()))
                {
                    var r = u.research;
                    Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Research);
                    if (MainButtonDefOf.Research.TabWindow is MainTabWindow_Research w) w.Select(r);
                }
            }
            else if (!u.benchBuilt && Widgets.ButtonText(br, "CC_Making_OpenBuild".Translate()))
            {
                var des = BuildCopyCommandUtility.FindAllowedDesignator(u.benchDef);
                if (des != null)
                {
                    Find.MainTabsRoot.EscapeCurrentTab(false);
                    Find.DesignatorManager.Select(des);
                }
            }
            return h;
        }
    }
}
