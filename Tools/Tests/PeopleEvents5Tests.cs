using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The peoples' own events, round 5 (Peoples/Events5/; docs/peoples-events-proposals.md, items 35, 36, 37, 38, 39, 41, 42, 43,
/// 44, 45 and 46): their pure rules (who the risen are and the promise to the Bell, the mourners' season and their truce, the
/// grave-garden's ground and work, the field of the fallen's graves and turns, the bond's afternoon, the egg's tending and
/// the tamer's return, the ring's bouts and its champion, the night's mending and its pay, a piece made better, the wagon of
/// the age, the yard's coming and its turns), their defs and words, the player's mods they draw on, and the seams with the
/// game and rounds 1 to 4.
/// </summary>
public static class PeopleEvents5Tests
{
    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the peoples' events, round 5: " + what);
        }
        float F(string v) => float.Parse(v, CultureInfo.InvariantCulture);
        const int Day = PeopleEvents5Math.DayTicks;

        // ================= 35. The field that stirs =================

        C(PeopleEvents5Math.Risen(true, true) && !PeopleEvents5Math.Risen(false, true) && !PeopleEvents5Math.Risen(true, false), "the risen are bodies that lay on the colony's land");
        C(PeopleEvents5Math.StirWardens == 3 && PeopleEvents5Math.StirChance <= 0.5f && PeopleEvents5Math.StirGapDays >= 10 && PeopleEvents5Math.StirGraves >= 6,
            "three gravewardens, at a chance, not soon again, a grave each");
        C(!PeopleEvents5Math.PromiseBroken(-1, 5) && PeopleEvents5Math.PromiseBroken(0, 59 * Day) && !PeopleEvents5Math.PromiseBroken(0, 61 * Day)
          && PeopleEvents5Math.PromiseGoodwill > 0 && PeopleEvents5Math.PromiseBrokenGoodwill < 0 && PeopleEvents5Math.StirGoodwill > 0,
            "the promise to bury the dead holds a year; broken, the Bell thinks less of the colony");

        // ================= 36. Mourners from the other side =================

        C(PeopleEvents5Math.MournAfterDays == 15 && PeopleEvents5Math.MournHours == 1 && PeopleEvents5Math.MournersCount == 2 && PeopleEvents5Math.MournExpireDays >= 15,
            "two mourners a season after the burial, an hour at the graves");
        C(PeopleEvents5Math.MournWithGoodwill > PeopleEvents5Math.MournGoodwill && PeopleEvents5Math.MournGoodwill > 0 && PeopleEvents5Math.MournTurnedBellGoodwill < 0
          && PeopleEvents5Math.TruceChance(true) == 1f && PeopleEvents5Math.TruceChance(false) == 0.5f,
            "their people warm, more with one of the colony beside them; a truce talked of; turned away, the Bell saddened");

        // ================= 37. A grave-garden for the colony =================

        C(PeopleEvents5Math.GardenMinGraves == 3 && PeopleEvents5Math.GardenRadius(0f) == 6f && PeopleEvents5Math.GardenRadius(8f) == 10f && PeopleEvents5Math.GardenRadius(20f) == 14f
          && PeopleEvents5Math.GardenRadius(float.NaN) == 6f, "three graves together at least; the garden's ground reaches its farthest grave");
        C(PeopleEvents5Math.GardenDays == 3 && PeopleEvents5Math.MaxStones == 30 && PeopleEvents5Math.MaxRoses == 10 && !PeopleEvents5Math.WorkHour(6) && PeopleEvents5Math.WorkHour(7)
          && PeopleEvents5Math.WorkHour(19) && !PeopleEvents5Math.WorkHour(20) && PeopleEvents5Math.GardenGoodwill > PeopleEvents5Math.PostGoodwill,
            "three days' work by day, stones and roses bounded; the whole garden warmer than the post alone");

        // ================= 38. The field of the fallen =================

        C(PeopleEvents5Math.FallenMin == 12 && PeopleEvents5Math.FallenGraves >= PeopleEvents5Math.FallenMin && PeopleEvents5Math.Dusk(18) && !PeopleEvents5Math.Dusk(17) && !PeopleEvents5Math.Dusk(19),
            "a dozen dead or more; the bell at dusk");
        C(PeopleEvents5Math.FallenGoodwill(0) == 3 && PeopleEvents5Math.FallenGoodwill(3) == 5 && PeopleEvents5Math.FallenGoodwill(20) == 8 && PeopleEvents5Math.FallenPeoples == 3
          && PeopleEvents5Math.MournersDay >= 1f, "each of up to three peoples thinks better of the colony, by its dead");
        C(PeopleEvents5Math.FieldStirChance <= 0.15f && PeopleEvents5Math.ClaimChance <= 0.25f && Math.Abs(PeopleEvents5Math.ClaimTalkFails(0) - 0.75f) < 1e-4f
          && Math.Abs(PeopleEvents5Math.ClaimTalkFails(10) - 0.4f) < 1e-4f && PeopleEvents5Math.ClaimTalkFails(30) == 0.05f && PeopleEvents5Math.ClaimRefusedGoodwill < 0,
            "it turns rarely, once a stay each; talked out of it by the talker's Social");
        C(PeopleEvents5Math.FallenWood == 40 && PeopleEvents5Math.FallenCloth == 20, "wood and cloth for the graves");

        // ================= 39. The bond freely given =================

        C(PeopleEvents5Math.BondTicks >= 7500 && PeopleEvents5Math.BondXp > PeopleEvents5Math.WordsXp && PeopleEvents5Math.BondGoodwill > 0, "an afternoon's work; a lesson more than a few pointers");

        // ================= 41. An egg from the crater =================

        C(PeopleEvents5Math.Most(new[] { 0, 3, 2 }) == 1 && PeopleEvents5Math.Most(new[] { 0, 0 }) == -1 && PeopleEvents5Math.Most(null) == -1 && PeopleEvents5Math.Most(new[] { 2, 2 }) == 0,
            "bonded to who tended it most, else its keeper");
        C(PeopleEvents5Math.GrownAfterDays == 15 && PeopleEvents5Math.TendCells >= 2f && PeopleEvents5Math.EggForgetDays >= 60 && PeopleEvents5Math.GrownGoodwill > PeopleEvents5Math.GrownWordsGoodwill,
            "the tamer back a season after the hatching");

        // ================= 42. The Circuit's ring =================

        C(!PeopleEvents5Math.BoutHour(12) && PeopleEvents5Math.BoutHour(13) && PeopleEvents5Math.BoutHour(17) && !PeopleEvents5Math.BoutHour(18) && PeopleEvents5Math.ChallengesPerDay == 2
          && !PeopleEvents5Math.Champion(0) && PeopleEvents5Math.Champion(1) && PeopleEvents5Math.ChampionGoodwill > PeopleEvents5Math.RingGoodwill,
            "a bout each afternoon, two challengers a day, the champion with one win at least");

        // ================= 43. Mend it tonight =================

        C(!PeopleEvents5Math.MendHour(18) && PeopleEvents5Math.MendHour(19) && PeopleEvents5Math.MendHour(23) && !PeopleEvents5Math.MendHour(0) && PeopleEvents5Math.AfterRaid(0, Day)
          && !PeopleEvents5Math.AfterRaid(0, Day + 1) && !PeopleEvents5Math.AfterRaid(-1, 5), "after dark on the night after a raid");
        C(PeopleEvents5Math.MendSilver(0) == 40 && PeopleEvents5Math.MendSilver(2000) == 130 && PeopleEvents5Math.MendSilver(10000) == 250 && PeopleEvents5Math.MendSilverOnly(2000, 500f) == 330
          && PeopleEvents5Math.MendSilverOnly(0, float.NaN) == 60, "a little silver by the damage; more in silver alone, the gear kept");
        C(PeopleEvents5Math.MendTicks(0) == 300 && PeopleEvents5Math.MendTicks(100) == 600 && PeopleEvents5Math.MendTicks(1000) == 2500 && PeopleEvents5Math.Broken(50, 100, false)
          && !PeopleEvents5Math.Broken(70, 100, false) && PeopleEvents5Math.Broken(90, 100, true) && PeopleEvents5Math.MendCrew == 3 && PeopleEvents5Math.MinDamaged == 3,
            "each piece by its damage; the raiders' gear broken or tainted; a crew of three");

        // ================= 44. Made better =================

        C(PeopleEvents5Math.Betterable(4) && !PeopleEvents5Math.Betterable(5) && !PeopleEvents5Math.Betterable(-1) && PeopleEvents5Math.Better(2) == 3 && PeopleEvents5Math.Better(5) == 5,
            "a quality higher, masterwork at most");
        C(PeopleEvents5Math.BetterSilver(100f) == 80 && PeopleEvents5Math.BetterSilver(1000f) == 300 && PeopleEvents5Math.BetterSilver(5000f) == 800 && PeopleEvents5Math.BetterSteel(1000f) == 50
          && PeopleEvents5Math.BetterSilverOnly(1000f) == 450 && PeopleEvents5Math.BetterHours == 24 && PeopleEvents5Math.MinArmour > 0f,
            "silver and steel by its worth, or half again in silver; a day at the cart");
        foreach (float w in new[] { 0f, 100f, 777f, 1000f, 2400f, 9000f })
            C(PeopleEvents5Math.BetterSilverOnly(w) == Math.Max(10, (int)Math.Round(PeopleEvents5Math.BetterSilver(w) * 1.5f / 10f) * 10),
                $"the silver alone is what the carry counts at half again (worth {w})");

        // ================= 45. A wagon of our own =================

        C(PeopleEvents5Math.WagonDefs(0).SequenceEqual(new[] { "VVE_Wagon" }) && PeopleEvents5Math.WagonDefs(1).SequenceEqual(new[] { "VVE_Wagon" })
          && PeopleEvents5Math.WagonDefs(2)[0] == "VVE_Mule" && PeopleEvents5Math.WagonDefs(3)[0] == "VVE_Mule", "the wagon before the industrial age, a small truck after");
        C(PeopleEvents5Math.WagonGoods(1).SequenceEqual(new[] { ("WoodLog", 120), ("Steel", 30) }) && PeopleEvents5Math.WagonGoods(2).SequenceEqual(new[] { ("Steel", 150), ("ComponentIndustrial", 5) })
          && PeopleEvents5Math.WagonFee(0) < PeopleEvents5Math.WagonFee(2) && PeopleEvents5Math.WagonHours == 24, "made from the colony's goods of its age, over a day");

        // ================= 46. The Wheel stops by =================

        C(PeopleEvents5Math.Near(6f) && !PeopleEvents5Math.Near(6.5f) && !PeopleEvents5Math.Near(float.NaN) && !PeopleEvents5Math.Near(-1f) && PeopleEvents5Math.YardMinHours < PeopleEvents5Math.YardMaxHours,
            "a town moved within a few tiles brings its yard within the day");
        C(PeopleEvents5Math.YardMendPrice(100f, false) == 30 && PeopleEvents5Math.YardMendPrice(100f, true) == 35 && PeopleEvents5Math.YardMendPrice(0f, false) == 25
          && PeopleEvents5Math.StayLongerDays == 2 && PeopleEvents5Math.QuarrelChance <= 0.3f && PeopleEvents5Math.QuarrelTension > 0f,
            "the mending at a third of the wear, a tenth more after an argument; two more days when they stay on");

        // ================= Their defs =================

        string defsPath = Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents5.xml");
        var events = XDocument.Load(defsPath).Root!.Elements("IncidentDef").ToDictionary(Dn);
        C(events.Keys.SequenceEqual(new[] { "Rimfolk_Evt_FieldStirs", "Rimfolk_Evt_OtherSideMourners", "Rimfolk_Evt_GraveGarden", "Rimfolk_Evt_FieldOfFallen", "Rimfolk_Evt_FallenClaimed",
            "Rimfolk_Evt_BondGiven", "Rimfolk_Evt_CraterEgg", "Rimfolk_Evt_CraterEggGrown", "Rimfolk_Evt_CircuitRing", "Rimfolk_Evt_MendTonight", "Rimfolk_Evt_MadeBetter",
            "Rimfolk_Evt_WagonOfOurOwn", "Rimfolk_Evt_WheelStopsBy" }), "thirteen events: seven drawn, six fired when their moment comes");
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { src }.Concat(parts).ToArray()));
        string dir = Path.Combine(src, "Peoples", "Events5");
        string code = string.Join("\n", Directory.GetFiles(dir, "*.cs").Where(f => !f.EndsWith("PeopleEvents5Math.cs")).OrderBy(f => f).Select(File.ReadAllText));
        string everyCode = string.Join("\n", Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        var callings = new Dictionary<string, string>
        {
            ["Rimfolk_Evt_FieldStirs"] = "Sextons", ["Rimfolk_Evt_OtherSideMourners"] = "Sextons", ["Rimfolk_Evt_GraveGarden"] = "Sextons", ["Rimfolk_Evt_FieldOfFallen"] = "Sextons",
            ["Rimfolk_Evt_BondGiven"] = "Tamers", ["Rimfolk_Evt_CraterEgg"] = "Tamers", ["Rimfolk_Evt_CraterEggGrown"] = "Tamers", ["Rimfolk_Evt_CircuitRing"] = "Tamers",
            ["Rimfolk_Evt_MendTonight"] = "Salvagers", ["Rimfolk_Evt_MadeBetter"] = "Salvagers", ["Rimfolk_Evt_WagonOfOurOwn"] = "Salvagers"
        };
        // The two whose people are their own record's: the yard of the town that moved (the Wheel's, at peace), and the dead's own people at the field.
        C(code.Contains("PeoplesD.Theirs(r.faction, PeoplesMath.Salvagers)") && code.Contains("!st.claimPeople.HostileTo(Faction.OfPlayer)"),
            "the yard only from a Wheel at peace; the dead's people ask at the gate only at peace with the colony");
        var fired = new Dictionary<string, string>
        {
            ["Rimfolk_Evt_FieldStirs"] = "PeopleEvents2.Fire(EventDef, map, f)", ["Rimfolk_Evt_OtherSideMourners"] = "PeopleEvents2.Fire(EventDef, map, Escort(r.people))",
            ["Rimfolk_Evt_FallenClaimed"] = "PeopleEvents2.Fire(ClaimEvent, c.map, f)", ["Rimfolk_Evt_CraterEggGrown"] = "PeopleEvents2.Fire(GrownEvent, home, f)",
            ["Rimfolk_Evt_MendTonight"] = "PeopleEvents2.Fire(EventDef, map, f)", ["Rimfolk_Evt_WheelStopsBy"] = "PeopleEvents2.Fire(EventDef, r.map, r.faction)"
        };
        var defaults = new Dictionary<string, string> { ["Rimfolk_Evt_FieldStirs"] = "0" };
        var lastAnswers = new[]
        {
            "ColonyCouncil.DilemmaEffect_SendAway", "ColonyCouncil.DilemmaEffect_FieldStirs", "ColonyCouncil.DilemmaEffect_MournersTurned", "ColonyCouncil.DilemmaEffect_FallenClaim"
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
            C(texts.Count >= 5 && texts.Any(t => Regex.IsMatch(t, @"^\[[^\]]*(lowtech|tribal|medieval|industrial|spacer|age\d)")), $"{name}: five or more texts, in the forms of the age");
            var options = ext.Element("options")!.Elements("li").ToList();
            C(options.Count == 3 && ext.Element("defaultOption")?.Value == (defaults.TryGetValue(name, out var d) ? d : "2"), $"{name}: three answers, no answer the last (or as its note says)");
            C(options[2].Descendants("li").Any(li => lastAnswers.Contains(li.Attribute("Class")?.Value)), $"{name}: the last answer sends them on");
            var lineTags = ext.Element("lines")?.Elements("li").Select(l => Regex.Match(l.Value, @"^\[([^\],]+)").Groups[1].Value).Distinct().ToList() ?? new List<string>();
            C(lineTags.Count > 0 && lineTags.All(t => code.Contains("\"" + t + "\"")), $"{name}: every line's moment is one the code says it at");
            C(ext.Element("lines")?.Elements("li").All(l => Regex.Matches(Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", ""), @"[.?](\s|$)").Count == 1) ?? true, $"{name}: every spoken line one sentence");
        }
        C(Ext(events["Rimfolk_Evt_FieldStirs"], "HappeningExtension")!.Element("mods")!.Elements("li").Any(l => l.Value == "Ludeon.RimWorld.Anomaly") && code.Contains("ModsConfig.AnomalyActive"),
            "the field stirs only with Anomaly");
        C(Ext(events["Rimfolk_Evt_WagonOfOurOwn"], "HappeningExtension")!.Element("mods")!.Elements("li").Any(l => l.Value == "OskarPotocki.VanillaVehiclesExpanded")
          && code.Contains("WagonOfOurOwn.Vehicle(PeopleEvents2.ColonyAge) != null"), "the wagon only with Vehicle Framework and Vanilla Vehicles Expanded");
        C(Regex.IsMatch(code, @"override float RiskChance\(HappeningContext ctx, int option, float chance\) =>\s*option == 1 \? PeopleEvents5Math\.ClaimTalkFails\("),
            "the talk at the field's gate goes by the rule's own chance, on the card");
        var better = Ext(events["Rimfolk_Evt_MadeBetter"], "HappeningExtension")!.Element("options")!.Elements("li").ToList();
        C(better[1].Descendants("factor").Single().Value == "1.5" && better[1].Descendants("tens").Single().Value == "true" && better.Take(2).All(o => o.Descendants("steward").All(s => s.Value == "false")),
            "the piece paid in silver alone at half again, as the letter says");

        // The camps.
        var campKinds = XDocument.Load(Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKinds_PeopleEvents5.xml")).Root!.Elements("ColonyCouncil.CampKindDef").ToDictionary(Dn);
        C(campKinds.Keys.SequenceEqual(new[] { "Rimfolk_Camp_FieldOfFallen", "Rimfolk_Camp_CircuitRing", "Rimfolk_Camp_WheelYard" }), "three camps of their own");
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
        }
        var fallen = campKinds["Rimfolk_Camp_FieldOfFallen"];
        C(fallen.Element("calling")!.Value == "sextons" && fallen.Element("days")!.Value == "3~6" && fallen.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).SequenceEqual(new[] { "wood", "cloth" })
          && fallen.Descendants("count").Any(c => c.Value == PeopleEvents5Math.FallenWood.ToString()) && fallen.Descendants("count").Any(c => c.Value == PeopleEvents5Math.FallenCloth.ToString()),
            "the field of the fallen: three to six days; wood and cloth asked for");
        var ring = campKinds["Rimfolk_Camp_CircuitRing"];
        C(ring.Element("calling")!.Value == "tamers" && ring.Element("people")!.Value == "6~10" && ring.Element("days")!.Value == "4~6"
          && ring.Element("situationKinds")!.Elements("li").Any(l => l.Value == "fair") && ring.Element("animals")!.Elements("li").Any(a => a.Element("pokemon")?.Value == "true")
          && ring.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).SequenceEqual(new[] { "challenge", "feed" }), "the ring: six to ten tamers, four to six days, with the fair; their animals, Pokémon with PokéWorld; a challenge and feed");
        var yard = campKinds["Rimfolk_Camp_WheelYard"];
        C(yard.Element("calling")!.Value == "salvagers" && yard.Element("days")!.Value == "3~3" && yard.Element("trader")?.Value == "true" && yard.Element("leaves")?.Value == "NoTrace"
          && yard.Element("structures")!.Elements("li").Any(s => s.Element("role")?.Value == "craft") && yard.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).SequenceEqual(new[] { "mend", "scrap", "wood" }),
            "the yard: three days, a trader (the scrap market), a mending tent, nothing left behind; mending, scrap and wood");

        // Their jobs, the bell-post, the token and the moods.
        var misc = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_PeopleEvents5_Misc.xml")).Root!.Elements().ToList();
        var jobs = misc.Where(e => e.Name.LocalName == "JobDef").ToList();
        C(jobs.Count == 13 && jobs.All(j => everyCode.Contains("class " + j.Element("driverClass")!.Value.Replace("ColonyCouncil.", "") + " : JobDriver") && code.Contains("\"" + Dn(j) + "\"")),
            "thirteen jobs, each driven by a driver of the round or round 1's and given by the round's code");
        var work = jobs.Where(j => j.Element("driverClass")!.Value == "ColonyCouncil.JobDriver_PEv5Work").ToList();
        var workOn = jobs.Where(j => j.Element("driverClass")!.Value == "ColonyCouncil.JobDriver_PeopleWorkOn").ToList();
        C(work.All(j => new[] { "dust", "dig", "sparks", "quiet" }.Contains(j.Descendants("mark").Single().Value)) && workOn.Count == 4
          && workOn.All(j => Regex.IsMatch(everyCode, @"class " + j.Descendants("done").Single().Value.Replace("ColonyCouncil.", "") + @" : PeopleWorkDone")),
            "the round's own work by its marks; the beast trained, looked over and the piece handed over and back are round 1's work on someone");
        C(workOn.Any(j => j.Descendants("mark").Single().Value == "train") && Read("Peoples", "Events", "PeopleWorkOn.cs").Contains("case \"train\":"), "round 1's work has a beast's mark");
        var post = misc.Single(e => Dn(e) == "Rimfolk_BellPost");
        C(post.Element("graphicData")!.Element("texPath")!.Value == "ColonyCouncil/Things/BellPost" && File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Things", "BellPost.png"))
          && post.Element("designationCategory") == null && post.Descendants("Beauty").Single().Value != "0" && File.Exists(Path.Combine(root, "Tools", "make_bell_post.py")),
            "the bell-post drawn, never built by hand, a pleasant thing to have");
        C(misc.Any(e => e.Name.LocalName == "HediffDef" && Dn(e) == "Rimfolk_CircuitToken" && e.Element("isBad")?.Value == "false") && code.Contains("\"Rimfolk_CircuitToken\""), "the Circuit's token, no harm in it");
        var thoughts = misc.Where(e => e.Name.LocalName == "ThoughtDef").ToDictionary(Dn);
        C(thoughts.Keys.SequenceEqual(new[] { "Rimfolk_Thought_QuietGround", "Rimfolk_Thought_FullerRite", "Rimfolk_Thought_RingChampion" }) && thoughts.Keys.All(t => code.Contains("\"" + t + "\""))
          && thoughts.Values.All(t => F(t.Descendants("baseMoodEffect").Single().Value) > 0f), "quiet ground, a fuller rite, the champion's token: good memories only");

        // The player's mods, drawn on softly and from their own catalogue.
        var cat = Catalogue(root);
        var active = cat.Where(r => r.active).Select(r => r.type + ":" + r.def).ToHashSet();
        C(active.Contains("ThingDef:Plant_Rose") && new[] { "FlagstoneGranite", "FlagstoneLimestone", "FlagstoneMarble", "FlagstoneSandstone", "FlagstoneSlate" }.All(t => active.Contains("TerrainDef:" + t))
          && code.Contains("\"Plant_Rose\"") && code.Contains("\"Flagstone\" + rock.defName") && code.Contains("\"FlagstoneGranite\""), "the roses and the land's own flagstones are the game's");
        C(active.Contains("MutantDef:Shambler") && active.Contains("SoundDef:TinyBell") && code.Contains("SoundDefOf.TinyBell") && code.Contains("MutantUtility.CanResurrectAsShambler"),
            "Anomaly's shamblers, the game's small bell");
        C(active.Contains("ExpertiseDef:Trainer") && code.Contains("Expertise.Give(hand, \"Trainer\")") && Read("Integrations", "Expertise.cs").Contains("public static bool Give(Pawn p, string defName)"),
            "Vanilla Skills Expanded's Trainer expertise, given by the mod's own way");
        C(Enumerable.Range(0, 4).SelectMany(PeopleEvents5Math.WagonDefs).All(v => active.Contains("VehicleDef:" + v))
          && Enumerable.Range(0, 4).SelectMany(PeopleEvents5Math.WagonGoods).All(g => active.Contains("ThingDef:" + g.def)) && code.Contains("BoatBuilders.Vehicle")
          && Read("Peoples", "Events4", "BoatBuilders.cs").Contains("public static Thing MakeVehicle(ThingDef def, Faction f)"), "Vanilla Vehicles Expanded's wagon and truck by name, made by Vehicle Framework's own way");
        var mods = File.ReadAllLines(Path.Combine(root, "docs", "player-mods", "mods.tsv")).Skip(1).Select(l => l.Split('\t')).Where(c => c.Length > 3).ToDictionary(c => c[0].ToLowerInvariant(), c => c[3] == "yes");
        var eggs = File.ReadAllLines(Path.Combine(root, "docs", "player-mods", "items.tsv")).Skip(1).Select(l => l.Split('\t')).Where(c => c.Length > 5 && c[2] == "yes" && c[5].Contains("EggsFertilized")).ToList();
        C(PeopleEvents5Math.EggPackages.All(p => mods.TryGetValue(p, out bool on) && on && eggs.Any(e => e[1].StartsWith(p, StringComparison.OrdinalIgnoreCase))),
            "the crater eggs' mods are in the player's list, each with fertile eggs of its own");
        C(code.Contains("PeoplesD.KindNamed(ctx.faction, \"Rimfolk_QuietBell_Gravewarden\")") && code.Contains("\"Rimfolk_QuietBell_Keeper\"") && code.Contains("\"Rimfolk_QuietBell_Sexton\"")
          && code.Contains("\"Rimfolk_Tamers_Tamer\"") && code.Contains("\"Rimfolk_Wheel_Hand\"") && code.Contains("PeoplesD.TraderKind(ctx.faction)"), "each people's own kind of person comes");

        // ================= Their words =================

        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeopleEvents5.xml")).Root!.Elements().Select(e => e.Name.LocalName).ToHashSet();
        var used = Regex.Matches(code, "\"(CC_PEv5_\\w+)\"").Select(mm => mm.Groups[1].Value).Distinct().ToList();
        C(used.Count >= 90 && used.All(keyed.Contains), $"every word the code says is keyed ({used.Count}{(used.Any(u => !keyed.Contains(u)) ? ", missing " + string.Join(", ", used.Where(u => !keyed.Contains(u))) : "")})");
        C(keyed.Where(k => k.StartsWith("CC_PEv5_YardHaggleBack")).Count() >= 2 && code.Contains("TextVariants.Vary(\"CC_PEv5_YardHaggleBack\")"), "the colonist's side of the argument in more than one wording");
        var texts5 = new[] { Path.Combine("Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents5.xml"), Path.Combine("Defs", "CampDefs", "Rimfolk_CampKinds_PeopleEvents5.xml"),
                Path.Combine("Languages", "English", "Keyed", "Rimfolk_PeopleEvents5.xml"), Path.Combine("Defs", "Misc", "Rimfolk_PeopleEvents5_Misc.xml") }
            .Select(x => Regex.Replace(File.ReadAllText(Path.Combine(root, x)), "<!--.*?-->|<\\?xml[^>]*>|\\[[^\\]]*\\]", "", RegexOptions.Singleline)).Aggregate((x, y) => x + y);
        C(!texts5.Contains('—') && !texts5.Contains('!') && !Regex.IsMatch(texts5, @"\bin \d+ (days?|hours?)\b"), "plain words, no countdowns");
        C(!Regex.IsMatch(texts5.ToLowerInvariant(), @"\b(dynasty|dynasties|bloodline|heir to the house)\b"), "the colony is the family: no houses");
        C(!Regex.IsMatch(texts5, @"\b(she|her|hers)\b"), "nobody of theirs is given a sex the game didn't");
        C(!Regex.IsMatch(texts5, @"\b(god|gods|goddess)\b", RegexOptions.IgnoreCase), "the gods unseen: no god named");
        string peoples = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Defs", "AlmanacArticleDefs"), "Almanac_Peoples*.xml").Select(File.ReadAllText)).ToLowerInvariant();
        C(peoples.Contains("the field that stirs") && peoples.Contains("mourners from the other side") && peoples.Contains("a grave-garden for the colony") && peoples.Contains("the field of the fallen")
          && peoples.Contains("bond freely given") && peoples.Contains("an egg from the crater") && peoples.Contains("the circuit's ring") && peoples.Contains("night crew")
          && peoples.Contains("made better") && peoples.Contains("a wagon of the colony's own") && peoples.Contains("the wheel stops by"), "each people's Almanac page says what it does now");

        // ================= The seams =================

        string four = Read("Peoples", "Events4", "PeopleEvents4.cs");
        C(four.Contains("return PeopleEvents5.Hour(v, comp, now);") && four.Contains("PeopleEvents5.Hourly(now)"), "round 5's visits and watch wait on round 1's hour, through rounds 2 to 4");
        var kinds = Regex.Matches(code, @"public const string ((?!Gift)\w*Kind) = ""([^""]+)"";").Select(mm => mm.Groups[2].Value).ToList();
        C(kinds.Count == 5 && kinds.All(k => k.StartsWith("pev:")) && kinds.Distinct().Count() == 5 && Read("Happenings", "Happenings.cs").Contains("case var k when PeopleEvents.Owns(k)"),
            "their visits are the events' own kind (\"pev:\"), the only ones the tracker hands to round 1's hour, none twice");
        var gifts = Regex.Matches(code, @"public const string GiftKind = ""([^""]+)"";").Select(mm => mm.Groups[1].Value).ToList();
        string earlier = string.Join("\n", new[] { "Events", "Events2", "Events3", "Events4" }.SelectMany(x => Directory.GetFiles(Path.Combine(src, "Peoples", x), "*.cs")).Select(File.ReadAllText));
        C(gifts.OrderBy(x => x).SequenceEqual(new[] { "better", "bond", "egg" })
          && gifts.All(g => !Regex.IsMatch(earlier, @"GiftKind = """ + g + @"""")) && code.Contains("PeopleEventsBook.Gave(GiftKind") && code.Contains("PeopleEvents.MayGive("),
            "three gifts of their own kind, once a colonist and spaced out as every round's");
        C(Read("Peoples", "PeoplesD2.cs").Contains("if (CircuitRing.BoutDone(trainer, mon)) return;"), "the ring's bouts end by the friendly bout's own job, their rules kept at the ring");
        C(Read("Peoples", "PeoplesD.cs").Contains("if (option == 0) PeopleEvents5.Safe(\"the mourners' day\", () => OtherSideMourners.Note(ctx));"), "the Bell's burial beyond the wall marks the mourners' day");
        C(Read("Peoples", "WheelTowns.cs").Contains("WheelStopsBy.Moved(s)"), "a Wheel town's move brings its yard");
        C(Read("Text", "Facts", "FactsMath.cs").Contains("\"them:camp:FieldOfFallen\", \"them:camp:CircuitRing\", \"them:camp:WheelYard\""), "the facts service knows the round's camps");
        C(code.Contains("PeopleWorkOn.Begin(") && code.Contains("new LordJob_AssaultThings(") && code.Contains("Callers.Graves(") && code.Contains("CampRaids.Send(")
          && code.Contains("HappeningWorker_TamerBout.BoutJob") && code.Contains("VisitorServices.Mend(") && code.Contains("Truces.Offer(") && code.Contains("SetGuestStatus(")
          && code.Contains("BoatBuilders.MakeVehicle(") && code.Contains("Couriers.Loot(") && code.Contains("JobDefOf.Steal") && code.Contains("HappeningWorker_EggTrade.Egg("),
            "the shapes reused: work on someone, the game's assault, the undertakers' graves, a band against a camp, the friendly bout, the fixer's mending, a truce offered, the Bell's escort, a vehicle made, the post, the game's taking-away, the hatchery's egg");
        var harms = Regex.Matches(code, @"Harm\.(Roll|Ail|Injure)\(([^,;]*,\s*)?HarmKinds\.(\w+)").Select(mm => mm.Groups[3].Value).Distinct().OrderBy(x => x).ToList();
        C(harms.SequenceEqual(new[] { "TamerBout" }) && !Regex.IsMatch(code, @"TakeDamage") && code.Contains("IncidentWorker_Happening.BattlesAllowed(c.map)"),
            "the only harm of their own the ring's bruise, the friendly bout's own kind; the field's stirring only while Rimfolk's world events may bring a fight");
        var patches = Regex.Matches(code, @"\[HarmonyPatch\(typeof\((\w+)\), nameof\(\w+\.(\w+)\)\)\]").Select(mm => mm.Groups[1].Value + "." + mm.Groups[2].Value).ToList();
        C(patches.SequenceEqual(new[] { "MutantUtility.CanResurrectAsShambler" }) && code.Contains("if (!__result || !RimfolkPatches.On"), "one game hook (the garden's quiet), only with the deeper game hooks on");
        C(!code.Contains("MoteMaker.ThrowText") && code.Contains("PeoplesG.SayLine(") && code.Contains("c.SayMoment(") && code.Contains("PeoplesG.Say(") && code.Contains("Speech.Say("),
            "they speak through the events' and the camps' own lines");
        C(!Regex.IsMatch(code, @"Sched\.|\bLocomotionUrgency\.Sprint"), "no clock of their own: the events' hour and the camps' hour");
        C(Regex.Matches(code, @"StoryDirection\.Factor\(StoryWaysMath\.Visitors\)").Count >= 2, "the code-fired comings read the storytellers' lean on visitors");
        string book = File.ReadAllText(Path.Combine(dir, "PeopleEvents5Book.cs"));
        C(book.Contains("stirs.Count > 4") && book.Contains("PeopleEvents5Math.MaxMourns") && book.Contains("gardens.Count > 4") && book.Contains("PeopleEvents5Math.MaxEggs")
          && book.Contains("betters.Count > 4") && book.Contains("yards.Count > 4") && book.Contains("mendRaids.Count > 8") && book.Contains("Scribe_Deep.Look(ref held, \"held\", this)"),
            "what they keep is bounded; the pieces at the cart held by the record, off every map");
        C(Regex.Matches(code, @"\[DebugAction\(""Rimfolk"", ""Peoples' events 5: ").Count == 10, "ten dev actions");
        string save = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(save.Contains("Peoples/Events5/PeopleEvents5Book.cs:eggs") && save.Contains("Peoples/Events5/CircuitRing.cs:wins") && save.Contains("Peoples/Events5/FieldOfFallen.cs:dead"),
            "their saved lists are in the save model");
        string audit = File.ReadAllText(Path.Combine(root, "Tools", "playout_audit.py"));
        C(events.Values.SelectMany(e => e.Descendants("li")).Select(li => li.Attribute("Class")?.Value?.Replace("ColonyCouncil.", "")).Where(v => v != null && v.StartsWith("DilemmaEffect_")
                && Regex.IsMatch(code, @"class " + v + @"\b") && v != "DilemmaEffect_BondWords").Distinct().All(v => audit.Contains("\"" + v + "\"")),
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
