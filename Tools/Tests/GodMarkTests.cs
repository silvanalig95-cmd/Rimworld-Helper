using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// A god's mark worth having (DESIGN.md, "A god's mark worth having"; Source/ColonyCouncil/Gods/Presence/GodMarkMath.cs and
/// GodMarkGoods.cs; the player, 19 November 2026: "many of them just generate a single stone [chunk]??"): the marks table
/// (every look's mark, no stone chunk or slag left), the worth of each mark of goods by the colony's age with the game's own
/// prices and the player's mods' meals, how many of each thing, the weapon's age, the kill's size, the wait between two marks
/// of goods and the most a year, every name in the player's active mods with the game's own last, the words (what was left,
/// no god named, plain), and the seams (the card, the colony's book and the moods say it; the art no colonist's).
/// </summary>
public static class GodMarkTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "a god's mark: " + what);
        }

        // ================= The marks table =================

        var expected = new Dictionary<string, (string mark, string wrath)>
        {
            ["Harvest"] = ("Flowers", "Ash"), ["Storm"] = ("Lightning", "Scorch"), ["Hunt"] = ("Kill", "Ash"), ["Healing"] = ("Flowers", "Ash"),
            ["War"] = ("Weapon", "Scorch"), ["War_Spacer"] = ("Weapon", "Scorch"), ["Craft"] = ("Metal", "Scorch"), ["Craft_Spacer"] = ("Metal", "Scorch"),
            ["Way"] = ("StandingStone", "Ash"), ["Dead"] = ("Ash", "Ash"), ["Night"] = ("Frost", "Frost"), ["Hearth"] = ("Hearth", "Scorch"),
            ["Wild"] = ("Tree", "Ash"), ["Waters"] = ("Spring", "Ash"), ["Machine"] = ("Components", "Scorch"), ["Machine_Old"] = ("Components", "Scorch"),
            ["Void"] = ("Scorch", "Scorch"), ["Bellwether"] = ("Flowers", "Ash"), ["Revel"] = ("None", "Ash"), ["Smoulder"] = ("Scorch", "Scorch"),
            ["Mog"] = ("None", "Ash"), ["Murmur"] = ("Spring", "Ash")
        };
        var looks = new[] { "Rimfolk_GodLooks.xml", "Rimfolk_GodsOnMap_Shapes.xml" }
            .SelectMany(f => XDocument.Load(Path.Combine(root, "Defs", "GodDefs", f)).Root!.Elements("ColonyCouncil.GodLookDef")).ToList();
        C(looks.Count == expected.Count, $"every look of the gods is in the table ({looks.Count})");
        foreach (var l in looks)
        {
            string key = l.Element("defName")!.Value.Replace("Rimfolk_GodLook_", "");
            string mark = l.Element("mark")?.Value ?? "None", wrath = l.Element("wrathMark")?.Value ?? "Scorch";
            C(expected.TryGetValue(key, out var want) && want.mark == mark && want.wrath == wrath, $"{key}: leaves {mark}, and {wrath} in anger");
            C(Enum.TryParse<GodMark>(mark, out _) && Enum.TryParse<GodMark>(wrath, out _), $"{key}: its marks are marks the code knows");
        }
        C(!Enum.GetNames(typeof(GodMark)).Contains("Stone") && !Enum.GetNames(typeof(GodMark)).Contains("Slag"), "no stone chunk or lump of slag is a god's mark any more");
        var goods = new[] { "Hunt", "War", "War_Spacer", "Craft", "Craft_Spacer", "Way", "Hearth", "Machine", "Machine_Old" };
        foreach (var k in goods)
            C(GodMarkMath.HasWorth(Enum.Parse<GodMark>(expected[k].mark)), $"{k}: its mark leaves goods worth having");
        C(!GodMarkMath.HasWorth(GodMark.Lightning) && GodPresenceMath.MarkRadius(GodMark.Lightning) > 0, "the Storm's lightning leaves a ring of scorched ground and no goods");
        foreach (GodMark m in Enum.GetValues(typeof(GodMark)))
            if (m != GodMark.None && !goods.Any(k => expected[k].mark == m.ToString()) && m != GodMark.Lightning)
                C(!GodMarkMath.HasWorth(m), $"{m}: only changes the ground");
        C(GodPresenceMath.MarkRadius(GodMark.Weapon) == GodPresenceMath.MarkRadius(GodMark.Scorch) && GodPresenceMath.MarkRadius(GodMark.Kill) == 0, "a weapon's ring as wide as a scorch; goods at the place");

        // ================= The worth by age =================

        C(Enumerable.Range(0, 4).All(a => GodMarkMath.Target(a) >= GodMarkMath.WorthMin && GodMarkMath.Target(a) <= GodMarkMath.WorthMax)
          && Enumerable.Range(1, 3).All(a => GodMarkMath.Target(a) > GodMarkMath.Target(a - 1)), "the worth aimed at is within 100 to 300 silver and grows with the age");
        C(GodMarkMath.Age(-3) == 0 && GodMarkMath.Age(9) == 3 && GodMarkMath.Target(7) == GodMarkMath.Target(3), "ages outside tribal to spacer are held within them");
        var items = Items(root);
        float Price(string def) => items.TryGetValue(def, out var r) ? r.value : float.NaN;
        float steel = Price("Steel"), plasteel = Price("Plasteel"), comp = Price("ComponentIndustrial"), adv = Price("ComponentSpacer"), wood = Price("WoodLog");
        C(steel > 1f && plasteel > 5f && comp > 20f && adv > 100f && wood > 0.5f, $"the game's own prices are read (steel {steel}, plasteel {plasteel}, components {comp} and {adv}, wood {wood})");
        bool In(float w) => w >= GodMarkMath.WorthMin && w <= GodMarkMath.WorthMax;
        float prevMetal = 0f, prevParts = 0f;
        for (int age = 0; age <= 3; age++)
        {
            // The Craft: the working metal.
            string metal = GodMarkMath.MetalFor(age)[0];
            float unit = Price(metal);
            int count = GodMarkMath.MetalCount(age, unit);
            float worth = count * unit;
            C(In(worth) && worth >= prevMetal, $"age {age}: {count} {metal}, {worth:0} silver");
            C(metal == (age == 3 ? "Plasteel" : "Steel"), $"age {age}: the working metal is {(age == 3 ? "plasteel" : "steel")}");
            prevMetal = worth;
            int steelOnly = GodMarkMath.MetalCount(age, steel);
            C(In(steelOnly * steel), $"age {age}: steel where the age's metal can't be had ({steelOnly}, {steelOnly * steel:0} silver)");
            // The Machine: components.
            var (a, p) = GodMarkMath.Components(age, comp, adv);
            float parts = a * adv + p * comp;
            C(In(parts) && parts >= prevParts && (age == 3 ? a == 1 : a == 0) && a + p >= 3, $"age {age}: {a} advanced and {p} components, {parts:0} silver");
            prevParts = parts;
            var (a0, p0) = GodMarkMath.Components(age, comp, 0f);
            C(a0 == 0 && In(p0 * comp), $"age {age}: plain components where no advanced one can be had ({p0})");
            // The Hearth: firewood and the age's warm meals, every one of the player's.
            foreach (var meal in GodMarkMath.MealsFor(age))
            {
                float mv = Price(meal);
                int meals = GodMarkMath.Meals(age, mv, wood);
                float hearth = GodMarkMath.FirewoodCount * wood + meals * mv;
                C(!float.IsNaN(mv) && In(hearth) && meals >= 3 && meals <= 8, $"age {age}: firewood and {meals} {meal}, {hearth:0} silver");
            }
            // War: the weapon's worth.
            C(GodMarkMath.WeaponMost(age) <= GodMarkMath.WorthMax && GodMarkMath.WeaponMost(age) >= GodMarkMath.Target(age)
              && GodMarkMath.WeaponFits(GodMarkMath.Target(age), age) && !GodMarkMath.WeaponFits(GodMarkMath.WorthMin - 1f, age) && !GodMarkMath.WeaponFits(GodMarkMath.WeaponMost(age) + 1f, age),
                $"age {age}: a weapon worth {GodMarkMath.WorthMin:0} to {GodMarkMath.WeaponMost(age):0} silver");
        }
        C(GodMarkMath.Count(120f, 1.9f, 10, 150) == 65 && GodMarkMath.Count(260f, 9f, 10, 150) == 30 && GodMarkMath.Count(160f, 32f, 2, 9) == 5,
            "counts rounded to fives from twenty up (65 steel, 30 plasteel), else to the nearest one (5 components)");
        C(GodMarkMath.Count(100f, 0f, 3, 8) == 3 && GodMarkMath.Count(10000f, 1f, 3, 8) == 8 && GodMarkMath.Count(1f, 100f, 3, 8) == 3, "a count stays within its least and most");

        // ================= The weapon's age, the kill's size =================

        C(GodMarkMath.WeaponTier(2, 2) == 0 && GodMarkMath.WeaponTier(1, 2) == 1 && GodMarkMath.WeaponTier(0, 2) == 1 && GodMarkMath.WeaponTier(3, 2) == -1
          && GodMarkMath.WeaponTier(-1, 2) == -1 && GodMarkMath.WeaponTier(0, 0) == 0 && GodMarkMath.WeaponTier(1, 0) == -1,
            "a weapon of the colony's own age first, an earlier age's after it, a later age's never");
        C(GodMarkMath.KillFits(1.2f) && GodMarkMath.KillFits(0.85f) && !GodMarkMath.KillFits(0.2f) && !GodMarkMath.KillFits(2.4f) && !GodMarkMath.KillFits(2.1f),
            "the kill is a deer or a boar, never a hare, an elk or a muffalo");
        C(GodMarkMath.KillFits(0.4f, true) && GodMarkMath.KillFits(1.8f, true) && !GodMarkMath.KillFits(0.2f, true) && !GodMarkMath.KillFits(4f, true) && !GodMarkMath.KillFits(float.NaN),
            "on a land with no such game a little smaller or larger, never a thrumbo");
        C(GodMarkMath.KillWorthFits(250f) && GodMarkMath.KillWorthFits(GodMarkMath.KillWorth(436f)) && !GodMarkMath.KillWorthFits(0f) && !GodMarkMath.KillWorthFits(800f)
          && !GodMarkMath.KillWorthFits(float.NaN), "the kill worth about 300 silver at most, a deer's meat and leather as a butcher gets them within it");
        C(Math.Abs(GodMarkMath.KillWorth(400f) - 300f) < 0.01f && GodMarkMath.KillWorth(-1f) == 0f && GodMarkMath.KillWorth(float.NaN) == 0f, "a kill counted as a butcher of middling skill gets it");

        // ================= How often =================

        foreach (GodMark m in Enum.GetValues(typeof(GodMark)))
        {
            var lesser = GodMarkMath.Lesser(m);
            C(!GodMarkMath.HasWorth(lesser) && (GodMarkMath.HasWorth(m) || lesser == m), $"{m}: within the wait it leaves {lesser}, no goods");
            C(GodMarkMath.Allowed(m, 3f) == (GodMarkMath.HasWorth(m) ? lesser : m) && GodMarkMath.Allowed(m, GodMarkMath.WorthGapDays) == m && GodMarkMath.Allowed(m, 9999f) == m,
                $"{m}: goods once the wait is past, its plain form within it");
        }
        C(GodMarkMath.Lesser(GodMark.Weapon) == GodMark.Scorch && GodMarkMath.Lesser(GodMark.Metal) == GodMark.Scorch && GodMarkMath.Lesser(GodMark.Kill) == GodMark.None,
            "within the wait war and the forge scorch the ground, the hunt leaves nothing");
        C(!GodMarkMath.WorthDue(float.NaN) && GodMarkMath.WorthDue(10f) && !GodMarkMath.WorthDue(9.9f), "the wait is ten days");
        C(GodMarkMath.MostAYear == 6 && Math.Abs(GodMarkMath.MostWorthAYear - 1800f) < 0.01f, "six marks of goods a year at the most, 1,800 silver at the very most");

        // ================= The player's mods =================

        var active = Active(root);
        var names = GodMarkMath.MealsFor(0).Concat(GodMarkMath.MealsFor(1)).Concat(GodMarkMath.MealsFor(2)).Concat(GodMarkMath.MealsFor(3))
            .Concat(GodMarkMath.MetalFor(3)).Concat(GodMarkMath.Stones)
            .Concat(new[] { GodMarkMath.Component, GodMarkMath.AdvancedComponent, GodMarkMath.Firewood, GodMarkMath.FallbackBlocks }).Distinct().ToList();
        foreach (var name in names)
            C(active.TryGetValue(name, out var mod), $"'{name}' is in the player's active mods ({(active.TryGetValue(name, out var m2) ? m2 : "none")})");
        foreach (var list in new[] { GodMarkMath.MealsFor(0), GodMarkMath.MealsFor(1), GodMarkMath.MealsFor(2), GodMarkMath.MealsFor(3), GodMarkMath.MetalFor(3), GodMarkMath.Stones })
        {
            string last = list[list.Length - 1];
            C(active.TryGetValue(last, out var mod) && mod == "ludeon.rimworld", $"the base game's {last} last, to fall back on");
        }
        C(GodMarkMath.Stones.Concat(GodMarkMath.MealsFor(0)).Concat(GodMarkMath.MealsFor(3)).Any(x => active.TryGetValue(x, out var mod) && !mod.StartsWith("ludeon")),
            "the player's mods' things first where they have them (Vanilla Cooking Expanded's meals, Vanilla Furniture Expanded - Art's obelisk)");
        string[] Pref = { "MealSimple", "MealFine", "MealFine", "MealLavish" };
        for (int age = 0; age <= 3; age++)
            foreach (var meal in GodMarkMath.MealsFor(age))
                C(items.TryGetValue(meal, out var r) && r.pref == Pref[age] && r.food == "Meal", $"age {age}: {meal} is a {Pref[age]} meal");
        foreach (var stone in GodMarkMath.Stones)
            C(items.TryGetValue(stone, out var r) && r.cats.Contains("BuildingsArt") && r.stuff.Contains("Stony") && (r.size == "" || r.size == "(1,1)"),
                $"{stone}: a work of art of one cell that can be cut from stone");

        // ================= The words =================

        var keyed = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var e in XDocument.Load(file).Root!.Elements())
                keyed[e.Name.LocalName] = e.Value;
        var holes = new Dictionary<GodMark, string[]>
        {
            [GodMark.Kill] = new[] { "{ANIMAL}" }, [GodMark.Weapon] = new[] { "{WEAPON}" }, [GodMark.StandingStone] = new[] { "{STONE}", "{ROCK}" },
            [GodMark.Hearth] = new[] { "{MEALS}" }, [GodMark.Metal] = new[] { "{METAL}" }, [GodMark.Components] = new[] { "{PARTS}" }
        };
        foreach (GodMark m in Enum.GetValues(typeof(GodMark)))
        {
            if (m == GodMark.None)
            {
                C(GodMarkMath.WordsKey(m) == null, "no mark, no words");
                continue;
            }
            foreach (var alone in m == GodMark.Weapon ? new[] { false, true } : new[] { false })
            {
                string key = GodMarkMath.WordsKey(m, alone);
                C(keyed.TryGetValue(key, out var text), $"{key}: the words for what was left");
                text ??= "";
                var want = holes.TryGetValue(m, out var h) ? h : new string[0];
                var has = Regex.Matches(text, @"\{[A-Z_]+\}").Cast<Match>().Select(x => x.Value).Distinct().ToList();
                C(want.All(text.Contains) && has.All(want.Contains), $"{key}: says what was left ({string.Join(", ", want)})");
                C(Regex.Matches(text, @"[.?!](\s|$)").Count == 1 && text.EndsWith("."), $"{key}: one sentence");
                C(!text.Contains("{NAME}") && !Regex.IsMatch(text, @"\bgods?\b", RegexOptions.IgnoreCase), $"{key}: names no god and says nothing of one (the gods are unseen)");
                C(!text.Contains('—') && !text.Contains('!') && !Regex.IsMatch(text, @"\b(colour|grey|armour|honour)\b"), $"{key}: plain, no dashes or exclamations, American spelling");
                C(text.Contains("where it happened"), $"{key}: says where: where it happened");
            }
        }
        C(keyed.TryGetValue("CC_GodP_Left_ComponentsAnd", out var and) && and.Contains("{ADVANCED}") && and.Contains("{PARTS}"), "an advanced component and the others named together");
        C(GodMarkMath.WithLeft("A warm light fell on one place", "When it was over, 65 steel lay where it happened.") == "A warm light fell on one place. When it was over, 65 steel lay where it happened."
          && GodMarkMath.WithLeft("It went.", "Snow lay there.") == "It went. Snow lay there." && GodMarkMath.WithLeft(null, "Snow lay there.") == "Snow lay there."
          && GodMarkMath.WithLeft("It went.", null) == "It went." && GodMarkMath.WithLeft("It went.", "  ") == "It went.",
            "what was left follows the moment's words as a sentence of its own");

        // ================= The seams =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string presence = Read("Gods/Presence/GodPresence.cs"), card = Read("Gods/Presence/GodCard.cs"), trails = Read("Gods/Presence/GodTrails.cs"),
            goodsSrc = Read("Gods/Presence/GodMarkGoods.cs"), gods = Read("Gods/Gods.cs"), touch = Read("Gods/Touch/GodTouch.cs"), wiring = Read("Gods/GodsWiring.cs"),
            seam = Read("Gods/GodPresenceSeam.cs"), debug = Read("Gods/Presence/GodPresenceDebug.cs"), looksDef = Read("Gods/Presence/GodLookDef.cs");
        C(presence.Contains("m.mark = GodMarkGoods.Plan(GodPresenceMath.MarkFor(kind, look.mark, look.wrathMark), map, at);") && presence.Contains("GodTrails.Mark(m.mark, map, m.at, m.a);"),
            "the mark is chosen as the moment begins and laid at the sting");
        C(presence.IndexOf("m.mark = GodMarkGoods.Plan(", StringComparison.Ordinal) < presence.IndexOf("LastLeft = m.mark?.words;", StringComparison.Ordinal)
          && wiring.Contains("s.left = played ? GodPresence.LastLeft : null;") && seam.Contains("public string left;"),
            "the core's seam takes what was left from the moment it just began");
        C(card.Contains("body = GodMarkMath.WithLeft(body, left);") && presence.Contains("m.sign, m.mark?.words);"), "the card says what was left after its words");
        C(gods.Contains("GodMarkMath.WithLeft(!spec.text.NullOrEmpty() ? spec.text : (\"CC_GodLore_Moment_\" + spec.kind).Translate().Resolve(), spec.left)"),
            "the colony's book says what was left after the moment's words");
        C(touch.Contains("GodMarkMath.WithLeft(spec.text, spec.left)"), "the moods of those who saw it say what was left");
        C(goodsSrc.Contains("ArtGenerationContext.Outsider") && !goodsSrc.Contains("JustCreatedBy"), "the standing stone is nobody of the colony's work: its author unknown, no story of the colony's on it");
        C(goodsSrc.Contains("PokeWorldLink.IsPokemon(k)") && goodsSrc.Contains("SacredAnimals.SacredToColony(k.race)") && goodsSrc.Contains("BiomeAnimals.Of(biome)"),
            "the kill is the land's game, never a Pokémon or an animal the colony's faith holds sacred");
        C(goodsSrc.Contains("CompBladelinkWeapon") && goodsSrc.Contains("\"Grenade\"") && goodsSrc.Contains("PeopleAgesMath.AgeOfTech") && goodsSrc.Contains("TryRandomStuffByCommonalityFor(d, out stuff, tech)"),
            "the weapon: no persona weapon or grenade, of the colony's age, of a material the age has");
        C(goodsSrc.Contains("SetForbidden(false, false)") && goodsSrc.Contains("!map.areaManager.Home[cell.Value]"), "the kill is the colony's to take, its blood only outside the home area");
        C(goodsSrc.Contains("book?.NoteWorth()") && presence.Contains("Scribe_Values.Look(ref lastWorth, \"lastWorth\", -999999);"), "when goods were last left is kept with the game");
        C(debug.Contains("\"Gods: a god's mark here...\"") && debug.Contains("GodMarkGoods.AnyTime = true;") && debug.Contains("GodMarkGoods.AnyTime = false;"),
            "dev mode lays any mark in any age, and a moment tried there leaves its goods whenever");
        C(!trails.Contains("ChunkSlagSteel") && !trails.Contains("StoneChunk") && !goodsSrc.Contains("Chunk"), "no chunk left behind by a god");
        C(looksDef.Contains("public GodMark mark = GodMark.None;"), "a look with no mark named leaves none");
        return n;
    }

    /// <summary>The player's active mods' defs: name to its mod.</summary>
    private static Dictionary<string, string> Active(string root)
    {
        var d = new Dictionary<string, string>();
        using var gz = new GZipStream(File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz")), CompressionMode.Decompress);
        using var r = new StreamReader(gz);
        string line;
        while ((line = r.ReadLine()) != null)
        {
            var t = line.Split('\t');
            if (t.Length < 4 || t[0] == "type" || t[3] != "yes" || t[0] != "ThingDef") continue;
            d[t[1]] = t[2];
        }
        return d;
    }

    /// <summary>The player's items: name to price, preferability, food type, categories, stuff and size (docs/player-mods/items.tsv).</summary>
    private static Dictionary<string, (float value, string pref, string food, string cats, string stuff, string size)> Items(string root)
    {
        var d = new Dictionary<string, (float, string, string, string, string, string)>();
        var lines = File.ReadAllLines(Path.Combine(root, "docs", "player-mods", "items.tsv"));
        var head = lines[0].Split('\t').ToList();
        int Col(string name) => head.IndexOf(name);
        int dn = Col("defName"), act = Col("active"), mv = Col("marketValue"), pref = Col("preferability"), food = Col("foodType"), cats = Col("thingCategories"),
            stuff = Col("madeOf"), size = Col("size");
        foreach (var line in lines.Skip(1))
        {
            var t = line.Split('\t');
            if (t.Length <= size || t[act] != "yes") continue;
            float v = float.TryParse(t[mv], NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : float.NaN;
            d[t[dn]] = (v, t[pref], t[food], t[cats], t[stuff], t[size]);
        }
        return d;
    }
}
