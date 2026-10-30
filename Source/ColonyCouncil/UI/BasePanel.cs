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
            if (Widgets.ButtonText(new Rect(rect.xMax - 160f, rect.y + 6f, 160f, 30f), "CC_Base_LookAgain".Translate()))
            {
                BaseReview.For(map, true);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            bool show = BaseOverlay.Show;
            var showRect = new Rect(rect.xMax - 320f, rect.y + 8f, 150f, 24f);
            Widgets.CheckboxLabeled(showRect, "CC_Base_ShowOnMap".Translate(), ref show);
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
                GUI.color = Color.white;
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
            legend.Add((new Color(0.78f, 0.78f, 0.78f), "CC_Base_Legend_Walls".Translate().Resolve()));
            legend.Add((new Color(0.45f, 0.75f, 1f), "CC_Base_Legend_Doors".Translate().Resolve()));
            y += BaseMiniMap.Legend(x + 4f, y, w - 8f, legend) + 6f;

            // In numbers.
            Text.Font = GameFont.Tiny;
            GUI.color = UiKit.Faint;
            string facts = "CC_Base_Facts".Translate(roles.Count, findings.Count, findings.Count(f => f.fix != null), findings.Count(f => f.template != null)).Resolve();
            float fh = Text.CalcHeight(facts, w - 8f);
            Widgets.Label(new Rect(x + 4f, y, w - 8f, fh), facts);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            return y + fh + 4f;
        }

        // ---------------- Right: the findings ----------------

        private static float DrawFindings(Map map, List<BaseFinding> findings, float x, float y, float w)
        {
            if (findings.Count == 0) y += UiKit.EmptyState(x, y, w, Icons.Base, "CC_Base_AllGood".Translate(), 140f);
            foreach (var group in Groups)
            {
                var part = findings.Where(f => f.group == group).ToList();
                if (part.Count == 0) continue;
                y += UiKit.SectionTitle(x, y, w, ("CC_Base_Group_" + group).Translate(), null, UiKit.HeaderColor) + 4f;
                foreach (var f in part) y += DrawFinding(f, x, y, w, map) + 6f;
                y += 8f;
            }
            return y;
        }

        private static float DrawFinding(BaseFinding f, float x, float y, float width, Map map)
        {
            var level = f.Level;
            var color = LevelColor(level);
            Text.Font = GameFont.Small;
            float textW = Mathf.Max(80f, width - LeftW - ButtonsW - 12f);
            float titleH = Text.CalcHeight(f.title ?? "", textW);
            float detailH = f.detail.NullOrEmpty() ? 0f : Text.CalcHeight(f.detail, textW);
            int buttons = (f.look.IsValid ? 1 : 0) + (f.fix != null ? 1 : 0) + (f.template != null ? 1 : 0);
            float h = Mathf.Max(48f, titleH + detailH + 18f, buttons * 30f + 10f);
            var row = new Rect(x, y, width, h);
            UiKit.Card(row, color);
            bool hovered = Mouse.IsOver(row);
            if (hovered) BaseOverlay.Hover(f.key);
            if (hovered || BaseOverlay.HoveredKey == f.key) Widgets.DrawBox(row, 1);
            UiKit.Pill(x + 10f, y + 8f, ("CC_Level_" + level).Translate(), color, level >= AdviceLevel.High ? Icons.Warning : null, null, LeftW - 14f);

            float textX = x + LeftW;
            GUI.color = color;
            Widgets.Label(new Rect(textX, y + 8f, textW, titleH), f.title);
            GUI.color = Color.white;
            if (detailH > 0f)
            {
                GUI.color = UiKit.Faint;
                Widgets.Label(new Rect(textX, y + 8f + titleH, textW, detailH), f.detail);
                GUI.color = Color.white;
            }

            float bx = x + width - ButtonsW + 4f, bw = ButtonsW - 12f, by = y + 6f;
            if (f.look.IsValid)
            {
                if (Widgets.ButtonText(new Rect(bx, by, bw, 26f), "CC_Show".Translate())) MainTabWindow_Council.Jump(new LookTargets(f.look, map));
                by += 30f;
            }
            if (f.template != null && Widgets.ButtonText(new Rect(bx, by, bw, 26f), "CC_PlanIt".Translate()))
            {
                MainTabWindow_Council.StartPlanning(f.template, true);
            }
            if (f.template != null) by += 30f;
            if (f.fix != null)
            {
                var fixRect = new Rect(bx, by, bw, 26f);
                if (Widgets.ButtonText(fixRect, UiKit.Fit(f.fixLabel, bw - 8f)))
                {
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    try
                    {
                        f.fix();
                    }
                    catch (System.Exception e)
                    {
                        CouncilLog.ErrorOnce("base review fix " + f.key, e);
                    }
                }
                if (f.things != null) TooltipHandler.TipRegion(fixRect, "CC_Base_FixTip".Translate(f.things.Count));
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
