using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony the council's pages show: the map on screen when it's one of the homes, else the seat
    /// (<see cref="ColonyState.InView"/>), worked out once a frame. With more than one colony, the Council section's pages
    /// name it above them, with a button that goes to another colony (the map in view changes, and the pages with it).
    /// </summary>
    public static class ColonyView
    {
        private static int frame = -1;
        private static ColonyState cached;

        /// <summary>The council of the colony in view; null without a game.</summary>
        public static ColonyState Current
        {
            get
            {
                if (frame != Time.frameCount || cached == null)
                {
                    frame = Time.frameCount;
                    cached = ColonyState.InView;
                }
                return cached;
            }
        }

        /// <summary>
        /// With more than one colony, a line naming whose council the page shows, and a button to another colony. Returns
        /// the height used (0 with one colony, where nothing changes on the page).
        /// </summary>
        public static float Header(Rect rect)
        {
            if (!ColonyState.Several) return 0f;
            var map = Current?.Map;
            if (map == null) return 0f;
            const float h = 26f;
            const float buttonW = 170f;
            var row = new Rect(rect.x, rect.y, rect.width, h);
            bool seat = map == Homes.Seat;
            string text = "CC_Colony_CouncilOf".Translate(Homes.Name(map).Named("PLACE")).Resolve();
            if (seat) text += " " + "CC_Colony_TheSeat".Translate().Resolve();
            // Looking at a map that isn't one of the homes: the seat's council, and the page says so.
            if (!Homes.IsHome(Find.CurrentMap)) text += " " + "CC_Colony_NotInView".Translate().Resolve();
            UiKit.Icon(new Rect(row.x + 2f, row.y + 4f, 18f, 18f), Icons.Council, UiKit.Gold);
            UiKit.LabelFit(new Rect(row.x + 26f, row.y, Mathf.Max(0f, row.width - buttonW - 34f), h), text, UiKit.HeaderColor);
            var button = new Rect(row.xMax - buttonW, row.y + 1f, buttonW, h - 2f);
            if (Widgets.ButtonText(button, "CC_Colony_Switch".Translate()))
            {
                var options = new List<FloatMenuOption>();
                foreach (var other in Homes.All)
                {
                    if (other == map) continue;
                    var m = other;
                    options.Add(new FloatMenuOption(Homes.Name(m), () => Verse.Current.Game.CurrentMap = m));
                }
                if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
            }
            TooltipHandler.TipRegion(button, "CC_Colony_SwitchTip".Translate());
            return h;
        }
    }
}
