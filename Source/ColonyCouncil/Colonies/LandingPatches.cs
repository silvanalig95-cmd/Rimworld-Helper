using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The start as the game makes it (Colonies/LandingRecord.cs): the things on the first map before and after the
    /// scenario's parts give the starting kit. Only while <c>Find.GameInitData</c> is set, which is the game's start alone.
    /// </summary>
    [HarmonyPatch(typeof(Scenario), nameof(Scenario.GenerateIntoMap))]
    public static class Patch_LandingRecord
    {
        public static void Prefix(Map map)
        {
            if (Find.GameInitData == null || !RimfolkPatches.On) return;
            try
            {
                LandingRecorder.Begin(map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("landing: the start before the kit", e);
            }
        }

        public static void Postfix(Map map)
        {
            if (Find.GameInitData == null) return;
            try
            {
                LandingRecorder.End(map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("landing: the start's kit", e);
            }
        }
    }

    /// <summary>A scatter part of the scenario: what it puts down near the start, or anywhere, is told apart in the record.</summary>
    [HarmonyPatch(typeof(ScenPart_ScatterThings), nameof(ScenPart_ScatterThings.GenerateIntoMap))]
    public static class Patch_LandingScatter
    {
        public static void Prefix(Map map)
        {
            if (Find.GameInitData == null) return;
            try
            {
                LandingRecorder.BeginScatter(map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("landing: before a scatter", e);
            }
        }

        public static void Postfix(ScenPart_ScatterThings __instance, Map map)
        {
            if (Find.GameInitData == null) return;
            try
            {
                bool near = __instance is ScenPart_ScatterThingsNearPlayerStart;
                if (!near && !(__instance is ScenPart_ScatterThingsAnywhere))
                {
                    // Another mod's scatter part: the game's own property says which.
                    var prop = AccessTools.Property(__instance.GetType(), "NearPlayerStart");
                    if (prop != null && prop.GetValue(__instance) is bool b) near = b;
                }
                LandingRecorder.EndScatter(map, near);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("landing: after a scatter", e);
            }
        }
    }

    /// <summary>
    /// A new map of the same place: the game seeds a map from the world's seed and its tile, so the same tile would give the
    /// same map. While Rimfolk makes one (<see cref="Landing.SeedTurn"/>), the seed the map's content is made from is turned.
    /// The world's own seed is never touched (the world's noise built lazily from it, a mineral-rich tile's ore, stays).
    /// </summary>
    [HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.GenerateContentsIntoMap))]
    public static class Patch_LandingSeed
    {
        public static void Prefix(ref int seed)
        {
            if (Landing.SeedTurn != 0) seed = LandingMath.TurnSeed(seed, Landing.SeedTurn);
        }
    }

    /// <summary>
    /// "Choose another landing site" in the game's own menu (Esc). The menu's options are drawn by
    /// <c>OptionListingUtility.DrawOptionListing</c>, called twice in <c>MainMenuDrawer.DoMainMenuControls</c>: first the
    /// main options, then the web links. The first call goes through <see cref="LandingMenu.Draw"/>, which adds the entry
    /// and draws the list. If the call isn't there, nothing changes, and it is said once in the log.
    /// </summary>
    [HarmonyPatch(typeof(MainMenuDrawer), nameof(MainMenuDrawer.DoMainMenuControls))]
    public static class Patch_LandingMenu
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            var target = AccessTools.Method(typeof(OptionListingUtility), nameof(OptionListingUtility.DrawOptionListing), new[] { typeof(Rect), typeof(List<ListableOption>) });
            var ours = AccessTools.Method(typeof(LandingMenu), nameof(LandingMenu.Draw));
            bool done = false;
            if (target != null && ours != null && ours.ReturnType == target.ReturnType)
                foreach (var ins in list)
                    if (ins.Calls(target))
                    {
                        ins.operand = ours;
                        done = true;
                        break;
                    }
            if (!done)
            {
                LandingMenu.Missing = true;
                Log.Warning("[Rimfolk] The game's menu isn't drawn the way Rimfolk expects, so \"Choose another landing site\" is only on the colony's card.");
            }
            return list;
        }
    }

    /// <summary>The menu window grows by one option's height while the entry shows, so the game's own last option isn't cut off.</summary>
    [HarmonyPatch(typeof(MainTabWindow_Menu), nameof(MainTabWindow_Menu.RequestedTabSize), MethodType.Getter)]
    public static class Patch_LandingMenuSize
    {
        public static void Postfix(ref Vector2 __result)
        {
            try
            {
                if (!LandingMenu.Missing && Landing.Shows) __result.y += LandingMenu.OptionHeight;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("landing: the menu's size", e);
            }
        }
    }

    /// <summary>The entry in the game's menu: before "Quit to main menu", greyed with why while it isn't offered.</summary>
    public static class LandingMenu
    {
        /// <summary>One option and the gap after it, as the game's option listing draws them.</summary>
        public const float OptionHeight = 52f;

        /// <summary>The menu's drawing call wasn't found: the entry is on the colony's card only.</summary>
        public static bool Missing;

        public static float Draw(Rect rect, List<ListableOption> list)
        {
            try
            {
                if (list != null && Landing.Shows && !list.Any(o => o is ListableOption_Landing))
                {
                    // As the game makes its labels: the translation as a plain string.
                    string quit = "QuitToMainMenu".Translate(), saveQuit = "SaveAndQuitToMainMenu".Translate();
                    int at = list.FindIndex(o => o.label == quit || o.label == saveQuit);
                    var entry = new ListableOption_Landing();
                    if (at >= 0) list.Insert(at, entry);
                    else list.Add(entry);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("landing: the menu's entry", e);
            }
            return OptionListingUtility.DrawOptionListing(rect, list);
        }
    }

    /// <summary>The game's menu option, drawn greyed with why when it isn't offered.</summary>
    public class ListableOption_Landing : ListableOption
    {
        public ListableOption_Landing() : base(Landing.Label, Open)
        {
        }

        private static void Open()
        {
            Find.MainTabsRoot.EscapeCurrentTab(false);
            Dialog_Landing.Open();
        }

        public override float DrawOption(Vector2 pos, float width)
        {
            float h = Mathf.Max(minHeight, Text.CalcHeight(label, width));
            var rect = new Rect(pos.x, pos.y, width, h);
            string why = Landing.WhyNot();
            if (Widgets.ButtonText(rect, label, true, true, why == null) && why == null) action();
            TooltipHandler.TipRegion(rect, why == null ? Landing.Tip : why);
            return h;
        }
    }
}
