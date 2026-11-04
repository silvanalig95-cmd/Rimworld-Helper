using ColonyCouncil;

/// <summary>
/// Where the colony gathers out of doors (YardMath): a pleasant terrace with seats beats bare ground beside the
/// machinery, a cramped corner is no place at all, and the far edge of the home ground loses to the same place nearer in.
/// </summary>
public static class YardTests
{
    public static int Run(Action<bool, string> check)
    {
        int cases = 0;
        void C(bool ok, string what)
        {
            cases++;
            check(ok, "gathering yard: " + what);
        }

        var bare = new YardMath.Cell { beauty = 0f, room = 20, distance = 8f };
        var terrace = new YardMath.Cell { beauty = 3f, floor = true, seats = 3, room = 20, distance = 8f };
        var machines = new YardMath.Cell { beauty = -1f, works = 2, room = 20, distance = 5f };
        float b = YardMath.Score(bare), t = YardMath.Score(terrace), m = YardMath.Score(machines);
        C(t > b && b > m, $"a terrace with seats ({t:0.0}) beats bare ground ({b:0.0}), which beats the spot by the batteries ({m:0.0})");
        C(YardMath.Score(new YardMath.Cell { room = YardMath.MinRoom - 1, beauty = 10f, floor = true }) == YardMath.Unfit,
            "a corner too small for a group is no gathering place, however pretty");
        var party = bare;
        party.gatherNear = true;
        C(YardMath.Score(party) > b, "a party spot or a fire close by draws the gathering");
        var far = bare;
        far.distance = 26f;
        C(YardMath.Score(far) < b, "the same ground far out at the edge loses to it nearer in");
        foreach (var bad in new[] { "stockpile", "field", "byDoor" })
        {
            var c = bare;
            if (bad == "stockpile") c.stockpile = true;
            if (bad == "field") c.field = true;
            if (bad == "byDoor") c.byDoor = true;
            C(YardMath.Score(c) < b, $"a {bad} cell is a worse place to gather than bare ground");
        }
        var ugly = bare;
        ugly.beauty = -40f;
        C(YardMath.Score(ugly) >= b - 10.01f, "beauty counts, but within bounds (a corpse nearby doesn't swamp everything else)");

        // More than one place: a terrace's many good cells are one place, the yard across the colony another.
        var ranked = new List<(int x, int z, float s)>
        {
            (10, 10, 9f), (11, 10, 8.9f), (10, 11, 8.8f), (12, 12, 8.5f), (30, 10, 7f), (31, 11, 6.9f), (10, 40, 2f), (50, 50, -8f)
        };
        var places = YardMath.Places(ranked);
        C(places.Count == 4 && places[0] == 0 && places[1] == 4 && places[2] == 6 && places[3] == 7,
            "cells close together make one place, the best first; cells far apart make others");
        var many = new List<(int x, int z, float s)>();
        for (int i = 0; i < 20; i++) many.Add((i * 10, 0, 5f - i * 0.1f));
        C(YardMath.Places(many).Count == YardMath.MaxPlaces, "at most a handful of places are weighed");

        float best = 9f;
        C(YardMath.Weight(9f, best, -1) > YardMath.Weight(7f, best, -1), "a better place is likelier");
        C(YardMath.Weight(7f, best, -1) > 0f, "a good place a little below the best still has a fair chance");
        C(YardMath.Weight(best - 2f * YardMath.Spread - 0.1f, best, -1) == 0f, "a poor place far below the best is never chosen");
        C(YardMath.Weight(9f, best, 0) < YardMath.Weight(7f, best, -1), "the place used last time gives way to another good one");
        C(YardMath.Weight(9f, best, 0) > 0f, "but it isn't ruled out: with no other place it is still used");
        C(YardMath.Weight(9f, best, 1) > YardMath.Weight(9f, best, 0) && YardMath.Weight(9f, best, 2) > YardMath.Weight(9f, best, 1),
            "the longer ago a place was used, the less it is held back");

        var weights = new List<float> { 1f, 0f, 3f };
        C(YardMath.Pick(weights, 0f) == 0 && YardMath.Pick(weights, 0.2f) == 0, "a low roll takes the first place");
        C(YardMath.Pick(weights, 0.3f) == 2 && YardMath.Pick(weights, 0.999f) == 2, "a place with no weight is skipped");
        C(YardMath.Pick(new List<float> { 0f, 0f }, 0.5f) == -1, "nothing to choose from: no place");
        int[] counts = new int[3];
        for (int i = 0; i < 400; i++) counts[YardMath.Pick(weights, i / 400f)]++;
        C(counts[1] == 0 && Math.Abs(counts[2] - 300) <= 2, "places are chosen in proportion to their weight");
        return cases;
    }
}
