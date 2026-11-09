// The apparel review: every piece of armour and apparel of the player's active mods rated with the mod's own apparel
// rating (Source/ColonyCouncil/Apparel/ApparelRateMath.cs and ApparelTagMath.cs, linked), from the numbers
// Tools/apparel_extract.py resolved. Run from the repository root:
//   python3 Tools/apparel_extract.py <collection folder> docs/player-mods/apparel_numbers.json.gz
//   dotnet run -c Release --project Tools/ApparelReview
// It writes docs/apparel/README.md and docs/apparel/apparel.json.
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ColonyCouncil;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

string root = Directory.GetCurrentDirectory();
while (!File.Exists(Path.Combine(root, "CLAUDE.md")) && Directory.GetParent(root) != null) root = Directory.GetParent(root).FullName;
string input = args.Length > 0 ? args[0] : Path.Combine(root, "docs/player-mods/apparel_numbers.json.gz");
string outDir = args.Length > 1 ? args[1] : Path.Combine(root, "docs/apparel");

JsonNode data;
using (var fs = File.OpenRead(input))
using (Stream s = input.EndsWith(".gz") ? new GZipStream(fs, CompressionMode.Decompress) : fs)
    data = JsonNode.Parse(s);
Inputs.Data = data.AsObject();

// ---------------------------------------------------------------- checks: the rating's own copies against the player's defs
var problems = new List<string>();
var zones = Inputs.Arr(data["body"], "zones").ToList();
foreach (var z in zones)
{
    var groups = Inputs.Strs(z, "groups");
    var mine = ApparelRateMath.Human.FirstOrDefault(h => h.Groups.OrderBy(x => x).SequenceEqual(groups.OrderBy(x => x)));
    if (mine == null || Math.Abs(mine.Ranged - Inputs.F(z, "ranged")) > 0.0002f || Math.Abs(mine.Melee - Inputs.F(z, "melee")) > 0.0002f ||
        Math.Abs(mine.MeleeBlunt - Inputs.F(z, "meleeBlunt")) > 0.0002f)
        problems.Add("the body zone " + string.Join(",", groups) + " differs from Bodies_Humanlike");
}
if (zones.Count != ApparelRateMath.Human.Length) problems.Add($"the body has {zones.Count} zones, the rating {ApparelRateMath.Human.Length}");
var threatDefs = data["threats"];
void Gun(string def, string label)
{
    var t = ApparelRateMath.Threats.First(x => x.Label == label);
    var p = threatDefs?[def]?["verbs"]?[0]?["projectile"];
    if (p == null || Math.Abs(Inputs.F(p, "damage") - t.Damage) > 0.01f || Math.Abs(Inputs.F(p, "armorPenetration") - t.Pen) > 0.005f)
        problems.Add($"{label}: the rating has {t.Damage}/{t.Pen}, the defs {Inputs.F(p, "damage")}/{Inputs.F(p, "armorPenetration")}");
}
Gun("Gun_Autopistol", "autopistol"); Gun("Gun_Revolver", "revolver"); Gun("Gun_MachinePistol", "machine pistol"); Gun("Gun_HeavySMG", "heavy SMG");
Gun("Gun_PumpShotgun", "pump shotgun"); Gun("Gun_BoltActionRifle", "bolt-action rifle"); Gun("Gun_AssaultRifle", "assault rifle"); Gun("Gun_LMG", "LMG");
Gun("Gun_SniperRifle", "sniper rifle"); Gun("Gun_ChargeRifle", "charge rifle"); Gun("Bow_Recurve", "recurve bow"); Gun("Gun_ChargeLance", "lancer's charge lance");
Gun("Gun_ChargeBlasterHeavy", "centipede's heavy charge blaster"); Gun("Gun_Needle", "pikeman's needle gun"); Gun("Gun_MiniShotgun", "militor's mini-shotgun");
Gun("Gun_ThumpCannon", "termite's thump cannon"); Gun("Gun_InfernoCannon", "centipede's inferno cannon");
foreach (var (label, weight, tools) in ApparelRateMath.Animals)
{
    string def = label switch { "timber wolf" => "Wolf_Timber", "warg" => "Warg", "grizzly bear" => "Bear_Grizzly", "cougar" => "Cougar", "megaspider" => "Megaspider", "thrumbo" => "Thrumbo", _ => "Muffalo" };
    var defTools = Inputs.Arr(threatDefs?[def], "tools").ToList();
    if (defTools.Count != tools.Length) { problems.Add($"{label}: {tools.Length} tools in the rating, {defTools.Count} in the defs"); continue; }
    for (int i = 0; i < tools.Length; i++)
        if (Math.Abs(Inputs.F(defTools[i], "power") - tools[i].power) > 0.01f || Math.Abs(Inputs.F(defTools[i], "cooldownTime") - tools[i].cd) > 0.01f ||
            Math.Abs(Inputs.F(defTools[i], "chanceFactor", 1f) - tools[i].chance) > 0.01f)
            problems.Add($"{label}'s {tools[i].tool}: the rating differs from the defs");
}
var scy = Inputs.Arr(threatDefs?["Mech_Scyther"], "tools").FirstOrDefault();
if (scy == null || Inputs.F(scy, "power") != 20f) problems.Add("the scyther's blade differs from the defs");

