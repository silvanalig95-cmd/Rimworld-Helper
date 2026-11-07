using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Rimfolk's sorts in the game's two "Sort by" menus of the trade screen (Defs/Misc/Rimfolk_TradeSorts.xml): each a
    /// TransferableSorterDef with one of these comparers, so the game's own menus offer them and nothing is patched to
    /// sort. The highest first: what the trader pays, what it asks (each for this negotiator, as the price columns
    /// show it), the price a kilo, the best deal against the usual price, Rimfolk's suggestions first, and with Vanilla
    /// Trading Expanded the price change on its market (the biggest moves first). A row without
    /// that side (nothing of it to sell, or the trader has none) goes last. The menus are shared with the caravan and
    /// transport pod windows, which have no prices: there these sort as the game's market value does, and with the
    /// deeper game hooks they are taken out of those menus (Trade/TradeScreen.cs).
    /// </summary>
    public abstract class TransferableComparer_Rimfolk : TransferableComparer
    {
        /// <summary>The row's key, the highest first; float.MinValue for a row without it.</summary>
        protected abstract float Key(Tradeable t, TradeNote n);

        public override int Compare(Transferable lhs, Transferable rhs)
        {
            if (!(lhs is Tradeable a) || !(rhs is Tradeable b) || !TradeScreen.Trading || !a.HasAnyThing || !b.HasAnyThing)
                return lhs?.AnyThing == null || rhs?.AnyThing == null ? 0 : lhs.AnyThing.MarketValue.CompareTo(rhs.AnyThing.MarketValue);
            try
            {
                return KeyOf(b).CompareTo(KeyOf(a));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("trade sort", e);
                return 0;
            }
        }

        private float KeyOf(Tradeable t) => Key(t, TradeAdvice.For(t));

        protected static bool Silver => TradeSession.TradeCurrency == TradeCurrency.Silver;
    }

    /// <summary>What the trader pays for it, the highest first.</summary>
    public class TransferableComparer_TraderPays : TransferableComparer_Rimfolk
    {
        protected override float Key(Tradeable t, TradeNote n) =>
            t.CountHeldBy(Transactor.Colony) > 0 && t.TraderWillTrade ? t.GetPriceFor(TradeAction.PlayerSells) : float.MinValue;
    }

    /// <summary>What the trader asks for it, the highest first.</summary>
    public class TransferableComparer_TraderAsks : TransferableComparer_Rimfolk
    {
        protected override float Key(Tradeable t, TradeNote n) =>
            t.CountHeldBy(Transactor.Trader) > 0 && t.TraderWillTrade ? t.GetPriceFor(TradeAction.PlayerBuys) : float.MinValue;
    }

    /// <summary>The trade price a kilo, the highest first: what the trader pays where the colony has it, else what it asks.</summary>
    public class TransferableComparer_PricePerKg : TransferableComparer_Rimfolk
    {
        protected override float Key(Tradeable t, TradeNote n)
        {
            if (!t.TraderWillTrade) return float.MinValue;
            float mass = t.AnyThing.GetStatValue(StatDefOf.Mass);
            if (mass <= 0.0001f) return float.MinValue;
            if (t.CountHeldBy(Transactor.Colony) > 0) return t.GetPriceFor(TradeAction.PlayerSells) / mass;
            if (t.CountHeldBy(Transactor.Trader) > 0) return t.GetPriceFor(TradeAction.PlayerBuys) / mass;
            return float.MinValue;
        }
    }

    /// <summary>The best deal against the usual price, the best first: a sale above it, a purchase under it (Vanilla Trading Expanded's market counted).</summary>
    public class TransferableComparer_BestDeal : TransferableComparer_Rimfolk
    {
        protected override float Key(Tradeable t, TradeNote n) => n != null && Silver && (n.canSell || n.canBuy) ? n.Deal : float.MinValue;
    }

    /// <summary>Rimfolk's suggestions first: what to buy, then what to sell, then what is kept, each by how pressing.</summary>
    public class TransferableComparer_Suggested : TransferableComparer_Rimfolk
    {
        protected override float Key(Tradeable t, TradeNote n) => n == null ? 0f : TradeAdviceMath.SortScore(n.mark, n.priority);
    }

    /// <summary>
    /// The price change on Vanilla Trading Expanded's market, the biggest moves first (up or down, over the last 7 or 30
    /// days, the row's trend line saying which way); a good it keeps no history of goes last. Only offered with VTE.
    /// </summary>
    public class TransferableComparer_PriceChange : TransferableComparer_Rimfolk
    {
        protected override float Key(Tradeable t, TradeNote n)
        {
            var v = TradeMarket.Known(t.ThingDef);
            float move = v != null ? TradeMarketMath.MoveScore(v.stats) : -1f;
            return move < 0f ? float.MinValue : move;
        }
    }
}
