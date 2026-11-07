using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>A good's market as Vanilla Trading Expanded keeps it, read into plain numbers (TradeMarketMath.Stats).</summary>
    public sealed class MarketStats
    {
        /// <summary>The market keeps a history of it (one price at least).</summary>
        public bool known;
        /// <summary>Days of history: the prices it keeps, one a day.</summary>
        public int days;
        /// <summary>Its market price now and its usual (the game's own) worth.</summary>
        public float now, usual;
        /// <summary>The price now against its usual worth: 0.2 is a fifth above it.</summary>
        public float vsUsual;
        /// <summary>The average of its last days (up to 30), the days it is over, and the price now against it.</summary>
        public float average, vsAverage;
        public int averageDays;
        /// <summary>The change over the last days (up to 7, up to 30): 0.1 is a tenth up; the days each is over (0: too few to say).</summary>
        public float week, month;
        public int weekDays, monthDays;
        /// <summary>The highest and lowest of its last days (up to 60, and now), and the days they are over.</summary>
        public float high, low;
        public int rangeDays;
        /// <summary>At its highest or lowest of those days, where the days are enough and the prices moved enough to say so.</summary>
        public bool atHigh, atLow;
        /// <summary>Where it is going: +1 rising notably, -1 falling notably, 0 neither (TradeMarketMath.Heading).</summary>
        public int heading;
    }

    /// <summary>How this trade, or the colony's trades of a good lately, are likely to move its market price.</summary>
    public enum ImpactLevel
    {
        None,
        May,
        Likely,
        Sure
    }

    public struct MarketImpact
    {
        public ImpactLevel level;
        /// <summary>+1 the price goes up (the colony bought), -1 down (it sold).</summary>
        public int way;
        /// <summary>By several times the usual step (a big trade).</summary>
        public bool sharp;
        /// <summary>This trade evens out what the colony traded of it lately, so the price is unlikely to move.</summary>
        public bool evensOut;
        /// <summary>Nothing of it is in this trade: what is said is what the colony traded of it lately.</summary>
        public bool lately;
    }

    /// <summary>A reason from the market for a row's mark (Trade/TradeAdvice.cs): a sale or a buy made better or worse by where its price stands.</summary>
    public enum MarketHint
    {
        None,
        /// <summary>A sale at the highest price of the last 60 days.</summary>
        HighSellNow,
        /// <summary>A sale of a good whose price is falling fast: worth less the longer it is held.</summary>
        FallingSellSooner,
        /// <summary>A sale at the lowest price of the last 60 days: what isn't needed may fetch more later.</summary>
        LowSellCanWait,
        /// <summary>A buy at the lowest price of the last 60 days.</summary>
        LowBuyNow,
        /// <summary>A buy of a good whose price is rising fast: dearer later.</summary>
        RisingBuySooner,
        /// <summary>A buy at the highest price of the last 60 days: what can wait may cost less later.</summary>
        HighBuyCanWait
    }

    /// <summary>One line of the colony's own trade record, as the rules read it.</summary>
    public struct TradeRecordEntry
    {
        public int tick;
        public bool sold;
        public float price;
        /// <summary>Its quality (0 to 6), -1 for a good without one.</summary>
        public int quality;

        public TradeRecordEntry(int tick, bool sold, float price, int quality = -1)
        {
            this.tick = tick;
            this.sold = sold;
            this.price = price;
            this.quality = quality;
        }
    }

    /// <summary>
    /// The trade screen's market and the colony's own trade record as plain rules (Trade/TradeMarket.cs reads Vanilla
    /// Trading Expanded and the record into these): a good's price now against its usual worth and its average of the
    /// last 30 days, its change over the last 7 and 30 days, its high and low of the last 60, whether it moved notably,
    /// what Vanilla Trading Expanded's own rule for the colony's trades makes of this trade, the market's reasons for a
    /// mark, and the record's caps. Nothing here changes a price or a trade.
    /// <para>
    /// Vanilla Trading Expanded keeps one price a day of each good it trades in (its market value at the start of the
    /// day). The price "so many days ago" is the one it kept that many records back, as its own terminal reads it. Every
    /// three days it adds up the market worth of what the colony sold (less what it bought) of each good since the last
    /// time, and moves the price with a chance of that worth over 1,000 silver (all of it at 1,000 or more), down for a
    /// sale and up for a purchase, by 1% to 10% (several times that past 1,000 silver, up to ten times), scaled by its
    /// setting.
    /// </para>
    /// </summary>
    public static class TradeMarketMath
    {
        // ---------------- The market's history ----------------

        public const int WeekDays = 7, MonthDays = 30, RangeDays = 60, AverageDays = 30;
        /// <summary>A move this big over the last 7 days, or the last 30, is notable (the row's trend mark, the price change sort's top).</summary>
        public const float WeekMove = 0.1f, MonthMove = 0.2f;
        /// <summary>Days of history a move needs before it is called one: a week's over 3 days at least, a month's over 10.</summary>
        public const int WeekLeast = 3, MonthLeast = 10;
        /// <summary>Within this share of its 60-day high or low is "at" it, where those days are 10 or more and the prices moved a tenth between them.</summary>
        public const float AtExtreme = 0.02f, ExtremeSpread = 0.1f;
        public const int ExtremeLeast = 10;
        /// <summary>A change under this is "steady" in the words (a whole percent rounds it).</summary>
        public const float Steady = 0.02f;

        /// <summary>
        /// A good's market from its history (<paramref name="records"/>, one price a day, oldest first), its price now and its
        /// usual worth. With no history, <c>known</c> is false and nothing else is said.
        /// </summary>
        public static MarketStats Stats(IList<float> records, float now, float usual)
        {
            var s = new MarketStats { now = now, usual = usual };
            int n = records?.Count ?? 0;
            if (n == 0 || now <= 0f) return s;
            s.known = true;
            s.days = n;
            s.vsUsual = usual > 0f ? now / usual - 1f : 0f;
            // The average of the last days.
            int a = Math.Min(AverageDays, n);
            double sum = 0;
            for (int i = n - a; i < n; i++) sum += records[i];
            s.average = (float)(sum / a);
            s.averageDays = a;
            s.vsAverage = s.average > 0f ? now / s.average - 1f : 0f;
            // The change over the last 7 and 30 days.
            s.week = Change(records, now, WeekDays, out s.weekDays);
            s.month = Change(records, now, MonthDays, out s.monthDays);
            // The high and the low of the last 60 days, and now.
            int r = Math.Min(RangeDays, n);
            float hi = now, lo = now;
            for (int i = n - r; i < n; i++)
            {
                float v = records[i];
                if (v <= 0f) continue;
                if (v > hi) hi = v;
                if (v < lo) lo = v;
            }
            s.high = hi;
            s.low = lo;
            s.rangeDays = r;
            bool spread = r >= ExtremeLeast && lo > 0f && hi / lo - 1f >= ExtremeSpread;
            s.atHigh = spread && now >= hi * (1f - AtExtreme);
            s.atLow = spread && !s.atHigh && now <= lo * (1f + AtExtreme);
            s.heading = Heading(s);
            return s;
        }

        /// <summary>
        /// The change from the price kept <paramref name="days"/> records back to now: 0.1 is a tenth up. With fewer records,
        /// from the oldest; <paramref name="over"/> is the days it is over, 0 with fewer than two.
        /// </summary>
        public static float Change(IList<float> records, float now, int days, out int over)
        {
            over = 0;
            int n = records?.Count ?? 0;
            if (n < 2 || days <= 0) return 0f;
            int back = Math.Min(days, n);
            float then = records[n - back];
            if (then <= 0f) return 0f;
            over = back;
            return now / then - 1f;
        }

        /// <summary>Where the price is going: the week's move where it is notable, else the month's; 0 for neither.</summary>
        public static int Heading(MarketStats s)
        {
            if (s == null || !s.known) return 0;
            if (s.weekDays >= WeekLeast && Math.Abs(s.week) >= WeekMove) return s.week > 0f ? 1 : -1;
            if (s.monthDays >= MonthLeast && Math.Abs(s.month) >= MonthMove) return s.month > 0f ? 1 : -1;
            return 0;
        }

        /// <summary>How much it moved, for the price change sort: the larger of its week's and its month's move, either way; -1 with no history.</summary>
        public static float MoveScore(MarketStats s)
        {
            if (s == null || !s.known || s.weekDays == 0 && s.monthDays == 0) return -1f;
            return Math.Max(Math.Abs(s.week), Math.Abs(s.month));
        }

        /// <summary>A change in whole percent: 0.183 is 18, -0.05 is -5.</summary>
        public static int Percent(float change) => (int)Math.Round(change * 100f, MidpointRounding.AwayFromZero);

        /// <summary>Steady in the words: under 2% either way.</summary>
        public static bool IsSteady(float change) => Math.Abs(change) < Steady;

        /// <summary>The last <paramref name="count"/> prices and now, oldest first, for the drawn line.</summary>
        public static List<float> Points(IList<float> records, float now, int count = RangeDays)
        {
            var list = new List<float>();
            int n = records?.Count ?? 0;
            for (int i = Math.Max(0, n - Math.Max(1, count)); i < n; i++) list.Add(records[i]);
            if (now > 0f) list.Add(now);
            return list;
        }

        // ---------------- This trade's own effect, by Vanilla Trading Expanded's rule ----------------

        /// <summary>VTE: the chance a good's price moves is the market worth the colony traded of it over this much silver.</summary>
        public const float SilverForCertain = 1000f;
        /// <summary>The step grows past this much silver, up to ten times.</summary>
        public const float SilverPerStep = 1000f, MaxSteps = 10f;
        /// <summary>The words' thresholds on that chance: "may" from a tenth, "likely" from two in five, "almost surely" from four in five.</summary>
        public const float MayChance = 0.1f, LikelyChance = 0.4f, SureChance = 0.8f;
        /// <summary>"Sharply" from three times the usual step (with VTE's own setting for it).</summary>
        public const float SharpSteps = 3f;

        /// <summary>The chance VTE moves the price for this net market worth traded (a sale positive, a purchase negative).</summary>
        public static float Chance(float net) => Math.Min(1f, Math.Abs(net) / SilverForCertain);

        /// <summary>How many of VTE's usual steps the move is, for this net worth: 1 up to 1,000 silver, ten at most.</summary>
        public static float Steps(float net) => Math.Min(MaxSteps, Math.Max(1f, Math.Abs(net) / SilverPerStep));

        public static ImpactLevel Level(float net, float multiplier)
        {
            if (multiplier <= 0f) return ImpactLevel.None;
            float c = Chance(net);
            return c >= SureChance ? ImpactLevel.Sure : c >= LikelyChance ? ImpactLevel.Likely : c >= MayChance ? ImpactLevel.May : ImpactLevel.None;
        }

        /// <summary>
        /// What VTE's rule makes of this trade: <paramref name="lately"/> is the net market worth the colony traded of the good
        /// since VTE last reckoned (sold positive), <paramref name="thisTrade"/> this trade's, <paramref name="multiplier"/>
        /// VTE's own setting for how much the colony's trades move prices.
        /// </summary>
        public static MarketImpact Impact(float lately, float thisTrade, float multiplier)
        {
            var r = new MarketImpact();
            float total = lately + thisTrade;
            if (multiplier <= 0f) return r;
            if (Math.Abs(thisTrade) < 0.01f)
            {
                r.lately = true;
                r.level = Level(lately, multiplier);
                r.way = lately > 0f ? -1 : 1;
                r.sharp = Steps(lately) * multiplier >= SharpSteps;
                if (r.level == ImpactLevel.None) r.way = 0;
                return r;
            }
            r.level = Level(total, multiplier);
            r.way = total > 0f ? -1 : 1;
            r.sharp = Steps(total) * multiplier >= SharpSteps;
            // Against what was traded lately, and leaving less of a move than there was: it evens it out.
            if (Math.Sign(thisTrade) != Math.Sign(lately) && Level(lately, multiplier) > r.level) r.evensOut = true;
            // What was traded lately still outweighs this trade: the move is theirs, whichever way this trade goes.
            if (r.level != ImpactLevel.None && Math.Sign(total) != Math.Sign(thisTrade)) r.lately = true;
            if (r.level == ImpactLevel.None) r.way = 0;
            return r;
        }

        // ---------------- The market's reasons for a mark ----------------

        /// <summary>
        /// The market's reason for a sale (<paramref name="sale"/>) or a buy already marked: at its 60-day high or low first,
        /// then a fast move (a sale of a falling good is better made now; a buy of a rising one).
        /// </summary>
        public static MarketHint Hint(MarketStats s, bool sale)
        {
            if (s == null || !s.known) return MarketHint.None;
            if (sale)
            {
                if (s.atHigh) return MarketHint.HighSellNow;
                if (s.heading < 0) return MarketHint.FallingSellSooner;
                if (s.atLow) return MarketHint.LowSellCanWait;
            }
            else
            {
                if (s.atLow) return MarketHint.LowBuyNow;
                if (s.heading > 0) return MarketHint.RisingBuySooner;
                if (s.atHigh) return MarketHint.HighBuyCanWait;
            }
            return MarketHint.None;
        }

        // ---------------- The colony's own trade record ----------------

        /// <summary>The record keeps the newest few lines of each good, and so many goods, the one traded longest ago going first.</summary>
        public const int MaxLinesPerGood = 5, MaxGoods = 50;
        /// <summary>The best price is the best of the last 15 days (a season, as the trade advice counts one).</summary>
        public const int BestDays = 15;
        public const int TicksADay = 60000;
        /// <summary>A trader's name kept at most this long.</summary>
        public const int MaxWho = 48;

        /// <summary>How many of a good's lines to drop from the end (the oldest, the lines kept newest first).</summary>
        public static int Overflow(int lines, int max = MaxLinesPerGood) => Math.Max(0, lines - Math.Max(1, max));

        /// <summary>The good to let go when the record is full: the one whose newest line is the oldest (the first of those). -1 while there is room.</summary>
        public static int ToDrop(IList<int> newestTicks, int max = MaxGoods)
        {
            if (newestTicks == null || newestTicks.Count <= Math.Max(1, max)) return -1;
            int at = 0;
            for (int i = 1; i < newestTicks.Count; i++)
                if (newestTicks[i] < newestTicks[at]) at = i;
            return at;
        }

        /// <summary>
        /// The best line of one side in the last <see cref="BestDays"/> days: the highest price got for a sale, the lowest
        /// paid for a purchase, of the same quality where <paramref name="quality"/> is one (-1: any). -1 for none.
        /// </summary>
        public static int Best(IList<TradeRecordEntry> lines, bool sold, int now, int quality = -1)
        {
            int at = -1;
            if (lines == null) return at;
            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                if (l.sold != sold || now - l.tick > BestDays * TicksADay || l.price <= 0f) continue;
                if (quality >= 0 && l.quality >= 0 && l.quality != quality) continue;
                if (at < 0 || (sold ? l.price > lines[at].price : l.price < lines[at].price)) at = i;
            }
            return at;
        }

        /// <summary>The newest line of one side, of the same quality where there is one of it, else of any. -1 for none.</summary>
        public static int Last(IList<TradeRecordEntry> lines, bool sold, int quality = -1)
        {
            if (lines == null) return -1;
            int any = -1, same = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                if (l.sold != sold) continue;
                if (any < 0 || l.tick > lines[any].tick) any = i;
                if ((quality < 0 || l.quality < 0 || l.quality == quality) && (same < 0 || l.tick > lines[same].tick)) same = i;
            }
            return same >= 0 ? same : any;
        }

        /// <summary>Whole days between a tick and now.</summary>
        public static int DaysAgo(int tick, int now) => Math.Max(0, (now - tick) / TicksADay);

        /// <summary>Several rows of one good in one trade, as one line: the pieces added up, the price the average a piece.</summary>
        public static float MergedPrice(float priceA, int countA, float priceB, int countB)
        {
            int n = Math.Max(0, countA) + Math.Max(0, countB);
            return n == 0 ? 0f : (priceA * Math.Max(0, countA) + priceB * Math.Max(0, countB)) / n;
        }

        /// <summary>A trader's name, cut to fit the record.</summary>
        public static string Who(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            name = name.Trim();
            return name.Length <= MaxWho ? name : name.Substring(0, MaxWho).TrimEnd() + "...";
        }
    }
}
