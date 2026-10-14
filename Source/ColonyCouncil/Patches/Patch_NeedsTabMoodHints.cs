using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The game's own Needs tab: hovering a mood also shows which mod it comes from, why it's there and what to do about
    /// it (the same as Rimfolk's Mood tab), as more lines in the game's own tooltip. The game builds that tooltip inside
    /// DrawThoughtGroup and hands it to TooltipHandler.TipRegion; the transpiler sends that one call through
    /// <see cref="TipRegion"/>, which adds Rimfolk's lines and passes it on. If another game version builds it some
    /// other way, the call isn't found and the lines go in a box of their own under the mouse instead (the postfix).
    /// </summary>
    [HarmonyPatch(typeof(NeedsCardUtility), "DrawThoughtGroup")]
    public static class Patch_NeedsTabMoodHints
    {
        /// <summary>The game's tooltip call was found and goes through <see cref="TipRegion"/>.</summary>
        private static bool combined;

        // Rimfolk's lines for the mood under the mouse, worked out every half second or so rather than every frame.
        // Kept by id, not by reference, so a thought or colonist from an earlier game isn't held on to.
        private static int cachedThought, cachedPawn, cachedFrame = -9999;
        private static ThoughtDef cachedDef;
        private static string cachedText;
        // One tooltip per mood row and frame gets the lines, should the game draw more than one there.
        private static int addedFrame = -1, addedThought;

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            combined = false;
            var list = instructions.ToList();
            var ps = original?.GetParameters();
            int groupArg = ps == null ? -1 : Array.FindIndex(ps, p => typeof(Thought).IsAssignableFrom(p.ParameterType));
            int pawnArg = ps == null ? -1 : Array.FindIndex(ps, p => p.ParameterType == typeof(Pawn));
            if (groupArg < 0 || pawnArg < 0) return list;
            // A static method's arguments start at 0; an instance method's at 1 (this).
            int offset = original.IsStatic ? 0 : 1;
            // The game's call (a tip signal), and the text-getter form in case another version uses that one.
            var swaps = new List<(MethodInfo game, MethodInfo ours)>
            {
                (AccessTools.Method(typeof(TooltipHandler), nameof(TooltipHandler.TipRegion), new[] { typeof(Rect), typeof(TipSignal) }),
                    AccessTools.Method(typeof(Patch_NeedsTabMoodHints), nameof(TipRegion))),
                (AccessTools.Method(typeof(TooltipHandler), nameof(TooltipHandler.TipRegion), new[] { typeof(Rect), typeof(Func<string>), typeof(int) }),
                    AccessTools.Method(typeof(Patch_NeedsTabMoodHints), nameof(TipRegionGetter)))
            };
            swaps.RemoveAll(s => s.game == null || s.ours == null);
            var result = new List<CodeInstruction>(list.Count + 4);
            foreach (var ci in list)
            {
                var swap = swaps.FirstOrDefault(s => ci.Calls(s.game));
                if (swap.game != null)
                {
                    // On the stack: the game's arguments. Add the mood and the colonist, and call ours instead.
                    var loadGroup = CodeInstruction.LoadArgument(groupArg + offset);
                    ci.MoveLabelsTo(loadGroup);
                    ci.MoveBlocksTo(loadGroup);
                    result.Add(loadGroup);
                    result.Add(CodeInstruction.LoadArgument(pawnArg + offset));
                    result.Add(new CodeInstruction(System.Reflection.Emit.OpCodes.Call, swap.ours));
                    combined = true;
                    continue;
                }
                result.Add(ci);
            }
            return result;
        }

        /// <summary>The text-getter form of the game's call, sent the same way.</summary>
        public static void TipRegionGetter(Rect rect, Func<string> textGetter, int uniqueId, Thought group, Pawn pawn) =>
            TipRegion(rect, new TipSignal(textGetter, uniqueId), group, pawn);

        /// <summary>The game's tooltip for a mood row, with Rimfolk's lines added under its own text.</summary>
        public static void TipRegion(Rect rect, TipSignal tip, Thought group, Pawn pawn)
        {
            try
            {
                var ev = Event.current;
                if (ev != null && ev.type == EventType.Repaint && Wanted(group, pawn) && Mouse.IsOver(rect))
                {
                    int id = RuntimeHelpers.GetHashCode(group);
                    int frame = Time.frameCount;
                    if (addedFrame != frame || addedThought != id)
                    {
                        addedFrame = frame;
                        addedThought = id;
                        string extra = Lines(pawn, group);
                        if (!extra.NullOrEmpty())
                        {
                            if (tip.textGetter != null)
                            {
                                var own = tip.textGetter;
                                tip.textGetter = () => Join(own(), extra);
                            }
                            else tip.text = Join(tip.text, extra);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("needs tab hint", e);
            }
            TooltipHandler.TipRegion(rect, tip);
        }

        /// <summary>Only if the game's tooltip call couldn't be found: Rimfolk's lines in a box of their own.</summary>
        public static void Postfix(Rect rect, Thought group, Pawn pawn)
        {
            if (combined || !Wanted(group, pawn) || !Mouse.IsOver(rect)) return;
            try
            {
                var thought = group;
                var who = pawn;
                TooltipHandler.TipRegion(rect, () => Lines(who, thought), Gen.HashCombineInt(thought.def.shortHash, who.thingIDNumber ^ 0x7A11));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("needs tab hint", e);
            }
        }

        private static bool Wanted(Thought group, Pawn pawn)
        {
            if (!RimfolkPatches.On || group?.def == null || pawn == null) return false;
            var s = ColonyCouncilMod.Settings;
            return s != null && s.moodInsight && s.needsTabHints;
        }

        private static string Lines(Pawn pawn, Thought group)
        {
            int thought = RuntimeHelpers.GetHashCode(group);
            int frame = Time.frameCount;
            if (cachedText == null || thought != cachedThought || group.def != cachedDef || pawn.thingIDNumber != cachedPawn || frame - cachedFrame > 30 || frame < cachedFrame)
            {
                cachedText = MoodInsight.HintLines(pawn, group) ?? "";
                cachedThought = thought;
                cachedDef = group.def;
                cachedPawn = pawn.thingIDNumber;
                cachedFrame = frame;
            }
            return cachedText;
        }

        private static string Join(string game, string extra) => game.NullOrEmpty() ? extra : game.TrimEnd() + "\n\n" + extra;
    }
}
