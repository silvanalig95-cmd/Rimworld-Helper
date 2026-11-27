using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The content round 4 (docs/content-round-4-camps-events.md; the player's go of 27 November 2026: "just more content to the
/// already existing mechanics"): six camps (Camps/CampRound4.cs, Defs/CampDefs/Rimfolk_CampKinds_Round4.xml) and eight events
/// of everyday life on the map (Happenings/Workers_Round4.cs, Defs/IncidentDefs/Rimfolk_Round4_Events.xml). The rules without
/// game types (Happenings/Round4CampsEventsMath.cs); each camp a kind with ways of its own, in every age's form, its words
/// and the moments it says, its state small; each event a Rimfolk event whose answers play out on the map, its age forms, no
/// god named and no countdown on the card, what it keeps on the map one of OnMapTimed's, every hurt through the shared budget
/// and named before the answer; the mods' defs by name from the player's active mods.
/// </summary>
public static class Round4CampsEventsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "content round 4: " + what);
        }

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        string Cls(XElement e) => ((string)e.Attribute("Class") ?? "").Replace("ColonyCouncil.", "");
        List<string> Lis(XElement e, string name) => e?.Element(name)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
        List<string> TagsOf(string l)
        {
            var m = Regex.Match(l ?? "", @"^\[([^\]]*)\]");
            return m.Success ? m.Groups[1].Value.Split(',').Select(t => t.Trim()).ToList() : new List<string>();
        }
        string Bare(string l) => Regex.Replace(l ?? "", @"^\[[^\]]*\]\s*", "");
        bool OneSentence(string l) => Regex.Matches(Bare(l).TrimEnd('.', '?'), @"[.?!]\s").Count == 0 && !Bare(l).Contains('!');
        var godNames = new[] { "Hollin", "Smoulder", "Bellwether", "Morel", "Ennu", "Pip", "Cirrus", "Tamiu", "Helios", "Selene", "Murmur", "Revel", "Hush", "Ducat", "Undertow" };
        var ages = new[] { "tribal", "medieval", "industrial", "spacer" };

        // ================= The rules =================

        C(Round4CampsEventsMath.CarvingQuality(0f) == 2 && Round4CampsEventsMath.CarvingQuality(0.9f) == 3 && Round4CampsEventsMath.CarvingQuality(0.99f) == 4,
            "a carving of theirs: normal mostly, good now and then, excellent rarely, never poor");
        C(Round4CampsEventsMath.FireWatchFits(30f, false, false) && !Round4CampsEventsMath.FireWatchFits(30f, true, false) && !Round4CampsEventsMath.FireWatchFits(30f, false, true)
          && !Round4CampsEventsMath.FireWatchFits(Round4CampsEventsMath.FireHeatFrom - 1f, false, false), "the fire-watch: hot, dry now, on dry land");
        C(Round4CampsEventsMath.BreakHour(Round4CampsEventsMath.BreakFrom) && !Round4CampsEventsMath.BreakHour(20) && Round4CampsEventsMath.Beaters == 2 && Round4CampsEventsMath.BreakMost <= 60,
            "the firebreak by the working hours, two at a fire, a few dozen plants at most");
        C(Round4CampsEventsMath.Dawn(6) && !Round4CampsEventsMath.Dawn(12) && Round4CampsEventsMath.FlockMin >= 3 && Round4CampsEventsMath.FlockMax <= 6 && Round4CampsEventsMath.BirdsADay == 1,
            "the fowlers: a small flock, one bird at dawn");
        C(Round4CampsEventsMath.SnowFits(0.5f, -3f) && !Round4CampsEventsMath.SnowFits(0.1f, -3f) && !Round4CampsEventsMath.SnowFits(0.5f, 2f) && Round4CampsEventsMath.FiguresMost <= 6,
            "the snow-carvers: deep snow and freezing, a handful of figures");
        C(Round4CampsEventsMath.CheeseOf(0) == 0 && Round4CampsEventsMath.CheeseOf(10) == 5 && Round4CampsEventsMath.CheeseOf(1000) == Round4CampsEventsMath.CheeseMilkMost / 2,
            "a day's cheese: half the milk's weight, at most half the most");
        foreach (var age in ages)
            C(Round4CampsEventsMath.Instruments(age).Length >= 2, $"the instrument-makers of the {age} age have theirs, and a nearer age's after");
        C(Round4CampsEventsMath.Instruments("tribal")[0] == "Drum" && Round4CampsEventsMath.Instruments("medieval")[0] == "Harp" && Round4CampsEventsMath.Instruments("spacer")[0] == "Harpsichord",
            "a drum for a tribe, a harp for the medieval, a harpsichord for spacers");
        C(Round4CampsEventsMath.CrowsFit(true, Round4CampsEventsMath.SeedlingsFrom, true) && !Round4CampsEventsMath.CrowsFit(false, 99, true) && !Round4CampsEventsMath.CrowsFit(true, Round4CampsEventsMath.SeedlingsFrom - 1, true)
          && !Round4CampsEventsMath.CrowsFit(true, 99, false), "crows: spring, a field freshly sown, a bird that can live here");
        C(Round4CampsEventsMath.Peck(0, 0.1f) == 2 && Round4CampsEventsMath.Peck(0, 0.9f) == 1 && Round4CampsEventsMath.Peck(Round4CampsEventsMath.PeckMost, 0.1f) == 0
          && Round4CampsEventsMath.Peck(Round4CampsEventsMath.PeckMost - 1, 0.1f) == 1, "a peck: one or two seedlings, never past the most");
        C(Round4CampsEventsMath.MeltFits(true, 5f, true) && !Round4CampsEventsMath.MeltFits(false, 5f, true) && !Round4CampsEventsMath.MeltFits(true, 0f, true) && !Round4CampsEventsMath.MeltFits(true, 5f, false),
            "meltwater: spring, above freezing, snow on the land to melt");
        C(Round4CampsEventsMath.MidgesFit(true, 20f, 20) && !Round4CampsEventsMath.MidgesFit(false, 20f, 20) && !Round4CampsEventsMath.MidgesFit(true, 10f, 20) && !Round4CampsEventsMath.MidgesFit(true, 20f, 2)
          && Round4CampsEventsMath.Dusk(19) && !Round4CampsEventsMath.Dusk(15), "midges: a warm summer, wet ground by a field, at dusk");
        C(Round4CampsEventsMath.BiteChance > 0f && Round4CampsEventsMath.BiteChance <= 0.1f && Round4CampsEventsMath.SlipChancePerHour > 0f && Round4CampsEventsMath.SlipChancePerHour <= 0.02f,
            "the bites and the slips are small chances, before the budget's scale");
        C(Round4CampsEventsMath.Nearest(0) == 0 && Round4CampsEventsMath.Nearest(1) == 1 && Round4CampsEventsMath.Nearest(7) == 3, "only the nearest: the nearer half, one at least");
        C(Round4CampsEventsMath.IceFits(true, -5f, true, 2) && !Round4CampsEventsMath.IceFits(true, 1f, true, 2) && !Round4CampsEventsMath.IceFits(true, -5f, false, 2) && !Round4CampsEventsMath.IceFits(true, -5f, true, 1)
          && Round4CampsEventsMath.IceGone(1f, 0f) && Round4CampsEventsMath.IceGone(-5f, Round4CampsEventsMath.IceMostHours) && !Round4CampsEventsMath.IceGone(-5f, 2f),
            "ice: winter, freezing after wet weather, two doors or more; gone when it thaws or its time is up");
        C(Round4CampsEventsMath.WoodShort(10, 3) && !Round4CampsEventsMath.WoodShort(500, 3) && !Round4CampsEventsMath.WoodShort(0, 0), "the wood is short by the colony's size");
        C(Round4CampsEventsMath.StonesFit(true, -10f, 60, 20) && !Round4CampsEventsMath.StonesFit(true, 5f, 60, 20) && !Round4CampsEventsMath.StonesFit(false, -10f, 60, 20) && !Round4CampsEventsMath.StonesFit(true, -10f, 20, 20),
            "stones in the field: spring after a freezing winter, a big field with open ground");
        C(Round4CampsEventsMath.WashingFits(20f, false, 9) && !Round4CampsEventsMath.WashingFits(20f, true, 9) && !Round4CampsEventsMath.WashingFits(10f, false, 9) && !Round4CampsEventsMath.WashingFits(20f, false, 15),
            "washing day: a warm, dry morning");

        // ================= The camps =================

        string camps = Code("Camps/CampRound4.cs");
        var campDoc = XDocument.Load(Path.Combine(root, "Defs", "CampDefs", "Rimfolk_CampKinds_Round4.xml")).Root!;
        var kinds = new Dictionary<string, string>
        {
            ["Rimfolk_Camp_Woodcarvers"] = "CampWorker_R4KWoodcarvers", ["Rimfolk_Camp_FireWatchers"] = "CampWorker_R4KFireWatchers", ["Rimfolk_Camp_Wildfowlers"] = "CampWorker_R4KWildfowlers",
            ["Rimfolk_Camp_SnowCarvers"] = "CampWorker_R4KSnowCarvers", ["Rimfolk_Camp_Cheesemakers"] = "CampWorker_R4KCheesemakers", ["Rimfolk_Camp_InstrumentMakers"] = "CampWorker_R4KInstruments"
        };
        var campSeasons = new HashSet<string>();
        foreach (var (k, worker) in kinds)
        {
            var d = campDoc.Elements("ColonyCouncil.CampKindDef").FirstOrDefault(e => (string)e.Element("defName") == k);
            C(d != null && (string)d.Element("workerClass") == "ColonyCouncil." + worker && camps.Contains("class " + worker + " : CampWorker"), $"{k} is a camp kind with ways of its own ({worker})");
            if (d == null) continue;
            foreach (var s in Lis(d, "seasons")) campSeasons.Add(s);
            C(Lis(d, "texts").Count >= 5 && Lis(d, "lines").Count >= 5 && Lis(d, "farewell").Count >= 3 && Lis(d, "settled").Count >= 1 && Lis(d, "joinTexts").Count >= 1 && Lis(d, "titles").Count >= 3,
                $"{k} has its words");
            foreach (var age in ages)
                C(Lis(d, "texts").Any(l => TagsOf(l).Contains("their:" + age)), $"{k} comes in the {age} age's form");
            foreach (var l in Lis(d, "lines").Concat(Lis(d, "momentLines")))
                C(Regex.Matches(Bare(l), @"[.?!]\s+\S").Count == 0, $"{k}'s bubble is one sentence: {l}");
            C(!Lis(d, "texts").Concat(Lis(d, "titles")).Any(l => Regex.IsMatch(l, @"\{(FIELD|INSTRUMENT|BIRDS|DAYS)\}")), $"{k}'s arrival letter uses only the camps' common words and no countdown");
            string words = string.Join(" ", d.Descendants().Where(e => !e.HasElements).Select(e => e.Value));
            C(!godNames.Any(g => Regex.IsMatch(words, @"\b" + g + @"\b")), $"{k} names no god");
            C(camps.Contains("\"" + k + "\""), $"{k} can be brought from the dev action (CampRound4.Kinds)");
            C(d.Element("asks")?.Elements("li").Count() >= 2, $"{k} has asks of its own on its page");
        }
        C(campSeasons.IsSupersetOf(new[] { "Spring", "Summer", "Fall", "Winter" }), "the camps come over every season");
        C(!camps.Contains("Scribe_Collections") && !camps.Contains("Sched." + "Every"), "each camp's state is a few values and single references: no saved collection, no Sched offset");
        C(camps.Contains("[DebugAction(\"Rimfolk\", \"Round 4: one of the new camps here now...\""), "the dev action brings each camp");
        // R4-C1.
        C(camps.Contains("CampRound3.Sit(c, st.sitter, ref st.withFrom, Round4CampsEventsMath.SitHours") && camps.Contains("\"CC_R4K_Carve_Title\"") && camps.Contains("art.Title = title")
          && camps.Contains("MinifyUtility.MakeMinified(t)") && camps.Contains("CampRound4.SetOut(c,"), "R4-C1: once the one sent has sat an hour with the carver, a carving with their name is set out at their camp's edge");
        C(camps.Contains("CampRound3.Refund(c, st.fee, \"CC_R4K_Carve_Refund\""), "R4-C1: never sat down, the fee comes back");
        // R4-C2.
        C(camps.Contains("JobMaker.MakeJob(JobDefOf.BeatFire, f)") && camps.Contains("f.parent == null && !f.Position.Roofed(map)"), "R4-C2: they run to a fire in the open and beat it out (the game's own)");
        C(camps.Contains("p.def.plant.IsTree || p.def.plant.harvestedThingDef != null") && camps.Contains("x.GetZone(map) != null") && camps.Contains("JobMaker.MakeJob(JobDefOf.CutPlant, plant)"),
            "R4-C2: the firebreak cuts dry grass and bushes outside the field, never a tree, a crop or anything in a zone");
        // R4-C3.
        C(camps.Contains("\"AEXP_WildGoose\", \"Goose\", \"Duck\"") && camps.Contains("JobMaker.MakeJob(JobDefOf.Hunt, b)") && camps.Contains("Round4CampsEventsMath.Dawn(hour)"),
            "R4-C3: the flock of the player's mods' wild goose or the game's, one taken at dawn by the game's own hunt");
        // R4-C4.
        C(camps.Contains("GetNamedSilentFail(\"Snowman\")") && camps.Contains("Round4CampsEventsMath.FiguresMost") && campDoc.ToString().Contains("<wearSet>Rimfolk_CampWear_Winter</wearSet>"),
            "R4-C4: the game's own snow figures, a handful in a stay; winter folk in the winter folk's clothes");
        // R4-C5.
        C(camps.Contains("GetNamedSilentFail(\"VCE_Cheese\")") && camps.Contains("c.Own(put ?? made)") && campDoc.ToString().Contains("DilemmaEffect_CampMilkSale"),
            "R4-C5: the milk in their store turns to cheese that stays theirs; the goats' milk sold the camps' own way");
        // R4-C6.
        C(camps.Contains("mi.StartPlaying(pawn)") && camps.Contains("mi.StopPlaying()") && camps.Contains("st.instrument.Destroy(DestroyMode.Vanish)") && camps.Contains("t.SetFaction(c.faction)"),
            "R4-C6: their own instrument by the fire, played with its own music, packed again when they go");
        var facts = Code("Text/Facts/FactsMath.cs");
        C(kinds.Keys.All(k => facts.Contains("\"them:camp:" + k.Replace("Rimfolk_Camp_", "") + "\"")), "the facts service knows the round's camps");
        var almanac = File.ReadAllText(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Camps.xml"));
        C(almanac.Contains("<heading>Woodcarvers, fire-watchers, wildfowlers and more</heading>"), "the camps' Almanac page has the round's section");

        // ================= The events =================

        var events = XDocument.Load(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Round4_Events.xml")).Root!;
        string workers = Code("Happenings/Workers_Round4.cs");
        string timed = Code("PlayOut/OnMapTimed.cs");
        string audit = File.ReadAllText(Path.Combine(root, "Tools", "playout_audit.py"));
        string keyedAll = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        var keyed = Regex.Matches(keyedAll, @"^\s*<([A-Za-z0-9_]+)>(.*?)</\1>\s*$", RegexOptions.Multiline).Cast<Match>()
            .GroupBy(m => m.Groups[1].Value).ToDictionary(g => g.Key, g => g.First().Groups[2].Value);
        var thoughts = new HashSet<string>(Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories)
            .SelectMany(f => XDocument.Load(f).Root!.Elements("ThoughtDef")).Select(e => e.Element("defName")?.Value ?? ""));

        var evNames = new[] { "Crows", "Meltwater", "Midges", "StormShore", "IcyPaths", "OldTree", "FieldStones", "WashingDay" };
        var seasons = new HashSet<string>();
        foreach (var name in evNames)
        {
            var d = events.Elements("IncidentDef").FirstOrDefault(e => e.Element("defName")?.Value == "Rimfolk_Evt_R4E" + name);
            C(d != null, $"{name}: a Rimfolk event");
            if (d == null) continue;
            C(d.Attribute("ParentName")?.Value == "Council_EventBase", $"{name}: in the Rimfolk events' pool");
            string worker = (d.Element("workerClass")?.Value ?? "").Replace("ColonyCouncil.", "");
            C(worker == "HappeningWorker_R4E" + name && workers.Contains("class " + worker + " : IncidentWorker_Happening"), $"{name}: its worker in Workers_Round4.cs");
            C(workers.Contains("\"Rimfolk_Evt_R4E" + name + "\""), $"{name}: in the dev action's list");
            var ext = d.Element("modExtensions")?.Elements("li").FirstOrDefault(e => Cls(e) == "HappeningExtension");
            C(ext != null, $"{name}: a happening");
            if (ext == null) continue;
            foreach (var s in Lis(ext, "seasons")) seasons.Add(s);
            var options = ext.Element("options")?.Elements("li").ToList() ?? new List<XElement>();
            C(options.Count == 3, $"{name}: three answers");
            int played = options.Count(o => (o.Element("effects")?.Elements("li") ?? Enumerable.Empty<XElement>()).Any(e => audit.Contains("\"" + Cls(e) + "\"")));
            C(played >= 2, $"{name}: its answers play out on the map ({played} of 3)");
            int def = int.TryParse(ext.Element("defaultOption")?.Value, out var dv) ? dv : 0;
            C(def >= 0 && def < options.Count, $"{name}: its default answer is one of them");
            foreach (var e in options.SelectMany(o => o.Element("effects")?.Elements("li") ?? Enumerable.Empty<XElement>()).Where(e => Cls(e).StartsWith("DilemmaEffect_R4E")))
                C(workers.Contains("class " + Cls(e) + " : DilemmaEffect") && audit.Contains("\"" + Cls(e) + "\""), $"{name}: {Cls(e)} in Workers_Round4.cs, and a play-out the audit knows");
            var texts = Lis(ext, "texts");
            C(texts.Count >= 3, $"{name}: three wordings or more");
            C(texts.Any(t => Regex.IsMatch(t, @"^\[!?(lowtech|tribal|medieval|industrial|spacer)")) || options.Any(o => Lis(o, "outcomes").Any(t => Regex.IsMatch(t, @"^\[(tribal|medieval|lowtech|industrial|spacer)"))),
                $"{name}: its age forms in the words");
            string words = string.Join(" ", texts.Concat(Lis(ext, "titles")).Concat(options.SelectMany(o => new[] { o.Element("label")?.Value ?? "", o.Element("outcome")?.Value ?? "" }.Concat(Lis(o, "outcomes")))));
            C(!godNames.Any(g => Regex.IsMatch(words, @"\b" + g + @"\b")), $"{name}: no god named on the card");
            C(!Regex.IsMatch(words, @"\b[Ii]n (\d+|a|an|two|three|four|five) (days?|hours?)\b") && !Regex.IsMatch(words, @"\d+ (days?|hours?)\b") && !Regex.IsMatch(words, @"\bday or two\b"),
                $"{name}: no countdown in the words");
            string bare = Regex.Replace(words, @"\[[^\]]*\]", "");
            C(!bare.Contains('—') && !bare.Contains('!'), $"{name}: plain words, no em dashes or exclamations on the card");
            var lines = Lis(ext, "lines");
            C(lines.Count >= 2 && lines.All(l => Regex.IsMatch(string.Join(",", TagsOf(l)), @"^[a-z]+(,!?[a-z]+)?$")), $"{name}: its spoken lines tagged with their moment");
            C(lines.All(OneSentence), $"{name}: each spoken line one sentence");
        }
        var allLines = events.Descendants("lines").SelectMany(l => l.Elements("li")).Select(l => l.Value).ToList();
        foreach (var moment in Regex.Matches(workers, @"(?:MapDangers|Round3Map)\.Say\([^;]*?""([a-z]+)""\);").Cast<Match>().Select(m => m.Groups[1].Value).Distinct())
            C(allLines.Any(l => TagsOf(l).Contains(moment)), $"the moment '{moment}' has its lines");
        C(seasons.IsSupersetOf(new[] { "Spring", "Summer", "Fall", "Winter" }), "the events spread over every season");

        // What each keeps on the map, and its steps.
        var tk = new[] { "R4ECrows", "R4EMelt", "R4EMidges", "R4EShore", "R4EIce" };
        int dinner = timed.IndexOf("        R3Dinner,", StringComparison.Ordinal);
        C(dinner > 0 && tk.All(k => timed.IndexOf("        " + k, StringComparison.Ordinal) > dinner), "what they keep on the map: OnMapTimed's kinds, after round 3's last");
        C(tk.All(k => Regex.IsMatch(timed, @"case TimedKind\." + k + @":[\s\S]{0,260}return R4EMap\.Step") && Regex.IsMatch(timed, @"case TimedKind\." + k + @":[\s\S]{0,260}R4EMap\.End")),
            "each stepped and ended by R4EMap");
        C(!workers.Contains("Scribe_Collections") && !workers.Contains("Sched." + "Every"), "no new saved collection and no Sched offset of their own");

        // Harm: only through the shared budget, named before the answer.
        C(workers.Contains("Harm.Roll(p, HarmKinds.LandAilments, Round4CampsEventsMath.BiteChance, HurtClass.Mild)") && workers.Contains("HarmFour.Def(\"Council_Ail_InsectBites\")"),
            "the midges' bites: an ailment of the land through the shared budget, told at once");
        C(workers.Contains("Harm.Roll(p, HarmKinds.Dangers, chance, HurtClass.Mild)") && workers.Contains("HarmFour.Def(\"Council_Ail_SprainedAnkle\")") && workers.Contains("k.nights >= 2"),
            "a slip on the ice: a sprained ankle through the shared budget (the dangers on the map), twice at most");
        C(workers.Contains("\"CC_R4E_Impact_MidgesRisk\"") && workers.Contains("\"CC_R4E_Impact_IceRisk\"") && workers.Contains("\"CC_R4E_Impact_IceRiskSome\""), "every risk named under its answer");
        C(!Regex.IsMatch(workers + camps, @"new HarmKind\("), "no new kind of hurt");

        // The play-outs as the words say.
        C(workers.Contains("DesignationDefOf.Hunt") && workers.Contains("Round2Map.Leave(x, urgency)") && workers.Contains("p.Destroy(DestroyMode.Vanish)"),
            "the crows: marked for the hunters, chased or let go off the land; the seedlings they pull up gone from the field");
        C(workers.Contains("FilthMaker.TryMakeFilth(c, map, water)") && workers.Contains("d.SetForbidden(true, false)") && workers.Contains("door.SetForbidden(false, false)"),
            "the meltwater: the game's own puddles by the door, the door shut until it stops and opened again");
        C(workers.Contains("TimedKind.Smudge") && workers.Contains("Round2Map.GoTo(p, Round2Map.Home(map, p))"), "the midges: no bites by the smudge fires, or walked in before dusk");
        C(workers.Contains("\"VCEF_RawLobster\"") && workers.Contains("\"VCE_RawNoriSeaweed\"") && workers.Contains("put.SetForbidden(true, false)") && workers.Contains("FleckMaker.WaterSplash"),
            "the shore: the player's mods' catch held back until the answer, and what is left taken back by the sea");
        C(workers.Contains("FleckMaker.ThrowDustPuff(c, map, 0.6f)") && workers.Contains("p.pather.Moving"), "the ice: ash scattered door to door, and only those walking out may slip");
        C(workers.Contains("DesignationDefOf.CutPlant") && workers.Contains("GetNamedSilentFail(\"Stool\")") && workers.Contains("GenConstruct.PlaceBlueprintForBuild(seat"),
            "the old tree: marked for cutting, or a stool marked under it");
        C(workers.Contains("rock?.building?.mineableThing") && workers.Contains("r.MakeNewBill()") && workers.Contains("VarietyThree.First(bench, bill)"),
            "the stones: the land's own rock, carried off or cut into blocks by a bill at the colony's bench");
        var washing = events.Elements("IncidentDef").FirstOrDefault(e => e.Element("defName")?.Value == "Rimfolk_Evt_R4EWashingDay");
        C(washing?.ToString().Contains("<li>Water</li>") == true && washing.ToString().Contains("<to>Water</to>"), "washing day: a gathering at the water, or a walk down to it");
        var tree = events.Elements("IncidentDef").FirstOrDefault(e => e.Element("defName")?.Value == "Rimfolk_Evt_R4EOldTree");
        var treeExt = tree?.Element("modExtensions")?.Elements("li").FirstOrDefault(e => Cls(e) == "HappeningExtension");
        C(treeExt?.Element("effects")?.Elements("li").Any(e => Cls(e) == "DilemmaEffect_Visit" && (string)e.Element("to") == "Place") == true,
            "the old tree: the one fond of it walks out to stand under it as the card comes");

        // Words and moods.
        foreach (var key in Regex.Matches(workers + camps, @"""(CC_R4[EK]_[A-Za-z_]*[A-Za-z])""").Cast<Match>().Select(m => m.Groups[1].Value).Distinct())
            C(keyed.ContainsKey(key), $"its words: {key}");
        foreach (var mode in new[] { "mop", "shut", "leave" })
            C(keyed.ContainsKey("CC_R4E_Impact_Melt_" + mode) && keyed.ContainsKey("CC_R4E_Impact_Melt_" + mode + "Brief"), $"the meltwater's answer: {mode}");
        foreach (var t in Regex.Matches(workers + camps, @"""(Rimfolk_R4[EK]_[A-Za-z]+)""").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().Where(t => !t.Contains("Camp")))
            C(thoughts.Contains(t), $"its mood: {t}");
        var keyedFile = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Round4_CampsEvents.xml"));
        C(!Regex.Replace(keyedFile, "<!--[\\s\\S]*?-->", "").Contains('—'), "no em dashes in the round's words");

        // The mods' defs by name, all from the player's active mods (or the game and its DLCs).
        var pool = new HashSet<string>();
        using (var gz = new GZipStream(File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz")), CompressionMode.Decompress))
        using (var r = new StreamReader(gz))
        {
            r.ReadLine();
            string line;
            while ((line = r.ReadLine()) != null)
            {
                var f = line.Split('\t');
                if (f.Length >= 4 && f[3] == "yes") pool.Add(f[0] + ":" + f[1]);
            }
        }
        foreach (var t in new[] { "AEXP_WildGoose", "Goose", "Duck", "Crow", "VCEF_RawLobster", "VCE_RawNoriSeaweed", "VCE_Cheese", "Joy_Piano", "Drum", "Harp", "Harpsichord", "Piano", "Snowman", "SculptureSmall", "Stool", "Filth_Water" })
        {
            bool isAnimal = t == "AEXP_WildGoose" || t == "Goose" || t == "Duck" || t == "Crow";
            C(pool.Contains("ThingDef:" + t) && (!isAnimal || pool.Contains("PawnKindDef:" + t)), $"the def {t} is in the player's active mods or the game");
        }
        foreach (var ail in new[] { "Council_Ail_InsectBites", "Council_Ail_SprainedAnkle" })
            C(Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Any(f => File.ReadAllText(f).Contains("<defName>" + ail + "</defName>")), $"the ailment {ail} is Rimfolk's own");
        return n;
    }
}
