using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Deleting a painted picture or a backdrop for good from the game (docs: DESIGN.md, "Portraits: deleting a picture for
    /// good"; the player, 22 November 2026: "when i really don't like an image, that i can say it's fully deleted on the
    /// record and it actually deletes the image and any reference to it? this way i could cycle out all the basic ai faces.
    /// it would actually delete the image files directly through the rimworld client").
    /// <list type="bullet">
    /// <item>On the large portrait's right-click menu (<see cref="InspectPortrait"/>): "Delete this picture for good..." for
    /// the painted picture shown and "Delete this backdrop for good..." for the backdrop shown, each only for one from the
    /// library folder (a mod's pictures are the mod's). The game's own two-button dialog names the file, who holds it now,
    /// where its files go, and that it can't be undone in the game.</item>
    /// <item>Confirmed, on a worker thread (<see cref="PortraitDeleteMath.Run"/>): its line written into the library
    /// folder's deleted.csv, then its records (the picture's and its expressions') taken out of records.xml, each written
    /// safely and tried again a few times (a cloud drive may hold a file a moment); on an error nothing is changed, in the
    /// folder or the game, and a message says so. Then its files (the picture and its expressions, or the backdrop and its
    /// depth map) moved into the bin "deleted pictures" beside the library folder, or deleted, as the setting "Deleting a
    /// picture" says.</item>
    /// <item>In the game, once written: its records dropped from what was read without reading the folder again, its
    /// textures let go, everyone who held it picked again by the usual rules (their pins first, the closest free face, else
    /// the game's own drawing), the large portrait's backdrop chosen again; a message says what was done.</item>
    /// </list>
    /// One deletion at a time, never while the folder is being read.
    /// </summary>
    public static class PortraitDelete
    {
        /// <summary>A deletion is being written on a worker now.</summary>
        public static bool Running { get; private set; }

        private static PortraitDeleteMode Mode => ColonyCouncilMod.Settings?.portraitDelete ?? PortraitDeleteMode.Bin;

        /// <summary>How many holders are named before "and N others".</summary>
        private const int NamesMost = 5;

        // ---------------- The menu ----------------

        /// <summary>The large portrait's menu entries: delete the painted picture shown, and the backdrop shown, for good.</summary>
        public static void AddOptions(Pawn p, List<FloatMenuOption> options)
        {
            if (p == null || options == null) return;
            try
            {
                var pic = PortraitSources.For(p);
                if (pic?.file != null && !pic.name.NullOrEmpty())
                {
                    string label = "CC_Portrait_Delete".Translate().Resolve();
                    string name = pic.name;
                    string why = WhyNot(PortraitFolder.PersonNamed(name) != null, "CC_Portrait_DeleteFromMod");
                    options.Add(why == null ? new FloatMenuOption(label, () => ConfirmPicture(p, name)) : new FloatMenuOption(label + " (" + why + ")", null));
                }
                string back = FaceEngine.BackdropShown(p);
                if (back != null)
                {
                    string label = "CC_Portrait_DeleteBackdrop".Translate().Resolve();
                    string why = WhyNot(PortraitFolder.BackdropNamed(back) != null, "CC_Portrait_DeleteBackdropFromMod");
                    options.Add(why == null ? new FloatMenuOption(label, () => ConfirmBackdrop(p, back)) : new FloatMenuOption(label + " (" + why + ")", null));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: the large portrait's entries to delete a picture", e);
            }
        }

        /// <summary>Why an entry is greyed out (a mod's picture, a deletion still being written, the folder being read, no place for the bin), or null.</summary>
        private static string WhyNot(bool fromFolder, string modKey)
        {
            string key = !fromFolder ? modKey
                : Running ? "CC_Portrait_DeleteBusy"
                : PortraitFolder.Reading ? "CC_Portrait_DeleteReading"
                : Mode == PortraitDeleteMode.Bin && PortraitDeleteMath.BinRoot(PortraitFolder.Folder) == null ? "CC_Portrait_DeleteNoBin"
                : null;
            return key?.Translate().Resolve();
        }

        // ---------------- Asking ----------------

        private static void ConfirmPicture(Pawn shown, string name)
        {
            var fp = PortraitFolder.PersonNamed(name);
            if (fp == null)
            {
                Gone(name);
                return;
            }
            int expressions = PortraitFolder.People.Count(x => x.record.variantOf == name);
            var holders = NamesOf(PortraitKeeper.Instance?.HoldersOfPicture(name) ?? new List<int>(), shown);
            var text = new StringBuilder();
            text.Append("CC_Portrait_DeleteWhat".Translate(FileShown(fp.texPath).Named("FILE")).Resolve());
            if (expressions > 0) text.Append(' ').Append("CC_Portrait_DeleteWhatExpressions".Translate(expressions.Named("COUNT")).Resolve());
            text.Append("\n\n").Append(holders.Count > 0
                ? "CC_Portrait_DeleteHeld".Translate(Listed(holders).Named("NAMES")).Resolve()
                : "CC_Portrait_DeleteHeldNone".Translate().Resolve());
            Ending(text, fp.texPath);
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text.ToString(), () => StartPicture(name), true, "CC_Portrait_DeleteTitle".Translate().Resolve()));
        }

        private static void ConfirmBackdrop(Pawn shown, string name)
        {
            var fb = PortraitFolder.BackdropNamed(name);
            if (fb == null)
            {
                Gone(name);
                return;
            }
            var text = new StringBuilder();
            text.Append("CC_Portrait_DeleteBackdropWhat".Translate(FileShown(fb.texPath).Named("FILE")).Resolve());
            if (!fb.depthPath.NullOrEmpty()) text.Append(' ').Append("CC_Portrait_DeleteWhatDepth".Translate().Resolve());
            text.Append("\n\n").Append("CC_Portrait_DeleteBackdropShown".Translate(shown.Named("PAWN")).Resolve());
            Ending(text, fb.texPath);
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text.ToString(), () => StartBackdrop(name), true, "CC_Portrait_DeleteBackdropTitle".Translate().Resolve()));
        }

        /// <summary>Where its files go, what happens to its record, and that it can't be undone in the game.</summary>
        private static void Ending(StringBuilder text, string texPath)
        {
            text.Append("\n\n").Append(Mode == PortraitDeleteMode.Bin
                ? "CC_Portrait_DeleteToBin".Translate(BinShown(texPath).Named("BIN")).Resolve()
                : "CC_Portrait_DeleteFiles".Translate().Resolve());
            text.Append(' ').Append("CC_Portrait_DeleteRecord".Translate().Resolve());
            text.Append("\n\n").Append("CC_Portrait_DeleteNoUndo".Translate().Resolve());
        }

        private static void Gone(string name) =>
            Messages.Message("CC_Portrait_DeleteGone".Translate(name.Named("NAME")).Resolve(), MessageTypeDefOf.RejectInput, false);

        // ---------------- Starting ----------------

        private static void StartPicture(string name)
        {
            var fp = PortraitFolder.PersonNamed(name);
            if (fp == null || Running || PortraitFolder.Reading)
            {
                Gone(name);
                return;
            }
            var job = NewJob(fp.texPath, false);
            if (job == null) return;
            job.records = PortraitDeleteMath.RecordsOf(name, PortraitFolder.People.Select(x => new KeyValuePair<string, string>(x.record.name, x.record.variantOf)));
            job.named.AddRange(PortraitFolder.People.Where(x => x.record.variantOf == name).Select(x => x.texPath));
            job.line.record = name;
            Keep(job);
            Start(job);
        }

        private static void StartBackdrop(string name)
        {
            var fb = PortraitFolder.BackdropNamed(name);
            if (fb == null || Running || PortraitFolder.Reading)
            {
                Gone(name);
                return;
            }
            var job = NewJob(fb.texPath, true);
            if (job == null) return;
            job.records.Add(name);
            if (!fb.depthPath.NullOrEmpty()) job.named.Add(fb.depthPath);
            job.line.record = name;
            Keep(job);
            Start(job);
        }

        /// <summary>A deletion of the picture at <paramref name="texPath"/> in the library folder read now: where the bin is (as the setting says) and its line for deleted.csv; null where there is no place for the bin.</summary>
        private static DeleteJob NewJob(string texPath, bool backdrop)
        {
            string folder = PortraitFolder.Folder;
            string batch = PortraitDeleteMath.BatchOf(texPath), stem = PortraitDeleteMath.StemOf(texPath);
            bool bin = Mode == PortraitDeleteMode.Bin;
            string root = PortraitDeleteMath.BinRoot(folder);
            if (folder.NullOrEmpty() || bin && root == null)
            {
                Messages.Message("CC_Portrait_DeleteFailed".Translate(FileShown(texPath).Named("FILE"), "CC_Portrait_DeleteNoBin".Translate().Named("WHY")).Resolve(),
                    MessageTypeDefOf.RejectInput, false);
                return null;
            }
            return new DeleteJob
            {
                folder = folder,
                bin = bin ? batch.Length > 0 ? Path.Combine(root, batch.Replace('/', Path.DirectorySeparatorChar)) : root : null,
                texPath = texPath,
                backdrop = backdrop,
                line = new DeletedPicture
                {
                    file = stem + ".png", batch = batch, row = PortraitDeleteMath.RowOf(stem), date = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    how = bin ? "deleted in the game, moved to the bin" : "deleted in the game, files deleted"
                }
            };
        }

        /// <summary>The files every other record of the folder uses, so the deletion never touches them.</summary>
        private static void Keep(DeleteJob job)
        {
            var mine = new HashSet<string>(job.records, StringComparer.Ordinal);
            foreach (var x in PortraitFolder.People)
                if (!mine.Contains(x.record.name))
                    job.keep.Add(x.texPath);
            foreach (var b in PortraitFolder.Backdrops)
            {
                if (mine.Contains(b.record.name)) continue;
                job.keep.Add(b.texPath);
                if (!b.depthPath.NullOrEmpty()) job.keep.Add(b.depthPath);
            }
        }

        private static void Start(DeleteJob job)
        {
            Running = true;
            PortraitStore.Run(() => PortraitDeleteMath.Run(job, ms => Thread.Sleep(ms)), (got, ex) => Done(job, got, ex), false);
        }

        // ---------------- Done ----------------

        /// <summary>
        /// On the game's thread once it is written: nothing changed where it couldn't be; else its records dropped from what
        /// was read, its textures let go, its holders picked again, the large portrait made again, and a message.
        /// </summary>
        private static void Done(DeleteJob job, DeleteResult r, Exception ex)
        {
            Running = false;
            string file = FileShown(job.texPath);
            if (ex != null || r == null || !r.done)
            {
                string error = ex?.Message ?? r?.error ?? "";
                string why = r == null || ex != null ? "CC_Portrait_DeleteWhyError".Translate(error.Named("ERROR")).Resolve()
                    : r.stop == DeleteStop.NoRecords ? "CC_Portrait_DeleteWhyNoRecords".Translate().Resolve()
                    : r.stop == DeleteStop.ReadRecords ? "CC_Portrait_DeleteWhyRead".Translate(error.Named("ERROR")).Resolve()
                    : r.stop == DeleteStop.WriteList ? "CC_Portrait_DeleteWhyList".Translate(error.Named("ERROR")).Resolve()
                    : "CC_Portrait_DeleteWhyRecords".Translate(error.Named("ERROR")).Resolve();
                string said = "CC_Portrait_DeleteFailed".Translate(file.Named("FILE"), why.Named("WHY")).Resolve();
                Log.Warning("[Rimfolk] Portraits: " + said);
                Messages.Message(said, MessageTypeDefOf.CautionInput, false);
                return;
            }
            var ids = new List<int>();
            try
            {
                var keeper = PortraitKeeper.Instance;
                if (!job.backdrop && keeper != null)
                    foreach (var n in job.records)
                        ids.AddRange(keeper.LetGoPicture(n));
                PortraitFolder.Forget(job.records);
                foreach (int id in ids) PortraitSources.Forget(id);
                FaceEngine.ClearStaged();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: a picture deleted for good, in the game", e);
            }
            var text = new StringBuilder();
            text.Append((job.backdrop ? "CC_Portrait_DeletedBackdrop" : "CC_Portrait_Deleted").Translate(file.Named("FILE")).Resolve());
            int extras = r.files.Count(f => !string.Equals(f, PortraitFolderMath.FileKey(job.texPath), StringComparison.OrdinalIgnoreCase));
            if (extras > 0)
                text.Append(' ').Append((job.backdrop ? "CC_Portrait_DeletedDepth" : "CC_Portrait_DeletedExpressions").Translate(extras.Named("COUNT")).Resolve());
            if (r.files.Count > 0)
                text.Append(' ').Append(job.bin != null ? "CC_Portrait_DeletedToBin".Translate(BinShown(job.texPath).Named("BIN")).Resolve() : "CC_Portrait_DeletedFiles".Translate().Resolve());
            if (job.backdrop) text.Append(' ').Append("CC_Portrait_DeletedBackdropAgain".Translate().Resolve());
            else if (ids.Count > 0) text.Append(' ').Append("CC_Portrait_DeletedHolders".Translate(Listed(NamesOf(ids, null)).Named("NAMES")).Resolve());
            if (r.failed.Count > 0)
                text.Append(' ').Append("CC_Portrait_DeletedPartly".Translate(r.failed.Count.Named("COUNT"), r.failedError.Named("ERROR")).Resolve());
            string done = text.ToString();
            Log.Message("[Rimfolk] Portraits: " + done + " Records taken out of records.xml: " + r.recordsTaken + "; files: " + string.Join(", ", r.files)
                        + (r.failed.Count > 0 ? "; not moved: " + string.Join(", ", r.failed) : "") + ".");
            Messages.Message(done, r.failed.Count > 0 ? MessageTypeDefOf.CautionInput : MessageTypeDefOf.SilentInput, false);
        }

        // ---------------- Words ----------------

        /// <summary>A picture's file as the player finds it in the library folder: "colonists\Avila_style_..._.png".</summary>
        private static string FileShown(string texPath) => PortraitFolderMath.FileKey(texPath).Replace('/', '\\');

        /// <summary>Its folder in the bin, beside the library folder: "deleted pictures\colonists".</summary>
        private static string BinShown(string texPath)
        {
            string batch = PortraitDeleteMath.BatchOf(texPath);
            return PortraitDeleteMath.BinFolder + (batch.Length > 0 ? "\\" + batch.Replace('/', '\\') : "");
        }

        /// <summary>
        /// The names of the people with these ids (the one shown first; anyone no longer anywhere in the world counted), for
        /// the dialog and the message.
        /// </summary>
        private static List<string> NamesOf(ICollection<int> ids, Pawn first)
        {
            var names = new List<string>();
            var want = new HashSet<int>(ids ?? new List<int>());
            if (first != null && want.Remove(first.thingIDNumber)) names.Add(first.LabelShortCap);
            if (want.Count > 0)
                foreach (var q in PawnsFinder.All_AliveOrDead)
                {
                    if (q == null || !want.Remove(q.thingIDNumber)) continue;
                    names.Add(q.LabelShortCap);
                    if (want.Count == 0) break;
                }
            if (want.Count > 0) names.Add("CC_Portrait_DeleteNoLonger".Translate(want.Count.Named("COUNT")).Resolve());
            return names;
        }

        /// <summary>"Ape, Bors and Cale", or the first few and "and 4 others".</summary>
        private static string Listed(List<string> names)
        {
            if (names.Count <= NamesMost + 1) return names.ToCommaList(true);
            var some = names.Take(NamesMost).ToList();
            some.Add("CC_Portrait_DeleteOthers".Translate((names.Count - NamesMost).Named("COUNT")).Resolve());
            return some.ToCommaList(true);
        }
    }
}
