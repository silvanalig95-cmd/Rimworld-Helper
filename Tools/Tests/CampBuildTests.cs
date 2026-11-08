using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Camps that build something proper (Camps/CampBuildMath, CampTakeMath, CampDayMath, CampPackMath; the kits in
/// Defs/CampDefs/Rimfolk_CampKits.xml, the kinds in Defs/CampDefs): what is cleared before building and what is never
/// touched, a bed for every sleeper, huts and tents that are rooms (walls all round, one door), a seat for everyone at the
/// tables, every kind's full layout on open ground and on a map full of trees; the kits' pieces from the active rows of the
/// player's catalogue by role and age; what they may take; a camper's day; what's packed and what stays; what a stay leaves.
/// </summary>
public static class CampBuildTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "camp build: " + what);
        }

        // ---------------- What a cell holds, and what becomes of it ----------------

        C(CampBuildMath.Fate(new CampCell()) == CellFate.Free, "open ground is free");
        C(CampBuildMath.Fate(new CampCell { tree = true }) == CellFate.Fell, "a wild tree is felled, not a reason to give up");
        C(CampBuildMath.Fate(new CampCell { plant = true }) == CellFate.Cut, "a bush is cut");
        C(CampBuildMath.Fate(new CampCell { chunk = true }) == CellFate.Haul, "a chunk is carried aside");
        C(CampBuildMath.Fate(new CampCell { rock = true }) == CellFate.Mine, "plain rock is mined");
        C(CampBuildMath.Fate(new CampCell { rock = true, tree = true, chunk = true }) == CellFate.Mine, "rock is the most work of all");
        foreach (var (cell, what) in new[]
                 {
                     (new CampCell { colony = true, tree = true }, "the colony's (its home area, a zone, a plan, its things)"),
                     (new CampCell { ore = true }, "an ore"),
                     (new CampCell { badGround = true }, "water or soft ground"),
                     (new CampCell { building = true }, "someone else's building"),
                     (new CampCell { sown = true }, "a sown or marked plant"),
                     (new CampCell { valuable = true }, "something of value"),
                     (new CampCell { roofed = true }, "under the mountain"),
                     (new CampCell { outside = true }, "off the map or fogged")
                 })
            C(CampBuildMath.Fate(cell) == CellFate.Never, "never touched: " + what);
        C(CampBuildMath.Cost(CellFate.Free) == 0 && CampBuildMath.Cost(CellFate.Cut) < CampBuildMath.Cost(CellFate.Fell) && CampBuildMath.Cost(CellFate.Fell) < CampBuildMath.Cost(CellFate.Mine)
          && CampBuildMath.Cost(CellFate.Never) < 0, "the less to clear the better; never, never");

        // ---------------- Rooms ----------------

        for (int s = 1; s <= 12; s++)
        {
            var room = CampBuildMath.Room(s, false, 1, 2, 1);
            C(room.Count(p => p.role == "hutbed") == s, $"a hut for {s} has {s} beds");
            C(CampBuildMath.Enclosed(room, 1, out var why), $"a hut for {s} is a room ({why})");
            var (w, d, two) = CampBuildMath.RoomSize(s, false);
            C(w >= 3 && w <= CampBuildMath.RowMax && (two == s > 6), $"a hut for {s}: {w} by {d}, {(two ? "two rows" : "one row")}");
            C(Overlaps(room) == null, $"nothing in the hut for {s} overlaps ({Overlaps(room)})");
            C(CampBuildMath.Inside(room, 1).Count == w * d, $"the roof covers the hut for {s}'s inside");
        }
        for (int s = 1; s <= 12; s++)
        {
            int tents = (s + 1) / 2;
            var row = CampBuildMath.TentRow(s, tents, 1, 2, 1);
            C(row.Count(p => p.role == "hutbed") == s, $"tents for {s}: a bed each");
            bool all = Enumerable.Range(1, tents).All(r => CampBuildMath.Enclosed(row, r, out _));
            C(all, $"each of {tents} tents for {s} is a room with one door");
            C(Overlaps(row) == null, $"tents for {s} share walls without overlapping ({Overlaps(row)})");
        }
        var leaky = CampBuildMath.Room(4, false, 1, 2, 1);
        leaky.RemoveAll(p => p.role == "hut" && p.minX == 0 && p.minZ == 2);
        C(!CampBuildMath.Enclosed(leaky, 1, out _), "a hut with a wall missing is no room");
        var twoDoors = CampBuildMath.Room(4, false, 1, 2, 1);
        twoDoors.First(p => p.role == "hut" && p.minZ == 0).role = "door";
        C(!CampBuildMath.Enclosed(twoDoors, 1, out _), "a hut has one door");

        // ---------------- The whole camp ----------------

        int Open(int x, int z) => 0;
        int Wooded(int x, int z) => CampBuildMath.Cost(CellFate.Fell);
        int Rocky(int x, int z) => (x * 7 + z * 13) % 5 == 0 ? CampBuildMath.Cost(CellFate.Mine) : CampBuildMath.Cost(CellFate.Fell);
        // The colony's zone or water across one side of the site: the camp keeps off it.
        int HalfBlocked(int x, int z) => x <= -5 ? -1 : 0;
        foreach (var form in new[] { CampSleepForm.Ring, CampSleepForm.Hut, CampSleepForm.Tents })
            foreach (bool tentBeds in form == CampSleepForm.Tents ? new[] { false, true } : new[] { false })
                for (int s = 1; s <= 12; s++)
                {
                    var spec = new CampLayoutSpec
                    {
                        sleepers = s, sleep = form, tentBeds = tentBeds, tables = CampBuildMath.TablesFor(s, 5f, true), butcher = true, craft = true, picket = true, stash = true,
                        stall = s >= 3, spot = s >= 4, lights = CampBuildMath.LightsFor(s, true), windbreak = form == CampSleepForm.Ring
                    };
                    string what = $"{form}{(tentBeds ? " (tents that are beds)" : "")} for {s}";
                    foreach (var (cost, ground) in new (Func<int, int, int>, string)[] { (Open, "open ground"), (Wooded, "a map full of trees"), (Rocky, "trees and rock") })
                    {
                        var l = CampBuildMath.Plan(spec, cost);
                        C(l.Complete && l.Livable, $"{what} on {ground}: everything in it ({string.Join(", ", l.missing)})");
                        int beds = l.Count("bed") + l.Count("hutbed");
                        C(beds == s, $"{what} on {ground}: a bed for every sleeper ({beds})");
                        C(Overlaps(l.pieces) == null, $"{what} on {ground}: nothing overlaps ({Overlaps(l.pieces)})");
                        C(l.pieces.All(p => Math.Abs(p.minX) <= CampBuildMath.Reach && Math.Abs(p.maxX) <= CampBuildMath.Reach && Math.Abs(p.minZ) <= CampBuildMath.Reach && Math.Abs(p.maxZ) <= CampBuildMath.Reach),
                            $"{what} on {ground}: within the camp's reach");
                        int seats = l.Count("seat");
                        C(spec.tables == 0 || seats >= Math.Min(s, spec.tables * 4), $"{what} on {ground}: a seat for everyone at the tables ({seats})");
                        foreach (int room in l.pieces.Where(p => p.room > 0).Select(p => p.room).Distinct())
                            C(CampBuildMath.Enclosed(l.pieces, room, out var why), $"{what} on {ground}: room {room} is enclosed ({why})");
                        if (form == CampSleepForm.Hut) C(l.pieces.Select(p => p.room).Where(r => r > 0).Distinct().Count() == 1, $"{what}: one hut for all of them");
                        if (form == CampSleepForm.Ring || tentBeds) C(l.Count("hut") == 0, $"{what}: no walls round the beds");
                        // The fire's ring is kept free to sit and cook.
                        C(!l.pieces.Any(p => p.role != "fire" && p.minX <= 1 && p.maxX >= -1 && p.minZ <= 1 && p.maxZ >= -1), $"{what} on {ground}: a way round the fire");
                    }
                    var cut = CampBuildMath.Plan(spec, HalfBlocked);
                    C(cut.Livable, $"{what} beside the colony's land: still a camp ({string.Join(", ", cut.missing)})");
                    C(cut.pieces.All(p => p.minX > -5), $"{what} beside the colony's land: nothing on it");
                }
        // Rock and trees cost more than open ground: the cheapest site wins.
        var specHut = new CampLayoutSpec { sleepers = 6, sleep = CampSleepForm.Hut, tables = 2, lights = 2, butcher = true, craft = true };
        C(CampBuildMath.Plan(specHut, Open).cost == 0 && CampBuildMath.Plan(specHut, Wooded).cost > 0 && CampBuildMath.Plan(specHut, Rocky).cost > CampBuildMath.Plan(specHut, Wooded).cost,
            "a wooded site costs felling, a rocky one more");
        // The player's camp: a war party of three gets three bedrolls round its fire, not one.
        var war = CampBuildMath.Plan(new CampLayoutSpec { sleepers = 3, sleep = CampSleepForm.Ring, windbreak = true, tables = CampBuildMath.TablesFor(3, 1.2f, true), lights = 1, butcher = true });
        C(war.Count("bed") == 3 && war.Count("wall") > 0 && war.Count("table") == 0, "the war party of three: three beds round the fire, a windbreak behind them, no table for a night's stop");
        C(CampBuildMath.TablesFor(3, 1f, true) == 0 && CampBuildMath.TablesFor(5, 4f, true) == 2 && CampBuildMath.TablesFor(12, 6f, true) == 3 && CampBuildMath.TablesFor(8, 6f, false) == 0,
            "tables: none for a night, one for every four, three at most, none for a kind that doesn't dine");
        C(CampBuildMath.LightsFor(3, true) == 1 && CampBuildMath.LightsFor(6, true) == 2 && CampBuildMath.LightsFor(11, true) == 3 && CampBuildMath.LightsFor(5, false) == 0, "lights by the people");
        // Turned layouts stay the same shape.
        var turned = CampBuildMath.Turned(CampBuildMath.Room(3, false, 1, 2, 1), 1, 0, 0);
        C(CampBuildMath.Enclosed(turned, 1, out _), "a hut turned a quarter is still a room");
        C(CampBuildMath.Turn(0, 1, 1) == (1, 0) && CampBuildMath.Turn(0, 1, 2) == (0, -1) && CampBuildMath.Turn(0, 1, 3) == (-1, 0), "a quarter turn sends +z to +x");

        // ---------------- Ages ----------------

        C(CampBuildMath.AgeOf(1) == "tribal" && CampBuildMath.AgeOf(2) == "tribal" && CampBuildMath.AgeOf(3) == "medieval" && CampBuildMath.AgeOf(4) == "industrial"
          && CampBuildMath.AgeOf(5) == "spacer" && CampBuildMath.AgeOf(7) == "spacer", "the people's age by their tech level");
        C(CampBuildMath.Within(0, 2) && CampBuildMath.Within(2, 2) && !CampBuildMath.Within(3, 2) && CampBuildMath.Within(3, 4) && CampBuildMath.Within(2, 0), "research within the people's age");

        // ---------------- The kits: the player's active mods, by role and age ----------------

        n += Kits(root, C);

        // ---------------- Every kind ----------------

        n += Kinds(root, C);

        // ---------------- What they may take ----------------

        TakeFacts F(TakeWhat w) => new TakeFacts { what = w, stoneValue = 1f };
        C(CampTakeMath.May(F(TakeWhat.Tree)), "a wild tree outside the colony is theirs to fell");
        C(CampTakeMath.May(F(TakeWhat.Animal)), "a wild beast is theirs to hunt");
        C(CampTakeMath.May(F(TakeWhat.Plant)), "a wild plant is theirs to gather");
        var tree = F(TakeWhat.Tree);
        tree.sown = true;
        C(CampTakeMath.Why(tree) == "sown", "never a sown tree");
        TakeFacts With(TakeWhat w, Func<TakeFacts, TakeFacts> f) => f(F(w));
        C(CampTakeMath.Why(With(TakeWhat.Animal, f => { f.colony = true; return f; })) == "colony", "never the colony's animals");
        C(CampTakeMath.Why(With(TakeWhat.Animal, f => { f.others = true; return f; })) == "others", "never another people's animals");
        C(CampTakeMath.Why(With(TakeWhat.Building, f => { f.colony = true; return f; })) == "colony", "never the colony's buildings, machinery or furniture");
        C(CampTakeMath.Why(With(TakeWhat.Building, f => f)) == "building", "never anyone's building");
        C(CampTakeMath.Why(With(TakeWhat.Item, f => { f.stored = true; f.stoneChunk = true; return f; })) == "stored", "never anything in a stockpile");
        C(CampTakeMath.Why(With(TakeWhat.Item, f => { f.forbidden = true; f.stoneChunk = true; return f; })) == "forbidden", "never what the colony forbade");
        C(CampTakeMath.Why(With(TakeWhat.Plant, f => { f.zoned = true; return f; })) == "zone", "never a crop in a growing zone");
        C(CampTakeMath.Why(With(TakeWhat.Tree, f => { f.home = true; return f; })) == "home", "never in the home area");
        C(CampTakeMath.May(With(TakeWhat.Item, f => { f.stoneChunk = true; return f; })), "a stone chunk lying about is theirs to carry");
        C(CampTakeMath.Why(With(TakeWhat.Item, f => { f.value = 6f; return f; })) == "valuable", "never anything worth more than plain stone");
        C(CampTakeMath.May(With(TakeWhat.Item, f => { f.theirs = true; f.value = 50f; return f; })), "their own goods, whatever they're worth");
        C(CampTakeMath.Why(With(TakeWhat.Item, f => { f.theirs = true; f.stored = true; return f; })) == "stored", "theirs carried into the colony's stockpile is the colony's now");
        // Mineables, by what they yield: plain stone yes; steel, silver, gold, plasteel, uranium, jade, components, a mod's ore never.
        C(CampTakeMath.PlainRock(false, true, 0f, 1f), "granite, limestone, marble: plain rock");
        C(CampTakeMath.PlainRock(false, true, 0f, 1f) && CampTakeMath.PlainRock(false, false, 0f, 1f), "collapsed rock that yields nothing: plain");
        foreach (var (name, value) in new[] { ("steel", 1.9f), ("silver", 1f), ("gold", 10f), ("plasteel", 9f), ("uranium", 6f), ("jade", 5f), ("components", 32f), ("a mod's ore", 3f) })
            C(!CampTakeMath.PlainRock(true, false, value, 1f), "never a resource lump: " + name);
        C(!CampTakeMath.PlainRock(false, false, 3f, 1f), "never a rock that yields something worth more than stone (a mod's ore not marked a resource)");
        var rockF = With(TakeWhat.Mineable, f => { f.stoneChunk = true; return f; });
        C(CampTakeMath.May(rockF), "plain rock clear of the colony is theirs to mine");
        rockF.resourceRock = true;
        C(CampTakeMath.Why(rockF) == "ore", "never an ore");

        // ---------------- A camper's day ----------------

        var parts = Enumerable.Range(0, 24).Select(CampDayMath.HourOf).ToList();
        C(Enumerable.Range(0, 6).All(h => parts[h] == CampHour.Night) && parts[22] == CampHour.Night && parts[23] == CampHour.Night, "night from ten to six, in their beds");
        C(parts[6] == CampHour.Morning && parts[12] == CampHour.Noon && parts[17] == CampHour.Supper && parts[18] == CampHour.Supper, "a meal in the morning, at noon and at supper");
        C(Enumerable.Range(7, 5).All(h => parts[h] == CampHour.Work) && Enumerable.Range(13, 4).All(h => parts[h] == CampHour.Work), "work from seven to noon and one to five");
        C(Enumerable.Range(19, 3).All(h => parts[h] == CampHour.Evening), "the evening by the fire from seven");
        C(parts.Count(p => p == CampHour.Work) == 9 && parts.Count(p => p == CampHour.Night) == 8, "nine hours' work, eight of sleep");
        C(CampDayMath.MealBelow(CampHour.Noon) > CampDayMath.MealBelow(CampHour.Work) && CampDayMath.MealBelow(CampHour.Work) == CampDayMath.Starving, "they eat at mealtimes, and at other hours only when starving");
        C(CampDayMath.HourOf(-1) == CampHour.Night && CampDayMath.HourOf(30) == CampHour.Morning, "hours wrap round the day");
        int Skill(CampLabour w) => w == CampLabour.Mine ? -1 : w == CampLabour.Craft ? 12 : 3;
        var order = CampDayMath.Order(new[] { CampLabour.Hunt, CampLabour.Mine }, Skill);
        C(order[0] == CampLabour.Hunt, "their kind's calling first");
        C(!order.Contains(CampLabour.Mine), "never a work they can't do");
        C(order.IndexOf(CampLabour.Craft) < order.IndexOf(CampLabour.Chop), "then what they're best at");
        C(order.Last() == CampLabour.Mend && order.Contains(CampLabour.Haul), "hauling and mending at the end");
        C(CampDayMath.PerDay(CampLabour.Chop, false, 0.3f, 3) == 0.3f && CampDayMath.PerDay(CampLabour.Chop, true, 1f, 3) == 1.5f, "a kind's own rate, half again for its calling");
        C(CampDayMath.PerDay(CampLabour.Hunt, false, 0f, 4) == 0f, "no hunting for a kind that doesn't hunt");
        C(CampDayMath.PerDay(CampLabour.Mine, false, 0f, 5) == 0f && CampDayMath.PerDay(CampLabour.Mine, true, 0f, 3) == 5f, "mining only for a kind that comes to mine");
        C(CampDayMath.PerDay(CampLabour.Chop, false, 0f, 4) > 0.5f && CampDayMath.PerDay(CampLabour.Chop, false, 0f, 4) < 2f, "every camp cuts a tree or so a day for its fire");
        C(CampDayMath.PerDay(CampLabour.Gather, false, 0f, 0) == 0f, "no hands, no work");
        C(CampDayMath.Roll(1.4f, 0.3) == 2 && CampDayMath.Roll(1.4f, 0.5) == 1 && CampDayMath.Roll(0f, 0.0) == 0, "a rate's fraction is a chance");
        C(CampDayMath.StayCap(CampLabour.Chop, 50, 5f) == 10 && CampDayMath.StayCap(CampLabour.Hunt, 6, 5f) == 2 && CampDayMath.StayCap(CampLabour.Chop, 0, 5f) == 0, "never the land stripped in a stay");
        C(CampDayMath.TalkChance(1) == 0f && CampDayMath.TalkChance(3) > 0f && CampDayMath.TalkChance(20) <= 0.6f, "talk by the fire with company");

        // ---------------- Packing, and what stays ----------------

        foreach (var e in new[] { "time", "asked", "moved", "declined", "hunger", "crowded", "unpaid", "brawlTheirs", "weather", "grief" })
            C(CampPackMath.Orderly(e) && CampPackMath.PackHours(e) > 0, $"{e}: they pack in their own time");
        C(CampPackMath.Orderly("hamlet") && CampPackMath.PackHours("hamlet") == 0, "settlers off with the colony's caravan go at once, packing as they go");
        foreach (var e in new[] { "forced", "raid", "harmed", "arrest", "fight", "fallout", "brawl", "lost", "empty", null })
            C(!CampPackMath.Orderly(e) && CampPackMath.PackHours(e) == 0, $"{e ?? "none"}: they go with what they carry");
        C(CampPackMath.PackHours("time") > CampPackMath.PackHours("weather"), "a few hours at the end of a stay, less in bad weather");
        C(CampPackMath.Fate(CampLeaves.Standing, true, true, true) == PackFate.Pack, "bedrolls, tents and stools are packed and carried");
        C(CampPackMath.Fate(CampLeaves.Standing, true, false, true) == PackFate.Leave, "driven off, they leave the furniture they can't carry");
        C(CampPackMath.Fate(CampLeaves.Standing, true, true, false) == PackFate.Leave, "nobody to carry it: it stays");
        C(CampPackMath.Fate(CampLeaves.Standing, false, true, true) == PackFate.Leave, "walls, doors, huts and the fire pit stay standing, nobody's");
        C(CampPackMath.Fate(CampLeaves.NoTrace, false, true, true) == PackFate.TakeDown, "a people that leaves no trace takes its constructions down");
        C(CampPackMath.Fate(CampLeaves.NoTrace, false, false, true) == PackFate.Leave, "...but not when driven off");
        C(CampPackMath.Fate(CampLeaves.Employer, false, true, true) == PackFate.Hand && CampPackMath.Fate(CampLeaves.Employer, true, true, true) == PackFate.Pack,
            "a camp hired by contract hands its post to the colony, and packs its bedding");
        C(CampPackMath.Fate(CampLeaves.Employer, false, true, true, false) == PackFate.Leave, "...but not to a colony that didn't pay it");

        // ---------------- What a stay leaves ----------------

        var good = new StayFacts { orderly = true, days = 5f, people = 4, tension = 10f, warmth = 6, butchered = 3, felled = 6, mined = 4, woodLeft = 40, hidesLeft = 20, stoneLeft = 8 };
        var left = CampPackMath.Leaves(good, _ => 0.0);
        C(left.Contains(Leftover.Woodpile) && left.Contains(Leftover.Hides) && left.Contains(Leftover.Cairn) && left.Contains(Leftover.Gift), "a warm stay that cut, hunted and mined leaves a woodpile, hides, a cairn, a gift");
        C(!left.Any(CampPackMath.CanHurt) && !left.Contains(Leftover.Refuse), "and nothing bad");
        C(CampPackMath.Leaves(good, _ => 0.99).Count == 0, "each only by its chance");
        var nothing = new StayFacts { orderly = true, days = 2f, people = 3, tension = 10f };
        C(CampPackMath.Leaves(nothing, _ => 0.0).Count == 0, "a stay that took nothing and was nothing to the colony leaves nothing");
        var sour = new StayFacts { orderly = false, days = 7f, people = 6, tension = 70f, butchered = 4, hotDry = true, woodLeft = 40 };
        var bad = CampPackMath.Leaves(sour, _ => 0.0);
        C(bad.Contains(Leftover.Refuse) && bad.Contains(Leftover.Offal) && bad.Contains(Leftover.Embers), "a sour stay ended in a hurry, hot and dry, after butchering: refuse, offal, embers");
        C(!bad.Contains(Leftover.Woodpile) && !bad.Contains(Leftover.Gift), "and no good turns");
        var calm = sour;
        calm.hotDry = false;
        calm.butchered = 0;
        C(!CampPackMath.Leaves(calm, _ => 0.0).Any(CampPackMath.CanHurt), "no embers without the dry heat, no offal without butchering");
        var only = good;
        only.calling = l => l == Leftover.Gift;
        C(CampPackMath.Leaves(only, _ => 0.0).SequenceEqual(new[] { Leftover.Gift }), "only what the kind's calling allows");
        C(CampPackMath.CanHurt(Leftover.Offal) && CampPackMath.CanHurt(Leftover.Embers) && !CampPackMath.CanHurt(Leftover.Refuse) && !CampPackMath.CanHurt(Leftover.LameBeast),
            "only the predator and the fire can hurt anyone (the shared budget)");
        C(HarmKinds.Of("campLeft") == HarmKinds.CampLeft && HarmKinds.CampLeft.weight > 0f && HarmKinds.CampLeft.weight < 0.01f, "what a camp left is a kind of the shared budget, a rare one");
        return n;
    }

    /// <summary>The first two pieces that overlap, or null.</summary>
    private static string Overlaps(List<CampPiece> pieces)
    {
        var seen = new Dictionary<(int, int), CampPiece>();
        foreach (var p in pieces)
            foreach (var c in p.Cells())
            {
                if (seen.TryGetValue(c, out var o)) return o + " / " + p;
                seen[c] = p;
            }
        return null;
    }

    private static readonly Dictionary<string, string[]> RoleRows = new Dictionary<string, string[]>
    {
        ["fire"] = new[] { "fire" },
        ["bed"] = new[] { "bedroll", "sleepingspot", "bed" },
        ["tent"] = new[] { "tent", "bedroll" },
        ["hut"] = new[] { "wall" },
        ["tentwall"] = new[] { "wall" },
        ["wall"] = new[] { "wall", "fence" },
        ["door"] = new[] { "door" },
        ["tentdoor"] = new[] { "door" },
        ["table"] = new[] { "table" },
        ["seat"] = new[] { "seat" },
        ["light"] = new[] { "torch", "brazier" },
        ["butcher"] = new[] { "workspot", "bench" },
        ["craft"] = new[] { "workspot", "bench" },
        ["stall"] = new[] { "table" },
        ["picket"] = new[] { "picket" },
        ["stash"] = new[] { "storage" },
    };

    private static readonly string[] AgeTech = { "tribal", "medieval", "industrial", "spacer" };

    private static int Kits(string root, Action<bool, string> C)
    {
        int n = 0;
        void Check(bool ok, string what)
        {
            n++;
            C(ok, what);
        }
        var table = Path.Combine(root, "docs", "player-mods", "buildings.tsv");
        var file = Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKits.xml");
        Check(File.Exists(table) && File.Exists(file), "the catalogue's buildings and the camp kits are there");
        if (!File.Exists(table) || !File.Exists(file)) return n;
        var rows = new Dictionary<string, Dictionary<string, string>>();
        var lines = File.ReadAllLines(table);
        var head = lines[0].Split('\t');
        foreach (var l in lines.Skip(1))
        {
            var cells = l.Split('\t');
            var row = new Dictionary<string, string>();
            for (int i = 0; i < head.Length && i < cells.Length; i++) row[head[i]] = cells[i];
            rows[row["defName"]] = row;
        }
        Check(rows.Count > 300, $"the buildings table has the player's buildings ({rows.Count})");
        var techs = new[] { "Undefined", "Animal", "Neolithic", "Medieval", "Industrial", "Spacer", "Ultra", "Archotech" };
        var kits = XDocument.Load(file).Root!.Elements("ColonyCouncil.CampKitDef").ToList();
        Check(kits.Select(k => k.Element("age")?.Value).OrderBy(a => a).SequenceEqual(AgeTech.OrderBy(a => a)), "a kit for each age: tribal, medieval, industrial, spacer");
        foreach (var kit in kits)
        {
            string age = kit.Element("age")?.Value ?? "?";
            int peopleTech = Array.IndexOf(AgeTech, age) + 2;
            var parts = kit.Element("parts")?.Elements("li").ToList() ?? new List<XElement>();
            foreach (var role in RoleRows.Keys)
            {
                var part = parts.FirstOrDefault(p => p.Element("role")?.Value == role);
                Check(part != null, $"{age}: the kit has a {role}");
                if (part == null) continue;
                var defs = part.Element("defs")?.Elements("li").ToList() ?? new List<XElement>();
                Check(defs.Count > 0, $"{age} {role}: something to build");
                if (defs.Count == 0) continue;
                var last = defs.Last();
                Check(last.Attribute("MayRequire") == null && rows.TryGetValue(last.Value, out var lr) && lr["mod"].StartsWith("ludeon."), $"{age} {role}: ends with the game's own ({last.Value}), which is always there");
                foreach (var d in defs)
                {
                    string name = d.Value;
                    Check(rows.TryGetValue(name, out var r), $"{age} {role}: {name} is in the player's catalogue");
                    if (r == null) continue;
                    Check(r["active"] == "yes", $"{age} {role}: {name} is from an active mod ({r["mod"]})");
                    var may = d.Attribute("MayRequire")?.Value;
                    Check(r["mod"].StartsWith("ludeon.") ? may == null : may != null && may.ToLowerInvariant().Split(',').Contains(r["mod"]),
                        $"{age} {role}: {name} is soft, under its own mod's package ({may ?? "none"} for {r["mod"]})");
                    var roles = r["role"].Split(',');
                    Check(RoleRows[role].Any(roles.Contains), $"{age} {role}: {name} is a {string.Join(" or ", RoleRows[role])} ({r["role"]})");
                    if (role == "butcher") Check(r["work"].Contains("butcher"), $"{age}: {name} is where beasts are butchered");
                    if (role == "craft") Check(r["work"].Contains("craft"), $"{age}: {name} is where things are crafted");
                    if (role == "light") Check(r["power"] != "yes", $"{age}: {name} needs no power (a camp has none)");
                    // A mod's piece only within the people's age (the game's own last is weighed in the game, as any other).
                    int rt = Array.IndexOf(techs, r["researchTech"]);
                    if (d != last) Check(rt <= Math.Max(2, peopleTech), $"{age} {role}: {name}'s research ({r["researchTech"]}) is within the age");
                }
            }
        }
        return n;
    }

    private static readonly string[] Labours = Enum.GetNames(typeof(CampLabour));

    private static int Kinds(string root, Action<bool, string> C)
    {
        int n = 0;
        void Check(bool ok, string what)
        {
            n++;
            C(ok, what);
        }
        var kinds = Directory.GetFiles(Path.Combine(root, "Defs", "CampDefs"), "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.CampKindDef")).ToList();
        Check(kinds.Count >= 41, $"every kind of camp ({kinds.Count})");
        var forms = new Dictionary<string, int>();
        foreach (var k in kinds)
        {
            string dn = k.Element("defName")?.Value ?? "?";
            string sleep = k.Element("sleep")?.Value;
            Check(sleep != null && Enum.TryParse<CampSleepForm>(sleep, out _), $"{dn}: says how it sleeps ({sleep})");
            forms[sleep ?? "?"] = forms.GetValueOrDefault(sleep ?? "?") + 1;
            var works = k.Element("works")?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
            Check(works.Count > 0 && works.All(Labours.Contains), $"{dn}: its callings are work the camp knows ({string.Join(", ", works)})");
            string leaves = k.Element("leaves")?.Value ?? "Standing";
            Check(Enum.TryParse<CampLeaves>(leaves, out _), $"{dn}: what it does with its constructions ({leaves})");
            if (leaves != "Standing") Check(!string.IsNullOrEmpty(k.Element("leavesWhy")?.Value), $"{dn}: says why it doesn't leave them standing");
            var structures = k.Element("structures")?.Elements("li").ToList() ?? new List<XElement>();
            Check(structures.Any(s => s.Element("role")?.Value == "fire"), $"{dn}: a fire");
            Check(!structures.Any(s => new[] { "hut", "wall", "tentwall", "door" }.Contains(s.Element("role")?.Value)), $"{dn}: its shelter and windbreak come from its people's kit, not a stray list");
            // Its beds too, but for the austere kinds' sleeping places on the ground (no count: a bed for each sleeper).
            Check(structures.Where(s => s.Element("role")?.Value == "bed").All(s => s.Element("defs")?.Elements("li").All(x => x.Value == "SleepingSpot") == true && s.Element("count") == null),
                $"{dn}: its beds are its people's, a bed each (or the austere sleeping place)");
            Check(!k.ToString().Contains("DankPyon_") && !k.ToString().Contains("Outland_TentDoor"), $"{dn}: nothing of a mod the player hasn't enabled");
            // Its full layout, at its biggest, for its stay, on open ground and among trees.
            var people = (k.Element("people")?.Value ?? "3~6").Split('~').Select(int.Parse).ToArray();
            var days = (k.Element("days")?.Value ?? "4~10").Split('~').Select(float.Parse).ToArray();
            var form = Enum.TryParse<CampSleepForm>(sleep, out var f) ? f : CampSleepForm.Ring;
            foreach (int s in new[] { people[0], people[people.Length - 1] }.Distinct())
                foreach (bool tentBeds in form == CampSleepForm.Tents ? new[] { true, false } : new[] { false })
                {
                    bool hunts = works.Contains("Hunt") || float.TryParse(k.Element("huntsPerDay")?.Value, out var hpd) && hpd > 0f;
                    var spec = new CampLayoutSpec
                    {
                        sleepers = s, sleep = form, tentBeds = tentBeds, tables = CampBuildMath.TablesFor(s, days.Max(), true), butcher = hunts,
                        craft = CampBuildMath.CraftFor(days.Max()) || works.Contains("Craft"), stall = structures.Any(x => x.Element("role")?.Value == "stall"),
                        spot = structures.Any(x => x.Element("role")?.Value == "spot"), picket = k.Element("animals") != null, stash = days.Max() >= 3f, lights = CampBuildMath.LightsFor(s, true),
                        windbreak = form == CampSleepForm.Ring
                    };
                    foreach (var (ground, cost) in new (string, Func<int, int, int>)[] { ("open ground", (x, z) => 0), ("a map full of trees", (x, z) => CampBuildMath.Cost(CellFate.Fell)) })
                    {
                        var l = CampBuildMath.Plan(spec, cost);
                        Check(l.Complete && l.Count("bed") + l.Count("hutbed") == s, $"{dn} of {s}{(tentBeds ? " in a mod's tents" : "")} on {ground}: its full layout, a bed each ({string.Join(", ", l.missing)})");
                    }
                }
        }
        Check(forms.GetValueOrDefault("Ring") >= 5 && forms.GetValueOrDefault("Hut") >= 5 && forms.GetValueOrDefault("Tents") >= 5, $"some sleep round the fire, some in huts, some in tents ({string.Join(", ", forms.Select(kv => kv.Key + " " + kv.Value))})");
        var warParty = kinds.FirstOrDefault(k => k.Element("defName")?.Value == "Council_Camp_WarParty");
        Check(warParty?.Element("sleep")?.Value == "Ring", "a war party sleeps round its fire");
        var settlers = kinds.FirstOrDefault(k => k.Element("defName")?.Value == "Rimfolk_Camp_Settlers");
        Check(settlers?.Element("sleep")?.Value == "Hut", "settlers put up a hut");
        var market = kinds.FirstOrDefault(k => k.Element("defName")?.Value == "Council_Camp_Market");
        Check(market?.Element("sleep")?.Value == "Tents", "traders pitch tents");
        return n;
    }
}
