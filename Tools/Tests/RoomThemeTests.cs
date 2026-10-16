using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Every room theme on every room template and size. The theme's furniture is chosen the way the game chooses it: for a
/// piece's purpose, the first of the theme's pieces the game has that keeps the shape of the plan's own pick
/// (ThemeMath.KeepsShape); walls, doors and pieces in the wall stay as they are. The layout must then pass the same checks
/// as the plan itself, in every style, all four turns, mirrored and not: with the best furniture, the fallback furniture,
/// and with the furniture of every DLC and mod the template doesn't require left out (a missing theme piece falls back to
/// the next one, and in the end to the plan's own). A theme that wants plants adds its plant pots from the comfortable
/// style on, and they must leave everything reachable. Each theme piece must be known to the tests, so its shape is too.
/// </summary>
public static class RoomThemeTests
{
    public class Theme
    {
        public string defName, file, decorFrom, era;
        public List<(string role, List<string> defs)> swaps = new();
        public List<string> pots = new();
    }

    public static List<Theme> Load(string root)
    {
        var themes = new List<Theme>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "RoomTemplateDefs"), "*.xml"))
        {
            foreach (var el in XDocument.Load(file).Root!.Elements())
            {
                if (el.Name.LocalName != "ColonyCouncil.RoomThemeDef") continue;
                var th = new Theme
                {
                    defName = el.Element("defName")?.Value ?? "?",
                    file = Path.GetFileName(file),
                    decorFrom = el.Element("decorFrom")?.Value,
                    era = el.Element("era")?.Value,
                    pots = el.Element("pots")?.Elements().Select(x => x.Value).ToList() ?? new List<string>()
                };
                foreach (var li in el.Element("swaps")?.Elements() ?? Enumerable.Empty<XElement>())
                    th.swaps.Add((li.Element("role")?.Value, li.Element("defs")?.Elements().Select(x => x.Value).ToList() ?? new List<string>()));
                themes.Add(th);
            }
        }
        return themes;
    }

    /// <summary>Beds that tend patients better (the game asks the def; the tests know them by name).</summary>
    private static readonly HashSet<string> Medical = new() { "HospitalBed" };

    public static ThemeMath.Shape ShapeOf(RoomTemplateTests.Fact f) =>
        new ThemeMath.Shape { sizeX = f.sx, sizeZ = f.sz, rotatable = f.rotatable, passable = f.pass != "", spots = f.spots };

    /// <summary>The theme's piece for a slot, as the game picks it, or the plan's own when the theme has nothing that fits.</summary>
    public static string ThemedPick(Theme theme, RoomTemplateTests.Thing th, string basePick, Func<string, bool> present)
    {
        var facts = RoomTemplateTests.facts;
        if (basePick == null || !ThemeMath.Themable(th.role, th.inWall)) return basePick;
        foreach (var cand in ThemeMath.Candidates(th.role, theme.swaps))
        {
            if (cand == basePick) return basePick;
            if (!facts.TryGetValue(cand, out var f) || !present(cand)) continue;
            if (!ThemeMath.KeepsPurpose(Medical.Contains(basePick), Medical.Contains(cand))) continue;
            if (ThemeMath.KeepsShape(ShapeOf(facts[basePick]), ShapeOf(f))) return cand;
        }
        return basePick;
    }

    public static int Run(string root, List<RoomTemplateTests.Template> templates, Action<bool, string> check)
    {
        var facts = RoomTemplateTests.facts;
        ShapeRules(check);

        var themes = Load(root);
        check(themes.Count >= 8, $"expected at least 8 room themes, found {themes.Count}");
        var roleKeys = RoomTemplateTests.RoleKeys(root);
        foreach (var theme in themes)
        {
            check(theme.decorFrom is null or "comfortable" or "lavish", $"{theme.file}: {theme.defName} decorFrom must be comfortable or lavish");
            check(theme.era is null or "lowtech" or "industrial", $"{theme.file}: {theme.defName} era must be lowtech or industrial");
            foreach (var (role, defs) in theme.swaps)
            {
                check(ThemeMath.Themable(role, false), $"{theme.file}: {theme.defName} swaps {role}, which a theme must leave alone");
                check(role != null && roleKeys.Contains(role), $"{theme.file}: {theme.defName} swaps the role {role}, which has no CC_Role_{role} label");
                check(defs.Count > 0, $"{theme.file}: {theme.defName} has a swap for {role} with no furniture");
                foreach (var d in defs)
                    check(facts.ContainsKey(d), $"{theme.file}: {theme.defName} uses {d} for {role}: add its shape to the tests' facts");
            }
            foreach (var p in theme.pots)
                check(facts.TryGetValue(p, out var pf) && pf.sx == 1 && pf.sz == 1, $"{theme.file}: {theme.defName} pot {p} must be a known single cell");
        }

        int count = 0, swapsSeen = 0;
        foreach (var theme in themes)
        {
            int decorFrom = ThemeMath.DecorFrom(theme.decorFrom);
            foreach (var t in templates)
            {
                bool Present(string name) => RoomTemplateTests.SourceOf(name) is not string s || t.requires.Contains(s);
                string PresentPick(RoomTemplateTests.Thing th) => th.defs.FirstOrDefault(d => facts.ContainsKey(d) && Present(d));
                var modes = new List<(string label, Func<RoomTemplateTests.Thing, string> basePick, Func<string, bool> present)>
                {
                    ("best furniture", th => RoomTemplateTests.Pick(th, false), _ => true),
                    ("fallback furniture", th => RoomTemplateTests.Pick(th, true), _ => true)
                };
                // Without the DLCs and mods the template doesn't need, unless its key piece is missing (then it isn't offered).
                if (!t.things.Any(th => !th.optional && PresentPick(th) == null))
                    modes.Add(("without unrequired DLC and mod furniture", PresentPick, Present));
                foreach (var (label, basePick, present) in modes)
                {
                    string Themed(RoomTemplateTests.Thing th)
                    {
                        var b = basePick(th);
                        var p = ThemedPick(theme, th, b, present);
                        if (p != b) swapsSeen++;
                        return p;
                    }
                    for (int style = 0; style <= 2; style++)
                    {
                        var (pieces, notes) = RoomTemplateTests.Arrange(t, Themed, style, decorFrom);
                        string where = $"{t.file}: {t.defName} in {theme.defName} ({label}, style {style})";
                        foreach (var n in notes) check(false, $"{where}: layout had to change: {n}");
                        for (int mirror = 0; mirror < 2; mirror++)
                            for (int turn = 0; turn < 4; turn++)
                            {
                                RoomTemplateTests.CheckWorld(t, pieces, mirror == 1, turn, $"{where} turn {turn}{(mirror == 1 ? " mirrored" : "")}", check);
                                count++;
                            }
                    }
                }
            }
        }
        // The themes do change rooms: a check that found nothing to swap would prove nothing.
        check(swapsSeen > 1000, $"the themes swapped only {swapsSeen} pieces across all rooms");
        return count;
    }

    /// <summary>The shape rule on its own: what a theme may and may not swap.</summary>
    private static void ShapeRules(Action<bool, string> check)
    {
        var facts = RoomTemplateTests.facts;
        bool Keeps(string a, string b) => ThemeMath.KeepsShape(ShapeOf(facts[a]), ShapeOf(facts[b]));
        check(Keeps("Bed", "Bed_Simple") && Keeps("Bed", "Bedroll"), "a bed may become a simple bed or a bedroll");
        check(!Keeps("Bedroll", "Bed"), "a bedroll people step over may not become a bed that blocks");
        check(!Keeps("Bed", "DoubleBed"), "a single bed may not become a double");
        check(Keeps("DiningChair", "Armchair") && Keeps("StandingLamp", "TorchLamp") && Keeps("StandingLamp", "VFE_FloorLamp"), "chairs and lamps swap freely");
        check(!Keeps("VFE_FloorLamp", "StandingLamp"), "a lamp people walk over may not become one that blocks");
        check(Keeps("BasinStuff", "WashBucket") && !Keeps("WashBucket", "BasinStuff"), "a piece may lose its work spot, never gain one");
        check(!Keeps("Table2x2c", "Table1x2c") && Keeps("Table2x2c", "Table_Royal2x2c"), "tables only swap for the same size");
        check(!Keeps("Harp", "PlantPot"), "a piece with a work spot may not become one that can't face the same way");
        check(!ThemeMath.KeepsPurpose(true, false) && ThemeMath.KeepsPurpose(false, false) && ThemeMath.KeepsPurpose(true, true),
            "a hospital bed never becomes an ordinary one");
        check(!ThemeMath.Themable("wall", false) && !ThemeMath.Themable("door", false) && !ThemeMath.Themable("light", true) && ThemeMath.Themable("bed", false),
            "walls, doors and pieces in the wall stay as they are");
        var swaps = new List<(string, List<string>)> { ("bed", new List<string> { "A", "B" }), ("chair", new List<string> { "C" }), ("bed", new List<string> { "B", "D" }) };
        check(string.Join(",", ThemeMath.Candidates("bed", swaps)) == "A,B,D", "swap lists for one purpose are read in order");
        check(ThemeMath.AddsDecor(TemplateLayout.Comfortable, ThemeMath.DecorFrom("comfortable")) && !ThemeMath.AddsDecor(TemplateLayout.Comfortable, ThemeMath.DecorFrom(null))
              && !ThemeMath.AddsRug(TemplateLayout.Comfortable), "a theme that wants plants adds pots from the comfortable style on, never the rug");
    }
}
