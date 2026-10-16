using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A theme's furniture for one purpose (bed, chair, light, ...), best first. Names are looked up silently.</summary>
    public class ThemeSwap
    {
        public string role;
        public List<string> defs = new List<string>();
    }

    /// <summary>
    /// A look a room plan can be built in (rustic, stately, spartan, cosy, tribal, spacer): the same layout with other
    /// furniture, materials and floor. It sits beside the plan's style, which decides how much goes in; the theme decides
    /// what it looks like. Only what the colony has unlocked is used: a piece the colony can't build yet, or a material
    /// it hasn't got enough of, is skipped for the next one on the list, and in the end for the plan's own choice. A
    /// piece is only swapped for one of the same shape (<see cref="ThemeMath.KeepsShape"/>), so the layout never changes.
    /// Mod furniture is listed by name and simply missing when the mod is off.
    /// </summary>
    public class RoomThemeDef : Def
    {
        public int order = 100;
        /// <summary>"industrial": not for tribal and medieval colonies; "lowtech": only for them. Empty: any colony.</summary>
        public string era;
        /// <summary>Wall (and door) materials, best first, by defName; then any of the categories; then the usual pick.</summary>
        public List<string> wallStuff;
        public List<string> wallStuffCategories;
        /// <summary>Furniture materials, best first, by defName; then any of the categories; then the usual pick.</summary>
        public List<string> furnitureStuff;
        public List<string> furnitureStuffCategories;
        /// <summary>Floors, best first. They replace the plan's own floor; a room without one (a greenhouse, a crypt) stays bare.</summary>
        public List<string> floors;
        /// <summary>Floors for the lavish style. Empty: <see cref="floors"/>.</summary>
        public List<string> lavishFloors;
        /// <summary>Doors, best first (an autodoor for a spacer room). Empty: the plan's own.</summary>
        public List<string> doors;
        public List<ThemeSwap> swaps = new List<ThemeSwap>();
        /// <summary>Rugs for the lavish style, before the usual ones.</summary>
        public List<string> rugs;
        /// <summary>Plant pots added along the walls, before the plain one.</summary>
        public List<string> pots;
        /// <summary>"comfortable": plant pots along the walls from the comfortable style on. Empty: lavish only, as without a theme.</summary>
        public string decorFrom;
        /// <summary>Building whose icon stands for the theme in the menu; the first one that exists.</summary>
        public List<string> iconDefs;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (!era.NullOrEmpty() && era != "lowtech" && era != "industrial") yield return "era must be lowtech or industrial";
            if (!decorFrom.NullOrEmpty() && decorFrom != "comfortable" && decorFrom != "lavish") yield return "decorFrom must be comfortable or lavish";
            if (swaps != null)
                foreach (var s in swaps)
                {
                    if (s == null || s.role.NullOrEmpty()) yield return "a swap has no role";
                    else if (!ThemeMath.Themable(s.role, false)) yield return "walls and doors can't be swapped (" + s.role + ")";
                    if (s != null && (s.defs == null || s.defs.Count == 0)) yield return "a swap lists no furniture";
                }
        }

        // ---------------- Which themes ----------------

        private static List<RoomThemeDef> all;

        /// <summary>Every theme, in menu order.</summary>
        public static List<RoomThemeDef> All => all ??= DefDatabase<RoomThemeDef>.AllDefsListForReading.OrderBy(t => t.order).ThenBy(t => t.defName).ToList();

        /// <summary>The colony's era fits this theme (no spacer rooms in a tribe, unless the player says otherwise).</summary>
        public bool EraFits
        {
            get
            {
                if (era.NullOrEmpty()) return true;
                bool low = Avail.IsPreIndustrial;
                return era == "lowtech" ? low : !low;
            }
        }

        [Unsaved] private int reachHour = -1;
        [Unsaved] private Map reachMap;
        [Unsaved] private bool reachable;

        /// <summary>Why the colony can't use this theme yet, or null if it can.</summary>
        public string UnavailableReason(Map map)
        {
            if (!EraFits) return (era == "lowtech" ? "CC_Theme_ForLowTech" : "CC_Theme_ForIndustrial").Translate().Resolve();
            if (!AnythingWithinReach(map)) return "CC_Theme_NothingYet".Translate().Resolve();
            return null;
        }

        /// <summary>
        /// Something of this theme is unlocked: a piece of its furniture the colony can build, a floor it can lay, or one
        /// of its materials in stock. Remembered for the in-game hour.
        /// </summary>
        public bool AnythingWithinReach(Map map)
        {
            if (map == null) return true;
            int hour = (Find.TickManager?.TicksGame ?? 0) / GenDate.TicksPerHour;
            if (hour == reachHour && map == reachMap) return reachable;
            reachHour = hour;
            reachMap = map;
            reachable = false;
            try
            {
                reachable = (swaps ?? new List<ThemeSwap>()).Where(s => s?.defs != null).SelectMany(s => s.defs)
                                .Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Any(d => d != null && Avail.CanBuild(d, map))
                            || (floors ?? new List<string>()).Concat(lavishFloors ?? new List<string>())
                                .Select(n => DefDatabase<TerrainDef>.GetNamedSilentFail(n)).Any(f => f != null && Avail.CanBuild(f, map))
                            || (wallStuff ?? new List<string>()).Concat(furnitureStuff ?? new List<string>())
                                .Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Any(s => s != null && MaterialCounts.Have(map, s) > 0);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("room theme " + defName, e);
                reachable = true;
            }
            return reachable;
        }

        // ---------------- Furniture ----------------

        [Unsaved] private Dictionary<string, List<string>> candidates;

        /// <summary>The theme's furniture for a purpose, best first; empty if the theme leaves it as the plan has it.</summary>
        public List<string> CandidatesFor(string role)
        {
            if (role.NullOrEmpty()) return new List<string>();
            candidates ??= new Dictionary<string, List<string>>();
            if (!candidates.TryGetValue(role, out var list))
                candidates[role] = list = ThemeMath.Candidates(role, (swaps ?? new List<ThemeSwap>()).Where(s => s != null).Select(s => (s.role, s.defs)));
            return list;
        }

        /// <summary>The layout rules' view of a building: its footprint, facing rules, whether people pass through, its work spots.</summary>
        public static ThemeMath.Shape ShapeOf(ThingDef d)
        {
            var shape = new ThemeMath.Shape
            {
                sizeX = d.size.x,
                sizeZ = d.size.z,
                rotatable = d.rotatable,
                passable = RoomTemplateDef.IsPassable(d),
                attached = WallLights.IsAttached(d),
                spots = new List<(int x, int z)>()
            };
            if (d.hasInteractionCell) shape.spots.Add((d.interactionCellOffset.x, d.interactionCellOffset.z));
            if (d.multipleInteractionCellOffsets != null)
                foreach (var o in d.multipleInteractionCellOffsets)
                    if (!shape.spots.Contains((o.x, o.z))) shape.spots.Add((o.x, o.z));
            return shape;
        }

        [Unsaved] private TemplateThing potThing, rugThing;

        /// <summary>The plant pots this theme adds along the walls: its own first, then the plain one.</summary>
        public TemplateThing PotThing => potThing ??= pots.NullOrEmpty()
            ? TemplateThing.AutoDecor
            : new TemplateThing { defs = pots.Concat(TemplateThing.AutoDecor.defs).Distinct().ToList(), role = "plantpot", tier = "lavish" };

        /// <summary>The rug the lavish style lays: the theme's own first, then the usual ones.</summary>
        public TemplateThing RugThing => rugThing ??= rugs.NullOrEmpty()
            ? TemplateThing.AutoRug
            : new TemplateThing { defs = rugs.Concat(TemplateThing.AutoRug.defs).Distinct().ToList(), role = "rug", tier = "lavish" };

        public int DecorFrom => ThemeMath.DecorFrom(decorFrom);

        /// <summary>The decor a plan in some theme added, found by its group (for the planner's material and variant menus).</summary>
        public static TemplateThing DecorThingFor(string group)
        {
            foreach (var t in All)
            {
                if (t.PotThing.GroupKey == group) return t.PotThing;
                if (t.RugThing.GroupKey == group) return t.RugThing;
            }
            return null;
        }

        // ---------------- Materials ----------------

        [Unsaved] private List<ThingDef> wallStuffDefs, furnitureStuffDefs;
        [Unsaved] private List<StuffCategoryDef> wallCats, furnitureCats;

        private static List<ThingDef> Things(List<string> names) =>
            names == null ? new List<ThingDef>() : names.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null && d.IsStuff).ToList();

        private static List<StuffCategoryDef> Cats(List<string> names) =>
            names == null ? new List<StuffCategoryDef>() : names.Select(n => DefDatabase<StuffCategoryDef>.GetNamedSilentFail(n)).Where(c => c != null).ToList();

        /// <summary>
        /// The theme's material for <paramref name="units"/> of <paramref name="def"/>: the first of its own materials the
        /// colony has enough of (after what the rest of the plan takes), else the best of its kinds of material that's in
        /// stock (never anything precious), else null for the usual pick. Walls never get bone, as everywhere.
        /// </summary>
        public ThingDef PickStuff(BuildableDef def, Map map, int units, MaterialUse use, Dictionary<ThingDef, int> reserved = null)
        {
            if (def == null || !def.MadeFromStuff || map == null) return null;
            bool shell = use == MaterialUse.Wall || use == MaterialUse.Door;
            var named = shell ? wallStuffDefs ??= Things(wallStuff) : furnitureStuffDefs ??= Things(furnitureStuff);
            var cats = shell ? wallCats ??= Cats(wallStuffCategories) : furnitureCats ??= Cats(furnitureStuffCategories);
            units = Mathf.Max(1, units);
            foreach (var s in named)
                if (Avail.StuffAllowed(def, s) && MaterialPicker.Allowed(s, use) && Covers(def, s, units, map, reserved)) return s;
            if (cats.Count == 0) return null;
            ThingDef best = null;
            int bestHave = 0;
            foreach (var s in Avail.AllowedStuffs(def))
            {
                if (s.stuffProps?.categories == null || !s.stuffProps.categories.Any(cats.Contains)) continue;
                if (!MaterialPicker.Allowed(s, use) || MaterialPicker.IsPrecious(s) || !Covers(def, s, units, map, reserved)) continue;
                int have = MaterialCounts.Have(map, s);
                if (best == null || have > bestHave)
                {
                    best = s;
                    bestHave = have;
                }
            }
            return best;
        }

        private static bool Covers(BuildableDef def, ThingDef s, int units, Map map, Dictionary<ThingDef, int> reserved)
        {
            int have = MaterialCounts.Have(map, s);
            if (reserved != null && reserved.TryGetValue(s, out int used)) have -= used;
            return have >= units * Mathf.Max(1, MaterialCounts.PerUnit(def, s));
        }

        /// <summary>The theme's door, if it has one the colony can build.</summary>
        public ThingDef DoorFor(Map map)
        {
            if (doors == null) return null;
            foreach (var n in doors)
            {
                var d = DefDatabase<ThingDef>.GetNamedSilentFail(n);
                if (d != null && d.IsDoor && d.size.x == 1 && d.size.z == 1 && Avail.CanBuild(d, map)) return d;
            }
            return null;
        }

        /// <summary>A building whose icon stands for the theme in the menu: the first <see cref="iconDefs"/> entry that exists, else none.</summary>
        public ThingDef IconDef
        {
            get
            {
                if (iconDefs == null) return null;
                foreach (var n in iconDefs)
                {
                    var d = DefDatabase<ThingDef>.GetNamedSilentFail(n);
                    if (d != null && d.uiIcon != null && d.uiIcon != BaseContent.BadTex) return d;
                }
                return null;
            }
        }
    }
}
