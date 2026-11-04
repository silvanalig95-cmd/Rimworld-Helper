using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Camps come on their own time, not only by luck among Rimfolk's many events (where one stood among some two hundred
    /// and fifty and a colony could go years without seeing one): once a day, in the daytime at the colony (its seat) on the
    /// ground, while a camp could come (CampTracker.CanStartOn: none here, none gone in the last fortnight or so, never in
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

        private static void Next(CampTracker tracker, int now, int hour)
        {
            tracker.tryHours = 0;
            tracker.nextTryTick = NextTry(now, hour, false);
        }

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
                // A try further off than a few days is another game's (one loaded since): pick again.
                if (tracker.nextTryTick < 0 || tracker.nextTryTick > now + 3 * GenDate.TicksPerDay) tracker.nextTryTick = NextTry(now, hour, true);
                if (now < tracker.nextTryTick) return;
                int day = GenDate.DaysPassed;
                var (code, detail, _) = tracker.Blocker(map);
                if (code == null && HappeningTracker.Waiting) code = "waiting";
                if (code == null && !LetterGap.Open) code = "lettergap";
                if (code != null)
                {
                    // A passing reason puts the try off by the hour, for up to a day; anything else settles the day at once.
                    if (Passing.Contains(code) && tracker.tryHours < Retries)
                    {
                        tracker.tryHours++;
                        return;
                    }
                    tracker.Record(day, hour, code, detail);
                    Next(tracker, now, hour);
                    return;
                }
                Next(tracker, now, hour);
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
