using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the ground that stays and the mountain that wakes (Debug actions, "Rimfolk", "Ground: ..."): a mark of
    /// each kind now, a quake noted as if one had shaken the map, the mountain's next stage, and what the ground remembers.
    /// </summary>
    public static class GroundDebug
    {
        private static void Say(string text) => Messages.Message("Ground: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Ground: leave a mark now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMark()
        {
            var map = Find.CurrentMap;
            if (map == null) return;
            var kinds = new[] { GroundMarkKind.Cave, GroundMarkKind.Spring, GroundMarkKind.Fissure, GroundMarkKind.Scree, GroundMarkKind.Landslide, GroundMarkKind.Sinkhole };
            Find.WindowStack.Add(new FloatMenu(kinds.Select(k => new FloatMenuOption(k.ToString(), () => Say(GroundMarks.DevMark(map, k)))).ToList()));
        }

        [DebugAction("Rimfolk", "Ground: a quake noted now (its mark rolled at the next hour)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugQuake()
        {
            var map = Find.CurrentMap;
            if (map == null) return;
            var sources = new[] { QuakeSource.Quake, QuakeSource.Rockslide, QuakeSource.WarmVent };
            Find.WindowStack.Add(new FloatMenu(sources.Select(s => new FloatMenuOption(s.ToString(), () => Say(GroundMarks.DevQuake(map, s)))).ToList()));
        }

        [DebugAction("Rimfolk", "Ground: the mountain's next stage now (wakes it when quiet)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWake()
        {
            var map = Find.CurrentMap;
            var g = GroundMap.Of(map);
            if (g != null) Say(GroundWaking.DevNext(g, map));
        }

        [DebugAction("Rimfolk", "Ground: what the ground remembers here", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugReport()
        {
            var map = Find.CurrentMap;
            var g = GroundMap.Of(map);
            if (g == null) return;
            int now = Find.TickManager.TicksGame;
            string Days(int tick) => tick < 0 ? "never" : GroundMath.DaysSince(tick, now).ToString("0.0") + " days ago";
            Say("the land wakes as " + GroundWaking.LandOf(map) + "; the mountain " + g.stage + (g.stageUntil >= 0 ? " for " + ((g.stageUntil - now) / (float)GenDate.TicksPerHour).ToString("0") + " more hours" : "")
                + ", last woke " + Days(g.lastWake) + "; last mark " + Days(g.lastMark) + "; a quake waiting: " + (g.quakeDue >= 0 ? g.quakeSource.ToString() : "none")
                + "; a sinkhole's sign: " + (g.sinkDue >= 0 ? g.sinkAt.ToString() : "none") + "; ash lying on " + g.ash.Count + " cells"
                + (g.ashDue >= 0 ? ", turning in " + ((g.ashDue - now) / (float)GenDate.TicksPerHour).ToString("0") + " hours" : "") + ".");
        }
    }
}
