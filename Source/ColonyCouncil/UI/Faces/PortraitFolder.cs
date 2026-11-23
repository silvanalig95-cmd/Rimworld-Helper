using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The player's portrait library folder (docs: DESIGN.md, "Portraits, phase 4"; the player, 22 November 2026: the
    /// pictures stay in one folder kept in step between their two computers, not in a mod), set in Rimfolk's settings
    /// ("Portrait library folder"; empty: none). Its <c>records.xml</c> describes every picture as the defs do
    /// (<see cref="PortraitFolderMath.Parse"/>), its pictures are in its subfolders (<c>&lt;folder&gt;/&lt;texPath&gt;.png</c>),
    /// read only when shown as any library picture is.
    /// <list type="bullet">
    /// <item>Read when the game has started (after the defs) and again with "Reload the library", on a worker thread, so a
    /// folder kept in step by a cloud drive (whose files may take a moment to open the first time) never holds the game up.</item>
    /// <item>A missing or unreadable folder: one message, and the folder's part of the library is off; tried again a minute
    /// later, a few times.</item>
    /// <item>Its records take part in the matching exactly as the defs of any mod do; the defs (a mod's <c>Portraits</c>
    /// folder, or Textures) are still read beside it.</item>
    /// </list>
    /// </summary>
    [StaticConstructorOnStartup]
    public static class PortraitFolder
    {
        /// <summary>The folder read (as cleaned), or "" for none.</summary>
        public static string Folder { get; private set; } = "";

        /// <summary>Changes whenever the folder's records change, so the library and the backdrops are read again.</summary>
        public static int Version;

        public static readonly List<FolderPortrait> People = new List<FolderPortrait>();
        public static readonly List<FolderBackdrop> Backdrops = new List<FolderBackdrop>();
        private static readonly Dictionary<string, PictureFile> files = new Dictionary<string, PictureFile>(StringComparer.OrdinalIgnoreCase);

        /// <summary>What the last reading found, for the settings' tooltip and the log.</summary>
        public static string Status = "";

        private static bool reading, told, waitedByHand;
        private static float retryAt = -1f, readAt = -1000f;
        private static int tries;

        /// <summary>A reading this recent (seconds) is not repeated when a game starts.</summary>
        public const float FreshFor = 60f;

        /// <summary>The folder is being read on a worker now (a picture isn't deleted meanwhile).</summary>
        public static bool Reading => reading;

        private const float RetryIn = 60f;
        private const int TriesMost = 10;

        static PortraitFolder()
        {
            try
            {
                Start(false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: reading the library folder at start-up", e);
            }
        }

        /// <summary>The folder as the settings have it now (cleaned of quotation marks and spaces at its ends).</summary>
        public static string Asked => PortraitFolderMath.CleanFolder(ColonyCouncilMod.Settings?.portraitFolder);

        /// <summary>A picture's file in the folder, the same object for the same path across readings (so a picture once loaded stays loaded).</summary>
        public static PictureFile FileOf(string key, string texPath, bool cutOut)
        {
            string path = PathOf(texPath);
            if (!files.TryGetValue(path, out var f))
            {
                f = new PictureFile { key = key, file = path, texPath = texPath, cutOut = cutOut };
                files[path] = f;
            }
            return f;
        }

        private static string PathOf(string texPath) => Path.Combine(Folder, PortraitFolderMath.FileKey(texPath).Replace('/', Path.DirectorySeparatorChar));

        /// <summary>A person's picture read from the folder (not an expression) by its record's name, or null.</summary>
        public static FolderPortrait PersonNamed(string name) => name.NullOrEmpty() ? null : People.Find(p => p.record.name == name && !p.record.IsExpression);

        /// <summary>Any record of a person's picture read from the folder by its name (an expression too), or null.</summary>
        public static FolderPortrait RecordNamed(string name) => name.NullOrEmpty() ? null : People.Find(p => p.record.name == name);

        /// <summary>The names deleted.csv lists (without .png, in small letters), as read with the records: a variant's file made again after one was deleted takes the next free name.</summary>
        public static HashSet<string> Deleted { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>A picture deleted for good in the game: its name taken, as deleted.csv now lists it.</summary>
        public static void NoteDeleted(string texPath) => Deleted.Add(PortraitDeleteMath.StemOf(texPath));

        /// <summary>Records changed in the game (corrected): the library and the backdrops read again from them, without reading the folder.</summary>
        public static void Changed() => Version++;

        /// <summary>A backdrop read from the folder by its record's name, or null.</summary>
        public static FolderBackdrop BackdropNamed(string name) => name.NullOrEmpty() ? null : Backdrops.Find(b => b.record.name == name);

        /// <summary>
        /// Pictures deleted for good in the game (UI/Faces/PortraitDelete.cs): their records taken out of what was read,
        /// and their files let go (the textures, and their entries here, so a new file at the same place is read anew); the
        /// library and the backdrops are then read again from what is left, without reading the folder again. Returns how
        /// many records were taken out.
        /// </summary>
        public static int Forget(ICollection<string> names)
        {
            if (names == null || names.Count == 0) return 0;
            int n = 0;
            foreach (var p in People.Where(x => names.Contains(x.record.name)).ToList())
            {
                LetGoFile(p.texPath);
                People.Remove(p);
                n++;
            }
            foreach (var b in Backdrops.Where(x => names.Contains(x.record.name)).ToList())
            {
                LetGoFile(b.texPath);
                if (!b.depthPath.NullOrEmpty()) LetGoFile(b.depthPath);
                Backdrops.Remove(b);
                n++;
            }
            if (n > 0) Version++;
            return n;
        }

        private static void LetGoFile(string texPath)
        {
            string path = PathOf(texPath);
            if (!files.TryGetValue(path, out var f)) return;
            PortraitStore.LetGo(f);
            files.Remove(path);
        }

        /// <summary>Reads the folder (again) on a worker thread; <paramref name="byHand"/> (the settings' button) tells the player what came of it.</summary>
        public static void Start(bool byHand)
        {
            // The looks' words are known before the records are read on a worker.
            PortraitLooks.Ensure();
            string folder = Asked;
            if (byHand)
            {
                told = false;
                tries = 0;
            }
            if (folder.Length == 0)
            {
                if (Folder.Length > 0 || People.Count > 0 || Backdrops.Count > 0)
                {
                    People.Clear();
                    Backdrops.Clear();
                    Version++;
                }
                Folder = "";
                Status = "CC_PortraitFolder_None".Translate().Resolve();
                retryAt = -1f;
                return;
            }
            if (reading) return;
            // A picture being deleted for good or corrected: read once it is written, so the reading never brings back what it changes.
            if (PortraitDelete.Running || PortraitCorrect.Running)
            {
                waitedByHand |= byHand;
                retryAt = Time.realtimeSinceStartup + 1f;
                return;
            }
            byHand |= waitedByHand;
            waitedByHand = false;
            reading = true;
            retryAt = -1f;
            PortraitStore.Run(() => Read(folder), (got, ex) => Done(folder, got, ex, byHand), false);
        }

        /// <summary>
        /// A game started, a new colony or a save loaded (PortraitKeeper.FinalizeInit): the folder read again on a worker,
        /// unless it is being read or was read in the last minute (the game's own start-up reading). Nothing is looked at
        /// while playing (the player, 23 November 2026: "the reread on startup and new game, or manual reload is enough, i'm
        /// a bit concerned about lags and performance if it automatically rereads").
        /// </summary>
        public static void GameStarted()
        {
            if (reading || Time.realtimeSinceStartup - readAt < FreshFor) return;
            Start(false);
        }

        /// <summary>Once a frame from the store's pump: a failed reading tried again a minute later, a few times.</summary>
        public static void Pump()
        {
            if (retryAt > 0f && Time.realtimeSinceStartup >= retryAt && !reading)
            {
                retryAt = -1f;
                Start(false);
            }
        }

        private sealed class Got
        {
            public List<FolderPortrait> people = new List<FolderPortrait>();
            public List<FolderBackdrop> backdrops = new List<FolderBackdrop>();
            public HashSet<string> pictures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> deleted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            /// <summary>How many records the corrections made in the game changed on top of records.xml.</summary>
            public int corrected;
            public List<string> problems = new List<string>();
            public string error;
        }

        /// <summary>On a worker thread: records.xml read and parsed, and the folder's pictures listed.</summary>
        private static Got Read(string folder)
        {
            var got = new Got();
            if (!Directory.Exists(folder))
            {
                got.error = "CC_PortraitFolder_NotThere";
                return got;
            }
            string records = Path.Combine(folder, "records.xml");
            if (!File.Exists(records))
            {
                got.error = "CC_PortraitFolder_NoRecords";
                return got;
            }
            PortraitFolderMath.Parse(File.ReadAllText(records), got.people, got.backdrops, got.problems);
            foreach (var f in Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories))
                got.pictures.Add(f.Substring(folder.Length).TrimStart('\\', '/').Replace('\\', '/'));
            try
            {
                string list = Path.Combine(folder, PortraitDeleteMath.ListFile);
                if (File.Exists(list))
                    foreach (var d in PortraitDeleteMath.Read(PortraitDeleteMath.TextOf(File.ReadAllBytes(list))))
                        got.deleted.Add(PortraitDeleteMath.StemOf(d.file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? d.file.Substring(0, d.file.Length - 4) : d.file));
            }
            catch (Exception)
            {
                // deleted.csv held open (Excel): read again with the folder; only the names of variants made again depend on it.
            }
            // The corrections made in the game always hold (the player, 23 November 2026: "the corrections i make through the
            // game client are always prioritized"): taken again on top of records.xml, in case it was written without them.
            try
            {
                string corrections = Path.Combine(folder, PortraitCorrectMath.ListFile);
                if (File.Exists(corrections)) got.corrected = PortraitCorrectMath.ApplyGameLines(got.people, PortraitDeleteMath.TextOf(File.ReadAllBytes(corrections)));
            }
            catch (Exception e)
            {
                got.problems.Add("corrections.csv could not be read (" + e.Message + "), so the corrections made in the game were not taken again");
            }
            return got;
        }

        private static void Done(string folder, Got got, Exception ex, bool byHand)
        {
            reading = false;
            if (folder != Asked) return;
            if (ex != null || got == null || got.error != null)
            {
                string why = got?.error != null ? got.error.Translate().Resolve() : ex?.Message ?? "?";
                Status = "CC_PortraitFolder_Failed".Translate(folder.Named("FOLDER"), why.Named("WHY")).Resolve();
                if (Folder.Length > 0 || People.Count > 0 || Backdrops.Count > 0)
                {
                    People.Clear();
                    Backdrops.Clear();
                    Version++;
                }
                Folder = "";
                if (!told || byHand)
                {
                    told = true;
                    Log.Warning("[Rimfolk] " + Status);
                    if (Current.ProgramState == ProgramState.Playing) Messages.Message(Status, MessageTypeDefOf.CautionInput, false);
                }
                if (++tries < TriesMost) retryAt = Time.realtimeSinceStartup + RetryIn;
                return;
            }
            tries = 0;
            readAt = Time.realtimeSinceStartup;
            int before = Folder == folder ? People.Count(x => !x.record.IsExpression && !x.record.IsMarkVariant) : -1;
            Folder = folder;
            People.Clear();
            Backdrops.Clear();
            Deleted = got.deleted;
            int missing = 0, expressions = 0, faces = 0, depths = 0, marked = 0;
            foreach (var p in got.people)
                if (got.pictures.Contains(PortraitFolderMath.FileKey(p.texPath)))
                {
                    People.Add(p);
                    if (p.record.IsMarkVariant) marked++;
                    if (!p.record.IsExpression) continue;
                    expressions++;
                    // Phase 6: told only for the expressions Rimfolk shows (a later word is kept quietly).
                    if (PortraitLifeMath.FaceWord(p.record.variant) != null) faces++;
                }
                else missing++;
            foreach (var b in got.backdrops)
                if (got.pictures.Contains(PortraitFolderMath.FileKey(b.texPath)))
                {
                    // Phase 6: its depth map where the file is there (named in the record, else beside it by name).
                    b.depthPath = PortraitFolderMath.DepthOf(b, got.pictures);
                    if (b.depthPath.Length > 0) depths++;
                    Backdrops.Add(b);
                }
                else missing++;
            Version++;
            Status = "CC_PortraitFolder_Read".Translate((People.Count - expressions - marked).Named("PEOPLE"), Backdrops.Count.Named("BACKDROPS"), folder.Named("FOLDER")).Resolve()
                     + (faces + depths > 0 ? " " + "CC_PortraitFolder_Alive".Translate(faces.Named("FACES"), depths.Named("DEPTHS")).Resolve() : "")
                     + (marked > 0 ? " " + "CC_PortraitFolder_Marked".Translate(marked.Named("COUNT")).Resolve() : "")
                     + (missing > 0 ? " " + "CC_PortraitFolder_Missing".Translate(missing.Named("COUNT")).Resolve() : "");
            Log.Message("[Rimfolk] Portraits: " + Status
                        + (got.corrected > 0 ? " Corrections made in the game (corrections.csv) taken again on top of records.xml for " + got.corrected + " records." : ""));
            if (got.problems.Count > 0)
                CouncilLog.WarnOnce("Portraits: records.xml in " + folder + ": " + got.problems.Count + " words it doesn't know, each left out (the rest of the record kept): "
                                    + string.Join("; ", got.problems.Take(12)) + (got.problems.Count > 12 ? "; ..." : ""));
            if (byHand && Current.ProgramState == ProgramState.Playing) Messages.Message(Status, MessageTypeDefOf.SilentInput, false);
            else if (before >= 0 && Current.ProgramState == ProgramState.Playing)
            {
                // Read again when a game started: a short word only where the library changed since its last reading.
                int now = People.Count - expressions - marked;
                if (now != before)
                    Messages.Message("CC_PortraitFolder_ReadAgain".Translate(now.Named("COUNT"), (now - before).ToStringWithSign().Named("CHANGE")).Resolve(),
                        MessageTypeDefOf.SilentInput, false);
            }
        }
    }
}
