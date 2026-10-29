using System;
using System.Collections.Generic;
using ColonyCouncil;

/// <summary>
/// Reserved shelf slots (Building/ShelfReserveMath): which slots are empty (each needing a stack of its own), when a
/// new stack may take a slot, which stack moves out to make room, and the best of a group at hand; and which rooms are
/// for keeping things, not for staying (ChewSpotMath).
/// </summary>
public static class ShelfReserveTests
{
    private static ShelfReserveMath.Stack S(string key, int count, params int[] covers) =>
        new ShelfReserveMath.Stack { key = key, count = count, covers = covers };

    public static int Run(Action<bool, string> check)
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

        // The best at hand: simple, fine, lavish ranked 1, 2, 3.
        var bestOf = ShelfReserveMath.Best(new List<float> { 1f, 2f, 3f }, new List<bool> { true, true, false });
        C(bestOf.Count == 1 && bestOf[0] == 1, "no lavish meal at hand: the fine meal is the best there is");
        C(ShelfReserveMath.Best(new List<float> { 1f, 2f, 3f }, new List<bool> { true, true, true })[0] == 2, "a lavish meal at hand: it is the best");
        C(ShelfReserveMath.Best(new List<float> { 1f, 2f }, new List<bool> { false, false }).Count == 0, "none at hand: no best");
        C(ShelfReserveMath.Best(new List<float> { 2f, 2f, 1f }, new List<bool> { true, true, true }).Count == 2, "two kinds ranked alike: either counts");
        C(ShelfReserveMath.MoveOut(6, null, 3, 1).Count == 0 && ShelfReserveMath.Missing(2, null).Count == 2, "an empty shelf misses every kind and moves nothing");

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
