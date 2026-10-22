using ColonyCouncil;

/// <summary>
/// Goods and people that come and go on the map (Couriers/CourierMath): which people sends the courier, how many come
/// and whether a pack animal comes with them, how a load is shared out, when a waiting trip comes, and what happens to
/// the rest of a deal when a trip ends early.
/// </summary>
public static class CourierTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "couriers: " + what);
        }

        // ---------------- Who sends them ----------------

        CourierCandidate F(int id, bool usable, int tech, float dist) => new CourierCandidate { id = id, usable = usable, tech = tech, distance = dist };
        const int Neolithic = 2, Medieval = 3, Industrial = 4, Spacer = 5;

        var mixed = new List<CourierCandidate>
        {
            F(1, true, Industrial, 10f), F(2, true, Medieval, 30f), F(3, false, Neolithic, 2f), F(4, true, Neolithic, 25f), F(5, true, Spacer, 1f)
        };
        C(CourierMath.ChooseFaction(mixed, 1, Neolithic, true) == 0, "the event's own people send it when they can, whatever their tech");
        C(CourierMath.ChooseFaction(mixed, 3, Neolithic, true) == 3, "an event's people who can't (hostile) are passed over");
        C(CourierMath.ChooseFaction(mixed, -1, Neolithic, true) == 3, "a tribal colony hears from the nearest tribe or medieval folk, not the nearer spacers");
        C(CourierMath.ChooseFaction(mixed.Take(4).ToList(), -1, Industrial, false) == 0, "an industrial colony: the nearest people a step either side of it, not the nearer tribe");
        C(CourierMath.ChooseFaction(mixed, -1, Industrial, false) == 4, "spacers next door are a step away from an industrial colony too");
        C(CourierMath.ChooseFaction(mixed, -1, Spacer, false) == 4, "a spacer colony: the spacers next door");
        C(CourierMath.ChooseFaction(mixed, 99, Medieval, true) == 3, "an unknown event people is no hindrance");
        var noneFits = new List<CourierCandidate> { F(1, true, Spacer, 5f), F(2, true, Industrial, 50f) };
        C(CourierMath.ChooseFaction(noneFits, -1, Neolithic, true) == 1, "nobody fits a tribe: the closest in tech before the nearest");
        var ties = new List<CourierCandidate> { F(7, true, Medieval, 10f), F(3, true, Medieval, 10f) };
        C(CourierMath.ChooseFaction(ties, -1, Medieval, true) == 1, "a tie goes the same way every time");
        C(CourierMath.ChooseFaction(new List<CourierCandidate> { F(1, false, Medieval, 1f) }, 1, Medieval, true) == -1, "nobody who can: nobody comes");
        C(CourierMath.ChooseFaction(new List<CourierCandidate>(), -1, Medieval, true) == -1 && CourierMath.ChooseFaction(null, -1, Medieval, true) == -1,
            "no peoples at all: nobody comes");
        C(CourierMath.TechFits(Neolithic, Neolithic, true) && CourierMath.TechFits(Medieval, Neolithic, true) && !CourierMath.TechFits(Industrial, Medieval, true),
            "a pre-industrial colony deals with tribes and medieval folk only");
        C(CourierMath.TechFits(Industrial, Spacer, false) && CourierMath.TechFits(Spacer, Industrial, false) && !CourierMath.TechFits(Neolithic, Industrial, false),
            "anyone else: a step either side");

        // ---------------- How many come ----------------

        // 150 silver (0.008 kg each, stacks of 500): a purse. 250 corn: two sacks' worth. 300 wood: a cart's worth.
        float purse = CourierMath.Bulk(150 * 0.008f, 150 / 500f);
        float corn = CourierMath.Bulk(250 * 0.03f, 250 / 75f);
        float wood = CourierMath.Bulk(300 * 0.4f, 300 / 75f);
        C(CourierMath.People(purse) == 1 && !CourierMath.PackAnimal(purse, true), "a purse of silver: one runner, no animal");
        C(CourierMath.People(corn) == 2, $"250 corn: two porters (bulk {corn:0.0})");
        C(CourierMath.People(wood) == CourierMath.MaxPeople && CourierMath.PackAnimal(wood, true), "a cart's worth of wood: three and a pack animal");
        C(!CourierMath.PackAnimal(wood, false), "no pack animal from people whose caravans don't use them");
        C(CourierMath.PeopleWithAnimal(corn, true) == 1 && CourierMath.PeopleWithAnimal(corn, false) == 2, "an animal spares a porter");
        C(CourierMath.PeopleWithAnimal(wood, true) == 2, "a big load still brings two to lead and load");
        for (float b = 0f; b < 400f; b += 7.5f)
        {
            int p = CourierMath.People(b);
            C(p >= 1 && p <= CourierMath.MaxPeople, $"one to three people for bulk {b}");
            C(CourierMath.PeopleWithAnimal(b, true) >= 1 && CourierMath.PeopleWithAnimal(b, true) <= p, $"never more with an animal than without (bulk {b})");
            C(!CourierMath.PackAnimal(b, true) || b > CourierMath.PersonLoad, $"an animal only for more than one person carries (bulk {b})");
        }
        C(CourierMath.Bulk(-5f, -1f) == 0f, "no load is no bulk");

        // ---------------- Sharing a load ----------------

        int[] s = CourierMath.Split(250, new List<float> { 35f, 35f, 84f });
        C(s.Sum() == 250 && s[2] > s[0] && Math.Abs(s[0] - s[1]) <= 1, "the animal carries the most, the porters alike, nothing lost: " + string.Join(",", s));
        C(CourierMath.Split(0, new List<float> { 35f }).Sum() == 0 && CourierMath.Split(5, new List<float>()).Length == 0, "nothing to share, or nobody to share it");
        C(CourierMath.Split(7, new List<float> { 0f, 0f }).SequenceEqual(new[] { 7, 0 }), "nobody has room: the first takes it");
        for (int count = 0; count <= 60; count += 7)
            for (int k = 1; k <= 4; k++)
            {
                var room = Enumerable.Range(0, k).Select(i => 10f + i * 13f).ToList();
                var split = CourierMath.Split(count, room);
                C(split.Length == k && split.Sum() == count && split.All(x => x >= 0), $"split {count} over {k} adds up");
            }

        // ---------------- When they come ----------------

        int day = CourierMath.TicksPerDay;
        C(CourierMath.Verdict(100, 50, 1000, 10, true, true, true) == CourierWait.Go, "due, by day, safe: they come");
        C(CourierMath.Verdict(40, 50, 1000, 10, true, true, true) == CourierWait.Wait, "not yet due: wait");
        C(CourierMath.Verdict(100, 50, 1000, 22, true, true, true) == CourierWait.Wait, "after dark: wait for morning");
        C(CourierMath.Verdict(100, 50, 1000, 3, true, true, true) == CourierWait.Wait, "before dawn: wait");
        C(CourierMath.Verdict(100, 50, 1000, 10, false, true, true) == CourierWait.Wait, "enemies about: wait");
        C(CourierMath.Verdict(1001, 50, 1000, 10, false, true, true) == CourierWait.Settle, "never safe for too long: the old way");
        C(CourierMath.Verdict(100, 50, 1000, 10, true, false, true) == CourierWait.Settle, "setting off: the old way, at once");
        C(CourierMath.Verdict(100, 50, 1000, 10, true, true, false) == CourierWait.Drop, "the map gone: nothing to settle");
        C(CourierMath.Daytime(CourierMath.DayFrom) && !CourierMath.Daytime(CourierMath.DayTo) && !CourierMath.Daytime(CourierMath.DayFrom - 1), "the hours they arrive in");
        C(CourierMath.GiveUpTicks >= day && CourierMath.GiveUpTicks <= day * 5, "they are waited for a few days at most");
        C(CourierMath.LingerTicks < CourierMath.WorkTicks && CourierMath.WorkTicks <= CourierMath.AwaitTicks, "linger briefly; an errand's courier waits longest");

        // ---------------- When it ends ----------------

        foreach (CourierEnd e in Enum.GetValues(typeof(CourierEnd)))
        {
            bool inc = CourierMath.SettleIncoming(e), outg = CourierMath.SettleOutgoing(e);
            if (e == CourierEnd.Harmed) C(!inc && !outg, "hurt by the colony: the deal holds as far as it got, both ways");
            else if (e == CourierEnd.MapGone) C(!inc && !outg, "the map gone: nothing to settle");
            else if (e == CourierEnd.Hostile) C(inc && !outg, "their people turned hostile: what's owed to the colony is set down, they take no more");
            else C(inc && outg, $"{e}: the rest goes the old way");
        }
        C(CourierMath.Left(250, 180) == 70 && CourierMath.Left(250, 300) == 0 && CourierMath.Left(10, -5) == 10, "what's still owed");
        C(CourierMath.CollectDone(250, 250, 40) && CourierMath.CollectDone(250, 100, 0) && !CourierMath.CollectDone(250, 100, 40),
            "collecting is done when all is taken, or when the stores have none left");
        C(CourierMath.WorkOver(1000 + CourierMath.WorkTicks, 1000, CourierMath.WorkTicks) && !CourierMath.WorkOver(1000, 1000, CourierMath.WorkTicks)
            && !CourierMath.WorkOver(99999, -1, CourierMath.WorkTicks), "out of time at the gate, never before they got there");
        C(CourierMath.MaxTrips >= 6 && CourierMath.MaxTrips <= 20, "a handful of trips at once at most");
        return n;
    }
}
