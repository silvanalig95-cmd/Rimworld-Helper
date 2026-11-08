using System;
using System.Collections.Generic;
using System.Text;

namespace ColonyCouncil
{
    /// <summary>
    /// Where a kind stands in the food chain (Animals/HuntMath.cs): what eats what. A hunter takes only wild animals of a
    /// lower step. The order is the comparison: add nothing between.
    /// </summary>
    public enum FoodChain
    {
        /// <summary>It doesn't hunt: grazers, browsers, fruit and seed eaters, poultry, rooters, lapdogs.</summary>
        Prey,
        /// <summary>Mice, rats, hares and small birds: cats, foxes, weasels, small owls and hawks, small snakes, terriers.</summary>
        SmallHunter,
        /// <summary>Deer-sized and smaller: dogs, wolves, hyenas, eagles, monitors, raptors.</summary>
        Hunter,
        /// <summary>Nothing hunts it: bears, big cats, crocodiles, the great dinosaurs and dragons.</summary>
        Apex
    }

    /// <summary>What is known of a kind for the food chain, read from its def (Animals/PetHunts.cs) or a test's table.</summary>
    public struct HuntFacts
    {
        public string defName, label;
        /// <summary>The game's own predator flag.</summary>
        public bool predator;
        /// <summary>The game's maxPreyBodySize, or 0 where it has none (the game's 99999).</summary>
        public float maxPrey;
        /// <summary>Its diet takes raw meat; plants (grass, trees, fruit, seeds); nothing at all.</summary>
        public bool meat, plants, nothing;
        /// <summary>A bite, claws, a beak or a sting among its attacks; a venomous one.</summary>
        public bool teeth, venom;
        public bool insect, dryad;
        /// <summary>Its kind's grown body size.</summary>
        public float bodySize;
        public bool pokemon, legendary;
        /// <summary>A Pokémon's types by name (Fire, Dark...); null otherwise.</summary>
        public IList<string> types;
    }

    /// <summary>The judgement on a kind: its step in the food chain, whether it hunts in packs, and why.</summary>
    public struct HuntJudgement
    {
        public FoodChain level;
        /// <summary>Two or more of the colony's own of it hunt together (dogs, wolves, hyenas, lions, raptors).</summary>
        public bool pack;
        /// <summary>It takes insects (an anteater, a Pokémon that eats Durant); else insects are never its prey.</summary>
        public bool insects;
        /// <summary>It takes venomous prey (a mongoose); else a venomous animal is never its prey.</summary>
        public bool venom;
        /// <summary>Why, in plain words, for the dev action and the docs.</summary>
        public string reason;
        /// <summary>What decided it: "kind" (its defName in the table), "family" (a word of its name), "pokemon", "type", "rule".</summary>
        public string by;

        public bool Hunts => level != FoodChain.Prey;

        public override string ToString() => level + (pack ? ", in packs" : "") + " (" + by + ": " + reason + ")";
    }

    /// <summary>A wild animal looked at as prey (Animals/PetHunts.cs).</summary>
    public struct PreyFacts
    {
        public bool animal, humanlike, wild, penned, storied, sacred, legendary, lord, markedForTaming, mental, oddDeath, insect, venom, downed;
        /// <summary>The player's order to take its body when it dies is on it (Animals/BodyWhenItDies.cs): it is left to bleed out.</summary>
        public bool markedForBody;
        public FoodChain level;
        /// <summary>Its body size now (a calf is small).</summary>
        public float size;
        /// <summary>The game's measure of how much of a fight it is: its kind's combat power times its health times its size.</summary>
        public float might;
        /// <summary>The game's chance it turns on the one hurting it (its revenge chance against this hunter, in melee).</summary>
        public float revenge;
    }

    /// <summary>The hunter, for its choice of prey.</summary>
    public struct HunterFacts
    {
        public HuntJudgement kind;
        public float size;
        /// <summary>The game's maxPreyBodySize for its kind, or 0.</summary>
        public float gameMax;
        public float might;
        /// <summary>Two or more of the colony's own of its pack kind are hunting together.</summary>
        public bool pack;
        /// <summary>Fed, out for small game.</summary>
        public bool sport;
    }

