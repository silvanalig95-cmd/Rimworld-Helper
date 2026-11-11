using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Camps that come in the coldest winter (the player's request of 10 November 2026: "i haven't had any camps probably
/// becauser it was just freezing cold all the time, i'd like a handful of camp events that can also come in the coldest
/// winter"). What kept them away was the flat floor of the cold (no camp below -30 °C, every one packing up below -35)
/// and ordinary travellers' layers. Winter folk have no flat floor: the game's own limit for each of them, as they are
/// dressed in their own winter clothes, decides; everyone else keeps the floor and the same limit. Then the five kinds,
/// their wardrobe by age and the mod pool, and the mod's side: the gates, the dressing, the ice fishers' holes and the
/// caravan's night under the colony's roof.
/// </summary>
public static class CampWinterTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "winter camps: " + what);
        }

        // ---------------- The rules ----------------

        C(CampWeatherMath.TooExtremeToCome(CampWeatherMath.ExtremeCold - 1f) && CampWeatherMath.TooExtremeToCome(CampWeatherMath.ExtremeCold - 1f, false),
            "ordinary travellers still stay away below the floor of the cold");
        C(!CampWeatherMath.TooExtremeToCome(-55f, true) && !CampWeatherMath.TooExtremeToStay(-60f, true), "winter folk: no flat floor of the cold, coming or staying");
        C(CampWeatherMath.TooExtremeToCome(CampWeatherMath.ExtremeHeat + 1f, true) && CampWeatherMath.TooExtremeToStay(CampWeatherMath.ExtremeHeat + CampWeatherMath.StayPast + 1f, true),
            "the heat is the same for everyone");
        bool same = true;
        for (float t = -80f; t <= 80f; t += 0.5f)
        {
            if (CampWeatherMath.TooExtremeToCome(t) != CampWeatherMath.TooExtremeToCome(t, false) || CampWeatherMath.TooExtremeToStay(t) != CampWeatherMath.TooExtremeToStay(t, false)) same = false;
            if (CampWeatherMath.TooExtremeToCome(t, true) && !CampWeatherMath.TooExtremeToCome(t)) same = false;
            if (!CampWeatherMath.TooExtremeToCome(t, true) && CampWeatherMath.TooExtremeToStay(t, true)) same = false;
        }
        C(same, "every other camp keeps its gates as they were; winter folk come in all they'd stay in, and anything an ordinary camp comes in");
        C(CampWeatherMath.ColdEnough(-3f, 2f) && !CampWeatherMath.ColdEnough(4f, 2f) && CampWeatherMath.ColdEnough(30f, CampWeatherMath.NoColdMark),
            "a kind that comes only in the cold waits for the coming days' warmest hour to drop below its mark; no mark, any weather");
        C(!CampWeatherMath.WouldStop(-48f, -20f, -30f, 20f) && CampWeatherMath.WouldStop(-38f, -20f, -30f, 20f),
            "whether anyone comes is the game's own limit as they're dressed: ten degrees past comfortable, for their coldest night");
        C(CampWeatherMath.PackUp(3, 4) && !CampWeatherMath.PackUp(1, 4), "and they pack up only when the cold starts to hurt most of them");

        // ---------------- The kinds ----------------

        var doc = XDocument.Load(Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKinds_Winter.xml")).Root!;
        var kinds = doc.Elements("ColonyCouncil.CampKindDef").ToList();
        string V(XElement k, string e) => k.Element(e)?.Value;
        List<string> L(XElement k, string e) => k.Element(e)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
        C(kinds.Count >= 5 && kinds.Count <= 6, $"a handful of winter kinds ({kinds.Count})");
        C(kinds.Any(k => V(k, "work") == "icefish" && V(k, "needs") == "ice"), "ice fishers who need ice and fish through it");
        C(kinds.Any(k => V(k, "inSnow") == "true" && V(k, "trader") == "true"), "a caravan come in out of the snow, trading while it waits");
        C(kinds.Count(k => V(k, "armedHunters") == "true") >= 2, "trappers and herd followers who hunt");
        C(kinds.Any(k => V(k, "needs") == "wildHerd"), "herd followers come only after a wild herd on the land");
        foreach (var k in kinds)
        {
            string dn = V(k, "defName") ?? "?";
            C(V(k, "winter") == "true" && V(k, "wearSet") == "Rimfolk_CampWear_Winter", $"{dn}: winter folk, in the winter wardrobe");
            C(float.TryParse(V(k, "coldBelow"), out var below) && below <= 8f, $"{dn}: comes only while the land stays cold ({V(k, "coldBelow")})");
            var structures = k.Element("structures")?.Elements("li").ToList() ?? new List<XElement>();
            C(structures.Any(s => s.Element("role")?.Value == "fire") && (k.Element("campGoods")?.ToString().Contains("WoodLog") ?? false),
                $"{dn}: a fire, with wood by it to keep it burning");
            C(new[] { "Hut", "Tents", "Ring" }.Contains(V(k, "sleep")), $"{dn}: a shelter for the cold (a hut, tents, or a ring of beds behind a windbreak)");
            var texts = L(k, "texts");
            C(texts.Count >= 5 && texts.Any(t => Regex.IsMatch(t, @"^\[(their:tribal|their:lowtech|their:medieval)")) && texts.Any(t => t.StartsWith("[their:hightech]") || t.StartsWith("[their:industrial]")),
                $"{dn}: its letter in the form of its people's age, lowtech and hightech");
            C(L(k, "titles").Count >= 3 && L(k, "settled").Count >= 3 && L(k, "farewell").Count >= 3 && L(k, "lines").Count >= 7 && L(k, "joinTexts").Count >= 1,
                $"{dn}: its story told (titles, settling, lines, joining, farewell)");
            var asks = k.Element("asks")?.Elements("li").ToList() ?? new List<XElement>();
            C(asks.Count >= 3, $"{dn}: asks of its own ({asks.Count})");
            C(L(k, "help").Count >= 2, $"{dn}: good turns of its own");
            C(!k.ToString().Contains("—") && !Regex.IsMatch(string.Join(" ", texts), "!"), $"{dn}: no em dashes or exclamation marks in its letters");
        }
        foreach (var (file, dn) in new[] { ("Rimfolk_CampKinds_Peoples.xml", "Rimfolk_Camp_WinterHerd"), ("Rimfolk_CampKinds_PeoplesLore.xml", "Rimfolk_Camp_LeagueWinter") })
        {
            var k = XDocument.Load(Path.Combine(root, "Defs", "CampDefs", file)).Root!.Elements("ColonyCouncil.CampKindDef").Single(x => V(x, "defName") == dn);
            C(V(k, "winter") == "true" && V(k, "wearSet") == "Rimfolk_CampWear_Winter", $"{dn}: a winter camp already, now winter folk in the same clothes");
        }
        var shelters = kinds.Where(k => k.Element("asks")!.Elements("li").Any(a => a.Element("key")?.Value == "shelter")).ToList();
        C(shelters.Count >= 1 && shelters.All(k => k.Element("asks")!.Elements("li").First(a => a.Element("key")?.Value == "shelter").Element("theyAsk")?.Value == "true"),
            "the caravan (and the pilgrims) ask for the colony's roof on a stormy night");

        // ---------------- The wardrobe, by age and the mod pool ----------------

        var wardrobe = doc.Elements("ColonyCouncil.CampWearDef").Single();
        var wears = wardrobe.Element("wears")!.Elements("li").ToList();
        bool For(XElement w, string tech)
        {
            var order = new[] { "Undefined", "Animal", "Neolithic", "Medieval", "Industrial", "Spacer", "Ultra", "Archotech" };
            int t = Array.IndexOf(order, tech), lo = Array.IndexOf(order, w.Element("minTech")?.Value ?? "Undefined"), hi = Array.IndexOf(order, w.Element("maxTech")?.Value ?? "Archotech");
            return t >= lo && t <= hi;
        }
        foreach (var tech in new[] { "Neolithic", "Medieval", "Industrial", "Spacer" })
        {
            var mine = wears.Where(w => For(w, tech)).ToList();
            C(mine.Any(w => w.Element("defs")!.Elements("li").Any(x => x.Value == "Apparel_Parka")) && mine.Any(w => w.Element("defs")!.Elements("li").Any(x => x.Value == "Apparel_KidParka")),
                $"{tech}: a coat for the grown and a kid's parka for a child");
            C(mine.Count >= 2, $"{tech}: a coat and something for the head or feet");
        }
        foreach (var w in wears)
        {
            var defs = w.Element("defs")!.Elements("li").ToList();
            int firstPlain = defs.FindIndex(x => x.Attribute("MayRequire") == null);
            C(firstPlain < 0 || defs.Skip(firstPlain).All(x => x.Attribute("MayRequire") == null || x.Attribute("MayRequire")!.Value.StartsWith("Ludeon.")),
                $"{string.Join("/", defs.Select(x => x.Value))}: the player's mods first, the game's own last");
        }
        C(wears.Any(w => w.ToString().Contains("VanillaExpanded.VAPPE")) && wears.Any(w => w.ToString().Contains("VE.VanillaChristmasExpanded")) && wears.Any(w => w.ToString().Contains("OskarPotocki.VFE.Medieval2")),
            "from the player's mod pool: Vanilla Apparel Expanded, Vanilla Christmas Expanded, VFE Medieval 2");

        // ---------------- The mod's side ----------------

        string camps = Path.Combine(root, "Source", "ColonyCouncil", "Camps");
        string Code(string f) => Regex.Replace(File.ReadAllText(Path.Combine(camps, f)), @"//[^\n]*", "");
        string tracker = Code("CampTracker.cs"), def = Code("CampKindDef.cs"), weather = Code("CampWeather.cs"), life = Code("CampLife.cs"), land = Code("CampKindsLand.cs"),
            winter = Code("CampWinter.cs"), work = Code("CampWork.cs");
        C(Regex.IsMatch(def, @"public bool FitsWeather\(Map map\)[\s\S]*?CampWeatherMath\.TooExtremeToCome\(map\.mapTemperature\.OutdoorTemp, winter\)[\s\S]*?CampLand\.SnowNow\(map\)[\s\S]*?CampWeatherMath\.ColdEnough\(CampWeather\.Usual\(map\)\.high, coldBelow\)"),
            "a kind fits the weather by its own floor, its snow and its mark of cold");
        C(tracker.Contains("k.FitsSeason(map) && k.FitsWeather(map)") && Code("CampMemory.cs").Contains("!p.kind.FitsWeather(map)") && Code("CampRaid.cs").Contains("k.FitsWeather(map)"),
            "a new camp, a camp coming back and a rival party all keep to it");
        C(Regex.IsMatch(tracker, @"CampWeatherMath\.TooExtremeToCome\(temp\) && \(CampWeatherMath\.TooExtremeToCome\(temp, true\)[\s\S]*?k\.winter && k\.FitsEra\(\) && k\.Possible && !k\.fights && k\.FitsWeather\(map\)"),
            "past the floor of the cold the day stays open only for winter folk who could come");
        C(tracker.Contains("CampWeather.Dress(p, low, high, f, kind)") && tracker.Contains("CampWeatherMath.WouldStop(low, high, comfy.min, comfy.max)"),
            "dressed in their own clothes, every one of them must be safe through the stay's coldest night (the game's own limit)");
        C(Regex.IsMatch(weather, @"kind\?\.Wears != null && low < p\.ComfortableTemperatureRange\(\)\.min\) added\.AddRange\(WearOwn\(p, kind, tech\)\)") && weather.Contains("w.ForAge(tech)"),
            "their winter clothes go on first when the stay is colder than they're comfortable in, by their people's age");
        C(life.Contains("CampWeatherMath.TooExtremeToStay(temp, kind?.winter ?? false)") && life.Contains("CampWeatherMath.Exposed(temp, comfy.min, comfy.max)"),
            "they stay until the cold starts to hurt most of them");
        C(land.Contains("case IceNeed: return WishWorker_Fishing.CanFish(map) && IceTargets(map).Count > 0;") && land.Contains("case CampLand.IceWork: return IceFishJob(p);")
            && land.Contains("Fished(p, job.targetB.Cell, true)"), "ice fishers come for frozen water with fish (fishing open to the colony), fish it, and keep the catch");
        C(winter.Contains("map.terrainGrid.RemoveTempTerrain(hole)") && work.Contains("CampLand.WorkTick(pawn, job)") && winter.Contains("UnderIce(map, h)"),
            "a hole cut in the ice and kept open while they fish; the game freezes it over again");
        C(land.Contains("else if (kind.work == \"watch\" || Shelters) WatchHourly(hour);") && land.Contains("(kind?.work != \"watch\" && !Shelters)") && land.Contains("c.kind?.work != \"watch\" && !c.Shelters"),
            "a night under the colony's roof for any kind that asks for it, not the watchers alone");
        C(Code("CampKinds.cs").Contains("if (CampLand.SnowNow(map)) tags.Add(\"snow\");") && Code("CampKinds.cs").Contains("tags.Add(\"ice\")"), "their words know the snow and the ice");
        string facts = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Text", "Facts", "FactsMath.cs"));
        foreach (var k in kinds) C(facts.Contains("\"them:camp:" + (V(k, "defName") ?? "").Replace("Rimfolk_Camp_", "") + "\""), $"{V(k, "defName")}: a facts tag");
        return n;
    }
}
