using System;
using System.Collections.Generic;
using System.Text;

namespace ColonyCouncil
{
    /// <summary>What someone was to an event: who did it, for whom, against whom, whom it was about, with whom.</summary>
    public enum HistoryRole : byte
    {
        Did,
        For,
        Against,
        About,
        With
    }

    /// <summary>Someone (or something: a people, a town, a colony) in an event, and their part in it.</summary>
    public readonly struct HistoryActor : IEquatable<HistoryActor>
    {
        public readonly Ref who;
        public readonly HistoryRole role;

        public HistoryActor(Ref who, HistoryRole role)
        {
            this.who = who;
            this.role = role;
        }

        public bool Equals(HistoryActor other) => who == other.who && role == other.role;

        public override bool Equals(object obj) => obj is HistoryActor a && Equals(a);

        public override int GetHashCode() => who.GetHashCode() * 31 + (int)role;

        public override string ToString() => HistoryMath.RoleName(role) + ":" + who;
    }

    /// <summary>
    /// One thing that happened in the world, as the history keeps it: when, what kind, who took part and how, where, how
    /// much it matters, and its words (the finished line, as the chronicle wrote it, or a text key with its values). A
    /// summary (<see cref="folded"/>) stands for several small ones of one kind, for one actor, in one season: its
    /// <see cref="count"/>, from <see cref="first"/> to <see cref="tick"/>. Pure (Tools/Tests checks it); saving it is in
    /// History.cs.
    /// </summary>
    public sealed partial class HistoryEntry
    {
        public int id;
        /// <summary>When it happened (a summary: the newest it stands for; a run merged within a day: the first).</summary>
        public int tick;
        /// <summary>A summary: the oldest it stands for. Otherwise its tick.</summary>
        public int first;
        /// <summary>How many times: more than one for a run merged within a day, or a summary.</summary>
        public int count = 1;
        /// <summary>A small key (<see cref="HistoryKinds"/>): "loss", "deed:traded", "world:began", "tie:pact:made".</summary>
        public string kind;
        /// <summary>Who took part, the one it is mostly of first (a summary is per that one).</summary>
        public List<HistoryActor> actors = new List<HistoryActor>();
        /// <summary>Where: a colony, a town, a tile, or none.</summary>
        public Ref place;
        /// <summary>How much it matters: <see cref="HistoryMath.Everyday"/>, or <see cref="HistoryMath.Milestone"/> and above (kept, never folded).</summary>
        public int weight = HistoryMath.Everyday;
        /// <summary>A text key for its words ("CC_Memory_Deed_traded"), with <see cref="values"/>; or null.</summary>
        public string key;
        public List<string> values;
        /// <summary>The finished line (the chronicle's), or null.</summary>
        public string line;
        /// <summary>A summary of older small events (see the class).</summary>
        public bool folded;

        public bool Weighty => HistoryMath.IsWeighty(weight);

        /// <summary>The one a summary of it would be for: its first actor, else its place.</summary>
        public Ref FoldActor => actors != null && actors.Count > 0 ? actors[0].who : place;

        public bool Involves(Ref r)
        {
            if (r.IsNone || actors == null) return false;
            for (int i = 0; i < actors.Count; i++)
                if (actors[i].who == r)
                    return true;
            return false;
        }

        public HistoryRole? RoleOf(Ref r)
        {
            if (r.IsNone || actors == null) return null;
            for (int i = 0; i < actors.Count; i++)
                if (actors[i].who == r)
                    return actors[i].role;
            return null;
        }

        public override string ToString() => $"#{id} {kind} @{tick}" + (count > 1 ? " x" + count : "") + " " + HistoryMath.PackActors(actors) + " at " + place;
    }

