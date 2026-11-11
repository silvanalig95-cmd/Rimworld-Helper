using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A god's ways in the world (Defs/GodDefs/Rimfolk_GodsWorld_Ways.xml): what it gives, how its anger shows, the price it
    /// asks, the holy place its people seek, its feast's name, its side in a quarrel of the sky, and the tags its myths are
    /// told by. Keyed on the god (<see cref="gods"/>: a god's own key, its patron's defName or its name; the hand-made gods
    /// to come name theirs here), else on a domain (<see cref="domain"/>); anything a def leaves out falls back to the
    /// domain's generic ways (GodWorldMath). Read through <see cref="GodWays"/>.
    /// </summary>
    public class GodWaysDef : Def
    {
        /// <summary>The gods it is for, by key (GodWays.Keys); empty: every god of <see cref="domain"/>.</summary>
        public List<string> gods = new List<string>();
        public GodDomain domain = GodDomain.None;
        /// <summary>A RimWorld of Magic class trait it grants, first the likeliest.</summary>
        public List<string> classes;
        /// <summary>A RimWorld of Magic spell scroll (SpellOf_*) it gives one who casts.</summary>
        public List<string> spells;
        /// <summary>A Vanilla Psycasts Expanded path it opens.</summary>
        public List<string> paths;
        /// <summary>The beasts it gives (PawnKindDef names), and the beasts its anger sends.</summary>
        public List<string> beasts, wrathBeasts;
        /// <summary>Its blessing on a body (a HediffDef name).</summary>
        public string hand;
        /// <summary>The shape of its anger (GodWrath), and the prices it asks (GodPrice), any of them.</summary>
        public GodWrath wrath = GodWrath.None;
        public List<GodPrice> prices;
        /// <summary>The holy place its people seek on another's land (tree, water, wreck, grave, stone).</summary>
        public string site;
        /// <summary>Its feast's name in its people's mouths ("the Night of Open Doors").</summary>
        [MustTranslate] public string feast;
        /// <summary>Its side in a quarrel of the sky: "storm" (rain and thunder on its days) or "dry" (a clear sky); none: it doesn't quarrel in the weather.</summary>
        public string weather;
        /// <summary>The tags its myths are told by, beside its domain's (Rimfolk_Pool_GodMyth, Rimfolk_Pool_GodWorld): "godkey:varrak".</summary>
        public List<string> mythTags;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (gods.NullOrEmpty() && domain == GodDomain.None) yield return "a god's ways name the gods or the domain they are for";
            if (site != null && !GodWorldMath.SiteKinds.Contains(site)) yield return "site must be one of " + string.Join(", ", GodWorldMath.SiteKinds);
            if (weather != null && weather != "storm" && weather != "dry") yield return "weather must be storm or dry";
        }
    }

    /// <summary>
    /// A god's ways, by the god first (a GodWaysDef that names it), then its domain's def, then its domain's generic ways
    /// (GodWorldMath), then its second domain's. Every part of the gods in the world reads what a god does through here,
    /// so a hand-made god's def can give it ways of its own without code.
    /// </summary>
    public static class GodWays
    {
        private static List<GodWaysDef> all;

        private static List<GodWaysDef> All => all ??= DefDatabase<GodWaysDef>.AllDefsListForReading.ToList();

        /// <summary>The keys a god answers to in the ways defs: its patron's defName and its own name (lower case).</summary>
        public static IEnumerable<string> Keys(God g)
        {
            if (g == null) yield break;
            if (!g.patron.NullOrEmpty()) yield return g.patron;
            if (!g.name.NullOrEmpty()) yield return g.name.ToLowerInvariant();
        }

        /// <summary>The defs that speak for this god, its own first, then its domain's, then its second domain's.</summary>
        public static IEnumerable<GodWaysDef> For(God g)
        {
            if (g == null) yield break;
            var keys = Keys(g).ToList();
            foreach (var d in All)
                if (!d.gods.NullOrEmpty() && d.gods.Any(keys.Contains))
                    yield return d;
            foreach (var d in All)
                if (d.gods.NullOrEmpty() && d.domain == g.domain)
                    yield return d;
            if (g.second == GodDomain.None) yield break;
            foreach (var d in All)
                if (d.gods.NullOrEmpty() && d.domain == g.second)
                    yield return d;
        }

        private static List<string> First(God g, Func<GodWaysDef, List<string>> pick, Func<GodDomain, string[]> fallback)
        {
            var own = For(g).Select(pick).FirstOrDefault(l => !l.NullOrEmpty());
            if (own != null) return own;
            var list = fallback(g.domain).ToList();
            if (list.Count == 0 && g.second != GodDomain.None) list = fallback(g.second).ToList();
            return list;
        }

        public static List<string> Classes(God g) => g == null ? new List<string>() : First(g, d => d.classes, x => GodWorldMath.ClassesFor(x, g.second));

        public static List<string> Spells(God g) => g == null ? new List<string>() : First(g, d => d.spells, GodWorldMath.SpellsFor);

        public static List<string> Paths(God g) => g == null ? new List<string>() : First(g, d => d.paths, GodWorldMath.PathsFor);

        public static List<string> Beasts(God g) => g == null ? new List<string>() : First(g, d => d.beasts, GodWorldMath.BeastsFor);

        public static List<string> WrathBeasts(God g) => g == null ? new List<string>() : First(g, d => d.wrathBeasts, GodWorldMath.WrathBeasts);

        /// <summary>Its blessing on a body: its own hediff, else its domain's (Rimfolk_GodGift_&lt;Domain&gt;).</summary>
        public static string Hand(God g) => g == null ? null : For(g).Select(d => d.hand).FirstOrDefault(h => !h.NullOrEmpty()) ?? GodWorldMath.HandOf(g.domain);

        /// <summary>The shape of its anger: its own, else its domain's by its nature (a patient or cold god waits), else its second domain's.</summary>
        public static GodWrath Wrath(God g)
        {
            if (g == null) return GodWrath.None;
            var own = For(g).Select(d => d.wrath).FirstOrDefault(w => w != GodWrath.None);
            // A patient or cold god never strikes, whatever its ways: it waits.
            if (own != GodWrath.None) return g.nature == GodNature.Patient || g.nature == GodNature.Cold ? GodWrath.None : own;
            var w0 = GodWorldMath.WrathOf(g.domain, g.nature);
            return w0 != GodWrath.None ? w0 : GodWorldMath.WrathOf(g.second, g.nature);
        }

        /// <summary>The price it asks: one of its own, by the roll, else by its nature and kind (GodWorldMath.PriceOf).</summary>
        public static GodPrice Price(God g, float roll)
        {
            var own = For(g).Select(d => d.prices).FirstOrDefault(l => !l.NullOrEmpty());
            if (own != null) return own[Math.Min(own.Count - 1, (int)(GodWorldMath.Clamp(roll, 0f, 0.9999f) * own.Count))];
            return GodWorldMath.PriceOf(g.nature, g.kind, g.domain, roll);
        }

        /// <summary>The holy place its people seek: its own, else its domain's, else its second domain's.</summary>
        public static string Site(God g) =>
            g == null ? null : For(g).Select(d => d.site).FirstOrDefault(s => !s.NullOrEmpty()) ?? GodWorldMath.SiteKind(g.domain) ?? GodWorldMath.SiteKind(g.second);

        /// <summary>Its feast's name: its own, else its domain's words (CC_GW_Feast_&lt;Domain&gt;).</summary>
        public static string Feast(God g) =>
            g == null ? "" : For(g).Select(d => d.feast).FirstOrDefault(f => !f.NullOrEmpty()) ?? GodWorld.T("CC_GW_Feast_" + g.domain, g);

        /// <summary>Its side in a quarrel of the sky: its own, else "storm" for the storm's, "dry" for the fields', the waters' and the hearth's.</summary>
        public static string Weather(God g)
        {
            if (g == null) return null;
            var own = For(g).Select(d => d.weather).FirstOrDefault(w => !w.NullOrEmpty());
            if (own != null) return own;
            if (g.domain == GodDomain.Storm || g.second == GodDomain.Storm) return "storm";
            return GodWorldMath.WeatherQuarrel(GodDomain.Storm, g.domain) || GodWorldMath.WeatherQuarrel(GodDomain.Storm, g.second) ? "dry" : null;
        }

        /// <summary>The tags its lines and myths are told by beyond its domain's: its own (godkey:...).</summary>
        public static IEnumerable<string> Tags(God g) => g == null ? Enumerable.Empty<string>() : For(g).Where(d => d.mythTags != null).SelectMany(d => d.mythTags).Distinct();
    }
}
