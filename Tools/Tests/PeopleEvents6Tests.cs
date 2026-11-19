using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The peoples' own events, round 6 (Peoples/Events6/; docs/peoples-events-proposals.md, items 48, 49, 50, 52, 53, 54, 55,
/// 56, 57 and 58): their pure rules (the rain in a dry spell and its storm, the tide held and whom it touches, the storm
/// tower's week and how it turns, the fire in the canopy, the seeds' price and plot, the camp after the fire and its term,
/// the contract's price, company and split, the old soldier's musters and his horse, the other side's contract and the
/// Banner's share of the raid, the company on the march and how it turns), their defs and words, the player's mods they draw
/// on, and the seams with the game and rounds 1 to 5.
/// </summary>
public static class PeopleEvents6Tests
{
    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the peoples' events, round 6: " + what);
        }
        float F(string v) => float.Parse(v, CultureInfo.InvariantCulture);

        // ================= 48. Breaking the drought =================

        C(PeopleEvents6Math.Dry("Drought") && PeopleEvents6Math.Dry("VEE_Drought") && PeopleEvents6Math.Dry("HeatWave") && !PeopleEvents6Math.Dry("ColdSnap") && !PeopleEvents6Math.Dry(null),
            "the droughts of Odyssey and Vanilla Events Expanded and the game's heat wave");
        C(PeopleEvents6Math.RainWalkers == 2 && PeopleEvents6Math.DryHours >= 6 && PeopleEvents6Math.RainChance <= 0.5f && PeopleEvents6Math.RainGapDays >= 15, "two storm-walkers, once a dry spell has run, at a chance, not soon again");
        C(PeopleEvents6Math.RainFee(1f) == 200 && PeopleEvents6Math.RainFee(0.1f) == 120 && PeopleEvents6Math.RainFee(5f) == 400 && PeopleEvents6Math.RainFee(float.NaN) == 200,
            "their fee by the colony's wealth, within reason");
        C(PeopleEvents6Math.Storm(0.1f) && !PeopleEvents6Math.Storm(0.25f) && !PeopleEvents6Math.Storm(0.9f) && Math.Abs(PeopleEvents6Math.StormOdds - 0.25f) < 1e-6f, "one time in four, a thunderstorm");
        C(PeopleEvents6Math.SetUpHour(17) && PeopleEvents6Math.SetUpHour(23) && PeopleEvents6Math.SetUpHour(2) && !PeopleEvents6Math.SetUpHour(5) && !PeopleEvents6Math.SetUpHour(12)
          && PeopleEvents6Math.RainNow(6) && !PeopleEvents6Math.RainNow(5) && !PeopleEvents6Math.RainNow(12), "the rods from dusk, the rain at first light");
        C(PeopleEvents6Math.RainGoodwill > 0 && PeopleEvents6Math.NightXp > 0f, "their thanks, and the night's lesson");

        // ================= 49. The tide read =================

        C(PeopleEvents6Math.Tide("PsychicDrone") && PeopleEvents6Math.Tide("PsychicSoothe") && !PeopleEvents6Math.Tide("ColdSnap") && PeopleEvents6Math.Drone("PsychicDrone")
          && !PeopleEvents6Math.Drone("PsychicSoothe"), "the game's drone and soothe are held; only a drone brings the reader");
        C(PeopleEvents6Math.Worst(new[] { 0.6f, 0.2f, 0.4f }) == 1 && PeopleEvents6Math.Worst(new float[0]) == -1 && PeopleEvents6Math.Worst(null) == -1
          && PeopleEvents6Math.Worst(new[] { float.NaN, 0.3f }) == 1, "the one the drone touches worst: the lowest mood");
        C(PeopleEvents6Math.SitTicks == 2500 && PeopleEvents6Math.TideForgetDays >= 2 && PeopleEvents6Math.SitGoodwill > 0, "an hour each, a held tide let go after a few days");

        // ================= 50. The storm tower =================

        C(PeopleEvents6Math.TowerReaders == 2 && PeopleEvents6Math.TowerDays == 7 && PeopleEvents6Math.TowerMealsGoodwill > 0 && PeopleEvents6Math.TowerGoodwill > 0,
            "two readers and an apprentice, a week; warmer with the meals");
        C(PeopleEvents6Math.StrikeChance <= 0.3f && PeopleEvents6Math.ChillChance <= 0.25f && PeopleEvents6Math.StrikeMin > 18f && PeopleEvents6Math.StrikeMax > PeopleEvents6Math.StrikeMin
          && PeopleEvents6Math.StrikeClear >= 4f, "it turns rarely: a strike past the rods' reach and clear of anyone, a chill");
        C(PeopleEvents6Math.PastRods(20f, PeoplesEMath.RodRadius) && !PeopleEvents6Math.PastRods(18.5f, PeoplesEMath.RodRadius) && !PeopleEvents6Math.PastRods(float.NaN, PeoplesEMath.RodRadius),
            "the stray strike lands past the rods' reach");
        C(PeopleEvents6Math.ChillNight(2f, 23, true, 20f) && PeopleEvents6Math.ChillNight(3f, 2, false, 5f) && !PeopleEvents6Math.ChillNight(1f, 23, true, 0f)
          && !PeopleEvents6Math.ChillNight(3f, 14, true, 0f) && !PeopleEvents6Math.ChillNight(3f, 23, false, 20f), "the chill only after two days, at night, in the rain or the cold");

        // ================= 52. Fire in the canopy =================

        C(PeopleEvents6Math.CanopyFires >= 5 && PeopleEvents6Math.GuardsMin == 3 && PeopleEvents6Math.GuardsMax == 5 && PeopleEvents6Math.CanopyChance <= 0.6f && PeopleEvents6Math.CanopyGapDays >= 10,
            "a wildfire among the trees brings three to five thorn-guards, at a chance, not soon again");
        C(PeopleEvents6Math.Out(0) && !PeopleEvents6Math.Out(1) && PeopleEvents6Math.CanopyRadius >= 30f && PeopleEvents6Math.CanopyHours >= 8, "they stay until the fire within their reach is out");

        // ================= 53. Seeds of the old wood =================

        C(PeopleEvents6Math.SeedPrice(16, 20f) == 80 && PeopleEvents6Math.SeedPrice(0, 0f) == 60 && PeopleEvents6Math.SeedPrice(16, 1000f) == 300 && PeopleEvents6Math.SeedPrice(9, float.NaN) == 60,
            "the seeds at a quarter of a plot's first harvest, within reason");
        C(PeopleEvents6Math.SwapCount(80, 1.1f) == 91 && PeopleEvents6Math.SwapCount(80, 0f) == 0 && PeopleEvents6Math.SwapCount(80, float.NaN) == 0 && PeopleEvents6Math.SwapCount(10, 100f) == 1,
            "swapped for a quarter again in the colony's own harvest");
        C(PeopleEvents6Math.PlotSide == 4 && PeopleEvents6Math.PlotSmall == 3 && PeopleEvents6Math.SowTicks >= 5000 && PeopleEvents6Math.GrowerXp > 0f, "a plot of four by four, three by three at a pinch");

        // ================= 54. After the fire =================

        C(PeopleEvents6Math.BurnedMin == 10 && PeopleEvents6Math.BlightMin == 6 && PeopleEvents6Math.AfterDays == 7 && PeopleEvents6Math.AfterGapDays >= 20, "after ten burned trees or a blight on six plants, a week");
        C(PeopleEvents6Math.PlantingRadius(0f) == 6f && PeopleEvents6Math.PlantingRadius(10f) == 12f && PeopleEvents6Math.PlantingRadius(100f) == 30f && PeopleEvents6Math.PlantingRadius(float.NaN) == 6f,
            "the planting reaches the farthest of the burn, within reason");
        C(PeopleEvents6Math.Sing(19) && PeopleEvents6Math.Sing(22) && !PeopleEvents6Math.Sing(18) && !PeopleEvents6Math.Sing(23), "they sing at night by their fire");
        C(PeopleEvents6Math.FellWrongGoodwill < 0 && PeopleEvents6Math.AfterGoodwill > 0 && PeopleEvents6Math.RekindleChance <= 0.2f && PeopleEvents6Math.MaxSaplings <= 30 && PeopleEvents6Math.SaplingsApartSq >= 4,
            "a wrong when the colony fells within the planting; the fire starts again rarely; the saplings bounded and spaced");

        // ================= 55. A contract on the colony's enemy =================

        C(PeopleEvents6Math.ContractPrice(1000f, false) == 600 && PeopleEvents6Math.ContractPrice(1000f, true) == 900 && PeopleEvents6Math.ContractPrice(0f, false) == 200
          && PeopleEvents6Math.ContractPrice(100000f, true) == 2000, "the price by the threat, a town dearer than a camp");
        foreach (int p in new[] { 200, 600, 870, 2000 })
            C(PeopleEvents6Math.UpFront(p) == Math.Max(10, (int)Math.Round(p * 1.5f / 10f) * 10), $"their share up front is what the carry counts at half again ({p})");
        C(PeopleEvents6Math.Company(0f) == 3 && PeopleEvents6Math.Company(1000f) == 5 && PeopleEvents6Math.Company(99999f) == 8, "the company three to eight by the threat");
        C(PeopleEvents6Math.Share(101) == 50 && PeopleEvents6Math.Share(0) == 0 && PeopleEvents6Math.Share(-5) == 0 && PeopleEvents6Math.LootShare == 0.5f, "half the silver found there, by the contract");
        C(PeopleEvents6Math.ContractTiles >= 8f && PeopleEvents6Math.ContractDays >= 10 && PeopleEvents6Math.LapseGoodwill < 0 && PeopleEvents6Math.ContractGoodwill > 0, "a camp within reach, days enough to get there");

        // ================= 56. An old soldier settles =================

        C(PeopleEvents6Math.MusterYearDays == 60 && PeopleEvents6Math.MusterDays == 7 && PeopleEvents6Math.VeteranMinAge >= 40f && PeopleEvents6Math.VeteranMaxAge > PeopleEvents6Math.VeteranMinAge,
            "once a year, a week at the muster; past the age for the march");
        C(PeopleEvents6Math.MusterFee(1f) == 150 && PeopleEvents6Math.MusterFee(0.1f) == 100 && PeopleEvents6Math.MusterFee(5f) == 300 && PeopleEvents6Math.BuyOut(150) == 450,
            "the fee by the colony's wealth; bought out for three years' fees");
        foreach (int fee in new[] { 100, 150, 230, 300 })
            C(PeopleEvents6Math.BuyOut(fee) == Math.Max(10, (int)Math.Round(fee * 3f / 10f) * 10), $"the buy-out is what the carry counts at three times ({fee})");
        C(PeopleEvents6Math.Mounts(0).SequenceEqual(new[] { "Horse" }) && PeopleEvents6Math.Mounts(2).SequenceEqual(new[] { "Horse" }) && PeopleEvents6Math.Mounts(3)[0] == "GR_ParagonHorse"
          && PeopleEvents6Math.Mounts(3).Last() == "Horse", "the war-horse of the age: the game's horse, the Paragon horse in the spacer age");
        C(PeopleEvents6Math.MusterRefusedGoodwill < 0 && PeopleEvents6Math.MusterGoodwill > 0 && PeopleEvents6Math.MusterAnswerDays >= 1, "refusing both costs goodwill");

        // ================= 57. The other side's contract =================

        C(PeopleEvents6Math.HireChance <= 0.25f && PeopleEvents6Math.HireGapDays >= 30, "the Banner hired against the colony rarely");
        C(PeopleEvents6Math.HoldHours(0f) == 18 && PeopleEvents6Math.HoldHours(0.999f) == 30 && PeopleEvents6Math.HoldHours(2f) == 30 && PeopleEvents6Math.HoldHours(-1f) == 18, "the raid held a day or so");
        C(PeopleEvents6Math.BuyOutPrice(1000f) == 600 && PeopleEvents6Math.StandAsidePrice(1000f) == 300 && PeopleEvents6Math.BuyOutPrice(0f) == 200 && PeopleEvents6Math.StandAsidePrice(0f) == 100
          && PeopleEvents6Math.BuyOutPrice(5000f) > PeopleEvents6Math.StandAsidePrice(5000f), "buying the contract dearer than paying them to stand aside");
        C(PeopleEvents6Math.BannerShare > 0f && PeopleEvents6Math.BannerShare < 0.5f && PeopleEvents6Math.Allies(0f) == 2 && PeopleEvents6Math.Allies(2000f) == 3 && PeopleEvents6Math.Allies(99999f) == 6,
            "marching with the raid, a part of its own size; standing with the colony, two to six");

        // ================= 58. A company on the march =================

        C(PeopleEvents6Math.DrillHour(5) && PeopleEvents6Math.DrillHour(7) && !PeopleEvents6Math.DrillHour(4) && !PeopleEvents6Math.DrillHour(8), "they drill at dawn");
        C(PeopleEvents6Math.FoodPay(300f) == 380 && PeopleEvents6Math.FoodPay(0f) == 0 && PeopleEvents6Math.FoodMeals == 20, "twenty meals bought at a quarter over their worth");
        C(PeopleEvents6Math.CaughtChance <= 0.2f && PeopleEvents6Math.BetChance <= 0.3f && PeopleEvents6Math.StayedGoodwill < 0 && PeopleEvents6Math.EnlistDays == 15 && PeopleEvents6Math.EnlistPay > 0,
            "it turns rarely; a soldier who stays is held against the colony; a season enlisted, with pay");

        // ================= Their defs =================

        string defsPath = Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents6.xml");
        var events = XDocument.Load(defsPath).Root!.Elements("IncidentDef").ToDictionary(Dn);
        C(events.Keys.SequenceEqual(new[] { "Rimfolk_Evt_BreakDrought", "Rimfolk_Evt_TideReader", "Rimfolk_Evt_StormTower", "Rimfolk_Evt_CanopyFire", "Rimfolk_Evt_OldWoodSeeds",
            "Rimfolk_Evt_AfterFire", "Rimfolk_Evt_BannerContract", "Rimfolk_Evt_OldSoldier", "Rimfolk_Evt_BannerMuster", "Rimfolk_Evt_OtherSideContract", "Rimfolk_Evt_CompanyMarch" }),
            "eleven events: five drawn, six fired when their moment comes");
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { src }.Concat(parts).ToArray()));
        string dir = Path.Combine(src, "Peoples", "Events6");
        string code = string.Join("\n", Directory.GetFiles(dir, "*.cs").Where(f => !f.EndsWith("PeopleEvents6Math.cs")).OrderBy(f => f).Select(File.ReadAllText));
        string everyCode = string.Join("\n", Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        var callings = new Dictionary<string, string>
        {
            ["Rimfolk_Evt_BreakDrought"] = "Seers", ["Rimfolk_Evt_TideReader"] = "Seers", ["Rimfolk_Evt_StormTower"] = "Seers", ["Rimfolk_Evt_CanopyFire"] = "Grovekeepers",
            ["Rimfolk_Evt_OldWoodSeeds"] = "Grovekeepers", ["Rimfolk_Evt_AfterFire"] = "Grovekeepers", ["Rimfolk_Evt_BannerContract"] = "Mercenaries", ["Rimfolk_Evt_OldSoldier"] = "Mercenaries",
            ["Rimfolk_Evt_BannerMuster"] = "Mercenaries", ["Rimfolk_Evt_OtherSideContract"] = "Mercenaries", ["Rimfolk_Evt_CompanyMarch"] = "Mercenaries"
        };
        var fired = new Dictionary<string, string>
        {
            ["Rimfolk_Evt_BreakDrought"] = "PeopleEvents2.Fire(EventDef, map, f)", ["Rimfolk_Evt_TideReader"] = "PeopleEvents2.Fire(EventDef, r.map, f)",
            ["Rimfolk_Evt_CanopyFire"] = "PeopleEvents2.Fire(EventDef, map, f)", ["Rimfolk_Evt_AfterFire"] = "PeopleEvents2.Fire(EventDef, map, asked.faction)",
            ["Rimfolk_Evt_BannerMuster"] = "PeopleEvents2.Fire(MusterDef, p.Map, r.faction)", ["Rimfolk_Evt_OtherSideContract"] = "PeopleEvents2.Fire(EventDef, map, banner)"
        };
        // The two whose people are already at work when the letter comes: no answer means the first.
        var defaults = new Dictionary<string, string> { ["Rimfolk_Evt_TideReader"] = "0", ["Rimfolk_Evt_CanopyFire"] = "0" };
        var lastAnswers = new[] { "ColonyCouncil.DilemmaEffect_SendAway", "ColonyCouncil.DilemmaEffect_CanopyFire" };
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
            if (fired.TryGetValue(name, out var how)) C(chance == 0f && code.Contains("\"" + name + "\"") && code.Contains(how), $"{name}: never drawn, fired only when its moment comes");
            else
                C(chance >= 0.3f && chance <= 0.8f && int.Parse(e.Element("minRefireDays")!.Value) >= 30 && int.Parse(e.Element("earliestDay")!.Value) >= 10,
                    $"{name}: a small, spread chance, not soon again, not the first moment it is free");
            C(ext.Element("group")?.Value == "Visitors", $"{name}: among the visitors, so the storytellers' lean on visitors reads it");
            var texts = ext.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
            C(texts.Count >= 5 && texts.Any(t => Regex.IsMatch(t, @"^\[[^\]]*(lowtech|tribal|medieval|industrial|spacer|age\d)")), $"{name}: five or more texts, in the forms of the age");
            var options = ext.Element("options")!.Elements("li").ToList();
            C(options.Count == 3 && ext.Element("defaultOption")?.Value == (defaults.TryGetValue(name, out var d) ? d : "2"), $"{name}: three answers, no answer the last (or as its note says)");
            C(options[2].Descendants("li").Any(li => lastAnswers.Contains(li.Attribute("Class")?.Value)) || options[2].Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_OtherSideContract"),
                $"{name}: the last answer sends them on (or lets the Banner march, as the sergeant goes)");
            var lineTags = ext.Element("lines")?.Elements("li").Select(l => Regex.Match(l.Value, @"^\[([^\],]+)").Groups[1].Value).Distinct().ToList() ?? new List<string>();
            C(lineTags.Count > 0 && lineTags.All(t => code.Contains("\"" + t + "\"")), $"{name}: every line's moment is one the code says it at");
            C(ext.Element("lines")?.Elements("li").All(l => Regex.Matches(Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", ""), @"[.?](\s|$)").Count == 1) ?? true, $"{name}: every spoken line one sentence");
            var silver = options.SelectMany(o => o.Descendants("li")).Where(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_CarryOut" && li.Element("what")?.Value == "silver").ToList();
            C(silver.All(li => li.Element("steward")?.Value == "false"), $"{name}: the silver carried out is the sum the card says");
        }
        var tower = Ext(events["Rimfolk_Evt_StormTower"], "HappeningExtension")!;
        C(tower.Element("seasons")!.Elements("li").Select(l => l.Value).SequenceEqual(new[] { "Spring", "Summer" }) && code.Contains("StormTower.Stormy(map)"),
            "the storm tower only in the stormy months, on land that has storms");
        var contract = Ext(events["Rimfolk_Evt_BannerContract"], "HappeningExtension")!.Element("options")!.Elements("li").ToList();
        C(contract[1].Descendants("factor").Single().Value == "1.5" && contract[1].Descendants("tens").Single().Value == "true", "the company's share up front at half again, as the card says");
        var soldier = Ext(events["Rimfolk_Evt_OldSoldier"], "HappeningExtension")!;
        C(soldier.Element("addsPeople")?.Value == "true" && soldier.Element("options")!.Elements("li").ElementAt(1).Descendants("factor").Single().Value == "3",
            "the old soldier adds to the colony; the musters bought out at three years' fees");
        C(Ext(events["Rimfolk_Evt_OtherSideContract"], "HappeningExtension")!.Element("options")!.Elements("li").ElementAt(1).Descendants("variant").Single().Value == "true"
          && code.Contains("ctx.variant = PeopleEvents6Math.StandAsidePrice(r.points);"), "standing aside paid at its own price, the card's second number");
        C(soldier.Element("texts")!.Elements("li").Where(l => l.Value.Contains("{BEAST}")).All(l => Regex.IsMatch(l.Value, @"^\[[^\]]*horse")),
            "the old soldier's horse is named only where there is one");
        var after = Ext(events["Rimfolk_Evt_AfterFire"], "HappeningExtension")!;
        C(after.Element("texts")!.Elements("li").Where(l => l.Value.Contains("burned trees")).All(l => Regex.IsMatch(l.Value, @"^\[[^\]]*fire")), "burned trees are spoken of only after a fire");

        // The camps.
        var campKinds = XDocument.Load(Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKinds_PeopleEvents6.xml")).Root!.Elements("ColonyCouncil.CampKindDef").ToDictionary(Dn);
        C(campKinds.Keys.SequenceEqual(new[] { "Rimfolk_Camp_StormTower", "Rimfolk_Camp_AfterFire", "Rimfolk_Camp_CompanyMarch" }), "three camps of their own");
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
            C(k.Element("leaves")?.Value == "NoTrace", $"{name}: nothing of theirs left behind but what the code says");
        }
        var towerCamp = campKinds["Rimfolk_Camp_StormTower"];
        C(towerCamp.Element("calling")!.Value == "seers" && towerCamp.Element("people")!.Value == "3~3" && towerCamp.Element("days")!.Value == "7~7"
          && towerCamp.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).SequenceEqual(new[] { "watch", "tend" })
          && towerCamp.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_Lesson" && li.Element("skill")?.Value == "Plants"),
            "the tower: two readers and an apprentice a week; the night watch a lesson at the fields; the apprentice tended");
        var afterCamp = campKinds["Rimfolk_Camp_AfterFire"];
        C(afterCamp.Element("calling")!.Value == "grovekeepers" && afterCamp.Element("days")!.Value == "7~7"
          && afterCamp.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).SequenceEqual(new[] { "food", "plant" })
          && afterCamp.Element("asks")!.Elements("li").First().Element("theyAsk")?.Value == "true", "the camp after the fire: a week; they ask for food; the planter beside them");
        var march = campKinds["Rimfolk_Camp_CompanyMarch"];
        C(march.Element("calling")!.Value == "mercenaries" && march.Element("people")!.Value == "8~12" && march.Element("days")!.Value == "1~2" && march.Element("fights")?.Value == "true"
          && march.Element("buys")!.Elements("li").Select(l => l.Value).SequenceEqual(new[] { "Meal", "RoadFood", "RawFood" }) && F(march.Element("joinChance")!.Value) > 0f
          && march.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).SequenceEqual(new[] { "drill", "enlist" }),
            "the company: eight to twelve, a night or two, takes up arms, buys food, may let a soldier stay; drill and enlist");
        C(Read("Camps", "CampLife.cs").Contains("string key = \"CC_Camp_End_\" + (ending ?? \"empty\");") && File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeopleEvents6.xml")).Contains("<CC_Camp_End_felled>")
          && code.Contains("c.Leave(\"felled\")"), "a camp left for a tree felled in the planting says why");

        // Their jobs, the mast and the vial, the moods.
        var misc = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_PeopleEvents6_Misc.xml")).Root!.Elements().ToList();
        var jobs = misc.Where(e => e.Name.LocalName == "JobDef").ToList();
        C(jobs.Count == 11 && jobs.All(j => everyCode.Contains("class " + j.Element("driverClass")!.Value.Replace("ColonyCouncil.", "") + " : JobDriver") && code.Contains("\"" + Dn(j) + "\"")),
            "eleven jobs, each driven by a driver of the round or round 1's and given by the round's code");
        var work = jobs.Where(j => j.Element("driverClass")!.Value == "ColonyCouncil.JobDriver_PEv6Work").ToList();
        var workOn = jobs.Where(j => j.Element("driverClass")!.Value == "ColonyCouncil.JobDriver_PeopleWorkOn").ToList();
        C(work.All(j => new[] { "dust", "sparks", "sow", "glass", "drill", "chop", "quiet" }.Contains(j.Descendants("mark").Single().Value) && code.Contains("case \"" + j.Descendants("mark").Single().Value + "\"")
                        || j.Descendants("mark").Single().Value == "dust")
          && workOn.Count == 2 && workOn.All(j => Regex.IsMatch(everyCode, @"class " + j.Descendants("done").Single().Value.Replace("ColonyCouncil.", "") + @" : PeopleWorkDone")),
            "the round's own work by its marks; the reader's hour and the plot sown are round 1's work on someone");
        C(code.Contains("pawn.natives?.TryBeatFire(fire)") && code.Contains("class JobDriver_PEv6Firefight : JobDriver"), "the fire beaten out by the game's own beating");
        var mast = misc.Single(e => Dn(e) == "Rimfolk_StormglassMast");
        var vial = misc.Single(e => Dn(e) == "Rimfolk_StormVial");
        C(mast.Element("graphicData")!.Element("texPath")!.Value == "ColonyCouncil/Things/StormglassMast" && vial.Element("graphicData")!.Element("texPath")!.Value == "ColonyCouncil/Things/StormVial"
          && File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Things", "StormglassMast.png")) && File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Things", "StormVial.png"))
          && File.Exists(Path.Combine(root, "Tools", "make_storm_glass.py")) && mast.Element("designationCategory") == null, "the mast and the vial drawn; the mast never built by hand");
        var thoughts = misc.Where(e => e.Name.LocalName == "ThoughtDef").ToDictionary(Dn);
        C(thoughts.Keys.SequenceEqual(new[] { "Rimfolk_Thought_SatThroughDrone", "Rimfolk_Thought_Mustered" }) && thoughts.Values.All(t => F(t.Descendants("baseMoodEffect").Single().Value) > 0f)
          && code.Contains("\"Rimfolk_Thought_SatThroughDrone\"") && code.Contains("\"Rimfolk_Thought_Mustered\""), "a calm hour through the drone, a week at the muster: good memories only");

        // The player's mods, drawn on softly and from their own catalogue.
        var cat = Catalogue(root);
        var active = cat.Where(r => r.active).Select(r => r.type + ":" + r.def).ToHashSet();
        var activeMods = cat.Where(r => r.active).Select(r => r.mod.ToLowerInvariant()).ToHashSet();
        C(PeopleEvents6Math.DryConditions.All(c => active.Contains("GameConditionDef:" + c)) && PeopleEvents6Math.TideIncidents.All(i => active.Contains("IncidentDef:" + i)),
            "the dry spells and the tides are the game's, Odyssey's and Vanilla Events Expanded's");
        C(Enumerable.Range(0, 4).SelectMany(PeopleEvents6Math.Mounts).All(m => active.Contains("PawnKindDef:" + m)), "the war-horses are in the player's list (Vanilla Genetics Expanded's Paragon horse)");
        C(PeopleEvents6Math.SeedPackages.All(p => activeMods.Contains(p)) && PeopleEvents6Math.SeedPackages.All(p => cat.Any(r => r.active && r.type == "ThingDef" && r.mod.Equals(p, StringComparison.OrdinalIgnoreCase))),
            "the seeds' mods are in the player's list, each with plants of its own");
        C(active.Contains("ThingDef:BurnedTree") && active.Contains("HediffDef:Flu") && active.Contains("ThingCategoryDef:PlantFoodRaw") && active.Contains("IncidentDef:Wildfire")
          && code.Contains("\"BurnedTree\"") && code.Contains("\"Flu\"") && code.Contains("\"PlantFoodRaw\""), "the game's burned trees, flu and raw plant food; Seasonal Weather's wildfire is one of the fires they come to");
        C(code.Contains("PeoplesD.KindNamed(ctx.faction, \"Rimfolk_Stormglass_Walker\")") && code.Contains("\"Rimfolk_Stormglass_Reader\"") && code.Contains("\"Rimfolk_Stormglass_Apprentice\"")
          && code.Contains("\"Rimfolk_Mossborn_Thorn\"") && code.Contains("\"Rimfolk_Mossborn_HerbTrader\"") && code.Contains("\"Rimfolk_Mossborn_Tender\"")
          && code.Contains("\"Rimfolk_AshenBanner_Captain\"") && code.Contains("\"Rimfolk_AshenBanner_Sergeant\"") && code.Contains("\"Rimfolk_AshenBanner_Soldier\"") && code.Contains("\"Rimfolk_AshenBanner_Shot\""),
            "each people's own kind of person comes");
        var kinds = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Defs", "PeopleDefs"), "Rimfolk_People_*.xml").Select(File.ReadAllText));
        C(Regex.Matches(code, "\"(Rimfolk_(Stormglass|Mossborn|AshenBanner)_\\w+)\"").Select(mm => mm.Groups[1].Value).Distinct().All(k => kinds.Contains("<defName>" + k + "</defName>")),
            "every kind of person named is one of the people's own");

        // ================= Their words =================

        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeopleEvents6.xml")).Root!.Elements().Select(e => e.Name.LocalName).ToHashSet();
        var used = Regex.Matches(code, "\"(CC_PEv6_\\w+)\"").Select(mm => mm.Groups[1].Value).Distinct().ToList();
        C(used.Count >= 100 && used.All(keyed.Contains), $"every word the code says is keyed ({used.Count}{(used.Any(u => !keyed.Contains(u)) ? ", missing " + string.Join(", ", used.Where(u => !keyed.Contains(u))) : "")})");
        C(keyed.Count(k => k.StartsWith("CC_PEv6_BetBack")) >= 2 && code.Contains("TextVariants.Vary(\"CC_PEv6_BetBack\")"), "the colonist's side of the bet in more than one wording");
        var texts6 = new[] { Path.Combine("Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents6.xml"), Path.Combine("Defs", "CampDefs", "Rimfolk_CampKinds_PeopleEvents6.xml"),
                Path.Combine("Languages", "English", "Keyed", "Rimfolk_PeopleEvents6.xml"), Path.Combine("Defs", "Misc", "Rimfolk_PeopleEvents6_Misc.xml") }
            .Select(x => Regex.Replace(File.ReadAllText(Path.Combine(root, x)), "<!--.*?-->|<\\?xml[^>]*>|\\[[^\\]]*\\]", "", RegexOptions.Singleline)).Aggregate((x, y) => x + y);
        C(!texts6.Contains('—') && !texts6.Contains('!') && !Regex.IsMatch(texts6, @"\bin \d+ (days?|hours?)\b"), "plain words, no countdowns");
        C(!Regex.IsMatch(texts6.ToLowerInvariant(), @"\b(dynasty|dynasties|bloodline|heir to the house)\b"), "the colony is the family: no houses");
        C(!Regex.IsMatch(texts6, @"\b(she|her|hers)\b"), "nobody of theirs is given a sex the game didn't");
        C(!Regex.IsMatch(texts6, @"\b(god|gods|goddess)\b", RegexOptions.IgnoreCase), "the gods unseen: no god named");
        string peoples = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Defs", "AlmanacArticleDefs"), "Almanac_Peoples*.xml").Select(File.ReadAllText)).ToLowerInvariant();
        C(peoples.Contains("breaking the drought") && peoples.Contains("the tide read") && peoples.Contains("the storm tower") && peoples.Contains("fire in the canopy")
          && peoples.Contains("seeds of the old wood") && peoples.Contains("after the fire") && peoples.Contains("a contract on the colony's enemy") && peoples.Contains("an old soldier settles")
          && peoples.Contains("the other side's contract") && peoples.Contains("a company on the march"), "each people's Almanac page says what it does now");

        // ================= The seams =================

        string five = Read("Peoples", "Events5", "PeopleEvents5.cs");
        C(five.Contains("return PeopleEvents6.Hour(v, comp, now);") && five.Contains("PeopleEvents6.Hourly(now)"), "round 6's visits and watch wait on round 1's hour, through rounds 2 to 5");
        var visitKinds = Regex.Matches(code, @"public const string (\w*Kind) = ""([^""]+)"";").Select(mm => mm.Groups[2].Value).ToList();
        C(visitKinds.Count == 3 && visitKinds.All(k => k.StartsWith("pev:")) && visitKinds.Distinct().Count() == 3 && Read("Happenings", "Happenings.cs").Contains("case var k when PeopleEvents.Owns(k)"),
            "their visits are the events' own kind (\"pev:\"), none twice");
        string earlier = string.Join("\n", new[] { "Events", "Events2", "Events3", "Events4", "Events5" }.SelectMany(x => Directory.GetFiles(Path.Combine(src, "Peoples", x), "*.cs")).Select(File.ReadAllText));
        C(visitKinds.All(k => !earlier.Contains("\"" + k + "\"")), "none of their visits' kinds is an earlier round's");
        string season = Read("Peoples", "Events2", "SeasonAway.cs");
        C(season.Contains("[OldSoldier.AwayKey] = new MusterAway()") && season.Contains("[CompanyMarch.AwayKey] = new EnlistAway()") && code.Contains("public const string AwayKey = \"muster\";")
          && code.Contains("public const string AwayKey = \"enlist\";") && code.Contains("SeasonAway.Send(OldSoldier.AwayKey") && code.Contains("SeasonAway.Send(CompanyMarch.AwayKey"),
            "the muster and the season enlisted are round 2's season away");
        C(Read("Peoples", "PeoplesE.cs").Contains("AfterFire.Felled(plant, by)"), "a tree the colony fells is weighed against the Mossborn's planting");
        C(Read("Text", "Facts", "FactsMath.cs").Contains("\"them:camp:StormTower\", \"them:camp:AfterFire\", \"them:camp:CompanyMarch\""), "the facts service knows the round's camps");
        C(Read("..", "..", "Tools", "DefValidator", "Program.cs").Contains("\"STORMS\", \"PLANTED\""), "the def check knows the camps' own counts");
        C(code.Contains("PeopleWorkOn.Begin(") && code.Contains("GlassWatch.Of(map)") && code.Contains("GlassWatch.Begin(") && code.Contains("RelayMast.Site(map)") && code.Contains("PeoplesE.Sapling(")
          && code.Contains("PeoplesE.SaplingCell(") && code.Contains("CampRaids.Send(") && code.Contains("Outsiders.QuietBrawl(") && code.Contains("new LordJob_AssaultThings(")
          && code.Contains("new LordJob_AssistColony(") && code.Contains("Couriers.PaySilver(") && code.Contains("Arrivals.Join(") && code.Contains("LeagueGuard.Fighters(")
          && code.Contains("HappeningWorker_GlassLesson.Taught"),
            "the shapes reused: work on someone, the Conclave's watch and its lesson, the high ground of the relay mast, the Mossborn's saplings, a band against a camp, the outsiders' brawl, the game's assault and its allies, the post, joining, the league's fighters");
        var harms = Regex.Matches(code, @"Harm\.(Roll|Ail|Injure|Fight)\(([^,;]*,\s*){0,2}HarmKinds\.(\w+)").Select(mm => mm.Groups[3].Value).Distinct().OrderBy(x => x).ToList();
        C(harms.SequenceEqual(new[] { "CampBrawl", "CampFever", "CampLeft" }) && !Regex.IsMatch(code, @"TakeDamage") && code.Contains("IncidentWorker_Happening.BattlesAllowed(c.map)")
          && Regex.Matches(code, @"HarmBook\.Instance\?\.Came\(HarmKinds\.CampLeft").Count == 2,
            "no harm kind of their own: the camps' brawl, fever and embers within the shared budget; the band only while Rimfolk's world events may bring a fight");
        var patches = Regex.Matches(code, @"\[HarmonyPatch\(typeof\((\w+)\), (nameof\(\w+\.(\w+)\)|""(\w+)"")\)\]").Select(mm => mm.Groups[1].Value + "." + (mm.Groups[3].Value + mm.Groups[4].Value)).ToList();
        C(patches.SequenceEqual(new[] { "Storyteller.TryFire", "Storyteller.TryFire", "IncidentWorker_PsychicDrone.DoConditionAndLetter", "IncidentWorker_PsychicSoothe.DoConditionAndLetter" })
          && Regex.Matches(code, @"if \(!__runOriginal\) return false;").Count == 2 && code.Contains("[HarmonyPriority(Priority.Low)]") && Regex.Matches(code, @"!RimfolkPatches\.On").Count >= 4,
            "four game hooks, each off with the deeper game hooks; the two on the storyteller leave a firing another hold took");
        C(!code.Contains("MoteMaker.ThrowText") && code.Contains("PeoplesG.SayLine(") && code.Contains("c.SayMoment(") && code.Contains("PeoplesG.Say(") && code.Contains("Speech.Say("),
            "they speak through the events' and the camps' own lines");
        C(!Regex.IsMatch(code, @"Sched\.|\bLocomotionUrgency\.Sprint"), "no clock of their own: the events' hour and the camps' hour");
        C(Regex.Matches(code, @"StoryDirection\.Factor\(StoryWaysMath\.Visitors\)").Count >= 3 && code.Contains("StoryDirection.Factor(StoryWaysMath.Arms)"),
            "the code-fired comings read the storytellers' lean on visitors, the hired raid their lean on arms");
        string book = File.ReadAllText(Path.Combine(dir, "PeopleEvents6Book.cs"));
        C(book.Contains("Note(rainRolled, map.uniqueID") && book.Contains("tides.Count > 4") && book.Contains("afters.Count > 4") && book.Contains("contracts.Count > 4") && book.Contains("hires.Count > 4")
          && book.Contains("PeopleEvents6.MaxVeterans") && book.Contains("while (list.Count > max)"), "what they keep is bounded");
        C(Regex.Matches(code, @"\[DebugAction\(""Rimfolk"", ""Peoples' events 6: ").Count == 10, "ten dev actions");
        string save = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(save.Contains("Peoples/Events6/PeopleEvents6Book.cs:contracts") && save.Contains("Peoples/Events6/PeopleEvents6Book.cs:veterans") && save.Contains("Peoples/Events6/AfterFire.cs:saplings")
          && save.Contains("Peoples/Events6/StormTower.cs:rods"), "their saved lists are in the save model");
        string audit = File.ReadAllText(Path.Combine(root, "Tools", "playout_audit.py"));
        C(events.Values.Concat(campKinds.Values).SelectMany(e => e.Descendants("li")).Select(li => li.Attribute("Class")?.Value?.Replace("ColonyCouncil.", ""))
                .Where(v => v != null && v.StartsWith("DilemmaEffect_") && Regex.IsMatch(code, @"class " + v + @"\b") && v != "DilemmaEffect_AfterFed").Distinct().All(v => audit.Contains("\"" + v + "\"")),
            "the play-out audit knows each answer of theirs that plays out on the map");
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
