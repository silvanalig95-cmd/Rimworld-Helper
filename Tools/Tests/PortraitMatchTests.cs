using System.Text;
using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// The portraits' hard rules of 23 November 2026 and keeping a picture for training (the player: "the race, xenotype, the
/// gender, also I would say the skin color and age ... always have to match the pawn for sure. And if there's no image
/// with this combination available for the pawn, then it should just take no image"; special things such as a bionic arm
/// only for pawns who have them; "maybe I can right click and then already flag for training ... or it's get copied to a
/// special location from which I can take the image to further train the model later"). The pure parts: their own age
/// band only for a new pick, a kept picture through its stage; the skin's eight tones, the game's nine melanin skins
/// against the painter's palette (read from the portrait scripts), a skin no human grows by its colour; the mark artificialArm
/// and the arm parts it is read from; training.csv written and read and the whole copy on a library of its own; the words
/// and the seams.
/// </summary>
public static class PortraitMatchTests
{
    private static PortraitColour Hex(string h) => PortraitLibraryMath.TryColour(h, out var c) ? c : throw new Exception("bad colour " + h);

    private static PortraitLook Look(PortraitAge age, string skin = "#e0b898", string xenotype = "baseliner", string[]? marks = null)
    {
        var l = new PortraitLook { sex = PortraitSex.Male, age = age, skin = Hex(skin), hairColour = Hex("#4a3020"), xenotype = xenotype, baseliner = xenotype == "baseliner" };
        foreach (var m in marks ?? Array.Empty<string>()) l.marks.Add(PortraitLibraryMath.MarkKey(m));
        return l;
    }

    private static PortraitRecord Rec(string name, PortraitAge age, string skin = "#e0b898", string? xenotype = null, string[]? marks = null)
    {
        var r = new PortraitRecord { name = name, sex = PortraitSex.Male, age = age, skin = Hex(skin), skinSet = true, hairColour = Hex("#4a3020"), hairColourSet = true };
        if (xenotype != null) r.xenotypes.Add(xenotype);
        foreach (var m in marks ?? Array.Empty<string>()) r.marks.Add(m);
        return r;
    }

    /// <summary>The game's skin colours: its nine melanin skins (Skin_Melanin1 to 9), and Biotech's skins no human grows with the missing list's word for each.</summary>
    private static readonly (string gene, string hex)[] Melanin =
    {
        ("Skin_Melanin1", "#f2ede0"), ("Skin_Melanin2", "#ffefd5"), ("Skin_Melanin3", "#ffefbd"), ("Skin_Melanin4", "#e4c699"), ("Skin_Melanin5", "#e49e5a"),
        ("Skin_Melanin6", "#b3804d"), ("Skin_Melanin7", "#825b30"), ("Skin_Melanin8", "#634624"), ("Skin_Melanin9", "#402917")
    };

    private static readonly (string gene, string hex, string word)[] Unusual =
    {
        ("Skin_InkBlack", "#0d0d0f", "black"), ("Skin_SlateGray", "#737880", "grey"), ("Skin_LightGray", "#b3b3b3", "grey"), ("Skin_SheerWhite", "#ebedfa", "white"),
        ("Skin_Blue", "#668cd9", "blue"), ("Skin_Purple", "#8c59a6", "purple"), ("Skin_Green", "#73a659", "green"), ("Skin_DeepRed", "#a61f1a", "red"),
        ("Skin_Orange", "#f28026", "orange"), ("Skin_DeepYellow", "#e6d140", "yellow"), ("Skin_PaleYellow", "#f2f299", "yellow")
    };

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "portraits' hard rules: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.Exists(Path.Combine(src, rel)) ? File.ReadAllText(Path.Combine(src, rel)) : "";
        string art = Path.Combine(root, "Tools", "PortraitArt");
        string Art(string file) => File.Exists(Path.Combine(art, file)) ? File.ReadAllText(Path.Combine(art, file)) : "";

