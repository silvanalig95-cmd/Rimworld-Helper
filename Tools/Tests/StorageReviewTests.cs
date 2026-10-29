using System;
using System.Collections.Generic;
using ColonyCouncil;

/// <summary>The stores looked over (Building/StorageReviewMath): catch-alls, goods no store takes, benches far from their materials, spread, and where a new stockpile fits.</summary>
public static class StorageReviewTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the stores looked over: " + what);
        }

        C(StorageReviewMath.IsCatchAll(900, 1000) && !StorageReviewMath.IsCatchAll(500, 1000) && !StorageReviewMath.IsCatchAll(5, 0), "a stockpile allowing four fifths of everything takes everything");
        C(StorageReviewMath.CatchAllScore(1) == 0f && StorageReviewMath.CatchAllScore(2) > 0f && StorageReviewMath.CatchAllScore(9) == 0.3f, "one catch-all is fine, two are a muddle, many are capped");
        C(StorageReviewMath.UnstoredScore(0) == 0f && StorageReviewMath.UnstoredScore(10) < StorageReviewMath.UnstoredScore(100) && StorageReviewMath.UnstoredScore(10000) == 0.4f, "goods lying about matter more the more there are, up to a cap");
        C(StorageReviewMath.BenchFarScore(15f) == 0f && StorageReviewMath.BenchFarScore(30f) > 0f && StorageReviewMath.BenchFarScore(30f) < StorageReviewMath.BenchFarScore(60f), "a bench twenty cells from its materials is near enough; farther matters more");
        C(StorageReviewMath.SpreadScore(2) == 0f && StorageReviewMath.SpreadScore(3) > 0f && StorageReviewMath.SpreadScore(20) == 0.25f, "one kind of goods in three rooms scatters");

        // Where a new stockpile fits: a 3x3 block nearest the bench's work spot, never on it.
        {
            var blocked = new HashSet<(int x, int z)> { (5, 5), (5, 6), (5, 7), (6, 5), (6, 6), (6, 7) };
            bool Free((int x, int z) c) => c.x >= 0 && c.z >= 0 && c.x < 12 && c.z < 12 && !blocked.Contains(c);
            bool ok = StorageReviewMath.BestBlock(Free, (4, 6), 7, 3, out var corner);
            C(ok, "a block is found by the bench");
            bool covers = false;
            for (int x = corner.x; x < corner.x + 3; x++)
                for (int z = corner.z; z < corner.z + 3; z++)
                    if ((x, z) == (4, 6) || blocked.Contains((x, z))) covers = true;
            C(!covers, $"it covers neither the work spot nor the bench ({corner.x},{corner.z})");
            float cx = corner.x + 1f, cz = corner.z + 1f;
            C(Math.Abs(cx - 4f) + Math.Abs(cz - 6f) <= 4f, $"and it is close by ({corner.x},{corner.z})");
            bool none = StorageReviewMath.BestBlock(c => c.x >= 0 && c.z >= 0 && c.x < 2 && c.z < 2, (0, 0), 3, 3, out _);
            C(!none, "no block where the ground is too tight");
            C(StorageReviewMath.BestBlock(c => c.x >= 0 && c.z >= 0 && c.x < 3 && c.z < 3 && c != (0, 0), (0, 0), 3, 2, out var small) && (small == (1, 0) || small == (0, 1)), $"a smaller block fits beside the target ({small.x},{small.z})");
        }
        return n;
    }
}
