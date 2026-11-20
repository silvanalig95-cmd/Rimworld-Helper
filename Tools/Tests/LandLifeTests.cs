using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The land and its life (the content update's integrations, C19, C20, C21, C22, C24, C28): the work party after every
/// disaster (AfterStormMath), last winter as the weather record keeps it for the advisor (WeatherMath), the herd's autumn
/// count (HerdStoryMath), the colony's animals at a line and the weather in a raid (DefenseLineMath, ArtTalesMath.Held), a
/// cup for the sick (SickCupMath); the defs and words they stand on, every mod's def by name in the player's active mods,
/// and the seams into the weather book, the play-outs, the advisor, the herd, the trade window, the defense lines and the
/// council's hour.
/// </summary>
public static class LandLifeTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the land and its life: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string keyedAll = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        var keyed = Regex.Matches(keyedAll, @"^\s*<([A-Za-z0-9_]+)>(.*?)</\1>\s*$", RegexOptions.Multiline).Cast<Match>()
            .GroupBy(m => m.Groups[1].Value).ToDictionary(g => g.Key, g => g.First().Groups[2].Value);
        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f).Root!).ToList();
        IEnumerable<XElement> All(string tag) => defs.SelectMany(r => r.Elements(tag));
        XElement Def(string tag, string name) => All(tag).FirstOrDefault(e => e.Element("defName")?.Value == name);

        // The player's active mods' items (docs/player-mods/items.tsv): every def named in code must be there.
        var active = new HashSet<string>();
        foreach (var line in File.ReadLines(Path.Combine(root, "docs", "player-mods", "items.tsv")).Skip(1))
        {
            var p = line.Split('\t');
            if (p.Length > 2 && p[2] == "yes") active.Add(p[0]);
        }

        // ================= C19: after the storm =================

        C(AfterStormMath.Kinds.SequenceEqual(new[] { "quake", "tornado", "flashstorm", "hail", "flood" }) && AfterStormMath.IsKind("hail") && !AfterStormMath.IsKind("rain"),
            "five disasters, in the annals' order");
        C(AfterStormMath.DelayHours("flashstorm") == 2 && AfterStormMath.DelayHours("flood") == 3 && AfterStormMath.DelayHours("tornado") == 1, "the party sets out an hour or so after it ends");
        C(AfterStormMath.Due(100, 100) && !AfterStormMath.Due(101, 100) && !AfterStormMath.TooLate(0, 48 * 2500) && AfterStormMath.TooLate(0, 48 * 2500 + 1),
            "due once its time comes, let go two days after");
        string w = AfterStormMath.Add("", "tornado", 500);
        w = AfterStormMath.Add(w, "hail", 900);
        w = AfterStormMath.Add(w, "tornado", 700);
        var waiting = AfterStormMath.Unpack(w);
        C(waiting.Count == 2 && waiting[0] == ("hail", 900) && waiting[1] == ("tornado", 700), "one party of a kind waits at a time, the newest due kept (" + w + ")");
        C(AfterStormMath.Unpack(AfterStormMath.Remove(w, "hail")).Single().kind == "tornado" && AfterStormMath.Unpack("rain@5;x;hail@y;flood@7").Single() == ("flood", 7),
            "taken off when set going; a damaged or unknown entry dropped");
        string many = "";
        foreach (var k in new[] { "quake", "tornado", "flood", "hail", "flashstorm" }) many = AfterStormMath.Add(many, k, 1);
        C(AfterStormMath.Unpack(many).Count == AfterStormMath.MaxWaiting && AfterStormMath.Unpack(many)[0].kind == "tornado", "four wait at most, the oldest let go");
        string counts = AfterStormMath.Count("", 5, "quake");
        counts = AfterStormMath.Count(counts, 5, "quake");
        counts = AfterStormMath.Count(counts, 5, "hail");
        counts = AfterStormMath.Count(counts, 6, "flood");
        C(AfterStormMath.CountsFor(counts, 5)["quake"] == 2 && AfterStormMath.CountsFor(counts, 6)["flood"] == 1 && !AfterStormMath.CountsFor(counts, 6).ContainsKey("quake"),
            "the year's count, each year its own (" + counts + ")");
        counts = AfterStormMath.Count(counts, 7, "tornado");
        C(AfterStormMath.CountsFor(counts, 5).Count == 0 && AfterStormMath.CountsFor(counts, 6).Count == 1 && AfterStormMath.CountsFor(counts, 7).Count == 1,
            "only this year and the one before are kept");
        C(AfterStormMath.Annals(AfterStormMath.Count(AfterStormMath.Count("", 3, "hail"), 3, "quake"), 3).Select(x => x.kind).SequenceEqual(new[] { "quake", "hail" }),
            "the annals in their order");
        foreach (var k in AfterStormMath.Kinds)
        {
            C(keyed.ContainsKey("CC_Annals_After_" + k) && keyed.ContainsKey("CC_Annals_After_" + k + "_Once"), k + " has its annals words");
            C(k == "quake" ? keyed.ContainsKey("CC_Weather_QuakeParty") && keyed.ContainsKey("CC_Weather_QuakeChronicle")
                : keyed.ContainsKey("CC_After_Party_" + k) && keyed.ContainsKey("CC_After_Chronicle_" + k) && keyed["CC_After_Chronicle_" + k].Contains("{PAWN_nameDef}"),
                k + " has its party's name and its chronicle line");
        }
        string after = Read("Weather/AfterStorm.cs");
        C(Read("Weather/WeatherBook.cs").Contains("AfterStorm.Hourly(map, this, year)") && !Read("Weather/WeatherDisasters.cs").Contains("QuakeOver"),
            "the weather record's hour looks after the disasters (the old quake party folded in)");
        C(Read("PlayOut/OnMapPlays.cs").Contains("AfterStorm.Over(p, what, happened)") && after.Contains("p.afterward = AfterPrefix + kind") && after.Contains("p.tally <= 0"),
            "the chronicle keeps it only when the party did some of the work");
        C(after.Contains("\"SeasonalFlood\"") && after.Contains("\"VEE_TidalFlooding\"") && after.Contains("\"VEE_Earthquake\"") && after.Contains("\"BurnedTree\"") && after.Contains("FiresAtHome(map) > 0"),
            "Odyssey's flood, the landmarks' tidal flooding, the events' earthquake by name; the burnt trees; the fires out first");
        C(after.Contains("class Patch_AfterStorm_TryExecute") && after.Contains("RimfolkPatches.On"), "the events' earthquake noted with the deeper hooks");

        // ================= C20: the advisor remembers last winter =================

        WeatherDay Day(int day, float min, float max, int cover = 0)
        {
            var d = new WeatherDay { day = day, year = day / 60, dayOfYear = day % 60, quadrum = day % 60 / 15 };
            for (int h = 0; h < 24; h++) WeatherMath.AddHour(d, false, false, false, false, h < 12 ? min : max, h < cover);
            return d;
        }
        var frozen = Day(1, -12f, -2f, 20);
        var chilly = Day(2, 0f, 8f);
        var mild = Day(3, 6f, 14f);
        C(frozen.Cold && frozen.Frozen && frozen.Covered && chilly.Cold && !chilly.Frozen && !chilly.Covered && !mild.Cold, "a cold day (below 6 °C on average), a frozen one, one with the snow lying");
        var q = new WeatherQuarter { counted = true };
        var list = new List<WeatherDay>();
        foreach (var d in new[] { frozen, chilly, mild })
        {
            list.Add(d);
            WeatherMath.AddDay(q, d, list);
        }
        C(q.coldDays == 2 && q.frozenDays == 1 && q.coverDays == 1, "a season counts its cold, frozen and snowy days");
        var back = WeatherMath.UnpackQuarter(WeatherMath.Pack(q));
        C(back != null && back.counted && back.coldDays == 2 && back.frozenDays == 1 && back.coverDays == 1, "they're kept in the save");
        var old = WeatherMath.UnpackQuarter("4,3,4,15,2,9,1,0,0,-20,3,-120,-110,2,1,cold");
        C(old != null && !old.counted && old.coldDays == 0 && old.name == "cold", "a season kept before is read, but not as a counted winter");
        C(WeatherMath.UnpackDay(WeatherMath.Pack(frozen)).cover == 20 && WeatherMath.UnpackDay("1,0,0,1,24,0,0,0,0,-5,3,1").cover == 0, "a day keeps its snow, an older day has none");
        WeatherQuarter Q(int year, int quadrum, int days, int cold, int froze, int snow, float coldest, bool counted = true) =>
            new WeatherQuarter { year = year, quadrum = quadrum, days = days, coldDays = cold, frozenDays = froze, coverDays = snow, coldest = coldest, counted = counted };
        var kept = new List<WeatherQuarter>
        {
            Q(4, 1, 15, 0, 0, 0, 10f), Q(4, 2, 15, 3, 0, 0, 2f), Q(4, 3, 15, 15, 11, 9, -24f), Q(5, 0, 15, 4, 0, 0, 1f), Q(5, 1, 15, 0, 0, 0, 12f), Q(5, 2, 8, 2, 0, 0, 0f)
        };
        var lw = WeatherMath.WinterBefore(kept, 5, 2);
        C(lw != null && lw.coldDays == 22 && lw.frozenDays == 11 && lw.coverDays == 9 && Math.Abs(lw.coldest + 24f) < 0.01f && lw.days == 60,
            "last winter: the four seasons before this one, the cold, the frost, the snow and the coldest night");
        C(WeatherMath.WinterBefore(kept, 4, 2) == null, "a winter the record saw too little of isn't spoken of");
        kept[2].counted = false;
        var partly = WeatherMath.WinterBefore(kept, 5, 2);
        C(partly != null && partly.coldDays == 7, "a season kept before the cold was counted is left out");
        C(WeatherMath.WinterBefore(new List<WeatherQuarter> { Q(1, 0, 15, 0, 0, 0, 9f), Q(1, 1, 15, 0, 0, 0, 9f) }, 1, 2) == null, "no cold at all: nothing to say");
        C(WeatherMath.DaysOf(100f, 9f) == 11 && WeatherMath.DaysOf(10f, 0f) == 0 && WeatherMath.DaysOf(0f, 3f) == 0, "the days the stores last, whole days");
        string lastWinter = Read("Advisor/AdvisorLastWinter.cs");
        C(Read("Advisor/AdvisorSignals5.cs").Contains("AdvisorLastWinter.Firewood(map, have, perDay)") && Read("Advisor/AdvisorSignals5.cs").Contains("AdvisorLastWinter.Stores(map)")
          && Read("Advisor/AdvisorSignals_YearFour.cs").Contains("AdvisorLastWinter.Feed(map, days)") && Read("Advisor/AdvisorSignals_YearFour.cs").Contains("AdvisorLastWinter.HerdCold(map)"),
            "the firewood, winter stores, herd feed and herd cold notes read last winter");
        foreach (var k in Regex.Matches(lastWinter, @"""(CC_Adv_LastWinter_\w+)""").Cast<Match>().Select(m => m.Groups[1].Value).Distinct())
            C(keyed.ContainsKey(k) && keyed[k].StartsWith("Last winter"), k + " says last winter in the colony's terms");

        // ================= C21: the autumn count =================

        C(HerdStoryMath.CountDue(true, 5, 4, 4, 9) && !HerdStoryMath.CountDue(true, 5, 5, 4, 9) && !HerdStoryMath.CountDue(false, 5, 4, 9, 9)
          && !HerdStoryMath.CountDue(true, 5, 4, 3, 9) && !HerdStoryMath.CountDue(true, 5, 4, 6, 7) && !HerdStoryMath.CountDue(true, 5, 4, 6, 15),
            "once each autumn, with four head or more, in the count's hours");
        C(HerdStoryMath.Yearling(0.5f, 1) && !HerdStoryMath.Yearling(0.05f, 0) && !HerdStoryMath.Yearling(1.2f, 2), "a yearling: past its first stage, not yet a year old");
        C(HerdStoryMath.SlaughterCount(12, 40f, 20f) == 1 && HerdStoryMath.SlaughterCount(12, 10f, 20f) == 4 && HerdStoryMath.SlaughterCount(6, 1f, 20f) == 2
          && HerdStoryMath.SlaughterCount(4, 1f, 20f) == 1 && HerdStoryMath.SlaughterCount(0, 1f, 20f) == 0 && HerdStoryMath.SlaughterCount(9, 30f, 0f) == 1,
            "the oldest to the butcher: one when the hay lasts, the share it falls short by when it doesn't, a third of the herd and four at most");
        C(HerdStoryMath.Against(12, 10, 4, 5) == "more" && HerdStoryMath.Against(8, 10, 4, 5) == "fewer" && HerdStoryMath.Against(10, 10, 4, 5) == "same"
          && HerdStoryMath.Against(10, 10, 3, 5) == null && HerdStoryMath.Against(10, 0, 4, 5) == null, "against last autumn's count, only when there was one");
        var dlm = Def("ColonyCouncil.DilemmaDef", "Rimfolk_Dlm_HerdCount");
        var texts = dlm?.Element("texts")?.Elements("li").Select(l => l.Value).ToList() ?? new List<string>();
        var options = dlm?.Element("options")?.Elements("li").ToList() ?? new List<XElement>();
        C(dlm != null && dlm.Element("workerClass")?.Value == "ColonyCouncil.DilemmaWorker_HerdCount" && texts.Count >= 4 && options.Count == 3,
            "the council's question: its worker, texts and three answers");
        C(texts.Any(t => t.StartsWith("[lastwinter]") && t.Contains("{WINTER} days")) && texts.Any(t => t.StartsWith("[!lastwinter]")) && texts.All(t => t.Contains("{HEAD}")),
            "the hay set against last winter's cold, or the winter ahead when the record has none");
        string Effects(XElement o) => string.Join(",", o.Element("effects")?.Elements("li").Select(e => (string)e.Attribute("Class")) ?? Enumerable.Empty<string>());
        C(Effects(options[1]).Contains("ColonyCouncil.DilemmaEffect_HerdSell") && Effects(options[2]).Contains("ColonyCouncil.DilemmaEffect_HerdOldest") && !Effects(options[0]).Contains("Herd"),
            "keep them all; the yearlings to the next traders; the oldest to the butcher");
        var placeholders = texts.Concat(options.Select(o => o.Element("label")?.Value + " " + o.Element("outcome")?.Value))
            .SelectMany(t => Regex.Matches(t, @"\{([A-Z]+)").Cast<Match>().Select(m => m.Groups[1].Value)).Distinct().ToList();
        C(placeholders.All(p => new[] { "A", "B", "HEAD", "FEED", "WINTER", "COUNT", "YEARLINGS", "LEADER" }.Contains(p)), "only what the question gives (" + string.Join(", ", placeholders) + ")");
        string count = Read("Animals/HerdCount.cs");
        C(Read("Animals/AnimalLife.cs").Contains("HerdCount.Hourly(comp.HerdOf(m), m)") && Read("PlayOut/OnMapPlays.cs").Contains("HerdCount.Counted(p, happened)")
          && Read("PlayOut/OnMapPlays.cs").Contains("p.mode == Herd") && count.Contains("herd = true"),
            "the count is a round of the herd, its end kept by the herd's story");
        C(Read("Trade/TradeAdvice.cs").Contains("HerdCount.ForSale(a)") && count.Contains("LordJob_TradeWithColony") && count.Contains("new DilemmaEffect_Visit"),
            "the yearlings marked to sell in the trade window, and the counter goes down to the pens when a caravan stops");
        C(count.Contains("a != leader") && count.Contains("PetTracker.PersonOf(a) == null") && count.Contains("HediffDefOf.Pregnant"),
            "never the leader, anyone's own, bonded, named or carrying young");

        // ================= C22 and C28: the animals at the line, the lines in today's weather =================

        var fought = DefenseLineMath.Lessons(new DefenseLineMath.RaidTally { through = 5, animalsFought = 3, animalsBrought = 4 });
        C(fought.SequenceEqual(new[] { DefenseLineMath.Lesson.Held, DefenseLineMath.Lesson.AnimalsHeld }), "the animals held: a note beside the lesson (" + string.Join(",", fought) + ")");
        var fell = DefenseLineMath.Lessons(new DefenseLineMath.RaidTally { through = 5, animalsFought = 3, animalsBrought = 1, animalsDowned = 3 });
        C(fell.Count == 1 && fell[0] == DefenseLineMath.Lesson.AnimalsBack, "three animals downed at the line: keep them back");
        C(DefenseLineMath.Lessons(new DefenseLineMath.RaidTally { through = 5, animalsFought = 1, animalsDowned = 1 }).Contains(DefenseLineMath.Lesson.AnimalsBack)
          && !DefenseLineMath.Lessons(new DefenseLineMath.RaidTally { through = 5, animalsFought = 2, animalsBrought = 2, animalsDowned = 1 }).Contains(DefenseLineMath.Lesson.AnimalsBack),
            "one downed that brought nobody down is a lesson; one downed among those that held is not");
        var fog = DefenseLineMath.Lessons(new DefenseLineMath.RaidTally { roundRight = 1, broke = 4, downedFront = 2, inside = 3, downedCoverRight = 1, aim = 50 });
        C(fog.Count == 4 && fog.Last() == DefenseLineMath.Lesson.Weather, "the weather a note beside the three lessons, never in their place");
        C(DefenseLineMath.AimPercent(0.5f) == 50 && DefenseLineMath.AimPercent(1.3f) == 100 && DefenseLineMath.AimLost(80) && DefenseLineMath.AimLost(90) && !DefenseLineMath.AimLost(91),
            "the weather's aim as the game has it, worth a word from a tenth lost");
        C(new DefenseLineMath.RaidTally().aim == 100 && DefenseLineMath.Lessons(new DefenseLineMath.RaidTally { through = 3 }).Single() == DefenseLineMath.Lesson.Held,
            "a raid in clear weather says nothing of it");
        C(DefenseLineMath.Brought(2, 4, 1, 2) == 2 && DefenseLineMath.Brought(0, 1, 0, 3) == 3 && DefenseLineMath.Brought(5, 5, 2, 2) == 0, "brought down: the greater of its downings and kills");
        C(ArtTalesMath.Held(new[] { "Held", "AnimalsHeld", "Weather" }) && ArtTalesMath.Held(new[] { "Held" }) && !ArtTalesMath.Held(new[] { "Held", "AnimalsBack" }) && !ArtTalesMath.Held(new[] { "Weather" }),
            "a line held keeps its tale beside the notes");
        string watch = Read("Defense/DefenseLinesWatch.cs");
        C(watch.Contains("RecordDefOf.PawnsDowned") && watch.Contains("RecordDefOf.Kills") && watch.Contains("CurWeatherAccuracyMultiplier") && watch.Contains("AnimalTempers.Noted(a, \"CC_DefLine_AnimalNoted\"")
          && watch.Contains("\"Rimfolk_Journal_AnimalAtLine\"") && watch.Contains("lastHarmTick >= raidStart"),
            "the animals followed by their own records, the weather by the game's own factor; the card and the journal, unless the fight came to their person");
        C(Read("UI/DefenseLinesPanel.cs").Contains("y = Weather(r, x, y, w, draw);"), "the line's card says what today's weather does");
        foreach (var k in new[] { "AnimalNoted", "Raid_Animals", "Raid_AnimalsFell", "TheAnimals", "TheKind", "Lesson_AnimalsBack", "Lesson_AnimalsHeld", "Lesson_Weather", "WeatherRaid", "WeatherNow" })
            C(keyed.ContainsKey("CC_DefLine_" + k), "CC_DefLine_" + k + " is in the words");
        C(keyed["CC_DefLine_WeatherNow"].Contains("open sky") && keyed["CC_DefLine_WeatherNow"].Contains("alike") && keyed["CC_DefLine_WeatherNow"].Contains("melee"),
            "the weather said as the game has it: shots under the open sky, both sides, not melee");

        // ================= C24: a cup for the sick =================

        foreach (var a in SickCupMath.Ailments) C(Def("HediffDef", a) != null, a + " is one of Rimfolk's ailments");
        foreach (var d in SickCupMath.Drinks) C(active.Contains(d), d + " is in the player's active mods");
        C(SickCupMath.KindOf("VBE_MintTea") == "tea" && SickCupMath.KindOf("VCE_HotCocoa") == "cocoa" && SickCupMath.KindOf("RB_BaseHoney") == "honey" && SickCupMath.KindOf("VFEM2_Honey") == "honey"
          && SickCupMath.Drinks.All(d => SickCupMath.KindOf(d) != null), "each drink's kind, for the words said with it");
        C(Math.Abs(SickCupMath.Eased(0.5f) - 0.46f) < 0.001f && SickCupMath.Eased(0.05f) == 0.05f && SickCupMath.Eased(0.061f) >= 0.02f, "a little off the ailment; one almost gone left to go");
        C(SickCupMath.Due(-1, 0) && !SickCupMath.Due(0, 59999) && SickCupMath.Due(0, 60000), "one cup a day each");
        string cup = Read("Immersion/SickCup.cs");
        C(cup.Contains("FirstPot.WillDrink(sick, d)") && cup.Contains("SmallHabits.CanListen(o)") && cup.Contains("Together.Can(by, sick)") && cup.Contains("JobDefOf.Ingest"),
            "never against a policy, a faith or a teetotaller; the bringer at their leisure; handed over side by side and drunk there");
        C(Read("CouncilGameComponent.cs").Contains("SickCup.Hourly") && Def("JobDef", "Rimfolk_SickCup")?.Element("driverClass")?.Value == "ColonyCouncil.JobDriver_SickCup"
          && Def("ThoughtDef", "Rimfolk_SickCup_Got") != null && Def("ThoughtDef", "Rimfolk_SickCup_Gave") != null,
            "the hour, the job and the two thank-yous");

        // ================= The words the code names, and the dev actions =================

        foreach (var file in new[] { "Weather/AfterStorm.cs", "Animals/HerdCount.cs", "Immersion/SickCup.cs", "Defense/DefenseLinesWatch.cs", "Advisor/AdvisorLastWinter.cs", "Weather/WeatherBook.cs" })
            foreach (var k in Regex.Matches(Read(file), @"""(CC_[A-Za-z0-9_]+)""").Cast<Match>().Select(m => m.Groups[1].Value).Distinct())
                C(keyed.ContainsKey(k) || k.EndsWith("_"), k + " (" + file + ") is in the words");
        string dev = Read("Weather/LandLifeDebug.cs");
        C(new[] { "AfterStorm.DevNow", "DevLastWinter", "HerdCount.Start(h, map, true)", "HerdCount.DevAsk", "HerdCount.DevTraders", "DevRaidNotes", "SickCup.TrySend" }.All(dev.Contains),
            "a dev action for each");
        return n;
    }
}
