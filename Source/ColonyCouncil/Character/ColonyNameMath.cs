using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's one name (phase 4 of the review, part 1: the player's decision 6 of 6 November 2026), as rules with no
    /// game types so Tools/Tests can check them. Four systems say what the colony is known for: its ways (the council's
    /// choices: merciful, thrifty), what its work made it (the colony's traits: walled, learned), its renown among the
    /// peoples (how far its name carries and what it is known for there) and what each people remembers. Their data stays
    /// where other systems read it; the player reads one card (Character/ColonyName.cs) and one chronicle sentence when the
    /// name changes. Here: the one list of what it is called (<see cref="Order"/>), the name as a key to see it change
    /// (<see cref="Key"/>), what changed (<see cref="Diff"/>), and the clauses of the sentence (<see cref="Clauses"/>).
    /// </summary>
    public static class ColonyNameMath
    {
        /// <summary>One thing the colony is called: an id ("way:Mercy+", "trait:Council_Trait_Walled"), how strongly (2: far and wide), and whether it is one of its ways.</summary>
        public readonly struct Called
        {
            public readonly string id;
            public readonly int strength;
            public readonly bool way;

            public Called(string id, int strength, bool way)
            {
                this.id = id;
                this.strength = strength;
                this.way = way;
            }
        }

        public const string WayPrefix = "way:", TraitPrefix = "trait:";

        /// <summary>A way's id by its pole: "way:Mercy+" merciful, "way:Mercy-" harsh.</summary>
        public static string WayId(string way, bool positive) => WayPrefix + way + (positive ? "+" : "-");

        public static string TraitId(string defName) => TraitPrefix + defName;

        /// <summary>
        /// What it is called, as one list: the strongest first (a way known far and wide), at the same strength the council's
        /// ways before what the work made it, then in the order given (the ways by how far along, the traits by their order).
        /// </summary>
        public static List<Called> Order(IEnumerable<Called> items)
        {
            var list = items?.Where(c => c.id != null && c.strength > 0).ToList() ?? new List<Called>();
            var indexed = list.Select((c, i) => (c, i)).ToList();
            indexed.Sort((a, b) =>
            {
                int s = b.c.strength.CompareTo(a.c.strength);
                if (s != 0) return s;
                if (a.c.way != b.c.way) return a.c.way ? -1 : 1;
                return a.i.CompareTo(b.i);
            });
            return indexed.Select(x => x.c).ToList();
        }

        // ================= The name as a key =================

        /// <summary>A part not read (its setting off, renown not yet looked at): never a change, and kept as it was.</summary>
        public const string Unread = "?";

        /// <summary>
        /// The name as one line: what it is called (each id with its strength) and what it is known for beyond its walls
        /// (a KnownFor's name; "None" while its name carries nowhere or for nothing). <paramref name="called"/> null and
        /// <paramref name="knownFor"/> null are parts not read.
        /// </summary>
        public static string Key(IEnumerable<Called> called, string knownFor)
        {
            var sb = new StringBuilder("called=");
            if (called == null) sb.Append(Unread);
            else sb.Append(string.Join(",", called.Where(c => c.id != null && c.strength > 0).Select(c => c.id + ":" + c.strength.ToString(CultureInfo.InvariantCulture))));
            sb.Append(";for=").Append(knownFor ?? Unread);
            return sb.ToString();
        }

        private static void Parts(string key, out string called, out string knownFor)
        {
            called = Unread;
            knownFor = Unread;
            if (string.IsNullOrEmpty(key)) return;
            foreach (var part in key.Split(';'))
            {
                if (part.StartsWith("called=", StringComparison.Ordinal)) called = part.Substring(7);
                else if (part.StartsWith("for=", StringComparison.Ordinal)) knownFor = part.Substring(4);
            }
        }

        private static Dictionary<string, int> CalledOf(string part)
        {
            var d = new Dictionary<string, int>();
            if (part == null || part == Unread || part.Length == 0) return d;
            foreach (var item in part.Split(','))
            {
                int i = item.LastIndexOf(':');
                if (i <= 0 || !int.TryParse(item.Substring(i + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int s)) continue;
                d[item.Substring(0, i)] = s;
            }
            return d;
        }

        /// <summary>The key kept after a look: each part as it is now, or as it was where it isn't read now (a setting turned off and on tells nothing).</summary>
        public static string Merge(string was, string now)
        {
            Parts(was, out var wc, out var wf);
            Parts(now, out var nc, out var nf);
            return "called=" + (nc == Unread ? wc : nc) + ";for=" + (nf == Unread ? wf : nf);
        }

        /// <summary>What changed in the colony's name between two looks.</summary>
        public sealed class Change
        {
            /// <summary>Called it now (a known way or trait it wasn't).</summary>
            public readonly List<string> started = new List<string>();
            /// <summary>Called it far and wide now (a way that went from known to renowned, or came in renowned).</summary>
            public readonly List<string> wider = new List<string>();
            /// <summary>No longer called it at all.</summary>
            public readonly List<string> stopped = new List<string>();
            /// <summary>Known for something beyond its walls it wasn't (a KnownFor's name), or null.</summary>
            public string forNow;
            /// <summary>No longer known for this beyond its walls (with nothing else in its place), or null.</summary>
            public string forGone;

            public bool Empty => started.Count == 0 && wider.Count == 0 && stopped.Count == 0 && forNow == null && forGone == null;
        }

        /// <summary>
        /// What changed from one look to the next. A part not read on either side changes nothing (the first look after an
        /// older save is quiet: it has no key at all, and is set without a word, ColonyName.Look). A way known far and wide
        /// that is only known now is still what it is called, and says nothing.
        /// </summary>
        public static Change Diff(string was, string now)
        {
            var c = new Change();
            Parts(was, out var wc, out var wf);
            Parts(now, out var nc, out var nf);
            if (wc != Unread && nc != Unread)
            {
                var a = CalledOf(wc);
                var b = CalledOf(nc);
                foreach (var kv in b)
                {
                    if (!a.TryGetValue(kv.Key, out int before)) (kv.Value >= 2 ? c.wider : c.started).Add(kv.Key);
                    else if (before < 2 && kv.Value >= 2) c.wider.Add(kv.Key);
                }
                foreach (var kv in a)
                    if (!b.ContainsKey(kv.Key))
                        c.stopped.Add(kv.Key);
            }
            if (wf != Unread && nf != Unread && wf != nf)
            {
                if (nf != NoKnownFor) c.forNow = nf;
                else c.forGone = wf;
            }
            return c;
        }

        /// <summary>KnownFor.None's name: known for nothing beyond its walls (yet, or any more).</summary>
        public const string NoKnownFor = "None";

        /// <summary>
        /// What the colony is known for beyond its walls, held: the kind it was last known for while that kind is still
        /// enough by renown's own rule (<paramref name="knownAt"/> points and <paramref name="share"/> of all), so two kinds
        /// close together don't take turns in the chronicle; else what renown reads now (<paramref name="fresh"/>). Kinds by
        /// their number (0 none).
        /// </summary>
        public static int HeldKind(int told, float[] byKind, float knownAt, float share, int fresh)
        {
            if (byKind == null || told <= 0 || told >= byKind.Length) return fresh;
            float all = 0f;
            for (int i = 1; i < byKind.Length; i++)
                if (byKind[i] > 0f)
                    all += byKind[i];
            return byKind[told] >= knownAt && byKind[told] >= share * all ? told : fresh;
        }

        /// <summary>The kind a key keeps for beyond its walls (a KnownFor's name), or null for none read.</summary>
        public static string KnownForOf(string key)
        {
            Parts(key, out _, out var f);
            return f == Unread ? null : f;
        }

        /// <summary>
        /// The clauses of the one chronicle sentence, in order, each a key and what it is about: "Started" (the words
        /// started), "Wider", "Stopped", "For", "ForGone"; every clause after the first speaks of the colony as "it" (its key
        /// with "_It"). The sentence is the clauses joined ("CC_Name_Changed_Line").
        /// </summary>
        public static List<(string key, string what)> Clauses(Change c)
        {
            var list = new List<(string, string)>();
            if (c == null || c.Empty) return list;
            void Add(string name, string what) => list.Add(("CC_Name_Changed_" + name + (list.Count > 0 ? "_It" : ""), what));
            if (c.started.Count > 0) Add("Started", "started");
            if (c.wider.Count > 0) Add("Wider", "wider");
            if (c.stopped.Count > 0) Add("Stopped", "stopped");
            if (c.forNow != null) Add("For", c.forNow);
            if (c.forGone != null) Add("ForGone", c.forGone);
            return list;
        }

        /// <summary>Every clause key the sentence may use (for the tests: each has words).</summary>
        public static IEnumerable<string> ClauseKeys()
        {
            foreach (var n in new[] { "Started", "Wider", "Stopped", "For", "ForGone" })
            {
                yield return "CC_Name_Changed_" + n;
                yield return "CC_Name_Changed_" + n + "_It";
            }
        }

        /// <summary>
        /// The card's first line, what it is known for and how far its name carries, by what there is: called something at
        /// home (<paramref name="called"/>), known for something beyond its walls (<paramref name="knownFor"/>), and whether
        /// renown is read at all (<paramref name="renown"/>: its setting).
        /// </summary>
        public static string CardKey(bool called, bool knownFor, bool renown)
        {
            if (!renown) return called ? "CC_Name_Card_CalledHome" : "CC_Name_Card_BareHome";
            if (called) return knownFor ? "CC_Name_Card_CalledFor" : "CC_Name_Card_Called";
            return knownFor ? "CC_Name_Card_For" : "CC_Name_Card_Bare";
        }
    }
}
