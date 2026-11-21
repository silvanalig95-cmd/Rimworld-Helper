using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Portraits, phase 3: the library of painted portraits and the matcher (the player, 21 November 2026: "a large image
/// library for as many ages and sexes and races and hairstyles and whatever combined, kind of have everything accurately
/// described ... So you can always select the most appropriate one to the pawn", "maybe even same characters. Also, when
/// they're injured, they might have an eye less", "as many races as possible", "they don't have to always be in the same
/// angle"; docs/portrait-art/PLAN.md). A def per picture read into a record (its words, colours and errors); a pawn's
/// looks read from the game's words; the matcher (the kind first, sex and age, the distance, the threshold, faces kept and
/// their variants following the body, two alike people apart, picking again); the views of a whole picture for each
/// place, turned to face its text or the other of a pair; the seams (a source the face engine asks first, nothing
/// shipped). Writes the matcher's table for sample people against the first wave's prompt list to
/// docs/portrait-previews/matcher.md (there is no art yet to show).
/// </summary>
public static class PortraitLibraryTests
{
    private static PortraitColour Hex(string h) => PortraitLibraryMath.TryColour(h, out var c) ? c : throw new Exception("bad colour " + h);

    /// <summary>A person's looks as the reader would give them.</summary>
    private static PortraitLook Look(PortraitSex sex, PortraitAge age, string skin, string hairColour, PortraitHairStyle hair = PortraitHairStyle.Short,
        PortraitBeard beard = PortraitBeard.None, PortraitBuild build = PortraitBuild.Average, PortraitEra era = PortraitEra.Unknown, PortraitDress dress = PortraitDress.Unknown,
        string xenotype = "baseliner", string race = "human", string[]? features = null, string[]? marks = null, bool headwear = false)
    {
        var l = new PortraitLook
        {
            sex = sex, age = age, skin = Hex(skin), hairColour = Hex(hairColour), hair = hair, beard = beard, build = build, era = era, dress = dress, headwear = headwear,
            xenotype = xenotype, race = race, baseliner = xenotype == "baseliner" && race == "human"
        };
        l.unusualSkin = PortraitLibraryMath.UnusualSkin(l.skin);
        foreach (var f in features ?? Array.Empty<string>()) l.features.Add(f);
        foreach (var m in marks ?? Array.Empty<string>()) l.marks.Add(PortraitLibraryMath.MarkKey(m));
        return l;
    }

    /// <summary>A def's record from an XML element, as the game's def holds the same fields.</summary>
    private static PortraitRecord FromXml(XElement e, List<string> problems)
    {
        string? F(string name) => e.Element(name)?.Value;
        IList<string>? L(string name) => e.Element(name)?.Elements("li").Select(x => x.Value).ToList();
        return PortraitLibraryMath.Record(F("defName"), F, L, problems);
    }

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "portrait library: " + what);
        }
        bool Near(float a, float b, float eps = 0.002f) => Math.Abs(a - b) <= eps;
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.Exists(Path.Combine(src, rel)) ? File.ReadAllText(Path.Combine(src, rel)) : "";

        // ---------------- A picture's def ----------------
        string fixtures = Path.Combine(root, "Tools", "Tests", "PortraitFixtures");
        string defSrc = Read("UI/Faces/PortraitDef.cs");
        var sample = XDocument.Load(Path.Combine(fixtures, "PortraitDefs_Sample.xml")).Root!.Elements("ColonyCouncil.PortraitDef").ToList();
        var samples = new List<PortraitRecord>();
        foreach (var e in sample)
        {
            var problems = new List<string>();
            var r = FromXml(e, problems);
            samples.Add(r);
            C(problems.Count == 0, $"{r.name}: read without a problem ({string.Join("; ", problems)})");
            foreach (var field in e.Elements().Select(x => x.Name.LocalName).Where(x => x != "defName"))
                C(Regex.IsMatch(defSrc, @"public (string|List<string>|float) [^;]*\b" + field + @"\b"), $"{r.name}: '{field}' is a field of the def");
            string tex = e.Element("texPath")!.Value;
            C(File.Exists(Path.Combine(fixtures, Path.GetFileName(tex) + ".png")), $"{r.name}: its placeholder picture is there ({tex})");
        }
        C(samples.Count == 5 && PortraitLibraryMath.LibraryProblems(samples).Count == 0, "the sample library: five pictures, nothing wrong across them");
        var aldo = samples.Where(r => r.Character == "sample_aldo").ToList();
        C(aldo.Count == 3 && aldo.Count(r => r.variant == "base") == 1 && aldo.Any(r => r.marks.Contains("lostEye")) && aldo.Any(r => r.age == PortraitAge.Old),
            "a face with three variants: its base, a lost eye (written as an eye patch), older");
        var ines = samples.First(r => r.name == "Portrait_Sample_Ines");
        C(ines.Character == ines.name && ines.hair == PortraitHairStyle.Braids && ines.sex == PortraitSex.Female && ines.age == PortraitAge.Young && Near(ines.skin.r, 0.55f),
            "a face of its own (no character given): its own name; the words and the colour read");
        var elf = samples.First(r => r.name == "Portrait_Sample_DarkElf");
        C(elf.xenotypes.SequenceEqual(new[] { "devdesigner_xenotypedarkelves" }) && elf.features.Contains("pointedEars") && elf.facing == PortraitFacing.Right && elf.dress == PortraitDress.Armour,
            "a xenotype's picture: its xenotype, its features, its facing and armour");
        // What is wrong with a def is told (the game shows it among the def errors at start-up).
        {
            var problems = new List<string>();
            var fields = new Dictionary<string, string> { ["sex"] = "boy", ["age"] = "ancient", ["hair"] = "curly", ["era"] = "stone", ["facing"] = "up", ["skinColor"] = "pink" };
            var lists = new Dictionary<string, IList<string>> { ["features"] = new[] { "wings", "horns" }, ["marks"] = new[] { "missingArm" }, ["xenotypes"] = new[] { "Baseliner", "Impid" } };
            var r = PortraitLibraryMath.Record("Bad", k => fields.TryGetValue(k, out var v) ? v : null, k => lists.TryGetValue(k, out var v) ? v : null, problems);
            foreach (var want in new[] { "texPath", "sex", "age", "hair", "era", "facing", "skinColor", "hairColor", "wings", "missingArm", "baseliner" })
                C(problems.Any(p => p.Contains(want)), $"a def's problem told: {want}");
            C(r.features.SetEquals(new[] { "horns" }) && r.age == PortraitAge.Adult && r.hair == PortraitHairStyle.Short, "a def's unknown words are left out, the rest kept");
        }
        var twoBases = new List<PortraitRecord>
        {
            new PortraitRecord { name = "a", character = "x", marks = { "scar" }, variant = "scarred" }, new PortraitRecord { name = "b", character = "x", sex = PortraitSex.Female, age = PortraitAge.Old, variant = "older" }
        };
        C(PortraitLibraryMath.LibraryProblems(twoBases).Count == 2, "across the library: a face with no plain base, and variants of two sexes, are told");
        foreach (var (text, r, g, b) in new[] { ("#c49a6c", 196f, 154f, 108f), ("c49a6c", 196f, 154f, 108f), ("(196, 154, 108)", 196f, 154f, 108f), ("(0.5, 0.25, 1)", 127.5f, 63.75f, 255f), ("(1, 1, 1, 1)", 255f, 255f, 255f) })
        {
            bool ok = PortraitLibraryMath.TryColour(text, out var c);
            C(ok && Near(c.r * 255f, r, 0.6f) && Near(c.g * 255f, g, 0.6f) && Near(c.b * 255f, b, 0.6f), $"the colour '{text}' read ({c})");
        }
        foreach (var bad in new[] { "", "pink", "#12345", "(1, 2)", "(a, b, c)", "(-1, 0, 0)" })
            C(!PortraitLibraryMath.TryColour(bad, out _), $"'{bad}' is no colour");
        C(PortraitLibraryMath.Hex(Hex("#c49a6c")) == "c49a6c", "a colour written back as #rrggbb");
        // Nothing of the library ships yet: every portrait is the game's own face until the pictures and their defs are added.
        C(!Directory.Exists(Path.Combine(root, "Defs", "PortraitDefs")) || !Directory.GetFiles(Path.Combine(root, "Defs", "PortraitDefs"), "*.xml").Any(), "no portrait defs ship yet");
        C(!Directory.Exists(Path.Combine(root, "Textures", "Rimfolk", "Portraits")) || !Directory.GetFiles(Path.Combine(root, "Textures", "Rimfolk", "Portraits"), "*.png", SearchOption.AllDirectories).Any(),
            "no portrait pictures ship yet");
        C(Directory.GetFiles(fixtures, "*.png").Length == 2, "two placeholder pictures, for the tests only");

        // ---------------- A pawn's looks read from the game's words ----------------
        bool Age(float y, float le, out PortraitAge a) => PortraitLibraryMath.AgeOf(y, le, out a);
        C(!Age(1f, 80f, out _) && !Age(3.5f, 80f, out _) && !Age(float.NaN, 80f, out _), "a baby or a toddler: no picture");
        C(Age(4f, 80f, out var a1) && a1 == PortraitAge.Child && Age(12.9f, 80f, out var a2) && a2 == PortraitAge.Child, "four to twelve: a child");
        C(Age(13f, 80f, out var a3) && a3 == PortraitAge.Young && Age(23.9f, 80f, out var a4) && a4 == PortraitAge.Young, "13 to 23: young");
        C(Age(24f, 80f, out var a5) && a5 == PortraitAge.Adult && Age(59f, 80f, out var a6) && a6 == PortraitAge.Adult, "24 to 59: grown");
        C(Age(60f, 80f, out var a7) && a7 == PortraitAge.Old && Age(110f, 80f, out var a8) && a8 == PortraitAge.Old, "60 and over: old");
        C(Age(150f, 400f, out var a9) && a9 == PortraitAge.Adult && Age(320f, 400f, out var a10) && a10 == PortraitAge.Old && Age(30f, 0f, out var a11) && a11 == PortraitAge.Adult,
            "a long-lived kind ages by its own life expectancy; none known, a human's");
        // The game's skin colours (its melanin genes', approximately): all human. Others no human grows.
        var melanin = new[] { "f2ede0", "ffefd5", "ffefbd", "e4c699", "e49e5a", "b3804d", "825b30", "634624", "402917" };
        foreach (var m in melanin) C(!PortraitLibraryMath.UnusualSkin(Hex(m)), $"skin #{m}: a human skin");
        foreach (var (what, hex) in new[]
                 {
                     ("green", "73a659"), ("blue", "668cd9"), ("purple", "8c59a6"), ("slate grey", "737880"), ("light grey", "b3b3b3"), ("ink black", "0d0d0f"), ("a cold sheer white", "ebedfa"),
                     ("deep red", "a61f1a"), ("orange", "f28026"), ("yellow", "e6d140"), ("pale yellow", "f2f299")
                 })
            C(PortraitLibraryMath.UnusualSkin(Hex(hex)), $"{what} skin: no human's");
        C(string.Join(",", PortraitLibraryMath.Tokens("HalfShaved", "VHE_LongAfro", "Things/Pawn/Humanlike/Hairs/BriefPonytail")) == "half,shaved,vhe,long,afro,things,pawn,humanlike,hairs,brief,ponytail",
            "a def's words split where the case changes or a sign stands");
        PortraitHairStyle? H(string name) => PortraitLibraryMath.HairFromWords(PortraitLibraryMath.Tokens(name));
        foreach (var (name, want) in new (string, PortraitHairStyle?)[]
                 {
                     ("Long", PortraitHairStyle.Long), ("VHE_LongAfro", PortraitHairStyle.Long), ("Locks", PortraitHairStyle.Long), ("VHE_Dreadlock", PortraitHairStyle.Long), ("Flowy", PortraitHairStyle.Long),
                     ("BriefPonytail", PortraitHairStyle.Tied), ("VHE_LongPonytail", PortraitHairStyle.Tied), ("Pigtails", PortraitHairStyle.Tied), ("BunchesRS", PortraitHairStyle.Tied),
                     ("BraidedKnot", PortraitHairStyle.Braids), ("VHE_BattleBraids", PortraitHairStyle.Braids), ("ShaveTopBraid", PortraitHairStyle.Braids), ("FancyBun", PortraitHairStyle.Bun),
                     ("Mohawk", PortraitHairStyle.Short), ("Fauxhawk", PortraitHairStyle.Short), ("ShavedMohawk", PortraitHairStyle.Short), ("HalfShaved", PortraitHairStyle.Short),
                     ("SideCut", PortraitHairStyle.Short), ("ShortCut", PortraitHairStyle.Short), ("Bowlcut", PortraitHairStyle.Short), ("VHE_Buzzcut", PortraitHairStyle.Short),
                     ("Spikes", PortraitHairStyle.Short), ("Tuft", PortraitHairStyle.Short), ("Afro", PortraitHairStyle.Short), ("Recruit", PortraitHairStyle.Short), ("Bob", PortraitHairStyle.Medium),
                     ("Bald", PortraitHairStyle.Bald), ("Shaved", PortraitHairStyle.Bald), ("PatchyShave", PortraitHairStyle.Bald), ("RoughShave", PortraitHairStyle.Bald),
                     ("Cute", null), ("Curly", null), ("Princess", null), ("VHE_Ragnar", null)
                 })
            C(H(name) == want, $"the hair '{name}': {want?.ToString() ?? "not told by its words (its texture is measured)"} (read {H(name)?.ToString() ?? "none"})");
        // The player's own hairs, beards and genes (docs/player-mods, active rows).
        var hairs = new List<string>();
        var beards = new List<string>();
        var genes = new List<string>();
        string defs = Path.Combine(root, "docs", "player-mods", "defs.tsv.gz");
        if (File.Exists(defs))
        {
            using var gz = new GZipStream(File.OpenRead(defs), CompressionMode.Decompress);
            using var rd = new StreamReader(gz);
            string? line;
            while ((line = rd.ReadLine()) != null)
            {
                var c = line.Split('\t');
                if (c.Length < 4 || c[3] != "yes") continue;
                if (c[0] == "HairDef") hairs.Add(c[1]);
                else if (c[0] == "BeardDef") beards.Add(c[1]);
                else if (c[0] == "GeneDef") genes.Add(c[1]);
            }
        }
        int told = hairs.Count(h => H(h) != null);
        C(hairs.Count > 250 && told > 30, $"the player's {hairs.Count} hairs: {told} told by their names alone, the rest by their labels, texture paths and tags in the game, else their texture's shape");
        Console.WriteLine($"  (portrait library: of the player's {hairs.Count} hairs the names alone tell {told}; of their {genes.Count} genes, {genes.Count(g => PortraitLibraryMath.FeaturesOf(PortraitLibraryMath.Tokens(g)).Count > 0)} show a feature by name)");
        float[] Rows(int bottomRow, float width = 32f, int count = 32, float cover = 0.5f) => Enumerable.Range(0, count).Select(i => i >= 7 && i <= bottomRow ? cover * width : 0f).ToArray();
        C(PortraitLibraryMath.HairFromShape(Rows(17), 32f) == PortraitHairStyle.Short, "hair ending above the jaw: short");
        C(PortraitLibraryMath.HairFromShape(Rows(20), 32f) == PortraitHairStyle.Medium, "hair to the jaw: medium");
        C(PortraitLibraryMath.HairFromShape(Rows(24), 32f) == PortraitHairStyle.Long && PortraitLibraryMath.HairFromShape(Rows(31), 32f) == PortraitHairStyle.Long, "hair past the jaw: long");
        C(PortraitLibraryMath.HairFromShape(Rows(9, 32f, 32, 0.05f), 32f) == PortraitHairStyle.Bald && PortraitLibraryMath.HairFromShape(new float[32], 32f) == PortraitHairStyle.Bald, "hardly anything drawn: bald");
        C(PortraitLibraryMath.HairFromShape(null!, 32f) == null && PortraitLibraryMath.HairFromShape(new float[4], 32f) == null, "nothing to read: not told");
        C(PortraitLibraryMath.HairFallback(PortraitSex.Female) == PortraitHairStyle.Long && PortraitLibraryMath.HairFallback(PortraitSex.Male) == PortraitHairStyle.Short, "untold: long for a woman, short for anyone else");
        PortraitBeard B(string name) => PortraitLibraryMath.BeardFromWords(PortraitLibraryMath.Tokens(name), false);
        foreach (var (name, want) in new[]
                 {
                     ("NoBeard", PortraitBeard.None), ("Stubble", PortraitBeard.Stubble), ("Moustache", PortraitBeard.Moustache), ("FurskinCurlyMoustache", PortraitBeard.Moustache),
                     ("Goatee", PortraitBeard.Short), ("SoulPatch", PortraitBeard.Short), ("VanDyke", PortraitBeard.Short), ("Circle", PortraitBeard.Short), ("Anchor", PortraitBeard.Short),
                     ("StacheAndChops", PortraitBeard.Full), ("MuttonChops", PortraitBeard.Full), ("Full", PortraitBeard.Full), ("Norse", PortraitBeard.Full), ("VRE_Hickock", PortraitBeard.Full)
                 })
            C(B(name) == want, $"the beard '{name}': {want} (read {B(name)})");
        C(PortraitLibraryMath.BeardFromWords(PortraitLibraryMath.Tokens("Full"), true) == PortraitBeard.None && beards.Count > 40, $"no beard where the game draws none; the player's {beards.Count} beards each read as one of five");
        PortraitBuild Bu(string name) => PortraitLibraryMath.BuildFromWords(PortraitLibraryMath.Tokens(name));
        C(Bu("Thin") == PortraitBuild.Slim && Bu("Fat") == PortraitBuild.Heavy && Bu("Hulk") == PortraitBuild.Broad && Bu("Male") == PortraitBuild.Average && Bu("Female") == PortraitBuild.Average
          && Bu("Child") == PortraitBuild.Average, "the game's body types to the four builds");
        C(PortraitLibraryMath.EraOf(1) == PortraitEra.Tribal && PortraitLibraryMath.EraOf(2) == PortraitEra.Tribal && PortraitLibraryMath.EraOf(3) == PortraitEra.Medieval
          && PortraitLibraryMath.EraOf(4) == PortraitEra.Industrial && PortraitLibraryMath.EraOf(5) == PortraitEra.Spacer && PortraitLibraryMath.EraOf(7) == PortraitEra.Spacer && PortraitLibraryMath.EraOf(0) == PortraitEra.Unknown,
            "the game's tech levels to the four ages of clothes");
        var W = (int tech, int layer, bool torso) => new PortraitLibraryMath.WornPiece(tech, layer, torso);
        C(PortraitLibraryMath.EraOfWorn(new[] { W(4, 10, true), W(3, 30, true), W(5, 50, false) }, 2) == PortraitEra.Medieval, "a medieval coat over an industrial shirt, a spacer helmet: medieval");
        C(PortraitLibraryMath.EraOfWorn(new[] { W(0, 30, true), W(2, 10, true) }, 4) == PortraitEra.Tribal, "a piece of unknown age is passed over");
        C(PortraitLibraryMath.EraOfWorn(Array.Empty<PortraitLibraryMath.WornPiece>(), 2) == PortraitEra.Tribal && PortraitLibraryMath.EraOfWorn(null!, 0) == PortraitEra.Unknown,
            "nothing worn: their people's age, else any");
        List<string> T(string s) => PortraitLibraryMath.Tokens(s);
        C(PortraitLibraryMath.DressOf(null!, true, false, false, false) == PortraitDress.Rags && PortraitLibraryMath.DressOf(T("Apparel_Parka"), false, true, false, false) == PortraitDress.Rags
          && PortraitLibraryMath.DressOf(T("Apparel_PowerArmor"), false, false, true, false) == PortraitDress.Armour && PortraitLibraryMath.DressOf(T("Apparel_Robe"), false, false, false, false) == PortraitDress.Robes
          && PortraitLibraryMath.DressOf(T("Apparel_Cape"), false, false, false, false) == PortraitDress.Robes && PortraitLibraryMath.DressOf(T("Apparel_Duster"), false, false, false, true) == PortraitDress.Fine
          && PortraitLibraryMath.DressOf(T("Apparel_FormalShirt"), false, false, false, false) == PortraitDress.Fine && PortraitLibraryMath.DressOf(T("Apparel_CollarShirt"), false, false, false, false) == PortraitDress.Work,
            "the kind of clothes: nothing on the torso or in tatters rags, armour, robes and cloaks, fine, else work");
        List<string> Fe(string gene) => PortraitLibraryMath.FeaturesOf(T(gene));
        C(Fe("Ears_Pointed").SequenceEqual(new[] { "pointedEars" }) && Fe("Ears_Floppy").SequenceEqual(new[] { "longEars" }) && Fe("Ears_Cat").SequenceEqual(new[] { "longEars" })
          && Fe("Headbone_MiniHorns").SequenceEqual(new[] { "horns" }) && Fe("Headbone_CenterHorn").SequenceEqual(new[] { "horns" }) && Fe("Headbone_Human").Count == 0
          && Fe("Furskin").SequenceEqual(new[] { "fur" }) && Fe("Nose_Pig").SequenceEqual(new[] { "snout" }) && Fe("Tail_Furry").Contains("tail") && Fe("Skin_Melanin5").Count == 0,
            "features from the genes' words: ears, horns, fur, a snout, a tail; the plain human head and skin colours none");

        // ---------------- The matcher ----------------
        var man = Look(PortraitSex.Male, PortraitAge.Adult, "#e0b898", "#4a3020", PortraitHairStyle.Short, PortraitBeard.Full, PortraitBuild.Broad, PortraitEra.Medieval, PortraitDress.Work);
        var aldoBase = samples.First(r => r.name == "Portrait_Sample_Aldo");
        C(PortraitLibraryMath.Distance(man, aldoBase) == 0f, "a picture of exactly their look: no distance");
        PortraitPick Pick(PortraitLook l, IList<PortraitRecord> recs, int seed, Func<string, int>? uses = null) => PortraitLibraryMath.Pick(l, recs, seed, uses ?? (_ => 0));
        C(!Pick(man, new List<PortraitRecord>(), 1).Any && !Pick(man, null!, 1).Any, "an empty library: no picture (the game's own face)");
        var p1 = Pick(man, samples, 1);
        C(p1.Any && p1.character == "sample_aldo" && samples[p1.index].variant == "base", "the closest face, in its plain variant");
        // Marks follow the body: a lost eye takes the variant with it; a mark never shown on someone without it.
        var oneEye = Look(PortraitSex.Male, PortraitAge.Adult, "#e0b898", "#4a3020", PortraitHairStyle.Short, PortraitBeard.Full, PortraitBuild.Broad, PortraitEra.Medieval, PortraitDress.Work, marks: new[] { "lostEye" });
        var v1 = PortraitLibraryMath.Variant(oneEye, samples, "sample_aldo");
        C(v1.Any && samples[v1.index].variant == "lostEye", "a lost eye: the same face's variant with the eye patch");
        var v2 = PortraitLibraryMath.Variant(man, samples, "sample_aldo");
        C(v2.Any && samples[v2.index].variant == "base", "both eyes: never the eye patch");
        var oldAldo = Look(PortraitSex.Male, PortraitAge.Old, "#e0b898", "#a0a0a0", PortraitHairStyle.Short, PortraitBeard.Full, PortraitBuild.Broad, PortraitEra.Medieval, PortraitDress.Work);
        var v3 = PortraitLibraryMath.Variant(oldAldo, samples, "sample_aldo");
        C(v3.Any && samples[v3.index].variant == "older", "grown old: the same face's older variant");
        var bionic = Look(PortraitSex.Male, PortraitAge.Adult, "#e0b898", "#4a3020", PortraitHairStyle.Short, PortraitBeard.Full, PortraitBuild.Broad, PortraitEra.Medieval, PortraitDress.Work, marks: new[] { "bionicEye" });
        var v4 = PortraitLibraryMath.Variant(bionic, samples, "sample_aldo");
        C(v4.Any && samples[v4.index].variant == "base", "a mark the face has no variant for (a bionic eye): its plain variant");
        C(PortraitLibraryMath.MarkKey("eyePatch") == "lostEye" && PortraitLibraryMath.Shows(samples.First(r => r.variant == "lostEye"), oneEye), "an eye patch is a lost eye");
        // The kind first.
        var darkElf = Look(PortraitSex.Female, PortraitAge.Adult, "#6a6f8a", "#e8e8f0", PortraitHairStyle.Long, xenotype: "devdesigner_xenotypedarkelves", features: new[] { "pointedEars" });
        var pe = Pick(darkElf, samples, 3);
        C(pe.Any && samples[pe.index].name == "Portrait_Sample_DarkElf", "a dark elf gets the dark elves' picture");
        var elfLikeInes = Look(PortraitSex.Female, PortraitAge.Young, "(0.55, 0.35, 0.23)", "#1e1a18", PortraitHairStyle.Braids, xenotype: "devdesigner_xenotypedarkelves", features: new[] { "pointedEars" });
        var pe2 = Pick(elfLikeInes, samples, 3);
        var inesAt = samples.IndexOf(ines);
        C(PortraitLibraryMath.Distance(elfLikeInes, ines) < 2f && float.IsInfinity(PortraitLibraryMath.Distances(elfLikeInes, samples)[inesAt]) && (!pe2.Any || samples[pe2.index].name == "Portrait_Sample_DarkElf"),
            "a kind with pictures gets only its own kind's (or the game's face), never a baseliner's that looks closer");
        var baselinerWoman = Look(PortraitSex.Female, PortraitAge.Adult, "#6a6f8a", "#e8e8f0", PortraitHairStyle.Long);
        var pb = Pick(baselinerWoman, samples, 3);
        C(!pb.Any || samples[pb.index].xenotypes.Count == 0, "a baseliner never gets a xenotype's picture");
        var hussar = Look(PortraitSex.Female, PortraitAge.Young, "(0.55, 0.35, 0.23)", "#1e1a18", PortraitHairStyle.Braids, PortraitBeard.None, PortraitBuild.Slim, PortraitEra.Industrial, PortraitDress.Work, xenotype: "hussar");
        var ph = Pick(hussar, samples, 3);
        C(ph.Any && samples[ph.index].name == "Portrait_Sample_Ines" && Near(ph.distance, PortraitLibraryMath.NearKindCost),
            "a kind with no pictures that looks near a human (a hussar): the nearest baseliner's, a little further");
        var yttakin = Look(PortraitSex.Female, PortraitAge.Young, "(0.55, 0.35, 0.23)", "#1e1a18", PortraitHairStyle.Braids, xenotype: "yttakin", features: new[] { "fur" });
        var impid = Look(PortraitSex.Female, PortraitAge.Young, "(0.55, 0.35, 0.23)", "#1e1a18", PortraitHairStyle.Braids, xenotype: "impid", features: new[] { "horns", "pointedEars" });
        var green = Look(PortraitSex.Female, PortraitAge.Young, "#73a659", "#1e1a18", PortraitHairStyle.Braids, xenotype: "");
        C(!Pick(yttakin, samples, 3).Any && !Pick(impid, samples, 3).Any && !Pick(green, samples, 3).Any && yttakin.FarFromHuman && impid.FarFromHuman && green.FarFromHuman,
            "a kind with no pictures far from a human's look (fur, horns, a skin no human grows): the game's own face");
        var alien = Look(PortraitSex.Female, PortraitAge.Young, "(0.55, 0.35, 0.23)", "#1e1a18", PortraitHairStyle.Braids, xenotype: "baseliner", race: "alien_kurin");
        alien.humanShaped = false;
        alien.baseliner = false;
        C(!Pick(alien, samples, 3).Any, "another mod's race of its own shape, with no pictures: the game's own face");
        var kurinPic = new PortraitRecord { name = "kurin", races = { "alien_kurin" }, sex = PortraitSex.Female, age = PortraitAge.Young, skin = Hex("#8c5a3c"), skinSet = true };
        var pk = Pick(alien, samples.Append(kurinPic).ToList(), 3);
        C(pk.Any && pk.character == "kurin", "with its own race's pictures, those");
        // Sex and age.
        var womanAldo = Look(PortraitSex.Female, PortraitAge.Adult, "#e0b898", "#4a3020", PortraitHairStyle.Short, PortraitBeard.Full, PortraitBuild.Broad, PortraitEra.Medieval, PortraitDress.Work);
        var pw = Pick(womanAldo, samples, 3);
        C(!pw.Any || samples[pw.index].sex != PortraitSex.Male, "a woman never gets a man's picture");
        var child = Look(PortraitSex.Male, PortraitAge.Child, "#e0b898", "#4a3020", PortraitHairStyle.Short, PortraitBeard.None, PortraitBuild.Average, PortraitEra.Medieval, PortraitDress.Work);
        C(!Pick(child, samples, 3).Any, "a child never gets a grown man's picture");
        var young = Look(PortraitSex.Male, PortraitAge.Young, "#e0b898", "#4a3020", PortraitHairStyle.Short, PortraitBeard.Full, PortraitBuild.Broad, PortraitEra.Medieval, PortraitDress.Work);
        var py = Pick(young, samples, 3);
        C(py.Any && py.character == "sample_aldo" && Near(py.distance, PortraitLibraryMath.NearAgeCost), "no young man's picture: a grown one stands in, further");
        var headwearPic = new PortraitRecord
        {
            name = "helmed", sex = PortraitSex.Male, age = PortraitAge.Adult, headwear = true, skin = Hex("#e0b898"), skinSet = true, hairColour = Hex("#4a3020"), hairColourSet = true, beard = PortraitBeard.Full,
            build = PortraitBuild.Broad, era = PortraitEra.Medieval, dress = PortraitDress.Work
        };
        var helmed = Look(PortraitSex.Male, PortraitAge.Adult, "#e0b898", "#4a3020", PortraitHairStyle.Short, PortraitBeard.Full, PortraitBuild.Broad, PortraitEra.Medieval, PortraitDress.Work, headwear: true);
        C(!Pick(man, new List<PortraitRecord> { headwearPic }, 1).Any && Pick(helmed, new List<PortraitRecord> { headwearPic }, 1).Any, "a picture with headwear only for someone wearing it, with headgear shown");
        // The distance's order: skin and clothes' age most, then hair colour, then the rest.
        float D(PortraitLook l, PortraitRecord r) => PortraitLibraryMath.Distance(l, r);
        PortraitRecord Rec(string skin = "#e0b898", string hair = "#4a3020", PortraitHairStyle style = PortraitHairStyle.Short, PortraitEra era = PortraitEra.Medieval) =>
            new PortraitRecord { name = "r", sex = PortraitSex.Male, skin = Hex(skin), skinSet = true, hairColour = Hex(hair), hairColourSet = true, hair = style, beard = PortraitBeard.Full, build = PortraitBuild.Broad, era = era, dress = PortraitDress.Work };
        C(D(man, Rec(skin: "#e6c0a2")) == 0f, "a skin a few points apart (a painter's tone): no cost");
        C(D(man, Rec(skin: "#c4956c")) > D(man, Rec(hair: "#8a6440")) && D(man, Rec(era: PortraitEra.Industrial)) > D(man, Rec(style: PortraitHairStyle.Medium)),
            "a skin band apart costs more than a shade of hair; an age of clothes more than a hair's length");
        C(D(man, Rec(skin: "#5a3a28")) > PortraitLibraryMath.CloseEnough, "a fair man and a dark brown picture: too far");
        C(D(man, Rec(hair: "#e6e6e2")) <= PortraitLibraryMath.HairColourMost, "the hair's colour costs at most its most");
        // Two alike people get different faces where the library has them; the same person always the same.
        var wave = Wave1(root);
        C(wave.Count == 200, $"the first wave's 200 prompts read as a library ({wave.Count})");
        var twinLook = Look(PortraitSex.Female, PortraitAge.Young, "#f1d6c2", "#9a3c1c", PortraitHairStyle.Long, era: PortraitEra.Medieval, dress: PortraitDress.Work);
        var first = Pick(twinLook, wave, 12345);
        C(first.Any && Enumerable.Range(0, 10).All(k => Pick(twinLook, wave, 12345).index == first.index), "the same person: the same face every time");
        // Three faces of one look, and one a little further: alike people spread over the three, never the further one.
        PortraitRecord Face(string name, string hair) => new PortraitRecord
        {
            name = name, sex = PortraitSex.Female, age = PortraitAge.Young, skin = Hex("#f1d6c2"), skinSet = true, hairColour = Hex(hair), hairColourSet = true, hair = PortraitHairStyle.Long,
            era = PortraitEra.Medieval, dress = PortraitDress.Work
        };
        var alike = new List<PortraitRecord> { Face("a", "#9a3c1c"), Face("b", "#9a3c1c"), Face("c", "#9a3c1c"), Face("far", "#1c1a19") };
        var uses = new Dictionary<string, int>();
        var picked = new List<string>();
        for (int k = 0; k < 3; k++)
        {
            var t = Pick(twinLook, alike, 1, c => uses.TryGetValue(c, out int u) ? u : 0);
            picked.Add(t.character);
            uses[t.character] = uses.TryGetValue(t.character, out int had) ? had + 1 : 1;
        }
        C(picked.Distinct().Count() == 3 && !picked.Contains("far"), $"three alike people get three different faces ({string.Join(", ", picked)})");
        C(Enumerable.Range(1, 60).Select(id => Pick(twinLook, alike, id).character).Distinct().Count() == 3, "alike people with no faces kept yet spread over the equally near faces by their ids");
        var nearer = new List<PortraitRecord> { Face("near", "#9a3c1c"), Face("browner", "#7a3c1c") };
        C(Enumerable.Range(1, 30).All(id => Pick(twinLook, nearer, id).character == "near"), "the first person of a look always gets the closest face, not one a little further");
        var nearUses = new Dictionary<string, int> { ["near"] = 1 };
        C(Pick(twinLook, nearer, 5, c => nearUses.TryGetValue(c, out int u) ? u : 0).character == "browner", "the next alike person the next closest");
        // Picking again.
        C(PortraitLibraryMath.PickAgain(false, false, null!, "k", null!, "w", false, 0, 1, 9f, 0f), "nothing kept: picked");
        C(!PortraitLibraryMath.PickAgain(true, true, "k", "k", "w", "w2", true, 1, 1, 3f, 0f), "a face kept for good: new clothes or dyed hair keep it");
        C(PortraitLibraryMath.PickAgain(true, true, "k", "k2", "w", "w", true, 1, 1, 0f, 0f), "another kind or sex now: picked again");
        C(PortraitLibraryMath.PickAgain(true, true, "k", "k", "w", "w", false, 1, 1, 0f, 0f), "the face gone from the library, or none of its variants may stand for them: picked again");
        C(!PortraitLibraryMath.PickAgain(true, true, "k", "k", "w", "w", true, 1, 2, 3f, 2f) && PortraitLibraryMath.PickAgain(true, true, "k", "k", "w", "w", true, 1, 2, 3f, 1.5f),
            "a grown library: picked again only for a face clearly closer");
        C(PortraitLibraryMath.PickAgain(true, false, "k", "k", "w", "w", false, 1, 2, 9f, 9f) && PortraitLibraryMath.PickAgain(true, false, "k", "k", "w", "w2", false, 1, 1, 9f, 9f)
          && !PortraitLibraryMath.PickAgain(true, false, "k", "k", "w", "w", false, 2, 2, 9f, 9f), "kept with no face close enough: looked at again when the library or their looks change");

        // ---------------- The views of a whole picture ----------------
        var lib = PortraitViewMath.Library;
        foreach (var frame in new[] { FaceFrame.Face, FaceFrame.Bust, FaceFrame.Figure })
        foreach (float aspect in new[] { 1f, 94f / 134f, 0.5f, 1.6f, 3f })
        foreach (float lift in new[] { 0f, 0.05f, -0.08f, 0.5f })
        {
            var v = PortraitViewMath.View(lib, frame, aspect, lift);
            string at = $"{frame}, {aspect:0.00} across, lifted {lift}: {v}";
            C(v.x >= PortraitViewMath.Margin - 1e-5f && v.y >= PortraitViewMath.Margin - 1e-5f && v.x + v.w <= 1f - PortraitViewMath.Margin + 1e-5f && v.y + v.h <= 1f - PortraitViewMath.Margin + 1e-5f,
                $"{at}: inside the picture, a margin kept");
            C(Near(v.w / v.h, aspect, 0.01f), $"{at}: the tile's shape");
            C(Near(v.headU, 0.5f, 0.01f), $"{at}: the head in the middle across");
        }
        var bust = PortraitViewMath.View(lib, FaceFrame.Bust, 1f);
        var face = PortraitViewMath.View(lib, FaceFrame.Face, 1f);
        var card = PortraitViewMath.View(lib, FaceFrame.Figure, 94f / 134f);
        C(Near(bust.headShare, 0.5f, 0.02f) && bust.headV > 0.28f && bust.headV < 0.42f, $"the large portrait: the head about half the square, high in it ({bust})");
        C(face.headShare > bust.headShare && face.headV > 0.3f && face.headV < 0.48f, $"the round portraits: closer on the face ({face})");
        C(card.h >= 0.95f && card.w < 0.7f, $"the card's tall tile: the picture's height, its sides cut ({card})");
        C(PortraitViewMath.View(lib, FaceFrame.Bust, 1f, 0.025f).headV < bust.headV && PortraitViewMath.View(lib, FaceFrame.Bust, 1f, -0.05f).headV > bust.headV,
            "a pose's lift moves the head up in the portrait, a lowered head down");
        var odd = PortraitViewMath.View(new PictureLayout(float.NaN, 2f, 0f, FaceTurn.Front), FaceFrame.Bust, float.NaN);
        C(Near(odd.headShare, bust.headShare) && Near(odd.w, bust.w), "a layout or tile that makes no sense: the library's, square");
        var off = PortraitViewMath.View(new PictureLayout(0.42f, 0.33f, 0.4f, FaceTurn.Left), FaceFrame.Face, 1f);
        C(Near(off.headU, 0.5f, 0.01f) && Near(off.headShare, PortraitViewMath.HeadShareOf(FaceFrame.Face), 0.01f), "a picture whose head is off the middle (its def says where): framed on the head");
        C(PortraitViewMath.FacesOf(PortraitFacing.ThreeQuarterLeft) == FaceTurn.Left && PortraitViewMath.FacesOf(PortraitFacing.Right) == FaceTurn.Right && PortraitViewMath.FacesOf(PortraitFacing.Front) == FaceTurn.Front,
            "a picture's facing: a little or three-quarter to a side is that side");
        var leftPic = new PictureLayout(0.5f, 0.36f, 0.45f, FaceTurn.Left);
        C(PortraitViewMath.Mirror(leftPic, PortraitViewMath.Asked(FaceTurn.Front, FaceTurn.Right)) && !PortraitViewMath.Mirror(leftPic, PortraitViewMath.Asked(FaceTurn.Left, FaceTurn.Right)),
            "a picture facing left is turned to face its text on the right, but faces left as the right one of a pair");
        C(!PortraitViewMath.Mirror(leftPic, PortraitViewMath.Asked(FaceTurn.Front, FaceTurn.Front)) && !PortraitViewMath.Mirror(lib, FaceTurn.Right), "no text beside it: as drawn; a picture facing the front never mirrored");
        PortraitViewMath.TexCoords(bust, false, out float u0, out float v0, out float w0, out float h0);
        PortraitViewMath.TexCoords(bust, true, out float u1, out _, out float w1, out _);
        C(Near(u0, bust.x) && Near(v0, 1f - bust.y - bust.h) && Near(w0, bust.w) && Near(h0, bust.h) && Near(u1, bust.x + bust.w) && Near(w1, -bust.w),
            "the texture's coordinates from its foot up; mirrored, from its right");
        foreach (var b in (FaceBearing[])Enum.GetValues(typeof(FaceBearing)))
        {
            PortraitViewMath.Pose(FacePoseMath.Shift(b, FaceTurn.Front, 2), out float l2, out float turn2);
            C(Math.Abs(l2) <= 0.1f && Math.Abs(turn2) <= 5f, $"{b}: a small move of the whole picture ({l2}, {turn2}°)");
        }

        // ---------------- The seams ----------------
        string engine = Read("UI/Faces/FaceEngine.cs"), pics = Read("UI/Faces/FaceEnginePictures.cs"), sources = Read("UI/Faces/PortraitSources.cs"), libSrc = Read("UI/Faces/PortraitLibrary.cs");
        string reader = Read("UI/Faces/PortraitReader.cs"), keeper = Read("UI/Faces/PortraitKeeper.cs"), kit = Read("UI/UiKit.cs");
        C(Regex.Matches(engine, @"PortraitSources\.For\(p\)").Count == 2 && engine.Contains("if (pic != null) DrawPicture(") && engine.Contains("DrawPictureRound(inner, p, pic, pose, alpha);"),
            "the face engine asks the portrait sources first, in the large and the round portraits, and draws the game's face where none has a picture");
        C(sources.Contains("interface IPortraitSource") && sources.Contains("Sources = { PortraitLibrary.Source }") && libSrc.Contains("public sealed class PortraitLibrary : IPortraitSource"),
            "the library is one portrait source of a list; another (a portrait painted live) is added there");
        C(engine.Contains("PortraitSources.Forget(id);") && engine.Contains("PortraitSources.NewGame();"), "the sources hear when the game marks a portrait dirty and when a game starts");
        C(libSrc.Contains("DefDatabase<PortraitDef>.AllDefsListForReading") && defSrc.Contains("public class PortraitDef : Def") && defSrc.Contains("ContentFinder<Texture2D>.Get(texPath, false)")
          && defSrc.Contains("public override IEnumerable<string> ConfigErrors()"), "a def per picture, from any mod; its texture the game's; what is wrong with it among the def errors");
        C(libSrc.Contains("PortraitLibraryMath.Pick(look, records, id,") && libSrc.Contains("PortraitLibraryMath.Variant(look, records, k.character)") && libSrc.Contains("PortraitLibraryMath.PickAgain(")
          && libSrc.Contains("keeper?.Set(id, pick.character,"), "each person matched, their face kept and its variant chosen for their body now");
        C(libSrc.Contains("ColonyCouncilMod.Settings?.portraitLibrary ?? true") && libSrc.Contains("records.Count > 0"), "with the setting off or no pictures, the game's own face");
        C(Regex.Matches(libSrc, @"\[DebugAction\(""Rimfolk"", ""Portraits: ").Count == 3 && libSrc.Contains("best matches with their scores"),
            "dev actions: the best matches with their scores in the log, pick one person's again, everyone's");
        C(keeper.Contains("Scribe_Collections.Look(ref kept, \"kept\", LookMode.Deep);") && keeper.Contains("public const int MaxKept = 400;") && keeper.Contains("ForgetAfterDays"),
            "the faces kept in the save, at most 400, a stranger not pictured for a long while let go");
        C(reader.Contains("p.story.SkinColor") && reader.Contains("p.story.HairColor") && reader.Contains("p.style?.beardDef") && reader.Contains("p.apparel?.WornApparel")
          && reader.Contains("p.DevelopmentalStage.Baby()") && reader.Contains("genes.Xenotype") && reader.Contains("p.story.bodyType") && reader.Contains("Hediff_MissingPart")
          && reader.Contains("BodyPartTagDefOf.SightSource") && reader.Contains("g.def.renderNodeProperties"), "a pawn's looks read from the game's own data, for any mod's race, xenotype, hair, beard and clothes");
        C(reader.Contains("ContentFinder<Texture2D>.Get(def.texPath + \"_south\", false)") && reader.Contains("PortraitLibraryMath.HairFromShape(rows, S)") && reader.Contains("hairs[def] = h;"),
            "a hair its words don't tell: its front texture measured once");
        C(pics.Contains("PortraitViewMath.View(pic.layout, frame,") && pics.Contains("PortraitViewMath.Mirror(pic.layout, PortraitViewMath.Asked(pose.turn, TextSide(") && pics.Contains("GreyPicture(pic.texture)"),
            "a picture framed by its layout for each place, turned to its text or the other of a pair, grey for the dead");
        C(kit.Contains("public static void DrawInCircle(Rect r, Texture tex, Rect view)"), "a part of a picture drawn in a circle");
        string settings = Read("CouncilSettings.cs");
        C(settings.Contains("public bool portraitLibrary = true;") && settings.Contains("Scribe_Values.Look(ref portraitLibrary, \"portraitLibrary\", true);")
          && SettingsCatalog.Find("portraitLibrary")?.section == "CC_SetSec_Portraits", "the setting 'Painted portraits from the library', on, under Portraits");
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Portraits.xml"));
        foreach (var k in new[] { "CC_Settings_PortraitLibrary", "CC_Settings_PortraitLibrary_Tip", "CC_SetNote_PortraitLibrary" })
            C(keyed.Contains("<" + k + ">"), $"the words: {k}");

        WriteTable(root, wave, C);
        return n;
    }

    // ---------------- The first wave as a library, and the matcher's table ----------------

    private static readonly Dictionary<string, string> SkinPaint = new() { ["s1"] = "#f1d6c2", ["s2"] = "#e0b898", ["s3"] = "#c4956c", ["s4"] = "#8d5a3b", ["s5"] = "#5a3a28" };

    private static readonly Dictionary<string, string> HairPaint = new()
    {
        ["black"] = "#1c1a19", ["brown"] = "#4a3020", ["lbrown"] = "#8a6440", ["blond"] = "#d8b878", ["red"] = "#9a3c1c", ["grey"] = "#9a9a9a", ["white"] = "#e6e6e2"
    };

    /// <summary>The first wave's prompt list read as a library, each row a face of its own, its colours the painted mid-tones of its words.</summary>
    private static List<PortraitRecord> Wave1(string root)
    {
        string csv = Path.Combine(root, "docs", "portrait-art", "prompts.csv");
        var list = new List<PortraitRecord>();
        if (!File.Exists(csv)) return list;
        foreach (var row in File.ReadAllLines(csv).Skip(1).Where(l => l.Length > 0))
        {
            var c = row.Split(',');
            if (c.Length < 9) continue;
            list.Add(new PortraitRecord
            {
                name = c[0].Replace(".png", ""),
                sex = c[2] == "m" ? PortraitSex.Male : PortraitSex.Female,
                age = Enum.Parse<PortraitAge>(c[3], true),
                skin = Hex(SkinPaint[c[4]]), skinSet = true,
                hairColour = Hex(HairPaint[c[5]]), hairColourSet = true,
                hair = c[6] == "bald" ? PortraitHairStyle.Bald : c[6] == "long" ? PortraitHairStyle.Long : PortraitHairStyle.Short,
                beard = c[7] == "beard" ? PortraitBeard.Full : c[7] == "stubble" ? PortraitBeard.Stubble : c[7] == "moustache" ? PortraitBeard.Moustache : PortraitBeard.None,
                era = Enum.Parse<PortraitEra>(c[8], true),
                facing = PortraitFacing.ThreeQuarterLeft
            });
        }
        return list;
    }

    /// <summary>Sample people as the game might read them (their skin and hair in the game's own colours).</summary>
    private static (string who, PortraitLook? look)[] Samples()
    {
        PortraitLook L(PortraitSex s, PortraitAge a, string skin, string hair, PortraitHairStyle h, PortraitBeard b, PortraitEra e, string xeno = "baseliner", string[]? f = null, string[]? m = null) =>
            Look(s, a, skin, hair, h, b, PortraitBuild.Average, e, PortraitDress.Work, xeno, "human", f, m);
        const PortraitSex M = PortraitSex.Male, F = PortraitSex.Female;
        return new (string, PortraitLook?)[]
        {
            ("a grown man, the game's fairest skin, dark brown hair cut short, a full beard, in a medieval tunic", L(M, PortraitAge.Adult, "f2ede0", "594030", PortraitHairStyle.Short, PortraitBeard.Full, PortraitEra.Medieval)),
            ("another grown man who looks just like him", L(M, PortraitAge.Adult, "f2ede0", "594030", PortraitHairStyle.Short, PortraitBeard.Full, PortraitEra.Medieval)),
            ("a grown woman, the game's darkest skin, long black hair, in an industrial jacket", L(F, PortraitAge.Adult, "402917", "1a1a1a", PortraitHairStyle.Long, PortraitBeard.None, PortraitEra.Industrial)),
            ("a young woman, very fair, long red hair tied back, medieval", L(F, PortraitAge.Young, "ffefd5", "a8481e", PortraitHairStyle.Tied, PortraitBeard.None, PortraitEra.Medieval)),
            ("a young man, a tan skin, short light brown hair, stubble, in a flak vest", L(M, PortraitAge.Young, "e49e5a", "8c6a48", PortraitHairStyle.Short, PortraitBeard.Stubble, PortraitEra.Industrial)),
            ("an old woman, a brown skin, long grey hair in a bun, tribal", L(F, PortraitAge.Old, "b3804d", "a6a6a6", PortraitHairStyle.Bun, PortraitBeard.None, PortraitEra.Tribal)),
            ("an old man, fair, bald, a white beard, in a spacer suit", L(M, PortraitAge.Old, "e4c699", "ebebe6", PortraitHairStyle.Bald, PortraitBeard.Full, PortraitEra.Spacer)),
            ("an old man who never went grey: brown hair, short, a moustache, medieval", L(M, PortraitAge.Old, "e4c699", "594030", PortraitHairStyle.Short, PortraitBeard.Moustache, PortraitEra.Medieval)),
            ("a boy of ten, a dark skin, short black hair, tribal", L(M, PortraitAge.Child, "634624", "1a1a1a", PortraitHairStyle.Short, PortraitBeard.None, PortraitEra.Tribal)),
            ("a girl of eight, fair, long blond hair, medieval", L(F, PortraitAge.Child, "ffefbd", "e8cc90", PortraitHairStyle.Long, PortraitBeard.None, PortraitEra.Medieval)),
            ("a grown woman, fair, short blond hair, in a spacer suit", L(F, PortraitAge.Adult, "ffefbd", "e8cc90", PortraitHairStyle.Short, PortraitBeard.None, PortraitEra.Spacer)),
            ("a grown woman, a dark skin, her head shaved, industrial", L(F, PortraitAge.Adult, "634624", "1a1a1a", PortraitHairStyle.Bald, PortraitBeard.None, PortraitEra.Industrial)),
            ("a grown man with dyed blue hair, short, industrial", L(M, PortraitAge.Adult, "ffefbd", "3359cc", PortraitHairStyle.Short, PortraitBeard.None, PortraitEra.Industrial)),
            ("a teenage boy of fifteen, very fair, short blond hair, industrial", L(M, PortraitAge.Young, "f2ede0", "e8cc90", PortraitHairStyle.Short, PortraitBeard.None, PortraitEra.Industrial)),
            ("a grown man, a dark skin, short black hair, a moustache, tribal", L(M, PortraitAge.Adult, "402917", "1a1a1a", PortraitHairStyle.Short, PortraitBeard.Moustache, PortraitEra.Tribal)),
            ("a hussar woman, a tan skin, long brown hair, spacer (no hussar pictures yet)", L(F, PortraitAge.Adult, "e49e5a", "594030", PortraitHairStyle.Long, PortraitBeard.None, PortraitEra.Spacer, "hussar")),
            ("an impid man, horns and pointed ears (no impid pictures yet)", L(M, PortraitAge.Adult, "e49e5a", "1a1a1a", PortraitHairStyle.Short, PortraitBeard.None, PortraitEra.Tribal, "impid", new[] { "horns", "pointedEars" })),
            ("a dark elf woman (no dark elf pictures yet)", L(F, PortraitAge.Adult, "6a6f8a", "e8e8f0", PortraitHairStyle.Long, PortraitBeard.None, PortraitEra.Medieval, "devdesigner_xenotypedarkelves", new[] { "pointedEars" })),
            ("a toddler of two", null),
        };
    }

    /// <summary>The matcher's choices for the sample people, with the trial's 16 pictures and the first wave's 200, written to docs/portrait-previews/matcher.md.</summary>
    private static void WriteTable(string root, List<PortraitRecord> wave, Action<bool, string> C)
    {
        if (wave.Count == 0) return;
        var pilotNames = new HashSet<string>(File.ReadAllLines(Path.Combine(root, "docs", "portrait-art", "prompts.csv")).Skip(1).Select(l => l.Split(','))
            .Where(c => c.Length > 1 && c[1] == "yes").Select(c => c[0].Replace(".png", "")));
        var trial = wave.Where(r => pilotNames.Contains(r.name)).ToList();
        var sb = new StringBuilder();
        sb.AppendLine("# The portrait library's matcher: who gets which picture");
        sb.AppendLine();
        sb.AppendLine("Written by the tests (`Tools/Tests/PortraitLibraryTests.cs`). There is no art yet, so this table stands in for preview");
        sb.AppendLine("sheets: the first wave's prompt list (`docs/portrait-art/prompts.csv`) is read as a library, each row a face of its own");
        sb.AppendLine("with the painted mid-tones of its words (skin s1 #f1d6c2 to s5 #5a3a28), and each sample person, with their skin and");
        sb.AppendLine("hair in the game's own colours, is matched against the trial's 16 pictures and against all 200. The score is the");
        sb.AppendLine("distance (0 is their exact look; above " + PortraitLibraryMath.CloseEnough.ToString("0") + " the game's own face is kept); the last column gives the nearest three of the 200.");
        sb.AppendLine();
        sb.AppendLine("| Person | Trial (16) | All 200 | Nearest of the 200 |");
        sb.AppendLine("|---|---|---|---|");
        var used = new Dictionary<string, int>();
        var allPicks = new List<string>();
        var samples = Samples();
        for (int row = 0; row < samples.Length; row++)
        {
            var (who, look) = samples[row];
            if (look == null)
            {
                C(!PortraitLibraryMath.AgeOf(2f, 80f, out _), "the table: a toddler keeps the game's own face");
                sb.AppendLine($"| {who} | the game's own face | the game's own face (no picture for a baby or a toddler) | |");
                continue;
            }
            int seed = 97 * (row + 1);
            string Cell(List<PortraitRecord> lib, bool count)
            {
                var pick = PortraitLibraryMath.Pick(look, lib, seed, c => count && used.TryGetValue(c, out int u) ? u : 0);
                if (!pick.Any) return float.IsInfinity(pick.distance) ? "the game's own face (none of their kind, sex and age)" : $"the game's own face (nearest {pick.distance:0.0})";
                if (count)
                {
                    used[pick.character] = used.TryGetValue(pick.character, out int u2) ? u2 + 1 : 1;
                    allPicks.Add(pick.character);
                }
                return $"`{lib[pick.index].name}` ({pick.distance:0.0})";
            }
            string trialCell = Cell(trial, false), allCell = Cell(wave, true);
            if (allPicks.Count < row + 1) allPicks.Add("");
            var d = PortraitLibraryMath.Distances(look, wave);
            var next = Enumerable.Range(0, wave.Count).Where(i => !float.IsInfinity(d[i])).OrderBy(i => d[i]).ThenBy(i => wave[i].name, StringComparer.Ordinal).Take(3)
                .Select(i => $"`{wave[i].name}` {d[i]:0.0}");
            sb.AppendLine($"| {who} | {trialCell} | {allCell} | {string.Join(", ", next)} |");
        }
        sb.AppendLine();
        if (allPicks.Count > 1 && allPicks[0].Length > 0 && allPicks[0] != allPicks[1])
            sb.AppendLine("The two men who look alike get different faces: of the nearest few, each person gets the nearest of those fewest others keep, then");
        else
            sb.AppendLine("The two men who look alike get the same face here, as the first wave has only one that close to them; with several of");
        if (allPicks.Count > 1 && allPicks[0].Length > 0 && allPicks[0] == allPicks[1])
            sb.AppendLine("that look, each person gets the nearest of the few fewest others keep, then one by their own id (the tests try it).");
        else
            sb.AppendLine("one by their own id.");
        sb.AppendLine("A kind with no pictures of its own gets a baseliner's where it looks near a human (the hussar),");
        sb.AppendLine("else the game's own face (the impid's horns, the dark elf's grey skin); once a kind's pictures are in, its people get");
        sb.AppendLine("only theirs. A person keeps their face for good; when their body changes (a lost eye, a scar, growing old) the face's");
        sb.AppendLine("variant with that mark is shown where the library has one.");
        string dir = Path.Combine(root, "docs", "portrait-previews");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "matcher.md");
        string text = sb.ToString().Replace("\r\n", "\n");
        if (!File.Exists(path) || File.ReadAllText(path).Replace("\r\n", "\n") != text) File.WriteAllText(path, text);
        C(File.Exists(path), "the matcher's table written to docs/portrait-previews/matcher.md");
    }
}
