using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Rimfolk's own peoples, release G (Peoples/PeoplesGMath.cs, PeoplesG.cs, PeoplesG2.cs; docs/design/peoples.md,
/// "Release G"; docs/design/peoples-third.md, step 3): the Helix Works, the Lattice, the Choir of the Spire and the Sunken
/// Vault. Their pure rules (the sample and its draw, gene work, the case, the batch; the repairs, the lost mech, the parts
/// and the turret; the listening's calm and unease, the art and the stay; the vault's decline unless supplied, its barter,
/// its lost craft and its child), their defs (none an enemy by nature, once each in a new world, the Works mostly not
/// baseliners, the Lattice's mechs only with Biotech, the Choir without armour, the Vault's suit), their arms for another
/// age, their towns' plans, their callers and asks, the patch, release B's arising with them, their harm (none rolled),
/// their words and page, and the seams. Release A's checks that every people meets run over all twenty-three in
/// PeoplesTests, PeoplesGearTests and PeoplesLoreTests.
/// </summary>
public static class PeoplesGTests
{
    private static readonly string[] Four = { "editors", "mechanitors", "listeners", "vaultfolk" };

    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "peoples, release G: " + what);
        }
        float F(string v) => float.Parse(v, CultureInfo.InvariantCulture);

        // ================= The Helix Works =================

        C(PeoplesGMath.Rarity(0, 0, true) == 0 && PeoplesGMath.Rarity(1, 0, true) == 3 && PeoplesGMath.Rarity(0, 10, false) == 10 && PeoplesGMath.Rarity(0, 30, false) == 12
          && PeoplesGMath.Rarity(2, 5, false) == 11 && PeoplesGMath.Rarity(-1, -4, false) == 0 && PeoplesGMath.Rarity(0, 9, true) == 0,
            "how rare a colonist's genes are: an archite gene three, a xenotype's own genes one each to twelve, a plain baseliner nothing");
        C(!PeoplesGMath.WorthASample(2) && PeoplesGMath.WorthASample(3) && PeoplesGMath.SamplePrice(0) == 80 && PeoplesGMath.SamplePrice(3) == 155
          && PeoplesGMath.SamplePrice(24) == 680 && PeoplesGMath.SamplePrice(99) == 680, "a sample worth asking for, 80 silver and 25 a point, at most 680");
        C(PeoplesGMath.DrawSeverity > 0f && PeoplesGMath.DrawSeverity < 0.15f && PeoplesGMath.DrawTicks > 0 && PeoplesGMath.HoldTicks > PeoplesGMath.DrawTicks,
            "the draw is a little blood, below the game's first stage of blood loss");
        C(PeoplesGMath.GenePrice(0, 0) == 180 && PeoplesGMath.GenePrice(-2, 0) == 360 && PeoplesGMath.GenePrice(-1, 2) == 350 && PeoplesGMath.GenePrice(3, 0) == 180
          && PeoplesGMath.GeneWorkTicks == 2 * PeoplesGMath.HourTicks && PeoplesGMath.GenepackPay == 1, "a gene's price by what it costs the body, two hours' work, or a genepack");
        var genes = File.ReadAllLines(Path.Combine(root, "docs", "player-mods", "genes.tsv")).Skip(1).Select(l => l.Split('\t')).Where(c => c.Length > 4)
            .GroupBy(c => c[0]).ToDictionary(g => g.Key, g => g.Last());
        C(PeoplesGMath.Offered.Length >= 8 && PeoplesGMath.Offered.All(g => genes.TryGetValue(g, out var r) && r[1] == "ludeon.rimworld.biotech" && r[2] == "yes" && r[4] != "Archite"),
            "the Works' genes are Biotech's own gentle ones, none archite");
        C(PeoplesGMath.CaseMedicine(1f) == 6 && PeoplesGMath.CaseMedicine(0f) == 4 && PeoplesGMath.CaseMedicine(5f) == 12 && PeoplesGMath.CaseUltratech(1f) == 1
          && PeoplesGMath.CaseUltratech(2f) == 2 && PeoplesGMath.CasePenoxycyline(1f) == 4 && PeoplesGMath.CasePrice(100f) == 110 && PeoplesGMath.CasePrice(float.NaN) == 0,
            "the vat-keeper's case by the colony's wealth, a tenth over its worth");
        C(PeoplesGMath.BatchSize(0f) == 1 && PeoplesGMath.BatchSize(0.5f) == 2 && PeoplesGMath.BatchSize(0.999f) == 3 && PeoplesGMath.BatchSize(float.NaN) == 1
          && PeoplesGMath.FaultStart < 0.9f && PeoplesGMath.BatchMedicineEach == 2 && PeoplesGMath.BatchGoodwill > 0, "a batch of one to three, sick but never near the worst");

        // ================= The Lattice =================

        C(PeoplesGMath.RepairPrice(1, 2) == 70 && PeoplesGMath.RepairPrice(3, 7) == 230 && PeoplesGMath.RepairPrice(6, 40) == 600 && PeoplesGMath.RepairPrice(0, 0) == 0
          && PeoplesGMath.RepairMax == 6 && PeoplesGMath.SubcorePay == 1, "repairs at 40 a mech and 15 a wound, at most 600, or a subcore");
        C(PeoplesGMath.KeptGoodwill == -15 && PeoplesGMath.ReturnedGoodwill > 0 && PeoplesGMath.CanKeep(true, 2f, 1f) && !PeoplesGMath.CanKeep(false, 9f, 1f)
          && !PeoplesGMath.CanKeep(true, 0.5f, 1f) && !PeoplesGMath.CanKeep(true, float.NaN, 1f), "a lost mech kept only by a mechanitor with the bandwidth, at fifteen of their goodwill");
        C(PeoplesGMath.PartsPrice(100f) == 120 && PeoplesGMath.PartsMinWorth == 150f && PeoplesGMath.TurretPrice(400f) == 440 && PeoplesGMath.TurretHours == 2,
            "parts at a fifth over their worth, a turret at a tenth over");

        // ================= The Choir of the Spire =================

        C(PeoplesGMath.Felt(0f, 0f, false) == 0 && PeoplesGMath.Felt(0.04f, 0.5f, true) == 0 && PeoplesGMath.Felt(1f, 0.1f, false) == -1 && PeoplesGMath.Felt(1f, 0.3f, false) == 1
          && PeoplesGMath.Felt(float.NaN, 0.9f, false) == 1, "the psychically deaf feel nothing; the rest a calm or an unease");
        C(Math.Abs(PeoplesGMath.UneaseChance(1f, false) - 0.2f) < 1e-4f && PeoplesGMath.UneaseChance(2f, false) > PeoplesGMath.UneaseChance(1f, false)
          && PeoplesGMath.UneaseChance(1f, true) > PeoplesGMath.UneaseChance(1f, false) && PeoplesGMath.UneaseChance(9f, true) <= 0.6f && PeoplesGMath.UneaseChance(0.1f, false) >= 0.05f,
            "unease one time in five, more for the sensitive and when the void stirs, never more than three in five");
        int calm = 0, uneasy = 0;
        for (int i = 0; i < 1000; i++)
        {
            int felt = PeoplesGMath.Felt(1f, i / 1000f, false);
            if (felt > 0) calm++;
            else if (felt < 0) uneasy++;
        }
        C(calm > uneasy * 3 && uneasy > 150, $"a calm for most, an unease for some ({calm} and {uneasy} of a thousand)");
        C(PeoplesGMath.Pick(500f, new List<float> { 600f, 700f, 1300f }) == 0 && PeoplesGMath.Pick(1100f, new List<float> { 600f, 700f, 1300f }) == 2
          && PeoplesGMath.Pick(300f, new List<float> { 600f }) == -1 && PeoplesGMath.Pick(float.NaN, new List<float> { 1f }) == -1 && PeoplesGMath.Pick(500f, null) == -1
          && PeoplesGMath.ArtMinWorth == 300f && PeoplesGMath.ArtPieces == 2, "for art, the dearest of the Spire's goods no more than a quarter over its worth");
        C(PeoplesGMath.StayMinDays < PeoplesGMath.StayMaxDays && PeoplesGMath.OrdinaryHour(14) && !PeoplesGMath.OrdinaryHour(8) && !PeoplesGMath.OrdinaryHour(20),
            "the listener's stay of some days, an hour each afternoon");

        // ================= The Sunken Vault =================

        C(PeoplesGMath.SupplyWeight(0f) == 0 && PeoplesGMath.SupplyWeight(60f) == 2 && PeoplesGMath.SupplyWeight(600f) == 10 && PeoplesGMath.SupplyWeight(1e6f) == 30
          && PeoplesGMath.SupplyWeight(float.NaN) == 0 && PeoplesGMath.SupplyWeight(300f) * 60f == 300f, "what the colony gives is remembered as supply by its worth");
        C(PeoplesGMath.BarterComponents(1f) == 6 && PeoplesGMath.BarterComponents(0f) == 4 && PeoplesGMath.BarterComponents(9f) == 12 && PeoplesGMath.BarterFood(1f) == 60
          && PeoplesGMath.BarterFood(0f) == 40 && PeoplesGMath.BarterFood(3f) == 120 && PeoplesGMath.GiftGoodwill > PeoplesGMath.BarterGoodwill,
            "the last spacer goods for components and food by the colony's wealth; given for nothing, a friend's");
        C(PeoplesGMath.TeachHours == 3 && PeoplesGMath.CraftWorth > 0f && PeoplesGMath.ChildMax < 13f && PeoplesGMath.YouthMin >= 13f && PeoplesGMath.SkyTicks > 0,
            "a craft taught in a few hours; a child with Biotech, a youth without");
        // The vault's decline, played out from its def's ways (PeopleAgesMath.Season, 0.169.0's rule).
        var vaultWays = new AgeWays { pace = 12f, limit = 3, floor = 1, declines = true, declineYears = PeoplesGMath.DeclineYears };
        (int age, int seasons) Fall(AgeDeeds d, float fate, int max)
        {
            int age = 3, seasons = 0;
            float progress = 0f;
            while (seasons < max)
            {
                var st = PeopleAgesMath.Season(age, progress, vaultWays, fate, d);
                seasons++;
                if (st.change < 0 && age == 3)
                {
                    age = st.age;
                    progress = st.progress;
                    return (age, seasons);
                }
                age = st.age;
                progress = st.progress;
            }
            return (age, seasons);
        }
        var first = Fall(new AgeDeeds(), 1f, 400);
        C(first.age == 2 && first.seasons >= 15 && first.seasons <= 17, $"unsupplied, the vault falls from the spacer age in about four years ({first.seasons} seasons)");
        int a = 3, s = 0;
        float p = 0f;
        for (int i = 0; i < 160; i++)
        {
            var st = PeopleAgesMath.Season(a, p, vaultWays, 1f, new AgeDeeds());
            a = st.age;
            p = st.progress;
            s++;
        }
        C(a == 1, $"and on down to the medieval age, never lower (forty years: the age {a})");
        var held = Fall(new AgeDeeds { supply = 1500f }, 1f, 80);
        var half = Fall(new AgeDeeds { gifts = 750f }, 1f, 400);
        C(held.age == 3 && half.age == 2 && half.seasons > first.seasons * 1.8f, $"kept supplied it holds its age; half supplied it falls half as fast ({half.seasons} seasons)");

        // ================= Their defs =================

        string peopleDir = Path.Combine(root, "Defs", "PeopleDefs");
        var all = Directory.GetFiles(peopleDir, "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements()).ToList();
        string Dn(XElement e) => e.Element("defName")?.Value ?? "";
        XElement Ext(XElement d, string cls) => d.Element("modExtensions")?.Elements("li").FirstOrDefault(li => li.Attribute("Class")?.Value == "ColonyCouncil." + cls);
        var factions = all.Where(e => e.Name.LocalName == "FactionDef").ToDictionary(f => Ext(f, "PeopleExtension")!.Element("calling")!.Value);
        C(Four.All(factions.ContainsKey) && Dn(factions["editors"]) == "Rimfolk_HelixWorks" && Dn(factions["mechanitors"]) == "Rimfolk_Lattice"
          && Dn(factions["listeners"]) == "Rimfolk_Choir" && Dn(factions["vaultfolk"]) == "Rimfolk_SunkenVault", "their defs");
        C(Four.All(c => factions[c].Element("techLevel")!.Value == "Spacer"), "all four of the spacer age (the Choir beyond it, with the archotech's flavour)");
        string Way(string c, string what) => Ext(factions[c], "PeopleExtension")!.Element(what)?.Value;
        C(Four.All(c => Way(c, "agePace") != null && Way(c, "ageLimit") == "3") && Way("vaultfolk", "ageDeclines") == "true" && Way("vaultfolk", "ageFloor") == "1"
          && F(Way("vaultfolk", "ageDeclineYears")!) == PeoplesGMath.DeclineYears && new[] { "editors", "mechanitors", "listeners" }.All(c => Way(c, "ageDeclines") == null),
            "at the height of the ages; the vault the one that falls unless supplied, about every four years, never below the medieval age");
        var allDefs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).SelectMany(f => XDocument.Load(f).Root!.Elements()).ToList();
        var camps = allDefs.Where(e => e.Name.LocalName == "ColonyCouncil.CampKindDef").Select(Dn).ToHashSet();
        float Raid(string c) => F(factions[c].Element("raidCommonalityFromPointsCurve")!.Descendants("li").Single().Value.Trim('(', ')').Split(',')[1]);
        foreach (var c in Four)
        {
            var f = factions[c];
            var ext = Ext(f, "PeopleExtension")!;
            string name = Dn(f);
            C(f.Element("naturalEnemy")?.Value == "false" && f.Element("permanentEnemy")?.Value == "false" && f.Element("requiredCountAtGameStart")?.Value == "0"
              && f.Element("startingCountAtWorldCreation")?.Value == "1", $"{name}: nobody's enemy by nature, once in a new world, never required");
            C(Raid(c) <= 0.15f, $"{name}: raids rarely, and only when wronged");
            C(ext.Element("camps")!.Elements("li").Count() == 3 && ext.Element("camps")!.Elements("li").All(li => camps.Contains(li.Value)), $"{name}: three kinds of camp it favours, each a real one");
            C(f.Element("fixedIdeo")?.Value == "true" && !string.IsNullOrEmpty(f.Element("ideoName")?.Value) && f.Element("styles")!.Elements("li").All(li => li.Attribute("MayRequire")?.Value == "Ludeon.RimWorld.Ideology"),
                $"{name}: a faith of its own, and its style only with Ideology");
            foreach (var w in new[] { "head", "envoy", "trader", "courier" })
                C(!string.IsNullOrEmpty(ext.Element(w)?.Value), $"{name}: its own word for its {w}");
        }
        C(Four.Where(c => c != "listeners").All(c => Raid("listeners") < Raid(c)), "the Choir rarely fights at all");
        C(Way("listeners", "ariseAges") == "0~3" && new[] { "editors", "mechanitors", "vaultfolk" }.All(c => Way(c, "ariseAges") == "2~3"),
            "the Choir arises in every age; gene work, mechs and a shelter want the industrial age at least");
        float Xeno(string c) => factions[c].Element("xenotypeSet")!.Element("xenotypeChances")!.Elements().Sum(e => F(e.Value));
        C(Xeno("editors") > 0.5f && factions["editors"].Element("xenotypeSet")!.Element("xenotypeChances")!.Elements().Count() >= 25
          && new[] { "mechanitors", "listeners", "vaultfolk" }.All(c => Xeno(c) <= 0.35f), "the Works mostly their own work, from every kind of people; the others mostly baseliners");
        var mechOpts = all.Where(e => e.Name.LocalName == "FactionDef").SelectMany(f => f.Element("pawnGroupMakers")!.Descendants().Where(o => o.Name.LocalName.StartsWith("Mech_")).Select(o => (f, o))).ToList();
        C(mechOpts.Count >= 6 && mechOpts.All(x => Dn(x.f) == "Rimfolk_Lattice" && x.o.Attribute("MayRequire")?.Value == "Ludeon.RimWorld.Biotech")
          && !factions["mechanitors"].Element("pawnGroupMakers")!.Elements("li").Single(li => li.Element("kindDef")!.Value == "Peaceful").Descendants().Any(o => o.Name.LocalName.StartsWith("Mech_")),
            "the Lattice's mechs fight, guard its caravans and work its yards, only with Biotech, never on a peaceful visit");
        C(factions["listeners"].Element("forcedMemes")!.Elements("li").Any(li => li.Value == "AM_Monastic" && li.Attribute("MayRequire")?.Value == "sarg.alphamemes")
          && factions["vaultfolk"].Element("forcedMemes")!.Elements("li").Any(li => li.Value == "Tunneler"), "the Choir monastic with Alpha Memes; the vault tunnelers");

        // ================= Their look, and their arms for another age =================

        var kinds = all.Where(e => e.Name.LocalName == "PawnKindDef" && e.Attribute("Abstract") == null).ToList();
        List<XElement> Of(string prefix) => kinds.Where(k => Dn(k).StartsWith(prefix)).ToList();
        List<string> Required(XElement k) => k.Element("apparelRequired")?.Elements("li").Select(li => li.Value).ToList() ?? new List<string>();
        XElement Base(string name) => all.Single(e => e.Attribute("Name")?.Value == name);
        List<string> Dyed(string name) => Base(name).Element("specificApparelRequirements")!.Elements("li").Select(li => li.Element("apparelDef")!.Value).ToList();
        C(Of("Rimfolk_HelixWorks_").Count == 8 && Of("Rimfolk_Lattice_").Count == 6 && Of("Rimfolk_Choir_").Count == 5 && Of("Rimfolk_SunkenVault_").Count == 6,
            "their kinds of people by role");
        C(Of("Rimfolk_HelixWorks_").All(k => Required(k).Contains("Apparel_CollarShirt")) && Dyed("Rimfolk_HelixWorksKindBase").Contains("Apparel_LabCoat")
          && Dyed("Rimfolk_HelixWorksKindBase").Contains("Apparel_CollarShirt") && Of("Rimfolk_HelixWorks_").Count(k => Required(k).Contains("Apparel_LabCoat")) >= 4,
            "the Works in clinic mint, the lab coat on their editors and the mint shirt under it on everyone");
        C(Of("Rimfolk_Lattice_").All(k => Required(k).Contains("Apparel_Robe")) && Dyed("Rimfolk_LatticeKindBase").SequenceEqual(new[] { "Apparel_Robe" })
          && Required(kinds.Single(k => Dn(k) == "Rimfolk_Lattice_MechTender")).Contains("Apparel_AirwireHeadset")
          && kinds.Single(k => Dn(k) == "Rimfolk_Lattice_MechTender").Descendants("techHediffsRequired").Any(), "the Lattice in the indigo robe, its mech-tenders with headset and mechlink");
        var choir = Of("Rimfolk_Choir_");
        string[] armour = { "Apparel_ArmorRecon", "Apparel_FlakVest", "Apparel_PlateArmor", "Apparel_AdvancedHelmet", "Apparel_SimpleHelmet", "Apparel_ArmorHelmetRecon" };
        C(choir.All(k => Required(k).Contains("Apparel_Robe") && !Required(k).Any(armour.Contains)
                         && !(k.Element("apparelTags")?.Elements("li").Any(li => li.Value.Contains("Military") || li.Value.Contains("Armor")) ?? false)),
            "the Choir in pale robes, and no armour on any of them");
        C(choir.Where(k => k.Descendants("psylink").Any()).Count() >= 4 && choir.Where(k => k.Element("requiredWorkTags") != null).All(k => k.Descendants("psylink").Any()),
            "most of them psycasters; those who fight, with the mind");
        C(Of("Rimfolk_SunkenVault_").All(k => Required(k).Take(2).SequenceEqual(new[] { "Apparel_CollarShirt", "Apparel_Pants" }))
          && Dyed("Rimfolk_SunkenVaultKindBase").Take(2).SequenceEqual(new[] { "Apparel_CollarShirt", "Apparel_Pants" }), "the vault suit in vault olive on every dweller");
        var patched = Ext(kinds.Single(k => Dn(k) == "Rimfolk_SunkenVault_Dweller"), "PeopleKindExtension")!.Element("ageArms")!.Elements("li").ToList();
        C(patched.Select(x => x.Element("ages")!.Value).SequenceEqual(new[] { "1~1", "2~2" }) && patched.All(x => x.Element("apparel")!.Elements("li").Any() && x.Element("weaponTags") == null),
            "in a poorer age the suit patched with whatever is at hand");
        var armed = kinds.Where(k => Four.Select(c => Dn(factions[c]) + "_").Any(pfx => Dn(k).StartsWith(pfx)) && Ext(k, "PeopleKindExtension")?.Element("ageArms") != null
                                     && Ext(k, "PeopleKindExtension")!.Element("ageArms")!.Descendants("weaponTags").Any()).ToList();
        C(armed.Select(Dn).OrderBy(x => x).SequenceEqual(new[] { "Rimfolk_HelixWorks_Guard", "Rimfolk_Lattice_Warden", "Rimfolk_SunkenVault_Security" }),
            "their fighters take the arms of another age");
        foreach (var k in armed)
            foreach (var set in Ext(k, "PeopleKindExtension")!.Element("ageArms")!.Elements("li"))
            {
                var ages = set.Element("ages")!.Value.Split('~').Select(int.Parse).ToArray();
                C(ages[0] >= 1 && ages[1] <= 2 && set.Element("weaponTags")!.Elements("li").All(li => li.Attribute("MayRequire") == null),
                    $"{Dn(k)}: the base game's arms for the ages {ages[0]} to {ages[1]}, the ages it can fall back to");
            }

        // ================= Their towns' plans =================

        var plans = XDocument.Load(Path.Combine(peopleDir, "Rimfolk_TownPlans_PeoplesG.xml")).Root!.Elements("ColonyCouncil.TownPlanDef").ToDictionary(Dn);
        List<string> Rows(string plan) => plans[plan].Element("rows")!.Elements("li").Select(li => li.Value).ToList();
        int Count(string plan, Func<XElement, bool> pick) =>
            plans[plan].Element("keys")!.Elements("li").Where(pick).Select(k => k.Element("key")!.Value[0]).Sum(ch => Rows(plan).Sum(r => r.Count(x => x == ch)));
        int Things(string plan, string thing) => Count(plan, k => k.Element("thing")?.Value == thing);
        int Marks(string plan, string mark) => Count(plan, k => k.Element("mark")?.Value == mark);
        C(plans.Keys.SequenceEqual(new[] { "Rimfolk_Plan_HelixLab", "Rimfolk_Plan_LatticeYards", "Rimfolk_Plan_ChoirRing", "Rimfolk_Plan_VaultDoor" })
          && Four.All(c => plans.ContainsKey(Way(c, "plan")!)), "a plan for each of release G's peoples");
        C(Things("Rimfolk_Plan_HelixLab", "HiTechResearchBench") >= 2 && Things("Rimfolk_Plan_HelixLab", "HospitalBed") >= 3 && Marks("Rimfolk_Plan_HelixLab", "plant:Plant_Rose") >= 10,
            "the lab: its benches, the vat hall's cradles and a quiet garden");
        C(Things("Rimfolk_Plan_LatticeYards", "CommsConsole") == 1 && Things("Rimfolk_Plan_LatticeYards", "TableMachining") >= 2 && Things("Rimfolk_Plan_LatticeYards", "Battery") >= 6
          && Things("Rimfolk_Plan_LatticeYards", "Turret_MiniTurret") == 4, "the yards: the control tower, the gestation hall, the batteries under four turrets");
        C(Things("Rimfolk_Plan_ChoirRing", "SculptureGrand") == 1 && Things("Rimfolk_Plan_ChoirRing", "Column") >= 4 && Things("Rimfolk_Plan_ChoirRing", "Bookcase") >= 2,
            "the ring: a monolith among columns, and its quiet halls");
        C(Things("Rimfolk_Plan_VaultDoor", "Autodoor") >= 2 && Things("Rimfolk_Plan_VaultDoor", "Bed") >= 10 && Things("Rimfolk_Plan_VaultDoor", "Bedroll") >= 6
          && Things("Rimfolk_Plan_VaultDoor", "Campfire") >= 2 && Marks("Rimfolk_Plan_VaultDoor", "plant:Plant_Potato") >= 10, "the vault behind its door, and the surface camp round it");

        // ================= Their callers and asks =================

        var events = XDocument.Load(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeoplesG.xml")).Root!.Elements("IncidentDef").ToDictionary(Dn);
        C(events.Keys.SequenceEqual(new[] { "Rimfolk_Evt_HelixSample", "Rimfolk_Evt_HelixGeneWork", "Rimfolk_Evt_HelixCase", "Rimfolk_Evt_HelixBatch", "Rimfolk_Evt_LatticeRepair",
            "Rimfolk_Evt_LatticeLostMech", "Rimfolk_Evt_LatticeParts", "Rimfolk_Evt_LatticeTurret", "Rimfolk_Evt_ChoirListening", "Rimfolk_Evt_ChoirArt", "Rimfolk_Evt_VaultBarter",
            "Rimfolk_Evt_VaultCraft", "Rimfolk_Evt_VaultChild" }), "thirteen callers at the gate: four of the Works and the Lattice, two of the Choir (and its ask), three of the Vault");
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { src }.Concat(parts).ToArray()));
        string code = Read("Peoples", "PeoplesG.cs") + Read("Peoples", "PeoplesG2.cs");
        string everyCode = string.Join("\n", Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        foreach (var (name, e) in events)
        {
            string worker = e.Element("workerClass")!.Value.Replace("ColonyCouncil.", "");
            C(code.Contains("class " + worker + " "), $"{name}: its worker is release G's own");
            var effects = e.Descendants("li").Select(li => li.Attribute("Class")?.Value).Where(v => v != null && v.StartsWith("ColonyCouncil.DilemmaEffect_")).Select(v => v.Replace("ColonyCouncil.", "")).Distinct();
            C(effects.All(x => Regex.IsMatch(everyCode, @"class " + x + @"\b")), $"{name}: every answer's effect is real");
            var ext = Ext(e, "HappeningExtension")!;
            var options = ext.Element("options")!.Elements("li").ToList();
            C(options.Count == 3 && options[2].Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_SendAway"),
                $"{name}: three answers, the last to send them on their way");
            C(F(e.Element("baseChance")!.Value) <= 2f && int.Parse(e.Element("minRefireDays")!.Value) >= 12, $"{name}: a small share of the callers, not soon again");
            string journal = ext.Element("journal")?.Value;
            C(journal == null || allDefs.Any(d => Dn(d) == journal), $"{name}: the colonists write about it in a journal that exists");
            var lineTags = ext.Element("lines")?.Elements("li").Select(l => Regex.Match(l.Value, @"^\[([^\],]+)").Groups[1].Value).Distinct().ToList() ?? new List<string>();
            C(lineTags.Count > 0 && lineTags.All(t => code.Contains("\"" + t + "\"")), $"{name}: every line's moment is one its worker says it at");
            bool biotech = ext.Element("mods")?.Elements("li").Any(li => li.Value == "Ludeon.RimWorld.Biotech") ?? false;
            bool wants = new[] { "HelixSample", "HelixGeneWork", "HelixBatch", "LatticeRepair", "LatticeLostMech", "LatticeParts" }.Any(x => name.EndsWith(x));
            C(biotech == wants, $"{name}: {(wants ? "only with Biotech" : "with or without Biotech")}");
        }
        bool Has(XElement d, string cls, string field = null, string value = null) =>
            d.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil." + cls && (field == null || li.Element(field)?.Value == value));
        int Times(XElement d, string cls) => d.Descendants("li").Count(li => li.Attribute("Class")?.Value == "ColonyCouncil." + cls);
        C(Times(events["Rimfolk_Evt_HelixSample"], "DilemmaEffect_HelixSample") == 2 && Has(events["Rimfolk_Evt_HelixSample"], "DilemmaEffect_Learn", "skill", "Medicine"),
            "the sample drawn where they stand, the colony's doctor watching if it likes");
        C(Times(events["Rimfolk_Evt_HelixGeneWork"], "DilemmaEffect_HelixGene") == 2 && Has(events["Rimfolk_Evt_HelixGeneWork"], "DilemmaEffect_CarryOut", "what", "silver")
          && Has(events["Rimfolk_Evt_HelixGeneWork"], "DilemmaEffect_CarryOut", "what", "Genepack"), "a gene written in for silver or a genepack");
        C(Times(events["Rimfolk_Evt_HelixCase"], "DilemmaEffect_PeoplesGCrate") == 2, "the case bought and set down at the gate");
        C(Has(events["Rimfolk_Evt_HelixBatch"], "DilemmaEffect_HelixBatch") && Has(events["Rimfolk_Evt_HelixBatch"], "DilemmaEffect_CarryOut", "what", "medicine")
          && Ext(events["Rimfolk_Evt_HelixBatch"], "HappeningExtension")!.Element("defaultOption")!.Value == "0", "the batch tended at the gate, or medicine for the road");
        C(Times(events["Rimfolk_Evt_LatticeRepair"], "DilemmaEffect_LatticeRepair") == 2 && Has(events["Rimfolk_Evt_LatticeRepair"], "DilemmaEffect_CarryOut", "what", "SubcoreBasic"),
            "the mechs mended for silver or a subcore");
        var lost = events["Rimfolk_Evt_LatticeLostMech"];
        C(Has(lost, "DilemmaEffect_LatticeReturn") && Has(lost, "DilemmaEffect_LatticeKeep") && Has(lost, "DilemmaEffect_LatticeMechHome")
          && Ext(lost, "HappeningExtension")!.Element("defaultOption")!.Value == "0", "the lost mech returned, kept at a cost, or let go; returned unless the colony says otherwise");
        C(Times(events["Rimfolk_Evt_LatticeParts"], "DilemmaEffect_LatticeParts") == 2 && Times(events["Rimfolk_Evt_LatticeTurret"], "DilemmaEffect_LatticeTurret") == 2,
            "the parts sold, the turret raised");
        C(Times(events["Rimfolk_Evt_ChoirListening"], "DilemmaEffect_ChoirListen") == 2 && Has(events["Rimfolk_Evt_ChoirListening"], "DilemmaEffect_ChoirListen", "together", "true"),
            "the Choir listens, someone of the colony sitting with them if it likes");
        C(Times(events["Rimfolk_Evt_ChoirArt"], "DilemmaEffect_ChoirArt") == 2 && Has(events["Rimfolk_Evt_ChoirArt"], "DilemmaEffect_Learn", "skill", "Artistic"), "art for the Spire's goods");
        var barter = events["Rimfolk_Evt_VaultBarter"];
        C(Has(barter, "DilemmaEffect_CarryOut", "what", "ComponentIndustrial") && Has(barter, "DilemmaEffect_CarryOut", "what", "rawfood") && Times(barter, "DilemmaEffect_VaultSupply") == 2
          && Has(barter, "DilemmaEffect_VaultSupply", "gift", "true") && Times(barter, "DilemmaEffect_PeoplesGCrate") == 1, "components and food for the crate, or given for nothing; supply either way");
        C(Times(events["Rimfolk_Evt_VaultCraft"], "DilemmaEffect_VaultTeach") == 2 && Has(events["Rimfolk_Evt_VaultCraft"], "DilemmaEffect_VaultTeach", "withB", "true")
          && Has(events["Rimfolk_Evt_VaultCraft"], "DilemmaEffect_Learn", "skill", "Crafting"), "a lost craft taught at the colony's bench");
        var child = events["Rimfolk_Evt_VaultChild"];
        C(Has(child, "DilemmaEffect_Join") && Times(child, "DilemmaEffect_VaultSky") == 2 && Ext(child, "HappeningExtension")!.Element("addsPeople")?.Value == "true",
            "the child who never saw a sky: taken in as anyone is, or a while under it");
        var missions = XDocument.Load(Path.Combine(root, "Defs", "MissionDefs", "Rimfolk_Missions_PeoplesG.xml")).Root!.Elements("IncidentDef").ToDictionary(Dn);
        string Only(string m) => Ext(missions[m], "MissionPeople")?.Element("only")?.Value;
        C(Only("Rimfolk_Mission_HelixGenepacks") == "editors" && Only("Rimfolk_Mission_LatticeHands") == "mechanitors" && Only("Rimfolk_Mission_ChoirStay") == "listeners"
          && Only("Rimfolk_Mission_VaultCells") == "vaultfolk", "an ask of each people, only theirs");
        C(missions["Rimfolk_Mission_HelixGenepacks"].Descendants("li").Any(li => li.Element("what")?.Value == "Genepack" && li.Element("instead")?.Value == "medicine"),
            "the Works ask for genepacks, medicine where there are none (or no Biotech)");
        C(Ext(missions["Rimfolk_Mission_LatticeHands"], "MissionHands")!.Element("skill")?.Value == "Crafting" && Ext(missions["Rimfolk_Mission_LatticeHands"], "MissionHands")!.Element("hands")?.Value == "2",
            "two good crafters for the Lattice's yards");
        C(missions["Rimfolk_Mission_ChoirStay"].Element("workerClass")!.Value == "ColonyCouncil.HappeningWorker_MissionChoirStay" && code.Contains("HappeningWorker_MissionHost")
          && code.Contains("Hosting.Beds(") && code.Contains("Hosting.Meal("), "the Choir's listener stays as the colony's guest, at its table and in a bed of its own");
        C(Ext(missions["Rimfolk_Mission_VaultCells"], "MissionDelivery")!.Element("kind")!.Value == "vaultcells" && code.Contains("public const string MissionKind = \"vaultcells\";")
          && Read("World", "Patch_FactionMemory.cs").Contains("m.kind == VaultSupply.MissionKind) VaultSupply.Noted(m.faction, m.reward, m.title)"),
            "parts delivered to the vault are supply too");
        C(missions.Values.All(m => F(m.Element("baseChance")!.Value) <= 0.4f && int.Parse(m.Element("minRefireDays")!.Value) >= 60), "a small share of the missions' one draw, not soon again");
        var thoughts = XDocument.Load(Path.Combine(root, "Defs", "ThoughtDefs", "Rimfolk_Thoughts_PeoplesG.xml")).Root!.Elements("ThoughtDef").ToDictionary(Dn);
        float Mood(string t) => F(thoughts[t].Descendants("baseMoodEffect").Single().Value);
        C(Mood("Rimfolk_Thought_ChoirCalm") > 0f && Mood("Rimfolk_Thought_ChoirCalm") <= 5f && Mood("Rimfolk_Thought_ChoirUnease") < 0f && Mood("Rimfolk_Thought_ChoirUnease") >= -5f
          && F(thoughts["Rimfolk_Thought_ChoirCalm"].Element("durationDays")!.Value) <= 2f && F(thoughts["Rimfolk_Thought_ChoirUnease"].Element("durationDays")!.Value) <= 2f,
            "the listening shown as moods: a small calm, a small unease, for a day or so");
        C(missions.Values.SelectMany(m => m.Descendants("thought")).All(t => thoughts.ContainsKey(t.Value)) && thoughts.ContainsKey("Rimfolk_Thought_ChoirFollowed"),
            "the colony's other feelings: the yards' day, the listener at someone's side");

        // ================= The patch on Rimfolk's own defs =================

        var patch = XDocument.Load(Path.Combine(root, "Patches", "Rimfolk_PeoplesG.xml")).Root!;
        var defNames = allDefs.Select(Dn).Where(x => x != "").ToHashSet();
        foreach (var op in patch.Elements("Operation"))
        {
            var m = Regex.Match(op.Element("xpath")?.Value ?? "", "defName=\"([^\"]+)\"");
            C(op.Attribute("Class")?.Value == "PatchOperationConditional" && m.Success && defNames.Contains(m.Groups[1].Value), $"the patch reaches {m.Groups[1].Value}, conditionally");
        }
        C(patch.Descendants("calling").Select(c => c.Value).OrderBy(x => x).SequenceEqual(Four.OrderBy(x => x)), "four world events prefer them, one each");
        var earlier = Directory.GetFiles(Path.Combine(root, "Patches"), "Rimfolk_Peoples*.xml").Where(p => !p.EndsWith("Rimfolk_PeoplesG.xml"))
            .SelectMany(p => XDocument.Load(p).Root!.Elements("Operation")).Select(op => Regex.Match(op.Element("xpath")?.Value ?? "", "defName=\"([^\"]+)\"").Groups[1].Value).ToHashSet();
        C(patch.Elements("Operation").All(op => !earlier.Contains(Regex.Match(op.Element("xpath")!.Value, "defName=\"([^\"]+)\"").Groups[1].Value)),
            "each a world event no earlier release's people prefers");

        // ================= Release B's peoples that arise =================

        C(ArisingMath.Callings(ArisingMath.Refugees).Contains("vaultfolk") && new[] { "listeners", "editors", "mechanitors" }.All(c => ArisingMath.Callings(ArisingMath.Exiles).Contains(c))
          && Four.All(c => !ArisingMath.Callings(ArisingMath.League).Contains(c) && !ArisingMath.Callings(ArisingMath.Splinter).Contains(c)),
            "survivors may come up from a shelter, a band of the banished listen, edit or link; a league or a splinter none");
        string Lean(int age, int psylink = 0, int medicine = 0, bool mechlink = false, bool vault = false, string kind = ArisingMath.Exiles) =>
            ArisingMath.Lean(kind, false, false, 0, 0, 0, false, false, age, 0, 0, 0, psylink, 0, 0, false, false, medicine, mechlink, vault);
        C(Lean(3, psylink: 4) == "listeners" && Lean(2, psylink: 4) == "seers" && Lean(3, psylink: 3) == "seers" && Lean(3, medicine: 12) == "editors" && Lean(2, medicine: 12) == null
          && Lean(2, mechlink: true) == "mechanitors" && Lean(1, mechlink: true) == null && Lean(3, medicine: 12, mechlink: true) == "editors",
            "a band led by a strong psycaster of the spacer age listens, by a surgeon of the spacer age edits, by a mechanitor links");
        C(ArisingMath.Lean(ArisingMath.Exiles, false, false, 12, 0, 0, false, false, 3, 0, 0, 0, 5, 0, 0, false, false, 12, true, false) == "scholars"
          && ArisingMath.Lean(ArisingMath.Exiles, false, false, 0, 0, 0, false, false, 3, 0, 0, 0, 0, 12, 0, false, false, 12, true, false) == "drillers",
            "the earlier leans first");
        C(Lean(2, vault: true, kind: ArisingMath.Refugees) == "vaultfolk" && Lean(1, vault: true, kind: ArisingMath.Refugees) == null
          && ArisingMath.Lean(ArisingMath.Refugees, false, false, 0, 0, 0, false, false, 3, 0, 0, 0, 0, 0, 0, true, false, 0, false, true) == "linesmen",
            "survivors of an industrial or spacer town on their fated quarter come up as vaultfolk; the Line's ways first");
        C(ArisingMath.VaultChance == 0.25f && new[] { ArisingMath.SaltRefugees, ArisingMath.SaltExiles, ArisingMath.SaltSplinter, ArisingMath.SaltRiver, ArisingMath.SaltBell,
                ArisingMath.SaltWheel, ArisingMath.SaltLine, ArisingMath.SaltCrew }.All(x => x != ArisingMath.SaltVault), "its own fated roll, apart from the others'");
        string arising = Read("Peoples", "Arising.cs");
        C(arising.Contains("ArisingMath.Fated(r.town, ArisingMath.SaltVault, ArisingMath.VaultChance)") && arising.Contains("Skill(r.pawn, SkillDefOf.Medicine), Mechlink(r.pawn)"),
            "the town's fated roll, the leader's own medicine and mechlink");
        var arise = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Peoples_Arising.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (var (cause, c) in new[] { ("refugees", "vaultfolk"), ("exiles", "listeners"), ("exiles", "editors"), ("exiles", "mechanitors") })
            C(arise.ContainsKey($"CC_Arise_{cause}_{c}") && arise.ContainsKey($"CC_Arise_{cause}_{c}_2") && new[] { "", "_2", "_3" }.All(x => arise.TryGetValue($"CC_Arise_Name_{cause}_{c}{x}", out var v) && v.Contains("{WHAT}")),
                $"{cause}_{c}: how a new people of these ways lives, told two ways, and three names for it");
        var voices = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeopleAges.xml")).Root!.Elements().Select(e => e.Name.LocalName).ToHashSet();
        C(Four.All(c => voices.Contains("CC_PeopleAge_Voice_" + c + "_Rise") && voices.Contains("CC_PeopleAge_Voice_" + c + "_Fall")), "each tells its rise and its fall in its own voice");

        // ================= No harm rolled; the draw only what was asked =================

        C(!Regex.IsMatch(code, @"Harm\.|TakeDamage|StartSocialFight"), "nothing of theirs rolls a harm: none of their touchpoints hurts anyone by chance");
        var hediffs = Regex.Matches(code, @"(AddHediff|AdjustSeverity)\(([^;]*)").Select(m => m.Groups[2].Value).ToList();
        C(hediffs.Count == 3 && hediffs.Any(h => h.Contains("HediffDefOf.BloodLoss, PeoplesGMath.DrawSeverity")) && hediffs.Any(h => h.StartsWith("HediffDefOf.XenogermReplicating"))
          && hediffs.Any(h => h.StartsWith("h)")), "the draw's little blood, the game's mark of a xenogerm, and the gene fault only on their own vat-grown");
        C(code.Contains("var h = HediffMaker.MakeHediff(fault, v);") && code.Contains("PeoplesD.KindNamed(ctx.faction, \"Rimfolk_HelixWorks_Vatborn\")"), "the fault is the batch's, never the colony's");

        // ================= Their words and their page =================

        var almanac = XDocument.Load(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_PeoplesG.xml")).Root!.Elements().Single();
        C(Dn(almanac) == "Council_Alm_C_PeoplesG" && almanac.Element("keywords")!.Elements("li").Count() >= 4 && almanac.Element("seeAlso")!.Elements("li").Any(l => l.Value == "Council_Alm_C_PeoplesF"),
            "an Almanac page of their own, beside release F's");
        var almText = string.Join("\n", almanac.Descendants().Where(e => !e.HasElements).Select(e => e.Value));
        C(!almText.Contains('—') && !almText.Contains('!') && !Regex.IsMatch(almText, @"\bin \d+ (days?|hours?)\b"), "its words plain, no countdowns");
        C(almText.Contains("a fifth over") && almText.Contains("fifteen of their goodwill") && almText.Contains("one in five") && almText.Contains("about every four years")
          && almText.Contains("medieval age") && almText.Contains("six in ten") && almText.Contains("blood draw"), "its rules are the code's");
        C(almText.Contains("Orla Venn") && almText.Contains("Tamsin Arro") && almText.Contains("Ilsabet Ruen") && almText.Contains("Hollis Crane"), "and its foundings the defs'");
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeoplesG.xml")).Root!.Elements().Select(e => e.Name.LocalName).ToHashSet();
        var used = Regex.Matches(code + Read("PlayOut", "PlayCarry.cs"), "\"(CC_PeoplesG_\\w+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        C(used.Count >= 40 && used.All(keyed.Contains), $"every word the code says is keyed ({used.Count}{(used.Any(u => !keyed.Contains(u)) ? ", missing " + string.Join(", ", used.Where(u => !keyed.Contains(u))) : "")})");
        var gTexts = new[] { Path.Combine("Defs", "IncidentDefs", "Rimfolk_Events_PeoplesG.xml"), Path.Combine("Defs", "FlavorPoolDefs", "Rimfolk_Lines_PeoplesG.xml"),
                Path.Combine("Languages", "English", "Keyed", "Rimfolk_PeoplesG.xml"), Path.Combine("Defs", "MissionDefs", "Rimfolk_Missions_PeoplesG.xml"),
                Path.Combine("Defs", "OutsiderTalkDefs", "Rimfolk_OutsiderTalks_PeoplesG.xml"), Path.Combine("Defs", "AlmanacArticleDefs", "Almanac_PeoplesG.xml") }
            .Select(x => File.ReadAllText(Path.Combine(root, x)).ToLowerInvariant()).Aggregate((x, y) => x + y);
        C(!Regex.IsMatch(gTexts, @"\b(dynasty|dynasties|bloodline|heir to the house)\b"), "the colony is the family: no houses, no bloodlines; the child joins as anyone does");

        // ================= The seams =================

        C(!Regex.IsMatch(code + Read("Peoples", "PeoplesGMath.cs"), @"Sched\.|Scribe_|ExposeData|\[HarmonyPatch"), "no clock, no game hook and nothing saved of their own");
        C(!Regex.IsMatch(code, @"LocomotionUrgency\.Walk|new LordToil_Travel\(|new LordJob_VisitColony\(") && Regex.Matches(code, @"new LordJob_VisitOnErrand\(").Count >= 1
          && code.Contains("Arrivals.Visit(") && code.Contains("new LordJob_ExitMapBest(Pace.Errand"), "they come, go about their work and leave at an errand's pace");
        C(code.Contains("MechRepairUtility.RepairTick(mech)") && code.Contains("PawnRelationDefOf.Overseer") && code.Contains("MechanitorUtility.CanControlMech(p, mech).Accepted"),
            "the game's own mech repair and overseer, and its own test of a mechanitor's bandwidth");
        C(code.Contains("OutsiderTend.Send(hc.a, patients)") && code.Contains("taker.genes.AddGene(gene, true)") && code.Contains("StatDefOf.PsychicSensitivity")
          && code.Contains("Find.Anomaly.Level > 0"), "the batch tended by the colony's doctor, the gene a xenogene, the listening by psychic sensitivity and the void's stirring");
        string carry = Read("PlayOut", "PlayCarry.cs");
        C(carry.Contains("public const string MechParts = \"mechparts\";") && carry.Contains("public const string Art = \"art\";") && carry.Contains("Missions.Matching(map, \"art\")"),
            "the subcores and chips, and the packed art, carried out as errands");
        var misc = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_PeoplesG_Misc.xml")).Root!.Elements().ToList();
        C(new[] { "Draw", "GeneWork", "Repair", "Relink", "RaiseTurret", "Listen", "Ordinary", "Teach", "Sky" }.All(j => misc.Any(e => Dn(e) == "Rimfolk_PeoplesG_" + j)
                                                                                                                          && code.Contains("\"Rimfolk_PeoplesG_" + j + "\"")),
            "their work on the map has jobs of its own");
        var fault = misc.Single(e => Dn(e) == "Rimfolk_GeneFault");
        C(fault.Element("tendable")?.Value == "true" && fault.Element("lethalSeverity") == null && F(fault.Element("maxSeverity")!.Value) < 1f, "the gene fault wants tending and never kills");
        C(new[] { "HelixWorks", "Lattice", "Choir", "SunkenVault" }.All(x => File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Peoples", x + ".png"))
                                                                             && File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Peoples", x + "Town.png"))),
            "an emblem and a town icon for each");
        C(Regex.Matches(code, @"Speech\.Say\(").Count >= 2 && code.Contains("PeoplesG.SayLine(") && !code.Contains("MoteMaker.ThrowText"), "they speak through Rimfolk's speech");
        C(Regex.Matches(code, @"\[DebugAction\(""Rimfolk"", ""Peoples: ").Count == 3, "three dev actions: the state, the Choir heard, the vault's fall");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
