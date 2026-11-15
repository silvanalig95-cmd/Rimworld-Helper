using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Camps that keep their goods in their own storage, clean their ground and grow over their stay (Camps/CampDayMath's
/// stowing and cleaning, CampTakeMath's own store, CampPackMath's refuse, CampBuildMath's growth; the kits' grows in
/// Defs/CampDefs/Rimfolk_CampKits.xml, the kinds' tidiness in Defs/CampDefs): the player's save of 15 November 2026 had a
/// surveyors' camp whose goods lay round its shelf and never went onto it.
/// </summary>
public static class CampGrowTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "camp grow: " + what);
        }

        // ---------------- Their goods into their own storage (the player's report) ----------------

        var loose = new StowFacts { theirs = true, atCamp = true, storeTakes = true };
        C(CampDayMath.Stows(loose), "their goods lying beside the stash go onto their shelf (until now anything within three and a half cells of it counted as put away)");
        StowFacts With(Func<StowFacts, StowFacts> f) => f(loose);
        C(!CampDayMath.Stows(With(f => { f.theirs = false; return f; })), "never the colony's things, nor anyone's but theirs");
        C(!CampDayMath.Stows(With(f => { f.inStore = true; return f; })), "what is on their shelf stays there");
        C(!CampDayMath.Stows(With(f => { f.corpse = true; return f; })), "a body goes to the butcher's spot, not the shelf");
        C(!CampDayMath.Stows(With(f => { f.putOut = true; return f; })), "bales by the beasts and a stall's wares keep their places");
        C(!CampDayMath.Stows(With(f => { f.inColony = true; return f; })), "nothing out of the colony's storage or home area");
        C(!CampDayMath.Stows(With(f => { f.busy = true; return f; })), "not what another of them has in hand");
        C(!CampDayMath.Stows(With(f => { f.storeTakes = false; return f; })), "no store with room that takes it: it waits by the stash");
        C(!CampDayMath.Stows(With(f => { f.atCamp = false; return f; })), "only what lies at their camp");
        C(CampDayMath.StowCount(75, 30, 75) == 30 && CampDayMath.StowCount(10, 99999, 75) == 10 && CampDayMath.StowCount(200, 500, 75) == 75 && CampDayMath.StowCount(5, 0, 0) == 1,
            "a trip carries what the shelf has room for and the carrier can lift");
        var own = new TakeFacts { what = TakeWhat.Item, theirs = true, stored = true, ownStore = true, stoneValue = 1f, value = 40f };
        C(CampTakeMath.May(own), "their goods on their own shelf are still theirs to use and carry");
        var colonyShelf = own;
        colonyShelf.ownStore = false;
        C(CampTakeMath.Why(colonyShelf) == "stored", "...but in the colony's storage, never");
        var strange = own;
        strange.theirs = false;
        strange.value = 0.5f;
        C(CampTakeMath.Why(strange) == "stored", "and what isn't theirs on any shelf is never theirs to take");

        // ---------------- Clean ground round their camp ----------------

        foreach (var name in new[] { "Filth_Blood", "Filth_BloodInsect", "Filth_Vomit", "Filth_Ash", "Filth_Trash", "Filth_Dirt", "Filth_RubbleRock", "Filth_AnimalFilth", "Filth_CorpseBile", "Filth_Fuel" })
            C(CampDayMath.Mess(name), $"{name} is a mess they clean");
        foreach (var name in new[] { "RG_Filth_LeavesTreeBirch", "Filth_Sand", "Filth_Snow", "Filth_Water", "Filth_Mud", "", null })
            C(!CampDayMath.Mess(name), $"{name ?? "nothing"} is the land's, left as it lies");
        var mess = new CleanFacts { mess = true, onTheirGround = true };
        C(CampDayMath.Cleans(mess), "blood on their own ground is cleaned");
        C(!CampDayMath.Cleans(new CleanFacts { mess = true, onTheirGround = true, colonyGround = true }), "never on the colony's ground");
        C(!CampDayMath.Cleans(new CleanFacts { mess = true, onTheirGround = true, colonyRoom = true }), "never in a room of the colony's");
        C(!CampDayMath.Cleans(new CleanFacts { mess = true, onTheirGround = false }), "only on their own ground");
        C(!CampDayMath.Cleans(new CleanFacts { mess = false, onTheirGround = true }), "not the land's own leaves and sand");
        C(!CampDayMath.Cleans(new CleanFacts { mess = true, onTheirGround = true, reserved = true }), "not what someone else is cleaning");
        C(CampDayMath.CleanChance(CampTidy.Orderly) > CampDayMath.CleanChance(CampTidy.Usual) && CampDayMath.CleanChance(CampTidy.Usual) > CampDayMath.CleanChance(CampTidy.Careless)
          && CampDayMath.CleanChance(CampTidy.Careless) > 0f, "the orderly clean most often, the careless seldom, but all now and then");
        C(CampDayMath.CleanFrom(CampTidy.Orderly) < CampDayMath.CleanFrom(CampTidy.Usual) && CampDayMath.CleanFrom(CampTidy.Usual) < CampDayMath.CleanFrom(CampTidy.Careless),
            "the orderly clean a single spot of blood, the careless wait for a mess");
        C(CampDayMath.TidiesBeforeLeaving(CampTidy.Orderly, true) && !CampDayMath.TidiesBeforeLeaving(CampTidy.Orderly, false) && !CampDayMath.TidiesBeforeLeaving(CampTidy.Usual, true),
            "the orderly tidy their site before they go in their own time, never in flight");
        C(CampDayMath.Cleaners(CampTidy.Usual, 8) == 1 && CampDayMath.Cleaners(CampTidy.Orderly, 6) == 2, "one at the cleaning, two for a big orderly camp");
        // Refuse left behind: a sour stay, or a careless kind in a hurry; no longer a long crowded one.
        var longCrowded = new StayFacts { orderly = true, days = 8f, people = 8, tension = 20f };
        C(!CampPackMath.Leaves(longCrowded, _ => 0.0).Contains(Leftover.Refuse), "a long, crowded stay that went well leaves no refuse: they cleaned as they stayed");
        var hurried = new StayFacts { orderly = false, days = 3f, people = 4, tension = 20f };
        C(!CampPackMath.Leaves(hurried, _ => 0.0).Contains(Leftover.Refuse), "an ordinary camp going in a hurry leaves no refuse");
        hurried.careless = true;
        C(CampPackMath.Leaves(hurried, _ => 0.0).Contains(Leftover.Refuse), "a careless kind going in a hurry does");
        var carelessOrderly = hurried;
        carelessOrderly.orderly = true;
        C(!CampPackMath.Leaves(carelessOrderly, _ => 0.0).Contains(Leftover.Refuse), "...but not going in its own time");
        C(CampPackMath.Leaves(new StayFacts { orderly = true, days = 3f, people = 3, tension = 70f }, _ => 0.0).Contains(Leftover.Refuse), "a stay gone sour leaves it, as a sign of it");
        n += Kinds(root, C);

        // ---------------- How much a camp grows ----------------

        C(CampBuildMath.GrowCeiling(3, 2f) == 0 && CampBuildMath.GrowCeiling(6, 1.5f) == 0, "a short stay stays as it was put up");
        C(CampBuildMath.GrowCeiling(3, 2.5f) == 1, "two and a half days: one thing added");
        C(CampBuildMath.GrowCeiling(3, 7f) == 3 && CampBuildMath.GrowCeiling(6, 10f) == 5 && CampBuildMath.GrowCeiling(12, 14f) == CampBuildMath.GrowMost, "a long stay grows to a settled look");
        C(CampBuildMath.GrowCeiling(1, 15f) == 2, "a hermit alone adds a little, however long");
        bool rises = true;
        for (int p = 1; p <= 12; p++)
            for (float d = 1f; d <= 18f; d += 0.5f)
            {
                int c = CampBuildMath.GrowCeiling(p, d);
                if (c > CampBuildMath.GrowMost || c < CampBuildMath.GrowCeiling(p, d - 0.5f) || (p > 1 && c < CampBuildMath.GrowCeiling(p - 1, d))) rises = false;
            }
        C(rises, "more for a longer stay and more people, never past the most");
        C(Enumerable.Range(0, 11).All(i => CampBuildMath.GrowGapHours(0, i / 10.0) >= 18 && CampBuildMath.GrowGapHours(0, i / 10.0) <= 28)
          && Enumerable.Range(0, 11).All(i => CampBuildMath.GrowGapHours(3, i / 10.0) >= 19 && CampBuildMath.GrowGapHours(3, i / 10.0) <= 34), "a day or so after it stands, then a day or so apart");

        // ---------------- What a kind adds ----------------

        bool All(string r) => true;
        var hunters = new GrowFacts { people = 4, days = 7f, age = "medieval", calling = "Hunt", hunts = true, dines = true, stash = true, has = All };
        var hw = CampBuildMath.GrowWants(hunters);
        C(hw.FirstOrDefault() == "drying", $"hunters first put up a rack to dry their meat ({string.Join(", ", hw)})");
        C(hw.Contains("fireseat") && hw.Contains("post") && hw.Contains("latrine") && hw.Contains("banner"), "and what any camp of the age would: seats, a lean-to, a latrine, a banner");
        var drovers = new GrowFacts { people = 4, days = 5f, age = "tribal", calling = "Chop", beasts = true, dines = true, has = All };
        var dw = CampBuildMath.GrowWants(drovers);
        C(dw.FirstOrDefault() == "fence", $"drovers first pen their beasts ({string.Join(", ", dw)})");
        C(!dw.Contains("banner"), "a tribal camp puts up no banner");
        C(!dw.Contains("post"), "no lean-to without a shelf to cover");
        C(CampBuildMath.GrowWants(new GrowFacts { people = 4, days = 6f, age = "medieval", calling = "Mine", stash = true, dines = true, has = All }).First() == "stonework", "those who come to cut rock put up a stonecutter's table");
        C(CampBuildMath.GrowWants(new GrowFacts { people = 4, days = 6f, age = "medieval", calling = "Chop", stash = true, dines = true, has = All }).First() == "woodpile", "woodcutters a firewood rack");
        C(CampBuildMath.GrowWants(new GrowFacts { people = 3, days = 6f, age = "medieval", calling = "Craft", workTable = true, ownSpot = true, dines = true, has = All }).All(r => r != "tailoring"),
            "the smith has a forge already");
        C(CampBuildMath.GrowWants(new GrowFacts { people = 3, days = 6f, age = "medieval", faith = true, ownSpot = false, dines = true, has = All }).Contains("shrine"), "a people of a faith puts up a small altar");
        C(!CampBuildMath.GrowWants(new GrowFacts { people = 3, days = 6f, age = "medieval", faith = true, ownSpot = true, dines = true, has = All }).Contains("shrine"), "...unless their kind has a spot of its own");
        C(CampBuildMath.GrowWants(new GrowFacts { people = 3, days = 6f, age = "tribal", empire = true, has = All }).Contains("banner"), "the Empire's people put up their banner");
        C(!CampBuildMath.GrowWants(new GrowFacts { people = 3, days = 3f, age = "medieval", dines = true, stash = true, has = All }).Any(r => r == "post" || r == "latrine" || r == "banner"),
            "a short stay adds no lean-to, latrine or banner");
        C(CampBuildMath.GrowWants(new GrowFacts { people = 2, days = 6f, age = "medieval", dines = true, has = All }).All(r => r != "latrine"), "two people need no latrine");
        var piling = hunters;
        piling.loose = 3;
        C(CampBuildMath.GrowWants(piling).First() == "store", "goods with no room come first: another shelf");
        piling.looseFood = true;
        C(CampBuildMath.GrowWants(piling).First() == "basket", "...a basket for their food");
        piling.has = r => r != "basket";
        C(CampBuildMath.GrowWants(piling).First() == "store", "...or a shelf where the game has no basket");
        var none = hunters;
        none.has = r => r == "fireseat";
        C(CampBuildMath.GrowWants(none).SequenceEqual(new[] { "fireseat" }), "only what the game has a piece for");
        C(CampBuildMath.GrowNext(hunters, new HashSet<string> { "drying" }, 0) == hw[1], "the next not yet built");
        C(CampBuildMath.GrowNext(hunters, null, CampBuildMath.GrowCeiling(4, 7f)) == null, "nothing once they've grown as much as the stay allows");
        C(CampBuildMath.GrowRoles.All(r => CampBuildMath.GrowPieces(r, 4) >= 1) && CampBuildMath.GrowPieces("fireseat", 9) == 4 && CampBuildMath.GrowPieces("fireseat", 1) == 2,
            "seats for each of them, four at most");

        // ---------------- Where it goes ----------------

        int Open(int x, int z) => 0;
        int Wooded(int x, int z) => CampBuildMath.Cost(CellFate.Fell);
        int HalfBlocked(int x, int z) => x <= -5 ? -1 : 0;
        var fits = new Dictionary<string, int>();
        int openLayouts = 0;
        foreach (var form in new[] { CampSleepForm.Ring, CampSleepForm.Hut, CampSleepForm.Tents })
            foreach (int people in new[] { 3, 6, 10 })
                foreach (var (cost, ground) in new (Func<int, int, int>, string)[] { (Open, "open ground"), (Wooded, "a map full of trees"), (HalfBlocked, "beside the colony's land") })
                {
                    var spec = new CampLayoutSpec
                    {
                        sleepers = people, sleep = form, tables = CampBuildMath.TablesFor(people, 7f, true), butcher = true, craft = true, picket = true, stash = true,
                        lights = CampBuildMath.LightsFor(people, true), windbreak = form == CampSleepForm.Ring
                    };
                    var layout = CampBuildMath.Plan(spec, cost);
                    string what = $"{form} of {people} on {ground}";
                    var site = SiteOf(layout, cost);
                    var before = new HashSet<(int, int)>(site.taken);
                    var grown = new List<CampPiece>();
                    if (ground == "open ground") openLayouts++;
                    foreach (var role in CampBuildMath.GrowRoles)
                    {
                        int w = role == "stonework" || role == "tailoring" ? 3 : role == "woodpile" || role == "store" || role == "basket" ? 2 : 1;
                        var pieces = CampBuildMath.GrowPlace(role, site, w, 1, CampBuildMath.GrowPieces(role, people), 3);
                        bool must = role != "latrine" || form != CampSleepForm.Ring;
                        if (pieces == null)
                        {
                            if (ground != "beside the colony's land" && must && role != "fence") C(false, $"{what}: room for {role} (the stash at {layout.stash})");
                            continue;
                        }
                        if (ground == "open ground") fits[role] = fits.GetValueOrDefault(role) + 1;
                        C(pieces.All(p => p.role == role), $"{what}: {role}'s pieces are its own");
                        C(pieces.SelectMany(p => p.Cells()).All(c => !site.taken.Contains(c)), $"{what}: {role} on nothing that stands");
                        C(pieces.SelectMany(p => p.Cells()).All(c => !site.keep.Contains(c)), $"{what}: {role} on no cell kept free");
                        C(pieces.SelectMany(p => p.Cells()).All(c => Math.Abs(c.x) <= CampBuildMath.Reach && Math.Abs(c.z) <= CampBuildMath.Reach), $"{what}: {role} within the camp's reach");
                        C(pieces.SelectMany(p => p.Cells()).All(c => cost(c.x, c.z) >= 0), $"{what}: {role} never on the colony's ground");
                        C(pieces.SelectMany(p => p.Cells()).All(c => Math.Max(Math.Abs(c.x), Math.Abs(c.z)) >= 2), $"{what}: {role} keeps the way round the fire free");
                        Check(role, pieces, site, layout, what, C);
                        foreach (var p in pieces)
                            foreach (var c in p.Cells()) site.taken.Add(c);
                        if (role != "fireseat" && role != "post" && role != "fence")
                            foreach (var p in pieces) site.keep.Add(CampBuildMath.FrontCell(p));
                        grown.AddRange(pieces);
                    }
                    int cells = grown.Sum(p => p.Width * p.Depth);
                    C(cells <= 50, $"{what}: everything it could add is {cells} cells, a settled look, not a map filled");
                    C(Overlap(grown) == null, $"{what}: nothing it added overlaps ({Overlap(grown)})");
                }
        foreach (var role in CampBuildMath.GrowRoles)
            C(fits.GetValueOrDefault(role) * 3 >= openLayouts * 2, $"on open ground {role} finds room in most camps ({fits.GetValueOrDefault(role)} of {openLayouts})");
        n += Kits(root, C);
        return n;
    }

    /// <summary>The camp as laid out, as the game reads it for growth: what stands, the stash's ground kept and its shelf to roof, the picket, the butcher's spot, the beds.</summary>
    private static GrowSite SiteOf(CampLayout l, Func<int, int, int> cost)
    {
        var site = new GrowSite { cost = cost };
        foreach (var p in l.pieces)
            foreach (var c in p.Cells()) site.taken.Add(c);
        foreach (var p in l.pieces.Where(p => p.role == "bed" || p.role == "hutbed"))
            foreach (var c in p.Cells()) site.beds.Add(c);
        foreach (var p in l.pieces.Where(p => p.role == "door"))
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                site.keep.Add((p.minX + dx, p.minZ + dz));
        foreach (var p in l.pieces.Where(p => p.role == "butcher" || p.role == "craft"))
            site.keep.Add((p.minX, p.minZ - 1));
        foreach (int room in l.pieces.Where(p => p.room > 0).Select(p => p.room).Distinct())
            foreach (var c in CampBuildMath.Inside(l.pieces, room)) site.keep.Add(c);
        site.stash = l.stash;
        var shelf = l.pieces.FirstOrDefault(p => p.role == "stash");
        if (shelf != null)
            for (int x = l.stash.x; x <= l.stash.x + 1; x++)
                for (int z = l.stash.z; z <= l.stash.z + 1; z++)
                {
                    site.cover.Add((x, z));
                    if (!shelf.Holds(x, z)) site.keep.Add((x, z));
                }
        var picket = l.pieces.FirstOrDefault(p => p.role == "picket");
        if (picket != null) site.picket = (picket.minX, picket.minZ);
        var butcher = l.pieces.FirstOrDefault(p => p.role == "butcher");
        if (butcher != null) site.butcher = (butcher.minX, butcher.minZ);
        return site;
    }

    /// <summary>Each addition as it should stand.</summary>
    private static void Check(string role, List<CampPiece> pieces, GrowSite site, CampLayout l, string what, Action<bool, string> C)
    {
        switch (role)
        {
            case "fireseat":
                C(pieces.Count >= 2 && pieces.Count <= 4, $"{what}: two to four seats round the fire ({pieces.Count})");
                C(pieces.All(p => Math.Max(Math.Abs(p.minX), Math.Abs(p.minZ)) == 2), $"{what}: the seats two cells out from the fire");
                C(pieces.All(p => pieces.All(o => o == p || Math.Abs(o.minX - p.minX) > 1 || Math.Abs(o.minZ - p.minZ) > 1)), $"{what}: a cell between the seats, so the fire is never closed in");
                C(pieces.All(p => p.facing == CampBuildMath.LookToward(p.minX, p.minZ)), $"{what}: each seat looks at the fire");
                break;
            case "post":
                C(pieces.Count >= 2 && pieces.Count <= CampBuildMath.LeanToPosts, $"{what}: two to four posts for the lean-to ({pieces.Count})");
                var roof = CampBuildMath.Roofed(pieces.Select(p => (p.minX, p.minZ)), site.cover);
                C(site.cover.All(roof.Contains), $"{what}: the lean-to's roof covers the stash and its shelf");
                C(roof.All(c => pieces.Any(p => Math.Max(Math.Abs(p.minX - c.x), Math.Abs(p.minZ - c.z)) <= CampBuildMath.RoofReach)), $"{what}: every roofed cell near a post");
                C(roof.Count <= 36, $"{what}: a lean-to, not a hall ({roof.Count} cells)");
                break;
            case "fence":
                var inside = CampBuildMath.PenInside(pieces.Select(p => (p.minX, p.minZ)));
                C(inside.Count == 9, $"{what}: a pen three by three for three beasts ({inside.Count})");
                C(inside.All(c => !site.taken.Contains(c) && !pieces.Any(p => p.Holds(c.x, c.z))), $"{what}: the pen's inside is free ground");
                int ring = 2 * (pieces.Max(p => p.maxX) - pieces.Min(p => p.minX) + 1) + 2 * (pieces.Max(p => p.maxZ) - pieces.Min(p => p.minZ) + 1) - 4;
                C(pieces.Count == ring - 1, $"{what}: fences all round but one gap ({pieces.Count} of {ring})");
                break;
            case "banner":
                C(pieces.All(p => p.maxZ <= -4), $"{what}: the banner at the edge on the colony's side");
                break;
            case "shrine":
                C(pieces.All(p => p.minZ >= 2), $"{what}: the altar on the far side");
                break;
            case "latrine":
                C(pieces.All(p => p.minZ >= 3 && site.beds.All(b => Math.Abs(b.x - p.minX) + Math.Abs(b.z - p.minZ) >= 5)), $"{what}: the latrine at the far edge, away from the beds");
                break;
            case "stonework":
            case "tailoring":
            case "woodpile":
            case "store":
            case "basket":
            case "drying":
                foreach (var p in pieces)
                {
                    var front = CampBuildMath.FrontCell(p);
                    C(!site.taken.Contains(front) && !pieces.Any(o => o.Holds(front.x, front.z)), $"{what}: {role}'s front free to stand at");
                    if (role == "stonework" || role == "tailoring") C(p.Width * p.Depth == 3, $"{what}: the bench whole");
                    if (role == "store" || role == "basket")
                        C(site.stash.HasValue && Math.Abs(p.minX - site.stash.Value.x) + Math.Abs(p.minZ - site.stash.Value.z) <= 9, $"{what}: {role} by the stash");
                }
                break;
        }
    }

    private static string Overlap(List<CampPiece> pieces)
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

    /// <summary>The kinds' tidiness: a word the game knows, the orderly and the careless where their letters have them so.</summary>
    private static int Kinds(string root, Action<bool, string> C)
    {
        int n = 0;
        void Check(bool ok, string what)
        {
            n++;
            C(ok, what);
        }
        var kinds = Directory.GetFiles(Path.Combine(root, "Defs", "CampDefs"), "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.CampKindDef")).ToList();
        string TidyOf(string dn) => kinds.FirstOrDefault(k => k.Element("defName")?.Value == dn)?.Element("tidy")?.Value;
        foreach (var k in kinds.Where(k => k.Element("tidy") != null))
            Check(Enum.TryParse<CampTidy>(k.Element("tidy")!.Value, out _), $"{k.Element("defName")?.Value}: a tidiness the game knows ({k.Element("tidy")!.Value})");
        Check(TidyOf("Council_Camp_Healers") == "Orderly" && TidyOf("Council_Camp_Pilgrims") == "Orderly" && TidyOf("Rimfolk_Camp_Scholars") == "Orderly", "healers, pilgrims and scholars keep an orderly camp");
        Check(TidyOf("Rimfolk_Camp_Salvagers") == "Careless" && TidyOf("Council_Camp_Minstrels") == "Careless", "salvagers and minstrels a careless one");
        Check(kinds.Count(k => k.Element("tidy") != null) >= 15, "a good many kinds say how tidy they are");
        return n;
    }

    /// <summary>The kits' grows: every piece from the player's catalogue, active, soft under its own mod, of a role the growth knows.</summary>
    private static int Kits(string root, Action<bool, string> C)
    {
        int n = 0;
        void Check(bool ok, string what)
        {
            n++;
            C(ok, what);
        }
        var table = Path.Combine(root, "docs", "player-mods", "defs.tsv.gz");
        var file = Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKits.xml");
        Check(File.Exists(table) && File.Exists(file), "the catalogue of defs and the camp kits are there");
        if (!File.Exists(table) || !File.Exists(file)) return n;
        var things = new Dictionary<string, (string mod, bool active)>();
        using (var gz = new GZipStream(File.OpenRead(table), CompressionMode.Decompress))
        using (var r = new StreamReader(gz))
        {
            r.ReadLine();
            string line;
            while ((line = r.ReadLine()) != null)
            {
                var cells = line.Split('\t');
                if (cells.Length < 4 || cells[0] != "ThingDef") continue;
                if (!things.ContainsKey(cells[1]) || cells[3] == "yes") things[cells[1]] = (cells[2], cells[3] == "yes");
            }
        }
        var growRoles = CampBuildMath.GrowRoles.Where(r => r != "fireseat" && r != "store").Concat(new[] { "imperialbanner" }).ToList();
        foreach (var kit in XDocument.Load(file).Root!.Elements("ColonyCouncil.CampKitDef"))
        {
            string age = kit.Element("age")?.Value ?? "?";
            var grows = kit.Element("grows")?.Elements("li").ToList() ?? new List<XElement>();
            foreach (var role in growRoles)
                Check(grows.Any(g => g.Element("role")?.Value == role), $"{age}: the kit says what it adds as {role}");
            foreach (var g in grows)
            {
                string role = g.Element("role")?.Value ?? "?";
                Check(growRoles.Contains(role), $"{age}: {role} is a growth the camp knows");
                foreach (var d in g.Element("defs")?.Elements("li") ?? Enumerable.Empty<XElement>())
                {
                    Check(things.TryGetValue(d.Value, out var t), $"{age} {role}: {d.Value} is in the player's catalogue");
                    if (!things.TryGetValue(d.Value, out t)) continue;
                    Check(t.active, $"{age} {role}: {d.Value} is from an active mod ({t.mod})");
                    var may = d.Attribute("MayRequire")?.Value;
                    bool core = t.mod == "ludeon.rimworld";
                    Check(core ? may == null : may != null && may.ToLowerInvariant().Split(',').Contains(t.mod), $"{age} {role}: {d.Value} is soft, under its own package ({may ?? "none"} for {t.mod})");
                }
            }
        }
        return n;
    }
}
