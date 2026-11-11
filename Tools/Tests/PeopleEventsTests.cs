using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The peoples' own events, round 1 (Peoples/Events/; docs/peoples-events-proposals.md, items 10, 5, 47, 83, 51, 1, 19,
/// 17, 34 and 95): their pure rules (gifts once a colonist and spaced out, the beast's price, the order's limb by its age,
/// storm-sense, the mechlink's price, the ledger's warmth and the promise, crates held in trust and the book's verdict,
/// the reading's parts, the hostel's bread and feuds, the lantern post's rates, the bout and the game's tame chance), their
/// defs and words, the player's mods they draw on, and the seams: the visitor who works on a colonist, goods held in
/// trust, the reading of an old place, the legend beast, the camp kind with ways of its own.
/// </summary>
public static class PeopleEventsTests
{
    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the peoples' events: " + what);
        }
        float F(string v) => float.Parse(v, CultureInfo.InvariantCulture);
        const int Day = PeopleEventsMath.DayTicks;

        // ================= Gifts: once a colonist, and spaced out =================

        C(PeopleEventsMath.GiftOpen(5, -1) && !PeopleEventsMath.GiftOpen(19 * Day, 0) && PeopleEventsMath.GiftOpen(20 * Day, 0) && PeopleEventsMath.GiftGapDays >= 15,
            "a gift of the peoples only twenty days or more after the last one");
        C(PeopleEventsMath.GiftKey("beast", 4512) == "beast:4512" && PeopleEventsMath.GiftKey(null, 1) == ":1" && PeopleEventsMath.MaxGifts >= 60, "one gift of a kind a colonist, the record bounded");

        // ================= 10. A beast for a colonist =================

        C(PeopleEventsMath.BeastFits(true, 2.4f, 0.25f, false, false, false) && !PeopleEventsMath.BeastFits(true, 2.4f, 0.25f, true, false, false)
          && !PeopleEventsMath.BeastFits(true, 2.4f, 0.25f, false, true, false) && !PeopleEventsMath.BeastFits(true, 0.5f, 0.1f, false, false, false)
          && !PeopleEventsMath.BeastFits(true, 2f, 0.9f, false, false, false) && !PeopleEventsMath.BeastFits(false, 2f, 0.1f, false, false, false)
          && !PeopleEventsMath.BeastFits(true, float.NaN, 0.1f, false, false, false) && !PeopleEventsMath.BeastFits(true, 2f, 0.1f, false, false, true),
            "a beast to ride or load: big enough, tame enough, no predator, insect or dryad");
        C(PeopleEventsMath.BeastWeight(true, true, 0.5f) == 1f && PeopleEventsMath.BeastWeight(true, false, 0.5f) == 0.5f && PeopleEventsMath.BeastWeight(false, true, 0f) == 0.05f
          && PeopleEventsMath.BeastWeight(false, false, float.NaN) == 0.05f, "by the land, a beast to ride twice as likely with Giddy-Up");
        C(PeopleEventsMath.BeastPrice(500f) == 450 && PeopleEventsMath.BeastPrice(0f) == 60 && PeopleEventsMath.BeastPrice(float.NaN) == 60 && PeopleEventsMath.BeastPrice(-9f) == 60,
            "a fair price: nine tenths of its worth");
        C(PeopleEventsMath.TradeEnough(250f, 500f) && !PeopleEventsMath.TradeEnough(249f, 500f) && !PeopleEventsMath.TradeEnough(100f, 0f) && !PeopleEventsMath.TradeEnough(float.NaN, 1f),
            "a young beast of the colony's worth half of theirs to trade");
        C(PeopleEventsMath.FirstTasks.SequenceEqual(new[] { "Tameness", "Obedience" }), "its first tasks, the rest the colony's to teach");

        // ================= 5. A limb fitted =================

        C(PeopleEventsMath.PartKind("Leg") == "leg" && PeopleEventsMath.PartKind("Foot") == "foot" && PeopleEventsMath.PartKind("Hand") == "hand" && PeopleEventsMath.PartKind("Arm") == "arm"
          && PeopleEventsMath.PartKind("Shoulder") == "arm" && PeopleEventsMath.PartKind("Eye") == null && PeopleEventsMath.PartKind(null) == null, "a hand, a foot, an arm or a leg");
        C(PeopleEventsMath.LimbRecipe("leg", 1) == "InstallPegLeg" && PeopleEventsMath.LimbRecipe("foot", 0) == "InstallPegLeg" && PeopleEventsMath.LimbRecipe("hand", 1) == "InstallWoodenHand"
          && PeopleEventsMath.LimbRecipe("arm", 1) == null && PeopleEventsMath.LimbRecipe("leg", 2) == "InstallSimpleProstheticLeg" && PeopleEventsMath.LimbRecipe("hand", 3) == "InstallSimpleProstheticArm"
          && PeopleEventsMath.LimbRecipe("arm", 2) == "InstallSimpleProstheticArm" && PeopleEventsMath.LimbRecipe("eye", 3) == null,
            "a peg leg or wooden hand from the colony's wood before the industrial age, the simple prostheses after");
        var limbs = new[] { "leg", "foot", "hand", "arm" }.SelectMany(k => Enumerable.Range(0, 4).Select(a => PeopleEventsMath.LimbRecipe(k, a))).Where(r => r != null).ToList();
        C(limbs.All(r => !r.Contains("Bionic") && !r.Contains("Archotech")), "never bionics: those are the enclave's");
        C(PeopleEventsMath.Wooden("InstallPegLeg") && PeopleEventsMath.Wooden("InstallWoodenHand") && !PeopleEventsMath.Wooden("InstallSimpleProstheticLeg"), "wooden limbs take the colony's wood");
        C(PeopleEventsMath.ChancePercent(0.873f) == 87 && PeopleEventsMath.ChancePercent(1.5f) == 99 && PeopleEventsMath.ChancePercent(0f) == 1 && PeopleEventsMath.ChancePercent(float.NaN) == 0,
            "the game's own surgery quality shown as a chance on the card");
        C(PeopleEventsMath.SurgeonMin == 10 && PeopleEventsMath.SurgeonMax == 16 && PeopleEventsMath.SparedWeight >= 10 && PeopleEventsMath.MedicineBack > 0,
            "a surgeon of Medicine 10 to 16; the fitting big enough to owe the order a favour");

        // ================= 47. Storm-sense =================

        C(PeopleEventsMath.StormPrice(1f) == 350 && PeopleEventsMath.StormPrice(0.1f) == 250 && PeopleEventsMath.StormPrice(9f) == 700 && PeopleEventsMath.StormPrice(float.NaN) == 350,
            "storm-sense for 350 silver by the colony's wealth, within 250 and 700");
        C(PeopleEventsMath.Hears(0.5f) && PeopleEventsMath.Hears(1.6f) && !PeopleEventsMath.Hears(0.2f) && !PeopleEventsMath.Hears(float.NaN), "only the sensitive hear the storm");
        C(PeopleEventsMath.StormTicks == PeopleEventsMath.HourTicks && PeopleEventsMath.StormPaths.SequenceEqual(new[] { "VPE_Staticlord", "VPE_Frostshaper" }) && PeopleEventsMath.StormTome == "BookOfStormBorn",
            "an hour out in the storm; the Staticlord or Frostshaper path, or the Lightning tome");

        // ================= 83. A mechlink =================

        C(PeopleEventsMath.MechlinkPrice(600f, 200f) == 600 && PeopleEventsMath.MechlinkPrice(0f, 0f) == 400 && PeopleEventsMath.MechlinkPrice(5000f, 5000f) == 1200
          && PeopleEventsMath.MechlinkPrice(float.NaN, 300f) == 400, "a mechlink and a mech for three quarters of their worth, within 400 and 1200");
        C(PeopleEventsMath.SubcoresPay == 2 && PeopleEventsMath.MechlinkTicks == 2 * PeopleEventsMath.HourTicks, "or two basic subcores; two hours' work");

        // ================= 51. A Gauranlen seed =================

        C(PeopleEventsMath.LedgerWarm(30, 2, 2) && !PeopleEventsMath.LedgerWarm(29, 0, 0) && !PeopleEventsMath.LedgerWarm(80, 5, 4) && PeopleEventsMath.LedgerWarm(80, 0, 0),
            "the ledger warm: their goodwill 30 or more, no more felled than planted this season");
        C(PeopleEventsMath.Promise(0, 9) == 0 && PeopleEventsMath.Promise(4, 4) == 1 && PeopleEventsMath.Promise(4, 3) == -1 && PeopleEventsMath.Promise(-1, 0) == 0,
            "the promise kept, broken, or nothing felled");
        C(PeopleEventsMath.PromiseKeptGoodwill > 0 && PeopleEventsMath.PromiseBrokenGoodwill < 0 && -PeopleEventsMath.PromiseBrokenGoodwill > PeopleEventsMath.PromiseKeptGoodwill,
            "a broken promise weighs more than a kept one");

        // ================= 1. Crates held in trust =================

        var counts = Enumerable.Range(0, 1000).Select(i => PeopleEventsMath.Crates(i / 1000f)).ToList();
        C(counts.Min() == 6 && counts.Max() == 10 && PeopleEventsMath.Crates(float.NaN) == 6 && PeopleEventsMath.Crates(5f) == 10, "six to ten crates");
        var vanillaThings = Catalogue(root).Where(r => r.type == "ThingDef" && r.mod == "ludeon.rimworld" && r.active).Select(r => r.def).ToHashSet();
        for (int age = 0; age < 4; age++)
        {
            var goods = Enumerable.Range(0, 5).Select(i => PeopleEventsMath.CrateGoods(age, i)).ToList();
            C(goods.All(g => vanillaThings.Contains(g.def) && g.count > 0) && goods.Select(g => g.def).Distinct().Count() == 5,
                $"the age {age} league's crates hold the base game's goods, five kinds");
        }
        C(PeopleEventsMath.CrateGoods(2, -3).def == PeopleEventsMath.CrateGoods(2, 2).def && PeopleEventsMath.CrateGoods(9, 0).def == PeopleEventsMath.CrateGoods(3, 0).def,
            "any place in the load has goods");
        C(PeopleEventsMath.TrustFee(1500f) == 150 && PeopleEventsMath.TrustFee(0f) == 20 && PeopleEventsMath.TrustFee(float.NaN) == 20, "a tenth of their worth as the fee");
        C(PeopleEventsMath.Verdict(8, 8, 0, 0, 0) == PeopleEventsMath.Ink.Good && PeopleEventsMath.Verdict(8, 6, 2, 0, 0) == PeopleEventsMath.Ink.Good
          && PeopleEventsMath.Verdict(8, 7, 0, 1, 0) == PeopleEventsMath.Ink.Red && PeopleEventsMath.Verdict(8, 7, 0, 0, 1) == PeopleEventsMath.Ink.Red
          && PeopleEventsMath.Verdict(8, 7, 0, 0, 0) == PeopleEventsMath.Ink.Red && PeopleEventsMath.Verdict(0, 0, 0, 0, 0) == PeopleEventsMath.Ink.None
          && PeopleEventsMath.Verdict(8, 7, 1, 1, 0) == PeopleEventsMath.Ink.Red, "good ink when every crate is there, a raid's fire forgiven; red ink for one opened, sold or gone");
        C(PeopleEventsMath.Means(0) == "beast" && PeopleEventsMath.Means(1) == "beast" && PeopleEventsMath.Means(2) == "truck" && PeopleEventsMath.Means(3) == "lander",
            "a lame beast, a broken truck, a lander short of lift, by the league's age");
        C(PeopleEventsMath.GoodInkTerms > 0f && PeopleEventsMath.GoodInkTerms <= 0.05f && PeopleEventsMath.GoodInkDays == 60 && PeopleEventsMath.RedInkDays == 15
          && PeopleEventsMath.RedInkGoodwill < 0 && PeopleEventsMath.MaxTrusts >= 2, "a few points better for a year; no caravans for a season");

        // ================= 19. What lies in the old place =================

        C(PeopleEventsMath.ReadingParts(2, 0, 1, 3, 1).SequenceEqual(new[] { "mechs", "hives", "caskets", "crates" }) && PeopleEventsMath.ReadingParts(0, 0, 0, 0, 0).SequenceEqual(new[] { "nothing" })
          && PeopleEventsMath.ReadingParts(0, 4, 0, 0, 0).SequenceEqual(new[] { "fleshbeasts" }), "the reading says what is there, part by part, or nothing");
        C(PeopleEventsMath.Dangerous(1, 0, 0, 0) && PeopleEventsMath.Dangerous(0, 0, 0, 2) && !PeopleEventsMath.Dangerous(0, 0, 0, 0), "opened, what sleeps there wakes");
        C(PeopleEventsMath.MeasureDays == 4f && PeopleEventsMath.ResearchShare > 0f && PeopleEventsMath.ResearchMax <= 1500f, "four days measuring, research left for the colony");

        // ================= 17. A hostel on the road =================

        C(PeopleEventsMath.Bread(1) == 3 && PeopleEventsMath.Bread(7) == 7 && PeopleEventsMath.TravellersMax <= 5 && PeopleEventsMath.TravellerGapMin >= 6
          && PeopleEventsMath.TravellerStayMin >= 12 && PeopleEventsMath.TravellerStayMax <= 48, "bread for each at the hostel; a traveller now and then for a night or two");
        C(PeopleEventsMath.Feuding(-30, false) && !PeopleEventsMath.Feuding(-30, true) && !PeopleEventsMath.Feuding(0, false), "feuding peoples, never two at war, may quarrel");
        C(PeopleEventsMath.HostelCough > 0f && PeopleEventsMath.HostelCough <= 0.1f, "the cough round the hut at the harm budget's rate");

        // ================= 34. The lantern post =================

        C(PeopleEventsMath.PostDays == 10f && PeopleEventsMath.FireVisitChance <= 0.15f && PeopleEventsMath.HoundLostChance <= 0.1f && PeopleEventsMath.WarnGapHours >= 6
          && PeopleEventsMath.TrailHours >= 12 && PeopleEventsMath.HoundsMeat > 0, "ten days at most; a night at the fire and a lost hound rare, once a stay");

        // ================= 95. Kill it or win it over =================

        C(PeopleEventsMath.WardenWins(10, 10) == 0.5f && PeopleEventsMath.WardenWins(20, 0) == 0.85f && PeopleEventsMath.WardenWins(0, 20) == 0.15f
          && PeopleEventsMath.WardenWins(12, 10) > 0.5f, "the bout by their hands, never sure either way");
        C(Math.Abs(PeopleEventsMath.TameChance(0.4f, 0.5f) - 0.4f) < 1e-4f && PeopleEventsMath.TameChance(0.4f, 1f) == 0f && Math.Abs(PeopleEventsMath.TameChance(0.3f, 0f) - 0.6f) < 1e-4f
          && PeopleEventsMath.TameChance(0.9f, 0f) == 1f && PeopleEventsMath.TameChance(float.NaN, 0.5f) == 0f && Math.Abs(PeopleEventsMath.TameChance(0.5f, 0.985f) - 0.015f) < 1e-3f,
            "the tamer's try at the game's own chance: twice at no wildness, once at half, none at all");
        C(PeopleEventsMath.TameTries >= 4 && PeopleEventsMath.PassedOverGoodwill < 0 && PeopleEventsMath.SidedGoodwill > 0 && PeopleEventsMath.BoutLoserGoodwill > PeopleEventsMath.PassedOverGoodwill,
            "the side passed over thinks less of the colony; the bout's loser a little");

        // ================= Their defs =================

        var events = XDocument.Load(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents.xml")).Root!.Elements("IncidentDef").ToDictionary(Dn);
        C(events.Keys.SequenceEqual(new[] { "Rimfolk_Evt_HerdersBeast", "Rimfolk_Evt_OrderLimb", "Rimfolk_Evt_StormSense", "Rimfolk_Evt_LatticeMechlink", "Rimfolk_Evt_MossSeed",
            "Rimfolk_Evt_LeagueTrust", "Rimfolk_Evt_ScholarsReading", "Rimfolk_Evt_LanternPost", "Rimfolk_Evt_HuntOrTame" }), "nine events (the hostel comes with its world events)");
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { src }.Concat(parts).ToArray()));
        string dir = Path.Combine(src, "Peoples", "Events");
        string code = string.Join("\n", Directory.GetFiles(dir, "*.cs").Where(f => !f.EndsWith("PeopleEventsMath.cs")).OrderBy(f => f).Select(File.ReadAllText));
        string everyCode = string.Join("\n", Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        var callings = new Dictionary<string, string>
        {
            ["Rimfolk_Evt_HerdersBeast"] = "Herders", ["Rimfolk_Evt_OrderLimb"] = "Healers", ["Rimfolk_Evt_StormSense"] = "Seers", ["Rimfolk_Evt_LatticeMechlink"] = "Mechanitors",
            ["Rimfolk_Evt_MossSeed"] = "Grovekeepers", ["Rimfolk_Evt_LeagueTrust"] = "Traders", ["Rimfolk_Evt_ScholarsReading"] = "Scholars", ["Rimfolk_Evt_LanternPost"] = "Hunters",
            ["Rimfolk_Evt_HuntOrTame"] = "Hunters"
        };
        foreach (var (name, e) in events)
        {
            string worker = e.Element("workerClass")!.Value.Replace("ColonyCouncil.", "");
            var m = Regex.Match(code, @"class " + worker + @" : HappeningWorker_Visitors\s*\{(.*?)\n    \}", RegexOptions.Singleline);
            C(m.Success, $"{name}: its worker is the round's own, a visitors' event");
            C(m.Success && m.Groups[1].Value.Contains("PeoplesMath." + callings[name]) || name == "Rimfolk_Evt_MossSeed" && code.Contains("PeoplesD.Theirs(f, PeoplesMath.Grovekeepers)"),
                $"{name}: only from a people of its calling at peace with the colony");
            var effects = e.Descendants("li").Select(li => li.Attribute("Class")?.Value).Where(v => v != null && v.StartsWith("ColonyCouncil.DilemmaEffect_")).Select(v => v.Replace("ColonyCouncil.", "")).Distinct();
            C(effects.All(x => Regex.IsMatch(everyCode, @"class " + x + @"\b")), $"{name}: every answer's effect is real");
            var ext = Ext(e, "HappeningExtension")!;
            C(F(e.Element("baseChance")!.Value) <= 1f && int.Parse(e.Element("minRefireDays")!.Value) >= 20 && int.Parse(e.Element("earliestDay")!.Value) >= 8,
                $"{name}: a small share of the visitors, not soon again, not in the first week");
            C(ext.Element("group")?.Value == "Visitors", $"{name}: among the visitors, at the peoples' touchpoints' rates");
            var texts = ext.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
            C(texts.Count >= 5 && texts.Any(t => t.StartsWith("[lowtech]") || t.StartsWith("[!lowtech]") || Regex.IsMatch(t, @"^\[(beast|truck|lander|tree|grove|walls|landmark|tome|rod|paths|subcores|strange|wooden|prosthetic)")),
                $"{name}: five or more texts, in the forms of the age");
            var options = ext.Element("options")?.Elements("li").ToList() ?? new List<XElement>();
            if (name == "Rimfolk_Evt_LanternPost") C(options.Count == 0, $"{name}: a letter: the camp is the camps' own from there");
            else
            {
                C(options.Count == 3 && options[2].Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_SendAway")
                  || name == "Rimfolk_Evt_HuntOrTame" && options.Count == 3, $"{name}: three answers, the last to send them on");
                C(ext.Element("defaultOption")?.Value == "2", $"{name}: no answer, the last answer");
            }
            var lineTags = ext.Element("lines")?.Elements("li").Select(l => Regex.Match(l.Value, @"^\[([^\],]+)").Groups[1].Value).Distinct().ToList() ?? new List<string>();
            C(lineTags.Count > 0 && lineTags.All(t => code.Contains("\"" + t + "\"") || t == "fell"), $"{name}: every line's moment is one the code says it at");
            C(ext.Element("lines")!.Elements("li").All(l => Regex.Matches(Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", ""), @"[.?](\s|$)").Count == 1), $"{name}: every spoken line one sentence");
        }
        C(Ext(events["Rimfolk_Evt_StormSense"], "HappeningExtension")!.Element("mods")!.Elements("li").Select(l => l.Value).SequenceEqual(new[] { "Ludeon.RimWorld.Royalty", "torann.arimworldofmagic" })
          && code.Contains("ModsConfig.RoyaltyActive") && code.Contains("PeopleEventsMath.StormTome"), "storm-sense with Royalty, else A RimWorld of Magic's tome");
        C(Ext(events["Rimfolk_Evt_LatticeMechlink"], "HappeningExtension")!.Element("mods")!.Elements("li").Single().Value == "Ludeon.RimWorld.Biotech" && code.Contains("ModsConfig.BiotechActive"),
            "the mechlink only with Biotech");
        C(Regex.Matches(code, @"PeopleEventsBook\.Gave\(").Count == 5 && Regex.Matches(code, @"PeopleEvents\.Takers\(map, GiftKind\)|PeopleEvents\.Takers\(map, HerdersBeast\.GiftKind\)").Count >= 4,
            "five gifts, each once a colonist and spaced out");

        // The camps.
        var campKinds = XDocument.Load(Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKinds_PeopleEvents.xml")).Root!.Elements("ColonyCouncil.CampKindDef").ToDictionary(Dn);
        C(campKinds.Keys.SequenceEqual(new[] { "Rimfolk_Camp_PilgrimHostel", "Rimfolk_Camp_LanternPost", "Rimfolk_Camp_ScholarsMeasure" }), "three camps of their own");
        foreach (var (name, k) in campKinds)
        {
            string worker = k.Element("workerClass")!.Value.Replace("ColonyCouncil.", "");
            C(F(k.Element("weight")!.Value) == 0f && code.Contains("class " + worker + " : CampWorker"), $"{name}: never on the camps' own draw; ways of its own ({worker})");
        }
        C(campKinds["Rimfolk_Camp_PilgrimHostel"].Element("calling")!.Value == "pilgrims" && campKinds["Rimfolk_Camp_LanternPost"].Element("calling")!.Value == "hunters"
          && campKinds["Rimfolk_Camp_ScholarsMeasure"].Element("calling")!.Value == "scholars", "each only from a people of its calling");
        var hostel = campKinds["Rimfolk_Camp_PilgrimHostel"];
        C(hostel.Element("sleep")!.Value == "Hut" && hostel.Descendants("li").Any(li => li.Attribute("MayRequire")?.Value == "Ludeon.RimWorld.Royalty" && li.Value == "Brazier")
          && hostel.Element("leaves") == null, "the hostel: a long hut, the shrine's flame, and the hut left standing");
        C(hostel.Element("asks")!.Elements("li").Any(a => a.Element("key")?.Value == "bread" && a.Element("theyAsk")?.Value == "true")
          && hostel.Element("asks")!.Elements("li").Any(a => a.Element("key")?.Value == "blessing" && a.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_HostelCough")),
            "bread asked for each morning; the evening blessing, with the cough going round");
        var post = campKinds["Rimfolk_Camp_LanternPost"];
        C(post.Element("animals")!.Descendants("count").Single().Value == "2~2" && post.Element("days")!.Value == "10~10" && post.Element("armedHunters")?.Value == "true"
          && post.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).SequenceEqual(new[] { "meat", "trail", "lantern" }),
            "the post: three Wardens and two hounds for ten days at most; meat for the hounds, the trail, the lantern");
        var patch = XDocument.Load(Path.Combine(root, "Patches", "Rimfolk_PeopleEvents.xml")).Root!;
        C(patch.Elements("Operation").Count() == 2 && patch.Elements("Operation").All(op => op.Attribute("Class")?.Value == "PatchOperationConditional")
          && patch.Descendants("kind").All(k => k.Value == "Rimfolk_Camp_PilgrimHostel")
          && patch.Descendants("xpath").Any(x => x.Value.Contains("Council_WS_Pilgrimage")) && patch.Descendants("xpath").Any(x => x.Value.Contains("Council_WS_HolyYear")),
            "the hostel comes with a great pilgrimage or a holy year, conditionally");

        // Their jobs and the crate.
        var misc = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_PeopleEvents_Misc.xml")).Root!.Elements().ToList();
        var works = misc.Where(e => Ext(e, "PeopleWorkJob") != null).ToList();
        C(works.Select(Dn).SequenceEqual(new[] { "Rimfolk_PEv_Limb", "Rimfolk_PEv_Storm", "Rimfolk_PEv_Mechlink", "Rimfolk_PEv_Seed" })
          && works.All(w => Ext(w, "PeopleWorkJob")!.Element("done")!.Value is string d && code.Contains("class " + d.Replace("ColonyCouncil.", "") + " : PeopleWorkDone")),
            "the work on a colonist: the limb at the bed, the storm under the sky, the mechlink where they stand, the seed beside them");
        C(works.Select(w => Ext(w, "PeopleWorkJob")!.Element("place")!.Value).SequenceEqual(new[] { "bed", "sky", "here", "beside" }), "each work's place for the one it is done on");
        var crate = misc.Single(e => Dn(e) == "Rimfolk_TrustCrate");
        C(crate.Element("thingClass")!.Value == "ColonyCouncil.TrustCrate" && crate.Element("stackLimit")!.Value == "1" && crate.Element("tradeability")!.Value == "Sellable"
          && File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Things", "TrustCrate.png")), "the sealed crate: one at a time, sellable (and the book will say so), drawn");
        var thought = misc.Single(e => Dn(e) == "Rimfolk_Thought_MechlinkStrange");
        C(F(thought.Descendants("baseMoodEffect").Single().Value) < 0f && F(thought.Descendants("baseMoodEffect").Single().Value) >= -4f && F(thought.Element("durationDays")!.Value) <= 3f
          && File.ReadAllText(Path.Combine(root, "Defs", "MoodHintDefs", "Rimfolk_MoodHints_PeopleEvents.xml")).Contains("Rimfolk_Thought_MechlinkStrange"),
            "a tribal colony finds a mechlink strange: a small mood, a few days, with what helps");

        // The player's mods, drawn on softly and from their own catalogue.
        var active = Catalogue(root).Where(r => r.active).Select(r => r.type + ":" + r.def).ToHashSet();
        C(PeopleEventsMath.StarterMechs.All(m => active.Contains("PawnKindDef:" + m)) && PeopleEventsMath.StormPaths.All(p => active.Contains("PsycasterPathDef:" + p))
          && active.Contains("ThingDef:" + PeopleEventsMath.StormTome) && active.Contains("ThingDef:GauranlenSeed") && active.Contains("ThingDef:Plant_Healroot")
          && active.Contains("PawnKindDef:AEXP_GermanShepherd") && active.Contains("ThingDef:Brazier"), "every mod's thing they name is in the player's active mods");
        C(code.Contains("BiomeAnimals.Of(biome)") && code.Contains("GiddyUpMounts.Ridable(") && code.Contains("\"Horse\", \"Yak\", \"Dromedary\", \"Muffalo\""),
            "the beast by the land (the mods' beasts with it), ridden with Giddy-Up, the base game's beasts to fall back on");
        C(code.Contains("GeologicalLandforms.WorldTileInfo") && code.Contains(".Landmark?.name"), "a landmark of the game's and Vanilla Landmarks Expanded's, a landform of Geological Landforms' by name");

        // ================= Their words =================

        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeopleEvents.xml")).Root!.Elements().Select(e => e.Name.LocalName).ToHashSet();
        var used = Regex.Matches(code + Read("PlayOut", "PlayCarry.cs"), "\"(CC_PEv_\\w+)\"").Select(m => m.Groups[1].Value).Where(k => !k.EndsWith("_")).Distinct().ToList();
        C(used.Count >= 100 && used.All(keyed.Contains), $"every word the code says is keyed ({used.Count}{(used.Any(u => !keyed.Contains(u)) ? ", missing " + string.Join(", ", used.Where(u => !keyed.Contains(u))) : "")})");
        C(new[] { "beast", "truck", "lander" }.All(m => keyed.Contains("CC_PEv_Means_" + m)) && PeopleEventsMath.ReadingParts(1, 1, 1, 1, 1).All(p => keyed.Contains("CC_PEv_Read_" + p))
          && keyed.Contains("CC_PEv_Read_nothing"), "the league's means and the reading's parts, each with words");
        var texts2 = new[] { Path.Combine("Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents.xml"), Path.Combine("Defs", "CampDefs", "Rimfolk_CampKinds_PeopleEvents.xml"),
                Path.Combine("Languages", "English", "Keyed", "Rimfolk_PeopleEvents.xml"), Path.Combine("Defs", "Misc", "Rimfolk_PeopleEvents_Misc.xml") }
            .Select(x => Regex.Replace(File.ReadAllText(Path.Combine(root, x)), "<!--.*?-->|<\\?xml[^>]*>|\\[[^\\]]*\\]", "", RegexOptions.Singleline)).Aggregate((x, y) => x + y);
        C(!texts2.Contains('—') && !texts2.Contains('!') && !Regex.IsMatch(texts2, @"\bin \d+ (days?|hours?)\b"), "plain words, no countdowns");
        C(!Regex.IsMatch(texts2.ToLowerInvariant(), @"\b(dynasty|dynasties|bloodline|heir to the house)\b"), "the colony is the family: no houses");
        string Alm(string f) => File.ReadAllText(Path.Combine(root, "Defs", "AlmanacArticleDefs", f));
        const string Heading = "<heading>Of their own: gifts, trust and camps</heading>";
        C(new[] { "Almanac_Peoples.xml", "Almanac_PeoplesC.xml", "Almanac_PeoplesD.xml", "Almanac_PeoplesE.xml", "Almanac_PeoplesG.xml" }.All(f => Alm(f).Contains(Heading)),
            "each people's Almanac page has a section of their own");
        C(Alm("Almanac_Peoples.xml").Contains("good ink") && Alm("Almanac_Peoples.xml").Contains("Book of the Spared") && Alm("Almanac_Peoples.xml").Contains("leads it home")
          && Alm("Almanac_PeoplesC.xml").Contains("hostel") && Alm("Almanac_PeoplesC.xml").Contains("exactly what lies inside")
          && Alm("Almanac_PeoplesD.xml").Contains("lantern goes out") && Alm("Almanac_PeoplesD.xml").Contains("bout at the gate")
          && Alm("Almanac_PeoplesE.xml").Contains("Lightning tome") && Alm("Almanac_PeoplesE.xml").Contains("a seedling for every tree felled")
          && Alm("Almanac_PeoplesG.xml").Contains("mechlink") && Alm("Almanac_Peoples.xml").Contains("within twenty days"),
            "and says what each does, as the code does it");

        // ================= The seams =================

        C(Read("Happenings", "Happenings.cs").Contains("case var k when PeopleEvents.Owns(k): keep = PeopleEvents.Hour(v, comp, now); break;")
          && Read("Happenings", "Happenings.cs").Contains("Scribe_Values.Look(ref cell, \"cell\", IntVec3.Invalid);"), "their visits wait on the events' hour, a seed's spot kept");
        C(Read("Peoples", "PeoplesE.cs").Contains("MossPromise.Reckoned(felled, planted);"), "the promise reckoned with the Mossborn's ledger");
        string land = Read("Camps", "CampKindsLand.cs");
        C(land.Contains("kind.Worker.Hourly(this") && land.Contains("kind.Worker.DayJob(this, p)") && land.Contains("kind.Worker.NightJob(this, p, hour)") && land.Contains("kind.Worker.SiteFor(map, usual, shade)")
          && land.Contains("kind?.Worker?.Started(this)") && land.Contains("kind?.Worker?.Leaving(this)") && land.Contains("Scribe_Deep.Look(ref workState, \"workState\")"),
            "a camp kind's ways hooked beside the land kinds': its hour, its people's day and night, its site, its coming and going, its state saved");
        C(Read("Camps", "CampKindDef.cs").Contains("(Worker?.FitsMap(map) ?? true)") && Read("Camps", "Camp.cs").Contains("s = kind.Worker.Fill(this, s);")
          && Read("Camps", "CampLook.cs").Contains("kind?.Worker?.AddMarks(this, marks);") && Read("Camps", "CampAsks.cs").Contains("?? kind?.Worker?.AskBlocked(this, a)"),
            "and its land, its words, its farewell's marks and its own asks");
        C(Read("PlayOut", "PlayCarry.cs").Contains("public const string Meat = \"meat\";"), "meat carried out for the hounds");
        C(code.Contains("r.Worker.ApplyOnPawn(on, part, worker, new List<Thing>(), null)") && code.Contains("surgeryOutcomeEffect.GetQuality("), "the limb by the game's own surgery and its own chance");
        C(code.Contains("HediffDefOf.MechlinkImplant") && code.Contains("PawnRelationDefOf.Overseer") && code.Contains("ChangePsylinkLevel(1, false)") && code.Contains("PsycastPaths.Follow(on")
          && code.Contains("comp.DoPlant(on, cell, map)") && code.Contains("ConnectToPawn(on,") && code.Contains("PawnRelationDefOf.Bond") && code.Contains("training.Train(td, b, true)"),
            "the game's own mechlink and overseer, psylink and paths, seed and connection, bond and training");
        C(code.Contains("TameUtility.CanTame(beast)") && code.Contains("PawnUtility.GetManhunterOnTameFailChance(beast)") && code.Contains("DesignationDefOf.Tame")
          && code.Contains("HappeningWorker_WardensHunt.Hunt(hunt"), "the taming the game's own, its revenge at the game's rate; the hunt the Wardens' own");
        C(code.Contains("WakeUp()") && code.Contains("EjectContents()") && code.Contains("FloodFillerFog.FloodUnfog("), "an old place opened wakes what sleeps there, the game's own way");
        var harms = Regex.Matches(code, @"Harm\.(Roll|Ail|Injure)\(([^,;]*,\s*)?HarmKinds\.(\w+)").Select(mm => mm.Groups[3].Value).Distinct().ToList();
        C(harms.OrderBy(x => x).SequenceEqual(new[] { "CampBrawl", "PilgrimCough" }) && !Regex.IsMatch(code, @"TakeDamage|\.Kill\("),
            "the only harm of their own: the pilgrims' cough and a camp brawl's rate, through the harm budget; the rest the game's own");
        C(!code.Contains("MoteMaker.ThrowText") && code.Contains("Speech.Say") == false && code.Contains("PeoplesG.SayLine(") && code.Contains("c.SayMoment("),
            "they speak through the events' and the camps' own lines");
        C(!Regex.IsMatch(code, @"Sched\.|\bLocomotionUrgency\.Sprint"), "no clock of their own: the events' hour and the camps' hour");
        string book = File.ReadAllText(Path.Combine(dir, "PeopleEventsBook.cs"));
        C(book.Contains("PeopleEventsMath.MaxGifts") && book.Contains("PeopleEventsMath.MaxRead") && File.ReadAllText(Path.Combine(dir, "PeopleTrust.cs")).Contains("PeopleEventsMath.MaxTrusts"),
            "what they keep is bounded");
        C(Regex.Matches(code, @"\[DebugAction\(""Rimfolk"", ""Peoples' events: ").Count == 4, "four dev actions: the record, a camp here now, the fetch, the reckoning");
        C(File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py")).Contains("Peoples/Events/PeopleEventsBook.cs:gifts"), "their saved lists are in the save model");
        return n;
    }

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
