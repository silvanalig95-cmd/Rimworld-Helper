using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One thing a god likes offered, and how much (1 a fair gift, 1.5 a loved one).</summary>
    public class OfferingLike
    {
        public OfferingKind kind;
        public float weight = 1f;
    }

    /// <summary>
    /// How one god is worshipped, as its own character says (Defs/GodDefs/Rimfolk_Worship_Gods.xml): what it loves offered and
    /// what it refuses, its festival's day and name and the dishes and rite it is kept with, the chant, the vows it is
    /// most often made. A def names the gods it is for (<see cref="gods"/>: a god's name, or the defName of the patron or the
    /// hand-made god it is); a def with only a <see cref="domain"/> speaks for every god of that domain. Anything a def leaves
    /// out falls back to the domain's own (WorshipMath). The hand-made pantheon (docs/gods-pantheon-brainstorm.md) fills
    /// these per god; the generated one has none, and worship runs on the domains' tables.
    /// </summary>
    public class GodWorshipDef : Def
    {
        /// <summary>The gods it is for: names, or the defName of the god's patron or hand-made def (any matches).</summary>
        public List<string> gods;
        /// <summary>Or every god of this domain (when no def names the god itself).</summary>
        public GodDomain domain = GodDomain.None;
        /// <summary>What it loves offered, best first; replaces its domains' tables.</summary>
        public List<OfferingLike> likes;
        /// <summary>What it will not take, whatever its domain likes.</summary>
        public List<OfferingKind> refuses;
        /// <summary>Its festival: the season (0 spring to 3 winter) and the day in it (0 to 14); -1 for the domain's.</summary>
        public int festivalSeason = -1, festivalDay = -1;
        /// <summary>The festival's name, with {GOD}: "the Longest Night of {GOD}".</summary>
        [MustTranslate] public string festivalName;
        /// <summary>The festival's dishes and rite in a phrase, for its words ("bread broken at the hearth").</summary>
        [MustTranslate] public string festivalDishes;
        /// <summary>The chant its rites are kept with (SoundDef names, the first that is loaded).</summary>
        public List<string> chants;
        /// <summary>The vows its people make most (the others still come, less often).</summary>
        public List<VowKind> vows;

        private static Dictionary<int, GodWorshipDef> cache;
        private static int cacheVersion = -1;

        /// <summary>The worship def for a god: one naming it, else one for its domain, else null (the domain's tables).</summary>
        public static GodWorshipDef For(God g)
        {
            if (g == null) return null;
            if (cache == null || cacheVersion != Gods.Version)
            {
                cache = new Dictionary<int, GodWorshipDef>();
                cacheVersion = Gods.Version;
            }
            if (cache.TryGetValue(g.id, out var have)) return have;
            var all = DefDatabase<GodWorshipDef>.AllDefsListForReading;
            var found = all.FirstOrDefault(d => d.gods != null && d.gods.Any(n => !n.NullOrEmpty() && (n == g.name || n == g.patron)))
                        ?? all.FirstOrDefault(d => d.gods.NullOrEmpty() && d.domain != GodDomain.None && d.domain == g.domain);
            cache[g.id] = found;
            return found;
        }

        /// <summary>The (kind, weight) table of what a god likes: its own def's, or null for its domains'.</summary>
        public static (OfferingKind, float)[] LikesOf(God g)
        {
            var d = For(g);
            return d?.likes.NullOrEmpty() != false ? null : d.likes.Select(l => (l.kind, l.weight)).ToArray();
        }

        /// <summary>How well an offering of a kind suits a god: its own likes and refusals where it has them, else its domains' (WorshipMath.Fit).</summary>
        public static float Fit(God g, OfferingKind k)
        {
            if (g == null) return 0f;
            var d = For(g);
            if (d?.refuses != null && d.refuses.Contains(k)) return 0f;
            var own = LikesOf(g);
            return own != null ? WorshipMath.FitFrom(own, k, g.kind) : WorshipMath.Fit(k, g.domain, g.second, g.kind);
        }

        /// <summary>What a god likes, best first: its own, else its domain's.</summary>
        public static OfferingKind[] Likes(God g)
        {
            var own = LikesOf(g);
            return own != null ? own.Select(x => x.Item1).ToArray() : WorshipMath.Likes(g?.domain ?? GodDomain.None);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (gods.NullOrEmpty() && domain == GodDomain.None) yield return "a god's worship names the gods it is for, or a domain";
            if (festivalSeason > 3 || festivalDay > 14) yield return "a festival's season is 0 to 3 and its day 0 to 14";
            if (!festivalName.NullOrEmpty() && !festivalName.Contains("{GOD}")) yield return "a festival's name says {GOD}";
        }
    }
}
