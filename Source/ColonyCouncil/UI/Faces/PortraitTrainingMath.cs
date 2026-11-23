using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>One line of training.csv: a picture kept for training, its file's name in the training folder (with .png), its batch's folder, its record's name, and when.</summary>
    public sealed class TrainingPicture
    {
        public string file = "", batch = "", record = "", date = "";
    }

    /// <summary>A picture to keep for training, as read on the game's thread: everything the worker needs.</summary>
    public sealed class TrainingJob
    {
        /// <summary>The library folder the picture is in.</summary>
        public string folder = "";
        /// <summary>The training folder beside the library folder (<c>&lt;the library folder's parent&gt;\training</c>).</summary>
        public string root = "";
        /// <summary>The picture's path from the library folder, without .png.</summary>
        public string texPath = "";
        /// <summary>Its line for training.csv (the file's name in the training folder is filled in by the worker).</summary>
        public TrainingPicture line = new TrainingPicture();
        public int tries = PortraitDeleteMath.Tries;
    }

    /// <summary>The step keeping a picture for training stopped at, with nothing changed.</summary>
    public enum TrainingStop
    {
        None,
        NoFile,
        ReadList,
        Copy,
        WriteList
    }

    /// <summary>What came of keeping a picture for training.</summary>
    public sealed class TrainingResult
    {
        /// <summary>Copied and listed in training.csv.</summary>
        public bool done;
        /// <summary>training.csv already lists it: nothing done.</summary>
        public bool already;
        public TrainingStop stop;
        public string error = "";
        /// <summary>The file's name in the training folder (its own, or with " (2)" where another file has that name).</summary>
        public string copiedAs = "";
    }

    /// <summary>
    /// Keeping a painted picture for training (docs: DESIGN.md, "Portraits: the same age and skin, a bionic arm, and keeping
    /// a picture for training"; the player, 23 November 2026: "maybe I can right click and then already flag for training
    /// ... or it's get copied to a special location from which I can take the image to further train the model later"),
    /// without game types (Tools/Tests try it on a folder of its own):
    /// <list type="bullet">
    /// <item>the training folder beside the library folder (<see cref="Root"/>), never inside it, where the scripts would
    /// read it as a batch;</item>
    /// <item>its list, training.csv (<see cref="Append"/>, <see cref="Read"/>, <see cref="Listed"/>), which the training
    /// script reads;</item>
    /// <item>the whole copy (<see cref="Run"/>), on a worker thread: the picture copied as it is into the training folder's
    /// folder of its batch, then its line added to the list, each tried again a few times; on an error nothing changed.</item>
    /// </list>
    /// A picture deleted for good later stays in the training folder.
    /// </summary>
    public static class PortraitTrainingMath
    {
        /// <summary>The training folder beside the library folder, and its list.</summary>
        public const string Folder = "training", ListFile = "training.csv";

        /// <summary>training.csv's columns, in the order written.</summary>
        public static readonly string[] Columns = { "file", "batch", "record", "date" };

        /// <summary>
        /// The training folder for a library folder: <c>training</c> beside it (in its parent folder, as the bin of deleted
        /// pictures); null where the folder has no parent (a drive's root).
        /// </summary>
        public static string Root(string folder)
        {
            string bin = PortraitDeleteMath.BinRoot(folder);
            return bin == null ? null : Path.Combine(Path.GetDirectoryName(bin) ?? "", Folder);
        }

        /// <summary>Where a picture of this batch goes in the training folder (its batch's folder, or the training folder itself for none).</summary>
        public static string FolderOf(string root, string batch) =>
            string.IsNullOrEmpty(batch) ? root : Path.Combine(root, batch.Replace('/', Path.DirectorySeparatorChar));

        // ---------------- training.csv ----------------

        /// <summary>One line of training.csv, its cells split by <paramref name="delim"/> (a comma, or Excel's semicolon in some countries).</summary>
        public static string Line(TrainingPicture t, char delim = ',') =>
            string.Join(delim.ToString(), new[] { t.file, t.batch, t.record, t.date }.Select(v => PortraitDeleteMath.Quote(v, delim)));

        /// <summary>
        /// training.csv's text with one more line: a new file's columns and the line; else the line added at its end with its
        /// own separator and line endings, everything already in it kept as it was.
        /// </summary>
        public static string Append(string text, TrainingPicture t)
        {
            if (string.IsNullOrWhiteSpace((text ?? "").TrimStart('﻿'))) return string.Join(",", Columns) + "\r\n" + Line(t) + "\r\n";
            string s = text.TrimStart('﻿');
            string nl = s.Contains("\r\n") || !s.Contains("\n") ? "\r\n" : "\n";
            if (!s.EndsWith("\n")) s += nl;
            return s + Line(t, PortraitDeleteMath.DelimiterOf(s)) + nl;
        }

        /// <summary>The pictures training.csv's text lists, each read by its columns' names (a file saved again in Excel too); lines without a file passed over.</summary>
        public static List<TrainingPicture> Read(string text)
        {
            var list = new List<TrainingPicture>();
            if (string.IsNullOrWhiteSpace(text)) return list;
            char delim = PortraitDeleteMath.DelimiterOf(text);
            List<string> head = null;
            foreach (var raw in text.TrimStart('﻿').Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                var cells = PortraitDeleteMath.Cells(line, delim);
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
                var t = new TrainingPicture { file = Cell("file"), batch = Cell("batch"), record = Cell("record"), date = Cell("date") };
                if (t.file.Length > 0) list.Add(t);
            }
            return list;
        }

        /// <summary>
        /// Whether the list already has this picture: a line of its record (where both name one), or of its batch and file
        /// name (a line typed by hand without a record). Letters' case aside in the batch and the file, as on Windows.
        /// </summary>
        public static bool Listed(IEnumerable<TrainingPicture> list, string record, string batch, string file)
        {
            if (list == null) return false;
            string b = (batch ?? "").Replace('\\', '/').Trim('/');
            foreach (var t in list)
            {
                if (!string.IsNullOrEmpty(record) && t.record.Length > 0)
                {
                    if (t.record == record) return true;
                    continue;
                }
                if (string.Equals(t.file, file, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(t.batch.Replace('\\', '/').Trim('/'), b, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // ---------------- The copy ----------------

        /// <summary>
        /// Keeping a picture for training, on a worker thread (<paramref name="wait"/> sleeps it between tries): the list
        /// read (a picture it already lists is left as it is); the picture copied as it is into the training folder's folder
        /// of its batch (made where needed; a different file of the same name there kept, the copy named " (2)"; the same
        /// file there already used as it is), through a temporary file, so a half copy is never left under its name; then its
        /// line added to training.csv, written safely. Each step is tried again a few times (a cloud drive may hold a file a
        /// moment); where the list can't be written, the copy made is taken away again, so nothing has changed.
        /// </summary>
        public static TrainingResult Run(TrainingJob job, Action<int> wait)
        {
            var r = new TrainingResult();
            string from = Path.Combine(job.folder, PortraitFolderMath.FileKey(job.texPath).Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(from))
            {
                r.stop = TrainingStop.NoFile;
                return r;
            }
            string listPath = Path.Combine(job.root, ListFile);
            string old = null;
            if (!PortraitDeleteMath.Attempt(() => old = File.Exists(listPath) ? PortraitDeleteMath.TextOf(File.ReadAllBytes(listPath)) : null, job.tries, wait, out var readErr))
            {
                r.stop = TrainingStop.ReadList;
                r.error = readErr?.Message ?? "";
                return r;
            }
            string stem = PortraitDeleteMath.StemOf(job.texPath), own = stem + ".png";
            if (Listed(Read(old), job.line.record, job.line.batch, own))
            {
                r.already = true;
                return r;
            }

            // The copy, as it is, through a temporary file.
            string dir = FolderOf(job.root, job.line.batch);
            string name = own, made = null;
            bool copied = PortraitDeleteMath.Attempt(() =>
            {
                Directory.CreateDirectory(dir);
                string target = Path.Combine(dir, own);
                if (File.Exists(target))
                {
                    if (PortraitDeleteMath.Sha256(File.ReadAllBytes(target)) == PortraitDeleteMath.Sha256(File.ReadAllBytes(from)))
                    {
                        name = own;
                        return;
                    }
                    name = PortraitDeleteMath.BinNames(stem, new[] { own }, n => File.Exists(Path.Combine(dir, n)))[0];
                }
                else name = own;
                string to = Path.Combine(dir, name), tmp = to + ".tmp";
                try
                {
                    File.Copy(from, tmp, true);
                    File.Move(tmp, to);
                }
                catch (Exception)
                {
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
                made = to;
            }, job.tries, wait, out var copyErr);
            if (!copied)
            {
                r.stop = TrainingStop.Copy;
                r.error = copyErr?.Message ?? "";
                return r;
            }

            // Then the list; where it can't be written, the copy made is taken away again.
            job.line.file = name;
            if (!PortraitDeleteMath.Attempt(() => PortraitDeleteMath.WriteSafely(listPath, Append(old, job.line), true), job.tries, wait, out var listErr))
            {
                if (made != null)
                    PortraitDeleteMath.Attempt(() =>
                    {
                        if (File.Exists(made)) File.Delete(made);
                    }, job.tries, wait, out _);
                r.stop = TrainingStop.WriteList;
                r.error = listErr?.Message ?? "";
                return r;
            }
            r.done = true;
            r.copiedAs = name;
            return r;
        }
    }
}
