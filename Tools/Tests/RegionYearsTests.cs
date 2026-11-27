using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The region's years and the camps (the content update's integrations, part B: B5, B11, B13, B19, B27): the pure rules
/// (RegionPricesMath, HamletMath's region part, RaidersCampSignsMath, TakenInMath, CallsFromHomeMath), and the defs, words
/// and seams they stand on (the season prices' hook shared with the moot, the hamlet's letter and carter, the camp's scouts,
/// runaway and smoke, the camps' beasts and goods from the player's mods, a call to arms answered from home).
/// </summary>
public static class RegionYearsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the region's years: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var defFiles = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).ToDictionary(f => f, f => XDocument.Load(f));
        XElement Def(string type, string name) =>
            defFiles.Values.SelectMany(d => d.Root.Elements()).FirstOrDefault(e => (e.Name.LocalName == type || e.Name.LocalName == "ColonyCouncil." + type) && (string)e.Element("defName") == name);
        var keyed = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var e in XDocument.Load(f).Root.Elements())
                keyed[e.Name.LocalName] = e.Value;
        bool OneSentence(string s)
        {
            string t = (s ?? "").Trim().TrimEnd('.', '?');
            return t.Length > 0 && !Regex.IsMatch(t, @"[.?!] ") && !t.Contains("\\n") && !t.Contains('!');
        }

        // ================= B5. Traders' prices follow the region's hard and good years =================

        var lacks = new Dictionary<string, RegionGoods>
        {
            ["Council_WS_Famine"] = RegionGoods.Food, ["Rimfolk_WS_Blight"] = RegionGoods.Food, ["Council_WS_Locusts"] = RegionGoods.Food,
            ["Rimfolk_WS_ColdYear"] = RegionGoods.Food, ["Council_WS_Drought"] = RegionGoods.Food,
            ["Council_WS_Plague"] = RegionGoods.Medicine, ["Rimfolk_WS_MedicineScarce"] = RegionGoods.Medicine,
            ["Council_WS_IronScarce"] = RegionGoods.Steel, ["Rimfolk_WS_NeighborsWar"] = RegionGoods.Steel,
            ["Council_WS_GreatFire"] = RegionGoods.Wood, ["Council_WS_Flood"] = RegionGoods.Wood, ["Council_WS_Rebuilding"] = RegionGoods.Wood,
            ["Rimfolk_WS_GreatWork"] = RegionGoods.Wood,
            ["Council_WS_Murrain"] = RegionGoods.Textiles, ["Council_WS_Embargo"] = RegionGoods.Textiles,
            // The content update's variety (World/Variety/): the two scarcities and a bridge to build again.
            ["Rimfolk_WS_ClothScarce"] = RegionGoods.Textiles, ["Rimfolk_WS_TimberScarce"] = RegionGoods.Wood, ["Rimfolk_WS_BridgeDown"] = RegionGoods.Wood,
            // The content round (World/Variety/WorldRound2.cs): good metal bought dear while the coin is mistrusted.
            ["Rimfolk_WS_BadCoin"] = RegionGoods.Steel
        };
        foreach (var kv in lacks)
        {
            C(RegionPricesMath.Goods(kv.Key, out bool dear) == kv.Value && dear, $"{kv.Key} makes {kv.Value} dear at its town");
            C(Def("WorldSituationDef", kv.Key) != null, $"{kv.Key} is a world event of the mod's");
        }
        C(RegionPricesMath.Goods(RegionPricesMath.Bumper, out bool bumperDear) == RegionGoods.Food && !bumperDear && Def("WorldSituationDef", RegionPricesMath.Bumper) != null,
            "a bumper harvest makes food cheap");
        C(RegionPricesMath.Goods("Council_WS_HardWinter", out _) == RegionGoods.None && RegionPricesMath.Goods(null, out _) == RegionGoods.None,
            "a world event that names no goods moves no price");
        C(RegionPricesMath.Events.Count() == lacks.Count + 1 && RegionPricesMath.Events.All(e => lacks.ContainsKey(e) || e == RegionPricesMath.Bumper),
            "every world event that moves a price is one of the proposal's");
        C(RegionPricesMath.BothSides("Rimfolk_WS_NeighborsWar") && RegionPricesMath.BothSides("Council_WS_Embargo") && !RegionPricesMath.BothSides("Council_WS_Famine"),
            "a war between neighbours and closed markets touch both peoples, nothing else does");
        C(RegionPricesMath.Factor("Council_WS_Famine", RegionGoods.Food) == RegionPricesMath.Dear && RegionPricesMath.Factor("Council_WS_Famine", RegionGoods.Steel) == 1f
          && RegionPricesMath.Factor(RegionPricesMath.Bumper, RegionGoods.Food) == RegionPricesMath.Cheap && RegionPricesMath.Factor("Council_WS_Famine", RegionGoods.None) == 1f,
            "a world event's factor falls only on its own goods");
        C(RegionPricesMath.Dear > 1f && RegionPricesMath.Dear <= 1.3f && RegionPricesMath.Cheap < 1f && RegionPricesMath.Cheap >= 0.75f,
            "the moves are a quarter dearer and a fifth cheaper, no more");
        C(Math.Abs(RegionPricesMath.Factor(new[] { "Council_WS_Famine", "Rimfolk_WS_Blight", "Council_WS_Drought" }, RegionGoods.Food) - RegionPricesMath.Max) < 1e-5f
          && Math.Abs(RegionPricesMath.Factor(new[] { "Council_WS_Famine", RegionPricesMath.Bumper }, RegionGoods.Food) - 1f) < 1e-5f
          && RegionPricesMath.Factor((IEnumerable<string>)null, RegionGoods.Food) == 1f,
            "several world events at one people's towns multiply, within half and one and a half");
        C(Math.Abs(RegionPricesMath.Combined(0.8f, 1.25f) - 1f) < 1e-5f && RegionPricesMath.Combined(1.5f, 1.5f) == RegionPricesMath.Max
          && RegionPricesMath.Combined(0.5f, 0.5f) == RegionPricesMath.Min && RegionPricesMath.Combined(float.NaN, 1.25f) == 1.25f,
            "the moot's season price and the region's year are one factor, kept within half and one and a half");
        C(RegionPricesMath.Percent(1.25f) == 25 && RegionPricesMath.Percent(0.8f) == 20 && RegionPricesMath.Percent(1f) == 0, "the hover's percent");
        C(RegionPricesMath.Kind(true, true, false, false, false) == RegionGoods.Medicine && RegionPricesMath.Kind(false, true, false, true, false) == RegionGoods.Food
          && RegionPricesMath.Kind(false, false, true, false, false) == RegionGoods.Steel && RegionPricesMath.Kind(false, false, false, true, true) == RegionGoods.Wood
          && RegionPricesMath.Kind(false, false, false, false, true) == RegionGoods.Textiles && RegionPricesMath.Kind(false, false, false, false, false) == RegionGoods.None,
            "a thing's kind: medicine before food (herbal medicine is no food), then steel, wood, cloth and leather");
        foreach (RegionGoods g in Enum.GetValues(typeof(RegionGoods)))
            if (g != RegionGoods.None)
                C(keyed.ContainsKey("CC_RegionPrice_Goods_" + g), $"the goods {g} have a word");
        foreach (var k in new[] { "CC_RegionPrice_DearAt", "CC_RegionPrice_Dear", "CC_RegionPrice_CheapAt", "CC_RegionPrice_Cheap", "CC_RegionPrice_MootCheap", "CC_RegionPrice_MootDear" })
            C(keyed.TryGetValue(k, out var v) && v.Contains("{PCT}%") && v.Contains("{GOODS}") && v.Contains("buying or selling"), $"{k} says how much, of what, and that both ways move");
        C(keyed["CC_RegionPrice_DearAt"].Contains("{PLACE}") && keyed["CC_RegionPrice_DearAt"].Contains("{EVENT}") && !keyed["CC_RegionPrice_Dear"].Contains("{PLACE}"),
            "the hover names where the world event is, and leaves it out on the other side of a war");
        string season = Code(Path.Combine("Peoples", "Events10", "PeopleEvents10.cs"));
        string prices = Code(Path.Combine("World", "Region", "RegionPrices.cs"));
        C(season.Contains("RegionPricesMath.Combined(") && season.Contains("RegionPrices.Factor(def, RegionPrices.Trader)") && Regex.Matches(season, @"HarmonyPatch\(typeof\(Tradeable\), nameof\(Tradeable\.GetPriceFor\)\)").Count == 1,
            "the region's years ride on the moot's one price hook, never a second one that would pull against it");
        C(prices.Contains("nameof(Tradeable.GetPriceTooltip)") && prices.Contains("RimfolkPatches.On") && prices.Contains("TradeSession.trader?.Faction"),
            "the price's hover says why, by the trader's own people");
        C(prices.Contains("worldSituations") && prices.Contains("ws.Running"), "only running world events move prices, and none with world events off");

        // ================= B11. The hamlet's hard seasons follow the region =================

        var needs = new Dictionary<string, HamletNeed>
        {
            ["Council_WS_Plague"] = HamletNeed.Doctor,
            ["Council_WS_Famine"] = HamletNeed.Food, ["Rimfolk_WS_Blight"] = HamletNeed.Food, ["Council_WS_Locusts"] = HamletNeed.Food,
            ["Council_WS_Drought"] = HamletNeed.Food, ["Rimfolk_WS_ColdYear"] = HamletNeed.Food,
            ["Council_WS_GreatFire"] = HamletNeed.Tools, ["Council_WS_Flood"] = HamletNeed.Tools, ["Council_WS_Rebuilding"] = HamletNeed.Tools
        };
        C(HamletMath.RegionEvents.Length == needs.Count && HamletMath.RegionEvents.All(needs.ContainsKey), "the hamlet's hard years are the proposal's nine");
        foreach (var kv in needs)
        {
            C(HamletMath.RegionNeed(kv.Key) == kv.Value, $"{kv.Key} brings the hamlet the need {kv.Value}");
            C(keyed.TryGetValue("CC_Hamlet_Region_" + kv.Key, out var line) && line.Contains("{HAMLET}") && line.Contains("{WHERE}"), $"{kv.Key} has the letter's line naming the hamlet and where");
        }
        C(HamletMath.RegionNeed(HamletMath.Bumper) == null && HamletMath.RegionNeed("Council_WS_HardWinter") == null && HamletMath.RegionNeed(null) == null,
            "a bumper year or an unrelated world event brings no need");
        C(HamletMath.Bumper == RegionPricesMath.Bumper, "the bumper year is the same world event for the hamlet and the prices");
        C(HamletMath.Reaches(0f) && HamletMath.Reaches(HamletMath.RegionTiles) && !HamletMath.Reaches(HamletMath.RegionTiles + 0.5f) && !HamletMath.Reaches(-1f),
            "a world event reaches the hamlet within the region's tiles");
        bool rollFallsBack = true;
        for (float roll = 0f; roll <= 1f; roll += 0.05f)
            for (int last = -1; last < 3; last++)
            {
                rollFallsBack &= HamletMath.NeedFor(null, roll, last) == HamletMath.NeedFor(roll, last);
                rollFallsBack &= HamletMath.NeedFor(HamletNeed.Doctor, roll, last) == HamletNeed.Doctor;
            }
        C(rollFallsBack, "with a hard year near the need is the year's, else the old roll");
        int day = 60000, now = 100 * day;
        int founded = now - (HamletMath.HelpGapDays + 1) * day;
        C(HamletMath.HelpDue(HamletMath.HungryUnder - 1f, -1, founded, now, false) && !HamletMath.HelpDue(HamletMath.HungryUnder + 5f, -1, founded, now, false)
          && HamletMath.HelpDue(HamletMath.HungryUnder + 5f, -1, founded, now, true) && !HamletMath.HelpDue(HamletMath.HardYearUnder + 1f, -1, founded, now, true),
            "a hard year near brings the ask a little sooner (a little better off, and still asking)");
        C(HamletMath.HardYearUnder > HamletMath.HungryUnder && HamletMath.HardYearUnder <= HamletMath.PoorUnder, "a little sooner, not for a flourishing hamlet");
        C(!HamletMath.HelpDue(10f, now - day, founded, now, true) && !HamletMath.HelpDue(10f, -1, now - day, now, true), "the gap between asks and a new hamlet's first days hold in a hard year too");
        C(HamletMath.Harvest(10, true) == 15 && HamletMath.Harvest(10, false) == 10 && Enumerable.Range(1, 40).All(x => HamletMath.Harvest(x, true) > x),
            "a bumper year near makes the carter's load half again as big");
        C(HamletMath.WordFamily(HamletNeed.Doctor) == "Sick" && HamletMath.WordFamily(HamletNeed.Food) == "Hungry" && HamletMath.WordFamily(HamletNeed.Tools) == "Damage",
            "the carter's words by the need");
        foreach (var fam in new[] { "Sick", "Hungry", "Damage" })
            foreach (var k in new[] { "CC_Hamlet_Word_" + fam, "CC_Hamlet_Word_" + fam + "_2" })
                C(keyed.TryGetValue(k, out var v) && OneSentence(v), $"{k} is one spoken sentence");
        C(keyed.ContainsKey("CC_Hamlet_Harvest_Bumper") && keyed.ContainsKey("CC_Hamlet_Region_At") && keyed.ContainsKey("CC_Hamlet_Region_InRegion"), "the bumper load and the places have words");
        string hamlet = Code(Path.Combine("World", "Region", "HamletLife.cs"));
        C(hamlet.Contains("HamletMath.HelpDue(p, h.helpAt, h.founded, now, hard != null)") && hamlet.Contains("HamletMath.NeedFor(HamletMath.RegionNeed(hard?.def?.defName), Rand.Value, h.lastNeed)")
          && hamlet.Contains("Speech.Say(carter, word)") && hamlet.Contains("CourierState.Working"),
            "the hamlet's day reads the region, the need follows it, and their carter at the gate says it");
        C(Code(Path.Combine("World", "Region", "Hamlet.cs")).Contains("\"hardEvent\"") && Code(Path.Combine("World", "Region", "RegionBook.cs")).Contains("Hamlets.CarterWord(now)"),
            "the year an ask followed is saved, and the region's hour has the carter speak");

        // ================= B13. Signs of the raiders' camp on the map =================

        int hour = RaidersCampSignsMath.HourTicks;
        bool timesFit = true;
        foreach (float w in new[] { 0f, 0.3f, 0.7f, 1f, float.NaN })
            foreach (float l in new[] { 0f, 0.3f, 0.7f, 1f, float.NaN })
            {
                timesFit &= RaidersCampSignsMath.GoneBefore(w, l);
                timesFit &= RaidersCampSignsMath.WatchTicks(w) >= hour && RaidersCampSignsMath.WatchTicks(w) <= 1.5f * hour;
                timesFit &= RaidersCampSignsMath.LeadTicks(l) >= hour && RaidersCampSignsMath.LeadTicks(l) <= 2.5f * hour;
            }
        C(timesFit, "the scouts watch an hour or so and are gone before the raid, which comes an hour or two after they were seen");
        C(RaidersCampSignsMath.Scouts == 2, "two scouts");
        C(!RaidersCampSignsMath.Due(-1, now) && RaidersCampSignsMath.Due(now, now) && !RaidersCampSignsMath.Due(now + 1, now)
          && !RaidersCampSignsMath.TooLate(now, now + RaidersCampSignsMath.LateHours * hour) && RaidersCampSignsMath.TooLate(now, now + RaidersCampSignsMath.LateHours * hour + 1),
            "the raid is due when its time comes, and given up half a day late");
        int foundAt = now - RaidersCampSignsMath.RunawayAfterDays * day;
        C(RaidersCampSignsMath.RunawayMay(false, CampStage.Found, foundAt, false, now) && !RaidersCampSignsMath.RunawayMay(true, CampStage.Found, foundAt, false, now)
          && !RaidersCampSignsMath.RunawayMay(false, CampStage.Found, foundAt, true, now) && !RaidersCampSignsMath.RunawayMay(false, CampStage.Found, foundAt + 1, false, now)
          && !RaidersCampSignsMath.RunawayMay(false, CampStage.Hidden, foundAt, false, now) && !RaidersCampSignsMath.RunawayMay(false, CampStage.Found, -1, false, now),
            "a runaway once in a camp's life, some days after it is found, never during its raid");
        C(RaidersCampSignsMath.Mtb(10f, true) == 10f * RaidersCampSignsMath.SoonerFactor && RaidersCampSignsMath.Mtb(10f, false) == 10f
          && float.IsInfinity(RaidersCampSignsMath.Mtb(float.PositiveInfinity, true)) && RaidersCampSignsMath.SoonerFactor > 0f && RaidersCampSignsMath.SoonerFactor < 1f,
            "taken in, the camp's next raid comes sooner (and a camp that never raids still doesn't)");
        C(RaidersCampSignsMath.SmokeDue(RaidersCampSignsMath.DuskHour, -1, now) && !RaidersCampSignsMath.SmokeDue(RaidersCampSignsMath.DuskHour - 1, -1, now)
          && !RaidersCampSignsMath.SmokeDue(RaidersCampSignsMath.DuskHour, now - day, now) && RaidersCampSignsMath.SmokeDue(RaidersCampSignsMath.DuskHour, now - RaidersCampSignsMath.SmokeGapDays * day, now),
            "the smoke is spoken of at dusk, every few days at most");
        var runaway = Def("IncidentDef", "Rimfolk_Evt_CampRunaway");
        C(runaway != null && (string)runaway.Element("baseChance") == "0" && (string)runaway.Element("workerClass") == "ColonyCouncil.HappeningWorker_CampRunaway",
            "the runaway's event comes only from its camp, never from the storyteller");
        var takeIn = runaway?.Descendants("options").FirstOrDefault()?.Elements("li").FirstOrDefault();
        var effects = takeIn?.Element("effects")?.Elements("li").Select(e => (string)e.Attribute("Class") ?? "").ToList() ?? new List<string>();
        C(effects.Contains("ColonyCouncil.DilemmaEffect_CampRunaway") && effects.Contains("ColonyCouncil.DilemmaEffect_Remember")
          && takeIn.Descendants("kind").Any(k => k.Value == "rescued")
          && takeIn.Element("effects").Elements("li").Any(e => (string)e.Attribute("Class") == "ColonyCouncil.DilemmaEffect_Goodwill" && (string)e.Element("amount") == RaidersCampSignsMath.RunawayGoodwill.ToString()),
            "taken in, the runaway goes to the colony's beds, their people remember it and think better of the colony");
        C(runaway?.Descendants("texts").SelectMany(t => t.Elements("li")).Count() >= 3 && runaway.Descendants("texts").SelectMany(t => t.Elements("li")).All(t => t.Value.Contains("{TALE}") && t.Value.Contains("{PLACE}")),
            "the runaway's letters say which camp and where");
        C(Def("DutyDef", "Rimfolk_CampScoutWatch") != null, "the scouts' watch is a duty of its own");
        string memory = Code(Path.Combine("World", "FactionMemory.cs"));
        C(Regex.IsMatch(memory, @"\[""rescued""\] = K\(DeedSide\.Favor, \d+") && Code(Path.Combine("Diplomacy", "FavourMath.cs")).Contains("\"rescued\"")
          && new[] { "CC_Memory_Deed_rescued", "CC_Memory_Deed_rescued_Name", "CC_Memory_Deed_rescued_Many", "CC_Dip_For_rescued" }.All(keyed.ContainsKey),
            "their people remember a runaway taken in as a good turn, in words");
        foreach (var k in new[] { "CC_RaidCamp_Smoke", "CC_RaidCamp_Smoke_2", "CC_RaidCamp_Smoke_3" })
            C(keyed.TryGetValue(k, out var v) && OneSentence(v) && v.Contains("{DIR}"), $"{k} is one spoken sentence with the camp's direction");
        foreach (var k in new[] { "CC_RaidCamp_Scouts", "CC_RaidCamp_Scouts_2", "CC_RaidCamp_RaidScouted" })
            C(keyed.TryGetValue(k, out var v) && v.Contains("{SIDE}"), $"{k} names the side");
        string camp = Code(Path.Combine("World", "Region", "RaidersCamp.cs"));
        string signs = Code(Path.Combine("World", "Region", "RaidersCampSigns.cs"));
        C(camp.Contains("&& !Scout(home, c, now)") && camp.Contains("DueRaid(home, c, now)") && camp.Contains("Smoke(home, c, now)") && camp.Contains("Runaway(home, c, now)")
          && camp.Contains("RaidersCampSignsMath.Mtb(") && camp.Contains("\"raidCell\""),
            "the camp's hour sends its scouts before each raid and the raid after them, its day the runaway, and the signs are saved");
        C(signs.Contains("JobDefOf.Wait_MaintainPosture") && !signs.Contains("Wait_Combat") && signs.Contains("LordToil_ExitMapAndDefendSelf") && signs.Contains("Trigger_PawnHarmed")
          && signs.Contains("AddFleeToil => false"),
            "the scouts never start a fight: they stand and watch, and hurt, they leave defending themselves");

        // ================= Those taken in (B13's runaway, B27's wounded) =================

        bool severities = true;
        foreach (float roll in new[] { 0f, 0.5f, 1f, float.NaN })
            foreach (float hp in new[] { 4f, 10f, 30f, 0f })
            {
                float s = TakenInMath.Severity(roll, hp);
                severities &= s <= TakenInMath.SeverityMax && s <= hp * TakenInMath.PartShare + 1e-5f && s >= 0f && (hp * TakenInMath.PartShare < TakenInMath.SeverityMin || s >= TakenInMath.SeverityMin);
            }
        C(severities, "a wound from before is never worse than its share of the part (nothing lost)");
        C(TakenInMath.Well(false, false, false) && !TakenInMath.Well(true, false, false) && !TakenInMath.Well(false, true, false) && !TakenInMath.Well(false, false, true),
            "well again: nothing to tend, no bed rest, on their feet");
        C(TakenInMath.GoesHome(true, false, now, now) && !TakenInMath.GoesHome(true, true, now, now) && !TakenInMath.GoesHome(false, false, now, now + day)
          && TakenInMath.GoesHome(false, false, now, now + TakenInMath.StayDays * day) && !TakenInMath.GoesHome(false, true, now, now + 100 * day),
            "they walk home when well, or after their days here if they can walk, never while down");
        C(Enumerable.Range(0, 11).Select(i => TakenInMath.ArriveTicks(i / 10f)).All(t => t >= TakenInMath.ArriveDaysMin * day && t <= TakenInMath.ArriveDaysMax * day),
            "the wounded come a day to three after the colony offered its beds");
        C(TakenInMath.MinBleedDays >= 1f, "no wound from before bleeds them out before anyone can tend it");
        string takenIn = Code(Path.Combine("World", "Region", "TakenIn.cs"));
        C(takenIn.Contains("p.health.Downed") && takenIn.Contains("TicksUntilDeathDueToBloodLoss") && takenIn.Contains("p.health.RemoveHediff(h)")
          && takenIn.Contains("GetNamedSilentFail") && !takenIn.Contains("HediffDefOf.Gunshot"),
            "the wounds from before leave them on their feet, by the game's own wounds found by name");
        C(takenIn.Contains("FactionMemory.Note(f, \"tended\"") && takenIn.Contains("KinMemory.Credit(") && Code(Path.Combine("World", "Patch_FactionMemory.cs")).Contains("TakenIn.Credited("),
            "their people remember who tended them, once");

        // ================= B27. Answer a call to arms from home =================

        C(CallsFromHomeMath.FoodKind(CallsFromHomeMath.Food, 0) == "rawfood" && CallsFromHomeMath.FoodKind(CallsFromHomeMath.Food - 1, CallsFromHomeMath.Meals) == "meals"
          && CallsFromHomeMath.FoodKind(CallsFromHomeMath.Food - 1, CallsFromHomeMath.Meals - 1) == null,
            "raw food when the stores hold enough, else meals, else none");
        C(CallsFromHomeMath.FoodCount("meals") == CallsFromHomeMath.Meals && CallsFromHomeMath.FoodCount("rawfood") == CallsFromHomeMath.Food, "how much food goes");
        C(CallsFromHomeMath.SuppliesEnough(CallsFromHomeMath.Food, 0, CallsFromHomeMath.Medicine) && !CallsFromHomeMath.SuppliesEnough(CallsFromHomeMath.Food, 0, CallsFromHomeMath.Medicine - 1)
          && !CallsFromHomeMath.SuppliesEnough(0, 0, 50), "supplies need food and medicine; the weapons only as many as there are");
        C(CallsFromHomeMath.ArmsSent(0) == 0 && CallsFromHomeMath.ArmsSent(1) == 1 && CallsFromHomeMath.ArmsSent(9) == CallsFromHomeMath.Arms && CallsFromHomeMath.ArmsSent(-3) == 0,
            "at most two spare weapons");
        C(CallsFromHomeMath.PaysSilver(true, 1) && CallsFromHomeMath.PaysSilver(true, 3) && !CallsFromHomeMath.PaysSilver(true, 0) && !CallsFromHomeMath.PaysSilver(false, 2),
            "silver in place of service is the liege's call, from the medieval age on (a tribe sends supplies)");
        C(new[] { 0f, 40000f, 200000f, 1e6f }.All(w => CallsFromHomeMath.Silver(w) == FealtyMath.Tribute(w)), "the silver is a season's tribute");
        int keptWord = int.Parse(Regex.Match(memory, @"\[""keptWord""\] = K\(DeedSide\.Favor, (\d+)").Groups[1].Value);
        int levies = int.Parse(Regex.Match(memory, @"\[""levies""\] = K\(DeedSide\.Favor, (\d+)").Groups[1].Value);
        C(CallsFromHomeMath.KeptWeight * 2 == keptWord && CallsFromHomeMath.LeviesWeight * 2 == levies, "kept from home, the word counts half that of fighters sent");
        var counts = Enumerable.Range(0, 101).Select(i => CallsFromHomeMath.Wounded(i / 100f)).ToList();
        C(counts.Min() == TakenInMath.WoundedMin && counts.Max() == TakenInMath.WoundedMax && CallsFromHomeMath.Wounded(float.NaN) == TakenInMath.WoundedMin,
            "two to four of their wounded come");
        foreach (var k in new[] { "CC_Arms_FromHome", "CC_Arms_FromHome_Silver", "CC_Arms_Opt_Supplies", "CC_Arms_Opt_Silver", "CC_Arms_Opt_Wounded", "CC_Arms_NoDoctor",
                     "CC_Arms_Msg_Supplies", "CC_Arms_Msg_Silver", "CC_Arms_Msg_Wounded", "CC_Arms_Wounded_Label", "CC_Arms_Wounded_Text", "CC_Arms_Hist_Supplies",
                     "CC_Arms_Hist_Silver", "CC_Arms_Hist_Wounded", "CC_Play_Arms", "CC_TakenIn_Home", "CC_TakenIn_HomeHurt", "CC_TakenIn_Died", "CC_TakenIn_Impact" })
            C(keyed.ContainsKey(k), $"{k} has its words");
        string arms = Code(Path.Combine("Diplomacy", "CallsToArms.cs"));
        string fromHome = Code(Path.Combine("Diplomacy", "CallsFromHome.cs"));
        C(arms.Contains("CallsFromHome.LetterText(") && arms.Contains("CallsFromHome.Send(") && arms.Contains("CallsFromHome.OfferBeds(") && arms.Contains("CallsFromHome.WhyNot(")
          && arms.Contains("CallsFromHome.WoundedWhyNot("),
            "the call's letter offers both answers from home, greyed out with the reason when they can't be given");
        C(Regex.Matches(fromHome, @"new DilemmaEffect_CarryOut \{[^}]*to = PlaySpot\.Gate").Count >= 2 && fromHome.Contains("TakenIn.Expect(") && fromHome.Contains("CallsToArms.Close(t, pact, Tie.State.Kept)"),
            "the supplies and the silver are carried to the gate for their runners, their wounded come to the colony's beds, and the call is kept");
        C(Code(Path.Combine("PlayOut", "PlayCarry.cs")).Contains("Biocoded") && Code(Path.Combine("PlayOut", "PlayCarry.cs")).Contains("Precept_Relic"),
            "a spare weapon is never one biocoded, bonded or a relic");

        // ================= B19. Camps' herds, dogs and goods from the full mod pool =================

        XElement Camp(string name) => Def("ColonyCouncil.CampKindDef", name) ?? Def("CampKindDef", name);
        List<(string def, string mod)> Items(XElement list) =>
            list?.Elements("li").Select(li => (li.Value.Trim(), (string)li.Attribute("MayRequire"))).ToList() ?? new List<(string, string)>();
        bool FirstModThenGame(IEnumerable<XElement> entries, string def, string mod) =>
            entries.Any(e =>
            {
                var items = Items(e.Element("defs"));
                int i = items.FindIndex(x => x.def == def);
                return i >= 0 && items[i].mod == mod && items.Skip(i + 1).Any(x => x.mod == null);
            });
        var salt = Camp("Council_Camp_SaltCaravan");
        C(salt != null && FirstModThenGame(salt.Element("campGoods").Elements("li"), "VCE_Salt", "vanillaexpanded.vcooke")
          && FirstModThenGame(salt.Element("traderGoods").Elements("li"), "VCE_Salt", "vanillaexpanded.vcooke"),
            "the salt caravan carries salt at its stall and in its trader's pack (Vanilla Cooking Expanded's, else salted fare)");
        var market = Camp("Council_Camp_Market");
        C(market != null && FirstModThenGame(market.Element("campGoods").Elements("li"), "VFEM2_Honey", "oskarpotocki.vfe.medieval2"),
            "the market's stall has raw honey (VFE Medieval 2's or Alpha Bees', else berries)");
        var herbs = Camp("Rimfolk_Camp_HerbGatherers");
        C(herbs != null && FirstModThenGame(herbs.Element("campGoods").Elements("li"), "VBE_RawTea", "vanillaexpanded.vbrewe"),
            "the herb-gatherers carry tea leaves (Vanilla Brewing Expanded's, else berries)");
        List<(string def, string mod)> Kinds(XElement camp) =>
            camp?.Element("animals")?.Elements("li").SelectMany(li => Items(li.Element("kinds"))).ToList() ?? new List<(string, string)>();
        var hermit = Kinds(Camp("Council_Camp_Hermit"));
        C(hermit.FirstOrDefault().def == "Cat" && hermit.Any(k => k.def.StartsWith("AEXP_Cat") && k.mod == "vanillaexpanded.vanillaanimalsexpanded"), "the hermit has a cat, the game's or Vanilla Animals Expanded's");
        var allKinds = defFiles.Where(kv => kv.Key.Contains(Path.Combine("Defs", "CampDefs"))).SelectMany(kv => kv.Value.Root.Elements())
            .SelectMany(camp2 => camp2.Element("animals")?.Elements("li") ?? Enumerable.Empty<XElement>()).Select(li => Items(li.Element("kinds"))).ToList();
        foreach (var (def, mod) in new[] { ("Aurochs", "spino.megafauna"), ("AA_ChameleonYak", "sarg.alphaanimals"), ("AA_Thunderox", "sarg.alphaanimals"),
                     ("VCE_Megareindeer", "ve.vanillachristmasexpanded"), ("AEXP_Camel", "vanillaexpanded.vanillaanimalsexpanded"),
                     ("AEXP_IndianElephant", "vanillaexpanded.vanillaanimalsexpanded"), ("AEXP_GermanShepherd", "vanillaexpanded.vanillaanimalsexpanded"),
                     ("AEXP_Beagle", "vanillaexpanded.vanillaanimalsexpanded"), ("AEXP_FrenchBulldog", "vanillaexpanded.vanillaanimalsexpanded") })
        {
            var lists = allKinds.Where(l => l.Any(k => k.def == def)).ToList();
            C(lists.Count > 0 && lists.All(l => l.Where(k => k.def == def).All(k => k.mod == mod) && l.Any(k => k.mod == null)),
                $"{def} comes with some camp, only with its mod, and the game's own beasts in each list are the fallback");
        }
        C(Code(Path.Combine("Camps", "CampKindDef.cs")).Contains("public List<CampGoods> traderGoods") && Code(Path.Combine("Camps", "CampTracker.cs")).Contains("c.kind.traderGoods"),
            "a camp's trader always carries its own goods");
        C(Code(Path.Combine("Camps", "CampLook.cs")).Contains("if (g.follow) return opts.RandomElement();"), "a camp's dog is any that fits, the game's or the mods'");

        return n;
    }
}
