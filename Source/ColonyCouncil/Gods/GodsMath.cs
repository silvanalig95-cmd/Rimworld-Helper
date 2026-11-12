using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>What a god is god of (docs/design/gods.md, "Domains"). None is a god with no second domain.</summary>
    public enum GodDomain
    {
        None,
        /// <summary>The harvest and the rain.</summary>
        Harvest,
        Storm,
        /// <summary>The hunt and the beasts.</summary>
        Hunt,
        /// <summary>Healing and sickness.</summary>
        Healing,
        War,
        /// <summary>Craft and the forge.</summary>
        Craft,
        /// <summary>Roads, trade and travellers.</summary>
        Way,
        Dead,
        /// <summary>Night, the moon and dreams.</summary>
        Night,
        /// <summary>Fire and home.</summary>
        Hearth,
        /// <summary>The green and growing things.</summary>
        Wild,
        /// <summary>Rivers and the sea.</summary>
        Waters,
        Machine,
        Void
    }

    /// <summary>
    /// How a people of its age pictures a god: spirits for tribes, old gods and saints for the medieval, half-forgotten
    /// gods and gods of the machine for the industrial, archotechs for spacers (RimWorld's own god-like minds).
    /// </summary>
    public enum GodForm
    {
        Spirit,
        OldGod,
        Saint,
        HalfForgotten,
        MachineGod,
        Archotech
    }

    /// <summary>
    /// A god that works differently from the rest (the player, 11 November 2026: "there could definitely be also outliers
    /// that work differently or have really different way of interacting, being"). Later parts read it.
    /// </summary>
    public enum GodKind
    {
        Ordinary,
        /// <summary>Cold and exact: bargains in terms, keeps every term, ignores offerings, judges deeds only.</summary>
        Archotech,
        /// <summary>The void: answers anyone, always pays, always takes more.</summary>
        Hunger,
        /// <summary>Lies, swaps blessings for curses and back, loves a joke at a priest's expense.</summary>
        Trickster,
        /// <summary>Silent, worshipped by a few, its mark found in old places.</summary>
        Dead,
        /// <summary>Born from the colony's own faith: weak, growing with devotion.</summary>
        Newborn,
        /// <summary>No worshippers left, bitter or gentle.</summary>
        Fading,
        /// <summary>Held somewhere, and wants out (the Sleeper in the Sunken Vault).</summary>
        Imprisoned
    }

    /// <summary>A god's nature: how fast its favour moves, what offends it, and how loudly it answers.</summary>
    public enum GodNature
    {
        Proud,
        Jealous,
        Generous,
        Capricious,
        Wrathful,
        Patient,
        Playful,
        Cold,
        Grieving
    }

    /// <summary>The colony's standing with a god, in words (never a number in play).</summary>
    public enum FavourWord
    {
        Wrathful,
        Displeased,
        Unknown,
        Noticed,
        Pleased,
        Favoured,
        Beloved
    }

    /// <summary>Where a change of favour came from: its nature and kind weigh each differently.</summary>
    public enum FavourSource
    {
        /// <summary>A deed in the god's domain (a big harvest, a legend beast brought down).</summary>
        Deed,
        Offering,
        Rite,
        Vow,
        /// <summary>Harm to its sacred things.</summary>
        Sacrilege,
        /// <summary>Its rites and its dead let go.</summary>
        Neglect,
        /// <summary>Favour with a god it is tied to (its rival honoured, its ally pleased).</summary>
        Spill,
        /// <summary>The dev tools.</summary>
        Dev
    }

    /// <summary>What offends a god, by its nature (GodsMath.Offends).</summary>
    [Flags]
    public enum GodOffence
    {
        None = 0,
        /// <summary>Its rites and its days let go.</summary>
        Neglect = 1,
        /// <summary>Its rivals honoured.</summary>
        Rivals = 2,
        /// <summary>Harm to its sacred things.</summary>
        Sacrilege = 4,
        /// <summary>The hungry and the stranger turned away.</summary>
        Stinginess = 8,
        /// <summary>A vow or a term broken.</summary>
        BrokenWord = 16,
        /// <summary>The dead left lying.</summary>
        Unburied = 32,
        /// <summary>No feast, no song, no game for too long.</summary>
        Dullness = 64,
        /// <summary>Scoffing at it.</summary>
        Scoffing = 128
    }

    /// <summary>
    /// The pure rules of the pantheon (docs/design/gods.md, "Core: the pantheon"), with no game types so Tools/Tests can
    /// check them: a god's domain from its own words and its faith's memes and precepts, its form by its people's age, the
    /// gods Rimfolk's makers give a people when Ideology isn't there, favour in words and its slow drift back toward
    /// unknown, how a nature and a kind weigh favour, and the text placeholders. How near each god is, and so how plainly it
    /// acts, is GodNearMath's.
    /// </summary>
    public static class GodsMath
    {
        /// <summary>The fourteen domains, in order.</summary>
        public static readonly GodDomain[] Domains =
        {
            GodDomain.Harvest, GodDomain.Storm, GodDomain.Hunt, GodDomain.Healing, GodDomain.War, GodDomain.Craft, GodDomain.Way,
            GodDomain.Dead, GodDomain.Night, GodDomain.Hearth, GodDomain.Wild, GodDomain.Waters, GodDomain.Machine, GodDomain.Void
        };

        public static readonly GodNature[] Natures = (GodNature[])Enum.GetValues(typeof(GodNature));

        public static readonly GodKind[] Kinds = (GodKind[])Enum.GetValues(typeof(GodKind));

        /// <summary>The text placeholders for a god: its name, its epithet, and what it is ("goddess of the harvest").</summary>
        public const string God = "GOD", Epithet = "GODEPITHET", Of = "GODOF";

        public static readonly string[] Placeholders = { God, Epithet, Of };

        // ================= A god's domain, from its words =================

        /// <summary>
        /// Words that point at a domain, and how strongly: a deity's name and type ("Goddess of nature"), a meme's label, the
        /// game's own meme concepts (Ideology's "[god] of [memeConcept]").
        /// </summary>
        private static readonly Dictionary<string, (GodDomain d, float w)[]> WordTable = BuildWords();

        private static Dictionary<string, (GodDomain, float)[]> BuildWords()
        {
            var t = new Dictionary<string, (GodDomain, float)[]>(StringComparer.OrdinalIgnoreCase);
            void W(GodDomain d, float w, params string[] words)
            {
                foreach (var word in words)
                {
                    if (t.TryGetValue(word, out var had)) t[word] = had.Concat(new[] { (d, w) }).ToArray();
                    else t[word] = new[] { (d, w) };
                }
            }
            W(GodDomain.Harvest, 1f, "harvest", "harvests", "grain", "grains", "crop", "crops", "field", "fields", "bread", "sower", "sowing", "seed", "seeds",
                "fertility", "plenty", "abundance", "barley", "wheat", "corn", "rice", "farm", "farming", "farmer", "plough", "plow", "sheaf", "orchard", "fruit",
                "granary", "tea", "coffee", "sweetness");
            W(GodDomain.Harvest, 0.6f, "rain", "rains", "summer", "sun");
            W(GodDomain.Harvest, 0.4f, "ranching");
            W(GodDomain.Storm, 1f, "storm", "storms", "thunder", "lightning", "sky", "skies", "wind", "winds", "weather", "tempest", "hail", "gale", "cloud",
                "clouds", "heavens", "heaven", "sun");
            W(GodDomain.Storm, 0.4f, "rain", "rains");
            W(GodDomain.Hunt, 1f, "hunt", "hunting", "hunter", "hunters", "beast", "beasts", "prey", "chase", "bow", "arrow", "arrows", "herd", "herds", "flock",
                "wolf", "wolves", "stag", "antler", "antlers", "spear", "ranching", "meat");
            W(GodDomain.Hunt, 0.6f, "animals", "animal");
            W(GodDomain.Healing, 1f, "healing", "healer", "health", "medicine", "sickness", "disease", "diseases", "plague", "plagues", "fever", "mercy", "cure",
                "care", "purity", "immaculacy", "body", "wellness");
            W(GodDomain.Healing, 0.5f, "flesh", "life");
            W(GodDomain.War, 1f, "war", "wars", "violence", "dominance", "raiding", "raid", "raids", "assault", "battle", "battles", "victory", "conquest", "sword",
                "swords", "blade", "blades", "shield", "soldier", "soldiers", "slaughter", "fury", "pain", "torment", "torture", "suffering", "strength",
                "gladiator", "duel", "honor", "honour", "piracy", "conquerors", "spear");
            W(GodDomain.War, 0.6f, "blood", "rage", "theft");
            W(GodDomain.Craft, 1f, "craft", "crafts", "forge", "smith", "smithing", "iron", "steel", "stone", "stones", "work", "labour", "labor", "art", "making",
                "maker", "mason", "kiln", "hammer", "anvil", "building", "builders", "tools", "industry", "scrap", "earth", "creation");
            W(GodDomain.Craft, 0.6f, "artifice", "fire", "depth");
            W(GodDomain.Way, 1f, "road", "roads", "way", "ways", "travel", "travellers", "travelers", "journey", "journeys", "trade", "trader", "traders", "merchant",
                "merchants", "market", "coin", "wanderer", "wanderers", "nomad", "nomads", "pilgrim", "pilgrims", "path", "paths", "crossroads", "promises",
                "freedom", "independence", "luck", "fortune", "stranger", "strangers");
            W(GodDomain.Way, 0.5f, "theft", "self");
            W(GodDomain.Dead, 1f, "death", "dead", "dying", "burial", "grave", "graves", "tomb", "tombs", "ancestors", "ancestor", "souls", "soul", "sin",
                "atonement", "repentance", "shame", "judgement", "judgment", "afterlife", "mourning", "bones", "murder", "underworld");
            W(GodDomain.Dead, 0.6f, "depth", "ritual");
            W(GodDomain.Night, 1f, "night", "moon", "moons", "dream", "dreams", "darkness", "dimness", "blackness", "shadow", "shadows", "stars", "star", "sleep",
                "vision", "visions", "sight", "blindness", "perception", "bliss", "mystery", "secrets", "owl", "astrology", "truth", "drugs", "silence");
            W(GodDomain.Night, 0.5f, "ritual", "space");
            W(GodDomain.Hearth, 1f, "hearth", "home", "homes", "family", "kin", "flame", "flames", "light", "mankind", "humanity", "man", "woman", "masculinity",
                "femininity", "mother", "father", "children", "child", "marriage", "love", "feast", "feasts", "people", "nudity", "skin");
            W(GodDomain.Hearth, 0.8f, "fire");
            W(GodDomain.Wild, 1f, "nature", "wild", "wilds", "forest", "forests", "tree", "trees", "wood", "woods", "green", "growth", "leaf", "leaves", "flowers",
                "spring", "grove", "groves", "moss", "gauranlen");
            W(GodDomain.Wild, 0.6f, "animals", "animal", "life", "creation", "beasts");
            W(GodDomain.Waters, 1f, "water", "waters", "river", "rivers", "sea", "seas", "ocean", "oceans", "tide", "tides", "wave", "waves", "flood", "floods",
                "fish", "fishing", "lake", "lakes", "well", "wells", "boat", "boats", "ferry");
            W(GodDomain.Waters, 0.5f, "rain", "rains");
            W(GodDomain.Machine, 1f, "technology", "machine", "machines", "mechanoid", "mechanoids", "mechanical", "silicon", "augmentation", "enhancement",
                "engine", "engines", "gears", "electricity", "signal", "signals", "wire", "archotech", "archotechs", "circuit", "computer", "progress",
                "starships", "starship");
            W(GodDomain.Machine, 0.6f, "artifice", "space");
            W(GodDomain.Void, 1f, "void", "inhumanity", "entities", "entity", "hunger", "abyss", "madness", "horror", "nothing", "nothingness", "chaos",
                "eldritch", "monolith");
            W(GodDomain.Void, 0.5f, "darkness", "rage", "blood", "flesh");
            return t;
        }

        /// <summary>Adds what these words say of a domain, times <paramref name="weight"/>, into <paramref name="scores"/>.</summary>
        public static void ScoreWords(string words, float weight, float[] scores)
        {
            if (string.IsNullOrEmpty(words) || scores == null) return;
            foreach (var raw in words.Split(new[] { ' ', ',', '.', '-', '\'', '(', ')', ':', ';', '!', '?', '"', '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string w = raw.Trim();
                if (w.Length < 2) continue;
                if (!WordTable.TryGetValue(w, out var hits) && !(w.EndsWith("s") && WordTable.TryGetValue(w.Substring(0, w.Length - 1), out hits))) continue;
                foreach (var (d, s) in hits) scores[(int)d] += s * weight;
            }
        }

        /// <summary>
        /// A meme's lean, by its defName: the game's (Ideology, Anomaly, Biotech, Odyssey) and the player's mods' (Alpha
        /// Memes, Vanilla Ideology Expanded and its friends). A meme not here is read by its label's words.
        /// </summary>
        private static readonly Dictionary<string, (GodDomain d, float w)[]> MemeTable = new Dictionary<string, (GodDomain, float)[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Inhuman"] = new[] { (GodDomain.Void, 3f), (GodDomain.Night, 1f) },
            ["Ritualist"] = new[] { (GodDomain.Dead, 1f), (GodDomain.Night, 1f) },
            ["Bloodfeeding"] = new[] { (GodDomain.Void, 2f), (GodDomain.Night, 1f) },
            ["MaleSupremacy"] = new[] { (GodDomain.War, 1f), (GodDomain.Hearth, 1f) },
            ["FemaleSupremacy"] = new[] { (GodDomain.Hearth, 1f), (GodDomain.Harvest, 1f) },
            ["Supremacist"] = new[] { (GodDomain.War, 3f) },
            ["Loyalist"] = new[] { (GodDomain.Hearth, 2f), (GodDomain.Way, 1f) },
            ["Guilty"] = new[] { (GodDomain.Dead, 2f), (GodDomain.Night, 1f) },
            ["Individualist"] = new[] { (GodDomain.Way, 2f) },
            ["Collectivist"] = new[] { (GodDomain.Hearth, 2f), (GodDomain.Craft, 1f) },
            ["AnimalPersonhood"] = new[] { (GodDomain.Wild, 2f), (GodDomain.Hunt, 1f) },
            ["Transhumanist"] = new[] { (GodDomain.Machine, 3f), (GodDomain.Healing, 1f) },
            ["FleshPurity"] = new[] { (GodDomain.Healing, 2f), (GodDomain.Wild, 1f) },
            ["PainIsVirtue"] = new[] { (GodDomain.War, 2f), (GodDomain.Dead, 1f) },
            ["Cannibal"] = new[] { (GodDomain.Void, 2f), (GodDomain.Hunt, 1f) },
            ["Proselytizer"] = new[] { (GodDomain.Way, 2f), (GodDomain.Night, 1f) },
            ["HighLife"] = new[] { (GodDomain.Night, 2f), (GodDomain.Hearth, 1f) },
            ["Nudism"] = new[] { (GodDomain.Wild, 1f), (GodDomain.Hearth, 1f) },
            ["Rancher"] = new[] { (GodDomain.Hunt, 2f), (GodDomain.Harvest, 1f) },
            ["Raider"] = new[] { (GodDomain.War, 3f), (GodDomain.Way, 1f) },
            ["Blindsight"] = new[] { (GodDomain.Night, 3f) },
            ["Darkness"] = new[] { (GodDomain.Night, 3f) },
            ["Tunneler"] = new[] { (GodDomain.Craft, 2f), (GodDomain.Dead, 2f) },
            ["TreeConnection"] = new[] { (GodDomain.Wild, 3f) },
            ["HumanPrimacy"] = new[] { (GodDomain.Hearth, 2f), (GodDomain.Craft, 1f) },
            ["NaturePrimacy"] = new[] { (GodDomain.Wild, 3f), (GodDomain.Harvest, 1f) },
            ["Shipborn"] = new[] { (GodDomain.Machine, 2f), (GodDomain.Night, 2f) },
            ["Structure_Animist"] = new[] { (GodDomain.Wild, 1f), (GodDomain.Hunt, 1f) },
            ["Structure_Archist"] = new[] { (GodDomain.Machine, 3f) },
            ["Structure_TheistEmbodied"] = new[] { (GodDomain.Hearth, 0.5f), (GodDomain.Wild, 0.5f) },
            ["Structure_TheistAbstract"] = new[] { (GodDomain.Night, 0.5f), (GodDomain.Wild, 0.5f) },
            ["Structure_OriginChristian"] = new[] { (GodDomain.Healing, 1f), (GodDomain.Dead, 1f) },
            ["Structure_OriginIslamic"] = new[] { (GodDomain.Way, 1f), (GodDomain.Hearth, 1f) },
            ["Structure_OriginHindu"] = new[] { (GodDomain.Wild, 1f), (GodDomain.Hearth, 1f) },
            ["Structure_OriginBuddhist"] = new[] { (GodDomain.Night, 1f), (GodDomain.Healing, 1f) },
            // Alpha Memes.
            ["AM_Artist"] = new[] { (GodDomain.Craft, 2f) },
            ["AM_Bulwark"] = new[] { (GodDomain.War, 2f), (GodDomain.Craft, 1f) },
            ["AM_CoffeePrimacy"] = new[] { (GodDomain.Harvest, 1f), (GodDomain.Hearth, 1f) },
            ["AM_TeaPrimacy"] = new[] { (GodDomain.Harvest, 1f), (GodDomain.Hearth, 1f) },
            ["AM_Cowboys"] = new[] { (GodDomain.Way, 2f), (GodDomain.Hunt, 1f) },
            ["AM_DeepDevotion"] = new[] { (GodDomain.Night, 1f), (GodDomain.Hearth, 1f) },
            ["AM_Deforestation"] = new[] { (GodDomain.Craft, 2f) },
            ["AM_Epicurean"] = new[] { (GodDomain.Hearth, 2f), (GodDomain.Harvest, 1f) },
            ["AM_Fertility"] = new[] { (GodDomain.Harvest, 3f), (GodDomain.Hearth, 1f) },
            ["AM_GauranlenSupremacy"] = new[] { (GodDomain.Wild, 3f) },
            ["AM_Gladiator"] = new[] { (GodDomain.War, 3f) },
            ["AM_Iconoclast"] = new[] { (GodDomain.Way, 1f), (GodDomain.War, 1f) },
            ["AM_Madness"] = new[] { (GodDomain.Void, 3f) },
            ["AM_Monastic"] = new[] { (GodDomain.Night, 1f), (GodDomain.Hearth, 1f) },
            ["AM_NakedTruth"] = new[] { (GodDomain.Night, 1f), (GodDomain.Wild, 1f) },
            ["AM_NonViolence"] = new[] { (GodDomain.Healing, 2f) },
            ["AM_PainIsDivine"] = new[] { (GodDomain.War, 1f), (GodDomain.Dead, 1f), (GodDomain.Void, 1f) },
            ["AM_PrefabPreference"] = new[] { (GodDomain.Craft, 2f) },
            ["AM_PsychicVampirism"] = new[] { (GodDomain.Void, 2f), (GodDomain.Night, 1f) },
            ["AM_Sadist"] = new[] { (GodDomain.War, 2f), (GodDomain.Void, 1f) },
            ["AM_Sharpshooter"] = new[] { (GodDomain.Hunt, 2f), (GodDomain.War, 1f) },
            ["AM_BiologicalCorruptors"] = new[] { (GodDomain.Void, 2f) },
            ["AM_BiologicalDefilers"] = new[] { (GodDomain.Void, 2f) },
            ["AM_BiologicalReconstructors"] = new[] { (GodDomain.Healing, 2f) },
            ["AM_VampireHunting"] = new[] { (GodDomain.Hunt, 2f), (GodDomain.Night, 1f) },
            ["AM_WaterPrimacy"] = new[] { (GodDomain.Waters, 3f) },
            ["AM_Youth"] = new[] { (GodDomain.Hearth, 1f), (GodDomain.Wild, 1f) },
            ["AM_Structure_Alienism"] = new[] { (GodDomain.Void, 1f), (GodDomain.Night, 1f) },
            ["AM_Structure_Corsair"] = new[] { (GodDomain.Waters, 2f), (GodDomain.War, 1f) },
            ["AM_Structure_Cubicism"] = new[] { (GodDomain.Craft, 1f), (GodDomain.Machine, 1f) },
            ["AM_Structure_FleshCult"] = new[] { (GodDomain.Void, 2f), (GodDomain.Healing, 1f) },
            ["AM_Structure_Horaxian"] = new[] { (GodDomain.Void, 3f) },
            ["AM_Structure_Kemetism"] = new[] { (GodDomain.Dead, 2f), (GodDomain.Harvest, 1f), (GodDomain.Waters, 1f) },
            ["AM_Structure_Mesoamerican"] = new[] { (GodDomain.Harvest, 2f), (GodDomain.War, 1f), (GodDomain.Storm, 1f) },
            ["AM_Structure_Neolithic"] = new[] { (GodDomain.Hunt, 2f), (GodDomain.Wild, 1f) },
            ["AM_Structure_Scavenger"] = new[] { (GodDomain.Way, 1f), (GodDomain.Craft, 1f) },
            ["AM_Structure_SteampunkRevival"] = new[] { (GodDomain.Machine, 2f), (GodDomain.Craft, 2f) },
            // Vanilla Ideology Expanded (memes, structures) and the other Vanilla Expanded memes.
            ["VME_Angler"] = new[] { (GodDomain.Waters, 3f) },
            ["VME_Anonymity"] = new[] { (GodDomain.Night, 1f) },
            ["VME_Aristocratic"] = new[] { (GodDomain.Hearth, 1f), (GodDomain.War, 1f) },
            ["VME_Astrology"] = new[] { (GodDomain.Night, 3f) },
            ["VME_BloodCourt"] = new[] { (GodDomain.Void, 1f), (GodDomain.War, 1f) },
            ["VME_Bushido"] = new[] { (GodDomain.War, 2f) },
            ["VME_CityBuilders"] = new[] { (GodDomain.Craft, 2f), (GodDomain.Hearth, 1f) },
            ["VME_CraftCulture"] = new[] { (GodDomain.Craft, 3f) },
            ["VME_Cultured"] = new[] { (GodDomain.Craft, 1f), (GodDomain.Hearth, 1f) },
            ["VME_Egalitarian"] = new[] { (GodDomain.Hearth, 1f) },
            ["VME_Elders"] = new[] { (GodDomain.Dead, 1f), (GodDomain.Hearth, 1f) },
            ["VME_Emancipation"] = new[] { (GodDomain.Way, 1f) },
            ["VME_ExaltedPriesthood"] = new[] { (GodDomain.Night, 1f) },
            ["VME_FireWorship"] = new[] { (GodDomain.Hearth, 3f) },
            ["VME_Fleshcrafters"] = new[] { (GodDomain.Healing, 1f), (GodDomain.Void, 1f) },
            ["VME_Gestalt"] = new[] { (GodDomain.Machine, 1f), (GodDomain.Night, 1f) },
            ["VME_GodEmperor"] = new[] { (GodDomain.War, 1f), (GodDomain.Hearth, 1f) },
            ["VME_HardcoreIndustrialism"] = new[] { (GodDomain.Machine, 2f), (GodDomain.Craft, 2f) },
            ["VME_Healthcare"] = new[] { (GodDomain.Healing, 3f) },
            ["VME_HolyDiseases"] = new[] { (GodDomain.Healing, 2f), (GodDomain.Void, 1f) },
            ["VME_InsectoidSupremacy"] = new[] { (GodDomain.Void, 2f), (GodDomain.Wild, 1f) },
            ["VME_Light"] = new[] { (GodDomain.Hearth, 2f) },
            ["VME_MechanoidSupremacy"] = new[] { (GodDomain.Machine, 3f) },
            ["VME_Nationalist"] = new[] { (GodDomain.War, 1f), (GodDomain.Hearth, 1f) },
            ["VME_Nomad"] = new[] { (GodDomain.Way, 3f) },
            ["VME_Pacifist"] = new[] { (GodDomain.Healing, 2f) },
            ["VME_PartyLife"] = new[] { (GodDomain.Hearth, 2f) },
            ["VME_Progressive"] = new[] { (GodDomain.Machine, 2f) },
            ["VME_PsychicFocus"] = new[] { (GodDomain.Night, 2f) },
            ["VME_Republic"] = new[] { (GodDomain.Hearth, 2f) },
            ["VME_Royal"] = new[] { (GodDomain.Hearth, 1f), (GodDomain.War, 1f) },
            ["VME_Scrapper"] = new[] { (GodDomain.Craft, 2f) },
            ["VME_SweetTeeth"] = new[] { (GodDomain.Harvest, 1f) },
            ["VME_Trader"] = new[] { (GodDomain.Way, 3f) },
            ["VME_Vegan"] = new[] { (GodDomain.Harvest, 2f), (GodDomain.Wild, 1f) },
            ["VME_ViolentConversion"] = new[] { (GodDomain.War, 2f) },
            ["VME_Structure_Authoritarianism"] = new[] { (GodDomain.War, 1f) },
            ["VME_Structure_Bacchanalianism"] = new[] { (GodDomain.Hearth, 2f), (GodDomain.Harvest, 1f) },
            ["VME_Structure_ChthonianCult"] = new[] { (GodDomain.Void, 3f) },
            ["VME_Structure_Corporate"] = new[] { (GodDomain.Way, 2f), (GodDomain.Machine, 1f) },
            ["VME_Structure_CultOfPersonality"] = new[] { (GodDomain.Hearth, 1f) },
            ["VME_Structure_Eschatologism"] = new[] { (GodDomain.Dead, 2f), (GodDomain.Void, 1f) },
            ["VME_Structure_Esotericism"] = new[] { (GodDomain.Night, 2f) },
            ["VME_Structure_Pantheism"] = new[] { (GodDomain.Wild, 2f) },
            ["VME_Structure_Serketist"] = new[] { (GodDomain.Machine, 1f), (GodDomain.Night, 1f) },
            ["VME_Structure_Shintaoism"] = new[] { (GodDomain.Wild, 1f), (GodDomain.Hearth, 1f) },
            ["VFEA_Isolationist"] = new[] { (GodDomain.Hearth, 1f) },
            ["VVE_Nauticals"] = new[] { (GodDomain.Waters, 3f) },
            ["VVE_Roadragers"] = new[] { (GodDomain.Way, 2f), (GodDomain.War, 1f) },
            ["VVE_Skyseekers"] = new[] { (GodDomain.Storm, 2f), (GodDomain.Machine, 1f) },
            ["VVE_Travelers"] = new[] { (GodDomain.Way, 3f) },
            ["VFEM2_Structure_Norse"] = new[] { (GodDomain.Storm, 2f), (GodDomain.War, 2f) },
            ["VFEP_PirateMeme"] = new[] { (GodDomain.War, 2f), (GodDomain.Waters, 1f) },
            ["TM_Transcendent"] = new[] { (GodDomain.Night, 2f) },
            ["GR_CarefulGeneticists"] = new[] { (GodDomain.Healing, 2f) },
            ["GR_MadScientists"] = new[] { (GodDomain.Void, 1f), (GodDomain.Healing, 1f) },
            ["HAR_Xenophilia"] = new[] { (GodDomain.Way, 1f) },
            ["HAR_Xenophobia"] = new[] { (GodDomain.Hearth, 1f) },
            ["VQE_Technophobia"] = new[] { (GodDomain.Wild, 2f), (GodDomain.Hearth, 1f) }
        };

        /// <summary>Whether a meme is one of those Rimfolk reads by its defName (the rest by their labels).</summary>
        public static bool KnowsMeme(string defName) => !string.IsNullOrEmpty(defName) && MemeTable.ContainsKey(defName);

        /// <summary>Adds a meme's lean (by defName, else its label's words) times <paramref name="weight"/>.</summary>
        public static void ScoreMeme(string defName, string label, float weight, float[] scores)
        {
            if (scores == null) return;
            if (!string.IsNullOrEmpty(defName) && MemeTable.TryGetValue(defName, out var hits))
            {
                foreach (var (d, w) in hits) scores[(int)d] += w * weight;
                return;
            }
            ScoreWords(label, weight, scores);
        }

        /// <summary>Precepts that lean a faith toward a domain, by a part of their defName (the game's own, and mods' alike-named).</summary>
        private static readonly (string part, GodDomain d, float w)[] PreceptTable =
        {
            ("Trees_Desired", GodDomain.Wild, 1f), ("TreeCutting_", GodDomain.Wild, 1f), ("GauranlenConnection", GodDomain.Wild, 1f),
            ("AnimalConnection_Strong", GodDomain.Wild, 1f), ("KillingInnocentAnimals_Abhorrent", GodDomain.Wild, 1f), ("KillingInnocentAnimals_Horrible", GodDomain.Wild, 0.5f),
            ("Raiding_Required", GodDomain.War, 1f), ("Raiding_Respected", GodDomain.War, 0.7f), ("Execution_Required", GodDomain.War, 0.5f),
            ("Execution_Required", GodDomain.Dead, 0.5f), ("Pain_Idealized", GodDomain.War, 1f), ("Slavery_Honorable", GodDomain.War, 0.5f),
            ("Charity_Essential", GodDomain.Hearth, 0.7f), ("Charity_Essential", GodDomain.Way, 0.5f), ("Charity_Important", GodDomain.Hearth, 0.4f),
            ("Corpses_DontCare", GodDomain.Void, 0.3f), ("Darkness_", GodDomain.Night, 1f), ("Cannibalism_Preferred", GodDomain.Void, 1f),
            ("Cannibalism_RequiredStrong", GodDomain.Void, 1f), ("Bloodfeeders_", GodDomain.Void, 0.5f), ("BodyMod_Approved", GodDomain.Machine, 1f),
            ("Prosthetic_Approved", GodDomain.Machine, 0.5f), ("MeatEating_Abhorrent", GodDomain.Harvest, 0.5f), ("MeatEating_Horrible", GodDomain.Harvest, 0.5f),
            ("Mining_Prohibited", GodDomain.Wild, 0.5f), ("MiningYield_High", GodDomain.Craft, 0.5f), ("Research_High", GodDomain.Craft, 0.5f),
            ("Drinking_", GodDomain.Hearth, 0.2f), ("Proselytizing_Frequently", GodDomain.Way, 0.5f)
        };

        /// <summary>Adds what a faith's precepts lean toward, times <paramref name="weight"/>.</summary>
        public static void ScorePrecepts(IEnumerable<string> precepts, float weight, float[] scores)
        {
            if (precepts == null || scores == null) return;
            foreach (var p in precepts)
            {
                if (string.IsNullOrEmpty(p)) continue;
                foreach (var (part, d, w) in PreceptTable)
                    if (p.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
                        scores[(int)d] += w * weight;
            }
        }

        /// <summary>A fresh score for every domain (index by (int)GodDomain).</summary>
        public static float[] NewScores() => new float[Domains.Length + 1];

        /// <summary>
        /// The domain an Ideology deity is god of, and a second one: its own words (its name and type) count three times,
        /// the meme it was made from twice, its faith's memes once and its precepts half. <paramref name="taken"/>: the
        /// domains the faith's other gods have already; a god takes another of its own leanings rather than share, while
        /// that one is at least half as strong. With nothing to read at all, the age's most usual domain by the seed.
        /// </summary>
        public static void DeityDomains(string name, string type, string relatedMeme, string relatedMemeLabel, IEnumerable<(string def, string label)> memes,
            IEnumerable<string> precepts, ICollection<GodDomain> taken, int age, int seed, out GodDomain domain, out GodDomain second)
        {
            var s = NewScores();
            ScoreWords(name, 3f, s);
            ScoreWords(type, 3f, s);
            if (!string.IsNullOrEmpty(relatedMeme) || !string.IsNullOrEmpty(relatedMemeLabel)) ScoreMeme(relatedMeme, relatedMemeLabel, 2f, s);
            if (memes != null)
                foreach (var (def, label) in memes)
                    ScoreMeme(def, label, 1f, s);
            ScorePrecepts(precepts, 0.5f, s);
            Pick(s, taken, age, seed, out domain, out second);
        }

        /// <summary>
        /// The best and second domains of a set of scores (see <see cref="DeityDomains"/>). The second must be at least
        /// two fifths of the first, and is None otherwise.
        /// </summary>
        public static void Pick(float[] scores, ICollection<GodDomain> taken, int age, int seed, out GodDomain domain, out GodDomain second)
        {
            var order = Domains.Where(d => scores[(int)d] > 0f).OrderByDescending(d => scores[(int)d] + AgeLean(age, d) * 0.01f).ToList();
            if (order.Count == 0)
            {
                domain = AgeDefault(age, seed, taken);
                second = GodDomain.None;
                return;
            }
            domain = order[0];
            float best = scores[(int)domain];
            if (taken != null && taken.Contains(domain))
            {
                var free = order.FirstOrDefault(d => !taken.Contains(d) && scores[(int)d] >= best * 0.5f);
                if (free != GodDomain.None) domain = free;
            }
            var d0 = domain;
            second = order.FirstOrDefault(d => d != d0 && scores[(int)d] >= scores[(int)d0] * 0.4f);
        }

        /// <summary>How usual a domain is among gods of an age (0 tribal to 3 spacer): spirits of the hunt and the wild, the medieval harvest and war, the industrial forge and road, the spacer machine.</summary>
        public static float AgeLean(int age, GodDomain d)
        {
            switch (Math.Max(0, Math.Min(3, age)))
            {
                case 0:
                    return d switch
                    {
                        GodDomain.Wild => 2f, GodDomain.Hunt => 2f, GodDomain.Storm => 1.5f, GodDomain.Dead => 1.5f, GodDomain.Night => 1f, GodDomain.Waters => 1f,
                        GodDomain.Harvest => 1f, GodDomain.Hearth => 0.8f, GodDomain.Healing => 0.6f, GodDomain.War => 0.6f, GodDomain.Way => 0.4f, GodDomain.Craft => 0.4f,
                        GodDomain.Void => 0.2f, _ => 0f
                    };
                case 1:
                    return d switch
                    {
                        GodDomain.Harvest => 2f, GodDomain.War => 1.5f, GodDomain.Hearth => 1.5f, GodDomain.Dead => 1.5f, GodDomain.Craft => 1.2f, GodDomain.Healing => 1f,
                        GodDomain.Way => 1f, GodDomain.Storm => 1f, GodDomain.Waters => 0.8f, GodDomain.Night => 0.8f, GodDomain.Hunt => 0.8f, GodDomain.Wild => 0.6f,
                        GodDomain.Void => 0.2f, _ => 0f
                    };
                case 2:
                    return d switch
                    {
                        GodDomain.Craft => 2f, GodDomain.Machine => 1.5f, GodDomain.Way => 1.5f, GodDomain.War => 1.2f, GodDomain.Harvest => 1f, GodDomain.Hearth => 1f,
                        GodDomain.Healing => 1f, GodDomain.Dead => 0.8f, GodDomain.Storm => 0.6f, GodDomain.Waters => 0.6f, GodDomain.Night => 0.6f, GodDomain.Hunt => 0.4f,
                        GodDomain.Wild => 0.4f, GodDomain.Void => 0.3f, _ => 0f
                    };
                default:
                    return d switch
                    {
                        GodDomain.Machine => 3f, GodDomain.Night => 1.5f, GodDomain.Healing => 1f, GodDomain.Way => 1f, GodDomain.Void => 0.7f, GodDomain.Craft => 0.6f,
                        GodDomain.War => 0.6f, GodDomain.Dead => 0.4f, GodDomain.Storm => 0.3f, GodDomain.Hearth => 0.3f, GodDomain.Waters => 0.2f, GodDomain.Harvest => 0.2f,
                        GodDomain.Wild => 0.2f, _ => 0f
                    };
            }
        }

        /// <summary>A domain for a god with no words to read: the age's leanings, by the seed, not one the faith has already where it can.</summary>
        public static GodDomain AgeDefault(int age, int seed, ICollection<GodDomain> taken)
        {
            var pool = Domains.Where(d => AgeLean(age, d) > 0f && (taken == null || !taken.Contains(d))).ToList();
            if (pool.Count == 0) pool = Domains.Where(d => AgeLean(age, d) > 0f).ToList();
            return Weighted(pool, d => AgeLean(age, d), Roll(seed, 17));
        }

        // ================= Rimfolk's makers: a people's gods without Ideology =================

        /// <summary>
        /// The domains of the two or three gods Rimfolk's makers give a people with no faith to read (no Ideology, or a faith
        /// that names no god but holds the world alive: animist spirits, archist minds): the age's leanings, raiders' war and
        /// night, and the land round their towns (forest: the wild; desert: storm and harvest; ice: night and storm; marsh:
        /// waters and the dead; a river or the coast: waters). Raiders always have a god of war. <paramref name="count"/> 0
        /// picks two or three by the seed (three more often in the older ages).
        /// </summary>
        public static List<GodDomain> MakerDomains(int age, bool raiders, string biome, bool water, int seed, int count = 0)
        {
            var w = new Dictionary<GodDomain, float>();
            foreach (var d in Domains) w[d] = AgeLean(age, d);
            if (raiders)
            {
                w[GodDomain.Night] += 1f;
                w[GodDomain.Void] += 0.5f;
                w[GodDomain.Hearth] *= 0.5f;
            }
            foreach (var (d, b) in BiomeLean(biome)) w[d] += b * 1.5f;
            if (water) w[GodDomain.Waters] += 3f;
            // The age's own god counts more (the machine of spacers, the hunt and the wild of tribes).
            var top = Domains.OrderByDescending(d => AgeLean(age, d)).First();
            w[top] += 1.5f;
            if (count <= 0) count = Roll(seed, 3) < (age <= 1 ? 0.6f : 0.35f) ? 3 : 2;
            var picked = new List<GodDomain>();
            if (raiders) picked.Add(GodDomain.War);
            for (int i = 0; picked.Count < count; i++)
            {
                var pool = Domains.Where(d => !picked.Contains(d) && w[d] > 0f).ToList();
                if (pool.Count == 0) break;
                picked.Add(Weighted(pool, d => w[d], Roll(seed, 31 + i * 7)));
            }
            return picked;
        }

        /// <summary>What the land round a people's towns leans its gods toward, by the biome's defName.</summary>
        public static IEnumerable<(GodDomain d, float w)> BiomeLean(string biome)
        {
            if (string.IsNullOrEmpty(biome)) yield break;
            string b = biome.ToLowerInvariant();
            if (b.Contains("forest") || b.Contains("jungle") || b.Contains("grove")) yield return (GodDomain.Wild, 2f);
            if (b.Contains("desert") || b.Contains("arid") || b.Contains("shrub")) yield return (GodDomain.Storm, 1.5f);
            if (b.Contains("desert") || b.Contains("arid") || b.Contains("grass") || b.Contains("steppe") || b.Contains("plain")) yield return (GodDomain.Harvest, 1.2f);
            if (b.Contains("tundra") || b.Contains("ice") || b.Contains("boreal") || b.Contains("glacier") || b.Contains("snow")) yield return (GodDomain.Night, 1.2f);
            if (b.Contains("tundra") || b.Contains("ice") || b.Contains("glacier")) yield return (GodDomain.Storm, 1f);
            if (b.Contains("boreal") || b.Contains("tundra") || b.Contains("grass")) yield return (GodDomain.Hunt, 1f);
            if (b.Contains("swamp") || b.Contains("bog") || b.Contains("marsh")) yield return (GodDomain.Waters, 1.5f);
            if (b.Contains("swamp") || b.Contains("bog") || b.Contains("marsh")) yield return (GodDomain.Dead, 1f);
            if (b.Contains("ocean") || b.Contains("sea") || b.Contains("coast") || b.Contains("lake")) yield return (GodDomain.Waters, 2f);
            if (b.Contains("lava") || b.Contains("scar") || b.Contains("ash") || b.Contains("volcan")) yield return (GodDomain.War, 1f);
            if (b.Contains("glow") || b.Contains("crystal")) yield return (GodDomain.Night, 1f);
        }

        // ================= The age's form =================

        /// <summary>
        /// How a people of an age (0 tribal to 3 spacer) pictures a god of these domains: a spirit for a tribe, an old god for
        /// the medieval (a saint, by the seed, now and then for healing, the dead, the hearth and the road), a half-forgotten
        /// god for the industrial (a god of the machine for the machine and the forge with it), an archotech for spacers.
        /// </summary>
        public static GodForm FormOf(int age, GodDomain domain, GodDomain second, int seed)
        {
            switch (Math.Max(0, Math.Min(3, age)))
            {
                case 0: return GodForm.Spirit;
                case 1:
                    bool holy = domain == GodDomain.Healing || domain == GodDomain.Dead || domain == GodDomain.Hearth || domain == GodDomain.Way;
                    return holy && Roll(seed, 5) < 0.3f ? GodForm.Saint : GodForm.OldGod;
                case 2:
                    return domain == GodDomain.Machine || second == GodDomain.Machine && domain == GodDomain.Craft ? GodForm.MachineGod : GodForm.HalfForgotten;
                default: return GodForm.Archotech;
            }
        }

        /// <summary>The age a form belongs to (0 tribal to 3 spacer), for its names.</summary>
        public static int AgeOfForm(GodForm f) => f switch
        {
            GodForm.Spirit => 0,
            GodForm.OldGod => 1,
            GodForm.Saint => 1,
            GodForm.HalfForgotten => 2,
            GodForm.MachineGod => 2,
            _ => 3
        };

        /// <summary>
        /// The kind a god takes from its form when nothing sets it otherwise: an archotech is one (cold and exact), every
        /// other form ordinary. A kind given by the lore (the Hunger, the Sleeper) or by what happened (fading, dead) wins.
        /// </summary>
        public static GodKind KindOfForm(GodForm f) => f == GodForm.Archotech ? GodKind.Archotech : GodKind.Ordinary;

        // ================= A god's nature =================

        /// <summary>The natures a domain leans to (its first the likeliest).</summary>
        public static GodNature[] NatureLeans(GodDomain d) => d switch
        {
            GodDomain.Harvest => new[] { GodNature.Generous, GodNature.Patient, GodNature.Capricious },
            GodDomain.Storm => new[] { GodNature.Wrathful, GodNature.Capricious, GodNature.Proud },
            GodDomain.Hunt => new[] { GodNature.Proud, GodNature.Playful, GodNature.Patient },
            GodDomain.Healing => new[] { GodNature.Generous, GodNature.Patient, GodNature.Grieving },
            GodDomain.War => new[] { GodNature.Proud, GodNature.Wrathful, GodNature.Jealous },
            GodDomain.Craft => new[] { GodNature.Proud, GodNature.Patient, GodNature.Jealous },
            GodDomain.Way => new[] { GodNature.Playful, GodNature.Generous, GodNature.Capricious },
            GodDomain.Dead => new[] { GodNature.Patient, GodNature.Grieving, GodNature.Cold },
            GodDomain.Night => new[] { GodNature.Capricious, GodNature.Grieving, GodNature.Playful },
            GodDomain.Hearth => new[] { GodNature.Generous, GodNature.Jealous, GodNature.Patient },
            GodDomain.Wild => new[] { GodNature.Capricious, GodNature.Patient, GodNature.Wrathful },
            GodDomain.Waters => new[] { GodNature.Capricious, GodNature.Generous, GodNature.Grieving },
            GodDomain.Machine => new[] { GodNature.Cold, GodNature.Patient, GodNature.Proud },
            GodDomain.Void => new[] { GodNature.Jealous, GodNature.Wrathful, GodNature.Cold },
            _ => new[] { GodNature.Patient }
        };

        /// <summary>
        /// A god's nature by its domain's leanings and its kind, the same for the same seed: an archotech is cold (now and then
        /// patient or proud), a trickster playful, a fading god grieving, the Hunger jealous, a newborn playful or generous.
        /// </summary>
        public static GodNature NatureFor(GodDomain d, GodKind kind, int seed)
        {
            float r = Roll(seed, 11);
            switch (kind)
            {
                case GodKind.Archotech: return r < 0.7f ? GodNature.Cold : r < 0.85f ? GodNature.Patient : GodNature.Proud;
                case GodKind.Trickster: return GodNature.Playful;
                case GodKind.Hunger: return GodNature.Jealous;
                case GodKind.Fading: return r < 0.6f ? GodNature.Grieving : GodNature.Wrathful;
                case GodKind.Dead: return r < 0.5f ? GodNature.Grieving : GodNature.Patient;
                case GodKind.Newborn: return r < 0.5f ? GodNature.Playful : GodNature.Generous;
                case GodKind.Imprisoned: return r < 0.5f ? GodNature.Patient : GodNature.Wrathful;
            }
            var leans = NatureLeans(d);
            return r < 0.5f ? leans[0] : r < 0.8f ? leans[Math.Min(1, leans.Length - 1)] : leans[leans.Length - 1];
        }

        /// <summary>How much a change of favour moves, by the god's nature (gains, or losses when <paramref name="gain"/> is false).</summary>
        public static float Speed(GodNature n, bool gain) => n switch
        {
            GodNature.Proud => gain ? 0.9f : 1.3f,
            GodNature.Jealous => gain ? 1f : 1.2f,
            GodNature.Generous => gain ? 1.25f : 0.8f,
            GodNature.Capricious => 1.5f,
            GodNature.Wrathful => gain ? 0.9f : 1.5f,
            GodNature.Patient => 0.75f,
            GodNature.Playful => 1.2f,
            GodNature.Cold => 0.6f,
            GodNature.Grieving => gain ? 0.8f : 1f,
            _ => 1f
        };

        /// <summary>How many days the god's favour takes to halve back toward unknown (its regard when <paramref name="good"/>, its grudge otherwise).</summary>
        public static float HalfLifeDays(GodNature n, bool good)
        {
            float days = 30f;
            switch (n)
            {
                case GodNature.Patient: days *= 1.5f; break;
                case GodNature.Capricious: days *= 0.6f; break;
                case GodNature.Generous: days *= good ? 1.3f : 0.7f; break;
                case GodNature.Wrathful: days *= good ? 0.9f : 1.6f; break;
                case GodNature.Jealous: days *= good ? 0.8f : 1.3f; break;
                case GodNature.Grieving: days *= 1.3f; break;
                case GodNature.Playful: days *= 0.8f; break;
                case GodNature.Proud: days *= good ? 1f : 1.3f; break;
            }
            return days;
        }

        /// <summary>How loudly a god answers (0 hardly at all to 1 the whole sky): later parts read it for how plain a sign is.</summary>
        public static float Loudness(GodNature n, GodKind kind)
        {
            float l = n switch
            {
                GodNature.Wrathful => 0.9f,
                GodNature.Proud => 0.8f,
                GodNature.Capricious => 0.7f,
                GodNature.Playful => 0.6f,
                GodNature.Jealous => 0.6f,
                GodNature.Generous => 0.5f,
                GodNature.Grieving => 0.3f,
                GodNature.Patient => 0.3f,
                GodNature.Cold => 0.2f,
                _ => 0.5f
            };
            switch (kind)
            {
                case GodKind.Dead: return 0.05f;
                case GodKind.Fading: return l * 0.5f;
                case GodKind.Newborn: return l * 0.6f;
                case GodKind.Hunger: return Math.Max(l, 0.8f);
                case GodKind.Imprisoned: return l * 0.7f;
            }
            return l;
        }

        /// <summary>What offends a god of this nature most.</summary>
        public static GodOffence Offends(GodNature n) => n switch
        {
            GodNature.Proud => GodOffence.Neglect | GodOffence.Scoffing,
            GodNature.Jealous => GodOffence.Rivals | GodOffence.Neglect,
            GodNature.Generous => GodOffence.Stinginess,
            GodNature.Capricious => GodOffence.Dullness | GodOffence.Neglect,
            GodNature.Wrathful => GodOffence.Sacrilege | GodOffence.Scoffing,
            GodNature.Patient => GodOffence.BrokenWord,
            GodNature.Playful => GodOffence.Dullness | GodOffence.Scoffing,
            GodNature.Cold => GodOffence.BrokenWord,
            GodNature.Grieving => GodOffence.Unburied | GodOffence.Neglect,
            _ => GodOffence.None
        };

        /// <summary>The offence a source of lost favour is (None for one that offends nobody in particular).</summary>
        public static GodOffence OffenceOf(FavourSource s) => s switch
        {
            FavourSource.Sacrilege => GodOffence.Sacrilege,
            FavourSource.Neglect => GodOffence.Neglect,
            FavourSource.Spill => GodOffence.Rivals,
            FavourSource.Vow => GodOffence.BrokenWord,
            _ => GodOffence.None
        };

        /// <summary>A loss that is one of the things the god's nature takes worst counts half again.</summary>
        public const float OffenceFactor = 1.5f;

        /// <summary>
        /// How much of a change of favour a god feels, by its nature, its kind and where it came from: an archotech cares
        /// nothing for offerings and little for rites (it judges deeds), the dead hardly hear, a fading god is grateful for
        /// anything, the Hunger takes everything gladly, a newborn feels it all; a loss that offends its nature counts more.
        /// <paramref name="offence"/>: the offence this loss is, when the caller knows better than the source (the dead
        /// left lying is neglect of the dead, Unburied).
        /// </summary>
        public static float Scale(float amount, GodNature n, GodKind kind, FavourSource source, GodOffence offence = GodOffence.None)
        {
            if (float.IsNaN(amount) || amount == 0f) return 0f;
            if (source == FavourSource.Dev) return amount;
            bool gain = amount > 0f;
            float a = amount * Speed(n, gain);
            switch (kind)
            {
                case GodKind.Archotech:
                    if (source == FavourSource.Offering) return 0f;
                    if (source == FavourSource.Rite) a *= 0.25f;
                    break;
                case GodKind.Dead: a *= 0.3f; break;
                case GodKind.Fading: if (gain) a *= 1.5f; break;
                case GodKind.Hunger: if (gain) a *= 1.5f; break;
                case GodKind.Newborn: a *= 1.2f; break;
            }
            if (!gain)
            {
                var off = offence != GodOffence.None ? offence : OffenceOf(source);
                if (off != GodOffence.None && (Offends(n) & off) != 0) a *= OffenceFactor;
            }
            return a;
        }

        // ================= Favour in words =================

        /// <summary>Favour runs from -100 (wrathful) to 100 (beloved); 0 is unknown to the god.</summary>
        public const float Most = 100f;

        /// <summary>The lower edge of each word above wrathful: displeased from -60, unknown from -15, noticed from 10, pleased 30, favoured 55, beloved 80.</summary>
        public const float DispleasedFrom = -60f, UnknownFrom = -15f, NoticedFrom = 10f, PleasedFrom = 30f, FavouredFrom = 55f, BelovedFrom = 80f;

        /// <summary>The word for a standing.</summary>
        public static FavourWord Word(float favour)
        {
            if (float.IsNaN(favour)) return FavourWord.Unknown;
            if (favour >= BelovedFrom) return FavourWord.Beloved;
            if (favour >= FavouredFrom) return FavourWord.Favoured;
            if (favour >= PleasedFrom) return FavourWord.Pleased;
            if (favour >= NoticedFrom) return FavourWord.Noticed;
            if (favour >= UnknownFrom) return FavourWord.Unknown;
            if (favour >= DispleasedFrom) return FavourWord.Displeased;
            return FavourWord.Wrathful;
        }

        /// <summary>A standing kept within -100 and 100.</summary>
        public static float Clamp(float favour) => float.IsNaN(favour) ? 0f : Math.Max(-Most, Math.Min(Most, favour));

        /// <summary>The least a standing drifts back in a day, so a small one comes to rest at unknown rather than nearly.</summary>
        public const float LeastDriftPerDay = 0.2f;

        /// <summary>
        /// A standing after <paramref name="days"/> left alone: halved toward unknown every <see cref="HalfLifeDays"/> by the
        /// god's nature, and at least <see cref="LeastDriftPerDay"/> a day, never past unknown.
        /// </summary>
        public static float Drift(float favour, float days, GodNature n)
        {
            if (float.IsNaN(favour) || days <= 0f || favour == 0f) return Clamp(favour);
            bool good = favour > 0f;
            float half = HalfLifeDays(n, good);
            float a = Math.Abs(favour);
            float left = a * (float)Math.Pow(0.5, days / half);
            left = Math.Min(left, a - LeastDriftPerDay * days);
            if (left <= 0f) return 0f;
            return good ? left : -left;
        }

        /// <summary>The god the colony stands best with is its patron, from noticed up; none below.</summary>
        public static bool PatronWorthy(float favour) => !float.IsNaN(favour) && favour >= NoticedFrom;

        // ================= The first favour sources (Gods/GodDeeds.cs) =================

        /// <summary>A day's harvest is a big one: the colony's people brought in this many plants and the raw food in store rose this much (felling alone doesn't count).</summary>
        public const int BigHarvestPlants = 40, BigHarvestRawRise = 40;

        /// <summary>How much each deed moves the gods of its domain (before their natures): a big harvest, a great beast brought down, a threat beaten off, a funeral with words said at a grave (or without one).</summary>
        public const float HarvestDeed = 3f, GreatBeastDeed = 6f, ThreatBeatenDeed = 3f, FuneralDeed = 4f, FuneralUngravedDeed = 2f;

        /// <summary>A sacred animal of a domain slaughtered by the colony, or hunted wild (the hunt's own gods don't mind a hunt).</summary>
        public const float SacredSlaughtered = -4f, SacredHunted = -2f;

        /// <summary>The dead left lying: this many of the human dead a day or more on a home's ground, out of any grave, displease the gods of the dead this much a day.</summary>
        public const int DeadLyingCount = 4;
        public const float DeadLyingDeed = -2f;

        /// <summary>A day's harvest that counts as a deed for the harvest's gods.</summary>
        public static bool BigHarvest(int plants, int rawRise) => plants >= BigHarvestPlants && rawRise >= BigHarvestRawRise;

        /// <summary>Enough of the dead left lying to displease the gods of the dead.</summary>
        public static bool DeadLeftLying(int corpses) => corpses >= DeadLyingCount;

        /// <summary>A sacred animal's death as the gods of its domain take it: slaughtered or hunted by the colony; a hunt is the hunt's own gods' way.</summary>
        public static float SacredDeath(GodDomain d, bool slaughtered, bool wildHunted)
        {
            if (slaughtered) return SacredSlaughtered;
            if (wildHunted && d != GodDomain.Hunt) return SacredHunted;
            return 0f;
        }

        // ================= Newborn gods =================

        /// <summary>Deeds of a domain within a year the colony's faith has no god for, before one is born of it.</summary>
        public const int NewbornDeeds = 8;

        /// <summary>A newborn grows into an ordinary god once its devotion reaches this.</summary>
        public const float NewbornGrown = 100f;

        /// <summary>At most this many newborns ever, and one every this many days.</summary>
        public const int MaxNewborns = 3, NewbornGapDays = 120;

        /// <summary>
        /// A god is born of the colony's own faith: its deeds keep serving a domain (<see cref="NewbornDeeds"/> in a year)
        /// that none of its faith's gods hold, it has a faith to be born into, and the last newborn is long enough ago.
        /// </summary>
        public static bool NewbornDue(int deedsThisYear, bool faithHasDomain, bool hasFaith, int newborns, int daysSinceLast) =>
            hasFaith && !faithHasDomain && deedsThisYear >= NewbornDeeds && newborns < MaxNewborns && daysSinceLast >= NewbornGapDays;

        /// <summary>What a newborn's devotion grows by from a gain of favour (a third of it; losses take nothing away).</summary>
        public static float Devotion(float gain) => float.IsNaN(gain) || gain <= 0f ? 0f : gain / 3f;

        // ================= Fading and dead =================

        /// <summary>A god with no worshippers fades; after this many days faded it is a dead god.</summary>
        public const int FadingToDeadDays = 120;

        /// <summary>
        /// The kind a god has now by whether anyone holds it and how long since anyone did: one nobody holds fades, and in time
        /// falls silent (a dead god); one taken up again comes back to its own kind (an ordinary god for one that was always
        /// faded or dead: the old ones of the old places). A god whose own kind is fading stays fading while nobody holds it,
        /// and the lore's own (the Hunger, the Sleeper) keep theirs.
        /// </summary>
        public static GodKind KindNow(GodKind kind, GodKind own, bool worshipped, int daysSinceWorship)
        {
            if (kind == GodKind.Fading || kind == GodKind.Dead)
            {
                if (worshipped) return own == GodKind.Fading || own == GodKind.Dead ? GodKind.Ordinary : own;
                return kind == GodKind.Fading && own != GodKind.Fading && daysSinceWorship >= FadingToDeadDays ? GodKind.Dead : kind;
            }
            if (worshipped) return kind;
            // The lore's own (the Hunger, the Sleeper) keep theirs even with their people gone.
            if (kind == GodKind.Hunger || kind == GodKind.Imprisoned) return kind;
            return GodKind.Fading;
        }

        // ================= Words =================

        /// <summary>What a god of this form and gender is called ("god", "goddess", "spirit", "saint", "archotech"): the key's ending, CC_God_Noun_*.</summary>
        public static string Noun(GodForm f, int gender)
        {
            switch (f)
            {
                case GodForm.Spirit: return "Spirit";
                case GodForm.Saint: return "Saint";
                case GodForm.Archotech: return "Archotech";
                default: return gender == 2 ? "Goddess" : "God";
            }
        }

        /// <summary>The domain's key ending (CC_God_Domain_Harvest, CC_God_DomainOf_Harvest).</summary>
        public static string Key(GodDomain d) => d.ToString();

        // ================= Chance, the same each time =================

        /// <summary>A number in [0, 1) for a seed and a salt, the same every time (a world rebuilt reads the same).</summary>
        public static float Roll(int seed, int salt)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)salt * 40503u + 0x9E3779B9u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>A seed made of several numbers.</summary>
        public static int Seed(params int[] parts)
        {
            unchecked
            {
                int h = 17;
                foreach (int p in parts) h = h * 31 + p;
                return h;
            }
        }

        /// <summary>A seed for a string (a name, a defName), the same in every run.</summary>
        public static int Seed(string s)
        {
            unchecked
            {
                int h = 23;
                if (s != null)
                    foreach (char c in s)
                        h = h * 31 + c;
                return h;
            }
        }

        /// <summary>One of a list by weight, at <paramref name="roll"/> in [0, 1).</summary>
        public static T Weighted<T>(IList<T> items, Func<T, float> weight, float roll)
        {
            if (items == null || items.Count == 0) return default;
            float total = 0f;
            foreach (var i in items) total += Math.Max(0f, weight(i));
            if (total <= 0f) return items[0];
            float r = roll * total;
            foreach (var i in items)
            {
                float w = Math.Max(0f, weight(i));
                if (r < w) return i;
                r -= w;
            }
            return items[items.Count - 1];
        }

        /// <summary>A name from a list that isn't taken yet (by the seed), or the list's first with a number when all are.</summary>
        public static string PickName(IList<string> names, ICollection<string> taken, int seed)
        {
            if (names == null || names.Count == 0) return null;
            int start = (int)(Roll(seed, 41) * names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                string n = names[(start + i) % names.Count];
                if (!string.IsNullOrEmpty(n) && (taken == null || !taken.Contains(n))) return n;
            }
            return null;
        }
    }
}
