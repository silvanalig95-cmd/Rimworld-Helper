using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's stock in a panel of its own, movable like the others: the game's own counts, as its resource list
    /// shows them (map.resourceCounter: what lies in storage), with the game's item icons. Four styles to switch
    /// between, each remembered with the panel: classic (icon and count, like the game's list, on the far left where
    /// that list is), grouped (the game's categories as folding headers with their totals), an icon grid (tiles that
    /// wrap, for a strip along an edge), and essentials (meals and days of food, medicine, silver, steel, components,
    /// wood and whatever is pinned, large and calm).
    ///
    /// In every style: items pinned to the top (right-click an item), an optional search box, items at zero left out
    /// (or shown, with what ran out since yesterday), the order by category, name, count or change, and the change
    /// since about the same hour yesterday as a small +/- (from the stock snapshots <see cref="HudComponent"/> keeps
    /// while this panel is on). Hover an item for its name, description and where it's stored; click it to go to its
    /// stacks, one after another with each click, as the game's alerts do.
    ///
    /// While it shows, the game's own resource list steps aside (<see cref="Patch_HudGameResources"/>, a setting on by
    /// default): only its drawing; the counts are the game's either way. Gathered at most twice a second.
    /// </summary>
    public class HudInventory : HudPanel
    {
        public const string StyleClassic = "classic", StyleGrouped = "grouped", StyleGrid = "grid", StyleEssentials = "essentials";
        public static readonly string[] Styles = { StyleClassic, StyleGrouped, StyleGrid, StyleEssentials };

        public const string PartChange = "change", PartZero = "zero", PartSearch = "search", PartLabels = "labels";
        public static readonly string[] Parts = { PartChange, PartLabels, PartZero, PartSearch };

        /// <summary>The inventory's orders, in its menus.</summary>
        public static readonly HudSort[] Sorts = { HudSort.Category, HudSort.Name, HudSort.Count, HudSort.Change };

        private sealed class Item
        {
            public ThingDef def;
            public StockEntry e;
            /// <summary>For the essentials: a whole category (meals, medicine) instead of one item.</summary>
            public ThingCategoryDef cat;
            public string value;
        }

        private sealed class Group
        {
            public string key, label;
            public int total;
            public Texture2D icon;
            public bool folded;
            public readonly List<Item> items = new List<Item>();
        }

        private readonly List<Item> shown = new List<Item>();
        private readonly List<Group> groups = new List<Group>();
        private readonly List<StockEntry> entries = new List<StockEntry>();
        private readonly Dictionary<string, ThingDef> defs = new Dictionary<string, ThingDef>();
        private readonly Dictionary<string, int> cycle = new Dictionary<string, int>();
        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private bool searchFocused, unfocusSearch;
        private int hidden;
        private string moreText, emptyText;
        private bool readOk;
        private Map map;

        private static List<ThingCategoryDef> roots;
        private static readonly Dictionary<ThingDef, int> groupOf = new Dictionary<ThingDef, int>();
        private static readonly Dictionary<ThingDef, (float at, string text)> tips = new Dictionary<ThingDef, (float, string)>();
        private static ThingCategoryDef meals;

        public HudInventory() : base(HudLayout.Inventory)
        {
        }

        /// <summary>The stock snapshots are kept while this panel is on (they give the change since yesterday).</summary>
        public static bool Keeping
        {
            get
            {
                var s = ColonyCouncilMod.Settings;
                if (s == null || HudManager.Failed(HudLayout.Inventory)) return false;
                var st = HudLayout.State(HudLayout.Inventory);
                return st.on && st.Shows(PartChange);
            }
        }

        /// <summary>The panel reads the game's counts (so the game's own list may step aside).</summary>
        public bool ReadOk => readOk;

        public override bool KeepsKeyboard => searchFocused;

        private string Style => Styles.Contains(St.style) ? St.style : StyleClassic;

        // ---------------- Gathering ----------------

        /// <summary>The game's resource list's roots (food, manufactured, raw resources, items, weapons, apparel...), in its order.</summary>
        private static List<ThingCategoryDef> Roots
        {
            get
            {
                if (roots == null)
                {
                    roots = DefDatabase<ThingCategoryDef>.AllDefsListForReading.Where(c => c != null && c.resourceReadoutRoot).ToList();
                    meals = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("FoodMeals");
                }
                return roots;
            }
        }

        private static int GroupOf(ThingDef def)
        {
            if (groupOf.TryGetValue(def, out int g)) return g;
            var list = Roots;
            g = list.Count;
            for (int i = 0; i < list.Count; i++)
                if (list[i].ContainedInThisOrDescendant(def))
                {
                    g = i;
                    break;
                }
            groupOf[def] = g;
            return g;
        }

        protected override void Refresh()
        {
            shown.Clear();
            groups.Clear();
            entries.Clear();
            moreText = null;
            hidden = 0;
            var st = St;
            map = HudVitals.MapFor();
            readOk = map?.resourceCounter != null;
            emptyText = HudKit.T(readOk ? "CC_Pnl_I_Empty" : "CC_Pnl_NoMap");
            if (!readOk) return;
            int hour = (Find.TickManager?.TicksGame ?? 0) / GenDate.TicksPerHour;
            var yesterday = st.Shows(PartChange) ? HudComponent.Instance?.Yesterday(map, hour) : null;
            bool zeros = st.Shows(PartZero);
            bool searching = st.Shows(PartSearch) && search.filter.Active;
            int order = 0;
            foreach (var kv in map.resourceCounter.AllCountedAmounts)
            {
                var def = kv.Key;
                if (def == null) continue;
                order++;
                int count = kv.Value;
                bool pinned = st.pins.Contains(def.defName);
                var e = new StockEntry
                {
                    key = def.defName, label = def.label ?? def.defName, count = count, pinned = pinned, order = order, group = GroupOf(def),
                    hasChange = yesterday != null
                };
                if (e.hasChange) e.change = HudInventoryMath.Change(count, yesterday, def.defName);
                // At zero only when asked: the game's always-shown ones, and what ran out since yesterday.
                bool show = count > 0 || pinned || (zeros && (def.resourceReadoutAlwaysShow || (e.hasChange && e.change != 0)));
                if (!show) continue;
                if (searching && !search.filter.Matches(def)) continue;
                defs[def.defName] = def;
                entries.Add(e);
            }
            HudInventoryMath.Sort(entries, st.sort);
            if (Style == StyleEssentials)
            {
                Essentials(st, yesterday, hour);
                return;
            }
            // The rows' limit: pinned ones always, then the first of the rest in the panel's order.
            int room = st.maxRows <= 0 ? int.MaxValue : st.maxRows;
            int taken = 0;
            foreach (var e in entries)
            {
                if (!e.pinned && taken >= room)
                {
                    hidden++;
                    continue;
                }
                if (!e.pinned) taken++;
                shown.Add(new Item { def = defs[e.key], e = e });
            }
            if (Style == StyleGrouped) GroupRows(st);
            if (hidden > 0) moreText = "CC_Pnl_AndMore".Translate(hidden).Resolve();
            if (shown.Count == 0 && searching) emptyText = HudKit.T("CC_Pnl_I_NoMatch");
        }

        /// <summary>The grouped style: a pinned group, then the game's categories in its order, each folded or not.</summary>
        private void GroupRows(HudPanelState st)
        {
            var byGroup = new Dictionary<int, Group>();
            Group pinned = null;
            foreach (var it in shown)
            {
                Group g;
                if (it.e.pinned)
                {
                    pinned ??= new Group { key = "pinned", label = HudKit.T("CC_Pnl_I_Pinned"), total = -1 };
                    g = pinned;
                }
                else if (!byGroup.TryGetValue(it.e.group, out g))
                {
                    var cat = it.e.group < Roots.Count ? Roots[it.e.group] : null;
                    g = new Group
                    {
                        key = cat?.defName ?? "other", label = cat != null ? (string)cat.LabelCap : HudKit.T("CC_Pnl_I_Other"), icon = cat?.icon,
                        total = cat != null ? map.resourceCounter.GetCountIn(cat) : -1
                    };
                    byGroup[it.e.group] = g;
                }
                g.items.Add(it);
            }
            if (pinned != null) groups.Add(pinned);
            foreach (var kv in byGroup.OrderBy(k => k.Key)) groups.Add(kv.Value);
            foreach (var g in groups) g.folded = st.folds.Contains(g.key);
        }

        /// <summary>What most players watch: meals and days of food, medicine, silver, steel, components, wood, then the pinned.</summary>
        private void Essentials(HudPanelState st, Dictionary<string, int> yesterday, int hour)
        {
            var rc = map.resourceCounter;
            bool zeros = st.Shows(PartZero);
            void Category(ThingCategoryDef cat, ThingDef iconOf, string value)
            {
                if (cat == null) return;
                int count = rc.GetCountIn(cat);
                var e = new StockEntry { key = "cat:" + cat.defName, label = cat.LabelCap, count = count, hasChange = yesterday != null };
                if (e.hasChange)
                {
                    int was = 0;
                    foreach (var kv in yesterday)
                    {
                        var d = DefDatabase<ThingDef>.GetNamedSilentFail(kv.Key);
                        if (d != null && cat.ContainedInThisOrDescendant(d)) was += kv.Value;
                    }
                    e.change = count - was;
                }
                shown.Add(new Item { def = iconOf, cat = cat, e = e, value = value });
            }
            void One(ThingDef def)
            {
                if (def == null || shown.Any(i => i.def == def && i.cat == null)) return;
                int count = rc.GetCount(def);
                bool pinned = st.pins.Contains(def.defName);
                if (count <= 0 && !pinned && !zeros) return;
                var e = new StockEntry { key = def.defName, label = def.label, count = count, pinned = pinned, hasChange = yesterday != null };
                if (e.hasChange) e.change = HudInventoryMath.Change(count, yesterday, def.defName);
                defs[def.defName] = def;
                shown.Add(new Item { def = def, e = e });
            }
            var facts = MapFacts.Of(map);
            float days = facts.FoodDays;
            string food = days >= 900f ? null : "CC_Pnl_I_FoodDays".Translate(days < 10f ? days.ToString("0.#") : days.ToString("0")).Resolve();
            if (meals == null) _ = Roots;
            Category(meals, ThingDefOf.MealSimple, food);
            Category(ThingCategoryDefOf.Medicine, ThingDefOf.MedicineIndustrial, null);
            One(ThingDefOf.Silver);
            One(ThingDefOf.Steel);
            One(ThingDefOf.ComponentIndustrial);
            One(ThingDefOf.WoodLog);
            foreach (var e in entries)
                if (e.pinned && defs.TryGetValue(e.key, out var d))
                    One(d);
        }

        // ---------------- Sizes ----------------

        private float RowH(float s) => Mathf.Round((Style == StyleEssentials ? 26f : 20f) * s);
        private float HeadH(float s) => Mathf.Round(19f * s);
        private float TileW(float s) => Mathf.Round(38f * s);
        private float TileH(float s) => Mathf.Round(40f * s);
        private float SearchH(float s) => St.Shows(PartSearch) ? Mathf.Round(26f * s) : 0f;
        private bool Labels => Style != StyleClassic || St.Shows(PartLabels);

        private float Width(float s)
        {
            switch (Style)
            {
                case StyleGrid:
                    HudInventoryMath.GridSize(Math.Max(1, shown.Count), St.columns, TileW(s), TileH(s), 2f, out float w, out _);
                    return Mathf.Max(Mathf.Round(130f * s), w + Mathf.Round(8f * s));
                case StyleClassic:
                    return Mathf.Round((Labels ? 196f : 126f) * s);
                default:
                    return Mathf.Round(214f * s);
            }
        }

        protected override Vector2 Measure(float s)
        {
            float pad = Mathf.Round(4f * s);
            float top = SearchH(s);
            float more = moreText != null ? Mathf.Round(16f * s) : 0f;
            float w = Width(s);
            if (shown.Count == 0) return new Vector2(Mathf.Max(w, Mathf.Round(150f * s)), pad * 2f + top + Mathf.Round(20f * s));
            float h;
            switch (Style)
            {
                case StyleGrid:
                    HudInventoryMath.GridSize(shown.Count, St.columns, TileW(s), TileH(s), 2f, out _, out h);
                    break;
                case StyleGrouped:
                    h = 0f;
                    foreach (var g in groups) h += HeadH(s) + (g.folded ? 0f : g.items.Count * RowH(s));
                    break;
                default:
                    h = shown.Count * RowH(s);
                    break;
            }
            return new Vector2(w, pad * 2f + top + h + more);
        }

        // ---------------- Drawing ----------------

        protected override void DrawBody(Rect r, float s)
        {
            float pad = Mathf.Round(4f * s);
            float y = r.y + pad + SearchH(s);
            if (shown.Count == 0)
            {
                HudKit.Label(new Rect(r.x + pad, y, r.width - pad * 2f, Mathf.Round(20f * s)), emptyText, UiKit.Faint, HudKit.Minor(s));
                return;
            }
            switch (Style)
            {
                case StyleGrid:
                    y = DrawGrid(r, y, s);
                    break;
                case StyleGrouped:
                    y = DrawGrouped(r, y, s);
                    break;
                default:
                    for (int i = 0; i < shown.Count; i++) DrawRow(new Rect(r.x + 2f, y + i * RowH(s), r.width - 4f, RowH(s)), shown[i], s, i);
                    y += shown.Count * RowH(s);
                    break;
            }
            if (moreText != null)
            {
                var mr = new Rect(r.x + pad, y, r.width - pad * 2f, Mathf.Round(16f * s));
                HudKit.Label(mr, moreText, UiKit.Faint, GameFont.Tiny);
                Tip(mr, HudKit.T("CC_Pnl_MoreLinesTip"));
            }
        }

        private static readonly Color Gain = new Color(0.55f, 0.8f, 0.55f);
        private static readonly Color Loss = new Color(0.92f, 0.62f, 0.45f);

        private void Icon(Rect r, Item it)
        {
            if (it.def == null || Event.current.type != EventType.Repaint) return;
            try
            {
                Widgets.ThingIcon(r, it.def);
            }
            catch (Exception)
            {
                if (it.def.uiIcon != null)
                {
                    GUI.color = it.def.uiIconColor;
                    GUI.DrawTexture(r, it.def.uiIcon, ScaleMode.ScaleToFit);
                    GUI.color = Color.white;
                }
            }
        }

        /// <summary>A row: the icon, the name (but in the classic look), the count and the change since yesterday.</summary>
        private void DrawRow(Rect rr, Item it, float s, int index)
        {
            bool big = Style == StyleEssentials;
            if (it.e.pinned) Widgets.DrawBoxSolid(rr, UiKit.WithAlpha(UiKit.Gold, 0.07f));
            else UiKit.Zebra(rr, index);
            if (Over(rr)) Widgets.DrawBoxSolid(rr, UiKit.CardHover);
            float icon = Mathf.Round((big ? 22f : 18f) * s);
            float x = rr.x + Mathf.Round(3f * s);
            Icon(new Rect(x, rr.y + (rr.height - icon) / 2f, icon, icon), it);
            x += icon + Mathf.Round(5f * s);
            float right = rr.xMax - Mathf.Round(3f * s);
            var font = big ? HudKit.Main(Mathf.Max(s, 1f)) : HudKit.Main(s);
            if (St.Shows(PartChange) && it.e.hasChange && it.e.change != 0)
            {
                string ch = HudInventoryMath.Signed(it.e.change);
                float cw = Mathf.Round(34f * s);
                HudKit.Label(new Rect(right - cw, rr.y, cw, rr.height), ch, it.e.change > 0 ? Gain : Loss, GameFont.Tiny, TextAnchor.MiddleRight);
                right -= cw + 2f;
            }
            else if (St.Shows(PartChange) && Style == StyleClassic) right -= Mathf.Round(36f * s);
            string count = it.e.count.ToString();
            if (Labels)
            {
                float vw = it.value.NullOrEmpty() ? 0f : HudKit.Width(it.value, GameFont.Tiny) + 6f;
                float nw = HudKit.Width(count, font) + 4f;
                HudKit.Label(new Rect(right - nw, rr.y, nw, rr.height), count, it.e.count > 0 ? Color.white : UiKit.Dim, font, TextAnchor.MiddleRight);
                right -= nw + 4f;
                if (vw > 0f)
                {
                    HudKit.Label(new Rect(right - vw, rr.y, vw, rr.height), it.value, UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                    right -= vw;
                }
                HudKit.Label(new Rect(x, rr.y, Mathf.Max(0f, right - x), rr.height), it.e.label.CapitalizeFirst(), it.e.pinned ? UiKit.Gold : UiKit.Faint,
                    big ? font : HudKit.Minor(s));
            }
            else HudKit.Label(new Rect(x, rr.y, Mathf.Max(0f, right - x), rr.height), count, it.e.count > 0 ? Color.white : UiKit.Dim, font);
            Interact(rr, it);
        }

        private float DrawGrouped(Rect r, float y, float s)
        {
            float hh = HeadH(s), rh = RowH(s);
            int index = 0;
            foreach (var g in groups)
            {
                var head = new Rect(r.x + 2f, y, r.width - 4f, hh);
                y += hh;
                Widgets.DrawBoxSolid(head, new Color(1f, 1f, 1f, 0.05f));
                if (Over(head)) Widgets.DrawBoxSolid(head, UiKit.CardHover);
                float x = head.x + Mathf.Round(3f * s);
                float a = Mathf.Round(9f * s);
                var arrow = g.folded ? TexButton.Reveal : TexButton.Collapse;
                if (arrow != null && Event.current.type == EventType.Repaint)
                {
                    GUI.color = UiKit.Faint;
                    GUI.DrawTexture(new Rect(x, head.y + (hh - a) / 2f, a, a), arrow);
                    GUI.color = Color.white;
                }
                x += a + Mathf.Round(4f * s);
                if (g.icon != null)
                {
                    float ic = Mathf.Round(13f * s);
                    UiKit.Icon(new Rect(x, head.y + (hh - ic) / 2f, ic, ic), g.icon, UiKit.HeaderColor);
                    x += ic + Mathf.Round(4f * s);
                }
                string total = g.total >= 0 ? g.total.ToString() : g.items.Count.ToString();
                float tw = HudKit.Width(total, GameFont.Tiny) + 4f;
                HudKit.Label(new Rect(head.xMax - tw - 3f, head.y, tw, hh), total, UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                HudKit.Label(new Rect(x, head.y, Mathf.Max(0f, head.xMax - tw - 6f - x), hh), g.label, UiKit.HeaderColor, GameFont.Tiny);
                var group = g;
                Tip(head, HudKit.T(g.folded ? "CC_Pnl_I_UnfoldTip" : "CC_Pnl_I_FoldTip"));
                if (Clicked(head))
                {
                    if (Event.current.button == 1) OpenMenu();
                    else
                    {
                        var st = St;
                        if (!st.folds.Remove(group.key)) st.folds.Add(group.key);
                        MarkDirty();
                        HudLayout.Save();
                        SoundDefOf.Click.PlayOneShotOnCamera();
                    }
                }
                if (g.folded) continue;
                foreach (var it in g.items)
                {
                    DrawRow(new Rect(r.x + 2f + Mathf.Round(6f * s), y, r.width - 4f - Mathf.Round(6f * s), rh), it, s, index++);
                    y += rh;
                }
            }
            return y;
        }

        private float DrawGrid(Rect r, float y, float s)
        {
            float tw = TileW(s), th = TileH(s), gap = 2f;
            float x0 = r.x + Mathf.Round(4f * s);
            int cols = Mathf.Max(1, Mathf.Min(St.columns, shown.Count));
            for (int i = 0; i < shown.Count; i++)
            {
                var it = shown[i];
                HudInventoryMath.GridCell(i, cols, tw, th, gap, out float cx, out float cy);
                var tile = new Rect(x0 + cx, y + cy, tw, th);
                Widgets.DrawBoxSolid(tile, it.e.pinned ? UiKit.WithAlpha(UiKit.Gold, 0.1f) : new Color(1f, 1f, 1f, 0.04f));
                if (Over(tile)) Widgets.DrawBoxSolid(tile, UiKit.CardHover);
                float ic = Mathf.Round(24f * s);
                Icon(new Rect(tile.x + (tw - ic) / 2f, tile.y + Mathf.Round(2f * s), ic, ic), it);
                var low = new Rect(tile.x, tile.yMax - Mathf.Round(15f * s), tw, Mathf.Round(15f * s));
                HudKit.Label(low, HudInventoryMath.ShortCount(it.e.count), it.e.count > 0 ? Color.white : UiKit.Dim, GameFont.Tiny, TextAnchor.MiddleCenter);
                if (St.Shows(PartChange) && it.e.hasChange && it.e.change != 0 && Event.current.type == EventType.Repaint)
                {
                    // A small mark in the corner: up or down since yesterday.
                    var c = it.e.change > 0 ? Gain : Loss;
                    float m = Mathf.Max(3f, Mathf.Round(4f * s));
                    Widgets.DrawBoxSolid(new Rect(tile.xMax - m - 2f, tile.y + 2f, m, m), c);
                }
                Interact(tile, it);
            }
            HudInventoryMath.GridSize(shown.Count, cols, tw, th, gap, out _, out float h);
            return y + h;
        }

        /// <summary>An item's tooltip, its click (go to its stacks, one after another) and its right-click (pin, unpin, the menu).</summary>
        private void Interact(Rect r, Item it)
        {
            var item = it;
            Tip(r, () => TipFor(item), Gen.HashCombineInt(0x1A7E40, (it.cat?.shortHash ?? 0) ^ (it.def?.shortHash ?? 0)));
            if (!Clicked(r)) return;
            if (Event.current.button == 1) ItemMenu(item);
            else GoTo(item);
        }

        // ---------------- Hover and click ----------------

        /// <summary>The things to go to: the item's stacks in storage (or anywhere on the map), the same order each time.</summary>
        private List<Thing> StacksOf(Item it)
        {
            var list = new List<Thing>();
            if (map == null) return list;
            if (it.cat != null)
            {
                foreach (var d in it.cat.DescendantThingDefs)
                    list.AddRange(map.listerThings.ThingsOfDef(d).Where(t => t.Spawned && t.IsInAnyStorage()));
            }
            else if (it.def != null)
            {
                list.AddRange(map.listerThings.ThingsOfDef(it.def).Where(t => t.Spawned && t.IsInAnyStorage()));
                if (list.Count == 0) list.AddRange(map.listerThings.ThingsOfDef(it.def).Where(t => t.Spawned));
            }
            list.Sort((a, b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
            return list;
        }

        private void GoTo(Item it)
        {
            try
            {
                var stacks = StacksOf(it);
                if (stacks.Count == 0) return;
                cycle.TryGetValue(it.e.key, out int i);
                i = (i + 1) % stacks.Count;
                cycle[it.e.key] = i;
                CameraJumper.TryJumpAndSelect(stacks[i]);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("inventory: go to the stacks", e);
            }
        }

        /// <summary>The item's name and description, where it's kept (stacks in each storage), and the change since yesterday; kept half a second.</summary>
        private string TipFor(Item it)
        {
            var key = it.def;
            float now = Time.realtimeSinceStartup;
            if (key != null && it.cat == null && tips.TryGetValue(key, out var c) && now - c.at < 0.5f && now >= c.at) return c.text;
            var sb = new StringBuilder();
            sb.Append((it.cat != null ? (string)it.cat.LabelCap : it.def?.LabelCap.ToString() ?? it.e.label).Colorize(UiKit.HeaderColor));
            sb.Append(": ").Append(it.e.count);
            if (it.cat == null && !(it.def?.description).NullOrEmpty())
            {
                string d = it.def.description.Trim();
                int cut = d.IndexOf('\n');
                if (cut > 0) d = d.Substring(0, cut).Trim();
                if (d.Length > 280) d = d.Substring(0, 277).TrimEnd() + "...";
                sb.Append("\n").Append(d.Colorize(ColoredText.SubtleGrayColor));
            }
            if (!it.value.NullOrEmpty()) sb.Append("\n").Append(it.value);
            var stacks = StacksOf(it);
            if (stacks.Count > 0)
            {
                var where = new Dictionary<string, int>();
                foreach (var t in stacks)
                {
                    string place;
                    try
                    {
                        place = t.GetSlotGroup()?.parent?.SlotYielderLabel();
                    }
                    catch (Exception)
                    {
                        place = null;
                    }
                    place = place.NullOrEmpty() ? HudKit.T("CC_Pnl_I_Loose") : place.CapitalizeFirst();
                    where.TryGetValue(place, out int n);
                    where[place] = n + 1;
                }
                sb.Append("\n\n").Append("CC_Pnl_I_Stacks".Translate(stacks.Count).Resolve());
                foreach (var kv in where.OrderByDescending(k => k.Value).Take(6)) sb.Append("\n  ").Append(kv.Key).Append(": ").Append(kv.Value);
                if (where.Count > 6) sb.Append("\n  ...");
            }
            if (it.e.hasChange)
                sb.Append("\n\n").Append(it.e.change == 0 ? HudKit.T("CC_Pnl_I_NoChange")
                    : "CC_Pnl_I_ChangeTip".Translate(HudInventoryMath.Signed(it.e.change)).Resolve());
            else if (St.Shows(PartChange)) sb.Append("\n\n").Append(HudKit.T("CC_Pnl_I_ChangeSoon").Colorize(ColoredText.SubtleGrayColor));
            if (it.e.pinned) sb.Append("\n").Append(HudKit.T("CC_Pnl_I_PinnedTip").Colorize(UiKit.Gold));
            string text = sb.ToString();
            if (key != null && it.cat == null)
            {
                if (tips.Count > 200) tips.Clear();
                tips[key] = (now, text);
            }
            return text;
        }

        private void ItemMenu(Item it)
        {
            var first = new List<FloatMenuOption>();
            if (it.def != null && it.cat == null)
            {
                var st = St;
                string name = it.def.defName;
                bool pinned = st.pins.Contains(name);
                first.Add(new FloatMenuOption((pinned ? "CC_Pnl_I_Unpin" : "CC_Pnl_I_Pin").Translate(it.def.label).Resolve(), () =>
                {
                    if (pinned) st.pins.Remove(name);
                    else if (!st.pins.Contains(name)) st.pins.Add(name);
                    MarkDirty();
                    HudLayout.Save();
                }));
            }
            OpenMenu(first);
        }

        // ---------------- The search box (only the window can take the keyboard) ----------------

        protected override void WindowExtras(bool repaintOnly)
        {
            if (!St.Shows(PartSearch))
            {
                searchFocused = false;
                return;
            }
            if (unfocusSearch)
            {
                unfocusSearch = false;
                if (search.CurrentlyFocused()) search.Unfocus();
            }
            float s = HudMath.ClampScale(Scale);
            float pad = Mathf.Round(4f * s);
            var body = BodyLocal;
            var r = new Rect(body.x + pad, body.y + pad, body.width - pad * 2f, Mathf.Round(24f * s));
            search.OnGUI(r, MarkDirty, MarkDirty);
            searchFocused = search.CurrentlyFocused();
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            // While the search box is typed in, the keys are its own: the camera stays, and a click elsewhere ends it.
            preventCameraMotion = searchFocused;
            if (searchFocused && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) && Find.WindowStack?.GetWindowAt(UI.MousePositionOnUIInverted) != this)
                unfocusSearch = true;
        }

        // ---------------- The bar and the menu ----------------

        private static string StyleLabel(string style) => HudKit.T("CC_Pnl_I_Style_" + style);

        protected override float HeaderButtons(Rect bar, float right)
        {
            var st = St;
            string next = Styles[(Array.IndexOf(Styles, Style) + 1) % Styles.Length];
            return BarText(bar, right, StyleLabel(Style), "CC_Pnl_I_StyleTip".Translate(StyleLabel(next)).Resolve(), () => SetStyle(st, next));
        }

        private void SetStyle(HudPanelState st, string style)
        {
            st.style = style;
            MarkDirty();
        }

        protected override void MenuOptions(List<FloatMenuOption> options)
        {
            var st = St;
            foreach (var style in Styles)
            {
                string sty = style;
                options.Add(new FloatMenuOption(StyleLabel(style), () =>
                {
                    SetStyle(st, sty);
                    HudLayout.Save();
                }, Style == style ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex, Color.white));
            }
            foreach (var sort in Sorts)
            {
                var so = sort;
                options.Add(new FloatMenuOption(HudKit.T("CC_Pnl_I_Sort_" + sort), () =>
                {
                    st.sort = so;
                    MarkDirty();
                    HudLayout.Save();
                }, st.sort == sort ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex, Color.white));
            }
            var s = ColonyCouncilMod.Settings;
            options.Add(new FloatMenuOption(HudKit.T(s.hudHideGameResources ? "CC_Pnl_I_ShowGameList" : "CC_Pnl_I_HideGameList"), () =>
            {
                s.hudHideGameResources = !s.hudHideGameResources;
                HudLayout.Save();
            }));
        }
    }
}
