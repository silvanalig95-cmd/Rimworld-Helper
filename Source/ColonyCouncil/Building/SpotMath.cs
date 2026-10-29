using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// What makes a spot good for a room plan (Building/TemplatePlanner.FindSpots), as plain numbers: standing walls it
    /// would share, rock to mine, zones and home ground under it, how far from the colony's middle, how close to the
    /// rooms it goes with and how far from those it doesn't, the gate and the fields where those matter, and the
    /// workshops where quiet matters. The score adds them up; the reasons are the parts that count.
    /// </summary>
    public static class SpotMath
    {
        /// <summary>Within this many cells a wanted neighbour counts, and an unwanted one costs.</summary>
        public const float NearReach = 14f, AvoidReach = 12f, GateReach = 20f, FieldsReach = 16f, QuietReach = 10f;
        /// <summary>"Beside" a room: this close.</summary>
        public const float Beside = 8f;

        public struct Features
        {
            public int sharedWalls, rock, zoneCells, homeCells, thickRoof, floorCells;
            public float distCenter;
            /// <summary>Per wanted role: the nearest such room's distance (-1 none on the map); the same for unwanted roles and the workshops.</summary>
            public float[] near, avoid, noisy;
            public float distGate, distFields;
            public bool wantsGate, wantsFields, wantsQuiet;
        }

        public static float Score(Features f)
        {
            float score = f.sharedWalls * 3f - f.rock * 1.5f - f.zoneCells * 1f + f.homeCells * 0.3f - f.thickRoof * 0.5f;
            score -= f.distCenter * 0.6f;
            if (f.near != null)
                foreach (float d in f.near)
                    if (d >= 0f && d < NearReach) score += (NearReach - d) * 0.8f;
            if (f.avoid != null)
                foreach (float d in f.avoid)
                    if (d >= 0f && d < AvoidReach) score -= (AvoidReach - d) * 0.8f;
            if (f.wantsGate && f.distGate >= 0f && f.distGate < GateReach) score += (GateReach - f.distGate) * 0.5f;
            if (f.wantsFields && f.distFields >= 0f && f.distFields < FieldsReach) score += (FieldsReach - f.distFields) * 0.5f;
            if (f.wantsQuiet && f.noisy != null)
                foreach (float d in f.noisy)
                    if (d >= 0f && d < QuietReach) score -= (QuietReach - d) * 0.6f;
            return score;
        }

        /// <summary>
        /// The best few spots that don't overlap: candidates in score order, keeping each whose middle is at least
        /// <paramref name="gap"/> from every better one kept. Returns indexes into <paramref name="scores"/>.
        /// </summary>
        public static List<int> Distinct(IList<float> scores, IList<(int x, int z)> mids, float gap, int want)
        {
            var order = new List<int>();
            for (int i = 0; i < scores.Count; i++) order.Add(i);
            order.Sort((a, b) => scores[b].CompareTo(scores[a]));
            var kept = new List<int>();
            foreach (int i in order)
            {
                bool clear = true;
                foreach (int k in kept)
                {
                    int dx = mids[i].x - mids[k].x, dz = mids[i].z - mids[k].z;
                    if (dx * dx + dz * dz < gap * gap)
                    {
                        clear = false;
                        break;
                    }
                }
                if (!clear) continue;
                kept.Add(i);
                if (kept.Count >= want) break;
            }
            return kept;
        }

        /// <summary>Whether a wanted room is beside the spot, near it, or neither (-1 none on the map).</summary>
        public static int Closeness(float d) => d < 0f ? -1 : d < Beside ? 2 : d < NearReach ? 1 : 0;
    }
}
