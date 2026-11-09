using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The rating beside each piece of apparel on the gear tab ("Ratings on the gear tab" under Apparel): the weapons' small
    /// badge with the number in its band's colour, its tooltip the tags and the colonist's own rating, a click opening
    /// Rimfolk's apparel card, and on the game's tab and Nice Inventory Tab's rows a small button to the card beside it. On
    /// the game's own tab the row gives up room at its end for them; on Nice Inventory Tab's (read by name, skipped without
    /// it) they sit on the piece's picture in a row (its legacy apparel box, belts and shields in its equipment box), on a
    /// cell's corner (apparel carried), and on an apparel slot's top left corner in its grid (below the slot's warning mark
    /// when it shows one).
    /// </summary>
    public static class ApparelGearTab
    {
        public static bool On => RimfolkPatches.On && ApparelLive.On && (ColonyCouncilMod.Settings?.apparelGearTab ?? true);

        /// <summary>The badge in a rect, and the card's button beside it when there is room.</summary>
        /// <param name="badgeClicks">The badge opens the card on a click here (the game's tab); on Nice Inventory Tab its click is caught before the widget's own (<see cref="Click"/>).</param>
        /// <param name="clickTip">Its tooltip says a click opens the card.</param>
        public static void Bits(Rect badge, Rect? button, Thing thing, Pawn pawn, bool badgeClicks, bool clickTip = false)
        {
            var r = ApparelLive.Of(thing);
            if (r?.Rating == null) return;
            WeaponUi.Badge(badge, r.Rating.Overall, () => ApparelUi.Tip(r, pawn, thing, badgeClicks || clickTip), thing.thingIDNumber ^ 0x7A11);
            if ((badgeClicks || clickTip) && ApparelCardWindow.On && Mouse.IsOver(badge)) Widgets.DrawHighlight(badge);
            if (badgeClicks && ApparelCardWindow.On && Widgets.ButtonInvisible(badge, false)) ApparelCardWindow.Open(thing, pawn);
            if (button.HasValue && ApparelCardWindow.On)
            {
                var b = button.Value;
                if (Mouse.IsOver(b)) Widgets.DrawHighlight(b);
                UiKit.Icon(b.ContractedBy(1f), Icons.CatNumbers, Mouse.IsOver(b) ? GenUI.MouseoverColor : UiKit.WithAlpha(Color.white, 0.8f));
                TooltipHandler.TipRegion(b, "CC_Apparel_OpenCard_Tip".Translate());
            }
        }

        /// <summary>A click on the badge or the button, caught before the widget's own click (Nice Inventory Tab opens the game's card on any click).</summary>
        public static void Click(Rect r, Thing thing, Pawn pawn)
        {
            if (!ApparelCardWindow.On || thing == null || !ApparelLive.Rateable(thing.def)) return;
            if (Widgets.ButtonInvisible(r, false)) ApparelCardWindow.Open(thing, pawn);
        }

        // ---------------- Nice Inventory Tab's rows, cells and apparel slots, by name ----------------

        internal static readonly Type NiceSlot = GenTypes.GetTypeInAnyAssembly("NiceInventoryTab.ApparelGridSlot");
        private static readonly FieldInfo Geometry = GenTypes.GetTypeInAnyAssembly("NiceInventoryTab.Widget") is Type w ? AccessTools.Field(w, "Geometry") : null;
        private static readonly FieldInfo SlotProblems = NiceSlot != null ? AccessTools.Field(NiceSlot, "problems") : null;

        private static readonly Dictionary<Type, FieldInfo> items = new Dictionary<Type, FieldInfo>();

        /// <summary>A Nice Inventory Tab widget's piece, its wearer or carrier, and its place; false when it isn't apparel the rating reads.</summary>
        internal static bool Read(object widget, out Thing thing, out Pawn pawn, out Rect geometry)
        {
            thing = null;
            pawn = null;
            geometry = default;
            if (widget == null || Geometry == null) return false;
            var t = widget.GetType();
            if (!items.TryGetValue(t, out var item))
            {
                item = AccessTools.Field(t, "Item");
                items[t] = item;
            }
            thing = item?.GetValue(widget) as Thing;
            if (thing == null || !ApparelLive.Rateable(thing.def)) return false;
            pawn = ApparelLive.Holder(thing);
            geometry = (Rect)Geometry.GetValue(widget);
            return true;
        }

        /// <summary>An apparel slot in Nice Inventory Tab's grid: the badge at its top left corner, under the slot's warning mark when it shows one.</summary>
        internal static Rect SlotBadge(object slot, Rect g)
        {
            int problems = 0;
            try
            {
                if (SlotProblems != null) problems = Convert.ToInt32(SlotProblems.GetValue(slot));
            }
            catch
            {
                problems = 0;
            }
            return new Rect(g.x + 3f, g.y + (problems != 0 ? 24f : 3f), WeaponGearTab.BadgeW, WeaponGearTab.BadgeH);
        }
    }

    /// <summary>The badge and the card's button at the end of each apparel row on the game's own gear tab (the row gives up the room).</summary>
    [HarmonyPatch(typeof(ITab_Pawn_Gear), "DrawThingRow")]
    public static class Patch_ApparelGearRow
    {
        private static bool broken;
        private static readonly MethodInfo SelPawn = AccessTools.PropertyGetter(typeof(ITab_Pawn_Gear), "SelPawnForGear");

        public static void Prefix(ref float y, ref float width, Thing thing, out (float y, float width, bool on) __state)
        {
            __state = default;
            if (broken || thing == null || !ApparelGearTab.On || !ApparelLive.Rateable(thing.def) || width < 200f) return;
            __state = (y, width, true);
            width -= WeaponGearTab.RowRoom;
        }

        public static void Postfix(ITab_Pawn_Gear __instance, Thing thing, (float y, float width, bool on) __state)
        {
            if (!__state.on || broken) return;
            try
            {
                var pawn = SelPawn?.Invoke(__instance, null) as Pawn;
                float x = __state.width - WeaponGearTab.RowRoom + 4f;
                var badge = new Rect(x, __state.y + (28f - WeaponGearTab.BadgeH) / 2f, WeaponGearTab.BadgeW, WeaponGearTab.BadgeH);
                var button = new Rect(badge.xMax + 4f, __state.y + (28f - WeaponGearTab.ButtonSize) / 2f, WeaponGearTab.ButtonSize, WeaponGearTab.ButtonSize);
                ApparelGearTab.Click(button, thing, pawn);
                ApparelGearTab.Bits(badge, button, thing, pawn, true);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("the apparel rating on the gear tab", e);
            }
        }
    }

    /// <summary>The badge and the card's button on Nice Inventory Tab's rows (its legacy apparel box, belts and shields in its equipment box), read by name; skipped without the mod.</summary>
    [HarmonyPatch]
    public static class Patch_ApparelNiceInventoryRow
    {
        private static bool broken;

        public static bool Prepare() => Target() != null;

        public static MethodBase TargetMethod() => Target();

        public static string StandsDownWhy() => WeaponGearTab.NiceRow == null ? "Nice Inventory Tab isn't loaded" : "";

        private static MethodBase Target() => WeaponGearTab.NiceRow is Type t ? AccessTools.Method(t, "Draw") : null;

        public static void Prefix(object __instance)
        {
            if (broken || !ApparelGearTab.On || !ApparelGearTab.Read(__instance, out var thing, out var pawn, out var g)) return;
            // The row's own click opens the game's card; the badge and the small button open Rimfolk's.
            var (badge, button) = WeaponGearTab.RowRects(g);
            ApparelGearTab.Click(button, thing, pawn);
            ApparelGearTab.Click(badge, thing, pawn);
        }

        public static void Postfix(object __instance)
        {
            if (broken || !ApparelGearTab.On) return;
            try
            {
                if (!ApparelGearTab.Read(__instance, out var thing, out var pawn, out var g)) return;
                var (badge, button) = WeaponGearTab.RowRects(g);
                ApparelGearTab.Bits(badge, button, thing, pawn, false, true);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("the apparel rating on Nice Inventory Tab's rows", e);
            }
        }
    }

    /// <summary>The badge and the card's button on Nice Inventory Tab's inventory cells (apparel carried), read by name; skipped without the mod.</summary>
    [HarmonyPatch]
    public static class Patch_ApparelNiceInventoryCell
    {
        private static bool broken;

        public static bool Prepare() => Target() != null;

        public static MethodBase TargetMethod() => Target();

        public static string StandsDownWhy() => WeaponGearTab.NiceCell == null ? "Nice Inventory Tab isn't loaded" : "";

        private static MethodBase Target() => WeaponGearTab.NiceCell is Type t ? AccessTools.Method(t, "Draw") : null;

        public static void Prefix(object __instance)
        {
            if (broken || !ApparelGearTab.On || !ApparelGearTab.Read(__instance, out var thing, out var pawn, out var g)) return;
            var (badge, button) = WeaponGearTab.CellRects(g);
            if (g.width >= 80f) ApparelGearTab.Click(button, thing, pawn);
            ApparelGearTab.Click(badge, thing, pawn);
        }

        public static void Postfix(object __instance)
        {
            if (broken || !ApparelGearTab.On) return;
            try
            {
                if (!ApparelGearTab.Read(__instance, out var thing, out var pawn, out var g)) return;
                var (badge, button) = WeaponGearTab.CellRects(g);
                ApparelGearTab.Bits(badge, g.width >= 80f ? button : (Rect?)null, thing, pawn, false, true);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("the apparel rating on Nice Inventory Tab's cells", e);
            }
        }
    }

    /// <summary>
    /// The badge on Nice Inventory Tab's apparel slots (its grid, "alternative layout"), read by name; skipped without the
    /// mod. Its drop and lock buttons hold the slot's right corners and its stars the top's middle, so the badge alone sits
    /// at the top left (under the warning mark when the slot shows one), and its click opens Rimfolk's card.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_ApparelNiceInventorySlot
    {
        private static bool broken;

        public static bool Prepare() => Target() != null;

        public static MethodBase TargetMethod() => Target();

        public static string StandsDownWhy() => ApparelGearTab.NiceSlot == null ? "Nice Inventory Tab isn't loaded" : "";

        private static MethodBase Target() => ApparelGearTab.NiceSlot is Type t ? AccessTools.Method(t, "Draw") : null;

        public static void Prefix(object __instance)
        {
            if (broken || !ApparelGearTab.On || !ApparelGearTab.Read(__instance, out var thing, out var pawn, out var g)) return;
            ApparelGearTab.Click(ApparelGearTab.SlotBadge(__instance, g), thing, pawn);
        }

        public static void Postfix(object __instance)
        {
            if (broken || !ApparelGearTab.On) return;
            try
            {
                if (!ApparelGearTab.Read(__instance, out var thing, out var pawn, out var g)) return;
                ApparelGearTab.Bits(ApparelGearTab.SlotBadge(__instance, g), null, thing, pawn, false, true);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("the apparel rating on Nice Inventory Tab's apparel slots", e);
            }
        }
    }
}
