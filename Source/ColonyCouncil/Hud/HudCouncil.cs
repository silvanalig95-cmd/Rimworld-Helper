using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The council at a glance: open wishes (and whose has waited longest), petitions, promises coming due, questions
    /// waiting for an answer, the decrees in force with the days they have left, the next day on the calendar or the
    /// next meeting, and the colony's trust. Only what has something to say shows; each line opens the council window
    /// on the right page (a waiting question opens its letter).
    /// </summary>
    public class HudCouncil : HudLinesPanel
    {
        public const string PartWishes = "wishes", PartPetitions = "petitions", PartPromises = "promises", PartQuestions = "questions",
            PartDecrees = "decrees", PartCalendar = "calendar", PartTrust = "trust";
        public static readonly string[] Parts = { PartWishes, PartPetitions, PartPromises, PartQuestions, PartDecrees, PartCalendar, PartTrust };

        public HudCouncil() : base(HudLayout.Council)
        {
        }

        public override string[] PartNames => Parts;

        protected override float FullWidth => 244f;

        protected override string Empty => HudKit.T("CC_Pnl_CouncilCalm");

        private static Action Page(string page) => () => MainTabWindow_Council.OpenTab(page);

        private static string ClickPage(string page) => "CC_Week_ClickPage".Translate(MainTabWindow_Council.TabName(page)).Resolve();

        protected override void Gather()
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            int now = Find.TickManager.TicksGame;
            Wishes(comp, now);
            Petitions(comp);
            Promises(comp, now);
            Questions(now);
            Decrees(comp, now);
            Coming(comp, now);
            Trust(comp);
        }

        private void Wishes(CouncilGameComponent comp, int now)
        {
            if (!S.enableWishes) return;
            var open = comp.wishes.Where(w => w.state == WishState.Open && w.pawn != null).ToList();
            if (open.Count == 0) return;
            var oldest = open.OrderBy(w => w.createdTick).First();
            float score = 0f;
            Wish worst = null;
            foreach (var w in open)
            {
                int total = Mathf.Max(1, w.expiresTick - w.createdTick);
                float s = HudRank.Wish(Mathf.Clamp01(w.TicksLeft / (float)total), w.stage);
                if (s > score)
                {
                    score = s;
                    worst = w;
                }
            }
            string text = "CC_Pnl_Wishes".Translate(open.Count).Resolve();
            string value = "CC_Pnl_WaitedShort".Translate(oldest.pawn.LabelShort, UiKit.Duration(now - oldest.createdTick)).Resolve();
            var tipLines = open.OrderBy(w => w.TicksLeft).Take(6)
                .Select(w => "CC_Pnl_WishLine".Translate(w.pawn.LabelShort, w.Label, UiKit.Duration(Mathf.Max(0, w.TicksLeft))).Resolve());
            string tip = string.Join("\n", tipLines);
            if (worst != null && score >= HudRank.SoonFloor) tip = "CC_Pnl_WishLapsing".Translate(worst.pawn.LabelShort, worst.Label).Resolve() + "\n\n" + tip;
            Add(PartWishes, Icons.Wishes, text, value, score, Tip(tip, ClickPage("Wishes")), Page("Wishes"));
        }

        private void Petitions(CouncilGameComponent comp)
        {
            var reqs = comp.decrees.requests.Where(r => r?.def != null && r.pawn != null).ToList();
            if (reqs.Count == 0) return;
            string tip = string.Join("\n", reqs.Take(6).Select(r =>
                (r.repeal ? "CC_Week_PetitionLift" : "CC_Week_Petition").Translate(r.pawn.LabelShort, r.def.label).Resolve()));
            Add(PartPetitions, Icons.Decree, "CC_Pnl_Petitions".Translate(reqs.Count).Resolve(), null, 150f, Tip(tip, ClickPage("Decrees")), Page("Decrees"));
        }

        private void Promises(CouncilGameComponent comp, int now)
        {
            int count = 0, soonest = int.MaxValue;
            float score = 0f;
            var tip = new List<string>();
            if (S.enableWishes)
                foreach (var w in comp.wishes)
                {
                    if (w.state != WishState.Open || !w.promised || w.pawn == null) continue;
                    count++;
                    soonest = Mathf.Min(soonest, w.TicksLeft);
                    int total = Mathf.Max(1, w.expiresTick - w.createdTick);
                    score = Mathf.Max(score, HudRank.Wish(Mathf.Clamp01(w.TicksLeft / (float)total), w.stage));
                    tip.Add("CC_Pnl_WishLine".Translate(w.pawn.LabelShort, w.Label, UiKit.Duration(Mathf.Max(0, w.TicksLeft))).Resolve());
                }
            var watches = comp.kept?.watches;
            if (KeptTracker.On && watches != null)
                foreach (var w in watches)
                {
                    if (w?.pawn == null || w.phase == KeptPhase.Kept) continue;
                    bool missing = w.phase == KeptPhase.Missing;
                    int left = missing ? w.graceUntil - now : w.takenBackTick + (int)(KeptMath.AmendsDays * KeptMath.TicksPerDay) - now;
                    count++;
                    soonest = Mathf.Min(soonest, left);
                    // A granted wish taken back, or about to be: soon, sooner as its time runs out.
                    score = Mathf.Max(score, left < GenDate.TicksPerDay ? 270f : 230f);
                    tip.Add((missing ? "CC_Week_MissingLeft" : "CC_Week_TakenBackLeft").Translate(KeptTracker.Span(Mathf.Max(1, left))).Resolve() + ": " + w.pawn.LabelShort +
                            ", " + (w.label ?? ""));
                }
            if (count == 0) return;
            string value = soonest < int.MaxValue ? "CC_Pnl_DueIn".Translate(UiKit.Duration(Mathf.Max(0, soonest))).Resolve() : null;
            Add(PartPromises, Icons.Promise, "CC_Pnl_Promises".Translate(count).Resolve(), value, Mathf.Max(score, 120f), Tip(string.Join("\n", tip), ClickPage("Wishes")),
                Page("Wishes"));
        }

        private void Questions(int now)
        {
            var waiting = new List<(Letter letter, int left)>();
            foreach (var l in Find.LetterStack.LettersListForReading)
            {
                if (WeekModel.WaitingKind(l) == null) continue;
                int left = l is LetterWithTimeout lt && lt.TimeoutActive ? lt.disappearAtTick - now : -1;
                waiting.Add((l, left));
            }
            if (waiting.Count == 0) return;
            var first = waiting.OrderBy(w => w.left >= 0 ? w.left : int.MaxValue).ThenBy(w => w.letter.arrivalTick).First();
            float hours = first.left >= 0 ? first.left / (float)GenDate.TicksPerHour : -1f;
            string value = first.left >= 0 ? "CC_Pnl_LeftShort".Translate(UiKit.Duration(first.left)).Resolve() : null;
            string tip = string.Join("\n", waiting.Take(6).Select(w => w.letter.Label.ToString()));
            var open = first.letter;
            Add(PartQuestions, Icons.Dilemmas ?? Icons.Decree, "CC_Pnl_Questions".Translate(waiting.Count).Resolve(), value, HudRank.Question(hours),
                Tip(tip, HudKit.T("CC_Pnl_ClickLetter")), () => open.OpenLetter());
        }

        private void Decrees(CouncilGameComponent comp, int now)
        {
            foreach (var d in comp.decrees.active)
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
                var day = CalendarTracker.Coming(now).FirstOrDefault();
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
            var amb = comp.ambitions;
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
            var people = WeekModel.Colonists();
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
