using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Rimfolk's own peoples, release E (Peoples/PeoplesEMath.cs, PeoplesE.cs, PeoplesE2.cs, PeoplesEPatches.cs,
/// Camps/CampContract.cs; docs/design/peoples.md, "Release E"; docs/design/peoples-next.md): the Stormglass Conclave, the
/// Mossborn, the Ashen Banner and the Kiln Guild. Their pure rules (the watch on the sky and what it holds back, the rod,
/// the lesson; the ledger of the colony's trees, the saplings, the grave's tree, the Gauranlen tree tended; the squad's
/// wages by the age and its contract; the guild's pay by quality and its bids), their defs (none an enemy by nature, once
/// each in a new world, every age, the Banner's one kit), their arms and kits for another age, their towns' plans, their
/// callers at the gate and their asks, the squad's camp, the patch, release B's arising with them, their words and page,
/// and the seams. Release A's checks that every people meets run over all fifteen in PeoplesTests, PeoplesGearTests and
/// PeoplesLoreTests.
/// </summary>
public static class PeoplesETests
{
    private static readonly string[] Four = { "seers", "grovekeepers", "mercenaries", "artisans" };

    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "peoples, release E: " + what);
        }
        const int D = PeoplesEMath.DayTicks;

        // ================= The Stormglass Conclave =================

        C(new[] { "ColdSnap", "HeatWave", "ToxicFallout", "VolcanicWinter", "Flashstorm", "Eclipse" }.All(PeoplesEMath.Foreseen) && PeoplesEMath.Foreseen("VEE_Drought")
          && !PeoplesEMath.Foreseen("RaidEnemy") && !PeoplesEMath.Foreseen("Aurora") && !PeoplesEMath.Foreseen(null), "the glass foresees the sky's conditions, and nothing else");
        C(PeoplesEMath.HoldHours(0f) == 18 && PeoplesEMath.HoldHours(0.9999f) == 30 && PeoplesEMath.HoldHours(float.NaN) == 18
          && Enumerable.Range(0, 100).All(i => PeoplesEMath.HoldHours(i / 100f) is int h && h >= 18 && h <= 30) && PeoplesEMath.RetryHours == 12,
            "a condition held back a day or so, the warning first, and the game may bring it a while after");
        C(PeoplesEMath.WatchFee(0f) == 80 && PeoplesEMath.WatchFee(150000f) == 180 && PeoplesEMath.WatchFee(1e9f) == 400 && PeoplesEMath.WatchFee(float.NaN) == 80
          && PeoplesEMath.WatchFee(33333f) % 10 == 0 && PeoplesEMath.WatchDays == 15, "a season's watch, 80 to 400 silver by the colony's wealth");
        C(PeoplesEMath.Offset("ColdSnap") == -20f && PeoplesEMath.Offset("HeatWave") == 17f && PeoplesEMath.Offset("VolcanicWinter") == -7f && PeoplesEMath.Offset("Eclipse") == 0f,
            "the fields reckon a held cold snap, heat wave or volcanic winter by the game's own offsets");
        PeoplesEMath.Span(10 * PeoplesEMath.HourTicks, 0, 2f, 72, out int from, out int hours);
        PeoplesEMath.Span(0, 5, 3f, 24, out int from2, out int hours2);
        PeoplesEMath.Span(100 * PeoplesEMath.HourTicks, 0, 2f, 72, out _, out int hours3);
        C(from == 10 && hours == 48 && from2 == 0 && hours2 == 24 && hours3 == 0, "a held condition in the forecast from its hour, for its usual length, within the forecast");
        C(PeoplesEMath.Draws(0, 0) && PeoplesEMath.Draws(18, 0) && !PeoplesEMath.Draws(13, 13) && PeoplesEMath.Draws(12, 12) && PeoplesEMath.RodRadius == 18f,
            "the rod takes the lightning within eighteen cells of it");
        C(Math.Abs(PeoplesEMath.Taught(0.5f) - 0.65f) < 0.001f && PeoplesEMath.Taught(0.9f) == PeoplesEMath.TaughtMax && PeoplesEMath.Taught(float.NaN) == PeoplesEMath.TaughtMax
          && PeoplesEMath.TaughtMax < 1f && PeoplesEMath.TaughtDays == 60, "a reader taught the glass is nearer the truth for a season, never certain");

        // ================= The Mossborn =================

        C(PeoplesEMath.Shift(3, 4) == 0 && PeoplesEMath.Shift(0, 30) == 5 && PeoplesEMath.Shift(5, 15) == 3 && PeoplesEMath.Shift(25, 5) == -3 && PeoplesEMath.Shift(70, 0) == -6
          && PeoplesEMath.Shift(20, 15) == 0 && PeoplesEMath.Shift(-5, -5) == 0, "a season's trees: warmer for planting, cooler for the axe, nothing for a few");
        C(PeoplesEMath.Kept(50, -6) == -6 && PeoplesEMath.Kept(4, -6) == -4 && PeoplesEMath.Kept(0, -3) == 0 && PeoplesEMath.Kept(-20, -3) == 0 && PeoplesEMath.Kept(-20, 5) == 5,
            "cooler, never an enemy by it");
        C(PeoplesEMath.Saplings(12) == 6 && PeoplesEMath.Saplings(30) == 10 && PeoplesEMath.Saplings(300) == 14 && PeoplesEMath.Saplings(-4) == 6 && PeoplesEMath.SaplingsAfter == 12
          && PeoplesEMath.ReckonDays == 15, "a seed-carrier after a dozen felled, six to fourteen saplings; the ledger every fifteen days");
        C(PeoplesEMath.GroveFresh(0) && PeoplesEMath.GroveFresh(10 * D) && !PeoplesEMath.GroveFresh(10 * D + 1) && !PeoplesEMath.GroveFresh(-1), "a tree for one who died within ten days");
        C(Math.Abs(PeoplesEMath.Tended(0.5f) - 0.65f) < 0.001f && PeoplesEMath.Tended(0.95f) == 1f && PeoplesEMath.Tended(float.NaN) == PeoplesEMath.TendStrength,
            "the Gauranlen tree's bond the stronger, never past full");

        // ================= The Ashen Banner =================

        C(PeoplesEMath.Wage(4, 1) == 240 && PeoplesEMath.Wage(3, 0) == 110 && PeoplesEMath.Wage(5, 3) == 720 && PeoplesEMath.Wage(0, 0) == 60
          && Enumerable.Range(0, 3).All(a => PeoplesEMath.Wage(4, a + 1) > PeoplesEMath.Wage(4, a)), "a wage for the squad by the age it lives in: its kit is the age's");
        C(!PeoplesEMath.WageDue(0f, 0) && !PeoplesEMath.WageDue(4.9f, 0) && PeoplesEMath.WageDue(5f, 0) && PeoplesEMath.WageNumber(5f) == 1 && PeoplesEMath.WageDue(10f, 0)
          && PeoplesEMath.WageNumber(10f) == 2 && !PeoplesEMath.WageDue(15f, 0) && !PeoplesEMath.WageDue(7f, 1) && PeoplesEMath.WageNumber(float.NaN) == 0,
            "the first wage at hiring, then asked on the fifth and the tenth day, none at the season's end");
        C(PeoplesEMath.SquadMin == 3 && PeoplesEMath.SquadMax == 5 && PeoplesEMath.HireDays == 15 && PeoplesEMath.ContractGoodwill > 0 && PeoplesEMath.UnpaidGoodwill < -PeoplesEMath.ContractGoodwill
          && PeoplesEMath.DeserterTaken < 0 && PeoplesEMath.DeserterTaken > PeoplesEMath.UnpaidGoodwill, "three to five for the season; a broken contract weighs more than a kept one");
        C(PeoplesEMath.RollFor(0, 30 * D) && !PeoplesEMath.RollFor(0, 30 * D + 1) && !PeoplesEMath.RollFor(-1, 0) && !PeoplesEMath.RollFor(100, 50), "the Roll read for one who fell within the month");

        // ================= The Kiln Guild =================

        C(Enumerable.Range(0, 6).All(q => PeoplesEMath.QualityFactor(q + 1) > PeoplesEMath.QualityFactor(q)) && PeoplesEMath.QualityFactor(0) == 0.5f && PeoplesEMath.QualityFactor(6) > 3f,
            "the guild pays more the better the work, three times and more for a legend");
        C(PeoplesEMath.GuildPay(100f, 3) == 150 && PeoplesEMath.GuildPay(10f, 0) == 40 && PeoplesEMath.GuildPay(float.NaN, 6) == 40 && PeoplesEMath.GuildPay(133f, 4) % 5 == 0,
            "its pay to the nearest five, 40 at least");
        C(PeoplesEMath.Bid(1000f, 4) == 0 && PeoplesEMath.Bid(1000f, 5) == 1800 && PeoplesEMath.Bid(1000f, 6) == 2400 && PeoplesEMath.Bid(50f, 5) == 200 && PeoplesEMath.BidQuality == 5,
            "bids on masterworks and legends only, well over their worth");
        C(PeoplesEMath.WanderMinDays == 4f && PeoplesEMath.WanderMaxDays == 6f && PeoplesEMath.BenchHours == 2 && PeoplesEMath.LeftQuality == 3, "the wander-year: some days at the bench, a good piece left");

        // ================= Their defs =================

        string peopleDir = Path.Combine(root, "Defs", "PeopleDefs");
        var all = Directory.GetFiles(peopleDir, "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements()).ToList();
        string Dn(XElement e) => e.Element("defName")?.Value ?? "";
        XElement Ext(XElement d, string cls) => d.Element("modExtensions")?.Elements("li").FirstOrDefault(li => li.Attribute("Class")?.Value == "ColonyCouncil." + cls);
        float F(string v) => float.Parse(v, CultureInfo.InvariantCulture);
        var factions = all.Where(e => e.Name.LocalName == "FactionDef").ToDictionary(f => Ext(f, "PeopleExtension")!.Element("calling")!.Value);
        C(Four.All(factions.ContainsKey) && Dn(factions["seers"]) == "Rimfolk_Stormglass" && Dn(factions["grovekeepers"]) == "Rimfolk_Mossborn"
          && Dn(factions["mercenaries"]) == "Rimfolk_AshenBanner" && Dn(factions["artisans"]) == "Rimfolk_KilnGuild", "their defs");
        string Tech(string c) => factions[c].Element("techLevel")!.Value;
        C(Tech("seers") == "Industrial" && Tech("grovekeepers") == "Neolithic" && Tech("mercenaries") == "Medieval" && Tech("artisans") == "Medieval",
            "the Conclave industrial, the Mossborn tribal, the Banner and the guild medieval");
        var allDefs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).SelectMany(f => XDocument.Load(f).Root!.Elements()).ToList();
        var camps = allDefs.Where(e => e.Name.LocalName == "ColonyCouncil.CampKindDef").Select(Dn).ToHashSet();
        foreach (var c in Four)
        {
            var f = factions[c];
            var ext = Ext(f, "PeopleExtension")!;
            string name = Dn(f);
            C(f.Element("naturalEnemy")?.Value == "false" && f.Element("permanentEnemy")?.Value == "false" && f.Element("requiredCountAtGameStart")?.Value == "0"
              && f.Element("startingCountAtWorldCreation")?.Value == "1", $"{name}: nobody's enemy by nature, once in a new world, never required");
            C(F(f.Element("raidCommonalityFromPointsCurve")!.Descendants("li").Single().Value.Trim('(', ')').Split(',')[1]) <= 0.4f, $"{name}: raids rarely, and only when wronged");
            C(ext.Element("ariseAges")?.Value == "0~3", $"{name}: arises in every age");
            C(ext.Element("camps")!.Elements("li").Count() == 3 && ext.Element("camps")!.Elements("li").All(li => camps.Contains(li.Value)), $"{name}: three kinds of camp it favours, each a real one");
            C(f.Element("fixedIdeo")?.Value == "true" && !string.IsNullOrEmpty(f.Element("ideoName")?.Value) && f.Element("styles")!.Elements("li").All(li => li.Attribute("MayRequire")?.Value == "Ludeon.RimWorld.Ideology"),
                $"{name}: a faith of its own, and its style only with Ideology");
            var kinds = all.Where(e => e.Name.LocalName == "PawnKindDef" && Dn(e).StartsWith(name + "_")).ToList();
            C(kinds.Count == 5, $"{name}: five kinds of people by role");
        }
        C(factions["mercenaries"].Element("modExtensions")!.Descendants("herd").Any(), "the Banner's horses in their fort's stables");
        C(!factions["seers"].Element("forcedMemes")!.Elements("li").Any(li => li.Value == "VME_PsychicFocus"), "no meme that needs a DLC its MayRequire doesn't name (Vanilla Memes' psychic focus needs Royalty too)");

        // ================= The Banner's one kit, and the others' variety =================

        XElement Kind(string dn) => all.Single(e => e.Name.LocalName == "PawnKindDef" && Dn(e) == dn);
        List<string> Required(string dn) => Kind(dn).Element("apparelRequired")?.Elements("li").Select(li => li.Value).ToList() ?? new List<string>();
        var soldier = Required("Rimfolk_AshenBanner_Soldier");
        C(soldier.Contains("Apparel_PlateArmor") && soldier.Contains("Apparel_SimpleHelmet") && Required("Rimfolk_AshenBanner_Shot").Take(soldier.Count).SequenceEqual(soldier),
            "every soldier of the Banner in the same issued kit, shots too");
        var bannerArms = Ext(Kind("Rimfolk_AshenBanner_Soldier"), "PeopleKindExtension")!.Element("ageArms")!.Elements("li").ToList();
        C(bannerArms.Count == 3 && bannerArms.All(s => s.Element("apparel")?.Elements("li").Any() == true), "and in every other age the age's kit is issued with the age's arms");
        C(Required("Rimfolk_AshenBanner_Sergeant").Contains("Apparel_Sash") && Required("Rimfolk_AshenBanner_Captain").Contains("Apparel_Sash"), "the officers' black sash for the dead");
        string gifts = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Peoples", "PeopleGifts.cs"));
        C(gifts.Contains("Kit(p, set.apparel)") && gifts.Contains("ApparelUtility.CanWearTogether") && gifts.Contains("r.ApparelDef == def && r.Color != default"),
            "an age's kit worn over what clashes with it, in the people's colour");
        C(File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Peoples", "PeopleDefs.cs")).Contains("public List<ThingDef> apparel"), "a kit for an age in its arms");

        // Their arms for another age: real tags of the player's active mods, within what the set may cost.
        var catalogue = File.ReadAllLines(Path.Combine(root, "docs", "player-mods", "weapons.tsv")).Skip(1).Select(l => l.Split('\t')).Where(c => c.Length >= 9)
            .Select(c => (def: c[0], mod: c[1].ToLowerInvariant(), active: c[2] == "yes", tags: c[5].Split(',', StringSplitOptions.RemoveEmptyEntries), worth: c[8])).ToList();
        // The base game's weapons made of stuff carry no worth in the catalogue: their sum is in the gear table.
        var gearWorth = File.ReadAllLines(Path.Combine(root, "Tools", "Tests", "PeopleGear.txt")).Where(l => !l.StartsWith("#") && l.Contains('\t')).Select(l => l.Split('\t'))
            .Where(c => c.Length > 5 && c[1] == "weapon" && c[5] != "-").ToDictionary(c => c[2], c => F(c[5]));
        float Worth((string def, string mod, bool active, string[] tags, string worth) w) =>
            float.TryParse(w.worth, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0f ? v : gearWorth.TryGetValue(w.def, out var g) ? g : float.NaN;
        string[] names = factions.Where(kv => Four.Contains(kv.Key)).Select(kv => Dn(kv.Value) + "_").ToArray();
        var armed = all.Where(e => e.Name.LocalName == "PawnKindDef" && names.Any(p => Dn(e).StartsWith(p)) && Ext(e, "PeopleKindExtension")?.Element("ageArms") != null).ToList();
        C(armed.Count >= 5, $"their fighters' arms change with the age ({armed.Count})");
        foreach (var k in armed)
        {
            string kind = Dn(k);
            float budget = F(k.Element("weaponMoney")!.Value.Split('~')[1]);
            foreach (var set in Ext(k, "PeopleKindExtension")!.Element("ageArms")!.Elements("li"))
            {
                var ages = set.Element("ages")!.Value.Split('~').Select(int.Parse).ToArray();
                var tags = set.Element("weaponTags")!.Elements("li").ToList();
                float means = set.Element("money") != null ? F(set.Element("money")!.Value) : Math.Max(budget, 150f) * 1.25f;
                C(ages[0] >= 0 && ages[1] <= 3 && ages[0] <= ages[1] && tags.Count > 0, $"{kind}: arms for the ages {ages[0]} to {ages[1]}, with their tags");
                C(tags.All(li =>
                    {
                        string mod = li.Attribute("MayRequire")?.Value.ToLowerInvariant();
                        return catalogue.Any(w => w.active && w.tags.Contains(li.Value) && (mod == null ? w.mod == "ludeon.rimworld" : w.mod == mod));
                    }), $"{kind}: every tag a real one of the game's or of the mod it names, an active mod of the player's");
                C(catalogue.Any(w => w.active && tags.Any(t => w.tags.Contains(t.Value)) && Worth(w) <= means),
                    $"{kind}: a weapon of the ages {ages[0]} to {ages[1]} within what the set may cost ({means:0})");
                if (ages[1] <= 1)
                    C(tags.Any(li => li.Attribute("MayRequire") == null), $"{kind}: a tribal or medieval weapon of the base game");
            }
        }

        // ================= Their towns' plans =================

        var plans = XDocument.Load(Path.Combine(peopleDir, "Rimfolk_TownPlans_PeoplesE.xml")).Root!.Elements("ColonyCouncil.TownPlanDef").ToDictionary(Dn);
        List<string> Rows(string plan) => plans[plan].Element("rows")!.Elements("li").Select(li => li.Value).ToList();
        int Count(string plan, Func<XElement, bool> pick) =>
            plans[plan].Element("keys")!.Elements("li").Where(pick).Select(k => k.Element("key")!.Value[0]).Sum(ch => Rows(plan).Sum(r => r.Count(x => x == ch)));
        int Things(string plan, string thing) => Count(plan, k => k.Element("thing")?.Value == thing);
        C(plans.Keys.SequenceEqual(new[] { "Rimfolk_Plan_GlassTower", "Rimfolk_Plan_MossGrove", "Rimfolk_Plan_BannerFort", "Rimfolk_Plan_Guildhall" })
          && Four.All(c => plans.ContainsKey(Ext(factions[c], "PeopleExtension")!.Element("plan")!.Value)), "a plan for each of release E's peoples");
        C(Things("Rimfolk_Plan_GlassTower", "Bookcase") >= 4 && Things("Rimfolk_Plan_GlassTower", "SimpleResearchBench") >= 1 && Things("Rimfolk_Plan_GlassTower", "Column") >= 8
          && Count("Rimfolk_Plan_GlassTower", k => (k.Element("mark")?.Value ?? "").StartsWith("plant:")) >= 30, "the tower: its library of the record, the rods round it, the flower beds");
        C(Things("Rimfolk_Plan_MossGrove", "Grave") >= 10 && Count("Rimfolk_Plan_MossGrove", k => k.Element("mark")?.Value == "plant:Plant_TreeOak") >= 15
          && Count("Rimfolk_Plan_MossGrove", k => k.Element("mark")?.Value == "plant:Plant_Healroot") >= 20 && Things("Rimfolk_Plan_MossGrove", "Bed") == 0,
            "the grove: graves among young trees, healroot beds, bedrolls in the hall");
        C(Things("Rimfolk_Plan_BannerFort", "Bed") >= 8 && Things("Rimfolk_Plan_BannerFort", "Sandbags") >= 8 && Things("Rimfolk_Plan_BannerFort", "Bookcase") >= 2
          && Count("Rimfolk_Plan_BannerFort", k => k.Element("mark")?.Value == "pen") >= 40, "the fort: barracks, the hall of the Roll, the drill yard and the stables");
        C(Things("Rimfolk_Plan_Guildhall", "FueledSmithy") == 1 && Things("Rimfolk_Plan_Guildhall", "TableStonecutter") == 1 && Things("Rimfolk_Plan_Guildhall", "TableSculpting") == 1
          && Things("Rimfolk_Plan_Guildhall", "Table1x2c") >= 6, "the guildhall: smithy, stonecutter's and sculptor's tables, and the market's stalls");

        // ================= Their callers and asks =================

        var events = XDocument.Load(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeoplesE.xml")).Root!.Elements("IncidentDef").ToDictionary(Dn);
        C(events.Keys.SequenceEqual(new[] { "Rimfolk_Evt_GlassReading", "Rimfolk_Evt_GlassRod", "Rimfolk_Evt_GlassLesson", "Rimfolk_Evt_MossSaplings", "Rimfolk_Evt_MossGraveTree",
            "Rimfolk_Evt_MossTreeTend", "Rimfolk_Evt_BannerHire", "Rimfolk_Evt_BannerDeserter", "Rimfolk_Evt_BannerRoll", "Rimfolk_Evt_GuildCommission", "Rimfolk_Evt_GuildCollects",
            "Rimfolk_Evt_GuildBid" }), "twelve callers at the gate, three of each people");
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { src }.Concat(parts).ToArray()));
        string code = Read("Peoples", "PeoplesE.cs") + Read("Peoples", "PeoplesE2.cs"), hooks = Read("Peoples", "PeoplesEPatches.cs"), contract = Read("Camps", "CampContract.cs");
        string everyCode = string.Join("\n", Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        foreach (var (name, e) in events)
        {
            string worker = e.Element("workerClass")!.Value.Replace("ColonyCouncil.", "");
            C(code.Contains("class " + worker + " "), $"{name}: its worker is release E's own");
            var effects = e.Descendants("li").Select(li => li.Attribute("Class")?.Value).Where(v => v != null && v.StartsWith("ColonyCouncil.DilemmaEffect_")).Select(v => v.Replace("ColonyCouncil.", "")).Distinct();
            C(effects.All(x => Regex.IsMatch(everyCode, @"class " + x + @"\b")), $"{name}: every answer's effect is real");
            var ext = Ext(e, "HappeningExtension")!;
            C(ext.Element("options")!.Elements("li").Count() == 3 && e.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_SendAway"),
                $"{name}: three answers, one of them to send them on their way");
            C(F(e.Element("baseChance")!.Value) <= 2f && int.Parse(e.Element("minRefireDays")!.Value) >= (name == "Rimfolk_Evt_GuildCollects" ? 3 : 12),
                $"{name}: a small share of the callers, not soon again (the guild's journeyman back for a finished piece sooner)");
            string journal = ext.Element("journal")?.Value;
            C(journal == null || allDefs.Any(d => Dn(d) == journal), $"{name}: the colonists write about it in a journal that exists");
            var lineTags = ext.Element("lines")?.Elements("li").Select(l => Regex.Match(l.Value, @"^\[([^\],]+)").Groups[1].Value).Distinct().ToList() ?? new List<string>();
            C(lineTags.All(t => code.Contains("\"" + t + "\"") || t == "view" || t == "bought"), $"{name}: every line's moment is one its worker says it at");
        }
        bool Has(XElement d, string cls, string field = null, string value = null) =>
            d.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil." + cls && (field == null || li.Element(field)?.Value == value));
        C(Has(events["Rimfolk_Evt_GlassReading"], "DilemmaEffect_CarryOut", "what", "silver") && Has(events["Rimfolk_Evt_GlassReading"], "DilemmaEffect_GlassWatch")
          && Has(events["Rimfolk_Evt_GlassReading"], "DilemmaEffect_Visit", "to", "Stranger"), "the watch: the fee carried out to the reader, or a reading of tonight's sky at the gate");
        C(events["Rimfolk_Evt_GlassRod"].Descendants("li").Count(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_GlassRod") == 2
          && Has(events["Rimfolk_Evt_GlassRod"], "DilemmaEffect_Learn", "skill", "Construction"), "the rod set up by the storm-walker, the colony's builder beside them if it likes");
        C(Has(events["Rimfolk_Evt_GlassLesson"], "DilemmaEffect_Lesson", "teacher", "X") && Has(events["Rimfolk_Evt_GlassLesson"], "DilemmaEffect_Lesson", "skill", "Plants")
          && Has(events["Rimfolk_Evt_GlassLesson"], "DilemmaEffect_GlassTaught"), "the glass taught at dusk, and a season of reading it");
        C(events["Rimfolk_Evt_MossSaplings"].Descendants("li").Count(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_MossPlant") == 2,
            "saplings planted by the seed-carrier, alone or with the colony's planter");
        C(Has(events["Rimfolk_Evt_MossGraveTree"], "DilemmaEffect_MossGraveTree") && Has(events["Rimfolk_Evt_MossGraveTree"], "DilemmaEffect_Vigil", "to", "Grave")
          && Has(events["Rimfolk_Evt_MossGraveTree"], "DilemmaEffect_Thought", "thought", "Rimfolk_Evt_GraveTree"), "a sapling beside the grave, the colony gathered there if it likes");
        C(Ext(events["Rimfolk_Evt_MossTreeTend"], "HappeningExtension")!.Element("mods")!.Elements("li").Any(l => l.Value == "Ludeon.RimWorld.Ideology")
          && Has(events["Rimfolk_Evt_MossTreeTend"], "DilemmaEffect_MossTend"), "the Gauranlen tree tended only with Ideology");
        C(Has(events["Rimfolk_Evt_BannerHire"], "DilemmaEffect_CarryOut", "what", "silver") && Has(events["Rimfolk_Evt_BannerHire"], "DilemmaEffect_BannerHire")
          && Has(events["Rimfolk_Evt_BannerHire"], "DilemmaEffect_Lesson", "skill", "Melee"), "the squad hired, the first wage carried out, or a drill for the colony's best fighter");
        C(Has(events["Rimfolk_Evt_BannerDeserter"], "DilemmaEffect_Join") && Has(events["Rimfolk_Evt_BannerDeserter"], "DilemmaEffect_BannerDeserter")
          && Has(events["Rimfolk_Evt_BannerDeserter"], "DilemmaEffect_NightLodging"), "a deserter taken in, lodged for the night, or turned away");
        C(Has(events["Rimfolk_Evt_BannerRoll"], "DilemmaEffect_Vigil", "pool", "Rimfolk_Pool_PlayBannerRoll") && Has(events["Rimfolk_Evt_BannerRoll"], "DilemmaEffect_Vigil", "to", "Grave"),
            "the name read into the Roll at the grave, with words of its own");
        C(Has(events["Rimfolk_Evt_GuildCommission"], "DilemmaEffect_GuildCommission") && events["Rimfolk_Evt_GuildCollects"].Descendants("li")
              .Count(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_SellArt" && li.Element("commission")?.Value == "true") == 2
          && Has(events["Rimfolk_Evt_GuildCollects"], "DilemmaEffect_KeepCommission"), "a commission made at the bench, handed over to the guild for its pay, or kept");
        C(Has(events["Rimfolk_Evt_GuildBid"], "DilemmaEffect_SellArt") && Has(events["Rimfolk_Evt_GuildBid"], "DilemmaEffect_ArtWord"), "a masterwork sold to the guild, or kept and talked of");
        C(code.Contains("PlayMake.KeepMade(map, maker, def") && code.Contains("Callers.Commission(x, maker, def") && Read("Visitors", "Callers.cs").Contains("HappeningWorker_GuildCollects.Guilds(c)"),
            "the guild's commission made as the collector's is, and collected only by the guild");

        var missions = XDocument.Load(Path.Combine(root, "Defs", "MissionDefs", "Rimfolk_Missions_PeoplesE.xml")).Root!.Elements("IncidentDef").ToDictionary(Dn);
        string Only(string m) => Ext(missions[m], "MissionPeople")?.Element("only")?.Value;
        C(Only("Rimfolk_Mission_GlassApprentice") == "seers" && Only("Rimfolk_Mission_MossBurned") == "grovekeepers" && Only("Rimfolk_Mission_BannerMuster") == "mercenaries"
          && Only("Rimfolk_Mission_WanderYear") == "artisans", "an ask of each people, only theirs");
        C(missions["Rimfolk_Mission_GlassApprentice"].Element("workerClass")!.Value.EndsWith("MissionGlassApprentice") && missions["Rimfolk_Mission_WanderYear"].Element("workerClass")!.Value.EndsWith("MissionWanderYear")
          && code.Contains("class HappeningWorker_MissionGlassApprentice : HappeningWorker_MissionHost") && code.Contains("class HappeningWorker_MissionWanderYear : HappeningWorker_MissionHost"),
            "an apprentice of the glass and a journeyman's wander-year, guests hosted at the colony's table and beds");
        C(Ext(missions["Rimfolk_Mission_BannerMuster"], "MissionHands")!.Element("skill")?.Value == "Melee" && Ext(missions["Rimfolk_Mission_BannerMuster"], "MissionHands")!.Element("hands")?.Value == "2"
          && missions["Rimfolk_Mission_MossBurned"].Descendants("what").Select(w => w.Value).SequenceEqual(new[] { "rawfood", "medicine" }),
            "two fighters for the Banner's wall, food and medicine for a burned grove");
        C(missions.Values.All(m => F(m.Element("baseChance")!.Value) <= 0.4f && int.Parse(m.Element("minRefireDays")!.Value) >= 60), "a small share of the missions' one draw, not soon again");
        C(code.Contains("PeoplesE.Start(x, PeoplesE.SkyJob") && code.Contains("PeoplesE.Start(x, PeoplesE.BenchJob") && code.Contains("ThingRoles.From(ThingRole.SmallArt"),
            "the apprentice lies out under the sky, the journeyman works at the bench and leaves a small work of art of the guild's age");
        var thoughts = XDocument.Load(Path.Combine(root, "Defs", "ThoughtDefs", "Rimfolk_Thoughts_PeoplesE.xml")).Root!.Elements("ThoughtDef").Select(Dn).ToHashSet();
        C(new[] { "Rimfolk_Evt_GraveTree", "Rimfolk_Evt_NameOnRoll", "Rimfolk_Mission_StoodWithBanner" }.All(thoughts.Contains), "the colony's thoughts: the grave's tree, the name on the Roll, the wall");

        // ================= The squad's camp =================

        var squad = allDefs.Single(e => e.Name.LocalName == "ColonyCouncil.CampKindDef" && Dn(e) == "Rimfolk_Camp_BannerSquad");
        C(squad.Element("fights")?.Value == "true" && squad.Element("weight")?.Value == "0" && squad.Element("work")?.Value == "contract" && squad.Element("calling")?.Value == "mercenaries"
          && squad.Element("days")?.Value == PeoplesEMath.HireDays + "~" + PeoplesEMath.HireDays, "the squad's camp: never on its own, fights for the colony, the season's contract");
        var wage = squad.Element("asks")!.Elements("li").Single(a => a.Element("key")?.Value == "wage");
        C(wage.Element("theyAsk")?.Value == "true" && wage.Element("once")?.Value == "false" && wage.Element("price")?.Value == "wage" && wage.Element("needs")?.Value == "wageDue"
          && wage.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_BannerWage"), "the wage asked by letter, its price the squad's wage");
        C(contract.Contains("Request(ask)") && contract.Contains("BannerSquad.Unpaid(this)") && contract.Contains("Leave(\"unpaid\")") && contract.Contains("BannerSquad.Kept(this)")
          && Read("Camps", "CampAsks.cs").Contains("a?.price == ContractWagePrice ? ContractWage()") && Read("Camps", "CampKindsLand.cs").Contains("case \"contract\": return ContractJob(p);")
          && Read("Camps", "CampLife.cs").Contains("ending == \"unpaid\""), "unpaid they walk, kept they go content, the wage carried out as the ask's price, a sentry at the gate by day");
        C(Read("Camps", "CampLife.cs").Contains("if (kind.fights || guards || ally || defend || fighting)"), "a camp that fights takes up arms when the colony is attacked");

        // ================= The patch on Rimfolk's own defs =================

        var patch = XDocument.Load(Path.Combine(root, "Patches", "Rimfolk_PeoplesE.xml")).Root!;
        var defNames = allDefs.Select(Dn).Where(x => x != "").ToHashSet();
        foreach (var op in patch.Elements("Operation"))
        {
            var m = Regex.Match(op.Element("xpath")?.Value ?? "", "defName=\"([^\"]+)\"");
            C(op.Attribute("Class")?.Value == "PatchOperationConditional" && m.Success && defNames.Contains(m.Groups[1].Value), $"the patch reaches {m.Groups[1].Value}, conditionally");
        }
        C(patch.Descendants("calling").Select(c => c.Value).OrderBy(x => x).SequenceEqual(Four.OrderBy(x => x)), "four world events prefer them, one each");
        C(patch.Descendants("prefers").Select(c => c.Value).OrderBy(x => x).SequenceEqual(new[] { "artisans", "seers" }), "the art collector from the guild likeliest, the fortune teller from the Conclave");
        C(new[] { "seers", "artisans" }.All(p => patch.Descendants("li").Any(li => li.Value.StartsWith("[" + PeoplesMath.Tag(p)))), "each says it in that people's voice when they are the ones who came");
        C(Read("Happenings", "Workers_Variety.cs").Contains("ctx.faction = Arrivals.Friendly(null, Peoples.Weigh(ext.prefers));"), "the fortune teller weighs the calling it prefers");

        // ================= Release B's peoples that arise =================

        C(Four.All(c => ArisingMath.Callings(ArisingMath.Exiles).Contains(c)) && Four.All(c => !ArisingMath.Callings(ArisingMath.League).Contains(c) && !ArisingMath.Callings(ArisingMath.Refugees).Contains(c)),
            "a band of the banished may take any of their ways; a league or survivors none");
        C(ArisingMath.Lean(ArisingMath.Exiles, false, false, 0, 0, 0, false, false, 2, 0, 0, 0, 1) == "seers" && ArisingMath.Lean(ArisingMath.Exiles, false, false, 0, 0, 0, false, false, 2, 12, 0, 0, 0) == "mercenaries"
          && ArisingMath.Lean(ArisingMath.Exiles, false, false, 0, 0, 0, false, false, 2, 0, 0, 12, 0) == "artisans" && ArisingMath.Lean(ArisingMath.Exiles, false, false, 0, 0, 0, false, false, 2, 0, 12, 0, 0) == "grovekeepers"
          && ArisingMath.Lean(ArisingMath.Exiles, false, false, 12, 0, 0, false, false, 2, 12, 12, 12, 2) == "scholars" && ArisingMath.Lean(ArisingMath.Exiles, false, false, 0, 0, 0, false, false, 2, 9, 9, 9, 0) == null,
            "a band led by a psycaster reads the sky, by a fighter hires out, by a maker keeps a guild, by a grower a grove; the learned first; nothing else for a band");
        C(ArisingMath.Lean(ArisingMath.Exiles, false, false, 0, 9, 9, true, true, 3) == null, "release D's leans as they were");
        C(Read("Peoples", "Arising.cs").Contains("Skill(r.pawn, SkillDefOf.Melee), Skill(r.pawn, SkillDefOf.Plants), Skill(r.pawn, SkillDefOf.Crafting), Psylink(r.pawn)"), "the leader's own skills and psylink");
        var arise = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Peoples_Arising.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (var c in Four)
            C(arise.ContainsKey("CC_Arise_exiles_" + c) && arise.ContainsKey("CC_Arise_exiles_" + c + "_2") && new[] { "", "_2", "_3" }.All(s => arise.TryGetValue("CC_Arise_Name_exiles_" + c + s, out var v) && v.Contains("{WHAT}")),
                $"exiles_{c}: how a new people of these ways lives, told two ways, and three names for it");
        C(arise["CC_Arise_exiles_seers"].Contains("vial") && arise["CC_Arise_exiles_mercenaries"].Contains("company") && arise["CC_Arise_exiles_artisans"].Contains("mark")
          && arise["CC_Arise_exiles_grovekeepers"].Contains("seedling"), "and what it does is what its people do at the gate");

        // ================= Nobody hurt but by the game's own fights =================

        C(!Regex.IsMatch(code + hooks + contract, @"Harm\.(Roll|Injure|Ail|Hurt)\("), "nothing of theirs hurts anybody: the squad's fights are the game's own, against what attacks the colony");

        // ================= Their words and their page =================

        var almanac = XDocument.Load(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_PeoplesE.xml")).Root!.Elements().Single();
        C(Dn(almanac) == "Council_Alm_C_PeoplesE" && almanac.Element("keywords")!.Elements("li").Count() >= 4 && almanac.Element("seeAlso")!.Elements("li").Any(l => l.Value == "Council_Alm_C_PeoplesD"),
            "an Almanac page of their own, beside release D's");
        var almText = string.Join("\n", almanac.Descendants().Where(e => !e.HasElements).Select(e => e.Value));
        C(!almText.Contains('—') && !almText.Contains('!') && !Regex.IsMatch(almText, @"\bin \d+ (days?|hours?)\b"), "its words plain, no countdowns");
        C(almText.Contains("18 cells") && almText.Contains("every fifteen days") && almText.Contains("a dozen") && almText.Contains("fifth and the tenth day") && almText.Contains("half again")
          && almText.Contains("Hesper Lyle") && almText.Contains("Elin Sedge") && almText.Contains("Corra Mallin") && almText.Contains("Oswin Kell"), "its rules are the code's, and its foundings the defs'");
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeoplesE.xml")).Root!.Elements().Select(e => e.Name.LocalName).ToHashSet();
        var used = Regex.Matches(code + contract, "\"(CC_PeoplesE_\\w+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        C(used.Count >= 40 && used.All(keyed.Contains) && keyed.Contains("CC_Camp_End_unpaid"), $"every word the code says is keyed ({used.Count})");
        var eTexts = new[] { Path.Combine("Defs", "IncidentDefs", "Rimfolk_Events_PeoplesE.xml"), Path.Combine("Defs", "FlavorPoolDefs", "Rimfolk_Lines_PeoplesE.xml"),
                Path.Combine("Languages", "English", "Keyed", "Rimfolk_PeoplesE.xml"), Path.Combine("Defs", "MissionDefs", "Rimfolk_Missions_PeoplesE.xml"),
                Path.Combine("Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_PeoplesE.xml"), Path.Combine("Defs", "CampDefs", "Rimfolk_CampKinds_PeoplesE.xml") }
            .Select(p => File.ReadAllText(Path.Combine(root, p)).ToLowerInvariant()).Aggregate((a, b) => a + b);
        C(!Regex.IsMatch(eTexts, @"\b(dynasty|dynasties|bloodline|heir to the house)\b"), "the colony is the family: no houses, no bloodlines");

        // ================= The seams =================

        C(!Regex.IsMatch(code + Read("Peoples", "PeoplesEMath.cs") + contract, @"Sched\.|Scribe_|ExposeData"), "no clock and nothing saved of their own");
        C(Regex.Matches(hooks, @"\[HarmonyPatch\(").Count == 4 && Regex.Matches(hooks, @"RimfolkPatches\.On").Count >= 3, "four game hooks, each off with the deeper game hooks");
        C(hooks.Contains("typeof(Storyteller), nameof(Storyteller.TryFire)") && hooks.Contains("if (queued ||") && code.Contains("Find.Storyteller.incidentQueue.Add(new QueuedIncident(fi, at,"),
            "a condition the storyteller sends held in the game's own queue, never one already coming back out of it");
        C(hooks.Contains("typeof(WeatherEvent_LightningStrike)") && hooks.Contains("typeof(GenExplosion), nameof(GenExplosion.DoExplosion)") && hooks.Contains("public static void Finalizer()"),
            "the rod takes the strike, and no fire starts at it");
        C(hooks.Contains("typeof(Plant), nameof(Plant.PlantCollected)") && code.Contains("by?.Faction == null || !by.Faction.IsPlayer") && code.Contains("p.sown"),
            "the ledger counts the trees the colony's people cut, and the trees sown by hand");
        var book = Read("Peoples", "ArisingBook.cs");
        C(book.Contains("groveFelled") && book.Contains("Scribe_Values.Look(ref groveFelled") && Read("World", "Region", "RegionBook.cs").Contains("MossLedger.Daily(now)"),
            "the ledger's counts kept in the peoples' book, reckoned on the region's day");
        string happen = Read("Happenings", "Happenings.cs");
        C(happen.Contains("case GlassWatch.Kind:") && happen.Contains("case GlassWatch.RunnerKind:"), "the watch and its runners ride on the visits Rimfolk already keeps");
        C(Read("Fields", "FieldWeather.cs").Contains("GlassWatch.Ahead(map") && Read("Weather", "WeatherDynamics.cs").Contains("Rimfolk_StormglassRod")
          && Read("Weather", "WeatherDynamics.cs").Contains("PeoplesEMath.Taught("), "the fields reckon the held cold and heat; the rod a station, the glass a better forecast");
        C(File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Things", "StormglassRod.png")), "the rod has its own texture");
        C(Regex.Matches(code + contract, @"Speech\.Say\(").Count >= 3 && !code.Contains("MoteMaker.ThrowText"), "they speak through Rimfolk's speech");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
