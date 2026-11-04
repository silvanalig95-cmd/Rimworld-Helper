using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The terms of a tie: a few named values, each optional. What they mean depends on the kind (a favour's goods and
    /// count, a truce's days, a debt's silver, a regard's score); <see cref="about"/> is a third party it concerns (the
    /// notable a good word is for, the person a ward is).
    /// </summary>
    public sealed class TieTerms
    {
        /// <summary>A thing's def name (the goods asked for, the silver owed).</summary>
        public string goods;
        public int count;
        public float days;
        /// <summary>A free key: a text key, a name, a story ("loan", "forced").</summary>
        public string key;
        public float number;
        public Ref about;

        public TieTerms Copy() => (TieTerms)MemberwiseClone();
    }

    /// <summary>
    /// One tie between two parties (the world ledger's record, <see cref="WorldLedger"/>): what kind, from whom to whom,
    /// on what terms, since and until when, how it stands, and where it came from. <see cref="from"/> is the party bound
    /// (who owes, who swore, who holds the grudge, who promised); <see cref="to"/> the party it's toward; for the mutual
    /// kinds (a pact, a truce, a marriage) <see cref="from"/> is whoever offered. The record is pure (Tools/Tests checks
    /// its rules, <see cref="TieRules"/>); saving it is in Tie.cs.
    /// </summary>
    public partial class Tie
    {
        public enum Kind
        {
            /// <summary>A favour owed: from owes to a good turn (goods brought on the next visit).</summary>
            Favour,
            Pact,
            Truce,
            Marriage,
            /// <summary>Someone of one party in the other's keeping (about: the ward).</summary>
            Ward,
            Hostage,
            /// <summary>From holds of to (land by their grant, a vassal's oath).</summary>
            Fealty,
            /// <summary>From holds something against to.</summary>
            Grudge,
            /// <summary>From owes to (goods and count: what; it comes due at until).</summary>
            Debt,
            /// <summary>From promised to something (about: whom it concerns).</summary>
            Promise,
            /// <summary>How from regards to (number: the score).</summary>
            Regard,
            /// <summary>
            /// From (a family's people) has a claim on to, one of theirs who joined another (about: who keeps them now):
            /// their family may come to ask for them back. Pressing it is calling it in; letting them go keeps it, refusing
            /// breaks it.
            /// </summary>
            Claim,
            /// <summary>
            /// From (whoever knows it) holds a secret about to (the one it is about): leverage that can be called in (key:
            /// what it is). Reserved for the keeper of secrets (stage 2, round c): its rules and bounds stand, nothing makes one yet.
            /// </summary>
            Secret,
            /// <summary>
            /// From (the schemer) works against to (the target), its progress in its terms (number). Reserved for schemes
            /// (stage 2, round c): kept when it succeeds, broken when found out, ended when given up; nothing makes one yet.
            /// </summary>
            Scheme
        }

        public enum State
        {
            /// <summary>In force.</summary>
            Open,
            /// <summary>Honoured: the favour done, the debt paid, the term served.</summary>
            Kept,
            /// <summary>Asked for now: the debt due, the favour called upon, the ally summoned. Still in force.</summary>
            CalledIn,
            /// <summary>Not honoured: refused, betrayed, or called in and not met in time.</summary>
            Broken,
            /// <summary>Over with nobody at fault: lapsed, let go, ran its course, one side gone.</summary>
            Ended
        }

        /// <summary>No tick: no end set, not closed, not known.</summary>
        public const int Never = -1;

        public int id;
        public Kind kind;
        public Ref from;
        public Ref to;
        public TieTerms terms = new TieTerms();
        public int since = Never;
        /// <summary>When it ends by itself (<see cref="TieRules.AtUntil"/>), or <see cref="Never"/>.</summary>
        public int until = Never;
        public State state;
        /// <summary>When it left the live states (kept, broken, ended), for dropping it later; <see cref="Never"/> while live.</summary>
        public int closed = Never;
        /// <summary>Where it came from: a free key for now (history ids come with the history, stage 0 step 4).</summary>
        public string origin;
        /// <summary>
        /// Not saved. Set on a tie read from one of today's other stores (the store's name, "outsiders:favour"): a view,
        /// built afresh, not editable through the ledger. Null on the ledger's own ties.
        /// </summary>
        public string source;

        public bool IsView => source != null;

        public bool Live => TieRules.Live(state);

        /// <summary>A party to it, or the one it concerns.</summary>
        public bool Involves(Ref r) => !r.IsNone && (from == r || to == r || terms != null && terms.about == r);

        /// <summary>Between these two, either way round.</summary>
        public bool Between(Ref a, Ref b) => !a.IsNone && !b.IsNone && (from == a && to == b || from == b && to == a);

        public override string ToString() => $"{kind} {from}>{to} {state}" + (IsView ? " (view: " + source + ")" : "");
    }

    /// <summary>
    /// How ties move between states, what reaching their end does, and how many are kept. Pure: Tools/Tests checks it.
    /// <list type="bullet">
    /// <item>Open and called in are live; kept, broken and ended are closed, and stay closed.</item>
    /// <item>From open: kept, called in, broken or ended. From called in: kept, broken, ended, or open again (put off,
    /// given more time). Only what can be asked for can be called in (a favour, a pact, fealty, a debt, a promise, a
    /// hostage, a family's claim, a secret); a regard is never kept or broken, only ended; a scheme is kept (it worked),
    /// broken (found out) or ended (given up), never called in.</item>
    /// <item>At its until: an open debt comes due (called in, with no end until the one asking sets one); any other open
    /// tie ends (it ran its course); a tie called in and not met by then is broken.</item>
    /// <item>Closed ties are dropped <see cref="KeepClosedDays"/> days after they closed (the history folds them in,
    /// step 4). At most <see cref="MaxPerPairKind"/> of one kind between the same two (either way round), and
    /// <see cref="MaxOf"/> of one kind in all: a new one makes the oldest closed one go, and is refused when all are live.</item>
    /// </list>
    /// </summary>
    public static class TieRules
    {
        public const int DayTicks = 60000;
        public const int KeepClosedDays = 60;
        public const int MaxPerPairKind = 3;
        public const int MaxPerKind = 60;
        /// <summary>Regard is kept between many pairs (each notable and colonist, each outsider pair): more room.</summary>
        public const int MaxRegard = 300;
        /// <summary>How many kinds of tie there are (Secret and Scheme reserved for stage 2's round c).</summary>
        public const int Kinds = 14;
        /// <summary>All ties at most: every kind but regard at <see cref="MaxPerKind"/>, and regard at <see cref="MaxRegard"/>.</summary>
        public const int MaxTies = (Kinds - 1) * MaxPerKind + MaxRegard;

        public static int MaxOf(Tie.Kind kind) => kind == Tie.Kind.Regard ? MaxRegard : MaxPerKind;

        public static bool Live(Tie.State s) => s == Tie.State.Open || s == Tie.State.CalledIn;

        /// <summary>Can be asked for: a favour, a pact, fealty, a debt, a promise, a hostage, a family's claim, a secret held over someone.</summary>
        public static bool CanCallIn(Tie.Kind k) =>
            k == Tie.Kind.Favour || k == Tie.Kind.Pact || k == Tie.Kind.Fealty || k == Tie.Kind.Debt || k == Tie.Kind.Promise || k == Tie.Kind.Hostage
            || k == Tie.Kind.Claim || k == Tie.Kind.Secret;

        /// <summary>Can be honoured or failed (everything but a regard, which only ends).</summary>
        public static bool CanKeepOrBreak(Tie.Kind k) => k != Tie.Kind.Regard;

        /// <summary>A tie of this kind may go from one state to the other (staying put is not a move).</summary>
        public static bool CanMove(Tie.Kind kind, Tie.State from, Tie.State to)
        {
            if (from == to || !Live(from)) return false;
            switch (to)
            {
                case Tie.State.Ended: return true;
                case Tie.State.Kept:
                case Tie.State.Broken: return CanKeepOrBreak(kind);
                case Tie.State.CalledIn: return from == Tie.State.Open && CanCallIn(kind);
                case Tie.State.Open: return from == Tie.State.CalledIn;
                default: return false;
            }
        }

        /// <summary>What a tie of this kind in this state becomes when its until comes (unchanged: nothing happens).</summary>
        public static Tie.State AtUntil(Tie.Kind kind, Tie.State state)
        {
            switch (state)
            {
                case Tie.State.Open: return kind == Tie.Kind.Debt ? Tie.State.CalledIn : Tie.State.Ended;
                case Tie.State.CalledIn: return Tie.State.Broken;
                default: return state;
            }
        }

        /// <summary>Its until has come (one is set, and now is at or past it).</summary>
        public static bool Due(int until, int now) => until != Tie.Never && now >= until;

        /// <summary>How the tie stands at <paramref name="now"/>, with its until applied (without changing it).</summary>
        public static Tie.State StateAt(Tie t, int now) => t == null ? Tie.State.Ended : Due(t.until, now) ? AtUntil(t.kind, t.state) : t.state;

        /// <summary>
        /// Moves a tie to a state if it may, noting when it closed. Called in clears its until (whoever asks sets a new one);
        /// put off (back to open) keeps whatever until was set. Returns whether it moved.
        /// </summary>
        public static bool Move(Tie t, Tie.State to, int now)
        {
            if (t == null || !CanMove(t.kind, t.state, to)) return false;
            t.state = to;
            if (to == Tie.State.CalledIn) t.until = Tie.Never;
            t.closed = Live(to) ? Tie.Never : now;
            return true;
        }

        /// <summary>
        /// Applies its until if it has come (however late it's looked at, a tie that closed this way closed at its until).
        /// Returns whether it changed.
        /// </summary>
        public static bool Expire(Tie t, int now)
        {
            if (t == null || !Live(t.state) || !Due(t.until, now)) return false;
            var next = AtUntil(t.kind, t.state);
            if (next == t.state) return false;
            t.state = next;
            t.closed = Live(next) ? Tie.Never : t.until;
            if (next == Tie.State.CalledIn) t.until = Tie.Never;
            return true;
        }

        /// <summary>Closed long enough to be dropped.</summary>
        public static bool Forgotten(Tie t, int now) => t != null && !Live(t.state) && t.closed != Tie.Never && now - t.closed >= KeepClosedDays * DayTicks;

        /// <summary>Applies every tie's until, and drops those closed long enough ago (and any null). Returns how many changed or went.</summary>
        public static int Tidy(List<Tie> ties, int now)
        {
            if (ties == null) return 0;
            int changed = 0;
            foreach (var t in ties)
                if (Expire(t, now))
                    changed++;
            changed += ties.RemoveAll(t => t == null || Forgotten(t, now));
            return changed;
        }

        /// <summary>
        /// Makes room for one more tie of <paramref name="kind"/> between <paramref name="a"/> and <paramref name="b"/>:
        /// over a bound, the oldest closed tie of that group goes. False when there is no room because all of them are
        /// live (a closed one may have gone for the other bound already: it would have gone in time anyway).
        /// </summary>
        public static bool MakeRoom(List<Tie> ties, Tie.Kind kind, Ref a, Ref b)
        {
            if (ties == null) return true;
            return Fit(ties, t => t.kind == kind && t.Between(a, b), MaxPerPairKind) && Fit(ties, t => t.kind == kind, MaxOf(kind));
        }

        private static bool Fit(List<Tie> ties, Func<Tie, bool> group, int max)
        {
            while (true)
            {
                int count = 0;
                Tie oldest = null;
                foreach (var t in ties)
                {
                    if (t == null || !group(t)) continue;
                    count++;
                    if (!Live(t.state) && (oldest == null || t.closed < oldest.closed)) oldest = t;
                }
                if (count < max) return true;
                if (oldest == null) return false;
                ties.Remove(oldest);
            }
        }
    }
}
