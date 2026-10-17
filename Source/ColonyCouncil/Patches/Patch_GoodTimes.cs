using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Getting used to good times (GoodTimes.cs). Every memory a colonist gains passes through here, however it's given
    /// (the council, a moment, an encounter, a dilemma, a world event): one of Rimfolk's good memories lasts less long
    /// when the colonist's mood is already high. A memory that renews one already there, instead of joining the list,
    /// passes its time on to that one. The same hook lets a memory leave a mark on a colonist's character (PersonaMarks).
    /// </summary>
    [HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory), new[] { typeof(Thought_Memory), typeof(Pawn) })]
    public static class Patch_GoodTimesFade
    {
        public static void Prefix(MemoryThoughtHandler __instance, Thought_Memory newThought, out int __state)
        {
            __state = GoodTimes.Untouched;
            try
            {
                __state = GoodTimes.BeforeGiven(__instance, newThought);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("good times fade", e);
            }
        }

        public static void Postfix(MemoryThoughtHandler __instance, Thought_Memory newThought, int __state)
        {
            // What happens to people changes them: some memories leave a mark on their character (Persona/Marks.cs).
            PersonaMarks.Notify_Memory(__instance?.pawn, newThought);
            if (__state == GoodTimes.Untouched) return;
            try
            {
                GoodTimes.AfterGiven(__instance, newThought, __state);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("good times fade", e);
            }
        }
    }

    /// <summary>
    /// Standards cover the game's comforts (GoodTimes.cs). The game adds up a colonist's mood group by group through
    /// this method, and the Needs tab shows the same number, so a good mood from the game or another mod is felt (and
    /// shown) a little less by a colonist with high standards. Bad moods and Rimfolk's own pass through unchanged.
    /// </summary>
    [HarmonyPatch(typeof(ThoughtHandler), nameof(ThoughtHandler.MoodOffsetOfGroup))]
    public static class Patch_ComfortStandards
    {
        public static void Postfix(ThoughtHandler __instance, Thought group, ref float __result)
        {
            if (__result <= 0f || GoodTimes.Raw) return;
            try
            {
                __result = GoodTimes.Felt(__instance, group, __result);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("standards and comforts", e);
            }
        }
    }
}
