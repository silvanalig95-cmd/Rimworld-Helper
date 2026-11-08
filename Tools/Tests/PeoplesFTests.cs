using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Rimfolk's own peoples, release F (Peoples/PeoplesFMath.cs, PeoplesF.cs, PeoplesF2.cs, PeoplesFPatches.cs;
/// docs/design/peoples.md, "Release F"; docs/design/peoples-third.md, step 2): the Signal Line, the Deepwell Union, the
/// Last Republic and the Hollow Fleet. Their pure rules (the relay mast's site, its storm wear, the letters it hastens
/// and the raid it warns of; the union's drill and its share, its strike; the census, the papers, the conscript's season
/// and the radio; the Fleet's shuttle, its salvage and the sleeper woken too late), their defs (none an enemy by nature,
/// once each in a new world, every age, the Line's linesmen, the Republic's Civic Guard and the Fleet's crew in one kit),
/// their arms for another age, their towns' plans, their callers and asks, the patch, release B's arising with them,
/// their one harm, their words and page, and the seams. Release A's checks that every people meets run over all
/// nineteen in PeoplesTests, PeoplesGearTests and PeoplesLoreTests.
/// </summary>
public static class PeoplesFTests
{
    private static readonly string[] Four = { "linesmen", "drillers", "citizens", "crew" };

    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "peoples, release F: " + what);
        }
        const int D = PeoplesFMath.DayTicks;

        // ================= The Signal Line =================

        C(PeoplesFMath.SiteScore(4, true, 0f) == 14f && PeoplesFMath.SiteScore(9, false, 40f) == 10f && PeoplesFMath.SiteScore(1, false, 0f) > PeoplesFMath.SiteScore(0, true, 0f)
          && PeoplesFMath.SiteScore(2, false, 10f) > PeoplesFMath.SiteScore(2, false, 30f) && PeoplesFMath.SiteScore(0, true, float.NaN) == 0f
          && PeoplesFMath.MastMinCells == 8 && PeoplesFMath.MastMaxCells == 40, "the mast on the highest ground: the hillside first, rough ground next, the nearer the better");
        C(PeoplesFMath.Quiet(109, 220) && !PeoplesFMath.Quiet(110, 220) && PeoplesFMath.Quiet(0, 0) && PeoplesFMath.QuietBelow == 0.5f && PeoplesFMath.StormWear == 1,
            "a storm wears it, and under half its strength it is quiet");
        C(PeoplesFMath.Relayed(6f) == 3f && PeoplesFMath.Relayed(0.6f) == 0.5f && PeoplesFMath.Relayed(0f) == 0.5f && PeoplesFMath.Relayed(float.NaN) == 0.5f,
            "a letter's leg through the mast in half the days, never under the half day to hand it on");
        C(PeoplesFMath.WarnHours(0f) == 2 && PeoplesFMath.WarnHours(0.5f) == 3 && PeoplesFMath.WarnHours(0.9999f) == 4 && PeoplesFMath.WarnHours(float.NaN) == 2
          && Enumerable.Range(0, 100).All(i => PeoplesFMath.WarnHours(i / 100f) is int h && h >= 2 && h <= 4) && PeoplesFMath.WarnRetryHours == 6,
            "a raid comes a couple of hours after the line's warning");
        C(PeoplesFMath.Warns(true, true, false, false) && !PeoplesFMath.Warns(false, true, false, false) && !PeoplesFMath.Warns(true, false, false, false)
          && !PeoplesFMath.Warns(true, true, true, false) && !PeoplesFMath.Warns(true, true, false, true), "it warns of the storyteller's raids only, never a quest's or one already held");
        C(PeoplesFMath.WirePrice(100f) == 120 && PeoplesFMath.WirePrice(13f) == 15 && PeoplesFMath.WirePrice(float.NaN) == 0 && PeoplesFMath.WireMin == 4 && PeoplesFMath.WireMax == 20
          && PeoplesFMath.NewsFee == 50, "components bought at a fifth over their worth, four to twenty; the news of a caravan for fifty silver");

        // ================= The Deepwell Union =================

        C(PeoplesFMath.Yield(0, 1f, 1f) == 0 && PeoplesFMath.Yield(100, 10f, 1f) == 70 && PeoplesFMath.Yield(1000, 1f, 1f) == 700 && PeoplesFMath.Yield(5000, 1f, 3f) == 1400
          && PeoplesFMath.Yield(10, 1000f, 1f) == 1 && PeoplesFMath.Yield(100, float.NaN, float.NaN) == 100 && PeoplesFMath.DrillHours == 6,
            "the seam drilled for six hours: what lies there, up to about 700 silver's worth, more for a richer colony");
        C(PeoplesFMath.ColonyShare(10) == 6 && PeoplesFMath.ColonyShare(5) == 3 && PeoplesFMath.ColonyShare(1) == 1 && PeoplesFMath.ColonyShare(0) == 0 && PeoplesFMath.ColonyShare(7) == 4
          && PeoplesFMath.UnionShare == 0.4f && Enumerable.Range(1, 200).All(t => PeoplesFMath.ColonyShare(t) <= t && PeoplesFMath.ColonyShare(t) >= t / 2),
            "two in five goes with the union, the rest stays with the colony");
        C(PeoplesFMath.AccidentChance == 0.05f && PeoplesFMath.AccidentLow < PeoplesFMath.AccidentHigh && PeoplesFMath.AccidentHigh <= 8f, "one shift in twenty the rig hurts the colony's hand, never badly");
        C(PeoplesFMath.GoodsPrice(100f) == 110 && PeoplesFMath.GoodsPrice(0f) == 10 && PeoplesFMath.GoodsPrice(1000f) == 1100 && PeoplesFMath.CrateSteel(1f) == 60 && PeoplesFMath.CrateSteel(5f) == 120
          && PeoplesFMath.CrateSteel(0f) == 40 && PeoplesFMath.CrateComponents(1f) == 4 && PeoplesFMath.CrateComponents(0f) == 2 && PeoplesFMath.CrateComponents(2f) == 8,
            "the assayer's crate: steel and components by the colony's wealth, a tenth over their worth");
        C(Enumerable.Range(-3, 50).All(id => Enumerable.Range(0, PeoplesFMath.StrikeLookDays).Count(d => PeoplesFMath.StrikeDay(d, id)) == 1) && PeoplesFMath.StrikeDay(8, 1)
          && !PeoplesFMath.StrikeDay(-1, 0) && Math.Abs(PeoplesFMath.StrikeChance - 1f / 7f) < 0.0001f, "each union looked at once a season, a strike on one look in seven");
        C(PeoplesFMath.Striking(0, 30 * D - 1, false) && !PeoplesFMath.Striking(0, 30 * D, false) && !PeoplesFMath.Striking(0, 5, true) && !PeoplesFMath.Striking(-1, 0, false)
          && !PeoplesFMath.Striking(100, 50, false) && PeoplesFMath.StrikeDays == 30 && PeoplesFMath.StrikeAskAfterDays == 2, "a strike holds a month, or until it is settled");
        C(PeoplesFMath.PicketFood(0) == 60 && PeoplesFMath.PicketFood(5) == 100 && PeoplesFMath.PicketFood(20) == 160 && PeoplesFMath.SettledGoodwill > 0, "food for the picket line, 60 to 160");

        // ================= The Last Republic =================

        C(PeoplesFMath.CensusTicks == 420 && PeoplesFMath.CountedGoodwill > 0 && PeoplesFMath.UncountedGoodwill < 0 && -PeoplesFMath.UncountedGoodwill < PeoplesFMath.CountedGoodwill,
            "a colony counted is thought the better of, a refusal a little the worse");
        C(PeoplesFMath.Holds(0, 60 * D - 1, false) && !PeoplesFMath.Holds(0, 60 * D, false) && !PeoplesFMath.Holds(0, 1, true) && !PeoplesFMath.Holds(-1, 0, false)
          && PeoplesFMath.PapersDays == 60 && PeoplesFMath.PapersTerms == 0.06f && PeoplesFMath.PapersGoodwill > 0 && PeoplesFMath.StruckGoodwill < -PeoplesFMath.PapersGoodwill,
            "papers hold a year unless struck, a citizen's terms six in a hundred better; struck weighs more than granted");
        C(PeoplesFMath.ServicePay(0) == 150 && PeoplesFMath.ServicePay(2) == 330 && PeoplesFMath.ServicePay(3) == 420 && PeoplesFMath.ServicePay(9) == 420 && PeoplesFMath.ServicePay(-1) == 150
          && PeoplesFMath.ServiceDays == 15 && PeoplesFMath.ServiceXp > 0f && PeoplesFMath.ServedGoodwill > -PeoplesFMath.RefusedGoodwill, "the conscript's season: fifteen days at the town, paid by the Republic's age");
        C(PeoplesFMath.Exemption(0f) == 120 && PeoplesFMath.Exemption(60000f) == 220 && PeoplesFMath.Exemption(1e9f) == 480 && PeoplesFMath.Exemption(float.NaN) == 120
          && PeoplesFMath.Exemption(33333f) % 10 == 0, "excused for 120 to 480 silver by the colony's wealth");
        C(Enumerable.Range(-3, 50).All(id => Enumerable.Range(0, PeoplesFMath.RadioDays).Count(d => PeoplesFMath.RadioDay(d, id)) == 1) && PeoplesFMath.RadioDay(10, 0),
            "the radio speaks of the colony once a season, on its own day");
        C(PeoplesFMath.Broadcast(50, false, false) == "praise" && PeoplesFMath.Broadcast(10, true, false) == "praise" && PeoplesFMath.Broadcast(10, false, false) == null
          && PeoplesFMath.Broadcast(-1, true, false) == "denounce" && PeoplesFMath.Broadcast(80, true, true) == "denounce", "praise for a friend or a citizen, a denunciation for an enemy, else nothing");

        // ================= The Hollow Fleet =================

        C(PeoplesFMath.ShuttleGuards(0.6f) == 1 && PeoplesFMath.ShuttleGuards(1.3f) == 2 && PeoplesFMath.ShuttleGuards(2f) == 3 && PeoplesFMath.ShuttleGuards(0f) == 1
          && PeoplesFMath.ShuttleGuards(5f) == 3 && PeoplesFMath.ShuttleGuards(float.NaN) == 2 && PeoplesFMath.ShuttleAge(2) && !PeoplesFMath.ShuttleAge(1),
            "the quartermaster comes down with one to three of the ship's guard, to a colony of the industrial age or later");
        C(PeoplesFMath.CasketPrice(1000f) == 1100 && PeoplesFMath.CasketPrice(0f) == 100 && PeoplesFMath.CasketPrice(float.NaN) == 110, "a casket from the hull at a tenth over its worth");
        C(PeoplesFMath.SalvagePrice(100f) == 130 && PeoplesFMath.SalvagePrice(220f) == 285 && PeoplesFMath.SalvagePrice(float.NaN) == 0 && PeoplesFMath.ChunkWorth == 220f
          && PeoplesFMath.CutHours == 2 && PeoplesFMath.CutMax == 3 && PeoplesFMath.SalvageMinWorth == 150f, "salvage at three tenths over its worth, chunks cut where they lie");
        C(PeoplesFMath.SleptYears(0f) == 190 && PeoplesFMath.SleptYears(0.9999f) == 230 && PeoplesFMath.SleptYears(float.NaN) == 190
          && Enumerable.Range(0, 100).All(i => PeoplesFMath.SleptYears(i / 100f) is int y && y >= 190 && y <= 230)
          && PeoplesFMath.SleeperHour(15) && PeoplesFMath.SleeperHour(21) && !PeoplesFMath.SleeperHour(14) && !PeoplesFMath.SleeperHour(22),
            "a sleeper woken after two hundred years or so asks shelter towards evening");

        // ================= Their defs =================

        string peopleDir = Path.Combine(root, "Defs", "PeopleDefs");
        var all = Directory.GetFiles(peopleDir, "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements()).ToList();
        string Dn(XElement e) => e.Element("defName")?.Value ?? "";
        XElement Ext(XElement d, string cls) => d.Element("modExtensions")?.Elements("li").FirstOrDefault(li => li.Attribute("Class")?.Value == "ColonyCouncil." + cls);
        float F(string v) => float.Parse(v, CultureInfo.InvariantCulture);
        var factions = all.Where(e => e.Name.LocalName == "FactionDef").ToDictionary(f => Ext(f, "PeopleExtension")!.Element("calling")!.Value);
        C(Four.All(factions.ContainsKey) && Dn(factions["linesmen"]) == "Rimfolk_SignalLine" && Dn(factions["drillers"]) == "Rimfolk_Deepwell"
          && Dn(factions["citizens"]) == "Rimfolk_Republic" && Dn(factions["crew"]) == "Rimfolk_HollowFleet", "their defs");
        string Tech(string c) => factions[c].Element("techLevel")!.Value;
        C(Tech("linesmen") == "Industrial" && Tech("drillers") == "Industrial" && Tech("citizens") == "Industrial" && Tech("crew") == "Spacer",
            "the Line, the union and the Republic industrial, the Fleet spacer");
        string Pace(string c, string what) => Ext(factions[c], "PeopleExtension")!.Element(what)?.Value;
        C(Four.All(c => Pace(c, "agePace") != null && Pace(c, "ageLimit") == "3"), "each rises toward the spacer age at its own pace, the Fleet's no further");
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
            C(kinds.Count == (c == "crew" ? 6 : 5), $"{name}: its kinds of people by role ({kinds.Count})");
            foreach (var w in new[] { "head", "envoy", "trader", "courier" })
                C(!string.IsNullOrEmpty(ext.Element(w)?.Value), $"{name}: its own word for its {w}");
        }
        C(factions["crew"].Element("forcedMemes")!.Elements("li").Any(li => li.Value == "Shipborn" && li.Attribute("MayRequire")?.Value == "Ludeon.RimWorld.Ideology,Ludeon.RimWorld.Odyssey"),
            "the Fleet shipborn only with Odyssey");
        C(Ext(factions["crew"], "PeopleExtension")!.Element("herd") == null, "no herd in a hull");

        // ================= One kit where they have soldiers, and the others' variety =================

        XElement Kind(string dn) => all.Single(e => e.Name.LocalName == "PawnKindDef" && Dn(e) == dn);
        List<string> Required(string dn) => Kind(dn).Element("apparelRequired")?.Elements("li").Select(li => li.Value).ToList() ?? new List<string>();
        var lineman = Required("Rimfolk_SignalLine_Lineman");
        C(lineman.Contains("Apparel_Duster") && lineman.Contains("VAE_Headgear_Hardhat") && Required("Rimfolk_SignalLine_Chief").SequenceEqual(lineman)
          && Required("Rimfolk_SignalLine_Operator").Count == 0, "the linemen in one kit, an oilskin and a hard hat, the chief too; the operators in their own clothes");
        var guard = Required("Rimfolk_Republic_Guard");
        const string navy = "(0.14, 0.20, 0.42)";
        C(guard.SequenceEqual(new[] { "Apparel_CollarShirt", "Apparel_Pants", "Apparel_FlakVest", "Apparel_SimpleHelmet" })
          && Kind("Rimfolk_Republic_Guard").Element("specificApparelRequirements")!.Elements("li").All(li => li.Element("color")?.Value == navy)
          && Required("Rimfolk_Republic_Citizen").Count == 0, "the Civic Guard in one uniform of civic navy, the citizens in their own clothes");
        var fleet = all.Where(e => e.Name.LocalName == "PawnKindDef" && Dn(e).StartsWith("Rimfolk_HollowFleet_")).Select(Dn).ToList();
        C(fleet.All(k => Required(k).Take(2).SequenceEqual(new[] { "Apparel_CollarShirt", "Apparel_Pants" })), "every rank of the Fleet in the ship's uniform");
        var arms = Ext(Kind("Rimfolk_Republic_Guard"), "PeopleKindExtension")!.Element("ageArms")!.Elements("li").ToList();
        C(arms.Count == 3 && arms.All(s => s.Element("apparel")?.Elements("li").Any() == true), "and in every other age the Guard's kit is the age's, in the same navy");
        C(Ext(Kind("Rimfolk_HollowFleet_Guard"), "PeopleKindExtension")!.Element("ageArms")!.Elements("li").Select(s => s.Element("ages")!.Value).SequenceEqual(new[] { "0~0", "1~1", "2~2" }),
            "the ship's guard falls back through every earlier age");

        // Their arms for another age: real tags of the player's active mods, within what the set may cost.
        var catalogue = File.ReadAllLines(Path.Combine(root, "docs", "player-mods", "weapons.tsv")).Skip(1).Select(l => l.Split('\t')).Where(c => c.Length >= 9)
            .Select(c => (def: c[0], mod: c[1].ToLowerInvariant(), active: c[2] == "yes", tags: c[5].Split(',', StringSplitOptions.RemoveEmptyEntries), worth: c[8])).ToList();
        var gearWorth = File.ReadAllLines(Path.Combine(root, "Tools", "Tests", "PeopleGear.txt")).Where(l => !l.StartsWith("#") && l.Contains('\t')).Select(l => l.Split('\t'))
            .Where(c => c.Length > 5 && c[1] == "weapon" && c[5] != "-").GroupBy(c => c[2]).ToDictionary(g => g.Key, g => F(g.First()[5]));
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

        var plans = XDocument.Load(Path.Combine(peopleDir, "Rimfolk_TownPlans_PeoplesF.xml")).Root!.Elements("ColonyCouncil.TownPlanDef").ToDictionary(Dn);
        List<string> Rows(string plan) => plans[plan].Element("rows")!.Elements("li").Select(li => li.Value).ToList();
        int Count(string plan, Func<XElement, bool> pick) =>
            plans[plan].Element("keys")!.Elements("li").Where(pick).Select(k => k.Element("key")!.Value[0]).Sum(ch => Rows(plan).Sum(r => r.Count(x => x == ch)));
        int Things(string plan, string thing) => Count(plan, k => k.Element("thing")?.Value == thing);
        C(plans.Keys.SequenceEqual(new[] { "Rimfolk_Plan_LineRelay", "Rimfolk_Plan_UnionPit", "Rimfolk_Plan_RepublicHall", "Rimfolk_Plan_FleetHull" })
          && Four.All(c => plans.ContainsKey(Ext(factions[c], "PeopleExtension")!.Element("plan")!.Value)), "a plan for each of release F's peoples");
        C(Things("Rimfolk_Plan_LineRelay", "Rimfolk_RelayMast") == 1 && Things("Rimfolk_Plan_LineRelay", "CommsConsole") >= 1 && Things("Rimfolk_Plan_LineRelay", "Bed") >= 6
          && Things("Rimfolk_Plan_LineRelay", "ShelfSmall") >= 6, "the relay: its mast, the hut with its consoles, a bunkhouse and the cable yard");
        C(Things("Rimfolk_Plan_UnionPit", "Bed") >= 10 && Things("Rimfolk_Plan_UnionPit", "FueledSmithy") == 1 && Things("Rimfolk_Plan_UnionPit", "Bookcase") >= 1
          && Things("Rimfolk_Plan_UnionPit", "Stool") >= 6 && Things("Rimfolk_Plan_UnionPit", "Column") >= 4, "the pit: the headframe, the union hall and its book, a boarding house and the smithy");
        C(Things("Rimfolk_Plan_RepublicHall", "CommsConsole") >= 1 && Things("Rimfolk_Plan_RepublicHall", "Bookcase") >= 6 && Things("Rimfolk_Plan_RepublicHall", "Sandbags") >= 1
          && Things("Rimfolk_Plan_RepublicHall", "DiningChair") >= 6, "the town hall: its radio, the archive, the ration office and the guardhouse");
        C(Things("Rimfolk_Plan_FleetHull", "CryptosleepCasket") >= 4 && Things("Rimfolk_Plan_FleetHull", "HydroponicsBasin") >= 2 && Things("Rimfolk_Plan_FleetHull", "CommsConsole") >= 1
          && Things("Rimfolk_Plan_FleetHull", "Bed") >= 6, "the hull: bridge, crew deck, the sleepers' hold and hydroponics");

        // ================= Their callers and asks =================

        var events = XDocument.Load(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeoplesF.xml")).Root!.Elements("IncidentDef").ToDictionary(Dn);
        C(events.Keys.SequenceEqual(new[] { "Rimfolk_Evt_LineMast", "Rimfolk_Evt_LineWire", "Rimfolk_Evt_LineStorm", "Rimfolk_Evt_UnionDrill", "Rimfolk_Evt_UnionGoods",
            "Rimfolk_Evt_UnionStrike", "Rimfolk_Evt_RepublicCensus", "Rimfolk_Evt_RepublicPapers", "Rimfolk_Evt_RepublicConscript", "Rimfolk_Evt_FleetShuttle", "Rimfolk_Evt_FleetSalvage",
            "Rimfolk_Evt_FleetSleeper" }), "twelve callers at the gate, three of each people");
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { src }.Concat(parts).ToArray()));
        string code = Read("Peoples", "PeoplesF.cs") + Read("Peoples", "PeoplesF2.cs"), hooks = Read("Peoples", "PeoplesFPatches.cs");
        string everyCode = string.Join("\n", Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        foreach (var (name, e) in events)
        {
            string worker = e.Element("workerClass")!.Value.Replace("ColonyCouncil.", "");
            C(code.Contains("class " + worker + " "), $"{name}: its worker is release F's own");
            var effects = e.Descendants("li").Select(li => li.Attribute("Class")?.Value).Where(v => v != null && v.StartsWith("ColonyCouncil.DilemmaEffect_")).Select(v => v.Replace("ColonyCouncil.", "")).Distinct();
            C(effects.All(x => Regex.IsMatch(everyCode, @"class " + x + @"\b")), $"{name}: every answer's effect is real");
            var ext = Ext(e, "HappeningExtension")!;
            C(ext.Element("options")!.Elements("li").Count() == 3 && e.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_SendAway"),
                $"{name}: three answers, one of them to send them on their way");
            C(F(e.Element("baseChance")!.Value) <= 2f && int.Parse(e.Element("minRefireDays")!.Value) >= 12, $"{name}: a small share of the callers, not soon again");
            string journal = ext.Element("journal")?.Value;
            C(journal == null || allDefs.Any(d => Dn(d) == journal), $"{name}: the colonists write about it in a journal that exists");
            var lineTags = ext.Element("lines")?.Elements("li").Select(l => Regex.Match(l.Value, @"^\[([^\],]+)").Groups[1].Value).Distinct().ToList() ?? new List<string>();
            C(lineTags.All(t => code.Contains("\"" + t + "\"")), $"{name}: every line's moment is one its worker says it at");
        }
        bool Has(XElement d, string cls, string field = null, string value = null) =>
            d.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil." + cls && (field == null || li.Element(field)?.Value == value));
        int Times(XElement d, string cls) => d.Descendants("li").Count(li => li.Attribute("Class")?.Value == "ColonyCouncil." + cls);
        C(Times(events["Rimfolk_Evt_LineMast"], "DilemmaEffect_LineMast") == 2 && Has(events["Rimfolk_Evt_LineMast"], "DilemmaEffect_Learn", "skill", "Construction"),
            "the mast raised by the lineman, the colony's builder beside them if it likes");
        C(Has(events["Rimfolk_Evt_LineWire"], "DilemmaEffect_CarryOut", "what", "ComponentIndustrial") && Has(events["Rimfolk_Evt_LineWire"], "DilemmaEffect_CarryOut", "count", PeoplesFMath.NewsFee.ToString()),
            "the components carried out to the wire-trader, or the news paid for");
        C(Times(events["Rimfolk_Evt_LineStorm"], "DilemmaEffect_LineClimb") == 2, "the mast climbed and made fast in the storm");
        C(Has(events["Rimfolk_Evt_UnionDrill"], "DilemmaEffect_UnionDrill") && Has(events["Rimfolk_Evt_UnionDrill"], "DilemmaEffect_UnionDrill", "together", "true")
          && Has(events["Rimfolk_Evt_UnionDrill"], "DilemmaEffect_Learn", "skill", "Mining"), "the seam drilled by the crew, the colony's best miner beside them if it likes");
        C(Has(events["Rimfolk_Evt_UnionGoods"], "DilemmaEffect_CarryOut", "what", "silver") && Has(events["Rimfolk_Evt_UnionGoods"], "DilemmaEffect_UnionGoods"), "the crate paid for and set down at the gate");
        C(Has(events["Rimfolk_Evt_UnionStrike"], "DilemmaEffect_CarryOut", "what", "rawfood") && Times(events["Rimfolk_Evt_UnionStrike"], "DilemmaEffect_UnionSettle") == 2
          && Has(events["Rimfolk_Evt_UnionStrike"], "DilemmaEffect_Learn", "skill", "Social"), "the strike settled by food for the picket line, or a good talker");
        C(Times(events["Rimfolk_Evt_RepublicCensus"], "DilemmaEffect_RepublicCensus") == 2 && Has(events["Rimfolk_Evt_RepublicCensus"], "DilemmaEffect_RepublicUncounted"),
            "the census-taker walks the colony, shown round if it likes, or is turned away");
        C(Times(events["Rimfolk_Evt_RepublicPapers"], "DilemmaEffect_RepublicPapers") == 2, "the papers taken, with the oath at the gate if it likes");
        C(Has(events["Rimfolk_Evt_RepublicConscript"], "DilemmaEffect_RepublicConscript") && Has(events["Rimfolk_Evt_RepublicConscript"], "DilemmaEffect_CarryOut", "what", "silver")
          && Has(events["Rimfolk_Evt_RepublicConscript"], "DilemmaEffect_RepublicRefused"), "a conscript sent, the exemption paid, or refused");
        C(Has(events["Rimfolk_Evt_FleetShuttle"], "DilemmaEffect_FleetShuttle", "casket", "true") && Has(events["Rimfolk_Evt_FleetShuttle"], "DilemmaEffect_Consume", "what", "silver"),
            "the shuttle down to trade, the casket bought if the colony likes");
        C(Times(events["Rimfolk_Evt_FleetSalvage"], "DilemmaEffect_FleetSalvage") == 2 && Has(events["Rimfolk_Evt_FleetSalvage"], "DilemmaEffect_Learn", "skill", "Crafting"),
            "the salvage bought and the chunks cut where they lie");
        C(Has(events["Rimfolk_Evt_FleetSleeper"], "DilemmaEffect_Join") && Has(events["Rimfolk_Evt_FleetSleeper"], "DilemmaEffect_NightLodging")
          && Ext(events["Rimfolk_Evt_FleetSleeper"], "HappeningExtension")!.Element("journal")?.Value == "Rimfolk_Journal_Evt_FleetSleeper", "the sleeper taken in, lodged for the night, or turned away");

        var missions = XDocument.Load(Path.Combine(root, "Defs", "MissionDefs", "Rimfolk_Missions_PeoplesF.xml")).Root!.Elements("IncidentDef").ToDictionary(Dn);
        string Only(string m) => Ext(missions[m], "MissionPeople")?.Element("only")?.Value;
        C(Only("Rimfolk_Mission_LineSpan") == "linesmen" && Only("Rimfolk_Mission_UnionShift") == "drillers" && Only("Rimfolk_Mission_RepublicCount") == "citizens"
          && Only("Rimfolk_Mission_FleetLift") == "crew", "an ask of each people, only theirs");
        C(Ext(missions["Rimfolk_Mission_UnionShift"], "MissionHands")!.Element("skill")?.Value == "Mining" && Ext(missions["Rimfolk_Mission_UnionShift"], "MissionHands")!.Element("hands")?.Value == "2"
          && Ext(missions["Rimfolk_Mission_RepublicCount"], "MissionHands")!.Element("skill")?.Value == "Social" && Ext(missions["Rimfolk_Mission_RepublicCount"], "MissionHands")!.Element("hands")?.Value == "1",
            "two good miners for a short shift, a witness at the count");
        C(missions["Rimfolk_Mission_LineSpan"].Descendants("what").Select(w => w.Value).SequenceEqual(new[] { "ComponentIndustrial", "Steel" })
          && missions["Rimfolk_Mission_FleetLift"].Descendants("what").Select(w => w.Value).SequenceEqual(new[] { "Plasteel", "ComponentIndustrial" }),
            "parts or steel for a fallen span, plasteel or parts for the lift");
        C(missions.Values.All(m => F(m.Element("baseChance")!.Value) <= 0.4f && int.Parse(m.Element("minRefireDays")!.Value) >= 60), "a small share of the missions' one draw, not soon again");
        var thoughts = XDocument.Load(Path.Combine(root, "Defs", "ThoughtDefs", "Rimfolk_Thoughts_PeoplesF.xml")).Root!.Elements("ThoughtDef").Select(Dn).ToHashSet();
        C(new[] { "Rimfolk_Thought_RadioPraised", "Rimfolk_Thought_RadioDenounced", "Rimfolk_Mission_ServedRepublic", "Rimfolk_Mission_WitnessedCount", "Rimfolk_Mission_ShiftWithUnion",
                "Rimfolk_Evt_StrikeSettled" }.All(thoughts.Contains) && missions.Values.SelectMany(m => m.Descendants("thought")).All(t => thoughts.Contains(t.Value)),
            "the colony's thoughts: the radio's praise and its denunciation, the season served, the count witnessed, the shift, the strike settled");

        // ================= The patch on Rimfolk's own defs =================

        var patch = XDocument.Load(Path.Combine(root, "Patches", "Rimfolk_PeoplesF.xml")).Root!;
        var defNames = allDefs.Select(Dn).Where(x => x != "").ToHashSet();
        foreach (var op in patch.Elements("Operation"))
        {
            var m = Regex.Match(op.Element("xpath")?.Value ?? "", "defName=\"([^\"]+)\"");
            C(op.Attribute("Class")?.Value == "PatchOperationConditional" && m.Success && defNames.Contains(m.Groups[1].Value), $"the patch reaches {m.Groups[1].Value}, conditionally");
        }
        C(patch.Descendants("calling").Select(c => c.Value).OrderBy(x => x).SequenceEqual(Four.OrderBy(x => x)), "four world events prefer them, one each");
        var earlier = Directory.GetFiles(Path.Combine(root, "Patches"), "Rimfolk_Peoples*.xml").Where(p => !p.EndsWith("Rimfolk_PeoplesF.xml"))
            .SelectMany(p => XDocument.Load(p).Root!.Elements("Operation")).Select(op => Regex.Match(op.Element("xpath")?.Value ?? "", "defName=\"([^\"]+)\"").Groups[1].Value).ToHashSet();
        C(patch.Elements("Operation").All(op => !earlier.Contains(Regex.Match(op.Element("xpath")!.Value, "defName=\"([^\"]+)\"").Groups[1].Value)),
            "each a world event no earlier release's people prefers");

        // ================= Release B's peoples that arise =================

        C(new[] { "linesmen", "crew" }.All(c => ArisingMath.Callings(ArisingMath.Refugees).Contains(c)) && new[] { "drillers", "citizens" }.All(c => ArisingMath.Callings(ArisingMath.Exiles).Contains(c))
          && Four.All(c => !ArisingMath.Callings(ArisingMath.League).Contains(c)), "survivors may keep a radio or a ship's watches, a band of the banished dig or vote; a league none");
        string Lean(int mining, int social, bool line, bool crew, int age, string kind = ArisingMath.Exiles) =>
            ArisingMath.Lean(kind, false, false, 0, 0, 0, false, false, age, 0, 0, 0, 0, mining, social, line, crew);
        C(Lean(12, 0, false, false, 2) == "drillers" && Lean(0, 12, false, false, 2) == "citizens" && Lean(12, 12, false, false, 2) == "drillers" && Lean(9, 9, false, false, 2) == null
          && ArisingMath.Lean(ArisingMath.Exiles, false, false, 0, 0, 0, false, false, 2, 12, 0, 0, 0, 12, 12, false, false) == "mercenaries",
            "a band led by a good miner digs as a union, by a good talker keeps a census; release E's leans first");
        C(Lean(0, 0, true, false, 2, ArisingMath.Refugees) == "linesmen" && Lean(0, 0, true, false, 1, ArisingMath.Refugees) == null && Lean(0, 0, false, true, 3, ArisingMath.Refugees) == "crew"
          && Lean(0, 0, false, true, 2, ArisingMath.Refugees) == null && Lean(0, 0, true, true, 3, ArisingMath.Refugees) == "linesmen"
          && ArisingMath.Lean(ArisingMath.Refugees, false, false, 0, 0, 0, false, true, 2, 0, 0, 0, 0, 0, 0, true, true) == "salvagers",
            "survivors of an industrial or spacer town on their fated quarter raise masts, of a spacer town on their fated third keep its watches; the Wheel's ways first");
        C(ArisingMath.Lean(ArisingMath.Exiles, false, false, 0, 9, 9, true, true, 3) == null && ArisingMath.Lean(ArisingMath.Exiles, false, false, 0, 0, 0, false, false, 2, 0, 0, 0, 1) == "seers",
            "release D's and E's leans as they were");
        C(ArisingMath.LineChance == 0.25f && ArisingMath.CrewChance == 0.33f && ArisingMath.SaltLine != ArisingMath.SaltCrew
          && new[] { ArisingMath.SaltRefugees, ArisingMath.SaltExiles, ArisingMath.SaltSplinter, ArisingMath.SaltRiver, ArisingMath.SaltBell, ArisingMath.SaltWheel }.All(s => s != ArisingMath.SaltLine && s != ArisingMath.SaltCrew),
            "their own fated rolls, apart from the others'");
        string arising = Read("Peoples", "Arising.cs");
        C(arising.Contains("ArisingMath.Fated(r.town, ArisingMath.SaltLine, ArisingMath.LineChance)") && arising.Contains("Skill(r.pawn, SkillDefOf.Mining), Skill(r.pawn, SkillDefOf.Social)"),
            "the town's fated rolls, the leader's own mining and talk");
        var arise = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Peoples_Arising.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (var (cause, c) in new[] { ("refugees", "linesmen"), ("refugees", "crew"), ("exiles", "drillers"), ("exiles", "citizens") })
            C(arise.ContainsKey($"CC_Arise_{cause}_{c}") && arise.ContainsKey($"CC_Arise_{cause}_{c}_2") && new[] { "", "_2", "_3" }.All(s => arise.TryGetValue($"CC_Arise_Name_{cause}_{c}{s}", out var v) && v.Contains("{WHAT}")),
                $"{cause}_{c}: how a new people of these ways lives, told two ways, and three names for it");
        C(arise["CC_Arise_refugees_linesmen"].Contains("mast") && arise["CC_Arise_refugees_crew"].Contains("watch") && arise["CC_Arise_exiles_drillers"].Contains("vote")
          && arise["CC_Arise_exiles_citizens"].Contains("census"), "and what it does is what its people do at the gate");
        var voices = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeopleAges.xml")).Root!.Elements().Select(e => e.Name.LocalName).ToHashSet();
        C(Four.All(c => voices.Contains("CC_PeopleAge_Voice_" + c + "_Rise") && voices.Contains("CC_PeopleAge_Voice_" + c + "_Fall")), "each tells its rise and its fall in its own voice");

        // ================= One harm, at the game's rate =================

        var harms = Regex.Matches(code + hooks, @"Harm\.(Roll|Injure|Ail|Hurt)\(([^;]*)").Select(m => m.Groups[2].Value).ToList();
        C(harms.Count == 2 && harms.All(h => h.Contains("HarmKinds.UnionDrill")), "nothing of theirs hurts anybody but a shift at the union's rig, through the harm budget");
        C(HarmKinds.All.Contains(HarmKinds.UnionDrill) && HarmKinds.UnionDrill.system == "peoples" && code.Contains("\"CC_PeoplesF_DrillMishap\""), "the rig's mishap in the budget's peoples' share, and shown");
        C(!Regex.IsMatch(Read("Peoples", "PeoplesF2.cs"), @"Harm\.|TakeDamage|AddHediff\(HediffDefOf\.(?!CryptosleepSickness)"),
            "the conscript's dangers are the world's own roads; the sleeper wakes only with cryptosleep sickness");

        // ================= Their words and their page =================

        var almanac = XDocument.Load(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_PeoplesF.xml")).Root!.Elements().Single();
        C(Dn(almanac) == "Council_Alm_C_PeoplesF" && almanac.Element("keywords")!.Elements("li").Count() >= 4 && almanac.Element("seeAlso")!.Elements("li").Any(l => l.Value == "Council_Alm_C_PeoplesE"),
            "an Almanac page of their own, beside release E's");
        var almText = string.Join("\n", almanac.Descendants().Where(e => !e.HasElements).Select(e => e.Value));
        C(!almText.Contains('—') && !almText.Contains('!') && !Regex.IsMatch(almText, @"\bin \d+ (days?|hours?)\b"), "its words plain, no countdowns");
        C(almText.Contains("half the days") && almText.Contains("a couple of hours") && almText.Contains("a fifth over") && almText.Contains("fifty silver") && almText.Contains("two in five")
          && almText.Contains("six hours") && almText.Contains("one shift in twenty") && almText.Contains("after a month") && almText.Contains("six in a hundred")
          && almText.Contains("fifteen days") && almText.Contains("three tenths") && almText.Contains("under half its strength"), "its rules are the code's");
        C(almText.Contains("Ennis Calloway") && almText.Contains("Marta Grieve") && almText.Contains("Clement Ashdown") && almText.Contains("Imre Sallow"), "and its foundings the defs'");
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeoplesF.xml")).Root!.Elements().Select(e => e.Name.LocalName).ToHashSet();
        var used = Regex.Matches(code + hooks, "\"(CC_PeoplesF_\\w+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        C(used.Count >= 40 && used.All(keyed.Contains), $"every word the code says is keyed ({used.Count}{(used.Any(u => !keyed.Contains(u)) ? ", missing " + string.Join(", ", used.Where(u => !keyed.Contains(u))) : "")})");
        var fTexts = new[] { Path.Combine("Defs", "IncidentDefs", "Rimfolk_Events_PeoplesF.xml"), Path.Combine("Defs", "FlavorPoolDefs", "Rimfolk_Lines_PeoplesF.xml"),
                Path.Combine("Languages", "English", "Keyed", "Rimfolk_PeoplesF.xml"), Path.Combine("Defs", "MissionDefs", "Rimfolk_Missions_PeoplesF.xml"),
                Path.Combine("Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_PeoplesF.xml"), Path.Combine("Defs", "AlmanacArticleDefs", "Almanac_PeoplesF.xml") }
            .Select(p => File.ReadAllText(Path.Combine(root, p)).ToLowerInvariant()).Aggregate((a, b) => a + b);
        C(!Regex.IsMatch(fTexts, @"\b(dynasty|dynasties|bloodline|heir to the house)\b"), "the colony is the family: no houses, no bloodlines");

        // ================= The seams =================

        C(!Regex.IsMatch(code + Read("Peoples", "PeoplesFMath.cs") + hooks, @"Sched\.|Scribe_|ExposeData"), "no clock and nothing saved of their own: the visits Rimfolk already keeps hold it");
        C(Regex.Matches(hooks, @"\[HarmonyPatch\(").Count == 6 && Regex.Matches(hooks, @"RimfolkPatches\.On").Count >= 6, "six game hooks, each off with the deeper game hooks");
        C(hooks.Contains("typeof(Storyteller), nameof(Storyteller.TryFire)") && hooks.Contains("if (queued ||") && code.Contains("Find.Storyteller.incidentQueue.Add(new QueuedIncident(fi, at,"),
            "a raid the storyteller sends held in the game's own queue for the warning, never one already coming back out of it");
        C(hooks.Contains("typeof(IncidentWorker_TraderCaravanArrival), nameof(IncidentWorker_TraderCaravanArrival.FactionCanBeGroupSource)")
          && hooks.Contains("typeof(IncidentWorker_VisitorGroup), \"TryConvertOnePawnToSmallTrader\"") && hooks.Contains("typeof(Settlement), nameof(Settlement.CanTradeNow), MethodType.Getter"),
            "a strike stalls the union's caravans, its visitors' trade and trade at its towns");
        C(hooks.Contains("nameof(Pawn.TradePriceImprovementOffsetForPlayer)") && hooks.Contains("nameof(Settlement.TradePriceImprovementOffsetForPlayer)") && hooks.Contains("Papers.Terms(__instance.Faction)"),
            "papers give a citizen's terms with the Republic's traders, caravans and towns");
        string happen = Read("Happenings", "Happenings.cs");
        C(happen.Contains("case UnionStrike.Kind:") && happen.Contains("case Papers.Kind:") && happen.Contains("case Conscripts.Kind:"), "the strike, the papers and the conscript ride on the visits Rimfolk already keeps");
        C(Read("World", "Region", "RegionBook.cs").Contains("PeoplesF.Daily(now)"), "strikes called and the radio heard on the region's day");
        string letters = Read("World", "NeighbourLetters.cs");
        C(Regex.Matches(letters, @"RelayMast\.Working\(l\.map\)").Count >= 2 && letters.Contains("PeoplesFMath.Relayed(l.backDays)"), "a letter by a messenger or a bearer, and its answer, down the mast");
        string reach = Read("Post", "PostReach.cs");
        C(reach.Contains("return RelayMast.Working(home);") && reach.Contains("Peoples.CallingOf(f) == PeoplesMath.Linesmen"), "the post's reach widened by a singing mast at home, or a town of a people that keeps masts");
        C(code.Contains("CaravanExitMapUtility.ExitMapAndCreateCaravan(") && code.Contains("CaravanMaker.MakeCaravan(") && code.Contains("new CaravanArrivalAction_Enter(")
          && code.Contains("WorldLedger.Keep(p, KeepFor.Lent"), "the conscript goes and comes back as a caravan of one over the world's own roads, the colony's still");
        C(code.Contains("TransportShipMaker.MakeTransportShip(TransportShipDefOf.Ship_Shuttle") && code.Contains("DropPodUtility.DropThingsNear(spot, map"), "the Fleet's shuttle, or drop pods without Royalty");
        C(code.Contains("Couriers.TakeFor(ctx, PlayCarry.Salvage") && Read("PlayOut", "PlayCarry.cs").Contains("public const string Salvage = \"salvage\";") && code.Contains("Couriers.Loot("),
            "the salvage carried out to the buyer, the union's crate set down at the gate");
        string pace = Read("PaceVisit.cs");
        C(!Regex.IsMatch(code, @"LocomotionUrgency\.Walk|new LordToil_Travel\(|new LordJob_VisitColony\(") && Regex.Matches(code, @"new LordJob_VisitOnErrand\(").Count >= 4
          && pace.Contains("exit.locomotion = Pace.Errand") && pace.Contains("new LordToil_TravelOnErrand(toil.FlagLoc)") && Read("Happenings", "HappeningHelpers.cs").Contains("new LordJob_VisitOnErrand(f, spot,")
          && code.Contains("walk.locomotionUrgency = Pace.Errand") && Read("Peoples", "PeoplesE.cs").Contains("job.locomotionUrgency = Pace.Errand"),
            "they come, go about their work and leave at an errand's pace, not the game's half-speed walk");
        var misc = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_PeoplesF_Misc.xml")).Root!.Elements().ToList();
        var mast = misc.Single(e => Dn(e) == "Rimfolk_RelayMast");
        C(mast.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.CompProperties_RelayMast") && mast.Element("tickerType")?.Value == "Rare"
          && File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Things", "RelayMast.png")), "the mast is a building of its own, worn by the weather, with its own texture");
        C(new[] { "RaiseMast", "ClimbMast", "Drill", "Count", "CutChunk", "ConscriptOut" }.All(j => misc.Any(e => Dn(e) == "Rimfolk_PeoplesF_" + j)), "their work on the map has jobs of its own");
        C(new[] { "SignalLine", "Deepwell", "Republic", "HollowFleet" }.All(p => File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Peoples", p + ".png"))
                                                                               && File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Peoples", p + "Town.png"))),
            "an emblem and a town icon for each");
        C(Regex.Matches(code, @"Speech\.Say\(").Count >= 1 && code.Contains("PeoplesF.SayLine(") && !code.Contains("MoteMaker.ThrowText"), "they speak through Rimfolk's speech");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
