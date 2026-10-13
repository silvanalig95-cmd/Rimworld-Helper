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
        /// for plant pots, bedside tables, dressers, columns, drapes, pedestals and rugs, core for everything else.
        /// </summary>
        public string tier;
        /// <summary>A material to prefer (a saved room remembers what it was built of), used when the colony has enough.</summary>
        public string stuff;

        public string Tier => TemplateLayout.TierOf(tier, role);

        /// <summary>Part of a room in this style. Pieces the room can't do without are always in.</summary>
        public bool InStyle(RoomStyle style) => TemplateLayout.InStyle(tier, role, optional, (int)style);

        /// <summary>Things that share the same spec are configured together in the planner (all the beds, all the chairs).</summary>
        public string GroupKey => (role ?? "") + "|" + (defs == null ? "" : string.Join(",", defs));

        /// <summary>The plant pots the lavish style adds along the walls.</summary>
        public static readonly TemplateThing AutoDecor = new TemplateThing { defs = new List<string> { "PlantPot" }, role = "plantpot", tier = "lavish" };

        /// <summary>The rug the lavish style lays on open floor, where a mod provides one that can be built.</summary>
        public static readonly TemplateThing AutoRug = new TemplateThing
        {
            defs = new List<string> { "DankPyon_DropDownSilkRug3x3c", "AM_MesoamericanRug_Medium", "DankPyon_RugCougar2x2c" },
            role = "rug",
            tier = "lavish"
        };
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
        /// <summary>The lavish style lays a rug on the open floor if a mod provides one (off for rooms that need the floor clear).</summary>
        public bool rug = true;

        /// <summary>A room the player saved from the map (Your rooms); not a def in the database.</summary>
        [Unsaved] public CustomRoomData custom;
        /// <summary>The wall material a saved room was built of, preferred when there's enough of it.</summary>
        [Unsaved] public string wallStuffHint;
        /// <summary>What the last <see cref="Resolve"/> had to change to make the layout work (turned or left-out pieces).</summary>
        [Unsaved] public List<string> layoutNotes = new List<string>();

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
                case "pew": return d => d.building != null && d.building.isSittable && d.defName.IndexOf("Pew", StringComparison.OrdinalIgnoreCase) >= 0;
                case "column": return d => d.building != null && d.defName.IndexOf("Column", StringComparison.OrdinalIgnoreCase) >= 0 && d.passability != Traversability.Impassable;
                case "drape": return d => d.building != null && d.defName.IndexOf("Drape", StringComparison.OrdinalIgnoreCase) >= 0;
                case "pedestal": return d => d.building != null && (d.defName.IndexOf("Plinth", StringComparison.OrdinalIgnoreCase) >= 0 || d.defName.IndexOf("Pedestal", StringComparison.OrdinalIgnoreCase) >= 0);
                case "rug": return d => d.building != null && d.passability == Traversability.Standable && d.defName.IndexOf("Rug", StringComparison.OrdinalIgnoreCase) >= 0;
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
                case "campfire": return d => d.defName.IndexOf("Campfire", StringComparison.OrdinalIgnoreCase) >= 0 && Avail.IsHeater(d);
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
        /// Picks concrete furniture for this colony: the first preferred def it can build that fits, else anything with
        /// the same purpose and footprint. Missing optional pieces are dropped, and so are pieces the chosen style leaves
        /// out. Then the layout is checked as a whole (<see cref="TemplateLayout.Arrange"/>): a piece whose work spot would
        /// face a wall is turned, one that fits nowhere is left out. The lavish style adds plant pots along free stretches
        /// of wall and a rug. The planner's choices (another variant, another material, the style, the door) are applied
        /// when <paramref name="config"/> is given.
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
            var picked = new List<(TemplateThing t, ThingDef def, int slot)>();
            for (int i = 0; i < things.Count; i++)
            {
                var t = things[i];
                if (!t.InStyle(style)) continue;
                ThingDef pick = DefaultPick(t, map);
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
                    && Avail.CanBuild(chosen, map) && Fits(t, chosen))
                    pick = chosen;
                picked.Add((t, pick, i));
            }

            int door = config != null ? config.DoorX(this) : DoorX;
            var pieces = picked.Select(p => PieceFor(p.t, p.def, p.slot)).ToList();
            layoutNotes = TemplateLayout.Arrange(size.x, size.z, door, pieces);

            // What the walls, door and floor of this plan use comes off the stock first, and each group of furniture
            // takes its share in turn, so no two parts of the room count on the same blocks.
            var reserved = new Dictionary<ThingDef, int>();
            if (config != null && map != null)
            {
                try
                {
                    var walls = config.WallStuff(this, map);
                    MaterialPicker.Reserve(reserved, ThingDefOf.Wall, walls, RoomPlanConfig.WallCount(this));
                    var doorDef = config.DoorDef(map);
                    MaterialPicker.Reserve(reserved, doorDef, config.DoorStuff(doorDef, walls, map), 1);
                    var floor = config.Floor(this, map);
                    if (floor != null) MaterialPicker.Reserve(reserved, floor, config.FloorStuff(floor, map, size.x * size.z), size.x * size.z);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("room plan materials", e);
                }
            }
            var groupStuff = new Dictionary<(string, ThingDef), ThingDef>();
            for (int i = 0; i < picked.Count; i++)
            {
                var piece = pieces[i];
                if (piece.dropped) continue;
                var (t, def, slot) = picked[i];
                int units = groupCounts.TryGetValue(t.GroupKey, out int n) ? n : 1;
                if (!groupStuff.TryGetValue((t.GroupKey, def), out var stuff))
                {
                    stuff = StuffFor(t, def, map, config, units, reserved);
                    groupStuff[(t.GroupKey, def)] = stuff;
                    MaterialPicker.Reserve(reserved, def, stuff, units);
                }
                list.Add(new ResolvedThing
                {
                    def = def,
                    stuff = stuff,
                    rect = new CellRect(piece.minX, piece.minZ, piece.w, piece.h),
                    rot = new Rot4(piece.facing),
                    inWall = t.inWall,
                    group = t.GroupKey,
                    slot = slot
                });
            }
            if (style == RoomStyle.Lavish && missing.Count == 0) AddDecor(list, pieces.Where(p => !p.dropped).ToList(), map, config, door, reserved);
            return list;
        }

        /// <summary>The layout rules' view of a piece: its size, facing rules and work spots.</summary>
        public static TemplateLayout.Piece PieceFor(TemplateThing t, ThingDef d, int slot)
        {
            var p = new TemplateLayout.Piece
            {
                slot = slot,
                x = t.pos.x,
                z = t.pos.z,
                rot = t.rot.AsInt,
                sizeX = d.size.x,
                sizeZ = d.size.z,
                rotatable = d.rotatable,
                passable = IsPassable(d),
                optional = t.optional,
                inWall = t.inWall,
                isDoor = d.IsDoor,
                role = t.role
            };
            if (d.hasInteractionCell) p.spots.Add((d.interactionCellOffset.x, d.interactionCellOffset.z));
            if (d.multipleInteractionCellOffsets != null)
                foreach (var o in d.multipleInteractionCellOffsets)
                    if (!p.spots.Contains((o.x, o.z))) p.spots.Add((o.x, o.z));
            return p;
        }

        /// <summary>
        /// The material for a piece: what the player picked (this time, or last time if there's still enough of it), what a
        /// saved room was built of, else whatever the colony has enough of and can spare most easily.
        /// </summary>
        private ThingDef StuffFor(TemplateThing t, ThingDef pick, Map map, RoomPlanConfig config, int units, Dictionary<ThingDef, int> reserved = null)
        {
            if (!pick.MadeFromStuff) return null;
            string key = t.GroupKey;
            var use = UseOf(t);
            if (config != null && config.stuff.TryGetValue(key, out var s) && s != null && Avail.StuffAllowed(pick, s) && MaterialPicker.Allowed(s, use)
                && (config.pickedNow.Contains(key) || MaterialPicker.Affordable(pick, s, units, map)))
                return s;
            // Partition walls and inner doors match the room's own.
            if (config != null && use != MaterialUse.Furniture)
            {
                var w = config.WallStuff(this, map);
                if (w != null && Avail.StuffAllowed(pick, w) && MaterialPicker.Allowed(w, use)) return w;
            }
            if (!t.stuff.NullOrEmpty())
            {
                var hint = DefDatabase<ThingDef>.GetNamedSilentFail(t.stuff);
                if (hint != null && Avail.StuffAllowed(pick, hint) && MaterialPicker.Allowed(hint, use) && MaterialPicker.Affordable(pick, hint, units, map)) return hint;
            }
            return MaterialPicker.Pick(pick, map, units, use, reserved);
        }

        /// <summary>Walls and doors inside a room (a partition, a second door) are built like the room's own: never of bone.</summary>
        public static MaterialUse UseOf(TemplateThing t) =>
            t == null ? MaterialUse.Furniture : t.role == "wall" ? MaterialUse.Wall : t.role == "door" ? MaterialUse.Door : MaterialUse.Furniture;

        /// <summary>
        /// The lavish style: a rug on open floor where a mod provides one, and plant pots in free corners and along walls,
        /// never in the way of the door, a work spot or a bed.
        /// </summary>
        private void AddDecor(List<ResolvedThing> list, List<TemplateLayout.Piece> pieces, Map map, RoomPlanConfig config, int door, Dictionary<ThingDef, int> reserved)
        {
            if (rug)
            {
                var rugDef = DefaultPick(TemplateThing.AutoRug, map);
                if (rugDef != null && TemplateLayout.RugSpot(size.x, size.z, door, pieces, rugDef.size.x, rugDef.size.z, out int rx, out int rz))
                {
                    var rugPiece = PieceFor(TemplateThing.AutoRug, rugDef, -1);
                    rugPiece.minX = rx;
                    rugPiece.minZ = rz;
                    rugPiece.w = rugDef.size.x;
                    rugPiece.h = rugDef.size.z;
                    rugPiece.facing = 0;
                    if (TemplateLayout.RootFor(rx, rz, rugPiece.w, rugPiece.h, 0, rugDef.size.x, rugDef.size.z, out _, out _))
                    {
                        pieces.Add(rugPiece);
                        list.Add(new ResolvedThing
                        {
                            def = rugDef,
                            stuff = rugDef.MadeFromStuff ? StuffFor(TemplateThing.AutoRug, rugDef, map, config, 1, reserved) : null,
                            rect = new CellRect(rx, rz, rugPiece.w, rugPiece.h),
                            rot = Rot4.North,
                            group = TemplateThing.AutoRug.GroupKey,
                            slot = -1
                        });
                    }
                }
            }
            var pot = DefaultPick(TemplateThing.AutoDecor, map);
            if (pot == null || pot.size.x != 1 || pot.size.z != 1) return;
            int existing = list.Count(r => Avail.IsPlantPot(r.def));
            int max = Mathf.Clamp(size.x * size.z / 12, 1, 4) - existing / 2;
            var spots = TemplateLayout.DecorSpots(size.x, size.z, door, pieces, max);
            string group = TemplateThing.AutoDecor.GroupKey;
            ThingDef stuff = pot.MadeFromStuff ? StuffFor(TemplateThing.AutoDecor, pot, map, config, spots.Count, reserved) : null;
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

        private ThingDef DefaultPick(TemplateThing t, Map map)
        {
            var known = t.Defs.ToList();
            IntVec2 wantedSize = known.Count > 0 ? known[0].size : new IntVec2(1, 1);
            foreach (var d in known)
            {
                if (Avail.CanBuild(d, map) && Fits(t, d)) return d;
            }
            if (!t.role.NullOrEmpty())
            {
                var pred = RolePredicate(t.role);
                if (pred != null)
                {
                    var d = Avail.BestBuildable(map, x => pred(x) && x.size == wantedSize);
                    if (d != null && Fits(t, d)) return d;
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
                if (!result.Contains(d) && Avail.CanBuild(d, map) && Fits(t, d)) result.Add(d);
            }
            var pred = t.role.NullOrEmpty() ? null : RolePredicate(t.role);
            if (pred != null)
            {
                foreach (var d in Avail.AllBuildings)
                {
                    if (result.Contains(d) || d.size != wantedSize || !pred(d)) continue;
                    if (Avail.CanBuild(d, map) && Fits(t, d)) result.Add(d);
                }
            }
            return result;
        }

        public TemplateThing ThingInGroup(string group) =>
            things.FirstOrDefault(t => t.GroupKey == group)
            ?? (group == TemplateThing.AutoDecor.GroupKey ? TemplateThing.AutoDecor : group == TemplateThing.AutoRug.GroupKey ? TemplateThing.AutoRug : null);

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
        /// The piece fits the room at its spot (in the wall ring for in-wall things). One that sticks out by a cell (a
        /// bigger bench from a mod, a slip in a template) counts: the layout nudges it back inside.
        /// </summary>
        private bool Fits(TemplateThing t, ThingDef d)
        {
            int rot = d.rotatable ? t.rot.AsInt : 0;
            TemplateLayout.OccupiedRect(t.pos.x, t.pos.z, rot, d.size.x, d.size.z, out int minX, out int minZ, out int w, out int h);
            if (t.inWall)
                return minX >= 0 && minZ >= 0 && minX + w <= FullWidth && minZ + h <= FullHeight;
            if (minX >= 0 && minZ >= 0 && minX + w <= size.x && minZ + h <= size.z) return true;
            return TemplateLayout.NudgeInside(minX, minZ, w, h, size.x, size.z, out _, out _);
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

        /// <summary>The floor for a style: none for spartan (unless the room has to stay clean), the template's own, or a finer one for lavish.</summary>
        public TerrainDef FloorFor(RoomStyle style, Map map)
        {
            switch (style)
            {
                case RoomStyle.Spartan: return NeedsCleanFloor ? ResolveFloor(map) : null;
                case RoomStyle.Lavish: return ResolveLavishFloor(map);
                default: return ResolveFloor(map);
            }
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

        /// <summary>
        /// "dining room (for 8)" for a size of a room type, else the label. Left as it is when the label already says it
        /// ("roomy bedroom") or the size is the standard one.
        /// </summary>
        public string FullLabel
        {
            get
            {
                var type = RoomTypeDef.Of(this);
                string v = type?.VariantLabel(this);
                if (v.NullOrEmpty() || type.Variants.Count < 2 || label.NullOrEmpty()) return label;
                if (v == "CC_Rooms_Standard".Translate().ToString() || label.IndexOf(v, StringComparison.OrdinalIgnoreCase) >= 0) return label;
                return "CC_Rooms_VariantFull".Translate(label, v).Resolve();
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
