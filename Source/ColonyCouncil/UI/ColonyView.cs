using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony the council's pages show: the map on screen when it's one of the homes, else the seat
    /// (<see cref="ColonyState.InView"/>), worked out once a frame. With more than one colony, every view's header names
    /// it in a chip that goes to another colony (the map in view changes, and the pages with it: <see cref="Chip"/>,
    /// drawn by UiKit.ViewHeader; docs/design/stage3-many-colonies.md, 3c).
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

        /// <summary>The chip's words: the colony in view, and whether it is the seat.</summary>
        public static string ChipLabel()
        {
            var map = Current?.Map;
            if (map == null) return null;
            string text = Homes.Name(map);
            return map == Homes.Seat ? text + " " + "CC_Colony_TheSeat".Translate().Resolve() : text;
        }

        /// <summary>
        /// With more than one colony, a chip naming the colony in view, right-aligned so it ends at <paramref name="right"/>
        /// on the line at <paramref name="y"/>; a click lists the other colonies to go to. Returns the width it took (0 with
        /// one colony, where nothing changes in the header).
        /// </summary>
        public static float Chip(float right, float y, float maxWidth)
        {
            if (!ColonyState.Several) return 0f;
            string label = ChipLabel();
            if (label.NullOrEmpty()) return 0f;
            Text.Font = GameFont.Tiny;
            float w = Mathf.Min(maxWidth, Text.CalcSize(label).x + 30f);
            Text.Font = GameFont.Small;
            if (w < 60f) return 0f;
            var r = new Rect(right - w, y, w, 20f);
            Widgets.DrawBoxSolid(r, UiKit.WithAlpha(UiKit.Gold, 0.12f));
            Widgets.DrawHighlightIfMouseover(r);
            UiKit.Icon(new Rect(r.x + 3f, r.y + 2f, 16f, 16f), Icons.Colony, UiKit.Gold);
            UiKit.LabelFit(new Rect(r.x + 22f, r.y, r.width - 24f, r.height), label, UiKit.Gold, GameFont.Tiny);
            string tip = "CC_Colony_CouncilOf".Translate(Homes.Name(Current.Map).Named("PLACE")).Resolve() + "\n\n" + "CC_Colony_SwitchTip".Translate().Resolve();
            if (!Homes.IsHome(Find.CurrentMap)) tip += "\n\n" + "CC_Colony_NotInView".Translate().Resolve();
            TooltipHandler.TipRegion(r, tip);
            if (Widgets.ButtonInvisible(r)) Others(Current.Map);
            return w;
        }

        /// <summary>The other colonies, to go to one (the map in view changes).</summary>
        private static void Others(Map map)
        {
            var options = new List<FloatMenuOption>();
            foreach (var other in Homes.All)
            {
                if (other == map) continue;
                var m = other;
                options.Add(new FloatMenuOption(Homes.Name(m) + (m == Homes.Seat ? " " + "CC_Colony_TheSeat".Translate().Resolve() : ""), () => Verse.Current.Game.CurrentMap = m));
            }
            if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>
        /// With more than one colony, a line naming whose council the page shows, and a button to another colony. Returns
        /// the height used (0 with one colony, where nothing changes on the page). The chip in every view's header does
        /// this now; kept for a page that draws no view header of its own.
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
