using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Colony, then Plan: the colony's next steps (Building/NextSteps.cs). The plan first, in the player's order, each
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

        private const float ButtonsW = 150f, LeftW = 44f;

        public static void Draw(Rect rect, CouncilGameComponent comp, Map map)
        {
            if (!NextSteps.On)
            {
                UiKit.EmptyState(rect, Icons.Plan, "CC_Plan_Off".Translate());
                return;
            }
            if (map == null || !map.IsPlayerHome || comp == null)
            {
                UiKit.EmptyState(rect, Icons.Colony, "CC_NoHomeMap".Translate());
                return;
            }
            var plan = NextSteps.PlanFor(map);
            var proposed = NextSteps.ProposedFor(map);
            int open = plan.Count(s => s.Planned);
            string stat = open > 0 ? "CC_Plan_Stat".Translate(open).Resolve() : null;
            float top = UiKit.PageHeader(rect, Icons.Plan, "CC_Plan_Header".Translate(), "CC_Plan_Sub".Translate(), stat, UiKit.Gold, 170f, "CC_Plan_HeaderTip".Translate());
            if (open > 1 && Widgets.ButtonText(new Rect(rect.xMax - 160f, rect.y + 6f, 160f, 30f), "CC_Plan_SortUrgency".Translate())) NextSteps.SortByUrgency(map);

            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 18f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;

            y += UiKit.SectionTitle(0f, y, view.width, "CC_Plan_ThePlan".Translate(), Icons.Plan, UiKit.HeaderColor) + 4f;
            if (plan.Count == 0) y += UiKit.Paragraph(8f, y, view.width - 16f, "CC_Plan_Empty".Translate(), UiKit.Faint) + 8f;
            int n = 0;
            foreach (var s in plan)
            {
                n += s.Planned ? 1 : 0;
                y += DrawStep(s, s.Planned ? n : 0, 0f, y, view.width, true, plan.IndexOf(s) == 0, plan.IndexOf(s) == plan.Count - 1) + 6f;
            }
            y += DrawAddOwn(0f, y, view.width, map) + 12f;

            y += UiKit.SectionTitle(0f, y, view.width, "CC_Plan_Proposes".Translate(), Icons.Priorities, UiKit.HeaderColor,
                proposed.Count > 1 ? null : null) + 4f;
            if (proposed.Count == 0) y += UiKit.Paragraph(8f, y, view.width - 16f, "CC_Plan_NothingProposed".Translate(), UiKit.Faint) + 8f;
            else
            {
                if (Widgets.ButtonText(new Rect(view.width - 170f, y - 30f, 166f, 26f), "CC_Plan_TakeAll".Translate())) NextSteps.TakeAll(map);
                foreach (var s in proposed) y += DrawStep(s, 0, 0f, y, view.width, false, false, false) + 6f;
            }
            height = Mathf.Max(y + 10f, 100f);
            Widgets.EndScrollView();
        }

        private static float TextWidth(float width) => Mathf.Max(80f, width - LeftW - ButtonsW - 12f);

        private static float DrawStep(NextStep s, int number, float x, float y, float width, bool inPlan, bool first, bool last)
        {
            var level = NextSteps.LevelOf(s);
            var color = s.Done ? UiKit.Dim : s.HasSource ? LevelColor(level) : UiKit.Info;
            var acts = s.Done ? new List<(string, System.Action)>() : NextSteps.Actions(s);
            Text.Font = GameFont.Small;
            float textW = TextWidth(width);
            float titleH = Text.CalcHeight(s.title ?? "", textW);
            Text.Font = GameFont.Tiny;
            float detailH = s.detail.NullOrEmpty() ? 0f : Text.CalcHeight(s.detail, textW);
            Text.Font = GameFont.Small;
            // Buttons: what it opens, then done / take, let go; two to a row when there are many.
            int buttons = acts.Count + (s.Done ? 2 : inPlan ? 2 : 2);
            float h = Mathf.Max(52f, titleH + detailH + 18f, Mathf.CeilToInt(buttons / 2f) * 28f + 10f);
            var row = new Rect(x, y, width, h);
            UiKit.Card(row, color);

            // The number in the plan (or the level, for a proposal), and the arrows.
            if (inPlan)
            {
                UiKit.LabelFit(new Rect(x + 8f, y + 6f, LeftW - 10f, 22f), s.Done ? "" : number.ToString(), color, GameFont.Medium, TextAnchor.MiddleCenter);
                if (!s.Done)
                {
                    var up = new Rect(x + 10f, y + h - 24f, 14f, 14f);
                    var down = new Rect(x + 24f, y + h - 24f, 14f, 14f);
                    if (!first && Widgets.ButtonImage(up, TexButton.ReorderUp, Color.white, GenUI.MouseoverColor)) NextSteps.Move(s, -1);
                    if (!last && Widgets.ButtonImage(down, TexButton.ReorderDown, Color.white, GenUI.MouseoverColor)) NextSteps.Move(s, 1);
                }
            }
            else UiKit.Pill(x + 8f, y + 8f, ("CC_Level_" + level).Translate(), color, level >= AdviceLevel.High ? Icons.Warning : null, null, LeftW + 40f);

            float textX = x + (inPlan ? LeftW : LeftW + 50f);
            float tw = inPlan ? textW : textW - 50f;
            float cy = y + 8f;
            GUI.color = s.Done ? UiKit.Dim : Color.white;
            Widgets.Label(new Rect(textX, cy, tw, titleH), s.title);
            cy += titleH;
            if (detailH > 0f)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = s.Done ? UiKit.Dim : UiKit.Faint;
                Widgets.Label(new Rect(textX, cy, tw, detailH), s.detail);
                Text.Font = GameFont.Small;
            }
            GUI.color = Color.white;
            if (s.Done)
            {
                UiKit.Pill(textX, y + h - 24f, "CC_Plan_DonePill".Translate(), UiKit.Good, null, null, 120f);
            }
            else if (s.thing != null)
            {
                var iconRect = new Rect(textX + tw - 30f, y + 8f, 26f, 26f);
                Widgets.ThingIcon(iconRect.ContractedBy(1f), s.thing);
                TooltipHandler.TipRegion(iconRect, s.thing.LabelCap);
            }
            TooltipHandler.TipRegion(new Rect(textX, y, tw, h), KindTip(s));

            // The buttons.
            float bx = x + width - ButtonsW + 4f, bw = (ButtonsW - 16f) / 2f;
            int slot = 0;
            Rect Next()
            {
                var r = new Rect(bx + (slot % 2) * (bw + 4f), y + 6f + (slot / 2) * 28f, bw, 24f);
                slot++;
                return r;
            }
            foreach (var (label, act) in acts)
                if (Widgets.ButtonText(Next(), UiKit.Fit(label, bw - 8f)))
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    act();
                }
            if (s.Done)
            {
                if (Widgets.ButtonText(Next(), UiKit.Fit("CC_Plan_TakeBack".Translate(), bw - 8f))) NextSteps.TakeBack(s);
                if (Widgets.ButtonText(Next(), UiKit.Fit("CC_Plan_Remove".Translate(), bw - 8f))) NextSteps.Drop(s);
            }
            else if (inPlan)
            {
                if (Widgets.ButtonText(Next(), UiKit.Fit("CC_Plan_MarkDone".Translate(), bw - 8f))) NextSteps.MarkDone(s);
                var drop = Next();
                TooltipHandler.TipRegion(drop, s.HasSource ? "CC_Plan_LetGoTip".Translate() : "CC_Plan_RemoveOwnTip".Translate());
                if (Widgets.ButtonText(drop, UiKit.Fit("CC_Plan_LetGo".Translate(), bw - 8f))) NextSteps.Drop(s);
            }
            else
            {
                if (Widgets.ButtonText(Next(), UiKit.Fit("CC_Plan_Take".Translate(), bw - 8f))) NextSteps.Take(s);
                var drop = Next();
                TooltipHandler.TipRegion(drop, "CC_Plan_LetGoTip".Translate());
                if (Widgets.ButtonText(drop, UiKit.Fit("CC_Plan_NotNow".Translate(), bw - 8f))) NextSteps.Drop(s);
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
                GUI.color = Color.white;
            }
            // A room plan for the step, if it's a room to build.
            var roomRect = new Rect(field.xMax + 8f, y + 5f, 240f, 24f);
            string roomLabel = newTemplate != null ? newTemplate.FullLabel : "CC_Plan_OwnNoRoom".Translate().Resolve();
            if (Widgets.ButtonText(roomRect, UiKit.Fit(roomLabel, 230f)))
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
            if (Widgets.ButtonText(add, "CC_Plan_Add".Translate(), true, true, can) && can)
            {
                NextSteps.AddOwn(map, newStep, newTemplate);
                newStep = "";
                newTemplate = null;
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
            return h;
        }

        private static Color LevelColor(AdviceLevel level)
        {
            switch (level)
            {
                case AdviceLevel.Critical: return new Color(0.9f, 0.3f, 0.25f);
                case AdviceLevel.High: return new Color(0.95f, 0.6f, 0.2f);
                case AdviceLevel.Medium: return new Color(0.9f, 0.85f, 0.35f);
                case AdviceLevel.Low: return new Color(0.65f, 0.65f, 0.65f);
                default: return new Color(0.45f, 0.7f, 0.95f);
            }
        }
    }
}
