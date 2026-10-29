using System;

namespace ColonyCouncil
{
    /// <summary>
    /// The stores looked over (Building/StorageReview.cs) as plain rules: a stockpile that takes everything, goods
    /// lying about that no store takes, a bench far from the store of its materials, one kind of goods stored in many
    /// rooms, a store that takes nothing, and where a new little stockpile fits beside a bench.
    /// </summary>
    public static class StorageReviewMath
    {
        /// <summary>A stockpile allowing this share of everything storable takes everything, as far as sorting goes.</summary>
        public const float CatchAllShare = 0.8f;
        /// <summary>Beyond this many cells from a bench to the nearest store of its materials, every bill starts with a walk.</summary>
        public const float FarBench = 20f;
        /// <summary>One kind of goods accepted by stores in this many rooms or more scatters.</summary>
        public const int SpreadRooms = 3;

        public static bool IsCatchAll(int allowed, int storable) => storable > 0 && allowed >= storable * CatchAllShare;

        /// <summary>Two catch-all stockpiles are a muddle; more are worse, up to a point.</summary>
        public static float CatchAllScore(int zones) => zones < 2 ? 0f : Math.Min(0.3f, 0.12f + (zones - 2) * 0.05f);

        public static float UnstoredScore(int count) => count <= 0 ? 0f : Math.Min(0.4f, 0.08f + count / 200f);

        public static float BenchFarScore(float distance) => distance <= FarBench ? 0f : Math.Min(0.35f, 0.12f + (distance - FarBench) / 100f);

        public static float SpreadScore(int rooms) => rooms < SpreadRooms ? 0f : Math.Min(0.25f, 0.1f + (rooms - SpreadRooms) * 0.03f);

        public static float EmptyScore(int stores) => stores <= 0 ? 0f : Math.Min(0.15f, 0.06f + (stores - 1) * 0.02f);

        /// <summary>
        /// The square of <paramref name="size"/> free cells nearest the target, within <paramref name="radius"/> of it,
        /// by its bottom-left corner. False when none fits. The target itself is never free, so no block covers it.
        /// </summary>
        public static bool BestBlock(Func<(int x, int z), bool> free, (int x, int z) target, int radius, int size, out (int x, int z) corner)
        {
            corner = (0, 0);
            float best = float.MaxValue;
            bool found = false;
            for (int x = target.x - radius - size + 1; x <= target.x + radius; x++)
            {
                for (int z = target.z - radius - size + 1; z <= target.z + radius; z++)
                {
                    bool ok = true;
                    for (int dx = 0; dx < size && ok; dx++)
                        for (int dz = 0; dz < size && ok; dz++)
                        {
                            var c = (x + dx, z + dz);
                            if (c == target || !free(c)) ok = false;
                        }
                    if (!ok) continue;
                    float cx = x + (size - 1) / 2f, cz = z + (size - 1) / 2f;
                    float d = (cx - target.x) * (cx - target.x) + (cz - target.z) * (cz - target.z);
                    if (d < best)
                    {
                        best = d;
                        corner = (x, z);
                        found = true;
                    }
                }
            }
            return found;
        }
    }
}
