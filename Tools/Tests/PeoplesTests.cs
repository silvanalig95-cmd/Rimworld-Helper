using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Rimfolk's own peoples, release A and the defs of release C (Peoples/PeoplesMath.cs; docs/design/peoples.md; release C's
/// own rules and callers in PeoplesCTests): what each lives by and the tags
/// Rimfolk's lines are written for, how much likelier their caravans, camps and asks are, their notables' titles, their
/// towns' plans read into the kept-layout grid by the game's own footprint rule; and the mod's side: the seven faction
/// defs on the game's parents, never required at a world's making, their gear, faith, kinds of people and traders soft
/// on every mod, the emblems drawn in code, their asks, callers and camp only from a people of their calling, the patch
/// on Rimfolk's own defs, the words, and the seams in the code (no clock of its own, nothing saved).
/// </summary>
public static class PeoplesTests
{
    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "peoples: " + what);
        }

        bool Near(float a, float b) => Math.Abs(a - b) < 0.0001f;

        // ================= What they live by =================

        C(PeoplesMath.Callings.SequenceEqual(new[] { "traders", "healers", "herders", "pilgrims", "scholars", "brokers", "fishers" }),
            "seven callings: the road league's, the hospitallers', the herders'; the pilgrims', the scholars', the enclave's brokers', the river folk's");
        C(PeoplesMath.Tags.SequenceEqual(PeoplesMath.Callings.Select(c => "their:" + c)) && PeoplesMath.Tag("healers") == "their:healers" && PeoplesMath.Tag("fishers") == "their:fishers",
            "each gives Rimfolk's lines a tag of its own");
        C(PeoplesMath.Tag(null) == null && PeoplesMath.Tag("pirates") == null && !PeoplesMath.IsCalling("") && PeoplesMath.IsCalling("herders"), "any other people gives none");

        // ================= How often =================

        C(Near(PeoplesMath.Preference("herders", "herders"), PeoplesMath.Prefer) && Near(PeoplesMath.Preference("herders", "traders"), 1f)
          && Near(PeoplesMath.Preference(null, "traders"), 1f) && Near(PeoplesMath.Preference("", null), 1f), "a preference weighs its calling five times, nobody else less");
        C(PeoplesMath.Fits(null, null) && PeoplesMath.Fits("", "traders") && PeoplesMath.Fits("healers", "healers") && !PeoplesMath.Fits("healers", null)
          && !PeoplesMath.Fits("healers", "herders"), "only for a calling: a people of it, anyone when it names none");
        C(Near(PeoplesMath.CaravanFactor(3f), 3f) && Near(PeoplesMath.CaravanFactor(99f), PeoplesMath.MaxCaravanWeight) && Near(PeoplesMath.CaravanFactor(0f), PeoplesMath.MinCaravanWeight)
          && Near(PeoplesMath.CaravanFactor(float.NaN), 1f), "a caravan weight read safely, within bounds");
        C(Near(PeoplesMath.CampFactor(true), 4f) && Near(PeoplesMath.CampFactor(false), 1f), "a kind of camp they favour comes from them four times as often");

        // ================= Their ways =================

        C(PeoplesMath.Title("head", true, "prior", "prioress", "subprior", null, "almoner", "cellarer") == "prioress"
          && PeoplesMath.Title("head", false, "prior", "prioress", null, null, null, null) == "prior", "the head by their ways, a woman by the female word");
        C(PeoplesMath.Title("heir", true, "x", null, "subprior", "", null, null) == "subprior" && PeoplesMath.Title("heir", false, "x", null, null, null, null, null) == null,
            "no female word: the plain one; no word: the age's own");
        C(PeoplesMath.Title("envoy", false, null, null, null, null, "almoner", null) == "almoner" && PeoplesMath.Title("trader", true, null, null, null, null, null, "drover") == "drover"
          && PeoplesMath.Title("acquaintance", false, "a", "b", "c", "d", "e", "f") == null, "the envoy and the trader by their ways; someone met keeps the met word");
        C(PeoplesMath.TendsLodgers("healers") && !PeoplesMath.TendsLodgers("traders") && !PeoplesMath.TendsLodgers(null), "only the hospitallers' sickhouse tends lodgers");
        C(PeoplesMath.PlagueYear(new[] { "trade", "sickness" }) && !PeoplesMath.PlagueYear(new[] { "hunger" }) && !PeoplesMath.PlagueYear(null), "a plague year: a sickness among the world events");

        // ================= The game's footprint rule =================

        void Foot(int x, int z, int rot, int sx, int sz, int ex, int ez, int ew, int eh, string what)
        {
            PeoplesMath.Footprint(x, z, rot, sx, sz, out int mx, out int mz, out int w, out int h);
            C(mx == ex && mz == ez && w == ew && h == eh, $"footprint: {what} ({mx},{mz} {w}x{h})");
        }
        Foot(5, 5, 0, 1, 1, 5, 5, 1, 1, "one cell is its origin");
        Foot(5, 5, 0, 1, 2, 5, 5, 1, 2, "a bed facing North reaches north");
        Foot(5, 5, 2, 1, 2, 5, 4, 1, 2, "a bed facing South reaches south (the cell below in a plan)");
        Foot(5, 5, 1, 1, 2, 5, 5, 2, 1, "a bed facing East reaches east");
        Foot(5, 5, 3, 1, 2, 4, 5, 2, 1, "a bed facing West reaches west");
        Foot(5, 5, 0, 2, 2, 5, 5, 2, 2, "a 2 by 2 table reaches east and north");
        Foot(5, 5, 0, 3, 1, 4, 5, 3, 1, "a 3 by 1 bench is centred");
        C(PeoplesMath.RowZ(0, 10) == 9 && PeoplesMath.RowZ(9, 10) == 0, "a plan's first row is its north");
        C(PeoplesMath.IsGround('_') && PeoplesMath.IsGround(' ') && !PeoplesMath.IsGround('#'), "an underscore (or a space) is the ground as it is");

        var tiny = new List<PlanKey>
        {
            new PlanKey { key = '#', thing = "Wall" }, new PlanKey { key = 'b', thing = "Bed", rot = 2, sizeZ = 2, floor = "F", roof = true },
            new PlanKey { key = '.', floor = "F", roof = true, mark = "store" }
        };
        C(PeoplesMath.Check(new[] { "###", "#b#", "#.#", "###" }, tiny).Count == 0, "a sound plan has no faults");
        C(PeoplesMath.Check(new[] { "###", "#b#", "###" }, tiny).Any(e => e.Contains("covers")), "a bed over a wall is a fault");
        C(PeoplesMath.Check(new[] { "###", "#b#" }, tiny).Any(e => e.Contains("reaches past")), "a bed past the plan's edge is a fault");
        C(PeoplesMath.Check(new[] { "###", "#?#" }, tiny).Any(e => e.Contains("no key")) && PeoplesMath.Check(new[] { "##", "#" }, tiny).Any(e => e.Contains("long")),
            "an unknown character and a short row are faults");
        C(PeoplesMath.Check(new[] { "#" }, tiny.Concat(new[] { new PlanKey { key = '#' } }).ToList()).Any(e => e.Contains("twice")), "a key given twice is a fault");
        var marks = new Dictionary<string, List<int>>();
        var g = PeoplesMath.Grid(new[] { "###", "#b#", "#.#", "###" }, tiny, 10, 20, 250, 250, marks);
        C(g != null && g.width == 3 && g.height == 4 && g.originX == 10 && g.originZ == 20 && g.mapX == 250, "the grid is the plan's size, at its corner");
        int bedAt = g!.Index(1, 2);
        C(LayoutMath.Entry(g, LayoutMath.NumberOf(g.things[bedAt])) == LayoutMath.BuildingEntry("Bed", null) && LayoutMath.RotOf(g.things[bedAt]) == 2,
            "a building at its origin with its turn, the way a kept layout keeps one");
        C((g.flags[bedAt] & LayoutMath.Roof) != 0 && (g.flags[bedAt] & LayoutMath.Clear) != 0 && g.floors[bedAt] != 0, "its roof, its floor, and the land's rock cleared");
        C(marks.TryGetValue("store", out var store) && store.Count == 1 && store[0] == g.Index(1, 1), "what a cell is for, by its index");
        C(PeoplesMath.Grid(new[] { "###", "#b#" }, tiny, 0, 0, 250, 250, null) == null, "an unsound plan gives no grid");
        var ground = PeoplesMath.Grid(new[] { "#_#" }, tiny, 0, 0, 250, 250, null);
        C(ground != null && ground.flags[1] == 0 && ground.things[1] == 0, "the ground is left as it is");
        PeoplesMath.Corner(125, 125, 33, 17, 250, 250, out int cx, out int cz);
        C(cx == 109 && cz == 117, "the plan centred on the town's middle");
        PeoplesMath.Corner(245, 3, 33, 17, 250, 250, out cx, out cz);
        C(cx == 217 && cz == 0, "moved in to fit the map");
        C(PeoplesMath.LandFits(0, 100) && PeoplesMath.LandFits(6, 100) && !PeoplesMath.LandFits(7, 100) && !PeoplesMath.LandFits(0, 0), "a little bad ground is borne, more is not");
        C(PeoplesMath.HerdCount(100, 8, 14, 0f) == 8 && PeoplesMath.HerdCount(100, 8, 14, 0.999f) == 14 && PeoplesMath.HerdCount(20, 8, 14, 0.5f) == 5
          && PeoplesMath.HerdCount(0, 8, 14, 0.5f) == 0, "the herd in the pens: the people's count, room to graze");

        // ================= The defs =================

        string peopleDir = Path.Combine(root, "Defs", "PeopleDefs");
        var files = Directory.GetFiles(peopleDir, "*.xml").OrderBy(f => f).ToList();
        var all = files.SelectMany(f => XDocument.Load(f).Root!.Elements()).ToList();
        var factions = all.Where(e => e.Name.LocalName == "FactionDef").ToList();
        var kinds = all.Where(e => e.Name.LocalName == "PawnKindDef").ToList();
        var traders = all.Where(e => e.Name.LocalName == "TraderKindDef").ToList();
        var plans = all.Where(e => e.Name.LocalName == "ColonyCouncil.TownPlanDef").ToList();
        string Dn(XElement e) => e.Element("defName")?.Value ?? "";
        var kindNames = kinds.Select(Dn).Where(x => x != "").ToHashSet();
        var traderNames = traders.Select(Dn).ToHashSet();
        C(factions.Count == 7, "seven peoples: release A's three and release C's four");
        var campNames = Directory.GetFiles(Path.Combine(root, "Defs", "CampDefs"), "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.CampKindDef"))
            .Select(Dn).ToHashSet();
        // Kinds the peoples borrow from the game: its mercenaries, its children (Biotech), Ideology's work camps, its pack beasts.
        var borrowed = new HashSet<string> { "Mercenary_Gunner", "Villager_Child", "Tribal_Child", "Miner", "Hunter", "Logger", "Farmer", "Tribal_Hunter", "Tribal_Logger",
            "Muffalo", "Dromedary", "Horse", "Alpaca", "Elephant", "Yak", "Donkey" };
        var callings = new List<string>();
        var weights = new Dictionary<string, float>();
        var towns = new Dictionary<string, float>();
        foreach (var f in factions)
        {
            string name = Dn(f);
            string parent = f.Attribute("ParentName")?.Value ?? "";
            C(parent == "OutlanderFactionBase" || parent == "TribeBase", $"{name}: on the game's own parent");
            C(f.Element("requiredCountAtGameStart")?.Value == "0", $"{name}: never required at a world's making");
            C(f.Element("startingCountAtWorldCreation")?.Value == "1" && int.Parse(f.Element("maxConfigurableAtWorldCreation")?.Value ?? "0") > 0
              && f.Element("displayInFactionSelection") == null, $"{name}: listed once among a new world's peoples, at the game's usual chances");
            C(f.Element("naturalEnemy")?.Value == "false" && f.Element("permanentEnemy")?.Value == "false" && f.Element("hidden")?.Value == "false"
              && f.Element("canSiege")?.Value == "false", $"{name}: peaceful, never a natural or permanent enemy, no sieges");
            var raid = f.Element("raidCommonalityFromPointsCurve");
            C(raid?.Attribute("Inherit")?.Value == "False" && raid.Descendants("li").All(li => float.Parse(li.Value.Trim('(', ')').Split(',')[1], System.Globalization.CultureInfo.InvariantCulture) < 1f),
                $"{name}: raids only when wronged, and less often than other peoples then");
            C(f.Element("colorSpectrum")?.Elements("li").Count() == 2, $"{name}: its colours");
            foreach (var tex in new[] { "factionIconPath", "settlementTexturePath" })
            {
                string path = f.Element(tex)?.Value ?? "";
                C(path.StartsWith("ColonyCouncil/Peoples/") && File.Exists(Path.Combine(root, "Textures", path + ".png")), $"{name}: {tex} drawn in code ({path})");
            }
            C(f.Element("fixedIdeo")?.Value == "true" && !string.IsNullOrEmpty(f.Element("ideoName")?.Value) && !string.IsNullOrEmpty(f.Element("ideoDescription")?.Value),
                $"{name}: a faith of its own (Ideology)");
            var memes = f.Element("forcedMemes")?.Elements("li").ToList() ?? new List<XElement>();
            C(memes.Count >= 2 && memes.All(li => li.Attribute("MayRequire") != null)
              && memes.Count(li => li.Attribute("MayRequire")!.Value == "Ludeon.RimWorld.Ideology") >= 1, $"{name}: its memes soft, at least one of the game's own without any mod");
            C(f.Element("structureMemeWeights")?.Attribute("Inherit")?.Value == "False" && f.Element("structureMemeWeights")!.Elements().All(e => e.Attribute("MayRequire") != null)
              && f.Element("styles")?.Elements("li").All(li => li.Attribute("MayRequire")?.Value == "Ludeon.RimWorld.Ideology") == true, $"{name}: its structure and style only with Ideology");
            var xeno = f.Element("xenotypeSet")?.Element("xenotypeChances")?.Elements().ToList() ?? new List<XElement>();
            C(xeno.Count >= 1 && xeno.All(e => (e.Attribute("MayRequire")?.Value ?? "").Contains("Ludeon.RimWorld.Biotech"))
              && xeno.Sum(e => float.Parse(e.Value, System.Globalization.CultureInfo.InvariantCulture)) <= 0.15f, $"{name}: humans mostly, another kind of people only with Biotech");
            C(kindNames.Contains(f.Element("basicMemberKind")?.Value ?? ""), $"{name}: its ordinary member is its own");
            var groups = f.Element("pawnGroupMakers");
            C(groups?.Attribute("Inherit")?.Value == "False", $"{name}: its own groups, none of the parent's");
            var groupKinds = groups!.Elements("li").Select(li => li.Element("kindDef")?.Value).ToList();
            C(new[] { "Combat", "Peaceful", "Trader", "Settlement" }.All(groupKinds.Contains), $"{name}: fights, peaceful visits, caravans and its towns' defenders");
            foreach (var opt in groups.Descendants().Where(e => e.Parent != null && new[] { "options", "traders", "carriers", "guards" }.Contains(e.Parent.Name.LocalName)))
                C(kindNames.Contains(opt.Name.LocalName) || borrowed.Contains(opt.Name.LocalName), $"{name}: {opt.Name.LocalName} is its own kind or the game's");
            var trader = groups.Elements("li").First(li => li.Element("kindDef")?.Value == "Trader");
            C(trader.Element("traders")!.Elements().All(t => kinds.First(k => Dn(k) == t.Name.LocalName).Element("trader")?.Value == "true")
              && trader.Element("guards")!.Elements().Count() >= 2, $"{name}: a trader of its own, guarded");
            foreach (var list in new[] { "caravanTraderKinds", "visitorTraderKinds", "baseTraderKinds" })
                C(f.Element(list)?.Attribute("Inherit")?.Value == "False" && f.Element(list)!.Elements("li").All(li => traderNames.Contains(li.Value)), $"{name}: {list} its own");
            var ext = f.Element("modExtensions")?.Elements("li").FirstOrDefault(li => li.Attribute("Class")?.Value == "ColonyCouncil.PeopleExtension");
            C(ext != null, $"{name}: its template for Rimfolk");
            if (ext == null) continue;
            string calling = ext.Element("calling")?.Value ?? "";
            callings.Add(calling);
            C(PeoplesMath.IsCalling(calling), $"{name}: lives by a calling Rimfolk's words know");
            foreach (var word in new[] { "townWord", "townWords", "head", "heir", "envoy", "trader", "courier", "couriers" })
                C(!string.IsNullOrEmpty(ext.Element(word)?.Value), $"{name}: its {word} in its own ways");
            weights[calling] = float.Parse(ext.Element("caravanWeight")?.Value ?? "1", System.Globalization.CultureInfo.InvariantCulture);
            towns[calling] = float.Parse(f.Element("settlementGenerationWeight")!.Value, System.Globalization.CultureInfo.InvariantCulture);
            C(ext.Element("camps")!.Elements("li").All(li => campNames.Contains(li.Value)), $"{name}: the camps it favours are Rimfolk's");
            C(plans.Any(p => Dn(p) == ext.Element("plan")?.Value), $"{name}: its towns' plan");
            C(!string.IsNullOrEmpty(f.Element("leaderTitle")?.Value) && f.Element("factionNameMaker")?.Value.StartsWith("Rimfolk_Namer_") == true
              && f.Element("settlementNameMaker")?.Value.StartsWith("Rimfolk_Namer_") == true, $"{name}: its leader's title, its own names and its towns'");
        }
        C(callings.Distinct().Count() == 7 && callings.Count == 7 && callings.All(PeoplesMath.IsCalling), "one people for each calling");
        C(weights["traders"] == 3f && weights["traders"] > weights["herders"] && weights["herders"] > weights["healers"], "the league's many caravans, the herders' half again");
        C(towns["traders"] < towns["herders"] && towns["herders"] < towns["healers"] && towns["traders"] < 1f, "the league's few towns");

        // Their people: every kind of their own is on its people, an ordinary member forces no skill but the herders' herding.
        foreach (var k in kinds.Where(k => k.Attribute("Abstract") == null))
        {
            string name = Dn(k);
            var parent = kinds.FirstOrDefault(p => p.Attribute("Name")?.Value == k.Attribute("ParentName")?.Value);
            C(parent?.Element("defaultFactionDef") != null && factions.Any(f => Dn(f) == parent.Element("defaultFactionDef")!.Value), $"{name}: one of its people's");
            C(!string.IsNullOrEmpty(k.Element("label")?.Value) && float.Parse(k.Element("combatPower")?.Value ?? "0", System.Globalization.CultureInfo.InvariantCulture) > 0f,
                $"{name}: a label and a strength");
            foreach (var li in k.Element("apparelRequired")?.Elements("li") ?? Enumerable.Empty<XElement>())
                C(!new[] { "Apparel_HatTop", "Apparel_Tailcap", "Apparel_Broadwrap" }.Contains(li.Value) || li.Attribute("MayRequire") != null,
                    $"{name}: {li.Value} only with its DLC");
        }
        foreach (var f in factions)
        {
            var basic = kinds.First(k => Dn(k) == f.Element("basicMemberKind")!.Value);
            var skills = basic.Element("skills")?.Descendants("skill").Select(s => s.Value).ToList() ?? new List<string>();
            C(skills.All(s => s == "Animals"), $"{Dn(f)}: an ordinary member is no healer or trader by force (a joiner may be one)");
        }

        // Their traders: rich stock for the league, the herd for the herders, medicine for the order; buying the colony's art.
        int Gens(string trader) => traders.First(t => Dn(t) == trader).Element("stockGenerators")!.Elements("li").Count();
        C(Gens("Rimfolk_Caravan_League_Goods") >= 20 && Gens("Rimfolk_Caravan_League_Far") >= 15, "the league's caravans: rich and varied stock");
        var herd = traders.First(t => Dn(t) == "Rimfolk_Caravan_Herders_Herd").Element("stockGenerators")!.Elements("li")
            .First(li => li.Attribute("Class")?.Value == "StockGenerator_Animals");
        C(herd.Element("countRange")?.Value == "8~14", "the herders' caravan walks a herd of eight to fourteen beside its pack beasts");
        C(traders.First(t => Dn(t) == "Rimfolk_Caravan_Order").Descendants("thingDef").Any(d => d.Value == "MedicineHerbal"), "the order's caravan carries medicine");
        C(traders.All(t => t.Element("stockGenerators")!.Elements("li").Any(li => li.Attribute("Class")?.Value == "StockGenerator_SingleDef" && li.Element("thingDef")?.Value == "Silver")),
            "every trader of theirs carries silver");

        // ================= Their towns' plans =================

        // The sizes of the game's buildings the plans use (its own defs): a building not listed here needs its size added.
        var sizes = new Dictionary<string, (int, int)>
        {
            ["Wall"] = (1, 1), ["Door"] = (1, 1), ["Bed"] = (1, 2), ["Bedroll"] = (1, 2), ["TorchLamp"] = (1, 1), ["Table2x2c"] = (2, 2), ["Table1x2c"] = (1, 2),
            ["Stool"] = (1, 1), ["ShelfSmall"] = (1, 1), ["Campfire"] = (1, 1), ["Fence"] = (1, 1), ["FenceGate"] = (1, 1), ["AnimalSleepingSpot"] = (1, 1), ["Column"] = (1, 1),
            // Release C's: the archive's bookcases, chairs and research bench, the market's beacon and comms console, shelves.
            ["Bookcase"] = (2, 1), ["BookcaseSmall"] = (1, 1), ["DiningChair"] = (1, 1), ["SimpleResearchBench"] = (3, 2), ["Shelf"] = (2, 1),
            ["OrbitalTradeBeacon"] = (1, 1), ["CommsConsole"] = (3, 2)
        };
        C(plans.Count == 7, "a plan for each people's towns");
        foreach (var p in plans)
        {
            string name = Dn(p);
            var rows = p.Element("rows")!.Elements("li").Select(li => li.Value).ToList();
            var keys = new List<PlanKey>();
            bool known = true;
            foreach (var k in p.Element("keys")!.Elements("li"))
            {
                string thing = k.Element("thing")?.Value;
                if (thing != null && !sizes.ContainsKey(thing))
                {
                    known = false;
                    C(false, $"{name}: {thing}'s size is known to the test");
                    continue;
                }
                var (sx, sz) = thing != null ? sizes[thing] : (1, 1);
                int rot = (k.Element("rot")?.Value ?? "North") switch { "East" => 1, "South" => 2, "West" => 3, _ => 0 };
                keys.Add(new PlanKey { key = k.Element("key")!.Value[0], thing = thing, stuff = k.Element("stuff")?.Value, rot = rot, sizeX = sx, sizeZ = sz,
                    floor = k.Element("floor")?.Value, roof = k.Element("roof")?.Value == "true", mark = k.Element("mark")?.Value });
            }
            var faults = PeoplesMath.Check(rows, keys);
            C(known && faults.Count == 0, $"{name} is sound ({string.Join("; ", faults.Take(3))})");
            C(rows.All(r => !r.Contains(' ')), $"{name}: the ground written as an underscore, so no row loses its spaces");
            C(rows[0].Length <= 40 && rows.Count <= 30, $"{name}: no bigger than the game's own base ({rows[0].Length}x{rows.Count})");
            C(p.Element("keys")!.Descendants("thing").All(t => sizes.ContainsKey(t.Value)) && p.Element("wallStuff")!.Elements("li").Any(),
                $"{name}: only the base game's buildings, and a wall material");
            var pm = new Dictionary<string, List<int>>();
            var grid = PeoplesMath.Grid(rows, keys, 100, 100, 250, 250, pm);
            C(grid != null && pm.ContainsKey("store") && pm["store"].Count >= 6, $"{name}: a storehouse for its goods");
            // Pens: none of their cells reached from the plan's edge but through a gate.
            if (pm.ContainsKey("pen"))
            {
                var block = new HashSet<char>(keys.Where(k => k.thing != null && k.thing != "AnimalSleepingSpot").Select(k => k.key));
                int w = rows[0].Length, h = rows.Count;
                var seen = new HashSet<(int, int)>();
                var q = new Queue<(int, int)>();
                for (int r = 0; r < h; r++)
                    for (int c = 0; c < w; c++)
                        if ((r == 0 || c == 0 || r == h - 1 || c == w - 1) && !block.Contains(rows[r][c]) && seen.Add((r, c)))
                            q.Enqueue((r, c));
                while (q.Count > 0)
                {
                    var (r, c) = q.Dequeue();
                    foreach (var (dr, dc) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int nr = r + dr, nc = c + dc;
                        if (nr >= 0 && nc >= 0 && nr < h && nc < w && !block.Contains(rows[nr][nc]) && seen.Add((nr, nc))) q.Enqueue((nr, nc));
                    }
                }
                var penKeys = keys.Where(k => k.mark == "pen").Select(k => k.key).ToHashSet();
                C(!seen.Any(s => penKeys.Contains(rows[s.Item1][s.Item2])), $"{name}: its pens closed but for their gates");
                C(rows.Any(r => r.Contains('g')), $"{name}: its pens have gates");
            }
        }
        string PlanOf(string calling) => factions.Select(f => f.Element("modExtensions")!.Elements("li").First()).First(e => e.Element("calling")!.Value == calling).Element("plan")!.Value;
        var orderPlan = plans.First(p => Dn(p) == PlanOf("healers"));
        C(orderPlan.Element("keys")!.Elements("li").Any(k => k.Element("mark")?.Value == "plant:Plant_Healroot")
          && orderPlan.Element("rows")!.Elements("li").Sum(r => r.Value.Count(ch => ch == 'b')) >= 16, "the order's sickhouse: a ward of sixteen beds and an herb garden");
        C(plans.First(p => Dn(p) == PlanOf("herders")).Element("keys")!.Elements("li").Any(k => k.Element("mark")?.Value == "pen")
          && factions.Any(f => f.Descendants("herdCount").Any(c => c.Value == "8~14")), "the herders' camp: pens, and a herd in them");
        C(PeoplesMath.HerdCount(78, 8, 14, 0.999f) == 14, "the herders' pens hold their whole herd");

        // ================= Their asks, callers and camp =================

        var missions = XDocument.Load(Path.Combine(root, "Defs", "MissionDefs", "Rimfolk_Missions_Peoples.xml")).Root!.Elements("IncidentDef").ToList();
        XElement Ext(XElement d, string cls) => d.Element("modExtensions")?.Elements("li").FirstOrDefault(li => li.Attribute("Class")?.Value == "ColonyCouncil." + cls);
        var medicine = missions.First(m => Dn(m) == "Rimfolk_Mission_OrderMedicine");
        C(Ext(medicine, "MissionPeople")?.Element("only")?.Value == "healers" && Ext(medicine, "MissionPeople")?.Element("whileKind")?.Value == "sickness"
          && medicine.Element("workerClass")?.Value == "ColonyCouncil.HappeningWorker_MissionDelivery", "the order asks for medicine, only in a plague year, through the deliveries");
        C(Ext(medicine, "MissionDelivery")!.Descendants("what").All(w => w.Value == "medicine"), "medicine and nothing else");
        var escort = missions.First(m => Dn(m) == "Rimfolk_Mission_LeagueEscort");
        C(Ext(escort, "MissionPeople")?.Element("only")?.Value == "traders" && escort.Element("workerClass")?.Value == "ColonyCouncil.HappeningWorker_MissionEscortWho",
            "the league asks for an escort, through the traveller's way");
        C(missions.All(m => float.Parse(m.Element("baseChance")!.Value, System.Globalization.CultureInfo.InvariantCulture) <= 0.4f && int.Parse(m.Element("minRefireDays")!.Value) >= 60),
            "a small share of the missions' one draw, not soon again");
        var events = XDocument.Load(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_Peoples.xml")).Root!.Elements("IncidentDef").ToList();
        var healer = events.First(e => Dn(e) == "Rimfolk_Evt_OrderHealer");
        C(Ext(healer, "HappeningSetting")?.Element("people")?.Value == "calling:healers" && Ext(healer, "HappeningSetting")!.Descendants("li").Any(li => li.Value == "ailing"),
            "the order's hospitaller comes only from the order, for a colonist with an ailment");
        C(healer.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_Visit") && healer.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_EaseAilment"),
            "the care plays out on the map and eases the ailment");
        var outrider = events.First(e => Dn(e) == "Rimfolk_Evt_LeagueOutrider");
        C(Ext(outrider, "HappeningSetting")?.Element("people")?.Value == "calling:traders" && Ext(outrider, "HappeningSetting")!.Descendants("li").Any(li => li.Value == "caravan")
          && outrider.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.WorldEffect_Incident" && li.Element("incident")?.Value == "TraderCaravanArrival"
                                               && li.Element("withFaction")?.Value == "true" && li.Element("anyFaction") == null),
            "the league's outrider: only when its caravan can come, and bidden, the league's own caravan comes (never another's)");
        var camp = XDocument.Load(Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKinds_Peoples.xml")).Root!.Elements("ColonyCouncil.CampKindDef").Single();
        C(camp.Element("calling")?.Value == "herders" && camp.Element("seasons")!.Elements("li").Select(l => l.Value).Contains("Winter")
          && !camp.Element("seasons")!.Elements("li").Any(l => l.Value == "Summer"), "the herders' winter grazing: only theirs, only in the cold months");
        int maxAnimals = int.Parse(camp.Element("maxAnimals")!.Value);
        int most = camp.Element("animals")!.Elements("li").Sum(li => int.Parse(li.Element("count")!.Value.Split('~').Last()));
        C(maxAnimals <= CampLookMath.MaxHerd && most <= maxAnimals && most >= 10, $"a real herd, within its cap ({most} of {maxAnimals})");
        C(CampLookMath.AnimalsFor(9, 0) == CampLookMath.MaxAnimals && CampLookMath.AnimalsFor(9, 0, 14) == 9 && CampLookMath.AnimalsFor(9, 10, 14) == 4
          && CampLookMath.AnimalsFor(20, 0, 99) == CampLookMath.MaxHerd, "a kind's own cap, never past the most any camp brings");
        C(FactsMath.RegionTags.Contains(FactsMath.CampTag(Dn(camp))), "talk knows one of the winter herd's camp");

        // ================= The patch on Rimfolk's own defs =================

        var patch = XDocument.Load(Path.Combine(root, "Patches", "Rimfolk_Peoples.xml")).Root!;
        var allDefs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).SelectMany(f => XDocument.Load(f).Root!.Elements())
            .Select(Dn).Where(x => x != "").ToHashSet();
        foreach (var op in patch.Elements("Operation"))
        {
            string xpath = op.Element("xpath")?.Value ?? "";
            var m = Regex.Match(xpath, "defName=\"([^\"]+)\"");
            C(op.Attribute("Class")?.Value == "PatchOperationConditional" && m.Success && allDefs.Contains(m.Groups[1].Value), $"the patch reaches {m.Groups[1].Value}, conditionally");
        }
        C(patch.Descendants("calling").All(c => PeoplesMath.IsCalling(c.Value)) && patch.Descendants("prefers").All(c => PeoplesMath.IsCalling(c.Value))
          && patch.Descendants("calling").Count() == 8 && patch.Descendants("prefers").Count() == 5, "the patch prefers callings Rimfolk knows: eight world events, two asks and three callers");

        // ================= Their words =================

        var textFiles = files.Concat(new[]
        {
            Path.Combine(root, "Defs", "MissionDefs", "Rimfolk_Missions_Peoples.xml"), Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_Peoples.xml"),
            Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKinds_Peoples.xml"), Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Lines_Peoples.xml"),
            Path.Combine(root, "Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_NewPeoples.xml"), Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Peoples.xml"),
            Path.Combine(root, "Patches", "Rimfolk_Peoples.xml"),
            // Release C's.
            Path.Combine(root, "Defs", "MissionDefs", "Rimfolk_Missions_PeoplesC.xml"), Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeoplesC.xml"),
            Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Lines_PeoplesC.xml"), Path.Combine(root, "Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_PeoplesC.xml"),
            Path.Combine(root, "Defs", "ThoughtDefs", "Rimfolk_Thoughts_PeoplesC.xml"), Path.Combine(root, "Patches", "Rimfolk_PeoplesC.xml")
        }).ToList();
        foreach (var f in textFiles)
        {
            // A line's own words, its tag block ([them,!likesus]) apart.
            var texts = XDocument.Load(f).Descendants().Where(e => !e.HasElements).Select(e => Regex.Replace(e.Value, @"^\[[^\]]*\]\s*", "")).Where(v => v.Contains(' ')).ToList();
            C(texts.All(t => !t.Contains('—') && !t.Contains('!')), $"{Path.GetFileName(f)}: no em dashes and no exclamation marks");
            C(texts.All(t => !Regex.IsMatch(t, @"\bin \d+ (days?|hours?)\b")), $"{Path.GetFileName(f)}: no countdowns");
        }
        // Release A's lines and release C's, each in a file of its own.
        var lines = new[] { "Rimfolk_Lines_Peoples.xml", "Rimfolk_Lines_PeoplesC.xml" }
            .SelectMany(f => XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", f)).Root!.Elements("ColonyCouncil.FlavorExtensionDef")).ToList();
        IEnumerable<XElement> Pool(string pool) => lines.Where(l => l.Element("pool")!.Value == pool).SelectMany(l => l.Descendants("li"));
        foreach (var calling in PeoplesMath.Callings)
        {
            string tag = PeoplesMath.Tag(calling)!;
            C(Pool("Rimfolk_Pool_NeighbourWrites").Count(li => li.Value.Contains(tag)) >= 7, $"their letters in the {calling}' own words");
            C(Pool("Rimfolk_CourierLines").Count(li => li.Value.Contains(tag)) >= 2, $"their messengers in the {calling}' own words");
        }
        foreach (var li in Pool("Rimfolk_CourierLines"))
        {
            string body = Regex.Replace(li.Value, @"^\[[^\]]*\]\s*", "");
            C(Regex.Matches(body, @"[.?]").Count == 1 && body.EndsWith("."), $"a messenger's line is one sentence: {body}");
        }
        C(Pool("Rimfolk_Pool_NeighbourWrites").Where(li => li.Value.StartsWith("[help,cool") || li.Value.StartsWith("[help,curt")).Count() == 0, "help is promised only in a warm or cordial answer");
        var talks = new[] { "Rimfolk_OutsiderTalks_NewPeoples.xml", "Rimfolk_OutsiderTalks_PeoplesC.xml" }
            .SelectMany(f => XDocument.Load(Path.Combine(root, "Defs", "OutsiderTalkDefs", f)).Root!.Elements("ColonyCouncil.OutsiderTalkDef")).ToList();
        foreach (var calling in PeoplesMath.Callings)
            C(talks.Count(t => (t.Element("when")?.Value ?? "").Contains(PeoplesMath.Tag(calling)!)) >= 3, $"three pieces of talk for the {calling}");
        var almanac = XDocument.Load(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Peoples.xml")).Root!.Elements().Single();
        C(Dn(almanac) == "Council_Alm_C_Peoples" && almanac.Element("keywords")!.Elements("li").Count() >= 3, "an Almanac page of its own");

        // ================= The emblems =================

        foreach (var png in new[] { "League", "LeagueTown", "Order", "OrderTown", "Herders", "HerdersTown", "Pilgrims", "PilgrimsTown", "Scholars", "ScholarsTown",
                     "Enclave", "EnclaveTown", "RiverFolk", "RiverFolkTown" })
        {
            string path = Path.Combine(root, "Textures", "ColonyCouncil", "Peoples", png + ".png");
            bool ok = File.Exists(path);
            if (ok)
            {
                var b = File.ReadAllBytes(path);
                int w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19], h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
                ok = w == 128 && h == 128 && b[25] == 6;
            }
            C(ok, $"{png}: a 128 px white glyph with its own alpha, for the game to tint");
        }
        C(File.Exists(Path.Combine(root, "Tools", "make_emblems.py")) && File.Exists(Path.Combine(root, "Tools", "emblems_preview.png")), "drawn in code, with a preview sheet");

        // ================= The seams =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { src }.Concat(parts).ToArray()));
        // Release A's code: no clock, nothing saved. Release B's peoples that arise (Peoples/Arising*.cs) keep one book (ArisingTests).
        string peoples = string.Join("\n", Directory.GetFiles(Path.Combine(src, "Peoples"), "*.cs").Where(f => !Path.GetFileName(f).StartsWith("Arising")).Select(File.ReadAllText));
        C(!peoples.Contains("Sched.") && !peoples.Contains("Scribe_") && !peoples.Contains("ExposeData"), "no clock of its own, nothing saved");
        C(Read("World", "Notables.cs").Contains("Peoples.Title(n.faction, \"head\", female)") && Read("World", "Region", "RegionFaces.cs").Contains("Peoples.Title(f, \"head\""),
            "notables titled by their people's ways");
        C(Read("Couriers", "CourierWords.cs").Contains("Peoples.Courier(f, plural)"), "their messengers by their own word");
        C(Read("World", "Region", "RiseFallPatches.cs").Contains("Peoples.CaravanFactor(f)"), "their caravans weighed in the game's choice of whose comes");
        C(Read("Colonies", "HandOverPeoples.cs").Contains("!Peoples.IsOursDef(d)"), "a handed-over town never borrows one of these peoples' kinds");
        C(Read("Colonies", "TownAfterPatches.cs").Contains("PeopleTowns.PlaceTown(map)"), "their towns' plans placed in the settlement step's prefix");
        C(Read("Harm", "SickHome.cs").Contains("Peoples.TendsLodgers(s.Faction)"), "the sickhouse tends the colony's lodgers");
        C(Read("Text", "Facts", "FactsWho.cs").Contains("PeoplesMath.Tag(Peoples.CallingOf(p))") && Read("Missions", "Missions.cs").Contains("Peoples.AddTags(m.faction, tags)")
          && Read("World", "NeighbourLetters.cs").Contains("Peoples.AddTags(n.faction, tags)") && Read("Couriers", "CourierWords.cs").Contains("Peoples.AddTags(f, tags)"),
            "talk, asks, letters and messengers know them by their calling");
        C(Read("Missions", "Workers_MissionNeighbours4.cs").Contains("MissionPeople.Weighed(d)"), "an ask of theirs takes no share of the draw in a world without them");
        C(Read("Happenings", "Workers_Setting.cs").Contains("Peoples.Preference(s.prefers, f)"), "a caller's people by the calling it prefers");
        C(Read("Camps", "CampTracker.cs").Contains("Peoples.CampFactor(kind, x)") && Read("Camps", "CampTracker.cs").Contains("Peoples.CanSend(k)"),
            "favoured camps likelier from them; a camp only theirs only while they are at peace");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
