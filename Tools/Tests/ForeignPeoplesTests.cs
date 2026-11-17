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
/// The Stormriders' gear, looks and magic (the player's request of 17 November 2026; Peoples/Foreign/; Defs/PeopleDefs/
/// Rimfolk_ForeignPeoples.xml; DESIGN.md "The Stormriders: their gear, their looks and some magic"): the pure rules (a
/// kind's role, a pawn's look, when a piece may take another's place, styles by gender, the sidearm, the gifts with and
/// without A RimWorld of Magic and their caps); the dark elves' profile against the player's own mods (docs/player-mods,
/// active rows): every name real and under the mod its MayRequire names, every dark elf kind of the faction in a role, no
/// piece of a fighter's role on a layer and body part of the kind's own kit (the Elves mod's own required apparel, 1.6),
/// the gifts' shares within their caps and the casters only the sorceresses; the lines said in plain words; the seams.
/// </summary>
public static class ForeignPeoplesTests
{
    /// <summary>
    /// What each dark elf kind of Elves (Continued) requires (its apparelRequired, from the mod's own 1.6
    /// Defs/PawnKinds_DarkEvles.xml, read 17 November 2026): their kit, which Rimfolk's pieces never take the place of.
    /// </summary>
    private static readonly Dictionary<string, string[]> ElvesKit = new Dictionary<string, string[]>
    {
        ["DevDesigner_DarkElves_Penitent"] = new[] { "DevDesigner_WoodElves_Boots", "DevDesigner_DarkElves_Cape", "DevDesigner_WoodElves_Tunic" },
        ["DevDesigner_DarkElves_Trader"] = new[] { "DevDesigner_WoodElves_Boots", "DevDesigner_DarkElves_Cape", "DevDesigner_WoodElves_Tunic" },
        ["DevDesigner_DarkElves_Dreadspear"] = new[] { "DevDesigner_WoodElves_Boots", "DevDesigner_WoodElves_Gloves", "DevDesigner_WoodElves_Tunic", "DevDesigner_Elves_Chainmail",
            "DevDesigner_DarkElves_DreadspearArmor", "DevDesigner_DarkElves_DreadspearHelmet", "DevDesigner_DarkElves_Shield" },
        ["DevDesigner_DarkElves_Darkshard"] = new[] { "DevDesigner_WoodElves_Boots", "DevDesigner_WoodElves_Gloves", "DevDesigner_WoodElves_Tunic", "DevDesigner_Elves_Chainmail",
            "DevDesigner_DarkElves_DreadspearArmor", "DevDesigner_DarkElves_DreadspearHelmet", "DevDesigner_DarkElves_Quiver" },
        ["DevDesigner_DarkElves_DarkRider"] = new[] { "DevDesigner_WoodElves_Boots", "DevDesigner_WoodElves_Gloves", "DevDesigner_WoodElves_Tunic", "DevDesigner_Elves_Gambeson",
            "DevDesigner_DarkElves_LeatherArmor" },
        ["DevDesigner_DarkElves_BlackGuard"] = new[] { "DevDesigner_WoodElves_Boots", "DevDesigner_WoodElves_Gloves", "DevDesigner_WoodElves_Tunic", "DevDesigner_Elves_Chainmail",
            "DevDesigner_DarkElves_BlackGuardCloak", "DevDesigner_DarkElves_BlackGuardArmor", "DevDesigner_DarkElves_BlackGuardHelmet", "DevDesigner_DarkElves_BlackGuardPauldrons" },
        ["DevDesigner_DarkElves_ColdOneKnight"] = new[] { "DevDesigner_WoodElves_Boots", "DevDesigner_WoodElves_Gloves", "DevDesigner_WoodElves_Tunic", "DevDesigner_Elves_Chainmail",
            "DevDesigner_DarkElves_BlackGuardPauldrons", "DevDesigner_DarkElves_ColdOneKnightArmor", "DevDesigner_DarkElves_ColdOneKnightHelmet", "DevDesigner_DarkElves_ColdOneKnightCloak",
            "DevDesigner_DarkElves_Shield" },
        ["DevDesigner_DarkElves_Witch"] = new[] { "DevDesigner_WoodElves_Boots", "DevDesigner_WoodElves_Gloves", "DevDesigner_DarkElves_WitchBikini", "DevDesigner_DarkElves_WitchArmor",
            "DevDesigner_DarkElves_WitchHelmet" },
        ["DevDesigner_DarkElves_Sorceress"] = new[] { "DevDesigner_WoodElves_Boots", "DevDesigner_WoodElves_Gloves", "DevDesigner_DarkElves_SorceressRobe", "DevDesigner_DarkElves_SorceressHelmet" },
        ["DevDesigner_DarkElves_Morathi"] = new[] { "DevDesigner_WoodElves_Boots", "DevDesigner_WoodElves_Gloves", "DevDesigner_DarkElves_MorathiPauldrons", "DevDesigner_DarkElves_MorathiRobe",
            "DevDesigner_DarkElves_MorathiArmor", "DevDesigner_DarkElves_MorathiHelmet" },
    };

