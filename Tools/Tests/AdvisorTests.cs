using ColonyCouncil;

/// <summary>
/// The advisor's newer checks (AdvisorMath): when a mission is pressing, a decree costs more than it gives, the only
/// doctor needs a second, the woodpile and the stores fall short of the winter, a calendar day lacks what its way
/// needs, and when a kitchen, a freezer, a dining room, a workshop or a prison cell has been outgrown or wants
/// something; and which size a room grows to, and which beds and benches a light reaches.
/// </summary>
public static class AdvisorTests
{
    public static int Run(Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "advisor: " + what);
        }

        const int day = AdvisorMath.TicksPerDay;

        // ---------- Missions: the last two days, or the last quarter when that's under four days ----------
        Check(AdvisorMath.MissionPressing(day, 10 * day, false), "a day left is pressing");
        Check(AdvisorMath.MissionPressing(2 * day, 10 * day, false), "two days left is pressing");
        Check(!AdvisorMath.MissionPressing(5 * day, 10 * day, false), "five of ten days left isn't");
        Check(AdvisorMath.MissionPressing(3 * day, 12 * day, false), "a quarter of twelve days, three left, is pressing");
        Check(!AdvisorMath.MissionPressing(5 * day, 30 * day, false), "a sixth of a long mission, five days left, isn't yet");
        Check(!AdvisorMath.MissionPressing(day / 2, 10 * day, true), "goods all there: never pressing");
        Check(!AdvisorMath.MissionPressing(0, 10 * day, false), "over is over");
        Check(!AdvisorMath.MissionPressing(-day, 10 * day, false), "past the deadline isn't pressing any more");

        // ---------- Decrees: its reason gone, a loss of three or more, and more against than for ----------
        Check(AdvisorMath.DecreeCostsMore(4f, -10f, 3, 1, false), "three against, one for, net -6, the reason passed");
        Check(!AdvisorMath.DecreeCostsMore(4f, -10f, 3, 1, true), "while what it's for still holds, its cost is the point");
        Check(!AdvisorMath.DecreeCostsMore(8f, -10f, 3, 2, false), "net -2 isn't enough");
        Check(!AdvisorMath.DecreeCostsMore(6f, -10f, 2, 3, false), "more for than against: worth it");
        Check(!AdvisorMath.DecreeCostsMore(0f, 0f, 0, 0, false), "nobody minds");
        Check(AdvisorMath.DecreeCostsMore(0f, -3f, 1, 0, false), "exactly -3 with one against counts");

        // ---------- Doctors ----------
        Check(AdvisorMath.NeedsBackupDoctor(1, 3), "one doctor for three");
        Check(!AdvisorMath.NeedsBackupDoctor(1, 2), "two people: a second doctor is the other one anyway");
        Check(!AdvisorMath.NeedsBackupDoctor(2, 8), "two doctors are enough");
        Check(!AdvisorMath.NeedsBackupDoctor(0, 5), "no doctor at all is the plain medical advice");

        // ---------- Firewood ----------
        Check(AdvisorMath.FirewoodNeeded(10f, 30f) == 300, "a campfire for a month: 300 logs");
        Check(AdvisorMath.FirewoodNeeded(10f, 200f) == 600, "at most two months are counted");
        Check(AdvisorMath.FirewoodNeeded(0f, 30f) == 0 && AdvisorMath.FirewoodNeeded(10f, 0f) == 0, "no fire or no winter: nothing");
        Check(AdvisorMath.FirewoodNeeded(2.5f, 3f) == 8, "rounded up");
        Check(AdvisorMath.FirewoodShort(100, 300), "a third of it is short");
        Check(!AdvisorMath.FirewoodShort(200, 300), "two thirds is enough to start with");
        Check(!AdvisorMath.FirewoodShort(0, 10), "a torch's worth isn't worth a word");

        // ---------- Food that keeps through winter ----------
        Check(AdvisorMath.StoresWontKeep(3f, 20f, 30f), "plenty that spoils, little that keeps");
        Check(!AdvisorMath.StoresWontKeep(16f, 20f, 30f), "half the winter's worth keeps: fine");
        Check(!AdvisorMath.StoresWontKeep(0f, 3f, 30f), "nothing much to preserve");
        Check(!AdvisorMath.StoresWontKeep(0f, 20f, 6f), "a short winter needs no stores");
        Check(AdvisorMath.StoresWontKeep(14f, 20f, 60f), "a long winter is counted as a month at most: 14 of 15 days is short");
        Check(!AdvisorMath.StoresWontKeep(15f, 20f, 60f), "15 days that keep see half of a month out");

        // ---------- Calendar days and what their way needs ----------
        Check(AdvisorMath.CalendarLacks("Feast", 3, 6, 0, false, 0) == "Feast", "a feast with three meals for six");
        Check(AdvisorMath.CalendarLacks("Feast", 6, 6, 0, false, 0) == null, "a meal for everyone");
        Check(AdvisorMath.CalendarLacks("Hunt", 0, 6, 0, false, 0) == "Hunt", "a hunt with nobody to hunt");
        Check(AdvisorMath.CalendarLacks("Hunt", 0, 6, 1, false, 0) == null, "one hunter is enough");
        Check(AdvisorMath.CalendarLacks("Bonfire", 0, 6, 0, false, 5) == "Bonfire", "no fire and five logs");
        Check(AdvisorMath.CalendarLacks("Bonfire", 0, 6, 0, true, 0) == null, "a campfire to gather round");
        Check(AdvisorMath.CalendarLacks("Bonfire", 0, 6, 0, false, 40) == null, "wood to lay one");
        foreach (var way in new[] { "Rest", "Games", "Gifts", "Remembrance", "Personal" })
            Check(AdvisorMath.CalendarLacks(way, 0, 6, 0, false, 0) == null, way + " needs nothing laid by");

        // ---------- Kitchens ----------
        Check(AdvisorMath.StovesFor(1) == 1 && AdvisorMath.StovesFor(7) == 1 && AdvisorMath.StovesFor(8) == 2 && AdvisorMath.StovesFor(15) == 2 && AdvisorMath.StovesFor(16) == 3, "stoves by size");
        Check(AdvisorMath.KitchenOutgrown(10, 1, 2, 4), "ten people, one stove, two cooks, four meals");
        Check(!AdvisorMath.KitchenOutgrown(10, 1, 1, 4), "one cook: a second stove wouldn't help");
        Check(!AdvisorMath.KitchenOutgrown(10, 1, 2, 12), "meals enough in store");
        Check(!AdvisorMath.KitchenOutgrown(6, 1, 3, 0), "a small colony is fine with one stove");
        Check(!AdvisorMath.KitchenOutgrown(10, 2, 3, 0), "two stoves for ten");
        Check(!AdvisorMath.KitchenOutgrown(10, 0, 3, 0), "no stove at all is other advice");

        // ---------- Freezers ----------
        Check(AdvisorMath.FreezerTooSmall(20, 18, 40), "nine tenths full and food out in the warm");
        Check(!AdvisorMath.FreezerTooSmall(20, 10, 40), "half full: just stock it");
        Check(!AdvisorMath.FreezerTooSmall(20, 20, 10), "full, but little left out");
        Check(AdvisorMath.FreezerTooSmall(20, 17, 30), "the thresholds count");
        Check(!AdvisorMath.FreezerTooSmall(0, 0, 100), "no freezer is the plain freezer advice");

        // ---------- Dining rooms ----------
        Check(AdvisorMath.DiningCramped(20f, 6), "rather tight for six");
        Check(!AdvisorMath.DiningCramped(40f, 6), "average space for six");
        Check(AdvisorMath.DiningCramped(40f, 20), "average space is cramped for twenty");
        Check(!AdvisorMath.DiningCramped(10f, 4), "four people make do");

        // ---------- Prison cells ----------
        Check(AdvisorMath.PrisonLacks(true, true, true, 30f) == 0, "a furnished cell lacks nothing");
        Check(AdvisorMath.PrisonLacks(false, false, false, 5f) == 4, "a bare cell lacks everything");
        Check(!AdvisorMath.PrisonUncomfortable(1) && AdvisorMath.PrisonUncomfortable(2), "two things missing is worth a word");

        // ---------- Workshops ----------
        Check(AdvisorMath.WorkshopWanting(3, 1, 0), "one bench in the dark");
        Check(AdvisorMath.WorkshopWanting(4, 0, 2), "half the benches without a chair");
        Check(!AdvisorMath.WorkshopWanting(4, 0, 1), "one bench without a chair is fine");
        Check(!AdvisorMath.WorkshopWanting(0, 0, 0), "no benches, no advice");

        // ---------- Room sizes by floor ----------
        var areas = new List<int> { 16, 30, 48 };
        Check(AdvisorMath.LargerSize(areas, 20) == 1, "a room of 20 grows to 30");
        Check(AdvisorMath.LargerSize(areas, 10) == 0, "a room of 10 grows to 16");
        Check(AdvisorMath.LargerSize(areas, 60) == 2, "bigger than every size: the largest");
        Check(AdvisorMath.LargerSize(new List<int> { 48, 16, 30 }, 20) == 2, "order doesn't matter");
        Check(AdvisorMath.LargerSize(new List<int>(), 20) == -1, "no sizes at all");

        // ---------- Light reaching a bed or a bench ----------
        var lights = new List<(int x, int z, float reach)> { (0, 0, LightMath.Reach(10f)) };
        Check(AdvisorMath.Lit(3, 3, lights), "a lamp lights a 5 by 5 room corner to corner");
        Check(!AdvisorMath.Lit(8, 0, lights), "eight cells off is past its reach");
        Check(!AdvisorMath.Lit(0, 0, new List<(int x, int z, float reach)>()), "no lights, no light");
        Check(!AdvisorMath.Lit(0, 0, new List<(int x, int z, float reach)> { (0, 0, 0f) }), "a light with no glow lights nothing");
        Check(AdvisorMath.Lit(6, 0, new List<(int x, int z, float reach)> { (0, 0, 1f), (6, 1, 2f) }), "any light will do");
        return cases;
    }
}
