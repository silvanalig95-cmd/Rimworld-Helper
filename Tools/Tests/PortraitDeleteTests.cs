using System.Text;
using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// Deleting a painted picture for good from the game (the player, 22 November 2026: "when i really don't like an image,
/// that i can say it's fully deleted on the record and it actually deletes the image and any reference to it"). The pure
/// parts: its records taken out of a records.xml text and the rest kept as it was; which files are a picture's own (its
/// expressions, a backdrop's depth map, never another record's); the bin's names; deleted.csv written and read (a file
/// saved again by Excel too); trying again; the whole deletion on a small library of its own (a bin, a name taken there,
/// deleting outright, nothing changed where a file can't be written); the setting, the words and the seams in the mod and
/// the portrait scripts.
/// </summary>
public static class PortraitDeleteTests
{
    private const string Records = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n"
                                   + "<!-- Rimfolk portrait library, written by Update-PortraitLibrary.ps1: one record per picture in this folder. -->\r\n"
                                   + "<Defs>\r\n"
                                   + "  <ColonyCouncil.PortraitDef>\r\n    <defName>P_X</defName>\r\n    <texPath>colonists/X</texPath>\r\n    <sex>male</sex>\r\n    <age>adult</age>\r\n    <skinColor>#c9a27e</skinColor>\r\n    <hairColor>#2a2a2a</hairColor>\r\n    <character>colonists_X</character>\r\n  </ColonyCouncil.PortraitDef>\r\n"
                                   + "  <ColonyCouncil.PortraitDef>\r\n    <defName>P_X_blink</defName>\r\n    <texPath>colonists/X_v-blink</texPath>\r\n    <variant>blink</variant>\r\n    <variantOf>P_X</variantOf>\r\n    <sex>male</sex>\r\n    <age>adult</age>\r\n    <skinColor>#c9a27e</skinColor>\r\n    <hairColor>#2a2a2a</hairColor>\r\n    <character>colonists_X</character>\r\n  </ColonyCouncil.PortraitDef>\r\n"
                                   + "  <ColonyCouncil.PortraitDef>\r\n    <defName>P_X_smile</defName>\r\n    <texPath>colonists/X_v-smile</texPath>\r\n    <variant>smile</variant>\r\n    <variantOf>P_X</variantOf>\r\n    <sex>male</sex>\r\n    <age>adult</age>\r\n    <skinColor>#c9a27e</skinColor>\r\n    <hairColor>#2a2a2a</hairColor>\r\n    <character>colonists_X</character>\r\n  </ColonyCouncil.PortraitDef>\r\n"
                                   + "  <ColonyCouncil.PortraitDef>\r\n    <defName>P_Y</defName>\r\n    <texPath>colonists/Y</texPath>\r\n    <sex>female</sex>\r\n    <age>young</age>\r\n    <skinColor>#c9a27e</skinColor>\r\n    <hairColor>#2a2a2a</hairColor>\r\n    <pawn>Ape &amp; Co</pawn>\r\n  </ColonyCouncil.PortraitDef>\r\n"
                                   + "  <ColonyCouncil.PortraitDef>\r\n    <defName>P_X2</defName>\r\n    <texPath>colonists/X (2)</texPath>\r\n    <sex>male</sex>\r\n    <age>adult</age>\r\n    <skinColor>#c9a27e</skinColor>\r\n    <hairColor>#2a2a2a</hairColor>\r\n  </ColonyCouncil.PortraitDef>\r\n"
                                   + "  <ColonyCouncil.BackdropDef>\r\n    <defName>B_bed</defName>\r\n    <texPath>backdrops/bed</texPath>\r\n    <depthPath>backdrops/bed_depth</depthPath>\r\n    <place>room</place>\r\n    <rooms><li>Bedroom</li></rooms>\r\n  </ColonyCouncil.BackdropDef>\r\n"
                                   + "</Defs>\r\n";

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "deleting a picture for good: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.Exists(Path.Combine(src, rel)) ? File.ReadAllText(Path.Combine(src, rel)) : "";

