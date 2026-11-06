using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Choosing another landing site in a new game's first days (the player's request, 6 November 2026; Colonies/LandingMath.cs):
/// when it is offered and why not, who comes along, the starting kit followed through hauling, merging and use and never
/// counted larger, where it goes on the new map, the map's sizes and seed, and the code's seams: the record of the start
/// only at the game's own start, saved only while the offer is open, the same settlement kept, no founding, abandoning or
/// mood, the colony never lost between maps, the menu's entry, and the words.
/// </summary>
public static class LandingTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "landing: " + what);
        }

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        var keyed = Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml")
            .SelectMany(f => XDocument.Load(f).Root!.Elements()).GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => g.First().Value);
        const int Day = LandingMath.DayTicks;

        // ================= When it is offered =================

        C(LandingMath.OfferDays == 3 && LandingMath.OfferTicks == 3 * Day, "offered for the first three days");
        C(LandingMath.Open(0, 0) && LandingMath.Open(3 * Day - 1, 0) && !LandingMath.Open(3 * Day, 0), "open from the landing until three days have passed");
        C(!LandingMath.Open(5, -1) && !LandingMath.Open(4, 10), "never without a landing, never before it");
        C(LandingMath.Open(10 * Day + 5, 10 * Day), "open again for three days after a landing chosen here");
        C(LandingMath.Shows(true, true, Day, 0, false), "the entry shows in a start Rimfolk saw, in its first days");
        C(!LandingMath.Shows(false, true, Day, 0, false) && !LandingMath.Shows(true, false, Day, 0, false) && !LandingMath.Shows(true, true, Day, 0, true)
          && !LandingMath.Shows(true, true, 4 * Day, 0, false), "but not with the deeper hooks off, an unseen start, the tutorial or after the first days");

        C(LandingMath.WhyNot(new LandingState()) == LandingWhy.None, "nothing in the way: offered");
        var order = new (Action<LandingState> set, LandingWhy why)[]
        {
            (s => s.on = false, LandingWhy.Off), (s => s.recorded = false, LandingWhy.NotSeen), (s => s.open = false, LandingWhy.Over),
            (s => s.tutorial = true, LandingWhy.Tutorial), (s => s.gravship = true, LandingWhy.Gravship), (s => s.space = true, LandingWhy.Space),
            (s => s.homes = 2, LandingWhy.Homes), (s => s.plans = true, LandingWhy.Plans), (s => s.monolithWoken = true, LandingWhy.Monolith),
            (s => s.away = true, LandingWhy.Away), (s => s.inside = true, LandingWhy.Inside), (s => s.guests = true, LandingWhy.Guests),
            (s => s.quest = true, LandingWhy.Quest), (s => s.threat = true, LandingWhy.Threat)
        };
        foreach (var (set, why) in order)
        {
            var s = new LandingState();
            set(s);
            C(LandingMath.WhyNot(s) == why, "on its own, " + why + " is the reason");
        }
        for (int i = 0; i < order.Length; i++)
        {
            var s = new LandingState();
            for (int j = i; j < order.Length; j++) order[j].set(s);
            C(LandingMath.WhyNot(s) == order[i].why, "with every later reason too, the first told is " + order[i].why);
        }
        C(LandingMath.WhyNot(new LandingState { startHome = false }) == LandingWhy.Homes && LandingMath.WhyNot(new LandingState { homes = 0 }) == LandingWhy.Homes,
            "only while the one home is the map it landed on");
        C(Enum.GetValues(typeof(LandingWhy)).Cast<LandingWhy>().Where(w => w != LandingWhy.None).All(w => keyed.ContainsKey("CC_Landing_No_" + w)),
            "every reason has its words");

        // ================= Who comes along =================

        C(LandingMath.SideOf(true, false, false, true, false, false) == LandingSide.Colonist, "a colonist");
        C(LandingMath.SideOf(false, true, false, true, false, false) == LandingSide.Prisoner, "a prisoner of the colony, of whatever people");
        C(LandingMath.SideOf(true, false, true, true, false, false) == LandingSide.Slave && LandingMath.SideOf(false, false, true, true, false, false) == LandingSide.Slave,
            "a slave of the colony");
        C(LandingMath.SideOf(true, false, false, false, true, false) == LandingSide.Mech && LandingMath.SideOf(true, false, false, false, false, true) == LandingSide.Animal,
            "the colony's mechs and animals");
        C(LandingMath.SideOf(false, false, false, true, false, false) == LandingSide.Other && LandingMath.SideOf(false, false, false, false, false, true) == LandingSide.Other,
            "a visitor or a wild animal is nobody's to bring");
        C(new[] { LandingSide.Colonist, LandingSide.Prisoner, LandingSide.Slave, LandingSide.Mech, LandingSide.Animal }.All(LandingMath.Comes) && !LandingMath.Comes(LandingSide.Other),
            "every colonist, prisoner, slave, mech and animal of the colony comes, nobody else");
        C(LandingMath.OwnPod(LandingSide.Colonist) && LandingMath.OwnPod(LandingSide.Prisoner) && LandingMath.OwnPod(LandingSide.Slave)
          && !LandingMath.OwnPod(LandingSide.Mech) && !LandingMath.OwnPod(LandingSide.Animal), "each person a pod of their own, as at the start; mechs and animals share");
        C(LandingMath.Guest(true, false, false, false) && !LandingMath.Guest(true, true, false, false) && !LandingMath.Guest(true, false, true, false)
          && !LandingMath.Guest(true, false, false, true) && !LandingMath.Guest(false, false, false, false), "a guest: hosted by the colony, not its own, its prisoner or its slave");

        // ================= The starting kit =================

        C(LandingMath.KindOf(true, true, true) == LandingKind.Gear, "a starting colonist's own gear is theirs, wherever it lies");
        C(LandingMath.KindOf(false, true, true) == LandingKind.Near && LandingMath.KindOf(false, true, false) == LandingKind.Anywhere, "scattered near the start, or anywhere");
        C(LandingMath.KindOf(false, false, false) == LandingKind.Pods, "else it came with the people");
        C(LandingMath.Recorded(true, false, LandingKind.Pods) && LandingMath.Recorded(false, true, LandingKind.Anywhere) && !LandingMath.Recorded(false, true, LandingKind.Near)
          && !LandingMath.Recorded(false, false, LandingKind.Pods), "items are the kit, and the buildings scattered anywhere (ship chunks)");
        C(LandingMath.PlaceOf(LandingKind.Gear) == LandingKind.Pods && LandingMath.PlaceOf(LandingKind.Pods) == LandingKind.Pods
          && LandingMath.PlaceOf(LandingKind.Near) == LandingKind.Near && LandingMath.PlaceOf(LandingKind.Anywhere) == LandingKind.Anywhere,
            "on the new map: with the people, near the new start, or anywhere, as on the first");

        C(LandingMath.KitIn(450, 300) == 300 && LandingMath.KitIn(450, 500) == 450 && LandingMath.KitIn(-3, 10) == 0 && LandingMath.KitIn(5, 0) == 0,
            "the kit's part of a stack: never more than the stack, never below none");
        bool conserved = true, firstOff = true, merged = true;
        foreach (int before in new[] { 1, 2, 7, 75, 450, 550 })
        foreach (int kit in new[] { -1, 0, 1, 74, 75, 450, 600 })
        foreach (int split in new[] { 0, 1, 50, 75, 449, 450, 549 })
        {
            if (split > before) continue;
            int off = LandingMath.SplitKit(kit, before, split, out int rest);
            int k = LandingMath.KitIn(kit, before);
            conserved &= off + rest == k && off >= 0 && rest >= 0 && off <= split && rest <= before - split;
            firstOff &= off == Math.Min(split, k);
            int moved = LandingMath.MergeKit(kit, before, split);
            merged &= moved >= 0 && moved <= split && moved <= k && moved == Math.Min(split, k);
        }
        C(conserved, "a split keeps every piece of the kit, in one part or the other, and makes none");
        C(firstOff, "the kit's part goes with the part split off first");
        C(merged, "a merge moves the kit's part first, never more than was taken");

        // The colonists' first day: 450 steel hauled to a stockpile in loads of 75, merged there with 200 mined from the
        // map, then a builder takes 75 of it. What is left of the kit is 375, never the 575 lying there.
        int pile = 450, pileKit = 450, store = 0, storeKit = 0;
        while (pile > 0)
        {
            int load = Math.Min(75, pile);
            int carried = LandingMath.SplitKit(pileKit, pile, load, out pileKit);
            pile -= load;
            storeKit = LandingMath.KitIn(storeKit, store) + LandingMath.MergeKit(carried, load, load);
            store += load;
        }
        C(store == 450 && storeKit == 450 && pileKit == 0, "hauled a load at a time and merged on the stockpile, the kit is still all there");
        storeKit = LandingMath.KitIn(storeKit, store) + LandingMath.MergeKit(0, 200, 200);
        store += 200;
        C(storeKit == 450, "steel mined on the map merges in and never counts as the kit");
        int used = LandingMath.SplitKit(storeKit, store, 75, out storeKit);
        store -= 75;
        C(used == 75 && LandingMath.Takes(storeKit, store) == 375, "a builder's 75 come from the kit first: 375 comes along of the 575 there");
        C(LandingMath.Takes(30, 12) == 12, "twelve meals left of thirty: twelve come");

        // ================= The map =================

        var game = new[] { 200, 225, 250, 275, 300, 325 };
        var sizes = LandingMath.Sizes(game, new[] { 350, 400 }, false, 250);
        C(sizes.SequenceEqual(game), "the game's own sizes, smallest first");
        C(LandingMath.Sizes(game, new[] { 350, 400 }, true, 250).SequenceEqual(game.Concat(new[] { 350, 400 })), "and its larger test sizes when those are on");
        C(LandingMath.Sizes(game, null, false, 260).Contains(260), "the map's own size is offered when it is none of the game's");
        C(LandingMath.DefaultSize(sizes, 275) == 275 && LandingMath.DefaultSize(sizes, 999) == sizes[sizes.Count / 2], "the current size is chosen first");
        C(LandingMath.HeavySize == 280, "the game's warning for a heavy map, above 280");
        C(LandingMath.TurnSeed(12345, 0) == 12345, "no turn, the game's own seed");
        bool turned = true;
        foreach (int seed in new[] { 0, 1, -7, 12345, int.MaxValue, int.MinValue })
        foreach (int turn in new[] { 1, -1, 99, 123456789 })
            turned &= LandingMath.TurnSeed(seed, turn) != seed && LandingMath.TurnSeed(seed, turn) == LandingMath.TurnSeed(seed, turn);
        C(turned, "a turn gives another map of the same tile, the same each time it is asked");

        // ================= The code's seams =================

        string landing = Code("Colonies/Landing.cs"), record = Code("Colonies/LandingRecord.cs"), patches = Code("Colonies/LandingPatches.cs"), window = Code("Colonies/LandingWindow.cs");
        string comp = Code("CouncilGameComponent.cs");
        C(Regex.IsMatch(patches, @"\[HarmonyPatch\(typeof\(Scenario\), nameof\(Scenario\.GenerateIntoMap\)\)\]")
          && Regex.Matches(patches, @"if \(Find\.GameInitData == null").Count >= 4, "the start is recorded around the scenario's own parts, only while the game's start data is there");
        C(patches.Contains("[HarmonyPatch(typeof(ScenPart_ScatterThings), nameof(ScenPart_ScatterThings.GenerateIntoMap))]"), "what a scatter part put down is told apart");
        C(patches.Contains("[HarmonyPatch(typeof(Thing), nameof(Thing.SplitOff))]") && patches.Contains("[HarmonyPatch(typeof(Thing), nameof(Thing.TryAbsorbStack))]")
          && Regex.Matches(patches, @"if \(!LandingKit\.Tracking|LandingKit\.Tracking &&").Count == 2, "the kit is followed through splits and merges, only while the offer is open");
        C(record.Contains("Scribe_References.Look(ref thing, \"thing\")") && !record.Contains("ThingMaker.MakeThing") && !landing.Contains("ThingMaker.MakeThing"),
            "the kit's own things are carried, never new ones made");
        C(record.Contains("if (Scribe.mode != LoadSaveMode.Saving || landing != null) Scribe_Deep.Look(ref landing, \"landing\")")
          && record.Contains("if (Scribe.mode == LoadSaveMode.Saving) Landing.Expire(this)"), "the record is saved only while the offer is open");
        C(comp.Contains("ExposeLanding();") && comp.Contains("Safe(\"landing\", () => Landing.Expire(this));"), "and let go in the council's hour once the first days are over");
        C(!(landing + record + patches + window).Contains("Sched."), "no new slot in the schedule");
        string save = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(save.Contains("\"Colonies/LandingRecord.cs:kit\"") && save.Contains("\"Colonies/LandingRecord.cs:conditions\""), "modelled for the save's size");

        C(patches.Contains("[HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.GenerateContentsIntoMap))]") && !landing.Contains("seedString") && !patches.Contains("seedString = "),
            "a new map of the same place turns the map's seed, never the world's");
        C(Regex.IsMatch(landing, @"finally\s*\{\s*SeedTurn = 0;"), "and the turn is always undone");
        C(landing.Contains("Current.Game.DeinitAndRemoveMap(map, false)") && landing.Contains("settlement.Tile = to;") && landing.Contains("Find.GameInfo.startingTile = to;"),
            "the old map goes, the same settlement moves, and the starting tile follows (the monolith comes the game's way)");
        C(landing.Contains("MapGenerator.GenerateMap(mapSize, settlement,") && landing.Contains("Find.Anomaly.monolith = fresh") && landing.Contains("fresh.AutoActivate(old.wakeAt)"),
            "the new map is made the game's way for the same settlement, and its monolith is the game's own, waking when the scenario said");
        C(landing.Contains("DropPodUtility.DropThingGroupsNear(start, made, groups, LandingMath.PodOpenDelay, !r.ByPods"), "they arrive the scenario's way, by pods or standing");
        C(Regex.IsMatch(landing, @"Find\.GameEnder\.CheckOrUpdateGameOver\(\);\s*if \(Find\.GameEnder\.gameEnding\)") && landing.Contains("Find.GameEnder.gameEnding = false;"),
            "the game's over-check ends with the colony still standing");
        C(landing.Contains("CaravanMaker.MakeCaravan"), "if no map can be made at all, the colony goes on as a caravan, never lost between maps");
        C(!Regex.IsMatch(landing, @"\.Abandon\(|Foundings\.(Plan|Pick|Notify)|History\.Add|ColonyCalendar\.|TryGainMemory|ThoughtDef|Speech\.|Together\.|SetFaction\("),
            "nothing treats it as a founding, an abandoning or a journey; no remembrance, parting or mood");
        C(landing.Contains("CameraDriver.JumpToCurrentMapLoc(start)") && landing.Contains("Tell.Message(NewsKind.Colony"), "the camera at the new start, and a short message");
        C(landing.Contains("TileFinder.IsValidTileForNewSettlement(tile, sb)") && window.Contains("FoundingPlace.Facts(") && window.Contains("Find.WorldTargeter.BeginTargeting"),
            "another place: the game's targeter and tile check, with Rimfolk's facts of the tile");
        C(window.Contains("Dialog_AdvancedGameConfig.MapSizes") && window.Contains("Prefs.TestMapSizes"), "the game's map sizes");
        C(Regex.IsMatch(patches, @"\[HarmonyPatch\(typeof\(MainMenuDrawer\), nameof\(MainMenuDrawer\.DoMainMenuControls\)\)\]") && patches.Contains("ins.operand = ours;")
          && Regex.IsMatch(patches, @"done = true;\s*break;") && patches.Contains("Log.Warning("), "the menu's first option list goes through the entry's helper, once; if it isn't found, nothing changes and the log says so once");
        C(Code("Colonies/ColonyCards.cs").Contains("Dialog_Landing.Open()"), "the same entry on the colony's card");

        // ================= The words =================

        var mine = keyed.Where(kv => kv.Key.StartsWith("CC_Landing_")).ToList();
        C(mine.Count >= 50, "the window's and the entry's words are there");
        C(mine.All(kv => !kv.Value.Contains('—') && !kv.Value.Contains('!')), "plain words: no em dash, no exclamation mark");
        foreach (var age in new[] { "tribal", "medieval", "industrial", "spacer" })
            C(keyed.ContainsKey("CC_Landing_People_" + age) && keyed.ContainsKey("CC_Landing_Done_" + age), "the words take the colony's age: " + age);
        C(keyed["CC_Landing_Menu"] == "Choose another landing site" && keyed.ContainsKey("CC_Landing_MenuStanding"), "the entry's name, and a start on foot's");
        C(keyed["CC_Landing_ComesGear"].Contains("wear") && keyed["CC_Landing_ComesGear"].Contains("wield") && keyed["CC_Landing_ComesGear"].Contains("carry"),
            "what they wear, wield and carry comes, as the move takes it");
        C(keyed["CC_Landing_Lost"].Contains("the dead") && keyed["CC_Landing_Lost"].Contains("not of the colony") && keyed["CC_Landing_Lost"].Contains("but the starting kit"),
            "what stays with the old map, as the move leaves it");
        C(keyed["CC_Landing_No_Gravship"].Contains("gravship"), "a gravship start is told why it isn't offered");
        C(Regex.Matches(keyed["CC_Landing_Done_industrial"], @"[.?]\s").Count == 0, "the message after is one sentence");

        return n;
    }
}
