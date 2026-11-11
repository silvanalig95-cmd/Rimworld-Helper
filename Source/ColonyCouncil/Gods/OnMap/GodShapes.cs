using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What shapes a god takes on the map (Defs/GodDefs/Rimfolk_GodsOnMap_Shapes.xml; the player, 11 November 2026: "not
    /// every god necessarily has to have a sacred beast, each god should be individual in what it looks like and whether it
    /// has any form at all"): whether it can come as a stranger at the gate, whether it walks as a beast and as which, and
    /// whether it answers in the weather, and how (some gods have no body and show only in the sky). Who comes, and as
    /// what, is read from here, never decided by the code: a god named in <see cref="gods"/> (by its name, its patron's
    /// defName, its look's defName, or the defName of a hand-made god's own def) takes that shape; any other god its
    /// domain's (<see cref="domains"/>), else the default.
    /// </summary>
    public class GodShapeDef : Def
    {
        /// <summary>The particular gods this shape is for: a god's name, its patron's defName, its look's defName, or its own def's.</summary>
        public List<string> gods = new List<string>();
        /// <summary>The domains this is every god's shape for (the generic shape by domain), when no shape names the god itself.</summary>
        public List<GodDomain> domains = new List<GodDomain>();
        /// <summary>The default: the shape of a god with nothing else to go by.</summary>
        public bool isDefault;

        /// <summary>It can come as a lone traveller asking for shelter (a god in disguise).</summary>
        public bool stranger = true;
        /// <summary>It walks as a beast, and which (ThingDef names, the player's mods' first; empty: its look's own beasts).</summary>
        public bool beast = true;
        public List<string> beasts = new List<string>();
        /// <summary>It answers in the weather, with these voices (Rain, Thunder, Eclipse, Aurora; empty: its domain's).</summary>
        public bool weather = true;
        public List<WeatherVoice> voices = new List<WeatherVoice>();
        /// <summary>It has no body at all: it shows only in the sky, and answers there more readily and more often.</summary>
        public bool bodiless;
        /// <summary>A tag its own lines carry (a stranger's hints, what is said of it), so a god written by hand can have words of its own.</summary>
        public string lineTag;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (!isDefault && gods.NullOrEmpty() && domains.NullOrEmpty()) yield return "a god's shape is for some gods, some domains, or the default";
            if (bodiless && (stranger || beast)) yield return "a bodiless god comes neither as a stranger nor as a beast";
            if (voices != null && voices.Contains(WeatherVoice.None)) yield return "None isn't a weather voice";
        }
    }

    /// <summary>A god's shape as it stands now (GodShapes.Of).</summary>
    public static class GodShapes
    {
        /// <summary>For the hand-made pantheon to come: a god's shape by its own def, ahead of these defs (null: by these defs).</summary>
        public static Func<God, GodShapeDef> Override;

        private static List<GodShapeDef> All => DefDatabase<GodShapeDef>.AllDefsListForReading;

        private static readonly GodShapeDef Open = new GodShapeDef { defName = "Rimfolk_GodShape_Open", isDefault = true };

        /// <summary>The shape of a god: its own, else its domain's, else the default (everything open).</summary>
        public static GodShapeDef Of(God g)
        {
            if (g == null) return Open;
            try
            {
                var own = Override?.Invoke(g);
                if (own != null) return own;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a god's own shape", e);
            }
            var all = All;
            foreach (var s in all)
                if (s.gods != null && s.gods.Count > 0 && (s.gods.Contains(g.name) || g.patron != null && s.gods.Contains(g.patron) || g.look != null && s.gods.Contains(g.look)))
                    return s;
            foreach (var s in all)
                if (s.domains != null && s.domains.Contains(g.domain))
                    return s;
            return all.FirstOrDefault(s => s.isDefault) ?? Open;
        }

        public static bool Stranger(God g) => g != null && Of(g).stranger;

        public static bool Beast(God g) => g != null && Of(g).beast;

        public static bool Weather(God g) => g != null && Of(g).weather;

        public static bool Bodiless(God g) => g != null && Of(g).bodiless;

        /// <summary>The weather voices it uses, or null for its domain's.</summary>
        public static IList<WeatherVoice> Voices(God g)
        {
            var v = Of(g).voices;
            return v.NullOrEmpty() ? null : v;
        }

        /// <summary>The beasts its shape names that are in this game (ThingDefs), or null for its look's own.</summary>
        public static List<ThingDef> Beasts(God g)
        {
            var names = Of(g).beasts;
            if (names.NullOrEmpty()) return null;
            return names.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d?.race != null).ToList();
        }

        /// <summary>Its own lines' tag, or null.</summary>
        public static string LineTag(God g) => g == null ? null : Of(g).lineTag;
    }

    /// <summary>
    /// The gods on the map in words, at the step of how real the gods are (the player: the gods pulling the strings, named
    /// in what happens): a god the colony knows by its name, one it doesn't as what it is ("a goddess of the harvest"); in
    /// Felt "some say", in Believed "the colony believes", in Walking plainly.
    /// </summary>
    public static class GodsOnMapWords
    {
        /// <summary>{GOD}, {GODEPITHET} and {GODOF} for the god: its name where the colony knows it or it shows itself (<paramref name="named"/>), else what it is.</summary>
        public static NamedArgument[] Args(God g, bool named)
        {
            if (g == null) return new NamedArgument[0];
            if (named || g.known) return Gods.Args(g);
            string what = Gods.AGodOf(g);
            return new[] { what.Named(GodsMath.God), "".Named(GodsMath.Epithet), Gods.GodOf(g).Named(GodsMath.Of) };
        }

        /// <summary>A happening put to the god in the voice of how real the gods are: "some say {WHAT}", plain in Walking.</summary>
        public static string Voice(string what) =>
            ("CC_God_Voice_" + GodsMath.Voice(Gods.Realness)).Translate(what.Named("STANDS")).Resolve().CapitalizeFirst();

        /// <summary>What a god did, veiled ("some say ...") or not, for its acts remembered.</summary>
        public static string Veil(string what, bool veiled) => (veiled ? "CC_GodM_SomeSay".Translate(what.Named("WHAT")).Resolve() : what).CapitalizeFirst();

        /// <summary>A sign put to the god at the realness step: "Some say it is a sign of Ceren."</summary>
        public static string Sign(God g) => g == null ? "" : ("CC_GodM_Sign_" + GodsMath.Voice(Gods.Realness)).Translate(Args(g, false)).Resolve();
    }
}
