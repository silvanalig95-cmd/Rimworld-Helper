using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>How much a room plan puts in: the essentials, the standard furnishing, or extra decor and a nicer floor.</summary>
    public enum RoomStyle
    {
        Spartan,
        Comfortable,
        Lavish
    }

    /// <summary>One piece of furniture in a room template.</summary>
    public class TemplateThing
    {
        /// <summary>
        /// Preferred defNames, best first. The first one the colony can build is used. Names are looked up
        /// at runtime, so a def missing from this game (DLC off, removed by a mod) is simply skipped.
        /// </summary>
        public List<string> defs = new List<string>();

        public IEnumerable<ThingDef> Defs
        {
            get
            {
                if (defs == null) yield break;
                foreach (var name in defs)
                {
                    var d = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                    if (d != null) yield return d;
                }
            }
        }
        /// <summary>
        /// Fallback by purpose (bed, light, heater, table, chair, ...) so modded or era-appropriate
        /// furniture is picked when none of <see cref="defs"/> is available.
        /// </summary>
        public string role;
        /// <summary>Root cell in interior coordinates ((0,0) is the bottom-left floor cell).</summary>
        public IntVec2 pos;
        public Rot4 rot = Rot4.North;
        public bool optional = true;
        /// <summary>
        /// Placed in the wall ring instead of a wall segment (coolers, vents). Then <see cref="pos"/> is in
        /// full coordinates, where (0,0) is the bottom-left wall corner.
        /// </summary>
        public bool inWall;
        /// <summary>
        /// Which styles include it: "core" (always), "decor" (comfortable and lavish), "lavish" (lavish only). Empty: decor
        /// for plant pots, bedside tables, dressers, columns and drapes, core for everything else.
        /// </summary>
        public string tier;
        /// <summary>A material to prefer (a saved room remembers what it was built of), used when the colony has enough.</summary>
        public string stuff;

        private static readonly HashSet<string> decorRoles = new HashSet<string> { "plantpot", "endtable", "dresser", "column", "drape", "decor" };

        public string Tier => !tier.NullOrEmpty() ? tier : role != null && decorRoles.Contains(role) ? "decor" : "core";

        /// <summary>Part of a room in this style. Pieces the room can't do without are always in.</summary>
        public bool InStyle(RoomStyle style)
        {
            if (!optional) return true;
            switch (Tier)
            {
                case "lavish": return style == RoomStyle.Lavish;
                case "decor": return style != RoomStyle.Spartan;
                default: return true;
            }
        }

        /// <summary>Things that share the same spec are configured together in the planner (all the beds, all the chairs).</summary>
        public string GroupKey => (role ?? "") + "|" + (defs == null ? "" : string.Join(",", defs));

        /// <summary>The plant pots the lavish style adds along the walls.</summary>
        public static readonly TemplateThing AutoDecor = new TemplateThing { defs = new List<string> { "PlantPot" }, role = "plantpot", tier = "lavish" };
    }

    /// <summary>A simple rectangular room: walls, one door, furniture.</summary>
    public class RoomTemplateDef : Def
    {
        /// <summary>Interior floor size (walls are added around it).</summary>
        public IntVec2 size = new IntVec2(4, 4);
        /// <summary>Door position along the south wall in interior x; -1 = middle.</summary>
        public int doorX = -1;
        public List<TemplateThing> things = new List<TemplateThing>();
        /// <summary>Optional floor defNames, best first. Skipped if nothing is buildable.</summary>
        public List<string> floors;
        /// <summary>Floors for the lavish style, best first. Empty: carpets, then fine stone tiles.</summary>
        public List<string> lavishFloors;
        public int order = 100;
        [MustTranslate] public string purpose;
        /// <summary>Good-practice notes shown in the planner, one at a time.</summary>
        [MustTranslate] public List<string> tips;
        /// <summary>Section in the council's Build tab.</summary>
        public string group = "Living";
        /// <summary>Leave the plants inside alone (only those in the way of walls or furniture are cut).</summary>
        public bool keepPlants;
        /// <summary>Building whose icon stands for this template in menus. First one that exists is used.</summary>
        public List<string> iconDefs;
        /// <summary>Room roles (RoomRoleDef defNames) this room likes to be near / far from, used by "find a good spot".</summary>
        public List<string> near;
        public List<string> avoid;
        /// <summary>Show in the rooms menu (the "rooms" button in the bottom bar).</summary>
        public bool architect = true;
        /// <summary>"lowtech": only for tribal and medieval colonies; "industrial": only once the colony is past that.</summary>
        public string era;

        /// <summary>A room the player saved from the map (Your rooms); not a def in the database.</summary>
        [Unsaved] public CustomRoomData custom;
        /// <summary>The wall material a saved room was built of, preferred when there's enough of it.</summary>
        [Unsaved] public string wallStuffHint;

        public int FullWidth => size.x + 2;
        public int FullHeight => size.z + 2;
        public int DoorX => doorX >= 0 && doorX < size.x ? doorX : size.x / 2;

        /// <summary>Floors that must stay clean (hospitals, kitchens): the spartan style keeps them, the lavish one doesn't swap them for carpet.</summary>
        public bool NeedsCleanFloor => floors != null && floors.Contains("SterileTile");

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (size.x < 1 || size.z < 1) yield return "size must be at least 1x1";
            if (!era.NullOrEmpty() && era != "lowtech" && era != "industrial") yield return "era must be lowtech or industrial";
            foreach (var t in things)
            {
                if ((t.defs == null || t.defs.Count == 0) && t.role.NullOrEmpty()) yield return "a template thing has neither defs nor role";
                if (!t.tier.NullOrEmpty() && t.tier != "core" && t.tier != "decor" && t.tier != "lavish") yield return "tier must be core, decor or lavish";
                if (t.inWall)
                {
                    bool onRing = t.pos.x == 0 || t.pos.z == 0 || t.pos.x == FullWidth - 1 || t.pos.z == FullHeight - 1;
                    if (!onRing || t.pos.x < 0 || t.pos.z < 0 || t.pos.x >= FullWidth || t.pos.z >= FullHeight)
                        yield return "in-wall thing position " + t.pos + " is not on the wall ring";
                }
                else if (t.pos.x < 0 || t.pos.z < 0 || t.pos.x >= size.x || t.pos.z >= size.z)
                {
                    yield return "thing position " + t.pos + " is outside the interior";
                }
            }
        }

        /// <summary>The colony's era fits this room (tribal longhouses stay out of an industrial colony, and the other way round).</summary>
        public bool EraFits
        {
            get
            {
                if (era.NullOrEmpty()) return true;
                bool low = Avail.IsPreIndustrial;
                return era == "lowtech" ? low : !low;
            }
        }

        public static Func<ThingDef, bool> RolePredicate(string role)
        {
            switch (role)
            {
                case "bed": return Avail.IsSingleBed;
                case "doublebed": return Avail.IsDoubleBed;
                case "light": return Avail.IsLight;
                case "heater": return Avail.IsHeater;
                case "cooler": return d => d.thingClass != null && typeof(Building_Cooler).IsAssignableFrom(d.thingClass);
                case "table": return Avail.IsEatingTable;
                case "chair": return Avail.IsChair;
                case "endtable": return Avail.IsEndTable;
                case "dresser": return Avail.IsDresser;
                case "joy": return Avail.IsJoyGiver;
                case "research": return Avail.IsResearchBench;
                case "cook": return Avail.IsCookingStation;
                case "craft": return d => Avail.IsWorkstationFor(d, SkillDefOf.Crafting);
                case "art": return d => Avail.IsWorkstationFor(d, SkillDefOf.Artistic);
                case "animalbed": return Avail.IsAnimalBed;
                case "hydroponics": return Avail.IsHydroponics;
                case "plantpot": return Avail.IsPlantPot;
                case "storage": return d => d.thingClass != null && typeof(Building_Storage).IsAssignableFrom(d.thingClass) && d.building?.fixedStorageSettings == null;
                case "grave": return Avail.IsGrave;
                case "butcher": return AdvisorWorker_Kitchen.IsButcherTable;
                case "fermenter": return Avail.IsFermenter;
                case "battery": return d => d.comps != null && d.comps.Any(c => c is CompProperties_Battery);
                case "throne": return d => d.thingClass != null && typeof(Building_Throne).IsAssignableFrom(d.thingClass);
                case "meditation": return d => d.comps != null && d.comps.Any(c => c is CompProperties_MeditationFocus);
                case "music": return d => d.thingClass != null && typeof(Building_MusicalInstrument).IsAssignableFrom(d.thingClass);
                case "medical": return d => Avail.IsSingleBed(d) && d.building != null && d.statBases != null && d.statBases.Any(sm => sm.stat == StatDefOf.MedicalTendQualityOffset && sm.value > 0f);
                case "monitor": return d => d.defName.Contains("VitalsMonitor");
                case "crematorium": return d => d.AllRecipes != null && d.AllRecipes.Any(r => r.defName.Contains("Cremat"));
                case "tailor": return d => d.AllRecipes != null && d.AllRecipes.Any(r => r.ProducedThingDef != null && r.ProducedThingDef.IsApparel && !r.ProducedThingDef.IsWeapon) && d.building != null && !Avail.IsWorkstationFor(d, SkillDefOf.Cooking);
                case "drugs": return d => d.AllRecipes != null && d.AllRecipes.Any(r => r.ProducedThingDef != null && r.ProducedThingDef.IsDrug);
                case "school": return d => d.defName.Contains("SchoolDesk") || d.defName.Contains("Blackboard");
                case "toybox": return d => d.defName.Contains("ToyBox");
                case "crib": return d => Avail.IsHumanBed(d) && d.building.bed_maxBodySize > 0f && d.building.bed_maxBodySize < 0.5f;
                // Added with the room variants.
                case "bookcase": return d => d.thingClass != null && typeof(Building_Bookcase).IsAssignableFrom(d.thingClass);
                case "altar": return d => d.building != null && d.defName.IndexOf("Altar", StringComparison.OrdinalIgnoreCase) >= 0;
                case "lectern": return d => d.building != null && d.defName.IndexOf("Lectern", StringComparison.OrdinalIgnoreCase) >= 0;
                case "column": return d => d.building != null && d.defName.IndexOf("Column", StringComparison.OrdinalIgnoreCase) >= 0 && d.passability != Traversability.Impassable;
                case "drape": return d => d.building != null && d.defName.IndexOf("Drape", StringComparison.OrdinalIgnoreCase) >= 0;
                case "toolcabinet": return d => d.building != null && d.defName.IndexOf("ToolCabinet", StringComparison.OrdinalIgnoreCase) >= 0;
                case "stonecutter": return d => d.IsWorkTable && d.AllRecipes != null && d.AllRecipes.Any(r => r.ProducedThingDef?.stuffProps?.categories != null && r.ProducedThingDef.stuffProps.categories.Contains(StuffCategoryDefOf.Stony));
                case "fabrication": return d => d.IsWorkTable && d.AllRecipes != null && d.AllRecipes.Any(r => r.ProducedThingDef == ThingDefOf.ComponentIndustrial);
                case "gestator": return d => d.thingClass != null && typeof(Building_MechGestator).IsAssignableFrom(d.thingClass);
                case "recharger": return d => d.thingClass != null && typeof(Building_MechCharger).IsAssignableFrom(d.thingClass);
                case "geneassembler": return d => d.thingClass != null && typeof(Building_GeneAssembler).IsAssignableFrom(d.thingClass);
                case "genebank": return d => d.comps != null && d.comps.Any(c => c is CompProperties_GenepackContainer) && !(d.thingClass != null && typeof(Building_GeneAssembler).IsAssignableFrom(d.thingClass));
                case "holding": return d => d.thingClass != null && typeof(Building_HoldingPlatform).IsAssignableFrom(d.thingClass);
                case "inhibitor": return d => d.building != null && d.defName.IndexOf("Inhibitor", StringComparison.OrdinalIgnoreCase) >= 0;
                case "comms": return d => d.thingClass != null && typeof(Building_CommsConsole).IsAssignableFrom(d.thingClass);
                case "tradebeacon": return d => d.thingClass != null && typeof(Building_OrbitalTradeBeacon).IsAssignableFrom(d.thingClass);
                case "washer": return d => HomeLife.ClassIs(d.thingClass, "DubsBadHygiene", "Building_WashingMachine");
                case "trough": return d => HomeLife.ClassIs(d.thingClass, "DubsBadHygiene", "Building_WaterTrough");
                case "wall": return d => d.IsWall && d.size.x == 1 && d.size.z == 1 && d.MadeFromStuff;
                case "door": return d => d.IsDoor && d.size.x == 1 && d.size.z == 1;
                // Mod furniture (toilets, baths, sauna heaters): Integrations/HomeLife.cs.
                default: return HomeLife.RolePredicate(role);
            }
        }

        /// <summary>Roles a saved room's furniture is recognized by, so it still works when the exact piece is gone (most specific first).</summary>
        public static readonly string[] InferableRoles =
        {
            "medical", "crib", "bed", "doublebed", "animalbed", "throne", "grave", "light", "heater", "cooler", "table", "chair", "endtable",
            "dresser", "plantpot", "bookcase", "research", "cook", "butcher", "stonecutter", "fabrication", "tailor", "drugs", "craft", "art",
            "hydroponics", "fermenter", "battery", "music", "monitor", "school", "toybox", "storage", "joy"
        };

        /// <summary>
        /// Picks concrete furniture for this colony: the first preferred def it can build that fits, else
        /// anything with the same purpose and footprint. Missing optional pieces are dropped, and so are pieces the chosen
        /// style leaves out; the lavish style adds plant pots along free stretches of wall. The planner's choices (another
        /// variant, another material, the style) are applied when <paramref name="config"/> is given.
        /// </summary>
        public List<ResolvedThing> Resolve(Map map, out List<string> missing, RoomPlanConfig config = null)
        {
            var list = new List<ResolvedThing>();
            missing = new List<string>();
            var style = config?.style ?? RoomStyle.Comfortable;
            var groupCounts = new Dictionary<string, int>();
            foreach (var t in things)
            {
                if (!t.InStyle(style)) continue;
                groupCounts.TryGetValue(t.GroupKey, out int n);
                groupCounts[t.GroupKey] = n + 1;
            }
            for (int i = 0; i < things.Count; i++)
            {
                var t = things[i];
                if (!t.InStyle(style)) continue;
                ThingDef pick = DefaultPick(t, map, out CellRect rect);
                if (pick == null)
                {
                    if (!t.optional)
                    {
                        var known = t.Defs.FirstOrDefault();
                        missing.Add(known != null ? known.label : ("CC_Role_" + t.role).Translate().Resolve());
                    }
                    continue;
                }
                if (config != null && config.variant.TryGetValue(t.GroupKey, out var chosen) && chosen != pick
                    && Avail.CanBuild(chosen, map) && Fits(t, chosen, out var chosenRect))
                {
                    pick = chosen;
                    rect = chosenRect;
                }
                list.Add(new ResolvedThing
                {
                    def = pick,
                    stuff = StuffFor(t, pick, map, config, groupCounts.TryGetValue(t.GroupKey, out int units) ? units : 1),
                    rect = rect,
                    rot = t.rot,
                    inWall = t.inWall,
                    group = t.GroupKey,
                    slot = i
                });
            }
            if (style == RoomStyle.Lavish && missing.Count == 0) AddDecor(list, map, config);
            return list;
        }

        /// <summary>
        /// The material for a piece: what the player picked (this time, or last time if there's still enough of it), what a
        /// saved room was built of, else whatever the colony has enough of and can spare most easily.
        /// </summary>
        private ThingDef StuffFor(TemplateThing t, ThingDef pick, Map map, RoomPlanConfig config, int units)
        {
            if (!pick.MadeFromStuff) return null;
            string key = t.GroupKey;
            if (config != null && config.stuff.TryGetValue(key, out var s) && s != null && Avail.StuffAllowed(pick, s)
                && (config.pickedNow.Contains(key) || MaterialPicker.Affordable(pick, s, units, map)))
                return s;
            // Partition walls and inner doors match the room's own.
            if (config != null && t.role == "wall")
            {
                var w = config.WallStuff(this, map);
                if (w != null && Avail.StuffAllowed(pick, w)) return w;
            }
            if (!t.stuff.NullOrEmpty())
            {
                var hint = DefDatabase<ThingDef>.GetNamedSilentFail(t.stuff);
                if (hint != null && Avail.StuffAllowed(pick, hint) && MaterialPicker.Affordable(pick, hint, units, map)) return hint;
            }
            var use = t.role == "wall" ? MaterialUse.Wall : t.role == "door" ? MaterialUse.Door : MaterialUse.Furniture;
            return MaterialPicker.Pick(pick, map, units, use);
        }

        /// <summary>The lavish style: plant pots in free corners and along walls, never in the way of the door, a work spot or a bed.</summary>
        private void AddDecor(List<ResolvedThing> list, Map map, RoomPlanConfig config)
        {
            var pot = DefaultPick(TemplateThing.AutoDecor, map, out _);
            if (pot == null || pot.size.x != 1 || pot.size.z != 1) return;
            int door = config != null ? config.DoorX(this) : DoorX;
            var boxes = new List<TemplateLayout.Box>();
            foreach (var r in list)
            {
                if (r.inWall) continue;
                var box = new TemplateLayout.Box(r.rect.minX, r.rect.minZ, r.rect.Width, r.rect.Height, IsPassable(r.def));
                if (r.def.hasInteractionCell)
                {
                    var root = RootOf(r);
                    TemplateLayout.RotateOffset(r.def.interactionCellOffset.x, r.def.interactionCellOffset.z, r.rot.AsInt, out int ox, out int oz);
                    box.ix = root.x + ox;
                    box.iz = root.z + oz;
                }
                boxes.Add(box);
            }
            int existing = list.Count(r => Avail.IsPlantPot(r.def));
            int max = Mathf.Clamp(size.x * size.z / 12, 1, 4) - existing / 2;
            var spots = TemplateLayout.DecorSpots(size.x, size.z, door, boxes, max);
            string group = TemplateThing.AutoDecor.GroupKey;
            ThingDef stuff = pot.MadeFromStuff ? StuffFor(TemplateThing.AutoDecor, pot, map, config, spots.Count) : null;
            foreach (var (x, z) in spots)
            {
                list.Add(new ResolvedThing
                {
                    def = pot,
                    stuff = stuff,
                    rect = new CellRect(x, z, 1, 1),
                    rot = Rot4.North,
                    group = group,
                    slot = -1
                });
            }
        }

        /// <summary>Things people walk over or sit in: they don't block the way through a room.</summary>
        public static bool IsPassable(ThingDef d) =>
            d != null && (d.passability == Traversability.Standable || Avail.IsChair(d) || d.IsDoor);

        private static IntVec2 RootOf(ResolvedThing r)
        {
            TemplateLayout.RootFor(r.rect.minX, r.rect.minZ, r.rect.Width, r.rect.Height, r.rot.AsInt, r.def.size.x, r.def.size.z, out int x, out int z);
            return new IntVec2(x, z);
        }

        private ThingDef DefaultPick(TemplateThing t, Map map, out CellRect rect)
        {
            rect = default;
            var known = t.Defs.ToList();
            IntVec2 wantedSize = known.Count > 0 ? known[0].size : new IntVec2(1, 1);
            foreach (var d in known)
            {
                if (Avail.CanBuild(d, map) && Fits(t, d, out rect)) return d;
            }
            if (!t.role.NullOrEmpty())
            {
                var pred = RolePredicate(t.role);
                if (pred != null)
                {
                    var d = Avail.BestBuildable(map, x => pred(x) && x.size == wantedSize);
                    if (d != null && Fits(t, d, out rect)) return d;
                }
            }
            return null;
        }

        /// <summary>Everything the colony could build in this spot instead: the preferred defs and anything with the same purpose and footprint.</summary>
        public List<ThingDef> VariantsFor(TemplateThing t, Map map)
        {
            var result = new List<ThingDef>();
            var known = t.Defs.ToList();
            IntVec2 wantedSize = known.Count > 0 ? known[0].size : new IntVec2(1, 1);
            foreach (var d in known)
            {
                if (!result.Contains(d) && Avail.CanBuild(d, map) && Fits(t, d, out _)) result.Add(d);
            }
            var pred = t.role.NullOrEmpty() ? null : RolePredicate(t.role);
            if (pred != null)
            {
                foreach (var d in Avail.AllBuildings)
                {
                    if (result.Contains(d) || d.size != wantedSize || !pred(d)) continue;
                    if (Avail.CanBuild(d, map) && Fits(t, d, out _)) result.Add(d);
                }
            }
            return result;
        }

        public TemplateThing ThingInGroup(string group) =>
            things.FirstOrDefault(t => t.GroupKey == group) ?? (group == TemplateThing.AutoDecor.GroupKey ? TemplateThing.AutoDecor : null);

        [Unsaved] private Texture2D cachedIcon;
        [Unsaved] private bool iconResolved;

        /// <summary>Icon for menus: the first <see cref="iconDefs"/> entry that exists, else the first furniture piece, else a wall.</summary>
        public Texture2D Icon
        {
            get
            {
                if (!iconResolved)
                {
                    iconResolved = true;
                    ThingDef d = null;
                    if (iconDefs != null)
                        d = iconDefs.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(x => x != null && x.uiIcon != null && x.uiIcon != BaseContent.BadTex);
                    if (d == null)
                        d = things.Where(x => !x.inWall).SelectMany(x => x.Defs).FirstOrDefault(x => x.uiIcon != null && x.uiIcon != BaseContent.BadTex);
                    cachedIcon = d?.uiIcon ?? ThingDefOf.Wall.uiIcon;
                }
                return cachedIcon;
            }
        }

        /// <summary>
        /// Footprint in interior coordinates (or full coordinates for in-wall things), if it fits. A piece that sticks out
        /// by one cell (a bigger bench from a mod, a slip in a template) is nudged back inside.
        /// </summary>
        private bool Fits(TemplateThing t, ThingDef d, out CellRect rect)
        {
            rect = GenAdj.OccupiedRect(new IntVec3(t.pos.x, 0, t.pos.z), t.rot, d.size);
            if (t.inWall)
                return rect.minX >= 0 && rect.minZ >= 0 && rect.maxX < FullWidth && rect.maxZ < FullHeight;
            if (rect.minX >= 0 && rect.minZ >= 0 && rect.maxX < size.x && rect.maxZ < size.z) return true;
            if (!TemplateLayout.NudgeInside(rect.minX, rect.minZ, rect.Width, rect.Height, size.x, size.z, out int dx, out int dz)) return false;
            rect = new CellRect(rect.minX + dx, rect.minZ + dz, rect.Width, rect.Height);
            return true;
        }

        /// <summary>The template's default floor: the first of <see cref="floors"/> the colony can build and afford.</summary>
        public TerrainDef ResolveFloor(Map map) => ResolveFloor(floors, map);

        private static readonly List<string> DefaultLavishFloors = new List<string>
        {
            "CarpetRed", "CarpetGreen", "CarpetBlue", "CarpetCream", "CarpetDark", "TileMarble", "TileGranite", "TileSlate", "TileLimestone", "TileSandstone"
        };

        /// <summary>The lavish style's floor: carpet or fine tiles where the colony can lay them; a clean room keeps its clean floor.</summary>
        public TerrainDef ResolveLavishFloor(Map map)
        {
            if (NeedsCleanFloor) return ResolveFloor(map);
            return ResolveFloor(lavishFloors ?? DefaultLavishFloors, map) ?? ResolveFloor(map);
        }

        private static TerrainDef ResolveFloor(List<string> names, Map map)
        {
            if (names == null) return null;
            List<TerrainDef> listed = null;
            foreach (var name in names)
            {
                var f = DefDatabase<TerrainDef>.GetNamedSilentFail(name);
                if (f == null) continue;
                if (Avail.CanBuild(f, map) && Avail.MaterialsWithinReach(f, map)) return f;
                (listed ??= new List<TerrainDef>()).Add(f);
            }
            // A floor mod (Stuffed Floors) may have retired the listed floors for its own versions: same material, new def.
            if (listed != null)
                foreach (var f in listed)
                {
                    var sub = FloorSubstitute(f, map);
                    if (sub != null) return sub;
                }
            return null;
        }

        private static readonly Dictionary<TerrainDef, (float time, Map map, TerrainDef sub)> substitutes = new Dictionary<TerrainDef, (float, Map, TerrainDef)>();

        /// <summary>A buildable floor made of the same material as <paramref name="f"/>, as close to it in beauty as there is.</summary>
        private static TerrainDef FloorSubstitute(TerrainDef f, Map map)
        {
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (substitutes.TryGetValue(f, out var cached) && cached.map == map && now - cached.time < 5f) return cached.sub;
            TerrainDef best = null;
            var material = f.costList?.FirstOrDefault()?.thingDef;
            if (material != null)
            {
                float beauty = f.GetStatValueAbstract(StatDefOf.Beauty);
                float bestGap = float.MaxValue;
                foreach (var t in DefDatabase<TerrainDef>.AllDefsListForReading)
                {
                    if (t == f || t.designationCategory == null || t.costList == null || !t.costList.Any(c => c.thingDef == material)) continue;
                    if (!Avail.CanBuild(t, map) || !Avail.MaterialsWithinReach(t, map)) continue;
                    float gap = System.Math.Abs(t.GetStatValueAbstract(StatDefOf.Beauty) - beauty);
                    if (gap < bestGap)
                    {
                        best = t;
                        bestGap = gap;
                    }
                }
            }
            substitutes[f] = (now, map, best);
            return best;
        }

        [Unsaved] private Map cachedMap;
        [Unsaved] private float cachedTime = -999f;
        [Unsaved] private List<ResolvedThing> cachedThings;
        [Unsaved] private string cachedReason;

        /// <summary>Resolve() for UI code, cached for a couple of seconds so tabs don't redo it every frame.</summary>
        public List<ResolvedThing> ResolveCached(Map map, out string unavailableReason)
        {
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (cachedMap != map || now - cachedTime > 2f + (shortHash % 64) / 64f || cachedThings == null)
            {
                cachedMap = map;
                cachedTime = now;
                if (!Avail.CanBuild(ThingDefOf.Wall, map))
                {
                    cachedThings = new List<ResolvedThing>();
                    cachedReason = "CC_Template_NoWalls".Translate();
                }
                else
                {
                    cachedThings = Resolve(map, out var missing);
                    cachedReason = missing.Count > 0 ? "CC_Template_Missing".Translate(TextUtil.JoinAnd(missing)).Resolve() : null;
                    if (cachedReason == null && !EraFits)
                        cachedReason = (era == "lowtech" ? "CC_Template_ForLowTech" : "CC_Template_ForIndustrial").Translate().Resolve();
                }
            }
            unavailableReason = cachedReason;
            return cachedThings;
        }

        /// <summary>Why the colony can't use this template right now, or null if it can.</summary>
        public string UnavailableReason(Map map)
        {
            ResolveCached(map, out var reason);
            return reason;
        }

        /// <summary>"bedroom, roomy" for a variant of a room type, else the label.</summary>
        public string FullLabel
        {
            get
            {
                var type = RoomTypeDef.Of(this);
                string v = type?.VariantLabel(this);
                return v.NullOrEmpty() || type.Variants.Count < 2 ? label : "CC_Rooms_VariantFull".Translate(label, v).Resolve();
            }
        }
    }

    public class ResolvedThing
    {
        public ThingDef def;
        public ThingDef stuff;
        /// <summary>Interior coordinates, or full coordinates when <see cref="inWall"/>.</summary>
        public CellRect rect;
        public Rot4 rot;
        public bool inWall;
        public string group;
        public int slot;
    }
}
