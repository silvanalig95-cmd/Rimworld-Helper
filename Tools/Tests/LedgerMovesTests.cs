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

        // ---------------- Guest kin: a family's claim ----------------

        {
            const int Window = 15;
            Check(TieRules.CanCallIn(Tie.Kind.Claim), "a claim can be called in: the family comes to ask");
            int due = 40 * Day;
            var claim = new Tie { kind = Tie.Kind.Claim, from = Ref.People(3), to = Ref.Person(9), since = 20 * Day, until = StoreMath.After(due, Window) };
            claim.terms.count = due;
            claim.terms.about = Ref.Colony(12);
            Check(TieRules.StateAt(claim, due + Window * Day) == Tie.State.Open, "on the last tick of the window the family may still come");
            Check(TieRules.StateAt(claim, due + Window * Day + 1) == Tie.State.Ended, "past the window, never: ended (the old store dropped it then)");
            Check(TieRules.Move(claim, Tie.State.CalledIn, due + Day) && claim.until == Tie.Never, "the family comes: called in");
            claim.until = due + Day + Window * Day;
            var refused = new Tie { kind = Tie.Kind.Claim, state = Tie.State.CalledIn, until = claim.until };
            Check(TieRules.Move(claim, Tie.State.Kept, due + 2 * Day) && claim.closed == due + 2 * Day, "they go home with them: kept");
            Check(TieRules.Expire(refused, refused.until) && refused.state == Tie.State.Broken, "the council keeps them: a fortnight on, broken");
            var lapsed = new Tie { kind = Tie.Kind.Claim, state = Tie.State.CalledIn };
            Check(TieRules.Move(lapsed, Tie.State.Ended, 5), "nobody came after all: ended");
            var many = new List<Tie>();
            bool room = true;
            for (int i = 0; i < 12; i++)
            {
                if (!TieRules.MakeRoom(many, Tie.Kind.Claim, Ref.People(3), Ref.Person(100 + i))) room = false;
                many.Add(new Tie { kind = Tie.Kind.Claim, from = Ref.People(3), to = Ref.Person(100 + i) });
            }
            Check(room, "a claim is on the one who joined: twelve from one people all have room");

            // The old list (CouncilGameComponent.guestKin) against the ledger's open claims: joined (again: the old one
            // goes), each hour the gone and the lapsed go, and the first whose time has come and who can be asked is.
            var r = new Random(3);
            var oldKin = new List<(int pawn, int due)>();
            var ties = new List<Tie>();
            bool same = true;
            int now = 0;
            for (int step = 0; step < 3000; step++)
            {
                now += r.Next(0, Day / 3);
                if (r.Next(4) == 0) TieRules.Tidy(ties, now);
                int p = r.Next(1, 12);
                if (r.Next(3) == 0)
                {
                    int d = now + r.Next(10, 31) * Day;
                    oldKin.RemoveAll(k => k.pawn == p);
                    oldKin.Add((p, d));
                    foreach (var t in ties.Where(t => t.state == Tie.State.Open && t.to == Ref.Person(p))) TieRules.Move(t, Tie.State.Ended, now);
                    var t2 = new Tie { kind = Tie.Kind.Claim, from = Ref.People(1), to = Ref.Person(p), since = now, until = StoreMath.After(d, Window) };
                    t2.terms.count = d;
                    ties.Add(t2);
                }
                else
                {
                    int gone = r.Next(1, 30);
                    oldKin.RemoveAll(k => k.pawn == gone || now > k.due + Window * Day);
                    foreach (var t in ties.Where(t => t.state == Tie.State.Open).ToList())
                        if (t.to == Ref.Person(gone) || now > t.terms.count + Window * Day) TieRules.Move(t, Tie.State.Ended, now);
                    int ready = r.Next(1, 12);
                    var o = oldKin.Where(k => now >= k.due).FirstOrDefault(k => k.pawn == ready);
                    if (o.pawn != 0) oldKin.Remove(o);
                    var mine = ties.Where(t => t.state == Tie.State.Open && now >= t.terms.count).FirstOrDefault(t => t.to == Ref.Person(ready));
                    if (mine != null) TieRules.Move(mine, Tie.State.CalledIn, now);
                    if ((o.pawn != 0) != (mine != null)) same = false;
                }
                var open = ties.Where(t => t.state == Tie.State.Open && TieRules.StateAt(t, now) == Tie.State.Open).Select(t => (t.to.id, t.terms.count)).ToList();
                var theirs = oldKin.Where(k => now <= k.due + Window * Day).ToList();
                if (!open.SequenceEqual(theirs)) same = false;
            }
            Check(same, "three thousand joinings and hours: the ledger's open claims are the old list's, and the same one is pressed");
        }

        // ---------------- The scenario's debt ----------------

        {
            var debt = new Tie { kind = Tie.Kind.Debt, from = Ref.Colony(12), to = Ref.People(4), since = 0, until = 30 * Day };
            debt.terms.goods = "Silver";
            debt.terms.count = 3000;
            Check(TieRules.StateAt(debt, 30 * Day - 1) == Tie.State.Open, "owed, not yet due");
            Check(TieRules.Expire(debt, 30 * Day) && debt.state == Tie.State.CalledIn && debt.until == Tie.Never, "at its day it comes due: called in, with no end");
            Check(!TieRules.Expire(debt, 900 * Day) && debt.Live, "called in, it waits for the council's answer however long the letter takes (the memo's next look retries)");
            // Half paid: more owed, put off once more, due again.
            Check(TieRules.Move(debt, Tie.State.Open, 31 * Day), "half paid: open again");
            debt.terms.count = 2030;
            debt.terms.number += 1;
            debt.until = 52 * Day;
            Check(debt.terms.count == 2030 && (int)debt.terms.number == 1 && TieRules.StateAt(debt, 52 * Day) == Tie.State.CalledIn, "more owed, put off once, due again at its new day");
            TieRules.Expire(debt, 52 * Day);
            var refusedDebt = new Tie { kind = Tie.Kind.Debt, state = Tie.State.CalledIn };
            Check(TieRules.Move(debt, Tie.State.Kept, 53 * Day) && TieRules.Move(refusedDebt, Tie.State.Broken, 53 * Day), "paid: kept; refused: broken");
            // A live debt never closes by itself (only the scenario closes it, as it uses the memo): the two stay in step.
            var r = new Random(9);
            bool closesAlone = false;
            for (int i = 0; i < 500; i++)
            {
                var t = new Tie { kind = Tie.Kind.Debt, from = Ref.Colony(1), to = Ref.People(2), state = r.Next(2) == 0 ? Tie.State.Open : Tie.State.CalledIn };
                t.until = t.state == Tie.State.Open ? r.Next(0, 100) * Day : Tie.Never;
                var list = new List<Tie> { t };
                TieRules.Tidy(list, r.Next(0, 200) * Day);
                if (!t.Live || list.Count == 0) closesAlone = true;
            }
            Check(!closesAlone, "a debt open or called in is still live after any tidy: unused memo, live tie");
        }

        // ---------------- The grant of land: fealty ----------------

        {
            var fealty = new Tie { kind = Tie.Kind.Fealty, from = Ref.Colony(12), to = Ref.People(4), since = 5 * Day };
            fealty.terms.key = "granted";
            fealty.terms.count = 25;
            var list = new List<Tie> { fealty };
            TieRules.Tidy(list, 5000 * Day);
            Check(list.Count == 1 && fealty.state == Tie.State.Open, "the colony holds its land by their grant for as long as it lasts: no end of its own");
            Check(TieRules.CanCallIn(Tie.Kind.Fealty), "their grant can be called in, as the deed they remember says");
            Check(TieRules.MakeRoom(list, Tie.Kind.Fealty, Ref.Colony(12), Ref.People(5)), "a grant of another people has room beside it");
        }

        // ---------------- Regard ----------------

        {
            // A notable's own regard of the colony and a regular's bond are the ledger's: one each, far inside its room
            // for regard even if every notable lingered three times over.
            int own = NotableMath.MaxNotables + RegularMath.KeepMax + 1;
            Check(NotableMath.MaxNotables == 42 && 3 * own <= TieRules.MaxRegard, "the notables' own regard and the regulars' bonds fit the ledger's regard three times over (two new towns' four included)");
            // The notables' regard of each colonist stays with the notable: with it the ledger could run out of room.
            int withColonists = NotableMath.MaxNotables * (1 + NotableMath.MaxRegards) + RegularMath.KeepMax + 1;
            Check(withColonists > TieRules.MaxRegard, "the notables' regard of eight colonists each would pass the ledger's three hundred: it stays a view");
            // Feeding and fading are the notables' own: the tie only holds the score.
            var reg = new Tie { kind = Tie.Kind.Regard, from = Ref.Person(5), to = Ref.Colony(12) };
            reg.terms.count = 7;
            float field = 0f;
            var rr = new Random(13);
            bool sameScore = true;
            for (int i = 0; i < 1000; i++)
            {
                float add = (float)(rr.NextDouble() * 40 - 20);
                if (rr.Next(3) == 0)
                {
                    float days = rr.Next(1, 5);
                    field = NotableMath.Fade(field, days);
                    reg.terms.number = NotableMath.Fade(reg.terms.number, days);
                }
                else
                {
                    field += add;
                    reg.terms.number += add;
                }
                if (field != reg.terms.number) sameScore = false;
            }
            Check(sameScore, "a regard fed and faded a thousand times holds the same score as the field it replaced");
            Check(!TieRules.CanCallIn(Tie.Kind.Regard) && !TieRules.Expire(reg, int.MaxValue) && reg.Live, "a regard has no end of its own: it ends when its notable or regular goes");
            var regards = new List<Tie>();
            bool room = true;
            for (int i = 0; i < own; i++)
            {
                if (!TieRules.MakeRoom(regards, Tie.Kind.Regard, Ref.Person(100 + i), Ref.Colony(12))) room = false;
                regards.Add(new Tie { kind = Tie.Kind.Regard, from = Ref.Person(100 + i), to = Ref.Colony(12) });
            }
            Check(room, "every notable and regular has room for their regard");
        }

        // ---------------- One way to keep someone in the world ----------------

        {
            var kept = new List<string>();
            Check(LedgerMath.AddReason(kept, "notable") && !LedgerMath.AddReason(kept, "notable") && kept.Count == 1, "a reason is kept once");
            Check(!LedgerMath.AddReason(kept, null) && !LedgerMath.AddReason(kept, "") && !LedgerMath.AddReason(null, "memo"), "no reason, nothing kept");
            Check(LedgerMath.Held(kept, false) && !LedgerMath.Held(new List<string>(), false) && LedgerMath.Held(new List<string>(), true) && !LedgerMath.Held(null, false),
                "held while a reason holds, or something outside the ledger holds them");
            Check(!LedgerMath.HeldBesides(kept, "notable", false) && LedgerMath.HeldBesides(kept, "memo", false) && LedgerMath.HeldBesides(kept, "notable", true),
                "held besides one reason: by another, or from outside");
            Check(LedgerMath.RemoveReason(kept, "notable") && !LedgerMath.RemoveReason(kept, "notable") && !LedgerMath.Held(kept, false), "taken back once, and then nothing holds them");

            // Seven parts keep a few people for their own reasons and let go in any order: someone is let go exactly when
            // the last reason goes, never while another still holds them.
            string[] ways = { "notable", "memo", "departed", "regular", "camp", "chain", "situation:3" };
            var r = new Random(17);
            var reasons = new Dictionary<int, List<string>>();
            var truth = new Dictionary<int, HashSet<string>>();
            bool rightAlways = true, letGoWhileHeld = false;
            for (int step = 0; step < 5000; step++)
            {
                int who = r.Next(1, 6);
                string why = ways[r.Next(ways.Length)];
                if (!reasons.TryGetValue(who, out var list)) reasons[who] = list = new List<string>();
                if (!truth.TryGetValue(who, out var set)) truth[who] = set = new HashSet<string>();
                if (r.Next(2) == 0)
                {
                    LedgerMath.AddReason(list, why);
                    set.Add(why);
                }
                else
                {
                    LedgerMath.RemoveReason(list, why);
                    set.Remove(why);
                    bool free = !LedgerMath.Held(list, false);
                    if (free != (set.Count == 0)) rightAlways = false;
                    if (free && set.Count > 0) letGoWhileHeld = true;
                }
            }
            Check(rightAlways && !letGoWhileHeld, "five thousand keeps and lets-go by seven parts: let go exactly when the last reason goes");
        }

        return n;
    }
}
