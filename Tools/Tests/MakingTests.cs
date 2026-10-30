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

        // Targets by the colony's size, and by what the building and the bills want.
        C(MakingMath.TargetFor("meals", 0) == MakingMath.TargetFor("meals") && MakingMath.TargetFor("blocks", 5) == MakingMath.TargetFor("blocks"), "no colonists counted, or blocks: the usual");
        C(MakingMath.TargetFor("meals", 1) == 10 && MakingMath.TargetFor("meals", 5) == 30 && MakingMath.TargetFor("meals", 20) == 60, "meals: three days of two a head, within bounds");
        C(MakingMath.TargetFor("medicine", 2) == 8 && MakingMath.TargetFor("medicine", 10) == 20 && MakingMath.TargetFor("medicine", 40) == 30, "medicine: two a head, within bounds");
        C(MakingMath.DemandTarget(20, 100, 150) == 150, "a want the usual count covers asks for the usual");
        C(MakingMath.DemandTarget(200, 100, 150) == 320, "a want beyond it asks for the stock, the want and a fifth more, in fives");
        C(MakingMath.DemandTarget(3, 4, 10) == 15, "a small want still gets five more");
        C(MakingMath.DemandTarget(50, 0, 10) == 10, "nothing short: the usual");
        C(MakingMath.Short(100, 30) == 70 && MakingMath.Short(10, 30) == 0, "what the building lacks beyond the stock");
        C(MakingMath.WantedRank("weapons", true) < MakingMath.WantedRank("meals", false), "what is waited on comes before the staples");
        return n;
    }
}
