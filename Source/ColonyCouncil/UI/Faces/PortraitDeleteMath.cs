using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ColonyCouncil
{
    /// <summary>What happens to a picture's files when it is deleted for good in the game: moved to the bin next to the library folder (the default), or deleted.</summary>
    public enum PortraitDeleteMode
    {
        Bin,
        Delete
    }

    /// <summary>
    /// One line of the library folder's <c>deleted.csv</c>: a picture deleted for good, which the portrait scripts never
    /// copy, record or check again. Its file name in the library (with .png), its batch's folder, its batch row's name, its
    /// record's name, when, how, and the SHA-256 of its file ("" where not known), so a " (2)" copy of the same picture is
    /// held back too and a different picture that only shares the start of its name is not.
    /// </summary>
    public sealed class DeletedPicture
    {
        public string file = "", batch = "", row = "", record = "", date = "", how = "", sha256 = "";
    }

    /// <summary>A picture or backdrop to delete for good, as read on the game's thread: everything the worker needs.</summary>
    public sealed class DeleteJob
    {
        /// <summary>The library folder (its records.xml and deleted.csv).</summary>
        public string folder = "";
        /// <summary>The bin's folder for this picture (<c>&lt;the library folder's parent&gt;\deleted pictures\&lt;batch&gt;</c>), or null to delete the files.</summary>
        public string bin;
        /// <summary>The records to take out of records.xml: the picture's and its expressions'.</summary>
        public List<string> records = new List<string>();
        /// <summary>The picture's path from the folder, without .png.</summary>
        public string texPath = "";
        public bool backdrop;
        /// <summary>A picture of an animal or creature (UI/Faces/CreatureLibrary.cs).</summary>
        public bool creature;
        /// <summary>A version of a picture (an expression or a mark variant), deleted on its own; the picture stays.</summary>
        public bool variant;
        /// <summary>Mark variants go with it (its other versions are told as versions, not only expressions).</summary>
        public bool marked;
        /// <summary>Other files its records name (its expressions' pictures, a backdrop's depth map), paths from the folder without .png.</summary>
        public List<string> named = new List<string>();
        /// <summary>The files every other record uses (paths from the folder without .png): never touched.</summary>
        public List<string> keep = new List<string>();
        /// <summary>Its line for deleted.csv (the SHA-256 is filled in by the worker).</summary>
        public DeletedPicture line = new DeletedPicture();
        public int tries = PortraitDeleteMath.Tries;
    }

    /// <summary>The step a deletion stopped at, with nothing changed.</summary>
    public enum DeleteStop
    {
        None,
        NoRecords,
        ReadRecords,
        WriteList,
        WriteRecords
    }

    /// <summary>What came of a deletion.</summary>
    public sealed class DeleteResult
    {
        /// <summary>records.xml and deleted.csv were written; otherwise nothing was changed (<see cref="stop"/> says where it stopped, <see cref="error"/> why).</summary>
        public bool done;
        public DeleteStop stop;
        public string error = "";
        /// <summary>How many records were taken out of records.xml (none where it no longer had them).</summary>
        public int recordsTaken;
        /// <summary>The files moved to the bin or deleted (paths from the folder), and where each went in the bin.</summary>
        public List<string> files = new List<string>(), movedTo = new List<string>();
        /// <summary>The files that could not be moved or deleted (paths from the folder), and the last error.</summary>
        public List<string> failed = new List<string>();
        public string failedError = "";
    }

    /// <summary>
    /// Deleting a painted picture for good from the game (docs: DESIGN.md, "Portraits: deleting a picture for good"; the
    /// player, 22 November 2026: "when i really don't like an image, that i can say it's fully deleted on the record and it
    /// actually deletes the image and any reference to it ... this way i could cycle out all the basic ai faces"), without
    /// game types (Tools/Tests try it on a folder of its own):
    /// <list type="bullet">
    /// <item>its records taken out of records.xml's text (<see cref="WithoutRecords"/>), the rest of the file as it was;</item>
    /// <item>which files are its own (<see cref="FilesOf"/>): its picture and its expressions (<c>&lt;stem&gt;_v-*.png</c>), or
    /// a backdrop and its depth map (<c>&lt;stem&gt;_depth.png</c>), never a file another record uses;</item>
    /// <item>their names in the bin (<see cref="BinNames"/>): their own, or all with the same " (2)" where a name is taken;</item>
    /// <item>its line in deleted.csv (<see cref="Append"/>, <see cref="Read"/>), which the portrait scripts honour;</item>
    /// <item>the whole deletion (<see cref="Run"/>), on a worker thread: the list and the records written safely (to a
    /// temporary file, then replaced), each step tried again a few times (a cloud drive may hold a file a moment), and on
    /// an error nothing changed; then the files moved or deleted.</item>
    /// </list>
    /// </summary>
    public static class PortraitDeleteMath
    {
        /// <summary>The bin's folder, next to the library folder, and the list in the library folder.</summary>
        public const string BinFolder = "deleted pictures", ListFile = "deleted.csv", RecordsFile = "records.xml";

        /// <summary>How many times a step is tried before the deletion gives up (a cloud drive may hold a file a moment).</summary>
        public const int Tries = 5;

        /// <summary>deleted.csv's columns, in the order written.</summary>
        public static readonly string[] Columns = { "file", "batch", "row", "record", "date", "how", "sha256" };

        // ---------------- records.xml ----------------

        /// <summary>
        /// records.xml's text without the records named (<paramref name="names"/>, as their defName, or for a record without
        /// one the name the game gives it by its place), each with the line it stood on; everything else as it was (the
        /// declaration, the comment, the other records, the line endings). The names taken out are added to
        /// <paramref name="removed"/>. Throws where the text is no XML at all.
        /// </summary>
        public static string WithoutRecords(string xml, ICollection<string> names, List<string> removed)
        {
            var doc = XDocument.Parse(xml ?? "", LoadOptions.PreserveWhitespace);
            var root = doc.Root;
            if (root == null || names == null || names.Count == 0) return xml;
            var gone = new List<XElement>();
            int n = 0;
            foreach (var e in root.Elements())
            {
                n++;
                string kind = e.Name.LocalName;
                bool person = kind == "ColonyCouncil.PortraitDef", backdrop = kind == "ColonyCouncil.BackdropDef", creature = kind == CreaturePortraitMath.Element;
                if (!person && !backdrop && !creature) continue;
                // The same name the game reads (PortraitFolderMath.Parse).
                string name = e.Element("defName")?.Value?.Trim() ?? "";
                if (name.Length == 0) name = (person ? "Portrait_" : creature ? "Creature_" : "Backdrop_") + n;
                if (!names.Contains(name)) continue;
                gone.Add(e);
                removed?.Add(name);
            }
            if (gone.Count == 0) return xml;
            foreach (var e in gone)
            {
                if (e.PreviousNode is XText t && string.IsNullOrWhiteSpace(t.Value)) t.Remove();
                e.Remove();
            }
            return Written(doc, xml);
        }

        /// <summary>
        /// A records.xml read with its whitespace kept and changed, as text again the way it was written: the declaration,
        /// the comment and the records each on their line, the file's own line endings (<paramref name="xml"/>: its text before).
        /// </summary>
        public static string Written(XDocument doc, string xml)
        {
            var sb = new StringBuilder();
            if (doc.Declaration != null) sb.Append(doc.Declaration);
            foreach (var node in doc.Nodes())
            {
                if (sb.Length > 0 && !(node is XText)) sb.Append('\n');
                sb.Append(node is XText x ? x.Value.Trim('\r', '\n') : node.ToString(SaveOptions.DisableFormatting));
            }
            sb.Append('\n');
            string text = sb.ToString();
            // XML reads every line ending as one; the file's own are put back.
            return xml.Contains("\r\n") ? text.Replace("\r\n", "\n").Replace("\n", "\r\n") : text;
        }

        /// <summary>
        /// The records of a picture: its own, and its versions' (the records whose <c>variantOf</c> names it: its expressions
        /// and mark variants), and theirs in turn (a mark variant's own expressions).
        /// </summary>
        public static List<string> RecordsOf(string picture, IEnumerable<KeyValuePair<string, string>> nameAndVariantOf)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(picture)) return list;
            list.Add(picture);
            if (nameAndVariantOf == null) return list;
            var pairs = nameAndVariantOf.ToList();
            for (int at = 0; at < list.Count; at++)
                foreach (var kv in pairs)
                    if (kv.Value == list[at] && !string.IsNullOrEmpty(kv.Key) && !list.Contains(kv.Key))
                        list.Add(kv.Key);
            return list;
        }

        // ---------------- Its files ----------------

        /// <summary>The folder part of a picture's path from the library folder, its batch's folder ("colonists" for "colonists/Avila_..."); "" for none.</summary>
        public static string BatchOf(string texPath)
        {
            string t = (texPath ?? "").Replace('\\', '/').Trim('/');
            int at = t.LastIndexOf('/');
            return at > 0 ? t.Substring(0, at) : "";
        }

        /// <summary>The file name part of a picture's path, without .png.</summary>
        public static string StemOf(string texPath)
        {
            string t = (texPath ?? "").Replace('\\', '/').Trim('/');
            int at = t.LastIndexOf('/');
            return at >= 0 ? t.Substring(at + 1) : t;
        }

        /// <summary>A " (2)" (the copier's or Windows' copy of a name) taken off the end of a file's name without .png.</summary>
        public static string StripCopy(string stem) => Regex.Replace(stem ?? "", @"\s*\(\d+\)$", "");

        /// <summary>
        /// The batch row's name in a picture's name, as the library script reads it (<c>&lt;row&gt;_style..._...</c>, a " (2)"
        /// copy's too: "Avila" for "Avila_style_261122-143005_01_cut_00001_"); "" where the name has no style part.
        /// </summary>
        public static string RowOf(string stem)
        {
            var m = Regex.Match(StripCopy(stem ?? ""), @"^(?<name>.+?)_style[^_]*_");
            return m.Success ? m.Groups["name"].Value : "";
        }

        /// <summary>
        /// The files of a picture in the library folder (paths from the folder with forward slashes, spelled as
        /// <paramref name="listing"/> has them; only those there): its own; the files its other records name
        /// (<paramref name="named"/>: its expressions', a backdrop's depth map); and beside it by name, for a person its
        /// expressions and mark variants (<c>&lt;stem&gt;_v-&lt;word&gt;.png</c>, <c>-2</c> and so on, and a variant's own
        /// <c>_v-</c> expressions), for a backdrop its depth map (<c>&lt;stem&gt;_depth.png</c>).
        /// A file any other record uses (<paramref name="keep"/>) is never one of them. The picture's own first. Letters'
        /// case aside, as on Windows.
        /// </summary>
        public static List<string> FilesOf(string texPath, bool backdrop, IEnumerable<string> named, IEnumerable<string> keep, IEnumerable<string> listing)
        {
            var have = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in listing ?? Enumerable.Empty<string>())
            {
                string k = (f ?? "").Replace('\\', '/').TrimStart('/');
                if (k.Length > 0 && !have.ContainsKey(k)) have[k] = k;
            }
            var kept = new HashSet<string>((keep ?? Enumerable.Empty<string>()).Select(PortraitFolderMath.FileKey), StringComparer.OrdinalIgnoreCase);
            var list = new List<string>();
            void Add(string key)
            {
                if (have.TryGetValue(key, out string real) && !kept.Contains(real) && !list.Contains(real, StringComparer.OrdinalIgnoreCase)) list.Add(real);
            }
            Add(PortraitFolderMath.FileKey(texPath));
            foreach (var t in named ?? Enumerable.Empty<string>())
                if (!string.IsNullOrWhiteSpace(t))
                    Add(PortraitFolderMath.FileKey(t));
            string dir = BatchOf(texPath), stem = StemOf(texPath);
            if (stem.Length == 0) return list;
            // A person's versions: its expressions and mark variants (a variant made again after one was deleted, "-2"), and a mark variant's own expressions.
            var beside = new Regex("^" + Regex.Escape(stem) + (backdrop ? "_depth" : "_v-[A-Za-z]+(?:-[0-9]+)?(?:_v-[A-Za-z]+(?:-[0-9]+)?)?") + @"\.png$", RegexOptions.IgnoreCase);
            foreach (var k in have.Values.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                if (string.Equals(BatchOf(k), dir, StringComparison.OrdinalIgnoreCase) && beside.IsMatch(StemOf(k)))
                    Add(k);
            return list;
        }

        // ---------------- The bin ----------------

        /// <summary>
        /// The bin for a library folder: <c>deleted pictures</c> beside it (in its parent folder), never inside it, where the
        /// scripts would read it as a batch; null where the folder has no parent (a drive's root).
        /// </summary>
        public static string BinRoot(string folder)
        {
            string f = (folder ?? "").Trim().TrimEnd('\\', '/');
            if (f.Length == 0) return null;
            string parent;
            try
            {
                parent = Path.GetDirectoryName(f);
            }
            catch (ArgumentException)
            {
                return null;
            }
            return string.IsNullOrEmpty(parent) ? null : Path.Combine(parent, BinFolder);
        }

        /// <summary>
        /// The names a picture's files take in the bin's folder (<paramref name="names"/>: file names), in the same order:
        /// their own where none is taken there (<paramref name="taken"/>), else all with the same " (2)", " (3)" ... after the
        /// picture's name (<paramref name="stem"/>), so a picture and its expressions keep going together
        /// ("X_v-blink.png" becomes "X (2)_v-blink.png"); a name that doesn't start with the picture's takes it before ".png".
        /// </summary>
        public static List<string> BinNames(string stem, IList<string> names, Func<string, bool> taken)
        {
            var list = new List<string>();
            if (names == null || names.Count == 0) return list;
            for (int k = 1; k < 10000; k++)
            {
                list = names.Select(n => k == 1 ? n : Numbered(n, stem, k)).ToList();
                if (list.Distinct(StringComparer.OrdinalIgnoreCase).Count() == list.Count && (taken == null || !list.Any(taken))) return list;
            }
            return list;
        }

        private static string Numbered(string name, string stem, int k)
        {
            string suffix = " (" + k.ToString(CultureInfo.InvariantCulture) + ")";
            if (!string.IsNullOrEmpty(stem) && name.StartsWith(stem, StringComparison.OrdinalIgnoreCase)) return name.Substring(0, stem.Length) + suffix + name.Substring(stem.Length);
            string ext = Path.GetExtension(name);
            return Path.GetFileNameWithoutExtension(name) + suffix + ext;
        }

        // ---------------- deleted.csv ----------------

        /// <summary>A CSV cell: in quotes (its own doubled) where it holds the separator, a quote or a line break.</summary>
        public static string Quote(string v, char delim)
        {
            v ??= "";
            return v.IndexOfAny(new[] { delim, '"', '\n', '\r' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        }

        /// <summary>One line of deleted.csv, its cells split by <paramref name="delim"/> (a comma, or Excel's semicolon in some countries).</summary>
        public static string Line(DeletedPicture d, char delim = ',') =>
            string.Join(delim.ToString(), new[] { d.file, d.batch, d.row, d.record, d.date, d.how, d.sha256 }.Select(v => Quote(v, delim)));

        /// <summary>The cells of one CSV line split by <paramref name="delim"/> (quotes as Excel writes them).</summary>
        public static List<string> Cells(string line, char delim)
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
                else if (ch == delim)
                {
                    cells.Add(sb.ToString());
                    sb.Clear();
                }
                else sb.Append(ch);
            }
            cells.Add(sb.ToString());
            return cells;
        }

        /// <summary>The file's cells' separator by its first line: a semicolon where it has semicolons and no commas (Excel in some countries), else a comma.</summary>
        public static char DelimiterOf(string text)
        {
            string first = FirstLine(text);
            return first.IndexOf(';') >= 0 && first.IndexOf(',') < 0 ? ';' : ',';
        }

        private static string FirstLine(string text)
        {
            foreach (var raw in (text ?? "").TrimStart('﻿').Replace("\r\n", "\n").Split('\n'))
                if (raw.Trim().Length > 0)
                    return raw.Trim();
            return "";
        }

        /// <summary>
        /// deleted.csv's text with one more line: a new file's columns and the line; else the line added at its end with its
        /// own separator and line endings, everything already in it kept as it was (lines the player added or changed in Excel too).
        /// </summary>
        public static string Append(string text, DeletedPicture d)
        {
            if (string.IsNullOrWhiteSpace((text ?? "").TrimStart('﻿'))) return string.Join(",", Columns) + "\r\n" + Line(d) + "\r\n";
            string t = text.TrimStart('﻿');
            string nl = t.Contains("\r\n") || !t.Contains("\n") ? "\r\n" : "\n";
            if (!t.EndsWith("\n")) t += nl;
            return t + Line(d, DelimiterOf(t)) + nl;
        }

        /// <summary>The pictures deleted.csv's text lists, each read by its columns' names (a file the player saved again in Excel too); lines without a file passed over.</summary>
        public static List<DeletedPicture> Read(string text)
        {
            var list = new List<DeletedPicture>();
            if (string.IsNullOrWhiteSpace(text)) return list;
            char delim = DelimiterOf(text);
            List<string> head = null;
            foreach (var raw in text.TrimStart('﻿').Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                var cells = Cells(line, delim);
                if (head == null)
                {
                    head = cells.Select(c => c.Trim().ToLowerInvariant()).ToList();
                    continue;
                }
                string Cell(string col)
                {
                    int at = head.IndexOf(col);
                    return at >= 0 && at < cells.Count ? cells[at].Trim() : "";
                }
                var d = new DeletedPicture
                {
                    file = Cell("file"), batch = Cell("batch"), row = Cell("row"), record = Cell("record"), date = Cell("date"), how = Cell("how"), sha256 = Cell("sha256")
                };
                if (d.file.Length > 0) list.Add(d);
            }
            return list;
        }

        /// <summary>The SHA-256 of a file's bytes in capitals, as PowerShell's Get-FileHash gives it.</summary>
        public static string Sha256(byte[] bytes)
        {
            if (bytes == null) return "";
            using (var sha = SHA256.Create())
            {
                var h = sha.ComputeHash(bytes);
                var sb = new StringBuilder(h.Length * 2);
                foreach (byte b in h) sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        // ---------------- Trying again ----------------

        /// <summary>How long to wait after the <paramref name="failed"/>th failure, in milliseconds: 0.3, 0.6, 1.2, 2.4 seconds.</summary>
        public static int WaitMs(int failed) => 300 << Math.Max(0, Math.Min(failed, 4));

        /// <summary>
        /// Runs <paramref name="act"/> up to <paramref name="tries"/> times, waiting a little longer after each failure (a
        /// cloud drive may hold a file a moment); true once it runs through, else false with the last error.
        /// </summary>
        public static bool Attempt(Action act, int tries, Action<int> wait, out Exception last)
        {
            last = null;
            int most = Math.Max(1, tries);
            for (int i = 0; i < most; i++)
            {
                try
                {
                    act();
                    last = null;
                    return true;
                }
                catch (Exception e)
                {
                    last = e;
                    if (i + 1 < most) wait?.Invoke(WaitMs(i));
                }
            }
            return false;
        }

        /// <summary>
        /// Writes a file safely: the text into a temporary file beside it, which then replaces it (the file is never half
        /// written; a cloud drive sees one change); where the drive can't replace a file, it is copied over it. A new file
        /// is the temporary one moved into place.
        /// </summary>
        public static void WriteSafely(string path, string text, bool bom)
        {
            var enc = new UTF8Encoding(bom);
            var body = enc.GetBytes(text ?? "");
            var pre = enc.GetPreamble();
            WriteSafely(path, pre.Length > 0 ? pre.Concat(body).ToArray() : body);
        }

        /// <summary><see cref="WriteSafely(string, string, bool)"/> for bytes as they are (a file put back as it was).</summary>
        public static void WriteSafely(string path, byte[] bytes)
        {
            string tmp = path + ".tmp";
            File.WriteAllBytes(tmp, bytes ?? new byte[0]);
            try
            {
                if (!File.Exists(path))
                {
                    File.Move(tmp, path);
                    return;
                }
                try
                {
                    File.Replace(tmp, path, null);
                }
                catch (Exception)
                {
                    File.Copy(tmp, path, true);
                    File.Delete(tmp);
                }
            }
            catch (Exception)
            {
                // Tried again from the start: the temporary file is written anew.
                try
                {
                    if (File.Exists(tmp)) File.Delete(tmp);
                }
                catch (Exception)
                {
                    // Left for the next try to write over.
                }
                throw;
            }
        }

        // ---------------- The deletion ----------------

        /// <summary>A file's bytes as text (UTF-8, its mark at the start taken off), or null for none.</summary>
        public static string TextOf(byte[] bytes)
        {
            if (bytes == null) return null;
            using (var r = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, true)) return r.ReadToEnd();
        }

        /// <summary>
        /// The whole deletion, on a worker thread (<paramref name="wait"/> sleeps it between tries): records.xml read and its
        /// records taken out; the picture's files found and its SHA-256 read; its line added to deleted.csv, then records.xml
        /// written, each safely and tried again a few times. Where either can't be written, nothing is changed (a list
        /// written before records.xml failed is put back). Then its files moved into the bin (<see cref="DeleteJob.bin"/>,
        /// made where needed, a name there already taken given a " (2)") or deleted; a file that can't be is left where it
        /// is and named in the result (its picture is out of the records and listed, so nothing uses it).
        /// </summary>
        public static DeleteResult Run(DeleteJob job, Action<int> wait)
        {
            var r = new DeleteResult();
            string recordsPath = Path.Combine(job.folder, RecordsFile), listPath = Path.Combine(job.folder, ListFile);
            string xml = null;
            if (!File.Exists(recordsPath))
            {
                r.stop = DeleteStop.NoRecords;
                return r;
            }
            if (!Attempt(() => xml = File.ReadAllText(recordsPath), job.tries, wait, out var readErr))
            {
                r.stop = DeleteStop.ReadRecords;
                r.error = readErr?.Message ?? "";
                return r;
            }
            var removed = new List<string>();
            string newXml;
            try
            {
                newXml = WithoutRecords(xml, new HashSet<string>(job.records, StringComparer.Ordinal), removed);
            }
            catch (Exception e)
            {
                r.stop = DeleteStop.ReadRecords;
                r.error = e.Message;
                return r;
            }
            r.recordsTaken = removed.Count;

            // Its files: its folder's pictures, and any file its records name elsewhere.
            var listing = new List<string>();
            string dir = BatchOf(job.texPath);
            try
            {
                string full = Path.Combine(job.folder, dir.Replace('/', Path.DirectorySeparatorChar));
                if (Directory.Exists(full))
                    foreach (var f in Directory.GetFiles(full, "*.png"))
                        listing.Add((dir.Length > 0 ? dir + "/" : "") + Path.GetFileName(f));
                foreach (var t in job.named)
                {
                    string key = PortraitFolderMath.FileKey(t);
                    if (File.Exists(Path.Combine(job.folder, key.Replace('/', Path.DirectorySeparatorChar)))) listing.Add(key);
                }
            }
            catch (Exception)
            {
                // A folder that can't be listed: only the records and the list are changed; its files stay.
            }
            var files = FilesOf(job.texPath, job.backdrop, job.named, job.keep, listing);
            string own = PortraitFolderMath.FileKey(job.texPath);
            if (files.Count > 0 && string.Equals(files[0], own, StringComparison.OrdinalIgnoreCase))
                try
                {
                    job.line.sha256 = Sha256(File.ReadAllBytes(Path.Combine(job.folder, files[0].Replace('/', Path.DirectorySeparatorChar))));
                }
                catch (Exception)
                {
                    job.line.sha256 = "";
                }

            // The list first: if it can't be written, nothing has changed.
            byte[] oldList = null;
            if (!Attempt(() => oldList = File.Exists(listPath) ? File.ReadAllBytes(listPath) : null, job.tries, wait, out var listReadErr)
                || !Attempt(() => WriteSafely(listPath, Append(TextOf(oldList), job.line), true), job.tries, wait, out listReadErr))
            {
                r.stop = DeleteStop.WriteList;
                r.error = listReadErr?.Message ?? "";
                return r;
            }
            // Then the records; if they can't be written, the list is put back as it was.
            if (removed.Count > 0 && !Attempt(() => WriteSafely(recordsPath, newXml, false), job.tries, wait, out var writeErr))
            {
                Attempt(() =>
                {
                    if (oldList == null) File.Delete(listPath);
                    else WriteSafely(listPath, oldList);
                }, job.tries, wait, out _);
                r.stop = DeleteStop.WriteRecords;
                r.error = writeErr?.Message ?? "";
                return r;
            }
            r.done = true;

            // The files, into the bin or deleted.
            string Full(string key) => Path.Combine(job.folder, key.Replace('/', Path.DirectorySeparatorChar));
            List<string> to = null;
            if (job.bin != null && files.Count > 0)
            {
                if (Attempt(() => Directory.CreateDirectory(job.bin), job.tries, wait, out var dirErr))
                    to = BinNames(StemOf(job.texPath), files.Select(f => f.Substring(f.LastIndexOf('/') + 1)).ToList(), n => File.Exists(Path.Combine(job.bin, n)));
                else
                {
                    r.failed.AddRange(files);
                    r.failedError = dirErr?.Message ?? "";
                    return r;
                }
            }
            for (int i = 0; i < files.Count; i++)
            {
                string from = Full(files[i]);
                string dest = to != null ? Path.Combine(job.bin, to[i]) : null;
                bool ok = Attempt(() =>
                {
                    if (!File.Exists(from)) return;
                    if (dest != null) File.Move(from, dest);
                    else File.Delete(from);
                }, job.tries, wait, out var fileErr);
                if (ok)
                {
                    r.files.Add(files[i]);
                    r.movedTo.Add(to != null ? to[i] : "");
                }
                else
                {
                    r.failed.Add(files[i]);
                    r.failedError = fileErr?.Message ?? "";
                }
            }
            return r;
        }
    }
}
