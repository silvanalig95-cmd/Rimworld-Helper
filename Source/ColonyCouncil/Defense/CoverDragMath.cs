using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>A cell drafted colonists could take cover in, seen from where they gather (Defense/CoverDrag.cs).</summary>
    public struct CoverCell
    {
        public int x, z;
        /// <summary>Across the line they hold (cells, + to the right facing the enemy), and toward the enemy (+) or back (-).</summary>
        public float lateral, depth;
        /// <summary>The game's chance that cover here stops a shot from the enemy's side (0 to 1).</summary>
        public float cover;
        /// <summary>From here a colonist can shoot toward the enemy (leaning out counts).</summary>
        public bool sees;
    }

    /// <summary>
    /// Taking cover with a drag (Defense/CoverDrag.cs): the cell pressed is where the drafted colonists gather, the drag
    /// points at the enemy, and each colonist gets a cell near there with the most cover from that side. Shooters keep
    /// to cells they can shoot from, long guns a step back and melee fighters a step forward, spread across the line in
    /// the order they stand so nobody crosses another's path, and not on top of each other. No game types, so
    /// Tools/Tests checks it.
    /// </summary>
    public static class CoverDragMath
    {
        /// <summary>Cells between neighbors on the line; how far back and forward of the gathering cell cover is looked for.</summary>
        public const float Spacing = 2f;
        public const int RearDepth = 6, ForwardDepth = 4;

        /// <summary>Weapon reach: melee up to this, a long gun from this.</summary>
        public const float MeleeRange = 1.5f, LongRange = 25f;

        /// <summary>Score taken off a cell for each colonist already placed next to it (one grenade, two down).</summary>
        public const float NeighborPenalty = 12f;

        /// <summary>A shooter's cell they can't shoot from loses this much (as much as 60% cover is worth).</summary>
        public const float BlindPenalty = 60f;

        /// <summary>Where a cell lies from the gathering cell, along a unit direction (dx, dz) toward the enemy.</summary>
        public static (float lateral, float depth) Frame(float x, float z, float sx, float sz, float dx, float dz)
        {
            float ox = x - sx, oz = z - sz;
            return (ox * dz - oz * dx, ox * dx + oz * dz);
        }

        /// <summary>How far toward the enemy a colonist with this reach would rather stand: melee a step forward, long guns a step back.</summary>
        public static float PreferredDepth(float range) => range <= MeleeRange ? 1f : range >= LongRange ? -2f : 0f;

        /// <summary>No further forward than this: shooters stay about on the line, melee fighters a little ahead of it.</summary>
        public static float ForwardLimit(float range) => range <= MeleeRange ? ForwardDepth : 2f;

        /// <summary>Half the width of the strip looked at, for this many colonists.</summary>
        public static int HalfWidth(int count) => Math.Max(4, (int)Math.Ceiling(Math.Max(0, count - 1) * Spacing / 2f) + 3);

        /// <summary>The place on the line of the <paramref name="slot"/>th colonist of <paramref name="count"/>, from left to right.</summary>
        public static float IdealLateral(int slot, int count) => (slot - (count - 1) / 2f) * Spacing;

        /// <summary>
        /// How good a cell is for a colonist with this reach and place on the line; NegativeInfinity when it's too far
        /// forward. <paramref name="aimed"/>: there is an enemy side (else only the cover around and the place count).
        /// </summary>
        public static float Score(CoverCell c, float range, float idealLateral, bool aimed)
        {
            if (aimed && c.depth > ForwardLimit(range)) return float.NegativeInfinity;
            float s = c.cover * 100f;
            if (aimed)
            {
                if (range > MeleeRange && !c.sees) s -= BlindPenalty;
                s -= Math.Abs(c.depth - PreferredDepth(range)) * 4f;
            }
            s -= Math.Abs(c.lateral - idealLateral) * 3f;
            return s;
        }

        /// <summary>
        /// A cell for each colonist (an index into <paramref name="cells"/>, or -1 when none suits): the best pair of
        /// colonist and cell first, then the next, each cell taken once. <paramref name="ranges"/> and
        /// <paramref name="laterals"/> are each colonist's weapon reach and where they stand across the line now;
        /// <paramref name="canUse"/> says whether a colonist can get to a cell, asked only of the pairs about to be
        /// chosen.
        /// </summary>
        public static int[] Assign(IList<CoverCell> cells, IList<float> ranges, IList<float> laterals, bool aimed, Func<int, int, bool> canUse)
        {
            int n = ranges.Count, m = cells.Count;
            var result = new int[n];
            for (int i = 0; i < n; i++) result[i] = -1;
            if (n == 0 || m == 0) return result;
            // Their places on the line follow where they stand, so nobody crosses another's path.
            var order = new List<int>();
            for (int i = 0; i < n; i++) order.Add(i);
            order.Sort((a, b) => laterals[a] != laterals[b] ? laterals[a].CompareTo(laterals[b]) : a.CompareTo(b));
            var ideal = new float[n];
            for (int slot = 0; slot < n; slot++) ideal[order[slot]] = IdealLateral(slot, n);
            var taken = new bool[m];
            var takenCells = new List<int>();
            var refused = new HashSet<long>();
            int placed = 0;
            while (placed < n)
            {
                int bestPawn = -1, bestCell = -1;
                float best = float.NegativeInfinity;
                for (int p = 0; p < n; p++)
                {
                    if (result[p] >= 0) continue;
                    for (int c = 0; c < m; c++)
                    {
                        if (taken[c] || refused.Contains((long)p * m + c)) continue;
                        float s = Score(cells[c], ranges[p], ideal[p], aimed);
                        if (float.IsNegativeInfinity(s)) continue;
                        s -= Neighbors(cells, takenCells, c) * NeighborPenalty;
                        if (s > best)
                        {
                            best = s;
                            bestPawn = p;
                            bestCell = c;
                        }
                    }
                }
                if (bestPawn < 0) break;
                if (canUse != null && !canUse(bestPawn, bestCell))
                {
                    refused.Add((long)bestPawn * m + bestCell);
                    continue;
                }
                result[bestPawn] = bestCell;
                taken[bestCell] = true;
                takenCells.Add(bestCell);
                placed++;
            }
            return result;
        }

        private static int Neighbors(IList<CoverCell> cells, List<int> takenCells, int c)
        {
            int n = 0;
            var me = cells[c];
            foreach (int t in takenCells)
            {
                var o = cells[t];
                if (Math.Abs(o.x - me.x) <= 1 && Math.Abs(o.z - me.z) <= 1) n++;
            }
            return n;
        }
    }
}