// ---------------------------------------------------------------- rate every piece
string Plain(List<string> cats)
{
    foreach (var (cat, s) in new[] { ("Fabric", "Cloth"), ("Metallic", "Steel"), ("Leathery", "Leather_Plain"), ("Woody", "WoodLog"), ("Stony", "BlocksGranite") })
        if (cats.Contains(cat) && data["stuffs"]?[s] != null) return s;
    return null;
}

var all = new List<Piece>();
var leftOut = new List<(JsonNode n, string why)>();
foreach (var n in (JsonArray)data["apparel"])
{
    if (Inputs.S(n, "leftOut") != "") { leftOut.Add((n, Inputs.S(n, "leftOut"))); continue; }
    var a = Inputs.Facts(n);
    string plainDef = a.StuffDef != "" ? Plain(a.StuffCategories) ?? a.StuffDef : null;
    var r = ApparelRateMath.Rate(a, plainDef == null ? null : Inputs.WithStuff(a, plainDef));
    var piece = new Piece { Node = n, Facts = a, Rating = r };
    foreach (var s in Inputs.Strs(n, "stuffs"))
    {
        var b = Inputs.WithStuff(a, s);
        piece.ByStuff[b.StuffLabel] = ApparelRateMath.Rate(b, r.Role).Overall;
    }
    piece.Tags = ApparelTagMath.For(r, a, byStuff: piece.ByStuff);
    piece.Plain = ApparelTagMath.Line(r, piece.Tags);
    piece.Table = a.Ages == "Child" ? "children" : r.Role;
    all.Add(piece);
}

// The anchors as the review reads their defs must rate as the tests' anchors.
var anchorDefs = new Dictionary<string, string>
{
    ["flak vest"] = "Apparel_FlakVest", ["flak pants"] = "Apparel_FlakPants", ["flak jacket"] = "Apparel_FlakJacket", ["plate armor"] = "Apparel_PlateArmor",
    ["recon armor"] = "Apparel_ArmorRecon", ["marine armor"] = "Apparel_PowerArmor", ["cataphract armor"] = "Apparel_ArmorCataphract",
    ["simple helmet"] = "Apparel_SimpleHelmet", ["flak helmet"] = "Apparel_AdvancedHelmet", ["recon helmet"] = "Apparel_ArmorHelmetRecon",
    ["marine helmet"] = "Apparel_PowerArmorHelmet", ["cataphract helmet"] = "Apparel_ArmorHelmetCataphract", ["shield belt"] = "Apparel_ShieldBelt",
    ["jump pack"] = "Apparel_PackJump", ["smokepop pack"] = "Apparel_SmokepopBelt", ["firefoam pop pack"] = "Apparel_FirefoampopPack",
    ["low-shield pack"] = "Apparel_PackBroadshield", ["parka"] = "Apparel_Parka", ["tribalwear"] = "Apparel_TribalA", ["duster"] = "Apparel_Duster",
    ["jacket"] = "Apparel_Jacket", ["T-shirt"] = "Apparel_BasicShirt", ["button-down shirt"] = "Apparel_CollarShirt", ["pants"] = "Apparel_Pants",
    ["tuque"] = "Apparel_Tuque", ["cowboy hat"] = "Apparel_CowboyHat", ["psychic foil helmet"] = "Apparel_PsychicFoilHelmet", ["eltex skullcap"] = "Apparel_EltexSkullcap",
};
foreach (var (name, r) in ApparelAnchors.All())
{
    var p = all.FirstOrDefault(x => x.Facts.DefName == anchorDefs[name]);
    if (p == null) { problems.Add("anchor " + name + " is not among the rated pieces"); continue; }
    if (Math.Abs(p.Rating.Overall - r.Overall) > 0.001f || p.Rating.Role != r.Role)
        problems.Add($"anchor {name}: the tests rate it {r.Overall} ({r.Role}), its def {p.Rating.Overall} ({p.Rating.Role})");
}
foreach (var pr in problems) Console.WriteLine("CHECK: " + pr);

