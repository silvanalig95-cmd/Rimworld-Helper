using System;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the land's climate (Debug actions, "Rimfolk", "Climate: ..."): this land's next seasons as drawn, with the
    /// drift; once in a lifetime set going now; the season's forecast said again.
    /// </summary>
    public static class ClimateDebug
    {
        private static void Say(string text) => Messages.Message("Climate: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Climate: this land's next eight seasons and the drift (to the log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSeasons()
        {
            string text = LandClimate.DevSeasons(Find.CurrentMap, 8);
            Log.Message("[Rimfolk] The land's climate:\n" + text);
            Say("written to the log.");
        }

        [DebugAction("Rimfolk", "Climate: once in a lifetime now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRare()
        {
            var map = Find.CurrentMap;
            if (map == null) return;
            var kinds = ((RareKind[])Enum.GetValues(typeof(RareKind))).Where(k => k != RareKind.None);
            Find.WindowStack.Add(new FloatMenu(kinds.Select(k => new FloatMenuOption(k.ToString(), () => Say(LandClimate.DevRare(map, k)))).ToList()));
        }

        [DebugAction("Rimfolk", "Climate: the season's forecast now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugForecast() => Say(LandClimate.DevForecast(Find.CurrentMap));
    }
}
