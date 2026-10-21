using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One piece of defense advice: what to do, why, whether it's done, and the plan and place that do it.</summary>
    public class DefenseAdviceItem
    {
        public string key;
        public string title, why;
        public float score;
        public bool done;
        /// <summary>Advice with nothing to tick off (a threat to keep in mind).</summary>
        public bool standing;
        public DefenseTemplateDef template;
        /// <summary>The survey's choke point it's about (index), or -1.</summary>
        public int choke = -1;
        public IntVec3 look = IntVec3.Invalid;
        public AlmanacArticleDef article;

        public AdviceLevel Level
        {
            get
            {
                if (standing) return AdviceLevel.Idea;
                if (score >= 0.7f) return AdviceLevel.Critical;
                if (score >= 0.45f) return AdviceLevel.High;
                if (score >= 0.22f) return AdviceLevel.Medium;
                return AdviceLevel.Low;
            }
        }
    }

    /// <summary>
    /// The advice the defense page gives, worked out from the survey, ranked by what matters most now, with what's
    /// already done marked and moved to the end. Each piece says why, in the colony's own terms: the direction raids
    /// come from, how wide the gap is and what closes it, the factions that can besiege or drop pods, the cells under
    /// the mountain, the weapons by the beds.
    /// </summary>
    public static class DefenseAdvice
    {
        private static DefenseSurvey forSurvey;
        private static List<DefenseAdviceItem> items;

        /// <summary>The advice for a survey, worked out once per survey.</summary>
        public static List<DefenseAdviceItem> For(DefenseSurvey s)
        {
            if (s == null) return new List<DefenseAdviceItem>();
            if (forSurvey == s && items != null) return items;
            try
            {
                items = Build(s);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("defense advice", e);
                items = new List<DefenseAdviceItem>();
            }
            forSurvey = s;
            return items;
        }

        /// <summary>The first open piece of advice that has a plan to place (for the marshal and the wish).</summary>
        public static DefenseAdviceItem TopPlan(DefenseSurvey s) => For(s).FirstOrDefault(i => !i.done && !i.standing && i.template != null);

        private static DefenseTemplateDef T(string name) => DefDatabase<DefenseTemplateDef>.GetNamedSilentFail(name);
        private static AlmanacArticleDef A(string name) => DefDatabase<AlmanacArticleDef>.GetNamedSilentFail(name);

        private static string Factions(List<Faction> list)
        {
            var names = list.Select(f => f.Name).Where(n => !n.NullOrEmpty()).Distinct().Take(3).ToList();
            if (list.Count > 3) names.Add("CC_Def_Others".Translate());
            return TextUtil.JoinAnd(names);
        }

        /// <summary>
        /// The plan that closes a gap: a stretch of perimeter wall for a wide one, a palisade or a castle gate for a medieval
        /// colony that can raise one, else a gate with sandbag lines.
        /// </summary>
        public static DefenseTemplateDef GateFor(DefenseSurvey s, DefenseSurvey.ChokeInfo c)
        {
            var gate = T("Rimfolk_Defense_Gate");
            var wall = T("Rimfolk_Defense_Wall");
            if (c != null && c.c.width > 9 && wall != null) return wall;
            if (Avail.IsPreIndustrial && s?.map != null)
            {
                var castle = T("Rimfolk_Defense_CastleGate");
                if (castle != null && castle.UnavailableReason(s.map) == null) return castle;
                var palisade = T("Rimfolk_Defense_PalisadeGatehouse");
                if (palisade != null && palisade.UnavailableReason(s.map) == null) return palisade;
            }
            return gate ?? wall;
        }

        /// <summary>Which way a place lies from the colony's middle (a compass point).</summary>
        public static string Where(DefenseSurvey s, IntVec3 c)
        {
            var d = c - s.center;
            if (d.x == 0 && d.z == 0) return DefenseSurvey.Direction(0);
            float angle = (Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 360f) % 360f;
            return DefenseSurvey.Direction(DefenseMath.Compass(angle));
        }

        private static List<DefenseAdviceItem> Build(DefenseSurvey s)
        {
            var list = new List<DefenseAdviceItem>();
            var map = s.map;
            var r = s.result;
            bool pre = Avail.IsPreIndustrial;
            var gate = T("Rimfolk_Defense_Gate");
            var palisade = T("Rimfolk_Defense_PalisadeGatehouse");
            var castle = T("Rimfolk_Defense_CastleGate");
            var wall = T("Rimfolk_Defense_Wall");
            // A medieval colony that can raise a palisade or a castle wall gets that instead of a plain gate.
            DefenseTemplateDef gateNow = gate;
            if (pre && castle != null && castle.UnavailableReason(map) == null) gateNow = castle;
            else if (pre && palisade != null && palisade.UnavailableReason(map) == null) gateNow = palisade;

            // ---------- Where they come from, and the gaps ----------
            if (r != null && !r.unreachable)
            {
                for (int i = 0; i < s.chokes.Count && i < 3; i++)
                {
                    var c = s.chokes[i];
                    // A gap a line holds already is that line's: its card says what's next there.
                    var holder = s.CoveringLine(c);
                    string heldBy = holder != null ? " " + "CC_Def_Adv_CoveredBy".Translate(DefenseLineReview.NameOf(holder, map)).Resolve() : "";
                    // The busiest gap always; the others when a fair share of raids pass them.
                    if (i > 0 && c.c.share < 0.2f) continue;
                    string dir = Where(s, c.cell);
                    string came = c.c.share >= 0.9f ? "CC_Def_Came_All".Translate().ToString()
                        : c.c.share >= 0.55f ? "CC_Def_Came_Most".Translate(DefenseSurvey.FacingWord(c.c.facing), Mathf.RoundToInt(c.c.share * 10f)).ToString()
                        : "CC_Def_Came_Some".Translate(Mathf.Max(1, Mathf.RoundToInt(c.c.share * 10f)), DefenseSurvey.FacingWord(c.c.facing)).ToString();
                    if (!c.Held)
                    {
                        bool wide = c.c.width > 9;
                        var tpl = wide ? wall : gateNow;
                        string plan = c.c.bridge ? "CC_Def_Plan_Bridge".Translate()
                            : wide ? "CC_Def_Plan_Wall".Translate()
                            : tpl == palisade || tpl == castle ? "CC_Def_Plan_Palisade".Translate()
                            : "CC_Def_Plan_Gate".Translate();
                        list.Add(new DefenseAdviceItem
                        {
                            key = "gap" + i,
                            title = "CC_Def_Adv_Gap_Title".Translate(dir),
                            why = came + " " + "CC_Def_Gap_Line".Translate(s.GapWords(c), DefenseSurvey.Cells(c.c.width)) + " " + plan + heldBy,
                            score = 0.3f + 0.6f * c.c.share,
                            template = tpl,
                            choke = i,
                            look = holder?.gate ?? c.cell,
                            article = A("Rimfolk_AlmDef_ChokePoints")
                        });
                    }
                    else
                    {
                        // A gate stands in the gap already: is there cover behind it?
                        list.Add(new DefenseAdviceItem
                        {
                            key = "cover" + i,
                            title = "CC_Def_Adv_Cover_Title".Translate(dir),
                            why = came + " " + "CC_Def_Adv_Cover".Translate(dir, c.coverInside) + heldBy,
                            score = 0.25f + 0.4f * c.c.share,
                            done = c.coverInside >= 3,
                            template = gateNow,
                            choke = i,
                            look = holder?.gate ?? c.cell,
                            article = A("Rimfolk_AlmDef_Cover")
                        });
                        if (c.woodWalls >= 3 && gateNow != palisade)
                            list.Add(new DefenseAdviceItem
                            {
                                key = "wood" + i,
                                title = "CC_Def_Adv_Wood_Title".Translate(),
                                why = "CC_Def_Adv_Wood".Translate(dir),
                                score = 0.3f,
                                look = c.cell,
                                article = A("Rimfolk_AlmDef_WallsGates")
                            });
                    }
                }

                // Open country: no gap to hold, so make one.
                bool openCountry = !r.enclosed && (s.chokes.Count == 0 || r.openShare >= 0.5f);
                if (openCountry || r.enclosed)
                {
                    int sides = r.sideReach.Count(n => n > 0);
                    list.Add(new DefenseAdviceItem
                    {
                        key = "wall",
                        title = "CC_Def_Adv_Wall_Title".Translate(),
                        why = r.enclosed ? "CC_Def_Adv_WallDone".Translate().ToString() : "CC_Def_Adv_Wall".Translate(sides, Mathf.Max(1, r.openings)).ToString(),
                        score = 0.6f,
                        done = r.enclosed,
                        template = wall,
                        look = s.approach,
                        article = A("Rimfolk_AlmDef_WallsGates")
                    });
                }

                var main = s.Main;
                bool held = r.enclosed || (main != null && main.Held);
                string mainDir = main != null ? Where(s, main.cell) : s.approach.IsValid ? Where(s, s.approach) : DefenseSurvey.Direction(DefenseMath.Compass(r.angle));

                // A killbox once raids are big.
                if (held && s.raidPoints >= 700f)
                {
                    bool strong = main != null && main.coverInside >= 6 && (main.turretsNear >= 2 || s.ranged >= 4);
                    list.Add(new DefenseAdviceItem
                    {
                        key = "killbox",
                        title = "CC_Def_Adv_Killbox_Title".Translate(),
                        why = "CC_Def_Adv_Killbox".Translate(s.raidPoints.ToString("0")),
                        score = 0.3f + (s.raidPoints >= 2000f ? 0.15f : 0f),
                        done = strong,
                        template = T("Rimfolk_Defense_Killbox"),
                        choke = main != null ? 0 : -1,
                        look = main?.cell ?? IntVec3.Invalid,
                        article = A("Rimfolk_AlmDef_Killbox")
                    });
                }

                // Traps on the way in.
                var trap = Avail.BestBuildable(map, Avail.IsTrap, "TrapSpike");
                if (trap != null && DefenseTemplateDef.RoleOff("trap") == null && (main != null || s.approach.IsValid))
                {
                    int near = main?.trapsOutside ?? 0;
                    list.Add(new DefenseAdviceItem
                    {
                        key = "traps",
                        title = "CC_Def_Adv_Traps_Title".Translate(),
                        why = "CC_Def_Adv_Traps".Translate(mainDir, trap.label),
                        score = 0.28f,
                        done = near >= 3,
                        template = T("Rimfolk_Defense_TrapCorridor"),
                        choke = main != null ? 0 : -1,
                        look = main?.cell ?? s.approach,
                        article = A("Rimfolk_AlmDef_Traps")
                    });
                }

                // Turrets behind the gate, where the colony can build them.
                var turret = Avail.BestBuildable(map, Avail.IsAutoTurret, "Turret_MiniTurret");
                if (!pre && turret != null && held && DefenseTemplateDef.RoleOff("turret") == null)
                    list.Add(new DefenseAdviceItem
                    {
                        key = "turrets",
                        title = "CC_Def_Adv_Turrets_Title".Translate(),
                        why = "CC_Def_Adv_Turrets".Translate(mainDir),
                        score = 0.27f,
                        done = main != null && main.turretsNear >= 2,
                        template = T("Rimfolk_Defense_TurretNest"),
                        choke = main != null ? 0 : -1,
                        look = main?.cell ?? IntVec3.Invalid,
                        article = A("Rimfolk_AlmDef_Turrets")
                    });
                // An archers' post for a colony of bows.
                if ((pre || s.bows) && held)
                    list.Add(new DefenseAdviceItem
                    {
                        key = "post",
                        title = "CC_Def_Adv_Post_Title".Translate(),
                        why = "CC_Def_Adv_Post".Translate(mainDir, s.ranged),
                        score = 0.27f,
                        done = main != null && main.coverInside >= 6,
                        template = T("Rimfolk_Defense_Post"),
                        choke = main != null ? 0 : -1,
                        look = main?.cell ?? IntVec3.Invalid,
                        article = A("Rimfolk_AlmDef_Medieval")
                    });

                // Sappers and breachers go round a held gate.
                if (s.canBreach && held)
                    list.Add(new DefenseAdviceItem
                    {
                        key = "sappers",
                        title = "CC_Def_Adv_Sappers_Title".Translate(),
                        why = "CC_Def_Adv_Sappers".Translate(Mathf.RoundToInt(s.doubleWalls * 100f)).Resolve() + (s.tribal ? " " + "CC_Def_Adv_SappersTribal".Translate().Resolve() : ""),
                        score = 0.38f + (s.raidPoints >= 1000f ? 0.1f : 0f),
                        done = s.doubleWalls >= 0.6f || s.doorsOnWay >= 2,
                        template = wall,
                        article = A("Rimfolk_AlmDef_Killbox")
                    });

                // A line to fall back to.
                if (held)
                    list.Add(new DefenseAdviceItem
                    {
                        key = "fallback",
                        title = "CC_Def_Adv_Fallback_Title".Translate(),
                        why = "CC_Def_Adv_Fallback".Translate(),
                        score = 0.3f,
                        done = s.doorsOnWay >= 2,
                        template = T("Rimfolk_Defense_Fallback"),
                        choke = main != null ? 0 : -1,
                        article = A("Rimfolk_AlmDef_Fallback")
                    });
            }
            else if (r != null)
            {
                list.Add(new DefenseAdviceItem
                {
                    key = "noway",
                    title = "CC_Def_Adv_NoWay_Title".Translate(),
                    why = "CC_Def_Adv_NoWay".Translate(),
                    standing = true,
                    score = 0.2f,
                    article = A("Rimfolk_AlmDef_DropPods")
                });
            }

            // ---------- Threats that ignore the gate ----------
            if (s.canDrop)
            {
                var droppers = s.hostiles.Where(f => f.def.techLevel >= TechLevel.Industrial).ToList();
                list.Add(new DefenseAdviceItem
                {
                    key = "pods",
                    title = "CC_Def_Adv_Pods_Title".Translate(),
                    why = "CC_Def_Adv_Pods".Translate(Factions(droppers), s.weaponsNearBeds),
                    score = 0.45f,
                    done = s.bedCenter.IsValid && s.weaponsNearBeds >= Mathf.Max(2, s.fighters / 2),
                    template = T("Rimfolk_Defense_Keep"),
                    look = s.bedCenter,
                    article = A("Rimfolk_AlmDef_DropPods")
                });
            }
            if (s.thickRoof >= 40)
                list.Add(new DefenseAdviceItem
                {
                    key = "insects",
                    title = "CC_Def_Adv_Insects_Title".Translate(),
                    why = "CC_Def_Adv_Insects".Translate(s.thickRoof, s.darkWarm),
                    score = 0.35f + Mathf.Min(0.2f, s.darkWarm / 1000f),
                    done = s.darkWarm < 20,
                    template = T("Rimfolk_Defense_Keep"),
                    article = A("Rimfolk_AlmDef_Infestations")
                });
            if (s.canSiege)
            {
                var mortar = Avail.BestBuildable(map, DefenseTemplateDef.IsMortar, "Turret_Mortar");
                var pit = T("Rimfolk_Defense_MortarPit");
                var besiegers = s.hostiles.Where(f => f.def.canSiege).ToList();
                string how = mortar != null ? "CC_Def_Adv_Siege_Mortar".Translate().ToString()
                    : "CC_Def_Adv_Siege_NoMortar".Translate(pit?.UnavailableReason(map) ?? "").ToString();
                list.Add(new DefenseAdviceItem
                {
                    key = "siege",
                    title = "CC_Def_Adv_Siege_Title".Translate(),
                    why = "CC_Def_Adv_Siege".Translate(Factions(besiegers)) + " " + how,
                    score = 0.4f,
                    done = s.mortars >= 1,
                    template = pit,
                    article = A("Rimfolk_AlmDef_Sieges")
                });
            }
            if (s.mechanoids && s.mechsYet)
                list.Add(new DefenseAdviceItem
                {
                    key = "mechs",
                    title = "CC_Def_Adv_Mechs_Title".Translate(),
                    why = (pre ? "CC_Def_Adv_MechsLow" : "CC_Def_Adv_Mechs").Translate(),
                    score = 0.38f,
                    done = !pre && s.emp,
                    standing = pre,
                    article = A("Rimfolk_AlmDef_Mechanoids")
                });
            if (s.mechClusters)
                list.Add(new DefenseAdviceItem
                {
                    key = "cluster",
                    title = "CC_Def_Adv_Cluster_Title".Translate(),
                    why = "CC_Def_Adv_Cluster".Translate(),
                    standing = true,
                    score = 0.2f,
                    article = A("Rimfolk_AlmDef_Mechanoids")
                });
            if (s.anomaly)
                list.Add(new DefenseAdviceItem
                {
                    key = "anomaly",
                    title = "CC_Def_Adv_Anomaly_Title".Translate(),
                    why = "CC_Def_Adv_Anomaly".Translate(),
                    standing = true,
                    score = 0.25f,
                    article = A("Rimfolk_AlmDef_Anomaly")
                });
            if (s.wastepacks > 0)
                list.Add(new DefenseAdviceItem
                {
                    key = "wastepacks",
                    title = "CC_Def_Adv_Waste_Title".Translate(),
                    why = "CC_Def_Adv_Waste".Translate(s.wastepacks),
                    score = 0.3f,
                    article = A("Rimfolk_AlmDef_Biotech")
                });

            // ---------- People ----------
            if (s.assess.ratio < 1f)
                list.Add(new DefenseAdviceItem
                {
                    key = "fighters",
                    title = "CC_Def_Adv_Fighters_Title".Translate(),
                    why = "CC_Def_Adv_Fighters".Translate(s.raidPoints.ToString("0"), s.fighters, s.assess.defenseScore.ToString("0")),
                    score = 0.35f + 0.5f * (1f - s.assess.ratio),
                    article = A("Council_Alm_3_01_RaidPoints")
                });
            if (s.fighters > 0)
            {
                bool ok = s.hospital && s.medicine >= s.fighters;
                list.Add(new DefenseAdviceItem
                {
                    key = "medical",
                    title = "CC_Def_Adv_Medical_Title".Translate(),
                    why = "CC_Def_Adv_Medical".Translate(s.medicine).Resolve() + (s.hospital ? "" : " " + "CC_Def_Adv_NoHospital".Translate().Resolve()),
                    score = 0.32f,
                    done = ok,
                    article = A("Rimfolk_AlmDef_Fallback")
                });
            }
            if (s.trapsInHome > 0)
                list.Add(new DefenseAdviceItem
                {
                    key = "traphome",
                    title = "CC_Def_Adv_TrapHome_Title".Translate(),
                    why = "CC_Def_Adv_TrapHome".Translate(s.trapsInHome),
                    score = 0.15f,
                    standing = true,
                    article = A("Rimfolk_AlmDef_Traps")
                });

            // Open first, by what matters most; then what's done.
            return list.Where(i => !i.done && !i.standing).OrderByDescending(i => i.score)
                .Concat(list.Where(i => i.standing && !i.done).OrderByDescending(i => i.score))
                .Concat(list.Where(i => i.done)).ToList();
        }
    }
}
