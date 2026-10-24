using ColonyCouncil;

/// <summary>
/// Camps that look lived-in (Camps/CampLookMath): a handful of animals at most, fed before they'd go after the colony's
/// crops, porters' loads going out and coming back, a farewell walk that always ends, a smith mending the gate first,
/// and saplings in a ring round the old fire.
/// </summary>
public static class CampLookTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "camp look: " + what);
        }

        // ---------------- Animals ----------------

        C(CampLookMath.AnimalsFor(3, 0) == 3, "a herd of three comes as three");
        C(CampLookMath.AnimalsFor(4, 3) == CampLookMath.MaxAnimals - 3, "a later group gets only the room that's left");
        C(CampLookMath.AnimalsFor(9, 0) == CampLookMath.MaxAnimals, "never more than a handful");
        C(CampLookMath.AnimalsFor(2, CampLookMath.MaxAnimals) == 0 && CampLookMath.AnimalsFor(2, 99) == 0, "none once the camp has its handful");
        C(CampLookMath.AnimalsFor(-2, 0) == 0 && CampLookMath.AnimalsFor(2, -5) == 2, "odd counts come to no less than none");
        C(CampLookMath.MaxAnimals <= 6, "a handful is a handful");

        // ---------------- Feeding ----------------

        C(CampLookMath.Fed(0.1f) == CampLookMath.FeedTo, "a hungry beast is fed by the camp");
        C(CampLookMath.Fed(CampLookMath.FeedBelow + 0.05f) == CampLookMath.FeedBelow + 0.05f, "a beast that isn't hungry is left alone");
        C(CampLookMath.Fed(0.95f) == 0.95f, "feeding never takes anything away");
        // The game sends a hungry herbivore out to eat at about 45%: the camp feeds them before that.
        C(CampLookMath.FeedBelow >= 0.45f && CampLookMath.FeedTo > CampLookMath.FeedBelow, "fed before the game would send them after the colony's crops");
        C(CampLookMath.GrazeBelow > CampLookMath.FeedTo, "grazing still happens between feeds");

        // ---------------- Porters ----------------

        C(CampLookMath.CarryOut(0f) && CampLookMath.CarryOut(CampLookMath.AtSpot), "a load at its home spot goes out");
        C(!CampLookMath.CarryOut(CampLookMath.AtSpot + 0.5f) && !CampLookMath.CarryOut(20f), "a load anywhere else comes home");

        // ---------------- The farewell walk ----------------

        const int H = CampLookMath.TicksPerHour;
        C(!CampLookMath.WalkOver(false, true, 1000, 1000 + CampLookMath.WalkTicks), "a walk under way goes on");
        C(CampLookMath.WalkOver(true, true, 1000, 5000), "a finished walk is over");
        C(CampLookMath.WalkOver(false, false, 1000, 5000), "a walk with nobody left to walk it is over");
        C(CampLookMath.WalkOver(false, true, 5001, 5000), "a walk past its time is over, whatever happens");
        C(CampLookMath.WalkTicks >= 2 * H && CampLookMath.WalkTicks <= 8 * H, "a last walk takes hours, not days");

        // ---------------- A smith's mending ----------------

        C(CampLookMath.RepairScore(40f, true) < CampLookMath.RepairScore(30f, false), "the gate a little farther off is mended before a nearer wall");
        C(CampLookMath.RepairScore(10f, false) < CampLookMath.RepairScore(30f, true), "but not a gate across the map before the wall beside the camp");
        C(CampLookMath.RepairScore(CampLookMath.RepairRange + 1f, true) == float.MaxValue, "nothing out of range is mended");
        C(CampLookMath.RepairBelow > 0.5f && CampLookMath.RepairBelow < 1f, "only what's noticeably knocked about");
        C(CampLookMath.RepairTicks == CampLookMath.TicksPerDay, "a day to get to it");

        // ---------------- Saplings round the old fire ----------------

        int ring = 0;
        for (int dx = -4; dx <= 4; dx++)
        for (int dz = -4; dz <= 4; dz++)
            if (CampLookMath.InRing(dx, dz)) ring++;
        C(!CampLookMath.InRing(0, 0) && !CampLookMath.InRing(1, 1), "never on the fire's own cell or beside it");
        C(CampLookMath.InRing(2, 0) && CampLookMath.InRing(2, 2) && !CampLookMath.InRing(3, 0), "a ring two steps out");
        C(ring >= 12 && ring <= 24, "room round the ring for a handful of saplings, spaced");
        return n;
    }
}
