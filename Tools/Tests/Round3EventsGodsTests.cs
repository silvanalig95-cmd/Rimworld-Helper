using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The content round 3 (docs/content-round-3-events-gods.md; the player's go of 27 November 2026: "just more content to the
/// already existing mechanics"): eight events of everyday life on the map and six happenings of the gods on the land. The
/// rules without game types (Happenings/Round3EventsMath.cs); each event a Rimfolk event whose answers play out on the map,
/// its age forms, no god named and no countdown on the card, what it keeps on the map one of OnMapTimed's, every hurt through
/// the shared budget and named before the answer; the six after every older happening of the gods, each its own god's, on the
/// gods' pace, its card named only where the colony is sure, its words unnamed and its book line, a dev action each, its
/// state saved and small; the mods' defs by name from the player's active mods.
/// </summary>
public static class Round3EventsGodsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "content round 3: " + what);
        }

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        string Cls(XElement e) => ((string)e.Attribute("Class") ?? "").Replace("ColonyCouncil.", "");
        List<string> Lis(XElement e, string name) => e?.Element(name)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
        string Tags(string l) => Regex.Match(l, @"^\[([^\]]*)\]").Groups[1].Value;
        string Bare(string l) => Regex.Replace(l, @"^\[[^\]]*\]\s*", "");
        var T = GodPaceMath.Trigger.None;
        var O = GodPaceMath.Trigger.Ordinary;
        var S = GodPaceMath.Trigger.Strong;
        var godNames = new[] { "Hollin", "Smoulder", "Bellwether", "Morel", "Ennu", "Pip", "Cirrus", "Tamiu", "Helios", "Selene", "Murmur", "Revel", "Hush", "Ducat", "Undertow" };

        // ================= The events' rules =================

        C(Round3EventsMath.MastFits(true, Round3EventsMath.TreesFrom, true) && !Round3EventsMath.MastFits(false, 99, true) && !Round3EventsMath.MastFits(true, Round3EventsMath.TreesFrom - 1, true)
          && !Round3EventsMath.MastFits(true, 99, false), "the mast year: autumn, many trees, a boar that can live here");
        C(Round3EventsMath.BoarMin >= 2 && Round3EventsMath.BoarMax <= 6 && Round3EventsMath.BoarTaken == 2 && Round3EventsMath.SealsTaken == 2, "a small group comes; the hunters take two");
        C(Round3EventsMath.SnakeGoes(Round3EventsMath.SnakeDusk) && Round3EventsMath.SnakeGoes(23) && Round3EventsMath.SnakeGoes(2) && !Round3EventsMath.SnakeGoes(12) && !Round3EventsMath.SnakeGoes(17),
            "a snake left alone moves off at dusk");
        C(Round3EventsMath.BiteChance > 0f && Round3EventsMath.BiteChance <= 0.1f && Round3EventsMath.BiteHigh <= 6f, "lifted on a stick, a small chance of a small bite, before the budget's scale");
        C(Round3EventsMath.RoofHeavy(true, Round3EventsMath.RoomCellsFrom, Round3EventsMath.DeepSnow) && !Round3EventsMath.RoofHeavy(false, 99, 1f)
          && !Round3EventsMath.RoofHeavy(true, Round3EventsMath.RoomCellsFrom - 1, 1f) && !Round3EventsMath.RoofHeavy(true, 99, Round3EventsMath.DeepSnow - 0.1f),
            "a roof heavy with snow: a big room under a built roof, deep snow round it");
        C(Round3EventsMath.CollapseMost <= 3 && Round3EventsMath.CollapseAfterMin >= 3f && Round3EventsMath.CollapseAfterMax <= 16f, "left, the roof gives over a few cells later that day");
        C(Round3EventsMath.PathFits(Round3EventsMath.WetFrom, 5f) && !Round3EventsMath.PathFits(Round3EventsMath.WetFrom - 1, 5f) && !Round3EventsMath.PathFits(Round3EventsMath.WetMost + 1, 5f)
          && !Round3EventsMath.PathFits(10, -2f), "the way across the marsh: enough wet ground, a short way, not frozen");
        C(Round3EventsMath.Worst(10) == 5 && Round3EventsMath.Worst(5) == 3 && Round3EventsMath.Worst(1) == 1 && Round3EventsMath.Worst(0) == 0, "the worst of it: the wettest half, two at least");
        C(Round3EventsMath.ColdRoomFits(20f, 6f, 10) && !Round3EventsMath.ColdRoomFits(12f, 4f, 10) && !Round3EventsMath.ColdRoomFits(20f, 12f, 10) && !Round3EventsMath.ColdRoomFits(20f, 6f, 3),
            "the cold room: the food warm where it is, the deep room cold, room for a store");
        C(Round3EventsMath.DinnerFits(10, 2, 40f, 2) && !Round3EventsMath.DinnerFits(13, 2, 40f, 2) && !Round3EventsMath.DinnerFits(10, 1, 40f, 2) && !Round3EventsMath.DinnerFits(10, 3, 10f, 3)
          && !Round3EventsMath.DinnerFits(10, 3, 40f, 2), "dinner in the field: the late morning, two or more far out, a meal each");
        C(Round3EventsMath.Worn(49, 100) && !Round3EventsMath.Worn(50, 100) && !Round3EventsMath.Worn(1, 0) && Round3EventsMath.Spare(80, 100) && !Round3EventsMath.Spare(79, 100),
            "worn: the game's own tattered line; a spare in the stores in good shape");
        C(Round3EventsMath.BillCount(0) == 1 && Round3EventsMath.BillCount(3) == 3 && Round3EventsMath.BillCount(9) == Round3EventsMath.BillMost, "one garment each, four at most");

        // ================= The events =================

        var events = XDocument.Load(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Round3_Events.xml")).Root!;
        string workers = Code("Happenings/Workers_Round3.cs");
        string timed = Code("PlayOut/OnMapTimed.cs");
        string audit = File.ReadAllText(Path.Combine(root, "Tools", "playout_audit.py"));
        string keyedAll = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        var keyed = Regex.Matches(keyedAll, @"^\s*<([A-Za-z0-9_]+)>(.*?)</\1>\s*$", RegexOptions.Multiline).Cast<Match>()
            .GroupBy(m => m.Groups[1].Value).ToDictionary(g => g.Key, g => g.First().Groups[2].Value);
        var thoughts = new HashSet<string>(Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories)
            .SelectMany(f => XDocument.Load(f).Root!.Elements("ThoughtDef")).Select(e => e.Element("defName")?.Value ?? ""));

        var evNames = new[] { "MastYear", "SnakeWoodpile", "RoofSnow", "MarshPath", "ColdRoom", "Seals", "FieldDinner", "WornClothes" };
        var seasons = new HashSet<string>();
        foreach (var name in evNames)
        {
            var d = events.Elements("IncidentDef").FirstOrDefault(e => e.Element("defName")?.Value == "Rimfolk_Evt_R3" + name);
            C(d != null, $"{name}: a Rimfolk event");
            if (d == null) continue;
            C(d.Attribute("ParentName")?.Value == "Council_EventBase", $"{name}: in the Rimfolk events' pool");
            string worker = (d.Element("workerClass")?.Value ?? "").Replace("ColonyCouncil.", "");
            C(worker == "HappeningWorker_R3" + name && workers.Contains("class " + worker + " : IncidentWorker_Happening"), $"{name}: its worker in Workers_Round3.cs");
            var ext = d.Element("modExtensions")?.Elements("li").FirstOrDefault(e => Cls(e) == "HappeningExtension");
            C(ext != null, $"{name}: a happening");
            if (ext == null) continue;
            foreach (var s in Lis(ext, "seasons")) seasons.Add(s);
            var options = ext.Element("options")?.Elements("li").ToList() ?? new List<XElement>();
            C(options.Count == 3, $"{name}: three answers");
            int played = options.Count(o => (o.Element("effects")?.Elements("li") ?? Enumerable.Empty<XElement>()).Any(e => audit.Contains("\"" + Cls(e) + "\"")));
            C(played >= 2, $"{name}: its answers play out on the map ({played} of 3)");
            foreach (var e in options.SelectMany(o => o.Element("effects")?.Elements("li") ?? Enumerable.Empty<XElement>()).Where(e => Cls(e).StartsWith("DilemmaEffect_R3")))
                C(workers.Contains("class " + Cls(e) + " : DilemmaEffect"), $"{name}: {Cls(e)} in Workers_Round3.cs");
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
            C(lines.Count >= 2 && lines.All(l => Regex.IsMatch(Tags(l), @"^[a-z]+(,!?[a-z]+)?$")), $"{name}: its spoken lines tagged with their moment");
            C(lines.All(l => Regex.Matches(Bare(l).TrimEnd('.', '?'), @"[.?!]\s").Count == 0 && !Bare(l).Contains('!')), $"{name}: each spoken line one sentence");
        }
        // Every moment the code speaks at has a line in one of the events.
        var allLines = events.Descendants("lines").SelectMany(l => l.Elements("li")).Select(l => l.Value).ToList();
        foreach (var moment in Regex.Matches(workers, @"(?:MapDangers|Round3Map)\.Say\([^;]*?""([a-z]+)""\);").Cast<Match>().Select(m => m.Groups[1].Value).Distinct())
            C(allLines.Any(l => Tags(l).Split(',').Contains(moment)), $"the moment '{moment}' has its lines");
        C(seasons.IsSupersetOf(new[] { "Spring", "Summer", "Fall", "Winter" }), "spread over every season");

        // What each keeps on the map, and its steps.
        var kinds = new[] { "R3Boar", "R3Snake", "R3Roof", "R3Seals", "R3Dinner" };
        int ordeal = timed.IndexOf("        Ordeal,", StringComparison.Ordinal);
        C(ordeal > 0 && kinds.All(k => timed.IndexOf("        " + k, StringComparison.Ordinal) > ordeal), "what they keep on the map: OnMapTimed's kinds, after round 2's last");
        C(kinds.All(k => Regex.IsMatch(timed, @"case TimedKind\." + k + @":[\s\S]{0,200}return Round3Map\.Step") && Regex.IsMatch(timed, @"case TimedKind\." + k + @":[\s\S]{0,200}Round3Map\.End")),
            "each stepped and ended by Round3Map");
        C(!workers.Contains("Scribe_Collections") && !workers.Contains("Sched."  + "Every"), "no new saved collection and no Sched offset of their own");

        // Harm: only through the shared budget, named before the answer, at the game's own rate otherwise.
        C(workers.Contains("Harm.Roll(h, HarmKinds.Dangers, Round3EventsMath.BiteChance)") && workers.Contains("MapDangers.Wound(h, HurtKind.Bite"),
            "the snake's bite on the stick through the shared budget (the dangers on the map), a small one, told at once");
        C(workers.Contains("\"CC_R3_Impact_LiftRisk\"") && workers.Contains("\"CC_R3_Impact_HuntRisk\"") && workers.Contains("\"CC_R3_Impact_SnakeRisk\"") && workers.Contains("\"CC_R3_Impact_RoofLeave\""),
            "every hurt and risk named under its answer: the bite, a hunted animal turning, the roof giving");
        C(workers.Contains("PawnUtility.GetManhunterOnDamageChance"), "a hunted animal's turn is the game's own chance, named where the kind has it");
        C(workers.Contains("c.GetFirstPawn(map) == null && c.GetThingList(map).All(t => t is Filth)") && workers.Contains("RoofCollapserImmediate.DropRoofInCells(cells, map)"),
            "the roof comes down only over cells with nobody and nothing under them (the game's own collapse)");
        C(!Regex.IsMatch(workers, @"new HarmKind\("), "no new kind of hurt");

        // The play-outs as the words say.
        C(workers.Contains("DesignationDefOf.Hunt") && workers.Contains("Round2Map.Leave(x, urgency)") && workers.Contains("LocomotionUrgency.Jog"),
            "the boar and the seals: marked for the hunters, or run off the land when driven, the rest walk off when their time is up");
        C(workers.Contains("t.SetForbidden(true, false)") && workers.Contains("Round3EventsMath.SnakeGoes(GenLocalDate.HourOfDay(map))") && workers.Contains("Round2Map.GoTo(x, to)"),
            "the snake: the woodpile forbidden until dusk, or walked off ahead of the handler and let go");
        C(workers.Contains("map.snowGrid.AddDepth(n, 0.03f)") && workers.Contains("GenConstruct.PlaceBlueprintForBuild(Column, cell, map"), "the roof: the snow thrown down along the walls, or a column marked inside");
        C(workers.Contains("GetNamedSilentFail(\"Bridge\")") && workers.Contains("GenConstruct.PlaceBlueprintForBuild(bridge, c, map"), "the marsh: the game's own bridge marked over the wet steps");
        C(workers.Contains("new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager)") && workers.Contains("StoragePriority.Important")
          && workers.Contains("r.Cells.Any(c => !(map.roofGrid.RoofAt(c)?.isThickRoof ?? false))"), "the cold room: a store marked under thick rock, ahead of the others, so the haulers carry the food down");
        C(workers.Contains("OwnHappenings.Carry(cook, stack, k.cell, n)") && workers.Contains("JobMaker.MakeJob(JobDefOf.Ingest, meal)") && workers.Contains("p.needs.food.CurLevel"),
            "dinner: carried out to the field's edge and eaten there, or eaten at the table");
        C(workers.Contains("recipe.MakeNewBill()") && workers.Contains("JobMaker.MakeJob(JobDefOf.Wear, spare)") && workers.Contains("p.outfits?.CurrentApparelPolicy?.filter?.Allows(t)"),
            "clothes: a bill at the colony's own bench (any mod's recipe it lists), or spares from the stores the outfit allows");
        foreach (var key in Regex.Matches(workers, @"""(CC_R3_[A-Za-z_]*[A-Za-z])""").Cast<Match>().Select(m => m.Groups[1].Value).Distinct())
            C(keyed.ContainsKey(key), $"its words: {key}");
        foreach (var mode in new[] { "Hunt", "Drive", "Let_boar", "Let_seals", "Lift", "Shovel", "Prop", "Marsh_all", "Marsh_worst", "Cold_all", "Cold_meat", "Dinner_carry", "Dinner_table" })
            C(keyed.ContainsKey("CC_R3_Impact_" + mode), $"its answer's line: CC_R3_Impact_{mode}");
        foreach (var t in Regex.Matches(workers, @"""(Rimfolk_R3_[A-Za-z]+)""").Cast<Match>().Select(m => m.Groups[1].Value).Distinct())
            C(thoughts.Contains(t), $"its memory: {t}");
        C(keyed.Where(kv => kv.Key.StartsWith("CC_R3_")).All(kv => !kv.Value.Contains('!') && !kv.Value.Contains('—') && !godNames.Any(g => Regex.IsMatch(kv.Value, @"\b" + g + @"\b"))),
            "its messages in plain words, no god named");

        // ================= The gods' six on the land =================

        var six = OwnHappeningsMath.Land3;
        C(six.Length == 6 && six.SequenceEqual(new[] { OwnHappening.BellwetherNewborn, OwnHappening.HeliosRipen, OwnHappening.MorelFoxfire, OwnHappening.PipFinished, OwnHappening.SeleneTide, OwnHappening.CirrusGlory }),
            "six happenings on the land");
        C(six.All(h => (int)h > (int)OwnHappening.FaithLightning) && (int)six.Last() == OwnHappeningsMath.All.Length - 1 && OwnHappeningsMath.FirstLand3 == six[0],
            "added after every older one, so a save's list of when each last came keeps its order");
        C(six.All(OwnHappeningsMath.IsLand3) && !six.Any(OwnHappeningsMath.IsBetween) && !OwnHappeningsMath.IsLand3(OwnHappening.FaithLightning) && OwnHappeningsMath.IsBetween(OwnHappening.FaithLightning),
            "on the land, none of them between two people");
        C(six.Select(OwnHappeningsMath.GodOf).SequenceEqual(new[] { "Bellwether", "Helios", "Morel", "Pip", "Selene", "Cirrus" }) && !six.Any(OwnHappeningsMath.OfAnyGod),
            "each its own god's (the white beast, the sun, the rot, the Hum, the moon, the sky)");
        C(OwnHappeningsMath.All.Select(OwnHappeningsMath.Name).Distinct().Count() == OwnHappeningsMath.All.Length, "each its own name among all the gods' happenings");
        foreach (var g in new[] { "Bellwether", "Helios", "Morel", "Pip", "Selene", "Cirrus" })
            C(OwnHappeningsMath.Of(g).Count(h => !OwnHappeningsMath.IsBetween(h)) <= 5, g + ": five on the land at most");
        foreach (var h in six)
        {
            var p = OwnHappeningsMath.PaceOf(h);
            C(p == OwnHappeningsMath.Land3PaceOf(h) && p.gap >= 25f, h + ": its own pace, far apart (a gap of " + p.gap + " days)");
            C(OwnHappeningsMath.DayChance(h, 999f, T, 1f) == 0f, h + ": nothing at all without a reason, however long since and however near its god");
            if (p.mean <= 0f) continue;
            C(OwnHappeningsMath.DayChance(h, p.gap - 0.5f, S, 1f) == 0f, h + ": never within its gap");
            float first = OwnHappeningsMath.DayChance(h, p.gap, O, 0f), later = OwnHappeningsMath.DayChance(h, p.gap + p.ramp, O, 0f);
            C(first > 0f && first < later && OwnHappeningsMath.DayChance(h, p.gap + p.ramp, S, 0f) >= later && OwnHappeningsMath.DayChance(h, p.gap + p.ramp, O, 1f) >= later,
                h + ": least likely right after its gap, more over its ramp, a little more for a strong reason or a near god");
        }
        C(OwnHappeningsMath.PaceOf(OwnHappening.BellwetherNewborn).mean <= 0f && OwnHappeningsMath.NewbornChance(10f, 0f) == 0f && OwnHappeningsMath.NewbornChance(50f, 0f) > 0.4f
          && OwnHappeningsMath.NewbornChance(50f, 1f) > OwnHappeningsMath.NewbornChance(50f, 0f) && OwnHappeningsMath.NewbornChance(50f, 1f) <= 1f,
            "the ring in the snow follows a birth only, at its own chance past its gap, a little more the nearer the white beast is");
        C(OwnHappeningsMath.NewbornFits(5f, true, -5f, 0.5f) && !OwnHappeningsMath.NewbornFits(20f, true, -5f, 0.5f) && !OwnHappeningsMath.NewbornFits(5f, false, -5f, 0.5f)
          && !OwnHappeningsMath.NewbornFits(5f, true, 2f, 0.5f) && !OwnHappeningsMath.NewbornFits(5f, true, -5f, 0f), "born in the night, out of doors, in a frost, with snow round it");
        C(OwnHappeningsMath.RipenTrigger(true, true, 32f, 0.85f, false, 0) == O && OwnHappeningsMath.RipenTrigger(true, true, 32f, 0.85f, false, 2) == S
          && OwnHappeningsMath.RipenTrigger(false, true, 32f, 0.85f, false, 2) == T && OwnHappeningsMath.RipenTrigger(true, false, 32f, 0.85f, false, 2) == T
          && OwnHappeningsMath.RipenTrigger(true, true, 25f, 0.85f, false, 2) == T && OwnHappeningsMath.RipenTrigger(true, true, 32f, 0.6f, false, 2) == T
          && OwnHappeningsMath.RipenTrigger(true, true, 32f, 0.85f, true, 2) == T, "ripe at noon: a hot clear summer noon, a field nearly ripe and none of it ripe yet");
        C(OwnHappeningsMath.FoxfireTrigger(2, 10f, true, false) == O && OwnHappeningsMath.FoxfireTrigger(2, 10f, true, true) == S && OwnHappeningsMath.FoxfireTrigger(1, 10f, true, true) == T
          && OwnHappeningsMath.FoxfireTrigger(3, 0f, true, true) == T && OwnHappeningsMath.FoxfireTrigger(3, 10f, false, true) == T && OwnHappeningsMath.FoxfireHour(22) && OwnHappeningsMath.FoxfireHour(1)
          && !OwnHappeningsMath.FoxfireHour(12), "the light on the wet ground: at night after wet days, not freezing, strongly where something rots");
        C(OwnHappeningsMath.FinishedTrigger(true, true, false) == O && OwnHappeningsMath.FinishedTrigger(true, true, true) == S && OwnHappeningsMath.FinishedTrigger(true, false, true) == T
          && OwnHappeningsMath.FinishedTrigger(false, true, true) == T && Math.Abs(OwnHappeningsMath.FinishedWork(1000f) - 100f) < 0.01f && OwnHappeningsMath.FinishedWork(5f) >= 1f
          && OwnHappeningsMath.FinishedWork(0f) == 0f, "done in the night: a piece at a bench with nobody at it; a tenth of the work left stays");
        C(OwnHappeningsMath.TideTrigger(true, true, OwnHappeningsMath.TideCellsFrom) == O && OwnHappeningsMath.TideTrigger(false, true, 40) == T && OwnHappeningsMath.TideTrigger(true, false, 40) == T
          && OwnHappeningsMath.TideTrigger(true, true, OwnHappeningsMath.TideCellsFrom - 1) == T && OwnHappeningsMath.TideHour(22) && !OwnHappeningsMath.TideHour(4) && OwnHappeningsMath.TideCellsMost <= 40,
            "the tide: a full moon on the coast with empty shore, a few dozen cells at most");
        C(OwnHappeningsMath.GloryTrigger(true, true, true, false) == O && OwnHappeningsMath.GloryTrigger(true, true, true, true) == S && OwnHappeningsMath.GloryTrigger(false, true, true, true) == T
          && OwnHappeningsMath.GloryTrigger(true, false, true, true) == T && OwnHappeningsMath.GloryTrigger(true, true, false, true) == T, "the giant in the mist: a foggy morning on high ground, someone out");

        string gods = Code("Gods/OnMap/OwnRound3.cs");
        string seven = Code("Gods/OnMap/OwnSeven.cs");
        string frame = Code("Gods/OnMap/OwnHappenings.cs");
        string touch = Code("Gods/Touch/GodTouchMath.cs");
        string dbg = Code("Gods/OwnRound3Debug.cs");
        C(seven.Contains("OwnRound3.Hourly(b, map, now)") && seven.Contains("OwnRound3.Step(b, now)") && frame.Contains("ExposeRound3();"),
            "looked at in the gods' hour and steps, saved with the gods' book");
        foreach (var h in six)
            C(gods.Contains("OwnHappening." + h) && Regex.IsMatch(gods, @"OwnHappenings\.Came\(b, OwnHappening\." + h), h + ": comes in the code and is noted when it does");
        C(Regex.Matches(gods, @"OwnHappenings\.Calls\(b, OwnHappening\.").Count == 5 && gods.Contains("OwnHappenings.Acts(g, map)") && gods.Contains("GodTouch.Turn(g, map)"),
            "only where its god acts at the home, on the gods' pace, giving the other gods their turn");
        C(!gods.Contains("Harm.") && !gods.Contains("HarmKinds") && !gods.Contains("Messages.Message"), "none of them hurts anyone; the map's code speaks through the teller and the frame only");
        C(gods.Contains("map.snowGrid.SetDepth(c, 0f)") && gods.Contains("HediffDefOf.Hypothermia"), "the ring: the snow cleared in a ring, the newborn warm");
        C(gods.Contains("p.Growth = 1f;") && gods.Contains("FleckMaker.ThrowHeatGlow"), "ripe at noon: the field ripe at once, the heat shimmering");
        C(gods.Contains("GetNamedSilentFail(\"SoilRich\")") && gods.Contains("OwnHappeningsMath.FoxfireSoilMost"), "the light: the soil rich by morning, a few cells");
        C(gods.Contains("piece.workLeft = OwnHappeningsMath.FinishedWork(piece.workLeft)") && gods.Contains("FleckMaker.ThrowMicroSparks"), "done in the night: sparks at the bench, the work left cut");
        C(gods.Contains("map.terrainGrid.SetTempTerrain(c, water)") && gods.Contains("map.terrainGrid.RemoveTempTerrain(c)") && gods.Contains("c.GetThingList(map).Count > 0")
          && gods.Contains("map.areaManager.Home[c]"), "the tide: water laid over empty shore out of the home ground for the night, taken away at first light");
        C(gods.Contains("Color.HSVToRGB") && gods.Contains("OwnHappenings.Stand(p,"), "the giant in the mist: a ring of colours round the one who stops to look");
        C(Regex.IsMatch(gods, @"Scribe_Collections\.Look\(ref tide,") && gods.Contains("tide.Count > OwnHappeningsMath.TideCellsMost"), "the tide's cells saved, a few dozen at most");
        string save = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(save.Contains("\"Gods/OnMap/OwnRound3.cs:tide\"") && save.Contains("\"Gods/OnMap/OwnHappenings.cs:last\": (\"fixed\", " + OwnHappeningsMath.All.Length + ","),
            "the saved lists modelled in the save estimate (the tide, and one a happening)");
        C(touch.Contains("\"CC_GodR3_Saw_\"") && GodTouchMath.InOwnEffectKeys("CC_GodR3_Saw_Newborn") && GodTouchMath.HasOwnEffect("CC_GodR3_Saw_Glory"),
            "each brings its own on the map, so nothing of the gods' touch comes on top");
        foreach (var h in six)
            C(Regex.IsMatch(dbg, @"DebugAction\(""Rimfolk"", ""Gods: " + OwnHappeningsMath.GodOf(h) + @"'s [^""]+ now""") && dbg.Contains("OwnHappening." + h), h + ": a dev action brings it now");

        // The unseen gods: cards in three wordings, named only in the title the colony may use; the book in its witness's words.
        foreach (var w in new[] { "Newborn", "Ripen", "Foxfire", "Finished", "Tide", "Glory" })
        {
            C(keyed.ContainsKey("CC_GodR3_" + w + "_Title") && keyed.TryGetValue("CC_GodR3_" + w + "_TitleNamed", out var named) && named.Contains("{GOD}"), w + ": a title, and a named one with the colony's name");
            C(new[] { "_Text", "_Text_2", "_Text_3" }.All(s => keyed.TryGetValue("CC_GodR3_" + w + s, out var t) && !t.Contains("{GOD}") && !godNames.Any(g => Regex.IsMatch(t, @"\b" + g + @"\b"))),
                w + ": its card in three wordings, never naming a god");
            C(gods.Contains("\"CC_GodR3_" + w + "_Title\", \"CC_GodR3_" + w + "_TitleNamed\""), w + ": its card titled by the frame (Gods.CardName decides the name)");
        }
        foreach (var key in Regex.Matches(gods, @"""(CC_GodR3_Saw_[A-Za-z]+)""").Cast<Match>().Select(m => m.Groups[1].Value).Distinct())
            C(keyed.TryGetValue(key, out var t) && t.Contains("{WHO}") && !t.Contains("{GOD}"), $"the book keeps it in its witness's words: {key}");
        C(keyed.Where(kv => kv.Key.StartsWith("CC_GodR3_")).All(kv => !kv.Value.Contains('!') && !kv.Value.Contains('—')), "the gods' cards in plain words");

        var ext3 = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_Round3_GodsLand.xml")).Root!.Elements().FirstOrDefault(e => e.Name.LocalName == "ColonyCouncil.FlavorExtensionDef");
        C(ext3?.Element("pool")?.Value == "Rimfolk_Pool_GodOwnSaid", "what people say joined to the gods' own happenings' pool");
        var said = ext3?.Descendants("li").Select(l => l.Value).ToList() ?? new List<string>();
        foreach (var tag in new[] { "r3newborn", "r3ripen", "r3foxfire", "r3richsoil", "r3pipnight", "r3finished", "r3tide", "r3glory" })
        {
            C(said.Count(l => Tags(l) == tag) >= 2 && said.Any(l => Tags(l) == tag + ",doubt"), tag + ": what a believer says, and what a doubter says");
            C(Regex.IsMatch(gods, @"""" + tag + @""""), tag + ": said in the code");
        }
        C(said.All(l => !l.Contains("{GOD}") && !godNames.Any(g => Regex.IsMatch(l, @"\b" + g + @"\b")) && Regex.Matches(Bare(l).TrimEnd('.', '?'), @"[.?!]\s").Count == 0 && !l.Contains('!')),
            "their words never name a god (their own guess), one sentence each");

        // ================= The mods, by name, from the player's active mods =================

        var kindsTsv = File.ReadAllLines(Path.Combine(root, "docs", "player-mods", "pawnkinds.tsv")).Skip(1).Select(l => l.Split('\t')).Where(c => c.Length > 2 && c[2] == "yes").Select(c => c[0]).ToHashSet();
        foreach (var k in new[] { "WildBoar", "VAEWaste_Wasteboar", "AEXP_Rattlesnake", "Cobra", "Seal", "SeaLion", "Otter" })
            C(kindsTsv.Contains(k) && workers.Contains("\"" + k + "\""), $"the animal {k}: in the player's active mods, by name in code");
        var defsTsv = new HashSet<string>();
        using (var gz = new GZipStream(File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz")), CompressionMode.Decompress))
        using (var r = new StreamReader(gz))
        {
            string line;
            while ((line = r.ReadLine()) != null)
            {
                var c = line.Split('\t');
                if (c.Length > 3 && c[3] == "yes") defsTsv.Add(c[0] + ":" + c[1]);
            }
        }
        foreach (var d in new[] { "TerrainDef:Bridge", "TerrainDef:SoilRich", "TerrainDef:ShallowFloodwater", "ThingDef:Column", "SoundDef:Roof_Start", "SoundDef:HissSmall", "SoundDef:Pawn_Wildboar_Call" })
            C(defsTsv.Contains(d), $"{d}: the game's or an active mod's");
        return n;
    }
}
