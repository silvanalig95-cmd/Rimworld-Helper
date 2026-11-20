using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The comms console reaches only peoples with a radio (the player, 20 November 2026, of the Stormriders: "if they are
    /// still on older technology, it doesn't make sense if they are all reachable with the comms console", and the go: "only
    /// the ones with radio can answer"; DESIGN.md "The comms console by radio, and the foreign peoples' ages"). The game's
    /// console lists every people whatever its age; its call to a people of the tribal or medieval age (by the age it lives
    /// in now, Rimfolk's where Rimfolk keeps one: Peoples.TechOf), with no town within reach of the Signal Line's masts, is
    /// listed greyed with the reason and how a letter reaches them (PostReach.Callable, the same rule as every call of
    /// Rimfolk's). Only the console's own call per people changes (Faction.CommFloatMenuOption): ships in orbit, other
    /// mods' contacts on the console, quests, the Empire's permits and a people's dialog opened any other way are as they
    /// were, and the Empire, the outlanders, the pirates and every people of the industrial age or later answer as before.
    /// Setting "Only peoples with a radio answer the comms console" (on); off with the deeper game hooks.
    /// </summary>
    public static class ConsoleRadio
    {
        public static bool On => ColonyCouncilMod.Settings?.radioConsole ?? true;

        /// <summary>The console reaches them now (PostReach.Callable): the colony itself and no people at all are never greyed.</summary>
        public static bool Reaches(Faction f) => f == null || f.IsPlayer || PostReach.Callable(f);

        /// <summary>The console's call to a people, greyed with its reason where they have no radio; else left as the game made it.</summary>
        public static void Grey(FloatMenuOption option, Faction f)
        {
            if (option == null || f == null || !ConsoleRadioMath.Greyed(On, RimfolkPatches.On, f.IsPlayer, Reaches(f))) return;
            bool letters = NeighbourLetters.On;
            var args = new[] { f.Name.Named("PEOPLE"), PeopleAgeWords.AgeWord(Peoples.AgeOf(f)).Named("AGE") };
            option.action = null;
            option.Disabled = true;
            option.Label = option.Label + " (" + (letters ? "CC_ConsoleRadio_ShortLetter" : "CC_ConsoleRadio_Short").Translate().Resolve() + ")";
            option.tooltip = new TipSignal(ConsoleRadioMath.Key(letters).Translate(args).Resolve());
        }
    }

    /// <summary>
    /// The comms console's own call to a people (the game's float menu option a people gives the console): greyed with its
    /// reason when they have no radio (ConsoleRadio.Grey). A postfix, so the game's option is made as always and only
    /// changed after; anything else that opens a people's dialog is left alone.
    /// </summary>
    [HarmonyPatch(typeof(Faction), nameof(Faction.CommFloatMenuOption))]
    public static class Patch_ConsoleRadio
    {
        public static void Postfix(Faction __instance, FloatMenuOption __result)
        {
            if (__result == null || !RimfolkPatches.On || !ConsoleRadio.On) return;
            try
            {
                ConsoleRadio.Grey(__result, __instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the comms console by radio", e);
            }
        }
    }
}
