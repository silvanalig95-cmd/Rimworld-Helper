using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
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

        /// <summary>Things that share the same spec are configured together in the planner (all the beds, all the chairs).</summary>
        public string GroupKey => (role ?? "") + "|" + (defs == null ? "" : string.Join(",", defs));
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

        public int FullWidth => size.x + 2;
        public int FullHeight => size.z + 2;
        public int DoorX => doorX >= 0 && doorX < size.x ? doorX : size.x / 2;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (size.x < 1 || size.z < 1) yield return "size must be at least 1x1";
            foreach (var t in things)
            {
                if ((t.defs == null || t.defs.Count == 0) && t.role.NullOrEmpty()) yield return "a template thing has neither defs nor role";
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
                // Mod furniture (toilets, baths, sauna heaters): Integrations/HomeLife.cs.
                default: return HomeLife.RolePredicate(role);
            }
        }

        /// <summary>
        /// Picks concrete furniture for this colony: the first preferred def it can build that fits, else
        /// anything with the same purpose and footprint. Missing optional pieces are dropped. The planner's
        /// choices (another variant, another material) are applied when <paramref name="config"/> is given.
        /// </summary>
        public List<ResolvedThing> Resolve(Map map, out List<string> missing, RoomPlanConfig config = null)
        {
            var list = new List<ResolvedThing>();
            missing = new List<string>();
            for (int i = 0; i < things.Count; i++)
            {
                var t = things[i];
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
                ThingDef stuff = null;
                if (pick.MadeFromStuff)
                {
                    if (config != null && config.stuff.TryGetValue(t.GroupKey, out var s) && s != null && Avail.StuffAllowed(pick, s)) stuff = s;
                    else stuff = Avail.BestStuff(pick, map);
                }
                list.Add(new ResolvedThing
                {
                    def = pick,
                    stuff = stuff,
                    rect = rect,
                    rot = t.rot,
                    inWall = t.inWall,
                    group = t.GroupKey,
                    slot = i
                });
            }
            return list;
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

        public TemplateThing ThingInGroup(string group) => things.FirstOrDefault(t => t.GroupKey == group);

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

        /// <summary>Footprint in interior coordinates (or full coordinates for in-wall things), if it fits.</summary>
        private bool Fits(TemplateThing t, ThingDef d, out CellRect rect)
        {
            rect = GenAdj.OccupiedRect(new IntVec3(t.pos.x, 0, t.pos.z), t.rot, d.size);
            if (t.inWall)
                return rect.minX >= 0 && rect.minZ >= 0 && rect.maxX < FullWidth && rect.maxZ < FullHeight;
            return rect.minX >= 0 && rect.minZ >= 0 && rect.maxX < size.x && rect.maxZ < size.z;
        }

        /// <summary>The template's default floor: the first of <see cref="floors"/> the colony can build and afford.</summary>
        public TerrainDef ResolveFloor(Map map)
        {
            if (floors == null) return null;
            List<TerrainDef> listed = null;
            foreach (var name in floors)
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
