using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The second round of event variety (the player's request, 6 November 2026): four council questions about what the
/// colony meets (the cold snap's hours, blight in the fields, the good weapon, the animal whose person died), the herd
/// crossing, two world events (a blight in the region, medicine gone scarce) and two camp kinds (woodcutters,
/// herb-gatherers). The rules without game types (Dilemmas/VarietyTwoMath.cs, the cold snap's short days in
/// AnswerShiftMath, the woods in CampLandMath), and the defs: every age's forms, answers that play out on the map,
/// the risk named before the hunt, both ways in the world events, the camps' stay, requests and goodbye, and nothing
/// that marks the calendar.
/// </summary>
public static class VarietyTwoTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "event variety: " + what);
        }

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        XElement Def(string file, string element, string name) =>
            XDocument.Load(Path.Combine(root, file)).Root!.Elements(element).FirstOrDefault(e => (string)e.Element("defName") == name);
        List<string> Lis(XElement e, string name) => e?.Element(name)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
        var keyed = Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml")
            .SelectMany(f => XDocument.Load(f).Root!.Elements()).GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => g.First().Value);

        // ================= The rules =================

        C(VarietyTwoMath.Upgrade(300f, 0f, false, false), "anything is a step up for someone unarmed");
        C(VarietyTwoMath.Upgrade(400f, 200f, true, true) && !VarietyTwoMath.Upgrade(240f, 200f, true, true), "a weapon of the same kind worth clearly more is a step up; a little more isn't");
        C(!VarietyTwoMath.Upgrade(90f, 60f, true, true) && VarietyTwoMath.Upgrade(100f, 60f, true, true), "a fine knife is no step up from a plain one: the margin in silver counts too");
        C(!VarietyTwoMath.Upgrade(900f, 100f, true, false), "a gun is no step up for a fighter with a blade, nor a blade for a shooter");
        C(!VarietyTwoMath.Upgrade(0f, 0f, false, false), "nothing is no step up");
        C(VarietyTwoMath.RecentLoss(0f) && VarietyTwoMath.RecentLoss(VarietyTwoMath.LossDays) && !VarietyTwoMath.RecentLoss(VarietyTwoMath.LossDays + 0.5f),
            "a death is fresh for the days it should be");
        C(!VarietyTwoMath.RecentLoss(-1f), "a death nothing tells of is never fresh");
        C(VarietyTwoMath.Freezing(-5f) && VarietyTwoMath.Freezing(VarietyTwoMath.FreezingAt) && !VarietyTwoMath.Freezing(8f), "a cold snap asked about only when it freezes");
        C(VarietyTwoMath.ColdRoom(5f) && !VarietyTwoMath.ColdRoom(18f) && VarietyTwoMath.ColdRoomAt < 21f,
            "a cold room is colder than the fire kept in it needs (OnMapTimed keeps fires in rooms under 21)");
        C(!VarietyTwoMath.BlightWorthAsking(1) && VarietyTwoMath.BlightWorthAsking(VarietyTwoMath.MinBlighted), "one blighted plant is not worth the council's time; two are");
        C(!VarietyTwoMath.HerdBigEnough(3) && VarietyTwoMath.HerdBigEnough(4), "a herd is four or more");
        C(VarietyTwoMath.FewOf(4) == 2 && VarietyTwoMath.FewOf(9) == 3 && VarietyTwoMath.FewOf(30) == 4, "a few of a herd: a third, two to four");
        C(VarietyTwoMath.FewOf(2) == 1 && VarietyTwoMath.FewOf(1) == 0 && Enumerable.Range(2, 40).All(h => VarietyTwoMath.FewOf(h) < h), "never the whole herd");
        C(!CampLandMath.WoodsEnough(CampLandMath.MinWoods - 1) && CampLandMath.WoodsEnough(CampLandMath.MinWoods), "woodcutters need woods");
        C(CampLandMath.TreeGrown > 0f && CampLandMath.TreeGrown < 1f && CampLandMath.StandAround >= 3, "a grown tree, in a stand");

        // The cold snap's short days: abed through the frost of the morning, to bed early, never someone's recreation.
        string Day(int h) => h <= 5 || h >= 22 ? "Sleep" : h == 20 || h == 21 ? "Joy" : "Anything";
        var cold = AnswerShiftMath.Plan(AnswerShiftMath.Cold, Day);
        C(AnswerShiftMath.Plans.Contains(AnswerShiftMath.Cold), "the short days are one of the plans");
        C(cold.All(c => c.set == "Sleep") && cold.Select(c => c.hour).OrderBy(h => h).SequenceEqual(new[] { 6, 7, 8, 19 }), "short days sleep 6 to 8 and from 19 (20 and 21 were recreation, kept)");
        var allDay = AnswerShiftMath.Plan(AnswerShiftMath.Cold, h => "Anything");
        C(allDay.Select(c => c.hour).OrderBy(h => h).SequenceEqual(new[] { 5, 6, 7, 8, 19, 20, 21 }), "for someone awake all day: 5 to 8 and 19 to 21");
        C(AnswerShiftMath.Hours(AnswerShiftMath.Cold, 12f) >= 24f * 10f, "the short days last until the cold snap ends, days at most");
        foreach (var k in new[] { "CC_Impact_Hours_cold", "CC_Impact_HoursBrief_cold", "CC_DlmV_Hours_cold", "CC_DlmV_HoursLabel_cold" })
            C(keyed.ContainsKey(k), $"the short days have their words ({k})");

        // ================= The questions =================

        const string dlmFile = "Defs/DilemmaDefs/Rimfolk_Dilemmas_Variety2.xml";
        var workers = Code("Dilemmas/DilemmaWorkers_Variety2.cs");
        var onMap = new[] { "DilemmaEffect_Hours", "DilemmaEffect_KeepFire", "DilemmaEffect_CutBlight", "DilemmaEffect_TakeUp", "DilemmaEffect_NewMaster" };
        var questions = new[] { "Rimfolk_DlmE_ColdSnap", "Rimfolk_DlmE_Blight", "Rimfolk_DlmE_GoodWeapon", "Rimfolk_DlmE_MasterGone" };
        var allDlm = XDocument.Load(Path.Combine(root, dlmFile)).Root!.Elements("ColonyCouncil.DilemmaDef").ToList();
        C(allDlm.Count == questions.Length, $"four questions ({allDlm.Count})");
        foreach (var name in questions)
        {
            var d = Def(dlmFile, "ColonyCouncil.DilemmaDef", name);
            C(d != null, name + ": there");
            if (d == null) continue;
            string worker = ((string)d.Element("workerClass") ?? "").Replace("ColonyCouncil.", "");
            C(worker.Length > 0 && workers.Contains("class " + worker + " : DilemmaWorker"), $"{name}: its worker ({worker}) is in the code");
            C(d.Element("timely") == null && d.Element("withinDaysOfThreat") == null && d.Element("seasons") == null,
                $"{name}: asked in the usual rhythm, never outside it (no more questions a year)");
            var texts = Lis(d, "texts");
            C(texts.Any(t => t.StartsWith("[lowtech]")) && texts.Any(t => t.StartsWith("[!lowtech]")), $"{name}: a form for a low-tech colony and one for the rest");
            C(texts.Any(t => !t.StartsWith("[")), $"{name}: a line for any colony");
            var options = d.Element("options")!.Elements("li").ToList();
            int def = int.Parse((string)d.Element("defaultOption") ?? "0");
            C(options.Count == 3 && def >= 0 && def < options.Count, $"{name}: three answers, the council's silence one of them");
            C(options.Count(o => o.Element("effects")?.Elements("li").Any(e => onMap.Contains(((string)e.Attribute("Class") ?? "").Replace("ColonyCouncil.", ""))) == true) >= 1,
                $"{name}: at least one answer plays out on the map");
            foreach (var o in options)
            {
                string label = (string)o.Element("label") ?? "?";
                C(o.Element("effects")?.Elements("li").Any() == true, $"{name} [{label}]: does something");
                C(!((string)o.Element("outcome") ?? "").Contains('—') && !((string)o.Element("outcome") ?? "").Contains('!'), $"{name} [{label}]: plain prose");
            }
            // The answer the silence picks gives the least: nothing on the map.
            var silent = options[def];
            C(silent.Element("effects")!.Elements("li").All(e => !onMap.Contains(((string)e.Attribute("Class") ?? "").Replace("ColonyCouncil.", ""))),
                $"{name}: the council's silence sets nothing going");
        }
        foreach (var effect in new[] { "DilemmaEffect_CutBlight", "DilemmaEffect_TakeUp", "DilemmaEffect_NewMaster" })
        {
            var body = Regex.Match(workers, "class " + effect + @" : DilemmaEffect\s*\{(.*?)\n    \}", RegexOptions.Singleline).Groups[1].Value;
            C(body.Contains("public override void Describe(") && body.Contains("public override string Unavailable("), $"{effect}: says what it does under the answer, and greys it out when it can't");
        }
        C(Regex.IsMatch(workers, @"GameConditionDefOf\.ColdSnap") && workers.Contains("VarietyTwoMath.Freezing("), "the cold snap's question needs the game's cold snap, and frost");
        C(workers.Contains("VarietyTwoMath.ColdRoom(") && workers.Contains("room.Temperature"), "and a room cold enough for the fire it may keep");
        C(workers.Contains("plant.Blighted") && workers.Contains("DesignationDefOf.CutPlant") && workers.Contains("DesignationDefOf.HarvestPlant"), "the blight is the game's, and the answers mark it with the game's own designations");
        C(workers.Contains("JobDefOf.Equip") && workers.Contains("EquipmentUtility.CanEquip(") && workers.Contains("CompBiocodable.IsBiocoded("), "the weapon is taken up with the game's own job, by someone who can carry it");
        C(workers.Contains("TrainableUtility.CanBeMaster(") && workers.Contains("playerSettings.Master = p") && workers.Contains("TimedKind.Follow"), "the animal's new master is the game's, and it follows them a day");
        C(workers.Contains("TrainableDefOf.Obedience") && workers.Contains("VarietyTwoMath.RecentLoss("), "only an animal trained to obey, after a recent loss");
        foreach (Match m in Regex.Matches(workers + Code("Happenings/Workers_Variety2.cs"), "\"(CC_[A-Za-z_]+)\""))
            C(keyed.ContainsKey(m.Groups[1].Value), $"the words for {m.Groups[1].Value}");
        foreach (var t in new[] { "Rimfolk_DlmE_FrozenThrough", "Rimfolk_DlmE_BlightLeft", "Rimfolk_DlmE_ArmedWell", "Rimfolk_DlmE_KeptTheirAnimal", "Rimfolk_DlmE_NewCharge", "Rimfolk_DlmE_NotGivenAnimal" })
        {
            var th = Def("Defs/ThoughtDefs/Rimfolk_Thoughts_Variety2.xml", "ThoughtDef", t);
            C(th != null && File.ReadAllText(Path.Combine(root, dlmFile)).Contains(">" + t + "<"), $"{t}: a memory an answer gives");
            if (th == null) continue;
            float mood = float.Parse(th.Descendants("baseMoodEffect").First().Value, System.Globalization.CultureInfo.InvariantCulture);
            C(Math.Abs(mood) <= 4f, $"{t}: small");
            if (th.ToString().Contains("RewardInstead")) C(mood == 0f, $"{t}: something else instead of a mood, so no mood");
        }

        // ================= The herd crossing =================

        var herd = Def("Defs/IncidentDefs/Rimfolk_Events_Variety2.xml", "IncidentDef", "Rimfolk_Evt_HerdCrossing");
        C(herd != null, "the herd crossing: there");
        if (herd != null)
        {
            var ext = herd.Descendants("li").First(e => ((string)e.Attribute("Class") ?? "").EndsWith("HappeningExtension"));
            var texts = Lis(ext, "texts");
            C(texts.Any(t => t.StartsWith("[lowtech]")) && texts.Any(t => t.StartsWith("[!lowtech]")), "the herd crossing: every age's forms");
            C(texts.All(t => Regex.IsMatch(t, @"\bturn\b")), "the herd crossing: every letter names the risk before the answer (a hunted beast may turn)");
            var options = ext.Element("options")!.Elements("li").ToList();
            C(options.Count == 3 && options.Take(2).All(o => o.ToString().Contains("DilemmaEffect_HuntHerd")) && !options[2].ToString().Contains("HuntHerd"),
                "the herd crossing: a few, the biggest, or let them cross");
            C((string)ext.Element("defaultOption") == "2", "the herd crossing: the silence lets them cross");
            C((string)ext.Element("group") == "Nature" && ext.Element("battle") == null, "the herd crossing: the land's, never a fight of its own");
        }
        string herdCode = Code("Happenings/Workers_Variety2.cs");
        C(herdCode.Contains("LordJob_ExitMapNear") && herdCode.Contains("VarietyTwoMath.HerdBigEnough("), "the herd is the game's own, crossing for an edge, big enough");
        C(herdCode.Contains("GetManhunterOnDamageChance") && herdCode.Contains("CC_Impact_HuntHerdRisk"), "the game's own risk is read and named under the answer");
        C(herdCode.Contains("DesignationDefOf.Hunt") && !herdCode.Contains("Harm."), "the hunt is the game's designation; no harm of Rimfolk's own");

        // ================= The world events =================

        const string wsFile = "Defs/WorldSituationDefs/Rimfolk_WS_Variety2.xml";
        foreach (var name in new[] { "Rimfolk_WS_Blight", "Rimfolk_WS_MedicineScarce" })
        {
            var s = Def(wsFile, "ColonyCouncil.WorldSituationDef", name);
            C(s != null, name + ": there");
            if (s == null) continue;
            var texts = Lis(s, "texts");
            C(texts.Count >= 2 && texts.Any(t => t.StartsWith("[theirLowtech]") || t.StartsWith("[lowtech]")), $"{name}: forms for the peoples' ages");
            var beats = s.Element("beats")!.Elements("li").ToList();
            C(beats.Count >= 2 && beats.Count(b => b.Element("options") != null) >= 2, $"{name}: two questions or more along the way");
            var endings = s.Element("endings")!.Elements("li").ToList();
            C(endings.Any(e => e.Element("minHelp") != null) && endings.Any(e => e.Element("minHelp") == null), $"{name}: ends by what the colony did");
            C(beats.SelectMany(b => b.Element("options")?.Elements("li") ?? Enumerable.Empty<XElement>()).Any(o => o.ToString().Contains("WorldEffect_Silver")),
                $"{name}: what the colony has is worth more (silver for it)");
            C(beats.SelectMany(b => b.Element("options")?.Elements("li") ?? Enumerable.Empty<XElement>()).Any(o => o.Element("ways")?.Element("generosity") != null),
                $"{name}: or it can do good");
        }
        var blight = Def(wsFile, "ColonyCouncil.WorldSituationDef", "Rimfolk_WS_Blight");
        C(blight?.Element("modifiers")?.Elements("li").Any(m => (string)m.Element("incident") == "CropBlight" && float.Parse((string)m.Element("factor"), System.Globalization.CultureInfo.InvariantCulture) > 1f) == true,
            "the region's blight makes the game's blight likelier on the colony's own fields: the other way it pulls");

        // ================= The camps =================

        const string campFile = "Defs/CampDefs/Rimfolk_CampKinds_Variety2.xml";
        foreach (var name in new[] { "Rimfolk_Camp_Woodcutters", "Rimfolk_Camp_HerbGatherers" })
        {
            var k = Def(campFile, "ColonyCouncil.CampKindDef", name);
            C(k != null, name + ": there");
            if (k == null) continue;
            foreach (var part in new[] { "titles", "texts", "settled", "joinTexts", "farewell", "lines" })
                C(Lis(k, part).Count >= 2, $"{name}: its {part}");
            C(Lis(k, "texts").Count(t => t.StartsWith("[their:")) >= 3, $"{name}: in the form of its own people's age");
            C(k.Element("leaveBehind") == null && k.Element("farewellWalk") == null, $"{name}: leaves no gift that would mark the colony's calendar");
            var asks = k.Element("asks")!.Elements("li").ToList();
            C(asks.Count >= 3 && asks.Any(a => (string)a.Element("theyAsk") == "true"), $"{name}: the colony's asks and a request of their own");
            foreach (var a in asks.Where(a => (string)a.Element("theyAsk") == "true"))
                C(Lis(a, "askTexts").Count > 0 && Lis(a, "refused").Count > 0 && a.Element("refuseLabel") != null, $"{name}: their request is asked, and a no is told");
            C(asks.All(a => a.Element("effects")?.Elements("li").Any() == true && Lis(a, "outcomes").Count > 0), $"{name}: every ask does something, and says so");
            C(Lis(k, "help").Count > 0 && float.Parse((string)k.Element("helpChance") ?? "0.15", System.Globalization.CultureInfo.InvariantCulture) > 0f, $"{name}: a good turn of its own");
        }
        var wood = Def(campFile, "ColonyCouncil.CampKindDef", "Rimfolk_Camp_Woodcutters");
        C((string)wood?.Element("needs") == "woods" && float.Parse((string)wood?.Element("chopsPerDay") ?? "0", System.Globalization.CultureInfo.InvariantCulture) > 0f,
            "woodcutters come for the woods and fell real trees");
        C(wood?.Element("campGoods")?.Descendants("leftForColony").Any(e => e.Value == "true") == true, "their woodpile is left when they go in peace");
        string land = Code("Camps/CampKindsLand.cs");
        C(Regex.Matches(land, "case \"woods\":").Count == 2 && land.Contains("WoodsTargets(map)") && land.Contains("CampLandMath.WoodsEnough("), "the woods: whether they come, and where they pitch");
        var herbs = Def(campFile, "ColonyCouncil.CampKindDef", "Rimfolk_Camp_HerbGatherers");
        C(Lis(herbs, "help").Contains("medicine") && Lis(herbs, "situationKinds").Contains("scarcity"), "herb-gatherers gather and leave medicine, and come oftener when it's scarce");
        C(keyed.ContainsKey("CC_CampErrand_Arrived_herbwalk"), "their walk in the fields has its words");

        // ================= The play-out audit knows them =================

        string audit = File.ReadAllText(Path.Combine(root, "Tools", "playout_audit.py"));
        foreach (var effect in new[] { "DilemmaEffect_CutBlight", "DilemmaEffect_TakeUp", "DilemmaEffect_NewMaster", "DilemmaEffect_HuntHerd" })
            C(audit.Contains("\"" + effect + "\""), $"{effect} counts as playing out on the map");
        return n;
    }
}
