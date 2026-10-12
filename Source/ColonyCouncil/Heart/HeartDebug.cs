using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Dev mode (Debug actions, "Rimfolk"): push a colonist's strain around and try the day off, to test the heart.</summary>
    public static class HeartDebug
    {
        [DebugAction("Rimfolk", "Heart: add 20 strain", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AddStrain(Pawn p) => Change(p, 20f);

        [DebugAction("Rimfolk", "Heart: take off 20 strain", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LessStrain(Pawn p) => Change(p, -20f);

        [DebugAction("Rimfolk", "Heart: look at them now", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void EvaluateNow(Pawn p)
        {
            var h = Heart.On && Heart.Eligible(p) ? HeartTracker.Instance?.For(p, true) : null;
            if (h == null)
            {
                Messages.Message("No heart: feelings and strain are off, or this isn't one of the colony's people.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            int now = Find.TickManager.TicksGame;
            HeartLogic.Evaluate(p, h, HeartMapContext.For(p.MapHeld, now), now);
            Messages.Message(p.LabelShort + ": " + h.inspectLine, p, MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction("Rimfolk", "Heart: day off now (if allowed)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void EpisodeNow(Pawn p)
        {
            var h = Heart.On && Heart.Eligible(p) ? HeartTracker.Instance?.For(p, true) : null;
            if (h == null) return;
            if (h.strain < HeartMath.EpisodeAt) h.strain = HeartMath.EpisodeAt;
            int now = Find.TickManager.TicksGame;
            string blocked = Episodes.Blocked(p, h, HeartMapContext.For(p.MapHeld, now), now);
            if (blocked != null)
            {
                Messages.Message("No day off for " + p.LabelShort + ": " + blocked + ".", p, MessageTypeDefOf.RejectInput, false);
                return;
            }
            Episodes.Start(p, h, now);
        }

        private static void Change(Pawn p, float amount)
        {
            if (!Heart.On || !Heart.Eligible(p)) return;
            HeartLogic.Change(p, amount, StrainRelief.Council, StrainSource.Council);
            Messages.Message(p.LabelShort + ": strain " + Heart.StrainOf(p).ToString("0"), p, MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