    /// <summary>
    /// The kinds of history events: small string keys, with families before a colon ("deed:traded" is of the family
    /// "deed"; "tie:pact:made" of "tie:pact" and of "tie"). A query for a family finds every kind in it.
    /// </summary>
    public static class HistoryKinds
    {
        /// <summary>The chronicle's kinds (ChronicleKind, in its order), as history kinds.</summary>
        public static readonly string[] Chronicle =
        {
            "wishMade", "wishGranted", "wishExpired", "wishDismissed", "petition", "meeting", "ambitionChosen", "ambitionAchieved",
            "ambitionFailed", "ambitionAbandoned", "personalTouch", "note", "council", "bond", "life", "secret", "loss", "dream",
            "stranger", "world"
        };

        /// <summary>The chronicle's milestones (ChronicleEntry.Milestone): losses, ambitions won or lost, life events, world events, secrets out.</summary>
        public static readonly string[] Milestones = { "loss", "ambitionAchieved", "ambitionFailed", "life", "world", "secret" };

        /// <summary>A world situation began or ended (of the family "world", the chronicle's kind for world events).</summary>
        public const string WorldBegan = "world:began";
        public const string WorldEnded = "world:ended";
        /// <summary>A deed a people remembers: "deed:" and the deed's kind (FactionMemory.Kinds).</summary>
        public const string Deed = "deed";
        /// <summary>A tie between two parties made or moved: "tie:", its kind, ":" and what happened (made, kept, calledIn, broken, ended, putOff).</summary>
        public const string Tie = "tie";

        /// <summary>The history kind of a chronicle entry's kind (its number in ChronicleKind); "note" for one it doesn't know.</summary>
        public static string OfChronicle(int chronicleKind) => chronicleKind >= 0 && chronicleKind < Chronicle.Length ? Chronicle[chronicleKind] : "note";

        public static string OfDeed(string deedKind) => Deed + ":" + (string.IsNullOrEmpty(deedKind) ? "?" : deedKind);

        public static string OfTie(Tie.Kind kind, string what) => Tie + ":" + HistoryMath.LowerFirst(kind.ToString()) + ":" + what;

        /// <summary>The family of a kind (what comes before its first colon), or the kind itself.</summary>
        public static string Family(string kind)
        {
            if (kind == null) return null;
            int i = kind.IndexOf(':');
            return i < 0 ? kind : kind.Substring(0, i);
        }

        /// <summary>The kind is the one asked for, or of that family ("deed" asks for every deed, "tie:pact" for a pact's every turn).</summary>
        public static bool Matches(string kind, string asked)
        {
            if (kind == null || asked == null) return false;
            if (kind.Length == asked.Length) return string.Equals(kind, asked, StringComparison.Ordinal);
            return kind.Length > asked.Length && kind[asked.Length] == ':' && kind.StartsWith(asked, StringComparison.Ordinal);
        }

        /// <summary>The kind matches any of these (none asked: every kind).</summary>
        public static bool MatchesAny(string kind, IReadOnlyList<string> asked)
        {
            if (asked == null || asked.Count == 0) return true;
            for (int i = 0; i < asked.Count; i++)
                if (Matches(kind, asked[i]))
                    return true;
            return false;
        }

        /// <summary>The kind and each family it belongs to, longest first ("tie:pact:made", "tie:pact", "tie").</summary>
        public static IEnumerable<string> Levels(string kind)
        {
            if (string.IsNullOrEmpty(kind)) yield break;
            yield return kind;
            for (int i = kind.Length - 1; i > 0; i--)
                if (kind[i] == ':')
                    yield return kind.Substring(0, i);
        }
    }

    /// <summary>The history's rules: weights, seasons, folding and its bounds, how actors are written. Pure: Tools/Tests checks them.</summary>
    public static class HistoryMath
    {
        public const int DayTicks = 60000;
        /// <summary>A season (a quadrum): fifteen days.</summary>
        public const int SeasonTicks = 15 * DayTicks;

        /// <summary>An everyday event: folded into its season's summary once it is old.</summary>
        public const int Everyday = 1;
        /// <summary>A milestone, as the chronicle's (ChronicleEntry.Milestone): kept as it is.</summary>
        public const int Milestone = 2;

