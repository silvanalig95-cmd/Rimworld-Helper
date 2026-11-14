using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Reserved shelf slots (Building/ShelfReserveMath): which slots are empty (each needing a stack of its own), when a
/// new stack may take a slot, which stack moves out to make room, the best of a group at hand, any of meals or medicine
/// kept best first in the order colonists pick their food (a better kind taking a lesser stack's slot, never back and
/// forth), and making room on a full shelf (by a colonist's order, and on their own when nothing else stores the stack
/// that must go), with the seams of making room and fetching (Building/ShelfMakeRoom.cs, ShelfReserves.cs); and which
/// rooms are for keeping things, not for staying (ChewSpotMath).
/// </summary>
public static class ShelfReserveTests
{
    private static ShelfReserveMath.Stack S(string key, int count, params int[] covers) =>
        new ShelfReserveMath.Stack { key = key, count = count, covers = covers };

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "reserved shelf slots: " + what);
        }

        // The hospital shelf: kinds 0 herbal, 1 medicine, 2 glitterworld medicine; six slots.
        var hospital = new List<ShelfReserveMath.Stack> { S("Herbal", 25, 0), S("Herbal", 25, 0), S("Medicine", 25, 1) };
        var missing = ShelfReserveMath.Missing(3, hospital);
        C(missing.Count == 1 && missing[0] == 2, "glitterworld medicine is the one missing");
        C(ShelfReserveMath.MayTakeSlot(6, 3, false, 1) && ShelfReserveMath.MayTakeSlot(6, 4, false, 1) && !ShelfReserveMath.MayTakeSlot(6, 5, false, 1),
            "more herbal fills the free slots but the last, kept for the glitterworld medicine");
        C(ShelfReserveMath.MayTakeSlot(6, 5, true, 1), "the glitterworld medicine takes the slot kept for it");
        C(ShelfReserveMath.MayTakeSlot(6, 5, false, 0), "with no glitterworld medicine on the map, the last slot is used meanwhile");
        C(!ShelfReserveMath.MayTakeSlot(6, 6, true, 1), "a full shelf takes nothing new, reserved or not");

        var full = new List<ShelfReserveMath.Stack>
        {
            S("Herbal", 25, 0), S("Herbal", 10, 0), S("Herbal", 25, 0), S("Herbal", 25, 0), S("Medicine", 25, 1), S("Medicine", 3, 1)
        };
        var outs = ShelfReserveMath.MoveOut(6, full, 3, 1);
        C(outs.Count == 1 && outs[0] == 1, "glitterworld medicine turns up: the smallest stack of what the shelf holds most of (herbal) moves out");
        C(ShelfReserveMath.MoveOut(6, full, 3, 0).Count == 0, "nothing missing that the colony has: nothing moves");
        C(ShelfReserveMath.MoveOut(6, hospital, 3, 1).Count == 0, "a free slot for it already: nothing moves");

        var lastOnes = new List<ShelfReserveMath.Stack> { S("Herbal", 25, 0), S("Medicine", 25, 1) };
        C(ShelfReserveMath.MoveOut(2, lastOnes, 3, 1).Count == 0, "a stack filling a reserved slot never moves out to make room for another");

        // The kitchen shelf: kinds 0 any meal (fine and simple both count), 1 tea, 2 beer, 3 smokeleaf; six slots.
        var kitchen = new List<ShelfReserveMath.Stack>
        {
            S("MealFine", 10, 0), S("MealSimple", 10, 0), S("MealSimple", 4, 0), S("Beer", 25, 2), S("Beer", 25, 2), S("Beer", 20, 2)
        };
        var kMissing = ShelfReserveMath.Missing(4, kitchen);
        C(kMissing.Count == 2 && kMissing.Contains(1) && kMissing.Contains(3), "tea and smokeleaf are missing; a meal of any kind counts for meals");
        var kOut = ShelfReserveMath.MoveOut(6, kitchen, 4, 2);
        C(kOut.Count == 2 && kOut.Contains(5) && kOut.Contains(2) && !kOut.Contains(0),
            "tea and smokeleaf turn up: one beer and one simple meal move out, the fine meal stays and so does a stack of beer");

        C(ShelfReserveMath.Missing(0, kitchen).Count == 0 && ShelfReserveMath.MoveOut(6, kitchen, 0, 0).Count == 0, "nothing reserved, nothing changes");

        // Several slots for one kind: slots 0 and 1 herbal, 2 medicine.
        var twoHerbal = new List<ShelfReserveMath.Stack> { S("Herbal", 25, 0, 1), S("Medicine", 25, 2) };
        var two = ShelfReserveMath.Missing(3, twoHerbal);
        C(two.Count == 1 && (two[0] == 0 || two[0] == 1), "two herbal slots and one herbal stack: one herbal slot is still empty");
        twoHerbal.Add(S("Herbal", 5, 0, 1));
        C(ShelfReserveMath.Missing(3, twoHerbal).Count == 0, "a second herbal stack fills the second slot");
        var fill = ShelfReserveMath.Fill(3, new List<ShelfReserveMath.Stack> { S("Herbal", 5, 0, 1), S("Herbal", 25, 0, 1), S("Herbal", 25, 0, 1) });
        C(fill[0] >= 1 && fill[1] >= 1 && fill[0] != fill[1] && fill[2] == -1, "the bigger stacks fill the slots, and the small one is left over");
        var tight = new List<ShelfReserveMath.Stack> { S("Herbal", 25, 0, 1), S("Herbal", 25, 0, 1), S("Herbal", 5, 0, 1), S("Medicine", 25, 2) };
        var tightOut = ShelfReserveMath.MoveOut(4, tight, 4, 1);
        C(tightOut.Count == 1 && tightOut[0] == 2, "full, with a slot waiting: the herbal stack filling no slot moves out, not one of the two kept");

        // Two kinds that one stack could fill each need a stack of their own: slot 0 fine meal, slot 1 any meal.
        var meals = new List<ShelfReserveMath.Stack> { S("MealFine", 10, 0, 1) };
        var mealGap = ShelfReserveMath.Missing(2, meals);
        C(mealGap.Count == 1 && mealGap[0] == 1, "one fine meal fills the fine meal slot, and the any-meal slot still waits for a stack");
        meals.Add(S("MealSimple", 10, 1));
        C(ShelfReserveMath.Missing(2, meals).Count == 0, "a simple meal fills the any-meal slot, the fine meal keeps its own");
        // The fine meal is matched first and could take the any-meal slot; the simple meal needs it, so the fine meal moves over.
        var swap = ShelfReserveMath.Fill(2, new List<ShelfReserveMath.Stack> { S("MealFine", 20, 1, 0), S("MealSimple", 10, 1) });
        C(swap[0] == 0 && swap[1] == 1, "the stacks are matched so both slots are filled, the fine meal moving over for the simple one");

        // Kept off for now (the hospital's meals with no patient in the room): every meal stack goes, the medicine stays.
        var offMeals = new List<ShelfReserveMath.Stack>
        {
            new ShelfReserveMath.Stack { key = "MealSimple", count = 4, covers = new int[0], barred = true },
            S("Herbal", 25, 0)
        };
        var offOut = ShelfReserveMath.MoveOut(6, offMeals, 1, 0, null);
        C(offOut.Count == 1 && offOut[0] == 0, "a kind kept off for now moves out, even with room to spare");

        // No more than one slot of meals (entry 0, capped at 1, its slot 0).
        ShelfReserveMath.Stack M(string key, int count, int[] covers) => new ShelfReserveMath.Stack { key = key, count = count, covers = covers, groups = new[] { 0 } };
        var capped = new List<ShelfReserveMath.Stack> { M("MealSimple", 10, new[] { 0 }), M("MealSimple", 4, new[] { 0 }), M("MealSimple", 2, new[] { 0 }) };
        var capOut = ShelfReserveMath.MoveOut(6, capped, 1, 0, new[] { 1 });
        C(capOut.Count == 2 && capOut.Contains(1) && capOut.Contains(2), "one slot of meals and no more: the two smaller meal stacks move out, the biggest stays");
        C(ShelfReserveMath.MoveOut(6, capped, 1, 0, new[] { -1 }).Count == 0, "without the cap, the extra meal stacks may stay on a shelf with room");
        // A fine meal slot (0, uncapped) beside one any-meal slot (1, entry 1 capped at 1): the fine meal counts against the cap too.
        var fineAndAny = new List<ShelfReserveMath.Stack>
        {
            new ShelfReserveMath.Stack { key = "MealFine", count = 10, covers = new[] { 0, 1 }, groups = new[] { 1 } },
            new ShelfReserveMath.Stack { key = "MealSimple", count = 10, covers = new[] { 1 }, groups = new[] { 1 } },
            new ShelfReserveMath.Stack { key = "MealSimple", count = 3, covers = new[] { 1 }, groups = new[] { 1 } }
        };
        var faOut = ShelfReserveMath.MoveOut(6, fineAndAny, 2, 0, new[] { -1, 1 });
        C(faOut.Count == 1 && faOut[0] == 2, "a fine meal slot and one any-meal slot keep two meal stacks; the third moves out");

        // At most so many per stack.
        C(ShelfReserveMath.SpaceFor(4, 10, -1) == 4, "four meals per stack: a new stack takes four");
        C(ShelfReserveMath.SpaceFor(4, 10, 3) == 1 && ShelfReserveMath.SpaceFor(4, 10, 4) == 0, "a stack of three takes one more, a stack of four none");
        C(ShelfReserveMath.SpaceFor(0, 10, 3) == 7 && ShelfReserveMath.SpaceFor(20, 10, -1) == 10, "no limit, or one past the stack size: a full stack");

        // The best at hand: simple, fine, lavish ranked 1, 2, 3.
        var bestOf = ShelfReserveMath.Best(new List<double> { 1, 2, 3 }, new List<bool> { true, true, false });
        C(bestOf.Count == 1 && bestOf[0] == 1, "no lavish meal at hand: the fine meal is the best there is");
        C(ShelfReserveMath.Best(new List<double> { 1, 2, 3 }, new List<bool> { true, true, true })[0] == 2, "a lavish meal at hand: it is the best");
        C(ShelfReserveMath.Best(new List<double> { 1, 2 }, new List<bool> { false, false }).Count == 0, "none at hand: no best");
        C(ShelfReserveMath.Best(new List<double> { 2, 2, 1 }, new List<bool> { true, true, true }).Count == 2, "two kinds ranked alike: either counts");
        C(ShelfReserveMath.MoveOut(6, null, 3, 1).Count == 0 && ShelfReserveMath.Missing(2, null).Count == 2, "an empty shelf misses every kind and moves nothing");

        // ---------------- Best first (the player's report, 14 November 2026) ----------------

        // Which kinds of a group are ranked: a meals group with drinks and food eaten only when starving in it.
        // Kinds: 0 fine meal, 1 simple meal, 2 fine cocktail (a drug), 3 canned meat (desperate only), 4 lavish meal.
        var mealsGroup = ShelfReserveMath.RankedKinds(new List<bool> { false, false, false, false, false }, new List<bool> { true, true, false, false, true });
        C(mealsGroup.Count == 3 && mealsGroup.Contains(0) && mealsGroup.Contains(1) && mealsGroup.Contains(4),
            "meals with cocktails and canned food in the group: the meals are ranked, the cocktail and the canned meat left out");
        C(ShelfReserveMath.OffersBest(new List<bool> { false, false, false }, new List<bool> { true, true, false }),
            "two meals and a cocktail: \"the best at hand\" is offered (it wasn't while any kind of the group couldn't be ranked)");
        C(!ShelfReserveMath.OffersBest(new List<bool> { false, false }, new List<bool> { true, false }), "one meal and a cocktail: nothing to choose between");
        C(ShelfReserveMath.RankedKinds(new List<bool> { true, true, true }, new List<bool> { false, false, false }).Count == 3, "all medicine: every kind ranked");
        C(ShelfReserveMath.RankedKinds(new List<bool> { true, false, false }, new List<bool> { false, false, true }).Count == 0,
            "medicine with drugs and food (manufactured goods): nothing is ranked, no best at hand");
        C(ShelfReserveMath.RankedKinds(new List<bool>(), new List<bool>()).Count == 0 && ShelfReserveMath.RankedKinds(null, null).Count == 0, "no kinds: none ranked");
        var artisan = Enumerable.Range(0, 58).Select(i => i < 5).ToList();
        C(ShelfReserveMath.RankedKinds(artisan.Select(_ => false).ToList(), artisan).Count == 0,
            "a group mostly not food (Alpha Crafts' artisan goods, five to eat of 58): nothing ranked, its \"any\" stays whichever comes first");
        C(ShelfReserveMath.RankedKinds(new List<bool> { false, false, false, false }, new List<bool> { true, true, false, false }).Count == 2,
            "half of the group food: its meals are ranked");

        // The player's mods (docs/player-mods/items.tsv, active rows): every meal of FoodMeals a colonist eats is ranked, the 23 others not.
        string[] prefs = { "Undefined", "NeverForNutrition", "DesperateOnly", "DesperateOnlyForHumanlikes", "RawBad", "RawTasty", "MealTerrible", "MealAwful", "MealSimple", "MealFine", "MealLavish" };
        var tsv = File.ReadAllLines(Path.Combine(root, "docs", "player-mods", "items.tsv"));
        var head = tsv[0].Split('\t').ToList();
        int cDef = head.IndexOf("defName"), cActive = head.IndexOf("active"), cCats = head.IndexOf("thingCategories"), cPref = head.IndexOf("preferability"), cDrug = head.IndexOf("drugCategory");
        var poolMeals = tsv.Skip(1).Select(l => l.Split('\t')).Where(r => r.Length > cDrug && r[cActive] == "yes" && r[cCats].Split(',').Contains("FoodMeals")).ToList();
        var poolEaten = poolMeals.Select(r => Array.IndexOf(prefs, r[cPref]) >= 4 && r[cDrug].Length == 0).ToList();
        var poolNoMedicine = poolMeals.Select(_ => false).ToList();
        var poolRanked = ShelfReserveMath.RankedKinds(poolNoMedicine, poolEaten);
        var left = Enumerable.Range(0, poolMeals.Count).Where(i => !poolRanked.Contains(i)).Select(i => poolMeals[i][cDef]).ToList();
        C(poolMeals.Count > 40 && left.Count == 23 && ShelfReserveMath.OffersBest(poolNoMedicine, poolEaten),
            $"the player's meals: {poolRanked.Count} ranked, the 23 drinks, desserts, canned and deep-fried goods left out, and the best at hand offered");
        C(left.Contains("VBE_FineCocktail") && left.Contains("VCE_LavishDessert") && left.Contains("VCE_CannedMeat") && left.Contains("AC_JamonSerrano")
          && !left.Contains("MealFine") && !left.Contains("VCE_FineBake") && !left.Contains("MealSimple"),
            "left out: Vanilla Brewing's cocktails, Vanilla Cooking's desserts and canned goods, Alpha Crafts' jamon; fine meals, bakes and simple meals ranked");

        // How colonists like a food (the game's FoodOptimality): taste through the game's curve, the def's own offset.
        C(ShelfReserveMath.TasteOptimality(0f) == 0f && ShelfReserveMath.TasteOptimality(5f) == 40f && ShelfReserveMath.TasteOptimality(12f) == 96f,
            "the game's curve: no taste nothing, a fine meal's +5 mood 40, a lavish meal's +12 96");
        C(Math.Abs(ShelfReserveMath.TasteOptimality(-4f) + 65f) < 0.01f && ShelfReserveMath.TasteOptimality(-100f) == -600f && ShelfReserveMath.TasteOptimality(300f) == 800f,
            "nutrient paste's -4 mood costs 65; the curve's ends hold beyond them");
        // The game's own meals: lavish (+12, offset 16), fine (+5, 16), simple (16), survival pack (simple, -5, worth 24), paste (awful, -4, 16).
        double lavish = ShelfReserveMath.FoodRank(true, 10, 12f, 16f, 40f), fine = ShelfReserveMath.FoodRank(true, 9, 5f, 16f, 20f);
        double simple = ShelfReserveMath.FoodRank(true, 8, 0f, 16f, 15f), pack = ShelfReserveMath.FoodRank(true, 8, 0f, -5f, 24f);
        double paste = ShelfReserveMath.FoodRank(true, 7, -4f, 16f, 10f), berries = ShelfReserveMath.FoodRank(false, 5, 0f, 0f, 1.2f);
        double babyFood = ShelfReserveMath.FoodRank(false, 6, 0f, 0f, 1.25f), raw = ShelfReserveMath.FoodRank(false, 4, 0f, 0f, 2f);
        C(lavish > fine && fine > simple && simple > pack && pack > paste && paste > berries && berries > raw,
            "a lavish meal, then fine, simple, a survival pack, nutrient paste, berries, raw food: the order colonists pick them in");
        C(simple > pack, "a simple meal before a survival pack, though the pack is worth more (its offset for people is -5 against 16)");
        C(paste > babyFood, "a terrible meal (baby food) after every meal: like raw food, it's only looked for when urgently hungry");
        double fineBake = ShelfReserveMath.FoodRank(true, 9, 5f, 16f, 20f), ruinedFineGrill = ShelfReserveMath.FoodRank(true, 9, 0f, -5f, 18f);
        C(fineBake == fine, "Vanilla Cooking's fine bake and the fine meal rank alike (the same taste, offset and worth): neither swaps for the other");
        C(ruinedFineGrill < simple, "a ruined fine grill (no taste, offset -5) after a simple meal, though it is called fine: colonists pick the simple meal");
        C(ShelfReserveMath.FoodRank(true, 10, 14f, 20f, 70f) > lavish, "a gourmet meal (+14, offset 20) before a lavish one");
        C(ShelfReserveMath.Unranked < raw && ShelfReserveMath.NoneComing < ShelfReserveMath.Unranked,
            "what colonists don't eat as a meal comes after every ranked kind; with nothing coming, anything may fill a slot");

        // An empty slot takes only as good as the best that would come.
        C(!ShelfReserveMath.TakesEmptySlot(simple, fine) && ShelfReserveMath.TakesEmptySlot(fine, fine) && ShelfReserveMath.TakesEmptySlot(lavish, fine),
            "fine meals would come: a simple meal doesn't take the empty slot, a fine or lavish one does");
        C(ShelfReserveMath.TakesEmptySlot(simple, ShelfReserveMath.NoneComing) && ShelfReserveMath.TakesEmptySlot(ShelfReserveMath.Unranked, ShelfReserveMath.NoneComing),
            "nothing would come: a simple meal, or even a cocktail, fills it");
        C(!ShelfReserveMath.TakesEmptySlot(ShelfReserveMath.Unranked, simple), "a cocktail doesn't take a meal slot while a meal would come");

        // Filling best first: slots 0 to 2 any meal (ranked), 3 wood.
        ShelfReserveMath.Stack R(string key, int count, double rank, params int[] covers) => new ShelfReserveMath.Stack { key = key, count = count, covers = covers, rank = rank };
        var mealShelf = new List<ShelfReserveMath.Stack>
        {
            R("MealSimple", 10, simple, 0, 1, 2), R("MealSimple", 8, simple, 0, 1, 2), R("VCE_SimpleBake", 6, simple, 0, 1, 2), R("MealFine", 2, fine, 0, 1, 2), R("WoodLog", 75, 0, 3)
        };
        var bestFill = ShelfReserveMath.Fill(4, mealShelf);
        C(bestFill.Take(3).Contains(3) && bestFill[3] == 4 && !bestFill.Contains(2), "the fine meals take a meal slot before bigger stacks of simple meals; the smallest simple stack is left over");
        C(ShelfReserveMath.MoveOut(5, mealShelf, 4, 1).SequenceEqual(new[] { 2 }), "room needed: the simple stack left over moves out, not the fine meals");

        // The player's shelf (the Ballroom's wooden big shelf): any meals, 3 slots, all filled with simple meals and
        // bakes; wood; steel and components; fine meals in the fridge.
        var ballroom = new List<ShelfReserveMath.Stack>
        {
            R("MealSimple", 4, simple, 0, 1, 2), R("VCE_SimpleBake", 4, simple, 0, 1, 2), R("MealSimple", 3, simple, 0, 1, 2), R("WoodLog", 75, 0, 3),
            R("Steel", 75, 0), R("Steel", 75, 0), R("Steel", 75, 0), R("ComponentIndustrial", 34, 0)
        };
        var anyMeals = new List<int> { 0, 1, 2 };
        var ballFill = ShelfReserveMath.Fill(4, ballroom);
        C(ShelfReserveMath.SwapDue(ballroom, ballFill, anyMeals, fine) == 2, "fine meals in the fridge: the smallest stack of simple meals is due to give its slot up");
        C(ShelfReserveMath.SwapDue(ballroom, ballFill, anyMeals, simple) == -1 && ShelfReserveMath.SwapDue(ballroom, ballFill, anyMeals, ShelfReserveMath.NoneComing) == -1,
            "only simple meals would come, or nothing: no swap (kinds ranked alike never swap)");
        C(ShelfReserveMath.SwapOut(ballroom, ballFill, anyMeals, fine, 2, new HashSet<int>()) == 2, "the shelf full, no meal stack spare: the simple meals filling the slot go back to the store");
        C(ShelfReserveMath.SwapOut(ballroom, ballFill, anyMeals, fine, 2, new HashSet<int> { 2 }) == -1, "already on its way out: nothing more goes");
        var oneEmpty = new List<ShelfReserveMath.Stack>(ballroom);
        oneEmpty.RemoveAt(2);
        C(ShelfReserveMath.SwapDue(oneEmpty, ShelfReserveMath.Fill(4, oneEmpty), anyMeals, fine) == -1, "a meal slot still empty: no swap, the empty slot waits for the fine meals");
        var spare = new List<ShelfReserveMath.Stack>(ballroom) { R("MealSimple", 1, simple, 0, 1, 2) };
        var spareFill = ShelfReserveMath.Fill(4, spare);
        C(ShelfReserveMath.SwapOut(spare, spareFill, anyMeals, fine, ShelfReserveMath.SwapDue(spare, spareFill, anyMeals, fine), new HashSet<int>()) == 8,
            "a spare simple meal stack filling no slot goes back first, the slots keep theirs");
        var spareFine = new List<ShelfReserveMath.Stack>(ballroom) { R("MealFine", 1, fine, 0, 1, 2) };
        var spareFineFill = ShelfReserveMath.Fill(4, spareFine);
        C(ShelfReserveMath.SwapOut(spareFine, spareFineFill, anyMeals, fine, ShelfReserveMath.SwapDue(spareFine, spareFineFill, anyMeals, fine), new HashSet<int>()) != 8,
            "a stack as good as the better kind never goes for it");

        // It plays out to the best meals on the shelf and never carries a stack back and forth: the fridge holds 12 fine
        // meals (three hauls of four); each step a fine meal comes into an empty or free slot, or a lesser stack goes back.
        var shelfNow = new List<ShelfReserveMath.Stack>(ballroom);
        int fridgeFine = 12, carriedBack = 0, steps = 0;
        bool backAgain = false;
        while (steps++ < 30)
        {
            double comingBest = fridgeFine > 0 ? fine : simple;
            var f = ShelfReserveMath.Fill(4, shelfNow);
            bool empty = anyMeals.Any(k => f[k] < 0);
            int due = ShelfReserveMath.SwapDue(shelfNow, f, anyMeals, comingBest);
            if (!empty && due < 0) break;
            // A simple meal carried back would come in again only into an empty slot it may take, or a free slot left over.
            if (carriedBack > 0 && empty && ShelfReserveMath.TakesEmptySlot(simple, comingBest)) backAgain = true;
            if (carriedBack > 0 && ShelfReserveMath.MayTakeSlot(8, shelfNow.Count, false, (empty ? 1 : 0) + (due >= 0 ? 1 : 0))) backAgain = true;
            if (empty || shelfNow.Count < 8)
            {
                shelfNow.Add(R("MealFine", 4, fine, 0, 1, 2));
                fridgeFine -= 4;
                continue;
            }
            shelfNow.RemoveAt(ShelfReserveMath.SwapOut(shelfNow, f, anyMeals, comingBest, due, new HashSet<int>()));
            carriedBack++;
        }
        var endFill = ShelfReserveMath.Fill(4, shelfNow);
        C(steps < 30 && anyMeals.All(k => endFill[k] >= 0 && shelfNow[endFill[k]].key == "MealFine") && carriedBack == 3 && fridgeFine == 0,
            "the three meal slots end with the fine meals, each simple stack carried back once");
        C(!backAgain, "no simple meal carried back may come in again while fine meals would come");

        // The wait for a better kind gives up after three hours, and the kind is left out for a day.
        C(!ShelfReserveMath.GivesUp(-1, 100000) && !ShelfReserveMath.GivesUp(1000, 1000 + ShelfReserveMath.GateTicks) && ShelfReserveMath.GivesUp(1000, 1001 + ShelfReserveMath.GateTicks),
            "not waiting: never; three hours: still waiting; past them: a lesser kind may fill the slot");
        C(ShelfReserveMath.GateTicks == 7500 && ShelfReserveMath.RestTicks == 60000, "three hours of waiting, a day left out");

        // ---------------- Making room on a full shelf ----------------

        // Whether a colonist ordered to haul a stack may make room for it.
        C(ShelfReserveMath.MayMakeRoom(6, 6, true, true, true), "a full shelf with an empty meal slot: room can be made for the meal");
        C(!ShelfReserveMath.MayMakeRoom(6, 5, true, true, true), "a free slot already: nothing to make room for (the game's own haul finds it)");
        C(!ShelfReserveMath.MayMakeRoom(6, 6, false, true, true), "a stack that fills no empty reserved slot: no room is made for it");
        C(!ShelfReserveMath.MayMakeRoom(6, 6, true, false, true), "the shelf's storage settings don't allow it: no room is made");
        C(!ShelfReserveMath.MayMakeRoom(6, 6, true, true, false), "the shelf isn't better storage than where it lies: no room is made (the game's own rule for a haul)");
        C(!ShelfReserveMath.MayMakeRoom(0, 0, true, true, true), "a shelf that holds nothing: no room");

        // Which stack goes: the kitchen shelf (slot 0 any meal, empty; slot 1 beer), full of beer, wood, cloth and herbal.
        var kitchenFull = new List<ShelfReserveMath.Stack> { S("Beer", 25, 1), S("WoodLog", 75), S("Cloth", 20), S("MedicineHerbal", 10) };
        var kWorth = new List<float> { 300f, 90f, 30f, 100f };
        var allMovable = new List<bool> { true, true, true, true };
        var noneMarked = new List<bool> { false, false, false, false };
        C(ShelfReserveMath.RoomFrom(kitchenFull, noneMarked, kWorth, allMovable) == 2, "nothing marked to move out: the stack of least worth of no reserved kind goes (the cloth)");
        C(ShelfReserveMath.RoomFrom(kitchenFull, new List<bool> { false, true, false, false }, kWorth, allMovable) == 1,
            "a stack the reserves mark to move out goes first, whatever it's worth (the wood)");
        C(ShelfReserveMath.RoomFrom(kitchenFull, noneMarked, kWorth, new List<bool> { true, true, false, true }) == 1,
            "the cloth can't be taken (reserved, out of reach): the next of least worth goes");
        C(ShelfReserveMath.RoomFrom(kitchenFull, new List<bool> { false, true, false, false }, kWorth, new List<bool> { true, false, true, true }) == 2,
            "the marked stack can't be taken: the stack of least worth of no reserved kind goes instead");
        var onlyKept = new List<ShelfReserveMath.Stack> { S("Beer", 25, 1), S("MealFine", 10, 0, 2) };
        C(ShelfReserveMath.RoomFrom(onlyKept, new List<bool> { false, false }, new List<float> { 10f, 200f }, new List<bool> { true, true }) == -1,
            "never a stack of a reserved kind the reserves don't mark, however cheap: no room");
        C(ShelfReserveMath.RoomFrom(new List<ShelfReserveMath.Stack> { new ShelfReserveMath.Stack { key = "MealSimple", count = 4, covers = new int[0], barred = true } },
            new List<bool> { false }, new List<float> { 40f }, new List<bool> { true }) == -1, "a stack of a kind kept off for now goes only when marked");
        C(ShelfReserveMath.RoomFrom(new List<ShelfReserveMath.Stack> { S("Cloth", 30), S("Cloth", 10) }, new List<bool> { false, false }, new List<float> { 45f, 45f },
            new List<bool> { true, true }) == 1, "two of the same worth: the smaller stack goes");
        C(ShelfReserveMath.RoomFrom(null, null, null, null) == -1 && ShelfReserveMath.RoomFrom(kitchenFull, null, null, null) == 3,
            "nothing on the shelf: none; nothing known of worth or reach: the smallest stack of no reserved kind");

        // The hospital shelf full of herbal with glitterworld medicine missing: the reserves' own choice, counting it as
        // waiting (a stack ordered there from outside the home area isn't counted yet), is a herbal stack its slot doesn't need.
        var herbalFull = new List<ShelfReserveMath.Stack>
        {
            S("MedicineHerbal", 25, 0), S("MedicineHerbal", 25, 0), S("MedicineHerbal", 8, 0), S("MedicineHerbal", 25, 0), S("MedicineIndustrial", 25, 1), S("MedicineHerbal", 25, 0)
        };
        var herbalOut = ShelfReserveMath.MoveOut(6, herbalFull, 3, 1);
        var herbalMarked = new List<bool>();
        for (int i = 0; i < herbalFull.Count; i++) herbalMarked.Add(herbalOut.Contains(i));
        int herbalPick = ShelfReserveMath.RoomFrom(herbalFull, herbalMarked, new List<float> { 250f, 250f, 80f, 250f, 450f, 250f }, new List<bool> { true, true, true, true, true, true });
        C(herbalPick == 2, "every stack is of a reserved kind: the herbal stack the reserves would move (the smallest of what it holds most of) goes");
        C(herbalPick != 4 && herbalPick != 0, "the stacks filling their own slots never go for the glitterworld medicine");

        // On their own: how many stacks must still be set down beside the shelf.
        C(ShelfReserveMath.ShortOfRoom(6, 6, 1, 0) == 1, "full, a kind waiting, nothing on its way out: one stack is set down");
        C(ShelfReserveMath.ShortOfRoom(6, 6, 1, 1) == 0, "a stack already being carried out: nobody sets down a second one");
        C(ShelfReserveMath.ShortOfRoom(6, 5, 1, 0) == 0, "a free slot for the waiting kind: nothing is set down");
        C(ShelfReserveMath.ShortOfRoom(6, 5, 2, 0) == 1 && ShelfReserveMath.ShortOfRoom(6, 6, 2, 1) == 1, "two kinds waiting, one slot free or one stack leaving: one more is set down");
        C(ShelfReserveMath.ShortOfRoom(6, 6, 0, 0) == 0, "nothing waiting, or only a kind kept in storage as good as the shelf (it never comes): nothing is set down, however full");
        C(ShelfReserveMath.ShortOfRoom(6, 7, 1, 0) == 1, "a shelf holding more than its count (another mod's rule): no free slot, one set down");
        // It never loops: a stack set down isn't put back while it would take the slot kept for the waiting kind.
        C(!ShelfReserveMath.MayTakeSlot(6, 5, false, 1), "the stack set down stays off: the free slot is kept for the waiting kind");
        C(ShelfReserveMath.MayTakeSlot(6, 5, true, 1), "the waiting kind takes the freed slot");
        C(!ShelfReserveMath.MayTakeSlot(6, 6, false, 0), "with the waiting kind in, the shelf is full again and the stack stays off");

        // ---------------- The seams of making room ----------------

        string src = Path.Combine(root, "Source", "ColonyCouncil", "Building");
        string room = File.ReadAllText(Path.Combine(src, "ShelfMakeRoom.cs"));
        string reserves = File.ReadAllText(Path.Combine(src, "ShelfReserves.cs"));
        foreach (var (cls, target) in new[]
                 {
                     ("Patch_ShelfMakeRoomAuto", "[HarmonyPatch(typeof(HaulAIUtility), nameof(HaulAIUtility.HaulToStorageJob))]"),
                     ("Patch_ShelfMakeRoomMenu", "[HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.GetOptions))]")
                 })
        {
            var m = Regex.Match(room, Regex.Escape(target) + @"\s*public static class " + cls + @"\b(.*?)\n    \}", RegexOptions.Singleline);
            C(m.Success, $"{cls} is a patch class of its own on the game's method, applied by RimfolkPatches.ApplyAll");
            C(m.Success && m.Groups[1].Value.Contains("public static void Postfix(") && !m.Groups[1].Value.Contains("Prefix") && m.Groups[1].Value.Contains("catch (Exception e)"),
                $"{cls} is a postfix, its errors caught");
        }
        C(room.Contains("if (__result != null || broken || ShelfReserves.Reserved.Count == 0 || t == null || p == null) return;"),
            "on their own: only where the game's haul found no storage, and only with a reserved shelf");
        C(room.Contains("if (st == null || st.waiting <= 0 || !st.moveOut.Contains(t)) return null;")
          && room.Contains("ShelfReserveMath.ShortOfRoom(st.capacity, st.held, st.waiting, leaving) <= 0")
          && room.Contains("map.reservationManager.IsReservedByAnyoneOf(o, Faction.OfPlayer)"),
            "on their own: a stack the reserves mark, while a kind waits and the shelf is short of room, counting stacks on their way out");
        C(room.Contains("ShelfReserveMath.ShortOfRoom(st.capacity, st.held, reserves.Coming(st, shelf), leaving) <= 0")
          && reserves.Contains("if (stored && ((int)g.Settings.Priority > (int)priority || Cached(other, s => s.holding.Contains(t), true))) continue;")
          && reserves.Contains("if (!st.here[k] && st.onMap[k] && st.comes[st.slotEntry[k]]) n++;"),
            "on their own: only for a kind the hauling would bring (loose in the home area, or in storage of the shelf's priority or lower, not holding another shelf's slot)");
        C(room.Contains("HaulAIUtility.CanHaulAside(p, t, out var cell) ? MoveJob(t, cell, shelf, false)"), "set down on a free cell beside the shelf (the game's haul aside)");
        C(room.Contains("if (StoreUtility.TryFindBestBetterStorageFor(thing, pawn, map, current, pawn.Faction, out _, out _)) return null;")
          && room.Contains("var current = StoreUtility.CurrentStoragePriorityOf(thing, true);"),
            "ordered: only where the game's own haul finds no cell");
        C(room.Contains("ShelfReserveMath.MayMakeRoom(st.capacity, st.held, st.FillsMissing(thing.def), allowed, better)")
          && room.Contains("ShelfReserveMath.RoomFrom(st.stacks, st.things.Select(marked.Contains).ToList(), worth, movable)")
          && room.Contains("ShelfReserveMath.MoveOut(st.capacity, st.stacks, st.kinds.Count, st.waiting + 1, st.caps)"),
            "ordered: the pure rules decide whether room is made and which stack goes");
        C(room.Contains("StoreUtility.TryFindBestBetterStorageFor(occupant, pawn, pawn.Map, StoragePriority.Unstored, pawn.Faction, out var cell, out var dest) && dest != shelf")
          && room.Contains("HaulAIUtility.CanHaulAside(pawn, occupant, out var aside)"),
            "the stack off the shelf goes to its best other storage, else beside the shelf");
        C(room.Contains("pawn.jobs.TryTakeOrderedJob(plan.first, JobTag.Misc)") && room.Contains("JobMaker.MakeJob(JobDefOf.HaulToCell, plan.thing, plan.freed)")
          && room.Contains("if (!haul.TryMakePreToilReservations(pawn, false)) return;") && room.Contains("pawn.jobs.jobQueue.EnqueueFirst(haul, JobTag.Misc)"),
            "ordered: two jobs, the game's haul into the freed cell queued next and reserved from the order on");
        C(Regex.IsMatch(room, @"protected override bool AppliesInt\(FloatMenuContext context\) =>\s*ShelfReserves\.On")
          && room.Contains("public class FloatMenuOptionProvider_RimfolkShelfRoom : FloatMenuOptionProvider"),
            "the menu's provider stands down with the setting or the deeper hooks off");
        C(room.Contains("pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling) || !pawn.workSettings.WorkIsActive(WorkTypeDefOf.Hauling)"),
            "someone who doesn't haul keeps the game's own line");
        C(room.Contains("x.Label.StartsWith(head) && x.Label.EndsWith(tail)") && room.Contains("HaulAIUtility.NoEmptyPlaceLowerTrans"),
            "only the game's \"Cannot haul\" line for want of a spot is taken out, and only beside the line that works");
        C(reserves.Contains("public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.shelfReserves ?? true);"),
            "everything behind the shelf reserves setting and the deeper game hooks");
        C(reserves.Contains("return ShelfReserveMath.MayTakeSlot(capacity, st.held, fills, st.waiting + st.swaps);")
          && reserves.Contains("bool fills = st.FillsMissing(thing.def) || st.swaps > 0 && st.FillsSwap(thing.def);"),
            "the store rule that keeps a stack set down off the waiting slot, and a free slot for a better kind's swap");
        C(reserves.Contains("public bool Admits(int e, ThingDef d) => !ranked[e] || ShelfReserveMath.TakesEmptySlot(RankIn(e, d), bestComing[e]);")
          && reserves.Contains("if (!here[k] && kinds[k].Matches(d) && Admits(slotEntry[k], d)) return true;"),
            "an empty slot of any meals takes only as good as the best that would come");

        // Best first, on the game's side.
        C(reserves.Contains("var stages = ing.tasteThought?.stages;") && reserves.Contains("ing.optimalityOffsetHumanlikes, d.BaseMarketValue")
          && reserves.Contains("ing.preferability >= FoodPreferability.MealAwful"),
            "a food's rank is read from its taste thought, its offset for people, its preferability and worth");
        C(reserves.Contains("d?.ingestible != null && !d.IsDrug && !d.IsMedicine && d.ingestible.preferability >= FoodPreferability.RawBad"),
            "eaten as a meal: no drug, no medicine, raw food or better");
        C(reserves.Contains("int go = ShelfReserveMath.SwapOut(stacks, filledBy, slotsOf[e], best, low, taken);")
          && reserves.Contains("if (go < 0 || !HasOtherStore(things[go], shelf)) continue;") && reserves.Contains("if (free > 0) free--;"),
            "a swap goes into a free slot, else the lesser stack goes back to a store that would take it, never set down");
        C(reserves.Contains("if (!looking.Add(shelf)) return had ? st : null;") && !reserves.Contains("CurrentStoragePriorityOf"),
            "a look never asks a storage whether it accepts a stack (which could look again), and never looks at its own shelf twice");
        C(reserves.Contains("WaitForBetter(shelf, st, e, slotsOf[e].Count(k => !st.here[k]), now);") && reserves.Contains("rests[key] = now + ShelfReserveMath.RestTicks;"),
            "the wait for a better kind gives up, and that kind is left out for a while");
        // Fetched from storage of the shelf's own priority.
        var fetch = Regex.Match(reserves, Regex.Escape("[HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.IsInValidBestStorage))]") + @"\s*public static class Patch_ShelfReserveFetch\b(.*?)\n    \}",
            RegexOptions.Singleline);
        C(fetch.Success && fetch.Groups[1].Value.Contains("public static void Postfix(Thing t, ref bool __result)") && fetch.Groups[1].Value.Contains("catch (Exception e)")
          && fetch.Groups[1].Value.Contains("if (!__result || broken || ShelfReserves.Reserved.Count == 0 || t == null || !t.Spawned || !ShelfReserves.On) return;"),
            "Patch_ShelfReserveFetch is a postfix of its own on the game's best-storage check, only with a reserved shelf, its errors caught");
        C(reserves.Contains("if (settings == null || settings.Priority != g.Settings.Priority) continue;") && reserves.Contains("if (st.wantCount <= Incoming(s)) continue;"),
            "fetched only from storage of the shelf's own priority (lower the game's hauling empties itself; higher keeps it), no more than the slots waiting");
        C(reserves.Contains("StoreUtility.TryFindBestBetterStoreCellForIn(t, p, map, StoragePriority.Unstored, p.Faction, group, out var cell)")
          && reserves.Contains("var job = HaulAIUtility.HaulToCellStorageJob(p, t, cell, false);"),
            "the fetch is the game's own haul to a cell of the shelf, the cell found by the game's search");
        C(room.Contains("var job = ShelfMakeRoom.SetDownJob(p, t) ?? ShelfReserves.FetchJob(p, t);"), "fetching only where the game's haul found no better storage");
        C(room.Contains("bool better = (int)settings.Priority >= (int)current;"), "ordered: room is made on a shelf of the same priority as where the stack lies too");
        // The job and its words.
        var job = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_ShelfReserves.xml")).Root!.Element("JobDef");
        C(job?.Element("defName")?.Value == "Rimfolk_MakeRoomOnShelf" && job.Element("driverClass")?.Value == "ColonyCouncil.JobDriver_MakeRoomOnShelf",
            "the job making room, on the game's haul to a cell");
        C(room.Contains("public class JobDriver_MakeRoomOnShelf : JobDriver_HaulToCell") && room.Contains("public static JobDef Rimfolk_MakeRoomOnShelf;"),
            "its driver is the game's haul to a cell, its report Rimfolk's");
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Stores.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        string pick = File.ReadAllText(Path.Combine(src, "ShelfReservePick.cs"));
        foreach (var code in new[] { room, reserves, pick })
            foreach (Match m in Regex.Matches(code, "\"(CC_Shelf(?:Pick)?_[A-Za-z_]+)\""))
                C(keyed.ContainsKey(m.Groups[1].Value), $"{m.Groups[1].Value} exists");
        C(keyed.TryGetValue("CC_Shelf_BetterComing", out var betterLine) && betterLine.Contains("{BETTER}") && betterLine.Contains("{LESSER}"),
            "the status names the better kind and the one it replaces");
        C(pick.Contains("(r.ranked ? \"CC_ShelfPick_AnyBestFirstTip\" : \"CC_ShelfPick_AnyTip\")") && keyed["CC_ShelfPick_AnyBestFirstTip"].Contains("best first"),
            "the picker's \"any\" says when it is kept best first");
        C(keyed.TryGetValue("CC_Shelf_MakeRoomHaul", out var line) && line.Contains("{SHELF}") && line.Contains("{THING}") && !line.Contains("!"),
            "the menu's line names the shelf and the stack");
        C(keyed.TryGetValue("CC_Shelf_MakingRoomFor", out var report) && report.StartsWith("making room on the {SHELF} for {KIND}"),
            "the job says what they do: making room on the shelf for the kind");

        // Where a drink is had.
        C(ChewSpotMath.StoreRoom(false, -8f, 20, 0), "a freezer is for keeping things");
        C(ChewSpotMath.StoreRoom(false, -8f, 20, 2), "even a freezer with a chair in it");
        C(ChewSpotMath.StoreRoom(false, 18f, 12, 0), "a warm store room with nowhere to sit too");
        C(!ChewSpotMath.StoreRoom(false, 18f, 4, 6), "a dining room with a shelf in it is for staying");
        C(!ChewSpotMath.StoreRoom(false, -5f, 0, 6), "a cold dining room with no stores is still a dining room");
        C(!ChewSpotMath.StoreRoom(true, -8f, 20, 0), "outdoors is never a store room");
        return n;
    }
}
