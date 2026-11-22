using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The peoples' own events, round 4 (Peoples/Events4/; docs/peoples-events-proposals.md, items 18, 21, 23, 24, 27, 28, 29, 30,
/// 31, 32 and 33): their pure rules (the examination's odds and the fellow's book, the summer school's lessons and its
/// disputation, the contract's size, price and default, the broker's cargo and the fifth paid, what fouls water, the
/// knot night's lanterns, the flooded families, the hull's stages and the dinghy, the page's pelt and passion, the
/// manhunt, the hive's fire by the age), their defs and words, the player's mods they draw on, and the seams with the game
/// and rounds 1 to 3.
/// </summary>
public static class PeopleEvents4Tests
{
    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the peoples' events, round 4: " + what);
        }
        float F(string v) => float.Parse(v, CultureInfo.InvariantCulture);

        // ================= 18. The fellowship examined =================

        C(!PeopleEvents4Math.Examinable(11, false) && PeopleEvents4Math.Examinable(12, false) && PeopleEvents4Math.Examinable(10, true) && !PeopleEvents4Math.Examinable(9, true),
            "examined at Intellectual 12, the summer school's best student at 10");
        C(Math.Abs(PeopleEvents4Math.ExamFail(12, 6, true) - 0.32f) < 1e-4f && Math.Abs(PeopleEvents4Math.ExamFail(12, 6, false) - 0.53f) < 1e-4f && PeopleEvents4Math.ExamFail(20, 20, true) == 0.05f
          && PeopleEvents4Math.ExamFail(0, 0, false) == 0.95f, "the chance on the card: Intellectual and Social before the colony, Intellectual alone in private");
        C(PeopleEvents4Math.ExamGapDays >= 30 && PeopleEvents4Math.BookEveryDays == 15 && PeopleEvents4Math.ExamXp > 0f && PeopleEvents4Math.MaxFellows >= 6, "not asked again soon; a book each season");

        // ================= 21. The summer school =================

        C(!PeopleEvents4Math.LessonHour(12) && PeopleEvents4Math.LessonHour(13) && PeopleEvents4Math.LessonHour(18) && !PeopleEvents4Math.LessonHour(19) && PeopleEvents4Math.LessonsPerDay == 2,
            "a lesson each afternoon, two a day at most");
        C(PeopleEvents4Math.LessonXp(true) > PeopleEvents4Math.LessonXp(false) && PeopleEvents4Math.LendBooks == 3 && PeopleEvents4Math.BestStudent(2) && !PeopleEvents4Math.BestStudent(1),
            "more learned with the colony's books on the table; the best student has two lessons at least");
        C(PeopleEvents4Math.SettleOdds(0) == 0.35f && Math.Abs(PeopleEvents4Math.SettleOdds(10) - 0.75f) < 1e-4f && PeopleEvents4Math.SettleOdds(20) == 0.9f && PeopleEvents4Math.DisputeChance <= 0.4f
          && PeopleEvents4Math.SulkTension > 0f && PeopleEvents4Math.SettledTension < 0f, "the disputation once a stay at a chance, ended by the talker's Social, else a sulk");

        // ================= 23. A contract to the last digit =================

        C(PeopleEvents4Math.ContractKinds.SequenceEqual(new[] { "meals", "cloth", "beer", "drugs", "art", "components" }), "meals, cloth, beer, drugs, art, components");
        C(PeopleEvents4Math.ContractWorth(1f, 1f) == 500 && PeopleEvents4Math.ContractWorth(0.1f, 0.1f) == 300 && PeopleEvents4Math.ContractWorth(5f, 5f) == 3000
          && PeopleEvents4Math.ContractCount(500, 15f) == 33 && PeopleEvents4Math.ContractCount(500, 0f) == 0, "the contract's worth by wealth and size, in the thing's count");
        C(PeopleEvents4Math.ContractPrice(33, 15f, false) == 590 && PeopleEvents4Math.ContractPrice(33, 15f, true) == 540, "a fifth over its worth, a tenth after a default");
        C(PeopleEvents4Math.DefaultFee(590) == 60 && PeopleEvents4Math.DefaultFee(100) == 30 && PeopleEvents4Math.ShortPay(20, 15f, 60) == 240 && PeopleEvents4Math.ShortPay(1, 15f, 60) == 0,
            "short: what came at its worth, less the fee");
        C(PeopleEvents4Math.NextScale(1f, true) == 1.5f && PeopleEvents4Math.NextScale(2.5f, true) == 3f && PeopleEvents4Math.NextScale(2f, false) == 1f && PeopleEvents4Math.ContractMinDays == 15
          && PeopleEvents4Math.ContractMaxDays == 30 && PeopleEvents4Math.DefaultGoodwill < 0, "a bigger contract after one kept, the first size after a default; due in fifteen to thirty days");

        // ================= 24. Stranded with something to sell =================

        C(PeopleEvents4Math.CargoWorth(1f) == 900 && PeopleEvents4Math.CargoWorth(0.1f) == 500 && PeopleEvents4Math.CargoWorth(10f) == 2250 && PeopleEvents4Math.Share(900) == 180
          && PeopleEvents4Math.CargoCrates(0.2f) == 2 && PeopleEvents4Math.CargoCrates(0.8f) == 3, "two or three crates of cargo, a fifth of it paid");
        C(PeopleEvents4Math.LanderMinHours == 48 && PeopleEvents4Math.LanderMaxHours == 72 && PeopleEvents4Math.RescueGoodwill == 10 && PeopleEvents4Math.RobbedGoodwill < 0
          && PeopleEvents4Math.Robbed(3, 2) && !PeopleEvents4Math.Robbed(3, 3), "the lander two or three days after a rescue, +10; a crate gone is a crate taken");

        // ================= 27. Fouled water =================

        C(PeopleEvents4Math.FoulKinds.SequenceEqual(new[] { "corpse", "pollution", "wastepack" }) && PeopleEvents4Math.FoulReach == 3 && PeopleEvents4Math.CleanGoodwill == 5
          && PeopleEvents4Math.AloneGoodwill < PeopleEvents4Math.CleanGoodwill && PeopleEvents4Math.StopDays >= 30 && PeopleEvents4Math.Clean(0) && !PeopleEvents4Math.Clean(1),
            "a body, poisoned ground or a wastepack by the water; cleaned together +5, the catches stopped until clean");

        // ================= 28. Lanterns on the water =================

        C(PeopleEvents4Math.Lanterns(0, 0) == 3 && PeopleEvents4Math.Lanterns(4, 3) == 7 && PeopleEvents4Math.Lanterns(30, 30) == 12 && PeopleEvents4Math.LanternsSet == 6
          && PeopleEvents4Math.LanternHour(16) && !PeopleEvents4Math.LanternHour(20), "a lantern for everyone lost or taken in, at dusk");

        // ================= 29. Flooded out =================

        C(PeopleEvents4Math.FoodDays == 3 && PeopleEvents4Math.FloodSickChance <= 0.1f && PeopleEvents4Math.GuestsMax == 3 && PeopleEvents4Math.KnotGoodwill == 12 && PeopleEvents4Math.FloodTurnedGoodwill < 0,
            "food until their catch comes in; a rare sickness; a knot in the net, +12");

        // ================= 30. The boat-builders =================

        var stages = new[] { 0f, 0.9f, 1f, 2.9f, 3f, 4.9f, 5f, 7f }.Select(PeopleEvents4Math.HullStage).ToList();
        C(stages.SequenceEqual(new[] { 0, 0, 1, 1, 2, 2, 3, 3 }) && PeopleEvents4Math.HullStage(float.NaN) == 0 && PeopleEvents4Math.HullStages == 3, "the keel, the ribs, the planked hull, day by day");
        C(PeopleEvents4Math.DinghySilver(1f) == 350 && PeopleEvents4Math.DinghySilver(0.1f) == 200 && PeopleEvents4Math.DinghySilver(10f) == 800 && PeopleEvents4Math.DinghyWood == 60
          && PeopleEvents4Math.TreesRefusedGoodwill < 0 && PeopleEvents4Math.MinTrees >= 5, "the dinghy for wood and silver; trees refused, cooler");

        // ================= 31. A page in the book =================

        C(PeopleEvents4Math.PageKinds.SequenceEqual(new[] { "legend", "nemesis", "alpha" }) && PeopleEvents4Math.PeltPrice(10, 5f) == 100 && PeopleEvents4Math.PeltPrice(0, 5f) == 20
          && PeopleEvents4Math.PeltCount(40f, 25) == 25 && PeopleEvents4Math.PeltCount(40f, 100) == 40 && PeopleEvents4Math.KillSkill(true) == "Shooting" && PeopleEvents4Math.KillSkill(false) == "Melee",
            "a legend, a nemesis or an alpha; the pelt at twice its worth; a passion in the skill that made the kill");
        C(PeopleEvents4Math.PageGoodwill == 6 && PeopleEvents4Math.PageChance <= 0.2f && PeopleEvents4Math.PageMinHours >= 2 && PeopleEvents4Math.PageExpireDays >= 5, "some hours after the kill, +6");

        // ================= 32. Wardens at the manhunt =================

        C(PeopleEvents4Math.Beset(3, 0.5f) && PeopleEvents4Math.Beset(1, 1.5f) && !PeopleEvents4Math.Beset(1, 0.5f) && !PeopleEvents4Math.Beset(0, 3f), "a manhunting pack or a mad beast of a size");
        C(PeopleEvents4Math.ManhuntWardens(1) == 2 && PeopleEvents4Math.ManhuntWardens(8) == 4 && PeopleEvents4Math.ManhuntWardens(40) == 4 && PeopleEvents4Math.ManhuntGoodwill == 3
          && PeopleEvents4Math.ManhuntChance <= 0.5f && PeopleEvents4Math.ManhuntGapDays >= 10, "two to four Wardens, at a chance, not soon again, +3");

        // ================= 33. The hive burned =================

        C(PeopleEvents4Math.Fire(0) == "torch" && PeopleEvents4Math.Fire(1) == "torch" && PeopleEvents4Math.Fire(2) == "molotov" && PeopleEvents4Math.Fire(3) == "incendiary"
          && PeopleEvents4Math.FireWeapon(1) == null && PeopleEvents4Math.FireWeapon(2) == "Weapon_GrenadeMolotov" && PeopleEvents4Math.FireWeapon(3) == "Gun_IncendiaryLauncher",
            "torches, molotovs, an incendiary launcher by their age");
        C(PeopleEvents4Math.HiveWardens(1) == 3 && PeopleEvents4Math.HiveWardens(9) == 5 && PeopleEvents4Math.HiveWaitHours >= 1 && PeopleEvents4Math.HiveWatchHours == 24 && PeopleEvents4Math.HiveGoodwill > 0,
            "three to five Wardens; together they wait for the colony's fighters, refused they keep watch a day");

        // ================= Their defs =================

        var events = XDocument.Load(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents4.xml")).Root!.Elements("IncidentDef").ToDictionary(Dn);
        C(events.Keys.SequenceEqual(new[] { "Rimfolk_Evt_FellowExamined", "Rimfolk_Evt_SummerSchool", "Rimfolk_Evt_EnclaveContract", "Rimfolk_Evt_ContractDue", "Rimfolk_Evt_BrokerPod",
            "Rimfolk_Evt_FouledWater", "Rimfolk_Evt_KnotLanterns", "Rimfolk_Evt_FloodedOut", "Rimfolk_Evt_BoatBuilders", "Rimfolk_Evt_WardensPage", "Rimfolk_Evt_WardensManhunt",
            "Rimfolk_Evt_HiveBurned" }), "twelve events: eight drawn, four fired when their moment comes");
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { src }.Concat(parts).ToArray()));
        string dir = Path.Combine(src, "Peoples", "Events4");
        string code = string.Join("\n", Directory.GetFiles(dir, "*.cs").Where(f => !f.EndsWith("PeopleEvents4Math.cs")).OrderBy(f => f).Select(File.ReadAllText));
        string everyCode = string.Join("\n", Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        var callings = new Dictionary<string, string>
        {
            ["Rimfolk_Evt_FellowExamined"] = "Scholars", ["Rimfolk_Evt_SummerSchool"] = "Scholars", ["Rimfolk_Evt_EnclaveContract"] = "Brokers", ["Rimfolk_Evt_BrokerPod"] = "Brokers",
            ["Rimfolk_Evt_FouledWater"] = "Fishers", ["Rimfolk_Evt_FloodedOut"] = "Fishers", ["Rimfolk_Evt_BoatBuilders"] = "Fishers", ["Rimfolk_Evt_WardensPage"] = "Hunters",
            ["Rimfolk_Evt_WardensManhunt"] = "Hunters", ["Rimfolk_Evt_HiveBurned"] = "Hunters"
        };
        // The two whose people are their own record's: the contract's day (the enclave that signed it) and the knot night (the river folk whose night it is).
        C(code.Contains("PeoplesD.Friendly(PeoplesMath.Brokers)") && Regex.IsMatch(code, @"PeoplesD\.Theirs\(f, PeoplesMath\.Fishers\) \|\| f\.HostileTo\(Faction\.OfPlayer\)")
          && code.Contains("r.faction.HostileTo(Faction.OfPlayer)"), "the contract's day and the knot night only from their own people, at peace");
        var fired = new Dictionary<string, string>
        {
            ["Rimfolk_Evt_ContractDue"] = "PeopleEvents2.Fire(DueEvent, r.map", ["Rimfolk_Evt_KnotLanterns"] = "PeopleEvents2.Fire(EventDef, map, f)",
            ["Rimfolk_Evt_WardensPage"] = "PeopleEvents2.Fire(EventDef, map, f)", ["Rimfolk_Evt_WardensManhunt"] = "PeopleEvents2.Fire(EventDef, map, f)"
        };
        var defaults = new Dictionary<string, string> { ["Rimfolk_Evt_KnotLanterns"] = "1", ["Rimfolk_Evt_WardensManhunt"] = "0" };
        var lastAnswers = new[]
        {
            "ColonyCouncil.DilemmaEffect_SendAway", "ColonyCouncil.DilemmaEffect_BrokerPod", "ColonyCouncil.DilemmaEffect_WardensManhunt", "ColonyCouncil.DilemmaEffect_HiveBurned"
        };
        foreach (var (name, e) in events)
        {
            string worker = e.Element("workerClass")!.Value.Replace("ColonyCouncil.", "");
            var m = Regex.Match(code, @"class " + worker + @" : HappeningWorker_Visitors\s*\{(.*?)\n    \}", RegexOptions.Singleline);
            C(m.Success, $"{name}: its worker is the round's own");
            if (callings.TryGetValue(name, out var calling))
                C(m.Success && m.Groups[1].Value.Contains("PeoplesMath." + calling) && m.Groups[1].Value.Contains("PeoplesD.Friendly("), $"{name}: only from a people of its calling at peace with the colony");
            var effects = e.Descendants("li").Select(li => li.Attribute("Class")?.Value).Where(v => v != null && v.StartsWith("ColonyCouncil.DilemmaEffect_")).Select(v => v.Replace("ColonyCouncil.", "")).Distinct();
            C(effects.All(x => Regex.IsMatch(everyCode, @"class " + x + @"\b")), $"{name}: every answer's effect is real");
            var ext = Ext(e, "HappeningExtension")!;
            float chance = F(e.Element("baseChance")!.Value);
            if (fired.TryGetValue(name, out var how)) C(chance == 0f && code.Contains("\"" + name + "\"") && code.Contains(how), $"{name}: never drawn, fired only when its moment comes");
            else
                C(chance >= 0.3f && chance <= 0.8f && int.Parse(e.Element("minRefireDays")!.Value) >= 30 && int.Parse(e.Element("earliestDay")!.Value) >= 10,
                    $"{name}: a small, spread chance, not soon again, not the first moment it is free");
            C(ext.Element("group")?.Value == "Visitors", $"{name}: among the visitors, so the storytellers' lean on visitors reads it");
            var texts = ext.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
            C(texts.Count >= 5 && texts.Any(t => Regex.IsMatch(t, @"^\[[^\]]*(lowtech|tribal|medieval|industrial|spacer|age\d|pod|walk|floating|torches|torch|molotov|lowcolony)")),
                $"{name}: five or more texts, in the forms of the age");
            var options = ext.Element("options")!.Elements("li").ToList();
            C(options.Count == 3 && ext.Element("defaultOption")?.Value == (defaults.TryGetValue(name, out var d) ? d : "2"), $"{name}: three answers, no answer the last (or as its note says)");
            C(options[2].Descendants("li").Any(li => lastAnswers.Contains(li.Attribute("Class")?.Value)), $"{name}: the last answer sends them on");
            var lineTags = ext.Element("lines")?.Elements("li").Select(l => Regex.Match(l.Value, @"^\[([^\],]+)").Groups[1].Value).Distinct().ToList() ?? new List<string>();
            C(lineTags.Count > 0 && lineTags.All(t => code.Contains("\"" + t + "\"")), $"{name}: every line's moment is one the code says it at");
            C(ext.Element("lines")?.Elements("li").All(l => Regex.Matches(Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", ""), @"[.?](\s|$)").Count == 1) ?? true, $"{name}: every spoken line one sentence");
        }
        C(Ext(events["Rimfolk_Evt_SummerSchool"], "HappeningExtension")!.Element("seasons")!.Elements("li").Any(l => l.Value == "Summer"), "the summer school only in summer");
        C(Regex.IsMatch(code, @"override float RiskChance\(HappeningContext ctx, int option, float chance\) =>\s*option == 0 \? PeopleEvents4Math\.ExamFail\(Fellowship\.Intellectual\(ctx\.a\), Fellowship\.Social\(ctx\.a\), true\)"),
            "the examination's chance on the card is the rule's own");
        C(Ext(events["Rimfolk_Evt_FellowExamined"], "HappeningExtension")!.Element("options")!.Elements("li").Take(2).All(o => o.Element("riskReplaces")?.Value == "true" && o.Element("riskEffects") != null),
            "the examination passed or failed by the card's roll, before the colony or in private");

        // The camps.
        var campKinds = XDocument.Load(Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKinds_PeopleEvents4.xml")).Root!.Elements("ColonyCouncil.CampKindDef").ToDictionary(Dn);
        C(campKinds.Keys.SequenceEqual(new[] { "Rimfolk_Camp_SummerSchool", "Rimfolk_Camp_FloodedOut", "Rimfolk_Camp_BoatBuilders" }), "three camps of their own");
        foreach (var (name, k) in campKinds)
        {
            string worker = k.Element("workerClass")!.Value.Replace("ColonyCouncil.", "");
            C(F(k.Element("weight")!.Value) == 0f && code.Contains("class " + worker + " : CampWorker"), $"{name}: never on the camps' own draw; ways of its own ({worker})");
            C(k.Descendants("li").Where(li => li.Parent?.Name.LocalName is "lines" or "momentLines").All(l => Regex.Matches(Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", ""), @"[.?](\s|$)").Count == 1),
                $"{name}: every spoken line one sentence");
            var moments = k.Element("momentLines")?.Elements("li").Select(l => Regex.Match(l.Value, @"^\[([^\]]+)\]").Groups[1].Value).Distinct().ToList() ?? new List<string>();
            C(moments.All(t => code.Contains("\"" + t + "\"") || t == "fish"), $"{name}: every moment line is said at a moment the code or the camps have");
            var marks = k.Element("farewell")!.Elements("li").SelectMany(l => Regex.Match(l.Value, @"^\[([^\]]+)\]").Groups[1].Value.Split(',')).Where(t => t.Length > 0).Distinct().ToList();
            C(marks.All(t => code.Contains("marks.Add(\"" + t + "\")")), $"{name}: every farewell is for a mark the code gives");
            var askKeys = k.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).ToList();
            C(askKeys.Where(a => code.Contains("case \"" + a + "\"") || code.Contains("a?.key == \"" + a + "\"")).Count() >= 1, $"{name}: its own asks are minded by its worker");
        }
        var school = campKinds["Rimfolk_Camp_SummerSchool"];
        C(school.Element("calling")!.Value == "scholars" && school.Element("days")!.Value == "7~7" && school.Element("structures")!.Elements("li").Any(s => s.Element("role")?.Value == "table")
          && school.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).SequenceEqual(new[] { "lessonInt", "lessonMed", "lessonSoc", "lend", "food", "settle" }),
            "the summer school: a week at its table; lessons in learning, medicine and speaking, the books lent, fed, the argument ended");
        var flood = campKinds["Rimfolk_Camp_FloodedOut"];
        C(flood.Element("calling")!.Value == "fishers" && flood.Element("days")!.Value == "5~10" && flood.Element("family")?.Value == "true" && flood.Element("work")?.Value == "fish"
          && F(flood.Element("joinChance")!.Value) >= 0.15f && F(flood.Element("extendChance")!.Value) >= 0.3f
          && flood.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).SequenceEqual(new[] { "food", "catch", "guests" }),
            "the flooded families: five to ten days, fishing; food, their catch, their old and young as guests; some may stay, or ask to stay longer");
        var boat = campKinds["Rimfolk_Camp_BoatBuilders"];
        C(boat.Element("calling")!.Value == "fishers" && boat.Element("sleep")!.Value == "Hut" && boat.Element("leaves") == null && boat.Element("works")!.Elements("li").Any(l => l.Value == "Chop")
          && boat.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).SequenceEqual(new[] { "crafter", "dinghy" }),
            "the boat-builders: a shed that stays, the camps' own felling; the crafter beside them, the dinghy");

        // Their jobs, the cord, the hull and the moods.
        var misc = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_PeopleEvents4_Misc.xml")).Root!.Elements().ToList();
        var jobs = misc.Where(e => e.Name.LocalName == "JobDef").ToList();
        C(jobs.Count == 10 && jobs.All(j => everyCode.Contains("class " + j.Element("driverClass")!.Value.Replace("ColonyCouncil.", "") + " : JobDriver") && code.Contains("\"" + Dn(j) + "\"")),
            "ten jobs, each driven by a driver of the round or round 1's and given by the round's code");
        var work = jobs.Where(j => j.Element("driverClass")!.Value == "ColonyCouncil.JobDriver_PEv4Work").ToList();
        C(work.All(j => new[] { "water", "dust", "fire", "sparks", "quiet" }.Contains(j.Descendants("mark").Single().Value)) && jobs.Count(j => j.Element("driverClass")!.Value == "ColonyCouncil.JobDriver_PeopleWorkOn") == 2,
            "the round's own work by its marks; the examination and the cord the visitor's work on a colonist (round 1's shape)");
        C(Enumerable.Range(1, 3).All(i => misc.Any(e => Dn(e) == "Rimfolk_BoatHull_" + i && e.Element("graphicData")!.Element("texPath")!.Value == "ColonyCouncil/Things/BoatHull_" + i)
                                          && File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Things", "BoatHull_" + i + ".png")))
          && !misc.Any(e => e.Element("designationCategory") != null), "the hull drawn in its three stages, never built by hand");
        C(misc.Any(e => e.Name.LocalName == "HediffDef" && Dn(e) == "Rimfolk_RedCord" && e.Element("isBad")?.Value == "false") && code.Contains("\"Rimfolk_RedCord\""), "the red cord, no harm in it");
        var thoughts = misc.Where(e => e.Name.LocalName == "ThoughtDef").ToDictionary(Dn);
        C(thoughts.Keys.All(t => code.Contains("\"" + t + "\"") || File.ReadAllText(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents4.xml")).Contains(t))
          && F(thoughts["Rimfolk_Thought_FellowNamed"].Descendants("baseMoodEffect").Single().Value) > 0f && F(thoughts["Rimfolk_Thought_FellowStung"].Descendants("baseMoodEffect").Single().Value) < 0f
          && F(thoughts["Rimfolk_Thought_PageInBook"].Element("durationDays")!.Value) >= 30f, "the lasting memories, the sting, the lanterns kept by those who came");
        C(misc.Any(e => e.Name.LocalName.EndsWith("MoodHintDef") && e.Descendants("li").Any(l => l.Value == "Rimfolk_Thought_FellowStung")), "the sting says what to do about it");

        // The player's mods, drawn on softly and from their own catalogue.
        var active = Catalogue(root).Where(r => r.active).Select(r => r.type + ":" + r.def).ToHashSet();
        C(PeopleEvents4Math.BookDefs.All(b => active.Contains("ThingDef:" + b)) && !PeopleEvents4Math.BookDefs.Contains("Tome") && code.Contains("t.def.defName != \"Tome\""),
            "the society's books by name are in the player's mods; Anomaly's tome never");
        C(active.Contains("ExpertiseDef:Researcher") && active.Contains("ExpertiseDef:Hunter") && code.Contains("Expertise.Give(p, \"Researcher\")") && code.Contains("Expertise.Give(hunter, \"Hunter\")")
          && Read("Integrations", "Expertise.cs").Contains("public static bool Give(Pawn p, string defName)"), "Vanilla Skills Expanded's Researcher and Hunter expertise, given by the mod's own way");
        C(PeopleEvents4Math.CargoDefs.All(c => active.Contains("ThingDef:" + c)) && active.Contains("ThingDef:Beer") && active.Contains("ThingDef:Cloth") && active.Contains("ThingDef:ComponentIndustrial"),
            "the cargo and the contract's goods are the game's own");
        C(active.Contains("ThingDef:Wastepack") && active.Contains("ThingDef:Stagz_WaterCandle") && active.Contains("ThingDef:TorchLamp") && code.Contains("\"Stagz_WaterCandle\""),
            "the wastepacks; Merren's floating lanterns, else the game's torches");
        C(PeopleEvents4Math.RackDefs.All(r => active.Contains("ThingDef:" + r)) && active.Contains("VehicleDef:VVE_Dinghy") && code.Contains("\"Vehicles.VehicleSpawner\""),
            "the families' drying racks and Vanilla Vehicles Expanded's dinghy by name, in the player's mods");
        C(PeopleEvents4Math.HiveDefs.All(h => active.Contains("ThingDef:" + h)) && !PeopleEvents4Math.HiveDefs.Any(h => h.Contains("Artificial"))
          && new[] { 2, 3 }.All(a => active.Contains("ThingDef:" + PeopleEvents4Math.FireWeapon(a))), "the game's and Insectoids 2's wild hives (never a colony's own), the fire's arms");
        string wsDefs = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Defs", "WorldSituationDefs"), "*.xml").Select(File.ReadAllText));
        C(wsDefs.Contains("<defName>Council_WS_Flood</defName>") && code.Contains("\"Council_WS_Flood\""), "the flood they come with");
        C(code.Contains("Rimfolk_Scholars_Don") && code.Contains("Rimfolk_Scholars_Fellow") && code.Contains("Rimfolk_Enclave_Factor") && code.Contains("Rimfolk_Enclave_Marshal")
          && code.Contains("Rimfolk_Enclave_Hand") && code.Contains("Rimfolk_RiverFolk_Warden") && code.Contains("Rimfolk_RiverFolk_Ferryman") && code.Contains("Rimfolk_RiverFolk_Fisher")
          && code.Contains("Rimfolk_RiverFolk_Master") && code.Contains("Rimfolk_Wardens_Master") && code.Contains("PeoplesD.FighterKind(ctx.faction)"), "each people's own kind of person comes");

        // ================= Their words =================

        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeopleEvents4.xml")).Root!.Elements().Select(e => e.Name.LocalName).ToHashSet();
        var used = Regex.Matches(code, "\"(CC_PEv4_\\w+)\"").Select(mm => mm.Groups[1].Value).Where(k => !k.EndsWith("_")).Distinct().ToList();
        C(used.Count >= 120 && used.All(keyed.Contains), $"every word the code says is keyed ({used.Count}{(used.Any(u => !keyed.Contains(u)) ? ", missing " + string.Join(", ", used.Where(u => !keyed.Contains(u))) : "")})");
        C(PeopleEvents4Math.FoulKinds.All(f => keyed.Contains("CC_PEv4_Foul_" + f)) && PeopleEvents4Math.PageKinds.All(p => keyed.Contains("CC_PEv4_Page_" + p))
          && new[] { 0, 2, 3 }.All(a => keyed.Contains("CC_PEv4_Fire_" + PeopleEvents4Math.Fire(a))) && Enumerable.Range(1, 3).All(i => keyed.Contains("CC_PEv4_Hull_" + i)),
            "each fouling, each great beast, each fire and each stage of the hull, with words");
        var texts4 = new[] { Path.Combine("Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents4.xml"), Path.Combine("Defs", "CampDefs", "Rimfolk_CampKinds_PeopleEvents4.xml"),
                Path.Combine("Languages", "English", "Keyed", "Rimfolk_PeopleEvents4.xml"), Path.Combine("Defs", "Misc", "Rimfolk_PeopleEvents4_Misc.xml") }
            .Select(x => Regex.Replace(File.ReadAllText(Path.Combine(root, x)), "<!--.*?-->|<\\?xml[^>]*>|\\[[^\\]]*\\]", "", RegexOptions.Singleline)).Aggregate((x, y) => x + y);
        C(!texts4.Contains('—') && !texts4.Contains('!') && !Regex.IsMatch(texts4, @"\bin \d+ (days?|hours?)\b"), "plain words, no countdowns");
        C(!Regex.IsMatch(texts4.ToLowerInvariant(), @"\b(dynasty|dynasties|bloodline|heir to the house)\b"), "the colony is the family: no houses");
        C(!Regex.IsMatch(texts4, @"\b(she|her|hers)\b"), "nobody of theirs is given a sex the game didn't");
        C(!Regex.IsMatch(texts4, @"\b(god|gods|goddess)\b", RegexOptions.IgnoreCase), "the gods unseen: no god named");
        string peoples = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Defs", "AlmanacArticleDefs"), "Almanac_Peoples*.xml").Select(File.ReadAllText)).ToLowerInvariant();
        C(peoples.Contains("corresponding fellow") && peoples.Contains("summer school") && peoples.Contains("contract to the last digit") && peoples.Contains("stranded")
          && peoples.Contains("fouled water") && peoples.Contains("lanterns on the water") && peoples.Contains("flooded out") && peoples.Contains("boat-builders") && peoples.Contains("red cord")
          && peoples.Contains("manhunt") && peoples.Contains("hive burned"), "each people's Almanac page says what it does now");

        // ================= The seams =================

        C(Read("Peoples", "Events3", "PeopleEvents3.cs").Contains("return PeopleEvents4.Hour(v, comp, now);") && Read("Peoples", "Events3", "PeopleEvents3.cs").Contains("PeopleEvents4.Hourly(now)"),
            "round 4's visits and watch wait on round 1's hour, through rounds 2 and 3");
        var kinds = Regex.Matches(code, @"public const string ((?!Gift)\w*Kind) = ""([^""]+)"";").Select(mm => mm.Groups[2].Value).ToList();
        C(kinds.Count == 5 && kinds.All(k => k.StartsWith("pev:")) && kinds.Distinct().Count() == 5 && Read("Happenings", "Happenings.cs").Contains("case var k when PeopleEvents.Owns(k)"),
            "their visits are the events' own kind (\"pev:\"), the only ones the tracker hands to round 1's hour, none twice");
        string happenings = Read("Happenings", "Happenings.cs");
        C(happenings.Contains("note.great = victim.RaceProps.Animal ? WardensPage.GreatKind(victim) : null;") && happenings.Contains("WardensPage.Killed(victim, dinfo, note.great, note.map)")
          && happenings.Contains("WardensManhunt.Killed(victim, dinfo)"), "a great beast's kind taken before its death, the page and the manhunt's kills after it");
        C(Read("Interplay", "Interplay_Gatherings.cs").Contains("Fellowship.GatheringOver(play, attended, early);"), "the examination's verdict at the evening's end");
        C(Read("Peoples", "PeoplesCallers.cs").Contains("!FouledWater.CatchesStopped(map)") && Read("Peoples", "PeoplesLore.cs").Contains("!FouledWater.CatchesStopped(map)"),
            "the boat-trader and the nets stop while fouled water is left");
        C(Read("Peoples", "Events2", "RiverCrossing.cs").Contains("KnotLanterns.AtCrossing(hc)"), "at a crossing of theirs, the knot night's rest sets the lanterns");
        C(Read("Text", "Facts", "FactsMath.cs").Contains("\"them:camp:SummerSchool\", \"them:camp:FloodedOut\", \"them:camp:BoatBuilders\""), "the facts service knows the round's camps");
        C(Read("Peoples", "Events", "PeopleWorkOn.cs").Contains("case \"quiet\": break;"), "round 1's work on a colonist has a quiet mark");
        C(code.Contains("PeopleWorkOn.Begin(") && code.Contains("PlayOuts.Collected().LastOrDefault(p => p.IsGathering)") && code.Contains("CampAnswers.Carry(") && code.Contains("Litters.CarryJob")
          && code.Contains("PeoplesD.Divide(") && code.Contains("new LordJob_AssaultThings(") && code.Contains("InvitedGuests.HandOver(") && code.Contains("Couriers.Deliver(")
          && code.Contains("DropPodUtility.DropThingsNear(") && code.Contains("HealthUtility.DamageUntilDowned(") && code.Contains("WorldLedger.AddTie(Tie.Kind.Debt"),
            "the shapes reused: work on a colonist, a gathering's verdict, the camps' carrying, the litter, the hunt's split, the game's assault, Hospitality's guests, the post, the lander, the pod crash, the debts");
        var harms = Regex.Matches(code, @"Harm\.(Roll|Ail|Injure)\(([^,;]*,\s*)?HarmKinds\.(\w+)").Select(mm => mm.Groups[3].Value).Distinct().OrderBy(x => x).ToList();
        C(harms.SequenceEqual(new[] { "CampFever" }) && !Regex.IsMatch(code, @"TakeDamage") && code.Contains("IllnessBudget.Allows(") && code.Contains("IllnessBudget.Owe("),
            "the only harm of their own the flood's sickness, through the harm and illness budgets; the fights the game's own");
        C(Regex.Matches(code, @"\[HarmonyPatch\(").Count == 0, "no game hook of its own");
        C(!code.Contains("MoteMaker.ThrowText") && code.Contains("PeoplesG.SayLine(") && code.Contains("c.SayMoment(") && code.Contains("PeoplesG.Say("), "they speak through the events' and the camps' own lines");
        C(!Regex.IsMatch(code, @"Sched\.|\bLocomotionUrgency\.Sprint"), "no clock of their own: the events' hour and the camps' hour");
        C(Regex.Matches(code, @"StoryDirection\.Factor\(StoryWaysMath\.Visitors\)").Count >= 2, "the code-fired comings read the storytellers' lean on visitors");
        string book = File.ReadAllText(Path.Combine(dir, "PeopleEvents4Book.cs"));
        C(book.Contains("PeopleEvents4Math.MaxFellows") && book.Contains("PeopleEvents4Math.MaxPages") && book.Contains("contracts.Count > 6") && book.Contains("pods.Count > 4")
          && book.Contains("fouled.Count > 4") && book.Contains("lanternYears.Count > 12"), "what they keep is bounded");
        C(Regex.Matches(code, @"\[DebugAction\(""Rimfolk"", ""Peoples' events 4: ").Count == 9, "nine dev actions");
        string save = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(save.Contains("Peoples/Events4/PeopleEvents4Book.cs:pages") && save.Contains("Peoples/Events4/SummerSchool.cs:students"), "their saved lists are in the save model");
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
            if (c.Length >= 4) catalogue.Add((c[0], c[1], c[2], OffForNow.Counts(root, c[3], c[2])));
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
