using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// How the nine reach the colony: only through people, in pieces, never all at once (docs/design/gods.md, "the
    /// contract"). Each is said over a head, one sentence, and written into the colony's book as heard:
    /// <list type="bullet">
    /// <item>At the evening fire or at supper, now and then, a colonist tells another the next piece of an old story of
    /// their people's gods (its pieces in order, GodLoreMath.Next), a saying, a line of what a god is or how it is seen (the
    /// next the colony hasn't heard, <see cref="WhatOf"/>), or a word on two gods; the colony's priest or moral guide, when
    /// it's them, as a sermon.</item>
    /// <item>A visitor of another people near a colonist tells a piece of their own gods' stories, a saying, or what one of
    /// them is or how it is seen, by their own names for them: a name new to the colony becomes a page of its own.</item>
    /// <item>A neighbour's letter retells one of their stories whole, or speaks of a god's hand they heard of at the colony
    /// ({GODTALE}, {GODHAND} in the postscripts), written into the book only once the words are in the letter
    /// (<see cref="Written"/>).</item>
    /// </list>
    /// Each is also set down as a verse of the name's chapter, as the teller's (GodLore.Set): a piece of a tale, a saying, a
    /// line of what it is or how it is seen, kin, a sign learned from a tale heard whole.
    /// <see cref="Tell"/> lets the other parts (worship's priest, dreams) have someone tell a piece.
    /// </summary>
    public static class GodTales
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private static List<TaleSketch> sketches;

        /// <summary>Every story of the nine, as the rules need them (made once).</summary>
        private static List<TaleSketch> Sketches => sketches ??= DefDatabase<GodDef>.AllDefsListForReading.SelectMany(d => (d.stories ?? new List<GodStory>())
            .Where(s => s != null && !s.key.NullOrEmpty() && !s.pieces.NullOrEmpty())
            .Select(s => new TaleSketch(s.key, s.pieces.Count, new[] { d.key }.Concat(s.with ?? new List<string>()).ToArray()))).ToList();

        private static (GodDef def, GodStory story) StoryOf(string key)
        {
            foreach (var d in DefDatabase<GodDef>.AllDefsListForReading)
            {
                var s = d.Story(key);
                if (s != null) return (d, s);
            }
            return (null, null);
        }

        // ================= The hour =================

        public static void Hourly(int now)
        {
            var lore = GodLore.Instance;
            if (lore == null) return;
            foreach (var map in Homes.All.ToList())
            {
                if (map == null) continue;
                int home = Homes.KeyOf(map);
                int last = lore.lastTalk.TryGetValue(home, out int t) ? t : -1;
                int since = last < 0 ? -1 : (now - last) / GenDate.TicksPerHour;
                int hour = GenLocalDate.HourOfDay(map);
                if (GodLoreMath.TalkNow(hour, since, GodLoreMath.EveningChance, Rand.Value) && Evening(map)) lore.lastTalk[home] = now;
                else if ((since < 0 || since >= GodLoreMath.GapHours) && Rand.Chance(GodLoreMath.VisitorChance) && Visitor(map)) lore.lastTalk[home] = now;
            }
        }

        private static bool Able(Pawn p) => p != null && p.Spawned && !p.Dead && !p.Downed && !p.InMentalState && !p.Drafted && p.Awake()
                                            && p.RaceProps?.Humanlike == true && p.DevelopmentalStage.Adult() && People.HasInnerLife(p);

        /// <summary>Talk of the gods at the evening fire: someone who believes, to someone near.</summary>
        private static bool Evening(Map map)
        {
            var people = map.mapPawns.FreeColonistsSpawned.Where(Able).ToList();
            if (people.Count < 2) return false;
            var keeper = GodLore.Keeper(out var voice);
            Pawn teller = keeper != null && people.Contains(keeper) && Rand.Chance(0.4f) ? keeper
                : people.Where(p => GodLore.Credence(p) >= 0f).InRandomOrder().FirstOrDefault();
            if (teller == null) return false;
            var listener = people.Where(p => p != teller && Together.Can(teller, p)).InRandomOrder().FirstOrDefault();
            if (listener == null) return false;
            bool sermon = teller == keeper && voice == GodLoreMath.Voice.Devout;
            var mine = Gods.ColonyGods(map).Select(g => g.Key).ToList();
            string how = sermon ? "Sermon" : "Supper";
            switch (GodLoreMath.Talk(Rand.Value))
            {
                case GodTalkKind.Saying when Saying(teller, listener, mine, how):
                    return true;
                case GodTalkKind.Tie when TieWords(teller, listener):
                    return true;
                case GodTalkKind.What when WhatOf(teller, listener, mine, false, how):
                    return true;
                case GodTalkKind.Seen when WhatOf(teller, listener, mine, true, how):
                    return true;
                default:
                    return Tell(teller, listener, mine, how);
            }
        }

        /// <summary>A visitor of another people tells something of their gods to a colonist near them.</summary>
        private static bool Visitor(Map map)
        {
            var colonists = map.mapPawns.FreeColonistsSpawned.Where(Able).ToList();
            foreach (var v in map.mapPawns.AllPawnsSpawned.ToList().InRandomOrder())
            {
                if (v?.Faction == null || v.Faction.IsPlayer || v.IsPrisoner || v.HostileTo(Faction.OfPlayer) || !Able(v)) continue;
                var listener = colonists.FirstOrDefault(c => Together.Can(v, c));
                if (listener == null) continue;
                var theirs = Gods.Of(v.Faction).Select(g => g.Key).ToList();
                if (theirs.Count == 0) continue;
                int hour = GenLocalDate.HourOfDay(map);
                string how = hour >= GodLoreMath.EveningFrom || hour < 5 ? "Fire" : "Gate";
                switch (GodLoreMath.VisitorTalk(Rand.Value))
                {
                    case GodTalkKind.Saying when Saying(v, listener, theirs, how):
                        return true;
                    case GodTalkKind.What when WhatOf(v, listener, theirs, false, how):
                        return true;
                    case GodTalkKind.Seen when WhatOf(v, listener, theirs, true, how):
                        return true;
                    default:
                        return Tell(v, listener, theirs, how);
                }
            }
            return false;
        }

        // ================= Telling =================

        /// <summary>
        /// Someone tells another the next piece of a story of these gods (the colony's count of each story moves on only with
        /// a piece it hasn't heard), said over their head and written into the book as heard. For the other parts (a priest's
        /// word, a dream told): <paramref name="how"/> is CC_GodLore_How_*.
        /// </summary>
        public static bool Tell(Pawn teller, Pawn listener, ICollection<string> gods, string how = "Supper")
        {
            var lore = GodLore.Instance;
            if (lore == null || teller == null) return false;
            if (!GodLoreMath.Next(Sketches, lore.told, gods, Rand.Value, out string key, out int piece)) return false;
            var (def, story) = StoryOf(key);
            var g = Gods.ByKey(def?.key);
            if (story == null || g == null || piece < 0 || piece >= story.pieces.Count) return false;
            string name = GodLore.NameOf(g, teller);
            string text = Named(story.pieces[piece], g.name, name, 90, out bool renamed);
            text = Others(text, story.with, teller, lore);
            if (!renamed) name = g.name;
            // together: the listener was found near the teller (Together.Can); with none (a part's own call), said to nobody in particular.
            Speech.Say(teller, text, listener);
            lore.told[key] = GodLoreMath.AfterTelling(lore.told.TryGetValue(key, out int n) ? n : 0, piece, story.pieces.Count);
            GodLore.Heard(teller, g, text, how, key, piece, name);
            // The tale in its chapter, piece by piece as told.
            GodLore.Set(BookPart.Tales, g, name, text, teller, how, key, piece);
            // Heard whole, a story teaches the signs it explains as the god's own, set down with the tale and its teller.
            if (lore.told[key] >= story.pieces.Count)
                foreach (var sign in story.teaches ?? new List<string>())
                    GodLore.LearnSign(g, sign, key, name, teller.LabelShort, GodLore.PeopleOf(teller), how);
            // The other gods named in it are names the colony has now heard, and kin of this one in each other's chapters.
            foreach (var other in story.with ?? new List<string>())
            {
                var og = Gods.ByKey(other);
                if (og == null) continue;
                string on = lore.PageOf(og)?.name ?? og.name;
                lore.Page(on, og, "CC_GodLore_FromStory".Translate(GodLore.TellerOf(teller).Named("TELLER"), story.title.UncapitalizeFirst().Named("TITLE")).Resolve());
                GodLore.Set(BookPart.Kin, g, name, story.title, teller.LabelShort, GodLore.PeopleOf(teller), "Tale", key, -1, false, og);
                GodLore.Set(BookPart.Kin, og, on, story.title, teller.LabelShort, GodLore.PeopleOf(teller), "Tale", key, -1, false, g);
            }
            return true;
        }

        /// <summary>
        /// Someone tells another one line of what a god of theirs is (or, <paramref name="seen"/>, of how it is seen): the next
        /// line the colony hasn't heard of a god it still has some to hear of (in order, GodBookMath.NextInOrder), else one again;
        /// said over their head by their own name for the god, written into the book as heard and set down in its chapter.
        /// </summary>
        public static bool WhatOf(Pawn teller, Pawn listener, ICollection<string> gods, bool seen, string how)
        {
            var lore = GodLore.Instance;
            if (lore == null || teller == null) return false;
            var part = seen ? BookPart.Known : BookPart.What;
            string mark = seen ? "seen" : null;
            List<string> Lines(God x) => seen ? x?.def?.seenLines : x?.def?.whatLines;
            var pool = (gods ?? Gods.All.Select(x => x.Key).ToList()).Select(Gods.ByKey).Where(x => !Lines(x).NullOrEmpty()).ToList();
            if (pool.Count == 0) return false;
            var g = pool.Where(x => GodBookMath.NextInOrder(Lines(x).Count, lore.LinesHeard(part, x, mark)) >= 0).InRandomOrder().FirstOrDefault() ?? pool.RandomElement();
            var lines = Lines(g);
            int i = GodBookMath.NextInOrder(lines.Count, lore.LinesHeard(part, g, mark));
            if (i < 0) i = Rand.Range(0, lines.Count);
            string name = GodLore.NameOf(g, teller);
            string line = lines[i].Formatted(name.Named(GodsMath.God)).Resolve().CapitalizeFirst();
            if (line.Length > 90)
            {
                name = g.name;
                line = lines[i].Formatted(name.Named(GodsMath.God)).Resolve().CapitalizeFirst();
            }
            if (line.Length > 90) return false;
            // together: the evening's talk and a visitor's found the listener near the teller (Together.Can), and Speech.Say checks it again.
            Speech.Say(teller, line, listener);
            GodLore.Heard(teller, g, line, how, null, -1, name);
            GodLore.Set(part, g, name, line, teller, how, mark, i);
            return true;
        }

        /// <summary>
        /// A saying about one of their gods (one the colony hasn't heard, where there is one: GodBookMath.NextLine), said to
        /// someone near: a colonist's written as their belief, a visitor's as told; set down among the god's sayings as theirs.
        /// </summary>
        public static bool Saying(Pawn teller, Pawn listener, List<string> gods, string how = "Supper")
        {
            var lore = GodLore.Instance;
            var g = gods.Select(Gods.ByKey).Where(x => x?.def?.sayings != null && x.def.sayings.Count > 0).InRandomOrder().FirstOrDefault();
            if (g == null || lore == null || teller == null) return false;
            int i = GodBookMath.NextLine(g.def.sayings.Count, lore.LinesHeard(BookPart.Sayings, g), Rand.Value);
            string name = GodLore.NameOf(g, teller);
            string line = g.def.sayings[i].Formatted(name.Named(GodsMath.God)).Resolve().CapitalizeFirst();
            if (line.Length > 90) return false;
            // together: the evening's talk and a visitor's found the listener near the teller (Together.Can), and Speech.Say checks it again.
            Speech.Say(teller, line, listener);
            if (teller.IsColonist) GodLore.Believes(teller, g, line);
            else GodLore.Heard(teller, g, line, how, null, -1, name);
            GodLore.Set(BookPart.Sayings, g, name, line, teller, how, null, i);
            return true;
        }

        /// <summary>A word on two gods the colony has names for both of, by the colony's names, written as told at supper.</summary>
        private static bool TieWords(Pawn teller, Pawn listener)
        {
            var lore = GodLore.Instance;
            var known = Gods.All.Where(g => lore.PageOf(g) != null).ToList();
            var tie = Gods.All.SelectMany(Gods.TiesOf).Distinct()
                .Where(t => known.Any(g => g.id == t.a) && known.Any(g => g.id == t.b)).InRandomOrder().FirstOrDefault();
            if (tie == null) return false;
            var a = Gods.ById(tie.a);
            var b = Gods.ById(tie.b);
            string words = Pantheon.TieWords(tie);
            if (words.NullOrEmpty()) return false;
            words = Named(words, a.name, lore.PageOf(a).name, 90, out _);
            words = Named(words, b.name, lore.PageOf(b).name, 90, out _);
            // together: the evening's talk found the listener near the teller (Together.Can), and Speech.Say checks it again.
            Speech.Say(teller, words, listener);
            GodLore.Heard(teller, a, words, "Supper", null, -1, lore.PageOf(a).name);
            // Kin, in both chapters, as the teller put it.
            GodLore.Set(BookPart.Kin, a, lore.PageOf(a).name, words, teller, "Supper", "tie", -1, false, b);
            GodLore.Set(BookPart.Kin, b, lore.PageOf(b).name, words, teller, "Supper", "tie", -1, false, a);
            return true;
        }

        /// <summary>
        /// A god's short name in a line put as the teller names it (their age's name, or the colony's), wherever it stands,
        /// so long as the line stays within <paramref name="max"/>; else the short name as written. A name that opens a
        /// sentence keeps its capital ("The Pale Sister"), one inside it doesn't ("by the Pale Sister"); the line's start
        /// is capitalized.
        /// </summary>
        public static string Named(string line, string shortName, string name, int max, out bool renamed)
        {
            renamed = false;
            if (line.NullOrEmpty() || shortName.NullOrEmpty() || name.NullOrEmpty() || name == shortName) return line;
            string swapped = line.Replace(shortName, name);
            string capShort = shortName.CapitalizeFirst();
            if (capShort != shortName) swapped = swapped.Replace(capShort, name.CapitalizeFirst());
            swapped = swapped.CapitalizeFirst();
            if (swapped.Length > max) return line;
            renamed = true;
            return swapped;
        }

        /// <summary>The other gods named in a line, as the colony names them where it has a page (as the story has them otherwise).</summary>
        private static string Others(string line, List<string> others, Pawn teller, GodLore lore)
        {
            if (others == null) return line;
            foreach (var k in others)
            {
                var og = Gods.ByKey(k);
                var page = og != null ? lore.PageOf(og) : null;
                if (page != null) line = Named(line, og.name, page.name, 90, out _);
            }
            return line;
        }

        // ================= Letters =================

        private sealed class LetterLore
        {
            public God god;
            public string teller, name, story;
            public LoreHand hand;
            /// <summary>The writer by their own name (null: written for their people), and their people.</summary>
            public string by;
            public Faction people;
        }

        /// <summary>What a letter being made may say of the gods, by its value, until the letter's words are known (Written).</summary>
        private static readonly Dictionary<string, LetterLore> waiting = new Dictionary<string, LetterLore>();

        /// <summary>
        /// A neighbour's letter being made (GodWords.Letter): now and then it retells one of their gods' stories whole
        /// ({GODTALE}), or speaks of a god's hand they heard of at the colony ({GODHAND}); both only when the letter's lines
        /// use them, which <see cref="Written"/> checks.
        /// </summary>
        public static void Letter(Dictionary<string, string> values, Pawn writer, Faction f)
        {
            var lore = GodLore.Instance;
            if (lore == null || values == null || f == null || f.IsPlayer) return;
            waiting.Clear();
            string teller = writer != null ? GodLore.TellerOf(writer) : "CC_GodLore_TellerPeople".Translate(f.Name.Named("PEOPLE")).Resolve();
            var theirs = Gods.Of(f);
            // A hand of a god at the colony, a few days after, if they revere that god and it isn't written of yet.
            int now = Now;
            var hand = lore.hands.FirstOrDefault(h => GodHandsMath.Writable(h.tick, now, h.written, GenDate.TicksPerDay) && theirs.Any(g => g.id == h.god));
            if (hand != null && Rand.Chance(GodLoreMath.LetterHandChance))
            {
                var g = Gods.ById(hand.god);
                var line = g?.def?.letterLines?.RandomElementWithFallback();
                if (!line.NullOrEmpty())
                {
                    string name = writer != null ? Gods.NameBy(g, writer) : g.name;
                    string text = line.Formatted(name.Named(GodsMath.God)).Resolve().CapitalizeFirst();
                    values["GODHAND"] = text;
                    waiting[text] = new LetterLore { god = g, teller = teller, name = name, hand = hand, by = writer?.LabelShort, people = f };
                }
            }
            // A story of theirs retold whole, one the colony hasn't had in a letter.
            if (!Rand.Chance(GodLoreMath.LetterTaleChance)) return;
            var tale = theirs.Where(g => g.def?.stories != null).SelectMany(g => g.def.stories.Select(s => (g, s))).Where(x => !x.s.retelling.NullOrEmpty() && !lore.retold.Contains(x.s.key))
                .InRandomOrder().FirstOrDefault();
            if (tale.s == null) return;
            string tn = writer != null ? Gods.NameBy(tale.g, writer) : tale.g.name;
            string retelling = Named(tale.s.retelling, tale.g.name, tn, int.MaxValue, out _);
            values["GODTALE"] = retelling;
            waiting[retelling] = new LetterLore { god = tale.g, teller = teller, name = tn, story = tale.s.key, by = writer?.LabelShort, people = f };
        }

        /// <summary>
        /// A letter's words are made: what of the gods made it into them is written into the colony's book as heard in a letter
        /// (and a story retold, a hand written of, are not offered again); the god its writer named becomes a name the colony
        /// has heard.
        /// </summary>
        public static void Written(IReadOnlyDictionary<string, string> values, string text)
        {
            var lore = GodLore.Instance;
            try
            {
                if (lore == null || text.NullOrEmpty() || values == null) return;
                foreach (var kv in waiting)
                {
                    if (!text.Contains(kv.Key)) continue;
                    var w = kv.Value;
                    GodLore.Heard(w.teller, w.god, kv.Key, "Letter", w.story, -1, w.name, w.people, w.by);
                    if (w.story != null && !lore.retold.Contains(w.story)) lore.retold.Add(w.story);
                    if (w.story != null)
                    {
                        // The tale whole in its chapter, as the letter has it, and the signs it explains.
                        GodLore.Set(BookPart.Tales, w.god, w.name, kv.Key, w.by, w.people, "Letter", w.story, -1);
                        foreach (var sign in w.god?.def?.Story(w.story)?.teaches ?? new List<string>())
                            GodLore.LearnSign(w.god, sign, w.story, w.name, w.by, w.people, "Letter");
                    }
                    if (w.hand != null) w.hand.written = true;
                }
                if (values.TryGetValue(GodsMath.God, out string named) && !named.NullOrEmpty() && text.Contains(named) && lastLetterGod != null && lastLetterTeller != null)
                {
                    lore.Page(named, lastLetterGod, "CC_GodLore_FromLetter".Translate(lastLetterTeller.Named("TELLER")).Resolve());
                    if (lastLetterPeople != null) lore.Called(named, lastLetterGod, lastLetterBy, lastLetterPeople, "Letter");
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a letter's words", e);
            }
            finally
            {
                waiting.Clear();
                lastLetterGod = null;
                lastLetterTeller = null;
                lastLetterBy = null;
                lastLetterPeople = null;
            }
        }

        /// <summary>The god a letter being made names ({GOD}), and its writer, until its words are known.</summary>
        internal static God lastLetterGod;
        internal static string lastLetterTeller;
        /// <summary>The writer by their own name (null: written for their people), and their people: whose name for the god it is.</summary>
        internal static string lastLetterBy;
        internal static Faction lastLetterPeople;
    }
}
