using System;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Camps come on their own time, not only by luck among Rimfolk's many events (where one stood among some two hundred
    /// and fifty and a colony could go years without seeing one): once a day, at ten in the morning on the busiest home
    /// on the ground, while a camp could come (CampTracker.CanStartOn: none here, none gone in the last fortnight or so,
    /// never in the first twenty days, fair weather, room on the map, a friendly people) and no other question is
    /// waiting, there's a <see cref="DailyChance"/> one makes camp. So about one a season, by day, unannounced.
    /// </summary>
    public class CampTimer : GameComponent
    {
        /// <summary>Each morning a camp could come: about three weeks' wait on average.</summary>
        public const float DailyChance = 0.05f;

        /// <summary>The hour of the morning a camp arrives.</summary>
        public const int Hour = 10;

        public CampTimer(Game game)
        {
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (!Sched.Hourly(now, Sched.Camp) || !CampTracker.Enabled) return;
            try
            {
                var map = HappeningTracker.HomeMap();
                var tracker = CampTracker.Instance;
                if (map == null || tracker == null || GenLocalDate.HourOfDay(map) != Hour) return;
                if (HappeningTracker.Waiting || !LetterGap.Open || !tracker.CanStartOn(map)) return;
                if (!Rand.Chance(DailyChance)) return;
                var def = DefDatabase<IncidentDef>.GetNamedSilentFail("Council_Evt_Camp");
                if (def?.Worker == null) return;
                var parms = StorytellerUtility.DefaultParmsNow(def.category ?? IncidentCategoryDefOf.Misc, map);
                parms.forced = true;
                if (def.Worker.CanFireNow(parms)) def.Worker.TryExecute(parms);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a camp on its own time", e);
            }
        }
    }
}
