using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One of the fourteen domains (Defs/GodDefs/Rimfolk_Pantheon_Domains.xml; docs/design/gods.md, "Domains"): what a god
    /// of it is god of in words ("goddess of the harvest"), its two colours and symbol, the names Rimfolk's makers give its
    /// gods in each age, its epithets, and its sacred things (an animal kind, a plant, a place, a deed). The animals and
    /// plants are named by defName and looked up softly, so a mod's that isn't loaded is simply not there. The look the
    /// presence part draws a god of it in is <c>Rimfolk_GodLook_&lt;Domain&gt;</c>.
    /// </summary>
    public class GodDomainDef : Def
    {
        public GodDomain domain;
        /// <summary>What a god of it is god of, after "of": "the harvest", "storms".</summary>
        [MustTranslate] public string ofWords;
        public Color colour = Color.white, colour2 = Color.gray;
        /// <summary>The symbol, white on transparent (Tools/make_god_symbols.py), tinted in the god's colours when drawn.</summary>
        public string symbol;
        /// <summary>Names by the form a god takes: a tribe's spirits, the medieval's old gods and saints, the industrial's half-forgotten and machine gods, spacers' archotechs.</summary>
        [MustTranslate] public List<string> spiritNames, oldNames, saintNames, newNames, machineNames, archotechNames;
        /// <summary>Its gods' epithets, each a noun phrase after the name ("the Bringer of Rain").</summary>
        [MustTranslate] public List<string> epithets;
        /// <summary>The animal kinds and plants sacred to it (defNames, the game's and the player's mods'; looked up softly).</summary>
        public List<string> sacredAnimals, sacredPlants;
        /// <summary>Its sacred place and deed, in a few words ("a crossroads", "a traveller sheltered").</summary>
        [MustTranslate] public string sacredPlace, sacredDeed;
        /// <summary>The name of a god of it born of the colony's own faith ("the Little Sheaf").</summary>
        [MustTranslate] public string newbornName;

        /// <summary>The look the presence part draws its gods in.</summary>
        public string Look => "Rimfolk_GodLook_" + domain;

        /// <summary>The names for gods of this form, falling back to the nearest age's.</summary>
        public List<string> NamesFor(GodForm f)
        {
            List<string> list = f switch
            {
                GodForm.Spirit => spiritNames,
                GodForm.OldGod => oldNames,
                GodForm.Saint => saintNames,
                GodForm.HalfForgotten => newNames,
                GodForm.MachineGod => machineNames,
                _ => archotechNames
            };
            if (!list.NullOrEmpty()) return list;
            return f switch
            {
                GodForm.Saint => oldNames,
                GodForm.MachineGod => newNames,
                _ => oldNames ?? newNames ?? spiritNames ?? archotechNames
            } ?? new List<string>();
        }

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
            if (symbol.NullOrEmpty()) yield return "a god domain has a symbol";
            if (spiritNames.NullOrEmpty() || oldNames.NullOrEmpty() || newNames.NullOrEmpty() || archotechNames.NullOrEmpty())
                yield return "a god domain names gods for every age (spirits, old gods, the half-forgotten, archotechs)";
            if (epithets.NullOrEmpty() || epithets.Count < 3) yield return "a god domain has at least three epithets";
            if (epithets != null && epithets.Any(x => x.NullOrEmpty() || !x.StartsWith("the "))) yield return "an epithet is a noun phrase beginning \"the \" (\"the Bringer of Rain\")";
        }
    }

    /// <summary>
    /// The patron of one or more of Rimfolk's peoples (Defs/GodDefs/Rimfolk_Pantheon_Patrons.xml; docs/design/gods.md,
    /// "The patrons of Rimfolk's peoples"), tuned to their lore: its name and epithet, its domains, its kind and nature,
    /// the form it takes (unset: its people's age's), and a line of how its people tell of it. Where a people's faith
    /// already has a god of the patron's domain, that god is the patron and keeps its faith's name.
    /// </summary>
    public class GodPatronDef : Def
    {
        public List<FactionDef> peoples = new List<FactionDef>();
        [MustTranslate] public string godName, epithet;
        /// <summary>How its people tell of it, one sentence or two (on the god's page).</summary>
        [MustTranslate] public string lore;
        public GodDomain domain, second;
        public GodKind kind = GodKind.Ordinary;
        public GodNature nature = GodNature.Patient;
        public Gender gender = Gender.None;
        /// <summary>The form it takes, by name (GodForm); unset: its first people's age's.</summary>
        public string form;

        /// <summary>The form set for it, or null for its people's age's.</summary>
        public GodForm? Form => !form.NullOrEmpty() && Enum.TryParse(form, out GodForm f) ? f : (GodForm?)null;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (peoples.NullOrEmpty()) yield return "a patron names the peoples it is patron of";
            if (godName.NullOrEmpty() || epithet.NullOrEmpty() || lore.NullOrEmpty()) yield return "a patron has a name, an epithet and a line of lore";
            if (!epithet.NullOrEmpty() && !epithet.StartsWith("the ")) yield return "a patron's epithet is a noun phrase beginning \"the \"";
            if (domain == GodDomain.None) yield return "a patron has a domain";
            if (second == domain) yield return "a patron's second domain is another";
            if (!form.NullOrEmpty() && Form == null) yield return "'" + form + "' isn't a god's form (" + string.Join(", ", Enum.GetNames(typeof(GodForm))) + ")";
        }
    }
}
