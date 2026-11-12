using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A verse of the colony's book (GodBookMath.BookVerse), saved with the game.</summary>
    public class LoreVerse : BookVerse, IExposable
    {
        public void ExposeData()
        {
            Scribe_Values.Look(ref part, "part", BookPart.Names);
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref page, "page");
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref index, "index", -1);
            Scribe_Values.Look(ref against, "against");
            Scribe_Values.Look(ref other, "other", -1);
            Scribe_Values.Look(ref text, "text");
            Scribe_Values.Look(ref by, "by");
            Scribe_Values.Look(ref people, "people");
            Scribe_Values.Look(ref age, "age", -1);
            Scribe_Values.Look(ref how, "how");
            Scribe_Values.Look(ref about, "about");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref keeper, "keeper");
        }
    }

    /// <summary>
    /// The colony's book as a holy book (the player, 12 November 2026): besides its dated entries, the verses each name's
    /// chapter is made of (GodBookMath, Gods/GodLorePage.cs). A verse is set down only when something happens in play that
    /// someone of the colony saw, was told or came to believe, with who said it and where:
    /// <list type="bullet">
    /// <item>Of its names: a name used by anyone whose words reach the book (a teller, a letter, a colonist), once a people,
    /// with that people's age; two names worked out to be one.</item>
    /// <item>Of what it is, and how it is seen: a line told at the fire, at supper or at the gate (GodTales, WhatOf); a
    /// scoffer's answer to a believer, as a doubt.</item>
    /// <item>Of how it is known: a sign learned from a tale heard whole or retold in a letter; what the colony saw of it
    /// plainly (Gods.Moment).</item>
    /// <item>Of what it loves and what it hates: what a believer came to hold after a god's hand they saw, under the god they
    /// took it for (which may not be the god it was).</item>
    /// <item>The sayings, the tales (each piece as told, a tale retold whole in a letter), kin (a word on two gods at supper, a
    /// tale that names another), and the feasts and rites (kept here, or a neighbour's invitation to their god's feast).</item>
    /// </list>
    /// A book from before this was written down from what it had kept (its stories' counts, retellings, signs and entries)
    /// the first time it is opened or written in (<see cref="Migrate"/>).
    /// </summary>
    public partial class GodLore
    {
        /// <summary>The verses of the book's chapters (GodBookMath.MaxVerses at most).</summary>
        public List<LoreVerse> verses = new List<LoreVerse>();

        /// <summary>1 once a book from before the verses was written into them.</summary>
        public int versesFrom;

        // ================= Setting a verse down =================

        /// <summary>A teller's people as the book keeps it: anyone not of the colony's.</summary>
        public static Faction PeopleOf(Pawn p) => p?.Faction != null && !p.Faction.IsPlayer && !p.IsColonist ? p.Faction : null;

        private static int AgeOf(Faction f)
        {
            if (f == null || f.IsPlayer) return -1;
            try
            {
                return Ages.IsEmpire(f) ? 4 : Pantheon.AgeOf(f);
            }
            catch
            {
                return -1;
            }
        }

        private static string Short(Pawn p) => p?.LabelShort;

        /// <summary>
        /// Sets a verse down in the chapter of the name it was said under (the page made if the name is new, heard as
        /// <paramref name="from"/>): the part, the words as said, who said them (<paramref name="by"/>, a name; their people, or
        /// none for the colony's own), where (<paramref name="how"/>), and what it is of. <paramref name="source"/>: whose lore
        /// the words are, when not the god of the page (a belief under the god someone took a hand for). Null when the book has
        /// it already from this teller, from two, or is full.
        /// </summary>
        public static LoreVerse Set(BookPart part, God g, string name, string text, string by, Faction people, string how, string key = null, int index = -1,
            bool against = false, God other = null, string about = null, God source = null, string from = null)
        {
            var b = Book;
            if (b == null || g == null || text.NullOrEmpty()) return null;
            try
            {
                b.Migrate();
                name ??= b.PageOf(g)?.name ?? Gods.Name(g);
                string teller = people != null && by != null ? "CC_GodLore_TellerOf".Translate(by.Named("NAME"), people.Name.Named("PEOPLE")).Resolve() : by;
                string page = b.Page(name, g, from ?? "CC_GodLore_FromTold".Translate((teller ?? "CC_GodLore_Someone".Translate().ToString()).Named("TELLER"), HowWords(how).Named("HOW")).Resolve());
                if (page == null) return null;
                var v = new LoreVerse
                {
                    part = part, god = (source ?? g).id, page = page, key = key, index = index, against = against, other = other?.id ?? -1, text = text, by = by,
                    people = people?.Name, age = AgeOf(people), how = how, about = about, tick = Now, keeper = Keeper(out _)?.LabelShort
                };
                if (!GodBookMath.Accepts(b.verses, v)) return null;
                b.verses.Add(v);
                Version++;
                if (part != BookPart.Names) b.Called(name, g, by, people, how);
                return v;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the book, a verse", e);
                return null;
            }
        }

        /// <summary>The same, said by someone on the map (a colonist by name, anyone else with their people).</summary>
        public static LoreVerse Set(BookPart part, God g, string name, string text, Pawn teller, string how, string key = null, int index = -1, bool against = false,
            God other = null, string about = null, God source = null) =>
            teller == null ? null : Set(part, g, name, text, Short(teller), PeopleOf(teller), how, key, index, against, other, about, source);

        /// <summary>
        /// A name used for a god by someone whose words reached the book: set down once a people (once for the colony's own),
        /// with that people's age, under the page the name is.
        /// </summary>
        internal void Called(string name, God g, string by, Faction people, string how)
        {
            if (name.NullOrEmpty() || g == null) return;
            var n = Named(name);
            if (n == null) return;
            string page = Root(n)?.name ?? name;
            var v = new LoreVerse
            {
                part = BookPart.Names, god = g.id, page = page, key = name, text = name, by = by, people = people?.Name, age = AgeOf(people), how = how ?? "Saw", tick = Now,
                keeper = Keeper(out _)?.LabelShort
            };
            if (!GodBookMath.Accepts(verses, v)) return;
            verses.Add(v);
            Version++;
        }

        /// <summary>The indices of a god's list the colony has heard in a part (from any teller, under any name).</summary>
        public HashSet<int> LinesHeard(BookPart part, God g, string key = null) =>
            g == null ? new HashSet<int>() : new HashSet<int>(verses.Where(v => v.part == part && v.god == g.id && v.key == key && v.index >= 0).Select(v => v.index));

        // ================= What a believer comes to hold =================

        /// <summary>
        /// One who saw a god's hand (a gift, or a wrath) comes to hold what it loves or hates, from what they saw follow: set
        /// down under the god they took it for, as one of the things the hand's own god is pleased or angered by. When the hand
        /// was taken for another god than its own, the belief sits under the wrong name, and the book never says so.
        /// <paramref name="after"/>: the event in words, when there is one.
        /// </summary>
        public static LoreVerse Inferred(Pawn holder, God taken, God truth, bool hates, string after = null)
        {
            var b = Book;
            var list = hates ? truth?.def?.angers : truth?.def?.pleases;
            if (b == null || holder == null || taken == null || list.NullOrEmpty()) return null;
            string name = NameOf(taken, holder);
            string page = b.Root(b.Named(name))?.name ?? name;
            var held = new HashSet<int>(b.verses.Where(v => v.part == BookPart.Loves && v.god == truth.id && v.against == hates && v.page == page).Select(v => v.index));
            int i = GodBookMath.NextLine(list.Count, held, Rand.Value);
            if (i < 0) return null;
            return Set(BookPart.Loves, taken, name, list[i], holder, "Holds", null, i, hates, null, after.NullOrEmpty() ? null : after, truth);
        }

        /// <summary>The believer among those who saw something: the one who believes most, if anyone does.</summary>
        public static Pawn Believer(IEnumerable<Pawn> pawns) =>
            pawns?.Where(p => p != null && p.IsColonist && Credence(p) >= GodLoreMath.BelieverFrom).OrderByDescending(Credence).FirstOrDefault();

        /// <summary>A scoffer's answer to a believer (one sentence, said aloud), set down as a doubt of what the god is, under the god the believer named.</summary>
        public static LoreVerse Disputed(Pawn scoffer, God taken, string line) =>
            scoffer == null || taken == null || line.NullOrEmpty() ? null : Set(BookPart.What, taken, NameOf(taken, scoffer), line, scoffer, "Holds", "doubt", -1, true);

        /// <summary>The colony saw a god plainly (a moment it showed itself in, or revealed itself): set down as how it is known, with who saw it.</summary>
        public static LoreVerse Sighted(IList<Pawn> witnesses, God g, string words)
        {
            var who = witnesses?.Where(p => p != null && p.IsColonist).Select(p => p.LabelShort).Distinct().ToList();
            if (who.NullOrEmpty() || g == null || words.NullOrEmpty()) return null;
            string names = Names(who);
            string what = GodBookMath.Bare(GodWorld.FirstSentence(words));
            string text = "CC_GodBook_Sight".Translate(names.Named("WHO"), what.Named("WHAT")).Resolve().CapitalizeFirst();
            return Set(BookPart.Known, g, NameOf(g, witnesses.First(p => p != null && p.IsColonist)), text, names, null, "Saw", "plain");
        }

        /// <summary>A rite kept here for a god (a festival, a place given, a vow, a beast let go), in the words the book has it in.</summary>
        public static LoreVerse Kept(God g, string words) => g == null || words.NullOrEmpty() ? null : Set(BookPart.Rites, g, null, words.CapitalizeFirst(), (string)null, null, "Kept", words);

        // ================= Signs =================

        /// <summary>
        /// The colony learns a sign as a god's own from a tale (heard whole at the fire, or retold in a letter): the sign is
        /// known (<see cref="LearnSign(God, string)"/>), and set down as how the god is known, with the tale it came from and
        /// who told it.
        /// </summary>
        public static void LearnSign(God g, string sign, string story, string name, string by, Faction people, string how)
        {
            LearnSign(g, sign);
            var b = Book;
            if (b == null || g == null || sign.NullOrEmpty()) return;
            string words = SignWords(sign);
            string title = g.def?.Story(story)?.title;
            string text = title != null
                ? "CC_GodBook_SignOf".Translate(words.Named("SIGN"), title.UncapitalizeFirst().Named("TITLE")).Resolve()
                : "CC_GodBook_SignIs".Translate(words.Named("SIGN")).Resolve();
            // Two of a tale's signs can read the same (an earthquake and a modded one): set down once.
            if (b.verses.Any(v => v.part == BookPart.Known && v.god == g.id && v.text == text)) return;
            Set(BookPart.Known, g, name, text, by, people, how, sign, -1, false, null, story);
        }

        /// <summary>A sign in words ("the ground shaking"): its own words where the book has them, else the event's name.</summary>
        public static string SignWords(string sign)
        {
            if (sign.NullOrEmpty()) return "";
            string k = "CC_GodBook_Sign_" + sign;
            if (k.CanTranslate()) return k.Translate();
            string label = DefDatabase<IncidentDef>.GetNamedSilentFail(sign)?.label ?? DefDatabase<GameConditionDef>.GetNamedSilentFail(sign)?.label;
            return label.NullOrEmpty() ? sign : Find.ActiveLanguageWorker.WithIndefiniteArticle(label);
        }

        // ================= A book from before =================

        /// <summary>
        /// A book kept before its chapters had verses is written into them once, from what it kept: each story's pieces heard
        /// (its count, and the entries that told them, with their tellers), the stories retold whole in letters, and the signs
        /// learned. Who told what the entries no longer have is not written.
        /// </summary>
        internal void Migrate()
        {
            if (versesFrom >= 1) return;
            versesFrom = 1;
            try
            {
                foreach (var d in DefDatabase<GodDef>.AllDefsListForReading)
                {
                    var g = Gods.ByKey(d.key);
                    if (g == null || d.stories == null) continue;
                    foreach (var s in d.stories.Where(x => x != null && !x.key.NullOrEmpty()))
                    {
                        int n = told.TryGetValue(s.key, out int t) ? Math.Min(t, s.pieces?.Count ?? 0) : 0;
                        for (int i = 0; i < n; i++)
                        {
                            var e = entries.FirstOrDefault(x => x.story == s.key && x.piece == i && x.page != null);
                            string page = e != null ? Root(Named(e.page))?.name : PageOf(g)?.name;
                            if (page == null) continue;
                            string text = GodTales.Named(s.pieces[i], g.name, page, 90, out _);
                            Old(BookPart.Tales, g, page, text, e?.teller, s.key, i, e?.tick ?? 0);
                        }
                        if (retold.Contains(s.key) && PageOf(g) is LoreName at && !s.retelling.NullOrEmpty())
                        {
                            var e = entries.FirstOrDefault(x => x.story == s.key && x.piece < 0 && x.kind == LoreKind.Heard);
                            Old(BookPart.Tales, g, Root(at)?.name ?? at.name, GodTales.Named(s.retelling, g.name, at.name, int.MaxValue, out _), e?.teller, s.key, -1, e?.tick ?? 0, "Letter");
                        }
                    }
                }
                foreach (var sk in signs.ToList())
                {
                    int bar = sk.IndexOf('|');
                    if (bar <= 0 || !int.TryParse(sk.Substring(0, bar), out int id)) continue;
                    var g = Gods.ById(id);
                    var at = g != null ? PageOf(g) : null;
                    if (at == null) continue;
                    string sign = sk.Substring(bar + 1);
                    var story = g.def?.stories?.FirstOrDefault(s => s?.teaches != null && s.teaches.Contains(sign));
                    string words = SignWords(sign);
                    string text = story != null
                        ? "CC_GodBook_SignOf".Translate(words.Named("SIGN"), story.title.UncapitalizeFirst().Named("TITLE")).Resolve()
                        : "CC_GodBook_SignIs".Translate(words.Named("SIGN")).Resolve();
                    if (verses.Any(v => v.part == BookPart.Known && v.god == g.id && v.text == text)) continue;
                    Old(BookPart.Known, g, Root(at)?.name ?? at.name, text, null, sign, -1, 0, null, story?.key);
                }
                Version++;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the book's older pages", e);
            }
        }

        private void Old(BookPart part, God g, string page, string text, string by, string key, int index, int tick, string how = null, string about = null)
        {
            var v = new LoreVerse { part = part, god = g.id, page = page, key = key, index = index, text = text, by = by, how = how, about = about, tick = tick };
            if (GodBookMath.Accepts(verses, v)) verses.Add(v);
        }
    }
}
