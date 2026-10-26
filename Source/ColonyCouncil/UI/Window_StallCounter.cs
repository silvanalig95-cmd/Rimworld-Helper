using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// RimMarket's colony stall sales table, Rimfolk's way (Integrations/RimMarketStall.cs): on the left the colony's
    /// goods by category, with a search and a sort, each a click away from the counter (right-click for how many); on
    /// the right only what is for sale, a card a line, with the count, the quality range, the price on a slider with what
    /// it fetches each and for all and how well it sells, and what has sold and lies on the counter; one price for every
    /// line at once; and at the foot what it all comes to and how often shoppers come at these prices. Every change goes
    /// straight to the stall through RimMarket's own sale lines. The goods are counted at most every two seconds.
    /// </summary>
    public class Window_StallCounter : Window
    {
        private readonly Thing stall;

        /// <summary>One article of the colony's, counted by quality (0 to 6) with a last slot for goods without one.</summary>
        private sealed class Good
        {
            public ThingDef def;
            public bool hasQuality;
            public readonly int[] onMap = new int[Slots];
            public readonly int[] all = new int[Slots];
            public readonly float[] worth = new float[Slots];
            public ThingCategoryDef category;
            public int onMapTotal;
            public float worthOnMap;

            public void Clear()
            {
                for (int i = 0; i < Slots; i++)
                {
                    onMap[i] = 0;
                    all[i] = 0;
                    worth[i] = 0f;
                }
                onMapTotal = 0;
                worthOnMap = 0f;
            }
        }

        private enum Sort
        {
            Worth,
            Count,
            Name
        }

        private const int Slots = 8, NoQuality = 7;
        private const float Gap = 14f, RowH = 30f, CardH = 74f, FooterH = 58f, RefreshSeconds = 2f;
        private const string FieldPrefix = "CC_StallCount_";
        private static readonly Regex Digits = new Regex("^[0-9]*$");

        private static readonly QualityRange[] Ranges =
        {
            QualityRange.All,
            new QualityRange(QualityCategory.Awful, QualityCategory.Poor),
            new QualityRange(QualityCategory.Awful, QualityCategory.Normal),
            new QualityRange(QualityCategory.Awful, QualityCategory.Good),
            new QualityRange(QualityCategory.Normal, QualityCategory.Legendary),
            new QualityRange(QualityCategory.Good, QualityCategory.Legendary),
            new QualityRange(QualityCategory.Excellent, QualityCategory.Legendary),
            new QualityRange(QualityCategory.Masterwork, QualityCategory.Legendary),
            new QualityRange(QualityCategory.Normal, QualityCategory.Normal),
            new QualityRange(QualityCategory.Good, QualityCategory.Good),
            new QualityRange(QualityCategory.Legendary, QualityCategory.Legendary)
        };

        private readonly Dictionary<ThingDef, Good> goods = new Dictionary<ThingDef, Good>();
        private readonly Dictionary<ThingDef, bool> sellable = new Dictionary<ThingDef, bool>();
        private readonly List<Good> shown = new List<Good>();
        private readonly List<(ThingCategoryDef cat, float worth)> categories = new List<(ThingCategoryDef, float)>();
        private readonly List<RimMarketStall.Line> lines = new List<RimMarketStall.Line>();
        private readonly Dictionary<ThingDef, int> held = new Dictionary<ThingDef, int>();
        private readonly Dictionary<ThingDef, string> buffers = new Dictionary<ThingDef, string>();
        private readonly QuickSearchWidget search = new QuickSearchWidget();
        private float countedAt = -99f;
        private ThingCategoryDef category;
        private static Sort sort = Sort.Worth;
        /// <summary>The price a new line starts at: the last one set here this session.</summary>
        private static float newPrice = StallCounterMath.DefaultPrice;
        private Vector2 goodsScroll, counterScroll;
        private float goodsHeight, counterHeight;
        private bool toCounterEnd;
        /// <summary>The line whose count is being typed: it's set when the box is left or Enter pressed, as in RimMarket's own table (each keystroke would take goods off the counter and back).</summary>
        private ThingDef editing;

        public override Vector2 InitialSize => new Vector2(Mathf.Min(1180f, UI.screenWidth - 40f), Mathf.Min(780f, UI.screenHeight - 60f));

        protected override float Margin => 16f;

        public Window_StallCounter(Thing stall)
        {
            this.stall = stall;
            doCloseX = true;
            absorbInputAroundWindow = true;
            forcePause = true;
            closeOnClickedOutside = false;
        }

        // ---------------- Counting ----------------

        /// <summary>The colony's goods as RimMarket's own table counts them: what's on the map, not forbidden or in the dark, and what lies on the counter.</summary>
        private void Count()
        {
            countedAt = Time.realtimeSinceStartup;
            foreach (var g in goods.Values) g.Clear();
            var map = stall.Map;
            var things = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver);
            for (int i = 0; i < things.Count; i++)
            {
                var t = things[i];
                if (!t.Spawned || t.Position.Fogged(map) || t.IsForbidden(Faction.OfPlayer)) continue;
                var def = RimMarketStall.StockDef(t);
                if (def == null || !Sellable(def) || !RimMarketStall.Tradeable(t)) continue;
                Add(GoodFor(def), t, true);
            }
            foreach (var t in RimMarketStall.Stock(stall))
            {
                var def = RimMarketStall.StockDef(t);
                if (def != null) Add(GoodFor(def), t, false);
            }
            RimMarketStall.Lines(stall, lines);
            foreach (var l in lines) GoodFor(l.def);
            // The categories with goods in them, the richest first.
            categories.Clear();
            foreach (var group in goods.Values.Where(g => g.onMapTotal > 0).GroupBy(g => g.category))
                categories.Add((group.Key, group.Sum(g => g.worthOnMap)));
            categories.Sort((a, b) => b.worth.CompareTo(a.worth));
        }

        private bool Sellable(ThingDef def)
        {
            if (!sellable.TryGetValue(def, out bool ok)) sellable[def] = ok = RimMarketStall.CanSell(def);
            return ok;
        }

        private Good GoodFor(ThingDef def)
        {
            if (goods.TryGetValue(def, out var g)) return g;
            g = new Good { def = def, hasQuality = def.HasComp(typeof(CompQuality)), category = TopCategory(def) };
            goods[def] = g;
            return g;
        }

        private static void Add(Good g, Thing t, bool onMap)
        {
            int slot = t.TryGetQuality(out var q) ? (int)q : NoQuality;
            float value = RimMarketStall.BaseValue(t) * t.stackCount;
            if (onMap)
            {
                g.onMap[slot] += t.stackCount;
                g.onMapTotal += t.stackCount;
                g.worthOnMap += value;
            }
            g.all[slot] += t.stackCount;
            g.worth[slot] += value;
        }

        /// <summary>The article's category under the root: food, manufactured, weapons, apparel...</summary>
        private static ThingCategoryDef TopCategory(ThingDef def)
        {
            var c = def.FirstThingCategory;
            while (c?.parent != null && c.parent != ThingCategoryDefOf.Root) c = c.parent;
            return c;
        }

        private static bool InRange(QualityRange r, int slot) => slot == NoQuality || r.Includes((QualityCategory)slot);

        /// <summary>How many of it the colony has for this line (its quality range), not counting what is on the counter.</summary>
        private static int Available(Good g, QualityRange r)
        {
            int n = 0;
            for (int i = 0; i < Slots; i++)
                if (InRange(r, i)) n += g.onMap[i];
            return n;
        }

        /// <summary>What one of it is worth on average in this quality range (RimMarket's own way); the base value when there's none to go by.</summary>
        private float UnitValue(Good g, QualityRange r)
        {
            int n = 0;
            float w = 0f;
            for (int i = 0; i < Slots; i++)
            {
                if (!InRange(r, i)) continue;
                n += g.all[i];
                w += g.worth[i];
            }
            return n > 0 && w > 0f ? w / n : RimMarketStall.BaseValue(g.def);
        }

        private static int PriceEach(float unitValue, float price) => Mathf.Max(1, Mathf.RoundToInt(unitValue * price));

        private void Held()
        {
            held.Clear();
            foreach (var t in RimMarketStall.Stock(stall))
            {
                var def = RimMarketStall.StockDef(t);
                if (def == null) continue;
                held.TryGetValue(def, out int n);
                held[def] = n + t.stackCount;
            }
        }

        private int HeldOf(ThingDef def) => held.TryGetValue(def, out int n) ? n : 0;

        private bool OnCounter(ThingDef def)
        {
            foreach (var l in lines)
                if (l.def == def) return true;
            return false;
        }

        // ---------------- Changes ----------------

        private void Put(Good g, int count)
        {
            var line = lines.FirstOrDefault(l => l.def == g.def);
            if (line.def != null) RimMarketStall.SetLine(stall, g.def, count, line.price, line.quality);
            else
            {
                RimMarketStall.SetLine(stall, g.def, count, newPrice, QualityRange.All);
                toCounterEnd = true;
            }
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
            Changed();
        }

        private void SetCount(RimMarketStall.Line l, int count)
        {
            if (editing == l.def)
            {
                editing = null;
                UI.UnfocusCurrentControl();
            }
            if (count == l.toSell) return;
            RimMarketStall.SetLine(stall, l.def, count, l.price, l.quality);
            Changed();
        }

        private void Changed()
        {
            RimMarketStall.Lines(stall, lines);
            countedAt = -99f;
        }

        // ---------------- Drawing ----------------

        public override void DoWindowContents(Rect inRect)
        {
            if (stall == null || stall.Destroyed || !stall.Spawned || !RimMarketStall.IsColonyStall(stall))
            {
                Close();
                return;
            }
            if (editing != null && GUI.GetNameOfFocusedControl() != FieldPrefix + editing.defName) Commit();
            if (Time.realtimeSinceStartup - countedAt > RefreshSeconds) Count();
            else RimMarketStall.Lines(stall, lines);
            Held();
            Text.Font = GameFont.Small;
            float y = inRect.y;
            int articles = lines.Sum(l => l.toSell);
            string stat = lines.Count == 0 ? null : "CC_Stall_Stat".Translate(articles, lines.Count).Resolve();
            y += UiKit.PageHeader(new Rect(inRect.x, y, inRect.width, UiKit.HeaderHeight), Icons.Trader, "CC_Stall_Title".Translate(stall.LabelCap).Resolve(),
                "CC_Stall_Sub".Translate().Resolve(), stat, UiKit.Gold, 28f) + 6f;
            var body = new Rect(inRect.x, y, inRect.width, inRect.yMax - FooterH - y);
            float leftW = Mathf.Floor((body.width - Gap) * 0.42f);
            Goods(new Rect(body.x, body.y, leftW, body.height));
            Counter(new Rect(body.x + leftW + Gap, body.y, body.width - leftW - Gap, body.height));
            Footer(new Rect(inRect.x, inRect.yMax - FooterH + 8f, inRect.width, FooterH - 8f));
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        // The colony's goods, by category, a click from the counter.
        private void Goods(Rect r)
        {
            float y = r.y;
            y += UiKit.SectionTitle(r.x, y, r.width, "CC_Stall_Goods".Translate(), Icons.Table, null,
                "CC_Stall_GoodsCount".Translate(goods.Values.Count(g => g.onMapTotal > 0)).Resolve()) + 4f;
            search.OnGUI(new Rect(r.x, y, r.width - 138f, 24f));
            var sortRect = new Rect(r.xMax - 132f, y, 132f, 24f);
            if (Widgets.ButtonText(sortRect, ("CC_Stall_Sort_" + sort).Translate()))
            {
                sort = sort == Sort.Worth ? Sort.Count : sort == Sort.Count ? Sort.Name : Sort.Worth;
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
            TooltipHandler.TipRegion(sortRect, "CC_Stall_SortTip".Translate());
            y += 30f;
            // The categories, as small tabs that wrap.
            float x = r.x;
            float tabH = 24f;
            void Tab(ThingCategoryDef cat, string label)
            {
                float w = Mathf.Min(r.width, Mathf.Max(54f, UiKit.TabButtonWidth(label, null) - 8f));
                if (x + w > r.xMax)
                {
                    x = r.x;
                    y += tabH + 3f;
                }
                if (UiKit.TabButton(new Rect(x, y, w, tabH), label, null, category == cat) && category != cat)
                {
                    category = cat;
                    goodsScroll = Vector2.zero;
                }
                x += w + 3f;
            }
            Tab(null, "CC_Stall_AllGoods".Translate());
            foreach (var (cat, _) in categories)
                if (cat != null) Tab(cat, cat.LabelCap);
            y += tabH + 6f;

            shown.Clear();
            foreach (var g in goods.Values)
            {
                if (g.onMapTotal <= 0) continue;
                if (category != null && g.category != category) continue;
                if (search.filter.Active && !search.filter.Matches(g.def)) continue;
                shown.Add(g);
            }
            switch (sort)
            {
                case Sort.Count: shown.Sort((a, b) => b.onMapTotal.CompareTo(a.onMapTotal)); break;
                case Sort.Name: shown.Sort((a, b) => string.Compare(a.def.label, b.def.label, System.StringComparison.CurrentCultureIgnoreCase)); break;
                default: shown.Sort((a, b) => b.worthOnMap.CompareTo(a.worthOnMap)); break;
            }
            var outer = new Rect(r.x, y, r.width, r.yMax - y);
            if (shown.Count == 0)
            {
                UiKit.EmptyState(outer, Icons.Table, "CC_Stall_NoGoods".Translate());
                return;
            }
            goodsHeight = shown.Count * RowH;
            var view = new Rect(0f, 0f, outer.width - 16f, Mathf.Max(goodsHeight, outer.height - 1f));
            Widgets.BeginScrollView(outer, ref goodsScroll, view);
            for (int i = 0; i < shown.Count; i++)
            {
                float ry = i * RowH;
                if (ry + RowH < goodsScroll.y || ry > goodsScroll.y + outer.height) continue;
                GoodRow(new Rect(0f, ry, view.width, RowH), shown[i], i);
            }
            Widgets.EndScrollView();
        }

        private void GoodRow(Rect r, Good g, int index)
        {
            UiKit.Zebra(r, index);
            Widgets.DrawHighlightIfMouseover(r);
            bool on = OnCounter(g.def);
            Widgets.ThingIcon(new Rect(r.x + 4f, r.y + 3f, 24f, 24f), g.def);
            float right = r.xMax - 4f;
            // What it's all worth, and how many.
            string worth = Mathf.RoundToInt(g.worthOnMap).ToStringCached();
            Text.Font = GameFont.Small;
            float ww = Text.CalcSize(worth).x + 4f;
            UiKit.LabelFit(new Rect(right - ww, r.y, ww, r.height), worth, UiKit.Faint, GameFont.Small, TextAnchor.MiddleRight);
            Widgets.ThingIcon(new Rect(right - ww - 20f, r.y + 6f, 18f, 18f), ThingDefOf.Silver);
            right -= ww + 26f;
            string count = "×" + g.onMapTotal.ToStringCached();
            float cw = Text.CalcSize(count).x + 6f;
            UiKit.LabelFit(new Rect(right - cw, r.y, cw, r.height), count, Color.white, GameFont.Small, TextAnchor.MiddleRight);
            right -= cw + 6f;
            if (on)
            {
                string pill = "CC_Stall_OnCounterPill".Translate();
                float pw = UiKit.PillWidth(pill);
                UiKit.Pill(right - pw, r.y + (r.height - UiKit.PillHeight) / 2f, pill, UiKit.Good);
                right -= pw + 6f;
            }
            UiKit.LabelFit(new Rect(r.x + 34f, r.y, Mathf.Max(0f, right - r.x - 34f), r.height), g.def.LabelCap, on ? UiKit.Faint : Color.white);
            string tip = (on ? "CC_Stall_GoodTipOn" : "CC_Stall_GoodTip").Translate(g.onMapTotal).Resolve();
            if (g.hasQuality) tip += "\n\n" + Breakdown(g);
            TooltipHandler.TipRegion(r, tip);
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 1 && Mouse.IsOver(r))
            {
                AmountMenu(g);
                e.Use();
            }
            else if (Widgets.ButtonInvisible(r, false)) Put(g, Most(g));
        }

        /// <summary>All of an article there is for its line: in the line's quality range when it's already for sale, with what lies on the counter.</summary>
        private int Most(Good g)
        {
            var line = lines.FirstOrDefault(l => l.def == g.def);
            return (line.def != null ? Available(g, line.quality) : g.onMapTotal) + HeldOf(g.def);
        }

        private void AmountMenu(Good g)
        {
            int have = Most(g);
            var options = new List<FloatMenuOption>();
            foreach (int n in StallCounterMath.QuickAmounts(have))
            {
                int count = n;
                options.Add(new FloatMenuOption((count == have ? "CC_Stall_SellAll" : "CC_Stall_SellN").Translate(count).Resolve(), () => Put(g, count)));
            }
            if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
        }

        private static string Breakdown(Good g)
        {
            var parts = new List<string>();
            for (int i = 0; i < NoQuality; i++)
                if (g.onMap[i] > 0) parts.Add(((QualityCategory)i).GetLabel() + " " + g.onMap[i]);
            return parts.Count == 0 ? "CC_Stall_NoneAround".Translate().Resolve() : string.Join(", ", parts);
        }

        // What is for sale, a card a line.
        private void Counter(Rect r)
        {
            float y = r.y;
            float clearW = 150f;
            y += UiKit.SectionTitle(r.x, y, r.width - (lines.Count > 0 ? clearW + 8f : 0f), "CC_Stall_Counter".Translate(), Icons.Trader);
            if (lines.Count > 0)
            {
                var clear = new Rect(r.xMax - clearW, r.y, clearW, 24f);
                if (Widgets.ButtonText(clear, "CC_Stall_ClearAll".Translate()))
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CC_Stall_ClearAllConfirm".Translate(), () =>
                    {
                        foreach (var l in lines.ToList()) RimMarketStall.SetLine(stall, l.def, 0, l.price, l.quality);
                        Changed();
                    }, true));
            }
            y += 4f;
            float presetH = lines.Count > 0 ? 34f : 0f;
            var outer = new Rect(r.x, y, r.width, r.yMax - y - presetH);
            if (lines.Count == 0)
            {
                UiKit.EmptyState(outer, Icons.Trader, "CC_Stall_Empty".Translate());
                return;
            }
            counterHeight = lines.Count * (CardH + 6f);
            var view = new Rect(0f, 0f, outer.width - 16f, Mathf.Max(counterHeight, outer.height - 1f));
            if (toCounterEnd)
            {
                counterScroll.y = Mathf.Max(0f, counterHeight - outer.height);
                toCounterEnd = false;
            }
            Widgets.BeginScrollView(outer, ref counterScroll, view);
            var snapshot = lines.ToList();
            for (int i = 0; i < snapshot.Count; i++)
            {
                float cy = i * (CardH + 6f);
                if (cy + CardH < counterScroll.y || cy > counterScroll.y + outer.height) continue;
                Card(new Rect(0f, cy, view.width, CardH), snapshot[i]);
            }
            Widgets.EndScrollView();
            Presets(new Rect(r.x, r.yMax - presetH + 6f, r.width, presetH - 6f));
        }

        private void Card(Rect r, RimMarketStall.Line l)
        {
            var g = GoodFor(l.def);
            int heldHere = HeldOf(l.def);
            int available = Available(g, l.quality);
            int most = available + heldHere;
            float unit = UnitValue(g, l.quality);
            int each = PriceEach(unit, l.price);
            float chance = RimMarketStall.ChanceFor(l.price);
            int demand = StallCounterMath.Demand(chance);
            var demandColor = demand == 3 ? UiKit.Good : demand == 2 ? UiKit.Neutral : demand == 1 ? UiKit.Warn : UiKit.Bad;
            UiKit.Card(r, demandColor, false, true);
            Widgets.ThingIcon(new Rect(r.x + 10f, r.y + 10f, 36f, 36f), l.def);
            float x = r.x + 56f;
            // The name, the quality range and the way off the table.
            var remove = new Rect(r.xMax - 26f, r.y + 6f, 20f, 20f);
            if (Widgets.ButtonImage(remove, TexButton.CloseXSmall))
            {
                RimMarketStall.SetLine(stall, l.def, 0, l.price, l.quality);
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                Changed();
                return;
            }
            TooltipHandler.TipRegion(remove, "CC_Stall_RemoveTip".Translate());
            float right = remove.x - 6f;
            if (g.hasQuality)
            {
                var q = new Rect(right - 130f, r.y + 5f, 130f, 22f);
                if (Widgets.ButtonText(q, RangeLabel(l.quality, true))) QualityMenu(l, g);
                TooltipHandler.TipRegion(q, "CC_Stall_QualityTip".Translate());
                right = q.x - 6f;
            }
            Widgets.InfoCardButton(right - 24f, r.y + 5f, l.def);
            right -= 30f;
            UiKit.LabelFit(new Rect(x, r.y + 4f, Mathf.Max(0f, right - x), 24f), l.def.LabelCap, Color.white);
            // What has happened so far, and what it all fetches.
            var parts = new List<string>();
            if (l.sold > 0) parts.Add("CC_Stall_Sold".Translate(l.sold).Resolve());
            parts.Add(heldHere > 0 ? "CC_Stall_Held".Translate(heldHere).Resolve() : "CC_Stall_HeldNone".Translate().Resolve());
            parts.Add("CC_Stall_InColony".Translate(available).Resolve());
            parts.Add("CC_Stall_ForAll".Translate(StallCounterMath.LineWorth(each, l.toSell)).Resolve());
            UiKit.LabelFit(new Rect(x, r.y + 24f, r.xMax - x - 8f, 20f), string.Join(" · ", parts), UiKit.Faint, GameFont.Tiny);

            // How many: minus, the number, plus, all.
            float cy = r.y + 46f;
            int step = GenUI.CurrentAdjustmentMultiplier();
            var adjustTip = "CC_Stall_CountTip".Translate();
            var minus = new Rect(x, cy, 24f, 24f);
            if (Widgets.ButtonText(minus, "-") && l.toSell > 1) SetCount(l, Mathf.Max(1, l.toSell - step));
            TooltipHandler.TipRegion(minus, adjustTip);
            CountField(new Rect(minus.xMax + 3f, cy, 52f, 24f), l);
            var plus = new Rect(minus.xMax + 58f, cy, 24f, 24f);
            if (Widgets.ButtonText(plus, "+") && l.toSell < most) SetCount(l, Mathf.Min(most, l.toSell + step));
            TooltipHandler.TipRegion(plus, adjustTip);
            UiKit.LabelFit(new Rect(plus.xMax + 6f, cy, 56f, 24f), "CC_Stall_Of".Translate(most).Resolve(), UiKit.Faint);
            var all = new Rect(plus.xMax + 64f, cy, 40f, 24f);
            if (Widgets.ButtonText(all, "CC_Stall_All".Translate()) && most > 0) SetCount(l, most);
            TooltipHandler.TipRegion(all, "CC_Stall_AllTip".Translate(most));

            // The price: a slider, what it fetches each, and how well it sells.
            float px = all.xMax + 18f;
            string pillText = (demand == 3 ? "CC_Stall_Demand3" : demand == 2 ? "CC_Stall_Demand2" : demand == 1 ? "CC_Stall_Demand1" : "CC_Stall_Demand0").Translate();
            float pillW = UiKit.PillWidth(pillText);
            string priceText = "CC_Stall_PriceEach".Translate(l.price.ToStringPercent(), each).Resolve();
            Text.Font = GameFont.Small;
            float labelW = Text.CalcSize(priceText).x + 8f;
            float sliderW = Mathf.Max(60f, r.xMax - 10f - pillW - 8f - labelW - px);
            float price = Widgets.HorizontalSlider(new Rect(px, cy + 4f, sliderW, 16f), l.price, StallCounterMath.MinPrice, StallCounterMath.MaxPrice, false, null, null, null,
                StallCounterMath.PriceStep);
            price = StallCounterMath.SnapPrice(price);
            if (Mathf.Abs(price - l.price) > 0.001f)
            {
                RimMarketStall.SetPrice(l.order, price);
                newPrice = price;
            }
            UiKit.LabelFit(new Rect(px + sliderW + 6f, cy, labelW, 24f), priceText, Color.white);
            UiKit.Pill(r.xMax - 10f - pillW, cy + (24f - UiKit.PillHeight) / 2f, pillText, demandColor, null,
                "CC_Stall_DemandTip".Translate(l.price.ToStringPercent(), chance.ToStringPercent()).Resolve());
            TooltipHandler.TipRegion(new Rect(px, cy, sliderW, 24f), "CC_Stall_PriceTip".Translate());
        }

        private void CountField(Rect r, RimMarketStall.Line l)
        {
            string name = FieldPrefix + l.def.defName;
            if (editing != l.def || !buffers.ContainsKey(l.def)) buffers[l.def] = l.toSell.ToString();
            GUI.SetNextControlName(name);
            string typed = Widgets.TextField(r, buffers[l.def], 6, Digits);
            if (GUI.GetNameOfFocusedControl() != name) return;
            if (editing != null && editing != l.def) Commit();
            editing = l.def;
            buffers[l.def] = typed;
        }

        /// <summary>The count typed into a line's box, set (at least one, no more than there is; an empty box changes nothing).</summary>
        private void Commit()
        {
            var def = editing;
            editing = null;
            if (def == null || !buffers.TryGetValue(def, out string text) || !int.TryParse(text, out int n) || n < 1) return;
            var line = lines.FirstOrDefault(x => x.def == def);
            if (line.def == null) return;
            int most = Available(GoodFor(def), line.quality) + HeldOf(def);
            SetCount(line, StallCounterMath.ClampCount(n, most));
        }

        public override void PreClose()
        {
            base.PreClose();
            Commit();
        }

        private void QualityMenu(RimMarketStall.Line l, Good g)
        {
            var options = new List<FloatMenuOption>();
            foreach (var range in Ranges)
            {
                var chosen = range;
                int n = Available(g, chosen);
                options.Add(new FloatMenuOption(RangeLabel(chosen, false) + " (" + n + ")", () =>
                {
                    RimMarketStall.SetLine(stall, l.def, l.toSell, l.price, chosen);
                    Changed();
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static string RangeLabel(QualityRange r, bool shortForm)
        {
            if (r.min == QualityCategory.Awful && r.max == QualityCategory.Legendary) return (shortForm ? "CC_Stall_QualityAnyShort" : "CC_Stall_QualityAny").Translate();
            if (r.min == r.max) return "CC_Stall_QualityOnly".Translate(r.min.GetLabel()).CapitalizeFirst();
            if (r.min == QualityCategory.Awful) return "CC_Stall_QualityOrWorse".Translate(r.max.GetLabel()).CapitalizeFirst();
            if (r.max == QualityCategory.Legendary) return "CC_Stall_QualityOrBetter".Translate(r.min.GetLabel()).CapitalizeFirst();
            return r.min.GetLabel().CapitalizeFirst() + " – " + r.max.GetLabel();
        }

        // One price for every line.
        private void Presets(Rect r)
        {
            Text.Font = GameFont.Small;
            string label = "CC_Stall_PriceAll".Translate();
            float lw = Text.CalcSize(label).x + 10f;
            UiKit.LabelFit(new Rect(r.x, r.y, lw, 26f), label, UiKit.Faint);
            float x = r.x + lw;
            foreach (var (key, price) in new[] { ("CC_Stall_Bargain", StallCounterMath.Bargain), ("CC_Stall_Fair", StallCounterMath.Fair), ("CC_Stall_Dear", StallCounterMath.Dear) })
            {
                string text = key.Translate(price.ToStringPercent());
                float w = Text.CalcSize(text).x + 20f;
                var b = new Rect(x, r.y, w, 26f);
                if (Widgets.ButtonText(b, text))
                {
                    foreach (var l in lines) RimMarketStall.SetPrice(l.order, price);
                    newPrice = price;
                    Changed();
                }
                TooltipHandler.TipRegion(b, (key + "Tip").Translate(price.ToStringPercent(), RimMarketStall.ChanceFor(price).ToStringPercent()));
                x += w + 6f;
            }
        }

        // What it all comes to, and how often shoppers come at these prices.
        private void Footer(Rect r)
        {
            UiKit.Divider(r.x, r.y - 4f, r.width);
            var close = new Rect(r.xMax - 130f, r.y + 6f, 130f, 34f);
            if (Widgets.ButtonText(close, "CloseButton".Translate())) Close();
            float w = close.x - r.x - 12f;
            if (lines.Count == 0)
            {
                UiKit.LabelFit(new Rect(r.x, r.y + 2f, w, 22f), "CC_Stall_SummaryNone".Translate(), UiKit.Faint);
                return;
            }
            int articles = 0, silver = 0;
            foreach (var l in lines)
            {
                articles += l.toSell;
                silver += StallCounterMath.LineWorth(PriceEach(UnitValue(GoodFor(l.def), l.quality), l.price), l.toSell);
            }
            UiKit.LabelFit(new Rect(r.x, r.y + 2f, w, 22f), "CC_Stall_Summary".Translate(articles, lines.Count, silver).Resolve(), Color.white);
            int ticks = RimMarketStall.ShopperTicks(stall.Map, out float draw);
            string line;
            Color color;
            if (ticks < 0)
            {
                line = "CC_Stall_ShoppersOff".Translate();
                color = UiKit.Faint;
            }
            else
            {
                string every = ticks.ToStringTicksToPeriod();
                line = (draw >= 1.5f ? "CC_Stall_ShoppersBargain" : draw < 0.8f ? "CC_Stall_ShoppersDear" : "CC_Stall_Shoppers").Translate(every).Resolve();
                color = draw >= 1.5f ? UiKit.Good : draw < 0.8f ? UiKit.Warn : UiKit.Faint;
            }
            UiKit.LabelFit(new Rect(r.x, r.y + 24f, w, 22f), line, color);
        }

        public override void OnAcceptKeyPressed()
        {
            if (editing != null)
            {
                Commit();
                UI.UnfocusCurrentControl();
                return;
            }
            base.OnAcceptKeyPressed();
        }
    }
}
