using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Where a step comes in the ladder for one aim (a killbox line builds its killbox early).</summary>
    public class DefenseStepOrder
    {
        public string aim;
        public int order;
    }

    /// <summary>
    /// One rung of the ladder a defense line climbs: close the gap, clear the field of fire, cover behind the gate,
    /// traps on the way in, guns that never sleep, the killbox, harden the line, a line to fall back to. Each says why it
    /// comes now, how it works and the common mistakes, names the Almanac page and the plan it places and where (on the
    /// line, so many cells behind or in front of it, or between it and the beds), and has a worker that reads from the
    /// map whether it's done.
    /// </summary>
    public class DefenseStepDef : Def
    {
        public int order = 100;
        /// <summary>The aims it belongs to (gate, killbox, turrets, archers); empty for all.</summary>
        public List<string> aims;
        /// <summary>A different place in the ladder for some aims.</summary>
        public List<DefenseStepOrder> orderFor;
        /// <summary>"lowtech", "industrial", or empty for any colony.</summary>
        public string era;
        /// <summary>Below these raid points it's only an idea for later (unless the line's aim wants it now).</summary>
        public float minRaidPoints;
        /// <summary>Aims that want it now whatever the raids.</summary>
        public List<string> nowFor;
        /// <summary>The plans to place, best first; looked up by name, so one that's missing is passed over.</summary>
        public List<string> templates;
        /// <summary>Where, relative to the line: "line" (its wall row on the gate, <see cref="distance"/> cells back), "behind", "front", "between".</summary>
        public string placeAt;
        public int distance;
        [MustTranslate] public string labelLowTech;
        [MustTranslate] public string whyNow;
        [MustTranslate] public string howItWorks;
        [MustTranslate] public List<string> mistakes;
        /// <summary>Almanac pages, best first; the first that exists is linked.</summary>
        public List<string> articles;
        public Type workerClass = typeof(DefenseStepWorker);

        [Unsaved] private DefenseStepWorker worker;

        public DefenseStepWorker Worker
        {
            get
            {
                if (worker == null)
                {
                    try
                    {
                        worker = (DefenseStepWorker)Activator.CreateInstance(workerClass ?? typeof(DefenseStepWorker));
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("defense step " + defName, e);
                        worker = new DefenseStepWorker();
                    }
                    worker.def = this;
                }
                return worker;
            }
        }

        public string LabelNow => Avail.IsPreIndustrial && !labelLowTech.NullOrEmpty() ? labelLowTech : label;

        public string LabelCapNow => LabelNow.CapitalizeFirst();

        public int OrderFor(string aim) => orderFor?.FirstOrDefault(o => o != null && o.aim == aim)?.order ?? order;

        public bool BelongsTo(string aim) => aims.NullOrEmpty() || aims.Contains(aim);

        public bool EraFits
        {
            get
            {
                if (era.NullOrEmpty()) return true;
                return era == "lowtech" ? Avail.IsPreIndustrial : !Avail.IsPreIndustrial;
            }
        }

        public AlmanacArticleDef Article => articles?.Select(a => DefDatabase<AlmanacArticleDef>.GetNamedSilentFail(a)).FirstOrDefault(a => a != null);

        /// <summary>The first of <see cref="templates"/> the game has (available or not).</summary>
        public DefenseTemplateDef FirstTemplate => templates?.Select(t => DefDatabase<DefenseTemplateDef>.GetNamedSilentFail(t)).FirstOrDefault(t => t != null);

        /// <summary>The steps for a line with this aim, in its order.</summary>
        public static List<DefenseStepDef> Ladder(string aim) =>
            DefDatabase<DefenseStepDef>.AllDefsListForReading.Where(d => d.BelongsTo(aim) && d.EraFits).OrderBy(d => d.OrderFor(aim)).ToList();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (!era.NullOrEmpty() && era != "lowtech" && era != "industrial") yield return "era must be lowtech or industrial";
            if (!placeAt.NullOrEmpty() && placeAt != "line" && placeAt != "behind" && placeAt != "front" && placeAt != "between") yield return "unknown placeAt " + placeAt;
            if (aims != null)
                foreach (var a in aims)
                    if (!DefenseLine.Aims.Contains(a)) yield return "unknown aim " + a;
            if (workerClass == null || !typeof(DefenseStepWorker).IsAssignableFrom(workerClass)) yield return "workerClass must be a DefenseStepWorker";
            if (howItWorks.NullOrEmpty()) yield return "no howItWorks";
        }
    }

    /// <summary>What a step's worker says: where it stands, how far along, what's left (or why not), and the plan to place.</summary>
    public class StepResult
    {
        public DefenseStepDef def;
        /// <summary>A name for this colony when the step's own doesn't fit (a sniper's post in a colony of rifles).</summary>
        public string label;
        public StepState state = StepState.Open;
        public float progress;
        /// <summary>One sentence: what's left, why not yet, or why it doesn't apply.</summary>
        public string left;
        /// <summary>More to say on the card (the killbox's explanation, where it fits).</summary>
        public List<string> notes = new List<string>();
        public DefenseTemplateDef template;
        public string placeAt;
        public int distance;
        public bool closeGap = true;
        /// <summary>A one-click fix in place of a plan (clearing the field).</summary>
        public Action fix;
        public string fixLabel, fixTip;
        /// <summary>Where "Show" looks.</summary>
        public IntVec3 look = IntVec3.Invalid;

        public bool Counts => state != StepState.Skipped && state != StepState.NotApplicable;

        public string Label => label ?? def?.LabelCapNow ?? "?";
    }

    /// <summary>
    /// Reads one step from the map for a line. The base worker places its plan and calls it done by the plans the
    /// line has of it; the steps' own workers (<see cref="StepWorker_CloseGap"/> and the rest) read what stands, so a
    /// step built by hand counts too.
    /// </summary>
    public class DefenseStepWorker
    {
        public DefenseStepDef def;

        public virtual StepResult Evaluate(DefenseLineFacts f)
        {
            var r = Start(f);
            var p = f.BestPlan(r.template);
            if (p != null) Band(r, p.Progress, "CC_DefLine_Left_Plan".Translate(p.present, p.total).Resolve());
            return r;
        }

        /// <summary>A result with the step's plan and place filled in: the first plan the colony can build, else the first there is.</summary>
        protected StepResult Start(DefenseLineFacts f)
        {
            var r = new StepResult { def = def, placeAt = def.placeAt ?? "behind", distance = def.distance, look = f.gate };
            r.template = Pick(f, def.templates);
            return r;
        }

        protected static DefenseTemplateDef Pick(DefenseLineFacts f, IEnumerable<string> names)
        {
            if (names == null) return null;
            DefenseTemplateDef first = null;
            foreach (var n in names)
            {
                var t = DefDatabase<DefenseTemplateDef>.GetNamedSilentFail(n);
                if (t == null) continue;
                first ??= t;
                if (t.EraFits && t.UnavailableReason(f.map) == null) return t;
            }
            return first;
        }

        protected static DefenseTemplateDef T(string name) => DefDatabase<DefenseTemplateDef>.GetNamedSilentFail(name);

        /// <summary>Done, partly or open by a share, with the sentence for what's left.</summary>
        protected static void Band(StepResult r, float progress, string left)
        {
            r.progress = Math.Max(r.progress, progress);
            r.state = DefenseLineMath.Band(r.progress);
            if (r.state != StepState.Done) r.left = left;
        }

        /// <summary>Partly done (or open, at nothing), with the sentence for what's left either way.</summary>
        protected static void Part(StepResult r, float progress, string left)
        {
            r.progress = Math.Max(0f, Math.Min(0.84f, progress));
            r.state = r.progress > 0.001f ? StepState.Partly : StepState.Open;
            r.left = left;
        }

        /// <summary>Not yet, because the plan can't be built (the research or the mod it needs).</summary>
        protected static bool Blocked(StepResult r, DefenseLineFacts f)
        {
            if (r.template == null) return false;
            string why = r.template.UnavailableReason(f.map);
            if (why == null) return false;
            r.state = StepState.NotYet;
            r.left = why;
            return true;
        }
    }
}
