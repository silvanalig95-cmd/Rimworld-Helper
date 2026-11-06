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
    /// - <see cref="PokemonSounds.Creatures"/> (the default): each species borrows the calls of the nearest animal of the
    ///   game or another mod (the same body plan and flesh, the nearest size: <see cref="PokeWorldSoundsMath.Distance"/>), its
    ///   idle call, its angry call, its cries when wounded and dying, its attack sound, and that animal's pace between calls.
    /// - <see cref="PokemonSounds.Rarer"/>: their own cries, a third as often.
    /// - <see cref="PokemonSounds.Own"/>: as PokéWorld has them.
    /// The game plays an animal's calls from its race's life stages (Pawn_CallTracker, LifeStageUtility), so this changes
    /// def data only, no hook: each species gets its own copies of its life stages with the borrowed sounds, and the
    /// originals are put back when the setting changes (applied at startup and when the settings window closes). Nothing
    /// is saved. With PokéWorld not loaded, nothing happens.
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
                    int given = 0;
                    foreach (var d in PokeWorldLink.AllSpecies)
                    {
                        var m = Match(d, animals);
                        if (m == null) continue;
                        Keep(d);
                        Borrow(d, m);
                        given++;
                    }
                    if (Prefs.DevMode || given == 0)
                        Log.Message("[Rimfolk] PokéWorld: " + given + " species sound like animals now, from " + animals.Count + " to choose from"
                                    + (matches.Count > 0 ? " (" + matches.Take(5).Select(kv => kv.Key.label + " like a " + kv.Value.label).ToCommaList() + ")." : "."));
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

        /// <summary>Animals with a voice of their own: not Pokémon, not mechanoids, dryads or the void's creatures.</summary>
        private static List<ThingDef> Candidates()
        {
            var list = new List<ThingDef>();
            foreach (var d in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                var r = d.race;
                if (r == null || !r.Animal || r.Humanlike || r.IsMechanoid || r.Dryad || PokeWorldLink.IsPokemon(d)) continue;
                if (r.lifeStageAges.NullOrEmpty() || !r.lifeStageAges.Any(l => l?.soundCall != null)) continue;
                list.Add(d);
            }
            return list;
        }

        /// <summary>The nearest animal: the same body plan and flesh, the nearest size; ties go to the def name, so it's the same each time.</summary>
        private static ThingDef Match(ThingDef pokemon, List<ThingDef> animals)
        {
            var r = pokemon.race;
            ThingDef best = null;
            float bestDistance = float.MaxValue;
            foreach (var a in animals)
            {
                float d = PokeWorldSoundsMath.Distance(a.race.body == r.body, a.race.FleshType == r.FleshType, r.baseBodySize, a.race.baseBodySize);
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
            r.soundCallIntervalRange = animal.race.soundCallIntervalRange;
            r.soundCallIntervalFriendlyFactor = animal.race.soundCallIntervalFriendlyFactor;
            r.soundCallIntervalAggressiveFactor = animal.race.soundCallIntervalAggressiveFactor;
            matches[d] = animal;
        }

        /// <summary>The sound at stage j, else the nearest younger stage's, else an older one's (as the game looks for one).</summary>
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
