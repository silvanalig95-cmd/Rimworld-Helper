using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Camps come on their own time, not only by luck among Rimfolk's many events (where one stood among some two hundred
    /// and fifty and a colony could go years without seeing one): once a day, in the daytime on the busiest home on the
    /// ground, while a camp could come (CampTracker.CanStartOn: none here, none gone in the last fortnight or so, never in
    /// the first twenty days, no really extreme weather, room on the map, a friendly people) and no other question is
    /// waiting, there's a <see cref="DailyChance"/> one makes camp. So about one in three weeks, by day, unannounced.
    /// <para>
    /// The day's one choice is made in the first hour from <see cref="FirstHour"/> to <see cref="LastHour"/> that the way
    /// is open: a question that came in the last hours, a world event's question still unanswered, danger on the map, a
    /// spell of extreme weather or a hazard only put it off to a later hour that day (until 0.56.2 it was ten in the
    /// morning or not at all, and Rimfolk's other questions shut most mornings). What happened each day goes into the
    /// camps' diary (CampTracker.diary), shown on the World page and kept in the save.
    /// </para>
    /// </summary>
    public class CampTimer : GameComponent
    {
        /// <summary>Each morning a camp could come: about three weeks' wait on average.</summary>
        public const float DailyChance = 0.05f;

        /// <summary>The hours of the day a camp may arrive: the day's choice is made in the first of them the way is open.</summary>
        public const int FirstHour = 8, LastHour = 17;

        /// <summary>What only puts the day's choice off to a later hour: it may pass by the afternoon.</summary>
        private static readonly HashSet<string> Passing = new HashSet<string> { "threat", "weather", "hazard", "waiting", "lettergap" };

        public CampTimer(Game game)
        {
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (!Sched.Hourly(now, Sched.Camp)) return;
            try
            {
                var map = HappeningTracker.HomeMap();
                var tracker = CampTracker.Instance;
                if (map == null || tracker == null) return;
                // Help in a fight from the colony's friends (WarCamp.cs), and the remembered camps' fading (CampMemory.cs).
                WarCamps.Hourly(map, tracker);
                if (GenLocalDate.HourOfDay(map) == 3) CampReturns.Daily();
                int hour = GenLocalDate.HourOfDay(map);
                if (hour < FirstHour || hour > LastHour) return;
                int day = GenDate.DaysPassed;
                if (tracker.rolledDay == day) return;
                var (code, detail, _) = tracker.Blocker(map);
                if (code == null && HappeningTracker.Waiting) code = "waiting";
                if (code == null && !LetterGap.Open) code = "lettergap";
                if (code != null)
                {
                    // Only the last hour of the day writes a passing reason down; anything else settles the day at once.
                    if (Passing.Contains(code) && hour < LastHour) return;
                    tracker.rolledDay = day;
                    tracker.Record(day, hour, code, detail);
                    return;
                }
                tracker.rolledDay = day;
                tracker.openMornings++;
                if (!Rand.Chance(DailyChance))
                {
                    tracker.Record(day, hour, "open", null);
                    return;
                }
                var def = DefDatabase<IncidentDef>.GetNamedSilentFail("Council_Evt_Camp");
                if (def?.Worker == null)
                {
                    tracker.Record(day, hour, "failed", "the camp event is missing");
                    return;
                }
                var parms = StorytellerUtility.DefaultParmsNow(def.category ?? IncidentCategoryDefOf.Misc, map);
                parms.forced = true;
                if (!def.Worker.CanFireNow(parms))
                {
                    tracker.Record(day, hour, "checks", null);
                    return;
                }
                tracker.lastWhy.Clear();
                bool came = def.Worker.TryExecute(parms);
                tracker.Record(day, hour, came ? "came" : "failed", came ? null : string.Join("; ", tracker.lastWhy));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a camp on its own time", e);
            }
        }
    }
}
