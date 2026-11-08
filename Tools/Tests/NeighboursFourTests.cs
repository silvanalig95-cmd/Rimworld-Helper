using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The fourth round of neighbours and the people who come to the colony (the player's request, 6 November 2026): new
/// neighbours' requests in the missions framework (hands lent to a town, a store kept, a young beast raised, an old one
/// and a hunter lost on the land, a traveller seen home, a wall, fodder, stores for a long road, seed grain), new callers
/// at the gate told from data, and more ways to tell the callers that had one. The rules without game types
/// (Missions/NeighboursFourMath.cs), and the defs and code: every age's forms and the Empire's, requests in the name of
/// the town's head, answers that play out on the map, no countdown in the words, no hurt, and one draw for every mission
/// so the missions come no oftener than before.
/// </summary>
public static class NeighboursFourTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "neighbours four: " + what);
        }

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        List<string> Lis(XElement e, string name) => e?.Element(name)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
        string Cls(XElement e) => ((string)e.Attribute("Class") ?? "").Replace("ColonyCouncil.", "");
        float F(string s, float d = 0f) => s == null ? d : float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        var keyed = Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml")
            .SelectMany(f => XDocument.Load(f).Root!.Elements()).GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => g.First().Value);
        bool Plain(string t)
        {
            string body = Regex.Replace(t ?? "", @"^\[[^\]]*\]\s*", "");
            return !body.Contains('—') && !body.Contains('!');
        }
        bool NoCountdown(string t) => !Regex.IsMatch(t, @"\{DAYS\}|\b(in|within) \d+ (days|hours)\b|\bdays? (left|more)\b");

        // ================= The rules =================

        C(NeighboursFourMath.PoolFactor(NeighboursFourMath.MissionPoolWeight) == 1f && NeighboursFourMath.PoolFactor(3f) == 1f
          && Math.Abs(NeighboursFourMath.PoolFactor(13.6f) - 0.5f) < 0.001f && NeighboursFourMath.PoolFactor(0f) == 1f, "the missions' one draw: a share of what they weighed, never more");
        C(NeighboursFourMath.HandsShare(36, 36) == 1f && NeighboursFourMath.HandsShare(40, 36) == 1f && NeighboursFourMath.HandsShare(18, 36) == 0.5f
          && NeighboursFourMath.HandsShare(17, 36) == 0f && NeighboursFourMath.HandsShare(0, 36) == 0f, "the whole stay earns it all, half the stay half, less nothing");
        C(NeighboursFourMath.SharePay(200, 1f) == 200 && NeighboursFourMath.SharePay(210, 0.5f) == 110 && NeighboursFourMath.SharePay(10, 0.5f) == 10, "the pay rounded to tens, ten at least");
        C(NeighboursFourMath.HandsDays(1f, 36) == 7f && NeighboursFourMath.HandsDays(2.5f, 12) == 9f, "time for the road both ways, the stay and three days to get ready");
        C(NeighboursFourMath.CanSpare(2, 3, 2) && !NeighboursFourMath.CanSpare(2, 2, 2) && !NeighboursFourMath.CanSpare(1, 5, 2), "enough who can do it, and someone left at home");
        C(NeighboursFourMath.KeepFee(1000f) == 250 && NeighboursFourMath.KeepFee(10f) == 20 && NeighboursFourMath.KeepFeeShare == 0.25f, "the keeping is a quarter of the goods' worth, twenty at least");
        C(NeighboursFourMath.Shortfall(0, 2f) == 0 && NeighboursFourMath.Shortfall(11, 1f) == 20 && NeighboursFourMath.Shortfall(30, 1.9f) == 60, "what's missing paid at its worth, rounded up to tens");
        C(NeighboursFourMath.KeepOutcome(100, 100, 0, 0) == KeepEnd.Kept && NeighboursFourMath.KeepOutcome(90, 100, 50, 20) == KeepEnd.MadeGood
          && NeighboursFourMath.KeepOutcome(90, 100, 10, 20) == KeepEnd.Short, "kept, made good in silver, or short");
        C(NeighboursFourMath.KeptWell(0f, false, false) && !NeighboursFourMath.KeptWell(NeighboursFourMath.BeastThin, false, false) && !NeighboursFourMath.KeptWell(0f, true, false)
          && !NeighboursFourMath.KeptWell(0f, false, true), "a beast kept well: fed, not sick, no wound left untended");
        C(NeighboursFourMath.BeastShare(true) == 1f && NeighboursFourMath.BeastShare(false) == 0.5f, "kept poorly, half the pay");

        // ================= The missions' one draw =================

        var missionFiles = Directory.GetFiles(Path.Combine(root, "Defs", "MissionDefs"), "*.xml");
        var ours = new[] { "Rimfolk_Missions_Neighbours4.xml", "Rimfolk_Missions_Neighbours4_Goods.xml" };
        // The asks of Rimfolk's own peoples since (Peoples/), which take a share of the same draw (Tools/Tests/PeoplesTests.cs).
        var since = new[] { "Rimfolk_Missions_Peoples.xml", "Rimfolk_Missions_PeoplesC.xml", "Rimfolk_Missions_PeoplesD.xml", "Rimfolk_Missions_PeoplesE.xml", "Rimfolk_Missions_PeoplesF.xml",
            "Rimfolk_Missions_PeoplesG.xml" };
        float before = 0f, after = 0f;
        foreach (var f in missionFiles)
            foreach (var d in XDocument.Load(f).Root!.Elements("IncidentDef"))
            {
                string w = (string)d.Element("workerClass") ?? "";
                if (!w.Contains("HappeningWorker_Mission")) continue;
                float b = F((string)d.Element("baseChance"));
                after += b;
                if (!ours.Contains(Path.GetFileName(f)) && !since.Contains(Path.GetFileName(f))) before += b;
            }
        C(Math.Abs(before - NeighboursFourMath.MissionPoolWeight) < 0.001f, $"the pool's weight is what the missions weighed before this round ({before:0.0})");
        C(Math.Abs(NeighboursFourMath.PoolFactor(after) * after - NeighboursFourMath.MissionPoolWeight) < 0.001f, $"with the new ones the missions still weigh {NeighboursFourMath.MissionPoolWeight} in all ({after:0.0} scaled)");
        var happen = Code("Happenings/Happenings.cs");
        C(happen.Contains("f *= MissionPool.Factor(def);") && happen.Contains("float w = d.baseChance * MissionPool.Factor(d);"), "the share is taken in the storyteller's draw and in Rimfolk's own");
        var sim = File.ReadAllText(Path.Combine(root, "Tools", "Balance", "Sim.cs")) + File.ReadAllText(Path.Combine(root, "Tools", "Balance", "Code.cs"));
        C(sim.Contains("MissionPoolWeight") && sim.Contains("NeighboursFourMath.cs"), "the balance model takes the same share");

        // ================= The requests =================

        var code = Code("Missions/Workers_MissionNeighbours4.cs");
        var road = Code("Missions/Workers_MissionRoad.cs");
        var missions = ours.SelectMany(f => XDocument.Load(Path.Combine(root, "Defs", "MissionDefs", f)).Root!.Elements("IncidentDef")).ToList();
        C(missions.Count == 14, $"fourteen requests ({missions.Count})");
        var outcomesOf = new Dictionary<string, string[]>
        {
            ["HappeningWorker_MissionHands"] = new[] { "worked", "partial", "missed" },
            ["HappeningWorker_MissionKeep"] = new[] { "kept", "madegood", "short" },
            ["HappeningWorker_MissionBeast"] = new[] { "returned", "poorly", "bonded", "died", "lost" },
            ["HappeningWorker_MissionLost"] = new[] { "found", "searchers", "harmed" },
            ["HappeningWorker_MissionEscortWho"] = new[] { "arrived", "alone", "turned", "harmed", "late" },
            ["HappeningWorker_MissionDelivery"] = new[] { "delivered", "partial", "missed" },
        };
        var hosted = new[] { "HappeningWorker_MissionHands", "HappeningWorker_MissionKeep", "HappeningWorker_MissionBeast", "HappeningWorker_MissionDelivery" };
        foreach (var d in missions)
        {
            string name = (string)d.Element("defName");
            string worker = ((string)d.Element("workerClass") ?? "").Replace("ColonyCouncil.", "");
            C(outcomesOf.ContainsKey(worker), $"{name}: a worker of this round or the delivery framework ({worker})");
            if (worker != "HappeningWorker_MissionDelivery") C(code.Contains("class " + worker + " : "), $"{name}: its worker is in the code");
            C(F((string)d.Element("baseChance")) <= 0.4f && int.Parse((string)d.Element("minRefireDays")) >= 60, $"{name}: a small share of the draw, not soon again");
            var ext = d.Descendants("li").First(x => Cls(x) == "HappeningExtension");
            var text = d.Descendants("li").First(x => Cls(x) == "MissionText");
            var texts = Lis(ext, "texts");
            C(texts.Count >= 4, $"{name}: several ways to open ({texts.Count})");
            C(texts.Any(t => !Regex.IsMatch(t, @"^\[[^\]]*their:(tribal|medieval|industrial|spacer|lowtech|hightech)"))
              || texts.Any(t => t.Contains("their:lowtech") || t.Contains("their:tribal")) && texts.Any(t => t.Contains("their:hightech") || t.Contains("their:industrial")),
                $"{name}: an opening for every people's age");
            C(texts.Any(t => Regex.IsMatch(t, @"^\[[^\]]*their:empire")) || worker == "HappeningWorker_MissionLost", $"{name}: the Empire asks in its own titles and honour");
            if (hosted.Contains(worker)) C(texts.Any(t => t.StartsWith("[host]") && t.Contains("{HOST}")), $"{name}: a request in the name of the town's head, as the region knows them");
            var options = ext.Element("options")!.Elements("li").ToList();
            C(options.Count == 2, $"{name}: taken on or declined");
            var all = texts.Concat(Lis(text, "terms")).Concat(Lis(text, "accepted")).Concat(Lis(text, "ready")).Concat(Lis(text, "reminder")).Concat(Lis(text, "done"))
                .Concat(Lis(text, "fail")).Concat(Lis(text, "cancelled")).Concat(options.Select(o => (string)o.Element("outcome") ?? "")).ToList();
            C(all.All(Plain), $"{name}: plain prose, no em dashes or exclamation marks");
            C(all.All(NoCountdown), $"{name}: no countdown in the words (the Quests tab shows the time)");
            var ends = Lis(text, "done").Concat(Lis(text, "fail")).ToList();
            foreach (var o in outcomesOf.GetValueOrDefault(worker, Array.Empty<string>()))
                C(ends.Any(t => Regex.IsMatch(t, @"^\[([^\]]*,)?" + o + @"(,[^\]]*)?\]")), $"{name}: an ending for {o}");
            C(Lis(text, "cancelled").Count > 0 && Lis(text, "terms").Count > 0 && Lis(text, "accepted").Count > 0, $"{name}: its terms, its taking on, and its calling off");
        }
        C(code.Contains("m.host = InviteForms.Host(") && Code("Missions/Workers_MissionDelivery.cs").Contains("m.host = InviteForms.Host(") && Code("Missions/InviteForms.cs").Contains("tags.Add(\"host\")"),
            "the town's head named by their title when the ledger knows them, and only then");
        var tagsCode = Code("Missions/Missions.cs");
        C(tagsCode.Contains("tags.Add(\"their:\" + Ages.Tag(Ages.Of(m.faction)))") && tagsCode.Contains("tags.Add(\"their:empire\")"), "a request's words by the asking people's age, the Empire apart");

        // Each kind as it plays out.
        C(code.Contains("MissionPeoples.CaravansAt(town)") && code.Contains("CaravanInventoryUtility.GiveThing(c, silver)") && code.Contains("p.skills?.Learn(h.skill"),
            "hands: the colony's caravan at their town, paid into its packs, what they learned there");
        C(code.Contains("Couriers.Deliver(map, things, m.faction, spot)") && code.Contains("Couriers.CollectGoals(") && code.Contains("ThingDefOf.Silver.defName, shortfall"),
            "a store: their carts bring it to the gate and come back for it, what's missing made good in silver");
        C(code.Contains("Arrivals.Visit(new List<Pawn> { drover, x }") && code.Contains("x.SetFaction(Faction.OfPlayer)") && code.Contains("HappeningWorker_MissionRunaway.HandBack(m, map,"),
            "a young beast: walked in by their drover and handed over at the gate, led home by them at the end (the runaway's way)");
        C(code.Contains("HappeningWorker_MissionRunaway.Bonded(x)"), "a beast bonded to one of the colony's stays where it is loved");
        C(code.Contains("class HappeningWorker_MissionLost : HappeningWorker_MissionLostChild") && road.Contains("protected virtual FloatRange LostAges")
          && road.Contains("KinMemory.Note(m.faction, FoundDeed, finderNow, m.name)"), "the lost: the lost child's search, for an old one and a hunter");
        C(code.Contains("class HappeningWorker_MissionEscortWho : HappeningWorker_MissionEscort") && road.Contains("protected virtual FloatRange TravellerAges") && road.Contains("SetOffKey.Translate("),
            "a traveller seen home: the pilgrim's road, for a trader, a healer or a scholar");
        C(!code.Contains("Harm.") && !code.Contains("TakeDamage") && !code.Contains("AddHediff"), "the requests hurt nobody");
        C(!code.Contains("Sched.") && !code.Contains("Scribe_"), "no tick of their own and nothing new saved: the missions' own fields");

        // ================= What their people remember =================

        var memory = Code("World/FactionMemory.cs");
        var favour = Code("Diplomacy/FavourMath.cs");
        foreach (var k in new[] { "lentHands", "foundLost" })
        {
            C(Regex.IsMatch(memory, "\\[\"" + k + "\"\\] = K\\(DeedSide\\.Favor"), $"{k}: a good turn their people remember");
            C(favour.Contains("\"" + k + "\""), $"{k}: big enough to be a debt of honour");
            C(keyed.ContainsKey("CC_Memory_Deed_" + k) && keyed.ContainsKey("CC_Memory_Deed_" + k + "_Name") && keyed.ContainsKey("CC_Dip_For_" + k), $"{k}: in words");
        }
        var hands = missions.Where(d => ((string)d.Element("workerClass")).EndsWith("MissionHands")).ToList();
        C(hands.Count == 5, $"five kinds of hands lent ({hands.Count})");
        foreach (var d in hands)
        {
            var h = d.Descendants("li").First(x => Cls(x) == "MissionHands");
            int hours = int.Parse((string)h.Element("hours")), count = int.Parse((string)h.Element("hands"));
            C(hours >= 12 && hours <= 48 && count >= 1 && count <= 2 && h.Element("skill") != null, $"{d.Element("defName")!.Value}: one or two of the colony's, half a day to two days");
        }

        // ================= Callers at the gate =================

        const string callFile = "Defs/IncidentDefs/Rimfolk_Events_Neighbours4.xml";
        var callers = XDocument.Load(Path.Combine(root, callFile)).Root!.Elements("IncidentDef").ToList();
        C(callers.Count == 7, $"seven new callers ({callers.Count})");
        var playOut = new[] { "DilemmaEffect_Lesson", "DilemmaEffect_CarryOut", "DilemmaEffect_Visit", "DilemmaEffect_Spawn", "DilemmaEffect_SpawnAnimal", "DilemmaEffect_SowPatch", "DilemmaEffect_Plant" };
        var gathers = new[] { "DilemmaEffect_Gathering", "DilemmaEffect_Feast", "DilemmaEffect_Bonfire", "DilemmaEffect_Toast", "DilemmaEffect_Words", "VisitEffect_WorkHere", "DilemmaEffect_Race",
            "DilemmaEffect_Contest", "DilemmaEffect_WorkParty", "DilemmaEffect_Procession", "DilemmaEffect_Vigil", "DilemmaEffect_Search", "DilemmaEffect_HuntParty" };
        foreach (var e in callers)
        {
            string name = (string)e.Element("defName");
            C(((string)e.Element("workerClass")).EndsWith("HappeningWorker_Setting"), $"{name}: told from data");
            var ext = e.Descendants("li").First(x => Cls(x) == "HappeningExtension");
            var setting = e.Descendants("li").First(x => Cls(x) == "HappeningSetting");
            C((string)setting.Element("visitor") == "true" && (string)setting.Element("people") == "own", $"{name}: someone of a people of about the colony's age, at the gate");
            var texts = Lis(ext, "texts");
            bool low = texts.Any(t => Regex.IsMatch(t, @"^\[[^\]]*(lowtech|tribal|medieval)")), high = texts.Any(t => Regex.IsMatch(t, @"^\[[^\]]*(!lowtech|industrial|spacer)"));
            C(low && high || texts.Any(t => !t.StartsWith("[")), $"{name}: every age's forms");
            C(F((string)e.Element("baseChance")) <= 0.8f && int.Parse((string)e.Element("minRefireDays")) >= 35, $"{name}: an ordinary share of the pool, and not soon again");
            C((string)ext.Element("group") == "Visitors" && ext.Element("battle") == null, $"{name}: a visitor, never a fight");
            var options = ext.Element("options")!.Elements("li").ToList();
            int def = int.Parse((string)ext.Element("defaultOption") ?? "0");
            C(options.Count >= 3 && def < options.Count, $"{name}: a real choice");
            C(options[def].Element("effects")!.Elements("li").All(x => Cls(x) == "DilemmaEffect_SendAway"), $"{name}: silence sends them on, and sets nothing going");
            C(options.Count(o => o.Element("effects")?.Elements("li").Any(x => playOut.Contains(Cls(x))) == true) >= 2, $"{name}: two answers play out on the map");
            C(!options.Any(o => o.Element("effects")?.Elements("li").Any(x => gathers.Contains(Cls(x))) == true), $"{name}: nothing that gathers the colony (no more staged scenes)");
            C(!options.Any(o => o.Element("effects")?.Elements("li").Any(x => Cls(x).StartsWith("DilemmaEffect_Hediff") || Cls(x) == "DilemmaEffect_Fight") == true), $"{name}: no hurt");
            var words = texts.Concat(options.Select(o => (string)o.Element("outcome") ?? "")).Concat(options.SelectMany(o => Lis(o, "outcomes"))).ToList();
            C(words.All(Plain) && words.All(NoCountdown), $"{name}: plain prose, no countdown");
        }

        // ================= More ways to tell the callers =================

        var callerDefs = XDocument.Load(Path.Combine(root, "Defs/IncidentDefs/Rimfolk_Events_Callers.xml")).Root!.Elements("IncidentDef")
            .ToDictionary(d => (string)d.Element("defName"), d => d);
        var variants = XDocument.Load(Path.Combine(root, "Defs/IncidentDefs/Rimfolk_Events_Neighbours4_Variants.xml")).Root!.Elements("ColonyCouncil.LetterVariantsDef").ToList();
        C(variants.Count == 5, $"five callers told more ways ({variants.Count})");
        foreach (var v in variants)
        {
            string target = (string)v.Element("incident");
            C(callerDefs.ContainsKey(target), $"{v.Element("defName")!.Value}: for {target}, a caller of the colony's");
            if (!callerDefs.ContainsKey(target)) continue;
            int count = callerDefs[target].Descendants("li").First(x => Cls(x) == "HappeningExtension").Element("options")!.Elements("li").Count();
            var outs = v.Element("outcomes")!.Elements("li").ToList();
            C(Lis(v, "texts").Count >= 2 && outs.Count == count && outs.All(o => int.Parse((string)o.Element("option")) < count), $"{target}: new openings, and an ending for each answer");
            C(Lis(v, "texts").Concat(outs.SelectMany(o => Lis(o, "lines"))).All(t => Plain(t) && NoCountdown(t)), $"{target}: plain prose");
        }

        // ================= Memories and words =================

        var thoughts = XDocument.Load(Path.Combine(root, "Defs/ThoughtDefs/Rimfolk_Thoughts_Neighbours4.xml")).Root!.Elements("ThoughtDef").ToList();
        string mdefs = string.Concat(ours.Select(f => File.ReadAllText(Path.Combine(root, "Defs", "MissionDefs", f))));
        foreach (var th in thoughts)
        {
            string name = (string)th.Element("defName");
            C(mdefs.Contains(">" + name + "<"), $"{name}: a memory a request gives");
            C(F(th.Descendants("baseMoodEffect").First().Value) == 0f && th.Descendants("li").Any(x => Cls(x) == "RewardInstead"), $"{name}: no mood of its own, a little recreation instead");
        }
        foreach (Match m in Regex.Matches(code + road, "\"(CC_[A-Za-z_]+)\""))
            C(keyed.ContainsKey(m.Groups[1].Value), $"the words for {m.Groups[1].Value}");
        C(keyed.Where(k => k.Key.StartsWith("CC_Mission_Hands") || k.Key.StartsWith("CC_Mission_Keep") || k.Key.StartsWith("CC_Mission_Beast") || k.Key.StartsWith("CC_Mission_EscortWho"))
            .All(k => Plain(k.Value) && NoCountdown(k.Value)), "the requests' panel words: plain, no countdown");
        return n;
    }
}
