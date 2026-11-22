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

        private static bool reading, told;
        private static float retryAt = -1f;
        private static int tries;

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
            string path = Path.Combine(Folder, PortraitFolderMath.FileKey(texPath).Replace('/', Path.DirectorySeparatorChar));
            if (!files.TryGetValue(path, out var f))
            {
                f = new PictureFile { key = key, file = path, texPath = texPath, cutOut = cutOut };
                files[path] = f;
            }
            return f;
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
            reading = true;
            retryAt = -1f;
            PortraitStore.Run(() => Read(folder), (got, ex) => Done(folder, got, ex, byHand), false);
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
            Folder = folder;
            People.Clear();
            Backdrops.Clear();
            int missing = 0;
            foreach (var p in got.people)
                if (got.pictures.Contains(PortraitFolderMath.FileKey(p.texPath))) People.Add(p);
                else missing++;
            foreach (var b in got.backdrops)
                if (got.pictures.Contains(PortraitFolderMath.FileKey(b.texPath))) Backdrops.Add(b);
                else missing++;
            Version++;
            Status = "CC_PortraitFolder_Read".Translate(People.Count.Named("PEOPLE"), Backdrops.Count.Named("BACKDROPS"), folder.Named("FOLDER")).Resolve()
                     + (missing > 0 ? " " + "CC_PortraitFolder_Missing".Translate(missing.Named("COUNT")).Resolve() : "");
            Log.Message("[Rimfolk] Portraits: " + Status);
            if (got.problems.Count > 0)
                CouncilLog.WarnOnce("Portraits: records.xml in " + folder + ": " + got.problems.Count + " words it doesn't know, each left out (the rest of the record kept): "
                                    + string.Join("; ", got.problems.Take(12)) + (got.problems.Count > 12 ? "; ..." : ""));
            if (byHand && Current.ProgramState == ProgramState.Playing) Messages.Message(Status, MessageTypeDefOf.SilentInput, false);
        }
    }
}
