using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Debts of honour (docs/design/stage2-diplomacy.md, round a, item 1), as the world ledger keeps them: Favour ties of
    /// origin "debts" (FavourMath.Origin). When the colony does a people a good turn of size 10 or more in their memory, they
    /// owe the colony (from their people to the colony that did it, about the colonist who did it); when they do the colony
    /// one, the colony owes them. One open each way per people and colony: a new good turn renews it. A notable helped in
    /// person (freed, tended, hosted) owes the colonist who did it, twelve such at most. Each is let go two years after the
    /// turn that made or renewed it, if nobody asks. The letter that tells of the good turn says so; when none comes within
    /// two hours the line comes in the evening's news. Calling one in: FavourCalls.cs (the colony) and FavourAsks.cs
    /// (theirs). Nothing is saved here but the ties; the lines waiting for a letter are a session's.
    /// </summary>
    public static class FavoursOwed
    {
        public const string Origin = FavourMath.Origin;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The setting, and a memory to make debts from.</summary>
        public static bool On => Diplomacy.FavoursOn && FactionMemory.On;

        // ================= Reading =================

        /// <summary>Every debt of honour still open or called in, oldest first.</summary>
        public static List<Tie> Live()
        {
            var list = new List<Tie>();
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Favour))
                if (t != null && t.Live && t.origin == Origin)
                    list.Add(t);
            return list;
        }

        public static Tie Get(int id)
        {
            var t = WorldLedger.Get(id);
            return t != null && t.kind == Tie.Kind.Favour && t.origin == Origin ? t : null;
        }

        /// <summary>A person owes a person: a notable's personal favour to a colonist.</summary>
        public static bool IsPersonal(Tie t) => t != null && FavourMath.Personal(t.from, t.to);

        /// <summary>The colony owes it to a people.</summary>
        public static bool ColonyOwes(Tie t) => t != null && FavourMath.ColonyOwes(t.from, t.to);

        /// <summary>The notable who owes a personal favour (null for a people's debt, or one no longer a notable).</summary>
        public static Notable NotableOf(Tie t)
        {
            if (t == null || t.from.kind != RefKind.Person) return null;
            return NotableBook.Instance?.notables.FirstOrDefault(n => n != null && n.Person == t.from);
        }

        /// <summary>The people a debt is with: the one that owes, the one owed, or a notable's people.</summary>
        public static Faction PeopleOf(Tie t)
        {
            if (t == null) return null;
            if (t.from.kind == RefKind.People) return Refs.Faction(t.from);
            if (t.to.kind == RefKind.People) return Refs.Faction(t.to);
            return NotableOf(t)?.faction ?? WorldLedger.PersonRecord(t.from)?.people;
        }

        /// <summary>The colonist it is about: owed in person, or the one who did the good turn.</summary>
        public static Pawn Colonist(Tie t) => t == null ? null : IsPersonal(t) ? WorldLedger.PawnOf(t.to) : WorldLedger.PawnOf(t.terms.about);

        /// <summary>The home whose debt it is: the colony on either end, the home of the colonist owed, else the seat.</summary>
        public static Map HomeOf(Tie t)
        {
            if (t == null) return Homes.Seat;
            if (t.to.kind == RefKind.Colony) return Diplomacy.HomeOf(t.to);
            if (t.from.kind == RefKind.Colony) return Diplomacy.HomeOf(t.from);
            var c = WorldLedger.PawnOf(t.to);
            return c != null ? Homes.Of(c) ?? Homes.Seat : Homes.Seat;
        }

        /// <summary>What a people (and its notables in person) owe the colony.</summary>
        public static List<Tie> OwedBy(Faction f)
        {
            var list = new List<Tie>();
            if (f == null) return list;
            foreach (var t in Live())
                if (!ColonyOwes(t) && PeopleOf(t) == f)
                    list.Add(t);
            return list;
        }

        /// <summary>What the colony owes a people.</summary>
        public static List<Tie> OwedTo(Faction f)
        {
            var list = new List<Tie>();
            if (f == null) return list;
            var r = Refs.Of(f);
            foreach (var t in Live())
                if (ColonyOwes(t) && t.to == r)
                    list.Add(t);
            return list;
        }

        private static Tie Open(Ref from, Ref to)
        {
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Favour))
                if (t != null && t.state == Tie.State.Open && t.origin == Origin && t.from == from && t.to == to)
                    return t;
            return null;
        }

        /// <summary>The colony a good turn belongs to: the home of the colonist who did it, else the seat.</summary>
        private static Ref ColonyOf(Pawn by)
        {
            var home = by != null && KinMemory.Ours(by) ? Homes.Of(by) : null;
            return Diplomacy.Colony(home);
        }

        // ================= Making them =================

        /// <summary>
        /// A deed the memory just noted (FactionMemory.Note): one that answers a debt of the colony's called in keeps it
        /// (FavourAsks); a good turn big enough makes a debt, or renews the one open the same way.
        /// </summary>
        public static void Noted(FactionDeed d)
        {
            if (d?.faction == null || !On || !Diplomacy.Deals(d.faction)) return;
            try
            {
                if (FavourAsks.Answered(d)) return;
                bool ours = d.side == DeedSide.Favor;
                if (!ours && d.side != DeedSide.TheirFavor) return;
                if (!FavourMath.Earns(d.kind, ours, d.weight)) return;
                // A captive paid for or exchanged leaving healthy earns no debt of honour (Diplomacy/Ransoms.cs).
                if (ours && d.kind == "released" && Ransoms.PaidFor(d.faction, d.name)) return;
                var people = Refs.Of(d.faction);
                var colony = ColonyOf(ours ? d.by : null);
                if (people.IsNone || colony.IsNone) return;
                var by = ours && KinMemory.Ours(d.by) ? d.by : null;
                bool made = MakeOrRenew(ours ? people : colony, ours ? colony : people, d, by, out var t);
                if (!made || t == null) return;
                var home = Diplomacy.HomeOf(colony);
                var form = DipWords.Form(home, d.faction);
                string line = ours
                    ? DipWords.T("CC_Dip_Line_TheyOwe", DipWords.People(d.faction).Named("PEOPLE"), form.word.Named("WORD"), form.rite.Named("RITE"), DipWords.Why(d.kind).Named("WHY"))
                    : DipWords.T("CC_Dip_Line_WeOwe", DipWords.People(d.faction).Named("PEOPLE"), form.word.Named("WORD"), form.rite.Named("RITE"), DipWords.Why(d.kind).Named("WHY"));
                Say(d.faction, home, line);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("favours owed: a deed", e);
            }
        }

        /// <summary>
        /// A notable of theirs helped in person by a colonist (freed, tended; hosted or walked home by a mission that names
        /// them): the notable owes that colonist. <paramref name="helped"/>: the one helped, when known; else the notable
        /// the deed names.
        /// </summary>
        public static void Personal(FactionDeed d, Pawn helped, Pawn colonist)
        {
            if (d?.faction == null || !On || !KinMemory.Ours(colonist) || d.side != DeedSide.Favor || !FavourMath.Earns(d.kind, true, d.weight)) return;
            try
            {
                // The people's debt for this good turn names who did it (the memory names them only after noting the deed).
                var people = Refs.Of(d.faction);
                foreach (var t0 in WorldLedger.OwnTies(Tie.Kind.Favour))
                    if (t0 != null && t0.state == Tie.State.Open && t0.origin == Origin && t0.from == people && t0.terms.key == d.kind && t0.terms.about.IsNone)
                    {
                        t0.terms.about = WorldLedger.PersonOf(colonist);
                        WorldLedger.Touch(t0);
                        break;
                    }
                var book = NotableBook.Instance;
                if (book == null || !NotableBook.On) return;
                var n = helped != null
                    ? book.notables.FirstOrDefault(x => x != null && x.faction == d.faction && x.pawn == helped)
                    : d.name.NullOrEmpty() ? null : book.notables.FirstOrDefault(x => x != null && x.faction == d.faction && x.Alive && x.Name == d.name);
                if (n == null || !n.Alive) return;
                var from = n.Person;
                var to = WorldLedger.PersonOf(colonist);
                if (from.IsNone || to.IsNone) return;
                bool made = MakeOrRenew(from, to, d, colonist, out var t);
                if (!made || t == null) return;
                // Twelve personal favours at most: the oldest let go.
                var personal = Live().Where(IsPersonal).OrderBy(x => x.since).ToList();
                for (int i = 0; i < personal.Count - FavourMath.MaxPersonal; i++) WorldLedger.Set(personal[i], Tie.State.Ended);
                string line = DipWords.T("CC_Dip_Line_Personal", NotableBook.Called(n).Named("NOTABLE"), n.Name.Named("NAME"), colonist.Named("PAWN"),
                    DipWords.Why(d.kind).Named("WHY"));
                Say(d.faction, Homes.Of(colonist) ?? Homes.Seat, line);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("favours owed: in person", e);
            }
        }

        /// <summary>A new debt from one to the other, or the open one renewed (its turn, size and doer the newest). True when it is new.</summary>
        private static bool MakeOrRenew(Ref from, Ref to, FactionDeed d, Pawn by, out Tie t)
        {
            int now = Now;
            t = Open(from, to);
            var about = by != null ? WorldLedger.PersonOf(by) : Ref.None;
            if (t != null)
            {
                t.until = FavourMath.Until(now);
                t.terms.key = d.kind;
                t.terms.number = Math.Max(t.terms.number, d.weight);
                if (!about.IsNone) t.terms.about = about;
                WorldLedger.Touch(t);
                return false;
            }
            // Already called in the same way (asked and not yet answered): no second one beside it.
            if (WorldLedger.OwnTies(Tie.Kind.Favour).Any(x => x != null && x.state == Tie.State.CalledIn && x.origin == Origin && x.from == from && x.to == to)) return false;
            var terms = new TieTerms { key = d.kind, number = d.weight, about = about };
            t = WorldLedger.AddTie(Tie.Kind.Favour, from, to, terms, FavourMath.Until(now), Origin);
            return t != null;
        }

        /// <summary>A debt made by a dev action or a test of the game: from a people to the colony, or the other way.</summary>
        internal static Tie Make(Faction f, Map home, bool theyOwe, string kind)
        {
            if (f == null) return null;
            var people = Refs.Of(f);
            var colony = Diplomacy.Colony(home);
            var d = new FactionDeed { faction = f, kind = kind, weight = FavourMath.MinSize, side = theyOwe ? DeedSide.Favor : DeedSide.TheirFavor };
            MakeOrRenew(theyOwe ? people : colony, theyOwe ? colony : people, d, null, out var t);
            return t;
        }

        // ================= Closing them =================

        /// <summary>Asked for now: what was asked in its terms (and by when, for one of theirs), and the history told.</summary>
        public static bool CallIn(Tie t, string code, float dueDay = 0f)
        {
            if (t == null || t.state != Tie.State.Open) return false;
            if (!WorldLedger.Set(t, Tie.State.CalledIn)) return false;
            t.terms.goods = code;
            t.terms.days = dueDay;
            WorldLedger.Touch(t);
            return true;
        }

        /// <summary>Kept (honoured), broken (refused or left unanswered), or put off (asked of no one after all).</summary>
        public static void Close(Tie t, Tie.State state)
        {
            if (t == null || !t.Live) return;
            if (state == Tie.State.Open)
            {
                if (t.state == Tie.State.CalledIn && WorldLedger.Set(t, Tie.State.Open))
                {
                    t.terms.goods = null;
                    t.terms.days = 0f;
                    t.until = FavourMath.Until(Now);
                    WorldLedger.Touch(t);
                }
                return;
            }
            if (t.state == Tie.State.Open && (state == Tie.State.Kept || state == Tie.State.Broken)) WorldLedger.Set(t, Tie.State.CalledIn);
            WorldLedger.Set(t, state);
        }

        // ================= The line in the letter that tells it =================

        private sealed class Waiting
        {
            public Faction people;
            public Map home;
            public string line;
            public int tick;
        }

        private static readonly List<Waiting> waiting = new List<Waiting>();
        private static Game waitingGame;

        /// <summary>A letter about the good turn that came within this long of it carries the line.</summary>
        private const int LetterTicks = 2 * 2500;

        /// <summary>While set, a debt made is not told on its own: the caller's own words say it.</summary>
        private static bool quiet;

        /// <summary>
        /// Notes a deed whose debt is told in the caller's own words (a letter's answer that says they owe the colony,
        /// World/NeighbourLetters.cs): the debt is made as always, only not told a second time.
        /// </summary>
        public static void Quietly(Action note)
        {
            bool was = quiet;
            quiet = true;
            try
            {
                note?.Invoke();
            }
            finally
            {
                quiet = was;
            }
        }

        /// <summary>The line goes into the letter that tells the good turn (one about their people, in the last hours), else the evening's news.</summary>
        private static void Say(Faction f, Map home, string line)
        {
            if (line.NullOrEmpty() || quiet) return;
            if (waitingGame != Current.Game)
            {
                waitingGame = Current.Game;
                waiting.Clear();
            }
            var w = new Waiting { people = f, home = home, line = line, tick = Now };
            if (!Append(w)) waiting.Add(w);
        }

        /// <summary>Lines still waiting for their letter: added when it has come, or told as news once it is clearly not coming.</summary>
        public static void Hourly(int now)
        {
            if (waiting.Count == 0) return;
            if (waitingGame != Current.Game)
            {
                waitingGame = Current.Game;
                waiting.Clear();
                return;
            }
            foreach (var w in waiting.ToList())
            {
                if (Append(w))
                {
                    waiting.Remove(w);
                    continue;
                }
                if (now - w.tick < LetterTicks) continue;
                waiting.Remove(w);
                // apart: what their people now owe, told in the news (no letter came about the good turn).
                Tell.Minor(NewsKind.World, Importance.Notable, w.home, w.line, LookTargets.Invalid);
            }
        }

        /// <summary>The newest letter about their people that came within the last hours gets the line at its end.</summary>
        private static bool Append(Waiting w)
        {
            try
            {
                var stack = Find.LetterStack?.LettersListForReading;
                if (stack == null) return false;
                int now = Now;
                for (int i = stack.Count - 1; i >= 0; i--)
                {
                    if (!(stack[i] is ChoiceLetter c) || c.arrivalTick < w.tick - LetterTicks || c.arrivalTick > now) continue;
                    string text = c.Text.RawText ?? "";
                    bool about = c.relatedFaction == w.people || w.people != null && (text.Contains(w.people.Name) || (c.Label.RawText ?? "").Contains(w.people.Name));
                    if (!about || text.Contains(w.line)) continue;
                    c.Text = text + "\n\n" + w.line;
                    return true;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("favours owed: the letter's line", e);
            }
            return false;
        }

        /// <summary>The line said now with a letter that has just come (their call through a mission), else held as above.</summary>
        internal static void SayWithLetter(Faction f, Map home, string line) => Say(f, home, line);
    }
}
