using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The ties that exist today in Rimfolk's other stores, read as ties (views: <see cref="Tie.source"/> names the store,
    /// none is editable through the ledger yet). Only those still in force: what ended stays in its own store's record.
    /// <list type="bullet">
    /// <item>A grant of land the colony holds of a people (the deed "granted", ScenPart_CouncilBeholden): fealty.</item>
    /// <item>A vendetta a colonist swore against a people (VendettaTracker): the colonist's grudge.</item>
    /// </list>
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
                Read("granted", list, Granted);
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

        private static void Granted(List<Tie> list)
        {
            var mem = FactionMemory.Instance;
            if (mem == null || !FactionMemory.On) return;
            int now = Now;
            foreach (var d in mem.deeds)
            {
                if (d?.faction == null || d.kind != "granted" || d.settled) continue;
                var terms = new TieTerms { key = "granted", count = d.weight, number = d.StrengthAt(now) };
                View(list, "memory:granted", Tie.Kind.Fealty, Refs.Seat, Refs.Of(d.faction), terms, d.first, Tie.Never, Tie.State.Open, "deed:" + d.id);
            }
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
