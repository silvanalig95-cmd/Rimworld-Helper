using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>"Close the gap": a wall across it with a gate, and no cheap way round.</summary>
    public class StepWorker_CloseGap : DefenseStepWorker
    {
        public override StepResult Evaluate(DefenseLineFacts f)
        {
            var r = Start(f);
            r.template = GateFor(f);
            r.placeAt = "line";
            r.distance = 0;
            int n = f.span.Count;
            if (f.Closed)
            {
                if (f.Leaks)
                {
                    r.state = StepState.Partly;
                    r.progress = 0.8f;
                    r.left = DefenseLineReview.LeakWords(f);
                }
                else r.state = StepState.Done;
                return r;
            }
            if (n > 0 && f.spanClosed >= n && f.doors == 0) Part(r, 0.7f, "CC_DefLine_Close_NoDoor".Translate());
            else if (f.spanPlanned > 0 && f.spanClosed + f.spanPlanned >= n) Part(r, Mathf.Min(0.8f, f.spanClosed / (float)Mathf.Max(1, n)), "CC_DefLine_Close_Waiting".Translate(f.spanClosed, n).Resolve());
            else if (f.spanClosed > 0) Part(r, Mathf.Min(0.8f, f.spanClosed / (float)Mathf.Max(1, n)), "CC_DefLine_Close_Some".Translate(f.spanClosed, n, n - f.spanClosed - f.spanPlanned).Resolve());
            else if (!Blocked(r, f)) r.left = "CC_DefLine_Close_Open".Translate(n).Resolve();
            return r;
        }

        /// <summary>A gate with sandbags; a stretch of wall when the gap is wide; a palisade or a castle gate for a medieval colony that can raise one.</summary>
        public static DefenseTemplateDef GateFor(DefenseLineFacts f)
        {
            var gate = T("Rimfolk_Defense_Gate");
            var wall = T("Rimfolk_Defense_Wall");
            if (f.span.Count > 9 && wall != null) return wall;
            if (f.pre)
            {
                var castle = T("Rimfolk_Defense_CastleGate");
                if (castle != null && castle.UnavailableReason(f.map) == null) return castle;
                var palisade = T("Rimfolk_Defense_PalisadeGatehouse");
                if (palisade != null && palisade.UnavailableReason(f.map) == null) return palisade;
            }
            return gate ?? wall;
        }
    }

    /// <summary>"Clear the field of fire": nothing in the kill zone or in front of the gate for raiders to shelter behind.</summary>
    public class StepWorker_ClearField : DefenseStepWorker
    {
        public override StepResult Evaluate(DefenseLineFacts f)
        {
            var r = Start(f);
            r.template = null;
            int fixable = f.trees + f.plants + f.chunks + f.rocks;
            int other = f.walls + f.things;
            if (fixable + other == 0)
            {
                r.state = StepState.Done;
                return r;
            }
            var dm = f.map.designationManager;
            int marked = f.shelter.Count(t => Marked(t, dm));
            string what = DefenseLineReview.ShelterWords(f);
            if (fixable == 0) Band(r, 0.7f, "CC_DefLine_Field_ByHand".Translate(what).Resolve());
            else if (marked >= fixable) Band(r, 0.6f, "CC_DefLine_Field_Marked".Translate(what).Resolve());
            else Band(r, f.shelter.Count <= 2 ? 0.5f : 0f, "CC_DefLine_Field_Left".Translate(what).Resolve());
            if (r.state == StepState.Open) r.left = "CC_DefLine_Field_Left".Translate(what).Resolve();
            if (marked < fixable)
            {
                r.fix = () => DefenseLineReview.ClearField(f);
                r.fixLabel = "CC_DefLine_Field_Fix".Translate();
                r.fixTip = "CC_DefLine_Field_Fix_Tip".Translate();
            }
            return r;
        }

        public static bool Marked(Thing t, DesignationManager dm)
        {
            if (t.def.building != null && t.def.mineable) return dm.DesignationAt(t.Position, DesignationDefOf.Mine) != null;
            return dm.DesignationOn(t) != null;
        }
    }

    /// <summary>"Cover behind the gate": enough firing positions behind cover that see the gate, at a distance that suits the colony's weapons.</summary>
    public class StepWorker_Cover : DefenseStepWorker
    {
        public static int Need(DefenseLineFacts f) => Mathf.Clamp(f.ranged, 3, 6);

        public override StepResult Evaluate(DefenseLineFacts f)
        {
            var r = Start(f);
            r.placeAt = "behind";
            // The plan's own cover row, not its front edge, goes the distance back.
            r.distance = Mathf.Max(1, f.coverDist - DefensePlanner.CoverRow(r.template));
            r.look = f.inner;
            int need = Need(f);
            if (f.seeing >= need)
            {
                r.state = StepState.Done;
                return r;
            }
            if (f.seeing > 0) Band(r, Mathf.Min(0.8f, f.seeing / (float)need), "CC_DefLine_Cover_Some".Translate(f.seeing, need, f.coverDist).Resolve());
            else if (f.cover.Count > 0) Band(r, 0.15f, "CC_DefLine_Cover_Blind".Translate(f.cover.Count).Resolve());
            else if (!Blocked(r, f)) r.left = "CC_DefLine_Cover_None".Translate(need, f.coverDist).Resolve();
            return r;
        }
    }

    /// <summary>"Traps on the way in": a few traps in front of the gate, where raiders walk and the colony's people don't.</summary>
    public class StepWorker_Traps : DefenseStepWorker
    {
        public const int Need = 3;

        public override StepResult Evaluate(DefenseLineFacts f)
        {
            var r = Start(f);
            r.placeAt = "front";
            r.distance = Mathf.Max(1, def.distance);
            r.look = f.gate + f.outward * 4;
            string off = DefenseTemplateDef.RoleOff("trap");
            if (off != null)
            {
                r.state = StepState.NotApplicable;
                r.left = "CC_DefLine_Off".Translate(off).Resolve();
                return r;
            }
            var trap = Avail.BestBuildable(f.map, Avail.IsTrap, "TrapSpike");
            if (trap == null)
            {
                r.state = StepState.NotYet;
                r.left = "CC_DefLine_Traps_NotYet".Translate(DefenseLineReview.ResearchFor("TrapSpike")).Resolve();
                return r;
            }
            if (f.traps >= Need)
            {
                r.state = StepState.Done;
                return r;
            }
            if (f.trapsWaiting > 0) Band(r, Mathf.Min(0.8f, f.traps / (float)Need), "CC_DefLine_Traps_Waiting".Translate(f.traps, f.trapsWaiting).Resolve());
            else if (f.traps > 0) Band(r, f.traps / (float)Need, "CC_DefLine_Traps_Some".Translate(f.traps, Need).Resolve());
            var p = f.BestPlan(r.template);
            if (p != null && p.Progress > r.progress) Band(r, p.Progress, "CC_DefLine_Left_Plan".Translate(p.built, p.total).Resolve());
            // Where its plan would go on the way in, or why it wouldn't fit there (worked out once per survey and line).
            if (r.state != StepState.Done && r.template != null && r.template.UnavailableReason(f.map) == null && DefenseApproachPlanner.Applies(r.template, r.placeAt))
            {
                var trial = DefenseApproachPlanner.TrialFor(f, r.template, r.distance);
                if (!(trial?.why).NullOrEmpty()) r.notes.Add(trial.fits ? trial.why : "CC_DefLine_Approach_NoRoomNote".Translate(trial.why).Resolve());
            }
            return r;
        }
    }

    /// <summary>"Guns that never sleep": two working turrets behind the line; for a colony without them, an archers' (or sniper's) post further back.</summary>
    public class StepWorker_Guns : DefenseStepWorker
    {
        public override StepResult Evaluate(DefenseLineFacts f)
        {
            var r = Start(f);
            r.placeAt = "behind";
            r.look = f.inner;
            string off = DefenseTemplateDef.RoleOff("turret");
            var turret = off == null ? Avail.BestBuildable(f.map, Avail.IsAutoTurret, "Turret_MiniTurret") : null;
            bool posts = f.pre || f.line.aim == "archers" || turret == null;
            if (!posts)
            {
                r.template = T("Rimfolk_Defense_TurretNest") ?? r.template;
                r.distance = r.template?.behind ?? 6;
                if (f.turretsWorking >= 2)
                {
                    r.state = StepState.Done;
                    return r;
                }
                var trouble = f.turrets.Select(DefenseLineFacts.TurretTrouble).FirstOrDefault(t => t != null);
                if (trouble != null) Band(r, 0.5f, trouble);
                else if (f.turretsWorking == 1) Band(r, 0.5f, "CC_DefLine_Guns_One".Translate());
                var p = f.BestPlan(r.template);
                if (p != null && p.Progress > r.progress) Band(r, p.Progress, "CC_DefLine_Left_Plan".Translate(p.built, p.total).Resolve());
                if (r.state == StepState.Open) Blocked(r, f);
                return r;
            }
            r.template = T("Rimfolk_Defense_Post") ?? r.template;
            if (!f.pre) r.label = "CC_DefLine_Guns_PostLabel".Translate();
            r.distance = Mathf.Max(1, Mathf.Clamp(f.coverDist + 3, 8, 13) - DefensePlanner.CoverRow(r.template));
            if (!f.pre && off != null) r.notes.Add("CC_DefLine_Guns_TurretsOff".Translate(off).Resolve());
            else if (!f.pre && turret == null && f.line.aim != "archers") r.notes.Add("CC_DefLine_Guns_TurretResearch".Translate(DefenseLineReview.ResearchFor("Turret_MiniTurret")).Resolve());
            int far = f.firing.Count(p => p.sees && Mathf.Abs(f.line.Ahead(p.cell)) >= f.coverDist + 2);
            var post = f.BestPlan(r.template);
            if (far >= 3 || post != null && post.Progress >= 0.85f)
            {
                r.state = StepState.Done;
                return r;
            }
            if (post != null && post.Progress > 0f) Band(r, post.Progress, "CC_DefLine_Left_Plan".Translate(post.built, post.total).Resolve());
            else if (far > 0) Band(r, far / 3f, "CC_DefLine_Guns_Posts".Translate(far).Resolve());
            if (r.state == StepState.Open) Blocked(r, f);
            return r;
        }
    }

    /// <summary>
    /// "The killbox": once raids are big (or at once for a line meant to be one), a killbox behind the gate, the small
    /// one, the full one or one dug into the mountain as the place allows. The planner's own trial placement says
    /// whether it fits there and how much rock or building has to go; when it doesn't, where it would.
    /// </summary>
    public class StepWorker_Killbox : DefenseStepWorker
    {
        public class Trial
        {
            public DefenseTemplateDef t;
            public bool fits;
            public IntVec3 anchor;
            public int rot;
            public bool mirror;
            public int mine, removed, waiting, cut;
            public string elsewhere;
        }

        private static readonly Dictionary<int, (DefenseSurvey s, int version, Trial trial)> trials = new Dictionary<int, (DefenseSurvey, int, Trial)>();

        public static DefenseTemplateDef KillboxFor(DefenseLineFacts f)
        {
            var full = T("Rimfolk_Defense_Killbox");
            var small = T("Rimfolk_Defense_SmallKillbox");
            var tunnel = T("Rimfolk_Defense_TunnelKillbox");
            DefenseTemplateDef pick = null;
            if (f.mountain && tunnel != null) pick = tunnel;
            else if (small != null && f.span.Count <= 5 && f.raidPoints < 1500f) pick = small;
            pick ??= full ?? small ?? tunnel;
            // One the colony can build beats the better fit it can't.
            if (pick != null && pick.UnavailableReason(f.map) != null)
                foreach (var other in new[] { full, small, tunnel })
                    if (other != null && other.UnavailableReason(f.map) == null) return other;
            return pick;
        }

        public override StepResult Evaluate(DefenseLineFacts f)
        {
            var r = Start(f);
            r.placeAt = "line";
            r.distance = 0;
            r.look = f.inner - f.outward * 4;
            r.template = KillboxFor(f);
            Explain(r, f);
            if (f.raidPoints < def.minRaidPoints && !(def.nowFor?.Contains(f.line.aim) ?? false) && !Built(f))
            {
                r.left = "CC_DefLine_Later".Translate(f.raidPoints.ToString("0"), def.minRaidPoints.ToString("0")).Resolve();
                r.state = StepState.Later;
                return r;
            }
            if (Built(f))
            {
                r.state = StepState.Done;
                return r;
            }
            if (r.template == null)
            {
                r.state = StepState.NotYet;
                return r;
            }
            if (Blocked(r, f)) return r;
            var trial = TrialFor(f, r.template);
            if (trial.fits)
            {
                var p = f.BestPlanOfKind("killbox");
                if (p != null && p.Progress > 0f) Band(r, p.Progress, "CC_DefLine_Left_Plan".Translate(p.built, p.total).Resolve());
                var costs = new List<string>();
                if (trial.mine > 0) costs.Add("CC_DefLine_Killbox_Mine".Translate(trial.mine).Resolve());
                if (trial.removed > 0) costs.Add("CC_DefLine_Killbox_Remove".Translate(trial.removed).Resolve());
                r.notes.Add(costs.Count == 0
                    ? "CC_DefLine_Killbox_Fits".Translate(r.template.LabelNow, r.template.Width, r.template.Height).Resolve()
                    : "CC_DefLine_Killbox_FitsIf".Translate(r.template.LabelNow, TextUtil.JoinAnd(costs)).Resolve());
            }
            else
            {
                r.state = StepState.NotYet;
                r.left = "CC_DefLine_Killbox_NoRoom".Translate(r.template.LabelNow, r.template.Width, r.template.Height).Resolve()
                         + (trial.elsewhere.NullOrEmpty() ? " " + "CC_DefLine_Killbox_Nowhere".Translate().Resolve() : " " + trial.elsewhere);
            }
            return r;
        }

        /// <summary>Built by plan, or by hand: plenty of cover that sees the gate, and turrets or plenty of shooters.</summary>
        public static bool Built(DefenseLineFacts f)
        {
            var p = f.BestPlanOfKind("killbox");
            if (p != null && p.Progress >= 0.85f) return true;
            return f.Closed && f.seeing >= 6 && (f.turretsWorking >= 2 || f.ranged >= 4) && f.coverAt >= 6;
        }

        private static void Explain(StepResult r, DefenseLineFacts f)
        {
            r.notes.Add("CC_DefLine_Killbox_Why1".Translate());
            r.notes.Add("CC_DefLine_Killbox_Why2".Translate());
            r.notes.Add("CC_DefLine_Killbox_Why3".Translate());
            r.notes.Add((f.s.canDrop ? "CC_DefLine_Killbox_Why4Pods" : "CC_DefLine_Killbox_Why4").Translate());
            if (f.mountain) r.notes.Add("CC_DefLine_Killbox_Tunnel".Translate());
        }

        /// <summary>The planner's trial placement on the line, kept until the survey or the line changes.</summary>
        public static Trial TrialFor(DefenseLineFacts f, DefenseTemplateDef t)
        {
            if (trials.TryGetValue(f.line.id, out var kept) && kept.s == f.s && kept.version == f.line.version && kept.trial.t == t) return kept.trial;
            var trial = new Trial { t = t };
            try
            {
                var pieces = t.ResolveCached(f.map, out _);
                var cfg = DefensePlanConfig.For(t).Copy();
                cfg.closeGap = true;
                trial.fits = DefensePlanner.FindSpotAt(t, f.map, cfg, pieces, f.gate, f.line.facing, "line", 0, true, out trial.anchor, out trial.rot, out trial.mirror);
                if (trial.fits)
                {
                    cfg.rotation = trial.rot;
                    cfg.mirror = trial.mirror;
                    var plan = DefensePlanner.Plan(t, f.map, trial.anchor, cfg, pieces, true);
                    trial.mine = plan.mine.Count;
                    trial.removed = plan.deconstruct.Count;
                    trial.waiting = plan.Count(PieceState.Waiting);
                }
                else trial.elsewhere = Elsewhere(f, t, pieces, cfg);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("defense line: killbox trial", e);
            }
            if (trials.Count > 20) trials.Clear();
            trials[f.line.id] = (f.s, f.line.version, trial);
            return trial;
        }

        /// <summary>Where else on the map it would fit: another gap raiders pass, or where the busiest way in crosses open ground.</summary>
        private static string Elsewhere(DefenseLineFacts f, DefenseTemplateDef t, List<DefensePiece> pieces, DefensePlanConfig cfg)
        {
            int tried = 0;
            foreach (var c in f.s.chokes)
            {
                if (c.cell.DistanceTo(f.gate) < 8f || c.c.share < 0.1f) continue;
                if (++tried > 2) break;
                if (DefensePlanner.FindSpotAt(t, f.map, cfg, pieces, c.cell, c.c.facing, "line", 0, true, out _, out _, out _))
                    return "CC_DefLine_Killbox_There".Translate(DefenseAdvice.Where(f.s, c.cell), f.s.GapWords(c), DefenseSurvey.Cells(c.c.width)).Resolve();
            }
            // Further back along the way in, where the ground may open out.
            var back = f.gate - f.outward * 6;
            if (back.InBounds(f.map) && DefensePlanner.FindSpotAt(t, f.map, cfg, pieces, back, f.line.facing, "line", 0, true, out _, out _, out _))
                return "CC_DefLine_Killbox_Back".Translate(6).Resolve();
            return null;
        }
    }

    /// <summary>"Harden the line": walls two thick and of stone, so breachers, sappers and fire take longer.</summary>
    public class StepWorker_Harden : DefenseStepWorker
    {
        public override StepResult Evaluate(DefenseLineFacts f)
        {
            var r = Start(f);
            r.placeAt = "line";
            r.distance = Mathf.Max(1, def.distance);
            // Embrasures have their firing positions behind them: they're never doubled.
            int n = f.parts.Count - f.embrasures;
            if (n <= 0 || !f.Closed)
            {
                r.state = StepState.NotYet;
                r.left = "CC_DefLine_Harden_NoWall".Translate();
                return r;
            }
            // Behind a palisade or a castle gate, a second wall would close the embrasures' firing positions.
            bool gatehouse = f.embrasures > 0 || f.plans.Any(p => p.def != null && p.def.kind == "palisade");
            if (gatehouse)
            {
                r.template = null;
                r.notes.Add("CC_DefLine_Harden_Gatehouse".Translate());
            }
            float thick = Mathf.Min(1f, f.thick / (float)n), wood = Mathf.Min(1f, f.wood / (float)f.parts.Count);
            if (thick >= 0.8f && wood <= 0.2f) r.state = StepState.Done;
            else
            {
                var parts = new List<string>();
                if (thick < 0.8f) parts.Add("CC_DefLine_Harden_Thin".Translate(n - f.thick, n).Resolve());
                if (wood > 0.2f) parts.Add("CC_DefLine_Harden_Wood".Translate(f.wood).Resolve());
                Band(r, Mathf.Min(0.8f, 0.6f * thick + 0.4f * (1f - wood)), string.Join(" ", parts));
            }
            var emb = Avail.BestBuildable(f.map, d => d.defName.IndexOf("Embrasure", System.StringComparison.OrdinalIgnoreCase) >= 0 || d.defName == "DankPyon_EmbPalisade",
                "DankPyon_CastleWallEmbrasures", "DankPyon_EmbPalisade");
            if (emb != null && f.embrasures == 0) r.notes.Add("CC_DefLine_Harden_Embrasures".Translate(emb.label).Resolve());
            if (r.template == null && !gatehouse && r.state != StepState.Done) r.notes.Add("CC_DefLine_Harden_ByHand".Translate());
            return r;
        }
    }

    /// <summary>"A line to fall back to": a second door between the gate and the beds, with cover behind it.</summary>
    public class StepWorker_Fallback : DefenseStepWorker
    {
        public override StepResult Evaluate(DefenseLineFacts f)
        {
            var r = Start(f);
            r.placeAt = "between";
            if (f.doorsBehind >= 1)
            {
                r.state = StepState.Done;
                return r;
            }
            var p = f.BestPlan(r.template);
            if (p != null && p.Progress > 0f) Band(r, p.Progress, "CC_DefLine_Left_Plan".Translate(p.built, p.total).Resolve());
            if (r.state == StepState.Open && !Blocked(r, f)) r.left = "CC_DefLine_Fallback_None".Translate();
            return r;
        }
    }
}