string[] tableOrder = { "body armour", "helmet", "off-hand shield", "utility", "cold-weather gear", "hot-weather gear", "hazard gear", "everyday clothing", "headgear", "ceremonial or psychic", "children" };
var tableNames = new Dictionary<string, string>
{
    ["body armour"] = "Body armour", ["helmet"] = "Helmets", ["off-hand shield"] = "Shields carried in the off hand",
    ["utility"] = "Utility: belts, packs and accessories", ["cold-weather gear"] = "Cold-weather gear", ["hot-weather gear"] = "Hot-weather gear", ["hazard gear"] = "Vacuum and hazard suits",
    ["everyday clothing"] = "Everyday clothing", ["headgear"] = "Hats, masks and other headgear", ["ceremonial or psychic"] = "Ceremonial and psychic pieces",
    ["children"] = "For children",
};

Directory.CreateDirectory(outDir);
File.WriteAllText(Path.Combine(outDir, "apparel.json"), Json(), new UTF8Encoding(false));
File.WriteAllText(Path.Combine(outDir, "README.md"), Readme.Write(all, leftOut, tableOrder, tableNames, data), new UTF8Encoding(false));
Console.WriteLine($"Rated {all.Count} pieces ({all.Count(p => p.Facts.Unsure.Count > 0)} marked rough), left out {leftOut.Count}; wrote {outDir}/README.md and apparel.json.");
foreach (var t in tableOrder)
{
    var list = all.Where(p => p.Table == t).OrderByDescending(p => p.Rating.Overall).ToList();
    Console.WriteLine($"{tableNames[t]}: {list.Count}");
    Console.WriteLine("  top: " + string.Join(", ", list.Take(8).Select(p => $"{p.Facts.Label} ({p.Facts.ModName}) {p.Rating.Overall:0.0}")));
    Console.WriteLine("  bottom: " + string.Join(", ", list.AsEnumerable().Reverse().Take(6).Select(p => $"{p.Facts.Label} ({p.Facts.ModName}) {p.Rating.Overall:0.0}")));
}
foreach (var (name, r) in ApparelAnchors.All())
    Console.WriteLine($"  anchor {name}: {r.Overall:0.0} ({r.Role}) worth {r.Worth:0.000} fight {r.Fight:0.000} guns {r.Sub["guns"]} melee {r.Sub["melee"]} mech {r.Sub["mechanoids"]} cold {r.Sub["cold"]} heat {r.Sub["heat"]}");
return problems.Count == 0 ? 0 : 1;

// ---------------------------------------------------------------- the JSON