        // ---------------- records.xml ----------------
        {
            var removed = new List<string>();
            string after = PortraitDeleteMath.WithoutRecords(Records, new HashSet<string> { "P_X", "P_X_blink", "P_X_smile" }, removed);
            var people = new List<FolderPortrait>();
            var backs = new List<FolderBackdrop>();
            var probs = new List<string>();
            PortraitFolderMath.Parse(after, people, backs, probs);
            C(removed.Count == 3 && removed.Contains("P_X") && removed.Contains("P_X_blink") && removed.Contains("P_X_smile"), "the picture's record and its expressions' taken out, and named");
            C(probs.Count == 0 && people.Select(p => p.record.name).SequenceEqual(new[] { "P_Y", "P_X2" }) && backs.Count == 1 && backs[0].depthPath == "backdrops/bed_depth",
                "every other record read as before, the backdrop with its depth map");
            C(people[0].record.pawn == "Ape & Co" && people[0].record.sex == PortraitSex.Female, "the other records' words kept (an escaped name too)");
            C(after.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<!-- Rimfolk portrait library") && after.EndsWith("</Defs>\r\n"),
                $"the declaration, the comment and the end as they were ({after.Substring(0, Math.Min(60, after.Length)).Replace("\r", "\\r").Replace("\n", "\\n")})");
            C(!after.Contains("\n\r\n") && !Regex.IsMatch(after, "(?<!\r)\n") && !after.Contains("P_X<") && !after.Contains("X_v-"),
                "no blank line where they stood, the file's own line endings kept, nothing of them left");
            string rest = Records;
            foreach (var cut in new[] { "P_X", "P_X_blink", "P_X_smile" })
            {
                int a = rest.IndexOf("  <ColonyCouncil.PortraitDef>\r\n    <defName>" + cut + "</defName>", StringComparison.Ordinal);
                int b = rest.IndexOf("</ColonyCouncil.PortraitDef>\r\n", a, StringComparison.Ordinal) + "</ColonyCouncil.PortraitDef>\r\n".Length;
                rest = rest.Remove(a, b - a);
            }
            C(after == rest.Replace("Ape &amp; Co", "Ape &amp; Co"), "the rest exactly as it was, character for character");
            var none = new List<string>();
            C(ReferenceEquals(PortraitDeleteMath.WithoutRecords(Records, new HashSet<string> { "P_Gone" }, none), Records) && none.Count == 0,
                "a name records.xml doesn't have: the text unchanged");
            var again = new List<string>();
            C(PortraitDeleteMath.WithoutRecords(after, new HashSet<string> { "P_X" }, again) == after && again.Count == 0, "taken out twice: the second time nothing");
            var back = new List<string>();
            string noBack = PortraitDeleteMath.WithoutRecords(Records, new HashSet<string> { "B_bed" }, back);
            people.Clear();
            backs.Clear();
            PortraitFolderMath.Parse(noBack, people, backs, null);
            C(back.SequenceEqual(new[] { "B_bed" }) && backs.Count == 0 && people.Count == 5, "a backdrop's record taken out, the people kept");
            string lf = Records.Replace("\r\n", "\n");
            string lfAfter = PortraitDeleteMath.WithoutRecords(lf, new HashSet<string> { "P_Y" }, new List<string>());
            C(!lfAfter.Contains("\r") && !lfAfter.Contains("P_Y") && lfAfter.Contains("P_X2"), "a file with plain line endings keeps them");
            string unnamed = "<Defs>\n  <ColonyCouncil.PortraitDef>\n    <texPath>a/one</texPath>\n  </ColonyCouncil.PortraitDef>\n  <ColonyCouncil.PortraitDef>\n    <texPath>a/two</texPath>\n  </ColonyCouncil.PortraitDef>\n</Defs>\n";
            people.Clear();
            PortraitFolderMath.Parse(unnamed, people, null, null);
            var un = new List<string>();
            string unAfter = PortraitDeleteMath.WithoutRecords(unnamed, new HashSet<string> { people[1].record.name }, un);
            C(people[1].record.name == "Portrait_2" && un.SequenceEqual(new[] { "Portrait_2" }) && unAfter.Contains("a/one") && !unAfter.Contains("a/two"),
                "a record without a defName taken out by the name the game gives it");
            bool threw = false;
            try
            {
                PortraitDeleteMath.WithoutRecords("not xml <", new HashSet<string> { "P_X" }, new List<string>());
            }
            catch (Exception)
            {
                threw = true;
            }
            C(threw, "a records.xml that is no XML at all is not written over");
            var of = PortraitDeleteMath.RecordsOf("P_X", new[]
            {
                new KeyValuePair<string, string>("P_X", ""), new KeyValuePair<string, string>("P_X_blink", "P_X"), new KeyValuePair<string, string>("P_Y", ""),
                new KeyValuePair<string, string>("P_Y_blink", "P_Y"), new KeyValuePair<string, string>("P_X_pain", "P_X")
            });
            C(of.SequenceEqual(new[] { "P_X", "P_X_blink", "P_X_pain" }), "a picture's records: its own, then its expressions', no one else's");
        }

