using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: harm, part 5 (Debug actions, "Rimfolk", "Harm five: ..."). Each brings one of the fourteen about now, or
    /// makes its next chance come up, past its rate but never past the shared cap: what follows is the feature's own. The
    /// forecast that missed writes its note on the field under the mouse.
    /// </summary>
    public static class HarmFiveDebug
    {
        private static void Say(string text) => Messages.Message("Harm five: " + text, MessageTypeDefOf.SilentInput, false);

        private static void Pick(IEnumerable<Pawn> pawns, Func<Pawn, string> act)
        {
            var options = pawns.Select(p => new FloatMenuOption(p.LabelShort, () => Say(act(p)))).ToList();
            if (options.Count == 0) options.Add(new FloatMenuOption("(nobody here)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static IEnumerable<Pawn> Ours => Find.CurrentMap.mapPawns.FreeColonistsSpawned;

        private static IEnumerable<Pawn> Guests => Find.CurrentMap.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Humanlike && p.Faction != null && !p.Faction.IsPlayer && !p.HostileTo(Faction.OfPlayer));

        [DebugAction("Rimfolk", "Harm five: the sick work on (one of ours with a cold)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSickWorker() => Pick(Ours.Where(p => Catching.CatchingOf(p, out _) != null), Carriers.DebugSickWorker);

        [DebugAction("Rimfolk", "Harm five: a visitor or trader carries the plague...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGate() => Pick(Guests, Carriers.DebugGate);

        [DebugAction("Rimfolk", "Harm five: the next courier arrives sick", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCourier()
        {
            Carriers.DebugCourier = true;
            Say("the next courier's party to come onto a home map carries the plague's sickness (with the couriers and world events on).");
        }

        [DebugAction("Rimfolk", "Harm five: the camp's sick one is catching", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCamp() => Say(Carriers.DebugCamp(Find.CurrentMap));

        [DebugAction("Rimfolk", "Harm five: the next hospital patient brings something worse", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPatient()
        {
            Carriers.DebugPatient = true;
            Say("the next patient for the hospital brings tuberculosis or cholera; whoever sleeps in their room may catch it.");
        }

        [DebugAction("Rimfolk", "Harm five: the next healer's batch is bad", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBatch()
        {
            Carriers.DebugBatch = true;
            Say("the next travelling healer's paid batch is bad (a doctor of Medicine 8 or more still catches it).");
        }

        [DebugAction("Rimfolk", "Harm five: the next worms or bites looked at grow", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGrow()
        {
            Untended.DebugGrow = true;
            Say("the next colonist's hour with untended worms or insect bites grows them into gut worms or malaria.");
        }

        [DebugAction("Rimfolk", "Harm five: dirty water (what Dubs Bad Hygiene reads as)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWater()
        {
            Untended.DebugWater = true;
            Say(Untended.DebugReadWater());
        }

        [DebugAction("Rimfolk", "Harm five: the next cold wait at the gate bites", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGateCold()
        {
            ColdHurts.DebugGate = true;
            Say("the next traveller's hour waiting at the gate past the hour brings frostnip or chilblains, frost or not.");
        }

        [DebugAction("Rimfolk", "Harm five: the next hour on the night watch bites", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWatch()
        {
            ColdHurts.DebugWatch = true;
            Say("the next watcher's hour on the walls brings frostnip or chilblains, frost or not.");
        }

        [DebugAction("Rimfolk", "Harm five: a predator comes for the next one out alone", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPredator()
        {
            ColdHurts.DebugPredator = true;
            NightWalks.DebugPredator = true;
            Say("the next watcher or restless walker outside the walls draws a hungry predator in reach, if there is one.");
        }

        [DebugAction("Rimfolk", "Harm five: a weather lover's next hour in it", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLover()
        {
            ColdHurts.DebugLover = true;
            Say("the next colonist who stands in the rain or loves the snow, outdoors in their hour, takes the chest cold or frostnip.");
        }

        [DebugAction("Rimfolk", "Harm five: sleepwalk now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSleepwalk() => Pick(Ours, NightWalks.DebugSleepwalk);

        [DebugAction("Rimfolk", "Harm five: a restless walk now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRestless() => Pick(Ours, NightWalks.DebugRestless);

        [DebugAction("Rimfolk", "Harm five: the next fisher slips in", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFish()
        {
            ColdHurts.DebugFish = true;
            Say("the next colonist's hour fishing by the water ends in a slip into it, frost or not.");
        }

        [DebugAction("Rimfolk", "Harm five: the forecast missed (the field under the mouse)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugForecast()
        {
            var map = Find.CurrentMap;
            var z = UI.MouseCell().GetZone(map) as Zone_Growing;
            Say(z == null ? "put the mouse over a growing zone." : FieldWeather.Of(map)?.DebugMissed(z) ?? "no field weather on this map.");
        }
    }
}
