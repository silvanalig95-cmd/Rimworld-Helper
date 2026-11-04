using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The ties still kept in Rimfolk's other stores, read as ties (views: <see cref="Tie.source"/> names the store, not
    /// editable through the ledger). Only those still in force: what ended stays in its own store's record.
    /// <list type="bullet">
    /// <item>A vendetta a colonist swore against a people (VendettaTracker): the colonist's grudge. It stays a view: the
    /// vendetta is the colonist's own story (their kills, the victim), and the tie is only how the world sees it.</item>
    /// </list>
    /// The rest moved into the ledger as its own ties (stage 0 step 3b): grudges (Happenings/Grudges.cs), favours and good
    /// words (Outsiders/OutsiderTies.cs), guest kin as claims (Integrations/GuestWill.cs), the scenario's debt
    /// (Scenarios/ScenarioDebts.cs) and the grant of land as fealty (ScenPart_CouncilBeholden).
    /// Read once a 250-tick stretch (sooner when the faction memory, the notables or the ledger change).
    /// </summary>
    public partial class WorldLedger
    {
        private static List<Tie> today = new List<Tie>();
        private static int todayAt = Fresh.Never;
        private static int todayVersion;
        private static Game todayGame;

        /// <summary>Today's ties kept in the other stores, as views (the same list until the stretch or a source changes).</summary>
        public static IReadOnlyList<Tie> Today()
        {
            int now = Now;
            int v = SourcesVersion;
            if (todayGame == Current.Game && Fresh.Holds(todayAt, todayVersion, now, v, Sched.RareTicks)) return today;
            todayGame = Current.Game;
            todayAt = now;
            todayVersion = v;
            var list = new List<Tie>();
            if (Current.Game != null)
            {
                Read("vendettas", list, Vendettas);
            }
            today = list;
            return today;
        }

        private static void Read(string what, List<Tie> list, Action<List<Tie>> read)
        {
            try
            {
                read(list);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world ledger: today's " + what, e);
            }
        }

        private static void View(List<Tie> list, string source, Tie.Kind kind, Ref from, Ref to, TieTerms terms, int since, int until, Tie.State state = Tie.State.Open, string origin = null)
        {
            if (from.IsNone || to.IsNone || from == to) return;
            list.Add(new Tie { kind = kind, from = from, to = to, terms = terms ?? new TieTerms(), since = since, until = until, state = state, origin = origin, source = source });
        }

        private static void Vendettas(List<Tie> list)
        {
            var t = CouncilGameComponent.Instance?.vendettas;
            if (t == null) return;
            foreach (var v in t.vendettas)
            {
                if (v == null || !v.Active) continue;
                var terms = new TieTerms { key = v.victim, count = v.kills, number = Vendetta.KillsToSettle };
                View(list, "secrets:vendetta", Tie.Kind.Grudge, Refs.Of(v.avenger), Refs.Of(v.faction), terms, v.tick, Tie.Never);
            }
        }
    }
}
