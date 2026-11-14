using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>How a colonist holds one god, in words (the Culture tab, the book): from leaning toward it to fervent, or in dread of it.</summary>
    public enum BeliefWord
    {
        None,
        Leans,
        Holds,
        Devoted,
        Fervent,
        Uneasy,
        Dread
    }

    /// <summary>What first drew someone to a god, for the line of why.</summary>
    public enum DrawKind
    {
        None,
        Trait,
        Passion,
        Xenotype,
        Backstory,
        People,
        Faith,
        Bond,
        Turned
    }

    /// <summary>Something that moves a colonist's belief in a god (PawnGodsMath.Amount).</summary>
    public enum BeliefEvent
    {
        Prayed,
        PrayedAtShrine,
        Answered,
        Credited,
        Unanswered,
        Crisis,
        Offered,
        VowKept,
        GreatVowKept,
        VowBroken,
        Festival,
        RiteLed,
        OwnRite,
        Saw,
        SawPlain,
        SawBlessing,
        SawOmen,
        Gift,
        Dreamt,
        Told,
        Telling,
        Mocked,
        LivedThrough,
        Bickered,
        TurnedFrom,
        TurnedTo,
        /// <summary>A gift left at the god's place found taken, in the god's own way (a sapling, black soil, a warm floor).</summary>
        SawTaken,
        /// <summary>A god's own presence lived through on the land (the green leaning, a white silence), to its end.</summary>
        LivedPresence
    }

    /// <summary>The small rite each god's devotees keep (PawnGodsMath.RiteOf): where they go and what they do there.</summary>
    public enum BeliefRite
    {
        /// <summary>Hollin's: a hand on a tree's trunk in passing.</summary>
        Tree,
        /// <summary>Smoulder's: a coin dropped into a crack in the rock.</summary>
        Rock,
        /// <summary>Bellwether's: a while beside a grazing beast, the grass left long.</summary>
        Grazing,
        /// <summary>Morel's: scraps buried in the soil.</summary>
        Soil,
        /// <summary>Ennu's: an extra bowl set at the table.</summary>
        Table,
        /// <summary>Pip's: two taps on a working machine.</summary>
        Machine,
        /// <summary>Cirrus's: a look up at the sky at dusk.</summary>
        Sky,
        /// <summary>Hush's: a moment's silence out in the cold.</summary>
        Cold,
        /// <summary>Undertow's: a whisper into the dark at night.</summary>
        Dark,
        /// <summary>Helios's: facing the sunrise.</summary>
        Sunrise,
        /// <summary>Selene's: a look at the moon at night.</summary>
        Moon,
        /// <summary>Tamiu's: a sit by a grave at dusk, else a cat fed.</summary>
        Grave,
        /// <summary>Murmur's: a hum at the water's edge or the well.</summary>
        Water,
        /// <summary>Ducat's: a coin turned over at the silver.</summary>
        Silver
    }

    /// <summary>What a colonist is, as the draws read it, with no game types (PawnGods.Sketch makes it).</summary>
    public sealed class BeliefSketch
    {
        /// <summary>Their traits: defName and degree, and the label for the line of why.</summary>
        public List<(string def, int degree, string label)> traits = new List<(string, int, string)>();
        /// <summary>Their skills with a passion: defName, 1 minor or 2 major, the label.</summary>
        public List<(string def, int passion, string label)> passions = new List<(string, int, string)>();
        public string xenotype, xenotypeLabel;
        /// <summary>Their backstories' words (titles and defNames), and the title for the line of why.</summary>
        public List<(string words, string title)> backstories = new List<(string, string)>();
        /// <summary>The gods their own people revere (keys), and the people's name.</summary>
        public List<string> peopleGods = new List<string>();
        public string people;
        /// <summary>What their faith's memes lean toward, by god key (0 to 1, the strongest 1), and the faith's name.</summary>
        public Dictionary<string, float> faith = new Dictionary<string, float>();
        public string faithName;
        /// <summary>A bond with a cat, or with another animal.</summary>
        public bool catBond, animalBond;
        public bool devout, scoffer;
    }

    /// <summary>One god's draw on someone: how strongly, and the strongest reason.</summary>
    public struct BeliefDraw
    {
        public string god;
        public float amount;
        public DrawKind kind;
        public string what;
    }

    /// <summary>
    /// The pure rules of each colonist's own belief in each god (the player, 14 November 2026: "pawns should have and develop
    /// kind of a relationship in a way, or rather a belief to gods ... the more they pray and interact to a god, that they
    /// kind of, yeah, get a stronger belief in that god"; docs/gods-next-proposals.md, section E). No game types, so
    /// Tools/Tests covers them.
    /// <list type="bullet">
    /// <item>A belief is a pawn fact, never the god's: how strongly someone holds a god (0 to 100) and how much they dread it
    /// (0 to 100). It is shown in words only (<see cref="Word"/>), for a god the colony has heard of, by the colony's name for it.</item>
    /// <item>What draws them, once, when they are first among the colony's people (<see cref="Draw"/>): their traits, their
    /// passions, what they are (their xenotype), their past (their backstories' words), the gods of the people they came
    /// from, their faith's memes, a bond with a cat or a beast. Never their parents or kin: nothing here reads a family.
    /// Character alone takes them as far as holding a god (<see cref="DrawCap"/>); devotion comes from what they do and see.</item>
    /// <item>How it grows (<see cref="Grow"/>, <see cref="Amount"/>): prayer, an answer, an offering carried, a vow kept, a
    /// festival, their own small rite, a god's moment seen (the god they took it for, even wrongly), a gift, a dream, a tale.
    /// Each step is smaller the more they hold already.</item>
    /// <item>How it fades: unused for <see cref="NeglectDays"/>, a little a day, down to half of what their character drew
    /// (<see cref="Fade"/>); a prayer unanswered, a crisis of faith, a vow broken, a scoffer's word. Dread fades slower.</item>
    /// <item>Dread (<see cref="DreadFrom"/>): a god's anger that hurt them or happened beside them. They still believe, in
    /// fear: they keep away from its places and flinch at its signs.</item>
    /// <item>Won over (<see cref="WonOver"/>): a strong moment of a god not their first (seen plainly, a gift, an answer)
    /// can make it their first; a scoffer far more rarely.</item>
    /// <item>Whom they turn to (<see cref="Choose"/>): their first god, or one of the moment's domain they hold; never one
    /// they dread more than they hold.</item>
    /// </list>
    /// </summary>
    public static class PawnGodsMath
    {
        public const float Max = 100f;

        /// <summary>From these a belief shows in words: leaning toward a god, holding to it, devoted, fervent.</summary>
        public const float LeansFrom = 10f, HoldsFrom = 30f, DevotedFrom = 55f, FerventFrom = 80f;

        /// <summary>From these a god's dread shows: uneasy about it, in dread of it.</summary>
        public const float UneasyFrom = 20f, DreadFrom = 45f;

        /// <summary>At most this many gods kept a person (the strongest); one below <see cref="Forget"/> in both is let go.</summary>
        public const int MaxHeld = 6;

        public const float Forget = 1f;

        /// <summary>Character alone draws someone this far at most: past holding to a god, short of devotion.</summary>
        public const float DrawCap = 48f;

        /// <summary>Unused this many days, a belief fades by <see cref="FadePerDay"/> a day, down to <see cref="FloorShare"/> of what their character drew.</summary>
        public const float NeglectDays = 12f, FadePerDay = 0.5f, FloorShare = 0.5f;

        /// <summary>Dread lingers this many days untouched, then fades by <see cref="DreadFadePerDay"/> a day.</summary>
        public const float DreadKeepDays = 15f, DreadFadePerDay = 0.6f;

        public static float Clamp(float x) => float.IsNaN(x) ? 0f : Math.Max(0f, Math.Min(Max, x));

        // ================= Words =================

        /// <summary>
        /// How someone holds a god, in words: dread first where it is as strong as their belief (they still believe, in fear);
        /// else their belief's step; None below <see cref="LeansFrom"/>. A scoffer doubts them all (the caller says so instead).
        /// </summary>
        public static BeliefWord Word(float hold, float dread)
        {
            if (dread >= UneasyFrom && dread >= hold) return dread >= DreadFrom ? BeliefWord.Dread : BeliefWord.Uneasy;
            if (hold >= FerventFrom) return BeliefWord.Fervent;
            if (hold >= DevotedFrom) return BeliefWord.Devoted;
            if (hold >= HoldsFrom) return BeliefWord.Holds;
            if (hold >= LeansFrom) return BeliefWord.Leans;
            return BeliefWord.None;
        }

        /// <summary>A word of fear rather than of faith.</summary>
        public static bool Fears(BeliefWord w) => w == BeliefWord.Uneasy || w == BeliefWord.Dread;

        /// <summary>They dread this god more than they hold it: they never pray to it, keep away from its places, flinch at its signs.</summary>
        public static bool Dreads(float hold, float dread) => dread >= UneasyFrom && dread >= hold;

        // ================= What draws them =================

        /// <summary>Traits that draw someone to a god: the game's, Vanilla Traits Expanded's, Anomaly's, Rimfolk's own and A RimWorld of Magic's classes, by defName (degree 0: any).</summary>
        public static readonly (string trait, int degree, string god, float w)[] TraitDraws =
        {
            ("Kind", 0, "Ennu", 22f), ("VTE_Wanderlust", 0, "Ennu", 16f), ("VTE_Wanderlust", 0, "Ducat", 6f), ("Rimfolk_TrailWise", 0, "Ennu", 10f),
            ("Rimfolk_BornCook", 0, "Ennu", 10f),
            ("Greedy", 0, "Ducat", 22f), ("VTE_Tycoon", 0, "Ducat", 22f), ("VTE_Kleptomaniac", 0, "Ducat", 10f),
            ("NightOwl", 0, "Selene", 20f), ("VTE_Insomniac", 0, "Selene", 16f), ("Rimfolk_Healer", 0, "Selene", 12f), ("Rimfolk_Healer", 0, "Morel", 8f),
            ("Ascetic", 0, "Hush", 20f), ("Tough", 0, "Hush", 12f), ("VTE_ColdInclined", 0, "Hush", 18f), ("NaturalMood", -2, "Hush", 6f),
            ("Pyromaniac", 0, "Smoulder", 20f), ("VTE_HeatInclined", 0, "Smoulder", 12f), ("VTE_HeatInclined", 0, "Helios", 10f),
            ("VTE_ChildOfMountain", 0, "Smoulder", 20f), ("Undergrounder", 0, "Smoulder", 18f), ("VTE_Groundbreaker", 0, "Smoulder", 10f),
            ("TooSmart", 0, "Pip", 18f), ("Transhumanist", 0, "Pip", 20f), ("Rimfolk_Mender", 0, "Pip", 12f), ("Rimfolk_Scholar", 0, "Cirrus", 8f), ("Rimfolk_Scholar", 0, "Pip", 6f),
            ("VTE_Ecologist", 0, "Hollin", 22f), ("BodyPurist", 0, "Hollin", 14f), ("Rimfolk_GreenThumb", 0, "Hollin", 16f), ("VTE_Technophobe", 0, "Hollin", 10f),
            ("VTE_AnimalLover", 0, "Bellwether", 18f), ("VTE_Menagerist", 0, "Bellwether", 18f), ("Rimfolk_AnimalFriend", 0, "Bellwether", 16f), ("VTE_DogPerson", 0, "Bellwether", 8f),
            ("VTE_CatPerson", 0, "Mog", 22f),
            ("VTE_ChildOfSea", 0, "Murmur", 22f),
            ("PsychicSensitivity", 2, "Cirrus", 20f), ("PsychicSensitivity", 1, "Cirrus", 12f), ("TorturedArtist", 0, "Cirrus", 16f),
            ("NaturalMood", 2, "Helios", 12f), ("NaturalMood", 1, "Helios", 6f), ("Joyous", 0, "Helios", 10f),
            ("Cannibal", 0, "Undertow", 18f), ("Psychopath", 0, "Undertow", 14f), ("Bloodlust", 0, "Undertow", 8f), ("VoidFascination", 0, "Undertow", 20f),
            ("Occultist", 0, "Undertow", 14f), ("Occultist", 0, "Mog", 6f),
            // A RimWorld of Magic's classes are traits.
            ("Druid", 0, "Hollin", 20f), ("Necromancer", 0, "Morel", 14f), ("Necromancer", 0, "Mog", 10f), ("Undead", 0, "Mog", 12f), ("Lich", 0, "Mog", 10f),
            ("DeathKnight", 0, "Mog", 8f), ("Technomancer", 0, "Pip", 18f), ("StormBorn", 0, "Cirrus", 20f), ("HeartOfFrost", 0, "Hush", 20f),
            ("InnerFire", 0, "Smoulder", 16f), ("InnerFire", 0, "Helios", 8f), ("Geomancer", 0, "Smoulder", 14f), ("Chronomancer", 0, "Selene", 14f),
            ("Priest", 0, "Helios", 10f), ("Paladin", 0, "Helios", 10f), ("Ranger", 0, "Bellwether", 14f), ("Warlock", 0, "Undertow", 10f), ("Succubus", 0, "Undertow", 8f)
        };

        /// <summary>Work loved that draws someone to a god, by skill defName: the weight of a burning passion (half for a minor one).</summary>
        public static readonly (string skill, string god, float w)[] PassionDraws =
        {
            ("Plants", "Hollin", 12f), ("Plants", "Murmur", 6f), ("Mining", "Smoulder", 12f), ("Animals", "Bellwether", 12f), ("Animals", "Mog", 4f),
            ("Medicine", "Selene", 8f), ("Medicine", "Morel", 6f), ("Crafting", "Pip", 8f), ("Crafting", "Smoulder", 5f), ("Construction", "Smoulder", 5f),
            ("Construction", "Pip", 4f), ("Social", "Ennu", 10f), ("Social", "Ducat", 6f), ("Artistic", "Cirrus", 12f), ("Intellectual", "Pip", 6f),
            ("Intellectual", "Cirrus", 6f), ("Cooking", "Ennu", 6f), ("Cooking", "Morel", 4f), ("Shooting", "Bellwether", 4f)
        };

        /// <summary>What someone is that draws them: xenotypes of the game and the player's mods, by defName.</summary>
        public static readonly (string xeno, string god, float w)[] XenotypeDraws =
        {
            ("VRE_Animakin", "Hollin", 25f), ("VRE_Gauranlenkin", "Hollin", 25f), ("VRE_Poluxkin", "Hollin", 25f), ("DevDesigner_XenotypeWoodElves", "Hollin", 20f),
            ("FG_Fairy", "Hollin", 12f), ("FG_PixieFairy", "Hollin", 12f), ("FG_LesserFairy", "Hollin", 12f), ("FG_CourtFairy", "Hollin", 12f),
            ("VRE_Fungoid", "Morel", 25f), ("AG_Mycormorph", "Morel", 25f),
            ("VRE_Lycan", "Selene", 25f), ("VRE_Wolfman", "Selene", 25f), ("DevDesigner_XenotypeDarkElves", "Selene", 18f),
            ("Sanguophage", "Selene", 14f), ("Sanguophage", "Undertow", 8f), ("VRE_Strigoi", "Undertow", 14f), ("VRE_Strigoi", "Selene", 10f),
            ("VRE_Bruxa", "Selene", 14f), ("VRE_Ekkimian", "Selene", 14f),
            ("VREA_AndroidAwakened", "Pip", 25f), ("VREA_AndroidBasic", "Pip", 25f), ("Genie", "Pip", 10f),
            ("Stagz_Merfolk_Abyssal", "Murmur", 25f), ("Stagz_Merfolk_Coastal", "Murmur", 25f), ("AG_Nereid", "Murmur", 22f), ("DV_Bogleg", "Murmur", 20f),
            ("Yttakin", "Hush", 18f),
            ("Dirtmole", "Smoulder", 18f), ("Stoneborn", "Smoulder", 22f), ("AG_Drakonori", "Smoulder", 25f), ("AG_Efreet", "Smoulder", 18f), ("Impid", "Smoulder", 14f),
            ("AG_MindDevourer", "Undertow", 20f), ("AG_Forsaken", "Undertow", 16f),
            ("Starjack", "Cirrus", 18f),
            ("DV_Venator", "Bellwether", 14f)
        };

        /// <summary>Words in someone's past (a backstory's title or defName, a word that starts so) that draw them to a god.</summary>
        public static readonly (string word, string god, float w)[] BackstoryDraws =
        {
            ("farm", "Hollin", 10f), ("farm", "Murmur", 4f), ("grower", "Hollin", 10f), ("gardener", "Hollin", 10f), ("herbal", "Hollin", 10f), ("botan", "Hollin", 10f),
            ("forester", "Hollin", 10f), ("druid", "Hollin", 10f),
            ("miner", "Smoulder", 10f), ("mining", "Smoulder", 10f), ("digger", "Smoulder", 10f), ("quarr", "Smoulder", 10f), ("smith", "Smoulder", 10f),
            ("blacksmith", "Smoulder", 10f), ("forge", "Smoulder", 10f),
            ("hunter", "Bellwether", 10f), ("trapper", "Bellwether", 10f), ("herder", "Bellwether", 10f), ("shepherd", "Bellwether", 10f),
            ("rancher", "Bellwether", 10f), ("tamer", "Bellwether", 10f), ("zookeeper", "Bellwether", 10f),
            ("grave", "Mog", 12f), ("grave", "Morel", 6f), ("undertaker", "Mog", 12f), ("mortician", "Mog", 12f), ("embalm", "Mog", 12f), ("tomb", "Mog", 12f),
            ("coroner", "Mog", 10f), ("coroner", "Morel", 6f),
            ("doctor", "Selene", 8f), ("doctor", "Morel", 6f), ("medic", "Selene", 8f), ("medic", "Morel", 6f), ("nurse", "Selene", 8f), ("healer", "Selene", 8f),
            ("surgeon", "Selene", 6f), ("physician", "Selene", 8f),
            ("innkeeper", "Ennu", 10f), ("cook", "Ennu", 8f), ("chef", "Ennu", 8f), ("servant", "Ennu", 8f), ("butler", "Ennu", 8f), ("refugee", "Ennu", 10f),
            ("wanderer", "Ennu", 10f), ("vagabond", "Ennu", 10f), ("drifter", "Ennu", 10f), ("nomad", "Ennu", 10f), ("pilgrim", "Ennu", 10f),
            ("engineer", "Pip", 10f), ("mechanic", "Pip", 10f), ("technician", "Pip", 10f), ("programmer", "Pip", 10f), ("hacker", "Pip", 10f),
            ("roboticist", "Pip", 10f), ("machinist", "Pip", 10f), ("electrician", "Pip", 10f),
            ("astronomer", "Cirrus", 10f), ("pilot", "Cirrus", 10f), ("stargazer", "Cirrus", 10f), ("navigator", "Cirrus", 8f), ("artist", "Cirrus", 10f),
            ("painter", "Cirrus", 8f), ("sculptor", "Cirrus", 8f), ("poet", "Cirrus", 8f), ("musician", "Cirrus", 8f),
            ("arctic", "Hush", 10f), ("ice", "Hush", 10f), ("tundra", "Hush", 10f), ("winter", "Hush", 10f), ("snow", "Hush", 10f), ("hermit", "Hush", 10f),
            ("monk", "Hush", 8f),
            ("cultist", "Undertow", 8f), ("occult", "Undertow", 8f), ("slave", "Undertow", 6f), ("convict", "Undertow", 6f), ("torturer", "Undertow", 8f),
            ("assassin", "Undertow", 8f),
            ("desert", "Helios", 8f), ("sun", "Helios", 8f),
            ("night", "Selene", 6f), ("watchman", "Selene", 6f), ("dream", "Selene", 6f),
            ("fisher", "Murmur", 10f), ("sailor", "Murmur", 10f), ("seafarer", "Murmur", 10f), ("seaman", "Murmur", 10f), ("boat", "Murmur", 8f),
            ("marsh", "Murmur", 10f), ("swamp", "Murmur", 10f), ("river", "Murmur", 8f), ("diver", "Murmur", 8f),
            ("trader", "Ducat", 10f), ("merchant", "Ducat", 10f), ("banker", "Ducat", 10f), ("accountant", "Ducat", 8f), ("dealer", "Ducat", 8f),
            ("smuggler", "Ducat", 8f), ("tycoon", "Ducat", 10f), ("noble", "Ducat", 6f)
        };

        /// <summary>The gods of the people they came from draw them this much each (at most three).</summary>
        public const float PeopleDraw = 15f;

        /// <summary>Their faith's strongest lean draws them this much (the rest by their share of it).</summary>
        public const float FaithDraw = 10f;

        /// <summary>A bond with a cat draws them to Tamiu, one with another animal to Bellwether.</summary>
        public const float CatBond = 12f, AnimalBond = 6f;

        /// <summary>The devout are drawn a little more, scoffers far less.</summary>
        public const float DevoutDraw = 1.15f, ScofferDraw = 0.35f;

        /// <summary>The words of a past, lowercased, split where anything but a letter is.</summary>
        public static List<string> Words(string s)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(s)) return list;
            var cur = new System.Text.StringBuilder();
            foreach (char c in s)
            {
                if (char.IsLetter(c))
                {
                    // A defName's capitals start a word too ("UrbworldMiner").
                    if (char.IsUpper(c) && cur.Length > 0 && char.IsLower(cur[cur.Length - 1]))
                    {
                        list.Add(cur.ToString());
                        cur.Clear();
                    }
                    cur.Append(char.ToLowerInvariant(c));
                }
                else if (cur.Length > 0)
                {
                    list.Add(cur.ToString());
                    cur.Clear();
                }
            }
            if (cur.Length > 0) list.Add(cur.ToString());
            return list;
        }

        /// <summary>
        /// Each god's draw on someone from what they are (traits, passions, what they are, their past, their people's gods, their
        /// faith, a bond), summed and capped at <see cref="DrawCap"/>, with the strongest reason kept for the line of why; the
        /// devout drawn a little more, a scoffer far less. Strongest first; only gods drawn at least <see cref="Forget"/>.
        /// </summary>
        public static List<BeliefDraw> Draw(BeliefSketch s)
        {
            var sum = new Dictionary<string, float>();
            var best = new Dictionary<string, (float w, DrawKind kind, string what)>();
            void Add(string god, float w, DrawKind kind, string what)
            {
                if (string.IsNullOrEmpty(god) || w <= 0f) return;
                sum[god] = (sum.TryGetValue(god, out float n) ? n : 0f) + w;
                if (!best.TryGetValue(god, out var b) || w > b.w) best[god] = (w, kind, what);
            }
            if (s == null) return new List<BeliefDraw>();
            foreach (var (def, degree, label) in s.traits)
                foreach (var d in TraitDraws)
                    if (d.trait == def && (d.degree == 0 || d.degree == degree))
                        Add(d.god, d.w, DrawKind.Trait, label);
            foreach (var (def, passion, label) in s.passions)
            {
                if (passion <= 0) continue;
                foreach (var d in PassionDraws)
                    if (d.skill == def)
                        Add(d.god, passion >= 2 ? d.w : d.w * 0.5f, DrawKind.Passion, label);
            }
            if (!string.IsNullOrEmpty(s.xenotype))
                foreach (var d in XenotypeDraws)
                    if (d.xeno == s.xenotype)
                        Add(d.god, d.w, DrawKind.Xenotype, s.xenotypeLabel);
            foreach (var (words, title) in s.backstories)
            {
                var ws = Words(words);
                // One backstory draws to a god once, by its strongest word.
                var taken = new Dictionary<string, float>();
                foreach (var d in BackstoryDraws)
                    if (ws.Any(w => w.StartsWith(d.word, StringComparison.Ordinal)) && (!taken.TryGetValue(d.god, out float had) || d.w > had))
                        taken[d.god] = d.w;
                foreach (var kv in taken) Add(kv.Key, kv.Value, DrawKind.Backstory, title);
            }
            foreach (var g in s.peopleGods.Distinct().Take(3)) Add(g, PeopleDraw, DrawKind.People, s.people);
            foreach (var kv in s.faith) Add(kv.Key, FaithDraw * Math.Max(0f, Math.Min(1f, kv.Value)), DrawKind.Faith, s.faithName);
            if (s.catBond) Add("Mog", CatBond, DrawKind.Bond, null);
            if (s.animalBond) Add("Bellwether", AnimalBond, DrawKind.Bond, null);
            float scale = s.scoffer ? ScofferDraw : s.devout ? DevoutDraw : 1f;
            return sum.Select(kv => new BeliefDraw { god = kv.Key, amount = Math.Min(DrawCap, kv.Value * scale), kind = best[kv.Key].kind, what = best[kv.Key].what })
                .Where(d => d.amount >= Forget).OrderByDescending(d => d.amount).ThenBy(d => d.god, StringComparer.Ordinal).Take(MaxHeld).ToList();
        }

        /// <summary>What a faith's memes lean toward, by domain (index by (int)GodDomain), as a share of each god's (its domain whole, its second half), the strongest 1.</summary>
        public static Dictionary<string, float> FaithLean(float[] domainScores, IEnumerable<(string god, GodDomain domain, GodDomain second)> gods)
        {
            var lean = new Dictionary<string, float>();
            if (domainScores == null || gods == null) return lean;
            float S(GodDomain d) => d == GodDomain.None || (int)d >= domainScores.Length ? 0f : Math.Max(0f, domainScores[(int)d]);
            foreach (var (god, domain, second) in gods)
            {
                float v = S(domain) + 0.5f * S(second);
                if (v > 0f) lean[god] = v;
            }
            float top = lean.Count == 0 ? 0f : lean.Values.Max();
            if (top <= 0f) return new Dictionary<string, float>();
            // Only a real lean counts: a third of the strongest at least.
            return lean.Where(kv => kv.Value >= top / 3f).ToDictionary(kv => kv.Key, kv => kv.Value / top);
        }

        // ================= How it grows and fades =================

        /// <summary>How much an event moves someone's belief in a god (below 0 it fades); dread has its own (<see cref="DreadAmount"/>).</summary>
        public static float Amount(BeliefEvent e) => e switch
        {
            BeliefEvent.Prayed => 2f,
            BeliefEvent.PrayedAtShrine => 3f,
            BeliefEvent.Answered => 9f,
            BeliefEvent.Credited => 5f,
            BeliefEvent.Unanswered => -4f,
            BeliefEvent.Crisis => -12f,
            BeliefEvent.Offered => 3f,
            BeliefEvent.VowKept => 8f,
            BeliefEvent.GreatVowKept => 12f,
            BeliefEvent.VowBroken => -10f,
            BeliefEvent.Festival => 3f,
            BeliefEvent.RiteLed => 5f,
            BeliefEvent.OwnRite => 1.5f,
            BeliefEvent.Saw => 4f,
            BeliefEvent.SawPlain => 10f,
            BeliefEvent.SawBlessing => 6f,
            BeliefEvent.SawOmen => 2f,
            BeliefEvent.Gift => 12f,
            BeliefEvent.Dreamt => 4f,
            BeliefEvent.Told => 1.5f,
            BeliefEvent.Telling => 0.5f,
            BeliefEvent.Mocked => -1f,
            BeliefEvent.LivedThrough => 6f,
            BeliefEvent.Bickered => 0.5f,
            BeliefEvent.TurnedFrom => -25f,
            BeliefEvent.TurnedTo => 20f,
            BeliefEvent.SawTaken => 3f,
            BeliefEvent.LivedPresence => 3f,
            _ => 0f
        };

        /// <summary>A step of belief: what is gained is smaller the more they hold already (a fervent believer gains little more); what is lost is lost in full.</summary>
        public static float Grow(float hold, float amount)
        {
            hold = Clamp(hold);
            if (float.IsNaN(amount) || amount == 0f) return hold;
            if (amount < 0f) return Clamp(hold + amount);
            return Clamp(hold + amount * (1f - hold / (Max * 1.15f)));
        }

        /// <summary>
        /// A day of neglect: past <see cref="NeglectDays"/> since it last grew, a belief fades by <see cref="FadePerDay"/>, never below
        /// <see cref="FloorShare"/> of what their character drew (a kind heart still leans toward the Guest), nor rises to it.
        /// </summary>
        public static float Fade(float hold, float drawn, float daysUnused)
        {
            hold = Clamp(hold);
            if (daysUnused < NeglectDays) return hold;
            float floor = Clamp(drawn) * FloorShare;
            if (hold <= floor) return hold;
            return Math.Max(floor, hold - FadePerDay);
        }

        /// <summary>How much dread a god's anger leaves: on them, beside them (within a few steps), or only seen; a believer of that god fears it the more.</summary>
        public static float DreadAmount(bool onThem, bool beside, float hold)
        {
            float d = onThem ? 22f : beside ? 12f : 4f;
            return d * (1f + Math.Min(1f, Clamp(hold) / Max) * 0.5f);
        }

        /// <summary>Dread lingers <see cref="DreadKeepDays"/>, then fades by <see cref="DreadFadePerDay"/> a day.</summary>
        public static float FadeDread(float dread, float daysSince) => daysSince < DreadKeepDays ? Clamp(dread) : Math.Max(0f, Clamp(dread) - DreadFadePerDay);

        // ================= Won over =================

        /// <summary>How strong a moment is, to win someone over: a god seen plainly, a gift to them, a prayer answered, a kindness seen.</summary>
        public static float Strength(BeliefEvent e) => e switch
        {
            BeliefEvent.SawPlain => 1f,
            BeliefEvent.Gift => 1f,
            BeliefEvent.Answered => 0.6f,
            BeliefEvent.SawBlessing => 0.4f,
            BeliefEvent.LivedThrough => 0.3f,
            BeliefEvent.LivedPresence => 0.15f,
            _ => 0f
        };

        /// <summary>The chance a strong moment wins someone over to a god, at its full strength; a scoffer this share of it.</summary>
        public const float WonOverChance = 0.3f, ScofferWonOver = 0.35f;

        /// <summary>
        /// Someone won over to a god after a strong moment: a god not already their first (they hold another more), not one
        /// they dread more than they hold, by the moment's strength and the roll; a scoffer far more rarely.
        /// </summary>
        public static bool WonOver(float holdThis, float dreadThis, float firstOther, float strength, bool scoffer, float roll)
        {
            if (strength <= 0f || Dreads(holdThis, dreadThis)) return false;
            if (!scoffer && holdThis >= firstOther) return false;
            float chance = WonOverChance * Math.Min(1f, strength) * (scoffer ? ScofferWonOver : 1f);
            return roll < chance;
        }

        /// <summary>Won over, they hold the god this much: past their first by <see cref="WonOverMargin"/>, and holding to it at least.</summary>
        public const float WonOverMargin = 8f;

        public static float WonOverHold(float holdThis, float firstOther) => Clamp(Math.Max(holdThis, Math.Max(firstOther + WonOverMargin, HoldsFrom + 5f)));

        // ================= Whom they turn to =================

        /// <summary>
        /// Of someone's gods ((hold, dread, of the moment's domain) each), the one they pray to and swear by, or -1 to fall back
        /// on the moment's domain: never one they dread more than they hold. A devoted believer turns to their first god in
        /// everything; else to one of the moment's domain they at least lean toward; else to their first if they hold to it;
        /// with no moment, to their first they at least lean toward.
        /// </summary>
        public static int Choose(IList<(float hold, float dread, bool ofDomain)> gods, bool domainAsked)
        {
            if (gods == null || gods.Count == 0) return -1;
            int first = -1, domain = -1;
            for (int i = 0; i < gods.Count; i++)
            {
                var (hold, dread, of) = gods[i];
                if (hold < LeansFrom || Dreads(hold, dread)) continue;
                if (first < 0 || hold > gods[first].hold) first = i;
                if (of && (domain < 0 || hold > gods[domain].hold)) domain = i;
            }
            if (first < 0) return -1;
            if (!domainAsked) return first;
            if (gods[first].hold >= DevotedFrom) return first;
            if (domain >= 0) return domain;
            return gods[first].hold >= HoldsFrom ? first : -1;
        }

        /// <summary>The domain of someone's work, for whom they turn to with nothing else to go by (skill defName to domain).</summary>
        public static GodDomain WorkDomain(string skill) => skill switch
        {
            "Plants" => GodDomain.Harvest,
            "Mining" => GodDomain.Craft,
            "Crafting" => GodDomain.Craft,
            "Construction" => GodDomain.Craft,
            "Animals" => GodDomain.Wild,
            "Medicine" => GodDomain.Healing,
            "Social" => GodDomain.Way,
            "Cooking" => GodDomain.Hearth,
            "Intellectual" => GodDomain.Machine,
            "Artistic" => GodDomain.Night,
            "Shooting" => GodDomain.Hunt,
            "Melee" => GodDomain.War,
            _ => GodDomain.None
        };

        // ================= The small rite =================

        /// <summary>Each god's devotees' small rite (one a god, by key).</summary>
        public static readonly Dictionary<string, BeliefRite> Rites = new Dictionary<string, BeliefRite>(StringComparer.Ordinal)
        {
            ["Hollin"] = BeliefRite.Tree, ["Smoulder"] = BeliefRite.Rock, ["Bellwether"] = BeliefRite.Grazing, ["Morel"] = BeliefRite.Soil,
            ["Ennu"] = BeliefRite.Table, ["Pip"] = BeliefRite.Machine, ["Cirrus"] = BeliefRite.Sky, ["Hush"] = BeliefRite.Cold,
            ["Undertow"] = BeliefRite.Dark, ["Helios"] = BeliefRite.Sunrise, ["Selene"] = BeliefRite.Moon, ["Mog"] = BeliefRite.Grave,
            ["Murmur"] = BeliefRite.Water, ["Ducat"] = BeliefRite.Silver
        };

        public static BeliefRite? RiteOf(string god) => god != null && Rites.TryGetValue(god, out var r) ? r : (BeliefRite?)null;

        /// <summary>
        /// When a rite is kept, by the hour of the day: dusk, dawn, the night, or the waking day; Hush's silence out of doors
        /// on any waking hour of a cold day (5 degrees or less), else only at nightfall.
        /// </summary>
        public static bool RiteHour(BeliefRite r, int hour, float outdoorTemp) => r switch
        {
            BeliefRite.Sky => hour >= 17 && hour <= 20,
            BeliefRite.Grave => hour >= 17 && hour <= 21,
            BeliefRite.Sunrise => hour >= 5 && hour <= 8,
            BeliefRite.Moon => hour >= 20 || hour <= 2,
            BeliefRite.Dark => hour >= 19 || hour <= 3,
            BeliefRite.Cold => outdoorTemp <= 5f ? hour >= 6 && hour <= 22 : hour >= 20 && hour <= 22,
            _ => hour >= 7 && hour <= 21
        };

        /// <summary>
        /// The chance in an hour that someone keeps their god's small rite, by how they hold it, when the hour is the rite's and
        /// they are free then (about a third of their waking hours): about every few days for the devoted, nearly daily for the
        /// fervent, once a week or so for one who holds to it; never below holding to it.
        /// </summary>
        public static float RiteChance(BeliefWord w) => w switch
        {
            BeliefWord.Fervent => 0.15f,
            BeliefWord.Devoted => 0.08f,
            BeliefWord.Holds => 0.03f,
            _ => 0f
        };

        /// <summary>A rite at most once in this many days a person, and at most this many begun in an hour at a home.</summary>
        public const float RiteGapDays = 1f;

        public const int RitesAnHour = 2;

        // ================= Rival devotees =================

        /// <summary>
        /// How likely two devotees of rival gods bicker in the game's own talk (its weight beside the game's chit-chat, which
        /// is 1): both hold their god, two different gods set against each other; the more devoted, the likelier. None otherwise.
        /// </summary>
        public static float BickerWeight(float holdA, float holdB, bool rivals, bool sameGod)
        {
            if (!rivals || sameGod || holdA < HoldsFrom || holdB < HoldsFrom) return 0f;
            float both = (Math.Min(Max, holdA) + Math.Min(Max, holdB)) / (2f * Max);
            return 0.02f + 0.04f * both;
        }

        /// <summary>Two rival devotees bicker at most once in this many days.</summary>
        public const float BickerGapDays = 6f;

        // ================= Dread, kept away =================

        /// <summary>Someone in dread of a god turns away from its place within this many cells, at most once in this many days.</summary>
        public const float KeepAwayRange = 8f, KeepAwayGapDays = 1f;

        /// <summary>They walk at least this far from it.</summary>
        public const float KeepAwayDistance = 14f;

        /// <summary>A flinch at a sign of the god they dread, once in this many days a person.</summary>
        public const float FlinchGapDays = 0.5f;

        // ================= The why =================

        /// <summary>An event is worth a new line of why when it moves belief this much or more (a prayer alone doesn't).</summary>
        public const float WhyFrom = 5f;

        /// <summary>A line of why is kept this short (it shows when a belief is hovered).</summary>
        public const int WhyLength = 110;

        /// <summary>A line cut short at its last word within <see cref="WhyLength"/>.</summary>
        public static string Short(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= WhyLength) return s;
            int cut = s.LastIndexOf(' ', WhyLength - 1);
            if (cut < WhyLength / 2) cut = WhyLength - 1;
            return s.Substring(0, cut).TrimEnd(',', ';', ':', ' ') + "...";
        }

        // ================= Keeping it small =================

        /// <summary>
        /// Which gods someone keeps (indices), the strongest by belief or dread first, at most <see cref="MaxHeld"/>, none faded to
        /// nothing in both.
        /// </summary>
        public static List<int> Keep(IList<(float hold, float dread)> gods)
        {
            var idx = new List<int>();
            if (gods == null) return idx;
            for (int i = 0; i < gods.Count; i++)
                if (gods[i].hold >= Forget || gods[i].dread >= Forget)
                    idx.Add(i);
            return idx.OrderByDescending(i => Math.Max(gods[i].hold, gods[i].dread)).Take(MaxHeld).ToList();
        }
    }
}
