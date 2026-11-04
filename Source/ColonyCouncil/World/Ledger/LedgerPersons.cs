using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Someone the world ledger knows (a notable, a party to one of its ties): their ref, their pawn while the game keeps
    /// one, their name for when it doesn't, and their people as last known.
    /// </summary>
    public class LedgerPerson : IExposable
    {
        public Ref person;
        public Pawn pawn;
        public string name;
        public Faction people;

        /// <summary>Their name: the pawn's own while there is one, else the one kept.</summary>
        public string Name => pawn != null && !pawn.Discarded ? pawn.LabelShort : name ?? "?";

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && pawn != null && pawn.Discarded) pawn = null;
            Refs.Look(ref person, "ref");
            Scribe_References.Look(ref pawn, "pawn", true);
            Scribe_Values.Look(ref name, "name");
            Scribe_References.Look(ref people, "people");
        }
    }

    /// <summary>A town the ledger knows: its name and people, and when the colony first missed it from the world (-1 while it stands).</summary>
    public class LedgerTown : IExposable
    {
        public int id = -1;
        public string name;
        public Faction people;
        public int gone = -1;

        public Ref Town => Ref.Town(id);

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id", -1);
            Scribe_Values.Look(ref name, "name");
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref gone, "gone", -1);
        }
    }

    /// <summary>
    /// The people and towns the ledger knows. Every notable is a person here (their pawn's ref while one is kept, a ledger
    /// number for one known only by name), and so is anyone a tie of the ledger names; the towns are those the notables
    /// live in, and a town gone from the world is remembered two years. A person or town nothing needs any more goes
    /// at the day's tidy (LedgerMath.KeepPerson, KeepTown).
    /// </summary>
    public partial class WorldLedger
    {
        private List<LedgerPerson> persons = new List<LedgerPerson>();
        private List<LedgerTown> townRecords = new List<LedgerTown>();

        [Unsaved] private Dictionary<Ref, LedgerPerson> personIndex;
        [Unsaved] private int prunedDay = -1;

        private Dictionary<Ref, LedgerPerson> PersonIndex
        {
            get
            {
                if (personIndex == null || personIndex.Count != persons.Count)
                {
                    personIndex = new Dictionary<Ref, LedgerPerson>();
                    foreach (var p in persons)
                        if (p != null && !p.person.IsNone)
                            personIndex[p.person] = p;
                }
                return personIndex;
            }
        }

        /// <summary>The ledger's record of a person, or null.</summary>
        public static LedgerPerson PersonRecord(Ref r)
        {
            var l = Instance;
            if (l == null || r.kind != RefKind.Person) return null;
            return l.PersonIndex.TryGetValue(r, out var p) ? p : null;
        }

        /// <summary>
        /// The person a pawn is, known to the ledger from now on (for as long as something needs them): their pawn's ref,
        /// with the pawn kept by reference while the game has it.
        /// </summary>
        public static Ref PersonOf(Pawn p)
        {
            var r = Refs.Of(p);
            var l = Instance;
            if (l == null || r.IsNone) return r;
            if (!l.PersonIndex.TryGetValue(r, out var rec))
            {
                rec = new LedgerPerson { person = r, pawn = p, name = p.LabelShort, people = p.Faction };
                l.persons.Add(rec);
                l.personIndex[r] = rec;
                Changed();
            }
            else if (!p.Discarded)
            {
                // The name as it is now (a colonist may have been renamed since).
                if (rec.pawn == null) rec.pawn = p;
                rec.name = p.LabelShort;
            }
            return r;
        }

        /// <summary>Someone known only by name (no pawn): a ledger number of their own, kept as a person.</summary>
        public static Ref NewPerson(string name, Faction people = null)
        {
            var l = Instance;
            if (l == null) return Ref.None;
            var r = Ref.LedgerPerson(l.NewNumber());
            var rec = new LedgerPerson { person = r, name = name, people = people };
            l.persons.Add(rec);
            l.PersonIndex[r] = rec;
            Changed();
            return r;
        }

        /// <summary>The person behind a pawn if there is one (no record made), else the person known by name, else none.</summary>
        public static Ref PersonOr(Pawn p, string name, Faction people = null) => p != null ? PersonOf(p) : !name.NullOrEmpty() ? NewPerson(name, people) : Ref.None;

        /// <summary>A person's pawn as the ledger keeps it (null once the game let them go and the ledger was saved since).</summary>
        public static Pawn PawnOf(Ref r) => PersonRecord(r)?.pawn;

        /// <summary>A person's name as the ledger knows it, or null.</summary>
        public static string NameOf(Ref r) => PersonRecord(r)?.Name;

        /// <summary>The persons the ledger knows (for the views and the dev window; read only).</summary>
        public IReadOnlyList<LedgerPerson> Persons => persons;

        // ---------------- Towns ----------------

        /// <summary>The ledger's record of a town, or null.</summary>
        public static LedgerTown TownRecord(int id)
        {
            var l = Instance;
            if (l == null || id < 0) return null;
            for (int i = 0; i < l.townRecords.Count; i++)
                if (l.townRecords[i].id == id)
                    return l.townRecords[i];
            return null;
        }

        /// <summary>A town someone the colony knows lives in: noted (its name and people kept for when it's gone).</summary>
        public static void NoteTown(int id, string name, Faction people)
        {
            var l = Instance;
            if (l == null || id < 0) return;
            var t = TownRecord(id);
            if (t == null)
            {
                t = new LedgerTown { id = id };
                l.townRecords.Add(t);
            }
            else if ((name.NullOrEmpty() || t.name == name) && (people == null || t.people == people)) return;
            if (!name.NullOrEmpty()) t.name = name;
            if (people != null) t.people = people;
            Changed();
        }

        // ---------------- Upkeep ----------------

        /// <summary>Once a day (and before saving): persons nothing names and towns nobody lives in any more go; towns missed from the world are noted.</summary>
        private void PruneKnown(int now, bool force)
        {
            int day = now / TieRules.DayTicks;
            if (!force && day == prunedDay) return;
            // Not while a save is being read: the stores are still handing over their people.
            if (Scribe.mode != LoadSaveMode.Inactive && Scribe.mode != LoadSaveMode.Saving) return;
            prunedDay = day;
            var named = new HashSet<Ref>();
            foreach (var t in ties)
            {
                if (t == null) continue;
                named.Add(t.from);
                named.Add(t.to);
                if (t.terms != null) named.Add(t.terms.about);
            }
            var book = NotableBook.Instance;
            var notable = new HashSet<Ref>();
            var lived = new HashSet<int>();
            if (book != null)
                foreach (var n in book.notables)
                {
                    if (n == null) continue;
                    notable.Add(n.person);
                    if (n.town >= 0) lived.Add(n.town);
                }
            int before = persons.Count + townRecords.Count;
            persons.RemoveAll(p => p == null || p.person.IsNone || !LedgerMath.KeepPerson(notable.Contains(p.person), named.Contains(p.person), HeldInWorld(p)));
            foreach (var p in persons)
                if (p.pawn != null && !p.pawn.Discarded && p.pawn.Faction != null)
                    p.people = p.pawn.Faction;
            var standing = new HashSet<int>();
            var world = Find.WorldObjects?.Settlements;
            if (world != null)
                foreach (var s in world)
                    if (s != null && !s.Destroyed)
                        standing.Add(s.ID);
            foreach (var t in townRecords)
                if (t != null && t.gone < 0 && !standing.Contains(t.id))
                    t.gone = now;
            townRecords.RemoveAll(t => t == null || t.id < 0 || !LedgerMath.KeepTown(lived.Contains(t.id), standing.Contains(t.id), t.gone, now));
            if (persons.Count + townRecords.Count != before)
            {
                personIndex = null;
                Changed();
            }
        }

        /// <summary>Some reason keeps them in the world (none are kept through the ledger yet).</summary>
        private static bool HeldInWorld(LedgerPerson p) => false;

        private void ExposeKnown()
        {
            Scribe_Collections.Look(ref persons, "persons", LookMode.Deep);
            Scribe_Collections.Look(ref townRecords, "towns", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                persons ??= new List<LedgerPerson>();
                persons.RemoveAll(p => p == null || p.person.IsNone);
                townRecords ??= new List<LedgerTown>();
                townRecords.RemoveAll(t => t == null || t.id < 0);
                personIndex = null;
                prunedDay = -1;
            }
        }
    }
}
