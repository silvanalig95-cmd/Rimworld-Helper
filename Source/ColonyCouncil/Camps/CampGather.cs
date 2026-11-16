using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    [DefOf]
    public static class CampGatherDefOf
    {
        public static JobDef Rimfolk_CampBuild;
        public static JobDef Rimfolk_CampCutStone;

        static CampGatherDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CampGatherDefOf));
        }
    }

    // What a camp lays out is built, and what it needs is gathered on the land (CampGatherMath; the player's report of 16
    // November 2026, with their save: three stools laid out round a surveyors' fire were never built and were given up,
    // and a second shelf stood as a frame holding all its wood for more than a day). Every frame of theirs is finished by
    // their own construction job (JobDriver_CampBuild); a piece short of a material is gathered for on the land by one of
    // them (two in a camp of six), by day once the camp stands: a wild tree felled and its logs carried to the piece, plain
    // rock mined and its chunk dressed into blocks at their stonecutter's table or, tribal and medieval, by hand at their
    // crafting spot. What only a town makes (steel, cloth, chemfuel) is only what they carry. Never the colony's things:
    // every tree, rock and chunk is asked of CampTake first.
    public partial class Camp
    {
        // ---------------- What a material comes from ----------------

        /// <summary>Where a material for their building comes from: logs from trees, stone blocks from rock, the rest only what they carry.</summary>
        public static CampSource SourceOf(ThingDef d)
        {
            if (d == null) return CampSource.Carried;
            if (d.thingCategories != null && d.IsWithinCategory(ThingCategoryDefOf.StoneBlocks)) return CampSource.Rock;
            if (d == ThingDefOf.WoodLog || (d.stuffProps?.categories?.Contains(StuffCategoryDefOf.Woody) ?? false)) return CampSource.Trees;
            return CampSource.Carried;
        }

        /// <summary>The stone blocks a chunk is dressed into, and how many (none for anything but a stone chunk).</summary>
        public static (ThingDef blocks, int count) BlocksOf(ThingDef chunk)
        {
            if (chunk == null || !CampTake.StoneChunk(chunk) || chunk.butcherProducts == null) return (null, 0);
            foreach (var p in chunk.butcherProducts)
                if (p?.thingDef != null && p.count > 0 && SourceOf(p.thingDef) == CampSource.Rock) return (p.thingDef, p.count);
            return (null, 0);
        }

        /// <summary>The stone blocks a cell of rock gives once mined and dressed (none for anything but plain rock).</summary>
        private static ThingDef BlocksOfRock(Thing rock) => rock != null && CampTake.PlainRock(rock.def) ? BlocksOf(rock.def.building?.mineableThing).blocks : null;

        // ---------------- What their building needs ----------------

        [Unsaved] private List<(ThingDef def, GatherFacts facts)> buildNeeds;
        [Unsaved] private int buildNeedsTick = -99999;
        /// <summary>A mark of how far the addition under way has come, to see whether anything moved on it this hour (-1: not yet looked).</summary>
        [Unsaved] private int growMark = -1;

        /// <summary>Their pieces' needs are looked at again (a tree felled, a chunk dressed, a job handed out).</summary>
        public void BuildNeedsChanged() => buildNeedsTick = -99999;

        /// <summary>They have building to do: a blueprint or frame of theirs, or a plan waiting for its ground to be cleared.</summary>
        public bool BuildWork() => Unfinished().Count > 0 || plans.Any(p => p.waiting && !p.placed && !p.dropped);

        /// <summary>What their own work left lying (the logs of a tree they felled, a chunk they mined), not yet in their goods.</summary>
        public List<Thing> WorkLeft()
        {
            var list = new List<Thing>();
            if (map == null) return list;
            var home = map.areaManager.Home;
            foreach (var cell in felled)
            {
                if (!cell.InBounds(map)) continue;
                foreach (var t in GenRadial.RadialDistinctThingsAround(cell, map, 1.5f, true))
                    if (t.def.category == ThingCategory.Item && !(t is Corpse) && t.def.EverHaulable && !Owns(t) && !list.Contains(t) && FromTheirWork(t) && !(home != null && home[t.Position]))
                        list.Add(t);
            }
            return list;
        }

        /// <summary>What they build with: their goods at the camp (not what they put out or sell) and what their own work left lying.</summary>
        public List<Thing> BuildGoods()
        {
            var list = Goods().Where(t => !(t is Corpse) && !IsProp(t) && !IsWare(t)).ToList();
            list.AddRange(WorkLeft());
            return list;
        }

        /// <summary>How much of a thing they have to build with: what they build with and what they carry in their packs (not their trader's stock).</summary>
        private int HaveFor(ThingDef def, List<Thing> goods)
        {
            int n = goods.Where(t => t.def == def).Sum(t => t.stackCount);
            foreach (var p in Here)
            {
                var inv = p.inventory?.innerContainer;
                if (inv == null) continue;
                foreach (var t in inv)
                    if (t.def == def && (p != trader || p.inventory.NotForSale(t))) n += t.stackCount;
            }
            return n;
        }

        /// <summary>What a blueprint or frame of theirs still needs, material by material, less what is in it or on its way to it.</summary>
        private IEnumerable<(ThingDef def, int count)> StillNeeds(Thing t)
        {
            List<ThingDefCountClass> cost = null;
            try
            {
                cost = t is Blueprint_Build bp ? bp.TotalMaterialCost() : t is Frame f ? f.TotalMaterialCost() : null;
            }
            catch (Exception e)
            {
                CouncilLog.WarnOnce("camp piece's cost (" + t.def.defName + "): " + e.Message);
            }
            if (cost == null) yield break;
            foreach (var c in cost)
            {
                if (c?.thingDef == null) continue;
                int n = t is Frame frame ? frame.ThingCountNeeded(c.thingDef) : c.count;
                if (t is IHaulEnroute e && map != null) n -= map.enrouteManager.GetEnroute(e, c.thingDef);
                if (n > 0) yield return (c.thingDef, n);
            }
        }

        /// <summary>Grown hands about the camp who could do a work for their building (their trader keeps to the stall).</summary>
        private IEnumerable<Pawn> Workers(bool traderToo) => Here.Where(p => !p.Downed && !p.DevelopmentalStage.Juvenile() && (traderToo || p != trader));

        /// <summary>
        /// The facts about one material, for <see cref="CampGatherMath"/>: how much more of it the pieces need than they have,
        /// and what the land within their reach gives of it.
        /// </summary>
        private GatherFacts FactsFor(ThingDef def, int need, List<Thing> goods)
        {
            var hands = Workers(false).ToList();
            var f = new GatherFacts
            {
                source = SourceOf(def),
                shortBy = need - HaveFor(def, goods),
                packing = packingFor != null,
                hauler = Workers(true).Any(p => !p.WorkTagIsDisabled(WorkTags.Hauling)),
                feller = hands.Any(p => Camp.SkillFor(p, CampLabour.Chop) >= 0),
                miner = hands.Any(p => Camp.SkillFor(p, CampLabour.Mine) >= 0)
            };
            if (f.shortBy <= 0 || map == null) return f;
            if (f.source == CampSource.Trees) f.treeYield = TreeYield(def, f.shortBy);
            else if (f.source == CampSource.Rock)
            {
                f.cutter = CutterSpot() != null;
                foreach (var t in goods)
                {
                    var (blocks, per) = BlocksOf(t.def);
                    if (blocks == def) f.chunkYield += per * t.stackCount;
                }
                foreach (var t in FreeChunks(def, null)) f.freeChunkYield += BlocksOf(t.def).count * t.stackCount;
                f.rockCells = RockCells(def, f.shortBy / CampGatherMath.BlocksPerRock + 1);
            }
            return f;
        }

        /// <summary>
        /// What their pieces under way still need, material by material, with what the land offers of each (looked at again
        /// at most every ten seconds of game time, or as soon as something changes for it).
        /// </summary>
        public List<(ThingDef def, GatherFacts facts)> BuildNeeds()
        {
            if (buildNeeds != null && Now - buildNeedsTick < 600) return buildNeeds;
            buildNeedsTick = Now;
            buildNeeds = NeedsOf(Unfinished());
            return buildNeeds;
        }

        /// <summary>What these pieces still need, material by material, with what the land offers of each.</summary>
        private List<(ThingDef def, GatherFacts facts)> NeedsOf(IEnumerable<Thing> pieces)
        {
            var need = new Dictionary<ThingDef, int>();
            foreach (var t in pieces)
                foreach (var (def, n) in StillNeeds(t))
                    need[def] = (need.TryGetValue(def, out var m) ? m : 0) + n;
            var list = new List<(ThingDef, GatherFacts)>();
            if (need.Count == 0) return list;
            var goods = BuildGoods();
            foreach (var kv in need) list.Add((kv.Key, FactsFor(kv.Key, kv.Value, goods)));
            return list;
        }

        /// <summary>
        /// Each hour of a settled camp with something laid out: what their pieces need that they carry in their packs goes
        /// down at their stash, where it can be carried to them.
        /// </summary>
        private void BuildHourly()
        {
            if (!Settled || map == null || Unfinished().Count == 0) return;
            BuildNeedsChanged();
            var needs = BuildNeeds();
            if (needs.Count > 0) DropMaterials(new HashSet<ThingDef>(needs.Select(n => n.def)));
        }

        // ---------------- What the land gives ----------------

        /// <summary>A grown wild tree near the camp they may fell: nobody planted it or marked it, not a tree of the faiths, outside their camp itself, well away from the colony.</summary>
        public bool Fellable(Plant p)
        {
            if (p == null || !p.Spawned || p.def.plant == null || !p.def.plant.IsTree || p.def.plant.treeCategory == TreeCategory.Super) return false;
            if (p.sown || !p.HarvestableNow || p.def.plant.harvestedThingDef == null || p.Position.InHorDistOf(center, CampSite.CoreRadius + 1)) return false;
            return map.designationManager.DesignationOn(p) == null && MayWork(p.Position) && CampTake.May(p, this);
        }

        /// <summary>The logs of a wood the wild trees within their reach would give, counted until there are <paramref name="want"/>.</summary>
        private int TreeYield(ThingDef wood, int want)
        {
            int n = 0;
            foreach (var t in GenRadial.RadialDistinctThingsAround(center, map, CampSite.WorkRadius, true))
            {
                if (!(t is Plant p) || p.def.plant?.harvestedThingDef != wood || !Fellable(p)) continue;
                n += Mathf.Max(1, p.YieldNow());
                if (n >= want) break;
            }
            return n;
        }

        /// <summary>Plain rock within their reach they may mine whose chunks are dressed into these blocks, counted up to <paramref name="cap"/>.</summary>
        private int RockCells(ThingDef blocks, int cap)
        {
            int n = 0;
            foreach (var rock in Rocks(blocks))
            {
                n++;
                if (n >= cap) break;
            }
            return n;
        }

        /// <summary>Plain rock within their reach they may mine whose chunks are dressed into these blocks (any plain rock for null), nearest first.</summary>
        private IEnumerable<Thing> Rocks(ThingDef blocks)
        {
            foreach (var c in GenRadial.RadialCellsAround(center, CampSite.WorkRadius, false))
            {
                if (!c.InBounds(map)) continue;
                var rock = c.GetFirstMineable(map);
                if (rock == null) continue;
                var gives = BlocksOfRock(rock);
                if (gives == null || (blocks != null && gives != blocks) || !MayWork(c) || !CampTake.May(rock, this) || CampSite.FateAt(map, c) != CellFate.Mine) continue;
                yield return rock;
            }
        }

        /// <summary>Chunks of stone lying free on the land within their reach that they may take, whose blocks these are, nearest first; reachable for <paramref name="p"/> when given.</summary>
        private IEnumerable<Thing> FreeChunks(ThingDef blocks, Pawn p)
        {
            var list = new List<Thing>();
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
            {
                if (!t.Spawned || Owns(t) || !CampTake.StoneChunk(t.def) || BlocksOf(t.def).blocks != blocks || !t.Position.InHorDistOf(center, CampSite.WorkRadius)) continue;
                if (!MayWork(t.Position) || !CampTake.May(t, this)) continue;
                if (p != null && (Here.Any(o => o != p && o.CurJob?.targetA.Thing == t) || !p.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some))) continue;
                list.Add(t);
            }
            return list.OrderBy(t => t.Position.DistanceToSquared(center));
        }

        /// <summary>
        /// Where they dress stone into blocks: a stonecutter's table of theirs; else, for a tribal or medieval people, their
        /// crafting spot or their own work spot, by hand. Null when they have neither.
        /// </summary>
        public Building CutterSpot()
        {
            if (map == null) return null;
            foreach (var p in plans)
            {
                if (p.dropped || p.def == null || !p.def.IsWorkTable) continue;
                var b = p.Built(map, faction);
                // A table whose own recipes make stone blocks (the game's stonecutter's table, a mod's).
                if (b?.def.AllRecipes != null && b.def.AllRecipes.Any(r => r?.products != null && r.products.Any(x => x?.thingDef != null && SourceOf(x.thingDef) == CampSource.Rock)))
                    return b;
            }
            if (!CampGatherMath.CutsByHand(CampKit.AgeOf(faction))) return null;
            foreach (var role in new[] { "craft", "spot" })
            {
                var b = plans.Where(p => p.role == role && !p.dropped && p.def != null && p.def.IsWorkTable).Select(p => p.Built(map, faction)).FirstOrDefault(x => x != null);
                if (b != null) return b;
            }
            return null;
        }

        /// <summary>
        /// The stone blocks the land round the camp gives them: of a chunk of theirs, else of the nearest plain rock they may
        /// mine (for a piece that can be of stone, where there are no trees to cut).
        /// </summary>
        private ThingDef LocalBlocks()
        {
            foreach (var t in Goods())
            {
                var b = BlocksOf(t.def).blocks;
                if (b != null) return b;
            }
            if (map == null) return null;
            var rock = Rocks(null).FirstOrDefault();
            return BlocksOfRock(rock);
        }

        // ---------------- Gathering ----------------

        [Unsaved] private int nextBuildRockSearch = -1;

        /// <summary>One of them is gathering for their building now: felling a tree of theirs, mining, dressing stone.</summary>
        private bool Gathering(Pawn o)
        {
            var job = o.CurJob;
            if (job == null) return false;
            return (job.def == JobDefOf.CutPlant && trees.Contains(job.targetA.Thing)) || job.def == JobDefOf.Mine || job.def == CampGatherDefOf.Rimfolk_CampCutStone;
        }

        /// <summary>
        /// Something gathered for their building, when their pieces are short of a material the land gives: a wild tree
        /// felled, a chunk dressed into blocks, a chunk lying free fetched, plain rock mined. One of them at it at a time
        /// (two in a camp of six), never their trader.
        /// </summary>
        public Job GatherForBuildJob(Pawn p)
        {
            if (p == null || map == null || p == trader || packingFor != null || p.DevelopmentalStage.Juvenile()) return null;
            var needs = BuildNeeds();
            if (!needs.Any(n => n.facts.shortBy > 0)) return null;
            if (Here.Count(o => o != p && Gathering(o)) >= CampGatherMath.Gatherers(Hands)) return null;
            foreach (var (def, f) in needs)
            {
                Job job = null;
                switch (CampGatherMath.Next(f))
                {
                    case CampGatherStep.Fell:
                        job = FellFor(p, def);
                        break;
                    case CampGatherStep.Cut:
                        job = CutFor(p, def);
                        break;
                    case CampGatherStep.Pick:
                        job = PickFor(p, def);
                        break;
                    case CampGatherStep.Mine:
                        job = MineFor(p, def);
                        break;
                }
                if (job == null) continue;
                BuildNeedsChanged();
                return job;
            }
            return null;
        }

        /// <summary>A wild tree felled for its logs, the nearest that gives this wood.</summary>
        private Job FellFor(Pawn p, ThingDef wood)
        {
            if (SkillFor(p, CampLabour.Chop) < 0) return null;
            var tree = CampWork.Tree(this, p, wood);
            if (tree == null) return null;
            trees.Add(tree);
            SayLine(p, "timber", 0.3f);
            return JobMaker.MakeJob(JobDefOf.CutPlant, tree);
        }

        /// <summary>Plain rock mined for a chunk of the stone their pieces need, the nearest to the camp.</summary>
        private Job MineFor(Pawn p, ThingDef blocks)
        {
            if (Now < nextBuildRockSearch || SkillFor(p, CampLabour.Mine) < 0) return null;
            var rock = Rocks(blocks).FirstOrDefault(r => !Here.Any(o => o.CurJob?.targetA.Thing == r) && p.CanReserveAndReach(r, PathEndMode.Touch, Danger.Some));
            if (rock == null)
            {
                nextBuildRockSearch = Now + GenDate.TicksPerHour * 2;
                return null;
            }
            rockMined++;
            Worked(rock.Position);
            AddTension(0.3f);
            SayLine(p, "stone", 0.3f);
            var job = JobMaker.MakeJob(JobDefOf.Mine, rock);
            job.ignoreDesignations = true;
            return job;
        }

        /// <summary>A chunk lying free on the land carried to their stash, to be dressed into blocks.</summary>
        private Job PickFor(Pawn p, ThingDef blocks)
        {
            if (p.WorkTagIsDisabled(WorkTags.Hauling)) return null;
            var chunk = FreeChunks(blocks, p).FirstOrDefault();
            return chunk == null ? null : CampWork.HaulToStash(this, p, chunk);
        }

        /// <summary>A chunk of theirs dressed into blocks at their stonecutter's table, or by hand at their crafting spot.</summary>
        private Job CutFor(Pawn p, ThingDef blocks)
        {
            if (SkillFor(p, CampLabour.Craft) < 0 || Here.Any(o => o != p && o.CurJobDef == CampGatherDefOf.Rimfolk_CampCutStone)) return null;
            var spot = CutterSpot();
            var chunk = Goods().FirstOrDefault(t => BlocksOf(t.def).blocks == blocks);
            if (spot == null || chunk == null) return null;
            var cell = spot.def.hasInteractionCell ? spot.InteractionCell : spot.Position;
            if (!p.CanReach(cell, PathEndMode.OnCell, Danger.Some)) return null;
            var job = JobMaker.MakeJob(CampGatherDefOf.Rimfolk_CampCutStone, cell, spot);
            job.targetC = chunk;
            SayLine(p, "stone", 0.3f);
            return job;
        }

        /// <summary>The stone dressed: the chunk used up, its blocks in their stash, theirs.</summary>
        public void CutStone(Pawn p, Thing chunk)
        {
            if (chunk == null || chunk.Destroyed || !Owns(chunk) || BlocksOf(chunk.def).blocks == null) chunk = Goods().FirstOrDefault(t => BlocksOf(t.def).blocks != null);
            if (chunk == null) return;
            var (blocks, count) = BlocksOf(chunk.def);
            if (chunk.stackCount > 1) chunk.SplitOff(1).Destroy();
            else chunk.Destroy();
            var made = ThingMaker.MakeThing(blocks);
            made.stackCount = Mathf.Max(1, Mathf.Min(count, blocks.stackLimit));
            if (GenPlace.TryPlaceThing(made, stash, map, ThingPlaceMode.Near, out var put, null, AtStash) && put != null) Own(put);
            thingsMade++;
            p?.skills?.Learn(SkillDefOf.Crafting, 100f);
            BuildNeedsChanged();
        }

        // ---------------- Holding on, or giving up ----------------

        /// <summary>One of them can build this: construction is open to them and they have the skill it needs.</summary>
        public bool CanBuildPiece(ThingDef def) => Workers(true).Any(p => CampWork.CanBuild(p) && CampWork.Skilled(p, def));

        /// <summary>A mark of how far the pieces have come: what stands, what is in the frames, the work done on them, the trees felled, rock mined and stone dressed.</summary>
        private int ProgressMark(List<CampPlan> mine)
        {
            int m = mine.Count(p => p.Built(map, faction) != null) * 7919;
            var cells = new HashSet<IntVec3>(mine.Select(p => p.cell));
            foreach (var t in Unfinished())
            {
                if (!cells.Contains(t.Position)) continue;
                m += 31;
                if (t is Frame f) m += f.resourceContainer.TotalStackCount * 13 + (int)f.workDone;
            }
            return m + treesFelled * 101 + rockMined * 103 + thingsMade * 107;
        }

        /// <summary>
        /// Why the addition under way is given up now (CampGatherMath.GiveUpWhy: packing, builder, hauler, land, stalled), or
        /// null while it goes on; anything that moved on it this hour gives it two more days.
        /// </summary>
        private string GrowHoldUp(List<CampPlan> mine)
        {
            int mark = ProgressMark(mine);
            if (mark != growMark)
            {
                growMark = mark;
                growUntil = Now + CampGatherMath.StallTicks;
            }
            // Nobody about just now (all away on an errand): it waits for them.
            if (Hands == 0) return null;
            var cells = new HashSet<IntVec3>(mine.Select(p => p.cell));
            var pieces = Unfinished().Where(t => cells.Contains(t.Position)).ToList();
            var needs = NeedsOf(pieces);
            var f = new BuildFacts
            {
                packing = packingFor != null,
                builder = mine.Where(p => p.Built(map, faction) == null).All(p => CanBuildPiece(p.def)),
                needsCarrying = pieces.Any(t => !(t is Frame fr && fr.IsCompleted())),
                hauler = Workers(true).Any(p => !p.WorkTagIsDisabled(WorkTags.Hauling)),
                landGives = needs.All(n => CampGatherMath.LandGives(n.facts)),
                sinceProgress = Now - (growUntil - CampGatherMath.StallTicks)
            };
            return CampGatherMath.GiveUpWhy(f);
        }

        // ---------------- What their building waits on (dev mode) ----------------

        /// <summary>Every piece of theirs laid out and what each of them would do for it now, and what they gather: for a report of a camp that doesn't build.</summary>
        public string BuildReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Label} of {faction?.Name} at {center}: {phase}, growing {growing ?? "nothing"}" + (growing != null ? $", given up in {(growUntil - Now) / (float)GenDate.TicksPerHour:0.#} h without progress" : "") + $", hands {Hands}, hour {GenLocalDate.HourOfDay(map)}.");
            var goods = BuildGoods();
            foreach (var t in Unfinished())
            {
                sb.Append($"  {t.LabelCap} at {t.Position}");
                if (t is Frame f) sb.Append($", {f.resourceContainer.TotalStackCount} in it, work {f.workDone:0}/{f.WorkToBuild:0}" + (f.IsCompleted() ? ", all its materials" : ""));
                sb.AppendLine(":");
                foreach (var p in Here)
                {
                    var job = CampWork.BuildStep(this, p, t, goods, out string why);
                    // What the game itself says of it, for a report: its reservations, a way to it, what stands on it.
                    var reserver = map.reservationManager.FirstRespectedReserver(t, p);
                    string game = (p.CanReserve(t) ? "" : ", the game: reserved" + (reserver != null ? " by " + reserver.LabelShort : ""))
                                  + (p.CanReach(t, PathEndMode.Touch, Danger.Some) ? "" : ", the game: no way to it")
                                  + (GenConstruct.FirstBlockingThing(t, p) is Thing block ? ", the game: in the way " + block.LabelShort : "");
                    sb.AppendLine($"    {p.LabelShort} ({p.CurJobDef?.defName ?? "no job"}): " + (job != null ? "would " + job.def.defName + (job.targetA.Thing != null && job.targetA.Thing != t ? " " + job.targetA.Thing.LabelShort : "") : why ?? "nothing") + game);
                }
            }
            foreach (var p in plans.Where(x => x.waiting && !x.placed && !x.dropped))
                sb.AppendLine($"  {p.def?.label} at {p.cell} waits for its ground: {Blocking(p)?.LabelShort ?? "nothing they may clear"}");
            BuildNeedsChanged();
            foreach (var (def, f) in BuildNeeds())
                sb.AppendLine($"  {def.label}: short {f.shortBy}, {f.source}, trees give {f.treeYield}, chunks {f.chunkYield}+{f.freeChunkYield}, rock {f.rockCells}, cutter {f.cutter}, feller {f.feller}, miner {f.miner}: {CampGatherMath.Next(f)}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// One of a camp's people finishing a frame of theirs that holds all its materials: the game's own construction work
    /// (the builder's construction speed, the material's pace, the frame's construction effect, the skill learnt, the
    /// game's own finishing with its quality), without the checks the game makes of the colony's builders that a stranger
    /// can fall foul of (work settings, what the colony forbade, an ideoligion's say on what may be built, the map's
    /// reservations: one of them builds a frame at a time by the camp's own count, CampWork.BuildStep).
    /// </summary>
    public class JobDriver_CampBuild : JobDriver, IBuildableDriver
    {
        private Frame Frame => job.GetTarget(TargetIndex.A).Thing as Frame;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public bool TryGetBuildableRect(out CellRect rect)
        {
            var f = Frame;
            rect = f != null && f.Spawned ? f.OccupiedRect() : CellRect.Empty;
            return f != null && f.Spawned;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => CampTracker.CampOf(pawn) == null || Frame == null || Frame.Faction != pawn.Faction || !Frame.IsCompleted());
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var build = ToilMaker.MakeToil("CampBuild");
            build.initAction = () => GenClamor.DoClamor(pawn, 15f, ClamorDefOf.Construction);
            build.tickIntervalAction = delta =>
            {
                var frame = Frame;
                if (frame == null || frame.Destroyed) return;
                pawn.skills?.Learn(SkillDefOf.Construction, 0.25f * delta);
                pawn.rotationTracker.FaceTarget(frame);
                float work = pawn.GetStatValue(StatDefOf.ConstructionSpeed) * 1.7f * delta;
                if (frame.Stuff != null) work *= frame.Stuff.GetStatValueAbstract(StatDefOf.ConstructionSpeedFactor);
                frame.workDone += work;
                if (frame.workDone < frame.WorkToBuild) return;
                frame.CompleteConstruction(pawn);
                CampTracker.CampOf(pawn)?.BuildNeedsChanged();
                ReadyForNextToil();
            };
            build.WithEffect(() => Frame?.ConstructionEffect ?? EffecterDefOf.ConstructMetal, TargetIndex.A);
            build.WithProgressBar(TargetIndex.A, () => Frame == null ? 1f : Frame.PercentComplete);
            build.defaultCompleteMode = ToilCompleteMode.Delay;
            build.defaultDuration = 5000;
            build.activeSkill = () => SkillDefOf.Construction;
            build.handlingFacing = true;
            yield return build;
        }
    }

    /// <summary>Dev mode (Debug actions, "Rimfolk"): what each camp's building waits on, written to the log.</summary>
    public static class CampBuildDebug
    {
        [DebugAction("Rimfolk", "Camps: what their building waits on", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBuildWaits()
        {
            var map = Find.CurrentMap;
            var camps = CampTracker.Instance?.All.Where(c => c.map == map && c.Settled).ToList();
            if (camps.NullOrEmpty())
            {
                Messages.Message("Camps: none settled on this map.", MessageTypeDefOf.SilentInput, false);
                return;
            }
            foreach (var c in camps)
            {
                try
                {
                    Log.Message("[Rimfolk] " + c.BuildReport());
                }
                catch (Exception e)
                {
                    Log.Warning("[Rimfolk] camp build report: " + e);
                }
            }
            Messages.Message("Camps: what their building waits on is in the log.", MessageTypeDefOf.SilentInput, false);
        }
    }
}
