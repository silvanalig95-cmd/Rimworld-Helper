using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the animals' days and scenes (Debug actions, "Rimfolk", "Animals: ..."): whom each animal is with, any of
    /// the daily moments now on this map past its pace and its wait, and an encounter's scene played for a colonist and an
    /// animal picked (Animals/AnimalDays.cs, AnimalScenes.cs).
    /// </summary>
    public static class AnimalDaysDebug
    {
        private static void Say(string text) => Messages.Message("Animals: " + text, MessageTypeDefOf.SilentInput, false);

        private static void Menu(IEnumerable<FloatMenuOption> options, string none)
        {
            var list = options.ToList();
            if (list.Count == 0) list.Add(new FloatMenuOption(none, null));
            Find.WindowStack.Add(new FloatMenu(list));
        }

        [DebugAction("Rimfolk", "Animals: whom each animal is with (bond, master, fond of)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPeople()
        {
            string text = AnimalDays.DebugPeople(Find.CurrentMap);
            Log.Message("[Rimfolk] Whom the colony's animals here are with:\n" + text);
            Say("the list is in the log.");
        }

        [DebugAction("Rimfolk", "Animals: a daily moment now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDay()
        {
            Menu(((AnimalDay[])Enum.GetValues(typeof(AnimalDay))).Select(d => new FloatMenuOption(d.ToString(), () => Say(AnimalDays.DebugNow(Find.CurrentMap, d)))), "(none)");
        }

        [DebugAction("Rimfolk", "Animals: an encounter's scene now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugScene()
        {
            var map = Find.CurrentMap;
            if (map == null) return;
            var scenes = ((AnimalScene[])Enum.GetValues(typeof(AnimalScene))).Where(s => s != AnimalScene.None);
            Menu(scenes.Select(s => new FloatMenuOption(s.ToString(), () =>
                Menu(map.mapPawns.FreeColonistsSpawned.Select(p => new FloatMenuOption(p.LabelShort, () =>
                    Menu(map.mapPawns.SpawnedColonyAnimals.OrderBy(a => a.Position.DistanceToSquared(p.Position))
                        .Select(a => new FloatMenuOption(a.LabelShort + " (" + AnimalFacts.KindLabel(a) + ")", () => Say(AnimalScenes.DebugPlay(s, p, a)))), "(no animal of the colony here)"))),
                    "(no colonist here)"))), "(none)");
        }
    }
}
