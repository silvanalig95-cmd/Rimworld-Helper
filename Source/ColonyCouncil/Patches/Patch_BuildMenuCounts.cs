using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The game's material menu (clicking a wall, door or piece of furniture made of stuff in the build menu): each
    /// material shows how much the colony has, stored and lying around, and how many it would make, like
    /// "Steel wall    420 (+85 lying around), enough for 84". Only for that menu: the flag is set while the build
    /// designator opens it, and the labels are changed before the menu measures itself. Another designator, or a mod
    /// that builds its own menu, is left alone.
    /// </summary>
    [HarmonyPatch(typeof(Designator_Build), nameof(Designator_Build.ProcessInput))]
    public static class Patch_BuildMenuCounts
    {
        /// <summary>The build designator opening its material menu right now (the game draws menus on one thread).</summary>
        internal static Designator_Build opening;

        public static bool Enabled => RimfolkPatches.On && (ColonyCouncilMod.Settings?.buildMenuCounts ?? true);

        public static void Prefix(Designator_Build __instance)
        {
            opening = Enabled ? __instance : null;
        }

        public static Exception Finalizer(Exception __exception)
        {
            opening = null;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(FloatMenu), MethodType.Constructor, new[] { typeof(List<FloatMenuOption>) })]
    public static class Patch_BuildMenuCountsLabels
    {
        private static AccessTools.FieldRef<FloatMenuOption, ThingDef> shownItem;
        private static bool failed;

        /// <summary>
        /// Each option's own label and the label it was given, so an option a mod keeps and shows again gets today's
        /// counts instead of a second note after the first.
        /// </summary>
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FloatMenuOption, string[]> noted =
            new System.Runtime.CompilerServices.ConditionalWeakTable<FloatMenuOption, string[]>();

        public static void Prefix(List<FloatMenuOption> options)
        {
            var des = Patch_BuildMenuCounts.opening;
            if (des == null || options == null || failed) return;
            // One menu per click: whatever else opens a menu during this call is not ours.
            Patch_BuildMenuCounts.opening = null;
            try
            {
                var map = Find.CurrentMap;
                var def = des.PlacingDef;
                if (map == null || def == null || !def.MadeFromStuff) return;
                shownItem ??= AccessTools.FieldRefAccess<FloatMenuOption, ThingDef>("shownItem");
                foreach (var o in options)
                {
                    if (o == null) continue;
                    var stuff = shownItem(o);
                    if (stuff == null || !stuff.IsStuff || !Avail.StuffAllowed(def, stuff)) continue;
                    string label = o.Label ?? "";
                    if (noted.TryGetValue(o, out var was))
                    {
                        if (label == was[1]) label = was[0];
                        noted.Remove(o);
                    }
                    string withNote = label + "    " + MaterialCounts.MenuNote(map, def, stuff);
                    o.Label = withNote;
                    noted.Add(o, new[] { label, withNote });
                }
            }
            catch (Exception e)
            {
                // The menu changed in a way this doesn't understand: stop trying, the menu works as before.
                failed = true;
                CouncilLog.ErrorOnce("build menu material counts", e);
            }
        }
    }
}
