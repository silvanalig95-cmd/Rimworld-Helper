using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Semantic checks over every room template and every size of every room type, in all four turns, mirrored and not,
/// in each style, with the best and the fallback furniture: the same layout code the game runs (TemplateLayout), fed
/// with what the buildings are (size, facing rules, work spots). Checks that pieces sit inside the room without
/// overlapping, the door is free, work spots are inside and free, chairs next to a table face it, bed heads are against
/// a wall, the door turns with its wall, and every piece can be reached from the door. Then again with the furniture of
/// every DLC and mod the template doesn't require left out, as a game without them resolves it: the first listed def
/// that is there, or nothing; a room whose key piece is missing must say what it misses. Room types are checked too.
///
/// Facts come from the game's defs where known, and from hand-made 1.6 rooms (Alpha Prefabs layouts) for conventions:
/// a bed's head is its root cell (it extends toward its facing); workbenches face the wall with the work spot behind
/// them (0,0,-1); mech gestators and rechargers put their back to the wall and work on their facing side; DBH toilets
/// face the wall behind them.
///
/// Every layout is checked twice more with wall lamps (LightMath): a strong lamp and a weak one. A small or medium room
/// hangs them in place of its plain lights; each stands on a free floor cell in front of a wall, facing that wall, in
/// every turn and mirrored: never on or beside a door, never on a cooler or vent in the wall, never on a cell where
/// something else is planned (WallLampTests).
/// </summary>
public static class RoomTemplateTests
{
    public class Fact
    {
        public int sx = 1, sz = 1;
        public bool rotatable = true;
        /// <summary>"" blocks, "stand" walkable, "sit" a seat, "door".</summary>
        public string pass = "";
        public List<(int x, int z)> spots = new();
        /// <summary>bed (head at the root), chair, table, wall, door, walllight.</summary>
        public string kind = "";
        /// <summary>Hung on a wall (a wall lamp): it stands in front of its wall, facing it.</summary>
        public bool attached;
    }

    internal static readonly Dictionary<string, Fact> facts = new();

    /// <summary>Furniture that isn't in the base game: the packageId of the DLC or mod it comes from.</summary>
    private static readonly Dictionary<string, string> source = new(StringComparer.OrdinalIgnoreCase);

    private static void From(string package, string names)
    {
        foreach (var name in names.Split(' ', StringSplitOptions.RemoveEmptyEntries)) source[name] = package;
    }

    /// <summary>Where a def comes from: a DLC or mod packageId, "mod" for other mods' furniture, null for the base game.</summary>
    internal static string SourceOf(string name)
    {
        if (source.TryGetValue(name, out var s)) return s;
        foreach (var prefix in new[] { "VFE_", "VFEE_", "VFEM2_", "VFES_", "DankPyon_", "AM_", "Bed_", "Seat_", "Table_Royal", "Table_Wardrobe", "Table_Illuminated", "Table_interactive" })
            if (name.StartsWith(prefix, StringComparison.Ordinal)) return "mod";
        return null;
    }

    private static void F(string names, int sx, int sz, string flags = "", string spots = "", string kind = "")
    {
        foreach (var name in names.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = new Fact { sx = sx, sz = sz, kind = kind };
            foreach (var flag in flags.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (flag == "norot") f.rotatable = false;
                else if (flag == "wall") f.attached = true;
                else f.pass = flag;
            }
            foreach (var s in spots.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var p = s.Split(',');
                f.spots.Add((int.Parse(p[0]), int.Parse(p[1])));
            }
            facts[name] = f;
        }
    }

