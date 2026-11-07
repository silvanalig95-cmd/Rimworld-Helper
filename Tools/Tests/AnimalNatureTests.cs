using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The animals' natures (Animals/AnimalNatureMath.cs): the nine temperaments and which a kind leans to, the fixed pick
/// for one animal, how often each shows, friends and rivals, grief at a grave, an old animal knowing its handler, who
/// may speak and the compass word for a danger; and the mod's side: the settings and their defaults (speech a fantasy
/// part, on Auto), no letter or message anywhere in it (the player's condition), every line pool and tag the code asks
/// for, the jobs and their words, the one book saved by id, the hooks into the pets' watch, the fight, the meeting, the
/// council's questions, the dreams and PokéWorld's evolutions, the dev actions, the Almanac's page.
/// </summary>
public static class AnimalNatureTests
{
    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "animal natures: " + what);
        }

        // ================= The temperaments =================

        C(AnimalNatureMath.All.Length == 9 && AnimalNatureMath.All.Select(AnimalNatureMath.Key).SequenceEqual(new[]
            { "brave", "timid", "greedy", "gentle", "proud", "mischievous", "loyal", "lazy", "curious" }), "nine temperaments, in their saved order");
        C(AnimalNatureMath.TryOf(6, out var loyal) && loyal == Temper.Loyal && !AnimalNatureMath.TryOf(9, out _) && !AnimalNatureMath.TryOf(-1, out _),
            "a saved number reads back, and one out of range is none");

        // ================= A kind's family =================

        C(AnimalNatureMath.Words("YorkshireTerrier").SequenceEqual(new[] { "yorkshire", "terrier" }) && AnimalNatureMath.Words("snow_hare").SequenceEqual(new[] { "snow", "hare" }),
            "a name's words, split at capitals and at anything not a letter");
        C(AnimalNatureMath.Family("Husky", "husky", false, false) == "canine" && AnimalNatureMath.Family("Cat", "cat", false, false) == "feline"
          && AnimalNatureMath.Family("Muffalo", "muffalo", false, false) == "grazer" && AnimalNatureMath.Family("Horse", "horse", false, false) == "hoofed"
          && AnimalNatureMath.Family("Thrumbo", "thrumbo", false, false) == "thrumbo" && AnimalNatureMath.Family("Bear_Grizzly", "grizzly bear", false, false) == "ursine",
            "the game's own: a husky canine, a cat feline, a muffalo a grazer, a horse hoofed, a thrumbo itself, a bear ursine");
        C(AnimalNatureMath.Family("Cow", "cattle", false, false) == "grazer" && AnimalNatureMath.Family("Caterpillar", "caterpillar", false, false) == "beast",
            "words, not letters: cattle are no cats, nor is a caterpillar");
        C(AnimalNatureMath.Family("Chicken", "chicken", true, false) == "bird" && AnimalNatureMath.Family("Megascarab", "megascarab", false, true) == "bug",
            "a bird by its body, an insect a bug whatever its name");
        C(AnimalNatureMath.Family("VAE_Lynx", "snow lynx", false, false) == "feline" && AnimalNatureMath.Family("GR_Manwolf", "manwolf", false, false) == "canine",
            "other mods' kinds by their words (Vanilla Animals Expanded, Vanilla Genetics Expanded)");
        C(AnimalNatureMath.Family("Growlithe", "growlithe", false, false) == "beast", "a Pokémon with no family word is a beast, its types lean it instead");

        // ================= Which temperament a kind leans to =================

        NatureFacts Facts(string family, float size = 1f, bool predator = false, int train = 1, params string[] types) =>
            new NatureFacts { family = family, bodySize = size, predator = predator, trainability = train, types = types, pokemon = types.Length > 0 };
        Temper Top(float[] w) => (Temper)Array.IndexOf(w, w.Max());
        var dog = AnimalNatureMath.Weights(Facts("canine", 0.8f, false, 2));
        var cat = AnimalNatureMath.Weights(Facts("feline", 0.4f, true));
        var muffalo = AnimalNatureMath.Weights(Facts("grazer", 2.4f, false, 1));
        var thrumbo = AnimalNatureMath.Weights(Facts("thrumbo", 4f, false, 1));
        C(Top(dog) == Temper.Loyal, "a dog leans loyal");
        C(Top(cat) == Temper.Proud, "a cat leans proud");
        C(Top(muffalo) == Temper.Gentle, "a muffalo leans gentle");
        C(Top(thrumbo) == Temper.Gentle && thrumbo[(int)Temper.Timid] == thrumbo.Min(), "a thrumbo leans gentle, and is least of all timid");
        var fire = AnimalNatureMath.Weights(Facts("beast", 0.9f, false, 1, "Fire"));
        var ghost = AnimalNatureMath.Weights(Facts("beast", 0.5f, false, 1, "Ghost"));
        C(Top(fire) == Temper.Brave || Top(fire) == Temper.Proud, "a fire Pokémon leans brave or proud");
        C(Top(ghost) == Temper.Mischievous, "a ghost Pokémon leans mischievous");
        C(AnimalNatureMath.Weights(Facts("beast", 1f, false, 1, "PSYCHIC"))[(int)Temper.Proud] > 1f, "a type is read whatever its case");
        foreach (var fam in new[] { "canine", "feline", "hoofed", "grazer", "ursine", "thrumbo", "bird", "bug", "reptile", "small", "beast" })
        {
            var w = AnimalNatureMath.Weights(Facts(fam));
            C(w.Length == 9 && w.All(x => x > 0f), $"{fam}: every temperament possible");
        }

        // ================= The fixed pick =================

        C(AnimalNatureMath.Pick(12345, dog) == AnimalNatureMath.Pick(12345, dog), "the same animal always gets the same temperament");
        var counts = new int[9];
        for (int i = 0; i < 18000; i++) counts[(int)AnimalNatureMath.Pick(i * 7 + 1, AnimalNatureMath.Weights(Facts("beast")))]++;
        C(counts.All(c => c > 1000 && c < 3000), "an even kind's temperaments come about evenly: " + string.Join(",", counts));
        var dogs = new int[9];
        for (int i = 0; i < 9000; i++) dogs[(int)AnimalNatureMath.Pick(i * 13 + 5, dog)]++;
        C(Array.IndexOf(dogs, dogs.Max()) == (int)Temper.Loyal && dogs.All(c => c > 0), "most dogs are loyal, and some are anything: " + string.Join(",", dogs));
        C(AnimalNatureMath.Pick(1, null) == Temper.Curious && AnimalNatureMath.Pick(1, new float[9]) == Temper.Curious, "no weights: curious");
        C(AnimalNatureMath.Mix(1) != AnimalNatureMath.Mix(2) && AnimalNatureMath.Mix(-5) == AnimalNatureMath.Mix(-5), "the seed's number spreads, and stays the same");

        // ================= How often =================

        C(AnimalNatureMath.MomentDays(Temper.Greedy) == 5f && AnimalNatureMath.MomentDays(Temper.Mischievous) == 5f && AnimalNatureMath.MomentDays(Temper.Gentle) == 3f
          && AnimalNatureMath.MomentDays(Temper.Brave) == 2f && AnimalNatureMath.MomentDays(Temper.Loyal) == 0.5f, "the days between each temperament's moments");
        C(AnimalNatureMath.HourChance(5f, 0f) == 0f && Math.Abs(AnimalNatureMath.HourChance(5f, 1f) - 1f / 120f) < 1e-6 && Math.Abs(AnimalNatureMath.HourChance(5f, 2f) - 2f / 120f) < 1e-6,
            "the hour's chance: none at 0%, a fifth of a day's at 100% for five days, twice that at 200%");
        C(AnimalNatureMath.HourChance(0.01f, 2f) <= 1f && AnimalNatureMath.HourChance(0f, 1f) == 0f, "never past certain, nothing for no days");
        C(AnimalNatureMath.MomentsAnHour == 1, "one moment an hour on a map at most");
        C(AnimalNatureMath.FightChance(1f) == 0.6f && AnimalNatureMath.FightChance(0f) == 0f && AnimalNatureMath.FightChance(2f) == 1f, "in a fight: three in five at 100%, every time at 200%, none at 0%");
        C(AnimalNatureMath.StandMinSize > 0.2f && AnimalNatureMath.FightRange >= 15f, "a brave hen stays put; the fight is looked at within reach");
        C(AnimalNatureMath.StandTicks <= 2500 && AnimalNatureMath.ShowTicks < AnimalNatureMath.StandTicks, "a stand lasts an hour at most, showing off less");
        C(AnimalNatureMath.Dangerous(true, false, 0.1f, 2f) && AnimalNatureMath.Dangerous(false, true, 0.1f, 2f) && !AnimalNatureMath.Dangerous(false, false, 0.5f, 2f)
          && AnimalNatureMath.Dangerous(false, false, 1.8f, 2f), "warned of: an angry beast, one hunting, or one near its own size; not a fox to a horse");

        // ================= Friends and rivals =================

        C(AnimalNatureMath.FriendNights == 3 && AnimalNatureMath.NightCounts(-1, 0) && !AnimalNatureMath.NightCounts(4, 4) && AnimalNatureMath.NightCounts(4, 5),
            "three nights side by side, each counted once");
        C(AnimalNatureMath.MaxFriends == 2 && AnimalNatureMath.MaxRivals == 1 && AnimalNatureMath.TieFadeDays == 40f && AnimalNatureMath.PairForgetDays == 10f,
            "two friends and one rival each; forty days apart and they drift; a pair unseen ten days forgotten");
        float plain = AnimalNatureMath.RivalChance(false, false, Temper.Curious, Temper.Lazy, false, false);
        float catDog = AnimalNatureMath.RivalChance(true, false, Temper.Curious, Temper.Lazy, false, false);
        C(plain > 0f && plain <= 0.005f && Math.Abs(catDog - 10f * plain) < 1e-5, "a feud is rare, and a cat and a dog ten times likelier");
        C(AnimalNatureMath.RivalChance(true, true, Temper.Proud, Temper.Greedy, true, false) == 0f && AnimalNatureMath.RivalChance(true, true, Temper.Proud, Temper.Greedy, false, true) == 0f,
            "never between friends, nor two of a kind");
        C(AnimalNatureMath.RivalChance(false, false, Temper.Gentle, Temper.Lazy, false, false) < plain
          && AnimalNatureMath.RivalChance(false, false, Temper.Proud, Temper.Lazy, false, false) > plain, "the gentle keep the peace; the proud don't");
        C(AnimalNatureMath.RivalChance(true, true, Temper.Proud, Temper.Mischievous, false, false) <= 0.5f, "never more than even");
        C(AnimalNatureMath.TieHourChance(false) > AnimalNatureMath.TieHourChance(true) && AnimalNatureMath.TieHourChance(true) < 0.05f, "friends' moments come oftener than rivals', both now and then");

        // ================= Grief and old animals =================

        float early = AnimalNatureMath.GraveHourChance(5f), later = AnimalNatureMath.GraveHourChance(90f);
        C(early > later && later > 0f && AnimalNatureMath.GraveHourChance(121f) == 0f && AnimalNatureMath.GraveHourChance(-1f) == 0f,
            "a grave every few days at first, now and then later, none after four months");
        C(Math.Abs(early * AnimalNatureMath.WakingHours - 1f / 2.5f) < 1e-5, "about every two and a half days in the first two months");
        C(AnimalNatureMath.Grieving(100f) && !AnimalNatureMath.Grieving(130f), "its card says it grieves while it does");
        C(!AnimalNatureMath.Recognises(1f, -1f) && AnimalNatureMath.Recognises(3f, -1f) && !AnimalNatureMath.Recognises(130f, -1f)
          && !AnimalNatureMath.Recognises(30f, 4f) && AnimalNatureMath.Recognises(30f, 11f),
            "it knows its handler two days to two years after, not twice in ten days");

        // ================= Who speaks =================

        C(!AnimalNatureMath.Speaks(AnimalSpeech.Auto, false) && AnimalNatureMath.Speaks(AnimalSpeech.Auto, true) && AnimalNatureMath.Speaks(AnimalSpeech.On, false)
          && !AnimalNatureMath.Speaks(AnimalSpeech.Off, true), "speech: Auto only with a fantasy source, On always, Off never");
        C(!AnimalNatureMath.HearsByPsylink(2) && AnimalNatureMath.HearsByPsylink(3) && AnimalNatureMath.HearsByPsylink(6), "a psycaster of level three or more hears their bonded animal");
        C(AnimalNatureMath.Voice(Facts("canine")) == "dog" && AnimalNatureMath.Voice(Facts("feline")) == "cat" && AnimalNatureMath.Voice(Facts("thrumbo")) == "thrumbo"
          && AnimalNatureMath.Voice(Facts("beast", 1f, false, 1, "Psychic")) == "pokemon" && AnimalNatureMath.Voice(Facts("nothing")) == "beast", "its voice by its family, a Pokémon's its own");
        C(AnimalNatureMath.Compass(0, 10) == "north" && AnimalNatureMath.Compass(10, 0) == "east" && AnimalNatureMath.Compass(-10, -10) == "southwest"
          && AnimalNatureMath.Compass(3, -20) == "south" && AnimalNatureMath.Compass(1, -1) == "here", "where a danger is, by the compass (north up the map)");
        C(AnimalNatureMath.DilemmaShare > 0f && AnimalNatureMath.DilemmaShare <= 0.5f && AnimalNatureMath.SayGapDays >= 1f && AnimalNatureMath.SayHourChance <= 0.25f,
            "rare: a line a day at most, a word on half the questions about its person at most");

        // ================= What is kept =================

        C(AnimalNatureMath.MaxMarks == 300 && AnimalNatureMath.MaxTies == 80 && AnimalNatureMath.MaxGone == 30 && AnimalNatureMath.MarkForgetDays == 120f,
            "kept: three hundred temperaments, eighty ties, thirty gone");

        // ================= The mod's side =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.Exists(Path.Combine(src, rel)) ? File.ReadAllText(Path.Combine(src, rel)) : "";
        string tempers = Read("Animals/AnimalTempers.cs"), bonds = Read("Animals/AnimalBonds.cs"), voices = Read("Animals/AnimalVoices.cs"), book = Read("Animals/AnimalBook.cs"),
            jobs = Read("Animals/AnimalNatureJobs.cs"), debug = Read("Animals/AnimalNatureDebug.cs"), settings = Read("CouncilSettings.cs"), catalog = Read("UI/SettingsCatalog.cs");
        C(tempers.Contains("public static bool Companion(Pawn a)") && bonds.Contains("Where(AnimalTempers.Companion)"), "friends and feuds are the companions', not the whole named herd's");
        C(tempers.Length > 0 && bonds.Length > 0 && voices.Length > 0 && book.Length > 0 && jobs.Length > 0 && debug.Length > 0, "the six files are there");

        // No pop-ups: the player's condition for the bonds, and the rule for the rest.
        var popups = new Regex(@"\bTell\.(Letter|Message|Minor)\(|\bNews\.Post\(|\bMessages\.Message\(|\bLetterStack\b|\bLetterMaker\b");
        foreach (var (name, text) in new[] { ("AnimalBonds.cs", bonds), ("AnimalTempers.cs", tempers), ("AnimalVoices.cs", voices), ("AnimalBook.cs", book), ("AnimalNatureJobs.cs", jobs) })
            C(!popups.IsMatch(text), $"{name}: no letter and no message");
        C(!Regex.IsMatch(tempers + bonds + voices, @"MoteMaker\.ThrowText"), "speech only through Speech.Say");
        C(Regex.Matches(tempers + bonds + voices, @"Speech\.Say\(").Count >= 4, "what is said goes through Speech.Say");
        C(!Regex.IsMatch(bonds, @"Harm\.(Injure|Roll|Fight)"), "nobody is hurt in a feud");

        // The settings.
        C(settings.Contains("public bool animalTempers = true;") && settings.Contains("public float animalMoments = 1f;") && settings.Contains("public bool animalBonds = true;")
          && settings.Contains("public AnimalSpeech speakingAnimals = AnimalSpeech.Auto;"), "the four settings and their defaults (speech on Auto)");
        C(catalog.Contains("Toggle(\"animalTempers\"") && catalog.Contains("Toggle(\"animalBonds\"") && catalog.Contains("Choice(\"speakingAnimals\"") && catalog.Contains("Slider(\"animalMoments\""),
            "each drawn in the settings");
        C(voices.Contains("PokeWorldLink.Active") && voices.Contains("SettingsCatalog.MagicId") && voices.Contains("GetPsylinkLevel"), "Auto looks for PokéWorld, A RimWorld of Magic and a psylink");
        string keyed = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        var tip = Regex.Match(keyed, @"<CC_Settings_SpeakingAnimals_Tip>(.*?)</CC_Settings_SpeakingAnimals_Tip>").Groups[1].Value;
        C(tip.Contains("PokéWorld") && tip.Contains("A RimWorld of Magic") && tip.Contains("psylink") && tip.Contains("Auto") && tip.Contains("otherwise off"),
            "the tooltip says speech is off by default unless a fantasy source is there");

        // Who speaks, softly by mod.
        C(voices.Contains("\"Psychic\"") && voices.Contains("TypesOf"), "PokéWorld: a psychic-type Pokémon, by its types");
        C(voices.Contains("GetNamedSilentFail(\"Druid\")") && voices.Contains("GetNamedSilentFail(\"Ranger\")"), "A RimWorld of Magic: a druid or a ranger, their classes read by name");
        C(voices.Contains("\"CompProperties_HumanoidHybrid\""), "Vanilla Genetics Expanded: the humanoid hybrids, by their comp's name");
        C(voices.Contains("TechLevel.Neolithic") && voices.Contains("\"AnimalPersonhood\""), "a spirit animal in a tribe, or a faith of animal personhood");

        // Kept by id, never by reference (the handler aside).
        C(Regex.Matches(book, @"Scribe_References\.Look").Count == 1 && book.Contains("Scribe_References.Look(ref handler"), "the book keeps animals by id; only the handler by reference");
        C(Regex.Matches(book, @"Scribe_Collections\.Look").Count == 3, "three collections saved: temperaments, ties, gone");
        string estimate = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        foreach (var col in new[] { "marks", "ties", "gone" })
            C(estimate.Contains("\"Animals/AnimalBook.cs:" + col + "\""), $"the save estimate models AnimalBook's {col}");

        // The hooks.
        C(Read("Strangers/Pets.cs").Contains("AnimalBonds.Notify_Died(pet)") && Read("Strangers/Pets.cs").Contains("AnimalBonds.Notify_Left(pet)"), "the pets' watch tells of a death and of one gone alive");
        C(Read("Ambitions/CouncilMeeting.cs").Contains("AnimalVoices.MeetingWord(this, here)"), "a word at the council meeting");
        C(Read("Dilemmas/Dilemmas.cs").Contains("AnimalVoices.DilemmaWord(ctx)"), "a word on the council's question");
        C(Read("Social/Talk/TalkTracker.cs").Contains("AnimalTempers.FightLook()") && Read("Social/Talk/TalkTracker.cs").Contains("AnimalVoices.FightLook()"),
            "the fight's look rides on the fight sides' tick (no new slot)");
        C(Read("Integrations/PokeWorldLife.cs").Contains("AnimalBook.Instance?.Carry(before.pawn, p)"), "an evolved Pokémon keeps its temperament, friends and rivals");
        C(Read("Dreams/Dreams.cs").Contains("case DreamAbout.SpiritAnimal:"), "the dreams know the spirit animal");
        C(Read("Animals/AnimalLife.cs").Contains("AnimalTempers.Hourly") && Read("Animals/AnimalLife.cs").Contains("AnimalBonds.Hourly") && Read("Animals/AnimalLife.cs").Contains("AnimalVoices.Hourly")
          && Read("Animals/AnimalLife.cs").Contains("AnimalBook.Instance?.Daily()"), "the animals' hour and day run them");
        C(jobs.Contains("[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]") && jobs.Contains("RimfolkPatches.On"), "the card's line is a patch that honours the deeper hooks");
        C(Regex.Matches(debug, @"\[DebugAction\(""Rimfolk"", ""Animals: ").Count >= 12, "the dev actions, one for each moment");

        // The defs: the jobs and their words, the pools, the dream.
        string defs = Path.Combine(root, "Defs");
        var jobDoc = XDocument.Load(Path.Combine(defs, "Misc", "Rimfolk_AnimalNature_Jobs.xml"));
        var jobNames = jobDoc.Root!.Elements("JobDef").Select(e => e.Element("defName")?.Value).ToList();
        foreach (var j in Regex.Matches(jobs, @"public static JobDef (\w+);").Select(m => m.Groups[1].Value))
            C(jobNames.Contains(j), $"the job {j} has its def");
        foreach (var r in jobDoc.Descendants("report").Select(e => e.Value))
            C(keyed.Contains("<" + r + ">"), $"the job's words {r} exist");
        var pools = new Dictionary<string, List<string>>();
        foreach (var file in Directory.GetFiles(Path.Combine(defs, "FlavorPoolDefs"), "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements("ColonyCouncil.FlavorPoolDef"))
                pools[el.Element("defName")!.Value] = el.Descendants("li").Select(li => li.Value).ToList();
        foreach (var p in new[] { "Council_Pool_SeeTemper", "Council_Pool_SeeAnimals", "Council_Pool_AnimalVoice", "Council_Journal_AnimalNature", "Council_Journal_AnimalBonds", "Council_Journal_AnimalVoice" })
            C(pools.ContainsKey(p) && (tempers + bonds + voices).Contains("\"" + p + "\""), $"{p} exists and is used");
        var see = pools.GetValueOrDefault("Council_Pool_SeeTemper") ?? new List<string>();
        foreach (var t in AnimalNatureMath.All)
            C(see.Count(l => l.Contains("temper:" + AnimalNatureMath.Key(t))) >= 3, $"someone has something to say on seeing a {AnimalNatureMath.Key(t)} one");
        C(see.All(l => l.StartsWith("[") && l.Contains("temper:")), "every line said on seeing a temperament names its temperament (none said in another's place)");
        var seeAnimals = pools.GetValueOrDefault("Council_Pool_SeeAnimals") ?? new List<string>();
        foreach (var tag in new[] { "friends", "rivals", "grave,person", "grave,friend", "knowsme" })
            C(seeAnimals.Count(l => l.StartsWith("[" + tag)) >= 2, $"lines for {tag}");
        var voice = pools.GetValueOrDefault("Council_Pool_AnimalVoice") ?? new List<string>();
        foreach (var v in new[] { "dog", "cat", "horse", "herd", "bear", "thrumbo", "bird", "bug", "reptile", "small", "beast", "pokemon" })
            C(voice.Any(l => l.Contains("idle") && l.Contains("voice:" + v)), $"a {v}'s voice has words of its own");
        foreach (var moment in new[] { "warn,raid", "warn,predator", "meeting,side:person", "meeting,side:own", "dilemma,side:person", "dilemma,side:own", "dilemma,aboutme" })
            C(voice.Count(l => l.StartsWith("[" + moment)) >= 2, $"a speaking animal has words for {moment}");
        C(voice.Where(l => l.StartsWith("[warn")).All(l => l.Contains("{PLACE}")), "every warning says where");
        var dream = XDocument.Load(Path.Combine(defs, "DreamDefs", "Council_Dreams_SpiritAnimal.xml")).Root!.Element("ColonyCouncil.DreamDef");
        C(dream?.Element("about")?.Value == "SpiritAnimal" && dream.Descendants("li").Count() >= 10, "the spirit animal's dream, with its lines");

        // The Almanac's page.
        string alm = File.ReadAllText(Path.Combine(defs, "AlmanacArticleDefs", "Almanac_Council_AnimalNature.xml"));
        C(alm.Contains("Council_Alm_C_AnimalNature") && alm.Contains("Council_Alm_C_Animals"), "the Almanac has a page, beside the animals' page");
        foreach (var t in AnimalNatureMath.All) C(alm.Contains(AnimalNatureMath.Key(t)), $"the Almanac names the {AnimalNatureMath.Key(t)} one");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
