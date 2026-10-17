using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>A place someone shoots from behind the line's cover, whether it sees the gate, and how far it is.</summary>
    public class FirePos
    {
        public IntVec3 cell;
        public bool sees;
        public float dist;
        public Thing cover;
    }

    /// <summary>A plan the line has placed, read again: how many of its pieces stand, and how many are planned and waiting for builders.</summary>
    public class PlanProgress
    {
        public DefenseLinePlan saved;
        public DefenseTemplateDef def;
        public int built, planned, total;

        public float Progress => DefenseLineMath.Progress(built, total);
        public int present => built;
    }

    /// <summary>
    /// What the line review reads from the map for one line, once per review: the gap and the wall across it, the way
    /// round with the gate shut (from the survey's grid, brought up to date round the line), the cover behind it and who
    /// sees the gate from there, turrets and traps, what raiders would shelter behind in the field, the doors on the way
    /// to the beds, the plans the line has placed, and the fighters. The steps' workers and the review's checks read it.
    /// </summary>
    public class DefenseLineFacts
    {
        public Map map;
        public DefenseSurvey s;
        public DefenseLine line;
        public IntVec3 gate, inner, outward, right;
        public Building_Door door;
        public bool pre;
        /// <summary>Where the cover should go for the colony's weapons, and where it stands now (the nearest firing positions that see the gate).</summary>
        public int coverDist, coverAt;
        public float bestRange;

        public List<IntVec3> span = new List<IntVec3>();
        public int spanClosed, spanPlanned;
        /// <summary>The walls, doors and embrasures of the line's row, running on from the gate.</summary>
        public List<Building> parts = new List<Building>();
        public int doors, wood, thick, embrasures;
        public DefenseLineMath.RowSide rowLeft, rowRight;
        public DefenseLineMath.Leak leak = new DefenseLineMath.Leak();

        public List<Thing> cover = new List<Thing>();
        public List<FirePos> firing = new List<FirePos>();
        public int seeing;

        public List<Building_Turret> turrets = new List<Building_Turret>();
        public int turretsWorking;
        public float turretWorth;
        public int traps, trapsWaiting;

        public List<IntVec3> zone = new List<IntVec3>(), front = new List<IntVec3>();
        public List<Thing> shelter = new List<Thing>();
        public int trees, plants, chunks, rocks, walls, things;
        public List<Thing> damaged = new List<Thing>();
        public List<Corpse> corpses = new List<Corpse>();

        public int doorsBehind;
        public List<PlanProgress> plans = new List<PlanProgress>();
        public bool mountain;
        public float raidPoints;
        public List<float> fighters = new List<float>();
        public int ranged;
        /// <summary>Shooters whose belief prefers combat in darkness (they aim better at targets in the dark), and the share of the kill zone left dark at night.</summary>
        public int darkBetter;
        public float darkShare;
        public bool zoneRoofed;
        private readonly List<Pawn> shooters = new List<Pawn>();
        private readonly Dictionary<DefenseLinePlan, DefensePlan> replanned = new Dictionary<DefenseLinePlan, DefensePlan>();

        /// <summary>The gap is closed across: every cell of it wall, door or rock, with a gate in it.</summary>
        public bool Closed => span.Count > 0 && spanClosed >= span.Count && doors > 0;

        /// <summary>A way round over open ground, with the gap closed.</summary>
        public bool Leaks => Closed && leak.found && !leak.viaDoor;

        public PlanProgress BestPlan(DefenseTemplateDef t) => t == null ? null : plans.Where(p => p.def == t).OrderByDescending(p => p.Progress).FirstOrDefault();

        public PlanProgress BestPlanOfKind(string kind) => plans.Where(p => p.def != null && p.def.kind == kind).OrderByDescending(p => p.Progress).FirstOrDefault();

        public IntVec3 Cell(int index) => s?.grid == null || index < 0 ? IntVec3.Invalid : new IntVec3(index % s.grid.w, 0, index / s.grid.w);

        /// <summary>"to the west": the side of the line, as a compass point.</summary>
        public string SideWord(int sign) => DefenseSurvey.FacingWord(DefenseLineMath.FacingOf(right.x * sign, right.z * sign));

        // ---------------- Reading the map ----------------

        public static DefenseLineFacts Read(Map map, DefenseSurvey s, DefenseLine line)
        {
            var f = new DefenseLineFacts { map = map, s = s, line = line, pre = Avail.IsPreIndustrial, raidPoints = s.raidPoints, bestRange = s.bestRange };
            f.outward = line.Outward;
            f.right = line.Right;
            FindGate(f);
            f.inner = f.gate - f.outward;
            f.coverDist = DefenseLineMath.CoverDistance(s.bestRange);
            Span(f);
            Row(f);
            Leak(f);
            Fighters(f);
            Plans(f);
            Cover(f);
            Turrets(f);
            Traps(f);
            Zone(f);
            Behind(f);
            Mountain(f);
            Dark(f);
            return f;
        }

        /// <summary>The gate: the door at the line's cell, else the door in the gap nearest it (which then becomes the line's gate).</summary>
        private static void FindGate(DefenseLineFacts f)
        {
            var map = f.map;
            f.gate = f.line.gate;
            f.door = f.gate.InBounds(map) ? f.gate.GetEdifice(map) as Building_Door : null;
            if (f.door != null) return;
            Building_Door best = null;
            float bestD = 99f;
            foreach (var c in f.line.span.Concat(Row(f.gate, f.line.Right, 6)))
            {
                if (!c.InBounds(map) || !(c.GetEdifice(map) is Building_Door d) || d.Faction != Faction.OfPlayer) continue;
                float dist = c.DistanceTo(f.gate);
                if (dist < bestD)
                {
                    bestD = dist;
                    best = d;
                }
            }
            if (best == null) return;
            f.door = best;
            f.gate = best.Position;
            f.line.gate = best.Position;
        }

        private static IEnumerable<IntVec3> Row(IntVec3 gate, IntVec3 right, int reach)
        {
            for (int k = 1; k <= reach; k++)
            {
                yield return gate + right * k;
                yield return gate - right * k;
            }
        }

        private static bool Built(Thing t) => t != null && !(t is Blueprint) && !(t is Frame);

        /// <summary>A wall or door is planned or going up on the cell (a blueprint or a frame).</summary>
        public static bool PlannedWallOrDoor(IntVec3 c, Map map)
        {
            foreach (var t in c.GetThingList(map))
                if ((t is Blueprint_Build || t is Frame) && t.def.entityDefToBuild is ThingDef td && (TemplatePlanner.IsWallLike(td) || td.IsDoor || td.passability == Traversability.Impassable))
                    return true;
            return false;
        }

        private static void Span(DefenseLineFacts f)
        {
            var map = f.map;
            f.span = f.line.span.Where(c => c.InBounds(map)).ToList();
            if (f.span.Count == 0) f.span.Add(f.gate);
            foreach (var c in f.span)
            {
                var ed = c.GetEdifice(map);
                if (ed != null && (ed.def.IsDoor || ed.def.passability == Traversability.Impassable)) f.spanClosed++;
                else if (PlannedWallOrDoor(c, map)) f.spanPlanned++;
            }
        }

        private static bool IsEmbrasure(ThingDef d) =>
            d?.building != null && (d.defName.IndexOf("Embrasure", StringComparison.OrdinalIgnoreCase) >= 0 || d.defName.IndexOf("EmbPalisade", StringComparison.OrdinalIgnoreCase) >= 0);

        private static bool Woody(Thing t) => t.Stuff?.stuffProps?.categories != null && t.Stuff.stuffProps.categories.Contains(StuffCategoryDefOf.Woody);

        /// <summary>The line's own row: the colony's walls, doors and embrasures running on from the gate on both sides.</summary>
        private static void Row(DefenseLineFacts f)
        {
            var map = f.map;
            var cells = new List<IntVec3> { f.gate };
            for (int sign = -1; sign <= 1; sign += 2)
                for (int k = 1; k <= 30; k++)
                {
                    var c = f.gate + f.right * (k * sign);
                    if (!c.InBounds(map)) break;
                    var ed = c.GetEdifice(map);
                    if (ed == null || ed.Faction != Faction.OfPlayer || !(ed.def.IsDoor || ed.def.passability == Traversability.Impassable)) break;
                    cells.Add(c);
                }
            foreach (var c in cells)
            {
                if (!(c.GetEdifice(map) is Building b) || b.Faction != Faction.OfPlayer) continue;
                f.parts.Add(b);
                if (b.def.IsDoor) f.doors++;
                if (IsEmbrasure(b.def)) f.embrasures++;
                if (Woody(b)) f.wood++;
                var behind = c - f.outward;
                var back = behind.InBounds(map) ? behind.GetEdifice(map) : null;
                if (b.def.IsDoor || back != null && (TemplatePlanner.IsWallLike(back.def) || back.def.IsDoor)) f.thick++;
                if (b.def.useHitPoints && b.HitPoints < b.MaxHitPoints * 0.75f) f.damaged.Add(b);
            }
        }

        // ---------------- The way round ----------------

        private static readonly Dictionary<int, (DefenseSurvey s, int hash, DefenseLineMath.Leak leak, DefenseLineMath.RowSide l, DefenseLineMath.RowSide r)> leaks =
            new Dictionary<int, (DefenseSurvey, int, DefenseLineMath.Leak, DefenseLineMath.RowSide, DefenseLineMath.RowSide)>();

        public const int PatchRadius = 18;

        /// <summary>
        /// The way round, on the survey's grid with the cells round the line read again from the map (what's been built
        /// since the survey counts); kept until the survey or those cells change.
        /// </summary>
        private static void Leak(DefenseLineFacts f)
        {
            var g = f.s.grid;
            if (g == null || !f.gate.InBounds(f.map)) return;
            var undo = new List<(int i, byte k)>();
            int hash = f.gate.GetHashCode() * 31 + f.line.facing;
            var player = Faction.OfPlayer;
            try
            {
                for (int dx = -PatchRadius; dx <= PatchRadius; dx++)
                    for (int dz = -PatchRadius; dz <= PatchRadius; dz++)
                    {
                        var c = new IntVec3(f.gate.x + dx, 0, f.gate.z + dz);
                        if (!c.InBounds(f.map)) continue;
                        int i = g.Index(c.x, c.z);
                        byte k = DefenseSurvey.KindAt(f.map, c, player, null, out _);
                        hash = hash * 7 + k + 1;
                        if (k == g.kind[i]) continue;
                        undo.Add((i, g.kind[i]));
                        g.kind[i] = k;
                    }
                if (leaks.TryGetValue(f.line.id, out var kept) && kept.s == f.s && kept.hash == hash)
                {
                    f.leak = kept.leak;
                    f.rowLeft = kept.l;
                    f.rowRight = kept.r;
                    return;
                }
                f.rowLeft = DefenseLineMath.ScanRow(g, f.gate.x, f.gate.z, f.line.facing, -1, DefenseMath.MaxSpanSide + 6);
                f.rowRight = DefenseLineMath.ScanRow(g, f.gate.x, f.gate.z, f.line.facing, 1, DefenseMath.MaxSpanSide + 6);
                f.leak = DefenseLineMath.FindLeak(g, f.gate.x, f.gate.z, f.line.facing);
                if (leaks.Count > 40) leaks.Clear();
                leaks[f.line.id] = (f.s, hash, f.leak, f.rowLeft, f.rowRight);
            }
            finally
            {
                for (int u = undo.Count - 1; u >= 0; u--) g.kind[undo[u].i] = undo[u].k;
            }
        }

        // ---------------- People and weapons ----------------

        private static void Fighters(DefenseLineFacts f)
        {
            foreach (var p in f.map.mapPawns.FreeColonistsSpawned.ToList())
            {
                if (p.Downed || p.WorkTagIsDisabled(WorkTags.Violent) || p.DevelopmentalStage.Juvenile() || p.IsQuestLodger()) continue;
                var weapon = p.equipment?.Primary;
                bool ranged = weapon != null && weapon.def.IsRangedWeapon;
                if (ranged) f.ranged++;
                int skill = ranged ? p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0 : p.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
                float worth = (30f + 8f * skill) * (weapon == null ? 0.5f : ranged ? 1.3f : 1f);
                f.fighters.Add(worth);
                if (ranged) f.shooters.Add(p);
            }
        }

        // ---------------- Cover and who sees the gate ----------------

        private static void Cover(DefenseLineFacts f)
        {
            var map = f.map;
            var target = f.inner.InBounds(map) && f.inner.Standable(map) ? f.inner : f.gate;
            var taken = new HashSet<IntVec3>();
            foreach (var b in Avail.ColonyBuildings(map, d => Avail.IsCover(d) || IsEmbrasure(d)))
            {
                var c = b.Position;
                int ahead = f.line.Ahead(c), lat = f.line.Lateral(c);
                if (ahead > 0 || ahead < -18 || Mathf.Abs(lat) > 14) continue;
                f.cover.Add(b);
                if (b.def.useHitPoints && b.HitPoints < b.MaxHitPoints * 0.6f) f.damaged.Add(b);
                // The shooter stands on the far side of the cover from the gate: behind it, or beside it for cover that
                // faces the gate from the flank.
                var step = Mathf.Abs(lat) > Mathf.Abs(ahead) + 2 ? f.right * Math.Sign(lat) : -f.outward;
                var at = c + step;
                if (!at.InBounds(map) || !at.Standable(map) || taken.Contains(at) || Avail.IsCover(at.GetEdifice(map)?.def)) continue;
                taken.Add(at);
                f.firing.Add(new FirePos { cell = at, cover = b, dist = at.DistanceTo(f.gate) });
            }
            // The firing positions of the line's own plans count too, where their cover stands.
            foreach (var kv in f.replanned)
            {
                foreach (var c in kv.Value.firing)
                {
                    if (taken.Contains(c) || !c.InBounds(map) || !c.Standable(map)) continue;
                    var front = c + f.outward;
                    if (!front.InBounds(map) || !(Avail.IsCover(front.GetEdifice(map)?.def) || IsEmbrasure(front.GetEdifice(map)?.def))) continue;
                    taken.Add(c);
                    f.firing.Add(new FirePos { cell = c, cover = front.GetEdifice(map), dist = c.DistanceTo(f.gate) });
                }
            }
            float reach = f.bestRange > 0f ? f.bestRange : 25f;
            foreach (var p in f.firing)
            {
                p.sees = p.cell != target && GenSight.LineOfSight(p.cell, target, map, true);
                if (p.sees && p.dist <= reach) f.seeing++;
            }
            var near = f.firing.Where(p => p.sees).Select(p => Mathf.Abs(f.line.Ahead(p.cell))).OrderBy(d => d).ToList();
            f.coverAt = near.Count > 0 ? near[near.Count / 2] : 0;
        }

        // ---------------- Turrets and traps ----------------

        public static float TurretWorth(Thing t)
        {
            float value = 0f;
            try
            {
                value = t.MarketValue;
            }
            catch
            {
                // a modded turret whose value can't be read counts as a light one
            }
            return value >= 1000f ? 240f : 120f;
        }

        /// <summary>Why a turret isn't working: no power, out of fuel or barrels, broken down; null when it is.</summary>
        public static string TurretTrouble(Building_Turret t)
        {
            var power = t.TryGetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn) return "CC_DefLine_Turret_NoPower".Translate(t.LabelShort);
            var fuel = t.TryGetComp<CompRefuelable>();
            if (fuel != null && !fuel.HasFuel) return "CC_DefLine_Turret_NoFuel".Translate(t.LabelShort, fuel.Props.FuelLabel);
            var breakdown = t.TryGetComp<CompBreakdownable>();
            if (breakdown != null && breakdown.BrokenDown) return "CC_DefLine_Turret_Broken".Translate(t.LabelShort);
            return null;
        }

        private static void Turrets(DefenseLineFacts f)
        {
            foreach (var b in Avail.ColonyBuildings(f.map, d => d.thingClass != null && typeof(Building_Turret).IsAssignableFrom(d.thingClass) && !DefenseTemplateDef.IsMortar(d)))
            {
                if (!(b is Building_Turret t)) continue;
                int ahead = f.line.Ahead(t.Position);
                if (ahead > 3 || t.Position.DistanceTo(f.gate) > 22f) continue;
                f.turrets.Add(t);
                if (t.def.useHitPoints && t.HitPoints < t.MaxHitPoints * 0.6f) f.damaged.Add(t);
                if (!Avail.IsAutoTurret(t.def) || TurretTrouble(t) != null) continue;
                f.turretsWorking++;
                f.turretWorth += TurretWorth(t);
            }
        }

        private static bool InFront(DefenseLineFacts f, IntVec3 c, int depth, int half)
        {
            int ahead = f.line.Ahead(c);
            return ahead >= 1 && ahead <= depth && Mathf.Abs(f.line.Lateral(c)) <= half;
        }

        private static void Traps(DefenseLineFacts f)
        {
            int half = Mathf.Max(8, f.span.Count / 2 + 4);
            foreach (var b in Avail.ColonyBuildings(f.map, Avail.IsTrap))
                if (InFront(f, b.Position, 18, half)) f.traps++;
            foreach (var group in new[] { ThingRequestGroup.Blueprint, ThingRequestGroup.BuildingFrame })
                foreach (var t in f.map.listerThings.ThingsInGroup(group))
                    if (t.Faction == Faction.OfPlayer && t.def.entityDefToBuild is ThingDef td && Avail.IsTrap(td) && InFront(f, t.Position, 18, half))
                        f.trapsWaiting++;
        }

        // ---------------- The field of fire ----------------

        private static void Zone(DefenseLineFacts f)
        {
            var map = f.map;
            int half = Mathf.Clamp(f.span.Count / 2 + 1, 2, 8);
            int depth = Mathf.Max(3, (f.coverAt > 0 ? f.coverAt : f.coverDist) - 1);
            var own = new HashSet<Thing>(f.cover);
            foreach (var p in f.parts) own.Add(p);
            int roofed = 0;
            for (int a = -depth; a <= 8; a++)
            {
                if (a == 0) continue;
                int w = a < 0 ? half + (-a) / 3 : half + 2;
                for (int l = -w; l <= w; l++)
                {
                    var c = f.gate + f.outward * a + f.right * l;
                    if (!c.InBounds(map) || c.Fogged(map)) continue;
                    (a < 0 ? f.zone : f.front).Add(c);
                    if (a < 0 && c.Roofed(map)) roofed++;
                    var list = c.GetThingList(map);
                    for (int i = 0; i < list.Count; i++)
                    {
                        var t = list[i];
                        if (own.Contains(t)) continue;
                        if (t is Corpse corpse)
                        {
                            f.corpses.Add(corpse);
                            continue;
                        }
                        if (t is Plant plant && plant.def.fillPercent >= 0.2f)
                        {
                            f.shelter.Add(t);
                            if (plant.def.plant != null && plant.def.plant.IsTree) f.trees++;
                            else f.plants++;
                            continue;
                        }
                        if (t.def.category == ThingCategory.Item && t.def.thingCategories != null && t.def.thingCategories.Contains(ThingCategoryDefOf.Chunks))
                        {
                            f.shelter.Add(t);
                            f.chunks++;
                            continue;
                        }
                        if (t.def.category != ThingCategory.Building || t is Blueprint || t is Frame || CoverUtility.BaseBlockChance(t) < 0.2f) continue;
                        if (t.def.IsDoor || Avail.IsTrap(t.def) || t is Building_Turret) continue;
                        bool natural = t.def.building != null && (t.def.building.isNaturalRock || t.def.mineable && t.Faction == null);
                        // The rock at the gap's own sides is the gap, not something in the field.
                        if (natural && Mathf.Abs(f.line.Lateral(c)) >= f.span.Count / 2) continue;
                        f.shelter.Add(t);
                        if (natural) f.rocks++;
                        else if (t.Faction == Faction.OfPlayer && TemplatePlanner.IsWallLike(t.def)) f.walls++;
                        else f.things++;
                    }
                }
            }
            f.zoneRoofed = f.zone.Count > 0 && roofed * 2 > f.zone.Count;
        }

        // ---------------- The line's plans ----------------

        public static DefensePlan Replan(Map map, DefenseLinePlan saved, DefenseTemplateDef t)
        {
            try
            {
                var pieces = t.ResolveCached(map, out _);
                var cfg = new DefensePlanConfig { rotation = saved.rotation, mirror = saved.mirror, closeGap = saved.closeGap };
                return DefensePlanner.Plan(t, map, saved.anchor, cfg, pieces, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("defense line: reading a plan", e);
                return null;
            }
        }

        /// <summary>A building that does a piece's job stands there (not its blueprint): the same building, or one fit for the role.</summary>
        private static bool Stands(PlannedThing p, DefensePlan plan, Map map)
        {
            if (plan.inRock.Contains(p)) return true;
            if (!p.cell.InBounds(map)) return false;
            foreach (var t in p.cell.GetThingList(map))
            {
                if (!(t is Building b) || !Built(b) || b.Faction != Faction.OfPlayer && !(p.group == "wall" && TemplatePlanner.IsWallLike(b.def))) continue;
                if (b.def == p.def) return true;
                switch (p.group)
                {
                    case "wall": if (TemplatePlanner.IsWallLike(b.def)) return true; break;
                    case "gate": if (b.def.IsDoor) return true; break;
                    case "embrasure": if (b.def.passability == Traversability.Impassable) return true; break;
                    case "cover":
                    case "slow": if (Avail.IsCover(b.def) || b.def.passability == Traversability.PassThroughOnly) return true; break;
                    case "trap": if (Avail.IsTrap(b.def)) return true; break;
                    case "turret": if (b is Building_Turret) return true; break;
                    case "mortar": if (DefenseTemplateDef.IsMortar(b.def)) return true; break;
                    case "shelf": if (b.def.thingClass != null && typeof(Building_Storage).IsAssignableFrom(b.def.thingClass)) return true; break;
                }
            }
            return false;
        }

        private static bool Planned(PlannedThing p, Map map)
        {
            if (!p.cell.InBounds(map)) return false;
            foreach (var t in p.cell.GetThingList(map))
                if ((t is Blueprint_Build || t is Frame) && t.def.entityDefToBuild == p.def) return true;
            return false;
        }

        private static void Plans(DefenseLineFacts f)
        {
            foreach (var saved in f.line.plans)
            {
                var t = saved.Def;
                if (t == null) continue;
                var plan = Replan(f.map, saved, t);
                if (plan == null) continue;
                f.replanned[saved] = plan;
                var pp = new PlanProgress { saved = saved, def = t };
                foreach (var p in plan.pieces)
                {
                    pp.total++;
                    if (Stands(p, plan, f.map)) pp.built++;
                    else if (Planned(p, f.map)) pp.planned++;
                }
                f.plans.Add(pp);
            }
        }

        // ---------------- Behind the line ----------------

        private static readonly Dictionary<DefenseSurvey, int[]> toCore = new Dictionary<DefenseSurvey, int[]>();

        private static void Behind(DefenseLineFacts f)
        {
            var g = f.s.grid;
            if (g == null || !f.gate.InBounds(f.map)) return;
            if (!toCore.TryGetValue(f.s, out var dist))
            {
                if (toCore.Count > 4) toCore.Clear();
                dist = DefenseMath.Distances(g, DefenseLineMath.Targets(g));
                toCore[f.s] = dist;
            }
            f.doorsBehind = DefenseLineMath.DoorsBehind(g, dist, f.gate.x, f.gate.z, f.line.facing);
        }

        private static void Mountain(DefenseLineFacts f)
        {
            int thick = 0;
            foreach (var c in GenRadial.RadialCellsAround(f.gate, 6f, true))
            {
                if (!c.InBounds(f.map)) continue;
                var roof = c.GetRoof(f.map);
                if (roof != null && roof.isThickRoof) thick++;
            }
            f.mountain = thick >= 8;
        }

        /// <summary>
        /// Who aims better at targets in the dark (a belief that prefers combat in darkness: outdoors or indoors, as the
        /// kill zone is roofed or not), and at night the share of the kill zone with no lamp on it. Darkness by itself
        /// doesn't change anyone's aim.
        /// </summary>
        private static void Dark(DefenseLineFacts f)
        {
            if (!ModsConfig.IdeologyActive || f.zone.Count == 0) return;
            var stat = f.zoneRoofed ? StatDefOf.ShootingAccuracyIndoorsDarkOffset : StatDefOf.ShootingAccuracyOutdoorsDarkOffset;
            if (stat == null) return;
            try
            {
                foreach (var p in f.shooters)
                {
                    if (p.GetStatValue(stat) > 0.001f) f.darkBetter++;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("defense line: darkness", e);
                return;
            }
            if (f.darkBetter == 0) return;
            int dark = 0;
            foreach (var c in f.zone)
                if (f.map.glowGrid.GroundGlowAt(c, false, true) < 0.3f) dark++;
            f.darkShare = dark / (float)f.zone.Count;
        }
    }
}
