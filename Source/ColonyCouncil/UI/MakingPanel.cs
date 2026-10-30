using System.Collections.Generic;
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
        private static List<(string key, bool on, List<string> texts)> preview = new List<(string, bool, List<string>)>();
        private static int previewFrame = -999;
        private static Map previewMap;

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

            // The chains followed to the end: what the building and the bills wait on, link by link, and the bills that can't go on.
            if (r.chains.Count > 0 || r.starved.Count > 0)
            {
                y += UiKit.SectionTitle(0f, y, view.width, "CC_Making_Chains".Translate(), null, UiKit.HeaderColor) + 4f;
                foreach (var root in r.chains)
                {
                    foreach (var node in root.All()) y += DrawLink(node, root, r, 0f, y, view.width, map) + 3f;
                    y += 6f;
                }
                foreach (var sb in r.starved) y += DrawStarved(sb, 0f, y, view.width) + 4f;
                y += 10f;
            }

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

            // The automations: what each would do now, and the last things done, undone at a click (Building/Automations.cs).
            var auto = CouncilGameComponent.Instance?.automations;
            if (auto != null)
            {
                y += UiKit.SectionTitle(0f, y, view.width, "CC_Auto_Section".Translate(), null, UiKit.HeaderColor) + 4f;
                TooltipHandler.TipRegion(new Rect(0f, y - 28f, view.width, 24f), "CC_Auto_SectionTip".Translate());
                if (Time.frameCount - previewFrame > 120 || previewMap != map)
                {
                    preview = auto.Preview(map);
                    previewFrame = Time.frameCount;
                    previewMap = map;
                }
                foreach (var (key, on, texts) in preview)
                {
                    string label = ("CC_Settings_" + char.ToUpperInvariant(key[0]) + key.Substring(1)).Translate();
                    string state = (on ? "CC_Check_Auto_On" : "CC_Check_Auto_Off").Translate();
                    string body = texts.Count == 0 ? "CC_Auto_WouldNothing".Translate().Resolve() : string.Join(" ", texts.Take(3));
                    y += UiKit.Paragraph(8f, y, view.width - 16f, label + " (" + state + "): " + body, on ? UiKit.Good : UiKit.Faint) + 4f;
                }
                var last = auto.actions.AsEnumerable().Reverse().Take(6).ToList();
                if (last.Count > 0)
                {
                    y += 4f;
                    Text.Font = GameFont.Small;
                    GUI.color = UiKit.Faint;
                    Widgets.Label(new Rect(8f, y, view.width - 16f, 22f), "CC_Auto_Last".Translate());
                    GUI.color = Color.white;
                    y += 24f;
                    foreach (var a in last)
                    {
                        Text.Font = GameFont.Tiny;
                        string text = "CC_DayN".Translate(a.tick / GenDate.TicksPerDay + 1).ToString() + ": " + a.text + (a.undone ? " " + "CC_Auto_UndoneMark".Translate() : "");
                        float th = Text.CalcHeight(text, view.width - 16f - 90f);
                        GUI.color = a.undone ? UiKit.Dim : UiKit.Faint;
                        Widgets.Label(new Rect(8f, y, view.width - 16f - 90f, th), text);
                        GUI.color = Color.white;
                        Text.Font = GameFont.Small;
                        if (a.CanUndo)
                        {
                            var ur = new Rect(view.width - 84f, y, 76f, 24f);
                            if (Widgets.ButtonText(ur, "CC_Auto_Undo".Translate()))
                            {
                                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                                auto.Undo(a);
                                previewFrame = -999;
                            }
                            TooltipHandler.TipRegion(ur, "CC_Auto_UndoTip".Translate());
                        }
                        y += Mathf.Max(th, 24f) + 4f;
                    }
                }
                y += 8f;
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
            if (Mouse.IsOver(row) && o.bench != null && o.bench.Spawned && o.bench.Map == Find.CurrentMap)
                MapHighlights.Cells("making:" + o.recipe.defName, o.bench.Map, o.bench.OccupiedRect().Cells, UiKit.Good, o.product.LabelCap, o.bench.Position);
            Widgets.ThingIcon(new Rect(x + 10f, y + 6f, 28f, 28f), o.product);
            string title = o.product.LabelCap + (o.productStock > 0 ? " " + "CC_Making_Have".Translate(o.productStock).Resolve() : "");
            if (o.wantedBy != null) title += " " + o.wantedBy;
            UiKit.LabelFit(new Rect(x + 46f, y + 4f, textW, 22f), title, o.wantedBy != null ? UiKit.Gold : Color.white);
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

        /// <summary>
        /// One link of a chain, indented by its depth: the thing, what there is and what is wanted, its use and make in a
        /// day and how long it lasts, the bench and batch that make it (or the plant that yields it, or why the chain
        /// stops here), and the bill, the whole chain's bills, or the sowing, at a click.
        /// </summary>
        private static float DrawLink(ChainNode n, ChainNode root, Making.Result r, float x, float y, float width, Map map)
        {
            float indent = n.depth * 18f;
            x += indent;
            width -= indent;
            Text.Font = GameFont.Small;
            float textW = width - ButtonsW - 60f;
            var lines = new List<string>();
            if (n == root && !n.why.NullOrEmpty()) lines.Add(n.why);
            else if (n != root) lines.Add("CC_Making_Chain_ForParent".Translate(root.thing.label).Resolve());
            if (n.useADay > 0f || n.makeADay > 0f)
            {
                float days = n.DaysLeft;
                lines.Add("CC_Making_Chain_Rates".Translate(n.useADay.ToString("0.#").Named("USE"), n.makeADay.ToString("0.#").Named("MAKE"),
                    (days >= ChainMath.Far ? "CC_Making_Chain_Never".Translate().Resolve() : "CC_Making_Chain_Days".Translate(days.ToString("0.#")).Resolve()).Named("DAYS")).Resolve());
            }
            if (n.cycle) lines.Add("CC_Making_Chain_Cycle".Translate());
            else if (n.tooDeep) lines.Add("CC_Making_Chain_Deep".Translate());
            else if (n.recipe != null)
            {
                lines.Add("CC_Making_Chain_Batch".Translate(((int)n.perBatch).Named("N"), (n.hoursABatch >= ChainMath.Far ? "?" : n.hoursABatch.ToString("0.#")).Named("H"),
                    n.bench.LabelCap.ToString().Named("BENCH"), n.PartsLine.Named("PARTS")).Resolve());
                if (n == root && n.parts.Count > 0) lines.Add("CC_Making_Chain_Lead".Translate(n.LeadDays.ToString("0.#")).Resolve());
            }
            else lines.Add("CC_Making_Chain_NoMaker".Translate());
            if (n.plant != null)
                lines.Add(n.zone != null ? "CC_Making_ZoneGrows".Translate(n.plant.label.Named("PLANT"), n.zone.Cells.Count.Named("N")).Resolve()
                    : "CC_Making_Chain_Plant".Translate(n.plant.label.Named("PLANT"), n.cells.Named("N")).Resolve());
            string line = string.Join(" ", lines);
            Text.Font = GameFont.Tiny;
            float lineH = Text.CalcHeight(line, textW);
            Text.Font = GameFont.Small;
            float h = Mathf.Max(44f, 24f + lineH + 8f);
            var row = new Rect(x, y, width, h);
            UiKit.Card(row, n.cycle || n.tooDeep ? UiKit.Dim : n.recipe == null && n.plant == null ? UiKit.Warn : n.canMake || n.plant != null ? UiKit.Gold : UiKit.Info);
            if (Mouse.IsOver(row) && n.bench != null && n.bench.Spawned && n.bench.Map == Find.CurrentMap)
                MapHighlights.Cells("chain:" + n.thing.defName + n.depth, n.bench.Map, n.bench.OccupiedRect().Cells, UiKit.Gold, n.thing.LabelCap, n.bench.Position);
            Widgets.ThingIcon(new Rect(x + 10f, y + 6f, 28f, 28f), n.thing);
            string title = n.thing.LabelCap + " " + "CC_Making_Have".Translate(n.stock).Resolve() + " " + "CC_Making_ShortLine".Translate(n.want).Resolve();
            UiKit.LabelFit(new Rect(x + 46f, y + 4f, textW, 22f), title, n == root ? UiKit.Gold : Color.white);
            Text.Font = GameFont.Tiny;
            GUI.color = UiKit.Faint;
            Widgets.Label(new Rect(x + 46f, y + 24f, textW, lineH), line);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            if (n.recipe != null) TooltipHandler.TipRegion(new Rect(x, y, width - ButtonsW, h), n.recipe.LabelCap + " (" + n.bench.def.label + ")\n" + (n.recipe.description ?? ""));
            var br = new Rect(x + width - ButtonsW + 4f, y + 8f, ButtonsW - 12f, 26f);
            // The whole chain's bills at the root, when more than one link is still to set.
            int unset = root.All().Count(o => o.recipe != null && !o.billed);
            if (n == root && unset >= 2)
            {
                if (Widgets.ButtonText(br, "CC_Making_Chain_SetAll".Translate(unset)))
                {
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    foreach (var link in r.needs.Where(c => c.root == root && !c.billed).ToList()) Making.SetUpChain(link, true);
                    Messages.Message("CC_Making_ChainSet".Translate(root.thing.label.Named("PRODUCT"), unset.Named("N")).Resolve(), new LookTargets(root.bench), MessageTypeDefOf.TaskCompletion, false);
                }
                TooltipHandler.TipRegion(br, "CC_Making_Chain_SetAllTip".Translate());
                br.y += 28f;
                h = Mathf.Max(h, 64f);
            }
            if (n.recipe != null)
            {
                if (n.billed) UiKit.Paragraph(br.x, br.y + 4f, br.width, "CC_Making_BillOn".Translate(), UiKit.Faint);
                else
                {
                    var link = r.needs.FirstOrDefault(c => c.node == n);
                    if (link != null && Widgets.ButtonText(br, "CC_Making_SetBill".Translate(n.target)))
                    {
                        SoundDefOf.Tick_High.PlayOneShotOnCamera();
                        Making.SetUpChain(link);
                    }
                    TooltipHandler.TipRegion(br, "CC_Making_SetBillTip".Translate(n.bench.LabelCap.ToString()));
                }
            }
            else if (n.plant != null && n.cells > 0)
            {
                if (Widgets.ButtonText(br, "CC_Making_Sow".Translate(n.cells)))
                {
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    var zone = Sowing.Sow(map, n.plant, n.cells);
                    if (zone != null)
                    {
                        Messages.Message("CC_Making_Sown".Translate(n.cells.Named("N"), n.plant.label.Named("PLANT"), n.thing.label.Named("PRODUCT")).Resolve(), new LookTargets(zone.Cells[0], map), MessageTypeDefOf.TaskCompletion, false);
                        Making.For(map, true);
                    }
                    else Messages.Message("CC_Making_NoGround".Translate(n.plant.label).Resolve(), MessageTypeDefOf.RejectInput, false);
                }
                TooltipHandler.TipRegion(br, "CC_Making_SowTip".Translate());
            }
            return h;
        }

        /// <summary>A link in a chain: the material wanted, the bench here that makes it, and its bill sized to the want.</summary>
        private static float DrawNeed(ChainNeed c, float x, float y, float width)
        {
            Text.Font = GameFont.Small;
            float textW = width - ButtonsW - 60f;
            string line = c.why + " " + (c.canMake ? "CC_Making_From".Translate(c.PartsLine.Named("PARTS")).Resolve() : "CC_Making_CantMake".Translate(c.PartsLine.Named("PARTS")).Resolve());
            Text.Font = GameFont.Tiny;
            float lineH = Text.CalcHeight(line, textW);
            Text.Font = GameFont.Small;
            float h = Mathf.Max(44f, 24f + lineH + 8f);
            var row = new Rect(x, y, width, h);
            UiKit.Card(row, c.canMake ? UiKit.Gold : UiKit.Warn);
            if (Mouse.IsOver(row) && c.bench != null && c.bench.Spawned && c.bench.Map == Find.CurrentMap)
                MapHighlights.Cells("chain:" + c.recipe.defName, c.bench.Map, c.bench.OccupiedRect().Cells, UiKit.Gold, c.product.LabelCap, c.bench.Position);
            Widgets.ThingIcon(new Rect(x + 10f, y + 6f, 28f, 28f), c.product);
            string title = c.product.LabelCap + " " + "CC_Making_Have".Translate(c.stock).Resolve() + (c.shortBy > 0 ? " " + "CC_Making_ShortLine".Translate(c.shortBy).Resolve() : "");
            UiKit.LabelFit(new Rect(x + 46f, y + 4f, textW, 22f), title, Color.white);
            Text.Font = GameFont.Tiny;
            GUI.color = UiKit.Faint;
            Widgets.Label(new Rect(x + 46f, y + 24f, textW, lineH), line);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(new Rect(x, y, width - ButtonsW, h), c.recipe.LabelCap + " (" + c.benchDef.label + ")\n" + (c.recipe.description ?? ""));
            var br = new Rect(x + width - ButtonsW + 4f, y + 8f, ButtonsW - 12f, 26f);
            if (c.billed) UiKit.Paragraph(br.x, br.y + 4f, br.width, "CC_Making_BillOn".Translate(), UiKit.Faint);
            else if (c.canMake)
            {
                if (Widgets.ButtonText(br, "CC_Making_SetBill".Translate(c.target)))
                {
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    Making.SetUpChain(c);
                }
                TooltipHandler.TipRegion(br, "CC_Making_SetBillTip".Translate(c.bench.LabelCap.ToString()));
            }
            else UiKit.Paragraph(br.x, br.y + 4f, br.width, "CC_Making_PartsShort".Translate(), UiKit.Warn);
            return h;
        }

        /// <summary>A bill that can't go on, what it waits on, and the bill that would feed it when a bench here can.</summary>
        private static float DrawStarved(StarvedBill sb, float x, float y, float width)
        {
            Text.Font = GameFont.Small;
            float textW = width - ButtonsW - 60f;
            string line = "CC_Making_Starved".Translate(sb.missing).Resolve();
            if (sb.fix == null) line += " " + "CC_Making_StarvedNoFix".Translate().Resolve();
            Text.Font = GameFont.Tiny;
            float lineH = Text.CalcHeight(line, textW);
            Text.Font = GameFont.Small;
            float h = Mathf.Max(44f, 24f + lineH + 8f);
            var row = new Rect(x, y, width, h);
            UiKit.Card(row, UiKit.Warn);
            if (Mouse.IsOver(row) && sb.bench != null && sb.bench.Spawned && sb.bench.Map == Find.CurrentMap)
                MapHighlights.Cells("starved:" + sb.bench.thingIDNumber, sb.bench.Map, sb.bench.OccupiedRect().Cells, UiKit.Warn, sb.bench.LabelCap, sb.bench.Position);
            if (sb.bench != null) Widgets.ThingIcon(new Rect(x + 10f, y + 6f, 28f, 28f), sb.bench);
            UiKit.LabelFit(new Rect(x + 46f, y + 4f, textW, 22f), sb.bench.LabelCap + ": " + sb.bill.LabelCap, Color.white);
            Text.Font = GameFont.Tiny;
            GUI.color = UiKit.Faint;
            Widgets.Label(new Rect(x + 46f, y + 24f, textW, lineH), line);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            var br = new Rect(x + width - ButtonsW + 4f, y + 8f, ButtonsW - 12f, 26f);
            if (sb.fix != null && sb.fix.canMake && !sb.fix.billed)
            {
                if (Widgets.ButtonText(br, "CC_Making_SetBillFor".Translate(sb.fix.product.label)))
                {
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    Making.SetUpChain(sb.fix);
                }
                TooltipHandler.TipRegion(br, "CC_Making_SetBillTip".Translate(sb.fix.bench.LabelCap.ToString()));
            }
            else if (sb.fix != null && sb.fix.billed) UiKit.Paragraph(br.x, br.y + 4f, br.width, "CC_Making_BillOn".Translate(), UiKit.Faint);
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
            if (Mouse.IsOver(row) && map == Find.CurrentMap)
                MapHighlights.Cells("unused:" + u.def.defName, map, map.listerThings.ThingsOfDef(u.def).Where(t => t.Spawned).Take(12).Select(t => t.Position), UiKit.Info, u.def.LabelCap);
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