        // ---------------- Its files ----------------
        {
            var listing = new[]
            {
                "colonists/X.png", "colonists/X_v-blink.png", "colonists/x_V-SMILE.png", "colonists/X (2).png", "colonists/X (2)_v-blink.png", "colonists/Y_v-blink.png",
                "colonists/X_depth.png", "colonists/X_v-blink.png.part", "other/X_v-frown.png", "colonists/Xa_v-pain.png", "backdrops/bed.png", "backdrops/bed_depth.png",
                "backdrops/bed_v-blink.png", "elsewhere/X_v-pain.png"
            };
            var files = PortraitDeleteMath.FilesOf("colonists/X", false, null, null, listing);
            C(files.SequenceEqual(new[] { "colonists/X.png", "colonists/X_v-blink.png", "colonists/x_V-SMILE.png" }),
                $"a person's files: their picture first, then its expressions beside it, letters' case aside ({string.Join(", ", files)})");
            C(!files.Any(f => f.Contains("(2)") || f.Contains("Y_") || f.Contains("_depth") || f.Contains("other/") || f.Contains("Xa_")),
                "never a \" (2)\" copy, another picture's expressions, a depth map, the same name in another folder, or a longer name");
            var named = PortraitDeleteMath.FilesOf("colonists/X", false, new[] { "elsewhere/X_v-pain" }, null, listing);
            C(named.Contains("elsewhere/X_v-pain.png"), "an expression its record names elsewhere goes with it");
            var kept = PortraitDeleteMath.FilesOf("colonists/X", false, null, new[] { "colonists/X_v-blink" }, listing);
            C(!kept.Contains("colonists/X_v-blink.png") && kept.Contains("colonists/X.png"), "a file another record uses is never touched");
            var bed = PortraitDeleteMath.FilesOf("backdrops/bed", true, new[] { "backdrops/bed_depth" }, null, listing);
            C(bed.SequenceEqual(new[] { "backdrops/bed.png", "backdrops/bed_depth.png" }), "a backdrop's files: the backdrop and its depth map, no expressions");
            var lost = PortraitDeleteMath.FilesOf("colonists/Z", false, null, null, listing);
            C(lost.Count == 0, "a picture whose file is gone: nothing");
            var nested = PortraitDeleteMath.FilesOf("a/b/X", false, null, null, new[] { "a/b/X.png", "a/b/X_v-blink.png", "a/X_v-blink.png" });
            C(nested.SequenceEqual(new[] { "a/b/X.png", "a/b/X_v-blink.png" }), "a picture in a folder within a folder");
        }

        // ---------------- Names ----------------
        C(PortraitDeleteMath.BatchOf("colonists/Avila_style_01_cut_00001_") == "colonists" && PortraitDeleteMath.BatchOf("a\\b\\c") == "a/b" && PortraitDeleteMath.BatchOf("solo") == "",
            "a picture's batch folder from its path");
        C(PortraitDeleteMath.StemOf("colonists/Avila_style_01_cut_00001_") == "Avila_style_01_cut_00001_" && PortraitDeleteMath.StemOf("solo") == "solo", "a picture's own name from its path");
        C(PortraitDeleteMath.RowOf("Avila_style_261122-143005_01_cut_00001_") == "Avila" && PortraitDeleteMath.RowOf("room_bedroom_tribal_style-backdrop_01_00001_") == "room_bedroom_tribal"
          && PortraitDeleteMath.RowOf("Avila_style_01_cut_00001_ (2)") == "Avila" && PortraitDeleteMath.RowOf("whatever") == "", "the batch row's name, as the library script reads it");
        C(PortraitDeleteMath.StripCopy("X (2)") == "X" && PortraitDeleteMath.StripCopy("X(12)") == "X" && PortraitDeleteMath.StripCopy("X") == "X", "a copy's \" (2)\" taken off");