        /// <summary>Small events older than this many days fold into their season's summary.</summary>
        public const int FoldDays = 60;
        /// <summary>When the last sixty days and the milestones alone are over the bound, small events fold sooner: from these many days, in turn.</summary>
        public static readonly int[] CrowdedFoldDays = { 30, 15 };
        /// <summary>The most events kept; past it the oldest small ones (older seasons' summaries first) go, then the oldest milestones.</summary>
        public const int MaxEvents = 800;
        /// <summary>The newest this many always stay, whatever they are.</summary>
        public const int KeepNewest = 200;
        /// <summary>A small event like one of the same kind, actors and place within this long adds to its count instead (a raid's fallen, a day's trading).</summary>
        public const int MergeTicks = DayTicks;
        /// <summary>A deed of at least this size (FactionDeed.weight) is a milestone of the colony's dealings with a people.</summary>
        public const int DeedMilestone = 15;

        public static bool IsWeighty(int weight) => weight >= Milestone;

        /// <summary>A chronicle entry's weight: its milestones (the chronicle's own rule) are milestones here too.</summary>
        public static int OfChronicleKind(string kind) => Array.IndexOf(HistoryKinds.Milestones, kind) >= 0 ? Milestone : Everyday;

        public static int OfDeed(int deedWeight) => deedWeight >= DeedMilestone ? Milestone : Everyday;

        /// <summary>Pacts, truces, marriages, fealty, hostages and wards are milestones when made, kept, broken or ended; the rest everyday.</summary>
        public static int OfTie(Tie.Kind kind)
        {
            switch (kind)
            {
                case Tie.Kind.Pact:
                case Tie.Kind.Truce:
                case Tie.Kind.Marriage:
                case Tie.Kind.Fealty:
                case Tie.Kind.Hostage:
                case Tie.Kind.Ward:
                    return Milestone;
                default:
                    return Everyday;
            }
        }

        /// <summary>The season a tick of the game is in, counted on the planet's calendar (<paramref name="startAbs"/>: the game's first tick on it).</summary>
        public static int SeasonOf(int tick, int startAbs)
        {
            long abs = (long)tick + startAbs;
            if (abs < 0) abs = 0;
            return (int)(abs / SeasonTicks);
        }

        // ---------------- Words ----------------

        private static readonly string[] Roles = { "did", "for", "against", "about", "with" };

        public static string RoleName(HistoryRole role) => (int)role < Roles.Length ? Roles[(int)role] : "about";

        public static bool TryRole(string s, out HistoryRole role)
        {
            int i = Array.IndexOf(Roles, s);
            role = i < 0 ? HistoryRole.About : (HistoryRole)i;
            return i >= 0;
        }

        /// <summary>"Wish" from "wish", "deed" from "Deed".</summary>
        public static string LowerFirst(string s) => string.IsNullOrEmpty(s) || char.IsLower(s[0]) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