    private const string Elves = "devdesigner.elves.continued";

    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the Stormriders: " + what);
        }

        // ================= The rules =================

        var kinds = new List<IList<string>> { new List<string> { "A" }, null, new List<string>(), new List<string> { "B", "C" } };
        var names = new List<string> { "spear", ForeignPeoplesMath.TraderRole, ForeignPeoplesMath.FolkRole, "guard" };
        C(ForeignPeoplesMath.RoleOf(kinds, names, "A", false) == 0 && ForeignPeoplesMath.RoleOf(kinds, names, "C", true) == 3
          && ForeignPeoplesMath.RoleOf(kinds, names, "Z", true) == 1 && ForeignPeoplesMath.RoleOf(kinds, names, "Z", false) == 2
          && ForeignPeoplesMath.RoleOf(kinds, names, null, false) == 2,
            "a kind plays the role that names it; a trader no role names the trader's, anyone else the folk's");
        C(ForeignPeoplesMath.RoleOf(new List<IList<string>> { new List<string> { "A" } }, new List<string> { "spear" }, "Z", false) == -1
          && ForeignPeoplesMath.RoleOf(null, names, "A", false) == -1, "no role fits: nothing is given");
        var w = new List<float> { 4f, 3f, 0f, 3f };
        C(ForeignPeoplesMath.PickLook(w, 0f) == 0 && ForeignPeoplesMath.PickLook(w, 0.39f) == 0 && ForeignPeoplesMath.PickLook(w, 0.41f) == 1
          && ForeignPeoplesMath.PickLook(w, 0.69f) == 1 && ForeignPeoplesMath.PickLook(w, 0.71f) == 3 && ForeignPeoplesMath.PickLook(w, 1f) == 3,
            "a look by its weight, a look of no weight never");
        C(ForeignPeoplesMath.PickLook(new List<float> { 0f, float.NaN }, 0.5f) == -1 && ForeignPeoplesMath.PickLook(null, 0.5f) == -1
          && ForeignPeoplesMath.PickLook(w, float.NaN) == -1, "no look with weight, or a bad roll: none");
        int[] counts = new int[4];
        var rng = new Random(7);
        for (int i = 0; i < 10000; i++) counts[ForeignPeoplesMath.PickLook(w, (float)rng.NextDouble())]++;
        C(Math.Abs(counts[0] - 4000) < 300 && Math.Abs(counts[1] - 3000) < 300 && counts[2] == 0 && Math.Abs(counts[3] - 3000) < 300,
            $"over many pawns each look comes by its share ({string.Join(", ", counts)} of 10000)");
        C(ForeignPeoplesMath.Rolls(0.3f, 0.29f) && !ForeignPeoplesMath.Rolls(0.3f, 0.3f) && !ForeignPeoplesMath.Rolls(0f, 0f) && !ForeignPeoplesMath.Rolls(float.NaN, 0f)
          && !ForeignPeoplesMath.Rolls(0.5f, float.NaN) && ForeignPeoplesMath.Rolls(1f, 0.999f), "a chance read safely");
        C(!ForeignPeoplesMath.MayDisplace(true, false, 0f, 1f) && ForeignPeoplesMath.MayDisplace(true, true, 0.1f, 0.2f) && ForeignPeoplesMath.MayDisplace(false, false, 0.1f, 0.1f)
          && !ForeignPeoplesMath.MayDisplace(false, false, 0.5f, 0.2f) && !ForeignPeoplesMath.MayDisplace(true, true, 0.5f, 0.2f)
          && ForeignPeoplesMath.MayDisplace(false, false, 0.21f, 0.2f) && !ForeignPeoplesMath.MayDisplace(false, false, float.NaN, 0.2f),
            "a piece never takes the place of the kind's own kit (unless the role names it) nor of one that protects more");
        C(ForeignPeoplesMath.StyleFits("Any", true) && ForeignPeoplesMath.StyleFits("Any", false) && ForeignPeoplesMath.StyleFits("Male", false) && !ForeignPeoplesMath.StyleFits("Male", true)
          && ForeignPeoplesMath.StyleFits("MaleUsually", false) && !ForeignPeoplesMath.StyleFits("MaleUsually", true) && ForeignPeoplesMath.StyleFits("Female", true)
          && !ForeignPeoplesMath.StyleFits("Female", false) && ForeignPeoplesMath.StyleFits("FemaleUsually", true) && !ForeignPeoplesMath.StyleFits("FemaleUsually", false)
          && !ForeignPeoplesMath.StyleFits(null, true), "a hair style by the game's own style gender");
        C(ForeignPeoplesMath.Sidearm(false, true, 0.5f, 0.1f) && !ForeignPeoplesMath.Sidearm(true, true, 1f, 0f) && !ForeignPeoplesMath.Sidearm(false, false, 1f, 0f)
          && !ForeignPeoplesMath.Sidearm(false, true, 0.5f, 0.6f), "a sidearm only for one who carries nothing and can fight");
        var with = new List<string> { "class" };
        var without = new List<string> { "psylink" };
        C(ForeignPeoplesMath.Gifts(true, with, without) == with && ForeignPeoplesMath.Gifts(false, with, without) == without && ForeignPeoplesMath.Gifts<string>(false, with, null) == null,
            "with A RimWorld of Magic its classes, without it the role's other gifts or none");
        C(ForeignPeoplesMath.GiftCap(true, false) == PeoplesGearMath.MaxShare && ForeignPeoplesMath.GiftCap(false, true) == PeoplesMagicMath.MagicShare
          && ForeignPeoplesMath.GiftCap(false, false) == PeoplesMagicMath.SpareShare && PeoplesGearMath.MaxShare < 1f,
            "the casters by name up to the peoples' whole share (never all), the rest as Rimfolk's peoples' ordinary kinds");
        C(ForeignPeoplesMath.GiftsFit(new List<float> { 0.05f, 0.1f }, false, true) && !ForeignPeoplesMath.GiftsFit(new List<float> { 0.1f, 0.1f }, false, true)
          && ForeignPeoplesMath.GiftsFit(new List<float> { 0.3f, 0.3f }, true, false) && !ForeignPeoplesMath.GiftsFit(new List<float> { 0.4f, 0.3f }, true, true)
          && !ForeignPeoplesMath.GiftsFit(new List<float> { 0f }, true, true) && ForeignPeoplesMath.GiftsFit(new List<float>(), false, false), "a role's shares within its cap");

        // ================= The player's own catalogue (docs/player-mods, active rows) =================

        string cat = Path.Combine(root, "docs", "player-mods");
        var pool = new Dictionary<string, List<(string type, string mod)>>();
        using (var gz = new GZipStream(File.OpenRead(Path.Combine(cat, "defs.tsv.gz")), CompressionMode.Decompress))
        using (var rd = new StreamReader(gz))
        {
            rd.ReadLine();
            string line;
            while ((line = rd.ReadLine()) != null)
            {
                var c = line.Split('\t');
                if (c.Length < 4 || c[3] != "yes") continue;
                if (!pool.TryGetValue(c[1], out var list)) pool[c[1]] = list = new List<(string, string)>();
                list.Add((c[0], c[2]));
            }
        }
        List<Dictionary<string, string>> Table(string file)
        {
            var lines = File.ReadAllLines(Path.Combine(cat, file));
            var head = lines[0].Split('\t');
            return lines.Skip(1).Where(l => l.Length > 0).Select(l =>
            {
                var c = l.Split('\t');
                var row = new Dictionary<string, string>();
                for (int i = 0; i < head.Length; i++) row[head[i]] = i < c.Length ? c[i] : "";
                return row;
            }).Where(r => r["active"] == "yes").ToList();
        }
        var apparel = Table("apparel.tsv").GroupBy(r => r["defName"]).ToDictionary(g => g.Key, g => g.Last());
        var weapons = Table("weapons.tsv").GroupBy(r => r["defName"]).ToDictionary(g => g.Key, g => g.Last());
        var traits = Table("traits.tsv").GroupBy(r => r["defName"]).ToDictionary(g => g.Key, g => g.Last());
        var darkKinds = Table("pawnkinds.tsv").Where(r => r["faction"] == "DevDesigner_DarkElvesFaction" && r["race"] == "Human").Select(r => r["defName"]).ToList();
        bool Is(string name, string type) => pool.TryGetValue(name, out var hits) && hits.Any(h => h.type == type);

        // ================= The dark elves' profile =================

        string file = Path.Combine(root, "Defs", "PeopleDefs", "Rimfolk_ForeignPeoples.xml");
        var defs = XDocument.Load(file).Root!.Elements("ColonyCouncil.ForeignPeopleDef").ToList();
        C(defs.Count == 1, "one profile, the dark elves'");
        var d = defs.FirstOrDefault();
        if (d == null) return n;
        C(d.Element("factions")?.Elements("li").Select(e => e.Value).SequenceEqual(new[] { "DevDesigner_DarkElvesFaction" }) == true && Is("DevDesigner_DarkElvesFaction", "FactionDef"),
            "for the Elves mod's dark elves, the Stormriders' faction in the player's save");
        bool magic = d.Element("builtOnMagic")?.Value == "true";

        // Every name of another mod is under its MayRequire's mod, and every name is in the player's active mods.
        var named = d.Descendants("li").Where(e => !e.HasElements && Regex.IsMatch(e.Value.Trim(), @"^[A-Za-z][A-Za-z0-9_]*$")).ToList();
        int checkedNames = 0;
        foreach (var e in named)
        {
            string name = e.Value.Trim();
            if (e.Parent?.Name.LocalName == "li") continue;
            checkedNames++;
            bool known = pool.TryGetValue(name, out var hits);
            string req = e.Attribute("MayRequire")?.Value?.ToLowerInvariant();
            C(known, $"{name} is in the player's active mods");
            if (!known) continue;
            if (req == null) C(hits.Any(h => h.mod == "ludeon.rimworld"), $"{name}: a name without MayRequire is the base game's");
            else
                C(req.Split(',').Select(x => x.Trim()).Any(pkg => hits.Any(h => h.mod == pkg)), $"{name}: MayRequire names its own mod ({req})");
        }
        C(checkedNames >= 60, $"every name looked up ({checkedNames})");

        var roles = d.Element("roles")!.Elements("li").ToList();
        string Name(XElement r) => r.Element("name")?.Value ?? "";
        List<string> Li(XElement? parent) => parent?.Elements("li").Select(e => e.Value.Trim()).ToList() ?? new List<string>();
        var roleNames = roles.Select(Name).ToList();
        C(roleNames.Contains(ForeignPeoplesMath.FolkRole) && roleNames.Contains(ForeignPeoplesMath.TraderRole) && roleNames.Distinct().Count() == roleNames.Count,
            "the folk's role for any kind no role names, the trader's, each role once");
        var roleKinds = roles.Select(r => (IList<string>)Li(r.Element("kinds"))).ToList();
        C(darkKinds.Count == ElvesKit.Count && darkKinds.All(ElvesKit.ContainsKey), $"the Elves mod's dark elf kinds as the catalogue has them ({darkKinds.Count})");
        foreach (string kind in darkKinds)
            C(roleKinds.Count(k => k.Contains(kind)) == 1, $"{kind} plays one role");
        C(ForeignPeoplesMath.RoleOf(roleKinds, roleNames, "Rimfolk_SomeCampKind", false) == roleNames.IndexOf(ForeignPeoplesMath.FolkRole)
          && ForeignPeoplesMath.RoleOf(roleKinds, roleNames, "SomeTrader", true) == roleNames.IndexOf(ForeignPeoplesMath.TraderRole),
            "a kind of theirs no role names (a camp's) dresses as their folk or their trader");

        var fighters = new HashSet<string> { "spear", "crossbow", "rider", "guard", "knight", "witch", "sorceress", "head" };
        foreach (var r in roles)
        {
            string name = Name(r);
            bool casters = r.Element("casters")?.Value == "true";
            var pieces = r.Element("pieces")?.Elements("li").ToList() ?? new List<XElement>();
            foreach (var p in pieces)
            {
                float ch = float.Parse(p.Element("chance")!.Value, CultureInfo.InvariantCulture);
                var list = Li(p.Element("apparel"));
                C(ch > 0f && ch <= 1f && list.Count > 0 && list.All(apparel.ContainsKey), $"{name}: a piece is a chance of apparel of the player's mods ({string.Join(", ", list)})");
            }
            foreach (string listName in new[] { "replaceable", "dyeMain", "dyeAccent" })
                C(Li(r.Element(listName)).All(apparel.ContainsKey), $"{name}: its {listName} names apparel");
            C(Li(r.Element("sidearms")).All(x => weapons.TryGetValue(x, out var wr) && wr["kind"] == "melee"), $"{name}: a sidearm is a melee weapon");
            // A fighter keeps their whole kit: no piece of the role on a layer and body part of a required piece.
            if (fighters.Contains(name))
            {
                C(Li(r.Element("replaceable")).Count == 0 && Li(r.Element("sidearms")).Count == 0, $"{name}: a fighter's kit is never replaced, their weapon stays");
                foreach (string kind in Li(r.Element("kinds")))
                {
                    if (!ElvesKit.TryGetValue(kind, out var kit)) continue;
                    var kitSlots = kit.Where(apparel.ContainsKey).SelectMany(k => Slots(apparel[k])).ToHashSet();
                    foreach (string piece in pieces.SelectMany(p => Li(p.Element("apparel"))).Where(apparel.ContainsKey))
                        C(!Slots(apparel[piece]).Any(kitSlots.Contains), $"{name}: {piece} sits beside {kind}'s own kit");
                }
            }
            // The gifts: within their cap, each a class Rimfolk gives and the player's A RimWorld of Magic has.
            foreach (string listName in new[] { "gifts", "giftsWithoutMagic" })
            {
                var gifts = r.Element(listName)?.Elements("li").ToList() ?? new List<XElement>();
                var shares = gifts.Select(g => float.Parse(g.Element("chance")!.Value, CultureInfo.InvariantCulture)).ToList();
                C(ForeignPeoplesMath.GiftsFit(shares, casters, magic), $"{name}: its {listName} ({shares.Sum():0.00}) within {ForeignPeoplesMath.GiftCap(casters, magic):0.00}");
                foreach (var g in gifts)
                {
                    string trait = g.Element("trait")?.Value;
                    bool link = g.Element("psylink") != null;
                    C(trait != null ^ link, $"{name}: a gift is a class or a psylink");
                    if (trait != null)
                        C(listName == "gifts" && PeoplesGearMath.MagicSetting(trait) != null && traits.TryGetValue(trait, out var tr) && tr["mod"] == "torann.arimworldofmagic",
                            $"{name}: {trait} is one of A RimWorld of Magic's classes Rimfolk gives, in the player's mods");
                    if (link) C(listName == "giftsWithoutMagic" && Li(g.Element("paths")).All(x => Is(x, "PsycasterPathDef")), $"{name}: a psylink only without A RimWorld of Magic, its paths real");
                }
            }
            C(!casters || name == "sorceress" || name == "head", $"{name}: only the sorceresses are casters by their own name");
        }
        var byRole = roles.ToDictionary(Name);
        C(Li(byRole["sorceress"].Element("kinds")).SequenceEqual(new[] { "DevDesigner_DarkElves_Sorceress" }) && Li(byRole["head"].Element("kinds")).SequenceEqual(new[] { "DevDesigner_DarkElves_Morathi" }),
            "the casters by name are the Sorceress and the Supreme Sorceress");
        C(roles.Where(r => Name(r) != "sorceress" && Name(r) != "head").All(r => r.Element("gifts") != null), "some of every other role can do magic too, sparingly");
        C(roles.Where(r => Name(r) != "sorceress" && Name(r) != "head").All(r => r.Element("giftsWithoutMagic") == null), "without A RimWorld of Magic only the sorceresses have a gift (a psylink)");
        C(Li(byRole["trader"].Element("replaceable")).SequenceEqual(new[] { "DevDesigner_DarkElves_Cape" }) && Li(byRole["folk"].Element("replaceable")).Contains("DevDesigner_WoodElves_Tunic"),
            "only a civilian's cape or tunic may give way to another piece");

        // The looks.
        var looks = d.Element("looks")!.Elements("li").ToList();
        C(looks.Count >= 3 && looks.All(l => float.Parse(l.Element("weight")?.Value ?? "1", CultureInfo.InvariantCulture) > 0f), $"a few looks ({looks.Count}), each with a weight");
        foreach (var l in looks)
        {
            string label = l.Element("label")?.Value ?? "?";
            var main = Rgb(l.Element("main")?.Value);
            var accent = Rgb(l.Element("accent")?.Value);
            C(main != null && accent != null && !main.SequenceEqual(accent), $"{label}: two colours of their own");
            C(main == null || main.Max() <= 140, $"{label}: a dark main colour, as dark elves wear");
            C(Li(l.Element("hair")).All(x => Is(x, "HairDef")), $"{label}: hair styles of the player's mods");
            C(Li(l.Element("faceTattoos")).All(x => Is(x, "TattooDef") && (x.StartsWith("Face_") || x.Contains("TattooFace"))), $"{label}: face tattoos for the face");
            C(Li(l.Element("bodyTattoos")).All(x => Is(x, "TattooDef") && (x.StartsWith("Body_") || x.Contains("TattooBody"))), $"{label}: body tattoos for the body");
        }
        C(looks.Any(l => l.Element("hair") == null && l.Element("faceTattoos") == null), "one look leaves hair and skin as they came");
        C(Rgb(looks[0].Element("main")?.Value)!.SequenceEqual(new[] { 36, 38, 55 }) && Rgb(looks[0].Element("accent")?.Value)!.SequenceEqual(new[] { 63, 53, 85 }),
            "the commonest look keeps the Elves mod's own midnight and purple");

        // ================= The words =================

        var idle = Lines(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Pool_VisitorIdle.xml")).Where(x => x.StartsWith("[") && x.Contains("folk:darkelves")).ToList();
        C(idle.Count >= 5, $"a dark elf visitor has a few lines of their own ({idle.Count})");
        var magicLines = Lines(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_PeoplesMagic_Lines.xml")).Where(x => x.StartsWith("[cl:Bladedancer]")).ToList();
        C(magicLines.Count >= 1, "a bladedancer names their craft in words of their own");
        foreach (string l in idle.Concat(magicLines))
        {
            string text = Regex.Replace(l, @"^\[[^\]]*\]\s*", "");
            C(Regex.Matches(text, @"[.?]").Count == 1 && (text.EndsWith(".") || text.EndsWith("?")) && !text.Contains('—') && !text.Contains('!'), $"one plain sentence: {text}");
        }

        // ================= The seams =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string fp = File.ReadAllText(Path.Combine(src, "Peoples", "Foreign", "ForeignPeoples.cs"));
        C(fp.Contains("[HarmonyPatch(typeof(PawnGenerator), \"GenerateNewPawnInternal\")]") && fp.Contains("!RimfolkPatches.On"),
            "only fresh pawns as the game makes them, off with the deeper hooks: those already in a game keep what they have");
        C(fp.Contains("PeopleGifts.GiveFrom(p, gifts, age)") && fp.Contains("PeoplesMagic.Loaded, role.gifts, role.giftsWithoutMagic"),
            "the gifts by Rimfolk's peoples' own machinery (only classes the mod has on and allows them)");
        C(fp.Contains("ForeignPeoplesMath.MayDisplace(Required(p.kindDef, w.def)") && fp.Contains("StatDefOf.ArmorRating_Sharp"), "every piece by the one rule of what it may take the place of");
        C(fp.Contains("PeoplesMagicMath.Wand(name)) name = PeoplesMagicMath.Garb(trait, 3)") && fp.Contains("|| PeoplesMagicMath.Wand(name)) return"),
            "a caster's own hood, never a wand: their weapon stays");
        C(fp.Contains("g.def.forcedHair != null || g.def.hairTagFilter != null") && fp.Contains("ForeignPeoplesMath.StyleFits(h.styleGender.ToString(), female)"),
            "a hair style never against their genes, by the game's style gender");
        C(fp.Contains("ModsConfig.IdeologyActive") && fp.Contains("t.tattooType == type"), "tattoos only with Ideology, each in its place");
        C(!fp.Contains("LetterStack") && !fp.Contains("Messages.Message") && !fp.Contains("Scribe_") && !fp.Contains("genes.SetXenotype") && !fp.Contains("ChangeKind"),
            "no letter, no message, nothing saved, their xenotype and kind untouched");
        string giftsSrc = File.ReadAllText(Path.Combine(src, "Peoples", "PeopleGifts.cs"));
        C(giftsSrc.Contains("internal static void GiveFrom(Pawn p, IList<PeopleGift> gifts, int age)") && giftsSrc.Contains("GiveFrom(p, ext.gifts, age)"), "Rimfolk's own peoples give their gifts the same way");
        string pm = File.ReadAllText(Path.Combine(src, "Peoples", "Magic", "PeoplesMagic.cs"));
        C(pm.Contains("public static bool Ours(Faction f) => Peoples.IsOurs(f) || ForeignPeoples.Has(f);") && pm.Contains("!Ours(p.Faction) || p.RaceProps?.Humanlike != true ? null : Craft(p)")
          && pm.Contains("IsKeptOut(ability.defName) && Ours(p.Faction)") && pm.Contains("ForeignPeoples.ClassShares(f)") && pm.Contains("OnMagic(f) ? \"CC_PeoplesMagic_Card_Many\""),
            "their casters name their craft, cast in peace by the mod's own rules, are kept from the broken spells, and their card says what crafts are among them");
        C(File.ReadAllText(Path.Combine(src, "Peoples", "Magic", "PeoplesMagicWatch.cs")).Contains("PeoplesMagicMath.PeaceChance(PeoplesMagic.OnMagic(x.Faction))"), "a coven's casters help in peace a little more often");
        C(File.ReadAllText(Path.Combine(src, "Peoples", "Peoples.cs")).Contains("ForeignPeoples.AddTags(f, tags);"), "their own tag reaches Rimfolk's lines");
        string dbg = File.ReadAllText(Path.Combine(src, "Peoples", "Foreign", "ForeignPeoplesDebug.cs"));
        C(dbg.Contains("Peoples of other mods: their gear, looks and gifts (log)...") && dbg.Contains("Peoples of other mods: one of each of their kinds here now, as visitors (testing)..."),
            "dev actions to see them made");
        return n;
    }

    /// <summary>A piece's (layer, body part group) pairs, as the game tells pieces that can't be worn together.</summary>
    private static IEnumerable<string> Slots(Dictionary<string, string> row)
    {
        var layers = row["layers"].Split(',', StringSplitOptions.RemoveEmptyEntries);
        var groups = row["bodyPartGroups"].Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (string l in layers)
            foreach (string g in groups)
                yield return l + "/" + g;
    }

    private static int[]? Rgb(string? text)
    {
        if (text == null) return null;
        var m = Regex.Match(text, @"^\((\d+),(\d+),(\d+)\)$");
        return m.Success ? new[] { int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value) } : null;
    }

    private static IEnumerable<string> Lines(string file) => XDocument.Load(file).Descendants("li").Select(e => e.Value.Trim());

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
