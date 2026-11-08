using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Planning, then Base: the base looked over (Building/BaseReview.cs). A little map of the base on the left
    /// (UI/BaseMiniMap.cs) with the rooms by their use and the findings marked on it; on the right what the review
    /// found, most pressing first, in parts: the rooms, the walks, the stores, the doors, the walls, the works. Each
    /// with where to look and, where the council can do it at a click, the order to give. A hovered finding brightens
    /// on the map and the little map alike; "Show on the map" keeps the findings drawn with the window closed.
    /// </summary>
    public static class BasePanel
    {
        private static Vector2 scroll;
        private static float height = 600f;

        private const float ButtonsW = 150f, LeftW = 110f, MapW = 330f, Gap = 14f;
        private static readonly string[] Groups = { "rooms", "walks", "stores", "doors", "walls", "works" };

        public static void Draw(Rect rect, Map map)
        {
            if (map == null || !map.IsPlayerHome)
            {
                UiKit.EmptyState(rect, Icons.Colony, "CC_NoHomeMap".Translate());
                return;
            }
            BaseOverlay.PageDrawn();
            var findings = BaseReview.For(map);
            int pressing = findings.Count(f => f.Level >= AdviceLevel.Medium);
            string stat = findings.Count > 0 ? "CC_Base_Stat".Translate(findings.Count, pressing).Resolve() : null;
            float top = UiKit.PageHeader(rect, Icons.Base, "CC_Base_Header".Translate(), "CC_Base_Sub".Translate(), stat, UiKit.Gold, 330f, "CC_Base_HeaderTip".Translate());
            if (UiKit.ActButton(new Rect(rect.xMax - 160f, rect.y + 6f, 160f, UiKit.RowStandard), "CC_Base_LookAgain".Translate(), null)) BaseReview.For(map, true);
            bool show = BaseOverlay.Show;
            var showRect = new Rect(rect.xMax - 320f, rect.y + 8f, 150f, 24f);
            UiKit.Toggle(showRect, "CC_Base_ShowOnMap".Translate(), ref show);
            TooltipHandler.TipRegion(showRect, "CC_Base_ShowOnMap_Tip".Translate());
            if (show != BaseOverlay.Show) BaseOverlay.Show = show;

            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 18f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float w = view.width;
                bool two = w >= 760f;
                float lw = two ? MapW : w;
                float rx = two ? lw + Gap : 0f;
                float rw = two ? w - lw - Gap : w;
                float left = DrawLeft(map, findings, 0f, 0f, lw);
                float right = DrawFindings(map, findings, rx, two ? 0f : left + 10f, rw);
                height = Mathf.Max(two ? Mathf.Max(left, right) : right, 100f) + 10f;
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = UiKit.Ink;
            }
        }

        // ---------------- Left: the little map and what it shows ----------------

        private static float DrawLeft(Map map, List<BaseFinding> findings, float x, float y, float w)
        {
            float mapH = Mathf.Min(w, 330f) - 16f;
            var box = new Rect(x, y, w, mapH + 16f);
            UiKit.Card(box, null, false, false);
            var area = new Rect(x + 8f, y + 8f, w - 16f, mapH);
            if (BaseMiniMap.Any(map)) BaseMiniMap.Draw(area, map, findings);
            else UiKit.EmptyState(area, Icons.Base, "CC_Base_MapNone".Translate());
            y += box.height + 6f;

            // What the colours mean: the findings' parts, then the kinds of room.
            var legend = new List<(Color, string)>();
            foreach (var group in Groups)
            {
                if (!findings.Any(f => f.group == group)) continue;
                legend.Add((BaseOverlay.ColorOf(new BaseFinding { group = group }), ("CC_Base_Group_" + group).Translate().Resolve().ToLower()));
            }
            var roles = BaseReview.RoomsByRole(map);
            foreach (var (role, color) in BaseMiniMap.RoomColors)
            {
                int n = roles.Count(r => r.role == role);
                if (n == 0) continue;
                string name = role == "Freezer" ? "CC_Base_Freezer".Translate().Resolve() : RoomUtil.RoleNamed(role)?.label ?? role.ToLower();
                legend.Add((color, n > 1 ? n + " " + Find.ActiveLanguageWorker.Pluralize(name, n) : name));
            }
            // The little map's own colours for walls and doors (a picture's colours, as the region map's land is).
            legend.Add((BaseMiniMap.WallColor, "CC_Base_Legend_Walls".Translate().Resolve()));
            legend.Add((BaseMiniMap.DoorColor, "CC_Base_Legend_Doors".Translate().Resolve()));
            y += BaseMiniMap.Legend(x + 4f, y, w - 8f, legend) + 6f;

            // In numbers.
            Text.Font = GameFont.Tiny;
            GUI.color = UiKit.Faint;
            string facts = "CC_Base_Facts".Translate(roles.Count, findings.Count, findings.Count(f => f.fix != null), findings.Count(f => f.template != null)).Resolve();
            float fh = Text.CalcHeight(facts, w - 8f);
            Widgets.Label(new Rect(x + 4f, y, w - 8f, fh), facts);
            GUI.color = UiKit.Ink;
            Text.Font = GameFont.Small;
            return y + fh + 4f;
        }

        // ---------------- Right: the findings ----------------

        private static readonly Folds folds = new Folds("aside");

        private static float DrawFindings(Map map, List<BaseFinding> findings, float x, float y, float w)
        {
            if (findings.Count == 0) y += UiKit.EmptyState(x, y, w, Icons.Base, "CC_Base_AllGood".Translate(), 140f);
            foreach (var group in Groups)
            {
                var part = findings.Where(f => f.group == group).ToList();
                if (part.Count == 0) continue;
                y += UiKit.SectionTitle(x, y, w, ("CC_Base_Group_" + group).Translate(), part.Count) + 4f;
                foreach (var f in part) y += DrawFinding(f, x, y, w, map) + UiKit.CardGap;
                y += UiKit.SectionGap - UiKit.CardGap;
            }
            // The fixes done lately, with Undo and whether each worked; and the findings set aside, to bring back.
            y += AheadPanel.Done(x, y, w, map, BaseReview.Source) + 4f;
            y += AheadPanel.Aside(x, y, w, BaseReview.Source, folds) + 8f;
            return y;
        }

        /// <summary>A finding's buttons: Show, Plan the room, its fix as a step (Undo, a check later), Not this; each its whole label wide.</summary>
        private static List<(string label, string tip, System.Action act)> ButtonsOf(BaseFinding f, Map map)
        {
            var list = new List<(string, string, System.Action)>();
            if (f.look.IsValid) list.Add(("CC_Show".Translate().Resolve(), "CC_Plan_ShowTip".Translate().Resolve(), () => MainTabWindow_Council.Jump(new LookTargets(f.look, map))));
            if (f.fix != null && !f.fixLabel.NullOrEmpty())
            {
                string tip = (f.fixTip.NullOrEmpty() ? f.fixLabel : f.fixTip) + (f.things != null ? "\n\n" + "CC_Base_FixTip".Translate(f.things.Count).Resolve() : "")
                             + "\n\n" + "CC_Ahead_DoItTip".Translate().Resolve();
                list.Add((f.fixLabel, tip, () =>
                {
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    BaseReview.Apply(map, f);
                }));
            }
            if (f.template != null)
                list.Add(("CC_PlanIt".Translate().Resolve(), "CC_Plan_PlanItTip".Translate(f.template.label).Resolve(), () => MainTabWindow_Council.StartPlanning(f.template, true)));
            string key = BaseReview.KeyOf(f), title = f.title;
            list.Add(("CC_Ahead_NotThis".Translate().Resolve(), "CC_Ahead_NotThisTip".Translate().Resolve(), () =>
            {
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                SuggestTracker.Instance?.Decline(key, title, 0f, map);
            }));
            return list;
        }

        private static float DrawFinding(BaseFinding f, float x, float y, float width, Map map)
        {
            var level = f.Level;
            var color = UiKit.LevelColor(level);
            var buttonsList = ButtonsOf(f, map);
            float buttonsW = AheadPanel.ButtonsWidth(buttonsList.Select(b => b.label), ButtonsW - 12f) + 12f;
            Text.Font = GameFont.Small;
            float textW = Mathf.Max(80f, width - LeftW - buttonsW - 12f);
            float titleH = Text.CalcHeight(f.title ?? "", textW);
            float detailH = f.detail.NullOrEmpty() ? 0f : Text.CalcHeight(f.detail, textW);
            int buttons = buttonsList.Count;
            float h = Mathf.Max(48f, titleH + detailH + 18f, buttons * (UiKit.RowStandard + 4f) + 10f);
            var row = new Rect(x, y, width, h);
            UiKit.Card(row, color);
            bool hovered = Mouse.IsOver(row);
            if (hovered) BaseOverlay.Hover(f.key);
            if (hovered || BaseOverlay.HoveredKey == f.key) Widgets.DrawBox(row, 1);
            UiKit.Pill(x + 10f, y + 8f, ("CC_Level_" + level).Translate(), color, level >= AdviceLevel.High ? Icons.Warning : null, null, LeftW - 14f);

            float textX = x + LeftW;
            // The level's colour on its tag and the card's edge, beside the words; the title in ink.
            GUI.color = UiKit.Ink;
            Widgets.Label(new Rect(textX, y + 8f, textW, titleH), f.title);
            if (detailH > 0f)
            {
                GUI.color = UiKit.Faint;
                Widgets.Label(new Rect(textX, y + 8f + titleH, textW, detailH), f.detail);
                GUI.color = UiKit.Ink;
            }

            float bx = x + width - buttonsW + 4f, bw = buttonsW - 12f, by = y + 6f;
            foreach (var (label, tip, act) in buttonsList)
            {
                if (UiKit.ActButton(new Rect(bx, by, bw, UiKit.RowStandard), label, null, tip)) act();
                by += UiKit.RowStandard + 4f;
            }
            return h;
        }
    }
}
