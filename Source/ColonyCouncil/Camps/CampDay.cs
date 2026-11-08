using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    [DefOf]
    public static class CampDayDefOf
    {
        public static JobDef Rimfolk_CampCraft;
        public static JobDef Rimfolk_CampMend;
        public static JobDef Rimfolk_CampPack;

        static CampDayDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CampDayDefOf));
        }
    }

    // A camper's day (CampDayMath): the work by the person and the kind's calling (wood, game, a catch, plain rock, wild
    // plants, something made at their spot, mending their camp), meals at their table, their own beds, and the evening's
    // talk by the fire. Everything they take is asked of CampTake first.
    public partial class Camp
    {
        /// <summary>Today's counts of the newer work, and how many the day allows (rolled each morning, CampDayMath.PerDay).</summary>
        public int dayMines, minesToday, dayGathers, gathersToday, dayCrafts, craftsToday, dayFish, fishToday;
        /// <summary>What their work brought in over the stay: rock mined, plants gathered, things made, beasts dressed.</summary>
        public int rockMined, plantsGathered, thingsMade, beastsDressed;

        private void ExposeDay()
        {
            Scribe_Values.Look(ref dayMines, "dayMines");
            Scribe_Values.Look(ref minesToday, "minesToday");
            Scribe_Values.Look(ref dayGathers, "dayGathers");
            Scribe_Values.Look(ref gathersToday, "gathersToday");
            Scribe_Values.Look(ref dayCrafts, "dayCrafts");
            Scribe_Values.Look(ref craftsToday, "craftsToday");
            Scribe_Values.Look(ref dayFish, "dayFish");
            Scribe_Values.Look(ref fishToday, "fishToday");
            Scribe_Values.Look(ref rockMined, "rockMined");
            Scribe_Values.Look(ref plantsGathered, "plantsGathered");
            Scribe_Values.Look(ref thingsMade, "thingsMade");
            Scribe_Values.Look(ref beastsDressed, "beastsDressed");
            ExposePack();
        }

        /// <summary>Their kind comes to do this work.</summary>
        public bool Calls(CampLabour w) => kind?.works != null && kind.works.Contains(w);

        /// <summary>Grown hands about the camp, up and able.</summary>
        public int Hands => Here.Count(p => !p.Downed && !p.DevelopmentalStage.Juvenile());

        /// <summary>The day's counts of every work, from the kind's rates and callings and how many hands they have (CampDayMath).</summary>
        private void RollDay()
        {
            int hands = Hands;
            float Rate(CampLabour w, float own) => CampDayMath.PerDay(w, Calls(w), own, hands);
            huntsToday = CampDayMath.Roll(kind.huntsPerDay > 0f || Calls(CampLabour.Hunt) ? Rate(CampLabour.Hunt, kind.huntsPerDay) : 0f, Rand.Value);
            chopsToday = CampDayMath.Roll(Rate(CampLabour.Chop, kind.chopsPerDay), Rand.Value);
            minesToday = CampDayMath.Roll(Rate(CampLabour.Mine, 0f), Rand.Value);
            gathersToday = CampDayMath.Roll(Rate(CampLabour.Gather, 0f), Rand.Value);
            craftsToday = CampDayMath.Roll(Rate(CampLabour.Craft, 0f), Rand.Value);
            fishToday = kind.work == "fish" ? 0 : CampDayMath.Roll(Rate(CampLabour.Fish, 0f), Rand.Value);
            dayMines = dayGathers = dayCrafts = dayFish = 0;
        }

        /// <summary>
        /// Something lying where their own work left it: the logs of a tree they felled, a chunk of rock they mined, what a
        /// plant they gathered gave (the game drops what a stranger cuts or digs forbidden to the colony), or the body of
        /// their own kill.
        /// </summary>
        public bool FromTheirWork(Thing t)
        {
            if (t == null || map == null || !t.Spawned || t.Map != map) return false;
            if (t is Corpse c) return kills.Contains(c.InnerPawn);
            if (t.def.category != ThingCategory.Item || !t.IsForbidden(Faction.OfPlayer) || StoreUtility.IsInAnyStorage(t)) return false;
            foreach (var cell in felled)
                if (cell.InHorDistOf(t.Position, 1.5f)) return true;
            return false;
        }

        /// <summary>A cell their work has just been at: what it leaves there is theirs to fetch for two days (CampWork.Fetch).</summary>
        public void Worked(IntVec3 c)
        {
            if (!c.IsValid || felled.Contains(c)) return;
            felled.Add(c);
            felledTicks.Add(Now);
        }

        /// <summary>The day's work in words for the letter that brings them: their callings first, then what every camp does.</summary>
        public string WorkWords()
        {
            var words = new List<string>();
            var order = new List<CampLabour>();
            if (kind?.works != null) order.AddRange(kind.works);
            if (kind != null && (kind.huntsPerDay > 0f) && !order.Contains(CampLabour.Hunt)) order.Add(CampLabour.Hunt);
            foreach (var w in new[] { CampLabour.Chop, CampLabour.Gather })
                if (!order.Contains(w)) order.Add(w);
            if (CampLand.Odyssey || CampLand.FishingMod)
                if (!order.Contains(CampLabour.Fish)) order.Add(CampLabour.Fish);
            foreach (var w in order)
            {
                string key = "CC_Camp_Work_" + w;
                if (key.CanTranslate()) words.Add(key.Translate());
                if (words.Count >= 4) break;
            }
            return words.Count == 0 ? null : TextUtil.JoinAnd(words);
        }

        // ---------------- The day's work ----------------

        /// <summary>A grown camper's skill for a work (-1 when they can't do it at all), for the order they try the day's work in.</summary>
        public static int SkillFor(Pawn p, CampLabour w)
        {
            int Level(SkillDef s) => p.skills?.GetSkill(s) is SkillRecord r && !r.TotallyDisabled ? r.Level : -1;
            switch (w)
            {
                case CampLabour.Chop: return p.WorkTypeIsDisabled(WorkTypeDefOf.PlantCutting) ? -1 : Level(SkillDefOf.Plants);
                case CampLabour.Gather: return p.WorkTypeIsDisabled(WorkTypeDefOf.PlantCutting) ? -1 : Level(SkillDefOf.Plants);
                case CampLabour.Hunt: return GamePrey.CanHunt(p) ? Level(SkillDefOf.Shooting) : -1;
                case CampLabour.Fish: return p.WorkTagIsDisabled(WorkTags.ManualSkilled) ? -1 : Level(SkillDefOf.Animals);
                case CampLabour.Mine: return p.WorkTypeIsDisabled(WorkTypeDefOf.Mining) ? -1 : Level(SkillDefOf.Mining);
                case CampLabour.Craft: return p.WorkTagIsDisabled(WorkTags.Crafting) ? -1 : Level(SkillDefOf.Crafting);
                case CampLabour.Mend: return p.WorkTypeIsDisabled(WorkTypeDefOf.Construction) ? -1 : Level(SkillDefOf.Construction);
                default: return p.WorkTagIsDisabled(WorkTags.Hauling) ? -1 : 0;
            }
        }

        /// <summary>A job of one kind of day work for this camper, or null when there's none of it to do now.</summary>
        public Job LabourJob(Pawn p, CampLabour w)
        {
            switch (w)
            {
                case CampLabour.Mine: return dayMines < minesToday ? MineJob(p) : null;
                case CampLabour.Gather: return dayGathers < gathersToday ? GatherJob(p) : null;
                case CampLabour.Craft: return dayCrafts < craftsToday ? CraftJob(p) : null;
                case CampLabour.Fish: return kind.work != "fish" && dayFish < fishToday && (CampLand.Odyssey || CampLand.FishingMod) ? CommonFishJob(p) : null;
                case CampLabour.Mend: return MendJob(p);
                default: return null;
            }
        }

        [Unsaved] private int nextRockSearch = -1, nextPlantSearch = -1;

        /// <summary>
        /// Plain rock near the camp to mine: a mineable whose yield is plain stone (CampTake.PlainRock), with no overhead rock
        /// near it that could come down, clear of the colony, reachable. The chunk it leaves is theirs.
        /// </summary>
        private Job MineJob(Pawn p)
        {
            if (Now < nextRockSearch || SkillFor(p, CampLabour.Mine) < 0) return null;
            Thing best = null;
            float bestD = float.MaxValue;
            foreach (var c in GenRadial.RadialCellsAround(center, CampSite.WorkRadius, false))
            {
                if (!c.InBounds(map)) continue;
                var rock = c.GetFirstMineable(map);
                if (rock == null || !CampTake.PlainRock(rock.def) || !MayWork(c) || !CampTake.May(rock, this)) continue;
                float d = c.DistanceToSquared(center);
                if (d >= bestD || CampSite.FateAt(map, c) != CellFate.Mine) continue;
                if (Here.Any(o => o.CurJob?.targetA.Thing == rock) || !p.CanReserveAndReach(rock, PathEndMode.Touch, Danger.Some)) continue;
                best = rock;
                bestD = d;
            }
            if (best == null)
            {
                nextRockSearch = Now + GenDate.TicksPerHour * 3;
                return null;
            }
            dayMines++;
            rockMined++;
            Worked(best.Position);
            AddTension(0.3f);
            var job = JobMaker.MakeJob(JobDefOf.Mine, best);
            job.ignoreDesignations = true;
            return job;
        }

        /// <summary>A wild plant ripe for gathering near the camp (berries, wild healroot, a fruit): nobody sowed it, nobody marked it, clear of the colony.</summary>
        private Job GatherJob(Pawn p)
        {
            if (Now < nextPlantSearch || SkillFor(p, CampLabour.Gather) < 0) return null;
            Plant best = null;
            float bestD = float.MaxValue;
            foreach (var t in GenRadial.RadialDistinctThingsAround(center, map, CampSite.WorkRadius, true))
            {
                if (!(t is Plant plant) || plant.def.plant == null || plant.def.plant.IsTree || !plant.HarvestableNow) continue;
                var yields = plant.def.plant.harvestedThingDef;
                if (yields == null || !(yields.IsNutritionGivingIngestible || yields.IsMedicine) || !MayWork(plant.Position) || !CampTake.May(plant, this)) continue;
                float d = plant.Position.DistanceToSquared(center);
                if (d >= bestD || Here.Any(o => o.CurJob?.targetA.Thing == plant) || !p.CanReserveAndReach(plant, PathEndMode.Touch, Danger.Some)) continue;
                best = plant;
                bestD = d;
            }
            if (best == null)
            {
                nextPlantSearch = Now + GenDate.TicksPerHour * 3;
                return null;
            }
            dayGathers++;
            plantsGathered++;
            Worked(best.Position);
            AddTension(0.2f);
            return JobMaker.MakeJob(JobDefOf.Harvest, best);
        }

        /// <summary>A catch where there's water with fish near the camp, for a camp that doesn't come for the fishing (the fishers' own work is CampKindsLand.cs).</summary>
        private Job CommonFishJob(Pawn p)
        {
            if (SkillFor(p, CampLabour.Fish) < 0 || Now < landNextSearch) return null;
            var spots = FishSpots(farWater).Where(x => x.stand.GetFirstPawn(map) == null && !Here.Any(o => o != p && o.CurJob?.def == CampLandDefOf.Rimfolk_CampFish && o.CurJob.targetA.Cell == x.stand)
                                                       && p.CanReach(x.stand, PathEndMode.OnCell, Danger.Some)).Take(5).ToList();
            if (spots.Count == 0)
            {
                landNextSearch = Now + GenDate.TicksPerHour * 2;
                return null;
            }
            var spot = spots.RandomElement();
            return JobMaker.MakeJob(CampLandDefOf.Rimfolk_CampFish, spot.stand, spot.water);
        }

        /// <summary>One of their own buildings worn below <see cref="CampDayMath.MendBelow"/> of its hit points: mended by whoever builds.</summary>
        private Job MendJob(Pawn p)
        {
            if (SkillFor(p, CampLabour.Mend) < 0) return null;
            var b = Buildings().FirstOrDefault(x => x.def.useHitPoints && x.HitPoints < x.MaxHitPoints * CampDayMath.MendBelow && !Here.Any(o => o.CurJob?.targetA.Thing == x)
                                                    && p.CanReserveAndReach(x, PathEndMode.Touch, Danger.Some));
            return b == null ? null : JobMaker.MakeJob(CampDayDefOf.Rimfolk_CampMend, b);
        }

        /// <summary>Their building mended: back to its full strength.</summary>
        public void Mended(Pawn p, Thing t)
        {
            if (t == null || t.Destroyed || !t.def.useHitPoints) return;
            t.HitPoints = t.MaxHitPoints;
        }

        // ---------------- Something made at their spot ----------------

        /// <summary>Their crafting spot standing: the kit's, or their own spot where it is a work table (the smith's forge).</summary>
        private Building CraftSpot() =>
            plans.Where(x => !x.dropped && x.def != null && (x.role == "craft" || (x.role == "spot" && x.def.IsWorkTable))).Select(x => x.Built(map, faction)).FirstOrDefault(b => b != null);

        /// <summary>
        /// Something simple to make at their crafting spot from what's in their stash: a recipe the spot knows, needing no more
        /// than their people's age knows, of their wood, leather, cloth or stone, worth little. Null when they have nothing to
        /// make it of.
        /// </summary>
        private (RecipeDef recipe, List<(Thing thing, int count)> use, ThingDef stuff)? CraftPlan()
        {
            var spot = CraftSpot();
            if (spot == null) return null;
            var goods = Goods().Where(t => !IsProp(t) && !(t is Corpse)).ToList();
            int tech = (int)(faction?.def?.techLevel ?? TechLevel.Neolithic);
            foreach (var r in spot.def.AllRecipes.InRandomOrder())
            {
                if (r?.products == null || r.products.Count != 1 || r.ingredients.NullOrEmpty() || r.workAmount > 12000f) continue;
                var product = r.products[0].thingDef;
                if (product == null || product.category != ThingCategory.Item || product.BaseMarketValue > 200f || product.IsMedicine || product.IsDrug) continue;
                if (r.researchPrerequisite != null && !CampBuildMath.Within((int)r.researchPrerequisite.techLevel, tech)) continue;
                if (r.researchPrerequisites != null && r.researchPrerequisites.Any(x => x != null && !CampBuildMath.Within((int)x.techLevel, tech))) continue;
                var use = new List<(Thing, int)>();
                ThingDef stuff = null;
                bool ok = true;
                foreach (var ing in r.ingredients)
                {
                    // One stack of theirs that covers it, as the game counts it (by value per piece for wood, leather, cloth).
                    Thing have = null;
                    int count = 0;
                    foreach (var t in goods.Where(t => ing.filter.Allows(t) && (r.fixedIngredientFilter == null || r.fixedIngredientFilter.Allows(t)) && !use.Any(u => u.Item1 == t)).OrderByDescending(t => t.stackCount))
                    {
                        int n = ing.CountRequiredOfFor(t.def, r);
                        if (n <= 0 || n > t.stackCount) continue;
                        have = t;
                        count = n;
                        break;
                    }
                    if (have == null)
                    {
                        ok = false;
                        break;
                    }
                    use.Add((have, count));
                    if (stuff == null && product.MadeFromStuff && have.def.IsStuff && GenStuff.AllowedStuffsFor(product).Contains(have.def)) stuff = have.def;
                }
                if (!ok || (product.MadeFromStuff && stuff == null)) continue;
                return (r, use, stuff);
            }
            return null;
        }

        private Job CraftJob(Pawn p)
        {
            if (SkillFor(p, CampLabour.Craft) < 0 || Here.Any(o => o.CurJobDef == CampDayDefOf.Rimfolk_CampCraft)) return null;
            var spot = CraftSpot();
            if (spot == null || CraftPlan() == null) return null;
            var cell = spot.def.hasInteractionCell ? spot.InteractionCell : spot.Position;
            if (!p.CanReach(cell, PathEndMode.OnCell, Danger.Some)) return null;
            return JobMaker.MakeJob(CampDayDefOf.Rimfolk_CampCraft, cell, spot);
        }

        /// <summary>The work at their spot done: what it was made of used up, what was made in their stash, of the maker's skill.</summary>
        public void Crafted(Pawn p)
        {
            var plan = CraftPlan();
            if (plan == null) return;
            var (recipe, use, stuff) = plan.Value;
            var product = recipe.products[0];
            foreach (var (t, n) in use)
            {
                if (t.Destroyed) return;
                if (n >= t.stackCount) t.Destroy();
                else t.SplitOff(n).Destroy();
            }
            var made = ThingMaker.MakeThing(product.thingDef, stuff);
            made.stackCount = Mathf.Max(1, Mathf.Min(product.count, product.thingDef.stackLimit));
            made.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityCreatedByPawn(p, SkillDefOf.Crafting), ArtGenerationContext.Outsider);
            dayCrafts++;
            thingsMade++;
            if (GenPlace.TryPlaceThing(made, stash, map, ThingPlaceMode.Near, out var put, null, AtStash) && put != null) Own(put);
            p.skills?.Learn(SkillDefOf.Crafting, 200f);
            SayLine(p, "craft", 0.5f);
        }

        // ---------------- Meals, beds and the evening ----------------

        /// <summary>A free seat of theirs at a table (and the table it faces), for a meal: the nearest to the fire first.</summary>
        public (IntVec3 seat, IntVec3 table)? SeatAtTable(Pawn p)
        {
            var tables = plans.Where(x => x.role == "table" && !x.dropped).Select(x => x.Built(map, faction)).Where(b => b != null).ToList();
            if (tables.Count == 0) return null;
            foreach (var s in plans.Where(x => x.role == "seat" && !x.dropped).OrderBy(x => x.cell.DistanceToSquared(FireCell)))
            {
                var seat = s.Built(map, faction);
                if (seat == null) continue;
                var c = seat.Position;
                if (c.GetFirstPawn(map) is Pawn o && o != p) continue;
                if (Here.Any(o2 => o2 != p && o2.CurJob != null && o2.CurJob.targetA.Cell == c)) continue;
                var table = tables.FirstOrDefault(t => t.OccupiedRect().ExpandedBy(1).Contains(c));
                if (table == null || !p.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                var at = table.OccupiedRect().Cells.OrderBy(x => x.DistanceToSquared(c)).First();
                return (c, at);
            }
            // No seats, or all taken: a place on the ground at the table's side.
            foreach (var t in tables)
                foreach (var c in t.OccupiedRect().ExpandedBy(1).EdgeCells)
                    if (c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map) == null && !Here.Any(o => o != p && o.CurJob != null && o.CurJob.targetA.Cell == c) && p.CanReach(c, PathEndMode.OnCell, Danger.Some))
                        return (c, t.OccupiedRect().Cells.OrderBy(x => x.DistanceToSquared(c)).First());
            return null;
        }

        /// <summary>The bed of theirs this camper sleeps in: the one they hold, else a free one they claim (a bed each).</summary>
        public Building_Bed BedFor(Pawn p)
        {
            Building_Bed free = null;
            foreach (var b in Buildings().OfType<Building_Bed>())
            {
                if (b.Medical || b.ForPrisoners || !b.def.building.bed_humanlike) continue;
                if (b.OwnersForReading.Contains(p)) return b;
                if (free == null && b.AnyUnownedSleepingSlot && RestUtility.IsValidBedFor(b, p, p, false)) free = b;
            }
            if (free != null)
            {
                try
                {
                    p.ownership?.ClaimBedIfNonMedical(free);
                }
                catch (Exception e)
                {
                    CouncilLog.WarnOnce("camp bed claimed: " + e.Message);
                }
            }
            return free;
        }

        /// <summary>
        /// The evening's talk by the fire (each evening hour): one of them sitting there says something to another, a line
        /// of the camp's evening, and the two have the game's own chat.
        /// </summary>
        private void EveningTalk()
        {
            if (CampDayMath.HourOf(GenLocalDate.HourOfDay(map)) != CampHour.Evening) return;
            var sitting = Here.Where(p => p.CurJobDef == CampDefOf.Council_CampSit && Together.Able(p)).ToList();
            if (!Rand.Chance(CampDayMath.TalkChance(sitting.Count))) return;
            var a = sitting.RandomElement();
            var b = sitting.Where(x => x != a && Together.Can(a, x)).RandomElementWithFallback();
            if (b == null) return;
            try
            {
                string line = Flavor.FromPool("Rimfolk_Pool_CampEvening", a, null, null, "their:" + CampKit.AgeOf(faction));
                // together: two of them sitting at their own fire, within talking range (Together.Can).
                if (!line.NullOrEmpty()) Speech.Say(a, line.Formatted(a.Named("PAWN"), b.Named("OTHER")).Resolve(), b);
                a.interactions?.TryInteractWith(b, InteractionDefOf.Chitchat);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp evening talk", e);
            }
        }

        /// <summary>A line of the camp's day work (Rimfolk_Pool_CampWork, by what is done), over a camper's head now and then.</summary>
        public void SayLine(Pawn p, string what, float chance)
        {
            if (p == null || !Rand.Chance(chance) || !Together.Able(p)) return;
            try
            {
                string line = Flavor.FromPool("Rimfolk_Pool_CampWork", p, null, null, what);
                if (!line.NullOrEmpty()) Speech.Say(p, line.Formatted(p.Named("PAWN")).Resolve());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp work line", e);
            }
        }
    }
}
