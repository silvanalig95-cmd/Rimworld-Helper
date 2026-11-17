using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// A land whose wild animals the game can't read (the player's startup log, 7 November 2026: ReGrowth's RG_Dodo and
/// RG_Thrinaxodon in one land's list twice, so <c>BiomeDef.CommonalityOfAnimal</c> threw for Giddy-Up). The merge
/// (Animals/BiomeAnimalsMath.cs), the log's line, and that every reading in Rimfolk goes through Animals/BiomeAnimals.cs.
/// </summary>
public static class BiomeAnimalsTests
{
    private static KeyValuePair<string, float> R(string animal, float commonality) => new KeyValuePair<string, float>(animal, commonality);

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "a land's wild animals: " + what);
        }

        // ---------- The merge ----------
        var repeated = new List<string>();
        var plain = BiomeAnimalsMath.Merge(new[] { R("Deer", 0.5f), R("Hare", 1f), R("Muffalo", 0.2f) }, repeated, out int unnamed);
        C(plain.Count == 3 && plain["Deer"] == 0.5f && plain["Hare"] == 1f && plain["Muffalo"] == 0.2f, "a list without repeats is read as it is");
        C(repeated.Count == 0 && unnamed == 0, "nothing to say about a list without repeats");

        repeated.Clear();
        var twice = BiomeAnimalsMath.Merge(new[] { R("Deer", 0.5f), R("RG_Dodo", 0.04f), R("Hare", 1f), R("RG_Dodo", 0.06f), R("RG_Thrinaxodon", 0.1f), R("RG_Thrinaxodon", 0.1f) },
            repeated, out unnamed);
        C(twice.Count == 4, "each animal once");
        C(Math.Abs(twice["RG_Dodo"] - 0.05f) < 1e-6f && Math.Abs(twice["RG_Thrinaxodon"] - 0.1f) < 1e-6f, "a repeated animal gets the mean of its records");
        C(twice["Deer"] == 0.5f && twice["Hare"] == 1f, "the others keep their own");
        C(repeated.SequenceEqual(new[] { "RG_Dodo", "RG_Thrinaxodon" }), "the repeated animals, each once, in the order their second record comes");

        repeated.Clear();
        var thrice = BiomeAnimalsMath.Merge(new[] { R("Boar", 0.3f), R("Boar", 0.6f), R("Boar", 0.9f) }, repeated, out _);
        C(Math.Abs(thrice["Boar"] - 0.6f) < 1e-6f && repeated.Count == 1, "three records of one animal: their mean, and the animal named once");

        repeated.Clear();
        var gone = BiomeAnimalsMath.Merge(new[] { R(null, 0.2f), R("Deer", 0.5f), R(null, 0.1f) }, repeated, out unnamed);
        C(gone.Count == 1 && unnamed == 2 && repeated.Count == 0, "records naming no animal (its def missing) are left out and counted");

        var zero = BiomeAnimalsMath.Merge(new[] { R("Ghost", 0f), R("Ghost", 0.4f) }, null, out _);
        C(zero["Ghost"] > 0f, "an animal one mod lists at nothing and another at some lives there (the mean is above nothing)");
        C(BiomeAnimalsMath.Merge<string>(null, null, out unnamed).Count == 0 && unnamed == 0, "no list, no animals");
        C(BiomeAnimalsMath.Merge(Array.Empty<KeyValuePair<string, float>>(), null, out _).Count == 0, "an empty list, no animals");

        // ---------- The log's line ----------
        C(BiomeAnimalsMath.Names(new[] { "RG_Dodo", "RG_Thrinaxodon", "RG_Dodo" }) == "RG_Dodo, RG_Thrinaxodon", "the animals named, each once");
        C(BiomeAnimalsMath.Names(Enumerable.Range(1, 9).Select(i => "A" + i)) == "A1, A2, A3, A4, A5, A6 and 3 more", "at most six named, then how many more");
        C(BiomeAnimalsMath.Names(null) == "", "no names, nothing");
        C(BiomeAnimalsMath.Problem(Array.Empty<string>(), 0, 0) == null, "nothing wrong, nothing logged");
        string both = BiomeAnimalsMath.Problem(new[] { "RG_Dodo" }, 1, 0);
        C(both.Contains("RG_Dodo listed more than once") && both.Contains("a record naming an animal whose def is missing"), "the repeat and the missing def both named");
        C(BiomeAnimalsMath.Problem(Array.Empty<string>(), 3, 2).Contains("3 records") && BiomeAnimalsMath.Problem(Array.Empty<string>(), 0, 2).Contains("2 pawn kinds without a race"),
            "counts in words");

        // ---------- The mend at load (the player's log, 17 November 2026) ----------
        var rep = new List<string>();
        var kept = BiomeAnimalsMath.KeepOnce(new[] { R("Deer", 0.5f), R(null, 0.3f), R("RG_Dodo", 0.04f), R("Hare", 1f), R("RG_Dodo", 0.06f) }, rep, out int missing);
        C(kept.Select(k => k.Key).SequenceEqual(new[] { 0, 2, 3 }), "one list: each animal kept at its first record, in the list's order");
        C(Math.Abs(kept[1].Value - 0.05f) < 1e-6f && kept[0].Value == 0.5f && kept[2].Value == 1f, "one list: a repeat at the mean, the others as they were");
        C(rep.SequenceEqual(new[] { "RG_Dodo" }) && missing == 1, "one list: the repeat named, the record without an animal left out and counted");
        C(BiomeAnimalsMath.KeepOnce<string>(null, null, out missing).Count == 0 && missing == 0, "one list: no list, nothing kept");

        // The land's own list and the races naming it. Kinds and races are named alike here, as ReGrowth's are.
        Func<string, string> raceOf = k => k == "NoRace" ? null : k.Split('/')[0];
        int KindsOf(string race) => race == "Wolf" ? 2 : 1;
        var land = BiomeAnimalsMath.MendPlain(new[] { R("Deer", 0.5f), R("RG_Dodo", 0.04f), R("Hare", 1f), R("Hare", 0.6f) },
            new[] { new KeyValuePair<string, float>("RG_Dodo", 0.06f), new KeyValuePair<string, float>("Boar", 0.3f) }, raceOf, KindsOf);
        C(land.own.Select(k => k.Key).SequenceEqual(new[] { 0, 2 }), "the land's list loses the animal its race names there too, and keeps one record of a repeat");
        C(Math.Abs(land.own[1].Value - 0.8f) < 1e-6f, "a repeat in the land's list at the mean");
        C(Math.Abs(land.races["RG_Dodo"] - 0.05f) < 1e-6f && land.races["Boar"] == 0.3f, "the race's record takes the mean of the land's and its own; another race keeps its own");
        C(land.repeatedKinds.SequenceEqual(new[] { "RG_Dodo", "Hare" }) && land.repeatedRaces.Count == 0 && land.unnamed == 0, "the repeats named in the land's order");

        var wolves = BiomeAnimalsMath.MendPlain(new[] { R("Wolf/Timber", 0.2f) }, new[] { new KeyValuePair<string, float>("Wolf", 0.1f) }, raceOf, KindsOf);
        C(wolves.own.Count == 0 && wolves.races["Wolf"] == 0.1f, "a race with more kinds keeps its own number, so its other kinds don't change");
        var twiceRace = BiomeAnimalsMath.MendPlain(new[] { R(null, 0.2f) },
            new[] { new KeyValuePair<string, float>("Bear", 0.2f), new KeyValuePair<string, float>("Bear", 0.4f) }, raceOf, KindsOf);
        C(Math.Abs(twiceRace.races["Bear"] - 0.3f) < 1e-6f && twiceRace.repeatedRaces.SequenceEqual(new[] { "Bear" }) && twiceRace.unnamed == 1,
            "a race naming the land twice keeps one record at the mean; a record without an animal is counted");
        var clean = BiomeAnimalsMath.MendPlain(new[] { R("Deer", 0.5f), R("NoRace", 0.1f) }, new[] { new KeyValuePair<string, float>("Boar", 0.3f) }, raceOf, KindsOf);
        C(clean.own.Count == 2 && clean.own[0].Value == 0.5f && clean.races["Boar"] == 0.3f && clean.repeatedKinds.Count == 0, "a land without repeats is left as it is");

        string line = BiomeAnimalsMath.MendLine(new[] { "RG_Dodo", "RG_Thrinaxodon", "RG_Dodo" }, new[] { "TemperateForest", "AridShrubland" }, 0);
        C(line == "Wild animals of TemperateForest, AridShrubland: RG_Dodo, RG_Thrinaxodon listed more than once (two mods adding the same animal). Rimfolk kept each one once, at the mean of its listings, so the game can read these lands' wild animals in full.",
            "the log's line names the animals and the lands");
        C(BiomeAnimalsMath.MendLine(Array.Empty<string>(), new[] { "Desert" }, 2).Contains("2 records naming animals whose defs are missing. Rimfolk left them out"), "missing defs said in words");
        C(BiomeAnimalsMath.MendLine(Array.Empty<string>(), Array.Empty<string>(), 0) == null && BiomeAnimalsMath.MendLine(Array.Empty<string>(), new[] { "Desert" }, 0) == null,
            "nothing mended, nothing logged");
        C(!line.Contains("—") && !line.Contains("!"), "plain line");

        string mend = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Animals", "BiomeAnimalsMend.cs"));
        string modCtor = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "ColonyCouncilMod.cs"));
        C(modCtor.Contains("LongEventHandler.ExecuteWhenFinished(BiomeAnimalsMend.Run);"), "the mend is queued from the mod's constructor, before every mod's startup class");
        C(mend.Contains("if (done) return;") && mend.Contains("catch (Exception e)"), "the mend runs once and never throws into the load");
        C(mend.Contains("\"cachedAnimalCommonalities\"") && mend.Contains("\"cachedPollutionAnimalCommonalities\"") && mend.Contains("\"cachedCoastalAnimalCommonalities\"")
          && mend.Contains("f.SetValue(biome, null)"), "a mended land's cached dictionaries are cleared, so the game builds them again");
        C(mend.Contains("ModsConfig.BiotechActive") && mend.Contains("ModsConfig.OdysseyActive"), "the pollution and coastal lists mended only with their expansion");
        C(mend.Contains("Log.Message(\"[Rimfolk] \" + line)") && !mend.Contains("Log.Warning") && !mend.Contains("Log.Error("), "one plain message, no warning or error");

        // ---------- The helper ----------
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string helper = File.ReadAllText(Path.Combine(src, "Animals", "BiomeAnimals.cs"));
        C(helper.Contains("return biome.AllWildAnimals.ToList();") && helper.Contains("return biome.CommonalityOfAnimal(kind);"),
            "where nothing is wrong, the game's own answers (the list read whole inside the try, so a throw can't escape)");
        C(helper.Contains("if (!land.listOurs)") && helper.Contains("if (!land.plainOurs)"), "the game is asked only where its reading works");
        C(helper.Contains("ModsConfig.BiotechActive") && helper.Contains("ModsConfig.OdysseyActive") && helper.Contains("\"pollutionWildAnimals\"") && helper.Contains("\"coastalWildAnimals\""),
            "the pollution and coastal lists read as the game reads them, only with their expansion");
        C(helper.Contains("k.race?.race") && helper.Contains("w.biome == biome"), "the lands each race names are read too");
        C(helper.Contains("BiomeAnimalsMend.Run();"), "the lists are mended before they are first read here");
        C(helper.Contains("CouncilLog.WarnOnce(\"the game can't read the wild animals of \" + biome.defName"), "said once in the log, naming the land");
        C(helper.Contains("DefDatabase<PawnKindDef>.AllDefsListForReading"), "the list in the game's order");

        // ---------- Every reading in Rimfolk goes through it ----------
        var direct = new Regex(@"\.AllWildAnimals\b|\.CommonalityOfAnimal\(");
        int users = 0;
        foreach (var f in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(src, f).Replace('\\', '/');
            if (rel.StartsWith("obj/", StringComparison.Ordinal) || rel.StartsWith("bin/", StringComparison.Ordinal) || rel == "Animals/BiomeAnimals.cs") continue;
            string code = Regex.Replace(File.ReadAllText(f), @"//[^\n]*", "");
            if (direct.IsMatch(code)) C(false, $"{rel} asks the game for a land's animals directly");
            if (code.Contains("BiomeAnimals.")) users++;
        }
        C(users >= 12, $"the camps, wishes, goals, visitors, events, missions and PokéWorld ask BiomeAnimals ({users} files)");
        string poke = File.ReadAllText(Path.Combine(src, "Integrations", "PokeWorld.cs"));
        C(poke.Contains("foreach (var k in BiomeAnimals.Of(biome))"), "PokéWorld's look at every land at startup can't switch it off over one land");
        return n;
    }
}
