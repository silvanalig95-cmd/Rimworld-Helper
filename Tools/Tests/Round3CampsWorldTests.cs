using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The content round's third part, its camps and world events (docs/content-round-3-camps-world.md, R3-C1 to R3-W6;
/// Camps/CampRound3.cs, World/Variety/WorldRound3.cs, Defs/CampDefs/Rimfolk_CampKinds_Round3.xml,
/// Defs/WorldSituationDefs/Rimfolk_WS_Round3.xml): the pure rules (Round3WorldMath), and the defs, words and seams of each.
/// </summary>
public static class Round3CampsWorldTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "round 3 camps and world events: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f)).ToList();
        XElement Def(string type, string name) =>
            defs.SelectMany(d => d.Root.Elements()).FirstOrDefault(e => (e.Name.LocalName == type || e.Name.LocalName == "ColonyCouncil." + type) && (string)e.Element("defName") == name);
        List<string> Tags(string line)
        {
            var m = Regex.Match(line ?? "", @"^\[([^\]]*)\]");
            return m.Success ? m.Groups[1].Value.Split(',').Select(t => t.Trim()).ToList() : new List<string>();
        }
        List<string> Lines(XElement e, string list) => e?.Element(list)?.Elements("li").Select(li => li.Value).ToList() ?? new List<string>();
        string camps = Code("Camps/CampRound3.cs"), world = Code("World/Variety/WorldRound3.cs");
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Round3_CampsWorld.xml"));
        var ages = new[] { "tribal", "medieval", "industrial", "spacer" };

        // ================= The pure rules =================

        // R3-C1.
        C(!Round3WorldMath.Rained(1f, false) && Round3WorldMath.Rained(Round3WorldMath.RainMark, true) && !Round3WorldMath.Rained(0.1f, true),
            "R3-C1: the rain counts only after their first rite, and only as rain");
        C(Round3WorldMath.RiteHour(19) && Round3WorldMath.RiteHour(21) && !Round3WorldMath.RiteHour(18) && !Round3WorldMath.RiteHour(22), "R3-C1: the rite is kept in the evening");
        C(Round3WorldMath.RainFee(0f) == 40 && Round3WorldMath.RainFee(1f) == 80 && Round3WorldMath.RainFee(100f) == 300 && Round3WorldMath.RainFee(1.33f) % 10 == 0,
            "R3-C1: the fee by the colony's wealth, in tens, 40 to 300");
        C(!Round3WorldMath.SendsUp("tribal") && !Round3WorldMath.SendsUp("medieval") && Round3WorldMath.SendsUp("industrial") && Round3WorldMath.SendsUp("spacer"),
            "R3-C1: the old ages dance, the later ones send something up");
        // R3-C2.
        foreach (var age in ages)
        {
            var palette = Round3WorldMath.Palette(age);
            C(palette.Length >= 2 && palette.All(k => keyed.Contains("<CC_R3Dye_Colour_" + k + ">")), $"R3-C2: the {age} dyers' colours are named");
            C(palette.All(k => Round3WorldMath.Rgb(k).Length == 3 && Round3WorldMath.Rgb(k).All(v => v >= 0f && v <= 1f)), $"R3-C2: the {age} dyers' colours are colours");
            C(Enumerable.Range(0, 11).Select(i => Round3WorldMath.ColourOf(age, i / 10f)).All(palette.Contains), $"R3-C2: the {age} camp's colour is one of their age's");
        }
        C(Round3WorldMath.Palette("tribal").Contains("woad") && Round3WorldMath.Palette("industrial").Contains("aniline") && !Round3WorldMath.Palette("medieval").Contains("aniline"),
            "R3-C2: woad before the mills, aniline after");
        // R3-C3.
        C(Round3WorldMath.MarkOf(true, true, true, true, true) == Round3WorldMath.Mark.Psylink && Round3WorldMath.MarkOf(false, true, true, true, true) == Round3WorldMath.Mark.Xenotype
          && Round3WorldMath.MarkOf(false, false, true, true, true) == Round3WorldMath.Mark.Sensitive && Round3WorldMath.MarkOf(false, false, false, true, true) == Round3WorldMath.Mark.Healer
          && Round3WorldMath.MarkOf(false, false, false, true, false) == Round3WorldMath.Mark.None, "R3-C3: whom they suspect, strongest first; the healer only in an age of herbs");
        C(Round3WorldMath.ClearedChance(0) == 0.45f && Round3WorldMath.ClearedChance(20) == 0.9f && Round3WorldMath.ClearedChance(10) > Round3WorldMath.ClearedChance(5),
            "R3-C3: a good talker is likelier to be cleared, 45% to 90%");
        C(Round3WorldMath.ClearedGoodwill > 0 && Round3WorldMath.NamedGoodwill < 0, "R3-C3: their people warmer when the suspect is cleared, cooler when they name a witch");
        // R3-C4.
        C(Round3WorldMath.RagPrice(0) == 0 && Round3WorldMath.RagPrice(1) == 10 && Round3WorldMath.RagPrice(10) == 40 && Round3WorldMath.MaxRags >= 10, "R3-C4: the price of the rags, in tens");
        // R3-C5.
        C(Round3WorldMath.Gamble(0, Round3WorldMath.SharpEye, 0f, 0f) == Round3WorldMath.Play.Caught && Round3WorldMath.Gamble(0, Round3WorldMath.SharpEye - 1, 0f, 0f) == Round3WorldMath.Play.Won
          && Round3WorldMath.Gamble(20, 0, 0f, 0.99f) == Round3WorldMath.Play.Lost, "R3-C5: a sharp eye catches the dice, else won or lost by the roll");
        C(Round3WorldMath.WinChance(0) == 0.3f && Round3WorldMath.WinChance(20) == 0.6f && Round3WorldMath.WinChance(20) < 0.75f, "R3-C5: against loaded dice the colony wins less than half the time, at best 60%");
        C(Round3WorldMath.Back(Round3WorldMath.Play.Won, 40) == 80 && Round3WorldMath.Back(Round3WorldMath.Play.Caught, 40) == 40 && Round3WorldMath.Back(Round3WorldMath.Play.Lost, 40) == 0,
            "R3-C5: won, twice the stake back; caught, the stake back; lost, nothing");
        // The world events.
        C(Round3WorldMath.Returns(0f) && !Round3WorldMath.Returns(0.99f), "R3-W3: an expedition comes back at a chance, settled at its start");
        C(Round3WorldMath.FreedChance(true) > Round3WorldMath.FreedChance(false) && Round3WorldMath.RansomShare(0) == 30 && Round3WorldMath.RansomShare(1000000) == 150
          && Round3WorldMath.RansomShare(2000) == 50, "R3-W4: ransomed likelier if the colony gave; its share in tens, 30 to 150");
        C(Enumerable.Range(0, 11).Select(i => Round3WorldMath.Rats(i / 10f)).All(r => r >= 4 && r <= 8), "R3-W5: four to eight rats");
        C(Round3WorldMath.SkillFor("tribal") == "Crafting" && Round3WorldMath.SkillFor("medieval") == "Plants" && Round3WorldMath.SkillFor("industrial") == "Plants"
          && Round3WorldMath.SkillFor("spacer") == "Crafting" && Round3WorldMath.SkillFlag("Plants") == "growing" && Round3WorldMath.SkillFlag("Crafting") == "crafting",
            "R3-W6: the craft of the new way by the age of the people who bring it");

        // ================= The camps =================

        var kinds = new Dictionary<string, string>
        {
            ["Rimfolk_Camp_RainCallers"] = "CampWorker_R3RainCallers", ["Rimfolk_Camp_Dyers"] = "CampWorker_R3Dyers", ["Rimfolk_Camp_WitchFinders"] = "CampWorker_R3WitchFinders",
            ["Rimfolk_Camp_RagPickers"] = "CampWorker_R3RagPickers", ["Rimfolk_Camp_Gamblers"] = "CampWorker_R3Gamblers", ["Rimfolk_Camp_Kennel"] = "CampWorker_R3Kennel"
        };
        foreach (var (k, worker) in kinds)
        {
            var d = Def("CampKindDef", k);
            C(d != null && (string)d.Element("workerClass") == "ColonyCouncil." + worker && camps.Contains("class " + worker + " : CampWorker"), $"{k} is a camp kind with ways of its own ({worker})");
            if (d == null) continue;
            C(Lines(d, "texts").Count >= 5 && Lines(d, "lines").Count >= 5 && Lines(d, "farewell").Count >= 3 && Lines(d, "settled").Count >= 1 && Lines(d, "joinTexts").Count >= 1,
                $"{k} has its words");
            foreach (var age in ages)
                C(Lines(d, "texts").Any(l => Tags(l).Contains("their:" + age)), $"{k} comes in the {age} age's form");
            foreach (var l in Lines(d, "lines").Concat(Lines(d, "momentLines")))
                C(Regex.Matches(Regex.Replace(l, @"^\[[^\]]*\]\s*", ""), @"[.?!]\s+\S").Count == 0, $"{k}'s bubble is one sentence: {l}");
            // The arrival letter is written before the camp's own placeholders are known: none of them there.
            C(!Lines(d, "texts").Concat(Lines(d, "titles")).Any(l => Regex.IsMatch(l, @"\{(SILVER|KIND|WHO|WHY)\}")), $"{k}'s arrival letter uses only the camps' common words");
            C(camps.Contains("\"" + k + "\""), $"{k} can be brought from the dev action (CampRound3.Kinds)");
            // A kind's own state holds no saved collection (Tools/save_estimate.py would need a model for one).
        }
        C(!Regex.IsMatch(camps, @"Scribe_Collections") && !Regex.IsMatch(world, @"Scribe_Collections"), "nothing new kept in a saved collection: each camp's state is a few values, each world event's its flags");
        C(world.Contains("CampRound3.Kinds") && world.Contains("[DebugAction(\"Rimfolk\", \"Round 3:"), "the dev actions bring each camp and start each world event");
        // R3-C1.
        var rain = Def("CampKindDef", "Rimfolk_Camp_RainCallers");
        C(new[] { "drought", "hunger" }.All(Lines(rain, "situationKinds").Contains) && camps.Contains("GenLocalDate.Season(map) == Season.Summer"), "R3-C1: in a drought or a hungry year, else in summer");
        C(camps.Contains("curWeather?.rainRate") && !camps.Contains("TransitionTo") && !camps.Contains("weatherManager.curWeather =") && !world.Contains("TransitionTo"),
            "R3-C1: the rain is the game's own weather, read and never made");
        C(camps.Contains("\"Rimfolk_R2_DanceInYard\"") && camps.Contains("\"Rimfolk_R3_CampSkyWork\"") && camps.Contains("FleckMaker.ThrowSmoke"), "R3-C1: they dance round the fire, or send something up that goes off over their camp");
        C(camps.Contains("new DilemmaEffect_CarryOut { what = \"silver\", count = st.fee, to = PlaySpot.Camp"), "R3-C1: the promised fee is carried out to their camp by someone of the colony");
        C(rain?.Element("asks")?.Elements("li").Any(a => (string)a.Element("key") == "fee" && (string)a.Element("theyAsk") == "true" && a.Element("silver") == null) == true,
            "R3-C1: the fee is asked as a promise, nothing paid when it is given");
        C(camps.Contains("Gods.ByKey(\"Cirrus\")") && keyed.Contains("<CC_R3Rain_SkyLine>"), "R3-C1: the sky's believers have their say, the god unnamed");
        // R3-C2.
        C(camps.Contains("outer.SetColor(ColourOf(st), false)") && camps.Contains("SetAllGraphicsDirty"), "R3-C2: the clothes take the camp's colour where they are worn");
        C(Def("CampKindDef", "Rimfolk_Camp_Dyers")?.Descendants("li").Any(li => (string)li.Attribute("Class") == "ColonyCouncil.DilemmaEffect_Visit") == true && camps.Contains("Together.Can"),
            "R3-C2: the one sent walks to their camp, and is dyed once with them");
        // R3-C3.
        C(camps.Contains("GetPsylinkLevel()") && camps.Contains("XenotypeDefOf.Baseliner") && camps.Contains("StatDefOf.PsychicSensitivity") && camps.Contains("SkillDefOf.Medicine"),
            "R3-C3: they look for a psychic link, a people not baseline, the psychically sensitive, the healer");
        C(camps.Contains("new DilemmaEffect_Visit { who = PlayWho.A, to = PlaySpot.Camp") && camps.Contains("Round3WorldMath.ClearedChance(social)"),
            "R3-C3: the suspect walks to their fire, and their way with words weighs the verdict");
        C(Def("ThoughtDef", "Rimfolk_R3_NamedWitch") != null && Def("ThoughtDef", "Rimfolk_R3_Questioned") != null, "R3-C3: the suspect feels it");
        var witch = Def("CampKindDef", "Rimfolk_Camp_WitchFinders");
        C(witch?.Element("asks")?.Elements("li").Any(a => (string)a.Element("key") == "question" && float.Parse((string)a.Element("refuseTension") ?? "0") >= 10f) == true
          && !camps.Contains("Harm.Roll"), "R3-C3: refused, their unease rises (any blows only by the camps' own rule)");
        // R3-C4.
        C(camps.Contains("what = PlayCarry.Scrap") && camps.Contains("c.StartBring(ThingDefOf.Silver, price)"), "R3-C4: the worn-out things carried out; the price brought to the gate");
        // R3-C5.
        C(camps.Contains("Round3WorldMath.Gamble(social, sharp, Rand.Value, Rand.Value)") && camps.Contains("c.AddTension(Round3WorldMath.CaughtTension)") && camps.Contains("Gods.ByKey(\"Ducat\")"),
            "R3-C5: the evening settled by the roll; caught dice, their unease up; the ledger's believers speak");
        var dice = Def("CampKindDef", "Rimfolk_Camp_Gamblers");
        C(dice?.Element("asks")?.Elements("li").Any(a => (string)a.Element("needs") == "evening" && a.Element("silver") != null) == true, "R3-C5: the stake is the ask's silver, played in the evening");
        // R3-C6.
        var kennel = Def("CampKindDef", "Rimfolk_Camp_Kennel");
        C(camps.Contains("c.HandOverBeast()") && camps.Contains("new DilemmaEffect_Follow"), "R3-C6: the dog bought is the colony's where it stands, and keeps at someone's heels");
        C(kennel?.Element("animals")?.Elements("li").Sum(a => int.Parse(((string)a.Element("count") ?? "1~1").Split('~').Last())) <= 5
          && kennel.Descendants("li").Any(li => li.Value == "AEXP_GermanShepherd" && (string)li.Attribute("MayRequire") == "vanillaexpanded.vanillaanimalsexpanded")
          && kennel.Descendants("li").Any(li => li.Value == "Husky"), "R3-C6: their dogs from the player's mods first, softly, the game's after, five at most");
        C(kennel?.Element("leaveBehind")?.Elements("li").Any(g => (string)g.Element("young") == "true" && (string)g.Element("at") == "gate") == true, "R3-C6: a pup at the gate after a good stay");

        // ================= The world events =================

        var events = new Dictionary<string, string>
        {
            ["Rimfolk_WS_MineFall"] = "SituationWorker_R3MineFall", ["Rimfolk_WS_FouledWells"] = "SituationWorker_R3Wells", ["Rimfolk_WS_Expedition"] = "SituationWorker_R3Expedition",
            ["Rimfolk_WS_Ransom"] = "SituationWorker_R3Ransom", ["Rimfolk_WS_Rats"] = "SituationWorker_R3Rats", ["Rimfolk_WS_NewWay"] = "SituationWorker_R3NewWay"
        };
        var kindsSeen = new HashSet<string>(defs.SelectMany(d => d.Root.Elements("ColonyCouncil.WorldSituationDef")).Where(e => !events.ContainsKey((string)e.Element("defName")))
            .Select(e => (string)e.Element("kind")).Where(k => k != null));
        foreach (var (e, worker) in events)
        {
            var d = Def("WorldSituationDef", e);
            C(d != null && (string)d.Element("workerClass") == "ColonyCouncil." + worker && world.Contains("class " + worker + " : SituationWorker_Variety"),
                $"{e} is a world event of its people's age ({worker})");
            if (d == null) continue;
            C(Lines(d, "texts").Count >= 4 && Lines(d, "titles").Count >= 2 && Lines(d, "summaries").Count >= 1 && d.Element("endings")?.Elements("li").Count() >= 2,
                $"{e} has its letters, titles, what it means and its endings");
            C(kindsSeen.Add((string)d.Element("kind")), $"{e} has a kind of its own, not one of the region's others");
            foreach (var t in VarietyMath.TheirAgeTags)
            {
                C(Lines(d, "names").Any(l => Tags(l).Contains(t)), $"{e} has a name in the {t} age's form");
                C(Lines(d, "texts").Any(l => Tags(l).Contains(t)), $"{e} has a letter in the {t} age's form");
            }
            foreach (var l in Lines(d, "theirs").Concat(Lines(d, "theirEnds")))
                C(WorldHomeMath.ForAPeople(Tags(l)), $"{e}'s own paragraph names a people: {l.Substring(0, Math.Min(40, l.Length))}");
            C(world.Contains("\"" + e + "\""), $"{e} can be started from the dev action");
            // No countdown in the words, and no god named.
            foreach (var l in d.Descendants("li").Where(x => !x.HasElements).Select(x => x.Value))
                C(!Regex.IsMatch(l, @"\bin (\d+|two|three|four|five|six|seven) days\b") && !Regex.IsMatch(l, @"\{GOD\}"), $"{e}: no countdown or god in '{l.Substring(0, Math.Min(40, l.Length))}'");
        }
        // R3-W1.
        var mine = Def("WorldSituationDef", "Rimfolk_WS_MineFall");
        C(mine?.Descendants("li").Any(li => (string)li.Attribute("Class") == "ColonyCouncil.WorldEffect_Away") == true
          && mine.Descendants("li").Any(li => (string)li.Attribute("Class") == "ColonyCouncil.DilemmaEffect_CarryOut" && (string)li.Element("what") == "wood")
          && mine.Descendants("li").Any(li => (string)li.Attribute("Class") == "ColonyCouncil.DilemmaEffect_CarryOut" && (string)li.Element("what") == "medicine"), "R3-W1: timber or medicine carried out, or the colony's miner sent for the digging");
        C(mine?.Element("endings")?.Elements("li").Any(x => (string)x.Element("minHelp") == "1") == true, "R3-W1: brought out likelier with the colony's help");
        // R3-W2.
        var wells = Def("WorldSituationDef", "Rimfolk_WS_FouledWells");
        C(world.Contains("protected override bool CanHappen(Map map) => FreshWater(map);") && wells?.Descendants("li").Any(li => (string)li.Attribute("Class") == "ColonyCouncil.VisitEffect_Come" && (string)li.Element("spot") == "Water") == true,
            "R3-W2: only with fresh water near the colony; the carriers stand at the colony's water");
        C(wells?.Element("beats")?.Elements("li").Any(b => (string)b.Element("key") == "carriers" && Lines(b, "requires").Contains("drawing") && (string)b.Element("maxTimes") == "3") == true,
            "R3-W2: let, the carriers come a few times");
        // R3-W3.
        C(world.Contains("Round3WorldMath.Returns(Rand.Value) ? \"willReturn\" : \"willBeLost\""), "R3-W3: whether it comes back settled at its start");
        var exp = Def("WorldSituationDef", "Rimfolk_WS_Expedition");
        C(exp?.Element("beats")?.Elements("li").Any(b => (string)b.Element("key") == "survivor" && Lines(b, "requires").Contains("willBeLost")) == true
          && exp.Element("endings").Elements("li").Any(x => (string)x.Element("key") == "back" && Lines(x, "requires").Contains("willReturn")), "R3-W3: shown by what follows: back, or one alone at the gate");
        // R3-W4.
        C(world.Contains("Round3WorldMath.FreedChance(s.Has(\"gave\"))") && world.Contains("Round3WorldMath.RansomShare("), "R3-W4: ransomed likelier if the colony gave; its share by its silver");
        var ransom = Def("WorldSituationDef", "Rimfolk_WS_Ransom");
        C(ransom?.Element("beats")?.Elements("li").Any(b => (string)b.Element("key") == "freed" && Lines(b, "requires").Contains("freed")) == true, "R3-W4: the freed walk home past the colony");
        // R3-W5.
        C(world.Contains("SeasonAndOutdoorTemperatureAcceptableFor") && world.Contains("JobDefOf.AttackMelee") && !world.Contains("Manhunter"), "R3-W5: the game's own rats, where they can live, never manhunting; the dog after them");
        var rats = Def("WorldSituationDef", "Rimfolk_WS_Rats");
        C(rats?.Descendants("li").Any(li => (string)li.Attribute("Class") == "ColonyCouncil.VisitEffect_AtGate" && (string)li.Element("pet") == "dog") == true, "R3-W5: the catcher comes with a dog");
        // R3-W6.
        var way = Def("WorldSituationDef", "Rimfolk_WS_NewWay");
        C(way?.Element("beats")?.Elements("li").Count(b => b.Descendants("li").Any(li => (string)li.Attribute("Class") == "ColonyCouncil.DilemmaEffect_Lesson")) == 2
          && world.Contains("Couriers.Hand(def, 1, map"), "R3-W6: a lesson at the gate in the craft of the age, or the drawings handed over");

        // ================= The words =================

        foreach (Match m in Regex.Matches(camps + world, "\"(CC_R3[A-Za-z_]*[A-Za-z])\""))
            C(keyed.Contains("<" + m.Groups[1].Value + ">"), $"{m.Groups[1].Value} has its words");
        foreach (var mark in Enum.GetNames(typeof(Round3WorldMath.Mark))) C(keyed.Contains("<CC_R3Witch_Mark_" + mark + ">"), $"R3-C3: what marks one in words ({mark})");
        C(File.ReadAllText(Path.Combine(root, "Tools", "Tests", "Tests.csproj")).Contains("World/Variety/Round3WorldMath.cs"), "the pure rules are linked into the tests");
        foreach (var k in kinds.Keys) C(Code("Text/Facts/FactsMath.cs").Contains("\"them:camp:" + k.Replace("Rimfolk_Camp_", "") + "\""), $"{k}: the facts service knows the camp");
        return n;
    }
}