    /// <summary>One of the colony's animals right now, for whether it may hunt (Animals/PetHunts.cs).</summary>
    public struct HuntState
    {
        public bool settingOn, keptHome, hunts, adult, spawned, downed, drafted, followsMaster, roped, penned, inLord, mental, medicalRest, danger, meleeAttack, canMove;
        public float health;
    }

    /// <summary>Why a hungry animal has nothing it may eat in reach, for the alert's words.</summary>
    public enum NoFoodWhy
    {
        /// <summary>There is food it eats on the map, outside its allowed area.</summary>
        Area,
        /// <summary>The food it eats inside its area is all forbidden.</summary>
        Forbidden,
        /// <summary>There is no food it eats on the map at all.</summary>
        None
    }

    /// <summary>
    /// The colony's animals hunting for themselves (the player's request, 7 November 2026: "can you make animals hunt if they
    /// would do in reality, and your judgement for fictive species, and that they hunt what they would normally would, or hunt
    /// things below them in the food chain, not above"), with no game types so the tests can run it: who hunts and where each
    /// kind stands in the food chain (a table with a reason per kind or family, and a rule for every kind it doesn't name),
    /// which wild animal a hunter may take, when it may hunt, how often a fed one goes after small game, and the alert's why.
    /// Animals/PetHunts.cs does it on the map with the game's own hunt.
    /// </summary>
    public static class HuntMath
    {
        // ---------------- The steps by size ----------------

        /// <summary>A hunter the table leaves to its size: under this a small hunter, under <see cref="HunterBelow"/> a hunter, else apex.</summary>
        public const float SmallHunterBelow = 0.7f;
        public const float HunterBelow = 1.6f;

        public static FoodChain BySize(float bodySize) =>
            bodySize < SmallHunterBelow ? FoodChain.SmallHunter : bodySize < HunterBelow ? FoodChain.Hunter : FoodChain.Apex;

        // ---------------- The table ----------------

        private sealed class Kind
        {
            /// <summary>Its step; null: a hunter by its size (<see cref="BySize"/>).</summary>
            public FoodChain? level;
            public bool pack, insects, venom;
            /// <summary>A prey word that stands even over the game's predator flag (summoned and made things).</summary>
            public bool firm;
            public string reason;
        }

        private static Kind K(FoodChain? level, string reason, bool pack = false, bool insects = false, bool venom = false, bool firm = false) =>
            new Kind { level = level, reason = reason, pack = pack, insects = insects, venom = venom, firm = firm };

        private const FoodChain P = FoodChain.Prey, Small = FoodChain.SmallHunter, Mid = FoodChain.Hunter, Top = FoodChain.Apex;
        private static readonly FoodChain? Sized = null;

        private static void Add(Dictionary<string, Kind> d, Kind k, params string[] keys)
        {
            foreach (var key in keys) d[key] = k;
        }

        /// <summary>Kinds by defName, where a name says nothing or misleads (a giant penguin, a walrus kin, a dragonfly).</summary>
        private static readonly Dictionary<string, Kind> byDefName = BuildDefNames();

        private static Dictionary<string, Kind> BuildDefNames()
        {
            var d = new Dictionary<string, Kind>();
            // Megafauna: the game marks these predators, but what they ate was in the sea.
            Add(d, K(P, "a giant penguin: it fishes, it doesn't hunt on land"), "Palaeeudyptes");
            Add(d, K(P, "a walrus kin: it dug for shellfish"), "Gomphotaria");
            Add(d, K(Small, "a giant dragonfly: it takes small things on the wing"), "Meganeura");
            // Alpha Animals: its game flag says predator, its description a snail that only defends itself.
            Add(d, K(P, "a snail: it grazes and only shocks what attacks it"), "AA_Plasmorph");
            Add(d, K(P, "a scavenger: it digs the frozen dead out of the snow"), "AA_Frostmite");
            return d;
        }

