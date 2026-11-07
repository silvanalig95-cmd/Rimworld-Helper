using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Rimfolk's own peoples, release C (Peoples/PeoplesMath.cs, ArisingMath.cs; docs/design/peoples.md, "Release C"): the
/// pilgrim fellowship, the learned society, the spacer enclave and the river folk. Their pure rules (the ages a template
/// arises in, the pilgrims' night, the enclave's lander, the river landing's docks), each people's own ways in its def
/// (its age, how few its towns, how many its caravans, its faith and gear soft on every mod, the span it arises in),
/// their towns' plans (the shrine, the archive, the pad, the landing's docks by the water), their callers at the gate
/// and their asks (each only theirs), the patch on Rimfolk's world events and visitors, release B's arising with them by
/// cause, age and place, their Almanac page, and the seams in the code. Release A's checks that every people meets (its
/// parent, its groups, its plan's soundness, its words) run over all seven in PeoplesTests.
/// </summary>
public static class PeoplesCTests
{
    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "peoples, release C: " + what);
        }

        // ================= The ages a template arises in =================

        PeoplesMath.AgeSpan(2, -1, -1, out int lo, out int hi);
        C(lo == 2 && hi == 2, "a template with no span arises in its own age");
        PeoplesMath.AgeSpan(1, 0, 2, out lo, out hi);
        C(lo == 0 && hi == 2, "a span from the tribes to the industrial towns");
        PeoplesMath.AgeSpan(1, 3, 1, out lo, out hi);
        C(lo == 1 && hi == 1, "an upside-down span is the template's own age");
        PeoplesMath.AgeSpan(1, 1, 9, out lo, out hi);
        C(lo == 1 && hi == 3, "a span never past the spacer age");
        C(PeoplesMath.AgeDistance(2, 0, 2) == 0 && PeoplesMath.AgeDistance(3, 0, 2) == 1 && PeoplesMath.AgeDistance(0, 1, 3) == 1 && PeoplesMath.AgeDistance(0, 3, 3) == 3,
            "how far an age lies outside a span");
        C(PeoplesMath.HolyYear(new[] { "trade", "faith" }) && !PeoplesMath.HolyYear(new[] { "sickness" }) && !PeoplesMath.HolyYear(null), "a holy year: a world event of the faith");

        // ================= The pilgrims' night =================

        C(!PeoplesMath.ShelterHour(15) && PeoplesMath.ShelterHour(16) && PeoplesMath.ShelterHour(22) && !PeoplesMath.ShelterHour(23) && !PeoplesMath.ShelterHour(3),
            "pilgrims ask for shelter at dusk, never in the morning");
        C(PeoplesMath.HoursToLight(18, 6) == 12 && PeoplesMath.HoursToLight(22, 6) == 8 && PeoplesMath.HoursToLight(2, 6) == 4, "the night they stay lasts until first light");
        C(PeoplesMath.BandMin == 3 && PeoplesMath.BandMax == 5, "a band of three to five");
        C(PeoplesMath.CoughChance > 0f && PeoplesMath.CoughChance <= 0.15f, "a pilgrim's cough is caught one night in ten at most, before the harm setting");

        // ================= The enclave's lander =================

        C(PeoplesMath.LanderGuards(0.6f) == 2 && PeoplesMath.LanderGuards(2f) == 4 && PeoplesMath.LanderGuards(1.3f) == 3 && PeoplesMath.LanderGuards(float.NaN) >= 2
          && PeoplesMath.LanderGuards(9f) == 4, "two to four marshals, by the colony's wealth");
        C(!PeoplesMath.LanderAge(0) && !PeoplesMath.LanderAge(1) && PeoplesMath.LanderAge(2) && PeoplesMath.LanderAge(3), "the lander comes to an industrial or spacer colony");

        // ================= The river landing's docks =================

        C(PeoplesMath.ByWater(10, 40) && !PeoplesMath.ByWater(9, 40) && !PeoplesMath.ByWater(0, 0) && !PeoplesMath.ByWater(0, 4), "a landing is by the water with a quarter of its docks in it");
        C(PeoplesMath.DockSpot(12, 0, 100) == 12 && PeoplesMath.DockSpot(12, 7, 100) == -1 && PeoplesMath.DockSpot(0, 3, 100) == 0,
            "a spot is worth its docks in the water, while the rest of the plan stands on good ground");

        // ================= Their defs =================

        string peopleDir = Path.Combine(root, "Defs", "PeopleDefs");
        var all = Directory.GetFiles(peopleDir, "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements()).ToList();
        string Dn(XElement e) => e.Element("defName")?.Value ?? "";
        XElement Ext(XElement d, string cls) => d.Element("modExtensions")?.Elements("li").FirstOrDefault(li => li.Attribute("Class")?.Value == "ColonyCouncil." + cls);
        var factions = all.Where(e => e.Name.LocalName == "FactionDef").ToDictionary(f => Ext(f, "PeopleExtension")!.Element("calling")!.Value);
        float F(string v) => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
        string Tech(string calling) => factions[calling].Element("techLevel")!.Value;
        C(Tech("pilgrims") == "Medieval" && Tech("scholars") == "Industrial" && Tech("brokers") == "Spacer" && Tech("fishers") == "Medieval",
            "the fellowship and the river folk medieval, the society industrial, the enclave spacer");
        C(Dn(factions["pilgrims"]) == "Rimfolk_Pilgrims" && Dn(factions["scholars"]) == "Rimfolk_Scholars" && Dn(factions["brokers"]) == "Rimfolk_Enclave"
          && Dn(factions["fishers"]) == "Rimfolk_RiverFolk", "their defs");
        float Weight(string c) => F(Ext(factions[c], "PeopleExtension")!.Element("caravanWeight")!.Value);
        float Towns(string c) => F(factions[c].Element("settlementGenerationWeight")!.Value);
        C(Weight("pilgrims") < 1f && Weight("brokers") < 1f && Weight("fishers") > 1f && Weight("scholars") == 1f,
            "few caravans of pilgrims and of the enclave (its trade comes down by lander), a few more of the river folk's");
        C(Towns("brokers") < Towns("scholars") && Towns("scholars") < Towns("pilgrims") && Towns("pilgrims") < Towns("fishers") && Towns("fishers") < 1f,
            "the enclave's markets fewest, the river folk's landings most");
        string Raid(string c) => factions[c].Element("raidCommonalityFromPointsCurve")!.Descendants("li").Single().Value;
        C(new[] { "pilgrims", "scholars", "brokers", "fishers" }.All(c => F(Raid(c).Trim('(', ')').Split(',')[1]) <= 0.3f), "they raid rarely, and only when wronged");
        string Arise(string c) => Ext(factions[c], "PeopleExtension")!.Element("ariseAges")?.Value;
        C(Arise("pilgrims") == "1~3" && Arise("scholars") == "1~3" && Arise("fishers") == "0~2" && Arise("brokers") == null && Arise("traders") == null,
            "the ages they arise in: pilgrims and scholars from the medieval towns on, river folk to the industrial, the enclave the spacer age only");
        var river = Ext(factions["fishers"], "PeopleExtension")!;
        C(river.Element("byWater")?.Value == "true" && river.Element("fish")?.Value == "40~90" && factions.Values.Count(f => Ext(f, "PeopleExtension")!.Element("byWater") != null) == 1,
            "only the river folk's towns stand by the water, with fish in their racks");
        // Soft on every mod: their memes and kinds of people, their gear of the DLCs.
        foreach (var c in new[] { "pilgrims", "scholars", "brokers", "fishers" })
        {
            var f = factions[c];
            C(f.Element("forcedMemes")!.Elements("li").All(li => li.Attribute("MayRequire") != null)
              && f.Element("forcedMemes")!.Elements("li").Any(li => li.Attribute("MayRequire")!.Value == "Ludeon.RimWorld.Ideology"), $"{c}: memes of the game's own, a mod's only when loaded");
            C(Ext(f, "PeopleExtension")!.Element("plan")!.Value.StartsWith("Rimfolk_Plan_"), $"{c}: a plan of its own");
        }
        var kinds = all.Where(e => e.Name.LocalName == "PawnKindDef").ToList();
        var dlcGear = new[] { "Apparel_Shadecone", "Apparel_Flophat", "Apparel_Beret", "Apparel_ShirtRuffle" };
        C(kinds.SelectMany(k => k.Element("apparelRequired")?.Elements("li") ?? Enumerable.Empty<XElement>()).Where(li => dlcGear.Contains(li.Value)).All(li => li.Attribute("MayRequire") != null),
            "the DLCs' hats and shirts only with their DLC");
        C(factions["fishers"].Descendants("li").Where(li => li.Element("categoryDef")?.Value == "Fish").All(li => li.Attribute("MayRequire")?.Value == "Ludeon.RimWorld.Odyssey")
          && factions["fishers"].Document!.Descendants("categoryDef").Count(c => c.Value == "Fish") == 3, "the river folk's fish in their stock only with Odyssey");
        C(all.Where(e => e.Name.LocalName == "TraderKindDef" && Dn(e).Contains("Scholars")).All(t => t.Descendants("thingDef").Any(d => d.Value == "TextBook")),
            "the society's traders carry books");
        C(all.Where(e => e.Name.LocalName == "TraderKindDef" && Dn(e).Contains("Enclave_")).All(t => t.Descendants("thingDef").Any(d => d.Value == "ComponentSpacer" || d.Value == "MealSurvivalPack")),
            "the enclave's traders carry goods from above");

        // ================= Their towns' plans =================

        var plans = XDocument.Load(Path.Combine(peopleDir, "Rimfolk_TownPlans_PeoplesC.xml")).Root!.Elements("ColonyCouncil.TownPlanDef").ToDictionary(Dn);
        List<string> Rows(string plan) => plans[plan].Element("rows")!.Elements("li").Select(li => li.Value).ToList();
        string KeyOf(string plan, Func<XElement, bool> pick) => plans[plan].Element("keys")!.Elements("li").Where(pick).Select(k => k.Element("key")!.Value).FirstOrDefault() ?? "\0";
        int Count(string plan, string key) => Rows(plan).Sum(r => r.Count(ch => key.Contains(ch)));
        C(plans.Keys.SequenceEqual(new[] { "Rimfolk_Plan_ShrineHostel", "Rimfolk_Plan_Archive", "Rimfolk_Plan_LandingMarket", "Rimfolk_Plan_RiverLanding" })
          && factions.Values.Select(f => Ext(f, "PeopleExtension")!.Element("plan")!.Value).Count(plans.ContainsKey) == 4, "a plan for each of release C's peoples");
        C(Count("Rimfolk_Plan_ShrineHostel", KeyOf("Rimfolk_Plan_ShrineHostel", k => k.Element("thing")?.Value == "Bedroll")) == 12
          && Count("Rimfolk_Plan_ShrineHostel", KeyOf("Rimfolk_Plan_ShrineHostel", k => k.Element("thing")?.Value == "Campfire")) == 1
          && plans["Rimfolk_Plan_ShrineHostel"].Descendants("mark").Any(m => m.Value == "plant:Plant_Rose"), "the shrine's court: its flame and flower beds; the hostel: twelve bedrolls");
        C(Count("Rimfolk_Plan_Archive", KeyOf("Rimfolk_Plan_Archive", k => k.Element("thing")?.Value == "Bookcase") + KeyOf("Rimfolk_Plan_Archive", k => k.Element("thing")?.Value == "BookcaseSmall")) >= 20
          && Count("Rimfolk_Plan_Archive", KeyOf("Rimfolk_Plan_Archive", k => k.Element("thing")?.Value == "SimpleResearchBench")) == 1, "the archive: its bookcases and its research bench");
        C(Count("Rimfolk_Plan_LandingMarket", KeyOf("Rimfolk_Plan_LandingMarket", k => k.Element("floor")?.Value == "Concrete" && k.Element("thing") == null)) >= 100
          && Count("Rimfolk_Plan_LandingMarket", KeyOf("Rimfolk_Plan_LandingMarket", k => k.Element("thing")?.Value == "OrbitalTradeBeacon")) == 1, "the market: its landing pad and its trade beacon");
        var landing = Rows("Rimfolk_Plan_RiverLanding");
        char dock = KeyOf("Rimfolk_Plan_RiverLanding", k => k.Element("mark")?.Value == "dock")[0];
        var dockKey = plans["Rimfolk_Plan_RiverLanding"].Element("keys")!.Elements("li").Single(k => k.Element("mark")?.Value == "dock");
        C(dockKey.Element("thing") == null && dockKey.Element("floor") == null && dockKey.Element("roof") == null, "a dock is laid by its own code: a jetty over water, planks on dry ground");
        C(landing.Last().All(ch => ch == dock) && landing.All(r => r[r.Length - 1] == dock), "the landing's docks along its south and east sides, to meet a river either way");
        C(Count("Rimfolk_Plan_RiverLanding", dock.ToString()) >= 120, "docks enough to reach the water");

        // ================= Their callers and asks =================

        var events = XDocument.Load(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeoplesC.xml")).Root!.Elements("IncidentDef").ToDictionary(Dn);
        C(events.Keys.SequenceEqual(new[] { "Rimfolk_Evt_PilgrimShelter", "Rimfolk_Evt_ScholarCopyist", "Rimfolk_Evt_RiverCatch", "Rimfolk_Evt_EnclaveLander" }), "four callers at the gate");
        var shelter = events["Rimfolk_Evt_PilgrimShelter"];
        var shelterExt = Ext(shelter, "HappeningExtension")!;
        C(shelter.Element("workerClass")!.Value == "ColonyCouncil.HappeningWorker_PilgrimShelter" && shelterExt.Element("count")!.Value == "3~5"
          && shelterExt.Element("options")!.Elements("li").Count() == 3, "a band of three to five asks a night's shelter: in, outside the gate, or not at all");
        C(shelterExt.Element("lines")!.Elements("li").Any(l => l.Value.StartsWith("[bless]")) && shelterExt.Element("lines")!.Elements("li").Any(l => l.Value.StartsWith("[cough]"))
          && Ext(shelter, "HappeningLivingText")!.Descendants("li").Any(l => l.Value.StartsWith("[in,cough]")) && Ext(shelter, "HappeningLivingText")!.Descendants("li").Any(l => l.Value.StartsWith("[in,!cough]")),
            "the morning: a blessing said at the gate, and the cough said and told when it was caught");
        var copyist = events["Rimfolk_Evt_ScholarCopyist"];
        C(Ext(copyist, "HappeningSetting")!.Element("people")!.Value == "calling:scholars" && Ext(copyist, "HappeningSetting")!.Element("visitAt")!.Value == "gathering"
          && copyist.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_Goods") && copyist.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_Lesson"),
            "the society's copyist works at the colony's table, and leaves a book or teaches");
        C(Ext(copyist, "HappeningExtension")!.Element("texts")!.Elements("li").Any(t => t.Value.StartsWith("[tribal]")), "a tribal colony's stories are written down instead");
        var catchDef = events["Rimfolk_Evt_RiverCatch"];
        C(Ext(catchDef, "HappeningSetting")!.Element("people")!.Value == "calling:fishers" && catchDef.Descendants("li").Count(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_Goods") == 2,
            "a boat-trader of the river folk brings the catch to the gate");
        var lander = events["Rimfolk_Evt_EnclaveLander"];
        C(lander.Element("workerClass")!.Value == "ColonyCouncil.HappeningWorker_EnclaveLander" && lander.Descendants("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_EnclaveLanding")
          && Ext(lander, "HappeningExtension")!.Element("texts")!.Elements("li").All(t => !t.Value.StartsWith("[lowtech")), "the enclave's lander, bidden, comes down by drop pod");
        var missions = XDocument.Load(Path.Combine(root, "Defs", "MissionDefs", "Rimfolk_Missions_PeoplesC.xml")).Root!.Elements("IncidentDef").ToDictionary(Dn);
        C(Ext(missions["Rimfolk_Mission_HostelBread"], "MissionPeople")!.Element("only")!.Value == "pilgrims"
          && Ext(missions["Rimfolk_Mission_HostelBread"], "MissionPeople")!.Element("whileKind")!.Value == "faith", "the fellowship's hostel asks for food only in a holy year");
        C(Ext(missions["Rimfolk_Mission_RiverBoats"], "MissionPeople")!.Element("only")!.Value == "fishers"
          && missions["Rimfolk_Mission_RiverBoats"].Descendants("what").Select(w => w.Value).SequenceEqual(new[] { "WoodLog", "textiles" }), "the river folk ask for timber in the spring and cloth in the summer");
        C(missions.Values.All(m => F(m.Element("baseChance")!.Value) <= 0.4f && int.Parse(m.Element("minRefireDays")!.Value) >= 60), "a small share of the missions' one draw, not soon again");
        var thoughts = XDocument.Load(Path.Combine(root, "Defs", "ThoughtDefs", "Rimfolk_Thoughts_PeoplesC.xml")).Root!.Elements("ThoughtDef").Select(Dn).ToList();
        C(thoughts.Contains("Rimfolk_Evt_ShelteredPilgrims") && thoughts.Contains("Rimfolk_Evt_RecordsKept"), "the colony is glad of the pilgrims' night and of its records kept");

        // ================= The patch on Rimfolk's own defs =================

        var patch = XDocument.Load(Path.Combine(root, "Patches", "Rimfolk_PeoplesC.xml")).Root!;
        var allDefs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).SelectMany(f => XDocument.Load(f).Root!.Elements()).Select(Dn).Where(x => x != "").ToHashSet();
        foreach (var op in patch.Elements("Operation"))
        {
            var m = Regex.Match(op.Element("xpath")?.Value ?? "", "defName=\"([^\"]+)\"");
            C(op.Attribute("Class")?.Value == "PatchOperationConditional" && m.Success && allDefs.Contains(m.Groups[1].Value), $"the patch reaches {m.Groups[1].Value}, conditionally");
        }
        var called = patch.Descendants("calling").Select(c => c.Value).ToList();
        var preferred = patch.Descendants("prefers").Select(c => c.Value).ToList();
        C(called.Count == 9 && called.Count(c => c == "pilgrims") == 3 && called.Count(c => c == "scholars") == 2 && called.Count(c => c == "brokers") == 2 && called.Count(c => c == "fishers") == 2,
            "nine world events prefer them: three the pilgrims', two each the others'");
        C(preferred.Count == 5 && preferred.Count(c => c == "pilgrims") == 3 && preferred.Count(c => c == "scholars") == 2, "five visitors prefer them: pilgrims of three kinds, scholars of two");

        // ================= Release B's peoples that arise =================

        var callings = new[] { "traders", "healers", "herders", "pilgrims", "scholars", "brokers", "fishers" };
        var from = new[] { 2, 1, 0, 1, 1, 3, 0 };
        var to = new[] { 2, 1, 0, 3, 3, 3, 2 };
        string Of(string kind, int age, string lean = null)
        {
            int i = ArisingMath.Template(kind, age, callings, from, to, lean);
            return i < 0 ? null : callings[i];
        }
        C(Enumerable.Range(0, 4).Select(a => Of("refugees", a)).SequenceEqual(new[] { "herders", "healers", "traders", "traders" }), "survivors as before, by their age");
        C(Enumerable.Range(0, 4).All(a => Of("refugees", a, "fishers") == "fishers"), "survivors by a river take to it as river folk, in any age");
        C(Enumerable.Range(0, 4).Select(a => Of("league", a)).SequenceEqual(new[] { "herders", "traders", "traders", "brokers" }), "a league of spacer-age towns is an enclave");
        C(Of("exiles", 0, "scholars") == "herders" && Enumerable.Range(1, 3).All(a => Of("exiles", a, "scholars") == "scholars"), "a band led by a learned one keeps a school, but among the tribes");
        C(Enumerable.Range(0, 4).All(a => Of("exiles", a, "fishers") == "fishers") && Of("exiles", 2) == "traders", "a band by a river takes to the boats; else as before");
        C(Enumerable.Range(0, 4).Select(a => Of("splinter", a)).SequenceEqual(new[] { "herders", "healers", "pilgrims", "pilgrims" }), "faithful who broke away walk the roads as pilgrims from the industrial age");
        C(Of("league", 3, "fishers") == "brokers" && Of("splinter", 1, "scholars") == "healers", "a cause never leans where it may not");
        C(ArisingMath.Lean("exiles", false, false, 12) == "scholars" && ArisingMath.Lean("exiles", false, false, 9) == null && ArisingMath.Lean("exiles", true, true, 0) == "fishers"
          && ArisingMath.Lean("refugees", true, true, 0) == "fishers" && ArisingMath.Lean("refugees", true, false, 0) == null && ArisingMath.Lean("league", true, true, 20) == null,
            "what a cause leans to: a learned leader to scholars, a river (its fated half) to the boats");
        C(ArisingMath.Wanted("refugees", "fishers").First() == "fishers" && ArisingMath.Wanted("refugees", null).SequenceEqual(new[] { "herders", "healers", "traders" })
          && ArisingMath.Callings("exiles").SequenceEqual(new[] { "herders", "traders", "scholars", "fishers", "tamers", "hunters" }), "a lean comes first, and only a cause's own leans");
        float share = Enumerable.Range(0, 4000).Count(i => ArisingMath.Fated(i * 7919L, ArisingMath.SaltRiver, ArisingMath.RiverChance)) / 4000f;
        C(Math.Abs(share - 0.5f) < 0.04f, $"half the causes near a river lean to it ({share:0.000})");
        // The pace and the caps are release B's, unchanged.
        C(ArisingMath.GapDays == 40f && ArisingMath.KindGapDays == 90f && ArisingMath.MaxStanding == 6 && ArisingMath.Kinds.Length == 4, "release B's pace and caps unchanged");

        // ================= Their words and their page =================

        var almanac = XDocument.Load(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_PeoplesC.xml")).Root!.Elements().Single();
        C(Dn(almanac) == "Council_Alm_C_PeoplesC" && almanac.Element("keywords")!.Elements("li").Count() >= 4 && almanac.Element("seeAlso")!.Elements("li").Any(l => l.Value == "Council_Alm_C_Peoples"),
            "an Almanac page of their own, beside release A's");
        var almText = string.Join("\n", almanac.Descendants().Where(e => !e.HasElements).Select(e => e.Value));
        C(!almText.Contains('—') && !almText.Contains('!') && !Regex.IsMatch(almText, @"\bin \d+ (days?|hours?)\b"), "its words plain, no countdowns");
        C(almText.Contains("one night in ten") && almText.Contains("drop pod") && almText.Contains("Odyssey") && almText.Contains("river"), "its rules are the code's");
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PeoplesC.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        C(new[] { "CC_PeoplesC_CoughWhy", "CC_PeoplesC_LanderGone", "CC_PeoplesC_Impact_Lander", "CC_PeoplesC_Impact_LanderBrief" }.All(keyed.ContainsKey), "the code's words");
        var arise = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Peoples_Arising.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        C(arise["CC_Arise_refugees_fishers"].Contains("landing") && arise["CC_Arise_exiles_scholars"].Contains("archive") && arise["CC_Arise_splinter_pilgrims"].Contains("hostel")
          && arise["CC_Arise_league_brokers"].Contains("landing pad") && arise["CC_Arise_league_brokers"].StartsWith("{MEANS}"), "how a new people of theirs lives is what its town is on the map");

        // ================= The seams =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(params string[] parts) => File.ReadAllText(Path.Combine(new[] { src }.Concat(parts).ToArray()));
        string callers = Read("Peoples", "PeoplesCallers.cs"), towns = Read("Peoples", "PeopleTowns.cs"), arising = Read("Peoples", "Arising.cs");
        C(callers.Contains("VisitorServices.Lodge(ctx.map, p, false)") && callers.Contains("LivingEvents.Schedule(LivingEvents.Shelter")
          && Read("Happenings", "Workers_Living.cs").Contains("case Shelter: HappeningWorker_PilgrimShelter.Morning(m); break;"), "each pilgrim sleeps in a warm corner indoors; the morning comes as a memo");
        C(callers.Contains("Harm.Roll(host, HarmKinds.PilgrimCough, PeoplesMath.CoughChance, HurtClass.Mild)") && callers.Contains("Harm.Ail(host, cold"), "the cough through the harm setting's cap and scale");
        C(callers.Contains("DropPodUtility.DropThingsNear(spot, map") && callers.Contains("new LordJob_VisitColony(f, chill,") && callers.Contains("PeoplesMath.LanderAge((int)Ages.Ours)"),
            "the lander by drop pod, trading from the factor's pack under the game's own visitors' lord, for a later age");
        C(towns.Contains("LayDocks(map, Cells(map, grid, marks, \"dock\"))") && towns.Contains("TerrainDefOf.Bridge") && towns.Contains("PeoplesMath.DockSpot(water, bad, used.Count)"),
            "a landing where its docks reach the water, a jetty laid over it");
        C(towns.Contains("public static void ToTheRivers()") && Read("Peoples", "ArisingBook.cs").Contains("PeopleTowns.ToTheRivers();"), "a new world's river folk move to a river as the game starts");
        C(arising.Contains("TryTownTile(c.near, c.minTiles, c.maxTiles, c.Kin(), Score(c), out tile, river)") && arising.Contains("c.lean = null;")
          && arising.Contains("river && !Peoples.HasRiver(t)"), "a new people of the river folk on a river; with none in reach, the cause's own ways instead");
        C(Read("Happenings", "Workers_Visitors.cs").Split(new[] { "Peoples.Weigh(ext.prefers)" }, StringSplitOptions.None).Length == 3
          && Read("Happenings", "Workers_Visitors2.cs").Split(new[] { "Peoples.Weigh(ext.prefers" }, StringSplitOptions.None).Length == 4, "five visitor events weigh the calling they prefer");
        C(!Regex.IsMatch(callers + towns, @"Sched\.|Scribe_|ExposeData|\[HarmonyPatch"), "no clock, nothing saved, no Harmony patch of their own");
        // Fixed on the way: a caller of a calling (the order's hospitaller, the league's outrider, the copyist, the boat-trader)
        // was half the time a Hospitality guest of any people already here, under that people's name.
        C(Read("Happenings", "Workers_Setting.cs").Contains("!s.waits && !ofCalling"), "a caller of a calling is never a guest of another people standing in");
        C(callers.Contains("GatheringSpot(map) != null"), "the copyist comes only to a colony with a place it gathers");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
