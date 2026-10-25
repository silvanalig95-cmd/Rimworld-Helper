using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The defense planner (Source/ColonyCouncil/Defense): every defense template follows the rules in every turn and
/// mirrored (no overlapping pieces, gates in wall lines with a way through, every firing position covered toward the
/// threat, or in a pocket it can lean out of, and the cover open to the front, traps on the raiders' way and never
/// beside each other or a gate, turrets three cells apart, a closing line wall all the way across); every drawn one
/// (DefenseShapes) does so for a range of colonies, and holds up the way raids play out (DefenseSight: nobody can shoot
/// at a firing position from good cover, out of a grenade's throw against grenades, turrets out of each other's blast);
/// bad layouts are caught, the old killbox's blind spot among them; the map reading finds the ways in, the narrow
/// places on them with their widths and facings, what closes them (rock, water, walls, a bridge), and whether a colony
/// is walled in, on small maps made of characters.
/// </summary>
public static class DefenseTests
{
    private static readonly HashSet<string> Kinds = new() { "gate", "wall", "killbox", "traps", "nest", "post", "fallback", "keep", "mortar", "palisade" };
    private static readonly HashSet<string> Places = new() { "choke", "front", "behind", "between", "core" };

    public class Template
    {
        public string defName, kind, placeAt;
        public int line = -1;
        public List<string> rows = new();
        public List<string> roles = new();
        /// <summary>Its &lt;shape&gt;, read the way the game reads it (fields by name), or null for a fixed layout.</summary>
        public DefenseShapeOptions shape;
    }

    /// <summary>A &lt;shape&gt; element's fields, as RimWorld's loader fills them.</summary>
    public static DefenseShapeOptions ReadShape(XElement el)
    {
        if (el == null) return null;
        var o = new DefenseShapeOptions();
        foreach (var child in el.Elements())
        {
            var f = typeof(DefenseShapeOptions).GetField(child.Name.LocalName);
            if (f == null) throw new InvalidDataException("unknown shape field " + child.Name.LocalName);
            object v = f.FieldType == typeof(bool) ? bool.Parse(child.Value) : f.FieldType == typeof(int) ? int.Parse(child.Value) : child.Value;
            f.SetValue(o, v);
        }
        return o;
    }