        // ---------------- Their own age band ----------------
        {
            var bands = (PortraitAge[])Enum.GetValues(typeof(PortraitAge));
            C(bands.Length == 9 && bands.All(a => bands.All(b => PortraitLibraryMath.AgeMayStand(a, b) == (a == b))), "a picture only ever stands for a person of its own age band, of the nine");
            C(bands.All(a => bands.All(b => a == b ? PortraitLibraryMath.AgeCost(a, b) == 0f : float.IsInfinity(PortraitLibraryMath.AgeCost(a, b)))), "its own band costs nothing, any other may not stand");
            var adultPic = Rec("adult", PortraitAge.Adult);
            var young = Look(PortraitAge.Young);
            C(!PortraitLibraryMath.Pick(young, new List<PortraitRecord> { adultPic }, 1, _ => 0).Any, "a young man with only a grown man's picture: no picture (the game's own drawing)");
            var lib = new List<PortraitRecord> { adultPic, Rec("young", PortraitAge.Young), Rec("middle", PortraitAge.Middle), Rec("teen", PortraitAge.Teen) };
            var p = PortraitLibraryMath.Pick(young, lib, 1, _ => 0);
            C(p.Any && lib[p.index].name == "young", "with a young man's picture, that one");
            var choices = PortraitLibraryMath.Choices(young, lib, _ => false, PortraitPickMode.Closest);
            C(choices.Count == 1 && lib[choices[0]].name == "young", "choosing another picture offers only pictures of their own band");
            var drinker = Look(PortraitAge.Young);
            drinker.AddLook("drinker", false);
            var showsOnAdult = Rec("adultDrinker", PortraitAge.Adult);
            showsOnAdult.looks.Add("drinker");
            C(PortraitLibraryMath.LooksLacking(drinker, new List<PortraitRecord> { showsOnAdult }).SequenceEqual(new[] { "drinker" }), "a look shown only on another band's picture is still missing for them");
            var oldAge = Look(PortraitAge.Elder);
            C(!PortraitLibraryMath.Pick(oldAge, new List<PortraitRecord> { Rec("old", PortraitAge.Old) }, 1, _ => 0).Any, "an elder never gets an old picture as a new pick");
            // A picture kept stays through its stage: young, adult and middle-aged one stage, old and elder one.
            C(!PortraitLibraryMath.PickAgain(true, PortraitStage.Grown, PortraitLibraryMath.StageOf(PortraitAge.Adult), "young", true, 0, false, 1, 1, "young words", "adult words", 0)
              && !PortraitLibraryMath.PickAgain(true, PortraitStage.Old, PortraitLibraryMath.StageOf(PortraitAge.Elder), "old", true, 0, false, 1, 1, "old words", "elder words", 0),
                "a picture kept stays when the person crosses a band within its stage (young to adult, old to elder)");
            C(PortraitLibraryMath.PickAgain(true, PortraitStage.Teen, PortraitLibraryMath.StageOf(PortraitAge.Young), "teen", true, 0, false, 1, 1, "w", "w2", 0)
              && PortraitLibraryMath.PickAgain(true, PortraitStage.Grown, PortraitLibraryMath.StageOf(PortraitAge.Old), "middle", true, 0, false, 1, 1, "w", "w2", 0),
                "into the next stage (a teen grown up, a grown-up grown old): picked again, by their new band");
            C(PortraitLibraryMath.PickAgain(true, PortraitStage.Grown, PortraitStage.Grown, "", true, 0, false, 1, 1, "young words", "adult words", 0),
                "kept with no picture: looked at again in a new band (their words change), so a picture of it is found");
            var pin = Rec("pin", PortraitAge.Young);
            pin.pawn = "Avila";
            C(PortraitLibraryMath.PinFit(pin, Look(PortraitAge.Middle)) == 2, "a picture pinned to a colonist still fits by stage (the player's own choice)");
            string libSrc = Read("UI/Faces/PortraitLibrary.cs");
            C(libSrc.Contains("(int)PortraitLibraryMath.StageOf(ageNow) == k.stage") && libSrc.Contains("PortraitLibraryMath.Choices(look, records,"),
                "in the game a kept picture is held by its stage, the choices by the matcher's rules");
        }