        /// <summary>
        /// Families by a word of the label or the defName (the last word first: a red fox is a fox, a sabertooth squirrel a
        /// squirrel). Real animals by what they really do. A word that says prey doesn't overrule the game's own predator flag
        /// (a mod's vampire deer is a hunter); a word that says hunter does (the game's dogs aren't flagged).
        /// </summary>
        private static readonly Dictionary<string, Kind> byWord = BuildWords();

        private static Dictionary<string, Kind> BuildWords()
        {
            var d = new Dictionary<string, Kind>();
            // Dogs and their wild kin hunt, in packs when there are two or more; the lapdogs barely do.
            Add(d, K(Sized, "a dog: it chases down and kills small game, more in a pack", pack: true),
                "dog", "dogs", "hound", "husky", "malamute", "samoyed", "retriever", "labrador", "shepherd", "collie", "rottweiler", "dane", "mastiff",
                "doberman", "pinscher", "beagle", "dachshund", "spaniel", "setter", "greyhound", "whippet", "wolfhound", "akita", "shiba", "terrier",
                "corgi", "poodle", "basenji", "bloodhound", "dingo", "dhole");
            Add(d, K(P, "a lapdog: bred for company, it barely hunts"), "pug", "bulldog", "tzu", "chihuahua", "pekingese", "pomeranian", "maltese", "bichon");
            Add(d, K(Sized, "a wolf: wolves hunt deer and smaller, in packs", pack: true), "wolf", "wolves", "warg");
            Add(d, K(Sized, "a wild dog of the plains: it hunts in a pack", pack: true), "hyena", "coyote", "jackal");
            Add(d, K(Sized, "a fox: it lives on mice, hares and birds"), "fox", "foxes", "vulpine");
            Add(d, K(Sized, "a marsupial wolf: it hunted wallabies and birds"), "thylacine");
            // Cats: a house cat keeps to mice and birds whatever its size; the wild cats by their size; the big cats on top.
            Add(d, K(Small, "a cat: it keeps the mice and birds down"), "cat", "cats", "kitten", "housecat");
            Add(d, K(Sized, "a wild cat: it hunts hares, birds and young deer"), "wildcat", "lynx", "bobcat", "caracal", "serval", "ocelot", "margay");
            Add(d, K(Top, "a big cat: nothing hunts it, and it takes deer and antelope"), "cougar", "puma", "panther", "jaguar", "leopard", "cheetah", "tiger",
                "smilodon", "sabertooth", "sabretooth");
            Add(d, K(Top, "a lion: lions hunt together", pack: true), "lion", "lions", "lioness");
            // The weasel family, the mongoose (which takes snakes), the badger and the wolverine.
            Add(d, K(Sized, "a weasel: it hunts mice, rabbits and birds"), "weasel", "stoat", "ermine", "mink", "ferret", "marten", "polecat");
            Add(d, K(Mid, "a mongoose: it hunts small animals, snakes too", venom: true), "mongoose", "meerkat");
            Add(d, K(Sized, "a badger or a wolverine: it digs out and takes small game"), "badger", "wolverine");
            // Bears take what they can; the panda keeps to bamboo.
            Add(d, K(Top, "a bear: it takes deer and smaller when it can"), "bear", "bears", "grizzly", "kodiak");
            Add(d, K(P, "a panda: it eats bamboo"), "panda");
            // Birds of prey by their size; the carrion birds eat the dead and don't kill.
            Add(d, K(Sized, "a bird of prey: it takes small animals and birds"), "eagle", "hawk", "falcon", "kestrel", "harrier", "owl", "osprey", "buzzard",
                "goshawk", "sparrowhawk");
            Add(d, K(P, "a carrion bird: it eats the dead, it doesn't kill"), "vulture", "condor");
            // Snakes swallow what they kill; crocodiles take what comes to the water; monitors hunt.
            Add(d, K(Sized, "a snake: it kills small animals and swallows them"), "snake", "serpent", "cobra", "viper", "adder", "mamba", "rattlesnake", "python",
                "boa", "anaconda", "constrictor", "titanoboa", "gigantophis");
            Add(d, K(Top, "a crocodile: it takes what comes to the water's edge"), "crocodile", "alligator", "caiman", "gharial", "croc");
            Add(d, K(Sized, "a monitor lizard: it hunts what it can overpower"), "monitor", "komodo", "megalania");
            // The hunting dinosaurs; the raptors in packs.
            Add(d, K(Sized, "a raptor: it hunted in packs", pack: true), "raptor", "velociraptor", "utahraptor", "dakotaraptor", "deinonychus");
            Add(d, K(Sized, "a hunting dinosaur"), "rex", "tyrannosaurus", "allosaurus", "carnotaurus", "spinosaurus", "baryonyx", "giganotosaurus", "yutyrannus",
                "dilophosaurus", "compsognathus");
            // The ones that don't hunt, by family (most need no word: the rule finds they eat no meat).
            Add(d, K(P, "a fish eater: it hunts in the water, not on land"), "penguin", "seal", "walrus", "otter", "pelican", "heron", "stork", "gull");
            Add(d, K(P, "a lizard or a tortoise: leaves, fruit and insects"), "lizard", "iguana", "gecko", "skink", "chameleon", "anole", "tortoise", "turtle");
            Add(d, K(P, "an ape or a monkey: fruit and leaves, insects now and then"), "monkey", "ape", "baboon", "chimpanzee", "bonobo", "mandrill", "gorilla",
                "orangutan", "lemur", "macaque");
            Add(d, K(P, "a rooter: it eats what it turns up"), "pig", "boar", "hog", "warthog", "raccoon", "opossum", "skunk", "hedgehog", "platypus", "rat",
                "mouse", "rodent");
            Add(d, K(P, "an insect eater: ants and grubs", insects: true), "anteater", "aardvark", "pangolin", "armadillo");
            Add(d, K(P, "a grazer or a browser"), "deer", "elk", "moose", "caribou", "reindeer", "gazelle", "antelope", "ibex", "goat", "sheep", "cow", "cattle",
                "muffalo", "bison", "buffalo", "yak", "horse", "donkey", "zebra", "quagga", "camel", "dromedary", "alpaca", "llama", "elephant", "mammoth",
                "rhinoceros", "rhino", "hippopotamus", "giraffe", "tapir", "capybara", "beaver", "kangaroo", "koala", "wildebeest", "thrumbo");
            Add(d, K(P, "a fowl: seeds and greens"), "chicken", "duck", "goose", "turkey", "ostrich", "emu", "cassowary", "pheasant", "dodo", "kiwi", "moa",
                "peacock", "quail", "crane");
            Add(d, K(P, "a small grazer: grass, seeds and roots"), "hare", "rabbit", "squirrel", "chinchilla", "guinea", "hamster", "marmot", "gopher");
            // Summoned and made things (A RimWorld of Magic's minions, demons, elementals and sentinels): whatever the game's flag, they don't feed themselves.
            Add(d, K(P, "summoned or made: it doesn't feed itself", firm: true), "minion", "minions", "demon", "elemental", "sentinel", "golem", "summoned");
            return d;
        }

