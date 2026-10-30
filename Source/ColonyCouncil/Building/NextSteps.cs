using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One step of the colony's plan (CouncilGameComponent.nextSteps): what to do, why, where it came from, and what
    /// opens to do it (a room plan, a defense plan, a research project, a place on the map, a council page). Its
    /// <see cref="key"/> names its source, so the council can tell when the need has passed.
    /// </summary>
    public class NextStep : IExposable
    {
        public const string KindAdvisor = "advisor", KindDefense = "defense", KindResearch = "research", KindBase = "base", KindOwn = "own";

        public string key, kind, title, detail;
        public float score;
        public int state = NextStepsMath.Proposed;
        /// <summary>When it was proposed, when its state last changed, and how many looks in a row its source has been missing.</summary>
        public int addedTick, changedTick, missing;
        public RoomTemplateDef template;
        public bool upgrade;
        public DefenseTemplateDef defense;
        public int choke = -1;
        public ResearchProjectDef research;
        public ThingDef thing;
        public string page;
        public IntVec3 look = IntVec3.Invalid;
        public Map map;

        public bool Planned => state == NextStepsMath.Planned;
        public bool Proposed => state == NextStepsMath.Proposed;
        public bool Done => state == NextStepsMath.Done;
        public bool Dropped => state == NextStepsMath.Dropped;
        public bool HasSource => kind != KindOwn;

        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref title, "title");
            Scribe_Values.Look(ref detail, "detail");
            Scribe_Values.Look(ref score, "score");
            Scribe_Values.Look(ref state, "state");
            Scribe_Values.Look(ref addedTick, "addedTick");
            Scribe_Values.Look(ref changedTick, "changedTick");
            Scribe_Values.Look(ref missing, "missing");
            // Silently: a plan or project from a mod since removed just leaves the step without its button.
            CouncilScribe.LookDef(ref template, "template");
            Scribe_Values.Look(ref upgrade, "upgrade");
            CouncilScribe.LookDef(ref defense, "defense");
            Scribe_Values.Look(ref choke, "choke", -1);
            CouncilScribe.LookDef(ref research, "research");
            CouncilScribe.LookDef(ref thing, "thing");
            Scribe_Values.Look(ref page, "page");
            Scribe_Values.Look(ref look, "look", IntVec3.Invalid);
            Scribe_References.Look(ref map, "map");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                kind ??= KindOwn;
                key ??= "own:" + addedTick;
                title ??= "?";
            }
        }
    }

    /// <summary>
    /// The colony's next steps (the setting "The colony's next steps"): a short, ordered plan the council keeps with the
    /// player. Each hour the council looks at what it watches (the advisor's priorities, the defense page's advice, an
    /// idle research bench) and proposes steps from it, the most pressing first; the player takes a step into the plan,
    /// lets it go (not proposed again for a fortnight), or adds steps of their own. A planned step is done when the
    /// player says so, or when its source has stopped asking for it for a few hours. The plan shows on the council
    /// window's Plan page, on This week and on the council panel, each step with a button that opens what does it.
    /// </summary>
    public static class NextSteps
    {
        public static bool On => ColonyCouncilMod.Settings?.nextSteps ?? true;

        private static List<NextStep> List => CouncilGameComponent.Instance?.nextSteps;

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The plan, in the player's order (done steps still shown at their place for a few days).</summary>
        public static List<NextStep> PlanFor(Map map) =>
            List?.Where(s => (s.Planned || s.Done) && (s.map == null || s.map == map)).ToList() ?? new List<NextStep>();

        public static List<NextStep> ProposedFor(Map map) =>
            List?.Where(s => s.Proposed && (s.map == null || s.map == map)).OrderByDescending(s => s.score).ToList() ?? new List<NextStep>();

        /// <summary>The first undone step of the plan, for the panels.</summary>
        public static NextStep First(Map map) => List?.FirstOrDefault(s => s.Planned && (s.map == null || s.map == map));

        public static int PlannedCount(Map map) => List?.Count(s => s.Planned && (s.map == null || s.map == map)) ?? 0;

        // ---------------- What the council proposes ----------------

        /// <summary>The council's hour (CouncilGameComponent): the sources looked at, planned steps checked, proposals renewed.</summary>
        public static void Hourly(CouncilGameComponent comp)
        {
            var list = comp?.nextSteps;
            if (list == null || !On) return;
            int now = Now;
            int day = GenDate.TicksPerDay;
            // Let-go steps forgotten after a fortnight, done ones taken off the list after a few days.
            list.RemoveAll(s => s == null || s.Dropped && !NextStepsMath.StillDropped(s.changedTick, now, day) || s.Done && !NextStepsMath.StillShownDone(s.changedTick, now, day));
            foreach (var map in Find.Maps.Where(m => m.IsPlayerHome))
            {
                List<NextStep> found;
                try
                {
                    found = Candidates(map);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("next steps: sources", e);
                    continue;
                }
                var byKey = found.ToDictionary(c => c.key, c => c);
                // Planned steps with a source: refreshed while the source asks, done once it has stopped for a while.
                foreach (var s in list.Where(s => s.Planned && s.HasSource && s.map == map).ToList())
                {
                    if (byKey.TryGetValue(s.key, out var live))
                    {
                        s.missing = 0;
                        Carry(live, s);
                    }
                    else if (NextStepsMath.DoneNow(true, ++s.missing))
                    {
                        s.state = NextStepsMath.Done;
                        s.changedTick = now;
                        Messages.Message("CC_Plan_StepDone".Translate(s.title.Named("STEP")).Resolve(), MessageTypeDefOf.PositiveEvent, false);
                    }
                }
                // Proposals: what's current and not already in the plan, done lately or let go.
                list.RemoveAll(s => s.Proposed && s.map == map);
                var keys = list.Where(s => s.map == map || s.map == null).ToList();
                var scores = found.Select(c => c.score).ToList();
                var inPlan = found.Select(c => keys.Any(s => s.Planned && s.key == c.key)).ToList();
                var doneLately = found.Select(c => keys.Any(s => s.Done && s.key == c.key)).ToList();
                var dropped = found.Select(c => keys.Any(s => s.Dropped && s.key == c.key)).ToList();
                foreach (int i in NextStepsMath.Propose(scores, inPlan, doneLately, dropped))
                {
                    var c = found[i];
                    c.state = NextStepsMath.Proposed;
                    c.addedTick = c.changedTick = now;
                    list.Add(c);
                }
            }
        }

        private static void Carry(NextStep from, NextStep to)
        {
            to.title = from.title;
            to.detail = from.detail;
            to.score = from.score;
            to.template = from.template;
            to.upgrade = from.upgrade;
            to.defense = from.defense;
            to.choke = from.choke;
            to.research = from.research;
            to.thing = from.thing;
            to.page = from.page;
            to.look = from.look;
        }

        private static string KeyOf(AdvisorSignal s) => "adv:" + s.def.defName + (s.key.NullOrEmpty() ? "" : ":" + s.key);

        /// <summary>Everything the council could propose now: the advisor's priorities, the defense advice, an idle research bench.</summary>
        private static List<NextStep> Candidates(Map map)
        {
            var found = new List<NextStep>();
            var seen = new HashSet<string>();
            if (ColonyCouncilMod.Settings.enableAdvisor)
            {
                var signals = CouncilMapComponent.For(map)?.Signals ?? new List<AdvisorSignal>();
                foreach (var s in signals)
                {
                    bool something = s.template != null || s.act != null || s.suggestThing != null || MainTabWindow_Council.IsPage(s.page) || (s.look != null && s.look.IsValid);
                    if (!NextStepsMath.WorthProposing(s.Score, something || s.Level >= AdviceLevel.Medium, s.def.category == AdvisorCategory.Idea ? 0.04f : 0.08f)) continue;
                    string key = KeyOf(s);
                    if (!seen.Add(key)) continue;
                    found.Add(new NextStep
                    {
                        key = key, kind = NextStep.KindAdvisor, title = s.title, detail = s.detail, score = s.Score, map = map,
                        template = s.template, upgrade = s.upgrade, thing = s.suggestThing, page = MainTabWindow_Council.IsPage(s.page) ? s.page : null,
                        look = s.look != null && s.look.IsValid && s.look.PrimaryTarget.Cell.IsValid ? s.look.PrimaryTarget.Cell : IntVec3.Invalid
                    });
                }
            }
            try
            {
                var survey = DefenseSurvey.For(map);
                foreach (var item in DefenseAdvice.For(survey))
                {
                    if (item.done || item.standing || item.template == null) continue;
                    string key = "def:" + item.key;
                    if (!seen.Add(key) || !NextStepsMath.WorthProposing(item.score, true)) continue;
                    found.Add(new NextStep
                    {
                        key = key, kind = NextStep.KindDefense, title = item.title, detail = item.why, score = item.score, map = map,
                        defense = item.template, choke = item.choke, look = item.look, page = "Defenses"
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("next steps: defense advice", e);
            }
            try
            {
                foreach (var f in BaseReview.For(map, true))
                {
                    string key = "base:" + f.key;
                    if (!seen.Add(key) || !NextStepsMath.WorthProposing(f.score, true)) continue;
                    found.Add(new NextStep { key = key, kind = NextStep.KindBase, title = f.title, detail = f.detail, score = f.score, map = map, look = f.look, page = "Base", template = f.template });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("next steps: base review", e);
            }
            if (map.listerBuildings.ColonistsHaveResearchBench() && Find.ResearchManager.GetProject() == null)
            {
                var next = AdvisorWorker_Research.SuggestNext(true);
                if (next != null && seen.Add("research"))
                    found.Add(new NextStep
                    {
                        key = "research", kind = NextStep.KindResearch, title = "CC_Plan_Research_Title".Translate().Resolve(),
                        detail = "CC_Plan_Research_Detail".Translate(next.LabelCap.ToString().Named("PROJECT"), next.CostApparent.ToString("0").Named("COST")).Resolve(),
                        score = 0.25f, research = next, map = map
                    });
            }
            return found;
        }

        // ---------------- What the player does with a step ----------------

        public static void Take(NextStep s)
        {
            var list = List;
            if (s == null || list == null || s.Planned) return;
            list.Remove(s);
            s.state = NextStepsMath.Planned;
            s.changedTick = Now;
            s.missing = 0;
            var planned = list.Where(x => x.Planned && x.map == s.map).ToList();
            int at = NextStepsMath.InsertAt(planned.Select(x => x.score).ToList(), s.score);
            int index = at < planned.Count ? list.IndexOf(planned[at]) : (planned.Count > 0 ? list.IndexOf(planned[planned.Count - 1]) + 1 : 0);
            list.Insert(Mathf.Clamp(index, 0, list.Count), s);
        }

        public static void TakeAll(Map map)
        {
            foreach (var s in ProposedFor(map)) Take(s);
        }

        /// <summary>Let go: a proposal or a planned step with a source isn't proposed again for a fortnight; the player's own step just goes.</summary>
        public static void Drop(NextStep s)
        {
            var list = List;
            if (s == null || list == null) return;
            if (!s.HasSource || s.Done)
            {
                list.Remove(s);
                return;
            }
            s.state = NextStepsMath.Dropped;
            s.changedTick = Now;
        }

        public static void MarkDone(NextStep s)
        {
            if (s == null || !s.Planned) return;
            s.state = NextStepsMath.Done;
            s.changedTick = Now;
        }

        /// <summary>Back into the plan, from done or let go.</summary>
        public static void TakeBack(NextStep s)
        {
            if (s == null || s.Planned) return;
            s.state = NextStepsMath.Proposed;
            Take(s);
        }

        public static void Move(NextStep s, int dir)
        {
            var list = List;
            if (s == null || list == null) return;
            var plan = list.Where(x => (x.Planned || x.Done) && x.map == s.map).ToList();
            int i = plan.IndexOf(s);
            int to = NextStepsMath.Moved(i, plan.Count, dir);
            if (to == i) return;
            var other = plan[to];
            int a = list.IndexOf(s), b = list.IndexOf(other);
            list[a] = other;
            list[b] = s;
        }

        public static void SortByUrgency(Map map)
        {
            var list = List;
            if (list == null) return;
            var plan = list.Where(x => x.Planned && x.map == map).ToList();
            if (plan.Count < 2) return;
            var order = NextStepsMath.ByUrgency(plan.Select(x => x.score).ToList());
            var slots = plan.Select(x => list.IndexOf(x)).OrderBy(i => i).ToList();
            for (int i = 0; i < order.Count; i++) list[slots[i]] = plan[order[i]];
        }

        public static NextStep AddOwn(Map map, string title, RoomTemplateDef template)
        {
            var list = List;
            if (list == null || title.NullOrEmpty()) return null;
            var s = new NextStep
            {
                key = "own:" + Now + ":" + Rand.Range(0, 9999), kind = NextStep.KindOwn, title = title.Trim(), score = 0f, map = map, template = template,
                state = NextStepsMath.Planned, addedTick = Now, changedTick = Now
            };
            list.Add(s);
            return s;
        }

        // ---------------- What a step opens ----------------

        /// <summary>The live advisor signal behind a step, if it still fires (its "do it" action isn't saved).</summary>
        public static AdvisorSignal LiveSignal(NextStep s)
        {
            if (s?.map == null || s.kind != NextStep.KindAdvisor) return null;
            return CouncilMapComponent.For(s.map)?.Signals.FirstOrDefault(x => KeyOf(x) == s.key);
        }

        /// <summary>The buttons a step offers: each a label and what it does, the main one first.</summary>
        public static List<(string label, Action act)> Actions(NextStep s)
        {
            var acts = new List<(string, Action)>();
            if (s == null) return acts;
            var map = s.map ?? Find.CurrentMap;
            if (s.template != null && map != null && s.template.UnavailableReason(map) == null)
                acts.Add(((s.upgrade ? "CC_UpgradeIt" : "CC_PlanIt").Translate().Resolve(), () =>
                {
                    if (s.upgrade) Designator_PlanRoom.Begin(s.template, false, true);
                    else MainTabWindow_Council.StartPlanning(s.template, true);
                }));
            if (s.defense != null && map != null)
                acts.Add(("CC_Plan_PlaceIt".Translate().Resolve(), () => Designator_PlanDefense.Begin(s.defense, true, s.choke)));
            if (s.research != null && !s.research.IsFinished)
            {
                var r = s.research;
                if (r.CanStartNow && Find.ResearchManager.GetProject() == null)
                    acts.Add(("CC_Plan_StartResearch".Translate().Resolve(), () =>
                    {
                        Find.ResearchManager.SetCurrentProject(r);
                        Messages.Message("CC_Plan_ResearchStarted".Translate(r.LabelCap.ToString().Named("PROJECT")).Resolve(), MessageTypeDefOf.SilentInput, false);
                    }));
                acts.Add(("CC_Plan_OpenResearch".Translate().Resolve(), () =>
                {
                    Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Research);
                    if (MainButtonDefOf.Research.TabWindow is MainTabWindow_Research w) w.Select(r);
                }));
            }
            if (s.kind == NextStep.KindBase && map != null && s.key != null && s.key.StartsWith("base:"))
            {
                var finding = BaseReview.Finding(map, s.key.Substring(5));
                if (finding?.fix != null && !finding.fixLabel.NullOrEmpty())
                    acts.Add((finding.fixLabel, () =>
                    {
                        try
                        {
                            finding.fix();
                        }
                        catch (Exception e)
                        {
                            CouncilLog.ErrorOnce("next step fix " + s.key, e);
                        }
                    }));
            }
            var live = LiveSignal(s);
            if (live?.act != null && !live.actLabel.NullOrEmpty())
                acts.Add((live.actLabel, () =>
                {
                    try
                    {
                        live.act();
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("next step action " + s.key, e);
                    }
                }));
            if (live?.look != null && live.look.IsValid) acts.Add(("CC_Show".Translate().Resolve(), () => MainTabWindow_Council.Jump(live.look)));
            else if (s.look.IsValid && map != null) acts.Add(("CC_Show".Translate().Resolve(), () => MainTabWindow_Council.Jump(new LookTargets(s.look, map))));
            if (MainTabWindow_Council.IsPage(s.page) && s.page != "Plan")
                acts.Add(("CC_Mood_GoTo".Translate(MainTabWindow_Council.TabName(s.page)).Resolve(), () => MainTabWindow_Council.OpenTab(s.page)));
            return acts;
        }

        /// <summary>The level a step's score means, in the advisor's words.</summary>
        public static AdviceLevel LevelOf(NextStep s)
        {
            if (s == null || !s.HasSource) return AdviceLevel.Idea;
            return (AdviceLevel)NextStepsMath.LevelOf(s.score);
        }
    }
}
