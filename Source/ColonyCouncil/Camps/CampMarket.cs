using System;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // A camp's people at the colony's market (Visitors/VisitorTrade.cs, with RimMarket): by day while they live here,
    // one of them now and then walks over to the colony's stalls and buys what the camp needs (CampKindDef.buys), with
    // coin from their trader's purse if they have a trader, else the coin they brought for the road. One a day at most,
    // a few in a stay.
    public partial class Camp
    {
        /// <summary>Purchases made at the colony's market stalls this stay, not again before <see cref="marketNext"/> (-1: not yet thought of), and whether their coin for the road was counted out.</summary>
        public int marketBuys;
        public int marketNext = -1;
        public bool marketPurse;

        /// <summary>
        /// One of them going over to the colony's market now, or null: living here at peace, in the market's hours, one
        /// of them at a time (never their trader, who keeps their own stall), not before the day's turn comes.
        /// </summary>
        public Job MarketJob(Pawn p)
        {
            if (!VisitorTrade.On || phase != CampPhase.Living || Hostile || p == null || p == trader || map == null) return null;
            // A neighbour settled for good goes once a day for as long as it stays; a passing camp a few times.
            if (!holding && marketBuys >= VisitorTradeMath.CampBuys) return null;
            int now = Now;
            if (marketNext < 0)
            {
                marketNext = now + Rand.Range(GenDate.TicksPerHour * 2, GenDate.TicksPerHour * 8);
                return null;
            }
            if (now < marketNext || Carrying(p) || p.DevelopmentalStage.Juvenile() || !VisitorTrade.MayShop(map, faction)) return null;
            var here = Here;
            if (VisitorTrade.AnyShopping(here, p)) return null;
            // Someone else of theirs still has the coin from before: they go.
            bool coin = RimMarketStall.SilverIn(p) >= VisitorTradeMath.LeastSilver;
            if (!coin && Free.Any(x => x != p && x != trader && RimMarketStall.SilverIn(x) >= VisitorTradeMath.LeastSilver)) return null;
            marketNext = now + GenDate.TicksPerHour * 2;
            if (RimMarketStall.OpenStalls(map).Count == 0) return null;
            if (!coin && !MarketPurse(p))
            {
                marketNext = now + GenDate.TicksPerDay;
                return null;
            }
            return VisitorTrade.ShopJob(p, VisitorTradeMath.Wants(kind?.buys));
        }

        /// <summary>
        /// Coin for the market into the pack of whoever goes: out of their trader's purse if they have a trader with
        /// silver (not a trade with the colony), else what they brought for the road, once a stay. False with none.
        /// </summary>
        private bool MarketPurse(Pawn p)
        {
            var (min, max) = VisitorTradeMath.Purse(null, false);
            int want = Rand.RangeInclusive(min, max);
            int fromTrader = trader?.inventory != null && Here.Contains(trader) ? Math.Min(want, RimMarketStall.SilverIn(trader)) : 0;
            if (fromTrader >= VisitorTradeMath.LeastSilver)
            {
                trader.inventory.RemoveCount(ThingDefOf.Silver, fromTrader);
                // Silver leaving their trader's purse here is no trade with the colony (CampLife.WatchTrade).
                traderSilver = -1;
                VisitorTrade.GivePurse(p, fromTrader);
                return true;
            }
            if (marketPurse) return false;
            marketPurse = true;
            VisitorTrade.GivePurse(p, want);
            return true;
        }

        /// <summary>A purchase made at the colony's market: the next not before tomorrow.</summary>
        public void MarketBought()
        {
            marketBuys++;
            marketNext = Now + Rand.Range(GenDate.TicksPerDay * 4 / 5, GenDate.TicksPerDay * 6 / 5);
        }

        private void ExposeMarket()
        {
            Scribe_Values.Look(ref marketBuys, "marketBuys");
            Scribe_Values.Look(ref marketNext, "marketNext", -1);
            Scribe_Values.Look(ref marketPurse, "marketPurse");
        }
    }
}