        /// <summary>
        /// PokéWorld's Pokémon by species (the label, letters only, lower case), by what the species is: its canines,
        /// foxes, cats, weasels, bears, birds of prey, snakes, crocodiles, hunting dragons and fossils, and hunting bugs. The
        /// Dark and Dragon types that don't hunt are named too, as Prey, so the type rule below never makes a hunter of them.
        /// </summary>
        private static readonly Dictionary<string, Kind> pokemon = BuildPokemon();

        private static Dictionary<string, Kind> BuildPokemon()
        {
            var d = new Dictionary<string, Kind>();
            Add(d, K(Sized, "a canine Pokémon: it hunts, in packs", pack: true),
                "growlithe", "arcanine", "houndour", "houndoom", "poochyena", "mightyena", "electrike", "manectric", "lillipup", "herdier", "stoutland",
                "rockruff", "lycanroc", "yamper", "boltund", "furfrou", "riolu", "lucario");
            Add(d, K(Sized, "a fox Pokémon: it hunts small game"), "vulpix", "ninetales", "zorua", "zoroark", "nickit", "thievul", "fennekin");
            Add(d, K(Small, "a cat Pokémon: it keeps to small game"), "meowth", "persian", "perrserker", "glameow", "purugly", "skitty", "delcatty", "espurr",
                "meowstic", "purrloin", "liepard", "litten", "torracat");
            Add(d, K(Sized, "a lion Pokémon: it hunts with its pride", pack: true), "shinx", "luxio", "luxray", "litleo", "pyroar");
            Add(d, K(Sized, "a weasel Pokémon: it hunts small game", pack: true), "sneasel", "weavile");
            Add(d, K(Sized, "a mongoose Pokémon: it hunts snakes and Rattata", venom: true), "zangoose", "yungoos", "gumshoos");
            Add(d, K(Sized, "a bear Pokémon: it takes what it can"), "ursaring", "beartic");
            Add(d, K(Sized, "a bird of prey Pokémon"), "pidgeotto", "pidgeot", "spearow", "fearow", "hoothoot", "noctowl", "honchkrow", "starly", "staravia",
                "staraptor", "rufflet", "braviary", "fletchling", "fletchinder", "talonflame", "rowlet", "dartrix", "decidueye", "rookidee", "corvisquire",
                "corviknight", "noivern");
            Add(d, K(Sized, "a snake Pokémon: it swallows small game", venom: true), "ekans", "arbok", "seviper");
            Add(d, K(Sized, "a crocodile Pokémon"), "sandile", "krokorok", "krookodile", "totodile", "croconaw", "feraligatr");
            Add(d, K(Sized, "a hunting dragon Pokémon"), "gible", "gabite", "garchomp", "bagon", "shelgon", "salamence", "deino", "zweilous", "hydreigon",
                "axew", "fraxure", "haxorus", "druddigon", "trapinch", "vibrava", "flygon");
            Add(d, K(Sized, "a fossil hunter Pokémon"), "tyrunt", "tyrantrum", "aerodactyl", "archen", "archeops", "kabutops", "tyranitar");
            Add(d, K(Sized, "a hunting bug Pokémon: it catches what wanders close"), "scyther", "scizor", "spinarak", "ariados", "galvantula", "skorupi", "drapion",
                "sizzlipede", "centiskorch", "venipede", "whirlipede", "scolipede");
            Add(d, K(Sized, "a Pokémon in a blade-armed pack: they hunt together", pack: true), "pawniard", "bisharp");
            Add(d, K(Small, "an anteater Pokémon: it eats Durant", insects: true), "heatmor");
            // Dark and Dragon types that don't hunt.
            Add(d, K(P, "a gentle dragon Pokémon"), "dratini", "dragonair", "dragonite", "altaria", "swablu", "goomy", "sliggoo", "goodra", "applin", "flapple",
                "appletun", "drampa", "turtonator", "duraludon", "dreepy", "drakloak", "dragapult");
            Add(d, K(P, "a fighter Pokémon that tests itself, not a hunter"), "jangmoo", "hakamoo", "kommoo", "scraggy", "scrafty", "pangoro", "incineroar",
                "obstagoon");
            Add(d, K(P, "a water Pokémon: it hunts at sea, not on land"), "carvanha", "sharpedo", "crawdaunt", "kingdra", "dragalge", "dracovish", "greninja");
            Add(d, K(P, "a trickster Pokémon: it feeds on mischief, not on game"), "sableye", "spiritomb", "impidimp", "morgrem", "grimmsnarl", "inkay", "malamar",
                "nuzleaf", "shiftry", "cacturne", "murkrow", "morpeko", "absol", "umbreon", "dracozolt");
            Add(d, K(P, "a skunk Pokémon: it digs for grubs"), "stunky", "skuntank");
            Add(d, K(P, "a carrion bird Pokémon: it eats the dead"), "vullaby", "mandibuzz");
            return d;
        }

