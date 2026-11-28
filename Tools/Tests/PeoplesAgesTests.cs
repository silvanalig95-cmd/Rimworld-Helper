using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Content round 7, the peoples' older events in their own age (docs/content-round-7-peoples-ages.md): the 96 events of rounds
/// 1 to 3 and releases C to G that were told in the colony's age. Each has its people's forms (texts tagged [age0] to [age3]
/// that its worker can pick by the people's age, or its goods by their age in code), or is checked and left with its reason;
/// no text told only of the tribal or medieval age names a later age's things (the review's word list and the game's own high
/// tech words); what they bring, ask or pay follows PplAgesMath; the colony's own means stay the colony's.
/// </summary>
public static class PeoplesAgesTests
{
    /// <summary>The 96 events the review of the peoples' events found told only in the colony's age.</summary>
    public static readonly string[] NinetySix =
    {
        "HerdersBeast", "OrderLimb", "StormSense", "LatticeMechlink", "MossSeed", "LeagueTrust", "ScholarsReading", "LanternPost", "HuntOrTame",
        "Pilgrimage", "LeagueGuard", "EnclaveBody", "EnclaveInstalment", "RiverCrossing", "KnotNight", "GuildMark", "HerdersDrought", "EnclavePad",
        "ArchiveTrust", "LameBeast",
        "RedInk", "SickbedLitters", "SickRoom", "FieldSickhouse", "DroveCrossing", "ThiefTrail", "HardBirth", "OldShrine", "TwoFaiths",
        "FellowExamined", "SummerSchool", "EnclaveContract", "ContractDue", "FouledWater", "KnotLanterns", "HiveBurned",
        "OrderHealer", "LeagueOutrider",
        "PilgrimShelter", "ScholarCopyist", "RiverCatch", "EnclaveLander",
        "WardensHunt", "PeltBuyer", "WardensLantern", "BellForTheDead", "BellFuneral", "BellVoidSigns", "TamerBout", "EggTrade", "TamerStray",
        "WheelMending", "WheelScrap", "WheelWreck",
        "GlassReading", "GlassRod", "GlassLesson", "MossSaplings", "MossGraveTree", "MossTreeTend", "BannerHire", "BannerDeserter", "BannerRoll",
        "GuildCommission", "GuildCollects", "GuildBid",
        "LineMast", "LineWire", "LineStorm", "UnionDrill", "UnionGoods", "UnionStrike", "RepublicCensus", "RepublicPapers", "RepublicConscript",
        "FleetShuttle", "FleetSalvage", "FleetSleeper",
        "HelixSample", "HelixGeneWork", "HelixCase", "HelixBatch", "LatticeRepair", "LatticeLostMech", "LatticeParts", "LatticeTurret",
        "ChoirListening", "ChoirArt", "VaultBarter", "VaultCraft", "VaultChild",
        "OrderNurse", "LongWalk", "Disputation", "EnclaveSalvage", "RiverNets",
    };

