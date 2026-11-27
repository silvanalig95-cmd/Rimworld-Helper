using System;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the content round 3's six happenings of the gods on the land now (Debug actions, "Rimfolk", "Gods: &lt;god&gt;'s
    /// ... now"), kept out of Gods/OnMap as the others' are. Each calls the happening as its hour would, past its pace (what it
    /// needs on the map it still needs: a colony animal, a field, wet or wooded ground, an unfinished piece, a shore).
    /// </summary>
    public static class OwnRound3Debug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        private static void Now(OwnHappening h, Func<OwnHappeningsBook, God, Map, int, bool> fire, string none)
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            var g = OwnHappenings.GodOf(h);
            if (b == null || map == null || g == null)
            {
                Say("the gods are off, no game, or no " + OwnHappeningsMath.GodOf(h) + " in this pantheon.");
                return;
            }
            OwnHappenings.forced = h;
            bool done;
            try
            {
                done = fire(b, g, map, Find.TickManager.TicksGame);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods' own happenings, round 3: a dev action", e);
                done = false;
            }
            finally
            {
                OwnHappenings.forced = null;
            }
            Say(done ? Gods.Name(g) + "'s " + OwnHappeningsMath.Name(h).ToLowerInvariant() + ": under way." : none);
        }

        [DebugAction("Rimfolk", "Gods: Bellwether's ring in the snow now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Newborn() => Now(OwnHappening.BellwetherNewborn, (b, g, m, n) =>
            OwnRound3.Ring(b, g, m, OwnRound3.Newborn(m) ?? m.mapPawns.SpawnedColonyAnimals.OrderBy(a => a.ageTracker.AgeBiologicalTicks).FirstOrDefault(), n), "no animal of the colony's here.");

        [DebugAction("Rimfolk", "Gods: Helios's field ripe at noon now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Ripen() => Now(OwnHappening.HeliosRipen, (b, g, m, n) => OwnRound3.Ripen(b, g, m, OwnRound3.NearRipe(m, out _, out _), n), "no field growing in the open.");

        [DebugAction("Rimfolk", "Gods: Morel's light on the wet ground now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Foxfire() => Now(OwnHappening.MorelFoxfire, (b, g, m, n) => OwnRound3.BeginFoxfire(b, m, OwnRound3.Patch(m), n),
            "no wet or wooded ground with soil beside it near the colony, or the light is already there (it fades at first light).");

        [DebugAction("Rimfolk", "Gods: Pip's work done in the night now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Finished() => Now(OwnHappening.PipFinished, (b, g, m, n) =>
        {
            var piece = OwnRound3.Piece(m, out var bench);
            return OwnRound3.BeginFinished(b, m, piece, bench, n);
        }, "no unfinished piece at a bench with nobody at it, or one is already under way (the maker looks at six in the morning).");

        [DebugAction("Rimfolk", "Gods: Selene's full-moon tide now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Tide() => Now(OwnHappening.SeleneTide, (b, g, m, n) => OwnRound3.BeginTide(b, m, OwnRound3.Shore(m), n),
            "no empty shore by the sea near the colony, or the tide is already up (it goes back at first light).");

        [DebugAction("Rimfolk", "Gods: Cirrus's giant in the mist now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Glory() => Now(OwnHappening.CirrusGlory, (b, g, m, n) => OwnRound3.Glory(b, g, m, OwnRound3.OutInTheOpen(b, m), n), "nobody of the colony out in the open and free.");
    }
}
