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

        /// <summary>Single-cell doors the colony can build (simple doors, autodoors, modded ones).</summary>
        public static List<ThingDef> DoorOptions(Map map)
        {
            return Avail.AllBuildings
                .Where(d => d.IsDoor && d.size.x == 1 && d.size.z == 1 && Avail.CanBuild(d, map))
                .OrderBy(d => d == ThingDefOf.Door ? 0 : 1)
                .ThenBy(d => d.label)
                .ToList();
        }

        /// <summary>Floors the colony can lay, cheapest work first.</summary>
        public static List<TerrainDef> FloorOptions(Map map)
        {
            return DefDatabase<TerrainDef>.AllDefsListForReading
                .Where(f => f.BuildableByPlayer && f.layerable && !f.bridge && Avail.CanBuild(f, map))
                .OrderBy(f => f.GetStatValueAbstract(StatDefOf.WorkToBuild))
                .ToList();
        }

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
