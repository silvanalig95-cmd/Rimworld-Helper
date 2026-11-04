using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The calendars' book, in a game component of its own: one calendar per colony (<see cref="ColonyCalendar"/>), each
    /// with its own founding day, days, traditions and date, kept and saved here (a book of colonies, as the council's:
    /// <see cref="ColonyBook"/>). Once an hour each home's calendar looks at its own date and keeps its days at its own
    /// home; once a day a lost home's calendar is let go after a while (with no home left, its people carry it to the
    /// next). A save from before there was one calendar per colony gives the calendar it had to the seat, untouched. With
    /// one colony there is one calendar, and it plays as before.
    /// </summary>
    public class CalendarTracker : GameComponent
    {
        /// <summary>One calendar per colony; an old save's single one is the seat's.</summary>
        public List<ColonyCalendar> colonies = new List<ColonyCalendar>();

        /// <summary>While an old save loads: its one calendar, read from where it always was, for the seat.</summary>
        [Unsaved] private ColonyCalendar oldSave;

        private static CalendarTracker cached;
        private static Game cachedGame;

        /// <summary>The book. A colony's calendar: <see cref="ColonyCalendar.For"/>, <see cref="ColonyCalendar.Of"/>, <see cref="ColonyCalendar.Seat"/>.</summary>
        public static CalendarTracker Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<CalendarTracker>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public CalendarTracker(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
        }

        /// <summary>
        /// A home's calendar, made when it first needs one (the seat takes an old save's); for a map that isn't a home, the
        /// seat's; with no home at all, the one its people carry. The first calendar of a game fills itself in from what
        /// the colony already lived through (an older save); a colony founded later starts with its own founding day.
        /// </summary>
        public ColonyCalendar Colony(Map map)
        {
            colonies ??= new List<ColonyCalendar>();
            return ColonyBook.Entry(colonies, map, () => ColonyCalendar.Made(colonies.Count == 0));
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (!Sched.Hourly(now, Sched.Calendar)) return;
            using var clock = PassClock.Time(Pass.Calendar);
            try
            {
                // Each home's own calendar, in the order the homes were founded (one colony: the one calendar, as before).
                foreach (var cal in ColonyCalendar.All())
                {
                    try
                    {
                        cal.Hourly();
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("calendar", e);
                    }
                }
                if (now % GenDate.TicksPerDay < GenDate.TicksPerHour) ColonyBook.Daily(colonies, null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("calendar", e);
            }
        }

        /// <summary>Homes lost (Homes.Changed): a lost home's calendar is kept a while, or carried when no home is left.</summary>
        internal void HomesLost(IReadOnlyList<int> lost) => ColonyBook.Lost(colonies, lost, null);

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving) colonies?.RemoveAll(c => c == null);
            Scribe_Collections.Look(ref colonies, "colonies", LookMode.Deep);
            // A save from before there was one calendar per colony: what it had, where it always was, is the seat's.
            if (Scribe.mode == LoadSaveMode.LoadingVars && colonies == null)
            {
                oldSave = new ColonyCalendar();
                colonies = new List<ColonyCalendar> { oldSave };
            }
            if (oldSave != null && Scribe.mode != LoadSaveMode.Saving) oldSave.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                oldSave = null;
                colonies ??= new List<ColonyCalendar>();
                colonies.RemoveAll(c => c == null);
            }
        }
    }
}
