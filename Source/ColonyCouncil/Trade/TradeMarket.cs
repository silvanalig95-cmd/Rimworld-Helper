using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A good's market for the trade screen, read once a trade: Vanilla Trading Expanded's history of it in numbers, its drawn line, the news that last moved it.</summary>
    public sealed class MarketView
    {
        public ThingDef def;
        /// <summary>VTE keeps a history of it (and its part on the trade screen is on).</summary>
        public bool market;
        public MarketStats stats;
        /// <summary>The last 60 days' prices and now, oldest first.</summary>
        public List<float> points;
        /// <summary>Made of a stuff: the market's prices are its kind's, without the stuff, so they are told as shares of the usual.</summary>
        public bool stuffed;
        public bool hasNews;
        public VanillaTrading.NewsItem news;
    }

    /// <summary>
    /// The trade screen's market (DESIGN.md "The trade screen"): for each good, Vanilla Trading Expanded's market value now
    /// against its usual worth and its average of the last 30 days, its change over the last 7 and 30 days, its high and
    /// low of the last 60 with a drawn line of them, the news that last moved it, what this trade is likely to do to its
    /// price by VTE's own rule for the colony's trades, and the colony's own last prices for it (Trade/TradeRecord.cs,
    /// without VTE too). Shown in a card beside the mouse on either price of a good's row (the game's own price tooltip
    /// stays on the other side of the mouse) and on the small trend line drawn in the row of a good whose price moved
    /// notably; the Market window (<see cref="Window_TradeMarket"/>, from a button on the silver row) lists them all. The
    /// rules are <see cref="TradeMarketMath"/>'s. Nothing is changed: VTE is only read, and the colony's record only
    /// written when a trade goes through.
    /// </summary>
    public static class TradeMarket
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The card, the trend line and the Market button: on, with the deeper game hooks, on a trade.</summary>
        public static bool On => RimfolkPatches.On && (S?.tradeMarket ?? true) && TradeScreen.Trading;

        /// <summary>VTE's history is read (the trend line, the price change sort, the market's part of the card and window).</summary>
        public static bool History => VanillaTrading.HistoryOn;

        private static string W(string key, params NamedArgument[] args) => key.Translate(args).Resolve();

        private static string Money(float silver) => silver.ToStringMoney();

        // ---------------- The views, read once a trade ----------------

        private static TradeDeal viewsDeal;
        private static readonly Dictionary<ThingDef, MarketView> views = new Dictionary<ThingDef, MarketView>();
        private static Dictionary<ThingDef, VanillaTrading.NewsItem> newsByGood;

        private static void Fresh()
        {
            var deal = TradeSession.Active ? TradeSession.deal : null;
            if (deal == viewsDeal) return;
            viewsDeal = deal;
            views.Clear();
            newsByGood = null;
        }

        /// <summary>A good's market for this trade (read once a trade: the prices don't move while it is open), or null without VTE's history.</summary>
        public static MarketView View(ThingDef def)
        {
            if (def == null || !History) return null;
            Fresh();
            if (views.TryGetValue(def, out var v)) return v;
            v = new MarketView { def = def, stuffed = def.MadeFromStuff };
            try
            {
                if (VanillaTrading.Market(def, out var history, out float now, out float usual))
                {
                    v.stats = TradeMarketMath.Stats(history, now, usual);
                    v.market = v.stats.known;
                    v.points = TradeMarketMath.Points(history, now);
                    newsByGood ??= VanillaTrading.NewsByGood(Find.TickManager.TicksGame - TradeMarketMath.RangeDays * GenDate.TicksPerDay);
                    if (newsByGood.TryGetValue(def, out var item))
                    {
                        v.hasNews = true;
                        v.news = item;
                    }
                }
            }
            catch (Exception e)
            {
                v.market = false;
                CouncilLog.ErrorOnce("trade market", e);
            }
            views[def] = v;
            return v;
        }

        /// <summary>The good's market where there is one to show, else null.</summary>
        public static MarketView Known(ThingDef def)
        {
            var v = View(def);
            return v != null && v.market ? v : null;
        }

        // ---------------- The words ----------------

        private static string Ago(int tick)
        {
            int days = TradeMarketMath.DaysAgo(tick, Find.TickManager.TicksGame);
            return days == 0 ? W("CC_Trade_Ago_Today") : days == 1 ? W("CC_Trade_Ago_Yesterday") : "CC_Trade_Ago_Days".Translate(days).Resolve();
        }

        private static string Way(float change)
        {
            int pct = Mathf.Abs(TradeMarketMath.Percent(change));
            return TradeMarketMath.IsSteady(change) ? W("CC_Trade_Mkt_Steady") : W(change > 0f ? "CC_Trade_Mkt_Up" : "CC_Trade_Mkt_Down", pct.Named("PCT"));
        }

        /// <summary>A share of the usual, for a good made of a stuff: 1.31 is "131%".</summary>
        private static string Share(float price, float usual) => usual > 0f ? Mathf.RoundToInt(price / usual * 100f) + "%" : "-";

        /// <summary>The market's part of the card: where its value stands, where it went, its 60 days' high and low, the news.</summary>
        public static List<string> MarketLines(MarketView v)
        {
            var lines = new List<string>();
            if (v == null || !v.market) return lines;
            var s = v.stats;
            int pct = Mathf.Abs(TradeMarketMath.Percent(s.vsUsual));
            string how = TradeMarketMath.IsSteady(s.vsUsual)
                ? (v.stuffed ? W("CC_Trade_Mkt_UsualSameStuff") : W("CC_Trade_Mkt_UsualSame", Money(s.usual).Named("USUAL")))
                : v.stuffed
                    ? W(s.vsUsual > 0f ? "CC_Trade_Mkt_UsualAboveStuff" : "CC_Trade_Mkt_UsualBelowStuff", pct.Named("PCT"))
                    : W(s.vsUsual > 0f ? "CC_Trade_Mkt_UsualAbove" : "CC_Trade_Mkt_UsualBelow", pct.Named("PCT"), Money(s.usual).Named("USUAL"));
            lines.Add(v.stuffed ? W("CC_Trade_Mkt_NowStuff", how.Named("HOW")) : W("CC_Trade_Mkt_Now", Money(s.now).Named("NOW"), how.Named("HOW")));
            if (s.days < 2)
            {
                lines.Add(W("CC_Trade_Mkt_Young"));
                return lines;
            }
            int avg = Mathf.Abs(TradeMarketMath.Percent(s.vsAverage));
            lines.Add(TradeMarketMath.IsSteady(s.vsAverage)
                ? W("CC_Trade_Mkt_AvgSame", s.averageDays.Named("DAYS"))
                : W(s.vsAverage > 0f ? "CC_Trade_Mkt_AvgAbove" : "CC_Trade_Mkt_AvgBelow", avg.Named("PCT"), s.averageDays.Named("DAYS")));
            if (s.weekDays > 0) lines.Add(W("CC_Trade_Mkt_Trend", s.weekDays.Named("DAYS"), Way(s.week).Named("HOW")));
            if (s.monthDays > s.weekDays) lines.Add(W("CC_Trade_Mkt_Trend", s.monthDays.Named("DAYS"), Way(s.month).Named("HOW")));
            lines.Add(v.stuffed
                ? W("CC_Trade_Mkt_RangeStuff", s.rangeDays.Named("DAYS"), Share(s.low, s.usual).Named("LOW"), Share(s.high, s.usual).Named("HIGH"))
                : W("CC_Trade_Mkt_Range", s.rangeDays.Named("DAYS"), Money(s.low).Named("LOW"), Money(s.high).Named("HIGH")));
            if (s.atHigh) lines.Add(W("CC_Trade_Mkt_AtHigh", s.rangeDays.Named("DAYS")));
            else if (s.atLow) lines.Add(W("CC_Trade_Mkt_AtLow", s.rangeDays.Named("DAYS")));
            return lines;
        }

        /// <summary>VTE's news is a sentence or two; a longer one is cut here.</summary>
        private const int MaxNews = 160;

        /// <summary>The news that last moved its price, in a short line, or null.</summary>
        public static string NewsLine(MarketView v)
        {
            if (v == null || !v.hasNews || v.news.way == 0) return null;
            string text = v.news.text.Trim();
            if (text.Length > MaxNews) text = text.Substring(0, MaxNews).TrimEnd() + "...";
            return W(v.news.way > 0 ? "CC_Trade_Mkt_NewsUp" : "CC_Trade_Mkt_NewsDown", Ago(v.news.tick).Named("AGO"), text.Named("TEXT"));
        }

        /// <summary>What this trade sells (positive) or buys of a good, in the market worth VTE counts the colony's trades at.</summary>
        private static float ThisTrade(ThingDef def, float worth)
        {
            var deal = TradeSession.Active ? TradeSession.deal : null;
            if (deal == null || worth <= 0f) return 0f;
            int count = 0;
            foreach (var t in deal.AllTradeables)
                if (t != null && !t.IsCurrency && t.HasAnyThing && !(t.AnyThing is MinifiedThing) && t.ThingDef == def)
                    count -= t.CountToTransfer;
            return count * worth;
        }

        /// <summary>
        /// What this trade (or, with nothing of it in the trade, what the colony traded of it lately) is likely to do to its
        /// market value by VTE's own rule, in plain words and no numbers; null when it is unlikely to move it.
        /// </summary>
        public static string ImpactLine(ThingDef def)
        {
            var v = Known(def);
            if (v == null) return null;
            try
            {
                float mine = ThisTrade(def, v.stats.now);
                float lately = VanillaTrading.Pending(def);
                var impact = TradeMarketMath.Impact(lately, mine, VanillaTrading.ImpactMultiplier);
                if (impact.evensOut && impact.level == ImpactLevel.None) return W("CC_Trade_Impact_Evens");
                if (impact.level == ImpactLevel.None) return null;
                string doing = impact.lately
                    ? W(impact.way < 0 ? "CC_Trade_Impact_LatelySold" : "CC_Trade_Impact_LatelyBought")
                    : W(impact.way < 0 ? "CC_Trade_Impact_Selling" : "CC_Trade_Impact_Buying");
                string chance = W(impact.level == ImpactLevel.Sure ? "CC_Trade_Impact_Sure" : impact.level == ImpactLevel.Likely ? "CC_Trade_Impact_Likely" : "CC_Trade_Impact_May");
                string way = W(impact.way < 0 ? impact.sharp ? "CC_Trade_Impact_DownSharp" : "CC_Trade_Impact_Down" : impact.sharp ? "CC_Trade_Impact_UpSharp" : "CC_Trade_Impact_Up");
                string line = W("CC_Trade_Impact", doing.Named("DOING"), chance.Named("CHANCE"), way.Named("WAY"));
                return impact.evensOut ? line + " " + W("CC_Trade_Impact_EvensSome") : line;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trade market impact", e);
                return null;
            }
        }

        /// <summary>The market's reason for a sale or a buy already marked (Trade/TradeAdvice.cs), or null.</summary>
        public static string HintLine(ThingDef def, bool sale)
        {
            var v = Known(def);
            if (v == null) return null;
            var s = v.stats;
            // The move the heading is from: the week's where notable, else the month's.
            bool week = s.weekDays >= TradeMarketMath.WeekLeast && Mathf.Abs(s.week) >= TradeMarketMath.WeekMove;
            int pct = Mathf.Abs(TradeMarketMath.Percent(week ? s.week : s.month));
            int days = week ? s.weekDays : s.monthDays;
            switch (TradeMarketMath.Hint(s, sale))
            {
                case MarketHint.HighSellNow: return W("CC_Trade_Hint_HighSellNow", s.rangeDays.Named("DAYS"));
                case MarketHint.FallingSellSooner: return W("CC_Trade_Hint_FallingSellSooner", pct.Named("PCT"), days.Named("DAYS"));
                case MarketHint.LowSellCanWait: return W("CC_Trade_Hint_LowSellCanWait", s.rangeDays.Named("DAYS"));
                case MarketHint.LowBuyNow: return W("CC_Trade_Hint_LowBuyNow", s.rangeDays.Named("DAYS"));
                case MarketHint.RisingBuySooner: return W("CC_Trade_Hint_RisingBuySooner", pct.Named("PCT"), days.Named("DAYS"));
                case MarketHint.HighBuyCanWait: return W("CC_Trade_Hint_HighBuyCanWait", s.rangeDays.Named("DAYS"));
                default: return null;
            }
        }

        // ---------------- The colony's own record ----------------

        private static int QualityOf(Tradeable t) => t != null && t.HasAnyThing && t.AnyThing.GetInnerIfMinified().TryGetQuality(out var q) ? (int)q : -1;

        private static string Pieces(TradeLine l) =>
            l.quality >= 0 ? W("CC_Trade_Rec_CountQuality", l.count.Named("COUNT"), ((QualityCategory)l.quality).GetLabel().Named("QUALITY")) : l.count.ToStringCached();

        /// <summary>One line of the record in words: "sold 120 at $3.10 each to Kanata, 6 days ago".</summary>
        public static string LineWords(TradeLine l) =>
            W(l.sold ? "CC_Trade_Rec_Sold" : "CC_Trade_Rec_Bought", Pieces(l).Named("COUNT"), Money(l.price).Named("PRICE"),
                (l.who.NullOrEmpty() ? W("CC_Trade_Rec_Someone") : l.who).Named("WHO"), Ago(l.tick).Named("AGO"));

        /// <summary>The colony's own last prices for a row's good, and the best of the last 15 days where that isn't the last.</summary>
        public static List<string> RecordLines(ThingDef def, ThingDef stuff, int quality)
        {
            var lines = new List<string>();
            var book = TradeBook.On ? TradeBook.Instance : null;
            var log = book?.For(def, stuff);
            if (log == null || log.lines.Count == 0) return lines;
            var entries = log.Entries;
            int now = Find.TickManager.TicksGame;
            foreach (bool sold in new[] { true, false })
            {
                int last = TradeMarketMath.Last(entries, sold, quality);
                if (last < 0) continue;
                lines.Add(LineWords(log.lines[last]));
                int best = TradeMarketMath.Best(entries, sold, now, quality);
                if (best >= 0 && best != last && Math.Abs(entries[best].price - entries[last].price) >= 0.005f)
                {
                    var b = log.lines[best];
                    lines.Add(W(sold ? "CC_Trade_Rec_BestSold" : "CC_Trade_Rec_BestBought", TradeMarketMath.BestDays.Named("DAYS"), Money(b.price).Named("PRICE"),
                        (b.who.NullOrEmpty() ? W("CC_Trade_Rec_Someone") : b.who).Named("WHO")));
                }
            }
            return lines;
        }

        // ---------------- The card ----------------

        private sealed class Card
        {
            public string title;
            public readonly List<string> market = new List<string>();
            public MarketView view;
            public string news, impact;
            public readonly List<string> record = new List<string>();

            public bool Any => market.Count > 0 || impact != null || record.Count > 0;
        }

        private const int CardWindowId = 0x6A11E7;
        private const float CardWidth = 330f, Pad = 10f, ChartH = 54f, Delay = 0.45f;

        private static object hoverKey, builtKey;
        private static float hoverSince, builtHeight;
        private static int hoverFrame = -10;
        private static Card builtCard;

        private static Card Build(Tradeable t)
        {
            var c = new Card { title = t.LabelCap };
            var v = Known(t.ThingDef);
            if (v != null)
            {
                c.view = v;
                c.market.AddRange(MarketLines(v));
                c.news = NewsLine(v);
                c.impact = ImpactLine(t.ThingDef);
            }
            if (TradeBook.On) c.record.AddRange(RecordLines(t.ThingDef, t.StuffDef, QualityOf(t)));
            return c;
        }

        /// <summary>
        /// The mouse is on a good's price (or its trend line) in a row: after the game's own tooltip pause, the card shows
        /// beside the mouse, on the side away from the game's tooltip. <paramref name="key"/> tells one hover place from
        /// another; <paramref name="rect"/> is in the drawing's own coordinates.
        /// </summary>
        public static void Hover(Tradeable t, object key, Rect rect)
        {
            if (t == null || !t.HasAnyThing || !Mouse.IsOver(rect)) return;
            float now = Time.realtimeSinceStartup;
            if (!Equals(hoverKey, key) || Time.frameCount - hoverFrame > 2)
            {
                hoverSince = now;
                builtCard = null;
            }
            hoverKey = key;
            hoverFrame = Time.frameCount;
            if (now - hoverSince < Delay || Event.current.type != EventType.Repaint) return;
            // Built once while the mouse stays (the counts can't change under it), and its height with it.
            if (!Equals(builtKey, key) || builtCard == null)
            {
                try
                {
                    builtCard = Build(t);
                    builtHeight = builtCard.Any ? Height(builtCard) : 0f;
                }
                catch (Exception e)
                {
                    builtCard = null;
                    CouncilLog.ErrorOnce("trade market card", e);
                    return;
                }
                builtKey = key;
            }
            var c = builtCard;
            if (!c.Any) return;
            float h = builtHeight;
            var mouse = UI.MousePositionOnUIInverted;
            // Left of the mouse (the game puts its tooltips on the right); on the right past them where there's no room.
            float x = mouse.x - 18f - CardWidth;
            if (x < 4f) x = Mathf.Min(mouse.x + 18f + 270f, UI.screenWidth - CardWidth - 4f);
            float y = Mathf.Clamp(mouse.y + 14f, 4f, Mathf.Max(4f, UI.screenHeight - h - 4f));
            var r = new Rect(x, y, CardWidth, h);
            Find.WindowStack.ImmediateWindow(CardWindowId, r, WindowLayer.Super, () => DrawCard(c, r.AtZero()), doBackground: false);
        }

        private static float TextH(string s, float w)
        {
            Text.Font = GameFont.Small;
            return Text.CalcHeight(s, w);
        }

        private static float Height(Card c)
        {
            float w = CardWidth - Pad * 2f;
            var oldFont = Text.Font;
            float h = Pad + 24f;
            if (c.market.Count > 0)
            {
                h += 20f;
                foreach (var s in c.market) h += TextH(s, w) + 2f;
                if (c.view?.points != null && c.view.points.Count >= 2) h += ChartH + 20f;
                if (c.news != null) h += TextH(c.news, w) + 4f;
            }
            if (c.impact != null) h += TextH(c.impact, w) + 6f;
            if (c.record.Count > 0)
            {
                h += 24f;
                foreach (var s in c.record) h += TextH(s, w) + 2f;
            }
            Text.Font = oldFont;
            return Mathf.Ceil(h + Pad);
        }

        private static void DrawCard(Card c, Rect rect)
        {
            var oldFont = Text.Font;
            var oldAnchor = Text.Anchor;
            try
            {
                Widgets.DrawBoxSolidWithOutline(rect, UiKit.PreviewFill, UiKit.PreviewEdge, 1);
                float x = Pad, y = Pad, w = rect.width - Pad * 2f;
                Text.Anchor = TextAnchor.UpperLeft;
                UiKit.LabelFit(new Rect(x, y, w, 22f), c.title, UiKit.HeaderColor);
                y += 24f;
                if (c.market.Count > 0)
                {
                    Head(x, ref y, w, W("CC_Trade_Card_Market"));
                    foreach (var s in c.market) Para(x, ref y, w, s, Color.white);
                    var pts = c.view?.points;
                    if (pts != null && pts.Count >= 2)
                    {
                        y += 4f;
                        Chart(new Rect(x, y, w, ChartH), c.view);
                        y += ChartH + 2f;
                        Text.Font = GameFont.Tiny;
                        GUI.color = UiKit.Dim;
                        Widgets.Label(new Rect(x, y, w, 16f), W("CC_Trade_Card_Chart", (pts.Count - 1).Named("DAYS")).Truncate(w));
                        GUI.color = Color.white;
                        y += 14f;
                    }
                    if (c.news != null) Para(x, ref y, w, c.news, UiKit.Faint, 2f);
                }
                if (c.impact != null) Para(x, ref y, w, c.impact, UiKit.Soon, 4f);
                if (c.record.Count > 0)
                {
                    y += 4f;
                    Head(x, ref y, w, W("CC_Trade_Card_Record"));
                    foreach (var s in c.record) Para(x, ref y, w, s, Color.white);
                }
            }
            finally
            {
                GUI.color = Color.white;
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
            }
        }

        private static void Head(float x, ref float y, float w, string label)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = UiKit.Heading;
            Widgets.Label(new Rect(x, y, w, 18f), label.Truncate(w));
            GUI.color = Color.white;
            UiKit.Divider(x, y + 17f, w);
            y += 20f;
        }

        private static void Para(float x, ref float y, float w, string s, Color color, float before = 0f)
        {
            y += before;
            Text.Font = GameFont.Small;
            float h = Text.CalcHeight(s, w);
            GUI.color = color;
            Widgets.Label(new Rect(x, y, w, h), s);
            GUI.color = Color.white;
            y += h + 2f;
        }

        /// <summary>The drawn line of the last 60 days and now, with its usual worth as a faint line across it.</summary>
        private static void Chart(Rect r, MarketView v)
        {
            var pts = v.points;
            float min = Mathf.Min(pts.Min(), v.stats.usual), max = Mathf.Max(pts.Max(), v.stats.usual);
            float pad = Mathf.Max((max - min) * 0.1f, max * 0.02f);
            min = Mathf.Max(0f, min - pad);
            max += pad;
            Widgets.DrawBoxSolid(r, UiKit.MeterBack);
            if (v.stats.usual > 0f && max > min)
            {
                float uy = r.yMax - Mathf.Clamp01((v.stats.usual - min) / (max - min)) * r.height;
                Widgets.DrawLine(new Vector2(r.x, uy), new Vector2(r.xMax, uy), UiKit.WithAlpha(UiKit.Faint, 0.45f), 1f);
            }
            UiKit.DaysLine(r.ContractedBy(2f), pts, pts.Count, min, max, UiKit.DaysColor(v.stats.heading));
        }

        // ---------------- The trend line in a row ----------------

        private const float TrendW = 28f, TrendH = 14f;

        /// <summary>Beside the name of a good whose market value moved notably: a small line of its last 30 days, rising or falling in colour; the card on it.</summary>
        public static void DrawTrendMark(Tradeable t, Rect row, ref float curX)
        {
            if (t == null || t.IsCurrency || !t.HasAnyThing || t.AnyThing is Pawn) return;
            var v = Known(t.ThingDef);
            if (v == null || v.stats.heading == 0 || v.points == null || v.points.Count < 2) return;
            var r = new Rect(curX - TrendW - 2f, (row.height - TrendH) / 2f, TrendW, TrendH);
            curX -= TrendW + 4f;
            var color = UiKit.DaysColor(v.stats.heading);
            if (Event.current.type == EventType.Repaint) Line(r, v.points, Math.Min(v.points.Count, TradeMarketMath.MonthDays + 1), color);
            if (Mouse.IsOver(r)) Widgets.DrawHighlight(r.ExpandedBy(2f));
            Hover(t, (t, 2), r.ExpandedBy(2f));
        }

        /// <summary>A thin line of the last <paramref name="count"/> values, no fill (the row's mark is small).</summary>
        private static void Line(Rect r, IList<float> values, int count, Color color)
        {
            int n = Math.Min(count, values.Count);
            if (n < 2) return;
            int from = values.Count - n;
            float min = float.MaxValue, max = float.MinValue;
            for (int i = from; i < values.Count; i++)
            {
                min = Mathf.Min(min, values[i]);
                max = Mathf.Max(max, values[i]);
            }
            if (max - min < 0.0001f) max = min + 1f;
            float step = r.width / (n - 1);
            Vector2 At(int i) => new Vector2(r.x + i * step, r.yMax - Mathf.Clamp01((values[from + i] - min) / (max - min)) * r.height);
            for (int i = 1; i < n; i++) Widgets.DrawLine(At(i - 1), At(i), color, 1.5f);
        }

        // ---------------- The price cells ----------------

        /// <summary>After the game draws a row: the card on either of its prices (where that side has some and the trader deals in it).</summary>
        public static void HoverPrices(Rect row, Tradeable t)
        {
            if (t == null || t.IsCurrency || !t.HasAnyThing || t.AnyThing is Pawn || !t.TraderWillTrade) return;
            float w = row.width;
            if (t.CountHeldBy(Transactor.Colony) > 0) Hover(t, (t, 0), new Rect(row.x + w - TradeUI.TotalNumbersColumnsWidths + TradeUI.CountColumnWidth, row.y, TradeUI.PriceColumnWidth, row.height));
            if (t.CountHeldBy(Transactor.Trader) > 0) Hover(t, (t, 1), new Rect(row.x + w - TradeUI.CountColumnWidth - TradeUI.PriceColumnWidth, row.y, TradeUI.PriceColumnWidth, row.height));
        }

        // ---------------- The Market button, on the silver row ----------------

        private static bool buttonDrawn;
        private static float buttonWidth = -1f;

        private static float ButtonWidth
        {
            get
            {
                if (buttonWidth < 0f)
                {
                    var old = Text.Font;
                    Text.Font = GameFont.Small;
                    buttonWidth = Mathf.Max(80f, Text.CalcSize(W("CC_Trade_MarketButton")).x + 24f);
                    Text.Font = old;
                }
                return buttonWidth;
            }
        }

        /// <summary>The Market button at the right of the silver row's name, where the row has room (from the game's extra icons, so the name's tooltip stops short of it).</summary>
        public static void DrawMarketButton(Rect row, ref float curX)
        {
            // Asked for here: the fallback after the row stands down, whether or not there is room.
            buttonDrawn = true;
            float bw = ButtonWidth;
            if (curX - bw - 8f < 140f) return;
            var r = new Rect(curX - bw - 6f, (row.height - 24f) / 2f, bw, 24f);
            curX -= bw + 10f;
            Button(r);
        }

        /// <summary>After the silver row is drawn: the button where the game's extra icons weren't asked for it.</summary>
        public static void MarketButtonFallback(Rect row)
        {
            if (buttonDrawn)
            {
                buttonDrawn = false;
                return;
            }
            float bw = ButtonWidth;
            float right = row.x + row.width - TradeUI.TotalNumbersColumnsWidths - 6f;
            if (right - bw - row.x < 140f) return;
            Button(new Rect(right - bw, row.y + (row.height - 24f) / 2f, bw, 24f));
        }

        private static void Button(Rect r)
        {
            var old = Text.Font;
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(r, W("CC_Trade_MarketButton")) && Find.WindowStack.WindowOfType<Window_TradeMarket>() == null)
                Find.WindowStack.Add(new Window_TradeMarket());
            Text.Font = old;
            TooltipHandler.TipRegion(r, W(History ? "CC_Trade_MarketButtonTip" : "CC_Trade_MarketButtonTipNoVte"));
        }
    }

    // ---------------- The game's hooks ----------------

    /// <summary>The market card on a good's prices, after the game draws its row; on the silver row, the Market button where it wasn't drawn with the row's icons.</summary>
    [HarmonyPatch(typeof(TradeUI), nameof(TradeUI.DrawTradeableRow))]
    public static class Patch_TradeMarketCard
    {
        public static void Postfix(Rect rect, Tradeable trad)
        {
            if (trad == null || !TradeMarket.On) return;
            try
            {
                if (trad.IsCurrency) TradeMarket.MarketButtonFallback(rect);
                else TradeMarket.HoverPrices(rect, trad);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trade market card", e);
            }
        }
    }
}