    static RoomTemplateTests()
    {
        // Beds: the head is the root cell.
        F("Bed HospitalBed Bed_Simple Bed_Ergonomic Bed_StoneSlab Bed_AdvBed VFEM2_FurBed", 1, 2, kind: "bed");
        F("Sarcophagus", 1, 2);
        F("Bedroll SleepingSpot", 1, 2, "stand", kind: "bed");
        F("DoubleBed RoyalBed Bed_Kingsize VFE_Bed_SimpleDouble Bed_DoubleErgonomic Bed_AdvDoubleBed VFEM2_DoubleFurBed", 2, 2, kind: "bed");
        F("BedrollDouble DoubleSleepingSpot", 2, 2, "stand", kind: "bed");
        F("Crib", 1, 1, kind: "bed");
        F("Grave", 1, 2, "stand");
        F("AnimalBed AnimalSleepingBox", 1, 1);
        F("AnimalSleepingSpot", 1, 1, "stand");
        // Seats and tables.
        F("DiningChair Stool Armchair Seat_SquareChair Seat_ModernChair Seat_RoyalChair Seat_RoyalArmchair Throne DBHSaunaSeating", 1, 1, "sit", kind: "chair");
        // Seats the room themes use (Vanilla Furniture Expanded and its Spacer module).
        F("Seat_Bench Seat_Cushion VFE_ModularCouch Seat_AdvChair", 1, 1, "sit", kind: "chair");
        F("GrandThrone", 2, 2, "sit", kind: "chair");
        F("Pew", 3, 1, "sit", kind: "chair");
        F("Table1x2c Table_Royal1x2c", 1, 2, kind: "table");
        F("Table2x2c Table_Royal2x2c", 2, 2, kind: "table");
        F("Table2x4c Table_Royal2x4c", 2, 4, kind: "table");
        F("Table3x3c Table_Royal3x3c", 3, 3, kind: "table");
        F("ChessTable Table_interactive_1x1c", 1, 1, kind: "table");
        F("Table_interactive_2x2c", 2, 2, kind: "table");
        F("PokerTable", 2, 2, kind: "table");
        // Billiards players walk round the table; no seats, no work spot.
        F("BilliardsTable", 2, 3);
        F("FlatscreenTelevision", 2, 1);
        // Workbenches: the worker stands behind them.
        F("TableButcher TableSculpting TableStonecutter TableMachining ElectricSmithy FueledSmithy ElectricTailoringBench HandTailoringBench DrugLab ElectricStove FueledStove Brewery VFE_ComponentFabricationBench VFE_TableStonecutterElectric VFE_TableButcherElectric VFE_TableDrugLabElectric",
            3, 1, spots: "0,-1");
        F("SimpleResearchBench ElectricCrematorium", 3, 2, spots: "0,-1");
        F("HiTechResearchBench FabricationBench VFE_TableMachiningLarge VFE_TableSmithyLarge VFE_TableTailorLarge", 5, 2, spots: "0,-1");
        F("Campfire", 1, 1, spots: "0,-1");
        F("Harp", 1, 1, spots: "0,-1");
        F("Harpsichord", 2, 3, spots: "0,-2");
        F("HorseshoesPin", 1, 1, spots: "0,-3");
        // Biotech and Anomaly
        F("MechGestator", 3, 2, spots: "0,2");
        F("BasicRecharger StandardRecharger", 3, 1, spots: "0,1");
        F("GeneAssembler", 3, 2, spots: "0,2");
        F("GeneBank", 2, 1);
        F("GeneProcessor MultiAnalyzer", 2, 2);
        F("SchoolDesk", 2, 1, spots: "0,-1");
        F("Blackboard", 2, 1);
        F("ToyBox", 1, 1);
        F("HoldingPlatform", 2, 2);
        F("ShardInhibitor", 1, 1);
        F("ElectricInhibitor", 1, 2);
        // Ideology
        F("Altar_Small", 1, 1);
        F("Altar_Medium", 3, 1);
        F("Altar_Large", 3, 2);
        F("Lectern", 1, 1, spots: "0,-1");
        F("Drape", 2, 1);
        F("Brazier DarklightBrazier", 1, 1, "norot");
        F("Column VFE_Plinth DankPyon_Pedestal", 1, 1, "norot");
        // Furniture, light, storage
        F("EndTable Table_RoyalEndTable Table_IlluminatedEndTable", 1, 1);
        F("Dresser Table_RoyalDresser Table_Wardrobe VFE_RoyalWardrobe Table_IlluminatedDresser VFES_Table_IlluminatedWardrobe", 2, 1);
        F("PlantPot VFE_DecorativePlantPot", 1, 1, "norot");
        F("StandingLamp TorchLamp SunLamp Heater PassiveCooler VFE_RoyalLamp VFEE_Candelabra", 1, 1, "norot");
        // 1.6 wall lamps: on the floor cell in front of a wall, facing it, walked under.
        F("WallLamp TorchWallLamp", 1, 1, "stand wall", kind: "walllight");
        // Vanilla Furniture Expanded's floor lamp can be walked over.
        F("VFE_FloorLamp", 1, 1, "stand norot");
        F("MeditationSpot", 1, 1, "stand norot");
        F("Cooler Vent", 1, 1);
        F("Shelf Bookcase ToolCabinet", 2, 1);
        F("ShelfSmall BookcaseSmall VitalsMonitor FermentingBarrel TubeTelevision", 1, 1);
        F("Battery", 1, 2);
        F("HydroponicsBasin", 1, 4);
        F("Door", 1, 1, "door norot", kind: "door");
        F("Wall", 1, 1, "norot", kind: "wall");
        F("PetWaterBowl LitterBox", 1, 1);
        // Dubs Bad Hygiene
        F("ToiletStuff PitLatrine WashBucket ShowerStuff ShowerSimple DBHSaunaHeaterLog DBHSaunaHeaterElec WashingMachine", 1, 1);
        F("ToiletAdvStuff ShowerAdvStuff", 1, 1);
        // A stall door only hides the toilet: people walk through it, it can't turn, and it doesn't split the room.
        F("ToiletStallDoor", 1, 1, "stand norot");
        F("BasinStuff", 1, 1, spots: "0,-1");
        F("BathtubStuff", 1, 2);
        F("HotTub", 2, 2);
        F("KitchenSink", 3, 1, spots: "0,-1");
        // Medieval Overhaul's furniture in the hall and stately themes (its 1.6 defs: sizes as given there, the rest from
        // the game's bases they build on).
        F("DankPyon_Bed_Rustic_Red DankPyon_BedWicker DankPyon_StrawBed", 1, 2, kind: "bed");
        F("DankPyon_Bed_RusticDouble_Red DankPyon_BedWickerDouble DankPyon_StrawBedDouble DankPyon_RoyalTudorBed", 2, 2, kind: "bed");
        F("DankPyon_RusticChair DankPyon_RusticStool DankPyon_RoyalArmchair", 1, 1, "sit", kind: "chair");
        F("DankPyon_RusticTable1x2c", 1, 2, kind: "table");
        F("DankPyon_RusticTable2x2c", 2, 2, kind: "table");
        F("DankPyon_Table2x4c", 2, 4, kind: "table");
        F("DankPyon_RusticEndTable DankPyon_RoyalEndTable", 1, 1);
        F("DankPyon_RusticDresser DankPyon_RoyalDresser", 2, 1);
        // Its candle stands and lamps turn, unlike the game's own lights; its oil wall lamp and wall torch hang on a wall.
        F("DankPyon_CandleStand DankPyon_RusticLamp DankPyon_Candelabra", 1, 1);
        F("DankPyon_WallLamp DankPyon_WallTorch", 1, 1, "stand wall", kind: "walllight");
        // Alpha Props: Parks and Gardens' working street lights can be walked past, like a floor lamp.
        F("VFEPD_AP_LamplightA_Functional VFEPD_AP_LamplightC_Functional", 1, 1, "stand norot");

        From("Ludeon.RimWorld.Royalty", "Throne GrandThrone RoyalBed Harp Harpsichord Brazier MeditationSpot");
        From("Ludeon.RimWorld.Ideology", "Altar_Small Altar_Medium Altar_Large Lectern Pew Drape DarklightBrazier");
        From("Ludeon.RimWorld.Biotech", "Crib SchoolDesk Blackboard ToyBox MechGestator BasicRecharger StandardRecharger GeneAssembler GeneBank GeneProcessor");
        From("Ludeon.RimWorld.Anomaly", "HoldingPlatform ShardInhibitor ElectricInhibitor");
        From("dankpyon.medieval.overhaul", "DankPyon_Bed_Rustic_Red DankPyon_BedWicker DankPyon_StrawBed DankPyon_Bed_RusticDouble_Red DankPyon_BedWickerDouble "
                                          + "DankPyon_StrawBedDouble DankPyon_RoyalTudorBed DankPyon_RusticChair DankPyon_RusticStool DankPyon_RoyalArmchair "
                                          + "DankPyon_RusticTable1x2c DankPyon_RusticTable2x2c DankPyon_Table2x4c DankPyon_RusticEndTable DankPyon_RoyalEndTable "
                                          + "DankPyon_RusticDresser DankPyon_RoyalDresser DankPyon_CandleStand DankPyon_RusticLamp DankPyon_Candelabra "
                                          + "DankPyon_WallLamp DankPyon_WallTorch");
        From("sarg.alphapropsparks", "VFEPD_AP_LamplightA_Functional VFEPD_AP_LamplightC_Functional");
        From("dubwise.dubsbadhygiene", "ToiletStuff ToiletAdvStuff PitLatrine WashBucket ShowerStuff ShowerSimple ShowerAdvStuff BasinStuff BathtubStuff HotTub KitchenSink "
                                       + "ToiletStallDoor DBHSaunaHeaterLog DBHSaunaHeaterElec DBHSaunaSeating WashingMachine PetWaterBowl LitterBox");
    }

