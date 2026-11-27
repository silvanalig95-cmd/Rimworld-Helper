using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Content round 5, the colony out in the world (docs/content-round-5-away.md, R5A-M1 to R5A-E8;
/// Missions/Round5Away.cs, Defs/MissionDefs/Rimfolk_Round5_Missions.xml, Defs/EncounterDefs/Rimfolk_Round5_Encounters.xml):
/// the pure rules of the order (Round5AwayMath), and the defs, words and seams of each: six missions on the frames there are
/// (hands lent, a captive walked home, a delivery, and the order whose goods are picked from the colony's own stores), every
/// people's age and the Empire's, requests in the town head's name, an ending for every way each can end; eight encounters
/// with their lands, moments and play-outs, one with a risk through the harm budget; no countdowns, no god named, plain
/// words, the defs read by name from the player's active mods, nothing saved and no tick of their own.
/// </summary>
public static class Round5AwayTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "round 5 away: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string Cls(XElement e) => ((string)e.Attribute("Class") ?? "").Replace("ColonyCouncil.", "");
        List<string> Lis(XElement e, string name) => e?.Element(name)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
        List<string> Tags(string line)
        {
            var m = Regex.Match(line ?? "", @"^\[([^\]]*)\]");
            return m.Success ? m.Groups[1].Value.Split(',').Select(t => t.Trim()).ToList() : new List<string>();
        }
        float F(string s, float d = 0f) => s == null ? d : float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        bool Plain(string t)
        {
            string body = Regex.Replace(t ?? "", @"\[[^\]]*\]", "");
            return !body.Contains('—') && !body.Contains('!');
        }
        bool NoCountdown(string t) =>
            !Regex.IsMatch(t ?? "", @"\{DAYS\}|\b(in|within) (\d+|a|two|three|four|five|six|seven) (days?|hours?)\b|\d+ (days?|hours?)\b|\bdays? (left|more)\b");
        var gods = new[] { "Hollin", "Smoulder", "Bellwether", "Morel", "Ennu", "Pip", "Cirrus", "Tamiu", "Helios", "Selene", "Murmur", "Revel", "Hush", "Ducat", "Undertow" };
        bool NoGod(string t) => !t.Contains("{GOD}") && !gods.Any(g => Regex.IsMatch(t, @"\b" + g + @"\b"));
        string code = Code("Missions/Round5Away.cs");
        var allSource = string.Join("\n", Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));

        // ================= The pure rules: an order from their merchants =================

        C(Round5AwayMath.OrderWorth(10, 12f) && !Round5AwayMath.OrderWorth(9, 100f) && !Round5AwayMath.OrderWorth(50, 2f) && Round5AwayMath.OrderWorth(80, 1.5f)
          && !Round5AwayMath.OrderWorth(20, 0f), "R5A-M3: ten or more in the stores, worth 120 or more in all");
        C(Round5AwayMath.OrderWeight(0, 10f) == 0f && Round5AwayMath.OrderWeight(10, 0f) == 0f && Math.Abs(Round5AwayMath.OrderWeight(25, 4f) - 10f) < 0.001f,
            "R5A-M3: each good weighed by the square root of its worth in the stores");
        C(Round5AwayMath.OrderCount(9, 100f, 0.5f, 1f) == 0 && Round5AwayMath.OrderCount(5, 50f, 0.5f, 1f) == 0, "R5A-M3: nothing ordered of what the colony keeps too little of");
        C(Round5AwayMath.OrderCount(100, 2f, 0f, 1f) == 70 && Round5AwayMath.OrderCount(100, 2f, 1f, 1f) == 130 && Round5AwayMath.OrderCount(100, 2f, 0.5f, 1f) == 100,
            "R5A-M3: about as much as the colony keeps, a third less to a third more");
        C(Round5AwayMath.OrderCount(100, 2f, 2f, 1f) == 130 && Round5AwayMath.OrderCount(100, 2f, -1f, 1f) == 70, "R5A-M3: the roll held between nought and one");
        C(Round5AwayMath.OrderCount(200, 20f, 1f, 1f) == 60 && Round5AwayMath.OrderCount(200, 20f, 1f, 2f) == 120 && Round5AwayMath.OrderCount(200, 20f, 1f, 0.1f) == 30,
            "R5A-M3: never more than 1200 silver's worth at the colony's wealth (half that at the least)");
        C(Round5AwayMath.OrderCount(10, 300f, 0f, 1f) == Round5AwayMath.OrderMinCount && Round5AwayMath.OrderCount(10, 200f, 0f, 1f) == 6, "R5A-M3: five at least");
        bool niceAll = true;
        for (int have = 40; have <= 400; have += 7)
            for (int i = 0; i <= 10; i++)
            {
                int c = Round5AwayMath.OrderCount(have, 3f, i / 10f, 1f);
                niceAll &= c >= Round5AwayMath.OrderMinCount && (c < 20 || c % 5 == 0) && (c < 100 || c % 10 == 0) && c <= Math.Ceiling(have * Round5AwayMath.OrderHigh) + 5;
            }
        C(niceAll, "R5A-M3: the count rounded as the missions round theirs, tens from a hundred and fives from twenty");
        C(Round5AwayMath.Nice(7) == 7 && Round5AwayMath.Nice(23) == 25 && Round5AwayMath.Nice(104) == 100 && Round5AwayMath.Nice(155) == 160, "the missions' rounding");
        C(Round5AwayMath.OrderPay(100f) == 150 && Round5AwayMath.OrderPay(1f) == 20 && Round5AwayMath.OrderPay(333f) == 500 && Round5AwayMath.OrderPayFactor == 1.5f,
            "R5A-M3: half again the goods' worth, in tens, twenty at least");
        C(Round5AwayMath.OrderTag("meals") == "food" && Round5AwayMath.OrderTag("textiles") == "cloth" && Round5AwayMath.OrderTag("medicine") == "medicine"
          && Round5AwayMath.OrderTag("drinks") == "drink" && Round5AwayMath.OrderTag("blocks") == "stone" && Round5AwayMath.OrderTag("ComponentIndustrial") == "goods",
            "R5A-M3: the order's words by what is ordered");
        C(Round5AwayMath.OrderGroups.SequenceEqual(new[] { "meals", "textiles", "medicine", "drinks", "blocks" }), "R5A-M3: the groups an order may be for, the missions' own");

        // ================= The missions =================

        var missionDoc = XDocument.Load(Path.Combine(root, "Defs", "MissionDefs", "Rimfolk_Round5_Missions.xml")).Root!;
        var missions = missionDoc.Elements("IncidentDef").ToList();
        C(missions.Count == 6, $"six missions ({missions.Count})");
        var expected = new Dictionary<string, (string worker, string ext, string kind)>
        {
            ["Rimfolk_R5A_Mission_RoundUp"] = ("HappeningWorker_MissionHands", "MissionHands", "roundup"),
            ["Rimfolk_R5A_Mission_Carver"] = ("HappeningWorker_MissionHands", "MissionHands", "carver"),
            ["Rimfolk_R5A_Mission_Order"] = ("HappeningWorker_R5AOrder", "MissionDelivery", "order"),
            ["Rimfolk_R5A_Mission_Cook"] = ("HappeningWorker_MissionHands", "MissionHands", "cook"),
            ["Rimfolk_R5A_Mission_Captive"] = ("HappeningWorker_MissionEscortWho", "MissionEscortWho", "captive"),
            ["Rimfolk_R5A_Mission_Bell"] = ("HappeningWorker_MissionDelivery", "MissionDelivery", "bell"),
        };
        var outcomesOf = new Dictionary<string, string[]>
        {
            ["HappeningWorker_MissionHands"] = new[] { "worked", "partial", "missed" },
            ["HappeningWorker_MissionEscortWho"] = new[] { "arrived", "alone", "turned", "harmed", "late" },
            ["HappeningWorker_MissionDelivery"] = new[] { "delivered", "partial", "missed" },
            ["HappeningWorker_R5AOrder"] = new[] { "delivered", "partial", "missed" },
        };
        // Every other mission's kind, so each of these is a kind of its own (and rests on its own).
        var otherKinds = new HashSet<string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Defs", "MissionDefs"), "*.xml").Where(f => !f.EndsWith("Rimfolk_Round5_Missions.xml")))
            foreach (var li in XDocument.Load(f).Root!.Descendants("li").Where(x => x.Attribute("Class") != null && x.Element("kind") != null))
                otherKinds.Add((string)li.Element("kind"));
        var thoughtDefs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories)
            .SelectMany(f => XDocument.Load(f).Root!.Elements("ThoughtDef")).Where(t => t.Element("defName") != null)
            .GroupBy(t => (string)t.Element("defName")).ToDictionary(g => g.Key, g => g.First());
        foreach (var (name, (worker, extName, kind)) in expected)
        {
            var d = missions.FirstOrDefault(e => (string)e.Element("defName") == name);
            C(d != null, $"{name}: a mission");
            if (d == null) continue;
            C(((string)d.Element("workerClass") ?? "") == "ColonyCouncil." + worker && Regex.IsMatch(allSource, @"class " + worker + @" : "), $"{name}: its worker ({worker})");
            C(F((string)d.Element("baseChance")) <= 0.4f && int.Parse((string)d.Element("minRefireDays")) >= 60, $"{name}: a small share of the missions' draw, not soon again");
            var he = d.Descendants("li").First(x => Cls(x) == "HappeningExtension");
            var mx = d.Descendants("li").FirstOrDefault(x => Cls(x) == extName);
            var text = d.Descendants("li").First(x => Cls(x) == "MissionText");
            C(mx != null && (string)mx.Element("kind") == kind, $"{name}: told from its {extName}, kind {kind}");
            C(otherKinds.Add(kind), $"{name}: a kind of its own ({kind}), not one of the other missions'");
            var texts = Lis(he, "texts");
            C(texts.Count >= 4, $"{name}: several ways to open ({texts.Count})");
            // The walk home is the pilgrim's road, which names no town head; the others ask in the head's name when the region knows them.
            if (worker != "HappeningWorker_MissionEscortWho")
                C(texts.Any(t => t.StartsWith("[host") && t.Contains("{HOST}")), $"{name}: a request in the name of the town's head, as the region knows them");
            else C(!texts.Any(t => t.Contains("{HOST}")), $"{name}: no town head named where the frame knows none");
            C(texts.Any(t => Tags(t).Contains("their:empire")), $"{name}: the Empire asks in its own titles and honour");
            bool low = texts.Any(t => Tags(t).Any(g => g == "their:lowtech" || g == "their:tribal" || g == "their:medieval"));
            bool high = texts.Any(t => Tags(t).Any(g => g == "their:hightech" || g == "their:industrial" || g == "their:spacer"));
            C(low && high, $"{name}: an opening for a low-tech people and for a high-tech one");
            var options = he.Element("options")!.Elements("li").ToList();
            C(options.Count == 2 && (string)he.Element("defaultOption") == "1", $"{name}: taken on or declined, declined if left");
            var all = texts.Concat(Lis(he, "titles")).Concat(Lis(text, "terms")).Concat(Lis(text, "accepted")).Concat(Lis(text, "ready")).Concat(Lis(text, "reminder"))
                .Concat(Lis(text, "done")).Concat(Lis(text, "doneTitles")).Concat(Lis(text, "fail")).Concat(Lis(text, "failTitles")).Concat(Lis(text, "cancelled"))
                .Concat(options.SelectMany(o => new[] { (string)o.Element("label") ?? "", (string)o.Element("outcome") ?? "" })).ToList();
            C(all.All(Plain), $"{name}: plain prose, no em dashes or exclamation marks");
            C(all.All(NoCountdown), $"{name}: no countdown in the words (the Quests tab shows the time)");
            C(all.All(NoGod), $"{name}: no god named");
            C(d.Element("label")?.Value is string label && !Regex.IsMatch(label, @"[\[\]{}]"), $"{name}: a plain label");
            var ends = Lis(text, "done").Concat(Lis(text, "fail")).ToList();
            foreach (var o in outcomesOf[worker])
                C(ends.Any(t => Tags(t).Contains(o)), $"{name}: an ending for {o}");
            C(Lis(text, "cancelled").Count > 0 && Lis(text, "terms").Count > 0 && Lis(text, "accepted").Count > 0 && Lis(text, "ready").Count > 0,
                $"{name}: its terms, its taking on, its being ready and its calling off");
            // Each form its words: every tag a form gives (a season's work, a variant of the goods) has an opening line.
            var forms = mx?.Element("forms")?.Elements("li").Select(f => (string)f.Element("tag")).ToList()
                        ?? mx?.Element("variants")?.Elements("li").Select(v => (string)v.Element("tag") ?? v.Value).ToList() ?? new List<string>();
            foreach (var form in forms.Where(f => f != "order"))
                C(texts.Any(t => Tags(t).Contains(form)), $"{name}: an opening for its form {form}");
        }

        XElement M(string name) => missions.First(e => (string)e.Element("defName") == name);
        XElement Ext(string name, string cls) => M(name).Descendants("li").First(x => Cls(x) == cls);
        List<string> Seasons(XElement form) => form.Element("seasons")?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();

        // Hands lent: who, how many, how long, what they bring home and feel.
        foreach (var name in new[] { "Rimfolk_R5A_Mission_RoundUp", "Rimfolk_R5A_Mission_Carver", "Rimfolk_R5A_Mission_Cook" })
        {
            var h = Ext(name, "MissionHands");
            int hours = int.Parse((string)h.Element("hours")), hands = int.Parse((string)h.Element("hands"));
            C(hours >= 12 && hours <= 48 && hands >= 1 && hands <= 2 && h.Element("skill") != null && h.Element("work") != null && (string)h.Element("deed") == "lentHands",
                $"{name}: one or two of the colony's good at it, half a day to two days, remembered as hands lent");
            string th = (string)h.Element("thought");
            C(thoughtDefs.TryGetValue(th ?? "", out var t) && F(t.Descendants("baseMoodEffect").First().Value) == 0f && t.Descendants("li").Any(x => Cls(x) == "RewardInstead"),
                $"{name}: its feeling ({th}), no mood of its own, a little recreation instead");
        }
        var roundup = Ext("Rimfolk_R5A_Mission_RoundUp", "MissionHands");
        C((string)roundup.Element("skill") == "Animals" && (string)roundup.Element("work") == "Handling"
          && roundup.Element("forms")!.Elements("li").ToDictionary(f => (string)f.Element("tag"), Seasons) is var rf
          && rf["lambing"].SequenceEqual(new[] { "Spring" }) && rf["shearing"].Contains("Summer") && rf["roundup"].SequenceEqual(new[] { "Fall" }),
            "R5A-M1: the colony's handlers; the lambing in spring, the shearing in summer, the round-up in autumn");
        var carver = Ext("Rimfolk_R5A_Mission_Carver", "MissionHands");
        C((string)carver.Element("skill") == "Artistic" && (string)carver.Element("work") == "Art"
          && carver.Element("forms")!.Elements("li").Select(f => (string)f.Element("tag")).SequenceEqual(new[] { "hall", "holyhouse" }),
            "R5A-M2: the colony's artist, for their hall or their house of prayer");
        var carverTexts = Lis(M("Rimfolk_R5A_Mission_Carver").Descendants("li").First(x => Cls(x) == "HappeningExtension"), "texts");
        C(carverTexts.Where(t => t.StartsWith("[host")).All(t => Tags(t).Contains("hall") || Tags(t).Contains("holyhouse")), "R5A-M2: the town head's words for each of the two buildings");
        C(carverTexts.Where(t => Tags(t).Contains("holyhouse")).All(t => !t.Contains("{GOD}")) && carverTexts.Any(t => Tags(t).Contains("holyhouse") && t.Contains("house of prayer")),
            "R5A-M2: their house of prayer their own, named only as theirs");
        var cook = Ext("Rimfolk_R5A_Mission_Cook", "MissionHands");
        C((string)cook.Element("skill") == "Cooking" && (string)cook.Element("work") == "Cooking"
          && cook.Element("forms")!.Elements("li").SelectMany(Seasons).Intersect(new[] { "Spring", "Summer", "Fall", "Winter" }).Count() == 4,
            "R5A-M4: the colony's cook, a feast in every season");

        // The order.
        C(code.Contains("class HappeningWorker_R5AOrder : HappeningWorker_MissionDelivery") && code.Contains("protected override bool Setup(") && !code.Contains("override void Check("),
            "R5A-M3: a delivery whose goods are picked here; its carts, checks and endings the delivery's own");
        C(code.Contains("R5AOrders.Goods(map)") && code.Contains("Round5AwayMath.OrderCount(pick.have, pick.unit, Rand.Value, LivingEvents.WealthFactor(map))")
          && code.Contains("Round5AwayMath.OrderPay(n * pick.unit)") && code.Contains("Round5AwayMath.OrderTag(pick.what)"), "R5A-M3: the pure rules decide how much, what for and the words");
        C(code.Contains("DefDatabase<RecipeDef>.AllDefsListForReading") && code.Contains("r.products") && code.Contains("IsInAnyStorage()") && code.Contains("IsForbidden(Faction.OfPlayer)"),
            "R5A-M3: anything a recipe of the game's or a mod's makes, in the colony's stores and not forbidden");
        C(new[] { "d.IsWeapon", "d.IsApparel", "d.IsDrug", "ThingDefOf.Silver", "ThingCategoryDefOf.ResourcesRaw", "PlayerCanSell()" }.All(code.Contains),
            "R5A-M3: never weapons, clothes, drugs, silver or raw resources, and only what can be sold");
        C(code.Contains("m.host = InviteForms.Host(") && code.Contains("Interplay.MissionHead(map)"), "R5A-M3: in the town head's name; the reeve deals with it");
        var orderTexts = Lis(M("Rimfolk_R5A_Mission_Order").Descendants("li").First(x => Cls(x) == "HappeningExtension"), "texts");
        foreach (var tag in new[] { "food", "cloth", "medicine", "drink", "stone", "goods" })
            C(orderTexts.Any(t => Tags(t).Contains(tag)), $"R5A-M3: an opening for an order of {tag}");
        C(orderTexts.All(t => t.Contains("{TALE}")), "R5A-M3: every opening names the goods ordered");

        // The bell.
        var bell = Ext("Rimfolk_R5A_Mission_Bell", "MissionDelivery");
        var bellForms = bell.Element("variants")!.Elements("li").ToDictionary(v => (string)v.Element("tag"), v => (string)v.Element("era"));
        C(bellForms.Count == 4 && bellForms["drum"] == "tribal" && bellForms["bell"] == "medieval" && bellForms["siren"] == "industrial" && bellForms["beacon"] == "spacer",
            "R5A-M6: a drum, a bell, a siren or a beacon, by the colony's own means");
        var bellTitles = Lis(M("Rimfolk_R5A_Mission_Bell").Descendants("li").First(x => Cls(x) == "HappeningExtension"), "titles");
        C(bellForms.Keys.All(k => bellTitles.Any(t => Tags(t).Contains(k))), "R5A-M6: a title for each");
        var bellGoods = bell.Descendants("what").Select(x => x.Value).Concat(bell.Descendants("instead").Select(x => x.Value)).Distinct().ToList();
        C(bellGoods.All(g => g == "textiles" || new[] { "WoodLog", "Steel", "ComponentIndustrial", "Plasteel" }.Contains(g)), "R5A-M6: the game's own goods and cloth of any mod");

        // The captive.
        var captive = Ext("Rimfolk_R5A_Mission_Captive", "MissionEscortWho");
        var captiveTexts = Lis(M("Rimfolk_R5A_Mission_Captive").Descendants("li").First(x => Cls(x) == "HappeningExtension"), "texts");
        C(Lis(captive, "variants").SequenceEqual(new[] { "escaped", "released" }) && Lis(captive, "variants").All(v => captiveTexts.Any(t => Tags(t).Contains(v))),
            "R5A-M5: got away, or let go when their people paid; each told");
        C(Code("Missions/Workers_MissionNeighbours4.cs").Contains("class HappeningWorker_MissionEscortWho : HappeningWorker_MissionEscort"),
            "R5A-M5: the pilgrim's road: they wait at the colony, any caravan for their town takes them");
        C(captiveTexts.Concat(Lis(M("Rimfolk_R5A_Mission_Captive").Descendants("li").First(x => Cls(x) == "MissionText"), "ready")).All(t => !Regex.IsMatch(t, @"\b(ambush|bandits were seen|raiders are)\b")),
            "R5A-M5: the road's danger is the game's own, never promised in the words");

        // The missions' one draw: still the same weight in all, with these six in it.
        float total = 0f;
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Defs", "MissionDefs"), "*.xml"))
            foreach (var d in XDocument.Load(f).Root!.Elements("IncidentDef"))
                if (((string)d.Element("workerClass") ?? "").Contains("HappeningWorker_")
                    && d.Descendants("li").Any(x => Cls(x) == "MissionText")) total += F((string)d.Element("baseChance") ?? "0");
        C(Math.Abs(NeighboursFourMath.PoolFactor(total) * total - NeighboursFourMath.MissionPoolWeight) < 0.001f, $"the missions still weigh {NeighboursFourMath.MissionPoolWeight} in all ({total:0.0} scaled)");
        C(File.ReadAllText(Path.Combine(root, "Tools", "Tests", "NeighboursFourTests.cs")).Contains("\"Rimfolk_Round5_Missions.xml\""),
            "this round's missions take their share of the draw since the fourth round of neighbours (NeighboursFourTests)");

        // ================= The encounters =================

        var encDoc = XDocument.Load(Path.Combine(root, "Defs", "EncounterDefs", "Rimfolk_Round5_Encounters.xml")).Root!;
        var encs = encDoc.Elements("ColonyCouncil.EncounterDef").ToList();
        C(encs.Count == 8, $"eight encounters ({encs.Count})");
        var encExpected = new Dictionary<string, string>
        {
            ["Rimfolk_R5A_Enc_Scales"] = "Trader", ["Rimfolk_R5A_Enc_Grace"] = "Visitor", ["Rimfolk_R5A_Enc_Shelter"] = "Wild", ["Rimfolk_R5A_Enc_Forester"] = "Visitor",
            ["Rimfolk_R5A_Enc_Loom"] = "Visitor", ["Rimfolk_R5A_Enc_SnowGlare"] = "Visitor", ["Rimfolk_R5A_Enc_SoundRock"] = "Visitor", ["Rimfolk_R5A_Enc_Boundary"] = "Visitor",
        };
        var otherEnc = Directory.GetFiles(Path.Combine(root, "Defs", "EncounterDefs"), "*.xml").Where(f => !f.EndsWith("Rimfolk_Round5_Encounters.xml"))
            .SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.EncounterDef")).Select(e => (string)e.Element("label") ?? "").Where(l => l.Length > 0).ToHashSet();
        var harmful = new[] { "DilemmaEffect_Hediff", "DilemmaEffect_Fight", "DilemmaEffect_MentalState", "DilemmaEffect_TooClose" };
        foreach (var (name, kind) in encExpected)
        {
            var e = encs.FirstOrDefault(x => (string)x.Element("defName") == name);
            C(e != null, $"{name}: an encounter");
            if (e == null) continue;
            C((string)e.Element("stranger")?.Element("kind") == kind, $"{name}: met with a {kind}");
            string label = (string)e.Element("label") ?? "";
            C(label.Length > 0 && !Regex.IsMatch(label, @"[\[\]{}]") && !otherEnc.Contains(label), $"{name}: a plain label of its own ({label})");
            var texts = Lis(e, "texts");
            C(texts.Count >= 4, $"{name}: four ways to tell it or more ({texts.Count})");
            C(texts.All(Plain) && texts.All(NoCountdown) && texts.All(NoGod), $"{name}: plain words, no countdown, no god named");
            C(texts.All(t => t.Contains("{A_nameDef}")), $"{name}: every line names the colonist");
            if (kind != "Wild")
                C(texts.Any(t => Tags(t).Contains("theirLowtech")) && texts.Any(t => Tags(t).Contains("theirHightech")) && texts.Any(t => Tags(t).Count == 0),
                    $"{name}: a line for a low-tech people, one for a high-tech one, and plain ones");
            var effects = e.Element("effects")?.Elements("li").ToList() ?? new List<XElement>();
            C(effects.Count > 0, $"{name}: something comes of it");
            foreach (var x in effects)
                C(Regex.IsMatch(allSource, @"class " + Regex.Escape(Cls(x)) + @"\b"), $"{name}: the effect {Cls(x)} exists");
            foreach (var t in e.Descendants("thought").Select(x => x.Value))
                C(thoughtDefs.ContainsKey(t), $"{name}: the mood {t} exists");
            C(name == "Rimfolk_R5A_Enc_Shelter" || !effects.Any(x => harmful.Contains(Cls(x))), $"{name}: nobody hurt");
            C(e.Element("children") == null, $"{name}: one of the colony's grown people");
        }
        XElement E(string name) => encs.First(x => (string)x.Element("defName") == name);
        // R5A-E2: the visit plays out at a table.
        C(E("Rimfolk_R5A_Enc_Grace").Descendants("li").Any(x => Cls(x) == "DilemmaEffect_Visit" && (string)x.Element("to") == "Stranger" && (string)x.Element("sit") == "true")
          && Lis(E("Rimfolk_R5A_Enc_Grace"), "texts").All(t => t.Contains("sat down at a table")), "R5A-E2: the two sit down at a table, as the words say");
        // R5A-E3: the one with a risk.
        var shelter = E("Rimfolk_R5A_Enc_Shelter");
        C(shelter.Descendants("li").Count(x => Cls(x) == "DilemmaEffect_TooClose") == 1 && !shelter.Descendants("li").Any(x => Cls(x) == "DilemmaEffect_Hediff" || Cls(x) == "DilemmaEffect_Fight"),
            "R5A-E3: the wild one too close, the harm budget's own (by the game's taming-revenge chance), nothing else");
        C(Code("Harm/WildHurts.cs").Contains("Harm.Roll(a, HarmKinds.TooClose, chance, cls)") && Code("Harm/WildHurts.cs").Contains("HarmFour.Mote(a,"),
            "R5A-E3: rolled through the harm budget and shown with a mote and a line");
        var st = shelter.Element("stranger")!;
        C((string)st.Element("notPredator") == "true" && F((string)st.Element("maxBodySize")) <= 2.6f && (string)shelter.Element("moment") == "rain|snow|storm,outside"
          && ((string)shelter.Element("colonist")?.Element("tags") ?? "").Contains("!trait:Nervous") && ((string)shelter.Element("colonist")?.Element("tags") ?? "").Contains("!trait:Volatile"),
            "R5A-E3: a grazer no bigger than a muffalo, out in the weather, never the nervous who goes closer");
        C(Lis(shelter, "texts").Count(t => Tags(t).Count > 0) == 3 && new[] { "rain", "snow", "storm" }.All(w => Lis(shelter, "texts").Any(t => Tags(t).Contains(w))),
            "R5A-E3: the rain, the snow and the storm each told");
        // The lands and the seasons.
        C(Lis(E("Rimfolk_R5A_Enc_Forester"), "lands").SequenceEqual(new[] { "forest", "boreal" }), "R5A-E4: the forest");
        C(Lis(E("Rimfolk_R5A_Enc_SnowGlare"), "lands").SequenceEqual(new[] { "tundra", "boreal" }) && Lis(E("Rimfolk_R5A_Enc_SnowGlare"), "seasons").Contains("Winter")
          && (string)E("Rimfolk_R5A_Enc_SnowGlare").Element("dayOnly") == "true", "R5A-E6: the snowfields in winter, by day");
        C(Lis(E("Rimfolk_R5A_Enc_SoundRock"), "lands").SequenceEqual(new[] { "mountains", "hills", "caves" }), "R5A-E7: the mountains, the hills, the caves");
        var boundary = E("Rimfolk_R5A_Enc_Boundary");
        C(Lis(boundary, "lands").SequenceEqual(new[] { "grassland", "hills" }) && F((string)boundary.Element("stranger")!.Element("minGoodwill"), -100f) >= 0f
          && boundary.Descendants("li").Any(x => Cls(x) == "DilemmaEffect_Goodwill" && int.Parse((string)x.Element("amount")) <= 3), "R5A-E8: the open grass, a neighbour at peace, a little goodwill");
        // R5A-E5: the building scene, the game's benches and the active mods' looms.
        var loom = E("Rimfolk_R5A_Enc_Loom");
        C((string)loom.Element("scene") == "building" && Lis(loom, "colonyHas").Take(2).SequenceEqual(new[] { "HandTailoringBench", "ElectricTailoringBench" }),
            "R5A-E5: the visitor goes to the colony's bench and looks it over, the game's benches first");
        var activeBuildings = File.ReadAllLines(Path.Combine(root, "docs", "player-mods", "buildings.tsv")).Skip(1).Select(l => l.Split('\t'))
            .Where(c => c.Length > 2 && c[2] == "yes").Select(c => c[0]).ToHashSet();
        foreach (var b in Lis(loom, "colonyHas"))
            C(activeBuildings.Contains(b), $"R5A-E5: {b} is a building of the player's active mods");

        // ================= The defs read by name, from the player's active mods =================

        var active = new HashSet<string>();
        using (var gz = new GZipStream(File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz")), CompressionMode.Decompress))
        using (var r = new StreamReader(gz))
        {
            string line;
            while ((line = r.ReadLine()) != null)
            {
                var cols = line.Split('\t');
                if (cols.Length >= 4 && cols[3] == "yes") active.Add(cols[0] + "|" + cols[1]);
            }
        }
        foreach (var (type, name) in new[]
                 {
                     ("ThingDef", "WoodLog"), ("ThingDef", "Steel"), ("ThingDef", "ComponentIndustrial"), ("ThingDef", "Plasteel"), ("WorkTypeDef", "Handling"),
                     ("WorkTypeDef", "Art"), ("WorkTypeDef", "Cooking"), ("ThingDef", "HandTailoringBench"), ("ThingDef", "VFE_TableTailorLarge")
                 })
            C(active.Contains(type + "|" + name), $"{type} {name} is in the player's active mods");

        // ================= Seams: nothing saved, no tick, nobody hurt by the round's code =================

        C(!Regex.IsMatch(code, @"Scribe_|Sched\.|\bHarm\.|Injure\(|TakeDamage\(|AddHediff\("), "no saved state, no tick of its own, nobody hurt by the round's code");
        C(Regex.Matches(code, @"\bclass (\w+)").Cast<Match>().All(m => m.Groups[1].Value.StartsWith("R5A") || m.Groups[1].Value.Contains("_R5A")), "the round's classes are its own (R5A)");
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Round5_Away.xml"));
        foreach (Match m in Regex.Matches(code, "\"(CC_R5A_[A-Za-z_]*[A-Za-z])\""))
            C(keyed.Contains("<" + m.Groups[1].Value + ">"), $"{m.Groups[1].Value} has its words");
        C(!Regex.IsMatch(Regex.Replace(keyed, @"<!--.*?-->", "", RegexOptions.Singleline), @"\b(in|within) \d+ (days?|hours?)\b|—|!"), "the messages plain, no countdowns");
        var thoughtsDoc = XDocument.Load(Path.Combine(root, "Defs", "ThoughtDefs", "Rimfolk_Round5_Away_Thoughts.xml")).Root!;
        foreach (var t in thoughtsDoc.Elements("ThoughtDef"))
        {
            string tn = (string)t.Element("defName");
            var stages = t.Element("stages")!.Elements("li").ToList();
            C(stages.All(s => F((string)s.Element("baseMoodEffect")) is float mood && mood >= 0f && mood <= 2f) && stages.All(s => Plain((string)s.Element("description")) && NoGod((string)s.Element("description"))),
                $"{tn}: a small good mood or none, in plain words");
        }
        C(File.ReadAllText(Path.Combine(root, "Tools", "Tests", "Tests.csproj")).Contains("Missions/Round5AwayMath.cs"), "the pure rules are linked into the tests");
        string doc = File.ReadAllText(Path.Combine(root, "docs", "content-round-5-away.md"));
        C(expected.Keys.Concat(encExpected.Keys).All(doc.Contains), "every item is in the round's list (docs/content-round-5-away.md)");
        return n;
    }
}
