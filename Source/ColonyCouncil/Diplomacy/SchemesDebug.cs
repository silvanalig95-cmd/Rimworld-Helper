using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: schemes against the colony (Debug actions, "Rimfolk", "Schemes: ..."). A scheme begun now past the rate and
    /// the season, its act at any hour, the next act caught or not whatever the watch, a smear told now, and what is under
    /// way: what follows is the feature's own (one of theirs on the map, the harm service's cap for a fight).
    /// </summary>
    public static class SchemesDebug
    {
        private static void Say(string text) => Messages.Message("Schemes: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Schemes: a scheme against this home now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBegin()
        {
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            foreach (var act in new[] { SchemeAct.Machine, SchemeAct.Theft, SchemeAct.Fight, SchemeAct.Smear })
            {
                var a = act;
                options.Add(new FloatMenuOption(a.ToString(), () =>
                {
                    if (!Schemes.On)
                    {
                        Say("needs the setting above 0% and answers on the map.");
                        return;
                    }
                    var who = Schemes.DebugSchemer(map);
                    if (!who.HasValue)
                    {
                        Say("nobody to scheme: bring a visitor or a trader's caravan of a people at peace (the game's dev tools, Incidents).");
                        return;
                    }
                    string why = Schemes.Roll(map, Find.TickManager.TicksGame, 1f, true, a, who.Value);
                    Say(why ?? $"{DipWords.NameOf(who.Value)} means a {a.ToString().ToLowerInvariant()} against this home; one of theirs acts at its hour (the night, or by day for a fight).");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Schemes: their act at any hour (on or off)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAnyHour()
        {
            Schemes.anyHour = !Schemes.anyHour;
            Say(Schemes.anyHour ? "acts at any hour, from the next hour's look." : "acts at their own hour again.");
        }

        [DebugAction("Rimfolk", "Schemes: the next act is caught", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCaught()
        {
            Schemes.forceCaught = true;
            Say("the next act is caught, by whoever watches (else the nearest of the colony, woken).");
        }

        [DebugAction("Rimfolk", "Schemes: the next act is not caught", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugUnseen()
        {
            Schemes.forceCaught = false;
            Say("the next act goes unseen.");
        }

        [DebugAction("Rimfolk", "Schemes: a smear carried home is told now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLand() => Say(Schemes.DebugLand(Find.CurrentMap));

        [DebugAction("Rimfolk", "Schemes: what is under way", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugList()
        {
            string schemers = string.Join(", ", Schemes.Schemers(Find.CurrentMap).Select(DipWords.NameOf));
            Say(Schemes.DebugList() + " Schemers in reach: " + (schemers.Length > 0 ? schemers : "none") + ".");
        }
    }
}
