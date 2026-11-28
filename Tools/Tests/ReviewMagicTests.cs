using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The review of Rimfolk's integrations with A RimWorld of Magic, against the player's A RimWorld of Magic (Continued)
/// (docs/review-magic.md): every name of the mod Rimfolk reads is in the player's active mods; the continuation's class
/// progression read as the classes (a discipline as its class trait, the vocations in the mage and fighter tags); a spell
/// learned read from its knowledge store first; the casts Rimfolk asks of a visitor in peace let through the continuation's
/// own check on 1.7; the auto-cast's spells paid from the pool the mod's own ability class says, Spite only with the hate a
/// death knight of the progression spends, nobody cut in on while another system casts for them; the craft's count taking
/// each power once; and everything of the mod read softly, by name.
/// </summary>
public static class ReviewMagicTests
{
    private const string Rom = "torann.arimworldofmagic";

    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "review of the magic integrations: " + what);
        }

        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        if (root == null)
        {
            C(false, "found the mod's folder");
            return n;
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));

        var cat = Catalogue(root);
        var magicMods = new HashSet<string> { Rom, "huginn.rwom.integrations", "zal.jecsliterwom", "huginn.rwom.core" };
        var activeAny = new HashSet<string>(cat.Where(r => r.active).Select(r => r.def));
        var romActive = new HashSet<(string, string)>(cat.Where(r => r.active && r.mod == Rom).Select(r => (r.type, r.def)));
        bool Has(string type, string def) => romActive.Contains((type, def));

        // ================= Every name of the mod Rimfolk reads, in the player's active mods =================

        // The code: every string of the mod's that names a def (TM_, SpellOf_, BookOf, Mote_ and the rest the mod has), in
        // Rimfolk's C# outside comments; prefixes that end in "_" are a family, read with StartsWith.
        // A def only the magic mods have (any of them, in the catalogue): a name Rimfolk can only mean as theirs.
        var otherMods = new HashSet<string>(cat.Where(r => !magicMods.Contains(r.mod)).Select(r => r.def));
        var magicOnly = new HashSet<string>(cat.Where(r => magicMods.Contains(r.mod) && !otherMods.Contains(r.def)).Select(r => r.def));
        var lit = new Regex(@"""([A-Za-z][A-Za-z0-9_]{2,})""");
        var codeNames = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
            foreach (var raw in File.ReadLines(file))
            {
                if (raw.TrimStart().StartsWith("//")) continue;
                foreach (Match m in lit.Matches(raw))
                {
                    string name = m.Groups[1].Value;
                    // A family read with StartsWith ("BookOf", "TM_EStone_"), and Kure's TM_Shadowstep, named only to keep
                    // its spell id apart from the mod's TM_ShadowStep (MagicModelMath.ConventionSpellId).
                    if (name.EndsWith("_") || name == "BookOf" || name == "TM_Shadowstep") continue;
                    if (name.StartsWith("TM_") || name.StartsWith("SpellOf_") || name.StartsWith("BookOf") || magicOnly.Contains(name)) codeNames.Add(name);
                }
            }
        C(codeNames.Count >= 150, $"the mod's names read in the code ({codeNames.Count})");
        foreach (string name in codeNames)
            C(activeAny.Contains(name), $"{name} (in the code) is in the player's active mods");

        // The defs: every element guarded by the mod's package, and every TM_ name in an element's text.
        var defNames = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories)
                     .Concat(Directory.GetFiles(Path.Combine(root, "Patches"), "*.xml", SearchOption.AllDirectories)))
        {
            if (file.Replace('\\', '/').Contains("/Defs/PortraitLookDefs/")) continue;
            XDocument doc;
            try
            {
                doc = XDocument.Load(file);
            }
            catch
            {
                continue;
            }
            foreach (var el in doc.Descendants())
            {
                string req = (string)el.Attribute("MayRequire") ?? (string)el.Attribute("MayRequireAnyOf") ?? "";
                bool guarded = req.ToLowerInvariant().Contains(Rom);
                string text = el.HasElements ? null : el.Value.Trim();
                if (guarded && el.Name.LocalName != "li" && !Regex.IsMatch(el.Name.LocalName, "^[a-z]")) defNames.Add(el.Name.LocalName);
                else if (guarded && !string.IsNullOrEmpty(text) && Regex.IsMatch(text, @"^[A-Za-z][A-Za-z0-9_]+$")) defNames.Add(text);
                if (!string.IsNullOrEmpty(text) && Regex.IsMatch(text, @"^TM_[A-Za-z0-9_]+$")) defNames.Add(text);
            }
        }
        C(defNames.Count >= 60, $"the mod's names in the defs ({defNames.Count})");
        foreach (string name in defNames)
            C(activeAny.Contains(name), $"{name} (in the defs) is in the player's active mods");

        // The classes the tag groups list: each one a trait of the mod's, active.
        var groups = XDocument.Load(Path.Combine(root, "Defs", "IntegrationDefs", "Council_People_TagGroups.xml")).Root!
            .Elements("ColonyCouncil.TagGroupDef").ToDictionary(g => (string)g.Element("tag"), g => g.Element("traits")?.Elements("li").Select(li => li.Value.Trim()).ToList() ?? new List<string>());
        foreach (string tag in new[] { "mage", "fighter", "healer", "gifted" })
            foreach (string trait in groups[tag])
                C(Has("TraitDef", trait), $"the {tag} tag's {trait} is a trait of A RimWorld of Magic, active in the player's mods");

        // ================= The continuation's class progression =================

        foreach (string v in MagicModelMath.Vocations)
            C(Has("TraitDef", v), $"the vocation {v} is the continuation's, active in the player's mods");
        C(groups["mage"].Contains(MagicModelMath.VocationMage) && groups["mage"].Contains(MagicModelMath.VocationWarriorMage) && !groups["mage"].Contains(MagicModelMath.VocationWarrior),
            "a mage or warrior-mage by vocation is a mage (the player's Myst), a warrior is not");
        C(groups["fighter"].Contains(MagicModelMath.VocationWarrior) && groups["fighter"].Contains(MagicModelMath.VocationWarriorMage) && !groups["fighter"].Contains(MagicModelMath.VocationMage),
            "a warrior or warrior-mage by vocation is a fighter (the player's Cait and Myst), a mage is not");
        C(groups["mage"].Contains("TM_WeatherMage"), "the continuation's new weather mage is a mage");
        int disciplines = cat.Count(r => r.active && r.mod == Rom && r.type == "ClassDisciplineDef");
        C(disciplines == MagicModelMath.Disciplines.Count + 1, $"every discipline of the continuation is read ({MagicModelMath.Disciplines.Count} and the wanderer, of {disciplines})");
        foreach (var kv in MagicModelMath.Disciplines)
            C(Has("TraitDef", kv.Value), $"{kv.Key} stands for {kv.Value}, a trait of the mod's, active");
        C(MagicModelMath.Disciplines.Values.Where(t => t != "TM_Apothecary").All(t => groups["mage"].Contains(t) || groups["fighter"].Contains(t)) && groups["healer"].Contains("TM_Apothecary"),
            "each discipline's class is in the mage or fighter tag, the apothecary's in the healer's");
        C(MagicModelMath.ClassesOf("rwom.fire", 1).SequenceEqual(new[] { "InnerFire" }) && MagicModelMath.ClassesOf("rwom.supersoldier", 2).SequenceEqual(new[] { "TM_SuperSoldier" })
          && MagicModelMath.ClassesOf("rwom.shadow", 1).SequenceEqual(new[] { "TM_TheShadow" }) && MagicModelMath.ClassesOf("rwom.shadowwalker", 2).SequenceEqual(new[] { "TM_TheShadow" }),
            "the player's save: Cait's super soldier and Myst's two shadow halves read as their classes");
        C(MagicModelMath.ClassesOf(MagicModelMath.Wanderer, 1).SequenceEqual(new[] { "TM_Wanderer" }) && MagicModelMath.ClassesOf(MagicModelMath.Wanderer, 2).SequenceEqual(new[] { "TM_Wayfarer" })
          && MagicModelMath.ClassesOf(MagicModelMath.Wanderer, 3).SequenceEqual(new[] { "TM_Wanderer", "TM_Wayfarer" }), "the wanderer by its domain: wanderer, wayfarer, or both");
        C(MagicModelMath.ClassesOf("legacy.InnerFire.0", 1).SequenceEqual(new[] { "InnerFire" }) && MagicModelMath.ClassesOf("legacy.TM_Bard.4", 1).SequenceEqual(new[] { "TM_Bard" }),
            "RWoM Core's classic class read as its trait");
        C(!MagicModelMath.ClassesOf("rwom.nobody", 1).Any() && !MagicModelMath.ClassesOf(null, 1).Any() && !MagicModelMath.ClassesOf("", 0).Any(), "an id Rimfolk doesn't know is no class");

        // ================= A spell learned =================

        C(MagicModelMath.ConventionSpellId("TM_ArcaneBolt") == "rwom.tm_arcanebolt" && MagicModelMath.ConventionSpellId("TM_VeilOfShadows") == "rwom.tm_veilofshadows"
          && MagicModelMath.ConventionSpellId("TM_CQC") == "rwom.tm_cqc", "a spell's id as the player's save keeps it (rwom.tm_arcanebolt, rwom.tm_veilofshadows, rwom.tm_cqc)");
        C(MagicModelMath.ConventionSpellId("TM_Shadowstep") == "kure.tm_shadowstep" && MagicModelMath.ConventionSpellId("TM_ShadowStep") == "rwom.tm_shadowstep"
          && MagicModelMath.ConventionSpellId(null) == null, "the two shadow steps kept apart, as the continuation keeps them");
        C(MagicModelMath.Learned(null, true) && !MagicModelMath.Learned(null, false) && !MagicModelMath.Learned(false, true) && MagicModelMath.Learned(true, false),
            "the knowledge store first, the old power list only without an entry");
        C(MagicModelMath.EntryFound("Available") && MagicModelMath.EntryFound("MissingDefinition") && !MagicModelMath.EntryFound("NoRecord") && !MagicModelMath.EntryFound("PendingImport")
          && !MagicModelMath.EntryFound("ProviderUnavailable") && !MagicModelMath.EntryFound(null), "RWoM Core's answer: an entry found, or not");

        // ================= Casting on their own, against the player's defs =================

        var abilities = File.ReadAllLines(Path.Combine(root, "docs", "player-mods", "abilities.tsv")).Skip(1).Select(l => l.Split('\t'))
            .Where(c => c.Length >= 6 && c[1] == Rom && c[2] == "yes").ToDictionary(c => c[0], c => c[5]);
        foreach (var s in MagicAutoCastMath.Spells)
        {
            var levels = new[] { s.name, s.name + "_I", s.name + "_II", s.name + "_III" }.Where(abilities.ContainsKey).ToList();
            C(levels.Count > 0, $"{s.name}: an ability of the mod's, active");
            string want = s.pays == MagicAutoCastMath.Pays.Mana ? "TorannMagic.MagicAbility" : "TorannMagic.MightAbility";
            foreach (string level in levels)
            {
                C(abilities[level] == want, $"{level} is a {(s.pays == MagicAutoCastMath.Pays.Mana ? "spell, paid in mana" : "technique, paid in stamina")} ({abilities[level]})");
                C(MagicAutoCastMath.Of(level) == s, $"{level} is cast as {s.name}");
            }
        }
        C(MagicAutoCastMath.Spite == "TM_Spite" && MagicAutoCastMath.Spells.Any(s => s.name == MagicAutoCastMath.Spite), "Spite is one of the spells");
        C(MagicAutoCastMath.SpiteReady(false, 0f) && MagicAutoCastMath.SpiteReady(true, 20f) && MagicAutoCastMath.SpiteReady(true, 35f) && !MagicAutoCastMath.SpiteReady(true, 19.9f)
          && !MagicAutoCastMath.SpiteReady(true, float.NaN), "Spite: a death knight by the classic trait at once, one of the progression only with 20 hate");
        C(Has("HediffDef", MagicAutoCastMath.HateHediff) && Has("HediffDef", MagicAutoCastMath.HateHediff + "_V"), "the hate hediff and its levels are the mod's");

        // ================= Read softly, by name =================

        string model = Code(Path.Combine("Integrations", "MagicModel.cs"));
        foreach (string name in new[]
                 {
                     "\"TorannMagic.Core.PawnDisciplines\"", "\"TryRead\"", "\"TorannMagic.Core.PawnSpellKnowledge\"", "\"Read\"", "\"TorannMagic.Core.CastOwnership\"",
                     "\"IsCasting\"", "\"TorannMagic.SpellIds\"", "\"TorannMagic.ClassProgression.ClassIdentity\"", "\"TryGetState\"", "\"TorannMagic.AbilityKnowledgeComp\"",
                     "\"ReadyForQuery\"", "\"Spells\"", "\"Disciplines\"", "\"ClassId\"", "\"Domain\"", "\"Learned\"", "\"learned\"", "\"spellId\""
                 })
            C(model.Contains(name), $"MagicModel reads {name} by name");
        C(!Regex.IsMatch(model, @"using\s+(TorannMagic|AbilityUser)") && Regex.Matches(model, @"CouncilLog\.ErrorOnce").Count >= 5 && model.Contains("CouncilLog.WarnOnce"),
            "nothing of the mod's by type; each read logs once when it fails");
        C(model.Contains("if (!PeoplesMagic.Loaded) return;") && model.Contains("if (coreDisciplines == null && tryGetState == null) return none;"), "without the mod nothing is read");
        string rom = Code(Path.Combine("Peoples", "Magic", "PeoplesMagic.cs"));
        C(rom.Contains("&& Learned(p, d)") && rom.Contains("&& Learned(p, def)") && rom.Contains("MagicModelMath.Learned(MagicModel.Learned(p, def), true)"),
            "the peoples' casters and the colony's own find only spells the knowledge store doesn't mark unlearned");
        C(rom.Contains("Enum.IsDefined(contextType, \"AI\")"), "the framework's AI context read by name");
        C(rom.Contains("MagicModel.DisciplineClasses(p)"), "a craft read from a discipline too");
        string powers = Code(Path.Combine("Integrations", "MagicPowers.cs"));
        C(powers.Contains("seen.Add(power)") && powers.Contains("MagicModel.KnowledgeOf(p)"), "the craft's count: each power once (the continuation lists scroll spells twice), the knowledge store first");
        string magic = Code(Path.Combine("Integrations", "Magic.cs"));
        C(magic.Contains("\"IsMagicUser\"") && magic.Contains("\"IsMightUser\""), "a class level read only for a caster or a fighter the mod counts as one");
        string tags = Code(Path.Combine("Integrations", "PeopleTags.cs"));
        C(Regex.Matches(tags, @"MagicModel\.DisciplineClasses\(p\)").Count == 2, "a discipline is a class trait to the tags (trait:Druid) and to the tag groups");

        string patches = Code(Path.Combine("Peoples", "Magic", "PeoplesMagicPatches.cs"));
        C(patches.Contains("\"TorannMagic.Compatibility.AbilityCastSafety\"") && patches.Contains("\"IsAutomaticCast\"") && patches.Contains("PeoplesMagicWatch.Asked(__0?.CasterPawn)")
          && patches.Contains("public static string StandsDownWhy()"), "a visitor's spell in peace read as asked for by the continuation's 1.7 check, and stands down without it");
        string watch = Code(Path.Combine("Peoples", "Magic", "PeoplesMagicWatch.cs"));
        C(watch.Contains("!Patch_PeoplesMagicPeaceCast.Open") && watch.Contains("pending[i].job == job"), "no visitor is asked to cast where the continuation would hold it back; only the very job asked for passes");
        string cast = Code(Path.Combine("Integrations", "MagicAutoCast.cs"));
        C(cast.Contains("MagicModel.CastOwned(p)") && cast.Contains("MagicAutoCastMath.SpiteReady(MagicModel.InDiscipline(p, MagicAutoCastMath.KnightDiscipline), Hate(p))"),
            "casting on their own: never cutting in on another system's cast, Spite only with the hate it spends");
        C(Code(Path.Combine("Gods", "OnMap", "GodGifts.cs")).Contains("MagicModel.HasVocation(p) || MagicModel.DisciplineClasses(p).Count > 0"), "a god gives no class trait to one with a vocation or a discipline");
        C(Code(Path.Combine("Gods", "Worship", "WorshipMore.cs")).Contains("MagicModel.DisciplineClasses(p)") && WorshipMath.ClassDomain("TM_WeatherMage") == GodDomain.Storm,
            "a caster's god by their discipline, the weather mage's the storm's");
        C(Code(Path.Combine("Gods", "Presence", "GodReactions.cs")).Contains("MagicModel.DisciplineClasses(p)") && Code(Path.Combine("Animals", "AnimalVoices.cs")).Contains("MagicModel.DisciplineClasses(p)")
          && Code(Path.Combine("Gods", "Belief", "PawnGods.cs")).Contains("MagicModel.DisciplineClasses(p)"), "the devout, the animals' druids and rangers, and the gods' draws know the disciplines");
        C(Code(Path.Combine("OldPowers", "OldPowersNight.cs")).Contains("MagicModel.HasMana(p)"), "a mage under a sky full of power by the mod's mana, whatever the class");

        // ================= The notes =================

        string almanac = File.ReadAllText(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Mods_C_PeopleMagic.xml"));
        C(almanac.Contains("class progression") && almanac.Contains("vocation"), "the Almanac's page names the continuation's class progression");
        C(File.Exists(Path.Combine(root, "docs", "review-magic.md")), "the notes: docs/review-magic.md");

        return n;
    }

    private static List<(string type, string def, string mod, bool active)> catalogue;

    private static List<(string type, string def, string mod, bool active)> Catalogue(string root)
    {
        if (catalogue != null) return catalogue;
        catalogue = new List<(string, string, string, bool)>();
        using var fs = File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz"));
        using var gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress);
        using var rd = new StreamReader(gz);
        rd.ReadLine();
        string line;
        while ((line = rd.ReadLine()) != null)
        {
            var c = line.Split('\t');
            if (c.Length >= 4) catalogue.Add((c[0], c[1], c[2], c[3] == "yes"));
        }
        return catalogue;
    }
}
