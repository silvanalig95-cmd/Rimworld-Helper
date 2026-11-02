using System;
using System.Collections.Generic;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// Planning ahead, phase 3 (Building/AheadMoreMath.cs) and the leftovers of phases 1 and 2: Colony Manager's targets
/// raised to what the colony wants and the manager's stalls, the shopping list's sums (each reason's use, the stock
/// counted once, what is marked to mine), rock to mark, spare clothes by wear, hay kept out of the people's crops in
/// the field plan, and the people a quest brings counted in beds and seats.
/// </summary>
public static class AheadMoreTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "ahead more: " + what);
        }

        // ---------- Colony Manager ----------
        C(AheadMoreMath.RoundTarget(37f) == 40 && AheadMoreMath.RoundTarget(200f) == 200 && AheadMoreMath.RoundTarget(201f) == 250 && AheadMoreMath.RoundTarget(1360f) == 1400
          && AheadMoreMath.RoundTarget(0f) == 0, "targets by tens up to 200, by fifties above");
        C(AheadMoreMath.RaiseTo(500, 1360, 3000) == 1400 && AheadMoreMath.RaiseTo(500, 400, 3000) == 500 && AheadMoreMath.RaiseTo(500, 3600, 3000) == 3000
          && AheadMoreMath.RaiseTo(3000, 3600, 3000) == 3000 && AheadMoreMath.RaiseTo(500, 3600, 0) == 3600, "a target goes up to what is wanted, never past the slider's top");
        var hunt = new ManagerJobInfo { key = "hunting:0", kind = AheadMoreMath.Hunting, threshold = true, raises = true, target = 500, current = 340, max = 3000, wanted = 1360 };
        C(AheadMoreMath.ShouldRaise(hunt), "hunting at 500 while the raw food wants 1360 meat: raise it");
        hunt.suspended = true;
        C(!AheadMoreMath.ShouldRaise(hunt) && AheadMoreMath.ShouldResume(hunt), "a suspended job isn't raised; it is resumed while its goods are wanted");
        hunt.suspended = false;
        hunt.wanted = 450;
        C(!AheadMoreMath.ShouldRaise(hunt) && !AheadMoreMath.ShouldResume(hunt), "wanting less than the target: nothing to do");
        var herd = new ManagerJobInfo { kind = AheadMoreMath.Livestock, threshold = false, target = 10, current = 8, wanted = 20 };
        C(!AheadMoreMath.ShouldRaise(herd) && !AheadMoreMath.ShouldResume(herd), "a herd is only shown, never raised");
        var capped = new ManagerJobInfo { threshold = true, raises = true, target = 3000, max = 3000, wanted = 5000 };
        C(!AheadMoreMath.ShouldRaise(capped), "at the slider's top already: no step");

        var st = new ManagerState { jobs = 3, active = 2, workType = true, managersOn = 1, managersAble = 3, deskNeeded = true, desks = 1, desksWorking = 1 };
        C(AheadMoreMath.Stall(st) == null, "a desk that works and a manager at it: the manager works");
        st.desks = 0;
        st.desksWorking = 0;
        C(AheadMoreMath.Stall(st) == AheadMoreMath.NoDesk, "no desk: named first, nobody can manage without one");
        st.desks = 1;
        C(AheadMoreMath.Stall(st) == AheadMoreMath.DeskOff, "a desk without power, forbidden or broken");
        st.desksWorking = 1;
        st.managersOn = 0;
        C(AheadMoreMath.Stall(st) == AheadMoreMath.NoManager, "nobody with Managing on, somebody could");
        st.managersAble = 0;
        C(AheadMoreMath.Stall(st) == AheadMoreMath.NobodyCan, "nobody could manage at all");
        st.active = 0;
        C(AheadMoreMath.Stall(st) == null, "nothing waiting: no stall to name");
        C(AheadMoreMath.Stall(new ManagerState { active = 2, workType = false, deskNeeded = false }) == null, "a manager without its work type or desks isn't held up by them");
        C(AheadMoreMath.RaiseWorked(340, 620, 1400) && !AheadMoreMath.RaiseWorked(340, 590, 1400) && AheadMoreMath.RaiseWorked(340, 1200, 1400),
            "a raised target worked once the count rose a quarter of the way, or reached most of it");

        // ---------- The shopping list ----------
        // Steel 31 in store: the blueprints are 50 short, the components' chain wants 40 more.
        var steel = new ShopNeed { key = "Steel", label = "steel", have = 31, price = 1.9f };
        AheadMoreMath.AddReason(steel, "for the blueprints", AheadMoreMath.Gross(50, steel.have));
        AheadMoreMath.AddReason(steel, "for component", AheadMoreMath.Gross(40, steel.have));
        C(steel.want == 152 && steel.Short == 121, $"two reasons each reckoned against the same 31 steel: 152 used, 121 short ({steel.want}, {steel.Short})");
        steel.pending = 30;
        C(steel.Short == 91, "what is marked to mine comes off");
        C(AheadMoreMath.ShopShort(10, 20, 0) == 0 && AheadMoreMath.ShopShort(-5, 0, 0) == 0, "enough in store: nothing short");
        var neutro = new ShopNeed { key = "Neutroamine", label = "neutroamine", have = 0, price = 10f };
        AheadMoreMath.AddReason(neutro, "for medicine", AheadMoreMath.Gross(6, 0));
        var comp = new ShopNeed { key = "ComponentIndustrial", label = "component", have = 2, price = 32f };
        AheadMoreMath.AddReason(comp, "for the blueprints", AheadMoreMath.Gross(4, 2));
        var none = new ShopNeed { key = "Gold", label = "gold", have = 50 };
        AheadMoreMath.AddReason(none, "for the blueprints", 20);
        var list = AheadMoreMath.Order(new[] { neutro, steel, comp, none });
        C(list.Count == 3 && list[0] == steel && list[1] == comp && list[2] == neutro, "the list: what is short, the most silver first (" + string.Join(", ", list.Select(l => l.key)) + ")");
        C(AheadMoreMath.Order(Enumerable.Range(0, 30).Select(i => new ShopNeed { key = "k" + i, want = 5, price = 1f })).Count == AheadMoreMath.MaxShopRows, "a long list is cut");

        C(AheadMoreMath.CellsToMark(121, 40f) == 4 && AheadMoreMath.CellsToMark(5, 40f) == 1 && AheadMoreMath.CellsToMark(100000, 40f) == AheadMoreMath.MaxMineCells
          && AheadMoreMath.CellsToMark(0, 40f) == 0 && AheadMoreMath.CellsToMark(10, 0f) == 0, "rock to mark: the shortfall at the yield, at least one, at most a step");
        C(AheadMoreMath.MineWorked(4, 2) && !AheadMoreMath.MineWorked(4, 1) && !AheadMoreMath.MineWorked(0, 0), "a mining step worked once half its cells are mined");

        // ---------- Spare clothes ----------
        C(Math.Abs(AheadMoreMath.DaysToFray(100f, 100f, 0.4f) - 125f) < 0.01f && AheadMoreMath.DaysToFray(40f, 100f, 0.4f) == 0f
          && AheadMoreMath.DaysToFray(80f, 100f, 0f) == AheadMath.Far && AheadMoreMath.DaysToFray(52f, 100f, 1f) == 2f, "days until a garment wears below half, at its own wear a day");
        var parka = new ClothesKind { key = "Apparel_Parka", label = "parka", worn = 9, frayed = 3, soon = 1, spare = 1, wornOutADay = 0.2f, canMake = true, partsInStock = true };
        C(AheadMoreMath.ClothesWanted(parka, 5f) == 5 && AheadMoreMath.ClothesShort(parka, 5f) == 4 && AheadMoreMath.ClothesTarget(parka, 5f) == 5,
            "3 below half, 1 soon, 1 worn through in five days: 5 wanted, 1 spare, 4 short, keep 5");
        parka.billCover = 4;
        C(AheadMoreMath.ClothesShort(parka, 5f) == 0, "a bill that makes them covers it");
        parka.billCover = 0;
        parka.forever = true;
        C(AheadMoreMath.ClothesShort(parka, 5f) == 0, "a bill for ever covers anything");
        parka.forever = false;
        var hat = new ClothesKind { key = "Apparel_CowboyHat", label = "cowboy hat", worn = 4, frayed = 1, spare = 0 };
        var shirt = new ClothesKind { key = "Apparel_BasicShirt", label = "shirt", worn = 9, frayed = 6, spare = 0, managed = true };
        var pants = new ClothesKind { key = "Apparel_Pants", label = "pants", worn = 9, frayed = 0, spare = 2 };
        var steps = AheadMoreMath.ClothesSteps(new[] { hat, shirt, pants, parka }, 5f);
        C(steps.Count == 2 && steps[0] == parka && steps[1] == hat, "bills for the most short first; none for a bill Everybody Gets One sets, none where nothing is short");
        C(AheadMoreMath.ClothesWanted(new ClothesKind { wornOutADay = 0.05f }, 5f) == 1, "a worn-through rate rounds up to a whole piece");

        // ---------- Hay is not the people's food ----------
        var (x, fields) = AheadTests.Reference();
        var before = AheadMath.Plan(x);
        var hay = new AheadField
        {
            key = "hay", label = "hay", crop = "Plant_Haygrass", cropLabel = "haygrass", product = "Hay", productLabel = "hay", food = true, feedOnly = true, cells = 600, sown = 600,
            perCellADay = AheadMath.PerCellADay(18f, 0.05f, 9f, 1f), freeNext = 40
        };
        x.fields.Add(hay);
        var withHay = AheadMath.Plan(x);
        C(withHay.feedOnlyGive > 5f && withHay.cellsShort == before.cellsShort && withHay.ideas.Any(i => i.area == "fields") && !withHay.ideas.Any(i => i.field == hay),
            $"600 cells of hay give {withHay.feedOnlyGive:0.#} a day of feed and the people are still {withHay.cellsShort} cells short; the hay field is never grown for them");
        C(Math.Abs(withHay.peopleNeed - before.cropNeed) < 0.01f && withHay.cropGive > before.cropGive, "the people's need is theirs alone; what the fields give counts the hay apart");
        // Animals fed from the stores eat the hay; the people's surplus would feed them too.
        x.feedAnimals = 6;
        x.feedNeed = 6f;
        var fed = AheadMath.Plan(x);
        C(fed.cellsShort == before.cellsShort, $"six animals eat the hay: the people's shortfall is the same ({fed.cellsShort} cells)");
        x.fields.Remove(hay);
        var noHay = AheadMath.Plan(x);
        C(noHay.cellsShort > before.cellsShort, $"without the hay the animals' 6 a day add to the crops to grow ({noHay.cellsShort} cells against {before.cellsShort})");
        x.feedAnimals = 0;
        x.feedNeed = 0f;
        var unused = new AheadField { food = true, feedOnly = true, measured = true, harvest = 100f, stock = 300f, stockNutrition = 15f };
        C(AheadMath.OverSupplied(unused, 0f) && !AheadMath.OverSupplied(new AheadField { food = true, feedOnly = true, stockNutrition = 15f }, 10f),
            "hay nobody eats, two harvests in store: far more than used; hay against the animals' need by its nutrition");

        // ---------- People a quest brings ----------
        var g = new GrowthInput { colonists = 9, beds = 9, guests = 8, seats = 16, questJoining = 2 };
        var beds = GrowthMath.BedsRow(g);
        var dining = GrowthMath.DiningRow(g);
        C(beds.need == 9 && beds.soon == 11 && beds.Short == 0 && beds.SoonShort == 2 && beds.why == GrowthMath.WhyJoining,
            "two joiners from an accepted quest: two beds more once they come, not short now");
        C(dining.soon == 9 + 8 + 2 - GrowthMath.DiningSlack && dining.SoonShort == 2, "and two seats more");

        // ---------- A busy bench by the record ----------
        C(Math.Abs(GrowthMath.WorkedHours(4f, 1.5f) - 6f) < 0.001f && GrowthMath.WorkedHours(-1f, 2f) == 0f, "four batches a day at an hour and a half each: six hours worked");
        C(GrowthMath.BusyByRecord(7f, 10f) && !GrowthMath.BusyByRecord(7f, 3f) && !GrowthMath.BusyByRecord(3f, 40f),
            "busy by the record: worked most of a hand's day with as much again waiting; a bench worked three hours a day isn't, however much waits");
        C(GrowthMath.BusyBench(true, 3f, 40f, GrowthMath.BenchHoursADay) == false && GrowthMath.BusyBench(false, 0f, 40f, GrowthMath.BenchHoursADay)
          && !GrowthMath.BusyBench(false, 0f, 10f, GrowthMath.BenchHoursADay), "with the record its hours count; until it covers a day, the work queued alone (over 2 days of 8 hours)");

        // ---------- Mining for a room ----------
        C(Math.Abs(GrowthMath.MineHours(1500f, true) - 19f * 100f / 2500f) < 0.001f && Math.Abs(GrowthMath.MineHours(400f, false) - 10f * 100f / 2500f) < 0.001f
          && GrowthMath.MineHours(0f, true) == 0f, "a granite cell of 1500 hit points: 19 swings of 80 at 100 ticks each, about 0.76 hours");
        C(Math.Abs(GrowthMath.MinerHoursADay(2f) - 12f) < 0.001f, "two plain miners mine 12 hours a day");
        C(Math.Abs(GrowthMath.MineAsBuildHours(12f, 6f, 24f) - 48f) < 0.001f && GrowthMath.MineAsBuildHours(5f, 0f, 24f) == 5f && GrowthMath.MineAsBuildHours(0f, 6f, 24f) == 0f,
            "12 hours of mining for one miner take two days: 48 of the four builders' hours; nobody mining, the plain hours");
        var budget = new GrowthInput { builderHoursADay = 24f, minerHoursADay = 6f };
        float room = GrowthMath.BuildHours(5000f) + GrowthMath.MineAsBuildHours(30 * GrowthMath.MineHours(1500f, true), budget.minerHoursADay, budget.builderHoursADay);
        C(GrowthMath.Fits(0f, room, budget.builderHoursADay) && !GrowthMath.Fits(10f, room, budget.builderHoursADay),
            $"a room in rock (30 cells to mine) counts {room:0} hours: it goes alone, not after other building");
        return n;
    }
}
