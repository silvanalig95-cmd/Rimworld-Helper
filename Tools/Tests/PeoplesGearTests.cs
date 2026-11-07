using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The gear, kinds of people and gifts of Rimfolk's seven peoples (Defs/PeopleDefs/; Peoples/PeopleGifts.cs,
/// PeoplesGearMath.cs; docs/design/peoples.md "Their gear, their kinds of people and their gifts"): no people dresses every
/// kind in one fixed colour, each wears one signature colour on a piece or two, every kind has real variety (several
/// tags or alternatives, not one required outfit), the soldiers keep a common kit, every mod's def, tag, kind of people,
/// class and path is soft (MayRequire on it, or by name in the code) and really that mod's (Tools/Tests/PeopleGear.txt,
/// read from the mods' own defs), the base game alone dresses and arms every kind, traders carry nothing of their own into
/// their stock, each people's kinds of people are a real mix that still reads as one people, and the gifts' rules.
/// </summary>
public static class PeoplesGearTests
{
    private const string Biotech = "Ludeon.RimWorld.Biotech";

    private sealed class Item
    {
        public string source = "", kind = "", name = "";
        public string[] tags = Array.Empty<string>(), groups = Array.Empty<string>(), layers = Array.Empty<string>();
        public float price = -1f;
        public bool Core => source == "core";
    }

