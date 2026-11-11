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
    /// How one god is worshipped, as its own character has it (Defs/GodDefs/Rimfolk_Worship_Gods.xml;
    /// docs/gods-pantheon-brainstorm.md): what it takes and what it won't, whether it takes anything at all (Helios can't be
    /// given anything), whether worship moves it at all, whether what rots is its taking (Morel), its festival's day, name and
    /// dishes or none (a cat keeps no calendar), its chant, the vows its people make, and when it listens (Selene by night).
    /// Keyed by the god's short name (<see cref="gods"/>: "Hollin"; its defName "Rimfolk_God_Hollin" matches too). A def with
    /// only a <see cref="domain"/> speaks for every god of that domain. Anything a def leaves out falls back to the domain's
    /// own (WorshipMath). Hidden, as everything the gods are: nothing here is ever shown to the player.
    /// </summary>
    public class GodWorshipDef : Def
    {
        /// <summary>The gods it is for: short names ("Hollin"), or defNames ("Rimfolk_God_Hollin").</summary>
        public List<string> gods;
        /// <summary>Or every god of this domain (when no def names the god itself).</summary>
        public GodDomain domain = GodDomain.None;
        /// <summary>What it loves offered, best first; replaces its domains' tables.</summary>
        public List<OfferingLike> likes;
        /// <summary>What it will not take, whatever its domain likes.</summary>
        public List<OfferingKind> refuses;
        /// <summary>It takes nothing at all (Helios: he can't be given anything).</summary>
        public bool takesNoOfferings;
        /// <summary>Worship moves it not at all: no offering, prayer, rite or vow changes how it stands (Helios gives to all alike).</summary>
        public bool unmoved;
        /// <summary>What was left to rot at its place is its taking (Morel): a spoiled offering counts in full.</summary>
        public bool spoiledIsTaken;
        /// <summary>What it is given must go on (Ducat: gold given away, not kept): an offering that goes back to the stores counts for nothing.</summary>
        public bool givenAway;
        /// <summary>It never asks for anything in a dream (Mog: a full bowl she didn't ask for).</summary>
        public bool asksNothing;
        /// <summary>It keeps no festival (Mog).</summary>
        public bool noFestival;
        /// <summary>Its festival: the season (0 spring to 3 winter) and the day in it (0 to 14); -1 for the domain's.</summary>
        public int festivalSeason = -1, festivalDay = -1;
        /// <summary>The festival's name, with {GOD}: "the Longest Night".</summary>
        [MustTranslate] public string festivalName;
        /// <summary>The festival's dishes and rite in a phrase, for its words ("bread broken at the hearth").</summary>
        [MustTranslate] public string festivalDishes;
        /// <summary>The chant its rites are kept with (SoundDef names, the first that is loaded).</summary>
        public List<string> chants;
        /// <summary>The vows its people make most (the others still come, less often).</summary>
        public List<VowKind> vows;
        /// <summary>How much likelier a prayer said by night is heard (Selene: what was done by day returns at night); 1 for none.</summary>
        public float nightFactor = 1f;
        /// <summary>How much more often its people dream of it (Selene notices dreams told); 1 for none.</summary>
        public float dreamFactor = 1f;
        /// <summary>People believe it keeps the rain (Murmur's song, Cirrus's sky): it may be said to ask for something, or the rain stays away.</summary>
        public bool rainKeeper;

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
            var found = all.FirstOrDefault(d => d.gods != null && d.gods.Any(n => Names(n, g)))
                        ?? all.FirstOrDefault(d => d.gods.NullOrEmpty() && d.domain != GodDomain.None && d.domain == g.domain);
            cache[g.id] = found;
            return found;
        }

        private static bool Names(string n, God g) => !n.NullOrEmpty() && (n == g.Key || n == g.name || n == g.def?.defName || n == g.patron || n == "Rimfolk_God_" + g.name);

        /// <summary>The (kind, weight) table of what a god likes: its own def's, or null for its domains'.</summary>
        public static (OfferingKind, float)[] LikesOf(God g)
        {
            var d = For(g);
            return d?.likes.NullOrEmpty() != false ? null : d.likes.Select(l => (l.kind, l.weight)).ToArray();
        }

        /// <summary>It takes offerings at all: not an archotech or a dead god (WorshipMath), and not one whose own def says it takes nothing.</summary>
        public static bool TakesOfferings(God g) => g != null && WorshipMath.TakesOfferings(g.kind) && !(For(g)?.takesNoOfferings ?? false);

        /// <summary>Worship moves it (not one whose def says it is unmoved).</summary>
        public static bool Moved(God g) => g != null && !(For(g)?.unmoved ?? false);

        /// <summary>It keeps a festival on the colony's calendar.</summary>
        public static bool KeepsFestival(God g) => g != null && g.kind != GodKind.Dead && !(For(g)?.noFestival ?? false);

        /// <summary>How well an offering of a kind suits a god: its own likes and refusals where it has them, else its domains' (WorshipMath.Fit).</summary>
        public static float Fit(God g, OfferingKind k)
        {
            if (!TakesOfferings(g)) return 0f;
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
            if (!festivalName.NullOrEmpty() && festivalName.StartsWith(" ")) yield return "a festival's name starts with a word";
            if (takesNoOfferings && !likes.NullOrEmpty()) yield return "a god that takes nothing likes nothing";
            if (nightFactor <= 0f || dreamFactor <= 0f) yield return "night and dream factors are above zero";
        }
    }
}
