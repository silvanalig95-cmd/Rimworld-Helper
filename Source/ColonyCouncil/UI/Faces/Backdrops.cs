using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The painted backdrops behind the large portrait (docs: DESIGN.md, "Portraits, phase 4"; the player, 22 November 2026:
    /// "the backgrounds are correctly selected based on age and what it contains and what kind of room it is"): every
    /// <see cref="BackdropDef"/> of any mod and every backdrop of the library folder's records.xml, where a person is in the
    /// backdrops' words (<see cref="WhereOf"/>), and the backdrop for it (<see cref="BackdropMath.Choose"/>). Read only for
    /// the person at the inspect pane, at most every couple of seconds; what is in a room is read when the room changes
    /// and every half minute.
    /// </summary>
    public static class Backdrops
    {
        private static readonly List<BackdropRecord> records = new List<BackdropRecord>();
        private static readonly List<PictureFile> files = new List<PictureFile>();
        private static List<KeyValuePair<BackdropRecord, PictureFile>> fromDefs;
        private static int loadedVersion = -1;

        private sealed class RoomThings
        {
            public HashSet<string> words;
            public float until;
        }

        private static readonly Dictionary<int, RoomThings> rooms = new Dictionary<int, RoomThings>();
        private static readonly Dictionary<ThingDef, List<string>> thingWords = new Dictionary<ThingDef, List<string>>();

        /// <summary>How long what is in a room is kept before it is read again, in seconds; the most things read in one room.</summary>
        private const float RoomFor = 30f;

        private const int ThingsMost = 600;

        public static int Count
        {
            get
            {
                Load();
                return records.Count;
            }
        }

        /// <summary>Every mod's backdrop defs (those whose file is there; read once a session) and the library folder's backdrops (again whenever the folder is read).</summary>
        private static void Load()
        {
            if (loadedVersion == PortraitFolder.Version) return;
            loadedVersion = PortraitFolder.Version;
            try
            {
                if (fromDefs == null)
                {
                    fromDefs = new List<KeyValuePair<BackdropRecord, PictureFile>>();
                    foreach (var d in DefDatabase<BackdropDef>.AllDefsListForReading.OrderBy(x => x.defName, StringComparer.Ordinal))
                    {
                        var f = d.File;
                        if (f != null) fromDefs.Add(new KeyValuePair<BackdropRecord, PictureFile>(d.Record, f));
                    }
                }
                records.Clear();
                files.Clear();
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var kv in fromDefs)
                    if (names.Add(kv.Key.name))
                    {
                        records.Add(kv.Key);
                        files.Add(kv.Value);
                    }
                foreach (var b in PortraitFolder.Backdrops.OrderBy(x => x.record.name, StringComparer.Ordinal))
                    if (names.Add(b.record.name))
                    {
                        records.Add(b.record);
                        files.Add(PortraitFolder.FileOf(b.record.name, b.texPath, false));
                    }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: reading the backdrops", e);
            }
        }

        /// <summary>The backdrop's file for where a person is (null for none: the portrait's own ground), and the choice with why.</summary>
        public static PictureFile For(BackdropWhere w, out BackdropChoice choice)
        {
            Load();
            choice = BackdropMath.Choose(w, records);
            return choice.index >= 0 ? files[choice.index] : null;
        }

        /// <summary>For the library folder's missing list: a room or the land with no backdrop of its own, by the choice for it.</summary>
        public static void Note(BackdropWhere w, int who)
        {
            if (!PortraitNeeds.On || w == null) return;
            For(w, out var c);
            Def def = w.place == BackdropPlace.Room ? DefDatabase<RoomRoleDef>.GetNamedSilentFail(w.role) : DefDatabase<BiomeDef>.GetNamedSilentFail(w.biome);
            PortraitNeeds.NotePlace(w, c, def, who);
        }

        // ---------------- Where a person is ----------------

        /// <summary>The colony's age for its rooms: its own tech level (what it has researched, its people's), held to medieval where it lives before the industrial age (Rimfolk's era setting, no power).</summary>
        private static PortraitEra ColonyEra()
        {
            var tech = Avail.EffectiveTech;
            if (Avail.IsPreIndustrial && tech > TechLevel.Medieval) tech = TechLevel.Medieval;
            return PortraitLibraryMath.EraOf((int)tech);
        }

        /// <summary>
        /// Where a person is, in the backdrops' words, and the light where they stand (0 dark to 1 daylight): a room with a
        /// role (its role, its things, the colony's age on the player's maps, else the map's people's); a room of no
        /// particular kind (its things); outdoors (the biome's group of land; under a natural rock roof a cave); else nowhere.
        /// </summary>
        public static BackdropWhere WhereOf(Pawn p, out float glow)
        {
            glow = 1f;
            var map = p?.MapHeld;
            if (map == null) return new BackdropWhere();
            var pos = p.PositionHeld;
            if (!pos.InBounds(map)) return new BackdropWhere();
            glow = map.glowGrid.GroundGlowAt(pos);
            var room = pos.GetRoom(map);
            var roof = map.roofGrid.RoofAt(pos);
            bool rock = roof != null && roof.isNatural;
            if (room != null && !room.PsychologicallyOutdoors)
            {
                var w = WhereOfRoom(room, map);
                if (!(rock && w.role.Length == 0)) return w;
            }
            var land = WhereOfLand(map);
            if (rock) land.group = "cave";
            return land;
        }

        /// <summary>A room in the backdrops' words: its role ("" for none in particular), what is in it, the age.</summary>
        public static BackdropWhere WhereOfRoom(Room room, Map map)
        {
            var role = room.Role;
            var owner = map.ParentFaction;
            return new BackdropWhere
            {
                place = BackdropPlace.Room,
                role = role == null || role == RoomRoleDefOf.None || role.defName == "Room" ? "" : role.defName,
                seed = room.ID,
                era = map.IsPlayerHome || owner == null || owner == Faction.OfPlayerSilentFail ? ColonyEra() : PortraitLibraryMath.EraOf((int)Peoples.TechOf(owner)),
                contents = ThingsIn(room)
            };
        }

        /// <summary>A map's land in the backdrops' words: its biome's group (an unknown biome by its warmth, rain and plants).</summary>
        public static BackdropWhere WhereOfLand(Map map)
        {
            var biome = map.Biome;
            var tile = map.TileInfo;
            return new BackdropWhere
            {
                place = BackdropPlace.Outdoors,
                seed = map.uniqueID,
                biome = biome?.defName ?? "",
                group = BackdropMath.GroupOf(biome?.defName, tile?.temperature ?? float.NaN, tile?.rainfall ?? float.NaN, biome?.plantDensity ?? float.NaN, biome?.isExtremeBiome ?? false,
                    biome?.inVacuum ?? false, biome != null && biome.defName.IndexOf("cave", StringComparison.OrdinalIgnoreCase) >= 0)
            };
        }

        /// <summary>What is in a room in the backdrops' words (kept by the room's id a while).</summary>
        private static HashSet<string> ThingsIn(Room room)
        {
            float now = Time.realtimeSinceStartup;
            if (rooms.TryGetValue(room.ID, out var kept) && now < kept.until) return kept.words;
            var lists = new List<List<string>>();
            int n = 0;
            foreach (var t in room.ContainedAndAdjacentThings)
            {
                if (t?.def == null || ++n > ThingsMost) break;
                if (t.def.category != ThingCategory.Building && !t.def.IsMedicine && !(t is Book)) continue;
                lists.Add(WordsOf(t.def));
            }
            var words = BackdropMath.RoomContents(lists);
            if (rooms.Count > 256) rooms.Clear();
            rooms[room.ID] = new RoomThings { words = words, until = now + RoomFor };
            return words;
        }

        /// <summary>The words for a kind of thing, read once a def from what the game says of it (its class, its seat, its table, its fire).</summary>
        private static List<string> WordsOf(ThingDef d)
        {
            if (thingWords.TryGetValue(d, out var words)) return words;
            var f = BackdropMath.ThingIs.None;
            var cls = d.thingClass;
            bool Is(Type t) => cls != null && t.IsAssignableFrom(cls);
            if (Is(typeof(Building_Bed)) && (d.building?.bed_humanlike ?? false)) f |= BackdropMath.ThingIs.Bed;
            if (d.surfaceType == SurfaceType.Eat) f |= BackdropMath.ThingIs.Table;
            if (d.building?.isSittable ?? false) f |= BackdropMath.ThingIs.Seat;
            if (Is(typeof(Building_WorkTable))) f |= BackdropMath.ThingIs.WorkTable;
            if (Is(typeof(Building_ResearchBench))) f |= BackdropMath.ThingIs.Research | BackdropMath.ThingIs.WorkTable;
            if (Is(typeof(Building_Storage))) f |= BackdropMath.ThingIs.Storage;
            if (Is(typeof(Building_Throne))) f |= BackdropMath.ThingIs.Throne;
            if (d.building?.joyKind != null) f |= BackdropMath.ThingIs.Joy;
            if (d.IsMedicine) f |= BackdropMath.ThingIs.Medicine;
            if (Is(typeof(Book))) f |= BackdropMath.ThingIs.Book;
            if (d.comps != null && d.comps.Any(c => c is CompProperties_FireOverlay)) f |= BackdropMath.ThingIs.Fire;
            words = BackdropMath.ContentWordsOf(d.defName, f);
            thingWords[d] = words;
            return words;
        }

        /// <summary>For the log: where the person is and the backdrop they get, with why.</summary>
        public static string Explain(Pawn p)
        {
            var w = WhereOf(p, out float glow);
            var f = For(w, out var c);
            int step = PortraitPictureMath.LightStep(glow);
            return w + ", light " + glow.ToString("0.00") + " (step " + step + "): " + (f != null ? f.key : "none") + " (" + c.why + ")"
                   + (ColonyCouncilMod.Settings?.portraitBackdrop == PortraitBackdropMode.Plain ? "; the setting shows no backdrop" : "");
        }
    }
}
