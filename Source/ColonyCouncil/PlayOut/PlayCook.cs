using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// A feast's cooking. The raw food an answer spends on a feast it sets going isn't taken from the stores when the
    /// answer is given: in the hours before the feast, its organizer (if they cook; else the best cook of those asked)
    /// cooks it into meals at one of the colony's stoves, with a bill of the game's own for the best meal they can make
    /// there and the game's own cooking job, and the feast waits while the pot is on. The meals are served with the rest
    /// of the stores'. Nobody to cook, nothing to cook at, or a feast that never comes about: the food stays in the stores.
    /// A visiting cook who offered to cook the feast (<see cref="CookFromOutside"/>) does it in the colony's place, with
    /// the same bill and job, and leaves the meals by the stove for the colony's haulers.
    /// </summary>
    public static class PlayCook
    {
        private static WorkTypeDef cooking;
        private static WorkTypeDef Cooking => cooking ??= DefDatabase<WorkTypeDef>.GetNamedSilentFail("Cooking");

        /// <summary>When each feast may next look for a stove, so one with nowhere to cook doesn't look every few seconds.</summary>
        private static readonly Dictionary<int, int> nextLook = new Dictionary<int, int>();

        /// <summary>Raw food the answer being given spends: held for the feast it set going, if there is one. False: take it now.</summary>
        public static bool Hold(int units)
        {
            if (units <= 0) return false;
            var feast = PlayOuts.Collected().FirstOrDefault(p => p.kind == PlayKind.Feast && p.state == PlayState.Pending && !p.cookOver);
            if (feast == null) return false;
            feast.cookUnits += units;
            return true;
        }

        /// <summary>
        /// On the feast's clock while it waits to start: puts the pot on when the feast is near, and true while the feast
        /// should wait for the cooking.
        /// </summary>
        public static bool Waits(PlayOutTracker t, PlayOut p, int now)
        {
            if (p.kind != PlayKind.Feast || p.cookUnits <= 0 || p.cookOver || p.map == null) return false;
            int hour = GenLocalDate.HourOfDay(p.map);
            bool due = PlayOutMath.InHours(hour, p.hourFrom, p.hourTo);
            if (!due && PlayOutMath.HoursUntil(hour, p.hourFrom, p.hourTo) > PlayOutMath.CookLead) return false;
            Count(p);
            var bill = p.cookBill;
            // Enough cooked, or time the feast went ahead (it's been cooking a while once due, or it can't wait much
            // longer): the meal in the pot is the last.
            if (bill != null && !bill.DeletedOrDereferenced
                && (p.cooked >= p.cookMeals || PlayOutMath.StopCooking(due, now - p.cookFrom, p.deadline - now)))
                bill.suspended = true;
            if (OnIt(p))
            {
                p.waitReason = "CC_Play_Why_Cooking".Translate(p.cook.Named("PAWN")).Resolve();
                return true;
            }
            // Taken off the stove by hand, or done.
            if (bill != null && (bill.DeletedOrDereferenced || bill.suspended) || PlayOutMath.StopCooking(due, 0, p.deadline - now))
            {
                Over(p);
                return false;
            }
            var cook = p.cook != null && (CanCook(p.cook, p.map) || Outsider(p, p.cook)) ? p.cook : ChooseCook(p);
            bool free = cook != null && (Outsider(p, cook) ? OutsiderFree(cook) : PlayGroups.Free(t, p, cook));
            if (free && p.map.dangerWatcher.DangerRating == StoryDanger.None && Start(p, cook, now))
            {
                p.waitReason = "CC_Play_Why_Cooking".Translate(cook.Named("PAWN")).Resolve();
                return true;
            }
            // Can't cook just now: before the feast, again later; once it's due, a little while for a cook who's busy,
            // then it goes ahead with what there is.
            if (!due) return false;
            if (cook != null && !free && PlayOutMath.WaitForCook(hour, p.hourFrom, p.cooked))
            {
                p.waitReason = "CC_Play_Why_Cooking".Translate(cook.Named("PAWN")).Resolve();
                return true;
            }
            Over(p);
            return false;
        }

        /// <summary>The feast is over or never came about: the bill comes off the stove.</summary>
        public static void Clean(PlayOut p)
        {
            if (p == null || p.kind != PlayKind.Feast) return;
            nextLook.Remove(p.id);
            if (p.cookUnits > 0) Over(p);
        }

        /// <summary>A meal of a feast's bill is done: once there are enough, the bill is set aside so nobody cooks another.</summary>
        public static void Notify_Cooked(Bill_Production bill)
        {
            var p = PlayOutTracker.Instance?.plays.FirstOrDefault(x => x.cookBill == bill);
            if (p == null) return;
            Count(p);
            if (p.cooked >= p.cookMeals) bill.suspended = true;
        }

        private static void Count(PlayOut p)
        {
            var bill = p.cookBill;
            if (bill == null || bill.DeletedOrDereferenced) return;
            // The bill asks one meal more than the feast needs, so the game never calls it finished (and says so).
            p.cooked = Math.Max(p.cooked, p.cookMeals + 1 - bill.repeatCount);
        }

        private static void Over(PlayOut p)
        {
            p.cookOver = true;
            if (p.state == PlayState.Pending) p.waitReason = null;
            var bill = p.cookBill;
            p.cookBill = null;
            try
            {
                if (bill != null && !bill.DeletedOrDereferenced) bill.billStack?.Delete(bill);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feast bill", e);
            }
        }

        private static bool OnIt(PlayOut p)
        {
            var job = p.cook?.CurJob;
            if (job == null || p.cookBill == null) return false;
            if (job.bill == p.cookBill) return true;
            // Fuelling the stove first.
            return job.def == JobDefOf.Refuel && job.targetA.Thing != null && job.targetA.Thing == p.cookBill.billStack?.billGiver as Thing;
        }

        private static bool CanCook(Pawn x, Map map) =>
            x != null && !x.Dead && x.Spawned && x.Map == map && !x.Downed && PlayOuts.OurPerson(x) && Cooking != null
            && !x.WorkTypeIsDisabled(Cooking) && !x.DevelopmentalStage.Baby() && x.health?.capacities != null
            && x.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation);

        /// <summary>
        /// The visitor the feast was set going with cooks it (a traveling cook given the kitchen): set before the pot goes
        /// on, and kept as long as they can do it; if they can't, the colony's own cook takes over as for any feast.
        /// </summary>
        public static void CookFromOutside(PlayOut feast, Pawn x)
        {
            if (feast == null || feast.kind != PlayKind.Feast || feast.cookOver || x == null || feast.other != x || !Outsider(feast, x)) return;
            feast.cook = x;
        }

        /// <summary>The feast's own visitor, able to cook: here and calm, of a people at peace with the colony, and not on their way out.</summary>
        private static bool Outsider(PlayOut p, Pawn x)
        {
            if (x == null || x != p.other || x.Dead || !x.Spawned || x.Map != p.map || x.Downed || x.InMentalState || x.IsPrisoner) return false;
            if (x.Faction == null || x.Faction.IsPlayer || x.HostileTo(Faction.OfPlayer) || Cooking == null || x.WorkTypeIsDisabled(Cooking)) return false;
            if (x.DevelopmentalStage.Juvenile() || x.health?.capacities == null || !x.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return false;
            var duty = x.mindState?.duty?.def;
            return duty == null || !duty.defName.StartsWith("ExitMap");
        }

        /// <summary>The visiting cook can go to the stove now: awake, not eating or resting, not hungry or worn out.</summary>
        private static bool OutsiderFree(Pawn x) =>
            x.Awake() && x.CurJobDef != JobDefOf.Ingest && x.CurJobDef != JobDefOf.LayDown && !PlayOuts.NeedsPress(x);

        /// <summary>The feast's organizer if they cook, else the best cook of those asked.</summary>
        private static Pawn ChooseCook(PlayOut p)
        {
            if (CanCook(p.organizer, p.map)) return p.organizer;
            return p.invited.Where(x => CanCook(x, p.map)).OrderByDescending(x => x.skills?.GetSkill(SkillDefOf.Cooking)?.Level ?? 0).FirstOrDefault();
        }

        private static bool Start(PlayOut p, Pawn cook, int now)
        {
            var bill = p.cookBill;
            if (bill != null)
            {
                if (bill.PawnRestriction != cook) bill.SetPawnRestriction(cook);
                var next = JobFor(cook, bill.billStack?.billGiver as Thing, bill);
                if (next == null) return false;
                p.cook = cook;
                cook.jobs.StartJob(next, JobCondition.InterruptForced, null, false, true);
                return true;
            }
            if (nextLook.TryGetValue(p.id, out int at) && now < at) return false;
            nextLook[p.id] = now + PlayOutMath.TicksPerHour;
            foreach (var (stove, recipe) in Choices(p.map, cook))
            {
                if (!(recipe.MakeNewBill() is Bill_Production b) || !(stove is IBillGiver giver)) continue;
                int meals = PlayOutMath.FeastMeals(p.cookUnits, Units(recipe), p.invited.Count);
                b.repeatMode = BillRepeatModeDefOf.RepeatCount;
                b.repeatCount = meals + 1;
                b.SetPawnRestriction(cook);
                // A visiting cook sets the meals down by the stove: the colony's haulers take them to the stores.
                if (cook.Faction != Faction.OfPlayer) b.SetStoreMode(BillStoreModeDefOf.DropOnFloor);
                giver.BillStack.AddBill(b);
                // First on the stove, so the cook takes this one and not another of the colony's.
                var bills = giver.BillStack.Bills;
                bills.Remove(b);
                bills.Insert(0, b);
                var job = JobFor(cook, stove, b);
                if (job == null)
                {
                    giver.BillStack.Delete(b);
                    continue;
                }
                p.cookBill = b;
                p.cookMeals = meals;
                p.cooked = 0;
                p.cookFrom = now;
                p.cook = cook;
                cook.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                return true;
            }
            return false;
        }

        /// <summary>The game's own job for the bill (or for fuelling the stove before it), else null.</summary>
        private static Job JobFor(Pawn cook, Thing stove, Bill_Production bill)
        {
            if (stove == null || !stove.Spawned) return null;
            var giver = Giver(stove.def);
            if (!(giver?.Worker is WorkGiver_Scanner scanner)) return null;
            Job job;
            try
            {
                job = scanner.JobOnThing(cook, stove, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feast cooking job", e);
                return null;
            }
            if (job == null) return null;
            return job.bill == bill || job.def == JobDefOf.Refuel ? job : null;
        }

        private static readonly Dictionary<ThingDef, WorkGiverDef> givers = new Dictionary<ThingDef, WorkGiverDef>();

        /// <summary>The work giver that cooks at this kind of stove.</summary>
        private static WorkGiverDef Giver(ThingDef def)
        {
            if (def == null) return null;
            if (givers.TryGetValue(def, out var g)) return g;
            var all = DefDatabase<WorkGiverDef>.AllDefsListForReading
                .Where(w => w.giverClass != null && typeof(WorkGiver_DoBill).IsAssignableFrom(w.giverClass) && w.fixedBillGiverDefs != null && w.fixedBillGiverDefs.Contains(def))
                .ToList();
            g = all.FirstOrDefault(w => w.workType == Cooking) ?? all.FirstOrDefault();
            givers[def] = g;
            return g;
        }

        /// <summary>The colony's stoves the cook can get to, with the meals they can make there, the finest first and then the nearest.</summary>
        private static IEnumerable<(Thing, RecipeDef)> Choices(Map map, Pawn cook)
        {
            var list = new List<(Thing stove, RecipeDef recipe)>();
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.PotentialBillGiver))
            {
                if (!(t is IBillGiver) || t.Faction != Faction.OfPlayer || t.IsForbidden(cook) || t.IsForbidden(Faction.OfPlayer) || Giver(t.def) == null) continue;
                if (!cook.CanReach(t, PathEndMode.InteractionCell, Danger.Some)) continue;
                foreach (var r in t.def.AllRecipes)
                    if (Meal(r, cook))
                        list.Add((t, r));
            }
            return list.OrderByDescending(x => (int)x.recipe.ProducedThingDef.ingestible.preferability)
                .ThenBy(x => x.stove.Position.DistanceToSquared(cook.Position));
        }

        /// <summary>A meal for a table, one at a time, that the colony has learned to cook and the cook can make (no travel packs or batches).</summary>
        private static bool Meal(RecipeDef r, Pawn cook)
        {
            if (r == null || !r.AvailableNow || r.workSkill != SkillDefOf.Cooking || r.products == null || r.products.Count != 1 || r.products[0].count != 1) return false;
            var d = r.ProducedThingDef;
            var ing = d?.ingestible;
            if (ing == null || !d.IsNutritionGivingIngestible || d.IsDrug || !ing.HumanEdible || ing.preferability < FoodPreferability.MealSimple) return false;
            if (d.defName == "BabyFood" || Cuisine.IsSpoiledMeal(d) || !d.HasComp(typeof(CompRottable))) return false;
            return r.PawnSatisfiesSkillRequirements(cook);
        }

        /// <summary>How much raw food a meal of the recipe takes, in the stores' own count (a unit of raw food is 0.05 nutrition).</summary>
        private static int Units(RecipeDef r)
        {
            float n = 0f;
            foreach (var i in r.ingredients) n += i.GetBaseCount();
            return r.IngredientValueGetter is IngredientValueGetter_Nutrition ? Math.Max(1, (int)Math.Round(n / 0.05f)) : Math.Max(1, (int)Math.Round(n));
        }
    }

    /// <summary>A meal of a feast's bill is done (PlayOut/PlayCook.cs).</summary>
    [HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.Notify_IterationCompleted))]
    public static class Patch_FeastCooked
    {
        public static void Postfix(Bill_Production __instance)
        {
            try
            {
                PlayCook.Notify_Cooked(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("feast cooked", e);
            }
        }
    }
}
