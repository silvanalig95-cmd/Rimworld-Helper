using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    [DefOf]
    public static class TradeDefOf
    {
        public static JobDef Rimfolk_MarketBuy;

        static TradeDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(TradeDefOf));
        }
    }

    /// <summary>
    /// Visitors camped out on the colony's land (VisitorCamp.Camps) and people from a camp near the colony walk over to
    /// the colony's own market stalls (RimMarket, Integrations/RimMarketSale.cs) and buy something lying on the counter
    /// that fits who they are: drovers feed for their herd, a smith metal, a family a meal, anyone on the road food that
    /// keeps (VisitorTradeMath.Wants, a camp kind's <c>buys</c>). They stand at the counter a while, then the article goes
    /// off the counter into their pack and their silver is set down at the stall, as with RimMarket's own shoppers. A few
    /// purchases a visit, in the market's hours, only at peace and with the map quiet. Without RimMarket, without a colony
    /// stall with goods on its counter, or with the setting off, nothing of it happens.
    /// </summary>
    public static class VisitorTrade
    {
        private const string Pool = "Rimfolk_Pool_MarketBuy";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The setting is on, and RimMarket's sale was found.</summary>
        public static bool On => (ColonyCouncilMod.Settings?.marketCampersBuy ?? true) && RimMarketStall.SaleReady;

        /// <summary>A people at peace with the colony may walk over to buy now: in the market's hours, the map quiet.</summary>
        public static bool MayShop(Map map, Faction f)
        {
            if (map == null || f == null || f.IsPlayer || f.HostileTo(Faction.OfPlayer)) return false;
            if (MapFacts.ThreatOn(map) || map.dangerWatcher.DangerRating != StoryDanger.None) return false;
            return VisitorTradeMath.ShopHour(GenLocalDate.HourOfDay(map), RimMarketStall.ShopFrom, RimMarketStall.ShopTo);
        }

        /// <summary>The stall can still sell to them: open as RimMarket has it, and nothing threatening on the map.</summary>
        public static bool StillOpen(Pawn p, Thing stall) => On && stall != null && stall.Spawned && !MapFacts.ThreatOn(p.Map) && RimMarketStall.OpenForSale(stall);

        /// <summary>Someone of the group other than <paramref name="but"/> is on the way to a stall or at one.</summary>
        public static bool AnyShopping(IEnumerable<Pawn> group, Pawn but) => group.Any(x => x != but && x.CurJobDef == TradeDefOf.Rimfolk_MarketBuy);

        // ---------------- What a thing on the counter is ----------------

        /// <summary>The kinds of goods a thing on the counter is (a carving in its crate is art; hay is feed; pemmican a meal that keeps).</summary>
        public static TradeGoods Facts(Thing t)
        {
            var def = RimMarketStall.StockDef(t) ?? t?.def;
            if (def == null) return TradeGoods.None;
            var g = TradeGoods.None;
            if (def.IsArt) g |= TradeGoods.Art;
            if (t is MinifiedThing) return g;
            var ing = def.ingestible;
            if (ing != null && def.IsNutritionGivingIngestible && !def.IsDrug)
            {
                if ((ing.foodType & (FoodTypeFlags.Plant | FoodTypeFlags.Kibble)) != 0) g |= TradeGoods.Feed;
                if (ing.HumanEdible)
                {
                    if (ing.IsMeal) g |= TradeGoods.Meal;
                    if (ing.preferability >= FoodPreferability.MealAwful && Keeps(def)) g |= TradeGoods.RoadFood;
                    if ((ing.preferability == FoodPreferability.RawBad || ing.preferability == FoodPreferability.RawTasty) && (ing.foodType & FoodTypeFlags.Kibble) == 0)
                        g |= TradeGoods.RawFood;
                }
            }
            if (def.IsDrug && ing?.drugCategory == DrugCategory.Social) g |= TradeGoods.Drink;
            if (def.IsMedicine) g |= TradeGoods.Medicine;
            if (def.IsApparel) g |= TradeGoods.Clothes;
            if (def.IsWithinCategory(ThingCategoryDefOf.Textiles)) g |= TradeGoods.Textiles;
            if (def.IsStuff && def.stuffProps?.categories != null && def.stuffProps.categories.Contains(StuffCategoryDefOf.Metallic) || def == ThingDefOf.ComponentIndustrial)
                g |= TradeGoods.Metal;
            return g;
        }

        /// <summary>Food that keeps on the road: it doesn't rot, or not for weeks.</summary>
        private static bool Keeps(ThingDef def)
        {
            var rot = def.GetCompProperties<CompProperties_Rottable>();
            return rot == null || rot.daysToRotStart >= 20f;
        }

        // ---------------- Whose they are ----------------

        private static VisitorTrip TripOf(Pawn p) => p?.GetLord()?.LordJob is LordJob_RimfolkVisit j ? VisitorTracker.Instance?.Get(j.tripId) : null;

        /// <summary>What the buyer's people look for, how many mouths they are and how many beasts they drive.</summary>
        private static TradeGoods[] WantsOf(Pawn p, out VisitorTrip trip, out Camp camp, out int people, out int animals)
        {
            trip = TripOf(p);
            camp = trip == null ? CampTracker.CampOf(p) : null;
            people = 1;
            animals = 0;
            if (trip != null)
            {
                people = trip.Standing.Count;
                animals = trip.animals.Count(a => a != null && !a.Dead);
                return VisitorTradeMath.Wants(trip.what);
            }
            if (camp != null)
            {
                people = camp.Here.Count(x => x.RaceProps.Humanlike);
                animals = camp.herd.Count(a => a != null && !a.Dead);
                return VisitorTradeMath.Wants(camp.kind?.buys);
            }
            return null;
        }

        // ---------------- Going to buy ----------------

        /// <summary>The article on this counter that fits them best and they can pay one of, or null. Ties go to chance.</summary>
        private static Thing BestOn(Thing stall, IList<TradeGoods> wants, int purse, out int fit)
        {
            fit = 0;
            Thing best = null;
            int ties = 0;
            foreach (var t in RimMarketStall.Stock(stall))
            {
                if (!RimMarketStall.ForSale(stall, t)) continue;
                int f = VisitorTradeMath.Fit(Facts(t), wants);
                if (f <= 0 || f < fit || RimMarketStall.UnitPrice(stall, t) > purse) continue;
                if (f > fit)
                {
                    fit = f;
                    best = t;
                    ties = 1;
                }
                else if (Rand.Chance(1f / ++ties)) best = t;
            }
            return best;
        }

        /// <summary>
        /// The walk to the colony's stall with the article that fits them best (the nearest stall of those that have one),
        /// to RimMarket's own place in front of its counter. Null with nothing there for them, or no way to it.
        /// </summary>
        public static Job ShopJob(Pawn p, IList<TradeGoods> wants)
        {
            int purse = RimMarketStall.SilverIn(p);
            if (p?.Map == null || wants == null || purse < VisitorTradeMath.LeastSilver) return null;
            Thing stall = null;
            int bestFit = 0;
            float bestDist = float.MaxValue;
            foreach (var s in RimMarketStall.OpenStalls(p.Map))
            {
                if (BestOn(s, wants, purse, out int fit) == null) continue;
                float d = s.Position.DistanceToSquared(p.Position);
                if (fit < bestFit || fit == bestFit && d >= bestDist || !p.CanReach(s, PathEndMode.Touch, Danger.Some)) continue;
                stall = s;
                bestFit = fit;
                bestDist = d;
            }
            if (stall == null) return null;
            var cell = RimMarketStall.BrowseCell(stall, p);
            if (!cell.IsValid) return null;
            var job = JobMaker.MakeJob(TradeDefOf.Rimfolk_MarketBuy, stall, cell);
            job.locomotionUrgency = LocomotionUrgency.Walk;
            return job;
        }

        /// <summary>
        /// One of a visit's people camped out on the land, now and then: an hour or so after they settle, and a few hours
        /// after each try, whoever carries the group's coin walks over to the market (one of them at a time).
        /// </summary>
        public static Job VisitorJob(Pawn p)
        {
            if (!On || p?.Map == null || !p.RaceProps.Humanlike || p.Downed || p.InMentalState || p.DevelopmentalStage.Juvenile()) return null;
            var t = TripOf(p);
            if (t == null || !VisitorCamp.Camps(t) || t.shopped >= VisitorTradeMath.VisitBuys) return null;
            int now = Now;
            if (t.shopNext < 0)
            {
                t.shopNext = now + Rand.Range(GenDate.TicksPerHour / 2, GenDate.TicksPerHour * 3 / 2);
                return null;
            }
            if (now < t.shopNext || VisitorWork.WantsSleep(p) || !MayShop(p.Map, t.faction)) return null;
            var group = t.Standing;
            if (AnyShopping(group, p)) return null;
            // Someone else carries the group's coin: they go (or, all of it spent, nobody does).
            bool coin = RimMarketStall.SilverIn(p) >= VisitorTradeMath.LeastSilver;
            if (!coin && (t.purse || group.Any(x => x != p && !x.DevelopmentalStage.Juvenile() && RimMarketStall.SilverIn(x) >= VisitorTradeMath.LeastSilver))) return null;
            t.shopNext = now + GenDate.TicksPerHour * 2;
            if (RimMarketStall.OpenStalls(p.Map).Count == 0) return null;
            if (!coin)
            {
                t.purse = true;
                var (min, max) = VisitorTradeMath.Purse(t.what, t.robbed);
                if (max <= 0)
                {
                    // Robbed on the road: nothing to buy with.
                    t.shopped = VisitorTradeMath.VisitBuys;
                    return null;
                }
                GivePurse(p, Rand.RangeInclusive(min, max));
            }
            return ShopJob(p, VisitorTradeMath.Wants(t.what));
        }

        /// <summary>The coin they brought for the road, in the pack of whoever goes to buy.</summary>
        public static void GivePurse(Pawn p, int silver)
        {
            if (silver <= 0 || p?.inventory == null) return;
            var coin = ThingMaker.MakeThing(ThingDefOf.Silver);
            coin.stackCount = silver;
            p.inventory.TryAddItemNotForSale(coin);
        }

        // ---------------- At the counter ----------------

        /// <summary>
        /// At the counter, after a look over the goods: the article that fits them best is bought as a RimMarket shopper
        /// buys (as many as they need and can pay for, if the price doesn't put them off), and the colony hears of it.
        /// Nothing for them there, or too dear: they say so and go.
        /// </summary>
        public static void Buy(Pawn p, Thing stall)
        {
            if (!On || p?.Map == null || stall == null || !stall.Spawned) return;
            try
            {
                var wants = WantsOf(p, out var trip, out var camp, out int people, out int animals);
                if (wants == null) return;
                int purse = RimMarketStall.SilverIn(p);
                var article = BestOn(stall, wants, purse, out _);
                if (article == null)
                {
                    Say(p, "none", TradeGoods.None, trip, 0.5f);
                    return;
                }
                var kind = VisitorTradeMath.Kind(Facts(article), wants);
                int count = VisitorTradeMath.HowMany(VisitorTradeMath.Need(kind, people, animals), article.stackCount, RimMarketStall.UnitPrice(stall, article), purse);
                if (count <= 0 || !Rand.Chance(RimMarketStall.BuyChance(stall, RimMarketStall.StockDef(article))))
                {
                    Say(p, "dear", kind, trip, 0.7f);
                    return;
                }
                if (!RimMarketStall.SellTo(p, stall, article, count, out var sale)) return;
                RimMarketStall.BoughtTouches(p, stall, sale);
                Say(p, "buy", kind, trip, 0.6f);
                Notify.Message("CC_VisitTrade_Bought".Translate(p.Named("PAWN"), (p.Faction?.Name ?? "?").Named("FACTION"), sale.label.Named("THING"), sale.paid.Named("COUNT")).Resolve(),
                    new LookTargets(p), MessageTypeDefOf.NeutralEvent, Importance.Notable);
                if (trip != null)
                {
                    trip.shopped++;
                    trip.shopNext = Now + Rand.Range(GenDate.TicksPerHour * 4, GenDate.TicksPerHour * 8);
                }
                camp?.MarketBought();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("market buy", e);
            }
        }

        /// <summary>A line from the buyer at the counter: "buy" (with what kind it was), "dear" (put off by the price), "none" (nothing there for them).</summary>
        private static void Say(Pawn p, string moment, TradeGoods kind, VisitorTrip trip, float chance)
        {
            if (p == null || !p.Spawned || !Rand.Chance(chance)) return;
            string line = Flavor.FromPool(Pool, p, null, null, moment, kind == TradeGoods.None ? null : kind.ToString().ToLowerInvariant(), trip?.what);
            if (!line.NullOrEmpty()) Speech.Say(p, line);
        }
    }

    /// <summary>A visit's people camped on the land go over to the colony's market now and then (VisitorTrade.VisitorJob).</summary>
    public class JobGiver_VisitShop : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            try
            {
                return VisitorTrade.VisitorJob(pawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitor shopping", e);
                return null;
            }
        }
    }

    /// <summary>
    /// A visitor or a camper buying at the colony's market stall: the walk to the place in front of its counter, a look
    /// over the goods facing the stall, then the purchase (VisitorTrade.Buy). Over if the stall closes or trouble comes.
    /// </summary>
    public class JobDriver_MarketBuy : JobDriver
    {
        private const int LookTicks = 450;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => pawn.IsHashIntervalTick(60) && !VisitorTrade.StillOpen(pawn, job.targetA.Thing));
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            var look = ToilMaker.MakeToil("MarketBuyLook");
            look.defaultCompleteMode = ToilCompleteMode.Delay;
            look.defaultDuration = LookTicks;
            look.handlingFacing = true;
            look.socialMode = RandomSocialMode.Normal;
            look.initAction = () => pawn.pather?.StopDead();
            look.tickIntervalAction = delta =>
            {
                var stall = job.targetA.Thing;
                if (stall != null) pawn.rotationTracker.FaceCell(stall.Position);
            };
            yield return look;
            yield return Toils_General.Do(() => VisitorTrade.Buy(pawn, job.targetA.Thing));
        }
    }
}
