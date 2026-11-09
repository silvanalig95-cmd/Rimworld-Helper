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
    /// The rating beside each weapon on the gear tab ("Ratings on the gear tab"): a small badge with the number in its band's
    /// colour, its tooltip the tags and the colonist's own rating, and a small button to Rimfolk's weapon card. On the game's
    /// own tab the row gives up room at its end for them; on Nice Inventory Tab's (read by name, skipped without it) they sit
    /// on the weapon's picture, a row's top corners, a cell's top corners.
    /// </summary>
    public static class WeaponGearTab
    {
        public static bool On => RimfolkPatches.On && WeaponLive.On && (ColonyCouncilMod.Settings?.weaponGearTab ?? true);

        /// <summary>The room the badge and the button take at the end of a row of the game's gear tab.</summary>
        public const float RowRoom = 56f;

        public const float BadgeW = 30f, BadgeH = 17f, ButtonSize = 18f;

        /// <summary>The badge in a rect, and the card's button right of it when there is room for both.</summary>
        /// <param name="badgeClicks">The badge opens the card on a click here (the game's tab); on Nice Inventory Tab's rows and cells its click is caught before the row's own (<see cref="Click"/>).</param>
        /// <param name="clickTip">Its tooltip says a click opens the card (to compare with what the colonist holds).</param>
        public static void Bits(Rect badge, Rect? button, Thing thing, Pawn pawn, bool badgeClicks, bool clickTip = false)
        {
            var r = WeaponLive.Of(thing);
            if (r == null) return;
            WeaponUi.Badge(badge, r.Rating.Overall, () => WeaponUi.Tip(r, pawn, thing, badgeClicks || clickTip), thing.thingIDNumber ^ 0x5eed);
            if ((badgeClicks || clickTip) && WeaponCardWindow.On && Mouse.IsOver(badge)) Widgets.DrawHighlight(badge);
            if (badgeClicks && WeaponCardWindow.On && Widgets.ButtonInvisible(badge, false)) WeaponCardWindow.Open(thing, pawn);
            if (button.HasValue && WeaponCardWindow.On)
            {
                var b = button.Value;
                if (Mouse.IsOver(b)) Widgets.DrawHighlight(b);
                UiKit.Icon(b.ContractedBy(1f), Icons.CatNumbers, Mouse.IsOver(b) ? GenUI.MouseoverColor : new Color(1f, 1f, 1f, 0.8f));
                TooltipHandler.TipRegion(b, "CC_Weapon_OpenCard_Tip".Translate());
            }
        }

        /// <summary>A click on the card's button, caught before the row's own click (Nice Inventory Tab opens the game's card on any click).</summary>
        public static void Click(Rect button, Thing thing, Pawn pawn)
        {
            if (!WeaponCardWindow.On || thing == null || !WeaponLive.Rateable(thing.def)) return;
            if (Widgets.ButtonInvisible(button, false)) WeaponCardWindow.Open(thing, pawn);
        }

        // ---------------- Nice Inventory Tab's rows and cells, by name ----------------

        internal static readonly Type NiceRow = GenTypes.GetTypeInAnyAssembly("NiceInventoryTab.EquippedItem");
        internal static readonly Type NiceCell = GenTypes.GetTypeInAnyAssembly("NiceInventoryTab.InventoryItem");
        private static readonly FieldInfo Geometry = GenTypes.GetTypeInAnyAssembly("NiceInventoryTab.Widget") is Type w ? AccessTools.Field(w, "Geometry") : null;

        private static readonly Dictionary<Type, (FieldInfo item, FieldInfo pawn)> fields = new Dictionary<Type, (FieldInfo, FieldInfo)>();

        /// <summary>A widget class's weapon and colonist fields, looked up once a class.</summary>
        private static (FieldInfo item, FieldInfo pawn) Fields(Type t)
        {
            if (!fields.TryGetValue(t, out var f))
            {
                f = (AccessTools.Field(t, "Item"), AccessTools.Field(t, "pawn"));
                fields[t] = f;
            }
            return f;
        }

        /// <summary>A Nice Inventory Tab widget's weapon, colonist and place; false when it isn't a weapon the rating reads.</summary>
        internal static bool Read(object widget, out Thing thing, out Pawn pawn, out Rect geometry)
        {
            thing = null;
            pawn = null;
            geometry = default;
            if (widget == null || Geometry == null) return false;
            var (item, who) = Fields(widget.GetType());
            thing = item?.GetValue(widget) as Thing;
            pawn = who?.GetValue(widget) as Pawn;
            if (thing == null || !WeaponLive.Rateable(thing.def)) return false;
            geometry = (Rect)Geometry.GetValue(widget);
            return true;
        }

        /// <summary>On a row (the weapon in hand): the badge on its picture's top left corner, the button on its top right.</summary>
        internal static (Rect badge, Rect button) RowRects(Rect g)
        {
            float h = Mathf.Max(10f, g.height - 12f);
            var icon = new Rect(g.x + 6f, g.y + 6f, h * 1.2f, h);
            return (new Rect(icon.x - 1f, icon.y - 1f, BadgeW, BadgeH), new Rect(icon.xMax - ButtonSize, icon.y - 1f, ButtonSize, ButtonSize));
        }

        /// <summary>In a cell (the inventory): the badge at its top left, the button at its top right.</summary>
        internal static (Rect badge, Rect button) CellRects(Rect g) =>
            (new Rect(g.x + 3f, g.y + 3f, BadgeW, BadgeH), new Rect(g.xMax - ButtonSize - 3f, g.y + 3f, ButtonSize, ButtonSize));
    }

    /// <summary>The badge and the card's button at the end of each weapon's row on the game's own gear tab (the row gives up the room).</summary>
    [HarmonyPatch(typeof(ITab_Pawn_Gear), "DrawThingRow")]
    public static class Patch_WeaponGearRow
    {
        private static bool broken;
        private static readonly MethodInfo SelPawn = AccessTools.PropertyGetter(typeof(ITab_Pawn_Gear), "SelPawnForGear");

        public static void Prefix(ref float y, ref float width, Thing thing, out (float y, float width, bool on) __state)
        {
            __state = default;
            if (broken || thing == null || !WeaponGearTab.On || !WeaponLive.Rateable(thing.def) || width < 200f) return;
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
                WeaponGearTab.Click(button, thing, pawn);
                WeaponGearTab.Bits(badge, button, thing, pawn, true);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("the weapon rating on the gear tab", e);
            }
        }
    }

    /// <summary>The badge and the card's button on Nice Inventory Tab's rows (the weapon in hand), read by name; skipped without the mod.</summary>
    [HarmonyPatch]
    public static class Patch_WeaponNiceInventoryRow
    {
        private static bool broken;

        public static bool Prepare() => Target() != null;

        public static MethodBase TargetMethod() => Target();

        public static string StandsDownWhy() => WeaponGearTab.NiceRow == null ? "Nice Inventory Tab isn't loaded" : "";

        private static MethodBase Target() => WeaponGearTab.NiceRow is Type t ? AccessTools.Method(t, "Draw") : null;

        public static void Prefix(object __instance)
        {
            if (broken || !WeaponGearTab.On || !WeaponGearTab.Read(__instance, out var thing, out var pawn, out var g)) return;
            // The row has no room for a word more, so its badge is the button to compare: it and the small button open Rimfolk's card.
            var (badge, button) = WeaponGearTab.RowRects(g);
            WeaponGearTab.Click(button, thing, pawn);
            WeaponGearTab.Click(badge, thing, pawn);
        }

        public static void Postfix(object __instance)
        {
            if (broken || !WeaponGearTab.On) return;
            try
            {
                if (!WeaponGearTab.Read(__instance, out var thing, out var pawn, out var g)) return;
                var (badge, button) = WeaponGearTab.RowRects(g);
                WeaponGearTab.Bits(badge, button, thing, pawn, false, true);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("the weapon rating on Nice Inventory Tab's rows", e);
            }
        }
    }

    /// <summary>The badge and the card's button on Nice Inventory Tab's inventory cells (sidearms and the rest), read by name; skipped without the mod.</summary>
    [HarmonyPatch]
    public static class Patch_WeaponNiceInventoryCell
    {
        private static bool broken;

        public static bool Prepare() => Target() != null;

        public static MethodBase TargetMethod() => Target();

        public static string StandsDownWhy() => WeaponGearTab.NiceCell == null ? "Nice Inventory Tab isn't loaded" : "";

        private static MethodBase Target() => WeaponGearTab.NiceCell is Type t ? AccessTools.Method(t, "Draw") : null;

        public static void Prefix(object __instance)
        {
            if (broken || !WeaponGearTab.On || !WeaponGearTab.Read(__instance, out var thing, out var pawn, out var g)) return;
            var (badge, button) = WeaponGearTab.CellRects(g);
            if (g.width >= 80f) WeaponGearTab.Click(button, thing, pawn);
            WeaponGearTab.Click(badge, thing, pawn);
        }

        public static void Postfix(object __instance)
        {
            if (broken || !WeaponGearTab.On) return;
            try
            {
                if (!WeaponGearTab.Read(__instance, out var thing, out var pawn, out var g)) return;
                var (badge, button) = WeaponGearTab.CellRects(g);
                WeaponGearTab.Bits(badge, g.width >= 80f ? button : (Rect?)null, thing, pawn, false, true);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("the weapon rating on Nice Inventory Tab's cells", e);
            }
        }
    }
}
