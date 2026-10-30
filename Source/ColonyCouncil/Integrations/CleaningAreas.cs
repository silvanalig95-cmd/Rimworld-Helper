using System;
using System.Reflection;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The cleaning area mods (Cleaning Area, its temp replacement, Cleaning Area Relaunched): each keeps one area per
    /// map in a map component and swaps the game's CleanFilth work giver and Clean job driver for its own, which skip
    /// any filth outside that area. Anything of ours that hands out cleaning keeps to the same area, and a clean sweep
    /// lifts only the givers that do; a job for filth outside it would end at once and be handed out again, forever.
    /// Read by name, so none of the mods is needed.
    /// </summary>
    public static class CleaningAreas
    {
        private static bool looked;
        private static Type compType;
        private static MethodInfo getter;

        private static readonly (string type, string getter)[] Known =
        {
            ("CleaningAreaTemp.CleaningArea_MapComponent", "getCleaningArea"),
            ("CleaningArea.CleaningArea_MapComponent", "getCleaningArea"),
            ("CleaningAreaRelaunched.CleaningAreaRelaunched_MapComponent", "GetCleanArea")
        };

        private static void Look()
        {
            if (looked) return;
            looked = true;
            foreach (var (typeName, getterName) in Known)
            {
                try
                {
                    var t = GenTypes.GetTypeInAnyAssembly(typeName);
                    if (t == null) continue;
                    var m = t.GetMethod(getterName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null)
                        ?? t.GetProperty("cleanArea", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetGetMethod(true);
                    if (m == null || !typeof(Area).IsAssignableFrom(m.ReturnType)) continue;
                    compType = t;
                    getter = m;
                    return;
                }
                catch
                {
                    // A changed mod: no area to keep to.
                }
            }
        }

        /// <summary>A cleaning area mod is loaded.</summary>
        public static bool Present
        {
            get
            {
                Look();
                return compType != null;
            }
        }

        /// <summary>The map's cleaning area as the mod has it (the home area when none is set), or null without a mod.</summary>
        public static Area Of(Map map)
        {
            if (map == null || !Present) return null;
            try
            {
                MapComponent comp = null;
                foreach (var c in map.components)
                    if (c != null && compType.IsInstanceOfType(c))
                    {
                        comp = c;
                        break;
                    }
                if (comp == null) return map.areaManager.Home;
                return getter.Invoke(comp, null) as Area ?? map.areaManager.Home;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("cleaning area", e);
                return null;
            }
        }

        /// <summary>Whether filth here may be cleaned: inside the cleaning area, or anywhere without a cleaning area mod.</summary>
        public static bool Allows(Map map, IntVec3 c)
        {
            var area = Of(map);
            return area == null || area[c];
        }

        /// <summary>
        /// A cleaning work giver that keeps to the cleaning area: the game's own CleanFilth (which the mods replace with
        /// theirs) and the base game's other cleaning givers, which read the filth list the mods keep. Another mod's own
        /// cleaning giver reads filth its own way.
        /// </summary>
        public static bool KeepsToArea(WorkGiverDef def)
        {
            if (def == null) return false;
            if (!Present) return true;
            if (def.defName == "CleanFilth") return true;
            var cls = def.giverClass;
            return cls != null && cls.Assembly == typeof(WorkGiver_CleanFilth).Assembly;
        }
    }
}
