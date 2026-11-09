using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The suggested clothes inside Nice Inventory Tab, where the player looks, by name and read from its decompiled build,
    /// never referenced, as the suggested weapons are (Weapons/WeaponNiceTab.cs). In its first layout the apparel box
    /// (<c>NiceInventoryTab.ITab_Pawn_Gear_Patch.apparel_legacy_groupbox</c>) is a scrolling list of rows like the equipment
    /// box, so Rimfolk's row goes there, under the last piece worn and above the empty slots it shows. In its alternative
    /// layout the apparel box is a grid of slots (<c>apparel_alt_groupbox</c>, an <c>ApparelGridSlot</c> in every cell, cast
    /// as such by its own code), where no row of another kind may go, so the row goes into the equipment box's scrolling list
    /// (<c>equipment_alt_groupbox</c>), under the suggested weapons' row (or the last thing held). The row is one of its own
    /// <c>LabelWidget</c>s with no text, of the suggestions' height, kept in its place after Nice Inventory Tab's own recache;
    /// its drawing (a postfix on <c>LabelWidget.Draw</c>, for Rimfolk's row only) is the suggestions' rows in Nice Inventory
    /// Tab's own colours, inside its scroll view. Without the mod both hooks stand down.
    /// </summary>
    public static class ApparelNiceTab
    {
        private static readonly FieldInfo LegacyApparelBox = WeaponNiceTab.PatchType != null ? AccessTools.Field(WeaponNiceTab.PatchType, "apparel_legacy_groupbox") : null;
        private static readonly FieldInfo AltEquipmentBox = WeaponNiceTab.PatchType != null ? AccessTools.Field(WeaponNiceTab.PatchType, "equipment_alt_groupbox") : null;
        private static readonly FieldInfo Childs = WeaponNiceTab.WidgetType != null ? AccessTools.Field(WeaponNiceTab.WidgetType, "Childs") : null;
        private static readonly FieldInfo Visible = WeaponNiceTab.WidgetType != null ? AccessTools.Field(WeaponNiceTab.WidgetType, "Visible") : null;
        private static readonly FieldInfo Geometry = WeaponNiceTab.WidgetType != null ? AccessTools.Field(WeaponNiceTab.WidgetType, "Geometry") : null;
        private static readonly MethodInfo SetFixedHeight = WeaponNiceTab.WidgetType != null ? AccessTools.Method(WeaponNiceTab.WidgetType, "SetFixedHeight", new[] { typeof(float) }) : null;
        private static readonly MethodInfo UpdateLayout = WeaponNiceTab.WidgetType != null ? AccessTools.Method(WeaponNiceTab.WidgetType, "Update") : null;

        /// <summary>Every member the hooks read is there (else they stand down).</summary>
        internal static bool Ready => LegacyApparelBox != null && AltEquipmentBox != null && Childs != null && Visible != null && Geometry != null && SetFixedHeight != null &&
                                      UpdateLayout != null && WeaponNiceTab.LabelType != null && WeaponGearTab.NiceRow != null;

        private sealed class Spacer
        {
            public object box, widget;
            public Pawn pawn;
            public float height = -1f;
        }

        private static readonly Spacer legacy = new Spacer(), alt = new Spacer();
        private static readonly List<WeaponNiceMath.Child> kinds = new List<WeaponNiceMath.Child>();

        /// <summary>After Nice Inventory Tab's own recache: Rimfolk's clothes row in the box in use, in its place and of its height, or out of the boxes.</summary>
        internal static void Place(Pawn pawn)
        {
            float want = pawn != null && ApparelPickGearTab.On ? ApparelPickGearTab.Height(pawn) : 0f;
            Keep(legacy, LegacyApparelBox.GetValue(null), pawn, want);
            Keep(alt, AltEquipmentBox.GetValue(null), pawn, want);
        }

        private static void Keep(Spacer sp, object box, Pawn pawn, float want)
        {
            if (box == null) return;
            if (sp.box != box)
            {
                sp.box = box;
                sp.widget = null;
                sp.height = -1f;
            }
            var list = Layout(box);
            if (list == null) return;
            int at = sp.widget != null ? list.IndexOf(sp.widget) : -1;
            bool use = want > 0f && Visible.GetValue(box) is bool v && v;
            if (!use)
            {
                if (at >= 0)
                {
                    list.RemoveAt(at);
                    UpdateLayout.Invoke(box, null);
                }
                return;
            }
            sp.pawn = pawn;
            bool changed = false;
            if (sp.widget == null)
            {
                sp.widget = Activator.CreateInstance(WeaponNiceTab.LabelType);
                at = -1;
            }
            if (at < 0 || !WeaponNiceMath.InPlace(Kinds(list, sp.widget)))
            {
                if (at >= 0) list.RemoveAt(at);
                list.Insert(WeaponNiceMath.Place(Kinds(list, sp.widget)), sp.widget);
                changed = true;
            }
            if (Math.Abs(sp.height - want) > 0.5f)
            {
                SetFixedHeight.Invoke(sp.widget, new object[] { want });
                sp.height = want;
                changed = true;
            }
            if (changed) UpdateLayout.Invoke(box, null);
        }

        private static IList Layout(object box) =>
            Childs.GetValue(box) is IList outer && outer.Count > 0 && outer[0] != null ? Childs.GetValue(outer[0]) as IList : null;

        /// <summary>The box's rows as the place reads them: its own rows of things worn or held, and the suggested weapons' row (the clothes' row goes under it), are held.</summary>
        private static List<WeaponNiceMath.Child> Kinds(IList list, object ours)
        {
            kinds.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                var w = list[i];
                kinds.Add(ReferenceEquals(w, ours) ? WeaponNiceMath.Child.Ours
                    : WeaponGearTab.NiceRow.IsInstanceOfType(w) || WeaponNiceTab.IsSpacer(w) ? WeaponNiceMath.Child.Held : WeaponNiceMath.Child.Other);
            }
            return kinds;
        }

        /// <summary>Whether a widget is Rimfolk's clothes row.</summary>
        internal static bool IsSpacer(object widget) => widget != null && (ReferenceEquals(widget, legacy.widget) || ReferenceEquals(widget, alt.widget));

        /// <summary>Rimfolk's clothes row drawn where Nice Inventory Tab put it, in its scroll view; any other label widget is left alone.</summary>
        internal static void Draw(object widget)
        {
            var sp = ReferenceEquals(widget, legacy.widget) ? legacy : ReferenceEquals(widget, alt.widget) ? alt : null;
            if (sp?.pawn == null || !ApparelPickGearTab.On) return;
            var g = (Rect)Geometry.GetValue(widget);
            try
            {
                ApparelPickGearTab.Rows(g, sp.pawn, WeaponNiceTab.Look);
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }
    }

    /// <summary>Rimfolk's clothes row in Nice Inventory Tab's apparel box (or, in its grid layout, its equipment box), kept in its place after the tab's own recache; skipped without the mod.</summary>
    [HarmonyPatch]
    public static class Patch_ApparelNiceSuggestedPlace
    {
        private static bool broken;

        public static bool Prepare() => Target() != null;

        public static MethodBase TargetMethod() => Target();

        public static string StandsDownWhy() => WeaponNiceTab.PatchType == null ? "Nice Inventory Tab isn't loaded" : "";

        private static MethodBase Target() => WeaponNiceTab.PatchType != null && ApparelNiceTab.Ready ? AccessTools.Method(WeaponNiceTab.PatchType, "Recache", new[] { typeof(Pawn) }) : null;

        [HarmonyPriority(Priority.Low)]
        public static void Postfix(Pawn pawn)
        {
            if (broken) return;
            try
            {
                ApparelNiceTab.Place(pawn);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("the suggested clothes' place in Nice Inventory Tab", e);
            }
        }
    }

    /// <summary>Rimfolk's clothes row drawn in Nice Inventory Tab (its own label widgets untouched); skipped without the mod.</summary>
    [HarmonyPatch]
    public static class Patch_ApparelNiceSuggestedDraw
    {
        private static bool broken;

        public static bool Prepare() => Target() != null;

        public static MethodBase TargetMethod() => Target();

        public static string StandsDownWhy() => WeaponNiceTab.LabelType == null ? "Nice Inventory Tab isn't loaded" : "";

        private static MethodBase Target() => WeaponNiceTab.LabelType != null && ApparelNiceTab.Ready ? AccessTools.Method(WeaponNiceTab.LabelType, "Draw") : null;

        public static void Postfix(object __instance)
        {
            if (broken) return;
            try
            {
                ApparelNiceTab.Draw(__instance);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("the suggested clothes in Nice Inventory Tab", e);
            }
        }
    }
}
