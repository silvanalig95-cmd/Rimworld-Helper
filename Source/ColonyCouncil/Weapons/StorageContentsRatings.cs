using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The rating beside each weapon and piece of apparel on LWM's Deep Storage "Contents" tab (the player, 12 November
    /// 2026, with a steel weapons rack: "i'd like to see the weapns rating here"). The same badge and card button as on the
    /// gear tab ("Ratings on the gear tab", under Weapons and under Apparel): the row gives up room at its end for them, as
    /// the game's gear tab does. Read by name (LWM.DeepStorage.ITab_DeepStorage_Inventory.DrawThingRow); skipped without the
    /// mod, or when its row isn't the one expected. Rows that are not gear stay as they are.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_DeepStorageContentsRating
    {
        private static bool broken;

        private static readonly Type Tab = GenTypes.GetTypeInAnyAssembly("LWM.DeepStorage.ITab_DeepStorage_Inventory");

        /// <summary>Its row: DrawThingRow(ref float y, float width, Thing thing), by those names.</summary>
        private static MethodBase Target()
        {
            var m = Tab == null ? null : AccessTools.Method(Tab, "DrawThingRow");
            var ps = m?.GetParameters();
            if (ps == null || ps.Length != 3) return null;
            bool fits = ps[0].Name == "y" && ps[0].ParameterType == typeof(float).MakeByRefType()
                        && ps[1].Name == "width" && ps[1].ParameterType == typeof(float)
                        && ps[2].Name == "thing" && typeof(Thing).IsAssignableFrom(ps[2].ParameterType);
            return fits ? m : null;
        }

        public static bool Prepare() => Target() != null;

        public static MethodBase TargetMethod() => Target();

        public static string StandsDownWhy() => Tab == null ? "LWM's Deep Storage isn't loaded" : "";

        /// <summary>1 a weapon the rating reads, 2 apparel, 0 anything else.</summary>
        private static int Kind(Thing thing)
        {
            if (thing?.def == null) return 0;
            if (WeaponGearTab.On && WeaponLive.Rateable(thing.def)) return 1;
            if (ApparelGearTab.On && ApparelLive.Rateable(thing.def)) return 2;
            return 0;
        }

        public static void Prefix(ref float y, ref float width, Thing thing, out (float y, float width, int kind) __state)
        {
            __state = default;
            if (broken || width < 200f) return;
            int kind = Kind(thing);
            if (kind == 0) return;
            __state = (y, width, kind);
            width -= WeaponGearTab.RowRoom;
        }

        public static void Postfix(Thing thing, (float y, float width, int kind) __state)
        {
            if (__state.kind == 0 || broken) return;
            try
            {
                float x = __state.width - WeaponGearTab.RowRoom + 4f;
                var badge = new Rect(x, __state.y + (28f - WeaponGearTab.BadgeH) / 2f, WeaponGearTab.BadgeW, WeaponGearTab.BadgeH);
                var button = new Rect(badge.xMax + 4f, __state.y + (28f - WeaponGearTab.ButtonSize) / 2f, WeaponGearTab.ButtonSize, WeaponGearTab.ButtonSize);
                if (__state.kind == 1)
                {
                    WeaponGearTab.Click(button, thing, null);
                    WeaponGearTab.Bits(badge, button, thing, null, true);
                }
                else
                {
                    ApparelGearTab.Click(button, thing, null);
                    ApparelGearTab.Bits(badge, button, thing, null, true);
                }
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("the gear rating on Deep Storage's contents", e);
            }
        }
    }
}
