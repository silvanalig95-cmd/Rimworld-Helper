using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Every memory a colonist gains passes through here, however it's given (the council, a moment, an encounter, a
    /// dilemma, a world event). A small letdown or joy of Rimfolk's becomes a short moment first (SmallMoments). It lets a memory leave a mark on a colonist's character (PersonaMarks), lets a deep talk
    /// or a good time with someone close make a quiet trouble pass sooner (Troubles), and counts how often each kind has
    /// come lately (GoodLife: the everyday ones grow ordinary soonest).
    /// </summary>
    [HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory), new[] { typeof(Thought_Memory), typeof(Pawn) })]
    public static class Patch_GoodTimesFade
    {
        /// <summary>A small letdown or joy of Rimfolk's becomes a moment, and the memory goes on without a mood (Moods/SmallMoments.cs).</summary>
        public static void Prefix(MemoryThoughtHandler __instance, Thought_Memory newThought, Pawn otherPawn)
        {
            try
            {
                SmallMoments.Take(__instance, newThought, otherPawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("small letdowns and joys", e);
            }
        }

        public static void Postfix(MemoryThoughtHandler __instance, Thought_Memory newThought)
        {
            // What happens to people changes them: some memories leave a mark on their character (Persona/Marks.cs).
            PersonaMarks.Notify_Memory(__instance?.pawn, newThought);
            // Some memories are counted towards a trait that changes (Persona/TraitChanges.cs): human flesh eaten while starving.
            TraitChanges.Notify_Memory(__instance?.pawn, newThought);
            // A good meal lifts one who saves the best bite for last a while longer (Quirks/QuirkMoments.cs).
            QuirkMoments.Notify_Memory(__instance?.pawn, newThought);
            // A deep talk or a good time with a friend makes a quiet trouble pass sooner (Setbacks/Troubles.cs).
            Troubles.Notify_Memory(__instance?.pawn, newThought);
            // How often this kind has come lately: the everyday ones lose most to a good life (Moods/GoodLife.cs).
            GoodLife.Notify_Memory(__instance?.pawn, newThought);
        }
    }

    /// <summary>
    /// Good things grow ordinary (Moods/GoodLife.cs). The game adds up a colonist's mood group by group through this
    /// method, and the Needs tab shows the same number, so every good mood (the game's, other mods' and Rimfolk's) is
    /// felt (and shown) a share less by one whose good moods pile up past their standards' line, the everyday ones most
    /// and the new ones least. Bad moods pass through unchanged.
    /// </summary>
    [HarmonyPatch(typeof(ThoughtHandler), nameof(ThoughtHandler.MoodOffsetOfGroup))]
    public static class Patch_ComfortStandards
    {
        public static void Postfix(ThoughtHandler __instance, Thought group, ref float __result)
        {
            if (__result <= 0f || GoodLife.Raw) return;
            float share = 1f;
            try
            {
                share = GoodLife.ShareOf(__instance?.pawn, group?.def);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("good things grown ordinary", e);
            }
            if (share < 1f) __result *= share;
        }
    }
}
