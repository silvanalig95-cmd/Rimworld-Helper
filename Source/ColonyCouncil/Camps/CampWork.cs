using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>How long a camp job takes, and whether it's a sociable one (people chat while doing it).</summary>
    public class CampJobExtension : DefModExtension
    {
        public int ticks = 1000;
        public bool social;
    }

    /// <summary>
    /// The camp duty's job giver: what one of them does next. In order: eat, sleep, tend a colonist lying hurt
    /// outdoors nearby, build the camp, fetch their kills and wood to the stash, then by the time of day: butchering,
    /// cooking and sitting by the fire in the evening, hunting, felling trees and their own work by day. Only their own
    /// goods are ever used, and nothing is taken from inside or near the colony's home area.
    /// </summary>
    public class JobGiver_CampLife : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            var camp = CampTracker.CampOf(pawn);
            // Only the camp's own people: an animal sold to their trader joins the group too, and just stays near.
            if (camp == null || !camp.Settled || pawn.Map != camp.map || !camp.Has(pawn) || !pawn.RaceProps.Humanlike) return null;
            try
            {
                return CampWork.JobFor(camp, pawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp job", e);
                return null;
            }
        }
    }

    public static class CampWork
    {
        private static readonly Dictionary<int, int> nextSearch = new Dictionary<int, int>();
        private static Game searchGame;

        /// <summary>
        /// Not to look again for a camp's prey or trees before this tick. Camp ids start over in every game, so what
        /// another game (or an earlier load of this one) noted is let go.
        /// </summary>
        private static bool Searched(int key)
        {
            if (searchGame != Current.Game)
            {
                searchGame = Current.Game;
                nextSearch.Clear();
            }
            return nextSearch.TryGetValue(key, out int next) && Now < next && next - Now <= GenDate.TicksPerHour * 3;
        }

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>Jobs given to each pawn this tick: a job that fails at once mustn't be handed out again and again.</summary>
        private static readonly Dictionary<int, (int tick, int count)> given = new Dictionary<int, (int, int)>();

        public static Job JobFor(Camp camp, Pawn pawn)
        {
            if (pawn.Downed || pawn.InMentalState || pawn.needs == null) return null;
            int now = Now;
            if (given.TryGetValue(pawn.thingIDNumber, out var g) && g.tick == now)
            {
                if (g.count >= 3) return null;
                given[pawn.thingIDNumber] = (now, g.count + 1);
            }
            else given[pawn.thingIDNumber] = (now, 1);
            if (given.Count > 200) given.Clear();
            var map = pawn.Map;
            int hour = GenLocalDate.HourOfDay(map);
            bool night = hour >= 22 || hour < 6;
            var food = pawn.needs.food;
            var rest = pawn.needs.rest;
            if (food != null && food.CurLevelPercentage < 0.3f && Camp.MealsOf(pawn) > 0) return Eat(camp, pawn);
            if (rest != null && (rest.CurLevelPercentage < 0.2f || (night && rest.CurLevelPercentage < 0.95f))) return Sleep(camp, pawn);
            bool adult = !pawn.DevelopmentalStage.Juvenile();
            if (!adult) return Child(camp, pawn, hour);
            var job = Tend(camp, pawn);
            if (job != null) return job;
            if (camp.phase == CampPhase.Building || camp.Unfinished().Count > 0)
            {
                job = Build(camp, pawn);
                if (job != null) return job;
            }
            job = Fetch(camp, pawn);
            if (job != null) return job;
            if (hour >= 17 && hour < 22) return Evening(camp, pawn, hour);
            if (hour >= 7 && hour < 17 && camp.phase == CampPhase.Living) return Day(camp, pawn);
            return null;
        }

        // ---------------- Needs ----------------

        private static Job Eat(Camp camp, Pawn pawn) =>
            JobMaker.MakeJob(CampDefOf.Council_CampEat, SitCell(camp, pawn), camp.FireCell);

        private static Job Sleep(Camp camp, Pawn pawn)
        {
            Building_Bed bed = null;
            foreach (var b in camp.Buildings().OfType<Building_Bed>().OrderByDescending(b => b.OwnersForReading.Contains(pawn)))
            {
                if (b.Medical || b.ForPrisoners) continue;
                if (!b.OwnersForReading.Contains(pawn) && !b.AnyUnownedSleepingSlot) continue;
                if (!RestUtility.IsValidBedFor(b, pawn, pawn, false)) continue;
                bed = b;
                break;
            }
            if (bed != null) return JobMaker.MakeJob(JobDefOf.LayDown, bed);
            var map = pawn.Map;
            if (!CellFinder.TryFindRandomCellNear(camp.FireCell, map, 4, c => c.Standable(map) && c != camp.FireCell && c.GetFirstPawn(map) == null && c.GetEdifice(map) == null && !c.Roofed(map), out var cell))
                cell = pawn.Position;
            return JobMaker.MakeJob(JobDefOf.LayDown, cell);
        }

        /// <summary>Children stay close: by the fire in the evening, near the grown-ups by day.</summary>
        private static Job Child(Camp camp, Pawn pawn, int hour)
        {
            if (hour >= 17 && hour < 22 && Rand.Chance(0.5f)) return JobMaker.MakeJob(CampDefOf.Council_CampSit, SitCell(camp, pawn), camp.FireCell);
            return null;
        }

        /// <summary>A colonist lying hurt out in the open near the camp: their healer goes to them.</summary>
        private static Job Tend(Camp camp, Pawn pawn)
        {
            var med = pawn.skills?.GetSkill(SkillDefOf.Medicine);
            if (med == null || med.TotallyDisabled || med.Level < 4 || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)) return null;
            var map = pawn.Map;
            var home = map.areaManager.Home;
            foreach (var c in map.mapPawns.FreeColonistsSpawned)
            {
                if (!c.Downed || c.InBed() || !c.health.HasHediffsNeedingTend() || c.Position.Roofed(map) || (home != null && home[c.Position])) continue;
                if (!c.Position.InHorDistOf(camp.center, CampSite.WorkRadius + 6f)) continue;
                if (camp.Here.Any(p => p != pawn && p.CurJobDef == CampDefOf.Council_CampTend && p.CurJob.targetA.Thing == c)) continue;
                if (!pawn.CanReach(c, PathEndMode.Touch, Danger.Some)) continue;
                return JobMaker.MakeJob(CampDefOf.Council_CampTend, c);
            }
            return null;
        }

        // ---------------- Building ----------------

        private static Job Build(Camp camp, Pawn pawn)
        {
            bool canBuild = !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction);
            bool canHaul = !pawn.WorkTagIsDisabled(WorkTags.Hauling);
            if (!canBuild && !canHaul) return null;
            var goods = camp.Goods();
            foreach (var t in camp.Unfinished().OrderBy(t => t.Position.DistanceToSquared(pawn.Position)))
            {
                if (t.Destroyed || !pawn.CanReserve(t)) continue;
                if (!pawn.CanReach(t, PathEndMode.Touch, Danger.Some)) continue;
                // Bushes and trees in the way come down first, and their own goods are moved aside. Anything else (the
                // colony's things, a plant someone marked or sowed) is left be: the spot waits, and is given up on if
                // it's still blocked when their time for building runs out.
                var block = GenConstruct.FirstBlockingThing(t, pawn);
                if (block != null)
                {
                    if (!Clearable(camp, block)) continue;
                    var clear = GenConstruct.HandleBlockingThingJob(t, pawn);
                    if (clear != null) return clear;
                    continue;
                }
                if (t is Blueprint_Build bp)
                {
                    var cost = bp.TotalMaterialCost();
                    if (cost.NullOrEmpty())
                    {
                        if (canBuild) return JobMaker.MakeJob(JobDefOf.PlaceNoCostFrame, bp);
                        continue;
                    }
                    var job = Deliver(pawn, bp, cost, goods);
                    if (job != null) return job;
                }
                else if (t is Frame f)
                {
                    if (f.IsCompleted())
                    {
                        if (canBuild && Skilled(pawn, f)) return JobMaker.MakeJob(JobDefOf.FinishFrame, f);
                        continue;
                    }
                    var job = Deliver(pawn, f, f.TotalMaterialCost(), goods);
                    if (job != null) return job;
                }
            }
            return null;
        }

        /// <summary>What they may clear off a building spot: wild plants nobody has sown or marked, and their own goods.</summary>
        private static bool Clearable(Camp camp, Thing t)
        {
            if (t.def.category == ThingCategory.Plant) return !(t is Plant p && p.sown) && t.Spawned && t.Map.designationManager.DesignationOn(t) == null;
            return t.def.category == ThingCategory.Item && camp.Owns(t);
        }

        private static bool Skilled(Pawn pawn, Frame f)
        {
            var need = f.def.entityDefToBuild?.constructionSkillPrerequisite ?? 0;
            return need <= 0 || (pawn.skills?.GetSkill(SkillDefOf.Construction)?.Level ?? 0) >= need;
        }

        /// <summary>Their own materials to a blueprint or frame (never anyone else's).</summary>
        private static Job Deliver(Pawn pawn, Thing target, List<ThingDefCountClass> cost, List<Thing> goods)
        {
            if (pawn.WorkTagIsDisabled(WorkTags.Hauling)) return null;
            foreach (var need in cost)
            {
                int n = target is Frame f ? f.ThingCountNeededWithEnroute(need.thingDef, pawn) : ((IConstructible)target).ThingCountNeeded(need.thingDef);
                if (n <= 0) continue;
                var res = goods.Where(g => g.def == need.thingDef && g.Spawned && !g.Destroyed && pawn.CanReserveAndReach(g, PathEndMode.ClosestTouch, Danger.Some))
                    .OrderBy(g => g.Position.DistanceToSquared(pawn.Position)).FirstOrDefault();
                if (res == null) continue;
                var job = JobMaker.MakeJob(JobDefOf.HaulToContainer, res, target);
                job.count = Mathf.Min(n, res.stackCount);
                job.haulMode = HaulMode.ToContainer;
                return job;
            }
            return null;
        }

        // ---------------- Fetching ----------------

        /// <summary>Their kills and the wood from the trees they felled, carried to the stash.</summary>
        private static Job Fetch(Camp camp, Pawn pawn)
        {
            if (pawn.WorkTagIsDisabled(WorkTags.Hauling)) return null;
            var map = pawn.Map;
            var home = map.areaManager.Home;
            foreach (var k in camp.kills)
            {
                var corpse = k?.Corpse;
                if (corpse == null || !corpse.Spawned || corpse.Position.InHorDistOf(camp.stash, 3f)) continue;
                // Carried off into the colony's stockpiles or home since: not theirs to fetch back.
                if (InColony(corpse, home)) continue;
                if (!pawn.CanReserveAndReach(corpse, PathEndMode.ClosestTouch, Danger.Some)) continue;
                return HaulToStash(camp, pawn, corpse);
            }
            foreach (var cell in camp.felled)
            {
                if (!cell.InBounds(map)) continue;
                foreach (var t in GenRadial.RadialDistinctThingsAround(cell, map, 2.5f, true))
                {
                    // What their felling left lying there: the game drops what a stranger cuts forbidden to the colony,
                    // so wood the colony can use (its own, cut nearby) is never taken, nor anything in its stockpiles.
                    if (t.def.category != ThingCategory.Item || !t.def.EverHaulable || camp.Owns(t) || !t.IsForbidden(Faction.OfPlayer) || InColony(t, home)) continue;
                    if (t.def != ThingDefOf.WoodLog && !(t.def.stuffProps?.categories?.Contains(StuffCategoryDefOf.Woody) ?? false)) continue;
                    if (!pawn.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some)) continue;
                    return HaulToStash(camp, pawn, t);
                }
            }
            // Their own goods lying away from the stash (dropped, refunded), unless the colony has carried them off.
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
            {
                if (!camp.Owns(t) || !t.Spawned || InColony(t, home) || t.Position.InHorDistOf(camp.stash, 3.5f) || !t.Position.InHorDistOf(camp.center, CampSite.WorkRadius)) continue;
                if (!pawn.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some)) continue;
                return HaulToStash(camp, pawn, t);
            }
            return null;
        }

        /// <summary>In the colony's stockpiles or home area (only for a spawned thing).</summary>
        private static bool InColony(Thing t, Area home) => StoreUtility.IsInAnyStorage(t) || (home != null && home[t.Position]);

        private static Job HaulToStash(Camp camp, Pawn pawn, Thing t)
        {
            var map = pawn.Map;
            // A free spot by the stash that nobody else is bound for: a taken one fails the job (with an error) at once.
            if (!CellFinder.TryFindRandomCellNear(camp.stash, map, 2, c => c.Standable(map) && c.GetFirstItem(map) == null && c.GetEdifice(map) == null
                                                                     && pawn.CanReserve(c) && pawn.CanReach(c, PathEndMode.OnCell, Danger.Some), out var cell))
                return null;
            camp.Own(t);
            var job = JobMaker.MakeJob(JobDefOf.HaulToCell, t, cell);
            job.count = t.stackCount;
            job.haulMode = HaulMode.ToCellNonStorage;
            return job;
        }

        // ---------------- Evening ----------------

        private static Job Evening(Camp camp, Pawn pawn, int hour)
        {
            var corpse = camp.Goods().OfType<Corpse>().FirstOrDefault(c => !RottableUtility.IsDessicated(c) && pawn.CanReserveAndReach(c, PathEndMode.Touch, Danger.Some));
            if (corpse != null && !Busy(camp, CampDefOf.Council_CampButcher) && !pawn.WorkTagIsDisabled(WorkTags.ManualSkilled))
                return JobMaker.MakeJob(CampDefOf.Council_CampButcher, corpse);
            if (camp.lastCookDay != GenDate.DaysPassed && !Busy(camp, CampDefOf.Council_CampCook) && (pawn == Cook(camp) || hour >= 19) && !pawn.WorkTagIsDisabled(WorkTags.Cooking))
                return JobMaker.MakeJob(CampDefOf.Council_CampCook, SitCell(camp, pawn), camp.FireCell);
            if (Rand.Chance(0.6f)) return JobMaker.MakeJob(CampDefOf.Council_CampSit, SitCell(camp, pawn), camp.FireCell);
            return null;
        }

        private static Pawn Cook(Camp camp) =>
            camp.Here.Where(p => !p.DevelopmentalStage.Juvenile() && !p.WorkTagIsDisabled(WorkTags.Cooking))
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Cooking)?.Level ?? 0).FirstOrDefault();

        private static bool Busy(Camp camp, JobDef def) => camp.Here.Any(p => p.CurJobDef == def);

        // ---------------- Day ----------------

        private static Job Day(Camp camp, Pawn pawn)
        {
            var kind = camp.kind;
            // Their trader keeps to the stall, where the colony can find them.
            if (pawn == camp.trader && kind.task != null && Rand.Chance(0.75f)) return Task(camp, pawn);
            if (camp.dayHunts < camp.huntsToday && camp.prey.Count == 0 && CanHunt(pawn))
            {
                var animal = Prey(camp, pawn);
                if (animal != null)
                {
                    camp.prey.Add(animal);
                    var job = JobMaker.MakeJob(JobDefOf.Hunt, animal);
                    job.ignoreDesignations = true;
                    job.killIncappedTarget = true;
                    return job;
                }
            }
            if (camp.dayChops < camp.chopsToday && camp.trees.Count == 0 && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.PlantCutting))
            {
                var tree = Tree(camp, pawn);
                if (tree != null)
                {
                    camp.trees.Add(tree);
                    return JobMaker.MakeJob(JobDefOf.CutPlant, tree);
                }
            }
            if (kind.task != null && Rand.Chance(0.45f)) return Task(camp, pawn);
            if (Rand.Chance(0.2f)) return JobMaker.MakeJob(CampDefOf.Council_CampSit, SitCell(camp, pawn), camp.FireCell);
            return null;
        }

        private static Job Task(Camp camp, Pawn pawn)
        {
            var map = pawn.Map;
            var at = camp.plans.FirstOrDefault(p => p.role == camp.kind.taskAt)?.Built(map, camp.faction);
            IntVec3 cell;
            LocalTargetInfo face;
            if (at != null)
            {
                face = at;
                var root = at.def.hasInteractionCell ? at.InteractionCell : at.Position;
                if (!CellFinder.TryFindRandomCellNear(root, map, 1, c => c.Standable(map) && c.GetEdifice(map) == null && c.GetFirstPawn(map) == null, out cell)) cell = root;
            }
            else
            {
                face = camp.FireCell;
                if (!CellFinder.TryFindRandomCellNear(camp.center, map, CampSite.CoreRadius + 2, c => c.Standable(map) && c.GetEdifice(map) == null && !c.Roofed(map) && !(map.areaManager.Home?[c] ?? false), out cell)) cell = pawn.Position;
            }
            return JobMaker.MakeJob(camp.kind.task, cell, face);
        }

        private static bool CanHunt(Pawn pawn) =>
            pawn.equipment?.Primary != null && pawn.equipment.Primary.def.IsRangedWeapon && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hunting)
            && !pawn.WorkTagIsDisabled(WorkTags.Violent) && (pawn.health?.capacities?.CapableOf(PawnCapacityDefOf.Manipulation) ?? false);

        /// <summary>A wild animal near the camp that is safe to hunt (not a predator, unlikely to turn on anyone), well away from the colony.</summary>
        private static Pawn Prey(Camp camp, Pawn hunter)
        {
            int key = camp.id * 2;
            if (Searched(key)) return null;
            var map = hunter.Map;
            Pawn best = null;
            float bestD = float.MaxValue;
            foreach (var a in map.mapPawns.AllPawnsSpawned)
            {
                if (a.Faction != null || a.RaceProps == null || !a.RaceProps.Animal || a.Dead || a.Downed || a.InMentalState || a.RaceProps.predator) continue;
                if (a.BodySize > 2.5f || a.IsPrisoner || map.designationManager.DesignationOn(a) != null) continue;
                if (!camp.MayWork(a.Position)) continue;
                // Nobody of the colony (people or animals) anywhere near the line of fire.
                if (camp.ColonyInLineOfFire(hunter.Position, a.Position)) continue;
                if (PawnUtility.GetManhunterOnDamageChance(a, hunter) > 0.04f) continue;
                float d = a.Position.DistanceToSquared(camp.center);
                if (d >= bestD || !hunter.CanReserveAndReach(a, PathEndMode.Touch, Danger.Some)) continue;
                best = a;
                bestD = d;
            }
            if (best == null) nextSearch[key] = Now + GenDate.TicksPerHour * 2;
            return best;
        }

        /// <summary>A grown tree near the camp that nobody planted and nobody has marked, well away from the colony.</summary>
        private static Plant Tree(Camp camp, Pawn pawn)
        {
            int key = camp.id * 2 + 1;
            if (Searched(key)) return null;
            var map = pawn.Map;
            Plant best = null;
            float bestD = float.MaxValue;
            foreach (var t in GenRadial.RadialDistinctThingsAround(camp.center, map, CampSite.WorkRadius, true))
            {
                if (!(t is Plant p) || p.def.plant == null || !p.def.plant.IsTree || p.def.plant.treeCategory == TreeCategory.Super) continue;
                if (p.sown || !p.HarvestableNow || p.def.plant.harvestedThingDef == null || p.Position.InHorDistOf(camp.center, CampSite.CoreRadius + 1)) continue;
                if (map.designationManager.DesignationOn(p) != null || !camp.MayWork(p.Position)) continue;
                float d = p.Position.DistanceToSquared(camp.center);
                if (d >= bestD) continue;
                if (!pawn.CanReserveAndReach(p, PathEndMode.Touch, Danger.Some) || !PlantUtility.PawnWillingToCutPlant_Job(p, pawn)) continue;
                best = p;
                bestD = d;
            }
            if (best == null) nextSearch[key] = Now + GenDate.TicksPerHour * 3;
            return best;
        }

        /// <summary>A free spot near the fire to sit, eat or cook at.</summary>
        public static IntVec3 SitCell(Camp camp, Pawn pawn)
        {
            var map = pawn.Map;
            var fire = camp.FireCell;
            if (CellFinder.TryFindRandomCellNear(fire, map, 2, c => c != fire && c.Standable(map) && c.GetEdifice(map) == null && c.GetFirstPawn(map) == null && !c.Roofed(map), out var cell))
                return cell;
            return CellFinder.RandomClosewalkCellNear(fire, map, 3);
        }

        // ---------------- When a camp job is done ----------------

        public static void Done(Pawn pawn, Job job)
        {
            var camp = CampTracker.CampOf(pawn);
            if (camp == null || job == null) return;
            var def = job.def;
            try
            {
                if (def == CampDefOf.Council_CampEat) EatMeal(pawn);
                else if (def == CampDefOf.Council_CampCook)
                {
                    camp.lastCookDay = GenDate.DaysPassed;
                    camp.Cooked(pawn);
                }
                else if (def == CampDefOf.Council_CampButcher) camp.Butchered(pawn, job.targetA.Thing as Corpse);
                else if (def == CampDefOf.Council_CampTend)
                {
                    var patient = job.targetA.Thing as Pawn;
                    if (patient != null && !patient.Dead && patient.health.HasHediffsNeedingTend())
                    {
                        TendUtility.DoTend(pawn, patient, null);
                        camp.Tended(pawn, patient);
                    }
                }
                else if (def.defName == "Council_CampProspect") camp.Prospected(pawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp job done " + def.defName, e);
            }
        }

        private static void EatMeal(Pawn pawn)
        {
            var need = pawn.needs?.food;
            var meal = pawn.inventory?.innerContainer?.FirstOrDefault(t => Camp.IsMeal(pawn, t));
            if (need == null || meal == null) return;
            float per = meal.GetStatValue(StatDefOf.Nutrition);
            float got;
            try
            {
                got = meal.Ingested(pawn, need.NutritionWanted);
            }
            catch (Exception e)
            {
                // The game's eating balked at food from a pack (a mod's meal, say): one meal eaten, plainly, so they
                // don't sit down to eat again and again.
                CouncilLog.WarnOnce("camp meal (" + meal.def.defName + ") eaten the plain way: " + e.Message);
                got = per;
                if (!meal.Destroyed) meal.SplitOff(1).Destroy();
            }
            need.CurLevel += got;
        }
    }

    /// <summary>
    /// Colonists' own recreation, now and then: walking over to the camp to sit by their fire and talk. Never for those
    /// who resent sharing the land (greedy, jealous), nor once things are tense, nor with a camp packing up or at war.
    /// </summary>
    public class JoyGiver_VisitCamp : JoyGiver
    {
        /// <summary>A camp settled in and at peace with the colony (not packing up, not at war), on this colonist's map.</summary>
        private static Camp Open(Pawn pawn)
        {
            var c = CampTracker.Instance?.camp;
            return c != null && c.phase == CampPhase.Living && !c.Hostile && pawn?.Map != null && c.map == pawn.Map ? c : null;
        }

        public override float GetChance(Pawn pawn)
        {
            var c = Open(pawn);
            if (c == null || c.tension >= 60f || !pawn.IsFreeColonist) return 0f;
            var t = pawn.story?.traits;
            if (t != null && (t.HasTrait(TraitDefOf.Greedy) || t.HasTrait(TraitDefOf.Jealous))) return 0f;
            return base.GetChance(pawn);
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            var c = Open(pawn);
            // The visit's ticking feeds recreation: never for someone without that need (it would fail every tick).
            if (c == null || !pawn.IsFreeColonist || pawn.Drafted || pawn.DevelopmentalStage.Baby() || pawn.needs?.joy == null) return null;
            if (!pawn.Position.InHorDistOf(c.center, 90f) || MapFacts.ThreatOn(pawn.Map)) return null;
            var map = pawn.Map;
            var fire = c.FireCell;
            if (!CellFinder.TryFindRandomCellNear(fire, map, 3, x => x != fire && x.Standable(map) && x.GetEdifice(map) == null && !x.Roofed(map) && x.GetFirstPawn(map) == null
                                                                  && pawn.CanReach(x, PathEndMode.OnCell, Danger.Some), out var cell)) return null;
            return JobMaker.MakeJob(def.jobDef, cell, fire);
        }
    }

    /// <summary>A colonist's visit to the camp: a chat with whoever is about, then sitting by their fire a while.</summary>
    public class JobDriver_VisitCamp : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Over the moment they pack up or their people turn on the colony: nobody sits on at a fire that's now an enemy's.
            this.FailOn(() => CampTracker.Instance?.camp is Camp c ? c.phase != CampPhase.Living || c.Hostile || c.map != pawn.Map : true);
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var visit = ToilMaker.MakeToil("VisitCamp");
            visit.initAction = () => CampTracker.Instance?.camp?.Visited(pawn);
            visit.defaultCompleteMode = ToilCompleteMode.Delay;
            visit.defaultDuration = Mathf.Max(600, job.def.joyDuration);
            visit.socialMode = RandomSocialMode.SuperActive;
            visit.handlingFacing = true;
            visit.tickIntervalAction = delta =>
            {
                if (job.targetB.IsValid) pawn.rotationTracker.FaceCell(job.targetB.Cell);
                JoyUtility.JoyTickCheckEnd(pawn, delta, JoyTickFullJoyAction.EndJob);
            };
            yield return visit;
        }
    }

    /// <summary>
    /// A camp job: go to a spot (or to someone), spend a while there facing the fire or the work, then the job's
    /// effect (a meal eaten, supper cooked, a body butchered, a colonist tended, a find at the diggings).
    /// </summary>
    public class JobDriver_CampTask : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // A body to butcher is theirs alone while they work on it.
            if (job.def == CampDefOf.Council_CampButcher && job.targetA.HasThing) return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => CampTracker.CampOf(pawn) == null);
            var ext = job.def.GetModExtension<CampJobExtension>();
            if (job.targetA.HasThing)
            {
                this.FailOnDespawnedOrNull(TargetIndex.A);
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            }
            else yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var work = ToilMaker.MakeToil("CampTask");
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = Mathf.Max(60, ext?.ticks ?? 1000);
            work.handlingFacing = true;
            work.socialMode = ext != null && ext.social ? RandomSocialMode.SuperActive : RandomSocialMode.Normal;
            work.tickAction = () =>
            {
                var face = job.targetB.IsValid ? job.targetB : job.targetA;
                if (face.IsValid && face.Cell != pawn.Position) pawn.rotationTracker.FaceCell(face.Cell);
            };
            yield return work;
            yield return Toils_General.Do(() => CampWork.Done(pawn, job));
        }
    }
}
