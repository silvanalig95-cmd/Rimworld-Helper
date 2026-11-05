using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Where the council's tasks and the keeper of secrets show (docs/design/interface.md, "Where stage 2 and stage 3 go":
    /// the Colonist and Event cards, Council → Offices, Here and away, This week). The officer's card: their task, its last
    /// step and the next in words, and its acts (set a task, plan a visit, pay an informer, call it off), each greyed with
    /// why not; the Offices rows on the Decrees page (a line under each seat with tasks, and its act); a notable's card: what
    /// the colony knows of them and pressing it; a Secret tie's own card; a people's card: the tasks that fit them; Here and
    /// away: the tasks under way and the officer away; and the one inbox: a visit or a feast to plan in Needs you (counted),
    /// every task under way in Under way, in steps (never a time left).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class TaskPanels
    {
        static TaskPanels()
        {
            CardSections.RegisterLines(CardKind.Colonist, CardSlot.Now, 30f, OfficerLines, null, "task-officer");
            CardActs.Register(CardKind.Colonist, r => TaskWords.P("CC_Task_Act_Set"), r => SetWhyNot(Pawn(r)), r => SetFor(Pawn(r)), r => HasTasks(Pawn(r)) && TaskOf(Pawn(r)) == null,
                34f, TaskWords.P("CC_Task_Act_Set_Tip"), "task-set");
            CardActs.Register(CardKind.Colonist, r => TaskWords.P("CC_Task_Act_Visit"), r => TaskVisits.WhyNot(TaskOf(Pawn(r)), HomeOf(Pawn(r))),
                r => TaskVisits.Ask(TaskOf(Pawn(r)), HomeOf(Pawn(r))), r => TaskOf(Pawn(r)) is CouncilTask t && t.Live && TaskMath.MayVisit(t.kind) && !t.Away, 35f,
                TaskWords.P("CC_Task_Act_Visit_Tip"), "task-visit");
            CardActs.Register(CardKind.Colonist, r => TaskWords.P("CC_Keeper_Act_Informer"), r => KeeperOfSecrets.InformerWhyNot(TaskOf(Pawn(r)), HomeOf(Pawn(r))),
                r => KeeperOfSecrets.Informer(TaskOf(Pawn(r)), HomeOf(Pawn(r))), r => TaskOf(Pawn(r)) is CouncilTask t && t.Live && t.kind == TaskKind.Secrets, 36f,
                TaskWords.P("CC_Keeper_Act_Informer_Tip"), "task-informer");
            CardActs.Register(CardKind.Colonist, r => TaskWords.P("CC_Task_Act_Off"), r => null, r => CouncilTasks.CallOff(TaskOf(Pawn(r))), r => TaskOf(Pawn(r)) is CouncilTask t && t.Live,
                70f, TaskWords.P("CC_Task_Act_Off_Tip"), "task-off");
            // A notable of theirs: what the colony knows of them, and pressing it by letter.
            CardSections.RegisterLines(CardKind.Person, CardSlot.Ties, 40f, SecretLines, null, "secret-person");
            CardActs.Register(CardKind.Person, r => TaskWords.P("CC_Keeper_Act_Press"), r => SecretTies.PressByLetterWhyNot(OpenSecret(r), Home),
                r => SecretTies.PressByLetter(OpenSecret(r), Home), r => OpenSecret(r) != null, 45f, TaskWords.P("CC_Keeper_Act_Press_Tip"), "secret-press");
            // A Secret tie on its own card.
            CardSections.RegisterLines(CardKind.Tie, CardSlot.Now, 8f, TieLines, null, "secret-tie");
            // A people: the tasks that fit them, the target first, then who.
            CardSections.RegisterLines(CardKind.People, CardSlot.Now, 46f, r => PeopleLines(ThingRefs.Faction(r)), null, "task-people");
            CardActs.Register(CardKind.People, r => TaskWords.P("CC_Task_Act_ForPeople"), r => ForPeopleWhyNot(ThingRefs.Faction(r)), r => CouncilTasks.ChooseFor(Home, ThingRefs.Faction(r)),
                r => ThingRefs.Faction(r) != null && CouncilTasks.On, 48f, TaskWords.P("CC_Task_Act_ForPeople_Tip"), "task-people-act");
            // The one inbox.
            Inbox.Register("task", InboxSection.Needs, 37f, NeedRows, NeedCount);
            Inbox.Register("task-under-way", InboxSection.UnderWay, 57f, UnderWayRows);
        }

        private static Map Home => Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;

        private static Pawn Pawn(ThingRef r) => ThingRefs.Pawn(r);

        private static Map HomeOf(Pawn p) => p == null ? null : Homes.Of(p) ?? Home;

        private static CouncilTask TaskOf(Pawn p) => CouncilTasks.Of(p);

        private static string OfficeOf(Pawn p) => p == null ? null : OfficeTracker.HeldBy(p)?.defName;

        private static bool HasTasks(Pawn p) => CouncilTasks.On && TaskMath.HasTasks(OfficeOf(p));

        private static string SetWhyNot(Pawn p) => CouncilTasks.WhyNot(HomeOf(p), OfficeOf(p));

        private static void SetFor(Pawn p) => CouncilTasks.Choose(HomeOf(p), OfficeOf(p));

        // ================= The officer's card =================

        private static List<string> OfficerLines(ThingRef r)
        {
            var p = Pawn(r);
            if (p == null) return null;
            var list = new List<string>();
            try
            {
                var t = TaskOf(p);
                var home = HomeOf(p);
                if (t != null)
                {
                    list.Add(TaskWords.Row(t, home));
                    list.Add(TaskWords.Last(t));
                    string next = TaskWords.Next(t, home);
                    if (!next.NullOrEmpty()) list.Add(next);
                }
                else if (HasTasks(p))
                {
                    var last = CouncilTasks.Of(home, OfficeOf(p));
                    if (last != null && !last.result.NullOrEmpty()) list.Add(TaskWords.P("CC_Task_Card_Last", last.result.Named("RESULT")));
                }
                // The keeper's own duties, while the seat is theirs.
                if (OfficeOf(p) == TaskMath.KeeperOffice)
                {
                    int known = SecretTies.Ours().Count;
                    if (known > 0) list.Add(TaskWords.P("CC_Keeper_Card_Known", known.Named("COUNT")));
                    list.Add(TaskWords.P("CC_Keeper_Card_Guard"));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("council tasks: the officer's card", e);
            }
            return list;
        }

        // ================= Secrets on a notable's and a tie's card =================

        private static Tie OpenSecret(ThingRef r)
        {
            var n = ThingRefs.Notable(r);
            return n == null ? null : SecretTies.About(n).FirstOrDefault(t => t.state == Tie.State.Open && t.origin == KeeperMath.Origin);
        }

        private static List<string> SecretLines(ThingRef r)
        {
            var n = ThingRefs.Notable(r);
            if (n == null) return null;
            var list = new List<string>();
            foreach (var t in SecretTies.About(n).Where(t => t.origin == KeeperMath.Origin))
                list.Add(SecretLine(t, n));
            if (n.done.Contains(KeeperMath.CaughtKey)) list.Add(TaskWords.P("CC_Keeper_Card_Caught", n.Name.Named("NAME")));
            return list;
        }

        private static string SecretLine(Tie t, Notable n)
        {
            string name = n?.Name ?? DipWords.NameOf(t.to);
            string by = SecretTies.Learner(t)?.LabelShort ?? DipWords.NameOf(t.terms.about);
            return TaskWords.P("CC_Keeper_Tie_" + t.state, name.Named("NAME"), SecretTies.What(t).Named("SECRET"), by.Named("PAWN"));
        }

        private static List<string> TieLines(ThingRef r)
        {
            var t = ThingRefs.Tie(r);
            if (t == null || t.IsView || t.kind != Tie.Kind.Secret) return null;
            var list = new List<string> { SecretLine(t, SecretTies.NotableOf(t)), DipWords.Since(t.since) };
            if (t.Live) list.Add(TaskWords.P("CC_Keeper_Tie_Weight_" + Mathf.Clamp(SecretTies.Weight(t), 1, 3)));
            return list;
        }

        // ================= A people's card =================

        private static List<string> PeopleLines(Faction f) => f == null ? null : Rows(f, null).Select(x => x.text).ToList();

        private static string ForPeopleWhyNot(Faction f)
        {
            if (f == null) return TaskWords.P("CC_Task_Why_Gone");
            if (!CouncilTasks.On) return TaskWords.P("CC_Task_Why_Off");
            var home = Home;
            foreach (var k in TaskMath.Kinds)
                if (CouncilTasks.Offered(k) && TaskMath.TargetOf(k) != TaskTarget.Home && CouncilTasks.Targets(k, home, f).Count > 0)
                    return null;
            return TaskWords.P("CC_Task_Menu_None", TaskWords.People(f).Named("PEOPLE"));
        }

        /// <summary>The tasks under way with this people at a home (or any).</summary>
        public static List<DipRow> Rows(Faction f, Map home)
        {
            var list = new List<DipRow>();
            foreach (var (h, t) in CouncilTasks.Running())
                if (CouncilTasks.PeopleOf(t) == f && (home == null || h.Map == home))
                    list.Add(Row(h, t));
            return list;
        }

        private static DipRow Row(DiplomacyHome h, CouncilTask t)
        {
            var home = h.Map;
            string note = TaskWords.Last(t);
            string next = TaskWords.Next(t, home);
            if (!next.NullOrEmpty()) note += " " + next;
            return new DipRow { text = WeekModel.AtHome(home, TaskWords.Row(t, home)), note = note, color = t.Away ? UiKit.Info : UiKit.Warm, link = t.officer, act = RowAct(t, home) };
        }

        private static DipAct RowAct(CouncilTask t, Map home)
        {
            if (TaskMath.VisitOnly(t.kind) && t.Live && t.visit == VisitStage.None)
                return new DipAct { label = TaskWords.P("CC_Task_Act_Visit"), why = TaskVisits.WhyNot(t, home), tip = TaskWords.P("CC_Task_Act_Visit_Tip"), run = () => TaskVisits.Ask(t, home) };
            if (t.kind == TaskKind.Host && t.Live && !t.asked && t.letters > 0)
                return new DipAct { label = InboxWords.Plan, why = Feasts.PlanWhyNot(home), tip = TaskWords.P("CC_Task_Act_Feast_Tip"), run = () => PlanFeast(t, home) };
            return null;
        }

        private static void PlanFeast(CouncilTask t, Map home)
        {
            var n = CouncilTasks.WriteTo(t, home);
            if (home != null) Find.WindowStack.Add(new Dialog_Feast(home, CouncilTasks.PeopleOf(t), n));
        }

        // ================= Here and away =================

        /// <summary>Every task under way, for Here and away.</summary>
        public static IEnumerable<DipRow> TaskRows()
        {
            foreach (var (h, t) in CouncilTasks.Running())
                yield return Row(h, t);
        }

        /// <summary>The officers away on the council's business, for Here and away's "ours away".</summary>
        public static IEnumerable<DipRow> AwayRows()
        {
            foreach (var (h, t) in CouncilTasks.Running())
                if (t.visit == VisitStage.Road || t.visit == VisitStage.There || t.visit == VisitStage.Homeward)
                    yield return new DipRow
                    {
                        text = WeekModel.AtHome(h.Map, TaskWords.P("CC_Task_Away", ThingRefs.Link(t.OfficerName, ThingRefs.Of(t.officer)).Named("PAWN"),
                            TaskWords.Office(t.office).Named("OFFICE"), TaskWords.Next(t, h.Map).Named("WHERE"))),
                        color = UiKit.Info, link = (object)t.officer?.GetCaravan() ?? t.officer
                    };
        }

        // ================= Council → Offices =================

        /// <summary>
        /// Under an office's row on the Decrees page: its task in a line (or what the last came to), with its act (set one,
        /// plan the visit, call it off), greyed with why not. Nothing for an office without tasks. Returns the height used.
        /// </summary>
        public static float OfficeRow(OfficeDef office, Pawn holder, Map home, float x, float y, float width)
        {
            if (office == null || holder == null || !CouncilTasks.On || !TaskMath.HasTasks(office.defName)) return 0f;
            try
            {
                var t = CouncilTasks.Of(home, office.defName);
                bool live = t != null && (t.Live || t.Away);
                string text = live ? TaskWords.Row(t, home) + " " + TaskWords.Next(t, home)
                    : t != null && !t.result.NullOrEmpty() ? TaskWords.P("CC_Task_Card_Last", t.result.Named("RESULT")) : TaskWords.P("CC_Task_Office_None", TaskWords.Form(home, null).word.Named("WORD"));
                var act = live ? t.Live ? RowAct(t, home) ?? new DipAct { label = TaskWords.P("CC_Task_Act_Off"), tip = TaskWords.P("CC_Task_Act_Off_Tip"), run = () => CouncilTasks.CallOff(t) } : null
                    : new DipAct { label = TaskWords.P("CC_Task_Act_Set"), tip = TaskWords.P("CC_Task_Act_Set_Tip"), why = CouncilTasks.WhyNot(home, office.defName), run = () => CouncilTasks.Choose(home, office.defName) };
                var row = new DipRow { text = text, color = live ? UiKit.Warm : UiKit.Faint, link = live ? t.officer : null, act = act };
                return DiplomacyPanels.Row(x, y, width, row);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("council tasks: the office's row", e);
                return 0f;
            }
        }

        // ================= The inbox =================

        /// <summary>A step only the player can take: a visit to plan for a task that is one, a feast to plan for the steward's guests.</summary>
        private static IEnumerable<(DiplomacyHome h, CouncilTask t)> Needing()
        {
            foreach (var (h, t) in CouncilTasks.Running())
                if (TaskMath.NeedsPlayer(t.kind, t.stage, t.visit, t.asked, t.letters) && (t.kind != TaskKind.Host || t.lastHow >= 0 && t.lastHow <= (int)AnswerTone.Cool))
                    yield return (h, t);
        }

        private static int NeedCount() => Current.Game == null ? 0 : Needing().Count();

        private static void NeedRows(List<WeekRow> rows)
        {
            if (rows == null || Current.Game == null) return;
            try
            {
                foreach (var (h, t) in Needing())
                {
                    var task = t;
                    var home = h.Map;
                    var act = RowAct(t, home);
                    if (act == null) continue;
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Office, color = UiKit.Gold, faction = CouncilTasks.PeopleOf(t),
                        text = WeekModel.AtHome(home, TaskWords.P(t.kind == TaskKind.Host ? "CC_Task_Week_Feast" : "CC_Task_Week_Visit",
                            ThingRefs.Link(t.OfficerName, ThingRefs.Of(t.officer)).Named("PAWN"), TaskWords.TargetLink(t, home).Named("TARGET"))),
                        sub = TaskWords.P("CC_Task_WeekNote"), click = () => DiplomacyLinks.Open(task.officer), act = act.label, actWhy = act.why, actTip = act.tip, actRun = act.run,
                        follow = ThingRefs.Of(t.officer), order = t.started
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("council tasks: this week's asks", e);
            }
        }

        /// <summary>Under way: every task, in its four steps (set, at work, away, done), with its last step and the next in words.</summary>
        private static void UnderWayRows(List<WeekRow> rows)
        {
            if (rows == null || Current.Game == null) return;
            try
            {
                foreach (var (h, t) in CouncilTasks.Running())
                {
                    var task = t;
                    var home = h.Map;
                    rows.Add(new WeekRow
                    {
                        icon = t.Away ? Icons.Away ?? Icons.Office : Icons.Office, color = t.Away ? UiKit.Info : UiKit.Warm, faction = CouncilTasks.PeopleOf(t),
                        text = WeekModel.AtHome(home, TaskWords.Row(t, home)), sub = TaskWords.Next(t, home), steps = TaskWords.Steps,
                        step = TaskMath.StepIndex(t.stage, t.visit, t.Steps), click = () => DiplomacyLinks.Open(task.officer), follow = ThingRefs.Of(t.officer), order = t.started
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("council tasks: this week's under way", e);
            }
        }
    }
}
