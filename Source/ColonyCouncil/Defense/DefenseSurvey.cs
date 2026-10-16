using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's situation for its defense, read from the map: where raiders can come from and the narrow places on
    /// their way (<see cref="DefenseMath"/>), what already defends it, what it can build, who fights, and the threats it
    /// faces (the raids its wealth buys, the hostile peoples around, the DLC threats that are on). Worked out on demand,
    /// never more than once a game hour unless asked, and kept until the colony's buildings change a lot or a day has
    /// passed. Nothing here ticks.
    /// </summary>
    public class DefenseSurvey
    {
        public Map map;
        public int tick;
        public int buildings;
        public DefenseMath.MapGrid grid;
        public DefenseMath.Result result;
        public IntVec3 center;
        public IntVec3 bedCenter = IntVec3.Invalid;

        // ---------- Threats ----------
        public float raidPoints;
        public List<Faction> hostiles = new List<Faction>();
        public bool tribal, medieval, industrial, empire, mechanoids, mechsYet, canDrop, canSiege, canBreach;
        public bool insects, anomaly, mechClusters;
        public int wastepacks;
        /// <summary>Open cells in the home area under overhead mountain, and those of them dark and warm (where insects like to nest).</summary>
        public int thickRoof, darkWarm;

        // ---------- Fighters ----------
        public DefenseUtil.Assessment assess;
        public int fighters, ranged, melee, unarmed;
        public Pawn bestShooter, bestMelee;
        public float bestRange;
        /// <summary>Most of the ranged weapons are bows, crossbows and the like.</summary>
        public bool bows;

        // ---------- What stands already ----------
        public int walls, doors, autoTurrets, mannedTurrets, mortars, traps, cover, embrasures, trapsInHome;
        public int weaponsNearBeds, medicine;
        public bool hospital, emp;
        /// <summary>Doors on the busiest way in: two or more mean a second line behind the first.</summary>
        public int doorsOnWay;
        /// <summary>Of the walls facing the outside, the share with a second wall (or rock) behind them.</summary>
        public float doubleWalls;
        public int outerWalls;

        // ---------- Where to build ----------
        public List<ChokeInfo> chokes = new List<ChokeInfo>();
        /// <summary>No narrow place: where the busiest way in crosses open ground a little outside home, and the side it faces.</summary>
        public IntVec3 approach = IntVec3.Invalid;
        public int approachFacing;
        /// <summary>The side most raids come from (0 north .. 3 west).</summary>
        public int mainFacing;

        public class ChokeInfo
        {
            public DefenseMath.Choke c;
            public IntVec3 cell;
            /// <summary>A gate (door) already stands in the gap, in the colony's wall.</summary>
            public bool gated;
            public int coverInside, trapsOutside, turretsNear, woodWalls;
            public IntVec3 Outward => IntVec3.North.RotatedBy(new Rot4(c.facing));
        }

        public bool Reachable => result != null && !result.unreachable;

        public ChokeInfo Main => chokes.Count > 0 ? chokes[0] : null;

        private static readonly Dictionary<Map, DefenseSurvey> cache = new Dictionary<Map, DefenseSurvey>();
        private static Game cacheGame;

        /// <summary>
        /// The survey for a map: the one kept, unless it's a day old, or an hour old and the colony's buildings have
        /// changed a lot since (or <paramref name="force"/>). Null for a map that isn't a home.
        /// </summary>
        public static DefenseSurvey For(Map map, bool force = false)
        {
            if (map == null || !map.IsPlayerHome) return null;
            if (cacheGame != Current.Game)
            {
                cache.Clear();
                cacheGame = Current.Game;
            }
            int now = Find.TickManager.TicksGame;
            if (cache.TryGetValue(map, out var s) && !force)
            {
                int age = now - s.tick;
                if (age >= 0 && age < GenDate.TicksPerHour) return s;
                int count = map.listerBuildings.allBuildingsColonist.Count;
                bool changed = Mathf.Abs(count - s.buildings) >= Mathf.Max(6, s.buildings / 30);
                if (age >= 0 && age < GenDate.TicksPerDay && !changed) return s;
            }
            try
            {
                s = Survey(map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("defense survey", e);
                if (s == null) return null;
                s.tick = now;
            }
            cache[map] = s;
            return s;
        }

        /// <summary>The survey kept for a map, without working one out.</summary>
        public static DefenseSurvey Kept(Map map) => map != null && cacheGame == Current.Game && cache.TryGetValue(map, out var s) ? s : null;

        public static void Forget() => cache.Clear();

        private static DefenseSurvey Survey(Map map)
        {
            var s = new DefenseSurvey { map = map, tick = Find.TickManager.TicksGame, buildings = map.listerBuildings.allBuildingsColonist.Count };
            s.center = TemplatePlanner.ColonyCenter(map);
            s.grid = Grid(map, s);
            s.result = DefenseMath.Analyse(s.grid);
            s.mainFacing = Mathf.RoundToInt(s.result.angle / 90f) % 4;
            Threats(map, s);
            Fighters(map, s);
            Standing(map, s);
            Places(map, s);
            return s;
        }

        // ---------------- The map as a grid ----------------

        private static DefenseMath.MapGrid Grid(Map map, DefenseSurvey s)
        {
            int w = map.Size.x, h = map.Size.z;
            var m = new DefenseMath.MapGrid(w, h) { noBuildEdge = GenGrid.NoBuildEdgeWidth, centerX = s.center.x, centerZ = s.center.z };
            var pg = map.pathing?.Normal?.pathGrid;
            var home = map.areaManager.Home;
            var player = Faction.OfPlayer;
            for (int z = 0; z < h; z++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = new IntVec3(x, 0, z);
                    int i = m.Index(x, z);
                    m.home[i] = home[c];
                    if (c.Fogged(map))
                    {
                        m.kind[i] = DefenseMath.Unknown;
                        continue;
                    }
                    var ed = c.GetEdifice(map);
                    if (ed != null && ed.def.IsDoor)
                    {
                        m.kind[i] = DefenseMath.Door;
                        continue;
                    }
                    if (ed != null && ed.def.passability == Traversability.Impassable)
                    {
                        bool rock = ed.def.building != null && (ed.def.building.isNaturalRock || (ed.def.mineable && ed.Faction == null));
                        m.kind[i] = rock ? DefenseMath.Rock : ed.Faction == player ? DefenseMath.Wall : DefenseMath.Other;
                        continue;
                    }
                    var terrain = c.GetTerrain(map);
                    if (terrain == null || terrain.passability == Traversability.Impassable)
                    {
                        m.kind[i] = terrain != null && terrain.IsWater ? DefenseMath.Water : DefenseMath.Other;
                        continue;
                    }
                    m.kind[i] = terrain.bridge ? DefenseMath.Bridge : DefenseMath.Open;
                    if (pg != null)
                    {
                        int cost = pg.Cost(c);
                        m.cost[i] = cost >= 10000 ? 600 : Mathf.Clamp(cost, 0, 600);
                    }
                }
            }
            // Where people live: the rooms with the colonists' beds, else round the colony's middle.
            var beds = MapFacts.Of(map).Beds;
            long bx = 0, bz = 0;
            int bn = 0;
            foreach (var bed in beds)
            {
                if (bed == null || !bed.Spawned || bed.Medical || bed.ForPrisoners) continue;
                bx += bed.Position.x;
                bz += bed.Position.z;
                bn++;
                var room = bed.GetRoom();
                if (room != null && !room.PsychologicallyOutdoors && room.ProperRoom && room.CellCount <= 400)
                {
                    foreach (var c in room.Cells) m.core[m.Index(c.x, c.z)] = true;
                }
                else
                {
                    foreach (var c in GenRadial.RadialCellsAround(bed.Position, 2f, true))
                        if (c.InBounds(map)) m.core[m.Index(c.x, c.z)] = true;
                }
            }
            if (bn > 0) s.bedCenter = new IntVec3((int)(bx / bn), 0, (int)(bz / bn));
            else
                foreach (var c in GenRadial.RadialCellsAround(s.center, 5f, true))
                    if (c.InBounds(map)) m.core[m.Index(c.x, c.z)] = true;
            return m;
        }

        // ---------------- Threats ----------------

        private static void Threats(Map map, DefenseSurvey s)
        {
            s.raidPoints = Mathf.Max(35f, StorytellerUtility.DefaultThreatPointsNow(map));
            var dropMode = DefDatabase<PawnsArrivalModeDef>.GetNamedSilentFail("CenterDrop");
            var dropTech = dropMode != null ? dropMode.minTechLevel : TechLevel.Industrial;
            var player = Faction.OfPlayer;
            foreach (var f in Find.FactionManager.AllFactionsListForReading)
            {
                if (f == null || f == player || f.defeated || f.def == null) continue;
                bool hostile;
                try
                {
                    hostile = f.HostileTo(player);
                }
                catch
                {
                    continue;
                }
                if (!hostile) continue;
                if (f == Faction.OfMechanoids)
                {
                    s.mechanoids = true;
                    s.mechsYet = GenDate.DaysPassedSinceSettle >= f.def.earliestRaidDays;
                    continue;
                }
                if (f == Faction.OfInsects)
                {
                    s.insects = true;
                    continue;
                }
                if (ModsConfig.AnomalyActive && f == Faction.OfEntities) continue;
                if (!f.def.humanlikeFaction || f.Hidden) continue;
                s.hostiles.Add(f);
                var tech = f.def.techLevel;
                if (tech <= TechLevel.Neolithic) s.tribal = true;
                else if (tech == TechLevel.Medieval) s.medieval = true;
                else s.industrial = true;
                if (f.def.defName == "Empire") s.empire = true;
                if (f.def.canSiege) s.canSiege = true;
                if (tech >= dropTech) s.canDrop = true;
            }
            s.canBreach = s.hostiles.Count > 0 || (s.mechanoids && s.mechsYet);
            try
            {
                if (ModsConfig.RoyaltyActive && s.mechanoids)
                {
                    var cluster = DefDatabase<IncidentDef>.GetNamedSilentFail("MechCluster");
                    s.mechClusters = cluster != null && s.raidPoints >= cluster.minThreatPoints;
                }
                if (ModsConfig.AnomalyActive)
                    s.anomaly = Find.Storyteller?.difficulty?.AnomalyPlaystyleDef?.enableAnomalyContent ?? false;
                if (ModsConfig.BiotechActive && ThingDefOf.Wastepack != null)
                    s.wastepacks = map.listerThings.ThingsOfDef(ThingDefOf.Wastepack).Sum(t => t.stackCount);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("defense survey: DLC threats", e);
            }
            // Overhead mountain over the home area: where insects can come up.
            if (Find.Storyteller?.difficulty == null || Find.Storyteller.difficulty.allowBigThreats)
            {
                foreach (var c in map.areaManager.Home.ActiveCells)
                {
                    var roof = c.GetRoof(map);
                    if (roof == null || !roof.isThickRoof || c.GetEdifice(map) != null) continue;
                    s.thickRoof++;
                    if (map.glowGrid.GroundGlowAt(c) < 0.3f && c.GetTemperature(map) > -8f) s.darkWarm++;
                }
            }
        }

        // ---------------- Fighters ----------------

        private static void Fighters(Map map, DefenseSurvey s)
        {
            s.assess = DefenseUtil.Assess(map);
            int low = 0, bestShoot = -1, bestMeleeSkill = -1;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.ToList())
            {
                if (p.Downed || p.WorkTagIsDisabled(WorkTags.Violent) || p.DevelopmentalStage.Juvenile() || p.IsQuestLodger()) continue;
                s.fighters++;
                var weapon = p.equipment?.Primary;
                int shooting = p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
                int meleeSkill = p.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
                if (weapon == null) s.unarmed++;
                else if (weapon.def.IsRangedWeapon)
                {
                    s.ranged++;
                    if (weapon.def.techLevel <= TechLevel.Medieval) low++;
                    float range = weapon.def.Verbs?.FirstOrDefault()?.range ?? 0f;
                    if (range > s.bestRange) s.bestRange = range;
                    if (shooting > bestShoot)
                    {
                        bestShoot = shooting;
                        s.bestShooter = p;
                    }
                }
                else
                {
                    s.melee++;
                    if (meleeSkill > bestMeleeSkill)
                    {
                        bestMeleeSkill = meleeSkill;
                        s.bestMelee = p;
                    }
                }
            }
            s.bows = s.ranged > 0 && low * 2 >= s.ranged;
        }

        // ---------------- What stands already ----------------

        private static bool IsEmbrasure(ThingDef d) =>
            d?.building != null && (d.defName.IndexOf("Embrasure", StringComparison.OrdinalIgnoreCase) >= 0 || d.defName.IndexOf("EmbPalisade", StringComparison.OrdinalIgnoreCase) >= 0);

        private static bool IsMannedTurret(ThingDef d) =>
            d?.thingClass != null && typeof(Building_Turret).IsAssignableFrom(d.thingClass) && !Avail.IsAutoTurret(d) && !DefenseTemplateDef.IsMortar(d);

        public static bool IsEmp(ThingDef d)
        {
            if (d?.Verbs == null) return false;
            foreach (var v in d.Verbs)
                if (v?.defaultProjectile?.projectile?.damageDef == DamageDefOf.EMP) return true;
            return false;
        }

        private static void Standing(Map map, DefenseSurvey s)
        {
            var home = map.areaManager.Home;
            foreach (var b in Avail.ColonyBuildings(map, d => d.building != null))
            {
                var d = b.def;
                if (d.IsDoor) s.doors++;
                else if (d.IsWall) s.walls++;
                if (DefenseTemplateDef.IsMortar(d)) s.mortars++;
                else if (Avail.IsAutoTurret(d)) s.autoTurrets++;
                else if (IsMannedTurret(d)) s.mannedTurrets++;
                if (Avail.IsTrap(d))
                {
                    s.traps++;
                    if (home[b.Position]) s.trapsInHome++;
                    if (b.def.thingClass != null && typeof(Building_TrapExplosive).IsAssignableFrom(b.def.thingClass) && b.def.defName.IndexOf("EMP", StringComparison.OrdinalIgnoreCase) >= 0) s.emp = true;
                }
                if (Avail.IsCover(d)) s.cover++;
                if (IsEmbrasure(d)) s.embrasures++;
            }
            s.medicine = MapFacts.Of(map).Medicine;
            s.hospital = RoomUtil.ColonyRooms(map).Any(r => r.Role == RoomUtil.Hospital);
            // Weapons kept near the beds, and anything that fires EMP.
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon))
            {
                if (!t.Spawned) continue;
                if (s.bedCenter.IsValid && t.IsInAnyStorage() && t.Position.InHorDistOf(s.bedCenter, 15f)) s.weaponsNearBeds++;
                if (!s.emp && IsEmp(t.def)) s.emp = true;
            }
            if (!s.emp)
                foreach (var p in map.mapPawns.FreeColonistsSpawned)
                    if (IsEmp(p.equipment?.Primary?.def) || (p.inventory?.innerContainer?.Any(t => IsEmp(t.def)) ?? false))
                    {
                        s.emp = true;
                        break;
                    }
            var empShell = DefDatabase<ThingDef>.GetNamedSilentFail("Shell_EMP");
            if (!s.emp && empShell != null && map.listerThings.ThingsOfDef(empShell).Count > 0) s.emp = true;

            // The busiest way in, on to the beds: how many doors it passes (a second door behind the gate is a line to fall back to).
            var r = s.result;
            if (r != null) s.doorsOnWay = r.doorsToCore;
            // Walls facing the outside, and how many have a second wall or rock behind them.
            if (r != null && !r.unreachable)
            {
                int thick = 0;
                var g = s.grid;
                foreach (var b in Avail.ColonyBuildings(map, d => d.IsWall))
                {
                    var c = b.Position;
                    if (!c.InBounds(map)) continue;
                    for (int dir = 0; dir < 4; dir++)
                    {
                        var step = IntVec3.North.RotatedBy(new Rot4(dir));
                        var outside = c + step;
                        if (!outside.InBounds(map)) continue;
                        int oi = g.Index(outside.x, outside.z);
                        // Raiders' side: walkable, with a way in, and not where people live (the inside of the walls, or home).
                        if (!g.Passable(oi) || r.dist[oi] <= 0) continue;
                        // Faces raiders' ground: is there something solid behind it?
                        s.outerWalls++;
                        var behind = c - step;
                        if (behind.InBounds(map))
                        {
                            byte k = g.kind[g.Index(behind.x, behind.z)];
                            if (k == DefenseMath.Wall || k == DefenseMath.Rock) thick++;
                        }
                        break;
                    }
                }
                s.doubleWalls = s.outerWalls > 0 ? thick / (float)s.outerWalls : 0f;
            }
        }

        // ---------------- Where to build ----------------

        private static void Places(Map map, DefenseSurvey s)
        {
            var r = s.result;
            if (r == null) return;
            foreach (var c in r.chokes)
            {
                var info = new ChokeInfo { c = c, cell = new IntVec3(c.x, 0, c.z) };
                info.gated = c.doors > 0 && (c.endA == DefenseMath.Wall || c.endB == DefenseMath.Wall);
                var outward = info.Outward;
                foreach (var b in Avail.ColonyBuildings(map, d => Avail.IsCover(d) || Avail.IsTrap(d) || d.thingClass != null && typeof(Building_Turret).IsAssignableFrom(d.thingClass) || d.IsWall))
                {
                    var off = b.Position - info.cell;
                    int ahead = off.x * outward.x + off.z * outward.z;
                    float dist = off.LengthHorizontal;
                    if (Avail.IsCover(b.def) && ahead <= 0 && dist <= 9f) info.coverInside++;
                    else if (Avail.IsTrap(b.def) && ahead >= 0 && dist <= 14f) info.trapsOutside++;
                    else if (b.def.IsWall && dist <= 3.5f && b.Stuff?.stuffProps?.categories != null && b.Stuff.stuffProps.categories.Contains(StuffCategoryDefOf.Woody)) info.woodWalls++;
                    else if (b.def.thingClass != null && typeof(Building_Turret).IsAssignableFrom(b.def.thingClass) && !DefenseTemplateDef.IsMortar(b.def) && dist <= 16f) info.turretsNear++;
                }
                s.chokes.Add(info);
            }
            // Open country: where the busiest side's middle way in is a few steps outside home.
            if (r.shown.Count > 0)
            {
                int side = 0;
                for (int k = 1; k < 4; k++)
                    if (r.sideReach[k] > r.sideReach[side]) side = k;
                var ways = r.shown.Where(rt => rt.side == side).ToList();
                if (ways.Count == 0) ways = r.shown;
                var way = ways[ways.Count / 2];
                int last = way.cells.Count - 1;
                int at = Mathf.Max(0, last - 8);
                // Not inside the no-build margin by the map's edge.
                while (at < last && Near(way.cells[at], s.grid)) at++;
                int cell = way.cells[at];
                int back = way.cells[Mathf.Max(0, at - 6)];
                int dx = back % s.grid.w - cell % s.grid.w, dz = back / s.grid.w - cell / s.grid.w;
                s.approach = new IntVec3(cell % s.grid.w, 0, cell / s.grid.w);
                s.approachFacing = Mathf.Abs(dz) >= Mathf.Abs(dx) ? (dz >= 0 ? 0 : 2) : (dx >= 0 ? 1 : 3);
            }
        }

        private static bool Near(int cell, DefenseMath.MapGrid g)
        {
            int x = cell % g.w, z = cell / g.w, e = g.noBuildEdge;
            return x < e || z < e || x >= g.w - e || z >= g.h - e;
        }

        // ---------------- Words ----------------

        /// <summary>"5 cells", "1 cell".</summary>
        public static string Cells(int n) => n == 1 ? "CC_Def_Cell".Translate().ToString() : "CC_Def_Cells".Translate(n).ToString();

        /// <summary>"north-east": a compass point (0 north .. 7 north-west).</summary>
        public static string Direction(int compass) => ("CC_Def_Dir_" + ((compass % 8) + 8) % 8).Translate();

        /// <summary>The side a facing (0 north .. 3 west) looks to.</summary>
        public static string FacingWord(int facing) => Direction(DefenseMath.CompassOfFacing(facing));

        /// <summary>"between two rock faces", "between the rock and the water", "your gate", "the bridge"...</summary>
        public string GapWords(ChokeInfo info)
        {
            var c = info.c;
            if (c.bridge) return "CC_Def_Gap_Bridge".Translate();
            if (info.gated) return "CC_Def_Gap_Gate".Translate();
            byte a = c.endA, b = c.endB;
            bool Has(byte k) => a == k || b == k;
            if (a == DefenseMath.Rock && b == DefenseMath.Rock) return (Mountain(info) ? "CC_Def_Gap_Pass" : "CC_Def_Gap_RockRock").Translate();
            if (Has(DefenseMath.Rock) && Has(DefenseMath.Water)) return "CC_Def_Gap_RockWater".Translate();
            if (a == DefenseMath.Water && b == DefenseMath.Water) return "CC_Def_Gap_WaterWater".Translate();
            if (a == DefenseMath.Wall && b == DefenseMath.Wall) return "CC_Def_Gap_Walls".Translate();
            if (Has(DefenseMath.Wall) && Has(DefenseMath.Rock)) return "CC_Def_Gap_WallRock".Translate();
            if (Has(DefenseMath.Wall) && Has(DefenseMath.Water)) return "CC_Def_Gap_WallWater".Translate();
            if (Has(DefenseMath.Wall)) return "CC_Def_Gap_WallOther".Translate();
            if (Has(DefenseMath.Water)) return "CC_Def_Gap_WaterOther".Translate();
            if (Has(DefenseMath.Rock)) return "CC_Def_Gap_RockOther".Translate();
            return "CC_Def_Gap_Other".Translate();
        }

        /// <summary>Overhead mountain close by the gap: a pass through the mountains rather than between two hills.</summary>
        private bool Mountain(ChokeInfo info)
        {
            int thick = 0;
            foreach (var c in GenRadial.RadialCellsAround(info.cell, 6f, true))
            {
                if (!c.InBounds(map)) continue;
                var roof = c.GetRoof(map);
                if (roof != null && roof.isThickRoof) thick++;
            }
            return thick >= 6;
        }
    }
}
