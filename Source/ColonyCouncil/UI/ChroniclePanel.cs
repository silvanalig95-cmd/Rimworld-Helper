using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// History → Chronicle: everything the colony has been through, newest first, as a timeline (the day on the left,
    /// an icon for each kind of entry on the line), with filters and the yearly annals.
    /// </summary>
    public static class ChroniclePanel
    {
        private static Vector2 scroll;
        private static float height = 1000f;

        private enum ChronicleFilter
        {
            All,
            Wishes,
            Council,
            People,
            Strangers,
            World,
            Dreams,
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
                    if (Widgets.ButtonText(new Rect(bx, rect.y + 6f, dw, 28f), dl))
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
                if (Widgets.ButtonText(new Rect(bx, rect.y + 6f, w, 28f), label))
                    Find.WindowStack.Add(new Dialog_MessageBox(entry.text, title: "CC_Annals_Title".Translate(entry.year)));
            }

            var list = Entries(comp);
            float top = UiKit.PageHeader(rect, Icons.Chronicle, "CC_Chronicle_Header".Translate(), "CC_UI_Sub_Chronicle".Translate(),
                "CC_UI_Stat_Entries".Translate(list.Count).Resolve(), null, rect.xMax - bx);

            // Filters: everything, or one part of colony life. Squeezed evenly when they don't all fit.
            float total = 0f;
            foreach (var f in Filters) total += UiKit.TabButtonWidth(("CC_ChronicleFilter_" + f).Translate(), FilterIcon(f)) + 4f;
            float even = (rect.width - 4f * (Filters.Length - 1)) / Filters.Length;
            float fx = rect.x;
            foreach (var f in Filters)
            {
                string label = ("CC_ChronicleFilter_" + f).Translate();
                float fw = total > rect.width ? even : UiKit.TabButtonWidth(label, FilterIcon(f));
                if (UiKit.TabButton(new Rect(fx, rect.y + top, fw, 28f), label, FilterIcon(f), f == chronicleFilter))
                {
                    chronicleFilter = f;
                    scroll = Vector2.zero;
                }
                fx += fw + 4f;
            }
            top += 36f;

            var outRect = new Rect(rect.x, rect.y + top, rect.width, rect.height - top);
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
            // The line the entries hang on, only where it's in sight.
            if (list.Count > 0)
            {
                float lineTop = Mathf.Max(0f, scroll.y), lineBottom = Mathf.Min(height - 10f, scroll.y + outRect.height);
                if (lineBottom > lineTop) Widgets.DrawBoxSolid(new Rect(RailX - 1f, lineTop, 2f, lineBottom - lineTop), new Color(1f, 1f, 1f, 0.1f));
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
                    h = Mathf.Max(26f, Text.CalcHeight(e.text, width - TextX - 6f) + 6f);
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
                    if (i > 0) Widgets.DrawBoxSolid(new Rect(0f, y, width, 1f), new Color(1f, 1f, 1f, 0.06f));
                    UiKit.LabelFit(new Rect(4f, y + 3f, DateW - 6f, 20f), MainTabWindow_Council.DayString(e.tick), UiKit.Faint, GameFont.Tiny);
                }
                var color = UiKit.ChronicleColor(e.kind);
                UiKit.Badge(new Rect(RailX - 10f, y + 3f, 20f, 20f), Icons.For(e.kind), color);
                GUI.color = color;
                Widgets.Label(new Rect(TextX, y + 3f, width - TextX - 6f, h - 6f), e.text);
                GUI.color = Color.white;
                y += h;
            }
            height = Mathf.Max(y + 10f, 100f);
            Widgets.EndScrollView();
        }

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
                default:
                    return true;
            }
        }
    }
}
