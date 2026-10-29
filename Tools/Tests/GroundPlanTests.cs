using System;
using System.Collections.Generic;
using System.Linq;
using ColonyCouncil;

/// <summary>The ground plan (Building/GroundPlanMath): the order rooms are placed in, the ground each keeps, the outline, the colours.</summary>
public static class GroundPlanTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the ground plan: " + what);
        }

        var order = GroundPlanMath.Order(new[] { false, true, false, true }, new[] { 0.9f, 0.1f, 0.5f, 0.2f });
        C(order.SequenceEqual(new[] { 1, 3, 0, 2 }), $"planned rooms first in their order, then proposals by score ({string.Join(",", order)})");
        var many = GroundPlanMath.Order(Enumerable.Repeat(false, 10).ToList(), Enumerable.Range(0, 10).Select(i => i / 10f).ToList());
        C(many.Count == GroundPlanMath.MaxRooms && many[0] == 9, "at most six are drawn, the best first");

        var kept = GroundPlanMath.Kept(10, 20, 5, 4);
        C(kept.Count == (5 + 2) * (4 + 2) && kept.Contains((9, 19)) && kept.Contains((15, 24)) && !kept.Contains((8, 20)), "a room keeps its footprint and one cell round it");
        var edge = GroundPlanMath.Edge(0, 0, 4, 3);
        C(edge.Count == 4 * 3 - 2 && !edge.Contains((1, 1)) && edge.Contains((3, 2)), "the outline is the footprint's rim");
        C(GroundPlanMath.ColorOf(0) != GroundPlanMath.ColorOf(1) && GroundPlanMath.ColorOf(6) == GroundPlanMath.ColorOf(0), "colours differ and come round again");
        return n;
    }
}
