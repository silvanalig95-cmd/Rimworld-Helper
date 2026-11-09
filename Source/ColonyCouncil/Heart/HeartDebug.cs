using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Dev mode (Debug actions, "Rimfolk"): push a colonist's strain and good side around, and try the day off and the good side's overflowing, to test the heart.</summary>
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

        [DebugAction("Rimfolk", "Heart: fill the good side by 20", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MoreGood(Pawn p)
        {
            var h = GoodHeart.On && Heart.Eligible(p) ? HeartTracker.Instance?.For(p, true) : null;
            if (h == null) return;
            h.strain = 0f;
            h.good = HeartMath.Clamp(h.good + 20f);
            HeartView.Refresh(p, h);
            Messages.Message(p.LabelShort + ": the good side " + h.good.ToString("0") + " (" + GoodHeart.BandLabel(GoodHeart.Band(h)) + ")", p, MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction("Rimfolk", "Heart: the good side overflows now (if allowed)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ReleaseNow(Pawn p)
        {
            var h = GoodHeart.On && Heart.Eligible(p) ? HeartTracker.Instance?.For(p, true) : null;
            if (h == null) return;
            if (h.good < HeartMath.HighSpiritsAt) h.good = HeartMath.HighSpiritsAt;
            h.strain = 0f;
            int now = Find.TickManager.TicksGame;
            string blocked = GoodRelease.Blocked(p, h, HeartMapContext.For(p.MapHeld, now), now);
            if (blocked != null || !GoodRelease.Start(p, h, now))
                Messages.Message("Nothing comes of it for " + p.LabelShort + ": " + (blocked ?? "no one close and no inspiration the game allows") + ".", p, MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction("Rimfolk", "Heart: a heavy loss weighs on them (45)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Weigh(Pawn p)
        {
            var h = Heart.On && Heart.Eligible(p) ? HeartTracker.Instance?.For(p, true) : null;
            if (h == null) return;
            HeartFelt.Weigh(h, WeightKind.Loss, 45f, "CC_Felt_Why_LossSomeone".Translate(), null, Find.TickManager.TicksGame);
            HeartView.Refresh(p, h);
            Messages.Message(p.LabelShort + ": a weight of " + HeartFelt.Floor(h).ToString("0") + "; strain sinks toward it over the next hours.", p,
                MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction("Rimfolk", "Heart: a small act now (if one fits)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ActNow(Pawn p)
        {
            var h = Heart.On && Heart.Eligible(p) ? HeartTracker.Instance?.For(p, true) : null;
            if (h == null) return;
            int now = Find.TickManager.TicksGame;
            if (!HeartActs.Safe(p, HeartMapContext.For(p.MapHeld, now)) || !HeartActs.TryAny(p, h, h.Band, GoodHeart.Band(h), HeartActs.Strong(h)))
                Messages.Message("No act fits " + p.LabelShort + " now (" + HeartView.BandsText(h.Band, GoodHeart.Band(h)) + ").", p, MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction("Rimfolk", "Heart: say how they feel now", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SayNow(Pawn p)
        {
            var h = Heart.On && Heart.Eligible(p) ? HeartTracker.Instance?.For(p, true) : null;
            if (h == null) return;
            string line = HeartFelt.BandLine(p, h, null);
            if (line.NullOrEmpty() || !Speech.Say(p, line))
                Messages.Message("Nothing to say for " + p.LabelShort + " (" + HeartView.BandsText(h.Band, GoodHeart.Band(h)) + ").", p, MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction("Rimfolk", "Heart: a sign over the head now", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SignNow(Pawn p)
        {
            var h = Heart.On && Heart.Eligible(p) ? Heart.ForView(p) : null;
            var def = h?.feelings != null && h.feelings.Count > 0 ? h.feelings[0].def : DefDatabase<FeelingDef>.AllDefsListForReading.RandomElementWithFallback();
            if (def == null || !HeartFelt.ShowSign(p, def))
                Messages.Message("No sign for " + p.LabelShort + ".", p, MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction("Rimfolk", "Heart: a bad afternoon now (if safe)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void BadDayNow(Pawn p)
        {
            var h = Heart.On && Heart.Eligible(p) ? HeartTracker.Instance?.For(p, true) : null;
            if (h == null) return;
            int now = Find.TickManager.TicksGame;
            if (!HeartTimeOff.TryBadDay(p, h, HeartMapContext.For(p.MapHeld, now), now, true))
                Messages.Message("No bad afternoon for " + p.LabelShort + " now (" + (HeartTimeOff.Unsafe(p, HeartMapContext.For(p.MapHeld, now), 2) ?? "time off already, asleep or busy") + ").",
                    p, MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction("Rimfolk", "Heart: a day off now (the wait aside)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DayOffNow(Pawn p)
        {
            var h = Heart.On && Heart.Eligible(p) ? HeartTracker.Instance?.For(p, true) : null;
            if (h == null) return;
            h.lastDayOffTick = -999999;
            if (h.strain < HeartCountMath.DayOffFrom) HeartLogic.Change(p, HeartCountMath.DayOffFrom - h.strain + 1f, StrainRelief.Council, StrainSource.Council);
            if (!HeartTimeOff.GiveDayOff(p))
                Messages.Message("No day off for " + p.LabelShort + ": " + HeartTimeOff.WhyNotDayOff(p, h, Find.TickManager.TicksGame), p, MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction("Rimfolk", "Heart: end their time off now", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void EndOffNow(Pawn p)
        {
            var h = Heart.Of(p);
            if (h != null && h.AnyOff) HeartTimeOff.End(p, h, Find.TickManager.TicksGame, true, null);
        }

        private static void Change(Pawn p, float amount)
        {
            if (!Heart.On || !Heart.Eligible(p)) return;
            HeartLogic.Change(p, amount, StrainRelief.Council, StrainSource.Council);
            Messages.Message(p.LabelShort + ": strain " + Heart.StrainOf(p).ToString("0"), p, MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
