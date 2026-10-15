using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Colony → Priorities: what the advisor thinks the colony needs, most urgent first, as cards.</summary>
    public static class PrioritiesPanel
    {
        private static readonly Color CriticalColor = new Color(0.9f, 0.3f, 0.25f);
        private static readonly Color HighColor = new Color(0.95f, 0.6f, 0.2f);
        private static readonly Color MediumColor = new Color(0.9f, 0.85f, 0.35f);
        private static readonly Color LowColor = new Color(0.65f, 0.65f, 0.65f);
        private static readonly Color IdeaColor = new Color(0.45f, 0.7f, 0.95f);
        private static Vector2 scroll;
        private static float height = 1000f;

        private const float LeftW = 124f;
        private const float ButtonsW = 140f;
        private const float ThingW = 34f;

        private static CouncilSettings Settings => ColonyCouncilMod.Settings;

        /// <summary>A card's text and its measurements, kept until the width changes or the advisor refreshes.</summary>
        private class Layout
        {
            public float width = -1f, at, titleH, detailH, tipH, height;
            public string tip;
            public int buttons;
        }

        private static readonly Dictionary<AdvisorSignal, Layout> layouts = new Dictionary<AdvisorSignal, Layout>();

        public static void Draw(Rect rect, Map map)
        {
            if (!Settings.enableAdvisor)
            {
                UiKit.EmptyState(rect, Icons.Priorities, "CC_AdvisorDisabled".Translate());
                return;
            }
            if (map == null || !map.IsPlayerHome)
            {
                UiKit.EmptyState(rect, Icons.Colony, "CC_NoHomeMap".Translate());
                return;
            }
            var mc = CouncilMapComponent.For(map);
            var signals = mc?.Signals ?? new List<AdvisorSignal>();
            string era = UiCache.Get(map, 50, () => "CC_Era".Translate(Avail.EffectiveTech.ToStringHuman().CapitalizeFirst(),
                (Avail.HasElectricity ? "CC_Yes" : "CC_No").Translate()).Resolve());
            int urgent = 0;
            for (int i = 0; i < signals.Count; i++)
                if (signals[i].Level >= AdviceLevel.High) urgent++;
            string stat = urgent > 0 ? "CC_UI_Stat_Urgent".Translate(urgent).Resolve() : null;
            float top = UiKit.PageHeader(rect, Icons.Priorities, "CC_Priorities_Header".Translate(), era, stat, HighColor, 160f);
            if (Widgets.ButtonText(new Rect(rect.xMax - 150f, rect.y + 6f, 150f, 30f), "CC_Refresh".Translate()))
            {
                mc?.Refresh(true);
                layouts.Clear();
            }

            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 18f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;
            if (signals.Count == 0) y += UiKit.EmptyState(0f, y, view.width, Icons.Priorities, "CC_Priorities_AllGood".Translate(), 140f);
            if (layouts.Count > 200) layouts.Clear();
            foreach (var s in signals)
            {
                var layout = LayoutOf(s, view.width, map);
                // Only cards on screen are drawn.
                if (y + layout.height >= scroll.y && y <= scroll.y + outRect.height) DrawSignal(s, layout, 0f, y, view.width, map);
                y += layout.height + 8f;
            }
            height = Mathf.Max(y + 10f, 100f);
            Widgets.EndScrollView();
        }

        private static Layout LayoutOf(AdvisorSignal s, float width, Map map)
        {
            float now = Time.realtimeSinceStartup;
            if (layouts.TryGetValue(s, out var l) && Mathf.Abs(l.width - width) < 0.5f && now - l.at < 2f) return l;
            l = new Layout { width = width, at = now, tip = s.Tip };
            float textW = TextWidth(s, width);
            Text.Font = GameFont.Small;
            l.titleH = Text.CalcHeight(s.title ?? "", textW);
            l.detailH = Text.CalcHeight(s.detail ?? "", textW);
            Text.Font = GameFont.Tiny;
            l.tipH = l.tip.NullOrEmpty() ? 0f : Text.CalcHeight("CC_WhyItMatters".Translate() + ": " + l.tip, textW);
            Text.Font = GameFont.Small;
            if (s.look != null && s.look.IsValid) l.buttons++;
            if (s.template != null && map != null && s.template.UnavailableReason(map) == null) l.buttons++;
            if ((s.article ?? s.def.article) != null) l.buttons++;
            // Tall enough for the text, the level badge, and the column of buttons.
            l.height = Mathf.Max(68f, l.titleH + l.detailH + l.tipH + 16f, l.buttons * 30f + 10f);
            layouts[s] = l;
            return l;
        }

        private static float TextWidth(AdvisorSignal s, float width) => Mathf.Max(60f, width - LeftW - ButtonsW - (s.suggestThing != null ? ThingW : 0f));

        private static void DrawSignal(AdvisorSignal s, Layout l, float x, float y, float width, Map map)
        {
            var color = LevelColor(s.Level);
            var row = new Rect(x, y, width, l.height);
            UiKit.Card(row, color);

            // The level as a pill, the kind of need under it.
            string level = ("CC_Level_" + s.Level).Translate();
            UiKit.Pill(x + 12f, y + 10f, level, color, s.Level >= AdviceLevel.High ? Icons.Warning : null, null, LeftW - 18f);
            UiKit.LabelFit(new Rect(x + 12f, y + 32f, LeftW - 16f, 18f), ("CC_Cat_" + s.def.category).Translate(), UiKit.Faint, GameFont.Tiny);

            float textX = x + LeftW;
            float textW = TextWidth(s, width);
            float cy = y + 8f;
            GUI.color = color;
            Widgets.Label(new Rect(textX, cy, textW, l.titleH), s.title);
            GUI.color = Color.white;
            cy += l.titleH;
            Widgets.Label(new Rect(textX, cy, textW, l.detailH), s.detail ?? "");
            cy += l.detailH + 2f;
            if (!l.tip.NullOrEmpty())
            {
                Text.Font = GameFont.Tiny;
                GUI.color = UiKit.Faint;
                Widgets.Label(new Rect(textX, cy, textW, l.tipH), "CC_WhyItMatters".Translate() + ": " + l.tip);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }

            if (s.suggestThing != null)
            {
                var iconRect = new Rect(textX + textW + 4f, y + 8f, 26f, 26f);
                Widgets.DrawBoxSolid(iconRect, new Color(0f, 0f, 0f, 0.2f));
                Widgets.ThingIcon(iconRect.ContractedBy(1f), s.suggestThing);
                TooltipHandler.TipRegion(iconRect, s.suggestThing.LabelCap);
            }

            float bx = x + width - ButtonsW + 4f;
            float bw = ButtonsW - 12f;
            float by = y + 6f;
            if (s.look != null && s.look.IsValid)
            {
                if (Widgets.ButtonText(new Rect(bx, by, bw, 26f), "CC_Show".Translate())) MainTabWindow_Council.Jump(s.look);
                by += 30f;
            }
            if (s.template != null && map != null && s.template.UnavailableReason(map) == null)
            {
                if (Widgets.ButtonText(new Rect(bx, by, bw, 26f), (s.upgrade ? "CC_UpgradeIt" : "CC_PlanIt").Translate()))
                {
                    if (s.upgrade) Designator_PlanRoom.Begin(s.template, false, true);
                    else MainTabWindow_Council.StartPlanning(s.template, true);
                }
                by += 30f;
            }
            var article = s.article ?? s.def.article;
            if (article != null && Widgets.ButtonText(new Rect(bx, by, bw, 26f), "CC_ReadMore".Translate())) MainTabWindow_Council.OpenAlmanac(article);
        }

        private static Color LevelColor(AdviceLevel level)
        {
            switch (level)
            {
                case AdviceLevel.Critical: return CriticalColor;
                case AdviceLevel.High: return HighColor;
                case AdviceLevel.Medium: return MediumColor;
                case AdviceLevel.Low: return LowColor;
                default: return IdeaColor;
            }
        }
    }
}
