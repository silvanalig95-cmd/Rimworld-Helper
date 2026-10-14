using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// How much of a material the colony has on a map: stored (what the resource counter sees in stockpiles and on
    /// shelves) and lying around (spawned, not forbidden, not in storage). Used by the build menu's material list and
    /// the room planner. Remembered for a second so menus drawn every frame don't walk the map each time.
    /// </summary>
    public static class MaterialCounts
    {
        private struct Entry
        {
            public int stored, lying;
            public float time;
        }

        private static readonly Dictionary<(Map, ThingDef), Entry> cache = new Dictionary<(Map, ThingDef), Entry>();
        private static Game cacheGame;

        private static Entry Get(Map map, ThingDef def)
        {
            if (map == null || def == null) return default;
            // Another game was loaded: forget the old one's maps instead of keeping them in memory.
            if (cacheGame != Current.Game)
            {
                cache.Clear();
                cacheGame = Current.Game;
            }
            float now = Time.realtimeSinceStartup;
            if (cache.TryGetValue((map, def), out var e) && now - e.time < 1f && now >= e.time) return e;
            if (cache.Count > 600) cache.Clear();
            e = new Entry { time = now };
            bool counted = def.CountAsResource;
            if (counted)
            {
                try
                {
                    e.stored = map.resourceCounter.GetCount(def);
                }
                catch
                {
                    counted = false;
                }
            }
            var player = Faction.OfPlayerSilentFail;
            var things = map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < things.Count; i++)
            {
                var t = things[i];
                if (t == null || !t.Spawned || (player != null && t.IsForbidden(player))) continue;
                if (t.IsInAnyStorage())
                {
                    if (!counted) e.stored += t.stackCount;
                    continue;
                }
                if (t.Position.Fogged(map)) continue;
                e.lying += t.stackCount;
            }
            cache[(map, def)] = e;
            return e;
        }

        public static int Stored(Map map, ThingDef def) => Get(map, def).stored;

        public static int LyingAround(Map map, ThingDef def) => Get(map, def).lying;

        /// <summary>Stored plus lying around: what builders can actually fetch.</summary>
        public static int Have(Map map, ThingDef def)
        {
            var e = Get(map, def);
            return e.stored + e.lying;
        }

        /// <summary>"420" or "420 (+85 lying around)".</summary>
        public static string Short(Map map, ThingDef def)
        {
            var e = Get(map, def);
            return e.lying > 0 ? "CC_Mat_StoredAndLying".Translate(e.stored, e.lying).Resolve() : e.stored.ToString();
        }

        /// <summary>How much of <paramref name="stuff"/> one <paramref name="def"/> takes (adjusted for how bulky the material is).</summary>
        public static int PerUnit(BuildableDef def, ThingDef stuff)
        {
            if (def == null || stuff == null) return 0;
            try
            {
                foreach (var c in def.CostListAdjusted(stuff, false))
                    if (c.thingDef == stuff) return c.count;
            }
            catch
            {
            }
            return def.CostStuffCount;
        }

        /// <summary>How many of <paramref name="def"/> the colony could make from what it has (every ingredient counted). -1 if it costs nothing.</summary>
        public static int EnoughFor(Map map, BuildableDef def, ThingDef stuff)
        {
            if (map == null || def == null) return -1;
            int best = int.MaxValue;
            try
            {
                foreach (var c in def.CostListAdjusted(stuff, false))
                {
                    if (c.thingDef == null || c.count <= 0) continue;
                    best = Mathf.Min(best, Have(map, c.thingDef) / c.count);
                }
            }
            catch
            {
                return -1;
            }
            return best == int.MaxValue ? -1 : best;
        }

        /// <summary>The note added to a material in the build menu: "420 (+85 lying around) · enough for 84".</summary>
        public static string MenuNote(Map map, BuildableDef def, ThingDef stuff)
        {
            string note = Short(map, stuff);
            int n = EnoughFor(map, def, stuff);
            if (n >= 0) note += "CC_Mat_EnoughFor".Translate(n).Resolve();
            return note;
        }
    }

    /// <summary>What a material is being chosen for: walls never get bone, and fire matters more for walls and doors.</summary>
    public enum MaterialUse
    {
        Wall,
        Door,
        Floor,
        Furniture
    }

    /// <summary>
    /// Chooses the material for one group of a room plan from what the colony actually has: something there's enough of
    /// (with a little to spare), common and cheap before rare and precious (stone blocks, wood, bone, then steel; silver,
    /// gold, plasteel, uranium and jade only when nothing else will do), and the larger stock on a tie.
    /// </summary>
    public static class MaterialPicker
    {
        /// <summary>Have this much more than needed to count as comfortably enough.</summary>
        public const float Margin = 1.15f;

        private static readonly Dictionary<(Map, BuildableDef, int, MaterialUse), (float time, ThingDef pick)> memo = new Dictionary<(Map, BuildableDef, int, MaterialUse), (float, ThingDef)>();

        /// <summary>How well what the colony has covers <paramref name="units"/> pieces: 2 = with room to spare, 1 = only just, 0 = not enough.</summary>
        public static int Coverage(BuildableDef def, ThingDef stuff, int units, Map map)
        {
            if (def == null || stuff == null || map == null) return 0;
            int need = Mathf.Max(1, units) * MaterialCounts.PerUnit(def, stuff);
            int have = MaterialCounts.Have(map, stuff);
            if (have >= Mathf.CeilToInt(need * Margin)) return 2;
            return have >= need ? 1 : 0;
        }

        public static bool Affordable(BuildableDef def, ThingDef stuff, int units, Map map) => Coverage(def, stuff, units, map) > 0;

        public static bool IsBone(ThingDef s)
        {
            if (s == null || !s.IsStuff) return false;
            if (s.stuffProps?.categories != null && (s.stuffProps.categories.Contains(StuffCategoryDefOf.Fabric) || s.stuffProps.categories.Contains(StuffCategoryDefOf.Leathery)))
                return false;
            return (s.defName != null && s.defName.IndexOf("bone", System.StringComparison.OrdinalIgnoreCase) >= 0)
                   || (s.label != null && s.label.IndexOf("bone", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>Silver, gold, plasteel, uranium, jade and anything worth a lot per unit: kept for trade, not for walls and chairs.</summary>
        public static bool IsPrecious(ThingDef s)
        {
            if (s == null) return false;
            if (s == ThingDefOf.Silver || s == ThingDefOf.Gold || s == ThingDefOf.Plasteel || s == ThingDefOf.Uranium || s == ThingDefOf.Jade) return true;
            return s.BaseMarketValue >= 4f;
        }

        private static bool Has(ThingDef s, StuffCategoryDef cat) => cat != null && s?.stuffProps?.categories != null && s.stuffProps.categories.Contains(cat);

        /// <summary>Lower is better: stone blocks, wood, bone, steel and other metals, cloth and leather, the rest.</summary>
        public static int Commonness(ThingDef s, BuildableDef def, MaterialUse use)
        {
            if (IsPrecious(s)) return 9;
            if (IsBone(s)) return 2;
            if (Has(s, StuffCategoryDefOf.Stony)) return 0;
            if (Has(s, StuffCategoryDefOf.Woody)) return 1;
            if (s == ThingDefOf.Steel) return 3;
            if (Has(s, StuffCategoryDefOf.Metallic)) return 4;
            if (Has(s, StuffCategoryDefOf.Fabric) || Has(s, StuffCategoryDefOf.Leathery))
                return HardStuffWorks(def) ? 6 : 0;
            return 5;
        }

        private static readonly Dictionary<BuildableDef, bool> hardWorks = new Dictionary<BuildableDef, bool>();

        /// <summary>Can this be made of wood, stone or metal (so cloth and leather are a waste on it)?</summary>
        private static bool HardStuffWorks(BuildableDef def)
        {
            if (def == null) return false;
            if (!hardWorks.TryGetValue(def, out bool ok))
            {
                ok = Avail.AllowedStuffs(def).Any(s => Has(s, StuffCategoryDefOf.Stony) || Has(s, StuffCategoryDefOf.Woody) || Has(s, StuffCategoryDefOf.Metallic));
                hardWorks[def] = ok;
            }
            return ok;
        }

        /// <summary>Bone makes furniture, doors and floors, never walls.</summary>
        public static bool Allowed(ThingDef s, MaterialUse use) => !(IsBone(s) && use == MaterialUse.Wall);

        /// <summary>
        /// The best material for <paramref name="units"/> pieces of <paramref name="def"/> (or floor tiles), or the game's
        /// default. <paramref name="reserved"/> is what the rest of the same plan already uses of each material (walls,
        /// floor, other furniture), so two parts of one room don't count on the same blocks.
        /// </summary>
        public static ThingDef Pick(BuildableDef def, Map map, int units, MaterialUse use, Dictionary<ThingDef, int> reserved = null)
        {
            if (def == null || !def.MadeFromStuff) return null;
            if (map == null) return GenStuff.DefaultStuffFor(def);
            units = Mathf.Max(1, units);
            if (reserved != null && reserved.Count > 0) return Choose(def, map, units, use, reserved) ?? Choose(def, map, units, use, null) ?? Fallback(def, use);
            if (memoGame != Current.Game)
            {
                memo.Clear();
                memoGame = Current.Game;
            }
            float now = Time.realtimeSinceStartup;
            var key = (map, def, units, use);
            if (memo.TryGetValue(key, out var m) && now - m.time < 1.5f && now >= m.time) return m.pick;
            if (memo.Count > 400) memo.Clear();
            var pick = Choose(def, map, units, use, null) ?? Fallback(def, use);
            memo[key] = (now, pick);
            return pick;
        }

        private static Game memoGame;

        /// <summary>
        /// Nothing in stock at all: the game's default material if it suits the use, else the first that does. The game's
        /// own pick throws for a modded building whose stuff categories no loaded material belongs to; then null.
        /// </summary>
        private static ThingDef Fallback(BuildableDef def, MaterialUse use)
        {
            ThingDef d = null;
            try
            {
                d = GenStuff.DefaultStuffFor(def);
            }
            catch
            {
                // No material can make it.
            }
            if (d != null && Allowed(d, use) && Avail.StuffAllowed(def, d)) return d;
            return Avail.AllowedStuffs(def).FirstOrDefault(s => Allowed(s, use)) ?? d;
        }

        /// <summary>Adds what <paramref name="units"/> of <paramref name="def"/> in <paramref name="stuff"/> cost to <paramref name="reserved"/>.</summary>
        public static void Reserve(Dictionary<ThingDef, int> reserved, BuildableDef def, ThingDef stuff, int units)
        {
            if (reserved == null || def == null || units <= 0 || (def.MadeFromStuff && stuff == null)) return;
            try
            {
                foreach (var c in def.CostListAdjusted(stuff, false))
                {
                    if (c.thingDef == null) continue;
                    reserved.TryGetValue(c.thingDef, out int n);
                    reserved[c.thingDef] = n + c.count * units;
                }
            }
            catch
            {
                // A modded cost list that can't be read: nothing reserved for it.
            }
        }

        /// <summary>
        /// The tiers, best first: a common material with room to spare, a common one that only just covers it, a precious
        /// one that covers it, then (nothing covers it) the common one the colony has most of, then the precious one.
        /// Within a tier: stone blocks, wood, bone, steel and so on, then the cheaper, then the larger stock.
        /// </summary>
        private static ThingDef Choose(BuildableDef def, Map map, int units, MaterialUse use, Dictionary<ThingDef, int> reserved)
        {
            ThingDef best = null;
            (int tier, int rank, float cost, int have) bestScore = default;
            foreach (var s in Avail.AllowedStuffs(def))
            {
                if (!Allowed(s, use)) continue;
                int have = MaterialCounts.Have(map, s);
                if (reserved != null && reserved.TryGetValue(s, out int used)) have -= used;
                if (have <= 0) continue;
                int per = Mathf.Max(1, MaterialCounts.PerUnit(def, s));
                int need = units * per;
                int cover = have >= Mathf.CeilToInt(need * Margin) ? 2 : have >= need ? 1 : 0;
                bool precious = IsPrecious(s);
                int tier = cover == 2 && !precious ? 0 : cover == 1 && !precious ? 1 : cover > 0 ? 2 : !precious ? 3 : 4;
                // Without enough of anything, the larger stock decides.
                var score = tier >= 3 ? (tier, 0, 0f, have) : (tier, Commonness(s, def, use), s.BaseMarketValue * need, have);
                if (best == null || Better(score, bestScore))
                {
                    best = s;
                    bestScore = score;
                }
            }
            return best;
        }

        private static bool Better((int tier, int rank, float cost, int have) a, (int tier, int rank, float cost, int have) b)
        {
            if (a.tier != b.tier) return a.tier < b.tier;
            if (a.rank != b.rank) return a.rank < b.rank;
            if (Mathf.Abs(a.cost - b.cost) > 0.5f) return a.cost < b.cost;
            return a.have > b.have;
        }

        /// <summary>Green with room to spare, amber if it only just covers it, red if there isn't enough.</summary>
        public static Color CoverageColor(int coverage) =>
            coverage >= 2 ? new Color(0.55f, 0.9f, 0.55f) : coverage == 1 ? new Color(1f, 0.78f, 0.35f) : new Color(1f, 0.45f, 0.45f);
    }
}
