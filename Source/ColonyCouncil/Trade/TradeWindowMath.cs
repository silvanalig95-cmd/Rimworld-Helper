using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>How good a row's price is, one side at a time (a sale or a purchase): a word and a number of bars on Rimfolk's trade window.</summary>
    public enum DealGrade
    {
        /// <summary>No grade: that side isn't there (nothing of it to sell, or the trader has none), or it isn't a good (people).</summary>
        None,
        Poor,
        Fair,
        Good,
        Great
    }

    /// <summary>The filter chips of Rimfolk's trade window and the kinds its lists are grouped under.</summary>
    public enum TradeCategory
    {
        Food,
        Medicine,
        Raw,
        Manufactured,
        Weapons,
        Apparel,
        ArtBooks,
        Drugs,
        Animals,
        People,
        Other
    }

    /// <summary>
    /// What the lists of Rimfolk's trade window sort by, and the columns they show: the name, how many there are (ours in
    /// the sell list, theirs in the buy list), the price (what they pay, what they ask), the deal, the need and the count
    /// control. Value, market, trend and mass are sorts only (the row's tooltip says them).
    /// </summary>
    public enum TradeColumn
    {
        Name,
        Value,
        Ours,
        Pay,
        Count,
        Ask,
        Theirs,
        Deal,
        Market,
        Trend,
        Mass,
        Need
    }

    /// <summary>What a side's grade is read from (Trade/Window_RimfolkTrade.cs reads the game, the advice, the market and the record into it).</summary>
    public struct DealInput
    {
        /// <summary>A sale (else a purchase).</summary>
        public bool sale;
        /// <summary>The trader's price a piece (the game's own) and the usual one for this negotiator and difficulty.</summary>
        public float price, usual;
        /// <summary>Vanilla Trading Expanded's market: +1 it says now (a sale at a high or of a falling good, a buy at a low or of a rising one), -1 it says wait, 0 nothing.</summary>
        public int market;
        /// <summary>The colony's best price of this side in the last 15 days (the most it got, the least it paid); 0 for none.</summary>
        public float best;
        /// <summary>A purchase: the colony has a use for it (the advice marks it worth buying: a need, or a bargain on a stock it keeps).</summary>
        public bool use;
        /// <summary>A sale: the colony is short of it, or all it has is set aside.</summary>
        public bool kept;
        /// <summary>False: not graded at all (people).</summary>
        public bool graded;
    }

    /// <summary>A side's grade and what it came from, for the cell and its tooltip.</summary>
    public struct DealRating
    {
        public DealGrade grade;
        /// <summary>The price against the usual, better above 1 (a sale's price over the usual, a purchase's usual over the price), moved by the market and the record.</summary>
        public float score;
        /// <summary>The price against the usual as it is, either way: 0.12 is 12% above it, -0.1 a tenth under it.</summary>
        public float vsUsual;
        /// <summary>The market's step and the record's (+1, 0, -1).</summary>
        public int marketStep, recordStep;
        /// <summary>The grade was held at fair: a purchase the colony has no use for, a sale of what it is short of or keeps.</summary>
        public bool capped;

        public bool Graded => grade != DealGrade.None;
    }

    /// <summary>How the window's two lists stand: side by side when the window is wide enough, else as two tabs (the player's choice, remembered).</summary>
    public enum TradeListsChoice
    {
        /// <summary>Side by side when each list has room for all its columns, else tabs.</summary>
        Auto,
        /// <summary>Side by side wherever each list has room for the name, the price and the count; tabs in a narrower window.</summary>
        SideBySide,
        /// <summary>Always two tabs, one list at a time.</summary>
        Tabs
    }

    /// <summary>One row's keys for a list's sort (a list's own side: its deal is that side's); null where the row has none (those go last either way).</summary>
    public sealed class TradeRowKeys
    {
        public string name = "";
        public int category, ours, theirs;
        public float value, mass;
        public float? pay, ask, deal, market, trend, need;
        /// <summary>The trader won't deal in it: it goes under a heading of its own at the list's end.</summary>
        public bool off;
    }

    /// <summary>A line of one of the window's lists, in the order drawn: a kind's heading (<see cref="row"/> -1, with how many rows it heads) or a row.</summary>
    public struct ListLine
    {
        /// <summary>The row's index in the keys given, or -1 for a heading.</summary>
        public int row;
        /// <summary>The kind (a TradeCategory as a number), or <see cref="TradeWindowMath.OffGroup"/> for what the trader won't deal in.</summary>
        public int group;
        /// <summary>A heading's rows.</summary>
        public int count;
    }

    /// <summary>One good in the deal as the summary counts it: so many sold or bought at the trader's price, the usual price and its market value a piece.</summary>
    public struct DealLine
    {
        public bool sold;
        public int count;
        public float price, usual, value;

        public DealLine(bool sold, int count, float price, float usual, float value)
        {
            this.sold = sold;
            this.count = count;
            this.price = price;
            this.usual = usual;
            this.value = value;
        }
    }

    /// <summary>What a deal comes to, both ways: pieces, their market value, the silver at the trader's prices and at the usual ones.</summary>
    public sealed class DealSums
    {
        public int soldPieces, boughtPieces, soldGoods, boughtGoods;
        public float soldValue, boughtValue, soldSilver, boughtSilver, soldUsual, boughtUsual;

        public bool Any => soldPieces > 0 || boughtPieces > 0;

        /// <summary>The silver the colony comes out with from the goods (sales less purchases).</summary>
        public float Net => soldSilver - boughtSilver;

        public float UsualNet => soldUsual - boughtUsual;

        /// <summary>
        /// How much better for the colony this deal is than the usual prices, as a share of what the deal would come to at
        /// them: 0.12 is "about 12% better than usual". A sale alone is its price over the usual less one, a purchase
        /// alone one less the price over the usual; both, weighed by what each comes to.
        /// </summary>
        public float VsUsual
        {
            get
            {
                float turnover = soldUsual + boughtUsual;
                return turnover <= 0.0001f ? 0f : (Net - UsualNet) / turnover;
            }
        }

        /// <summary>By market value: what the colony gives (the goods sold and the silver paid) and gets (the goods bought and the silver paid it).</summary>
        public float GiveWorth => soldValue + boughtSilver;

        public float GetWorth => boughtValue + soldSilver;

        /// <summary>A share of the goods' market value paid for them: 0.62 is 62%; 0 with none.</summary>
        public float SoldShare => soldValue <= 0.0001f ? 0f : soldSilver / soldValue;

        public float BoughtShare => boughtValue <= 0.0001f ? 0f : boughtSilver / boughtValue;
    }

    /// <summary>A column's place in the window: how wide it is at least, at most, and how soon it is dropped when the window is narrow (lower stays longer).</summary>
    public struct ColumnSpec
    {
        public TradeColumn col;
        public float width, max;
        public int priority;

        public ColumnSpec(TradeColumn col, float width, int priority, float max = 0f)
        {
            this.col = col;
            this.width = width;
            this.priority = priority;
            this.max = max > 0f ? max : width;
        }
    }

    /// <summary>A column as laid out: where it starts and how wide it is.</summary>
    public struct ColumnPlace
    {
        public TradeColumn col;
        public float x, width;
    }

    /// <summary>
    /// Rimfolk's own trade window as plain rules (Trade/Window_RimfolkTrade.cs reads the game's deal, the advice, Vanilla
    /// Trading Expanded's market and the colony's record into these): each row's grade a side at a time, the shares its
    /// prices are of its market value and of the usual price, the order a column's click puts the rows in, the kinds they
    /// are grouped under, what the deal comes to, the best deals, the two lists side by side or as tabs and the columns
    /// that fit each, the rows' heights, the filter chips' rows, where a list scrolls to show a row, and the counts each
    /// list's control sets one way. Nothing here changes a trade; every count and price is the game's.
    /// <para>
    /// A grade is read from the price against the usual one for this negotiator and difficulty (the game's 60% and 140%
    /// of a thing's worth, moved by them; TradeAdviceMath's usual), since against its market value alone a sale is always
    /// under and a purchase always over: 1 is the usual price, a fifth better a great deal, 7% better a good one, within
    /// 7% fair, worse poor. Vanilla Trading Expanded's market moves it a step either way (a sale at a 60-day high, or of a
    /// good falling fast, is better made now; at a low it can wait; a purchase the other way round), and so does the
    /// colony's own record (better than its best of the last 15 days, or a tenth worse). A purchase the colony has no use
    /// for, or a sale of what it is short of or keeps, is never more than fair.
    /// </para>
    /// </summary>
    public static class TradeWindowMath
    {
        // ---------------- The grades ----------------

        /// <summary>The price against the usual at which a side is great, good and fair (below fair, poor). A fifth better is the advice's own good sale.</summary>
        public const float GreatAt = 1.2f, GoodAt = 1.07f, FairAt = 0.93f;
        /// <summary>One step of the market or the record, in the price against the usual.</summary>
        public const float Step = 0.05f;
        /// <summary>Better than the colony's best of the last 15 days by this share is a step up; worse by this share a step down.</summary>
        public const float RecordBeat = 0.02f, RecordBehind = 0.1f;

        /// <summary>The grade of a price against the usual (1 as usual, more is better), before the caps.</summary>
        public static DealGrade GradeOf(float score) =>
            score >= GreatAt ? DealGrade.Great : score >= GoodAt ? DealGrade.Good : score >= FairAt ? DealGrade.Fair : DealGrade.Poor;

        /// <summary>The market's step for the reason it gives (TradeMarketMath.Hint): +1 for now, -1 for wait.</summary>
        public static int MarketStep(MarketHint hint)
        {
            switch (hint)
            {
                case MarketHint.HighSellNow:
                case MarketHint.FallingSellSooner:
                case MarketHint.LowBuyNow:
                case MarketHint.RisingBuySooner:
                    return 1;
                case MarketHint.LowSellCanWait:
                case MarketHint.HighBuyCanWait:
                    return -1;
                default:
                    return 0;
            }
        }

        /// <summary>The record's step: the price against the colony's best of the last 15 days on this side (more got for a sale, less paid for a purchase is better).</summary>
        public static int RecordStep(bool sale, float price, float best)
        {
            if (best <= 0f || price <= 0f) return 0;
            float r = sale ? price / best : best / price;
            return r >= 1f + RecordBeat ? 1 : r <= 1f - RecordBehind ? -1 : 0;
        }

        /// <summary>A side's grade.</summary>
        public static DealRating Rate(DealInput d)
        {
            var r = new DealRating();
            if (!d.graded || d.price <= 0f || d.usual <= 0f) return r;
            r.vsUsual = d.price / d.usual - 1f;
            float deal = d.sale ? d.price / d.usual : d.usual / d.price;
            r.marketStep = Math.Sign(d.market);
            r.recordStep = RecordStep(d.sale, d.price, d.best);
            r.score = deal + Step * (r.marketStep + r.recordStep);
            r.grade = GradeOf(r.score);
            bool hold = d.sale ? d.kept : !d.use;
            if (hold && r.grade > DealGrade.Fair)
            {
                r.grade = DealGrade.Fair;
                r.capped = true;
            }
            return r;
        }

        /// <summary>The bars a grade fills of three: great three, good two, fair one, poor none (the word says it too).</summary>
        public static int Bars(DealGrade g) => g == DealGrade.Great ? 3 : g == DealGrade.Good ? 2 : g == DealGrade.Fair ? 1 : 0;

        /// <summary>A side's deal as a list sorts it: the grade first, then the score; null without a grade.</summary>
        public static float? SideKey(DealRating r) => r.Graded ? (int)r.grade * 100f + Math.Min(99f, Math.Max(0f, r.score * 10f)) : (float?)null;

        // ---------------- The shares ----------------

        /// <summary>A price as a share of its market value, in whole percent (62); -1 with no value.</summary>
        public static int ShareOf(float price, float value) => value <= 0f || price < 0f ? -1 : (int)Math.Round(price / value * 100f, MidpointRounding.AwayFromZero);

        /// <summary>A price against the usual, in whole percent either way (12 above, -10 under); 0 with no usual.</summary>
        public static int VsUsual(float price, float usual) => usual <= 0f ? 0 : (int)Math.Round((price / usual - 1f) * 100f, MidpointRounding.AwayFromZero);

        /// <summary>A signed share as the cells show it: "+12%", "-10%", "0%".</summary>
        public static string Signed(int pct) => (pct > 0 ? "+" : "") + pct + "%";

        /// <summary>How a price against the usual reads for the colony: +1 better (a sale above it, a purchase under it by a good deal's margin), -1 worse, 0 about usual.</summary>
        public static int Better(bool sale, float price, float usual)
        {
            if (usual <= 0f || price <= 0f) return 0;
            float deal = sale ? price / usual : usual / price;
            return deal >= GoodAt ? 1 : deal < FairAt ? -1 : 0;
        }

        // ---------------- The sort ----------------

        /// <summary>A column's first direction: names from A, numbers the highest first.</summary>
        public static bool DefaultDescending(TradeColumn c) => c != TradeColumn.Name;

        /// <summary>Whether a column sorts on a click (the count control doesn't).</summary>
        public static bool Sortable(TradeColumn c) => c != TradeColumn.Count;

        private static float? KeyOf(TradeRowKeys k, TradeColumn c)
        {
            switch (c)
            {
                case TradeColumn.Value: return k.value;
                case TradeColumn.Ours: return k.ours;
                case TradeColumn.Theirs: return k.theirs;
                case TradeColumn.Pay: return k.pay;
                case TradeColumn.Ask: return k.ask;
                case TradeColumn.Deal: return k.deal;
                case TradeColumn.Market: return k.market;
                case TradeColumn.Trend: return k.trend;
                case TradeColumn.Mass: return k.mass;
                case TradeColumn.Need: return k.need;
                default: return null;
            }
        }

        private static int Names(TradeRowKeys a, TradeRowKeys b) => string.Compare(a?.name ?? "", b?.name ?? "", StringComparison.CurrentCultureIgnoreCase);

        /// <summary>
        /// The rows in a column's order (their indices): names alphabetically, numbers by size; a row without that key last
        /// either way; ties by name, then as they came.
        /// </summary>
        public static List<int> Order(IList<TradeRowKeys> rows, TradeColumn col, bool descending)
        {
            var idx = Enumerable.Range(0, rows?.Count ?? 0).ToList();
            if (rows == null) return idx;
            int sign = descending ? -1 : 1;
            idx.Sort((i, j) =>
            {
                var a = rows[i];
                var b = rows[j];
                int c;
                switch (col)
                {
                    case TradeColumn.Name:
                        c = sign * Names(a, b);
                        break;
                    default:
                        float? ka = KeyOf(a, col), kb = KeyOf(b, col);
                        if (ka.HasValue != kb.HasValue) return ka.HasValue ? -1 : 1;
                        c = ka.HasValue ? sign * ka.Value.CompareTo(kb.Value) : 0;
                        break;
                }
                if (c == 0) c = Names(a, b);
                return c != 0 ? c : i.CompareTo(j);
            });
            return idx;
        }

        /// <summary>The group of what the trader won't deal in, after every kind.</summary>
        public const int OffGroup = -1;

        /// <summary>
        /// A list's lines: its rows under a heading for each kind, the kinds in their order (food first), each kind's rows
        /// in the column's order (<see cref="Order"/>); what the trader won't deal in last, under a heading of its own.
        /// </summary>
        public static List<ListLine> Grouped(IList<TradeRowKeys> rows, TradeColumn col, bool descending)
        {
            var lines = new List<ListLine>();
            if (rows == null || rows.Count == 0) return lines;
            var order = Order(rows, col, descending);
            foreach (var g in order.GroupBy(i => rows[i].off ? OffGroup : rows[i].category).OrderBy(g => g.Key == OffGroup ? int.MaxValue : g.Key))
            {
                var members = g.ToList();
                lines.Add(new ListLine { row = -1, group = g.Key, count = members.Count });
                foreach (int i in members) lines.Add(new ListLine { row = i, group = g.Key });
            }
            return lines;
        }

        // ---------------- What the deal comes to ----------------

        /// <summary>The deal's sums from its lines. A line with no usual price (people) counts at its own price there, so it neither helps nor hurts the comparison.</summary>
        public static DealSums Sum(IEnumerable<DealLine> lines)
        {
            var s = new DealSums();
            if (lines == null) return s;
            foreach (var l in lines)
            {
                if (l.count <= 0) continue;
                float price = Math.Max(0f, l.price), usual = l.usual > 0f ? l.usual : price, value = Math.Max(0f, l.value);
                if (l.sold)
                {
                    s.soldPieces += l.count;
                    s.soldGoods++;
                    s.soldValue += l.count * value;
                    s.soldSilver += l.count * price;
                    s.soldUsual += l.count * usual;
                }
                else
                {
                    s.boughtPieces += l.count;
                    s.boughtGoods++;
                    s.boughtValue += l.count * value;
                    s.boughtSilver += l.count * price;
                    s.boughtUsual += l.count * usual;
                }
            }
            return s;
        }

        /// <summary>Under this share either way the deal is "about the usual prices".</summary>
        public const float AboutUsual = 0.02f;

        /// <summary>The deal against the usual in words' terms: +1 better for the colony, -1 worse, 0 about as usual.</summary>
        public static int Leaning(DealSums s) => s == null || !s.Any || Math.Abs(s.VsUsual) < AboutUsual ? 0 : s.VsUsual > 0f ? 1 : -1;

        // ---------------- The best deals ----------------

        /// <summary>The best deals of one side: the rows graded good or great, the best first (grade, then score), so many at most. Returns their ids.</summary>
        public static List<int> Best(IList<(int id, DealRating rating)> sides, int max)
        {
            if (sides == null || max <= 0) return new List<int>();
            return sides.Where(s => s.rating.grade >= DealGrade.Good)
                .OrderByDescending(s => (int)s.rating.grade).ThenByDescending(s => s.rating.score).ThenBy(s => s.id)
                .Take(max).Select(s => s.id).ToList();
        }

        // ---------------- The layout ----------------

        /// <summary>
        /// The columns that fit a width, in their order on screen: each at its width, the ones dropped first (the highest
        /// priority number) left out until the rest fit; a column of priority 0 (the name, the count) always kept, at
        /// least its own width. What is left over goes to the name up to its widest, then to every column alike, so the
        /// table fills the width.
        /// </summary>
        public static List<ColumnPlace> Fit(IList<ColumnSpec> specs, float width, float gap)
        {
            var result = new List<ColumnPlace>();
            if (specs == null || specs.Count == 0) return result;
            var kept = new HashSet<int>(Enumerable.Range(0, specs.Count));
            float Need() => kept.Sum(i => specs[i].width) + gap * Math.Max(0, kept.Count - 1);
            foreach (int i in Enumerable.Range(0, specs.Count).OrderByDescending(i => specs[i].priority).ThenByDescending(i => i))
            {
                if (Need() <= width + 0.01f) break;
                if (specs[i].priority <= 0) continue;
                kept.Remove(i);
            }
            var widths = specs.Select(s => s.width).ToArray();
            float left = width - Need();
            if (left > 0f)
            {
                int name = Enumerable.Range(0, specs.Count).FirstOrDefault(i => specs[i].col == TradeColumn.Name && kept.Contains(i));
                if (specs[name].col == TradeColumn.Name)
                {
                    float grow = Math.Min(left, Math.Max(0f, specs[name].max - specs[name].width));
                    widths[name] += grow;
                    left -= grow;
                }
                if (left > 0f && kept.Count > 0)
                {
                    float each = left / kept.Count;
                    foreach (int i in kept) widths[i] += each;
                }
            }
            float x = 0f;
            for (int i = 0; i < specs.Count; i++)
            {
                if (!kept.Contains(i)) continue;
                result.Add(new ColumnPlace { col = specs[i].col, x = x, width = widths[i] });
                x += widths[i] + gap;
            }
            return result;
        }

        /// <summary>Chips of these widths laid out in rows within a width: where each starts (left to right, a new row where the next won't fit), and the height all the rows take.</summary>
        public static List<(float x, float y)> Flow(IList<float> widths, float avail, float gap, float rowH, out float height)
        {
            var places = new List<(float, float)>();
            height = 0f;
            if (widths == null || widths.Count == 0) return places;
            float x = 0f, y = 0f;
            foreach (float w in widths)
            {
                if (x > 0f && x + w > avail + 0.01f)
                {
                    x = 0f;
                    y += rowH + gap;
                }
                places.Add((x, y));
                x += w + gap;
            }
            height = y + rowH;
            return places;
        }

        /// <summary>Where a list scrolls to show a line (its top and height) in the middle of the view, kept within the list.</summary>
        public static float ScrollToSpan(float top, float height, float viewH, float contentH)
        {
            float y = top - (viewH - height) / 2f;
            return Math.Max(0f, Math.Min(y, Math.Max(0f, contentH - viewH)));
        }

        // ---------------- The two lists ----------------

        /// <summary>Between the two lists side by side; between a list's columns; the scroll bar a list keeps at its right.</summary>
        public const float ListGap = 12f, ColGap = 4f, Scrollbar = 16f;

        /// <summary>A row's least height (a one-line name beside a price and its mark), the room kept round a longer name, and a kind's heading.</summary>
        public const float RowMin = 40f, NamePad = 8f, GroupH = 26f;

        /// <summary>The name column's own parts: the picture and the space after it, and the info card's button.</summary>
        public const float PictureW = 40f, InfoW = 26f;

        /// <summary>
        /// A list's columns, left to right, and how soon each gives way in a narrow list (lower stays longer): the picture
        /// and name (always, wider when there's room), how many there are, the price with its mark against the usual
        /// (always), the deal, the need and the count control (always). The sell list shows ours and what they pay, the buy
        /// list theirs and what they ask. Made once; not to be changed.
        /// </summary>
        public static IList<ColumnSpec> ListColumns(bool sale) => sale ? sellColumns : buyColumns;

        private static readonly List<ColumnSpec> sellColumns = MakeColumns(true), buyColumns = MakeColumns(false);

        private static List<ColumnSpec> MakeColumns(bool sale) => new List<ColumnSpec>
        {
            new ColumnSpec(TradeColumn.Name, 176f, 0, 520f),
            new ColumnSpec(sale ? TradeColumn.Ours : TradeColumn.Theirs, 44f, 2),
            new ColumnSpec(sale ? TradeColumn.Pay : TradeColumn.Ask, 80f, 0),
            new ColumnSpec(TradeColumn.Deal, 60f, 1),
            new ColumnSpec(TradeColumn.Need, 70f, 3),
            new ColumnSpec(TradeColumn.Count, 136f, 0)
        };

        /// <summary>The width columns take at their least with the gaps between them: all of them, or only those that never give way.</summary>
        public static float ColumnsWidth(IList<ColumnSpec> specs, float gap, bool essentialOnly)
        {
            if (specs == null) return 0f;
            var kept = specs.Where(s => !essentialOnly || s.priority <= 0).ToList();
            return kept.Sum(s => s.width) + gap * Math.Max(0, kept.Count - 1);
        }

        /// <summary>A list's own width beside the other's or alone.</summary>
        public static float ListWidth(float table, bool sideBySide) => sideBySide ? Math.Max(0f, (table - ListGap) / 2f) : table;

        /// <summary>A list's rows' width: its own less the scroll bar.</summary>
        public static float RowsWidth(float list) => Math.Max(0f, list - Scrollbar);

        private static readonly float allColumns = ColumnsWidth(sellColumns, ColGap, false), essentialColumns = ColumnsWidth(sellColumns, ColGap, true);

        /// <summary>
        /// The two lists side by side, or as tabs: by width, side by side when each has room for all its columns; chosen
        /// side by side, wherever each has room for the name, the price and the count; chosen as tabs, always tabs.
        /// </summary>
        public static bool SideBySide(float table, TradeListsChoice choice)
        {
            if (choice == TradeListsChoice.Tabs) return false;
            float rows = RowsWidth(ListWidth(table, true));
            return rows + 0.01f >= (choice == TradeListsChoice.SideBySide ? essentialColumns : allColumns);
        }

        /// <summary>A row's height for its name's (wrapped to its column, never cut): at least the row's least, else the name and room round it.</summary>
        public static float RowHeight(float nameHeight) => Math.Max(RowMin, nameHeight + NamePad);

        // ---------------- One way: each list's count control ----------------

        /// <summary>
        /// How many of a good go a list's way: sold in the sell list, bought in the buy list; 0 when it goes the other way.
        /// The game keeps one count a good, positive for a purchase and negative for a sale.
        /// </summary>
        public static int ThisWay(int count, bool sale) => sale ? Math.Max(0, -count) : Math.Max(0, count);

        /// <summary>The good is in the deal the other way: being bought, in the sell list; being sold, in the buy list.</summary>
        public static bool OtherWay(int count, bool sale) => sale ? count > 0 : count < 0;

        /// <summary>The most that can go a list's way, from the game's limits: all the colony has to sell, all the trader has to sell it.</summary>
        public static int MostThisWay(bool sale, int least, int most) => sale ? Math.Max(0, -least) : Math.Max(0, most);

        /// <summary>The game's count for so many going a list's way, kept between none and the most (never over to the other way).</summary>
        public static int CountFor(bool sale, int n, int least, int most)
        {
            n = Math.Max(0, Math.Min(n, MostThisWay(sale, least, most)));
            return sale ? -n : n;
        }

        /// <summary>An arrow's count: <paramref name="step"/> more going the list's way (one, ten with Shift, a hundred with Ctrl), or fewer, kept between none and the most.</summary>
        public static int Stepped(bool sale, int count, bool more, int step, int least, int most)
        {
            long n = ThisWay(count, sale) + (more ? (long)step : -(long)step);
            return CountFor(sale, (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, n)), least, most);
        }

        // ---------------- The kinds ----------------

        /// <summary>What a thing is, for its kind (read from the game's def and thing).</summary>
        public struct Facts
        {
            public bool person, animal, medicine, drug, food, weapon, apparel, art, book, raw, made;
        }

        /// <summary>A thing's kind: people and animals first, then medicine and drugs (before food, since some are eaten), food, weapons, apparel, art and books, raw materials, what is made, and the rest.</summary>
        public static TradeCategory CategoryOf(Facts f)
        {
            if (f.person) return TradeCategory.People;
            if (f.animal) return TradeCategory.Animals;
            if (f.medicine) return TradeCategory.Medicine;
            if (f.drug) return TradeCategory.Drugs;
            if (f.food) return TradeCategory.Food;
            if (f.weapon) return TradeCategory.Weapons;
            if (f.apparel) return TradeCategory.Apparel;
            if (f.art || f.book) return TradeCategory.ArtBooks;
            if (f.raw) return TradeCategory.Raw;
            if (f.made) return TradeCategory.Manufactured;
            return TradeCategory.Other;
        }
    }
}
