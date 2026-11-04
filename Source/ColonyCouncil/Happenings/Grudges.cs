using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The grudges peoples hold against the colony for their own who died in its hands: one Grudge tie per people in the
    /// world ledger (from that people, to the colony), with the dead in its key and how many are not yet settled in its
    /// count (<see cref="GrudgeTally"/>, which also keeps the rules). Ten dead at most over all peoples, the oldest going
    /// first; a grudge acted on (their people came to settle it) is settled, and goes at the day's tidy with any held two
    /// years. A people's grudge tie ends when it holds nobody any more: kept if its last went settled, ended if they were
    /// forgotten. What the people remember of it stays in their memory (the deed "prisonerDied").
    /// </summary>
    public static class Grudges
    {
        /// <summary>The origin of the ledger's grudge ties kept here (other grudges are other origins).</summary>
        public const string Origin = "prisonerDied";

        /// <summary>One of the dead held against the colony, with their people.</summary>
        public sealed class Dead
        {
            public Faction faction;
            public GrudgeDead dead;

            public string Name => dead.name;
            public int Tick => dead.tick;
            public bool Settled => dead.settled;
        }

        /// <summary>The live grudge ties kept here, by their people's id, and the dead each holds.</summary>
        private sealed class Book
        {
            public readonly Dictionary<int, Tie> ties = new Dictionary<int, Tie>();
            public readonly Dictionary<int, List<GrudgeDead>> dead = new Dictionary<int, List<GrudgeDead>>();
        }

        private static Book Read()
        {
            var b = new Book();
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Grudge))
            {
                if (!t.Live || t.origin != Origin || t.from.kind != RefKind.People || b.ties.ContainsKey(t.from.id)) continue;
                b.ties[t.from.id] = t;
                b.dead[t.from.id] = GrudgeTally.Read(t.terms.key);
            }
            return b;
        }

        /// <summary>
        /// Every one of the dead held against the colony, in the order they died, settled ones too until the day's tidy
        /// (those of a people no longer in the world are left out, as an older save dropped them on load).
        /// </summary>
        public static List<Dead> All()
        {
            var list = new List<Dead>();
            var b = Read();
            if (b.ties.Count == 0) return list;
            var peoples = new Dictionary<int, Faction>();
            foreach (var kv in b.ties) peoples[kv.Key] = Refs.Faction(kv.Value.from);
            foreach (var kv in GrudgeTally.InOrder(b.dead))
            {
                var f = peoples[kv.Key];
                if (f != null) list.Add(new Dead { faction = f, dead = kv.Value });
            }
            return list;
        }

        /// <summary>A prisoner of theirs died in the colony's hands: their people's grudge holds them too.</summary>
        public static void Add(Faction f, string name, int now)
        {
            if (f == null) return;
            try
            {
                var b = Read();
                if (!b.ties.ContainsKey(f.loadID))
                {
                    var tie = WorldLedger.AddTie(Tie.Kind.Grudge, Refs.Of(f), Refs.Us, origin: Origin);
                    if (tie == null) return;
                    b.ties[f.loadID] = tie;
                }
                GrudgeTally.Add(b.dead, f.loadID, now, name);
                Write(b, null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("grudges: a prisoner died", e);
            }
        }

        /// <summary>Their people came to settle it: the dead of that name are settled.</summary>
        public static void Settle(Faction f, string name)
        {
            if (f == null) return;
            var b = Read();
            GrudgeTally.Settle(b.dead, f.loadID, name);
            Write(b, null);
        }

        /// <summary>The day's tidy: the settled go, and any held two years, and all of a people defeated or gone.</summary>
        public static void Prune(int now)
        {
            var b = Read();
            if (b.ties.Count == 0) return;
            var gone = new HashSet<int>();
            foreach (var kv in b.ties)
            {
                var f = Refs.Faction(kv.Value.from);
                if (f == null || f.defeated) gone.Add(kv.Key);
            }
            Write(b, GrudgeTally.Prune(b.dead, now, gone.Contains));
        }

        /// <summary>The dead back into their ties; a tie that holds nobody any more ends (kept if its last went settled).</summary>
        private static void Write(Book b, HashSet<int> settledGone)
        {
            foreach (var kv in b.ties)
            {
                var t = kv.Value;
                if (!b.dead.TryGetValue(kv.Key, out var dead)) dead = new List<GrudgeDead>();
                string key = GrudgeTally.Write(dead);
                int open = GrudgeTally.Open(dead);
                if (t.terms.key != key || t.terms.count != open)
                {
                    t.terms.key = key;
                    t.terms.count = open;
                    WorldLedger.Touch(t);
                }
                if (dead.Count == 0) WorldLedger.Set(t, settledGone != null && settledGone.Contains(kv.Key) ? Tie.State.Kept : Tie.State.Ended);
            }
        }

        /// <summary>
        /// An older save's grudges (the event tracker's list): moved into the ledger in the order they came, one tie per
        /// people, since the first of theirs died.
        /// </summary>
        public static void Adopt(List<Grudge> old)
        {
            if (old == null || old.Count == 0) return;
            try
            {
                var b = Read();
                var kept = old.Where(g => g?.faction != null).ToList();
                var peoples = new Dictionary<int, Faction>();
                foreach (var g in kept) peoples[g.faction.loadID] = g.faction;
                GrudgeTally.AddOld(b.dead, kept.Select(g => (g.faction.loadID, g.name, g.tick, g.settled)));
                foreach (var g in kept)
                {
                    int id = g.faction.loadID;
                    if (b.ties.ContainsKey(id)) continue;
                    var dead = b.dead[id];
                    var tie = WorldLedger.Adopt(new Tie
                    {
                        kind = Tie.Kind.Grudge, from = Refs.Of(g.faction), to = Refs.Us, since = dead.Min(d => d.tick), origin = Origin,
                        terms = new TieTerms { key = GrudgeTally.Write(dead), count = GrudgeTally.Open(dead) }
                    });
                    if (tie != null) b.ties[id] = tie;
                    else b.dead.Remove(id);
                }
                Write(b, null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("grudges: an older save's", e);
            }
        }
    }
}
