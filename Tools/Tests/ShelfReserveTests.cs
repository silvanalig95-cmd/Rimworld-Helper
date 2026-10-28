using System;
using System.Collections.Generic;
using ColonyCouncil;

/// <summary>
/// Reserved shelf slots (Building/ShelfReserveMath): which kinds are missing, when a new stack may take a slot, and
/// which stack moves out to make room; and which rooms are for keeping things, not for staying (ChewSpotMath).
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
        C(ShelfReserveMath.MoveOut(2, lastOnes, 3, 1).Count == 0, "the last stack of a reserved kind never moves out to make room for another");

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
