using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The Gods' book as a holy book (the player, 12 November 2026: "more like a wiki page basically building each up based on
/// gained knowledge, only a more mysterious and ominous wiki, you know like a religious text-style wiki"; docs/design/gods.md,
/// "The colony's book"): the pure rules (which verses the book keeps, which line a teller tells next, how a chapter is laid
/// out: parts in order, verses numbered, disputes side by side, a tale's gaps, silence and torn pages) and that nothing
/// appears unearned; the words (every verse said as someone's, by who said it and where, names by people and age, loves and
/// hates as held); the defs' lines on what a god is and how it is seen; and the seams: each part fed only from something
/// that happened in play, the page reading no god's lore from its def, nothing of favor or truth shown.
/// </summary>
public static class GodBookTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the Gods' book as a holy book: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));

        // ================= The parts =================

        C(GodBookMath.Order.SequenceEqual(new[] { BookPart.Names, BookPart.What, BookPart.Known, BookPart.Loves, BookPart.Sayings, BookPart.Tales, BookPart.Kin, BookPart.Rites }),
            "the parts in order: names, what it is, how it is known, loves and hates, sayings, tales, kin, feast and rites");

        // ================= What the book keeps =================

        BookVerse V(BookPart part, string by, int index = 0, string page = "Ducat", string people = null, string key = null, bool against = false, string how = "Supper", int tick = 1,
            string keeper = "Avila", int god = 13, string text = "words") =>
            new BookVerse { part = part, by = by, index = index, page = page, people = people, key = key, against = against, how = how, tick = tick, keeper = keeper, god = god, text = text };

        var book = new List<BookVerse>();
        var a = V(BookPart.Sayings, "Avila");
        C(GodBookMath.Accepts(book, a), "a saying heard is set down");
        book.Add(a);
        C(!GodBookMath.Accepts(book, V(BookPart.Sayings, "Avila")), "not twice from the same teller");
        var b2 = V(BookPart.Sayings, "Mirel", people: "the Silver League");
        C(GodBookMath.Accepts(book, b2), "a second teller of the same saying is kept beside the first");
        book.Add(b2);
        C(!GodBookMath.Accepts(book, V(BookPart.Sayings, "Bram")), $"at most {GodBookMath.MaxTellers} tellers of one thing");
        C(GodBookMath.Accepts(book, V(BookPart.Sayings, "Bram", index: 1)), "another saying is another thing");
        C(GodBookMath.Accepts(book, V(BookPart.Sayings, "Bram", page: "Lady Fortune")), "the same saying under another name is kept under that name");
        C(!GodBookMath.Accepts(book, V(BookPart.Sayings, "Bram", text: "")) && !GodBookMath.Accepts(book, V(BookPart.Sayings, "Bram", page: null)),
            "no verse without words or without a name to stand under");
        var full = Enumerable.Range(0, GodBookMath.MaxVerses).Select(i => V(BookPart.Sayings, "x" + i, index: i)).ToList();
        C(!GodBookMath.Accepts(full, V(BookPart.What, "Avila")), $"the book keeps {GodBookMath.MaxVerses} verses at most");
        var roomy = Enumerable.Range(0, GodBookMath.RoomForNew).Select(i => V(BookPart.Sayings, "x" + i, index: i)).ToList();
        C(GodBookMath.Accepts(roomy, V(BookPart.What, "Avila")) && !GodBookMath.Accepts(roomy, V(BookPart.Sayings, "Bram", index: 0)),
            "near full, a new thing still goes in, a second teller of an old one doesn't");
        var names = new List<BookVerse> { V(BookPart.Names, "Mirel", people: "the Silver League", key: "the Glint", how: "Gate") };
        C(!GodBookMath.Accepts(names, V(BookPart.Names, "Tamsin", people: "the Silver League", key: "the Glint", how: "Fire"))
          && GodBookMath.Accepts(names, V(BookPart.Names, "Orsk", people: "the Ash Kingdom", key: "the Glint", how: "Gate"))
          && GodBookMath.Accepts(names, V(BookPart.Names, "Avila", key: "the Glint", how: "Saw"))
          && GodBookMath.Accepts(names, V(BookPart.Names, "Bram", key: "the Glint", how: "Joined")),
            "a name once a people, once for the colony's own, and its working-out apart");
        var rites = Enumerable.Range(0, GodBookMath.MaxRites).Select(i => V(BookPart.Rites, null, index: -1, key: "rite " + i, how: "Kept")).ToList();
        C(!GodBookMath.Accepts(rites, V(BookPart.Rites, null, index: -1, key: "rite x", how: "Kept")), $"a god's rites at most {GodBookMath.MaxRites}");
        C(!GodBookMath.Accepts(new List<BookVerse> { V(BookPart.Rites, null, index: -1, key: "kept", how: "Kept") }, V(BookPart.Rites, null, index: -1, key: "kept", how: "Kept")),
            "the same rite kept again is set down once");

        // ================= Which line a teller tells next =================

        C(GodBookMath.NextInOrder(3, new HashSet<int>()) == 0 && GodBookMath.NextInOrder(3, new HashSet<int> { 0 }) == 1 && GodBookMath.NextInOrder(3, new HashSet<int> { 0, 1, 2 }) == -1,
            "what a god is and how it is seen, told in order, the next line not yet heard");
        C(GodBookMath.NextLine(5, new HashSet<int> { 0, 1, 2, 3 }, 0.1f) == 4 && GodBookMath.NextLine(5, new HashSet<int> { 0, 1, 2, 3, 4 }, 0.5f) == 2 && GodBookMath.NextLine(0, null, 0.5f) == -1,
            "a saying not yet heard first; when all are heard, any");
        var kinds = Enumerable.Range(0, 100).Select(i => GodLoreMath.Talk(i / 100f)).ToList();
        var visit = Enumerable.Range(0, 100).Select(i => GodLoreMath.VisitorTalk(i / 100f)).ToList();
        C(Enum.GetValues(typeof(GodTalkKind)).Cast<GodTalkKind>().All(kinds.Contains) && kinds.Count(k => k == GodTalkKind.Piece) > kinds.Count(k => k == GodTalkKind.What),
            "at the fire: pieces most often, and sayings, what a god is, how it is seen, a word on two gods");
        C(new[] { GodTalkKind.Piece, GodTalkKind.Saying, GodTalkKind.What, GodTalkKind.Seen }.All(visit.Contains) && !visit.Contains(GodTalkKind.Tie),
            "a visitor tells pieces, sayings, what their god is and how it is seen");

        // ================= A chapter laid out =================

        var pieces = new Dictionary<string, int> { ["Ducat_Chains"] = 3, ["Ducat_Beetle"] = 2, ["Ducat_Miser"] = 2 };
        var none = GodBookMath.Compose(new List<BookVerse>(), pieces);
        C(none.Count == 1 && none[0].kind == BookLineKind.Torn && none[0].parts.Count == GodBookMath.Order.Length,
            "nothing heard: the whole chapter a torn page, no tale, no gap, no verse (nothing comes from the def alone)");

        var verses = new List<BookVerse>
        {
            V(BookPart.Names, "Avila", key: "Ducat", how: "Saw", tick: 1),
            V(BookPart.Names, "Mirel", people: "the Silver League", key: "the Glint", how: "Gate", tick: 2),
            V(BookPart.What, "Mirel", people: "the Silver League", how: "Gate", tick: 3, index: 0, text: "The Glint lives in gold itself, in every coin, and never in one for long."),
            V(BookPart.What, "Bram", key: "doubt", against: true, how: "Holds", tick: 4, index: -1, text: "It's luck, plain luck."),
            V(BookPart.Loves, "Avila", how: "Holds", tick: 5, index: 0, text: "trade, often and fairly", keeper: "Avila"),
            V(BookPart.Loves, "Bram", how: "Holds", tick: 6, index: 1, against: true, text: "a hoard sat on", keeper: "Cass"),
            V(BookPart.Tales, "Mirel", people: "the Silver League", key: "Ducat_Chains", how: "Gate", tick: 7, index: 0, text: "Every coin Smoulder sleeps on is the Glint in chains."),
            V(BookPart.Tales, "Tamsin", people: "the Silver League", key: "Ducat_Chains", how: "Fire", tick: 8, index: 2, text: "They have hated each other since the first coin was struck."),
            V(BookPart.Tales, "Orsk", people: "the Ash Kingdom", key: "Ducat_Miser", how: "Letter", tick: 9, index: -1, text: "There was a miser who kept a chest of silver..."),
            V(BookPart.Sayings, "Tamsin", people: "the Silver League", how: "Fire", tick: 10, index: 3, text: "The Glint hates a miser more than a thief."),
        };
        var lines = GodBookMath.Compose(verses, pieces);
        var laid = GodBookMath.Laid(lines).ToList();
        C(laid.All(v => verses.Contains(v)) && laid.Distinct().Count() == laid.Count && verses.All(laid.Contains), "every verse laid out is one the book was given, each once");
        C(lines.Where(l => l.kind == BookLineKind.Head).Select(l => l.part).SequenceEqual(new[] { BookPart.Names, BookPart.What, BookPart.Loves, BookPart.Sayings, BookPart.Tales }),
            "a heading only for a part with something in it, in order");
        var known = lines.FirstOrDefault(l => l.kind == BookLineKind.Silence);
        var torn = lines.FirstOrDefault(l => l.kind == BookLineKind.Torn);
        C(known != null && known.part == BookPart.Known && torn != null && torn.parts.SequenceEqual(new[] { BookPart.Kin, BookPart.Rites }),
            "one empty part is a silence, two or more together a torn page");
        var numbers = lines.Where(l => l.number > 0).Select(l => l.number).ToList();
        C(numbers.SequenceEqual(Enumerable.Range(1, numbers.Count)) && lines.Count(l => l.first) == 1 && lines.First(l => l.kind == BookLineKind.Verse).first,
            "verses numbered through the chapter, the first with a drop capital");
        var claim = lines.First(l => l.part == BookPart.What && l.kind == BookLineKind.Verse);
        var doubt = lines.First(l => l.kind == BookLineKind.Doubt);
        C(claim.someSay && doubt.othersHold && lines.Any(l => l.kind == BookLineKind.Note && l.note == "Disagree"),
            "a claim disputed: Some say, Others hold, side by side, and the keeper's note");
        var lone = GodBookMath.Compose(new List<BookVerse> { V(BookPart.What, "Bram", key: "doubt", against: true, how: "Holds", index: -1) }, pieces);
        C(!lone.Any(l => l.someSay) && lone.Any(l => l.kind == BookLineKind.Doubt && !l.othersHold) && !lone.Any(l => l.note == "Disagree"), "a doubt with nothing to doubt beside it stands alone");
        var loves = lines.Where(l => l.part == BookPart.Loves && l.kind == BookLineKind.Verse).ToList();
        C(!loves[0].othersHold && loves[1].othersHold && loves[1].hand && !loves[0].hand, "what it loves as the first to hold it says; others who hold beside them; a new hand marked");
        var chains = lines.SkipWhile(l => !(l.kind == BookLineKind.TaleHead && l.tale == "Ducat_Chains")).Skip(1).TakeWhile(l => l.kind != BookLineKind.TaleHead).ToList();
        C(chains.Count(l => l.kind == BookLineKind.Verse) == 2 && chains.Count(l => l.kind == BookLineKind.Gap) == 1 && chains.First(l => l.kind == BookLineKind.Gap).gaps == 1
          && chains.Any(l => l.note == "Untold"), "a tale set down piece by piece as heard, the missing piece a gap, the keeper's note");
        var miser = lines.SkipWhile(l => !(l.kind == BookLineKind.TaleHead && l.tale == "Ducat_Miser")).Skip(1).TakeWhile(l => l.kind != BookLineKind.TaleHead).ToList();
        C(miser.Any(l => l.kind == BookLineKind.Retold) && !miser.Any(l => l.kind == BookLineKind.Gap), "a tale retold whole in a letter has no gaps");
        C(!lines.Any(l => l.tale == "Ducat_Beetle"), "a tale never heard isn't in the book, not even as a gap");
        C(lines.Where(l => l.verse?.people == "the Silver League" && GodBookMath.Attributed(l.verse)).All(l => l.theirPeople) && lines.Where(l => l.verse?.people == null).All(l => !l.theirPeople),
            "a people that said it through more than one mouth: put as theirs");
        var doubled = GodBookMath.Compose(new List<BookVerse>
        {
            V(BookPart.Tales, "Mirel", key: "Ducat_Beetle", index: 0, tick: 1), V(BookPart.Tales, "Tamsin", key: "Ducat_Beetle", index: 0, tick: 2)
        }, pieces);
        C(doubled.Count(l => l.kind == BookLineKind.Verse) == 1, "a piece told twice is set down once, as first told");

        // ================= The words: every verse said as someone's =================

        var keyed = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var e in XDocument.Load(f).Root!.Elements())
                keyed[e.Name.LocalName] = e.Value;
        var missing = new HashSet<string>();
        var ctx = new BookWordsContext
        {
            T = (key, args) =>
            {
                if (!keyed.TryGetValue(key, out var t))
                {
                    missing.Add(key);
                    return key;
                }
                foreach (var (name, value) in args) t = t.Replace("{" + name + "}", value ?? "");
                return t;
            },
            OtherName = id => id == 2 ? "Old Smoulder" : null,
            TaleTitle = k => k == "Ducat_Chains" ? "Gold in chains" : k == "Ducat_Miser" ? "The miser's chest" : k,
            The = w => "the " + w,
            Chapter = "Ducat"
        };
        string Say(BookLine l) => GodBookWords.Line(l, ctx);
        var said = lines.Select(l => (l, s: Say(l))).Where(x => x.s != null).ToList();
        C(missing.Count == 0, $"every word of a chapter is in the keyed words ({string.Join(", ", missing)})");
        C(said.All(x => !Regex.IsMatch(x.s, @"\{[A-Z]+\}")), "no placeholder left unfilled");
        string ours = Say(lines.First(l => l.kind == BookLineKind.Verse && l.part == BookPart.Names && l.verse.people == null));
        string theirs = Say(lines.First(l => l.kind == BookLineKind.Verse && l.part == BookPart.Names && l.verse.people != null));
        C(ours.Contains("Avila") && ours.Contains("Ducat") && theirs.Contains("the Silver League") && theirs.Contains("the Glint") && theirs.Contains("Mirel")
          && theirs.Contains(keyed["CC_GodBook_Age_Far"]), $"names by who calls it what (a people's age unknown: from far off): '{ours}' / '{theirs}'");
        var aged = V(BookPart.Names, "Mirel", people: "the Silver League", key: "the Glint", how: "Gate");
        aged.age = 2;
        C(GodBookWords.NameWords(aged, ctx).Contains(keyed["CC_GodBook_Age_2"]), "a people's name for it with that people's age");
        string what = Say(claim);
        C(what.StartsWith("Some say") && what.Contains("Mirel") == false && what.Contains("the Silver League") && !what.EndsWith(".;"),
            $"what it is in the teller's words, said as theirs: '{what}'");
        C(Say(doubt).Contains("Bram") && Say(doubt).Contains("It's luck, plain luck."), $"the doubt beside it, as the doubter said it: '{Say(doubt)}'");
        C(Say(loves[0]).Contains("Avila holds") || Say(loves[0]).Contains("so Avila holds"), $"what it loves, as one holds it: '{Say(loves[0])}'");
        C(Say(loves[1]).StartsWith("Others hold, with Bram"), $"what it hates, as others hold it: '{Say(loves[1])}'");
        // Who said it, after the words, by how it came.
        BookVerse W(string by, string people, string how) => new BookVerse { part = BookPart.Sayings, by = by, people = people, how = how, text = "Gold that sits still is Ducat in chains." };
        C(GodBookWords.AttributionKey(W("Avila", null, "Supper"), false) == "CC_GodBook_By_Says" && GodBookWords.AttributionKey(W("Avila", null, "Sermon"), false) == "CC_GodBook_By_Sermon"
          && GodBookWords.AttributionKey(W("Avila", null, "Holds"), false) == "CC_GodBook_By_Holds" && GodBookWords.AttributionKey(W("Mirel", "the League", "Gate"), false) == "CC_GodBook_By_Told"
          && GodBookWords.AttributionKey(W("Mirel", "the League", "Letter"), false) == "CC_GodBook_By_Letter" && GodBookWords.AttributionKey(W(null, "the League", "Letter"), false) == "CC_GodBook_By_TheirLetter"
          && GodBookWords.AttributionKey(W("Mirel", "the League", "Gate"), true) == "CC_GodBook_By_People" && GodBookWords.AttributionKey(W(null, null, "Fire"), false) == "CC_GodBook_By_Nobody",
            "who said it: a colonist by name (says, preached, holds), a visitor by name and people and where, a letter, a people, or nobody known");
        C(GodBookWords.Line(new BookLine { kind = BookLineKind.Verse, part = BookPart.Sayings, verse = W("Mirel", "the League", "Gate") }, ctx)
              == "Gold that sits still is Ducat in chains; so Mirel of the League told it at our gate.",
            "a verse is its words, then who told it and where");
        C(GodBookWords.Note("Disagree", "Bare", ctx) == null && GodBookWords.Note("Disagree", "Devout", ctx) != null, "the keeper's notes in their voice, none with nobody keeping the book");
        foreach (var voice in new[] { "Plain", "Devout", "Learned" })
            foreach (var note in new[] { "Disagree", "Untold" })
                C(keyed.ContainsKey("CC_GodBook_Note_" + note + "_" + voice), $"the keeper's note {note} in a {voice} voice");
        C(Enumerable.Range(0, 3).All(i => keyed.ContainsKey("CC_GodBook_Silence_" + i)) && Enumerable.Range(0, 5).All(i => keyed.ContainsKey("CC_GodBook_Age_" + i))
          && GodBookMath.Order.All(p => keyed.ContainsKey("CC_GodBook_Part_" + p)) && keyed.ContainsKey("CC_GodBook_Part_Witnesses"),
            "a heading for every part and the witnesses, three silences, the ages' words");
        string bookKeyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_GodBook.xml"));
        var bookText = Regex.Matches(bookKeyed, @"<(CC_[A-Za-z_]+)>([^<]*)</").Cast<Match>().Select(m => m.Groups[2].Value).ToList();
        C(bookText.Count > 40 && bookText.All(t => !t.Contains("—") && !t.Contains("!")) && !bookKeyed.Contains("favour") && !Regex.IsMatch(bookKeyed, @"\bcolour"),
            "the book's words: no dashes or exclamation marks, American spelling");
        C(!Regex.IsMatch(bookKeyed, @"\b(favor|pleased with|angry with|truly|in truth|the truth)\b", RegexOptions.IgnoreCase), "nothing in the book's words claims a god's favor or the truth");
        C(GodBookMath.Roman(1) == "I" && GodBookMath.Roman(4) == "IV" && GodBookMath.Roman(14) == "XIV" && GodBookMath.Roman(59) == "LIX", "chapters numbered as a book numbers them");
        C(GodBookMath.Bare("It is gold.") == "It is gold" && GodBookMath.Bare("It is gold...") == "It is gold..." && GodBookMath.Bare("Is it?") == "Is it?", "a verse's words without their full stop, so who said them can follow");

        // ================= The defs: what a god is and how it is seen, as people tell it =================

        var godDefs = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_Gods.xml")).Root!.Elements("ColonyCouncil.GodDef").ToList();
        string Val(XElement e, string f) => e.Element(f)?.Value ?? "";
        List<string> Li(XElement e, string f) => e.Element(f)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
        string First(string s)
        {
            for (int i = 12; i < s.Length; i++)
                if (s[i] == '.' || s[i] == '!' || s[i] == '?')
                    return s.Substring(0, i + 1);
            return s;
        }
        foreach (var e in godDefs)
        {
            string k = Val(e, "key");
            int longest = new[] { k }.Concat(new[] { "tribalNames", "medievalNames", "industrialNames", "spacerNames" }.Select(f => Li(e, f).FirstOrDefault() ?? "")).Max(x => x.Length);
            var told = Li(e, "whatLines").Concat(Li(e, "seenLines")).ToList();
            C(Li(e, "whatLines").Count >= 3 && Li(e, "seenLines").Count >= 3, $"{k}: three lines or more on what it is, and on how it is seen");
            foreach (var l in told)
                C(First(l) == l && l.Replace("{GOD}", new string('x', longest)).Length <= 90 && !l.Contains("—") && !l.Contains("!")
                  && Regex.Matches(l, @"\{([A-Z_]+)\}").Cast<Match>().All(m => m.Groups[1].Value == "GOD"),
                    $"{k}: '{l}' is one sentence a person can tell, naming only the god");
            var others = Li(e, "sayings").Concat(e.Element("stories")!.Elements("li").SelectMany(st => Li(st, "pieces"))).Select(x => x.Replace("{GOD}", k)).ToHashSet();
            C(told.Select(x => x.Replace("{GOD}", k)).All(x => !others.Contains(x)) && told.Distinct().Count() == told.Count, $"{k}: its lines say what its sayings and tales don't");
        }
        foreach (var sign in godDefs.SelectMany(e => e.Element("stories")!.Elements("li")).SelectMany(st => Li(st, "teaches")).Distinct())
            C(keyed.ContainsKey("CC_GodBook_Sign_" + sign), $"the sign {sign} in words");
        C(Read("Gods/GodDef.cs").Contains("[MustTranslate] public List<string> whatLines = new List<string>(), seenLines = new List<string>();"), "the lines are the def's, to translate");

        // ================= The seams: every part fed from something that happened =================

        string lore = Read("Gods/GodLore.cs"), verses2 = Read("Gods/GodLoreVerses.cs"), tales = Read("Gods/GodTales.cs"), page = Read("Gods/GodLorePage.cs");
        string hands = Read("Gods/GodHands.cs"), gods = Read("Gods/Gods.cs"), worship = Read("Gods/Worship/Worship.cs"), letters = Read("Gods/World/GodLetters.cs");
        C(lore.Contains("Scribe_Collections.Look(ref verses, \"verses\", LookMode.Deep);") && lore.Contains("verses ??= new List<LoreVerse>();") && lore.Contains("Scribe_Values.Look(ref versesFrom, \"versesFrom\");"),
            "the verses saved with the game and mended on a load");
        C(verses2.Contains("if (!GodBookMath.Accepts(b.verses, v)) return null;") && verses2.Contains("keeper = Keeper(out _)?.LabelShort"), "a verse set down by the rules, in the keeper's hand");
        C(lore.Contains("b.Called(NameOf(g, first), g, first.LabelShort, null, \"Saw\");") && lore.Contains("b.Called(name ?? Gods.Name(g), g, by, people, how);")
          && lore.Contains("how = \"Joined\""), "Of its names: from what colonists saw and said, what tellers told by their people, and names worked out to be one");
        C(tales.Contains("GodLore.Set(part, g, name, line, teller, how, mark, i);") && tales.Contains("Speech.Say(teller, line, listener);") && tales.Contains("GodLoreMath.VisitorTalk(Rand.Value)"),
            "Of what it is and how it is seen: a line said aloud at the fire or the gate, then set down as told");
        C(verses2.Contains("Set(BookPart.Known, g, name, text, by, people, how, sign, -1, false, null, story);") && tales.Contains("GodLore.LearnSign(g, sign, key, name, teller.LabelShort, GodLore.PeopleOf(teller), how);")
          && tales.Contains("GodLore.LearnSign(w.god, sign, w.story, w.name, w.by, w.people, \"Letter\");") && gods.Contains("if (plain) GodLore.Sighted(witnesses, spec.god, words);"),
            "Of how it is known: a sign learned from a tale heard whole or a letter, and what the colony saw plainly");
        C(hands.Contains("if (kind != HandKind.Sign) GodLore.Inferred(believer, taken, god, kind == HandKind.Wrath, label);") && hands.Contains("if (scoffer != null) GodLore.Disputed(scoffer, taken, answer);")
          && gods.Contains("GodLore.Inferred(GodLore.Believer(pawns), ByKey(taken), g, act.kind == GodActKind.Wrath);"),
            "Of what it loves and what it hates: what a believer came to hold after a hand they saw; a scoffer's answer as a doubt");
        C(verses2.Contains("var list = hates ? truth?.def?.angers : truth?.def?.pleases;") && verses2.Contains("return Set(BookPart.Loves, taken, name, list[i], holder, \"Holds\""),
            "a belief under the god it was taken for, from what the hand's own god cares for: it can be wrong, and nothing says so");
        C(tales.Contains("GodLore.Set(BookPart.Sayings, g, name, line, teller, how, null, i);") && tales.Contains("GodLore.Set(BookPart.Tales, g, name, text, teller, how, key, piece);")
          && tales.Contains("GodLore.Set(BookPart.Tales, w.god, w.name, kv.Key, w.by, w.people, \"Letter\", w.story, -1);"),
            "the sayings and the tales as heard, piece by piece, and a tale retold whole in a letter");
        C(tales.Contains("GodLore.Set(BookPart.Kin, a, lore.PageOf(a).name, words, teller, \"Supper\", \"tie\", -1, false, b);") && tales.Contains("GodLore.Set(BookPart.Kin, og, on, story.title"),
            "kin: a word on two gods at supper and a tale that names another, in both chapters");
        C(worship.Contains("if (who.Count > 0) GodLore.Kept(g, words);") && letters.Contains("\"CC_GodBook_FeastOf\".Translate("), "feasts and rites: kept here with someone there, or a neighbour's invitation");
        C(verses2.Contains("internal void Migrate()") && verses2.Contains("told.TryGetValue(s.key, out int t)") && verses2.Contains("retold.Contains(s.key)") && verses2.Contains("foreach (var sk in signs.ToList())"),
            "an older book's tales, retellings and signs written into its chapters from what it had kept");

        // ================= The page: a chapter, and nothing the pawns don't know =================

        C(page.Contains("GodBookMath.Compose(p.verses, Pieces)") && page.Contains("DropCap(") && page.Contains("SmallCaps(") && page.Contains("Tear(") && page.Contains("Ornament(")
          && page.Contains("Number(") && page.Contains("Margins(") && page.Contains("CC_GodBook_Part_Witnesses"),
            "a chapter: its number, the name, an ornament, parts in small capitals, numbered verses with a drop capital, the margin, torn pages, the witnesses last");
        foreach (var lorePart in new[] { "whatItIs", "howSeen", ".character", ".wants", ".pleases", ".angers", ".sayings", "whatLines", "seenLines", ".scoffs", ".hands", "retelling", ".pieces[" })
            C(!page.Contains(lorePart), $"the page reads no god's lore from its def ({lorePart}): only what the book was given");
        C(!page.Contains("Favour") && !page.Contains("FavourValue") && !page.Contains("Gods.Known(") && !page.Contains(".colour") && !page.Contains("new Color("),
            "no favor, no hidden knowledge, no god's colors, no colors of its own: the kit's tokens in both looks");
        C(page.Contains("UiKit.Card(") && page.Contains("UiKit.Gold") && page.Contains("UiKit.Ink") && page.Contains("UiKit.Dim"), "drawn with Rimfolk's kit, in either look");
        C(!File.Exists(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_GodsBook.xml"))
          && !Directory.GetFiles(Path.Combine(root, "Defs", "AlmanacArticleDefs"), "*.xml").Any(f => File.ReadAllText(f).Contains("CC_GodBook_")),
            "no Almanac page of the gods or their book");
        string estimate = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(estimate.Contains("\"Gods/GodLore.cs:verses\"") && estimate.Contains("MaxVerses"), "the verses modelled in the save estimate, by their cap");
        C(Read("Gods/GodsDebug.cs").Contains("Gods: tell of a god here (testing)..."), "a dev action: a colonist tells of a god here");

        // The sample chapter's lines, words and margins as a tab-separated file, for a picture of the page outside the game;
        // written only when GODBOOK_SAMPLE names a file.
        string sample = Environment.GetEnvironmentVariable("GODBOOK_SAMPLE");
        if (!string.IsNullOrEmpty(sample))
        {
            var sb = new StringBuilder();
            foreach (var l in lines)
            {
                string s = l.kind == BookLineKind.Note ? GodBookWords.Note(l.note, "Devout", ctx) : Say(l);
                string margin = l.verse == null ? "" : (l.part == BookPart.Tales && l.kind == BookLineKind.Verse ? GodBookWords.ToldBy(l.verse, ctx) + "|" : "") + "the " + (l.verse.tick + 2) + "th of Aprimay, 5502" + (l.hand ? "|" + GodBookWords.Hand(l.verse, ctx) : "");
                if (l.kind == BookLineKind.Retold) s = GodBookWords.Retold(l.verse, ctx) + "\u0001" + s;
                sb.Append(l.kind).Append('\t').Append(l.number).Append('\t').Append(l.first ? 1 : 0).Append('\t').Append(l.gaps).Append('\t').Append(s ?? "").Append('\t').Append(margin).Append('\n');
            }
            File.WriteAllText(sample, sb.ToString());
        }
        return n;
    }
}
