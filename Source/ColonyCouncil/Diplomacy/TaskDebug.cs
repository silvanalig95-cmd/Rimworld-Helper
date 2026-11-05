using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: force each part of the council's tasks and the keeper of secrets at the home on screen (Debug actions,
    /// "Rimfolk"): a task set at once (any office with a holder, any target, no question), every task's next step due now,
    /// a task come to what it is for, an officer's caravan at their town now and its day over; the keeper's secret learned
    /// about a notable, found out, the watch after dark now, a pressed secret answered now; the marshal's patrol now. Where
    /// it goes from there plays by the rules.
    /// </summary>
    public static class TaskDebug
    {
        private static Map Home => Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;

        private static int Now => Find.TickManager.TicksGame;

        private static void Say(string text) => Messages.Message("Council tasks: " + text, MessageTypeDefOf.SilentInput, false);

        private static void Menu(List<FloatMenuOption> options, string none)
        {
            if (options.Count == 0) options.Add(new FloatMenuOption(none, null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static IEnumerable<(DiplomacyHome home, CouncilTask task)> Live() => CouncilTasks.Running().Where(x => x.task.Live && x.home.Map == Home);

        [DebugAction("Rimfolk", "Tasks: set a task now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSet()
        {
            var home = Home;
            var opts = new List<FloatMenuOption>();
            foreach (var k in TaskMath.Kinds.Where(CouncilTasks.Offered))
            {
                var kind = k;
                var holder = CouncilTasks.HolderAt(home, TaskMath.OfficeOf(k));
                if (holder == null) continue;
                opts.Add(new FloatMenuOption(TaskWords.Office(TaskMath.OfficeOf(k)) + ": " + TaskWords.Kind(k), () =>
                {
                    var t = CouncilTasks.Of(home, TaskMath.OfficeOf(kind));
                    if (t != null && t.Live) CouncilTasks.End(CouncilTasks.HomeOf(t), t, TaskStage.Off, null, false);
                    Menu(CouncilTasks.Targets(kind, home).Select(c => new FloatMenuOption(c.label + (c.why != null ? " (" + c.why + ")" : ""), () =>
                    {
                        if (CouncilTasks.Set(home, c) == null) Say("couldn't set it: " + (CouncilTasks.TargetWhyNot(c, home) ?? CouncilTasks.WhyNot(home, TaskMath.OfficeOf(kind)) ?? "?"));
                    })).ToList(), "no target for it now");
                }));
            }
            Menu(opts, "no office with tasks has a holder");
        }

        [DebugAction("Rimfolk", "Tasks: every task's next step now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugNext()
        {
            int n = 0;
            foreach (var (_, t) in Live())
            {
                t.nextTick = Now;
                if (t.kind == TaskKind.Scout && t.last == TaskStep.Patrol) t.lastTick = Now - TaskMath.PatrolEveryDays * TaskMath.DayTicks;
                t.talkDay = -1;
                n++;
            }
            CouncilTasks.Hourly(Now);
            Say(n + " task(s) stepped");
        }

        [DebugAction("Rimfolk", "Tasks: a task comes to its end now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFinish()
        {
            Menu(Live().Select(x => new FloatMenuOption(TaskWords.Office(x.task.office) + ": " + TaskWords.Kind(x.task.kind), () =>
            {
                var t = x.task;
                var n = CouncilTasks.NotableOf(t);
                t.letterId = -1;
                if (t.letters == 0) t.letters = TaskMath.DemandAfter;
                if (t.kind == TaskKind.WinOver && n != null) n.personal += TaskMath.WinGoal - NotableBook.Opinion(n) + 1;
                if (t.kind == TaskKind.Trade) t.progress = TaskMath.TradeGoal;
                if (!TaskResults.Finished(x.home, t, x.home.Map, Now)) Say("nothing to finish for this kind now (it ends by its own steps)");
            })).ToList(), "no task under way here");
        }

        [DebugAction("Rimfolk", "Tasks: the officer's caravan at their town now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugThere()
        {
            foreach (var (h, t) in CouncilTasks.Running())
            {
                var c = t.officer?.GetCaravan();
                var town = CouncilTasks.TownOf(t, h.Map);
                if (c == null || town == null || t.visit != VisitStage.Road) continue;
                c.pather.StopDead();
                c.Tile = town.Tile;
                TaskVisits.Step(h, t, Now);
                Say(t.OfficerName + " is at " + town.Name);
                return;
            }
            Say("no officer on the road to a town");
        }

        [DebugAction("Rimfolk", "Tasks: the day at their town over now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDayOver()
        {
            foreach (var (h, t) in CouncilTasks.Running())
            {
                if (t.visit != VisitStage.There) continue;
                t.visitSince = Now - TaskMath.DayTicks * 2;
                TaskVisits.Step(h, t, Now);
                Say(t.OfficerName + " turns for home");
                return;
            }
            Say("no officer at a town");
        }

        [DebugAction("Rimfolk", "Keeper: a secret learned now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLearn()
        {
            var home = Home;
            var keeper = KeeperOfSecrets.At(home) ?? home.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            var book = NotableBook.Instance;
            if (book == null || keeper == null) return;
            Menu(book.notables.Where(n => n != null && n.Alive && n.faction != null && !n.faction.IsPlayer).Select(n => new FloatMenuOption(NotableBook.Called(n), () =>
            {
                var tie = SecretTies.Learn(Diplomacy.Colony(home), n, KeeperMath.Pick(Rand.Value), keeper);
                Say(tie != null ? "learned: " + SecretTies.What(tie) : "no room for it");
            })).ToList(), "no notable known");
        }

        [DebugAction("Rimfolk", "Keeper: the keeper's task found out now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCaught()
        {
            var x = Live().FirstOrDefault(v => v.task.kind == TaskKind.Secrets);
            if (x.task == null)
            {
                Say("no keeper's task under way here");
                return;
            }
            // A step that can only be found out: no chance of learning, the roll in the bad tenth.
            var t = x.task;
            var n = CouncilTasks.NotableOf(t);
            if (n == null) return;
            n.done.Remove(KeeperMath.CaughtSaidKey);
            Rand.PushState(1);
            try
            {
                for (int i = 0; i < 50 && t.Live; i++) KeeperOfSecrets.Step(x.home, t, x.home.Map, -1f, TaskStep.Talk, null);
            }
            finally
            {
                Rand.PopState();
            }
            Say(t.stage == TaskStage.Failed ? "found out" : "the rolls went another way: " + t.stage);
        }

        [DebugAction("Rimfolk", "Keeper: the watch after dark now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWatch()
        {
            var home = Home;
            var keeper = KeeperOfSecrets.At(home);
            var guest = home.mapPawns.AllPawnsSpawned.FirstOrDefault(p => Outsiders.IsOutsider(p) && p.GetLord() != null && !p.IsQuestLodger());
            if (keeper == null || guest == null)
            {
                Say("no keeper, or no guests on the map");
                return;
            }
            var job = JobMaker.MakeJob(JobDefOf.FollowClose, guest);
            job.expiryInterval = KeeperMath.WatchTicks;
            job.checkOverrideOnExpire = true;
            job.followRadius = 6f;
            keeper.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            Say(keeper.LabelShort + " walks with " + guest.LabelShort);
        }

        [DebugAction("Rimfolk", "Keeper: a pressed secret answered now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPress()
        {
            var book = NotableBook.Instance;
            foreach (var t in SecretTies.Ours().Where(t => t.state == Tie.State.CalledIn))
            {
                var l = book?.letters.FirstOrDefault(x => x.id == t.terms.count && x.Open);
                if (l == null) continue;
                l.due = Now;
                if (l.stage == LetterStage.ToWrite || l.stage == LetterStage.AwaitMessenger) l.stage = LetterStage.Travelling;
                NeighbourLetters.Hourly(book, Now);
                Say("their answer read");
                return;
            }
            Say("no secret pressed by letter on the road");
        }

        [DebugAction("Rimfolk", "Tasks: the marshal's patrol now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPatrol()
        {
            var x = Live().FirstOrDefault(v => v.task.kind == TaskKind.Scout);
            if (x.task == null)
            {
                Say("no marshal's patrol task here");
                return;
            }
            var p = x.task.officer;
            var cells = TaskJobs.PatrolCells(x.home.Map, p);
            if (cells.Count < 2)
            {
                Say("no way round the edges");
                return;
            }
            var job = JobMaker.MakeJob(TaskDefOf.Rimfolk_TaskPatrol, cells[0]);
            job.targetQueueA = cells.Select(c => (LocalTargetInfo)c).ToList();
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            Say(p.LabelShort + " walks the edges");
        }
    }
}