        // ---------------- The skin's tones ----------------
        string Py(string pattern, string text) => Regex.Match(text, pattern, RegexOptions.Singleline).Groups[1].Value;
        var palette = Regex.Matches(Py(@"HUMAN_SKINS = \[(.*?)\]", Art("catalogue_words.py")), @"\('(\w+)', '([^']+)', '(#[0-9a-fA-F]{6})'\)")
            .Select(m => (slug: m.Groups[1].Value, word: m.Groups[2].Value, hex: m.Groups[3].Value)).ToList();
        var wordHex = Regex.Matches(Py(@"SKIN_WORD_HEX = \{(.*?)\}", Art("plan_batches.py")), @"'([a-z ]+)': '(#[0-9a-fA-F]{6})'")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        C(palette.Count == 7 && wordHex.Count >= 18, $"the painter's palette read from the portrait scripts: seven human skins ({palette.Count}), a colour for each of the missing list's skin words ({wordHex.Count})");
        C(PortraitLibraryMath.SkinToneFrom.Length + 1 == 8 && PortraitLibraryMath.SkinToneFrom.Zip(PortraitLibraryMath.SkinToneFrom.Skip(1), (a, b) => a > b).All(x => x),
            "eight tones by lightness, lightest first");
        for (int i = 0; i < palette.Count; i++)
        {
            var (_, word, hex) = palette[i];
            C(PortraitLibraryMath.SkinTone(Hex(hex)) == i && PortraitFolderMath.SkinWord(Hex(hex)) == word && wordHex.TryGetValue(word, out var same) && same == hex,
                $"the palette's {word} ({hex}): tone {i}, the missing list's word for it, the colour the planner paints for that word");
        }
        foreach (var (gene, hex) in Melanin)
        {
            var game = Hex(hex);
            string word = PortraitFolderMath.SkinWord(game);
            bool painted = wordHex.TryGetValue(word, out var paint);
            int tone = PortraitLibraryMath.SkinTone(game);
            C(!PortraitLibraryMath.UnusualSkin(game) && painted && Math.Abs(PortraitLibraryMath.SkinTone(Hex(paint!)) - tone) <= 1 && PortraitLibraryMath.SkinAgrees(game, Hex(paint!)),
                $"{gene} ({hex}, tone {tone}): the palette's {word} ({paint}) in the same tone or the next, so its pictures stand for it");
            PortraitLibraryMath.Lab(Hex(paint ?? hex), out double l, out _, out _);
            C(Math.Abs(PortraitLibraryMath.SkinToneOf(l - PortraitLibraryMath.SkinSlack) - tone) <= 1 && Math.Abs(PortraitLibraryMath.SkinToneOf(l + PortraitLibraryMath.SkinSlack) - tone) <= 1,
                $"{gene}: a painted {word} a little lighter or darker (the slack, {PortraitLibraryMath.SkinSlack}) still in its tone or the next");
            var far = palette.Where(x => Math.Abs(PortraitLibraryMath.SkinTone(Hex(x.hex)) - tone) >= 2).Select(x => x.word).ToList();
            C(far.Count >= 4 && far.All(w => !PortraitLibraryMath.SkinAgrees(game, Hex(wordHex[w])) && !PortraitLibraryMath.SkinAgrees(Hex(wordHex[w]), game)),
                $"{gene}: never a skin two tones or more away ({string.Join(", ", far)})");
            foreach (var (other, oh, _) in Unusual.Where(u => u.gene != "Skin_SheerWhite"))
                if (PortraitLibraryMath.SkinAgrees(game, Hex(oh)) || PortraitLibraryMath.SkinAgrees(Hex(oh), game))
                    C(false, $"{gene} and {other} agree");
        }
        C(Melanin.All(m => Unusual.All(u => !PortraitLibraryMath.SkinAgrees(Hex(m.hex), Hex(u.hex)) && !PortraitLibraryMath.SkinAgrees(Hex(u.hex), Hex(m.hex)))),
            "a human skin never gets a skin no human grows (green, blue, red, orange, yellow, purple, grey, ink black, sheer white), nor those a human one");
        C(!PortraitLibraryMath.SkinAgrees(Hex("#f2ede0"), Hex("#d9aa86")) && !PortraitLibraryMath.SkinAgrees(Hex("#402917"), Hex("#7d5236")) && PortraitLibraryMath.SkinAgrees(Hex("#402917"), Hex("#4e3322")),
            "the game's fairest skin never a light one, its darkest never a brown one, but the palette's dark");

