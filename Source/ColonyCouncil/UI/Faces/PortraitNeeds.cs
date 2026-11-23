using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What the game needed and the portrait library didn't have, for the next batches (docs: DESIGN.md, "Portraits, phase
    /// 4" and "Portraits, phase 5"; the player, 22 November 2026: "a constantly evolving database of images", and "Maybe the
    /// mod can also then log in the log files or in the save, ideally, because that I regularly provide to you"). Kept in
    /// the save (<see cref="PortraitNeedsKept"/>) whether or not a library folder is set, and written to the folder's
    /// <c>missing.csv</c> while one is.
    /// <list type="bullet">
    /// <item>People: no picture of their kind, none of their sex and stage, or none close to their looks (their kind, sex,
    /// age, work, skin, hair, the age of their clothes).</item>
    /// <item>Looks (phase 5): a look of theirs no picture of their kind, sex and age shows, special or ordinary.</item>
    /// <item>Animals and creatures (selected at the inspect pane): no picture of their kind, or none of their age and sex.</item>
    /// <item>Rooms: no backdrop of their kind, or none of the colony's age; the land outdoors: no backdrop of its own group;
    /// from where the person at the inspect pane is, and once a day from every room with a kind on the colony's maps and
    /// their land.</item>
    /// <item>Each person, room or map counted once a session for each need. Noted while the painted library is on and has
    /// anything (pictures or backdrops) or a folder is set.</item>
    /// <item>The file written on saving the game and at most once a day, on a worker thread, merged with what it holds (one
    /// line per need, the counts added, the newest date kept); a write that fails is tried again the next time.</item>
    /// </list>
    /// </summary>
    public static class PortraitNeeds
    {
        private static readonly Dictionary<string, PortraitNeed> pending = new Dictionary<string, PortraitNeed>();
        private static readonly Dictionary<string, HashSet<int>> counted = new Dictionary<string, HashSet<int>>();
        private static Game countedFor;
        private static int lastDay = -1;
        private static bool writing;

        /// <summary>Noting what is missing: the painted library is on and has pictures or backdrops, or a folder is set; and a game is on.</summary>
        public static bool On =>
            Current.Game != null && (ColonyCouncilMod.Settings?.portraitLibrary ?? true)
                                 && (PortraitFolder.Folder.Length > 0 || PortraitLibrary.Source.Count > 0 || Backdrops.Count > 0);

        /// <summary>Writing missing.csv: a library folder is set.</summary>
        public static bool Writes => PortraitFolder.Folder.Length > 0;

        private static string Today => DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static int GameDay => (Find.TickManager?.TicksGame ?? 0) / GenDate.TicksPerDay;

        private static void Note(PortraitNeed n, int who)
        {
            // Each person, room or map once a session for each need; the game's ids start again with another game.
            if (countedFor != Current.Game)
            {
                countedFor = Current.Game;
                counted.Clear();
            }
            string key = n.Key;
            if (!counted.TryGetValue(key, out var ids))
            {
                ids = new HashSet<int>();
                counted[key] = ids;
            }
            if (!ids.Add(who)) return;
            n.count = 1;
            n.day = GameDay;
            PortraitNeedsKept.Instance?.Add(n.Copy());
            if (pending.TryGetValue(key, out var had))
            {
                had.count++;
                had.lastSeen = n.lastSeen;
                had.day = n.day;
                if (n.instead.Length > 0) had.instead = n.instead;
                return;
            }
            pending[key] = n;
        }

        /// <summary>A person's kind as the list writes it (race/xenotype, "custom" for one of the player's own making), and the def whose mod it is.</summary>
        private static string KindOf(Pawn p, out Def from)
        {
            var xeno = p.genes?.Xenotype;
            string xenotype = p.genes == null ? "Baseliner" : p.genes.UniqueXenotype ? "custom" : xeno?.defName ?? "Baseliner";
            from = xeno != null && xeno != XenotypeDefOf.Baseliner ? xeno : p.def;
            return p.def.defName + "/" + xenotype;
        }

        /// <summary>A person the library had no fitting picture for (<paramref name="why"/> a key of the reason), and what they got instead.</summary>
        public static void NotePerson(Pawn p, PortraitLook look, string why, string instead)
        {
            if (!On || p == null || look == null) return;
            string kind = KindOf(p, out var from);
            Note(new PortraitNeed
            {
                what = "person", kind = kind, role = look.role, mod = from?.modContentPack?.PackageIdPlayerFacing ?? "", sex = PortraitLibraryMath.Word(look.sex),
                age = PortraitLibraryMath.Word(look.age), skin = PortraitFolderMath.SkinWord(look.skin), hair = look.hair == PortraitHairStyle.Bald ? "bald" : PortraitFolderMath.HairWord(look.hairColour),
                era = look.era.ToString().ToLowerInvariant(), why = why.Translate().Resolve(), instead = instead.NullOrEmpty() ? "CC_PortraitNeed_GameFace".Translate().Resolve() : instead,
                lastSeen = Today
            }, p.thingIDNumber);
        }

        /// <summary>
        /// A look of a person's no picture of their kind, sex and age shows (phase 5): its word, their kind, sex and age band,
        /// the mod of what gave it (the trait, gene, hediff or body), special or not, and what they have instead.
        /// </summary>
        public static void NoteLook(Pawn p, PortraitLook look, string word, bool special, string instead)
        {
            if (!On || p == null || look == null || word.NullOrEmpty()) return;
            string kind = KindOf(p, out _);
            Note(new PortraitNeed
            {
                what = "look", kind = kind, look = word, mod = PortraitLooks.ModOf(p, word), sex = PortraitLibraryMath.Word(look.sex), age = PortraitLibraryMath.Word(look.age),
                why = (special ? "CC_PortraitNeed_NoSpecialLook" : "CC_PortraitNeed_NoLook").Translate().Resolve(),
                instead = instead.NullOrEmpty() ? "CC_PortraitNeed_GameFace".Translate().Resolve() : instead, lastSeen = Today
            }, p.thingIDNumber);
        }

        /// <summary>
        /// An animal or creature shown at the inspect pane with no painted picture of its kind (or none of its age and sex):
        /// its race, the mod it comes from, its sex where it has one and its age (baby, juvenile, adult), for the creature lists.
        /// </summary>
        public static void NoteCreature(Pawn p, string age, string sex, string why)
        {
            if (!On || p?.def == null) return;
            Note(new PortraitNeed
            {
                what = "creature", kind = p.def.defName, mod = p.def.modContentPack?.PackageIdPlayerFacing ?? "", sex = sex ?? "", age = age ?? "",
                why = why.Translate().Resolve(), instead = "CC_PortraitNeed_NoPortrait".Translate().Resolve(), lastSeen = Today
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
            if (Current.ProgramState != ProgramState.Playing) return;
            int day = GameDay;
            if (day == lastDay) return;
            bool first = lastDay < 0;
            lastDay = day;
            if (first || !On) return;
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
            if (!Writes || writing || pending.Count == 0) return;
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

        [DebugAction("Rimfolk", "Portraits: what the library lacks (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugLacks()
        {
            var kept = PortraitNeedsKept.Instance;
            var list = kept?.Needs ?? new List<PortraitNeed>();
            Log.Message("[Rimfolk] Portraits: what the library lacks, as kept in this save (" + list.Count + " lines, at most " + PortraitNeedsKept.MostLines
                        + ", the least recently seen let go; " + (Writes ? "also written to missing.csv in " + PortraitFolder.Folder : "no library folder set, so in the save only")
                        + "; day is the game's day last seen):\n" + PortraitFolderMath.Csv(list, true));
            Messages.Message("Portraits: what the library lacks (" + list.Count + " lines) is in the log.", MessageTypeDefOf.SilentInput, false);
        }
    }

    /// <summary>
    /// What the portrait library lacks, kept in the save (docs: DESIGN.md, "Portraits, phase 5"; the player, 22 November
    /// 2026: "ideally in the save ... So we always have like ideas where the image library could be enhanced even
    /// further"): one line per need (missing.csv's columns as a CSV line, with the game's day last seen), the counts added,
    /// at most <see cref="MostLines"/>, the least recently seen let go. Saved with its columns, so lines read back by name
    /// whatever columns a later version adds; on load, lines of the same need are merged (counts added), nothing lost.
    /// </summary>
    public class PortraitNeedsKept : GameComponent
    {
        public const int MostLines = 200;

        private string columns = "";
        private List<string> lines = new List<string>();

        [Unsaved] private List<PortraitNeed> needs = new List<PortraitNeed>();
        [Unsaved] private readonly Dictionary<string, PortraitNeed> byKey = new Dictionary<string, PortraitNeed>();

        private static PortraitNeedsKept cached;
        private static Game cachedGame;

        public PortraitNeedsKept(Game game)
        {
        }

        public static PortraitNeedsKept Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<PortraitNeedsKept>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        /// <summary>The lines in the list's order (people, looks, rooms, the land; the commonest first).</summary>
        public List<PortraitNeed> Needs => PortraitFolderMath.Order(needs);

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                columns = PortraitFolderMath.SaveColumnsLine;
                lines = PortraitFolderMath.Order(needs).Select(n => PortraitFolderMath.Line(n, true)).ToList();
            }
            Scribe_Values.Look(ref columns, "columns", "");
            Scribe_Collections.Look(ref lines, "lines", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                lines ??= new List<string>();
                // The save's lines and anything noted before they were read, the same needs merged: nothing lost.
                var merged = PortraitFolderMath.Merge(PortraitFolderMath.ReadLines(columns, lines), needs);
                needs = PortraitFolderMath.Keep(merged, MostLines);
                Index();
            }
            if (Scribe.mode == LoadSaveMode.Saving) lines = new List<string>();
        }

        private void Index()
        {
            byKey.Clear();
            foreach (var n in needs) byKey[n.Key] = n;
        }

        /// <summary>A need seen (counted once a person, room or map a session by the caller): its line's count added and its dates the newest; the least recently seen let go past the cap.</summary>
        public void Add(PortraitNeed n)
        {
            if (n == null) return;
            if (byKey.TryGetValue(n.Key, out var had))
            {
                had.count += n.count;
                if (n.day >= had.day)
                {
                    had.day = n.day;
                    had.lastSeen = n.lastSeen;
                    if (!n.mod.NullOrEmpty()) had.mod = n.mod;
                    if (!n.instead.NullOrEmpty()) had.instead = n.instead;
                }
                return;
            }
            needs.Add(n);
            byKey[n.Key] = n;
            if (needs.Count > MostLines)
            {
                needs = PortraitFolderMath.Keep(needs, MostLines);
                Index();
            }
        }
    }
}
