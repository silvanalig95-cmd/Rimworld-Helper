using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A god's ways in the world (Defs/GodDefs/Rimfolk_GodsWorld_Ways.xml): what it gives and to whom, what it asks in return,
    /// how it comes in a dream, how its kindness and its anger show, the beasts that are its kin, the holy place its people
    /// seek, its feast's name, its side in a quarrel the colony sees, the gods that turn from a bargain with it, and the tags
    /// its words are told by. All of it is hidden: the player meets only what it does. Keyed on the god by its short name
    /// (<see cref="gods"/>: "Hollin"; the nine of docs/gods-pantheon-brainstorm.md), else on a domain (<see cref="domain"/>);
    /// anything a def leaves out falls back to the domain's generic ways (GodWorldMath). Read through <see cref="GodWays"/>.
    /// </summary>
    public class GodWaysDef : Def
    {
        /// <summary>The gods it is for, by short name ("Hollin"); empty: every god of <see cref="domain"/>.</summary>
        public List<string> gods = new List<string>();
        public GodDomain domain = GodDomain.None;
        /// <summary>The kinds of gift it gives, any of them (GodGiftKind); unset: whatever can be given. "None" alone: no gifts.</summary>
        public List<GodGiftKind> gifts;
        /// <summary>Whom it gives to: "kindest" (the kind, then the best at talk), "youngest", "oldest", "nightowl", "any", "sensitive" (the most psychically sensitive), a skill's defName (the best at it); unset: its domain's skill.</summary>
        public string chooses;
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
        /// <summary>The shape of its anger at a home (GodWrath), of a grudge it holds against one colonist (Lightning or Beasts), and of its kindness (GodBlessing).</summary>
        public GodWrath wrath = GodWrath.None, grudge = GodWrath.None;
        public GodBlessing blessing = GodBlessing.None;
        /// <summary>Its own temper: the mean days between its acts of anger while the colony stands displeased (0: by its nature).</summary>
        public float wrathDays;
        /// <summary>The beasts that are its kin (ThingDef names): harmed by the colony, its anger is the shape <see cref="kinWrath"/> (Shunned), else a grudge.</summary>
        public List<string> kin;
        public GodWrath kinWrath = GodWrath.None;
        /// <summary>The prices it asks (GodPrice), any of them, and what its offering is: "silver" (the default), "meals", "rawfood", or a thing's defName.</summary>
        public List<GodPrice> prices;
        public string offering;
        /// <summary>The holy place its people seek on another's land (tree, water, wreck, grave, stone, geyser).</summary>
        public string site;
        /// <summary>Its feast's name in its people's mouths ("the Greening").</summary>
        [MustTranslate] public string feast;
        /// <summary>Its side in a quarrel the colony sees (GodWorldMath.Sides): storm, dry, cold, warm, sun, rain in the sky; growth, rot, spark, flow, hoard on the ground; more sides in <see cref="sides"/>.</summary>
        public string side;
        public List<string> sides;
        /// <summary>It watches the colony's debts (Rimfolk's favours owed): a debt the colony owed, kept or broken, moves its standing (Ducat).</summary>
        public bool debts;
        /// <summary>It gives blindly (Helios): its kindness and its too-much-giving come at its own rate, whatever the colony's standing with it.</summary>
        public bool blind;
        /// <summary>It returns the day at night (Selene): its kindness and its anger answer the colony's deeds of the day, by its face, not its standing.</summary>
        public bool mirror;
        /// <summary>The shapes its anger may take, any of them (in place of <see cref="wrath"/> alone).</summary>
        public List<GodWrath> wraths;
        /// <summary>Its dreams come true: the dream that comes before its gift tells the gift (Selene).</summary>
        public bool trueDreams;
        /// <summary>The gods that turn away from a colony that bargains with it, and the ones that go quiet a while, by short name (the Hunger's).</summary>
        public List<string> shuns, quiets;
        /// <summary>How it comes in a dream, as the dreamer tells it at breakfast, without its name ("a woman of green leaves stood at the foot of the bed").</summary>
        [MustTranslate] public string dream;
        /// <summary>More tags its lines and myths are told by, beside its domain's and its own key (Rimfolk_Pool_GodMyth, Rimfolk_Pool_GodWorld; godkey:hollin is added for a god's own ways).</summary>
        public List<string> mythTags;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (gods.NullOrEmpty() && domain == GodDomain.None) yield return "a god's ways name the gods or the domain they are for";
            if (site != null && !GodWorldMath.SiteKinds.Contains(site)) yield return "site must be one of " + string.Join(", ", GodWorldMath.SiteKinds);
            if (side != null && !GodWorldMath.Sides.Contains(side)) yield return "side must be one of " + string.Join(", ", GodWorldMath.Sides);
            if (sides != null && sides.Any(x => !GodWorldMath.Sides.Contains(x))) yield return "sides must each be one of " + string.Join(", ", GodWorldMath.Sides);
            if (grudge != GodWrath.None && grudge != GodWrath.Lightning && grudge != GodWrath.Beasts) yield return "a grudge is Lightning or Beasts";
            if (wrath == GodWrath.Shunned) yield return "Shunned is the anger of kin harmed (kinWrath), not of a home";
            if (!kin.NullOrEmpty() && kinWrath == GodWrath.None) yield return "kin needs kinWrath";
            if (blind && mirror) yield return "a god gives blindly or returns, not both";
        }
    }

    /// <summary>
    /// A god's ways, by the god first (a GodWaysDef that names it by its short name), then its domain's def, then its domain's
    /// generic ways (GodWorldMath), then its second domain's. Every part of the gods in the world reads what a god does through
    /// here, so each of the nine acts as it is.
    /// </summary>
    public static class GodWays
    {
        private static List<GodWaysDef> all;

        private static List<GodWaysDef> All => all ??= DefDatabase<GodWaysDef>.AllDefsListForReading.ToList();

        /// <summary>The key a god answers to in the ways defs: its short name.</summary>
        public static string Key(God g) => g?.name;

        private static bool Names(GodWaysDef d, God g) => !d.gods.NullOrEmpty() && g?.name != null && d.gods.Any(n => string.Equals(n, g.name, StringComparison.OrdinalIgnoreCase));

        /// <summary>The defs that speak for this god, its own first, then its domain's, then its second domain's.</summary>
        public static IEnumerable<GodWaysDef> For(God g)
        {
            if (g == null) yield break;
            foreach (var d in All)
                if (Names(d, g))
                    yield return d;
            foreach (var d in All)
                if (d.gods.NullOrEmpty() && d.domain == g.domain)
                    yield return d;
            if (g.second == GodDomain.None) yield break;
            foreach (var d in All)
                if (d.gods.NullOrEmpty() && d.domain == g.second)
                    yield return d;
        }

        /// <summary>A god's own def, if it has one (not its domain's).</summary>
        public static GodWaysDef Own(God g) => g == null ? null : All.FirstOrDefault(d => Names(d, g));

        /// <summary>A god of the pantheon by its short name, or null (the nine by name; any god of that name).</summary>
        public static God Named(string name) => name.NullOrEmpty() ? null : Gods.All.FirstOrDefault(g => string.Equals(g.name, name, StringComparison.OrdinalIgnoreCase));

        private static List<string> First(God g, Func<GodWaysDef, List<string>> pick, Func<GodDomain, string[]> fallback)
        {
            var own = For(g).Select(pick).FirstOrDefault(l => l != null);
            if (own != null) return own;
            var list = fallback(g.domain).ToList();
            if (list.Count == 0 && g.second != GodDomain.None) list = fallback(g.second).ToList();
            return list;
        }

        /// <summary>Whether it gives this kind of gift: its own list where it has one, else any.</summary>
        public static bool Gives(God g, GodGiftKind k)
        {
            var own = For(g).Select(d => d.gifts).FirstOrDefault(l => l != null);
            return own == null ? k != GodGiftKind.Passion && k != GodGiftKind.Hoard : own.Contains(k);
        }

        public static string Chooses(God g) => For(g).Select(d => d.chooses).FirstOrDefault(c => !c.NullOrEmpty());

        public static List<string> Classes(God g) => g == null ? new List<string>() : First(g, d => d.classes, x => GodWorldMath.ClassesFor(x, g.second));

        public static List<string> Spells(God g) => g == null ? new List<string>() : First(g, d => d.spells, GodWorldMath.SpellsFor);

        public static List<string> Paths(God g) => g == null ? new List<string>() : First(g, d => d.paths, GodWorldMath.PathsFor);

        public static List<string> Beasts(God g) => g == null ? new List<string>() : First(g, d => d.beasts, GodWorldMath.BeastsFor);

        public static List<string> WrathBeasts(God g) => g == null ? new List<string>() : First(g, d => d.wrathBeasts, GodWorldMath.WrathBeasts);

        /// <summary>Its blessing on a body: its own hediff, else its domain's (Rimfolk_GodGift_&lt;Domain&gt;).</summary>
        public static string Hand(God g) => g == null ? null : For(g).Select(d => d.hand).FirstOrDefault(h => !h.NullOrEmpty()) ?? GodWorldMath.HandOf(g.domain);

        /// <summary>The shape of its anger at a home: its own (none where its ways name none), else its domain's by its nature (a patient or cold god waits), else its second domain's.</summary>
        public static GodWrath Wrath(God g)
        {
            if (g == null) return GodWrath.None;
            // A god of its own ways with no anger named has none at a home (Bellwether, Ennu, Undertow).
            if (Own(g) is GodWaysDef mine) return mine.wrath;
            var own = For(g).Select(d => d.wrath).FirstOrDefault(w => w != GodWrath.None);
            if (own != GodWrath.None) return own;
            var w0 = GodWorldMath.WrathOf(g.domain, g.nature);
            return w0 != GodWrath.None ? w0 : GodWorldMath.WrathOf(g.second, g.nature);
        }

        /// <summary>The shape of a grudge it holds against one colonist: its own, else its anger's where that is lightning or beasts, else none.</summary>
        public static GodWrath Grudge(God g)
        {
            if (g == null) return GodWrath.None;
            var own = For(g).Select(d => d.grudge).FirstOrDefault(w => w != GodWrath.None);
            if (own != GodWrath.None) return own;
            var w0 = Wrath(g);
            return w0 == GodWrath.Lightning || w0 == GodWrath.Beasts ? w0 : GodWrath.None;
        }

        /// <summary>Its own temper (mean days between its acts of anger), or 0 for its nature's.</summary>
        public static float WrathDays(God g) => For(g).Select(d => d.wrathDays).FirstOrDefault(x => x > 0f);

        /// <summary>The beasts that are its kin (ThingDef names), and the shape of its anger when they are harmed.</summary>
        public static List<string> Kin(God g) => For(g).Select(d => d.kin).FirstOrDefault(l => l != null) ?? new List<string>();

        public static GodWrath KinWrath(God g) => For(g).Select(d => d.kinWrath).FirstOrDefault(w => w != GodWrath.None);

        /// <summary>How it comes in a dream, unnamed: its own words, else plain ones (CC_GW_Dream_Plain).</summary>
        public static string Dream(God g) => For(g).Select(d => d.dream).FirstOrDefault(x => !x.NullOrEmpty()) ?? "CC_GW_Dream_Plain".Translate().Resolve();

        /// <summary>The shape of its kindness: its own (none where its ways name none), else a fever eased for a god of healing.</summary>
        public static GodBlessing Blessing(God g)
        {
            if (g == null) return GodBlessing.None;
            if (Own(g) is GodWaysDef mine) return mine.blessing;
            var own = For(g).Select(d => d.blessing).FirstOrDefault(b => b != GodBlessing.None);
            if (own != GodBlessing.None) return own;
            return g.domain == GodDomain.Healing || g.second == GodDomain.Healing ? GodBlessing.Fever : GodBlessing.None;
        }

        /// <summary>The price it asks: one of its own, by the roll, else by its nature and kind (GodWorldMath.PriceOf).</summary>
        public static GodPrice Price(God g, float roll)
        {
            var own = For(g).Select(d => d.prices).FirstOrDefault(l => !l.NullOrEmpty());
            if (own != null) return own[Math.Min(own.Count - 1, (int)(GodWorldMath.Clamp(roll, 0f, 0.9999f) * own.Count))];
            return GodWorldMath.PriceOf(g.nature, g.kind, g.domain, roll);
        }

        /// <summary>What its offering is: silver, meals, raw food, or a thing by its defName.</summary>
        public static string Offering(God g) => For(g).Select(d => d.offering).FirstOrDefault(o => !o.NullOrEmpty()) ?? "silver";

        /// <summary>The holy place its people seek: its own, else its domain's, else its second domain's.</summary>
        public static string Site(God g)
        {
            if (g == null) return null;
            var own = Own(g);
            // A god of its own ways with no holy place named has none (Bellwether walks; the Guest is at the door).
            if (own != null) return own.site;
            return For(g).Select(d => d.site).FirstOrDefault(s => !s.NullOrEmpty()) ?? GodWorldMath.SiteKind(g.domain) ?? GodWorldMath.SiteKind(g.second);
        }

        /// <summary>Its feast's name: its own, else its domain's words (CC_GW_Feast_&lt;Domain&gt;).</summary>
        public static string Feast(God g) =>
            g == null ? "" : For(g).Select(d => d.feast).FirstOrDefault(f => !f.NullOrEmpty()) ?? GodWorld.T("CC_GW_Feast_" + g.domain, g);

        /// <summary>Its sides in the quarrels the colony sees: its own (Smoulder warm against Hush, the hoard against Ducat), else its domain's one.</summary>
        public static List<string> Sides(God g)
        {
            var list = new List<string>();
            if (g == null) return list;
            var own = Own(g);
            if (own != null)
            {
                if (own.side != null) list.Add(own.side);
                if (own.sides != null) list.AddRange(own.sides.Where(x => x != null && !list.Contains(x)));
                return list;
            }
            var s = Side(g);
            if (s != null) list.Add(s);
            return list;
        }

        /// <summary>It gives blindly (Helios), or it returns the day (Selene).</summary>
        public static bool Blind(God g) => For(g).Any(d => d.blind);

        public static bool Mirror(God g) => For(g).Any(d => d.mirror);

        /// <summary>The shapes its anger may take: its list, else its one shape.</summary>
        public static List<GodWrath> Wraths(God g)
        {
            var own = For(g).Select(d => d.wraths).FirstOrDefault(l => !l.NullOrEmpty());
            if (own != null) return own.ToList();
            var w = Wrath(g);
            return w == GodWrath.None ? new List<GodWrath>() : new List<GodWrath> { w };
        }

        /// <summary>Whether its dreams come true (Selene).</summary>
        public static bool TrueDreams(God g) => For(g).Any(d => d.trueDreams);

        /// <summary>Whether it watches the colony's debts (Ducat).</summary>
        public static bool Debts(God g) => For(g).Any(d => d.debts);

        /// <summary>Its first side in a quarrel the colony sees: its own, else "storm" for the storm's, "dry" for the fields', the waters' and the hearth's.</summary>
        public static string Side(God g)
        {
            if (g == null) return null;
            var own = Own(g);
            if (own != null) return own.side ?? own.sides?.FirstOrDefault();
            if (g.domain == GodDomain.Storm || g.second == GodDomain.Storm) return "storm";
            return GodWorldMath.WeatherQuarrel(GodDomain.Storm, g.domain) || GodWorldMath.WeatherQuarrel(GodDomain.Storm, g.second) ? "dry" : null;
        }

        /// <summary>The gods that turn away from a colony that bargains with this one, and the ones that go quiet a while.</summary>
        public static List<God> Shuns(God g) => GodsIn(g, d => d.shuns);

        public static List<God> Quiets(God g) => GodsIn(g, d => d.quiets);

        private static List<God> GodsIn(God g, Func<GodWaysDef, List<string>> pick) =>
            For(g).Select(pick).Where(l => l != null).SelectMany(l => l).Select(Named).Where(x => x != null && x != g).Distinct().ToList();

        /// <summary>The tags its lines and myths are told by beyond its domain's: its own key (godkey:hollin) where it has ways of its own, and any more its ways name.</summary>
        public static IEnumerable<string> Tags(God g)
        {
            if (g == null) return Enumerable.Empty<string>();
            var tags = For(g).Where(d => d.mythTags != null).SelectMany(d => d.mythTags).ToList();
            if (Own(g) != null && g.name != null) tags.Insert(0, "godkey:" + g.name.ToLowerInvariant());
            return tags.Distinct();
        }
    }
}
