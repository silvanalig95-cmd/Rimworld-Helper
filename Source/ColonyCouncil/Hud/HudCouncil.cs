using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The council at a glance: the plan's next step; what waits on the player, read from This week's Needs you (the
    /// one inbox, UI/Inbox.cs: open wishes and whose has waited longest, petitions, promises at risk, letters waiting
    /// for an answer, the other asks of the region and the peoples), so it shows what This week and the council button
    /// count; the decrees in force with the days they have left, the next day on the calendar or the next meeting, and
    /// the colony's trust. Only what has something to say shows; each line goes where its most pressing thing does.
    /// </summary>
    public class HudCouncil : HudLinesPanel
    {
        public const string PartWishes = InboxMath.PartWishes, PartPetitions = InboxMath.PartPetitions, PartPromises = InboxMath.PartPromises,
            PartQuestions = InboxMath.PartQuestions, PartAsks = InboxMath.PartAsks, PartDecrees = "decrees", PartCalendar = "calendar", PartTrust = "trust",
            PartNext = "next";
        public static readonly string[] Parts = { PartNext, PartWishes, PartPetitions, PartPromises, PartQuestions, PartAsks, PartDecrees, PartCalendar, PartTrust };

        public HudCouncil() : base(HudLayout.Council)
        {
        }

        public override string[] PartNames => Parts;

        protected override float FullWidth => 244f;

        protected override string Empty => HudKit.T("CC_Pnl_CouncilCalm");

        /// <summary>With more than one colony the panel names the colony in view, whose council it shows (3c).</summary>
        protected override string HeaderLabel
        {
            get
            {
                string own = base.HeaderLabel;
                if (!ColonyState.Several) return own;
                var map = ColonyView.Current?.Map;
                if (map == null) return own;
                // Made again only when the colony in view (or its name) changes, not every frame.
                string place = Homes.Name(map);
                if (labelMap != map || labelPlace != place || labelOwn != own)
                {
                    labelMap = map;
                    labelPlace = place;
                    labelOwn = own;
                    label = "CC_Many_HudCouncil".Translate(own.Named("PANEL"), place.Named("PLACE")).Resolve();
                }
                return label;
            }
        }

        private Map labelMap;
        private string labelPlace, labelOwn, label;

        private static Action Page(string page) => () => MainTabWindow_Council.OpenTab(page);

        private static string ClickPage(string page) => "CC_Week_ClickPage".Translate(MainTabWindow_Council.TabName(page)).Resolve();

        protected override void Gather()
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            int now = Find.TickManager.TicksGame;
            Next(now);
            Waiting(now);
            Decrees(comp, now);
            Coming(comp, now);
            Trust(comp);
        }

        /// <summary>The first step of the colony's plan, and how many wait behind it (Building/NextSteps.cs).</summary>
        private void Next(int now)
        {
            if (!NextSteps.On) return;
            var map = Find.CurrentMap;
            var first = NextSteps.First(map);
            if (first == null) return;
            int count = NextSteps.PlannedCount(map);
            string value = count > 1 ? "CC_Pnl_NextMore".Translate(count - 1).Resolve() : "";
            var level = NextSteps.LevelOf(first);
            float score = level >= AdviceLevel.Critical ? HudRank.SoonFloor + 60f : level >= AdviceLevel.High ? HudRank.SoonFloor : 0f;
            string tip = first.title + (first.detail.NullOrEmpty() ? "" : "\n" + first.detail);
            Add(PartNext, Icons.Plan ?? Icons.Week, "CC_Pnl_Next".Translate(first.title.Named("STEP")).Resolve(), value, score, Tip(tip, ClickPage("Plan")), Page("Plan"));
        }

        /// <summary>
        /// What waits on the player, read from This week's Needs you (Inbox.Waiting, the review's decision 16): one line for
        /// each kind (wishes, petitions, promises at risk, letters waiting, the other asks), its count, ranked by its most
        /// pressing row, the rows in the tooltip, and a click that goes where that row goes.
        /// </summary>
        private void Waiting(int now)
        {
            // A petition of several is one row of Needs you, its first want (Wishes/Petitions.cs), so it counts once here too.
            var waiting = Inbox.Waiting;
            var rows = new List<WeekRow>();
            var ranks = new List<float>();
            foreach (var part in InboxMath.PanelParts)
            {
                rows.Clear();
                foreach (var p in waiting.parts)
                foreach (var r in p.rows)
                    if (r.waits && InboxMath.PanelPart(r.part, p.provider.part) == part)
                        rows.Add(r);
                if (rows.Count == 0) continue;
                ranks.Clear();
                foreach (var r in rows) ranks.Add(r.rank);
                var first = rows[InboxMath.MostPressing(ranks)];
                float score = InboxMath.PanelScore(ranks);
                var tip = new List<string>();
                if (part == InboxMath.PartWishes && score >= HudRank.SoonFloor && first.about is Wish lapsing && lapsing.pawn != null)
                    tip.Add("CC_Pnl_WishLapsing".Translate(lapsing.pawn.LabelShort, lapsing.Label).Resolve() + "\n");
                if (rows.Count > 1)
                {
                    foreach (var r in rows.Take(6)) tip.Add(LinkTextMath.Plain(r.text));
                    if (rows.Count > 6) tip.Add("CC_Week_More".Translate(rows.Count - 6).Resolve());
                    tip.Add("");
                }
                tip.Add(first.tip.NullOrEmpty() ? LinkTextMath.Plain(first.text) : first.tip);
                var go = first.click ?? (() => MainTabWindow_Council.OpenTab("Week"));
                Add(part, PartIcon(part, first), PartText(part, rows.Count), PartValue(part, rows, now), score, string.Join("\n", tip), go);
            }
        }

        private static Texture2D PartIcon(string part, WeekRow first)
        {
            switch (part)
            {
                case InboxMath.PartWishes: return Icons.Wishes;
                case InboxMath.PartPetitions: return Icons.Decree;
                case InboxMath.PartPromises: return Icons.Promise;
                case InboxMath.PartQuestions: return Icons.Dilemmas ?? Icons.Decree;
                default: return first.icon ?? Icons.Inbox ?? Icons.Week;
            }
        }

        private static string PartText(string part, int count)
        {
            switch (part)
            {
                case InboxMath.PartWishes: return "CC_Pnl_Wishes".Translate(count).Resolve();
                case InboxMath.PartPetitions: return "CC_Pnl_Petitions".Translate(count).Resolve();
                case InboxMath.PartPromises: return "CC_Pnl_Promises".Translate(count).Resolve();
                case InboxMath.PartQuestions: return "CC_Pnl_Questions".Translate(count).Resolve();
                default: return "CC_Pnl_Asks".Translate(count).Resolve();
            }
        }

        /// <summary>The value at the right: for wishes, whose has waited longest and since when; the rest say it in their tooltips (never a time left).</summary>
        private static string PartValue(string part, List<WeekRow> rows, int now)
        {
            if (part != InboxMath.PartWishes) return null;
            Wish oldest = null;
            foreach (var r in rows)
                if (r.about is Wish w && w.pawn != null && (oldest == null || w.createdTick < oldest.createdTick))
                    oldest = w;
            return oldest == null ? null : "CC_Pnl_WaitedShort".Translate(oldest.pawn.LabelShort, UiKit.Duration(now - oldest.createdTick)).Resolve();
        }

        private void Decrees(CouncilGameComponent comp, int now)
        {
            foreach (var d in (ColonyView.Current?.decrees ?? comp.decrees).active)
            {
                if (d?.def == null) continue;
                int left = d.endTick - now;
                string value = d.endTick > 0 && left > 0 ? "CC_Pnl_LeftShort".Translate(UiKit.Duration(left)).Resolve() : null;
                string tip = d.def.description.NullOrEmpty() ? d.def.LabelCap.ToString() : d.def.LabelCap + "\n" + d.def.description;
                Add(PartDecrees, Icons.Decree, d.def.LabelCap, value, 105f, Tip(tip, ClickPage("Decrees")), Page("Decrees"));
            }
        }

        private void Coming(CouncilGameComponent comp, int now)
        {
            // The next day on the calendar (within five days) or the next meeting (within a week), whichever comes first.
            string text = null, value = null, tip = null, page = null;
            int due = int.MaxValue;
            try
            {
                var day = ColonyCalendar.Coming(now).FirstOrDefault();
                if (day != null && day.due > now)
                {
                    due = day.due;
                    text = day.title.NullOrEmpty() ? day.text : day.title;
                    tip = day.text;
                    page = "Calendar";
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("council panel: calendar", e);
            }
            var amb = ColonyView.Current?.ambitions ?? comp.ambitions;
            if (S.enableAmbitions && amb != null)
            {
                var meeting = amb.meeting;
                bool called = meeting?.map != null && (meeting.Started || meeting.scheduledTick >= 0);
                int until = called ? (meeting.Started ? 0 : meeting.scheduledTick - now) : amb.TicksUntilMeeting;
                if ((called || (until >= 0 && until <= GenDate.TicksPerDay * 7)) && now + Mathf.Max(0, until) < due)
                {
                    due = now + Mathf.Max(0, until);
                    text = "CC_Week_Meeting".Translate();
                    tip = null;
                    page = "Ambition";
                    if (called && meeting.Started) value = "CC_Week_UnderWay".Translate();
                }
            }
            if (text == null) return;
            if (value == null) value = "CC_Pnl_InShort".Translate(UiKit.Duration(Mathf.Max(0, due - now))).Resolve();
            Add(PartCalendar, page == "Calendar" ? CalendarPanel.Icon ?? Icons.Week : Icons.Council, text, value, 104f, Tip(tip, ClickPage(page)), Page(page));
        }

        private void Trust(CouncilGameComponent comp)
        {
            // The colony in view's own people, with more than one colony.
            var people = WeekModel.Colonists();
            if (ColonyState.Several)
            {
                var council = ColonyView.Current;
                people = people.Where(p => council == null || council.Has(p)).ToList();
            }
            if (people.Count == 0) return;
            float avg = 0f;
            foreach (var p in people) avg += comp.standings.TrustOf(p);
            avg /= people.Count;
            var level = PawnStanding.LevelFor(avg);
            string text = "CC_Pnl_Trust".Translate(("CC_Trust_" + level).Translate()).Resolve();
            string value = avg.ToString("+0;-0;0");
            var week = comp.history.LastOrDefault(h => h.day <= GenDate.DaysPassed - 7);
            string trend = null;
            if (week != null)
            {
                float d = avg - week.trust;
                if (Mathf.Abs(d) >= 1f) trend = "CC_Week_ColonyTrend".Translate(d.ToString("+0;-0")).Resolve();
            }
            float score = avg < -30f ? 230f : avg < 0f ? 140f : 100f;
            var line = Add(PartTrust, Icons.Trust, text, value, score, Tip("CC_Pnl_TrustTip".Translate(avg.ToString("0")).Resolve() + (trend != null ? "\n" + trend : ""),
                ClickPage("Everyone")), Page("Everyone"));
            line.quiet = UiKit.TrustColor(avg);
        }

        protected override void MenuOptions(List<FloatMenuOption> options)
        {
            options.Add(new FloatMenuOption(HudKit.T("CC_Pnl_OpenWeek"), () => MainTabWindow_Council.OpenTab("Week")));
        }
    }
}
