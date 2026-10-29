using System;
using System.Collections.Generic;
using ColonyCouncil;

/// <summary>Where a room plan should go (Building/SpotMath): the score's parts, distinct best spots, and closeness in words.</summary>
public static class SpotTests
{
    private static SpotMath.Features F() => new SpotMath.Features { near = new float[0], avoid = new float[0] };

    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "where a room goes: " + what);
        }

        var plain = F();
        float basis = SpotMath.Score(plain);
        var walls = F();
        walls.sharedWalls = 4;
        C(SpotMath.Score(walls) > basis, "standing walls to share make a spot better");
        var rock = F();
        rock.rock = 6;
        C(SpotMath.Score(rock) < basis, "rock to mine makes it worse");
        var far = F();
        far.distCenter = 30f;
        C(SpotMath.Score(far) < basis, "a long walk from the colony's middle counts against it");

        var byKitchen = F();
        byKitchen.near = new[] { 4f };
        var farKitchen = F();
        farKitchen.near = new[] { 30f };
        var noKitchen = F();
        noKitchen.near = new[] { -1f };
        C(SpotMath.Score(byKitchen) > SpotMath.Score(farKitchen) && SpotMath.Score(farKitchen) == SpotMath.Score(noKitchen), "beside a wanted room is better; far from it is no worse than none at all");
        var byPrison = F();
        byPrison.avoid = new[] { 5f };
        C(SpotMath.Score(byPrison) < basis, "close to an unwanted room is worse");

        var gateNear = F();
        gateNear.wantsGate = true;
        gateNear.distGate = 6f;
        var gateFar = F();
        gateFar.wantsGate = true;
        gateFar.distGate = 40f;
        var gateMoot = F();
        gateMoot.distGate = 6f;
        C(SpotMath.Score(gateNear) > SpotMath.Score(gateFar) && SpotMath.Score(gateMoot) == basis, "the gate counts only for a room that wants it");
        var fieldsNear = F();
        fieldsNear.wantsFields = true;
        fieldsNear.distFields = 5f;
        C(SpotMath.Score(fieldsNear) > basis, "a barn by the fields is better");
        var loud = F();
        loud.wantsQuiet = true;
        loud.noisy = new[] { 4f, -1f };
        var calm = F();
        calm.wantsQuiet = true;
        calm.noisy = new[] { 20f, -1f };
        var deaf = F();
        deaf.noisy = new[] { 4f, -1f };
        C(SpotMath.Score(loud) < SpotMath.Score(calm) && SpotMath.Score(deaf) == basis, "a bedroom beside the workshop is worse; a workshop beside one doesn't mind");

        var scores = new List<float> { 10f, 9f, 8f, 1f };
        var mids = new List<(int, int)> { (0, 0), (2, 0), (20, 0), (40, 0) };
        var kept = SpotMath.Distinct(scores, mids, 6f, 3);
        C(kept.Count == 3 && kept[0] == 0 && kept[1] == 2 && kept[2] == 3, "the best spots kept are apart: the second-best, on top of the best, is skipped");
        C(SpotMath.Distinct(scores, mids, 6f, 1).Count == 1, "as many as asked for");
        C(SpotMath.Closeness(3f) == 2 && SpotMath.Closeness(10f) == 1 && SpotMath.Closeness(30f) == 0 && SpotMath.Closeness(-1f) == -1, "beside, near, far, or none");
        return n;
    }
}
