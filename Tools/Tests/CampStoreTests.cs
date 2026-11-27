using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// A camp's things kept out of the weather (21 November 2026, the player: "this camp that's currently set up has all their
/// stuff just out in the open instead of on a shelf or somewhere where it doesn't deteriorate"; Camps/CampBuildMath,
/// CampDayMath, CampPackMath, CampStore.cs): which store a camp puts up by its stay and its people's age, the store tent
/// that is a room the game roofs (walls all round, one door that leads in and out), where it goes in every kind's layout,
/// what they brought to build with going back in their packs, and the store taken home when they go.
/// </summary>
public static class CampStoreTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "camp store: " + what);
        }

        // ---------------- Which store ----------------

        C(CampBuildMath.StoreKind(1f, true, true) == null, "a night's stop puts up no store: what they don't use stays packed");
        C(CampBuildMath.StoreKind(CampBuildMath.StoreFromDays, true, true) == "shelf", "from a day and a half, a shelf where their people know one that keeps");
        C(CampBuildMath.StoreKind(2f, true, true) == "shelf", "a stay of two days has its shelf (until now none under three days)");
        C(CampBuildMath.StoreKind(7f, false, true) == "tent", "a tribe, who know no such shelf, puts up a store tent (the grove-keepers' week in the burn)");
        C(CampBuildMath.StoreKind(7f, false, false) == null, "with neither, nothing they can't build");
        C(CampBuildMath.StoreTentInside(3) == (2, 2) && CampBuildMath.StoreTentInside(1) == (2, 2), "a store tent for up to three: two by two inside");
        C(CampBuildMath.StoreTentInside(4) == (3, 2) && CampBuildMath.StoreTentInside(12) == (3, 2), "for four or more: three by two");

        // ---------------- The store tent is a room ----------------

        foreach (var (w, d) in new[] { (2, 2), (3, 2), (1, 1), (4, 3) })
        {
            var tent = CampBuildMath.StoreTent(w, d);
            var walls = tent.SelectMany(p => p.Cells()).ToList();
            C(tent.Count(p => p.role == "storedoor") == 1, $"a store tent {w} by {d}: one door");
            C(tent.All(p => p.role == "storewall" || p.role == "storedoor"), $"a store tent {w} by {d}: walls and a door only");
            C(walls.Count == walls.Distinct().Count(), $"a store tent {w} by {d}: no two pieces on a cell");
            var inside = CampBuildMath.StoreInside(walls, (1, 1));
            C(inside.Count == w * d, $"a store tent {w} by {d}: its roof covers its whole floor ({inside.Count})");
            C(CampBuildMath.StoreDoorWorks(tent), $"a store tent {w} by {d}: its door leads in and out");
            for (int k = 1; k < 4; k++)
            {
                var turned = CampBuildMath.Turned(tent, k, 3, -2);
                var from = CampBuildMath.Turn(1, 1, k);
                C(CampBuildMath.StoreInside(turned.SelectMany(p => p.Cells()), (from.x + 3, from.z - 2)).Count == w * d && CampBuildMath.StoreDoorWorks(turned),
                    $"a store tent {w} by {d} turned {k}: still a room with its door");
            }
            var leaky = CampBuildMath.StoreTent(w, d);
            leaky.RemoveAll(p => p.role == "storewall" && p.minX == 0 && p.minZ == 1);
            C(CampBuildMath.StoreInside(leaky.SelectMany(p => p.Cells()), (1, 1)).Count == 0, $"a store tent {w} by {d} with a wall down: no roof");
        }
        C(CampBuildMath.StoreInside(CampBuildMath.StoreTent(2, 2).SelectMany(p => p.Cells()), (0, 0)).Count == 0, "no floor from a wall's own cell");

        // ---------------- Where it goes ----------------

        int Open(int x, int z) => 0;
        int Wooded(int x, int z) => CampBuildMath.Cost(CellFate.Fell);
        int Rocky(int x, int z) => (x * 7 + z * 13) % 5 == 0 ? CampBuildMath.Cost(CellFate.Mine) : CampBuildMath.Cost(CellFate.Fell);
        int HalfBlocked(int x, int z) => x <= -5 ? -1 : 0;
        foreach (var form in new[] { CampSleepForm.Ring, CampSleepForm.Hut, CampSleepForm.Tents })
            for (int s = 1; s <= 12; s++)
            {
                var spec = new CampLayoutSpec
                {
                    sleepers = s, sleep = form, tables = CampBuildMath.TablesFor(s, 5f, true), butcher = true, craft = true, picket = true, cover = true,
                    stall = s >= 3, spot = s >= 4, lights = CampBuildMath.LightsFor(s, true), windbreak = form == CampSleepForm.Ring
                };
                string what = $"a tribal camp ({form}) of {s}";
                foreach (var (cost, ground) in new (Func<int, int, int>, string)[] { (Open, "open ground"), (Wooded, "a map full of trees"), (Rocky, "trees and rock") })
                {
                    var l = CampBuildMath.Plan(spec, cost);
                    C(l.Complete && l.Livable, $"{what} on {ground}: everything in it, its store tent too ({string.Join(", ", l.missing)})");
                    var tent = l.pieces.Where(p => p.role == "storewall" || p.role == "storedoor").ToList();
                    var (iw, id) = CampBuildMath.StoreTentInside(s);
                    var inside = CampBuildMath.StoreInside(tent.SelectMany(p => p.Cells()), l.stash);
                    C(inside.Count == iw * id, $"{what} on {ground}: their goods are set down inside the store tent ({inside.Count} cells from {l.stash})");
                    C(CampBuildMath.StoreDoorWorks(tent), $"{what} on {ground}: the store tent's door leads in and out");
                    C(!l.pieces.Any(p => p.role == "stash"), $"{what} on {ground}: no shelf they couldn't make");
                    C(Overlaps(l.pieces) == null, $"{what} on {ground}: nothing overlaps ({Overlaps(l.pieces)})");
                    C(!l.pieces.Any(p => inside.Any(c => p.Holds(c.x, c.z))), $"{what} on {ground}: nothing stands on the store's floor");
                    C(tent.All(p => Math.Abs(p.minX) <= CampBuildMath.Reach && Math.Abs(p.maxZ) <= CampBuildMath.Reach && Math.Abs(p.maxX) <= CampBuildMath.Reach && Math.Abs(p.minZ) <= CampBuildMath.Reach),
                        $"{what} on {ground}: the store tent within the camp's reach");
                    // Its door's step outside is free, so they can walk in.
                    var door = tent.First(p => p.role == "storedoor");
                    var solid = new HashSet<(int, int)>(l.pieces.SelectMany(p => p.Cells()));
                    C(new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }.Any(dd => !solid.Contains((door.minX + dd.Item1, door.minZ + dd.Item2)) && !inside.Contains((door.minX + dd.Item1, door.minZ + dd.Item2))),
                        $"{what} on {ground}: the step outside the store's door is free");
                    C(!l.pieces.Any(p => p.role != "fire" && p.minX <= 1 && p.maxX >= -1 && p.minZ <= 1 && p.maxZ >= -1), $"{what} on {ground}: a way round the fire");
                }
                var cut = CampBuildMath.Plan(spec, HalfBlocked);
                C(cut.Livable && cut.pieces.All(p => p.minX > -5), $"{what} beside the colony's land: a camp with nothing on the colony's ground ({string.Join(", ", cut.missing)})");
            }
        // A camp with a shelf has no store tent, and its shelf as before.
        var shelfCamp = CampBuildMath.Plan(new CampLayoutSpec { sleepers = 4, sleep = CampSleepForm.Ring, stash = true, cover = true, tables = 1, lights = 1 });
        C(shelfCamp.Count("stash") == 1 && shelfCamp.Count("storewall") == 0, "a shelf, where their people have one: no store tent beside it");
        // No room for the store tent anywhere: the stash's ground as before, and it's missed.
        var hemmed = CampBuildMath.Plan(new CampLayoutSpec { sleepers = 2, sleep = CampSleepForm.Ring, cover = true }, (x, z) => Math.Abs(x) <= 3 && Math.Abs(z) <= 3 ? 0 : -1);
        C(hemmed.Livable && hemmed.missing.Contains("store") && hemmed.Count("storewall") == 0, "no room for a store tent: still a camp, the store missed");

        // Every kind, as a tribe would pitch it, has room for its store tent at its biggest.
        var kinds = Directory.GetFiles(Path.Combine(root, "Defs", "CampDefs"), "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.CampKindDef")).ToList();
        int tents = 0;
        foreach (var k in kinds)
        {
            string dn = k.Element("defName")?.Value ?? "?";
            var people = (k.Element("people")?.Value ?? "3~6").Split('~').Select(int.Parse).ToArray();
            var days = (k.Element("days")?.Value ?? "4~10").Split('~').Select(float.Parse).ToArray();
            var form = Enum.TryParse<CampSleepForm>(k.Element("sleep")?.Value, out var f) ? f : CampSleepForm.Ring;
            string store = CampBuildMath.StoreKind(days.Max(), false, true);
            if (store != "tent") continue;
            tents++;
            int s = people[people.Length - 1];
            var spec = new CampLayoutSpec
            {
                sleepers = s, sleep = form, tables = CampBuildMath.TablesFor(s, days.Max(), true), craft = CampBuildMath.CraftFor(days.Max()), cover = true,
                picket = k.Element("animals") != null, lights = CampBuildMath.LightsFor(s, true), windbreak = form == CampSleepForm.Ring
            };
            var l = CampBuildMath.Plan(spec, (x, z) => CampBuildMath.Cost(CellFate.Fell));
            C(l.Complete, $"{dn} of {s}, a tribe's: its store tent fits among trees ({string.Join(", ", l.missing)})");
        }
        C(tents >= 40, $"most kinds stay long enough for a store ({tents})");

        // ---------------- What they brought, back in their packs ----------------

        var ready = new RepackFacts { brought = true, theirs = true, atCamp = true, settled = true };
        C(CampDayMath.Repacks(ready), "leather they brought for their bedrolls and didn't use goes back in a pack once the camp stands");
        foreach (var (why, f) in new (string, Func<RepackFacts, RepackFacts>)[]
                 {
                     ("not brought (their hunt's hides, what they gathered or made)", x => { x.brought = false; return x; }),
                     ("not theirs", x => { x.theirs = false; return x; }),
                     ("away from their camp", x => { x.atCamp = false; return x; }),
                     ("while the camp is still going up, or they pack to go", x => { x.settled = false; return x; }),
                     ("while something is built or added", x => { x.underWay = true; return x; }),
                     ("while a piece still needs it", x => { x.needed = true; return x; }),
                     ("put out on purpose", x => { x.putOut = true; return x; }),
                     ("in someone's hands", x => { x.busy = true; return x; }),
                     ("taken by the colony", x => { x.inColony = true; return x; }),
                     ("in their store already: the group's goods stay there", x => { x.inStore = true; return x; }),
                     ("a store of theirs takes it: carried there, not into a pack", x => { x.storeTakes = true; return x; })
                 })
            C(!CampDayMath.Repacks(f(ready)), "not repacked: " + why);

        // ---------------- What they take when they go ----------------

        float food = CampDayMath.TakeFirst(true, false, 2f), medicine = CampDayMath.TakeFirst(false, true, 30f), silver = CampDayMath.TakeFirst(false, false, 100f),
            logs = CampDayMath.TakeFirst(false, false, 0.2f), stone = CampDayMath.TakeFirst(false, false, 0.05f);
        C(food > silver && medicine > silver && silver > logs && logs > stone, "food and medicine first, then what is worth most for its weight, the bulky and cheap last");
        C(CampDayMath.TakeFirst(false, false, 1e9f) < food, "however precious, never before their food");
        string life = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Camps", "CampLife.cs"));
        C(life.Contains("TakeGoods(carriers.Concat(") && life.Contains("MassUtility.FreeSpace") && life.Contains("t.SetForbidden(false, false);")
          && !life.Contains("carriers.RandomElement().inventory.innerContainer.TryAdd(t)"),
            "shared out among the packs and pack animals by what each can carry; what nobody can carry left for anyone");
        C(File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Camps.xml")).Contains("<CC_CampLeft_Uncarried>"), "and the leaving message says what was left");

        // ---------------- Taken home ----------------

        foreach (CampLeaves leaves in Enum.GetValues(typeof(CampLeaves)))
        {
            C(CampPackMath.Fate(leaves, false, true, true, true, true) == PackFate.TakeDown, $"the store tent's walls are taken down whatever the kind leaves ({leaves})");
            C(CampPackMath.Fate(leaves, true, true, true, true, true) == PackFate.Pack, $"a store tent's door that can be uninstalled is packed ({leaves})");
            C(CampPackMath.Fate(leaves, false, false, true, true, true) == PackFate.Leave, $"in a hurry the store tent is left ({leaves})");
            C(CampPackMath.Fate(leaves, false, true, false, true, true) == PackFate.Leave, $"with no hands the store tent is left ({leaves})");
        }
        C(CampPackMath.Fate(CampLeaves.Standing, false, true, true) == PackFate.Leave, "a wall of a kind that leaves its camp standing still stands (only the store is taken)");
        return n;
    }

    private static string Overlaps(List<CampPiece> pieces)
    {
        var seen = new Dictionary<(int, int), string>();
        foreach (var p in pieces)
            foreach (var c in p.Cells())
            {
                if (seen.TryGetValue(c, out var other)) return $"{p.role} on {other} at ({c.Item1},{c.Item2})";
                seen[c] = p.role;
            }
        return null;
    }
}