    /// <summary>Things of the industrial and spacer ages: the review's search (radio, wire, truck, pod, lander, shuttle, console) and the game's high tech words (Flavor.HighTech).</summary>
    private static readonly Regex Late = new Regex(
        @"\b(radios?|headsets?|beacons?|operators?|wires?|trucks?|pods?|landers?|shuttles?|consoles?|guns?|gunfire|rifles?|pistols?|shotguns?|revolvers?|turrets?|grenades?|bullets?|ammo|electric\w*|batter(y|ies)|computers?|comms|radios?|cryptosleep|spaceships?|starships?|lasers?|plasma|freezers?|solar|components?|glitterworlds?|motor\w*|barometers?|cameras?|nylon|plastic|typed|printed|insulated|recorders?|firebombs?|incendiary|thermos|welding|grinders?|data slates?)\b",
        RegexOptions.IgnoreCase);

    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the peoples' events in their own age: " + what);
        }

        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        if (root == null)
        {
            C(false, "found the mod's folder");
            return n;
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(params string[] rel) => File.ReadAllText(Path.Combine(new[] { src }.Concat(rel).ToArray()));
        string Between(string text, string from, string to)
        {
            int a = text.IndexOf(from, StringComparison.Ordinal);
            if (a < 0) return "";
            int b = text.IndexOf(to, a + from.Length, StringComparison.Ordinal);
            return b < 0 ? text.Substring(a) : text.Substring(a, b - a);
        }
        string Dn(XElement e) => (string)e.Element("defName")!;
        XElement? Ext(XElement e, string cls = "HappeningExtension") =>
            e.Element("modExtensions")?.Elements("li").FirstOrDefault(li => (string?)li.Attribute("Class") == "ColonyCouncil." + cls);

        // ================= The rules (PplAgesMath) =================

        C(PplAgesMath.GoodsAge(3, 0) == 0 && PplAgesMath.GoodsAge(0, 3) == 0 && PplAgesMath.GoodsAge(2, 3) == 2 && PplAgesMath.GoodsAge(9, -4) == 0,
            "a piece they hand over is never past their own age, nor past the colony's");
        C(PplAgesMath.Schematic(2, false) && PplAgesMath.Schematic(3, false) && !PplAgesMath.Schematic(1, false) && !PplAgesMath.Schematic(3, true),
            "a schematic only from a society of the industrial age or later, to a colony past the medieval age");
        C(PplAgesMath.Tower(0) && PplAgesMath.Tower(1) && !PplAgesMath.Tower(2) && !PplAgesMath.Tower(3) && PplAgesMath.Flags(1) == PplAgesMath.Tower(1),
            "a Line of the old ages keeps flag towers and buys cloth for their flags");
        C(PplAgesMath.Tower(1) == (1 < 2) && Code("Peoples", "Events7", "LineCrew.cs").Contains("public static bool Tower(Faction line) => PeopleEvents.AgeOf(line) < 2;"),
            "the same rule as the round-7 line crew's tower");
        C(PplAgesMath.ClothMin * 1.5f >= PeoplesFMath.WireMin * 32f * 0.9f && PplAgesMath.ClothMax * 1.5f <= PeoplesFMath.WireMax * 32f * 1.1f,
            "the cloth's range is about the worth of the components' (cloth 1.5, a component 32)");
        C(!PplAgesMath.Machined(0) && !PplAgesMath.Machined(1) && PplAgesMath.Machined(2) && PplAgesMath.Machined(3), "components and shells only from a union of the industrial age or later");
        C(PplAgesMath.SteelFor(4, 32f, 1.9f) == 70 && PplAgesMath.SteelFor(0, 32f, 1.9f) == 0 && PplAgesMath.SteelFor(4, float.NaN, 1.9f) == 0 && PplAgesMath.SteelFor(4, 32f, 0f) == 0,
            "an old union's crate: the components' worth in steel, to the nearest ten");
        C(PplAgesMath.CaseTier(3) == 3 && PplAgesMath.CaseTier(2) == 2 && PplAgesMath.CaseTier(1) == 1 && PplAgesMath.CaseTier(0) == 1, "the Works' case by their age, never below the medieval");
        C(PplAgesMath.HerbalFor(6, 18f, 10f) == 11 && PplAgesMath.HerbalFor(0, 18f, 10f) == 0 && PplAgesMath.HerbalFor(1, 1f, 100f) == 1, "herbal medicine of the same worth");
        C(!PplAgesMath.Turret(1) && PplAgesMath.Turret(2) && PplAgesMath.Turret(3), "a turret only from a Lattice of the industrial age or later");
        C(PplAgesMath.CanWork(0, 0) && PplAgesMath.CanWork(1, 2) && !PplAgesMath.CanWork(2, 1) && !PplAgesMath.CanWork(3, 0), "a guild master works a piece of the guild's own age or before");
        C(PeopleAgesMath.AgeOfTech(2) == 0 && PeopleAgesMath.AgeOfTech(3) == 1 && PeopleAgesMath.AgeOfTech(4) == 2 && PeopleAgesMath.AgeOfTech(5) == 3, "a piece's age from its tech level");

        // ================= The code that uses them =================

        var peoplesF = Code("Peoples", "PeoplesF.cs");
        var wire = Between(peoplesF, "public class HappeningWorker_LineWire", "\n    }\n");
        C(wire.Contains("PplAgesMath.Flags(PplAges.Of(line)) ? ThingDefOf.Cloth : ThingDefOf.ComponentIndustrial") && wire.Contains("ctx.thing = want;")
          && wire.Contains("PeoplesFMath.WirePrice(ctx.variant * want.BaseMarketValue)") && wire.Contains("\"flags\""),
            "the wire-trader buys what the Line's age needs, at the same rate, and says so");
        var mast = Between(peoplesF, "public static class RelayMast", "public class HappeningWorker_LineMast");
        C(mast.Contains("new[] { PeoplesF.MastDef, PplAges.TowerDef }") && mast.Contains("PplAges.MastFor("), "the colony's mast is a relay mast or a flag tower, raised by the Line's age");
        C(Between(peoplesF, "public class HappeningWorker_LineStorm", "\n    }\n").Contains("tags.Add(\"tower\")"), "a lineman in the storm climbs the colony's flag tower as a tower");
        C(peoplesF.Contains("public static List<(ThingDef def, int count)> Crate(Map map, Faction union = null)") && peoplesF.Contains("Crate(ctx.map, ctx.faction)")
          && peoplesF.Contains("PplAgesMath.Machined(PplAges.Of(union))"), "the union's crate by the union's age");
        var peoplesG = Code("Peoples", "PeoplesG.cs");
        C(peoplesG.Contains("Case(Map map, Faction works = null)") && peoplesG.Contains("Case(ctx.map, ctx.faction)") && peoplesG.Contains("ThingDefOf.MedicineHerbal"),
            "the Works' case by the Works' age");
        C(Between(peoplesG, "public class HappeningWorker_LatticeTurret", "\n    }\n").Contains("PplAgesMath.Turret(PplAges.Of(f))"), "the Lattice's turret by the Lattice's age");
        var mark = Code("Peoples", "Events2", "GuildMark.cs");
        C(mark.Contains("Takers(Map map, Faction guild = null)") && mark.Contains("PplAgesMath.CanWork(") && mark.Contains("GuildMark.Takers(map, guild)")
          && mark.Contains("GuildMark.Takers(ctx.map, ctx.faction)"), "the guild's master asks only for a weapon of the guild's age");
        C(Code("Peoples", "PeoplesD2.cs").Contains("ctx.thing = Refurbished(ctx.count, ctx.faction);"), "the Wheel's mended weapon within the Wheel's age");
        C(Code("Peoples", "PeoplesCallers.cs").Contains("ctx.thing = Book(ctx.faction);"), "the copyist's book by the society's age");
        var lore = Code("Peoples", "PeoplesLore.cs");
        C(lore.Contains("PplAgesMath.GoodsAge((int)Ages.Ours, PplAges.Of(f))") && lore.Contains("Make(ctx.count, ctx.faction)"), "the enclave's salvage within the enclave's age");
        var ages = Code("Peoples", "PplAges.cs");
        C(ages.Contains("public class HappeningWorker_PplAgesSetting : HappeningWorker_Setting") && ages.Contains("PeopleEvents.AgeTags(tags, ctx.faction);"),
            "the callers told from data have the people's age in their tags");

        // The flag tower: the round-7 tower to look at, the relay mast's working.
        var misc = XDocument.Load(Path.Combine(root, "Defs", "Misc", "Rimfolk_PeoplesF_Misc.xml")).Root!;
        var tower = misc.Elements("ThingDef").FirstOrDefault(d => Dn(d) == "Rimfolk_PplR8_LineTower");
        C(tower != null && tower.Element("graphicData")?.Element("texPath")?.Value == "ColonyCouncil/Things/SignalTower"
          && tower.Descendants("li").Any(li => (string?)li.Attribute("Class") == "ColonyCouncil.CompProperties_RelayMast")
          && File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Things", "SignalTower.png")), "the flag tower looks like the Line's tower and works as its mast");
        var keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_PplR8_PeoplesAges.xml"));
        foreach (var k in Regex.Matches(ages + peoplesF, "\"(CC_PplR8_\\w+)\"").Select(m => m.Groups[1].Value).Distinct())
            C(keyed.Contains("<" + k + ">"), $"the key {k} has its words");

        // ================= The 96: each in its people's age, or checked and left =================

        var defs = new Dictionary<string, (XElement def, string file)>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Defs", "IncidentDefs"), "Rimfolk_Events_People*.xml"))
            foreach (var d in XDocument.Load(f).Root!.Elements("IncidentDef"))
                defs[Dn(d)] = (d, f);
        string doc = File.ReadAllText(Path.Combine(root, "docs", "content-round-7-peoples-ages.md"));
        string leftPart = doc.Contains("### Checked and left") ? doc.Substring(doc.IndexOf("### Checked and left", StringComparison.Ordinal)) : "";
        leftPart = leftPart.Contains("## Mods") ? leftPart.Substring(0, leftPart.IndexOf("## Mods", StringComparison.Ordinal)) : leftPart;
        var leftRows = Regex.Matches(leftPart, @"\| `Rimfolk_Evt_(\w+)` \| [^|]+ \| ([^|]+) \|").ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Trim());
        string audit = File.ReadAllText(Path.Combine(root, "Tools", "peoples_impact_audit.py"));
        var allCode = string.Join("\n", Directory.GetFiles(Path.Combine(src, "Peoples"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        C(NinetySix.Length == 96 && NinetySix.Distinct().Count() == 96, "the ninety-six, each once");
        int given = 0, left = 0;
        foreach (var name in NinetySix)
        {
            string dn = "Rimfolk_Evt_" + name;
            if (!defs.TryGetValue(dn, out var entry))
            {
                C(false, $"{dn} is there");
                continue;
            }
            var ext = Ext(entry.def)!;
            var texts = ext.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
            string worker = ((string)entry.def.Element("workerClass")!).Replace("ColonyCouncil.", "");
            var cls = Regex.Match(allCode, @"public class " + Regex.Escape(worker) + @"\b[^\n]*\n(.*?)\n    \}\n", RegexOptions.Singleline).Groups[1].Value;
            bool tagged = texts.Any(t => Regex.IsMatch(t, @"^\[[^\]]*\bage[0-3]\b"));
            bool byCode = cls.Contains("PplAges");
            bool tellsAge = cls.Contains("PeopleEvents.AgeTags(tags, ctx.faction)") || worker == "HappeningWorker_PplAgesSetting";
            if (leftRows.ContainsKey(name))
            {
                left++;
                C(!tagged && leftRows[name].Length > 20 && audit.Contains("\"" + dn + "\""), $"{name}: checked and left, with its reason in the round's notes and the audit");
                continue;
            }
            given++;
            C((tagged || byCode) && tellsAge, $"{name}: told in its people's own age (texts by age, or its goods by their age), its worker telling the age");
            // The texts told only of the tribal or medieval age name nothing of a later age.
            foreach (var t in texts)
            {
                var m = Regex.Match(t, @"^\[([^\]]*)\]");
                if (!m.Success) continue;
                var tagAges = Regex.Matches(m.Groups[1].Value, @"\bage([0-3])\b").Select(x => int.Parse(x.Groups[1].Value)).ToList();
                if (tagAges.Count == 0 || tagAges.Any(a => a >= 2)) continue;
                string body = t.Substring(m.Length);
                C(!Late.IsMatch(body), $"{name}: a text of the old ages names nothing of a later one ({Late.Match(body).Value})");
            }
            // The words as shown: plain, nobody given a sex the game didn't.
            foreach (var t in texts.Where(t => Regex.IsMatch(t, @"^\[[^\]]*\bage[0-3]\b")))
            {
                string body = Regex.Replace(t, @"^\[[^\]]*\]\s*", "");
                C(!body.Contains('—') && !body.Contains('!') && (body.Contains("Brannoc") || !Regex.IsMatch(body, @"\b(she|her|hers|he|him|his)\b")), $"{name}: an age's text in plain words, nobody given a sex (but the fellowship's founder)");
            }
        }
        C(given == 83 && left == 13, $"83 given their people's forms and 13 checked and left ({given}, {left})");
        C(Regex.Matches(audit, "\"Rimfolk_Evt_\\w+\": \"").Count == left, "the audit leaves the same events the notes do");

        // ================= Ages each one can be shown in =================

        // The people's reachable ages (PeopleAgesMath.Reachable): every age a text of the event is tagged for is one of them.
        var reach = new Dictionary<string, List<int>>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Defs", "PeopleDefs"), "Rimfolk_People_*.xml"))
            foreach (var d in XDocument.Load(f).Root!.Elements())
            {
                var pe = d.Element("modExtensions")?.Elements("li").FirstOrDefault(li => (string?)li.Attribute("Class") == "ColonyCouncil.PeopleExtension");
                string? calling = pe?.Element("calling")?.Value;
                string? tech = d.Element("techLevel")?.Value;
                if (calling == null || tech == null) continue;
                int own = tech == "Neolithic" ? 0 : tech == "Medieval" ? 1 : tech == "Industrial" ? 2 : 3;
                int lo = -1, hi = -1;
                var arise = pe!.Element("ariseAges")?.Value?.Split('~');
                if (arise != null && arise.Length == 2) { lo = int.Parse(arise[0]); hi = int.Parse(arise[1]); }
                int limit = int.Parse(pe.Element("ageLimit")?.Value ?? "-1"), floor = int.Parse(pe.Element("ageFloor")?.Value ?? "-1");
                reach[calling] = PeopleAgesMath.Reachable(own, lo, hi, limit, floor);
            }
        C(reach.Count >= 23 && reach["brokers"].SequenceEqual(new[] { 2, 3 }) && reach["herders"].SequenceEqual(new[] { 0, 1, 2 }), "the peoples' ages read from their defs");
        foreach (var name in NinetySix.Where(x => !leftRows.ContainsKey(x) && defs.ContainsKey("Rimfolk_Evt_" + x)))
        {
            var (def, _) = defs["Rimfolk_Evt_" + name];
            var callings = Ext(def)!.Element("callings")?.Elements("li").Select(l => l.Value).ToList();
            if (callings == null || callings.Count == 0) continue;
            var can = callings.Where(reach.ContainsKey).SelectMany(c => reach[c]).ToHashSet();
            var tagged = Ext(def)!.Element("texts")!.Elements("li").Select(l => Regex.Match(l.Value, @"^\[([^\]]*)\]").Groups[1].Value)
                .SelectMany(t => Regex.Matches(t, @"\bage([0-3])\b").Select(x => int.Parse(x.Groups[1].Value))).ToHashSet();
            C(tagged.All(can.Contains), $"{name}: its texts are told only for ages its people can reach");
        }

        // ================= A bug found on the way =================

        // The Wheel's scrap-runners named the mended piece ({THING}) when there was none.
        var scrap = Ext(defs["Rimfolk_Evt_WheelScrap"].def)!.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
        C(scrap.Where(t => t.Contains("{THING}")).All(t => Regex.IsMatch(t, @"^\[[^\]]*!nopiece")), "the scrap-runners name the mended piece only when there is one");

        // ================= The callers told from data =================

        foreach (var name in new[] { "OrderHealer", "LeagueOutrider", "Disputation" })
            C((string?)defs["Rimfolk_Evt_" + name].def.Element("workerClass") == "ColonyCouncil.HappeningWorker_PplAgesSetting", $"{name}: told from data, in its people's age");

        // ================= The colony's own means stay its own =================

        var copyist = Ext(defs["Rimfolk_Evt_ScholarCopyist"].def)!.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
        C(copyist.Any(t => t.StartsWith("[tribal]")) && copyist.Any(t => t.StartsWith("[tribal,age0]")) && copyist.Any(t => t.StartsWith("[!tribal,age2|age3]")),
            "a tribal colony still tells its stories to the copyist, whose tools are the society's age");
        var lander = Ext(defs["Rimfolk_Evt_EnclaveLander"].def)!.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
        C(lander.Any(t => t.StartsWith("[industrial]")) && lander.Any(t => t.StartsWith("[spacer]")) && lander.Any(t => t.StartsWith("[age2]")),
            "the colony hears the enclave by its own radio or comms; the lander is the enclave's");

        // ================= The round's notes =================

        C(doc.Contains("## Mods that would help") && doc.Contains("## The rules on every item") && doc.Contains("Yes, alongside"), "the round's notes: the rules, the table and the mods that would help");
        C(NinetySix.All(x => doc.Contains("`Rimfolk_Evt_" + x + "`")), "every one of the 96 is in the round's table");
        return n;
    }
}