        // ---------------- A skin no human grows ----------------
        foreach (var (gene, hex, word) in Unusual)
        {
            var game = Hex(hex);
            C(PortraitLibraryMath.UnusualSkin(game) && PortraitFolderMath.SkinWord(game) == word, $"{gene} ({hex}): no human's, the missing list says {word}");
            var agree = wordHex.Where(kv => PortraitLibraryMath.SkinAgrees(game, Hex(kv.Value))).Select(kv => kv.Key).ToList();
            C(agree.SequenceEqual(new[] { word }), $"{gene}: of the colours the planner paints, only its own word's ({string.Join(", ", agree)})");
            var others = Unusual.Where(u => u.gene != gene && PortraitLibraryMath.SkinAgrees(game, Hex(u.hex))).Select(u => u.gene).ToList();
            C(word == "yellow" ? others.SequenceEqual(Unusual.Where(u => u.word == "yellow" && u.gene != gene).Select(u => u.gene)) : others.Count == 0,
                $"{gene}: no other of the game's skins ({string.Join(", ", others)}){(word == "yellow" ? " but the other yellow" : "")}");
        }
        C(!PortraitLibraryMath.SkinAgrees(Hex("#73a659"), Hex("#e0a0b8")) && !PortraitLibraryMath.SkinAgrees(Hex("#e0a0b8"), Hex("#73a659"))
          && !PortraitLibraryMath.SkinAgrees(Hex("#a8cf9f"), Hex("#e8b4c4")), "a green skin never a pink one (the game's green, the Avaloi's soft green and soft pink)");
        C(!PortraitLibraryMath.SkinAgrees(Hex("#b3b3b3"), Hex("#e0a0b8")) && !PortraitLibraryMath.SkinAgrees(Hex("#737880"), Hex("#5d7fa8")), "a grey never a pink, nor a slate grey a blue");
        C(PortraitLibraryMath.SkinAgrees(Hex("#ebedfa"), Hex("#efefef")) && PortraitLibraryMath.SkinAgrees(Hex("#73a659"), Hex("#6c8f4a")) && PortraitLibraryMath.SkinAgrees(Hex("#a61f1a"), Hex("#a3302a")),
            "nearly the same colour agrees: the game's sheer white and a painted white, its green and the korgs', its deep red and the impids'");
        C(Math.Abs(PortraitLibraryMath.AngleApart(350, 10) - 20) < 1e-9 && Math.Abs(PortraitLibraryMath.AngleApart(10, 190) - 180) < 1e-9, "hues apart the short way round");
        {
            var greenMan = Look(PortraitAge.Adult, "#73a659", "orc");
            var pink = Rec("pink", PortraitAge.Adult, "#e0a0b8", "orc");
            var green = Rec("green", PortraitAge.Adult, "#6f9a5b", "orc");
            C(!PortraitLibraryMath.Pick(greenMan, new List<PortraitRecord> { pink }, 1, _ => 0).Any, "a green-skinned person of a kind with only pink pictures: no picture");
            var pg = PortraitLibraryMath.Pick(greenMan, new List<PortraitRecord> { pink, green }, 1, _ => 0);
            C(pg.Any && pg.index == 1, "with a green picture of their kind, that one");
            var fair = Look(PortraitAge.Adult, "#f2ede0");
            C(!PortraitLibraryMath.Pick(fair, new List<PortraitRecord> { Rec("dark", PortraitAge.Adult, "#4e3322") }, 1, _ => 0).Any
              && PortraitLibraryMath.Pick(fair, new List<PortraitRecord> { Rec("dark", PortraitAge.Adult, "#4e3322"), Rec("fair", PortraitAge.Adult, "#ecc9ae") }, 1, _ => 0).index == 1,
                "a fair person with only a dark picture: none; with a fair one, that");
            var noSkin = Rec("noskin", PortraitAge.Adult);
            noSkin.skinSet = false;
            C(PortraitLibraryMath.SkinFits(noSkin, fair), "a record with no skin colour (a def error) isn't held to the skin");
            var fairDrinker = Look(PortraitAge.Adult, "#f2ede0");
            fairDrinker.AddLook("drinker", false);
            var darkDrinker = Rec("darkDrinker", PortraitAge.Adult, "#4e3322");
            darkDrinker.looks.Add("drinker");
            C(PortraitLibraryMath.LooksLacking(fairDrinker, new List<PortraitRecord> { darkDrinker }).SequenceEqual(new[] { "drinker" }), "a look shown only on a picture of another skin is still missing for them");
        }
        C(PortraitFolderMath.SkinWord(Hex("#0d0d0f")) == "black" && PortraitFolderMath.SkinWord(Hex("#1c1c20")) == "black" && PortraitFolderMath.SkinWord(Hex("#668cd9")) == "blue",
            "nearly black is black whatever its tint, so the missing list asks for an ink-black skin, not a blue one");
        C(Art("catalogue_words.py").Contains("    if v < 0.15:\n        return 'black'\n    if s < 0.12:\n        return 'white' if v > 0.85 else 'grey'"),
            "the planner's words the same, line for line");