    public static List<Template> Load(string root)
    {
        var list = new List<Template>();
        string dir = Path.Combine(root, "Defs", "DefenseTemplateDefs");
        if (!Directory.Exists(dir)) return list;
        foreach (var file in Directory.GetFiles(dir, "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements("ColonyCouncil.DefenseTemplateDef"))
            {
                var t = new Template
                {
                    defName = el.Element("defName")?.Value ?? "?",
                    kind = el.Element("kind")?.Value ?? "",
                    placeAt = el.Element("placeAt")?.Value ?? "choke",
                    rows = el.Element("layout")?.Elements("li").Select(e => e.Value).ToList() ?? new List<string>(),
                    shape = ReadShape(el.Element("shape"))
                };
                if (int.TryParse(el.Element("line")?.Value, out int line)) t.line = line;
                foreach (var p in el.Element("pieces")?.Elements("li") ?? Enumerable.Empty<XElement>())
                    t.roles.Add(p.Element("role")?.Value ?? "");
                list.Add(t);
            }
        return list;
    }

    public static int Run(string root, Action<bool, string> check)
    {
        int checks = 0;
        void Check(bool ok, string what)
        {
            checks++;
            check(ok, "defense: " + what);
        }

        // ---------- The templates ----------
        var templates = Load(root);
        Check(templates.Count >= 10, $"found the defense templates ({templates.Count})");
        foreach (var kind in new[] { "gate", "wall", "killbox", "traps", "nest", "post", "fallback", "keep", "mortar", "palisade" })
            Check(templates.Any(t => t.kind == kind), $"a template of kind {kind}");
        int layouts = 0;
        foreach (var t in templates)
        {
            Check(Kinds.Contains(t.kind), $"{t.defName}: unknown kind {t.kind}");
            Check(Places.Contains(t.placeAt), $"{t.defName}: unknown placeAt {t.placeAt}");
            foreach (var r in t.roles) Check(DefenseLayout.Roles.Contains(r), $"{t.defName}: unknown role {r}");
            if (t.shape != null) Check(DefenseShapes.Families.Contains(t.shape.family), $"{t.defName}: unknown shape family {t.shape.family}");
            // A drawn plan's fixed layout is only what plans placed before were built from (a new one may have none).
            if (t.shape != null && t.rows.Count == 0) continue;
            var g = DefenseLayout.Parse(t.rows, t.line);
            foreach (var p in DefenseLayout.Problems(g, t.kind)) Check(false, $"{t.defName}: {p}");
            // A plan that closes a gap and brings its own cover puts the shooters where a line's review wants them, not
            // so close behind the gate that the first raider through reaches them.
            if (t.placeAt == "choke" && g.line >= 0)
                for (int x = 0; x < g.W; x++)
                    for (int z = 0; z < g.line; z++)
                        if (g.cells[x, z] == DefenseLayout.Firing && g.At(x, z + 1) == DefenseLayout.Cover)
                            Check(DefenseLineMath.ReachOf(g.line - z, 0f) != DefenseLineMath.Reach.TooClose,
                                $"{t.defName}: the firing position at ({x},{z}) is {g.line - z} cells behind the gate, too close");
            for (int m = 0; m < 2; m++)
                for (int turn = 0; turn < 4; turn++)
                {
                    WorldChecks(t, g, m == 1, turn, Check);
                    layouts++;
                }
        }
        Console.WriteLine($"Checked {layouts} defense layouts ({templates.Count} templates in every turn, mirrored and not).");
        DrawnChecks(templates, Check);

        // ---------- The rules catch bad layouts ----------
        void Bad(string why, string kind, int line, params string[] rows)
        {
            var g = DefenseLayout.Parse(rows, line);
            Check(DefenseLayout.Problems(g, kind).Count > 0, $"a layout with {why} is caught");
        }
        Bad("a gate in open ground", "nest", -1, "...", ".G.", "...");
        Bad("traps side by side", "traps", -1, "W..W", "WTTW", "W..W");
        Bad("a firing position without cover", "post", -1, ".....", "WfffW", "W...W");
        Bad("cover against a wall", "post", -1, "WWWWW", "WSSSW", "WfffW", "W...W");
        Bad("a killbox raiders can't get through", "killbox", 0, "WWWWW", "W...W", "WSSSW", "WfffW");
        Bad("a gate line raiders can walk round", "gate", 1, ".....", "WWG..", ".....");
        Bad("turrets side by side", "nest", -1, ".SSSS.", "WUuUuW", "W....W");
        Bad("a turret block missing its root", "nest", -1, ".SS.", "Wuu.", "Wuu.");
        Bad("a trap by a gate", "gate", 1, "..T..", "WWGWW", ".....");
        Bad("rows of different widths", "post", -1, ".SSS.", "WfffW", "W..W");
        Bad("an unknown character", "post", -1, ".SSS.", "WfQfW", "W...W");
        Bad("a firing position nobody can reach", "post", -1, ".SSS.", "WfffW", "WWWWW");
        Bad("a keep open at the side", "keep", -1, "WWGWW", "W.S..", "W.f.W", "WWWWW");
        Bad("mortars side by side", "mortar", -1, ".SSSS.", ".mmmm.", ".MmMm.", "......");
        Bad("a firing position walled in front and at both sides", "post", -1, ".....", "SWWWS", "WWfWW", "W...W");
        Bad("a layout too big", "post", -1, Enumerable.Repeat(".SSS.", 1).Concat(Enumerable.Repeat("WfffW", 1)).Concat(Enumerable.Repeat("W...W", 41)).ToArray());
        // A pocket passes: a wall in front, sandbags at its front corners and beside it.
        {
            var g = DefenseLayout.Parse(new[] { ".......", "SWSWSWS", "WfSfSfW", "W.....W" });
            Check(DefenseLayout.Problems(g, "post").Count == 0, "a post of pockets passes: " + string.Join("; ", DefenseLayout.Problems(g, "post")));
            Check(DefenseLayout.Pocket(g, 1, 1) && DefenseLayout.Pocket(g, 3, 1), "a firing position behind a wall with sandbags beside it is a pocket");
        }
        // The old killbox, as the sight checker reads it: the corner before its last turn sees the shooters from three-quarters cover.
        {
            var old = DefenseLayout.Parse(new[]
            {
                "WWWWWWW.WWWWWWW", "W_____W.W_____W", "W_____W.WWW___W", "W_____W...W___W", "WWWWWWWWW.WWWWW", "_W...........W_",
                "_W...........W_", "_W.......T...W_", "_W...........W_", "_W...........W_", "_W...........W_", "_W......T....W_",
                "_W...........W_", "_W...........W_", "_W...........W_", "_W...........W_", "_W...........W_", "_WSSSSS.SSSSSW_",
                "_WuuWff.ffWuuW_", "_WUuW.....WUuW_", "_W...........W_"
            }, 0);
            var reading = DefenseSight.Read(old);
            Check(reading.worstCover >= 0.7f && reading.worstZ >= old.H - 5, $"the old killbox's corner sees its shooters from cover ({reading.worstCover:0.00} at {reading.worstX},{reading.worstZ})");
            Check(DefenseSight.Problems(old, true).Count > 0, "the sight checker catches it");
        }

        // A good one passes.
        {
            var g = DefenseLayout.Parse(new[] { ".......", "WWWGWWW", ".......", ".SS.SS.", ".ff.ff." }, 1);
            Check(DefenseLayout.Problems(g, "gate").Count == 0, "a plain gate with sandbags passes: " + string.Join("; ", DefenseLayout.Problems(g, "gate")));
            Check(g.pieces.Count == 7 + 4 && g.Count("gate") == 1 && g.Count("cover") == 4, "its pieces are read");
            Check(g.line == 3 && g.At(3, 3) == 'G', "the line row counts from the top");
        }
        // Turret blocks: the root at the bottom-left, the size from the room marks.
        {
            var g = DefenseLayout.Parse(new[] { ".S.", "uu.", "Uu." });
            var tp = g.pieces.First(p => p.role == "turret");
            Check(tp.x == 0 && tp.z == 0 && tp.w == 2 && tp.h == 2, $"a 2x2 turret block ({tp.x},{tp.z} {tp.w}x{tp.h})");
        }

        // ---------- Facing a threat ----------
        {
            var g = DefenseLayout.Parse(new[] { ".........", "WWWWGWWWW", ".........", ".SSS.SSS.", ".fff.fff." }, 1);
            for (int facing = 0; facing < 4; facing++)
                foreach (bool mirror in new[] { false, true })
                {
                    DefenseLayout.FaceAt(g, 4, g.line, mirror, facing, 100, 50, out int turn, out int ax, out int az);
                    DefenseLayout.ToWorld(g, 4, g.line, mirror, turn, out int wx, out int wz);
                    Check(ax + wx == 100 && az + wz == 50, $"the gate lands on the gap (facing {facing}{(mirror ? ", mirrored" : "")})");
                    Check(DefenseLayout.Outward(mirror, turn) == facing, $"the template faces the threat (facing {facing})");
                    // The open ground in front of the gate lies toward the threat.
                    DefenseLayout.ToWorld(g, 4, g.line + 1, mirror, turn, out int fx, out int fz);
                    TemplateLayout.Step(facing, out int sx, out int sz);
                    Check(fx - wx == sx && fz - wz == sz, $"in front of the gate is toward the threat (facing {facing})");
                }
        }

        // ---------- Closing a gap ----------
        {
            int[] ground = { 0, 0, 0, 1 };
            int n = DefenseLayout.CarryLine(i => i < ground.Length ? ground[i] : 0, 12, out int stop);
            Check(n == 3 && stop == DefenseLayout.GapClosed, "the line carries on three cells to the rock");
            n = DefenseLayout.CarryLine(i => i == 1 ? DefenseLayout.GapGround : 0, 12, out stop);
            Check(n == 1 && stop == DefenseLayout.GapGround, "it stops at water");
            n = DefenseLayout.CarryLine(i => 0, 5, out stop);
            Check(n == 5 && stop == DefenseLayout.GapTooWide, "it gives up on a gap too wide");
            n = DefenseLayout.CarryLine(i => 1, 5, out stop);
            Check(n == 0 && stop == DefenseLayout.GapClosed, "nothing to add when the rock is right there");
        }

        MapChecks(Check);
        return checks;
    }

    /// <summary>
    /// Every drawn plan for a range of colonies (bows and rifles, with and against grenades, short and long reach,
    /// small and big raids, no turret, a mini-turret, an autocannon, the planner's depth taken away or added): the
    /// layout rules, what raiders can see and hit (nobody shoots at a firing position from better than 45% cover; against
    /// grenades, nobody within a throw of the doorway unless the colony's reach or the player's depth leaves no room;
    /// turrets out of each other's and the shooters' blast), a placed plan's numbers read back from the save, and the
    /// world checks in every turn for the colony in the middle of the range.
    /// </summary>
    private static void DrawnChecks(List<Template> templates, Action<bool, string> check)
    {
        var drawn = templates.Where(t => t.shape != null).ToList();
        check(drawn.Count >= 10, $"most plans are drawn for the colony ({drawn.Count})");
        var specs = new List<DefenseShapeSpec>();
        foreach (int shooters in new[] { 4, 6, 8 })
            foreach (bool grenades in new[] { false, true })
                foreach (bool lowTech in new[] { false, true })
                    foreach (float range in new[] { 22.9f, 30.9f, 44.9f })
                        foreach (float points in new[] { 300f, 4000f })
                            foreach (int turret in new[] { 0, 1, 2 })
                                foreach (int depth in new[] { -3, 0, 5 })
                                    specs.Add(new DefenseShapeSpec
                                    {
                                        shooters = shooters, grenades = grenades, lowTech = lowTech, range = range, raidPoints = points,
                                        turretSize = turret, turrets = turret == 0 ? 0 : turret == 1 ? 2 : 1, depth = depth
                                    });
        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
        int built = 0;
        foreach (var t in drawn)
        {
            var seen = new HashSet<string>();
            var unique = specs.Where(sp => seen.Add(sp.Key)).ToList();
            System.Threading.Tasks.Parallel.ForEach(unique, spec =>
            {
                var shape = DefenseShapes.Build(t.shape, spec);
                System.Threading.Interlocked.Increment(ref built);
                var g = shape.grid;
                string where = $"{t.defName} [{spec.Key}]";
                foreach (var p in DefenseLayout.Problems(g, t.kind)) failures.Add($"{where}: {p}");
                // Reach and depth can leave no room to stand out of a throw; then the review says so, and nothing else may be wrong.
                bool roomForGrenades = spec.depth >= 0 && spec.range >= 30f;
                foreach (var p in DefenseSight.Problems(g, spec.grenades && roomForGrenades)) failures.Add($"{where}: {p}");
                if (t.shape.family != DefenseShapes.Box && t.shape.family != DefenseShapes.SideLines) return;
                int firing = DefenseSight.Firing(g).Count;
                if (firing < 4) failures.Add($"{where}: only {firing} firing positions");
                if (t.placeAt == "choke" && g.line < 0) failures.Add($"{where}: no wall line");
            });
        }
        Console.WriteLine($"Drew {built} defense plans ({drawn.Count} drawn templates for up to {specs.Count} colonies) and checked each by the layout rules and by what raiders can see and hit.");
        foreach (var f in failures.OrderBy(x => x).Take(40)) check(false, "drawn: " + f);
        check(failures.IsEmpty, $"every drawn plan holds for every colony ({built} drawn, {failures.Count} problems)");

        // What a box is drawn as, for numbers that matter.
        var killbox = drawn.FirstOrDefault(t => t.defName == "Rimfolk_Defense_Killbox");
        if (killbox != null)
        {
            var rifles = DefenseShapes.Build(killbox.shape, new DefenseShapeSpec { shooters = 6, grenades = true, range = 30.9f, turretSize = 1, turrets = 2 });
            check(rifles.straight >= 5 && rifles.nearest >= DefenseSight.GrenadeReach, $"a rifle killbox against grenades: a straight of five or more ({rifles.straight}), shooters out of a throw ({rifles.nearest:0.0})");
            check(rifles.grid.Count("turret") == 2, "two decoy mini-turrets in it");
            var big = DefenseShapes.Build(killbox.shape, new DefenseShapeSpec { shooters = 8, grenades = true, range = 30.9f, raidPoints = 4000f, turretSize = 2, turrets = 1 });
            var bigTurret = big.grid.pieces.FirstOrDefault(p => p.role == "turret");
            check(bigTurret != null && bigTurret.w == 2 && bigTurret.z < DefenseSight.Firing(big.grid).Min(f => f.z), "an autocannon stands behind the line, not in the box");
            check(big.grid.Count("slow") >= 2, $"a big raid's way in is slowed ({big.grid.Count("slow")})");
            var bows = DefenseShapes.Build(killbox.shape, new DefenseShapeSpec { shooters = 4, lowTech = true, range = 22.9f });
            check(bows.depth < rifles.depth && bows.grid.Count("cover") > 0 && bows.grid.Count("turret") == 0, "a tribal killbox is shallower, behind barricades, without turrets");
            var deeper = DefenseShapes.Build(killbox.shape, new DefenseShapeSpec { shooters = 6, grenades = true, range = 30.9f, turretSize = 1, turrets = 2, depth = 4 });
            var wider = DefenseShapes.Build(killbox.shape, new DefenseShapeSpec { shooters = 4, grenades = true, range = 30.9f });
            // Further back, the corner sees a wider strip down the straight: the straight grows with it.
            check(deeper.depth == rifles.depth + 4 && deeper.grid.H >= rifles.grid.H + 4 && deeper.straight >= rifles.straight,
                $"four rows more depth draw it four rows deeper ({rifles.depth} to {deeper.depth}; straight {rifles.straight} to {deeper.straight})");
            check(wider.grid.W < rifles.grid.W && wider.shooters == 4 && rifles.shooters == 6, "fewer shooters draw it narrower");
        }
        var gate = drawn.FirstOrDefault(t => t.defName == "Rimfolk_Defense_Gate");
        if (gate != null)
        {
            var plain = DefenseShapes.Build(gate.shape, new DefenseShapeSpec { shooters = 6, range = 30.9f });
            var grenade = DefenseShapes.Build(gate.shape, new DefenseShapeSpec { shooters = 6, range = 30.9f, grenades = true });
            check(grenade.nearest >= DefenseSight.GrenadeReach && plain.nearest < grenade.nearest, $"side lines stand further off against grenades ({plain.nearest:0.0} to {grenade.nearest:0.0})");
            check(DefenseSight.Read(plain.grid).worstCover <= 0.3f, "nobody outside the gate sees the side lines, and the gate gives little cover");
        }
        var traps = drawn.FirstOrDefault(t => t.shape.family == DefenseShapes.Traps);
        if (traps != null)
        {
            var few = DefenseShapes.Build(traps.shape, new DefenseShapeSpec { raidPoints = 300f });
            var many = DefenseShapes.Build(traps.shape, new DefenseShapeSpec { raidPoints = 6000f });
            check(few.grid.Count("trap") == 4 && many.grid.Count("trap") == 8, $"four traps for a small raid, eight for the biggest ({few.grid.Count("trap")}, {many.grid.Count("trap")})");
            bool beside = few.grid.pieces.Where(p => p.role == "trap").All(p => few.grid.At(p.x + 1, p.z) == DefenseLayout.Slow || few.grid.At(p.x - 1, p.z) == DefenseLayout.Slow);
            check(beside, "a fence beside each trap");
        }
        // A placed plan's numbers, kept in the save and read back.
        {
            var spec = new DefenseShapeSpec { shooters = 7, depth = -2, turretSize = 2, turrets = 1, grenades = true, lowTech = false, range = 36.9f, raidPoints = 2750f };
            var back = DefenseShapeSpec.Parse(spec.Save());
            check(back != null && back.Save() == spec.Save() && back.Key == spec.Key, $"a plan's numbers read back from the save ({spec.Save()})");
            check(DefenseShapeSpec.Parse(null) == null && DefenseShapeSpec.Parse("1,2") == null && DefenseShapeSpec.Parse("a,b,c,d,e,f,g,h") == null, "a plan placed before, or numbers that can't be read, give none");
        }
        // In every turn, mirrored and not, for the colony in the middle of the range.
        var middle = new DefenseShapeSpec { shooters = 6, grenades = true, range = 30.9f, raidPoints = 1500f, turretSize = 1, turrets = 2 };
        foreach (var t in drawn)
        {
            var g = DefenseShapes.Build(t.shape, middle).grid;
            for (int m = 0; m < 2; m++)
                for (int turn = 0; turn < 4; turn++) WorldChecks(t, g, m == 1, turn, check);
        }
    }

    /// <summary>The rules again in world space, after the template is mirrored and turned, and every piece's footprint and facing as the game builds it.</summary>
    private static void WorldChecks(Template t, DefenseLayout.Grid g, bool mirror, int turn, Action<bool, string> check)
    {
        string where = $"{t.defName} turn {turn}{(mirror ? " mirrored" : "")}";
        var world = new Dictionary<(int, int), char>();
        TemplateMath.RotatedSize(g.W, g.H, turn, out int rw, out int rh);
        bool inside = true;
        for (int x = 0; x < g.W; x++)
            for (int z = 0; z < g.H; z++)
            {
                DefenseLayout.ToWorld(g, x, z, mirror, turn, out int wx, out int wz);
                if (wx < 0 || wz < 0 || wx >= rw || wz >= rh) inside = false;
                world[(wx, wz)] = g.cells[x, z];
            }
        check(inside && world.Count == g.W * g.H, $"{where}: every cell lands once inside the turned rect");
        int outF = DefenseLayout.Outward(mirror, turn);
        TemplateLayout.Step(outF, out int ox, out int oz);
        // Left and right of the threat.
        TemplateLayout.Step(TemplateMath.Norm(outF + 1), out int px, out int pz);
        char At(int x, int z) => world.TryGetValue((x, z), out var c) ? c : '\0';
        foreach (var kv in world)
        {
            var (x, z) = kv.Key;
            char c = kv.Value;
            char front = At(x + ox, z + oz), back = At(x - ox, z - oz);
            if (c == DefenseLayout.Firing)
            {
                // Cover in front, or a pocket: a wall in front and, on one side, room to lean out past it.
                bool LeanInto(char ch) => ch == '\0' || !DefenseLayout.Solid(ch) && ch != DefenseLayout.Gate;
                bool pocket = front == DefenseLayout.Wall && (LeanInto(At(x + px, z + pz)) && LeanInto(At(x + px + ox, z + pz + oz))
                                                              || LeanInto(At(x - px, z - pz)) && LeanInto(At(x - px + ox, z - pz + oz)));
                check(front == DefenseLayout.Cover || front == DefenseLayout.Embrasure || pocket, $"{where}: firing position at ({x},{z}) covered toward the threat");
            }
            if ((c == DefenseLayout.Cover || c == DefenseLayout.Embrasure) && "fUuMm".IndexOf(back) >= 0)
                check(front == '\0' || front == '.' || front == '_' || front == 'T' || front == 'F', $"{where}: cover at ({x},{z}) faces outward onto open ground");
            if (c == DefenseLayout.Trap)
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                        if ((dx != 0 || dz != 0) && At(x + dx, z + dz) == DefenseLayout.Trap) check(false, $"{where}: traps beside each other at ({x},{z})");
            if (c == DefenseLayout.Gate)
            {
                bool across = DefenseLayout.InLine(At(x + px, z + pz)) && DefenseLayout.InLine(At(x - px, z - pz));
                bool along = DefenseLayout.InLine(At(x + ox, z + oz)) && DefenseLayout.InLine(At(x - ox, z - oz));
                check(across || along, $"{where}: the gate at ({x},{z}) stands in a wall");
                // The game turns a door to the wall it stands in: north in a wall running east-west, east in one running north-south.
                bool wallRunsX = DefenseLayout.InLine(At(x + 1, z)) && DefenseLayout.InLine(At(x - 1, z));
                int templateFacing = across ? 0 : 1;
                TemplateLayout.ToWorld(g.W, g.H, mirror, turn, 0, 0, 1, 1, templateFacing, false, true, 1, 1, out _, out _, out _, out _, out int doorFacing, out _, out _);
                check(wallRunsX ? doorFacing == 0 : doorFacing == 1, $"{where}: the gate at ({x},{z}) is turned with its wall (faces {doorFacing})");
            }
        }
        // Turrets and mortars: the game finds a root for the block's footprint without turning them (they only face north).
        foreach (var p in g.pieces)
        {
            if (p.w == 1 && p.h == 1) continue;
            bool ok = TemplateLayout.ToWorld(g.W, g.H, mirror, turn, p.x, p.z, p.w, p.h, 0, false, false, p.w, p.h,
                out int mx, out int mz, out int ww, out int wh, out int facing, out int rx, out int rz);
            check(ok && facing == 0, $"{where}: the {p.role} block at ({p.x},{p.z}) has a root facing north");
            var cells = new HashSet<(int, int)>();
            for (int i = p.x; i < p.x + p.w; i++)
                for (int j = p.z; j < p.z + p.h; j++)
                {
                    DefenseLayout.ToWorld(g, i, j, mirror, turn, out int wx, out int wz);
                    cells.Add((wx, wz));
                }
            check(cells.All(cl => cl.Item1 >= mx && cl.Item1 < mx + ww && cl.Item2 >= mz && cl.Item2 < mz + wh) && cells.Count == ww * wh,
                $"{where}: the {p.role} block at ({p.x},{p.z}) keeps its footprint");
        }
    }

    // ---------------- Reading a map ----------------

    /// <summary>
    /// A map from rows of characters, the top row north: # rock, ~ deep water, W wall, D door, = bridge, x something
    /// else in the way, ? unknown, h home, c where people live (home too), . open ground.
    /// </summary>
    public static DefenseMath.MapGrid Map(params string[] rows)
    {
        int h = rows.Length, w = rows[0].Length;
        var m = new DefenseMath.MapGrid(w, h);
        long cx = 0, cz = 0, cn = 0;
        for (int r = 0; r < h; r++)
        {
            int z = h - 1 - r;
            for (int x = 0; x < w; x++)
            {
                int i = m.Index(x, z);
                char c = rows[r][x];
                m.kind[i] = c switch
                {
                    '#' => DefenseMath.Rock,
                    '~' => DefenseMath.Water,
                    'W' => DefenseMath.Wall,
                    'D' => DefenseMath.Door,
                    '=' => DefenseMath.Bridge,
                    'x' => DefenseMath.Other,
                    '?' => DefenseMath.Unknown,
                    _ => DefenseMath.Open
                };
                if (c == 'h' || c == 'c') m.home[i] = true;
                if (c == 'c')
                {
                    m.core[i] = true;
                    cx += x;
                    cz += z;
                    cn++;
                }
            }
        }
        if (cn > 0)
        {
            m.centerX = (int)(cx / cn);
            m.centerZ = (int)(cz / cn);
        }
        return m;
    }

    /// <summary>A map of <paramref name="w"/> x <paramref name="h"/> filled by a function of (x, z) giving the character.</summary>
    public static DefenseMath.MapGrid Map(int w, int h, Func<int, int, char> at)
    {
        var rows = new string[h];
        for (int r = 0; r < h; r++)
        {
            int z = h - 1 - r;
            var chars = new char[w];
            for (int x = 0; x < w; x++) chars[x] = at(x, z);
            rows[r] = new string(chars);
        }
        return Map(rows);
    }

    private static void MapChecks(Action<bool, string> check)
    {
        // A valley five wide from the north edge down to a meadow closed in by rock.
        {
            var m = Map(40, 40, (x, z) =>
            {
                if (x >= 18 && x <= 22 && z >= 25) return '.';
                if (x >= 5 && x <= 34 && z >= 5 && z <= 24)
                {
                    if (x >= 18 && x <= 22 && z >= 10 && z <= 12) return 'c';
                    if (x >= 15 && x <= 25 && z >= 8 && z <= 14) return 'h';
                    return '.';
                }
                return '#';
            });
            var r = DefenseMath.Analyse(m);
            check(r.sideReach[0] == 5 && r.sideReach[1] == 0 && r.sideReach[2] == 0 && r.sideReach[3] == 0, $"valley: raiders reach home only from the north edge ({string.Join(",", r.sideReach)})");
            check(!r.enclosed && !r.unreachable, "valley: not walled in, reachable");
            check(r.chokes.Count >= 1, "valley: a choke point is found");
            if (r.chokes.Count >= 1)
            {
                var c = r.chokes[0];
                check(c.width == 5, $"valley: the pass is 5 wide (found {c.width})");
                check(c.facing == 0, $"valley: the pass faces north (found {c.facing})");
                check(c.share > 0.99f, $"valley: every raid passes it ({c.share:0.00})");
                check(c.endA == DefenseMath.Rock && c.endB == DefenseMath.Rock, "valley: rock on both sides");
                check(c.x >= 18 && c.x <= 22 && c.z >= 25, $"valley: the gap is in the pass ({c.x},{c.z})");
                check(c.sides == 1, "valley: the raids passing it come from the north");
            }
            check(r.concentration > 0.8f && DefenseMath.Compass(r.angle) == 0, $"valley: they come from the north ({r.angle:0}°, {r.concentration:0.00})");
            check(r.openShare < 0.01f, "valley: no way in through open country");
            check(r.shown.Count > 0 && r.shown.All(rt => r.dist[rt.cells[^1]] == 0 && rt.side == 0), "valley: the routes shown end at home and start in the north");
        }

        // Open country all round: no choke points, raids from every side.
        {
            var m = Map(40, 40, (x, z) => x >= 17 && x <= 23 && z >= 17 && z <= 23 ? (x == 20 && z == 20 ? 'c' : 'h') : '.');
            var r = DefenseMath.Analyse(m);
            check(r.sideReach.All(s => s > 0), "open: every side reaches home");
            check(r.chokes.Count == 0, $"open: no choke points ({r.chokes.Count})");
            check(r.concentration < 0.2f, $"open: no one direction ({r.concentration:0.00})");
            check(r.openShare > 0.99f, "open: all of it open country");
            check(!r.enclosed && r.openings == 1, $"open: home is open, as one opening all round ({r.openings})");
        }

        // A colony walled in with one door to the north: walled in, and the door is the narrow place.
        {
            var m = Map(40, 40, (x, z) =>
            {
                bool ring = (x == 12 || x == 27) && z >= 12 && z <= 27 || (z == 12 || z == 27) && x >= 12 && x <= 27;
                if (ring) return x == 19 && z == 27 ? 'D' : 'W';
                if (x > 12 && x < 27 && z > 12 && z < 27) return x == 19 && z == 19 ? 'c' : 'h';
                if (x >= 10 && x <= 29 && z >= 10 && z <= 29) return 'h';
                return '.';
            });
            var r = DefenseMath.Analyse(m);
            check(r.enclosed, "walled: the colony is walled in");
            check(r.openings == 0, "walled: no opening without a door");
            check(r.chokes.Count >= 1 && r.chokes[0].width == 1 && r.chokes[0].doors == 1, "walled: the door is the way in");
            if (r.chokes.Count >= 1)
            {
                var c = r.chokes[0];
                check(c.x == 19 && c.z == 27, $"walled: at the door ({c.x},{c.z})");
                check(c.facing == 0 && c.endA == DefenseMath.Wall && c.endB == DefenseMath.Wall, $"walled: facing north between walls (facing {c.facing})");
                check(c.share > 0.99f, "walled: every raid comes through it");
            }
            // Open the wall on the south side: no longer walled in, and that opening is where they come in.
            var open = Map(40, 40, (x, z) =>
            {
                bool ring = (x == 12 || x == 27) && z >= 12 && z <= 27 || (z == 12 || z == 27) && x >= 12 && x <= 27;
                if (ring) return x == 19 && z == 27 ? 'D' : x >= 19 && x <= 21 && z == 12 ? '.' : 'W';
                if (x > 12 && x < 27 && z > 12 && z < 27) return x == 19 && z == 19 ? 'c' : 'h';
                return '.';
            });
            var r2 = DefenseMath.Analyse(open);
            check(!r2.enclosed, "breach: a gap in the wall means not walled in");
            check(r2.chokes.Any(c => c.width == 3 && c.facing == 2), $"breach: the gap in the south wall is a choke point 3 wide facing south ({string.Join("; ", r2.chokes.Select(c => c.width + "/" + c.facing))})");
            check(r2.chokes.Count > 0 && r2.chokes[0].width == 3, "breach: the open gap takes the raids, not the door");
        }

        // A house with a bedroom on open ground: its door doesn't wall the colony in. Round it a perimeter wall with a gate does.
        {
            char House(int x, int z, bool perimeter)
            {
                bool houseRing = (x == 20 || x == 26) && z >= 20 && z <= 26 || (z == 20 || z == 26) && x >= 20 && x <= 26;
                if (houseRing) return x == 23 && z == 20 ? 'D' : 'W';
                if (x > 20 && x < 26 && z > 20 && z < 26) return 'c';
                if (perimeter)
                {
                    bool ring = (x == 12 || x == 37) && z >= 12 && z <= 37 || (z == 12 || z == 37) && x >= 12 && x <= 37;
                    if (ring) return x == 24 && z == 37 ? 'D' : 'W';
                }
                return x >= 10 && x <= 39 && z >= 10 && z <= 39 ? 'h' : '.';
            }
            var open = DefenseMath.Analyse(Map(50, 50, (x, z) => House(x, z, false)));
            check(!open.enclosed, "house: a house on open ground isn't walled in");
            var walled = DefenseMath.Analyse(Map(50, 50, (x, z) => House(x, z, true)));
            check(walled.enclosed, "house: inside a perimeter wall with a gate, it is");
            check(walled.chokes.Count > 0 && walled.chokes[0].doors == 1 && walled.chokes[0].x == 24 && walled.chokes[0].z == 37 && walled.chokes[0].facing == 0,
                $"house: the perimeter gate is the way in, facing north ({(walled.chokes.Count > 0 ? walled.chokes[0].x + "," + walled.chokes[0].z + " facing " + walled.chokes[0].facing : "none")})");
            check(walled.shown.All(rt => rt.cells.Any(c => c == 37 * 50 + 24)), "house: every way in passes the gate");
            check(walled.doorsToCore == 1, $"house: one door on the way to the beds besides the bedroom's own ({walled.doorsToCore})");
            var inner = DefenseMath.Analyse(Map(50, 50, (x, z) =>
            {
                bool ring2 = (x == 16 || x == 30) && z >= 16 && z <= 32 || (z == 16 || z == 32) && x >= 16 && x <= 30;
                if (ring2) return x == 23 && z == 32 ? 'D' : 'W';
                return House(x, z, true);
            }));
            check(inner.doorsToCore == 2, $"house: an inner ring makes a second line ({inner.doorsToCore})");
            // A strip of home area outside the wall doesn't make it open.
            check(DefenseMath.HomeDepth(Map(50, 50, (x, z) => House(x, z, true)))[11 * 50 + 25] <= 2, "house: the home strip outside the wall is shallow");
        }

        // Two passes: three wide to the north, nine wide to the east. More of the edge is open to the east, so more raids pass there.
        {
            var m = Map(50, 50, (x, z) =>
            {
                if (x >= 20 && x <= 22 && z >= 30) return '.';
                if (z >= 16 && z <= 24 && x >= 30) return '.';
                if (x >= 10 && x <= 30 && z >= 10 && z <= 30) return x >= 18 && x <= 22 && z >= 18 && z <= 22 ? (x == 20 && z == 20 ? 'c' : 'h') : '.';
                return '#';
            });
            var r = DefenseMath.Analyse(m);
            var north = r.chokes.FirstOrDefault(c => c.facing == 0);
            var east = r.chokes.FirstOrDefault(c => c.facing == 1);
            check(north != null && north.width == 3, $"two passes: the north pass is 3 wide ({north?.width})");
            check(east != null && east.width == 9, $"two passes: the east pass is 9 wide ({east?.width})");
            check(north != null && east != null && east.share > north.share && Math.Abs(east.share + north.share - 1f) < 0.05f, $"two passes: shares follow the open edge (north {north?.share:0.00}, east {east?.share:0.00})");
            check(r.chokes[0] == east, "two passes: the busier pass comes first");
        }

        // A gap between rock and water, and a river with a bridge.
        {
            var m = Map(40, 40, (x, z) =>
            {
                if (z >= 25)
                {
                    if (x < 16) return '#';
                    if (x <= 19) return '.';
                    return '~';
                }
                if (x >= 5 && x <= 34 && z >= 5) return x == 20 && z == 12 ? 'c' : x >= 17 && x <= 23 && z >= 9 && z <= 15 ? 'h' : '.';
                return '#';
            });
            var r = DefenseMath.Analyse(m);
            check(r.chokes.Count > 0 && r.chokes[0].width == 4, $"rock and water: the gap is 4 wide ({(r.chokes.Count > 0 ? r.chokes[0].width : -1)})");
            if (r.chokes.Count > 0)
                check(r.chokes[0].endA == DefenseMath.Rock && r.chokes[0].endB == DefenseMath.Water, $"rock and water: rock on the west, water on the east ({r.chokes[0].endA},{r.chokes[0].endB})");

            var river = Map(40, 40, (x, z) =>
            {
                if (z >= 24 && z <= 27) return x >= 10 && x <= 12 ? '=' : '~';
                return x == 20 && z == 8 ? 'c' : x >= 17 && x <= 23 && z >= 5 && z <= 11 ? 'h' : '.';
            });
            var rr = DefenseMath.Analyse(river);
            check(rr.chokes.Count > 0 && rr.chokes[0].bridge && rr.chokes[0].width == 3, $"river: the bridge is the way across, 3 wide ({(rr.chokes.Count > 0 ? rr.chokes[0].width + (rr.chokes[0].bridge ? " bridge" : "") : "none")})");
            check(rr.sideReach[0] > 0 && rr.sideReach[2] > 0, "river: both banks reach home");
        }

        // Nothing reaches home: an island in a lake.
        {
            var m = Map(30, 30, (x, z) => x >= 10 && x <= 19 && z >= 10 && z <= 19 ? (x == 15 && z == 15 ? 'c' : 'h') : '~');
            var r = DefenseMath.Analyse(m);
            check(r.unreachable && r.chokes.Count == 0, "island: no way in");
        }

        // The heap gives the lowest first.
        {
            var heap = new DefenseMath.MinHeap(2);
            var rnd = new Random(3);
            var keys = Enumerable.Range(0, 200).Select(_ => (long)rnd.Next(0, 1000)).ToList();
            for (int i = 0; i < keys.Count; i++) heap.Push(keys[i], i);
            long last = -1;
            bool sorted = true;
            while (heap.Count > 0)
            {
                heap.Pop(out long k, out _);
                if (k < last) sorted = false;
                last = k;
            }
            check(sorted, "the heap pops in order");
        }
        check(DefenseMath.Compass(0f) == 0 && DefenseMath.Compass(44f) == 1 && DefenseMath.Compass(90f) == 2 && DefenseMath.Compass(350f) == 0 && DefenseMath.Compass(315f) == 7, "compass points");
    }
}
