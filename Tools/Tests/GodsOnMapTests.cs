using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The gods on the map (docs/design/gods.md, "On the map"; Source/ColonyCouncil/Gods/OnMap/): the pure rules (how often a
/// god comes and as what, the omens before, the stranger's stay judged and what it brings, the kindest and their gift, the
/// sacred animal's stay and what it leads to, the weather a god answers in, the marks remembered and visited); the shapes
/// (who comes as what is data); the event, jobs, memory and deeds; the lines said aloud; the words; the seams (the core's
/// API and the presence part's, the one voice, the shared budgets, the soft lookups, the player's mods).
/// </summary>
public static class GodsOnMapTests
{
    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "gods on the map: " + what);
        }

        // ================= How often a god comes =================

        int day = 200;
        C(GodsOnMapMath.Due(GodRealness.Believed, day, 999, 999, 999, true, true, 0f, 0f) == ComingKind.None, "never in Believed");
        C(GodsOnMapMath.Due(GodRealness.Felt, GodsOnMapMath.EarliestDay - 1, 999, 999, 999, true, true, 0f, 0f) == ComingKind.None, "not in the colony's first days");
        C(GodsOnMapMath.Due(GodRealness.Felt, day, GodsOnMapMath.AnyGapDays - 1, 999, 999, true, true, 0f, 0f) == ComingKind.None, "never two comings close together");
        C(GodsOnMapMath.Due(GodRealness.Felt, day, 999, 999, 999, true, true, GodsOnMapMath.DailyChance + 0.001f, 0f) == ComingKind.None, "only on the day's roll");
        C(GodsOnMapMath.Due(GodRealness.Walking, day, 999, 10, 999, true, true, 0f, 0f) == ComingKind.Animal, "a stranger lately: the animal");
        C(GodsOnMapMath.Due(GodRealness.Walking, day, 999, 999, 10, true, true, 0f, 0.99f) == ComingKind.Stranger, "an animal lately: the stranger");
        C(GodsOnMapMath.Due(GodRealness.Felt, day, 999, 999, 999, false, false, 0f, 0f) == ComingKind.None, "nothing that can come: nothing comes");
        C(GodsOnMapMath.Due(GodRealness.Felt, day, 999, 999, 999, true, false, 0f, 0.99f) == ComingKind.Stranger, "only a stranger can come: a stranger");
        C(GodsOnMapMath.Due(GodRealness.Felt, day, 999, 999, 999, false, true, 0f, 0f) == ComingKind.Animal, "only an animal can come: an animal");
        // In a long game, a few times each.
        var rng = new Random(11);
        int strangers = 0, animals = 0, lastAny = -999, lastS = -999, lastA = -999;
        for (int d = 0; d < 600; d++)
        {
            var k = GodsOnMapMath.Due(GodRealness.Felt, d, d - lastAny, d - lastS, d - lastA, true, true, (float)rng.NextDouble(), (float)rng.NextDouble());
            if (k == ComingKind.None) continue;
            lastAny = d;
            if (k == ComingKind.Stranger)
            {
                strangers++;
                lastS = d;
            }
            else
            {
                animals++;
                lastA = d;
            }
        }
        C(strangers >= 2 && strangers <= 5 && animals >= 2 && animals <= 6, $"ten years: a few strangers ({strangers}) and a few sacred animals ({animals}), never a flood");
        C(GodsOnMapMath.Show(GodRealness.Walking) == GodShow.Plain && GodsOnMapMath.Show(GodRealness.Felt) == GodShow.Veiled && GodsOnMapMath.Show(GodRealness.Believed) == GodShow.None,
            "Walking shows plainly, Felt veiled, Believed not at all");
        C(GodsOnMapMath.StrangerWeight(GodDomain.Way, GodDomain.None, GodKind.Ordinary, false, false, true)
          > GodsOnMapMath.StrangerWeight(GodDomain.War, GodDomain.None, GodKind.Ordinary, false, false, true), "the road's god is the likeliest stranger");
        C(GodsOnMapMath.StrangerWeight(GodDomain.War, GodDomain.None, GodKind.Ordinary, false, true, true)
          > GodsOnMapMath.StrangerWeight(GodDomain.War, GodDomain.None, GodKind.Ordinary, false, false, true), "the colony's patron likelier");
        C(GodsOnMapMath.StrangerWeight(GodDomain.Way, GodDomain.None, GodKind.Dead, true, true, true) == 0f
          && GodsOnMapMath.StrangerWeight(GodDomain.Way, GodDomain.None, GodKind.Imprisoned, true, true, true) == 0f, "a dead or imprisoned god never walks");
        C(GodsOnMapMath.AnimalWeight(GodDomain.Wild, GodKind.Ordinary, 0, true, true, true) == 0f, "no beast that can live here: no animal");
        C(GodsOnMapMath.AnimalWeight(GodDomain.Wild, GodKind.Ordinary, 2, false, false, true) > GodsOnMapMath.AnimalWeight(GodDomain.Craft, GodKind.Ordinary, 2, false, false, true),
            "the wild walks as a beast most");

        // ================= The omens =================

        var omens = GodsOnMapMath.Omens(3, _ => true, 5);
        C(omens.Count == 3 && omens.Distinct().Count() == 3, "one omen a day, each of its own kind");
        C(GodsOnMapMath.Omens(9, _ => true, 5).Count == GodsOnMapMath.MaxOmens, "never more than four");
        C(GodsOnMapMath.Omens(3, k => k == OmenKind.Calm, 5).All(k => k == OmenKind.Calm), "only the kinds that can show");
        C(GodsOnMapMath.Omens(3, _ => false, 5).Count == 0 && GodsOnMapMath.Omens(0, _ => true, 5).Count == 0, "none that can show, or no days: no omens");
        bool spread = true;
        for (int i = 0; i < 3; i++)
        {
            int at = GodsOnMapMath.OmenAt(i, 3, 3, omens[i], 5);
            spread &= at >= i * 60000 && at < (i + 1) * 60000;
            int hour = at % 60000 / 2500;
            spread &= omens[i] == OmenKind.Dream ? hour >= 6 && hour <= 7 : hour >= 9 && hour <= 21;
        }
        C(spread, "one a day, in the waking hours (a dream at dawn, when people wake)");
        C(GodsOnMapMath.OmenAt(1, 2, 4, OmenKind.Calm, 1) / 60000 == 3, "the last on the coming's eve");
        C(!GodsOnMapMath.DreamCan(1) && GodsOnMapMath.DreamCan(2), "the same dream needs two dreamers");

        // ================= The stranger's stay judged =================

        var good = new StayTally { welcome = StrangerWelcome.Bed, content = 0.8f, ate = 3, slept = 2, talkers = 3, kindnesses = 2 };
        C(GodsOnMapMath.Judge(good) == Treatment.Generous, "a bed in a good room, a fine meal, talk and kindness: generous");
        C(GodsOnMapMath.Judge(new StayTally { welcome = StrangerWelcome.Bed, content = 0.6f, ate = 2, slept = 1, talkers = 1 }) == Treatment.Kind, "a bed, a simple meal, a word: kind");
        C(GodsOnMapMath.Judge(new StayTally { welcome = StrangerWelcome.Gate, content = 0.5f, ate = 2, slept = -1 }) == Treatment.Fair, "by the gate, fed: fair");
        C(GodsOnMapMath.Judge(new StayTally { welcome = StrangerWelcome.Gate, content = 0.2f, ate = 0, slept = -1, animalsNeglected = true }) == Treatment.Poor,
            "by the gate, hungry, unhappy, the beasts starving: poorly kept");
        C(GodsOnMapMath.Judge(new StayTally { welcome = StrangerWelcome.TurnedAway }) == Treatment.TurnedAway, "turned away");
        var struck = good;
        struck.harmed = true;
        var robbed = good;
        robbed.robbed = true;
        C(GodsOnMapMath.Judge(struck) == Treatment.Harmed && GodsOnMapMath.Judge(robbed) == Treatment.Harmed, "struck or robbed is the worst, whatever else was done");
        var worse = good;
        worse.animalsNeglected = true;
        worse.slaughtered = 2;
        GodsOnMapMath.Judge(good, out float s1);
        GodsOnMapMath.Judge(worse, out float s2);
        C(s2 < s1, "the animals' keeping counts");
        C(GodsOnMapMath.Judge(new StayTally { welcome = StrangerWelcome.Bed, content = float.NaN, ate = 2, slept = 1 }) >= Treatment.Fair, "an unknown mood counts as middling");
        var all = (Treatment[])Enum.GetValues(typeof(Treatment));
        C(all.Zip(all.Skip(1), (a, b) => GodsOnMapMath.FavourFor(a) < GodsOnMapMath.FavourFor(b)).All(x => x), "the better the welcome, the more favour");
        C(GodsOnMapMath.FavourFor(Treatment.Harmed) < 0 && GodsOnMapMath.FavourFor(Treatment.Fair) > 0, "struck displeases, fair pleases");
        C(GodsOnMapMath.SourceOf(Treatment.Harmed) == FavourSource.Sacrilege && GodsOnMapMath.OffenceOf(Treatment.TurnedAway) == GodOffence.Stinginess,
            "a blow is sacrilege; a stranger turned away is the stinginess a generous god hates");
        C(GodsOnMapMath.Curses(Treatment.Harmed) && GodsOnMapMath.Curses(Treatment.TurnedAway) && !GodsOnMapMath.Curses(Treatment.Poor), "the struck and the turned away curse");
        C(!GodsOnMapMath.Blesses(Treatment.Poor) && GodsOnMapMath.Blesses(Treatment.Fair) && !GodsOnMapMath.Gives(Treatment.Fair) && GodsOnMapMath.Gives(Treatment.Kind),
            "fair blesses, kind and better give the kindest a gift");
        C(GodsOnMapMath.EndKind(Treatment.Harmed) == PresenceKind.Wrath && GodsOnMapMath.EndKind(Treatment.Fair) == PresenceKind.Depart && GodsOnMapMath.EndKind(Treatment.Generous) == PresenceKind.Blessing,
            "its end: wrath, departure, blessing");

        // ---- The kindest ----
        C(GodsOnMapMath.Kindest(new[] { 1, 2, 2 }) == -1, "a word or two is not yet kindness");
        C(GodsOnMapMath.Kindest(new[] { 3, 7, 5 }) == 1, "the most kindness");
        C(GodsOnMapMath.Kindest(new[] { GodsOnMapMath.Blow + 6, 4 }) == 1 && GodsOnMapMath.Blow < 0 && GodsOnMapMath.Harsh < 0, "a blow and a harsh word count against");
        C(GodsOnMapMath.Tended > GodsOnMapMath.Shared && GodsOnMapMath.Shared > GodsOnMapMath.Word, "a wound tended counts most, then a meal shared, then a word");

        // ---- The gift ----
        C(GodsOnMapMath.Gift(false, false, false, false, 0.5f) == GodGift.Favour, "nothing else open: the god's favour");
        C(GodsOnMapMath.Gift(false, false, true, false, 0.99f) == GodGift.Passion && GodsOnMapMath.Gift(false, false, false, true, 0f) == GodGift.Trait, "a passion, a trait");
        C(GodsOnMapMath.Gift(true, false, true, false, 0f) == GodGift.Spell, "one who casts may get a spell");
        var counts = new Dictionary<GodGift, int>();
        for (int i = 0; i < 1000; i++)
        {
            var g = GodsOnMapMath.Gift(true, true, true, true, i / 1000f);
            counts[g] = (counts.TryGetValue(g, out int c) ? c : 0) + 1;
        }
        C(counts.Keys.Count == 4 && counts[GodGift.Class] < counts[GodGift.Passion], "every gift open comes, a class rarer than a passion");
        foreach (var d in GodsMath.Domains)
        {
            C(!string.IsNullOrEmpty(GodsOnMapMath.Skill(d)) && !string.IsNullOrEmpty(GodsOnMapMath.Trait(d).trait), $"{d}: a passion and a trait it gives");
            var (things, count) = GodsOnMapMath.Find(d, 2);
            C(things.Length > 0 && count > 0, $"{d}: a find");
        }
        C(GodsOnMapMath.Find(GodDomain.Healing, 0).things.All(t => t != "MedicineIndustrial"), "a tribe's find is of its age (herbal medicine, not the factory's)");

        // ================= The sacred animal =================

        C(GodsOnMapMath.Follows(0.3f) && !GodsOnMapMath.Follows(2f), "a small one follows someone; a large one walks and leads");
        C(GodsOnMapMath.AnimalFavour(false, false, false, false) == GodsOnMapMath.LeftBe && GodsOnMapMath.AnimalFavour(false, false, true, true) > GodsOnMapMath.LeftBe,
            "left be pleases; fed and followed more");
        C(GodsOnMapMath.AnimalFavour(true, false, true, true) < 0 && GodsOnMapMath.AnimalFavour(true, true, false, false) < GodsOnMapMath.AnimalFavour(true, false, false, false),
            "hurt angers whatever else, slain most");
        C(GodsOnMapMath.Boon(GodDomain.Healing, true, true, 0.1f) == AnimalBoon.Cure, "a healer's beast cures one who ails");
        C(GodsOnMapMath.Boon(GodDomain.Craft, false, true, 0.1f) == AnimalBoon.Vein, "the forge's beast finds ore");
        C(GodsOnMapMath.Boon(GodDomain.Craft, false, false, 0.1f) == AnimalBoon.Find && GodsOnMapMath.Boon(GodDomain.Way, false, true, 0.9f) == AnimalBoon.Find, "otherwise a find");
        C(GodsOnMapMath.LeadMin > 10 && GodsOnMapMath.LeadMax > GodsOnMapMath.LeadMin, "it leads far enough to be a walk");

        // ================= Weather as a voice =================

        C(GodsOnMapMath.Voice(GodDomain.Storm, GodDomain.None, 5f, false) == WeatherVoice.Thunder && GodsOnMapMath.Voice(GodDomain.Storm, GodDomain.None, -5f, true) == WeatherVoice.Thunder,
            "the storm speaks in thunder either way");
        C(GodsOnMapMath.Voice(GodDomain.Harvest, GodDomain.None, 5f, false) == WeatherVoice.Rain && GodsOnMapMath.Voice(GodDomain.Harvest, GodDomain.None, -5f, false) == WeatherVoice.None,
            "rain after the harvest's god is pleased, never in anger");
        C(GodsOnMapMath.Voice(GodDomain.Void, GodDomain.None, -5f, false) == WeatherVoice.Eclipse && GodsOnMapMath.Voice(GodDomain.Void, GodDomain.None, -5f, true) == WeatherVoice.None,
            "an eclipse for a dark god's anger, by day");
        C(GodsOnMapMath.Voice(GodDomain.Night, GodDomain.None, 5f, true) == WeatherVoice.Aurora && GodsOnMapMath.Voice(GodDomain.Night, GodDomain.None, 5f, false) == WeatherVoice.None,
            "an aurora on a god's night");
        C(GodsOnMapMath.Voice(GodDomain.Storm, GodDomain.None, 1f, false) == WeatherVoice.None, "a small change goes unanswered");
        C(GodsOnMapMath.Voice(GodDomain.War, GodDomain.None, 9f, false) == WeatherVoice.None, "a god with no voice in the sky keeps silent");
        var own = new List<WeatherVoice> { WeatherVoice.Eclipse, WeatherVoice.Rain };
        C(GodsOnMapMath.Voice(GodDomain.War, GodDomain.None, 5f, false, own) == WeatherVoice.Rain && GodsOnMapMath.Voice(GodDomain.War, GodDomain.None, -5f, false, own) == WeatherVoice.Eclipse,
            "a god's own voices, the first that fits the moment");
        C(GodsOnMapMath.Voice(GodDomain.Storm, GodDomain.None, 5f, true, new List<WeatherVoice> { WeatherVoice.Eclipse }) == WeatherVoice.None, "its own voices only, even against its domain's");
        C(GodsOnMapMath.Fits(WeatherVoice.Thunder, false, true) && !GodsOnMapMath.Fits(WeatherVoice.Rain, false, false) && !GodsOnMapMath.Fits(WeatherVoice.Aurora, true, false),
            "what fits when");
        C(GodsOnMapMath.VoiceChance(1f, 30f) <= 0.66f && GodsOnMapMath.VoiceChance(0f, 3f) > 0f && GodsOnMapMath.VoiceChance(0.9f, 10f) > GodsOnMapMath.VoiceChance(0.2f, 10f),
            "a loud god answers more often, never every time");
        C(GodsOnMapMath.VoiceChance(0.5f, 8f, true) > GodsOnMapMath.VoiceChance(0.5f, 8f) && GodsOnMapMath.WeatherGap(true) < GodsOnMapMath.WeatherGap(false),
            "a god with no body acts through the sky: more readily and more often");
        C(GodsOnMapMath.WeatherGapDays >= 10, "at the game's own pace: days between answers at a home");

        // ================= The marks =================

        C(GodsOnMapMath.Remembered(0) && GodsOnMapMath.Remembered(GodsOnMapMath.MarkYears * 60) && !GodsOnMapMath.Remembered(GodsOnMapMath.MarkYears * 60 + 1), "spoken of for years");
        C(GodsOnMapMath.VisitPick(new[] { 10, 3 }, new[] { 2, 0 }, new[] { 30, 30 }) == 1, "the newest, least visited first");
        C(GodsOnMapMath.VisitPick(new[] { 3 }, new[] { 0 }, new[] { 1 }) == -1, "not visited again too soon");
        C(GodsOnMapMath.VisitPick(new[] { 9999 }, new[] { 0 }, new[] { 9999 }) == -1, "a mark forgotten isn't visited");

        // ================= The shapes: who comes as what is data =================

        var shapesDoc = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodsOnMap_Shapes.xml")).Root!;
        var shapes = shapesDoc.Elements("ColonyCouncil.GodShapeDef").ToList();
        C(shapes.Count(s => s.Element("isDefault")?.Value == "true") == 1, "one default shape");
        var covered = shapes.SelectMany(s => s.Element("domains")?.Elements("li").Select(l => l.Value) ?? Enumerable.Empty<string>()).ToList();
        C(covered.Distinct().Count() == covered.Count, "each domain in one shape at most");
        foreach (var s in shapes)
        {
            bool bodiless = s.Element("bodiless")?.Value == "true";
            C(!bodiless || s.Element("stranger")?.Value == "false" && s.Element("beast")?.Value == "false", $"{s.Element("defName")?.Value}: a god with no body comes neither as a stranger nor as a beast");
            foreach (var v in s.Element("voices")?.Elements("li").Select(l => l.Value) ?? Enumerable.Empty<string>())
                C(Enum.TryParse<WeatherVoice>(v, out var wv) && wv != WeatherVoice.None, $"{s.Element("defName")?.Value}: the voice {v}");
            foreach (var d in s.Element("domains")?.Elements("li").Select(l => l.Value) ?? Enumerable.Empty<string>())
                C(Enum.TryParse<GodDomain>(d, out var gd) && gd != GodDomain.None, $"{s.Element("defName")?.Value}: the domain {d}");
        }
        C(shapes.Any(s => s.Element("bodiless")?.Value == "true") && shapes.Any(s => s.Element("beast")?.Value == "false" && s.Element("stranger")?.Value != "false"),
            "not every god has a body, and not every one with a body has a beast");
        string shapeCode = Code(root, "GodShapes.cs"), strangerCode = Code(root, "GodStranger.cs"), animalCode = Code(root, "SacredAnimal.cs"), weatherCode = Code(root, "GodWeather.cs");
        C(strangerCode.Contains("GodShapes.Stranger(g)") && animalCode.Contains("GodShapes.Beast(god)") && animalCode.Contains("GodShapes.Beasts(god)")
          && weatherCode.Contains("GodShapes.Weather(god)") && weatherCode.Contains("GodShapes.Voices(god)") && weatherCode.Contains("GodShapes.Bodiless(god)"),
            "the stranger, the beast and the weather each come only for a god whose shape has them");
        C(shapeCode.Contains("public static Func<God, GodShapeDef> Override") && shapeCode.Contains("s.gods.Contains(g.name)") && shapeCode.Contains("s.domains.Contains(g.domain)"),
            "a god's own shape first (a hand-made god's def, its name), then its domain's");

        // ================= The defs =================

        var defs = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodsOnMap.xml")).Root!;
        var ev = defs.Elements("IncidentDef").FirstOrDefault(e => e.Element("defName")?.Value == "Rimfolk_Evt_GodStranger");
        C(ev != null && ev.Element("baseChance")?.Value == "0" && ev.Element("workerClass")?.Value == "ColonyCouncil.HappeningWorker_GodStranger",
            "the stranger's event, only ever fired by a god's coming");
        var opts = ev?.Descendants("options").FirstOrDefault()?.Elements("li").ToList() ?? new List<XElement>();
        C(opts.Count == 3 && ev?.Descendants("defaultOption").FirstOrDefault()?.Value == "2", "a bed, away, or the gate (the default, so a letter left unanswered never turns a god away)");
        C(opts.Count == 3 && opts[0].ToString().Contains("DilemmaEffect_Visit") && opts[1].ToString().Contains("DilemmaEffect_SendAway"), "let in, someone goes to talk with them; turned away, they go");
        var texts = ev?.Descendants("texts").FirstOrDefault()?.Elements("li").Select(e => e.Value).ToList() ?? new List<string>();
        foreach (var age in new[] { "age0", "age1", "age2", "age3" }) C(texts.Any(t => t.StartsWith("[" + age + "]")), $"a stranger of the colony's age ({age})");
        C(texts.All(t => !Regex.IsMatch(t, @"\bgod\b", RegexOptions.IgnoreCase)), "the letter never says it is a god");
        foreach (var name in new[] { "Rimfolk_VisitGodMark", "Rimfolk_FeedSacredAnimal" })
            C(defs.Elements("JobDef").Any(e => e.Element("defName")?.Value == name), $"the job {name}");
        C(defs.Elements("ColonyCouncil.SharedMemoryDef").Any(e => e.Element("defName")?.Value == "Rimfolk_Mem_GodSeen" && e.Element("fadeDays")?.Value == "600"), "a god seen is remembered together for years");
        var deeds = defs.Elements("ColonyCouncil.DeedDef").ToList();
        C(deeds.Count == 5 && deeds.All(d => defs.Elements("RulePackDef").Any(r => r.Element("defName")?.Value == d.Element("logTopic")?.Value)), "the deeds talked of, each with its words");

        // ================= The lines said aloud =================

        var linesDoc = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodsOnMap_Lines.xml")).Root!;
        string code = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Source", "ColonyCouncil", "Gods", "OnMap"), "*.cs").Select(File.ReadAllText));
        var domains = new HashSet<string>(GodsMath.Domains.Select(d => d.ToString().ToLowerInvariant()));
        var known = new HashSet<string>(domains)
        {
            "age0", "age1", "age2", "age3", "walking", "felt", "believed", "gate", "animals", "birds", "calm", "candle", "restless", "rain", "thunder", "eclipse", "aurora",
            "pleased", "angered", "named", "appear", "act", "reveal", "depart", "blessing", "wrath", "omen", "small", "large", "chosen"
        };
        foreach (var pool in linesDoc.Elements("ColonyCouncil.FlavorPoolDef"))
        {
            string name = pool.Element("defName")?.Value ?? "";
            var lines = pool.Descendants("li").Select(l => l.Value).ToList();
            bool spoken = name != "Rimfolk_Pool_GodDream";
            C(lines.Count >= 5 && code.Contains("\"" + name + "\""), $"{name}: its lines, and the code says them");
            C(lines.Count(l => !l.StartsWith("[")) >= 3, $"{name}: lines that fit every god");
            foreach (var line in lines)
            {
                var m = Regex.Match(line, @"^\[([^\]]+)\]\s*(.*)$");
                var tags = m.Success ? m.Groups[1].Value.Split(',').Select(t => t.Trim().TrimStart('!')).ToList() : new List<string>();
                string body = m.Success ? m.Groups[2].Value : line;
                C(tags.All(known.Contains), $"{name}: '{line}' has only known tags");
                C(!body.StartsWith("{GOD}") && !body.Contains('—') && body.Count(ch => ch == '!') == 0, $"{name}: '{body}' never starts with the god's words, no dashes or exclamations");
                C(!Regex.IsMatch(body, @"\b(colour|grey|traveller|armour|honour)\b"), $"{name}: '{body}' in American spelling");
                if (!spoken) continue;
                C(body.Length <= 90 && !body.StartsWith("\""), $"{name}: '{body}' short enough to say");
                C(Regex.Matches(body, @"[.?!](\s|$)").Count == 1 && Regex.IsMatch(body, @"[.?!]$"), $"{name}: '{body}' is one sentence");
            }
        }

        // ================= The words =================

        var keyed = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var e in XDocument.Load(file).Root!.Elements())
                keyed[e.Name.LocalName] = e.Value;
        foreach (Match m in Regex.Matches(code, @"""(CC_[A-Za-z_]+[A-Za-z])""\.Translate"))
            C(keyed.ContainsKey(m.Groups[1].Value), $"the key {m.Groups[1].Value} exists");
        foreach (var t in Enum.GetNames(typeof(Treatment)))
            foreach (var k in new[] { "CC_GodM_Stranger_Title_", "CC_GodM_Stranger_Plain_", "CC_GodM_Stranger_Veiled_", "CC_GodM_Stranger_Act_", "CC_GodM_Why_" })
                C(keyed.ContainsKey(k + t), $"{k}{t}");
        foreach (var k in keyed.Keys.Where(k => k.StartsWith("CC_GodM_Stranger_Veiled_") || k.EndsWith("Veiled") && k.StartsWith("CC_GodM_") && k.Contains("Text")))
            C(keyed[k].Contains("ome say"), $"{k}: veiled, it says only what some say");
        foreach (var k in new[] { "Believed", "Felt", "Walking" }) C(keyed.ContainsKey("CC_GodM_Sign_" + k), $"a sign put to its god, {k}");
        foreach (var k in new[] { "Rain", "Thunder", "Eclipse", "Aurora" }) C(keyed.ContainsKey("CC_GodM_Weather_" + k) && keyed["CC_GodM_Weather_" + k].Contains("{GOD}"), $"the weather's {k} put to its god");
        foreach (var kv in keyed.Where(kv => kv.Key.StartsWith("CC_GodM_")))
            C(!kv.Value.Contains('—') && !kv.Value.Contains('!') && !Regex.IsMatch(kv.Value, @"\b(colour|colours|grey|armour)\b"), $"{kv.Key}: no dashes or exclamations, American spelling");

        // ================= The seams =================

        string bare = Regex.Replace(code, @"//[^\n]*", "");
        C(code.Contains("GodPresenceSeam.Call(") && code.Contains("GodLookSeam.Make(") && code.Contains("PresenceKind.Omen") && code.Contains("GodAvatar.Reveal("),
            "through the core's seams and the presence part's avatar");
        C(Regex.IsMatch(Code(root, "GodOmens.cs"), @"public static int Before\(God god, Map map, int days\)"), "GodOmens.Before(god, map, days): the omens the other parts can ask for");
        C(Regex.IsMatch(weatherCode, @"public static bool Answer\(God god, Map map, float amount, bool anyway = false\)"), "GodWeather.Answer: a god's answer the worship's festivals can ask for");
        C(code.Contains("Gods.Remember(") && code.Contains("AddChronicle(") && code.Contains("SharedMemories.Add(") && code.Contains("Deeds.Record("), "the marks in the history, the chronicle, the shared memories and the deeds");
        C(code.Contains("Gods.AddFavour(") && !Regex.IsMatch(bare, @"FavourValue\("), "favour moved through the core, never read as a number here");
        C(!bare.Contains("MoteMaker.ThrowText") && !Regex.IsMatch(Regex.Replace(code.Replace(Code(root, "GodsOnMapDebug.cs"), ""), @"//[^\n]*", ""), @"Messages\.Message\([^;]*MessageTypeDefOf\.(?!RejectInput)"),
            "speech through Speech.Say, news through the teller (a click's rejection and the dev actions answer directly)");
        C(Code(root, "GodGifts.cs").Contains("Harm.May(p)") && Code(root, "GodGifts.cs").Contains("IllnessBudget.Allows(") && Code(root, "GodGifts.cs").Contains("IllnessBudget.Owe(")
          && Code(root, "GodGifts.cs").Contains("Harm.Scale"), "a god's fever only under the shared harm cap and illness budget, at the game's rate");
        C(strangerCode.Contains("Tell.CanAsk") && strangerCode.Contains("InvitedGuests.HandOver(") && strangerCode.Contains("VisitorServices.Lodge("),
            "the stranger's question waits for the letters' gap; let in, Hospitality's guest when it is loaded, else Rimfolk's");
        C(animalCode.Contains("LegendBeasts") || animalCode.Contains("exitMapAfterTick"), "the sacred animal walks the land as the game's own wild beasts do");
        C(!code.Contains("TorannMagic.") || code.Contains("AccessTools.TypeByName(\"TorannMagic"), "A RimWorld of Magic reached by name only");
        C(!Regex.IsMatch(bare, @"DefDatabase<\w+>\.GetNamed\("), "every def looked up softly");
        string patches = Code(root, "GodsOnMapPatches.cs");
        C(Regex.Matches(patches, @"\[HarmonyPatch\(").Count == 4 && Regex.Matches(patches, @"GodAvatar\.Any|GodOmens\.Out").Count >= 4, "each hook looks first whether anything of the gods' is about");
        C(Code(root, "GodsOnMapDebug.cs").Split("[DebugAction(\"Rimfolk\", \"Gods: ").Length - 1 >= 8, "the dev actions: the stranger, its end, the animal, a lead, the weather, the omens, a mark, the log");
        string book = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Gods", "GodBook.cs"));
        C(book.Contains("GodsOnMapBook.Daily(now)") && book.Contains("GodsOnMapBook.Hourly(now)"), "kept in the gods' own day and hour, no pass of its own");

        // ================= The player's mods =================

        var active = Catalogue(root);
        foreach (var d in GodsMath.Domains)
        {
            foreach (var spell in GodsOnMapMath.Spells(d)) C(active.TryGetValue(spell, out var t) && t.Contains("ThingDef"), $"{d}: the spell {spell} is in the player's mods");
            foreach (var cls in GodsOnMapMath.Classes(d)) C(active.TryGetValue(cls, out var t) && t.Contains("TraitDef"), $"{d}: the class {cls} is in the player's mods");
            var (trait, _) = GodsOnMapMath.Trait(d);
            C(active.TryGetValue(trait, out var tt) && tt.Contains("TraitDef"), $"{d}: the trait {trait} is in the game");
            C(active.TryGetValue(GodsOnMapMath.Skill(d), out var st) && st.Contains("SkillDef"), $"{d}: the skill {GodsOnMapMath.Skill(d)}");
            foreach (int age in new[] { 0, 1, 2, 3 })
                C(GodsOnMapMath.Find(d, age).things.Any(x => active.TryGetValue(x, out var ft) && ft.Contains("ThingDef")), $"{d}, age {age}: its find is in the game");
        }
        return n;
    }

    private static Dictionary<string, HashSet<string>> catalogue;

    private static Dictionary<string, HashSet<string>> Catalogue(string root)
    {
        if (catalogue != null) return catalogue;
        catalogue = new Dictionary<string, HashSet<string>>();
        using var gz = new GZipStream(File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz")), CompressionMode.Decompress);
        using var r = new StreamReader(gz);
        string line;
        while ((line = r.ReadLine()) != null)
        {
            var t = line.Split('\t');
            if (t.Length < 4 || t[0] == "type" || t[3] != "yes") continue;
            if (!catalogue.TryGetValue(t[1], out var set)) catalogue[t[1]] = set = new HashSet<string>();
            set.Add(t[0]);
        }
        return catalogue;
    }

    private static string Code(string root, string file)
    {
        string p = Path.Combine(root, "Source", "ColonyCouncil", "Gods", "OnMap", file);
        return File.Exists(p) ? File.ReadAllText(p) : "";
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
