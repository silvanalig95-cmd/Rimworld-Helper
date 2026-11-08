using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The Market window, from the button on the trade screen's silver row (Trade/TradeMarket.cs): every good of this
    /// trade (the colony's and the trader's) a line, with Vanilla Trading Expanded's market value against the usual, its
    /// change over the last 7 and 30 days and a small line of its last 60 days, and the colony's own last prices for it
    /// (Trade/TradeRecord.cs; without VTE, its best of the last 15 days too). Each column sorts on a click, again for the
    /// other way; the biggest movers come first, or without VTE the goods traded most lately. The columns that need VTE are
    /// left out without it. Pointing at a good shows its card. Read only: it closes with the trade. In Rimfolk's frame (key
    /// "Market"), its title in the title bar; wider, the goods' names take the room.
    /// </summary>
    public class Window_TradeMarket : FramedWindow
    {
        private sealed class Line
        {
            public ThingDef def;
            /// <summary>The first row of it on the trade screen: its icon, its card.</summary>
            public Tradeable row;
            public string label;
            public MarketView view;
            public TradeGoodLog log;
            public TradeLine lastSold, lastBought, bestSold, bestBought;
            public int newest = int.MinValue;
        }

        private enum Col
        {
            Good,
            Usual,
            Week,
            Month,
            Move,
            LastSold,
            LastBought,
            BestSold,
            BestBought,
            Recent
        }

        private struct Column
        {
            public Col col;
            public string head, tip;
            public float width;
        }

        private const float RowH = 28f, HeadH = 26f, FootH = 34f, TitleH = 34f, Gap = 6f;

        private readonly List<Line> lines = new List<Line>();
        private readonly List<Column> columns = new List<Column>();
        private readonly bool market;
        private readonly TradeDeal deal;
        private Col sort;
        private bool descending = true;
        private Vector2 scroll;

        private static string W(string key, params NamedArgument[] args) => key.Translate(args).Resolve();

        public override Vector2 InitialSize => new Vector2(Mathf.Min(columns.Sum(c => c.width) + Gap * (columns.Count - 1) + StandardMargin * 2f + 20f, UI.screenWidth - 40f),
            Mathf.Min(640f, UI.screenHeight - 60f));

        protected override string FrameTitle => W(market ? "CC_Trade_Market_Title" : "CC_Trade_Market_TitleNoVte");

        protected override Texture2D FrameIcon => Icons.Trader;

        /// <summary>The room a wider window gives the goods' names (the other columns keep their widths).</summary>
        private float goodExtra;

        private float Width(Column c) => c.width + (c.col == Col.Good ? goodExtra : 0f);

        public Window_TradeMarket() : base("Market")
        {
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;
            forcePause = true;
            deal = TradeSession.deal;
            market = TradeMarket.History;
            Columns();
            Read();
            sort = market ? Col.Move : Col.Recent;
            Sort();
        }

        private void Columns()
        {
            void Add(Col c, string key, float width) => columns.Add(new Column { col = c, head = W(key), tip = W(key + "Tip"), width = width });
            Add(Col.Good, "CC_Trade_Market_Col_Good", market ? 200f : 240f);
            if (market)
            {
                Add(Col.Usual, "CC_Trade_Market_Col_Usual", 92f);
                Add(Col.Week, "CC_Trade_Market_Col_Week", 66f);
                Add(Col.Month, "CC_Trade_Market_Col_Month", 66f);
                Add(Col.Move, "CC_Trade_Market_Col_Line", 112f);
            }
            Add(Col.LastSold, "CC_Trade_Market_Col_LastSold", 104f);
            Add(Col.LastBought, "CC_Trade_Market_Col_LastBought", 104f);
            if (!market)
            {
                Add(Col.BestSold, "CC_Trade_Market_Col_BestSold", 116f);
                Add(Col.BestBought, "CC_Trade_Market_Col_BestBought", 116f);
            }
        }

        /// <summary>The goods of this trade, one line a kind of thing, with its market and the colony's record of it.</summary>
        private void Read()
        {
            lines.Clear();
            if (deal == null) return;
            var book = TradeBook.On ? TradeBook.Instance : null;
            int now = Find.TickManager.TicksGame;
            var seen = new Dictionary<ThingDef, Line>();
            foreach (var t in deal.AllTradeables)
            {
                if (t == null || t.IsCurrency || t.IsFavor || !t.HasAnyThing || t is Tradeable_Pawn || t.AnyThing is Pawn) continue;
                var def = t.ThingDef;
                if (def == null || seen.ContainsKey(def)) continue;
                var l = new Line { def = def, row = t, label = def.LabelCap };
                seen[def] = l;
                lines.Add(l);
                if (market) l.view = TradeMarket.Known(def);
                // A kind of thing made of several stuffs: the stuff this trade has first, else the one traded most lately.
                l.log = book?.For(def, t.StuffDef) ?? book?.Latest(def);
                if (l.log == null) continue;
                var entries = l.log.Entries;
                int i;
                if ((i = TradeMarketMath.Last(entries, true)) >= 0) l.lastSold = l.log.lines[i];
                if ((i = TradeMarketMath.Last(entries, false)) >= 0) l.lastBought = l.log.lines[i];
                if ((i = TradeMarketMath.Best(entries, true, now)) >= 0) l.bestSold = l.log.lines[i];
                if ((i = TradeMarketMath.Best(entries, false, now)) >= 0) l.bestBought = l.log.lines[i];
                l.newest = l.log.Newest;
            }
        }

        // ---------------- The sort ----------------

        /// <summary>A line's key for a column; null where it has none (those go last either way).</summary>
        private static float? Key(Line l, Col c)
        {
            var s = l.view?.stats;
            switch (c)
            {
                case Col.Usual: return s != null && s.known ? s.vsUsual : (float?)null;
                case Col.Week: return s != null && s.weekDays > 0 ? s.week : (float?)null;
                case Col.Month: return s != null && s.monthDays > 0 ? s.month : (float?)null;
                case Col.Move:
                    float m = TradeMarketMath.MoveScore(s);
                    return m >= 0f ? m : (float?)null;
                case Col.LastSold: return l.lastSold?.price;
                case Col.LastBought: return l.lastBought?.price;
                case Col.BestSold: return l.bestSold?.price;
                case Col.BestBought: return l.bestBought?.price;
                case Col.Recent: return l.log != null ? l.newest : (float?)null;
                default: return null;
            }
        }

        private void Sort()
        {
            if (sort == Col.Good)
            {
                lines.Sort((a, b) => descending ? string.Compare(b.label, a.label, StringComparison.CurrentCulture) : string.Compare(a.label, b.label, StringComparison.CurrentCulture));
                return;
            }
            var order = lines.OrderBy(l => Key(l, sort).HasValue ? 0 : 1);
            order = descending ? order.ThenByDescending(l => Key(l, sort) ?? 0f) : order.ThenBy(l => Key(l, sort) ?? 0f);
            var sorted = order.ThenBy(l => l.label).ToList();
            lines.Clear();
            lines.AddRange(sorted);
        }

        private void Click(Col c)
        {
            if (sort == c) descending = !descending;
            else
            {
                sort = c;
                descending = c != Col.Good;
            }
            Sort();
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
        }

        // ---------------- Drawing ----------------

        protected override void DoFramedContents(Rect inRect)
        {
            // It is the trade's: gone with it.
            if (!TradeScreen.Trading || TradeSession.deal != deal)
            {
                Close();
                return;
            }
            var oldFont = Text.Font;
            var oldAnchor = Text.Anchor;
            try
            {
                Text.Anchor = TextAnchor.UpperLeft;
                float y = 0f;
                // The title, unless the frame has it in its bar.
                if (!TitleInBar)
                {
                    Text.Font = GameFont.Medium;
                    Widgets.Label(new Rect(0f, 0f, inRect.width - 30f, TitleH), FrameTitle);
                    y = TitleH + 2f;
                }
                Text.Font = GameFont.Small;
                goodExtra = Mathf.Clamp(inRect.width - 16f - columns.Sum(c => c.width) - Gap * (columns.Count - 1), 0f, 320f);
                Header(new Rect(0f, y, inRect.width - 16f, HeadH));
                y += HeadH + 2f;
                var list = new Rect(0f, y, inRect.width, inRect.height - y - FootH);
                Rows(list);
                Text.Font = GameFont.Tiny;
                GUI.color = UiKit.Dim;
                Widgets.Label(new Rect(0f, inRect.height - FootH + 4f, inRect.width, FootH - 4f), W(market ? "CC_Trade_Market_Foot" : "CC_Trade_Market_FootNoVte"));
                GUI.color = Color.white;
            }
            finally
            {
                GUI.color = Color.white;
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
            }
        }

        private void Header(Rect r)
        {
            float x = r.x;
            Text.Font = GameFont.Tiny;
            foreach (var c in columns)
            {
                var cell = new Rect(x, r.y, Width(c), r.height);
                bool active = sort == c.col;
                if (Mouse.IsOver(cell)) Widgets.DrawHighlight(cell);
                GUI.color = active ? UiKit.Gold : UiKit.Faint;
                Text.Anchor = c.col == Col.Good ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
                Widgets.Label(c.col == Col.Good ? new Rect(cell.x + 30f, cell.y, cell.width - 30f, cell.height) : cell, c.head.Truncate(cell.width));
                GUI.color = Color.white;
                if (active) Widgets.DrawBoxSolid(new Rect(cell.x + 2f, cell.yMax - 2f, cell.width - 4f, 2f), UiKit.Gold);
                TooltipHandler.TipRegion(cell, c.tip + "\n\n" + W(active ? "CC_Trade_Market_Sorted" : "CC_Trade_Market_SortBy"));
                if (Widgets.ButtonInvisible(cell)) Click(c.col);
                x += cell.width + Gap;
            }
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            UiKit.Divider(r.x, r.yMax, r.width);
        }

        private void Rows(Rect area)
        {
            if (lines.Count == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = UiKit.Faint;
                Widgets.Label(area, W("CC_Trade_Market_Empty"));
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }
            var view = new Rect(0f, 0f, area.width - 16f, lines.Count * RowH);
            Widgets.BeginScrollView(area, ref scroll, view);
            try
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    float top = i * RowH;
                    if (top + RowH < scroll.y || top > scroll.y + area.height) continue;
                    Row(new Rect(0f, top, view.width, RowH), lines[i], i);
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private void Row(Rect r, Line l, int index)
        {
            if (index % 2 == 1) Widgets.DrawBoxSolid(r, UiKit.ZebraFill);
            if (Mouse.IsOver(r)) Widgets.DrawBoxSolid(r, UiKit.CardHover);
            float x = r.x;
            foreach (var c in columns)
            {
                var cell = new Rect(x, r.y, Width(c), r.height);
                Cell(cell, l, c.col);
                x += cell.width + Gap;
            }
        }

        private static string Signed(float change) => TradeMarketMath.Percent(change).ToString("+0;-0;0") + "%";

        private static Color ChangeColor(float change) => TradeMarketMath.IsSteady(change) ? UiKit.Faint : change > 0f ? UiKit.Warm : UiKit.Sour;

        private void Cell(Rect cell, Line l, Col col)
        {
            var s = l.view?.stats;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            switch (col)
            {
                case Col.Good:
                    var icon = new Rect(cell.x + 2f, cell.y + (cell.height - 24f) / 2f, 24f, 24f);
                    if (l.row.HasAnyThing) Widgets.ThingIcon(icon, l.row.AnyThing);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(new Rect(cell.x + 30f, cell.y, cell.width - 30f, cell.height), l.label.Truncate(cell.width - 30f));
                    // The good's card, as on the trade screen's prices.
                    TradeMarket.Hover(l.row, (l.row, 3), cell);
                    break;
                case Col.Usual:
                    if (s == null || !s.known) Dash(cell);
                    else
                    {
                        Value(cell, Signed(s.vsUsual), UiKit.Ink);
                        TooltipHandler.TipRegion(cell, string.Join("\n", TradeMarket.MarketLines(l.view)));
                    }
                    break;
                case Col.Week:
                case Col.Month:
                    int days = s == null ? 0 : col == Col.Week ? s.weekDays : s.monthDays;
                    if (days == 0) Dash(cell);
                    else
                    {
                        float ch = col == Col.Week ? s.week : s.month;
                        Value(cell, Signed(ch), ChangeColor(ch));
                        TooltipHandler.TipRegion(cell, W("CC_Trade_Market_ChangeTip", days.Named("DAYS")));
                    }
                    break;
                case Col.Move:
                    var pts = l.view?.points;
                    if (pts == null || pts.Count < 2) Dash(cell);
                    else
                    {
                        float min = pts.Min(), max = pts.Max();
                        if (max - min < 0.0001f) max = min + 1f;
                        UiKit.DaysLine(cell.ContractedBy(4f, 5f), pts, pts.Count, min, max, UiKit.DaysColor(s.heading));
                        TooltipHandler.TipRegion(cell, string.Join("\n", TradeMarket.MarketLines(l.view)));
                    }
                    break;
                case Col.LastSold:
                    Record(cell, l.lastSold);
                    break;
                case Col.LastBought:
                    Record(cell, l.lastBought);
                    break;
                case Col.BestSold:
                    Record(cell, l.bestSold);
                    break;
                case Col.BestBought:
                    Record(cell, l.bestBought);
                    break;
            }
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static void Value(Rect cell, string text, Color color)
        {
            GUI.color = color;
            Widgets.Label(cell, text);
            GUI.color = Color.white;
        }

        private static void Dash(Rect cell) => Value(cell, "-", UiKit.Dim);

        private static void Record(Rect cell, TradeLine line)
        {
            if (line == null)
            {
                Dash(cell);
                return;
            }
            Value(cell, line.price.ToStringMoney(), UiKit.Ink);
            if (Mouse.IsOver(cell)) Widgets.DrawHighlight(cell);
            TooltipHandler.TipRegion(cell, TradeMarket.LineWords(line).CapitalizeFirst());
        }
    }
}
