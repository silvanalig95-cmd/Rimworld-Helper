using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One piece of furniture in a room template.</summary>
    public class TemplateThing
    {
        /// <summary>Preferred defs, best first. The first one the colony can build is used.</summary>
        public List<ThingDef> defs = new List<ThingDef>();
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
    }

    /// <summary>A simple rectangular room: walls, one door, furniture.</summary>
    public class RoomTemplateDef : Def
    {
        /// <summary>Interior floor size (walls are added around it).</summary>
        public IntVec2 size = new IntVec2(4, 4);
        /// <summary>Door position along the south wall in interior x; -1 = middle.</summary>
        public int doorX = -1;
        public List<TemplateThing> things = new List<TemplateThing>();
        /// <summary>Optional floor, best first. Skipped if nothing is buildable.</summary>
        public List<TerrainDef> floors;
        public int order = 100;
        [MustTranslate] public string purpose;

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
                default: return null;
            }
        }

        /// <summary>
        /// Picks concrete furniture for this colony: the first preferred def it can build that fits, else
        /// anything with the same purpose and footprint. Missing optional pieces are dropped.
        /// </summary>
        public List<ResolvedThing> Resolve(Map map, out List<string> missing)
        {
            var list = new List<ResolvedThing>();
            missing = new List<string>();
            foreach (var t in things)
            {
                ThingDef pick = null;
                CellRect rect = default;
                IntVec2 wantedSize = t.defs != null && t.defs.Count > 0 && t.defs[0] != null ? t.defs[0].size : new IntVec2(1, 1);
                if (t.defs != null)
                {
                    foreach (var d in t.defs)
                    {
                        if (d != null && Avail.CanBuild(d, map) && Fits(t, d, out rect))
                        {
                            pick = d;
                            break;
                        }
                    }
                }
                if (pick == null && !t.role.NullOrEmpty())
                {
                    var pred = RolePredicate(t.role);
                    if (pred != null)
                    {
                        var d = Avail.BestBuildable(map, x => pred(x) && x.size == wantedSize);
                        if (d != null && Fits(t, d, out rect)) pick = d;
                    }
                }
                if (pick == null)
                {
                    if (!t.optional) missing.Add(t.defs != null && t.defs.Count > 0 && t.defs[0] != null ? t.defs[0].label : t.role);
                    continue;
                }
                list.Add(new ResolvedThing
                {
                    def = pick,
                    stuff = pick.MadeFromStuff ? Avail.BestStuff(pick, map) : null,
                    rect = rect,
                    rot = t.rot,
                    inWall = t.inWall
                });
            }
            return list;
        }

        /// <summary>Footprint in interior coordinates (or full coordinates for in-wall things), if it fits.</summary>
        private bool Fits(TemplateThing t, ThingDef d, out CellRect rect)
        {
            rect = GenAdj.OccupiedRect(new IntVec3(t.pos.x, 0, t.pos.z), t.rot, d.size);
            if (t.inWall)
                return rect.minX >= 0 && rect.minZ >= 0 && rect.maxX < FullWidth && rect.maxZ < FullHeight;
            return rect.minX >= 0 && rect.minZ >= 0 && rect.maxX < size.x && rect.maxZ < size.z;
        }

        public TerrainDef ResolveFloor(Map map)
        {
            if (floors == null) return null;
            foreach (var f in floors)
            {
                if (f != null && Avail.CanBuild(f, map) && Avail.MaterialsWithinReach(f, map)) return f;
            }
            return null;
        }

        /// <summary>Why the colony can't use this template right now, or null if it can.</summary>
        public string UnavailableReason(Map map)
        {
            if (!Avail.CanBuild(ThingDefOf.Wall, map)) return "CC_Template_NoWalls".Translate();
            Resolve(map, out var missing);
            if (missing.Count > 0) return "CC_Template_Missing".Translate(TextUtil.JoinAnd(missing));
            return null;
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
    }
}
