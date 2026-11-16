using LudeonTK;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode for gifts worth what they cost (Gods/Worship/OfferingGifts.cs, Gods/Touch/GodTouchReturn.cs, Gods/GodTemper.cs):
    /// someone asks to give something precious now, what the stores hold that is precious here, every taking answered in
    /// kind while it is on, and the keeper reading every god now. Each says what it did in the log.
    /// </summary>
    public static class OfferingGiftsDebug
    {
        [DebugAction("Rimfolk", "Gods: someone asks to give something precious now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AskNow() =>
            Log.Message("[Rimfolk] " + (OfferingGifts.DevAsk(Find.CurrentMap)
                ? "Someone devout asks to give something precious (the card is on the stack)."
                : "Nobody devout here holds a god with a place here, or the stores hold nothing precious of its liking to spare."));

        [DebugAction("Rimfolk", "Gods: what is precious here (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PreciousHere() => Log.Message("[Rimfolk] What the stores hold, precious or plain:\n" + OfferingGifts.DevPrecious(Find.CurrentMap));

        [DebugAction("Rimfolk", "Gods: every gift taken answered in kind (on, off)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void NextReturned()
        {
            GodTouchReturn.Forced = !GodTouchReturn.Forced;
            Log.Message("[Rimfolk] " + (GodTouchReturn.Forced ? "Every gift taken is answered in kind until this is called again." : "Gifts are answered in kind by their chance again."));
        }

        [DebugAction("Rimfolk", "Gods: the keeper reads the gods now (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ReadNow() => Log.Message("[Rimfolk] The keeper's reading of the gods:\n" + GodTemper.DevReadAll());
    }
}
