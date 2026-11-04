using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A favour asked of an outsider for their people's next visit, as the world ledger keeps it: a Favour tie from their
    /// people to the colonist who asked, about the outsider who promised, with the goods and how many in its terms, since
    /// it was asked. Read afresh from the ledger each time (<see cref="Favours"/>).
    /// </summary>
    public sealed class Favour
    {
        public Tie tie;
        public Faction faction;
        public ThingDef def;

        public Pawn outsider => WorldLedger.PawnOf(tie.terms.about);
        public string outsiderName => WorldLedger.PersonRecord(tie.terms.about)?.name;
        public Pawn by => WorldLedger.PawnOf(tie.to);
        public string byName => WorldLedger.PersonRecord(tie.to)?.name;
        public int count => tie.terms.count;
        public int asked => tie.since;

        /// <summary>Someone of theirs on the way over with it now (not saved: after a load they come round again).</summary>
        public Pawn bearer
        {
            get => Favours.BearerOf(tie);
            set => Favours.SetBearer(tie, value);
        }
    }

    /// <summary>
    /// The favours asked of outsiders (Outsiders.cs: asked in Bring, brought or let go in FavoursHourly and Brought): one
    /// open at a time per people, as the act allows, kept in the world ledger.
    /// </summary>
    public static class Favours
    {
        /// <summary>The origin of the ledger's favour ties kept here.</summary>
        public const string Origin = "outsiders";

        private static readonly Dictionary<int, Pawn> bearers = new Dictionary<int, Pawn>();
        private static Game bearersGame;

        /// <summary>The favours still open, in the order they were asked.</summary>
        public static List<Favour> Open()
        {
            var list = new List<Favour>();
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Favour))
            {
                if (!t.Live || t.origin != Origin) continue;
                list.Add(new Favour { tie = t, faction = Refs.Faction(t.from), def = t.terms.goods.NullOrEmpty() ? null : DefDatabase<ThingDef>.GetNamedSilentFail(t.terms.goods) });
            }
            return list;
        }

        /// <summary>How many are open.</summary>
        public static int Count => WorldLedger.OwnTies(Tie.Kind.Favour).Count(t => t.Live && t.origin == Origin);

        /// <summary>One is open with this people already.</summary>
        public static bool AskedOf(Faction f)
        {
            if (f == null) return false;
            var people = Refs.Of(f);
            return WorldLedger.OwnTies(Tie.Kind.Favour).Any(t => t.Live && t.origin == Origin && t.from == people);
        }

        /// <summary>A favour asked: their people owe the colonist who asked, the outsider promised it.</summary>
        public static void Ask(Pawn outsider, Faction f, Pawn by, ThingDef def, int count)
        {
            if (f == null || by == null || def == null) return;
            var terms = new TieTerms { goods = def.defName, count = count, days = OutsiderMath.BringGiveUpDays, about = outsider != null ? WorldLedger.PersonOf(outsider) : Ref.None };
            WorldLedger.AddTie(Tie.Kind.Favour, Refs.Of(f), WorldLedger.PersonOf(by), terms, origin: Origin);
        }

        /// <summary>Brought (kept), or let go (ended).</summary>
        public static void Close(Favour f, bool brought)
        {
            if (f?.tie == null) return;
            bearers.Remove(f.tie.id);
            WorldLedger.Set(f.tie, brought ? Tie.State.Kept : Tie.State.Ended);
        }

        public static Pawn BearerOf(Tie t)
        {
            CheckGame();
            return t != null && bearers.TryGetValue(t.id, out var p) ? p : null;
        }

        public static void SetBearer(Tie t, Pawn p)
        {
            if (t == null) return;
            CheckGame();
            if (p == null) bearers.Remove(t.id);
            else bearers[t.id] = p;
        }

        private static void CheckGame()
        {
            if (bearersGame == Current.Game) return;
            bearersGame = Current.Game;
            bearers.Clear();
        }

        /// <summary>An older save's favours (the outsiders' book): moved into the ledger in the order they were asked.</summary>
        public static void Adopt(List<OutsiderFavour> old)
        {
            if (old == null) return;
            foreach (var f in old)
            {
                if (f?.faction == null || f.def == null) continue;
                try
                {
                    var to = WorldLedger.PersonOr(f.by, f.byName);
                    if (to.IsNone) to = Refs.Us;
                    var terms = new TieTerms { goods = f.def.defName, count = f.count, days = OutsiderMath.BringGiveUpDays, about = WorldLedger.PersonOr(f.outsider, f.outsiderName, f.faction) };
                    WorldLedger.Adopt(new Tie { kind = Tie.Kind.Favour, from = Refs.Of(f.faction), to = to, terms = terms, since = f.asked, origin = Origin });
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("favours: an older save's", e);
                }
            }
        }
    }

    /// <summary>
    /// A good word an outsider promised to put in at home with one of their notables, as the world ledger keeps it: a
    /// Promise tie from the outsider to the colonist who asked, about the notable's person, with how much it moves them
    /// (number) and the notable's record (count) in its terms, lapsing twenty days after it was given.
    /// </summary>
    public sealed class GoodWord
    {
        public Tie tie;

        public Pawn outsider => WorldLedger.PawnOf(tie.from);
        public string outsiderName => WorldLedger.PersonRecord(tie.from)?.name;
        public Pawn by => WorldLedger.PawnOf(tie.to);
        public string byName => WorldLedger.PersonRecord(tie.to)?.name;
        /// <summary>The notable's record (NotableBook.Get): the one it's for, whoever holds that place by the time it lands.</summary>
        public int notable => tie.terms.count;
        public float shift => tie.terms.number;
        public int asked => tie.since;
    }

    /// <summary>The good words promised (Outsiders.cs: given in GoodWord, landing or lapsing in WordsHourly), kept in the world ledger.</summary>
    public static class GoodWords
    {
        /// <summary>The origin of the ledger's promise ties kept here.</summary>
        public const string Origin = "goodWord";

        /// <summary>The words still open, in the order they were given.</summary>
        public static List<GoodWord> Open()
        {
            var list = new List<GoodWord>();
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Promise))
                if (t.Live && t.origin == Origin)
                    list.Add(new GoodWord { tie = t });
            return list;
        }

        /// <summary>This outsider has a word still to put in.</summary>
        public static bool PromisedBy(Pawn x)
        {
            if (x == null) return false;
            var r = Refs.Of(x);
            return WorldLedger.OwnTies(Tie.Kind.Promise).Any(t => t.Live && t.origin == Origin && t.from == r);
        }

        /// <summary>A word promised; past <see cref="OutsiderBook.MaxWords"/> open, the oldest is let go.</summary>
        public static void Promise(Pawn x, Pawn by, Notable to, float shift)
        {
            if (x == null || by == null || to == null) return;
            var terms = new TieTerms { number = shift, count = to.id, days = OutsiderMath.GoodWordDays, about = to.Person };
            int now = Find.TickManager.TicksGame;
            WorldLedger.AddTie(Tie.Kind.Promise, WorldLedger.PersonOf(x), WorldLedger.PersonOf(by), terms, StoreMath.After(now, OutsiderMath.GoodWordDays), Origin);
            var open = Open();
            for (int i = 0; i < open.Count - OutsiderBook.MaxWords; i++) WorldLedger.Set(open[i].tie, Tie.State.Ended);
        }

        /// <summary>Landed (kept), or lapsed or lost (ended).</summary>
        public static void Close(GoodWord w, bool landed)
        {
            if (w?.tie != null) WorldLedger.Set(w.tie, landed ? Tie.State.Kept : Tie.State.Ended);
        }

        /// <summary>An older save's good words (the outsiders' book): moved into the ledger in the order they were given.</summary>
        public static void Adopt(List<OutsiderWord> old)
        {
            if (old == null) return;
            var book = NotableBook.Instance;
            foreach (var w in old)
            {
                if (w == null) continue;
                try
                {
                    var from = WorldLedger.PersonOr(w.outsider, w.outsiderName);
                    var to = WorldLedger.PersonOr(w.by, w.byName);
                    if (from.IsNone || to.IsNone) continue;
                    var n = book?.Get(w.notable);
                    var terms = new TieTerms { number = w.shift, count = w.notable, days = OutsiderMath.GoodWordDays, about = n?.Person ?? Ref.None };
                    WorldLedger.Adopt(new Tie
                    {
                        kind = Tie.Kind.Promise, from = from, to = to, terms = terms, since = w.asked, until = StoreMath.After(w.asked, OutsiderMath.GoodWordDays), origin = Origin
                    });
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("good words: an older save's", e);
                }
            }
        }
    }
}
