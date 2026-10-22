using ColonyCouncil;

/// <summary>
/// People from outside who are really there (Visitors/VisitorMath): how many may come and be on the map, when a waiting
/// group comes, when guests go on at first light, when someone at the gate has waited long enough, and which way
/// passers-by leave.
/// </summary>
public static class VisitorTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "visitors: " + what);
        }

        const int H = VisitorMath.TicksPerHour, D = VisitorMath.TicksPerDay;

        // ---------------- How many ----------------

        C(VisitorMath.People(0) == 1 && VisitorMath.People(-3) == 1, "a visit is at least one person");
        C(VisitorMath.People(4) == 4, "a family of four comes as four");
        C(VisitorMath.People(40) == VisitorMath.MaxPeople, "never more than a few people in one group");
        C(VisitorMath.Animals(-1) == 0 && VisitorMath.Animals(0) == 0, "no animals unless there's a herd or a beast");
        C(VisitorMath.Animals(99) == VisitorMath.MaxAnimals, "a herd is a few head, not a stampede");
        C(VisitorMath.Room(0) && VisitorMath.Room(VisitorMath.MaxHere - 1), "room for another group while few are here");
        C(!VisitorMath.Room(VisitorMath.MaxHere) && !VisitorMath.Room(VisitorMath.MaxHere + 2), "a map with enough visitors on it takes no more");
        C(VisitorMath.MaxHere * (VisitorMath.MaxPeople + VisitorMath.MaxAnimals) <= 60, "a map never holds more than a modest crowd of them");

        // ---------------- When they come ----------------

        C(VisitorMath.InHours(13, 13, 18) && VisitorMath.InHours(17, 13, 18), "the afternoon window holds its own hours");
        C(!VisitorMath.InHours(18, 13, 18) && !VisitorMath.InHours(12, 13, 18), "the window's end is not in it, nor the hour before");
        C(VisitorMath.InHours(22, 20, 4) && VisitorMath.InHours(2, 20, 4) && !VisitorMath.InHours(12, 20, 4), "a window may run past midnight");
        C(VisitorMath.InHours(3, 0, 0) && VisitorMath.InHours(15, 7, 7), "an empty window is any hour");

        int due = 1000, giveUp = due + VisitorMath.GiveUpTicks;
        C(VisitorMath.Verdict(due, due, giveUp, 14, 13, 18, true, true, true) == VisitWait.Go, "due, in their hours, safe: they come");
        C(VisitorMath.Verdict(due - 1, due, giveUp, 14, 13, 18, true, true, true) == VisitWait.Wait, "not before they're due");
        C(VisitorMath.Verdict(due, due, giveUp, 9, 13, 18, true, true, true) == VisitWait.Wait, "not outside their hours");
        C(VisitorMath.Verdict(due, due, giveUp, 14, 13, 18, false, true, true) == VisitWait.Wait, "not into danger");
        C(VisitorMath.Verdict(giveUp + 1, due, giveUp, 14, 13, 18, true, true, true) == VisitWait.Drop, "too long: they don't come at all");
        C(VisitorMath.Verdict(due, due, giveUp, 14, 13, 18, true, false, true) == VisitWait.Drop, "the setting off: nobody comes");
        C(VisitorMath.Verdict(due, due, giveUp, 14, 13, 18, true, true, false) == VisitWait.Drop, "the map gone: nobody comes");
        C(VisitorMath.GiveUpTicks <= 2 * D, "a visit that can't happen is given up within two days");

        // ---------------- How long guests stay ----------------

        C(VisitorMath.HoursToDawn(10, 1) == 20, "given a bed at ten in the morning, they go at six the next morning");
        C(VisitorMath.HoursToDawn(20, 1) == 10, "taken in at eight in the evening: away at first light");
        C(VisitorMath.HoursToDawn(1, 1) == 5, "taken in after midnight: the rest of that night counts");
        C(VisitorMath.HoursToDawn(5, 1) == 25, "an hour before dawn is no night's rest: they stay the next");
        C(VisitorMath.HoursToDawn(10, 2) == 44, "two nights: the dawn after the second");
        C(VisitorMath.HoursToDawn(10, 99) == VisitorMath.HoursToDawn(10, VisitorMath.MaxNights), "never more than a few nights, whatever the text hopes");
        C(VisitorMath.HoursToDawn(10, 0) == VisitorMath.HoursToDawn(10, 1), "a stay is at least one night");
        bool dawnOk = true;
        for (int h = 0; h < 24; h++)
            for (int nights = 1; nights <= VisitorMath.MaxNights; nights++)
            {
                int left = VisitorMath.HoursToDawn(h, nights);
                if ((h + left) % 24 != VisitorMath.DawnFrom || left < 2 || left > 24 * nights + 2) dawnOk = false;
            }
        C(dawnOk, "whenever they're given the night, they leave at first light, after a real night's rest");

        // ---------------- When someone at the gate has waited enough ----------------

        int arrived = 5000;
        C(!VisitorMath.WaitOver(arrived + H, arrived, true, true, -1, false, 0), "the letter still open: they wait");
        C(!VisitorMath.WaitOver(arrived + H, -1, true, false, arrived, false, 0), "not before they've even got there");
        C(VisitorMath.WaitOver(arrived + VisitorMath.MaxWaitTicks, arrived, true, true, -1, false, 0), "but never past the longest wait");
        int answered = arrived + 3 * H;
        C(!VisitorMath.WaitOver(answered, arrived, true, false, answered, false, 0), "answered: they linger a moment");
        C(VisitorMath.WaitOver(answered + VisitorMath.LingerTicks, arrived, true, false, answered, false, 0), "then they go");
        C(!VisitorMath.WaitOver(answered + 2 * H, arrived, true, false, answered, true, 0), "something on its way out to them: they wait for it");
        C(VisitorMath.WaitOver(answered + VisitorMath.ErrandWaitTicks, arrived, true, false, answered, true, 0), "not for ever, though");
        C(!VisitorMath.WaitOver(arrived + H, arrived, false, false, -1, false, 2 * H), "nothing asked: they stay their hours");
        C(VisitorMath.WaitOver(arrived + 2 * H, arrived, false, false, -1, false, 2 * H), "and then go");
        C(VisitorMath.MaxWaitTicks > 2 * D && VisitorMath.MaxWaitTicks < 3 * D, "the longest wait covers the slowest letter (two days) and not much more");

        // ---------------- Which way they go ----------------

        VisitorMath.Opposite(0, 100, 250, 250, out int ox, out int oz);
        C(ox == 249, "in from the west edge: out by the east");
        VisitorMath.Opposite(120, 249, 250, 250, out ox, out oz);
        C(oz == 0, "in from the north: out by the south");
        VisitorMath.Opposite(0, 0, 250, 250, out ox, out oz);
        C((ox == 249 || oz == 249) && ox >= 0 && oz >= 0, "in from a corner: out by the far corner's side");
        bool edge = true;
        for (int x = 0; x < 200; x += 13)
        {
            VisitorMath.Opposite(x, 0, 200, 150, out ox, out oz);
            if (ox < 0 || ox > 199 || oz < 0 || oz > 149 || ox != 0 && ox != 199 && oz != 0 && oz != 149) edge = false;
        }
        C(edge, "the way out is always on the map's edge");
        return n;
    }
}
