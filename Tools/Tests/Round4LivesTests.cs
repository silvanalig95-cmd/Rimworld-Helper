using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Content round 4, the colonists' own lives (docs/content-round-4-lives.md; the player's go of 27 November 2026: content
/// inside the frames there are). The rules without game types (Arcs/Round4LivesMath.cs), and the defs and code: eight
/// personal stories, eight wishes, four life goals and four ambitions, each on a worker that exists, the stories' talks and
/// moods there, the wishes asked in every voice with an age form and a promise that sets something going on the map, the
/// ambitions' ends played out, the gods named only as the colonist holds them (two pieces at most of the round touch them),
/// no countdowns, plain messages, the game's defs read by name from the player's active mods, nothing saved of its own and
/// nobody hurt.
/// </summary>
public static class Round4LivesTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "round 4 lives: " + what);
        }

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        XElement Defs(string rel) => XDocument.Load(Path.Combine(root, "Defs", rel)).Root!;
        string Cls(XElement e) => ((string)e.Attribute("Class") ?? "").Replace("ColonyCouncil.", "");
        List<string> Lis(XElement e, string name) => e?.Element(name)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
        string[] Tones = { "plain", "warm", "blunt", "cheerful", "gloomy", "cold" };
        IEnumerable<(string tone, string line)> Lines(XElement e) =>
            e == null ? Enumerable.Empty<(string, string)>() : e.Elements().Where(t => Tones.Contains(t.Name.LocalName)).SelectMany(t => t.Elements("li").Select(l => (t.Name.LocalName, l.Value)));
        bool Countdown(string s) => Regex.IsMatch(s, @"\bin (\d+|a|two|three|four|five|six) (days?|hours?)\b") || Regex.IsMatch(s, @"\b\d+ (days?|hours?)\b");

        // ================= The rules =================

        C(Round4LivesMath.TradeFell(true, 0, 6, 0) && !Round4LivesMath.TradeFell(true, 0, 7, 0) && !Round4LivesMath.TradeFell(true, 1, 3, 0)
          && !Round4LivesMath.TradeFell(true, 0, 3, 1) && !Round4LivesMath.TradeFell(false, 0, 3, 0),
            "a trade falls to someone: the work on, no passion, still poor at it (6 or less), nobody better doing it");
        C(Round4LivesMath.HandsOn("Cooking") && Round4LivesMath.HandsOn("Medicine") && !Round4LivesMath.HandsOn("Social") && !Round4LivesMath.HandsOn("Intellectual") && !Round4LivesMath.HandsOn("Melee"),
            "the stories of work are about hands-on work, never talk, study or fighting");
        C(Round4LivesMath.WagerWorks.All(w => Round4LivesMath.WagerRecord(w) != null) && Round4LivesMath.WagerRecord("Research") == null, "every kind of work a wager can be about has the record it counts by");
        C(Round4LivesMath.WagerPair(5, 9, 10, 30, false) && !Round4LivesMath.WagerPair(4, 9, 30, 30, false) && !Round4LivesMath.WagerPair(9, 9, 9, 30, false) && !Round4LivesMath.WagerPair(9, 9, 30, 30, true),
            "a wager between two good at it (5 or more), fond enough of each other (10), never rivals");
        C(Round4LivesMath.WagerOutcome(30f, 20f) == "won" && Round4LivesMath.WagerOutcome(20f, 30f) == "lost" && Round4LivesMath.WagerOutcome(30f, 28f) == "even"
          && Round4LivesMath.WagerOutcome(1f, 0f) == "even" && Round4LivesMath.WagerOutcome(3f, 0f) == "won" && Round4LivesMath.WagerOutcome(-5f, 0f) == "even",
            "the count: won, lost, or even within a tenth (and one apart at least)");
        C(Round4LivesMath.ReadWagerKey(Round4LivesMath.WagerKey("Mining", 123.5f, 7f), out string wk, out float ka, out float kb) && wk == "Mining" && Math.Abs(ka - 123.5f) < 0.01f && Math.Abs(kb - 7f) < 0.01f
          && !Round4LivesMath.ReadWagerKey("Mining", out _, out _, out _) && !Round4LivesMath.ReadWagerKey("Nonsense|1|2", out _, out _, out _),
            "the wager's key keeps the work and where both stood");
        C(Round4LivesMath.TalesPair(55f, 4f, true, 10, 10) && !Round4LivesMath.TalesPair(54f, 8f, true, 30, 30) && !Round4LivesMath.TalesPair(70f, 3f, true, 30, 30)
          && !Round4LivesMath.TalesPair(70f, 8f, false, 30, 30) && !Round4LivesMath.TalesPair(70f, 8f, true, 9, 30), "the old one's tales: an elder (55), a child of four or more, fond both ways");
        C(Round4LivesMath.RealWinter(-0.1f) && !Round4LivesMath.RealWinter(0f), "a real winter: the coldest month below freezing");
        C(Round4LivesMath.FirstWinter(true, true, 29f) && !Round4LivesMath.FirstWinter(true, true, 30f) && !Round4LivesMath.FirstWinter(false, true, 5f) && !Round4LivesMath.FirstWinter(true, false, 5f),
            "a first winter: in autumn, on land with a winter, for someone who came since the last one");
        C(Round4LivesMath.WinteredDays >= 60f, "the one to ask has been here a year");
        C(Round4LivesMath.WorkStrangers(0, 25, false, false) && !Round4LivesMath.WorkStrangers(26, 10, false, false) && !Round4LivesMath.WorkStrangers(-1, 10, false, false)
          && !Round4LivesMath.WorkStrangers(10, 10, true, false) && !Round4LivesMath.WorkStrangers(10, 10, false, true), "work friends: hardly know each other yet, not friends or partners");
        C(Round4LivesMath.GotOnWith(20, 4f, true, false) && !Round4LivesMath.GotOnWith(19, 1f, true, false) && !Round4LivesMath.GotOnWith(40, 4.5f, true, false)
          && !Round4LivesMath.GotOnWith(40, 1f, false, false) && !Round4LivesMath.GotOnWith(40, 1f, true, true), "friends over the hills: got on lately, a people at peace, gone home");
        C(Round4LivesMath.Spared(1000, 1000 + 10 * 60000, 60000, true) && !Round4LivesMath.Spared(1000, 1001 + 10 * 60000, 60000, true) && !Round4LivesMath.Spared(1000, 2000, 60000, false)
          && !Round4LivesMath.Spared(-1, 2000, 60000, true), "a promise in the dark: down within ten days, up now");
        C(Round4LivesMath.GoneQuiet(-1, 500000, 60000) && Round4LivesMath.GoneQuiet(0, 240000, 60000) && !Round4LivesMath.GoneQuiet(1, 240000, 60000), "an old friend gone quiet: no warm moment in four days");
        C(Round4LivesMath.WantsReading(60f, 0.75f) && !Round4LivesMath.WantsReading(59f, 0.5f) && !Round4LivesMath.WantsReading(70f, 0.8f), "read to: sixty or more, the eyes at three quarters or less");
        C(Round4LivesMath.FrostHarvestWorth(true, true, 12) && !Round4LivesMath.FrostHarvestWorth(true, true, 11) && !Round4LivesMath.FrostHarvestWorth(false, true, 50) && !Round4LivesMath.FrostHarvestWorth(true, false, 50),
            "the harvest before the frost: twelve ripe plants in autumn with a winter to come");
        C(Round4LivesMath.HarvestIn(1, 10) && !Round4LivesMath.HarvestIn(2, 10) && !Round4LivesMath.HarvestIn(0, 0), "the harvest is in at nine in ten");
        C(Round4LivesMath.SnowDay(30, -15f) && !Round4LivesMath.SnowDay(29, 0f) && !Round4LivesMath.SnowDay(100, -15.5f), "a snow day: deep snow, not bitter cold");
        C(Round4LivesMath.GraveWord(15f, 20) && !Round4LivesMath.GraveWord(15.5f, 50) && !Round4LivesMath.GraveWord(2f, 19), "a word at the grave: lately, and thought well of");
        C(Round4LivesMath.HardTime(0.3f, 0f, -1f) && Round4LivesMath.HardTime(0.8f, 30f, -1f) && Round4LivesMath.HardTime(0.8f, 0f, 9f) && !Round4LivesMath.HardTime(0.8f, 10f, 11f) && !Round4LivesMath.HardTime(-1f, 0f, -1f),
            "an hour at the shrine: a low mood, strain or a loss lately");
        C(Round4LivesMath.EldersShare(1, 2) == 0.5f && Round4LivesMath.EldersShare(3, 2) == 1f && Round4LivesMath.EldersShare(0, 0) == 1f, "a good old age: the share of elders well housed");
        C(Round4LivesMath.EldersWorth(1, 2) && !Round4LivesMath.EldersWorth(2, 2) && !Round4LivesMath.EldersWorth(0, 0), "pitched only with an elder who lacks a decent room");
        C(Round4LivesMath.ElderRoom >= 25f && Round4LivesMath.ElderRoom <= 40f, "a decent room, not a noble's");

        // ================= The code =================

        string code = Code("Arcs/Round4Lives.cs");
        string math = Code("Arcs/Round4LivesMath.cs");
        string audit = File.ReadAllText(Path.Combine(root, "Tools", "playout_audit.py"));
        var allSource = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Source", "ColonyCouncil"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        foreach (Match m in Regex.Matches(code + "\n" + math, @"\b(?:public|internal|private)\s+(?:static\s+|abstract\s+|sealed\s+)*class\s+(\w+)"))
            C(m.Groups[1].Value.StartsWith("R4L") || m.Groups[1].Value.StartsWith("Round4Lives"), $"the class {m.Groups[1].Value} carries the round's prefix");
        C(!Regex.IsMatch(code + math, @"Scribe_Collections|Scribe_Values|Scribe_References|Scribe_Deep|Sched\.|:\s*GameComponent\b"), "nothing saved of its own, no schedule of its own");
        C(!Regex.IsMatch(code, @"\bHarm\.|Injure\(|TakeDamage\(|HediffMaker"), "nobody is hurt");
        C(code.Contains("Together.When(") && Regex.Matches(code, @"// (together|apart):").Count >= 3, "what passes between two people waits until they are together, or says why it is apart");
        C(code.Contains("SetPawnRestriction") && code.Contains("VarietyThree.First(bench, bill)") && code.Contains("BillRepeatModeDefOf.RepeatCount"), "something new at the bench: the game's own bill, one, first in the list");
        C(code.Contains("DesignationDefOf.HarvestPlant") && code.Contains("Zone_Growing"), "the harvest before the frost: the game's own harvest marks on the fields");
        C(code.Contains("play.snowman = child") && code.Contains("\"BuildSnowman\""), "the snow day's snowman: the game's own job, at the gathering or alone");
        C(code.Contains("new DilemmaEffect_Visit") && code.Contains("e.Begin(ctx)") && code.Contains("Round4Lives.Took(w)"), "the wishes' visits set going by the play-outs' own effects, met when they took place");
        C(code.Contains("PlaySpot.Grave") && code.Contains("place = grave") && code.Contains("PlaySpot.Stranger") && code.Contains("new DilemmaEffect_ShrineThanks"), "a grave, a visitor and a shrine: the visits' own places");
        C(code.Contains("aspirations.When(p, \"downed\")") && code.Contains("Shrines.Nearest(p, god)") && code.Contains("Worship.Called(god, p)"), "a promise in the dark: down lately, a shrine of the god they hold, the god as they call it");
        C(code.Contains("KeepOut.IsVisitor(x)") && code.Contains("CampTracker.CampOf(x) == null"), "the visitors: the game's own visitors, not a camp");
        C(code.Contains("PawnCapacityDefOf.Sight") && code.Contains("ThingRequestGroup.Book"), "read to: the game's own sight and books");
        C(code.Contains("GenTemperature.AverageTemperatureAtTileForTwelfth") && code.Contains("GenLocalDate.Season(map)"), "the seasons by the map's own climate and calendar");
        C(code.Contains("EldersDay.Elders(map)") && code.Contains("RoomUtil.PrivateRoom(p)"), "a good old age: the colony's own elders, in rooms of their own");
        C(code.Contains("class R4LGoal_NeighbourCall : Goal_Record") && code.Contains("o.help > 0"), "the neighbours' call: Rimfolk's own count, while a call for help runs");
        // Two pieces of the round touch the gods, as the colonist holds them.
        int gods = Regex.Matches(code, @"PawnGods\.Held\(p\)|PawnGods\.Held\(pawn\)").Count;
        C(code.Contains("class R4LArcWorker_Promise") && code.Contains("class R4LWishWorker_ShrineHour") && gods <= 4, "at most four pieces touch the gods (the promise and the shrine)");

        // ================= Personal stories =================

        var arcsFile = Defs("ArcDefs/Rimfolk_Round4_Arcs.xml");
        var arcs = arcsFile.Elements("ColonyCouncil.ArcDef").ToList();
        C(arcs.Count == 8, $"eight personal stories ({arcs.Count})");
        var talks = new HashSet<string>(arcsFile.Elements("InteractionDef").Where(e => e.Attribute("ParentName")?.Value == "Council_ArcTalkBase").Select(e => e.Element("defName")!.Value));
        var thoughts = new HashSet<string>(Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories)
            .SelectMany(f => XDocument.Load(f).Root!.Elements("ThoughtDef")).Select(t => t.Element("defName")?.Value ?? ""));
        var godNames = new[] { "Hollin", "Smoulder", "Bellwether", "Morel", "Ennu", "Pip", "Cirrus", "Tamiu", "Helios", "Selene", "Murmur", "Revel", "Hush", "Ducat", "Undertow" };
        int endsOnMap = 0;
        foreach (var arc in arcs)
        {
            string name = arc.Element("defName")?.Value ?? "?";
            C(name.StartsWith("Rimfolk_R4L_Arc_"), $"{name}: the round's name");
            string worker = (arc.Element("workerClass")?.Value ?? "").Replace("ColonyCouncil.", "");
            C(worker.StartsWith("R4LArcWorker_") && code.Contains("class " + worker + " : ArcWorker") || worker == "ArcWorker_Apprentice", $"{name}: its worker {worker}");
            var stages = arc.Element("stages")?.Elements("li").ToList() ?? new List<XElement>();
            C(stages.Count >= 3 && stages.Count <= 5, $"{name}: three to five stages");
            foreach (var g in arc.Descendants("gate").Where(g => g.Element("kind")?.Value == "Talk"))
                C(talks.Contains(g.Element("talk")?.Value ?? ""), $"{name}: its talk {g.Element("talk")?.Value} is one of the round's");
            if (arc.Descendants("gate").Any(g => g.Element("kind")?.Value == "Custom"))
                C(Regex.IsMatch(code, @"class " + worker + @"[^{]*\{(?:(?!class ).)*override bool CustomGate", RegexOptions.Singleline), $"{name}: its worker answers its custom gates");
            foreach (var t in arc.Descendants("thought").Select(x => x.Value).Distinct())
                C(thoughts.Contains(t), $"{name}: the mood {t} exists");
            var last = stages.LastOrDefault();
            if ((last?.Descendants("li") ?? Enumerable.Empty<XElement>()).Any(e => audit.Contains("\"" + Cls(e) + "\"") && Cls(e).StartsWith("DilemmaEffect_"))) endsOnMap++;
            foreach (var w in stages.Select(s => s.Element("waiting")?.Value ?? ""))
                C(!Countdown(w), $"{name}: no countdown in \"{w}\"");
            string words = string.Join(" ", arc.Descendants("li").Where(l => !l.HasElements).Select(l => l.Value)) + " " + arc.Element("summary")?.Value;
            C(!godNames.Any(gname => Regex.IsMatch(words, @"\b" + gname + @"\b")), $"{name}: no god named but as the colonist calls it ({{NAME}})");
            C(!Regex.IsMatch(arc.Element("summary")?.Value ?? "", @"[{}]"), $"{name}: a summary without placeholders, for the Almanac");
        }
        C(endsOnMap >= 5, $"most stories end with something seen on the map ({endsOnMap} of 8)");
        var wager = arcs.First(a => a.Element("defName")?.Value == "Rimfolk_R4L_Arc_Wager");
        var wagerLines = wager.Descendants("li").Where(l => !l.HasElements).Select(l => l.Value).ToList();
        C(new[] { "[won]", "[lost]", "[even]" }.All(t => wagerLines.Any(l => l.StartsWith(t))), "the wager's count is told won, lost or even");
        C(wager.Descendants("li").Count(e => Cls(e) == "DilemmaEffect_Toast") == 2 && wager.Descendants("ifTag").Any(e => e.Value == "won"), "the loser raises the cup, whichever of the two it is");
        var healer = arcs.First(a => a.Element("defName")?.Value == "Rimfolk_R4L_Arc_HealersSide");
        C(Lis(healer, "skills").SequenceEqual(new[] { "Medicine" }) && healer.Descendants("record").Any(r => r.Value == "TimesTendedOther"), "at the healer's side: healing, and patients tended");
        var promise = arcs.First(a => a.Element("defName")?.Value == "Rimfolk_R4L_Arc_Promise");
        C(promise.Descendants("li").Any(e => Cls(e) == "DilemmaEffect_ShrineThanks") && promise.Descendants("li").Where(l => !l.HasElements).Any(l => l.Value.Contains("{NAME}")),
            "the promise kept at the shrine, the god named as the colonist calls it");
        var winter = arcs.First(a => a.Element("defName")?.Value == "Rimfolk_R4L_Arc_FirstWinter");
        C(winter.Descendants("gate").Count(g => g.Element("kind")?.Value == "Custom") == 2, "the first winter waits for winter and for spring");

        // ================= Wishes =================

        var wishes = Defs("WishDefs/Rimfolk_Round4_Wishes.xml").Elements("ColonyCouncil.WishDef").ToList();
        C(wishes.Count == 8, $"eight wishes ({wishes.Count})");
        var allWeights = Directory.GetFiles(Path.Combine(root, "Defs", "WishDefs"), "*.xml").Where(f => !f.Contains("Round4"))
            .SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.WishDef"))
            .Select(w => float.Parse((string)w.Element("weight") ?? "1", CultureInfo.InvariantCulture)).OrderBy(x => x).ToList();
        float median = allWeights[allWeights.Count / 2];
        var keyed = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Round4_Lives.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        foreach (var w in wishes)
        {
            string name = (string)w.Element("defName")!;
            C(name.StartsWith("Rimfolk_R4L_Wish_"), $"{name}: the round's name");
            string worker = ((string)w.Element("workerClass") ?? "").Replace("ColonyCouncil.", "");
            C(worker.StartsWith("R4LWishWorker_") && Regex.IsMatch(code, @"class " + worker + @" : (WishWorker|R4LWishWorker_Visit)\b"), $"{name}: its worker {worker}");
            C(thoughts.Contains((string)w.Element("grantedThought") ?? ""), $"{name}: its thanks are a mood that exists");
            C(w.Element("triggers") == null, $"{name}: no trigger, a share of the wish roll");
            float weight = float.Parse((string)w.Element("weight") ?? "1", CultureInfo.InvariantCulture);
            C(weight <= Math.Max(1.2f, median), $"{name}: weighs about as much as the usual wish ({weight}, the median {median})");
            var request = Lines(w.Element("request")).ToList();
            C(request.Count >= 8 && request.Select(x => x.tone).Distinct().Count() == 6, $"{name}: asked for in all six voices ({request.Count})");
            C(request.Any(x => x.line.StartsWith("[tribal]")) && request.Any(x => x.line.StartsWith("[spacer]")), $"{name}: age forms in the asking");
            C(Lines(w.Element("thanks")).Count() >= 4 && Lines(w.Element("expired")).Any(), $"{name}: thanks and a lapse in its own words");
            string label = (string)w.Element("label") ?? "";
            C(!Regex.IsMatch(label, @"[{}]") || (string)w.Element("ignoreIllegalLabelCharacterConfigError") == "true", $"{name}: a label with names carries the 1.7 flag");
            string words = string.Join(" ", request.Concat(Lines(w.Element("thanks"))).Concat(Lines(w.Element("expired"))).Select(x => x.line)) + " " + w.Element("fulfilledWhen")?.Value;
            C(!Countdown(words) && !words.Contains('—'), $"{name}: no countdown, no em dash");
            C(!godNames.Any(gname => Regex.IsMatch(words, @"\b" + gname + @"\b")), $"{name}: no god named but as the colonist calls it");
        }
        // Each promise sets something going, and says so in plain words.
        foreach (var key in new[] { "NewMake", "OldFriend", "ReadToMe", "FrostHarvest", "SnowDay", "GraveWord", "Visitors", "ShrineHour" })
        {
            C(keyed.ContainsKey("CC_R4L_Reason_" + key) && keyed.ContainsKey("CC_R4L_Promise_" + key), $"the wish {key}: its reason and its promise in the keyed words");
            C(code.Contains("\"CC_R4L_Promise_" + key + "\""), $"the wish {key}: its promise is told when it is set going");
        }
        foreach (Match m in Regex.Matches(code, "\"(CC_R4L_[A-Za-z_]+)\""))
            C(keyed.ContainsKey(m.Groups[1].Value), $"{m.Groups[1].Value} is in the keyed words");
        C(keyed.Keys.All(k => k.StartsWith("CC_R4L_") || k.StartsWith("CC_Asp_Spark_thought_")), "every keyed word carries the round's prefix, but the sparks named as the frame reads them");
        C(keyed.Values.All(v => !Countdown(v) && !v.Contains('—') && !v.Contains('!')), "the messages plain, no countdowns");

        // ================= Life goals =================

        var goals = Defs("AspirationDefs/Rimfolk_Round4_Aspirations.xml").Elements("ColonyCouncil.AspirationDef").ToList();
        C(goals.Count == 4, $"four life goals ({goals.Count})");
        var records = new HashSet<string>(Directory.GetFiles(Path.Combine(root, "Defs", "Misc"), "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements("RecordDef")).Select(r => r.Element("defName")?.Value ?? ""));
        var aspNames = new HashSet<string>(Directory.GetFiles(Path.Combine(root, "Defs", "AspirationDefs"), "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.AspirationDef")).Select(a => a.Element("defName")?.Value ?? ""));
        foreach (var g in goals)
        {
            string name = (string)g.Element("defName")!;
            string worker = ((string)g.Element("workerClass") ?? "").Replace("ColonyCouncil.", "");
            C(worker == "AspirationWorker_Skill" || worker == "AspirationWorker_Record", $"{name}: a worker there is ({worker})");
            if (worker == "AspirationWorker_Record")
            {
                string rec = (string)g.Element("record") ?? "";
                C(records.Contains(rec), $"{name}: Rimfolk's own count {rec}");
            }
            var chosen = Lines(g.Element("chosen")).ToList();
            C(chosen.Count >= 6 && chosen.Select(x => x.tone).Distinct().Count() >= 5, $"{name}: chosen in several voices ({chosen.Count})");
            C(chosen.Any(x => Regex.IsMatch(x.line, @"^\[(tribal|lowtech)\]")), $"{name}: an age form");
            C(Lines(g.Element("achieved")).Count() >= 4, $"{name}: achieved in its own words");
            foreach (var t in Lis(g, "sparks").Where(s => s.StartsWith("thought:")))
                C(thoughts.Contains(t.Substring(8)) || t == "thought:RescuedMe", $"{name}: the spark {t} is a mood that exists");
        }
        var drawing = goals.First(g => g.Element("defName")?.Value == "Rimfolk_R4L_Asp_ChildDrawing");
        C(drawing.Element("who")?.Element("child")?.Value == "true" && aspNames.Contains(drawing.Element("growsInto")?.Value ?? ""), "pictures on the floor: a child's, growing into a grown-up's");

        // ================= Ambitions =================

        var ambitions = Defs("AmbitionDefs/Rimfolk_Round4_Ambitions.xml").Elements("ColonyCouncil.AmbitionDef").ToList();
        C(ambitions.Count == 4, $"four ambitions ({ambitions.Count})");
        foreach (var a in ambitions)
        {
            string name = (string)a.Element("defName")!;
            foreach (var goal in a.Element("goals")?.Elements("li") ?? Enumerable.Empty<XElement>())
            {
                string cls = Cls(goal);
                C(cls == "Goal_Record" && allSource.Contains("class Goal_Record : AmbitionGoal") || cls.StartsWith("R4LGoal_") && code.Contains("class " + cls + " :"), $"{name}: its goal {cls}");
                if (goal.Element("record") != null) C(records.Contains(goal.Element("record")!.Value) || new[] { "PlantsSown", "ThingsCrafted" }.Contains(goal.Element("record")!.Value), $"{name}: its record exists");
            }
            var effects = a.Element("achievedEffects")?.Elements("li").ToList() ?? new List<XElement>();
            C(effects.Count > 0 && effects.All(e => audit.Contains("\"" + Cls(e) + "\"")), $"{name}: reached, it plays out on the map");
            C(!string.IsNullOrEmpty(a.Element("dayLabel")?.Value), $"{name}: its day on the calendar");
            var pitch = Lines(a.Element("pitch")).ToList();
            C(pitch.Count >= 8 && pitch.Select(x => x.tone).Distinct().Count() == 6, $"{name}: pitched in every voice");
            C(pitch.Any(x => x.line.StartsWith("[tribal]")), $"{name}: an age form in the pitch");
            C(Lines(a.Element("achieved")).All(x => !x.line.StartsWith("[")), $"{name}: achieved lines untagged");
            C(!Countdown((string)a.Element("description") ?? ""), $"{name}: no countdown in its description");
        }

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
        foreach (var work in Round4LivesMath.WagerWorks)
        {
            C(active.Contains("WorkTypeDef|" + work), $"WorkTypeDef {work} is in the player's active mods");
            C(active.Contains("RecordDef|" + Round4LivesMath.WagerRecord(work)), $"RecordDef {Round4LivesMath.WagerRecord(work)} is in the player's active mods");
        }
        foreach (var (type, name) in new[] { ("JobDef", "BuildSnowman"), ("RecordDef", "PlantsSown"), ("RecordDef", "ThingsCrafted"), ("RecordDef", "TimesTendedOther"), ("ThoughtDef", "RescuedMe") })
            C(active.Contains(type + "|" + name), $"{type} {name} is in the player's active mods");
        foreach (var skill in Round4LivesMath.HandsOnSkills)
            C(active.Contains("SkillDef|" + skill), $"SkillDef {skill} is in the game");
        C(File.ReadAllText(Path.Combine(root, "docs", "content-round-4-lives.md")).Contains("Not a duplicate of"), "the list says what each is not a duplicate of");
        return n;
    }
}
