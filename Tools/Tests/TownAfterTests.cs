using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The town a colony was, after the hand-over (Colonies/TownAfterMath.cs; docs/design/stage3-many-colonies.md, section 2,
/// "What it does afterwards", "Taking it back" and "Keeping the layout"; release 2b, the player's decisions 7A and 10A):
/// the kept layout's rectangle, table, bytes and round trip, its size on a typical base and the cap with what goes first
/// past it, where it goes on a map of another size; the head's letters, its days and the invitation, homecomings and
/// moving there, the answer to the colony's letter asking for it back and who comes with it, what taking it by force
/// costs; and the code's seams with the game (the guarded base generation, the trader, the letter's purpose).
/// </summary>
public static class TownAfterTests
{
    /// <summary>Raw deflate, as the game's own (Ionic's DeflateStream, Verse.CompressUtility).</summary>
    private static byte[] Deflate(byte[] raw)
    {
        using var ms = new MemoryStream();
        using (var d = new DeflateStream(ms, CompressionLevel.Optimal)) d.Write(raw, 0, raw.Length);
        return ms.ToArray();
    }

    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "town after: " + what);
        }

        // ---------------- The kept rectangle ----------------

        LayoutMath.Rect(40, 50, 120, 110, LayoutMath.Margin, 250, 250, out int x, out int z, out int w, out int h);
        C(x == 34 && z == 44 && w == 81 + 12 && h == 61 + 12, $"the home area's bounds and a margin of {LayoutMath.Margin} on every side ({x},{z} {w}x{h})");
        LayoutMath.Rect(2, 3, 249, 240, LayoutMath.Margin, 250, 250, out x, out z, out w, out h);
        C(x == 0 && z == 0 && w == 250 && h == 247, "clipped to the map");
        LayoutMath.Rect(10, 10, 5, 5, LayoutMath.Margin, 250, 250, out _, out _, out w, out h);
        C(w == 0 && h == 0, "no home area: nothing to keep");

        // ---------------- The table and the packing ----------------

        var g = LayoutGrid.Make(10, 20, 4, 3, 250, 250);
        var index = new Dictionary<string, int>();
        int wall = LayoutMath.Add(g, LayoutMath.BuildingEntry("Wall", "BlocksGranite"), index);
        int bed = LayoutMath.Add(g, LayoutMath.BuildingEntry("Bed", "WoodLog"), index);
        int wall2 = LayoutMath.Add(g, LayoutMath.BuildingEntry("Wall", "BlocksGranite"), index);
        int floor = LayoutMath.Add(g, LayoutMath.TerrainEntry("WoodPlankFloor"), index);
        C(wall == 1 && bed == 2 && wall2 == 1 && floor == 3 && g.table.Count == 3, "each def and stuff once in the table, numbered from 1");
        C(LayoutMath.ParseBuilding(LayoutMath.Entry(g, bed), out string d, out string s) && d == "Bed" && s == "WoodLog", "a building's def and stuff back from its entry");
        C(LayoutMath.ParseBuilding(LayoutMath.BuildingEntry("PowerConduit", null), out d, out s) && d == "PowerConduit" && s == null, "a building with no stuff");
        C(LayoutMath.ParseTerrain(LayoutMath.Entry(g, floor), out d) && d == "WoodPlankFloor" && !LayoutMath.ParseTerrain(LayoutMath.Entry(g, bed), out _), "a terrain's entry is not a building's");
        C(LayoutMath.Entry(g, 0) == null && LayoutMath.Entry(g, 9) == null, "no entry for a number not in the table");
        foreach (int rot in new[] { 0, 1, 2, 3 })
        {
            ushort p = LayoutMath.Pack(bed, rot);
            C(LayoutMath.NumberOf(p) == bed && LayoutMath.RotOf(p) == rot, $"a building's number and rotation {rot} in one value");
        }
        C(LayoutMath.NumberOf(LayoutMath.Pack(LayoutMath.MaxTable, 3)) == LayoutMath.MaxTable && LayoutMath.MaxTable == (1 << 14) - 1, "14 bits for the number, 2 for the rotation");
        var full = LayoutGrid.Make(0, 0, 1, 1, 1, 1);
        for (int i = 0; i < LayoutMath.MaxTable; i++) full.table.Add("b:X" + i);
        C(LayoutMath.Add(full, "b:New", null) == 0 && full.table.Count == LayoutMath.MaxTable, "a full table takes nothing more");

        // ---------------- The bytes and the round trip ----------------

        g.things[g.Index(0, 0)] = LayoutMath.Pack(wall, 0);
        g.things[g.Index(2, 1)] = LayoutMath.Pack(bed, 2);
        g.under[g.Index(0, 0)] = LayoutMath.Pack(LayoutMath.Add(g, LayoutMath.BuildingEntry("PowerConduit", null), index), 0);
        g.floors[g.Index(2, 1)] = (ushort)floor;
        g.flags[g.Index(2, 1)] = LayoutMath.Roof | LayoutMath.Clear;
        g.beds.Add(g.Index(2, 1));
        var back = LayoutMath.Shorts(LayoutMath.Bytes(g.things), g.Cells);
        C(back.SequenceEqual(g.things) && LayoutMath.Shorts(LayoutMath.Bytes(g.under), g.Cells).SequenceEqual(g.under)
          && LayoutMath.Shorts(LayoutMath.Bytes(g.floors), g.Cells).SequenceEqual(g.floors), "every layer comes back from its bytes as it was");
        C(LayoutMath.Bytes(new ushort[] { 0x1234 }).SequenceEqual(new byte[] { 0x34, 0x12 }), "two bytes a cell, low byte first");
        C(LayoutMath.Bytes(null) == null && LayoutMath.Shorts(null, 4) == null && LayoutMath.Shorts(new byte[] { 1, 0 }, 3).SequenceEqual(new ushort[] { 1, 0, 0 }),
            "a layer dropped stays dropped; short bytes leave the rest empty");
        C(!LayoutMath.Empty(g) && LayoutMath.Empty(LayoutGrid.Make(0, 0, 5, 5, 10, 10)) && LayoutMath.Empty(null), "a layout with no building is nothing worth keeping");
        C(LayoutMath.Any(g.flags, LayoutMath.Roof) && !LayoutMath.Any(new byte[] { LayoutMath.Clear }, LayoutMath.Roof), "the flags read bit by bit");
        C(LayoutMath.SavedSize(new byte[300], Deflate) < LayoutMath.SavedSize(new byte[300], null), "an empty layer takes little once deflated, as the game saves its grids");
        var noise = new byte[300];
        new Random(5).NextBytes(noise);
        C(LayoutMath.SavedSize(noise, Deflate) == 4 * ((300 + 2) / 3), "bytes deflate can't shrink are kept as they are (the game's own rule), as base64");

        // ---------------- A typical base, and the cap ----------------

        var typical = Base(92, 92, 18, new Random(7));
        int size = LayoutMath.SavedSize(typical, Deflate);
        C(size >= 1024 && size <= 40 * 1024, $"a mid-game base (a 80 by 80 home area, 18 rooms, every floor, conduit and roof): {size / 1024f:0.0} KB in the save");
        C(LayoutMath.Fit(typical, Deflate).Count == 0, "it fits whole");
        var big = Base(260, 260, 140, new Random(11));
        int bigSize = LayoutMath.SavedSize(big, Deflate);
        C(bigSize <= LayoutMath.MaxBytes, $"a base over most of a 275 map, 140 rooms: {bigSize / 1024f:0.0} KB, still under the cap");
        var worst = LayoutGrid.Make(0, 0, 250, 250, 250, 250);
        var rnd = new Random(3);
        for (int i = 0; i < worst.Cells; i++)
        {
            worst.things[i] = (ushort)rnd.Next(1, 60000);
            worst.under[i] = (ushort)rnd.Next(1, 60000);
            worst.floors[i] = (ushort)rnd.Next(1, 60000);
            worst.flags[i] = (byte)rnd.Next(0, 4);
        }
        worst.table.Add("b:Wall");
        var dropped = LayoutMath.Fit(worst, Deflate);
        C(dropped.Count > 0 && dropped[0] == LayoutMath.DropRoofs && (dropped.Count < 2 || dropped[1] == LayoutMath.DropFloors) && (dropped.Count < 3 || dropped[2] == LayoutMath.DropUnder),
            "past the cap: roofs go first, then the floors, then the lesser buildings (" + string.Join(", ", dropped) + ")");
        C(LayoutMath.SavedSize(worst, Deflate) <= LayoutMath.MaxBytes || dropped.Last() == LayoutMath.DropAll, "and it fits, or nothing is kept and the game makes its ordinary base");
        var roofsOnly = Base(92, 92, 18, new Random(7));
        int withRoofs = LayoutMath.SavedSize(roofsOnly, Deflate);
        var roofsDropped = LayoutMath.Fit(roofsOnly, Deflate, withRoofs - 1);
        C(roofsDropped.Count >= 1 && roofsDropped[0] == LayoutMath.DropRoofs && !LayoutMath.Any(roofsOnly.flags, LayoutMath.Roof) && LayoutMath.Any(roofsOnly.flags, LayoutMath.Clear),
            "dropping the roofs keeps the rock that is gone");
        C(LayoutMath.MaxBytes == 64 * 1024, "64 KB at most");

        // ---------------- On a map of another size ----------------

        C(LayoutMath.Shift(40, 100, 250) == 40 && LayoutMath.Shift(200, 100, 250) == 150 && LayoutMath.Shift(10, 300, 250) == 0,
            "where it was when it fits; moved in just enough when not");

        // ---------------- The head's letters ----------------

        int day = FormerMath.DayTicks;
        C(!FormerMath.LetterDue(0, -1, 3 * day, 0f) && FormerMath.LetterDue(0, -1, 6 * day, 0f), "the first letter a few days after the hand-over at the soonest");
        C(!FormerMath.LetterDue(0, 10 * day, 20 * day, 0f) && FormerMath.LetterDue(0, 10 * day, 26 * day, 0f), "a season apart at the least");
        C(!FormerMath.LetterDue(0, -1, 40 * day, 0.5f) && FormerMath.LetterChance < 0.2f, "and only now and then");
        C(FormerMath.LetterTag(10f) == "struggling" && FormerMath.LetterTag(90f) == "flourishing" && FormerMath.LetterTag(-1f) == "steady", "its news by its fortune");

        // ---------------- Its days ----------------

        int year = FormerMath.YearTicks;
        int founded = 3 * day, handed = 40 * day;
        C(FormerMath.DayComing(founded, handed, handed + 10 * day, -1) == TownDay.None, "nothing in its first days");
        C(FormerMath.DayComing(founded, handed, founded + year - 2 * day, -1) == TownDay.Founding, "the colony's founding day kept: invited a few days before");
        C(FormerMath.DayComing(founded, handed, handed + year - 3 * day, -1) == TownDay.HandOver, "the day of the hand-over kept: invited a year on");
        C(FormerMath.DayComing(founded, handed, handed + year - 3 * day, handed + year - 30 * day) == TownDay.None, "one invitation a year at most");
        C(FormerMath.DayComing(founded, handed, handed + 2 * year - 3 * day, handed + year - 3 * day) == TownDay.HandOver, "and again the next year");
        C(FormerMath.DaysToAnniversary(0, year - 2 * day) == 2 && FormerMath.DaysToAnniversary(0, year + 2 * day) == -1 && FormerMath.DaysToAnniversary(0, 2 * year) == 0,
            "the days to its day, never counted down in words");

        // ---------------- Homecomings ----------------

        C(!FormerMath.WouldAsk(true, true, true, true, true), "never the head");
        C(!FormerMath.WouldAsk(false, false, true, true, true), "only one of the colony's former people");
        C(FormerMath.WouldAsk(false, true, true, false, false) && FormerMath.WouldAsk(false, true, false, true, false) && FormerMath.WouldAsk(false, true, false, false, true)
          && !FormerMath.WouldAsk(false, true, false, false, false), "a spouse or child in the colony, a close friend, or their town struggling: else no reason");
        C(FormerMath.HomecomingChance(true, false, false) > FormerMath.HomecomingChance(false, true, false) && FormerMath.HomecomingChance(false, false, false) == 0f, "kin first");
        C(!FormerMath.HomecomingDue(0, -1, 10 * day, 1f, 0f) && FormerMath.HomecomingDue(0, -1, 16 * day, 1f, 0f), "not in the town's first season");
        C(!FormerMath.HomecomingDue(0, 20 * day, 30 * day, 1f, 0f) && FormerMath.HomecomingDue(0, 20 * day, 36 * day, 1f, 0f), "once a season per town at most");
        C(FormerMath.CloseFriends(50, 45) && !FormerMath.CloseFriends(80, 10), "a close friend thinks well of them too");

        // ---------------- Moving there ----------------

        C(FormerMath.KinThere(true, false, false) && FormerMath.KinThere(false, false, true) && !FormerMath.KinThere(false, false, false), "a spouse, a child or a close friend among those who stayed");
        C(FormerMath.MoveStrength(true, false, 0) > FormerMath.MoveStrength(false, true, 0) && FormerMath.MoveStrength(false, true, 0) > FormerMath.MoveStrength(false, false, 60)
          && FormerMath.MoveStrength(false, false, 100) <= 1f, "a spouse pulls hardest");

        // ---------------- Taking it back peacefully ----------------

        float fresh = FormerMath.TakeBackChance(50, 80, 50f, 0f, 0f);
        float hard = FormerMath.TakeBackChance(50, 80, 10f, 0f, 0f);
        float old = FormerMath.TakeBackChance(50, 80, 50f, 3f, 0f);
        float grown = FormerMath.TakeBackChance(50, 80, 50f, 0f, 0.8f);
        float sour = FormerMath.TakeBackChance(-60, 0, 50f, 0f, 0f);
        C(fresh > 0.3f && fresh < 0.8f, $"a warm head soon after: maybe ({fresh:0.00})");
        C(hard > fresh, "a struggling town is readier");
        C(old < fresh && grown < fresh && sour < fresh, "the years since, how much it has grown, and a cold head all weigh against");
        C(FormerMath.TakeBackChance(100, 100, 0f, 0f, 0f) == FormerMath.TakeBackMost && FormerMath.TakeBackChance(-100, -100, 100f, 5f, 1f) == FormerMath.TakeBackLeast,
            "never sure either way");
        C(FormerMath.Grown(40f, 70f, 0) > FormerMath.Grown(40f, 45f, 0) && FormerMath.Grown(40f, 30f, 1) == 0.25f && FormerMath.Grown(-1f, 80f, 0) == 0f,
            "how much it has grown: its fortune over what it started with, and towns of its own");
        C(FormerMath.TakeBackWhy(-60, 80, 50f, 0f, 0f) == "Regard" && FormerMath.TakeBackWhy(50, 80, 90f, 0f, 0f) == "Thriving"
          && FormerMath.TakeBackWhy(50, 80, 50f, 4f, 0f) == "Years" && FormerMath.TakeBackWhy(50, 80, 50f, 0f, 0.9f) == "Grown" && FormerMath.TakeBackWhy(60, 90, 40f, 0f, 0f) == "Own",
            "a no says why: the heaviest of what weighed against it, else that they would keep what they built");
        var who = FormerMath.Joiners(new List<float> { 10f, 60f, 30f, 90f, 40f, 25f });
        C(who.SequenceEqual(new[] { 3, 1, 4 }) && FormerMath.MaxJoiners == 3, "up to three of its other people choose to come, the most willing first");
        C(FormerMath.Joiners(new List<float> { 0f, 24f }).Count == 0, "none below the line");
        C(FormerMath.JoinScore(10, true, 50f) > FormerMath.JoinScore(10, false, 50f) && FormerMath.JoinScore(10, false, 20f) > FormerMath.JoinScore(10, false, 50f),
            "kin among the colony's people and the town's hard times bring them");

        // ---------------- Taking it by force ----------------

        C(FormerMath.Became(TownEnd.None, true, false, false, false) == TownEnd.None, "a town that stands is still theirs");
        C(FormerMath.Became(TownEnd.None, false, false, true, false) == TownEnd.Beaten, "its settlement gone and the game's ruin of a beaten town in its place: beaten");
        C(FormerMath.Became(TownEnd.Beaten, false, false, true, true) == TownEnd.Settled, "then settled with the game's own button");
        C(FormerMath.Became(TownEnd.TakenBack, true, true, false, true) == TownEnd.TakenBack, "taken back by their word stays so");
        C(FormerMath.TakenWeight >= 40 && FormerMath.TakenFaithWeight < FormerMath.TakenWeight, "a heavy wrong for its people, lighter for every people of the same faith");
        C(HandOverMath.BondEnds(false, true) && HandOverMath.Bond(true, true) == 0, "the bond ends with a taking by force");
        C(FormerMath.YearsSince(0, 2 * year + 5) == 2 && FormerMath.YearsSince(-1, 5) == 0, "the years since, whole");

        Seams(C);
        return n;
    }

    /// <summary>
    /// A made-up base: rooms of stone walls with a door, a bed, a table and a lamp, wood floors and a built roof inside,
    /// conduits along the walls, a mountain corner left as rock, open ground kept clear of it.
    /// </summary>
    private static LayoutGrid Base(int width, int height, int rooms, Random r)
    {
        var g = LayoutGrid.Make(20, 20, width, height, width + 40, height + 40);
        var index = new Dictionary<string, int>();
        string[] stuffs = { "BlocksGranite", "BlocksSandstone", "WoodLog", "Steel" };
        string[] furniture = { "Bed", "Table1x2c", "DiningChair", "Dresser", "EndTable", "StandingLamp", "Shelf", "Armchair", "TorchLamp", "PlantPot" };
        string[] floorsDefs = { "WoodPlankFloor", "TileGranite", "Carpet_Red", "Concrete", "PavedTile" };
        for (int i = 0; i < g.Cells; i++)
            g.flags[i] = LayoutMath.Clear;
        // A corner of rock that was never mined.
        for (int zz = 0; zz < height / 4; zz++)
            for (int xx = 0; xx < width / 4 - zz; xx++)
                g.flags[g.Index(xx, zz)] = 0;
        for (int k = 0; k < rooms; k++)
        {
            int rw = r.Next(5, 10), rh = r.Next(5, 10);
            int rx = r.Next(1, Math.Max(2, width - rw - 1)), rz = r.Next(1, Math.Max(2, height - rh - 1));
            int wall = LayoutMath.Add(g, LayoutMath.BuildingEntry("Wall", stuffs[r.Next(stuffs.Length)]), index);
            int floorNo = LayoutMath.Add(g, LayoutMath.TerrainEntry(floorsDefs[r.Next(floorsDefs.Length)]), index);
            int conduit = LayoutMath.Add(g, LayoutMath.BuildingEntry("PowerConduit", null), index);
            for (int xx = rx; xx < rx + rw; xx++)
                for (int zz = rz; zz < rz + rh; zz++)
                {
                    int i = g.Index(xx, zz);
                    bool edge = xx == rx || zz == rz || xx == rx + rw - 1 || zz == rz + rh - 1;
                    g.flags[i] = LayoutMath.Clear | LayoutMath.Roof;
                    if (edge)
                    {
                        g.things[i] = LayoutMath.Pack(wall, 0);
                        if (zz == rz) g.under[i] = LayoutMath.Pack(conduit, 0);
                    }
                    else g.floors[i] = (ushort)floorNo;
                }
            g.things[g.Index(rx + rw / 2, rz)] = LayoutMath.Pack(LayoutMath.Add(g, LayoutMath.BuildingEntry("Door", stuffs[r.Next(stuffs.Length)]), index), 0);
            for (int f = 0; f < 4; f++)
            {
                int fx = r.Next(rx + 1, rx + rw - 1), fz = r.Next(rz + 1, rz + rh - 1);
                g.things[g.Index(fx, fz)] = LayoutMath.Pack(LayoutMath.Add(g, LayoutMath.BuildingEntry(furniture[r.Next(furniture.Length)], stuffs[r.Next(stuffs.Length)]), index), r.Next(4));
            }
            g.beds.Add(g.Index(rx + 1, rz + 1));
        }
        return g;
    }

    private static void Seams(Action<bool, string> C)
    {
        string root = Root();
        C(root != null, "the repository found");
        if (root == null) return;
        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));

        // ---------------- The game's base generation, stood down for the town's map only ----------------

        string patches = Code("Colonies/TownAfterPatches.cs");
        C(patches.Contains("[HarmonyPatch(typeof(GenStep_Scatterer), nameof(GenStep_Scatterer.Generate))]") && patches.Contains("__instance is GenStep_Settlement")
          && patches.Contains("TownLayouts.PlaceTown(map)"), "a guarded prefix on the game's settlement step (and the framework's, a step of the same kind): the layout placed instead, for that map only");
        C(patches.Contains("[HarmonyPatch(typeof(GenStep_Power), nameof(GenStep_Power.Generate))]") && patches.Contains("TownLayouts.Placed(map)"),
            "the game's power step stands down for a town made from the layout");
        C(patches.Contains("[HarmonyPatch(typeof(PawnGroupKindWorker_Trader), \"GenerateTrader\")]") && patches.Contains("HandOverPeoples.IsOurs(parms.faction)")
          && patches.Contains("TownsAfter.TraderFor("), "a trader caravan of theirs: its Trader, a face the colony knows, is the one who trades");
        C(patches.Contains("RimfolkPatches.On"), "the deeper game hooks' setting holds for all three");
        string layout = Code("Colonies/TownLayout.cs");
        C(layout.Contains("new Sketch()") && layout.Contains(".Spawn(map, IntVec3.Zero, faction"), "the layout placed the way the game places its own structures (Sketch)");
        C(layout.Contains("DefDatabase<ThingDef>.GetNamedSilentFail(def)") && layout.Contains("DefDatabase<TerrainDef>.GetNamedSilentFail(tdef)"), "a def the game no longer has is left out");
        C(layout.Contains("DataExposeUtility.LookByteArray(ref things, \"things\")") && layout.Contains("LayoutMath.Fit(g, CompressUtility.Compress)"),
            "saved as the game saves its own grids (bytes, deflated), fitted under the cap with the game's own deflate");
        C(layout.Contains("new LordJob_DefendBase(f, rect.CenterCell, 25000)") && layout.Contains("PawnGroupKindDefOf.Settlement") && layout.Contains("HomeCell(map, layout, rect, p)")
          && layout.Contains("AtHome(map, rec.key, rec.layout, rect.Value, s.Faction)"),
            "its people: the game's settlement group under its defend-the-town lord, the notables at home in their own beds");
        C(layout.Contains("b.Faction == Faction.OfPlayer") && layout.Contains("d.building.isNaturalRock") && layout.Contains("b is Frame"),
            "only the colony's own buildings kept: not the land's rock, not a frame");
        C(layout.Contains("roofCollapseBuffer.CellsMarkedToCollapse.RemoveAll(") && layout.Contains("RoofCollapseUtility.WithinRangeOfRoofHolder(c, map)"),
            "the rock cleared doesn't bring down roofs the colony's own walls hold (nobody crushed at home)");
        C(!layout.Contains("listerThings.AllThings"), "items, plants and filth are never kept");
        string ho = Code("Colonies/HandOvers.cs");
        int capture = ho.IndexOf("TownLayouts.Capture(home, free)", StringComparison.Ordinal), made = ho.IndexOf("people = HandOverPeoples.Make(", StringComparison.Ordinal);
        C(capture > 0 && made > capture, "the layout kept in the hand-over's long event, before the place is its people's and the map closes");
        string formers = Code("World/Ledger/LedgerFormers.cs");
        C(formers.Contains("Scribe_Deep.Look(ref layout, \"layout\")") && formers.Contains("!Find.FactionManager.AllFactionsListForReading.Contains(people)) people = null"),
            "the layout on the town's record; a people the game let go leaves no reference behind");

        // ---------------- Taking it back ----------------

        string back = Code("Colonies/TownTakeBack.cs");
        C(back.Contains("FormerMath.TakeBackChance(") && back.Contains("FormerMath.TakeBackWhy("), "the head's answer by the rule, and a no says why");
        C(back.Contains("FoundingMath.LimitFor(Prefs.MaxNumberOfPlayerSettlements, Foundings.Counted())") && back.Contains("Prefs.MaxNumberOfPlayerSettlements = limit"),
            "the game's colony limit applies, raised by one at most (release 1's 5B)");
        C(back.Contains("s.SetFaction(Faction.OfPlayer)") && back.Contains("GetOrGenerateMapUtility.GetOrGenerateMap(s.Tile, size, null)") && back.Contains("TownLayouts.Place(map, rec.layout, Faction.OfPlayer, true)"),
            "taken back: the settlement the colony's, its map made the game's way for a home, the layout placed");
        C(back.Contains("Arrivals.Join(p, map, cell)") && back.Contains("formers.Concat(joiners)") && back.Contains("FormerMath.Joiners("),
            "its former colonists walk out of their rooms as colonists, and up to three of its other people who choose to");
        C(back.Contains("people.temporary = true") && back.Contains("people.leader = null"), "the people with no town left made temporary so the game lets it go, no stranger made to lead it");
        C(back.Contains("FoundingMath.KindTakenBack") && back.Contains("WorldLedger.ReopenColony(rec.key)"), "the milestone colony:takenBack, the colony's record standing again");
        C(back.Contains("FactionMemory.Note(people, FormerMath.DeedTaken") && back.Contains("FormerMath.DeedTakenFaith") && back.Contains("Tie.Kind.Grudge"),
            "taken by force: a heavy wrong for its people and every people of the same faith, in the memory and the ledger's grudges");
        C(back.Contains("is DestroyedSettlement") && back.Contains("TownEnd.Settled"), "beaten when the game's destroyed settlement stands in its place; settled again with the game's own button");
        string memory = Code("World/FactionMemory.cs");
        C(memory.Contains("[\"townTaken\"] = K(DeedSide.Wrong, 45)") && memory.Contains("[\"townTakenFaith\"] = K(DeedSide.Wrong, 20)"), "the deeds, as heavy as the rule says");
        // After the others; the dealings with a town's council (Dealings/, the diplomacy screen's step B) came after it.
        C(Code("World/NotableMath.cs").Contains("        TakeBack,\n") && Code("World/NotableMath.cs").IndexOf("        TakeBack,\n") < Code("World/NotableMath.cs").IndexOf("        Dealing\n    }")
          && Code("Diplomacy/DiplomacyLetters.cs").Contains("TownLetters.Owns(p)"),
            "asking for it back: a letter purpose of the neighbour letters' own, after the others");
        // To the head, or asked of someone else of the town to pass it on to the head (World/LetterRoutes.cs).
        C(Code("World/NeighboursPanel.cs").Contains("TownLetters.Owns(p) && !TownLetters.Shows(who ?? to)"), "offered only in a letter to the head of a town the colony was");

        // ---------------- Its life, and no schedule slot ----------------

        string region = Code("World/Region/RegionBook.cs");
        C(region.Contains("TownsAfter.Hourly(now)") && region.Contains("TownsAfter.Daily(now)") && region.Contains("WorldLedger.Formers.Count > 0"),
            "no schedule slot: the region's hour and day, only while a town the colony was is on record");
        string life = Code("Colonies/TownsAfter.cs");
        C(life.Contains("FormerMath.LetterDue(") && life.Contains("Rimfolk_Pool_TownLetter") && life.Contains("Facts.For(FactScene.Event(") && life.Contains("Couriers.MailAnswer(home, rec.people"),
            "the head's letters, by the rule and the facts, walked to the gate by their messenger");
        C(life.Contains("FormerMath.HomecomingDue(") && life.Contains("FoundingLetters.AskHomecoming(") && life.Contains("EdgeToward(home, rec.Town)") && life.Contains("Together.When("),
            "a homecoming asked by Tell.Ask, walked in from the town's side, welcomed face to face");
        C(life.Contains("TownDayMission.Invite(rec, day, home)") && life.Contains("FormerMath.DayComing("), "its days: an invitation a year at most, by the invitation missions");
        C(Code("Colonies/FoundingLetters.cs").Contains("Tell.Ask(l, TownsAfter.HomeFor(kin))") && Code("Colonies/FoundingLetters.cs").Contains("Tell.Ask(l, Homes.Seat)"), "its questions through Tell.Ask");
        string settings = Code("CouncilSettings.cs");
        C(settings.Contains("public bool townLife = true;") && settings.Contains("Scribe_Values.Look(ref townLife, \"townLife\", true);")
          && Code("UI/SettingsCatalog.cs").Contains("Toggle(\"townLife\""), "one setting, The town's life, in Many colonies");
        C(Code("Social/MovingOnTown.cs").Contains("FormerMath.KinThere(spouse, child, friend)") && Code("Social/MovingOnTown.cs").Contains("MovingOn.Spare(map)"),
            "moving there: a moving-on question of its own, never one of the last four grown-ups");

        // ---------------- The facts ----------------

        C(new[] { "town:former", "them:former" }.All(FactsMath.RegionTags.Contains) && new[] { "OLDCOLONY", "YEARSSINCE" }.All(FactsMath.RegionValues.Contains),
            "the facts' tags and placeholders for a town the colony was");
        C(Code("Text/Facts/FactsRegion.cs").Contains("FormerTags(s, f);"), "the region's provider reads them");
        var talk = XDocument.Load(Path.Combine(root, "Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_TownAfter.xml")).Root!.Elements("ColonyCouncil.OutsiderTalkDef").ToList();
        C(talk.Count >= 6, $"talk that remembers the colony's days ({talk.Count} pieces)");
        C(talk.All(t => Regex.IsMatch(t.Element("when")?.Value ?? "", @"\b(town:former|them:former)")), "each said only of a town the colony was");
        foreach (var tag in new[] { "town:former", "them:former" })
            C(talk.Any(t => (t.Element("when")?.Value ?? "").Split(',').Contains(tag)), "a piece for " + tag);

        // ---------------- The words ----------------

        var keyedDoc = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_TownAfter.xml")).Root!;
        var keyed = keyedDoc.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        C(keyed.Values.All(v => !v.Contains("—") && !v.Contains("!")), "no em dashes and no exclamation marks");
        C(keyed.Values.All(v => !Regex.IsMatch(v, @"\{DAYS\}|\bin \d+ (days?|hours?)\b|\bdays? (left|more)\b")), "no countdowns");
        foreach (var why in new[] { "Regard", "Goodwill", "Thriving", "Years", "Grown", "Own" })
            C(keyed.ContainsKey("CC_TownAfter_No_" + why), "a no's reason in words: " + why);
        foreach (var age in new[] { "Tribal", "Medieval", "Industrial", "Spacer" })
            C(keyed.ContainsKey("CC_TownAfter_LetterIntro_" + age), "the head's letter comes by the " + age.ToLowerInvariant() + " age's own means");
        var ages = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Ages.xml")).Root!.Elements().Select(e => e.Name.LocalName).ToHashSet();
        C(new[] { "Tribal", "Medieval", "Industrial", "Spacer" }.All(a => ages.Contains("CC_Form_Back_" + a) && ages.Contains("CC_Form_Back_" + a + "_Rite")), "coming back takes every age's form");
        var allKeyed = string.Concat(Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        C(allKeyed.Contains("<CC_Memory_Deed_townTaken>") && allKeyed.Contains("<CC_Memory_Deed_townTakenFaith>") && allKeyed.Contains("<CC_Letter_PurposeCap_TakeBack>"),
            "the deeds' and the letter purpose's words");
        var pools = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_TownAfter.xml")).Root!.Elements().ToList();
        List<string> Lines(string name) => pools.FirstOrDefault(p => p.Element("defName")?.Value == name)?.Descendants("li").Select(li => li.Value).ToList() ?? new List<string>();
        var home = Lines("Rimfolk_Pool_TownHomecoming");
        C(home.Any(l => l.StartsWith("[welcome]")) && home.Any(l => l.StartsWith("[back]")), "the welcome: one welcomes, one is back");
        foreach (var line in home)
        {
            string body = line.Substring(line.IndexOf(']') + 1).Trim();
            C(OneSentence(body) && body.Length <= 90 && !body.Contains("!"), $"a homecoming's bubble is one short sentence: '{body}'");
        }
        var letter = Lines("Rimfolk_Pool_TownLetter");
        C(new[] { "former", "new", "asks", "struggling", "poor", "steady", "thriving", "flourishing", "tribal", "medieval", "industrial", "spacer" }
              .All(t => letter.Any(l => l.StartsWith("[") && l.Substring(1, l.IndexOf(']') - 1).Split(',').Contains(t))),
            "the head's letters: the colony's days by its age, a head of the town's own, its fortune, someone asking after their own");
        C(letter.Where(l => l.Contains("{ASKER}")).All(l => l.StartsWith("[asks")), "{ASKER} only when someone asks");
        var answers = Lines("Rimfolk_Lines_NeighbourWrites_TakeBack");
        C(new[] { "warm", "cordial", "cool", "refusal" }.All(t => answers.Any(l => l.StartsWith("[takeback," + t))), "their own words for each answer");
        var mission = XDocument.Load(Path.Combine(root, "Defs", "MissionDefs", "Rimfolk_Missions_TownDay.xml")).Root!.Elements("IncidentDef").Single();
        string missionText = mission.ToString();
        C(mission.Element("baseChance")?.Value == "0" && mission.Element("workerClass")?.Value == "ColonyCouncil.HappeningWorker_MissionTownDay",
            "the town's day never drawn by the storyteller: only the town sends it");
        C(missionText.Contains("[founding]") && missionText.Contains("[handover]") && !Regex.IsMatch(missionText, @"(ends|is) in \{DAYS\}"), "both days in words, no countdown");
        var dilemma = XDocument.Load(Path.Combine(root, "Defs", "DilemmaDefs", "Rimfolk_Dilemmas_TownKin.xml")).Root!.ToString();
        C(dilemma.Contains("ColonyCouncil.DilemmaWorker_TownKin") && dilemma.Contains("DilemmaEffect_Procession") && dilemma.Contains("<joinFaction>true</joinFaction>")
          && dilemma.Contains("ColonyCouncil.DilemmaEffect_JoinTown"), "going: seen to the gate, one of the town's people from then on");
        foreach (var tie in new[] { "[spouse]", "[child]", "[friend]" }) C(dilemma.Contains(tie), "the question for a " + tie.Trim('[', ']'));
        var almanac = XDocument.Load(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_TownAfter.xml")).Root!.Elements().First();
        C(almanac.Element("label")?.Value == "the town afterwards" && !almanac.ToString().Contains("—"), "the Almanac page, a page of its own");
        string saves = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(saves.Contains("Colonies/TownLayout.cs:table") && saves.Contains("Colonies/TownLayout.cs:sleepers"), "the kept layout has its model in the save estimate");
    }

    private static bool OneSentence(string s)
    {
        for (int i = 12; i < s.Length - 1; i++)
            if (s[i] == '.' || s[i] == '!' || s[i] == '?')
                return false;
        return true;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
