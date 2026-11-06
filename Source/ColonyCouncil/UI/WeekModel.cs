using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One row of This week: one sentence (its names links to their cards), a quieter second line (what it is, or the
    /// steps of something under way), a note on the right, its act ("Answer", "Decide", "Look", "Pay"), where a click goes.
    /// </summary>
    public sealed class WeekRow
    {
        /// <summary>The sentence; names in it may be links (LinkText reads them).</summary>
        public string text, note, tip;
        public Texture2D icon;
        public Color color = Color.white, noteColor = UiKit.Faint;
        /// <summary>A portrait in front of the line instead of the icon (or a people's banner).</summary>
        public Pawn pawn;
        public Faction faction;
        public Action click;
        /// <summary>A second line in small print: what it is ("a letter waiting for your answer"), its newest change.</summary>
        public string sub;
        public Color subColor = UiKit.Faint;
        /// <summary>Something under way in steps (written, on the road, arrived, answered): the words and where it is; never a time left.</summary>
        public string[] steps;
        public int step = -1;
        /// <summary>Its act on the right ("Answer", "Decide", "Look", "Pay", "Send"), greyed with why not.</summary>
        public string act, actWhy, actTip;
        public Action actRun;
        /// <summary>What the row's pin follows (a colonist, a person, a place...): the pin shows on hover, or always when followed.</summary>
        public ThingRef follow;
        /// <summary>A wish the pin keeps at the top of This week.</summary>
        public Wish pinWish;
        public bool pinned;
        /// <summary>The mood balance bar: the good share of Rimfolk's moods (0..1), or -1 for an ordinary line.</summary>
        public float balance = -1f;
        public string balanceGood, balanceBad;
        /// <summary>Sorting within its part of the section (smaller first); not shown.</summary>
        public float order;
        /// <summary>
        /// What the row is about (a Wish, a GrantWatch, a petition, a Letter...), for what reads the inbox besides This week:
        /// the on-screen council panel and the colonist bar's badges (UI/Inbox.cs, <see cref="Inbox.Waiting"/>).
        /// </summary>
        public object about;
        /// <summary>Others it is about besides its pawn: a petition's other names, each of whom it waits on too.</summary>
        public List<Pawn> others;
        /// <summary>How pressing it is on the on-screen panels' scale (Hud/HudRank.cs); 0 is merely waiting.</summary>
        public float rank;
        /// <summary>The on-screen council panel's part it shows under (InboxMath.PanelParts); null: its provider's.</summary>
        public string part;
        /// <summary>A row of Needs you that waits on the player; false for one that only tells (the news letters' row).</summary>
        public bool waits = true;
        /// <summary>The note's and the act's widths, measured once when first drawn.</summary>
        public float noteWidth = -1f, actWidth = -1f;

        /// <summary>Two lines tall: a second line or steps under the sentence.</summary>
        public bool Tall => !sub.NullOrEmpty() || steps != null;

        /// <summary>The row's pin does something (a thing to follow, or a wish to pin).</summary>
        public bool Pinnable => !follow.IsNone || pinWish != null;
    }

    /// <summary>One section of This week: a heading that opens its view, its rows (the first few, the rest opened in place), and its footer rows.</summary>
    public sealed class WeekCard
    {
        public string key, title, empty, page, pageName, more, less;
        public InboxSection section;
        /// <summary>The count beside the title, or -1 for none.</summary>
        public int count = -1;
        public Texture2D icon;
        public Color color = UiKit.HeaderColor;
        public readonly List<WeekRow> rows = new List<WeekRow>();
        /// <summary>Rows that stand at the section's foot whatever is folded (the mood balance).</summary>
        public readonly List<WeekRow> footer = new List<WeekRow>();
        // Placed by the panel when the width changes.
        public int column;
        public float y, height;
        public int shown;
        public bool opened;
    }

    /// <summary>
    /// What This week shows, worked out at most once a second (or right after the player changes something, such as a
    /// pin): every provider of the inbox (UI/Inbox.cs) is asked once here, section by section, so drawing the page asks
    /// nothing of the game. Each provider is asked in its own try/catch, so one system's trouble leaves the rest standing.
    /// </summary>
    public sealed class WeekModel
    {
        public string title, sub, stat, headerTip;
        public Color statColor = UiKit.Gold;
        public readonly List<WeekCard> cards = new List<WeekCard>();
        public int needs;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static WeekModel Build(CouncilGameComponent comp, Map map, ICollection<InboxSection> opened = null)
        {
            var m = new WeekModel
            {
                title = "CC_Week_Header".Translate(),
                sub = "CC_UI_Sub_Week".Translate(),
                headerTip = "CC_Week_HeaderTip".Translate()
            };
            var rows = new List<WeekRow>();
            var memory = WeekMemory.Instance;
            foreach (var section in InboxMath.Sections)
            {
                var c = Card(section);
                c.opened = opened != null && opened.Contains(section);
                foreach (var e in Inbox.In(section))
                {
                    var p = e.value;
                    rows.Clear();
                    try
                    {
                        p.rows(rows);
                    }
                    catch (Exception ex)
                    {
                        CouncilLog.ErrorOnce("this week: " + p.id, ex);
                        continue;
                    }
                    rows.RemoveAll(r => r == null);
                    // A thing already followed shows its pin lit, whichever part's row it is.
                    foreach (var r in rows)
                        if (!r.follow.IsNone && r.pinWish == null)
                            r.pinned = memory != null && memory.IsFollowed(r.follow);
                    // Each part's rows as its card on This week always sorted them: by their order, equals as found (a stable sort).
                    var into = p.footer ? c.footer : c.rows;
                    into.AddRange(rows.OrderBy(r => r.order));
                }
                // Following and the plan show only with something in them.
                if ((section == InboxSection.Following || section == InboxSection.Plan) && c.rows.Count == 0 && c.footer.Count == 0) continue;
                int shown = InboxMath.Shown(section, c.rows.Count, c.opened);
                int hidden = c.rows.Count - InboxMath.Shown(section, c.rows.Count, false);
                if (hidden > 0)
                {
                    if (shown < c.rows.Count) c.more = "CC_Week_More".Translate(hidden).Resolve();
                    else c.less = "CC_Inbox_Fewer".Translate().Resolve();
                }
                // The heading's count: how many rows (Needs you's is the one count, below).
                if (section != InboxSection.People && section != InboxSection.Plan) c.count = c.rows.Count;
                m.cards.Add(c);
            }
            // The same count as the tab, the corner and the council button (the news letters aren't counted: they ask nothing).
            m.needs = Inbox.Count;
            // Needs you's heading says that count too, not its rows (the news letters' row among them), so it never
            // disagrees with the stat above it and the tab.
            foreach (var c in m.cards)
                if (c.section == InboxSection.Needs)
                    c.count = m.needs;
            m.stat = m.needs > 0 ? "CC_Week_StatNeeds".Translate(m.needs).Resolve() : "CC_Week_StatCalm".Translate().Resolve();
            m.statColor = m.needs > 0 ? UiKit.Gold : UiKit.Good;
            return m;
        }

        /// <summary>A section's card: its title, what it says when empty, its icon, its colour, the view its heading opens.</summary>
        private static WeekCard Card(InboxSection s)
        {
            string title, empty, page;
            Texture2D icon;
            Color color;
            switch (s)
            {
                case InboxSection.Following:
                    title = "CC_Inbox_Following";
                    empty = "CC_Inbox_Empty_Following";
                    icon = Icons.Pin ?? Icons.Promise;
                    color = UiKit.Gold;
                    page = null;
                    break;
                case InboxSection.Needs:
                    title = "CC_Inbox_Needs";
                    empty = "CC_Inbox_Empty_Needs";
                    icon = Icons.Inbox ?? Icons.Week;
                    color = UiKit.Gold;
                    page = null;
                    break;
                case InboxSection.UnderWay:
                    title = "CC_Inbox_UnderWay";
                    empty = "CC_Inbox_Empty_UnderWay";
                    icon = Icons.Away ?? Icons.Visitor;
                    color = UiKit.Info;
                    page = "HereAndAway";
                    break;
                case InboxSection.Coming:
                    title = "CC_Inbox_Coming";
                    empty = "CC_Inbox_Empty_Coming";
                    icon = CalendarPanel.Icon ?? Icons.Chronicle;
                    color = UiKit.HeaderColor;
                    page = "Calendar";
                    break;
                case InboxSection.People:
                    title = "CC_Inbox_People";
                    empty = "CC_Inbox_Empty_People";
                    icon = Icons.Moods;
                    color = UiKit.Good;
                    page = "Moods";
                    break;
                default:
                    title = "CC_Inbox_Plan";
                    empty = "CC_Inbox_Empty_Plan";
                    icon = Icons.Plan ?? Icons.Week;
                    color = UiKit.Gold;
                    page = "Plan";
                    break;
            }
            var c = new WeekCard
            {
                key = s.ToString(),
                section = s,
                title = title.Translate(),
                empty = empty.Translate(),
                icon = icon,
                color = color,
                page = page
            };
            if (page != null) c.pageName = MainTabWindow_Council.TabName(page);
            return c;
        }

        /// <summary>Everyone the page is about: the colony's free colonists with an inner life, on a map or away.</summary>
        public static List<Pawn> Colonists()
        {
            var list = new List<Pawn>();
            // A copy: the game reuses the list it hands out.
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoCryptosleep.ToList())
                if (p != null && !p.IsQuestLodger() && p.RaceProps.Humanlike && People.HasInnerLife(p) && !list.Contains(p))
                    list.Add(p);
            return list;
        }

        // ---------------- Clicks ----------------

        internal static Action Page(string page) => () => MainTabWindow_Council.OpenTab(page);

        /// <summary>
        /// Go to a thing or a place: one on a map, the window closed and the camera there, as before; a place out on the
        /// world (a town, a post, a camp) on the region map with its card (UI/Cards/Cards.cs), not the game's world view.
        /// </summary>
        internal static Action Go(GlobalTargetInfo target) => () =>
        {
            if (!target.IsValid) return;
            Cards.ShowOnMap(target);
        };

        /// <summary>Select a colonist and open one of their inspect tabs (the Mood tab, the Health tab).</summary>
        internal static Action Inspect(Pawn p, Type tab) => () =>
        {
            if (p == null) return;
            try
            {
                if (!p.Spawned && p.CarriedBy == null)
                {
                    Cards.Open(p);
                    return;
                }
                Find.MainTabsRoot.EscapeCurrentTab(false);
                CameraJumper.TryJumpAndSelect(p, CameraJumper.MovementMode.Pan);
                if (Find.Selector.SingleSelectedThing == p) InspectPaneUtility.OpenTab(tab);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("this week: inspect", e);
            }
        };

        internal static string ClickPage(string page) => "CC_Week_ClickPage".Translate(MainTabWindow_Council.TabName(page)).Resolve();

        internal static string Tip(string body, string click) => body.NullOrEmpty() ? click : body + "\n\n" + click.Colorize(ColoredText.SubtleGrayColor);

        internal static Color MoodColor(Pawn p, float mood)
        {
            if (p.InMentalState) return UiKit.Bad;
            var br = p.mindState?.mentalBreaker;
            float minor = 0.35f, major = 0.2f;
            try
            {
                if (br != null)
                {
                    minor = br.BreakThresholdMinor;
                    major = br.BreakThresholdMajor;
                }
            }
            catch
            {
            }
            return mood < major ? UiKit.Bad : mood < minor ? UiKit.Warn : mood < minor + 0.1f ? UiKit.Gold : UiKit.Good;
        }

        /// <summary>
        /// A click to one of the council's pages about one colony: with more than one colony, the map in view goes to that
        /// colony first, so the page shows its council.
        /// </summary>
        internal static Action CouncilPage(ColonyState council, string page)
        {
            var open = Page(page);
            var map = council?.Map;
            if (map == null || !ColonyState.Several) return open;
            return () =>
            {
                if (Find.CurrentMap != map && Homes.IsHome(map)) Current.Game.CurrentMap = map;
                open();
            };
        }

        /// <summary>With more than one colony, the settlement's name before a line about something at one home.</summary>
        internal static string AtHome(Map map, string text)
        {
            if (map == null || !ColonyState.Several || !Homes.IsHome(map)) return text;
            return "CC_Tell_At".Translate(Homes.Name(map).Named("PLACE"), text.Named("TEXT")).Resolve();
        }

        /// <summary>With more than one colony, the settlement's name before a line about one colony's council.</summary>
        internal static string AtColony(ColonyState council, string text)
        {
            var map = council?.Map;
            if (map == null || !ColonyState.Several) return text;
            return "CC_Tell_At".Translate(Homes.Name(map).Named("PLACE"), text.Named("TEXT")).Resolve();
        }

        /// <summary>A colonist's name as a link to their card, for a row's sentence.</summary>
        internal static string Name(Pawn p) => p == null ? "?" : ThingRefs.Link(p.LabelShort, ThingRefs.Of(p));

        /// <summary>The line's key for a Rimfolk letter still waiting for the player's answer, or null.</summary>
        internal static string WaitingKind(Letter l)
        {
            switch (l)
            {
                case ChoiceLetter_Dilemma d: return d.resolved ? null : "CC_Week_LetterDilemma";
                case ChoiceLetter_Happening h: return h.resolved ? null : "CC_Week_LetterQuestion";
                case ChoiceLetter_CalendarDay cd: return cd.resolved ? null : "CC_Week_LetterDay";
                case ChoiceLetter_Camp camp: return camp.resolved ? null : "CC_Week_LetterQuestion";
                case ChoiceLetter_Situation s: return s.resolved ? null : "CC_Week_LetterQuestion";
                case ChoiceLetter_CouncilMeeting _: return "CC_Week_LetterMeeting";
                default: return null;
            }
        }

        // ---------------- The count on the tab ----------------

        /// <summary>How many things wait on the player (Inbox.Count: wishes, petitions, letters, promises at risk, the region's and diplomacy's asks).</summary>
        public static int NeedsCount(CouncilGameComponent comp) => comp == null ? 0 : Inbox.Count;
    }
}
