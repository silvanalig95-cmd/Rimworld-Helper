using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // The council's questions about what the colony meets that nobody asked about before, third round
    // (Defs/DilemmaDefs/Rimfolk_Dilemmas_Variety3.xml): the inspired hands and what they work in, the shakes of someone in
    // withdrawal, the food that won't keep, the stores in the rain, the old walls on the colony's land, the new room and who
    // sleeps in it, what the colony learns next, and the hurt beast. Each comes only while the colony is as its letter says,
    // and each answer's words happen in the game: a bill at the bench for the one inspired, a dose taken or the colonist's
    // drug policy changed, meals cooked, a roof put up or the open part of a stockpile given up, a ruin pulled down or
    // claimed, a bed claimed, a study chosen, an animal marked for the hunters or for taming. The rules without game types
    // are in VarietyThreeMath.

    /// <summary>
    /// What a question of this round is about beyond A, B and {THING}: the bench, bed or ruin it points at
    /// (<see cref="place"/>), the plain stuff ({PLAIN}), the recipe, the stockpile in the rain, and the two studies
    /// ({STUDY1}, {STUDY2}). Saved with the letter.
    /// </summary>
    public class DilemmaContextMore : DilemmaContext
    {
        public Thing place;
        public ThingDef other;
        public RecipeDef recipe;
        public Zone zone;
        public ResearchProjectDef first, second;

        protected override void AddArgs(List<NamedArgument> args)
        {
            base.AddArgs(args);
            args.Add((other?.label ?? "").Named("PLAIN"));
            args.Add((place?.LabelNoCount ?? "").Named("PLACE"));
            args.Add((first?.label ?? "").Named("STUDY1"));
            args.Add((second?.label ?? "").Named("STUDY2"));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref place, "place");
            Scribe_Defs.Look(ref other, "other");
            Scribe_Defs.Look(ref recipe, "recipe");
            Scribe_References.Look(ref zone, "zone");
            Scribe_Defs.Look(ref first, "first");
            Scribe_Defs.Look(ref second, "second");
        }
    }

    /// <summary>Shared look-ups for this round's questions.</summary>
    public static class VarietyThree
    {
        public static List<Pawn> Grown(Map map) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && !p.DevelopmentalStage.Juvenile() && !p.Downed).ToList();

        public static int Level(Pawn p, SkillDef skill) => skill == null ? 0 : p?.skills?.GetSkill(skill)?.Level ?? 0;

        public static bool Does(Pawn p, WorkTypeDef work) => p != null && work != null && !p.WorkTypeIsDisabled(work);

        public static WorkTypeDef Work(string name) => DefDatabase<WorkTypeDef>.GetNamedSilentFail(name);

        /// <summary>The best at <paramref name="skill"/> of those who do <paramref name="work"/>, not <paramref name="not"/>.</summary>
        public static Pawn Best(IEnumerable<Pawn> pawns, SkillDef skill, WorkTypeDef work = null, Pawn not = null) =>
            pawns.Where(p => p != not && (work == null || Does(p, work))).OrderByDescending(p => Level(p, skill)).FirstOrDefault();

        /// <summary>Someone else of the colony, the best at <paramref name="skill"/> (any skill when none fits).</summary>
        public static Pawn Other(IEnumerable<Pawn> pawns, Pawn not, SkillDef skill) =>
            pawns.Where(p => p != not).OrderByDescending(p => Level(p, skill)).FirstOrDefault();

        /// <summary>A bill put first on its bench's stack, so whoever goes to the bench takes this one.</summary>
        public static void First(IBillGiver giver, Bill bill)
        {
            giver.BillStack.AddBill(bill);
            var bills = giver.BillStack.Bills;
            bills.Remove(bill);
            bills.Insert(0, bill);
        }
    }

    // ===================================== The inspired hands =====================================

    /// <summary>The colony's benches that make art, and what a piece costs.</summary>
    public static class ArtBenches
    {
        public static bool IsArt(RecipeDef r) => r?.ProducedThingDef != null && r.ProducedThingDef.IsArt && r.ProducedThingDef.MadeFromStuff && r.AvailableNow;

        public static IEnumerable<Building_WorkTable> Of(Map map) =>
            map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().Where(b => b.Spawned && b.def.AllRecipes != null && b.def.AllRecipes.Any(IsArt) && b.CurrentlyUsableForBills());

        public static int Cost(RecipeDef r) => r?.ProducedThingDef?.costStuffCount ?? 0;

        public static bool Takes(RecipeDef r, ThingDef stuff) => stuff != null && stuff.IsStuff && r.fixedIngredientFilter != null && r.fixedIngredientFilter.Allows(stuff);

        public static int Stock(Map map, ThingDef stuff) => stuff == null ? 0 : map.resourceCounter.GetCount(stuff);

        /// <summary>
        /// The piece, the colony's fine stuff and its plain one: the biggest piece of art a bench makes that both can pay
        /// for, the fine worth <see cref="VarietyThreeMath.FineFactor"/> times the plain a unit.
        /// </summary>
        public static bool TryChoose(Map map, out Building_WorkTable bench, out RecipeDef recipe, out ThingDef fine, out ThingDef plain)
        {
            bench = null;
            recipe = null;
            fine = plain = null;
            var counted = map.resourceCounter.AllCountedAmounts;
            foreach (var b in Of(map))
                foreach (var r in b.def.AllRecipes.Where(IsArt).OrderByDescending(Cost))
                {
                    int cost = Cost(r);
                    if (cost <= 0) continue;
                    var have = counted.Where(kv => kv.Value >= cost && Takes(r, kv.Key)).Select(kv => kv.Key).ToList();
                    if (have.Count < 2) continue;
                    var f = have.OrderByDescending(d => d.BaseMarketValue).First();
                    var p = have.OrderBy(d => d.BaseMarketValue).First();
                    if (!VarietyThreeMath.Fine(f.BaseMarketValue, p.BaseMarketValue)) continue;
                    bench = b;
                    recipe = r;
                    fine = f;
                    plain = p;
                    return true;
                }
            return false;
        }
    }

    /// <summary>
    /// Someone of the colony is inspired to make a piece of art (the game's own inspiration), there's a bench that makes it,
    /// and the colony has both a fine material for it ({THING}: gold, silver, jade) and a plain one ({PLAIN}: wood, stone).
    /// A is the inspired one, B the colony's best crafter, who would keep the fine stuff for other work.
    /// </summary>
    public class DilemmaWorker_Inspired : DilemmaWorker
    {
        public static bool Inspired(Pawn p) =>
            p?.mindState?.inspirationHandler?.CurStateDef == InspirationDefOf.Inspired_Creativity && !(p.skills?.GetSkill(SkillDefOf.Artistic)?.TotallyDisabled ?? true)
            && VarietyThree.Does(p, VarietyThree.Work("Art"));

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var pawns = VarietyThree.Grown(map);
            if (pawns.Count < def.minColonists) return false;
            var a = pawns.Where(p => def.roleA.Allows(p, null) && Inspired(p)).FirstOrDefault();
            if (a == null || !ArtBenches.TryChoose(map, out var bench, out var recipe, out var fine, out var plain)) return false;
            var b = VarietyThree.Best(pawns, SkillDefOf.Crafting, null, a);
            if (b == null) return false;
            ctx = new DilemmaContextMore { def = def, map = map, a = a, b = b, thing = fine, other = plain, recipe = recipe, place = bench, count = ArtBenches.Cost(recipe) };
            return true;
        }

        /// <summary>"precious" when the fine stuff is a precious metal (gold, silver), for the letter's lines.</summary>
        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (ctx?.thing?.stuffProps?.categories?.Contains(StuffCategoryDefOf.Metallic) == true && ctx.thing.BaseMarketValue >= 5f) tags.Add("precious");
        }
    }

    /// <summary>
    /// A bill at the question's bench for its piece of art, worked by A alone in the fine stuff (or, with
    /// <see cref="plain"/>, the plain one), one piece, first on the bench: A goes and makes it while the inspiration lasts.
    /// Can't be chosen once the stuff or the bench is gone.
    /// </summary>
    public class DilemmaEffect_ArtBill : DilemmaEffect
    {
        public bool plain;

        public override bool ChanceEach => false;

        private ThingDef Stuff(DilemmaContext ctx) => plain ? (ctx as DilemmaContextMore)?.other : ctx?.thing;

        private static Building_WorkTable Bench(DilemmaContext ctx) => (ctx as DilemmaContextMore)?.place is Building_WorkTable b && b.Spawned && b.Map == ctx.map ? b : null;

        public override string Unavailable(DilemmaContext ctx)
        {
            var more = ctx as DilemmaContextMore;
            if (more?.recipe == null || ctx.map == null) return null;
            var stuff = Stuff(ctx);
            if (Bench(ctx) == null) return "CC_DlmE_BenchGone".Translate().ToString();
            return ArtBenches.Stock(ctx.map, stuff) < ArtBenches.Cost(more.recipe) ? "CC_DlmE_ArtShort".Translate(stuff?.label ?? "").ToString() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var more = ctx as DilemmaContextMore;
            var stuff = Stuff(ctx);
            if (more?.recipe == null || stuff == null || ctx.a == null || Bench(ctx) == null) return;
            string piece = more.recipe.ProducedThingDef.label;
            sheet.Play("CC_Impact_ArtBill".Translate(ctx.a.LabelShort, piece, stuff.label).Resolve(), "CC_Impact_ArtBillBrief".Translate(ctx.a.LabelShort, stuff.label).Resolve());
            sheet.Add(ImpactKind.Goods, -1, "CC_Impact_ArtSpent".Translate(stuff.label).Resolve(), "CC_Impact_ArtSpentBrief".Translate(stuff.label).Resolve(), "artstuff");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var more = ctx as DilemmaContextMore;
            var bench = Bench(ctx);
            var stuff = Stuff(ctx);
            if (more?.recipe == null || bench == null || stuff == null || ctx.a == null || ctx.a.Dead) return;
            try
            {
                if (!(more.recipe.MakeNewBill() is Bill_Production bill)) return;
                bill.ingredientFilter.SetDisallowAll();
                bill.ingredientFilter.SetAllow(stuff, true);
                bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
                bill.repeatCount = 1;
                bill.SetPawnRestriction(ctx.a);
                VarietyThree.First(bench, bill);
                Tell.Message(NewsKind.Colony, Importance.Notable, ctx.map,
                    "CC_DlmE_ArtBillSet".Translate(ctx.a.Named("PAWN"), more.recipe.ProducedThingDef.label.Named("PIECE"), stuff.label.Named("STUFF"), bench.LabelNoCount.Named("BENCH")).Resolve(),
                    new LookTargets(bench), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("art bill", e);
            }
        }
    }

    // ===================================== The shakes =====================================

    /// <summary>Withdrawal from a drug the colony keeps, and the colony's drug policies.</summary>
    public static class Withdrawal
    {
        /// <summary>Their addiction in withdrawal now (the game's), never one whose withdrawal can kill (luciferium).</summary>
        public static Hediff_Addiction Of(Pawn p)
        {
            var hs = p?.health?.hediffSet?.hediffs;
            if (hs == null) return null;
            foreach (var h in hs)
                if (h is Hediff_Addiction a && a.CurStageIndex == 1 && a.Chemical != null && a.def.lethalSeverity <= 0f && a.Chemical.defName != "Luciferium")
                    return a;
            return null;
        }

        private static bool Gives(ThingDef d, ChemicalDef chem) => d != null && d.IsDrug && d.GetCompProperties<CompProperties_Drug>()?.chemical == chem;

        /// <summary>A stack in the colony's stores of a drug that eases it.</summary>
        public static Thing Drug(Map map, ChemicalDef chem) =>
            chem == null || map == null ? null
                : map.listerThings.ThingsInGroup(ThingRequestGroup.Drug).Where(t => t.Spawned && Gives(t.def, chem) && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer))
                    .OrderByDescending(t => t.stackCount).FirstOrDefault();

        public static Thing Drug(Map map, ThingDef def) =>
            def == null || map == null ? null
                : map.listerThings.ThingsOfDef(def).Where(t => t.Spawned && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer)).OrderByDescending(t => t.stackCount).FirstOrDefault();

        private static DrugPolicyEntry Entry(DrugPolicy policy, ThingDef drug)
        {
            for (int i = 0; i < policy.Count; i++)
                if (policy[i].drug == drug) return policy[i];
            return null;
        }

        /// <summary>One of the colony's own drug policies that never gives this drug (for addiction, for joy or on a schedule): the strictest.</summary>
        public static DrugPolicy Without(ThingDef drug)
        {
            var db = Current.Game?.drugPolicyDatabase;
            if (db == null || drug == null) return null;
            DrugPolicy best = null;
            int bestNone = -1;
            foreach (var policy in db.AllPolicies)
            {
                var e = Entry(policy, drug);
                if (e == null || e.allowedForAddiction || e.allowedForJoy || e.allowScheduled) continue;
                int none = 0;
                for (int i = 0; i < policy.Count; i++)
                    if (!policy[i].allowedForAddiction && !policy[i].allowedForJoy && !policy[i].allowScheduled) none++;
                if (none > bestNone)
                {
                    best = policy;
                    bestNone = none;
                }
            }
            return best;
        }
    }

    /// <summary>
    /// Someone of the colony is in withdrawal from a drug the colony has in its stores ({THING}), the game's own. A is the
    /// one in withdrawal, B the colony's best doctor.
    /// </summary>
    public class DilemmaWorker_Withdrawal : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var pawns = VarietyThree.Grown(map);
            if (pawns.Count < def.minColonists) return false;
            foreach (var a in pawns.Where(p => def.roleA.Allows(p, null) && Withdrawal.Of(p) != null).InRandomOrder())
            {
                var drug = Withdrawal.Drug(map, Withdrawal.Of(a).Chemical);
                if (drug == null) continue;
                var b = VarietyThree.Best(pawns, SkillDefOf.Medicine, WorkTypeDefOf.Doctor, a) ?? VarietyThree.Other(pawns, a, SkillDefOf.Social);
                if (b == null) continue;
                ctx = new DilemmaContextMore { def = def, map = map, a = a, b = b, thing = drug.def };
                return true;
            }
            return false;
        }

        /// <summary>"drink" for alcohol, "smoke" for smokeleaf, for the letter's lines.</summary>
        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            var chem = ctx?.thing?.GetCompProperties<CompProperties_Drug>()?.chemical?.defName;
            if (chem == "Alcohol") tags.Add("drink");
            else if (chem == "Smokeleaf") tags.Add("smoke");
        }
    }

    /// <summary>
    /// A takes one dose of the question's drug from the stores now (the game's own job), which eases the withdrawal and
    /// keeps the habit. Can't be chosen once the stores have none, or the withdrawal has passed.
    /// </summary>
    public class DilemmaEffect_Dose : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx)
        {
            if (ctx?.a == null || ctx.thing == null) return null;
            if (Withdrawal.Of(ctx.a) == null) return "CC_DlmE_ShakesPassed".Translate(ctx.a.Named("PAWN")).Resolve();
            return Withdrawal.Drug(ctx.map, ctx.thing) == null ? "CC_DlmE_DrugGone".Translate(ctx.thing.label).Resolve() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.a == null || ctx.thing == null || Withdrawal.Drug(ctx.map, ctx.thing) == null) return;
            sheet.Play("CC_Impact_Dose".Translate(ctx.a.LabelShort, ctx.thing.label).Resolve(), "CC_Impact_DoseBrief".Translate(ctx.a.LabelShort).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var p = ctx?.a;
            if (p == null || p.Dead || !p.Spawned || p.Map != ctx.map) return;
            try
            {
                var drug = Withdrawal.Drug(ctx.map, ctx.thing);
                if (drug == null || !p.CanReach(drug, PathEndMode.ClosestTouch, Danger.Some)) return;
                var job = JobMaker.MakeJob(JobDefOf.Ingest, drug);
                job.count = 1;
                if (!p.Downed && !p.Drafted && !p.InMentalState && p.Awake()) p.jobs.TryTakeOrderedJob(job, JobTag.Misc, false);
                else p.jobs.jobQueue.EnqueueLast(job, JobTag.Misc);
                Tell.Message(NewsKind.Health, Importance.Notable, ctx.map, "CC_DlmE_DoseTaken".Translate(p.Named("PAWN"), ctx.thing.label.Named("DRUG")).Resolve(), new LookTargets(p), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a dose", e);
            }
        }
    }

    /// <summary>
    /// A's drug policy set to one of the colony's own that never gives the question's drug (the strictest such, "No drugs"
    /// in a new game), so the withdrawal runs its course and the habit is broken, as the game has it. The policy stays
    /// until the player sets another. Can't be chosen when no policy keeps it from them.
    /// </summary>
    public class DilemmaEffect_RideItOut : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx)
        {
            if (ctx?.a?.drugs == null || ctx.thing == null) return null;
            return Withdrawal.Without(ctx.thing) == null ? "CC_DlmE_NoPolicy".Translate(ctx.thing.label).Resolve() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var policy = Withdrawal.Without(ctx?.thing);
            if (ctx?.a?.drugs == null || policy == null) return;
            sheet.Play("CC_Impact_RideOut".Translate(ctx.a.LabelShort, policy.label, ctx.thing.label).Resolve(), "CC_Impact_RideOutBrief".Translate(ctx.a.LabelShort).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var p = ctx?.a;
            var policy = Withdrawal.Without(ctx?.thing);
            if (p?.drugs == null || p.Dead || policy == null) return;
            try
            {
                p.drugs.CurrentPolicy = policy;
                Tell.Message(NewsKind.Health, Importance.Notable, ctx.map, "CC_DlmE_PolicySet".Translate(p.Named("PAWN"), policy.label.Named("POLICY"), ctx.thing.label.Named("DRUG")).Resolve(), new LookTargets(p), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("drug policy", e);
            }
        }
    }

    // ===================================== The food that won't keep =====================================

    /// <summary>Raw food in the colony's stores that will rot soon where it lies, and the stove that could save it.</summary>
    public static class Spoiling
    {
        public static float DaysLeft(Thing t)
        {
            var rot = t.TryGetComp<CompRottable>();
            if (rot == null || rot.Stage != RotStage.Fresh) return -1f;
            return rot.TicksUntilRotAtCurrentTemp / (float)GenDate.TicksPerDay;
        }

        private static bool Raw(ThingDef d) =>
            d.IsNutritionGivingIngestible && d.ingestible != null && !d.IsCorpse && !d.IsDrug && d.ingestible.preferability >= FoodPreferability.RawBad && d.ingestible.preferability <= FoodPreferability.RawTasty;

        /// <summary>Every stack of that kind in the stores rotting within the days, the kind with the most food in it.</summary>
        public static List<Thing> Stacks(Map map, out ThingDef kind, out float nutrition)
        {
            kind = null;
            nutrition = 0f;
            var groups = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(t => t.Spawned && Raw(t.def) && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer) && VarietyThreeMath.SpoilsSoon(DaysLeft(t)))
                .GroupBy(t => t.def).ToList();
            if (groups.Count == 0) return new List<Thing>();
            var best = groups.OrderByDescending(g => g.Sum(t => t.stackCount * t.GetStatValue(StatDefOf.Nutrition))).First();
            kind = best.Key;
            nutrition = best.Sum(t => t.stackCount * t.GetStatValue(StatDefOf.Nutrition));
            return best.ToList();
        }

        public static RecipeDef Meal => DefDatabase<RecipeDef>.GetNamedSilentFail("CookMealSimple");

        /// <summary>A stove of the colony's that cooks simple meals from that food now.</summary>
        public static Building_WorkTable Stove(Map map, ThingDef food)
        {
            var meal = Meal;
            if (meal == null || !meal.AvailableNow || food == null || meal.fixedIngredientFilter == null || !meal.fixedIngredientFilter.Allows(food)) return null;
            return map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>()
                .FirstOrDefault(b => b.Spawned && b.def.AllRecipes != null && b.def.AllRecipes.Contains(meal) && b.CurrentlyUsableForBills());
        }

        /// <summary>The food a simple meal takes, in nutrition.</summary>
        public static float PerMeal(RecipeDef r)
        {
            var first = r?.ingredients?.FirstOrDefault();
            return first == null ? 0.5f : Mathf.Max(0.1f, first.GetBaseCount());
        }
    }

    /// <summary>
    /// Raw food ({THING}) in the colony's stores will rot within a day or so where it lies, enough for {COUNT} meals and
    /// more, and a stove of the colony's could cook it. A is the colony's best cook, B whoever brought it in: the best
    /// hunter for meat, the best grower for the rest.
    /// </summary>
    public class DilemmaWorker_Spoiling : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var pawns = VarietyThree.Grown(map);
            if (pawns.Count < def.minColonists) return false;
            var stacks = Spoiling.Stacks(map, out var kind, out float nutrition);
            if (stacks.Count == 0 || !VarietyThreeMath.SpoilingWorth(nutrition)) return false;
            var stove = Spoiling.Stove(map, kind);
            if (stove == null) return false;
            var cook = VarietyThree.Best(pawns.Where(p => def.roleA.Allows(p, null)), SkillDefOf.Cooking, VarietyThree.Work("Cooking"));
            if (cook == null) return false;
            bool meat = kind.IsMeat;
            var b = meat ? VarietyThree.Best(pawns, SkillDefOf.Shooting, WorkTypeDefOf.Hunting, cook) : VarietyThree.Best(pawns, SkillDefOf.Plants, WorkTypeDefOf.Growing, cook);
            b = b ?? VarietyThree.Other(pawns, cook, SkillDefOf.Cooking);
            if (b == null) return false;
            ctx = new DilemmaContextMore
            {
                def = def, map = map, a = cook, b = b, thing = kind, place = stove, recipe = Spoiling.Meal,
                count = VarietyThreeMath.Meals(nutrition, Spoiling.PerMeal(Spoiling.Meal))
            };
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (ctx?.thing != null) tags.Add(ctx.thing.IsMeat ? "meat" : "crop");
        }
    }

    /// <summary>
    /// A cooking bill at the question's stove: simple meals from the food that won't keep, and only that food ({COUNT}
    /// meals, or with <see cref="supper"/> as many as the colony eats at one supper), first on the stove. Can't be chosen
    /// once the food is gone or the stove can't be used.
    /// </summary>
    public class DilemmaEffect_CookIt : DilemmaEffect
    {
        public bool supper;

        public override bool ChanceEach => false;

        private static Building_WorkTable Stove(DilemmaContext ctx) =>
            (ctx as DilemmaContextMore)?.place is Building_WorkTable b && b.Spawned && b.Map == ctx.map && b.CurrentlyUsableForBills() ? b : null;

        private int Count(DilemmaContext ctx) => supper ? Math.Min(ctx.count, Math.Max(2, ctx.Colonists.Count())) : ctx.count;

        public override string Unavailable(DilemmaContext ctx)
        {
            if (ctx?.map == null || ctx.thing == null) return null;
            if (Stove(ctx) == null) return "CC_DlmE_StoveGone".Translate().ToString();
            return ctx.map.listerThings.ThingsOfDef(ctx.thing).Any(t => t.IsInAnyStorage()) ? null : "CC_DlmE_FoodGone".Translate(ctx.thing.label).Resolve();
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.thing == null || Stove(ctx) == null) return;
            sheet.Play((supper ? "CC_Impact_CookSupper" : "CC_Impact_CookAll").Translate(ctx.a?.LabelShort ?? "", ctx.thing.label).Resolve(),
                (supper ? "CC_Impact_CookSupperBrief" : "CC_Impact_CookAllBrief").Translate(ctx.thing.label).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var more = ctx as DilemmaContextMore;
            var stove = Stove(ctx);
            if (more?.recipe == null || stove == null || ctx.thing == null) return;
            try
            {
                if (!(more.recipe.MakeNewBill() is Bill_Production bill)) return;
                bill.ingredientFilter.SetDisallowAll();
                bill.ingredientFilter.SetAllow(ctx.thing, true);
                bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
                bill.repeatCount = Math.Max(1, Count(ctx));
                VarietyThree.First(stove, bill);
                Tell.Message(NewsKind.Colony, Importance.Notable, ctx.map,
                    "CC_DlmE_CookSet".Translate(bill.repeatCount.Named("N"), ctx.thing.label.Named("FOOD"), stove.LabelNoCount.Named("STOVE")).Resolve(), new LookTargets(stove), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("cooking bill", e);
            }
        }
    }

    // ===================================== The stores in the rain =====================================

    /// <summary>A stockpile of the colony's partly under the open sky, with things lying there that the weather wears.</summary>
    public static class OpenStores
    {
        public static bool Worn(Thing t) => t.def.category == ThingCategory.Item && SteadyEnvironmentEffects.FinalDeteriorationRate(t) > 0.001f;

        public static List<IntVec3> Open(Zone_Stockpile z) => z.Cells.Where(c => !c.Roofed(z.Map)).ToList();

        public static List<Thing> WornThere(Zone_Stockpile z) => Open(z).SelectMany(c => c.GetThingList(z.Map)).Where(Worn).ToList();

        /// <summary>The cells a roof could be put over: a wall or a column close enough to carry it, and not kept open.</summary>
        public static List<IntVec3> Roofable(Zone_Stockpile z) =>
            Open(z).Where(c => !z.Map.areaManager.NoRoof[c] && RoofCollapseUtility.WithinRangeOfRoofHolder(c, z.Map)).ToList();

        public static Zone_Stockpile Worst(Map map) =>
            map.zoneManager.AllZones.OfType<Zone_Stockpile>().Where(z => VarietyThreeMath.RainWorth(Open(z).Count, WornThere(z).Count))
                .OrderByDescending(z => WornThere(z).Count).FirstOrDefault();
    }

    /// <summary>
    /// After rain, a stockpile of the colony's lies partly under the open sky, with things there the weather wears away. A
    /// is the colony's best hauler (who carries the stores), B its best builder.
    /// </summary>
    public class DilemmaWorker_StoresInRain : DilemmaWorker
    {
        private static readonly List<string> Wet = new List<string> { "rain:2/5" };

        public override bool CanHappenNow(Map map) => base.CanHappenNow(map) && (WeatherBook.Of(map)?.Fits(Wet) ?? false);

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var pawns = VarietyThree.Grown(map);
            if (pawns.Count < def.minColonists) return false;
            var zone = OpenStores.Worst(map);
            if (zone == null) return false;
            var b = VarietyThree.Best(pawns, SkillDefOf.Construction, WorkTypeDefOf.Construction);
            var a = pawns.Where(p => p != b && def.roleA.Allows(p, null) && VarietyThree.Does(p, WorkTypeDefOf.Hauling)).InRandomOrder().FirstOrDefault();
            if (a == null || b == null) return false;
            ctx = new DilemmaContextMore { def = def, map = map, a = a, b = b, zone = zone, count = OpenStores.WornThere(zone).Count };
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            var z = (ctx as DilemmaContextMore)?.zone as Zone_Stockpile;
            if (z != null && OpenStores.Open(z).Count == z.Cells.Count) tags.Add("allopen");
        }
    }

    /// <summary>
    /// The open cells of the question's stockpile that a roof can be carried over put in the game's build-roof area: the
    /// builders roof them in their own time. Can't be chosen when nothing there could hold a roof.
    /// </summary>
    public class DilemmaEffect_RoofStores : DilemmaEffect
    {
        public override bool ChanceEach => false;

        private static Zone_Stockpile Zone(DilemmaContext ctx) => (ctx as DilemmaContextMore)?.zone is Zone_Stockpile z && z.Map == ctx.map && z.Cells.Count > 0 ? z : null;

        public override string Unavailable(DilemmaContext ctx)
        {
            var z = Zone(ctx);
            if (z == null) return (ctx as DilemmaContextMore)?.zone != null ? "CC_DlmE_StoresGone".Translate().ToString() : null;
            return OpenStores.Roofable(z).Count == 0 ? "CC_DlmE_NoRoofHolds".Translate().ToString() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var z = Zone(ctx);
            if (z == null || OpenStores.Roofable(z).Count == 0) return;
            bool all = OpenStores.Roofable(z).Count == OpenStores.Open(z).Count;
            sheet.Play((all ? "CC_Impact_RoofAll" : "CC_Impact_RoofPart").Translate(z.label).Resolve(), "CC_Impact_RoofBrief".Translate(z.label).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var z = Zone(ctx);
            if (z == null) return;
            try
            {
                var cells = OpenStores.Roofable(z);
                foreach (var c in cells) z.Map.areaManager.BuildRoof[c] = true;
                if (cells.Count > 0)
                    Tell.Message(NewsKind.Colony, Importance.Notable, ctx.map, "CC_DlmE_RoofMarked".Translate(z.label.Named("ZONE")).Resolve(), new LookTargets(cells.Take(20).Select(c => new TargetInfo(c, z.Map))), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("roof over the stores", e);
            }
        }
    }

    /// <summary>
    /// The open cells taken out of the question's stockpile: what lay there is outside the stores now, and the haulers
    /// carry it to the colony's other stores in their own time. Can't be chosen when none of it is under a roof (the
    /// stockpile would be gone).
    /// </summary>
    public class DilemmaEffect_GiveUpOpen : DilemmaEffect
    {
        public override bool ChanceEach => false;

        private static Zone_Stockpile Zone(DilemmaContext ctx) => (ctx as DilemmaContextMore)?.zone is Zone_Stockpile z && z.Map == ctx.map && z.Cells.Count > 0 ? z : null;

        public override string Unavailable(DilemmaContext ctx)
        {
            var z = Zone(ctx);
            if (z == null) return (ctx as DilemmaContextMore)?.zone != null ? "CC_DlmE_StoresGone".Translate().ToString() : null;
            return OpenStores.Open(z).Count >= z.Cells.Count ? "CC_DlmE_NothingRoofed".Translate().ToString() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var z = Zone(ctx);
            if (z == null || OpenStores.Open(z).Count >= z.Cells.Count) return;
            sheet.Play("CC_Impact_GiveUpOpen".Translate(z.label).Resolve(), "CC_Impact_GiveUpOpenBrief".Translate(z.label).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var z = Zone(ctx);
            if (z == null) return;
            try
            {
                var open = OpenStores.Open(z);
                if (open.Count >= z.Cells.Count) return;
                var things = open.SelectMany(c => c.GetThingList(z.Map)).Where(t => t.def.category == ThingCategory.Item).ToList();
                foreach (var c in open) z.RemoveCell(c);
                // What's left under the roof may be in pieces now: the game makes each piece a stockpile of its own.
                z.CheckContiguous();
                Tell.Message(NewsKind.Colony, Importance.Notable, ctx.map, "CC_DlmE_OpenGivenUp".Translate(z.label.Named("ZONE")).Resolve(),
                    things.Count > 0 ? new LookTargets(things.Take(20)) : new LookTargets(z.Cells.First(), z.Map), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("stores given up", e);
            }
        }
    }

    // ===================================== The old walls =====================================

    /// <summary>The ruin nearest the colony: walls of nobody's, of cut stone or timber, that the colony could pull down or claim.</summary>
    public static class OldWalls
    {
        public static bool Ruin(Thing t, Map map) =>
            t is Building b && b.Spawned && b.Faction == null && b.def.IsWall && b.Stuff != null && !b.def.building.isNaturalRock && !b.Position.Fogged(map)
            && b.DeconstructibleBy(Faction.OfPlayer).Accepted;

        /// <summary>The walls of the ruin nearest the colony, within reach of it: those standing near the first one.</summary>
        public static List<Building> Nearest(Map map)
        {
            var center = StorytellerEvents.Center(map);
            var walls = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial).Where(t => Ruin(t, map) && t.Position.DistanceTo(center) <= 60f).Cast<Building>().ToList();
            var first = walls.OrderBy(w => w.Position.DistanceToSquared(center)).FirstOrDefault();
            if (first == null) return new List<Building>();
            return walls.Where(w => w.Position.DistanceTo(first.Position) <= 12f).ToList();
        }

        public static List<Building> Standing(DilemmaContextMore ctx) =>
            ctx?.place is Building first && first.Spawned && first.Map == ctx.map
                ? ctx.map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial).Where(t => Ruin(t, ctx.map) && t.Position.DistanceTo(first.Position) <= 12f).Cast<Building>().ToList()
                : new List<Building>();
    }

    /// <summary>
    /// An old ruin stands near the colony: walls of nobody's, of {THING}. A is the colony's best builder, who wants the
    /// stone; B the colony's eldest, who would leave them.
    /// </summary>
    public class DilemmaWorker_OldWalls : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var pawns = VarietyThree.Grown(map);
            if (pawns.Count < def.minColonists) return false;
            var walls = OldWalls.Nearest(map);
            if (!VarietyThreeMath.RuinWorth(walls.Count)) return false;
            var a = VarietyThree.Best(pawns.Where(p => def.roleA.Allows(p, null)), SkillDefOf.Construction, WorkTypeDefOf.Construction);
            var b = pawns.Where(p => p != a).OrderByDescending(p => p.ageTracker.AgeBiologicalYearsFloat).FirstOrDefault();
            if (a == null || b == null) return false;
            var stuff = walls.GroupBy(w => w.Stuff).OrderByDescending(g => g.Count()).First().Key;
            ctx = new DilemmaContextMore { def = def, map = map, a = a, b = b, thing = stuff, place = walls.OrderBy(w => w.Position.DistanceToSquared(StorytellerEvents.Center(map))).First(), count = walls.Count };
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (ctx?.thing?.stuffProps?.categories?.Contains(StuffCategoryDefOf.Woody) == true) tags.Add("timber");
            Lands.AddTags(ctx?.map, tags);
        }
    }

    /// <summary>
    /// The ruin's walls marked to be pulled down (the game's deconstruct designation: the builders take them down and the
    /// stone is the colony's), or with <see cref="claim"/> claimed as the colony's own, as the game's claim does, to build
    /// on. Can't be chosen once the ruin is gone.
    /// </summary>
    public class DilemmaEffect_OldWalls : DilemmaEffect
    {
        public bool claim;

        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) =>
            ctx is DilemmaContextMore more && more.place != null && OldWalls.Standing(more).Count == 0 ? "CC_DlmE_RuinGone".Translate().ToString() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!(ctx is DilemmaContextMore more) || OldWalls.Standing(more).Count == 0) return;
            string stuff = ctx.thing?.label ?? "";
            sheet.Play((claim ? "CC_Impact_ClaimWalls" : "CC_Impact_PullDown").Translate(stuff).Resolve(), (claim ? "CC_Impact_ClaimWallsBrief" : "CC_Impact_PullDownBrief").Translate(stuff).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (!(ctx is DilemmaContextMore more)) return;
            try
            {
                var walls = OldWalls.Standing(more);
                if (walls.Count == 0) return;
                var dm = ctx.map.designationManager;
                foreach (var w in walls)
                {
                    if (claim)
                    {
                        w.SetFaction(Faction.OfPlayer);
                        FleckMaker.ThrowMetaPuffs(new TargetInfo(w));
                    }
                    else if (dm.DesignationOn(w, DesignationDefOf.Deconstruct) == null)
                    {
                        dm.RemoveAllDesignationsOn(w);
                        dm.AddDesignation(new Designation(w, DesignationDefOf.Deconstruct));
                    }
                }
                Tell.Message(NewsKind.Colony, Importance.Notable, ctx.map, (claim ? "CC_DlmE_WallsClaimed" : "CC_DlmE_WallsMarked").Translate((ctx.thing?.label ?? "").Named("STUFF")).Resolve(),
                    new LookTargets(walls.Take(20)), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("old walls", e);
            }
        }
    }

    // ===================================== The new room =====================================

    /// <summary>A bedroom of the colony's with one bed nobody owns yet, and who sleeps worse now.</summary>
    public static class NewRooms
    {
        public static float Impressive(Room room) => room == null ? 0f : room.GetStat(RoomStatDefOf.Impressiveness);

        /// <summary>A single bed for a grown-up in a bedroom of its own, nobody's yet.</summary>
        public static Building_Bed Free(Map map) =>
            map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>()
                .Where(b => b.Spawned && !b.Medical && !b.ForPrisoners && b.def.building.bed_humanlike && b.SleepingSlotsCount == 1 && b.OwnersForReading.Count == 0
                            && b.ForOwnerType == BedOwnerType.Colonist && b.GetRoom() is Room r && r.Role == RoomRoleDefOf.Bedroom && r.ContainedBeds.Count() == 1)
                .OrderByDescending(b => Impressive(b.GetRoom())).FirstOrDefault();

        /// <summary>Where they sleep now, as the game rates it: 0 for no bed of their own; a shared room for less.</summary>
        public static float Theirs(Pawn p)
        {
            var bed = p.ownership?.OwnedBed;
            if (bed == null) return 0f;
            var room = bed.GetRoom();
            return room != null && room.Role == RoomRoleDefOf.Bedroom ? Impressive(room) : Impressive(room) * 0.5f;
        }

        /// <summary>Someone who would sleep better there: a grown-up who sleeps alone in a shared room, or has no bed of their own.</summary>
        public static bool Wants(Pawn p, Building_Bed bed)
        {
            if (p?.ownership == null || p.IsSlave || p.DevelopmentalStage.Juvenile()) return false;
            if (LovePartnerRelationUtility.ExistingLovePartner(p) is Pawn partner && partner.Spawned && partner.Map == p.Map) return false;
            var own = p.ownership.OwnedBed;
            if (own != null && own.GetRoom()?.Role == RoomRoleDefOf.Bedroom) return false;
            return VarietyThreeMath.BetterRoom(Impressive(bed.GetRoom()), Theirs(p));
        }

        public static int Days(Pawn p) => p?.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0;
    }

    /// <summary>
    /// A new bedroom stands with one bed nobody owns, and two of the colony would sleep better in it: those who sleep in a
    /// shared room or have no bed of their own. A has been here longest, B the next.
    /// </summary>
    public class DilemmaWorker_NewRoom : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var pawns = VarietyThree.Grown(map);
            if (pawns.Count < def.minColonists) return false;
            var bed = NewRooms.Free(map);
            if (bed == null) return false;
            var want = pawns.Where(p => def.roleA.Allows(p, null) && NewRooms.Wants(p, bed)).OrderByDescending(NewRooms.Days).ToList();
            if (want.Count < 2) return false;
            ctx = new DilemmaContextMore { def = def, map = map, a = want[0], b = want[1], place = bed };
            return true;
        }
    }

    /// <summary>
    /// The target (A unless given) claims the question's bed, the game's own ownership: it is theirs to sleep in from
    /// tonight. Can't be chosen once someone has it.
    /// </summary>
    public class DilemmaEffect_ClaimBed : DilemmaEffect
    {
        public override bool ChanceEach => false;

        private Pawn Who(DilemmaContext ctx) => target == DilemmaTarget.B ? ctx?.b : ctx?.a;

        private static Building_Bed Bed(DilemmaContext ctx) => (ctx as DilemmaContextMore)?.place is Building_Bed b && b.Spawned && b.Map == ctx.map ? b : null;

        public override string Unavailable(DilemmaContext ctx)
        {
            if (!(ctx is DilemmaContextMore more) || more.place == null) return null;
            var bed = Bed(ctx);
            return bed == null || bed.OwnersForReading.Count > 0 ? "CC_DlmE_BedTaken".Translate().ToString() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var p = Who(ctx);
            var bed = Bed(ctx);
            if (p == null || bed == null || bed.OwnersForReading.Count > 0) return;
            sheet.Play("CC_Impact_ClaimBed".Translate(p.LabelShort).Resolve(), "CC_Impact_ClaimBedBrief".Translate(p.LabelShort).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var p = Who(ctx);
            var bed = Bed(ctx);
            if (p?.ownership == null || p.Dead || bed == null || bed.OwnersForReading.Count > 0) return;
            try
            {
                if (p.ownership.ClaimBedIfNonMedical(bed))
                    Tell.Message(NewsKind.Colony, Importance.Notable, ctx.map, "CC_DlmE_BedClaimed".Translate(p.Named("PAWN")).Resolve(), new LookTargets(bed), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("new room", e);
            }
        }
    }

    // ===================================== What to learn next =====================================

    /// <summary>The studies the colony could take up now, and whether its benches are idle.</summary>
    public static class Studies
    {
        public static bool Idle => Find.ResearchManager?.GetProject() == null;

        public static List<ResearchProjectDef> Open() =>
            DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(p => p.knowledgeCategory == null && p.CanStartNow).OrderBy(p => p.CostApparent).ToList();

        public static bool Bench(Map map) => map.listerBuildings.allBuildingsColonist.Any(b => b is Building_ResearchBench && b.Spawned);

        public static bool Researches(Pawn p) => VarietyThree.Does(p, WorkTypeDefOf.Research) && !(p.skills?.GetSkill(SkillDefOf.Intellectual)?.TotallyDisabled ?? true);
    }

    /// <summary>
    /// The colony's study benches stand idle with nothing chosen to learn, and there is more than one study it could take
    /// up. A is the colony's best researcher, who would take the nearest one ({STUDY1}); B the best at another trade,
    /// who wants a bigger one ({STUDY2}).
    /// </summary>
    public class DilemmaWorker_NextStudy : DilemmaWorker
    {
        public override bool CanHappenNow(Map map) => base.CanHappenNow(map) && Studies.Idle && Studies.Bench(map);

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var pawns = VarietyThree.Grown(map);
            if (pawns.Count < def.minColonists) return false;
            var open = Studies.Open();
            if (open.Count < 2) return false;
            var a = VarietyThree.Best(pawns.Where(p => def.roleA.Allows(p, null) && Studies.Researches(p)), SkillDefOf.Intellectual);
            if (a == null) return false;
            var b = pawns.Where(p => p != a).OrderByDescending(p => p.skills?.skills?.Where(s => s.def != SkillDefOf.Intellectual).Select(s => s.Level).DefaultIfEmpty(0).Max() ?? 0).FirstOrDefault();
            if (b == null) return false;
            var first = open[0];
            var second = open.Skip(1).OrderByDescending(p => p.CostApparent).Take(3).RandomElement();
            ctx = new DilemmaContextMore { def = def, map = map, a = a, b = b, first = first, second = second };
            return true;
        }
    }

    /// <summary>
    /// The colony's study set to the question's first (or, with <see cref="second"/>, its second): the researchers go to
    /// their benches and work at it, as the game's research tab would have them. Can't be chosen once it can't be taken up,
    /// or another study has been chosen meanwhile.
    /// </summary>
    public class DilemmaEffect_Study : DilemmaEffect
    {
        public bool second;

        public override bool ChanceEach => false;

        private ResearchProjectDef Of(DilemmaContext ctx) => second ? (ctx as DilemmaContextMore)?.second : (ctx as DilemmaContextMore)?.first;

        public override string Unavailable(DilemmaContext ctx)
        {
            var proj = Of(ctx);
            if (proj == null) return null;
            if (!Studies.Idle) return "CC_DlmE_StudyChosen".Translate().ToString();
            return proj.CanStartNow ? null : "CC_DlmE_StudyClosed".Translate(proj.label).Resolve();
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var proj = Of(ctx);
            if (proj == null || !proj.CanStartNow || !Studies.Idle) return;
            sheet.Play("CC_Impact_Study".Translate(proj.label).Resolve(), "CC_Impact_StudyBrief".Translate(proj.label).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var proj = Of(ctx);
            if (proj == null || !proj.CanStartNow || !Studies.Idle) return;
            try
            {
                Find.ResearchManager.SetCurrentProject(proj);
                Tell.Message(NewsKind.Colony, Importance.Notable, ctx.map, "CC_DlmE_StudySet".Translate(proj.label.Named("STUDY")).Resolve(), LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("next study", e);
            }
        }
    }

    // ===================================== The hurt beast =====================================

    /// <summary>
    /// A wild animal lies hurt and down on the colony's land ({X}), not a manhunter, nobody's. A is the colony's best
    /// handler, B its best hunter.
    /// </summary>
    public class DilemmaWorker_HurtBeast : DilemmaWorker
    {
        public static bool Hurt(Pawn x, Map map) =>
            x != null && x.Spawned && !x.Dead && x.Downed && x.Faction == null && x.RaceProps.Animal && !x.IsWildMan() && !x.InMentalState && !x.Position.Fogged(map)
            && map.designationManager.DesignationOn(x) == null;

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var pawns = VarietyThree.Grown(map);
            if (pawns.Count < def.minColonists) return false;
            var center = StorytellerEvents.Center(map);
            var x = map.mapPawns.AllPawnsSpawned.Where(p => Hurt(p, map) && p.Position.DistanceTo(center) <= 60f).OrderByDescending(p => p.BodySize).FirstOrDefault();
            if (x == null) return false;
            var a = VarietyThree.Best(pawns.Where(p => def.roleA.Allows(p, null)), SkillDefOf.Animals, WorkTypeDefOf.Handling);
            var b = VarietyThree.Best(pawns.Where(p => !p.WorkTagIsDisabled(WorkTags.Violent)), SkillDefOf.Shooting, WorkTypeDefOf.Hunting, a);
            if (a == null || b == null) return false;
            ctx = new DilemmaContext { def = def, map = map, a = a, b = b, stranger = x };
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (ctx?.stranger != null && ctx.stranger.RaceProps.predator) tags.Add("predator");
            if (ctx?.stranger != null && !TameUtility.CanTame(ctx.stranger)) tags.Add("untameable");
        }
    }

    /// <summary>
    /// The question's animal or wild one ({X}) marked with the game's own designation: for the hunters (<see cref="tame"/>
    /// false) or for taming (true: the handlers try once it's on its feet and can be fed). Names the game's own risk that a
    /// failed tame turns it on the handler, before the answer. Can't be chosen once it's gone, or for taming when it can't
    /// be tamed.
    /// </summary>
    public class DilemmaEffect_MarkBeast : DilemmaEffect
    {
        public bool tame;

        public override bool ChanceEach => false;

        private static bool Here(Pawn x, Map map) => x != null && !x.Dead && x.Spawned && x.Map == map && x.Faction == null;

        private DesignationDef Mark => tame ? DesignationDefOf.Tame : DesignationDefOf.Hunt;

        public override string Unavailable(DilemmaContext ctx)
        {
            var x = ctx?.stranger;
            if (x == null) return null;
            if (!Here(x, ctx.map)) return "CC_DlmE_BeastGone".Translate(x.LabelShort).Resolve();
            return tame && !TameUtility.CanTame(x) ? "CC_DlmE_CantTame".Translate(x.LabelShort).Resolve() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var x = ctx?.stranger;
            if (!Here(x, ctx?.map) || tame && !TameUtility.CanTame(x)) return;
            sheet.Play((tame ? "CC_Impact_MarkTame" : "CC_Impact_MarkHunt").Translate(x.LabelShort).Resolve(), (tame ? "CC_Impact_MarkTameBrief" : "CC_Impact_MarkHuntBrief").Translate(x.LabelShort).Resolve());
            if (!tame) return;
            float turn = 0f;
            try
            {
                turn = PawnUtility.GetManhunterOnTameFailChance(x);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("tame risk", e);
            }
            if (turn >= 0.01f)
                sheet.Add(ImpactKind.Health, -1, "CC_Impact_TameRisk".Translate(x.LabelShort).Resolve(), "CC_Impact_TameRiskBrief".Translate().Resolve(), "tamerisk");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var x = ctx?.stranger;
            if (!Here(x, ctx?.map) || tame && !TameUtility.CanTame(x)) return;
            try
            {
                var dm = x.Map.designationManager;
                dm.RemoveAllDesignationsOn(x);
                dm.AddDesignation(new Designation(x, Mark));
                Tell.Message(NewsKind.Colony, Importance.Notable, ctx.map, (tame ? "CC_DlmE_MarkedTame" : "CC_DlmE_MarkedHunt").Translate(x.Named("X")).Resolve(), new LookTargets(x), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("beast marked", e);
            }
        }
    }
}