string Json()
{
    var arr = new JsonArray();
    foreach (var p in all.OrderBy(p => Array.IndexOf(tableOrder, p.Table)).ThenByDescending(p => p.Rating.Overall).ThenBy(p => p.Facts.Label))
    {
        var r = p.Rating;
        var a = p.Facts;
        var o = new JsonObject
        {
            ["defName"] = a.DefName, ["label"] = a.Label, ["mod"] = a.Mod, ["modName"] = a.ModName, ["techLevel"] = a.TechLevel,
            ["role"] = r.Role, ["table"] = p.Table, ["tableLabel"] = tableNames[p.Table], ["slot"] = r.Slot, ["ratedFor"] = r.For,
            ["rating"] = r.Overall, ["worth"] = Math.Round(r.Worth, 4),
            ["sub"] = Obj(r.Sub), ["situations"] = Obj(r.Situations), ["areas"] = Obj(r.Areas, 4), ["toughness"] = Obj(r.Toughness, 3),
            ["fight"] = new JsonObject { ["overall"] = Math.Round(r.Fight, 4), ["shooter"] = Math.Round(r.FightShooter, 4), ["melee"] = Math.Round(r.FightMelee, 4) },
            ["armour"] = new JsonObject { ["sharp"] = Math.Round(r.Sharp, 3), ["blunt"] = Math.Round(r.Blunt, 3), ["heat"] = Math.Round(r.Heat, 3) },
            ["insulation"] = new JsonObject { ["cold"] = Math.Round(r.Cold, 2), ["heat"] = Math.Round(r.HeatInsulation, 2) },
            ["coldRating"] = r.Situations["in the cold"], ["heatRating"] = r.Situations["in the heat"],
            ["coverage"] = Math.Round(r.Coverage, 4), ["layers"] = Arr(a.Layers), ["bodyPartGroups"] = Arr(a.Groups), ["apparelTags"] = Arr(a.Tags),
            ["tags"] = new JsonArray(p.Tags.Select(t => (JsonNode)new JsonObject
            {
                ["id"] = t.Id, ["label"] = t.Label, ["category"] = t.Category.ToString(), ["colour"] = WeaponTagMath.Style(t.Category).Hex,
                ["symbol"] = WeaponTagMath.Style(t.Category).Symbol, ["why"] = t.Why
            }).ToArray()),
            ["bestFor"] = Arr(r.BestFor), ["weakWhen"] = Arr(r.WeakWhen), ["skill"] = r.Skill,
            ["specials"] = new JsonArray(r.Specials.Select(s => (JsonNode)new JsonObject { ["key"] = s.Key, ["label"] = s.Label, ["counted"] = s.Counted, ["sign"] = s.Sign }).ToArray()),
            ["plain"] = p.Plain, ["unsure"] = Arr(a.Unsure),
            ["stuff"] = a.StuffDef == "" ? null : a.StuffDef, ["stuffLabel"] = a.StuffLabel == "" ? null : a.StuffLabel, ["ratingByStuff"] = Obj(p.ByStuff, 1),
            ["mass"] = ApparelRateMath.Stat(a.StatBases, "Mass"), ["hitPoints"] = ApparelRateMath.Stat(a.StatBases, "MaxHitPoints", 100f), ["marketValue"] = a.MarketValue,
            ["titles"] = new JsonArray(a.Titles.Select(t => (JsonNode)new JsonObject { ["label"] = t.label, ["seniority"] = t.seniority }).ToArray()),
            ["faiths"] = Arr(a.Faiths), ["gender"] = a.Gender, ["ages"] = a.Ages, ["mechanitor"] = r.Areas["mech"] > 0.01f || a.MechanitorApparel,
            ["psycaster"] = r.Areas["psychic"] > 0.02f, ["mage"] = a.Enchantment.Count > 0, ["slave"] = a.SlaveApparel,
            ["countsAsClothes"] = a.CountsAsClothes, ["hardToFind"] = a.HardToGet, ["research"] = a.Research, ["researchTech"] = a.ResearchTech,
            ["patches"] = Patches(p.Node),
        };
        arr.Add(o);
    }
    var meta = new JsonObject
    {
        ["about"] = "Every piece of armour and apparel of the player's active mods rated by Rimfolk's apparel rating (Source/ColonyCouncil/Apparel/ApparelRateMath.cs); see docs/apparel/README.md.",
        ["referenceWearer"] = "A human colonist, Shooting 8 and Melee 8, no traits, with an assault rifle (a shooter) or a steel longsword (a melee fighter); the piece alone, at normal quality, in its usual stuff.",
        ["families"] = new JsonArray(ApparelRateMath.Families.Select(f => (JsonNode)new JsonObject
        {
            ["family"] = f, ["weight"] = ApparelRateMath.FamilyWeights[f],
            ["attacks"] = new JsonArray(ApparelRateMath.Threats.Where(t => t.Family == f).Select(t => (JsonNode)new JsonObject
            {
                ["label"] = t.Label, ["damage"] = Math.Round(t.Damage, 2), ["armourPen"] = Math.Round(t.Pen, 3), ["armour"] = t.Armour.ToString(),
                ["lands"] = t.Ranged ? "anywhere" : "outside", ["share"] = Math.Round(t.Weight, 4)
            }).ToArray())
        }).ToArray()),
        ["bodyZones"] = new JsonArray(ApparelRateMath.Human.Select(z => (JsonNode)new JsonObject { ["groups"] = Arr(z.Groups), ["ranged"] = z.Ranged, ["melee"] = z.Melee, ["meleeBlunt"] = z.MeleeBlunt }).ToArray()),
        ["roles"] = new JsonArray(ApparelRateMath.Roles.Select(role => (JsonNode)new JsonObject
        {
            ["role"] = role, ["mid"] = ApparelRateMath.RoleScale(role).mid, ["width"] = ApparelRateMath.RoleScale(role).width, ["weights"] = Obj(ApparelRateMath.RoleWeights(role), 2)
        }).ToArray()),
        ["tagCategories"] = new JsonArray(WeaponTagMath.Categories.Select(c => (JsonNode)new JsonObject
        {
            ["category"] = c.Category.ToString(), ["label"] = c.Label, ["colour"] = c.Hex, ["symbol"] = c.Symbol, ["icon"] = c.IconKey
        }).ToArray()),
        ["tagVocabulary"] = new JsonArray(ApparelTagMath.ApparelTags.Select(d => (JsonNode)new JsonObject
        {
            ["id"] = d.Id, ["label"] = d.Label, ["category"] = d.Category.ToString(), ["importance"] = d.Importance, ["rule"] = d.Rule
        }).ToArray()),
        ["counts"] = new JsonObject { ["defs"] = all.Count + leftOut.Count, ["rated"] = all.Count, ["leftOut"] = leftOut.Count, ["unsure"] = all.Count(p => p.Facts.Unsure.Count > 0) },
        ["anchors"] = new JsonArray(ApparelAnchors.All().Select(x => (JsonNode)new JsonObject { ["name"] = x.name, ["role"] = x.r.Role, ["rating"] = x.r.Overall, ["sub"] = Obj(x.r.Sub) }).ToArray()),
    };
    var lo = new JsonArray(leftOut.Select(x => (JsonNode)new JsonObject { ["defName"] = Inputs.S(x.n, "defName"), ["label"] = Inputs.S(x.n, "label"), ["mod"] = Inputs.S(x.n, "mod"), ["modName"] = Inputs.S(x.n, "modName"), ["reason"] = x.why }).ToArray());
    var pretty = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    var flat = new JsonSerializerOptions { WriteIndented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    var sb = new StringBuilder();
    void List(string name, JsonArray items, bool last)
    {
        sb.Append("  \"").Append(name).Append("\": [\n");
        for (int i = 0; i < items.Count; i++)
            sb.Append("    ").Append(items[i].ToJsonString(flat)).Append(i < items.Count - 1 ? ",\n" : "\n");
        sb.Append("  ]").Append(last ? "\n" : ",\n");
    }
    sb.Append("{\n  \"meta\": ").Append(meta.ToJsonString(pretty).Replace("\n", "\n  ")).Append(",\n");
    List("apparel", arr, false);
    List("leftOut", lo, true);
    sb.Append("}\n");
    return sb.ToString();
}

static JsonObject Obj(Dictionary<string, float> d, int digits = 2)
{
    var o = new JsonObject();
    foreach (var kv in d) o[kv.Key] = Math.Round(kv.Value, digits);
    return o;
}

static JsonArray Arr(IEnumerable<string> xs) => new JsonArray(xs.Select(x => (JsonNode)x).ToArray());

// The patches that touch a piece, the mods' names without the em dash one of them carries (no em dashes in player text).
static JsonArray Patches(JsonNode n)
{
    var arr = new JsonArray();
    foreach (var p in Inputs.Arr(n, "patches"))
    {
        var o = JsonNode.Parse(p.ToJsonString()).AsObject();
        o["modName"] = Inputs.S(p, "modName").Replace(" — ", " - ");
        arr.Add(o);
    }
    return arr;
}

/// <summary>One rated piece: its node, facts, rating, tags, line, per-stuff ratings and table.</summary>
public sealed class Piece
{
    public JsonNode Node;
    public ApparelFacts Facts;
    public ApparelRating Rating;
    public List<ThingTag> Tags;
    public string Plain;
    public Dictionary<string, float> ByStuff = new();
    public string Table;
}
