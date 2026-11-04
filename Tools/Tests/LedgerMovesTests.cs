using System;
using System.Collections.Generic;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// The stores that moved into the world ledger (stage 0 step 3b; World/Ledger/StoreMath.cs and LedgerMath.cs): who and
/// which towns the ledger keeps, when a tie that lapses "after so many days" lapses, and the dead a people's grudge holds.
/// Each check says what the old store did and that the ledger's record does the same.
/// </summary>
public static class LedgerMovesTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void Check(bool ok, string what)
        {
            n++;
            check(ok, "ledger moves: " + what);
        }

        const int Day = TieRules.DayTicks;

        // ---------------- Persons and towns ----------------

        Check(LedgerMath.KeepPerson(true, false, false), "a notable stays a person");
        Check(LedgerMath.KeepPerson(false, true, false), "someone a tie names stays a person");
        Check(LedgerMath.KeepPerson(false, false, true), "someone kept in the world stays a person");
        Check(!LedgerMath.KeepPerson(false, false, false), "someone nothing needs goes");
        Check(LedgerMath.KeepTown(true, true, -1, 0) && LedgerMath.KeepTown(true, false, 0, 999 * Day), "a town a notable lives in is kept, standing or gone");
        Check(!LedgerMath.KeepTown(false, true, -1, 5 * Day), "a standing town nobody lives in isn't kept (the world has it)");
        Check(LedgerMath.KeepTown(false, false, 10 * Day, 10 * Day + LedgerMath.GoneTownDays * Day - 1), "a gone town is remembered two years");
        Check(!LedgerMath.KeepTown(false, false, 10 * Day, 10 * Day + LedgerMath.GoneTownDays * Day), "and then forgotten");
        Check(!LedgerMath.KeepTown(false, false, -1, 0), "a gone town never missed isn't kept");
        Check(LedgerMath.GoneTownDays == 120, "two sixty-day years");

        // ---------------- "More than so many days ago" as an until ----------------

        foreach (float days in new[] { 0f, 1f, 3.5f, 20f, 30f, 45f, 0.01f, 17.3f })
            foreach (int since in new[] { 0, 1, 12345, 2500000 })
            {
                int until = StoreMath.After(since, days);
                bool same = true;
                for (int now = until - 3; now <= until + 3; now++)
                    if (StoreMath.Past(now, since, days) != TieRules.Due(until, now))
                        same = false;
                Check(same, $"a tie with until After({since}, {days}) ends exactly when \"more than {days} days since\" first holds");
            }
        Check(StoreMath.After(int.MaxValue - 10, 30f) == int.MaxValue - 1, "an until never overflows");

        // ---------------- Grudges: one tie per people, as the old list behaved ----------------

        Check(GrudgeTally.Max == 10 && GrudgeTally.KeepDays == 120 && GrudgeTally.ActDays == 8, "ten dead at most, two years, acted on after eight days");
        var one = new List<GrudgeDead> { new GrudgeDead(3, 100, false, "Ana"), new GrudgeDead(7, 200, true, "Bo|b"), new GrudgeDead(9, 300, false, "C\nD") };
        var back = GrudgeTally.Read(GrudgeTally.Write(one));
        Check(back.Count == 3 && back[0].order == 3 && back[0].tick == 100 && !back[0].settled && back[0].name == "Ana"
              && back[1].settled && back[1].name == "Bo|b" && back[2].name == "C D", "the dead read back from the key (a bar in a name kept, a line break made a space)");
        Check(GrudgeTally.Open(back) == 2, "the count is those not settled");
        Check(GrudgeTally.Read(null).Count == 0 && GrudgeTally.Read("junk\n1|x|0|A").Count == 0 && GrudgeTally.Write(new List<GrudgeDead>()) == null,
            "nothing, junk or no one reads and writes as nobody");

        // The old list (HappeningTracker.grudges): appended, the first over ten removed, settled by people and name,
        // tidied of the settled, the defeated and the two-year-old.
        var rand = new Random(7);
        var old = new List<(int people, string name, int tick, bool settled)>();
        var book = new Dictionary<int, List<GrudgeDead>>();
        var defeated = new HashSet<int>();
        string[] names = { "Ana", "Bo", "Cy", "Di", "Ed" };
        bool alike = true, countsAlike = true;
        int now0 = 0;
        for (int step = 0; step < 4000; step++)
        {
            now0 += rand.Next(0, 3 * Day);
            int roll = rand.Next(10);
            int people = rand.Next(1, 6);
            string name = names[rand.Next(names.Length)];
            if (roll < 5)
            {
                old.Add((people, name, now0, false));
                while (old.Count > 10) old.RemoveAt(0);
                GrudgeTally.Add(book, people, now0, name);
            }
            else if (roll < 7)
            {
                for (int i = 0; i < old.Count; i++)
                    if (old[i].people == people && old[i].name == name)
                        old[i] = (old[i].people, old[i].name, old[i].tick, true);
                GrudgeTally.Settle(book, people, name);
            }
            else if (roll < 9)
            {
                if (rand.Next(20) == 0) defeated.Add(people);
                int now = now0;
                old.RemoveAll(g => g.settled || defeated.Contains(g.people) || now - g.tick > 2 * 60 * Day);
                GrudgeTally.Prune(book, now, defeated.Contains);
            }
            else defeated.Remove(people);
            var mine = GrudgeTally.InOrder(book);
            if (mine.Count != old.Count) alike = false;
            else
                for (int i = 0; i < old.Count; i++)
                    if (mine[i].Key != old[i].people || mine[i].Value.name != old[i].name || mine[i].Value.tick != old[i].tick || mine[i].Value.settled != old[i].settled)
                        alike = false;
            foreach (var kv in book)
                if (GrudgeTally.Open(kv.Value) != old.Count(g => g.people == kv.Key && !g.settled))
                    countsAlike = false;
        }
        Check(alike, "four thousand deaths, settlings and tidies: the peoples' grudge ties hold the same dead in the same order as the old list");
        Check(countsAlike, "and each people's count is their dead not yet settled");

        // The revenge picks among the same dead, so the same roll names the same one.
        var pick = GrudgeTally.InOrder(book).Where(kv => !kv.Value.settled).Select(kv => (kv.Key, kv.Value.name)).ToList();
        var oldPick = old.Where(g => !g.settled).Select(g => (g.people, g.name)).ToList();
        Check(pick.SequenceEqual(oldPick), "the dead a revenge can come for are the same, in the same order");

        // An older save's list moves in whole, in its order.
        var oldSave = new List<(int people, string name, int tick, bool settled)> { (4, "Ana", 10, false), (2, "Bo", 20, true), (4, "Cy", 30, false) };
        var moved = new Dictionary<int, List<GrudgeDead>>();
        GrudgeTally.AddOld(moved, oldSave);
        var movedOrder = GrudgeTally.InOrder(moved);
        Check(movedOrder.Count == 3 && movedOrder[0].Key == 4 && movedOrder[0].Value.name == "Ana" && movedOrder[1].Key == 2 && movedOrder[1].Value.settled
              && movedOrder[2].Value.name == "Cy" && moved.Count == 2 && moved[4].Count == 2, "an older save's grudges: one book entry per people, in the old order");
        GrudgeTally.Add(moved, 2, 40, "Di");
        Check(GrudgeTally.InOrder(moved).Last().Value.name == "Di", "a death after the move comes after them");

        // ---------------- Favours: the ledger never refuses one the old store allowed ----------------

        // The act allows one open favour per people and six in all; each is brought or let go (closed), and asked again.
        {
            var ties = new List<Tie>();
            var r = new Random(11);
            bool refused = false, tooMany = false;
            int now = 0, id = 1;
            for (int step = 0; step < 3000; step++)
            {
                now += r.Next(0, Day);
                TieRules.Tidy(ties, now);
                var people = Ref.People(r.Next(1, 9));
                var by = Ref.Person(r.Next(1, 4));
                var live = ties.Where(t => t.kind == Tie.Kind.Favour && t.Live).ToList();
                if (r.Next(2) == 0 && live.Count < 6 && !live.Any(t => t.from == people))
                {
                    if (!TieRules.MakeRoom(ties, Tie.Kind.Favour, people, by)) refused = true;
                    else ties.Add(new Tie { id = id++, kind = Tie.Kind.Favour, from = people, to = by, since = now, until = Tie.Never });
                }
                else if (live.Count > 0)
                    TieRules.Move(live[r.Next(live.Count)], r.Next(2) == 0 ? Tie.State.Kept : Tie.State.Ended, now);
                if (ties.Count(t => t.kind == Tie.Kind.Favour && t.Live) > 6) tooMany = true;
            }
            Check(!refused, "three thousand favours asked, brought and let go: the ledger always has room for one the act allows");
            Check(!tooMany, "and never more than six open");
            var lapse = new Tie { kind = Tie.Kind.Favour, from = Ref.People(1), to = Ref.Person(1), since = 0, until = Tie.Never };
            Check(!TieRules.Expire(lapse, 1000 * Day) && lapse.Live, "a favour has no until: the outsiders' hour lets it go after thirty days, with its message");
            Check(StoreMath.Past(30 * Day + 1, 0, 30f) && !StoreMath.Past(30 * Day, 0, 30f), "let go once more than thirty days have passed, as before");
        }

        // ---------------- Good words: the same words open as the old list ----------------

        // The old list: a word added, the first over eight removed; each hour those more than twenty days old go, and
        // those whose teller is home land. The ledger: a Promise tie with until After(asked, 20), the oldest open past
        // eight ended, the ledger's tidy ending those past their until whenever it runs.
        {
            const float WordDays = 20f;
            var r = new Random(5);
            var oldWords = new List<(int outsider, int asked)>();
            var ties = new List<Tie>();
            bool same = true, refused = false;
            int now = 0, id = 1;
            for (int step = 0; step < 4000; step++)
            {
                now += r.Next(0, Day / 2);
                if (r.Next(3) == 0) TieRules.Tidy(ties, now);
                int x = r.Next(1, 40);
                var open = ties.Where(t => t.Live).ToList();
                int roll = r.Next(4);
                if (roll == 0 && !oldWords.Any(w => w.outsider == x) && !open.Any(t => t.from == Ref.Person(x)))
                {
                    oldWords.Add((x, now));
                    while (oldWords.Count > 8) oldWords.RemoveAt(0);
                    var to = Ref.Person(1000);
                    if (!TieRules.MakeRoom(ties, Tie.Kind.Promise, Ref.Person(x), to)) refused = true;
                    else ties.Add(new Tie { id = id++, kind = Tie.Kind.Promise, from = Ref.Person(x), to = to, since = now, until = StoreMath.After(now, WordDays) });
                    open = ties.Where(t => t.Live).ToList();
                    for (int i = 0; i < open.Count - 8; i++) TieRules.Move(open[i], Tie.State.Ended, now);
                }
                else if (roll == 1)
                {
                    // The hour: lapsed ones go; one whose teller is home lands.
                    int home = r.Next(1, 40);
                    oldWords.RemoveAll(w => StoreMath.Past(now, w.asked, WordDays) || w.outsider == home);
                    foreach (var t in ties.Where(t => t.Live).ToList())
                        if (StoreMath.Past(now, t.since, WordDays)) TieRules.Move(t, Tie.State.Ended, now);
                        else if (t.from == Ref.Person(home)) TieRules.Move(t, Tie.State.Kept, now);
                }
                // What an hour would see: the old list against the ledger's open words not yet past their until.
                var mine = ties.Where(t => TieRules.Live(TieRules.StateAt(t, now))).Select(t => (t.from.id, t.since)).ToList();
                var theirs = oldWords.Where(w => !StoreMath.Past(now, w.asked, WordDays)).ToList();
                if (!mine.SequenceEqual(theirs)) same = false;
            }
            Check(same, "four thousand words given, landed and lapsed: the ledger's open words are the old list's, in order");
            Check(!refused, "and the ledger always had room for one");
        }

        return n;
    }
}
