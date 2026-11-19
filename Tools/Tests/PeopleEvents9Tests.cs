using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The peoples' own events, round 9 (Peoples/Events9/; docs/peoples-events-proposals.md, items 84 to 94): their pure rules (the Lattice's
/// bandwidth and dreams, the feral mech's kind and pay, the mech yard's work and hire, the hum's takers, the void's word and the sanity
/// given back, the long break, the ring's evenings and nights, the vault's fight, its last part and a year's supply, the console's
/// evenings, the scouts' lessons), their defs and words, the player's mods they draw on, and the seams with the game and rounds 1 to 8.
/// </summary>
public static class PeopleEvents9Tests
{
    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the peoples' events, round 9: " + what);
        }
        float F(string v) => float.Parse(v, CultureInfo.InvariantCulture);
        const int H = PeopleEvents9Math.HourTicks;

        // ================= 84. Linked into the Lattice =================

        C(PeopleEvents9Math.Bandwidth(true) == 2 && PeopleEvents9Math.Bandwidth(false) == 1, "+2 bandwidth as proposed; a link for work only +1");
        C(PeopleEvents9Math.Dream(0.1f, 0.2f) == 1 && PeopleEvents9Math.Dream(0.1f, 0.8f) == -1 && PeopleEvents9Math.Dream(0.5f, 0.2f) == 0 && PeopleEvents9Math.Dream(float.NaN, 0.2f) == 0,
            "now and then another's dream, either way");
        C(PeopleEvents9Math.DreamChance <= 0.2f && PeopleEvents9Math.GoodDreamShare == 0.5f && PeopleEvents9Math.DreamHour(6) && !PeopleEvents9Math.DreamHour(12),
            "about one night in seven, half good and half bad, told at dawn");
        C(PeopleEvents9Math.LinkTicks == 2 * H && PeopleEvents9Math.LinkGoodwill > 0 && PeopleEvents9Math.WorkLinkGoodwill > 0, "two hours' work with the array");

        // ================= 85. A mech gone feral =================

        C(PeopleEvents9Math.FeralPick(new[] { 45f, 75f, 150f }, 100f) == 1 && PeopleEvents9Math.FeralPick(new[] { 45f, 75f, 150f }, 10f) == 0
          && PeopleEvents9Math.FeralPick(new float[0], 100f) == -1 && PeopleEvents9Math.FeralPick(new[] { float.NaN, 75f }, 500f) == 1,
            "the strongest of the light war mechs within the home's share of threat, else the weakest");
        C(PeopleEvents9Math.FeralPay(1000f) == 100 && PeopleEvents9Math.FeralPay(100f) == 60 && PeopleEvents9Math.FeralPay(10000f) == 300 && PeopleEvents9Math.FeralPay(float.NaN) == 60,
            "the Lattice pays a tenth of the mech's worth for the trouble, within reason");
        C(PeopleEvents9Math.RelinkGoodwill > 0 && PeopleEvents9Math.DownGoodwill == 0 && PeopleEvents9Math.FeralRoamHours >= 24 && PeopleEvents9Math.FeralPoints <= 0.5f && PeopleEvents9Math.FeralChance <= 1f,
            "relinked they pay; destroyed, nothing either way; left be, it wanders off after a day and more");

        // ================= 86. A mech yard =================

        C(PeopleEvents9Math.YardPeople == 3 && PeopleEvents9Math.YardMechsMin == 4 && PeopleEvents9Math.YardMechsMax == 6 && PeopleEvents9Math.YardMinDays == 5 && PeopleEvents9Math.YardMaxDays == 7,
            "a mech-tender and two link-hands with four to six mechs, five to seven days");
        C(PeopleEvents9Math.Miners(4) == 2 && PeopleEvents9Math.Miners(5) == 2 && PeopleEvents9Math.Miners(1) == 1 && PeopleEvents9Math.Miners(0) == 0, "half the mechs cut rock, the rest haul it");
        C(PeopleEvents9Math.WantsMending(80, 100) && !PeopleEvents9Math.WantsMending(95, 100) && !PeopleEvents9Math.WantsMending(0, 0) && PeopleEvents9Math.YardHour(10) && !PeopleEvents9Math.YardHour(22)
          && PeopleEvents9Math.HireHours == 24, "the mechs at work by day; hired a day, they mend what is worn");
        C(PeopleEvents9Math.YardFeralChance <= 0.3f && PeopleEvents9Math.YardBandChance <= 0.3f && PeopleEvents9Math.RockPerDay <= 8, "it turns rarely; a few loads of rock a day");

        // ================= 87. The hum deepened =================

        C(PeopleEvents9Math.HumTaker(2, 1f) && !PeopleEvents9Math.HumTaker(6, 2f) && PeopleEvents9Math.HumTaker(0, 1.3f) && !PeopleEvents9Math.HumTaker(0, 1f) && !PeopleEvents9Math.HumTaker(0, float.NaN),
            "a psylink below six goes a level higher; a sensitive colonist without one wakes a first");
        C(PeopleEvents9Math.HumHour(18) && !PeopleEvents9Math.HumHour(10) && PeopleEvents9Math.HumTicks == 5 * H && PeopleEvents9Math.HumArtMinWorth > 0f, "the cantor comes at evening and sits through the night");

        // ================= 88. The void listens back =================

        C(PeopleEvents9Math.Shaken(0.3f, false) && !PeopleEvents9Math.Shaken(0.7f, false) && !PeopleEvents9Math.Shaken(-1f, false) && PeopleEvents9Math.Shaken(-1f, true),
            "shaken by the void: Insanity's sanity below half, or a memory of the void");
        C(Math.Abs(PeopleEvents9Math.SanityAfter(0.2f, 1f) - 0.5f) < 0.001f && PeopleEvents9Math.SanityAfter(0.9f, 1f) == 1f && float.IsNaN(PeopleEvents9Math.SanityAfter(float.NaN, 1f)),
            "some of their sanity returns, never past its whole");
        C(PeopleEvents9Math.VoidSitTicks == 8 * H && PeopleEvents9Math.VoidListenTicks == H && PeopleEvents9Math.ForetoldDays == 30, "a day at their side, or an hour at the edge; the word holds a month");

        // ================= 89. A mind that shouts =================

        C(PeopleEvents9Math.LongBreak(3 * H, false) && !PeopleEvents9Math.LongBreak(3 * H, true) && !PeopleEvents9Math.LongBreak(2 * H, false), "a long break that turns on nobody");
        C(PeopleEvents9Math.ShoutSitTicks == 2 * H && PeopleEvents9Math.ShoutChance <= 0.5f && PeopleEvents9Math.ShoutGapDays >= 15 && PeopleEvents9Math.MaxShoutsRolled >= 4,
            "two hours beside them; not every break, not soon again");

        // ================= 90. A listening ring =================

        C(PeopleEvents9Math.RingMin == 3 && PeopleEvents9Math.RingMax == 5 && PeopleEvents9Math.RingMinDays == 3 && PeopleEvents9Math.RingMaxDays == 5, "three to five of the Choir, three to five days");
        C(PeopleEvents9Math.SingHour(19) && !PeopleEvents9Math.SingHour(12) && PeopleEvents9Math.FeltHour(22) && PeopleEvents9Math.NightHour(23) && PeopleEvents9Math.NightHour(2) && !PeopleEvents9Math.NightHour(12),
            "they sing at dusk, felt late in the evening; loud work heard at night");
        C(PeopleEvents9Math.StrangeChance <= 0.5f && PeopleEvents9Math.LoudRadius >= 15f && PeopleEvents9Math.RingSitTicks == 2 * H, "the song turns strange now and then with the void stirring");

        // ================= 91. The seal broken =================

        C(PeopleEvents9Math.Won(0, 2) && !PeopleEvents9Math.Won(1, 2) && !PeopleEvents9Math.Won(0, 0), "won when nothing that came up stands and some of the colony's still do");
        C(PeopleEvents9Math.SealGoodwill == 15 && PeopleEvents9Math.SealDays == 5 && PeopleEvents9Math.InsectChance > 0f && PeopleEvents9Math.InsectChance < 1f && PeopleEvents9Math.SecurityMax <= 4
          && PeopleEvents9Math.SealPoints <= 1f, "+15 as proposed; five days to set out; insects or mechs; a few of their security beside the colony's");

        // ================= 92. The last part =================

        C(PeopleEvents9Math.PartKind("Eye") == "eye" && PeopleEvents9Math.PartKind("Foot") == "leg" && PeopleEvents9Math.PartKind("Hand") == "arm" && PeopleEvents9Math.PartKind("Jaw") == null
          && PeopleEvents9Math.BionicRecipe("eye") == "InstallBionicEye" && PeopleEvents9Math.BionicRecipe("arm") == "InstallBionicArm" && PeopleEvents9Math.BionicRecipe("leg") == "InstallBionicLeg"
          && PeopleEvents9Math.BionicRecipe(null) == null, "an eye or a limb lost, the game's own bionic for it");
        C(PeopleEvents9Math.PartComponents(1f) == 20 && PeopleEvents9Math.PartComponents(0.1f) == 15 && PeopleEvents9Math.PartComponents(9f) == 30 && PeopleEvents9Math.PartFood(1f) == 300
          && PeopleEvents9Math.PartFood(float.NaN) == 300, "a large supply of components and food, by the colony's wealth");
        float yearLeft = MemoryMath.Strength(PeopleEvents9Math.YearOfSupplyWeight, PeopleAgesMath.YearTicks) * PeopleAgesMath.GiftSilverPerWeight;
        C(PeopleEvents9Math.YearOfSupplyWeight <= 30 && yearLeft >= 0.95f * PeopleAgesMath.SupplyFull, $"the supply holds the vault's age for a year ({yearLeft:0} silver of a {PeopleAgesMath.SupplyFull:0} year left at its end)");

        // ================= 93. The console that talks to nobody =================

        C(PeopleEvents9Math.ConsoleEvenings == 3 && PeopleEvents9Math.ConsoleTalkTicks == H && PeopleEvents9Math.ConsoleHour(19) && !PeopleEvents9Math.ConsoleHour(12), "a few evenings, an hour each at dusk");
        C(PeopleEvents9Math.TellsOfCache(0.2f) && !PeopleEvents9Math.TellsOfCache(0.7f) && !PeopleEvents9Math.TellsOfCache(float.NaN) && PeopleEvents9Math.AskComponents(1f) == 6
          && PeopleEvents9Math.AskComponents(9f) == 10 && PeopleEvents9Math.AskMedicine(0.1f) == 3, "at the end, an ask or a cache; the ask within reason");
        C(PeopleEvents9Math.CacheTilesMin >= 2 && PeopleEvents9Math.CacheTilesMax <= 10 && PeopleEvents9Math.CacheDays >= 15, "the cache a few tiles off, kept a while");

        // ================= 94. Surface scouts =================

        C(PeopleEvents9Math.LessonOn(0) == "Plants" && PeopleEvents9Math.LessonOn(1) == "Animals" && PeopleEvents9Math.LessonOn(2) == "Shooting" && PeopleEvents9Math.LessonOn(3) == "Plants"
          && PeopleEvents9Math.LessonOn(-1) == "Shooting", "plants, animals and shooting in turn");
        C(PeopleEvents9Math.LessonHour(13) && !PeopleEvents9Math.LessonHour(10) && PeopleEvents9Math.LessonTicks == 2 * H && PeopleEvents9Math.ScoutPeople == 3 && PeopleEvents9Math.ScoutDays == 7,
            "three scouts a week, taught each afternoon");
        C(PeopleEvents9Math.OddsPrice(123f) == 120 && PeopleEvents9Math.OddsPrice(float.NaN) == 10 && PeopleEvents9Math.SkySickChance <= 0.5f && PeopleEvents9Math.LostChance <= 0.5f
          && PeopleEvents9Math.MaulChance <= 0.3f, "their odds and ends at their worth; it turns now and then");

        // ================= Their defs =================

        var events = XDocument.Load(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents9.xml")).Root!.Elements("IncidentDef").ToDictionary(Dn);
        C(events.Keys.SequenceEqual(new[] { "Rimfolk_Evt_LatticeLinked", "Rimfolk_Evt_FeralMech", "Rimfolk_Evt_MechYard", "Rimfolk_Evt_HumDeepened", "Rimfolk_Evt_VoidListens",
            "Rimfolk_Evt_MindShouts", "Rimfolk_Evt_ListeningRing", "Rimfolk_Evt_SealBroken", "Rimfolk_Evt_VaultLastPart", "Rimfolk_Evt_ConsoleTalk", "Rimfolk_Evt_ConsoleAsk",
            "Rimfolk_Evt_SurfaceScouts" }), "twelve events: ten drawn, two fired when their moment comes");
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { src }.Concat(parts).ToArray()));
        string dir = Path.Combine(src, "Peoples", "Events9");
        string code = string.Join("\n", Directory.GetFiles(dir, "*.cs").Where(f => !f.EndsWith("PeopleEvents9Math.cs")).OrderBy(f => f).Select(File.ReadAllText));
        string everyCode = string.Join("\n", Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        var callings = new Dictionary<string, string>
        {
            ["Rimfolk_Evt_LatticeLinked"] = "Mechanitors", ["Rimfolk_Evt_FeralMech"] = "Mechanitors", ["Rimfolk_Evt_MechYard"] = "Mechanitors",
            ["Rimfolk_Evt_HumDeepened"] = "Listeners", ["Rimfolk_Evt_VoidListens"] = "Listeners", ["Rimfolk_Evt_MindShouts"] = "Listeners", ["Rimfolk_Evt_ListeningRing"] = "Listeners",
            ["Rimfolk_Evt_SealBroken"] = "Vaultfolk", ["Rimfolk_Evt_VaultLastPart"] = "Vaultfolk", ["Rimfolk_Evt_ConsoleTalk"] = "Vaultfolk", ["Rimfolk_Evt_ConsoleAsk"] = "Vaultfolk",
            ["Rimfolk_Evt_SurfaceScouts"] = "Vaultfolk"
        };
        var fired = new Dictionary<string, string>
        {
            ["Rimfolk_Evt_MindShouts"] = "PeopleEvents2.Fire(EventDef, map, f)", ["Rimfolk_Evt_ConsoleAsk"] = "PeopleEvents2.Fire(AskEventDef, map, f)"
        };
        foreach (var (name, e) in events)
        {
            string worker = e.Element("workerClass")!.Value.Replace("ColonyCouncil.", "");
            var m = Regex.Match(code, @"class " + worker + @" : HappeningWorker_Visitors\s*\{(.*?)\n    \}", RegexOptions.Singleline);
            C(m.Success, $"{name}: its worker is the round's own");
            C(m.Success && m.Groups[1].Value.Contains("PeoplesMath." + callings[name]) && m.Groups[1].Value.Contains("PeoplesD.Friendly("), $"{name}: only from a people of its calling at peace with the colony");
            var effects = e.Descendants("li").Select(li => li.Attribute("Class")?.Value).Where(v => v != null && v.StartsWith("ColonyCouncil.DilemmaEffect_")).Select(v => v.Replace("ColonyCouncil.", "")).Distinct();
            C(effects.All(x => Regex.IsMatch(everyCode, @"class " + x + @"\b")), $"{name}: every answer's effect is real");
            var ext = Ext(e, "HappeningExtension")!;
            float chance = F(e.Element("baseChance")!.Value);
            if (fired.TryGetValue(name, out var how)) C(chance == 0f && code.Contains(how), $"{name}: never drawn, fired only when its moment comes");
            else
                C(chance >= 0.2f && chance <= 0.6f && int.Parse(e.Element("minRefireDays")!.Value) >= 30 && int.Parse(e.Element("earliestDay")!.Value) >= 10,
                    $"{name}: a small, spread chance, not soon again, not the first moment it is free");
            C(ext.Element("group")?.Value == "Visitors", $"{name}: among the visitors, so the storytellers' lean on visitors reads it");
            var texts = ext.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
            bool AgeText(int a) => texts.Any(t => Regex.Match(t, @"^\[([^\]]*)\]").Groups[1].Value.Split('|', ',').Contains("age" + a));
            C(texts.Count >= 5 && Enumerable.Range(0, 4).All(AgeText), $"{name}: five or more texts, every age in its own words");
            var options = ext.Element("options")!.Elements("li").ToList();
            C(options.Count == 3 && ext.Element("defaultOption")?.Value == "2", $"{name}: three answers, no answer the last");
            C(options.Last().Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_SendAway"), $"{name}: the last answer sends them on");
            var lineTags = ext.Element("lines")?.Elements("li").Select(l => Regex.Match(l.Value, @"^\[([^\],]+)").Groups[1].Value).Distinct().ToList() ?? new List<string>();
            C(lineTags.Count > 0 && lineTags.All(t => code.Contains("\"" + t + "\"")), $"{name}: every line's moment is one the code says it at");
            C(ext.Element("lines")?.Elements("li").All(l => Regex.Matches(Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", ""), @"[.?](\s|$)").Count == 1) ?? true, $"{name}: every spoken line one sentence");
            var silver = options.SelectMany(o => o.Descendants("li")).Where(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_CarryOut" && li.Element("what")?.Value == "silver").ToList();
            C(silver.All(li => li.Element("steward")?.Value == "false"), $"{name}: the silver carried out is the sum the card says");
        }
        string Mods(string ev) => string.Join(",", Ext(events[ev], "HappeningExtension")!.Element("mods")?.Elements("li").Select(l => l.Value) ?? Enumerable.Empty<string>());
        C(new[] { "Rimfolk_Evt_LatticeLinked", "Rimfolk_Evt_FeralMech", "Rimfolk_Evt_MechYard" }.All(x => Mods(x) == "Ludeon.RimWorld.Biotech") && Mods("Rimfolk_Evt_HumDeepened") == "Ludeon.RimWorld.Royalty"
          && Mods("Rimfolk_Evt_VoidListens") == "Ludeon.RimWorld.Anomaly" && Mods("Rimfolk_Evt_SealBroken") == "" && Mods("Rimfolk_Evt_SurfaceScouts") == "",
            "the Lattice's mechs need Biotech, the hum Royalty's psylinks, the void Anomaly; the Choir's other ways and the Vault's need nothing");
        C(new[] { "Rimfolk_Evt_FeralMech", "Rimfolk_Evt_SealBroken" }.All(x => Ext(events[x], "HappeningExtension")!.Element("battle")?.Value == "true")
          && code.Contains("IncidentWorker_Happening.BattlesAllowed("), "the fights come only where the colony's settings allow fights");
        var console = Ext(events["Rimfolk_Evt_ConsoleTalk"], "HappeningExtension")!.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
        C(console.Where(t => t.StartsWith("[radio")).All(t => !t.Contains("{X_")) && console.Where(t => t.Contains("{X_")).All(t => t.StartsWith("[letters")) && console.All(t => t.StartsWith("[")),
            "the overseer by radio has nobody at the gate; a colony without a radio gets a runner with letters");
        var feralLast = Ext(events["Rimfolk_Evt_FeralMech"], "HappeningExtension")!.Element("options")!.Elements("li").Select(o => o.Descendants("how").SingleOrDefault()?.Value).ToList();
        C(feralLast.SequenceEqual(new[] { "relink", "down", "home" }) && code.Contains("Faction.OfMechanoids, PawnGenerationContext.NonPlayer")
          && code.Contains("new LordJob_AssaultThings(ctx.faction, new List<Thing> { ctx.pet }"), "the feral mech answers to nobody; relinked at the gate, or taken down with the wardens beside the colony's people");

        // The camps.
        var campKinds = XDocument.Load(Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKinds_PeopleEvents9.xml")).Root!.Elements("ColonyCouncil.CampKindDef").ToDictionary(Dn);
        C(campKinds.Keys.SequenceEqual(new[] { "Rimfolk_Camp_MechYard", "Rimfolk_Camp_ListeningRing", "Rimfolk_Camp_SurfaceScouts" }), "three camps of their own");
        foreach (var (name, k) in campKinds)
        {
            string worker = k.Element("workerClass")!.Value.Replace("ColonyCouncil.", "");
            C(F(k.Element("weight")!.Value) == 0f && code.Contains("class " + worker + " : CampWorker"), $"{name}: never on the camps' own draw; ways of its own ({worker})");
            C(k.Descendants("li").Where(li => li.Parent?.Name.LocalName is "lines" or "momentLines").All(l => Regex.Matches(Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", ""), @"[.?](\s|$)").Count == 1),
                $"{name}: every spoken line one sentence");
            var moments = k.Element("momentLines")?.Elements("li").Select(l => Regex.Match(l.Value, @"^\[([^\]]+)\]").Groups[1].Value).Distinct().ToList() ?? new List<string>();
            C(moments.All(t => code.Contains("\"" + t + "\"")), $"{name}: every moment line is said at a moment the code has");
            var marks = k.Element("farewell")!.Elements("li").SelectMany(l => Regex.Match(l.Value, @"^\[([^\]]+)\]").Groups[1].Value.Split(',')).Where(t => t.Length > 0).Distinct().ToList();
            C(marks.All(t => code.Contains("marks.Add(\"" + t + "\")")), $"{name}: every farewell is for a mark the code gives");
            var askKeys = k.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).ToList();
            C(askKeys.Count(a => code.Contains("case \"" + a + "\"")) >= 1, $"{name}: its own asks are minded by its worker");
            C(k.Element("leaves")?.Value == "NoTrace", $"{name}: nothing of theirs left behind but what they say they leave");
        }
        var yard = campKinds["Rimfolk_Camp_MechYard"];
        C(yard.Element("calling")!.Value == "mechanitors" && yard.Element("people")!.Value == "3~3" && yard.Element("days")!.Value == "5~7"
          && yard.Element("asks")!.Elements("li").First().Element("key")!.Value == "hire" && code.Contains("CampTake.PlainRock(rock.def)") && code.Contains("m.DestroyMined(mech)")
          && code.Contains("HaulAIUtility.HaulToCellStorageJob(") && code.Contains("CampRaids.Send(c, enemy, false, true)") && code.Contains("t.SetForbidden(false, false)"),
            "the mech yard: a mech-tender and two link-hands; the mechs cut plain rock by the camps' rule and leave it; hired, they haul and mend; a band may come for the mechs");
        var ring = campKinds["Rimfolk_Camp_ListeningRing"];
        C(ring.Element("calling")!.Value == "listeners" && ring.Element("people")!.Value == "3~5" && ring.Element("days")!.Value == "3~5"
          && ring.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).SequenceEqual(new[] { "sit", "quiet" })
          && ring.Element("asks")!.Elements("li").Last().Element("theyAsk")?.Value == "true" && code.Contains("PeoplesGMath.Felt(") && code.Contains("ChoirListening.VoidStirs()")
          && code.Contains("QuietSite(map, hot)") && code.Contains("OffsetPsyfocusDirectly(1f)"),
            "the listening ring: three to five, a few days at the quietest place; felt by the listening's rule; psycasters' focus refilled; they ask for quiet at night; the void turns the song");
        var scouts = campKinds["Rimfolk_Camp_SurfaceScouts"];
        C(scouts.Element("calling")!.Value == "vaultfolk" && scouts.Element("people")!.Value == "3~3" && scouts.Element("days")!.Value == "7~7" && F(scouts.Element("joinChance")!.Value) >= 0.1f
          && scouts.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).Take(3).SequenceEqual(PeopleEvents9Math.Lessons.Select(l => "lesson" + l))
          && scouts.Element("asks")!.Elements("li").Take(3).All(a => a.Element("once")?.Value == "false" && a.Element("theyAsk")?.Value == "true")
          && code.Contains("VaultSupply.Noted(c.faction, PeopleEvents9Math.LessonWorth") && code.Contains("FactionMemory.Note(c.faction, \"learnedHere\""),
            "the surface scouts: three a week; taught plants, animals and shooting each afternoon, remembered as supply and learning; one may ask to stay");

        // Their jobs, moods and the link.
        var misc = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_PeopleEvents9_Misc.xml")).Root!.Elements().ToList();
        var jobs = misc.Where(e => e.Name.LocalName == "JobDef").ToList();
        var own = jobs.Where(j => j.Element("driverClass")!.Value == "ColonyCouncil.JobDriver_PEv9Work").ToList();
        C(own.Count == 13 && own.All(j => code.Contains("\"" + Dn(j) + "\"") && new[] { "look", "quiet", "sparks", "cross", "dust", "hum" }.Contains(j.Descendants("mark").Single().Value)),
            "thirteen jobs of the round's own work at a place, each given by its code, each with its mark");
        C(own.Where(j => j.Descendants("follow").SingleOrDefault()?.Value == "true").Select(Dn).SequenceEqual(new[] { "Rimfolk_PEv9_SitWith" }) && code.Contains("JumpToToil(go)"),
            "sitting with someone follows them if they wander");
        var workOn = jobs.Where(j => j.Element("driverClass")!.Value == "ColonyCouncil.JobDriver_PeopleWorkOn").ToList();
        C(workOn.Select(Dn).SequenceEqual(new[] { "Rimfolk_PEv9_Link", "Rimfolk_PEv9_Hum", "Rimfolk_PEv9_Bionic" })
          && workOn.All(j => code.Contains("class " + j.Descendants("done").Single().Value.Replace("ColonyCouncil.", "") + " : PeopleWorkDone"))
          && workOn.Select(j => j.Descendants("place").Single().Value).SequenceEqual(new[] { "here", "bed", "bed" }), "the link, the night's hum and the bionic are round 1's work on a colonist");
        var thoughts = misc.Where(e => e.Name.LocalName == "ThoughtDef").ToDictionary(Dn);
        C(thoughts.Count == 7 && thoughts.Keys.All(t => code.Contains("\"" + t + "\"")) && F(thoughts["Rimfolk_Thought_LatticeDreamGood"].Descendants("baseMoodEffect").Single().Value) > 0f
          && F(thoughts["Rimfolk_Thought_LatticeDreamBad"].Descendants("baseMoodEffect").Single().Value) < 0f
          && thoughts.Values.All(t => Math.Abs(F(t.Descendants("baseMoodEffect").Single().Value)) <= 4f), "seven moods, small ones; the dreams either way");
        var link = misc.Single(e => e.Name.LocalName == "HediffDef");
        C(Dn(link) == "Rimfolk_LatticeLink" && link.Element("hediffClass")!.Value == "ColonyCouncil.Hediff_LatticeLink" && link.Element("stages")!.Elements("li").Count() == 2
          && !link.ToString().Contains("MechBandwidth") && code.Contains("StatDefOf.MechBandwidth") && code.Contains("ModsConfig.BiotechActive"),
            "the link's bandwidth is set at startup with Biotech, so the def loads without it");
        C(code.Contains("f.HostileTo(Faction.OfPlayer)") && code.Contains("Broken(p, h)"), "the link goes quiet the day the Lattice is no longer at peace");

        // The player's mods, drawn on softly and from their own catalogue.
        var cat = Catalogue(root);
        bool From(string type, string def, string mod) => cat.Any(r => r.active && r.type == type && r.def == def && r.mod.Equals(mod, StringComparison.OrdinalIgnoreCase));
        C(PeopleEvents9Math.FeralKinds.All(k => From("PawnKindDef", k, k.StartsWith("AM_") ? "sarg.alphamechs" : "ludeon.rimworld.biotech")),
            "the feral mechs are Biotech's and Alpha Mechs' own, in the player's list");
        C(SealKinds(code).Count >= 8 && SealKinds(code).All(k => From("PawnKindDef", k, k.StartsWith("VFEI2_") ? "oskarpotocki.vfe.insectoid2" : "ludeon.rimworld"))
          && SealKinds(code).Any(k => k.StartsWith("VFEI2_")), "the vault's insects: the game's hive and Vanilla Factions Expanded - Insectoids 2's, in the player's list");
        C(PeopleEvents9Math.YardMiners.Concat(PeopleEvents9Math.YardHaulers).All(k => From("PawnKindDef", k, "ludeon.rimworld.biotech")), "the yard's labour mechs are Biotech's");
        C(From("NeedDef", "VAEI_Sanity", "vanillaexpanded.vanomalyeinsanity") && code.Contains("\"VAEI_Sanity\""), "the sanity is Vanilla Anomaly Expanded - Insanity's own");
        var voidMemories = Regex.Match(code, @"VoidMemories =\s*\{([^}]*)\}").Groups[1].Value;
        var memNames = Regex.Matches(voidMemories, "\"(\\w+)\"").Select(mm => mm.Groups[1].Value).ToList();
        C(memNames.Count >= 5 && memNames.All(x => From("ThoughtDef", x, "ludeon.rimworld.anomaly")), "the void's memories are Anomaly's own");
        C(From("PsycasterPathDef", "VPE_Harmonist", "vanillaexpanded.vpsycastse") && From("PsycasterPathDef", "VPE_Empath", "vanillaexpanded.vpsycastse")
          && code.Contains("\"VPE_Harmonist\", \"VPE_Empath\""), "a first psylink on the Choir's own paths of Vanilla Psycasts Expanded");
        C(new[] { "InstallBionicEye", "InstallBionicArm", "InstallBionicLeg" }.All(r => From("RecipeDef", r, "ludeon.rimworld")) && From("SitePartDef", "ItemStash", "ludeon.rimworld"),
            "the game's own bionics and item stash");
        var bionicLimbs = File.ReadAllLines(Path.Combine(root, "docs", "player-mods", "items.tsv")).Skip(1).Select(l => l.Split('\t'))
            .Where(c => c.Length > 5 && c[2] == "yes" && c[5].Contains("BodyPartsBionic") && Regex.IsMatch(c[0], "(Eye|Arm|Leg)$")).Select(c => c[0]).ToList();
        C(bionicLimbs.All(b => b.StartsWith("Bionic")), "no mod in the player's list adds a bionic eye, arm or leg of its own: the game's are the pool");
        var kinds = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Defs", "PeopleDefs"), "Rimfolk_People_*.xml").Select(File.ReadAllText));
        var named = Regex.Matches(code, "\"(Rimfolk_(Lattice|Choir|SunkenVault)_\\w+)\"").Select(mm => mm.Groups[1].Value).Distinct().ToList();
        C(named.Count >= 8 && named.All(k => kinds.Contains("<defName>" + k + "</defName>")), "every kind of person named is one of the people's own");

        // ================= Their words =================

        var keyedFile = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeopleEvents9.xml"));
        var keyed = XDocument.Parse(keyedFile).Root!.Elements().Select(e => e.Name.LocalName).ToHashSet();
        var used = Regex.Matches(code, "\"(CC_PEv9_\\w+[A-Za-z])\"").Select(mm => mm.Groups[1].Value).Distinct().ToList();
        C(used.Count >= 100 && used.All(keyed.Contains), $"every word the code says is keyed ({used.Count}{(used.Any(u => !keyed.Contains(u)) ? ", missing " + string.Join(", ", used.Where(u => !keyed.Contains(u))) : "")})");
        var built = Enumerable.Range(1, 3).SelectMany(i => new[] { "CC_PEv9_ConsoleRadio_" + i, "CC_PEv9_ConsoleLetter_" + i }).ToList();
        C(built.All(keyed.Contains) && code.Contains("\"CC_PEv9_ConsoleRadio_\" : \"CC_PEv9_ConsoleLetter_\""), "each evening's words, by radio and by letter, are keyed");
        var texts9 = new[] { Path.Combine("Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents9.xml"), Path.Combine("Defs", "CampDefs", "Rimfolk_CampKinds_PeopleEvents9.xml"),
                Path.Combine("Languages", "English", "Keyed", "Rimfolk_PeopleEvents9.xml"), Path.Combine("Defs", "Misc", "Rimfolk_PeopleEvents9_Misc.xml") }
            .Select(x => Regex.Replace(File.ReadAllText(Path.Combine(root, x)), "<!--.*?-->|<\\?xml[^>]*>|\\[[^\\]]*\\]", "", RegexOptions.Singleline)).Aggregate((x, y) => x + y);
        C(!texts9.Contains('—') && !texts9.Contains('!') && !Regex.IsMatch(texts9, @"\bin \d+ (days?|hours?)\b"), "plain words, no countdowns");
        C(!Regex.IsMatch(texts9.ToLowerInvariant(), @"\b(dynasty|dynasties|bloodline|heir to the house)\b"), "the colony is the family: no houses");
        C(!Regex.IsMatch(texts9, @"\b(she|her|hers)\b"), "nobody of theirs is given a sex the game didn't");
        C(!Regex.IsMatch(texts9, @"\b(god|gods|goddess|divine)\b", RegexOptions.IgnoreCase), "the gods unseen: no god named, the void and the Spire only as the pawns know them");
        string peoples = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Defs", "AlmanacArticleDefs"), "Almanac_Peoples*.xml").Select(File.ReadAllText)).ToLowerInvariant();
        C(new[] { "linked into the lattice", "a mech gone feral", "a mech yard", "the hum deepened", "the void listens back", "a mind that shouts", "a listening ring", "the seal broken",
            "the last part", "the console that talks to nobody", "surface scouts" }.All(peoples.Contains), "each people's Almanac page says what it does now");

        // ================= The seams =================

        string eight = Read("Peoples", "Events8", "PeopleEvents8.cs");
        C(eight.Contains("return PeopleEvents9.Hour(v, comp, now);") && eight.Contains("PeopleEvents9.Hourly(now)"), "round 9's visits and watch wait on round 1's hour, through rounds 2 to 8");
        var visitKinds = Regex.Matches(code, @"public const string (Kind) = ""([^""]+)"";").Select(mm => mm.Groups[2].Value).ToList();
        C(visitKinds.Count == 4 && visitKinds.All(k => k.StartsWith("pev:")) && visitKinds.Distinct().Count() == 4, "their visits are the events' own kind (\"pev:\"), none twice");
        string earlier = string.Join("\n", new[] { "Events", "Events2", "Events3", "Events4", "Events5", "Events6", "Events7", "Events8" }
            .SelectMany(x => Directory.GetFiles(Path.Combine(src, "Peoples", x), "*.cs")).Select(File.ReadAllText));
        C(visitKinds.All(k => !earlier.Contains("\"" + k + "\"")), "none of their visits' kinds is an earlier round's");
        C(Read("Peoples", "PeoplesFPatches.cs").Contains("VoidListens.Steer(fi);") && code.Contains("fi.def = named;") && code.Contains("named.Worker.CanFireNow(fi.parms)")
          && code.Contains("VoidThreat(fi.def)"), "the warden's word only gives the void's own next threat its form, through the mast's hook on the storyteller, when it can come");
        C(Read("Text", "Facts", "FactsMath.cs").Contains("\"them:camp:MechYard\", \"them:camp:ListeningRing\", \"them:camp:SurfaceScouts\""), "the facts service knows the round's camps");
        C(Read("..", "..", "Tools", "DefValidator", "Program.cs").Contains("\"CUT\", \"NIGHTS\", \"LESSONS\""), "the def check knows the camps' own placeholders");
        C(code.Contains("PeopleWorkOn.Begin(") && code.Contains("CallsToArms.Answering(") && code.Contains("CaravanIncidentUtility.SetupCaravanAttackMap(") && code.Contains("CampRaids.Send(")
          && code.Contains("ChoirListening.Heard(") && code.Contains("PeoplesGMath.Felt(") && code.Contains("VaultSupply.Noted(") && code.Contains("HappeningWorker_VaultBarter.Crate(")
          && code.Contains("HeardPlaces.Heard(") && code.Contains("OrderLimb.Bed(") && code.Contains("OrderLimb.Chance(") && code.Contains("PeopleEvents.Takers(") && code.Contains("PeopleEventsBook.Gave(")
          && code.Contains("new Bill_Medical(") && code.Contains("VoiceForColony.Radio(") && code.Contains("PsycastPaths.Follow(") && code.Contains("CampTracker.About("),
            "the shapes reused: round 1's work on a colonist and limb fitting, the calls to arms' fight beside a town, bands against a camp, round G's listening and vault supply, places heard of, the radio");
        C(code.Contains("FactionMemory.Note(f, \"townDefended\"") && code.Contains("PeopleEvents9Math.YearOfSupplyWeight") && code.Contains("PeopleAgeGifts.DeedKind"),
            "the vault remembers its town defended, and a year of supply for its last part");
        var harms = Regex.Matches(code, @"Harm\.(Roll|Ail|Injure|Fight)\(([^;]*?)HarmKinds\.(\w+)").Select(mm => mm.Groups[3].Value).Distinct().OrderBy(x => x).ToList();
        C(harms.SequenceEqual(new[] { "DroversBull", "TooClose" }) && !Regex.IsMatch(code, @"TakeDamage") && code.Contains("HarmBook.Instance?.Came(HarmKinds.DroversBull")
          && code.Contains("HarmBook.Instance?.Came(HarmKinds.TooClose"), "no harm kind of their own: the feral mechs the drovers' bull's kind, the beast that turns on a scout the wild one too close's");
        C(!code.Contains("[HarmonyPatch"), "no game hooks of their own");
        C(!code.Contains("MoteMaker.ThrowText") && code.Contains("PeoplesG.SayLine(") && code.Contains("c.SayMoment(") && code.Contains("PeoplesG.Say("), "they speak through the events' and the camps' own lines");
        C(!Regex.IsMatch(code, @"Sched\.|\bLocomotionUrgency\.Sprint"), "no clock of their own: the events' hour and the camps' hour");
        C(Regex.Matches(code, @"StoryDirection\.Factor\(StoryWaysMath\.Visitors\)").Count >= 1, "the listener the code fires reads the storytellers' lean on visitors");
        string book = File.ReadAllText(Path.Combine(dir, "PeopleEvents9Book.cs"));
        C(book.Contains("shouts.Count > PeopleEvents9Math.MaxShoutsRolled") && book.Contains("seals.Count > 3"), "what they keep is bounded");
        C(Regex.Matches(code, @"\[DebugAction\(""Rimfolk"", ""Peoples' events 9: ").Count == 7, "seven dev actions");
        string save = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(new[] { "shouts", "seals", "enemies" }.All(x => save.Contains("Peoples/Events9/PeopleEvents9Book.cs:" + x)) && save.Contains("Peoples/Events9/MechYard.cs:mechs")
          && save.Contains("Peoples/Events9/MechYard.cs:chunks"), "their saved lists are in the save model");
        string audit = File.ReadAllText(Path.Combine(root, "Tools", "playout_audit.py"));
        C(events.Values.Concat(campKinds.Values).SelectMany(e => e.Descendants("li")).Select(li => li.Attribute("Class")?.Value?.Replace("ColonyCouncil.", ""))
                .Where(v => v != null && v.StartsWith("DilemmaEffect_") && Regex.IsMatch(code, @"class " + v + @"\b") && v != "DilemmaEffect_RingQuiet" && v != "DilemmaEffect_ConsoleGive")
                .Distinct().All(v => audit.Contains("\"" + v + "\"")),
            "the play-out audit knows each answer of theirs that plays out on the map");
        return n;
    }

    /// <summary>The insects the vault's fight may bring, as its code names them.</summary>
    private static List<string> SealKinds(string code) =>
        Regex.Matches(Regex.Match(code, @"InsectKinds =\s*\{([^}]*)\}").Groups[1].Value, "\"(\\w+)\"").Select(m => m.Groups[1].Value).ToList();

    private static string Dn(XElement e) => e.Element("defName")?.Value ?? "";

    private static XElement Ext(XElement d, string cls) => d.Element("modExtensions")?.Elements("li").FirstOrDefault(li => li.Attribute("Class")?.Value == "ColonyCouncil." + cls);

    private static List<(string type, string def, string mod, bool active)> catalogue;

    /// <summary>The player's mods' every def (docs/player-mods/defs.tsv.gz): type, name, package, active.</summary>
    private static List<(string type, string def, string mod, bool active)> Catalogue(string root)
    {
        if (catalogue != null) return catalogue;
        catalogue = new List<(string, string, string, bool)>();
        using var fs = File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz"));
        using var gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress);
        using var rd = new StreamReader(gz);
        rd.ReadLine();
        string line;
        while ((line = rd.ReadLine()) != null)
        {
            var c = line.Split('\t');
            if (c.Length >= 4) catalogue.Add((c[0], c[1], c[2], c[3] == "yes"));
        }
        return catalogue;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
