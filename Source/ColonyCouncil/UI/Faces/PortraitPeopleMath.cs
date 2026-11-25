using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColonyCouncil
{
    /// <summary>
    /// One line of the library folder's people.csv: one of the colony's people or a notable, what the game sees in them and
    /// what the picture they hold does not show (<see cref="PortraitPeopleMath"/>).
    /// </summary>
    public sealed class PersonSeen
    {
        /// <summary>The pawn's number (as the save keeps it with their picture), their short name (as a picture's pawn column names them) and their full name.</summary>
        public string id = "", name = "", fullName = "";
        /// <summary>colonist, slave, prisoner or notable.</summary>
        public string standing = "";
        /// <summary>The picture they hold (its file from the library folder, forward slashes; "" where it is a mod's or none), its record, and the version shown now (its file, where another).</summary>
        public string picture = "", record = "", shown = "";
        /// <summary>How far the picture is from them (the matcher's distance, two decimals; "" without a picture).</summary>
        public string distance = "";
        /// <summary>Everything the matcher reads of them, in words (<see cref="PortraitLook.Words"/>).</summary>
        public string words = "";
        /// <summary>What the picture does not show of them, and what changed since a save before (each with the date first seen).</summary>
        public string lacks = "", changed = "";
        /// <summary>What the next save compares against: their age band, looks, marks, role and titles.</summary>
        public string age = "", looks = "", marks = "", role = "", title = "";
        /// <summary>The date they were first listed.</summary>
        public string first = "";
        /// <summary>Their order in the list (0 a colonist, 1 a slave, 2 a prisoner, 3 a notable; not written).</summary>
        public int rank;
    }

    /// <summary>
    /// What the game sees in each person and needs for them (the player, 25 November 2026: "the game should provide details
    /// of which variants it needs. for example the colonist Myst is some mage or so, so after the save is created and the
    /// image is assigned, details like this should be added to the logs and read by studio, so that specific versions can
    /// be created for this ... but also whenever the pawn develops or changes somehow in the game"), without game types
    /// (Tools/Tests try it):
    /// <list type="bullet">
    /// <item>what the picture someone holds does not show of them (<see cref="Lacks"/>): another age band, each look and its
    /// role they have and it hasn't, each mark of theirs neither it nor the version shown has (on its side);</item>
    /// <item>what changed since a save before (<see cref="Changes"/>): a new age band, look, mark, role or title, with the
    /// date first seen; kept while it still holds and the picture does not show it (a title while it is held);</item>
    /// <item>the list (<see cref="Rows"/>, <see cref="Csv"/>, <see cref="Read"/>): the library folder's people.csv, written
    /// at every save, one line for each of the colony's people and each notable.</item>
    /// </list>
    /// </summary>
    public static class PortraitPeopleMath
    {
        public const string ListFile = "people.csv";

        /// <summary>people.csv's columns, in the order written.</summary>
        public static readonly string[] Columns =
        {
            "id", "name", "fullName", "standing", "picture", "record", "shown", "distance", "words", "lacks", "changed", "age", "looks", "marks", "role", "title", "first"
        };

        /// <summary>The words of a standing, by the list's order.</summary>
        public static readonly string[] Standings = { "colonist", "slave", "prisoner", "notable" };

        /// <summary>The separator inside a cell that holds several (looks, marks, titles, lacks, changes).</summary>
        public const string Sep = "; ";

        // ---------------- What the picture does not show ----------------

        /// <summary>A mark as the list writes it: its word, and its side where it has one ("lostEye-left", "scar").</summary>
        public static string MarkWord(string mark, PortraitSide side)
        {
            string k = PortraitLibraryMath.MarkKey(mark);
            if (side == PortraitSide.None || !PortraitLibraryMath.Sided(k)) return k;
            return k + "-" + (side == PortraitSide.Left ? "left" : side == PortraitSide.Right ? "right" : "both");
        }

        /// <summary>A person's marks as the list writes them, in order.</summary>
        public static List<string> MarksOf(PortraitLook l) =>
            l == null ? new List<string>() : l.marks.Select(m => MarkWord(m, l.SidesOf(m))).Distinct().OrderBy(m => m, StringComparer.Ordinal).ToList();

        /// <summary>
        /// What the picture someone holds (<paramref name="root"/>; <paramref name="shown"/> the version shown for them now)
        /// does not show of them: "age:&lt;band&gt;" for another age band, "look:&lt;word&gt;" for each look of theirs it
        /// hasn't, "role:&lt;work&gt;" for their work where it shows another or none, "mark:&lt;mark&gt;" for each mark of
        /// theirs that neither shows. Everything where there is no picture.
        /// </summary>
        public static List<string> Lacks(PortraitLook l, PortraitRecord root, PortraitRecord shown)
        {
            var list = new List<string>();
            if (l == null) return list;
            if (root == null || l.age != root.age) list.Add("age:" + PortraitLibraryMath.Word(l.age));
            foreach (var w in l.looks.OrderBy(x => l.specials.Contains(x) ? 0 : 1).ThenBy(x => x, StringComparer.OrdinalIgnoreCase))
                if (root == null || !root.looks.Contains(w))
                    list.Add("look:" + w);
            if (l.role.Length > 0 && (root == null || !string.Equals(root.role, l.role, StringComparison.OrdinalIgnoreCase))) list.Add("role:" + l.role);
            var has = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in new[] { root, shown })
                if (r != null)
                    foreach (var m in r.marks)
                        has.Add(PortraitLibraryMath.MarkKey(m));
            foreach (var m in l.marks.Select(PortraitLibraryMath.MarkKey).Distinct().OrderBy(m => m, StringComparer.Ordinal))
                if (!has.Contains(m))
                    list.Add("mark:" + MarkWord(m, l.SidesOf(m)));
            return list;
        }

        // ---------------- What changed ----------------

        /// <summary>A cell's items (looks, marks, titles), the separator's spaces aside.</summary>
        public static List<string> Items(string cell) =>
            string.IsNullOrWhiteSpace(cell) ? new List<string>() : cell.Split(';').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

        /// <summary>A change as the list writes it: "kind:value@date".</summary>
        public static string Change(string what, string date) => what + "@" + (date ?? "");

        /// <summary>A change read back: what (kind:value) and its date.</summary>
        public static bool ReadChange(string text, out string what, out string date)
        {
            what = date = "";
            if (string.IsNullOrWhiteSpace(text)) return false;
            int at = text.LastIndexOf('@');
            what = (at >= 0 ? text.Substring(0, at) : text).Trim();
            date = at >= 0 ? text.Substring(at + 1).Trim() : "";
            return what.IndexOf(':') > 0;
        }

        /// <summary>
        /// What changed for a person since a save before (<paramref name="old"/>: their line in the file as it was, null where
        /// they weren't in it: nothing yet), with the date each was first seen: the changes the file had, kept while they
        /// still hold and the picture does not show them (a title while it is held), and the new ones of this save (a new
        /// age band, look, mark, role or title) dated <paramref name="today"/>.
        /// </summary>
        public static List<string> Changes(PersonSeen old, PersonSeen now, string today)
        {
            var list = new List<string>();
            if (old == null || now == null) return list;
            var lacks = new HashSet<string>(Items(now.lacks), StringComparer.OrdinalIgnoreCase);
            var looks = new HashSet<string>(Items(now.looks), StringComparer.OrdinalIgnoreCase);
            var marks = new HashSet<string>(Items(now.marks), StringComparer.Ordinal);
            var titles = new HashSet<string>(Items(now.title), StringComparer.OrdinalIgnoreCase);
            bool Holds(string what)
            {
                int c = what.IndexOf(':');
                string kind = what.Substring(0, c), value = what.Substring(c + 1);
                switch (kind)
                {
                    case "age": return string.Equals(now.age, value, StringComparison.OrdinalIgnoreCase) && lacks.Contains(what);
                    case "look": return looks.Contains(value) && lacks.Contains(what);
                    case "role": return string.Equals(now.role, value, StringComparison.OrdinalIgnoreCase) && lacks.Contains(what);
                    case "mark": return marks.Contains(value) && lacks.Contains(what);
                    case "title": return titles.Contains(value);
                }
                return false;
            }
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in Items(old.changed))
                if (ReadChange(c, out var what, out var date) && Holds(what) && seen.Add(what))
                    list.Add(Change(what, date));
            var fresh = new List<string>();
            if (old.age.Length > 0 && now.age.Length > 0 && !string.Equals(old.age, now.age, StringComparison.OrdinalIgnoreCase)) fresh.Add("age:" + now.age);
            var oldLooks = new HashSet<string>(Items(old.looks), StringComparer.OrdinalIgnoreCase);
            fresh.AddRange(Items(now.looks).Where(w => !oldLooks.Contains(w)).Select(w => "look:" + w));
            var oldMarks = new HashSet<string>(Items(old.marks), StringComparer.Ordinal);
            fresh.AddRange(Items(now.marks).Where(m => !oldMarks.Contains(m)).Select(m => "mark:" + m));
            if (now.role.Length > 0 && !string.Equals(old.role, now.role, StringComparison.OrdinalIgnoreCase)) fresh.Add("role:" + now.role);
            var oldTitles = new HashSet<string>(Items(old.title), StringComparer.OrdinalIgnoreCase);
            fresh.AddRange(Items(now.title).Where(t => !oldTitles.Contains(t)).Select(t => "title:" + t));
            foreach (var what in fresh)
                if (Holds(what) && seen.Add(what))
                    list.Add(Change(what, today));
            return list;
        }

        // ---------------- The list ----------------

        /// <summary>
        /// The list to write: one line per person (the first of a number seen twice kept), what changed worked out against
        /// the file as it was (<paramref name="old"/>), the date each was first listed kept from it or <paramref name="today"/>;
        /// in the colony's order (colonists, slaves, prisoners, notables), then by name.
        /// </summary>
        public static List<PersonSeen> Rows(IEnumerable<PersonSeen> fresh, IEnumerable<PersonSeen> old, string today)
        {
            var before = new Dictionary<string, PersonSeen>(StringComparer.Ordinal);
            foreach (var o in old ?? Enumerable.Empty<PersonSeen>())
                if (o != null && o.id.Length > 0 && !before.ContainsKey(o.id))
                    before[o.id] = o;
            var rows = new List<PersonSeen>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var n in fresh ?? Enumerable.Empty<PersonSeen>())
            {
                if (n == null || n.id.Length == 0 || !ids.Add(n.id)) continue;
                before.TryGetValue(n.id, out var o);
                n.changed = string.Join(Sep, Changes(o, n, today));
                n.first = o != null && o.first.Length > 0 ? o.first : today ?? "";
                rows.Add(n);
            }
            return rows.OrderBy(r => r.rank).ThenBy(r => r.name, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.id, StringComparer.Ordinal).ToList();
        }

        private static string[] Cells(PersonSeen r) =>
            new[] { r.id, r.name, r.fullName, r.standing, r.picture, r.record, r.shown, r.distance, r.words, r.lacks, r.changed, r.age, r.looks, r.marks, r.role, r.title, r.first };

        /// <summary>One line of the list.</summary>
        public static string Line(PersonSeen r) => string.Join(",", Cells(r).Select(v => PortraitDeleteMath.Quote(v ?? "", ',')));

        /// <summary>The list's text: the columns, then a line each, Windows' line endings (Excel and the Studio read it as it is).</summary>
        public static string Csv(IEnumerable<PersonSeen> rows)
        {
            var sb = new StringBuilder();
            sb.Append(string.Join(",", Columns)).Append("\r\n");
            foreach (var r in rows ?? Enumerable.Empty<PersonSeen>()) sb.Append(Line(r)).Append("\r\n");
            return sb.ToString();
        }

        /// <summary>The lines a people.csv's text holds, each read by its columns' names (a file saved again by Excel too); lines without an id passed over.</summary>
        public static List<PersonSeen> Read(string text)
        {
            var list = new List<PersonSeen>();
            if (string.IsNullOrWhiteSpace(text)) return list;
            char delim = PortraitDeleteMath.DelimiterOf(text);
            List<string> head = null;
            foreach (var raw in text.TrimStart('﻿').Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var cells = PortraitDeleteMath.Cells(line, delim);
                if (head == null)
                {
                    head = cells.Select(c => c.Trim()).ToList();
                    continue;
                }
                string Cell(string col)
                {
                    int at = head.FindIndex(h => string.Equals(h, col, StringComparison.OrdinalIgnoreCase));
                    return at >= 0 && at < cells.Count ? cells[at].Trim() : "";
                }
                var r = new PersonSeen
                {
                    id = Cell("id"), name = Cell("name"), fullName = Cell("fullName"), standing = Cell("standing"), picture = Cell("picture"), record = Cell("record"),
                    shown = Cell("shown"), distance = Cell("distance"), words = Cell("words"), lacks = Cell("lacks"), changed = Cell("changed"), age = Cell("age"),
                    looks = Cell("looks"), marks = Cell("marks"), role = Cell("role"), title = Cell("title"), first = Cell("first")
                };
                r.rank = Math.Max(0, Array.IndexOf(Standings, r.standing.ToLowerInvariant()));
                if (r.id.Length > 0) list.Add(r);
            }
            return list;
        }

        /// <summary>A distance as the list writes it (two decimals; "" for none).</summary>
        public static string DistanceWord(float d) => float.IsNaN(d) || float.IsInfinity(d) ? "" : d.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
