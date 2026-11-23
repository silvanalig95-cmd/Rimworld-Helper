using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Keeping a painted picture for training (docs: DESIGN.md, "Portraits: the same age and skin, a bionic arm, and keeping
    /// a picture for training"; the player, 23 November 2026: "maybe I can right click and then already flag for training
    /// ... or it's get copied to a special location from which I can take the image to further train the model later").
    /// <list type="bullet">
    /// <item>On the large portrait's right-click menu (<see cref="InspectPortrait"/>), before the entries to delete:
    /// "Keep for training" for the painted picture it shows, only for one of the library folder. Greyed out with the reason
    /// for a mod's picture, where it shows no painted picture (the game's own drawing, a backdrop), for a picture already
    /// kept, while the last one is still being copied or a deletion written, and where the library folder has no folder
    /// above it.</item>
    /// <item>Chosen, on a worker thread (<see cref="PortraitTrainingMath.Run"/>): the picture copied as it is (the cut-out)
    /// into <c>&lt;the library folder's parent&gt;\training\&lt;batch&gt;\</c> and its line added to
    /// <c>training\training.csv</c> (file, batch, record, date), which the training script reads; on an error nothing
    /// changed. A message says what was done.</item>
    /// </list>
    /// Nothing in the game changes: the picture stays in the library and with whoever holds it. One at a time.
    /// </summary>
    public static class PortraitTraining
    {
        /// <summary>A picture is being copied on a worker now.</summary>
        public static bool Running { get; private set; }

        /// <summary>The records kept for training, as training.csv listed them when first looked at and as kept since, for the training folder <see cref="keptRoot"/>.</summary>
        private static readonly HashSet<string> kept = new HashSet<string>(StringComparer.Ordinal);

        private static string keptRoot;
        private static bool reading;

        /// <summary>The large portrait's menu entry: keep the painted picture shown for training.</summary>
        public static void AddOption(Pawn p, List<FloatMenuOption> options)
        {
            if (p == null || options == null) return;
            try
            {
                string label = "CC_Portrait_Train".Translate().Resolve();
                var pic = PortraitSources.For(p);
                string name = pic?.file != null ? pic.name : null;
                string root = PortraitTrainingMath.Root(PortraitFolder.Folder);
                LookAtList(root);
                string why = name.NullOrEmpty() ? "CC_Portrait_TrainNoPicture"
                    : PortraitFolder.PersonNamed(name) == null ? "CC_Portrait_DeleteFromMod"
                    : root == null ? "CC_Portrait_TrainNoFolder"
                    : Running ? "CC_Portrait_TrainBusy"
                    : PortraitDelete.Running ? "CC_Portrait_DeleteBusy"
                    : kept.Contains(name) ? "CC_Portrait_TrainKept"
                    : null;
                options.Add(why == null ? new FloatMenuOption(label, () => Start(name)) : new FloatMenuOption(label + " (" + why.Translate().Resolve() + ")", null));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: the large portrait's entry to keep a picture for training", e);
            }
        }

        /// <summary>training.csv read on a worker, once for each training folder, so the menu knows which pictures are kept already.</summary>
        private static void LookAtList(string root)
        {
            if (root == null || root == keptRoot || reading) return;
            keptRoot = root;
            kept.Clear();
            reading = true;
            string list = Path.Combine(root, PortraitTrainingMath.ListFile);
            PortraitStore.Run(() => File.Exists(list) ? PortraitTrainingMath.Read(PortraitDeleteMath.TextOf(File.ReadAllBytes(list))) : new List<TrainingPicture>(), (got, ex) =>
            {
                reading = false;
                if (got == null || root != keptRoot) return;
                foreach (var t in got)
                    if (t.record.Length > 0)
                        kept.Add(t.record);
            }, false);
        }

        private static void Start(string name)
        {
            if (Running) return;
            var fp = PortraitFolder.PersonNamed(name);
            string folder = PortraitFolder.Folder, root = PortraitTrainingMath.Root(folder);
            if (fp == null || root == null)
            {
                Messages.Message("CC_Portrait_TrainGone".Translate(name.Named("NAME")).Resolve(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            var job = new TrainingJob
            {
                folder = folder,
                root = root,
                texPath = fp.texPath,
                line = new TrainingPicture
                {
                    batch = PortraitDeleteMath.BatchOf(fp.texPath), record = name, date = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                }
            };
            Running = true;
            PortraitStore.Run(() => PortraitTrainingMath.Run(job, ms => Thread.Sleep(ms)), (got, ex) => Done(job, got, ex), false);
        }

        /// <summary>On the game's thread once it is written: a message saying what was done, or why nothing was.</summary>
        private static void Done(TrainingJob job, TrainingResult r, Exception ex)
        {
            Running = false;
            string file = FileShown(job.texPath), where = WhereShown(job.line.batch);
            if (ex == null && r != null && (r.done || r.already))
            {
                if (job.root == keptRoot) kept.Add(job.line.record);
                string said = r.already
                    ? "CC_Portrait_TrainedAlready".Translate(file.Named("FILE")).Resolve()
                    : "CC_Portrait_Trained".Translate(file.Named("FILE"), where.Named("WHERE")).Resolve();
                if (r.done) Log.Message("[Rimfolk] Portraits: " + said + " As " + r.copiedAs + " in " + job.root + ".");
                Messages.Message(said, MessageTypeDefOf.SilentInput, false);
                return;
            }
            string error = ex?.Message ?? r?.error ?? "";
            string why = r == null || ex != null ? "CC_Portrait_DeleteWhyError".Translate(error.Named("ERROR")).Resolve()
                : r.stop == TrainingStop.NoFile ? "CC_Portrait_TrainWhyNoFile".Translate().Resolve()
                : r.stop == TrainingStop.Copy ? "CC_Portrait_TrainWhyCopy".Translate(error.Named("ERROR")).Resolve()
                : "CC_Portrait_TrainWhyList".Translate(error.Named("ERROR")).Resolve();
            string failed = "CC_Portrait_TrainFailed".Translate(file.Named("FILE"), why.Named("WHY")).Resolve();
            Log.Warning("[Rimfolk] Portraits: " + failed);
            Messages.Message(failed, MessageTypeDefOf.CautionInput, false);
        }

        /// <summary>A picture's file as the player finds it in the library folder: "colonists\Avila_style_..._.png".</summary>
        private static string FileShown(string texPath) => PortraitFolderMath.FileKey(texPath).Replace('/', '\\');

        /// <summary>Its folder in the training folder, beside the library folder: "training\colonists".</summary>
        private static string WhereShown(string batch) => PortraitTrainingMath.Folder + (batch.NullOrEmpty() ? "" : "\\" + batch.Replace('/', '\\'));
    }
}
