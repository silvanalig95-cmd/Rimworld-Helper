using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The versions of pictures the game wants painted (docs: DESIGN.md, "Portraits: about this picture, and the variants
    /// the game wants"; the player, 23 November 2026: "variants are first and foremost relevant for my actual colonists ...
    /// it should kind of log who is actually my colonist at the moment ... so that these images get prioritized in the
    /// variation script", "if a pawn loses an eye ... it then creates an entry, create a one-eyed, you know, with detailed
    /// instructions, variant of this specific image", and "it's also important that a variant is then marked as done once
    /// it's created and in the game, not that it will get created over and over"):
    /// <list type="bullet">
    /// <item>For each of the colony's own people keeping a painted picture of the library folder (colonists, then slaves,
    /// then prisoners; priority 1): the expressions the version shown for them lacks (blink, smile, frown, pain), and a mark
    /// variant of their picture for each mark of theirs it has no version of (a lost or bionic eye and its side, an eye
    /// patch, a scar, a burn, a missing ear, an artificial arm). For each notable's picture: its expressions (priority 2).</item>
    /// <item>Written to the library folder's <c>variants-wanted.csv</c> on saving the game (as missing.csv is), on a worker
    /// thread: the list as it is now, never a version the library has (records.xml read again just before, so one made in
    /// the meantime is done), the date each was first wanted kept from the file; written safely and tried again a few times
    /// (a cloud drive, or Excel, may hold the file); left as it was where nothing changed.</item>
    /// </list>
    /// A variant deleted for good in the game is gone from the library, so it is wanted again while someone still needs it.
    /// </summary>
    public static class PortraitVariants
    {
        private static bool writing, told;

        /// <summary>Whose a wish is: 0 a colonist, 1 a slave, 2 a prisoner (the colony's own, priority 1), 3 a notable (priority 2).</summary>
        public const int Colonist = 0, Slave = 1, Prisoner = 2, Notable = 3;

        /// <summary>A person's rank for the list: a colonist, a slave or a prisoner of the colony's; a notable; -1 anyone else.</summary>
        public static int RankOf(Pawn p, bool notable)
        {
            if (p == null) return -1;
            if (p.IsPrisonerOfColony) return Prisoner;
            if (p.Faction != null && p.Faction == Faction.OfPlayerSilentFail && !p.IsQuestLodger()) return p.IsSlaveOfColony ? Slave : Colonist;
            return notable ? Notable : -1;
        }

        private static string RankWord(int rank) =>
            (rank == Colonist ? "CC_PortraitWanted_Colonist" : rank == Slave ? "CC_PortraitWanted_Slave" : rank == Prisoner ? "CC_PortraitWanted_Prisoner" : "CC_PortraitWanted_Notable")
            .Translate().Resolve();

        /// <summary>
        /// What one person wants of the picture they keep (none where it isn't one of the library folder's): the expressions
        /// of the version shown for them (an artificial arm's variant takes its picture's), and for the colony's own the mark
        /// variants of their picture they need; each with its instruction, their name and rank, and its priority.
        /// </summary>
        public static List<VariantWanted> WantedFor(Pawn p, int rank)
        {
            var list = new List<VariantWanted>();
            var lib = PortraitLibrary.Source;
            if (p == null || rank < 0 || !lib.KeptFor(p, out int root, out int shown)) return list;
            var rootRec = lib.RecordAt(root);
            var rootFile = PortraitFolder.PersonNamed(rootRec?.name);
            if (rootFile == null) return list;
            string who = p.LabelShortCap + " (" + RankWord(rank) + ")";
            int priority = rank == Notable ? 2 : 1;
            var shownRec = lib.RecordAt(shown) ?? rootRec;
            // An artificial arm leaves the head as it was: its picture's expressions show over it until it has its own.
            string facesOf = shownRec.IsMarkVariant && PortraitLibraryMath.HeadUnchanged(shownRec.variant) && lib.ExpressionNamesOf(shownRec.name).Count == 0 ? rootRec.name : shownRec.name;
            var facesFile = PortraitFolder.RecordNamed(facesOf);
            if (facesFile != null)
                foreach (var w in PortraitVariantsMath.ExpressionsWanted(lib.ExpressionNamesOf(facesOf).Keys.ToList()))
                    list.Add(Row(facesFile, w, who, priority, rank));
            if (rank == Notable) return list;
            var marks = PortraitReader.MarksOf(p);
            var variants = lib.VariantsOf(root).Select(lib.RecordAt).Where(r => r != null).ToList();
            foreach (var w in PortraitVariantsMath.MarksWanted(marks, rootRec, variants))
                list.Add(Row(rootFile, w, who, priority, rank));
            return list;
        }

        private static VariantWanted Row(FolderPortrait fp, string word, string who, int priority, int rank) => new VariantWanted
        {
            picture = PortraitFolderMath.FileKey(fp.texPath), word = word, instruction = PortraitVariantsMath.Instruction(word, fp.record.era) ?? "", who = who, priority = priority,
            record = fp.record.name, rank = rank
        };

        /// <summary>Everything wanted now: the colony's own people's (colonists, slaves, prisoners) and the notables' who keep a picture.</summary>
        public static List<VariantWanted> AllWanted()
        {
            var list = new List<VariantWanted>();
            var keeper = PortraitKeeper.Instance;
            if (keeper == null || PortraitFolder.Folder.Length == 0 || PortraitLibrary.Source.Count == 0) return list;
            var seen = new HashSet<int>();
            void Take(Pawn p, bool notable)
            {
                if (p?.RaceProps == null || !p.RaceProps.Humanlike || !seen.Add(p.thingIDNumber)) return;
                list.AddRange(WantedFor(p, RankOf(p, notable)));
            }
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction) Take(p, false);
            foreach (var p in PawnsFinder.AllMaps_PrisonersOfColony) Take(p, false);
            var notables = new HashSet<int>(keeper.Notables.Where(k => !k.picture.NullOrEmpty()).Select(k => k.id));
            notables.ExceptWith(seen);
            if (notables.Count > 0)
                foreach (var p in PawnsFinder.All_AliveOrDead)
                    if (p != null && !p.Dead && notables.Remove(p.thingIDNumber))
                    {
                        Take(p, true);
                        if (notables.Count == 0) break;
                    }
            return list;
        }

        /// <summary>
        /// On saving the game: the list written to the library folder's variants-wanted.csv on a worker thread, records.xml read
        /// again there so a version made in the meantime is left out, the dates first wanted kept; tried again a few times, and
        /// at the next save where it can't be written now.
        /// </summary>
        public static void Write()
        {
            if (writing || PortraitFolder.Folder.Length == 0) return;
            List<VariantWanted> fresh;
            try
            {
                fresh = AllWanted();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: the variants the game wants", e);
                return;
            }
            string folder = PortraitFolder.Folder, today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            writing = true;
            PortraitStore.Run(() => WriteList(folder, fresh, today, ms => Thread.Sleep(ms)), (rows, ex) =>
            {
                writing = false;
                if (ex == null || told) return;
                told = true;
                CouncilLog.WarnOnce("Portraits: " + PortraitVariantsMath.ListFile + " in the library folder could not be written (" + ex.Message + "); tried again at the next save.");
            }, false);
        }

        /// <summary>On a worker thread: what the library has read from records.xml now, the old list for its dates, the new list written where it changed. Returns its rows.</summary>
        private static int WriteList(string folder, List<VariantWanted> fresh, string today, Action<int> wait)
        {
            string path = Path.Combine(folder, PortraitVariantsMath.ListFile), recordsPath = Path.Combine(folder, PortraitDeleteMath.RecordsFile);
            string oldText = "";
            HashSet<string> made = null;
            if (!PortraitDeleteMath.Attempt(() =>
                {
                    oldText = File.Exists(path) ? PortraitDeleteMath.TextOf(File.ReadAllBytes(path)) ?? "" : "";
                    if (File.Exists(recordsPath))
                    {
                        var people = new List<FolderPortrait>();
                        PortraitFolderMath.Parse(File.ReadAllText(recordsPath), people, null, null);
                        made = PortraitVariantsMath.Made(people.Select(x => x.record));
                    }
                }, PortraitDeleteMath.Tries, wait, out var readErr))
                throw readErr ?? new IOException("not readable");
            var rows = PortraitVariantsMath.Rows(fresh, PortraitVariantsMath.Read(oldText), today, made);
            string text = PortraitVariantsMath.Csv(rows);
            if (text == oldText.TrimStart('﻿')) return rows.Count;
            if (!PortraitDeleteMath.Attempt(() => PortraitDeleteMath.WriteSafely(path, text, true), PortraitDeleteMath.Tries, wait, out var writeErr))
                throw writeErr ?? new IOException("not writable");
            return rows.Count;
        }

        [DebugAction("Rimfolk", "Portraits: the variants the game wants (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugWanted()
        {
            var rows = PortraitVariantsMath.Rows(AllWanted(), null, DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), null);
            Log.Message("[Rimfolk] Portraits: the variants the game wants now (" + rows.Count + "; written to " + PortraitVariantsMath.ListFile + " in the library folder when the game is saved"
                        + (PortraitFolder.Folder.Length == 0 ? ", but no library folder is set" : "") + "):\n" + PortraitVariantsMath.Csv(rows));
            Messages.Message("Portraits: the variants the game wants (" + rows.Count + ") are in the log.", MessageTypeDefOf.SilentInput, false);
        }
    }
}
