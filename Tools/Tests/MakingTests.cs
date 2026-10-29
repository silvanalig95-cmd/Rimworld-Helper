using System;
using System.Collections.Generic;
using ColonyCouncil;

/// <summary>What the colony could make (Building/MakingMath): a batch's parts, plenty, the bill's count, the order of kinds, unused harvests.</summary>
public static class MakingTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "what you could make: " + what);
        }

        C(MakingMath.BatchPossible(new List<(float, float)> { (10f, 5f), (3f, 3f) }), "a batch with every part in stock is possible");
        C(!MakingMath.BatchPossible(new List<(float, float)> { (10f, 5f), (2f, 3f) }), "one part short and it isn't");
        C(!MakingMath.BatchPossible(new List<(float, float)>()), "a recipe with no parts isn't a batch");
        C(MakingMath.WorthShowing(0) && MakingMath.WorthShowing(59) && !MakingMath.WorthShowing(60), "sixty of a product is plenty");
        C(MakingMath.TargetFor("blocks") > MakingMath.TargetFor("meals") && MakingMath.TargetFor("meals") > MakingMath.TargetFor("components") && MakingMath.TargetFor("weapons") == 1, "bills ask for many blocks, some meals, a few components, one weapon");
        C(MakingMath.TargetFor("whatever") == 10, "anything else: ten");
        C(MakingMath.KindRank("meals") < MakingMath.KindRank("medicine") && MakingMath.KindRank("medicine") < MakingMath.KindRank("blocks") && MakingMath.KindRank("weapons") > MakingMath.KindRank("apparel"), "food first, arms last");
        C(MakingMath.IsWearable("apparel") && MakingMath.IsWearable("weapons") && !MakingMath.IsWearable("meals"), "clothes and weapons are the wearables");
        C(MakingMath.Unused(40, false) && !MakingMath.Unused(40, true) && !MakingMath.Unused(5, false), "a fair amount with no bench here that uses it is unused");
        return n;
    }
}
