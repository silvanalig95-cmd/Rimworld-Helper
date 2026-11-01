using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// "EMP for the mechanoids": on the ladder while mechanoid raids can come and the colony can build or make something
    /// that fires EMP (an EMP IED, EMP grenades, the EMP launcher, EMP shells for its mortars: whatever its research and
    /// DLCs give it), or has some already. Two EMP IEDs on the way in, where they go off under the first mechanoids before
    /// they reach the line; with no EMP IED to build, the EMP the colony can make or holds does it. Done when two EMP
    /// IEDs stand in front of the line, or, with none to build, when the colony has something that fires EMP.
    /// </summary>
    public class StepWorker_Emp : DefenseStepWorker
    {
        /// <summary>EMP IEDs wanted in front of the line.</summary>
        public const int Need = 2;

        /// <summary>The blast of an EMP IED (10.9), and a cell more: the colony's own turrets within it are stunned too.</summary>
        public const float Blast = 11.9f;

        /// <summary>A trap that goes off as an EMP blast: the EMP IED, or a mod's like it.</summary>
        public static bool IsEmpTrap(ThingDef d) => Avail.IsTrap(d) && d.GetCompProperties<CompProperties_Explosive>()?.explosiveDamageType == DamageDefOf.EMP;

        /// <summary>A weapon whose shot is EMP, or a mortar shell that loads as one.</summary>
        public static bool FiresEmp(ThingDef d) => DefenseSurvey.IsEmp(d) || d?.projectileWhenLoaded?.projectile?.damageDef == DamageDefOf.EMP;

        /// <summary>The EMP IED the colony can build now (researched, traps allowed in this game), or null.</summary>
        public static ThingDef Trap(Map map) => DefenseTemplateDef.RoleOff("trap") == null ? Avail.BestBuildable(map, IsEmpTrap, "TrapIED_EMP") : null;

        private static Map madeMap;
        private static int madeHour = -1;
        private static List<ThingDef> made = new List<ThingDef>();

        /// <summary>
        /// What fires EMP the colony can make now: a researched recipe at a bench it has or can build (EMP grenades, the
        /// launcher, and shells when it has or can build a mortar to fire them). Worked out once an in-game hour.
        /// </summary>
        public static List<ThingDef> Makeable(Map map)
        {
            int hour = Find.TickManager.TicksGame / GenDate.TicksPerHour;
            if (madeMap == map && madeHour == hour) return made;
            var list = new List<ThingDef>();
            bool mortar = StepWorker_Mortars.Mortar(map) != null || Avail.ColonyBuildings(map, DefenseTemplateDef.IsMortar).Any();
            foreach (var r in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                var d = r.ProducedThingDef;
                if (d == null || list.Contains(d) || !FiresEmp(d) || !r.AvailableNow) continue;
                if (d.projectileWhenLoaded != null && !mortar) continue;
                if (!Avail.RecipeUsers(r).Any(u => map.listerBuildings.ColonistsHaveBuilding(u) || Avail.CanBuild(u, map))) continue;
                list.Add(d);
            }
            madeMap = map;
            madeHour = hour;
            made = list;
            return list;
        }

        public override bool OnLadder(DefenseLineFacts f) => f.s.mechanoids && f.s.mechsYet && (f.s.emp || Trap(f.map) != null || Makeable(f.map).Count > 0);

        public override StepResult Evaluate(DefenseLineFacts f)
        {
            var r = Start(f);
            r.placeAt = "front";
            r.distance = Mathf.Max(1, def.distance);
            r.look = f.gate + f.outward * (r.distance + 3);
            var makes = Makeable(f.map);
            if (makes.Count > 0) r.notes.Add("CC_DefLadder_Emp_Make".Translate(TextUtil.JoinAnd(makes.Select(d => d.label).Take(4).ToList())).Resolve());
            var trap = Trap(f.map);
            if (trap == null)
            {
                // Nothing to build on the way in: EMP in the colony's hands does the work.
                r.template = null;
                if (f.s.emp) r.state = StepState.Done;
                else r.left = "CC_DefLadder_Emp_Nothing".Translate();
                return r;
            }
            int half = Mathf.Max(8, f.span.Count / 2 + 4);
            var built = Avail.ColonyBuildings(f.map, IsEmpTrap).Where(b => InFront(f, b.Position, half)).ToList();
            int waiting = 0;
            foreach (var group in new[] { ThingRequestGroup.Blueprint, ThingRequestGroup.BuildingFrame })
                foreach (var t in f.map.listerThings.ThingsInGroup(group))
                    if (t.Faction == Faction.OfPlayer && t.def.entityDefToBuild is ThingDef td && IsEmpTrap(td) && InFront(f, t.Position, half))
                        waiting++;
            // The blast stuns the colony's own turrets as it does the mechanoids.
            var turrets = Avail.ColonyBuildings(f.map, d => d.thingClass != null && typeof(Building_Turret).IsAssignableFrom(d.thingClass) && !DefenseTemplateDef.IsMortar(d)).ToList();
            var near = turrets.FirstOrDefault(t => built.Any(b => b.Position.DistanceTo(t.Position) < Blast));
            if (near != null) r.notes.Add("CC_DefLadder_Emp_NearTurret".Translate(near.LabelShort).Resolve());
            if (built.Count >= Need)
            {
                r.state = StepState.Done;
                return r;
            }
            var p = f.BestPlan(r.template);
            if (built.Count > 0 || waiting > 0) Band(r, Mathf.Min(0.8f, (built.Count + 0.5f * waiting) / Need), "CC_DefLadder_Emp_Some".Translate(built.Count, Need, waiting).Resolve());
            if (p != null && p.Progress > r.progress) Band(r, p.Progress, "CC_DefLine_Left_Plan".Translate(p.built, p.total).Resolve());
            if (f.s.emp && r.progress < 0.5f) Band(r, 0.5f, "CC_DefLadder_Emp_Held".Translate().Resolve());
            if (r.state == StepState.Open && !Blocked(r, f)) r.left = "CC_DefLadder_Emp_None".Translate(trap.label).Resolve();
            return r;
        }

        private static bool InFront(DefenseLineFacts f, IntVec3 c, int half)
        {
            int ahead = f.line.Ahead(c);
            return ahead >= 1 && ahead <= 24 && Mathf.Abs(f.line.Lateral(c)) <= half;
        }
    }

    /// <summary>
    /// "A bait yard for drop pods": on the ladder once the colony can build an orbital trade beacon and its enemies drop
    /// from orbit. In the game, four times in ten a raid that drops into the middle of a colony comes down by one of its
    /// orbital trade beacons instead, while it has one with power (mechanoids never do); a beacon out in front of the
    /// line, in a clear yard, brings those raids down outside the wall, from where they come in through the gate. Done
    /// when a beacon of the colony's stands out there under open sky, counted by the game as powered.
    /// </summary>
    public class StepWorker_BaitYard : DefenseStepWorker
    {
        /// <summary>The orbital trade beacon the colony can build now, or null.</summary>
        public static ThingDef Beacon(Map map) => Avail.BestBuildable(map, DefenseTemplateDef.IsBeacon, "OrbitalTradeBeacon");

        public override bool OnLadder(DefenseLineFacts f) => f.s.canDrop && Beacon(f.map) != null;

        public override StepResult Evaluate(DefenseLineFacts f)
        {
            var r = Start(f);
            r.placeAt = "front";
            r.distance = Mathf.Max(1, def.distance);
            r.look = f.gate + f.outward * (r.distance + 5);
            var droppers = f.s.hostiles.Where(x => x.def.techLevel >= TechLevel.Industrial).ToList();
            if (droppers.Count > 0) r.notes.Add("CC_DefLadder_Bait_Who".Translate(DefenseAdvice.Factions(droppers)).Resolve());
            var map = f.map;
            var all = Avail.ColonyBuildings(map, DefenseTemplateDef.IsBeacon).ToList();
            var outside = all.Where(b => Out(f, b.Position)).OrderBy(b => b.Position.DistanceTo(f.gate)).ToList();
            // Beacons elsewhere under open sky: the pods may come down by one of those instead.
            int open = all.Count(b => !outside.Contains(b) && !b.Position.Roofed(map));
            if (open > 0) r.notes.Add("CC_DefLadder_Bait_Others".Translate(open).Resolve());
            if (!f.Closed)
            {
                r.state = StepState.NotYet;
                r.left = "CC_DefLadder_Bait_Closed".Translate();
                return r;
            }
            if (f.seeing == 0 && f.turretsWorking == 0) r.notes.Add("CC_DefLadder_Bait_NoGuns".Translate());
            if (outside.Count > 0)
            {
                var powered = new HashSet<Building_OrbitalTradeBeacon>(Building_OrbitalTradeBeacon.AllPowered(map));
                var good = outside.FirstOrDefault(b => !b.Position.Roofed(map) && b is Building_OrbitalTradeBeacon tb && powered.Contains(tb));
                r.look = (good ?? outside[0]).Position;
                if (good != null)
                {
                    r.state = StepState.Done;
                    return r;
                }
                var b0 = outside[0];
                Part(r, 0.6f, (b0.Position.Roofed(map) ? "CC_DefLadder_Bait_Roof" : "CC_DefLadder_Bait_NoPower").Translate().Resolve());
                return r;
            }
            var p = f.BestPlan(r.template);
            if (p != null && p.Progress > 0f) Band(r, p.Progress, "CC_DefLine_Left_Plan".Translate(p.built, p.total).Resolve());
            if (r.state == StepState.Open && !Blocked(r, f)) r.left = "CC_DefLadder_Bait_None".Translate();
            return r;
        }

        /// <summary>Out in front of the line: a few cells past the wall, and not far off to the side.</summary>
        private static bool Out(DefenseLineFacts f, IntVec3 c)
        {
            int ahead = f.line.Ahead(c);
            return ahead >= 3 && ahead <= 30 && Mathf.Abs(f.line.Lateral(c)) <= Mathf.Max(16, f.span.Count / 2 + 10);
        }
    }

    /// <summary>
    /// "Mortars for sieges": on the ladder once the colony can build a mortar (researched, and mortars allowed in this
    /// game) while its enemies lay sieges or mechanoid clusters land near it. Two mortars in a pit deep inside, under
    /// open sky, a wall beside each, the shells on a shelf out of a blast's reach. Done when the colony has two mortars
    /// or the pit's plan stands; a mortar under a roof is named, as it can't fire.
    /// </summary>
    public class StepWorker_Mortars : DefenseStepWorker
    {
        public const int Need = 2;

        /// <summary>The mortar the colony can build now, or null.</summary>
        public static ThingDef Mortar(Map map) => DefenseTemplateDef.RoleOff("mortar") == null ? Avail.BestBuildable(map, DefenseTemplateDef.IsMortar, "Turret_Mortar") : null;

        public override bool OnLadder(DefenseLineFacts f) => (f.s.canSiege || f.s.mechClusters) && Mortar(f.map) != null;

        public override StepResult Evaluate(DefenseLineFacts f)
        {
            var r = Start(f);
            r.placeAt = "core";
            r.distance = 0;
            r.look = f.s.center;
            var besiegers = f.s.hostiles.Where(x => x.def.canSiege).ToList();
            if (besiegers.Count > 0) r.notes.Add("CC_DefLadder_Mortar_Sieges".Translate(DefenseAdvice.Factions(besiegers)).Resolve());
            if (f.s.mechClusters) r.notes.Add("CC_DefLadder_Mortar_Clusters".Translate());
            var map = f.map;
            var mortars = Avail.ColonyBuildings(map, DefenseTemplateDef.IsMortar).ToList();
            bool Roofed(Building b) => b.OccupiedRect().Cells.Any(c => c.Roofed(map));
            var roofed = mortars.FirstOrDefault(Roofed);
            int working = mortars.Count(b => !Roofed(b));
            if (mortars.Count > 0) r.look = mortars[0].Position;
            if (roofed != null) r.notes.Add("CC_DefLadder_Mortar_Roof".Translate(roofed.LabelShort).Resolve());
            var p = f.BestPlan(r.template);
            if (working >= Need || p != null && p.Progress >= 0.85f)
            {
                r.state = StepState.Done;
                return r;
            }
            if (p != null && p.Progress > 0f) Band(r, p.Progress, "CC_DefLine_Left_Plan".Translate(p.built, p.total).Resolve());
            if (working > 0) Band(r, 0.5f, "CC_DefLadder_Mortar_One".Translate().Resolve());
            if (r.state == StepState.Open && !Blocked(r, f)) r.left = "CC_DefLadder_Mortar_None".Translate();
            return r;
        }
    }
}
