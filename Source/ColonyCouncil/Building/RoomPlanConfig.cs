using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The player's choices for one kind of room while planning: orientation, door, style, theme, materials, furniture
    /// variants. The variants of a room type (a cozy, standard and roomy bedroom) share one, so switching size keeps the
    /// rest. Kept for the whole session; the materials the player picks are also remembered in the mod settings and used
    /// again next time while there's still enough of them. The clearing options (cut plants, mine rock, ...) are shared
    /// by all templates and saved in the mod settings too.
    /// </summary>
    public class RoomPlanConfig
    {
        public int rotation;
        public bool mirror;
        /// <summary>Door position along the entrance wall, in interior x. -1 = the template's default.</summary>
        public int doorX = -1;
        public ThingDef wallStuff;
        public ThingDef doorDef;
        public ThingDef doorStuff;
        /// <summary>False until the player picks a floor; then <see cref="floor"/> is used (null = no floor).</summary>
        public bool floorChosen;
        public TerrainDef floor;
        /// <summary>The material for a floor that can be made of different ones (null = whatever there's most of).</summary>
        public ThingDef floorStuff;
        public RoomStyle style = RoomStyle.Comfortable;
        /// <summary>The look the room is built in (rustic, stately, ...); null: as the plan was designed.</summary>
        public RoomThemeDef theme;
        public readonly Dictionary<string, ThingDef> variant = new Dictionary<string, ThingDef>();
        public readonly Dictionary<string, ThingDef> stuff = new Dictionary<string, ThingDef>();
        /// <summary>Groups whose material the player picked since the planner opened: kept even if the stock runs short.</summary>
        public readonly HashSet<string> pickedNow = new HashSet<string>();

        public const string WallKey = "walls", DoorKey = "door", FloorKey = "floor", ThemeKey = "theme";

        /// <summary>Bumped on every change, so cached plans know they're stale.</summary>
        public int version;

        /// <summary>The room type (or template) the remembered materials belong to.</summary>
        public string scope = "";

        public void Changed() => version++;

        private static readonly Dictionary<object, RoomPlanConfig> configs = new Dictionary<object, RoomPlanConfig>();

        public static RoomPlanConfig For(RoomTemplateDef t)
        {
            if (t == null) return new RoomPlanConfig();
            object key = (object)RoomTypeDef.Of(t) ?? t;
            if (!configs.TryGetValue(key, out var c))
            {
                c = new RoomPlanConfig { scope = ScopeOf(t) };
                c.LoadRemembered();
                configs[key] = c;
            }
            return c;
        }

        public static string ScopeOf(RoomTemplateDef t)
        {
            if (t == null) return "";
            if (t.custom != null) return "custom:" + t.custom.id;
            return RoomTypeDef.Of(t)?.defName ?? t.defName;
        }

        public static void ClearAll() => configs.Clear();

        public static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The planner opened: earlier picks count again only while the colony still has enough of them.</summary>
        public void StartSession()
        {
            pickedNow.Clear();
            LoadRemembered();
            Changed();
        }

        // ---------------- Picking materials ----------------

        public void PickStuff(string group, ThingDef s)
        {
            stuff[group] = s;
            pickedNow.Add(group);
            Remember(group, s);
            Changed();
        }

        public void PickWalls(ThingDef s)
        {
            wallStuff = s;
            pickedNow.Add(WallKey);
            Remember(WallKey, s);
            Changed();
        }

        public void PickDoorStuff(ThingDef s)
        {
            doorStuff = s;
            pickedNow.Add(DoorKey);
            Remember(DoorKey, s);
            Changed();
        }

        public void PickFloorStuff(ThingDef s)
        {
            floorStuff = s;
            pickedNow.Add(FloorKey);
            Remember(FloorKey, s);
            Changed();
        }

        /// <summary>
        /// Builds the room in another theme (null: as designed). The theme picks the look, so the materials, floor, door
        /// and furniture picked so far give way to it, as with "Use what we have"; what the player picks after that wins
        /// again. The theme is remembered for this room like a material.
        /// </summary>
        public void SetTheme(RoomThemeDef t)
        {
            if (t == theme) return;
            UseWhatWeHave();
            theme = t;
            floorChosen = false;
            floor = null;
            doorDef = null;
            variant.Clear();
            var settings = S;
            if (settings != null)
            {
                settings.planMaterials ??= new List<string>();
                string entry = scope + "::" + ThemeKey + "=>";
                settings.planMaterials.RemoveAll(e => e != null && e.StartsWith(entry));
                if (t != null) settings.planMaterials.Add(entry + t.defName);
                CouncilSettings.SaveNow();
            }
            Changed();
        }

        /// <summary>"Use what we have": forget every material choice for this room, so each one is picked from the stock again.</summary>
        public void UseWhatWeHave()
        {
            wallStuff = null;
            doorStuff = null;
            floorStuff = null;
            stuff.Clear();
            pickedNow.Clear();
            var s = S;
            if (s?.planMaterials != null)
            {
                string prefix = scope + "::";
                string themeEntry = prefix + ThemeKey + "=>";
                if (s.planMaterials.RemoveAll(e => e != null && e.StartsWith(prefix) && !e.StartsWith(themeEntry)) > 0) CouncilSettings.SaveNow();
            }
            Changed();
        }

        private void Remember(string group, ThingDef s)
        {
            var settings = S;
            if (settings == null || s == null) return;
            settings.planMaterials ??= new List<string>();
            string prefix = scope + "::" + group + "=>";
            settings.planMaterials.RemoveAll(e => e != null && e.StartsWith(prefix));
            settings.planMaterials.Add(prefix + s.defName);
            if (settings.planMaterials.Count > 400) settings.planMaterials.RemoveRange(0, settings.planMaterials.Count - 400);
            CouncilSettings.SaveNow();
        }

        private void LoadRemembered()
        {
            var settings = S;
            if (settings?.planMaterials == null) return;
            string prefix = scope + "::";
            foreach (var e in settings.planMaterials)
            {
                if (e == null || !e.StartsWith(prefix)) continue;
                int arrow = e.LastIndexOf("=>");
                if (arrow <= prefix.Length) continue;
                string group = e.Substring(prefix.Length, arrow - prefix.Length);
                if (group == ThemeKey)
                {
                    theme = DefDatabase<RoomThemeDef>.GetNamedSilentFail(e.Substring(arrow + 2));
                    continue;
                }
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(e.Substring(arrow + 2));
                if (def == null) continue;
                switch (group)
                {
                    case WallKey: wallStuff = def; break;
                    case DoorKey: doorStuff = def; break;
                    case FloorKey: floorStuff = def; break;
                    default: stuff[group] = def; break;
                }
            }
        }

        /// <summary>A copy that puts the door elsewhere (upgrading a room lays the furniture out around its own door).</summary>
        public RoomPlanConfig WithDoor(int door)
        {
            var c = new RoomPlanConfig
            {
                rotation = rotation,
                mirror = mirror,
                doorX = door,
                wallStuff = wallStuff,
                doorDef = doorDef,
                doorStuff = doorStuff,
                floorChosen = floorChosen,
                floor = floor,
                floorStuff = floorStuff,
                style = style,
                theme = theme,
                scope = scope,
                version = version
            };
            foreach (var kv in variant) c.variant[kv.Key] = kv.Value;
            foreach (var kv in stuff) c.stuff[kv.Key] = kv.Value;
            foreach (var g in pickedNow) c.pickedNow.Add(g);
            return c;
        }

        // ---------------- What the plan uses ----------------

        public int DoorX(RoomTemplateDef t) => doorX >= 0 && doorX < t.size.x ? doorX : t.DoorX;

        /// <summary>Wall segments a free-standing room of this size needs (the ring minus the door).</summary>
        public static int WallCount(RoomTemplateDef t) => (t.FullWidth + t.FullHeight) * 2 - 4 - 1;

        /// <summary>
        /// The wall material: the player's pick (this time, or an earlier one while there's enough of it), what a saved
        /// room was built of, else the most common material the colony has enough of. Stone comes before wood.
        /// </summary>
        public ThingDef WallStuff(RoomTemplateDef t, Map map)
        {
            var wall = ThingDefOf.Wall;
            int units = WallCount(t);
            if (wallStuff != null && Avail.StuffAllowed(wall, wallStuff) && MaterialPicker.Allowed(wallStuff, MaterialUse.Wall)
                && (pickedNow.Contains(WallKey) || MaterialPicker.Affordable(wall, wallStuff, units, map)))
                return wallStuff;
            if (theme != null)
            {
                var themed = theme.PickStuff(wall, map, units, MaterialUse.Wall);
                if (themed != null) return themed;
            }
            if (!t.wallStuffHint.NullOrEmpty())
            {
                var hint = DefDatabase<ThingDef>.GetNamedSilentFail(t.wallStuffHint);
                if (hint != null && Avail.StuffAllowed(wall, hint) && MaterialPicker.Allowed(hint, MaterialUse.Wall) && MaterialPicker.Affordable(wall, hint, units, map))
                    return hint;
            }
            return MaterialPicker.Pick(wall, map, units, MaterialUse.Wall);
        }

        public ThingDef DoorDef(Map map)
        {
            if (doorDef != null && Avail.CanBuild(doorDef, map)) return doorDef;
            var themed = theme?.DoorFor(map);
            if (themed != null) return themed;
            if (Avail.CanBuild(ThingDefOf.Door, map)) return ThingDefOf.Door;
            return DoorOptions(map).FirstOrDefault() ?? ThingDefOf.Door;
        }

        public ThingDef DoorStuff(ThingDef door, ThingDef walls, Map map)
        {
            if (door == null || !door.MadeFromStuff) return null;
            if (doorStuff != null && Avail.StuffAllowed(door, doorStuff) && MaterialPicker.Allowed(doorStuff, MaterialUse.Door)
                && (pickedNow.Contains(DoorKey) || MaterialPicker.Affordable(door, doorStuff, 1, map)))
                return doorStuff;
            // A door that matches the walls looks deliberate, if there's enough for both.
            if (walls != null && Avail.StuffAllowed(door, walls) && MaterialPicker.Allowed(walls, MaterialUse.Door)
                && MaterialCounts.Have(map, walls) >= MaterialCounts.PerUnit(door, walls) + MaterialCounts.PerUnit(ThingDefOf.Wall, walls) * 4)
                return walls;
            return MaterialPicker.Pick(door, map, 1, MaterialUse.Door);
        }

        public TerrainDef Floor(RoomTemplateDef t, Map map) => floorChosen ? floor : t.FloorFor(style, map, theme);

        /// <summary>The floor's material: the player's pick if it suits this floor (and there's enough), else the best the colony has.</summary>
        public ThingDef FloorStuff(TerrainDef f, Map map, int cells = -1)
        {
            if (f == null || !f.MadeFromStuff) return null;
            int units = cells > 0 ? cells : 1;
            if (floorStuff != null && Avail.StuffAllowed(f, floorStuff) && MaterialPicker.Allowed(floorStuff, MaterialUse.Floor)
                && (pickedNow.Contains(FloorKey) || MaterialPicker.Affordable(f, floorStuff, units, map)))
                return floorStuff;
            var themed = theme?.PickStuff(f, map, units, MaterialUse.Floor);
            if (themed != null) return themed;
            return MaterialPicker.Pick(f, map, units, MaterialUse.Floor);
        }

        /// <summary>Single-cell doors the colony can build (simple doors, autodoors, modded ones).</summary>
        public static List<ThingDef> DoorOptions(Map map)
        {
            return Avail.AllBuildings
                .Where(d => d.IsDoor && d.size.x == 1 && d.size.z == 1 && Avail.CanBuild(d, map))
                .OrderBy(d => d == ThingDefOf.Door ? 0 : 1)
                .ThenBy(d => d.label)
                .ToList();
        }

        /// <summary>
        /// Floors the colony can lay (with <paramref name="includeLocked"/>, also those still waiting on research),
        /// cheapest work first. Any player-buildable terrain you can walk on counts, so modded floors that don't use
        /// the vanilla floor template show up too; bridges, ship substructure, water and soil don't.
        /// </summary>
        public static List<TerrainDef> FloorOptions(Map map, bool includeLocked = false)
        {
            return DefDatabase<TerrainDef>.AllDefsListForReading
                .Where(f => IsFloor(f) && (includeLocked || Avail.CanBuild(f, map)))
                .OrderBy(f => f.GetStatValueAbstract(StatDefOf.WorkToBuild))
                .ToList();
        }

        public static bool IsFloor(TerrainDef f) =>
            f != null && f.BuildableByPlayer && !f.bridge && !f.IsSubstructure && !f.IsWater
            && f.passability != Traversability.Impassable && f.fertility <= 0f;

        /// <summary>Materials worth offering: anything the colony has (stored or lying around), plus the current pick; most first.</summary>
        public static List<ThingDef> StuffOptions(BuildableDef def, Map map, ThingDef current, MaterialUse use = MaterialUse.Furniture)
        {
            return Avail.AllowedStuffs(def)
                .Where(s => (s == current || MaterialCounts.Have(map, s) > 0) && MaterialPicker.Allowed(s, use))
                .OrderByDescending(s => MaterialCounts.Have(map, s))
                .ToList();
        }
    }
}
