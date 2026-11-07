using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// The colony's animals hunting for themselves (the player's request, 7 November 2026; Animals/HuntMath.cs, PetHunts.cs):
/// who hunts (the table and the rule, over every animal of the base game, the DLCs and the player's animal mods in
/// Tools/Tests/HuntKinds.txt), the food chain and the prey a hunter may take, when, the fed hunts' pace, the alert's why,
/// and the seams (the game's own hunt, the setting and the button, nothing saved but the button, no roll of its own).
/// </summary>
public static class HuntTests
{
    private sealed class Row
    {
        public string source;
        public HuntFacts facts;
    }

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "pets hunting: " + what);
        }

        // ---------- Every animal the player's game has, judged ----------
        var rows = new List<Row>();
        foreach (var line in File.ReadAllLines(Path.Combine(root, "Tools", "Tests", "HuntKinds.txt")))
        {
            if (line.StartsWith("#") || line.Trim().Length == 0) continue;
            var c = line.Split('\t');
            rows.Add(new Row
            {
                source = c[0],
                facts = new HuntFacts
                {
                    defName = c[1], label = c[2], predator = c[3] == "1", maxPrey = c[4] == "-" ? 0f : Num(c[4]), meat = c[5].Contains('m'), plants = c[5].Contains('p'),
                    nothing = c[5].Contains('n'), bodySize = Num(c[6]), teeth = c[7] == "1", venom = c[8] == "1", insect = c[9] == "1", dryad = c[10] == "1",
                    pokemon = c[11] != "-", types = c[11] == "-" ? null : c[11].Split(','), legendary = c[12] == "1"
                }
            });
        }
        C(rows.Count >= 1500 && rows.Count(r => r.facts.pokemon) >= 890, $"read every animal of the player's game ({rows.Count}, {rows.Count(r => r.facts.pokemon)} Pokémon)");
        var byName = rows.GroupBy(r => r.facts.defName).ToDictionary(g => g.Key, g => g.First().facts);
        HuntJudgement J(string defName) => byName.TryGetValue(defName, out var f) ? HuntMath.Judge(f) : new HuntJudgement { reason = "missing " + defName };
        void Is(string defName, FoodChain level, string why, bool? pack = null)
        {
            var j = J(defName);
            C(j.level == level && (pack == null || j.pack == pack), $"{defName} is {level}{(pack == true ? " in packs" : "")} ({why}; judged {j})");
        }

        // Real animals by what they really do: dogs, cats, foxes, wolves, bears, big cats, snakes hunt; grazers, fowl, rooters don't.
        Is("Husky", FoodChain.Hunter, "a dog, though the game doesn't flag it", true);
        Is("LabradorRetriever", FoodChain.Hunter, "a dog", true);
        Is("YorkshireTerrier", FoodChain.SmallHunter, "a small dog: a ratter", true);
        Is("AEXP_Pug", FoodChain.Prey, "a lapdog");
        Is("Cat", FoodChain.SmallHunter, "a cat");
        Is("AEXP_CatMaineCoon", FoodChain.SmallHunter, "a big house cat is still a cat");
        Is("Fox_Red", FoodChain.SmallHunter, "a fox");
        Is("Lynx", FoodChain.SmallHunter, "a wild cat of its size");
        Is("Wolf_Timber", FoodChain.Hunter, "a wolf", true);
        Is("Warg", FoodChain.Hunter, "a big wolf", true);
        Is("Bear_Grizzly", FoodChain.Apex, "a bear");
        Is("Cougar", FoodChain.Apex, "a big cat");
        Is("AEXP_Lion", FoodChain.Apex, "a lion, with its pride", true);
        Is("Cobra", FoodChain.SmallHunter, "a small snake");
        Is("AEXP_Anaconda", FoodChain.Apex, "a great constrictor");
        Is("AEXP_Crocodile", FoodChain.Apex, "a crocodile");
        Is("AEXP_Hyena", FoodChain.Hunter, "a hyena", true);
        Is("AEXP_BlackFootedFerret", FoodChain.SmallHunter, "a weasel");
        foreach (var grazer in new[] { "Muffalo", "Deer", "Elk", "Hare", "Squirrel", "Rat", "Chicken", "Duck", "Turkey", "Pig", "WildBoar", "Elephant", "Thrumbo", "Horse",
                     "Alpaca", "Ostrich", "Monkey", "Tortoise", "Iguana", "Raccoon", "AEXP_Panda", "AEXP_Kangaroo", "AEXP_Bonobo" })
            Is(grazer, FoodChain.Prey, "a grazer, a browser, a fowl or a rooter");
        Is("Dryad_Clawer", FoodChain.Prey, "a dryad");
        Is("AEXP_RockhopperPenguin", FoodChain.Prey, "a fish eater, though its diet is meat");
        // Fictive and prehistoric kinds by what they are.
        Is("Palaeeudyptes", FoodChain.Prey, "a giant penguin, though the game flags it a predator");
        Is("Gomphotaria", FoodChain.Prey, "a walrus kin");
        Is("Meganeura", FoodChain.SmallHunter, "a giant dragonfly");
        Is("AA_Plasmorph", FoodChain.Prey, "a snail the game flags");
        Is("AA_Frostmite", FoodChain.Prey, "a scavenger");
        Is("pphhyy_SanguineDeer", FoodChain.Hunter, "a mod's vampire deer: a prey word doesn't overrule the game's flag");
        Is("TyrannosaurusRex", FoodChain.Apex, "a hunting dinosaur");
        Is("Velociraptor", FoodChain.Hunter, "a raptor", true);
        Is("Compsognathus", FoodChain.SmallHunter, "a small hunting dinosaur");
        Is("Triceratops", FoodChain.Prey, "a plant eater");
        Is("Smilodon", FoodChain.Apex, "a sabertooth");
        Is("TM_DemonR", FoodChain.Prey, "summoned: a firm word over the game's flag");
        Is("RG_SabertoothSquirrel", FoodChain.Prey, "a squirrel: the last word of the name first");
        Is("AA_ArcticLion", FoodChain.Apex, "a lion of the cold");
        // Pokémon by species and type.
        Is("PW_Growlithe", FoodChain.SmallHunter, "a canine Pokémon of its size", true);
        Is("PW_Arcanine", FoodChain.Hunter, "a big canine Pokémon", true);
        Is("PW_Mightyena", FoodChain.SmallHunter, "a pack hunter", true);
        Is("PW_Garchomp", FoodChain.Hunter, "a land shark");
        Is("PW_Pidgeotto", FoodChain.SmallHunter, "a bird of prey");
        Is("PW_Meowth", FoodChain.SmallHunter, "a cat");
        Is("PW_Luxray", FoodChain.Hunter, "a lion", true);
        Is("PW_Centiskorch", FoodChain.Apex, "a great hunting centipede");
        foreach (var gentle in new[] { "PW_Pikachu", "PW_Eevee", "PW_Snorlax", "PW_Bulbasaur", "PW_Caterpie", "PW_Rattata", "PW_Pidgey", "PW_Dragonite", "PW_Altaria", "PW_Umbreon",
                     "PW_Sharpedo", "PW_Goodra", "PW_Absol", "PW_Mandibuzz" })
            Is(gentle, FoodChain.Prey, "a Pokémon of no hunting kind, or a gentle Dark or Dragon one");
        Is("PW_Mewtwo", FoodChain.Prey, "a legendary keeps to its legend");
        Is("PW_Rayquaza", FoodChain.Prey, "a legendary keeps to its legend");
        // The type rule for a Pokémon the table doesn't name.
        var darkPup = new HuntFacts { label = "Shadepup", pokemon = true, types = new[] { "Dark" }, bodySize = 0.6f, meat = true, plants = true };
        C(HuntMath.Judge(darkPup).level == FoodChain.SmallHunter && HuntMath.Judge(darkPup).by == "type", "an unnamed Dark type of some size hunts by its size");
        C(HuntMath.Judge(new HuntFacts { label = "Tinyshade", pokemon = true, types = new[] { "Dark" }, bodySize = 0.3f, meat = true }).level == FoodChain.Prey, "a tiny unnamed Dark type doesn't");
        C(HuntMath.Judge(new HuntFacts { label = "Greatwyrm", pokemon = true, types = new[] { "Dragon", "Flying" }, bodySize = 1.8f, meat = true }).level == FoodChain.Apex,
            "a big unnamed Dragon type is apex");
        C(HuntMath.Judge(new HuntFacts { label = "Wyrmling", pokemon = true, types = new[] { "Dragon" }, bodySize = 0.6f, meat = true }).level == FoodChain.Prey, "a small unnamed Dragon type doesn't");
        C(HuntMath.Judge(new HuntFacts { label = "Leafling", pokemon = true, types = new[] { "Grass" }, bodySize = 0.8f, meat = true }).level == FoodChain.Prey, "an unnamed Grass type doesn't");
        // The rule for kinds nobody named.
        C(HuntMath.Judge(new HuntFacts { defName = "X_Thing", label = "glorp", predator = true, meat = true, bodySize = 1f, teeth = true }).level == FoodChain.Hunter, "an unnamed kind the game flags hunts by its size");
        C(HuntMath.Judge(new HuntFacts { defName = "X_Thing", label = "glorp", meat = true, bodySize = 0.5f, teeth = true }).level == FoodChain.SmallHunter, "an unnamed meat eater with teeth hunts");
        C(HuntMath.Judge(new HuntFacts { defName = "X_Thing", label = "glorp", meat = true, plants = true, bodySize = 1f, teeth = true }).level == FoodChain.Prey, "an unnamed omnivore the game doesn't flag doesn't");
        C(HuntMath.Judge(new HuntFacts { defName = "X_Thing", label = "glorp", plants = true, predator = true, bodySize = 1f }).level == FoodChain.Prey, "nothing that eats no meat hunts, whatever its flag");
        C(HuntMath.BySize(0.69f) == FoodChain.SmallHunter && HuntMath.BySize(0.7f) == FoodChain.Hunter && HuntMath.BySize(1.6f) == FoodChain.Apex, "the steps by size: 0.7 and 1.6");

        // The table and the rule agree on the obvious: nothing that eats no meat hunts by either; every kind the game flags
        // hunts by both unless the table says otherwise on purpose (each such entry is named here).
        var purposeful = new[] { "a giant penguin", "a walrus kin", "a snail", "summoned or made", "a carrion bird", "a fish eater", "a panda" };
        int agreed = 0, overrides = 0;
        foreach (var r in rows.Where(r => !r.facts.pokemon))
        {
            var listed = HuntMath.Listed(r.facts);
            var rule = HuntMath.Rule(r.facts);
            if (listed == null) continue;
            bool obvious = !r.facts.meat || r.facts.predator;
            if (!obvious) continue;
            var j = HuntMath.Judge(r.facts);
            if (j.Hunts == rule.Hunts)
            {
                agreed++;
                continue;
            }
            overrides++;
            C(purposeful.Any(p => j.reason.StartsWith(p)), $"{r.facts.defName}: the table differs from the rule only on purpose ({j} against the rule's {rule})");
        }
        C(agreed >= 150 && overrides <= 30, $"the table and the rule agree on the obvious ({agreed} agree, {overrides} on purpose)");
        // Where both find a hunter by its size, they find the same step.
        foreach (var r in rows.Where(r => !r.facts.pokemon && r.facts.predator))
        {
            var j = HuntMath.Judge(r.facts);
            var rule = HuntMath.Rule(r.facts);
            if (j.Hunts && rule.Hunts && j.by == "family" && Math.Abs((int)j.level - (int)rule.level) > 1)
                C(false, $"{r.facts.defName}: the table's step ({j.level}) is within one of the rule's ({rule.level})");
        }
        // Nothing that eats no meat ever hunts, in any mod; no dryad does; every Pokémon is judged by species, type or legend.
        C(rows.Where(r => !r.facts.pokemon && !r.facts.meat).All(r => !HuntMath.Judge(r.facts).Hunts), "no kind that eats no meat hunts");
        C(rows.Where(r => r.facts.dryad).All(r => !HuntMath.Judge(r.facts).Hunts), "no dryad hunts");
        C(rows.Where(r => r.facts.pokemon).All(r => HuntMath.Judge(r.facts).by == "pokemon" || HuntMath.Judge(r.facts).by == "type"), "every Pokémon by its species or type");
        int pokeHunters = rows.Count(r => r.facts.pokemon && HuntMath.Judge(r.facts).Hunts);
        C(pokeHunters >= 80 && pokeHunters <= 200, $"a share of the Pokémon hunt, most don't ({pokeHunters} of {rows.Count(r => r.facts.pokemon)})");
        int hunters = rows.Count(r => !r.facts.pokemon && HuntMath.Judge(r.facts).Hunts);
        C(hunters >= 150, $"the other mods' hunters judged ({hunters})");
        // Every name the table gives is a kind that exists.
        var species = new HashSet<string>(rows.Where(r => r.facts.pokemon).Select(r => HuntMath.PokemonKey(r.facts.label)));
        foreach (var k in HuntMath.PokemonNamed) C(species.Contains(k), $"the Pokémon {k} in the table is a species PokéWorld has");
        foreach (var d in HuntMath.DefNamesNamed) C(byName.ContainsKey(d), $"the kind {d} in the table is one of the player's");
        C(HuntMath.PokemonKey("Jangmo-o") == "jangmoo" && HuntMath.PokemonKey("Mr. Mime") == "mrmime", "a Pokémon's key: its label's letters");

        // ---------- What a hunter may take ----------
        HunterFacts Hunter(string defName, float might, bool pack = false, bool sport = false)
        {
            var f = byName[defName];
            return new HunterFacts { kind = HuntMath.Judge(f), size = f.bodySize, gameMax = f.maxPrey, might = might, pack = pack, sport = sport };
        }
        PreyFacts Prey(string defName, float power, float revenge = 0f, float? size = null)
        {
            var f = byName[defName];
            float s = size ?? f.bodySize;
            return new PreyFacts { animal = true, wild = true, level = HuntMath.Judge(f).level, size = s, might = power * s, revenge = revenge, insect = f.insect, venom = f.venom };
        }
        string Why(HunterFacts h, PreyFacts p) => HuntMath.WhyNotPrey(h, p) ?? "takes it";
        var husky = Hunter("Husky", 75 * 0.86f);
        C(Why(husky, Prey("Hare", 30)) == "takes it", "a husky takes a hare");
        C(Why(husky, Prey("Squirrel", 33)) == "takes it", "a husky takes a squirrel");
        C(Why(husky, Prey("Fox_Red", 50)) == "takes it", "a husky takes a fox, a step below it");
        C(Why(husky, Prey("Deer", 50)) == "too big", "a husky alone doesn't take a deer");
        C(Why(husky, Prey("Wolf_Timber", 70, 3f)) == "not below it in the food chain", "a husky never takes a wolf");
        C(Why(husky, Prey("Cobra", 65, 1.5f)) == "venomous", "a husky never takes a cobra");
        var cat = Hunter("Cat", 35 * 0.32f);
        C(Why(cat, Prey("Rat", 30)) == "takes it" && Why(cat, Prey("Hare", 30)) == "takes it", "a cat takes rats and hares");
        C(Why(cat, Prey("Chicken", 30)) == "too big", "a cat doesn't take a wild hen");
        C(Why(cat, Prey("Fox_Red", 50)) == "not below it in the food chain", "a cat never takes a fox");
        var wolf = Hunter("Wolf_Timber", 70 * 0.85f);
        C(Why(wolf, Prey("Hare", 30)) == "takes it", "a wolf takes a hare");
        C(Why(wolf, Prey("Ibex", 50)) == "too big", "a wolf alone doesn't take an ibex");
        C(Why(Hunter("Wolf_Timber", 70 * 0.85f, pack: true), Prey("Ibex", 50)) == "takes it", "two wolves together take an ibex");
        C(Why(Hunter("Wolf_Timber", 70 * 0.85f, pack: true), Prey("Elk", 110)) == "too big", "two wolves don't take an elk");
        C(Why(Hunter("Wolf_Timber", 70 * 0.85f, sport: true), Prey("Gazelle", 40)) == "too big", "a fed wolf out for small game leaves a gazelle");
        C(Why(Hunter("Wolf_Timber", 70 * 0.85f, pack: true), Prey("Caribou", 110, 0.3f)) != "takes it", "not a caribou: too much of a fight, and its herd would turn");
        var bear = Hunter("Bear_Grizzly", 200 * 2.15f);
        C(Why(bear, Prey("Deer", 50)) == "takes it", "a bear takes a deer");
        C(Why(bear, Prey("Wolf_Timber", 70, 3f)) == "it would turn on it", "a bear leaves a wolf: wolves turn on what hurts them");
        C(Why(bear, Prey("Elk", 110)) == "too big", "a bear alone doesn't take an elk");
        C(Why(bear, Prey("Megascarab", 40)) == "an insect", "never an insect");
        C(Why(Hunter("AEXP_Hyena", 60 * 0.76f), Prey("Muffalo", 100, 0.3f, 0.6f)) != "takes it", "not a muffalo calf with its herd's temper");
        // Pokémon: a big one and a small one.
        var arcanine = Hunter("PW_Arcanine", 292 * 1.19f);
        C(Why(arcanine, Prey("PW_Rattata", 23, 0.3f)) == "takes it", "an Arcanine takes a Rattata, too small to hurt it");
        C(Why(arcanine, Prey("PW_Pidgey", 40, 0.3f)) == "takes it", "an Arcanine takes a Pidgey");
        C(Why(arcanine, Prey("PW_Growlithe", 76, 0.9f)) == "it would turn on it", "an Arcanine leaves a Growlithe: it would turn on it");
        C(Why(arcanine, Prey("PW_Houndoom", 200, 0.9f)) == "not below it in the food chain", "an Arcanine never takes a Houndoom, its equal");
        C(Why(arcanine, Prey("PW_Snorlax", 343, 1.8f)) == "too big", "an Arcanine never takes a Snorlax");
        var growlithe = Hunter("PW_Growlithe", 76 * 0.44f);
        C(Why(growlithe, Prey("PW_Rattata", 23, 0.3f)) == "it would turn on it", "a Growlithe leaves a Rattata near its size that would fight back");
        C(Why(growlithe, Prey("Hare", 30)) == "takes it", "a Growlithe takes a hare");
        // Never anyone's, the stories', the sacred, the penned, the marked, the troubled; never what bursts; a downed one only by size and step.
        var hare = Prey("Hare", 30);
        C(Why(husky, new PreyFacts { animal = true, humanlike = true, wild = true, size = 1f }) == "not an animal", "never a person");
        PreyFacts With(Func<PreyFacts, PreyFacts> f) => f(hare);
        C(Why(husky, With(p => { p.wild = false; return p; })) == "someone's animal", "never anyone's tame animal");
        C(Why(husky, With(p => { p.penned = true; return p; })) == "penned", "never a penned one");
        C(Why(husky, With(p => { p.storied = true; return p; })) == "one of the colony's stories", "never a named alpha, a nemesis, the beast that stays or a legend");
        C(Why(husky, With(p => { p.sacred = true; return p; })) == "held sacred", "never a sacred one");
        C(Why(husky, With(p => { p.lord = true; return p; })) == "in a group (a herd on the move)", "never a herd on the move (a stampede waiting)");
        C(Why(husky, With(p => { p.markedForTaming = true; return p; })) == "marked for taming", "never one marked for taming");
        C(Why(husky, With(p => { p.mental = true; return p; })) == "a manhunter or in a mental state", "never a manhunter");
        C(Why(husky, With(p => { p.oddDeath = true; return p; })) == "it bursts, splits or vanishes when it dies", "never a boomrat");
        var mongoose = new HunterFacts { kind = HuntMath.Judge(new HuntFacts { label = "mongoose", meat = true, bodySize = 0.5f }), size = 0.5f, might = 30f };
        C(mongoose.kind.venom && Why(mongoose, Prey("Cobra", 10, 0.3f, 0.12f)) == "takes it", "a mongoose may take a small snake");
        C(Why(husky, With(p => { p.might = 1000f; p.downed = true; return p; })) == "takes it", "one already down is no fight");
        // The limits.
        C(Math.Abs(HuntMath.PreyLimit(FoodChain.Hunter, 0.86f, 0f, false, false) - 0.688f) < 0.01f, "a husky alone: up to 0.69");
        C(Math.Abs(HuntMath.PreyLimit(FoodChain.Hunter, 0.86f, 0f, true, false) - 1.032f) < 0.01f, "two huskies: up to 1.03");
        C(Math.Abs(HuntMath.PreyLimit(FoodChain.SmallHunter, 0.32f, 0.25f, false, false) - 0.25f) < 0.001f, "a cat: the game's own 0.25");
        C(HuntMath.PreyLimit(FoodChain.SmallHunter, 1f, 1.25f, false, false) <= HuntMath.SmallHunterMax, "a big house cat still small game only");
        C(HuntMath.PreyLimit(FoodChain.Apex, 7f, 0f, false, true) <= HuntMath.SportMax, "out for sport, small game whatever its size");
        C(HuntMath.PreyLimit(FoodChain.Prey, 2f, 0f, true, false) == 0f, "what doesn't hunt takes nothing");

        // ---------- When ----------
        var ok = new HuntState { settingOn = true, hunts = true, adult = true, spawned = true, health = 1f, meleeAttack = true, canMove = true };
        C(HuntMath.WhyNot(ok) == null, "a grown, well hunter in its area may hunt");
        string W(Func<HuntState, HuntState> f) => HuntMath.WhyNot(f(ok));
        C(W(s => { s.settingOn = false; return s; }) == "setting", "the setting off: never");
        C(W(s => { s.keptHome = true; return s; }) == "kept", "its \"May hunt\" off: never");
        C(W(s => { s.hunts = false; return s; }) == "kind", "a kind that doesn't hunt: never");
        C(W(s => { s.adult = false; return s; }) == "young", "the young don't");
        C(W(s => { s.spawned = false; return s; }) == "away", "not in a caravan");
        C(W(s => { s.drafted = true; return s; }) == "drafted", "not drafted");
        C(W(s => { s.followsMaster = true; return s; }) == "master", "not following its master into work or a fight");
        C(W(s => { s.roped = true; return s; }) == "penned" && W(s => { s.penned = true; return s; }) == "penned", "not roped or penned");
        C(W(s => { s.inLord = true; return s; }) == "group", "not with a group (a caravan forming, a ritual)");
        C(W(s => { s.downed = true; return s; }) == "downed" && W(s => { s.medicalRest = true; return s; }) == "sick", "not downed or ill enough for bed");
        C(W(s => { s.health = 0.5f; return s; }) == "hurt", "not hurt");
        C(W(s => { s.danger = true; return s; }) == "danger", "not with danger on the map");
        C(W(s => { s.meleeAttack = false; return s; }) == "unable", "not without an attack");
        float hour = HuntMath.SportHourChance();
        C(Math.Abs(hour - 1f / 96f) < 1e-5f && HuntMath.SportHourChance(0f) == 0f, "a fed hunter sets out about once in four days");
        C(!HuntMath.SportReady(0, 2000, 60000) && HuntMath.SportReady(0, 90000, 60000) && HuntMath.SportReady(-1, 5, 60000), "and not again within a day and a half");
        C(HuntMath.SportAnHour == 1 && Math.Abs(HuntMath.CarryShare - 1f / 3f) < 1e-5f, "one a map an hour; a third of the kills carried home");
        // Fed hunts a colony-year: twenty hunters, each about a sixth of the hours free and fed.
        float year = 20 * 60 * 24 * hour / 6f;
        C(year > 30 && year < 70, $"a big colony's fed hunts a year are modest ({year:0})");

        // ---------- The alert's words ----------
        C(HuntMath.Why(true, true) == NoFoodWhy.Area && HuntMath.Why(false, true) == NoFoodWhy.Forbidden && HuntMath.Why(false, false) == NoFoodWhy.None, "why: the area, the forbidden, nothing");
        C(HuntMath.HuntWord(null, true) == null && HuntMath.HuntWord(null, false) == "prey" && HuntMath.HuntWord("kind", false) == "kind"
          && HuntMath.HuntWord("kept", false) == "kept" && HuntMath.HuntWord("setting", false) == "setting" && HuntMath.HuntWord("hurt", false) == "unable",
            "why it can't hunt: none in reach, its kind, kept, the setting, or the rest");

        // ---------- The seams ----------
        string src(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        string hunts = src("Animals/PetHunts.cs"), patches = src("Animals/PetHuntsPatches.cs"), book = src("Animals/AnimalBook.cs");
        string settings = src("CouncilSettings.cs"), catalog = src("UI/SettingsCatalog.cs");
        C(patches.Contains("[HarmonyPatch(typeof(JobGiver_GetFood), \"TryGiveJob\")]") && patches.Contains("__result.def != JobDefOf.PredatorHunt) return;"),
            "the hungry hunt rides on the game's food search, and food the game found always stands");
        C(hunts.Contains("JobMaker.MakeJob(JobDefOf.PredatorHunt, prey)") && hunts.Contains("job.killIncappedTarget = true;"), "the game's own hunt: it kills, then eats");
        C(hunts.Contains("InAllowedArea(a)") && hunts.Contains("x.IsForbidden(a)") && hunts.Contains("Strayed(a)"), "inside its allowed area, and given up when it strays");
        C(hunts.Contains("FoodUtility.GetPreyScoreFor(a, x)") && hunts.Contains("PawnUtility.GetManhunterOnDamageChance(x, hunter)"), "the game's own prey score and revenge chance");
        C(!hunts.Contains("Harm.Roll") && !hunts.Contains("Harm.Chance") && !hunts.Contains("Harm.Injure"), "no harm roll of its own: the fight is the game's");
        C(!hunts.Contains("LetterStack") && !hunts.Contains("Messages.Message") && !patches.Contains("LetterStack") && !patches.Contains("Messages.Message"), "no letter and no message");
        C(patches.Contains("class Alert_NoFoodInReach : Alert") && patches.Contains("AlertReport.CulpritsAre(culprits)"), "the alert is the game's own, and a click selects them");
        C(hunts.Contains("Council_Pool_SayPetNoFood") && hunts.Contains("DirectRelations.Where(r => r.def == PawnRelationDefOf.Bond)"), "its bonded person says so once");
        C(settings.Contains("public bool petsHunt = true;") && settings.Contains("Scribe_Values.Look(ref petsHunt, \"petsHunt\", true);") && catalog.Contains("Toggle(\"petsHunt\""),
            "the setting \"Pets hunt for themselves\", on by default, saved, on the settings page");
        C(settings.Contains("public bool noFoodAlert = true;") && settings.Contains("Scribe_Values.Look(ref noFoodAlert, \"noFoodAlert\", true);") && catalog.Contains("Toggle(\"noFoodAlert\""),
            "the alert's own switch with the alerts");
        C(patches.Contains("[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]") && hunts.Contains("new Command_Toggle") && hunts.Contains("SetKeptHome(id, !book.KeptHome(id))"),
            "the \"May hunt\" button on each hunter");
        C(book.Contains("Scribe_Collections.Look(ref keptHome, \"keptHome\", LookMode.Value);") && book.Contains("AnimalNatureMath.MaxKeptHome"),
            "only the button is saved: the ids kept home, capped");
        C(src("Beasts/BeastBook.cs").Contains("PetHunts.Rare(now)") && src("CouncilGameComponent.cs").Contains("Safe(\"pets hunting\", PetHunts.Hourly);"),
            "no clock of its own: the wild's short pass and the people's hour");
        C(hunts.Contains("AnimalTempers.Remark(hunter, \"Council_Pool_SeeHunt\"") && hunts.Contains("Council_Pool_PetBringsKill") && hunts.Contains("Council_Evt_PetPresent"),
            "seen: a word at the kill, and a present carried home with the thought it gives");
        string defs = File.ReadAllText(Path.Combine(root, "Defs", "Misc", "Rimfolk_PetHunts.xml"));
        C(defs.Contains("<defName>Rimfolk_PetBringsKill</defName>") && defs.Contains("ColonyCouncil.JobDriver_PetBringsKill"), "the carry home's job");
        return n;
    }

    private static float Num(string s) => float.Parse(s.StartsWith(".") ? "0" + s : s, System.Globalization.CultureInfo.InvariantCulture);
}
