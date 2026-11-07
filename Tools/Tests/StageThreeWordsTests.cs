using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Stage 3's content pass (7 November 2026): the words of many colonies, read from the defs as the game joins them (a
/// pool's own lines and those its extensions add from any file). The bubbles said over a head are one short sentence
/// with the tags their draw sets; the head's letter names only what its draw fills; the take-back answers keep to their
/// purpose; the talk with a town the colony was is about that town; the raid's letters name every one of the colony's
/// former people who died, one or many; the journals' days and the newcomers' first entry say only what happened; every
/// letter's fellow keeps to its key's placeholders and is read through TextVariants.Vary; the shipped lines found said in
/// the wrong voice are narrowed where they are picked; and nothing adds a draw.
/// </summary>
public static class StageThreeWordsTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "stage three words: " + what);
        }

        string root = Root();
        C(root != null, "found the mod's Defs folder");
        if (root == null) return n;
        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));

        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f).Root!).ToList();

        // Each pool's lines by tone, its own and those added by extensions in any file.
        var pools = new Dictionary<string, Dictionary<string, List<string>>>();
        foreach (var el in defs.SelectMany(d => d.Elements()))
        {
            string? name = el.Name.LocalName == "ColonyCouncil.FlavorPoolDef" ? el.Element("defName")?.Value
                : el.Name.LocalName == "ColonyCouncil.FlavorExtensionDef" ? el.Element("pool")?.Value : null;
            var block = el.Element("lines");
            if (name == null || block == null) continue;
            if (!pools.TryGetValue(name, out var tones)) pools[name] = tones = new Dictionary<string, List<string>>();
            foreach (var tone in block.Elements())
            {
                if (!tones.TryGetValue(tone.Name.LocalName, out var list)) tones[tone.Name.LocalName] = list = new List<string>();
                list.AddRange(tone.Elements("li").Select(li => li.Value));
            }
        }
        List<string> All(string pool) => pools.TryGetValue(pool, out var t) ? t.Values.SelectMany(l => l).ToList() : new List<string>();

        // ---------------- Said over a head ----------------

        // The tags any line can be picked by (Flavor.TagsFor), and what each draw adds.
        var moment = new HashSet<string>
        {
            "spring", "summer", "fall", "winter", "morning", "day", "evening", "night", "rain", "snow", "fog", "storm", "clear", "hot", "cold", "afterraid",
            "poor", "rich", "small", "large", "tribal", "medieval", "industrial", "spacer", "lowtech", "lowmood", "highmood", "child", "teen", "old", "hurt",
            "newcomer", "grieving", "inbed", "eating", "relaxing", "working", "outside", "indoors", "inownroom", "trusting", "doubtful", "bitter"
        };
        var families = new[] { "trait:", "skill:", "work:", "meme:", "xeno:", "weather:" };
        var spoken = new Dictionary<string, string[]>
        {
            ["Rimfolk_Pool_FirstNight"] = new[] { "fire", "speaker" },
            ["Rimfolk_Pool_FoundingSays"] = new[] { "leave", "seeoff" },
            ["Rimfolk_Pool_MergeWelcome"] = new[] { "fire", "speaker" },
            ["Rimfolk_Pool_MergeGreeting"] = new[] { "fire", "speaker" },
            ["Rimfolk_Pool_HandOverMeeting"] = new[] { "fire", "speaker" },
            ["Rimfolk_Pool_HandOver"] = new[] { "fire", "speaker" },
            ["Rimfolk_Pool_HandOverGive"] = new string[0],
            ["Rimfolk_Pool_HandOverTake"] = new string[0],
            ["Rimfolk_Pool_HandOverFarewell"] = new[] { "going", "staying" },
            ["Rimfolk_Pool_TownHomecoming"] = new[] { "welcome", "back" }
        };
        // The pools' lines before this pass, counted as their own files had them.
        var before = new Dictionary<string, int>
        {
            ["Rimfolk_Pool_FirstNight"] = 16, ["Rimfolk_Pool_FoundingSays"] = 10, ["Rimfolk_Pool_MergeWelcome"] = 13, ["Rimfolk_Pool_MergeGreeting"] = 6,
            ["Rimfolk_Pool_HandOverMeeting"] = 16, ["Rimfolk_Pool_HandOver"] = 18, ["Rimfolk_Pool_HandOverGive"] = 7, ["Rimfolk_Pool_HandOverTake"] = 7,
            ["Rimfolk_Pool_HandOverFarewell"] = 12, ["Rimfolk_Pool_TownHomecoming"] = 12
        };
        IEnumerable<string> TagsOf(string line)
        {
            var p = LineTags.Parse(line);
            if (p.alts == null) yield break;
            foreach (var alts in p.alts)
                foreach (var a in alts)
                    yield return a.name;
        }
        foreach (var kv in spoken)
        {
            var lines = All(kv.Key);
            C(lines.Count >= before[kv.Key] * 2, $"{kv.Key}: at least twice the lines it had ({before[kv.Key]} to {lines.Count})");
            foreach (var line in lines)
            {
                string body = LineTags.Parse(line).text;
                C(FirstSentence(body) == body && body.Length <= 90 && body.Count(ch => ch == '!') <= 1 && !body.Contains('—') && !body.StartsWith("\""),
                    $"{kv.Key}: '{body}' is one short sentence, one '!' at most, no dashes or quotes");
                foreach (var t in TagsOf(line))
                    C(moment.Contains(t) || kv.Value.Contains(t) || families.Any(f => t.StartsWith(f) && t.Length > f.Length), $"{kv.Key}: the tag '{t}' is one its draw sets");
            }
            // A voice of fewer than three lines is drawn with the plain ones (LineMemory.ToneDrawsAlone): none of these grew past it.
            foreach (var tone in pools[kv.Key].Where(t => t.Key != "plain"))
                C(!LineMemory.ToneDrawsAlone(tone.Value.Count), $"{kv.Key}: its {tone.Key} lines are drawn with the plain ones ({tone.Value.Count})");
        }
        bool Has(string line, string tag) => TagsOf(line).Contains(tag);
        // A tag as written, not its negation ([died], never [!died]).
        bool Pos(string line, string tag) => LineTags.Parse(line).tags?.Contains(tag) == true;
        C(All("Rimfolk_Pool_FoundingSays").All(l => Has(l, "leave") || Has(l, "seeoff")), "setting out: every line one of the party's or of those seeing them off");
        C(All("Rimfolk_Pool_HandOverFarewell").All(l => Has(l, "going") || Has(l, "staying")), "farewells: every line one going or one staying");
        C(All("Rimfolk_Pool_TownHomecoming").All(l => Has(l, "welcome") || Has(l, "back")), "a homecoming: every line the colonist's welcome or the one come home");
        C(All("Rimfolk_Pool_MergeGreeting").All(l => l.StartsWith("[speaker")), "the welcome's greeting is the speaker's, every line");
        C(All("Rimfolk_Pool_TownHomecoming").All(l => !Regex.IsMatch(LineTags.Parse(l).text, @"\b(old (bed|room)|your (bed|room|bunk))\b", RegexOptions.IgnoreCase)),
            "a homecoming speaks of no old room or bed: the colony's home may not be the place they left");
        C(All("Rimfolk_Pool_FirstNight").Where(l => Regex.IsMatch(LineTags.Parse(l).text, @"\bfire\b", RegexOptions.IgnoreCase) && !Has(l, "fire") && !Has(l, "speaker"))
              .All(l => ShippedOwn(l)), "the first night speaks of a fire only with one burning at the gathering");
        C(All("Rimfolk_Pool_FirstNight").Where(l => Regex.IsMatch(LineTags.Parse(l).text, @"\bold place\b")).All(l => l.StartsWith("[") && l.Substring(0, l.IndexOf(']')).Contains("!newcomer")),
            "the old place only in the mouth of one who lived there (!newcomer)");
        foreach (var age in new[] { "tribal", "medieval", "industrial", "spacer" })
        {
            C(All("Rimfolk_Pool_FirstNight").Count(l => Has(l, age)) >= 2, $"the first night in the {age} age's own words");
            C(All("Rimfolk_Pool_MergeGreeting").Any(l => Has(l, age)), $"the welcome's greeting in the {age} age's own words");
            C(All("Rimfolk_Pool_FoundingSays").Any(l => Has(l, age)), $"setting out in the {age} age's own words");
        }

        // ---------------- The town the colony was ----------------

        var letter = All("Rimfolk_Pool_TownLetter");
        C(letter.Count >= 80, $"the head's letter has words enough ({letter.Count})");
        var letterTags = new HashSet<string> { "former", "new", "years", "asks", "struggling", "poor", "steady", "thriving", "flourishing", "tribal", "medieval", "industrial", "spacer",
            "their:friend", "region:newtown:theirs", "tie:pact:trade", "tie:pact:defence", "deed:good", "deed:bad" };
        var letterHoles = new HashSet<string> { "HEAD", "TOWN", "OLDCOLONY", "YEARSSINCE", "COLONY", "PEOPLE", "ASKER", "KIN", "FRIEND", "NEWTOWN" };
        foreach (var line in letter)
        {
            var p = LineTags.Parse(line);
            foreach (var t in TagsOf(line))
                C(letterTags.Contains(t) || t.StartsWith("deed:") || t.StartsWith("town:lately:colony:"), $"the head's letter: the tag '{t}' is one its draw sets");
            foreach (var h in Texts.Holes(p.text)) C(letterHoles.Contains(h), $"the head's letter: {{{h}}} is a value its draw fills");
            C(!p.text.Contains('—') && !p.text.Contains('!'), $"the head's letter: no dashes or exclamation marks: '{p.text}'");
            if (p.text.Contains("{ASKER}") || p.text.Contains("{KIN}")) C(Has(line, "asks"), "{ASKER} and {KIN} only when someone asks: " + p.text);
            if (p.text.Contains("{FRIEND}")) C(Has(line, "their:friend"), "{FRIEND} only with their:friend");
            if (p.text.Contains("{NEWTOWN}")) C(TagsOf(line).Any(t => t.StartsWith("region:newtown")), "{NEWTOWN} only with a new town of theirs");
            if (p.text.Contains("{YEARSSINCE}")) C(Has(line, "years"), "the years since only in a line that knows how many: " + p.text);
            // A tribal town's word is carried by heart: writing only where the town is not a tribe's.
            if (Regex.IsMatch(p.text, @"\b(write|writes|written|wrote|letter|ink|paper)\b", RegexOptions.IgnoreCase))
                C(line.Contains("!tribal") || TagsOf(line).Any(t => t == "medieval" || t == "industrial" || t == "spacer")
                  || ShippedLines.AlsoWhen.TryGetValue(line, out var when) && when == "!tribal",
                    "a line that speaks of writing is not a tribal town's: " + p.text);
        }
        foreach (var t in new[] { "former", "new", "asks", "years", "struggling", "poor", "steady", "thriving", "flourishing", "deed:townDefended", "deed:refused", "town:lately:colony:homecoming" })
            C(letter.Count(l => Has(l, t)) >= (t == "former" ? 20 : t == "new" ? 6 : 2), $"the head's letter speaks of {t}");

        var answers = All("Rimfolk_Pool_NeighbourWrites").Where(l => l.StartsWith("[takeback")).ToList();
        C(answers.Count >= 25, $"their answer when asked for the town back ({answers.Count})");
        foreach (var l in answers)
        {
            var tags = TagsOf(l).ToList();
            C(tags.Count(t => t == "warm" || t == "cordial" || t == "cool" || t == "refusal") == 1, "a take-back answer is one way of answering: " + l);
            C(tags.All(t => new[] { "takeback", "warm", "cordial", "cool", "refusal", "theirLowtech", "theirHightech", "call", "letter" }.Contains(t)), "a take-back answer's tags are its draw's: " + l);
        }

        string dir = Path.Combine(root, "Defs", "OutsiderTalkDefs");
        var talk = Directory.GetFiles(dir, "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.OutsiderTalkDef")).ToList();
        var former = talk.Where(d => (d.Element("when")?.Value ?? "").Split(',').Any(t => t == "town:former" || t == "them:former" || t.StartsWith("town:former|"))).ToList();
        C(former.Count >= 30, $"talk with the people of a town the colony was ({former.Count} pieces)");
        var mine = XDocument.Load(Path.Combine(dir, "Rimfolk_OutsiderTalks_StageThree.xml")).Root!.Elements("ColonyCouncil.OutsiderTalkDef").ToList();
        C(mine.All(d => (d.Element("when")?.Value ?? "").Split(',').Any(t => t == "town:former" || t == "them:former")), "every piece of this pass is about a town the colony was");
        C(mine.All(d => (d.Element("defName")?.Value ?? "").StartsWith("Rimfolk_OT_S3_")), "this pass's pieces are named Rimfolk_OT_S3_");
        foreach (var where in new[] { "role:trader", "role:visitor", "first", "met", "their:tribal", "their:medieval", "their:industrial", "their:spacer", "us:noble", "kin", "town:lately:colony:handedOver" })
            C(mine.Any(d => (d.Element("when")?.Value ?? "").Contains(where) || d.Descendants("li").Any(li => li.Value.Contains(where))), $"the town's people are talked with {where}");

        // ---------------- The raid on the town ----------------

        var raid = XDocument.Load(Path.Combine(root, "Defs", "WorldSituationDefs", "Rimfolk_WS_TownRaid.xml")).Root!.Elements().Single();
        var news = raid.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
        var call = raid.Element("beats")!.Elements("li").Single().Element("texts")!.Elements("li").Select(l => l.Value).ToList();
        foreach (var means in new[] { "runner", "rider", "radio", "comms" })
        {
            C(news.Count(l => Has(l, means)) >= 5 && news.Any(l => Has(l, means) && Has(l, "enemyEmpire")), $"the news by {means}, the Empire's raid among them");
            C(call.Count(l => Has(l, means)) >= 4, $"the call by {means}");
            foreach (var head in new[] { "nohead", "!nohead" })
                C(news.Count(l => Has(l, means) && l.Contains(head == "nohead" ? ",nohead" : "!nohead")) >= 2 && call.Count(l => Has(l, means) && l.Contains(head == "nohead" ? ",nohead" : "!nohead")) >= 2,
                    $"by {means}, {(head == "nohead" ? "with no head" : "from its head")}, more than one wording");
        }
        C(news.Concat(call).Where(l => l.Contains("{OTHER")).All(l => l.Contains("!nohead")), "the head named only when there is one");
        var endings = raid.Element("endings")!.Elements("li").ToDictionary(e => e.Element("key")!.Value, e => e.Element("texts")!.Elements("li").Select(l => l.Value).ToList());
        foreach (var key in new[] { "defended", "fell", "hurt" })
            C(endings[key].Any(l => Has(l, "diedMany") && l.Contains("{DEAD}") && Regex.IsMatch(l, @"\bwere\b")), $"{key}: many of the colony's former people dead, told as many");
        C(endings["held"].Any(l => Pos(l, "seen") && Pos(l, "died") && l.Contains("{DEAD}")), "held after the colony's fighters left: those of the colony's former people killed are named");
        foreach (var kv in endings)
            foreach (var l in kv.Value.Where(l => Pos(l, "died")))
                C(l.Contains("{DEAD}"), $"{kv.Key}: a line for the dead names them");
        var narrowed = ShippedLines.AlsoWhen.Where(kv => kv.Value == "!diedMany").Select(kv => kv.Key).ToList();
        foreach (var key in new[] { "defended", "fell", "hurt" })
            foreach (var l in endings[key].Where(l => Pos(l, "died") && !Pos(l, "diedMany") && !Pos(l, "!diedMany")))
                C(narrowed.Contains(l), $"{key}: a shipped line that names one dead is said only for one: {l.Substring(0, Math.Min(60, l.Length))}");
        C(ShippedLines.AlsoWhen.TryGetValue(endings["held"].First(l => l.StartsWith("[seen]")), out var seen) && seen == "!died", "held: the shipped line that names nobody is said only when nobody died");
        C(Code("Colonies/TownRaid.cs").Contains("s.Set(\"died\")") && Code("Colonies/TownRaid.cs").Contains("s.Set(\"diedMany\")"), "the raid's dead are its flags, as the lines ask");
        // A letter keeps its paragraphs as the file writes them: the game may read "\n" as a new line.
        string paragraphs = endings["defended"].First(l => l.StartsWith("[died]"));
        C(ShippedLines.ConditionOf(paragraphs.Replace("\\n", "\n")) == ShippedLines.ConditionOf(paragraphs) && ShippedLines.ConditionOf(paragraphs) != null,
            "a shipped line with paragraphs is found however its new lines were read");
        C(Code("Lean/ShippedLines.cs").Contains("line.Replace(\"\\n\", \"\\\\n\")"), "and the pick asks it that way");

        // ---------------- Shipped lines in the wrong voice ----------------

        var speakerOnly = ShippedLines.AlsoWhen.Where(kv => kv.Value == "speaker").Select(kv => kv.Key).ToList();
        C(speakerOnly.Count(l => All("Rimfolk_Pool_HandOver").Contains(l)) >= 10, "the ceremony's lines in the voice of those who stay are its head's");
        C(speakerOnly.Count(l => All("Rimfolk_Pool_MergeWelcome").Contains(l)) >= 4, "the welcome's lines in the host's voice are the host's");
        C(Code("PlayOut/PlayOutLord.cs").Contains("if (speaker != null && speaker == organizer) tags.Add(\"speaker\");"), "the gathering's own speaker is [speaker], as those lines ask");
        C(Code("Colonies/HandOvers.cs").Contains("organizer = everyone.Contains(head) ? head : null") && Code("Colonies/Merges.cs").Contains("invited = home, organizer = speaker"),
            "the ceremony's organizer is its head, the welcome's its host");

        // ---------------- The founders' talk ----------------

        var talkLines = defs.SelectMany(d => d.Elements("ColonyCouncil.TalkLinesDef")).Where(d => d.Element("memory")?.Value == "Rimfolk_Mem_Founding").ToList();
        foreach (var turn in new[] { "Open", "Reply", "Close" })
            C(talkLines.Where(d => d.Element("turn")?.Value == turn).SelectMany(d => d.Descendants("li")).Count() >= (turn == "Close" ? 10 : 25),
                $"the founders' talk of the colony they founded: {turn}");

        // ---------------- The journals ----------------

        var days = defs.SelectMany(d => d.Elements("ColonyCouncil.JournalDayDef")).ToDictionary(d => d.Element("defName")!.Value);
        foreach (var (def, thought, social) in new[] { ("Rimfolk_JDay_Uprooted", "Rimfolk_Uprooted", false), ("Rimfolk_JDay_Parted", "Rimfolk_Parted", true), ("Rimfolk_JDay_FirstNight", "Rimfolk_FirstNight", false) })
        {
            C(days.TryGetValue(def, out var d) && d.Descendants("li").Any(li => li.Value == thought) && (d.Element("social")?.Value == "true") == social, $"{def}: the day of {thought}");
            var lines = All("Council_Journal_Day_" + d?.Element("key")?.Value);
            C(lines.Count >= 12, $"{def}: its lines ({lines.Count})");
            C(social ? lines.Count(l => l.Contains("{OTHER")) * 2 >= lines.Count : lines.All(l => !l.Contains("{OTHER")), $"{def}: {{OTHER}} only in the lines about someone, and in most of those");
            C(lines.All(l => !l.Contains('—')), $"{def}: no dashes");
        }
        C(All("Council_Journal_Day_FirstNight").All(l => !Regex.IsMatch(LineTags.Parse(l).text, @"\bfire\b")), "the first night's journal promises no fire: one is not always lit");
        C(All("Council_Journal_Day_Uprooted").All(l => !Regex.IsMatch(LineTags.Parse(l).text, @"\b(sent (away|here|off)|kept (here|back|behind)|made to (stay|go))\b")) && All("Council_Journal_Day_Parted").All(l => !l.Contains("colony")),
            "uprooted and parted in words true either way: sent away or kept, at a founding or a hand-over");
        var first = All("Rimfolk_Journal_NewcomerFirst");
        var firstTags = new HashSet<string>(moment) { "founding", "joining", "walked", "pods", "gravship", "scenario", "mechanitor", "tribalborn" };
        C(first.Count >= 30, $"the newcomers' first entry ({first.Count})");
        foreach (var l in first)
            foreach (var t in TagsOf(l))
                C(firstTags.Contains(t) || families.Any(f => t.StartsWith(f)), $"the newcomers' first entry: the tag '{t}' is one its draw sets");
        foreach (var t in new[] { "founding", "joining", "walked", "pods", "gravship", "mechanitor", "tribalborn" })
            C(first.Count(l => Has(l, t)) >= 2, $"the newcomers' first entry for {t}");
        C(first.Where(l => Regex.IsMatch(LineTags.Parse(l).text, @"\b(pod|pods|landing)\b")).All(l => Has(l, "pods")), "a landing only for those who came down in pods");
        C(first.Where(l => Regex.IsMatch(LineTags.Parse(l).text, @"\b(walked|walk)\b")).All(l => Has(l, "walked")), "a walk only for those who walked in");
        string foundings = Code("Colonies/Foundings.cs");
        C(foundings.Contains("FirstWords(came, founding, \"walked\", false);") && Code("Colonies/ScenarioStarts.cs").Contains("Foundings.FirstWords(came, founding, how == StartArrival.Pods ? \"pods\""),
            "the first entry is written as they come, walking in or by a scenario's arrival");
        C(foundings.Contains("journals.For(p) != null) continue;") && Code("Journal.cs").Contains("if (For(p) != null || p.records == null) continue;"),
            "in place of the day's first entry, never as well as it: one first entry each, as before");
        int firstWords = foundings.IndexOf("internal static void FirstWords", StringComparison.Ordinal);
        string firstBody = firstWords >= 0 ? foundings.Substring(firstWords, foundings.IndexOf("NewcomerFirstPool = ", firstWords, StringComparison.Ordinal) - firstWords) : "";
        C(firstBody.Length > 0 && !firstBody.Contains("Tell.") && !firstBody.Contains("Speech.") && !firstBody.Contains("Notify."), "the first entry is a journal line only: no letter, message or bubble added");

        // ---------------- The letters' fellows ----------------

        var keyedDir = Path.Combine(root, "Languages", "English", "Keyed");
        var keyed = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(keyedDir, "*.xml"))
            foreach (var e in XDocument.Load(f).Root!.Elements())
                keyed[e.Name.LocalName] = e.Value;
        var fellows = XDocument.Load(Path.Combine(keyedDir, "Rimfolk_StageThree_Variants.xml")).Root!.Elements().ToList();
        C(fellows.Count >= 30, $"the letters' fellows ({fellows.Count})");
        string colonies = string.Concat(Directory.GetFiles(Path.Combine(root, "Source", "ColonyCouncil", "Colonies"), "*.cs").Select(File.ReadAllText));
        foreach (var e in fellows)
        {
            string name = e.Name.LocalName;
            var m = Regex.Match(name, @"^(.*)_([2-6])$");
            C(m.Success, $"{name} is a numbered fellow");
            if (!m.Success) continue;
            string key = m.Groups[1].Value;
            int i = int.Parse(m.Groups[2].Value);
            C(keyed.ContainsKey(key) && Enumerable.Range(2, i - 2).All(k => keyed.ContainsKey(key + "_" + k)), $"{name}: its key and the fellows before it exist");
            if (!keyed.TryGetValue(key, out var baseText)) continue;
            var holes = Texts.Holes(baseText).ToHashSet();
            C(Texts.Holes(e.Value).All(h => holes.Contains(h) || h.StartsWith("PAWN_") && holes.Any(x => x.StartsWith("PAWN"))), $"{name}: only the placeholders its key is given");
            C(!e.Value.Contains('—') && !e.Value.Contains('!') && !Regex.IsMatch(e.Value, @"\bin \d+ (days?|hours?)\b|\{DAYS\}"), $"{name}: no dashes, exclamation marks or countdowns");
            C(baseText.TrimEnd().EndsWith(":") == e.Value.TrimEnd().EndsWith(":"), $"{name}: it leads into what follows as its key does");
            bool read = colonies.Contains("TextVariants.Vary(\"" + key + "\")") || key.StartsWith("CC_TownAfter_LetterIntro_") && colonies.Contains("TextVariants.Vary(comms || messenger ? \"CC_TownAfter_LetterIntro_\" + rec.age")
                        || key.StartsWith("CC_Found_Newcomers") && colonies.Contains("TextVariants.Vary(founding ? \"CC_Found_NewcomersFoundText\" : \"CC_Found_NewcomersText\")");
            C(read, $"{name}: its key is read through TextVariants.Vary");
        }
        C(fellows.All(e => !Regex.IsMatch(e.Name.LocalName, "History")), "history lines keep one wording");

        // ---------------- Going there ----------------

        var mission = XDocument.Load(Path.Combine(root, "Defs", "MissionDefs", "Rimfolk_Missions_TownDay.xml")).Root!.ToString();
        C(Regex.Matches(mission, @"<li>\[founding").Count >= 5 && Regex.Matches(mission, @"<li>\[handover").Count >= 5, "the town's days, the founding's and the hand-over's, in more than one wording");
        var dilemma = XDocument.Load(Path.Combine(root, "Defs", "DilemmaDefs", "Rimfolk_Dilemmas_TownKin.xml")).Root!.Descendants("texts").Single().Elements("li").Select(l => l.Value).ToList();
        foreach (var tie in new[] { "spouse", "child", "friend" }) C(dilemma.Count(l => l.StartsWith("[" + tie + "]")) >= 2, $"moving there to a {tie}, more than one wording");

        // ---------------- No draw added ----------------

        C(!Regex.IsMatch(string.Join("\n", defs.SelectMany(d => d.Elements("ColonyCouncil.JournalDayDef")).Select(d => d.ToString())), @"<weight>[3-9]"), "the journal's new days weigh as the game's own moments do");
        return n;
    }

    /// <summary>A line of the pool's own file before this pass (shipped), which keeps its words.</summary>
    private static bool ShippedOwn(string line) => ShippedText.Contains(line);

    private static readonly HashSet<string> ShippedText = new()
    {
        "[lowtech,fire] The fire caught on the first try, which is a good sign.",
        "[speaker,fire] We came a long way to sit round this fire, so let's make it worth the walk.",
        "[speaker,fire] Look around this fire, because these are the people who made this colony."
    };

    private static string FirstSentence(string s)
    {
        for (int i = 12; i < s.Length; i++)
            if (s[i] == '.' || s[i] == '!' || s[i] == '?')
                return s.Substring(0, i + 1);
        return s;
    }

    private static string? Root()
    {
        string? root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
