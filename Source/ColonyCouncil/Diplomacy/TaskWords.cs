using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The words of the council's tasks and the keeper of secrets (Languages/English/Keyed/Rimfolk_Tasks.xml): the tasks
    /// by both sides' ages (the colony's word for its officers' business by its own age, the elders' errands to the board's
    /// remits, and their way of receiving it by theirs: Ages.Form, the feature CouncilTasks), each kind in a few words,
    /// where a task stands (its last step and the next, never a time left), and their notables' secrets. Lines told often
    /// have numbered fellows (Rimfolk_Tasks_Variants.xml).
    /// </summary>
    public static class TaskWords
    {
        public static string T(string key, params NamedArgument[] args) => DipWords.T(key, args);

        public static string P(string key, params NamedArgument[] args) => DipWords.P(key, args);

        public static string People(Faction f) => DipWords.People(f);

        /// <summary>The colony's word for its officers' business by its age, and their way of receiving it by theirs.</summary>
        public static AgeForm Form(Map home, Faction f) => Ages.Form(AgeFeature.CouncilTasks, home ?? Homes.Seat, f);

        /// <summary>A levy's word by both ages (the clan's spears, levies, a militia, a security detail).</summary>
        public static string Levy(Map home, Faction f) => Ages.Form(AgeFeature.Levy, home ?? Homes.Seat, f).rite;

        public static string Kind(TaskKind k) => P("CC_Task_Kind_" + k);

        public static string KindTip(TaskKind k) => P("CC_Task_KindTip_" + k);

        public static string Office(string defName) => DefDatabase<OfficeDef>.GetNamedSilentFail(defName ?? "")?.label ?? "?";

        /// <summary>What their notable's secret is, in a few words that follow a name ("owes more than the town can pay back").</summary>
        public static string Secret(string kind) => KeeperMath.IsKind(kind) ? P("CC_Secret_Their_" + kind) : P("CC_Secret_Their_Any");

        /// <summary>A sickness running at a town, by its own name or what it is.</summary>
        public static string Situation(WorldSituation s) =>
            s == null ? "?" : !(s.ctx?.name).NullOrEmpty() ? s.ctx.name : Find.ActiveLanguageWorker.WithIndefiniteArticle(s.def?.label ?? "?");

        /// <summary>What an officer's letter is about, for the letter's own record (its topic).</summary>
        public static string Topic(CouncilTask t) => P("CC_Task_Topic_" + t.kind, (t.targetName ?? "?").Named("TARGET"));

        // ================= The council's question =================

        /// <summary>
        /// Before a task is set: who does it and what it takes from them, what each step is, and the risks named (the roads
        /// for a visit, the sickness a physician may bring home, the keeper found out, the work that waits).
        /// </summary>
        public static string Confirm(TaskChoice c, Pawn officer, Map home)
        {
            var parts = new List<string>
            {
                P("CC_Task_Confirm", officer.Named("PAWN"), Office(TaskMath.OfficeOf(c.kind)).Named("OFFICE"), Kind(c.kind).Named("KIND"), c.label.Named("TARGET"),
                    Form(home, c.people).word.Named("WORD")),
                P("CC_Task_How_" + c.kind, officer.Named("PAWN"), (c.notable?.Name ?? c.town?.Name ?? c.about?.LabelShort ?? People(c.people)).Named("NAME"),
                    TaskMath.WinGoal.Named("GOAL"))
            };
            if (TaskMath.VisitOnly(c.kind))
            {
                var comp = TaskVisits.Companion(home, officer);
                parts.Add(VisitCost(officer, comp, c.town));
            }
            else if (TaskMath.MayVisit(c.kind)) parts.Add(P("CC_Task_Confirm_Visit", officer.Named("PAWN")));
            if (c.kind == TaskKind.Secrets) parts.Add(P("CC_Task_Risk_Secrets", officer.Named("PAWN"), (c.notable?.Name ?? "?").Named("NAME")));
            if (c.kind == TaskKind.Tend) parts.Add(P("CC_Task_Risk_Tend", officer.Named("PAWN")));
            return string.Join("\n\n", parts);
        }

        /// <summary>A visit's cost: the officer and a companion away for the road there and back and a day, their work waiting, the roads the game's own.</summary>
        private static string VisitCost(Pawn officer, Pawn comp, Settlement town) =>
            comp != null
                ? P("CC_Task_Visit_Cost", officer.Named("PAWN"), comp.Named("OTHER"), (town?.Name ?? "?").Named("TOWN"), (town != null ? CardWords.DistanceShort(town.Tile) : "?").Named("DISTANCE"))
                : P("CC_Task_Visit_CostAlone", officer.Named("PAWN"), (town?.Name ?? "?").Named("TOWN"), (town != null ? CardWords.DistanceShort(town.Tile) : "?").Named("DISTANCE"));

        /// <summary>The council's question for a visit on a task already set.</summary>
        public static string VisitAsk(CouncilTask t, Map home, Settlement town, Pawn comp)
        {
            string text = P("CC_Task_Visit_Ask", t.officer.Named("PAWN"), (town?.Name ?? "?").Named("TOWN"), Kind(t.kind).Named("KIND")) + "\n\n" + VisitCost(t.officer, comp, town);
            if (t.kind == TaskKind.Tend) text += "\n\n" + P("CC_Task_Risk_Tend", t.officer.Named("PAWN"));
            if (t.kind == TaskKind.Secrets) text += "\n\n" + P("CC_Task_Risk_Secrets", t.officer.Named("PAWN"), (CouncilTasks.NotableOf(t)?.Name ?? "?").Named("NAME"));
            return text;
        }

        // ================= Where it stands =================

        private static string Link(Pawn p, string kept) => p != null ? ThingRefs.Link(p.LabelShort, ThingRefs.Of(p)) : kept ?? "?";

        /// <summary>The target as a link: the notable, the town, the colonist held, the people.</summary>
        public static string TargetLink(CouncilTask t, Map home)
        {
            var n = CouncilTasks.NotableOf(t);
            if (n != null) return ThingRefs.Link(NotableBook.Called(n), ThingRefs.Of(n));
            if (t.about != null) return Link(t.about, t.targetName);
            if (TaskMath.TargetOf(t.kind) == TaskTarget.Town)
            {
                var s = CouncilTasks.TownOf(t, home);
                if (s != null) return ThingRefs.Link(s.Name, ThingRefs.Of(s));
            }
            if (t.people != null) return ThingRefs.Link(People(t.people), ThingRefs.Of(t.people));
            return t.targetName ?? "?";
        }

        /// <summary>The task in a sentence: who, which office, what, at whom (links for Rimfolk's windows).</summary>
        public static string Row(CouncilTask t, Map home)
        {
            string who = Link(t.officer, t.officerName);
            if (!t.Live && !t.result.NullOrEmpty()) return t.result;
            return P("CC_Task_Row_" + t.kind, who.Named("PAWN"), Office(t.office).Named("OFFICE"), TargetLink(t, home).Named("TARGET"), Homes.Name(home).Named("COLONY"));
        }

        /// <summary>The last step in words: "Last: a letter, answered warmly."</summary>
        public static string Last(CouncilTask t)
        {
            if (t.last == TaskStep.None || t.lastTick < 0) return P("CC_Task_Last_None");
            string how;
            switch (t.last)
            {
                case TaskStep.Letter:
                case TaskStep.Call:
                    how = t.lastHow >= 0 && t.lastHow <= (int)AnswerTone.Silence ? NeighbourLetters.ToneWord((AnswerTone)t.lastHow) : "";
                    return P("CC_Task_Last_" + t.last, how.Named("TONE"), FactionMemory.AgoText(t.lastTick).Named("AGO"));
                case TaskStep.Patrol:
                    return P(t.lastHow == 1 ? "CC_Task_Last_PatrolSeen" : "CC_Task_Last_Patrol", FactionMemory.AgoText(t.lastTick).Named("AGO"));
                case TaskStep.Talk:
                case TaskStep.Informer:
                case TaskStep.Visit:
                    return P("CC_Task_Last_" + t.last, FactionMemory.AgoText(t.lastTick).Named("AGO"));
                default:
                    return P("CC_Task_Last_" + t.last, FactionMemory.AgoText(t.lastTick).Named("AGO"));
            }
        }

        /// <summary>What comes next in words, never a time left: "Next: another letter to Bram." "On the road to Ashford."</summary>
        public static string Next(CouncilTask t, Map home)
        {
            var town = CouncilTasks.TownOf(t, home);
            string at = town?.Name ?? t.townName ?? "?";
            switch (t.visit)
            {
                case VisitStage.Planned:
                {
                    var plan = JourneyBook.Plans.FirstOrDefault(p => p.id == t.planId);
                    return plan != null ? P("CC_Task_Next_Planned", at.Named("TOWN"), JourneyRun.When(plan).Named("WHEN")) : P("CC_Task_Next_Gate", at.Named("TOWN"));
                }
                case VisitStage.Road: return P("CC_Task_Next_Road", at.Named("TOWN"));
                case VisitStage.There: return P("CC_Task_Next_There", at.Named("TOWN"));
                case VisitStage.Homeward: return P("CC_Task_Next_Homeward");
            }
            if (!t.Live) return null;
            if (t.letterId >= 0 && (NeighbourLetters.Get(t.letterId)?.Open ?? false)) return P("CC_Task_Next_Answer");
            if (t.kind == TaskKind.Host && t.asked) return P("CC_Task_Next_Feast");
            if (t.kind == TaskKind.Host && t.letters > 0 && t.lastHow >= 0 && t.lastHow <= (int)AnswerTone.Cool) return P("CC_Task_Next_PlanFeast");
            if (TaskMath.VisitOnly(t.kind)) return P("CC_Task_Next_Visit", at.Named("TOWN"));
            if (t.kind == TaskKind.Secrets && t.tripId >= 0) return P("CC_Task_Next_Informer");
            if (t.kind == TaskKind.BringHome && t.asked) return P("CC_Task_Next_Price");
            return P("CC_Task_Next_" + t.kind, at.Named("TOWN"), (CouncilTasks.WriteTo(t, home)?.Name ?? "?").Named("NAME"));
        }

        // ================= This week's steps =================

        private static string[] steps;

        /// <summary>Set, at work, away, done: the same four for every task (never a time left).</summary>
        public static string[] Steps => steps ??= new[] { P("CC_Task_Step_Set"), P("CC_Task_Step_Work"), P("CC_Task_Step_Away"), P("CC_Task_Step_Done") };
    }
}
