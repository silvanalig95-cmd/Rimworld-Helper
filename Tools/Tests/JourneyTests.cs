using System;
using System.Collections.Generic;
using System.Linq;
using ColonyCouncil;
using static ColonyCouncil.JourneyMath;

/// <summary>
/// A journey made ready (Source/ColonyCouncil/Journeys/JourneyMath.cs): the default departure at the next dawn, each
/// traveller's last night shifted so they wake rested in time, the last hours' steps, what to pack for the days on the
/// road, travel food first, how long the food and feed last against the road, the list's groups, subtotals and counts as
/// the player changes them, clothes for the weather on the way, and the edge and gate to leave by.
/// </summary>
public static class JourneyTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void Check(bool ok, string what)
        {
            n++;
            check(ok, "journeys: " + what);
        }

        When(Check);
        Nights(Check);
        Steps(Check);
        Packing(Check);
        Food(Check);
        Lasting(Check);
        Changing(Check);
        Clothes(Check);
        Leaving(Check);
        return n;
    }

    // ---------------- When ----------------

    private static void When(Action<bool, string> Check)
    {
        Check(DefaultHoursAhead(20) == 10, $"at 20h the next dawn is ten hours off ({DefaultHoursAhead(20)})");
        Check(DefaultHoursAhead(22) == 32, $"at 22h dawn is too soon, so the one after ({DefaultHoursAhead(22)})");
        Check(DefaultHoursAhead(6) == 24, $"at dawn itself, the next day's ({DefaultHoursAhead(6)})");
        Check(DefaultHoursAhead(0) == 30, $"at midnight, the dawn after next ({DefaultHoursAhead(0)})");
        for (int h = 0; h < 24; h++)
        {
            int ahead = DefaultHoursAhead(h);
            Check(ahead >= DefaultLeadHours && ahead < DefaultLeadHours + 24, $"from {h}h the default is within a day past the lead ({ahead})");
            Check((h + ahead) % 24 == DawnHour, $"from {h}h the default departure is at dawn");
        }
        Check(ClampHoursAhead(0) == MinLeadHours && ClampHoursAhead(99999) == MaxDaysAhead * 24 && ClampHoursAhead(40) == 40, "a chosen departure stays within the lead and the last day");
    }

    // ---------------- The night before ----------------

    /// <summary>Their own timetable for each hour from <paramref name="hourNow"/>: asleep from <paramref name="sleepFrom"/> for <paramref name="sleepHours"/>, else work and some free time.</summary>
    private static Slot[] Own(int hourNow, int hours, int sleepFrom = 22, int sleepHours = 8)
    {
        var s = new Slot[hours];
        for (int i = 0; i < hours; i++)
        {
            int h = (hourNow + i) % 24;
            int since = ((h - sleepFrom) % 24 + 24) % 24;
            s[i] = since < sleepHours ? Slot.Sleep : h >= 18 ? Slot.Anything : Slot.Work;
        }
        return s;
    }

    private const float Fall = 0.0396f, Gain = 0.095f, JoyFall = 0.025f, JoyGain = 0.3f;

    private static void Nights(Action<bool, string> Check)
    {
        // Rest through a night: asleep in their sleeping hours once tired, up when full.
        Simulate(new[] { Slot.Sleep, Slot.Sleep, Slot.Sleep }, 0.5f, Fall, Gain, out float end, out float low);
        Check(Math.Abs(end - (0.5f + 3 * Gain)) < 1e-4 && low == 0.5f, $"three hours asleep from half rested ({end:0.000})");
        Simulate(new[] { Slot.Sleep, Slot.Sleep }, 0.9f, Fall, Gain, out end, out _);
        Check(end < 0.9f, "not tired enough to sleep: awake through the sleeping hours");
        Simulate(new[] { Slot.Work, Slot.Work }, 0.2f, Fall, Gain, out end, out _);
        Check(end < 0.2f, "never asleep in work hours");
        Simulate(new[] { Slot.Anything }, 0.2f, Fall, Gain, out end, out _);
        Check(end > 0.2f, "very tired in free hours: asleep");

        // Afternoon at 14h, setting off at dawn (16 hours): the night moves earlier so they wake in time for the last steps.
        var own = Own(14, 16);
        var plan = PlanNight(own, 4, 0.6f, Fall, Gain, 0.8f, JoyFall, JoyGain, true);
        Check(plan.Shifted && plan.wake == 16 - WakeHours, $"a dawn departure: up {WakeHours} hours before (bed {plan.bed}, wake {plan.wake})");
        Check(plan.bed < 8, $"to bed earlier than their own 22h (offset {plan.bed})");
        Check(plan.restAtDeparture >= RestTarget, $"rested at the departure ({plan.restAtDeparture:0.00})");
        Check(plan.restLowest >= RestFloor, $"never run into the ground ({plan.restLowest:0.00})");
        Simulate(plan.slots, 0.6f, Fall, Gain, plan.wake, out _, out _, out int late);
        Check(late == 0, $"up in time for the last steps ({late} hours late)");
        Check(Enumerable.Range(plan.bed, plan.wake - plan.bed).All(i => plan.slots[i] == Slot.Sleep), "the shifted night is one run of sleeping hours");
        Check(Enumerable.Range(plan.wake, 16 - plan.wake).All(i => plan.slots[i] == Slot.Anything), "the last hours are free");
        Check(plan.slots.Skip(12).All(s => s != Slot.Work), "no work in the free hours");
        // On their own hours they'd sleep on through the last steps (the game lets them sleep until rested).
        var baseline = (Slot[])own.Clone();
        for (int i = 12; i < 16; i++) baseline[i] = Slot.Anything;
        Simulate(baseline, 0.6f, Fall, Gain, plan.wake, out _, out _, out int ownLate);
        Check(ownLate >= 2, $"their own hours would have them asleep when the last steps start ({ownLate} hours)");

        // Without the shift, their own hours stay until the free hours.
        var kept = PlanNight(own, 4, 0.6f, Fall, Gain, 0.8f, JoyFall, JoyGain, false);
        Check(!kept.Shifted && kept.slots.Take(12).SequenceEqual(own.Take(12)), "sleep not shifted: their own hours kept");

        // Too soon for a night: only the free hours.
        var soon = PlanNight(Own(9, 3), 4, 0.7f, Fall, Gain, 0.8f, JoyFall, JoyGain, true);
        Check(!soon.Shifted && soon.slots.All(s => s == Slot.Anything), "three hours off: free hours only, no night");

        // Low on recreation: an hour or more of it before the night, on hours that were work or free.
        var bored = PlanNight(own, 4, 0.6f, Fall, Gain, 0.2f, JoyFall, JoyGain, true);
        Check(bored.joyHours >= 1 && bored.joyHours <= MaxJoyHours, $"recreation before the night ({bored.joyHours} hours)");
        if (bored.joyHours > 0)
        {
            int bed = bored.bed >= 0 ? bored.bed : LastNightStart(bored.slots, bored.slots.Length - 4);
            Check(bored.joyFrom + bored.joyHours == bed, "the recreation runs up to bedtime");
            Check(Enumerable.Range(bored.joyFrom, bored.joyHours).All(i => bored.slots[i] == Slot.Joy && (own[i] == Slot.Work || own[i] == Slot.Anything)),
                "recreation only in place of work or free hours");
        }
        Check(plan.joyHours == 0, "enough recreation already: none put in");

        // Many cases: never worse than their own hours, the free hours always free, the night where it says.
        var rnd = new Random(7);
        int worse = 0, floor = 0, cases = 0;
        for (int k = 0; k < 3000; k++)
        {
            int hourNow = rnd.Next(24), hours = 1 + rnd.Next(24), free = 2 + rnd.Next(5), sleepFrom = 19 + rnd.Next(8), sleepLen = 6 + rnd.Next(4);
            float rest = 0.2f + (float)rnd.NextDouble() * 0.8f, fall = 0.03f + (float)rnd.NextDouble() * 0.02f, gain = 0.07f + (float)rnd.NextDouble() * 0.05f;
            var o = Own(hourNow, hours, sleepFrom % 24, sleepLen);
            var p = PlanNight(o, free, rest, fall, gain, (float)rnd.NextDouble(), JoyFall, JoyGain, true);
            cases++;
            var b = (Slot[])o.Clone();
            for (int i = Math.Max(0, hours - free); i < hours; i++) b[i] = Slot.Anything;
            int wakeAt = Math.Max(0, hours - Math.Min(free, WakeHours));
            Simulate(b, rest, fall, gain, wakeAt, out float bEnd, out float bLow, out int bLate);
            Simulate(p.slots, rest, fall, gain, wakeAt, out float pEnd, out float pLow, out int pLate);
            float Value(float end, int lateHours) => Math.Min(end, 0.95f) * 10f - lateHours * LatePenalty;
            if (Value(pEnd, pLate) < Value(bEnd, bLate) - 0.5f && pLow <= bLow) worse++;
            if (pLow < RestFloor && pLow < bLow - 0.05f) floor++;
            if (p.slots.Length != hours || p.slots.Skip(wakeAt).Any(s => s != Slot.Anything) || p.slots.Skip(Math.Max(0, hours - free)).Any(s => s == Slot.Work))
                Check(false, $"case {k}: the free hours are free");
            if (p.Shifted)
            {
                if (p.wake - p.bed < MinNight || p.wake - p.bed > MaxNight || Enumerable.Range(p.bed, p.wake - p.bed).Any(i => p.slots[i] != Slot.Sleep))
                    Check(false, $"case {k}: the night is one run of {MinNight} to {MaxNight} sleeping hours");
            }
            if (p.joyHours > MaxJoyHours) Check(false, $"case {k}: at most {MaxJoyHours} hours of recreation");
        }
        Check(worse == 0, $"never worse off at the departure than on their own hours, rested and up in time ({worse} of {cases})");
        Check(floor == 0, $"never run lower than their own hours would below the floor ({floor} of {cases})");
        Check(LastNightStart(new[] { Slot.Work, Slot.Sleep, Slot.Sleep, Slot.Work }, 4) == 1 && LastNightStart(new[] { Slot.Work }, 1) == -1, "the last night's start");
    }

    // ---------------- The last hours ----------------

    private static void Steps(Action<bool, string> Check)
    {
        Check(Opens(Step.Dress, 4) == 4 && Opens(Step.Pack, 4) == 4, "dressing and packing open with the free hours");
        Check(Opens(Step.Meal, 4) == MealHours && Opens(Step.Toilet, 4) == WashHours && Opens(Step.Gather, 4) == GatherHours, "a meal, a wash, the gate");
        Check(Opens(Step.Meal, 4) > Opens(Step.Wash, 4) && Opens(Step.Wash, 4) > Opens(Step.Gather, 4), "in that order");
        for (int free = 0; free <= 8; free++)
            foreach (Step s in Enum.GetValues(typeof(Step)))
                Check(Opens(s, free) <= free, $"{s} never opens before {free} free hours start");
    }

    // ---------------- What to pack ----------------

    private static void Packing(Action<bool, string> Check)
    {
        Check(Math.Abs(FoodNutrition(3.2f, 4f) - 3.2f * (4f * 1.25f + 1f)) < 1e-4, "food: the days, a quarter more, a spare day");
        Check(FoodNutrition(3.2f, 0f) == 0f && FoodNutrition(0f, 4f) == 0f, "no days or nobody: no food");
        Check(Units(1.8f, 0.9f) == 2 && Units(1.81f, 0.9f) == 3 && Units(0f, 0.9f) == 0 && Units(1f, 0f) == 0, "units round up");
        Check(Medicine(2, 4f) == 2 && Medicine(2, 10f) == 4 && Medicine(1, 0.5f) == 1 && Medicine(0, 5f) == 0 && Medicine(3, 0f) == 0,
            $"medicine by people and days ({Medicine(2, 4f)}, {Medicine(2, 10f)}, {Medicine(1, 0.5f)})");
        Check(MedicineTaken(4, 5, 3) == 3 && MedicineTaken(4, 5, 0) == 4 && MedicineTaken(2, 10, 5) == 2 && MedicineTaken(3, 0, 1) == 0,
            "medicine: never more than half the stores while anyone stays home");
        Check(Bedrolls(3, 2f) == 3 && Bedrolls(3, 0.2f) == 0 && Tents(3, 2f) == 2 && Tents(4, 2f) == 2 && Tents(3, 0.2f) == 0, "bedrolls and tents for nights on the road");
        Check(Math.Abs(TripDays(90000, false) - 1.5f) < 1e-4 && Math.Abs(TripDays(90000, true) - 3f) < 1e-4 && TripDays(0, true) == 0f, "the trip's days");
        Check(WayShare(0.5f, 1f, true) == 0.5f && WayShare(1.5f, 1f, true) == 0.5f && WayShare(2f, 1f, true) == 0f && WayShare(1.5f, 1f, false) == 1f,
            "where on the way, there and back");
    }

    private static void Food(Action<bool, string> Check)
    {
        var inf = float.PositiveInfinity;
        var foods = new List<FoodOption>
        {
            new FoodOption { nutrition = 0.9f, mass = 0.44f, rotDays = 4f, preferability = 10, travel = 0, stock = 20 }, // fine meal
            new FoodOption { nutrition = 0.05f, mass = 0.018f, rotDays = inf, preferability = 8, travel = 1, stock = 300 }, // pemmican
            new FoodOption { nutrition = 0.9f, mass = 0.3f, rotDays = inf, preferability = 8, travel = 2, stock = 5 }, // packaged meal
            new FoodOption { nutrition = 0.05f, mass = 0.03f, rotDays = 14f, preferability = 5, travel = 0, stock = 200 }, // berries
            new FoodOption { nutrition = 0.9f, mass = 0.44f, rotDays = inf, preferability = 9, travel = 0, stock = 0 } // none in stock
        };
        var order = FoodOrder(foods, 5f);
        Check(order[0] == 2 && order[1] == 1, $"packaged meals first, then pemmican ({string.Join(",", order)})");
        Check(order.Last() == 0, "a meal that won't keep through five days comes last");
        Check(!order.Contains(4), "nothing in stock isn't listed");
        Check(FoodOrder(foods, 1f).IndexOf(0) < FoodOrder(foods, 1f).IndexOf(3), "for a day, a fine meal keeps and comes before berries");
        Check(Keeps(foods[3], 5f) && !Keeps(foods[3], 12f), "berries keep through five days, not twelve");

        float need = FoodNutrition(1.6f * 2, 5f);
        var packed = PackFood(foods, 5f, need);
        float got = packed.Sum(p => p.units * foods[p.food].nutrition);
        Check(got >= need - 1e-3 && got < need + 0.9f, $"packed enough and not much more ({got:0.00} for {need:0.00})");
        Check(packed[0].food == 2 && packed[0].units == 5, "all five packaged meals go first");
        Check(packed.All(p => p.units <= foods[p.food].stock), "never more than the stores have");
        Check(packed.All(p => p.food != 0), "the fine meal isn't needed: it stays");
        var little = new List<FoodOption> { foods[0] };
        var only = PackFood(little, 5f, 3f);
        Check(only.Count == 1 && only[0].units == 4, "with nothing else, what won't keep is packed after all");
        var starved = PackFood(new List<FoodOption> { foods[2] }, 5f, 100f);
        Check(starved.Count == 1 && starved[0].units == 5, "short stores: everything there is");
    }

    // ---------------- How long it lasts ----------------

    private static void Lasting(Action<bool, string> Check)
    {
        // The player's screenshot: six travellers at 1.6 nutrition a day each (the game's fed rate), a day and a half there
        // and back; packaged survival meals 11, a fine meal, insect jelly 226, simple meals 7, a simple bake, at 0.9 each
        // and 0.05 for the jelly.
        float eat = 6 * 1.6f, road = 1.5f;
        float food = 11 * 0.9f + 0.9f + 226 * 0.05f + 7 * 0.9f + 0.9f;
        float lasts = Lasts(food, eat);
        Check(Math.Abs(lasts - food / eat) < 1e-4, $"food lasts its nutrition over what they eat a day ({lasts:0.00} days)");
        Check(Math.Abs(226 * 0.05f / eat - 1.18f) < 0.01f, "226 insect jelly is worth about 1.2 days for six");
        Check(Math.Abs(MarginDays(road) - (road * 1.25f + 1f)) < 1e-4 && MarginDays(0f) == 0f, $"the margin: the road, a quarter more and a spare day ({MarginDays(road):0.00})");
        Check(Covers(lasts, road) == Cover.Margin, $"their list covers the road with the margin ({lasts:0.0} of {MarginDays(road):0.0})");
        // Exactly what the planner packs covers the margin, and what it packs is never told short.
        for (float d = 0.5f; d <= 30f; d += 0.5f)
        {
            float packed = FoodNutrition(eat, d);
            Check(Covers(Lasts(packed, eat), d) == Cover.Margin, $"the planner's own food for {d} days reads as enough with the margin");
        }
        Check(Covers(MarginDays(road) - 0.5f, road) == Cover.Road, "past the road but under the margin: enough for the road only");
        Check(Covers(road - 0.2f, road) == Cover.Short && Covers(0f, road) == Cover.Short, "under the road: short");
        Check(Math.Abs(ShortDays(1f, road) - 0.5f) < 1e-4 && ShortDays(4f, road) == 0f && ShortDays(float.PositiveInfinity, road) == 0f, "days short of the road, none when covered");
        Check(ShortMeals(0.5f, eat) == 6 && ShortMeals(0f, eat) == 0, $"half a day short for six: six simple meals ({ShortMeals(0.5f, eat)})");
        Check(ShortMeals(0.01f, eat) == 1, "any shortfall is at least a meal");
        Check(Lasts(0f, eat) == 0f && float.IsPositiveInfinity(Lasts(5f, 0f)) && Lasts(0f, 0f) == 0f, "no food lasts no days; food with nobody to eat it lasts");

        // Feed: a grazer needs it only on bare land, a non-grazer every day.
        var animals = new List<(float perDay, float feedDays)> { (2f, 0.5f), (1f, 3f), (4f, 0f) };
        var (feedLasts, need) = FeedCover(animals, 6f);
        Check(Math.Abs(feedLasts - 2f) < 1e-4, $"feed lasts over what the animals that need it eat ({feedLasts:0.00} days)");
        Check(Math.Abs(need - (2f * 0.5f + 1f * 3f) / 3f) < 1e-4, $"the days they need it, by what each eats ({need:0.00})");
        Check(FeedCover(new List<(float, float)> { (2f, 0f) }, 0f) == (0f, 0f), "grazers on good land: no feed needed");
        Check(float.IsPositiveInfinity(FeedCover(new List<(float, float)> { (2f, 0f) }, 3f).lasts), "feed for animals that graze all the way lasts");
        // The planner's own feed, per animal with the margin, reads as enough with the margin.
        float packedFeed = animals.Sum(a => FoodNutrition(a.perDay, a.feedDays));
        var packedCover = FeedCover(animals, packedFeed);
        Check(Covers(packedCover.lasts, packedCover.need) == Cover.Margin, "the planner's own feed reads as enough with the margin");
    }

    // ---------------- Changing the list ----------------

    private static void Changing(Action<bool, string> Check)
    {
        Check(ClampCount(50, 30) == 30 && ClampCount(-4, 30) == 0 && ClampCount(12, 30) == 12 && ClampCount(5, -1) == 0, "a typed count is kept to what is to hand");
        Check(StepCount(5, 1, 30) == 6 && StepCount(5, 10, 30) == 15 && StepCount(25, 10, 30) == 30 && StepCount(5, 100, 30) == 30, "+ by one, ten or a hundred, never past what is to hand");
        Check(StepCount(5, -10, 30) == 0 && StepCount(5, -1, 30) == 4, "- never below none");
        Check(StepCount(40, 1, 30) == 40 && StepCount(40, -1, 30) == 39, "a count already over what is to hand doesn't jump down with +, and - takes one off");
        Check(Math.Abs(PerPerson(5, 6) - 5f / 6f) < 1e-4 && PerPerson(5, 0) == 0f, "medicine a person");

        Check(GroupOf("food") == "food" && GroupOf("medicine") == "medicine" && GroupOf("feed") == "feed", "food, medicine and feed are their own groups");
        Check(GroupOf("bed") == "bedding" && GroupOf("tent") == "bedding", "beds and tents together as bedding");
        Check(GroupOf("other") == "other" && GroupOf(null) == "other" && GroupOf("drug") == "other", "anything else is other");
        Check(Groups.Distinct().Count() == Groups.Length && Groups[0] == "food" && Groups.Last() == "other", "the groups once each, food first, other last");
        foreach (var kind in new[] { "food", "medicine", "bed", "tent", "feed", "other" }) Check(Groups.Contains(GroupOf(kind)), $"a {kind} line shows in a group");

        var rows = new List<PackRow>
        {
            new PackRow { kind = "food", count = 11, nutrition = 0.9f, mass = 0.3f },
            new PackRow { kind = "food", count = 226, carried = 26, nutrition = 0.05f, mass = 0.02f },
            new PackRow { kind = "medicine", count = 5, mass = 0.5f },
            new PackRow { kind = "bed", count = 2, mass = 4f, sleeps = 2 },
            new PackRow { kind = "bed", count = 3, mass = 3f, sleeps = 1 },
            new PackRow { kind = "tent", count = 1, mass = 6f },
            new PackRow { kind = "other", count = 200, carried = 250, mass = 0.008f },
            new PackRow { kind = "other", count = 0, mass = 99f }
        };
        var food = Sum(rows, "food");
        Check(food.lines == 2 && food.count == 237 && Math.Abs(food.nutrition - (9.9f + 11.3f)) < 1e-3 && Math.Abs(food.mass - (3.3f + 4.52f)) < 1e-3, "the food's subtotal");
        var bedding = Sum(rows, "bedding");
        Check(bedding.sleeps == 2 * 2 + 3 && Math.Abs(bedding.mass - (8f + 9f + 6f)) < 1e-3, $"bedding sleeps a double bed's two (sleeps {bedding.sleeps}), tents weigh in");
        var other = Sum(rows, "other");
        Check(other.lines == 1 && Math.Abs(other.mass - 1.6f) < 1e-3, "a line at none isn't counted");
        var all = Sum(rows);
        Check(Math.Abs(all.mass - (food.mass + 2.5f + bedding.mass + other.mass)) < 1e-3, "every group's weight adds up to the whole");
        float load = LoadToTake(rows);
        Check(Math.Abs(load - (all.mass - 26 * 0.02f - 200 * 0.008f)) < 1e-3, $"what they carry already isn't taken up again ({load:0.00} kg)");
        Check(LoadToTake(new[] { new PackRow { count = 3, carried = 10, mass = 2f } }) == 0f, "carrying more than the line: nothing to take");
    }

    // ---------------- Dressed for the road ----------------

    private static void Clothes(Action<bool, string> Check)
    {
        // Worn: a shirt (0 is worn). Stores: a parka (1), a duster (2), a tuque (3).
        var items = new List<Garment>
        {
            new Garment { cold = 2f, heat = 2f, removable = true },
            new Garment { cold = 30f, heat = 0f, removable = true },
            new Garment { cold = 4f, heat = 22f, removable = true },
            new Garment { cold = 10f, heat = 0f, removable = true }
        };
        // The parka and the duster are both outer torso layers.
        bool Clash(int a, int b) => (a == 1 && b == 2) || (a == 2 && b == 1);
        var cold = Dress(14f, 30f, items, 1, Clash, -20f, 15f);
        Check(cold.Contains(1) && cold.Contains(3), $"a winter road: parka and tuque ({string.Join(",", cold)})");
        var r = Comfy(14f + 2f, 30f - 2f, items, new[] { 0 }.Concat(cold));
        Check(r.min <= -20f + 0.5f, $"comfortable down to the coldest hour ({r.min})");

        // A heat wave with a parka on: the duster replaces it.
        var hot = new List<Garment> { items[1], items[0], items[2], items[3] };
        bool Clash2(int a, int b) => (a == 0 && b == 2) || (a == 2 && b == 0);
        var heat = Dress(-16f, 28f, hot, 2, Clash2, 20f, 44f);
        Check(heat.SequenceEqual(new[] { 2 }), $"a heat wave: out of the parka, into the duster ({string.Join(",", heat)})");

        // A parka they keep on (locked or the player's choice) isn't taken off for the duster.
        hot[0] = new Garment { cold = 30f, heat = 0f, removable = false };
        Check(Dress(-16f, 28f, hot, 2, Clash2, 20f, 44f).Count == 0, "a parka they keep on stays on");

        // Already dressed for it: nothing. Taken by someone else: not offered.
        Check(Dress(-30f, 40f, items, 1, Clash, -20f, 15f).Count == 0, "already comfortable: nothing to change into");
        var taken = Dress(14f, 30f, items, 1, Clash, -20f, 15f, new HashSet<int> { 1 });
        Check(!taken.Contains(1), "a parka someone else takes isn't put on twice");
        Check(Dress(14f, 30f, items, 1, Clash, -20f, 15f, null, 1).Count == 1, "at most as many pieces as allowed");

        var range = RouteRange(new[] { -5f, 3f, 12f });
        Check(range.low == -5f - CampWeatherMath.Margin && range.high == 12f + CampWeatherMath.Margin, "the road's weather with a few degrees either way");
        Check(float.IsNaN(RouteRange(new float[0]).low), "no hours, no range");
    }

    // ---------------- Leaving the map ----------------

    private static void Leaving(Action<bool, string> Check)
    {
        Check(EdgeOrder(1f, 0f)[0] == 1 && EdgeOrder(1f, 0f)[3] == 3, "east: by the east edge, never the west first");
        Check(EdgeOrder(0f, -1f)[0] == 2, "south: by the south edge");
        Check(EdgeOrder(-0.2f, 1f)[0] == 0 && EdgeOrder(-0.2f, 1f)[1] == 3, "north and a little west: north, then west");
        Check(EdgeOrder(0.3f, 0.2f).Distinct().Count() == 4, "every edge once");

        var cells = new List<(int x, int z, bool road)> { (0, 50, false), (0, 80, true), (0, 120, false) };
        Check(BestEdgeCell(cells, 40, 20) == 0, "the nearest cell of the edge when the road is well out of the way");
        Check(BestEdgeCell(cells, 40, 60) == 1, "a road out when it's not much further");
        Check(BestEdgeCell(new List<(int, int, bool)>(), 0, 0) == -1, "no cells, none");

        var line = Line(2, 3, 9, 6);
        Check(line.First() == (2, 3) && line.Last() == (9, 6) && line.Count == 8, $"a line from end to end ({line.Count} cells)");
        Check(line.Zip(line.Skip(1), (a, b) => Math.Abs(a.x - b.x) <= 1 && Math.Abs(a.z - b.z) <= 1).All(x => x), "each step to a neighbouring cell");
        Check(Line(5, 5, 5, 5).Count == 1 && Line(4, 9, 4, 2).Count == 8, "a point, and a line straight down");

        var inside = new[] { true, true, true, false, false, false, false, false };
        var open = Enumerable.Repeat(true, 8).ToArray();
        Check(GateIndex(inside, open) == 2 + GateSteps, $"a few steps past the colony's last cell ({GateIndex(inside, open)})");
        var walled = (bool[])open.Clone();
        walled[5] = false;
        Check(GateIndex(inside, walled) == 6, "past a cell nobody can stand on");
        Check(GateIndex(new[] { false, false, false }, new[] { false, true, true }) == 1, "no colony on the way: the first open cell");
        Check(GateIndex(new[] { true, true }, new[] { true, true }) == -1, "nowhere outside: none");
        var shortWay = new[] { true, false };
        Check(GateIndex(shortWay, new[] { true, true }) == 1, "the edge comes first: the last open cell");
    }
}
