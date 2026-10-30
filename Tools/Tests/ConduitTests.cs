using System;
using System.Collections.Generic;
using System.Linq;
using ColonyCouncil;

/// <summary>Laying a conduit to a piece off the power (Building/ConduitMath): the shortest run from the net to within reach, round what's in the way.</summary>
public static class ConduitTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "laying a conduit: " + what);
        }

        // The net runs along x = 0; the piece stands at (20, 0); a wall of rock at x = 10 with a gap at z = 3.
        bool Passable((int x, int z) c) => c.x >= 0 && c.x <= 30 && c.z >= 0 && c.z <= 10 && !(c.x == 10 && c.z != 3);
        var sources = Enumerable.Range(0, 11).Select(z => (1, z)).ToList();
        var path = ConduitMath.Path(sources, Passable, (20, 0));
        C(path.Count > 0, "a run is found");
        C(path.Count > 0 && path[0].x == 1, "it starts beside the net");
        C(path.Count > 0 && ConduitMath.Within(path[path.Count - 1], (20, 0), ConduitMath.Reach), "and ends within reach of the piece");
        C(path.All(Passable), "every cell of it takes a conduit");
        C(path.Any(c => c.x == 10 && c.z == 3), "it goes through the gap in the rock");
        bool contiguous = true;
        for (int i = 1; i < path.Count; i++)
            if (Math.Abs(path[i].x - path[i - 1].x) + Math.Abs(path[i].z - path[i - 1].z) != 1) contiguous = false;
        C(contiguous, "the run is unbroken");
        // Shortest: beside the net at z=3 (through the gap), along to x=14 at z=0 or so: at most the Manhattan distance plus a little.
        C(path.Count <= 20, $"and no longer than it needs to be ({path.Count} cells)");

        C(ConduitMath.Path(sources, c => Passable(c) && c.x != 10, (20, 0)).Count == 0, "no gap, no run");
        C(ConduitMath.Path(sources, Passable, (20, 0), ConduitMath.Reach, 5).Count == 0, "a run longer than allowed isn't laid");
        var near = ConduitMath.Path(sources, Passable, (4, 0));
        C(near.Count == 1 && near[0].x == 1, "a piece already within reach of the net needs one cell beside the net at most");
        return n;
    }
}
