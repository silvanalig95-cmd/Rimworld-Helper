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
    /// The game's temperature readout at the bottom right ("Indoors 21C") on a field in the temperature's colour, by the
    /// colonists' comfort: deep blue in killing cold, white where they're comfortable, deep red in killing heat; the words
    /// dark on it and a thermometer at its left, filled to the temperature's height, its tooltip the temperature and a word for it (TemperatureScaleMath; the
    /// setting "The game's temperature in colour", with the deeper game hooks). The game draws that line in
    /// GlobalControlsOnGUI with Widgets.Label, its words from TemperatureString; the transpiler sends that one label through
    /// <see cref="Label"/>, which draws the field, the words on it and the thermometer just left of them. The
    /// temperature is the one the game made its words from (its own cached value), so the thermometer always matches the
    /// number shown. Should another game version draw the line some other way, the call isn't found and
    /// <see cref="Patch_TemperatureReadoutText"/> colours the words themselves instead (no thermometer).
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

        // What the words last shown come to: the game keeps the same words (the same string) until the number or the place
        // changes, so all of it is worked out again only then (the words' width once the font is known).
        private static string lastText;
        private static uint lastRgb = TemperatureScaleMath.Neutral;
        private static float lastLevel, lastWidth = -1f;
        private static string lastTip;
        private static GameFont lastFont;
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

        /// <summary>The game's temperature line: a field in the colour behind it, its words dark on the field, the thermometer left of them; with the setting off, as the game draws it.</summary>
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
                // The words are drawn against the line's right edge: the thermometer just left of them.
                if (lastWidth < 0f || lastFont != Text.Font)
                {
                    lastFont = Text.Font;
                    lastWidth = Text.CalcSize(text).x;
                }
                float h = Mathf.Min(rect.height - 2f, TemperatureScaleMath.ThermoHeight), w = TemperatureScaleMath.ThermoWidth * h / TemperatureScaleMath.ThermoHeight;
                var at = new Rect(rect.xMax - lastWidth - 3f - w, rect.y + (rect.height - h) / 2f, w, h);
                // A field in the temperature's colour behind the thermometer and the words, a little wider than both.
                var field = new Rect(at.x - TemperatureScaleMath.FieldPadX, rect.y + TemperatureScaleMath.FieldPadY,
                    rect.xMax - at.x + TemperatureScaleMath.FieldPadX * 2f, rect.height - TemperatureScaleMath.FieldPadY * 2f);
                var fill = TemperatureScale.ColorOf(rgb);
                UiKit.Round(field, new Color(fill.r, fill.g, fill.b, TemperatureScaleMath.FieldAlpha * was.a), SkinMath.RadiusSmall);
                var words = TemperatureScale.ColorOf(TemperatureScaleMath.FieldText(rgb));
                GUI.color = new Color(words.r, words.g, words.b, was.a);
                Widgets.Label(rect, text);
                GUI.color = was;
                Thermometer.Draw(at, lastLevel, TemperatureScale.ColorOf(TemperatureScaleMath.FieldThermo(rgb)), was.a);
                var tip = new Rect(at.x - 2f, rect.y, rect.xMax - at.x + 2f, rect.height);
                if (lastTip != null && Mouse.IsOver(tip)) TooltipHandler.TipRegion(tip, lastTip);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the game's temperature in colour: the thermometer", e);
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
            lastWidth = -1f;
            lastTip = null;
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
                    float c = (float)shown.GetValue(null);
                    lastRgb = TemperatureScale.Rgb(c);
                    lastLevel = TemperatureScale.Level(c);
                    lastTip = TemperatureScale.Tip(c);
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
