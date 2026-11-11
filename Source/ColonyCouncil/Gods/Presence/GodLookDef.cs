using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// How a god of a domain looks and comes (docs/design/gods.md, "Presence"; Defs/GodDefs/Rimfolk_GodLooks.xml): one a
    /// domain (Rimfolk_GodLook_&lt;Domain&gt;), with an age variant where the age changes it. Everything it names from the
    /// game, the DLCs and the player's mods (apparel, motes, effecters, sounds, songs, animals) is named as text and looked
    /// up softly once, so a missing mod's thing is simply skipped; GodPresenceTests checks every name against the player's
    /// mods (docs/player-mods).
    /// </summary>
    public class GodLookDef : Def
    {
        /// <summary>The domain (one of GodPresenceMath.Domains).</summary>
        public string domain;

        /// <summary>How a god of this look is called when nobody has named it (the dev actions, a moment without a name).</summary>
        public string sampleName;

        /// <summary>The ages it is for: the domain's look for every age, an age variant for a few.</summary>
        public TechLevel minTech = TechLevel.Undefined;
        public TechLevel maxTech = TechLevel.Archotech;

        /// <summary>Its two colours: the first its robes, its aura and its name; the second its crown or mask, its ring.</summary>
        public Color colorA = Color.white;
        public Color colorB = Color.white;

        /// <summary>The light that moves with it, and how far it reaches.</summary>
        public Color glowColor = Color.white;
        public float glowRadius = 6f;

        /// <summary>How much larger it is drawn than its kind.</summary>
        public float size = 1.25f;

        /// <summary>A person made a god's: their skin, hair and the light in their eyes.</summary>
        public Color skin = Color.white;
        public Color hair = Color.white;
        public Color eyes = Color.white;

        /// <summary>An animal made a god's: its whole body tinted this.</summary>
        public Color animalTint = Color.white;

        /// <summary>What it wears, in the order tried: a robe, cloak or mantle, then a crown, hood or mask (the player's mods' first).</summary>
        public List<string> body = new List<string>();
        public List<string> head = new List<string>();

        /// <summary>What it leaves where it walks.</summary>
        public GodTrail trail = GodTrail.None;

        /// <summary>What rises off it: motes (A RimWorld of Magic's, or the game's flecks) by name; Rimfolk's own when none is there.</summary>
        public List<string> motes = new List<string>();

        /// <summary>The flash of its coming: effecters by name, triggered where it stands.</summary>
        public List<string> flash = new List<string>();

        /// <summary>The circle on the ground at its coming: motes by name.</summary>
        public List<string> circle = new List<string>();

        /// <summary>Spirits that run out from it at its coming (motes drawn facing a way: a name ending East runs east).</summary>
        public List<string> spirits = new List<string>();

        /// <summary>The colour its presence gives the sky, and how far the wind dies (0 not at all, 1 dead still).</summary>
        public Color sky = Color.white;
        public float still = 0.85f;

        /// <summary>What it leaves on the land: after a coming or a blessing, and after a wrath.</summary>
        public GodMark mark = GodMark.None;
        public GodMark wrathMark = GodMark.Scorch;

        /// <summary>The sounds of its moments by name (one-shots or sustainers, played as each is): the sting, a wrath's, an omen's, a departure's, and its hum while near.</summary>
        public List<string> sting = new List<string>();
        public List<string> wrathSting = new List<string>();
        public List<string> omen = new List<string>();
        public List<string> depart = new List<string>();
        public List<string> hum = new List<string>();

        /// <summary>Its tracks by SongDef name, and a wrath's darker ones.</summary>
        public List<string> tracks = new List<string>();
        public List<string> darkTracks = new List<string>();

        /// <summary>The animals it walks as (the player's mods' first).</summary>
        public List<string> animals = new List<string>();

        // ---------------- Read once, softly ----------------

        [Unsaved] private bool resolved;
        [Unsaved] private List<ThingDef> bodyDefs, headDefs, animalDefs;
        [Unsaved] private List<Def> moteDefs, circleDefs, spiritDefs;
        [Unsaved] private List<EffecterDef> flashDefs;

        public string Domain => domain ?? "";

        public GodRgb A => Rgb(colorA);

        public GodRgb B => Rgb(colorB);

        public static GodRgb Rgb(Color c) => new GodRgb(c.r, c.g, c.b);

        public static Color ColorOf(GodRgb c, float a = 1f) => new Color(c.r, c.g, c.b, a);

        private void Resolve()
        {
            if (resolved) return;
            resolved = true;
            bodyDefs = Things(body, d => d.IsApparel);
            headDefs = Things(head, d => d.IsApparel);
            animalDefs = Things(animals, d => d.race != null);
            moteDefs = MoteDefs(motes);
            circleDefs = MoteDefs(circle);
            spiritDefs = MoteDefs(spirits);
            flashDefs = (flash ?? new List<string>()).Select(n => DefDatabase<EffecterDef>.GetNamedSilentFail(n)).Where(d => d != null).ToList();
        }

        private static List<ThingDef> Things(List<string> names, Func<ThingDef, bool> fits) =>
            (names ?? new List<string>()).Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null && fits(d)).ToList();

        /// <summary>A mote by name: a fleck of the game's, or a mote thing (A RimWorld of Magic's are).</summary>
        public static Def Mote(string name)
        {
            if (name.NullOrEmpty()) return null;
            var f = DefDatabase<FleckDef>.GetNamedSilentFail(name);
            if (f != null) return f;
            var t = DefDatabase<ThingDef>.GetNamedSilentFail(name);
            return t != null && t.mote != null ? t : null;
        }

        private static List<Def> MoteDefs(List<string> names) => (names ?? new List<string>()).Select(Mote).Where(d => d != null).ToList();

        /// <summary>Its robes, cloaks and mantles in the player's game, in order.</summary>
        public List<ThingDef> BodyApparel
        {
            get
            {
                Resolve();
                return bodyDefs;
            }
        }

        public List<ThingDef> HeadApparel
        {
            get
            {
                Resolve();
                return headDefs;
            }
        }

        /// <summary>The animals it walks as in the player's game, the player's mods' first (stage 2's sacred animal reads these).</summary>
        public List<ThingDef> AnimalKinds
        {
            get
            {
                Resolve();
                return animalDefs;
            }
        }

        public List<Def> Motes
        {
            get
            {
                Resolve();
                return moteDefs;
            }
        }

        public List<Def> Circles
        {
            get
            {
                Resolve();
                return circleDefs;
            }
        }

        public List<Def> Spirits
        {
            get
            {
                Resolve();
                return spiritDefs;
            }
        }

        public List<EffecterDef> Flashes
        {
            get
            {
                Resolve();
                return flashDefs;
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (!GodPresenceMath.IsDomain(domain)) yield return "unknown domain '" + domain + "'";
            if (minTech > maxTech) yield return "minTech after maxTech";
            if (size < 1f || size > GodPresenceMath.MaxSize) yield return "size out of 1 to " + GodPresenceMath.MaxSize;
            if (sampleName.NullOrEmpty()) yield return "no sampleName";
        }
    }

    /// <summary>The gods' looks: which one a domain takes in an age, and by name (the seam's wiring calls these).</summary>
    public static class GodLooks
    {
        public static List<GodLookDef> All => DefDatabase<GodLookDef>.AllDefsListForReading;

        /// <summary>A look by its defName, or null.</summary>
        public static GodLookDef Named(string defName) => defName.NullOrEmpty() ? null : DefDatabase<GodLookDef>.GetNamedSilentFail(defName);

        /// <summary>The look of a domain in an age (an age variant where there is one), or null for an unknown domain.</summary>
        public static GodLookDef For(string domain, TechLevel age)
        {
            var all = All;
            int i = GodPresenceMath.Choose(all.Select(l => (l.Domain, (int)l.minTech, (int)l.maxTech)).ToList(), domain, (int)age);
            return i >= 0 ? all[i] : null;
        }

        /// <summary>The look of a domain in the colony's own age.</summary>
        public static GodLookDef For(string domain) => For(domain, Avail.EffectiveTech);
    }
}
