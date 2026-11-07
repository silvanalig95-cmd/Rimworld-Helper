using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One trade of one good: when, which way, how many, the price a piece the colony got or paid, and with whom.</summary>
    public class TradeLine : IExposable
    {
        public int tick;
        public bool sold;
        public int count;
        public float price;
        public string who = "";
        /// <summary>Its quality (0 to 6), -1 for a good without one.</summary>
        public int quality = -1;

        public TradeRecordEntry Entry => new TradeRecordEntry(tick, sold, price, quality);

        public void ExposeData()
        {
            // Short names: the record holds a few hundred of these at most.
            Scribe_Values.Look(ref tick, "t");
            Scribe_Values.Look(ref sold, "s");
            Scribe_Values.Look(ref count, "n");
            Scribe_Values.Look(ref price, "p");
            Scribe_Values.Look(ref who, "w", "");
            Scribe_Values.Look(ref quality, "q", -1);
        }
    }

    /// <summary>A good (a thing of a stuff, or of none) in the colony's trade record, its lines newest first.</summary>
    public class TradeGoodLog : IExposable
    {
        public ThingDef def, stuff;
        public List<TradeLine> lines = new List<TradeLine>();

        public int Newest => lines.Count > 0 ? lines[0].tick : int.MinValue;

        public List<TradeRecordEntry> Entries => lines.Select(l => l.Entry).ToList();

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Defs.Look(ref stuff, "stuff");
            Scribe_Collections.Look(ref lines, "lines", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                lines ??= new List<TradeLine>();
                lines.RemoveAll(l => l == null);
            }
        }
    }

    /// <summary>
    /// The colony's own trade record (Trade/; DESIGN.md "The trade screen"): what it sold and bought, to whom and at what
    /// price a piece, written when a trade goes through (the game's TradeDeal.TryExecute, the hook Epithets/CouncilRecords.cs
    /// already has for the negotiator's count of trades). Kept small: the newest <see cref="TradeMarketMath.MaxLinesPerGood"/>
    /// lines of a good and <see cref="TradeMarketMath.MaxGoods"/> goods, the good traded longest ago going first. Silver
    /// trades only (a price in favor isn't a price a piece), never gifts, never people or animals. Read by the trade
    /// screen's market card and Market window (Trade/TradeMarket.cs); nothing else. A save from before has an empty one.
    /// </summary>
    public class TradeBook : GameComponent
    {
        public List<TradeGoodLog> goods = new List<TradeGoodLog>();

        private static TradeBook cached;
        private static Game cachedGame;

        public TradeBook(Game game)
        {
        }

        public static TradeBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<TradeBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref goods, "goods", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                goods ??= new List<TradeGoodLog>();
                goods.RemoveAll(g => g == null || g.def == null || g.lines == null || g.lines.Count == 0);
            }
        }

        /// <summary>The record's switch: on, and the deeper game hooks on (it is written by one).</summary>
        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.tradeRecord ?? true);

        /// <summary>A good's log, or null.</summary>
        public TradeGoodLog For(ThingDef def, ThingDef stuff)
        {
            if (def == null) return null;
            for (int i = 0; i < goods.Count; i++)
                if (goods[i].def == def && goods[i].stuff == stuff)
                    return goods[i];
            return null;
        }

        /// <summary>A good's log of any stuff: the one traded most lately (the Market window's line for a kind of thing).</summary>
        public TradeGoodLog Latest(ThingDef def)
        {
            TradeGoodLog best = null;
            for (int i = 0; i < goods.Count; i++)
                if (goods[i].def == def && (best == null || goods[i].Newest > best.Newest))
                    best = goods[i];
            return best;
        }

        /// <summary>Adds one line, the newest first, and keeps the record within its caps.</summary>
        public void Add(ThingDef def, ThingDef stuff, TradeLine line)
        {
            if (def == null || line == null || line.count <= 0) return;
            var log = For(def, stuff);
            if (log == null)
            {
                log = new TradeGoodLog { def = def, stuff = stuff };
                goods.Add(log);
            }
            log.lines.Insert(0, line);
            int over = TradeMarketMath.Overflow(log.lines.Count);
            if (over > 0) log.lines.RemoveRange(log.lines.Count - over, over);
            for (int drop = TradeMarketMath.ToDrop(goods.Select(g => g.Newest).ToList()); drop >= 0; drop = TradeMarketMath.ToDrop(goods.Select(g => g.Newest).ToList()))
                goods.RemoveAt(drop);
        }
    }

    /// <summary>
    /// What the open trade sells and buys, taken just before the game carries it out (the counts are gone after it) and
    /// written into the colony's trade record once the game says it went through.
    /// </summary>
    public static class TradeRecord
    {
        private struct Pending
        {
            public ThingDef def, stuff;
            public bool sold;
            public int count, quality;
            public float price;
        }

        private static readonly List<Pending> taken = new List<Pending>();
        private static string takenWho;

        /// <summary>Who the colony traded with, in a few words: the town, the ship, or the people of a caravan.</summary>
        public static string WhoOf(ITrader trader)
        {
            if (trader == null) return "";
            try
            {
                switch (trader)
                {
                    case Settlement s: return TradeMarketMath.Who(s.Label);
                    case Pawn p: return TradeMarketMath.Who(p.Faction != null && !p.Faction.IsPlayer ? p.Faction.Name : p.LabelShort);
                    default: return TradeMarketMath.Who(trader.TraderName);
                }
            }
            catch
            {
                return "";
            }
        }

        /// <summary>Before the trade is carried out: each good sold or bought, at the price a piece the trade gives for it.</summary>
        public static void Take()
        {
            taken.Clear();
            takenWho = null;
            if (!TradeBook.On || !TradeSession.Active || TradeSession.giftMode || TradeSession.TradeCurrency != TradeCurrency.Silver) return;
            var deal = TradeSession.deal;
            if (deal == null) return;
            try
            {
                foreach (var t in deal.AllTradeables)
                {
                    if (t == null || t.IsCurrency || t.IsFavor || t.CountToTransfer == 0 || !t.HasAnyThing || t is Tradeable_Pawn || t.AnyThing is Pawn) continue;
                    var thing = t.AnyThing.GetInnerIfMinified();
                    var action = t.ActionToDo;
                    if (action != TradeAction.PlayerBuys && action != TradeAction.PlayerSells) continue;
                    float price = t.GetPriceFor(action);
                    if (price <= 0f) continue;
                    int q = thing.TryGetQuality(out var qc) ? (int)qc : -1;
                    bool sold = action == TradeAction.PlayerSells;
                    int count = Math.Abs(t.CountToTransfer);
                    // Two rows of one good, one way, one quality (hit points apart) are one line, at the average price a piece.
                    int at = taken.FindIndex(p => p.def == thing.def && p.stuff == thing.Stuff && p.sold == sold && p.quality == q);
                    if (at >= 0)
                    {
                        var p = taken[at];
                        p.price = TradeMarketMath.MergedPrice(p.price, p.count, price, count);
                        p.count += count;
                        taken[at] = p;
                    }
                    else taken.Add(new Pending { def = thing.def, stuff = thing.Stuff, sold = sold, count = count, quality = q, price = price });
                }
                if (taken.Count > 0) takenWho = WhoOf(TradeSession.trader);
            }
            catch (Exception e)
            {
                taken.Clear();
                CouncilLog.ErrorOnce("trade record", e);
            }
        }

        /// <summary>The trade went through: what was taken goes into the record.</summary>
        public static void Write(bool traded)
        {
            if (taken.Count == 0) return;
            try
            {
                var book = TradeBook.Instance;
                if (!traded || book == null || !TradeBook.On) return;
                int now = Find.TickManager.TicksGame;
                foreach (var p in taken)
                    book.Add(p.def, p.stuff, new TradeLine { tick = now, sold = p.sold, count = p.count, price = p.price, who = takenWho ?? "", quality = p.quality });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trade record", e);
            }
            finally
            {
                taken.Clear();
                takenWho = null;
            }
        }
    }
}
