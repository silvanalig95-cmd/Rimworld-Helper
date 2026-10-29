using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// "Reserve a slot for...": what may be reserved on the selected shelves, with a search box. First what's on the shelf
    /// now; then the groups (any meal, any drug...), each with "the best at hand" where its kinds can be ranked (food,
    /// medicine); then everything the shelves' storage settings allow, dimmed where the colony has none on the map. A
    /// click reserves one slot for it or takes it off again, and the window stays open to pick several; the shelf
    /// window sets how many slots each keeps.
    /// </summary>
    public class Dialog_ShelfReservePick : Window
    {
        private enum RowKind
        {
            Header,
            Item,
            Group
        }

        private class Row
        {
            public RowKind kind;
            public string label, search;
            public Texture2D icon;
            public ThingDef def;
            public ThingCategoryDef cat;
            public int depth, section;
            public bool onMap, rankable;
        }

        private readonly List<Building_Storage> shelves;
        private readonly List<Row> rows = new List<Row>();
        private List<Row> shown;
        private string query = "", shownFor;
        private Vector2 scroll;

        private const float RowH = 28f;

        public Dialog_ShelfReservePick(List<Building_Storage> shelves)
        {
            this.shelves = shelves;
            doCloseX = true;
            doCloseButton = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;
            forcePause = false;
            preventCameraMotion = false;
            Build();
        }

        public override Vector2 InitialSize => new Vector2(Mathf.Min(580f, UI.screenWidth - 40f), Mathf.Min(700f, UI.screenHeight - 40f));

        private Building_Storage First => shelves.FirstOrDefault(s => s != null && !s.Destroyed && s.Map != null);

        private void Build()
        {
            var first = First;
            if (first == null) return;
            var map = first.Map;
            var allowed = first.settings.filter.AllowedThingDefs
                .Where(d => d != null && d.EverStorable(false) && shelves.All(s => s?.settings != null && s.settings.AllowedToAccept(d)))
                .Distinct().ToList();
            var onMap = new HashSet<ThingDef>();
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
                if (t?.def != null && t.Spawned && !t.Position.Fogged(map)) onMap.Add(t.def);
            var onShelf = new HashSet<ThingDef>();
            foreach (var s in shelves)
            {
                var g = s?.GetSlotGroup();
                if (g != null)
                    foreach (var t in g.HeldThings) onShelf.Add(t.def);
            }

            Row Item(ThingDef d, int section) => new Row
            {
                kind = RowKind.Item, def = d, label = d.LabelCap.Resolve(), search = d.label.ToLowerInvariant(), icon = d.uiIcon, onMap = onMap.Contains(d),
                section = section
            };

            // On the shelf now.
            Header("CC_ShelfPick_OnShelf", 0);
            foreach (var d in onShelf.Where(allowed.Contains).OrderBy(d => d.label)) rows.Add(Item(d, 0));

            // The groups, in the game's own order, with the kinds of each the shelves may hold.
            var inGroup = new Dictionary<ThingCategoryDef, List<ThingDef>>();
            foreach (var d in allowed)
            {
                if (d.thingCategories == null) continue;
                foreach (var c0 in d.thingCategories)
                    for (var c = c0; c != null; c = c.parent)
                    {
                        if (!inGroup.TryGetValue(c, out var list)) inGroup[c] = list = new List<ThingDef>();
                        if (!list.Contains(d)) list.Add(d);
                    }
            }
            Header("CC_ShelfPick_Groups", 1);
            var seen = new HashSet<ThingCategoryDef>();
            void Walk(ThingCategoryDef c, int depth)
            {
                if (c == null || !seen.Add(c) || !inGroup.TryGetValue(c, out var defs)) return;
                if (c.parent != null)
                    rows.Add(new Row
                    {
                        kind = RowKind.Group, cat = c, label = c.LabelCap.Resolve(), search = c.label.ToLowerInvariant(), icon = c.icon, depth = depth,
                        rankable = ShelfReserves.RankableGroup(defs), section = 1
                    });
                if (c.childCategories != null)
                    foreach (var child in c.childCategories) Walk(child, c.parent == null ? 0 : depth + 1);
            }
            foreach (var root in DefDatabase<ThingCategoryDef>.AllDefsListForReading.Where(c => c.parent == null)) Walk(root, 0);

            // Everything the shelves may hold.
            Header("CC_ShelfPick_All", 2);
            foreach (var d in allowed.OrderBy(d => d.label)) rows.Add(Item(d, 2));
        }

        private void Header(string key, int section) =>
            rows.Add(new Row { kind = RowKind.Header, label = key.Translate().Resolve(), search = "", section = section });

        /// <summary>The rows matching the search (all without one); a heading only with something under it.</summary>
        private List<Row> Shown()
        {
            if (shown != null && shownFor == query) return shown;
            string q = (query ?? "").Trim().ToLowerInvariant();
            var match = rows.Where(r => r.kind != RowKind.Header && (q.Length == 0 || r.search.Contains(q))).ToList();
            shown = new List<Row>();
            foreach (var r in rows)
            {
                if (r.kind == RowKind.Header)
                {
                    if (match.Any(m => m.section == r.section)) shown.Add(r);
                }
                else if (match.Contains(r)) shown.Add(r);
            }
            shownFor = query;
            return shown;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var first = First;
            var comp = ShelfReserves.Of(first?.Map);
            if (first == null || comp == null)
            {
                Close();
                return;
            }
            float top = UiKit.PageHeader(new Rect(inRect.x, inRect.y, inRect.width - 24f, UiKit.HeaderHeight), Icons.Pin, "CC_ShelfPick_Title".Translate(),
                "CC_ShelfPick_Sub".Translate());
            float y = inRect.y + top;
            var searchRect = new Rect(inRect.x, y, inRect.width, 28f);
            Widgets.Label(new Rect(searchRect.x, searchRect.y + 3f, 70f, 24f), "CC_ShelfPick_Search".Translate());
            query = Widgets.TextField(new Rect(searchRect.x + 74f, searchRect.y, searchRect.width - 74f, 28f), query ?? "");
            y += 36f;

            var kinds = comp.KindsOf(first).ToList();
            var list = Shown();
            var outRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - y - 48f);
            var view = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(list.Count * RowH, RowH));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            if (list.Count == 0) UiKit.LabelFit(new Rect(4f, 0f, view.width - 8f, RowH), "CC_ShelfPick_NothingFound".Translate(), UiKit.Faint);
            int from = Mathf.Max(0, Mathf.FloorToInt(scroll.y / RowH) - 1);
            int to = Mathf.Min(list.Count, from + Mathf.CeilToInt(outRect.height / RowH) + 2);
            for (int i = from; i < to; i++) DrawRow(list[i], new Rect(0f, i * RowH, view.width, RowH), i, kinds);
            Widgets.EndScrollView();
        }

        private void DrawRow(Row r, Rect row, int index, List<string> kinds)
        {
            if (r.kind == RowKind.Header)
            {
                UiKit.LabelFit(new Rect(row.x + 2f, row.y + 4f, row.width - 4f, row.height - 4f), r.label, UiKit.HeaderColor);
                return;
            }
            UiKit.Zebra(row, index);
            if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
            float x = 4f + r.depth * 14f;
            if (r.icon != null) GUI.DrawTexture(new Rect(x, row.y + 2f, 24f, 24f), r.icon);
            x += 30f;
            if (r.kind == RowKind.Item)
            {
                string raw = "def:" + r.def.defName;
                bool on = kinds.Contains(raw);
                var tick = new Rect(row.xMax - 28f, row.y + 2f, 24f, 24f);
                UiKit.LabelFit(new Rect(x, row.y, row.width - x - 150f, row.height), r.label, r.onMap || on ? Color.white : UiKit.Faint);
                if (!r.onMap) UiKit.LabelFit(new Rect(row.xMax - 150f, row.y, 118f, row.height), "CC_ShelfPick_NotOnMap".Translate(), UiKit.Dim, GameFont.Tiny);
                Widgets.CheckboxDraw(tick.x, tick.y, on, false, 24f);
                if (!r.def.description.NullOrEmpty()) TooltipHandler.TipRegion(row, r.def.description);
                if (Widgets.ButtonInvisible(row)) Toggle(kinds, raw);
                return;
            }
            // A group: "any" of it, and where its kinds can be ranked, "the best at hand".
            UiKit.LabelFit(new Rect(x, row.y, row.width - x - 220f, row.height), r.label, Color.white);
            string any = "cat:" + r.cat.defName, best = "best:" + r.cat.defName;
            var anyRect = new Rect(row.xMax - 220f, row.y, 80f, row.height);
            CheckLabel(anyRect, "CC_ShelfPick_Any".Translate(), kinds.Contains(any), "CC_ShelfPick_AnyTip".Translate(r.cat.label.Named("GROUP")), () => Toggle(kinds, any));
            if (r.rankable)
            {
                var bestRect = new Rect(row.xMax - 136f, row.y, 134f, row.height);
                CheckLabel(bestRect, "CC_ShelfPick_Best".Translate(), kinds.Contains(best), "CC_ShelfPick_BestTip".Translate(r.cat.label.Named("GROUP")), () => Toggle(kinds, best));
            }
        }

        private static void CheckLabel(Rect r, string label, bool on, string tip, System.Action click)
        {
            if (Mouse.IsOver(r)) Widgets.DrawHighlight(r);
            Widgets.CheckboxDraw(r.x + 2f, r.y + 3f, on, false, 22f);
            UiKit.LabelFit(new Rect(r.x + 28f, r.y, r.width - 30f, r.height), label, Color.white, GameFont.Tiny);
            TooltipHandler.TipRegion(r, tip);
            if (Widgets.ButtonInvisible(r)) click();
        }

        /// <summary>Reserved with one slot, or taken off with all its slots (the shelf window sets how many).</summary>
        private void Toggle(List<string> kinds, string raw)
        {
            var next = kinds.ToList();
            if (next.RemoveAll(s => s == raw) == 0) next.Add(raw);
            ShelfReserves.SetAll(shelves, next);
        }
    }
}
