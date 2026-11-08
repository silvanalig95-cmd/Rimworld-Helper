using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>How the list of peoples is sorted: closest first (the default), by regard, by name, by age.</summary>
    public enum PeoplesSort : byte
    {
        Closest,
        Regard,
        Name,
        Age
    }

    /// <summary>The list's three groups: allies, the rest at peace with the colony, those at war with it.</summary>
    public enum PeoplesGroup : byte
    {
        Allies,
        Neutral,
        War
    }

    /// <summary>The list's filters: any number on at once, and a people shows when it passes every one that is on.</summary>
    [Flags]
    public enum PeoplesFilter : byte
    {
        None = 0,
        /// <summary>A town of theirs within the region map's reach of a home.</summary>
        Neighbours = 1,
        /// <summary>Mail from them on its way in, or a letter of theirs waiting in the letter stack.</summary>
        Mail = 2,
        /// <summary>Ties with the colony (pacts, debts, marriages...) or something under way with them.</summary>
        Dealings = 4
    }

    /// <summary>A people's page's tabs, in their order.</summary>
    public enum PeopleTab : byte
    {
        Overview,
        Towns,
        People,
        Dealings,
        History
    }

    /// <summary>What "Do something with..." is aimed at: the whole people, one of its towns, one of its people.</summary>
    public enum ActTarget : byte
    {
        People,
        Town,
        Person
    }

    /// <summary>A line of the web between two peoples: none, allied, at war.</summary>
    public enum WebTie : byte
    {
        None,
        Allies,
        War
    }

    /// <summary>What the list knows of a people, as plain data.</summary>
    public struct PeopleFacts
    {
        public string name;
        /// <summary>Its words for the search, lower case: its name, its kind, its towns, the people the colony knows there.</summary>
        public string search;
        /// <summary>The game's relation with the colony: 1 allied, 0 neutral, -1 hostile.</summary>
        public int relation;
        public bool permanentEnemy;
        public bool hasGoodwill;
        public int goodwill;
        /// <summary>Days on the road to their nearest town by the post's reach rule (Post/PostReach.cs); -1 for no way by land.</summary>
        public float days;
        /// <summary>World tiles to that town (the tie-break, and the order of towns with no way by land).</summary>
        public float tiles;
        /// <summary>Their age, 0 tribal to 3 spacer (Peoples.AgeOf).</summary>
        public int age;
        public bool neighbour, mail, dealings;
    }

    /// <summary>
    /// The Peoples view's pure rules (docs/design/diplomacy-screen.md, step A; UI/Region/PeoplesView.cs): the groups, the
    /// four sorts (closest first by default, the player's choice kept for the session), the search and the filters, the
    /// regard meter, this season's trend, the web of who stands with whom, and the layout worked out once per width (the
    /// list beside the page at every window size, narrower when the drawer is open). No game types, so Tools/Tests checks it.
    /// </summary>
    public static class PeoplesViewMath
    {
        // ---------------- Groups ----------------

        /// <summary>Allies, at war (a people at war with everyone is always there), or the rest.</summary>
        public static PeoplesGroup GroupOf(int relation, bool permanentEnemy) =>
            permanentEnemy || relation < 0 ? PeoplesGroup.War : relation > 0 ? PeoplesGroup.Allies : PeoplesGroup.Neutral;

        public static PeoplesGroup GroupOf(PeopleFacts p) => GroupOf(p.relation, p.permanentEnemy);

        public static readonly PeoplesGroup[] Groups = { PeoplesGroup.Allies, PeoplesGroup.Neutral, PeoplesGroup.War };

        public static readonly PeoplesSort[] Sorts = { PeoplesSort.Closest, PeoplesSort.Regard, PeoplesSort.Name, PeoplesSort.Age };

        public static readonly PeoplesFilter[] Filters = { PeoplesFilter.Neighbours, PeoplesFilter.Mail, PeoplesFilter.Dealings };

        public static readonly PeopleTab[] Tabs = { PeopleTab.Overview, PeopleTab.Towns, PeopleTab.People, PeopleTab.Dealings, PeopleTab.History };

        // ---------------- Sorting ----------------

        /// <summary>Their nearest town's days as a sort key: towns with no way by land after every town with one, nearer first.</summary>
        public static float DaysKey(PeopleFacts p) => p.days >= 0f ? p.days : 100000f + Math.Max(0f, p.tiles);

        /// <summary>Their regard as a sort key: the game's goodwill, a people with none (at war with everyone) below all.</summary>
        public static int RegardKey(PeopleFacts p) => p.hasGoodwill && !p.permanentEnemy ? p.goodwill : -1000;

        /// <summary>
        /// The order within a group: closest first; by regard, warmest first; by name; by age, tribal to spacer. Ties go to the
        /// closest, then the name.
        /// </summary>
        public static int Compare(PeopleFacts a, PeopleFacts b, PeoplesSort sort)
        {
            int c = 0;
            switch (sort)
            {
                case PeoplesSort.Regard:
                    c = RegardKey(b).CompareTo(RegardKey(a));
                    break;
                case PeoplesSort.Name:
                    c = string.Compare(a.name ?? "", b.name ?? "", StringComparison.OrdinalIgnoreCase);
                    break;
                case PeoplesSort.Age:
                    c = a.age.CompareTo(b.age);
                    break;
            }
            if (c != 0) return c;
            c = DaysKey(a).CompareTo(DaysKey(b));
            if (c != 0) return c;
            return string.Compare(a.name ?? "", b.name ?? "", StringComparison.OrdinalIgnoreCase);
        }

        // ---------------- Search and filters ----------------

        /// <summary>Every word of the search is somewhere in its words (any case); an empty search matches all.</summary>
        public static bool Matches(string query, string search)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            string hay = (search ?? "").ToLowerInvariant();
            foreach (var word in query.ToLowerInvariant().Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
                if (hay.IndexOf(word, StringComparison.Ordinal) < 0)
                    return false;
            return true;
        }

        public static bool Has(PeopleFacts p, PeoplesFilter f)
        {
            switch (f)
            {
                case PeoplesFilter.Neighbours: return p.neighbour;
                case PeoplesFilter.Mail: return p.mail;
                case PeoplesFilter.Dealings: return p.dealings;
                default: return true;
            }
        }

        /// <summary>It passes every filter that is on.</summary>
        public static bool Passes(PeopleFacts p, PeoplesFilter on)
        {
            foreach (var f in Filters)
                if ((on & f) != 0 && !Has(p, f))
                    return false;
            return true;
        }

        /// <summary>A filter's count beside its chip: the peoples the search finds that have it.</summary>
        public static int Count(IList<PeopleFacts> all, PeoplesFilter f, string query)
        {
            int n = 0;
            if (all == null) return 0;
            for (int i = 0; i < all.Count; i++)
                if (Has(all[i], f) && Matches(query, all[i].search))
                    n++;
            return n;
        }

        /// <summary>
        /// The list: the peoples the search finds and every filter on passes, in their groups (allies, the rest, at war), each
        /// group in the sort's order. Indices into <paramref name="all"/>, one list a group.
        /// </summary>
        public static List<int>[] Grouped(IList<PeopleFacts> all, PeoplesSort sort, PeoplesFilter on, string query)
        {
            var groups = new[] { new List<int>(), new List<int>(), new List<int>() };
            if (all == null) return groups;
            for (int i = 0; i < all.Count; i++)
                if (Matches(query, all[i].search) && Passes(all[i], on))
                    groups[(int)GroupOf(all[i])].Add(i);
            foreach (var g in groups) g.Sort((x, y) => Compare(all[x], all[y], sort));
            return groups;
        }

        // ---------------- Regard ----------------

        /// <summary>The regard meter's fill: goodwill -100 to 100 as 0 to 1.</summary>
        public static float RegardFill(int goodwill) => Math.Max(0f, Math.Min(1f, (goodwill + 100f) / 200f));

        /// <summary>What it takes for a season's deeds to move the trend: a good turn or a wrong of some weight, not one sale.</summary>
        public const float TrendFrom = 4f;

        /// <summary>
        /// Which way their regard is heading this season, from what they remember the colony did in it (World/FactionMemory.cs):
        /// its good turns against its wrongs, by their weight. 1 warming, -1 cooling, 0 steady.
        /// </summary>
        public static int Trend(float favour, float wrong)
        {
            float d = favour - wrong;
            return d >= TrendFrom ? 1 : d <= -TrendFrom ? -1 : 0;
        }

        // ---------------- The web ----------------

        /// <summary>The most peoples the web draws: the first in the list's order.</summary>
        public const int WebMost = 14;

        /// <summary>
        /// A people's place on the web: round an ellipse from the top, clockwise, evenly spaced. The colony sits at its middle.
        /// </summary>
        public static void WebPoint(int i, int n, float cx, float cy, float rx, float ry, out float x, out float y)
        {
            if (n <= 0)
            {
                x = cx;
                y = cy;
                return;
            }
            double a = -Math.PI / 2.0 + 2.0 * Math.PI * i / n;
            x = cx + (float)(Math.Cos(a) * rx);
            y = cy + (float)(Math.Sin(a) * ry);
        }

        /// <summary>
        /// The line between two peoples: allied, at war, or none (a people at war with everyone draws no lines, or the web
        /// would be nothing but theirs: its dot says it instead).
        /// </summary>
        public static WebTie TieOf(int relation, bool permanentA, bool permanentB)
        {
            if (permanentA || permanentB) return WebTie.None;
            return relation > 0 ? WebTie.Allies : relation < 0 ? WebTie.War : WebTie.None;
        }

        // ---------------- Layout ----------------

        /// <summary>The list's width: 320 beside a roomy page, 260 narrower, 200 (compact rows) with the drawer open at the standard window.</summary>
        public const float ListWide = 320f, ListNarrow = 260f, ListCompact = 200f;

        /// <summary>The list in a window made large on a big screen (a view from 1400 wide): room for longer names and their words.</summary>
        public const float ListWider = 380f, WiderFrom = 1400f;

        /// <summary>Between the list and the page.</summary>
        public const float Gap = 12f;

        /// <summary>A row of the list, and the height of the web.</summary>
        public const float RowH = 40f, WebH = 156f;

        /// <summary>The list's width in a view this wide.</summary>
        public static float ListWidth(float viewWidth) => viewWidth >= WiderFrom ? ListWider : viewWidth >= 860f ? ListWide : viewWidth >= 640f ? ListNarrow : ListCompact;

        /// <summary>The page's width beside the list.</summary>
        public static float PageWidth(float viewWidth) => Math.Max(0f, viewWidth - ListWidth(viewWidth) - Gap);

        /// <summary>Compact rows (no age word, a shorter meter) in a list this narrow.</summary>
        public static bool Compact(float listWidth) => listWidth < 240f;

        /// <summary>
        /// The web stays fixed at the list's foot when the column is tall enough to keep seven rows above it (a 1080 screen at
        /// scale 1); on a shorter one it ends the list's scroll, so the rows keep the room.
        /// </summary>
        public static bool WebFixed(float columnHeight) => columnHeight - WebH - ListTop >= 7f * RowH;

        /// <summary>The view's body under its header (the list's column and the page) in a content area this tall.</summary>
        public static float BodyHeight(float contentHeight, float headerHeight) => Math.Max(0f, contentHeight - headerHeight - 4f);

        /// <summary>The search, the filter chips and the sort above the rows.</summary>
        public const float ListTop = 84f;

        /// <summary>The acts' rows in two columns on a page this wide, three on a large window's page, else one.</summary>
        public static int ActColumns(float pageWidth) => pageWidth >= 1180f ? 3 : pageWidth >= 760f ? 2 : 1;

        /// <summary>One act column's width on a page this wide (12 between columns).</summary>
        public static float ActColumnWidth(float pageWidth)
        {
            int n = ActColumns(pageWidth);
            return n <= 1 ? pageWidth : (pageWidth - 12f * (n - 1)) / n;
        }
    }
}
