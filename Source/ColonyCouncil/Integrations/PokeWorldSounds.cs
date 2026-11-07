using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What PokéWorld's Pokémon sound like (the player's request, 6 November 2026: "i love the pokeworld mod, but the
    /// constant pokemon screetches not ... replace them with other creature sounds from the base game or mods"). The
    /// setting "Pokémon sound like" (<see cref="CouncilSettings.pokemonSounds"/>, the other mods' page):
    /// - <see cref="PokemonSounds.Creatures"/> (the default): each species borrows the calls of an animal picked by its body
    ///   plan's family, its types and its size: another mod's where one that fits it better is loaded and calls grown, else
    ///   one of the base game's (<see cref="PokeWorldSoundsMath.Voices"/>, <see cref="PokeWorldSoundsMath.Pick"/>; the
    ///   nearest animal of the same family and size when none of those is there): its idle call, its angry call, its cries
    ///   when wounded and dying and its attack sound. The pace between calls is the slower of PokéWorld's and the animal's.
    /// - <see cref="PokemonSounds.Rarer"/>: their own cries, a third as often.
    /// - <see cref="PokemonSounds.Own"/>: as PokéWorld has them.
    /// The game plays an animal's calls from its race's life stages (Pawn_CallTracker, LifeStageUtility), so this changes
    /// def data only, no hook: each species gets its own copies of its life stages with the borrowed sounds, and the
    /// originals are put back when the setting changes (applied at startup and when the settings window closes). Nothing
    /// is saved. With PokéWorld not loaded, nothing happens.
    /// PokéWorld Updated (checked 7 November 2026) gives each of its 898 species one life stage whose call, angry, wounded
    /// and death sounds are all its cry, and plays no cry of its own anywhere else: its other sounds are its moves', its
    /// balls' and its menus', and a Pokémon caught or fished calls through the game's own call. So the life stages cover
    /// every cry, and the moves and balls are left as they are.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class PokeWorldSounds
    {
        private sealed class Original
        {
            public List<LifeStageAge> stages;
            public IntRange interval;
            public float friendly, aggressive;
        }

        private static readonly Dictionary<ThingDef, Original> originals = new Dictionary<ThingDef, Original>();
        private static readonly Dictionary<ThingDef, ThingDef> matches = new Dictionary<ThingDef, ThingDef>();
        private static readonly MethodInfo Clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>What the defs hold now: PokéWorld's own until something is applied.</summary>
        public static PokemonSounds Applied { get; private set; } = PokemonSounds.Own;

        public static PokemonSounds Wanted => ColonyCouncilMod.Settings?.pokemonSounds ?? PokemonSounds.Creatures;

        static PokeWorldSounds()
        {
            Apply();
        }

        /// <summary>The setting into the defs (once at startup, again when it changes). Safe to call any time.</summary>
        public static void Apply()
        {
            try
            {
                if (!PokeWorldLink.Active) return;
                var mode = Wanted;
                if (mode == Applied) return;
                Restore();
                if (mode == PokemonSounds.Rarer)
                {
                    foreach (var d in PokeWorldLink.AllSpecies)
                    {
                        Keep(d);
                        var r = d.race;
                        r.soundCallIntervalRange = new IntRange(PokeWorldSoundsMath.Rarer(r.soundCallIntervalRange.min), PokeWorldSoundsMath.Rarer(r.soundCallIntervalRange.max));
                    }
                }
                else if (mode == PokemonSounds.Creatures)
                {
                    var animals = Candidates();
                    var byName = new Dictionary<string, ThingDef>();
                    foreach (var a in animals)
                        if (!byName.ContainsKey(a.defName)) byName[a.defName] = a;
                    int given = 0;
                    standIns = 0;
                    foreach (var d in PokeWorldLink.AllSpecies)
                    {
                        var m = VoiceOf(d, animals, byName);
                        if (m == null) continue;
                        Keep(d);
                        Borrow(d, m);
                        given++;
                    }
                    if (Prefs.DevMode || given == 0)
                        Log.Message("[Rimfolk] PokéWorld: " + given + " of " + PokeWorldLink.SpeciesCount + " species sound like animals now, from " + animals.Count + " to choose from"
                                    + (standIns > 0 ? ", " + standIns + " by the nearest animal since the one named for them is missing" : "")
                                    + (matches.Count > 0 ? " (" + matches.Take(5).Select(kv => kv.Key.label + " like a " + kv.Value.label).ToCommaList() + ")." : "."));
                    if (Prefs.DevMode && !moddedLogged)
                    {
                        moddedLogged = true;
                        var modded = matches.Values.Where(a => a.modContentPack != null && !a.modContentPack.IsOfficialMod).GroupBy(a => a).OrderByDescending(g => g.Count())
                            .Select(g => g.Key.label + " (" + g.Key.modContentPack.Name + ") for " + g.Count()).ToList();
                        Log.Message("[Rimfolk] PokéWorld: " + (modded.Count > 0
                            ? "voices from other mods in use: " + modded.ToCommaList() + "."
                            : "no voice from another mod in use, only the base game's."));
                    }
                }
                Applied = mode;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("PokéWorld's sounds", e);
            }
        }

        /// <summary>The animal a species sounds like, or null (with PokéWorld's own sounds, or none found).</summary>
        public static ThingDef SoundsLike(ThingDef pokemon) => Applied == PokemonSounds.Creatures && pokemon != null && matches.TryGetValue(pokemon, out var m) ? m : null;

        /// <summary>
        /// Animals whose grown ones call: not Pokémon, not mechanoids, dryads or the void's creatures. The game plays a call
        /// from the animal's own stage onward (LifeStageUtility), so a grown turkey, emu or ostrich, whose chicks alone have
        /// a call, is silent; one of those would lend a chick's cheep to a grown Pokémon, and isn't taken.
        /// </summary>
        private static List<ThingDef> Candidates()
        {
            var list = new List<ThingDef>();
            foreach (var d in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                var r = d.race;
                if (r == null || !r.Animal || r.Humanlike || r.IsMechanoid || r.Dryad || PokeWorldLink.IsPokemon(d)) continue;
                if (r.lifeStageAges.NullOrEmpty() || r.lifeStageAges[r.lifeStageAges.Count - 1]?.soundCall == null) continue;
                list.Add(d);
            }
            return list;
        }

        /// <summary>
        /// The animal a species sounds like: the first the rule names for its body, types and size that is loaded and calls
        /// grown (another mod's where it fits better, else the base game's), else the nearest animal of the same family and
        /// size (counted in <see cref="standIns"/>).
        /// </summary>
        private static ThingDef VoiceOf(ThingDef pokemon, List<ThingDef> animals, Dictionary<string, ThingDef> byName)
        {
            var r = pokemon.race;
            var voices = PokeWorldSoundsMath.Voices(r.body?.defName, r.baseBodySize, PokeWorldLink.TypesOf(pokemon));
            string named = PokeWorldSoundsMath.Pick(voices, byName.ContainsKey, pokemon.defName);
            if (named != null) return byName[named];
            standIns++;
            return Nearest(pokemon, animals);
        }

        /// <summary>Whether the voices from other mods were said in the log already (once a session, in dev mode).</summary>
        private static bool moddedLogged;

        /// <summary>Species whose named animal was missing or silent when the voices were last given, so the nearest stood in.</summary>
        private static int standIns;

        /// <summary>The nearest animal: the same body family and flesh, the nearest size; ties go to the def name, so it's the same each time.</summary>
        private static ThingDef Nearest(ThingDef pokemon, List<ThingDef> animals)
        {
            var r = pokemon.race;
            var family = PokeWorldSoundsMath.FamilyOf(r.body?.defName);
            ThingDef best = null;
            float bestDistance = float.MaxValue;
            foreach (var a in animals)
            {
                float d = PokeWorldSoundsMath.Distance(PokeWorldSoundsMath.FamilyOf(a.race.body?.defName) == family, a.race.FleshType == r.FleshType, r.baseBodySize,
                    a.race.baseBodySize);
                if (d < bestDistance - 0.0001f || Math.Abs(d - bestDistance) <= 0.0001f && best != null && string.CompareOrdinal(a.defName, best.defName) < 0)
                {
                    best = a;
                    bestDistance = d;
                }
            }
            return best;
        }

        private static void Keep(ThingDef d)
        {
            if (originals.ContainsKey(d)) return;
            var r = d.race;
            originals[d] = new Original
            {
                stages = r.lifeStageAges, interval = r.soundCallIntervalRange, friendly = r.soundCallIntervalFriendlyFactor,
                aggressive = r.soundCallIntervalAggressiveFactor
            };
        }

        /// <summary>The species' own copies of its life stages, each with the borrowed animal's sounds, and the animal's pace.</summary>
        private static void Borrow(ThingDef d, ThingDef animal)
        {
            var r = d.race;
            var from = animal.race.lifeStageAges;
            var own = originals[d].stages;
            var copies = new List<LifeStageAge>(own.Count);
            for (int i = 0; i < own.Count; i++)
            {
                var copy = (LifeStageAge)Clone.Invoke(own[i], null);
                int j = PokeWorldSoundsMath.StageFor(i, own.Count, from.Count);
                copy.soundCall = Nearest(from, j, l => l.soundCall);
                copy.soundAngry = Nearest(from, j, l => l.soundAngry);
                copy.soundWounded = Nearest(from, j, l => l.soundWounded);
                copy.soundDeath = Nearest(from, j, l => l.soundDeath);
                copy.soundAttack = Nearest(from, j, l => l.soundAttack);
                copies.Add(copy);
            }
            r.lifeStageAges = copies;
            // Never more often than PokéWorld had it (its 3,000 to 6,000 ticks are already longer than most animals').
            var was = originals[d];
            var pace = PokeWorldSoundsMath.Slower(was.interval.min, was.interval.max, animal.race.soundCallIntervalRange.min, animal.race.soundCallIntervalRange.max);
            r.soundCallIntervalRange = new IntRange(pace.min, pace.max);
            r.soundCallIntervalFriendlyFactor = PokeWorldSoundsMath.SlowerFactor(was.friendly, animal.race.soundCallIntervalFriendlyFactor);
            r.soundCallIntervalAggressiveFactor = PokeWorldSoundsMath.SlowerFactor(was.aggressive, animal.race.soundCallIntervalAggressiveFactor);
            matches[d] = animal;
        }

        /// <summary>
        /// The sound at stage j, else the nearest younger stage's, else an older one's. (The game itself looks from a pawn's
        /// stage to the older ones only; the borrowed sound is set on each of the Pokémon's own stages, so it plays.)
        /// </summary>
        private static SoundDef Nearest(List<LifeStageAge> stages, int j, Func<LifeStageAge, SoundDef> get)
        {
            if (j < 0 || stages.NullOrEmpty()) return null;
            for (int k = Math.Min(j, stages.Count - 1); k >= 0; k--)
                if (stages[k] != null && get(stages[k]) != null) return get(stages[k]);
            for (int k = j + 1; k < stages.Count; k++)
                if (stages[k] != null && get(stages[k]) != null) return get(stages[k]);
            return null;
        }

        /// <summary>Every species back to PokéWorld's own.</summary>
        private static void Restore()
        {
            foreach (var kv in originals)
            {
                var r = kv.Key.race;
                r.lifeStageAges = kv.Value.stages;
                r.soundCallIntervalRange = kv.Value.interval;
                r.soundCallIntervalFriendlyFactor = kv.Value.friendly;
                r.soundCallIntervalAggressiveFactor = kv.Value.aggressive;
            }
            originals.Clear();
            matches.Clear();
            Applied = PokemonSounds.Own;
        }
    }
}