    /// <summary>One of the peoples' pawn kinds, its parent's lists merged in as the game's XML inheritance does.</summary>
    private sealed class Kind
    {
        public string name = "", faction = "";
        public XElement def = null!;
        public List<XElement> required = new(), tags = new(), specs = new(), weapons = new();
        public bool trader, fighter, violent;
        public float apparelMax, weaponMax;
    }

    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "peoples' gear: " + what);
        }

        // ================= The gifts' rules =================

        var shares = new List<float> { 0.1f, 0.2f };
        C(PeoplesGearMath.Pick(shares, 0f) == 0 && PeoplesGearMath.Pick(shares, 0.099f) == 0 && PeoplesGearMath.Pick(shares, 0.1f) == 1
          && PeoplesGearMath.Pick(shares, 0.299f) == 1 && PeoplesGearMath.Pick(shares, 0.3f) == -1 && PeoplesGearMath.Pick(shares, 0.99f) == -1,
            "one roll against the shares laid end to end: most of a kind has no gift");
        C(PeoplesGearMath.Pick(new List<float> { float.NaN, 0.2f }, 0.1f) == 1 && PeoplesGearMath.Pick(null, 0.1f) == -1 && PeoplesGearMath.Pick(shares, float.NaN) == -1,
            "a bad share counts as none, a bad roll gives nothing");
        C(PeoplesGearMath.Sound(shares) && !PeoplesGearMath.Sound(new List<float> { 0.4f, 0.3f }) && !PeoplesGearMath.Sound(new List<float> { 0f })
          && !PeoplesGearMath.Sound(new List<float>()) && !PeoplesGearMath.Sound(new List<float> { float.NaN }), "a share of a kind, never all of it");
        C(PeoplesGearMath.Levels(1, 3, 0f) == 1 && PeoplesGearMath.Levels(1, 3, 0.999f) == 3 && PeoplesGearMath.Levels(2, 4, 0.5f) == 3
          && PeoplesGearMath.Levels(0, 9, 0.999f) == 6 && PeoplesGearMath.Levels(3, 1, 0.5f) == 3 && PeoplesGearMath.Levels(1, 2, float.NaN) == 1,
            "a psylink of the kind's levels, from 1 to the game's 6");
        C(PeoplesGearMath.Powers(1) == 1 && PeoplesGearMath.Powers(3) == 2 && PeoplesGearMath.Powers(6) == 3 && PeoplesGearMath.Powers(0) == 1,
            "a psycaster knows a few of its path's powers by its levels");
        C(PeoplesGearMath.MagicSetting("Priest") == "Priest" && PeoplesGearMath.MagicSetting("InnerFire") == "FireMage" && PeoplesGearMath.MagicSetting("TM_Sniper") == "Sniper"
          && PeoplesGearMath.MagicSetting("Lich") == null && PeoplesGearMath.MagicSetting(null) == null, "each class by A RimWorld of Magic's own switch for it; no lich");
        C(PeoplesGearMath.Might("Ranger") && PeoplesGearMath.Might("TM_Commander") && !PeoplesGearMath.Might("Druid") && !PeoplesGearMath.Might(null)
          && PeoplesGearMath.MightClasses.All(PeoplesGearMath.MagicClasses.ContainsKey), "fighters and mages told apart");
        C(PeoplesGearMath.FactionAllows(null) && PeoplesGearMath.FactionAllows(1f) && !PeoplesGearMath.FactionAllows(0f) && PeoplesGearMath.FactionAllows(float.NaN),
            "the mod's own share for a people: none at 0");

        // ================= The catalogue =================

        var items = new Dictionary<string, Item>();
        foreach (var line in File.ReadAllLines(Path.Combine(root, "Tools", "Tests", "PeopleGear.txt")))
        {
            if (line.StartsWith("#") || line.Trim().Length == 0) continue;
            var c = line.Split('\t');
            var it = new Item { source = c[0], kind = c[1], name = c[2] };
            if (c.Length > 3 && c[3] != "-") it.tags = c[3].Split(',');
            if (c.Length > 4 && c[4] != "-")
            {
                var gl = c[4].Split('|');
                it.groups = gl[0].Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (gl.Length > 1) it.layers = gl[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
            }
            if (c.Length > 5 && c[5] != "-") it.price = float.Parse(c[5], CultureInfo.InvariantCulture);
            items[it.kind + ":" + it.name] = it;
        }
        C(items.Values.Count(i => i.Core && i.kind == "apparel") > 30 && items.Values.Count(i => i.source.StartsWith("mod:") && i.kind == "apparel") > 60
          && items.Values.Count(i => i.kind == "xenotype") > 30 && items.Values.Any(i => i.kind == "trait") && items.Values.Any(i => i.kind == "path"),
            "the catalogue of the game's and the mods' gear, kinds of people and gifts is read");
        IEnumerable<Item> Of(string kind) => items.Values.Where(i => i.kind == kind);
        Item Find(string kind, string name) => items.TryGetValue(kind + ":" + name, out var i) ? i : null;
        // The sources a tag comes from, and the items behind it.
        var tagSources = new Dictionary<string, HashSet<string>>();
        foreach (var i in Of("apparel").Concat(Of("weapon")))
            foreach (var t in i.tags)
            {
                if (!tagSources.TryGetValue(i.kind + ":" + t, out var set)) tagSources[i.kind + ":" + t] = set = new HashSet<string>();
                set.Add(i.source);
            }

        string Req(XElement e) => (e.Attribute("MayRequire")?.Value ?? "").ToLowerInvariant();
        string Reqs(params XElement[] es) => string.Join(",", es.Where(e => e != null).Select(Req));
        bool Covers(string mayRequire, string source)
        {
            if (source == "core") return true;
            string id = (source.StartsWith("mod:") ? source.Substring(4) : source).ToLowerInvariant();
            return mayRequire.Split(',').Any(r => r.Trim() == id);
        }

        // ================= The peoples' kinds =================

        string peopleDir = Path.Combine(root, "Defs", "PeopleDefs");
        var all = Directory.GetFiles(peopleDir, "Rimfolk_People_*.xml").OrderBy(f => f).SelectMany(f => XDocument.Load(f).Root!.Elements()).ToList();
        var factions = all.Where(e => e.Name.LocalName == "FactionDef").ToList();
        var kindEls = all.Where(e => e.Name.LocalName == "PawnKindDef").ToList();
        string Dn(XElement e) => e.Element("defName")?.Value ?? "";
        float Max(string range) => string.IsNullOrEmpty(range) ? 0f : float.Parse(range.Split('~').Last(), CultureInfo.InvariantCulture);
        List<XElement> Merged(XElement k, XElement parent, string list)
        {
            var own = k.Element(list);
            var mine = own?.Elements("li").ToList() ?? new List<XElement>();
            if (own?.Attribute("Inherit")?.Value == "False" || parent?.Element(list) == null) return mine;
            return parent.Element(list)!.Elements("li").Concat(mine).ToList();
        }
        var kinds = new List<Kind>();
        foreach (var k in kindEls.Where(k => k.Attribute("Abstract") == null))
        {
            var parent = kindEls.FirstOrDefault(p => p.Attribute("Name")?.Value == k.Attribute("ParentName")?.Value);
            kinds.Add(new Kind
            {
                name = Dn(k), def = k, faction = parent?.Element("defaultFactionDef")?.Value ?? k.Element("defaultFactionDef")?.Value ?? "",
                required = Merged(k, parent, "apparelRequired"), tags = Merged(k, parent, "apparelTags"), specs = Merged(k, parent, "specificApparelRequirements"),
                weapons = Merged(k, parent, "weaponTags"), trader = k.Element("trader")?.Value == "true", fighter = k.Element("isFighter")?.Value != "false",
                violent = (k.Element("requiredWorkTags")?.Value ?? "").Contains("Violent"), apparelMax = Max(k.Element("apparelMoney")?.Value ?? ""),
                weaponMax = Max(k.Element("weaponMoney")?.Value ?? "")
            });
        }
        C(kinds.Count >= 55 && factions.Count == 11 && kinds.All(k => factions.Any(f => Dn(f) == k.faction)), $"every kind of the eleven peoples read ({kinds.Count})");

        // ================= No fixed colour on everything; one signature colour a people =================

        C(kindEls.All(k => k.Element("apparelColor") == null), "no kind dyes everything it wears one colour (no apparelColor)");
        bool ColourOnly(XElement s) => s.Element("color") != null && s.Element("alternateTagChoices") == null && s.Element("requiredTag") == null;
        bool Filtered(XElement s) => s.Element("apparelDef") != null || s.Element("apparelLayer") != null || s.Element("bodyPartGroup") != null;
        C(kindEls.SelectMany(k => k.Element("specificApparelRequirements")?.Elements("li") ?? Enumerable.Empty<XElement>()).Where(s => s.Element("color") != null).All(Filtered),
            "a colour only ever on a named piece or one layer of the body, never on all of it");
        var colours = new Dictionary<string, string>();
        foreach (var f in factions)
        {
            string fd = Dn(f);
            var mine = kinds.Where(k => k.faction == fd).ToList();
            var cs = mine.SelectMany(k => k.specs).Select(s => s.Element("color")?.Value).Where(v => v != null).Select(v => Regex.Replace(v!, @"\s", "")).Distinct().ToList();
            C(cs.Count == 1, $"{fd}: one signature colour ({string.Join(" ", cs)})");
            if (cs.Count == 1) colours[fd] = cs[0];
            // Each kind sure to wear it, with the game and its DLCs: a piece of its colour it is given, or a coat of it whatever is picked.
            var dyedDefs = mine.SelectMany(k => k.specs).Where(s => ColourOnly(s) && s.Element("apparelDef") != null).Select(s => s.Element("apparelDef")!.Value).ToHashSet();
            foreach (var k in mine)
            {
                bool viaFilter = k.specs.Any(s => s.Element("color") != null && s.Element("alternateTagChoices") != null && Req(s) == "");
                bool viaRequired = k.required.Any(li => dyedDefs.Contains(li.Value) && (Req(li) == "" || Req(li).StartsWith("ludeon.rimworld")));
                bool viaChoice = k.specs.Where(s => s.Element("alternateTagChoices") != null && s.Element("color") == null && Req(s) == "").Any(s =>
                {
                    var tags = s.Element("alternateTagChoices")!.Elements("li").Where(li => Req(li) == "").Select(li => li.Element("tag")!.Value).ToList();
                    var cands = Of("apparel").Where(i => i.Core && i.tags.Intersect(tags).Any()).ToList();
                    return cands.Count > 0 && cands.All(i => dyedDefs.Contains(i.name));
                });
                C(viaFilter || viaRequired || viaChoice, $"{k.name}: wears its people's colour on a piece of its own");
            }
        }
        C(colours.Values.Distinct().Count() == 11, "eleven peoples, eleven colours");

        // ================= Every name soft and real =================

        var coreThings = new HashSet<string> { "MedicineHerbal", "MedicineIndustrial", "Cloth", "Hay", "Leather_Plain", "Beer", "Silver", "Novel", "TextBook", "Schematic",
            "ComponentIndustrial", "Chemfuel", "WoodLog",
            // Release D's: the Wardens' pemmican, the Circuit's kibble, the Wheel's steel.
            "Pemmican", "Kibble", "Steel" };
        foreach (var k in kindEls)
        {
            string kn = Dn(k) == "" ? k.Attribute("Name")?.Value ?? "?" : Dn(k);
            foreach (var li in k.Element("apparelRequired")?.Elements("li") ?? Enumerable.Empty<XElement>())
            {
                var it = Find("apparel", li.Value);
                C(it != null && Covers(Req(li), it.source), $"{kn}: {li.Value} is {(it == null ? "unknown" : it.source)}, and needs nothing else to be worn");
            }
            foreach (var li in k.Element("apparelTags")?.Elements("li") ?? Enumerable.Empty<XElement>())
                C(tagSources.TryGetValue("apparel:" + li.Value, out var src) && src.Any(s => Covers(Req(li), s)), $"{kn}: the apparel tag {li.Value} is the game's or soft on its mod");
            foreach (var li in k.Element("weaponTags")?.Elements("li") ?? Enumerable.Empty<XElement>())
                C(tagSources.TryGetValue("weapon:" + li.Value, out var src) && src.Any(s => Covers(Req(li), s)), $"{kn}: the weapon tag {li.Value} is the game's or soft on its mod");
            foreach (var s in k.Element("specificApparelRequirements")?.Elements("li") ?? Enumerable.Empty<XElement>())
            {
                string def = s.Element("apparelDef")?.Value;
                if (def != null)
                {
                    var it = Find("apparel", def);
                    C(it != null && Covers(Req(s), it.source), $"{kn}: its {def} is {(it == null ? "unknown" : it.source)}, soft on it");
                }
                foreach (var tc in s.Element("alternateTagChoices")?.Elements("li") ?? Enumerable.Empty<XElement>())
                {
                    string tag = tc.Element("tag")!.Value;
                    C(tagSources.TryGetValue("apparel:" + tag, out var src) && src.Any(x => Covers(Reqs(s, tc), x)), $"{kn}: its choice of {tag} is the game's or soft on its mod");
                    C(float.Parse(tc.Element("chance")!.Value, CultureInfo.InvariantCulture) > 0f, $"{kn}: its choice of {tag} can be taken");
                }
                C(s.Element("apparelLayer") == null || new[] { "Shell", "Middle", "OnSkin", "Overhead", "Belt" }.Contains(s.Element("apparelLayer")!.Value), $"{kn}: a layer of the game's own");
            }
            foreach (var t in k.Descendants("thingDef"))
                C(coreThings.Contains(t.Value) || (Find("apparel", t.Value) is Item ia && ia.Core), $"{kn}: carries {t.Value}, the base game's");
            C(k.Element("fixedInventory") == null || !k.Element("fixedInventory")!.HasElements, $"{kn}: what it carries by chance (inventoryOptions), not a fixed load");
        }

        // ================= Real variety, and the soldiers' common kit =================

        IEnumerable<Item> Behind(IEnumerable<XElement> tagLis, string kind) =>
            tagLis.SelectMany(li => Of(kind).Where(i => i.tags.Contains(li.Name.LocalName == "li" && li.Element("tag") != null ? li.Element("tag")!.Value : li.Value)));
        foreach (var k in kinds)
        {
            var choices = k.specs.Where(s => s.Element("alternateTagChoices") != null).SelectMany(s => s.Element("alternateTagChoices")!.Elements("li")).ToList();
            var candidates = Behind(k.tags, "apparel").Concat(Behind(choices, "apparel")).Select(i => i.name).Distinct().ToList();
            C(candidates.Count >= 3, $"{k.name}: real variety, {candidates.Count} pieces its tags and choices can give, not one required outfit");
            if (!k.trader && k.weapons.Count > 0)
                C(Behind(k.weapons, "weapon").Select(i => i.name).Distinct().Count() >= 2, $"{k.name}: more than one weapon its tags can give");
            if (k.violent)
                C(k.required.Count(li => Req(li) == "" || Req(li).StartsWith("ludeon.rimworld")) >= 1 || k.specs.Any(s => s.Element("alternateTagChoices") != null && Req(s) == ""),
                    $"{k.name}: a fighter, in its people's common kit (a piece every one of them wears)");
            if (!k.fighter && !k.trader && k.def.Element("factionLeader") == null)
                C(k.tags.Count >= 1 || choices.Count >= 2, $"{k.name}: civilians vary freely around their signature");
        }
        foreach (var f in factions)
        {
            var mine = kinds.Where(k => k.faction == Dn(f)).ToList();
            var kits = mine.Select(k => string.Join("|", k.required.Select(li => li.Value).OrderBy(x => x)) + "/" + string.Join("|", k.tags.Select(li => li.Value).OrderBy(x => x))).Distinct().Count();
            C(kits >= mine.Count - 1, $"{Dn(f)}: its kinds dress apart by role ({kits} kits for {mine.Count} kinds)");
        }

        // ================= The base game alone dresses and arms every kind =================

        foreach (var k in kinds)
        {
            var coreRequired = k.required.Where(li => Req(li) == "").Select(li => Find("apparel", li.Value)).Where(i => i != null && i.Core).ToList();
            var coreChoices = k.specs.Where(s => s.Element("alternateTagChoices") != null && Req(s) == "")
                .SelectMany(s => s.Element("alternateTagChoices")!.Elements("li").Where(li => Req(li) == "")).Select(li => li.Element("tag")!.Value).ToList();
            var coreTags = k.tags.Where(li => Req(li) == "").Select(li => li.Value).ToList();
            var possible = Of("apparel").Where(i => i.Core && (coreChoices.Intersect(i.tags).Any() || (coreTags.Intersect(i.tags).Any() && i.price >= 0 && i.price <= k.apparelMax))).ToList();
            bool Body(string part) => coreRequired.Any(i => i!.groups.Contains(part)) || possible.Any(i => i.groups.Contains(part));
            C(Body("Torso") && Body("Legs"), $"{k.name}: dressed by the base game alone, body and legs");
            if (k.weapons.Count > 0 || k.violent)
            {
                var coreWeapons = k.weapons.Where(li => Req(li) == "").Select(li => li.Value).ToList();
                C(Of("weapon").Any(i => i.Core && i.tags.Intersect(coreWeapons).Any() && i.price >= 0 && i.price <= k.weaponMax),
                    $"{k.name}: armed by the base game alone within its means ({k.weaponMax})");
            }
        }

        // ================= What they carry =================

        foreach (var k in kinds.Where(k => k.trader))
            C(k.def.Element("inventoryOptions") == null && k.def.Element("fixedInventory") == null, $"{k.name}: a trader carries nothing of its own, so its stock stays the trader's");
        C(kinds.Count(k => k.def.Element("inventoryOptions") != null) >= 12, "most of the peoples' kinds carry something of their calling");

        // ================= Their kinds of people =================

        foreach (var f in factions)
        {
            string fd = Dn(f);
            var xs = f.Element("xenotypeSet")?.Element("xenotypeChances")?.Elements().ToList() ?? new List<XElement>();
            float sum = xs.Sum(x => float.Parse(x.Value, CultureInfo.InvariantCulture));
            var sources = new HashSet<string>();
            foreach (var x in xs)
            {
                var it = Find("xenotype", x.Name.LocalName);
                C(it != null && Req(x).Split(',').Contains(Biotech.ToLowerInvariant()) && Covers(Req(x), it.source),
                    $"{fd}: {x.Name.LocalName} is {(it == null ? "unknown" : it.source)}'s, only with Biotech and it");
                if (it != null) sources.Add(it.source);
            }
            C(f.Element("xenotypeSet")?.Attribute("Inherit")?.Value == "False", $"{fd}: its own mix, none of its parent's");
            C(xs.Count >= 6 && sources.Count >= 3, $"{fd}: a real mix of kinds of people ({xs.Count} from {sources.Count} sources)");
            C(sum <= 0.35f && sum >= 0.15f, $"{fd}: baseliners common enough that it reads as one people ({1 - sum:0.00})");
            C(xs.Count(x => Req(x) == Biotech.ToLowerInvariant()) >= 2, $"{fd}: Biotech alone still gives it a mix");
        }

        // ================= Their gifts =================

        var giftKinds = kindEls.Where(k => k.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.PeopleKindExtension")).ToList();
        foreach (var k in giftKinds)
        {
            var gifts = k.Descendants("gifts").Single().Elements("li").ToList();
            var chances = gifts.Select(g => float.Parse(g.Element("chance")!.Value, CultureInfo.InvariantCulture)).ToList();
            C(PeoplesGearMath.Sound(chances), $"{Dn(k)}: its gifts for a share of it, never all ({chances.Sum():0.00})");
            foreach (var g in gifts)
            {
                string trait = g.Element("trait")?.Value;
                if (trait != null)
                    C(Find("trait", trait)?.source == "mod:Torann.ARimworldOfMagic" && PeoplesGearMath.MagicSetting(trait) != null && g.Element("psylink") == null,
                        $"{Dn(k)}: {trait} is A RimWorld of Magic's class, given by name");
                else
                {
                    C(g.Element("psylink") != null && Regex.IsMatch(g.Element("psylink")!.Value, @"^[1-6]~[1-6]$"), $"{Dn(k)}: a psylink of a few levels");
                    foreach (var path in g.Element("paths")?.Elements("li") ?? Enumerable.Empty<XElement>())
                        C(Find("path", path.Value)?.source == "mod:VanillaExpanded.VPsycastsE", $"{Dn(k)}: {path.Value} is Vanilla Psycasts Expanded's path, read by name");
                }
            }
        }
        C(giftKinds.Count >= 20 && factions.All(f => kinds.Any(k => k.faction == Dn(f) && giftKinds.Any(g => Dn(g) == k.name))), "every people has a few gifted among it");
        bool Gifted(string kind, string what) => kindEls.Any(k => Dn(k) == kind && k.Descendants().Any(e => e.Value == what && !e.HasElements));
        C(Gifted("Rimfolk_Order_Healer", "Priest") && Gifted("Rimfolk_Pilgrims_Warden", "Paladin") && Gifted("Rimfolk_RiverFolk_Master", "Druid")
          && Gifted("Rimfolk_Scholars_Don", "Arcanist") && Gifted("Rimfolk_Enclave_Chair", "VPE_Harmonist") && Gifted("Rimfolk_Herders_Archer", "Ranger"),
            "by calling, age and faith: hospitaller priests, warden paladins, river druids, scholar arcanists, an enclave of psycasters, herd rangers");
        // Medieval and tribal peoples get no psylinks; the spacers and the learned do. Release D's design names two more: the
        // Wardens' old tracker may be a Wildspeaker, and the Circuit has Wildspeakers in the spacer age (that gift of that age only).
        foreach (var f in factions)
        {
            string tech = f.Element("techLevel")!.Value;
            bool psy = kinds.Where(k => k.faction == Dn(f)).Any(k => k.def.Descendants("psylink").Any());
            C(psy == (tech == "Industrial" && Dn(f) == "Rimfolk_Scholars" || tech == "Spacer" || Dn(f) == "Rimfolk_Wardens" || Dn(f) == "Rimfolk_Tamers"),
                $"{Dn(f)}: psylinks only where the age and calling have them");
        }
        C(kindEls.Where(k => Dn(k).StartsWith("Rimfolk_Tamers_")).SelectMany(k => k.Descendants("psylink")).All(p => p.Parent!.Element("ages")?.Value == "3~3"),
            "the Circuit's psylinks only in the spacer age");
        C(kindEls.Where(k => Dn(k).StartsWith("Rimfolk_Wardens_")).Where(k => k.Descendants("psylink").Any()).Select(Dn).SequenceEqual(new[] { "Rimfolk_Wardens_Master" })
          && kindEls.Where(k => Dn(k).StartsWith("Rimfolk_Wardens_")).SelectMany(k => k.Descendants("paths")).SelectMany(p => p.Elements("li")).All(p => p.Value == "VPE_Wildspeaker"),
            "the Wardens' only psylink is their lantern-master's, a Wildspeaker's");

        // ================= The seams =================

        string srcDir = Path.Combine(root, "Source", "ColonyCouncil", "Peoples");
        string gifts2 = File.ReadAllText(Path.Combine(srcDir, "PeopleGifts.cs"));
        C(gifts2.Contains("[HarmonyPatch(typeof(PawnGenerator), \"GenerateNewPawnInternal\")]") && gifts2.Contains("!RimfolkPatches.On"),
            "the gifts given as a fresh pawn is made, never a pawn already in a game, with the deeper game hooks on");
        C(gifts2.Contains("GetNamedSilentFail(name)") && gifts2.Contains("GetNamedSilentFail(\"VPE_PsycastAbilityImplant\")") && gifts2.Contains("ChangePsylinkLevel(1, false)"),
            "classes and paths by name, nothing when their mod is missing; a psylink with no letter");
        C(!gifts2.Contains("LetterStack") && !gifts2.Contains("Messages.Message"), "no letter and no message");
        C(File.ReadAllText(Path.Combine(srcDir, "PeoplesDebug.cs")).Contains("their gear, kinds of people and gifts (log)") && File.ReadAllText(Path.Combine(srcDir, "PeoplesDebug.cs")).Contains("as visitors (testing)"),
            "dev actions to see each people's gear");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
