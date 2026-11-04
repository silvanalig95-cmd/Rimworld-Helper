using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The world ledger: one way to name anything out in the world (<see cref="Ref"/>), one record for every tie between
    /// two parties (<see cref="Tie"/>), and one place to ask about a town, a people or a person (<see cref="Town"/>,
    /// <see cref="People"/>, <see cref="Person"/>, in LedgerViews.cs). It saves its own ties (grudges, favours, good
    /// words, claims, the scenario's debt, fealty, a notable's and a regular's regard: each kept through a small helper
    /// beside the system that uses it), the persons and towns it knows (LedgerPersons.cs), why each person is kept in the
    /// world (LedgerKeep.cs), and the counter for ledger numbers. The rest it reads from the stores that keep it (the
    /// notables, what the peoples remember, the outsiders, the world situations), and the ties still kept there (a
    /// vendetta, the regard kept with its records) come back from <see cref="TiesOf"/> as views (LedgerToday.cs), marked
    /// and not editable here. New features about the world read and write through it. No pass of its own: its upkeep
    /// (ties reaching their end, closed ones dropped, and once a day persons, towns and reasons nothing needs) runs when
    /// it's asked, at most once a 250-tick stretch, and before saving.
    /// </summary>
    public partial class WorldLedger : GameComponent
    {
        private List<Tie> ties = new List<Tie>();
        private int nextTie = 1;
        /// <summary>The next ledger number (for a person with no pawn, or a house).</summary>
        private int nextNumber = 1;

        [Unsaved] private int tidiedAt = Fresh.Never;

        private static WorldLedger cached;
        private static Game cachedGame;
        private static int version;

        /// <summary>Bumped on every change to the ledger's own ties.</summary>
        public static int Version => version;

        private static void Changed() => version++;

        public WorldLedger(Game game)
        {
        }

        public static WorldLedger Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<WorldLedger>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            AfterLoadKeeps();
            Changed();
        }

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ---------------- Ledger numbers ----------------

        /// <summary>A new ledger number, for someone with no pawn or a house.</summary>
        public int NewNumber() => nextNumber++;

        /// <summary>A new person the ledger keeps with no pawn.</summary>
        public static Ref NewPerson()
        {
            var l = Instance;
            return l == null ? Ref.None : Ref.LedgerPerson(l.NewNumber());
        }

        /// <summary>A new house (a family line).</summary>
        public static Ref NewHouse()
        {
            var l = Instance;
            return l == null ? Ref.None : Ref.House(l.NewNumber());
        }

        // ---------------- Ties ----------------

        /// <summary>
        /// A new tie between two parties, open from now: null without a game, with a party missing or the same on both
        /// sides, or with no room (all ties of its kind between the two, or of its kind in all, still live).
        /// </summary>
        public static Tie AddTie(Tie.Kind kind, Ref from, Ref to, TieTerms terms = null, int until = Tie.Never, string origin = null)
        {
            var l = Instance;
            if (l == null || from.IsNone || to.IsNone || from == to) return null;
            try
            {
                int now = Now;
                l.Tidy(now);
                if (!TieRules.MakeRoom(l.ties, kind, from, to))
                {
                    Changed();
                    return null;
                }
                var t = new Tie { id = l.nextTie++, kind = kind, from = from, to = to, terms = terms?.Copy() ?? new TieTerms(), since = now, until = until, origin = origin };
                l.ties.Add(t);
                Changed();
                return t;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world ledger: a new tie", e);
                return null;
            }
        }

        /// <summary>
        /// Moves one of the ledger's own ties to a state, if its kind allows it from where it stands (TieRules.CanMove).
        /// A view of another store's tie can't be moved here. Returns whether it moved.
        /// </summary>
        public static bool Set(Tie tie, Tie.State state)
        {
            var l = Instance;
            if (l == null || tie == null || tie.IsView || !l.ties.Contains(tie)) return false;
            int now = Now;
            l.Tidy(now);
            if (!TieRules.Move(tie, state, now)) return false;
            Changed();
            return true;
        }

        /// <summary>
        /// A tie from a store that kept it before the ledger did (read from an older save), with its own since, until and
        /// state: given an id and kept, or null without a game, with a party missing or with no room.
        /// </summary>
        public static Tie Adopt(Tie t)
        {
            var l = Instance;
            if (l == null || t == null || t.from.IsNone || t.to.IsNone || t.from == t.to) return null;
            try
            {
                if (!TieRules.MakeRoom(l.ties, t.kind, t.from, t.to))
                {
                    CouncilLog.WarnOnce("world ledger: no room for an older save's " + t.kind + " (" + t.from + ">" + t.to + ")");
                    return null;
                }
                t.id = l.nextTie++;
                t.terms ??= new TieTerms();
                t.source = null;
                if (!TieRules.Live(t.state) && t.closed == Tie.Never) t.closed = Now;
                l.ties.Add(t);
                Changed();
                return t;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world ledger: an older save's tie", e);
                return null;
            }
        }

        /// <summary>
        /// The ledger's own ties of a kind, live and closed, oldest first (the order they were made): for the store that
        /// keeps its ties here. The list itself, not a copy: read it, don't change it.
        /// </summary>
        public static IEnumerable<Tie> OwnTies(Tie.Kind kind)
        {
            var l = Instance;
            if (l == null) yield break;
            for (int i = 0; i < l.ties.Count; i++)
                if (l.ties[i] != null && l.ties[i].kind == kind)
                    yield return l.ties[i];
        }

        /// <summary>The ledger's own live ties of a kind, oldest first: a new list.</summary>
        public static List<Tie> LiveTies(Tie.Kind kind)
        {
            var list = new List<Tie>();
            foreach (var t in OwnTies(kind))
                if (t.Live)
                    list.Add(t);
            return list;
        }

        /// <summary>One of the ledger's own ties had its terms or its until changed by the store that keeps it there.</summary>
        public static void Touch(Tie tie)
        {
            if (tie != null && !tie.IsView) Changed();
        }

        /// <summary>One of the ledger's own ties by its id, or null.</summary>
        public static Tie Get(int id)
        {
            var l = Instance;
            if (l == null || id <= 0) return null;
            for (int i = 0; i < l.ties.Count; i++)
                if (l.ties[i].id == id)
                    return l.ties[i];
            return null;
        }

        /// <summary>
        /// Every tie this party is in (from, to, or the one it concerns): the ledger's own first, then views of those kept
        /// in today's other stores (<see cref="Tie.IsView"/>). A new list each time.
        /// </summary>
        public static List<Tie> TiesOf(Ref r)
        {
            var list = new List<Tie>();
            if (r.IsNone) return list;
            var l = Instance;
            if (l != null)
            {
                l.Tidy(Now);
                foreach (var t in l.ties)
                    if (t.Involves(r))
                        list.Add(t);
            }
            foreach (var t in Today())
                if (t.Involves(r))
                    list.Add(t);
            return list;
        }

        /// <summary>The ties between two parties, either way round: the ledger's own first, then views. A new list each time.</summary>
        public static List<Tie> TiesBetween(Ref a, Ref b)
        {
            var list = new List<Tie>();
            if (a.IsNone || b.IsNone) return list;
            var l = Instance;
            if (l != null)
            {
                l.Tidy(Now);
                foreach (var t in l.ties)
                    if (t.Between(a, b))
                        list.Add(t);
            }
            foreach (var t in Today())
                if (t.Between(a, b))
                    list.Add(t);
            return list;
        }

        /// <summary>The ledger's own ties, for the dev window and the tests of a running game (read only).</summary>
        public IReadOnlyList<Tie> Own => ties;

        /// <summary>Ties reaching their end, and closed ones dropped after their keeping: at most once a 250-tick stretch.</summary>
        private void Tidy(int now)
        {
            if (Fresh.Holds(tidiedAt, now, Sched.RareTicks)) return;
            tidiedAt = now;
            try
            {
                if (TieRules.Tidy(ties, now) > 0) Changed();
                PruneKnown(now, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world ledger: upkeep", e);
            }
        }

        // ---------------- Saving ----------------

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                try
                {
                    TieRules.Tidy(ties, Now);
                    ties.RemoveAll(t => t == null || t.from.IsNone || t.to.IsNone);
                    PruneKnown(Now, true);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("world ledger: upkeep before saving", e);
                }
            }
            Scribe_Collections.Look(ref ties, "ties", LookMode.Deep);
            Scribe_Values.Look(ref nextTie, "nextTie", 1);
            Scribe_Values.Look(ref nextNumber, "nextNumber", 1);
            ExposeKnown();
            ExposeKeeps();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ties ??= new List<Tie>();
                ties.RemoveAll(t => t == null || t.from.IsNone || t.to.IsNone);
                foreach (var t in ties) t.terms ??= new TieTerms();
                // Ids stay unique whatever an older or edited save held.
                if (ties.Count > 0) nextTie = Math.Max(nextTie, ties.Max(t => t.id) + 1);
                int used = 0;
                foreach (var t in ties) used = Math.Max(used, Math.Max(t.from.Number, Math.Max(t.to.Number, t.terms.about.Number)));
                foreach (var p in persons) used = Math.Max(used, p.person.Number);
                nextNumber = Math.Max(Math.Max(1, nextNumber), used + 1);
                tidiedAt = Fresh.Never;
                Changed();
            }
        }
    }
}
