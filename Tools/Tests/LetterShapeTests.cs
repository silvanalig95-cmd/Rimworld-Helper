using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Letters of many shapes (the player, 10 November 2026: "a lot of text variety ... stories or stuff that is told by the
/// different pawns or factions. some might include poems, detailed stories of raider attacks, ideological ramblings ...
/// some could be several pages long, some just boiled down to the necessities"): Source/ColonyCouncil/Letters/
/// LetterKindMath.cs and the pools Defs/FlavorPoolDefs/Rimfolk_Letters_Note, _Lead, _Story, _Raid, _Poem, _Sermon,
/// _Chronicle, _Set, _Hands, _Enclosure, _Diary, _Part. Each shape only for the writers and situations it fits; the
/// weights (most letters a readable length, long ones occasional, notes from the curt, the busy, the hostile and
/// officials), never the same unusual shape twice running, the setting; the no-repeat memory; paging; stories about the
/// colony only from true facts; a raid on a town only from its record, else an old tale; medieval and modern never mixed;
/// every shape in medieval, tribal and plain; the pools' hygiene; whole letters of every shape (printed with
/// RIMFOLK_LETTERS_PRINT=1); the seams.
/// </summary>
public static class LetterShapeTests
{
    private static Dictionary<string, List<string>> pools;

    private static readonly LetterPart[] NewParts =
    {
        LetterPart.Note, LetterPart.Lead, LetterPart.Story, LetterPart.Raid, LetterPart.Poem, LetterPart.Sermon, LetterPart.Chronicle, LetterPart.Set, LetterPart.Hands,
        LetterPart.Enclosure, LetterPart.Diary, LetterPart.Part
    };

    private static readonly Regex Archaic = new Regex(@"\b(thee|thou|thy|thine|hast|hath|doth|dost|ye|thyself|wouldst|shouldst|canst|shalt|mayst|art thou|thou art)\b", RegexOptions.IgnoreCase);
    private static readonly Regex Modern = new Regex(@"\b(you|your|yours|yourself|yourselves)\b", RegexOptions.IgnoreCase);

    public static int Run(string root, Action<bool, string> check, bool print = false)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "letter shapes: " + what);
        }

        pools = Load(root);
        Fitting(C);
        Weights(C);
        Pages(C);
        Memory(C);
        Hygiene(C);
        Truth(C);
        Variants(C);
        Mixing(C);
        Precepts(root, C);
        Samples(C, print);
        Seams(root, C);
        Card(root, C);
        PartTwo(C);
        return n;
    }

    // ================= The player's card, 10 November 2026 =================

    /// <summary>
    /// What the player's screenshots of received letters showed: the letter on its card drawn as the letter it is, a
    /// favour's goods told twice and called a gift, "36 medicinal cactipine quill", a reply from the card, a signature
    /// "a trader who called of Karond-Keth 2", a trader's salutation "from the boss".
    /// </summary>
    private static void Card(string root, Action<bool, string> C)
    {
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.Exists(Path.Combine(src, rel)) ? File.ReadAllText(Path.Combine(src, rel)) : "";
        var w = new LetterWords();
        // 1. The letter on paper: the teller's words before it, the writer's own words, the note where the rest is, the narrator's lines.
        string kept = "Ashford, 7 Septober 5502\n\nStace, my friend,\n\nYour letter came, and we read it twice over.\n\nYour friend,\nDagna";
        string shown = "At Riverhold.\n\n" + kept + "\n\n" + Texts.Fill(w.goesOnMany, ("N", "2"), ("NAME", "Dagna")) + "\n\n" + w.mark + "\n\nDagna thinks better of the colony for it.";
        C(LetterFormMath.OnPaper(shown, kept, w, out string before, out string paper, out string more, out string under)
          && before == "At Riverhold." && paper == kept && more.StartsWith("(The letter goes on for 2 more pages") && under == "Dagna thinks better of the colony for it.",
            $"a received letter on its card: the teller's words, the letter, the note, the narrator's lines ({before} | {paper} | {more} | {under})");
        C(LetterFormMath.OnPaper(kept + "\n\n" + w.mark + "\n\nA line.", kept, w, out _, out string p2, out string m2, out string u2) && p2 == kept && m2 == "" && u2 == "A line.",
            "a letter shown whole: no note");
        C(!LetterFormMath.OnPaper("Something else entirely.", kept, w, out _, out _, out _, out _), "a card whose text isn't the kept letter is drawn plain");
        string model = Code("UI/EventCards/EventCardModel.cs"), card = Code("UI/EventCards/EventCardWindow.cs");
        C(model.Contains("LetterFormMath.OnPaper(text, told.text, LetterForm.Words") && model.Contains("told.shape != (byte)LetterShape.Written") && model.Contains("told.ours"),
            "only a received letter written out goes on paper, never a call's transcript or words passed on");
        C(card.Contains("LetterPaper.Draw(sheet, m.paper, paperH)") && card.Contains("m.paperUnder") && Code("Letters/LetterReadWindow.cs").Contains("LetterPaper.Draw(sheet"),
            "the card's paper is the reading window's");
        // 2. A favour's goods are told by its own line, never again as a gift.
        string nl = Code("World/NeighbourLetters.cs");
        C(nl.Contains("var gifts = new List<Thing>();") && nl.Contains("things.AddRange(gifts);") && nl.Contains("Couriers.Describe(gifts).Named(\"GOODS\")")
          && !nl.Contains("\"CC_Letter_Gift\").Translate(Couriers.Describe(things)"), "only what is sent as a gift is called a gift");
        C(nl.Contains("if (dip != null) things.AddRange(dip.things);") && !nl.Contains("if (dip != null) gifts.AddRange"), "a favour's, a dealing's goods are never among the gifts");
        // 3. Goods counted in plural words.
        C(CourierMath.Counted(36, "medicinal cactipine quill", false) == "medicinal cactipine quills", "36 medicinal cactipine quills");
        C(CourierMath.Counted(1, "medicinal cactipine quill", false) == "medicinal cactipine quill", "one quill");
        C(CourierMath.Counted(12, "herbal medicine", false) == "herbal medicine" && CourierMath.Counted(50, "silver", false) == "silver" && CourierMath.Counted(75, "cow meat", false) == "cow meat",
            "a mass keeps its form");
        C(CourierMath.Counted(40, "granite blocks", true) == "granite blocks" && CourierMath.Counted(40, "plainleather", true) == "plainleather", "a stuff keeps its form");
        C(CourierMath.Counted(30, "potatoes", false) == "potatoes" && CourierMath.Counted(4, "mech parts", false) == "mech parts", "a label already plural keeps its form");
        C(CourierMath.Counted(3, "berry", false) == "berries" && CourierMath.Counted(3, "match box", false) == "match boxes" && CourierMath.Counted(3, "component", false) == "components"
          && CourierMath.Counted(3, "packaged survival meal", false) == "packaged survival meals", "things counted one by one in the plural");
        C(CourierMath.Counted(3, "quill", false, (s, k) => s + "-pl") == "quill-pl", "the game's own plural when given");
        C(Code("Couriers/Couriers.cs").Contains("Counted(n, first.def.label, first.def)") && Code("Couriers/Couriers.cs").Contains("CourierMath.Counted(n, label, def?.IsStuff ?? false, Find.ActiveLanguageWorker.Pluralize)")
          && Code("Diplomacy/Schemes.cs").Contains("Couriers.Counted(t.stackCount, t.def.label, t.def)"), "goods counted in words go through the one rule");
        // 4. Write back from the card, the reading window and the People page.
        C(LetterKindMath.ReplyOf(true, true) == ReplyAs.Answer && LetterKindMath.ReplyOf(false, true) == ReplyAs.Greetings && LetterKindMath.ReplyOf(false, false) == ReplyAs.Any,
            "a reply answers their letter when it waits, else greets, else whatever can go");
        string reply = Code("Letters/LetterReply.cs");
        C(reply.Contains("DealingActs.Reply(n)") && reply.Contains("new Dialog_NeighbourLetter(n, LetterPurpose.Greetings, null)") && reply.Contains("NeighbourLetters.ChannelWhyNot(n, LetterChannel.Courier")
          && reply.Contains("NeighbourLetters.Writers(map).Count == 0") && reply.Contains("NeighboursPanel.WhyNoLetter(n)") && reply.Contains("o.Disable(why)"),
            "writing back opens the right letter, greyed with why");
        C(model.Contains("LetterReply.Act(letter") && Code("Letters/LetterReadWindow.cs").Contains("LetterReply.Open(with)") && Code("UI/Region/PeoplePage.cs").Contains("LetterReply.Open(n)"),
            "the act on the card, in the reading window and on the People page");
        string dealingKeys = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Dealings.xml"));
        C(Regex.IsMatch(dealingKeys, "<CC_World_HowToAnswer>[^<]*\"Write back\" on this card"), "the world's letter names the card's act");
        // 5. A letter signs, greets and heads with an office only, never with how the colony knows them.
        foreach (NotableRole role in Enum.GetValues(typeof(NotableRole)))
            foreach (var met in new[] { "freed", "tended", "camp", "guest", "trader", "visitor", "envoy", null })
            {
                var office = NotableMath.Office(role, met);
                if (role != NotableRole.Acquaintance) C(office == role, $"a {role}'s office is their role's");
                else C(met == "trader" ? office == NotableRole.Trader : met == "envoy" ? office == NotableRole.Envoy : office == null, $"one met as {met ?? "nobody"}: {office}");
            }
        var keys = XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Notables.xml")).Root!.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
        var offices = keys.Where(kv => kv.Key.StartsWith("CC_Notable_Title_") && !kv.Key.StartsWith("CC_Notable_Title_Met_")).ToList();
        C(offices.Count >= 14, "the offices' words: " + offices.Count);
        foreach (var (k, v) in offices)
        {
            C(!Regex.IsMatch(v, @"\b(who|by|once|a|an|the)\b") && !v.Contains(" "), $"an office is a word, not a description ({k}: {v})");
            string sign = LetterFormMath.Signature(SignStyle.Person, w, new Dictionary<string, string> { ["FROM"] = "Acraeraes", ["TITLE"] = v, ["TOWN"] = "Karond-Keth 2" });
            C(sign == $"Acraeraes, {v} of Karond-Keth 2", "signed with an office: " + sign);
        }
        C(LetterFormMath.Signature(SignStyle.PersonUntitled, w, new Dictionary<string, string> { ["FROM"] = "Acraeraes", ["TOWN"] = "Karond-Keth 2" }) == "Acraeraes of Karond-Keth 2",
            "one with no office signs with name and town");
        foreach (var (k, v) in keys.Where(kv => kv.Key.StartsWith("CC_Notable_Title_Met_")))
            C(Regex.IsMatch(v, @"\b(who|by|once|a)\b"), $"how the colony met them is a description, never signed ({k}: {v})");
        string form = Code("Letters/LetterForm.cs");
        C(form.Contains("string title = NotableBook.OfficeOf(n);") && form.Contains("string office = NotableBook.OfficeOf(n);") && !form.Contains("NotableBook.TitleOf(n)"),
            "the letter's form takes the office, never TitleOf's description");
        C(Code("World/Notables.cs").Contains("public static string OfficeOf(Notable n)") && form.Contains("s.tags.Add(\"nr:\" + n.role.ToString().ToLowerInvariant());"), "the office, and who writes in their town");
        // 6. A line that names the writer as their people's boss is the head's; one that says the boss bid them write is anyone else's.
        foreach (var part in new[] { LetterPart.Salute, LetterPart.Close, LetterPart.Open })
            foreach (var l in pools.TryGetValue(LetterFormMath.PoolOf(part), out var ls) ? ls : new List<string>())
            {
                var p = P(l);
                if (Regex.IsMatch(p.text, @"\bfrom the boss\b|\bthe boss, wich is me\b|\bfrom the boss,", RegexOptions.IgnoreCase))
                    C(p.tags.Contains("nr:head") && Has(p, "theirs"), "a writer who calls themselves the boss is their head: " + p.text);
                if (Regex.IsMatch(p.text, @"\bthe boss says\b", RegexOptions.IgnoreCase)) C(p.tags.Contains("!nr:head"), "the boss bid them write: not the head: " + p.text);
            }
    }

    // ================= Round 2, part 2: the content pass =================

    /// <summary>
    /// The second content pass (11 November 2026): every story theme in medieval English and plain; the peoples' everyday
    /// life for every calling; a raid on their road and the colony's own attack told whole; the Empire's gazette and court
    /// letters; each faith's sermons; the strange voices in the shapes opened to them; the familiarity tags the pieces use
    /// only the stages'; the library at its new size.
    /// </summary>
    private static void PartTwo(Action<bool, string> C)
    {
        string Speech(string l) => LetterFormMath.SpeechOf(P(l));
        foreach (var theme in LetterKindMath.Themes)
            foreach (var part in new[] { "st:open", "st:turn", "st:end" })
                C(Lines(LetterPart.Story).Count(l => Has(P(l), theme) && Has(P(l), part) && Speech(l) == LetterFormMath.Olde) >= 2, $"medieval {part} lines for {theme}");
        // Every people's calling has days of its own in a long letter; a medieval people's in medieval English.
        var medieval = new[] { "healers", "pilgrims", "hunters", "sextons", "mercenaries", "artisans", "fishers" };
        foreach (var calling in PeoplesMath.Callings)
        {
            var mine = Lines(LetterPart.Part).Where(l => Has(P(l), "pa:life") && Has(P(l), "their:" + calling)).ToList();
            C(mine.Count >= 1, "everyday life for their:" + calling);
            if (medieval.Contains(calling)) C(mine.Any(l => Speech(l) == LetterFormMath.Olde), "a medieval people's days in medieval English: " + calling);
        }
        // A raid on their road and the colony's own attack, told whole.
        foreach (var (rec, what) in new[] { ("rd:road", "on their road"), ("rd:you", "by the colony") })
            foreach (var part in new[] { "rd:come", "rd:stand", "rd:end", "rd:after" })
                C(Lines(LetterPart.Raid).Count(l => Has(P(l), rec) && Has(P(l), part) && Speech(l) == null) >= 2, $"a raid {what}: {part}");
        foreach (var end in new[] { "held", "hurt", "sacked", "ongoing", "unknown" })
            C(Lines(LetterPart.Raid).Any(l => Has(P(l), "rd:road") && Has(P(l), "rd:end:" + end)), "a road's raid that ended " + end);
        // The Empire's own pieces: its gazette, its court letters' parts, its court verse, its notes.
        C(Lines(LetterPart.Chronicle).Count(l => Has(P(l), "empire") && Has(P(l), "ch:head")) >= 3 && Lines(LetterPart.Chronicle).Count(l => Has(P(l), "empire") && Has(P(l), "ch:item")) >= 8
          && Lines(LetterPart.Chronicle).Count(l => Has(P(l), "empire") && Has(P(l), "ch:notice")) >= 3, "the Empire's gazette");
        C(Lines(LetterPart.Part).Count(l => Has(P(l), "empire")) >= 5 && Lines(LetterPart.Poem).Count(l => Has(P(l), "empire")) >= 6 && Lines(LetterPart.Note).Count(l => Has(P(l), "empire") || Has(P(l), "a:empire")) >= 4,
            "the Empire's court letters, verse and notes");
        // Each faith's sermons, by its memes' tags.
        foreach (var tag in LetterCultureMath.MemeTags.Values.SelectMany(x => x).Distinct())
            C(Lines(LetterPart.Sermon).Count(l => Has(P(l), tag)) >= 2, "sermons for " + tag);
        // A long letter never says one paragraph or heading twice, in any speech.
        foreach (var speech in new[] { null, LetterFormMath.Olde })
            for (int seed = 0; seed < 150; seed++)
            {
                var s = For(LetterKind.Long, speech, WorldAge.Industrial);
                s.length = LetterLength.Long;
                if (Make(s, LetterKind.Long, 9000 + seed) == null) continue;
                C(s.piece.Count == s.piece.Distinct().Count(), "a long letter with a paragraph or heading twice: " + string.Join(" / ", s.piece.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key)));
            }
        // The crier's roll and a tribe's season: an item on most subjects in each.
        foreach (var (tag, speech, what) in new[] { ((string)null, LetterFormMath.Olde, "the crier's roll"), ("w:tribal", (string)null, "a tribe's season told") })
        {
            int subjects = LetterKindMath.Subjects.Count(subj => Lines(LetterPart.Chronicle).Any(l => Has(P(l), subj) && Speech(l) == speech && (tag == null || Has(P(l), tag))));
            C(subjects >= 12, $"{what} tells of {subjects} subjects");
        }
        // The familiarity the pieces read is only the stages' (Letters/LetterThreadMath.cs).
        var stages = new HashSet<string>(LetterThreadMath.Stages.Select(LetterThreadMath.Tag)) { LetterThreadMath.LongTag };
        foreach (var (part, line) in All())
            foreach (var t in P(line).tags)
                foreach (var a in t.Split('|').Select(x => x.TrimStart('!')).Where(x => x.StartsWith("fam:")))
                    C(stages.Contains(a), $"a known stage {a}: {P(line).text}");
        C(All().Count(x => P(x.line).tags.Any(t => t.Contains("fam:"))) >= 25, "pieces that read how well the two know each other");
        // The strange voices in the shapes opened to them this round.
        foreach (var (k, voices) in new[] { (LetterKind.Poem, new[] { LetterCultureMath.Beast, LetterCultureMath.Hive, LetterCultureMath.Deep }),
                     (LetterKind.Recipe, new[] { LetterCultureMath.Machine, LetterCultureMath.Green, LetterCultureMath.Beast }),
                     (LetterKind.Riddle, new[] { LetterCultureMath.Machine, LetterCultureMath.Deep }),
                     (LetterKind.Enclosure, new[] { LetterCultureMath.Green, LetterCultureMath.Machine, LetterCultureMath.Ancient, LetterCultureMath.Hive }),
                     (LetterKind.Diary, new[] { LetterCultureMath.Machine, LetterCultureMath.Beast, LetterCultureMath.Green }),
                     (LetterKind.Sermon, new[] { LetterCultureMath.Hive }) })
            foreach (var v in voices)
                C(LetterKindMath.SpeaksIn(k, v), $"{v} writes {k}");
        // The library, at its new size (the second content pass, about a thousand lines more).
        var counts = NewParts.ToDictionary(x => x, x => Lines(x).Count);
        C(counts[LetterPart.Poem] >= 160 && counts[LetterPart.Sermon] >= 120 && counts[LetterPart.Story] >= 380 && counts[LetterPart.Raid] >= 220 && counts[LetterPart.Chronicle] >= 210
          && counts[LetterPart.Set] >= 200 && counts[LetterPart.Lead] >= 170 && counts[LetterPart.Part] >= 170 && counts[LetterPart.Diary] >= 110 && counts[LetterPart.Note] >= 90
          && counts[LetterPart.Enclosure] >= 80 && counts[LetterPart.Hands] >= 70, "the library after the second pass: " + string.Join(", ", counts.Select(kv => kv.Key + " " + kv.Value)));
    }

    // ================= Pools and specs =================

    private static Dictionary<string, List<string>> Load(string root)
    {
        var map = new Dictionary<string, List<string>>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Defs", "FlavorPoolDefs"), "Rimfolk_Letters_*.xml"))
        {
            var doc = XDocument.Load(f);
            foreach (var d in doc.Root!.Elements())
            {
                string name = d.Element("defName")?.Value;
                if (name == null) continue;
                if (!map.TryGetValue(name, out var list)) map[name] = list = new List<string>();
                foreach (var li in d.Element("lines")?.Element("plain")?.Elements("li") ?? Enumerable.Empty<XElement>()) list.Add(li.Value);
            }
        }
        return map;
    }

    private static List<string> Lines(LetterPart p) => pools.TryGetValue(LetterFormMath.PoolOf(p), out var l) ? l : new List<string>();

    private static LineTags.Parsed P(string line) => LineTags.Of(line);

    private static bool Has(LineTags.Parsed p, string tag) => p.tags != null && p.tags.Any(t => t.Split('|').Contains(tag));

    private static IEnumerable<(LetterPart part, string line)> All()
    {
        foreach (var p in NewParts)
            foreach (var l in Lines(p))
                yield return (p, l);
    }

    private static Dictionary<string, string> Base() => new Dictionary<string, string>
    {
        ["FROM"] = "Dagna", ["TO"] = "Stace", ["TOWN"] = "Ashford", ["COLONY"] = "Riverhold", ["PEOPLE"] = "the Rust Kin", ["COUNCIL"] = "the council of Ashford",
        ["TOTITLE"] = "Baroness", ["TITLE"] = "headwoman", ["PURPOSE"] = "greetings", ["ANIMAL"] = "elk", ["ANANIMAL"] = "an elk", ["ANIMALS"] = "elk", ["PLANT"] = "healroot", ["TREE"] = "oak tree",
        ["DAY1"] = "The 3rd day.", ["DAY2"] = "The 4th day.", ["DAY3"] = "The 5th day.", ["SIGNERS"] = "Signed: Dagna, Pell and Orrin, and many more of Ashford besides.",
        ["GOODS"] = "medicine", ["HELP"] = "food", ["IDEO"] = "the Way of Ash", ["DEITY"] = "the Sower"
    };

    /// <summary>A letter of theirs, written, by the situation and the writer's speech (null plain, olde, u:none, u:some, sv:...).</summary>
    private static LetterSpec Spec(string purpose = "greetings", AnswerTone tone = AnswerTone.Warm, WorldAge age = WorldAge.Industrial, string speech = null,
        LetterMotive motive = LetterMotive.Plain, bool ours = false)
    {
        var s = new LetterSpec { ours = ours, purpose = purpose, tone = tone, age = age, motive = motive, shape = LetterShape.Written, who = new LetterWho() };
        if (speech == LetterFormMath.Olde) s.age = WorldAge.Medieval;
        else if (speech == LetterFormMath.NoSchool) s.schooling = Schooling.None;
        else if (speech == LetterFormMath.SomeSchool) s.schooling = Schooling.Some;
        else if (speech != null) s.strange = speech;
        if (speech != LetterFormMath.Olde && s.age == WorldAge.Medieval) s.age = WorldAge.Industrial;
        foreach (var kv in Base()) s.values[kv.Key] = kv.Value;
        return s;
    }

    private static LetterSpec Talker(string speech = null, WorldAge age = WorldAge.Industrial)
    {
        var s = Spec(age: age, speech: speech, motive: LetterMotive.Friend);
        s.penFriend = true;
        s.humour = 60;
        s.who.talkative = s.who.lonely = s.who.artistic = s.who.zealous = true;
        s.who.news = true;
        s.who.hands.Add("Pell");
        s.who.hands.Add("Orrin");
        return s;
    }

    /// <summary>The pools as the shapes draw on them, drawn lines kept in <paramref name="drawn"/> (as written, with their tags).</summary>
    private static LetterLines Lines(LetterSpec s, Random r, List<(LetterPart part, string line)> drawn = null) => new LetterLines
    {
        pick = (part, tags) =>
        {
            var lines = Lines(part);
            string text = LetterFormMath.Pick(lines, tags, s.values, null, () => (float)r.NextDouble());
            if (text == null) return null;
            if (drawn != null)
                foreach (var l in lines)
                    if (P(l).text == text && LetterFormMath.Fits(l, tags) >= 0)
                    {
                        drawn.Add((part, l));
                        break;
                    }
            return Texts.Fill(text, s.values);
        },
        has = (part, tags) => LetterFormMath.Any(Lines(part), tags, s.values)
    };

    private static Func<LetterPart, HashSet<string>, string> Pick(LetterSpec s, Random r, List<(LetterPart part, string line)> drawn = null)
    {
        var l = Lines(s, r, drawn);
        return (part, tags) => l.Pick(part, tags);
    }

    /// <summary>The shape built and the letter made, or null when the shape couldn't be built.</summary>
    private static LetterText Make(LetterSpec s, LetterKind k, int seed, List<(LetterPart part, string line)> drawn = null)
    {
        var r = new Random(seed);
        s.kind = k;
        s.piece.Clear();
        s.after.Clear();
        s.riddleAnswer = null;
        if (!LetterKindMath.Build(s, new LetterWords(), Lines(s, r, drawn), () => (float)r.NextDouble())) return null;
        return LetterFormMath.Compose(s, new LetterWords(), Pick(s, r, drawn), () => (float)r.NextDouble());
    }

    private static string Piece(LetterSpec s) => string.Join("\n\n", s.piece.Concat(s.after));

    // ================= Which shapes fit =================

    private static void Fitting(Action<bool, string> C)
    {
        var kinds = LetterKindMath.Kinds;
        var plain = Spec();
        C(LetterKindMath.Fits(LetterKind.Usual, plain) && LetterKindMath.Fits(LetterKind.Note, plain), "the usual letter and a bare note fit anyone");
        foreach (var shape in new[] { LetterShape.Call, LetterShape.Spoken })
        {
            var t = Talker();
            t.shape = shape;
            C(kinds.All(k => LetterKindMath.Fits(k, t) == (k == LetterKind.Usual)), $"a {shape} keeps its own shape");
        }
        var ancient = Talker(LetterCultureMath.Ancient);
        C(!LetterKindMath.Fits(LetterKind.Note, ancient), "the ancient never write a bare note");
        // At war: a letter or a note, or a raider's boast.
        var war = Talker();
        war.war = true;
        war.tone = AnswerTone.Curt;
        C(kinds.Where(k => LetterKindMath.Fits(k, war)).SequenceEqual(new[] { LetterKind.Usual, LetterKind.Note }), "at war, only a letter or a note");
        war.who.boast = true;
        C(LetterKindMath.Fits(LetterKind.Raid, war), "at war, a raider's boast of their raid on the colony");
        // The business between peoples, a council, a refusal: a letter or a note.
        foreach (var purpose in new[] { "pact", "truce", "ransom", "envoy", "ward", "fealty", "takeback", "council", "favour" })
        {
            var b = Talker();
            b.purpose = purpose;
            if (purpose == "council") b.council = true;
            C(kinds.Where(k => LetterKindMath.Fits(k, b)).SequenceEqual(new[] { LetterKind.Usual, LetterKind.Note }), "the business of " + purpose + " is a letter or a note");
        }
        var refusal = Talker();
        refusal.tone = AnswerTone.Refusal;
        C(kinds.Where(k => LetterKindMath.Fits(k, refusal)).SequenceEqual(new[] { LetterKind.Usual, LetterKind.Note }), "a refusal is a letter or a note");
        // Each shape by its writers.
        var nobody = Spec();
        C(!LetterKindMath.Fits(LetterKind.Long, nobody) && !LetterKindMath.Fits(LetterKind.Diary, nobody), "a plain neighbour writes no long letter or diary");
        foreach (var (what, set) in new (string, Action<LetterSpec>)[]
                 {
                     ("talkative", s => s.who.talkative = true), ("lonely", s => s.who.lonely = true), ("zealous", s => s.who.zealous = true), ("a pen friend", s => s.penFriend = true)
                 })
        {
            var s = Spec();
            set(s);
            C(LetterKindMath.Fits(LetterKind.Long, s), "a long letter from the " + what);
        }
        var cool = Talker();
        cool.tone = AnswerTone.Cool;
        C(!LetterKindMath.Fits(LetterKind.Long, cool) && !LetterKindMath.Fits(LetterKind.Story, cool), "no long, storied letter in a cool tone");
        C(!LetterKindMath.Fits(LetterKind.Poem, nobody) && LetterKindMath.Fits(LetterKind.Poem, Artist()), "poems from artistic writers");
        C(LetterKindMath.Fits(LetterKind.Poem, Spec(speech: LetterCultureMath.Fey)) && LetterKindMath.Fits(LetterKind.Poem, Spec(speech: LetterCultureMath.Green)), "the fey and the plant folk sing");
        var zealot = Spec();
        zealot.who.zealous = true;
        C(!LetterKindMath.Fits(LetterKind.Sermon, nobody) && LetterKindMath.Fits(LetterKind.Sermon, zealot), "sermons from the zealous");
        // A raid's account: from a record; else an old tale from those who tell tales; never at a curt letter's but a boast.
        C(!LetterKindMath.Fits(LetterKind.Raid, nobody), "no raid's account from a plain neighbour of an unraided town");
        var raided = Spec(tone: AnswerTone.Cordial);
        raided.who.raid = new RaidRecord { days = 5, end = RaidEnd.Held, dead = 0 };
        C(LetterKindMath.Fits(LetterKind.Raid, raided), "a raid's account from a town raided in fact");
        var teller = Spec();
        teller.who.talkative = true;
        C(LetterKindMath.Fits(LetterKind.Raid, teller), "an old tale of a raid from a talker");
        var martial = Spec();
        martial.tags.Add("c:martial");
        C(LetterKindMath.Fits(LetterKind.Raid, martial) && LetterKindMath.Fits(LetterKind.Raid, Spec(age: WorldAge.Tribal)), "and from fighters' peoples and tribes");
        var curtRaid = Spec(tone: AnswerTone.Curt);
        curtRaid.who.raid = raided.who.raid;
        C(!LetterKindMath.Fits(LetterKind.Raid, curtRaid), "no raid's account in a curt letter but a boast");
        // A chronicle where there is news or from a town's head; a petition only where something is asked.
        C(!LetterKindMath.Fits(LetterKind.Chronicle, nobody), "no chronicle without news");
        var head = Spec(tone: AnswerTone.Cool);
        head.who.busy = true;
        C(LetterKindMath.Fits(LetterKind.Chronicle, head), "a town's head sends the sheet");
        var asks = Spec("first", AnswerTone.Cordial);
        asks.tags.Add("k:request");
        asks.grave = true;
        C(LetterKindMath.Fits(LetterKind.Petition, asks) && !LetterKindMath.Fits(LetterKind.Petition, Talker()), "a petition only where something is asked");
        C(LetterKindMath.Fits(LetterKind.Account, asks), "a list of what they need");
        var remedy = Spec("help", AnswerTone.Cool);
        remedy.tags.Add("medicine");
        C(LetterKindMath.Fits(LetterKind.Recipe, remedy), "a remedy in place of the medicine they can't send");
        var dour = Talker();
        dour.humour = -50;
        C(!LetterKindMath.Fits(LetterKind.Riddle, dour) && LetterKindMath.Fits(LetterKind.Riddle, Talker()), "riddles from the merry");
        var alone = Talker();
        alone.who.hands.Clear();
        C(!LetterKindMath.Fits(LetterKind.Hands, alone), "no other hands without anyone at home");
        var grave = Talker();
        grave.purpose = "condolences";
        C(LetterKindMath.Fits(LetterKind.Poem, grave) && !LetterKindMath.Fits(LetterKind.Story, grave) && !LetterKindMath.Fits(LetterKind.Riddle, grave)
          && !LetterKindMath.Fits(LetterKind.Recipe, grave), "a condolence: a lament, never a story, a riddle or a recipe");
        // Each speech only the shapes it has pieces in.
        foreach (var speech in LetterFormMath.Speeches.Where(x => x != null))
        {
            var s = Talker(speech);
            foreach (var k in kinds)
                if (LetterKindMath.Fits(k, s))
                    C(LetterKindMath.SpeaksIn(k, speech), $"{speech} writes {k} only in its own pieces");
        }
        var hive = Talker(LetterCultureMath.Hive);
        C(kinds.Where(k => LetterKindMath.Fits(k, hive)).All(k => k == LetterKind.Usual || k == LetterKind.Note || k == LetterKind.Chronicle || k == LetterKind.Poem || k == LetterKind.Sermon
                                                       || k == LetterKind.Enclosure), "a hive writes a letter, a note, the many's count, its hum, its creed or what it folds in");
        var some = Talker(LetterFormMath.SomeSchool);
        C(LetterKindMath.Fits(LetterKind.Story, some) && LetterKindMath.PieceSpeech(LetterFormMath.SomeSchool) == null, "a barely schooled hand writes the plain pieces");
        // The colony's own: a verse from a poet, a zealot's sermon, a note, other hands, an enclosure, the cook's recipe, a petition for help, its own raid.
        var ours = Spec(ours: true);
        ours.who.artistic = ours.who.zealous = ours.who.cook = ours.who.talkative = ours.who.lonely = true;
        ours.who.hands.Add("Hessa");
        ours.who.news = true;
        ours.humour = 90;
        var oursKinds = kinds.Where(k => LetterKindMath.Fits(k, ours)).ToList();
        C(oursKinds.SequenceEqual(new[] { LetterKind.Usual, LetterKind.Note, LetterKind.Poem, LetterKind.Sermon, LetterKind.Recipe, LetterKind.Hands, LetterKind.Enclosure }),
            "the colony's own: " + string.Join(", ", oursKinds));
        C(!LetterKindMath.Fits(LetterKind.Raid, ours), "the colony tells no raid of its own without the facts");
        ours.who.raid = new RaidRecord { ours = true, days = 3 };
        C(LetterKindMath.Fits(LetterKind.Raid, ours), "the colony's own raid, as the facts have it");
        var oursHelp = Spec("help", AnswerTone.Cordial, ours: true);
        oursHelp.who.signers.AddRange(new[] { "Stace", "Hessa", "Bautista" });
        C(LetterKindMath.Fits(LetterKind.Petition, oursHelp), "the colony's petition for help, signed by all");
        // Never the same unusual shape twice running; the usual may come again.
        var last = Talker();
        last.who.last = LetterKind.Poem;
        C(LetterKindMath.Weight(LetterKind.Poem, last, LetterLength.Mixed) == 0f && LetterKindMath.Weight(LetterKind.Story, last, LetterLength.Mixed) > 0f,
            "not the same shape twice running from one writer");
        last.who.last = LetterKind.Usual;
        C(LetterKindMath.Weight(LetterKind.Usual, last, LetterLength.Mixed) > 0f, "the usual letter can come again");
        // The setting.
        var t2 = Talker();
        foreach (var k in LetterKindMath.LongKinds)
        {
            C(LetterKindMath.Weight(k, t2, LetterLength.Short) == 0f, "Short: no " + k);
            float mixed = LetterKindMath.Weight(k, t2, LetterLength.Mixed);
            if (mixed > 0f) C(Math.Abs(LetterKindMath.Weight(k, t2, LetterLength.Long) - mixed * 2.5f) < 0.001f, "Long and storied: " + k + " two and a half times");
        }
        C(LetterKindMath.Weight(LetterKind.Note, Spec(tone: AnswerTone.Curt), LetterLength.Short) > LetterKindMath.Weight(LetterKind.Note, Spec(tone: AnswerTone.Curt), LetterLength.Mixed),
            "Short: notes more often");
    }

    private static LetterSpec Artist()
    {
        var s = Spec();
        s.who.artistic = true;
        return s;
    }

    // ================= The weights =================

    private static (float usual, float note, float longShare, Dictionary<LetterKind, float> share) Shares(LetterSpec s, LetterLength length)
    {
        var w = LetterKindMath.Kinds.ToDictionary(k => k, k => LetterKindMath.Weight(k, s, length));
        float total = w.Values.Sum();
        var share = w.ToDictionary(kv => kv.Key, kv => kv.Value / total);
        return (share[LetterKind.Usual], share[LetterKind.Note], LetterKindMath.LongKinds.Sum(k => share[k]), share);
    }

    private static void Weights(Action<bool, string> C)
    {
        // A talkative pen friend with family at home (no poet, no zealot).
        var friend = Spec(motive: LetterMotive.Friend);
        friend.penFriend = true;
        friend.humour = 30;
        friend.who.talkative = true;
        friend.who.hands.Add("Pell");
        var f = Shares(friend, LetterLength.Mixed);
        var all = Shares(Talker(), LetterLength.Mixed);
        C(all.usual >= 0.3f && all.longShare <= 0.45f, $"even a writer of every gift writes the usual letter often (usual {all.usual:P0}, long {all.longShare:P0})");
        C(f.usual >= 0.4f && f.longShare >= 0.1f && f.longShare <= 0.4f, $"a talkative pen friend: mostly a readable length, long ones as treats (usual {f.usual:P0}, long {f.longShare:P0})");
        var plain = Spec(tone: AnswerTone.Cordial);
        var p = Shares(plain, LetterLength.Mixed);
        C(p.usual >= 0.75f && p.longShare <= 0.05f, $"a plain neighbour writes the usual letter (usual {p.usual:P0}, long {p.longShare:P0})");
        var curt = Spec(tone: AnswerTone.Curt);
        C(Shares(curt, LetterLength.Mixed).note >= 0.25f, $"the curt write bare notes ({Shares(curt, LetterLength.Mixed).note:P0})");
        var warLetter = Spec(tone: AnswerTone.Cool);
        warLetter.war = true;
        C(Shares(warLetter, LetterLength.Mixed).note >= 0.25f, "the hostile write bare notes");
        var official = Spec("council", AnswerTone.Cordial, motive: LetterMotive.Duty);
        official.council = true;
        C(Shares(official, LetterLength.Mixed).note >= 0.2f, $"officials write bare notes ({Shares(official, LetterLength.Mixed).note:P0})");
        var busy = Spec(tone: AnswerTone.Cordial);
        busy.who.busy = true;
        C(Shares(busy, LetterLength.Mixed).note > p.note, "the busy write more notes");
        C(Shares(friend, LetterLength.Mixed).note < p.note, "warm friends write fewer notes");
        var zealot = Spec();
        zealot.who.zealous = true;
        C(Shares(zealot, LetterLength.Mixed).share[LetterKind.Sermon] >= 0.1f, "the zealous preach");
        C(Shares(Artist(), LetterLength.Mixed).share[LetterKind.Poem] >= 0.1f, "the artistic send poems");
        var raided = Spec(tone: AnswerTone.Cordial);
        raided.who.raid = new RaidRecord { days = 5, end = RaidEnd.Hurt, dead = 1 };
        float raidShare = Shares(raided, LetterLength.Mixed).share[LetterKind.Raid];
        C(raidShare >= 0.15f, $"a town raided tells of it ({raidShare:P0})");
        var teller = Spec();
        teller.who.talkative = true;
        C(Shares(teller, LetterLength.Mixed).share[LetterKind.Raid] < raidShare / 2f, "an old tale less often than a real raid");
        C(Shares(friend, LetterLength.Short).longShare == 0f, "Short: no long letter");
        float longer = Shares(friend, LetterLength.Long).longShare;
        C(longer > f.longShare && longer >= 0.35f, $"Long and storied: long ones often ({longer:P0})");
        // The choice follows the weights.
        var counts = LetterKindMath.Kinds.ToDictionary(k => k, k => 0);
        for (int i = 0; i < 10000; i++) counts[LetterKindMath.Choose(friend, LetterLength.Mixed, i / 10000f)]++;
        foreach (var k in LetterKindMath.Kinds)
            C(Math.Abs(counts[k] / 10000f - f.share[k]) < 0.002f, $"the choice follows the weights ({k}: {counts[k]})");
        C(LetterKindMath.Choose(Spec(tone: AnswerTone.Refusal), LetterLength.Mixed, 0.99f) != LetterKind.Story, "a refusal is never a story");
    }

    // ================= Pages =================

    private static void Pages(Action<bool, string> C)
    {
        string Strip(string t) => Regex.Replace(t, @"\s+", "");
        var paras = Enumerable.Range(1, 14).Select(i => $"Paragraph {i}. " + string.Concat(Enumerable.Repeat("Some words of the letter. ", 12))).ToList();
        string text = string.Join("\n\n", paras);
        var pages = LetterKindMath.Pages(text);
        C(pages.Count >= 3 && pages.All(pg => pg.Length <= LetterKindMath.PageChars), $"a long letter in pages of {LetterKindMath.PageChars} at most ({pages.Count})");
        C(Strip(string.Concat(pages)) == Strip(text), "nothing lost in the paging");
        C(pages.All(pg => !pg.StartsWith("\n") && !pg.EndsWith("\n")), "pages break between paragraphs");
        C(LetterKindMath.Pages("A short letter.").Count == 1 && LetterKindMath.Pages("").Count == 0, "a short letter is one page");
        string poem = string.Join("\n", Enumerable.Range(1, 120).Select(i => $"Line {i} of a very long ballad, sung slow"));
        var poemPages = LetterKindMath.Pages(poem);
        C(poemPages.Count > 1 && poemPages.All(pg => pg.Length <= LetterKindMath.PageChars) && Strip(string.Concat(poemPages)) == Strip(poem), "a poem too long for a page breaks between its lines");
        string run = string.Concat(Enumerable.Repeat("One long sentence that runs on. ", 120));
        var runPages = LetterKindMath.Pages(run.Trim());
        C(runPages.Count > 1 && runPages.All(pg => pg.Length <= LetterKindMath.PageChars), "a paragraph too long breaks after its sentences");
        // The game's own letter shows the first page of a long one, and says where the rest is.
        var s = Spec();
        s.heart = text;
        s.outcomes.Add("Dagna thinks better of the colony for it.");
        var t = LetterFormMath.Compose(s, new LetterWords(), (part, tags) => null, () => 0.99f);
        C(t.cut && t.full.StartsWith(t.pages[0]) && t.full.Contains("more pages") && t.full.Contains("Dagna thinks better") && !t.full.Contains(paras[13].Trim()),
            "a long letter's card shows the first page, a note where the rest is, and the narrator's lines");
        C(t.letter.Contains(paras[13].Trim()) && t.pages.Count == LetterKindMath.Pages(t.letter).Count, "the whole letter is kept, in pages");
        s.heart = "A short heart.";
        var shortT = LetterFormMath.Compose(s, new LetterWords(), (part, tags) => null, () => 0.99f);
        C(!shortT.cut && shortT.full.StartsWith(shortT.letter), "a usual letter is shown whole");
        C(LetterFormMath.MaxKeptLong >= 5000 && LetterFormMath.MaxKeptLong > LetterFormMath.MaxKept, "a long letter is kept longer");
    }

    // ================= Not twice =================

    private static void Memory(Action<bool, string> C)
    {
        var used = new List<string>();
        for (int i = 0; i < LetterKindMath.MaxUsed + 30; i++) LetterKindMath.Remember(used, "piece " + i);
        C(used.Count == LetterKindMath.MaxUsed && used[0] == "piece 30" && used[used.Count - 1] == "piece " + (LetterKindMath.MaxUsed + 29), "the long pieces remembered, the oldest let go");
        LetterKindMath.Remember(used, "piece 40");
        C(used[used.Count - 1] == "piece 40" && used.Count(x => x == "piece 40") == 1, "a piece used again moves to the end, once");
        C(LetterKindMath.Key("[po:verse,season:winter] The snow is on the roof again") == "The snow is on the roof again" && LetterKindMath.Key(new string('a', 200)).Length == 60,
            "a piece is remembered by its first words, tags left off");
        C(new[] { LetterPart.Poem, LetterPart.Sermon, LetterPart.Story, LetterPart.Raid, LetterPart.Set, LetterPart.Chronicle, LetterPart.Part, LetterPart.Diary }.All(LetterKindMath.Remembered)
          && !LetterKindMath.Remembered(LetterPart.Salute) && !LetterKindMath.Remembered(LetterPart.Note), "the long pieces' pools are remembered across the game");
        // With the remembered pieces avoided, the pick finds another while one fits.
        var s = Artist();
        var tags = LetterKindMath.PieceTags(s, LetterKind.Poem);
        var avoid = new HashSet<string>();
        int distinct = 0;
        for (int i = 0; i < 12; i++)
        {
            string line = LetterFormMath.Pick(Lines(LetterPart.Poem), tags, s.values, avoid, () => 0.3f);
            if (line != null && avoid.Add(line)) distinct++;
        }
        C(distinct == 12, "twelve poems in a row, none twice, while others fit");
        // The riddle's answer: kept with the letter, given in the writer's next.
        LetterKindMath.SplitRiddle("What has a neck but no head? ~~ A bottle.", out string riddle, out string answer);
        C(riddle == "What has a neck but no head?" && answer == "A bottle.", "a riddle and its answer");
        var r = Talker();
        var made = Make(r, LetterKind.Riddle, 3);
        C(made != null && !string.IsNullOrEmpty(r.riddleAnswer) && made.riddleAnswer == r.riddleAnswer && !made.letter.Contains(r.riddleAnswer), "a riddle's answer is kept, not told");
        var next = Talker();
        next.who.owedAnswer = "A bottle.";
        var nextMade = Make(next, LetterKind.Usual, 4);
        C(nextMade.letter.Contains("P.S.") && nextMade.letter.Contains("A bottle."), "the writer's next letter gives the answer in its P.S.: " + nextMade.letter);
        next.kind = LetterKind.Note;
        var note = LetterFormMath.Compose(next, new LetterWords(), Pick(next, new Random(5)), () => 0.5f);
        C(note.letter.Contains("A bottle."), "even in a bare note");
    }

    // ================= The pools =================

    private static readonly Regex Hole = new Regex(@"\{([A-Z_0-9]+)\}");

    private static void Hygiene(Action<bool, string> C)
    {
        var speeches = new HashSet<string>(LetterFormMath.Speeches.Where(x => x != null));
        foreach (var (part, line) in All())
        {
            var p = P(line);
            string name = part + ": " + (p.text.Length > 70 ? p.text.Substring(0, 70) : p.text);
            C(p.tags != null && p.tags.Length > 0, "every line is tagged: " + name);
            C(!line.Contains("—") && !line.Contains("–") && !p.text.Contains("!"), "no dashes or exclamation marks: " + name);
            C(!Regex.IsMatch(p.text, @"\b(dynasty|bloodline|lineage|heir to|noble house|our house|your house|the house of)\b", RegexOptions.IgnoreCase), "never dynastic: " + name);
            // Both sides draw these pools: a line that names a town or the colony says whose letter it is.
            bool shared = part == LetterPart.Lead || part == LetterPart.Poem || part == LetterPart.Sermon || part == LetterPart.Hands || part == LetterPart.Enclosure
                          || part == LetterPart.Set || part == LetterPart.Raid || part == LetterPart.Note;
            if (shared && (p.text.Contains("{TOWN}") || p.text.Contains("{COLONY}"))) C(Has(p, "theirs") || Has(p, "ours"), "a line naming a town says whose letter it is: " + name);
            foreach (var t in p.tags)
                foreach (var a in t.Split('|'))
                {
                    string tag = a.TrimStart('!');
                    if (tag.StartsWith("sv:")) C(speeches.Contains(tag), "a known voice " + tag + ": " + name);
                    if (tag.StartsWith("p:")) C(LetterCultureMath.Precepts.Values.Contains(tag), "a precept's tag from the table " + tag + ": " + name);
                    if (tag.StartsWith("lk:") && tag != LetterKindMath.AfterTag && tag != LetterKindMath.LeadTag) C(LetterKindMath.Kinds.Any(k => LetterKindMath.Tag(k) == tag), "a known shape " + tag + ": " + name);
                    if (tag.StartsWith("st:") && t.Contains("st:") && !new[] { "st:open", "st:turn", "st:end", "st:moral", "st:joke" }.Contains(tag))
                        C(LetterKindMath.Themes.Contains(tag), "a known story theme " + tag + ": " + name);
                    if (tag.StartsWith("chs:")) C(LetterKindMath.Subjects.Contains(tag), "a known chronicle subject " + tag + ": " + name);
                    if (tag.StartsWith("po:")) C(LetterKindMath.Forms.Contains(tag), "a known poem form " + tag + ": " + name);
                }
            if (LetterFormMath.SpeechOf(p) == LetterFormMath.NoSchool)
                C(char.IsLower(p.text[0]) || p.text.Length < 25, "an unschooled piece shows it: " + name);
            // A line of a piece in a speech the shape is never written in would never be drawn.
            if (part == LetterPart.Story || part == LetterPart.Raid || part == LetterPart.Poem || part == LetterPart.Sermon || part == LetterPart.Chronicle || part == LetterPart.Diary)
            {
                var kind = part == LetterPart.Story ? LetterKind.Story : part == LetterPart.Raid ? LetterKind.Raid : part == LetterPart.Poem ? LetterKind.Poem
                    : part == LetterPart.Sermon ? LetterKind.Sermon : part == LetterPart.Chronicle ? LetterKind.Chronicle : LetterKind.Diary;
                C(LetterKindMath.SpeaksIn(kind, LetterFormMath.SpeechOf(p)), $"a {kind} piece in a speech it is written in: " + name);
            }
        }
        // Stories: every theme opens, turns and ends in plain and medieval English; old tales and stories are never last week's news.
        foreach (var theme in LetterKindMath.Themes)
            foreach (var part in new[] { "st:open", "st:turn", "st:end" })
                C(Lines(LetterPart.Story).Count(l => Has(P(l), theme) && Has(P(l), part) && LetterFormMath.SpeechOf(P(l)) == null) >= 3, $"plain {part} lines for {theme}");
        var oldThemes = LetterKindMath.Themes.Where(th => new[] { "st:open", "st:turn", "st:end" }.All(pt => Lines(LetterPart.Story).Any(l => Has(P(l), th) && Has(P(l), pt) && LetterFormMath.SpeechOf(P(l)) == LetterFormMath.Olde))).ToList();
        C(oldThemes.Count >= 6, "medieval stories in six themes or more: " + string.Join(", ", oldThemes));
        var news = new Regex(@"\b(last week|this week|yesterday|days ago|this season|this spring|this summer|this autumn|this winter|this year)\b", RegexOptions.IgnoreCase);
        foreach (var l in Lines(LetterPart.Story)) C(!news.IsMatch(P(l).text), "a story is not news: " + P(l).text);
        // Raids: every line names its record; a real raid's ending, dead and burning follow the record; old tales are old.
        var oldMark = new Regex(@"\b(when I was|years ago|years back|long ago|before my time|before I was born|grandmother|grandsire|granda|in the days of|archive|unrecorded|old ones|before my mother)\b", RegexOptions.IgnoreCase);
        foreach (var l in Lines(LetterPart.Raid))
        {
            var p = P(l);
            string name = p.text;
            C(Has(p, "rd:now") || Has(p, "rd:old") || Has(p, "rd:boast") || Has(p, "rd:ours"), "a raid's line names its record: " + name);
            bool now = Has(p, "rd:now");
            if (now) C(Has(p, "theirs"), "a real raid on their town is theirs to tell: " + name);
            if (Has(p, "rd:ours")) C(Has(p, "ours"), "the colony's own raid is in its own letter: " + name);
            if ((now || Has(p, "rd:ours")) && Has(p, "rd:end") && !Has(p, "rd:boast")) C(p.tags.Any(t => t.Split('|').Any(a => a.StartsWith("rd:end:"))), "a real raid's ending names how it ended: " + name);
            if ((now || Has(p, "rd:ours")) && Has(p, "rd:fall")) C(p.tags.Any(t => t.Split('|').Any(a => a.StartsWith("rd:dead:"))), "who fell as the record has them: " + name);
            if (now && Has(p, "rd:burn")) C(p.tags.Any(t => t.Contains("rd:end:hurt") || t.Contains("rd:end:sacked")), "what burned, only for a town hurt or sacked: " + name);
            if (Has(p, "rd:old")) C(!news.IsMatch(name) && !Regex.IsMatch(name, @"\{RAIDERS\}"), "an old tale is never last week's news: " + name);
            if (Has(p, "rd:old") && Has(p, "rd:come") && LetterFormMath.SpeechOf(p) != LetterFormMath.NoSchool) C(oldMark.IsMatch(name), "an old tale says it is old: " + name);
            if (Has(p, "rd:boast")) C(!Regex.IsMatch(name, @"\b(we got what|we took|we burned|we killed)\b", RegexOptions.IgnoreCase), "a boast claims nothing of how it went: " + name);
        }
        foreach (var part in new[] { "rd:come", "rd:stand", "rd:burn", "rd:fall", "rd:end", "rd:after" })
            C(Lines(LetterPart.Raid).Count(l => Has(P(l), part) && Has(P(l), "rd:now") && LetterFormMath.SpeechOf(P(l)) == null) >= 4
              && Lines(LetterPart.Raid).Count(l => Has(P(l), part) && Has(P(l), "rd:old") && LetterFormMath.SpeechOf(P(l)) == null) >= 2, "real raids and old tales have " + part);
        foreach (var end in new[] { "held", "hurt", "sacked", "ongoing", "unknown" })
            C(Lines(LetterPart.Raid).Any(l => Has(P(l), "rd:end") && Has(P(l), "rd:end:" + end) && LetterFormMath.SpeechOf(P(l)) == null), "a raid that ended " + end);
        foreach (var dead in new[] { "none", "some", "many" })
            C(Lines(LetterPart.Raid).Any(l => Has(P(l), "rd:fall") && Has(P(l), "rd:dead:" + dead) && LetterFormMath.SpeechOf(P(l)) == null), "a raid's dead: " + dead);
        // Poems: each names one form; many of each; a lament is grave and only a lament goes into a condolence.
        foreach (var l in Lines(LetterPart.Poem))
            C(P(l).tags.Count(t => LetterKindMath.Forms.Contains(t)) == 1, "a poem names its form: " + P(l).text.Split('\n')[0]);
        foreach (var form in LetterKindMath.Forms)
            C(Lines(LetterPart.Poem).Count(l => Has(P(l), form)) >= (form == "po:war" ? 1 : 3), $"poems of the form {form}: {Lines(LetterPart.Poem).Count(l => Has(P(l), form))}");
        var condol = Artist();
        condol.purpose = "condolences";
        foreach (var l in Lines(LetterPart.Poem).Where(l => LetterFormMath.Fits(l, LetterKindMath.PieceTags(condol, LetterKind.Poem)) >= 0))
            C(Has(P(l), "po:lament"), "a condolence draws only a lament: " + P(l).text.Split('\n')[0]);
        foreach (var l in Lines(LetterPart.Poem).Where(l => Has(P(l), "po:lament"))) C(Has(P(l), "grave"), "a lament is grave");
        var zeal = Spec("condolences");
        zeal.who.zealous = true;
        foreach (var l in Lines(LetterPart.Sermon).Where(l => LetterFormMath.Fits(l, LetterKindMath.PieceTags(zeal, LetterKind.Sermon)) >= 0))
            C(Has(P(l), "condolences"), "a condolence draws only a consolation: " + P(l).text.Substring(0, 40));
        foreach (var l in Lines(LetterPart.Sermon))
        {
            var p = P(l);
            bool own = p.tags.Any(t => t.Split('|').Any(a => a.TrimStart('!').StartsWith("c:") || a.TrimStart('!').StartsWith("p:")));
            C(own != Has(p, LetterKindMath.AnyFaithTag), "a sermon is for its faith, or for any (sm:any): " + p.text.Substring(0, 40));
        }
        int ownDrawn = 0;
        for (int seed = 0; seed < 200; seed++)
        {
            var nature = Spec();
            nature.who.zealous = true;
            nature.tags.Add("c:nature");
            var drawn = new List<(LetterPart part, string line)>();
            if (Make(nature, LetterKind.Sermon, seed, drawn) != null && drawn.Any(d => d.part == LetterPart.Sermon && Has(P(d.line), "c:nature"))) ownDrawn++;
        }
        C(ownDrawn >= 140, $"a zealot preaches their own faith most of the time ({ownDrawn} of 200)");
        var zealCrank = Spec();
        zealCrank.who.zealous = zealCrank.who.crank = true;
        zealCrank.tags.Add("c:pious");
        zealCrank.tags.Add("c:nature");
        zealCrank.tags.Add(LetterKindMath.AnyFaithTag);
        C(Lines(LetterPart.Sermon).Count(l => LetterFormMath.Fits(l, LetterKindMath.PieceTags(zealCrank, LetterKind.Sermon)) >= 0 && Has(P(l), "zeal:crank")) >= 3, "cranks have tracts of their own");
        // Chronicles: every item about one subject, every subject but the weather on a fact the facts service holds.
        var factOf = new Dictionary<string, string[]>
        {
            ["chs:fortune"] = new[] { "town:struggling", "town:poor", "town:thriving", "town:flourishing" }, ["chs:situation"] = new[] { "town:situation", "town:situation:bad", "town:situation:good", "situation" },
            ["chs:loss"] = new[] { "{LOST}" }, ["chs:head"] = new[] { "town:head", "town:newhead", "town:headdied" }, ["chs:ill"] = new[] { "{ILL}" }, ["chs:war"] = new[] { "their:war" },
            ["chs:peace"] = new[] { "their:peace" }, ["chs:newtown"] = new[] { "region:newtown" }, ["chs:camp"] = new[] { "region:camp", "region:camp:theirfoe" }, ["chs:raid"] = new[] { "rd:now" },
            ["chs:deed"] = new[] { "deed:good" }, ["chs:colony"] = new[] { "colony:known" }, ["chs:caravan"] = new[] { "town:caravan" },
            ["chs:short"] = new[] { "town:short:food", "town:short:medicine", "town:short:fuel", "town:short:hands" }
        };
        foreach (var l in Lines(LetterPart.Chronicle).Where(l => Has(P(l), "ch:item")))
        {
            var p = P(l);
            var subj = p.tags.Where(t => t.StartsWith("chs:")).ToList();
            C(subj.Count == 1, "a chronicle's item is about one thing: " + p.text);
            if (subj.Count == 1 && factOf.TryGetValue(subj[0], out var facts))
                C(facts.Any(fct => fct.StartsWith("{") ? p.text.Contains(fct) : Has(p, fct)), "an item says only what the facts hold: " + p.text);
            C(!Regex.IsMatch(p.text, @"^\{(TOWNSITUATION|SITUATION|FORTUNEWHY)\}"), "an item never starts with a value that comes with 'the': " + p.text);
        }
        foreach (var subj in LetterKindMath.Subjects)
            C(Lines(LetterPart.Chronicle).Any(l => Has(P(l), subj) && LetterFormMath.SpeechOf(P(l)) == null), "chronicle items about " + subj);
        foreach (var l in Lines(LetterPart.Part).Where(l => Has(P(l), "pa:news")))
            C(P(l).tags.Any(t => t != "pa:news" && !t.StartsWith("w:") && !t.StartsWith("!")), "a long letter's news is about a real thing: " + P(l).text);
        // The colony's own enclosures and lines say only what is so at home.
        foreach (var l in Lines(LetterPart.Enclosure).Concat(Lines(LetterPart.Hands)).Where(l => Has(P(l), "ours")))
        {
            var p = P(l);
            if (Regex.IsMatch(p.text, @"\bchildren\b")) C(Has(p, "colony:children"), "children at home only when there are: " + p.text);
            if (Regex.IsMatch(p.text, @"\bharvest\b|\bgrain\b")) C(Has(p, "colony:harvest"), "a harvest only when there was one: " + p.text);
            if (Regex.IsMatch(p.text, @"\bour animals\b|\bthe animals here\b")) C(Has(p, "colony:animals"), "animals at home only when there are: " + p.text);
        }
        // The library: counted.
        var counts = NewParts.ToDictionary(x => x, x => Lines(x).Count);
        C(counts[LetterPart.Poem] >= 50 && counts[LetterPart.Sermon] >= 50 && counts[LetterPart.Story] >= 150 && counts[LetterPart.Raid] >= 120 && counts[LetterPart.Chronicle] >= 80
          && counts[LetterPart.Set] >= 80 && counts[LetterPart.Lead] >= 80, "a large library: " + string.Join(", ", counts.Select(kv => kv.Key + " " + kv.Value)));
    }

    // ================= Words match the map =================

    private static void Truth(Action<bool, string> C)
    {
        // The colony's own raid: only its own lines, and only as the facts have it.
        for (int seed = 0; seed < 60; seed++)
        {
            var s = Spec(ours: true, tone: AnswerTone.Warm);
            s.tags.Add("colony:raid");
            s.tags.Add("colony:raid:mechs");
            s.who.raid = new RaidRecord { ours = true, days = 3, end = RaidEnd.Unknown, dead = seed % 2 == 0 ? 1 : -1 };
            var drawn = new List<(LetterPart part, string line)>();
            var made = Make(s, LetterKind.Raid, seed, drawn);
            C(made != null, "the colony's own raid is told");
            foreach (var (part, line) in drawn.Where(d => d.part == LetterPart.Raid))
            {
                var p = P(line);
                C(Has(p, "rd:ours") && Has(p, "ours"), "the colony's raid draws only its own lines: " + p.text);
                C(!Has(p, "colony:raid:insects") && !Has(p, "colony:raid:animals") && !Has(p, "colony:raidby"), "only who came as the facts have it: " + p.text);
                if (Has(p, "rd:fall")) C(s.who.raid.dead == 1, "who fell only when some did: " + p.text);
            }
        }
        // A raid on their town: from its record, never an old tale; its ending and dead as the record has them.
        var ends = new[] { RaidEnd.Held, RaidEnd.Hurt, RaidEnd.Sacked, RaidEnd.Ongoing, RaidEnd.Unknown };
        for (int seed = 0; seed < 200; seed++)
        {
            var s = Spec(tone: AnswerTone.Cordial);
            s.humour = seed % 3 == 0 ? 60 : 0;
            var rec = new RaidRecord { days = seed % 50, end = ends[seed % ends.Length], dead = seed % 4 - 1, road = seed % 7 == 0, helped = seed % 5 == 0, raiders = seed % 2 == 0 ? "the Red Hand" : null };
            s.who.raid = rec;
            if (rec.raiders != null) s.values["RAIDERS"] = rec.raiders;
            var drawn = new List<(LetterPart part, string line)>();
            var made = Make(s, LetterKind.Raid, seed, drawn);
            C(made != null, $"a real raid is told ({rec.end}, dead {rec.dead}, road {rec.road})");
            foreach (var (part, line) in drawn.Where(d => d.part == LetterPart.Raid))
            {
                var p = P(line);
                C(Has(p, "rd:now") && !p.tags.Contains("rd:old"), "a real raid is never told as an old tale: " + p.text);
                foreach (var t in p.tags.Where(t => t.Contains("rd:end:")))
                    C(t.Split('|').Contains("rd:end:" + rec.end.ToString().ToLowerInvariant()), $"the ending as the record has it ({rec.end}): " + p.text);
                if (Has(p, "rd:fall")) C(rec.dead >= 0, "no one fell who the record doesn't hold: " + p.text);
                if (Has(p, "rd:dead:none")) C(rec.dead == 0, "none dead only when none died: " + p.text);
                if (Has(p, "rd:dead:many")) C(rec.dead >= 2, "many dead only when many died: " + p.text);
                if (Has(p, "rd:burn")) C(rec.end == RaidEnd.Hurt || rec.end == RaidEnd.Sacked, "burning only when the town was hurt or sacked: " + p.text);
                if (Has(p, "rd:road")) C(rec.road, "a road's raid only on the road: " + p.text);
                if (Has(p, "rd:walls")) C(!rec.road, "the walls only at the walls: " + p.text);
                if (Has(p, "rd:helped")) C(rec.helped, "the colony's fighters only when they stood there: " + p.text);
            }
        }
        // No record: only old tales, and only from tale-tellers.
        for (int seed = 0; seed < 60; seed++)
        {
            var s = Spec();
            s.who.talkative = true;
            var drawn = new List<(LetterPart part, string line)>();
            var made = Make(s, LetterKind.Raid, seed, drawn);
            C(made != null, "an old tale of a raid is told");
            foreach (var (part, line) in drawn.Where(d => d.part == LetterPart.Raid || d.part == LetterPart.Lead))
                C(!P(line).tags.Contains("rd:now"), "without a record, never a raid of last week: " + P(line).text);
        }
        // A chronicle and a long letter's news say only what the facts hold: with no facts, only the weather and notices.
        for (int seed = 0; seed < 40; seed++)
        {
            var s = Spec(tone: AnswerTone.Cordial);
            s.who.busy = true;
            s.tags.Add("season:winter");
            var drawn = new List<(LetterPart part, string line)>();
            Make(s, LetterKind.Chronicle, seed, drawn);
            foreach (var (part, line) in drawn.Where(d => d.part == LetterPart.Chronicle && Has(P(d.line), "ch:item")))
                C(Has(P(line), "chs:season"), "with no facts, a chronicle tells only the weather: " + P(line).text);
        }
        var told = Spec(tone: AnswerTone.Cordial);
        told.who.busy = true;
        told.tags.UnionWith(new[] { "town:loss", "their:war", "town:struggling", "region:newtown" });
        told.values["LOST"] = "Ennis";
        told.values["LOSTTITLE"] = "prior";
        told.values["WAR"] = "the Red Hand";
        told.values["FORTUNEWHY"] = "the blight";
        told.values["NEWTOWN"] = "Kettleford";
        told.values["NEWTOWNPEOPLE"] = "the River Folk";
        var chron = Make(told, LetterKind.Chronicle, 11);
        C(chron != null && (chron.letter.Contains("Ennis") || chron.letter.Contains("the Red Hand") || chron.letter.Contains("the blight") || chron.letter.Contains("Kettleford")),
            "a chronicle tells the real news: " + chron?.letter);
    }

    // ================= Every shape in medieval, tribal and plain =================

    private static LetterSpec For(LetterKind k, string speech, WorldAge age)
    {
        var s = Talker(speech, age);
        s.tags.Add("season:winter");
        switch (k)
        {
            case LetterKind.Raid:
                s.who.raid = new RaidRecord { days = 6, end = RaidEnd.Hurt, dead = 1 };
                break;
            case LetterKind.Petition:
            case LetterKind.Account:
                s.purpose = "first";
                s.tags.Add("k:request");
                s.grave = true;
                break;
            case LetterKind.Chronicle:
                s.who.busy = true;
                s.tags.Add("their:war");
                s.values["WAR"] = "the Red Hand";
                break;
            case LetterKind.Sermon:
                s.tags.Add("c:pious");
                break;
        }
        return s;
    }

    private static void Variants(Action<bool, string> C)
    {
        foreach (var k in LetterKindMath.Kinds.Where(x => x != LetterKind.Usual && x != LetterKind.Note))
        {
            foreach (var (what, speech, age) in new (string, string, WorldAge)[] { ("plain", null, WorldAge.Industrial), ("tribal", null, WorldAge.Tribal), ("medieval", LetterFormMath.Olde, WorldAge.Medieval), ("spacer", null, WorldAge.Spacer) })
            {
                var s = For(k, speech, age);
                C(LetterKindMath.Fits(k, s), $"{k} fits a {what} writer who suits it");
                bool built = Enumerable.Range(0, 30).Any(seed => Make(For(k, speech, age), k, seed) != null);
                C(built, $"{k} is written in {what}");
            }
            // Every other speech the shape is written in.
            foreach (var speech in LetterKindMath.Speeches[k].Where(x => x != null && x != LetterFormMath.Olde))
            {
                bool built = Enumerable.Range(0, 30).Any(seed =>
                {
                    var s = For(k, speech, WorldAge.Industrial);
                    if (speech == LetterFormMath.NoSchool && k == LetterKind.Account) s.tags.Add("k:request");
                    return LetterKindMath.Fits(k, s) && Make(s, k, seed) != null;
                });
                C(built, $"{k} is written in {speech}");
            }
        }
        // A tribe's pieces are its own where they matter: tales by the fire, its songs, its season told, its old raid.
        foreach (var part in new[] { LetterPart.Story, LetterPart.Raid, LetterPart.Poem, LetterPart.Chronicle, LetterPart.Part, LetterPart.Lead, LetterPart.Set, LetterPart.Diary, LetterPart.Enclosure })
            C(Lines(part).Count(l => Has(P(l), "w:tribal") && !P(l).tags.Any(t => t == "!w:tribal")) >= 2, "a tribe's own lines in " + part);
        // The colony's own shapes.
        foreach (var k in new[] { LetterKind.Poem, LetterKind.Sermon, LetterKind.Hands, LetterKind.Enclosure, LetterKind.Recipe })
        {
            bool built = Enumerable.Range(0, 30).Any(seed =>
            {
                var s = Spec(ours: true);
                s.who.artistic = s.who.zealous = s.who.cook = true;
                s.who.hands.Add("Hessa");
                s.values["COOK"] = "Bautista";
                s.tags.Add("c:pious");
                s.tags.Add("season:spring");
                return Make(s, k, seed) != null;
            });
            C(built, "the colony's own " + k);
        }
        var help = Spec("help", AnswerTone.Cordial, ours: true);
        help.who.signers.AddRange(new[] { "Stace", "Hessa" });
        var pet = Make(help, LetterKind.Petition, 1);
        C(pet != null && pet.letter.Contains("food") && pet.letter.Contains("Signed"), "the colony's petition asks for what it asks, signed");
    }

    // ================= Medieval and modern never mix =================

    private static void Mixing(Action<bool, string> C)
    {
        int looked = 0;
        foreach (var k in LetterKindMath.Kinds.Where(x => x != LetterKind.Usual && x != LetterKind.Note))
            for (int seed = 0; seed < 25; seed++)
            {
                var olde = For(k, LetterFormMath.Olde, WorldAge.Medieval);
                if (Make(olde, k, seed) != null)
                {
                    looked++;
                    C(!Modern.IsMatch(Piece(olde)), $"a medieval {k} keeps to thee and thou: " + Piece(olde));
                }
                foreach (var age in new[] { WorldAge.Tribal, WorldAge.Industrial, WorldAge.Spacer })
                {
                    var plain = For(k, null, age);
                    if (Make(plain, k, seed) == null) continue;
                    looked++;
                    C(!Archaic.IsMatch(Piece(plain)), $"a plain {k} has no thee or thou: " + Piece(plain));
                }
            }
        C(looked > 500, "pieces looked at for their English: " + looked);
        foreach (var (part, line) in All())
        {
            var p = P(line);
            if (LetterFormMath.SpeechOf(p) == LetterFormMath.Olde) C(!Modern.IsMatch(p.text), "a medieval line keeps to thee and thou: " + p.text);
            else C(!Archaic.IsMatch(p.text), "no thee or thou in a plain line: " + p.text);
        }
    }

    // ================= Precepts against the player's mods =================

    private static void Precepts(string root, Action<bool, string> C)
    {
        var active = new HashSet<string>();
        using (var gz = new GZipStream(File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz")), CompressionMode.Decompress))
        using (var rd = new StreamReader(gz))
        {
            string line;
            while ((line = rd.ReadLine()) != null)
            {
                var cols = line.Split('\t');
                if (cols.Length > 3 && cols[0] == "PreceptDef" && cols[3] == "yes") active.Add(cols[1]);
            }
        }
        foreach (var name in LetterCultureMath.Precepts.Keys) C(active.Contains(name), $"precept {name} is one of the player's active defs");
        foreach (var tag in LetterCultureMath.Precepts.Values.Distinct()) C(Lines(LetterPart.Sermon).Any(l => Has(P(l), tag)), "a sermon for " + tag);
        var tags = new HashSet<string>();
        LetterCultureMath.ReadPrecepts(new[] { "Charity_Essential", "MeatEating_Horrible", "Nonsense_Precept", null }, tags);
        C(tags.SetEquals(new[] { "p:charity", "p:nomeat" }), "a writer's precepts as tags");
    }

    // ================= Whole letters =================

    private static void Samples(Action<bool, string> C, bool print)
    {
        var shown = new List<(string title, string text)>();
        var w = new LetterWords();
        void Show(string title, LetterSpec s, LetterKind k, Func<string, bool> want = null)
        {
            for (int seed = 1; seed < 300; seed++)
            {
                var made = Make(s, k, seed);
                if (made == null || want != null && !want(made.letter)) continue;
                shown.Add((title, made.full + (made.cut ? "\n\n[The whole letter, " + made.pages.Count + " pages:]\n\n" + string.Join("\n\n[page]\n\n", made.pages) : "")));
                return;
            }
            C(false, "a sample could be made: " + title);
        }
        LetterSpec Theirs(string from, string town, string title, WorldAge age, string speech = null)
        {
            var s = Talker(speech, age);
            s.values["FROM"] = from;
            s.values["TOWN"] = town;
            s.values["TITLE"] = title;
            s.place = town;
            s.date = LetterFormMath.Date(LetterFormMath.DateStyleOf(s.age, false), w, 7, "Septober", 2, 5502, "when the geese came back");
            s.sign = LetterFormMath.Signature(SignStyle.Person, w, s.values);
            s.heart = "Your letter came, and we read it twice over, and were glad of it.";
            s.outcomes.Add($"In answer to Stace's letter of greetings, from {from} of {town}.");
            s.outcomes.Add($"{from} thinks a good deal better of the colony for it.");
            return s;
        }
        // A poem.
        var poet = Theirs("Wenna", "Ashford", "headwoman", WorldAge.Industrial);
        poet.tags.Add("season:autumn");
        Show("A poem (their artistic writer)", poet, LetterKind.Poem);
        // A raid's account, from a real record.
        var raided = Theirs("Okoa", "Grey Hollow", "chief", WorldAge.Industrial);
        raided.tone = AnswerTone.Cordial;
        raided.who.raid = new RaidRecord { raiders = "the Red Hand", days = 6, end = RaidEnd.Hurt, dead = 1 };
        raided.values["RAIDERS"] = "the Red Hand";
        raided.heart = "We thank you for asking after us. You will want to know how it has gone here.";
        Show("A raid's account (a real raid, the town hurt, some dead)", raided, LetterKind.Raid);
        // An android's incident report.
        var android = Theirs("Unit Ceres-4", "Hollowmere", "steward", WorldAge.Spacer, LetterCultureMath.Machine);
        android.who.raid = new RaidRecord { raiders = "the Rust Kin", days = 4, end = RaidEnd.Held, dead = 0, helped = true };
        android.values["RAIDERS"] = "the Rust Kin";
        android.date = "5502.3.07";
        android.heart = "Your letter was received.";
        Show("An android's incident report", android, LetterKind.Raid);
        // A sermon.
        var zealot = Theirs("Brother Aldo", "Saltmarsh", "elder", WorldAge.Industrial);
        zealot.tags.Add("c:nature");
        zealot.tags.Add("p:trees");
        Show("A sermon (a zealous writer of nature's faith)", zealot, LetterKind.Sermon, t => t.Contains("tree"));
        // A bare note.
        var curt = Spec(tone: AnswerTone.Curt, motive: LetterMotive.Wary);
        curt.values["FROM"] = "Harl";
        curt.heart = "We have no medicine to spare.";
        curt.sign = LetterFormMath.Signature(SignStyle.Short, w, curt.values);
        curt.outcomes.Add("Harl sends nothing.");
        Show("A bare note (curt)", curt, LetterKind.Note);
        // A long letter in parts.
        var friend = Theirs("Dagna", "Ashford", "headwoman", WorldAge.Industrial);
        friend.length = LetterLength.Long;
        friend.tags.UnionWith(new[] { "town:loss", "season:winter", "home:forest" });
        friend.values["LOST"] = "Ennis";
        friend.values["LOSTTITLE"] = "prior";
        Show("A long letter in parts (a pen friend, Long and storied)", friend, LetterKind.Long, t => t.Length > LetterKindMath.ShowMax);
        // A story.
        var tribe = Theirs("Kesh", "the Long Hut", "elder", WorldAge.Tribal);
        tribe.date = "when the geese came back";
        Show("A tribe's tale as told at the fire", tribe, LetterKind.Story, t => t.Contains("fire"));
        // A medieval piece.
        var med = Theirs("Aldous", "Wendham", "reeve", WorldAge.Medieval, LetterFormMath.Olde);
        med.heart = "Thy letter came to us by the post rider, and right glad we were of it.";
        med.tags.Add("season:winter");
        Show("A medieval ballad", med, LetterKind.Poem, t => t.Contains("\n\n") && t.Split('\n').Length > 14);
        var medStory = Theirs("Aldous", "Wendham", "reeve", WorldAge.Medieval, LetterFormMath.Olde);
        medStory.heart = "Thy letter came to us by the post rider, and right glad we were of it.";
        Show("A medieval tale", medStory, LetterKind.Story);
        // An unschooled one: a bandit's boast.
        var bandit = Spec("greetings", AnswerTone.Curt, WorldAge.Spacer, LetterFormMath.NoSchool);
        bandit.war = true;
        bandit.who.boast = true;
        bandit.values["FROM"] = "Knuckles";
        bandit.heart = "got yor leter. we dont want peece";
        bandit.sign = LetterFormMath.Signature(SignStyle.Mark, w, bandit.values);
        bandit.outcomes.Add("Their people are at war with the colony, and the answer shows it.");
        Show("An unschooled bandit's boast", bandit, LetterKind.Raid);
        // A chronicle.
        var sheet = Theirs("Marta", "Kettleford", "mayor", WorldAge.Industrial);
        sheet.tags.UnionWith(new[] { "their:war", "town:struggling", "season:winter", "town:caravan" });
        sheet.values["WAR"] = "the Ashen Banner";
        sheet.values["FORTUNEWHY"] = "the blight";
        Show("A town's newsletter", sheet, LetterKind.Chronicle);
        // A diary page and a letter by several hands, a riddle, a petition, a recipe.
        Show("A diary page", Theirs("Wenna", "Ashford", "headwoman", WorldAge.Industrial), LetterKind.Diary);
        Show("A letter by several hands", Theirs("Wenna", "Ashford", "headwoman", WorldAge.Industrial), LetterKind.Hands, t => t.Contains("Orrin adds"));
        Show("A riddle", Theirs("Wenna", "Ashford", "headwoman", WorldAge.Industrial), LetterKind.Riddle);
        var asks = Theirs("Marta", "Kettleford", "mayor", WorldAge.Industrial);
        asks.purpose = "first";
        asks.tags.Add("k:request");
        asks.grave = true;
        asks.heart = "We are short of medicine this season, and would be grateful if Riverhold could spare it.";
        Show("A petition with signatures", asks, LetterKind.Petition);
        var ours = Spec(ours: true, tone: AnswerTone.Warm);
        ours.values["FROM"] = "Hessa";
        ours.values["TO"] = "Dagna";
        ours.who.artistic = true;
        ours.heart = "We wanted to send our greetings, and to say that Ashford is often in our thoughts.";
        ours.sign = LetterFormMath.Signature(SignStyle.Ours, w, ours.values);
        Show("The colony's own poet", ours, LetterKind.Poem, t => t.Contains("Riverhold"));
        foreach (var (title, text) in shown)
        {
            C(text.Length > 80 && !Regex.IsMatch(text, @"\{[A-Z]+\}") && !text.Contains("\\n"), "a sample is whole and filled: " + title);
            if (print) Console.WriteLine("=== " + title + " ===\n" + text + "\n");
        }
    }

    // ================= The seams =================

    private static void Seams(string root, Action<bool, string> C)
    {
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.Exists(Path.Combine(src, rel)) ? File.ReadAllText(Path.Combine(src, rel)) : "";
        string form = Code("Letters/LetterForm.cs"), shapes = Code("Letters/LetterFormShapes.cs"), book = Code("Letters/LetterBook.cs"), window = Code("Letters/LetterReadWindow.cs");
        C(form.Contains("ShapeTheirs(s, n, ") && form.Contains("ShapeOurs(s, l, n, writer, map);") && form.Contains("SignNote("), "their letters and the colony's own take their shape before they are composed");
        C(shapes.Contains("LetterKindMath.Choose(s, s.length, Rand.Value)") && shapes.Contains("LetterKindMath.Build(s, Words, Lines(s)"), "the shape is chosen and built from the pools");
        C(shapes.Contains("WorldSituations.Instance") && shapes.Contains("History.At(place") && shapes.Contains("\"townAttacked\"") && shapes.Contains("\"townDefended\""),
            "a raid's record is read from the world events, the ledger's history and their memory of the colony");
        C(shapes.Contains("Facts.For(new FactScene") && shapes.Contains("PieceAsks"), "the pieces are filled from what the facts service holds");
        C(shapes.Contains("BiomeAnimals.Of(biome)") && shapes.Contains("AllWildPlants"), "the land's own animals and plants, every mod's");
        C(shapes.Contains("KeyDeityName") && shapes.Contains("LetterCultureMath.ReadPrecepts("), "their faith's god and precepts, softly");
        C(form.Contains("LetterKindMath.Remembered(part)") && form.Contains("LetterBook.Use(") && form.Contains("LetterBook.Used("), "the long pieces are remembered across the game");
        C(book.Contains("Scribe_Collections.Look(ref used, \"used\", LookMode.Value)") && book.Contains("LetterKindMath.MaxUsed") && book.Contains("Scribe_Values.Look(ref kind, \"kind\")")
          && book.Contains("Scribe_Values.Look(ref answer, \"answer\")") && book.Contains("MaxLong"), "the book saves the shapes, the answers owed and the remembered pieces, capped");
        C(book.Contains("CC_LetterRead_Whole") && Code("UI/EventCards/EventCardModel.cs").Contains("LetterBook.ReadAct("), "the card of a long letter offers the whole of it");
        C(window.Contains("LetterKindMath.Pages(") && window.Contains("CC_LetterRead_Page") && window.Contains("class LetterReadWindow : FramedWindow"), "the reading window pages a long letter on the shared frame");
        C(Code("CouncilSettings.cs").Contains("public LetterLength letterLength = LetterLength.Mixed") && Code("CouncilSettings.cs").Contains("Scribe_Values.Look(ref letterLength, \"letterLength\", LetterLength.Mixed)")
          && Code("UI/SettingsCatalog.cs").Contains("Choice(\"letterLength\""), "the setting, Mixed by default, under the letters'");
        string keys = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_LetterForm.xml"));
        foreach (var key in new[] { "CC_LetterRead_Whole", "CC_LetterRead_Page", "CC_LetterRead_PageBack", "CC_LetterRead_PageOn", "CC_LetterForm_GoesOnOne", "CC_LetterForm_GoesOnMany",
                     "CC_LetterForm_Adds", "CC_LetterForm_Answer", "CC_LetterForm_Signed", "CC_LetterForm_SignedMore", "CC_LetterForm_SignShort", "CC_LetterForm_DiarySeason",
                     "CC_Settings_LetterLength", "CC_LetterLength_Short", "CC_LetterLength_Mixed", "CC_LetterLength_Long", "CC_SetNote_LetterLength_Mixed" })
            C(keys.Contains("<" + key + ">"), "the key " + key);
        C(keys.Contains("<CC_LetterLength_Long>Long and storied</CC_LetterLength_Long>"), "the setting's choices in the player's words");
        C(File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py")).Contains("Letters/LetterBook.cs:used"), "the remembered pieces are modelled in the save estimate");
        C(File.ReadAllText(Path.Combine(root, "Tools", "Tests", "Tests.csproj")).Contains("Letters/LetterKindMath.cs"), "the rules are linked into the tests");
        string validator = File.ReadAllText(Path.Combine(root, "Tools", "DefValidator", "Program.cs"));
        foreach (var ph in new[] { "ANIMAL", "ANANIMAL", "ANIMALS", "PLANT", "TREE", "DEITY", "ANSWER" }) C(validator.Contains("\"" + ph + "\""), "the def validator knows {" + ph + "}");
        foreach (var part in NewParts) C(pools.ContainsKey(LetterFormMath.PoolOf(part)), "the pool " + LetterFormMath.PoolOf(part));
    }
}
