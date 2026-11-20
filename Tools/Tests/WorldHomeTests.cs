using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The world brought home (Source/ColonyCouncil/World/Home/; the content update's integrations, part B: B1, B2, B3, B23,
/// B25, B26, B32): the pure rules (WorldHomeMath, RenownMath's tables), and the defs, words and seams they stand on (the
/// peoples' own paragraphs in the world events, the quests a world event makes likelier and their season's lines, the
/// road's news, the encounters and council questions of a world event's season, the great events in the colony's book).
/// </summary>
public static class WorldHomeTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the world brought home: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string Read(string rel) => File.ReadAllText(Path.Combine(root, rel));
        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f)).ToList();
        XElement Def(string type, string name) =>
            defs.SelectMany(d => d.Root.Elements()).FirstOrDefault(e => (e.Name.LocalName == type || e.Name.LocalName == "ColonyCouncil." + type) && (string)e.Element("defName") == name);
        List<string> Tags(string line)
        {
            var m = Regex.Match(line ?? "", @"^\[([^\]]*)\]");
            return m.Success ? m.Groups[1].Value.Split(',').Select(t => t.Trim()).ToList() : new List<string>();
        }
        List<string> Lines(XElement e, string list) => e?.Element(list)?.Elements("li").Select(li => li.Value).ToList() ?? new List<string>();

        // ================= B1. World events in the peoples' own ways =================

        C(WorldHomeMath.ForAPeople(new[] { "their:healers" }) && WorldHomeMath.ForAPeople(new[] { "winter", " their:vaultfolk" })
          && !WorldHomeMath.ForAPeople(new[] { "their:grateful", "their:friend" }) && !WorldHomeMath.ForAPeople(new string[0]) && !WorldHomeMath.ForAPeople(null),
            "a people's own line names a calling (their:grateful is a memory, not a people)");
        // The world events that prefer a people (Patches/Rimfolk_Peoples*.xml), and the six hard ones.
        var prefer = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Patches"), "Rimfolk_Peoples*.xml"))
            foreach (Match m in Regex.Matches(File.ReadAllText(f), @"WorldSituationDef\[defName=""(\w+)""\]/faction</xpath>\s*<value>\s*<calling>(\w+)</calling>"))
                prefer[m.Groups[1].Value] = m.Groups[2].Value;
        C(prefer.Count >= 30, $"the world events that prefer a people are read from the patches ({prefer.Count})");
        foreach (var kv in prefer)
        {
            var d = Def("WorldSituationDef", kv.Key);
            string tag = "their:" + kv.Value;
            C(Lines(d, "theirs").Any(l => Tags(l).Contains(tag)), $"{kv.Key} has its start in {kv.Value}'s own ways");
            C(Lines(d, "theirEnds").Any(l => Tags(l).Contains(tag)), $"{kv.Key} has its ending in {kv.Value}'s own ways");
        }
        string[] hard = { "Council_WS_Plague", "Council_WS_Famine", "Council_WS_HardWinter", "Council_WS_Flood", "Council_WS_GreatFire", "Council_WS_Mourning" };
        foreach (var h in hard)
        {
            var lines = Lines(Def("WorldSituationDef", h), "theirs");
            foreach (var calling in PeoplesMath.Callings)
                C(lines.Count(l => Tags(l).Contains("their:" + calling)) >= 1, $"{h} falls on {calling} in their own ways");
        }
        int b1 = 0;
        foreach (var d in defs.SelectMany(x => x.Root.Elements()).Where(e => e.Name.LocalName == "ColonyCouncil.WorldSituationDef"))
            foreach (var l in Lines(d, "theirs").Concat(Lines(d, "theirEnds")))
            {
                b1++;
                C(WorldHomeMath.ForAPeople(Tags(l)), $"{d.Element("defName")?.Value}: a people's own line names its people: {l}");
                string words = Regex.Replace(l, @"^\[[^\]]*\]\s*", "");
                C(!words.Contains("—") && !words.Contains("!"), $"no em dash or exclamation mark: {l}");
            }
        C(b1 >= 200, $"about two hundred lines in the peoples' own ways ({b1})");
        string worker = Code("World/WorldSituationWorker.cs"), situations = Code("World/WorldSituations.cs");
        C(worker.Contains("public string Theirs(") && situations.Contains("worker.Theirs(def.theirs, s)") && situations.Contains("worker.Theirs(def.theirEnds, s, ending.key)"),
            "the seams: under the start letter and under the ending's");

        // ================= B2. World events steer the quests and storylines offered =================

        C(WorldHomeMath.SteerTags("sickness", "Council_WS_Plague", true).SequenceEqual(new[] { "ws:sickness", "wsdef:Council_WS_Plague", "ws:steered", "ws:theirs" })
          && !WorldHomeMath.SteerTags("war", "X", false).Contains("ws:theirs"), "a steered event's tags");
        C(WorldHomeMath.WorldTagged(new[] { "ws:sickness" }) && WorldHomeMath.WorldTagged(new[] { "wsdef:Council_WS_Assize" }) && !WorldHomeMath.WorldTagged(new[] { "ws:theirs", "order" })
          && !WorldHomeMath.WorldTagged(null), "a line of a world event's season (ws:theirs alone isn't one)");
        C(WorldHomeMath.SteerWeight(true) == WorldHomeMath.SteeredPeople && WorldHomeMath.SteerWeight(false) == 1f && WorldHomeMath.SteeredPeople > 1f,
            "the world event's own people likelier to ask");
        var steer = new Dictionary<string, string[]>
        {
            ["Council_WS_Plague"] = new[] { "Rimfolk_Storyline_SicknessMedicine", "Rimfolk_Mission_Healer", "Rimfolk_Mission_OrderMedicine" },
            ["Rimfolk_WS_MedicineScarce"] = new[] { "Rimfolk_Storyline_SicknessMedicine", "Rimfolk_Mission_Healer", "Rimfolk_Mission_OrderMedicine" },
            ["Council_WS_Drought"] = new[] { "Rimfolk_Storyline_WellWater" },
            ["Council_WS_BanditRoad"] = new[] { "Rimfolk_Storyline_RaidersDefences", "Rimfolk_Mission_Outlaw" },
            ["Council_WS_BanditLordFalls"] = new[] { "Rimfolk_Storyline_RaidersDefences", "Rimfolk_Mission_Outlaw" },
            ["Council_WS_Warlord"] = new[] { "Rimfolk_Storyline_RaidersDefences", "Rimfolk_Mission_Outlaw" },
            ["Council_WS_Embargo"] = new[] { "Rimfolk_Storyline_QuarrelTruce" },
            ["Council_WS_Feud"] = new[] { "Rimfolk_Storyline_QuarrelTruce" },
            ["Rimfolk_WS_NeighborsWar"] = new[] { "Rimfolk_Storyline_QuarrelTruce", "Rimfolk_Storyline_ChildrenShelter" },
            ["Council_WS_GreatFire"] = new[] { "Rimfolk_Storyline_ChildrenShelter" },
            ["Council_WS_Drovers"] = new[] { "Rimfolk_Storyline_HerdStray", "Rimfolk_Mission_SickAnimal" },
            ["Council_WS_Migration"] = new[] { "Rimfolk_Storyline_HerdStray", "Rimfolk_Mission_SickAnimal" },
            ["Council_WS_Murrain"] = new[] { "Rimfolk_Storyline_HerdStray", "Rimfolk_Mission_SickAnimal" },
            ["Council_WS_HolyYear"] = new[] { "Rimfolk_Storyline_RelicRest" },
            ["Council_WS_Pilgrimage"] = new[] { "Rimfolk_Storyline_RelicRest" },
            ["Council_WS_Mourning"] = new[] { "Rimfolk_Storyline_GraveSearch" },
            ["Council_WS_Assize"] = new[] { "Rimfolk_Mission_Prisoner", "Rimfolk_Mission_Dispute" },
            ["Council_WS_Fair"] = new[] { "Rimfolk_Mission_Musicians", "Rimfolk_Mission_WeddingPiece" },
            ["Council_WS_Songs"] = new[] { "Rimfolk_Mission_Musicians", "Rimfolk_Mission_WeddingPiece" },
            ["Rimfolk_WS_ColdYear"] = new[] { "Rimfolk_Mission_SeedKept" },
            ["Council_WS_HardWinter"] = new[] { "Rimfolk_Mission_SeedKept" }
        };
        var openers = new HashSet<string>();
        foreach (var kv in steer)
        {
            var ws = Def("WorldSituationDef", kv.Key);
            var raised = ws?.Element("modifiers")?.Elements("li").Where(li => li.Element("rimfolk") != null && float.Parse((string)li.Element("factor") ?? "1", System.Globalization.CultureInfo.InvariantCulture) > 1f)
                .Select(li => li.Element("rimfolk").Value).ToHashSet() ?? new HashSet<string>();
            foreach (var o in kv.Value)
            {
                C(raised.Contains(o), $"{kv.Key} makes {o} likelier");
                openers.Add(o);
            }
        }
        foreach (var o in openers)
        {
            var d = Def("IncidentDef", o);
            var ext = d?.Element("modExtensions")?.Elements("li").FirstOrDefault(li => (string)li.Attribute("Class") == "ColonyCouncil.HappeningExtension");
            var texts = Lines(ext, "texts");
            C(d != null && ext != null, $"{o} exists as one of Rimfolk's events");
            C(texts.Count(l => WorldHomeMath.WorldTagged(Tags(l))) >= 1, $"{o}'s first letter has a line for a world event's season");
            foreach (var l in texts.Where(l => l.Contains("{WSNAME}") || l.Contains("{WSPLACE}")))
                C(WorldHomeMath.WorldTagged(Tags(l)), $"{o}: {{WSNAME}} and {{WSPLACE}} only in a season's line: {l}");
        }
        string home = Code("World/Home/WorldHome.cs");
        C(Code("Happenings/IncidentWorker_Happening.cs").Contains("WorldSteer.During(def)") && Code("Happenings/IncidentWorker_Happening.cs").Contains("WorldSteer.AddTags(ctx, tags)")
          && Code("Happenings/IncidentWorker_Happening.cs").Contains("WorldSteer.Prefer(lines, tags)") && Code("Storylines/Storylines.cs").Contains("WorldSteer.Prefer(lines, context)")
          && Code("Happenings/Happenings.cs").Contains("WorldSteer.AddArgs(this, args)") && Code("Happenings/HappeningHelpers.cs").Contains("WorldSteer.Weight(f)")
          && Code("Missions/Workers_MissionRemembered.cs").Contains("WorldSteer.Weight(f)"),
            "the seams: the cast, the tags, the lines preferred, the words, the people weighed");

        // ================= B3. Messengers tell the road's news =================

        C(WorldHomeMath.OnRoad(3f, 20f, 20f) && WorldHomeMath.OnRoad(20f, 4f, 20f) && WorldHomeMath.OnRoad(10f, 10f, 20f) && !WorldHomeMath.OnRoad(30f, 30f, 20f)
          && !WorldHomeMath.OnRoad(float.NaN, 2f, 10f) && !WorldHomeMath.OnRoad(8f, 9f, float.NaN) && WorldHomeMath.OnRoad(4f, float.NaN, float.NaN) == false,
            "on the road: near the colony or their town, or not far out of the way between them");
        C(WorldHomeMath.RoadNewsChance > 0.5f && WorldHomeMath.RoadNewsChance < 1f, "the road's news most times there is any, not every time");
        C(WorldHomeMath.RoadTags("water", "Council_WS_Flood", true).SequenceEqual(new[] { "road", "ws:water", "wsdef:Council_WS_Flood", "ws:theirs" }), "the road's tags");
        var courier = Lines(Def("FlavorPoolDef", "Rimfolk_CourierLines")?.Element("lines"), "plain");
        var road = courier.Where(l => Tags(l).Contains("road")).ToList();
        C(road.Count >= 40, $"about forty lines of the road's news ({road.Count})");
        foreach (var l in road)
            C(WorldHomeMath.WorldTagged(Tags(l)), $"a line of the road's news names a world event: {l}");
        var kinds = defs.SelectMany(d => d.Root.Elements()).Where(e => e.Name.LocalName == "ColonyCouncil.WorldSituationDef").Select(e => (string)e.Element("kind")).Where(k => k != null).Distinct().ToList();
        foreach (var k in new[] { "sickness", "hunger", "water", "war", "fair", "faith", "drought", "weather", "fire", "mourning", "trade" })
            C(road.Any(l => Tags(l).Contains("ws:" + k)) && kinds.Contains(k), $"the road's news of a world event of {k}");
        C(Code("Couriers/CourierTracker.cs").Contains("RoadNews.Say(by, t.faction") && Code("World/NeighbourLetters.cs").Contains("RoadNews.Say(first, t.faction")
          && Code("Post/MailIn.cs").Contains("RoadNews.Say(carrier"), "the seams: the courier, the answer brought, the post's bag set down");

        // ================= B23. What the colony is known for draws the quests and camps =================

        var incidents = string.Concat(Directory.GetFiles(Path.Combine(root, "Defs", "IncidentDefs"), "*.xml").Concat(Directory.GetFiles(Path.Combine(root, "Defs", "MissionDefs"), "*.xml")).Select(File.ReadAllText));
        var camps = string.Concat(Directory.GetFiles(Path.Combine(root, "Defs", "CampDefs"), "*.xml").Select(File.ReadAllText));
        foreach (var kv in RenownMath.Camps)
            foreach (var d in kv.Value)
                C(camps.Contains("<defName>" + d + "</defName>"), $"the camp {d} ({kv.Key}) exists");
        C(RenownMath.Kinds.All(RenownMath.Camps.ContainsKey), "every kind of renown draws a camp");
        C(RenownMath.Events.Values.Sum(v => v.Length) >= 28 && RenownMath.Events[KnownFor.Healers].Contains("Rimfolk_Storyline_SicknessMedicine")
          && RenownMath.Events[KnownFor.Faithful].Contains("Rimfolk_Storyline_RelicRest"), "the quests and storylines that fit each kind of renown are in its table");
        C(RenownMath.CampFactor("Council_Camp_WarParty", KnownFor.Fighters, RenownBand.Region) == RenownMath.BandFactor(RenownBand.Region)
          && RenownMath.CampFactor("Council_Camp_WarParty", KnownFor.Healers, RenownBand.Peoples) == 1f && RenownMath.CampFactor(null, KnownFor.Hosts, RenownBand.Region) == 1f,
            "a camp drawn by the colony's name, as an event is");
        C(Code("Camps/CampKindDef.cs").Contains("Renown.CampFactor(defName, map)"), "the seam: a camp's weight");

        // ================= B25. Encounters of the region's hard times =================

        C(WorldHomeMath.SeasonFits(null, null) && WorldHomeMath.SeasonFits(new string[0], new[] { "war" }) && WorldHomeMath.SeasonFits(new[] { "hunger" }, new[] { "war", "hunger" })
          && !WorldHomeMath.SeasonFits(new[] { "hunger" }, new[] { "war" }) && !WorldHomeMath.SeasonFits(new[] { "hunger" }, null), "only in a world event's season");
        var encounters = defs.SelectMany(d => d.Root.Elements()).Where(e => e.Name.LocalName == "ColonyCouncil.EncounterDef" && e.Element("situations") != null).ToList();
        C(encounters.Count >= 14, $"about fourteen encounters of the region's hard times ({encounters.Count})");
        var wsNames = defs.SelectMany(d => d.Root.Elements()).Where(e => e.Name.LocalName == "ColonyCouncil.WorldSituationDef").Select(e => (string)e.Element("defName")).ToHashSet();
        foreach (var e in encounters)
        {
            string dn = (string)e.Element("defName");
            foreach (var s in Lines(e, "situations")) C(kinds.Contains(s) || wsNames.Contains(s), $"{dn}: its world event {s} exists");
            string scene = (string)e.Element("scene");
            C(scene == null || WorldScenesNames.Contains(scene), $"{dn}: a known scene");
        }
        foreach (var w in new[] { "Council_WS_Plague", "hunger", "Council_WS_HolyYear", "Rimfolk_WS_NeighborsWar", "Council_WS_Comet", "Council_WS_Fair", "Council_WS_Drovers", "Council_WS_IceRoad", "Council_WS_Songs" })
            C(encounters.Any(e => Lines(e, "situations").Contains(w)), $"an encounter of {w}'s season, as proposed");
        string enc = Code("Strangers/Encounters.cs");
        C(enc.Contains("WorldSeason.Fits(d.situations)") && enc.Contains("WorldScenes.Fits(def, p, s, map)") && enc.Contains("WorldScenes.Play(def, ctx)") && enc.Contains("WorldSeason.Fill("),
            "the seams: the season, the scene true on the map, played out as told, its words");

        // ================= B26. Council questions the world brings home =================

        C(WorldHomeMath.ApartChance(0.25f, true) == 0.125f && WorldHomeMath.ApartChance(0.25f, false) == 0.25f && WorldHomeMath.ApartChance(float.NaN, true) == 0f,
            "kept apart halves the sickness from travellers");
        C(WorldHomeMath.ApartGoodwill < 0 && WorldHomeMath.ApartGoodwill >= -2, "visitors kept at arm's length a little cooler");
        C(WorldHomeMath.NextVigilDay(10) == 17 && WorldHomeMath.FirstVigilDays == 1 && WorldHomeMath.VigilFrom < WorldHomeMath.VigilTo
          && WorldHomeMath.VigilHoursMin <= WorldHomeMath.VigilHoursMax && WorldHomeMath.VigilHoursMax <= 10f, "a vigil day each week, most of a working day");
        C(WorldHomeMath.SideTie(true, true, true) == 6 && WorldHomeMath.SideTie(false, false, false) == 0 && WorldHomeMath.SideTie(true, false, false) > WorldHomeMath.SideTie(false, true, true) - 1,
            "family counts most toward a side");
        C(WorldHomeMath.SideFlag(true) == "backFaction" && WorldHomeMath.SideFlag(false) == "backEnemy", "the side's flag");
        var war = Def("WorldSituationDef", "Rimfolk_WS_NeighborsWar");
        string Forbids(string key) => string.Join(",", war?.Element("beats")?.Elements("li").FirstOrDefault(b => (string)b.Element("key") == key)?.Element("forbids")?.Elements("li").Select(x => x.Value) ?? new string[0]);
        C(Forbids("food").Contains("backEnemy") && Forbids("column").Contains("backEnemy") && Forbids("steel").Contains("backFaction") && Forbids("worn").Contains("backFaction"),
            "the backed side's asks come, the other's don't");
        var questions = new Dictionary<string, string>
        {
            ["Rimfolk_Dlm_WS_Apart"] = "Council_WS_Plague", ["Rimfolk_Dlm_WS_Vigil"] = "Council_WS_HolyYear", ["Rimfolk_Dlm_WS_Sides"] = "Rimfolk_WS_NeighborsWar",
            ["Rimfolk_Dlm_WS_Mourning"] = "Council_WS_Mourning", ["Rimfolk_Dlm_WS_FairDay"] = "Council_WS_Fair"
        };
        foreach (var kv in questions)
        {
            var q = Def("DilemmaDef", kv.Key);
            C(q != null && Lines(q, "situations").Contains(kv.Value), $"{kv.Key} comes only in {kv.Value}'s season");
        }
        string effects(string dn) => string.Join(" ", Def("DilemmaDef", dn)?.Descendants("li").Select(li => (string)li.Attribute("Class")).Where(c => c != null) ?? new string[0]);
        C(effects("Rimfolk_Dlm_WS_Apart").Contains("DilemmaEffect_WorldApart") && effects("Rimfolk_Dlm_WS_Vigil").Contains("DilemmaEffect_WorldVow")
          && effects("Rimfolk_Dlm_WS_Sides").Contains("DilemmaEffect_WorldSide") && effects("Rimfolk_Dlm_WS_Mourning").Contains("DilemmaEffect_WorldMourning")
          && effects("Rimfolk_Dlm_WS_FairDay").Contains("DilemmaEffect_Gathering") && effects("Rimfolk_Dlm_WS_FairDay").Contains("DilemmaEffect_Rest"),
            "each answer does what it says");
        C(Code("Dilemmas/Dilemmas.cs").Contains("WorldSeason.Fits(def.situations)") && Code("Harm/Carriers.cs").Contains("WorldHomeMath.ApartChance(") && enc.Contains("WorldApart.Apart"),
            "the seams: the question's season, the carriers halved, no moments with travellers kept apart");
        C(Def("ThoughtDef", "Rimfolk_WH_VigilKept") != null && Def("JobDef", "Rimfolk_WH_KneelAt") != null && Def("JobDef", "Rimfolk_WH_WarmAt") != null, "the vigil's mood and the scenes' jobs");

        // ================= B32. The region's great events in the colony's book =================

        string godsXml = Read("Defs/GodDefs/Rimfolk_Gods.xml");
        var godKeys = new HashSet<string>(Regex.Matches(godsXml, @"^    <key>(\w+)</key>", RegexOptions.Multiline).Select(m => m.Groups[1].Value));
        foreach (var g in WorldHomeMath.Great)
        {
            C(wsNames.Contains(g), $"the great world event {g} exists");
            C(WorldHomeMath.GreatGods(g).Length > 0 && WorldHomeMath.GreatGods(g).All(godKeys.Contains), $"{g} is taken for one of the gods' doing");
        }
        C(WorldHomeMath.GreatGods("Council_WS_Fair").Length == 0 && WorldHomeMath.GreatGods(null).Length == 0, "a fair is nobody's doing");
        C(WorldHomeMath.GreatPast(true, 0f) && WorldHomeMath.GreatPast(false, WorldHomeMath.GreatNewDays + 0.1f) && !WorldHomeMath.GreatPast(false, 1f), "once, while it is new");
        var great = Lines(Def("FlavorPoolDef", "Rimfolk_Pool_WorldHome_Great")?.Element("lines"), "plain");
        foreach (var t in new[] { "named", "other", "scoff" }) C(great.Count(l => Tags(l).Contains(t)) >= 2, $"the great events' {t} lines");
        foreach (var g in WorldHomeMath.Great) C(great.Any(l => Tags(l).Contains("wsdef:" + g) && Tags(l).Contains("named")), $"a believer's own line for {g}");
        string keyed = Read("Languages/English/Keyed/Rimfolk_WorldHome.xml");
        C(Enumerable.Range(1, 3).All(i => keyed.Contains("<CC_WH_Great_Belief_" + i + ">")) && keyed.Contains("<CC_WH_Great_What>"), "the book's words, in the old voice");
        C(home.Contains("GodLore.Believes(first, g,") && home.Contains("GodLore.Disputed(doubter") && home.Contains("Worship.Called(g, p)"),
            "the book keeps who believes it, by the name they call the god, and the doubt");

        // ================= Saved, the dev actions =================

        string book = Code("World/Home/WorldHomeBook.cs");
        C(book.Contains("Scribe_Collections.Look(ref vows") && book.Contains("Scribe_Collections.Look(ref dyed"), "the vows and the colours saved");
        C(situations.Contains("WorldHome.Hourly(t)"), "the world events' hour carries the world brought home");
        string dev = Code("World/Home/WorldHomeDebug.cs");
        C(new[] { "a quest a running world event makes likelier", "the road's news", "an encounter of the region's hard times", "a council question the world brings home",
            "a great world event taken for a god's doing", "the next vigil day", "the mourning colours put back" }.All(dev.Contains), "a dev action for each");
        return n;
    }

    /// <summary>The scenes an encounter may name (WorldScenes.All), read from the source so the tests need no game types.</summary>
    private static HashSet<string> WorldScenesNames => new HashSet<string> { "pocket", "kneel", "warm", "sides", "sky", "dog", "hum" };
}
