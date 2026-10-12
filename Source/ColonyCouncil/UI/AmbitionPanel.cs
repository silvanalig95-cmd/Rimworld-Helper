using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Council → Ambition: the colony's current ambition and its goals, the next meeting, and past ambitions.</summary>
    public static class AmbitionPanel
    {
        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color GoodColor = new Color(0.55f, 0.9f, 0.55f);
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

        public static void Draw(Rect rect, CouncilGameComponent comp, Map map)
        {
            if (!Settings.enableAmbitions)
            {
                Widgets.Label(rect, "CC_AmbitionsDisabled".Translate());
                return;
            }
            var tracker = comp.ambitions;
            float width = BeginScroll(rect);
            float y = 0f;
            var a = tracker.active;
            Text.Font = GameFont.Medium;
            if (a != null)
            {
                Widgets.Label(new Rect(0f, y, width, 32f), "CC_Ambition_Current".Translate(a.def.LabelCap.ToString()));
                Text.Font = GameFont.Small;
                y += 34f;
                y += WrappedLabel(0f, y, width, a.def.description, Faint) + 6f;
                string meta = "CC_Ambition_Meta".Translate(a.proposer?.LabelShort ?? "?", TextUtil.DaysString(Mathf.Max(0, a.deadlineTick - Find.TickManager.TicksGame)));
                y += WrappedLabel(0f, y, width, meta) + 6f;
                float total = UiCache.Get(a, -1, () => a.Progress);
                Widgets.FillableBar(new Rect(0f, y, Mathf.Min(400f, width), 18f), total);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(0f, y, Mathf.Min(400f, width), 18f), total.ToStringPercent());
                Text.Anchor = TextAnchor.UpperLeft;
                y += 26f;
                for (int i = 0; i < a.def.goals.Count; i++)
                {
                    var g = a.def.goals[i];
                    int gi = i;
                    float p = UiCache.Get(a, gi * 2, () => g.Progress(a.map, a.Baseline(gi)));
                    bool done = p >= 0.999f;
                    Widgets.CheckboxDraw(6f, y, done, false, 22f);
                    string desc = UiCache.Get(a, gi * 2 + 1, () => g.Describe(a.map, a.Baseline(gi)));
                    y += Mathf.Max(24f, WrappedLabel(34f, y + 1f, width - 34f, desc, done ? GoodColor : Color.white)) + 4f;
                }
                y += 10f;
                if (a.def.template != null && map != null && a.def.template.UnavailableReason(map) == null)
                {
                    if (Widgets.ButtonText(new Rect(0f, y, 200f, 30f), "CC_PlanIt".Translate())) MainTabWindow_Council.StartPlanning(a.def.template, true);
                }
                if (Widgets.ButtonText(new Rect(210f, y, 200f, 30f), "CC_Ambition_Abandon".Translate()))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CC_Ambition_AbandonConfirm".Translate(a.def.label), () => tracker.Abandon(), true));
                }
                y += 40f;
            }
            else
            {
                Widgets.Label(new Rect(0f, y, width, 32f), "CC_Ambition_None".Translate());
                Text.Font = GameFont.Small;
                y += 34f;
                int ticks = tracker.TicksUntilMeeting;
                var m = tracker.meeting;
                string status;
                if (m != null && m.Started)
                    status = "CC_Meeting_InSession".Translate(m.venue ?? "?", m.attendees.Count, TextUtil.JoinAnd(m.pitches.Select(p => p.def.label).ToList())).Resolve();
                else if (m != null)
                    status = "CC_Meeting_Called".Translate(TextUtil.JoinAnd(m.pitches.Select(p => p.def.label).ToList())).Resolve();
                else
                    status = ticks >= 0 ? "CC_Ambition_NextMeeting".Translate(TextUtil.DaysString(ticks)).Resolve() : "CC_Ambition_SoonMeeting".Translate().Resolve();
                y += WrappedLabel(0f, y, width, status, Faint) + 8f;
                bool can = tracker.CanCallMeetingNow(out string reason);
                var btn = new Rect(0f, y, 240f, 32f);
                if (Widgets.ButtonText(btn, "CC_Ambition_CallMeeting".Translate(), true, true, can) && can) tracker.HoldMeeting(comp, true);
                if (!can && reason != null) TooltipHandler.TipRegion(btn, reason);
                y += 42f;
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, width, 30f), "CC_Ambition_History".Translate());
            Text.Font = GameFont.Small;
            y += 32f;
            int shown = 0;
            for (int i = comp.chronicle.Count - 1; i >= 0 && shown < 15; i--)
            {
                var e = comp.chronicle[i];
                if (e.kind < ChronicleKind.Meeting || e.kind > ChronicleKind.AmbitionAbandoned) continue;
                var col = e.kind == ChronicleKind.AmbitionAchieved ? GoodColor : Faint;
                y += WrappedLabel(0f, y, width, MainTabWindow_Council.AgoString(e.tick) + "  " + e.text, col) + 2f;
                shown++;
            }

            EndScroll(y);
        }
    }
}