        // ---------------- The bin ----------------
        string sep = Path.DirectorySeparatorChar.ToString();
        C(PortraitDeleteMath.BinRoot("/home/a/Rimfolk-portraits/library") == "/home/a/Rimfolk-portraits" + sep + "deleted pictures"
          && PortraitDeleteMath.BinRoot("/home/a/Rimfolk-portraits/library/") == "/home/a/Rimfolk-portraits" + sep + "deleted pictures",
            "the bin beside the library folder, in its parent, never inside it");
        C(PortraitDeleteMath.BinRoot("/") == null && PortraitDeleteMath.BinRoot("") == null && PortraitDeleteMath.BinRoot(null) == null, "a folder with no parent (a drive's root): no bin");
        {
            var names = new[] { "X.png", "X_v-blink.png", "X_v-smile.png" };
            C(PortraitDeleteMath.BinNames("X", names, _ => false).SequenceEqual(names), "nothing of the same name in the bin: their own names");
            var taken = new HashSet<string>(new[] { "X.png" }, StringComparer.OrdinalIgnoreCase);
            C(PortraitDeleteMath.BinNames("X", names, taken.Contains).SequenceEqual(new[] { "X (2).png", "X (2)_v-blink.png", "X (2)_v-smile.png" }),
                "the picture's name taken: all of them with \" (2)\" after the picture's name, so they keep going together");
            taken.Add("X (2)_v-smile.png");
            C(PortraitDeleteMath.BinNames("X", names, taken.Contains).SequenceEqual(new[] { "X (3).png", "X (3)_v-blink.png", "X (3)_v-smile.png" }), "\" (2)\" taken too: \" (3)\"");
            C(PortraitDeleteMath.BinNames("X", new[] { "X.png", "odd.png" }, new HashSet<string> { "odd.png" }.Contains).SequenceEqual(new[] { "X (2).png", "odd (2).png" }),
                "a name not starting with the picture's takes it before .png");
        }

        // ---------------- deleted.csv ----------------
        {
            var d = new DeletedPicture
            {
                file = "Avila_style_01_cut_00001_.png", batch = "colonists", row = "Avila", record = "Portrait_colonists_Avila_01", date = "2026-11-22 18:04",
                how = "deleted in the game, moved to the bin", sha256 = "AB12"
            };
            string fresh = PortraitDeleteMath.Append(null, d);
            C(fresh == "file,batch,row,record,date,how,sha256\r\n" + "Avila_style_01_cut_00001_.png,colonists,Avila,Portrait_colonists_Avila_01,2026-11-22 18:04,\"deleted in the game, moved to the bin\",AB12\r\n",
                "a new list: its columns, then the line, a cell with a comma in quotes");
            var e = new DeletedPicture { file = "Y \"odd\".png", batch = "b", how = "x" };
            string two = PortraitDeleteMath.Append(fresh, e);
            C(two.StartsWith(fresh) && two.EndsWith("\"Y \"\"odd\"\".png\",b,,,,x,\r\n"), "a line added at the end, everything before kept, quotes doubled");
            var read = PortraitDeleteMath.Read(two);
            C(read.Count == 2 && read[0].file == d.file && read[0].how == d.how && read[0].sha256 == "AB12" && read[1].file == "Y \"odd\".png" && read[1].sha256 == "",
                "read back as written");
            string excel = "\uFEFFfile;batch;row;record;date;how;sha256\r\nA.png;b;A;P_A;2026-11-20 10:00;by hand;\r\nB.png;b;B;P_B;2026-11-20 10:01;deleted in the game, files deleted;FF";
            string excelMore = PortraitDeleteMath.Append(excel, d);
            C(excelMore.EndsWith("\r\nAvila_style_01_cut_00001_.png;colonists;Avila;Portrait_colonists_Avila_01;2026-11-22 18:04;deleted in the game, moved to the bin;AB12\r\n")
              && excelMore.Contains("deleted in the game, files deleted;FF\r\nAvila"),
                "a list Excel saved with semicolons: the line with semicolons, after the last line (which had no line ending)");
            var fromExcel = PortraitDeleteMath.Read(excelMore);
            C(fromExcel.Count == 3 && fromExcel[0].file == "A.png" && fromExcel[1].how == "deleted in the game, files deleted" && fromExcel[1].sha256 == "FF" && fromExcel[2].row == "Avila",
                "a list Excel saved (its mark at the start, semicolons) read by its columns");
            var moved = PortraitDeleteMath.Read("sha256,file\nAA,One.png\n,Two.png\n,\n");
            C(moved.Count == 2 && moved[0].file == "One.png" && moved[0].sha256 == "AA" && moved[1].file == "Two.png", "columns in another order read by their names; a line without a file passed over");
            C(PortraitDeleteMath.Append("file,batch\n", d).EndsWith("\n" + PortraitDeleteMath.Line(d) + "\n") && !PortraitDeleteMath.Append("file,batch\n", d).Contains("\r"),
                "a list with plain line endings keeps them");
            C(PortraitDeleteMath.Sha256(Encoding.ASCII.GetBytes("abc")) == "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", "the SHA-256 as PowerShell's Get-FileHash writes it");
        }

