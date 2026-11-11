using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One of the fourteen domains (Defs/GodDefs/Rimfolk_Pantheon_Domains.xml; docs/design/gods.md, "Domains"), kept as the
    /// stage 2 parts' fallback table only: each of the nine is a def of its own (GodDef), and a domain is just the key a
    /// part falls back on where it has nothing written for a god. What a god of it is god of in words, its two colours, its
    /// sacred things (an animal kind, a plant, a place, a deed). The animals and plants are named by defName and looked up
    /// softly, so a mod's that isn't loaded is simply not there.
    /// </summary>
    public class GodDomainDef : Def
    {
        public GodDomain domain;
        /// <summary>What a god of it is god of, after "of": "the harvest", "storms".</summary>
        [MustTranslate] public string ofWords;
        public Color colour = Color.white, colour2 = Color.gray;
        /// <summary>The animal kinds and plants sacred to it (defNames, the game's and the player's mods'; looked up softly).</summary>
        public List<string> sacredAnimals, sacredPlants;
        /// <summary>Its sacred place and deed, in a few words ("a crossroads", "a traveler sheltered").</summary>
        [MustTranslate] public string sacredPlace, sacredDeed;

        private static Dictionary<GodDomain, GodDomainDef> byDomain;

        /// <summary>The def of a domain, or null.</summary>
        public static GodDomainDef Of(GodDomain d)
        {
            if (byDomain == null)
            {
                byDomain = new Dictionary<GodDomain, GodDomainDef>();
                foreach (var def in DefDatabase<GodDomainDef>.AllDefsListForReading)
                    if (def != null && def.domain != GodDomain.None)
                        byDomain[def.domain] = def;
            }
            return byDomain.TryGetValue(d, out var x) ? x : null;
        }

        private List<ThingDef> animals;

        /// <summary>The sacred animal kinds that are loaded.</summary>
        public List<ThingDef> Animals => animals ??= (sacredAnimals ?? new List<string>()).Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n))
            .Where(d => d?.race != null).ToList();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (domain == GodDomain.None) yield return "a god domain names its domain";
            if (ofWords.NullOrEmpty()) yield return "a god domain says what its gods are gods of (ofWords)";
        }
    }
}
