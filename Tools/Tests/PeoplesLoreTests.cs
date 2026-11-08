using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The seven peoples brought to the level of the new ones (docs/design/peoples-next.md; docs/design/peoples.md, "Their
/// stories, sayings and touchpoints"; Peoples/PeoplesLore.cs, PeoplesLoreMath.cs): each has a founding story in its
/// description, sayings, what it honours and fears (its card), and its voice in every pool their people speak in; its
/// new touchpoints at the gate are wired to their calling and play out on the map; its faith is soft on every mod and
/// sound (no two of its memes exclude each other, as the memes' own defs have it); the names of peoples that arise in its
/// ways carry its signature; its Almanac page tells it; and the code keeps no clock, saves nothing and hurts nobody.
/// </summary>
public static class PeoplesLoreTests
{
    /// <summary>The founder each people's ways began with, as its description, page and talk name them.</summary>
    private static readonly Dictionary<string, string> Founders = new()
    {
        ["healers"] = "Aldith", ["herders"] = "Tamsa", ["traders"] = "Edda Vane", ["pilgrims"] = "Old Brannoc", ["scholars"] = "Ilse Varro",
        ["brokers"] = "Ines Okoro", ["fishers"] = "Wenna Reed",
        // Release D's.
        ["hunters"] = "Tobin Garrow", ["sextons"] = "Ysolde Marr", ["tamers"] = "Nessa Pell", ["salvagers"] = "Mab Wainwright",
        // Release E's.
        ["seers"] = "Hesper Lyle", ["grovekeepers"] = "Elin Sedge", ["mercenaries"] = "Corra Mallin", ["artisans"] = "Oswin Kell",
        // Release F's.
        ["linesmen"] = "Ennis Calloway", ["drillers"] = "Marta Grieve", ["citizens"] = "Clement Ashdown", ["crew"] = "Imre Sallow",
        // Release G's.
        ["editors"] = "Orla Venn", ["mechanitors"] = "Tamsin Arro", ["listeners"] = "Ilsabet Ruen", ["vaultfolk"] = "Hollis Crane"
    };

    /// <summary>
    /// The exclusion tags of every meme the seven name (read from Ideology's, Vanilla Ideology Expanded - Memes' and Alpha
    /// Memes' own 1.6 defs and patches): two memes that share a tag can't stand in one faith.
    /// </summary>
    private static readonly Dictionary<string, string[]> MemeTags = new()
    {
        ["Guilty"] = new[] { "GroupRelation" }, ["FleshPurity"] = new[] { "DrugUse", "FleshAugmentation", "VME_FleshPurityVsSweetTeeth" },
        ["Rancher"] = new[] { "AnimalTreatment", "VME_VeganVsRancher" }, ["Collectivist"] = new[] { "Individualism", "VME_CollectivistVsEgalitarian" },
        ["Individualist"] = new[] { "Individualism", "SupremacistIndividualist" }, ["Proselytizer"] = new[] { "VME_Proselytism", "IsolationistProselytizer" },
        ["Transhumanist"] = new[] { "FleshAugmentation", "VME_EldersOrTranshumanist" }, ["NaturePrimacy"] = new[] { "Primacy" },
        ["VME_Healthcare"] = new[] { "VME_HolyDiseasesVsHealthcare", "VME_FleshcrafterVsHealthcare" }, ["VME_Light"] = new[] { "Darkness" },
        ["VME_Nomad"] = new[] { "VME_NomadVsCityBuilders" }, ["VME_Elders"] = new[] { "VME_EldersOrYouth", "VME_EldersOrTranshumanist" },
        ["VME_Trader"] = Array.Empty<string>(), ["VME_Republic"] = new[] { "VME_LeaderPrecepts" }, ["VME_Progressive"] = Array.Empty<string>(),
        ["VME_Egalitarian"] = new[] { "GroupRelation", "VME_Proselytism", "GenderSupremacy", "VME_CollectivistVsEgalitarian", "VME_IndustrialistVsEgalitarian" },
        ["AM_Monastic"] = new[] { "AM_Ascetic", "VME_AsceticVsHedonism", "AM_AsceticVsHighLife", "VME_AsceticVsSweetTeeth", "VME_AsceticVsPartyLife" },
        ["AM_WaterPrimacy"] = Array.Empty<string>(),
        // Release D's.
        ["HumanPrimacy"] = new[] { "Primacy" }, ["AnimalPersonhood"] = new[] { "AnimalTreatment" }, ["VME_ExaltedPriesthood"] = Array.Empty<string>(),
        ["VME_Scrapper"] = Array.Empty<string>(),
        // Release E's (Alpha Memes' patches add to Ideology's own: the tree's connection and its deforestation, the dryads).
        ["VME_Astrology"] = new[] { "VME_AstrologyVsGestalt", "VME_MadnessVsAstrology" },
        ["TreeConnection"] = new[] { "AM_DefilerVsTreeConnection", "AM_TreeConnectionAndDeforestation", "AM_Trees", "TreeConnectionExclusion" },
        ["AM_GauranlenSupremacy"] = new[] { "AM_GladiatorVsDryads", "AM_SharpshooterVsDryads" }, ["Loyalist"] = new[] { "GroupRelation" },
        ["AM_Bulwark"] = new[] { "AM_BulwarkVsNudist", "AM_Combat" }, ["VME_CraftCulture"] = new[] { "VME_LeaderPrecepts", "VME_SadistVsCraftCulture" },
        ["AM_Artist"] = new[] { "VME_AutomationEfficiency_Decreased" },
        // Release F's (docs/player-mods/memes.tsv).
        ["Tunneler"] = Array.Empty<string>(), ["VME_HardcoreIndustrialism"] = new[] { "VME_Work", "VME_IndustrialismVsViolent", "VME_IndustrialistVsEgalitarian" },
        ["VME_CityBuilders"] = new[] { "VME_NomadVsCityBuilders" }, ["Shipborn"] = Array.Empty<string>(),
        // Release G's (docs/player-mods/memes.tsv).
        ["VME_Gestalt"] = new[] { "Individualism", "VME_AstrologyVsGestalt", "VME_MadnessVsGestalt" }, ["VME_MechanoidSupremacy"] = new[] { "Primacy" },
        ["VME_PsychicFocus"] = new[] { "VME_LeaderPrecepts", "VME_AffinityVsVampires" }
    };

    private static string ModOf(string meme) =>
        meme == "Shipborn" ? "Ludeon.RimWorld.Ideology,Ludeon.RimWorld.Odyssey" : meme.StartsWith("VME_") ? "VanillaExpanded.VMemesE" : meme.StartsWith("AM_") ? "sarg.alphamemes" : "Ludeon.RimWorld.Ideology";

    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "peoples' lore: " + what);
        }

        // ================= The pure rules =================

        C(PeoplesLoreMath.NurseCount(0) == 0 && PeoplesLoreMath.NurseCount(2) == 2 && PeoplesLoreMath.NurseCount(9) == PeoplesLoreMath.NurseMax && PeoplesLoreMath.NurseMax == 4,
            "the hospitaller sees to the fevered, four at most");
        C(PeoplesLoreMath.Fever(true, false, false) && !PeoplesLoreMath.Fever(true, true, false) && !PeoplesLoreMath.Fever(false, false, false) && !PeoplesLoreMath.Fever(true, false, true),
            "a fever is an illness the body still has to fight off, never an everyday ailment");
        C(PeoplesLoreMath.LeavesYoung(10f, 40f) && !PeoplesLoreMath.LeavesYoung(40f, 40f) && !PeoplesLoreMath.LeavesYoung(float.NaN, 40f) && PeoplesLoreMath.LeavesYoung(39f, 0f)
          && PeoplesLoreMath.GoodStay == 40f, "a gift for a stay that went well: their unease under 40, the camp's own line for asking to join");
        C(PeoplesLoreMath.YoungFrom > 0f && PeoplesLoreMath.YoungFrom < PeoplesLoreMath.YoungTo && PeoplesLoreMath.YoungTo < 0.2f, "the young beast is weaned and not yet grown");
        C(PeoplesLoreMath.WalkMin == 6 && PeoplesLoreMath.WalkMax == 9, "the Long Walk: a file of six to nine");
        C(PeoplesLoreMath.Worth(1f) == PeoplesLoreMath.SalvageWorth && PeoplesLoreMath.Worth(9f) == PeoplesLoreMath.SalvageWorth * 2f && PeoplesLoreMath.Worth(0f) == PeoplesLoreMath.SalvageWorth * 0.6f
          && PeoplesLoreMath.Worth(float.NaN) == PeoplesLoreMath.SalvageWorth, "the salvage's worth by the colony's wealth, within the game's bounds");
        C(PeoplesLoreMath.Price(600f) == 240 && PeoplesLoreMath.Price(0f) == 50 && PeoplesLoreMath.Price(605f) % 10 == 0 && Math.Abs(PeoplesLoreMath.SalvagePriceShare - 0.4f) < 0.0001f,
            "the enclave sells it for two in five of its worth, to the nearest ten");
        for (int age = 0; age <= 3; age++)
        {
            var goods = PeoplesLoreMath.SalvageGoods(age);
            C(Math.Abs(goods.Sum(g => g.share) - 1f) < 0.001f && goods.All(g => g.share > 0f) && goods.Select(g => g.def).Distinct().Count() == goods.Count && goods.Any(g => g.def == "Steel"),
                $"the salvage of the age {age}: shares that come to the whole, steel always");
        }
        C(PeoplesLoreMath.SalvageGoods(0).Any(g => g.def == "Cloth") && !PeoplesLoreMath.SalvageGoods(0).Any(g => g.def == "Plasteel" || g.def == "ComponentSpacer")
          && PeoplesLoreMath.SalvageGoods(2).Any(g => g.def == "Plasteel") && PeoplesLoreMath.SalvageGoods(3).Any(g => g.def == "ComponentSpacer"),
            "what an earlier colony can use, plasteel from the industrial age, a spacer's component in the spacer age");
        C(PeoplesLoreMath.Count(1000f, 0.5f, 2f) == 250 && PeoplesLoreMath.Count(10f, 0.1f, 500f) == 1 && PeoplesLoreMath.Count(100f, 0.5f, 0f) == 0, "how many of a good its share comes to");
        C(PeoplesLoreMath.CardForm("a", "b") == "Both" && PeoplesLoreMath.CardForm("a", " ") == "Honour" && PeoplesLoreMath.CardForm(null, "b") == "Fear" && PeoplesLoreMath.CardForm("", null) == null,
            "the card's line by what they have");
        var says = new List<string> { "One.", "", "Two.", "Three." };
        C(PeoplesLoreMath.Saying(says, 7) == PeoplesLoreMath.Saying(says, 7) && PeoplesLoreMath.Saying(says, -5) != null && PeoplesLoreMath.Saying(null, 1) == null
          && Enumerable.Range(0, 30).Select(i => PeoplesLoreMath.Saying(says, i)).Distinct().Count() == 3, "a saying the same for the same people, never an empty one");

        // ================= Their lore =================

        string Dn(XElement e) => e.Element("defName")?.Value ?? "";
        XElement Ext(XElement d, string cls) => d.Element("modExtensions")?.Elements("li").FirstOrDefault(li => li.Attribute("Class")?.Value == "ColonyCouncil." + cls);
        var factions = Directory.GetFiles(Path.Combine(root, "Defs", "PeopleDefs"), "Rimfolk_People_*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements("FactionDef"))
            .ToDictionary(f => Ext(f, "PeopleExtension")!.Element("calling")!.Value);
        C(factions.Count == 23 && Founders.Keys.All(factions.ContainsKey), "twenty-three peoples, each with a founding");
        string Body(string line) => Regex.Replace(line, @"^\[[^\]]*\]\s*", "").Trim();
        foreach (var (calling, f) in factions)
        {
            string name = Dn(f);
            var paras = (f.Element("description")?.Value ?? "").Split(new[] { "\\n\\n" }, StringSplitOptions.None);
            C(paras.Length == 3 && paras[1].StartsWith("Their ways began") && paras[1].Contains(Founders[calling]) && Regex.Matches(paras[1], @"[.]").Count >= 3,
                $"{name}: its founding story, {Founders[calling]}'s, the middle of its description");
            C((f.Element("ideoDescription")?.Value ?? "").Length > 250, $"{name}: its creed tells something of its founding too");
            var ext = Ext(f, "PeopleExtension")!;
            string honours = ext.Element("honours")?.Value ?? "", fears = ext.Element("fears")?.Value ?? "";
            var sayings = ext.Element("sayings")?.Elements("li").Select(li => li.Value).ToList() ?? new List<string>();
            C(honours.Split(' ').Length >= 4 && fears.Split(' ').Length >= 4 && char.IsLower(honours[0]) && char.IsLower(fears[0]) && !honours.EndsWith(".") && !fears.EndsWith("."),
                $"{name}: what it honours and fears, as words that finish the card's sentence");
            C(sayings.Count >= 4 && sayings.All(s => s.Length <= 60 && s.EndsWith(".") && Regex.Matches(s, @"[.?]").Count == 1 && !s.Contains('!') && !s.Contains('—')),
                $"{name}: four sayings or more, one short sentence each");
        }

        // ================= Their voices =================

        // The seven's voices are in the lore's files, release D's four, release E's four and release F's four in their own.
        var lines = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Lines_PeoplesLore.xml")).Root!;
        var linesD = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Lines_PeoplesD.xml")).Root!;
        var linesE = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Lines_PeoplesE.xml")).Root!;
        var linesF = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Lines_PeoplesF.xml")).Root!;
        var linesG = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Lines_PeoplesG.xml")).Root!;
        IEnumerable<string> Pool(string pool) => new[] { lines, linesD, linesE, linesF, linesG }.SelectMany(d => d.Elements("ColonyCouncil.FlavorExtensionDef")).Where(e => e.Element("pool")!.Value == pool)
            .SelectMany(e => e.Descendants("li")).Select(li => li.Value);
        var talks = new[] { "Rimfolk_OutsiderTalks_PeoplesLore.xml", "Rimfolk_OutsiderTalks_PeoplesD.xml", "Rimfolk_OutsiderTalks_PeoplesE.xml", "Rimfolk_OutsiderTalks_PeoplesF.xml",
                "Rimfolk_OutsiderTalks_PeoplesG.xml" }
            .SelectMany(x => XDocument.Load(Path.Combine(root, "Defs", "OutsiderTalkDefs", x)).Root!.Elements("ColonyCouncil.OutsiderTalkDef")).ToList();
        var lore = new[] { "Rimfolk_Lines_PeoplesLore.xml", "Rimfolk_Lines_Peoples.xml", "Rimfolk_Lines_PeoplesC.xml", "Rimfolk_Lines_PeoplesD.xml", "Rimfolk_Lines_PeoplesE.xml",
                "Rimfolk_Lines_PeoplesF.xml", "Rimfolk_Lines_PeoplesG.xml" }
            .Select(x => Path.Combine(root, "Defs", "FlavorPoolDefs", x))
            .Concat(new[] { Path.Combine(root, "Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_PeoplesLore.xml"), Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeoplesLore.xml"),
                Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKinds_PeoplesLore.xml"), Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKinds_Peoples.xml"),
                Path.Combine(root, "Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_PeoplesD.xml"), Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeoplesD.xml"),
                Path.Combine(root, "Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_PeoplesE.xml"), Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeoplesE.xml"),
                Path.Combine(root, "Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_PeoplesF.xml"), Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeoplesF.xml"),
                Path.Combine(root, "Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_PeoplesG.xml"), Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeoplesG.xml") })
            .Select(File.ReadAllText).Aggregate((a, b) => a + "\n" + b).ToLowerInvariant().Replace("&apos;", "'");
        foreach (var (calling, f) in factions)
        {
            string tag = PeoplesMath.Tag(calling)!;
            var letters = Pool("Rimfolk_Pool_NeighbourWrites").Where(l => l.Contains(tag)).ToList();
            C(letters.Count >= 4 && letters.Any(l => l.StartsWith("[condolences")) && letters.Any(l => l.StartsWith("[warning")) && !letters.Any(l => l.StartsWith("[help,cool")),
                $"{calling}: their letters of grief and of thanks for a warning in their own words");
            var couriers = Pool("Rimfolk_CourierLines").Where(l => l.Contains(tag)).Select(Body).ToList();
            C(couriers.Count >= 2 && couriers.All(b => Regex.Matches(b, @"[.?]").Count == 1 && b.EndsWith(".")), $"{calling}: more of their messengers' words, one sentence each");
            var visits = Pool("Rimfolk_VisitorLines").Where(l => l.Contains(tag)).ToList();
            C(new[] { "arrive", "wait", "stay", "leave", "pass" }.All(m => visits.Any(l => l.StartsWith("[" + m + ","))), $"{calling}: their visitors speak at every moment of a visit");
            var outs = Pool("Rimfolk_Pool_OutsiderSays").Where(l => l.Contains(tag)).ToList();
            C(new[] { "chat", "endwarm", "faithtalk" }.All(m => outs.Any(l => l.StartsWith("[" + m + ",them,"))), $"{calling}: their words face to face, their faith's among them");
            var mine = talks.Where(t => (t.Element("when")?.Value ?? "").Split(',').Contains(tag)).ToList();
            C(mine.Count >= 3 && mine.Any(t => t.Element("uses")!.Value.Contains("know") && t.Descendants("li").Count() >= 5
                                                && t.Descendants("li").Any(li => li.Value.Contains(Founders[calling].Split(' ').Last()))),
                $"{calling}: three new pieces of talk, one of them telling how their ways began");
            var sayings = Ext(f, "PeopleExtension")!.Element("sayings")!.Elements("li").Select(li => li.Value.TrimEnd('.').ToLowerInvariant()).ToList();
            C(sayings.Count(s => lore.Contains(s)) >= 2, $"{calling}: their sayings are said in their lines");
        }
        var disputation = lines.Elements("ColonyCouncil.FlavorPoolDef").Single(e => Dn(e) == "Rimfolk_Pool_PlayDisputation");
        C(disputation.Descendants("li").Count() >= 8, "the night of the fire has its own words");
        C(File.ReadAllText(Path.Combine(root, "Tools", "Tests", "ImmersionTests.cs")).Contains("\"Rimfolk_Pool_PlayDisputation\""), "and they are checked as words said over a head");
        var newTexts = new[] { "Rimfolk_Lines_PeoplesLore.xml" }.Select(x => Path.Combine(root, "Defs", "FlavorPoolDefs", x))
            .Concat(new[] { Path.Combine(root, "Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_PeoplesLore.xml"), Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeoplesLore.xml"),
                Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKinds_PeoplesLore.xml"), Path.Combine(root, "Patches", "Rimfolk_PeoplesLore.xml"),
                Path.Combine(root, "Defs", "ThoughtDefs", "Rimfolk_Thoughts_PeoplesLore.xml"), Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeoplesLore.xml") });
        foreach (var file in newTexts)
        {
            var texts = XDocument.Load(file).Descendants().Where(e => !e.HasElements).Select(e => Body(e.Value)).Where(v => v.Contains(' ')).ToList();
            C(texts.All(t => !t.Contains('—') && !t.Contains('!')) && texts.All(t => !Regex.IsMatch(t, @"\bin \d+ (days?|hours?)\b")), $"{Path.GetFileName(file)}: plain words, no countdowns");
        }

        // ================= Their touchpoints =================

        var events = XDocument.Load(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeoplesLore.xml")).Root!.Elements("IncidentDef").ToDictionary(Dn);
        C(events.Keys.SequenceEqual(new[] { "Rimfolk_Evt_OrderNurse", "Rimfolk_Evt_LongWalk", "Rimfolk_Evt_Disputation", "Rimfolk_Evt_EnclaveSalvage", "Rimfolk_Evt_RiverNets" }),
            "five new callers at the gate");
        bool Has(XElement d, string cls) => d.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil." + cls);
        string People(XElement d) => Ext(d, "HappeningSetting")?.Element("people")?.Value;
        var nurse = events["Rimfolk_Evt_OrderNurse"];
        C(nurse.Element("workerClass")!.Value == "ColonyCouncil.HappeningWorker_OrderNurse" && Has(nurse, "DilemmaEffect_Lesson") && Has(nurse, "DilemmaEffect_SendAway")
          && Ext(nurse, "HappeningExtension")!.Element("lines")!.Elements("li").All(l => l.Value.StartsWith("[nurse]")), "the order's night nurse: from bed to bed, the doctor alongside if the colony likes, or sent away");
        var walk = events["Rimfolk_Evt_LongWalk"];
        C(walk.Element("workerClass")!.Value == "ColonyCouncil.HappeningWorker_LongWalk" && People(walk) == "calling:pilgrims" && Ext(walk, "HappeningSetting")!.Element("visitAt")!.Value == "grave"
          && Ext(walk, "HappeningExtension")!.Element("count")!.Value == PeoplesLoreMath.WalkMin + "~" + PeoplesLoreMath.WalkMax
          && walk.Descendants("li").Count(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_Procession") == 2
          && Ext(walk, "HappeningSetting")!.Descendants("need").Any(x => x.Value == "grave"), "the Long Walk: the pilgrims at the graves, walked out to or seen off, the graves' answer only with graves");
        var dispute = events["Rimfolk_Evt_Disputation"];
        var gathering = dispute.Descendants("li").Single(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_Gathering");
        C(People(dispute) == "calling:scholars" && gathering.Element("guests")?.Value == "stranger" && gathering.Element("pool")?.Value == "Rimfolk_Pool_PlayDisputation"
          && gathering.Element("hours")?.Value.StartsWith("18") == true && int.Parse(dispute.Element("minRefireDays")!.Value) >= 60,
            "the night of the fire: an evening's gathering with the fellow as its guest, once a year at most");
        var salvage = events["Rimfolk_Evt_EnclaveSalvage"];
        var salvageTexts = Ext(salvage, "HappeningExtension")!.Element("texts")!.Elements("li").Select(li => li.Value).ToList();
        C(salvage.Element("workerClass")!.Value == "ColonyCouncil.HappeningWorker_EnclaveSalvage" && Has(salvage, "DilemmaEffect_EnclaveSalvage")
          && salvage.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_Consume" && li.Element("by")?.Value == "Outsiders"),
            "the enclave's salvage: bought, it comes down by drop pod and the silver goes to their runner");
        C(salvageTexts.Any(t => t.StartsWith("[runner]")) && salvageTexts.Any(t => t.StartsWith("[industrial")) && salvageTexts.Any(t => t.StartsWith("[spacer")),
            "in every age: a runner on foot for a colony with no radio");
        var nets = events["Rimfolk_Evt_RiverNets"];
        C(nets.Element("workerClass")!.Value == "ColonyCouncil.HappeningWorker_RiverNets" && People(nets) == "calling:fishers" && Has(nets, "DilemmaEffect_Goods")
          && nets.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_Lesson" && li.Element("skill")?.Value == "Crafting")
          && !Ext(nets, "HappeningExtension")!.Element("seasons")!.Elements("li").Any(s => s.Value == "Winter"), "nets on the bank: the boatmen's knots taught and their thanks left, never in the frozen months");
        C(new[] { nurse, walk, dispute, nets }.All(d => !string.IsNullOrEmpty(Ext(d, "HappeningExtension")!.Element("journal")?.Value) || d == nets), "the colonists write about them");

        // The league's winter quarters and the herders' young beast: camps.
        var camp = XDocument.Load(Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKinds_PeoplesLore.xml")).Root!.Elements("ColonyCouncil.CampKindDef").Single();
        C(Dn(camp) == "Rimfolk_Camp_LeagueWinter" && camp.Element("calling")?.Value == "traders" && camp.Element("trader")?.Value == "true"
          && camp.Element("seasons")!.Elements("li").All(s => s.Value.Contains("Winter")), "the league's winter quarters: only theirs, only in the winter, trading all the while");
        C(camp.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_CampGuards") && camp.Element("asks")!.Elements("li").Any(a => a.Element("key")?.Value == "book"),
            "their wardens stand with the colony if asked, and the colony is written in the Waystone Book");
        var waystone = camp.Element("leaveBehind")!.Elements("li").Single();
        C(camp.Element("farewellWalk")?.Element("gift")?.Value == "waystone" && waystone.Element("at")?.Value == "gate" && waystone.Element("minified")?.Value == "true"
          && float.Parse(waystone.Element("maxTension")!.Value, System.Globalization.CultureInfo.InvariantCulture) == PeoplesLoreMath.GoodStay
          && camp.Element("farewell")!.Elements("li").Any(l => l.Value.StartsWith("[left:waystone]")), "after a stay that went well, a waystone in its crate at the gate, told in the farewell");
        var herd = XDocument.Load(Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKinds_Peoples.xml")).Root!.Elements("ColonyCouncil.CampKindDef").Single();
        var young = herd.Element("leaveBehind")?.Elements("li").SingleOrDefault();
        C(young?.Element("young")?.Value == "true" && young.Element("at")?.Value == "gate" && herd.Element("farewellWalk")?.Element("gift")?.Value == young.Element("key")?.Value
          && float.Parse(young.Element("maxTension")!.Value, System.Globalization.CultureInfo.InvariantCulture) == PeoplesLoreMath.GoodStay
          && herd.Element("farewell")!.Elements("li").Count(l => l.Value.StartsWith("[left:young]")) >= 2, "the herders' young beast handed over at the gate after a good winter, told in the farewell");
        C(FactsMath.RegionTags.Contains(FactsMath.CampTag(Dn(camp))), "talk knows one of the league's winter quarters");

        // The patch: callers that prefer them, each with a text in their voice.
        var patch = XDocument.Load(Path.Combine(root, "Patches", "Rimfolk_PeoplesLore.xml")).Root!;
        var allDefs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).SelectMany(f => XDocument.Load(f).Root!.Elements()).Select(Dn).Where(x => x != "").ToHashSet();
        foreach (var op in patch.Elements("Operation"))
        {
            var m = Regex.Match(op.Element("xpath")?.Value ?? "", "defName=\"([^\"]+)\"");
            C(op.Attribute("Class")?.Value == "PatchOperationConditional" && m.Success && allDefs.Contains(m.Groups[1].Value), $"the patch reaches {m.Groups[1].Value}, conditionally");
        }
        var preferred = patch.Descendants("prefers").Select(p => p.Value).ToList();
        C(preferred.Count == 5 && preferred.All(PeoplesMath.IsCalling) && preferred.Distinct().Count() == 5, "five callers prefer five of the peoples");
        C(preferred.All(p => patch.Descendants("li").Any(li => li.Value.StartsWith("[" + PeoplesMath.Tag(p) + "]"))), "each says it in that people's voice when they are the ones who came");

        // ================= Their faiths =================

        foreach (var (calling, f) in factions)
        {
            string name = Dn(f);
            var memes = f.Element("forcedMemes")!.Elements("li").ToList();
            C(memes.All(li => MemeTags.ContainsKey(li.Value) && li.Attribute("MayRequire")?.Value == ModOf(li.Value)), $"{name}: every meme is one read from its mod, named only with that mod");
            C(memes.Count <= 4, $"{name}: no more memes than the game allows a faith");
            var tags = memes.SelectMany(li => MemeTags.TryGetValue(li.Value, out var t) ? t.Distinct() : Array.Empty<string>()).ToList();
            C(tags.Count == tags.Distinct().Count(), $"{name}: no two of its memes exclude each other");
            C(f.Element("structureMemeWeights")!.Elements().All(e => e.Attribute("MayRequire")?.Value == ModOf(e.Name.LocalName)), $"{name}: its structures soft on their mods");
        }
        C(factions.Values.Count(f => f.Element("forcedMemes")!.Elements("li").Count() >= 3) >= 5, "where their faiths were thin, a meme more from the player's mods");

        // ================= Their names for peoples that arise =================

        var arise = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Peoples_Arising.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        var names = arise.Keys.Where(k => k.StartsWith("CC_Arise_Name_") && !Regex.IsMatch(k, @"_\d$")).ToList();
        C(names.Count >= 11 && names.All(k => arise.ContainsKey(k + "_2") && arise.ContainsKey(k + "_3") && arise[k + "_3"].Contains("{WHAT}")),
            "every name a people that arises takes has a third form carrying its ways");
        C(new[] { "Lamp", "Waystone", "Herd", "High Bank", "Manifest", "Handcart", "Long Grass" }.All(w => names.Any(k => arise[k + "_3"].Contains(w))),
            "the lamp, the waystone, the herd, the high bank, the manifest, the handcart and the long grass among them");
        C(new[] { "refugees_herders", "refugees_healers", "refugees_traders", "refugees_fishers", "exiles_scholars", "splinter_pilgrims", "league_brokers" }
            .All(k => arise.ContainsKey("CC_Arise_" + k + "_2")), "how a new people of their ways lives, told a second way that carries them");

        // ================= Their pages and the design =================

        string Page(string file) => string.Join("\n", XDocument.Load(Path.Combine(root, "Defs", "AlmanacArticleDefs", file)).Descendants().Where(e => !e.HasElements).Select(e => e.Value));
        string pages = Page("Almanac_Peoples.xml") + Page("Almanac_PeoplesC.xml") + Page("Almanac_PeoplesD.xml") + Page("Almanac_PeoplesE.xml") + Page("Almanac_PeoplesF.xml")
                       + Page("Almanac_PeoplesG.xml");
        string design = File.ReadAllText(Path.Combine(root, "docs", "design", "peoples.md"));
        C(Founders.Values.All(pages.Contains) && Founders.Values.All(design.Contains), "every founding on the Almanac's pages and in the design");
        C(pages.Contains("Where their ways began") && pages.Contains("bed to bed") && pages.Contains("winter quarters") && pages.Contains("walking day") && pages.Contains("night of the fire")
          && pages.Contains("salvage") && pages.Contains("nets") && pages.Contains("young beast"), "and every new touchpoint");

        // ================= The seams =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string code = File.ReadAllText(Path.Combine(src, "Peoples", "PeoplesLore.cs"));
        string look = File.ReadAllText(Path.Combine(src, "Camps", "CampLook.cs"));
        C(code.Contains("VisitorServices.Dose(ctx.map, x, who, hediff") && code.Contains("DropPodUtility.DropThingsNear(spot, map, things") && code.Contains("CardSections.RegisterLines(CardKind.People, CardSlot.Story"),
            "the hospitaller seen at each bedside, the salvage by drop pod, their card");
        C(!Regex.IsMatch(code + File.ReadAllText(Path.Combine(src, "Peoples", "PeoplesLoreMath.cs")), @"Sched\.|Scribe_|ExposeData|\[HarmonyPatch|Harm\."),
            "no clock, nothing saved, no patch of their own, nobody hurt");
        C(look.Contains("PeoplesLoreMath.LeavesYoung(tension, g.maxTension)") && look.Contains("GiveYoung(gift, cell, placed)") && look.Contains("!Earned(gateGift)"),
            "a camp's gift kept for a good stay, and no walk to the gate without it");
        C(File.ReadAllText(Path.Combine(src, "Peoples", "PeopleDefs.cs")).Contains("public string honours, fears;"), "what they honour and fear in their template");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
