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
    /// What the game sees in each person and needs for them, written to the library folder's <c>people.csv</c> at every save
    /// (the player, 25 November 2026: "the game should provide details of which variants it needs ... after the save is
    /// created and the image is assigned, details like this should be added to the logs and read by studio, so that specific
    /// versions can be created for this ... but also whenever the pawn develops or changes somehow in the game"): one line
    /// for each of the colony's people (colonists, slaves, prisoners) and each notable, with their number and name, their
    /// standing, the picture they hold and how far it is from them, the game's words for them, what the picture does not
    /// show, and what changed since a save before with the date first seen (<see cref="PortraitPeopleMath"/>). On a worker
    /// thread, written safely and tried again a few times; left as it was where nothing changed. Rimfolk Studio reads it.
    /// </summary>
    public static class PortraitPeople
    {
        private static bool writing, told;

        /// <summary>Their titles: the Empire's (in its own words for them) and the office they hold in the council.</summary>
        public static List<string> HonoursOf(Pawn p)
        {
            var list = new List<string>();
            if (p == null) return list;
            try
            {
                var titles = p.royalty?.AllTitlesForReading;
                if (titles != null)
                    foreach (var t in titles.Where(x => x?.def != null).OrderByDescending(x => x.def.seniority))
                    {
                        string label = t.def.GetLabelFor(p);
                        if (!label.NullOrEmpty()) list.Add(label.ToLowerInvariant());
                    }
                var office = OfficeTracker.HeldBy(p);
                if (office != null && !office.label.NullOrEmpty()) list.Add(office.label.ToLowerInvariant());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: a person's titles, for the people list", e);
            }
            return list.Distinct().ToList();
        }

        /// <summary>One person's line (null where the game can't read them as a person).</summary>
        public static PersonSeen Of(Pawn p, int rank)
        {
            if (p == null || rank < 0 || !PortraitReader.LookOf(p, out var look)) return null;
            var n = new PersonSeen
            {
                id = p.thingIDNumber.ToString(CultureInfo.InvariantCulture), name = p.Name?.ToStringShort ?? p.LabelShort, fullName = p.Name?.ToStringFull ?? p.LabelShort,
                standing = PortraitPeopleMath.Standings[Math.Min(rank, PortraitPeopleMath.Standings.Length - 1)], rank = rank, words = look.Words,
                age = PortraitLibraryMath.Word(look.age), role = look.role,
                looks = string.Join(PortraitPeopleMath.Sep, look.looks.OrderBy(w => w, StringComparer.OrdinalIgnoreCase)),
                marks = string.Join(PortraitPeopleMath.Sep, PortraitPeopleMath.MarksOf(look)),
                title = string.Join(PortraitPeopleMath.Sep, HonoursOf(p))
            };
            PortraitRecord rootRec = null, shownRec = null;
            var lib = PortraitLibrary.Source;
            if (lib.KeptFor(p, out int root, out int shown))
            {
                rootRec = lib.RecordAt(root);
                shownRec = lib.RecordAt(shown) ?? rootRec;
                if (rootRec != null)
                {
                    n.record = rootRec.name;
                    var file = PortraitFolder.RecordNamed(rootRec.name);
                    n.picture = file != null ? PortraitFolderMath.FileKey(file.texPath) : "";
                    if (shownRec != rootRec)
                    {
                        var shownFile = PortraitFolder.RecordNamed(shownRec.name);
                        n.shown = shownFile != null ? PortraitFolderMath.FileKey(shownFile.texPath) : shownRec.name;
                    }
                    n.distance = PortraitPeopleMath.DistanceWord(PortraitLibraryMath.Distance(look, shownRec));
                }
            }
            n.lacks = string.Join(PortraitPeopleMath.Sep, PortraitPeopleMath.Lacks(look, rootRec, shownRec));
            return n;
        }

        /// <summary>Everyone listed now: the colony's own people (colonists, slaves, prisoners), then the notables still alive.</summary>
        public static List<PersonSeen> All()
        {
            var list = new List<PersonSeen>();
            var keeper = PortraitKeeper.Instance;
            if (keeper == null) return list;
            var seen = new HashSet<int>();
            void Take(Pawn p, bool notable)
            {
                if (p?.RaceProps == null || !p.RaceProps.Humanlike || !seen.Add(p.thingIDNumber)) return;
                var line = Of(p, PortraitVariants.RankOf(p, notable));
                if (line != null) list.Add(line);
            }
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction) Take(p, false);
            foreach (var p in PawnsFinder.AllMaps_PrisonersOfColony) Take(p, false);
            var notables = new HashSet<int>(keeper.Notables.Select(k => k.id));
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

        /// <summary>On saving the game: the list written to the library folder's people.csv on a worker thread, what changed worked out against the file as it was.</summary>
        public static void Write()
        {
            if (writing || PortraitFolder.Folder.Length == 0) return;
            List<PersonSeen> fresh;
            try
            {
                fresh = All();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: the people list", e);
                return;
            }
            string folder = PortraitFolder.Folder, today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            writing = true;
            PortraitStore.Run(() => WriteList(folder, fresh, today, ms => Thread.Sleep(ms)), (rows, ex) =>
            {
                writing = false;
                if (ex == null || told) return;
                told = true;
                CouncilLog.WarnOnce("Portraits: " + PortraitPeopleMath.ListFile + " in the library folder could not be written (" + ex.Message + "); tried again at the next save.");
            }, false);
        }

        /// <summary>On a worker thread: the old list read for what changed and the dates, the new one written where it differs. Returns its lines.</summary>
        private static int WriteList(string folder, List<PersonSeen> fresh, string today, Action<int> wait)
        {
            string path = Path.Combine(folder, PortraitPeopleMath.ListFile);
            string oldText = "";
            if (!PortraitDeleteMath.Attempt(() => { oldText = File.Exists(path) ? PortraitDeleteMath.TextOf(File.ReadAllBytes(path)) ?? "" : ""; },
                    PortraitDeleteMath.Tries, wait, out var readErr))
                throw readErr ?? new IOException("not readable");
            var rows = PortraitPeopleMath.Rows(fresh, PortraitPeopleMath.Read(oldText), today);
            string text = PortraitPeopleMath.Csv(rows);
            if (text == oldText.TrimStart('﻿')) return rows.Count;
            if (!PortraitDeleteMath.Attempt(() => PortraitDeleteMath.WriteSafely(path, text, true), PortraitDeleteMath.Tries, wait, out var writeErr))
                throw writeErr ?? new IOException("not writable");
            return rows.Count;
        }

        [DebugAction("Rimfolk", "Portraits: the people list (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugPeople()
        {
            var rows = PortraitPeopleMath.Rows(All(), null, DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Log.Message("[Rimfolk] Portraits: the people list now (" + rows.Count + "; written to " + PortraitPeopleMath.ListFile + " in the library folder when the game is saved"
                        + (PortraitFolder.Folder.Length == 0 ? ", but no library folder is set" : "") + "):\n" + PortraitPeopleMath.Csv(rows));
            Messages.Message("Portraits: the people list (" + rows.Count + ") is in the log.", MessageTypeDefOf.SilentInput, false);
        }
    }
}
