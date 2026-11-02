using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The top of the Making page: planning ahead (Building/Ahead.cs). The stock rows (what there is, what goes a day,
    /// how long it lasts, what to keep, how it stands), the fields against the year, the suggestions (the soonest
    /// short first, each with its numbers and Do it / Show / Not this), the steps done lately with Undo and whether
    /// each worked, and what was set aside, to bring back. Colony Manager's targets and the shopping list are drawn by
    /// <see cref="AheadMorePanel"/>. Every button shows its whole label and says what it does.
    /// </summary>
    public static class AheadPanel
    {
        private static readonly Folds folds = new Folds("aside");
        private const float Pad = 8f;

        private static readonly string[] Columns = { "CC_Ahead_Col_Good", "CC_Ahead_Col_Have", "CC_Ahead_Col_ADay", "CC_Ahead_Col_Days", "CC_Ahead_Col_Keep", "CC_Ahead_Col_Status" };

        /// <summary>Draws the section; returns its height.</summary>
        public static float Draw(float x, float y, float width, Map map)
        {
            float top = y;
            var view = Ahead.For(map);
            var needs = view.needs;
            var plan = view.plan;
            if (needs == null) return 0f;
            string from = needs.measured ? "CC_Ahead_FromRecord".Translate(needs.measuredDays.ToString("0.#")).Resolve()
                : ColonyUse.Recording ? "CC_Ahead_FromSoon".Translate().Resolve() : "CC_Ahead_FromOff".Translate().Resolve();
            y += UiKit.SectionTitle(x, y, width, "CC_Ahead_Section".Translate(), null, UiKit.HeaderColor);
            TooltipHandler.TipRegion(new Rect(x, y - UiKit.SectionHeight, width, 24f), "CC_Ahead_SectionTip".Translate());
            y += UiKit.Paragraph(x + Pad, y, width - 2 * Pad, "CC_Ahead_Intro".Translate(Ahead.Who(needs)).Resolve() + " " + from, UiKit.Faint, GameFont.Tiny) + 6f;

            y += Rows(x, y, width, plan, needs) + 6f;
            float managerH = AheadMorePanel.Manager(x, y, width, view);
            if (managerH > 0f) y += managerH + 6f;
            y += Fields(x, y, width, plan, needs) + 6f;
            float shopH = AheadMorePanel.Shopping(x, y, width, view);
            if (shopH > 0f) y += shopH + 6f;

            // The suggestions.
            var open = Ahead.Open(map);
            if (open.Count == 0) y += UiKit.Paragraph(x + Pad, y, width - 2 * Pad, "CC_Ahead_None".Translate(), UiKit.Faint) + 6f;
            foreach (var s in open) y += DrawStep(s, x, y, width) + 4f;

            y += Done(x, y + 4f, width, map) + 4f;
            y += Aside(x, y, width) + 8f;
            return y - top;
        }

        // ---------------- The stock rows ----------------

        private static string Num(float v) => v >= AheadMath.Far ? "-" : v.ToString(v < 10f ? "0.#" : "0");

        private static string StatusText(AheadRow r)
        {
            switch (r.status)
            {
                case AheadMath.Short: return "CC_Ahead_Status_Short".Translate();
                case AheadMath.Low: return "CC_Ahead_Status_Low".Translate();
                case AheadMath.Enough: return "CC_Ahead_Status_Enough".Translate();
                case AheadMath.More: return "CC_Ahead_Status_More".Translate(r.cells);
                default: return "CC_Ahead_Status_NoUse".Translate();
            }
        }

        private static Color StatusColor(int status) => status == AheadMath.Short ? UiKit.Bad : status == AheadMath.Low ? UiKit.Warn : status == AheadMath.More ? UiKit.Info : status == AheadMath.Enough ? UiKit.Good : UiKit.Faint;

        private static string RowLabel(AheadRow r) => r.kind == AheadMath.Material ? r.label.CapitalizeFirst() : ("CC_Ahead_Kind_" + r.kind).Translate().Resolve();

        private static float Rows(float x, float y, float width, AheadPlan plan, ColonyNeeds needs)
        {
            var rows = plan.rows.Where(r => r.kind == AheadMath.Material || r.have > 0f || r.useADay > 0f).ToList();
            if (rows.Count == 0) return 0f;
            float[] share = { 0.27f, 0.13f, 0.12f, 0.12f, 0.13f, 0.23f };
            float inner = width - 2 * Pad;
            float Col(int i) => x + Pad + share.Take(i).Sum() * inner;
            float top = y;
            Text.Font = GameFont.Tiny;
            GUI.color = UiKit.Faint;
            for (int i = 0; i < Columns.Length; i++)
            {
                var r = new Rect(Col(i), y, share[i] * inner - 4f, 18f);
                Widgets.Label(r, Columns[i].Translate());
                TooltipHandler.TipRegion(r, (Columns[i] + "Tip").Translate());
            }
            GUI.color = Color.white;
            y += 18f;
            int index = 0;
            foreach (var row in rows)
            {
                var line = new Rect(x, y, width, 22f);
                UiKit.Zebra(line, index++);
                if (Mouse.IsOver(line)) Widgets.DrawHighlight(line);
                Text.Font = GameFont.Small;
                UiKit.LabelFit(new Rect(Col(0), y, share[0] * inner - 4f, 22f), RowLabel(row), Color.white);
                Text.Font = GameFont.Tiny;
                bool material = row.kind == AheadMath.Material;
                string have = row.food ? Num(row.have) : ((int)row.have).ToString();
                UiKit.LabelFit(new Rect(Col(1), y, share[1] * inner - 4f, 22f), have, Color.white, GameFont.Tiny);
                UiKit.LabelFit(new Rect(Col(2), y, share[2] * inner - 4f, 22f), material ? "-" : Num(row.useADay), UiKit.Faint, GameFont.Tiny);
                UiKit.LabelFit(new Rect(Col(3), y, share[3] * inner - 4f, 22f), material ? "-" : Num(row.daysLeft), row.status == AheadMath.Short ? UiKit.Bad : UiKit.Faint, GameFont.Tiny);
                UiKit.LabelFit(new Rect(Col(4), y, share[4] * inner - 4f, 22f), row.food ? Num(row.target) : ((int)row.target).ToString(), UiKit.Faint, GameFont.Tiny);
                UiKit.LabelFit(new Rect(Col(5), y, share[5] * inner - 4f, 22f), material ? "CC_Ahead_Status_ShortBy".Translate((int)(row.target - row.have)).Resolve() : StatusText(row),
                    StatusColor(row.status), GameFont.Tiny);
                TooltipHandler.TipRegion(line, RowTip(row, needs));
                y += 22f;
            }
            Text.Font = GameFont.Small;
            return y - top;
        }

        private static string RowTip(AheadRow r, ColonyNeeds needs)
        {
            var lines = new List<string>();
            if (r.kind == AheadMath.Material) lines.Add("CC_Ahead_Tip_Material".Translate(r.label, (int)r.have, (int)(r.target - r.have)).Resolve());
            else
            {
                lines.Add(Ahead.RowReason(r, needs));
                lines.Add(Ahead.TargetReason(r));
                if (r.food) lines.Add("CC_Ahead_Tip_Food".Translate().Resolve());
            }
            if (r.parts.Count > 0)
            {
                lines.Add("");
                foreach (var g in r.parts.Take(12))
                    lines.Add(g.nutrition > 0f
                        ? "CC_Ahead_Tip_PartFood".Translate(g.label.CapitalizeFirst().Named("LABEL"), ((int)g.count).Named("COUNT"), Num(g.Nutrition).Named("NUTR"), g.cells.Named("CELLS")).Resolve()
                        : "CC_Ahead_Tip_Part".Translate(g.label.CapitalizeFirst().Named("LABEL"), ((int)g.count).Named("COUNT"), g.cells.Named("CELLS")).Resolve());
                if (r.parts.Count > 12) lines.Add("CC_Ahead_Tip_More".Translate(r.parts.Count - 12).Resolve());
            }
            return string.Join("\n", lines);
        }

        // ---------------- The fields ----------------

        private static float Fields(float x, float y, float width, AheadPlan plan, ColonyNeeds needs)
        {
            if (plan.cropNeed <= 0f && needs.fields.Count == 0) return 0f;
            string line = plan.cropNeed <= 0f ? "CC_Ahead_FieldsNone".Translate().Resolve()
                : "CC_Ahead_FieldsLine".Translate(Num(plan.cropNeed).Named("NEED"), Num(plan.cropGive).Named("GIVE"), Num(plan.growingLeftThisYear).Named("LEFT"),
                    plan.withoutDays.Named("WITHOUT"), plan.cycleDays.Named("CYCLE")).Resolve();
            if (plan.feedOnlyGive > 0f) line += " " + "CC_AheadMore_FieldsHay".Translate(Num(plan.feedOnlyGive).Named("HAY"), Num(plan.peopleNeed).Named("PEOPLE")).Resolve();
            if (plan.cellsShort > 0) line += " " + "CC_Ahead_FieldsShort".Translate(plan.cellsShort).Resolve();
            else if (plan.cropNeed > 0f) line += " " + "CC_Ahead_FieldsEnough".Translate().Resolve();
            float h = UiKit.Paragraph(x + Pad, y, width - 2 * Pad, line, plan.cellsShort > 0 ? UiKit.Warn : UiKit.Faint, GameFont.Tiny);
            var tip = new List<string> { "CC_Ahead_FieldsTip".Translate().Resolve(), "" };
            foreach (var f in needs.fields.OrderByDescending(f => f.food).ThenByDescending(f => f.cells).Take(20))
                tip.Add(f.food
                    ? "CC_Ahead_FieldTipFood".Translate(f.label.Named("ZONE"), f.cropLabel.Named("CROP"), f.cells.Named("CELLS"), f.sown.Named("SOWN"), Num(f.perCellADay * f.cells).Named("GIVE")).Resolve()
                    : "CC_Ahead_FieldTipOther".Translate(f.label.Named("ZONE"), f.cropLabel.Named("CROP"), f.cells.Named("CELLS"), ((int)f.stock).Named("STOCK"), f.productLabel.Named("PRODUCT")).Resolve());
            TooltipHandler.TipRegion(new Rect(x, y, width, h), string.Join("\n", tip));
            return h;
        }

        // ---------------- The suggestions ----------------

        /// <summary>The buttons of a step: each as wide as its label, one under the other.</summary>
        public static float ButtonsWidth(IEnumerable<string> labels, float least = 90f)
        {
            Text.Font = GameFont.Small;
            float w = least;
            foreach (var l in labels) w = Mathf.Max(w, Text.CalcSize(l ?? "").x + 20f);
            return w;
        }

        /// <summary>
        /// "Not this" for any source (an advisor card, a base finding, a piece of defense advice, a step of the plan): its
        /// key set aside in the shared store until brought back. Returns true when pressed.
        /// </summary>
        public static bool NotThis(Rect r, string key, string title)
        {
            bool pressed = Widgets.ButtonText(r, "CC_Ahead_NotThis".Translate());
            if (pressed)
            {
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                SuggestTracker.Instance?.Decline(key, title);
            }
            TooltipHandler.TipRegion(r, "CC_Ahead_NotThisTip".Translate());
            return pressed;
        }

        /// <summary>Does a step through the shared core (the player's click) and reads the pages' numbers again; true when it was done.</summary>
        public static bool DoStep(Suggestion s)
        {
            if (s?.apply == null) return false;
            var d = SuggestTracker.Instance?.Apply(s);
            if (d != null) Refresh(s.map);
            return d != null;
        }

        /// <summary>A step's Do it tooltip: what it does on the map, and that it can be undone and is checked later.</summary>
        public static string DoTip(Suggestion s) => (s?.doTip ?? s?.title ?? "") + "\n\n" + "CC_Ahead_DoItTip".Translate().Resolve();

        private static string UrgencyText(Suggestion s) =>
            s.days >= SuggestMath.NoRush || s.days >= AheadMath.Far ? (s.when.NullOrEmpty() ? "CC_Ahead_NoRush".Translate().Resolve() : s.when)
            : s.days < 0.05f ? "CC_Ahead_ShortNow".Translate().Resolve() : "CC_Ahead_ShortIn".Translate(Num(s.days)).Resolve();

        /// <summary>After a step was done or undone: the needs read again, so the pages show the map as it is now.</summary>
        private static void Refresh(Map map)
        {
            if (map == null) return;
            ColonyNeeds.For(map, true);
            Making.For(map, true);
            BuildAhead.Refresh(map);
        }

        private static Color LevelColor(int level) => level >= 4 ? UiKit.Bad : level >= 3 ? UiKit.Warn : level >= 2 ? UiKit.Gold : level >= 1 ? UiKit.Info : UiKit.Faint;

        /// <summary>One suggestion as a card, from any source: its urgency, its words and numbers, and Do it / Show / Not this.</summary>
        public static float DrawStep(Suggestion s, float x, float y, float width)
        {
            var labels = new List<string>();
            string doLabel = s.CanApply ? s.doLabel ?? "CC_Ahead_DoIt".Translate().Resolve() : null;
            if (doLabel != null) labels.Add(doLabel);
            string show = "CC_Ahead_Show".Translate().Resolve(), notThis = "CC_Ahead_NotThis".Translate().Resolve();
            if (s.CanShow) labels.Add(show);
            labels.Add(notThis);
            float bw = ButtonsWidth(labels);
            float textX = x + 10f, textW = width - bw - 30f;
            string urgency = UrgencyText(s);
            Text.Font = GameFont.Small;
            float titleH = Text.CalcHeight(s.title ?? "", textW);
            Text.Font = GameFont.Tiny;
            float reasonH = s.reason.NullOrEmpty() ? 0f : Text.CalcHeight(s.reason, textW);
            Text.Font = GameFont.Small;
            float pillH = UiKit.PillHeight;
            float h = Mathf.Max(pillH + titleH + reasonH + 16f, labels.Count * 28f + 8f);
            var card = new Rect(x, y, width, h);
            var color = LevelColor(s.Level);
            UiKit.Card(card, color);
            if (Mouse.IsOver(card) && s.map != null && s.map == Find.CurrentMap)
            {
                var cells = s.Cells();
                if (cells.Count > 0) MapHighlights.Cells("ahead:" + s.key, s.map, cells, color, s.title);
            }
            UiKit.Pill(textX, y + 5f, urgency, color, null, "CC_Ahead_UrgencyTip".Translate().Resolve());
            float ty = y + 5f + pillH + 2f;
            UiKit.Paragraph(textX, ty, textW, s.title ?? "", Color.white);
            ty += titleH;
            if (reasonH > 0f) UiKit.Paragraph(textX, ty, textW, s.reason, UiKit.Faint, GameFont.Tiny);

            // The buttons, each saying what it does.
            float bx = x + width - bw - 8f, by = y + 5f;
            var tracker = SuggestTracker.Instance;
            if (doLabel != null)
            {
                var r = new Rect(bx, by, bw, 24f);
                if (Widgets.ButtonText(r, doLabel))
                {
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    if (tracker?.Apply(s) != null) Refresh(s.map);
                }
                TooltipHandler.TipRegion(r, (s.doTip ?? s.title) + "\n\n" + "CC_Ahead_DoItTip".Translate().Resolve());
                by += 28f;
            }
            if (s.CanShow)
            {
                var r = new Rect(bx, by, bw, 24f);
                if (Widgets.ButtonText(r, show)) Show(s, color);
                TooltipHandler.TipRegion(r, "CC_Ahead_ShowTip".Translate().Resolve());
                by += 28f;
            }
            var nr = new Rect(bx, by, bw, 24f);
            if (Widgets.ButtonText(nr, notThis))
            {
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                tracker?.Decline(s);
            }
            TooltipHandler.TipRegion(nr, "CC_Ahead_NotThisTip".Translate().Resolve());
            return h;
        }

        /// <summary>Closes the window, moves the view to the step and keeps its cells outlined for a few seconds.</summary>
        private static void Show(Suggestion s, Color color)
        {
            if (s.map == null) return;
            var cells = s.Cells();
            Find.MainTabsRoot.EscapeCurrentTab(false);
            if (s.map != Find.CurrentMap) Current.Game.CurrentMap = s.map;
            if (cells.Count > 0)
            {
                MapHighlights.Hold("ahead-show:" + s.key, s.map, cells, color, s.title, 8f);
                int cx = (int)cells.Average(c => c.x), cz = (int)cells.Average(c => c.z);
                CameraJumper.TryJump(new IntVec3(cx, 0, cz), s.map);
            }
            else if (s.look != null && s.look.IsValid) CameraJumper.TryJump(s.look.PrimaryTarget);
        }

        // ---------------- Done lately ----------------

        /// <summary>The steps a source did lately on a map, each with Undo and whether it worked.</summary>
        public static float Done(float x, float y, float width, Map map, string source = Ahead.Source) => Done(x, y, width, map, new[] { source });

        /// <summary>The steps any of these sources did lately on a map, each with Undo and whether it worked.</summary>
        public static float Done(float x, float y, float width, Map map, IList<string> sources)
        {
            var tracker = SuggestTracker.Instance;
            var done = tracker?.DoneFor(sources, map, 6);
            if (done == null || done.Count == 0) return 0f;
            float top = y;
            Text.Font = GameFont.Small;
            GUI.color = UiKit.Faint;
            Widgets.Label(new Rect(x + Pad, y, width - 2 * Pad, 22f), "CC_Ahead_DoneTitle".Translate());
            GUI.color = Color.white;
            y += 24f;
            string undoLabel = "CC_Auto_Undo".Translate().Resolve();
            float bw = ButtonsWidth(new[] { undoLabel });
            foreach (var d in done)
            {
                string state;
                Color color;
                switch (d.state)
                {
                    case SuggestMath.Worked:
                        state = "CC_Ahead_State_Worked".Translate().Resolve();
                        color = UiKit.Good;
                        break;
                    case SuggestMath.Failed:
                        state = "CC_Ahead_State_Failed".Translate(d.why ?? "").Resolve();
                        color = UiKit.Warn;
                        break;
                    case SuggestMath.Undone:
                        state = "CC_Ahead_State_Undone".Translate().Resolve();
                        color = UiKit.Dim;
                        break;
                    case SuggestMath.Waiting:
                        state = d.tries > 0 && !d.why.NullOrEmpty() ? "CC_Ahead_State_NotYet".Translate(d.why).Resolve() : "CC_Ahead_State_Waiting".Translate().Resolve();
                        color = UiKit.Faint;
                        break;
                    default:
                        state = "CC_Ahead_State_Done".Translate().Resolve();
                        color = UiKit.Faint;
                        break;
                }
                string text = MainTabWindow_Council.DayString(d.tick) + ": " + d.text + " " + state;
                float tw = width - 2 * Pad - bw - 8f;
                Text.Font = GameFont.Tiny;
                float th = Text.CalcHeight(text, tw);
                GUI.color = color;
                Widgets.Label(new Rect(x + Pad, y, tw, th), text);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                if (d.CanUndo)
                {
                    var ur = new Rect(x + width - Pad - bw, y, bw, 24f);
                    if (Widgets.ButtonText(ur, undoLabel))
                    {
                        SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                        tracker.Undo(d);
                        Refresh(d.map);
                    }
                    TooltipHandler.TipRegion(ur, "CC_Ahead_UndoTip".Translate(d.text).Resolve());
                }
                y += Mathf.Max(th, 24f) + 4f;
            }
            return y - top;
        }

        // ---------------- Set aside ----------------

        /// <summary>What the player set aside of a source's suggestions, folded, each to bring back.</summary>
        public static float Aside(float x, float y, float width, string source = Ahead.Source, Folds into = null) => Aside(x, y, width, new[] { source }, into);

        /// <summary>What the player set aside of any of these sources, folded, each to bring back.</summary>
        public static float Aside(float x, float y, float width, IList<string> sources, Folds into = null)
        {
            var tracker = SuggestTracker.Instance;
            var aside = tracker?.DeclinedFor(sources);
            if (aside == null || aside.Count == 0) return 0f;
            float top = y;
            y += UiKit.FoldTitle(x, y, width, "CC_Ahead_Aside".Translate(aside.Count), into ?? folds, "aside", out bool open, null, UiKit.Faint);
            if (!open) return y - top;
            string back = "CC_Ahead_BringBack".Translate().Resolve();
            float bw = ButtonsWidth(new[] { back });
            foreach (var (key, title, _) in aside)
            {
                float tw = width - 2 * Pad - bw - 8f;
                Text.Font = GameFont.Tiny;
                float th = Text.CalcHeight(title, tw);
                GUI.color = UiKit.Faint;
                Widgets.Label(new Rect(x + Pad, y, tw, th), title);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                var r = new Rect(x + width - Pad - bw, y, bw, 24f);
                if (Widgets.ButtonText(r, back))
                {
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    tracker.BringBack(key);
                }
                TooltipHandler.TipRegion(r, "CC_Ahead_BringBackTip".Translate().Resolve());
                y += Mathf.Max(th, 24f) + 4f;
            }
            return y - top;
        }
    }
}
