using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Correcting a picture's record in the game, from the window "About this picture" (docs: DESIGN.md, "Portraits: about
    /// this picture, and the variants the game wants"; the player, 23 November 2026: "if I see, for example, the sex is
    /// incorrect or the age is actually kind of a different age group, that I could immediately change and update this
    /// through the game"):
    /// <list type="bullet">
    /// <item>A change picked from a list (sex, age band, features, marks, side, looks, work, headwear; never free text) is
    /// taken at once: the record in the game changed, with its versions' (its expressions and mark variants, made from it;
    /// a mark variant keeps its own mark and side); whoever keeps it and no longer fits it is picked again by the usual rules
    /// (<see cref="PortraitLibrary.Corrected"/>); the library read again from the records as they are now.</item>
    /// <item>Then, on a worker thread (<see cref="PortraitCorrectMath.Run"/>): its lines added to the library folder's
    /// corrections.csv, marked yes, "set in the game" (so Update-PortraitLibrary keeps the change), and the records
    /// written into records.xml (so it stands when the game reads the folder again, before Update is run); each written
    /// safely and tried again a few times. A message says what was written.</item>
    /// </list>
    /// Only a picture of the library folder (a mod's pictures are the mod's), never while the folder is being read or a
    /// picture deleted; one writing at a time, a change made meanwhile written after it.
    /// </summary>
    public static class PortraitCorrect
    {
        /// <summary>A correction is being written on a worker now.</summary>
        public static bool Running { get; private set; }

        private static CorrectJob waiting;
        private static readonly List<string> waitingSaid = new List<string>();

        /// <summary>Why a picture can't be corrected now (a mod's picture, the folder being read, a deletion being written), or null.</summary>
        public static string WhyNot(string picture)
        {
            string key = PortraitFolder.RecordNamed(picture) == null ? "CC_Portrait_DeleteFromMod"
                : PortraitFolder.Reading ? "CC_Portrait_DeleteReading"
                : PortraitDelete.Running ? "CC_Portrait_DeleteBusy"
                : null;
            return key?.Translate().Resolve();
        }

        /// <summary>The picture a record is a version of (its own where it is none), as read from the library folder.</summary>
        public static FolderPortrait RootOf(string picture)
        {
            var fp = PortraitFolder.RecordNamed(picture);
            if (fp == null || fp.record.variantOf.Length == 0) return fp;
            return PortraitFolder.PersonNamed(fp.record.variantOf) ?? fp;
        }

        /// <summary>
        /// A change to a picture's record (<paramref name="change"/>, made on a copy), taken at once and written: its versions
        /// changed with it, whoever no longer fits it picked again, its lines written to corrections.csv and the records to
        /// records.xml. Nothing where the change changes nothing.
        /// </summary>
        public static void Change(string picture, Action<PortraitRecord> change)
        {
            var root = RootOf(picture);
            string why = WhyNot(root?.record.name);
            if (root == null || why != null)
            {
                Messages.Message("CC_Portrait_CorrectNot".Translate((why ?? "?").Named("WHY")).Resolve(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            var before = PortraitCorrectMath.Copy(root.record);
            var after = PortraitCorrectMath.Copy(root.record);
            change?.Invoke(after);
            var fixes = PortraitCorrectMath.Fixes(before, after);
            if (fixes.Count == 0) return;
            List<int> gone;
            var family = new Dictionary<string, PortraitRecord>(StringComparer.Ordinal);
            try
            {
                var names = PortraitDeleteMath.RecordsOf(root.record.name, PortraitFolder.People.Select(x => new KeyValuePair<string, string>(x.record.name, x.record.variantOf)));
                foreach (var fp in PortraitFolder.People.Where(x => names.Contains(x.record.name)))
                {
                    foreach (var f in fixes)
                        if (fp == root || PortraitCorrectMath.Carries(fp.record, f))
                            PortraitCorrectMath.Apply(fp.record, f);
                    // Copied for the worker: the game may change the record again while it writes.
                    family[fp.record.name] = PortraitCorrectMath.Copy(fp.record);
                }
                gone = PortraitLibrary.Source.Corrected(root.record.name);
                FaceEngine.ClearStaged();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: a picture's record corrected in the game", e);
                return;
            }
            string what = string.Join(", ", fixes.Select(f => f.field + " " + f.value));
            string said = "CC_Portrait_Corrected".Translate(PortraitCorrectMath.PictureCell(root.texPath).Named("FILE"), what.Named("WHAT")).Resolve();
            if (gone.Count > 0) said += " " + "CC_Portrait_CorrectedAgain".Translate(Names(gone).Named("NAMES")).Resolve();
            var job = new CorrectJob { folder = PortraitFolder.Folder, records = family };
            job.lines.Add(new KeyValuePair<string, List<PortraitFix>>(PortraitCorrectMath.PictureCell(root.texPath), fixes));
            Log.Message("[Rimfolk] Portraits: corrected in the game: " + root.record.name + ": " + what + (gone.Count > 0 ? "; picked again: " + gone.Count : "") + ".");
            Queue(job, said);
        }

        /// <summary>The job written now, or after the one being written (the two together).</summary>
        private static void Queue(CorrectJob job, string said)
        {
            if (!Running)
            {
                Start(job, new List<string> { said });
                return;
            }
            if (waiting == null) waiting = job;
            else
            {
                waiting.lines.AddRange(job.lines);
                foreach (var kv in job.records) waiting.records[kv.Key] = kv.Value;
            }
            waitingSaid.Add(said);
        }

        private static void Start(CorrectJob job, List<string> said)
        {
            Running = true;
            PortraitStore.Run(() => PortraitCorrectMath.Run(job, ms => Thread.Sleep(ms)), (r, ex) => Done(r, ex, said), false);
        }

        /// <summary>On the game's thread once written: a message saying what was written, or what wasn't; then a change made meanwhile.</summary>
        private static void Done(CorrectResult r, Exception ex, List<string> said)
        {
            Running = false;
            string text = string.Join(" ", said);
            if (ex == null && r != null && r.listed && r.recorded)
                Messages.Message(text + " " + "CC_Portrait_CorrectWritten".Translate().Resolve(), MessageTypeDefOf.SilentInput, false);
            else
            {
                string error = ex?.Message ?? r?.error ?? "";
                string why = r != null && r.listed ? "CC_Portrait_CorrectNoRecords".Translate(error.Named("ERROR")).Resolve() : "CC_Portrait_CorrectNoList".Translate(error.Named("ERROR")).Resolve();
                Log.Warning("[Rimfolk] Portraits: " + text + " " + why);
                Messages.Message(text + " " + why, MessageTypeDefOf.CautionInput, false);
            }
            if (waiting == null) return;
            var next = waiting;
            var nextSaid = new List<string>(waitingSaid);
            waiting = null;
            waitingSaid.Clear();
            Start(next, nextSaid);
        }

        /// <summary>"Ape, Bors and Cale": the names of the people with these ids.</summary>
        private static string Names(List<int> ids)
        {
            var want = new HashSet<int>(ids);
            var names = new List<string>();
            foreach (var q in PawnsFinder.All_AliveOrDead)
                if (q != null && want.Remove(q.thingIDNumber))
                    names.Add(q.LabelShortCap);
            return names.ToCommaList(true);
        }
    }
}
