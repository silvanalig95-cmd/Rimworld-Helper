using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The peoples' own events, round 7 (Peoples/Events7/; docs/peoples-events-proposals.md, items 60, 61, 62, 64, 65, 66, 67, 68, 69
/// and 70): their pure rules (the master's verdict and its chance, the journeyman's year, the guild's work and its stone, the
/// Line's call and its traders, the mast at a friend's town, the line crew's week, the cave-in's shifts and pay, the well and its
/// share, the worn down and their petition, the picket), their defs and words, the player's mods they draw on, and the seams with
/// the game and rounds 1 to 6.
/// </summary>
public static class PeopleEvents7Tests
{
    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the peoples' events, round 7: " + what);
        }
        float F(string v) => float.Parse(v, CultureInfo.InvariantCulture);

        // ================= 60. The master's mark =================

        C(PeopleEvents7Math.Judged(5) && PeopleEvents7Math.Judged(6) && !PeopleEvents7Math.Judged(4) && !PeopleEvents7Math.Judged(0), "only a masterwork or a legend is judged");
        C(Math.Abs(PeopleEvents7Math.FaultChance(5) - 0.2f) < 1e-6f && Math.Abs(PeopleEvents7Math.FaultChance(6) - 1f / 12f) < 1e-6f && PeopleEvents7Math.FaultChance(4) == 0f,
            "a fault in one masterwork in five, one legend in twelve");
        C(PeopleEvents7Math.Fault(5, 0.1f) && !PeopleEvents7Math.Fault(5, 0.3f) && !PeopleEvents7Math.Fault(6, 0.1f) && !PeopleEvents7Math.Fault(4, 0f) && !PeopleEvents7Math.Fault(5, float.NaN),
            "the verdict by its roll");
        C(PeopleEvents7Math.MarkKeepDays >= 5 && PeopleEvents7Math.MarkGapDays >= 15 && PeopleEvents7Math.MarkChance <= 0.6f && PeopleEvents7Math.JudgeTicks == 2500
          && PeopleEvents7Math.MarkGoodwill > 0 && PeopleEvents7Math.JudgeXp > 0f, "a master comes while the piece is fresh, not soon again; an hour at it");

        // ================= 61. Come back better =================

        C(PeopleEvents7Math.ComeBackDays == 60 && PeopleEvents7Math.MasterSkill >= 12 && PeopleEvents7Math.ComeBackForgetDays > PeopleEvents7Math.ComeBackDays
          && PeopleEvents7Math.ComeBackRetryDays >= 10 && PeopleEvents7Math.MaxComeBacks >= 2, "a year on, a master's hand; kept within reason");

        // ================= 62. A work that stays =================

        C(PeopleEvents7Math.Works.SequenceEqual(new[] { "bridge", "sculpture", "fountain" }) && PeopleEvents7Math.Journeymen == 3 && PeopleEvents7Math.WorkMinDays == 5
          && PeopleEvents7Math.WorkMaxDays == 8, "three journeymen, five to eight days, a bridge first, then a sculpture, then a fountain");
        C(PeopleEvents7Math.Material("bridge", 10, 12, 0) == 120 && PeopleEvents7Math.Material("sculpture", 0, 0, 400) == 400 && PeopleEvents7Math.Material("fountain", 0, 0, 10) == 50
          && PeopleEvents7Math.Material("bridge", 0, 0, 0) == 50, "the stone the work takes, by its cells or its own cost, never under fifty");
        C(PeopleEvents7Math.WorkDays(7) == 5 && PeopleEvents7Math.WorkDays(5) == 4 && PeopleEvents7Math.WorkDays(8) == 6 && PeopleEvents7Math.WorkDays(1) == 3 && PeopleEvents7Math.WorkDays(-3) == 3,
            "the days of work: two thirds of their stay, three at least");
        C(PeopleEvents7Math.MoreNeeded(400) == 100 && PeopleEvents7Math.MoreNeeded(20) == 10 && PeopleEvents7Math.MoreNeeded(-1) == 10, "a cracked block: a quarter more, ten at least");
        C(PeopleEvents7Math.WorkQuality(false) == 4 && PeopleEvents7Math.WorkQuality(true) == 3, "excellent, a grade plainer when they made do");
        C(PeopleEvents7Math.ShortChance <= 0.3f && PeopleEvents7Math.TornChance <= 0.2f && PeopleEvents7Math.WorkHour(8) && PeopleEvents7Math.WorkHour(17) && !PeopleEvents7Math.WorkHour(18)
          && !PeopleEvents7Math.WorkHour(7), "it turns rarely; they work by day");
        C(PeopleEvents7Math.BridgeMin >= 2 && PeopleEvents7Math.BridgeMax <= 14 && PeopleEvents7Math.BridgeWidth == 2, "a crossing two to twelve cells of water, two wide");

        // ================= 64. A voice for the colony =================

        C(PeopleEvents7Math.TradeKind("Caravan_Outlander_BulkGoods") == "goods" && PeopleEvents7Math.TradeKind("Caravan_Neolithic_BulkGoods") == "goods"
          && PeopleEvents7Math.TradeKind("Empire_Caravan_TraderGeneral") == "goods" && PeopleEvents7Math.TradeKind("Rimfolk_Caravan_Herders_Wool") == "goods",
            "traders of goods by their kind's name");
        C(PeopleEvents7Math.TradeKind("Caravan_Outlander_CombatSupplier") == "arms" && PeopleEvents7Math.TradeKind("Caravan_Neolithic_WarMerchant") == "arms"
          && PeopleEvents7Math.TradeKind("VFEC_Caravan_Republic_WarMerchant") == "arms", "traders of arms by their kind's name");
        C(PeopleEvents7Math.TradeKind("Caravan_Neolithic_Slaver") == null && PeopleEvents7Math.TradeKind("Caravan_Outlander_PirateMerchant") == null
          && PeopleEvents7Math.TradeKind("Empire_Caravan_TributeCollector") == null && PeopleEvents7Math.TradeKind("Orbital_BulkGoods") == null
          && PeopleEvents7Math.TradeKind("Rimfolk_Visitor_League") == null && PeopleEvents7Math.TradeKind(null) == null, "never slavers, pirates, tribute collectors, ships or visitors");
        C(PeopleEvents7Math.CallFee == PeoplesFMath.NewsFee && PeopleEvents7Math.HandsFee == 3 * PeopleEvents7Math.CallFee && PeopleEvents7Math.GoodWord > 0,
            "the Line's price a call (its price for news), three times it for hands");
        C(PeopleEvents7Math.CallHours(true, 0f) == 3 && PeopleEvents7Math.CallHours(true, 0.999f) == 6 && PeopleEvents7Math.CallHours(false, 0f) == 24
          && PeopleEvents7Math.CallHours(false, 0.999f) == 48 && PeopleEvents7Math.CallHours(false, float.NaN) == 24, "by radio within hours, by runner in a day or two");

        // ================= 65. A mast at a friend's town =================

        C(PeopleEvents7Math.MastFee(0f) == 200 && PeopleEvents7Math.MastFee(10f) == 250 && PeopleEvents7Math.MastFee(100f) == 450 && PeopleEvents7Math.MastFee(float.NaN) == 200,
            "the mast's fee by the road there, within reason");
        C(PeopleEvents7Math.MastComponents(250, 32f) == 8 && PeopleEvents7Math.MastComponents(250, 0f) == 0 && PeopleEvents7Math.MastComponents(10, 100f) == 2,
            "paid in components at their worth");
        C(PeopleEvents7Math.MastDays(2f) == 3f && PeopleEvents7Math.MastDays(float.NaN) == 1.5f && PeopleEvents7Math.MastDays(99f) == 31f, "it stands once the lineman has walked there and raised it");

        // ================= 66. The line crew =================

        C(PeopleEvents7Math.CrewMin == 3 && PeopleEvents7Math.CrewMax == 4 && PeopleEvents7Math.CrewDays == 7 && PeopleEvents7Math.RadioHour(20) && !PeopleEvents7Math.RadioHour(22)
          && PeopleEvents7Math.WireHour(8) && !PeopleEvents7Math.WireHour(17), "three or four linesmen a week; the wire by day, the radio in the evening");
        C(PeopleEvents7Math.KeepFee(1f) == 300 && PeopleEvents7Math.KeepFee(0.1f) == 200 && PeopleEvents7Math.KeepFee(5f) == 600 && PeopleEvents7Math.KeepFee(float.NaN) == 300,
            "the mast kept for a fee by the colony's wealth");
        C(PeopleEvents7Math.FallChance <= 0.15f && PeopleEvents7Math.MastStruckChance <= 0.25f && PeopleEvents7Math.FallLow < PeopleEvents7Math.FallHigh && PeopleEvents7Math.FallHigh <= 12f,
            "it turns rarely, a fall's wound within reason");

        // ================= 67. The pit's cave-in =================

        C(PeopleEvents7Math.CaveInDays(0f) == 4 && PeopleEvents7Math.CaveInDays(0.999f) == 6 && PeopleEvents7Math.CaveInDays(float.NaN) == 4 && PeopleEvents7Math.CaveInDays(5f) == 6,
            "four to six days at the pit");
        C(PeopleEvents7Math.CrushChance == PeoplesFMath.AccidentChance && PeopleEvents7Math.CrushHigh <= 10f, "one shift in twenty a crush, the drill rig's own chance");
        C(PeopleEvents7Math.CaveInPay(5) == 200 && PeopleEvents7Math.CaveInPay(-2) == 0 && PeopleEvents7Math.CaveInGoodwill == 12 && PeopleEvents7Math.CaveInAloneGoodwill * 2 == PeopleEvents7Math.CaveInGoodwill,
            "the union's pay, and twelve for both sent");
        C(PeopleEvents7Math.CaveInTiles >= 8f && PeopleEvents7Math.ShiftXp > 0f, "a pit within reach; a shift's lesson");

        // ================= 68. Oil in the ground =================

        C(PeopleEvents7Math.Wells.SequenceEqual(new[] { "OilWell", "DeepOilWell" }) && PeopleEvents7Math.WellPrice(1f) == 450 && PeopleEvents7Math.WellPrice(0.1f) == 300
          && PeopleEvents7Math.WellPrice(5f) == 900, "Rimefeller's wells, the price by the colony's wealth");
        C(PeopleEvents7Math.ShareSeasons * PeopleEvents7Math.ShareSeasonDays == 60 && PeopleEvents7Math.ShareChemfuel > 0 && PeopleEvents7Math.SeamFee > 0 && PeopleEvents7Math.SinkTicks == 15000,
            "a year's share in four seasons; six hours to sink the well");

        // ================= 69. The organizer's evening =================

        C(PeopleEvents7Math.Worn(40f, false) && PeopleEvents7Math.Worn(0f, true) && !PeopleEvents7Math.Worn(39f, false) && !PeopleEvents7Math.Worn(float.NaN, false),
            "worn down: the heart's strain, or the long hours");
        C(PeopleEvents7Math.Dusk(16) && PeopleEvents7Math.Dusk(18) && !PeopleEvents7Math.Dusk(19) && PeopleEvents7Math.Morning(7) && PeopleEvents7Math.Morning(10) && !PeopleEvents7Math.Morning(11),
            "at dusk the captain, in the morning the petition");
        C(PeopleEvents7Math.PetitionDays > 5 && PeopleEvents7Math.EveningChance <= 0.4f && PeopleEvents7Math.EveningGapDays >= 20 && PeopleEvents7Math.StrainedOne < PeopleEvents7Math.WornStrain,
            "the petition watched past the council's five days; not often");

        // ================= 70. The picket =================

        C(PeopleEvents7Math.PicketChance <= 0.5f && PeopleEvents7Math.PicketMaxDays == PeoplesFMath.StrikeDays && PeopleEvents7Math.PicketMeals > 0 && PeopleEvents7Math.Sing(20)
          && !PeopleEvents7Math.Sing(23) && PeopleEvents7Math.BreakersChance <= 0.15f, "a picket for the strike's length; songs at night; strikebreakers rarely");

        // ================= Their defs =================

        var events = XDocument.Load(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents7.xml")).Root!.Elements("IncidentDef").ToDictionary(Dn);
        C(events.Keys.SequenceEqual(new[] { "Rimfolk_Evt_MastersMark", "Rimfolk_Evt_ComeBackBetter", "Rimfolk_Evt_WorkThatStays", "Rimfolk_Evt_VoiceForColony", "Rimfolk_Evt_TownMast",
            "Rimfolk_Evt_LineCrew", "Rimfolk_Evt_PitCaveIn", "Rimfolk_Evt_OilInGround", "Rimfolk_Evt_DeepSeam", "Rimfolk_Evt_OrganizersEvening", "Rimfolk_Evt_Picket" }),
            "eleven events: seven drawn (the oil in two forms), four fired when their moment comes");
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { src }.Concat(parts).ToArray()));
        string dir = Path.Combine(src, "Peoples", "Events7");
        string code = string.Join("\n", Directory.GetFiles(dir, "*.cs").Where(f => !f.EndsWith("PeopleEvents7Math.cs")).OrderBy(f => f).Select(File.ReadAllText));
        string everyCode = string.Join("\n", Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        var callings = new Dictionary<string, string>
        {
            ["Rimfolk_Evt_MastersMark"] = "Artisans", ["Rimfolk_Evt_ComeBackBetter"] = "Artisans", ["Rimfolk_Evt_WorkThatStays"] = "Artisans", ["Rimfolk_Evt_VoiceForColony"] = "Linesmen",
            ["Rimfolk_Evt_TownMast"] = "Linesmen", ["Rimfolk_Evt_LineCrew"] = "Linesmen", ["Rimfolk_Evt_PitCaveIn"] = "Drillers", ["Rimfolk_Evt_OilInGround"] = "Drillers",
            ["Rimfolk_Evt_DeepSeam"] = "Drillers", ["Rimfolk_Evt_OrganizersEvening"] = "Drillers", ["Rimfolk_Evt_Picket"] = "Drillers"
        };
        var fired = new Dictionary<string, string>
        {
            ["Rimfolk_Evt_MastersMark"] = "PeopleEvents2.Fire(EventDef, r.map, f)", ["Rimfolk_Evt_ComeBackBetter"] = "PeopleEvents2.Fire(EventDef, r.maker.Map, r.faction)",
            ["Rimfolk_Evt_OrganizersEvening"] = "PeopleEvents2.Fire(EventDef, map, f)", ["Rimfolk_Evt_Picket"] = "PeopleEvents2.Fire(EventDef, home, f)"
        };
        foreach (var (name, e) in events)
        {
            string worker = e.Element("workerClass")!.Value.Replace("ColonyCouncil.", "");
            var m = Regex.Match(code, @"class " + worker + @" : HappeningWorker_Visitors\s*\{(.*?)\n    \}", RegexOptions.Singleline);
            C(m.Success, $"{name}: its worker is the round's own");
            C(m.Success && m.Groups[1].Value.Contains("PeoplesMath." + callings[name]) && (m.Groups[1].Value.Contains("PeoplesD.Friendly(") || m.Groups[1].Value.Contains("PeoplesD.Theirs(")),
                $"{name}: only from a people of its calling at peace with the colony");
            var effects = e.Descendants("li").Select(li => li.Attribute("Class")?.Value).Where(v => v != null && v.StartsWith("ColonyCouncil.DilemmaEffect_")).Select(v => v.Replace("ColonyCouncil.", "")).Distinct();
            C(effects.All(x => Regex.IsMatch(everyCode, @"class " + x + @"\b")), $"{name}: every answer's effect is real");
            var ext = Ext(e, "HappeningExtension")!;
            float chance = F(e.Element("baseChance")!.Value);
            if (fired.TryGetValue(name, out var how)) C(chance == 0f && code.Contains(how), $"{name}: never drawn, fired only when its moment comes");
            else
                C(chance >= 0.3f && chance <= 0.6f && int.Parse(e.Element("minRefireDays")!.Value) >= (name == "Rimfolk_Evt_VoiceForColony" ? 15 : 40) && int.Parse(e.Element("earliestDay")!.Value) >= 10,
                    $"{name}: a small, spread chance, not soon again (the Line's call once a season), not the first moment it is free");
            C(ext.Element("group")?.Value == "Visitors", $"{name}: among the visitors, so the storytellers' lean on visitors reads it");
            var texts = ext.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
            C(texts.Count >= 5 && Enumerable.Range(0, 4).Count(a => texts.Any(t => t.StartsWith("[age" + a + "]"))) >= 4, $"{name}: five or more texts, one for each age");
            var options = ext.Element("options")!.Elements("li").ToList();
            int count = name == "Rimfolk_Evt_VoiceForColony" ? 5 : 3;
            C(options.Count == count && ext.Element("defaultOption")?.Value == (count - 1).ToString(), $"{name}: {count} answers, no answer the last");
            C(options.Last().Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_SendAway"), $"{name}: the last answer sends them on");
            var lineTags = ext.Element("lines")?.Elements("li").Select(l => Regex.Match(l.Value, @"^\[([^\],]+)").Groups[1].Value).Distinct().ToList() ?? new List<string>();
            C(lineTags.Count > 0 && lineTags.All(t => code.Contains("\"" + t + "\"")), $"{name}: every line's moment is one the code says it at");
            C(ext.Element("lines")?.Elements("li").All(l => Regex.Matches(Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", ""), @"[.?](\s|$)").Count == 1) ?? true, $"{name}: every spoken line one sentence");
            var silver = options.SelectMany(o => o.Descendants("li")).Where(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_CarryOut" && li.Element("what")?.Value == "silver").ToList();
            C(silver.All(li => li.Element("steward")?.Value == "false"), $"{name}: the silver carried out is the sum the card says");
            var gatherLines = e.Descendants("li").Where(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_Gathering").SelectMany(g => g.Element("lines")?.Elements("li") ?? Enumerable.Empty<XElement>());
            C(gatherLines.All(l => Regex.Matches(l.Value, @"[.?](\s|$)").Count == 1), $"{name}: the gathering's lines one sentence each");
        }
        var voice = Ext(events["Rimfolk_Evt_VoiceForColony"], "HappeningExtension")!.Element("options")!.Elements("li").ToList();
        C(voice.Take(4).Select(o => o.Descendants("call").Single().Value).SequenceEqual(new[] { "goods", "arms", "hands", "word" }) && voice[2].Descendants("factor").Single().Value == "3",
            "the Line's call: goods, arms, hands at three times the price, or a good word");
        C(Ext(events["Rimfolk_Evt_TownMast"], "HappeningExtension")!.Element("options")!.Elements("li").ElementAt(1).Descendants("variant").Single().Value == "true"
          && code.Contains("ctx.variant = PeopleEvents7Math.MastComponents("), "the mast paid in components at their number, the card's second number");
        var pit = Ext(events["Rimfolk_Evt_PitCaveIn"], "HappeningExtension")!;
        C(pit.Element("texts")!.Elements("li").Where(l => l.Value.Contains("{B_")).All(l => l.Value.StartsWith("[doctor]")), "the doctor is named only where there is one");
        var evening = Ext(events["Rimfolk_Evt_OrganizersEvening"], "HappeningExtension")!.Element("options")!.Elements("li").ToList();
        C(evening.Take(2).All(o => o.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_Gathering")
                                   && o.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_OrganizerSpoke")),
            "the shift-captain speaks at a gathering at the fire, and the petition follows");
        C(code.Contains("t.Petition(def, asker)") && code.Contains("repeal = true") && code.Contains("\"Council_Decree_Rest\"") && code.Contains("\"Council_Decree_LongHours\"")
          && code.Contains("t.CannotEnact(rest, true) == null"), "the petition through the council's own: the days of rest, or the long hours lifted, only what it could grant");

        // The camps.
        var campKinds = XDocument.Load(Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKinds_PeopleEvents7.xml")).Root!.Elements("ColonyCouncil.CampKindDef").ToDictionary(Dn);
        C(campKinds.Keys.SequenceEqual(new[] { "Rimfolk_Camp_WorkThatStays", "Rimfolk_Camp_LineCrew", "Rimfolk_Camp_Picket" }), "three camps of their own");
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
        var work = campKinds["Rimfolk_Camp_WorkThatStays"];
        C(work.Element("calling")!.Value == "artisans" && work.Element("people")!.Value == "3~3" && work.Element("days")!.Value == "5~8"
          && work.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).SequenceEqual(new[] { "beside" }), "the guild's work: three journeymen, five to eight days; the colony's best beside them");
        var crew = campKinds["Rimfolk_Camp_LineCrew"];
        C(crew.Element("calling")!.Value == "linesmen" && crew.Element("people")!.Value == "3~4" && crew.Element("days")!.Value == "7~7"
          && crew.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).SequenceEqual(new[] { "food", "parts", "keep", "tend" })
          && crew.Element("asks")!.Elements("li").Single(a => a.Element("key")!.Value == "keep").Element("silver") != null,
            "the line crew: a week; they ask for food and components; the mast kept for a fee; the fallen tended");
        var picket = campKinds["Rimfolk_Camp_Picket"];
        C(picket.Element("calling")!.Value == "drillers" && picket.Element("fights")?.Value == "true"
          && picket.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).SequenceEqual(new[] { "food", "evening", "tend" })
          && picket.Element("asks")!.Elements("li").First().Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_PicketFed")
          && picket.Element("asks")!.Elements("li").ElementAt(1).Element("skill")?.Value == "Mining",
            "the picket: they take up arms; food for the line settles the strike; the colony's miner at their fire");
        C(code.Contains("c.Leave(\"time\")") && code.Contains("c.Leave(\"nostone\")") && File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeopleEvents7.xml")).Contains("<CC_Camp_End_nostone>"),
            "the picket goes with its own farewell when the strike ends; the journeymen say why when no stone came");

        // Their jobs, the tower, the moods.
        var misc = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_PeopleEvents7_Misc.xml")).Root!.Elements().ToList();
        var jobs = misc.Where(e => e.Name.LocalName == "JobDef").ToList();
        C(jobs.Count == 10 && jobs.All(j => j.Element("driverClass")!.Value == "ColonyCouncil.JobDriver_PEv7Work" && code.Contains("\"" + Dn(j) + "\"")
                                            && new[] { "dust", "chisel", "sparks", "wire", "drill", "look", "quiet" }.Contains(j.Descendants("mark").Single().Value)),
            "ten jobs, each the round's own work at a place, given by its code, each with its mark");
        var tower = misc.Single(e => Dn(e) == "Rimfolk_SignalTower");
        C(tower.Element("graphicData")!.Element("texPath")!.Value == "ColonyCouncil/Things/SignalTower" && File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Things", "SignalTower.png"))
          && File.Exists(Path.Combine(root, "Tools", "make_signal_tower.py")) && tower.Element("designationCategory") == null && code.Contains("\"Rimfolk_SignalTower\""),
            "the flag-and-lamp tower drawn; never built by hand");
        var thoughts = misc.Where(e => e.Name.LocalName == "ThoughtDef").ToDictionary(Dn);
        C(thoughts.Keys.SequenceEqual(new[] { "Rimfolk_Thought_MarkTrue", "Rimfolk_Thought_MarkFault", "Rimfolk_Thought_ApprenticeMaster", "Rimfolk_Thought_DugThemOut",
              "Rimfolk_Thought_PetitionHeard", "Rimfolk_Thought_PetitionTurnedAway" }) && thoughts.Keys.All(t => code.Contains("\"" + t + "\""))
          && F(thoughts["Rimfolk_Thought_MarkFault"].Descendants("baseMoodEffect").Single().Value) < 0f && F(thoughts["Rimfolk_Thought_PetitionTurnedAway"].Descendants("baseMoodEffect").Single().Value) < 0f
          && F(thoughts["Rimfolk_Thought_ApprenticeMaster"].Element("durationDays")!.Value) >= 30f, "six moods, the stings the fault and the petition ignored; the maker's a lasting one");

        // The player's mods, drawn on softly and from their own catalogue.
        var cat = Catalogue(root);
        var active = cat.Where(r => r.active).Select(r => r.type + ":" + r.def).ToHashSet();
        bool From(string type, string def, string mod) => cat.Any(r => r.active && r.type == type && r.def == def && r.mod.Equals(mod, StringComparison.OrdinalIgnoreCase));
        C(PeopleEvents7Math.Wells.All(w => From("ThingDef", w, "dubwise.rimefeller")), "Rimefeller's wells are in the player's list");
        C(new[] { "Granite", "Limestone", "Marble", "Sandstone", "Slate" }.All(s => From("TerrainDef", "VFEArch_Bridge" + s, "vanillaexpanded.vfearchitect")) && active.Contains("TerrainDef:Bridge")
          && code.Contains("\"VFEArch_Bridge\" + s") && code.Contains("\"Bridge\""), "a marked bridge like Hardwater's of the colony's stone (Vanilla Furniture Expanded - Architect), the game's wooden bridge after");
        var fountains = new[] { "AP_ThreeTieredFountain", "AP_RoundFountain", "AP_HexagonalFountain", "AP_SquareFountain", "AP_ObeliskFountain", "AP_ArabicFountain" };
        C(fountains.All(f => From("ThingDef", f, "sarg.alphapropsparks") && code.Contains("\"" + f + "\"")) && From("ThingDef", "Fountain", "dubwise.dubsbadhygiene") && active.Contains("ThingDef:SculptureGrand")
          && From("ThingDef", "VFE_StatueGrand", "vanillaexpanded.vfeart") && From("ThingDef", "VFE_Monument", "vanillaexpanded.vfeart") && code.Contains("\"VFE_StatueGrand\"") && code.Contains("\"VFE_Monument\""),
            "the fountains of Alpha Props - Parks and Dubs Bad Hygiene; Vanilla Furniture Expanded - Art's grand statue and monument and the game's grand sculpture");
        C(From("ExpertiseDef", PeopleEvents7Math.QualityExpertise, "vanillaexpanded.skills"), "the quality expertise is Vanilla Skills Expanded's own");
        C(active.Contains("IncidentDef:WandererJoin") && active.Contains("IncidentDef:TraderCaravanArrival") && active.Contains("ThingDef:Chemfuel") && active.Contains("RoomRoleDef:DiningRoom")
          && active.Contains("RoomRoleDef:RecRoom"), "the game's wanderer and caravan, its chemfuel and its rooms");
        var caravans = cat.Where(r => r.active && r.type == "TraderKindDef").Select(r => PeopleEvents7Math.TradeKind(r.def)).ToList();
        C(caravans.Count(k => k == "goods") >= 3 && caravans.Count(k => k == "arms") >= 3, "the player's peoples send traders of goods and of arms to call for");
        var kinds = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Defs", "PeopleDefs"), "Rimfolk_People_*.xml").Select(File.ReadAllText));
        var named = Regex.Matches(code, "\"(Rimfolk_(KilnGuild|SignalLine|Deepwell)_\\w+)\"").Select(mm => mm.Groups[1].Value).Distinct().ToList();
        C(named.Count >= 8 && named.All(k => kinds.Contains("<defName>" + k + "</defName>")), "every kind of person named is one of the people's own");

        // ================= Their words =================

        var keyedFile = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeopleEvents7.xml"));
        var keyed = XDocument.Parse(keyedFile).Root!.Elements().Select(e => e.Name.LocalName).ToHashSet();
        var used = Regex.Matches(code, "\"(CC_PEv7_\\w+[A-Za-z])\"").Select(mm => mm.Groups[1].Value).Distinct().ToList();
        C(used.Count >= 100 && used.All(keyed.Contains), $"every word the code says is keyed ({used.Count}{(used.Any(u => !keyed.Contains(u)) ? ", missing " + string.Join(", ", used.Where(u => !keyed.Contains(u))) : "")})");
        var built = new[] { "goods", "arms", "hands", "word" }.SelectMany(c => new[] { "CC_PEv7_Call_Radio_" + c, "CC_PEv7_Call_Runner_" + c })
            .Concat(new[] { "North", "South", "East", "West" }.Select(d => "CC_PEv7_Dir_" + d)).Concat(PeopleEvents7Math.Works.Select(w => "CC_PEv7_Work_" + w))
            .Concat(new[] { "CC_PEv7_MarkTrueBack_2", "CC_PEv7_MarkFaultBack_2", "CC_Memory_Deed_markTrue", "CC_Memory_Deed_markTrue_Name", "CC_Memory_Deed_markTrue_Many" }).ToList();
        C(built.All(keyed.Contains), "the words put together by the code are keyed: the calls by radio and runner, the directions, the works, the maker's replies, the deed");
        var texts7 = new[] { Path.Combine("Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents7.xml"), Path.Combine("Defs", "CampDefs", "Rimfolk_CampKinds_PeopleEvents7.xml"),
                Path.Combine("Languages", "English", "Keyed", "Rimfolk_PeopleEvents7.xml"), Path.Combine("Defs", "Misc", "Rimfolk_PeopleEvents7_Misc.xml") }
            .Select(x => Regex.Replace(File.ReadAllText(Path.Combine(root, x)), "<!--.*?-->|<\\?xml[^>]*>|\\[[^\\]]*\\]", "", RegexOptions.Singleline)).Aggregate((x, y) => x + y);
        C(!texts7.Contains('—') && !texts7.Contains('!') && !Regex.IsMatch(texts7, @"\bin \d+ (days?|hours?)\b"), "plain words, no countdowns");
        C(!Regex.IsMatch(texts7.ToLowerInvariant(), @"\b(dynasty|dynasties|bloodline|heir to the house)\b"), "the colony is the family: no houses");
        C(!Regex.IsMatch(texts7, @"\b(she|her|hers)\b"), "nobody of theirs is given a sex the game didn't");
        C(!Regex.IsMatch(texts7, @"\b(god|gods|goddess)\b", RegexOptions.IgnoreCase), "the gods unseen: no god named");
        string peoples = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Defs", "AlmanacArticleDefs"), "Almanac_Peoples*.xml").Select(File.ReadAllText)).ToLowerInvariant();
        C(new[] { "the master's mark", "come back better", "a work that stays", "a voice for the colony", "a mast at a friend's town", "the line crew", "the pit's cave-in", "oil in the ground",
            "the organizer's evening", "the picket" }.All(peoples.Contains), "each people's Almanac page says what it does now");

        // ================= The seams =================

        string six = Read("Peoples", "Events6", "PeopleEvents6.cs");
        C(six.Contains("return PeopleEvents7.Hour(v, comp, now);") && six.Contains("PeopleEvents7.Hourly(now)"), "round 7's visits and watch wait on round 1's hour, through rounds 2 to 6");
        var visitKinds = Regex.Matches(code, @"public const string (\w*Kind) = ""([^""]+)"";").Select(mm => mm.Groups[2].Value).ToList();
        C(visitKinds.Count == 3 && visitKinds.All(k => k.StartsWith("pev:")) && visitKinds.Distinct().Count() == 3, "their visits are the events' own kind (\"pev:\"), none twice");
        string earlier = string.Join("\n", new[] { "Events", "Events2", "Events3", "Events4", "Events5", "Events6" }.SelectMany(x => Directory.GetFiles(Path.Combine(src, "Peoples", x), "*.cs")).Select(File.ReadAllText));
        C(visitKinds.All(k => !earlier.Contains("\"" + k + "\"")), "none of their visits' kinds is an earlier round's");
        string season = Read("Peoples", "Events2", "SeasonAway.cs");
        C(season.Contains("[PitCaveIn.AwayKey] = new CaveInAway()") && code.Contains("public const string AwayKey = \"cavein\";") && code.Contains("SeasonAway.Send(AwayKey"),
            "the miner and the doctor at the pit are round 2's season away");
        C(Read("Arcs", "ArcHooks.cs").Contains("MastersMark.Made(worker, thing)"), "a fine piece made is heard of by the guild (the fine-piece hook)");
        C(Read("World", "Patch_FactionMemory.cs").Contains("ComeBackBetter.Ended(m)") && Read("World", "Ledger", "LedgerKeep.cs").Contains("case ComeBackBetter.KeepReason: return ComeBackBetter.Holds(p);"),
            "a wander-year ended well is noted for its journeyman's return, and they are kept in the world until then");
        C(Regex.Matches(Read("World", "NeighbourLetters.cs"), @"RelayMast\.Working\(l\.map\) \|\| PeopleEvents7\.Relays\(l\.map, n\)").Count == 2, "the letters' road both ways reads the Line's masts at towns and its crews");
        C(Read("Peoples", "PeoplesF.cs").Contains("!Picket.Here(Striker())"), "while a picket camps by the road, round F's strike visit at the gate waits on it");
        C(Read("World", "FactionMemory.cs").Contains("[\"markTrue\"]") && code.Contains("KinMemory.Note(f, \"markTrue\", maker, label)"), "the guild remembers a true mark by its maker's name");
        C(Read("Text", "Facts", "FactsMath.cs").Contains("\"them:camp:WorkThatStays\", \"them:camp:LineCrew\", \"them:camp:Picket\""), "the facts service knows the round's camps");
        C(Read("..", "..", "Tools", "DefValidator", "Program.cs").Contains("\"WORK\", \"MAST\", \"NEWS\""), "the def check knows the camps' own placeholders");
        C(code.Contains("UnionStrike.Settle(") && code.Contains("UnionStrike.Of(") && code.Contains("HappeningWorker_UnionDrill.Seam(") && code.Contains("RelayMast.Fits(") && code.Contains("PeoplesF.MastDef")
          && code.Contains("PeopleEvents6.HighGround(") && code.Contains("HeardPlaces.Heard(") && code.Contains("CampRaids.Send(") && code.Contains("PlayOuts.Run(") && code.Contains("PeopleWorkOn.Bring(")
          && code.Contains("Couriers.TakeFromStores(") && code.Contains("GenConstruct.CanPlaceBlueprintAt(") && code.Contains("Expertise.Give(") && code.Contains("DilemmaEffect_Made.Make("),
            "the shapes reused: the union's strike and drill, the relay mast and the high ground, places heard of, a band against a camp, the carry-out, the work beside, the couriers, Rimefeller's own placing, the expertise, a thing made");
        var harms = Regex.Matches(code, @"Harm\.(Roll|Ail|Injure|Fight)\(([^;]*?)HarmKinds\.(\w+)").Select(mm => mm.Groups[3].Value).Distinct().OrderBy(x => x).ToList();
        C(harms.SequenceEqual(new[] { "CampLeft", "Dangers", "UnionDrill" }) && !Regex.IsMatch(code, @"TakeDamage") && code.Contains("IncidentWorker_Happening.BattlesAllowed(c.map)")
          && Regex.Matches(code, @"HarmBook\.Instance\?\.Came\(HarmKinds\.CampLeft").Count == 1,
            "no harm kind of their own: the drill's crush at the pit, the dangers on the map for a fall from the mast, the camps' embers for the lightning's fire; the strikebreakers only while Rimfolk's world events may bring a fight");
        C(!code.Contains("[HarmonyPatch"), "no game hooks of their own");
        C(!code.Contains("MoteMaker.ThrowText") && code.Contains("PeoplesG.SayLine(") && code.Contains("c.SayMoment(") && code.Contains("PeoplesG.Say(") && code.Contains("Speech.Say("),
            "they speak through the events' and the camps' own lines");
        C(!Regex.IsMatch(code, @"Sched\.|\bLocomotionUrgency\.Sprint"), "no clock of their own: the events' hour and the camps' hour");
        C(Regex.Matches(code, @"StoryDirection\.Factor\(StoryWaysMath\.Visitors\)").Count >= 3, "the code-fired comings read the storytellers' lean on visitors");
        string book = File.ReadAllText(Path.Combine(dir, "PeopleEvents7Book.cs"));
        C(book.Contains("marks.Count > 6") && book.Contains("PeopleEvents7Math.MaxComeBacks") && book.Contains("PeopleEvents7Math.MaxTownMasts") && book.Contains("shares.Count > 4")
          && book.Contains("evenings.Count > 4") && book.Contains("picketRolled.Count > 8"), "what they keep is bounded");
        C(Regex.Matches(code, @"\[DebugAction\(""Rimfolk"", ""Peoples' events 7: ").Count == 9, "nine dev actions");
        string save = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(new[] { "marks", "comeBacks", "masts", "shares", "evenings", "picketRolled" }.All(x => save.Contains("Peoples/Events7/PeopleEvents7Book.cs:" + x)) && save.Contains("Peoples/Events7/WorkThatStays.cs:cells"),
            "their saved lists are in the save model");
        string audit = File.ReadAllText(Path.Combine(root, "Tools", "playout_audit.py"));
        C(events.Values.Concat(campKinds.Values).SelectMany(e => e.Descendants("li")).Select(li => li.Attribute("Class")?.Value?.Replace("ColonyCouncil.", ""))
                .Where(v => v != null && v.StartsWith("DilemmaEffect_") && Regex.IsMatch(code, @"class " + v + @"\b") && v != "DilemmaEffect_OrganizerSpoke").Distinct().All(v => audit.Contains("\"" + v + "\"")),
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
