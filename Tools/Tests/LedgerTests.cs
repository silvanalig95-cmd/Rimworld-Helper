using System;
using System.Collections.Generic;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// The world ledger's pure parts (World/Ledger/LedgerMath.cs, TieMath.cs): refs and their text form, how ties move
/// between states and what their end does, how long closed ties are kept, the bounds per pair and per kind, and the ages.
/// </summary>
public static class LedgerTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void Check(bool ok, string what)
        {
            n++;
            check(ok, "ledger: " + what);
        }

        const int Day = TieRules.DayTicks;

        // ---------------- Refs ----------------

        var pawn = Ref.Person(123);
        var ledgerPerson = Ref.LedgerPerson(4);
        var town = Ref.Town(55);
        var people = Ref.People(3);
        var colony = Ref.Colony(12);
        var house = Ref.House(2);
        Check(pawn.kind == RefKind.Person && pawn.id == 123 && pawn.HasPawn && pawn.Number == 0, "a pawn is a person by its thingIDNumber");
        Check(ledgerPerson.kind == RefKind.Person && ledgerPerson.id == -4 && !ledgerPerson.HasPawn && ledgerPerson.Number == 4,
            "a person with no pawn is their ledger number, stored below zero");
        Check(Ref.Person(4) != Ref.LedgerPerson(4), "pawn 4 and ledger person 4 are two people");
        Check(Ref.Person(0).HasPawn && !Ref.Person(0).IsNone, "pawn 0 is someone");
        Check(house.Number == 2 && town.Number == 0 && people.Number == 0, "a house's number is its ledger number");
        Check(Ref.Person(-1).IsNone && Ref.LedgerPerson(0).IsNone && Ref.LedgerPerson(-3).IsNone, "no person without a pawn id or a ledger number");
        Check(Ref.Town(-1).IsNone && Ref.People(-1).IsNone && Ref.House(0).IsNone && Ref.Colony(-1).IsNone, "the ids nothing can have name nobody");
        Check(!Ref.Colony(-7).IsNone, "a home with no settlement (a negative key) is still a colony");
        Check(Ref.None.IsNone && default(Ref).IsNone && Ref.None == default(Ref), "none is the default");
        Check(new Ref(RefKind.None, 9) == Ref.None, "none has no number");

        // Equality and hashing.
        Check(Ref.Town(55) == town && Ref.Town(55).Equals(town) && Ref.Town(55).Equals((object)town), "the same kind and number are equal");
        Check(Ref.Town(55).GetHashCode() == town.GetHashCode(), "equal refs hash the same");
        Check(Ref.Town(3) != Ref.People(3) && Ref.People(3) != Ref.Colony(3) && Ref.Colony(3) != Ref.House(3) && Ref.House(3) != Ref.Person(3),
            "the same number of two kinds are two things");
        var set = new HashSet<Ref> { Ref.Town(3), Ref.People(3), Ref.Colony(3), Ref.House(3), Ref.Person(3), Ref.LedgerPerson(3), Ref.Town(3) };
        Check(set.Count == 6, "a set of refs keeps each thing once");
        var dict = new Dictionary<Ref, int> { [Ref.People(7)] = 1 };
        Check(dict.ContainsKey(Ref.People(7)) && !dict.ContainsKey(Ref.Town(7)), "refs key a dictionary");
        var sorted = new List<Ref> { Ref.House(1), Ref.Town(9), Ref.Person(5), Ref.Town(2), Ref.LedgerPerson(1) };
        sorted.Sort();
        Check(sorted.SequenceEqual(new[] { Ref.LedgerPerson(1), Ref.Person(5), Ref.Town(2), Ref.Town(9), Ref.House(1) }), "refs sort by kind, then number");

        // The text form.
        Check(pawn.ToString() == "p123" && ledgerPerson.ToString() == "p-4" && town.ToString() == "t55" && people.ToString() == "f3"
              && colony.ToString() == "c12" && house.ToString() == "h2" && Ref.None.ToString() == "-", "the short text form of each kind");
        Check(Ref.Colony(-7).ToString() == "c-7", "a negative colony key keeps its sign");
        var all = new List<Ref>();
        foreach (int id in new[] { 0, 1, 2, 9, 10, 99, 123, 4096, 65535, 1000000, int.MaxValue })
        {
            all.Add(Ref.Person(id));
            all.Add(Ref.LedgerPerson(id));
            all.Add(Ref.Town(id));
            all.Add(Ref.People(id));
            all.Add(Ref.Colony(id));
            all.Add(Ref.Colony(-2 - id));
            all.Add(Ref.House(id));
        }
        all = all.Where(r => !r.IsNone).Distinct().ToList();
        foreach (var r in all)
        {
            Check(Ref.TryParse(r.ToString(), out var back) && back == r, $"{r} reads back as itself");
            Check(Ref.Parse(r.ToString()) == r, $"{r} parses back");
        }
        Check(all.Select(r => r.ToString()).Distinct().Count() == all.Count, "no two refs share a text form");
        foreach (var bad in new[] { null, "", "-", "p", "x5", "P5", "p 5", " p5", "p5 ", "p+5", "p1.5", "p--1", "p-", "p-0", "t-3", "f-1", "h0", "h-2", "c-1",
                     "p99999999999", "5", "t5x", "tt5" })
            Check(!Ref.TryParse(bad, out var r) && r.IsNone && Ref.Parse(bad).IsNone, $"\"{bad ?? "null"}\" is not a ref");

        // ---------------- Tie states ----------------

        var kinds = (Tie.Kind[])Enum.GetValues(typeof(Tie.Kind));
        var states = (Tie.State[])Enum.GetValues(typeof(Tie.State));
        Check(kinds.Length == 14 && kinds.Length == TieRules.Kinds, "fourteen kinds of tie (Secret reserved for stage 2, Scheme for schemes against the colony)");
        Check(TieRules.MaxTies == 1080, "all ties at most 1,080, as stage 2 designed");
        Check(TieRules.Live(Tie.State.Open) && TieRules.Live(Tie.State.CalledIn), "open and called in are live");
        Check(!TieRules.Live(Tie.State.Kept) && !TieRules.Live(Tie.State.Broken) && !TieRules.Live(Tie.State.Ended), "kept, broken and ended are closed");
        foreach (var k in kinds)
        {
            foreach (var s in states)
            {
                Check(!TieRules.CanMove(k, s, s), $"{k}: staying {s} is no move");
                if (!TieRules.Live(s))
                    foreach (var to in states)
                        Check(!TieRules.CanMove(k, s, to), $"{k}: {s} is closed for good (not to {to})");
            }
            Check(TieRules.CanMove(k, Tie.State.Open, Tie.State.Ended) && TieRules.CanMove(k, Tie.State.CalledIn, Tie.State.Ended), $"{k}: anything live can end");
            Check(TieRules.CanMove(k, Tie.State.Open, Tie.State.CalledIn) == TieRules.CanCallIn(k), $"{k}: called in only if it can be asked for");
            Check(TieRules.CanMove(k, Tie.State.Open, Tie.State.Kept) == (k != Tie.Kind.Regard), $"{k}: kept unless a regard");
            Check(TieRules.CanMove(k, Tie.State.Open, Tie.State.Broken) == (k != Tie.Kind.Regard), $"{k}: broken unless a regard");
            Check(!TieRules.CanMove(k, Tie.State.Open, Tie.State.Open), $"{k}: open to open is nothing");
        }
        foreach (var k in new[] { Tie.Kind.Favour, Tie.Kind.Pact, Tie.Kind.Fealty, Tie.Kind.Debt, Tie.Kind.Promise, Tie.Kind.Hostage, Tie.Kind.Claim, Tie.Kind.Secret })
        {
            Check(TieRules.CanCallIn(k), $"{k} can be called in");
            Check(TieRules.CanMove(k, Tie.State.CalledIn, Tie.State.Open), $"{k}: called in can be put off (open again)");
            Check(TieRules.CanMove(k, Tie.State.CalledIn, Tie.State.Kept) && TieRules.CanMove(k, Tie.State.CalledIn, Tie.State.Broken), $"{k}: called in is then kept or broken");
        }
        foreach (var k in new[] { Tie.Kind.Truce, Tie.Kind.Marriage, Tie.Kind.Ward, Tie.Kind.Grudge, Tie.Kind.Regard, Tie.Kind.Scheme })
            Check(!TieRules.CanCallIn(k), $"{k} can't be called in");

        Tie Make(Tie.Kind k, Ref a, Ref b, Tie.State s = Tie.State.Open, int until = Tie.Never, int closed = Tie.Never) =>
            new Tie { kind = k, from = a, to = b, state = s, until = until, closed = closed };

        // Moving.
        var favour = Make(Tie.Kind.Favour, people, pawn);
        Check(TieRules.Move(favour, Tie.State.CalledIn, 100) && favour.state == Tie.State.CalledIn && favour.closed == Tie.Never, "a favour is called in, still live");
        favour.until = 5000;
        Check(TieRules.Move(favour, Tie.State.Open, 200) && favour.state == Tie.State.Open && favour.until == 5000, "put off: open again, with the end given");
        Check(TieRules.Move(favour, Tie.State.Kept, 300) && favour.state == Tie.State.Kept && favour.closed == 300, "kept: closed now");
        Check(!TieRules.Move(favour, Tie.State.Open, 400) && favour.state == Tie.State.Kept && favour.closed == 300, "a kept favour stays kept");
        var calledWithEnd = Make(Tie.Kind.Debt, colony, people, Tie.State.Open, 9000);
        Check(TieRules.Move(calledWithEnd, Tie.State.CalledIn, 10) && calledWithEnd.until == Tie.Never, "called in clears the end (the one asking sets a new one)");
        var regard = Make(Tie.Kind.Regard, pawn, colony);
        Check(!TieRules.Move(regard, Tie.State.Kept, 10) && regard.state == Tie.State.Open, "a regard isn't kept");
        Check(TieRules.Move(regard, Tie.State.Ended, 10) && regard.closed == 10, "a regard ends");
        Check(!TieRules.Move(null, Tie.State.Ended, 10), "no tie, no move");

        // ---------------- Reaching the end ----------------

        foreach (var k in kinds)
        {
            Check(TieRules.AtUntil(k, Tie.State.Open) == (k == Tie.Kind.Debt ? Tie.State.CalledIn : Tie.State.Ended), $"{k}: open at its end {(k == Tie.Kind.Debt ? "comes due" : "ends")}");
            Check(TieRules.AtUntil(k, Tie.State.CalledIn) == Tie.State.Broken, $"{k}: called in and not met by its end is broken");
            foreach (var s in states.Where(s => !TieRules.Live(s)))
                Check(TieRules.AtUntil(k, s) == s, $"{k}: {s} stays {s} at its end");
        }
        var truce = Make(Tie.Kind.Truce, people, colony, Tie.State.Open, 1000);
        Check(!TieRules.Expire(truce, 999) && truce.state == Tie.State.Open, "before its end, nothing");
        Check(TieRules.StateAt(truce, 1000) == Tie.State.Ended && truce.state == Tie.State.Open, "asked at its end, it reads ended (without changing)");
        Check(TieRules.Expire(truce, 1000) && truce.state == Tie.State.Ended && truce.closed == 1000, "at its end a truce ends, closed then");
        Check(!TieRules.Expire(truce, 5000) && truce.closed == 1000, "ended stays ended");
        var debt = Make(Tie.Kind.Debt, colony, people, Tie.State.Open, 2000);
        Check(TieRules.Expire(debt, 2500) && debt.state == Tie.State.CalledIn && debt.until == Tie.Never && debt.closed == Tie.Never, "a debt comes due: called in, live, no end set");
        Check(!TieRules.Expire(debt, 900000), "a called-in debt with no end waits");
        debt.until = 3000;
        Check(TieRules.Expire(debt, 3000) && debt.state == Tie.State.Broken && debt.closed == 3000, "called in and not paid by the end given: broken");
        var forever = Make(Tie.Kind.Marriage, pawn, Ref.Person(9));
        Check(!TieRules.Expire(forever, int.MaxValue) && TieRules.StateAt(forever, int.MaxValue) == Tie.State.Open, "no end set, no end");
        Check(TieRules.Due(10, 10) && !TieRules.Due(10, 9) && !TieRules.Due(Tie.Never, 99), "due at or past the end");
        Check(TieRules.StateAt(null, 0) == Tie.State.Ended, "no tie reads ended");

        // ---------------- Keeping and dropping ----------------

        var ties = new List<Tie>
        {
            Make(Tie.Kind.Favour, people, pawn, Tie.State.Kept, Tie.Never, 0),
            Make(Tie.Kind.Favour, people, pawn, Tie.State.Broken, Tie.Never, 10 * Day),
            Make(Tie.Kind.Grudge, people, colony, Tie.State.Open),
            Make(Tie.Kind.Truce, people, colony, Tie.State.Open, 30 * Day),
            null
        };
        int changed = TieRules.Tidy(ties, 59 * Day);
        Check(ties.Count == 4 && !ties.Contains(null), "nothing closed under 60 days ago is dropped (a null is)");
        Check(ties[3].state == Tie.State.Ended && ties[3].closed == 30 * Day, "tidying applies ends that have come, closed at the end however late it's looked at");
        Check(changed == 2, "tidying counts what changed and went");
        TieRules.Tidy(ties, 60 * Day);
        Check(ties.Count == 3 && ties.All(t => t.closed != 0), "closed 60 days ago: dropped");
        TieRules.Tidy(ties, 90 * Day);
        Check(ties.Count == 1 && ties[0].kind == Tie.Kind.Grudge, "the live are never dropped; the rest after 60 days each");
        Check(!TieRules.Forgotten(Make(Tie.Kind.Debt, colony, people, Tie.State.Ended), int.MaxValue), "closed with no tick noted isn't dropped by age");
        Check(TieRules.Tidy(null, 0) == 0, "no list, nothing");

        // ---------------- Bounds ----------------

        Check(TieRules.MaxPerPairKind == 3 && TieRules.MaxOf(Tie.Kind.Favour) == TieRules.MaxPerKind && TieRules.MaxOf(Tie.Kind.Regard) == TieRules.MaxRegard,
            "the bounds: three a pair and kind, and per kind");
        Check(TieRules.MaxTies == (kinds.Length - 1) * TieRules.MaxPerKind + TieRules.MaxRegard, "all ties at most: each kind's bound added up");
        var b = Ref.Person(77);
        var pair = new List<Tie>
        {
            Make(Tie.Kind.Favour, people, b),
            Make(Tie.Kind.Favour, b, people),
            Make(Tie.Kind.Favour, people, b),
            Make(Tie.Kind.Debt, people, b),
            Make(Tie.Kind.Favour, people, Ref.Person(78))
        };
        Check(!TieRules.MakeRoom(pair, Tie.Kind.Favour, people, b) && pair.Count == 5, "three live favours between two, either way round: no room for a fourth");
        Check(TieRules.MakeRoom(pair, Tie.Kind.Debt, people, b) && pair.Count == 5, "another kind between the same two has room");
        Check(TieRules.MakeRoom(pair, Tie.Kind.Favour, people, Ref.Person(78)) && pair.Count == 5, "the same kind with someone else has room");
        pair[1].state = Tie.State.Kept;
        pair[1].closed = 50;
        pair[2].state = Tie.State.Ended;
        pair[2].closed = 20;
        var older = pair[2];
        var keptOne = pair[1];
        Check(TieRules.MakeRoom(pair, Tie.Kind.Favour, b, people) && pair.Count == 4 && !pair.Contains(older) && pair.Contains(keptOne),
            "a closed one makes room: the one closed longest ago goes");
        var many = new List<Tie>();
        for (int i = 0; i < TieRules.MaxPerKind; i++) many.Add(Make(Tie.Kind.Promise, Ref.Person(1000 + i), colony));
        Check(!TieRules.MakeRoom(many, Tie.Kind.Promise, Ref.Person(5000), colony) && many.Count == TieRules.MaxPerKind, "a kind full of live ties has no room");
        Check(TieRules.MakeRoom(many, Tie.Kind.Pact, Ref.Person(5000), colony), "another kind still has room");
        many[7].state = Tie.State.Ended;
        many[7].closed = 400;
        many[3].state = Tie.State.Broken;
        many[3].closed = 100;
        var first = many[3];
        Check(TieRules.MakeRoom(many, Tie.Kind.Promise, Ref.Person(5000), colony) && many.Count == TieRules.MaxPerKind - 1 && !many.Contains(first),
            "a full kind makes way with its oldest closed tie");
        var regards = new List<Tie>();
        for (int i = 0; i < TieRules.MaxPerKind; i++) regards.Add(Make(Tie.Kind.Regard, Ref.Person(2000 + i), colony));
        Check(TieRules.MakeRoom(regards, Tie.Kind.Regard, Ref.Person(9000), colony), "regard has more room than the other kinds");
        Check(TieRules.MakeRoom(null, Tie.Kind.Favour, people, b), "no list, room");

        // ---------------- A tie's parties ----------------

        var word = Make(Tie.Kind.Promise, Ref.Person(30), pawn);
        word.terms.about = Ref.Person(31);
        Check(word.Involves(Ref.Person(30)) && word.Involves(pawn) && word.Involves(Ref.Person(31)) && !word.Involves(people), "a tie involves its parties and the one it concerns");
        Check(!word.Involves(Ref.None), "nothing is involved in nothing");
        Check(word.Between(pawn, Ref.Person(30)) && word.Between(Ref.Person(30), pawn) && !word.Between(pawn, Ref.Person(31)), "between its two parties, either way round, not the one it concerns");
        Check(!word.IsView && new Tie { source = "outsiders:favour" }.IsView, "a tie read from another store is a view");
        var copy = word.terms.Copy();
        copy.count = 9;
        Check(word.terms.count == 0 && copy.about == word.terms.about, "terms copy apart");

        // ---------------- Ages ----------------

        Check(LedgerMath.AgeOf(0) == WorldAge.Tribal && LedgerMath.AgeOf(1) == WorldAge.Tribal && LedgerMath.AgeOf(2) == WorldAge.Tribal, "neolithic and below: tribal");
        Check(LedgerMath.AgeOf(3) == WorldAge.Medieval, "medieval");
        Check(LedgerMath.AgeOf(4) == WorldAge.Industrial, "industrial");
        Check(LedgerMath.AgeOf(5) == WorldAge.Spacer && LedgerMath.AgeOf(6) == WorldAge.Spacer && LedgerMath.AgeOf(7) == WorldAge.Spacer, "spacer and above: spacer");
        Check(LedgerMath.AgeOf(NotableMath.Neolithic) == WorldAge.Tribal && LedgerMath.AgeOf(NotableMath.Medieval) == WorldAge.Medieval
              && LedgerMath.AgeOf(NotableMath.Industrial) == WorldAge.Industrial && LedgerMath.AgeOf(NotableMath.Spacer) == WorldAge.Spacer,
            "the ages read the tech levels as the notables' titles do");

        return n;
    }
}
