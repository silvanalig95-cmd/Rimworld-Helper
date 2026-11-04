using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Council → Ambition: the colony's current ambition with meters for its progress and the time left, its goals
    /// as a checklist, the next meeting when there's no ambition, and past meetings and ambitions.
    /// </summary>
    public static class AmbitionPanel
    {
        // The interface's tokens (UI/UiKit.cs): an ambition reached is done, one under way gold (the colony's chosen aim).
        private static readonly Color GoodColor = UiKit.Done;
        private static readonly Color AmbitionColor = UiKit.Gold;
        private static Vector2 scroll;
        private static float height = 1000f;

        private static CouncilSettings Settings => ColonyCouncilMod.Settings;

        public static void Draw(Rect rect, CouncilGameComponent comp, Map map)
        {
            if (!Settings.enableAmbitions)
            {
                UiKit.EmptyState(rect, Icons.Ambition, "CC_AmbitionsDisabled".Translate());
                return;
            }
            // The ambition and meeting of the colony in view.
            var tracker = ColonyView.Current?.ambitions ?? comp.ambitions;
            var a = tracker.active;
            int now = Find.TickManager.TicksGame;
            float total = a != null ? UiCache.Get(a, -1, () => a.Progress) : 0f;
            string title = a != null ? "CC_Ambition_Current".Translate(a.def.LabelCap.ToString()).ToString() : "CC_Ambition_None".Translate().ToString();
            string sub = a != null
                ? UiCache.Get(a, -2, () => "CC_Ambition_Meta".Translate(a.proposer?.LabelShort ?? "?", TextUtil.DaysString(Mathf.Max(0, a.deadlineTick - Find.TickManager.TicksGame))).Resolve())
                : "CC_UI_Sub_Ambition".Translate().ToString();
            float top = UiKit.PageHeader(rect, Icons.Ambition, title, sub, a != null ? total.ToStringPercent() : null, total >= 0.999f ? GoodColor : AmbitionColor);

            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            var view = new Rect(0f, 0f, outRect.width - 18f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float width = view.width;
            float y = 0f;
            if (a != null)
            {
                y += UiKit.Paragraph(0f, y, width, a.def.description, UiKit.Faint) + 8f;
                // Progress and time, side by side when there's room.
                float mw = width >= 520f ? (width - 16f) / 2f : width;
                UiKit.Meter(new Rect(0f, y, mw, UiKit.MeterHeight), total, total >= 0.999f ? GoodColor : AmbitionColor, "CC_Progress".Translate(), total.ToStringPercent(),
                    Icons.Ambition);
                var timeRect = width >= 520f ? new Rect(mw + 16f, y, mw, UiKit.MeterHeight) : new Rect(0f, y + UiKit.MeterHeight + 6f, mw, UiKit.MeterHeight);
                UiKit.Countdown(timeRect, a.deadlineTick - now, Mathf.Max(1, a.deadlineTick - a.startTick), "CC_UI_TimeLeft".Translate());
                y = timeRect.yMax + 10f;
                if (a.proposer != null)
                {
                    UiKit.LabelFit(new Rect(0f, y, 110f, 26f), "CC_UI_ProposedBy".Translate(), UiKit.Faint, GameFont.Tiny);
                    UiKit.PawnChip(112f, y + 1f, a.proposer, 24f, true, 200f, link: true);
                    y += 32f;
                }

                // The goals, as a checklist with a small bar each.
                y += UiKit.SectionTitle(0f, y, width, "CC_UI_Goals".Translate(), Icons.LifeGoal);
                for (int i = 0; i < a.def.goals.Count; i++)
                {
                    var g = a.def.goals[i];
                    int gi = i;
                    float p = UiCache.Get(a, gi * 2, () => a.GoalProgress(gi));
                    bool done = p >= 0.999f;
                    // A later stage counts from when it begins: shown as it would read today, and marked as later.
                    bool later = a.Later(gi);
                    string desc = UiCache.Get(a, gi * 2 + 1, () => later
                        ? "CC_Ambition_LaterStage".Translate(g.Describe(a.map, g.Baseline(a.map))).ToString()
                        : g.Describe(a.map, a.Baseline(gi)));
                    float barW = Mathf.Min(120f, width * 0.2f);
                    float textW = width - 40f - barW - 12f;
                    Text.Font = GameFont.Small;
                    float h = Mathf.Max(28f, Text.CalcHeight(desc, textW) + 6f);
                    var row = new Rect(0f, y, width, h);
                    UiKit.Zebra(row, i);
                    Widgets.CheckboxDraw(6f, y + (h - 22f) / 2f, done, false, 22f);
                    GUI.color = done ? GoodColor : later ? UiKit.Faint : UiKit.Ink;
                    Widgets.Label(new Rect(36f, y + 3f, textW, h - 6f), desc);
                    GUI.color = UiKit.Ink;
                    UiKit.Bar(new Rect(width - barW - 6f, y + h / 2f - 4f, barW, 8f), p, done ? GoodColor : AmbitionColor);
                    y += h;
                }
                y += 12f;
                // Its acts: plan the room it asks for (greyed with why where the colony can't), give it up.
                if (a.def.template != null && map != null)
                {
                    string why = a.def.template.UnavailableReason(map);
                    if (UiKit.ActButton(new Rect(0f, y, 200f, UiKit.RowStandard), "CC_PlanIt".Translate(), why)) MainTabWindow_Council.StartPlanning(a.def.template, true);
                    if (why != null) UiKit.WhyNotLine(0f, y + UiKit.RowStandard + 2f, width, "CC_PlanIt".Translate(), why);
                }
                if (UiKit.ActButton(new Rect(210f, y, 220f, UiKit.RowStandard), "CC_Ambition_Abandon".Translate(), null))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CC_Ambition_AbandonConfirm".Translate(a.def.label), () => tracker.Abandon(), true));
                }
                y += 50f;
            }
            else
            {
                // No ambition: the meeting that's been called (where, when, a countdown, and buttons to hold it now or put
                // it off), the one in session, or when the council meets next and the button to call it.
                var m = tracker.meeting;
                bool scheduled = m != null && !m.Started && m.scheduledTick >= 0;
                bool inSession = m != null && m.Started;
                string status = UiCache.Get(tracker, -3, () => MeetingLine(tracker));
                Text.Font = GameFont.Small;
                float th = Text.CalcHeight(status, width - 40f);
                float meterH = scheduled || inSession ? UiKit.MeterHeight + 10f : 0f;
                float cardH = 16f + 36f + 8f + th + 10f + meterH + (inSession ? 0f : 32f) + 14f;
                UiKit.Card(new Rect(0f, y, width, cardH), null, false, false);
                UiKit.Icon(new Rect(width / 2f - 18f, y + 16f, 36f, 36f), Icons.Council, UiKit.Dim);
                Text.Anchor = TextAnchor.UpperCenter;
                GUI.color = UiKit.Faint;
                Widgets.Label(new Rect(20f, y + 60f, width - 40f, th), status);
                GUI.color = UiKit.Ink;
                Text.Anchor = TextAnchor.UpperLeft;
                float by = y + 60f + th + 10f;
                if (scheduled)
                {
                    UiKit.Countdown(new Rect(20f, by, width - 40f, UiKit.MeterHeight), m.scheduledTick - now, Mathf.Max(1, m.scheduledTick - m.calledTick), "CC_UI_StartsIn".Translate());
                    by += meterH;
                    float bw = Mathf.Min(200f, (width - 50f) / 2f);
                    var hold = new Rect(width / 2f - bw - 5f, by, bw, UiKit.RowStandard);
                    var off = new Rect(width / 2f + 5f, by, bw, UiKit.RowStandard);
                    bool due = now >= m.scheduledTick;
                    bool canHold = tracker.CanHoldNow, canPutOff = tracker.CanPutOffADay;
                    string holdTip = due && m.waitReason != null ? "CC_Meeting_Waiting".Translate(m.waitReason).ToString()
                        : !due && !canHold ? "CC_Meeting_HoldNow_Night".Translate().ToString() : "CC_Meeting_HoldNow_Tip".Translate().ToString();
                    // Greyed with why not (the reason as its tooltip) rather than refused.
                    if (UiKit.ActButton(hold, "CC_Meeting_HoldNow".Translate(), canHold ? null : holdTip, holdTip)) tracker.HoldNow();
                    if (UiKit.ActButton(off, "CC_Meeting_PutOffDayButton".Translate(), canPutOff ? null : "CC_Meeting_PutOffDayButton_Tip".Translate().ToString(),
                            "CC_Meeting_PutOffDayButton_Tip".Translate())) tracker.PutOffADay();
                }
                else if (inSession)
                {
                    int length = LordJob_CouncilMeeting.MeetingTicks;
                    UiKit.Countdown(new Rect(20f, by, width - 40f, UiKit.MeterHeight), length - (now - m.startedTick), length, "CC_UI_TimeLeft".Translate());
                }
                else
                {
                    bool can = tracker.CanCallMeetingNow(out string reason);
                    var btn = new Rect(width / 2f - 130f, by, 260f, UiKit.RowStandard);
                    if (UiKit.ActButton(btn, "CC_Ambition_CallMeeting".Translate(), can ? null : reason ?? "")) tracker.HoldMeeting(comp, true);
                }
                y += cardH + 14f;
            }

            y += UiKit.SectionTitle(0f, y, width, "CC_Ambition_History".Translate(), Icons.Chronicle);
            int shown = 0;
            const float dateW = 64f;
            for (int i = comp.chronicle.Count - 1; i >= 0 && shown < 15; i--)
            {
                var e = comp.chronicle[i];
                if (e.kind < ChronicleKind.Meeting || e.kind > ChronicleKind.AmbitionAbandoned) continue;
                var col = UiKit.ChronicleColor(e.kind);
                string text = ChroniclePanel.TextOf(e, comp);
                float h = Mathf.Max(24f, LinkText.Height(text, width - dateW - 28f) + 4f);
                UiKit.Zebra(new Rect(0f, y, width, h), shown);
                UiKit.LabelFit(new Rect(4f, y + 2f, dateW - 6f, 20f), MainTabWindow_Council.AgoString(e.tick), UiKit.Faint, GameFont.Tiny);
                UiKit.Icon(new Rect(dateW, y + 4f, UiKit.IconDense, UiKit.IconDense), Icons.For(e.kind), col);
                // The kind's colour on its icon; the words in ink with the colonists they name as links.
                LinkText.Draw(new Rect(dateW + 22f, y + 2f, width - dateW - 28f, h - 4f), text, col == UiKit.Faint ? UiKit.Faint : UiKit.Ink);
                y += h;
                shown++;
            }
            if (shown == 0)
            {
                UiKit.EmptyLine(new Rect(0f, y, width, UiKit.RowDense + 4f), "CC_Overview_Nothing".Translate());
                y += UiKit.RowDense + 8f;
            }
            height = Mathf.Max(y + 10f, 100f);
            Widgets.EndScrollView();
        }

        private static string MeetingLine(AmbitionTracker tracker)
        {
            int ticks = tracker.TicksUntilMeeting;
            int now = Find.TickManager.TicksGame;
            var m = tracker.meeting;
            if (m != null && m.Started) return InSessionText(m);
            if (m != null && m.map != null && m.scheduledTick >= 0)
            {
                string s = AmbitionTracker.AnnouncementText(m, now);
                if (now >= m.scheduledTick && m.waitReason != null) s += "\n\n" + "CC_Meeting_Waiting".Translate(m.waitReason).Resolve();
                return s;
            }
            if (m != null) return "CC_Meeting_Called".Translate(TextUtil.JoinAnd(m.pitches.Select(p => p.def.label).ToList())).Resolve();
            return ticks >= 0 ? "CC_Ambition_NextMeeting".Translate(TextUtil.DaysString(ticks)).Resolve() : "CC_Ambition_SoonMeeting".Translate().Resolve();
        }

        /// <summary>The meeting under way: where, how many so far, the topics, and who isn't coming and why.</summary>
        public static string InSessionText(CouncilMeeting m)
        {
            string s = "CC_Meeting_InSession".Translate(m.venue ?? "?", m.attendees.Count, TextUtil.JoinAnd(m.pitches.Select(p => p.def.label).ToList())).Resolve();
            var notComing = m.invites.Where(i => !i.Expected && i.status != MeetingStatus.Skipped && !i.Arrived && i.pawn != null)
                .Select(i => i.reason.NullOrEmpty() ? i.pawn.LabelShort : i.pawn.LabelShort + " (" + i.reason + ")").ToList();
            if (notComing.Count > 0) s += "\n" + "CC_Meeting_NotComing".Translate(string.Join(", ", notComing)).Resolve();
            return s;
        }
    }
}
