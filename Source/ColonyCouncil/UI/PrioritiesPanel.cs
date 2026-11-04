using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// Planning → Plan, Priorities: what the advisor thinks the colony needs, most urgent first, as cards (the second mode
    /// of the Plan view, beside the plan its cards are acted on in). Each card says why in
    /// the colony's numbers and offers what helps: Show, the room planner, its own action as a step of the shared
    /// suggestion core (kept with Undo and checked later), the page that deals with it, the Almanac, and "Not this" (set
    /// aside until brought back, here and on the Plan page alike). Under the cards: the steps done lately with Undo and
    /// whether each worked, and what was set aside. Every button shows its whole label and says what it does.
    /// </summary>
    public static class PrioritiesPanel
    {
        private static readonly Folds folds = new Folds("aside");
        private static Vector2 scroll;
        private static float height = 1000f;

        private const float LeftW = 124f;
        private const float LeastButtonsW = 140f;
        private const float ThingW = 34f;

        private static CouncilSettings Settings => ColonyCouncilMod.Settings;

        /// <summary>One button of a card: its whole label, what it does on the map, and the doing.</summary>
        private class Button
        {
            public string label, tip;
            public Action act;
        }

        /// <summary>A card's text, buttons and measurements, kept until the width changes or the advisor refreshes.</summary>
        private class Layout
        {
            public float width = -1f, at, titleH, detailH, tipH, height, buttonsW;
            public string tip;
            public List<Button> buttons = new List<Button>();
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
            float top = UiKit.PageHeader(rect, Icons.Priorities, "CC_Priorities_Header".Translate(), era, stat, UiKit.LevelColor(AdviceLevel.High), 160f);
            if (UiKit.ActButton(new Rect(rect.xMax - 150f, rect.y + 6f, 150f, UiKit.RowStandard), "CC_Refresh".Translate(), null, "CC_Same_RefreshTip".Translate()))
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
                y += layout.height + UiKit.CardGap;
            }
            // The steps done from these cards lately, with Undo and whether each worked; and what was set aside.
            y += AheadPanel.Done(0f, y + 4f, view.width, map, AdvisorSteps.Source) + 4f;
            y += AheadPanel.Aside(0f, y, view.width, AdvisorSteps.Source, folds) + 8f;
            height = Mathf.Max(y + 10f, 100f);
            Widgets.EndScrollView();
        }

        /// <summary>What a card offers, the main thing first: each with its whole label and what it does on the map.</summary>
        private static List<Button> ButtonsOf(AdvisorSignal s, Map map)
        {
            var list = new List<Button>();
            if (s.look != null && s.look.IsValid)
                list.Add(new Button { label = "CC_Show".Translate(), tip = "CC_Plan_ShowTip".Translate(), act = () => MainTabWindow_Council.Jump(s.look) });
            // A room building ahead reckons: its step placed from here as from the Plan page (the same step, the same Undo).
            if (s.buildKind != null)
            {
                var step = BuildAhead.Open(map).FirstOrDefault(x => x.key == GrowthMath.RoomKey(s.buildKind));
                if (step?.apply != null)
                    list.Add(new Button { label = step.doLabel ?? "CC_Ahead_DoIt".Translate().Resolve(), tip = step.title + "\n\n" + AheadPanel.DoTip(step), act = () => AheadPanel.DoStep(step) });
            }
            if (s.template != null && map != null && s.template.UnavailableReason(map) == null)
                list.Add(new Button
                {
                    label = (s.upgrade ? "CC_UpgradeIt" : "CC_PlanIt").Translate(), tip = (s.upgrade ? "CC_Plan_UpgradeItTip" : "CC_Plan_PlanItTip").Translate(s.template.label),
                    act = () =>
                    {
                        if (s.upgrade) Designator_PlanRoom.Begin(s.template, false, true);
                        else MainTabWindow_Council.StartPlanning(s.template, true);
                    }
                });
            // Something the advice can do now: orders on the map go through the shared core (Undo, a check later); opening a letter, a quest or a tab just opens it.
            if (s.act != null && !s.actLabel.NullOrEmpty())
            {
                if (AdvisorSteps.ActIsStep(s))
                {
                    var step = AdvisorSteps.StepOf(s, map);
                    list.Add(new Button { label = s.actLabel, tip = AheadPanel.DoTip(step), act = () => AheadPanel.DoStep(step) });
                }
                else
                    list.Add(new Button
                    {
                        label = s.actLabel, tip = s.actTip.NullOrEmpty() ? s.actLabel : s.actTip, act = () =>
                        {
                            try
                            {
                                s.act();
                            }
                            catch (Exception e)
                            {
                                CouncilLog.ErrorOnce("advisor action " + s.def?.defName, e);
                            }
                        }
                    });
            }
            if (MainTabWindow_Council.IsPage(s.page))
            {
                string page = MainTabWindow_Council.TabName(s.page);
                list.Add(new Button { label = "CC_Mood_GoTo".Translate(page).Resolve(), tip = "CC_Plan_GoToTip".Translate(page), act = () => MainTabWindow_Council.OpenTab(s.page) });
            }
            var article = s.article ?? s.def.article;
            if (article != null)
                list.Add(new Button { label = "CC_ReadMore".Translate(), tip = "CC_Same_ReadMoreTip".Translate(article.label.CapitalizeFirst()), act = () => MainTabWindow_Council.OpenAlmanac(article) });
            string key = AdvisorSteps.KeyOf(s), title = s.title;
            list.Add(new Button { label = "CC_Ahead_NotThis".Translate(), tip = "CC_Ahead_NotThisTip".Translate(), act = () => SuggestTracker.Instance?.Decline(key, title, 0f, map) });
            return list;
        }

        private static Layout LayoutOf(AdvisorSignal s, float width, Map map)
        {
            float now = Time.realtimeSinceStartup;
            if (layouts.TryGetValue(s, out var l) && Mathf.Abs(l.width - width) < 0.5f && now - l.at < 2f) return l;
            l = new Layout { width = width, at = now, tip = s.Tip };
            l.buttons = ButtonsOf(s, map);
            l.buttonsW = AheadPanel.ButtonsWidth(l.buttons.Select(b => b.label), LeastButtonsW - 12f) + 12f;
            float textW = TextWidth(s, width, l.buttonsW);
            Text.Font = GameFont.Small;
            l.titleH = Text.CalcHeight(s.title ?? "", textW);
            l.detailH = Text.CalcHeight(s.detail ?? "", textW);
            Text.Font = GameFont.Tiny;
            l.tipH = l.tip.NullOrEmpty() ? 0f : Text.CalcHeight("CC_WhyItMatters".Translate() + ": " + l.tip, textW);
            Text.Font = GameFont.Small;
            // Tall enough for the text, the level badge, and the column of buttons.
            l.height = Mathf.Max(68f, l.titleH + l.detailH + l.tipH + 16f, l.buttons.Count * (UiKit.RowStandard + 4f) + 10f);
            layouts[s] = l;
            return l;
        }

        private static float TextWidth(AdvisorSignal s, float width, float buttonsW) => Mathf.Max(60f, width - LeftW - buttonsW - (s.suggestThing != null ? ThingW : 0f));

        private static void DrawSignal(AdvisorSignal s, Layout l, float x, float y, float width, Map map)
        {
            var color = UiKit.LevelColor(s.Level);
            var row = new Rect(x, y, width, l.height);
            UiKit.Card(row, color);

            // The level as a pill, the kind of need under it.
            string level = ("CC_Level_" + s.Level).Translate();
            UiKit.Pill(x + 12f, y + 10f, level, color, s.Level >= AdviceLevel.High ? Icons.Warning : null, null, LeftW - 18f);
            UiKit.LabelFit(new Rect(x + 12f, y + 32f, LeftW - 16f, 18f), ("CC_Cat_" + s.def.category).Translate(), UiKit.Faint, GameFont.Tiny);

            float textX = x + LeftW;
            float textW = TextWidth(s, width, l.buttonsW);
            float cy = y + 8f;
            // The level's colour on its tag and the card's edge, beside the words; the title in ink.
            GUI.color = UiKit.Ink;
            Widgets.Label(new Rect(textX, cy, textW, l.titleH), s.title);
            cy += l.titleH;
            Widgets.Label(new Rect(textX, cy, textW, l.detailH), s.detail ?? "");
            cy += l.detailH + 2f;
            if (!l.tip.NullOrEmpty())
            {
                Text.Font = GameFont.Tiny;
                GUI.color = UiKit.Faint;
                Widgets.Label(new Rect(textX, cy, textW, l.tipH), "CC_WhyItMatters".Translate() + ": " + l.tip);
                GUI.color = UiKit.Ink;
                Text.Font = GameFont.Small;
            }

            if (s.suggestThing != null)
            {
                var iconRect = new Rect(textX + textW + 4f, y + 8f, 26f, 26f);
                Widgets.DrawBoxSolid(iconRect, UiKit.WithAlpha(UiKit.Shade, 0.2f));
                Widgets.ThingIcon(iconRect.ContractedBy(1f), s.suggestThing);
                TooltipHandler.TipRegion(iconRect, s.suggestThing.LabelCap);
            }

            // The buttons, each its whole label wide, each saying what it does.
            float bx = x + width - l.buttonsW + 4f;
            float bw = l.buttonsW - 12f;
            float by = y + 6f;
            foreach (var b in l.buttons)
            {
                var r = new Rect(bx, by, bw, UiKit.RowStandard);
                if (UiKit.ActButton(r, b.label, null, b.tip))
                {
                    b.act?.Invoke();
                    layouts.Clear();
                }
                by += UiKit.RowStandard + 4f;
            }
        }
    }
}
