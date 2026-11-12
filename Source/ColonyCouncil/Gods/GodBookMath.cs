using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ColonyCouncil
{
    /// <summary>
    /// The parts of a name's chapter in the colony's book of the gods, in the order they are set down. The witnesses (the
    /// dated entries) come after them all.
    /// </summary>
    public enum BookPart
    {
        /// <summary>Of its names: who calls it what, by people and age, and the names worked out to be one.</summary>
        Names,
        /// <summary>Of what it is: as told, in the tellers' words, and who will have none of it.</summary>
        What,
        /// <summary>Of how it is known: its signs once learned, how it is seen as told, what the colony saw of it plainly.</summary>
        Known,
        /// <summary>Of what it loves and what it hates: what someone here came to believe from what they saw follow.</summary>
        Loves,
        /// <summary>The sayings, as heard.</summary>
        Sayings,
        /// <summary>The tales, set down piece by piece as their pieces are heard.</summary>
        Tales,
        /// <summary>Of its kin and its enemies: words on it and another, and the tales they share.</summary>
        Kin,
        /// <summary>Its feast and its rites: kept here, or written of by those who keep them.</summary>
        Rites
    }

    /// <summary>
    /// One verse of the colony's book as the rules need it (the saved one is GodLore's LoreVerse): which part it is of, the
    /// name it was said under (its page), the words as said, who said them (a colonist by name; anyone else by name, with
    /// their people and its age), where, when, and whose hand set it down. <see cref="god"/> is whose lore the words are
    /// (hidden: it only keeps one of each thing, and is never shown); a belief can sit under another god's name than the god
    /// it came from, and the book never says so.
    /// </summary>
    public class BookVerse
    {
        public BookPart part;
        public int god = -1;
        public string page;
        /// <summary>What it is of: the name said (Names), a story (Tales, Kin), a sign (Known), "seen" or "plain" (Known), "doubt" (What), "tie" (Kin), the rite's words (Rites).</summary>
        public string key;
        /// <summary>The line of the god's own list it is (a saying, a piece, a line of what it is), or -1; a tale's -1 is its retelling whole.</summary>
        public int index = -1;
        /// <summary>A doubt (Of what it is), or a hate (Of what it loves and what it hates).</summary>
        public bool against;
        /// <summary>The other god a verse of kin is about, or -1.</summary>
        public int other = -1;
        public string text;
        public string by;
        public string people;
        /// <summary>The teller's people's age (0 tribal to 3 spacer, 4 the Empire), -1 for the colony's own.</summary>
        public int age = -1;
        /// <summary>Where and how (CC_GodLore_How_*: Fire, Gate, Letter, Supper, Sermon; or Saw, Holds, Kept, Joined, Tale, First).</summary>
        public string how;
        /// <summary>What it was said after or about: the event a belief followed, the tale a sign was learned from.</summary>
        public string about;
        public int tick;
        public string keeper;

        /// <summary>The same thing said (perhaps by someone else): of the same part, lore, page and line.</summary>
        public bool Same(BookVerse o) => o != null && o.part == part && o.god == god && o.page == page && o.key == key && o.index == index && o.against == against && o.other == other;
    }

    /// <summary>What a line of a chapter is.</summary>
    public enum BookLineKind
    {
        /// <summary>A part's heading.</summary>
        Head,
        /// <summary>A verse, numbered.</summary>
        Verse,
        /// <summary>A verse that will have none of what is said above it ("Others hold, with ...").</summary>
        Doubt,
        /// <summary>A tale's title.</summary>
        TaleHead,
        /// <summary>A tale's pieces not yet heard.</summary>
        Gap,
        /// <summary>A tale retold whole in a letter, numbered.</summary>
        Retold,
        /// <summary>The keeper's note in the margin.</summary>
        Note,
        /// <summary>One part with nothing in it: its heading, and silence.</summary>
        Silence,
        /// <summary>Two parts or more together with nothing in them: a torn page.</summary>
        Torn
    }

    /// <summary>One line of a chapter as laid out by <see cref="GodBookMath.Compose"/>; the page puts it in words.</summary>
    public sealed class BookLine
    {
        public BookLineKind kind;
        public BookPart part;
        public BookVerse verse;
        /// <summary>The verse's number in the chapter (0 for a line that isn't a verse).</summary>
        public int number;
        /// <summary>The first claim of a part that someone here disputes ("Some say: ...").</summary>
        public bool someSay;
        /// <summary>A view beside someone else's ("Others hold, with ...").</summary>
        public bool othersHold;
        /// <summary>Said by more than one of the same people: put as theirs ("so they say among ...").</summary>
        public bool theirPeople;
        /// <summary>Set down in another hand than the verse before it in the part.</summary>
        public bool hand;
        /// <summary>The chapter's first verse: it opens with a drop capital.</summary>
        public bool first;
        /// <summary>The tale a line is of (TaleHead, Gap, Retold, a piece).</summary>
        public string tale;
        /// <summary>How many pieces are missing at a gap.</summary>
        public int gaps;
        /// <summary>The parts a silence or a torn page stands for.</summary>
        public List<BookPart> parts;
        /// <summary>A keeper's note: "Disagree" (a part disputed), "Untold" (a tale with gaps).</summary>
        public string note;
    }

    /// <summary>
    /// The pure rules of the colony's book as a holy book (the player, 12 November 2026: "more like a wiki page basically
    /// building each up based on gained knowledge, only a more mysterious and ominous wiki, you know like a religious
    /// text-style wiki"): each name's page is a chapter in parts, and a part shows only what someone of the colony saw, was
    /// told or came to believe, each verse said as theirs, contradictions side by side, and what is still dark shown as
    /// silence, a torn page or a gap in a tale. Which verses the book keeps (<see cref="Accepts"/>), which line a teller tells
    /// next (<see cref="NextInOrder"/>, <see cref="NextLine"/>), and how a chapter is laid out (<see cref="Compose"/>). No game
    /// types, so Tools/Tests checks them.
    /// </summary>
    public static class GodBookMath
    {
        /// <summary>The parts in the order a chapter sets them down.</summary>
        public static readonly BookPart[] Order = (BookPart[])Enum.GetValues(typeof(BookPart));

        /// <summary>
        /// The book keeps at most <see cref="MaxVerses"/> verses; a second teller of a thing already set down only while it holds
        /// fewer than <see cref="RoomForNew"/>; one thing at most <see cref="MaxTellers"/> tellers; a god's rites at most
        /// <see cref="MaxRites"/>.
        /// </summary>
        public const int MaxVerses = 720, RoomForNew = 640, MaxTellers = 2, MaxRites = 6;

        /// <summary>The ways a verse came that stand in its own words, with no "so they say" after them.</summary>
        public static bool PlainWords(string how) => how == "Saw" || how == "Kept" || how == "Joined" || how == "Tale" || how == "First";

        /// <summary>
        /// The book sets a verse down: it has words and a part; the book isn't full; a name is kept once a people (and once
        /// for the colony's own), and a name worked out once; anything else not twice from one teller, from at most
        /// <see cref="MaxTellers"/> tellers, a second only while there is room; and a god's rites at most <see cref="MaxRites"/>.
        /// </summary>
        public static bool Accepts(IReadOnlyList<BookVerse> book, BookVerse v)
        {
            if (v == null || string.IsNullOrEmpty(v.text) || string.IsNullOrEmpty(v.page)) return false;
            int count = book?.Count ?? 0;
            if (count >= MaxVerses) return false;
            if (book == null) return true;
            if (v.part == BookPart.Names)
            {
                bool joined = v.how == "Joined";
                foreach (var o in book)
                    if (o != null && o.part == BookPart.Names && o.key == v.key && (o.how == "Joined") == joined && (joined || o.people == v.people))
                        return false;
                return true;
            }
            int same = 0, rites = 0;
            foreach (var o in book)
            {
                if (o == null) continue;
                if (v.part == BookPart.Rites && o.part == BookPart.Rites && o.god == v.god && o.page == v.page) rites++;
                if (!v.Same(o)) continue;
                if (o.by == v.by) return false;
                same++;
            }
            if (same >= MaxTellers) return false;
            if (same >= 1 && count >= RoomForNew) return false;
            if (v.part == BookPart.Rites && rites >= MaxRites && same == 0) return false;
            return true;
        }

        /// <summary>The first line of a list (of <paramref name="count"/>) the colony hasn't heard, in order; -1 when it has heard them all.</summary>
        public static int NextInOrder(int count, ICollection<int> heard)
        {
            for (int i = 0; i < count; i++)
                if (heard == null || !heard.Contains(i))
                    return i;
            return -1;
        }

        /// <summary>
        /// A line of a list (of <paramref name="count"/>) to say: one the colony hasn't heard, looked for from a place the roll
        /// picks; when it has heard them all, any, by the roll. -1 for an empty list.
        /// </summary>
        public static int NextLine(int count, ICollection<int> heard, float roll)
        {
            if (count <= 0) return -1;
            int start = Math.Max(0, Math.Min(count - 1, (int)(Math.Max(0f, roll) * count)));
            for (int i = 0; i < count; i++)
            {
                int j = (start + i) % count;
                if (heard == null || !heard.Contains(j)) return j;
            }
            return start;
        }

        /// <summary>The words without the full stop at their end (so "; so they say" can follow).</summary>
        public static string Bare(string words)
        {
            if (string.IsNullOrEmpty(words)) return words ?? "";
            string w = words.TrimEnd();
            return w.EndsWith(".") && !w.EndsWith("..") ? w.Substring(0, w.Length - 1) : w;
        }

        /// <summary>A chapter's number as the book writes it: I, II, III, IV ...</summary>
        public static string Roman(int n)
        {
            if (n <= 0) return "";
            var sb = new StringBuilder();
            int[] v = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            string[] s = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
            for (int i = 0; i < v.Length; i++)
                while (n >= v[i])
                {
                    sb.Append(s[i]);
                    n -= v[i];
                }
            return sb.ToString();
        }

        /// <summary>The age words for a people (0 tribal to 3 spacer, 4 the Empire); -1 for the colony's own.</summary>
        public static int AgeWord(int age) => age < 0 ? -1 : Math.Min(4, age);

        /// <summary>A verse whose words are followed by who said them ("; so Mirel told it at our gate").</summary>
        public static bool Attributed(BookVerse v) =>
            v != null && !PlainWords(v.how) && v.part != BookPart.Names && v.part != BookPart.Loves && v.part != BookPart.Tales && !(v.part == BookPart.What && v.against);

        /// <summary>
        /// A chapter laid out from the verses written under its name (and only those): each part in <see cref="Order"/> with its
        /// heading and verses, numbered through the chapter, the first opening with a drop capital; what is disputed set side by
        /// side ("Some say ... Others hold ..."); a tale's pieces in order with the ones not yet heard left as gaps (none once
        /// it was retold whole); one empty part as a silence, two or more together as a torn page. <paramref name="pieces"/>:
        /// how many pieces each tale has (a tale's length is known once one piece is heard). Nothing is laid out that isn't in
        /// <paramref name="verses"/>, except the headings, the gaps of a tale begun, the silences and tears, and the keeper's notes.
        /// </summary>
        public static List<BookLine> Compose(IList<BookVerse> verses, IDictionary<string, int> pieces)
        {
            var lines = new List<BookLine>();
            var all = (verses ?? new List<BookVerse>()).Where(v => v != null && !string.IsNullOrEmpty(v.text)).ToList();
            // A people with more than one teller in the chapter: their words put as the people's.
            var theirs = new HashSet<string>(all.Where(v => v.people != null && v.by != null && Attributed(v)).GroupBy(v => v.people)
                .Where(g => g.Select(v => v.by).Distinct().Count() >= 2).Select(g => g.Key));
            var empty = new List<BookPart>();
            int number = 0;
            bool firstDone = false;

            void Flush()
            {
                if (empty.Count == 1) lines.Add(new BookLine { kind = BookLineKind.Silence, part = empty[0], parts = new List<BookPart>(empty) });
                else if (empty.Count > 1) lines.Add(new BookLine { kind = BookLineKind.Torn, part = empty[0], parts = new List<BookPart>(empty) });
                empty.Clear();
            }

            BookLine Add(BookLineKind kind, BookPart part, BookVerse v, string prevKeeper, bool firstInPart)
            {
                var l = new BookLine { kind = kind, part = part, verse = v, number = ++number, theirPeople = v.people != null && theirs.Contains(v.people) && Attributed(v) };
                l.hand = !firstInPart && (v.keeper ?? "") != (prevKeeper ?? "");
                if (kind == BookLineKind.Verse && !firstDone)
                {
                    l.first = true;
                    firstDone = true;
                }
                lines.Add(l);
                return l;
            }

            void Run(BookPart part, IEnumerable<BookVerse> list, BookLineKind kind = BookLineKind.Verse)
            {
                string prev = null;
                bool firstInPart = true;
                foreach (var v in list)
                {
                    Add(kind, part, v, prev, firstInPart);
                    prev = v.keeper;
                    firstInPart = false;
                }
            }

            foreach (var part in Order)
            {
                var list = all.Where(v => v.part == part).ToList();
                if (list.Count == 0)
                {
                    empty.Add(part);
                    continue;
                }
                Flush();
                lines.Add(new BookLine { kind = BookLineKind.Head, part = part });
                switch (part)
                {
                    case BookPart.Names:
                        // The colony's own name first, then the peoples' by age, then the names worked out to be one.
                        Run(part, list.OrderBy(v => v.how == "Joined" ? 2 : v.people == null ? 0 : 1).ThenBy(v => v.age).ThenBy(v => v.people ?? "").ThenBy(v => v.tick));
                        break;
                    case BookPart.What:
                    {
                        var claims = list.Where(v => !v.against).OrderBy(v => v.tick).ToList();
                        var doubts = list.Where(v => v.against).OrderBy(v => v.tick).ToList();
                        Run(part, claims);
                        if (claims.Count > 0 && doubts.Count > 0) lines.Last(l => l.kind == BookLineKind.Verse && l.part == part && l.verse == claims[0]).someSay = true;
                        string prev = claims.Count > 0 ? claims[claims.Count - 1].keeper : null;
                        bool firstInPart = claims.Count == 0;
                        foreach (var d in doubts)
                        {
                            var l = Add(BookLineKind.Doubt, part, d, prev, firstInPart);
                            l.othersHold = claims.Count > 0;
                            prev = d.keeper;
                            firstInPart = false;
                        }
                        if (claims.Count > 0 && doubts.Count > 0) lines.Add(new BookLine { kind = BookLineKind.Note, part = part, note = "Disagree" });
                        break;
                    }
                    case BookPart.Loves:
                    {
                        // What it loves, then what it hates; the first to hold a thing says it, the rest are others who hold.
                        var ordered = list.OrderBy(v => v.against ? 1 : 0).ThenBy(v => v.tick).ToList();
                        string holder = list.OrderBy(v => v.tick).First().by;
                        int before = lines.Count;
                        Run(part, ordered);
                        for (int i = before; i < lines.Count; i++)
                            lines[i].othersHold = lines[i].verse != null && lines[i].verse.by != holder;
                        break;
                    }
                    case BookPart.Tales:
                        foreach (var tale in list.GroupBy(v => v.key ?? "").OrderBy(g => g.Min(v => v.tick)))
                        {
                            lines.Add(new BookLine { kind = BookLineKind.TaleHead, part = part, tale = tale.Key });
                            var retold = tale.Where(v => v.index < 0).OrderBy(v => v.tick).FirstOrDefault();
                            var heard = tale.Where(v => v.index >= 0).GroupBy(v => v.index).ToDictionary(g => g.Key, g => g.OrderBy(v => v.tick).First());
                            int n = pieces != null && pieces.TryGetValue(tale.Key, out int p) ? p : 0;
                            if (heard.Count > 0) n = Math.Max(n, heard.Keys.Max() + 1);
                            string prev = null;
                            bool firstInPart = true, gapped = false;
                            for (int i = 0; i < n; i++)
                            {
                                if (heard.TryGetValue(i, out var v))
                                {
                                    var l = Add(BookLineKind.Verse, part, v, prev, firstInPart);
                                    l.tale = tale.Key;
                                    prev = v.keeper;
                                    firstInPart = false;
                                    continue;
                                }
                                if (retold != null) continue;
                                int run = 0;
                                while (i + run < n && !heard.ContainsKey(i + run)) run++;
                                lines.Add(new BookLine { kind = BookLineKind.Gap, part = part, tale = tale.Key, gaps = run });
                                gapped = true;
                                i += run - 1;
                            }
                            if (retold != null)
                            {
                                var l = Add(BookLineKind.Retold, part, retold, prev, firstInPart);
                                l.tale = tale.Key;
                            }
                            if (gapped) lines.Add(new BookLine { kind = BookLineKind.Note, part = part, tale = tale.Key, note = "Untold" });
                        }
                        break;
                    case BookPart.Kin:
                        Run(part, list.OrderBy(v => v.other).ThenBy(v => v.tick));
                        break;
                    default:
                        Run(part, list.OrderBy(v => v.tick));
                        break;
                }
            }
            Flush();
            return lines;
        }

        /// <summary>The verses a chapter lays out: the lines that are verses, in order.</summary>
        public static IEnumerable<BookVerse> Laid(IEnumerable<BookLine> lines) => (lines ?? Enumerable.Empty<BookLine>()).Where(l => l?.verse != null).Select(l => l.verse);
    }

    /// <summary>
    /// What the words of a chapter need from the game, so they can be chosen without it: a key in the player's language with
    /// its named words (<see cref="T"/>), the book's name for another god (<see cref="OtherName"/>), a tale's title
    /// (<see cref="TaleTitle"/>), an event with its article (<see cref="The"/>), and the chapter's name.
    /// </summary>
    public sealed class BookWordsContext
    {
        public Func<string, (string name, string value)[], string> T;
        public Func<int, string> OtherName;
        public Func<string, string> TaleTitle;
        public Func<string, string> The;
        public string Chapter;

        public string Say(string key, params (string name, string value)[] args) => T?.Invoke(key, args) ?? key;
    }

    /// <summary>
    /// The words of a chapter's lines (Gods/GodLorePage.cs draws them): every verse said as someone's, never as fact. Who
    /// said it follows the words: a visitor by name and people and where ("so Mirel of the League told it at our gate"), a
    /// colonist by name ("so Avila says", "so Avila holds", "so Avila preached"), a letter ("so Mirel wrote", "so their
    /// letter said"), a people that said it through more than one mouth as theirs ("so they say among the people of ..."),
    /// and nobody where the book doesn't know ("who told it is not written"). Names by people and age; loves and hates as
    /// held, after what; a doubt beside what it doubts.
    /// </summary>
    public static class GodBookWords
    {
        /// <summary>A teller as the book names them: by name and people, or by name for the colony's own.</summary>
        public static string Teller(BookVerse v, BookWordsContext c) =>
            v.people != null && v.by != null ? c.Say("CC_GodLore_TellerOf", ("NAME", v.by), ("PEOPLE", v.people)) : v.by;

        private static string How(string how, BookWordsContext c) => c.Say("CC_GodLore_How_" + (how ?? "Fire"));

        /// <summary>The key of who said a verse, after its words.</summary>
        public static string AttributionKey(BookVerse v, bool theirPeople)
        {
            if (theirPeople && v.people != null) return "CC_GodBook_By_People";
            if (v.how == "Letter") return v.by != null ? "CC_GodBook_By_Letter" : "CC_GodBook_By_TheirLetter";
            if (v.by == null) return "CC_GodBook_By_Nobody";
            if (v.how == "Sermon") return "CC_GodBook_By_Sermon";
            if (v.how == "Holds") return "CC_GodBook_By_Holds";
            return v.people == null ? "CC_GodBook_By_Says" : "CC_GodBook_By_Told";
        }

        /// <summary>Who said a verse, after its words: "so Mirel of the League told it at our gate", "so Avila says".</summary>
        public static string Attribution(BookVerse v, bool theirPeople, BookWordsContext c)
        {
            string key = AttributionKey(v, theirPeople);
            switch (key)
            {
                case "CC_GodBook_By_People": return c.Say(key, ("PEOPLE", v.people));
                case "CC_GodBook_By_Letter": return c.Say(key, ("BY", Teller(v, c)));
                case "CC_GodBook_By_Told": return c.Say(key, ("BY", Teller(v, c)), ("HOW", How(v.how, c)));
                case "CC_GodBook_By_TheirLetter":
                case "CC_GodBook_By_Nobody":
                    return c.Say(key);
                default: return c.Say(key, ("BY", v.by));
            }
        }

        /// <summary>A part's heading.</summary>
        public static string Part(BookPart part, BookWordsContext c) => c.Say("CC_GodBook_Part_" + part);

        /// <summary>The words of a line: a heading, a verse with who said it, a doubt, a tale's title, a gap, a silence, a tear.</summary>
        public static string Line(BookLine l, BookWordsContext c)
        {
            var v = l.verse;
            switch (l.kind)
            {
                case BookLineKind.Head: return Part(l.part, c);
                case BookLineKind.Silence: return c.Say("CC_GodBook_Silence_" + (int)l.part % 3);
                case BookLineKind.Torn:
                {
                    // The parts' names hold "and" of their own, so they are set apart by semicolons.
                    var parts = (l.parts ?? new List<BookPart>()).Select(p => Lower(Part(p, c))).ToList();
                    return c.Say("CC_GodBook_Torn", ("PARTS", string.Join("; ", parts)));
                }
                case BookLineKind.TaleHead: return c.TaleTitle?.Invoke(l.tale) ?? l.tale ?? "";
                case BookLineKind.Gap: return c.Say("CC_GodBook_Gap");
                case BookLineKind.Doubt:
                    return c.Say(l.othersHold ? "CC_GodBook_OthersHold" : "CC_GodBook_HoldsNone", ("BY", v.by ?? c.Say("CC_GodLore_Someone")), ("WORDS", v.text));
                case BookLineKind.Retold: return v.text;
                case BookLineKind.Note: return null;
            }
            if (v == null) return "";
            switch (l.part)
            {
                case BookPart.Names: return NameWords(v, c);
                case BookPart.Loves: return LoveWords(v, l.othersHold, c);
                case BookPart.Tales: return v.text;
                case BookPart.Kin when v.how == "Tale":
                    return c.Say("CC_GodBook_KinTale", ("TITLE", Lower(v.text)), ("NAME", c.Chapter), ("OTHER", c.OtherName?.Invoke(v.other) ?? "?"));
            }
            string s = GodBookMath.PlainWords(v.how) ? v.text : c.Say("CC_GodBook_Verse", ("WORDS", GodBookMath.Bare(v.text)), ("ATTR", Attribution(v, l.theirPeople, c)));
            return l.someSay ? c.Say("CC_GodBook_SomeSay", ("VERSE", s)) : s;
        }

        /// <summary>Of its names: here among us, or among a people of an age, by who said it; as first heard; or two worked out to be one.</summary>
        public static string NameWords(BookVerse v, BookWordsContext c)
        {
            if (v.how == "Joined") return v.text;
            string name = v.key ?? v.text;
            if (v.how == "First") return c.Say("CC_GodBook_Name_First", ("NAME", name), ("FROM", v.about ?? c.Say("CC_GodLore_FromNowhere")));
            string by = v.by ?? c.Say("CC_GodLore_Someone");
            if (v.people == null) return c.Say("CC_GodBook_Name_Ours", ("NAME", name), ("BY", by));
            int age = GodBookMath.AgeWord(v.age);
            string ageWords = c.Say(age < 0 ? "CC_GodBook_Age_Far" : "CC_GodBook_Age_" + age);
            if (v.how == "Letter")
                return v.by != null
                    ? c.Say("CC_GodBook_Name_Letter", ("PEOPLE", v.people), ("AGE", ageWords), ("NAME", name), ("BY", v.by))
                    : c.Say("CC_GodBook_Name_TheirLetter", ("PEOPLE", v.people), ("AGE", ageWords), ("NAME", name));
            return c.Say("CC_GodBook_Name_Theirs", ("PEOPLE", v.people), ("AGE", ageWords), ("NAME", name), ("BY", by), ("HOW", How(v.how, c)));
        }

        /// <summary>Of what it loves and what it hates: as one holds it (after what they saw), or as others hold it beside them.</summary>
        public static string LoveWords(BookVerse v, bool others, BookWordsContext c)
        {
            string by = v.by ?? c.Say("CC_GodLore_Someone");
            if (others) return c.Say(v.against ? "CC_GodBook_OthersHate" : "CC_GodBook_OthersLove", ("BY", by), ("NAME", c.Chapter), ("ITEM", v.text));
            string key = (v.against ? "CC_GodBook_Hates" : "CC_GodBook_Loves") + (string.IsNullOrEmpty(v.about) ? "" : "After");
            string what = string.IsNullOrEmpty(v.about) ? "" : c.The?.Invoke(v.about) ?? v.about;
            return c.Say(key, ("NAME", c.Chapter), ("ITEM", v.text), ("BY", by), ("WHAT", what));
        }

        /// <summary>Who told a tale's piece, for the margin.</summary>
        public static string ToldBy(BookVerse v, BookWordsContext c)
        {
            if (v.by == null) return c.Say("CC_GodBook_ToldByNobody");
            return v.how == null ? c.Say("CC_GodBook_ToldByOnly", ("BY", Teller(v, c))) : c.Say("CC_GodBook_ToldBy", ("BY", Teller(v, c)), ("HOW", How(v.how, c)));
        }

        /// <summary>A tale retold whole: who wrote it.</summary>
        public static string Retold(BookVerse v, BookWordsContext c) => v.by != null ? c.Say("CC_GodBook_Retold", ("BY", Teller(v, c))) : c.Say("CC_GodBook_RetoldNobody");

        /// <summary>Whose hand set a verse down, for the margin.</summary>
        public static string Hand(BookVerse v, BookWordsContext c) => v.keeper != null ? c.Say("CC_GodBook_HandOf", ("KEEPER", v.keeper)) : c.Say("CC_GodBook_NoHand");

        /// <summary>The keeper's note in their own voice (Plain, Devout, Learned); none when nobody keeps the book (Bare).</summary>
        public static string Note(string note, string voice, BookWordsContext c) =>
            string.IsNullOrEmpty(note) || string.IsNullOrEmpty(voice) || voice == "Bare" ? null : c.Say("CC_GodBook_Note_" + note + "_" + voice);

        private static string Lower(string s) => string.IsNullOrEmpty(s) ? s ?? "" : char.ToLowerInvariant(s[0]) + s.Substring(1);
    }
}
