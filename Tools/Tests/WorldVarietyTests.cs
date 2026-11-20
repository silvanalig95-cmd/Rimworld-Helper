using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The content update's variety, the world (Source/ColonyCouncil/World/Variety/, Camps/CampVariety.cs, the singing in
/// Diplomacy/FeastContests.cs, the stud visit in Missions/Workers_MissionStud.cs; part B of
/// docs/content-update-proposals.md: B6, B7, B8, B15, B20, B21, B24, B28, and six world events and six camps more): the
/// pure rules (VarietyMath, FeastMath's hopes by people and the singing, the prices), and the defs, words and seams.
/// </summary>
public static class WorldVarietyTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the world's variety: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string Read(string rel) => File.ReadAllText(Path.Combine(root, rel));
        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f)).ToList();
        XElement Def(string type, string name) =>
            defs.SelectMany(d => d.Root.Elements()).FirstOrDefault(e => (e.Name.LocalName == type || e.Name.LocalName == "ColonyCouncil." + type) && (string)e.Element("defName") == name);
        IEnumerable<XElement> All(string type) => defs.SelectMany(d => d.Root.Elements()).Where(e => e.Name.LocalName == type || e.Name.LocalName == "ColonyCouncil." + type);
        List<string> Tags(string line)
        {
            var m = Regex.Match(line ?? "", @"^\[([^\]]*)\]");
            return m.Success ? m.Groups[1].Value.Split(',').Select(t => t.Trim()).ToList() : new List<string>();
        }
        List<string> Lines(XElement e, string list) => e?.Element(list)?.Elements("li").Select(li => li.Value).ToList() ?? new List<string>();
        string keyed = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));

        // ================= Their age, as a world event's lines are tagged =================

        C(VarietyMath.TheirAgeTag("tribal") == "theirTribal" && VarietyMath.TheirAgeTag("medieval") == "theirMedieval" && VarietyMath.TheirAgeTag("industrial") == "theirIndustrial"
          && VarietyMath.TheirAgeTag("spacer") == "theirSpacer" && VarietyMath.TheirAgeTag(null) == null && VarietyMath.TheirAgeTag("ultra") == null,
            "every age has its own tag for the world event's lines, nothing else does");
        foreach (var a in AgesMath.All) C(VarietyMath.TheirAgeTags.Contains(VarietyMath.TheirAgeTag(AgesMath.Tag(a))), $"the {a} age's tag is one of the known");

        // ================= The world events =================

        string[] events =
        {
            "Rimfolk_WS_ClothScarce", "Rimfolk_WS_TimberScarce", "Rimfolk_WS_Games", "Rimfolk_WS_Pardon", "Rimfolk_WS_HiringFair", "Rimfolk_WS_FoundingDay",
            "Rimfolk_WS_MudRoads", "Rimfolk_WS_FallingStars", "Rimfolk_WS_GreatBurying", "Rimfolk_WS_BridgeDown"
        };
        var kinds = new HashSet<string>();
        foreach (var e in events)
        {
            var d = Def("WorldSituationDef", e);
            C(d != null, $"{e} is a world event of the mod's");
            if (d == null) continue;
            C(((string)d.Element("workerClass") ?? "").StartsWith("ColonyCouncil.SituationWorker_") && Code("World/Variety/VarietyWorkers.cs").Contains("class " + ((string)d.Element("workerClass")).Substring(14) + " "),
                $"{e}'s worker is one of this round's (its people's age as a tag)");
            C(Lines(d, "texts").Count >= 2 && Lines(d, "titles").Count >= 2 && Lines(d, "summaries").Count >= 1 && d.Element("endings")?.Elements("li").Any() == true,
                $"{e} has its letters, titles, what it means and an ending");
            kinds.Add((string)d.Element("kind"));
            // A people's calling preferred: its own start and ending paragraphs.
            string calling = (string)d.Element("faction")?.Element("calling");
            if (!string.IsNullOrEmpty(calling))
            {
                C(Lines(d, "theirs").Any(l => Tags(l).Contains("their:" + calling)), $"{e} prefers the {calling} and has their start");
                C(Lines(d, "theirEnds").Any(l => Tags(l).Contains("their:" + calling)), $"{e} prefers the {calling} and has their ending");
            }
            foreach (var l in Lines(d, "theirs").Concat(Lines(d, "theirEnds")))
                C(WorldHomeMath.ForAPeople(Tags(l)), $"{e}'s own paragraph names a people: {l.Substring(0, Math.Min(40, l.Length))}");
            // Every age its own form: where it has names by age, one for each.
            var names = Lines(d, "names");
            if (names.Any(l => Tags(l).Any(t => VarietyMath.TheirAgeTags.Contains(t))))
                foreach (var t in VarietyMath.TheirAgeTags)
                    C(names.Any(l => Tags(l).Contains(t)), $"{e} has a name in the {t} age's form");
        }
        C(kinds.Contains("scarcity") && kinds.Contains("games") && kinds.Contains("pardon") && kinds.Count >= 9, "the scarcities share their kind with iron and medicine; the rest have kinds of their own");
        C(Lines(Def("WorldSituationDef", "Rimfolk_WS_Games"), "texts").Count(l => Tags(l).Any(t => VarietyMath.TheirAgeTags.Contains(t))) == 4,
            "the games take each age's form: the gathering games, a tourney, an athletics meet, a league season");
        // The ties (Peoples/PeopleTies.cs): a bound people's own lines.
        C(Code("World/Variety/VarietyWorkers.cs").Contains("PeopleTies.Bound(f)") && events.Sum(e => Def("WorldSituationDef", e)?.Descendants("li").Count(li => Tags(li.Value).Contains("bound")) ?? 0) >= 3,
            "a people bound to the colony by a tie has lines of its own ([bound])");

        // B6. The scarcities.
        C(VarietyMath.ClothFactor(true) == VarietyMath.ClothAfterFactor && VarietyMath.ClothFactor(false) == 1f && VarietyMath.ClothAfterFactor > 1f, "cloth goes scarce more readily after the flocks were lost");
        C(VarietyMath.CountsForCloth("Council_WS_Murrain", false, 10f) && VarietyMath.CountsForCloth("Council_WS_Embargo", false, 0f) && !VarietyMath.CountsForCloth("Council_WS_Murrain", true, 10f)
          && !VarietyMath.CountsForCloth("Council_WS_Murrain", false, VarietyMath.ClothAfterDays + 1f) && !VarietyMath.CountsForCloth("Council_WS_Plague", false, 5f),
            "cloth follows a sickness in the herds or markets closed, ended not quietly, within its while");
        C(VarietyMath.ClothAfter.All(x => Def("WorldSituationDef", x) != null), "what cloth follows are world events of the mod's");
        var timber = Def("WorldSituationDef", "Rimfolk_WS_TimberScarce");
        var after = timber?.Element("after")?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
        C(after.Contains("Council_WS_GreatFire") && after.Contains("Council_WS_Flood") && after.Contains("Council_WS_Rebuilding") && after.Contains("Rimfolk_WS_GreatWork") && after.All(x => Def("WorldSituationDef", x) != null),
            "timber goes scarce after a great fire, a flood, rebuilding or a great work");
        C(Code("World/WorldSituations.cs").Contains("d.Worker.WeightFactor(map)") && Code("World/WorldSituationWorker.cs").Contains("public virtual float WeightFactor(Map map) => 1f;"),
            "a world event's own worker can make it likelier now");
        C(RegionPricesMath.Goods("Rimfolk_WS_ClothScarce", out bool clothDear) == RegionGoods.Textiles && clothDear && RegionPricesMath.Goods("Rimfolk_WS_TimberScarce", out _) == RegionGoods.Wood
          && RegionPricesMath.Goods("Rimfolk_WS_BridgeDown", out _) == RegionGoods.Wood, "cloth and timber are bought dear where they are scarce, and timber where a bridge is built again");
        C(Lines(Def("CampKindDef", "Rimfolk_Camp_Shearers"), "situationKinds").Contains("Rimfolk_WS_ClothScarce") && Lines(Def("CampKindDef", "Rimfolk_Camp_Woodcutters"), "situationKinds").Contains("Rimfolk_WS_TimberScarce")
          && Code("Camps/CampKinds.cs").Contains("kind.situationKinds.Contains(s.def.defName)"),
            "shearers come more often while cloth is scarce and woodcutters while timber is, by the world event's name");

        // B7. The games.
        VarietyMath.GamesGuests(true, out int g1, out int g2);
        VarietyMath.GamesGuests(false, out int h1, out int h2);
        C(g1 + g2 <= FeastMath.MaxGuests && h1 + h2 <= FeastMath.MaxGuests && g2 > 0 && h2 == 0 && g1 > 0 && h1 > 0, "the games day's young people: two peoples or one, three guests at most");
        C(VarietyMath.GamesContests(true).SequenceEqual(new[] { ContestKind.Wrestling, ContestKind.Race, ContestKind.Shooting }) && !VarietyMath.GamesContests(false).Contains(ContestKind.Shooting),
            "the games day's bouts, race and shooting match (only with the colony's targets)");
        C(VarietyMath.ParseGamesTag(VarietyMath.GamesTag(42), out int gid) && gid == 42 && !VarietyMath.ParseGamesTag("wedding:3", out _) && !VarietyMath.ParseGamesTag(null, out _),
            "the games day's feast finds its world event again");
        C(VarietyMath.GamesYoungFrom >= 16f && VarietyMath.GamesYoungTo <= 30f, "young people come to the games day");
        C(Code("Diplomacy/FeastMath.cs").Contains("Games") && Code("Diplomacy/Feasts.cs").Contains("CC_Feast_Arrive_Games") && keyed.Contains("<CC_Feast_Arrive_Games>")
          && keyed.Contains("<CC_Feast_HereAs_Games>") && keyed.Contains("<CC_Feast_Week_Games>"), "the games day is a feast of its own purpose, with its own words");
        C(Code("World/Variety/GamesDay.cs").Contains("Feasts.StageChanged += OnStage") && Code("World/Variety/GamesDay.cs").Contains("s.Set(\"ourWin\")"),
            "a colonist who wins at the games day is named in its ending");
        var games = Def("WorldSituationDef", "Rimfolk_WS_Games");
        C(games?.Descendants("li").Any(li => (string)li.Attribute("Class") == "ColonyCouncil.DilemmaEffect_GamesDay") == true
          && games.Element("modifiers")?.Elements("li").Any(m => (string)m.Element("rimfolk") == "Council_Evt_Champion") == true,
            "travelling champions come more often, and the colony may hold a games day");

        // B8. The pardon.
        C(VarietyMath.PardonPossible(true, false) && VarietyMath.PardonPossible(false, true) && !VarietyMath.PardonPossible(false, false), "a pardon after a kind new head or in a holy year");
        C(VarietyMath.PardonCovers(true, false) && !VarietyMath.PardonCovers(false, false) && !VarietyMath.PardonCovers(true, true), "a pardon covers only its own people's");
        var pardon = Def("WorldSituationDef", "Rimfolk_WS_Pardon");
        var mods = pardon?.Element("modifiers")?.Elements("li").ToList() ?? new List<XElement>();
        C(mods.Any(m => (string)m.Element("rimfolk") == "Council_Evt_BountyHunters" && (string)m.Element("factor") == "0")
          && mods.Any(m => (string)m.Element("rimfolk") == "Rimfolk_Mission_Outlaw" && float.Parse((string)m.Element("factor"), System.Globalization.CultureInfo.InvariantCulture) < 1f),
            "no bounty hunters and a rarer outlaw quest while the pardon lasts");
        C(Lines(pardon, "afterFlags").Contains("kind") && Lines(pardon, "after").Contains("Council_WS_Succession"), "the pardon follows a kind new head");
        C(Code("World/Variety/Pardon.cs").Contains("PrisonerInteractionModeDefOf.Release"), "the prisoners walk out by the game's own release, as a ransomed captive does");

        // ================= B15. The peoples' own encounters =================

        var enc = All("EncounterDef").Where(e => e.Element("stranger")?.Element("calling") != null).ToList();
        C(enc.Count == PeoplesMath.Callings.Length * 2, $"two moments for each of the {PeoplesMath.Callings.Length} peoples ({enc.Count})");
        foreach (var c in PeoplesMath.Callings)
            C(enc.Count(e => (string)e.Element("stranger").Element("calling") == c) == 2, $"the {c} have their two moments");
        foreach (var e in enc)
        {
            string dn = (string)e.Element("defName");
            C(Lines(e, "texts").Count >= 2, $"{dn} has two ways to tell it");
            string scene = (string)e.Element("scene");
            if (scene == "building") C(Lines(e, "colonyHas").Count > 0, $"{dn} names the buildings its visitor looks over");
            if (!string.IsNullOrEmpty(scene)) C(Code("World/Home/WorldScenes.cs").Contains("\"" + scene + "\""), $"{dn}'s scene ({scene}) is a scene the encounters know");
        }
        C(Code("Strangers/Strangers.cs").Contains("public string calling;") && Code("Strangers/Strangers.cs").Contains("Peoples.CallingOf(FactionOf(x)) != r.calling"),
            "a stranger's role can ask for one of Rimfolk's peoples by its calling");
        C(Code("World/Home/WorldScenes.cs").Contains("Grave, Broken, Beast, Mech, Building") && Def("JobDef", "Rimfolk_PE_LookOver") != null && Def("JobDef", "Rimfolk_PE_StandAt") != null,
            "the peoples' own scenes play out: a grave stood at, a broken machine, an animal, a mech or a building looked over");

        // ================= The camps =================

        string[] camps = { "Rimfolk_Camp_Beekeepers", "Rimfolk_Camp_MushroomPickers", "Rimfolk_Camp_YoungHands", "Rimfolk_Camp_StrandedCarters", "Rimfolk_Camp_Gleaners", "Rimfolk_Camp_GamesTeam", "Rimfolk_Camp_WalkingHome", "Rimfolk_Camp_StarWatchers" };
        foreach (var k in camps)
        {
            var d = Def("CampKindDef", k);
            C(d != null && Lines(d, "texts").Count >= 2 && Lines(d, "lines").Count >= 5 && Lines(d, "farewell").Count >= 1 && Lines(d, "settled").Count >= 1, $"{k} has its words");
            foreach (var l in Lines(d, "lines")) C(Regex.Matches(Regex.Replace(l, @"^\[[^\]]*\]\s*", ""), @"[.?!]\s+\S").Count == 0, $"{k}'s bubble is one sentence: {l}");
        }
        C(Lines(Def("CampKindDef", "Rimfolk_Camp_YoungHands"), "situationKinds").Contains("hiring") && Lines(Def("CampKindDef", "Rimfolk_Camp_StrandedCarters"), "situationKinds").Contains("mud")
          && Lines(Def("CampKindDef", "Rimfolk_Camp_GamesTeam"), "situationKinds").Contains("games") && Lines(Def("CampKindDef", "Rimfolk_Camp_WalkingHome"), "situationKinds").Contains("pardon")
          && Lines(Def("CampKindDef", "Rimfolk_Camp_StarWatchers"), "situationKinds").Contains("stars") && Lines(Def("CampKindDef", "Rimfolk_Camp_Gleaners"), "situationKinds").Contains("hunger"),
            "the new camps come with the world events that put them on the road");
        // B20. Beekeepers: Alpha Bees' beehouse, and its goods.
        var bees = Def("CampKindDef", "Rimfolk_Camp_Beekeepers");
        C(bees?.Descendants("li").Any(li => li.Value == "RB_Beehouse" && (string)li.Attribute("MayRequire") == "sarg.rimbees") == true
          && Code("Camps/CampVariety.cs").Contains("public override bool FitsMap(Map map) => HiveDef != null;"), "beekeepers come with Alpha Bees, their hives its beehouse");
        C(Code("Camps/CampVariety.cs").Contains("RB_BasicBeekeeping") && Def("JobDef", "Rimfolk_HelpAtHives") != null, "a queen only for a colony that keeps bees; a colonist helps at the hives by the game's own sting");
        // B21. Mushroom pickers.
        var pick = Def("CampKindDef", "Rimfolk_Camp_MushroomPickers");
        C((string)pick?.Element("needs") == "woods" && Lines(pick, "gathers").Contains("VPE_Plant_Healtruffle") && Code("Camps/CampDay.cs").Contains("kind.gathers"),
            "mushroom pickers pitch by the woods and gather the land's caps first");
        C(VarietyMath.AfterFlush(2f, 6f) && !VarietyMath.AfterFlush(7f, 6f) && !VarietyMath.AfterFlush(-1f, 6f) && VarietyMath.BadCapChance > 0f && VarietyMath.BadCapChance <= 0.3f,
            "pickers come in the fall or after the colony's own mushrooms; a bad cap now and then");
        C(VarietyMath.StarHour(22) && VarietyMath.StarHour(0) && !VarietyMath.StarHour(3) && !VarietyMath.StarHour(12), "star-watchers lie out under the sky by night, and sleep");

        // ================= B24. A stud visit =================

        float d5 = VarietyMath.StudDays(0f), d7 = VarietyMath.StudDays(1f);
        C(d5 >= 5f && d7 <= 7f && d5 < d7, "their animal stands five to seven days");
        C(VarietyMath.StudFee(50f) == 30 && VarietyMath.StudFee(5000f) == 200 && VarietyMath.StudFee(700f) == 100 && VarietyMath.StudFee(700f) % 10 == 0, "the fee for a young one: a seventh of his worth, 30 to 200, in tens");
        C(VarietyMath.StudKeep(30) >= 20 && VarietyMath.StudKeep(100) == 50, "the silver for the keep instead: half the fee, at least 20");
        int full = VarietyMath.YoungDueTicks(0f, 20f, 0f), half = VarietyMath.YoungDueTicks(0.5f, 20f, 0f), late = VarietyMath.YoungDueTicks(1f, 20f, 1f);
        C(full == 22 * VarietyMath.DayTicks && half == 12 * VarietyMath.DayTicks && late == 5 * VarietyMath.DayTicks, "the young one comes when it is born, and two to five days on the road");
        var stud = Def("IncidentDef", "Rimfolk_Mission_StudVisit");
        C(stud != null && (string)stud.Element("workerClass") == "ColonyCouncil.HappeningWorker_MissionStud" && stud.Descendants("options").First().Elements("li").Count() == 3,
            "the stud visit: accept, accept for silver for the keep, or decline");
        string studCode = Code("Missions/Workers_MissionStud.cs");
        C(studCode.Contains("typeof(CompEggLayer)") && studCode.Contains("h.Father == x") && studCode.Contains("x.SetFaction(Faction.OfPlayer)"),
            "it stands as the colony's for the game's own mating, judged by who is pregnant by whom, egg layers left out");

        // ================= B28. Hopes by people, and the singing =================

        foreach (var c in PeoplesMath.Callings) C(FeastMath.HopeOfCalling(c) != FeastHope.None, $"the {c} hope for their own people's contest");
        C(FeastMath.HopeOfCalling("hunters") == FeastHope.Hunt && FeastMath.HopeOfCalling("mercenaries") == FeastHope.Bouts && FeastMath.HopeOfCalling("traders") == FeastHope.Tables
          && FeastMath.HopeOfCalling("salvagers") == FeastHope.Race && FeastMath.HopeOfCalling("pilgrims") == FeastHope.MealAndMusic && FeastMath.HopeOfCalling(null) == FeastHope.None,
            "the hunt, the bouts, the tables, the race, a fine meal and music, as proposed");
        C(FeastMath.Hope(WorldAge.Medieval, true, "traders") == FeastHope.MealAndMusic && FeastMath.Hope(WorldAge.Tribal, false, null) == FeastHope.Hunt
          && FeastMath.Hope(WorldAge.Spacer, false, "crew") == FeastHope.Bouts, "the Empire's own ways, then a people's calling, then its age");
        C(FeastMath.Answers(FeastHope.Bouts, ContestKind.Wrestling) && FeastMath.Answers(FeastHope.Tables, ContestKind.Tables) && FeastMath.Answers(FeastHope.Race, ContestKind.Race)
          && FeastMath.Answers(FeastHope.MealAndMusic, ContestKind.Singing) && !FeastMath.Answers(FeastHope.Bouts, ContestKind.Race), "each hope answered by its contest, music by the singing");
        C(FeastMath.Offered(FeastHope.MealAndMusic, new[] { ContestKind.Singing }, false) && FeastMath.Offered(FeastHope.MealAndMusic, new ContestKind[0], true)
          && !FeastMath.Offered(FeastHope.MealAndMusic, new[] { ContestKind.Race }, false), "music: an instrument or a singing contest");
        C(!FeastMath.Outdoor(ContestKind.Singing) && FeastMath.MaxTicks(ContestKind.Singing) > FeastMath.SingTicks * FeastMath.MaxSingers, "the singing is held indoors or by a fire, and has time for every singer");
        C(FeastMath.SingScore(20, 20, true, 1f) > FeastMath.SingScore(0, 0, false, 0f) && FeastMath.SingScore(10, 0, true, 0f) - FeastMath.SingScore(10, 0, false, 0f) == 2f
          && FeastMath.SingScore(30, 30, false, 2f) == FeastMath.SingScore(20, 20, false, 1f), "a song by artistic and social, an instrument and some luck, within bounds");
        C(FeastMath.SingWinner(new[] { 3f, 9f, 9f, 2f }) == 1 && FeastMath.SingWinner(new float[0]) == -1 && FeastMath.SingWinner(new[] { 0f }) == -1, "the best song wins, the first of equals");
        foreach (var k in new[] { "Contest_Singing", "ContestThe_Singing", "Msg_Begins_Singing", "Result_Singing", "Result_None_Singing", "Dlg_Contest_Singing", "Dlg_ContestTip_Singing",
                     "CWhy_Singing", "CWhy_SingPlace", "Hope_Bouts", "Hope_Tables", "Hope_Race" })
            C(keyed.Contains("<CC_Feast_" + k + ">"), $"the feast's words: CC_Feast_{k}");
        var singLines = defs.SelectMany(d => d.Root.Elements()).Where(e => (string)e.Element("defName") == "Rimfolk_Lines_OutsiderSays_Feasts").SelectMany(e => e.Descendants("li")).Select(li => li.Value).ToList();
        C(singLines.Count(l => Tags(l).Contains("feastsing")) >= 5 && singLines.Any(l => Tags(l).Contains("feastsingwin")), "a singer's word as they start, and the winner's");
        C(Code("Diplomacy/FeastSinging.cs").Contains("StartPlaying(pawn)") && Def("JobDef", "Rimfolk_FeastSing") != null, "the singer plays the colony's instrument by its own sound, or sings by a fire");

        // ================= The gods as the colony believes them, the council's questions =================

        C(WorldHomeMath.Great.Contains("Rimfolk_WS_FallingStars") && WorldHomeMath.GreatGods("Rimfolk_WS_FallingStars").Length > 0, "the falling stars are a great event the colony's believers take for a god's doing");
        var q = Def("DilemmaDef", "Rimfolk_Dlm_WS_FallingStars");
        C(q != null && Lines(q, "situations").Contains("Rimfolk_WS_FallingStars") && q.Descendants("li").Any(li => (string)li.Attribute("Class") == "ColonyCouncil.DilemmaEffect_Gathering"),
            "a council question in the falling stars' season: a night out under them, played out as a gathering");

        // ================= The seams =================

        C(Code("World/Variety/VarietyDebug.cs").Contains("World variety: start one of the new world events now") && Code("World/Variety/VarietyDebug.cs").Contains("World variety: one of the new camps here now")
          && Code("World/Variety/VarietyDebug.cs").Contains("World variety: a people's own encounter now") && Code("World/Variety/VarietyDebug.cs").Contains("a stud visit offered now"),
            "dev actions start each new world event, camp and encounter, and the stud visit");
        foreach (var e in events) C(Code("World/Variety/VarietyDebug.cs").Contains("\"" + e + "\""), $"{e} can be started from the dev action");
        foreach (var k in camps) C(Code("World/Variety/VarietyDebug.cs").Contains("\"" + k + "\""), $"{k} can be brought from the dev action");
        C(Read("Tools/Tests/Tests.csproj").Contains("World/Variety/VarietyMath.cs"), "the pure rules are linked into the tests");
        return n;
    }
}