        // ---------------- The hair's colour, a strong preference ----------------
        {
            PortraitLook Hair(string hair, PortraitAge age = PortraitAge.Adult)
            {
                var l = Look(age);
                l.hairColour = Hex(hair);
                l.hair = PortraitHairStyle.Short;
                l.era = PortraitEra.Tribal;
                l.dress = PortraitDress.Work;
                return l;
            }
            PortraitRecord Pic(string name, string hair, PortraitHairStyle style = PortraitHairStyle.Short, PortraitBeard beard = PortraitBeard.None, PortraitBuild build = PortraitBuild.Average,
                PortraitEra era = PortraitEra.Tribal, PortraitDress dress = PortraitDress.Work, PortraitAge age = PortraitAge.Adult)
            {
                var r = Rec(name, age);
                r.hairColour = Hex(hair);
                r.hair = style;
                r.beard = beard;
                r.build = build;
                r.era = era;
                r.dress = dress;
                return r;
            }
            float most = 0f;
            foreach (PortraitHairStyle a in Enum.GetValues(typeof(PortraitHairStyle)))
            foreach (PortraitHairStyle b in Enum.GetValues(typeof(PortraitHairStyle)))
                most = Math.Max(most, PortraitLibraryMath.HairApartOf(a, b));
            float beards = 0f, builds = 0f, dresses = 0f;
            foreach (PortraitBeard a in Enum.GetValues(typeof(PortraitBeard)))
            foreach (PortraitBeard b in Enum.GetValues(typeof(PortraitBeard)))
                beards = Math.Max(beards, PortraitLibraryMath.BeardApartOf(a, b));
            foreach (PortraitBuild a in Enum.GetValues(typeof(PortraitBuild)))
            foreach (PortraitBuild b in Enum.GetValues(typeof(PortraitBuild)))
                builds = Math.Max(builds, PortraitLibraryMath.BuildApartOf(a, b));
            foreach (PortraitDress a in Enum.GetValues(typeof(PortraitDress)))
            foreach (PortraitDress b in Enum.GetValues(typeof(PortraitDress)))
                dresses = Math.Max(dresses, PortraitLibraryMath.DressApartOf(a, b));
            float rest = PortraitLibraryMath.HairWeight * most + PortraitLibraryMath.BeardWeight * beards + PortraitLibraryMath.BuildWeight * builds + PortraitLibraryMath.DressWeight * dresses
                         + PortraitLibraryMath.EraWeight * ((int)PortraitEra.Spacer - (int)PortraitEra.Tribal);
            C(PortraitLibraryMath.HairColourMost > rest && Math.Abs(rest - 11.25f) < 1e-4, $"a clearly different hair colour costs more than a picture can on its hair, beard, build and clothes together ({rest})");
            // The game's blond hair against the palette's blond and brown.
            var blonde = Hair("#e8cc90");
            var brownAlike = Pic("brownAlike", "#5a3d27");
            var blondUnlike = Pic("blondUnlike", "#cfae74", PortraitHairStyle.Long, PortraitBeard.Full, PortraitBuild.Heavy, PortraitEra.Spacer, PortraitDress.Fine);
            var lib = new List<PortraitRecord> { brownAlike, blondUnlike };
            var p = PortraitLibraryMath.Pick(blonde, lib, 1, _ => 0);
            C(p.Any && lib[p.index] == blondUnlike && PortraitLibraryMath.Distance(blonde, blondUnlike) < PortraitLibraryMath.Distance(blonde, brownAlike),
                "a blonde person gets a blonde picture over a brown one that matches their hair, beard, build and clothes better");
            var only = PortraitLibraryMath.Pick(blonde, new List<PortraitRecord> { brownAlike }, 1, _ => 0);
            C(only.Any && only.index == 0, "with only a brown one fitting, the brown one is still theirs (never a hard rule)");
            C(PortraitLibraryMath.HairColourCost(Hex("#e8cc90"), Hex("#cfae74")) < 0.5f, "the game's blond and the palette's blond (11 apart) cost next to nothing");
            float near = PortraitLibraryMath.HairColourCost(Hex("#3b2a20"), Hex("#1c1a19"));
            C(near > 0f && near < PortraitLibraryMath.HairApartOf(PortraitHairStyle.Short, PortraitHairStyle.Long) && PortraitLibraryMath.HairColourCost(Hex("#3b2a20"), Hex("#cfae74")) == PortraitLibraryMath.HairColourMost,
                $"a dark-brown person prefers dark brown over black only mildly ({near:0.00}, less than a hair's length), over blond fully");
            var darkBrown = Hair("#3b2a20");
            var dbLong = Pic("dbLong", "#3b2a20", PortraitHairStyle.Long);
            var blackShort = Pic("blackShort", "#1c1a19");
            var dbShort = Pic("dbShort", "#3b2a20");
            var hairs = new List<PortraitRecord> { blackShort, dbLong, dbShort };
            C(PortraitLibraryMath.Pick(darkBrown, hairs, 1, _ => 0).index == 2 && PortraitLibraryMath.Distance(darkBrown, blackShort) < PortraitLibraryMath.Distance(darkBrown, dbLong),
                "neighbouring colours stay close: their own shade first, but black with their hair's length before dark brown with another");
            // Grey for the old: their grey hair is their colour.
            var greyOld = Hair("#a6a6a6", PortraitAge.Old);
            var greys = new List<PortraitRecord> { Pic("brownOld", "#5a3d27", age: PortraitAge.Old), Pic("greyOld", "#8e8c88", PortraitHairStyle.Bun, age: PortraitAge.Old) };
            C(PortraitLibraryMath.Pick(greyOld, greys, 1, _ => 0).index == 1, "an old person's grey hair counts as their colour: a grey-haired picture first");
            // A face nobody else keeps still comes first; the hair's colour orders the pictures within that.
            var kept = PortraitLibraryMath.Pick(blonde, lib, 1, c => c == "blondUnlike" ? 1 : 0);
            C(kept.Any && lib[kept.index] == brownAlike, "a face nobody else keeps first: the blonde face kept by another, the free brown one");
            var bald = Hair("#e8cc90");
            bald.hair = PortraitHairStyle.Bald;
            var baldPic = Pic("bald", "#5a3d27", PortraitHairStyle.Bald);
            C(Math.Abs(PortraitLibraryMath.Distance(bald, baldPic) - 0.5f * PortraitLibraryMath.HairColourMost) < 1e-4, "bald on both: the colour hardly shows, half its cost (as before)");
        }

