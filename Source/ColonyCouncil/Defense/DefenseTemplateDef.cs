using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>The buildings a defense template prefers for one of its roles, best first, and whether it can do without them.</summary>
    public class DefensePieceSpec
    {
        public string role;
        /// <summary>Preferred defNames, best first; looked up at runtime, so a mod that's missing is simply skipped.</summary>
        public List<string> defs;
        /// <summary>The plan can do without it (shelves and fences always can).</summary>
        public bool optional;
    }

    /// <summary>One building of a resolved defense plan, in template coordinates.</summary>
    public class DefensePiece
    {
        public string role;
        public ThingDef def;
        public ThingDef stuff;
        /// <summary>Its footprint in template cells.</summary>
        public CellRect rect;
        /// <summary>Facing in the template: a gate faces north in a wall running east to west, east in one running north to south.</summary>
        public int facing;
    }

    /// <summary>
    /// A defense the colony can place like a room plan: a gate in a wall line with sandbags, a killbox, a trap corridor, a
    /// turret nest, a shooting post, a stretch of perimeter wall, a fallback line, a keep, a mortar pit, a palisade with a
    /// gatehouse. The layout is a grid of characters (<see cref="DefenseLayout"/>): drawn for the colony's own numbers
    /// when the template has a <see cref="shape"/> (<see cref="DefenseShapes"/>), else the fixed one in the XML. Each role
    /// takes the first building the colony can build from the template's list, then from the role's own defaults, then
    /// anything that does the job and fits, so a medieval colony gets barricades and log gates where an industrial one gets
    /// sandbags and steel doors.
    /// </summary>
    public class DefenseTemplateDef : Def
    {
        /// <summary>gate, wall, killbox, traps, nest, post, fallback, keep, mortar, palisade.</summary>
        public string kind = "gate";
        public int order = 100;
        /// <summary>The wall line (row from the top) that "close the gap" carries on to either side, and that sits on a choke point; -1 for none.</summary>
        public int line = -1;
        /// <summary>
        /// Where the planner suggests it: "choke" (its line on the gap), "front" (in front of the gap, outside), "behind"
        /// (<see cref="behind"/> cells inside), "between" (between the gap and the beds), "core" (among the beds, or deep inside for mortars).
        /// </summary>
        public string placeAt = "choke";
        public int behind = 8;
        /// <summary>"lowtech": only for tribal and medieval colonies; "industrial": only once past that.</summary>
        public string era;
        /// <summary>The fixed layout. With a <see cref="shape"/>, only the plan of what was placed before layouts were drawn.</summary>
        public List<string> layout = new List<string>();
        /// <summary>Drawn from the colony's numbers (<see cref="DefenseShapes"/>) instead of the fixed layout.</summary>
        public DefenseShapeOptions shape;
        public List<DefensePieceSpec> pieces;
        /// <summary>Buildings one of which the colony must be able to build (the palisade, the castle wall): else the plan is greyed.</summary>
        public List<string> needs;
        [MustTranslate] public string labelLowTech;
        [MustTranslate] public string purpose;
        [MustTranslate] public List<string> tips;
        public List<string> iconDefs;

        public static readonly string[] Kinds = { "gate", "wall", "killbox", "traps", "nest", "post", "fallback", "keep", "mortar", "palisade" };
        public static readonly string[] Places = { "choke", "front", "behind", "between", "core" };

        [Unsaved] private DefenseLayout.Grid grid;
        [Unsaved] private readonly Dictionary<string, DefenseShape> drawn = new Dictionary<string, DefenseShape>();

        /// <summary>The fixed layout from the XML.</summary>
        public DefenseLayout.Grid LayoutGrid => grid ??= DefenseLayout.Parse(layout, line);

        /// <summary>Drawn for the colony instead of fixed.</summary>
        public bool Drawn => shape != null;

        /// <summary>It has a wall line that sits on the gap and can be carried on to the rock.</summary>
        public bool HasLine => line >= 0;

        /// <summary>The layout for a plain colony (four shooters, a mini-turret, rifles), or the fixed one: for what any of its layouts has.</summary>
        public DefenseLayout.Grid SampleGrid => GridFor(Sample);

        private static readonly DefenseShapeSpec Sample = new DefenseShapeSpec { turretSize = 1, turrets = 2 };

        /// <summary>The layout drawn for <paramref name="spec"/> (kept, as the same numbers draw the same layout), or null for a fixed one.</summary>
        public DefenseShape ShapeFor(DefenseShapeSpec spec)
        {
            if (shape == null || spec == null) return null;
            string key = spec.Key;
            if (!drawn.TryGetValue(key, out var made))
            {
                if (drawn.Count > 48) drawn.Clear();
                made = DefenseShapes.Build(shape, spec);
                drawn[key] = made;
            }
            return made;
        }

        /// <summary>The layout for these numbers: drawn, or the fixed one (no spec: a plan placed before layouts were drawn).</summary>
        public DefenseLayout.Grid GridFor(DefenseShapeSpec spec) => ShapeFor(spec)?.grid ?? LayoutGrid;

        /// <summary>The layout the planner uses with these choices.</summary>
        public DefenseLayout.Grid GridFor(Map map, DefensePlanConfig cfg) => GridFor(SpecFor(map, cfg));

        /// <summary>How far behind the gate (or in front, for a trap corridor) it goes when drawn for these numbers.</summary>
        public int BehindFor(DefenseShapeSpec spec)
        {
            var made = ShapeFor(spec);
            return made != null && made.behind > 0 ? made.behind : behind;
        }

        /// <summary>
        /// The numbers a drawn layout is drawn for: a placed plan's own (<see cref="DefensePlanConfig.spec"/>), else the
        /// colony's, with the shooters and depth the player added in the planner. Null for a fixed layout, and for a plan
        /// placed before layouts were drawn.
        /// </summary>
        public DefenseShapeSpec SpecFor(Map map, DefensePlanConfig cfg)
        {
            if (shape == null || cfg != null && cfg.fixedLayout) return null;
            if (cfg?.spec != null) return cfg.spec;
            var spec = ColonySpec(map, cfg);
            if (cfg != null && (cfg.moreShooters != 0 || cfg.moreDepth != 0))
            {
                spec = spec.Copy();
                spec.shooters = Mathf.Max(2, spec.shooters + cfg.moreShooters);
                spec.depth += cfg.moreDepth;
            }
            return spec;
        }

        [Unsaved] private Map specMap;
        [Unsaved] private float specTime = -999f;
        [Unsaved] private ThingDef specTurret;
        [Unsaved] private DefenseShapeSpec specKept;
        private static bool readingSurvey;

        /// <summary>
        /// The colony's own numbers: its ranged fighters (four at least) and the longest reach among them, the turret this
        /// plan would get (the player's pick, else the first it can build), whether the raiders it meets throw grenades
        /// (any hostile people past the tribes), bows and spears, and the size of raids now. Worked out again every few seconds.
        /// </summary>
        public DefenseShapeSpec ColonySpec(Map map, DefensePlanConfig cfg)
        {
            ThingDef chosen = null;
            if (cfg != null) cfg.variant.TryGetValue("turret", out chosen);
            float now = Time.realtimeSinceStartup;
            if (specKept != null && specMap == map && specTurret == chosen && now - specTime < 3f) return specKept;
            var spec = new DefenseShapeSpec();
            try
            {
                // A survey being made never asks a plan for its numbers; if one ever did, it gets the kept survey only.
                var s = map == null ? null : DefenseSurvey.Kept(map);
                if (s == null && map != null && !readingSurvey)
                {
                    readingSurvey = true;
                    try
                    {
                        s = DefenseSurvey.For(map);
                    }
                    finally
                    {
                        readingSurvey = false;
                    }
                }
                spec.lowTech = Avail.IsPreIndustrial || s != null && s.bows;
                spec.range = spec.lowTech ? 22.9f : 30.9f;
                if (s != null)
                {
                    spec.shooters = Mathf.Max(4, s.ranged);
                    if (s.bestRange > 1f) spec.range = s.bestRange;
                    spec.grenades = s.industrial || s.empire;
                    spec.raidPoints = s.raidPoints;
                }
                var turret = map != null ? Pick("turret", map, 2, 2, cfg) : null;
                // A turret someone has to man is no decoy out in a killbox: its gunner would stand in the kill zone.
                if (turret != null && shape?.family == DefenseShapes.Box && turret.HasComp(typeof(CompMannable))) turret = null;
                if (turret != null)
                {
                    spec.turretSize = turret.size.x >= 2 || turret.size.z >= 2 ? 2 : 1;
                    spec.turrets = spec.turretSize >= 2 ? 1 : 2;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("defense plan: the colony's numbers", e);
            }
            specKept = spec;
            specMap = map;
            specTurret = chosen;
            specTime = now;
            return spec;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (!Kinds.Contains(kind)) yield return "unknown kind " + kind;
            if (!Places.Contains(placeAt)) yield return "unknown placeAt " + placeAt;
            if (!era.NullOrEmpty() && era != "lowtech" && era != "industrial") yield return "era must be lowtech or industrial";
            if (pieces != null)
                foreach (var p in pieces)
                    if (p == null || !DefenseLayout.Roles.Contains(p.role)) yield return "unknown piece role " + p?.role;
            if (shape == null || layout.Count > 0)
                foreach (var problem in DefenseLayout.Problems(LayoutGrid, kind)) yield return "layout: " + problem;
            if (shape != null)
            {
                if (!DefenseShapes.Families.Contains(shape.family)) yield return "unknown shape family " + shape.family;
                else
                    foreach (var problem in DefenseLayout.Problems(ShapeFor(new DefenseShapeSpec()).grid, kind)) yield return "drawn layout: " + problem;
            }
        }

        /// <summary>The name for this colony: an archers' post in a colony of bows, a sniper's post in one of rifles.</summary>
        public string LabelNow => Avail.IsPreIndustrial && !labelLowTech.NullOrEmpty() ? labelLowTech : label;

        public string LabelCapNow => LabelNow.CapitalizeFirst();

        public bool EraFits
        {
            get
            {
                if (era.NullOrEmpty()) return true;
                return era == "lowtech" ? Avail.IsPreIndustrial : !Avail.IsPreIndustrial;
            }
        }

        public DefensePieceSpec Spec(string role) => pieces?.FirstOrDefault(p => p != null && p.role == role);

        public bool Optional(string role) => (Spec(role)?.optional ?? false) || role == "shelf" || role == "slow";

        /// <summary>The roles a layout uses, in the planner's order.</summary>
        public static List<string> RolesUsed(DefenseLayout.Grid g) => DefenseLayout.Roles.Where(r => g.pieces.Any(p => p.role == r)).ToList();

        // ---------------- Which building for which role ----------------

        /// <summary>The role's own first choices, after the template's.</summary>
        public static IEnumerable<string> DefaultDefs(string role)
        {
            switch (role)
            {
                case "wall": return new[] { "Wall" };
                case "gate": return new[] { "Door" };
                case "embrasure": return new[] { "DankPyon_CastleWallEmbrasures", "DankPyon_EmbPalisade" };
                case "cover": return Avail.IsPreIndustrial ? new[] { "Barricade", "Sandbags" } : new[] { "Sandbags", "Barricade" };
                case "slow": return new[] { "Fence", "Sandbags", "Barricade" };
                case "trap": return new[] { "TrapSpike" };
                case "turret": return new[] { "Turret_MiniTurret", "DankPyon_Turret_Scorpio", "Turret_Autocannon", "DankPyon_Turret_Ballista", "VFES_Turret_Ballista" };
                case "mortar": return new[] { "Turret_Mortar" };
                case "shelf": return new[] { "ShelfSmall" };
                default: return new string[0];
            }
        }

        public static bool IsMortar(ThingDef d)
        {
            try
            {
                return d?.building != null && d.building.turretGunDef != null && d.building.IsMortar;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsTurret(ThingDef d) => d?.thingClass != null && typeof(Building_Turret).IsAssignableFrom(d.thingClass) && !IsMortar(d);

        private static bool IsEmbrasure(ThingDef d) =>
            d?.building != null && d.passability == Traversability.Impassable && d.fillPercent >= 0.5f && d.fillPercent < 0.99f && !d.IsDoor
            && (d.defName.IndexOf("Embrasure", StringComparison.OrdinalIgnoreCase) >= 0 || d.defName.IndexOf("EmbPalisade", StringComparison.OrdinalIgnoreCase) >= 0);

        private static bool IsShelf(ThingDef d) =>
            d?.thingClass != null && typeof(Building_Storage).IsAssignableFrom(d.thingClass) && d.building?.fixedStorageSettings == null;

        /// <summary>
        /// Whether a building can take the role in a block of <paramref name="w"/> x <paramref name="h"/> cells. Named in the
        /// template's own list (<paramref name="listed"/>), a building only has to have the right shape; found by purpose, it
        /// has to be what the role says.
        /// </summary>
        public static bool FitsRole(ThingDef d, string role, int w, int h, bool listed)
        {
            if (d == null || d.category != ThingCategory.Building || d.size.x > w || d.size.z > h) return false;
            bool single = d.size.x == 1 && d.size.z == 1;
            switch (role)
            {
                case "wall": return single && d.passability == Traversability.Impassable && !d.IsDoor && (listed || (d.IsWall && d.MadeFromStuff));
                case "gate": return single && d.IsDoor;
                case "embrasure": return single && (listed ? d.passability == Traversability.Impassable : IsEmbrasure(d));
                case "cover": return single && (listed ? d.passability == Traversability.PassThroughOnly : Avail.IsCover(d));
                case "slow":
                    return single && d.passability == Traversability.PassThroughOnly && !d.IsDoor && !Avail.IsTrap(d)
                           && (listed || Avail.IsCover(d) || d.defName.IndexOf("Fence", StringComparison.OrdinalIgnoreCase) >= 0);
                case "trap":
                    // IEDs go off when shot and take walls with them: only when the template or the player names one.
                    return single && Avail.IsTrap(d) && (listed || !typeof(Building_TrapExplosive).IsAssignableFrom(d.thingClass));
                case "turret": return IsTurret(d);
                case "mortar": return IsMortar(d);
                case "shelf": return single && IsShelf(d);
                default: return false;
            }
        }

        /// <summary>Turrets, traps and mortars can be switched off for the player in the game's difficulty settings.</summary>
        public static string RoleOff(string role)
        {
            var diff = Find.Storyteller?.difficulty;
            if (diff == null) return null;
            if (role == "trap" && !diff.allowTraps) return "CC_Def_Off_Traps".Translate();
            if (role == "turret" && !diff.allowTurrets) return "CC_Def_Off_Turrets".Translate();
            if (role == "mortar" && !diff.allowMortars) return "CC_Def_Off_Mortars".Translate();
            return null;
        }

        /// <summary>The preferred buildings for a role: the template's, then the role's defaults, each once.</summary>
        public List<ThingDef> Preferred(string role)
        {
            var names = new List<string>();
            var spec = Spec(role);
            if (spec?.defs != null) names.AddRange(spec.defs);
            // A template that names its own (the palisade's) keeps to them.
            if (spec?.defs == null || spec.defs.Count == 0) names.AddRange(DefaultDefs(role));
            var list = new List<ThingDef>();
            foreach (var n in names)
            {
                var d = DefDatabase<ThingDef>.GetNamedSilentFail(n);
                if (d != null && !list.Contains(d)) list.Add(d);
            }
            return list;
        }

        /// <summary>The building a role uses in this colony: the player's pick, the first preferred one it can build, else the cheapest that does the job.</summary>
        public ThingDef Pick(string role, Map map, int w, int h, DefensePlanConfig cfg)
        {
            if (RoleOff(role) != null) return null;
            if (cfg != null && cfg.variant.TryGetValue(role, out var chosen) && chosen != null && Avail.CanBuild(chosen, map) && FitsRole(chosen, role, w, h, true))
                return chosen;
            var spec = Spec(role);
            foreach (var d in Preferred(role))
                if (Avail.CanBuild(d, map) && FitsRole(d, role, w, h, true)) return d;
            // A template with its own list (the palisade) doesn't fall back to anything else.
            if (spec?.defs != null && spec.defs.Count > 0) return null;
            return Avail.BestBuildable(map, d => FitsRole(d, role, w, h, false));
        }

        /// <summary>Everything the colony could build for a role here: the preferred ones and anything that does the job and fits.</summary>
        public List<ThingDef> VariantsFor(string role, Map map, int w, int h)
        {
            var result = new List<ThingDef>();
            if (RoleOff(role) != null) return result;
            foreach (var d in Preferred(role))
                if (!result.Contains(d) && Avail.CanBuild(d, map) && FitsRole(d, role, w, h, true)) result.Add(d);
            var spec = Spec(role);
            if (spec?.defs != null && spec.defs.Count > 0) return result;
            foreach (var d in Avail.AllBuildings)
                if (!result.Contains(d) && FitsRole(d, role, w, h, role == "trap") && Avail.CanBuild(d, map)) result.Add(d);
            return result;
        }

        /// <summary>
        /// Picks the colony's buildings and materials for every piece. Roles it can't fill are named in
        /// <paramref name="missing"/> (with the research they wait on) unless the template can do without them; those
        /// cells are then simply kept clear.
        /// </summary>
        public List<DefensePiece> Resolve(Map map, out List<string> missing, DefensePlanConfig cfg = null) => Resolve(map, out missing, cfg, SpecFor(map, cfg));

        private List<DefensePiece> Resolve(Map map, out List<string> missing, DefensePlanConfig cfg, DefenseShapeSpec spec)
        {
            missing = new List<string>();
            var list = new List<DefensePiece>();
            var g = GridFor(spec);
            var reserved = new Dictionary<ThingDef, int>();
            var chosen = new Dictionary<(string, int, int), ThingDef>();
            var stuffFor = new Dictionary<ThingDef, ThingDef>();
            var counts = new Dictionary<string, int>();
            foreach (var p in g.pieces) counts[p.role] = (counts.TryGetValue(p.role, out int n) ? n : 0) + 1;
            var said = new HashSet<string>();
            ThingDef wallStuff = null;
            // Role by role, walls first, so gates and embrasures can match the walls.
            foreach (var p in DefenseLayout.Roles.SelectMany(r => g.pieces.Where(x => x.role == r)))
            {
                // Planned without (a corridor with its sides left open): that ground is left as it is.
                if (cfg != null && cfg.leaveOut.Contains(p.role)) continue;
                if (!chosen.TryGetValue((p.role, p.w, p.h), out var def))
                {
                    def = Pick(p.role, map, p.w, p.h, cfg);
                    chosen[(p.role, p.w, p.h)] = def;
                }
                if (def == null)
                {
                    if (!Optional(p.role) && said.Add(p.role)) missing.Add(MissingText(p.role, map, p.w, p.h));
                    continue;
                }
                if (!stuffFor.TryGetValue(def, out var stuff))
                {
                    stuff = StuffFor(p.role, def, map, cfg, counts[p.role], reserved, wallStuff);
                    stuffFor[def] = stuff;
                    MaterialPicker.Reserve(reserved, def, stuff, counts[p.role]);
                }
                if (p.role == "wall" && wallStuff == null) wallStuff = stuff;
                list.Add(new DefensePiece
                {
                    role = p.role,
                    def = def,
                    stuff = stuff,
                    rect = new CellRect(p.x, p.z, def.size.x, def.size.z),
                    facing = p.role == "gate" && DefenseLayout.InLine(g.At(p.x, p.z + 1)) && DefenseLayout.InLine(g.At(p.x, p.z - 1)) ? 1 : 0
                });
            }
            return list;
        }

        /// <summary>The wall material use for walls and embrasures, doors for gates, the rest is furniture.</summary>
        public static MaterialUse UseOf(string role) => role == "wall" || role == "embrasure" ? MaterialUse.Wall : role == "gate" ? MaterialUse.Door : MaterialUse.Furniture;

        private static ThingDef StuffFor(string role, ThingDef def, Map map, DefensePlanConfig cfg, int units, Dictionary<ThingDef, int> reserved, ThingDef walls)
        {
            if (!def.MadeFromStuff) return null;
            var use = UseOf(role);
            if (cfg != null && cfg.stuff.TryGetValue(role, out var s) && s != null && Avail.StuffAllowed(def, s) && MaterialPicker.Allowed(s, use)
                && (cfg.pickedNow.Contains(role) || MaterialPicker.Affordable(def, s, units, map)))
                return s;
            // Gates and embrasures match the walls, when there's enough for them too.
            if ((role == "gate" || role == "embrasure") && walls != null && Avail.StuffAllowed(def, walls) && MaterialPicker.Allowed(walls, use)
                && MaterialPicker.Affordable(def, walls, units, map))
                return walls;
            return MaterialPicker.Pick(def, map, units, use, reserved);
        }

        /// <summary>"sandbags or barricades (research: ...)": what the colony lacks for a role, and why.</summary>
        private string MissingText(string role, Map map, int w, int h)
        {
            string off = RoleOff(role);
            if (off != null) return off;
            var first = Preferred(role).FirstOrDefault(d => FitsRole(d, role, w, h, true));
            if (first == null) return ("CC_DefRole_" + role).Translate().Resolve();
            var research = first.researchPrerequisites?.Where(r => r != null && !r.IsFinished).Select(r => r.label).ToList();
            if (research != null && research.Count > 0)
                return "CC_Def_MissingResearch".Translate(first.label, TextUtil.JoinAnd(research)).Resolve();
            return first.label;
        }

        private class Resolved
        {
            public Map map;
            public float time;
            public List<DefensePiece> pieces;
            public string reason;
        }

        [Unsaved] private readonly Dictionary<string, Resolved> resolvedKept = new Dictionary<string, Resolved>();

        /// <summary>
        /// Resolve() for the interface and the line reviews, remembered for a couple of seconds for each layout: the
        /// colony's now, or the one <paramref name="cfg"/> asks for (a placed plan's own). The pieces' choices of building
        /// and material are the colony's, not <paramref name="cfg"/>'s.
        /// </summary>
        public List<DefensePiece> ResolveCached(Map map, out string unavailableReason, DefensePlanConfig cfg = null)
        {
            var spec = SpecFor(map, cfg);
            string key = spec?.Key ?? "";
            float now = Time.realtimeSinceStartup;
            if (!resolvedKept.TryGetValue(key, out var kept) || kept.map != map || kept.pieces == null || now - kept.time > 2f + (shortHash % 64) / 64f)
            {
                if (kept == null)
                {
                    if (resolvedKept.Count > 16) resolvedKept.Clear();
                    kept = new Resolved();
                    resolvedKept[key] = kept;
                }
                kept.map = map;
                kept.time = now;
                kept.reason = null;
                kept.pieces = new List<DefensePiece>();
                try
                {
                    kept.reason = NeedsReason(map);
                    if (kept.reason == null)
                    {
                        kept.pieces = Resolve(map, out var missing, null, spec);
                        if (missing.Count > 0) kept.reason = "CC_Def_Missing".Translate(TextUtil.JoinAnd(missing)).Resolve();
                    }
                    if (kept.reason == null && !EraFits)
                        kept.reason = (era == "lowtech" ? "CC_Template_ForLowTech" : "CC_Template_ForIndustrial").Translate().Resolve();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("defense plan " + defName, e);
                    kept.reason = "CC_Def_Broken".Translate();
                }
            }
            unavailableReason = kept.reason;
            return kept.pieces;
        }

        public string UnavailableReason(Map map)
        {
            ResolveCached(map, out var reason);
            return reason;
        }

        /// <summary>What the template needs from a mod or research: null when the colony can build one of <see cref="needs"/>.</summary>
        private string NeedsReason(Map map)
        {
            if (needs.NullOrEmpty()) return null;
            var defs = needs.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null).ToList();
            if (defs.Any(d => Avail.CanBuild(d, map))) return null;
            if (defs.Count == 0) return "CC_Def_NeedsMod".Translate();
            var first = defs[0];
            var research = first.researchPrerequisites?.Where(r => r != null && !r.IsFinished).Select(r => r.label).ToList();
            return research != null && research.Count > 0
                ? "CC_Def_Missing".Translate("CC_Def_MissingResearch".Translate(first.label, TextUtil.JoinAnd(research)).Resolve()).Resolve()
                : "CC_Def_Missing".Translate(first.label).Resolve();
        }

        [Unsaved] private Texture2D cachedIcon;
        [Unsaved] private bool iconResolved;

        /// <summary>The first of <see cref="iconDefs"/> that exists, else a wall.</summary>
        public Texture2D Icon
        {
            get
            {
                if (!iconResolved)
                {
                    iconResolved = true;
                    ThingDef d = null;
                    if (iconDefs != null)
                        d = iconDefs.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(x => x != null && x.uiIcon != null && x.uiIcon != BaseContent.BadTex);
                    cachedIcon = d?.uiIcon ?? ThingDefOf.Wall.uiIcon;
                }
                return cachedIcon;
            }
        }

        public static List<DefenseTemplateDef> All => DefDatabase<DefenseTemplateDef>.AllDefsListForReading.OrderBy(d => d.order).ToList();
    }
}
