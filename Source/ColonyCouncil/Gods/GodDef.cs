using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One of the nine (Defs/GodDefs/Rimfolk_Gods.xml; docs/gods-pantheon-brainstorm.md; docs/design/gods.md, "the
    /// contract"): a being with names and a character. Its words: the short name (the label, and <see cref="key"/>, the
    /// name every part keys it by), its epithet, its names among the peoples by age, what it is, how it is seen, its
    /// character, what it wants, what pleases and what angers it, the sayings about it, the lines people say when they see
    /// its hand, a scoffer's answers, what a neighbour writes of its hand, and its old stories told a piece at a time. Its
    /// workings: its colours (for type), a domain and a second (the stage 2 parts' fallback only), its nature and kind, the
    /// look its presence is drawn in, its sacred animals, its ties to the others (each with its own words), and who reveres
    /// it (Rimfolk's peoples and the player's mods' by name, the rest by their kind).
    /// <para>
    /// The gods are an unseen presence, like a storyteller (the player, 11 November 2026): none of this is shown as a
    /// page or a list. It reaches the player only as people say or write it, in pieces.
    /// </para>
    /// </summary>
    public class GodDef : Def
    {
        /// <summary>The short name every part keys it by ("Hollin"), never translated.</summary>
        public string key;
        [MustTranslate] public string epithet;
        public Gender gender = Gender.None;
        /// <summary>What it is, after its name in a sentence ({GODOF}): "the mother of every root under the world".</summary>
        [MustTranslate] public string godOf;
        /// <summary>What people see when it shows itself and the colony has never heard of it (over it on the map, on its card): "a great white thrumbo".</summary>
        [MustTranslate] public string unheard;

        /// <summary>Its names among the peoples, by age (the first of each is the one a speaker of that age uses), and its others.</summary>
        [MustTranslate] public List<string> tribalNames = new List<string>(), medievalNames = new List<string>(), industrialNames = new List<string>(), spacerNames = new List<string>();
        [MustTranslate] public List<string> otherNames = new List<string>();

        /// <summary>What it is, how it is seen, its character, and what it wants: the lore the parts write from, in paragraphs.</summary>
        [MustTranslate] public string whatItIs, howSeen, character, wants;
        [MustTranslate] public List<string> pleases = new List<string>(), angers = new List<string>();

        /// <summary>Sayings about it, one sentence each, said over a head ({GOD} is the speaker's name for it).</summary>
        [MustTranslate] public List<string> sayings = new List<string>();
        /// <summary>
        /// What it is and how it is seen, as people tell it: one sentence each, said over a head in order at the fire or at the
        /// gate ({GOD} the teller's name for it), and set down in the colony's book as told (Gods/GodTales.cs, WhatOf).
        /// </summary>
        [MustTranslate] public List<string> whatLines = new List<string>(), seenLines = new List<string>();
        /// <summary>A believer's line when its hand is seen in an event (tagged [gift], [sign], [wrath]; [near] when it is drawn near, [veiled] when not), one sentence.</summary>
        [MustTranslate] public List<string> hands = new List<string>();
        /// <summary>A scoffer's answer where the colony doesn't know the hand for the god's, one sentence.</summary>
        [MustTranslate] public List<string> scoffs = new List<string>();
        /// <summary>What a neighbour writes of its hand at the colony, in a letter's postscript ({GOD}: the writer's name for it).</summary>
        [MustTranslate] public List<string> letterLines = new List<string>();

        /// <summary>Its old stories, each told in pieces.</summary>
        public List<GodStory> stories = new List<GodStory>();

        /// <summary>Its ties to the others, authored once on one of the two.</summary>
        public List<GodTieSpec> ties = new List<GodTieSpec>();

        /// <summary>Who reveres it: peoples by their FactionDef's name (Rimfolk's and the player's mods', looked up by name), and the rest by kind (GodLoreMath.Kinds).</summary>
        public List<string> peoples = new List<string>(), kinds = new List<string>();

        public Color color = Color.white, color2 = Color.gray;
        public GodDomain domain, second;
        public GodNature nature = GodNature.Patient;
        public GodKind kind = GodKind.Ordinary;
        /// <summary>Favor doesn't move it at all (Helios, who gives without looking): what is meant for it goes along its ties (to Selene).</summary>
        public bool unmoved;
        /// <summary>The look its presence is drawn in (a GodLookDef's defName), or none.</summary>
        public string look;
        /// <summary>The animals sacred to it (defNames, the game's and the player's mods', looked up softly).</summary>
        public List<string> sacredAnimals = new List<string>();

        /// <summary>Its names for an age (0 tribal to 3 spacer).</summary>
        public List<string> NamesOf(int age) => age <= 0 ? tribalNames : age == 1 ? medievalNames : age == 2 ? industrialNames : spacerNames;

        [Unsaved] private List<ThingDef> animals;

        /// <summary>The sacred animal kinds that are loaded.</summary>
        public List<ThingDef> Animals => animals ??= (sacredAnimals ?? new List<string>()).Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d?.race != null).ToList();

        /// <summary>A story of its by key, or null.</summary>
        public GodStory Story(string storyKey) => stories?.FirstOrDefault(s => s != null && s.key == storyKey);

        private static Dictionary<string, GodDef> byKey;

        /// <summary>The god's def by its short name (any case), or null.</summary>
        public static GodDef Of(string k)
        {
            if (k.NullOrEmpty()) return null;
            if (byKey == null)
            {
                byKey = new Dictionary<string, GodDef>(System.StringComparer.OrdinalIgnoreCase);
                foreach (var d in DefDatabase<GodDef>.AllDefsListForReading)
                    if (d != null && !d.key.NullOrEmpty())
                        byKey[d.key] = d;
            }
            return byKey.TryGetValue(k, out var x) ? x : null;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (key.NullOrEmpty() || !GodLoreMath.Keys.Contains(key)) yield return "a god's key is one of the nine short names (" + string.Join(", ", GodLoreMath.Keys) + ")";
            if (defName != "Rimfolk_God_" + key) yield return "a god's defName is Rimfolk_God_ and its key";
            if (epithet.NullOrEmpty()) yield return "a god has an epithet (\"the Rootmother\", \"Old Croak\")";
            if (godOf.NullOrEmpty()) yield return "a god says what it is (godOf)";
            if (unheard.NullOrEmpty()) yield return "a god says what people see of it before they have heard of it (unheard)";
            if (tribalNames.NullOrEmpty() || medievalNames.NullOrEmpty() || industrialNames.NullOrEmpty() || spacerNames.NullOrEmpty())
                yield return "a god has a name among the peoples of every age";
            if (whatItIs.NullOrEmpty() || howSeen.NullOrEmpty() || character.NullOrEmpty() || wants.NullOrEmpty()) yield return "a god has its lore: what it is, how it is seen, its character, what it wants";
            if (sayings.NullOrEmpty() || hands.NullOrEmpty() || scoffs.NullOrEmpty()) yield return "a god has sayings, believers' lines and scoffers' answers";
            if (whatLines.NullOrEmpty() || seenLines.NullOrEmpty()) yield return "a god has lines on what it is and how it is seen, as people tell it";
            if (stories.NullOrEmpty() || stories.Count < 2) yield return "a god has at least two stories";
            if (stories != null)
                foreach (var s in stories)
                    if (s == null || s.key.NullOrEmpty() || s.pieces.NullOrEmpty() || s.retelling.NullOrEmpty())
                        yield return "a story has a key, its pieces and a retelling";
            if (ties != null)
                foreach (var t in ties)
                    if (t == null || !GodLoreMath.Keys.Contains(t.other) || t.other == key || t.words.NullOrEmpty())
                        yield return "a tie names another of the nine and has words of its own";
            if (kinds != null)
                foreach (var k in kinds)
                    if (!GodLoreMath.Kinds.Contains(k))
                        yield return "'" + k + "' isn't a kind of people (" + string.Join(", ", GodLoreMath.Kinds) + ")";
            if (domain == GodDomain.None) yield return "a god has a domain (the fallback key)";
        }
    }

    /// <summary>
    /// One of the old stories: told in pieces, one sentence each, by people at the fire, a visitor, a priest (each piece in
    /// order, never all at once), and retold whole in a neighbour's letter. <see cref="with"/>: the other gods it is of.
    /// </summary>
    public class GodStory
    {
        public string key;
        [MustTranslate] public string title;
        [MustTranslate] public List<string> pieces = new List<string>();
        [MustTranslate] public string retelling;
        public List<string> with = new List<string>();
        /// <summary>The signs it explains as the god's own (events' def names): heard whole, the colony knows them (GodLore.LearnSign, Gods.CardName).</summary>
        public List<string> teaches = new List<string>();
    }

    /// <summary>A tie authored between two of the nine: the other's key, what it is, how strong, and its words (one sentence, as people say it).</summary>
    public class GodTieSpec
    {
        public string other;
        public GodTieKind kind = GodTieKind.Allies;
        public float strength = 0.5f;
        [MustTranslate] public string words;
    }
}
