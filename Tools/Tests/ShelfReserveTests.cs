using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Reserved shelf slots (Building/ShelfReserveMath): which slots are empty (each needing a stack of its own), when a
/// new stack may take a slot, which stack moves out to make room, the best of a group at hand, and making room on a full
/// shelf (by a colonist's order, and on their own when nothing else stores the stack that must go), with the seams of
/// making room (Building/ShelfMakeRoom.cs); and which rooms are for keeping things, not for staying (ChewSpotMath).
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
        var bestOf = ShelfReserveMath.Best(new List<float> { 1f, 2f, 3f }, new List<bool> { true, true, false });
        C(bestOf.Count == 1 && bestOf[0] == 1, "no lavish meal at hand: the fine meal is the best there is");
        C(ShelfReserveMath.Best(new List<float> { 1f, 2f, 3f }, new List<bool> { true, true, true })[0] == 2, "a lavish meal at hand: it is the best");
        C(ShelfReserveMath.Best(new List<float> { 1f, 2f }, new List<bool> { false, false }).Count == 0, "none at hand: no best");
        C(ShelfReserveMath.Best(new List<float> { 2f, 2f, 1f }, new List<bool> { true, true, true }).Count == 2, "two kinds ranked alike: either counts");
        C(ShelfReserveMath.MoveOut(6, null, 3, 1).Count == 0 && ShelfReserveMath.Missing(2, null).Count == 2, "an empty shelf misses every kind and moves nothing");

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
          && reserves.Contains("if (stored && (int)StoreUtility.CurrentStoragePriorityOf(t) >= (int)priority) continue;"),
            "on their own: only for a kind the usual hauling would bring (loose in the home area, or in storage below the shelf)");
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
        C(reserves.Contains("return ShelfReserveMath.MayTakeSlot(capacity, st.held, fills, st.waiting);"), "the store rule that keeps a stack set down off the waiting slot");
        // The job and its words.
        var job = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_ShelfReserves.xml")).Root!.Element("JobDef");
        C(job?.Element("defName")?.Value == "Rimfolk_MakeRoomOnShelf" && job.Element("driverClass")?.Value == "ColonyCouncil.JobDriver_MakeRoomOnShelf",
            "the job making room, on the game's haul to a cell");
        C(room.Contains("public class JobDriver_MakeRoomOnShelf : JobDriver_HaulToCell") && room.Contains("public static JobDef Rimfolk_MakeRoomOnShelf;"),
            "its driver is the game's haul to a cell, its report Rimfolk's");
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Stores.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (Match m in Regex.Matches(room, "\"(CC_Shelf_[A-Za-z_]+)\""))
            C(keyed.ContainsKey(m.Groups[1].Value), $"{m.Groups[1].Value} exists");
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
