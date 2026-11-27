using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The content round's fourth part, its council questions and world events (docs/content-round-4-council-world.md, R4C-1 to
/// R4W-6; Dilemmas/DilemmaWorkers_Round4.cs, World/Variety/WorldRound4.cs, Defs/DilemmaDefs/Rimfolk_Round4_Council.xml,
/// Defs/WorldSituationDefs/Rimfolk_WS_Round4.xml): the pure rules (Round4CouncilWorldMath), and the defs, words and seams of
/// each: ten questions with their workers and answers that play out, six world events in every age's form, no countdowns, no
/// god named but by the colony's own name, the game's and the player's mods' defs read by name from the active rows, no saved
/// collection and no hurt of their own.
/// </summary>
public static class Round4CouncilWorldTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "round 4 council and world events: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string Cls(XElement e) => ((string)e.Attribute("Class") ?? "").Replace("ColonyCouncil.", "");
        List<string> Lines(XElement e, string list) => e?.Element(list)?.Elements("li").Select(li => li.Value).ToList() ?? new List<string>();
        List<string> Tags(string line)
        {
            var m = Regex.Match(line ?? "", @"^\[([^\]]*)\]");
            return m.Success ? m.Groups[1].Value.Split(',').Select(t => t.Trim()).ToList() : new List<string>();
        }
        string dlm = Code("Dilemmas/DilemmaWorkers_Round4.cs"), world = Code("World/Variety/WorldRound4.cs");
        string audit = File.ReadAllText(Path.Combine(root, "Tools", "playout_audit.py"));
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Round4_CouncilWorld.xml"));
        var allSource = string.Join("\n", Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        var gods = new[] { "Hollin", "Smoulder", "Bellwether", "Morel", "Ennu", "Pip", "Cirrus", "Tamiu", "Helios", "Selene", "Murmur", "Revel", "Hush", "Ducat", "Undertow" };

        // ================= The pure rules =================

        C(Round4CouncilWorldMath.StoneWorth(30) && !Round4CouncilWorldMath.StoneWorth(29), "R4C-1: thirty loose stones or more are worth a question");
        C(Round4CouncilWorldMath.BlockBills(0) == 1 && Round4CouncilWorldMath.BlockBills(33) == 33 && Round4CouncilWorldMath.BlockBills(500) == 40, "R4C-1: a block bill for each stone, one to forty");
        C(Round4CouncilWorldMath.OwlLow(0.49f) && !Round4CouncilWorldMath.OwlLow(0.5f) && !Round4CouncilWorldMath.OwlLow(-1f), "R4C-2: the night owl's mood under half");
        C(Round4CouncilWorldMath.HardDecree(2f, 1f) && !Round4CouncilWorldMath.HardDecree(1f, 3f) && !Round4CouncilWorldMath.HardDecree(3f, 0.5f),
            "R4C-4: a decree of hard work, with a day or more left");
        C(Round4CouncilWorldMath.FallsHard(60f, false, false, 0f) && Round4CouncilWorldMath.FallsHard(30f, true, false, 0f) && Round4CouncilWorldMath.FallsHard(30f, false, true, 0f)
          && Round4CouncilWorldMath.FallsHard(30f, false, false, 55f) && !Round4CouncilWorldMath.FallsHard(59f, false, false, 54f), "R4C-4: it falls hard on an elder, the ill, the injured, the worn");
        C(Round4CouncilWorldMath.HandOverDays(0.2f) == 1 && Round4CouncilWorldMath.HandOverDays(3.2f) == 4 && Round4CouncilWorldMath.HandOverDays(40f) == 15, "R4C-4: the work handed over for the decree's days left, rounded up");
        C(Round4CouncilWorldMath.Master(10, 5) && !Round4CouncilWorldMath.Master(10, 6) && !Round4CouncilWorldMath.Master(9, 0) && Round4CouncilWorldMath.Master(12, -1),
            "R4C-5: a master is ten or more, five above the colony's best");
        C(Round4CouncilWorldMath.SlaveWorn(0.1f, 0.3f, 0.5f) && !Round4CouncilWorldMath.SlaveWorn(0.3f, 0.3f, 0.5f) && !Round4CouncilWorldMath.SlaveWorn(0.1f, 0.5f, 0.5f)
          && !Round4CouncilWorldMath.SlaveWorn(0.1f, 0.3f, 0.3f) && Round4CouncilWorldMath.SlaveEase < 0f, "R4C-7: a slave worn, low and held down well enough; the rest eases their fear");
        C(Round4CouncilWorldMath.DryDanger(25f, false, 30) && !Round4CouncilWorldMath.DryDanger(24f, false, 30) && !Round4CouncilWorldMath.DryDanger(30f, true, 99)
          && !Round4CouncilWorldMath.DryDanger(30f, false, 29) && Round4CouncilWorldMath.FirebreakMark >= Round4CouncilWorldMath.DryGrassFrom, "R4C-9: hot, dry and thirty dry plants at the edge");
        C(Round4CouncilWorldMath.SpringsFor(true, 0f) && Round4CouncilWorldMath.SpringsFor(false, 55f) && !Round4CouncilWorldMath.SpringsFor(false, 54f)
          && Round4CouncilWorldMath.Aches.Length == 6 && Round4CouncilWorldMath.SpringsOldFrom >= 40f, "R4W-1: an ache first, else one worn, else the eldest");
        C(Round4CouncilWorldMath.RaftPrice(100, 1.2f) == 40 && Round4CouncilWorldMath.RaftPrice(0, 1.2f) == 0 && Round4CouncilWorldMath.RaftPrice(1, 1f) == 10, "R4W-3: a third of the logs' worth, in tens");
        C(Enumerable.Range(0, 11).Select(i => Round4CouncilWorldMath.DriftLogs(i / 10f)).All(v => v >= 15 && v <= 35) && Round4CouncilWorldMath.DriftLogs(0.999f) == 35,
            "R4W-3: fifteen to thirty-five logs wash up");
        C(Round4CouncilWorldMath.ArmsPrice(100f, 3) == 150 && Round4CouncilWorldMath.ArmsPrice(10f, 1) == 30 && Round4CouncilWorldMath.ArmsFair(40f) && !Round4CouncilWorldMath.ArmsFair(39f)
          && !Round4CouncilWorldMath.ArmsFair(901f), "R4W-5: half their worth, thirty at least, a fair price only");
        C(Round4CouncilWorldMath.ArmsTech("tribal") == 2 && Round4CouncilWorldMath.ArmsTech("medieval") == 3 && Round4CouncilWorldMath.ArmsTech("industrial") == 4 && Round4CouncilWorldMath.ArmsTech("spacer") == 5,
            "R4W-5: the weapons of the people's age");
        C(Round4CouncilWorldMath.Found(0.5f) && !Round4CouncilWorldMath.Found(0.55f), "R4W-6: the caravan found at its chance");

        // ================= The council questions =================

        var council = XDocument.Load(Path.Combine(root, "Defs", "DilemmaDefs", "Rimfolk_Round4_Council.xml")).Root!;
        var dilemmas = council.Elements("ColonyCouncil.DilemmaDef").ToList();
        var names = new[] { "Stone", "NightOwl", "SeatAway", "DecreeToll", "VisitingMaster", "ColdCell", "SlaveRest", "PetBed", "DryGrass", "Grace" };
        C(dilemmas.Count == 10, $"ten council questions ({dilemmas.Count})");
        foreach (var name in names)
        {
            var d = dilemmas.FirstOrDefault(e => e.Element("defName")?.Value == "Rimfolk_DlmR4_" + name);
            C(d != null, $"{name}: a council question");
            if (d == null) continue;
            string worker = (d.Element("workerClass")?.Value ?? "").Replace("ColonyCouncil.", "");
            C(worker == "DilemmaWorker_R4C" + name && dlm.Contains("class " + worker + " "), $"{name}: its worker ({worker})");
            var options = d.Element("options")?.Elements("li").ToList() ?? new List<XElement>();
            C(options.Count == 3, $"{name}: three answers");
            int def = int.TryParse(d.Element("defaultOption")?.Value, out int dv) ? dv : -1;
            C(def >= 0 && def < options.Count, $"{name}: its default answer is one of them");
            var texts = Lines(d, "texts");
            C(texts.Count >= 3 && texts.Any(t => Regex.IsMatch(t, @"^\[(lowtech|theirLowtech)\]")), $"{name}: three wordings or more, with an age form");
            int played = options.Count(o => (o.Element("effects")?.Elements("li") ?? Enumerable.Empty<XElement>()).Any(e => audit.Contains("\"" + Cls(e) + "\"")));
            C(played >= 2, $"{name}: its answers play out on the map ({played} of 3)");
            foreach (var e in d.Descendants("li").Where(x => x.Attribute("Class") != null))
                C(Regex.IsMatch(allSource, @"class " + Regex.Escape(Cls(e)) + @"\b"), $"{name}: the effect {Cls(e)} exists");
            string words = string.Join(" ", texts.Concat(options.SelectMany(o => new[] { o.Element("label")?.Value ?? "", o.Element("outcome")?.Value ?? "" })));
            C(!Regex.IsMatch(words, @"\bin (\d+|a|two|three|four|five) (days?|hours?)\b") && !Regex.IsMatch(words, @"\d+ (days?|hours?)\b"), $"{name}: no countdown in the words");
            string bare = Regex.Replace(words, @"\[[^\]]*\]", "");
            C(!bare.Contains('—') && !bare.Contains('!'), $"{name}: no em dashes or exclamations");
            C(!gods.Any(g => Regex.IsMatch(words + string.Join(" ", d.Descendants("lines").SelectMany(l => l.Elements("li")).Select(l => l.Value)), @"\b" + g + @"\b")),
                $"{name}: no god named but by the colony's own name for it ({{GOD}})");
            C(d.Element("label")?.Value is string label && !Regex.IsMatch(label, @"[\[\]{}]"), $"{name}: a plain label");
            foreach (var l in d.Descendants("lines").SelectMany(x => x.Elements("li")).Select(x => x.Value))
                C(Regex.Matches(Regex.Replace(l, @"\{[^}]*\}", "X"), @"[.?]\s").Count == 0, $"{name}: a spoken line is one sentence ({l.Substring(0, Math.Min(30, l.Length))})");
        }
        var thoughts = new HashSet<string>(Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories)
            .SelectMany(f => XDocument.Load(f).Root!.Elements("ThoughtDef")).Select(t => t.Element("defName")?.Value ?? ""));
        foreach (var t in council.Descendants("thought").Select(x => x.Value).Distinct())
            C(thoughts.Contains(t), $"the mood {t} exists");
        C(council.Elements("ThoughtDef").All(t => t.Attribute("ParentName")?.Value == "Council_MemoryBase"), "its moods are council memories");

        // Their seams: the game's own ways, thin pieces over them, and nothing saved of their own but a decree's name.
        XElement Q(string name) => dilemmas.First(e => e.Element("defName")?.Value == "Rimfolk_DlmR4_" + name);
        C(dlm.Contains("ThingCategoryDefOf.StoneChunks") && dlm.Contains("IsInAnyStorage()") && Q("Stone").Descendants("li").Any(e => Cls(e) == "Round3CouncilBill")
          && Q("Stone").Descendants("li").Any(e => Cls(e) == "DilemmaEffect_WorkParty" && e.Element("tasks").Elements("li").Any(t => t.Value == "Stone")), "R4C-1: any loose stone; carried in, or a blocks bill");
        C(Q("NightOwl").Descendants("plan").Select(p => p.Value).SequenceEqual(new[] { "apart", "night" }) && dlm.Contains("R4CCouncil.NightOwlDay"), "R4C-2: the night hours for a while, or one night");
        C(dlm.Contains("council.HasDeputy(h)") && dlm.Contains("h.MapHeld != home") && Q("SeatAway").Descendants("li").Any(e => Cls(e) == "DilemmaEffect_Deputy"),
            "R4C-3: a holder away with no deputy in; the deputy that exists stands in");
        C(dlm.Contains("x.def.strainPerDay") && dlm.Contains("d.def.ReactionOf(a) > DecreeReaction.Dislike") && dlm.Contains("class DilemmaEffect_R4CHandOver : DilemmaEffect_HandOver")
          && dlm.Contains("Round4CouncilWorldMath.HandOverDays(R4CCouncil.DaysLeft(active))"), "R4C-4: a hard decree disliked; the work handed over for its days left");
        C(Q("DecreeToll").Descendants("li").Any(e => Cls(e) == "DilemmaEffect_HeartDayOff") && Q("DecreeToll").Element("texts").Elements("li").Any(l => l.Value.Contains("{DECREE}")),
            "R4C-4: the heart's own day off; the decree named");
        C(dlm.Contains("Round4CouncilWorldMath.Master(theirs, ours)") && dlm.Contains("VisitorServices.Stay(x,") && Q("VisitingMaster").Descendants("li").Any(e => Cls(e) == "DilemmaEffect_Lesson" && e.Element("teacher")?.Value == "X"),
            "R4C-5: a lesson from the visitor, who stays on for it");
        C(dlm.Contains("HediffDefOf.Hypothermia") && Q("ColdCell").Descendants("li").Any(e => Cls(e) == "DilemmaEffect_KeepFire" && e.Element("forWhom")?.Value == "X"),
            "R4C-6: the game's own hypothermia; a fire kept in the prisoner's cell");
        C(dlm.Contains("ModsConfig.IdeologyActive") && dlm.Contains("Need_Suppression") && Q("SlaveRest").Descendants("li").Any(e => Cls(e) == "DilemmaEffect_Rest" && e.Element("target")?.Value == "X"),
            "R4C-7: Ideology's slaves; the rest of today for the slave");
        C(dlm.Contains("PawnRelationDefOf.Bond") && dlm.Contains("AnimalPenUtility.NeedsToBeManagedByRope") && dlm.Contains("class DilemmaEffect_R4CPetBed : DilemmaEffect_Made")
          && dlm.Contains("Avail.CanBuild(d, map)") && dlm.Contains("!d.building.bed_humanlike"), "R4C-8: a small bonded animal; the finest animal bed of any mod the colony can build, set in the room");
        C(dlm.Contains("DesignationDefOf.CutPlant") && dlm.Contains("p.def.plant.IsTree") && Q("DryGrass").Descendants("li").Any(e => Cls(e) == "DilemmaEffect_WorkParty" && e.Element("tasks").Elements("li").Any(t => t.Value == "Cut"))
          && Lis(Q("DryGrass"), "seasons").Contains("Summer"), "R4C-9: summer's dry growth marked and cut, never a tree");
        C(dlm.Contains("class DilemmaWorker_R4CGrace : DilemmaWorker_Gods") && dlm.Contains("Worship.Scoffer(p)") && dlm.Contains("Shrines.Of(map, g).Count == 0")
          && Q("Grace").Descendants("li").Any(e => Cls(e) == "DilemmaEffect_ShrineWords"), "R4C-10: a believer with a place for the god and a scoffer; words at the table or at the place");
        C(!Regex.IsMatch(dlm, @"Scribe_Collections|Sched\.") && Regex.Matches(dlm, @"Scribe_Values\.Look").Count == 1 && dlm.Contains("Scribe_Values.Look(ref decree, \"decree\")"),
            "no saved collection or schedule; one saved word, the decree's name");
        C(!Regex.IsMatch(dlm + world, @"\bHarm\.|Injure\(|TakeDamage\("), "nobody is hurt by the round's own code");
        foreach (Match m in Regex.Matches(dlm + world, "\"(CC_R4[CW]_[A-Za-z_]*[A-Za-z])\""))
            C(keyed.Contains("<" + m.Groups[1].Value + ">"), $"{m.Groups[1].Value} has its words");
        C(!Regex.IsMatch(Regex.Replace(keyed, @"<!--.*?-->", "", RegexOptions.Singleline), @"\bin \d+ (days?|hours?)\b|—|!"), "the messages plain, no countdowns");

        // ================= The world events =================

        var wsDoc = XDocument.Load(Path.Combine(root, "Defs", "WorldSituationDefs", "Rimfolk_WS_Round4.xml")).Root!;
        var otherKinds = new HashSet<string>(Directory.GetFiles(Path.Combine(root, "Defs", "WorldSituationDefs"), "*.xml").Where(f => !f.EndsWith("Rimfolk_WS_Round4.xml"))
            .SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.WorldSituationDef")).Select(e => (string)e.Element("kind")).Where(k => k != null));
        var events = new Dictionary<string, string>
        {
            ["Rimfolk_WS_WarmSprings"] = "SituationWorker_R4WSprings", ["Rimfolk_WS_GiftRoad"] = "SituationWorker_R4WGiftRoad", ["Rimfolk_WS_Rafts"] = "SituationWorker_R4WRafts",
            ["Rimfolk_WS_Beacons"] = "SituationWorker_R4WBeacons", ["Rimfolk_WS_ArmsSold"] = "SituationWorker_R4WArms", ["Rimfolk_WS_MissingCaravan"] = "SituationWorker_R4WMissing"
        };
        int troubled = 0;
        foreach (var (e, worker) in events)
        {
            var d = wsDoc.Elements("ColonyCouncil.WorldSituationDef").FirstOrDefault(x => (string)x.Element("defName") == e);
            C(d != null && (string)d.Element("workerClass") == "ColonyCouncil." + worker && world.Contains("class " + worker + " : SituationWorker_Variety"),
                $"{e} is a world event of its people's age ({worker})");
            if (d == null) continue;
            if ((string)d.Element("bad") == "true") troubled++;
            C(Lines(d, "texts").Count >= 4 && Lines(d, "titles").Count >= 2 && Lines(d, "summaries").Count >= 1 && d.Element("endings")?.Elements("li").Count() >= 2,
                $"{e} has its letters, titles, what it means and its endings");
            C(otherKinds.Add((string)d.Element("kind")), $"{e} has a kind of its own, not one of the region's others");
            foreach (var t in VarietyMath.TheirAgeTags)
            {
                C(Lines(d, "names").Any(l => Tags(l).Contains(t)), $"{e} has a name in the {t} age's form");
                C(Lines(d, "texts").Any(l => Tags(l).Contains(t)), $"{e} has a letter in the {t} age's form");
            }
            foreach (var l in Lines(d, "theirs").Concat(Lines(d, "theirEnds")))
                C(WorldHomeMath.ForAPeople(Tags(l)), $"{e}'s own paragraph names a people: {l.Substring(0, Math.Min(40, l.Length))}");
            C(world.Contains("\"" + e + "\""), $"{e} can be started from the dev action");
            // Something comes onto the colony's map: someone at the gate or on the road.
            C(d.Descendants("li").Any(li => Cls(li) == "VisitEffect_AtGate" || Cls(li) == "VisitEffect_Come"), $"{e} brings someone to the colony's map");
            foreach (var l in d.Descendants("li").Where(x => !x.HasElements).Select(x => x.Value))
                C(!Regex.IsMatch(l, @"\bin (\d+|two|three|four|five|six|seven) days\b") && !Regex.IsMatch(l, @"\{GOD\}") && !gods.Any(g => Regex.IsMatch(l, @"\b" + g + @"\b"))
                  && !Regex.Replace(l, @"\[[^\]]*\]", "").Contains('—') && !Regex.Replace(l, @"\[[^\]]*\]", "").Contains('!'),
                    $"{e}: no countdown, god, em dash or exclamation in '{l.Substring(0, Math.Min(40, l.Length))}'");
        }
        C(troubled == 3, $"three troubled world events and three peaceful ({troubled} troubled)");
        XElement W(string name) => wsDoc.Elements("ColonyCouncil.WorldSituationDef").First(x => (string)x.Element("defName") == name);
        // R4W-1.
        var springs = W("Rimfolk_WS_WarmSprings");
        var away = springs.Descendants("li").FirstOrDefault(li => Cls(li) == "WorldEffect_Away");
        C(away != null && away.Element("days")?.Value == ((int)Round4CouncilWorldMath.SpringsDays).ToString() && away.Descendants("li").Any(li => Cls(li) == "DilemmaEffect_EaseAilment")
          && away.Descendants("li").Any(li => Cls(li) == "DilemmaEffect_StrainRelief") && world.Contains("Round4CouncilWorldMath.Aches.Contains"),
            "R4W-1: the one with an ache sent to bathe for three days, home with the ache and the strain eased");
        C(Round4CouncilWorldMath.Aches.All(a => Directory.GetFiles(Path.Combine(root, "Defs", "HediffDefs"), "*.xml").Any(f => File.ReadAllText(f).Contains("<defName>" + a + "</defName>"))),
            "R4W-1: the aches are Rimfolk's own ailments");
        // R4W-2.
        var gift = W("Rimfolk_WS_GiftRoad");
        C(gift.Element("beats").Elements("li").First().Element("options").Elements("li").All(o => o.Descendants("li").Any(li => Cls(li) == "WorldEffect_Loot")),
            "R4W-2: their gift is set down whatever the colony answers");
        // R4W-3.
        C(world.Contains("protected override bool CanHappen(Map map) => River(map);") && world.Contains("Bank(SituationEffectUtil.MapOf(s.ctx)).IsValid")
          && W("Rimfolk_WS_Rafts").Descendants("li").Any(li => Cls(li) == "VisitEffect_Come" && (string)li.Element("spot") == "Water"),
            "R4W-3: only with a river; the raftsmen at the colony's water; a broken raft told only where its logs can wash up");
        // R4W-4.
        var beacons = W("Rimfolk_WS_Beacons");
        C(beacons.Descendants("li").Count(li => Cls(li) == "DilemmaEffect_Bonfire") == 2 && beacons.Element("beats").Elements("li").Any(b => (string)b.Element("key") == "fire" && Lines(b, "requires").Contains("beacon") && (string)b.Element("maxTimes") == "2"),
            "R4W-4: the beacon fire laid tonight on the answer (its risk named there), and on two more evenings");
        // R4W-5.
        C(world.Contains("Round4CouncilWorldMath.ArmsFair(Value(d))") && world.Contains("CompBladelinkWeapon") && world.Contains("tradeability.TraderCanSell()")
          && W("Rimfolk_WS_ArmsSold").Descendants("li").Any(li => Cls(li) == "DilemmaEffect_Spawn" && (string)li.Element("count") == Round4CouncilWorldMath.ArmsCount.ToString()),
            "R4W-5: three weapons of the age from every mod's, a fair price, never a persona's");
        // R4W-6.
        C(world.Contains("Round4CouncilWorldMath.Found(Rand.Value) ? \"willBeFound\" : \"willBeLost\"") && W("Rimfolk_WS_MissingCaravan").Descendants("li").Any(li => Cls(li) == "DilemmaEffect_Search"),
            "R4W-6: whether it is found settled at its start; the colony's land searched");
        C(W("Rimfolk_WS_MissingCaravan").Element("beats").Elements("li").Any(b => (string)b.Element("key") == "found" && Lines(b, "requires").Contains("willBeFound")),
            "R4W-6: found, it passes the colony on the road home");

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
                     ("RecipeDef", "Make_StoneBlocksAny"), ("TraitDef", "NightOwl"), ("ThoughtDef", "NightOwlDuringTheDay"), ("ThingDef", "AnimalBed"), ("ThingDef", "AnimalSleepingBox"),
                     ("ThingDef", "AnimalSleepingSpot"), ("TraitDef", "Industriousness"), ("ThingDef", "MeleeWeapon_Spear"), ("ThingDef", "MeleeWeapon_LongSword"),
                     ("ThingDef", "Gun_BoltActionRifle"), ("ThingDef", "Gun_ChargeRifle")
                 })
        {
            C(active.Contains(type + "|" + name), $"{type} {name} is in the player's active mods");
            C(dlm.Contains("\"" + name + "\"") || world.Contains("\"" + name + "\""), $"{name} read by name, softly");
        }
        C(File.ReadAllText(Path.Combine(root, "Tools", "Tests", "Tests.csproj")).Contains("World/Variety/Round4CouncilWorldMath.cs"), "the pure rules are linked into the tests");
        return n;
    }

    private static List<string> Lis(XElement e, string name) => e?.Element(name)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
}
