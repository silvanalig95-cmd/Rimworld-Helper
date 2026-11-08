using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>What would take a thing's count against a colonist's style away, by how the game makes and styles things.</summary>
    public enum StyleFix
    {
        /// <summary>A styled building the colony could build again, and built again now it wouldn't go against them.</summary>
        Rebuild,
        /// <summary>A styled building that, built again, would take the same wrong style (the main ideoligion's).</summary>
        NoRebuild,
        /// <summary>Another ideoligion's own building, floor or relic: taken down, replaced with a plain one, or the relic moved.</summary>
        Replace,
        /// <summary>A styled item lying about (a weapon, a piece of clothing, a sculpture not installed): carried elsewhere.</summary>
        Move
    }

    /// <summary>One thing in one cell as the game counts it (a building counts in every cell it covers), or one cell of floor.</summary>
    public struct StyleItem
    {
        /// <summary>What it is grouped by: its def and style (or its ideoligion, for another's own building or relic).</summary>
        public string key;
        public string label;
        /// <summary>The thing's id, or the cell's index for a floor: a group counts each once.</summary>
        public int id;
        /// <summary>Its points by the game's own rule: plus for their style, minus for another.</summary>
        public float points;
        public bool floor;
        public StyleFix fix;
    }

    /// <summary>The things of one kind and style around them, with how many and what they come to.</summary>
    public struct StyleGroup
    {
        public string key, label;
        /// <summary>How many things (or cells of floor) of it there are.</summary>
        public int count;
        public float points;
        public bool floor;
        public StyleFix fix;
        public List<int> ids;
    }

    /// <summary>
    /// Which things have the "wrong" style (the player's question, 8 November 2026: "how do i even know what is having the
    /// 'wrong' style? I don't see it", and "i'd appreciate if you add notes about which exact items have the wrong style, so
    /// i can target these"). The pure rules behind <see cref="StyleSurroundings"/>: the things counted around a colonist
    /// grouped by kind and style and put in order, the game's threshold for the mood, what is shown and what is left as "and
    /// N more", and which line says what would fix it. The points themselves are the game's own (IdeoUtility).
    /// </summary>
    public static class StyleSurroundingsMath
    {
        /// <summary>The game's points for the mood either way (Pawn_StyleObserverTracker.BaseDominancePointsThreshold).</summary>
        public const float BaseThreshold = 10f;
        /// <summary>The game's reach from the spot it looks from (IdeoUtility.GetStyleDominanceFromCellsCenteredOn).</summary>
        public const float Radius = 24.9f;
        /// <summary>The game looks again this often, from a random cell within <see cref="LookRadius"/> in sight of them.</summary>
        public const int LookTicks = 900, LookRadius = 5;
        /// <summary>Lines of things against them shown in the tooltip, then "and N more"; and lines for them.</summary>
        public const int MaxAgainst = 8, MaxFor = 4;
        /// <summary>Rebuildable kinds named in the fix line where only some would help.</summary>
        public const int MaxFixLabels = 3;

        /// <summary>The game's threshold: 10 points, or a room's cell count for a room under 10 cells that isn't a doorway.</summary>
        public static float Threshold(bool doorway, int cells) => !doorway && cells < BaseThreshold ? cells : BaseThreshold;

        /// <summary>The game's stage: 0 the good mood (their style dominates), 1 the bad one, -1 none.</summary>
        public static int Stage(float total, float threshold) => total >= threshold ? 0 : total <= -threshold ? 1 : -1;

        /// <summary>
        /// The things counted, grouped by kind and style: points summed over every cell they cover, each thing (or cell of
        /// floor) counted once. Nothing that comes to zero.
        /// </summary>
        public static List<StyleGroup> Group(IEnumerable<StyleItem> items)
        {
            var byKey = new Dictionary<string, StyleGroup>();
            var seen = new Dictionary<string, HashSet<int>>();
            var order = new List<string>();
            foreach (var it in items ?? Enumerable.Empty<StyleItem>())
            {
                if (it.points == 0f || it.key == null) continue;
                // Plus and minus never share a group: another ideoligion's chair and one's own aren't one kind here.
                string k = (it.points < 0f ? "-|" : "+|") + it.key;
                if (!byKey.TryGetValue(k, out var g))
                {
                    g = new StyleGroup { key = it.key, label = it.label, floor = it.floor, fix = it.fix, ids = new List<int>() };
                    seen[k] = new HashSet<int>();
                    order.Add(k);
                }
                g.points += it.points;
                if (seen[k].Add(it.id))
                {
                    g.count++;
                    g.ids.Add(it.id);
                }
                byKey[k] = g;
            }
            return order.Select(k => byKey[k]).ToList();
        }

        /// <summary>What counts against them, the biggest first (then the most of them, then by name).</summary>
        public static List<StyleGroup> Against(IEnumerable<StyleGroup> groups) =>
            (groups ?? Enumerable.Empty<StyleGroup>()).Where(g => g.points < 0f).OrderBy(g => g.points).ThenByDescending(g => g.count)
            .ThenBy(g => g.label, StringComparer.Ordinal).ToList();

        /// <summary>What counts for them, the biggest first.</summary>
        public static List<StyleGroup> For(IEnumerable<StyleGroup> groups) =>
            (groups ?? Enumerable.Empty<StyleGroup>()).Where(g => g.points > 0f).OrderByDescending(g => g.points).ThenByDescending(g => g.count)
            .ThenBy(g => g.label, StringComparer.Ordinal).ToList();

        /// <summary>The sum the game compares with the threshold.</summary>
        public static float Total(IEnumerable<StyleItem> items) => (items ?? Enumerable.Empty<StyleItem>()).Sum(i => i.points);

        /// <summary>The groups shown (at most <paramref name="max"/>), and how many are left over and what they come to.</summary>
        public static List<StyleGroup> Shown(IList<StyleGroup> groups, int max, out int moreCount, out float morePoints)
        {
            moreCount = 0;
            morePoints = 0f;
            if (groups == null) return new List<StyleGroup>();
            if (groups.Count <= max) return groups.ToList();
            var rest = groups.Skip(max).ToList();
            moreCount = rest.Count;
            morePoints = rest.Sum(g => g.points);
            return groups.Take(max).ToList();
        }

        /// <summary>A sum of points as the tooltip shows it: "+12", "-20", "-7.5".</summary>
        public static string Points(float p)
        {
            float r = (float)Math.Round(p, 1);
            string s = Math.Abs(r).ToString("0.#", CultureInfo.InvariantCulture);
            return r > 0f ? "+" + s : r < 0f ? "-" + s : "0";
        }

        /// <summary>The line's key: a floor by its cells, several things with their count, one thing alone.</summary>
        public static string LineKey(StyleGroup g) => g.floor ? "CC_StyleNote_LineFloor" : g.count > 1 ? "CC_StyleNote_LineMany" : "CC_StyleNote_LineOne";

        /// <summary>
        /// Which line says what would fix it. New buildings take the main ideoligion's style (outside classic mode there is no
        /// choice), and the game has no way to restyle a built thing: so building them again helps only where the main
        /// ideoligion's style doesn't go against theirs. Null with nothing against them.
        /// </summary>
        public static string FixKey(bool havePrimary, bool ownIsPrimary, IEnumerable<StyleFix> against)
        {
            var fixes = (against ?? Enumerable.Empty<StyleFix>()).ToList();
            if (fixes.Count == 0) return null;
            if (!havePrimary) return "CC_StyleNote_Fix_Plain";
            bool rebuild = fixes.Contains(StyleFix.Rebuild), no = fixes.Contains(StyleFix.NoRebuild);
            if (!no) return rebuild ? ownIsPrimary ? "CC_StyleNote_Fix_Own" : "CC_StyleNote_Fix_Shared" : "CC_StyleNote_Fix_Plain";
            return rebuild ? "CC_StyleNote_Fix_Some" : "CC_StyleNote_Fix_None";
        }

        /// <summary>The kinds the "some" fix line names: those built again would help, the biggest first.</summary>
        public static List<string> RebuildLabels(IEnumerable<StyleGroup> against) =>
            (against ?? Enumerable.Empty<StyleGroup>()).Where(g => g.fix == StyleFix.Rebuild).OrderBy(g => g.points).Select(g => g.label).Distinct()
            .Take(MaxFixLabels).ToList();

        /// <summary>The colonist's button shows while they have the bad mood, with the setting and the hooks on.</summary>
        public static bool ButtonShows(bool on, bool colonist, bool hasStyleThought, int stage) => on && colonist && hasStyleThought && stage == 1;

        /// <summary>A thing's own line names the colonists whose style it goes against: only for a thing that counts at all.</summary>
        public static bool ThingCounts(float styleDominanceStat) => styleDominanceStat > 0f;
    }
}
