using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The peoples' own events, round 3 (Peoples/Events3/; docs/peoples-events-proposals.md, items 2, 4, 6, 7, 8, 9, 11, 13, 15
/// and 16): their pure rules (red ink's restitution and its argument, the road's floor by the age, its way across the map
/// and its stretch a day, the litters, the sick room's slower spread, the sickhouse's patients, the drove's toll and its
/// grazing, the thieves and the herders' trail, the calving and its hard birth, the shrine's wear and its stops, the table's
/// odds), their defs and words, the player's mods they draw on, and the seams with the game and rounds 1 and 2.
/// </summary>
public static class PeopleEvents3Tests
{
    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the peoples' events, round 3: " + what);
        }
        float F(string v) => float.Parse(v, CultureInfo.InvariantCulture);

        // ================= 2. Red ink =================

        C(PeopleEvents3Math.RedInkKinds.SequenceEqual(new[] { "robbed", "caravanRaided" }) && PeopleEvents3Math.KilledInRobbing(0, 2 * PeopleEvents3Math.DayTicks)
          && !PeopleEvents3Math.KilledInRobbing(0, 3 * PeopleEvents3Math.DayTicks), "the wrongs written in red: traders robbed, a caravan raided, and a killing that came with them");
        C(!PeopleEvents3Math.InBook(1f) && PeopleEvents3Math.InBook(2f) && PeopleEvents3Math.InBook(120f) && !PeopleEvents3Math.InBook(121f) && !PeopleEvents3Math.InBook(float.NaN),
            "the factor comes after the book is written up, never about an old wrong");
        C(PeopleEvents3Math.Restitution(20, 1, 1f) == 240 && PeopleEvents3Math.Restitution(1, 1, 0.1f) == 80 && PeopleEvents3Math.Restitution(500, 6, 5f) == 1500
          && PeopleEvents3Math.Restitution(20, 4, 1f) > PeopleEvents3Math.Restitution(20, 1, 1f), "paying back by the deed's size and how many, within 80 and 1500");
        C(PeopleEvents3Math.GoodwillBack(20) == 10 && PeopleEvents3Math.GoodwillBack(0) == 3 && PeopleEvents3Math.GoodwillBack(100) == 15 && PeopleEvents3Math.Twice(240) == 480,
            "part of their goodwill back; written twice, owed twice");
        C(PeopleEvents3Math.WorseTerms < 0f && PeopleEvents3Math.WorseTerms >= -0.1f && PeopleEvents3Math.ArgueRisk == 0.7f && PeopleEvents3Math.MaxRedInk >= 2,
            "worse terms with every league until paid; the argument's risk on the card");

        // ================= 4. The road gang =================

        C(PeopleEvents3Math.RoadFloor(0, "Granite") == "PackedDirt" && PeopleEvents3Math.RoadFloor(1, "Slate") == "FlagstoneSlate" && PeopleEvents3Math.RoadFloor(1, null) == "FlagstoneGranite"
          && PeopleEvents3Math.RoadFloor(2, "Granite") == "Concrete" && PeopleEvents3Math.RoadFloor(3, "Granite") == "PavedTile", "packed earth, flagstones of the land's stone, concrete, paved tile");
        var stretches = Enumerable.Range(0, 10).Select(d => PeopleEvents3Math.Stretch(100, 9, d)).ToList();
        C(stretches[0] == 12 && stretches[8] == 100 && stretches[9] == 100 && stretches.Zip(stretches.Skip(1), (a, b) => b >= a).All(x => x) && PeopleEvents3Math.Stretch(0, 5, 2) == 0,
            "a stretch a day, the whole road down by the last");
        // The way across: around a wall, straight where it can, nowhere it can't.
        float Open(int x, int z) => x == 5 && z >= 0 && z <= 7 ? -1f : 1f;
        var way = PeopleEvents3Math.Route(11, 11, Open, 0, 5, 10, 5);
        C(way != null && way.First() == (0, 5) && way.Last() == (10, 5) && way.All(p => Open(p.x, p.z) >= 0f) && way.Zip(way.Skip(1), (a, b) => Math.Abs(a.x - b.x) + Math.Abs(a.z - b.z)).All(d => d == 1),
            "the road's way goes round what it can't cross, four ways only, step by step");
        var straight = PeopleEvents3Math.Route(20, 9, (x, z) => 1f, 0, 4, 19, 4);
        C(straight != null && straight.Count == 20 && straight.All(p => p.z == 4), "on open ground it runs straight");
        C(PeopleEvents3Math.Route(5, 5, (x, z) => x == 2 ? -1f : 1f, 0, 2, 4, 2) == null && PeopleEvents3Math.Route(5, 5, (x, z) => 1f, -1, 0, 4, 4) == null, "no way, no road");
        var field = PeopleEvents3Math.Route(15, 9, (x, z) => x >= 5 && x <= 9 && z >= 2 && z <= 6 ? PeopleEvents3Math.FieldCost : 1f, 0, 4, 14, 4);
        C(field != null && !field.Any(p => p.x >= 5 && p.x <= 9 && p.z >= 2 && p.z <= 6), "round a field unless let through");
        var wide = PeopleEvents3Math.Widen(straight, PeopleEvents3Math.RoadWidth, 20, 9);
        C(wide.Count == 40 && wide.Distinct().Count() == 40 && wide.Count(p => p.z == 4) == 20 && PeopleEvents3Math.Widen(straight, 1, 20, 9).Count == 20, "two wide, none twice; a spur one wide");
        C(PeopleEvents3Math.SpurPrice(10, 1f) == 120 && PeopleEvents3Math.SpurPrice(1, 0.1f) == 60 && PeopleEvents3Math.SpurPrice(500, 5f) == 600 && PeopleEvents3Math.SpurStone(30) == 60
          && PeopleEvents3Math.SpurStone(1) == 20, "the spur for silver or stone");
        C(PeopleEvents3Math.BuilderWage(10) == 100 && PeopleEvents3Math.BuilderWage(0) == 40 && PeopleEvents3Math.LineRefusedGoodwill == -5 && PeopleEvents3Math.GangBandChance > 0f
          && PeopleEvents3Math.GangBandChance <= 0.2f && PeopleEvents3Math.AroundMargin >= 6, "the builder's day's wage; the line refused costs; a band now and then");

        // ================= 6. The litters =================

        C(PeopleEvents3Math.Litters(1) == 2 && PeopleEvents3Math.Litters(4) == 3 && PeopleEvents3Math.WorstHurt(0) == 0 && PeopleEvents3Math.WorstHurt(1) == 1 && PeopleEvents3Math.WorstHurt(5) == 3,
            "two or three bearers; the worst hurt, half of them");
        C(PeopleEvents3Math.LittersChance > 0f && PeopleEvents3Math.LittersChance < 0.5f && PeopleEvents3Math.LittersGapDays >= 10 && PeopleEvents3Math.LittersGoodwill == 5
          && PeopleEvents3Math.LittersMinHours >= 1, "a small chance after a raid, an hour or more after the fight, not soon again; the order +5");

        // ================= 7. The sick room =================

        C(Math.Abs(PeopleEvents3Math.SickRoomSpread - 1f / 3f) < 1e-5f && PeopleEvents3Math.SickRoomDays == 3 && PeopleEvents3Math.RoundHours.Length == 4 && PeopleEvents3Math.RoundHour(12)
          && !PeopleEvents3Math.RoundHour(13), "three days of rounds; the sickness at a third of its rate");
        C(PeopleEvents3Math.SisterCatch > 0f && PeopleEvents3Math.SisterCatch <= 0.1f && PeopleEvents3Math.SickRoomMedicine > 0, "she may catch it, at a small chance through the budgets");

        // ================= 8. The field sickhouse =================

        var pats = Enumerable.Range(0, 100).Select(i => PeopleEvents3Math.Patients(i / 100f)).ToList();
        C(pats.Min() == 2 && pats.Max() == 6 && PeopleEvents3Math.Patients(float.NaN) == 2, "two to six of the region's sick and wounded");
        C(PeopleEvents3Math.DoctorCatch > 0f && PeopleEvents3Math.DoctorCatch <= 0.15f && PeopleEvents3Math.CrossChance <= 0.1f && PeopleEvents3Math.WardXp >= 2000f
          && PeopleEvents3Math.MedicineBandChance <= 0.2f, "the doctor learns a lot and may bring it home; the crossing and the band rare");

        // ================= 9. The drove =================

        C(PeopleEvents3Math.DroveMin == 20 && PeopleEvents3Math.DroveMax == 40 && PeopleEvents3Math.DroveMinDays == 2f && PeopleEvents3Math.DroveMaxDays == 3f, "twenty to forty head, two or three days");
        C(PeopleEvents3Math.DroveToll(30, 1f) == 150 && PeopleEvents3Math.DroveToll(1, 0.1f) == 60 && PeopleEvents3Math.DroveToll(100, 5f) == 300, "a toll of five a head");
        C(PeopleEvents3Math.Grazed(10) == 2 && PeopleEvents3Math.Grazed(1000) == PeopleEvents3Math.DroveMaxGrazed && PeopleEvents3Math.Grazed(0) == 0, "a fifth of the crops within reach, at most a few dozen");
        C(PeopleEvents3Math.DroveShare(0f) == 0f && PeopleEvents3Math.DroveShare(1f) == 1f && Math.Abs(PeopleEvents3Math.DroveShare(0.5f) - 0.5f) < 1e-5f && PeopleEvents3Math.DroveShare(2f) == 1f,
            "the herd's ground from edge to edge, eased in and out");
        C(PeopleEvents3Math.DroveTurnedGoodwill < PeopleEvents3Math.DroveTollGoodwill && PeopleEvents3Math.DroveTollGoodwill < 0 && PeopleEvents3Math.CrossedGoodwill > 0
          && PeopleEvents3Math.KickChance <= 0.2f, "a toll costs, turning away costs more; a rare kick");

        // ================= 11. The thief on the drove road =================

        C(PeopleEvents3Math.DriveOffChance(true) > PeopleEvents3Math.DriveOffChance(false) && PeopleEvents3Math.DriveOffChance(true) <= 0.3f && PeopleEvents3Math.DriveOffChance(false) > 0f,
            "raiders on their way out drive beasts off now and then, likelier with drovers of their own");
        var driven = Enumerable.Range(0, 100).Select(i => PeopleEvents3Math.DriveOffCount(i / 100f)).ToList();
        C(driven.Min() == 1 && driven.Max() == 3, "one to three beasts");
        C(PeopleEvents3Math.BroughtBack(new[] { 0.1f, 0.5f, 0.7f, float.NaN }) == 2 && PeopleEvents3Math.BroughtBack(null) == 0 && PeopleEvents3Math.TheirDue(2) == 1
          && PeopleEvents3Math.TheirDue(0) == 0, "alone they bring back what they can, keeping one as their due");
        C(PeopleEvents3Math.TrailFromHour == 6 && PeopleEvents3Math.TrailToHour == 10 && PeopleEvents3Math.TrailTilesMin == 1 && PeopleEvents3Math.TrailTilesMax == 2
          && PeopleEvents3Math.ThiefPoints < 0.5f && PeopleEvents3Math.CampDays >= 3, "the next morning; the thieves' camp a tile or two off, a share of the threat");

        // ================= 13. Spring calving =================

        C(PeopleEvents3Math.InCalfMin == 4 && PeopleEvents3Math.InCalfMax == 7 && PeopleEvents3Math.GestationAt(5f, 20f) == 0.75f && PeopleEvents3Math.GestationAt(5f, 0f) == 0.99f
          && PeopleEvents3Math.GestationAt(100f, 20f) == 0.05f, "four to seven in calf, the game's pregnancy set so they calve over the stay");
        C(Math.Abs(PeopleEvents3Math.BirthOdds(0) - 0.45f) < 1e-4f && Math.Abs(PeopleEvents3Math.BirthOdds(10) - 0.8f) < 1e-4f && PeopleEvents3Math.BirthOdds(20) == 0.95f
          && Math.Abs(PeopleEvents3Math.HerdersOdds - 0.73f) < 1e-4f, "both live by the hand's way with beasts, as the card's roll has it");
        C(PeopleEvents3Math.HardBirthChance <= 0.4f && PeopleEvents3Math.CalfMissingChance <= 0.2f && PeopleEvents3Math.AfterbirthPredator <= 0.5f && PeopleEvents3Math.HardHeld < 1f,
            "a hard birth, a missing calf and the afterbirth's predator now and then");
        C(PeopleEvents3Math.CalvingToll(5, 1f) == 40 && PeopleEvents3Math.CalvingToll(10, 1f) == 80 && PeopleEvents3Math.CalvingToll(100, 5f) == 160, "the calving's toll");

        // ================= 15. The old shrine =================

        C(PeopleEvents3Math.HolyKinds.SequenceEqual(new[] { "ancient", "grave", "tree", "landmark" }) && PeopleEvents3Math.ShrineDays(false) == 3 && PeopleEvents3Math.ShrineDays(true) == 2,
            "an ancient structure, a grave, a great old tree, the landmark; three days' work, two with a hand");
        var offerings = Enumerable.Range(0, 100).Select(i => (PeopleEvents3Math.OfferingSilver(i / 100f), PeopleEvents3Math.OfferingHerbs(i / 100f))).ToList();
        C(offerings.Min(o => o.Item1) == 15 && offerings.Max(o => o.Item1) == 40 && offerings.Min(o => o.Item2) == 2 && offerings.Max(o => o.Item2) == 5, "a little silver or herbs at its foot");
        C(PeopleEvents3Math.Ruined(60, 180) && !PeopleEvents3Math.Ruined(70, 180) && PeopleEvents3Math.Kept(110, 180) && !PeopleEvents3Math.Kept(100, 180) && !PeopleEvents3Math.Ruined(1, 0),
            "fallen to ruin below a third, kept again above three fifths");
        C(PeopleEvents3Math.StopChance <= 0.15f && PeopleEvents3Math.StopGapDays >= 7 && PeopleEvents3Math.WeatherPerDay > 0f && PeopleEvents3Math.WeatherPerDay < 0.05f
          && PeopleEvents3Math.MournGoodwill < 0, "a pilgrim's stop now and then; it wears slowly; mourned");

        // ================= 16. Two faiths at one table =================

        C(Math.Abs(PeopleEvents3Math.SettleChance(0, 0) - 0.33f) < 1e-4f && Math.Abs(PeopleEvents3Math.SettleChance(10, 2) - 0.68f) < 1e-4f && PeopleEvents3Math.SettleChance(20, 20) == 0.95f
          && PeopleEvents3Math.SettleChance(4, 12) == PeopleEvents3Math.SettleChance(12, 0), "settled by the better speaker's Social, as the card's roll has it");
        C(PeopleEvents3Math.NearSplit(3, 2) && !PeopleEvents3Math.NearSplit(2, 2) && !PeopleEvents3Math.NearSplit(9, 2) && PeopleEvents3Math.NearSplit(9, 3), "near a split: a third wavering, two at least");
        C(PeopleEvents3Math.SettledDays >= 10 && PeopleEvents3Math.Steadied > 0f && PeopleEvents3Math.Steadied <= 0.15f && PeopleEvents3Math.SplitShaken > 0f && PeopleEvents3Math.SplitShaken <= 0.1f,
            "settled for a season; steadied, or shaken, a little");

        // ================= Their defs =================

        var events = XDocument.Load(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents3.xml")).Root!.Elements("IncidentDef").ToDictionary(Dn);
        C(events.Keys.SequenceEqual(new[] { "Rimfolk_Evt_RedInk", "Rimfolk_Evt_RoadGang", "Rimfolk_Evt_SickbedLitters", "Rimfolk_Evt_SickRoom", "Rimfolk_Evt_FieldSickhouse",
            "Rimfolk_Evt_DroveCrossing", "Rimfolk_Evt_ThiefTrail", "Rimfolk_Evt_SpringCalving", "Rimfolk_Evt_HardBirth", "Rimfolk_Evt_OldShrine", "Rimfolk_Evt_TwoFaiths" }),
            "eleven events: eight drawn, three fired when their moment comes");
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { src }.Concat(parts).ToArray()));
        string dir = Path.Combine(src, "Peoples", "Events3");
        string code = string.Join("\n", Directory.GetFiles(dir, "*.cs").Where(f => !f.EndsWith("PeopleEvents3Math.cs")).OrderBy(f => f).Select(File.ReadAllText));
        string everyCode = string.Join("\n", Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        var callings = new Dictionary<string, string>
        {
            ["Rimfolk_Evt_RedInk"] = "Traders", ["Rimfolk_Evt_RoadGang"] = "Traders", ["Rimfolk_Evt_SickbedLitters"] = "Healers", ["Rimfolk_Evt_SickRoom"] = "Healers",
            ["Rimfolk_Evt_FieldSickhouse"] = "Healers", ["Rimfolk_Evt_DroveCrossing"] = "Herders", ["Rimfolk_Evt_ThiefTrail"] = "Herders", ["Rimfolk_Evt_SpringCalving"] = "Herders",
            ["Rimfolk_Evt_HardBirth"] = "Herders", ["Rimfolk_Evt_OldShrine"] = "Pilgrims", ["Rimfolk_Evt_TwoFaiths"] = "Pilgrims"
        };
        var fired = new Dictionary<string, string>
        {
            ["Rimfolk_Evt_SickbedLitters"] = "PeopleEvents2.Fire(EventDef, map", ["Rimfolk_Evt_ThiefTrail"] = "PeopleEvents2.Fire(EventDef, r.map",
            ["Rimfolk_Evt_HardBirth"] = "PeopleEvents2.Fire(SpringCalving.HardEvent"
        };
        var lastAnswers = new[] { "ColonyCouncil.DilemmaEffect_SendAway", "ColonyCouncil.DilemmaEffect_ThiefLetGo", "ColonyCouncil.DilemmaEffect_HardBirth" };
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
            if (fired.TryGetValue(name, out var how)) C(chance == 0f && code.Contains("\"" + name + "\"") && code.Contains(how), $"{name}: never drawn, fired only when its moment comes");
            else
                C(chance >= 0.3f && chance <= 0.8f && int.Parse(e.Element("minRefireDays")!.Value) >= 30 && int.Parse(e.Element("earliestDay")!.Value) >= 10,
                    $"{name}: a small, spread chance, not soon again, not the first moment it is free");
            C(ext.Element("group")?.Value == "Visitors", $"{name}: among the visitors, so the storytellers' lean on visitors reads it");
            var texts = ext.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
            C(texts.Count >= 5 && texts.Any(t => Regex.IsMatch(t, @"^\[[^\]]*(lowtech|age\d|litters|cart|herbs|masks|ride)")), $"{name}: five or more texts, in the forms of the age");
            var options = ext.Element("options")!.Elements("li").ToList();
            C(options.Count == 3 && ext.Element("defaultOption")?.Value == "2", $"{name}: three answers, no answer the last");
            C(options[2].Descendants("li").Any(li => lastAnswers.Contains(li.Attribute("Class")?.Value)), $"{name}: the last answer sends them on (or lets the herders see it through)");
            var lineTags = ext.Element("lines")?.Elements("li").Select(l => Regex.Match(l.Value, @"^\[([^\],]+)").Groups[1].Value).Distinct().ToList() ?? new List<string>();
            C(lineTags.All(t => code.Contains("\"" + t + "\"")), $"{name}: every line's moment is one the code says it at");
            C(ext.Element("lines")?.Elements("li").All(l => Regex.Matches(Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", ""), @"[.?](\s|$)").Count == 1) ?? true, $"{name}: every spoken line one sentence");
        }
        // The rolls on the card: the argument, the birth, the table.
        bool Risky(string ev, int option, string skill, float risk) =>
            Ext(events[ev], "HappeningExtension")!.Element("options")!.Elements("li").ElementAt(option) is XElement o && F(o.Element("riskChance")?.Value ?? "0") == risk
            && (skill == null ? o.Element("riskSkill") == null : o.Element("riskSkill")?.Value == skill) && o.Element("riskReplaces")?.Value == "true" && o.Element("riskEffects") != null;
        C(Risky("Rimfolk_Evt_RedInk", 1, "Social", PeopleEvents3Math.ArgueRisk), "red ink: the talker's argument, a Social roll on the card: struck out, or written twice");
        C(Risky("Rimfolk_Evt_HardBirth", 0, "Animals", PeopleEvents3Math.HardRisk) && Risky("Rimfolk_Evt_HardBirth", 1, "Animals", PeopleEvents3Math.HardRisk)
          && Risky("Rimfolk_Evt_HardBirth", 2, null, PeopleEvents3Math.HerdersRisk), "the hard birth: the hand's way with beasts on the card; both live, or the mother lost");
        C(Risky("Rimfolk_Evt_TwoFaiths", 0, "Social", PeopleEvents3Math.SplitRisk), "two faiths: the speakers' Social on the card; settled, or the split comes anyway");
        C(Ext(events["Rimfolk_Evt_SpringCalving"], "HappeningExtension")!.Element("seasons")!.Elements("li").Select(l => l.Value).SequenceEqual(new[] { "Spring" }), "the calving only in spring");

        // The camps.
        var campKinds = XDocument.Load(Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKinds_PeopleEvents3.xml")).Root!.Elements("ColonyCouncil.CampKindDef").ToDictionary(Dn);
        C(campKinds.Keys.SequenceEqual(new[] { "Rimfolk_Camp_RoadGang", "Rimfolk_Camp_FieldSickhouse", "Rimfolk_Camp_SpringCalving" }), "three camps of their own");
        foreach (var (name, k) in campKinds)
        {
            string worker = k.Element("workerClass")!.Value.Replace("ColonyCouncil.", "");
            C(F(k.Element("weight")!.Value) == 0f && code.Contains("class " + worker + " : CampWorker"), $"{name}: never on the camps' own draw; ways of its own ({worker})");
            C(k.Descendants("li").Where(li => li.Parent?.Name.LocalName is "lines" or "momentLines").All(l => Regex.Matches(Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", ""), @"[.?](\s|$)").Count == 1),
                $"{name}: every spoken line one sentence");
            var moments = k.Element("momentLines")?.Elements("li").Select(l => Regex.Match(l.Value, @"^\[([^\]]+)\]").Groups[1].Value).Distinct().ToList() ?? new List<string>();
            C(moments.All(t => code.Contains("\"" + t + "\"") || t == "herd" || t == "round" || t == "gift"), $"{name}: every moment line is said at a moment the code or the camps have");
        }
        var gang = campKinds["Rimfolk_Camp_RoadGang"];
        C(gang.Element("calling")!.Value == "traders" && gang.Element("days")!.Value == "8~12" && gang.Element("situationKinds")!.Elements("li").Any(l => l.Value == "road")
          && gang.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).SequenceEqual(new[] { "food", "builder", "spur", "spurStone" }),
            "the road gang: eight to twelve days, with the new road; buys food, hires the builder, a spur for silver or stone");
        var sick = campKinds["Rimfolk_Camp_FieldSickhouse"];
        C(sick.Element("calling")!.Value == "healers" && sick.Element("sleep")!.Value == "Hut" && sick.Element("leaves")!.Value == "Standing"
          && sick.Element("structures")!.Elements("li").Any(s => s.Element("role")?.Value == "light")
          && sick.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).SequenceEqual(new[] { "medicine", "food", "cloth", "ward", "ours" }),
            "the field sickhouse: a long hut that stays, the lamp at its door; medicine, food, cloth, the doctor's day, the colony's sick taken in");
        var calving = campKinds["Rimfolk_Camp_SpringCalving"];
        C(calving.Element("calling")!.Value == "herders" && calving.Element("days")!.Value == "8~12" && calving.Element("seasons")!.Elements("li").Single().Value == "Spring"
          && calving.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).SequenceEqual(new[] { "hay", "milk" }) && calving.Element("asks")!.Elements("li").First().Element("theyAsk")?.Value == "true",
            "the calving: spring, eight to twelve days; they ask for hay and offer milk");

        // Their jobs, the shrine and its memory.
        var misc = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_PeopleEvents3_Misc.xml")).Root!.Elements().ToList();
        var jobs = misc.Where(e => e.Name.LocalName == "JobDef").ToList();
        C(jobs.Count == 9 && jobs.All(j => code.Contains("class " + j.Element("driverClass")!.Value.Replace("ColonyCouncil.", "") + " : JobDriver")
                                          && (code.Contains("\"" + Dn(j) + "\"") || misc.Any(g => g.Element("jobDef")?.Value == Dn(j)))),
            "nine jobs, each driven by the round's own code and given by it (or by the shrine's joy)");
        var shrine = misc.Single(e => Dn(e) == "Rimfolk_OldShrine");
        C(shrine.Element("graphicData")!.Element("texPath")!.Value == "ColonyCouncil/Things/OldShrine" && File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Things", "OldShrine.png"))
          && shrine.Element("hasInteractionCell")?.Value == "true" && shrine.Element("designationCategory") == null, "the old shrine, drawn, a place to kneel at, never built by hand");
        C(misc.Any(e => e.Name.LocalName == "JoyGiverDef" && e.Element("thingDefs")!.Elements("li").Any(l => l.Value == "Rimfolk_OldShrine"))
          && F(misc.Single(e => Dn(e) == "Rimfolk_Thought_PrayedAtShrine").Descendants("baseMoodEffect").Single().Value) > 0f, "the faithful pray there, and feel it");

        // The player's mods, drawn on softly and from their own catalogue.
        var active = Catalogue(root).Where(r => r.active).Select(r => r.type + ":" + r.def).ToHashSet();
        C(new[] { 0, 1, 2, 3 }.All(a => active.Contains("TerrainDef:" + PeopleEvents3Math.RoadFloor(a, "Granite"))) && new[] { "Sandstone", "Limestone", "Slate", "Marble" }.All(s => active.Contains("TerrainDef:Flagstone" + s)),
            "the road's floors are the game's own");
        C(PeopleEvents3Math.DroveKinds.Count(k => active.Contains("PawnKindDef:" + k)) == PeopleEvents3Math.DroveKinds.Length
          && PeopleEvents3Math.DroveKinds.Contains("AEXP_Camel") && PeopleEvents3Math.DroveKinds.Contains("AA_Thunderox") && PeopleEvents3Math.DroveKinds.Contains("Aurochs"),
            "the drove's beasts by name, the game's and Vanilla Animals Expanded's, Alpha Animals' and Megafauna's, all in the player's mods");
        C(PeopleEvents3Math.CalvingKinds.All(k => active.Contains("PawnKindDef:" + k)), "the calving beasts by name are in the player's mods");
        C(active.Contains("MeditationFocusDef:Dignified") && active.Contains("ThingDef:TorchLamp") && active.Contains("ThingDef:Bedroll") && active.Contains("SitePartDef:BanditCamp"),
            "the shrine's focus, the sickhouse's lamp and beds, the thieves' camp are the game's own");
        string wsDefs = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Defs", "WorldSituationDefs"), "*.xml").Select(File.ReadAllText));
        C(wsDefs.Contains("<defName>Council_WS_NewRoad</defName>") && code.Contains("\"Council_WS_NewRoad\"") && wsDefs.Contains("<defName>Council_WS_Plague</defName>"),
            "the world events they come with: a new road, a plague year or a war");
        C(code.Contains("Rimfolk_League_Factor") && code.Contains("Rimfolk_League_Master") && code.Contains("Rimfolk_League_Guard") && code.Contains("Rimfolk_League_Wayfarer")
          && code.Contains("Rimfolk_Order_Healer") && code.Contains("Rimfolk_Herders_Drover") && code.Contains("Rimfolk_Herders_Warden") && code.Contains("Rimfolk_Herders_Elder")
          && code.Contains("Rimfolk_Pilgrims_Keeper") && code.Contains("Rimfolk_Pilgrims_Pilgrim") && code.Contains("Rimfolk_QuietBell_Sexton"), "each people's own kind of person comes");

        // ================= Their words =================

        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeopleEvents3.xml")).Root!.Elements().Select(e => e.Name.LocalName).ToHashSet();
        var used = Regex.Matches(code, "\"(CC_PEv3_\\w+)\"").Select(mm => mm.Groups[1].Value).Where(k => !k.EndsWith("_")).Distinct().ToList();
        C(used.Count >= 150 && used.All(keyed.Contains), $"every word the code says is keyed ({used.Count}{(used.Any(u => !keyed.Contains(u)) ? ", missing " + string.Join(", ", used.Where(u => !keyed.Contains(u))) : "")})");
        C(Enumerable.Range(0, 4).All(a => keyed.Contains("CC_PEv3_Floor_" + a)) && PeopleEvents3Math.HolyKinds.All(h => keyed.Contains("CC_PEv3_ShrineBlessed_" + h)), "each floor and each holy place, with words");
        var texts3 = new[] { Path.Combine("Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents3.xml"), Path.Combine("Defs", "CampDefs", "Rimfolk_CampKinds_PeopleEvents3.xml"),
                Path.Combine("Languages", "English", "Keyed", "Rimfolk_PeopleEvents3.xml"), Path.Combine("Defs", "Misc", "Rimfolk_PeopleEvents3_Misc.xml") }
            .Select(x => Regex.Replace(File.ReadAllText(Path.Combine(root, x)), "<!--.*?-->|<\\?xml[^>]*>|\\[[^\\]]*\\]", "", RegexOptions.Singleline)).Aggregate((x, y) => x + y);
        C(!texts3.Contains('—') && !texts3.Contains('!') && !Regex.IsMatch(texts3, @"\bin \d+ (days?|hours?)\b"), "plain words, no countdowns");
        C(!Regex.IsMatch(texts3.ToLowerInvariant(), @"\b(dynasty|dynasties|bloodline|heir to the house)\b"), "the colony is the family: no houses");
        C(!Regex.IsMatch(texts3, @"\b(she|her|hers)\b"), "nobody of theirs is given a sex the game didn't");
        C(!Regex.IsMatch(texts3, @"\b(god|gods|goddess)\b", RegexOptions.IgnoreCase), "the gods unseen: no god named");
        string Alm(string f) => File.ReadAllText(Path.Combine(root, "Defs", "AlmanacArticleDefs", f));
        string peoples = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Defs", "AlmanacArticleDefs"), "Almanac_Peoples*.xml").Select(File.ReadAllText)).ToLowerInvariant();
        C(peoples.Contains("red ink") && peoples.Contains("road gang") && peoples.Contains("sick room") && peoples.Contains("field sickhouse") && peoples.Contains("litters")
          && peoples.Contains("drove") && peoples.Contains("thieves' trail") && peoples.Contains("spring calving") && peoples.Contains("old shrine") && peoples.Contains("one table"),
            "each people's Almanac page says what it does now");

        // ================= The seams =================

        C(Read("Peoples", "Events2", "PeopleEvents2.cs").Contains("return PeopleEvents3.Hour(v, comp, now);"), "round 3's visits wait on round 1's hour, through round 2's");
        C(Read("Happenings", "Happenings.cs").Contains("PeopleEvents3.Hourly(now)"), "the homes' hour watches for the raid's end, the theft and the shrines' seasons");
        C(Read("Ailments", "AilmentTracker.cs").Contains("* SickRoom.SpreadFactor(p)"), "the sickness spreads at a third of its rate while the sick room stands");
        C(Read("Peoples", "Events", "PeopleTrust.cs").Contains("float red = LeagueRedInk.Terms(f);"), "red ink's worse terms on the league's own price patch");
        C(Read("Dilemmas", "DilemmaWorkers_Faith.cs").Contains("|| TwoFaiths.Settled") && Read("Interplay", "Interplay_Gatherings.cs").Contains("TwoFaiths.GatheringOver(play, attended, early);"),
            "the faiths' clash rests while the blessing holds; the table's verdict at the evening's end");
        C(Read("Text", "Facts", "FactsMath.cs").Contains("\"them:camp:RoadGang\", \"them:camp:FieldSickhouse\", \"them:camp:SpringCalving\""), "the facts service knows the round's camps");
        C(code.Contains("CampRaidMath.AttackTick(") && code.Contains("CampAnswers.Carry(") && code.Contains("Hediff_Pregnant.DoBirthSpawn(") && code.Contains("JoyUtility.JoyTickCheckEnd(")
          && code.Contains("SiteMaker.MakeSite(SitePartDefOf.BanditCamp") && code.Contains("new CaravanArrivalAction_VisitSite(site)") && code.Contains("VisitorServices.Dose("),
            "bands against a camp, the camps' carrying, the game's own births, joy, bandit camp and caravans, and the visitors' dose, reused");
        var harms = Regex.Matches(code, @"Harm\.(Roll|Ail|Injure)\(([^,;]*,\s*)?HarmKinds\.(\w+)").Select(mm => mm.Groups[3].Value).Distinct().OrderBy(x => x).ToList();
        C(harms.SequenceEqual(new[] { "CampFever", "CampLeft", "DroversBull", "Physician" }) && !Regex.IsMatch(code, @"TakeDamage"),
            "the only harm of their own through the harm budget: the sister's and doctor's catching, the sickness crossing, the drove's kick, the afterbirth's predator; the fights the game's own");
        C(code.Contains("IllnessBudget.Allows(") && code.Contains("IllnessBudget.Owe("), "every sickness caught through the illness budget");
        C(Regex.Matches(code, @"\[HarmonyPatch\(").Count == 1 && code.Contains("if (!RimfolkPatches.On || __instance == null) return;"), "one game hook, behind the deep-hooks setting");
        C(!code.Contains("MoteMaker.ThrowText") && code.Contains("PeoplesG.SayLine(") && code.Contains("c.SayMoment(") && code.Contains("Speech.Say("), "they speak through the events', the camps' and Rimfolk's own lines");
        C(!Regex.IsMatch(code, @"Sched\.|\bLocomotionUrgency\.Sprint"), "no clock of their own: the events' hour and the camps' hour");
        C(code.Contains("StoryDirection.Factor(StoryWaysMath.Visitors)") && Regex.Matches(code, @"StoryDirection\.Factor\(StoryWaysMath\.Visitors\)").Count >= 3,
            "the code-fired comings read the storytellers' lean on visitors");
        string book = File.ReadAllText(Path.Combine(dir, "PeopleEvents3Book.cs"));
        C(book.Contains("PeopleEvents3Math.MaxRedInk") && book.Contains("PeopleEvents3Math.MaxShrines") && book.Contains("PeopleEvents3Math.MaxThefts"), "what they keep is bounded");
        C(Regex.Matches(code, @"\[DebugAction\(""Rimfolk"", ""Peoples' events 3: ").Count == 9, "nine dev actions");
        string save = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(save.Contains("Peoples/Events3/PeopleEvents3Book.cs:thefts") && save.Contains("Peoples/Events3/RoadGang.cs:road"), "their saved lists are in the save model");
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
