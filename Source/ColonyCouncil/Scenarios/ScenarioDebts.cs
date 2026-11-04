using System.Linq;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A scenario's silver debt, as the world ledger keeps it: a Debt tie from the colony to the people it owes, with the
    /// silver (goods and count), the times it was put off (number) and its story (key) in its terms, about whoever was
    /// robbed or lent it, coming due at its until (called in then, and while the council is asked); paid it is kept,
    /// refused broken, half paid it is put off (open again, with more owed and a new day). The scenario's memo ("debt",
    /// Happenings) stays as the scenario's handle on it: its place in the scenario's hour, its story, the one it's about
    /// kept in the world, its colony, and when the scenario next looks (int.MaxValue while the council is asked). The memo
    /// keeps the silver and the times put off itself only if the ledger had no room for the tie (never expected).
    /// </summary>
    public static class Debts
    {
        /// <summary>The origin of the ledger's debt ties kept here.</summary>
        public const string Origin = "scenario";

        /// <summary>The memo's tie, while the ledger keeps it.</summary>
        public static Tie Of(Memo m) => m != null && m.tie > 0 ? WorldLedger.Get(m.tie) : null;

        /// <summary>How much silver is owed.</summary>
        public static int Amount(Memo m)
        {
            var t = Of(m);
            return t != null ? t.terms.count : m?.amount ?? 0;
        }

        /// <summary>How many times it was put off.</summary>
        public static int Times(Memo m)
        {
            var t = Of(m);
            return t != null ? (int)t.terms.number : m?.times ?? 0;
        }

        /// <summary>A new debt: <paramref name="amount"/> silver, due at <paramref name="due"/>.</summary>
        public static void Owe(Memo m, int amount, int due)
        {
            if (m == null) return;
            var from = Refs.Colony(Homes.ForMemo(m.home));
            if (from.IsNone) from = Refs.Us;
            var terms = new TieTerms { goods = "Silver", count = amount, key = m.def, about = m.pawn != null ? WorldLedger.PersonOf(m.pawn) : Ref.None };
            var t = WorldLedger.AddTie(Tie.Kind.Debt, from, Refs.Of(m.faction), terms, due, Origin);
            if (t != null) m.tie = t.id;
            else m.amount = amount;
        }

        /// <summary>It has come due: called in (the council is about to be asked).</summary>
        public static void Due(Memo m)
        {
            var t = Of(m);
            if (t != null && t.state == Tie.State.Open) WorldLedger.Set(t, Tie.State.CalledIn);
        }

        /// <summary>Half paid and more time given: more owed, put off once more, due again at <paramref name="due"/>.</summary>
        public static void PutOff(Memo m, int amount, int due)
        {
            var t = Of(m);
            if (t == null)
            {
                if (m == null) return;
                m.amount = amount;
                m.times++;
                return;
            }
            t.terms.count = amount;
            t.terms.number = (int)t.terms.number + 1;
            if (t.state == Tie.State.CalledIn) WorldLedger.Set(t, Tie.State.Open);
            t.until = due;
            WorldLedger.Touch(t);
        }

        /// <summary>Over: paid (kept), refused (broken), or gone with the people it was owed to (ended).</summary>
        public static void Close(Memo m, Tie.State how)
        {
            var t = Of(m);
            if (t != null && t.Live) WorldLedger.Set(t, how);
        }

        /// <summary>The colony owes a scenario's silver now.</summary>
        public static bool AnyOwed(HappeningTracker h)
        {
            if (WorldLedger.OwnTies(Tie.Kind.Debt).Any(t => t.Live && t.origin == Origin)) return true;
            return h != null && h.memos.Any(m => m != null && !m.used && m.kind == "debt" && m.tie <= 0);
        }

        /// <summary>
        /// An older save's debt (kept whole in its memo): the tie made from it, with the memo's silver and times put off,
        /// due at its next look (called in if that has come, or the council is being asked).
        /// </summary>
        public static void Adopt(Memo m, int now)
        {
            if (m == null || m.used || m.kind != "debt" || m.tie > 0 || m.faction == null) return;
            var from = Refs.Colony(Homes.ForMemo(m.home));
            if (from.IsNone) from = Refs.Us;
            bool asking = m.due == int.MaxValue;
            var terms = new TieTerms { goods = "Silver", count = m.amount, number = m.times, key = m.def, about = m.pawn != null ? WorldLedger.PersonOf(m.pawn) : Ref.None };
            var t = WorldLedger.Adopt(new Tie
            {
                kind = Tie.Kind.Debt, from = from, to = Refs.Of(m.faction), terms = terms, since = m.tick,
                until = asking || m.due <= now ? Tie.Never : m.due,
                state = asking || m.due <= now ? Tie.State.CalledIn : Tie.State.Open, origin = Origin
            });
            if (t == null) return;
            m.tie = t.id;
            m.amount = 0;
            m.times = 0;
        }
    }
}
