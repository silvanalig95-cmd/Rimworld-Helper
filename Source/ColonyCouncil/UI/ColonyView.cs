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
                    var now = ColonyState.InView;
                    if (now != cached) version++;
                    cached = now;
                }
                return cached;
            }
        }

        // ---------------- The pages that follow the colony in view (3c) ----------------

        private static int version;
        private static int followsFrame = -1;
        private static bool everyone;

        /// <summary>
        /// Bumped when the colony in view changes or "everyone" is turned on or off: a page that follows the colony in view
        /// (ManyColoniesMath.FollowingPages) builds again at once when it moves.
        /// </summary>
        public static int Version
        {
            get
            {
                _ = Current;
                return version;
            }
        }

        /// <summary>Everyone from every colony on the pages that follow the colony in view (a click away on their header), for the session.</summary>
        public static bool Everyone
        {
            get => everyone;
            set
            {
                if (everyone == value) return;
                everyone = value;
                version++;
            }
        }

        /// <summary>The page drawn this frame follows the colony in view: its header shows the chip for everyone (with more than one colony).</summary>
        public static void Following(bool follows)
        {
            if (follows) followsFrame = Time.frameCount;
        }

        /// <summary>The colony in view's home key, or none (the seat's council for a map that isn't a home).</summary>
        private static int InViewKey => Homes.KeyOf(Current?.Map);

        /// <summary>
        /// Someone shows on a page that follows the colony in view: always with one colony or with everyone chosen; else only
        /// the colony in view's own people (ManyColoniesMath.Shows).
        /// </summary>
        public static bool Shows(Pawn p)
        {
            var homes = Homes.All;
            if (homes.Count <= 1 || everyone || p == null) return true;
            return ManyColoniesMath.Shows(Homes.KeyOf(Homes.Of(p)), InViewKey, everyone, homes.Count);
        }

        /// <summary>A colony's home (its key) shows on a page that follows the colony in view: none (about no home) always does.</summary>
        public static bool ShowsHome(int key) => key == HomesMath.None || ManyColoniesMath.Shows(key, InViewKey, everyone, Homes.All.Count);

        /// <summary>A line of the chronicle shows for the colony in view (ManyColoniesMath.ChronicleShows).</summary>
        public static bool ShowsLine(Ref place, Pawn about)
        {
            var homes = Homes.All;
            if (homes.Count <= 1 || everyone) return true;
            int placeKey = place.kind == RefKind.Colony ? place.id : HomesMath.None;
            int aboutKey = about != null ? Homes.KeyOf(Homes.Of(about)) : HomesMath.None;
            return ManyColoniesMath.ChronicleShows(placeKey, aboutKey, InViewKey, everyone, homes.Count);
        }

        /// <summary>
        /// On a page that follows the colony in view, with more than one colony: a chip to see everyone from every colony, or
        /// only the colony in view's, right-aligned to end at <paramref name="right"/>. Returns the width it took (0 otherwise).
        /// </summary>
        public static float ScopeChip(float right, float y, float maxWidth)
        {
            if (followsFrame != Time.frameCount || !ColonyState.Several) return 0f;
            string label = (everyone ? "CC_Many_ScopeEveryone" : "CC_Many_ScopeColony").Translate().Resolve();
            Text.Font = GameFont.Tiny;
            float w = Mathf.Min(maxWidth, Text.CalcSize(label).x + 14f);
            Text.Font = GameFont.Small;
            if (w < 40f) return 0f;
            var r = new Rect(right - w, y, w, 20f);
            Widgets.DrawBoxSolid(r, UiKit.WithAlpha(everyone ? UiKit.Gold : UiKit.Ink, 0.08f));
            UiKit.RowHover(r);
            UiKit.LabelFit(new Rect(r.x + 6f, r.y, r.width - 8f, r.height), label, everyone ? UiKit.Gold : UiKit.Faint, GameFont.Tiny);
            TooltipHandler.TipRegion(r, (everyone ? "CC_Many_ScopeEveryoneTip" : "CC_Many_ScopeColonyTip").Translate(Homes.Name(Current?.Map).Named("PLACE")).Resolve());
            if (Widgets.ButtonInvisible(r)) Everyone = !everyone;
            return w;
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
            UiKit.RowHover(r);
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
            if (UiKit.Button(button, "CC_Colony_Switch".Translate()))
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
