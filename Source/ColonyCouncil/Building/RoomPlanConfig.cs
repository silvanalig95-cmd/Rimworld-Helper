using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The player's choices for one room template while planning: orientation, door, materials, furniture
    /// variants. Kept for the whole session so the next bedroom comes out like the last one. The clearing
    /// options (cut plants, mine rock, ...) are shared by all templates and saved in the mod settings.
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
        public readonly Dictionary<string, ThingDef> variant = new Dictionary<string, ThingDef>();
        public readonly Dictionary<string, ThingDef> stuff = new Dictionary<string, ThingDef>();

        /// <summary>Bumped on every change, so cached plans know they're stale.</summary>
        public int version;

        public void Changed() => version++;

        private static readonly Dictionary<RoomTemplateDef, RoomPlanConfig> configs = new Dictionary<RoomTemplateDef, RoomPlanConfig>();

        public static RoomPlanConfig For(RoomTemplateDef t)
        {
            if (t == null) return new RoomPlanConfig();
            if (!configs.TryGetValue(t, out var c))
            {
                c = new RoomPlanConfig();
                configs[t] = c;
            }
            return c;
        }

        public static void ClearAll() => configs.Clear();

        public static CouncilSettings S => ColonyCouncilMod.Settings;

        public int DoorX(RoomTemplateDef t) => doorX >= 0 && doorX < t.size.x ? doorX : t.DoorX;

        public ThingDef WallStuff(RoomTemplateDef t, Map map)
        {
            if (wallStuff != null && GenStuff.AllowedStuffsFor(ThingDefOf.Wall).Contains(wallStuff)) return wallStuff;
            return Avail.BestStuff(ThingDefOf.Wall, map, (t.FullWidth + t.FullHeight) * 2 * ThingDefOf.Wall.CostStuffCount);
        }

        public ThingDef DoorDef(Map map)
        {
            if (doorDef != null && Avail.CanBuild(doorDef, map)) return doorDef;
            if (Avail.CanBuild(ThingDefOf.Door, map)) return ThingDefOf.Door;
            return DoorOptions(map).FirstOrDefault() ?? ThingDefOf.Door;
        }

        public ThingDef DoorStuff(ThingDef door, ThingDef walls, Map map)
        {
            if (door == null || !door.MadeFromStuff) return null;
            var allowed = GenStuff.AllowedStuffsFor(door).ToList();
            if (doorStuff != null && allowed.Contains(doorStuff)) return doorStuff;
            // A door that matches the walls looks deliberate; otherwise whatever there's most of.
            if (walls != null && allowed.Contains(walls) && map.resourceCounter.GetCount(walls) >= door.CostStuffCount) return walls;
            return Avail.BestStuff(door, map);
        }

        public TerrainDef Floor(RoomTemplateDef t, Map map) => floorChosen ? floor : t.ResolveFloor(map);

        /// <summary>The floor's material: the player's pick if it suits this floor, else whatever there's most of.</summary>
        public ThingDef FloorStuff(TerrainDef f, Map map, int cells = -1)
        {
            if (f == null || !f.MadeFromStuff) return null;
            if (floorStuff != null && GenStuff.AllowedStuffsFor(f).Contains(floorStuff)) return floorStuff;
            return Avail.BestStuff(f, map, cells > 0 ? f.CostStuffCount * cells : -1);
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

        /// <summary>Materials worth offering: anything in stock, plus the current pick.</summary>
        public static List<ThingDef> StuffOptions(BuildableDef def, Map map, ThingDef current)
        {
            var list = GenStuff.AllowedStuffsFor(def)
                .Where(s => s == current || map.resourceCounter.GetCount(s) > 0)
                .OrderByDescending(s => map.resourceCounter.GetCount(s))
                .ToList();
            return list;
        }
    }
}
