using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>One of Rimfolk's windows in its frame: its key in the settings, its least size, and whether it keeps the game's bottom bar clear.</summary>
    public struct FrameKey
    {
        public string key;
        public float leastW, leastH;
        /// <summary>It stands beside play (the four windows, the Almanac, a card) and stops above the bottom bar; a dialog may cover it.</summary>
        public bool keepsBar;

        public FrameKey(string key, float leastW, float leastH, bool keepsBar = false)
        {
            this.key = key;
            this.leastW = leastW;
            this.leastH = leastH;
            this.keepsBar = keepsBar;
        }

        /// <summary>The bottom bar it keeps clear (<see cref="WindowFrameMath.BottomBar"/>) or none.</summary>
        public float Bottom => keepsBar ? WindowFrameMath.BottomBar : 0f;
    }

    /// <summary>
    /// The rules of Rimfolk's framed windows (docs/design/rimfolk-look.md, stage 2), free of game types so Tools/Tests try
    /// them: every window's key and least size (the four windows that float, the Almanac, the card, the trade windows and
    /// every dialog of Rimfolk's own), the floating windows' one-at-a-time rule (opening one of the four closes the other
    /// three, as the corner turns pages), and how the council's pages lay out as the window grows (This week's columns).
    /// </summary>
    public static class FramedWindowsMath
    {
        // ---------------- The four windows that float ----------------

        /// <summary>The four windows' keys: the region, the council, planning and defense.</summary>
        public const string Region = "Region", Council = "Council", Planning = "Planning", Defense = "Defense";

        /// <summary>The four, in the corner's order.</summary>
        public static readonly RimfolkWindow[] Floating = { RimfolkWindow.Council, RimfolkWindow.World, RimfolkWindow.Defense, RimfolkWindow.Planning };

        /// <summary>A floating window's key in the settings (where it was left).</summary>
        public static string KeyOf(RimfolkWindow w)
        {
            switch (w)
            {
                case RimfolkWindow.World: return Region;
                case RimfolkWindow.Planning: return Planning;
                case RimfolkWindow.Defense: return Defense;
                default: return Council;
            }
        }

        /// <summary>
        /// One of the four at a time: the floating windows to close when <paramref name="opening"/> opens, every other one
        /// of the four that is open now (the opening one itself is left as it is).
        /// </summary>
        public static List<RimfolkWindow> ToClose(RimfolkWindow opening, IEnumerable<RimfolkWindow> open)
        {
            var list = new List<RimfolkWindow>();
            if (open == null) return list;
            foreach (var w in open)
                if (w != opening && !list.Contains(w))
                    list.Add(w);
            return list;
        }

        /// <summary>The floating windows open after <paramref name="opening"/> opened: that one alone.</summary>
        public static List<RimfolkWindow> AfterOpening(RimfolkWindow opening, IEnumerable<RimfolkWindow> open)
        {
            var after = new List<RimfolkWindow>();
            var closed = ToClose(opening, open);
            if (open != null)
                foreach (var w in open)
                    if (!closed.Contains(w) && !after.Contains(w))
                        after.Add(w);
            if (!after.Contains(opening)) after.Add(opening);
            return after;
        }

        // ---------------- Every framed window's key and least size ----------------

        /// <summary>The almanac's least: its categories, a page list and a page beside them at the smallest text.</summary>
        public const float AlmanacLeastW = 560f, AlmanacLeastH = 400f;

        /// <summary>
        /// Every window of Rimfolk's in the frame, by key: the least it is sized to (the room its contents need; it grows
        /// from there), and whether it keeps the bottom bar clear. A dialog's least is the size it was made for, so nothing
        /// in it is cut; one whose contents scroll may be made smaller.
        /// </summary>
        public static readonly FrameKey[] Keys =
        {
            // The four windows: the tabs and the corner in the title bar, a view with its drawer laid over it.
            new FrameKey(Region, WindowFrameMath.RegionLeastW, WindowFrameMath.RegionLeastH, true),
            new FrameKey(Council, 680f, 460f, true),
            new FrameKey(Planning, 680f, 460f, true),
            new FrameKey(Defense, 640f, 440f, true),
            // Beside play: the Almanac and a card in its floating window.
            new FrameKey("Almanac", AlmanacLeastW, AlmanacLeastH, true),
            new FrameKey("Card", 380f, 320f, true),
            // A colonist's card (UI/ColonistCard/), one per colonist beside play: its header, a column of its parts (they
            // scroll) and the foot's buttons in two rows.
            new FrameKey("ColonistCard", ColonistCardMath.LeastW, ColonistCardMath.LeastH, true),
            // Rimfolk's weapon card (Weapons/WeaponCardWindow.cs): its badge, chips and chart at the least width; the rest scrolls.
            new FrameKey("WeaponCard", 440f, 460f),
            // Rimfolk's apparel card (Apparel/ApparelCardWindow.cs): its badge, chips and body figure at the least width; the rest scrolls.
            new FrameKey("ApparelCard", 440f, 460f),
            // Rimfolk's item card (Items/ThingCardWindow.cs): its head and tags at the least width; the rest scrolls.
            new FrameKey("ItemCard", 440f, 460f),
            // The trade windows.
            new FrameKey("Trade", WindowFrameMath.TradeLeastW, WindowFrameMath.TradeLeastH),
            new FrameKey("Market", 820f, 400f),
            new FrameKey("Stall", 980f, 560f),
            // Letters, journeys, camps and the colony's founding. An event card (UI/EventCards/): its header, a few lines and
            // an answer, the foot's buttons (EventCardMath.LeastW, LeastH); the rest scrolls.
            new FrameKey("Letter", 580f, 740f),
            new FrameKey("EventCard", 520f, 440f),
            // A letter exchanged, to read again (Letters/LetterReadWindow.cs): a sheet or two of paper at the least width; the rest scrolls.
            new FrameKey("LetterRead", 420f, 360f),
            new FrameKey("Journey", 760f, 540f),
            new FrameKey("JourneyPick", 440f, 400f),
            new FrameKey("Camp", 560f, 480f),
            new FrameKey("Founding", 760f, 560f),
            new FrameKey("Newcomers", 1000f, 760f),
            new FrameKey("Landing", 600f, 500f),
            new FrameKey("HandOver", 800f, 560f),
            new FrameKey("ScenarioStart", 720f, 480f),
            // The council's and the colony's own dialogs.
            new FrameKey("Feast", 640f, 480f),
            new FrameKey("Journal", 420f, 360f),
            new FrameKey("News", 480f, 400f),
            new FrameKey("CalendarDay", 540f, 600f),
            new FrameKey("Clock", 640f, 480f),
            new FrameKey("Gift", 460f, 400f),
            new FrameKey("SelfCheck", 560f, 420f),
            new FrameKey("ArrangeBar", 420f, 420f),
            new FrameKey("PanelOptions", 360f, 400f),
            new FrameKey("GuestCheck", 420f, 360f),
            // Choosing other clothes for an ideoligion's role (Faith/RoleClothesWindow.cs): two rows asked for now, a few to pick from; the rest scrolls.
            new FrameKey("RoleClothes", 520f, 480f),
            // Building and the defenses: the plans' small windows beside the map.
            new FrameKey("NameLine", 380f, 170f),
            new FrameKey("NameRoom", 360f, 160f),
            new FrameKey("ShelfReserve", 560f, 460f),
            new FrameKey("ShelfPick", 440f, 400f),
            new FrameKey("FloorPicker", 400f, 420f),
            new FrameKey("GroundPlan", 380f, 360f),
            new FrameKey("Rearrange", 420f, 400f)
        };

        /// <summary>A window's key, its least size and its bottom bar; false for a key that is none of them.</summary>
        public static bool TryKey(string key, out FrameKey k)
        {
            foreach (var f in Keys)
                if (f.key == key)
                {
                    k = f;
                    return true;
                }
            k = new FrameKey(key, WindowFrameMath.LeastW, WindowFrameMath.LeastH);
            return false;
        }

        // ---------------- The council's pages as the window grows ----------------

        /// <summary>This week's cards: one column, two from this width (CouncilLayoutMath), three from this one.</summary>
        public const float WeekThreeFrom = 1500f;

        /// <summary>This week's columns in a page this wide: one, two from 700, three on a window made large.</summary>
        public static int WeekColumns(float width) => width >= WeekThreeFrom ? 3 : CouncilLayoutMath.Columns(width);

        // ---------------- Rimfolk's dialogs as they grow ----------------

        /// <summary>The journey planner's overview of who goes: 45% of the planner, 280 to 500 wide, up to 640 from this width.</summary>
        public const float JourneyWideFrom = 1400f;

        public static float JourneyWhoWidth(float width) => Math.Max(280f, Math.Min(width * 0.45f, width >= JourneyWideFrom ? 640f : 500f));

        /// <summary>The founding window's list of who goes: 46% of it, 300 to 460 wide, up to 600 from the journey's wide width.</summary>
        public static float FoundingWhoWidth(float width) => Math.Max(300f, Math.Min(width * 0.46f, width >= JourneyWideFrom ? 600f : 460f));

        /// <summary>
        /// A dialog's row of acts or switches (the camp's): its usual width, and in a dialog made at least twice as wide, up
        /// to half again as wide (never past half the dialog), so a long act stays on one line.
        /// </summary>
        public static float DialogActWidth(float width, float usual) => width >= usual * 2f ? Math.Max(usual, Math.Min(width * 0.5f, usual * 1.5f)) : usual;
    }
}