    public class Thing
    {
        public List<string> defs = new();
        public string role, tier;
        public int x, z, rot;
        public bool optional = true, inWall, keepOnFloor;

        /// <summary>A light the room may hang on its walls instead, as the game decides it (TemplateThing.MayHang).</summary>
        public bool MayHang(string pick) =>
            role == "light" && !inWall && !(keepOnFloor && pick != null && defs.Count > 0 && pick == defs[0]);
    }

    public class Template
    {
        public string defName, file, group;
        public int w, h, doorX = -1;
        public List<Thing> things = new();
        /// <summary>The DLCs and mods it loads with (MayRequire): their furniture is there whenever the template is.</summary>
        public HashSet<string> requires = new(StringComparer.OrdinalIgnoreCase);
        public int DoorX => doorX >= 0 && doorX < w ? doorX : w / 2;
    }

    public class RoomType
    {
        public string defName, main, file;
        public List<string> variants = new();
    }

    private static HashSet<string> Requires(XElement el)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var attr in new[] { "MayRequire", "MayRequireAnyOf" })
            foreach (var p in (el.Attribute(attr)?.Value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                set.Add(p);
        return set;
    }

    private static readonly Regex Vec = new(@"\(\s*(-?\d+)\s*,\s*(-?\d+)\s*\)");

    private static int Rot(string s) => s switch { null or "" or "North" => 0, "East" => 1, "South" => 2, "West" => 3, _ => int.Parse(s) };

    public static (List<Template> templates, List<RoomType> types) Load(string root)
    {
        var templates = new List<Template>();
        var types = new List<RoomType>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "RoomTemplateDefs"), "*.xml"))
        {
            var doc = XDocument.Load(file);
            foreach (var el in doc.Root!.Elements())
            {
                if (el.Name.LocalName == "ColonyCouncil.RoomTypeDef")
                {
                    types.Add(new RoomType
                    {
                        defName = el.Element("defName")?.Value ?? "?",
                        main = el.Element("main")?.Value,
                        file = Path.GetFileName(file),
                        variants = el.Element("variants")?.Elements().Select(v => v.Element("template")?.Value ?? "").ToList() ?? new List<string>()
                    });
                    continue;
                }
                if (el.Name.LocalName != "ColonyCouncil.RoomTemplateDef") continue;
                var t = new Template { defName = el.Element("defName")?.Value ?? "?", file = Path.GetFileName(file), group = el.Element("group")?.Value ?? "Living", requires = Requires(el) };
                var m = Vec.Match(el.Element("size")?.Value ?? "(4,4)");
                t.w = int.Parse(m.Groups[1].Value);
                t.h = int.Parse(m.Groups[2].Value);
                if (el.Element("doorX") != null) t.doorX = int.Parse(el.Element("doorX")!.Value);
                foreach (var li in el.Element("things")?.Elements() ?? Enumerable.Empty<XElement>())
                {
                    var th = new Thing
                    {
                        role = li.Element("role")?.Value,
                        tier = li.Element("tier")?.Value,
                        rot = Rot(li.Element("rot")?.Value),
                        optional = li.Element("optional")?.Value != "false",
                        inWall = li.Element("inWall")?.Value == "true",
                        keepOnFloor = li.Element("keepOnFloor")?.Value == "true"
                    };
                    var pm = Vec.Match(li.Element("pos")?.Value ?? "(0,0)");
                    th.x = int.Parse(pm.Groups[1].Value);
                    th.z = int.Parse(pm.Groups[2].Value);
                    th.defs = li.Element("defs")?.Elements().Select(d => d.Value).ToList() ?? new List<string>();
                    t.things.Add(th);
                }
                templates.Add(t);
            }
        }
        return (templates, types);
    }

    /// <summary>The def the game would use: the first (or last) listed one we know.</summary>
    internal static string Pick(Thing th, bool fallback)
    {
        var known = th.defs.Where(facts.ContainsKey).ToList();
        if (known.Count == 0) return null;
        return fallback ? known[^1] : known[0];
    }

    public static int Run(string root, Action<bool, string> check)
    {
        var (templates, types) = Load(root);
        check(templates.Count >= 50, $"expected at least 50 room templates, found {templates.Count}");
        var byName = templates.ToDictionary(t => t.defName);
        var typeOf = new Dictionary<string, string>();
        foreach (var type in types)
        {
            string file = type.file, name = type.defName;
            check(type.variants.Count > 0, $"{file}: room type {name} has no variants");
            check(type.main == null || type.variants.Contains(type.main), $"{file}: room type {name}'s main {type.main} isn't one of its variants");
            foreach (var v in type.variants)
            {
                check(byName.ContainsKey(v), $"{file}: room type {name} lists {v}, which isn't a template");
                check(!typeOf.TryGetValue(v, out var other) || other == name, $"{file}: {v} is a size of both {other} and {name}");
                typeOf[v] = name;
            }
            // Menus show a room type under the group of the size they show, so all its sizes belong in one group.
            var groups = type.variants.Where(byName.ContainsKey).Select(v => byName[v].group).Distinct().ToList();
            check(groups.Count <= 1, $"{file}: room type {name} spans the groups {string.Join(", ", groups)}");
        }

        // A key piece the colony can't build is named in the planner by its role ("The colony can't build cribs yet").
        var roleKeys = RoleKeys(root);
        foreach (var t in templates)
            foreach (var th in t.things)
                if (!string.IsNullOrEmpty(th.role))
                    check(roleKeys.Contains(th.role), $"{t.file}: {t.defName} uses the role {th.role}, which has no CC_Role_{th.role} label");

        int checkedLayouts = 0;
        foreach (var t in templates)
        {
            foreach (var th in t.things)
            {
                check(Pick(th, false) != null, $"{t.file}: {t.defName} slot ({th.x},{th.z}) [{string.Join(",", th.defs)}]: no known def (add facts to the tests)");
                // A def name that's listed but unknown to the tests is fine only for mod alternatives after a known one.
            }
            foreach (bool fallback in new[] { false, true })
            {
                // No wall lamp the colony can build (the plan's floor lights), then a strong wall lamp and a weak one.
                foreach (float reach in WallLampTests.Reaches)
                {
                    for (int style = 0; style <= 2; style++)
                    {
                        var (pieces, notes) = Arrange(t, th => Pick(th, fallback), style, TemplateLayout.Lavish, reach);
                        string where = $"{t.file}: {t.defName} ({(fallback ? "fallback" : "best")} furniture, style {style}{WallLampTests.Label(reach)})";
                        foreach (var n in notes) check(false, $"{where}: layout had to change: {n}");
                        WallLampTests.CheckLamps(t, pieces, th => Pick(th, fallback), style, reach, where, check);
                        for (int mirror = 0; mirror < 2; mirror++)
                            for (int turn = 0; turn < 4; turn++)
                            {
                                CheckWorld(t, pieces, mirror == 1, turn, $"{where} turn {turn}{(mirror == 1 ? " mirrored" : "")}", check);
                                checkedLayouts++;
                            }
                    }
                }
            }
        }

        // Without the DLCs and mods a template doesn't require: their furniture is left out (or the next listed def is
        // used), and the room still works. A room whose key piece is left with nothing is unavailable, with a reason.
        int baseLayouts = 0, unavailable = 0;
        foreach (var t in templates)
        {
            string Present(Thing th) => th.defs.FirstOrDefault(d => facts.ContainsKey(d) && (SourceOf(d) is not string s || t.requires.Contains(s)));
            var missingKey = t.things.Where(th => !th.optional && Present(th) == null).ToList();
            if (missingKey.Count > 0)
            {
                unavailable++;
                foreach (var th in missingKey)
                    check(!string.IsNullOrEmpty(th.role) && roleKeys.Contains(th.role),
                        $"{t.file}: {t.defName} slot ({th.x},{th.z}) can be missing its furniture, and has no role to name it by");
                continue;
            }
            foreach (float reach in WallLampTests.Reaches)
            {
                for (int style = 0; style <= 2; style++)
                {
                    var (pieces, notes) = Arrange(t, Present, style, TemplateLayout.Lavish, reach);
                    string where = $"{t.file}: {t.defName} (without the furniture of unrequired DLCs and mods, style {style}{WallLampTests.Label(reach)})";
                    foreach (var n in notes) check(false, $"{where}: layout had to change: {n}");
                    WallLampTests.CheckLamps(t, pieces, Present, style, reach, where, check);
                    for (int mirror = 0; mirror < 2; mirror++)
                        for (int turn = 0; turn < 4; turn++)
                        {
                            CheckWorld(t, pieces, mirror == 1, turn, $"{where} turn {turn}{(mirror == 1 ? " mirrored" : "")}", check);
                            baseLayouts++;
                        }
                }
            }
        }
        Console.WriteLine($"Checked {baseLayouts} room layouts with missing DLC and mod furniture left out ({unavailable} rooms wait for their key piece).");
        int themed = RoomThemeTests.Run(root, templates, check);
        Console.WriteLine($"Checked {themed} room layouts in a theme, with and without the DLC and mod furniture.");
        Console.WriteLine($"Checked {WallLampTests.Run(templates, check)} things about wall lamps (which light for which room, how many, obstacles).");
        return checkedLayouts;
    }

    /// <summary>The CC_Role_ labels in the English keyed files.</summary>
    internal static HashSet<string> RoleKeys(string root)
    {
        var keys = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements())
                if (el.Name.LocalName.StartsWith("CC_Role_", StringComparison.Ordinal)) keys.Add(el.Name.LocalName.Substring("CC_Role_".Length));
        return keys;
    }

    public static (List<(TemplateLayout.Piece piece, Fact fact)> pieces, List<string> notes) Arrange(Template t, bool fallback, int style) =>
        Arrange(t, th => Pick(th, fallback), style);

    /// <summary>
    /// Lays out a template with the def <paramref name="pick"/> chooses for each piece (null: left out), as the game's
    /// Resolve does: arranged, then (when the colony can build a wall lamp lighting <paramref name="lampReach"/> cells) its
    /// plain lights hung on the walls by the room's size, then the decor.
    /// </summary>
    public static (List<(TemplateLayout.Piece piece, Fact fact)> pieces, List<string> notes) Arrange(Template t, Func<Thing, string> pick, int style,
        int decorFrom = TemplateLayout.Lavish, float lampReach = 0f, string lampName = "WallLamp")
    {
        var list = new List<(TemplateLayout.Piece, Fact)>();
        var hangable = new List<(Thing th, TemplateLayout.Piece piece)>();
        for (int i = 0; i < t.things.Count; i++)
        {
            var th = t.things[i];
            if (!TemplateLayout.InStyle(th.tier, th.role, th.optional, style)) continue;
            var name = pick(th);
            // Wall lamps never stand where the plan puts floor furniture.
            if (name != null && facts[name].attached) name = null;
            if (name == null)
            {
                if (th.MayHang(null)) hangable.Add((th, null));
                continue;
            }
            var f = facts[name];
            var p = new TemplateLayout.Piece
            {
                slot = i,
                x = th.x,
                z = th.z,
                rot = th.rot,
                sizeX = f.sx,
                sizeZ = f.sz,
                rotatable = f.rotatable,
                passable = f.pass != "",
                optional = th.optional,
                inWall = th.inWall,
                isDoor = f.kind == "door",
                isWall = f.kind == "wall",
                role = name
            };
            p.spots.AddRange(f.spots);
            list.Add((p, f));
            if (th.MayHang(name)) hangable.Add((th, p));
        }
        var notes = TemplateLayout.Arrange(t.w, t.h, t.DoorX, list.Select(x => x.Item1).ToList());
        if (lampReach > 0f && hangable.Count > 0)
        {
            var lights = hangable.Select(h => (h.piece, h.th.x, h.th.z)).ToList();
            bool floorPlaced = lights.Any(l => l.piece != null && !l.piece.dropped);
            if (LightMath.UseWallLamps(t.w * t.h, true, floorPlaced, LightMath.Auto))
            {
                var plan = LightMath.LightWithWallLamps(t.w, t.h, t.DoorX, list.Select(x => x.Item1).ToList(), lights, lampReach);
                foreach (var s in plan.Lamps)
                {
                    var lamp = new TemplateLayout.Piece
                    {
                        x = s.x, z = s.z, minX = s.x, minZ = s.z, w = 1, h = 1, rot = s.facing, facing = s.facing,
                        passable = true, role = lampName + " (on the wall)"
                    };
                    list.Add((lamp, facts[lampName]));
                }
            }
        }
        if (ThemeMath.AddsDecor(style, decorFrom))
        {
            // The plant pots the lavish style (or a theme that wants plants) adds must leave everything reachable.
            var live = list.Select(x => x.Item1).Where(p => !p.dropped).ToList();
            var spots = TemplateLayout.DecorSpots(t.w, t.h, t.DoorX, live, 4);
            foreach (var (x, z) in spots)
                if (live.Any(p => p.role.EndsWith("(on the wall)") && p.Contains(x, z))) notes.Add($"a plant pot stands under a wall lamp at ({x},{z})");
            foreach (var (x, z) in spots)
            {
                var pot = new TemplateLayout.Piece { x = x, z = z, minX = x, minZ = z, w = 1, h = 1, role = "PlantPot (decor)" };
                list.Add((pot, facts["PlantPot"]));
            }
            var all = list.Select(p => p.Item1).Where(p => !p.dropped).ToList();
            if (TemplateLayout.Unreachable(t.w, t.h, t.DoorX, all, null, out string problem) > 0) notes.Add("decor walls something in: " + problem);
        }
        return (list.Where(x => !x.Item1.dropped).ToList(), notes);
    }

    internal static void CheckWorld(Template t, List<(TemplateLayout.Piece piece, Fact fact)> pieces, bool mirror, int turn, string where, Action<bool, string> check)
    {
        int fullW = t.w + 2, fullH = t.h + 2;
        TemplateMath.RotatedSize(fullW, fullH, turn, out int rw, out int rh);
        // The interior in world (turned full-rect) coordinates is everything but the outer ring.
        bool Interior(int x, int z) => x >= 1 && z >= 1 && x <= rw - 2 && z <= rh - 2;
        var placed = new List<(int minX, int minZ, int w, int h, int facing, int rx, int rz, TemplateLayout.Piece p, Fact f)>();
        foreach (var (p, f) in pieces)
        {
            int off = p.inWall ? 0 : 1;
            bool ok = TemplateLayout.ToWorld(fullW, fullH, mirror, turn, p.minX + off, p.minZ + off, p.w, p.h, p.facing, p.rotatable, p.isDoor, p.sizeX, p.sizeZ,
                out int mx, out int mz, out int ww, out int wh, out int facing, out int rx, out int rz);
            check(ok, $"{where}: {p.role} can't be placed with facing {facing}");
            // The game's own footprint for that root and facing is the planned one.
            TemplateLayout.OccupiedRect(rx, rz, facing, p.sizeX, p.sizeZ, out int gx, out int gz, out int gw, out int gh);
            check(gx == mx && gz == mz && gw == ww && gh == wh, $"{where}: {p.role} footprint differs from the game's");
            placed.Add((mx, mz, ww, wh, facing, rx, rz, p, f));
        }
        bool Covered(int x, int z, out (int minX, int minZ, int w, int h, int facing, int rx, int rz, TemplateLayout.Piece p, Fact f) by)
        {
            foreach (var q in placed)
            {
                if (q.p.inWall) continue;
                if (x >= q.minX && x < q.minX + q.w && z >= q.minZ && z < q.minZ + q.h)
                {
                    by = q;
                    return true;
                }
            }
            by = default;
            return false;
        }
        bool WallAt(int x, int z) => !Interior(x, z) || (Covered(x, z, out var q) && q.f.kind == "wall");

        // The door, and the cell inside it.
        int doorFx = t.DoorX + 1;
        TemplateMath.Transform(doorFx, 0, fullW, fullH, mirror, turn, out int dx, out int dz);
        TemplateMath.Transform(doorFx, 1, fullW, fullH, mirror, turn, out int ix, out int iz);
        check(!Covered(ix, iz, out var blocker), $"{where}: the cell inside the door is taken by {blocker.p?.role}");
        int wantDoor = ix != dx ? 1 : 0;
        check(TemplateMath.DoorFacing(turn) == wantDoor, $"{where}: the door faces {TemplateMath.DoorFacing(turn)} in a wall that needs {wantDoor}");

        for (int i = 0; i < placed.Count; i++)
        {
            var a = placed[i];
            if (a.p.inWall)
            {
                // On the ring, not in a corner.
                for (int x = a.minX; x < a.minX + a.w; x++)
                    for (int z = a.minZ; z < a.minZ + a.h; z++)
                    {
                        bool ring = x == 0 || z == 0 || x == rw - 1 || z == rh - 1;
                        bool corner = (x == 0 || x == rw - 1) && (z == 0 || z == rh - 1);
                        check(ring && !corner, $"{where}: in-wall {a.p.role} isn't in a wall");
                    }
                continue;
            }
            for (int x = a.minX; x < a.minX + a.w; x++)
                for (int z = a.minZ; z < a.minZ + a.h; z++)
                    check(Interior(x, z), $"{where}: {a.p.role} sticks out of the room at ({x},{z})");
            for (int j = i + 1; j < placed.Count; j++)
            {
                var b = placed[j];
                if (b.p.inWall) continue;
                bool overlap = a.minX < b.minX + b.w && b.minX < a.minX + a.w && a.minZ < b.minZ + b.h && b.minZ < a.minZ + a.h;
                check(!overlap, $"{where}: {a.p.role} overlaps {b.p.role}");
            }
            // Work spots: inside, not under anything that blocks, reachable is checked in template space.
            foreach (var (ox, oz) in a.f.spots)
            {
                TemplateLayout.RotateOffset(ox, oz, a.facing, out int sx, out int sz);
                int px = a.rx + sx, pz = a.rz + sz;
                check(Interior(px, pz), $"{where}: {a.p.role}'s work spot ({px},{pz}) is in the wall");
                if (Covered(px, pz, out var on)) check(on.f.pass != "", $"{where}: {a.p.role}'s work spot is under {on.p.role}");
            }
            // A chair next to a table faces it.
            if (a.f.kind == "chair" && a.w == 1 && a.h == 1)
            {
                bool nextToTable = false, facesTable = false;
                for (int f = 0; f < 4; f++)
                {
                    TemplateLayout.Step(f, out int sx, out int sz);
                    if (Covered(a.minX + sx, a.minZ + sz, out var n) && n.f.kind == "table")
                    {
                        nextToTable = true;
                        if (f == a.facing) facesTable = true;
                    }
                }
                check(!nextToTable || facesTable, $"{where}: {a.p.role} at ({a.minX},{a.minZ}) is next to a table but faces {a.facing}");
            }
            // A bed's head (its root cell) is against a wall: the cell behind it, opposite its facing.
            if (a.f.kind == "bed")
            {
                TemplateLayout.Step(TemplateMath.Norm(a.facing + 2), out int bx, out int bz);
                bool against = true;
                // Every sleeping slot of the head row.
                for (int x = a.minX; x < a.minX + a.w; x++)
                    for (int z = a.minZ; z < a.minZ + a.h; z++)
                    {
                        bool headRow = a.facing switch
                        {
                            0 => z == a.minZ,
                            2 => z == a.minZ + a.h - 1,
                            1 => x == a.minX,
                            _ => x == a.minX + a.w - 1
                        };
                        if (headRow && !WallAt(x + bx, z + bz)) against = false;
                    }
                check(against, $"{where}: {a.p.role} at ({a.minX},{a.minZ}) facing {a.facing} has its head away from the wall");
            }
            // A wall lamp stands in front of a wall and faces it (the game looks for its wall at cell + facing): a wall of the
            // ring or a partition, never the door, an inner door or a piece set in the wall, never a corner; and it is not
            // beside a door.
            if (a.f.kind == "walllight")
            {
                TemplateLayout.Step(a.facing, out int sx, out int sz);
                int wx = a.rx + sx, wz = a.rz + sz;
                bool ring = !Interior(wx, wz);
                bool partition = Covered(wx, wz, out var on) && on.f.kind == "wall";
                check(Interior(a.rx, a.rz), $"{where}: {a.p.role} at ({a.rx},{a.rz}) isn't on the floor");
                check(ring || partition, $"{where}: {a.p.role} at ({a.rx},{a.rz}) faces {a.facing}, away from any wall");
                check(!(wx == dx && wz == dz), $"{where}: {a.p.role} at ({a.rx},{a.rz}) hangs on the door");
                bool corner = (wx == 0 || wx == rw - 1) && (wz == 0 || wz == rh - 1);
                check(!corner && wx >= 0 && wz >= 0 && wx < rw && wz < rh, $"{where}: {a.p.role} at ({a.rx},{a.rz}) hangs on a corner");
                foreach (var q in placed)
                {
                    bool inQ = wx >= q.minX && wx < q.minX + q.w && wz >= q.minZ && wz < q.minZ + q.h;
                    if (inQ && (q.p.inWall || q.f.kind == "door")) check(false, $"{where}: {a.p.role} at ({a.rx},{a.rz}) hangs on {q.p.role}");
                }
                for (int f = 0; f < 4; f++)
                {
                    TemplateLayout.Step(f, out int nx, out int nz);
                    bool door = (a.rx + nx == dx && a.rz + nz == dz) || (Covered(a.rx + nx, a.rz + nz, out var n) && n.f.kind == "door");
                    check(!door, $"{where}: {a.p.role} at ({a.rx},{a.rz}) is beside a door");
                }
            }
            // A door in a partition faces the way of its wall.
            if (a.f.kind == "door")
            {
                bool ewWall = WallAt(a.minX - 1, a.minZ) && WallAt(a.minX + 1, a.minZ);
                bool nsWall = WallAt(a.minX, a.minZ - 1) && WallAt(a.minX, a.minZ + 1);
                if (ewWall != nsWall) check(a.facing == (ewWall ? 0 : 1), $"{where}: inner door faces {a.facing} in a {(ewWall ? "west-east" : "north-south")} wall");
            }
        }
    }
}
