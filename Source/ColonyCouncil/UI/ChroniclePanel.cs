using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Colony → Chronicle: everything the colony has been through, newest first, as a timeline (the day on the left,
    /// an icon for each kind of entry on the line, the colonist it is about a link to their card), with filters and the
    /// yearly annals. Two filters are what were pages of their own: Dreams (every dream told, by dreamer and kind) and
    /// Journals (each colonist's journal to read), UI/HistoryPanels.cs. The filter is part of the way back
    /// (UI/Cards/Nav.cs), and the old addresses "Dreams" and "Journals" open the Chronicle on theirs.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ChroniclePanel
    {
        private static Vector2 scroll;
        private static float height = 1000f;

        static ChroniclePanel()
        {
            Nav.Filters("Chronicle", () => chronicleFilter == ChronicleFilter.All ? null : chronicleFilter.ToString(), SetFilter);
        }

        /// <summary>The filter put back from a step of the way back, or asked for by an old address ("Dreams"); none for all.</summary>
        private static void SetFilter(string name)
        {
            var f = name != null && System.Enum.TryParse(name, out ChronicleFilter parsed) ? parsed : ChronicleFilter.All;
            if (f == chronicleFilter) return;
            chronicleFilter = f;
            scroll = Vector2.zero;
        }

        private enum ChronicleFilter
        {
            All,
            Wishes,
            Council,
            People,
            Strangers,
            World,
            Dreams,
            Journals,
            Losses
        }

        private static readonly ChronicleFilter[] Filters = (ChronicleFilter[])System.Enum.GetValues(typeof(ChronicleFilter));
        private static ChronicleFilter? filterMemo;

        /// <summary>The filter, remembered between sessions (UI/PageMemory.cs).</summary>
        private static ChronicleFilter chronicleFilter
        {
            get => filterMemo ??= PageMemory.GetEnum("chronicle.filter", ChronicleFilter.All);
            set
            {
                filterMemo = value;
                PageMemory.SetEnum("chronicle.filter", value);
            }
        }
        private static readonly Dictionary<ChronicleEntry, float> chronicleHeights = new Dictionary<ChronicleEntry, float>();
        private static float chronicleWidth;

        // The filtered entries, rebuilt when the filter or the chronicle changes.
        private static readonly List<ChronicleEntry> entries = new List<ChronicleEntry>();
        private static ChronicleFilter entriesFilter;
        private static int entriesCount = -1;
        private static List<ChronicleEntry> entriesSource;
        private static ChronicleEntry entriesLast;

        private static string[] filterLabels;

        private const float DateW = 74f;
        private const float RailX = DateW + 12f;
        private const float TextX = DateW + 32f;

        public static void Draw(Rect rect, CouncilGameComponent comp)
        {
            // The yearly annals, newest first, as buttons on the right of the header; the journals of the departed before them.
            Text.Font = GameFont.Small;
            float bx = rect.xMax;
            float minLeft = rect.x + Mathf.Min(320f, rect.width * 0.45f);
            // The journals of the departed walk every journal: worked out twice a second, not every frame.
            var departed = UiCache.Get(comp.journals, 92, () => comp.journals.Departed().ToList(), 0.5f);
            if (departed.Count > 0)
            {
                string dl = "CC_Journal_Departed".Translate();
                float dw = Text.CalcSize(dl).x + 20f;
                if (bx - dw - 6f >= minLeft)
                {
                    bx -= dw + 6f;
                    if (UiKit.ActButton(new Rect(bx, rect.y + 6f, dw, UiKit.RowStandard), dl, null))
                        Find.WindowStack.Add(new FloatMenu(departed.Select(j => new FloatMenuOption(j.name, () => Find.WindowStack.Add(new Dialog_Journal(j)))).ToList()));
                }
            }
            foreach (var a in comp.annals.written.AsEnumerable().Reverse().Take(6))
            {
                string label = "CC_Annals_Button".Translate(a.year);
                float w = Text.CalcSize(label).x + 20f;
                if (bx - w - 6f < minLeft) break;
                bx -= w + 6f;
                var entry = a;
                if (UiKit.ActButton(new Rect(bx, rect.y + 6f, w, UiKit.RowStandard), label, null))
                    Find.WindowStack.Add(new Dialog_MessageBox(entry.text, title: "CC_Annals_Title".Translate(entry.year)));
            }

            var list = Entries(comp);
            var filter = chronicleFilter;
            string sub = filter == ChronicleFilter.Dreams ? "CC_UI_Sub_Dreams".Translate() : filter == ChronicleFilter.Journals ? "CC_UI_Sub_Journals".Translate() : "CC_UI_Sub_Chronicle".Translate();
            string stat = filter == ChronicleFilter.Dreams ? "CC_UI_Stat_Dreams".Translate(HistoryPanels.DreamCount(comp)).Resolve()
                : filter == ChronicleFilter.Journals ? "CC_UI_Stat_Journals".Translate(HistoryPanels.JournalCount(comp)).Resolve()
                : "CC_UI_Stat_Entries".Translate(list.Count).Resolve();
            float top = UiKit.PageHeader(rect, Icons.Chronicle, "CC_Chronicle_Header".Translate(), sub, stat, null, rect.xMax - bx);

            // Filters: everything, or one part of colony life, as filter chips. Squeezed evenly when they don't all fit.
            filterLabels ??= Filters.Select(f => ("CC_ChronicleFilter_" + f).Translate().ToString()).ToArray();
            float total = 0f;
            foreach (var label in filterLabels) total += UiKit.FilterChipWidth(label) + 4f;
            float even = (rect.width - 4f * (Filters.Length - 1)) / Filters.Length;
            float fx = rect.x;
            for (int i = 0; i < Filters.Length; i++)
            {
                var f = Filters[i];
                float fw = total > rect.width ? even : UiKit.FilterChipWidth(filterLabels[i]);
                if (UiKit.FilterChip(new Rect(fx, rect.y + top, fw, 22f), filterLabels[i], f == chronicleFilter) && f != chronicleFilter)
                {
                    chronicleFilter = f;
                    scroll = Vector2.zero;
                }
                fx += fw + 4f;
            }
            top += 22f + UiKit.CardGap + 2f;

            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
            // What were the Dreams and Journals pages.
            if (chronicleFilter == ChronicleFilter.Dreams)
            {
                HistoryPanels.DreamsBody(outRect, comp);
                return;
            }
            if (chronicleFilter == ChronicleFilter.Journals)
            {
                HistoryPanels.JournalsBody(outRect, comp);
                return;
            }
            var view = new Rect(0f, 0f, outRect.width - 18f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float width = view.width;
            float y = 0f;
            if (list.Count == 0) y += UiKit.EmptyState(0f, y, width, FilterIcon(chronicleFilter), "CC_Chronicle_Empty".Translate(), 140f);
            if (chronicleWidth != width || chronicleHeights.Count > 3000)
            {
                chronicleHeights.Clear();
                chronicleWidth = width;
            }
            if (marked.Count > 3000) marked.Clear();
            // The line the entries hang on, only where it's in sight.
            if (list.Count > 0)
            {
                float lineTop = Mathf.Max(0f, scroll.y), lineBottom = Mathf.Min(height - 10f, scroll.y + outRect.height);
                if (lineBottom > lineTop) Widgets.DrawBoxSolid(new Rect(RailX - 1f, lineTop, 2f, lineBottom - lineTop), UiKit.CardBorder);
            }
            Text.Font = GameFont.Small;
            int lastDay = -1;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                int day = e.tick / GenDate.TicksPerDay;
                bool firstOfDay = day != lastDay;
                lastDay = day;
                if (!chronicleHeights.TryGetValue(e, out float h))
                {
                    h = Mathf.Max(26f, LinkText.Height(TextOf(e, comp), width - TextX - 6f) + 6f);
                    chronicleHeights[e] = h;
                }
                // Only what's on screen is drawn; the rest just takes up its space.
                if (y + h < scroll.y || y > scroll.y + outRect.height)
                {
                    y += h;
                    continue;
                }
                var row = new Rect(0f, y, width, h);
                UiKit.Zebra(row, i);
                if (firstOfDay)
                {
                    if (i > 0) Widgets.DrawBoxSolid(new Rect(0f, y, width, 1f), UiKit.WithAlpha(UiKit.Ink, 0.06f));
                    UiKit.LabelFit(new Rect(4f, y + 3f, DateW - 6f, 20f), MainTabWindow_Council.DayString(e.tick), UiKit.Faint, GameFont.Tiny);
                }
                // The kind's colour on its icon, beside the words; the words in ink (faint for what came to nothing).
                var color = UiKit.ChronicleColor(e.kind);
                UiKit.Badge(new Rect(RailX - 10f, y + 3f, 20f, 20f), Icons.For(e.kind), color);
                LinkText.Draw(new Rect(TextX, y + 3f, width - TextX - 6f, h - 6f), TextOf(e, comp), color == UiKit.Faint ? UiKit.Faint : UiKit.Ink);
                GUI.color = UiKit.Ink;
                y += h;
            }
            height = Mathf.Max(y + 10f, 100f);
            Widgets.EndScrollView();
        }

        // Each entry's text with the colonists it names as links to their cards (the one it is about first), made once per entry.
        private static readonly Dictionary<ChronicleEntry, string> marked = new Dictionary<ChronicleEntry, string>();
        private static readonly List<KeyValuePair<string, string>> names = new List<KeyValuePair<string, string>>();

        /// <summary>An entry's text with the colony's people it names as links (the Chronicle, the Wishes page's earlier wishes).</summary>
        internal static string TextOf(ChronicleEntry e, CouncilGameComponent comp)
        {
            if (marked.TryGetValue(e, out var s)) return s;
            s = e.text;
            if (!s.NullOrEmpty())
            {
                var colonists = Colonists(comp);
                names.Clear();
                if (!e.pawnId.NullOrEmpty() && colonists.TryGetValue(e.pawnId, out var about)) Add(about);
                foreach (var p in colonists.Values)
                    if (s.IndexOf(p.LabelShort, System.StringComparison.Ordinal) >= 0)
                        Add(p);
                if (names.Count > 0) s = LinkTextMath.MarkNames(s, names);
            }
            if (marked.Count > 3000) marked.Clear();
            marked[e] = s;
            return s;
        }

        private static void Add(Pawn p)
        {
            var thing = ThingRefs.Of(p);
            if (thing.IsNone || p.LabelShort.NullOrEmpty()) return;
            foreach (var kv in names)
                if (kv.Key == p.LabelShort)
                    return;
            names.Add(new KeyValuePair<string, string>(p.LabelShort, thing.ToString()));
        }

        /// <summary>The colony's people by their ids, found again every few seconds.</summary>
        private static Dictionary<string, Pawn> Colonists(CouncilGameComponent comp) =>
            UiCache.Get(comp.chronicle, 94, () =>
            {
                var d = new Dictionary<string, Pawn>();
                foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists) d[p.ThingID] = p;
                return d;
            }, 3f);

        private static List<ChronicleEntry> Entries(CouncilGameComponent comp)
        {
            // The chronicle is capped, so its length alone doesn't tell when it changed: its newest entry does.
            var last = comp.chronicle.Count > 0 ? comp.chronicle[comp.chronicle.Count - 1] : null;
            if (entriesSource == comp.chronicle && entriesCount == comp.chronicle.Count && entriesLast == last && entriesFilter == chronicleFilter) return entries;
            entries.Clear();
            for (int i = comp.chronicle.Count - 1; i >= 0; i--)
                if (Matches(comp.chronicle[i].kind, chronicleFilter)) entries.Add(comp.chronicle[i]);
            entriesSource = comp.chronicle;
            entriesCount = comp.chronicle.Count;
            entriesLast = last;
            entriesFilter = chronicleFilter;
            return entries;
        }

        private static Texture2D FilterIcon(ChronicleFilter f)
        {
            switch (f)
            {
                case ChronicleFilter.Wishes: return Icons.Wishes;
                case ChronicleFilter.Council: return Icons.Council;
                case ChronicleFilter.People: return Icons.Bonds;
                case ChronicleFilter.Strangers: return Icons.Visitor;
                case ChronicleFilter.World: return Icons.WorldEvent;
                case ChronicleFilter.Dreams: return Icons.Dream;
                case ChronicleFilter.Journals: return Icons.Journal;
                case ChronicleFilter.Losses: return Icons.Death;
                default: return Icons.Chronicle;
            }
        }

        private static bool Matches(ChronicleKind k, ChronicleFilter f)
        {
            switch (f)
            {
                case ChronicleFilter.Wishes:
                    return k == ChronicleKind.WishMade || k == ChronicleKind.WishGranted || k == ChronicleKind.WishExpired || k == ChronicleKind.WishDismissed
                           || k == ChronicleKind.Petition || k == ChronicleKind.PersonalTouch;
                case ChronicleFilter.Council:
                    return k == ChronicleKind.Meeting || k == ChronicleKind.AmbitionChosen || k == ChronicleKind.AmbitionAchieved || k == ChronicleKind.AmbitionFailed
                           || k == ChronicleKind.AmbitionAbandoned || k == ChronicleKind.Council;
                case ChronicleFilter.People:
                    return k == ChronicleKind.Bond || k == ChronicleKind.Life || k == ChronicleKind.Secret;
                case ChronicleFilter.Strangers:
                    return k == ChronicleKind.Stranger;
                case ChronicleFilter.World:
                    return k == ChronicleKind.World;
                case ChronicleFilter.Dreams:
                    return k == ChronicleKind.Dream;
                case ChronicleFilter.Losses:
                    return k == ChronicleKind.Loss;
                case ChronicleFilter.Journals:
                    // The journals themselves show (UI/HistoryPanels.cs), not lines of the chronicle.
                    return false;
                default:
                    return true;
            }
        }
    }
}
