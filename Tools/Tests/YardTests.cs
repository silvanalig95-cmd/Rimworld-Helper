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
        return cases;
    }
}
