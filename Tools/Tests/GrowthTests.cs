using System;
using System.Collections.Generic;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// Building ahead (Building/GrowthMath.cs): per kind of room, the colony's need against what is built and planned
/// and how soon it falls short; the rooms to build in order of urgency; the builders' couple of days; a project's
/// stages; the side a room keeps free to grow into; and the reference colony (the player's save, day 96) end to end.
/// </summary>
public static class GrowthTests
{
    private static bool Near(float a, float b, float tol = 0.01f) => Math.Abs(a - b) <= tol;

    public static int Run(Action<bool, string> check, bool print = false)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "building ahead: " + what);
        }

        // ---------- Beds ----------
        var x = new GrowthInput { colonists = 9, beds = 9 };
        var beds = GrowthMath.BedsRow(x);
        C(beds.need == 9 && beds.Short == 0 && beds.why == null, "nine colonists, nine beds: enough");
        x.colonists = 10;
        beds = GrowthMath.BedsRow(x);
        C(beds.Short == 1 && beds.days == 0f && beds.level == 4 && beds.why == GrowthMath.WhyNow, "ten sleep here and nine beds: one short now, the most urgent");
        x.bedsPlanned = 1;
        C(GrowthMath.BedsRow(x).Short == 0, "a bed already planned covers it");
        x = new GrowthInput { colonists = 9, beds = 9, joining = 1 };
        beds = GrowthMath.BedsRow(x);
        C(beds.Short == 0 && beds.SoonShort == 1 && beds.why == GrowthMath.WhyJoining && beds.days == GrowthMath.NoRush && beds.level == 2,
            "a prisoner being recruited: one bed short once they join, not now");
        x.slaves = 2;
        C(GrowthMath.BedsRow(x).need == 11, "slaves sleep here too");

        // ---------- Seats ----------
        x = new GrowthInput { colonists = 9, guests = 8, seats = 8 };
        var dining = GrowthMath.DiningRow(x);
        C(dining.need == 16 && dining.Short == 8 && dining.why == GrowthMath.WhyNow, $"17 eat here (guests too), one seat fewer is enough: 16 seats, 8 short ({dining.need}, {dining.Short})");
        x.guests = 0;
        x.seats = 8;
        C(GrowthMath.DiningRow(x).Short == 0, "nine colonists at eight seats: enough");
        x.joining = 2;
        C(GrowthMath.DiningRow(x).SoonShort == 2 && GrowthMath.DiningRow(x).Short == 0, "two joining: two seats short once they come");
        C(GrowthMath.DiningRow(new GrowthInput { colonists = 1 }).need == 0, "one alone needs no dining room");

        // ---------- The cold store ----------
        var cold = new GrowthInput
        {
            colonists = 9, guests = 8, rawUseADay = 27.2f * AheadMath.RawPerMeal, mealUseADay = 27.2f, mealDays = 2f, reserveDays = 5f, withoutDays = 13, winterIn = 20,
            rawPerSlot = 3.75f, mealPerSlot = 9f, coldSlots = 60
        };
        var cr = GrowthMath.ColdRow(cold);
        C(cr.mealSlots == 7 && cr.rawSlots == 21 && cr.need == 28, $"two days of meals take 7 places, five days of raw food 21: 28 now ({cr.mealSlots}, {cr.rawSlots})");
        C(cr.winterSlots == 73 && cr.soon == 80, $"through 13 days without growing the raw food takes 73 places: 80 ({cr.winterSlots}, {cr.soon})");
        C(cr.Short == 0 && cr.SoonShort == 20 && cr.days == 20f && cr.why == GrowthMath.WhyWinter && cr.level == 1,
            $"60 places: enough now, 20 short when the winter comes in 20 days ({cr.SoonShort}, {cr.days}, level {cr.level})");
        C(Near(cr.holdsDays, 53 * 3.75f / (27.2f * AheadMath.RawPerMeal), 0.01f) && cr.wantsDays == 18f, $"it holds {cr.holdsDays:0.#} days of raw food beside the meals; the target and the winter want 18");
        cold.winterIn = 1;
        C(GrowthMath.ColdRow(cold).level == 3, "the winter tomorrow: urgent");
        cold.coldSlots = 0;
        cr = GrowthMath.ColdRow(cold);
        C(cr.Short == 28 && cr.days == 0f && cr.why == GrowthMath.WhyNow && cr.holdsDays == 0f, "nothing cold at all: short now");
        cold.coldPlanned = 90;
        C(!GrowthMath.ColdRow(cold).Wanting, "a freezer being built covers it");
        var chick = new GrowthInput { rawUseADay = 15f, reserveDays = 5f, rawPerSlot = 0.03f * 75f };
        var plain = new GrowthInput { rawUseADay = 15f, reserveDays = 5f, rawPerSlot = 0.05f * 75f };
        C(GrowthMath.ColdRow(chick).need > GrowthMath.ColdRow(plain).need, "food with little nutrition a piece takes more places for the same days");
        C(GrowthMath.WinterIn(new[] { 1f, 0.5f, 0.05f, 0f }) == 2 && GrowthMath.WinterIn(new[] { 1f, 1f }) == -1 && GrowthMath.WinterIn(new[] { 0f }) == 0,
            "the winter starts on the first day without growing");
        C(GrowthMath.Slots(0f, 3f) == 0 && GrowthMath.Slots(9f, 9f) == 1 && GrowthMath.Slots(9.1f, 9f) == 2 && GrowthMath.Slots(5f, 0f) == 0, "places by the nutrition a place holds");

        // ---------- Hospital beds ----------
        C(GrowthMath.HospitalBedsFor(9) == 3 && GrowthMath.HospitalBedsFor(4) == 1 && GrowthMath.HospitalBedsFor(5) == 2 && GrowthMath.HospitalBedsFor(1) == 0,
            "one hospital bed for every four people");
        var h = GrowthMath.HospitalRow(new GrowthInput { colonists = 9, medicalBeds = 2, patientsMost = 1 });
        C(h.need == 3 && h.Short == 1 && h.why == GrowthMath.WhySize && h.level == 1 && h.days == GrowthMath.NoRush, "nine people, two hospital beds: one short for the colony's size, no rush");
        h = GrowthMath.HospitalRow(new GrowthInput { colonists = 9, medicalBeds = 2, patientsMost = 5 });
        C(h.need == 5 && h.why == GrowthMath.WhyInjuries && h.level == 2, "five laid up at once lately: five beds");
        h = GrowthMath.HospitalRow(new GrowthInput { colonists = 9, medicalBeds = 2, patientsNow = 3, patientsMost = 3 });
        C(h.why == GrowthMath.WhyNow && h.days == 0f && h.level == 3, "three laid up now in two beds: short now");

        // ---------- The workshop ----------
        var w = GrowthMath.WorkshopRow(new GrowthInput { benches = 5, benchesInWorkshop = 5, workshopSpare = 0 });
        C(w.Short == 0, "five benches in the workshop, none busy: enough");
        w = GrowthMath.WorkshopRow(new GrowthInput { benches = 5, benchesInWorkshop = 5, busy = 1, busyAlone = 1 });
        C(w.Short == 1 && w.why == GrowthMath.WhyBusy && w.level == 2, "a busy bench with no second of its kind and no room for one: a bench place short");
        C(GrowthMath.WorkshopRow(new GrowthInput { benches = 5, benchesInWorkshop = 5, workshopSpare = 1, busyAlone = 1 }).Short == 0, "room for a second bench in the workshop: enough");
        w = GrowthMath.WorkshopRow(new GrowthInput { benches = 4, benchesInWorkshop = 2 });
        C(w.Short == 2 && w.why == GrowthMath.WhyOutside && w.level == 1, "two benches outside any workshop");
        C(GrowthMath.Busy(17f, 8f) && !GrowthMath.Busy(16f, 8f) && !GrowthMath.Busy(40f, 0f), "busy: more than two days of a hand's eight hours waiting");
        C(GrowthMath.SparePlaces(22) == 2 && GrowthMath.SparePlaces(9) == 0 && GrowthMath.SparePlaces(-3) == 0, "a bench place for every ten free cells of workshop floor");

        // ---------- The stores ----------
        var s = GrowthMath.StorageRow(new GrowthInput { lying = 12, freeSlots = 40 });
        C(s.Short == 0, "12 stacks about, 40 places free: the haulers will get to them");
        s = GrowthMath.StorageRow(new GrowthInput { lying = 30, lyingOut = 10, freeSlots = 12 });
        C(s.Short == 18 && s.why == GrowthMath.WhyLying && s.level == 2, "30 stacks about, 12 places: 18 short, worse when they lie out under the sky");
        C(GrowthMath.StorageRow(new GrowthInput { lying = 4, freeSlots = 0 }).Short == 0, "a few stacks about are the haulers' lag");

        // ---------- The builders ----------
        C(Near(GrowthMath.BuildHours(4250f), 2f) && GrowthMath.BuildHours(0f) == 0f, "4250 work is two hours with the fetching");
        C(GrowthMath.BuilderHoursADay(4f) == 24f && GrowthMath.DaysOfWork(48f, 24f) == 2f && GrowthMath.DaysOfWork(5f, 0f) == GrowthMath.NoRush, "four plain builders build 24 hours a day");
        C(GrowthMath.Fits(0f, 100f, 24f) && GrowthMath.Fits(20f, 28f, 24f) && !GrowthMath.Fits(20f, 29f, 24f) && !GrowthMath.Fits(0f, 1f, 0f),
            "a room fits while what is queued and it take two days at most; any room when nothing is queued; nothing when nobody builds");
        C(GrowthMath.HowManyFit(0f, new[] { 30f, 10f, 9f }, 24f) == 2 && GrowthMath.HowManyFit(50f, new[] { 1f }, 24f) == 0 && GrowthMath.HowManyFit(0f, new[] { 60f, 1f }, 24f) == 1,
            "rooms fit one after another on what is queued");

        // ---------- Stages ----------
        C(GrowthMath.StageOf(new[] { 3, 20, 2, 0, 5 }) == GrowthMath.Clear && GrowthMath.StageOf(new[] { 0, 20, 2, 0, 5 }) == GrowthMath.Walls
          && GrowthMath.StageOf(new[] { 0, 0, 0, 0, 5 }) == GrowthMath.Furniture && GrowthMath.StageOf(new[] { 0, 0, 0, 0, 0 }) == GrowthMath.Finished,
            "a project is at the first stage with work left");
        C(GrowthMath.StageMoved(GrowthMath.Walls, 20, GrowthMath.Climate, 2) && GrowthMath.StageMoved(GrowthMath.Walls, 20, GrowthMath.Walls, 15)
          && !GrowthMath.StageMoved(GrowthMath.Walls, 20, GrowthMath.Walls, 16) && GrowthMath.StageMoved(GrowthMath.Walls, 2, GrowthMath.Walls, 1),
            "a stage step worked when the project moved on or a quarter of the stage got done");
        C(GrowthMath.RoomGoing(GrowthMath.Climate, 50f, 60f) && GrowthMath.RoomGoing(GrowthMath.Walls, 40f, 60f) && !GrowthMath.RoomGoing(GrowthMath.Walls, 41f, 60f),
            "a room is going once its walls stand or a third of it is done");

        // ---------- Room to grow ----------
        C(GrowthMath.KeepDepth(7, 7, GrowthMath.East) == 4 && GrowthMath.KeepDepth(4, 20, GrowthMath.North) == 6 && GrowthMath.KeepDepth(3, 3, GrowthMath.West) == 3,
            "a side kept about half the room deep, three to six");
        var keep = new[] { 4, 4, 4, 4 };
        C(GrowthMath.GrowSide(new[] { 6, 6, 6, 6 }, GrowthMath.South, keep) == GrowthMath.North, "free all round: the side facing away from the door");
        C(GrowthMath.GrowSide(new[] { 0, 5, 9, 2 }, GrowthMath.South, keep) == GrowthMath.East, "never the door's side; the side with room");
        C(GrowthMath.GrowSide(new[] { 2, 2, 9, 1 }, GrowthMath.South, keep) == -1, "no side with three free rows: none kept");
        C(GrowthMath.Strip(10, 20, 7, 5, GrowthMath.North, 4) == (10, 25, 7, 4) && GrowthMath.Strip(10, 20, 7, 5, GrowthMath.West, 3) == (7, 20, 3, 5)
          && GrowthMath.Strip(10, 20, 7, 5, GrowthMath.East, 3) == (17, 20, 3, 5) && GrowthMath.Strip(10, 20, 7, 5, GrowthMath.South, 2) == (10, 18, 7, 2),
            "the strip lies just outside the wall");
        C(GrowthMath.Inside(17, 20, (17, 20, 3, 5)) && !GrowthMath.Inside(16, 20, (17, 20, 3, 5)) && !GrowthMath.Inside(20, 24, (17, 20, 3, 5)), "inside a strip");

        // ---------- The reference colony (the player's save, day 96) ----------
        var input = Reference();
        var plan = GrowthMath.Plan(input);
        if (print) Print(input, plan);
        var rBeds = plan.Row(GrowthMath.Beds);
        var rCold = plan.Row(GrowthMath.Cold);
        var rDining = plan.Row(GrowthMath.Dining);
        var rHosp = plan.Row(GrowthMath.Hospital);
        C(rBeds.Short == 0 && rBeds.SoonShort == 0, "the reference colony: a bed for each of the nine");
        C(rCold.built == 0 && rCold.Short == rCold.need && rCold.days == 0f && rCold.need >= 30 && rCold.soon > 2 * rCold.need,
            $"the reference colony keeps nothing cold: {rCold.need} places short now, {rCold.soon} through the winter");
        C(rDining.need == 16 && rDining.Short == 8, "the reference colony: 17 eat at 8 seats");
        C(rHosp.Short == 1 && rHosp.level == 1, "the reference colony: a third hospital bed for nine, no rush");
        C(plan.Row(GrowthMath.Workshop).Short == 0, "the machining bills wait for steel, so the benches aren't busy: no workshop");
        C(plan.Row(GrowthMath.Storage).Short == 0, "the stores have room for what lies about");
        C(plan.ideas.Count == 3 && plan.ideas[0].kind == GrowthMath.Cold && plan.ideas[1].kind == GrowthMath.Dining && plan.ideas[2].kind == GrowthMath.Hospital,
            "the rooms in order: the cold store, the dining room, then a hospital bed (" + string.Join(", ", plan.ideas.Select(i => i.kind)) + ")");
        C(plan.ideas.Select(i => i.key).Distinct().Count() == plan.ideas.Count && plan.ideas.All(i => i.key == GrowthMath.RoomKey(i.kind)), "every room step has its own key");
        // The builders: four of them, nothing queued; the freezer first, then whether the dining room fits beside it.
        C(GrowthMath.HowManyFit(input.queuedHours, new[] { 20f, 16f }, input.builderHoursADay) == 2, "four builders take a freezer and a dining room in their couple of days");
        C(GrowthMath.HowManyFit(input.queuedHours, new[] { 40f, 16f }, input.builderHoursADay) == 1, "a big freezer alone, the dining room after");

        // Setting a room aside gives the turn to the next.
        input.declined = new HashSet<string> { GrowthMath.RoomKey(GrowthMath.Cold) };
        var again = GrowthMath.Plan(input);
        C(again.ideas.First().kind == GrowthMath.Dining && again.ideas.All(i => i.kind != GrowthMath.Cold), "with the cold store set aside, the dining room comes first");
        input.declined = new HashSet<string>();

        // A prisoner joining and the freezer built: the bed comes before the hospital.
        var later = Reference();
        later.joining = 1;
        later.coldSlots = 200;
        later.seats = 16;
        var laterPlan = GrowthMath.Plan(later);
        C(laterPlan.ideas.Count == 3 && laterPlan.ideas[0].kind == GrowthMath.Beds && laterPlan.ideas[1].kind == GrowthMath.Dining && laterPlan.ideas[2].kind == GrowthMath.Hospital,
            "with a prisoner being recruited: a bed and a seat for them, then the hospital (" + string.Join(", ", laterPlan.ideas.Select(i => i.kind)) + ")");
        return n;
    }

    /// <summary>
    /// The player's save at day 96 (docs/plan-ahead.md), with what the save doesn't say filled in by the fixture: 9
    /// colonists, each with a bed, and 8 Hospitality guests (in their guest beds); one dining room for 8; nothing kept
    /// cold (the meals keep two days, as the Ahead section's reference has it); two hospital beds, one laid up at most
    /// lately; one of each bench (five) in one workshop, the machining bills waiting for steel; 12 stacks about and 40
    /// places free; four builders, nothing under construction; the raw food as it lies in store (chickpeas 2390 in 35
    /// cells, corn 440, potatoes 268, rice 252) and a temperate land's year from day 36.
    /// </summary>
    public static GrowthInput Reference()
    {
        var temps = new float[] { 6, 10, 14, 18, 21, 22, 20, 15, 9, 2, -4, -2 };
        var factors = AheadMath.GrowFactors(AheadMath.DayTemperatures(temps, 36, 60), 0f, 6f, 42f, 58f);
        float rawNutrition = 2390 * 0.03f + 440 * 0.05f + 268 * 0.05f + 252 * 0.05f;
        int rawCells = 35 + AheadMath.StorageCells(440, 75) + AheadMath.StorageCells(268, 75) + AheadMath.StorageCells(252, 75);
        return new GrowthInput
        {
            colonists = 9, guests = 8, beds = 9, seats = 8, medicalBeds = 2, patientsMost = 1, patientDays = 15,
            mealUseADay = 17 * 1.6f, rawUseADay = AheadMath.RawFor(17 * 1.6f), mealDays = 2f, reserveDays = 5f, withoutDays = AheadMath.DaysWithout(factors),
            winterIn = GrowthMath.WinterIn(factors), rawPerSlot = rawNutrition / rawCells, mealPerSlot = 0.9f * 10f, coldSlots = 0,
            benches = 5, benchesInWorkshop = 5, workshopSpare = 0, busy = 0, busyAlone = 0, lying = 12, lyingOut = 4, freeSlots = 40,
            builders = 4, queuedHours = 0f, builderHoursADay = GrowthMath.BuilderHoursADay(4f)
        };
    }

    /// <summary>The reference colony's rows and rooms in words, for a look by hand (set RIMFOLK_GROWTH_PRINT=1).</summary>
    public static void Print(GrowthInput x, GrowthPlan p)
    {
        Console.WriteLine("  Reference colony, building ahead:");
        foreach (var r in p.rows)
            Console.WriteLine($"    {r.kind}: need {r.need} (soon {r.soon}), built {r.built}, planned {r.planned}, short {r.Short} (soon {r.SoonShort}), days {r.days:0.#}, level {r.level}, why {r.why ?? "-"}"
                              + (r.kind == GrowthMath.Cold ? $"; meals {r.mealSlots}, raw {r.rawSlots}, winter {r.winterSlots}; holds {r.holdsDays:0.#} days, wants {r.wantsDays:0.#}" : ""));
        Console.WriteLine($"    winter in {x.winterIn} days, {x.withoutDays} without growing; raw {x.rawUseADay:0.#} a day at {x.rawPerSlot:0.##} a place; builders {x.builders}, {x.builderHoursADay:0} hours a day");
        foreach (var i in p.ideas) Console.WriteLine($"    room {i.kind} [{i.key}]: want {i.want}, days {i.days:0.#}, level {i.level}");
    }
}