        // ---------------- Trying again ----------------
        {
            var waits = new List<int>();
            int calls = 0;
            bool ok = PortraitDeleteMath.Attempt(() =>
            {
                if (++calls < 3) throw new IOException("held by the drive");
            }, 5, waits.Add, out var last);
            C(ok && calls == 3 && last == null && waits.SequenceEqual(new[] { 300, 600 }), "held twice, done the third time, waiting a little longer each time");
            waits.Clear();
            calls = 0;
            ok = PortraitDeleteMath.Attempt(() =>
            {
                calls++;
                throw new IOException("still held");
            }, 5, waits.Add, out last);
            C(!ok && calls == 5 && last?.Message == "still held" && waits.SequenceEqual(new[] { 300, 600, 1200, 2400 }), "always held: given up after five tries, about four and a half seconds, with the error");
        }

        // ---------------- The whole deletion, on a library of its own ----------------
        string dir = Path.Combine(Path.GetTempPath(), "rimfolk-delete-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            string lib = Path.Combine(dir, "Rimfolk-portraits", "library");
            string Lib(string rel) => Path.Combine(lib, rel.Replace('/', Path.DirectorySeparatorChar));
            void Make(string rel, string content)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Lib(rel)));
                File.WriteAllText(Lib(rel), content);
            }
            void Setup()
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                foreach (var f in new[] { "colonists/X.png", "colonists/X_v-blink.png", "colonists/X_v-smile.png", "colonists/Y.png", "colonists/X (2).png", "backdrops/bed.png", "backdrops/bed_depth.png" })
                    Make(f, "picture " + f);
                File.WriteAllText(Lib("records.xml"), Records, new UTF8Encoding(false));
            }
            DeleteJob Job(string tex, bool backdrop, params string[] records)
            {
                string batch = PortraitDeleteMath.BatchOf(tex);
                return new DeleteJob
                {
                    folder = lib, bin = Path.Combine(PortraitDeleteMath.BinRoot(lib), batch), texPath = tex, backdrop = backdrop, records = records.ToList(), tries = 2,
                    named = backdrop ? new List<string> { "backdrops/bed_depth" } : new List<string> { "colonists/X_v-blink", "colonists/X_v-smile" },
                    keep = backdrop ? new List<string> { "colonists/X", "colonists/Y" } : new List<string> { "colonists/Y", "colonists/X (2)", "backdrops/bed", "backdrops/bed_depth" },
                    line = new DeletedPicture { file = PortraitDeleteMath.StemOf(tex) + ".png", batch = batch, row = "X", record = records[0], date = "2026-11-22 18:04", how = "deleted in the game, moved to the bin" }
                };
            }
            var noWait = new List<int>();

            Setup();
            string binDir = Path.Combine(dir, "Rimfolk-portraits", "deleted pictures", "colonists");
            var r = PortraitDeleteMath.Run(Job("colonists/X", false, "P_X", "P_X_blink", "P_X_smile"), noWait.Add);
            string recs = File.ReadAllText(Lib("records.xml"));
            C(r.done && r.recordsTaken == 3 && !recs.Contains("<defName>P_X</defName>") && recs.Contains("<defName>P_X2</defName>") && recs.Contains("<defName>P_Y</defName>"),
                "its records taken out of records.xml, the others kept");
            C(r.files.SequenceEqual(new[] { "colonists/X.png", "colonists/X_v-blink.png", "colonists/X_v-smile.png" }) && r.failed.Count == 0
              && File.Exists(Path.Combine(binDir, "X.png")) && File.Exists(Path.Combine(binDir, "X_v-blink.png")) && File.Exists(Path.Combine(binDir, "X_v-smile.png"))
              && !File.Exists(Lib("colonists/X.png")) && !File.Exists(Lib("colonists/X_v-blink.png")),
                "the picture and its expressions moved into the bin beside the library folder, made where needed");
            C(File.Exists(Lib("colonists/Y.png")) && File.Exists(Lib("colonists/X (2).png")) && File.Exists(Lib("backdrops/bed.png")), "nothing else touched (its \" (2)\" copy, another picture, the backdrops)");
            var listed = PortraitDeleteMath.Read(File.ReadAllText(Lib("deleted.csv")));
            C(listed.Count == 1 && listed[0].file == "X.png" && listed[0].record == "P_X" && listed[0].batch == "colonists"
              && listed[0].sha256 == PortraitDeleteMath.Sha256(Encoding.UTF8.GetBytes("picture colonists/X.png")),
                "its line in deleted.csv, with its file's SHA-256");
            C(File.ReadAllBytes(Lib("deleted.csv")).Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }) && !File.ReadAllBytes(Lib("records.xml")).Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }),
                "deleted.csv marked as UTF-8 for Excel, records.xml written as the script writes it");
            C(!Directory.GetFiles(lib, "*.tmp", SearchOption.AllDirectories).Any() && noWait.Count == 0, "no temporary file left, nothing waited for");

            // A second picture of the same name deleted: the bin's name taken, so " (2)".
            Make("colonists/X.png", "another picture colonists/X.png");
            Make("colonists/X_v-blink.png", "another blink");
            File.WriteAllText(Lib("records.xml"), Records, new UTF8Encoding(false));
            r = PortraitDeleteMath.Run(Job("colonists/X", false, "P_X", "P_X_blink", "P_X_smile"), noWait.Add);
            C(r.done && r.movedTo.SequenceEqual(new[] { "X (2).png", "X (2)_v-blink.png" }) && File.ReadAllText(Path.Combine(binDir, "X (2).png")) == "another picture colonists/X.png"
              && File.ReadAllText(Path.Combine(binDir, "X.png")) == "picture colonists/X.png",
                "a name already in the bin: the new ones get \" (2)\", the first kept");
            C(PortraitDeleteMath.Read(File.ReadAllText(Lib("deleted.csv"))).Count == 2, "a second line added to deleted.csv");

            // A backdrop and its depth map.
            r = PortraitDeleteMath.Run(Job("backdrops/bed", true, "B_bed"), noWait.Add);
            string backBin = Path.Combine(dir, "Rimfolk-portraits", "deleted pictures", "backdrops");
            C(r.done && r.recordsTaken == 1 && File.Exists(Path.Combine(backBin, "bed.png")) && File.Exists(Path.Combine(backBin, "bed_depth.png")) && !File.Exists(Lib("backdrops/bed.png"))
              && !File.ReadAllText(Lib("records.xml")).Contains("B_bed"),
                "a backdrop and its depth map into the bin's backdrops folder, its record out");

            // The setting "Delete the files".
            Setup();
            var del = Job("colonists/X", false, "P_X", "P_X_blink", "P_X_smile");
            del.bin = null;
            r = PortraitDeleteMath.Run(del, noWait.Add);
            C(r.done && r.files.Count == 3 && !File.Exists(Lib("colonists/X.png")) && !File.Exists(Lib("colonists/X_v-smile.png")) && !Directory.Exists(Path.Combine(dir, "Rimfolk-portraits", "deleted pictures"))
              && File.Exists(Lib("colonists/Y.png")),
                "deleting the files: gone, no bin made, the rest kept");

            // records.xml no longer has it (written again since): the list and the files still done.
            Setup();
            File.WriteAllText(Lib("records.xml"), "<Defs>\n</Defs>\n");
            r = PortraitDeleteMath.Run(Job("colonists/X", false, "P_X"), noWait.Add);
            C(r.done && r.recordsTaken == 0 && File.ReadAllText(Lib("records.xml")) == "<Defs>\n</Defs>\n" && File.Exists(Path.Combine(binDir, "X.png")),
                "a record no longer in records.xml: records.xml left as it is, the picture still deleted");

            // Nothing changed where a file can't be written.
            Setup();
            File.WriteAllText(Lib("deleted.csv"), "file,batch\r\nOld.png,b\r\n");
            Directory.CreateDirectory(Lib("records.xml.tmp"));
            noWait.Clear();
            r = PortraitDeleteMath.Run(Job("colonists/X", false, "P_X", "P_X_blink", "P_X_smile"), noWait.Add);
            C(!r.done && r.stop == DeleteStop.WriteRecords && File.ReadAllText(Lib("records.xml")) == Records && File.ReadAllBytes(Lib("deleted.csv")).SequenceEqual(Encoding.UTF8.GetBytes("file,batch\r\nOld.png,b\r\n"))
              && File.Exists(Lib("colonists/X.png")) && File.Exists(Lib("colonists/X_v-blink.png")) && noWait.Count > 0,
                "records.xml can't be written: tried again, then deleted.csv put back as it was and the files left, nothing changed");
            Directory.Delete(Lib("records.xml.tmp"));
            File.Delete(Lib("deleted.csv"));
            Directory.CreateDirectory(Lib("deleted.csv.tmp"));
            r = PortraitDeleteMath.Run(Job("colonists/X", false, "P_X"), noWait.Add);
            C(!r.done && r.stop == DeleteStop.WriteList && File.ReadAllText(Lib("records.xml")) == Records && !File.Exists(Lib("deleted.csv")) && File.Exists(Lib("colonists/X.png")),
                "deleted.csv can't be written: nothing changed");
            Directory.Delete(Lib("deleted.csv.tmp"));
            File.Delete(Lib("records.xml"));
            r = PortraitDeleteMath.Run(Job("colonists/X", false, "P_X"), noWait.Add);
            C(!r.done && r.stop == DeleteStop.NoRecords && !File.Exists(Lib("deleted.csv")) && File.Exists(Lib("colonists/X.png")), "no records.xml: nothing changed");
            File.WriteAllText(Lib("records.xml"), "not xml <");
            r = PortraitDeleteMath.Run(Job("colonists/X", false, "P_X"), noWait.Add);
            C(!r.done && r.stop == DeleteStop.ReadRecords && File.ReadAllText(Lib("records.xml")) == "not xml <" && File.Exists(Lib("colonists/X.png")),
                "a records.xml that isn't XML: left as it is, nothing changed");

            // Safely written: a new file, and one replaced.
            string safe = Path.Combine(dir, "safe.txt");
            PortraitDeleteMath.WriteSafely(safe, "one", false);
            PortraitDeleteMath.WriteSafely(safe, "two", false);
            C(File.ReadAllText(safe) == "two" && !File.Exists(safe + ".tmp"), "written safely: a new file, then replaced, no temporary file left");
        }
        finally
        {
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            catch (Exception)
            {
                // A temporary folder left behind.
            }
        }

        // ---------------- The setting, the words and the seams ----------------
        string settings = Read("CouncilSettings.cs");
        C(settings.Contains("public PortraitDeleteMode portraitDelete = PortraitDeleteMode.Bin;") && settings.Contains("Scribe_Values.Look(ref portraitDelete, \"portraitDelete\", PortraitDeleteMode.Bin);")
          && SettingsCatalog.Find("portraitDelete")?.section == "CC_SetSec_Portraits" && Enum.GetNames(typeof(PortraitDeleteMode)).SequenceEqual(SettingsCatalog.Find("portraitDelete")?.options ?? new string[0]),
            "the setting \"Deleting a picture\", to the bin by default, under Portraits, its choices the mode's");
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Portraits.xml"));
        C(keyed.Contains("<CC_SetLabel_PortraitDelete>Deleting a picture<") && keyed.Contains("<CC_PortraitDelete_Bin>Move it to the bin<") && keyed.Contains("<CC_PortraitDelete_Delete>Delete the files<")
          && keyed.Contains("<CC_Portrait_Delete>Delete this picture for good...<") && keyed.Contains("<CC_Portrait_DeleteBackdrop>Delete this backdrop for good...<")
          && keyed.Contains("<CC_Portrait_DeleteNoUndo>This can't be undone in the game.<"),
            "the setting, its choices and the menu's entries by the names the brief gave them");
        string del2 = Read("UI/Faces/PortraitDelete.cs");
        var used = Regex.Matches(del2, "\"(CC_[A-Za-z_]+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        var missing = used.Where(k => !keyed.Contains("<" + k + ">")).ToList();
        C(used.Count > 20 && missing.Count == 0, $"every word the deletion uses is in the language file ({string.Join(", ", missing)})");
        foreach (var k in new[] { "CC_SetNote_PortraitDelete_Bin", "CC_SetNote_PortraitDelete_Delete", "CC_SetTip_PortraitDelete" })
            C(keyed.Contains("<" + k + ">"), $"the words: {k}");
        var deleteWords = Regex.Matches(keyed, @"<(CC_(?:Portrait_Delete|PortraitDelete_|SetNote_PortraitDelete|SetTip_PortraitDelete|SetLabel_PortraitDelete)\w*)>([^<]*)<").Select(m => m.Groups[2].Value).ToList();
        C(deleteWords.Count > 30 && deleteWords.All(w => !w.Contains("\u2014") && !w.Contains("!")), "plain words: no dash of that kind, no exclamation marks");
        string inspect = Read("UI/Faces/InspectPortrait.cs"), folder = Read("UI/Faces/PortraitFolder.cs"), store = Read("UI/Faces/PortraitStore.cs"), keeper = Read("UI/Faces/PortraitKeeper.cs");
        string stage = Read("UI/Faces/FaceEngineStage.cs");
        C(inspect.Contains("PortraitDelete.AddOptions(p, options);"), "the entries on the large portrait's right-click menu");
        C(del2.Contains("PortraitFolder.PersonNamed(name) != null") && del2.Contains("PortraitFolder.BackdropNamed(back) != null") && del2.Contains("FaceEngine.BackdropShown(p)"),
            "only a library folder's picture or backdrop, the one shown");
        C(del2.Contains("Dialog_MessageBox.CreateConfirmation(") && del2.Contains("HoldersOfPicture(name)"), "the game's own two-button dialog, naming who holds it");
        C(del2.Contains("PortraitStore.Run(() => PortraitDeleteMath.Run(job, ms => Thread.Sleep(ms))"), "written on a worker thread, never holding the game up");
        int failAt = del2.IndexOf("!r.done", StringComparison.Ordinal), forgetAt = del2.IndexOf("PortraitFolder.Forget(job.records)", StringComparison.Ordinal);
        C(failAt > 0 && forgetAt > failAt && del2.IndexOf("return;", failAt, StringComparison.Ordinal) < forgetAt, "on an error nothing changes in the game");
        C(del2.Contains("keeper.LetGoPicture(n)") && del2.Contains("PortraitSources.Forget(id)") && del2.Contains("FaceEngine.ClearStaged()"),
            "in the game: its holders picked again, their pictures and the large portrait made again");
        C(folder.Contains("public static int Forget(ICollection<string> names)") && folder.Contains("PortraitStore.LetGo(f);") && folder.Contains("if (n > 0) Version++;")
          && folder.Contains("if (PortraitDelete.Running || PortraitCorrect.Running)"),
            "the folder's records dropped without reading it again, their textures let go; never read while a deletion is written");
        C(store.Contains("public static void LetGo(PictureFile f)") && store.Contains("if (f == null || f.gone) return null;") && Regex.Matches(store, @"if \(f\.gone\)").Count >= 3,
            "a deleted picture's texture let go, and one still being read let go when it comes");
        C(keeper.Contains("public List<int> LetGoPicture(string picture)") && keeper.Contains("k.library = 0;"), "whoever kept it keeps nothing now and is picked again when shown");
        C(stage.Contains("public static string BackdropShown(Pawn p)"), "the backdrop the large portrait shows, for its menu");

        // The portrait scripts' side (they honour deleted.csv) is kept and tested in Rimfolk Studio since 24 November 2026.
        return n;
    }
}
