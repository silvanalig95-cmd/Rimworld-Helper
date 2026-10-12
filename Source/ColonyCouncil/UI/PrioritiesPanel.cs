using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Colony → Priorities: what the advisor thinks the colony needs, most urgent first.</summary>
    public static class PrioritiesPanel
    {
        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color GoodColor = new Color(0.55f, 0.9f, 0.55f);
        private static readonly Color HighColor = new Color(0.95f, 0.6f, 0.2f);
        private static readonly Color MediumColor = new Color(0.9f, 0.85f, 0.35f);
        private static readonly Color IdeaColor = new Color(0.45f, 0.7f, 0.95f);
        private static readonly Color CriticalColor = new Color(0.9f, 0.3f, 0.25f);
        private static readonly Color LowColor = new Color(0.65f, 0.65f, 0.65f);
        private static Vector2 scroll;
        private static float lastHeight = 1000f;

        private static CouncilSettings Settings => ColonyCouncilMod.Settings;

        private static float BeginScroll(Rect outRect)
        {
            var view = new Rect(0f, 0f, outRect.width - 18f, lastHeight);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            return view.width;
        }

        private static void EndScroll(float height)
        {
            lastHeight = Mathf.Max(height + 10f, 100f);
            Widgets.EndScrollView();
        }

        private static float WrappedLabel(float x, float y, float width, string text, Color? color = null, GameFont font = GameFont.Small) =>
            MainTabWindow_Council.WrappedLabel(x, y, width, text, color, font);

        public static void Draw(Rect rect, Map map)
        {
            if (!Settings.enableAdvisor)
            {
                Widgets.Label(rect, "CC_AdvisorDisabled".Translate());
                return;
            }
            if (map == null || !map.IsPlayerHome)
            {
                Widgets.Label(rect, "CC_NoHomeMap".Translate());
                return;
            }
            var mc = CouncilMapComponent.For(map);
            var top = new Rect(rect.x, rect.y, rect.width, 48f);
            string era = "CC_Era".Translate(Avail.EffectiveTech.ToStringHuman().CapitalizeFirst(),
                (Avail.HasElectricity ? "CC_Yes" : "CC_No").Translate());
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(top.x, top.y, top.width - 160f, 32f), "CC_Priorities_Header".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Faint;
            Widgets.Label(new Rect(top.x, top.y + 28f, top.width - 160f, 22f), era);
            GUI.color = Color.white;
            if (Widgets.ButtonText(new Rect(top.xMax - 150f, top.y + 4f, 150f, 30f), "CC_Refresh".Translate())) mc?.Refresh(true);

            var signals = mc?.Signals ?? new List<AdvisorSignal>();
            var outRect = new Rect(rect.x, rect.y + 56f, rect.width, rect.height - 56f);
            float width = BeginScroll(outRect);
            float y = 0f;
            if (signals.Count == 0)
            {
                y += WrappedLabel(0f, y, width, "CC_Priorities_AllGood".Translate(), GoodColor);
            }
            foreach (var s in signals)
            {
                y += DrawSignal(s, 0f, y, width, map) + 8f;
            }
            EndScroll(y);
        }

        private static float DrawSignal(AdvisorSignal s, float x, float y, float width, Map map)
        {
            float textX = x + 120f;
            float textW = width - 120f - 140f;
            float h = 0f;
            Text.Font = GameFont.Small;
            float titleH = Text.CalcHeight(s.title, textW);
            float detailH = Text.CalcHeight(s.detail ?? "", textW);
            string tip = s.Tip;
            Text.Font = GameFont.Tiny;
            float tipH = tip.NullOrEmpty() ? 0f : Text.CalcHeight("CC_WhyItMatters".Translate() + ": " + tip, textW);
            Text.Font = GameFont.Small;
            h = Mathf.Max(64f, titleH + detailH + tipH + 14f);
            var row = new Rect(x, y, width, h);
            Widgets.DrawLightHighlight(row);

            var badge = new Rect(x + 6f, y + 8f, 104f, 24f);
            Widgets.DrawBoxSolid(badge, LevelColor(s.Level) * new Color(1f, 1f, 1f, 0.35f));
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(badge, ("CC_Level_" + s.Level).Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Faint;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(x + 6f, y + 36f, 104f, 20f), ("CC_Cat_" + s.def.category).Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            float cy = y + 6f;
            GUI.color = LevelColor(s.Level);
            Widgets.Label(new Rect(textX, cy, textW, titleH), s.title);
            GUI.color = Color.white;
            cy += titleH;
            Widgets.Label(new Rect(textX, cy, textW, detailH), s.detail ?? "");
            cy += detailH + 2f;
            if (!tip.NullOrEmpty())
            {
                Text.Font = GameFont.Tiny;
                GUI.color = Faint;
                Widgets.Label(new Rect(textX, cy, textW, tipH), "CC_WhyItMatters".Translate() + ": " + tip);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }

            float bx = x + width - 132f;
            float by = y + 6f;
            if (s.look != null && s.look.IsValid)
            {
                if (Widgets.ButtonText(new Rect(bx, by, 128f, 26f), "CC_Show".Translate())) MainTabWindow_Council.Jump(s.look);
                by += 30f;
            }
            if (s.template != null && map != null)
            {
                string reason = s.template.UnavailableReason(map);
                if (reason == null)
                {
                    if (Widgets.ButtonText(new Rect(bx, by, 128f, 26f), "CC_PlanIt".Translate())) MainTabWindow_Council.StartPlanning(s.template, true);
                }
                by += 30f;
            }
            var article = s.article ?? s.def.article;
            if (article != null)
            {
                if (Widgets.ButtonText(new Rect(bx, by, 128f, 26f), "CC_ReadMore".Translate())) MainTabWindow_Council.OpenAlmanac(article);
                by += 30f;
            }
            if (s.suggestThing != null)
            {
                var iconRect = new Rect(bx + 100f, y + h - 30f, 26f, 26f);
                Widgets.ThingIcon(iconRect, s.suggestThing);
                TooltipHandler.TipRegion(iconRect, s.suggestThing.LabelCap);
            }
            return h;
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
