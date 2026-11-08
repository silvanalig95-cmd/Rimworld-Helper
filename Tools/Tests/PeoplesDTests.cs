using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Rimfolk's own peoples, release D (Peoples/PeoplesDMath.cs, PeoplesD.cs, PeoplesD2.cs, WheelTowns.cs;
/// docs/design/peoples.md, "Release D"; docs/design/peoples-next.md): the Lantern Wardens, the Quiet Bell, the Circuit of
/// Tamers and the Wheel. Their pure rules (the age a people lives in, the hunt and the pelts, the bell and its funerals,
/// the bout by the Fighting-type spar's rules, the eggs and the strays, the scrap, the wreck and the towns that move), their
/// defs (none an enemy by nature, once each in a new world, every age), their arms for another age (real tags of the
/// player's active mods, soft, within what the set may cost), their towns' plans, their callers at the gate and their
/// asks, the patch, release B's arising with them, the tamers' bout as a kind of harm, their words and page, and the seams.
/// Release A's checks that every people meets run over all eleven in PeoplesTests, PeoplesGearTests and PeoplesLoreTests.
/// </summary>
public static class PeoplesDTests
{
    private static readonly string[] Four = { "hunters", "sextons", "tamers", "salvagers" };

    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "peoples, release D: " + what);
        }
        const int D = PeoplesDMath.DayTicks;

        // ================= The age a people lives in =================

        C(PeoplesDMath.AgeFits(0, -1, -1) && PeoplesDMath.AgeFits(3, -1, -1) && PeoplesDMath.AgeFits(1, 0, 1) && !PeoplesDMath.AgeFits(2, 0, 1)
          && PeoplesDMath.AgeFits(3, 3, 3) && !PeoplesDMath.AgeFits(2, 3, 3) && PeoplesDMath.AgeFits(3, 2, -1), "a gift or a kit of no age fits every age, else only its own");
        C(PeoplesDMath.PeopleAge(null, 0, 0, 1, 0, 3) == 1 && PeoplesDMath.PeopleAge(ArisingMath.League, 3, 3, 2, 0, 3) == 2, "a people of a template, or a league, lives in its template's age");
        C(PeoplesDMath.PeopleAge(ArisingMath.Refugees, 3, 0, 1, 0, 3) == 3 && PeoplesDMath.PeopleAge(ArisingMath.Splinter, 0, 3, 1, 0, 3) == 0
          && PeoplesDMath.PeopleAge(ArisingMath.Refugees, -1, 2, 1, 0, 3) == 1, "survivors and faithful who broke away keep the age of the people they came from");
        C(PeoplesDMath.PeopleAge(ArisingMath.Exiles, 3, 2, 1, 0, 3) == 2 && PeoplesDMath.PeopleAge(ArisingMath.Exiles, 0, 3, 1, 1, 2) == 2
          && PeoplesDMath.PeopleAge(ArisingMath.Exiles, 0, 9, 1, -1, -1) == 3, "a band of the banished the colony's, within the template's span");

        // ================= The Lantern Wardens =================

        C(PeoplesDMath.HuntersMin == 2 && PeoplesDMath.HuntersMax == 3 && PeoplesDMath.HuntDays == 2, "two or three Wardens, watching two days for the beast's end");
        C(PeoplesDMath.QuarryRank(true, true, true) == 3 && PeoplesDMath.QuarryRank(false, true, true) == 2 && PeoplesDMath.QuarryRank(false, false, true) == 1
          && PeoplesDMath.QuarryRank(false, false, false) == 0, "a legend of the region before a nemesis, a nemesis before a pack's alpha, nothing else");
        C(PeoplesDMath.SkinTicks == 5000 && PeoplesDMath.HideShare == 1f, "two hours at the carcass, and the hide is theirs");
        C(PeoplesDMath.Pelts(19) == 0 && PeoplesDMath.Pelts(20) == 20 && PeoplesDMath.Pelts(45) == 45 && PeoplesDMath.Pelts(300) == PeoplesDMath.PeltsMax && PeoplesDMath.PeltsMax == 60,
            "the pelt-buyer comes for twenty pelts at least, and buys sixty at most");
        C(PeoplesDMath.PeltPrice(30, 2f) == 70 && PeoplesDMath.PeltPrice(1, 1f) == 20 && PeoplesDMath.PeltPrice(0, 5f) == 0 && PeoplesDMath.PeltPrice(10, float.NaN) == 0
          && PeoplesDMath.PeltPrice(33, 3.1f) % 5 == 0 && PeoplesDMath.PeltRate > 1f && PeoplesDMath.PeltRate <= 1.25f, "a little over the pelts' worth, to the nearest five");
        C(PeoplesDMath.LanternFor(0, 30 * D) && !PeoplesDMath.LanternFor(0, 30 * D + 1) && !PeoplesDMath.LanternFor(-1, 5) && !PeoplesDMath.LanternFor(100, 50),
            "a lantern for one a beast took within the month");

        // ================= The Quiet Bell =================

        C(PeoplesDMath.GoodwillFor(0) == 0 && PeoplesDMath.GoodwillFor(1) == PeoplesDMath.BellGoodwill && PeoplesDMath.GoodwillFor(3) == PeoplesDMath.BellGoodwill + 1
          && PeoplesDMath.GoodwillFor(40) == PeoplesDMath.BellGoodwill + 4 && PeoplesDMath.BellPeoples == 3, "the raiders' own people think better of the colony, a little more for more of theirs, three peoples at most");
        C(PeoplesDMath.Fresh(0) && PeoplesDMath.Fresh(6 * D) && !PeoplesDMath.Fresh(6 * D + 1) && !PeoplesDMath.Fresh(-1), "a funeral for one who died within six days");
        C(!PeoplesDMath.VoidStirring(0, 99) && !PeoplesDMath.VoidStirring(1, 7) && PeoplesDMath.VoidStirring(1, 8) && PeoplesDMath.VoidStirring(2, 30), "the ground is listening: the void awake, and quiet a while");

        // ================= The Circuit of Tamers =================

        C(PeoplesDMath.BoutBlowChance == AnimalPowersMath.SparBlowChance && PeoplesDMath.BoutLow == AnimalPowersMath.SparLow && PeoplesDMath.BoutHigh == AnimalPowersMath.SparHigh
          && PeoplesDMath.BoutMinTicks == AnimalPowersMath.SparMinTicks && PeoplesDMath.BoutMaxTicks == AnimalPowersMath.SparMaxTicks
          && PeoplesDMath.BoutSwingTicks == AnimalPowersMath.SparSwingTicks && PeoplesDMath.BoutXpSwing == AnimalPowersMath.SparXpSwing,
            "the bout by the Fighting-type spar's own rules: its length, its swings, its practice, one in 25 a bruise of 3 to 6");
        C(Math.Abs(PeoplesDMath.WinChance(6, 6) - 0.2f) < 0.001f && PeoplesDMath.WinChance(0, 0) == 0.15f && PeoplesDMath.WinChance(20, 20) == 0.8f
          && Enumerable.Range(0, 20).All(s => PeoplesDMath.WinChance(s + 1, 8) >= PeoplesDMath.WinChance(s, 8) && PeoplesDMath.WinChance(8, s + 1) >= PeoplesDMath.WinChance(8, s)),
            "beating the tamer fairly: a fifth of the bouts for someone green, four in five at most, better with beasts and hands");
        C(PeoplesDMath.WinGoodwill > PeoplesDMath.LoseGoodwill && PeoplesDMath.LoseGoodwill > 0, "beaten fairly by a stranger is their highest honour; a good loss pleases them too");
        C(PeoplesDMath.EggPrice(20f, 2) == 60 && PeoplesDMath.EggPrice(1f, 1) == 20 && PeoplesDMath.EggPrice(10f, 0) == 0 && PeoplesDMath.EggPrice(float.NaN, 1) == 0, "an egg at half again its worth");
        C(PeoplesDMath.StrayRecent(0, 120 * D) && !PeoplesDMath.StrayRecent(0, 120 * D + 1) && !PeoplesDMath.StrayRecent(-1, 0), "a stray of the last four months brought home");
        C(PeoplesDMath.Finder(1000f) == 100 && PeoplesDMath.Finder(10f) == 15 && PeoplesDMath.Finder(float.NaN) == 15, "a tenth of its worth for the ranger's trouble, if the colony likes");

        // ================= The Wheel =================

        C(PeoplesDMath.Broken(0.39f, false) && !PeoplesDMath.Broken(0.4f, false) && PeoplesDMath.Broken(1f, true) && !PeoplesDMath.Broken(float.NaN, false),
            "scrap: worn under two fifths, or a dead man's");
        C(PeoplesDMath.ScrapPrice(10, 100f) == 55 && PeoplesDMath.ScrapPrice(0, 0f) == 0 && PeoplesDMath.ScrapPrice(1, 0f) == 10 && PeoplesDMath.ScrapPrice(-3, float.NaN) == 0
          && PeoplesDMath.ScrapMin == 6 && PeoplesDMath.ScrapMax == 30, "four silver a slag chunk and a share of the broken pieces' worth; six pieces at least");
        C(PeoplesDMath.WreckPrice(1000f) == 600 && PeoplesDMath.WreckPrice(5f) == 20 && PeoplesDMath.WreckPrice(float.NaN) == 0 && PeoplesDMath.WreckShare == 0.5f,
            "a wreck sold whole for three fifths of its parts, or stripped half and half");
        C(PeoplesDMath.Half(0) == 0 && PeoplesDMath.Half(1) == 0 && PeoplesDMath.Half(2) == 1 && PeoplesDMath.Half(7) == 3 && PeoplesDMath.StripTicks == 7500, "half of each part the colony's, three hours' stripping");
        var moves = Enumerable.Range(0, 50).Select(id => Enumerable.Range(0, PeoplesDMath.MoveEveryDays).Count(d => PeoplesDMath.MoveDay(d, id * 7919))).ToList();
        var firstDays = Enumerable.Range(0, 50).Select(id => Enumerable.Range(0, 30).First(d => PeoplesDMath.MoveDay(d, id * 7919))).Distinct().Count();
        C(moves.All(m => m == 1) && firstDays >= 15 && !PeoplesDMath.MoveDay(-1, 0) && Enumerable.Range(0, 30).Count(d => PeoplesDMath.MoveDay(d, -12345)) == 1,
            $"each Wheel town moves once a month, on a day of its own ({firstDays} days among 50 towns)");
        C(PeoplesDMath.MoveMin == 2 && PeoplesDMath.MoveMax == 5, "a few tiles along");

        // ================= Their defs =================

        string peopleDir = Path.Combine(root, "Defs", "PeopleDefs");
        var all = Directory.GetFiles(peopleDir, "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements()).ToList();
        string Dn(XElement e) => e.Element("defName")?.Value ?? "";
        XElement Ext(XElement d, string cls) => d.Element("modExtensions")?.Elements("li").FirstOrDefault(li => li.Attribute("Class")?.Value == "ColonyCouncil." + cls);
        float F(string v) => float.Parse(v, CultureInfo.InvariantCulture);
        var factions = all.Where(e => e.Name.LocalName == "FactionDef").ToDictionary(f => Ext(f, "PeopleExtension")!.Element("calling")!.Value);
        C(Four.All(factions.ContainsKey) && Dn(factions["hunters"]) == "Rimfolk_Wardens" && Dn(factions["sextons"]) == "Rimfolk_QuietBell" && Dn(factions["tamers"]) == "Rimfolk_Tamers"
          && Dn(factions["salvagers"]) == "Rimfolk_Wheel", "their defs");
        string Tech(string c) => factions[c].Element("techLevel")!.Value;
        C(Tech("hunters") == "Medieval" && Tech("sextons") == "Medieval" && Tech("tamers") == "Industrial" && Tech("salvagers") == "Industrial",
            "the Wardens and the Bell medieval, the Circuit and the Wheel industrial");
        var allDefs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).SelectMany(f => XDocument.Load(f).Root!.Elements()).ToList();
        var camps = allDefs.Where(e => e.Name.LocalName == "ColonyCouncil.CampKindDef").Select(Dn).ToHashSet();
        foreach (var c in Four)
        {
            var f = factions[c];
            var ext = Ext(f, "PeopleExtension")!;
            string name = Dn(f);
            C(f.Element("naturalEnemy")?.Value == "false" && f.Element("permanentEnemy")?.Value == "false" && f.Element("requiredCountAtGameStart")?.Value == "0"
              && f.Element("startingCountAtWorldCreation")?.Value == "1", $"{name}: nobody's enemy by nature, once in a new world, never required");
            C(F(f.Element("raidCommonalityFromPointsCurve")!.Descendants("li").Single().Value.Trim('(', ')').Split(',')[1]) <= 0.3f, $"{name}: raids rarely, and only when wronged");
            C(ext.Element("ariseAges")?.Value == "0~3", $"{name}: arises in every age");
            C(ext.Element("camps")!.Elements("li").Count() == 3 && ext.Element("camps")!.Elements("li").All(li => camps.Contains(li.Value)), $"{name}: three kinds of camp it favours, each a real one");
            C(f.Element("fixedIdeo")?.Value == "true" && !string.IsNullOrEmpty(f.Element("ideoName")?.Value) && f.Element("styles")!.Elements("li").All(li => li.Attribute("MayRequire")?.Value == "Ludeon.RimWorld.Ideology"),
                $"{name}: a faith of its own, and its style only with Ideology");
            var kinds = all.Where(e => e.Name.LocalName == "PawnKindDef" && Dn(e).StartsWith(name + "_")).ToList();
            C(kinds.Count == 5, $"{name}: five kinds of people by role");
        }
        C(factions["tamers"].Element("modExtensions")!.Descendants("herd").Any() && factions["salvagers"].Element("modExtensions")!.Descendants("herd").Any()
          && factions["hunters"].Element("modExtensions")!.Descendants("herd").Any(), "the Wardens' hounds, the Circuit's stables and the Wheel's draught beasts in their towns");

        // ================= Their arms for another age =================

        // The player's catalogue (docs/player-mods/weapons.tsv): what each mod's weapons are tagged and worth, and whether it is active.
        var catalogue = File.ReadAllLines(Path.Combine(root, "docs", "player-mods", "weapons.tsv")).Skip(1).Select(l => l.Split('\t')).Where(c => c.Length >= 9)
            .Select(c => (def: c[0], mod: c[1].ToLowerInvariant(), active: c[2] == "yes", tags: c[5].Split(',', StringSplitOptions.RemoveEmptyEntries), worth: c[8])).ToList();
        var gearWorth = File.ReadAllLines(Path.Combine(root, "Tools", "Tests", "PeopleGear.txt")).Where(l => !l.StartsWith("#") && l.Contains('\t')).Select(l => l.Split('\t'))
            .Where(c => c.Length > 5 && c[1] == "weapon" && c[5] != "-").ToDictionary(c => c[2], c => F(c[5]));
        float Worth((string def, string mod, bool active, string[] tags, string worth) w) =>
            float.TryParse(w.worth, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0f ? v : gearWorth.TryGetValue(w.def, out var g) ? g : float.NaN;
        // Release D's own (release E's are PeoplesETests').
        string[] dPeoples = Four.Select(c => Dn(factions[c]) + "_").ToArray();
        var armed = all.Where(e => e.Name.LocalName == "PawnKindDef" && dPeoples.Any(p => Dn(e).StartsWith(p)) && Ext(e, "PeopleKindExtension")?.Element("ageArms") != null).ToList();
        C(armed.Select(Dn).OrderBy(x => x).SequenceEqual(new[] { "Rimfolk_QuietBell_Gravewarden", "Rimfolk_Tamers_Ranger", "Rimfolk_Tamers_Ringwarden", "Rimfolk_Wardens_Tracker", "Rimfolk_Wheel_Guard" }),
            "the trackers', the gravewardens', the rangers', the ring-wardens' and the yard guards' arms change with the age");
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
                C(catalogue.Any(w => w.active && tags.Any(t => w.tags.Contains(t.Value)) && Worth(w) <= means), $"{kind}: a weapon of the ages {ages[0]} to {ages[1]} within what the set may cost ({means:0})");
                // The tribal and medieval ages' sets of the industrial peoples have a weapon of the base game alone.
                if (ages[1] <= 1)
                    C(tags.Any(li => li.Attribute("MayRequire") == null), $"{kind}: a tribal or medieval weapon of the base game");
            }
        }
        string gifts = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Peoples", "PeopleGifts.cs"));
        C(gifts.Contains("AgeArms.Arm(p, ext.ageArms, age)") && gifts.Contains("int age = Peoples.AgeOf(p.Faction)") && gifts.Contains("PeoplesDMath.AgeFits(age, g.ages.min, g.ages.max)"),
            "arms and gifts by the age a people lives in, as it is made");

        // ================= Their towns' plans =================

        var plans = XDocument.Load(Path.Combine(peopleDir, "Rimfolk_TownPlans_PeoplesD.xml")).Root!.Elements("ColonyCouncil.TownPlanDef").ToDictionary(Dn);
        List<string> Rows(string plan) => plans[plan].Element("rows")!.Elements("li").Select(li => li.Value).ToList();
        int Count(string plan, Func<XElement, bool> pick) =>
            plans[plan].Element("keys")!.Elements("li").Where(pick).Select(k => k.Element("key")!.Value[0]).Sum(ch => Rows(plan).Sum(r => r.Count(x => x == ch)));
        int Things(string plan, string thing) => Count(plan, k => k.Element("thing")?.Value == thing);
        C(plans.Keys.SequenceEqual(new[] { "Rimfolk_Plan_WardensLodge", "Rimfolk_Plan_GraveGarden", "Rimfolk_Plan_BoutRing", "Rimfolk_Plan_WheelYard" })
          && Four.All(c => plans.ContainsKey(Ext(factions[c], "PeopleExtension")!.Element("plan")!.Value)), "a plan for each of release D's peoples");
        C(Things("Rimfolk_Plan_WardensLodge", "ButcherSpot") == 1 && Things("Rimfolk_Plan_WardensLodge", "AnimalSleepingSpot") >= 4 && Count("Rimfolk_Plan_WardensLodge", k => k.Element("mark")?.Value == "pen") >= 40
          && Things("Rimfolk_Plan_WardensLodge", "TorchLamp") >= 8, "the lodge: its trophy hall, the butcher's spot, the kennels and lanterns at every way out");
        C(Things("Rimfolk_Plan_GraveGarden", "Grave") >= 12 && Things("Rimfolk_Plan_GraveGarden", "Sarcophagus") >= 2 && Count("Rimfolk_Plan_GraveGarden", k => k.Element("mark")?.Value == "plant:Plant_Rose") >= 12,
            "the grave-garden: rows of graves among roses, the ossuary's sarcophagi");
        C(Count("Rimfolk_Plan_BoutRing", k => k.Element("floor")?.Value == "PackedDirt" && k.Element("thing") == null) >= 100 && Things("Rimfolk_Plan_BoutRing", "AnimalSleepingBox") >= 4
          && Count("Rimfolk_Plan_BoutRing", k => k.Element("mark")?.Value == "pen") >= 100, "the ring: a ring of packed earth, the hatchery's boxes, stables for every kind");
        C(Things("Rimfolk_Plan_WheelYard", "FueledSmithy") == 1 && Things("Rimfolk_Plan_WheelYard", "TableMachining") == 1 && Things("Rimfolk_Plan_WheelYard", "HandTailoringBench") == 1
          && Things("Rimfolk_Plan_WheelYard", "Bedroll") >= 6 && Things("Rimfolk_Plan_WheelYard", "Bed") == 0, "the yard: the smithy, the machining table, the tailor's bench, and bedrolls, as if leaving tomorrow");

        // ================= Their callers and asks =================

        var events = XDocument.Load(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeoplesD.xml")).Root!.Elements("IncidentDef").ToDictionary(Dn);
        C(events.Keys.SequenceEqual(new[] { "Rimfolk_Evt_WardensHunt", "Rimfolk_Evt_PeltBuyer", "Rimfolk_Evt_WardensLantern", "Rimfolk_Evt_BellForTheDead", "Rimfolk_Evt_BellFuneral",
            "Rimfolk_Evt_BellVoidSigns", "Rimfolk_Evt_TamerBout", "Rimfolk_Evt_EggTrade", "Rimfolk_Evt_TamerStray", "Rimfolk_Evt_WheelMending", "Rimfolk_Evt_WheelScrap", "Rimfolk_Evt_WheelWreck" }),
            "twelve callers at the gate, three of each people");
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { src }.Concat(parts).ToArray()));
        string code = Read("Peoples", "PeoplesD.cs") + Read("Peoples", "PeoplesD2.cs"), towns = Read("Peoples", "WheelTowns.cs");
        string everyCode = string.Join("\n", Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        foreach (var (name, e) in events)
        {
            string worker = e.Element("workerClass")!.Value.Replace("ColonyCouncil.", "");
            C(code.Contains("class " + worker + " "), $"{name}: its worker is release D's own");
            var effects = e.Descendants("li").Select(li => li.Attribute("Class")?.Value).Where(v => v != null && v.StartsWith("ColonyCouncil.DilemmaEffect_")).Select(v => v.Replace("ColonyCouncil.", "")).Distinct();
            C(effects.All(x => Regex.IsMatch(everyCode, @"class " + x + @"\b")), $"{name}: every answer's effect is real");
            var ext = Ext(e, "HappeningExtension")!;
            C(ext.Element("options")!.Elements("li").Count() == 3 && e.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_SendAway"),
                $"{name}: three answers, one of them to send them on their way");
            C(F(e.Element("baseChance")!.Value) <= 2f && int.Parse(e.Element("minRefireDays")!.Value) >= 12, $"{name}: a small share of the callers, not soon again");
            string journal = ext.Element("journal")?.Value;
            C(journal == null || allDefs.Any(d => Dn(d) == journal), $"{name}: the colonists write about it in a journal that exists");
        }
        bool Has(XElement d, string cls, string field = null, string value = null) =>
            d.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil." + cls && (field == null || li.Element(field)?.Value == value));
        C(Has(events["Rimfolk_Evt_WardensHunt"], "DilemmaEffect_WardensHunt", "together", "true") && Has(events["Rimfolk_Evt_WardensHunt"], "DilemmaEffect_Learn", "skill", "Shooting"),
            "the hunt: beside the colony's best shot, or the Wardens alone");
        C(Has(events["Rimfolk_Evt_PeltBuyer"], "DilemmaEffect_CarryOut", "variant", "true") && Has(events["Rimfolk_Evt_PeltBuyer"], "DilemmaEffect_Lesson", "skill", "Animals"),
            "the pelt-buyer: the pelts carried out to them, a tracker's lesson in reading a trail");
        C(Has(events["Rimfolk_Evt_WardensLantern"], "DilemmaEffect_Lanterns", "at", "gate") && Has(events["Rimfolk_Evt_WardensLantern"], "DilemmaEffect_Vigil", "to", "Gate"),
            "a lantern lit at the gate, and a vigil by it if the colony keeps one");
        C(Has(events["Rimfolk_Evt_BellForTheDead"], "DilemmaEffect_UndertakersBury") && Has(events["Rimfolk_Evt_BellForTheDead"], "DilemmaEffect_UndertakersTake")
          && events["Rimfolk_Evt_BellForTheDead"].Descendants("li").Count(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_BellForTheDead") == 2,
            "the enemy's dead buried beyond the wall or carried off, the bell rung over each either way");
        C(Has(events["Rimfolk_Evt_BellFuneral"], "DilemmaEffect_Vigil", "to", "Grave") && Has(events["Rimfolk_Evt_BellFuneral"], "DilemmaEffect_Vigil", "pool", "Rimfolk_Pool_PlayBellFuneral"),
            "a funeral at the grave, with its own words");
        C(Ext(events["Rimfolk_Evt_BellVoidSigns"], "HappeningExtension")!.Element("mods")!.Elements("li").Any(l => l.Value == "Ludeon.RimWorld.Anomaly")
          && Has(events["Rimfolk_Evt_BellVoidSigns"], "DilemmaEffect_Vigil", "untilDawn", "true"), "the void's signs only with Anomaly: a watch on the walls until dawn, or a lamp at the gate");
        C(Has(events["Rimfolk_Evt_TamerBout"], "DilemmaEffect_TamerBout") && Has(events["Rimfolk_Evt_TamerBout"], "DilemmaEffect_Learn", "skill", "Animals"), "the bout, or an hour with the tamer and their companion");
        C(Has(events["Rimfolk_Evt_EggTrade"], "DilemmaEffect_TamerEgg", "swap", "true") && Has(events["Rimfolk_Evt_EggTrade"], "DilemmaEffect_CarryOut", "what", "silver"), "an egg swapped for one of the colony's, or bought");
        C(events["Rimfolk_Evt_TamerStray"].Descendants("li").Count(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_StrayBack") == 2, "a stray handed back at the gate, thanks or not");
        C(Has(events["Rimfolk_Evt_WheelScrap"], "DilemmaEffect_CarryOut", "what", "scrap") && Has(events["Rimfolk_Evt_WheelScrap"], "DilemmaEffect_WheelRefurbished"),
            "the scrap carried out to their cart, for silver or a piece mended back to new");
        C(Has(events["Rimfolk_Evt_WheelWreck"], "DilemmaEffect_WheelStrip") && Has(events["Rimfolk_Evt_WheelWreck"], "DilemmaEffect_WheelStrip", "share", "false"),
            "a wreck stripped where it lies, half for the colony, or sold whole");
        C(Ext(events["Rimfolk_Evt_WheelMending"], "HappeningExtension")!.Element("texts")!.Elements("li").Count() >= 2 && events["Rimfolk_Evt_WheelMending"].Element("workerClass")!.Value.EndsWith("WheelMending")
          && code.Contains("class HappeningWorker_WheelMending : HappeningWorker_Tinker"), "the fixer mends as the travelling tinker does, under the Wheel's name");

        var missions = XDocument.Load(Path.Combine(root, "Defs", "MissionDefs", "Rimfolk_Missions_PeoplesD.xml")).Root!.Elements("IncidentDef").ToDictionary(Dn);
        string Only(string m) => Ext(missions[m], "MissionPeople")?.Element("only")?.Value;
        C(Only("Rimfolk_Mission_WardensDrive") == "hunters" && Only("Rimfolk_Mission_BellShrouds") == "sextons" && Only("Rimfolk_Mission_TamersFoal") == "tamers" && Only("Rimfolk_Mission_WheelAxle") == "salvagers",
            "an ask of each people, only theirs");
        C(Ext(missions["Rimfolk_Mission_BellShrouds"], "MissionPeople")!.Element("whileKind")?.Value == "sickness" && missions["Rimfolk_Mission_BellShrouds"].Descendants("what").Any(w => w.Value == "textiles"),
            "linen for shrouds only in a plague year");
        C(missions["Rimfolk_Mission_WheelAxle"].Descendants("what").Select(w => w.Value).SequenceEqual(new[] { "Steel", "ComponentIndustrial" })
          && missions["Rimfolk_Mission_WardensDrive"].Element("workerClass")!.Value.EndsWith("MissionHands") && missions["Rimfolk_Mission_TamersFoal"].Element("workerClass")!.Value.EndsWith("MissionBeast"),
            "steel or parts for an axle, two good shots for a drive, a young beast raised a season");
        C(missions.Values.All(m => F(m.Element("baseChance")!.Value) <= 0.4f && int.Parse(m.Element("minRefireDays")!.Value) >= 60), "a small share of the missions' one draw, not soon again");
        var thoughts = XDocument.Load(Path.Combine(root, "Defs", "ThoughtDefs", "Rimfolk_Thoughts_PeoplesD.xml")).Root!.Elements("ThoughtDef").Select(Dn).ToHashSet();
        C(new[] { "Rimfolk_Evt_LanternLit", "Rimfolk_Evt_BellFuneral", "Rimfolk_Evt_BeatTamer", "Rimfolk_Evt_StrayHome", "Rimfolk_Mission_RodeWithWardens" }.All(thoughts.Contains),
            "the colony's thoughts: the lantern lit, the funeral, a tamer beaten fairly, a stray home, the drive");

        // ================= The patch on Rimfolk's own defs =================

        var patch = XDocument.Load(Path.Combine(root, "Patches", "Rimfolk_PeoplesD.xml")).Root!;
        var names = allDefs.Select(Dn).Where(x => x != "").ToHashSet();
        foreach (var op in patch.Elements("Operation"))
        {
            var m = Regex.Match(op.Element("xpath")?.Value ?? "", "defName=\"([^\"]+)\"");
            C(op.Attribute("Class")?.Value == "PatchOperationConditional" && m.Success && names.Contains(m.Groups[1].Value), $"the patch reaches {m.Groups[1].Value}, conditionally");
        }
        C(patch.Descendants("calling").Select(c => c.Value).OrderBy(x => x).SequenceEqual(Four.OrderBy(x => x)), "four world events prefer them, one each");
        C(patch.Descendants("prefers").Select(c => c.Value).OrderBy(x => x).SequenceEqual(new[] { "salvagers", "sextons", "tamers" }),
            "the undertakers from the Bell likeliest, the travelling tinker from the Wheel, the animal doctor from the Circuit");
        C(new[] { "sextons", "salvagers", "tamers" }.All(p => patch.Descendants("li").Any(li => li.Value.StartsWith("[" + PeoplesMath.Tag(p)))), "each says it in that people's voice when they are the ones who came");
        C(Read("Happenings", "Workers_Callers.cs").Contains("protected virtual Faction People(HappeningExtension ext) => Arrivals.Friendly(null, Peoples.Weigh(ext.prefers));")
          && Read("Happenings", "Workers_Callers.cs").Contains("ctx.faction = Arrivals.Friendly(null, Peoples.Weigh(ext.prefers));"), "the undertakers and the animal doctor weigh the calling they prefer");

        // ================= Release B's peoples that arise =================

        var callings = new[] { "traders", "healers", "herders", "pilgrims", "scholars", "brokers", "fishers", "hunters", "sextons", "tamers", "salvagers" };
        var from = new[] { 2, 1, 0, 1, 1, 3, 0, 0, 0, 0, 0 };
        var to = new[] { 2, 1, 0, 3, 3, 3, 2, 3, 3, 3, 3 };
        string Of(string kind, int age, string lean = null)
        {
            int i = ArisingMath.Template(kind, age, callings, from, to, lean);
            return i < 0 ? null : callings[i];
        }
        C(ArisingMath.Callings(ArisingMath.Refugees).SequenceEqual(new[] { "herders", "healers", "traders", "fishers", "sextons", "salvagers", "linesmen", "crew" })
          && ArisingMath.Callings(ArisingMath.League).All(c => !Four.Contains(c)) && ArisingMath.Callings(ArisingMath.Splinter).All(c => !Four.Contains(c)),
            "survivors may keep the Bell's ways or the Wheel's (and release F's the Line's or the Fleet's), a band the Circuit's or the Wardens'; a league or a splinter neither");
        C(Enumerable.Range(0, 4).All(a => Of("refugees", a, "sextons") == "sextons" && Of("exiles", a, "tamers") == "tamers" && Of("exiles", a, "hunters") == "hunters")
          && Of("refugees", 3, "salvagers") == "salvagers", "a cause that leans to them takes their ways in any age");
        C(Enumerable.Range(0, 4).Select(a => Of("refugees", a)).SequenceEqual(new[] { "herders", "healers", "traders", "traders" }) && Of("exiles", 2) == "traders", "else as before");
        C(ArisingMath.Lean(ArisingMath.Exiles, false, false, 0, 12, 0, false, false, 1) == "tamers" && ArisingMath.Lean(ArisingMath.Exiles, false, false, 0, 0, 10, false, false, 1) == "hunters"
          && ArisingMath.Lean(ArisingMath.Exiles, false, false, 10, 12, 12, false, false, 1) == "scholars" && ArisingMath.Lean(ArisingMath.Exiles, false, false, 0, 9, 9, true, true, 3) == null,
            "a band led by one good with beasts walks as tamers, by a good shot as wardens; the learned first; nothing else for a band");
        C(ArisingMath.Lean(ArisingMath.Refugees, false, false, 0, 0, 0, true, true, 3) == "sextons" && ArisingMath.Lean(ArisingMath.Refugees, false, false, 0, 0, 0, false, true, 2) == "salvagers"
          && ArisingMath.Lean(ArisingMath.Refugees, false, false, 0, 0, 0, false, true, 1) == null && ArisingMath.Lean(ArisingMath.Refugees, true, true, 0, 0, 0, true, true, 3) == "fishers"
          && ArisingMath.Lean(ArisingMath.League, false, false, 20, 20, 20, true, true, 3) == null,
            "survivors by the river first, else a quarter stay to bury their dead, else a third of an industrial or spacer town's take to the wheels");
        C(ArisingMath.Lean("exiles", false, false, 12) == "scholars" && ArisingMath.Lean("refugees", true, false, 0) == null, "release C's leans as they were");
        float Share(int salt, float chance) => Enumerable.Range(0, 4000).Count(i => ArisingMath.Fated(i * 7919L, salt, chance)) / 4000f;
        float bell = Share(ArisingMath.SaltBell, ArisingMath.BellChance), wheel = Share(ArisingMath.SaltWheel, ArisingMath.WheelChance);
        C(Math.Abs(bell - 0.25f) < 0.04f && Math.Abs(wheel - 0.33f) < 0.04f, $"a quarter of the survivors keep the dead ({bell:0.000}), a third take to the wheels ({wheel:0.000})");
        string arising = Read("Peoples", "Arising.cs");
        C(arising.Contains("ArisingMath.Fated(r.town, ArisingMath.SaltBell, ArisingMath.BellChance), ArisingMath.Fated(r.town, ArisingMath.SaltWheel, ArisingMath.WheelChance)")
          && arising.Contains("Skill(r.pawn, SkillDefOf.Animals), Skill(r.pawn, SkillDefOf.Shooting)"), "the town's own fate and the leader's own skills");
        var arise = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Peoples_Arising.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (var k in new[] { "refugees_sextons", "refugees_salvagers", "exiles_tamers", "exiles_hunters" })
            C(arise.ContainsKey("CC_Arise_" + k) && arise.ContainsKey("CC_Arise_" + k + "_2") && new[] { "", "_2", "_3" }.All(s => arise.TryGetValue("CC_Arise_Name_" + k + s, out var v) && v.Contains("{WHAT}")),
                $"{k}: how a new people of these ways lives, told two ways, and three names for it");
        C(arise["CC_Arise_refugees_sextons"].Contains("grave-garden") && arise["CC_Arise_refugees_salvagers"].Contains("wheels") && arise["CC_Arise_exiles_tamers"].Contains("bout")
          && arise["CC_Arise_exiles_hunters"].Contains("lantern"), "and what it does is what its people do at the gate");

        // ================= The tamers' bout, a kind of harm =================

        C(HarmKinds.TamerBout.key == "tamerBout" && HarmKinds.TamerBout.system == "peoples" && !HarmKinds.TamerBout.founding && HarmKinds.All.Contains(HarmKinds.TamerBout)
          && HarmKinds.TamerBout.weight > 0f && HarmKinds.TamerBout.weight < 0.02f, "the bout's bruise a kind of harm of its own, at the rate the harm model measures");
        C(code.Contains("Harm.Roll(trainer, HarmKinds.TamerBout, PeoplesDMath.BoutBlowChance, HurtClass.Bout)")
          && code.Contains("Harm.Injure(trainer, HurtKind.Bruise, HurtWhere.Any, PeoplesDMath.BoutLow, PeoplesDMath.BoutHigh, HarmKinds.TamerBout, HurtClass.Bout)")
          && code.Contains("\"CC_PeoplesD_BoutBlow\""), "through the harm setting's cap and scale, and told");
        C(code.Contains("if (Harm.On) sheet.Add(ImpactKind.Health"), "the answer says the bout may bruise, when harm is on");
        C(File.ReadAllText(Path.Combine(root, "Tools", "Balance", "HarmSim.cs")).Contains("Comes(y, HarmKinds.TamerBout, gtb, TamerBlow)"), "and the harm model counts it");
        C(Regex.Matches(code, @"Harm\.(Roll|Injure|Ail|Hurt)\(").Count == 2, "nothing else of theirs hurts anybody");

        // ================= Their words and their page =================

        var almanac = XDocument.Load(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_PeoplesD.xml")).Root!.Elements().Single();
        C(Dn(almanac) == "Council_Alm_C_PeoplesD" && almanac.Element("keywords")!.Elements("li").Count() >= 4 && almanac.Element("seeAlso")!.Elements("li").Any(l => l.Value == "Council_Alm_C_PeoplesC"),
            "an Almanac page of their own, beside release C's");
        var almText = string.Join("\n", almanac.Descendants().Where(e => !e.HasElements).Select(e => e.Value));
        C(!almText.Contains('—') && !almText.Contains('!') && !Regex.IsMatch(almText, @"\bin \d+ (days?|hours?)\b"), "its words plain, no countdowns");
        C(almText.Contains("one bout in 25") && almText.Contains("half for the colony") && almText.Contains("Anomaly") && almText.Contains("every month or so") && almText.Contains("PokéWorld"),
            "its rules are the code's");
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeoplesD.xml")).Root!.Elements().Select(e => e.Name.LocalName).ToHashSet();
        var used = Regex.Matches(code + towns, "\"(CC_PeoplesD_\\w+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        C(used.Count >= 30 && used.All(keyed.Contains), $"every word the code says is keyed ({used.Count})");
        var dTexts = new[] { Path.Combine("Defs", "IncidentDefs", "Rimfolk_Events_PeoplesD.xml"), Path.Combine("Defs", "FlavorPoolDefs", "Rimfolk_Lines_PeoplesD.xml"),
            Path.Combine("Languages", "English", "Keyed", "Rimfolk_PeoplesD.xml"), Path.Combine("Defs", "MissionDefs", "Rimfolk_Missions_PeoplesD.xml") }
            .Select(p => File.ReadAllText(Path.Combine(root, p)).ToLowerInvariant()).Aggregate((a, b) => a + b);
        C(!Regex.IsMatch(dTexts, @"\b(dynasty|dynasties|bloodline|heir to the house)\b"), "the colony is the family: no houses, no bloodlines");

        // ================= The seams =================

        C(!Regex.IsMatch(code + towns + Read("Peoples", "PeoplesDMath.cs"), @"Sched\.|Scribe_|ExposeData|\[HarmonyPatch"), "no clock, nothing saved, no Harmony patch of their own");
        C(Read("World", "Region", "RegionBook.cs").Contains("WheelTowns.Daily(now)") && towns.Contains("s.HasMap") && towns.Contains("c.IsPlayerControlled")
          && towns.Contains("PeoplesDMath.MoveDay(") && towns.Contains("RegionTowns.Refresh(true)"),
            "a Wheel town moves on the region's day, never with the colony's map of it or a caravan bound for it, and the region's map is told");
        string happen = Read("Happenings", "Happenings.cs");
        C(happen.Contains("HappeningWorker_WardensHunt.Killed(victim)") && happen.Contains("case HappeningWorker_WardensHunt.Kind:") && happen.Contains("case HappeningWorker_TamerBout.Kind:"),
            "the hunt and the bout ride on the visits Rimfolk already keeps");
        C(code.Contains("new LordJob_AssaultThings(ctx.faction, new List<Thing> { beast }") && code.Contains("WorkGiver_HunterHunt.HasHuntingWeapon(hunter)")
          && code.Contains("DesignationDefOf.Hunt"), "the Wardens go for the beast, the colony's shot hunting it beside them");
        C(code.Contains("PokeWorldLink.Active") && code.Contains("TypesOf(k.race).Contains(\"Fighting\")") && code.Contains("!PokeWorldLegends.IsLegendary(k.race)"),
            "with PokéWorld a Fighting type, never a legend; else a beast of the land");
        C(code.Contains("Couriers.Hand(egg") && code.Contains("Couriers.PaySilver(") && code.Contains("WorldSituationWorker.Goodwill(f, PeoplesDMath.GoodwillFor(n))"),
            "eggs and silver handed over at the gate, the bell's goodwill with the dead's peoples");
        C(Regex.Matches(code, @"Speech\.Say\(").Count >= 3 && !code.Contains("MoteMaker.ThrowText"), "they speak through Rimfolk's speech");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
