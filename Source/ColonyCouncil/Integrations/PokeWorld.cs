using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// PokéWorld (Pokémon as animals), read by name. Nothing here references its assembly. A Pokémon is a race whose
    /// comps include one of a class named CompPokemon; its generation and attributes are read from that comp's
    /// properties, the spawn slider and the generation switches from PokeWorld.PokeWorldSettings, and the PC storage is
    /// a world component named StorageSystem. All of it is looked up once at startup: with no Pokémon races loaded,
    /// <see cref="Active"/> is false and every method here returns at once, as if this file didn't exist.
    ///
    /// What it does with them:
    /// - Rimfolk's own animal events pick their animals the way PokéWorld's spawner does (<see cref="WildKinds"/>, and
    ///   <see cref="Allows"/> for a kind already chosen).
    /// - A Pokémon in the PC still counts as one of the colony's animals for wishes and goals that count animals
    ///   (<see cref="OwnedAnimalCount"/>).
    /// - Someone who catches a species the colony never had gets a journal line and a small memory (<see cref="Hourly"/>).
    /// - A colonist with a passion for animals may wish to catch a new species (<see cref="WishWorker_PokemonNewSpecies"/>).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class PokeWorldLink
    {
        /// <summary>
        /// Package ids of the versions we know: the original (1.4) and eth0net's update (1.5). The 1.6 port may use
        /// either or one of its own; the mod is found by its classes and races anyway, and its real id is added to the
        /// Almanac pages at startup.
        /// </summary>
        public static readonly string[] KnownPackageIds = { "gargamiel.pokeworld", "eth0net.pokeworld" };

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
        }

        private static readonly Dictionary<ThingDef, Species> species = new Dictionary<ThingDef, Species>();
        private static readonly HashSet<ThingDef> beltDefs = new HashSet<ThingDef>();
        private static readonly List<RecipeDef> beltRecipes = new List<RecipeDef>();

        private static FieldInfo selectedField, minField, maxField;
        private static readonly Dictionary<int, FieldInfo> generationFields = new Dictionary<int, FieldInfo>();
        private static object settingsInstance;
        private static Type storageType;
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
            foreach (var d in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (d?.race == null || d.comps == null) continue;
                foreach (var c in d.comps)
                {
                    if (c?.compClass == null || !NamedIn(c.compClass, "CompPokemon")) continue;
                    species[d] = new Species { generation = ReadInt(c, "generation"), rare = HasAttribute(c, "Legendary") || HasAttribute(c, "Fossil") };
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

            var asm = compClass?.Assembly;
            var settingsType = GenTypes.GetTypeInAnyAssembly("PokeWorld.PokeWorldSettings") ?? TypeNamed(asm, "PokeWorldSettings");
            if (settingsType != null)
            {
                selectedField = Field(settingsType, "selectedPokemonFrequency");
                minField = Field(settingsType, "minFrequency");
                maxField = Field(settingsType, "maxFrequency");
                for (int gen = 1; gen <= 9; gen++)
                {
                    var f = Field(settingsType, "allowGen" + gen);
                    if (f != null && f.FieldType == typeof(bool)) generationFields[gen] = f;
                }
                if (new[] { selectedField, minField, maxField }.Concat(generationFields.Values).Any(f => f != null && !f.IsStatic))
                    settingsInstance = SettingsOf(settingsType);
            }
            storageType = GenTypes.GetTypeInAnyAssembly("PokeWorld.StorageSystem") ?? TypeNamed(asm, "StorageSystem");

            if (PokemonShare() < 0f)
                CouncilLog.WarnOnce("PokéWorld: couldn't read its wild Pokémon frequency setting, so Rimfolk's animal events pick from every animal of the land.");
            else if (minField == null || maxField == null)
                CouncilLog.WarnOnce("PokéWorld: couldn't read the range of its wild Pokémon frequency slider; Rimfolk assumes 0 to 8, as in the original mod.");
            if (beltDefs.Count == 0)
                CouncilLog.WarnOnce("PokéWorld: no Poké Ball belts were recognized, so Rimfolk won't ask anyone to catch a Pokémon.");
            ShowAlmanacPages();
            if (Prefs.DevMode)
                Log.Message("[Rimfolk] PokéWorld found (" + (ModPack?.Name ?? "?") + "): " + species.Count + " species, " + beltDefs.Count + " ball belts, slider "
                            + (PokemonShare() < 0f ? "unreadable" : PokemonShare().ToStringPercent()) + ", PC storage " + (storageType != null ? "found" : "not found") + ".");
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

        private static bool HasAttribute(object props, string attribute)
        {
            try
            {
                var f = props.GetType().GetField("attributes", BindingFlags.Public | BindingFlags.Instance);
                if (!(f?.GetValue(props) is IEnumerable list)) return false;
                foreach (var a in list)
                    if (a != null && a.ToString() == attribute) return true;
            }
            catch
            {
                // Unreadable: treat it as an ordinary Pokémon.
            }
            return false;
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
            float min = ReadFloat(minField) ?? 0f, max = ReadFloat(maxField) ?? 8f;
            if (max <= min) return -1f;
            return Mathf.Clamp01((selected.Value - min) / (max - min));
        }

        /// <summary>The generation switches as they stand now (index = generation); a generation we can't read is allowed.</summary>
        private static bool[] AllowedGenerations()
        {
            var allowed = new bool[10];
            for (int gen = 0; gen < allowed.Length; gen++)
                allowed[gen] = !generationFields.TryGetValue(gen, out var f) || !(Read(f) is bool on) || on;
            return allowed;
        }

        // ===================== Who is a Pokémon =====================

        public static bool IsPokemon(ThingDef race) => Active && race != null && species.ContainsKey(race);

        public static bool IsPokemon(PawnKindDef kind) => IsPokemon(kind?.race);

        public static bool IsPokemon(Pawn p) => IsPokemon(p?.def);

        // ===================== Rimfolk's animal picks =====================

        /// <summary>
        /// The animal kinds one of Rimfolk's events may pick from, as PokéWorld's spawner would have them: at the
        /// slider's maximum only Pokémon, at its minimum no Pokémon, in between this pick is Pokémon with the slider's
        /// chance (falling back to the other side when that side has nothing that fits). Pokémon of a generation
        /// switched off, legendaries and fossils are never picked. Without PokéWorld, or with its slider unreadable,
        /// the kinds come back unchanged.
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
                else if (!s.rare && (s.generation < 0 || s.generation >= allowed.Length || allowed[s.generation])) pokemon.Add(k);
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
        }

        /// <summary>Species PokéWorld's Pokédex marks as caught (its private "pokedex" dictionary); skipped quietly if unreadable.</summary>
        private static void AddPokedexCaught()
        {
            try
            {
                var dex = Find.World?.components?.FirstOrDefault(c => c != null && c.GetType().Name == "PokedexManager");
                var f = dex?.GetType().GetField("pokedex", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (!(f?.GetValue(dex) is IDictionary entries)) return;
                foreach (DictionaryEntry e in entries)
                    if (e.Key is PawnKindDef k && k.race != null && e.Value?.ToString() == "Caught") knownSpecies.Add(k.race);
            }
            catch
            {
                // A changed Pokédex only means a species caught long ago may be greeted as new once.
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
