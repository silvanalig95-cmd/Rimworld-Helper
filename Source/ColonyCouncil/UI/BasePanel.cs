using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Colony, then Base: the base looked over (Building/BaseReview.cs). What the review found, most pressing first, in
    /// parts: the rooms, the walks, the doors, the walls, the works. Each with where to look and, where the council can
    /// do it at a click, the order to give.
    /// </summary>
    public static class BasePanel
    {
        private static Vector2 scroll;
        private static float height = 600f;

        private const float ButtonsW = 150f, LeftW = 110f;
        private static readonly string[] Groups = { "rooms", "walks", "stores", "doors", "walls", "works" };

        public static void Draw(Rect rect, Map map)
        {
            if (map == null || !map.IsPlayerHome)
            {
                UiKit.EmptyState(rect, Icons.Colony, "CC_NoHomeMap".Translate());
                return;
            }
            var findings = BaseReview.For(map);
            int pressing = findings.Count(f => f.Level >= AdviceLevel.Medium);
            string stat = findings.Count > 0 ? "CC_Base_Stat".Translate(findings.Count, pressing).Resolve() : null;
            float top = UiKit.PageHeader(rect, Icons.Base, "CC_Base_Header".Translate(), "CC_Base_Sub".Translate(), stat, UiKit.Gold, 170f, "CC_Base_HeaderTip".Translate());
            if (Widgets.ButtonText(new Rect(rect.xMax - 160f, rect.y + 6f, 160f, 30f), "CC_Base_LookAgain".Translate()))
            {
                BaseReview.For(map, true);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 18f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;
            if (findings.Count == 0) y += UiKit.EmptyState(0f, y, view.width, Icons.Base, "CC_Base_AllGood".Translate(), 140f);
            foreach (var group in Groups)
            {
                var part = findings.Where(f => f.group == group).ToList();
                if (part.Count == 0) continue;
                y += UiKit.SectionTitle(0f, y, view.width, ("CC_Base_Group_" + group).Translate(), null, UiKit.HeaderColor) + 4f;
                foreach (var f in part) y += DrawFinding(f, 0f, y, view.width, map) + 6f;
                y += 8f;
            }
            height = Mathf.Max(y + 10f, 100f);
            Widgets.EndScrollView();
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
