using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A family line out in the world, known by name: the founder's line of a new town and one more founding family.
    /// Only the head is a person of the ledger (a pawn, the town's founder or a notable); the rest are known by their
    /// first names only, so a family costs a line in the save.
    /// </summary>
    public class LedgerHouse : IExposable
    {
        /// <summary>The house as a ref (h + a ledger number).</summary>
        public Ref house;
        /// <summary>The family's name ("Coor").</summary>
        public string name;
        /// <summary>The town it lives in (a settlement's id).</summary>
        public int town = -1;
        public Faction people;
        /// <summary>Its head, a person of the ledger, or none.</summary>
        public Ref head;
        /// <summary>The others known by name (first names), at most RegionMath.MembersPerHouse.</summary>
        public List<string> members = new List<string>();
        /// <summary>One of the town's founding families.</summary>
        public bool founding;
        /// <summary>Named after one of them, not by a surname (a people who go by one name): "Yerrik's kin".</summary>
        public bool kin;
        public int since = -1;

        public void ExposeData()
        {
            Refs.Look(ref house, "ref");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref town, "town", -1);
            Scribe_References.Look(ref people, "people");
            Refs.Look(ref head, "head");
            Scribe_Collections.Look(ref members, "members", LookMode.Value);
            Scribe_Values.Look(ref founding, "founding");
            Scribe_Values.Look(ref kin, "kin");
            Scribe_Values.Look(ref since, "since", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                members ??= new List<string>();
                members.RemoveAll(m => m.NullOrEmpty());
            }
        }
    }

    /// <summary>
    /// A town being founded within the region map's reach of a home (another mod's settlement being built, Faction
    /// Territories' ten days): where, whose, since when, who leads the settlers, from which town and why, and what has
    /// been told. Dropped when the town stands (it becomes the town's record) or the site is gone.
    /// </summary>
    public class LedgerFounding : IExposable
    {
        /// <summary>The site's world object id.</summary>
        public int site = -1;
        /// <summary>Its tile as a ref (the history's place for it).</summary>
        public Ref tile;
        public Faction people;
        public int started = -1;
        public int completes = -1;
        /// <summary>Who leads the settlers (a person of the ledger, kept for the founding), or none while not yet made.</summary>
        public Ref founder;
        public Ref mother;
        public string motherName;
        public string why;
        /// <summary>The colony news has its line ("Settlers ... have begun a town ...").</summary>
        public bool told;
        /// <summary>The town stood (its id): its faces wait for the region's hour.</summary>
        public int built = -1;
        /// <summary>One of Rimfolk's own new towns: the world event's own letter told it.</summary>
        public bool byEvent;

        public void ExposeData()
        {
            Scribe_Values.Look(ref site, "site", -1);
            Refs.Look(ref tile, "tile");
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref started, "started", -1);
            Scribe_Values.Look(ref completes, "completes", -1);
            Refs.Look(ref founder, "founder");
            Refs.Look(ref mother, "mother");
            Scribe_Values.Look(ref motherName, "motherName");
            Scribe_Values.Look(ref why, "why");
            Scribe_Values.Look(ref told, "told");
            Scribe_Values.Look(ref built, "built", -1);
            Scribe_Values.Look(ref byEvent, "byEvent");
        }
    }

    /// <summary>
    /// The region's records on the world ledger (docs/design/stage1-region.md): the towns in detail (their fields are on
    /// <see cref="LedgerTown"/>), the families of new towns (<see cref="LedgerHouse"/>) and the towns being founded
    /// within reach (<see cref="LedgerFounding"/>). The region (World/Region/) reads and writes them through here.
    /// </summary>
    public partial class WorldLedger
    {
        private List<LedgerHouse> houses = new List<LedgerHouse>();
        private List<LedgerFounding> foundings = new List<LedgerFounding>();

        [Unsaved] private Dictionary<int, LedgerTown> townIndex;
        [Unsaved] private int townIndexCount = -1;

        /// <summary>A town's record by its id, made (with its name and people) when <paramref name="make"/> and it has none.</summary>
        public static LedgerTown TownFor(int id, string name, Faction people, bool make)
        {
            var l = Instance;
            if (l == null || id < 0) return null;
            var t = l.IndexedTown(id);
            if (t != null || !make) return t;
            t = new LedgerTown { id = id, name = name, people = people };
            l.townRecords.Add(t);
            Changed();
            return t;
        }

        private LedgerTown IndexedTown(int id)
        {
            if (townIndex == null || townIndexCount != townRecords.Count)
            {
                townIndex = new Dictionary<int, LedgerTown>();
                foreach (var t in townRecords)
                    if (t != null && t.id >= 0)
                        townIndex[t.id] = t;
                townIndexCount = townRecords.Count;
            }
            return townIndex.TryGetValue(id, out var r) ? r : null;
        }

        /// <summary>The towns the ledger keeps (read only).</summary>
        public IReadOnlyList<LedgerTown> Towns => townRecords;

        /// <summary>A town record's fields changed (its prosperity, its founder): the views are built again.</summary>
        public static void TownChanged() => Changed();

        // ---------------- Houses ----------------

        /// <summary>The houses the ledger keeps (read only).</summary>
        public IReadOnlyList<LedgerHouse> Houses => houses;

        /// <summary>A town's houses, founding families first.</summary>
        public static List<LedgerHouse> HousesOf(int town)
        {
            var list = new List<LedgerHouse>();
            var l = Instance;
            if (l == null || town < 0) return list;
            foreach (var h in l.houses)
                if (h != null && h.town == town)
                    list.Add(h);
            list.Sort((a, b) => a.founding != b.founding ? (a.founding ? -1 : 1) : a.since.CompareTo(b.since));
            return list;
        }

        /// <summary>
        /// A new house of a town: its family name, its head and members by name (at most RegionMath.MembersPerHouse). A
        /// town keeps RegionMath.HousesPerTown at most: the newest that isn't a founding family makes way, and none is
        /// added when all are founding families. Null without a game or a name.
        /// </summary>
        public static LedgerHouse AddHouse(int town, Faction people, string name, Ref head, IEnumerable<string> members, bool founding, bool kin = false)
        {
            var l = Instance;
            if (l == null || town < 0 || name.NullOrEmpty()) return null;
            var mine = HousesOf(town);
            foreach (var h in mine)
                if (h.name == name)
                    return h;
            if (mine.Count >= RegionMath.HousesPerTown)
            {
                LedgerHouse give = null;
                for (int i = mine.Count - 1; i >= 0 && give == null; i--)
                    if (!mine[i].founding)
                        give = mine[i];
                if (give == null) return null;
                l.houses.Remove(give);
            }
            var house = new LedgerHouse { house = NewHouse(), name = name, town = town, people = people, head = head, founding = founding, kin = kin, since = Now };
            if (members != null)
                foreach (var m in members)
                    if (!m.NullOrEmpty() && house.members.Count < RegionMath.MembersPerHouse && !house.members.Contains(m))
                        house.members.Add(m);
            l.houses.Add(house);
            Changed();
            return house;
        }

        // ---------------- Foundings ----------------

        /// <summary>The towns being founded within reach (read only).</summary>
        public IReadOnlyList<LedgerFounding> Foundings => foundings;

        /// <summary>The founding at a site (its world object id), or null.</summary>
        public static LedgerFounding FoundingAt(int site)
        {
            var l = Instance;
            if (l == null || site < 0) return null;
            foreach (var f in l.foundings)
                if (f != null && f.site == site)
                    return f;
            return null;
        }

        /// <summary>A founding begun: kept, at most RegionMath.MaxFoundings, the oldest going first (and its founder let go).</summary>
        public static LedgerFounding AddFounding(LedgerFounding f)
        {
            var l = Instance;
            if (l == null || f == null || f.site < 0) return null;
            var had = FoundingAt(f.site);
            if (had != null) return had;
            l.foundings.Add(f);
            var gone = new List<LedgerFounding>();
            while (l.foundings.Count > RegionMath.MaxFoundings)
            {
                gone.Add(l.foundings[0]);
                l.foundings.RemoveAt(0);
            }
            Changed();
            foreach (var g in gone) RegionFaces.FoundingGone(g);
            return f;
        }

        /// <summary>A founding is over (built, razed or out of reach).</summary>
        public static void RemoveFounding(LedgerFounding f)
        {
            var l = Instance;
            if (l == null || f == null) return;
            if (l.foundings.Remove(f)) Changed();
        }

        /// <summary>A person leads settlers or founded a town the ledger keeps: the reason KeepFor.Founding still holds.</summary>
        public static bool FounderNamed(Ref person)
        {
            var l = Instance;
            if (l == null || person.IsNone) return false;
            foreach (var t in l.townRecords)
                if (t != null && t.founder == person)
                    return true;
            foreach (var f in l.foundings)
                if (f != null && f.founder == person)
                    return true;
            return false;
        }

        /// <summary>Town records let go at the day's tidy: their houses go with them, and their founders are let go by the region.</summary>
        private void TownsDropped(List<LedgerTown> dropped)
        {
            var ids = new HashSet<int>();
            foreach (var t in dropped) ids.Add(t.id);
            houses.RemoveAll(h => h == null || ids.Contains(h.town));
            foreach (var t in dropped)
            {
                if (t.founder.IsNone || FounderNamed(t.founder)) continue;
                try
                {
                    RegionFaces.LetFounderGo(t.founder);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("world ledger: a founder let go", e);
                }
            }
        }

        private void ExposeRegion()
        {
            Scribe_Collections.Look(ref houses, "houses", LookMode.Deep);
            Scribe_Collections.Look(ref foundings, "foundings", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                houses ??= new List<LedgerHouse>();
                houses.RemoveAll(h => h == null || h.house.IsNone || h.town < 0);
                foundings ??= new List<LedgerFounding>();
                foundings.RemoveAll(f => f == null || f.site < 0);
                townIndex = null;
            }
        }
    }
}