        // ---------------- A bionic arm ----------------
        {
            C(PortraitLibraryMath.Marks.Contains("artificialArm"), "artificialArm is a mark");
            var problems = new List<string>();
            var fields = new Dictionary<string, string> { ["texPath"] = "x", ["skinColor"] = "#c9a27e", ["hairColor"] = "#2a2a2a" };
            var r = PortraitLibraryMath.Record("Arm", k => fields.TryGetValue(k, out var v) ? v : null, k => k == "marks" ? new[] { "artificialArm" } : null, problems);
            C(problems.Count == 0 && r.marks.SetEquals(new[] { "artificialArm" }), "a record's mark artificialArm read without a problem");
            var problems2 = new List<string>();
            PortraitLibraryMath.Record("Lost", k => fields.TryGetValue(k, out var v) ? v : null, k => k == "marks" ? new[] { "missingArm" } : null, problems2);
            C(problems2.Count == 1 && problems2[0].Contains("artificialArm"), "an unknown mark still a def error, naming artificialArm among the marks");
            var armPic = Rec("armPic", PortraitAge.Adult, marks: new[] { "artificialArm" });
            var plain = Rec("plain", PortraitAge.Adult);
            var twoArms = Look(PortraitAge.Adult);
            var withArm = Look(PortraitAge.Adult, marks: new[] { "artificialArm" });
            C(!PortraitLibraryMath.Shows(armPic, twoArms) && PortraitLibraryMath.Shows(armPic, withArm) && PortraitLibraryMath.Shows(plain, withArm),
                "a picture with a bionic arm only for someone with one; someone with one may take a plain picture");
            C(Math.Abs(PortraitLibraryMath.Distance(withArm, plain) - PortraitLibraryMath.Distance(withArm, armPic) - PortraitLibraryMath.MarkCost) < 1e-4,
                "someone with a bionic arm prefers a picture that shows it (a plain one costs the mark's 0.5)");
            var both = new List<PortraitRecord> { plain, armPic };
            C(PortraitLibraryMath.Pick(withArm, both, 1, _ => 0).index == 1 && PortraitLibraryMath.Pick(twoArms, both, 1, _ => 0).index == 0
              && PortraitLibraryMath.Pick(withArm, new List<PortraitRecord> { plain }, 1, _ => 0).index == 0,
                "the matcher: the arm's picture for them, the plain one for anyone else, the plain one for them where there is no other");
            List<string> T(params string[] s) => PortraitLibraryMath.Tokens(s);
            C(PortraitLibraryMath.ArmPart(T("Shoulder", "shoulder"), new[] { "ManipulationLimbCore" }) && PortraitLibraryMath.ArmPart(T("Arm", "arm"), null)
              && PortraitLibraryMath.ArmPart(T("Hand", "hand"), new[] { "ManipulationLimbSegment" }) && PortraitLibraryMath.ArmPart(T("Finger", "finger"), new[] { "ManipulationLimbDigit" })
              && PortraitLibraryMath.ArmPart(T("RF_LeftForearm", "left forearm"), null),
                "the game's shoulder, arm, hand and fingers are arm parts, by their names or tags, and a mod's forearm by its words");
            C(PortraitLibraryMath.ArmPart(T("Tentacle", "tentacle"), new[] { "ManipulationLimbCore" }) && !PortraitLibraryMath.ArmPart(T("Leg", "leg"), new[] { "MovingLimbCore" })
              && !PortraitLibraryMath.ArmPart(T("Foot", "foot"), new[] { "MovingLimbSegment" }) && !PortraitLibraryMath.ArmPart(T("Head", "head"), null)
              && !PortraitLibraryMath.ArmPart(T("Torso", "torso"), new[] { "BloodPumpingSource" }) && !PortraitLibraryMath.ArmPart(T("ArmorPlate", "armor plate"), null),
                "a mod's limb that handles things by its tag; never a leg, a foot, the head, the torso, nor armour by its name");
            string reader = Read("UI/Faces/PortraitReader.cs");
            C(reader.Contains("h is Hediff_AddedPart || h.def?.addedPartProps != null") && reader.Contains("l.AddMark(\"artificialArm\", SideOf(part))")
              && reader.Contains("IsArm(part.def) || part.parent?.def != null && IsArm(part.parent.def)") && reader.Contains("armParts[d] = arm;"),
                "the game reads it from any added part (a bionic, archotech or prosthetic one, a mod's) on an arm part or one whose parent is, each part's def read once");
            C(Art("test_portrait_catalogue.py").Contains("'missingEar', 'artificialArm'}"), "the catalogue's tests take the mark in a row's marks");
        }

