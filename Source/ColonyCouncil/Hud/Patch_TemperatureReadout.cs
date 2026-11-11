using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The game's temperature readout at the bottom right ("Indoors 21C") in colour, by the colonists' comfort: deep blue in
    /// killing cold, white where they're comfortable, deep red in killing heat (TemperatureScaleMath; the setting "The game's
    /// temperature in colour", with the deeper game hooks). The game draws that line in GlobalControlsOnGUI with
    /// Widgets.Label, its words from TemperatureString; the transpiler sends that one label through <see cref="Label"/>,
    /// which draws it in the temperature's colour with a thin dark edge round the words, so the colour reads over snow as
    /// well as over dark ground. The temperature is the one the game made its words from (its own cached value), so the
    /// colour always matches the number shown. Should another game version draw the line some other way, the call isn't
    /// found and <see cref="Patch_TemperatureReadoutText"/> colours the words themselves instead (no edge).
    /// </summary>
    [HarmonyPatch(typeof(GlobalControls), nameof(GlobalControls.GlobalControlsOnGUI))]
    public static class Patch_TemperatureReadout
    {
        /// <summary>The game's label for the temperature line was found and goes through <see cref="Label"/>.</summary>
        internal static bool swapped;

        /// <summary>The setting "The game's temperature in colour", with the deeper game hooks.</summary>
        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.tempReadoutColour ?? true);

        // The temperature the game's words were made from (GlobalControls keeps it to itself), found by name once.
        private static bool looked;
        private static FieldInfo shown;

        // The colour of the words last shown: the game keeps the same words (the same string) until the number or the
        // place changes, so the colour is worked out again only then.
        private static string lastText;
        private static uint lastRgb = TemperatureScaleMath.Neutral;
        private static bool lastOk;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            swapped = false;
            var list = instructions.ToList();
            var words = AccessTools.Method(typeof(GlobalControls), "TemperatureString");
            var label = AccessTools.Method(typeof(Widgets), nameof(Widgets.Label), new[] { typeof(Rect), typeof(string) });
            var ours = AccessTools.Method(typeof(Patch_TemperatureReadout), nameof(Label));
            if (words == null || label == null || ours == null) return list;
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].Calls(words)) continue;
                // The label that draws those words: the next call after them.
                for (int j = i + 1; j < list.Count && j <= i + 3; j++)
                {
                    if (!list[j].Calls(label)) continue;
                    list[j].operand = ours;
                    swapped = true;
                    break;
                }
                break;
            }
            return list;
        }

        /// <summary>The game's temperature line: in its colour with a dark edge, or as the game draws it with the setting off.</summary>
        public static void Label(Rect rect, string text)
        {
            if (!On || text.NullOrEmpty() || !Colour(text, out uint rgb))
            {
                Widgets.Label(rect, text);
                return;
            }
            var was = GUI.color;
            try
            {
                // A dark edge one pixel round the words (the words drawn dark a pixel to each side), then the words in colour.
                GUI.color = new Color(0f, 0f, 0f, TemperatureScaleMath.EdgeAlpha * was.a);
                Widgets.Label(new Rect(rect.x - 1f, rect.y, rect.width, rect.height), text);
                Widgets.Label(new Rect(rect.x + 1f, rect.y, rect.width, rect.height), text);
                Widgets.Label(new Rect(rect.x, rect.y - 1f, rect.width, rect.height), text);
                Widgets.Label(new Rect(rect.x, rect.y + 1f, rect.width, rect.height), text);
                var c = TemperatureScale.ColorOf(rgb);
                GUI.color = new Color(c.r, c.g, c.b, was.a);
                Widgets.Label(rect, text);
            }
            finally
            {
                GUI.color = was;
            }
        }

        /// <summary>The colour for the game's words now: false when the temperature can't be read (they are drawn as the game draws them).</summary>
        internal static bool Colour(string text, out uint rgb)
        {
            if (ReferenceEquals(text, lastText))
            {
                rgb = lastRgb;
                return lastOk;
            }
            lastText = text;
            lastOk = false;
            lastRgb = TemperatureScaleMath.Neutral;
            try
            {
                if (!looked)
                {
                    looked = true;
                    shown = AccessTools.Field(typeof(GlobalControls), "cachedTemperatureStringForTemperature");
                    if (shown == null || !shown.IsStatic || shown.FieldType != typeof(float)) shown = null;
                }
                if (shown != null)
                {
                    lastRgb = TemperatureScale.Rgb((float)shown.GetValue(null));
                    lastOk = true;
                }
            }
            catch (Exception e)
            {
                shown = null;
                CouncilLog.ErrorOnce("the game's temperature in colour", e);
            }
            rgb = lastRgb;
            return lastOk;
        }
    }

    /// <summary>
    /// Only if the game's label for the temperature line couldn't be found (<see cref="Patch_TemperatureReadout"/>): the
    /// game's words for it come back in their colour as rich text, which its label reads. Kept for the session by the words
    /// they were made from, so nothing new is made each frame.
    /// </summary>
    [HarmonyPatch(typeof(GlobalControls), "TemperatureString")]
    public static class Patch_TemperatureReadoutText
    {
        private static string from, made;

        public static void Postfix(ref string __result)
        {
            if (Patch_TemperatureReadout.swapped || !Patch_TemperatureReadout.On || __result.NullOrEmpty()) return;
            try
            {
                if (!ReferenceEquals(__result, from))
                {
                    from = __result;
                    made = Patch_TemperatureReadout.Colour(__result, out uint rgb) ? TemperatureScaleMath.Wrap(__result, rgb) : __result;
                }
                __result = made;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the game's temperature in colour (its words)", e);
            }
        }
    }
}
