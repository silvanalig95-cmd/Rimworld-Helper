using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Trees grown against the walls brought down (People4, 6 November 2026): three grown trees or more in the home area,
    /// each beside one of the colony's walls or doors, where a storm can bring one down on a roof (the leaning tree among
    /// the dangers). Promised, each still standing is marked for the game's own cutting, and the colony's cutters fell
    /// them: logs on the ground, a stump. Fulfilled once none of them stands. Never for one whose faith minds felling trees.
    /// </summary>
    public class WishWorker_TreesByWalls : WishWorker
    {
        private static readonly Dictionary<int, (int hour, string cells)> known = new Dictionary<int, (int, string)>();

        /// <summary>A wall, a door or a column of the colony's: something a falling tree breaks.</summary>
        private static bool Breaks(Building b) =>
            b != null && b.Faction == Faction.OfPlayer && b.def.building != null && !b.def.building.isNaturalRock
            && (b is Building_Door || b.def.fillPercent >= 0.99f || b.def.passability == Traversability.Impassable);

        /// <summary>A grown tree of the land (no anima, gauranlen or polux tree: those carry comps of their own).</summary>
        private static bool GrownTree(Thing t) =>
            t is Plant p && p.def.plant != null && p.def.plant.IsTree && p.Growth >= PeopleFourMath.GrownTree && p.def.comps.NullOrEmpty();

        /// <summary>The cells of grown trees in the home area beside the colony's walls or doors, as text; empty when too few.</summary>
        public static string TreesFor(Map map)
        {
            if (map == null || !map.IsPlayerHome) return "";
            int hour = Find.TickManager.TicksGame / GenDate.TicksPerHour;
            if (known.TryGetValue(map.uniqueID, out var k) && k.hour == hour) return k.cells;
            var cells = new List<(int, int)>();
            var home = map.areaManager.Home;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Plant))
            {
                if (!GrownTree(t) || !home[t.Position]) continue;
                bool beside = false;
                foreach (var c in GenAdj.CellsAdjacent8Way(t))
                {
                    if (!c.InBounds(map)) continue;
                    if (c.GetEdifice(map) is Building b && Breaks(b))
                    {
                        beside = true;
                        break;
                    }
                }
                if (beside) cells.Add((t.Position.x, t.Position.z));
            }
            string text = PeopleFourMath.TreesByWallsWorth(cells.Count) ? LifeNewMath.Encode(cells) : "";
            known[map.uniqueID] = (hour, text);
            if (known.Count > 20) known.Clear();
            return text;
        }

        private static IEnumerable<Plant> Standing(Wish wish)
        {
            var map = wish.Beneficiary?.MapHeld;
            if (map == null) yield break;
            foreach (var (x, z) in LifeNewMath.Decode(wish.subjectKey))
            {
                var c = new IntVec3(x, 0, z);
                if (!c.InBounds(map)) continue;
                var tree = c.GetPlant(map);
                if (tree != null && GrownTree(tree)) yield return tree;
            }
        }

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || pawn.DevelopmentalStage.Juvenile()) yield break;
            // The council's question about the same trees was asked lately (Rimfolk_DlmY_TreeLine): one voice at a time.
            if (YearFour.AskedLately(map, "Rimfolk_DlmY_TreeLine")) yield break;
            string cells = TreesFor(map);
            if (cells.NullOrEmpty()) yield break;
            int n = LifeNewMath.Decode(cells).Count;
            // A wind or rain makes it come up sooner: that's when people look at the trees over the roof.
            var w = map.weatherManager;
            float weight = w.curWeather != null && (w.curWeather.rainRate > 0.1f || w.curWeather.windSpeedFactor >= 1.5f) ? 1.6f : 0.8f;
            if (People.Passionate(pawn.skills?.GetSkill(SkillDefOf.Construction)) || People.Passionate(pawn.skills?.GetSkill(SkillDefOf.Plants))) weight *= 1.3f;
            var c = Candidate(weight, "CC_Reason_TreesByWalls".Translate(pawn.Named("PAWN"), n.Named("COUNT")));
            c.subjectKey = cells;
            c.targetValue = n;
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => !Standing(wish).Any();

        public override bool StillValid(Wish wish) => LifeNewMath.Decode(wish.subjectKey).Count > 0;

        public override float Progress(Wish wish) => wish.targetValue <= 0f ? -1f : 1f - Standing(wish).Count() / wish.targetValue;

        public override string ProgressText(Wish wish) => "CC_Wish_TreesByWalls_Progress".Translate(Standing(wish).Count()).Resolve();

        public override LookTargets LookAt(Wish wish)
        {
            var tree = Standing(wish).FirstOrDefault();
            return tree != null ? new LookTargets(tree) : base.LookAt(wish);
        }

        public override string OnPromised(Wish wish) => Mark(wish) ? "CC_Promise_TreesByWalls" : null;

        public override string TrySelfHelp(Wish wish) => Mark(wish) ? "CC_SelfHelp_TreesByWalls" : null;

        /// <summary>Each tree still standing marked for the game's own cutting.</summary>
        private static bool Mark(Wish wish)
        {
            int marked = 0;
            foreach (var tree in Standing(wish).ToList())
            {
                var dm = tree.Map.designationManager;
                if (dm.DesignationOn(tree, DesignationDefOf.CutPlant) == null && dm.DesignationOn(tree, DesignationDefOf.HarvestPlant) == null)
                {
                    dm.RemoveAllDesignationsOn(tree);
                    dm.AddDesignation(new Designation(tree, DesignationDefOf.CutPlant));
                }
                marked++;
            }
            return marked > 0;
        }
    }

    /// <summary>
    /// The snow cleared from the doors (People4): deep snow (0.4 and more) on twelve cells or more of the open ground
    /// within two steps of the colony's doors, and almost none of it marked for clearing. Promised, those cells go in the
    /// game's own snow-clearing area and the colony's cleaners shovel them. Fulfilled once nine in ten are clear.
    /// </summary>
    public class WishWorker_SnowWays : WishWorker
    {
        /// <summary>The open ground in the home area within two steps of the colony's doors.</summary>
        public static List<IntVec3> Ways(Map map)
        {
            var cells = new HashSet<IntVec3>();
            if (map == null) return new List<IntVec3>();
            var home = map.areaManager.Home;
            foreach (var b in map.listerBuildings.allBuildingsColonist)
            {
                if (!(b is Building_Door door) || !door.Spawned) continue;
                foreach (var c in GenRadial.RadialCellsAround(door.Position, 2.5f, false))
                    if (c.InBounds(map) && home[c] && !c.Roofed(map) && c.Standable(map) && c.GetEdifice(map) == null) cells.Add(c);
            }
            return cells.ToList();
        }

        private static int Marked(Map map, List<IntVec3> cells)
        {
            var area = map.areaManager.SnowOrSandClear;
            return area == null ? 0 : cells.Count(c => area[c]);
        }

        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            var map = HomeMap(pawn);
            if (map == null || !map.IsPlayerHome || pawn.DevelopmentalStage.Juvenile() || map.snowGrid == null) yield break;
            // The council's question about the snow was asked lately (Rimfolk_DlmY_SnowPaths): one voice at a time.
            if (YearFour.AskedLately(map, "Rimfolk_DlmY_SnowPaths")) yield break;
            var ways = Ways(map);
            var deep = ways.Where(c => map.snowGrid.GetDepth(c) >= PeopleFourMath.DeepSnow).ToList();
            if (!PeopleFourMath.SnowWorth(deep.Count, Marked(map, ways))) yield break;
            // The old, and anyone who's had a cold night lately, feel it most.
            float weight = Flavor.HumanAge(pawn) >= 55f ? 1.4f : 0.9f;
            var c = Candidate(weight, "CC_Reason_SnowWays".Translate(pawn.Named("PAWN"), deep.Count.Named("COUNT")));
            c.subjectKey = LifeNewMath.Encode(deep.Select(x => (x.x, x.z)));
            c.targetValue = deep.Count;
            yield return c;
        }

        private static List<IntVec3> Cells(Wish wish) => LifeNewMath.Decode(wish.subjectKey).Select(c => new IntVec3(c.x, 0, c.z)).ToList();

        private static int StillDeep(Wish wish, out int all)
        {
            var map = wish.Beneficiary?.MapHeld;
            var cells = Cells(wish);
            all = cells.Count;
            if (map?.snowGrid == null) return all;
            return cells.Count(c => c.InBounds(map) && map.snowGrid.GetDepth(c) > PeopleFourMath.ClearedSnow);
        }

        public override bool IsFulfilled(Wish wish) => PeopleFourMath.SnowCleared(StillDeep(wish, out int all), all);

        public override bool StillValid(Wish wish) => Cells(wish).Count > 0;

        public override float Progress(Wish wish)
        {
            int deep = StillDeep(wish, out int all);
            return all == 0 ? -1f : 1f - deep / (float)all;
        }

        public override string ProgressText(Wish wish) => "CC_Wish_SnowWays_Progress".Translate(StillDeep(wish, out int all), all).Resolve();

        public override LookTargets LookAt(Wish wish)
        {
            var map = wish.Beneficiary?.MapHeld;
            var first = Cells(wish).FirstOrDefault(c => map != null && c.InBounds(map));
            return map != null && first.IsValid && first.InBounds(map) ? new LookTargets(new TargetInfo(first, map)) : base.LookAt(wish);
        }

        public override string OnPromised(Wish wish) => Mark(wish) ? "CC_Promise_SnowWays" : null;

        public override string TrySelfHelp(Wish wish) => Mark(wish) ? "CC_SelfHelp_SnowWays" : null;

        /// <summary>The ways' cells put in the game's own snow-clearing area.</summary>
        private static bool Mark(Wish wish)
        {
            var map = wish.Beneficiary?.MapHeld;
            var area = map?.areaManager.SnowOrSandClear;
            if (area == null) return false;
            int marked = 0;
            foreach (var c in Cells(wish))
            {
                if (!c.InBounds(map)) continue;
                if (!area[c]) area[c] = true;
                marked++;
            }
            return marked > 0;
        }
    }
}
