using System;
using System.Collections.Generic;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The panels' saved layout (CouncilSettings.hudPanels): each panel's state found or made with its defaults, the
    /// default places (calm, out of the way of the game's own readouts at 1080p), putting places or everything back,
    /// and writing the settings file when something changes.
    /// </summary>
    public static class HudLayout
    {
        public const string Moods = HudIds.Moods;
        public const string Trend = HudIds.Trend;
        public const string Vitals = HudIds.Vitals;
        public const string Council = HudIds.Council;
        public const string Alerts = HudIds.Alerts;
        public const string Health = HudIds.Health;
        public const string Work = HudIds.Work;

        /// <summary>Every panel, in the order of the menus.</summary>
        public static readonly string[] Ids = HudIds.All;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The panel's state, made with its defaults the first time.</summary>
        public static HudPanelState State(string id)
        {
            var list = S.hudPanels ??= new List<HudPanelState>();
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].id == id)
                    return list[i];
            var st = Default(id);
            list.Add(st);
            return st;
        }

        /// <summary>
        /// A panel as Rimfolk ships it, its place laid out for the screen now (<see cref="HudMath.DefaultPlace"/>): the
        /// mood board and the trend on the left, the vitals, the council and the alerts (in the game's alert place,
        /// following the letters) on the right edge, work and health in a second column beside them. Calm: the alerts and
        /// health panels keep few rows, the council six.
        /// </summary>
        public static HudPanelState Default(string id)
        {
            var st = new HudPanelState { id = id };
            float sw = 1920f, sh = 1080f;
            try
            {
                if (UI.screenWidth > 0 && UI.screenHeight > 0)
                {
                    sw = UI.screenWidth;
                    sh = UI.screenHeight;
                }
            }
            catch (Exception)
            {
            }
            HudMath.DefaultPlace(id, sw, sh, out st.x, out st.y, out st.right, out st.bottom, out _);
            switch (id)
            {
                case Moods:
                    st.maxRows = 10;
                    // What would help shows on hover and on the hover card; beside every row it makes the board much wider.
                    st.hidden.Add(HudMoodBoard.PartAct);
                    break;
                case Trend:
                    st.days = 3;
                    break;
                case Council:
                    st.maxRows = 6;
                    break;
                case Alerts:
                    st.maxRows = 8;
                    break;
                case Health:
                    st.maxRows = 4;
                    break;
                case Work:
                    st.maxRows = 10;
                    break;
            }
            return st;
        }

        private static bool migrated;

        /// <summary>
        /// Once a session: the vitals and the council where 0.9.0 put them (on the right, a quarter and 43% down) move to
        /// their new places above the alerts panel, which now takes the game's alert place. Panels the player moved stay.
        /// </summary>
        public static void MigrateOldPlaces()
        {
            if (migrated) return;
            migrated = true;
            bool changed = false;
            foreach (var (id, oldY) in new[] { (Vitals, 0.25f), (Council, 0.43f) })
            {
                var list = S.hudPanels;
                var st = list?.Find(p => p != null && p.id == id);
                if (st == null || !st.right || st.bottom || Math.Abs(st.x - 1f) > 0.0001f || Math.Abs(st.y - oldY) > 0.0001f) continue;
                var d = Default(id);
                st.x = d.x;
                st.y = d.y;
                changed = true;
            }
            if (changed) Save();
        }

        /// <summary>Every panel back to its default place, keeping its options.</summary>
        public static void ResetPositions()
        {
            foreach (var id in Ids)
            {
                var st = State(id);
                var d = Default(id);
                st.x = d.x;
                st.y = d.y;
                st.right = d.right;
                st.bottom = d.bottom;
            }
            Save();
        }

        /// <summary>Every panel and the hover card as Rimfolk ships them: places, options and the switches above them.</summary>
        public static void ResetAll()
        {
            var s = S;
            var d = new CouncilSettings();
            s.hudShown = d.hudShown;
            s.hudLock = d.hudLock;
            s.hudHideWithTabs = d.hudHideWithTabs;
            s.hudHideOnWorld = d.hudHideOnWorld;
            s.hudTheme = d.hudTheme;
            s.hudFont = d.hudFont;
            s.hudMoodMargin = d.hudMoodMargin;
            s.hudFoodWarnDays = d.hudFoodWarnDays;
            s.hudFoodBadDays = d.hudFoodBadDays;
            s.hoverCard = d.hoverCard;
            s.hoverDelay = d.hoverDelay;
            s.hoverModifier = d.hoverModifier;
            s.hoverWho = d.hoverWho;
            s.hoverPlace = d.hoverPlace;
            s.hoverMood = d.hoverMood;
            s.hoverConcerns = d.hoverConcerns;
            s.hoverDoing = d.hoverDoing;
            s.hoverLines = d.hoverLines;
            s.hoverScale = d.hoverScale;
            s.hoverOpacity = d.hoverOpacity;
            s.hoverHideGameTip = d.hoverHideGameTip;
            s.hudHideGameAlerts = d.hudHideGameAlerts;
            s.hudPanels = new List<HudPanelState>();
            foreach (var id in Ids) s.hudPanels.Add(Default(id));
            HudManager.editing = false;
            Save();
        }

        /// <summary>Writes the settings file now (a panel moved or changed).</summary>
        public static void Save()
        {
            try
            {
                S.Write();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("saving the on-screen panels", e);
            }
        }

        /// <summary>The panel's name, for menus and its header.</summary>
        public static string Label(string id) => HudKit.T("CC_Pnl_Name_" + id);
    }
}