        /// <summary>Actors as saved: "about:p123 against:f3" (nothing for none).</summary>
        public static string PackActors(IReadOnlyList<HistoryActor> actors)
        {
            if (actors == null || actors.Count == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i].who.IsNone) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(RoleName(actors[i].role)).Append(':').Append(actors[i].who);
            }
            return sb.ToString();
        }

        /// <summary>Reads actors back; anything unreadable (an unknown role, a ref that isn't one) is left out.</summary>
        public static List<HistoryActor> UnpackActors(string s)
        {
            var list = new List<HistoryActor>();
            if (string.IsNullOrEmpty(s)) return list;
            foreach (var part in s.Split(' '))
            {
                int colon = part.IndexOf(':');
                if (colon <= 0 || !TryRole(part.Substring(0, colon), out var role)) continue;
                if (!Ref.TryParse(part.Substring(colon + 1), out var who)) continue;
                list.Add(new HistoryActor(who, role));
            }
            return list;
        }

        /// <summary>The number at the end of a ThingID ("Human123" is 123), as the chronicle keeps who an entry is about; -1 for none.</summary>
        public static int IdNumberOf(string thingId)
        {
            if (string.IsNullOrEmpty(thingId)) return -1;
            int end = thingId.Length, start = end;
            while (start > 0 && thingId[start - 1] >= '0' && thingId[start - 1] <= '9') start--;
            if (start == end || end - start > 9) return -1;
            return int.Parse(thingId.Substring(start), System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A line of the chronicle as a history event: same tick and words, its kind and weight by the chronicle's own, the
        /// colonist it is about as an actor (by the number in their ThingID), and the colony as its place.
        /// </summary>
        public static HistoryEntry FromChronicle(int tick, int chronicleKind, string text, string pawnId, Ref colony)
        {
            string kind = HistoryKinds.OfChronicle(chronicleKind);
            var e = new HistoryEntry { tick = tick, first = tick, kind = kind, line = text, place = colony, weight = OfChronicleKind(kind) };
            var who = Ref.Person(IdNumberOf(pawnId));
            if (!who.IsNone) e.actors.Add(new HistoryActor(who, HistoryRole.About));
            return e;
        }

        /// <summary>The same actors in the same order with the same roles.</summary>
        public static bool SameActors(IReadOnlyList<HistoryActor> a, IReadOnlyList<HistoryActor> b)
        {
            int na = a?.Count ?? 0, nb = b?.Count ?? 0;
            if (na != nb) return false;
            for (int i = 0; i < na; i++)
                if (!a[i].Equals(b[i]))
                    return false;
            return true;
        }

        public static bool SameValues(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            int na = a?.Count ?? 0, nb = b?.Count ?? 0;
            if (na != nb) return false;
            for (int i = 0; i < na; i++)
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                    return false;
            return true;
        }
    }

    /// <summary>
    /// The history itself: the events, oldest first, and their indexes by actor, by place, by kind (each family too) and
    /// by id, kept up as events come and rebuilt after a load or a fold. Queries return the newest first. Pure: Tools/Tests
    /// checks it; the game keeps one in the world ledger (History.cs).
    /// </summary>
    public sealed class HistoryBook
    {
        /// <summary>Oldest first (by tick, then id). Change it only through the book, or <see cref="Rebuild"/> after.</summary>
        public List<HistoryEntry> events = new List<HistoryEntry>();
        public int nextId = 1;

        private readonly Dictionary<Ref, List<HistoryEntry>> byActor = new Dictionary<Ref, List<HistoryEntry>>();
        private readonly Dictionary<Ref, List<HistoryEntry>> byPlace = new Dictionary<Ref, List<HistoryEntry>>();
        private readonly Dictionary<string, List<HistoryEntry>> byKind = new Dictionary<string, List<HistoryEntry>>();
        private readonly Dictionary<int, HistoryEntry> byId = new Dictionary<int, HistoryEntry>();

        /// <summary>Moves on with every change (an event added or merged, a fold, a load).</summary>
        public int Version { get; private set; }

        public int Count => events.Count;

        /// <summary>Oldest first, by tick and then id.</summary>
        private static int Order(HistoryEntry a, HistoryEntry b)
        {
            int c = a.tick.CompareTo(b.tick);
            return c != 0 ? c : a.id.CompareTo(b.id);
        }

        // ---------------- Adding ----------------

        /// <summary>
        /// Adds an event (its id given here), in its place by tick. With <paramref name="mergeTicks"/> above 0, a small
        /// event with no line of its own adds to the count of one just like it (kind, actors, place and key) from no
        /// longer ago than that, and that one comes back (its values kept only if the two had the same: "killed Ana" and
        /// "killed Bo" are two killed, with no name). Far over the bound (a quarter past it, which the daily fold
        /// never lets happen), the oldest small events go at once.
        /// </summary>
        public HistoryEntry Add(HistoryEntry e, int mergeTicks = 0)
        {
            if (e == null || string.IsNullOrEmpty(e.kind)) return null;
            e.actors ??= new List<HistoryActor>();
            e.actors.RemoveAll(a => a.who.IsNone);
            if (e.count < 1) e.count = 1;
            if (e.first <= 0 || e.first > e.tick) e.first = e.tick;
            if (mergeTicks > 0 && e.line == null && !e.Weighty && byKind.TryGetValue(e.kind, out var same))
            {
                for (int i = same.Count - 1; i >= 0; i--)
                {
                    var o = same[i];
                    if (o.tick < e.tick - mergeTicks) break;
                    if (o.kind != e.kind || o.folded || o.line != null || o.Weighty || o.tick > e.tick || o.place != e.place || o.key != e.key) continue;
                    if (!HistoryMath.SameActors(o.actors, e.actors)) continue;
                    o.count += e.count;
                    if (!HistoryMath.SameValues(o.values, e.values)) o.values = null;
                    Version++;
                    return o;
                }
            }
            e.id = nextId++;
            Insert(events, e);
            Index(e);
            Version++;
            if (events.Count > HistoryMath.MaxEvents + HistoryMath.MaxEvents / 4)
            {
                Caps.TrimKeeping(events, HistoryMath.MaxEvents, x => x.Weighty, HistoryMath.KeepNewest);
                Rebuild();
            }
            return e;
        }

        /// <summary>
        /// An older save's chronicle list, taken in on load: each line not here already comes in. Here already: the same
        /// tick and words (as many times as it is here), or a season's summary of its kind and of the one it is of whose
        /// days hold it (the line was folded into it, and counts there). Returns how many came in.
        /// </summary>
        public int TakeChronicle(IEnumerable<HistoryEntry> lines)
        {
            if (lines == null) return 0;
            var have = new Dictionary<string, int>();
            List<HistoryEntry> summaries = null;
            foreach (var e in events)
            {
                if (e.folded) (summaries ??= new List<HistoryEntry>()).Add(e);
                if (e.line == null) continue;
                string k = e.tick + "|" + e.line;
                have[k] = (have.TryGetValue(k, out int c) ? c : 0) + 1;
            }
            int n = 0;
            foreach (var e in lines)
            {
                if (e == null) continue;
                string k = e.tick + "|" + e.line;
                if (e.line != null && have.TryGetValue(k, out int c) && c > 0)
                {
                    have[k] = c - 1;
                    continue;
                }
                if (summaries != null && summaries.Exists(s => Holds(s, e))) continue;
                if (Add(e) != null) n++;
            }
            return n;
        }

        /// <summary>A season's summary that a small line was folded into: of its kind, of the one it is of, its days around the line's.</summary>
        public static bool Holds(HistoryEntry summary, HistoryEntry line) =>
            summary != null && line != null && summary.folded && !line.Weighty && summary.kind == line.kind && summary.FoldActor == line.FoldActor
            && line.tick >= summary.first && line.tick <= summary.tick;

        /// <summary>
        /// Gives someone already in an event another part (a battle's winner, known only after it was written): true when
        /// it changed. Their place in the event and the indexes stay as they are (the indexes go by who, not by part).
        /// </summary>
        public bool SetRole(HistoryEntry e, Ref who, HistoryRole role)
        {
            if (e?.actors == null || who.IsNone || !byId.TryGetValue(e.id, out var mine) || mine != e) return false;
            for (int i = 0; i < e.actors.Count; i++)
            {
                if (e.actors[i].who != who) continue;
                if (e.actors[i].role == role) return false;
                e.actors[i] = new HistoryActor(who, role);
                Version++;
                return true;
            }
            return false;
        }

        /// <summary>An event's values changed in place (an answer's "what came of it"): the book moves on, so views are built again. True when it is the book's.</summary>
        public bool Touch(HistoryEntry e)
        {
            if (e == null || !byId.TryGetValue(e.id, out var mine) || mine != e) return false;
            Version++;
            return true;
        }

        /// <summary>Into a list kept oldest first: at the end, unless it is older than the last.</summary>
        private static void Insert(List<HistoryEntry> list, HistoryEntry e)
        {
            int i = list.Count;
            while (i > 0 && Order(list[i - 1], e) > 0) i--;
            list.Insert(i, e);
        }

        private void Index(HistoryEntry e)
        {
            byId[e.id] = e;
            for (int i = 0; i < e.actors.Count; i++)
            {
                var who = e.actors[i].who;
                bool twice = false;
                for (int j = 0; j < i && !twice; j++) twice = e.actors[j].who == who;
                if (twice) continue;
                if (!byActor.TryGetValue(who, out var l)) byActor[who] = l = new List<HistoryEntry>();
                Insert(l, e);
            }
            if (!e.place.IsNone)
            {
                if (!byPlace.TryGetValue(e.place, out var l)) byPlace[e.place] = l = new List<HistoryEntry>();
                Insert(l, e);
            }
            foreach (var k in HistoryKinds.Levels(e.kind))
            {
                if (!byKind.TryGetValue(k, out var l)) byKind[k] = l = new List<HistoryEntry>();
                Insert(l, e);
            }
        }

        /// <summary>
        /// Takes in a saved list (an older save: nothing): drops what can't be read, puts them in order, keeps ids unique
        /// and the next id past them, and builds the indexes.
        /// </summary>
        public void Load(List<HistoryEntry> list, int savedNextId)
        {
            events = list ?? new List<HistoryEntry>();
            events.RemoveAll(e => e == null || string.IsNullOrEmpty(e.kind));
            var seen = new HashSet<int>();
            int max = 0;
            foreach (var e in events) max = Math.Max(max, e.id);
            nextId = Math.Max(Math.Max(1, savedNextId), max + 1);
            foreach (var e in events)
            {
                e.actors ??= new List<HistoryActor>();
                if (e.count < 1) e.count = 1;
                if (e.first < 0 || e.first > e.tick) e.first = e.tick;
                if (e.id <= 0 || !seen.Add(e.id)) e.id = nextId++;
            }
            Rebuild();
        }

        /// <summary>Puts the events in order and builds every index again.</summary>
        public void Rebuild()
        {
            events.Sort(Order);
            byActor.Clear();
            byPlace.Clear();
            byKind.Clear();
            byId.Clear();
            foreach (var e in events) Index(e);
            Version++;
        }

        // ---------------- Asking ----------------

        public HistoryEntry Get(int id) => byId.TryGetValue(id, out var e) ? e : null;

        /// <summary>What someone took part in (any role), newest first: only these kinds (or their families), only since then, at most so many.</summary>
        public List<HistoryEntry> Of(Ref who, IReadOnlyList<string> kinds = null, int since = int.MinValue, int max = int.MaxValue) =>
            Pick(who.IsNone ? null : byActor.TryGetValue(who, out var l) ? l : null, kinds, since, max, Ref.None, Ref.None);

        /// <summary>What happened at a place (a colony, a town, a tile), newest first, filtered the same way.</summary>
        public List<HistoryEntry> At(Ref place, IReadOnlyList<string> kinds = null, int since = int.MinValue, int max = int.MaxValue) =>
            Pick(place.IsNone ? null : byPlace.TryGetValue(place, out var l) ? l : null, kinds, since, max, Ref.None, Ref.None);

        /// <summary>Events of a kind (or family), newest first: of someone and at a place when those are given.</summary>
        public List<HistoryEntry> OfKind(string kind, Ref who = default, Ref place = default, int since = int.MinValue, int max = int.MaxValue) =>
            Pick(Smallest(kind, who, place), kind == null ? null : new[] { kind }, since, max, who, place);

        /// <summary>The newest event of a kind (or family), of someone and at a place when those are given; null for none.</summary>
        public HistoryEntry Latest(string kind, Ref who = default, Ref place = default)
        {
            var l = OfKind(kind, who, place, int.MinValue, 1);
            return l.Count > 0 ? l[0] : null;
        }

        /// <summary>How many times it happened (a summary counts all it stands for): of a kind or family (null: any), of someone, at a place, since then.</summary>
        public int CountOf(string kind = null, Ref who = default, Ref place = default, int since = int.MinValue)
        {
            var list = Smallest(kind, who, place);
            if (list == null) return 0;
            int n = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var e = list[i];
                if (e.tick < since) break;
                if (Fits(e, kind == null ? null : new[] { kind }, who, place)) n += e.count;
            }
            return n;
        }

        /// <summary>The shortest index list that holds everything asked (null when one of them holds nothing).</summary>
        private List<HistoryEntry> Smallest(string kind, Ref who, Ref place)
        {
            List<HistoryEntry> best = events;
            if (kind != null)
            {
                if (!byKind.TryGetValue(kind, out var k)) return null;
                best = k;
            }
            if (!who.IsNone)
            {
                if (!byActor.TryGetValue(who, out var a)) return null;
                if (a.Count < best.Count) best = a;
            }
            if (!place.IsNone)
            {
                if (!byPlace.TryGetValue(place, out var p)) return null;
                if (p.Count < best.Count) best = p;
            }
            return best;
        }

        private static bool Fits(HistoryEntry e, IReadOnlyList<string> kinds, Ref who, Ref place) =>
            HistoryKinds.MatchesAny(e.kind, kinds) && (who.IsNone || e.Involves(who)) && (place.IsNone || e.place == place);

        private static List<HistoryEntry> Pick(List<HistoryEntry> list, IReadOnlyList<string> kinds, int since, int max, Ref who, Ref place)
        {
            var found = new List<HistoryEntry>();
            if (list == null || max <= 0) return found;
            for (int i = list.Count - 1; i >= 0 && found.Count < max; i--)
            {
                var e = list[i];
                if (e.tick < since) break;
                if (Fits(e, kinds, who, place)) found.Add(e);
            }
            return found;
        }

        // ---------------- Folding ----------------

        /// <summary>
        /// Once a day: small events older than <see cref="HistoryMath.FoldDays"/> fold into one summary per kind, per the
        /// one they are of (<see cref="HistoryEntry.FoldActor"/>) and per season; milestones stay as they are. Still over
        /// <see cref="HistoryMath.MaxEvents"/>: the oldest small ones past those days go first (older seasons' summaries),
        /// so the last sixty days stay whole; only when they and the milestones alone are too many do small events fold
        /// sooner (<see cref="HistoryMath.CrowdedFoldDays"/>), and then the oldest small ones go, then the oldest
        /// milestones, the newest <see cref="HistoryMath.KeepNewest"/> always kept. Returns how many events fewer there are.
        /// </summary>
        public int Fold(int now, int startAbs)
        {
            int before = events.Count;
            int cutoff = now - HistoryMath.FoldDays * HistoryMath.DayTicks;
            bool changed = FoldOlderThan(cutoff, startAbs);
            if (events.Count > HistoryMath.MaxEvents && DropSmallBefore(cutoff, events.Count - HistoryMath.MaxEvents) > 0) changed = true;
            foreach (int days in HistoryMath.CrowdedFoldDays)
            {
                if (events.Count <= HistoryMath.MaxEvents) break;
                changed |= FoldOlderThan(now - days * HistoryMath.DayTicks, startAbs);
            }
            if (Caps.TrimKeeping(events, HistoryMath.MaxEvents, e => e.Weighty, HistoryMath.KeepNewest) > 0) changed = true;
            if (changed) Rebuild();
            return before - events.Count;
        }

        /// <summary>Drops up to <paramref name="most"/> of the oldest small events from before <paramref name="cutoff"/>. Returns how many went.</summary>
        private int DropSmallBefore(int cutoff, int most)
        {
            HashSet<HistoryEntry> drop = null;
            foreach (var e in events)
            {
                if (e.tick >= cutoff || (drop?.Count ?? 0) >= most) break;
                if (!e.Weighty) (drop ??= new HashSet<HistoryEntry>()).Add(e);
            }
            if (drop == null) return 0;
            events.RemoveAll(drop.Contains);
            return drop.Count;
        }

        private readonly struct FoldKey : IEquatable<FoldKey>
        {
            private readonly string kind;
            private readonly Ref of;
            private readonly int season;

            public FoldKey(string kind, Ref of, int season)
            {
                this.kind = kind;
                this.of = of;
                this.season = season;
            }

            public bool Equals(FoldKey o) => season == o.season && of == o.of && string.Equals(kind, o.kind, StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is FoldKey k && Equals(k);

            public override int GetHashCode() => ((kind?.GetHashCode() ?? 0) * 397 ^ of.GetHashCode()) * 31 + season;
        }

        /// <summary>Folds the small events from before <paramref name="cutoff"/>; true if anything folded.</summary>
        private bool FoldOlderThan(int cutoff, int startAbs)
        {
            var groups = new Dictionary<FoldKey, List<HistoryEntry>>();
            List<FoldKey> order = null;
            foreach (var e in events)
            {
                if (e.tick >= cutoff || e.Weighty) continue;
                var k = new FoldKey(e.kind, e.FoldActor, HistoryMath.SeasonOf(e.first, startAbs));
                if (!groups.TryGetValue(k, out var g))
                {
                    groups[k] = g = new List<HistoryEntry>();
                    (order ??= new List<FoldKey>()).Add(k);
                }
                g.Add(e);
            }
            if (order == null) return false;
            var gone = new HashSet<HistoryEntry>();
            foreach (var k in order)
            {
                var g = groups[k];
                if (g.Count < 2) continue;
                var into = Summary(g);
                foreach (var e in g)
                    if (e != into)
                        gone.Add(e);
            }
            if (gone.Count == 0) return false;
            events.RemoveAll(gone.Contains);
            events.Sort(Order);
            return true;
        }

        /// <summary>One summary for a group of small events: the group's summary if it has one (or its oldest event), with the rest added in.</summary>
        private static HistoryEntry Summary(List<HistoryEntry> g)
        {
            HistoryEntry into = null;
            foreach (var e in g)
                if (e.folded && (into == null || e.id < into.id))
                    into = e;
            if (into == null)
            {
                into = g[0];
                foreach (var e in g)
                    if (e.id < into.id)
                        into = e;
            }
            int count = 0, first = int.MaxValue, last = int.MinValue, weight = 0;
            bool samePlace = true, sameKey = true, sameValues = true;
            var actors = new List<HistoryActor>(into.actors);
            bool led = into.actors.Count > 0;
            var lead = led ? into.actors[0] : default;
            foreach (var e in g)
            {
                count += e.count;
                first = Math.Min(first, e.first);
                last = Math.Max(last, e.tick);
                weight = Math.Max(weight, e.weight);
                samePlace &= e.place == into.place;
                sameKey &= e.key == into.key;
                sameValues &= HistoryMath.SameValues(e.values, into.values);
                // Only those in every one stay, in the first one's order (the one it is of always among them).
                if (e != into) actors.RemoveAll(a => !e.actors.Contains(a));
            }
            // The one it is of stays first, as its group was made by them.
            if (led && (actors.Count == 0 || actors[0].who != lead.who))
            {
                actors.RemoveAll(a => a.who == lead.who);
                actors.Insert(0, lead);
            }
            into.count = count;
            into.first = first;
            into.tick = last;
            into.weight = Math.Min(weight, HistoryMath.Milestone - 1);
            into.folded = true;
            into.line = null;
            into.actors = actors;
            if (!samePlace) into.place = Ref.None;
            if (!sameKey) into.key = null;
            if (!sameValues) into.values = null;
            return into;
        }
    }
}
