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

    /// <summary>One line of missing.csv: what was needed and not there, how often, and when last.</summary>
    public sealed class PortraitNeed
    {
        public string what = "", kind = "", mod = "", sex = "", age = "", skin = "", hair = "", era = "", why = "", instead = "";
        public int count;
        public string lastSeen = "";

        /// <summary>What makes two needs the same line: everything but the mod, what was used instead, the count and the date.</summary>
        public string Key => string.Join("|", what, kind, sex, age, skin, hair, era, why).ToLowerInvariant();
    }

    /// <summary>
    /// The player's own library folder (docs: DESIGN.md, "Portraits, phase 4"; the player, 22 November 2026: the pictures
    /// stay in one folder kept in step between their computers, read by Rimfolk directly), without game types (Tools/Tests
    /// try it):
    /// <list type="bullet">
    /// <item>its <c>records.xml</c> read as the defs are (<see cref="Parse"/>): <c>ColonyCouncil.PortraitDef</c> and
    /// <c>ColonyCouncil.BackdropDef</c> elements under <c>Defs</c>, their <c>texPath</c> from the folder;</item>
    /// <item>what the game needed and the library didn't have, kept in its <c>missing.csv</c> for the next batches
    /// (<see cref="Merge"/>, <see cref="Csv"/>): people with no picture of their kind or none close, rooms with no
    /// backdrop of their kind or their age, land with no backdrop of its own; one line per need, the counts added and the
    /// newest date kept.</item>
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

        public static readonly string[] Columns = { "what", "kind", "mod", "sex", "age", "skin", "hair", "era", "why", "used instead", "how many", "last seen" };

        /// <summary>The file's first lines: what it is, in plain words, then the columns.</summary>
        public static readonly string[] Header =
        {
            "# What Rimfolk needed and the portrait library did not have, for the next batches. One line per need; the counts add up and the newest date is kept each time it is written.",
            "# what: person, room or outdoors. kind: a person's race/xenotype, a room's kind, or the land's biome. how many: people, rooms or maps seen with that need."
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

        /// <summary>The needs a missing.csv's text holds (its header and lines not its own passed over).</summary>
        public static List<PortraitNeed> Read(string text)
        {
            var list = new List<PortraitNeed>();
            if (string.IsNullOrEmpty(text)) return list;
            foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("what,")) continue;
                var c = Cells(line);
                if (c.Count < Columns.Length) continue;
                list.Add(new PortraitNeed
                {
                    what = c[0], kind = c[1], mod = c[2], sex = c[3], age = c[4], skin = c[5], hair = c[6], era = c[7], why = c[8], instead = c[9],
                    count = int.TryParse(c[10], NumberStyles.Integer, CultureInfo.InvariantCulture, out int k) ? k : 0, lastSeen = c[11]
                });
            }
            return list;
        }

        /// <summary>
        /// The file's needs and the new ones together: one line per need, the counts added, the newest date kept, the mod
        /// and what was used instead the newest where given; people first, then rooms, then the land, the commonest first.
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
                    had = new PortraitNeed
                    {
                        what = n.what, kind = n.kind, mod = n.mod, sex = n.sex, age = n.age, skin = n.skin, hair = n.hair, era = n.era, why = n.why, instead = n.instead,
                        count = n.count, lastSeen = n.lastSeen
                    };
                    byKey[n.Key] = had;
                    order.Add(had);
                    return;
                }
                had.count += n.count;
                if (string.CompareOrdinal(n.lastSeen ?? "", had.lastSeen ?? "") >= 0)
                {
                    had.lastSeen = n.lastSeen;
                    if (!string.IsNullOrEmpty(n.mod)) had.mod = n.mod;
                    if (!string.IsNullOrEmpty(n.instead)) had.instead = n.instead;
                }
            }
            foreach (var n in old ?? Enumerable.Empty<PortraitNeed>()) Add(n);
            foreach (var n in fresh ?? Enumerable.Empty<PortraitNeed>()) Add(n);
            int Rank(string what) => what == "person" ? 0 : what == "room" ? 1 : 2;
            return order.OrderBy(n => Rank(n.what)).ThenByDescending(n => n.count).ThenBy(n => n.kind, StringComparer.Ordinal).ThenBy(n => n.Key, StringComparer.Ordinal).ToList();
        }

        /// <summary>The needs as missing.csv's text: the header, the columns, a line each.</summary>
        public static string Csv(IEnumerable<PortraitNeed> needs)
        {
            var sb = new StringBuilder();
            foreach (var h in Header) sb.Append(h).Append('\n');
            sb.Append(string.Join(",", Columns)).Append('\n');
            foreach (var n in needs)
                sb.Append(string.Join(",", new[]
                {
                    n.what, n.kind, n.mod, n.sex, n.age, n.skin, n.hair, n.era, n.why, n.instead, n.count.ToString(CultureInfo.InvariantCulture), n.lastSeen
                }.Select(Quote))).Append('\n');
            return sb.ToString();
        }
    }
}