        /// <summary>A Pokémon's key: its label's letters, lower case ("Jangmo-o" is jangmoo, "Mr. Mime" mrmime).</summary>
        public static string PokemonKey(string label)
        {
            if (string.IsNullOrEmpty(label)) return "";
            var sb = new StringBuilder();
            foreach (char c in label)
                if (c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z') sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        /// <summary>The table names this Pokémon (for the tests: every name in it is a species that exists).</summary>
        public static IEnumerable<string> PokemonNamed => pokemon.Keys;

        public static IEnumerable<string> WordsNamed => byWord.Keys;

        public static IEnumerable<string> DefNamesNamed => byDefName.Keys;

        // ---------------- The judgement ----------------

        /// <summary>
        /// Where a kind stands, and why: a dryad, a kind that eats nothing and a kind whose diet has no meat (it couldn't eat
        /// its kill) never hunt; a Pokémon by its species in the table, else by its type (a Dark type of 0.4 or more, or a
        /// Dragon type of 1 or more, hunts by its size; any other doesn't), a legendary never; anything else by its defName in
        /// the table, else by a word of its name, else by the rule (<see cref="Rule"/>): the game's predator flag, or a
        /// meat-only diet with teeth or claws, makes it a hunter by its size (under 0.7 small, under 1.6 a hunter, else apex);
        /// an omnivore the game doesn't flag doesn't hunt.
        /// </summary>
        public static HuntJudgement Judge(HuntFacts f)
        {
            if (f.dryad) return Made(P, "a dryad: its tree feeds it", "rule");
            if (f.nothing) return Made(P, "it eats nothing it could hunt", "rule");
            if (!f.meat) return Made(P, "it eats no meat", "rule");
            if (f.pokemon)
            {
                if (f.legendary) return Made(P, "a legendary Pokémon keeps to its legend", "pokemon");
                if (pokemon.TryGetValue(PokemonKey(f.label), out var pk)) return From(pk, f, "pokemon");
                if (HasType(f.types, "Dark") && f.bodySize >= 0.4f) return Made(BySize(f.bodySize), "a Dark type of some size: they take their food by night", "type");
                if (HasType(f.types, "Dragon") && f.bodySize >= 1f) return Made(BySize(f.bodySize), "a big Dragon type", "type");
                return Made(P, "a Pokémon of no hunting kind: berries and leaves", "type");
            }
            if (!string.IsNullOrEmpty(f.defName) && byDefName.TryGetValue(f.defName, out var byName)) return From(byName, f, "kind");
            var family = Family(f);
            // A prey word on a kind the game counts a predator: a mod's twist on a real animal (a vampire deer); the game's flag stands.
            if (family != null && (family.firm || !(family.level == P && f.predator))) return From(family, f, "family");
            return Rule(f);
        }

        /// <summary>
        /// The rule for a kind the table doesn't name: no meat in its diet, it doesn't hunt; the game's predator flag, or a
        /// meat-only diet with teeth or claws, makes it a hunter by its size; an omnivore the game doesn't flag doesn't hunt.
        /// </summary>
        public static HuntJudgement Rule(HuntFacts f)
        {
            if (f.dryad) return Made(P, "a dryad: its tree feeds it", "rule");
            if (f.nothing) return Made(P, "it eats nothing it could hunt", "rule");
            if (!f.meat) return Made(P, "it eats no meat", "rule");
            if (f.predator) return Made(BySize(f.bodySize), "the game counts it a predator", "rule");
            if (!f.plants && f.teeth) return Made(BySize(f.bodySize), "a meat eater with teeth or claws", "rule");
            return Made(P, "an omnivore the game doesn't count a predator: it eats what it finds", "rule");
        }

        /// <summary>The table's own entry for a kind that isn't a Pokémon (by defName, else by a word of its name), or null when it names none.</summary>
        public static HuntJudgement? Listed(HuntFacts f)
        {
            if (!string.IsNullOrEmpty(f.defName) && byDefName.TryGetValue(f.defName, out var k)) return From(k, f, "kind");
            var family = Family(f);
            return family == null ? (HuntJudgement?)null : From(family, f, "family");
        }

        /// <summary>The family a word of its label names, else one of its defName, the last word first; null for none.</summary>
        private static Kind Family(HuntFacts f)
        {
            foreach (var source in new[] { f.label, f.defName })
            {
                var words = AnimalNatureMath.Words(source);
                for (int i = words.Count - 1; i >= 0; i--)
                    if (byWord.TryGetValue(words[i], out var k))
                        return k;
            }
            return null;
        }

        private static bool HasType(IList<string> types, string t)
        {
            if (types == null) return false;
            foreach (var x in types)
                if (string.Equals(x, t, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static HuntJudgement Made(FoodChain level, string reason, string by) => new HuntJudgement { level = level, reason = reason, by = by };

        private static HuntJudgement From(Kind k, HuntFacts f, string by) => new HuntJudgement
        {
            level = k.level ?? BySize(f.bodySize), pack = k.pack, insects = k.insects, venom = k.venom, reason = k.reason, by = by
        };

        // ---------------- What it may take ----------------

        /// <summary>Alone it takes prey up to this share of its own size; two or more of a pack kind together, up to <see cref="PackShare"/>.</summary>
        public const float LoneShare = 0.8f, PackShare = 1.2f;
        /// <summary>Whatever their size, a small hunter takes small game only, and a hunter nothing much bigger than a deer (a pack a little more).</summary>
        public const float SmallHunterMax = 0.4f, HunterMax = 1.25f, HunterPackMax = 1.6f;
        /// <summary>A fed animal out now and then goes after small game only.</summary>
        public const float SportMax = 0.4f;
        /// <summary>Its prey is less of a fight than this share of its own (the game's combat power times health times size); a pack, up to its own.</summary>
        public const float LoneMight = 0.6f, PackMight = 1f;
        /// <summary>
        /// The game's chance the prey turns on it (its revenge chance, three times in melee): at most this for prey of a third of
        /// its size or more, and <see cref="SmallRevenge"/> for smaller prey that can't do it much harm.
        /// </summary>
        public const float BigRevenge = 0.1f, SmallRevenge = 0.35f;
        public const float SmallPreyShare = 1f / 3f;

        /// <summary>The biggest prey it may take: a share of its size, the game's own limit for its kind where it has one, and its step's cap.</summary>
        public static float PreyLimit(FoodChain level, float size, float gameMax, bool pack, bool sport)
        {
            if (level == FoodChain.Prey) return 0f;
            float limit = size * (pack ? PackShare : LoneShare);
            if (gameMax > 0f) limit = Math.Min(limit, gameMax);
            if (level == FoodChain.SmallHunter) limit = Math.Min(limit, SmallHunterMax);
            else if (level == FoodChain.Hunter) limit = Math.Min(limit, pack ? HunterPackMax : HunterMax);
            if (sport) limit = Math.Min(limit, SportMax);
            return limit;
        }

        /// <summary>
        /// Why it may not take this animal, or null when it may: never a person, anyone's animal, one penned, one in Rimfolk's
        /// stories, a sacred or legendary one, one with a group (a herd on the move), one marked for taming, a manhunter or one
        /// in a mental state, one that bursts or splits when it dies; never an insect or a venomous animal unless its kind eats
        /// them; only lower in the food chain, no bigger than its limit; and, unless the prey is down already, less of a fight
        /// than its share and unlikely to turn on it.
        /// </summary>
        public static string WhyNotPrey(HunterFacts h, PreyFacts p)
        {
            if (!p.animal || p.humanlike) return "not an animal";
            if (!p.wild) return "someone's animal";
            if (p.penned) return "penned";
            if (p.storied) return "one of the colony's stories";
            if (p.sacred) return "held sacred";
            if (p.legendary) return "a legend";
            if (p.lord) return "in a group (a herd on the move)";
            if (p.markedForTaming) return "marked for taming";
            if (p.markedForBody) return "left to die for its body";
            if (p.mental) return "a manhunter or in a mental state";
            if (p.oddDeath) return "it bursts, splits or vanishes when it dies";
            if (p.insect && !h.kind.insects) return "an insect";
            if (p.venom && !h.kind.venom) return "venomous";
            if (p.level >= h.kind.level) return "not below it in the food chain";
            if (p.size > PreyLimit(h.kind.level, h.size, h.gameMax, h.pack, h.sport)) return "too big";
            if (!p.downed)
            {
                if (p.might >= h.might * (h.pack ? PackMight : LoneMight)) return "too much of a fight";
                float allowed = p.size >= h.size * SmallPreyShare ? BigRevenge : SmallRevenge;
                if (p.revenge > allowed) return "it would turn on it";
            }
            return null;
        }

        // ---------------- When ----------------

        /// <summary>Below this share of its health it doesn't go hunting (the game's own wild hunters stop at a quarter).</summary>
        public const float MinHealth = 0.6f;

        /// <summary>
        /// Why one of the colony's animals may not hunt now, or null: the setting off, kept home, a kind that doesn't hunt, young,
        /// not on the map (a caravan), downed, drafted, following its master into work or a fight, roped or penned, with a group
        /// (a caravan forming, a ritual), in a mental state, ill enough for bed, hurt, no attack, unable to move, or danger on the map.
        /// </summary>
        public static string WhyNot(HuntState s)
        {
            if (!s.settingOn) return "setting";
            if (s.keptHome) return "kept";
            if (!s.hunts) return "kind";
            if (!s.adult) return "young";
            if (!s.spawned) return "away";
            if (s.downed) return "downed";
            if (s.drafted) return "drafted";
            if (s.followsMaster) return "master";
            if (s.roped || s.penned) return "penned";
            if (s.inLord) return "group";
            if (s.mental) return "mental";
            if (s.medicalRest) return "sick";
            if (s.health < MinHealth) return "hurt";
            if (!s.meleeAttack || !s.canMove) return "unable";
            if (s.danger) return "danger";
            return null;
        }

        /// <summary>A fed hunter goes after small game about once in this many days.</summary>
        public const float SportDays = 4f;
        /// <summary>And not again within this many days.</summary>
        public const float SportGapDays = 1.5f;
        /// <summary>At most this many fed hunters set out in an hour on a map.</summary>
        public const int SportAnHour = 1;
        /// <summary>A fed one's kill is carried to its person this often (when they're about), else eaten where it fell.</summary>
        public const float CarryShare = 1f / 3f;

        /// <summary>The chance each hour a fed hunter sets out (about once in <see cref="SportDays"/>).</summary>
        public static float SportHourChance(float days = SportDays) => days <= 0f ? 0f : Math.Min(1f, 1f / (days * 24f));

        /// <summary>The last one was long enough ago (or never).</summary>
        public static bool SportReady(int lastTick, int now, int ticksPerDay) => lastTick < 0 || now - lastTick >= SportGapDays * ticksPerDay;

        /// <summary>A hunt is given up when its hunter has strayed this many cells or more outside its allowed area after the prey.</summary>
        public const int StrayCells = 6;

        // ---------------- The alert ----------------

        /// <summary>Why a hungry animal has nothing in reach: food outside its area first, then food inside it that is forbidden, else none.</summary>
        public static NoFoodWhy Why(bool foodOutsideArea, bool forbiddenInside) =>
            foodOutsideArea ? NoFoodWhy.Area : forbiddenInside ? NoFoodWhy.Forbidden : NoFoodWhy.None;

        /// <summary>
        /// Why it can't hunt its way out, for the alert's words: "setting" (pets don't hunt), "kind" (its kind doesn't),
        /// "kept", "young", "prey" (nothing it may take in reach), "unable" (the rest: hurt, ill, following its master...);
        /// null when it can and will.
        /// </summary>
        public static string HuntWord(string whyNot, bool preyInReach)
        {
            if (whyNot == null) return preyInReach ? null : "prey";
            if (whyNot == "setting" || whyNot == "kind" || whyNot == "kept" || whyNot == "young") return whyNot;
            return "unable";
        }

        /// <summary>A line about it from its person once when it starts, not again within this many hours of the last.</summary>
        public const int SayAgainHours = 12;
    }
}
