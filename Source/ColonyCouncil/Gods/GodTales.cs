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
    /// their people's gods (its pieces in order, GodLoreMath.Next), a saying, or a word on two gods; the colony's priest or
    /// moral guide, when it's them, as a sermon.</item>
    /// <item>A visitor of another people near a colonist tells a piece of their own gods' stories, by their own names for
    /// them: a name new to the colony becomes a page of its own.</item>
    /// <item>A neighbour's letter retells one of their stories whole, or speaks of a god's hand they heard of at the colony
    /// ({GODTALE}, {GODHAND} in the postscripts), written into the book only once the words are in the letter
    /// (<see cref="Written"/>).</item>
    /// </list>
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
            switch (GodLoreMath.Talk(Rand.Value))
            {
                case GodTalkKind.Saying when Saying(teller, listener, mine):
                    return true;
                case GodTalkKind.Tie when TieWords(teller, listener):
                    return true;
                default:
                    return Tell(teller, listener, mine, sermon ? "Sermon" : "Supper");
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
                return Tell(v, listener, theirs, hour >= GodLoreMath.EveningFrom || hour < 5 ? "Fire" : "Gate");
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
            // The other gods named in it are names the colony has now heard.
            foreach (var other in story.with ?? new List<string>())
            {
                var og = Gods.ByKey(other);
                if (og == null) continue;
                string on = lore.PageOf(og)?.name ?? og.name;
                lore.Page(on, og, "CC_GodLore_FromStory".Translate(GodLore.TellerOf(teller).Named("TELLER"), story.title.UncapitalizeFirst().Named("TITLE")).Resolve());
            }
            return true;
        }

        /// <summary>A saying about one of their gods, said to someone near and written as the teller's belief.</summary>
        private static bool Saying(Pawn teller, Pawn listener, List<string> gods)
        {
            var g = gods.Select(Gods.ByKey).Where(x => x?.def?.sayings != null && x.def.sayings.Count > 0).InRandomOrder().FirstOrDefault();
            if (g == null) return false;
            string line = g.def.sayings.RandomElement().Formatted(GodLore.NameOf(g, teller).Named(GodsMath.God)).Resolve().CapitalizeFirst();
            if (line.Length > 90) return false;
            Speech.Say(teller, line, listener);
            GodLore.Believes(teller, g, line);
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
            Speech.Say(teller, words, listener);
            GodLore.Heard(teller, a, words, "Supper", null, -1, lore.PageOf(a).name);
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
                    waiting[text] = new LetterLore { god = g, teller = teller, name = name, hand = hand };
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
            waiting[retelling] = new LetterLore { god = tale.g, teller = teller, name = tn, story = tale.s.key };
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
                    GodLore.Heard(w.teller, w.god, kv.Key, "Letter", w.story, -1, w.name);
                    if (w.story != null && !lore.retold.Contains(w.story)) lore.retold.Add(w.story);
                    if (w.hand != null) w.hand.written = true;
                }
                if (values.TryGetValue(GodsMath.God, out string named) && !named.NullOrEmpty() && text.Contains(named) && lastLetterGod != null && lastLetterTeller != null)
                    lore.Page(named, lastLetterGod, "CC_GodLore_FromLetter".Translate(lastLetterTeller.Named("TELLER")).Resolve());
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
            }
        }

        /// <summary>The god a letter being made names ({GOD}), and its writer, until its words are known.</summary>
        internal static God lastLetterGod;
        internal static string lastLetterTeller;
    }
}
