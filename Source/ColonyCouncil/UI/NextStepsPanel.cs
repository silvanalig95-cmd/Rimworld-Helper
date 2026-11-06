using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Planning → Plan: the colony's next steps (Building/NextSteps.cs). The plan first, in the player's order, each
    /// step with what opens to do it, arrows to move it, done and let go; a line to add a step of their own (with a
    /// room plan, if it's a room); then what the council proposes, most pressing first, each to take into the plan or
    /// let go.
    /// </summary>
    public static class NextStepsPanel
    {
        private static Vector2 scroll;
        private static float height = 800f;
        private static string newStep = "";
        private static RoomTemplateDef newTemplate;

        private const float ButtonsW = 290f, LeftW = 44f;

        /// <summary>The Plan page with the next steps switched off: building ahead alone, and a line saying the steps are off.</summary>
        private static void DrawBuildAheadOnly(Rect rect, Map map)
        {
            float top = UiKit.PageHeader(rect, Icons.Plan, "CC_Plan_Header".Translate(), "CC_Plan_Sub".Translate(), null, UiKit.Gold, 170f, "CC_Plan_HeaderTip".Translate());
            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 18f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;
            y += BuildAheadPanel.Draw(0f, y, view.width, map) + 10f;
            UiKit.EmptyLine(new Rect(0f, y, view.width, UiKit.RowStandard), "CC_Plan_OffWhatsOn".Translate());
            y += UiKit.RowStandard + UiKit.CardGap;
            height = Mathf.Max(y + 20f, outRect.height);
            Widgets.EndScrollView();
        }

        public static void Draw(Rect rect, CouncilGameComponent comp, Map map)
        {
            if (map == null || !map.IsPlayerHome || comp == null)
            {
                UiKit.EmptyState(rect, Icons.Colony, "CC_NoHomeMap".Translate());
                return;
            }
            if (!NextSteps.On)
            {
                // The next steps are off, but building ahead stays: it is planning ahead, not the plan.
                DrawBuildAheadOnly(rect, map);
                return;
            }
            var plan = NextSteps.PlanFor(map);
            var proposed = NextSteps.ProposedFor(map);
            int open = plan.Count(s => s.Planned);
            string stat = open > 0 ? "CC_Plan_Stat".Translate(open).Resolve() : null;
            float top = UiKit.PageHeader(rect, Icons.Plan, "CC_Plan_Header".Translate(), "CC_Plan_Sub".Translate(), stat, UiKit.Gold, 170f, "CC_Plan_HeaderTip".Translate());
            if (open > 1 && UiKit.ActButton(new Rect(rect.xMax - 160f, rect.y + 6f, 160f, UiKit.RowStandard), "CC_Plan_SortUrgency".Translate(), null)) NextSteps.SortByUrgency(map);
            if (UiKit.ActButton(new Rect(rect.xMax - 160f, rect.y + 38f, 160f, UiKit.RowStandard - 2f), "CC_Ground_Button".Translate(), null, "CC_Ground_ButtonTip".Translate()))
            {
                Find.MainTabsRoot.EscapeCurrentTab(false);
                GroundPlan.Show(map);
            }

            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 18f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;

            // Building ahead: the rooms the colony will be short of and what to build next (UI/BuildAheadPanel.cs).
            y += BuildAheadPanel.Draw(0f, y, view.width, map) + 10f;

            y += UiKit.SectionTitle(0f, y, view.width, "CC_Plan_ThePlan".Translate(), open, Icons.Plan) + 4f;
            if (plan.Count == 0)
            {
                UiKit.EmptyLine(new Rect(0f, y, view.width, UiKit.RowStandard), "CC_Plan_Empty".Translate());
                y += UiKit.RowStandard + UiKit.CardGap;
            }
            int n = 0;
            foreach (var s in plan)
            {
                n += s.Planned ? 1 : 0;
                y += DrawStep(s, s.Planned ? n : 0, 0f, y, view.width, true, plan.IndexOf(s) == 0, plan.IndexOf(s) == plan.Count - 1) + UiKit.CardGap;
            }
            y += DrawAddOwn(0f, y, view.width, map) + UiKit.SectionGap;

            // What the council proposes, its act ("Take them all") on the heading's right.
            UiKit.SectionHeading(0f, y, view.width, "CC_Plan_Proposes".Translate(), proposed.Count, null, null, out _,
                proposed.Count > 0 ? "CC_Plan_TakeAll".Translate().ToString() : null, null, out bool takeAll, "CC_Same_Plan_TakeAllTip".Translate());
            if (takeAll) NextSteps.TakeAll(map);
            y += UiKit.HeadingHeight + 4f;
            if (proposed.Count == 0)
            {
                UiKit.EmptyLine(new Rect(0f, y, view.width, UiKit.RowStandard), "CC_Plan_NothingProposed".Translate());
                y += UiKit.RowStandard + UiKit.CardGap;
            }
            else
                foreach (var s in proposed) y += DrawStep(s, 0, 0f, y, view.width, false, false, false) + UiKit.CardGap;
            // The steps done from here lately (a fix, a room's lamps, the research started), with Undo and whether each
            // worked; and what was set aside with "Not this", here or on its own page, to bring back.
            y += AheadPanel.Done(0f, y + 4f, view.width, map, NextSteps.Sources) + 4f;
            y += AheadPanel.Aside(0f, y, view.width, NextSteps.Sources, folds) + 8f;
            height = Mathf.Max(y + 10f, 100f);
            Widgets.EndScrollView();
        }

        private static readonly Folds folds = new Folds("aside");

        /// <summary>
        /// Where each button goes in the button column: two to a row, a label too long for half the column on a row of its
        /// own (x, y and width within the column).
        /// </summary>
        private static List<Rect> ButtonSpots(List<string> labels, float columnW, out int rows)
        {
            var spots = new List<Rect>();
            float half = (columnW - 16f) / 2f, full = columnW - 12f;
            int row = 0, col = 0;
            Text.Font = GameFont.Small;
            foreach (var label in labels)
            {
                bool wide = Text.CalcSize(label ?? "").x + 12f > half;
                if (wide && col == 1)
                {
                    row++;
                    col = 0;
                }
                spots.Add(new Rect(col * (half + 4f), row * 28f, wide ? full : half, 24f));
                if (wide || col == 1)
                {
                    row++;
                    col = 0;
                }
                else col = 1;
            }
            rows = row + (col == 1 ? 1 : 0);
            return spots;
        }

        private static float TextWidth(float width, float buttonsW) => Mathf.Max(80f, width - LeftW - buttonsW - 12f);

        private static float DrawStep(NextStep s, int number, float x, float y, float width, bool inPlan, bool first, bool last)
        {
            var level = NextSteps.LevelOf(s);
            var color = s.Done ? UiKit.Dim : s.HasSource ? UiKit.LevelColor(level) : UiKit.Cool;
            var acts = s.Done ? new List<(string label, string tip, System.Action act)>() : NextSteps.Actions(s);
            if (s.Done)
            {
                acts.Add(("CC_Plan_TakeBack".Translate(), "CC_Plan_TakeBackTip".Translate(), () => NextSteps.TakeBack(s)));
                acts.Add(("CC_Plan_Remove".Translate(), "CC_Plan_RemoveTip".Translate(), () => NextSteps.Drop(s)));
            }
            else if (inPlan)
            {
                acts.Add(("CC_Plan_MarkDone".Translate(), "CC_Plan_MarkDoneTip".Translate(), () => NextSteps.MarkDone(s)));
                // A step with a source is set aside like any suggestion (Bring back below); the player's own just goes.
                if (s.HasSource) acts.Add(("CC_Ahead_NotThis".Translate(), "CC_Same_Plan_NotThisTip".Translate(), () => NextSteps.Drop(s)));
                else acts.Add(("CC_Plan_Remove".Translate(), "CC_Plan_RemoveOwnTip".Translate(), () => NextSteps.Drop(s)));
            }
            else
            {
                acts.Add(("CC_Plan_Take".Translate(), "CC_Plan_TakeTip".Translate(), () => NextSteps.Take(s)));
                acts.Add(("CC_Ahead_NotThis".Translate(), "CC_Same_Plan_NotThisTip".Translate(), () => NextSteps.Drop(s)));
            }
            // The button column as wide as its longest label needs, so every label shows whole.
            Text.Font = GameFont.Small;
            float buttonsW = Mathf.Max(ButtonsW, acts.Count == 0 ? 0f : acts.Max(a => Text.CalcSize(a.label ?? "").x) + 24f);
            var spots = ButtonSpots(acts.Select(a => a.label).ToList(), buttonsW, out int buttonRows);
            Text.Font = GameFont.Small;
            float textW = TextWidth(width, buttonsW);
            float titleH = Text.CalcHeight(s.title ?? "", textW);
            Text.Font = GameFont.Tiny;
            float detailH = s.detail.NullOrEmpty() ? 0f : Text.CalcHeight(s.detail, textW);
            Text.Font = GameFont.Small;
            // A planned step with a source shows how urgent it is too (its level), above its words.
            bool pillInText = inPlan && !s.Done && s.HasSource;
            float pillH = pillInText ? UiKit.PillHeight + 2f : 0f;
            // Buttons: what it opens, then done / take, not this; two to a row, a long label on a row of its own.
            float h = Mathf.Max(52f, pillH + titleH + detailH + 18f, buttonRows * 28f + 10f);
            var row = new Rect(x, y, width, h);
            UiKit.Card(row, color);
            if (Mouse.IsOver(row) && s.map != null && s.map == Find.CurrentMap && s.look.IsValid && s.look.InBounds(s.map))
            {
                // A step from the base review shows what the review drew for it; any other, where it points.
                var finding = s.kind == NextStep.KindBase && s.key.StartsWith("base:") ? BaseReview.Finding(s.map, s.key.Substring(5)) : null;
                if (finding != null) BaseOverlay.Highlight(s.map, finding, true);
                else MapHighlights.Cells("plan:" + s.key, s.map, GenRadial.RadialCellsAround(s.look, 1.5f, true), color, s.title, s.look);
            }

            // The number in the plan (or the level, for a proposal), and the arrows.
            if (inPlan)
            {
                // Its place in the plan, in small type (Medium is for a view's or a card's one title).
                UiKit.LabelFit(new Rect(x + 8f, y + 6f, LeftW - 10f, 22f), s.Done ? "" : number.ToString(), color, GameFont.Small, TextAnchor.MiddleCenter);
                if (!s.Done)
                {
                    var up = new Rect(x + 10f, y + h - 24f, 14f, 14f);
                    var down = new Rect(x + 24f, y + h - 24f, 14f, 14f);
                    if (!first && Widgets.ButtonImage(up, TexButton.ReorderUp, UiKit.Ink, GenUI.MouseoverColor)) NextSteps.Move(s, -1);
                    if (!last && Widgets.ButtonImage(down, TexButton.ReorderDown, UiKit.Ink, GenUI.MouseoverColor)) NextSteps.Move(s, 1);
                }
            }
            else UiKit.Pill(x + 8f, y + 8f, ("CC_Level_" + level).Translate(), color, level >= AdviceLevel.High ? Icons.Warning : null, null, LeftW + 40f);

            float textX = x + (inPlan ? LeftW : LeftW + 50f);
            float tw = inPlan ? textW : textW - 50f;
            float cy = y + 8f;
            if (pillInText)
            {
                UiKit.Pill(textX, cy, ("CC_Level_" + level).Translate(), color, level >= AdviceLevel.High ? Icons.Warning : null, "CC_Same_Plan_LevelTip".Translate(), 200f);
                cy += pillH;
            }
            GUI.color = s.Done ? UiKit.Dim : UiKit.Ink;
            Widgets.Label(new Rect(textX, cy, tw, titleH), s.title);
            cy += titleH;
            if (detailH > 0f)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = s.Done ? UiKit.Dim : UiKit.Faint;
                Widgets.Label(new Rect(textX, cy, tw, detailH), s.detail);
                Text.Font = GameFont.Small;
            }
            GUI.color = UiKit.Ink;
            if (s.Done)
            {
                UiKit.Pill(textX, y + h - 24f, "CC_Plan_DonePill".Translate(), UiKit.Done, null, null, 120f);
            }
            else if (s.thing != null)
            {
                var iconRect = new Rect(textX + tw - 30f, y + 8f, 26f, 26f);
                Widgets.ThingIcon(iconRect.ContractedBy(1f), s.thing);
                TooltipHandler.TipRegion(iconRect, s.thing.LabelCap);
            }
            TooltipHandler.TipRegion(new Rect(textX, y, tw, h), KindTip(s));

            // The buttons, each its whole label wide (the column grows for a long one), each saying on hover what it does.
            float bx = x + width - buttonsW + 4f;
            for (int i = 0; i < acts.Count; i++)
            {
                var (label, tip, act) = acts[i];
                var spot = spots[i];
                var r = new Rect(bx + spot.x, y + 6f + spot.y, spot.width, 24f);
                if (UiKit.ActButton(r, label, null, tip))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    act();
                }
            }
            return h;
        }

        private static string KindTip(NextStep s)
        {
            string from = ("CC_Plan_From_" + s.kind).Translate().Resolve();
            string since = "CC_Plan_Since".Translate(UiKit.Duration(Mathf.Max(0, Find.TickManager.TicksGame - s.addedTick))).Resolve();
            return from + "\n" + since;
        }

        private static float DrawAddOwn(float x, float y, float width, Map map)
        {
            float h = 34f;
            var row = new Rect(x, y, width, h);
            UiKit.Card(row, null, false, false);
            var field = new Rect(x + 8f, y + 5f, Mathf.Max(120f, width - 8f - 240f - 110f - 24f), 24f);
            string shown = Widgets.TextField(field, newStep ?? "");
            if (shown != newStep) newStep = shown.Length > 120 ? shown.Substring(0, 120) : shown;
            if (newStep.NullOrEmpty())
            {
                GUI.color = UiKit.Dim;
                Widgets.Label(new Rect(field.x + 6f, field.y + 2f, field.width - 8f, 22f), "CC_Plan_OwnHint".Translate());
                GUI.color = UiKit.Ink;
            }
            // A room plan for the step, if it's a room to build.
            var roomRect = new Rect(field.xMax + 8f, y + 5f, 240f, 24f);
            string roomLabel = newTemplate != null ? newTemplate.FullLabel : "CC_Plan_OwnNoRoom".Translate().Resolve();
            if (UiKit.ActButton(roomRect, UiKit.Fit(roomLabel, 230f), null))
            {
                var options = new List<FloatMenuOption> { new FloatMenuOption("CC_Plan_OwnNoRoom".Translate(), () => newTemplate = null) };
                foreach (var t in RoomMenu.Entries(map, false, false).OrderBy(t => t.label))
                {
                    var tt = t;
                    options.Add(new FloatMenuOption(tt.FullLabel, () => newTemplate = tt));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            var add = new Rect(roomRect.xMax + 8f, y + 5f, 100f, 24f);
            bool can = !newStep.NullOrEmpty() && newStep.Trim().Length > 0;
            // Greyed with why not until there is something to add.
            if (UiKit.ActButton(add, "CC_Plan_Add".Translate(), can ? null : "CC_Plan_AddWhyNot".Translate().ToString()))
            {
                NextSteps.AddOwn(map, newStep, newTemplate);
                newStep = "";
                newTemplate = null;
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
            return h;
        }

    }
}
