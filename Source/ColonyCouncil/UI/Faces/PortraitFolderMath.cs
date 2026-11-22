using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace ColonyCouncil
{
    /// <summary>A picture of a person read from the library folder's records.xml: its record, its path from the folder, and the head's place where the record gives it.</summary>
    public sealed class FolderPortrait
    {
        public PortraitRecord record;
        public string texPath;
        public float headX = float.NaN, headY = float.NaN, headSize = float.NaN;
    }

    /// <summary>A backdrop read from the library folder's records.xml.</summary>
    public sealed class FolderBackdrop
    {
        public BackdropRecord record;
        public string texPath;
    }

    /// <summary>
    /// One line of missing.csv and of the save's list: what was needed and not there (a person, a look, a room, the land),
    /// how often, and when last (the day's date, and in the save the game's day as well).
    /// </summary>
    public sealed class PortraitNeed
    {
        public string what = "", kind = "", look = "", role = "", mod = "", sex = "", age = "", skin = "", hair = "", era = "", why = "", instead = "";
        public int count;
        public string lastSeen = "";
        /// <summary>The game's day it was last seen (days since the game began), for the save's list; -1 where not known.</summary>
        public int day = -1;

        /// <summary>What makes two needs the same line: everything but the mod, what was used instead, the count and the dates.</summary>
        public string Key => string.Join("|", what, kind, look, role, sex, age, skin, hair, era, why).ToLowerInvariant();

        public PortraitNeed Copy() => (PortraitNeed)MemberwiseClone();
    }

    /// <summary>
    /// The player's own library folder (docs: DESIGN.md, "Portraits, phase 4"; the player, 22 November 2026: the pictures
    /// stay in one folder kept in step between their computers, read by Rimfolk directly), without game types (Tools/Tests
    /// try it):
    /// <list type="bullet">
    /// <item>its <c>records.xml</c> read as the defs are (<see cref="Parse"/>): <c>ColonyCouncil.PortraitDef</c> and
    /// <c>ColonyCouncil.BackdropDef</c> elements under <c>Defs</c>, their <c>texPath</c> from the folder;</item>
    /// <item>what the game needed and the library didn't have, kept in its <c>missing.csv</c> for the next batches
    /// (<see cref="Merge"/>, <see cref="Csv"/>): people with no picture of their kind or none close, looks no picture of
    /// their kind shows, rooms with no backdrop of their kind or their age, land with no backdrop of its own; one line per
    /// need, the counts added and the newest date kept; and the same lines kept in the save (<see cref="Line"/>,
    /// <see cref="ReadLines"/>, <see cref="Keep"/>: at most a few hundred, the least recently seen let go).</item>
    /// </list>
    /// </summary>
    public static class PortraitFolderMath
    {
        // ---------------- records.xml ----------------

        /// <summary>
        /// The records in a records.xml's text, read with the same words as the defs; what is wrong with each (an unknown
        /// word, a missing texPath) in <paramref name="problems"/> with its name. Throws only where the text is no XML at all.
        /// </summary>
        public static void Parse(string xml, List<FolderPortrait> people, List<FolderBackdrop> backdrops, List<string> problems)
        {
            var doc = XDocument.Parse(xml);
            var root = doc.Root;
            if (root == null) return;
            var names = new HashSet<string>(StringComparer.Ordinal);
            int n = 0;
            foreach (var e in root.Elements())
            {
                n++;
                string kind = e.Name.LocalName;
                bool person = kind == "ColonyCouncil.PortraitDef", backdrop = kind == "ColonyCouncil.BackdropDef";
                if (!person && !backdrop)
                {
                    problems?.Add("element " + n + ": '" + kind + "' is not ColonyCouncil.PortraitDef or ColonyCouncil.BackdropDef");
                    continue;
                }
                string name = e.Element("defName")?.Value?.Trim() ?? "";
                if (name.Length == 0) name = (person ? "Portrait_" : "Backdrop_") + n;
                if (!names.Add(name))
                {
                    problems?.Add(name + ": a second record of the same name, left out");
                    continue;
                }
                string F(string f) => e.Element(f)?.Value;
                IList<string> L(string f) => e.Element(f)?.Elements("li").Select(x => x.Value).ToList();
                var own = new List<string>();
                string tex = (F("texPath") ?? "").Trim().Replace('\\', '/').TrimStart('/');
                if (person)
                {
                    var p = new FolderPortrait { record = PortraitLibraryMath.Record(name, F, L, own), texPath = tex };
                    p.headX = Number(F("headX"));
                    p.headY = Number(F("headY"));
                    p.headSize = Number(F("headSize"));
                    people?.Add(p);
                }
                else backdrops?.Add(new FolderBackdrop { record = BackdropMath.Record(name, F, L, own), texPath = tex });
                foreach (var o in own) problems?.Add(name + ": " + o);
            }
        }

        private static float Number(string text) =>
            !string.IsNullOrWhiteSpace(text) && float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : float.NaN;

        /// <summary>
        /// The folder as the player typed or pasted it: spaces and quotation marks at its ends taken off (a path copied from
        /// Windows' Explorer comes in quotes); empty for none.
        /// </summary>
        public static string CleanFolder(string text) => (text ?? "").Trim().Trim('"', '\'').Trim();

        /// <summary>A picture's path from the folder, for the folder's listing (forward slashes, ".png" added).</summary>
        public static string FileKey(string texPath) => (texPath ?? "").Replace('\\', '/').TrimStart('/') + ".png";

        // ---------------- Words for missing.csv ----------------

        /// <summary>A skin's colour in a word: very fair, fair, light, medium, tan, brown, dark; a skin no human grows by its hue (green, blue, grey ...).</summary>
        public static string SkinWord(PortraitColour c)
        {
            if (PortraitLibraryMath.UnusualSkin(c)) return HueWord(c);
            PortraitLibraryMath.Lab(c, out double l, out _, out _);
            return l >= 85 ? "very fair" : l >= 75 ? "fair" : l >= 65 ? "light" : l >= 55 ? "medium" : l >= 45 ? "tan" : l >= 33 ? "brown" : "dark";
        }

        /// <summary>A hair's colour in a word: black, brown, light brown, blond, red, grey, white, or its hue where it is dyed.</summary>
        public static string HairWord(PortraitColour c)
        {
            PortraitLibraryMath.Lab(c, out double l, out _, out _);
            PortraitLibraryMath.Hsv(c, out float h, out float s, out float v);
            if (l < 22) return "black";
            if (s < 0.15f) return l > 82 ? "white" : l > 45 ? "grey" : "black";
            if (h >= 150f && h <= 340f && s > 0.25f) return HueWord(c);
            if ((h < 25f || h > 340f) && s > 0.6f) return "red";
            if (l > 68) return "blond";
            return l > 45 ? "light brown" : "brown";
        }

        private static string HueWord(PortraitColour c)
        {
            PortraitLibraryMath.Hsv(c, out float h, out float s, out float v);
            if (s < 0.12f) return v < 0.15f ? "black" : v > 0.85f ? "white" : "grey";
            if (h < 20f || h >= 340f) return "red";
            if (h < 45f) return "orange";
            if (h < 70f) return "yellow";
            if (h < 165f) return "green";
            if (h < 200f) return "teal";
            if (h < 255f) return "blue";
            if (h < 290f) return "purple";
            return "pink";
        }

        // ---------------- missing.csv ----------------

        public static readonly string[] Columns = { "what", "kind", "look", "role", "mod", "sex", "age", "skin", "hair", "era", "why", "used instead", "how many", "last seen" };

        /// <summary>The save's columns: the file's, and the game's day last seen.</summary>
        public static readonly string[] SaveColumns = Columns.Concat(new[] { "day" }).ToArray();

        /// <summary>The file's first lines: what it is, in plain words, then the columns.</summary>
        public static readonly string[] Header =
        {
            "# What Rimfolk needed and the portrait library did not have, for the next batches. One line per need; the counts add up and the newest date is kept each time it is written.",
            "# what: person, look, room or outdoors. kind: a person's race/xenotype, a room's kind, or the land's biome. look: the look no picture of their kind shows (a look line). role: a person's work. how many: people, rooms or maps seen with that need, each once a session."
        };

        private static string Quote(string v)
        {
            v ??= "";
            return v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        }

        /// <summary>One CSV line split into its cells (quotes as Excel writes them).</summary>
        public static List<string> Cells(string line)
        {
            var cells = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (quoted)
                {
                    if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else if (ch == '"') quoted = false;
                    else sb.Append(ch);
                }
                else if (ch == '"') quoted = true;
                else if (ch == ',')
                {
                    cells.Add(sb.ToString());
                    sb.Clear();
                }
                else sb.Append(ch);
            }
            cells.Add(sb.ToString());
            return cells;
        }

        /// <summary>
        /// The needs a missing.csv's text holds, each line read by its file's own columns line (so a file written before
        /// the look and role columns reads too); comment lines and lines not its own passed over.
        /// </summary>
        public static List<PortraitNeed> Read(string text)
        {
            var list = new List<PortraitNeed>();
            if (string.IsNullOrEmpty(text)) return list;
            List<string> head = null;
            foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("what,"))
                {
                    head = Cells(line);
                    continue;
                }
                var n = FromCells(head ?? Columns.ToList(), Cells(line));
                if (n != null) list.Add(n);
            }
            return list;
        }

        /// <summary>A need from a line's cells by the columns' names; null where it has fewer cells than the columns or no 'what'.</summary>
        private static PortraitNeed FromCells(IList<string> head, IList<string> c)
        {
            if (c.Count < head.Count) return null;
            string Cell(string col)
            {
                int at = head.IndexOf(col);
                return at >= 0 && at < c.Count ? c[at] : "";
            }
            var n = new PortraitNeed
            {
                what = Cell("what"), kind = Cell("kind"), look = Cell("look"), role = Cell("role"), mod = Cell("mod"), sex = Cell("sex"), age = Cell("age"), skin = Cell("skin"),
                hair = Cell("hair"), era = Cell("era"), why = Cell("why"), instead = Cell("used instead"), lastSeen = Cell("last seen"),
                count = int.TryParse(Cell("how many"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int k) ? k : 0,
                day = int.TryParse(Cell("day"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int d) ? d : -1
            };
            return n.what.Length == 0 ? null : n;
        }

        /// <summary>One need as a CSV line in the file's columns, or with <paramref name="withDay"/> the save's (the game's day at its end).</summary>
        public static string Line(PortraitNeed n, bool withDay)
        {
            var cells = new List<string>
            {
                n.what, n.kind, n.look, n.role, n.mod, n.sex, n.age, n.skin, n.hair, n.era, n.why, n.instead, n.count.ToString(CultureInfo.InvariantCulture), n.lastSeen
            };
            if (withDay) cells.Add(n.day.ToString(CultureInfo.InvariantCulture));
            return string.Join(",", cells.Select(Quote));
        }

        /// <summary>The save's lines read back by the columns it saved with them (<paramref name="columns"/>, a CSV line; the save's own where empty).</summary>
        public static List<PortraitNeed> ReadLines(string columns, IEnumerable<string> lines)
        {
            var head = string.IsNullOrWhiteSpace(columns) ? SaveColumns.ToList() : Cells(columns.Trim());
            var list = new List<PortraitNeed>();
            if (lines == null) return list;
            foreach (var l in lines)
            {
                if (string.IsNullOrWhiteSpace(l)) continue;
                var n = FromCells(head, Cells(l.Trim()));
                if (n != null) list.Add(n);
            }
            return list;
        }

        /// <summary>The save's columns as one CSV line, saved with the lines so they read back by name.</summary>
        public static string SaveColumnsLine => string.Join(",", SaveColumns.Select(Quote));

        /// <summary>
        /// At most <paramref name="most"/> needs (the save's list): the most recently seen kept (the game's day, then the
        /// date, then the most often seen), the rest let go; in the list's own order (<see cref="Order"/>).
        /// </summary>
        public static List<PortraitNeed> Keep(IEnumerable<PortraitNeed> needs, int most)
        {
            var list = (needs ?? Enumerable.Empty<PortraitNeed>()).Where(n => n != null).ToList();
            if (list.Count > most)
                list = list.OrderByDescending(n => n.day).ThenByDescending(n => n.lastSeen ?? "", StringComparer.Ordinal).ThenByDescending(n => n.count)
                    .ThenBy(n => n.Key, StringComparer.Ordinal).Take(Math.Max(0, most)).ToList();
            return Order(list);
        }

        /// <summary>The list's order: people, then looks, then rooms, then the land; the commonest first.</summary>
        public static List<PortraitNeed> Order(IEnumerable<PortraitNeed> needs)
        {
            int Rank(string what) => what == "person" ? 0 : what == "look" ? 1 : what == "room" ? 2 : 3;
            return needs.OrderBy(n => Rank(n.what)).ThenByDescending(n => n.count).ThenBy(n => n.kind, StringComparer.Ordinal).ThenBy(n => n.Key, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// The file's needs and the new ones together: one line per need, the counts added, the newest date and game day
        /// kept, the mod and what was used instead the newest where given; people first, then looks, rooms and the land, the
        /// commonest first. Nothing is lost: every line of either is in the result.
        /// </summary>
        public static List<PortraitNeed> Merge(IEnumerable<PortraitNeed> old, IEnumerable<PortraitNeed> fresh)
        {
            var byKey = new Dictionary<string, PortraitNeed>();
            var order = new List<PortraitNeed>();
            void Add(PortraitNeed n)
            {
                if (n == null) return;
                if (!byKey.TryGetValue(n.Key, out var had))
                {
                    had = n.Copy();
                    byKey[n.Key] = had;
                    order.Add(had);
                    return;
                }
                had.count += n.count;
                if (n.day > had.day) had.day = n.day;
                if (string.CompareOrdinal(n.lastSeen ?? "", had.lastSeen ?? "") >= 0)
                {
                    had.lastSeen = n.lastSeen;
                    if (!string.IsNullOrEmpty(n.mod)) had.mod = n.mod;
                    if (!string.IsNullOrEmpty(n.instead)) had.instead = n.instead;
                }
            }
            foreach (var n in old ?? Enumerable.Empty<PortraitNeed>()) Add(n);
            foreach (var n in fresh ?? Enumerable.Empty<PortraitNeed>()) Add(n);
            return Order(order);
        }

        /// <summary>The needs as missing.csv's text: the header, the columns, a line each (with <paramref name="withDay"/> the save's columns, for the log).</summary>
        public static string Csv(IEnumerable<PortraitNeed> needs, bool withDay = false)
        {
            var sb = new StringBuilder();
            foreach (var h in Header) sb.Append(h).Append('\n');
            sb.Append(string.Join(",", withDay ? SaveColumns : Columns)).Append('\n');
            foreach (var n in needs) sb.Append(Line(n, withDay)).Append('\n');
            return sb.ToString();
        }
    }
}
