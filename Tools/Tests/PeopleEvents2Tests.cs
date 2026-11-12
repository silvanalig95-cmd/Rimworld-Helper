using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The peoples' own events, round 2 (Peoples/Events2/; docs/peoples-events-proposals.md, items 14, 22, 26, 59, 12, 25, 3,
/// 20, 40 and 63): their pure rules (the season away and its roads, the pilgrimage's days and what it eases, the guard's
/// share, the enclave's terms and instalments, the crossing's wood and the knot night, the guild's price and its unique
/// pieces by name, the dry-year camp's toll, strays and the road's losses, the pad's exact rent and its raise, the archive's
/// books and its verdict, the beast's leg by age, the news and its prices), their defs and words, the player's mods they
/// draw on, and the seams with round 1's shapes.
/// </summary>
public static class PeopleEvents2Tests
{
    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the peoples' events, round 2: " + what);
        }
        float F(string v) => float.Parse(v, CultureInfo.InvariantCulture);

        // ================= The season away =================

        C(PeopleEvents2Math.RoadDays(0f) == 0.5f && Math.Abs(PeopleEvents2Math.RoadDays(10f) - 4.5f) < 1e-4f && PeopleEvents2Math.RoadDays(float.NaN) == 0.5f && PeopleEvents2Math.RoadDays(-5f) == 0.5f,
            "a walking caravan of one: half a day and four tenths a tile");
        C(PeopleEvents2Math.SettingOut == 0 && PeopleEvents2Math.Going == 1 && PeopleEvents2Math.There == 2 && PeopleEvents2Math.Coming == 3 && PeopleEvents2Math.AwayMaxDays >= 60,
            "four stages, and long enough to wait for any of them");

        // ================= 14. A pilgrimage =================

        C(PeopleEvents2Math.ShrineInReach(14f) && !PeopleEvents2Math.ShrineInReach(14.5f) && !PeopleEvents2Math.ShrineInReach(float.NaN) && !PeopleEvents2Math.ShrineInReach(-1f),
            "a shrine in reach: both roads and two days there in thirty days");
        var shrineDays = Enumerable.Range(0, 101).Select(i => i / 100f).SelectMany(r => new[] { 0.5f, 3f, 8f, 14f }.Select(road => (road, days: PeopleEvents2Math.ShrineDays(road, r)))).ToList();
        C(shrineDays.All(x => x.days >= PeopleEvents2Math.ShrineMinDays) && shrineDays.All(x => PeopleEvents2Math.AwayDays(x.road, x.days) <= PeopleEvents2Math.PilgrimMaxDays + 0.01f)
          && shrineDays.Where(x => x.road <= 4f).All(x => PeopleEvents2Math.AwayDays(x.road, x.days) >= PeopleEvents2Math.PilgrimMinDays - 0.01f),
            "away ten to thirty days, roads and shrine together, never under two days there");
        C(PeopleEvents2Math.PilgrimMinDays == 10 && PeopleEvents2Math.PilgrimMaxDays == 30 && PeopleEvents2Math.PilgrimGoodwill == 6, "ten to thirty days, the fellowship +6");
        C(PeopleEvents2Math.PilgrimCough > 0f && PeopleEvents2Math.PilgrimCough <= 0.1f && PeopleEvents2Math.PilgrimCertainty > 0f && PeopleEvents2Math.PilgrimCertainty <= 0.25f,
            "the cough at a small rate through the budget; faith steadied a little");
        C(PeopleEvents2Math.EasedAge(100, 1000) == 750 && PeopleEvents2Math.EasedAge(900, 1000) == 900 && PeopleEvents2Math.EasedAge(-5, 0) == 0,
            "a grief or guilt eased: worn to three quarters of its time, never made younger");
        C(PeopleEvents2Math.ShrineKinds.SequenceEqual(new[] { "town", "relic", "old" }), "a fellowship town, a relic's town, an old shrine");

        // ================= 3. One guard, one wagon =================

        C(PeopleEvents2Math.GuardShare(5f, 0, 2) == 230 && PeopleEvents2Math.GuardShare(5f, 10, 2) > PeopleEvents2Math.GuardShare(5f, 0, 2)
          && PeopleEvents2Math.GuardShare(5f, 5, 0) < PeopleEvents2Math.GuardShare(5f, 5, 3) && PeopleEvents2Math.GuardShare(0f, 0, 0) == 60 && PeopleEvents2Math.GuardShare(float.NaN, 0, 1) == 60,
            "a share by the days, the guard's way with a bargain and the league's age");
        C(PeopleEvents2Math.GuardGoodwill == 8 && PeopleEvents2Math.GuardCaravans == 1.5f && PeopleEvents2Math.GuardYearDays == 60 && PeopleEvents2Math.GuardMinDays >= 3f,
            "the league +8, its caravans half again as often for a year");
        C(PeopleEvents2Math.PackBeast(true, true, false) && !PeopleEvents2Math.PackBeast(false, true, false) && !PeopleEvents2Math.PackBeast(true, false, false) && !PeopleEvents2Math.PackBeast(true, true, true),
            "a grown pack beast on its feet");

        // ================= 22. The body made better, on terms =================

        C(PeopleEvents2Math.BionicRecipe("Eye") == "InstallBionicEye" && PeopleEvents2Math.BionicRecipe("Shoulder") == "InstallBionicArm" && PeopleEvents2Math.BionicRecipe("Arm") == "InstallBionicArm"
          && PeopleEvents2Math.BionicRecipe("Leg") == "InstallBionicLeg" && PeopleEvents2Math.BionicRecipe("Spine") == "InstallBionicSpine" && PeopleEvents2Math.BionicRecipe("Heart") == null,
            "an eye, an arm, a leg or a spine");
        C(PeopleEvents2Math.BeastBionicRecipe("Leg") == "AI_AnimalBionicLeg" && PeopleEvents2Math.BeastBionicRecipe("Eye") == "AI_AnimalBionicEye" && PeopleEvents2Math.BeastBionicRecipe("Arm") == null,
            "or one of Alpha Implants' for a bonded beast");
        int total = PeopleEvents2Math.TermsTotal(1000), down = PeopleEvents2Math.Down(total);
        C(PeopleEvents2Math.BionicPrice(1000f) == 1100 && PeopleEvents2Math.BionicPrice(0f) == 300 && PeopleEvents2Math.BionicPrice(99999f) == 4000 && total == 1200 && down == 240,
            "the price now, a fifth more on terms, a fifth of that down");
        C(Enumerable.Range(1, 4).Sum(i => PeopleEvents2Math.Instalment(total, down, i)) == total - down && PeopleEvents2Math.Instalment(total, down, 5) == 0 && PeopleEvents2Math.Instalment(total, down, 0) == 0
          && PeopleEvents2Math.Instalments == 4 && PeopleEvents2Math.InstalmentDays == 15, "four seasonal instalments that add up to what is left");
        C(PeopleEvents2Math.SurgeonMin >= 14 && PeopleEvents2Math.DefaultGoodwill < 0 && PeopleEvents2Math.PaidUpGoodwill > 0 && PeopleEvents2Math.MaxTerms >= 2,
            "a surgeon whose surgery rarely fails; a default costs, the terms kept warm them");

        // ================= 26. A crossing =================

        C(PeopleEvents2Math.BridgeFor(0) == "Bridge" && PeopleEvents2Math.BridgeFor(1) == "Bridge" && PeopleEvents2Math.BridgeFor(2) == "HeavyBridge" && PeopleEvents2Math.BridgeFor(3) == "HeavyBridge",
            "rope and plank, then timber on piles");
        C(PeopleEvents2Math.CrossingWood(10, 4, 6, 3) == 72 && PeopleEvents2Math.CrossingWood(0, 0, 6, 3) == 20 && PeopleEvents2Math.CrossingMaxWater >= 10 && PeopleEvents2Math.BankPlanks == 2,
            "the colony's wood, each span's and plank's own cost");
        var knots = Enumerable.Range(0, 500).Select(PeopleEvents2Math.KnotDay).ToList();
        C(knots.All(d => d >= 0 && d < 60) && knots.Distinct().Count() >= 30 && PeopleEvents2Math.KnotDay(-7) >= 0, "a knot night on one day of the year, by the people");
        C(PeopleEvents2Math.KnotNight(12, 12, 3, 2) && !PeopleEvents2Math.KnotNight(12, 12, 3, 3) && !PeopleEvents2Math.KnotNight(11, 12, 3, 2), "once a year, on its day");

        // ================= 59. The guild's mark =================

        C(PeopleEvents2Math.UniqueOf("Bow_Great") == "Bow_Great_Unique" && PeopleEvents2Math.UniqueOf("Bow_Great_Unique") == null && PeopleEvents2Math.UniqueOf(null) == null,
            "a weapon's unique kind by its name");
        C(PeopleEvents2Math.MarkPrice(100f, 1000f) == 300 && PeopleEvents2Math.MarkPrice(100f, 120f) == 150 && PeopleEvents2Math.MarkPrice(0f, 99999f) == 900 && PeopleEvents2Math.MarkPrice(float.NaN, 300f) == 150,
            "a third of what the work adds, within 150 and 900");
        C(PeopleEvents2Math.WithCrafter(400) == 300 && PeopleEvents2Math.WithCrafter(50) == 100 && PeopleEvents2Math.Better(1f, 1.1f) && !PeopleEvents2Math.Better(1f, 1.05f) && !PeopleEvents2Math.Better(float.NaN, 2f),
            "a quarter off with the colony's crafter; a blade's stuff better by a tenth");
        C(PeopleEvents2Math.MarkTicks >= 6 * PeopleEvents2Math.HourTicks && PeopleEvents2Math.MaxMarks >= 20, "a day at the bench; the marked blades' names bounded");

        // ================= 12. Water in the dry year =================

        C(PeopleEvents2Math.HerdMax == 12 && PeopleEvents2Math.Toll(10, 1f) == 100 && PeopleEvents2Math.Toll(1, 0.1f) == 40 && PeopleEvents2Math.Toll(100, 5f) == 200,
            "up to a dozen head; a toll of ten a head");
        C(PeopleEvents2Math.DryGoodwill == 10 && PeopleEvents2Math.TollGoodwill < 0 && PeopleEvents2Math.TurnedAwayGoodwill < PeopleEvents2Math.TollGoodwill, "+10 for the water; a toll costs a little, turning away more");
        C(PeopleEvents2Math.StrayChance(true) == 2f * PeopleEvents2Math.StrayChance(false) && PeopleEvents2Math.StrayChance(true) <= 0.3f && PeopleEvents2Math.PredatorChance <= 0.5f,
            "a stray now and then, likelier on the colony's grazing; a predator through the budget");
        var lost = Enumerable.Range(0, 100).Select(i => PeopleEvents2Math.LostOnRoad(12, i / 100f)).ToList();
        C(lost.Min() == 2 && lost.Max() == 6 && PeopleEvents2Math.LostOnRoad(0, 0.9f) == 2 && PeopleEvents2Math.LostNewsMinDays >= 1, "turned away, two to half the herd lost on the road");

        // ================= 25. A pad by the colony =================

        var rents = Enumerable.Range(1, 9).Select(d => PeopleEvents2Math.PadRent(1f, d)).ToList();
        C(rents.All(r => r % 10 != 0 && r >= 41) && PeopleEvents2Math.PadRent(1f, 7) == 97 && PeopleEvents2Math.PadRent(1f, 0) == 91, "the rent at an exact figure, never a round one");
        C(PeopleEvents2Math.Raised(97) == 146 && PeopleEvents2Math.PaysRaise(0f, 0f) == 0.75f && PeopleEvents2Math.PaysRaise(1f, 100f) == 0.1f && PeopleEvents2Math.PaysRaise(0.3f, 20f) > PeopleEvents2Math.PaysRaise(0.6f, 20f),
            "raised half again: they pay or go, prouder and sourer go likelier");
        C(PeopleEvents2Math.MealsPrice(150f) == 165 && PeopleEvents2Math.MealsPrice(0f) == 0 && PeopleEvents2Math.RentDays == 5 && PeopleEvents2Math.PadBandChance > 0f && PeopleEvents2Math.PadBandChance <= 0.2f,
            "meals at their worth and a tenth; rent every five days; a band their stock draws now and then");

        // ================= 20. Books kept from the fire =================

        var books = Enumerable.Range(0, 100).Select(i => PeopleEvents2Math.ArchiveBooks(i / 100f)).ToList();
        C(books.Min() == 4 && books.Max() == 8 && PeopleEvents2Math.ArchiveBooks(float.NaN) == 4, "four to eight books");
        C(Enumerable.Range(0, 6).Select(i => PeopleEvents2Math.ArchiveBook(1, i, true)).Distinct().OrderBy(x => x).SequenceEqual(new[] { "Novel", "TextBook" })
          && Enumerable.Range(0, 8).Select(i => PeopleEvents2Math.ArchiveBook(2, i, true)).Distinct().Count() == 4 && !Enumerable.Range(0, 8).Any(i => PeopleEvents2Math.ArchiveBook(3, i, false) == "VBE_Newspaper")
          && PeopleEvents2Math.ArchiveBook(2, -1, false) != null, "the game's books by the society's age, Vanilla Books Expanded's papers with it");
        C(PeopleEvents2Math.Kept(6, 6, 0, 0) && !PeopleEvents2Math.Kept(6, 5, 0, 0) && !PeopleEvents2Math.Kept(6, 6, 1, 0) && !PeopleEvents2Math.Kept(6, 6, 0, 1) && !PeopleEvents2Math.Kept(0, 0, 0, 0),
            "every book there is thanks; one lost, sold or the tome's seal broken is a wrong");
        C(PeopleEvents2Math.ArchiveGoodwill == 8 && PeopleEvents2Math.LostWeight >= 15 && PeopleEvents2Math.Tome == "Tome" && PeopleEvents2Math.ArchiveMinDays >= 10, "+8 and a book left; a lasting wrong; Anomaly's tome sealed");

        // ================= 40. A leg for a lame beast =================

        C(PeopleEvents2Math.BeastLeg(0) == null && PeopleEvents2Math.BeastLeg(1) == null && PeopleEvents2Math.BeastLeg(2) == "AI_AnimalProstheticLeg" && PeopleEvents2Math.BeastLeg(3) == "AI_AnimalProstheticLeg",
            "Alpha Implants' leg from the industrial age, the stables before");
        C(PeopleEvents2Math.IsLeg("Leg") && PeopleEvents2Math.IsLeg("Paw") && !PeopleEvents2Math.IsLeg("Eye") && !PeopleEvents2Math.IsLeg(null), "a leg, or a paw or hoof of one");
        C(PeopleEvents2Math.LegPrice(500f) == 550 && PeopleEvents2Math.LegPrice(0f) == 150 && PeopleEvents2Math.LegPrice(99999f) == 900, "her price within 150 and 900");

        // ================= 63. News for sale =================

        C(PeopleEvents2Math.NewsKinds.SequenceEqual(new[] { "stash", "wreck", "outpost" }) && PeopleEvents2Math.NewsKinds.All(k => PeopleEvents2Math.NewsQuest(k) != null) && PeopleEvents2Math.NewsQuest("x") == null,
            "an item stash, a wreck's cargo, a bandit outpost, each the game's own quest");
        var prices = PeopleEvents2Math.NewsKinds.SelectMany(k => Enumerable.Range(0, 11).Select(i => PeopleEvents2Math.NewsPrice(k, i / 10f))).ToList();
        C(prices.Min() == 60 && prices.Max() == 200 && PeopleEvents2Math.NewsPrice("stash", 1f) < PeopleEvents2Math.NewsPrice("outpost", 0f) + 1, "60 to 200 silver, a stash cheapest, an outpost dearest");

        // ================= Their defs =================

        var events = XDocument.Load(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents2.xml")).Root!.Elements("IncidentDef").ToDictionary(Dn);
        C(events.Keys.SequenceEqual(new[] { "Rimfolk_Evt_Pilgrimage", "Rimfolk_Evt_LeagueGuard", "Rimfolk_Evt_EnclaveBody", "Rimfolk_Evt_EnclaveInstalment", "Rimfolk_Evt_RiverCrossing",
            "Rimfolk_Evt_KnotNight", "Rimfolk_Evt_GuildMark", "Rimfolk_Evt_HerdersDrought", "Rimfolk_Evt_EnclavePad", "Rimfolk_Evt_ArchiveTrust", "Rimfolk_Evt_LameBeast", "Rimfolk_Evt_NewsForSale" }),
            "twelve events: ten drawn, two fired when their day comes");
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { src }.Concat(parts).ToArray()));
        string dir = Path.Combine(src, "Peoples", "Events2");
        string code = string.Join("\n", Directory.GetFiles(dir, "*.cs").Where(f => !f.EndsWith("PeopleEvents2Math.cs")).OrderBy(f => f).Select(File.ReadAllText));
        string everyCode = string.Join("\n", Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        var callings = new Dictionary<string, string>
        {
            ["Rimfolk_Evt_Pilgrimage"] = "Pilgrims", ["Rimfolk_Evt_LeagueGuard"] = "Traders", ["Rimfolk_Evt_EnclaveBody"] = "Brokers", ["Rimfolk_Evt_RiverCrossing"] = "Fishers",
            ["Rimfolk_Evt_GuildMark"] = "Artisans", ["Rimfolk_Evt_HerdersDrought"] = "Herders", ["Rimfolk_Evt_EnclavePad"] = "Brokers", ["Rimfolk_Evt_ArchiveTrust"] = "Scholars",
            ["Rimfolk_Evt_LameBeast"] = "Tamers", ["Rimfolk_Evt_NewsForSale"] = "Linesmen"
        };
        var fired = new[] { "Rimfolk_Evt_EnclaveInstalment", "Rimfolk_Evt_KnotNight" };
        foreach (var (name, e) in events)
        {
            string worker = e.Element("workerClass")!.Value.Replace("ColonyCouncil.", "");
            var m = Regex.Match(code, @"class " + worker + @" : (HappeningWorker_Visitors|IncidentWorker_Happening)\s*\{(.*?)\n    \}", RegexOptions.Singleline);
            C(m.Success, $"{name}: its worker is the round's own");
            if (callings.TryGetValue(name, out var calling))
                C(m.Success && m.Groups[2].Value.Contains("PeoplesMath." + calling) && m.Groups[2].Value.Contains("PeoplesD.Friendly("), $"{name}: only from a people of its calling at peace with the colony");
            var effects = e.Descendants("li").Select(li => li.Attribute("Class")?.Value).Where(v => v != null && v.StartsWith("ColonyCouncil.DilemmaEffect_")).Select(v => v.Replace("ColonyCouncil.", "")).Distinct();
            C(effects.All(x => Regex.IsMatch(everyCode, @"class " + x + @"\b")), $"{name}: every answer's effect is real");
            var ext = Ext(e, "HappeningExtension")!;
            float chance = F(e.Element("baseChance")!.Value);
            if (fired.Contains(name)) C(chance == 0f && code.Contains("PeopleEvents2.Fire(\"" + name + "\""), $"{name}: never drawn, fired only when its day comes");
            else
                C(chance >= 0.3f && chance <= 0.8f && int.Parse(e.Element("minRefireDays")!.Value) >= 30 && int.Parse(e.Element("earliestDay")!.Value) >= 10,
                    $"{name}: a small, spread chance, not soon again, not the first moment it is free");
            C(ext.Element("group")?.Value == "Visitors", $"{name}: among the visitors");
            var texts = ext.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
            C(texts.Count >= 5 && (fired.Contains(name) || texts.Any(t => Regex.IsMatch(t, @"^\[[^\]]*(lowtech|tribal|pod|walk|rope|piles|noradio)"))), $"{name}: five or more texts, in the forms of the age");
            var options = ext.Element("options")!.Elements("li").ToList();
            C(options.Count == 3 && ext.Element("defaultOption")?.Value == "2", $"{name}: three answers, no answer the last");
            C(options[2].Descendants("li").Any(li => li.Attribute("Class")?.Value is "ColonyCouncil.DilemmaEffect_SendAway" or "ColonyCouncil.DilemmaEffect_EnclaveDefault"),
                $"{name}: the last answer sends them on (or, for a letter, lets the season pass)");
            var lineTags = ext.Element("lines")?.Elements("li").Select(l => Regex.Match(l.Value, @"^\[([^\],]+)").Groups[1].Value).Distinct().ToList() ?? new List<string>();
            C(lineTags.All(t => code.Contains("\"" + t + "\"")), $"{name}: every line's moment is one the code says it at");
            C(ext.Element("lines")?.Elements("li").All(l => Regex.Matches(Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", ""), @"[.?](\s|$)").Count == 1) ?? true, $"{name}: every spoken line one sentence");
        }
        C(events["Rimfolk_Evt_EnclaveBody"].Descendants("li").Count(li => li.Value.StartsWith("[") && li.Value.Contains("!beast")) >= 6
          && events["Rimfolk_Evt_EnclaveBody"].Descendants("li").Any(li => li.Value.StartsWith("[beast]")), "the enclave's words say a person's part or a beast's, never the wrong one");

        // The camps.
        var campKinds = XDocument.Load(Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKinds_PeopleEvents2.xml")).Root!.Elements("ColonyCouncil.CampKindDef").ToDictionary(Dn);
        C(campKinds.Keys.SequenceEqual(new[] { "Rimfolk_Camp_DryWater", "Rimfolk_Camp_EnclavePad" }), "two camps of their own");
        foreach (var (name, k) in campKinds)
        {
            string worker = k.Element("workerClass")!.Value.Replace("ColonyCouncil.", "");
            C(F(k.Element("weight")!.Value) == 0f && code.Contains("class " + worker + " : CampWorker"), $"{name}: never on the camps' own draw; ways of its own ({worker})");
            C(k.Descendants("li").Where(li => li.Parent?.Name.LocalName is "lines" or "momentLines").All(l => Regex.Matches(Regex.Replace(l.Value, @"^\[[^\]]*\]\s*", ""), @"[.?](\s|$)").Count == 1),
                $"{name}: every spoken line one sentence");
        }
        var dry = campKinds["Rimfolk_Camp_DryWater"];
        C(dry.Element("calling")!.Value == "herders" && dry.Element("days")!.Value == "5~8" && dry.Element("animals")!.Elements("li").Take(2).Sum(a => int.Parse(a.Element("count")!.Value.Split('~')[1])) <= 12
          && dry.Element("leaveBehind") != null && dry.Element("help")!.Elements("li").Any(h => h.Value == "milk"), "the herders: five to eight days, up to a dozen head and dogs, milk, a young beast after a good stay");
        C(dry.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).SequenceEqual(new[] { "hay", "grazing", "milk", "beasts" })
          && dry.Element("asks")!.Elements("li").Take(2).All(a => a.Element("theyAsk")?.Value == "true"), "they ask hay or grazing; milk and a hand with the colony's beasts offered");
        var pad = campKinds["Rimfolk_Camp_EnclavePad"];
        C(pad.Element("calling")!.Value == "brokers" && pad.Element("people")!.Value == "4~4" && pad.Element("days")!.Value == "10~15" && pad.Element("trader")?.Value == "true"
          && F(pad.Element("breakTerms")!.Value) >= 0.1f, "the pad: two factors and two marshals, ten to fifteen days, the game's own trading, terms bent now and then");
        C(pad.Element("asks")!.Elements("li").Select(a => a.Element("key")!.Value).SequenceEqual(new[] { "pad", "meals", "raise" }), "off the pad, their meals, the rent raised");

        // Their jobs and the shell.
        var misc = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_PeopleEvents2_Misc.xml")).Root!.Elements().ToList();
        var works = misc.Where(e => Ext(e, "PeopleWorkJob") != null).ToList();
        C(works.Select(Dn).SequenceEqual(new[] { "Rimfolk_PEv2_Bionic", "Rimfolk_PEv2_BeastBionic", "Rimfolk_PEv2_BeastLeg", "Rimfolk_PEv2_Mark" })
          && works.All(w => Ext(w, "PeopleWorkJob")!.Element("done")!.Value is string d && code.Contains("class " + d.Replace("ColonyCouncil.", "") + " : PeopleWorkDone")),
            "round 1's visitor who works on a colonist: a part at the bed, a beast's where it stands, a weapon at the bench");
        C(works.Select(w => Ext(w, "PeopleWorkJob")!.Element("place")!.Value).SequenceEqual(new[] { "bed", "here", "here", "beside" }) && Ext(works[3], "PeopleWorkJob")!.Element("atPlace")?.Value == "true",
            "each work's place, the master at the bench itself");
        C(misc.Any(e => Dn(e) == "Rimfolk_PEv2_SetOut" && e.Element("driverClass")!.Value == "ColonyCouncil.JobDriver_SetOut")
          && misc.Any(e => Dn(e) == "Rimfolk_PEv2_LayCrossing") && misc.Any(e => Dn(e) == "Rimfolk_PEv2_LayPad"), "setting out, laying a crossing, laying a pad");
        var shell = misc.Single(e => Dn(e) == "Rimfolk_PilgrimShell");
        C(shell.Element("graphicData")!.Element("texPath")!.Value == "ColonyCouncil/Things/PilgrimShell" && File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Things", "PilgrimShell.png")),
            "the pilgrim's shell, drawn");
        var memory = misc.Single(e => Dn(e) == "Rimfolk_Thought_Pilgrimage");
        C(F(memory.Descendants("baseMoodEffect").Single().Value) > 0f && F(memory.Element("durationDays")!.Value) >= 30f, "a lasting memory of the shrine");

        // The player's mods, drawn on softly and from their own catalogue.
        var active = Catalogue(root).Where(r => r.active).Select(r => r.type + ":" + r.def).ToHashSet();
        C(PeopleEvents2Math.RelicTokens.All(t => active.Contains("ThingDef:" + t)), "Vanilla Ideology Expanded - Relics and Artifacts' tokens are in the player's mods");
        C(new[] { "Eye", "Arm", "Leg", "Spine" }.All(p => active.Contains("RecipeDef:" + PeopleEvents2Math.BionicRecipe(p)))
          && new[] { "Eye", "Leg", "Spine" }.All(p => active.Contains("RecipeDef:" + PeopleEvents2Math.BeastBionicRecipe(p))) && active.Contains("RecipeDef:" + PeopleEvents2Math.BeastLeg(2)),
            "the game's bionics and Alpha Implants' beasts' parts are there");
        C(active.Contains("TerrainDef:Bridge") && active.Contains("TerrainDef:HeavyBridge") && active.Contains("TerrainDef:WoodPlankFloor") && active.Contains("TerrainDef:MetalTile"),
            "the bridges, the planks and the pad's floor are there");
        C(new[] { "TextBook", "Novel", "Schematic", "VBE_Newspaper", "Tome" }.All(b => active.Contains("ThingDef:" + b)), "the game's books, Vanilla Books Expanded's papers and Anomaly's tome are there");
        C(PeopleEvents2Math.NewsKinds.All(k => active.Contains("QuestScriptDef:" + PeopleEvents2Math.NewsQuest(k))), "the game's own quests for the news are there");
        C(active.Contains("GameConditionDef:Drought") && active.Contains("GameConditionDef:VEE_Drought"), "Odyssey's and Vanilla Events Expanded's droughts are there");
        var uniques = active.Where(a => a.StartsWith("ThingDef:") && a.EndsWith(PeopleEvents2Math.UniqueSuffix)).Select(a => a.Substring(9)).ToList();
        C(uniques.Count >= 20 && uniques.Count(u => active.Contains("ThingDef:" + u.Substring(0, u.Length - PeopleEvents2Math.UniqueSuffix.Length))) >= 20
          && uniques.Contains("Bow_Great_Unique") && uniques.Contains("VWE_Bow_Crossbow_Unique"), "the unique pieces found by name: Odyssey's greatbow, Vanilla Weapons Expanded's crossbow and their rifles");
        string wsDefs = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Defs", "WorldSituationDefs"), "*.xml").Select(File.ReadAllText));
        C(new[] { "Rimfolk_WS_GreatCaravan", "Council_WS_TradeBoom", "Council_WS_GreatFire", "Council_WS_Drought" }.All(d => wsDefs.Contains("<defName>" + d + "</defName>"))
          && code.Contains("\"Rimfolk_WS_GreatCaravan\", \"Council_WS_TradeBoom\"") && code.Contains("\"Council_WS_GreatFire\"") && code.Contains("\"Council_WS_Drought\""),
            "the world events they come with: a great caravan or a trading season, a great fire, a long drought");
        C(code.Contains("Rimfolk_League_Outrider") && code.Contains("Rimfolk_Pilgrims_Keeper") && code.Contains("Rimfolk_Enclave_Hand") && code.Contains("Rimfolk_RiverFolk_Ferryman")
          && code.Contains("Rimfolk_KilnGuild_Guildmaster") && code.Contains("Rimfolk_Herders_Elder") && code.Contains("Rimfolk_Scholars_Fellow") && code.Contains("Rimfolk_Tamers_Tamer")
          && code.Contains("Rimfolk_SignalLine_Operator"), "each people's own kind of person comes");

        // ================= Their words =================

        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeopleEvents2.xml")).Root!.Elements().Select(e => e.Name.LocalName).ToHashSet();
        var used = Regex.Matches(code + Read("PlayOut", "PlayCarry.cs"), "\"(CC_PEv2_\\w+)\"").Select(mm => mm.Groups[1].Value).Where(k => !k.EndsWith("_")).Distinct().ToList();
        C(used.Count >= 150 && used.All(keyed.Contains), $"every word the code says is keyed ({used.Count}{(used.Any(u => !keyed.Contains(u)) ? ", missing " + string.Join(", ", used.Where(u => !keyed.Contains(u))) : "")})");
        C(new[] { "rope", "piles" }.All(b => keyed.Contains("CC_PEv2_Bridge_" + b)) && PeopleEvents2Math.NewsKinds.All(k => keyed.Contains("CC_PEv2_News_" + k) && keyed.Contains("CC_PEv2_NewsWhat_" + k)),
            "the bridges and each kind of news, with words");
        var texts2 = new[] { Path.Combine("Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents2.xml"), Path.Combine("Defs", "CampDefs", "Rimfolk_CampKinds_PeopleEvents2.xml"),
                Path.Combine("Languages", "English", "Keyed", "Rimfolk_PeopleEvents2.xml"), Path.Combine("Defs", "Misc", "Rimfolk_PeopleEvents2_Misc.xml") }
            .Select(x => Regex.Replace(File.ReadAllText(Path.Combine(root, x)), "<!--.*?-->|<\\?xml[^>]*>|\\[[^\\]]*\\]", "", RegexOptions.Singleline)).Aggregate((x, y) => x + y);
        C(!texts2.Contains('—') && !texts2.Contains('!') && !Regex.IsMatch(texts2, @"\bin \d+ (days?|hours?)\b"), "plain words, no countdowns");
        C(!Regex.IsMatch(texts2.ToLowerInvariant(), @"\b(dynasty|dynasties|bloodline|heir to the house)\b"), "the colony is the family: no houses");
        C(!Regex.IsMatch(texts2, @"\b(she|her|hers)\b"), "nobody of theirs is given a sex the game didn't");
        string Alm(string f) => File.ReadAllText(Path.Combine(root, "Defs", "AlmanacArticleDefs", f));
        C(Alm("Almanac_Peoples.xml").Contains("One guard, one wagon") && Alm("Almanac_Peoples.xml").Contains("dry year")
          && Alm("Almanac_PeoplesC.xml").Contains("pilgrimage") && Alm("Almanac_PeoplesC.xml").Contains("on terms") && Alm("Almanac_PeoplesC.xml").Contains("pad")
          && Alm("Almanac_PeoplesC.xml").Contains("crossing") && Alm("Almanac_PeoplesC.xml").Contains("archive") && Alm("Almanac_PeoplesD.xml").Contains("stables")
          && Alm("Almanac_PeoplesE.xml").Contains("guild's mark") && Alm("Almanac_PeoplesF.xml").Contains("News for sale"),
            "each people's Almanac page says what it does now");
        C(Alm("Almanac_PeoplesF.xml").Contains("<heading>Of their own: gifts, trust and camps</heading>"), "the Signal Line's page has its section of their own");

        // ================= The seams =================

        C(Read("Peoples", "Events", "PeopleEvents.cs").Contains("return PeopleEvents2.Hour(v, comp, now);"), "round 2's visits wait on round 1's hour");
        C(Read("Peoples", "Peoples.cs").Contains("* LeagueGuard.CaravanFactor(f)") && Read("Peoples", "PeoplesCallers.cs").Contains("!EnclaveTerms.InDefault(f)"),
            "the league's caravans weighed half again; no lander of the enclave's while a default stands");
        C(Read("PlayOut", "PlayCarry.cs").Contains("public const string Archive = \"archive\";") && Read("PlayOut", "PlayCarry.cs").Contains("return ArchiveTrust.Held(map);"),
            "the archive's books carried out are only theirs");
        C(Read("Peoples", "Events", "PeopleWorkOn.cs").Contains("public bool atPlace;") && Read("Peoples", "PeopleTowns.cs").Contains("TerrainDef bridgeDef = null")
          && Read("Heart", "Feelings.cs").Contains("public static bool Feeds(ThoughtDef def, string key)"),
            "round 1's work at a place, the river folk's own dock-laying, and the heart's grief and shame reused");
        C(code.Contains("CaravanExitMapUtility.ExitMapAndCreateCaravan(") && code.Contains("CaravanMaker.MakeCaravan(") && code.Contains("WorldLedger.Keep(p, KeepFor.Lent"),
            "the season away goes by the game's own roads, kept in the world while away");
        C(code.Contains("r.Worker.ApplyOnPawn(on, part, worker, new List<Thing>(), null)") && code.Contains("OrderLimb.Chance("), "parts fitted by the game's own surgery and its chance, shown on the card");
        C(code.Contains("QuestUtility.GenerateQuestAndMakeAvailable(") && code.Contains("HeardPlaces.Heard(new[] { \"site:\" + site.ID }"), "the news is the game's own quest site, marked as heard of");
        C(code.Contains("PeopleTowns.LayDocks(") && code.Contains("Tie.Kind.Debt") && code.Contains("CampRaidMath.AttackTick(") && code.Contains("c.termFee = true") && code.Contains("CampTracker.RefreshTrader") == false
          && code.Contains("c.StrayHerd(field)") && code.Contains("c.EatCrops("), "docks, debts, bands against a camp, the camps' fee, and the camps' own straying, reused");
        C(code.Contains("PeopleEventsBook.Gave(Pilgrimage.GiftKind") && code.Contains("PeopleEventsBook.Gave(EnclaveBody.GiftKind") && code.Contains("PeopleEventsBook.Gave(GuildMark.GiftKind")
          && code.Contains("PeopleEventsBook.Gave(LameBeast.GiftKind"), "four gifts, each once and spaced out with round 1's");
        var harms = Regex.Matches(code, @"Harm\.(Roll|Ail|Injure)\(([^,;]*,\s*)?HarmKinds\.(\w+)").Select(mm => mm.Groups[3].Value).Distinct().OrderBy(x => x).ToList();
        C(harms.SequenceEqual(new[] { "CampLeft", "PilgrimCough" }) && !Regex.IsMatch(code, @"TakeDamage|\.Kill\("),
            "the only harm of their own: the pilgrims' cough and a predator the herd draws, through the harm budget; the rest the game's own");
        C(Regex.Matches(code, @"\[HarmonyPatch\(").Count == 3 && Regex.Matches(code, @"if \(!RimfolkPatches\.On|if \(!__result \|\| !RimfolkPatches\.On").Count >= 3, "three game hooks, each behind the deep-hooks setting");
        C(!code.Contains("MoteMaker.ThrowText") && !code.Contains("Speech.Say") && code.Contains("PeoplesG.SayLine(") && code.Contains("c.SayMoment("), "they speak through the events' and the camps' own lines");
        C(!Regex.IsMatch(code, @"Sched\.|\bLocomotionUrgency\.Sprint"), "no clock of their own: the events' hour and the camps' hour");
        string book = File.ReadAllText(Path.Combine(dir, "PeopleEvents2Book.cs"));
        C(book.Contains("PeopleEvents2Math.MaxCrossings") && book.Contains("PeopleEvents2Math.MaxTerms") && book.Contains("PeopleEvents2Math.MaxMarks"), "what they keep is bounded");
        C(Regex.Matches(code, @"\[DebugAction\(""Rimfolk"", ""Peoples' events 2: ").Count == 6, "six dev actions: the record, a camp here now, a season's days over, the next letter, the cart, the knot night");
        string save = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(save.Contains("Peoples/Events2/PeopleEvents2Book.cs:crossings") && save.Contains("Peoples/Events2/EnclavePad.cs:stock"), "their saved lists are in the save model");
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