        // ---------------- training.csv ----------------
        {
            var t = new TrainingPicture { file = "Avila_style_01_cut_00001_.png", batch = "colonists", record = "Portrait_colonists_Avila_01", date = "2026-11-23 18:04" };
            string fresh = PortraitTrainingMath.Append(null, t);
            C(fresh == "file,batch,record,date\r\nAvila_style_01_cut_00001_.png,colonists,Portrait_colonists_Avila_01,2026-11-23 18:04\r\n", "a new list: its columns, then the line");
            var odd = new TrainingPicture { file = "Y, \"odd\".png", batch = "b" };
            string two = PortraitTrainingMath.Append(fresh, odd);
            C(two.StartsWith(fresh) && two.EndsWith("\"Y, \"\"odd\"\".png\",b,,\r\n"), "a line added at the end, everything before kept, a cell with a comma or quotes in quotes");
            var read = PortraitTrainingMath.Read(two);
            C(read.Count == 2 && read[0].file == t.file && read[0].record == t.record && read[1].file == "Y, \"odd\".png" && read[1].record == "", "read back as written");
            string excel = "﻿file;batch;record;date\r\nA.png;b;P_A;2026-11-20 10:00";
            string more = PortraitTrainingMath.Append(excel, t);
            C(more.EndsWith("\r\nAvila_style_01_cut_00001_.png;colonists;Portrait_colonists_Avila_01;2026-11-23 18:04\r\n") && PortraitTrainingMath.Read(more).Count == 2,
                "a list Excel saved with semicolons: the line with semicolons, after the last line");
            C(PortraitTrainingMath.Listed(read, "Portrait_colonists_Avila_01", "other", "other.png") && !PortraitTrainingMath.Listed(read, "P_Z", "colonists", "Avila_style_01_cut_00001_.png")
              && PortraitTrainingMath.Listed(PortraitTrainingMath.Read("file,batch\nX.png,Colonists\n"), "P_X", "colonists", "x.png"),
                "a picture already listed: by its record, else (a line typed by hand) by its batch and file name, the letters' case aside");
            string libDir = Path.Combine(Path.GetTempPath(), "x", "Rimfolk-portraits", "library");
            C(PortraitTrainingMath.Root(libDir) == Path.Combine(Path.GetTempPath(), "x", "Rimfolk-portraits", "training") && PortraitTrainingMath.Root(libDir + Path.DirectorySeparatorChar) == PortraitTrainingMath.Root(libDir)
              && PortraitTrainingMath.Root("") == null && PortraitTrainingMath.Root(Path.DirectorySeparatorChar.ToString()) == null,
                "the training folder beside the library folder, never inside it; none for no folder or a drive's root");
        }

