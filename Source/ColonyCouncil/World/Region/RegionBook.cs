using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One home's part of the region that is saved (docs/design/stage1-region.md, "Shared rules"), kept by the council's
    /// rules for a colony's books (ColonyBook): with one colony there is one, and a second colony has its own. Its towns
    /// in detail and its remoteness are worked out, not saved (RegionTowns).
    /// </summary>
    public class RegionHome : IExposable, IColonyEntry
    {
        public int home = HomesMath.None;
        public int lostAt = ColonyStateMath.NotLost;

        public int Home
        {
            get => home;
            set => home = value;
        }

        public int LostAt
        {
            get => lostAt;
            set => lostAt = value;
        }

        /// <summary>When a new town's founder last wrote to this home (one letter a season at most), or -1.</summary>
        public int letterAt = -1;
        /// <summary>When a new town last gave this home its full welcome (once a year at most), or -1.</summary>
        public int welcomeAt = -1;
        /// <summary>Its raiders' camp, standing or last ended, and the word of the last people paid off (World/Region/RaidersCamp.cs); null until needed.</summary>
        public RaidersCampHome raiders;

        public void ExposeData()
        {
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref lostAt, "lostAt", ColonyStateMath.NotLost);
            Scribe_Values.Look(ref letterAt, "letterAt", -1);
            Scribe_Values.Look(ref welcomeAt, "welcomeAt", -1);
            Scribe_Deep.Look(ref raiders, "raiders");
        }
    }

    /// <summary>
    /// The region comes alive (docs/design/stage1-region.md; DESIGN.md, "The region"): the towns within the region map's
    /// reach of a home kept in detail on the world ledger, with a fortune the world's events move, and the new towns
    /// given faces (a founder, founding families, a reason, their news, talk and letters). Two passes, each timed by
    /// Rimfolk's clock: the hour (<see cref="Pass.Region"/>: founders to make, towns begun and built to be told) and the
    /// day (<see cref="Pass.DailyRegion"/>: the reach, remoteness, the fortunes). Nothing per tick, nothing beyond the
    /// reach. Off, a part makes nothing new and keeps its records.
    /// </summary>
    public class RegionBook : GameComponent
    {
        /// <summary>One per colony (ColonyBook).</summary>
        private List<RegionHome> homes = new List<RegionHome>();

        /// <summary>Founders made today (two a day at most); not saved: a load lets a little more through.</summary>
        [Unsaved] private int madeDay = -1;
        [Unsaved] private int madeToday;

        private static RegionBook cached;
        private static Game cachedGame;

        public RegionBook(Game game)
        {
        }

        public static RegionBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<RegionBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        /// <summary>The towns' fortunes (the setting): prosperity seeded, moved and shown.</summary>
        public static bool FortunesOn => ColonyCouncilMod.Settings?.regionFortunes ?? true;

        /// <summary>The new towns' faces (the setting): founders, families, news, talk and letters.</summary>
        public static bool FacesOn => ColonyCouncilMod.Settings?.regionFaces ?? true;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            RegionTowns.Invalidate();
        }

        /// <summary>A home's part (the seat's for a map that isn't a home).</summary>
        public RegionHome HomeOf(Map map)
        {
            homes ??= new List<RegionHome>();
            return ColonyBook.Entry(homes, map, () => new RegionHome());
        }

        /// <summary>Homes lost (Homes.Changed): each one's part kept a while, as the council's.</summary>
        internal void HomesLost(IReadOnlyList<int> lost)
        {
            ColonyBook.Lost(homes, lost, RaidersCamps.HomeLost);
            RegionTowns.Invalidate();
        }

        // ---------------- Founders made, two a day ----------------

        /// <summary>A founder may be made now (two a day at most). Counts it when <paramref name="take"/>.</summary>
        public bool MayMakeFounder(bool take)
        {
            int day = Now / GenDate.TicksPerDay;
            if (day != madeDay)
            {
                madeDay = day;
                madeToday = 0;
            }
            if (madeToday >= RegionMath.FoundersADay) return false;
            if (take) madeToday++;
            return true;
        }

        // ---------------- The passes ----------------

        public override void GameComponentTick()
        {
            var tm = Find.TickManager;
            if (tm == null) return;
            int now = tm.TicksGame;
            if (Sched.Hourly(now, Sched.Region))
                using (PassClock.Time(Pass.Region))
                    Safe("the hour", () => Hourly(now));
            if (Sched.Every(now, Sched.DayTicks, Sched.DailyRegion))
                using (PassClock.Time(Pass.DailyRegion))
                    Safe("the day", () => Daily(now));
        }

        private static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region: " + what, e);
            }
        }

        /// <summary>The hour: towns being founded get their founder and their line in the news; towns just built get their faces.</summary>
        private void Hourly(int now)
        {
            if (FacesOn) RegionFaces.Hourly(this, now);
            RaidersCamps.Hourly(this, now);
        }

        /// <summary>The day: the reach worked out again (with remoteness), the towns in detail seeded and their fortunes moved.</summary>
        private void Daily(int now)
        {
            RegionTowns.Refresh(true);
            Safe("foundings looked over", () => RegionFaces.Daily(now));
            if (FortunesOn) Safe("fortunes", () => RegionFortunes.Daily(now));
            Safe("raiders' camps", () => RaidersCamps.Daily(this, now));
            ColonyBook.Daily(homes, null);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref homes, "homes", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                homes ??= new List<RegionHome>();
                homes.RemoveAll(h => h == null);
            }
        }
    }
}
