using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What the game needed and the portrait library didn't have, written to the library folder's <c>missing.csv</c> for the
    /// next batches (docs: DESIGN.md, "Portraits, phase 4"; the player, 22 November 2026: "after we created the first batch,
    /// I will probably realize, oh, there's a room that's not yet represented ... include what is missing ... a constantly
    /// evolving database of images"). Only while a library folder is set.
    /// <list type="bullet">
    /// <item>People: no picture of their kind, none of their sex and stage, or none close to their looks (their kind, sex,
    /// age, skin, hair, the age of their clothes); counted once a person a session.</item>
    /// <item>Rooms: no backdrop of their kind, or none of the colony's age; the land outdoors: no backdrop of its own group;
    /// from where the person at the inspect pane is, and once a day from every room with a kind on the colony's maps and
    /// their land.</item>
    /// <item>Written on saving the game and at most once a day, on a worker thread, merged with what the file holds (one
    /// line per need, the counts added, the newest date kept); a write that fails is tried again the next time.</item>
    /// </list>
    /// </summary>
    public static class PortraitNeeds
    {
        private static readonly Dictionary<string, PortraitNeed> pending = new Dictionary<string, PortraitNeed>();
        private static readonly Dictionary<string, HashSet<int>> counted = new Dictionary<string, HashSet<int>>();
        private static int lastDay = -1;
        private static bool writing;

        public static bool On => PortraitFolder.Folder.Length > 0;

        private static string Today => DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static void Note(PortraitNeed n, int who)
        {
            string key = n.Key;
            if (!counted.TryGetValue(key, out var ids))
            {
                ids = new HashSet<int>();
                counted[key] = ids;
            }
            if (!ids.Add(who)) return;
            if (pending.TryGetValue(key, out var had))
            {
                had.count++;
                had.lastSeen = n.lastSeen;
                if (n.instead.Length > 0) had.instead = n.instead;
                return;
            }
            n.count = 1;
            pending[key] = n;
        }

        /// <summary>A person the library had no fitting picture for (<paramref name="why"/> a key of the reason), and what they got instead.</summary>
        public static void NotePerson(Pawn p, PortraitLook look, string why, string instead)
        {
            if (!On || p == null || look == null) return;
            var xeno = p.genes?.Xenotype;
            string xenotype = p.genes == null ? "Baseliner" : p.genes.UniqueXenotype ? "custom" : xeno?.defName ?? "Baseliner";
            Def from = xeno != null && xeno != XenotypeDefOf.Baseliner ? xeno : p.def;
            Note(new PortraitNeed
            {
                what = "person", kind = p.def.defName + "/" + xenotype, mod = from?.modContentPack?.PackageIdPlayerFacing ?? "", sex = PortraitLibraryMath.Word(look.sex),
                age = PortraitLibraryMath.Word(look.age), skin = PortraitFolderMath.SkinWord(look.skin), hair = look.hair == PortraitHairStyle.Bald ? "bald" : PortraitFolderMath.HairWord(look.hairColour),
                era = look.era.ToString().ToLowerInvariant(), why = why.Translate().Resolve(), instead = instead.NullOrEmpty() ? "CC_PortraitNeed_GameFace".Translate().Resolve() : instead,
                lastSeen = Today
            }, p.thingIDNumber);
        }

        /// <summary>A room or the land with no backdrop of its own (kind or age), from where a person is and the backdrop chosen for it.</summary>
        public static void NotePlace(BackdropWhere w, BackdropChoice c, Def def, int who)
        {
            if (!On || w == null) return;
            if (w.place == BackdropPlace.Room && w.role.Length > 0 && (!c.ownKind || !c.ownEra))
                Note(new PortraitNeed
                {
                    what = "room", kind = w.role, mod = def?.modContentPack?.PackageIdPlayerFacing ?? "", era = w.era.ToString().ToLowerInvariant(),
                    why = (c.ownKind ? "CC_PortraitNeed_NoAge" : "CC_PortraitNeed_NoRoom").Translate().Resolve(), instead = c.used ?? "", lastSeen = Today
                }, who);
            else if (w.place == BackdropPlace.Outdoors && w.biome.Length > 0 && !c.ownKind)
                Note(new PortraitNeed
                {
                    what = "outdoors", kind = w.biome, mod = def?.modContentPack?.PackageIdPlayerFacing ?? "",
                    why = "CC_PortraitNeed_NoLand".Translate(w.group.Named("GROUP")).Resolve(), instead = c.used ?? "", lastSeen = Today
                }, who);
        }

        /// <summary>Once a frame from the store's pump: once a day of the game, every room with a kind on the colony's maps and their land looked at, and the list written.</summary>
        public static void Pump()
        {
            if (!On || Current.ProgramState != ProgramState.Playing) return;
            int day = (Find.TickManager?.TicksGame ?? 0) / GenDate.TicksPerDay;
            if (day == lastDay) return;
            bool first = lastDay < 0;
            lastDay = day;
            if (first) return;
            try
            {
                LookAtTheColony();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: looking at the colony's rooms for the missing list", e);
            }
            Write();
        }

        /// <summary>Every room with a kind on the colony's maps, and each map's land, through the backdrops' choice.</summary>
        private static void LookAtTheColony()
        {
            foreach (var map in Find.Maps)
            {
                if (map == null || !map.IsPlayerHome) continue;
                var land = Backdrops.WhereOfLand(map);
                Backdrops.Note(land, map.uniqueID);
                foreach (var room in map.regionGrid.AllRooms.ToList())
                {
                    if (room == null || room.PsychologicallyOutdoors || room.Fogged) continue;
                    var w = Backdrops.WhereOfRoom(room, map);
                    if (w.role.Length > 0) Backdrops.Note(w, room.ID);
                }
            }
        }

        /// <summary>The list written to missing.csv on a worker thread (the file read, merged and written again); kept to try again if it fails.</summary>
        public static void Write()
        {
            if (!On || writing || pending.Count == 0) return;
            string path = Path.Combine(PortraitFolder.Folder, "missing.csv");
            var fresh = pending.Values.ToList();
            pending.Clear();
            writing = true;
            PortraitStore.Run(() =>
            {
                string old = File.Exists(path) ? File.ReadAllText(path) : "";
                var merged = PortraitFolderMath.Merge(PortraitFolderMath.Read(old), fresh);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, PortraitFolderMath.Csv(merged));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                return merged.Count;
            }, (lines, ex) =>
            {
                writing = false;
                if (ex == null) return;
                // Kept for the next time (a cloud drive busy with the file, the folder offline).
                foreach (var n in fresh)
                    if (pending.TryGetValue(n.Key, out var had)) had.count += n.count;
                    else pending[n.Key] = n;
                CouncilLog.WarnOnce("Portraits: missing.csv in the library folder could not be written (" + ex.Message + "); tried again later.");
            }, false);
        }
    }
}
