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
        public const string Moods = "moods";
        public const string Trend = "trend";
        public const string Vitals = "vitals";
        public const string Council = "council";

        /// <summary>Every panel, in the order of the menus.</summary>
        public static readonly string[] Ids = { Moods, Trend, Vitals, Council };

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
        /// A panel as Rimfolk ships it. The mood board on the left a little below the top (clear of the resource list),
        /// the trend graph low on the left above the inspect pane, the vitals and the council on the right above the
        /// alerts. Places are the anchor corner as fractions of the screen.
        /// </summary>
        public static HudPanelState Default(string id)
        {
            var st = new HudPanelState { id = id };
            switch (id)
            {
                case Moods:
                    st.x = 0f;
                    st.y = 0.27f;
                    st.maxRows = 10;
                    break;
                case Trend:
                    st.x = 0f;
                    st.y = 0.78f;
                    st.bottom = true;
                    st.days = 3;
                    break;
                case Vitals:
                    st.x = 1f;
                    st.y = 0.25f;
                    st.right = true;
                    break;
                case Council:
                    st.x = 1f;
                    st.y = 0.43f;
                    st.right = true;
                    st.maxRows = 6;
                    break;
            }
            return st;
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
        public static string Label(string id) => ("CC_Pnl_Name_" + id).Translate();
    }
}