        // ---------------- The copy, on a library of its own ----------------
        string dir = Path.Combine(Path.GetTempPath(), "rimfolk-training-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            string lib = Path.Combine(dir, "Rimfolk-portraits", "library");
            string root2 = PortraitTrainingMath.Root(lib);
            string Lib(string rel) => Path.Combine(lib, rel.Replace('/', Path.DirectorySeparatorChar));
            string Tr(string rel) => Path.Combine(root2, rel.Replace('/', Path.DirectorySeparatorChar));
            void Make(string path, string content)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, content);
            }
            TrainingJob Job(string tex, string record) => new TrainingJob
            {
                folder = lib, root = root2, texPath = tex, tries = 2,
                line = new TrainingPicture { batch = PortraitDeleteMath.BatchOf(tex), record = record, date = "2026-11-23 18:04" }
            };
            Make(Lib("colonists/X.png"), "picture colonists/X.png");
            Make(Lib("colonists/Y.png"), "picture colonists/Y.png");
            var waits = new List<int>();
            var r = PortraitTrainingMath.Run(Job("colonists/X", "P_X"), waits.Add);
            C(r.done && !r.already && r.copiedAs == "X.png" && File.ReadAllText(Tr("colonists/X.png")) == "picture colonists/X.png" && File.ReadAllText(Lib("colonists/X.png")) == "picture colonists/X.png",
                "the picture copied as it is into training\\colonists beside the library folder, made where needed; the library's own kept");
            var listed = PortraitTrainingMath.Read(File.ReadAllText(Tr("training.csv")));
            C(listed.Count == 1 && listed[0].file == "X.png" && listed[0].batch == "colonists" && listed[0].record == "P_X" && listed[0].date == "2026-11-23 18:04",
                "its line in training.csv: file, batch, record, date");
            C(File.ReadAllBytes(Tr("training.csv")).Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }) && !Directory.GetFiles(dir, "*.tmp", SearchOption.AllDirectories).Any() && waits.Count == 0,
                "training.csv marked as UTF-8 for Excel; no temporary file left, nothing waited for");
            r = PortraitTrainingMath.Run(Job("colonists/X", "P_X"), waits.Add);
            C(r.already && !r.done && PortraitTrainingMath.Read(File.ReadAllText(Tr("training.csv"))).Count == 1, "kept a second time: already listed, nothing copied or listed again");
            // Another picture of the same name in the training folder: kept, the copy named " (2)".
            Make(Tr("colonists/Y.png"), "another picture once named Y");
            r = PortraitTrainingMath.Run(Job("colonists/Y", "P_Y"), waits.Add);
            C(r.done && r.copiedAs == "Y (2).png" && File.ReadAllText(Tr("colonists/Y.png")) == "another picture once named Y" && File.ReadAllText(Tr("colonists/Y (2).png")) == "picture colonists/Y.png"
              && PortraitTrainingMath.Read(File.ReadAllText(Tr("training.csv")))[1].file == "Y (2).png",
                "a different file of the same name there: kept, the copy named \" (2)\" and listed so");
            // The same file there but not listed (copied by hand): listed, not copied again.
            Make(Lib("colonists/Z.png"), "picture colonists/Z.png");
            Make(Tr("colonists/Z.png"), "picture colonists/Z.png");
            r = PortraitTrainingMath.Run(Job("colonists/Z", "P_Z"), waits.Add);
            C(r.done && r.copiedAs == "Z.png" && !File.Exists(Tr("colonists/Z (2).png")) && PortraitTrainingMath.Read(File.ReadAllText(Tr("training.csv"))).Count == 3,
                "the same file already there: listed, not copied twice");
            // A picture with no batch folder.
            Make(Lib("Loose.png"), "picture Loose.png");
            r = PortraitTrainingMath.Run(Job("Loose", "P_Loose"), waits.Add);
            C(r.done && File.Exists(Tr("Loose.png")), "a picture in no batch folder: into the training folder itself");
            // Nothing changed where a file can't be read or written.
            r = PortraitTrainingMath.Run(Job("colonists/Gone", "P_Gone"), waits.Add);
            C(!r.done && r.stop == TrainingStop.NoFile, "a picture no longer in the library folder: nothing done");
            Make(Lib("colonists/W.png"), "picture colonists/W.png");
            string before = File.ReadAllText(Tr("training.csv"));
            Directory.CreateDirectory(Tr("training.csv.tmp"));
            waits.Clear();
            r = PortraitTrainingMath.Run(Job("colonists/W", "P_W"), waits.Add);
            C(!r.done && r.stop == TrainingStop.WriteList && !File.Exists(Tr("colonists/W.png")) && File.ReadAllText(Tr("training.csv")) == before && waits.Count > 0,
                "training.csv can't be written: tried again, then the copy taken away again, the list as it was");
            Directory.Delete(Tr("training.csv.tmp"));
            Directory.CreateDirectory(Tr("colonists/W.png.tmp"));
            r = PortraitTrainingMath.Run(Job("colonists/W", "P_W"), waits.Add);
            C(!r.done && r.stop == TrainingStop.Copy && File.ReadAllText(Tr("training.csv")) == before, "the picture can't be copied: nothing listed");
        }
        finally
        {
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            catch (Exception)
            {
                // A temporary folder left behind.
            }
        }

        // ---------------- The words and the seams ----------------
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Portraits.xml"));
        string train = Read("UI/Faces/PortraitTraining.cs"), inspect = Read("UI/Faces/InspectPortrait.cs");
        C(keyed.Contains("<CC_Portrait_Train>Keep for training<"), "the menu's entry by the name the proposal gave it");
        var used = Regex.Matches(train, "\"(CC_[A-Za-z_]+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        var missing = used.Where(k => !keyed.Contains("<" + k + ">")).ToList();
        C(used.Count >= 12 && missing.Count == 0, $"every word keeping a picture uses is in the language file ({string.Join(", ", missing)})");
        var words = Regex.Matches(keyed, @"<(CC_Portrait_Train\w*)>([^<]*)<").Select(m => m.Groups[2].Value).ToList();
        C(words.Count >= 12 && words.All(w => !w.Contains("—") && !w.Contains("!")), "plain words: no dash of that kind, no exclamation marks");
        C(keyed.Contains("of their own kind, sex, age band and skin tone") && keyed.Contains("<CC_PortraitNeed_NoStage>none of their sex, age and skin tone<") && !keyed.Contains("a near age"),
            "the settings and the missing list say the same, no near age left");
        int trainAt = inspect.IndexOf("PortraitTraining.AddOption(p, options);", StringComparison.Ordinal), deleteAt = inspect.IndexOf("PortraitDelete.AddOptions(p, options);", StringComparison.Ordinal);
        C(trainAt > 0 && deleteAt > trainAt, "on the large portrait's right-click menu, before the entries to delete");
        C(train.Contains("PortraitFolder.PersonNamed(name) == null ? \"CC_Portrait_DeleteFromMod\"") && train.Contains("PortraitTrainingMath.Root(PortraitFolder.Folder)")
          && train.Contains("\"CC_Portrait_TrainNoPicture\"") && train.Contains("\"CC_Portrait_TrainKept\""),
            "only a library folder's picture of a person, greyed for a mod's, none shown, one kept, no folder above the library");
        C(train.Contains("PortraitStore.Run(() => PortraitTrainingMath.Run(job, ms => Thread.Sleep(ms))") && !train.Contains("Scribe"), "copied on a worker thread, never holding the game up; nothing saved");
        return n;
    }
}
