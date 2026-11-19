using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Quests, round 3: Q14, a cache in old ruins (docs/quests-proposals.md; Missions/HomeQuests3Math.cs,
/// Workers_MissionHome5.cs), the one quest that sends the colony's people out: only within three tiles, under one quest in
/// ten among the single quests, the reward by the people's age, the days by the distance, what counts as done, every age's
/// old map in its words, plain words, the game's own item stash site, and the dev actions.
/// </summary>
public static class HomeQuests3Tests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "a cache in old ruins: " + what);
        }

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        bool Plain(string t)
        {
            string body = Regex.Replace(t ?? "", @"^\[[^\]]*\]\s*", "");
            return !body.Contains('—') && !body.Contains('!');
        }

        // ================= Only close, and rarely =================

        C(HomeQuests3Math.WithinReach(1f) && HomeQuests3Math.WithinReach(3f) && !HomeQuests3Math.WithinReach(3.5f) && !HomeQuests3Math.WithinReach(0.5f), "the ruins within one to three tiles");
        var missionDefs = Directory.GetFiles(Path.Combine(root, "Defs", "MissionDefs"), "Rimfolk_Missions_Home*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements("IncidentDef")).ToList();
        float all = missionDefs.Sum(d => F((string)d.Element("baseChance") ?? "0"));
        float away = missionDefs.Where(d => (string)d.Element("defName") == "Rimfolk_Mission_RuinsCache").Sum(d => F((string)d.Element("baseChance") ?? "0"));
        C(away > 0f && HomeQuests3Math.AwayShare(away, all) <= 0.1f, $"under one single quest in ten sends the colony's people out ({HomeQuests3Math.AwayShare(away, all):0.000})");
        C(Math.Abs(away - HomeQuests3Math.BaseChance) < 0.001f && HomeQuests3Math.BaseChance < HomeQuestsMath.BaseChance, "half the chance of a quest at home");

        // ================= The days, the reward, what counts =================

        C(HomeQuests3Math.Days(1f) == 6 && HomeQuests3Math.Days(2.4f) == 10 && HomeQuests3Math.Days(3f) == 10 && HomeQuests3Math.Days(9f) == 10, "six to ten days by the distance");
        int T = HomeQuestsMath.Tribal, S = HomeQuestsMath.Spacer;
        C(HomeQuests3Math.Reward(T, false, 1f, 0f) == 180 && HomeQuests3Math.Reward(S, false, 1f, 1f) == 490 && HomeQuests3Math.Reward(1, false, 5f, 1f) == 760,
            "the reward by the asking people's age and the colony's wealth");
        C(HomeQuests3Math.KeepWorth(240) == 120 && HomeQuests3Math.KeepWorth(0) == 0, "what else lay with it, the colony's: half the reward's worth");
        C(HomeQuests3Math.Outcome(true, false, false, true, false) == CacheEnd.Lost && HomeQuests3Math.Outcome(false, true, false, false, true) == CacheEnd.Home
          && HomeQuests3Math.Outcome(false, false, true, false, true) == CacheEnd.Away && HomeQuests3Math.Outcome(false, false, false, true, false) == CacheEnd.Away
          && HomeQuests3Math.Outcome(false, false, false, false, false) == CacheEnd.Lost && HomeQuests3Math.Outcome(false, false, false, true, true) == CacheEnd.Lost,
            "home, still away (at the ruins or with the caravan), or lost");

        // ================= The defs =================

        var d = missionDefs.FirstOrDefault(x => (string)x.Element("defName") == "Rimfolk_Mission_RuinsCache");
        C(d != null && (string)d.Element("workerClass") == "ColonyCouncil.HappeningWorker_MissionRuinsCache", "its event and worker");
        if (d == null) return n;
        XElement Ext(string cls) => d.Element("modExtensions")?.Elements("li").FirstOrDefault(li => (string)li.Attribute("Class") == "ColonyCouncil." + cls);
        var texts = Ext("HappeningExtension")!.Element("texts")!.Elements("li").Select(x => x.Value).ToList();
        foreach (var tag in new[] { "hide", "parchment", "survey", "slate" })
            C(texts.Any(t => t.StartsWith("[" + tag + "]")), "their old map by their age: " + tag);
        var text = Ext("MissionText")!;
        string terms = string.Join(" ", text.Element("terms")!.Elements("li").Select(x => x.Value));
        C(terms.Contains("within three tiles") && terms.Contains("caravan") && terms.Contains("{ITEM}") && terms.Contains("{DAYS}"), "the terms say how close, that a caravan goes, the reward and the days");
        foreach (var o in new[] { "lost", "notfound" })
            C(text.Element("fail")!.Elements("li").Any(l => l.Value.StartsWith("[" + o + "]")), "a letter for " + o);
        C(text.Element("done")!.Elements("li").Any(l => l.Value.StartsWith("[returned]")), "a letter for the cache returned");
        C(d.Descendants().Where(e => !e.HasElements).All(e => Plain(e.Value)), "plain words");
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Missions_Home3.xml")).Root!.Elements().ToList();
        C(keyed.Count >= 5 && keyed.All(e => Plain(e.Value)), "its messages in plain words");
        var item = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).SelectMany(f => XDocument.Load(f).Root!.Elements("ThingDef"))
            .FirstOrDefault(t => (string)t.Element("defName") == "Rimfolk_QuestCache");
        C(item != null && File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Things", "QuestCache.png")), "the old cache is Rimfolk's own, with its picture");

        // ================= The code =================

        var code = Code("Missions/Workers_MissionHome5.cs");
        C(code.Contains("TileFinder.TryFindNewSiteTile(out tile, map.Tile, HomeQuests3Math.MinTiles, HomeQuests3Math.MaxTiles"), "the ruins are only ever found within three tiles");
        C(code.Contains("GetNamedSilentFail(\"ItemStash\")") && code.Contains("sp.things ??= new ThingOwner<Thing>(sp, false)"), "the game's own item stash site, the cache among its things");
        C(code.Contains("[HarmonyPatch(typeof(Site), nameof(Site.PostMapGenerate))]") && code.Contains("HappeningWorker_MissionRuinsCache.MapMade(__instance)"),
            "the cache is in the ruins when the colony gets there, whatever the map making did");
        C(code.Contains("Couriers.CollectThings(map, new List<Thing> { m.thing }, m.faction, HomeQuests.PayThings(m, m.reward))"), "brought home, their people come for it and pay");
        C(code.Contains("if (site == null || site.HasMap) return;"), "the ruins are taken from the world map when it is over, never while the colony is there");
        C(!Regex.IsMatch(code, @"Harm\.|TakeDamage|LordJob_Assault"), "no fight of its own: the ruins hold the cache, and the land's own animals");
        C(Code("Missions/HomeQuests.cs").Contains("Array.IndexOf(HomeQuests3Math.Kinds, m.kind) >= 0"), "a quest at home for the tags and the dev tools");
        var dbg = Code("Missions/HomeQuestsDebug.cs");
        C(dbg.Contains("\"Rimfolk_Mission_RuinsCache\"") && dbg.Contains("HappeningWorker_MissionRuinsCache.DebugOutcomes"), "dev mode: offer it, and end it with each of its outcomes");
        return n;
    }
}
