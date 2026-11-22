using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>What a backdrop shows: a room, a look outdoors, or a plain painted ground.</summary>
    public enum BackdropPlace : byte
    {
        Room,
        Outdoors,
        Plain
    }

    /// <summary>What one backdrop shows, read from its def (<c>BackdropDef</c>): its place, the room roles it fits, its group of land outdoors, its age, and what is in it.</summary>
    public sealed class BackdropRecord
    {
        public string name = "";
        public BackdropPlace place = BackdropPlace.Plain;
        /// <summary>RoomRoleDef names (as written; compared without case).</summary>
        public List<string> rooms = new List<string>();
        /// <summary>For a look outdoors, its group of land (<see cref="BackdropMath.Groups"/>); "" otherwise.</summary>
        public string outdoors = "";
        /// <summary>Its age; unknown for any.</summary>
        public PortraitEra era;
        public HashSet<string> contents = new HashSet<string>();

        public override string ToString() => name;
    }

    /// <summary>Where a person is, in the backdrops' words: a room (its role, what is in it, the colony's age), outdoors (the land's group), or nowhere (plain).</summary>
    public sealed class BackdropWhere
    {
        public BackdropPlace place = BackdropPlace.Plain;
        /// <summary>The room's role (a RoomRoleDef name), or "" for a room of no particular kind.</summary>
        public string role = "";
        public HashSet<string> contents = new HashSet<string>();
        public PortraitEra era;
        public string group = "";
        /// <summary>A number for the place (the room's id, the map's), so the same place always gets the same backdrop.</summary>
        public int seed;

        /// <summary>What decides the backdrop, in words: when this changes the backdrop is chosen again.</summary>
        public string Key => place + "|" + role + "|" + group + "|" + era + "|" + seed + "|" + string.Join("+", contents.OrderBy(c => c, StringComparer.Ordinal));

        public override string ToString() =>
            place == BackdropPlace.Room ? "a room (" + (role.Length > 0 ? role : "of no particular kind") + ", " + era.ToString().ToLowerInvariant() + (contents.Count > 0 ? ", with " + string.Join(", ", contents.OrderBy(c => c, StringComparer.Ordinal)) : "") + ")"
            : place == BackdropPlace.Outdoors ? "outdoors (" + group + ")" : "no map";
    }

    /// <summary>A backdrop chosen (an index, -1 for none: the portrait's own ground) and why, for the log.</summary>
    public struct BackdropChoice
    {
        public int index;
        public string why;
    }

    /// <summary>
    /// Which backdrop stands behind the large portrait (docs: DESIGN.md, "Portraits, phase 4"), without game types
    /// (Tools/Tests try it):
    /// <list type="bullet">
    /// <item>In a room with a role: the backdrops of that role, else of the nearest role by a short table (a guest room as
    /// a bedroom, a prison barracks as a prison cell, a library as a laboratory); then the age nearest the colony's (a
    /// backdrop for any age a little behind its own); then the most of what is in the room; among equals one by the room's
    /// number, so it never flickers.</item>
    /// <item>In a room of no particular kind: the room backdrop with the most of what is in it (at least one thing), then
    /// the age; else plain.</item>
    /// <item>Outdoors: the map's biome by a table into sixteen groups of land (an unknown biome by its warmth, rain and
    /// plants), under a rock roof a cave; the group's backdrops, else a near group's; any age.</item>
    /// <item>No map (a caravan, the world), or nothing fits: a plain painted ground; with none of those, the portrait's own
    /// ground.</item>
    /// </list>
    /// </summary>
    public static class BackdropMath
    {
        // ---------------- Words ----------------

        /// <summary>The groups of land a look outdoors belongs to.</summary>
        public static readonly string[] Groups =
        {
            "temperateForest", "borealForest", "tundra", "ice", "grassland", "shrubland", "desert", "jungle", "swamp", "coast", "wasteland", "volcanic", "glowforest",
            "cave", "space", "mountain"
        };

        /// <summary>The words for what is in a room.</summary>
        public static readonly string[] Contents =
        {
            "bed", "beds", "table", "chairs", "hearth", "fire", "window", "shelves", "crates", "altar", "throne", "banners", "pillars", "workbench", "tools", "stove", "pots",
            "books", "medicine", "bars", "games"
        };

        private static string Known(string[] words, string w)
        {
            if (string.IsNullOrWhiteSpace(w)) return null;
            foreach (var k in words)
                if (string.Equals(k, w.Trim(), StringComparison.OrdinalIgnoreCase))
                    return k;
            return null;
        }

        /// <summary>A backdrop's record from its def's fields, with what is wrong with it (an unknown place, group, age or content word; a missing texPath).</summary>
        public static BackdropRecord Record(string name, Func<string, string> field, Func<string, IList<string>> list, List<string> problems)
        {
            var r = new BackdropRecord { name = name ?? "" };
            void Bad(string what) => problems?.Add(what);
            if (string.IsNullOrWhiteSpace(field("texPath"))) Bad("no texPath: the picture under the mod's Portraits folder, without .png");
            string place = (field("place") ?? "").Trim().ToLowerInvariant();
            if (place == "room" || place == "indoors") r.place = BackdropPlace.Room;
            else if (place == "outdoors" || place == "outside") r.place = BackdropPlace.Outdoors;
            else if (place == "plain" || place.Length == 0)
            {
                r.place = BackdropPlace.Plain;
                if (place.Length == 0) Bad("place: room, outdoors or plain is needed");
            }
            else Bad("place: '" + place + "' is not room, outdoors or plain");
            foreach (var x in list("rooms") ?? new string[0])
                if (!string.IsNullOrWhiteSpace(x))
                    r.rooms.Add(x.Trim());
            if (r.place == BackdropPlace.Room && r.rooms.Count == 0) Bad("rooms: a room backdrop names the room roles it fits (Bedroom, Barracks, ...)");
            string group = field("outdoors");
            if (!string.IsNullOrWhiteSpace(group))
            {
                var g = Known(Groups, group);
                if (g != null) r.outdoors = g;
                else Bad("outdoors: '" + group + "' is not one of " + string.Join(", ", Groups));
            }
            if (r.place == BackdropPlace.Outdoors && r.outdoors.Length == 0) Bad("outdoors: a backdrop outdoors names its group of land (" + string.Join(", ", Groups) + ")");
            string era = (field("era") ?? "").Trim().ToLowerInvariant();
            if (era.Length == 0 || era == "any") r.era = PortraitEra.Unknown;
            else if (era == "tribal" || era == "neolithic") r.era = PortraitEra.Tribal;
            else if (era == "medieval") r.era = PortraitEra.Medieval;
            else if (era == "industrial") r.era = PortraitEra.Industrial;
            else if (era == "spacer" || era == "ultra") r.era = PortraitEra.Spacer;
            else Bad("era: '" + era + "' is not tribal, medieval, industrial, spacer or any");
            foreach (var c in list("contents") ?? new string[0])
            {
                var k = Known(Contents, c);
                if (k != null) r.contents.Add(k);
                else if (!string.IsNullOrWhiteSpace(c)) Bad("contents: '" + c + "' is not one of " + string.Join(", ", Contents));
            }
            return r;
        }

        // ---------------- Rooms ----------------

        /// <summary>
        /// The nearest kinds of room for a role no backdrop names, in order (the player's mods' roles; docs/player-mods):
        /// guest rooms, nurseries, deathrest and coma rooms like bedrooms; prison barracks and containment like prison cells;
        /// playrooms like rec rooms; a library, classroom or gene lab like a laboratory; a ballroom or gallery like a dining
        /// or rec room; a worship room, ceremonial chamber or tomb like each other; a factory, reactor room like a workshop;
        /// a barn or cooled store like a storeroom. Bathrooms and saunas have none (a plain ground).
        /// </summary>
        public static readonly Dictionary<string, string[]> RoleFallbacks = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Bedroom"] = new[] { "GuestRoom", "Barracks" },
            ["GuestRoom"] = new[] { "Bedroom", "Barracks" },
            ["Barracks"] = new[] { "Bedroom", "GuestRoom" },
            ["Nursery"] = new[] { "Bedroom", "Playroom", "RecRoom" },
            ["Playroom"] = new[] { "RecRoom", "Nursery", "Bedroom" },
            ["DeathrestChamber"] = new[] { "Bedroom" },
            ["EBSG_ComaRestChamber"] = new[] { "Bedroom" },
            ["PrisonCell"] = new[] { "PrisonBarracks", "ContainmentCell" },
            ["PrisonBarracks"] = new[] { "PrisonCell", "Barracks" },
            ["ContainmentCell"] = new[] { "PrisonCell" },
            ["Hospital"] = new[] { "TherapyOffice", "Bedroom" },
            ["TherapyOffice"] = new[] { "Hospital", "Laboratory" },
            ["Laboratory"] = new[] { "VBE_Library", "Classroom", "GR_GenLab" },
            ["VBE_Library"] = new[] { "Laboratory", "Classroom" },
            ["Classroom"] = new[] { "Laboratory", "VBE_Library", "RecRoom" },
            ["GR_GenLab"] = new[] { "Laboratory" },
            ["DiningRoom"] = new[] { "RecRoom", "Kitchen", "VFEE_Ballroom" },
            ["RecRoom"] = new[] { "DiningRoom", "VFEE_Ballroom", "VFEE_Gallery" },
            ["VFEE_Ballroom"] = new[] { "DiningRoom", "RecRoom" },
            ["VFEE_Gallery"] = new[] { "RecRoom", "DiningRoom" },
            ["ThroneRoom"] = new[] { "VFEE_Ballroom", "DiningRoom" },
            ["Kitchen"] = new[] { "DiningRoom", "Workshop" },
            ["Workshop"] = new[] { "VFEFactory_FactoryRoom", "Laboratory", "Storeroom" },
            ["VFEFactory_FactoryRoom"] = new[] { "Workshop" },
            ["VPE_ReactorRoom"] = new[] { "Workshop" },
            ["WorshipRoom"] = new[] { "CeremonialChamber", "Tomb" },
            ["CeremonialChamber"] = new[] { "WorshipRoom", "Tomb" },
            ["Tomb"] = new[] { "WorshipRoom", "CeremonialChamber" },
            ["Storeroom"] = new[] { "CooledStoreroom", "Barn", "Workshop" },
            ["CooledStoreroom"] = new[] { "Storeroom", "Kitchen" },
            ["Barn"] = new[] { "Storeroom" },
            ["PrivateBathroom"] = new string[0],
            ["PublicBathroom"] = new string[0],
            ["SaunaRoom"] = new string[0]
        };

        /// <summary>A backdrop for any age stands this far from the colony's own age (a backdrop of the colony's own age first, then one for any, then the next age).</summary>
        public const float AnyEra = 0.5f;

        /// <summary>How far a backdrop's age is from the colony's.</summary>
        public static float EraApart(PortraitEra colony, PortraitEra backdrop) =>
            backdrop == PortraitEra.Unknown || colony == PortraitEra.Unknown ? AnyEra : Math.Abs((int)colony - (int)backdrop);

        /// <summary>How many of the room's things a backdrop shows.</summary>
        public static int Matching(BackdropRecord r, HashSet<string> contents)
        {
            int n = 0;
            if (contents == null) return n;
            foreach (var c in r.contents)
                if (contents.Contains(c))
                    n++;
            return n;
        }

        private static bool Fits(BackdropRecord r, string role)
        {
            foreach (var x in r.rooms)
                if (string.Equals(x, role, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>Of the candidates, the best by a score (lower first), then one by the place's number among equals (by name, so the same each time).</summary>
        private static int Best(IList<BackdropRecord> records, List<int> candidates, Func<int, double> score, int seed)
        {
            if (candidates.Count == 0) return -1;
            double best = candidates.Min(score);
            var equal = candidates.Where(i => score(i) <= best + 1e-6).OrderBy(i => records[i].name, StringComparer.Ordinal).ToList();
            return equal[(int)(PortraitLibraryMath.Mix(seed) % (uint)equal.Count)];
        }

        /// <summary>The backdrop for where a person is (see the class's notes); -1 for none.</summary>
        public static BackdropChoice Choose(BackdropWhere w, IList<BackdropRecord> records)
        {
            var none = new BackdropChoice { index = -1, why = "no backdrop in the library" };
            if (records == null || records.Count == 0 || w == null) return none;
            var all = Enumerable.Range(0, records.Count).ToList();
            if (w.place == BackdropPlace.Room)
            {
                var roles = new List<string>();
                if (w.role.Length > 0)
                {
                    roles.Add(w.role);
                    if (RoleFallbacks.TryGetValue(w.role, out var near)) roles.AddRange(near);
                }
                foreach (var role in roles)
                {
                    var fit = all.Where(i => records[i].place == BackdropPlace.Room && Fits(records[i], role)).ToList();
                    if (fit.Count == 0) continue;
                    // The age first, then what is in the room (each thing shown counts a little less than an age apart).
                    int pick = Best(records, fit, i => EraApart(w.era, records[i].era) * 100 - Matching(records[i], w.contents), w.seed);
                    return new BackdropChoice { index = pick, why = role == w.role ? "the room's own kind, " + role : "the nearest kind to " + w.role + ", " + role };
                }
                var byThings = all.Where(i => records[i].place == BackdropPlace.Room && Matching(records[i], w.contents) > 0).ToList();
                if (byThings.Count > 0)
                {
                    int pick = Best(records, byThings, i => -Matching(records[i], w.contents) * 100 + EraApart(w.era, records[i].era), w.seed);
                    return new BackdropChoice { index = pick, why = (w.role.Length > 0 ? "no backdrop for " + w.role + " or its like" : "a room of no particular kind") + ": by what is in it" };
                }
                return Plain(records, all, w.seed, w.role.Length > 0 ? "no backdrop for " + w.role + " or its like" : "a room with nothing a backdrop shows");
            }
            if (w.place == BackdropPlace.Outdoors && w.group.Length > 0)
            {
                var groups = new List<string> { w.group };
                if (GroupFallbacks.TryGetValue(w.group, out var near)) groups.AddRange(near);
                foreach (var g in groups)
                {
                    var fit = all.Where(i => records[i].place == BackdropPlace.Outdoors && records[i].outdoors == g).ToList();
                    if (fit.Count == 0) continue;
                    int pick = Best(records, fit, i => 0, w.seed);
                    return new BackdropChoice { index = pick, why = g == w.group ? "outdoors, " + g : "outdoors, " + w.group + " like " + g };
                }
                return Plain(records, all, w.seed, "no backdrop for " + w.group + " or its like");
            }
            return Plain(records, all, w.seed, "no map");
        }

        private static BackdropChoice Plain(IList<BackdropRecord> records, List<int> all, int seed, string why)
        {
            var plain = all.Where(i => records[i].place == BackdropPlace.Plain).ToList();
            if (plain.Count == 0) return new BackdropChoice { index = -1, why = why + "; no plain ground in the library: the portrait's own" };
            return new BackdropChoice { index = Best(records, plain, i => 0, seed), why = why + ": a plain ground" };
        }

        // ---------------- What is in a room ----------------

        /// <summary>What the game says of one building or thing in a room, besides its name: the flags <see cref="ContentWordsOf"/> reads.</summary>
        [Flags]
        public enum ThingIs
        {
            None = 0,
            /// <summary>A bed for people (a bedroll, a crib, a hospital bed).</summary>
            Bed = 1,
            /// <summary>A table people eat at.</summary>
            Table = 2,
            /// <summary>A seat.</summary>
            Seat = 4,
            /// <summary>A work table (a bench, a stove, a research bench).</summary>
            WorkTable = 8,
            /// <summary>A research bench.</summary>
            Research = 16,
            /// <summary>A shelf or other storage building.</summary>
            Storage = 32,
            /// <summary>A throne.</summary>
            Throne = 64,
            /// <summary>Something to pass the time with (a game table, a billiards table, horseshoes).</summary>
            Joy = 128,
            /// <summary>Medicine (an item).</summary>
            Medicine = 256,
            /// <summary>Burns with a fire people see (a campfire, a torch, a brazier, a hearth).</summary>
            Fire = 512,
            /// <summary>A book (an item).</summary>
            Book = 1024
        }

        /// <summary>
        /// The words for one thing in a room, by what the game says of it and its def name's words: a bed; a table; chairs
        /// (any seat); a hearth and a fire (a fireplace, campfire or hearth), a fire alone (a torch, a brazier); a window;
        /// shelves (a shelf or rack), crates (a crate, chest, barrel, box or bin); an altar (an altar, a lectern, a shrine);
        /// a throne; banners (a banner, a tapestry, a flag); pillars (a column); a workbench, tools (a tool cabinet); a
        /// stove (a stove or oven, its work table too); pots (a pot, cauldron, kettle); books (a bookcase, a book, a
        /// research bench); medicine (medicine, a vitals monitor or other medical building); bars (bars, a cage); games.
        /// </summary>
        public static List<string> ContentWordsOf(string defName, ThingIs flags)
        {
            var w = new List<string>();
            var t = PortraitLibraryMath.Tokens(defName);
            bool Starts(params string[] stems) => t.Any(x => stems.Any(s => x.StartsWith(s, StringComparison.Ordinal)));
            void Add(string word)
            {
                if (!w.Contains(word)) w.Add(word);
            }
            if ((flags & ThingIs.Bed) != 0) Add("bed");
            if ((flags & ThingIs.Table) != 0 || Starts("table") && (flags & ThingIs.WorkTable) == 0) Add("table");
            if ((flags & ThingIs.Seat) != 0) Add("chairs");
            if ((flags & ThingIs.Throne) != 0 || Starts("throne")) Add("throne");
            bool hearth = Starts("fireplace", "hearth", "campfire", "firepit") || t.Contains("large") && t.Contains("fire");
            if (hearth)
            {
                Add("hearth");
                Add("fire");
            }
            if ((flags & ThingIs.Fire) != 0 || Starts("brazier", "torch")) Add("fire");
            if (Starts("window")) Add("window");
            if (Starts("shelf", "shelves", "rack")) Add("shelves");
            else if ((flags & ThingIs.Storage) != 0 && !Starts("crate", "chest", "barrel", "box", "bin", "locker")) Add("shelves");
            if (Starts("crate", "chest", "barrel", "box", "bin", "locker", "basket")) Add("crates");
            if (Starts("altar", "lectern", "shrine")) Add("altar");
            if (Starts("banner", "tapestry", "flag", "pennant")) Add("banners");
            if (Starts("column", "pillar")) Add("pillars");
            bool stove = Starts("stove", "oven");
            if (stove) Add("stove");
            if ((flags & ThingIs.WorkTable) != 0 && (flags & ThingIs.Research) == 0 && !stove && !hearth) Add("workbench");
            if (Starts("tool")) Add("tools");
            if (Starts("pot", "cauldron", "kettle")) Add("pots");
            if ((flags & (ThingIs.Research | ThingIs.Book)) != 0 || Starts("book")) Add("books");
            if ((flags & ThingIs.Medicine) != 0 || Starts("vitals", "medical", "surgery", "iv")) Add("medicine");
            if (Starts("bars", "cage", "jail")) Add("bars");
            if ((flags & ThingIs.Joy) != 0 || Starts("chess", "billiard", "poker", "horseshoe", "arcade", "dart", "game")) Add("games");
            return w;
        }

        /// <summary>The words for a whole room from the words of each thing in it: all of them, and "beds" where there are two beds or more.</summary>
        public static HashSet<string> RoomContents(IEnumerable<List<string>> things)
        {
            var set = new HashSet<string>();
            int beds = 0;
            if (things == null) return set;
            foreach (var t in things)
            {
                if (t == null) continue;
                foreach (var w in t) set.Add(w);
                if (t.Contains("bed")) beds++;
            }
            if (beds >= 2) set.Add("beds");
            return set;
        }

        // ---------------- Outdoors ----------------

        /// <summary>
        /// The player's 56 active biomes (docs/player-mods, BiomeDef rows with active yes: the game's, Odyssey's, Anomaly's,
        /// Alpha Biomes', Alpha Genes', More Vanilla Biomes', ReGrowth's and Vanilla Quests Expanded's) by their group of land.
        /// </summary>
        public static readonly Dictionary<string, string> BiomeGroups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TemperateForest"] = "temperateForest", ["RG_AspenForest"] = "temperateForest", ["AB_OcularForest"] = "temperateForest", ["ZBiome_CloudForest"] = "temperateForest",
            ["BorealForest"] = "borealForest",
            ["Tundra"] = "tundra", ["ZBiome_AlpineMeadow"] = "tundra",
            ["IceSheet"] = "ice", ["SeaIce"] = "ice", ["GlacialPlain"] = "ice", ["ZBiome_GlacialShield"] = "ice", ["ZBiome_Iceberg_NoBeach"] = "ice",
            ["Grasslands"] = "grassland", ["ZBiome_Grasslands"] = "grassland", ["AB_IdyllicMeadows"] = "grassland",
            ["AridShrubland"] = "shrubland", ["ZBiome_Badlands"] = "shrubland",
            ["Desert"] = "desert", ["ExtremeDesert"] = "desert", ["ZBiome_DesertOasis"] = "desert", ["ZBiome_CoastalDunes"] = "coast",
            ["TropicalRainforest"] = "jungle", ["AB_MycoticJungle"] = "jungle", ["AB_FeraliskInfestedJungle"] = "jungle", ["RG_BoilingForest"] = "jungle",
            ["TemperateSwamp"] = "swamp", ["TropicalSwamp"] = "swamp", ["ColdBog"] = "swamp", ["ZBiome_Marsh"] = "swamp", ["AB_MiasmicMangrove"] = "swamp",
            ["AB_GelatinousSuperorganism"] = "swamp", ["AB_TarPits"] = "swamp",
            ["Ocean"] = "coast", ["Lake"] = "coast", ["ZBiome_Sandbar_NoBeach"] = "coast", ["AG_NereidPocketPlane"] = "coast",
            ["RG_AridWasteland"] = "wasteland", ["RG_TemperateWasteland"] = "wasteland", ["RG_FrigidWasteland"] = "wasteland", ["Scarlands"] = "wasteland",
            ["AB_MechanoidIntrusion"] = "wasteland", ["AB_GallatrossGraveyard"] = "wasteland", ["AB_PropaneLakes"] = "wasteland", ["MetalHell"] = "wasteland",
            ["VQEA_AncientComplex"] = "wasteland",
            ["LavaField"] = "volcanic", ["AB_PyroclasticConflagration"] = "volcanic",
            ["Glowforest"] = "glowforest", ["AG_PocketPlane"] = "glowforest",
            ["Underground"] = "cave", ["Undercave"] = "cave", ["Labyrinth"] = "cave", ["VQE_AncientSilo"] = "cave",
            ["Space"] = "space", ["Orbit"] = "space",
            ["AB_RockyCrags"] = "mountain"
        };

        /// <summary>A group's nearest groups, in order, where the library has no backdrop of its own for it.</summary>
        public static readonly Dictionary<string, string[]> GroupFallbacks = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["temperateForest"] = new[] { "borealForest", "grassland", "jungle" },
            ["borealForest"] = new[] { "temperateForest", "tundra", "mountain" },
            ["tundra"] = new[] { "ice", "borealForest", "mountain" },
            ["ice"] = new[] { "tundra", "mountain" },
            ["grassland"] = new[] { "shrubland", "temperateForest" },
            ["shrubland"] = new[] { "grassland", "desert" },
            ["desert"] = new[] { "shrubland", "wasteland" },
            ["jungle"] = new[] { "swamp", "temperateForest", "glowforest" },
            ["swamp"] = new[] { "jungle", "temperateForest" },
            ["coast"] = new[] { "grassland", "desert" },
            ["wasteland"] = new[] { "desert", "shrubland", "volcanic" },
            ["volcanic"] = new[] { "wasteland", "mountain", "desert" },
            ["glowforest"] = new[] { "jungle", "temperateForest" },
            ["cave"] = new[] { "mountain" },
            ["space"] = new string[0],
            ["mountain"] = new[] { "tundra", "shrubland", "cave" }
        };

        /// <summary>
        /// The group of land for a biome: by the table where it is one of the player's, else by its own traits: vacuum
        /// space; underground a cave; extreme and hot volcanic, extreme otherwise a wasteland; freezing ice, cold with few
        /// plants tundra, cold with trees a boreal forest; dry and hot a desert, dry otherwise shrubland; very wet and warm
        /// a jungle, very wet otherwise a swamp; with many plants a temperate forest; else grassland. Temperatures in
        /// degrees, rainfall in millimetres a year, plants as the game's density (about 0 to 1).
        /// </summary>
        public static string GroupOf(string biome, float temperature, float rainfall, float plants, bool extreme, bool vacuum, bool underground)
        {
            if (!string.IsNullOrEmpty(biome) && BiomeGroups.TryGetValue(biome, out var g)) return g;
            if (vacuum) return "space";
            if (underground) return "cave";
            if (float.IsNaN(temperature)) temperature = 12f;
            if (float.IsNaN(rainfall)) rainfall = 600f;
            if (float.IsNaN(plants)) plants = 0.5f;
            if (extreme) return temperature > 35f ? "volcanic" : temperature < -15f ? "ice" : "wasteland";
            if (temperature < -12f) return "ice";
            if (temperature < 0f) return plants < 0.3f ? "tundra" : "borealForest";
            if (rainfall < 400f) return temperature > 20f ? "desert" : "shrubland";
            if (rainfall > 2000f) return temperature > 18f ? "jungle" : "swamp";
            if (plants >= 0.6f) return temperature > 22f ? "jungle" : "temperateForest";
            return "grassland";
        }
    }
}
