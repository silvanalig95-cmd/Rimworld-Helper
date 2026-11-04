using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Camps come on their own time, not only by luck among Rimfolk's many events (where one stood among some two hundred
    /// and fifty and a colony could go years without seeing one): once a day at each colony on the ground, on its own
    /// clock (CampHome: its own day to try, its own gap since its last camp, its own diary), while a camp could come (CampTracker.CanStartOn: none here, none gone in the last fortnight or so, never in
    /// the first twenty days, no really extreme weather, room on the map, a friendly people) and no other question is
    /// waiting, there's a <see cref="DailyChance"/> one makes camp. So about one in three weeks, by day, unannounced.
    /// <para>
    /// The day's one try comes at any hour alike, and which kind of camp comes follows the hour (CampKindDef.arrives:
    /// hunters and fishers at dawn, traders by day, minstrels toward evening, deserters by night, many at any hour). A
    /// question that came in the last hours, a world event's question still unanswered, danger on the map, a spell of
    /// extreme weather or a
    /// hazard only put it off by the hour, for up to a day (until 0.56.2 it was ten in the morning or not at all, and
    /// Rimfolk's other questions shut most mornings). What happened each day goes into the camps' diary
    /// (CampTracker.diary), shown on the World page and kept in the save.
    /// </para>
    /// </summary>
    public class CampTimer : GameComponent
    {
        /// <summary>Each morning a camp could come: about three weeks' wait on average.</summary>
        public const float DailyChance = 0.05f;

        /// <summary>How often a passing reason may put the day's try off by an hour before the day counts as shut.</summary>
        public const int Retries = 23;

        /// <summary>
        /// The tick of the next try: at any hour alike, later today (<paramref name="first"/>) or some twelve to thirty-five
        /// hours on, so about one try a day. Which kind of camp comes then follows the hour (CampKindDef.arrives).
        /// </summary>
        public static int NextTry(int now, int hourNow, bool first)
        {
            int pick = Rand.RangeInclusive(0, 23);
            int delta = (pick - hourNow + 24) % 24;
            if (!first && delta < 12) delta += 24;
            return now + delta * GenDate.TicksPerHour;
        }

        /// <summary>What only puts the day's choice off to a later hour: it may pass by the afternoon.</summary>
        private static readonly HashSet<string> Passing = new HashSet<string> { "threat", "weather", "hazard", "waiting", "lettergap" };

        private static void Next(CampHome home, int now, int hour)
        {
            home.tryHours = 0;
            home.nextTryTick = NextTry(now, hour, false);
        }

        public CampTimer(Game game)
        {
        }

        /// <summary>One home's hour: help in a fight, and its day's try for a camp.</summary>
        private static void HourAt(Map map, CampTracker tracker, bool seat, int now)
        {
            var home = tracker.HomeOf(map);
            // Help in a fight from the colony's friends (WarCamp.cs), and the remembered camps' fading (CampMemory.cs, once a day, by the seat's hour).
            WarCamps.Hourly(map, tracker);
            if (seat && GenLocalDate.HourOfDay(map) == 3) CampReturns.Daily();
            int hour = GenLocalDate.HourOfDay(map);
            // A try further off than a few days is another game's (one loaded since): pick again.
            if (CampStoryMath.RepickTry(home.nextTryTick, now)) home.nextTryTick = NextTry(now, hour, true);
            if (now < home.nextTryTick) return;
            int day = GenDate.DaysPassed;
            var (code, detail, _) = tracker.Blocker(map);
            // A world event's question about this colony still unanswered.
            if (code == null && HappeningTracker.WaitingAt(map)) code = "waiting";
            if (code == null && !Tell.CanAsk) code = "lettergap";
            if (code != null)
            {
                // A passing reason puts the try off by the hour, for up to a day; anything else settles the day at once.
                if (CampStoryMath.PutOff(Passing.Contains(code), home.tryHours, Retries))
                {
                    home.tryHours++;
                    return;
                }
                home.Record(day, hour, code, detail);
                Next(home, now, hour);
                return;
            }
            Next(home, now, hour);
            home.openMornings++;
            if (!Rand.Chance(DailyChance))
            {
                home.Record(day, hour, "open", null);
                return;
            }
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail("Council_Evt_Camp");
            if (def?.Worker == null)
            {
                home.Record(day, hour, "failed", "the camp event is missing");
                return;
            }
            var parms = StorytellerUtility.DefaultParmsNow(def.category ?? IncidentCategoryDefOf.Misc, map);
            parms.forced = true;
            if (!def.Worker.CanFireNow(parms))
            {
                home.Record(day, hour, "checks", null);
                return;
            }
            tracker.lastWhy.Clear();
            bool came = def.Worker.TryExecute(parms);
            home.Record(day, hour, came ? "came" : "failed", came ? null : string.Join("; ", tracker.lastWhy));
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (!Sched.Hourly(now, Sched.Camp)) return;
            using var clock = PassClock.Time(Pass.Camp);
            try
            {
                var tracker = CampTracker.Instance;
                if (tracker == null) return;
                var seat = HappeningTracker.HomeMap();
                // Each home on the ground, in the order they were founded: camps of its own, on its own day. Nobody camps by
                // a home in space.
                foreach (var map in Homes.All)
                {
                    if (map == null || MapFacts.InVacuum(map)) continue;
                    try
                    {
                        HourAt(map, tracker, map == seat, now);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("a camp on its own time", e);
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a camp on its own time", e);
            }
        }
    }
}
