using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>A task the player can set: its kind and target in words, and why not when it can't be now (greyed).</summary>
    public sealed class TaskChoice
    {
        public TaskKind kind;
        public Notable notable;
        public Faction people;
        public Settlement town;
        public Pawn about;
        public string label;
        public string why;
    }

    /// <summary>
    /// Council tasks (docs/design/stage2-diplomacy.md, item 8; decision 14; release 2c.1): each of eight offices may take
    /// one task at a time at its home, chosen on the Decrees page's row for the office, on the officer's card, or from a
    /// people's card (the target first, then who). A task names its target and goes forward in steps the player sees on the
    /// map: letters written at a table or calls at the console (World/NeighbourLetters.cs, the purpose Task: TaskLetters.cs),
    /// talks with their people here and a paid informer at the gate (the keeper: KeeperOfSecrets.cs), a patrol of the edges
    /// at dawn (TaskJobs.cs), a visit by caravan through the journey planner (TaskVisits.cs: at most one officer away a home,
    /// a visit a season each), their notable received as a guest, a cart of samples, a feast. What each step does is
    /// TaskResults.cs; the rules are TaskMath.cs. History dip:task; facts us:task (FactsTasks.cs); This week, the cards and
    /// the Offices rows in TaskPanels.cs. The hour and the day ride on diplomacy's (Diplomacy.Hourly).
    /// <para>Entry points for later releases: the quartermaster's dues of the colony's own vassals (release 2c.2) are
    /// TaskKind.Dues, offered once <see cref="DuesWhyNot"/> and <see cref="DuesStep"/> are set, with <see cref="SendCart"/> for
    /// goods carried; the keeper's watch and Secret ties are KeeperOfSecrets.Watching and SecretTies (release 2c.3).</para>
    /// </summary>
    public static class CouncilTasks
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The "Council tasks" setting (with offices on): off, nothing new is set, and a visit under way still comes home.</summary>
        public static bool On => (S?.diplomacyTasks ?? true) && (S?.offices ?? true);

        // ================= Entry points for release 2c.2 =================

        /// <summary>Release 2c.2: why the colony's vassal can't have its dues collected now (null: it can), or unset while there are no vassals of Rimfolk's own.</summary>
        public static Func<Faction, Map, string> DuesWhyNot;

        /// <summary>Release 2c.2: the dues task's hour at its home (its carter, the goods, what came of it).</summary>
        public static Action<CouncilTask, DiplomacyHome, int> DuesStep;

        /// <summary>Whether a kind is offered now: built, or the dues once release 2c.2 has hooked them in.</summary>
        public static bool Offered(TaskKind k) => TaskMath.Built(k) || k == TaskKind.Dues && DuesWhyNot != null && DuesStep != null;

        // ================= Reading =================

        /// <summary>Every task record at every home that is ours (running or ended), with its home.</summary>
        public static List<(DiplomacyHome home, CouncilTask task)> All()
        {
            var list = new List<(DiplomacyHome, CouncilTask)>();
            var book = DiplomacyBook.Instance;
            if (book == null) return list;
            foreach (var h in book.Live())
                foreach (var t in h.tasks)
                    if (t != null)
                        list.Add((h, t));
            return list;
        }

        /// <summary>The tasks under way (or with an officer still away on one).</summary>
        public static List<(DiplomacyHome home, CouncilTask task)> Running() => All().Where(x => x.task.Live || x.task.Away).ToList();

        public static CouncilTask Get(int id, out DiplomacyHome home)
        {
            home = null;
            foreach (var (h, t) in All())
                if (t.id == id)
                {
                    home = h;
                    return t;
                }
            return null;
        }

        /// <summary>An office's record at a home (the task under way, or the last one), or null.</summary>
        public static CouncilTask Of(Map home, string office) => DiplomacyBook.Instance?.Existing(home)?.tasks.FirstOrDefault(t => t != null && t.office == office);

        /// <summary>The task an officer is on (or away on), or null.</summary>
        public static CouncilTask Of(Pawn officer, out DiplomacyHome home)
        {
            home = null;
            if (officer == null) return null;
            foreach (var (h, t) in All())
                if (t.officer == officer && (t.Live || t.Away))
                {
                    home = h;
                    return t;
                }
            return null;
        }

        public static CouncilTask Of(Pawn officer) => Of(officer, out _);

        /// <summary>Someone of this home away on the council's business (or about to set out on it), but <paramref name="except"/>.</summary>
        public static CouncilTask Away(Map home, CouncilTask except = null) =>
            DiplomacyBook.Instance?.Existing(home)?.tasks.FirstOrDefault(t => t != null && t != except && t.Away);

        /// <summary>Who holds an office at this home now.</summary>
        public static Pawn HolderAt(Map home, string office) => S != null && S.offices ? ColonyState.For(home)?.offices?.HolderOf(office) : null;

        public static OfficeDef OfficeDef(string office) => DefDatabase<OfficeDef>.GetNamedSilentFail(office);

        public static Notable NotableOf(CouncilTask t) => t == null || t.notable < 0 ? null : NotableBook.Instance?.Get(t.notable);

        /// <summary>The people a task deals with: the target's, else the target notable's.</summary>
        public static Faction PeopleOf(CouncilTask t) => t?.people ?? NotableOf(t)?.faction;

        /// <summary>The town a task is aimed at, or where it would go: the notable's, the town, else their people's nearest to the home.</summary>
        public static Settlement TownOf(CouncilTask t, Map home)
        {
            if (t == null) return null;
            if (t.town >= 0)
            {
                var s = NotableBook.TownOf(t.town);
                if (s != null) return s;
            }
            var n = NotableOf(t);
            if (n != null && n.town >= 0) return NotableBook.TownOf(n.town);
            var f = PeopleOf(t);
            return f != null && home != null ? WorldSituationWorker.NearestTown(f, home) : null;
        }

        /// <summary>The notable a task's letters go to: its target, else the head of their people.</summary>
        public static Notable WriteTo(CouncilTask t, Map home)
        {
            var n = NotableOf(t);
            if (n != null && n.Alive) return n;
            var f = PeopleOf(t);
            return f != null ? FavourCalls.HeadOf(f, home) : null;
        }

        // ================= Why not =================

        /// <summary>Why an office at this home can't take a task now (any kind), or null.</summary>
        public static string WhyNot(Map home, string office)
        {
            if (!On) return TaskWords.P("CC_Task_Why_Off");
            if (home == null) return TaskWords.P("CC_Task_Why_NoHome");
            var def = OfficeDef(office);
            if (def == null || !def.Available) return TaskWords.P("CC_Task_Why_NoSeat");
            var p = HolderAt(home, office);
            if (p == null) return TaskWords.P("CC_Task_Why_Empty", def.label.Named("OFFICE"));
            var t = Of(home, office);
            if (t != null && (t.Live || t.Away)) return TaskWords.P("CC_Task_Why_Busy", p.Named("PAWN"), TaskWords.Kind(t.kind).Named("KIND"));
            if (p.MapHeld != home) return TaskWords.P("CC_Task_Why_Away", p.Named("PAWN"));
            if (p.Dead || p.Downed || p.InMentalState) return TaskWords.P("CC_Task_Why_Unwell", p.Named("PAWN"));
            return null;
        }

        /// <summary>Why this kind can't be set at this home now (any target), or null.</summary>
        public static string WhyNot(Map home, TaskKind k)
        {
            if (!Offered(k)) return TaskWords.P("CC_Task_Why_NotYet");
            string why = WhyNot(home, TaskMath.OfficeOf(k));
            if (why != null) return why;
            if (k == TaskKind.Dues) return DuesWhyNot?.Invoke(null, home);
            var targets = Targets(k, home);
            if (targets.Count == 0) return TaskWords.P("CC_Task_Why_NoTarget_" + k);
            return targets.Any(c => c.why == null) ? null : targets[0].why;
        }

        // ================= Targets =================

        private static string NotableLabel(Notable n) =>
            TaskWords.P("CC_Task_Target_Notable", NotableBook.Called(n).Named("NAME"), NotableBook.RegardWords(NotableBook.Opinion(n)).Named("REGARD"));

        /// <summary>The notables a home's officers can reach: alive, of a town of another people the colony knows.</summary>
        private static IEnumerable<Notable> Notables()
        {
            var book = NotableBook.Instance;
            if (book == null || !NotableBook.On) yield break;
            foreach (var n in book.notables)
                if (n != null && n.Alive && n.faction != null && !n.faction.IsPlayer && Diplomacy.Deals(n.faction) && n.town >= 0 && n.role != NotableRole.Acquaintance
                    && NotableBook.TownOf(n.town) != null)
                    yield return n;
        }

        /// <summary>Another task at this home already aimed at the same target: its office's word, or null.</summary>
        private static string Taken(Map home, TaskChoice c)
        {
            var h = DiplomacyBook.Instance?.Existing(home);
            if (h == null) return null;
            foreach (var t in h.tasks)
            {
                if (t == null || !t.Live || TaskMath.OfficeOf(c.kind) == t.office) continue;
                bool same = c.notable != null && t.notable == c.notable.id || c.about != null && t.about == c.about
                            || c.town != null && t.town == c.town.ID && TaskMath.TargetOf(c.kind) == TaskTarget.Town
                            || c.people != null && t.people == c.people && TaskMath.TargetOf(c.kind) == TaskTarget.People && TaskMath.TargetOf(t.kind) == TaskTarget.People;
                if (same) return TaskWords.P("CC_Task_Why_Taken", TaskWords.Office(t.office).Named("OFFICE"));
            }
            return null;
        }

        /// <summary>What a kind can be aimed at from this home, each greyed with why not.</summary>
        public static List<TaskChoice> Targets(TaskKind k, Map home, Faction only = null)
        {
            var list = new List<TaskChoice>();
            if (home == null || Current.Game == null) return list;
            try
            {
                switch (TaskMath.TargetOf(k))
                {
                    case TaskTarget.Notable:
                        NotableTargets(k, home, only, list);
                        break;
                    case TaskTarget.People:
                        PeopleTargets(k, home, only, list);
                        break;
                    case TaskTarget.Town:
                        foreach (var (s, sit) in SickTowns(home))
                            if (only == null || s.Faction == only)
                                list.Add(new TaskChoice
                                {
                                    kind = k, town = s, people = s.Faction,
                                    label = TaskWords.P("CC_Task_Target_Sick", s.Name.Named("TOWN"), TaskWords.Situation(sit).Named("SICKNESS"))
                                });
                        break;
                    case TaskTarget.Colonist:
                        foreach (var f in Find.FactionManager.AllFactionsListForReading)
                        {
                            if (f == null || f.IsPlayer || only != null && f != only) continue;
                            foreach (var p in FavourCalls.Held(f))
                                list.Add(new TaskChoice { kind = k, about = p, people = f, label = TaskWords.P("CC_Task_Target_Held", p.Named("PAWN"), TaskWords.People(f).Named("PEOPLE")) });
                        }
                        break;
                    case TaskTarget.Home:
                        if (only == null) list.Add(new TaskChoice { kind = k, label = TaskWords.P("CC_Task_Target_Home", Homes.Name(home).Named("COLONY")) });
                        break;
                }
                foreach (var c in list) c.why ??= TargetWhyNot(c, home);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("council tasks: targets", e);
            }
            return list;
        }

        private static void NotableTargets(TaskKind k, Map home, Faction only, List<TaskChoice> list)
        {
            var faith = DecreeWork.ColonyFaith;
            var all = Notables().Where(n => only == null || n.faction == only).ToList();
            foreach (var n in all)
            {
                switch (k)
                {
                    case TaskKind.WinOver:
                        if (n.role != NotableRole.Head) continue;
                        break;
                    case TaskKind.Trade:
                        // Their trader; a town without one known, its head.
                        if (n.role != NotableRole.Trader && !(n.role == NotableRole.Head && !all.Any(x => x.town == n.town && x.role == NotableRole.Trader))) continue;
                        break;
                    case TaskKind.KeepFaith:
                    case TaskKind.SpeakFaith:
                    {
                        if (n.role != NotableRole.Head || faith == null || !ModsConfig.IdeologyActive) continue;
                        var ideo = n.pawn?.Ideo;
                        if (ideo == null || (k == TaskKind.KeepFaith) != (ideo == faith)) continue;
                        break;
                    }
                    case TaskKind.Secrets:
                        if (n.role != NotableRole.Head && n.role != NotableRole.Heir && n.role != NotableRole.Envoy && n.role != NotableRole.Trader) continue;
                        break;
                }
                list.Add(new TaskChoice { kind = k, notable = n, people = n.faction, town = NotableBook.TownOf(n.town), label = NotableLabel(n) });
            }
        }

        private static void PeopleTargets(TaskKind k, Map home, Faction only, List<TaskChoice> list)
        {
            var seen = new HashSet<Faction>();
            IEnumerable<Faction> peoples = Find.FactionManager.AllFactionsListForReading.Where(f => f != null && !f.IsPlayer && Diplomacy.Deals(f));
            foreach (var f in peoples)
            {
                if (only != null && f != only || !seen.Add(f)) continue;
                switch (k)
                {
                    case TaskKind.Drill:
                        if (Pacts.Of(f, home, PactKind.Defence) == null) continue;
                        break;
                    case TaskKind.TheirKin:
                        if (!home.mapPawns.PrisonersOfColonySpawned.Any(p => p?.Faction == f && p.RaceProps.Humanlike)) continue;
                        break;
                    case TaskKind.Host:
                        if (Diplomacy.Hostile(f) || FavourCalls.HeadOf(f, home) == null) continue;
                        if (WorldSituationWorker.NearestTown(f, home) == null) continue;
                        break;
                    case TaskKind.Dues:
                        if (DuesWhyNot == null) continue;
                        break;
                }
                var head = FavourCalls.HeadOf(f, home);
                list.Add(new TaskChoice
                {
                    kind = k, people = f, notable = k == TaskKind.Host ? head : null, town = WorldSituationWorker.NearestTown(f, home),
                    label = head != null ? TaskWords.P("CC_Task_Target_People", TaskWords.People(f).Named("PEOPLE"), NotableBook.Called(head).Named("NAME"))
                        : TaskWords.People(f)
                });
            }
        }

        /// <summary>Towns of peoples at peace with a sickness running there, and the world event.</summary>
        public static List<(Settlement town, WorldSituation sit)> SickTowns(Map home)
        {
            var list = new List<(Settlement, WorldSituation)>();
            var ws = WorldSituations.Instance;
            if (ws == null || home == null) return list;
            foreach (var s in ws.Running)
            {
                if (s?.def == null || s.def.kind != "sickness" || s.place.kind != RefKind.Town) continue;
                var town = Refs.Town(s.place);
                if (town == null || town.Destroyed || town.Faction == null || town.Faction.IsPlayer || Diplomacy.Hostile(town.Faction)) continue;
                if (list.Any(x => x.Item1 == town)) continue;
                list.Add((town, s));
            }
            return list;
        }

        /// <summary>Why this target can't be taken on now, or null.</summary>
        public static string TargetWhyNot(TaskChoice c, Map home)
        {
            if (c == null) return TaskWords.P("CC_Task_Why_Gone");
            var f = c.people ?? c.notable?.faction;
            if (TaskMath.AtPeaceOnly(c.kind) && f != null && Diplomacy.Hostile(f)) return TaskWords.P("CC_Task_Why_War", TaskWords.People(f).Named("PEOPLE"));
            string taken = Taken(home, c);
            if (taken != null) return taken;
            var n = c.notable;
            switch (c.kind)
            {
                case TaskKind.WinOver:
                    if (n != null && TaskMath.Won(NotableBook.Opinion(n))) return TaskWords.P("CC_Task_Why_Won", n.Name.Named("NAME"));
                    break;
                case TaskKind.Secrets:
                    if (n != null && SecretTies.About(n).Any(t => t.Live)) return TaskWords.P("CC_Task_Why_Known", n.Name.Named("NAME"));
                    break;
                case TaskKind.SpeakFaith:
                    if (n?.pawn?.ideo != null && n.pawn.ideo.Certainty <= TaskMath.MinCertainty + 0.01f) return TaskWords.P("CC_Task_Why_Shaken", n.Name.Named("NAME"));
                    break;
                case TaskKind.Host:
                {
                    string why = Feasts.PlanWhyNot(home);
                    if (why != null) return why;
                    if (n == null) return TaskWords.P("CC_Task_Why_NoHead", TaskWords.People(f).Named("PEOPLE"));
                    break;
                }
                case TaskKind.Dues:
                    return DuesWhyNot?.Invoke(f, home) ?? (DuesStep == null ? TaskWords.P("CC_Task_Why_NotYet") : null);
            }
            if (TaskMath.Writes(c.kind) && !NeighbourLetters.On) return "CC_Letter_Why_Off".Translate().Resolve();
            // A task that is a visit needs the road and the people to spare now.
            if (TaskMath.VisitOnly(c.kind)) return TaskVisits.WhyNot(c.kind, c.town, home, HolderAt(home, TaskMath.OfficeOf(c.kind)), -1, null);
            return null;
        }

        // ================= Setting one =================

        /// <summary>The player picks a task for an office (each greyed with why not), then its target, then confirms what it costs.</summary>
        public static void Choose(Map home, string office)
        {
            string why = WhyNot(home, office);
            if (why != null) return;
            var opts = new List<FloatMenuOption>();
            foreach (var k in TaskMath.KindsOf(office))
            {
                if (!Offered(k)) continue;
                var kind = k;
                string label = TaskWords.Kind(k).CapitalizeFirst();
                string kw = WhyNot(home, k);
                if (kw != null) opts.Add(new FloatMenuOption("CC_Out_Disabled".Translate(label.Named("LABEL"), kw.Named("WHY")).Resolve(), null));
                else opts.Add(new FloatMenuOption(label, () => ChooseTarget(home, kind, null)) { tooltip = TaskWords.KindTip(k) });
            }
            if (opts.Count == 0) opts.Add(new FloatMenuOption("CC_Out_Nothing".Translate().Resolve(), null));
            Find.WindowStack.Add(new FloatMenu(opts, TaskWords.P("CC_Task_Menu_Title", TaskWords.Office(office).Named("OFFICE"), TaskWords.Form(home, null).word.Named("WORD"))));
        }

        /// <summary>From a people's card: the tasks that fit them, each with the office that would take it, greyed with why not.</summary>
        public static void ChooseFor(Map home, Faction f)
        {
            var opts = new List<FloatMenuOption>();
            foreach (var k in TaskMath.Kinds)
            {
                if (!Offered(k) || TaskMath.TargetOf(k) == TaskTarget.Home) continue;
                var targets = Targets(k, home, f);
                if (targets.Count == 0) continue;
                string office = TaskMath.OfficeOf(k);
                string head = TaskWords.P("CC_Task_Menu_Kind", TaskWords.Office(office).CapitalizeFirst().Named("OFFICE"), TaskWords.Kind(k).Named("KIND"));
                string ow = WhyNot(home, office);
                foreach (var c in targets)
                {
                    var choice = c;
                    string label = head + ": " + c.label;
                    string why = ow ?? c.why;
                    opts.Add(why != null ? new FloatMenuOption("CC_Out_Disabled".Translate(label.Named("LABEL"), why.Named("WHY")).Resolve(), null)
                        : new FloatMenuOption(label, () => Confirm(home, choice)));
                }
            }
            if (opts.Count == 0) opts.Add(new FloatMenuOption(TaskWords.P("CC_Task_Menu_None", TaskWords.People(f).Named("PEOPLE")), null));
            Find.WindowStack.Add(new FloatMenu(opts, TaskWords.P("CC_Task_Menu_ForPeople", TaskWords.People(f).Named("PEOPLE"))));
        }

        private static void ChooseTarget(Map home, TaskKind k, Faction only)
        {
            var targets = Targets(k, home, only);
            if (targets.Count == 1 && TaskMath.TargetOf(k) == TaskTarget.Home && targets[0].why == null)
            {
                Confirm(home, targets[0]);
                return;
            }
            var opts = targets.Select(c =>
            {
                var choice = c;
                return c.why != null ? new FloatMenuOption("CC_Out_Disabled".Translate(c.label.Named("LABEL"), c.why.Named("WHY")).Resolve(), null)
                    : new FloatMenuOption(c.label, () => Confirm(home, choice));
            }).ToList();
            if (opts.Count == 0) opts.Add(new FloatMenuOption(TaskWords.P("CC_Task_Why_NoTarget_" + k), null));
            Find.WindowStack.Add(new FloatMenu(opts, TaskWords.Kind(k).CapitalizeFirst()));
        }

        /// <summary>The council's word on it, with what it costs and risks named before the choice.</summary>
        private static void Confirm(Map home, TaskChoice c)
        {
            var officer = HolderAt(home, TaskMath.OfficeOf(c.kind));
            if (officer == null) return;
            string text = TaskWords.Confirm(c, officer, home);
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, () => Set(home, c)));
        }

        /// <summary>Set: the record made (the office's last visit kept), the first step soon, and a visit planned for a task that is one.</summary>
        public static CouncilTask Set(Map home, TaskChoice c)
        {
            var book = DiplomacyBook.Instance;
            if (book == null || home == null || c == null) return null;
            string office = TaskMath.OfficeOf(c.kind);
            var officer = HolderAt(home, office);
            if (officer == null || WhyNot(home, office) != null || TargetWhyNot(c, home) != null) return null;
            var h = book.HomeOf(home);
            var old = h.tasks.FirstOrDefault(x => x != null && x.office == office);
            if (old != null) h.tasks.Remove(old);
            int now = Now;
            var t = new CouncilTask
            {
                id = book.NextId(), office = office, kind = c.kind, stage = TaskStage.Running, officer = officer, officerName = officer.LabelShort,
                notable = c.notable?.id ?? -1, people = c.people ?? c.notable?.faction, town = c.town?.ID ?? -1, townName = c.town?.Name, about = c.about,
                targetName = c.notable != null ? NotableBook.Called(c.notable) : c.about?.LabelShort ?? c.town?.Name ?? (c.people != null ? TaskWords.People(c.people) : Homes.Name(home)),
                started = now, nextTick = now + Mathf.RoundToInt(TaskMath.FirstLetterHours(Rand.Value) * TaskMath.HourTicks), lastVisit = old?.lastVisit ?? -1
            };
            while (h.tasks.Count >= TaskMath.MaxPerHome)
            {
                var drop = h.tasks.Where(x => x != null && !x.Live && !x.Away).OrderBy(x => x.ended).FirstOrDefault();
                if (drop == null) break;
                h.tasks.Remove(drop);
            }
            h.tasks.Add(t);
            Write(t, home, TaskWords.P("CC_Task_Hist_Set", officer.Named("PAWN"), TaskWords.Office(office).Named("OFFICE"), TaskWords.Kind(c.kind).Named("KIND"),
                t.targetName.Named("TARGET")), false);
            // apart: the council's decision, told in a message; the officer's steps play out on the map from here.
            Tell.Message(NewsKind.Colony, Importance.Notable, home, TaskWords.T("CC_Task_Msg_Set", officer.Named("PAWN"), TaskWords.Office(office).Named("OFFICE"),
                TaskWords.Kind(c.kind).Named("KIND"), t.targetName.Named("TARGET")), new LookTargets(officer), MessageTypeDefOf.NeutralEvent);
            if (TaskMath.VisitOnly(c.kind)) TaskVisits.Plan(t, home);
            Diplomacy.Dirty();
            return t;
        }

        /// <summary>The council calls it off (the card's or the row's act): a visit planned is called off too; one under way comes home.</summary>
        public static void CallOff(CouncilTask t)
        {
            var h = HomeOf(t);
            if (t == null || h == null || !t.Live) return;
            End(h, t, TaskStage.Off, TaskWords.P("CC_Task_Result_CalledOff", t.OfficerName.Named("PAWN")), true);
        }

        public static DiplomacyHome HomeOf(CouncilTask t)
        {
            foreach (var (h, x) in All())
                if (x == t)
                    return h;
            return null;
        }

        /// <summary>
        /// Over: done, failed or called off, with its words; a visit still only planned is called off, one under way still
        /// comes home (its hour goes on), the history and a message told.
        /// </summary>
        internal static void End(DiplomacyHome h, CouncilTask t, TaskStage stage, string line, bool told)
        {
            if (t == null || !t.Live) return;
            t.stage = stage;
            t.ended = Now;
            if (!line.NullOrEmpty()) t.result = line;
            if (t.visit == VisitStage.Planned) TaskVisits.CallOffPlan(t, h?.Map);
            var home = h?.Map;
            if (stage != TaskStage.Off || told) Write(t, home, line, stage == TaskStage.Done || stage == TaskStage.Failed);
            if (told && !line.NullOrEmpty())
                // apart: what became of the council's task, told in a message.
                Tell.Message(NewsKind.Colony, stage == TaskStage.Off ? Importance.Notable : Importance.Important, home, line,
                    t.officer != null && t.officer.Spawned ? new LookTargets(t.officer) : LookTargets.Invalid,
                    stage == TaskStage.Done ? MessageTypeDefOf.PositiveEvent : stage == TaskStage.Failed ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.NeutralEvent);
            Diplomacy.Dirty();
        }

        /// <summary>Into the history (dip:task): the officer did it, for the colony, with the target's people; a milestone when it came to something.</summary>
        internal static void Write(CouncilTask t, Map home, string line, bool milestone)
        {
            if (t == null || line.NullOrEmpty()) return;
            var actors = new List<HistoryActor>();
            if (t.officer != null) actors.Add(new HistoryActor(WorldLedger.PersonOf(t.officer), HistoryRole.Did));
            actors.Add(new HistoryActor(Diplomacy.Colony(home), HistoryRole.For));
            var n = NotableOf(t);
            if (n != null) actors.Add(new HistoryActor(n.Person, HistoryRole.With));
            else if (t.people != null) actors.Add(new HistoryActor(Refs.Of(t.people), HistoryRole.With));
            if (t.about != null) actors.Add(new HistoryActor(WorldLedger.PersonOf(t.about), HistoryRole.About));
            var town = TownOf(t, home);
            Diplomacy.Write(TaskMath.HistoryKind, actors, town != null ? Refs.Of(town) : Diplomacy.Colony(home), line, milestone);
        }

        // ================= The hour =================

        public static void Hourly(int now)
        {
            foreach (var (h, t) in All())
            {
                if (!t.Live && !t.Away) continue;
                try
                {
                    Step(h, t, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("council tasks: the hour", e);
                }
            }
        }

        /// <summary>Once a day: records ended a season ago let go (the office's last visit is a season old by then too).</summary>
        public static void Daily(int now)
        {
            var book = DiplomacyBook.Instance;
            if (book == null) return;
            foreach (var h in book.Live())
                h.tasks.RemoveAll(t => t == null || !t.Live && !t.Away && t.ended >= 0 && now - t.ended > TaskMath.SeasonDays * TaskMath.DayTicks
                                        && (t.lastVisit < 0 || now - t.lastVisit > TaskMath.SeasonDays * TaskMath.DayTicks));
        }

        /// <summary>Why a task can't go on (its officer, target or people gone), or null.</summary>
        private static string Broken(CouncilTask t, Map home)
        {
            var holder = HolderAt(home, t.office);
            if (t.officer == null || t.officer.Dead || holder != t.officer) return TaskWords.P("CC_Task_Result_Officer", t.OfficerName.Named("PAWN"), TaskWords.Office(t.office).Named("OFFICE"));
            var f = PeopleOf(t);
            if (f != null && (f.defeated || !Diplomacy.Deals(f))) return TaskWords.P("CC_Task_Result_Gone", t.OfficerName.Named("PAWN"), (t.targetName ?? "?").Named("TARGET"));
            if (TaskMath.AtPeaceOnly(t.kind) && f != null && Diplomacy.Hostile(f))
                return TaskWords.P("CC_Task_Result_War", t.OfficerName.Named("PAWN"), TaskWords.People(f).Named("PEOPLE"));
            if (TaskMath.TargetOf(t.kind) == TaskTarget.Notable)
            {
                var n = NotableOf(t);
                if (n == null || !n.Alive) return TaskWords.P("CC_Task_Result_Gone", t.OfficerName.Named("PAWN"), (t.targetName ?? "?").Named("TARGET"));
            }
            if (t.kind == TaskKind.Tend && t.visit == VisitStage.None && t.visits == 0 && !SickTowns(home).Any(x => x.town.ID == t.town))
                return TaskWords.P("CC_Task_Result_Healed", t.OfficerName.Named("PAWN"), (t.townName ?? "?").Named("TOWN"));
            return null;
        }

        private static void Step(DiplomacyHome h, CouncilTask t, int now)
        {
            var home = h.Map;
            if (home == null) return;
            // The visit first: it plays out to the end, whatever becomes of the task.
            if (t.visit != VisitStage.None)
            {
                TaskVisits.Step(h, t, now);
                if (!t.Live || t.visit != VisitStage.None && t.visit != VisitStage.Planned) return;
            }
            if (!t.Live) return;
            if (!On)
            {
                End(h, t, TaskStage.Off, null, false);
                return;
            }
            string why = Broken(t, home);
            if (why != null)
            {
                End(h, t, TaskStage.Off, why, true);
                return;
            }
            if (TaskResults.Finished(h, t, home, now)) return;
            if (TaskMath.GivesUp(t.kind, t.started, now) && t.visit == VisitStage.None)
            {
                bool done = TaskMath.SeasonDone(t.kind, t.Steps);
                End(h, t, done ? TaskStage.Done : TaskStage.Off,
                    done ? TaskWords.P("CC_Task_Result_Season_" + t.kind, t.OfficerName.Named("PAWN"), (t.targetName ?? "?").Named("TARGET"))
                        : TaskWords.P("CC_Task_Result_GaveUp", t.OfficerName.Named("PAWN"), TaskWords.Kind(t.kind).Named("KIND"), (t.targetName ?? "?").Named("TARGET")), true);
                return;
            }
            if (now < t.nextTick) return;
            switch (t.kind)
            {
                case TaskKind.Secrets:
                    KeeperOfSecrets.TaskHour(h, t, home, now);
                    break;
                case TaskKind.Scout:
                    PatrolHour(t, home, now);
                    break;
                case TaskKind.Drill:
                case TaskKind.Tend:
                    // The task is the visit: it waits for the plan (This week asks for it when there is none).
                    t.nextTick = now + 6 * TaskMath.HourTicks;
                    break;
                case TaskKind.Dues:
                    if (DuesStep != null) DuesStep(t, h, now);
                    else t.nextTick = now + TaskMath.DayTicks;
                    break;
                default:
                    LetterHour(t, home, now);
                    break;
            }
        }

        // ================= Letters and calls =================

        /// <summary>The next letter (or call) when the last is answered and its days are up; one at a time to them, by the officer.</summary>
        private static void LetterHour(CouncilTask t, Map home, int now)
        {
            if (t.letterId >= 0)
            {
                var open = NeighbourLetters.Get(t.letterId);
                if (open != null && open.Open)
                {
                    t.nextTick = now + 6 * TaskMath.HourTicks;
                    return;
                }
                t.letterId = -1;
            }
            // The steward writes once to prepare the feast; after a welcome answer the feast is the council's to plan.
            if (t.kind == TaskKind.Host && t.letters > 0 && t.lastHow >= 0 && t.lastHow <= (int)AnswerTone.Cool)
            {
                t.nextTick = now + TaskMath.DayTicks;
                return;
            }
            var n = WriteTo(t, home);
            if (n == null || !NeighbourLetters.On)
            {
                t.nextTick = now + 12 * TaskMath.HourTicks;
                return;
            }
            var l = TaskLetters.Send(n, t.officer, TaskLetters.TaskKey(t), TaskWords.Topic(t));
            if (l == null)
            {
                t.nextTick = now + Mathf.RoundToInt(TaskMath.RetryHours * TaskMath.HourTicks);
                return;
            }
            t.letterId = l.id;
            t.nextTick = now + Mathf.RoundToInt(TaskMath.NextLetterDays(Rand.Value) * TaskMath.DayTicks);
            Diplomacy.Dirty();
        }

        // ================= The marshal's patrol =================

        /// <summary>At dawn, every two days: the marshal walks the edges of the colony's land when free.</summary>
        private static void PatrolHour(CouncilTask t, Map home, int now)
        {
            t.nextTick = now + TaskMath.HourTicks;
            int lastPatrol = t.last == TaskStep.Patrol ? t.lastTick : t.started - TaskMath.PatrolEveryDays * TaskMath.DayTicks;
            if (!TaskMath.Dawn(GenLocalDate.HourOfDay(home)) || !TaskMath.PatrolDue(lastPatrol, now)) return;
            var p = t.officer;
            if (!Free(p, home) || p.CurJobDef == TaskDefOf.Rimfolk_TaskPatrol) return;
            var cells = TaskJobs.PatrolCells(home, p);
            if (cells.Count < 2) return;
            var job = JobMaker.MakeJob(TaskDefOf.Rimfolk_TaskPatrol, cells[0]);
            job.targetQueueA = cells.Select(c => (LocalTargetInfo)c).ToList();
            job.ignoreJoyTimeAssignment = true;
            // together: the marshal walks out alone; nobody else is needed for it.
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>The patrol walked (JobDriver_TaskPatrol): a hidden raiders' camp may be seen from the edges; the step noted.</summary>
        public static void Patrolled(Pawn p)
        {
            var t = Of(p, out var h);
            var home = h?.Map;
            if (t == null || t.kind != TaskKind.Scout || !t.Live || home == null) return;
            int now = Now;
            t.patrols++;
            t.last = TaskStep.Patrol;
            t.lastTick = now;
            bool seen = TaskMath.Spotted(Rand.Value) && RaidersCamps.SpotByPatrol(home, p);
            t.lastHow = seen ? 1 : 0;
            if (seen)
            {
                t.result = TaskWords.P("CC_Task_Patrol_Seen", p.Named("PAWN"));
                Write(t, home, t.result, true);
            }
            else
                Tell.Minor(NewsKind.Colony, Importance.Flavor, home, TaskWords.T("CC_Task_Patrol_Quiet", p.Named("PAWN"), Homes.Name(home).Named("COLONY")), new LookTargets(p),
                    MessageTypeDefOf.SilentInput);
            Diplomacy.Dirty();
        }

        // ================= Helpers =================

        /// <summary>Free to go about the council's business now: at home, awake, not needed elsewhere, no danger.</summary>
        public static bool Free(Pawn p, Map map)
        {
            if (p == null || !p.Spawned || p.Map != map || p.Dead || p.Downed || p.Drafted || p.InMentalState || !p.Awake() || p.GetLord() != null) return false;
            if (!PlayOuts.CanJoin(p) || !PlayOuts.Interruptible(p) || PlayOuts.NeedsPress(p)) return false;
            return PlayOuts.WhyNotNow(map, false) == null;
        }

        /// <summary>The home a task's officer belongs to (from the record), for the letters' answers.</summary>
        public static Map MapOf(CouncilTask t) => HomeOf(t)?.Map;

        /// <summary>
        /// Release 2c.2: a cart of goods from the stores to a people, fetched at the gate by their porter (the quartermaster's
        /// samples use it now; the vassals' dues the other way come with their own carter). The courier trip, or null.
        /// </summary>
        public static CourierTrip SendCart(Map home, ThingDef def, int count, Faction to) =>
            home == null || def == null || count <= 0 ? null : Couriers.CollectOrder(home, def, count, 0, to);

        /// <summary>Their home is no longer the colony's: its tasks end, a planned visit is called off.</summary>
        internal static void HomeLost(DiplomacyHome h)
        {
            if (h == null) return;
            foreach (var t in h.tasks.ToList())
            {
                if (t == null) continue;
                if (t.visit == VisitStage.Planned) TaskVisits.CallOffPlan(t, null);
                if (t.Live)
                {
                    t.stage = TaskStage.Off;
                    t.ended = Now;
                }
                if (t.visit == VisitStage.Planned) t.visit = VisitStage.None;
            }
        }
    }
}
