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
/// Content round 5, the colony's people together (docs/content-round-5-together.md; the player's go of 27 November 2026:
/// content inside the frames there are). The rules without game types (Social/Round5TogetherMath.cs), and the defs and code:
/// eight social events, each a share of the day's draw, read from the map (the season, the hour, what the two are doing,
/// a newcomer's first days, elders and children, the round's four scenes) with every age's lines; six shared memories, each
/// given by its event with a label that says when it was, and talked over in every voice; six quirks, each after a memory
/// a moment leaves, with journal lines in six voices, dreams and a word over the head; no countdowns, plain words, no god,
/// nothing saved and nobody hurt, the game's defs read by name from the player's active mods.
/// </summary>
public static class Round5TogetherTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "round 5 together: " + what);
        }

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        XElement Defs(string rel) => XDocument.Load(Path.Combine(root, "Defs", rel)).Root!;
        string Name(XElement e) => (string)e.Element("defName") ?? "";
        string Cls(XElement e) => ((string)e.Attribute("Class") ?? "").Replace("ColonyCouncil.", "");
        List<string> Lis(XElement e, string name) => e?.Element(name)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
        string[] Tones = { "plain", "warm", "blunt", "cheerful", "gloomy", "cold" };
        IEnumerable<(string tone, string line)> Lines(XElement e) =>
            e == null ? Enumerable.Empty<(string, string)>() : e.Elements().Where(t => Tones.Contains(t.Name.LocalName)).SelectMany(t => t.Elements("li").Select(l => (t.Name.LocalName, l.Value)));
        bool Countdown(string s) => Regex.IsMatch(s, @"\bin (\d+|a|two|three|four|five|six) (days?|hours?)\b") || Regex.IsMatch(s, @"\b\d+ (days?|hours?)\b");
        string Strip(string s, out string[] tags)
        {
            var m = Regex.Match(s, @"^\[([^\]]*)\]\s*");
            tags = m.Success ? m.Groups[1].Value.Split(',').Select(t => t.Trim()).ToArray() : new string[0];
            return m.Success ? s.Substring(m.Length) : s;
        }
        // Said aloud: the first whole sentence is all that shows (SpeechMath.Shorten, from the 13th character).
        bool OneSentence(string shown)
        {
            for (int i = 12; i < shown.Length - 1; i++)
                if (".!?".IndexOf(shown[i]) >= 0 && ".!?\"'".IndexOf(shown[i + 1]) < 0)
                    return false;
            return true;
        }
        var godNames = new[] { "Hollin", "Smoulder", "Bellwether", "Morel", "Ennu", "Pip", "Cirrus", "Tamiu", "Helios", "Selene", "Murmur", "Revel", "Hush", "Ducat", "Undertow" };
        bool Plain(string s) => !s.Contains('—') && !s.Contains('!') && !Countdown(s) && !godNames.Any(g => Regex.IsMatch(s, @"\b" + g + @"\b")) && !Regex.IsMatch(s, @"\bgods?\b", RegexOptions.IgnoreCase);
        var allDefs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f).Root!).ToList();
        var defNames = allDefs.SelectMany(d => d.Elements()).Select(Name).Where(x => x != "").ToHashSet();
        var thoughtNames = allDefs.SelectMany(d => d.Elements("ThoughtDef")).Select(Name).ToHashSet();

        // ================= The rules =================

        C(Round5TogetherMath.Scenes.Length == 4 && Round5TogetherMath.Scenes.All(Round5TogetherMath.KnownScene)
          && !Round5TogetherMath.KnownScene("") && !Round5TogetherMath.KnownScene(null) && !Round5TogetherMath.KnownScene("speech"),
            "four scenes, and a council season or nothing is none of them");
        C(Round5TogetherMath.OldHand(60f) && Round5TogetherMath.OldHand(400f) && !Round5TogetherMath.OldHand(59.9f) && !Round5TogetherMath.OldHand(0f),
            "an old hand has lived at the colony a year or more");
        C(Round5TogetherMath.Newcomer(0f) && Round5TogetherMath.Newcomer(9.9f) && !Round5TogetherMath.Newcomer(10f) && !Round5TogetherMath.Newcomer(-1f),
            "a newcomer is in their first ten days");
        C(Round5TogetherMath.NewcomerDays == 10f && Code("Flavor.cs").Contains("< GenDate.TicksPerDay * 10) tags.Add(\"newcomer\")"),
            "the newcomer's days are the moment's own newcomer tag");
        C(Round5TogetherMath.Elder(55f) && !Round5TogetherMath.Elder(54.9f), "one of the old: 55 or more, as the elders' other moments count them");
        C(!Round5TogetherMath.ChildGriefThought("BondedAnimalDied") && Round5TogetherMath.ChildGriefThought("PawnWithGoodOpinionDied")
          && Round5TogetherMath.ChildGriefThought("KnowColonistDied") && !Round5TogetherMath.ChildGriefThought(null) && !Round5TogetherMath.ChildGriefThought("Council_Soc_GoodTalk"),
            "a child asks about a death of someone, among the game's memories of grief, never an animal's");
        C(Round5TogetherMath.GriefFresh(0) && Round5TogetherMath.GriefFresh(5 * 60000) && !Round5TogetherMath.GriefFresh(5 * 60000 + 1) && !Round5TogetherMath.GriefFresh(-1),
            "asked about within five days of the death");
        C(Round5TogetherMath.SameGame(7, 7) && !Round5TogetherMath.SameGame(7, 8) && !Round5TogetherMath.SameGame(0, 0), "two at the same game: the same building");
        C(Round5TogetherMath.NearCells >= 5f && Round5TogetherMath.NearCells <= 12f, "near enough to hear, not across the map");
        C(Round5TogetherMath.Label("the spring sowing of {YEAR}", 5503, "spring") == "the spring sowing of 5503" && Round5TogetherMath.Label("the {SEASON} of {YEAR}", 5504, "fall") == "the fall of 5504"
          && Round5TogetherMath.Label("the game at the {THING}", 5503, "summer") == "the game at the {THING}" && Round5TogetherMath.Label(null, 1, "x") == "",
            "a memory's label says when it was, the event's own words filled in after");

        // ================= The code =================

        string code = Code("Social/Round5Together.cs");
        string math = Code("Social/Round5TogetherMath.cs");
        string soc = Code("Social/SocialEvents.cs");
        foreach (Match m in Regex.Matches(code + "\n" + math, @"\b(?:public|internal|private)\s+(?:static\s+|abstract\s+|sealed\s+)*class\s+(\w+)"))
            C(m.Groups[1].Value.StartsWith("R5T") || m.Groups[1].Value.StartsWith("Round5Together"), $"the class {m.Groups[1].Value} carries the round's prefix");
        C(!Regex.IsMatch(code + math, @"Scribe_|Sched\.|:\s*GameComponent\b|HarmonyPatch"), "nothing saved of its own, no schedule and no patch of its own");
        C(!Regex.IsMatch(code, @"\bHarm\.|Injure\(|TakeDamage\(|HediffMaker|StartSocialFight|MentalState"), "nobody is hurt");
        C(soc.Contains("if (!def.scene.NullOrEmpty() && !Round5Together.Fits(def.scene, a, b)) return false;") && soc.Contains("if (!def.scene.NullOrEmpty()) Round5Together.Fill(def.scene, ctx);")
          && soc.Contains("!Round5TogetherMath.KnownScene(scene)"), "a scene is read where a social event is cast, beside the council's seasons, and its name checked");
        C(Regex.IsMatch(soc, @"float expected = UnityEngine\.Mathf\.Clamp\(pawns\.Count / 5f, 0\.4f, 2f\)") && !Regex.IsMatch(soc, @"expected[^;]*AllDefs"),
            "the day's social events are still counted from the colony, so the new ones take a share");
        C(code.Contains("a.CurJobDef == JobDefOf.Sow && b.CurJobDef == JobDefOf.Sow"), "the sowing: both at the game's own sowing job");
        C(code.Contains("RecordDefOf.TimeAsColonistOrColonyAnimal"), "old hands and newcomers by the game's own record of time as a colonist");
        C(code.Contains("GroupPlaces.IsGames(t.def)") && code.Contains("Round5TogetherMath.SameGame(game.thingIDNumber"), "a game: the same building the game counts a game of skill or chance, the game's or a mod's");
        C(code.Contains("m.otherPawn") && code.Contains("dead.Dead && dead.Faction == Faction.OfPlayer") && code.Contains("ctx.stranger = LostLately(ctx.b)"),
            "the child's loss: the game's own memory of the death, naming someone of the colony who died");
        C(code.Contains("// together:") && code.Contains("SharedMemories.Add(ctx.a, ctx.b, memory, Label(ctx), weight)"), "the memory is given to the two of the event, who are together");
        C(Code("Immersion/HabitBubbles.cs").Contains("Round5Together.Bubbles(Add);") && code.Contains("Together.Hears(o, p, Round5TogetherMath.NearCells)") && code.Contains("Together.Company(p) != null"),
            "the quirks' words go on the habits' list, said when someone who can hear them is near");

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
        foreach (var (type, name) in new[] { ("JobDef", "Sow"), ("JobDef", "Ingest"), ("RecordDef", "TimeAsColonistOrColonyAnimal"), ("JoyKindDef", "Gaming_Cerebral"), ("JoyKindDef", "Gaming_Dexterity"),
                     ("StatDef", "SocialImpact"), ("StatDef", "GlobalLearningFactor") })
            C(active.Contains(type + "|" + name), $"{type} {name} is in the player's active mods");
        foreach (var t in Round5TogetherMath.GriefThoughts) C(active.Contains("ThoughtDef|" + t), $"ThoughtDef {t} is in the player's active mods");

        // ================= Social events =================

        var events = Defs("SocialEventDefs/Rimfolk_Round5_SocialEvents.xml").Elements("ColonyCouncil.SocialEventDef").ToList();
        C(events.Count == 8, $"eight social events ({events.Count})");
        var eras = new HashSet<string> { "tribal", "medieval", "industrial", "spacer", "lowtech", "!lowtech" };
        var allowed = new HashSet<string> { "DilemmaEffect_Thought", "DilemmaEffect_Learn", "DilemmaEffect_SharedMemory", "R5TMemoryEffect" };
        var memoryGivers = new Dictionary<string, string>();
        int ages = 0;
        foreach (var e in events)
        {
            string name = Name(e);
            C(name.StartsWith("Rimfolk_R5T_Soc_"), $"{name}: the round's name");
            var texts = Lis(e, "texts");
            C(texts.Count >= 5, $"{name}: five ways to tell it or more ({texts.Count})");
            C(texts.Count(t => !t.StartsWith("[")) >= 3, $"{name}: told in any age");
            var tagged = texts.Where(t => t.StartsWith("[")).SelectMany(t => { Strip(t, out var ts); return ts; }).ToHashSet();
            if (tagged.Count >= 3) ages++;
            string tone = (string)e.Element("tone") ?? "neutral";
            C(tone == "warm" || tone == "friction" || tone == "mend", $"{name}: a tone");
            string scene = (string)e.Element("scene");
            C(scene == null || Round5TogetherMath.KnownScene(scene), $"{name}: a known scene");
            bool work = e.Element("sameWork") != null;
            foreach (var t in texts)
            {
                string body = Strip(t, out var ts);
                C(ts.All(eras.Contains), $"{name}: only the age's tags in \"{t}\"");
                C(Plain(body), $"{name}: plain, no em dash, exclamation, countdown or god in \"{body}\"");
                foreach (Match m in Regex.Matches(body, @"\{([A-Za-z_]+)\}"))
                {
                    string ph = m.Groups[1].Value;
                    bool ok = Regex.IsMatch(ph, "^(A|B)_(nameDef|pronoun|possessive|objective)$") || (ph == "WORK" && work) || (ph == "THING" && scene == Round5TogetherMath.Game)
                              || (Regex.IsMatch(ph, "^X_(nameDef|pronoun|possessive|objective)$") && scene == Round5TogetherMath.ChildGrief);
                    C(ok, $"{name}: {{{ph}}} is filled in \"{body}\"");
                }
                C(!Regex.IsMatch(body, @"(^|[.?]\s+)\{[ABX]_(pronoun|possessive|objective)\}"), $"{name}: no sentence starts with a lowercase word in \"{body}\"");
            }
            foreach (var li in e.Element("effects")?.Elements("li") ?? Enumerable.Empty<XElement>())
            {
                C(allowed.Contains(Cls(li)), $"{name}: {Cls(li)} is a moment's effect (a memory, a mood, a little learned), nothing staged and no hurt");
                foreach (var th in li.Elements("thought")) C(thoughtNames.Contains(th.Value), $"{name}: its thought {th.Value} exists");
                foreach (var mem in li.Elements("memory")) C(defNames.Contains(mem.Value), $"{name}: its memory {mem.Value} exists");
                if (Cls(li) == "R5TMemoryEffect")
                {
                    string mem = (string)li.Element("memory");
                    memoryGivers[mem] = name;
                    string label = (string)li.Element("label") ?? "";
                    foreach (Match m in Regex.Matches(label, @"\{([A-Za-z_]+)\}"))
                    {
                        string ph = m.Groups[1].Value;
                        C(ph == "YEAR" || ph == "SEASON" || (ph == "THING" && scene == Round5TogetherMath.Game) || (ph == "X_nameDef" && scene == Round5TogetherMath.ChildGrief),
                            $"{name}: the memory's label fills {{{ph}}}");
                    }
                    C(label.StartsWith("the ") || label.StartsWith("a "), $"{name}: the memory's label reads as a name for it (\"{label}\")");
                }
            }
            // Read from the map: each names something that happens there, not just two people near each other.
            bool fromMap = e.Element("tie") != null || work || scene != null || e.Descendants("tags").Any() || e.Descendants("minAge").Any() || e.Descendants("child").Any();
            C(fromMap, $"{name}: comes from something on the map");
            foreach (var role in e.Elements().Where(x => x.Name.LocalName is "roleA" or "roleB"))
            {
                string t = (string)role.Element("tags");
                if (t != null && Regex.IsMatch(t, @"\b(spring|summer|fall|winter|evening|night|rain|outside|indoors|eating|relaxing|newcomer)\b"))
                    C(TogetherFourMath.NeedsMoment(t), $"{name}: \"{t}\" is read from the moment");
            }
            foreach (var w in Lis(e, "sameWork")) C(active.Contains("WorkTypeDef|" + w), $"{name}: the work {w} is in the player's active mods");
            C(e.Element("note") != null && Plain((string)e.Element("note")), $"{name}: a plain note for the tie's history");
        }
        C(ages == events.Count, $"every one of them takes the form of the age ({ages} of {events.Count})");
        var tagsOf = events.ToDictionary(Name, e => string.Join(",", e.Descendants("tags").Select(x => x.Value)));
        C(new[] { "spring", "summer", "winter" }.All(s => tagsOf.Values.Any(t => Regex.IsMatch(t, @"\b" + s + @"\b"))), "the seasons: spring, summer and winter (autumn has the last of the harvest)");
        C(events.Any(e => (string)e.Element("tie") == "Friends") && events.Any(e => (string)e.Element("tone") == "friction") && events.Any(e => (string)e.Element("tone") == "mend"),
            "friendship, rivalry and making up");
        C(events.Count(e => e.Descendants("child").Any(c => c.Value == "true")) == 2 && events.Any(e => e.Descendants("minAge").Any(a => a.Value == "55")), "children, and one of the old with a child");
        C(tagsOf.Values.Any(t => Regex.IsMatch(t, @"(?<!!)\bnewcomer\b")) && events.Any(e => (string)e.Element("scene") == Round5TogetherMath.OldHands), "newcomers and old hands");
        C(events.Count(e => e.Element("sameWork") != null || (string)e.Element("scene") == Round5TogetherMath.Sowing) >= 3
          && events.Count(e => Regex.IsMatch(string.Join(",", e.Descendants("tags").Select(x => x.Value)), @"\b(relaxing|eating)\b") || (string)e.Element("scene") == Round5TogetherMath.Game) >= 3,
            "work and leisure");
        var heated = events.First(e => Name(e) == "Rimfolk_R5T_Soc_HeatedGame");
        C((string)heated.Element("tone") == "friction" && (string)heated.Element("scene") == Round5TogetherMath.Game && (string)heated.Element("changeTie") == "Rivals"
          && Lis(heated, "texts").All(t => t.Contains("{THING}")), "a game that got heated: at the game it names, a rivalry a little stronger");
        var grief = events.First(e => Name(e) == "Rimfolk_R5T_Soc_ChildAsksDead");
        C(grief.Element("roleB")?.Element("child")?.Value == "true" && Lis(grief, "texts").All(t => t.Contains("{X_nameDef}")), "a child's question: the child asks, about the one who died, by name");
        foreach (var name in new[] { "Rimfolk_R5T_Soc_NewcomersWay", "Rimfolk_R5T_Soc_OldAndSmallHands" })
        {
            var e = events.First(x => Name(x) == name);
            C(Lis(e, "texts").All(t => t.Contains("{WORK}")) && e.Descendants("li").Any(li => Cls(li) == "DilemmaEffect_Learn"), $"{name}: at the work it names, and a little of its skill learned");
        }

        // ================= Their moods =================

        var thoughts = Defs("ThoughtDefs/Rimfolk_Round5_Thoughts.xml").Elements("ThoughtDef").ToList();
        int good = 0, bad = 0;
        foreach (var th in thoughts)
        {
            string name = Name(th);
            C(name.StartsWith("Rimfolk_R5T_"), $"{name}: the round's name");
            C((string)th.Element("thoughtClass") == "ColonyCouncil.Thought_CouncilSocial" && (string)th.Element("stackLimitForSameOtherPawn") == "1", $"{name}: about the other one, once each");
            float mood = float.Parse(th.Descendants("baseMoodEffect").First().Value, CultureInfo.InvariantCulture);
            bool instead = th.Descendants("li").Any(x => (string)x.Attribute("Class") == "ColonyCouncil.RewardInstead");
            C(Math.Abs(mood) <= 1f, $"{name}: small, as the other social events' are");
            C(!instead || mood == 0f, $"{name}: something instead of mood gives no mood");
            if (mood > 0f) good++;
            if (mood < 0f) bad++;
            C(Plain(th.Descendants("description").First().Value) && Plain(th.Descendants("label").First().Value), $"{name}: plain words");
            C(events.Any(e => e.Descendants("thought").Any(x => x.Value == name)), $"{name}: left by one of the round's moments");
        }
        C(good <= bad + 1, $"the new moments lift nobody's mood by themselves ({good} warm moods, {bad} sore ones; the rest give belonging instead)");

        // ================= Shared memories and their talk =================

        var memories = Defs("SharedMemoryDefs/Rimfolk_Round5_Memories.xml").Elements("ColonyCouncil.SharedMemoryDef").ToList();
        var talk = allDefs.SelectMany(d => d.Elements("ColonyCouncil.TalkLinesDef")).ToList();
        var talkTags = new HashSet<string> { "byme", "byother", "first", "fresh", "told", "worn", "spring", "summer", "fall", "winter", "evening", "rain", "lowtech", "!lowtech" };
        C(memories.Count == 6, $"six shared memories ({memories.Count})");
        foreach (var m in memories)
        {
            string name = Name(m);
            C(name.StartsWith("Rimfolk_R5T_Mem_"), $"{name}: the round's name");
            C(((string)m.Element("text") ?? "").Contains("{LABEL}") && Plain((string)m.Element("text")), $"{name}: its text says what it was, plainly");
            C(memoryGivers.ContainsKey(name), $"{name}: given by one of the round's moments");
            bool joke = (string)m.Element("joke") == "true";
            string outcome = joke ? "Joke" : "Reminisce";
            var own = talk.Where(l => (string)l.Element("memory") == name).ToList();
            foreach (var turn in new[] { "Open", "Reply", "Close" })
                C(own.Any(l => (string)l.Element("turn") == turn && l.Element("outcomes")!.Elements("li").Any(o => o.Value == outcome)), $"{name}: lines to {turn.ToLowerInvariant()} with");
            var lines = own.SelectMany(l => l.Descendants("li").Where(li => li.Parent?.Parent?.Name.LocalName == "lines")).Select(li => li.Value).ToList();
            C(lines.Count >= 30, $"{name}: thirty lines or more of its own ({lines.Count})");
            C(own.All(l => Tones.All(t => l.Element("lines")!.Element(t) != null) || (string)l.Element("turn") == "Close"), $"{name}: the opening and the reply in every voice");
            foreach (var line in lines)
            {
                string body = Strip(line, out var ts);
                C(ts.All(talkTags.Contains), $"{name}: only the talk's own tags in \"{line}\"");
                string shown = body.Replace("{OTHER}", "Margaret").Replace("{LABEL}", "the fight with Iron Hand");
                C(OneSentence(shown) && shown.Length <= 90, $"{name}: one short sentence said aloud: \"{body}\"");
                C(Regex.Matches(body, @"\{([A-Za-z_]+)\}").All(x => x.Groups[1].Value == "LABEL" || x.Groups[1].Value == "OTHER"), $"{name}: only {{LABEL}} and {{OTHER}} in \"{body}\"");
                C(Plain(body), $"{name}: plain words in \"{body}\"");
            }
            if (joke) C(new[] { "fresh", "told", "worn" }.All(t => lines.Any(l => l.Contains("[" + t))), $"{name}: a joke, fresh, told and worn");
        }
        foreach (var name in new[] { "Rimfolk_R5T_Mem_SmallHands", "Rimfolk_R5T_Mem_Answered" })
        {
            var m = memories.First(x => Name(x) == name);
            C(int.Parse((string)m.Element("fadeDays")) >= 400 && ((string)m.Element("text")).Contains("{A}") && ((string)m.Element("text")).Contains("{B}"),
                $"{name}: slow to fade, as the child talks it over only once grown, and says who was who");
        }
        C(Code("Social/Talk/Talk.cs").Contains("!p.DevelopmentalStage.Child() && Persona.Eligible(p)"), "children talk only of growing up, so a memory with a child in it waits for them to grow");

        // ================= Quirks =================

        var quirks = Defs("QuirkDefs/Rimfolk_Round5_Quirks.xml").Elements("ColonyCouncil.QuirkDef").ToList();
        C(quirks.Count == 6, $"six quirks ({quirks.Count})");
        var bubblePools = Defs("FlavorPoolDefs/Rimfolk_Round5_Bubbles.xml").Elements("ColonyCouncil.FlavorPoolDef").ToDictionary(Name, p => p);
        var dreams = Defs("DreamDefs/Rimfolk_Round5_Dreams.xml").Elements("ColonyCouncil.DreamDef").ToList();
        var knacks = Defs("HediffDefs/Rimfolk_Round5_Knacks.xml").Elements("HediffDef").ToList();
        var roundThoughts = thoughts.Select(Name).ToHashSet();
        foreach (var q in quirks)
        {
            string name = Name(q);
            C(name.StartsWith("Rimfolk_R5T_Quirk_"), $"{name}: the round's name");
            var after = Lis(q, "afterThoughts");
            C(after.Count > 0 && after.All(thoughtNames.Contains), $"{name}: picked up after memories that exist");
            C(after.Any(roundThoughts.Contains), $"{name}: one of the round's moments can give it");
            C(q.Element("byNature") == null && q.Element("afterBattle") == null && q.Element("condition") == null, $"{name}: only from what happened between people, and no mood of its own");
            float chance = float.Parse((string)q.Element("chance") ?? "0.1", CultureInfo.InvariantCulture);
            float days = float.Parse((string)q.Element("days") ?? "0", CultureInfo.InvariantCulture);
            C(chance > 0f && chance <= 0.08f && days > 0f && days <= 300f, $"{name}: comes seldom and fades in its time");
            C(Lines(q.Element("gained")).Select(x => x.tone).Distinct().Count() == 6 && Lines(q.Element("faded")).Any(), $"{name}: gained in six voices, and a word when it fades");
            foreach (var (tone, line) in Lines(q.Element("gained")).Concat(Lines(q.Element("faded"))))
            {
                string body = Strip(line, out var ts);
                C(ts.All(t => t == "child" || eras.Contains(t)), $"{name}: only the age's tags and [child] in \"{line}\"");
                C(Plain(body), $"{name}: plain words in \"{body}\"");
            }
            var m = Regex.Match(code, "add\\(\"" + name + "\", \"([A-Za-z0-9_]+)\"");
            C(m.Success && bubblePools.ContainsKey(m.Groups[1].Value), $"{name}: shows over a head now and then, from a pool that exists");
            string tag = "quirk:" + name;
            C(dreams.Any(d => ((string)d.Element("dreamer")?.Element("tags") ?? "").Split('|').Contains(tag)), $"{name}: dreams of its own");
            string hediff = (string)q.Element("hediff");
            if (hediff != null) C(knacks.Any(k => Name(k) == hediff && (string)k.Attribute("ParentName") == "Council_QuirkKnackBase"), $"{name}: its knack {hediff} is a habit's knack");
        }
        C(quirks.Count(q => q.Element("hediff") != null) == 2, "two of them have a small knack");
        var follows = quirks.First(q => Name(q) == "Rimfolk_R5T_Quirk_FollowsTheOld");
        C(follows.Element("who")?.Element("child")?.Value == "true" && (string)follows.Element("children") == "true", "following the old ones about is a child's");
        foreach (var k in knacks)
            foreach (var stat in k.Descendants("statOffsets").SelectMany(s => s.Elements()).Select(s => s.Name.LocalName).Distinct())
                C(active.Contains("StatDef|" + stat), $"{Name(k)}: the stat {stat} is the game's");
        foreach (var (pool, p) in bubblePools)
        {
            var lines = p.Descendants("li").Select(li => li.Value).ToList();
            C(lines.Count >= 6, $"{pool}: six lines or more ({lines.Count})");
            foreach (var line in lines)
            {
                string body = Strip(line, out _);
                C(OneSentence(body) && body.Length <= 90 && !body.StartsWith("\"") && body.Count(ch => ch == '!') <= 1, $"{pool}: one short sentence said over a head: \"{body}\"");
                C(!Regex.IsMatch(body, @"\{"), $"{pool}: nothing to fill in \"{body}\"");
                C(Plain(body), $"{pool}: plain words in \"{body}\"");
            }
        }
        C(Regex.Matches(code, "add\\(\"Rimfolk_R5T_Quirk_").Count == 6, "each of the six quirks has its word over the head");

        // ================= Dreams =================

        C(dreams.Count >= 3 && dreams.Any(d => (string)d.Element("kind") == "Nightmare"), $"dreams for the quirks, not every one a sweet one ({dreams.Count})");
        foreach (var d in dreams)
        {
            string name = Name(d);
            C(name.StartsWith("Rimfolk_R5T_Dream_"), $"{name}: the round's name");
            var tags = ((string)d.Element("dreamer")?.Element("tags") ?? "").Split('|');
            C(tags.Length > 0 && tags.All(t => quirks.Any(q => "quirk:" + Name(q) == t)), $"{name}: for the round's quirks");
            C(d.Element("lines")?.Elements().Count(t => t.Elements("li").Any()) == 6, $"{name}: in every voice");
            foreach (var (tone, line) in Lines(d.Element("lines")))
            {
                string body = Strip(line, out var ts);
                C(ts.All(t => tags.Contains(t)), $"{name}: a line for one of its quirks or for all of them: \"{line}\"");
                C(Plain(body), $"{name}: plain words in \"{body}\"");
            }
        }

        C(Regex.Matches(File.ReadAllText(Path.Combine(root, "docs", "content-round-5-together.md")), "[Nn]ot a duplicate of").Count >= 20, "the list says what each of the twenty is not a duplicate of");
        return n;
    }
}
