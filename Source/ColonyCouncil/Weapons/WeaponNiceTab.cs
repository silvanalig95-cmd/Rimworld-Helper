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
    /// The suggested weapons inside Nice Inventory Tab's equipment box, where the player looks (their words, 9 November 2026:
    /// "where do i see the weapon recommendation in this ui mod?"), all by name and read from its decompiled build, never
    /// referenced: the box (<c>NiceInventoryTab.ITab_Pawn_Gear_Patch.equipment_alt_groupbox</c> in its alternative layout,
    /// <c>equipment_legacy_groupbox</c> in its first) holds a scrolling list (<c>GroupBox.Childs[0]</c>, a
    /// <c>VScrollLayout</c>) of rows of fixed height (<c>EquippedItem</c> 60, the inventory's cells after them), which it
    /// lays out itself (<c>Widget.Update</c>) and scrolls when they are taller than the box. Rimfolk puts a row of its own in
    /// that list, right under the last thing held: a <c>LabelWidget</c> with no text of its own (it draws nothing) of the
    /// suggestions' height (<c>Widget.SetFixedHeight</c>), so the box makes room for it as for its own rows and nothing
    /// overlaps or is cut; its place and height are checked after Nice Inventory Tab's own recache each frame (a short list
    /// of references, nothing built), and only a change lays the box out again. Its drawing (a postfix on
    /// <c>LabelWidget.Draw</c>, for Rimfolk's row only) is the suggestions' rows in Nice Inventory Tab's own colours
    /// (<c>Assets</c>), inside its scroll view, so they scroll and clip with its rows. Without the mod both hooks stand down.
    /// </summary>
    public static class WeaponNiceTab
    {
        internal static readonly Type PatchType = GenTypes.GetTypeInAnyAssembly("NiceInventoryTab.ITab_Pawn_Gear_Patch");
        internal static readonly Type WidgetType = GenTypes.GetTypeInAnyAssembly("NiceInventoryTab.Widget");
        internal static readonly Type LabelType = GenTypes.GetTypeInAnyAssembly("NiceInventoryTab.LabelWidget");
        private static readonly Type AssetsType = GenTypes.GetTypeInAnyAssembly("NiceInventoryTab.Assets");

        private static readonly FieldInfo AltBox = PatchType != null ? AccessTools.Field(PatchType, "equipment_alt_groupbox") : null;
        private static readonly FieldInfo LegacyBox = PatchType != null ? AccessTools.Field(PatchType, "equipment_legacy_groupbox") : null;
        private static readonly FieldInfo Childs = WidgetType != null ? AccessTools.Field(WidgetType, "Childs") : null;
        private static readonly FieldInfo Visible = WidgetType != null ? AccessTools.Field(WidgetType, "Visible") : null;
        private static readonly FieldInfo Geometry = WidgetType != null ? AccessTools.Field(WidgetType, "Geometry") : null;
        private static readonly MethodInfo SetFixedHeight = WidgetType != null ? AccessTools.Method(WidgetType, "SetFixedHeight", new[] { typeof(float) }) : null;
        private static readonly MethodInfo UpdateLayout = WidgetType != null ? AccessTools.Method(WidgetType, "Update") : null;

        /// <summary>Every member the hooks read is there (else they stand down).</summary>
        internal static bool Ready => AltBox != null && LegacyBox != null && Childs != null && Visible != null && Geometry != null && SetFixedHeight != null && UpdateLayout != null &&
                                      LabelType != null && WeaponGearTab.NiceRow != null;

        /// <summary>Rimfolk's row in one equipment box: the box it was made for, its widget, the colonist it is drawn for, its height now.</summary>
        private sealed class Spacer
        {
            public object box, widget;
            public Pawn pawn;
            public float height = -1f;
        }

        private static readonly Spacer alt = new Spacer(), legacy = new Spacer();
        private static readonly List<WeaponNiceMath.Child> kinds = new List<WeaponNiceMath.Child>();

        /// <summary>After Nice Inventory Tab's own recache: Rimfolk's row in the box in use, in its place and of its height, or out of the boxes.</summary>
        internal static void Place(Pawn pawn)
        {
            float want = pawn != null && WeaponPickGearTab.On ? WeaponPickGearTab.Height(pawn) : 0f;
            Keep(alt, AltBox.GetValue(null), pawn, want);
            Keep(legacy, LegacyBox.GetValue(null), pawn, want);
        }

        private static void Keep(Spacer sp, object box, Pawn pawn, float want)
        {
            if (box == null) return;
            if (sp.box != box)
            {
                // A new box (Nice Inventory Tab made its layout again): a new row for it.
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
                sp.widget = Activator.CreateInstance(LabelType);
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

        /// <summary>A box's scrolling list of rows (its first child's children), or null.</summary>
        private static IList Layout(object box) =>
            Childs.GetValue(box) is IList outer && outer.Count > 0 && outer[0] != null ? Childs.GetValue(outer[0]) as IList : null;

        private static List<WeaponNiceMath.Child> Kinds(IList list, object ours)
        {
            kinds.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                var w = list[i];
                kinds.Add(ReferenceEquals(w, ours) ? WeaponNiceMath.Child.Ours : WeaponGearTab.NiceRow.IsInstanceOfType(w) ? WeaponNiceMath.Child.Held : WeaponNiceMath.Child.Other);
            }
            return kinds;
        }

        /// <summary>Rimfolk's row drawn where Nice Inventory Tab put it, in its scroll view; any other label widget is left alone.</summary>
        internal static void Draw(object widget)
        {
            var sp = ReferenceEquals(widget, alt.widget) ? alt : ReferenceEquals(widget, legacy.widget) ? legacy : null;
            if (sp?.pawn == null || !WeaponPickGearTab.On) return;
            var g = (Rect)Geometry.GetValue(widget);
            try
            {
                WeaponPickGearTab.Rows(g, sp.pawn, Look);
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        // ---------------- Nice Inventory Tab's colours ----------------

        private static SuggestLook look;

        /// <summary>A colour of Nice Inventory Tab's own (Assets), by name; its value in the build read when it can't be.</summary>
        private static Color Nice(string name, uint fallback)
        {
            try
            {
                if (AssetsType != null && AccessTools.Field(AssetsType, name)?.GetValue(null) is Color c) return c;
            }
            catch (Exception)
            {
                // Its value in the build read, below.
            }
            return new Color(SkinMath.R(fallback), SkinMath.G(fallback), SkinMath.B(fallback));
        }

        /// <summary>
        /// The look of Nice Inventory Tab's rows: its dark row ground (ColorBGD) and picture ground (ColorBG), its text
        /// (ColorStat, ColorLightGray), its good and bad (BuffColor, PenaltyColor), its lighter ground for the buttons (ColorBGL).
        /// </summary>
        internal static SuggestLook Look
        {
            get
            {
                if (look != null) return look;
                var good = Nice("BuffColor", 0x5de442);
                var fill = Nice("ColorBGL", 0x2f3337);
                return look = new SuggestLook
                {
                    ground = Nice("ColorBGD", 0x0e1013), tile = Nice("ColorBG", 0x15191d), ink = Nice("ColorStat", 0xe3e3e3),
                    faint = UiKit.WithAlpha(Nice("ColorLightGray", 0xc7c7c7), 0.85f), good = good, bad = Nice("PenaltyColor", 0xe95454),
                    warn = new Color(0.95f, 0.66f, 0.28f), frame = UiKit.WithAlpha(good, 0.6f), buttonFill = fill, buttonHover = Color.Lerp(fill, Color.white, 0.15f),
                    buttonInk = Nice("ColorStat", 0xe3e3e3)
                };
            }
        }
    }

    /// <summary>Rimfolk's row in Nice Inventory Tab's equipment box, kept in its place after the box's own recache; skipped without the mod.</summary>
    [HarmonyPatch]
    public static class Patch_WeaponNiceSuggestedPlace
    {
        private static bool broken;

        public static bool Prepare() => Target() != null;

        public static MethodBase TargetMethod() => Target();

        public static string StandsDownWhy() => WeaponNiceTab.PatchType == null ? "Nice Inventory Tab isn't loaded" : "";

        private static MethodBase Target() => WeaponNiceTab.PatchType != null && WeaponNiceTab.Ready ? AccessTools.Method(WeaponNiceTab.PatchType, "Recache", new[] { typeof(Pawn) }) : null;

        public static void Postfix(Pawn pawn)
        {
            if (broken) return;
            try
            {
                WeaponNiceTab.Place(pawn);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("the suggested weapons' place in Nice Inventory Tab's equipment box", e);
            }
        }
    }

    /// <summary>Rimfolk's row drawn in Nice Inventory Tab's equipment box (its own label widgets untouched); skipped without the mod.</summary>
    [HarmonyPatch]
    public static class Patch_WeaponNiceSuggestedDraw
    {
        private static bool broken;

        public static bool Prepare() => Target() != null;

        public static MethodBase TargetMethod() => Target();

        public static string StandsDownWhy() => WeaponNiceTab.LabelType == null ? "Nice Inventory Tab isn't loaded" : "";

        private static MethodBase Target() => WeaponNiceTab.LabelType != null && WeaponNiceTab.Ready ? AccessTools.Method(WeaponNiceTab.LabelType, "Draw") : null;

        public static void Postfix(object __instance)
        {
            if (broken) return;
            try
            {
                WeaponNiceTab.Draw(__instance);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("the suggested weapons in Nice Inventory Tab's equipment box", e);
            }
        }
    }
}
