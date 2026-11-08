using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: which things have the wrong style (Debug actions, "Rimfolk", "Wrong-style things: ..."). The count around a
    /// colonist with the game's own sum from the same cell beside it, to see the two agree.
    /// </summary>
    public static class StyleSurroundingsDebug
    {
        [DebugAction("Rimfolk", "Wrong-style things: the count around a colonist (log)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCount()
        {
            var people = Find.CurrentMap?.mapPawns.FreeColonistsSpawned.Concat(Find.CurrentMap.mapPawns.SlavesOfColonySpawned).Where(p => p.Ideo != null).ToList();
            var list = (people ?? new System.Collections.Generic.List<Pawn>()).Select(p => new FloatMenuOption(p.LabelShort + " (" + p.Ideo.name + ")", () =>
            {
                Log.Message("[Rimfolk] Wrong-style things:\n" + StyleSurroundings.Explain(p));
                Messages.Message("Wrong-style things: " + p.LabelShort + "'s count is in the log.", MessageTypeDefOf.SilentInput, false);
            })).ToList();
            if (list.Count == 0) list.Add(new FloatMenuOption("(no colonist with an ideoligion here)", null));
            Find.WindowStack.Add(new FloatMenu(list));
        }
    }
}
