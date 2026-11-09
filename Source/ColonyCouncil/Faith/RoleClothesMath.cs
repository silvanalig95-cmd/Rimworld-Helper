using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// Whether the colony can reach a piece of clothing an ideoligion's role asks for (the player, 9 November 2026: "i keep
    /// getting toga as mandatory gear but it's not really a standard research or anything worth spending 1200 research
    /// points into... across several playthroughs and ideologies already"). Free of game types, so Tools/Tests try it;
    /// Faith/RoleClothes.cs reads the game into it.
    /// <para>
    /// The rule. A piece is out of reach when the colony has none and every way to make it (each recipe at each bench or
    /// spot that does it) needs a research project that is all three of:
    /// <list type="bullet">
    /// <item>not finished (before the game begins: not one the colony's start gives it);</item>
    /// <item>off the usual path: in a research tab of its own, not the game's main tab, or one of a modded start's own
    /// projects (it carries the start tag of a start from a mod, such as VFE Classical's ClassicalStart, that is neither the
    /// colony's own start nor one of the game's own starts);</item>
    /// <item>dear for its age: it costs at least half again what a project of its age usually costs on the main tab (the
    /// median there; a neolithic project usually costs 500, so 750 or more is dear, and VFE Classical's togas cost 1200).</item>
    /// </list>
    /// A piece the ideoligion calls for (a meme or a desired-apparel precept asks for it, or its culture, a meme, a style
    /// or the role itself comes from the mod that adds the piece, or a style has its own look for it) is kept all the same.
    /// A piece nothing makes (bought or found only) is not judged: the rule is about research. A requirement several pieces
    /// meet is in reach when any of them is.
    /// </para>
    /// </summary>
    public static class RoleClothesMath
    {
        /// <summary>A project is dear for its age at this many times what its age usually costs on the main tab.</summary>
        public const float DearFactor = 1.5f;

        /// <summary>What an age usually costs when no main-tab project of any age gives a median.</summary>
        public const float FallbackTypical = 1000f;

        /// <summary>The game's tech levels as numbers (TechLevel's own values).</summary>
        public const int Undefined = 0, Animal = 1, Neolithic = 2, Medieval = 3, Industrial = 4, Spacer = 5, Ultra = 6, Archotech = 7;

        /// <summary>One research project as the rule reads it.</summary>
        public sealed class Research
        {
            public string name;
            /// <summary>Its tech level (TechLevel as a number).</summary>
            public int age;
            /// <summary>Its base cost, compared with its age's usual cost.</summary>
            public float cost;
            /// <summary>The cost the research screen shows the colony.</summary>
            public float shownCost;
            /// <summary>How much of it is done, 0 to 1.</summary>
            public float progress;
            public bool finished;
            /// <summary>On the game's main research tab.</summary>
            public bool mainTab;
            /// <summary>It carries a start tag of the colony's own start, of one of the game's own starts, of a start from a mod.</summary>
            public bool ownStart, gameStart, modStart;

            /// <summary>What is left of it, in the cost the research screen shows.</summary>
            public float Left => finished ? 0f : Math.Max(0f, shownCost) * (1f - Clamp01(progress));
        }

        /// <summary>One way to make a piece: a recipe at a bench or spot, and every research that takes.</summary>
        public sealed class Way
        {
            public List<Research> research = new List<Research>();
            /// <summary>The bench or spot it is made at, as the text names it.</summary>
            public string bench;
            /// <summary>The colony has that bench or spot built (or it is a spot, which needs nothing).</summary>
            public bool benchBuilt;
        }

        /// <summary>One piece of clothing as the rule reads it.</summary>
        public sealed class Item
        {
            public string name;
            public List<Way> ways = new List<Way>();
            /// <summary>How many the colony has (worn by its people, or not forbidden on its maps).</summary>
            public int has;
            /// <summary>The ideoligion calls for it (see the class summary).</summary>
            public bool calledFor;
        }

        /// <summary>How the colony can come by a piece, best first: the order the dialog lists them in.</summary>
        public enum Reach
        {
            /// <summary>The colony has one.</summary>
            Has,
            /// <summary>It can be made now: every research of a way is finished.</summary>
            Ready,
            /// <summary>It needs research the colony can reach.</summary>
            Research,
            /// <summary>Nothing makes it: bought or found only; not judged.</summary>
            Unmade,
            /// <summary>Every way is out of reach, but the ideoligion calls for it, so it is kept.</summary>
            CalledFor,
            /// <summary>Out of reach.</summary>
            OutOfReach
        }

        public static bool Reachable(Reach r) => r != Reach.OutOfReach;

        // ---------------- Each age's usual cost ----------------

        /// <summary>The median of some costs (0 for none).</summary>
        public static float Median(IEnumerable<float> costs)
        {
            var list = costs?.Where(c => c > 0f).OrderBy(c => c).ToList() ?? new List<float>();
            if (list.Count == 0) return 0f;
            int mid = list.Count / 2;
            return list.Count % 2 == 1 ? list[mid] : (list[mid - 1] + list[mid]) / 2f;
        }

        /// <summary>Each age's usual cost: the median of the main tab's projects of that age.</summary>
        public static Dictionary<int, float> Medians(IEnumerable<(int age, float cost)> mainTab)
        {
            var result = new Dictionary<int, float>();
            if (mainTab == null) return result;
            foreach (var g in mainTab.Where(p => p.cost > 0f).GroupBy(p => p.age))
            {
                float m = Median(g.Select(p => p.cost));
                if (m > 0f) result[g.Key] = m;
            }
            return result;
        }

        /// <summary>
        /// What a project of an age usually costs: its age's median on the main tab; an age without one takes the nearest
        /// age below that has one, else the nearest above, else <see cref="FallbackTypical"/>.
        /// </summary>
        public static float Typical(int age, IDictionary<int, float> medians)
        {
            if (medians != null && medians.Count > 0)
            {
                if (medians.TryGetValue(age, out float own) && own > 0f) return own;
                for (int a = age - 1; a >= Undefined; a--)
                    if (medians.TryGetValue(a, out float below) && below > 0f)
                        return below;
                for (int a = age + 1; a <= Archotech; a++)
                    if (medians.TryGetValue(a, out float above) && above > 0f)
                        return above;
            }
            return FallbackTypical;
        }

        // ---------------- The rule ----------------

        /// <summary>Off the usual path: a tab of its own, or a modded start's own project the colony didn't start with.</summary>
        public static bool OffPath(Research r) => r != null && (!r.mainTab || r.modStart && !r.ownStart && !r.gameStart);

        /// <summary>Dear for its age: at least <see cref="DearFactor"/> times what its age usually costs on the main tab.</summary>
        public static bool Dear(Research r, IDictionary<int, float> medians) => r != null && r.cost > 0f && r.cost >= DearFactor * Typical(r.age, medians);

        /// <summary>A project that puts a way out of reach: not finished, off the usual path, and dear for its age.</summary>
        public static bool Blocks(Research r, IDictionary<int, float> medians) => r != null && !r.finished && OffPath(r) && Dear(r, medians);

        /// <summary>A way that needs a project out of reach.</summary>
        public static bool Blocked(Way w, IDictionary<int, float> medians) => w?.research != null && w.research.Any(r => Blocks(r, medians));

        /// <summary>A way open now: every research it needs is finished.</summary>
        public static bool Open(Way w) => w != null && (w.research == null || w.research.All(r => r == null || r.finished));

        /// <summary>What is left of a way's research, in the cost the research screen shows.</summary>
        public static float Left(Way w) => w?.research == null ? 0f : w.research.Where(r => r != null).Sum(r => r.Left);

        /// <summary>How the colony can come by a piece (see <see cref="Reach"/>).</summary>
        public static Reach Of(Item item, IDictionary<int, float> medians)
        {
            if (item == null) return Reach.Unmade;
            if (item.has > 0) return Reach.Has;
            var ways = item.ways?.Where(w => w != null).ToList() ?? new List<Way>();
            if (ways.Count == 0) return Reach.Unmade;
            if (ways.Any(Open)) return Reach.Ready;
            if (ways.Any(w => !Blocked(w, medians))) return Reach.Research;
            return item.calledFor ? Reach.CalledFor : Reach.OutOfReach;
        }

        /// <summary>A requirement several pieces meet: the best reach among them (in reach when any piece is).</summary>
        public static Reach Best(IEnumerable<Reach> reaches)
        {
            var best = Reach.OutOfReach;
            bool any = false;
            if (reaches != null)
                foreach (var r in reaches)
                {
                    if (!any || r < best) best = r;
                    any = true;
                }
            return any ? best : Reach.Unmade;
        }

        /// <summary>
        /// The way to tell of: an open way at a bench the colony has built, then any open way, then a way in reach with the
        /// least research left, then the way out of reach with the least left. -1 for none.
        /// </summary>
        public static int BestWay(Item item, IDictionary<int, float> medians)
        {
            if (item?.ways == null || item.ways.Count == 0) return -1;
            int best = -1;
            (int rank, float left) bestKey = (int.MaxValue, float.MaxValue);
            for (int i = 0; i < item.ways.Count; i++)
            {
                var w = item.ways[i];
                if (w == null) continue;
                int rank = Open(w) ? (w.benchBuilt ? 0 : 1) : Blocked(w, medians) ? 3 : 2;
                float left = Left(w);
                if (rank < bestKey.rank || rank == bestKey.rank && left < bestKey.left)
                {
                    best = i;
                    bestKey = (rank, left);
                }
            }
            return best;
        }

        /// <summary>The research a way still needs, the most left first.</summary>
        public static List<Research> Missing(Way w) =>
            w?.research == null ? new List<Research>() : w.research.Where(r => r != null && !r.finished).OrderByDescending(r => r.Left).ThenBy(r => r.name, StringComparer.Ordinal).ToList();

        /// <summary>The projects that put the item out of reach, for the dialog's tooltip (each once).</summary>
        public static List<Research> Blockers(Item item, IDictionary<int, float> medians)
        {
            var list = new List<Research>();
            if (item?.ways == null) return list;
            foreach (var w in item.ways)
            {
                if (w?.research == null) continue;
                foreach (var r in w.research)
                    if (Blocks(r, medians) && !list.Contains(r))
                        list.Add(r);
            }
            return list;
        }

        // ---------------- The role's pool, as the game draws from it ----------------

        /// <summary>
        /// The pool the game draws a role's clothes from first (its non-desperate pass), with what is out of reach left out:
        /// the indices kept, in order. Left out only there: when what is left can't fill the role, the game draws again from
        /// everything (its desperate pass), so a role is never left short.
        /// </summary>
        public static List<int> Kept(IList<Reach> pool)
        {
            var kept = new List<int>();
            if (pool == null) return kept;
            for (int i = 0; i < pool.Count; i++)
                if (Reachable(pool[i]))
                    kept.Add(i);
            return kept;
        }

        // ---------------- The dialog's order ----------------

        /// <summary>One alternative in the dialog: its reach, the research left on its best way, its name, and whether it can be picked.</summary>
        public struct Choice
        {
            public Reach reach;
            public float left;
            public string name;
            public bool allowed;

            public Choice(Reach reach, float left, string name, bool allowed = true)
            {
                this.reach = reach;
                this.left = left;
                this.name = name;
                this.allowed = allowed;
            }
        }

        /// <summary>
        /// The dialog's order: those in reach first and those that can be picked before those that can't, then by reach (the
        /// colony has one, can be made now, needs research, nothing makes it, kept by the ideoligion), then the least research
        /// left, then by name; out of reach last. The indices, in that order.
        /// </summary>
        public static List<int> Order(IList<Choice> choices)
        {
            var idx = new List<int>();
            if (choices == null) return idx;
            for (int i = 0; i < choices.Count; i++) idx.Add(i);
            return idx.OrderBy(i => Reachable(choices[i].reach) ? 0 : 1)
                .ThenBy(i => choices[i].allowed ? 0 : 1)
                .ThenBy(i => (int)choices[i].reach)
                .ThenBy(i => choices[i].left)
                .ThenBy(i => choices[i].name ?? "", StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>A share done in whole percent, never 100 before it is finished and never 0 once begun.</summary>
        public static int Percent(float progress, bool finished)
        {
            if (finished) return 100;
            float p = Clamp01(progress);
            if (p <= 0f) return 0;
            int pct = (int)Math.Floor(p * 100f);
            return Math.Max(1, Math.Min(99, pct));
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
