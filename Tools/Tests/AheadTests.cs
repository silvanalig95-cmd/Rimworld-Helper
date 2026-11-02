using System;
using System.Collections.Generic;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// Planning ahead (Building/AheadMath.cs) and the shared suggestion core (Building/Suggest/SuggestMath.cs): days
/// left, targets cut by spoiling and room, a crop's nutrition a cell a day, the year's growing days, cells for a
/// shortfall, over-supply by nutrition (never by count), whether a step worked, the order and keeping of
/// suggestions; and the reference colony (the player's save, day 96) planned end to end.
/// </summary>
public static class AheadTests
{
    private static bool Near(float a, float b, float tol = 0.01f) => Math.Abs(a - b) <= tol;

    public static int Run(Action<bool, string> check, bool print = false)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "ahead: " + what);
        }

        // ---------- Eaters and stocks ----------
        C(Near(AheadMath.NutritionADay(2.6666667E-05f), 1.6f), "a grown colonist's hunger at 'fed' is 1.6 nutrition a day");
        C(Near(AheadMath.RawFor(0.9f), 0.5f) && AheadMath.RawFor(-1f) == 0f, "a meal of 0.9 nutrition takes 0.5 of ingredients");
        C(Near(AheadMath.DaysLeft(1.8f, 27.2f), 0.066f, 0.001f) && AheadMath.DaysLeft(5f, 0f) == AheadMath.Far, "two meals for seventeen eaters last a fifteenth of a day; nothing used never runs out");
        C(AheadMath.KeepDays(4f, 0f, false) == 2f && AheadMath.KeepDays(4f, 0f, true) == AheadMath.Far && AheadMath.KeepDays(4f, 0.6f, false) == AheadMath.Far
          && AheadMath.KeepDays(AheadMath.Far, 0f, false) == AheadMath.Far, "meals keep two days out of a freezer (half their four before rot); frozen or never rotting, no limit");
        C(Near(AheadMath.Target(27.2f, 3f, 2f), 54.4f) && Near(AheadMath.Target(27.2f, 3f, AheadMath.Far, 30f), 30f) && AheadMath.Target(-1f, 3f, 2f) == 0f,
            "a target is a day's use times the days, cut to what keeps and to the room");
        C(AheadMath.Status(1.8f, 27.2f, 54.4f) == AheadMath.Short && AheadMath.Status(30f, 10f, 50f) == AheadMath.Low && AheadMath.Status(50f, 10f, 50f) == AheadMath.Enough
          && AheadMath.Status(120f, 10f, 50f) == AheadMath.More && AheadMath.Status(0f, 0f, 0f) == AheadMath.NoUse && AheadMath.Status(9f, 10f, 5f) == AheadMath.Short,
            "short under a day or a quarter, low under three quarters, more than needed over twice; under a day is short whatever the target");
        C(AheadMath.StorageCells(2390f, 75f) == 32 && AheadMath.StorageCells(0f, 75f) == 0 && AheadMath.StorageCells(10f, 0f) == 10, "storage cells by the stack");
        C(Near(AheadMath.NutritionPerCell(0.05f, 75f), 3.75f), "a full cell of rice holds 3.75 nutrition");
        C(AheadMath.RoundUp5(59f) == 60 && AheadMath.RoundUp5(60f) == 60 && AheadMath.RoundUp5(0.5f) == 1 && AheadMath.RoundUp5(61f) == 65, "bill counts round up to fives");

        // ---------- Crops ----------
        C(Near(AheadMath.FertilityFactor(1.4f, 1f), 1.4f) && Near(AheadMath.FertilityFactor(0.7f, 0.4f), 0.88f) && Near(AheadMath.FertilityFactor(1f, 0.5f), 1f),
            "soil: rich soil 140% for a crop that minds it, potatoes on gravel 88%");
        C(AheadMath.TemperatureFactor(-5f, 0f, 6f, 42f, 58f) == 0f && Near(AheadMath.TemperatureFactor(3f, 0f, 6f, 42f, 58f), 0.5f) && AheadMath.TemperatureFactor(20f, 0f, 6f, 42f, 58f) == 1f
          && Near(AheadMath.TemperatureFactor(50f, 0f, 6f, 42f, 58f), 0.5f) && AheadMath.TemperatureFactor(60f, 0f, 6f, 42f, 58f) == 0f, "growth by temperature as the game reckons it");
        C(Near(AheadMath.CycleDays(3f, 1f), 3f / 0.55f + 0.5f) && AheadMath.CycleDays(3f, 0.5f) > AheadMath.CycleDays(3f, 1f), "rice takes 5.5 days resting at night, and half a day to replant; poorer soil, longer");
        float rice = AheadMath.PerCellADay(6f, 0.05f, 3f, 1f), corn = AheadMath.PerCellADay(22f, 0.05f, 11.3f, 1f), potato = AheadMath.PerCellADay(11f, 0.05f, 5.8f, 1f);
        C(rice > 0.045f && rice < 0.055f && corn > 0.045f && corn < 0.055f && potato > 0.045f && potato < 0.055f,
            $"rice, corn and potatoes each give about 0.05 nutrition a cell a day ({rice:0.0000}, {corn:0.0000}, {potato:0.0000})");
        C(AheadMath.PerCellADay(6f, 0.05f, 0f, 1f) == 0f && Near(AheadMath.PerCellADay(6f, 0.05f, 3f, 1f, 0.5f), rice / 2f, 0.0001f), "no grow days, nothing; half the yield, half the food");
        C(Near(1.6f / rice, 1.6f / rice) && 1.6f / rice > 25f && 1.6f / rice < 35f, $"one colonist eats the rice of about 30 cells of full growth ({1.6f / rice:0})");
        var flat = Enumerable.Repeat(20f, 12).ToArray();
        var temps = AheadMath.DayTemperatures(new float[] { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110 }, 0, 60);
        C(Near(temps[2], 0f) && Near(temps[7], 10f) && Near(temps[0], 44f) && temps.Length == 60, "a twelfth's own temperature in its middle, drawn straight in between, round the year");
        C(AheadMath.DayTemperatures(flat, 59, 3).All(t => Near(t, 20f)), "a land the same all year stays the same across the year's end");
        var cold = AheadMath.GrowFactors(AheadMath.DayTemperatures(new float[] { 6, 10, 14, 18, 21, 22, 20, 15, 9, 2, -4, -2 }, 36, 60), 0f, 6f, 42f, 58f);
        float grow = AheadMath.GrowingDays(cold);
        int without = AheadMath.DaysWithout(cold);
        C(grow > 40f && grow < 55f && without >= 8 && without <= 20, $"a temperate year from day 36: {grow:0} growing days in the next 60, {without} without");
        C(AheadMath.CellsFor(10f, 2.5f) == 4 && AheadMath.CellsFor(0f, 2.5f) == 0 && AheadMath.CellsFor(10f, 0f) == 0, "cells for a shortfall");
        C(AheadMath.RunsShortIn(10f, 5f, new float[] { 0f, 0f, 0f, 0f }) == 2f && AheadMath.RunsShortIn(10f, 5f, new float[] { 5f, 5f }) == AheadMath.Far
          && Near(AheadMath.RunsShortIn(10f, 5f, new float[] { 4f, 4f, 4f, 4f, 4f, 4f, 4f, 4f, 4f, 4f, 4f, 4f }), 10f), "a stock runs short by its use less each day's make");
        C(AheadMath.RunsShortIn(10f, 0f, new float[0]) == AheadMath.Far && Near(AheadMath.RunsShortIn(10f, 5f, new float[0]), 2f), "no use, never; no days to go by, the plain days left");

        // ---------- Whether a step worked ----------
        C(AheadMath.StockStepWorked(1.8f, 20f, 54.4f, 0f, 27.2f) && !AheadMath.StockStepWorked(1.8f, 3f, 54.4f, 0f, 27.2f) && AheadMath.StockStepWorked(1.8f, 3f, 54.4f, 20f, 27.2f)
          && AheadMath.StockStepWorked(1.8f, 45f, 54.4f, 0f, 27.2f), "a bill step worked when the stock rose a quarter of the way, or the bench made half a day's use");
        C(AheadMath.SownWorked(40, 20) && !AheadMath.SownWorked(40, 19) && !AheadMath.SownWorked(0, 0), "a field step worked once half its cells are sown");

        // ---------- The shared suggestion core ----------
        C(SuggestMath.Compare(0.1f, 0, "b", 3f, 4, "a") < 0 && SuggestMath.Compare(SuggestMath.NoRush, 3, "b", SuggestMath.NoRush, 1, "a") < 0
          && SuggestMath.Compare(5f, 0, "a", 5f, 0, "b") < 0 && SuggestMath.Compare(2000f, 0, "a", SuggestMath.NoRush, 0, "b") < 0, "the soonest short first, then the higher level, then the key");
        C(SuggestMath.LevelOf(0.1f) == 4 && SuggestMath.LevelOf(1f) == 3 && SuggestMath.LevelOf(4f) == 2 && SuggestMath.LevelOf(10f) == 1 && SuggestMath.LevelOf(40f) == 0, "levels by days");
        C(SuggestMath.DeclineUntil(1000, 0f) == -1 && SuggestMath.DeclineUntil(1000, 2f) == 121000 && SuggestMath.StillDeclined(-1, 999999) && SuggestMath.StillDeclined(121000, 120999)
          && !SuggestMath.StillDeclined(121000, 121000), "'Not this' holds for good, or for its days");
        C(!SuggestMath.CheckDue(0, 60000, 0, 59999) && SuggestMath.CheckDue(0, 60000, 0, 60000) && !SuggestMath.CheckDue(0, 60000, 1, 100000) && SuggestMath.CheckDue(0, 60000, 1, 120000),
            "a step is looked at a wait after it was done, and again a wait later");
        C(SuggestMath.AfterLook(true, 0) == SuggestMath.Worked && SuggestMath.AfterLook(false, 0) == SuggestMath.Waiting && SuggestMath.AfterLook(false, 1) == SuggestMath.Failed,
            "not working at the first look gets a second; at the last it is stuck");
        C(SuggestMath.OfSource("base:stubs", new[] { "base" }) && SuggestMath.OfSource("plan:research", new[] { "adv", "plan" }) && !SuggestMath.OfSource("basement:x", new[] { "base" })
          && SuggestMath.OfSource("line:12:leak", new[] { "line:12" }) && !SuggestMath.OfSource("line:123:leak", new[] { "line:12" }) && SuggestMath.OfSource("anything", null)
          && !SuggestMath.OfSource(null, new[] { "base" }), "a key belongs to a source by its start and a colon");
        var kept = Enumerable.Range(0, 30).ToList();
        C(SuggestMath.TrimOldest(kept, SuggestMath.DoneKept) == 10 && kept.First() == 10 && SuggestMath.TrimOldest(kept, SuggestMath.DoneKept) == 0, "the log keeps the newest");

        // ---------- The reference colony (the player's save, day 96) ----------
        var (input, fields) = Reference();
        var plan = AheadMath.Plan(input);
        if (print) Print(plan);
        var meal = plan.Row(AheadMath.Meal);
        var raw = plan.Row(AheadMath.Raw);
        C(meal != null && Near(meal.have, 1.8f) && Near(meal.useADay, 27.2f) && meal.daysLeft < 0.1f && meal.status == AheadMath.Short,
            $"two meals for 9 colonists and 8 guests: 1.8 nutrition against 27.2 a day, {meal?.daysLeft:0.00} days, short");
        C(meal != null && Near(meal.keepDays, 2f) && Near(meal.target, 54.4f), $"meals to keep: two days, as far as they keep out of a freezer ({meal?.target:0.#})");
        var raise = plan.ideas.FirstOrDefault(i => i.kind == "raise");
        C(raise != null && raise.bill.product == "MealSimple" && raise.from == 5 && raise.to == 60 && plan.ideas.First() == raise,
            $"the first suggestion: raise the simple meal bill from 5 to 60 ({raise?.bill?.product} {raise?.from} to {raise?.to})");
        C(!plan.ideas.Any(i => i.bill?.product == "MealFine"), "the fine meal bill at 2 is left as it is");
        C(!plan.ideas.Any(i => i.kind == "second" || i.kind == "add"), "one stove that can keep up and bills already set: no second bench, no new bill");
        C(raw != null && Near(raw.useADay, 27.2f * AheadMath.RawPerMeal) && raw.status <= AheadMath.Low, $"raw food: the cooks need {raw?.useADay:0.#} a day; {raw?.have:0.#} in store is {StatusName(raw?.status ?? -1)}");
        var chick = raw?.parts.FirstOrDefault(g => g.key == "Chickpeas");
        var cornGood = raw?.parts.FirstOrDefault(g => g.key == "RawCorn");
        C(chick != null && cornGood != null && chick.count > 5 * cornGood.count && chick.Nutrition < 4 * cornGood.Nutrition,
            $"chickpeas: {chick?.count} pieces, five times the corn's, but {chick?.Nutrition:0} nutrition against {cornGood?.Nutrition:0}");
        var chickField = fields.First(f => f.crop == "Plant_Chickpea");
        C(!AheadMath.OverSupplied(chickField, plan.cropNeed) && chickField.stockNutrition / plan.cropNeed < 10f,
            $"the chickpea field is judged by nutrition: {chickField.stockNutrition / plan.cropNeed:0.#} days of the colony's crop food, not over-supplied");
        float byCount = chickField.stock / (input.goods.Where(g => g.kind == AheadMath.Raw).Sum(g => g.count));
        C(byCount > 0.7f, $"by count alone the chickpeas would look like {byCount:P0} of the food");
        C(!plan.ideas.Any(i => i.kind == "switch" && i.field?.crop == "Plant_Chickpea"), "no suggestion to switch the chickpea field");
        C(plan.cropNeed > 14f && plan.cropNeed < 16f && plan.cropGive < plan.cropNeed && plan.cellsShort > 50,
            $"fields: need {plan.cropNeed:0.#} a day, give {plan.cropGive:0.#}, about {plan.cellsShort} cells short");
        var fieldIdeas = plan.ideas.Where(i => i.area == "fields").ToList();
        C(fieldIdeas.Count >= 1 && fieldIdeas.All(i => i.cells >= AheadMath.MinCellsAStep && i.cells <= AheadMath.MaxCellsAStep) && fieldIdeas.Any(i => i.kind == "grow"),
            $"the fields grow in steps of at most {AheadMath.MaxCellsAStep} cells: " + string.Join(", ", fieldIdeas.Select(i => i.kind + " " + i.cells)));
        C(fieldIdeas.All(i => i.kind != "grow" || i.field.food), "only food fields are grown");
        C(plan.ideas.Select(i => i.key).Distinct().Count() == plan.ideas.Count, "every suggestion has its own key");

        // Setting a suggestion aside gives its share to the next way.
        var firstGrow = fieldIdeas.First(i => i.kind == "grow");
        input.declined = new HashSet<string> { firstGrow.key, raise.key };
        var again = AheadMath.Plan(input);
        C(!again.ideas.Any(i => i.key == firstGrow.key || i.key == raise.key), "what the player set aside isn't suggested");
        C(again.ideas.Any(i => i.area == "fields"), "the fields' shortfall goes to the next field or a new one");
        C(again.ideas.Any(i => i.kind == "raise" && i.bill.product == "MealFine"), "with the simple meal bill set aside, the fine meal bill is raised instead");
        input.declined = new HashSet<string>();

        // An over-supplied crop is switched by days of use or nutrition, never by count.
        var hops = fields.First(f => f.crop == "Plant_Hops");
        hops.stock = 900f;
        hops.useADay = 3f;
        var withHops = AheadMath.Plan(input);
        var sw = withHops.ideas.FirstOrDefault(i => i.kind == "switch");
        C(AheadMath.OverSupplied(hops, withHops.cropNeed) && sw != null && sw.field == hops && sw.cells <= hops.cells, $"900 hops at 3 a day (300 days): {sw?.cells} cells switched to {sw?.crop?.label}");
        hops.stock = 0f;
        hops.useADay = 0f;

        // Meals that would rot: the bill is lowered.
        var many = Reference().input;
        many.bills.First(b => b.product == "MealSimple").target = 150;
        many.goods.First(g => g.key == "MealSimple").count = 70f;
        var lower = AheadMath.Plan(many).ideas.FirstOrDefault(i => i.kind == "lower");
        C(lower != null && lower.from == 150 && lower.to < 150, $"150 simple meals without a freezer: lowered to {lower?.to}");
        many.freezer = true;
        C(!AheadMath.Plan(many).ideas.Any(i => i.kind == "lower"), "with a freezer, nothing to lower");

        // Everybody Gets One's bills are only read.
        var ego = Reference().input;
        foreach (var b in ego.bills)
        {
            b.managed = true;
            b.mode = AheadMath.Other;
        }
        var egoPlan = AheadMath.Plan(ego);
        C(!egoPlan.ideas.Any(i => i.bill != null) && egoPlan.Row(AheadMath.Meal).managedBills == 2, "bills another mod sets are never changed");

        // A paste dispenser feeds people without stored meals.
        var paste = Reference().input;
        paste.dispenser = true;
        var pastePlan = AheadMath.Plan(paste);
        C(!pastePlan.ideas.Any(i => i.area == AheadMath.Meal) && pastePlan.Row(AheadMath.Raw).useADay > 15f,
            "with a paste dispenser no meal bills are asked for, and the raw food still feeds everyone");

        // No meal bill at all: one is added.
        var none = Reference().input;
        none.bills.Clear();
        none.canAdd.Add(new AheadAdd { key = "stove:CookMealSimple", kind = AheadMath.Meal, product = "MealSimple", label = "simple meal", bench = "fueled stove", unit = 0.9f });
        var add = AheadMath.Plan(none).ideas.FirstOrDefault(i => i.kind == "add");
        C(add != null && add.to == 65, $"no meal bill: add one until {add?.to} (54.4 nutrition of simple meals)");

        // The benches can't keep up: a copy on a second bench.
        var slow = Reference().input;
        foreach (var b in slow.bills) b.canMakeADay = 5f;
        slow.bills.First(b => b.product == "MealSimple").freeBench = "electric stove";
        C(AheadMath.Plan(slow).ideas.Any(i => i.kind == "second" && i.bill.product == "MealSimple"), "ten meals a day from one stove for thirty eaten: the bill copied to the second stove");

        // A land with no winter needs no reserve for one; enough fields, no field steps.
        var warm = Reference().input;
        warm.growFactors = Enumerable.Repeat(1f, 60).ToArray();
        foreach (var f in warm.fields) f.growFactors = null;
        foreach (var f in warm.fields.Where(f => f.food)) f.cells *= 3;
        var warmPlan = AheadMath.Plan(warm);
        C(warmPlan.withoutDays == 0 && !warmPlan.ideas.Any(i => i.area == "fields") && warmPlan.cellsShort == 0, "a warm land with three times the fields: nothing to sow");
        return n;
    }

    private static string StatusName(int s) => s switch { AheadMath.Short => "short", AheadMath.Low => "low", AheadMath.Enough => "enough", AheadMath.More => "more than needed", _ => "?" };

    /// <summary>
    /// The player's save at day 96 (docs/plan-ahead.md): 9 colonists and 8 Hospitality guests, a fine and a simple
    /// meal in store, the stove's bills "until 2" (fine) and "until 5" (simple), raw food chickpeas 2390 (in 35
    /// cells), corn 440, potatoes 268, rice 252; 15 growing zones, wheat four times, healroot 79, drago tree 92, hops 63,
    /// tobacco 52. The fixture's own assumptions: the zones' sizes not given in the save (wheat 4 x 36, chickpeas 35,
    /// corn 40, potatoes 30, rice 30), each with 20 free fertile cells beside it; the crops' numbers as their defs
    /// give them (Vanilla Cooking Expanded's wheat: 27 flour in 15 days), chickpeas (Vanilla Plants Expanded More)
    /// at 14 a harvest in 7 days and 0.03 nutrition each; a temperate land's year from day 36 (autumn).
    /// </summary>
    public static (AheadInput input, List<AheadField> fields) Reference()
    {
        var temps = new float[] { 6, 10, 14, 18, 21, 22, 20, 15, 9, 2, -4, -2 };
        var factors = AheadMath.GrowFactors(AheadMath.DayTemperatures(temps, 36, 60), 0f, 6f, 42f, 58f);
        var x = new AheadInput
        {
            colonists = 9, guests = 8, humanNeed = 17 * 1.6f, measured = false, growFactors = factors, growingLeftThisYear = factors.Take(24).Sum(),
            mealDays = 2f, reserveDays = 5f, freezer = false, mealRotDays = 4f, mealRoom = AheadMath.Far, newGround = AheadMath.MaxCellsAStep
        };
        AheadGood Raw(string key, string label, float count, float nutrition, float rot) =>
            new AheadGood { key = key, label = label, kind = AheadMath.Raw, count = count, nutrition = nutrition, stackLimit = 75f, cells = AheadMath.StorageCells(count, 75f), rotDays = rot };
        x.goods.Add(new AheadGood { key = "MealFine", label = "fine meal", kind = AheadMath.Meal, count = 1, nutrition = 0.9f, stackLimit = 10f, cells = 1, rotDays = 4f });
        x.goods.Add(new AheadGood { key = "MealSimple", label = "simple meal", kind = AheadMath.Meal, count = 1, nutrition = 0.9f, stackLimit = 10f, cells = 1, rotDays = 4f });
        var chick = Raw("Chickpeas", "chickpeas", 2390, 0.03f, 50f);
        chick.cells = 35;
        x.goods.Add(chick);
        x.goods.Add(Raw("RawCorn", "corn", 440, 0.05f, 60f));
        x.goods.Add(Raw("RawPotatoes", "potatoes", 268, 0.05f, 30f));
        x.goods.Add(Raw("RawRice", "rice", 252, 0.05f, 40f));
        x.bills.Add(new AheadBill { key = "stove:CookMealFine", product = "MealFine", label = "fine meal", bench = "fueled stove", kind = AheadMath.Meal, unit = 0.9f, target = 2, canMakeADay = 40f });
        x.bills.Add(new AheadBill { key = "stove:CookMealSimple", product = "MealSimple", label = "simple meal", bench = "fueled stove", kind = AheadMath.Meal, unit = 0.9f, target = 5, canMakeADay = 60f });

        AheadField Food(string key, string crop, string label, string product, int cells, float yield, float nutrition, float growDays, float stock) => new AheadField
        {
            key = key, label = label, crop = crop, cropLabel = label, product = product, productLabel = label, food = true, cells = cells, sown = cells,
            perCellADay = AheadMath.PerCellADay(yield, nutrition, growDays, 1f), freeNext = 20, stock = stock, stockNutrition = stock * nutrition,
            harvest = cells * yield, stockCells = AheadMath.StorageCells(stock, 75f)
        };
        AheadField Other(string key, string crop, string label, int cells) => new AheadField
        {
            key = key, label = label, crop = crop, cropLabel = label, product = crop, productLabel = label, food = false, cells = cells, sown = cells,
            perCellADay = 1f, harvest = cells * 8f
        };
        var fields = new List<AheadField>();
        for (int i = 1; i <= 4; i++) fields.Add(Food("w" + i, "VCE_Wheat", "wheat field " + i, "VCE_Flour", 36, 27f, 0.05f, 15f, 0f));
        fields.Add(Food("c", "Plant_Chickpea", "chickpeas", "Chickpeas", 35, 14f, 0.03f, 7f, 2390f));
        fields.Add(Food("corn", "Plant_Corn", "corn", "RawCorn", 40, 22f, 0.05f, 11.3f, 440f));
        fields.Add(Food("pot", "Plant_Potato", "potatoes", "RawPotatoes", 30, 11f, 0.05f, 5.8f, 268f));
        fields.Add(Food("rice", "Plant_Rice", "rice", "RawRice", 30, 6f, 0.05f, 3f, 252f));
        fields.Add(Other("heal", "Plant_Healroot", "healroot", 79));
        fields.Add(Other("drago", "Plant_DragoTree", "drago tree", 92));
        fields.Add(Other("hops", "Plant_Hops", "hops", 63));
        fields.Add(Other("tob", "Plant_Tobacco", "tobacco", 52));
        x.fields = fields;
        x.crops.Add(new AheadCrop { key = "Plant_Rice", label = "rice", perCellADay = AheadMath.PerCellADay(6f, 0.05f, 3f, 1f) });
        x.crops.Add(new AheadCrop { key = "Plant_Potato", label = "potatoes", perCellADay = AheadMath.PerCellADay(11f, 0.05f, 5.8f, 1f) });
        x.crops.Add(new AheadCrop { key = "Plant_Corn", label = "corn", perCellADay = AheadMath.PerCellADay(22f, 0.05f, 11.3f, 1f) });
        x.crops.Add(new AheadCrop { key = "Plant_Chickpea", label = "chickpeas", perCellADay = AheadMath.PerCellADay(14f, 0.03f, 7f, 1f) });
        return (x, fields);
    }

    /// <summary>The reference colony's plan in words, for a look by hand (set RIMFOLK_AHEAD_PRINT=1).</summary>
    public static void Print(AheadPlan p)
    {
        Console.WriteLine("  Reference colony, the plan:");
        foreach (var r in p.rows)
            Console.WriteLine($"    row {r.kind}: have {r.have:0.#} ({r.count:0} pieces), {r.useADay:0.#} a day, {r.daysLeft:0.##} days, keep {r.target:0.#} ({r.targetDays:0.#} days), {StatusName(r.status)}");
        Console.WriteLine($"    fields: need {p.cropNeed:0.##} a day, give {p.cropGive:0.##}, growing days {p.growingDays:0.#} of {p.cycleDays}, {p.withoutDays} without, {p.growingLeftThisYear:0.#} left this year, {p.cellsShort} cells short, runs short in {p.runsShortIn:0.#}");
        foreach (var i in p.ideas)
            Console.WriteLine($"    {i.kind} [{i.key}] days {i.days:0.##}: {i.bill?.label ?? i.add?.label ?? i.field?.label ?? i.crop?.label} from {i.from} to {i.to}, cells {i.cells}, gives {i.gives:0.##}{(i.crop != null ? " crop " + i.crop.label : "")}");
    }
}
