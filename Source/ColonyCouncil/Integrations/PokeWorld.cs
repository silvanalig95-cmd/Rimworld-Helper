using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// PokéWorld (Pokémon as animals), read by name. Nothing here references its assembly. A Pokémon is a race whose
    /// comps include one of a class named CompPokemon; its generation, rarity, types, attributes and shiny chance are read
    /// from that comp's properties, the spawn slider and the generation switches from PokeWorld.PokeWorldSettings (its
    /// GenerationAllowed, else the allowGen fields: eight generations in PokéWorld Updated), the PC storage is a world
    /// component named StorageSystem and the Pokédex one named PokedexManager. Checked against PokéWorld Updated for 1.6
    /// (themoonitself.pokeworld, 898 species) on 7 November 2026. All of it is looked up once at startup: with no Pokémon
    /// races loaded, <see cref="Active"/> is false and every method here returns at once, as if this file didn't exist.
    /// Any name that can't be read is said once in the log, with what Rimfolk does without it.
    ///
    /// What it does with them:
    /// - Rimfolk's own animal events pick their animals the way PokéWorld's spawner does (<see cref="WildKinds"/>, and
    ///   <see cref="Allows"/> for a kind already chosen): only species that live wild in some land (the spawner picks
    ///   from the land's own wild animals, so never a legendary, a fossil, a baby or the few it keeps out of the wild).
    /// - A Pokémon in the PC still counts as one of the colony's animals for wishes and goals that count animals
    ///   (<see cref="OwnedAnimalCount"/>).
    /// - Someone who catches a species the colony never had gets a journal line and a small memory (<see cref="Hourly"/>).
    /// - A colonist with a passion for animals may wish to catch a new species (<see cref="WishWorker_PokemonNewSpecies"/>).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class PokeWorldLink
    {
        /// <summary>
        /// Package ids of the versions we know: the original (1.4), eth0net's update (1.5) and PokéWorld Updated for
        /// 1.6 (themoonitself's, the player's since 7 November 2026). The mod is found by its classes and races anyway,
        /// and its real id is added to the Almanac pages at startup.
        /// </summary>
        public static readonly string[] KnownPackageIds = { "gargamiel.pokeworld", "eth0net.pokeworld", "themoonitself.pokeworld" };

        public const string JournalPool = "Council_Journal_PW_NewSpecies";

        /// <summary>PokéWorld's Pokémon are loaded.</summary>
        public static bool Active { get; private set; }

        /// <summary>The mod that adds the Pokémon, as the game knows it (for the Almanac and the log).</summary>
        public static ModContentPack ModPack { get; private set; }

        private sealed class Species
        {
            public int generation;
            /// <summary>A legendary or fossil Pokémon: never picked for one of Rimfolk's events.</summary>
            public bool rare;
            /// <summary>It lives wild in some land (a biome's wild animals), as PokéWorld's spawner picks them.</summary>
            public bool wild;
            /// <summary>PokéWorld can make one shiny (its shiny chance above nothing, or unread).</summary>
            public bool canShine = true;
            /// <summary>PokéWorld's own rarity, 1 (common) to 10 (legendary); 0 when unread.</summary>
            public int rarity;
            /// <summary>Its types by def name (Grass, Poison...), for what it sounds like (PokeWorldSounds); empty when unread.</summary>
            public List<string> types = new List<string>();
        }

        private static readonly Dictionary<ThingDef, Species> species = new Dictionary<ThingDef, Species>();
        private static readonly HashSet<ThingDef> beltDefs = new HashSet<ThingDef>();
        private static readonly List<RecipeDef> beltRecipes = new List<RecipeDef>();

        private static FieldInfo selectedField, minField, maxField;
        private static readonly Dictionary<int, FieldInfo> generationFields = new Dictionary<int, FieldInfo>();
        /// <summary>PokeWorldSettings.GenerationAllowed(int), what its own spawner asks; null when it has none.</summary>
        private static MethodInfo generationAllowed;
        private static int highestGeneration;
        private static object settingsInstance;
        private static Type storageType, pokedexType;
        private static bool readFailed;

        static PokeWorldLink()
        {
            try
            {
                Detect();
            }
            catch (Exception e)
            {
                Active = false;
                species.Clear();
                CouncilLog.ErrorOnce("PokéWorld detection", e);
            }
        }

        // ===================== Detection (once, at startup) =====================

        private static void Detect()
        {
            ThingDef first = null;
            Type compClass = null;
            bool attributesRead = false;
            foreach (var d in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (d?.race == null || d.comps == null) continue;
                foreach (var c in d.comps)
                {
                    if (c?.compClass == null || !NamedIn(c.compClass, "CompPokemon")) continue;
                    var attributes = ReadNames(c, "attributes");
                    attributesRead |= HasField(c, "attributes");
                    float? shine = ReadFloatField(c, "shinyChance");
                    species[d] = new Species
                    {
                        generation = ReadInt(c, "generation"), rarity = ReadInt(c, "rarity"), rare = attributes.Contains("Legendary") || attributes.Contains("Fossil"),
                        types = ReadNames(c, "types"), canShine = shine == null || shine.Value > 0f
                    };
                    if (first == null)
                    {
                        first = d;
                        compClass = c.compClass;
                    }
                    break;
                }
            }
            Active = species.Count > 0;
            if (!Active)
            {
                // The mod is listed but none of its races look like Pokémon: it changed too much to read.
                if (KnownPackageIds.Any(People.ModActive) || GenTypes.GetTypeInAnyAssembly("PokeWorld.CompPokemon") != null)
                    CouncilLog.WarnOnce("PokéWorld is loaded, but no Pokémon races were recognized; Rimfolk leaves it alone.");
                return;
            }
            ModPack = first.modContentPack;
            // Ball belts: things with a comp whose properties class is named CompProperties_Pokeball, and their recipes.
            foreach (var d in DefDatabase<ThingDef>.AllDefsListForReading)
                if (d?.race == null && d?.comps != null && d.comps.Any(c => c != null && NamedIn(c.GetType(), "CompProperties_Pokeball"))) beltDefs.Add(d);
            foreach (var r in DefDatabase<RecipeDef>.AllDefsListForReading)
                if (r?.ProducedThingDef != null && beltDefs.Contains(r.ProducedThingDef)) beltRecipes.Add(r);

            // What its spawner picks from: the land's own wild animals. Legendaries, fossils, babies (hatched, or the baby
            // that wanders in to join) and the few it keeps out of the wild (Porygon, Unown, Spiritomb...) are in no land's list.
            foreach (var biome in DefDatabase<BiomeDef>.AllDefsListForReading)
            {
                if (biome == null) continue;
                foreach (var k in biome.AllWildAnimals)
                    if (k?.race != null && species.TryGetValue(k.race, out var s)) s.wild = true;
            }
            if (!species.Values.Any(s => s.wild))
            {
                foreach (var s in species.Values) s.wild = true;
                CouncilLog.WarnOnce("PokéWorld: none of its Pokémon live wild in any land's list, so Rimfolk counts every one that isn't a legendary or a fossil as wild.");
            }
            highestGeneration = species.Values.Max(s => s.generation);
            if (highestGeneration <= 0)
                CouncilLog.WarnOnce("PokéWorld: couldn't read its Pokémon's generations, so its generation switches can't keep any out of Rimfolk's events.");
            if (species.Values.All(s => s.rarity <= 0))
                CouncilLog.WarnOnce("PokéWorld: couldn't read its Pokémon's rarity, so Rimfolk's rare sightings won't come.");
            if (species.Values.All(s => s.types.Count == 0))
                CouncilLog.WarnOnce("PokéWorld: couldn't read its Pokémon's types, so the animal each one sounds like is picked by its body and size alone.");
            if (!attributesRead)
            {
                // Without the attributes, PokéWorld's top rarity (10) marks the legendaries.
                foreach (var s in species.Values) s.rare |= s.rarity >= 10;
                CouncilLog.WarnOnce("PokéWorld: couldn't read its Pokémon's attributes; Rimfolk keeps those of the top rarity out of its events as legendaries.");
            }

            var asm = compClass?.Assembly;
            var settingsType = GenTypes.GetTypeInAnyAssembly("PokeWorld.PokeWorldSettings") ?? TypeNamed(asm, "PokeWorldSettings");
            if (settingsType != null)
            {
                selectedField = Field(settingsType, "selectedPokemonFrequency");
                minField = Field(settingsType, "minFrequency");
                maxField = Field(settingsType, "maxFrequency");
                var allowed = settingsType.GetMethod("GenerationAllowed", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(int) }, null);
                if (allowed != null && allowed.ReturnType == typeof(bool)) generationAllowed = allowed;
                for (int gen = 1; gen <= Math.Max(9, highestGeneration); gen++)
                {
                    var f = Field(settingsType, "allowGen" + gen);
                    if (f != null && f.FieldType == typeof(bool)) generationFields[gen] = f;
                }
                if (new[] { selectedField, minField, maxField }.Concat(generationFields.Values).Any(f => f != null && !f.IsStatic))
                    settingsInstance = SettingsOf(settingsType);
            }
            storageType = GenTypes.GetTypeInAnyAssembly("PokeWorld.StorageSystem") ?? TypeNamed(asm, "StorageSystem");
            pokedexType = GenTypes.GetTypeInAnyAssembly("PokeWorld.PokedexManager") ?? TypeNamed(asm, "PokedexManager");

            if (PokemonShare() < 0f)
                CouncilLog.WarnOnce("PokéWorld: couldn't read its wild Pokémon frequency setting, so Rimfolk's animal events pick from every animal of the land.");
            else if (minField == null || maxField == null)
                CouncilLog.WarnOnce("PokéWorld: couldn't read the range of its wild Pokémon frequency slider; Rimfolk assumes 0 to 8, as in the original mod.");
            if (generationAllowed == null && generationFields.Count == 0)
                CouncilLog.WarnOnce("PokéWorld: couldn't read its generation switches, so Rimfolk's animal events let every generation in.");
            else if (generationAllowed == null && highestGeneration > 0 && !generationFields.ContainsKey(highestGeneration))
                CouncilLog.WarnOnce("PokéWorld: found no switch for generation " + highestGeneration + "; Rimfolk lets a generation without a switch in.");
            if (storageType == null)
                CouncilLog.WarnOnce("PokéWorld: couldn't find its PC storage, so Pokémon put away in the PC don't count among the colony's animals for Rimfolk.");
            if (pokedexType == null)
                CouncilLog.WarnOnce("PokéWorld: couldn't find its Pokédex, so the Pokédex goals don't come and a species caught before may be greeted as new once.");
            if (beltDefs.Count == 0)
                CouncilLog.WarnOnce("PokéWorld: no Poké Ball belts were recognized, so Rimfolk won't ask anyone to catch a Pokémon.");
            ShowAlmanacPages();
            if (Prefs.DevMode)
                Log.Message("[Rimfolk] PokéWorld found (" + (ModPack?.Name ?? "?") + "): " + species.Count + " species (" + species.Values.Count(s => s.wild && !s.rare) + " roam the wild, "
                            + species.Values.Count(s => s.rare) + " legendaries and fossils), generations 1 to " + highestGeneration + " ("
                            + (generationAllowed != null ? "its own check" : generationFields.Count + " switches") + "), " + beltDefs.Count + " ball belts, slider "
                            + (PokemonShare() < 0f ? "unreadable" : PokemonShare().ToStringPercent()) + ", PC storage " + (storageType != null ? "found" : "not found")
                            + ", Pokédex " + (pokedexType != null ? "found" : "not found") + ".");
        }

        /// <summary>The type, or one of its base classes, is called <paramref name="name"/> (in any namespace, so a port that moved it still counts).</summary>
        private static bool NamedIn(Type type, string name)
        {
            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
                if (t.Name == name) return true;
            return false;
        }

        private static Type TypeNamed(Assembly asm, string name)
        {
            if (asm == null) return null;
            try
            {
                return asm.GetTypes().FirstOrDefault(t => t.Name == name);
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types?.FirstOrDefault(t => t != null && t.Name == name);
            }
        }

        private static FieldInfo Field(Type type, string name) =>
            type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

        private static int ReadInt(object props, string field)
        {
            try
            {
                var f = props.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
                return f != null ? Convert.ToInt32(f.GetValue(props)) : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>A list field of defs (or anything) as names: a def's defName, else its text. Empty when unread.</summary>
        private static List<string> ReadNames(object props, string field)
        {
            var names = new List<string>();
            try
            {
                var f = props.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
                if (f?.GetValue(props) is IEnumerable list)
                    foreach (var x in list)
                        if (x != null) names.Add(x is Def def ? def.defName : x.ToString());
            }
            catch
            {
                // Unreadable: no types, and the sound is picked by body and size alone.
            }
            return names;
        }

        /// <summary>A Pokémon's types by name (Grass, Poison...): empty for anything else or when unread.</summary>
        public static List<string> TypesOf(ThingDef race) => Active && race != null && species.TryGetValue(race, out var sp) ? sp.types : new List<string>();

        private static bool HasField(object props, string field) => props.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance) != null;

        /// <summary>A number field of the comp's properties, or null when it has none.</summary>
        private static float? ReadFloatField(object props, string field)
        {
            try
            {
                var f = props.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
                return f != null ? Convert.ToSingle(f.GetValue(props)) : (float?)null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>The settings object, for a version that keeps its settings on the instance instead of in static fields.</summary>
        private static object SettingsOf(Type settingsType)
        {
            try
            {
                var handle = LoadedModManager.ModHandles.FirstOrDefault(m => m != null && m.GetType().Assembly == settingsType.Assembly);
                if (handle == null) return null;
                var get = typeof(Verse.Mod).GetMethod("GetSettings", BindingFlags.Public | BindingFlags.Instance);
                return get?.MakeGenericMethod(settingsType).Invoke(handle, null);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// The Almanac's PokéWorld pages name the package ids we know; the 1.6 port may have its own, so the id of the
        /// mod that actually adds the Pokémon is added to them, and they show with any version.
        /// </summary>
        private static void ShowAlmanacPages()
        {
            string id = (ModPack?.PackageIdPlayerFacing ?? ModPack?.PackageId)?.ToLowerInvariant();
            if (id.NullOrEmpty()) return;
            foreach (var page in DefDatabase<AlmanacArticleDef>.AllDefsListForReading)
                if (page.mods != null && page.mods.Any(m => KnownPackageIds.Contains(m)) && !page.mods.Contains(id)) page.mods.Add(id);
        }

        // ===================== Settings (read live, so changes in the mod options apply at once) =====================

        private static object Read(FieldInfo f)
        {
            if (f == null || (!f.IsStatic && settingsInstance == null)) return null;
            try
            {
                return f.GetValue(f.IsStatic ? null : settingsInstance);
            }
            catch (Exception e)
            {
                if (!readFailed) CouncilLog.ErrorOnce("PokéWorld settings", e);
                readFailed = true;
                return null;
            }
        }

        private static float? ReadFloat(FieldInfo f)
        {
            var v = Read(f);
            if (v == null) return null;
            try
            {
                return Convert.ToSingle(v);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// The share of wild animals that are Pokémon, from the slider: 0 at its minimum ("no Pokémon"), 1 at its
        /// maximum ("only Pokémon"), -1 if it can't be read (then Rimfolk leaves its picks alone).
        /// </summary>
        public static float PokemonShare()
        {
            if (!Active) return 0f;
            float? selected = ReadFloat(selectedField);
            if (selected == null) return -1f;
            return AnimalMath.SliderShare(selected.Value, ReadFloat(minField) ?? 0f, ReadFloat(maxField) ?? 8f);
        }

        /// <summary>
        /// The generation switches as they stand now (index = generation, every generation PokéWorld's species have):
        /// its own GenerationAllowed when it has one, as its spawner asks, else its allowGen fields; one we can't read is allowed.
        /// </summary>
        private static bool[] AllowedGenerations()
        {
            var allowed = new bool[Math.Max(10, highestGeneration + 1)];
            for (int gen = 0; gen < allowed.Length; gen++)
                allowed[gen] = GenerationOn(gen);
            return allowed;
        }

        private static bool GenerationOn(int gen)
        {
            if (generationAllowed != null)
            {
                try
                {
                    return (bool)generationAllowed.Invoke(null, new object[] { gen });
                }
                catch (Exception e)
                {
                    // Its fields then, from here on.
                    CouncilLog.ErrorOnce("PokéWorld generation switches", e);
                    generationAllowed = null;
                }
            }
            return !generationFields.TryGetValue(gen, out var f) || !(Read(f) is bool on) || on;
        }

        // ===================== Who is a Pokémon =====================

        public static bool IsPokemon(ThingDef race) => Active && race != null && species.ContainsKey(race);

        /// <summary>Every Pokémon race PokéWorld adds (none when it isn't loaded).</summary>
        public static IEnumerable<ThingDef> AllSpecies => Active ? species.Keys.ToList() : new List<ThingDef>();

        public static bool IsPokemon(PawnKindDef kind) => IsPokemon(kind?.race);

        public static bool IsPokemon(Pawn p) => IsPokemon(p?.def);

        /// <summary>How many species PokéWorld has (its Pokédex), legendaries and fossils included.</summary>
        public static int SpeciesCount => Active ? species.Count : 0;

        /// <summary>A species rare enough to be talked about (PokéWorld's rarity 6 to 9), but not a legendary or a fossil.</summary>
        public static bool Rare(ThingDef race) => Active && race != null && species.TryGetValue(race, out var s) && !s.rare && AnimalMath.RareSighting(s.rarity);

        /// <summary>PokéWorld can make one of this species shiny (all but a few legendaries); true when its chance can't be read.</summary>
        public static bool CanShine(ThingDef race) => Active && race != null && species.TryGetValue(race, out var s) && s.canShine;

        /// <summary>A species that may turn up wild: it lives wild in some land, and isn't a legendary or a fossil.</summary>
        private static bool Roams(Species s, bool[] allowed) =>
            AnimalMath.PokemonRoams(s.rare, s.wild) && (s.generation < 0 || s.generation >= allowed.Length || allowed[s.generation]);

        /// <summary>
        /// The species that may turn up wild for Rimfolk now: living wild in some land, not a legendary or a fossil, of a
        /// generation switched on, and only while the spawn slider lets any Pokémon in at all.
        /// </summary>
        public static IEnumerable<ThingDef> Roaming()
        {
            if (!Active || PokemonShare() == 0f) yield break;
            var allowed = AllowedGenerations();
            foreach (var kv in species)
                if (Roams(kv.Value, allowed)) yield return kv.Key;
        }

        /// <summary>Ball belts on this map, kept or worn.</summary>
        public static int BeltCount(Map map)
        {
            if (!Active || map == null || beltDefs.Count == 0) return 0;
            int n = 0;
            foreach (var belt in beltDefs) n += map.listerThings.ThingsOfDef(belt).Count;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                var worn = p.apparel?.WornApparel;
                if (worn == null) continue;
                for (int i = 0; i < worn.Count; i++)
                    if (beltDefs.Contains(worn[i].def)) n++;
            }
            return n;
        }

        /// <summary>A ball belt can be made here now: its research done, and its bench built or buildable. The cheapest one, or null.</summary>
        public static ThingDef MakeableBelt(Map map)
        {
            if (!Active || map == null) return null;
            foreach (var r in beltRecipes.OrderBy(x => x.ProducedThingDef?.BaseMarketValue ?? 0f))
                if (r.AvailableNow && Avail.RecipeUsers(r).Any(u => map.listerBuildings.ColonistsHaveBuilding(u) || Avail.CanBuild(u, map))) return r.ProducedThingDef;
            return null;
        }

        // ===================== Rimfolk's animal picks =====================

        /// <summary>
        /// The animal kinds one of Rimfolk's events may pick from, as PokéWorld's spawner would have them: at the
        /// slider's maximum only Pokémon, at its minimum no Pokémon, in between this pick is Pokémon with the slider's
        /// chance (falling back to the other side when that side has nothing that fits, as its spawner does). Only
        /// Pokémon that live wild in some land, of a generation switched on: never a legendary, a fossil, a baby or one
        /// it keeps out of the wild. Without PokéWorld, or with its slider unreadable, the kinds come back unchanged.
        /// </summary>
        public static IEnumerable<PawnKindDef> WildKinds(IEnumerable<PawnKindDef> kinds)
        {
            if (!Active || kinds == null) return kinds;
            float share = PokemonShare();
            if (share < 0f) return kinds;
            var allowed = AllowedGenerations();
            var pokemon = new List<PawnKindDef>();
            var others = new List<PawnKindDef>();
            foreach (var k in kinds)
            {
                if (k?.race == null) continue;
                if (!species.TryGetValue(k.race, out var s)) others.Add(k);
                else if (Roams(s, allowed)) pokemon.Add(k);
            }
            if (share >= 1f) return pokemon;
            if (share <= 0f) return others;
            bool wantPokemon = Rand.Chance(share);
            var side = wantPokemon ? pokemon : others;
            return side.Count > 0 ? side : wantPokemon ? others : pokemon;
        }

        /// <summary>
        /// The same rule for one kind already chosen (the young of a remembered great beast): false only where
        /// <see cref="WildKinds"/> could never pick it, so a Pokémon at "No Pokémon", anything else at "Only Pokémon".
        /// </summary>
        public static bool Allows(PawnKindDef kind) => kind != null && WildKinds(new[] { kind })?.Any() == true;

        // ===================== The colony's Pokémon =====================

        private static int storedTick = -99999;
        private static Game storedGame;
        private static int storedCount;

        /// <summary>The colony's animals in PokéWorld's PC storage (a world component holding them, off every map).</summary>
        private static List<Pawn> InStorage()
        {
            var list = new List<Pawn>();
            if (!Active || storageType == null || Find.World?.components == null) return list;
            try
            {
                foreach (var c in Find.World.components)
                {
                    if (c == null || !storageType.IsInstanceOfType(c) || !(c is IThingHolder holder)) continue;
                    var things = holder.GetDirectlyHeldThings();
                    if (things == null) continue;
                    for (int i = 0; i < things.Count; i++)
                        if (things[i] is Pawn p && !p.Dead && p.Faction == Faction.OfPlayer && p.RaceProps != null && p.RaceProps.Animal) list.Add(p);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("PokéWorld storage", e);
                list.Clear();
            }
            return list;
        }

        /// <summary>How many animals are in the PC, counted at most every 250 ticks (the goal list asks every frame).</summary>
        private static int StoredCount()
        {
            if (!Active || storageType == null) return 0;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (storedGame == Current.Game && now >= storedTick && now - storedTick < 250) return storedCount;
            storedGame = Current.Game;
            storedTick = now;
            storedCount = InStorage().Count;
            return storedCount;
        }

        /// <summary>
        /// The colony's animals on this map (<see cref="RoomUtil.ColonyAnimalCount"/>, which already counts a Pokémon in
        /// its Poké Ball), plus, on a home map, the Pokémon put away in the PC. For wishes and goals that count how many
        /// animals the colony has gained, so storing one doesn't undo the progress.
        /// </summary>
        public static int OwnedAnimalCount(Map map)
        {
            int n = RoomUtil.ColonyAnimalCount(map);
            if (Active && map != null && map.IsPlayerHome) n += StoredCount();
            return n;
        }

        /// <summary>The Pokémon species the colony has: on this map (in or out of their balls) and in the PC.</summary>
        public static HashSet<ThingDef> OwnedSpecies(Map map)
        {
            var set = new HashSet<ThingDef>();
            if (!Active || map == null) return set;
            foreach (var p in map.mapPawns.PawnsInFaction(Faction.OfPlayer))
                if (!p.Dead && IsPokemon(p)) set.Add(p.def);
            if (map.IsPlayerHome)
                foreach (var p in InStorage())
                    if (IsPokemon(p)) set.Add(p.def);
            return set;
        }

        /// <summary>A wild Pokémon is out here that someone could go after: visible, unclaimed, not already beaten.</summary>
        public static IEnumerable<Pawn> WildPokemon(Map map)
        {
            if (!Active || map == null) yield break;
            foreach (var a in map.mapPawns.AllPawnsSpawned)
                if (a.Faction == null && !a.Dead && IsPokemon(a) && !map.fogGrid.IsFogged(a.Position)) yield return a;
        }

        /// <summary>
        /// The colony could throw a Poké Ball now: a ball belt is worn or kept on this map, or one can be made (its
        /// research done, and its bench built or buildable).
        /// </summary>
        public static bool CanCatch(Map map)
        {
            if (!Active || map == null || beltDefs.Count == 0) return false;
            foreach (var belt in beltDefs)
                if (map.listerThings.ThingsOfDef(belt).Count > 0) return true;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                var worn = p.apparel?.WornApparel;
                if (worn == null) continue;
                for (int i = 0; i < worn.Count; i++)
                    if (beltDefs.Contains(worn[i].def)) return true;
            }
            foreach (var r in beltRecipes)
                if (r.AvailableNow && Avail.RecipeUsers(r).Any(u => map.listerBuildings.ColonistsHaveBuilding(u) || Avail.CanBuild(u, map))) return true;
            return false;
        }

        // ===================== A species nobody here had caught =====================

        private static Game trackedGame;
        private static bool tracking;
        private static readonly HashSet<ThingDef> knownSpecies = new HashSet<ThingDef>();
        private static readonly Dictionary<Pawn, int> tamedBefore = new Dictionary<Pawn, int>();

        /// <summary>
        /// Each hour: a Pokémon of a species the colony never had, whose arrival matches a colonist's "animals tamed"
        /// record going up (a Poké Ball catch counts as taming; hatching, evolving, buying and wander-ins don't; see
        /// <see cref="Catcher"/>), earns the catcher a journal line, a small memory and a quiet message. The first hour after loading only notes what
        /// the colony has, together with what PokéWorld's Pokédex says was ever caught. Nothing is saved.
        /// </summary>
        public static void Hourly(CouncilGameComponent comp)
        {
            if (!Active || comp == null) return;
            if (trackedGame != Current.Game)
            {
                trackedGame = Current.Game;
                tracking = false;
                knownSpecies.Clear();
                tamedBefore.Clear();
            }
            var ours = new List<Pawn>();
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                foreach (var p in map.mapPawns.PawnsInFaction(Faction.OfPlayer))
                    if (!p.Dead && IsPokemon(p)) ours.Add(p);
            }
            if (!tracking)
            {
                foreach (var p in ours) knownSpecies.Add(p.def);
                foreach (var p in InStorage()) knownSpecies.Add(p.def);
                AddPokedexCaught();
                tracking = true;
            }
            else
            {
                List<Pawn> fresh = null;
                foreach (var p in ours)
                    if (knownSpecies.Add(p.def)) (fresh ??= new List<Pawn>()).Add(p);
                if (fresh != null) Celebrate(comp, fresh);
            }
            NoteTamed();
            // The colony's Pokémon growing up: evolutions, levels, friendship, fainting (Integrations/PokeWorldLife.cs).
            try
            {
                PokeWorldLife.Hourly(comp);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("PokéWorld: the colony's Pokémon", e);
            }
        }

        /// <summary>PokéWorld's Pokédex, the world component named PokedexManager; null without one.</summary>
        public static WorldComponent Pokedex()
        {
            if (!Active || Find.World?.components == null) return null;
            foreach (var c in Find.World.components)
                if (c != null && (pokedexType != null ? pokedexType.IsInstanceOfType(c) : c.GetType().Name == "PokedexManager")) return c;
            return null;
        }

        /// <summary>
        /// Species PokéWorld's Pokédex marks as caught: its IsPokemonCaught(kind) (PokéWorld Updated keeps the Pokédex by
        /// number, each entry with its kinds), else the original's private "pokedex" dictionary of kinds. Said once in the
        /// log when neither can be read: then a species caught long ago may be greeted as new once.
        /// </summary>
        private static void AddPokedexCaught()
        {
            var dex = Pokedex();
            if (dex == null) return;
            try
            {
                var isCaught = dex.GetType().GetMethod("IsPokemonCaught", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(PawnKindDef) }, null);
                if (isCaught != null && isCaught.ReturnType == typeof(bool))
                {
                    foreach (var k in DefDatabase<PawnKindDef>.AllDefsListForReading)
                        if (k?.race != null && species.ContainsKey(k.race) && !knownSpecies.Contains(k.race) && (bool)isCaught.Invoke(dex, new object[] { k })) knownSpecies.Add(k.race);
                    return;
                }
                var f = dex.GetType().GetField("pokedex", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (f?.GetValue(dex) is IDictionary entries)
                {
                    foreach (DictionaryEntry e in entries)
                        if (e.Key is PawnKindDef k && k.race != null && e.Value?.ToString() == "Caught") knownSpecies.Add(k.race);
                    return;
                }
                CouncilLog.WarnOnce("PokéWorld: couldn't read which species its Pokédex has caught; a species caught long ago may be greeted as new once.");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("PokéWorld: reading the Pokédex's caught species", e);
            }
        }

        private static void NoteTamed()
        {
            tamedBefore.Clear();
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                foreach (var p in map.mapPawns.FreeColonistsSpawned)
                    if (p.records != null) tamedBefore[p] = p.records.GetAsInt(RecordDefOf.AnimalsTamed);
            }
        }

        /// <summary>
        /// Who caught it: a colonist on its map whose "animals tamed" went up this hour (its master if that's one of
        /// them, else the nearest). If no record moved (a version that catches some other way), a Pokémon still in its
        /// ball goes to the nearest colonist wearing a ball belt within 40 cells; one out of its ball (hatched, bought,
        /// evolved, wandered in) to nobody.
        /// </summary>
        private static Pawn Catcher(Pawn pokemon)
        {
            var map = pokemon.MapHeld;
            if (map == null) return null;
            var at = pokemon.PositionHeld;
            Pawn best = null, wearer = null;
            float bestDist = float.MaxValue, wearerDist = 40f * 40f;
            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                float d = p.Position.DistanceToSquared(at);
                if (p.records != null && tamedBefore.TryGetValue(p, out int before) && p.records.GetAsInt(RecordDefOf.AnimalsTamed) > before)
                {
                    if (pokemon.playerSettings?.Master == p) return p;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = p;
                    }
                }
                else if (!pokemon.Spawned && d < wearerDist && p.apparel?.WornApparel != null && p.apparel.WornApparel.Any(a => beltDefs.Contains(a.def)))
                {
                    wearerDist = d;
                    wearer = p;
                }
            }
            return best ?? wearer;
        }

        private static void Celebrate(CouncilGameComponent comp, List<Pawn> fresh)
        {
            var thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_PW_CaughtNewSpecies");
            var granted = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Granted_PW_NewSpecies");
            var done = new HashSet<Pawn>();
            foreach (var a in fresh)
            {
                var who = Catcher(a);
                if (who == null || !People.HasInnerLife(who) || !done.Add(who)) continue;
                string kind = a.kindDef?.label ?? a.def.label;
                using (Causes.Push("CC_PW_Cause_NewSpecies".Translate(kind.Named("SPECIES")).Resolve()))
                {
                    News.Post("CC_PW_NewSpecies".Translate(who.Named("PAWN"), kind.Named("SPECIES")).Resolve(), new LookTargets(who), NewsKind.Land, MessageTypeDefOf.PositiveEvent, Importance.Flavor);
                    // A trainer whose wish this answered has the wish's own thanks already; no second memory on top.
                    var memories = who.needs?.mood?.thoughts?.memories;
                    if (granted == null || memories?.GetFirstMemoryOfDef(granted) == null) CouncilGameComponent.GiveThought(who, thought);
                    comp.journals.Write(who, JournalPool, null, kind.Named("BEAST"));
                }
            }
        }
    }

    /// <summary>
    /// PokéWorld: a colonist with a passion for animals (or an animal lover from a trait mod) wants to catch a species
    /// the colony doesn't have yet, when one is out on the map and the colony has a ball belt or can make one now.
    /// Fulfilled when the colony has more Pokémon species than when they asked. Without PokéWorld it never comes up,
    /// and an open one lapses quietly if the mod is removed.
    /// </summary>
    public class WishWorker_PokemonNewSpecies : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            if (!PokeWorldLink.Active) yield break;
            var map = HomeMap(pawn);
            var animals = pawn.skills?.GetSkill(SkillDefOf.Animals);
            if (map == null || animals == null || animals.TotallyDisabled || !(People.Passionate(animals) || PeopleTags.Has(pawn, "animallover"))) yield break;
            if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Handling)) yield break;
            var wild = PokeWorldLink.WildPokemon(map).ToList();
            if (wild.Count == 0) yield break;
            var owned = PokeWorldLink.OwnedSpecies(map);
            wild.RemoveAll(a => owned.Contains(a.def));
            if (wild.Count == 0 || !PokeWorldLink.CanCatch(map)) yield break;
            var example = wild.RandomElement();
            string kind = example.kindDef?.label ?? example.def.label;
            var c = Candidate(People.Burning(animals) ? 1.2f : 0.6f, "CC_PW_Reason_NewSpecies".Translate(pawn.Named("PAWN"), kind.Named("SPECIES")));
            c.baseline = owned.Count;
            c.subjectLabel = kind;
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => PokeWorldLink.OwnedSpecies(wish.Beneficiary?.MapHeld).Count > wish.baseline;

        public override bool StillValid(Wish wish) => PokeWorldLink.Active;
    }
}
