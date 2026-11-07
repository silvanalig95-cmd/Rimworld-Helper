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
/// class, gene, good and path is soft (MayRequire on it, or by name in the code) and really that mod's, in the player's own
/// mod list: Tools/Tests/PeopleGear.txt is built from the player's catalogue (docs/player-mods/, its active rows), and every
/// row of it and every name the peoples use is checked against the catalogue's active rows here, Rimfolk's own tags
/// (Patches/Rimfolk_PeoplesGear.xml) only on pieces of the player's active mods. A choice of the signature garment picks the
/// people's own pieces, and no kind's tags bring in a magic class's, another people's or (but for spacer soldiers) spacer
/// armour beyond half a hundredth, weighted as the game picks with the player's mods. The base game alone dresses and arms every kind, traders
/// carry nothing of their own into their stock, each people's kinds of people are a real mix that still reads as one
/// people, and the gifts' rules.
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
        public List<XElement> required = new(), tags = new(), specs = new(), weapons = new(), disallow = new();
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
        C(PeoplesGearMath.GiftKinds(true, false, false) == 1 && PeoplesGearMath.GiftKinds(false, false, true) == 1 && PeoplesGearMath.GiftKinds(true, true, false) == 2
          && PeoplesGearMath.GiftKinds(false, false, false) == 0, "a gift is one thing: a class, a psylink or genes");
        C(PeoplesGearMath.GenesFit("Industrial") && PeoplesGearMath.GenesFit("Spacer") && !PeoplesGearMath.GenesFit("Medieval") && !PeoplesGearMath.GenesFit("Neolithic")
          && !PeoplesGearMath.GenesFit(null), "genes put in only by the peoples whose age has the means");

        // ================= The player's own catalogue (docs/player-mods/, active rows) =================

        string catDir = Path.Combine(root, "docs", "player-mods");
        List<Dictionary<string, string>> Table(string file)
        {
            var lines = File.ReadAllLines(Path.Combine(catDir, file));
            var head = lines[0].Split('\t');
            return lines.Skip(1).Where(l => l.Length > 0).Select(l =>
            {
                var c = l.Split('\t');
                var row = new Dictionary<string, string>();
                for (int i = 0; i < head.Length; i++) row[head[i]] = i < c.Length ? c[i] : "";
                return row;
            }).ToList();
        }
        var mods = new Dictionary<string, bool>();
        foreach (var r in Table("mods.tsv")) mods[r["packageId"].ToLowerInvariant()] = r["active"] == "yes";
        bool ModOn(string id) => id.StartsWith("ludeon.rimworld") || mods.TryGetValue(id, out bool on) && on;
        Dictionary<string, Dictionary<string, string>> Active(string file) =>
            Table(file).Where(r => r["active"] == "yes").GroupBy(r => r["defName"]).ToDictionary(g => g.Key, g => g.Last());
        var catApparel = Active("apparel.tsv");
        var catWeapons = Active("weapons.tsv");
        var catXenos = Active("xenotypes.tsv");
        var catTraits = Active("traits.tsv");
        var catGenes = Active("genes.tsv");
        var catItems = Active("items.tsv");
        var catMemes = Active("memes.tsv");
        C(catApparel.Count > 400 && catWeapons.Count > 500 && catXenos.Count > 50 && catGenes.Count > 500 && catItems.Count > 2000 && mods.Count(m => m.Value) > 200,
            $"the player's catalogue is read ({catApparel.Count} pieces, {catWeapons.Count} weapons, {catXenos.Count} kinds of people of the game and their active mods)");
        string Src(string mod) => mod == "ludeon.rimworld" ? "core" : mod.StartsWith("ludeon.") ? mod : "mod:" + mod;
        string[] Split(string s) => (s ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        float Weight(Dictionary<string, string> r) =>
            float.TryParse(r.TryGetValue("commonality", out var c) ? c : "", NumberStyles.Float, CultureInfo.InvariantCulture, out float w) ? w : 1f;

        // Rimfolk's own tags, patched onto pieces by name (Patches/Rimfolk_PeoplesGear.xml).
        var patched = new Dictionary<string, List<string>>();
        var patchDoc = XDocument.Load(Path.Combine(root, "Patches", "Rimfolk_PeoplesGear.xml"));
        foreach (var op in patchDoc.Root!.Elements("Operation"))
        {
            string xp = op.Element("xpath")?.Value ?? "";
            if (!xp.EndsWith("/apparel/tags")) continue;
            foreach (Match m in Regex.Matches(xp, "defName=\"([^\"]+)\""))
                foreach (var li in op.Element("value")!.Elements("li"))
                {
                    if (!patched.TryGetValue(m.Groups[1].Value, out var l)) patched[m.Groups[1].Value] = l = new List<string>();
                    l.Add(li.Value);
                }
        }
        C(patched.Count >= 10 && patched.Values.SelectMany(v => v).All(t => t.StartsWith("Rimfolk_"))
          && patchDoc.Root.Elements("Operation").All(o => o.Element("success")?.Value == "Always"),
            "Rimfolk's own tags are Rimfolk's, and a patch whose piece is missing does nothing and logs nothing");
        foreach (var kv in patched)
            C(catApparel.ContainsKey(kv.Key), $"Rimfolk's {string.Join(", ", kv.Value)} goes on {kv.Key}, a piece of the game or the player's active mods");
        var rimfolkTags = patched.Values.SelectMany(v => v).ToHashSet();
        string[] CatTags(Dictionary<string, string> r, string col) =>
            Split(r[col]).Concat(col == "tags" && patched.TryGetValue(r["defName"], out var p) ? p : Enumerable.Empty<string>()).Distinct().ToArray();

        // ================= The list the checks read (Tools/Tests/PeopleGear.txt, built from the catalogue) =================

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
        C(items.Values.Count(i => i.Core && i.kind == "apparel") > 20 && items.Values.Count(i => i.source.StartsWith("mod:") && i.kind == "apparel") > 80
          && items.Values.Count(i => i.kind == "xenotype") > 30 && items.Values.Any(i => i.kind == "trait") && items.Values.Any(i => i.kind == "path")
          && items.Values.Any(i => i.kind == "gene") && items.Values.Any(i => i.kind == "item"),
            "the list of the game's and the mods' gear, kinds of people, gifts and goods is read");
        IEnumerable<Item> Of(string kind) => items.Values.Where(i => i.kind == kind);
        Item Find(string kind, string name) => items.TryGetValue(kind + ":" + name, out var i) ? i : null;

        // Every row of PeopleGear.txt is the player's: in the catalogue's active rows, from the same mod, with the same tags.
        foreach (var it in items.Values)
        {
            if (it.kind == "path")
            {
                C(it.source == "mod:vanillaexpanded.vpsycastse" && ModOn("vanillaexpanded.vpsycastse") && it.name.StartsWith("VPE_"),
                    $"PeopleGear.txt: {it.name} is Vanilla Psycasts Expanded's path, and it is in the player's list");
                continue;
            }
            Dictionary<string, string> row = null;
            bool found = it.kind switch
            {
                "apparel" => catApparel.TryGetValue(it.name, out row),
                "weapon" => catWeapons.TryGetValue(it.name, out row),
                "xenotype" => catXenos.TryGetValue(it.name, out row),
                "trait" => catTraits.TryGetValue(it.name, out row),
                "gene" => catGenes.TryGetValue(it.name, out row),
                "item" => catItems.TryGetValue(it.name, out row) || catApparel.TryGetValue(it.name, out row),
                _ => false
            };
            C(found && Src(row!["mod"]) == it.source,
                $"PeopleGear.txt: the {it.kind} {it.name} is {it.source}'s, in the player's active mods ({(found ? Src(row!["mod"]) : "not there")})");
            if (found && (it.kind == "apparel" || it.kind == "weapon"))
            {
                var tags = CatTags(row!, it.kind == "apparel" ? "tags" : "weaponTags");
                C(tags.OrderBy(t => t, StringComparer.Ordinal).SequenceEqual(it.tags.Distinct().OrderBy(t => t, StringComparer.Ordinal)),
                    $"PeopleGear.txt: {it.name}'s tags are the catalogue's ({string.Join(",", tags)})");
            }
        }

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
        // A tag is the game's or soft on its mod; Rimfolk's own tags are soft by their patch (a piece by name, nothing if missing).
        bool TagSoft(string kind, string tag, string mayRequire) =>
            tagSources.TryGetValue(kind + ":" + tag, out var src) && (rimfolkTags.Contains(tag) || src.Any(s => Covers(mayRequire, s)));

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
                weapons = Merged(k, parent, "weaponTags"), disallow = Merged(k, parent, "apparelDisallowTags"), trader = k.Element("trader")?.Value == "true",
                fighter = k.Element("isFighter")?.Value != "false", violent = (k.Element("requiredWorkTags")?.Value ?? "").Contains("Violent"),
                apparelMax = Max(k.Element("apparelMoney")?.Value ?? ""), weaponMax = Max(k.Element("weaponMoney")?.Value ?? "")
            });
        }
        C(kinds.Count >= 55 && factions.Count == 11 && kinds.All(k => factions.Any(f => Dn(f) == k.faction)), $"every kind of the eleven peoples read ({kinds.Count})");
        string TechOf(Kind k) => factions.First(f => Dn(f) == k.faction).Element("techLevel")!.Value;

        // Every piece of the player's active mods under a tag the peoples use is in PeopleGear.txt, so the checks below see
        // what the game can pick with the player's mods.
        IEnumerable<string> ChoiceTags(Kind k) => k.specs.SelectMany(s => s.Element("alternateTagChoices")?.Elements("li") ?? Enumerable.Empty<XElement>()).Select(li => li.Element("tag")!.Value);
        var usedApparelTags = kinds.SelectMany(k => k.tags.Select(li => li.Value).Concat(ChoiceTags(k)).Concat(k.disallow.Select(li => li.Value))).ToHashSet();
        var usedWeaponTags = kinds.SelectMany(k => k.weapons.Select(li => li.Value)).ToHashSet();
        var missing = catApparel.Values.Where(r => CatTags(r, "tags").Any(usedApparelTags.Contains) && Find("apparel", r["defName"]) == null).Select(r => r["defName"])
            .Concat(catWeapons.Values.Where(r => CatTags(r, "weaponTags").Any(usedWeaponTags.Contains) && Find("weapon", r["defName"]) == null).Select(r => r["defName"])).ToList();
        C(missing.Count == 0, $"PeopleGear.txt holds every piece and weapon of the player's active mods under the peoples' tags ({string.Join(", ", missing.Take(8))})");
        foreach (var t in rimfolkTags)
            C(usedApparelTags.Contains(t), $"Rimfolk's tag {t} is one a people's kind uses");

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
                // With the player's mods too: a choice of the signature garment (one any of whose pieces the people dye) lands
                // on the people's own pieces, as the game weighs them (a mod's piece under the same tag doesn't take its place).
                foreach (var s in k.specs.Where(s => s.Element("alternateTagChoices") != null && s.Element("color") == null))
                {
                    var tags = s.Element("alternateTagChoices")!.Elements("li").Select(li => li.Element("tag")!.Value).ToHashSet();
                    var cands = catApparel.Values.Where(r => CatTags(r, "tags").Any(tags.Contains)).ToList();
                    float total = cands.Sum(Weight), dyed = cands.Where(r => dyedDefs.Contains(r["defName"])).Sum(Weight);
                    if (dyed > 0f)
                        C(dyed / total >= 0.97f, $"{k.name}: its choice of {string.Join(", ", tags)} is its people's own pieces ({dyed / total:0.00} of it, with the player's mods)");
                }
            }
        }
        C(colours.Values.Distinct().Count() == 11, "eleven peoples, eleven colours");

        // ================= Every name soft and real, in the player's active mods =================

        foreach (var k in kindEls)
        {
            string kn = Dn(k) == "" ? k.Attribute("Name")?.Value ?? "?" : Dn(k);
            foreach (var e in k.DescendantsAndSelf().Where(e => e.Attribute("MayRequire") != null))
                foreach (var id in Req(e).Split(','))
                    C(ModOn(id.Trim()), $"{kn}: MayRequire {id.Trim()} is a mod in the player's list");
            foreach (var li in k.Element("apparelRequired")?.Elements("li") ?? Enumerable.Empty<XElement>())
            {
                var it = Find("apparel", li.Value);
                C(it != null && Covers(Req(li), it.source), $"{kn}: {li.Value} is {(it == null ? "unknown" : it.source)}, and needs nothing else to be worn");
                C(catApparel.ContainsKey(li.Value), $"{kn}: {li.Value} is in the player's active mods");
            }
            foreach (var li in k.Element("apparelTags")?.Elements("li") ?? Enumerable.Empty<XElement>())
                C(TagSoft("apparel", li.Value, Req(li)) && catApparel.Values.Any(r => CatTags(r, "tags").Contains(li.Value)),
                    $"{kn}: the apparel tag {li.Value} is the game's, Rimfolk's or soft on its mod, on a piece of the player's active mods");
            foreach (var li in k.Element("apparelDisallowTags")?.Elements("li") ?? Enumerable.Empty<XElement>())
                C(TagSoft("apparel", li.Value, Req(li)) && catApparel.Values.Any(r => CatTags(r, "tags").Contains(li.Value)), $"{kn}: it keeps out {li.Value}, a tag of the player's active mods");
            foreach (var li in k.Element("weaponTags")?.Elements("li") ?? Enumerable.Empty<XElement>())
                C(TagSoft("weapon", li.Value, Req(li)) && catWeapons.Values.Any(r => Split(r["weaponTags"]).Contains(li.Value)),
                    $"{kn}: the weapon tag {li.Value} is the game's or soft on its mod, on a weapon of the player's active mods");
            foreach (var s in k.Element("specificApparelRequirements")?.Elements("li") ?? Enumerable.Empty<XElement>())
            {
                string def = s.Element("apparelDef")?.Value;
                if (def != null)
                {
                    var it = Find("apparel", def);
                    C(it != null && Covers(Req(s), it.source) && catApparel.ContainsKey(def), $"{kn}: its {def} is {(it == null ? "unknown" : it.source)}, soft on it, in the player's active mods");
                }
                foreach (var tc in s.Element("alternateTagChoices")?.Elements("li") ?? Enumerable.Empty<XElement>())
                {
                    string tag = tc.Element("tag")!.Value;
                    C(TagSoft("apparel", tag, Reqs(s, tc)) && catApparel.Values.Any(r => CatTags(r, "tags").Contains(tag)), $"{kn}: its choice of {tag} is the game's, Rimfolk's or soft on its mod");
                    C(float.Parse(tc.Element("chance")!.Value, CultureInfo.InvariantCulture) > 0f, $"{kn}: its choice of {tag} can be taken");
                }
                C(s.Element("apparelLayer") == null || new[] { "Shell", "Middle", "OnSkin", "Overhead", "Belt" }.Contains(s.Element("apparelLayer")!.Value), $"{kn}: a layer of the game's own");
            }
            foreach (var t in k.Descendants("thingDef"))
            {
                var it = Find("item", t.Value);
                var li = t.Parent!;
                C(it != null && Covers(Req(li), it.source) && (catItems.ContainsKey(t.Value) || catApparel.ContainsKey(t.Value)),
                    $"{kn}: carries {t.Value}, {(it == null ? "unknown" : it.source)}'s, soft on it, in the player's active mods");
            }
            C(k.Element("fixedInventory") == null || !k.Element("fixedInventory")!.HasElements, $"{kn}: what it carries by chance (inventoryOptions), not a fixed load");
        }
        foreach (var f in factions)
            foreach (var li in (f.Element("forcedMemes")?.Elements("li") ?? Enumerable.Empty<XElement>()).Concat(f.Element("disallowedMemes")?.Elements("li") ?? Enumerable.Empty<XElement>()))
                C(catMemes.TryGetValue(li.Value, out var m) && Covers(Req(li), Src(m["mod"])), $"{Dn(f)}: the meme {li.Value} is in the player's active mods, soft on its mod");

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

        // ================= Nothing of another calling, with the player's mods =================

        // A magic class's garb (A RimWorld of Magic's arcane pieces), another people's (the elves', Alpha Genes' forsaken),
        // prestige armour, and spacer armour for any but a spacer people's fighters: no more than half a hundredth of what a kind's
        // tags and choices can give, as the game weighs them (a piece's own commonality), unless the kind names that tag.
        bool Foreign(Dictionary<string, string> r, Kind k, HashSet<string> named)
        {
            var t = CatTags(r, "tags");
            bool Marked(string tag) => t.Contains(tag) && !named.Contains(tag);
            if (Marked("Arcane") || Marked("PrestigeCombatGear") || Marked("Forsaken")) return true;
            // The elves' gear carries a tag of its own for each piece: theirs unless a kind names one.
            if (r["mod"] == "devdesigner.elves.continued" && !t.Any(x => x.StartsWith("DevDesigner_") && named.Contains(x))) return true;
            // Spacer armour: the spacer military's alone (the game's psychic foil helmet is every age's militia's too).
            bool spacerOnly = Marked("SpacerMilitary") && !t.Any(x => x.StartsWith("Industrial"));
            return spacerOnly && !(TechOf(k) == "Spacer" && k.fighter);
        }
        foreach (var k in kinds)
        {
            var named = k.tags.Select(li => li.Value).Concat(ChoiceTags(k)).ToHashSet();
            var keptOut = k.disallow.Select(li => li.Value).ToHashSet();
            var pool = catApparel.Values.Where(r => CatTags(r, "tags").Any(named.Contains) && !CatTags(r, "tags").Any(keptOut.Contains)).ToList();
            float total = pool.Sum(Weight), foreign = pool.Where(r => Foreign(r, k, named)).Sum(Weight);
            C(total <= 0f || foreign / total <= 0.005f,
                $"{k.name}: nothing of another calling or age among what its tags give ({foreign / Math.Max(total, 0.001f):0.000}: {string.Join(", ", pool.Where(r => Foreign(r, k, named)).Select(r => r["defName"]).Take(6))})");
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
        C(kindEls.SelectMany(k => k.Descendants("thingDef")).Count(t => Find("item", t.Value) is Item i && !i.Core) >= 8,
            "what they carry draws on the player's mods too (Alpha Crafts, Alpha Animals, Medieval 2, Odyssey)");

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
                C(it != null && Req(x).Split(',').Contains(Biotech.ToLowerInvariant()) && Covers(Req(x), it.source) && catXenos.ContainsKey(x.Name.LocalName),
                    $"{fd}: {x.Name.LocalName} is {(it == null ? "unknown" : it.source)}'s, in the player's active mods, only with Biotech and it");
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
            string tech = factions.First(f => Dn(f) == (kindEls.FirstOrDefault(p => p.Attribute("Name")?.Value == k.Attribute("ParentName")?.Value)?.Element("defaultFactionDef")?.Value)).Element("techLevel")!.Value;
            foreach (var g in gifts)
            {
                string trait = g.Element("trait")?.Value;
                var genes = g.Element("genes")?.Elements("li").Select(li => li.Value).ToList() ?? new List<string>();
                C(PeoplesGearMath.GiftKinds(trait != null, g.Element("psylink") != null, genes.Count > 0) == 1, $"{Dn(k)}: each gift is one thing");
                if (trait != null)
                    C(string.Equals(Find("trait", trait)?.source, "mod:torann.arimworldofmagic", StringComparison.OrdinalIgnoreCase) && catTraits.ContainsKey(trait)
                      && PeoplesGearMath.MagicSetting(trait) != null, $"{Dn(k)}: {trait} is A RimWorld of Magic's class, given by name, in the player's mods");
                else if (genes.Count > 0)
                {
                    C(PeoplesGearMath.GenesFit(tech), $"{Dn(k)}: genes put in by a people of the {tech} age, which has the means");
                    foreach (string gene in genes)
                        C(Find("gene", gene) != null && catGenes.ContainsKey(gene), $"{Dn(k)}: the gene {gene} is in the player's active mods, given by name");
                }
                else
                {
                    C(g.Element("psylink") != null && Regex.IsMatch(g.Element("psylink")!.Value, @"^[1-6]~[1-6]$"), $"{Dn(k)}: a psylink of a few levels");
                    foreach (var path in g.Element("paths")?.Elements("li") ?? Enumerable.Empty<XElement>())
                        C(string.Equals(Find("path", path.Value)?.source, "mod:vanillaexpanded.vpsycastse", StringComparison.OrdinalIgnoreCase),
                            $"{Dn(k)}: {path.Value} is Vanilla Psycasts Expanded's path, read by name");
                }
            }
        }
        C(giftKinds.Count >= 20 && factions.All(f => kinds.Any(k => k.faction == Dn(f) && giftKinds.Any(g => Dn(g) == k.name))), "every people has a few gifted among it");
        bool Gifted(string kind, string what) => kindEls.Any(k => Dn(k) == kind && k.Descendants().Any(e => e.Value == what && !e.HasElements));
        C(Gifted("Rimfolk_Order_Healer", "Priest") && Gifted("Rimfolk_Pilgrims_Warden", "Paladin") && Gifted("Rimfolk_RiverFolk_Master", "Druid")
          && Gifted("Rimfolk_Scholars_Don", "Arcanist") && Gifted("Rimfolk_Enclave_Chair", "VPE_Harmonist") && Gifted("Rimfolk_Herders_Archer", "Ranger")
          && Gifted("Rimfolk_Pilgrims_Pilgrim", "TM_Monk") && Gifted("Rimfolk_League_Wayfarer", "TM_Wayfarer") && Gifted("Rimfolk_Scholars_Don", "Learning_Fast"),
            "by calling, age and faith: hospitaller priests, warden paladins, river druids, scholar arcanists, an enclave of psycasters, herd rangers, pilgrim monks, road wayfarers, quick-study scholars");
        // Medieval and tribal peoples get no psylinks; the spacers and the learned do. Release D's design names two more: the
        // Wardens' old tracker may be a Wildspeaker, and the Circuit has Wildspeakers in the spacer age (that gift of that age only).
        foreach (var f in factions)
        {
            string tech = f.Element("techLevel")!.Value;
            bool psy = kinds.Where(k => k.faction == Dn(f)).Any(k => k.def.Descendants("psylink").Any());
            C(psy == (tech == "Industrial" && Dn(f) == "Rimfolk_Scholars" || tech == "Spacer" || Dn(f) == "Rimfolk_Wardens" || Dn(f) == "Rimfolk_Tamers"),
                $"{Dn(f)}: psylinks only where the age and calling have them");
            bool genes = kinds.Where(k => k.faction == Dn(f)).Any(k => k.def.Descendants("genes").Any());
            C(!genes || PeoplesGearMath.GenesFit(tech), $"{Dn(f)}: genes put in only where the age has the means");
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
        C(gifts2.Contains("DefDatabase<GeneDef>.GetNamedSilentFail(name)") && gifts2.Contains("XenotypeDefOf.Baseliner") && gifts2.Contains("ConflictsWith(def)") && gifts2.Contains("AddGene(def, true)"),
            "genes by name, only into a baseliner, never one that conflicts, as xenogenes");
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
