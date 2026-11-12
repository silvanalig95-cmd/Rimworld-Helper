using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// One visitor group's turns at the colony's market (VisitorTrade.GuestJob): how many purchases it made on which day,
    /// when one of them may go over next (-1 before their first look), and whether their coin for the road was counted out.
    /// Kept by the group's lord (its load id) while the group is on a map, in VisitorTracker.calls.
    /// </summary>
    public class MarketCall : IExposable
    {
        public int lord = -1;
        public int shopped, day = -1, next = -1;
        public bool purse;
        /// <summary>When the record was made: with too many kept, the oldest goes.</summary>
        public int made;

        /// <summary>A purchase made: counted for today, the next try a good while later.</summary>
        public void Bought(int now)
        {
            int today = now / GenDate.TicksPerDay;
            shopped = VisitorTradeMath.ShoppedToday(shopped, day, today) + 1;
            day = today;
            var (lo, hi) = VisitorTradeMath.NextTryHours(false);
            next = now + Rand.Range(GenDate.TicksPerHour * lo, GenDate.TicksPerHour * hi);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref lord, "lord", -1);
            Scribe_Values.Look(ref shopped, "shopped");
            Scribe_Values.Look(ref day, "day", -1);
            Scribe_Values.Look(ref next, "next", -1);
            Scribe_Values.Look(ref purse, "purse");
            Scribe_Values.Look(ref made, "made");
        }
    }

    /// <summary>
    /// Every other visitor at the colony shops at its market too (the player's request, 12 November 2026), with RimMarket:
    /// the game's visitor groups passing a day, a trade caravan's people, Rimfolk's own envoys, notables, guests, callers
    /// and those who come as the game's visit does (invited guests without Hospitality among them), and guests of a feast.
    /// One of a group with nothing better to do than wander about their spot may walk over to an open stall in the
    /// market's hours, and buy what fits who they are, as campers do (the same walk, look and sale: VisitorTrade.ShopJob,
    /// Buy): a trader's people what their trader deals in, an envoy or a notable something fine, a feast's guests something
    /// to take home, a people of Rimfolk's by its calling, anyone else on the road food that keeps. Their own silver
    /// first; a trader's people take coin from their trader's silver; anyone else their coin for the road, once a visit. A
    /// purchase or two a day, one of a group at a time, never more than a few at the stalls in all. Never on their way
    /// out, at war, with the map in danger, or while something holds them (an answer the colony owes them, a caller's
    /// work, a story of their own, the gods' affairs, a quest, Hospitality's stay). Their group's duty is left as it is:
    /// the walk is a job of theirs, which ends as soon as the group sets off, so a caravan leaves with its people and a
    /// visit's lord gathers them as always. Rimfolk's own visits and camps keep their own turn (VisitorTrade.VisitorJob,
    /// Camp.MarketJob); Hospitality's guests shop by RimMarket's own duty (Integrations/MarketGuests.cs).
    /// </summary>
    public static partial class VisitorTrade
    {
        /// <summary>The setting is on (with the deeper game hooks), and RimMarket's sale was found.</summary>
        public static bool GuestsOn => RimfolkPatches.On && (ColonyCouncilMod.Settings?.marketVisitorsBuy ?? true) && RimMarketStall.SaleReady;

        /// <summary>Groups' records kept at most: the oldest go first.</summary>
        private const int MaxCalls = 16;

        // ---------------- Who they are ----------------

        /// <summary>The kind of group a lord is, for the market: none for Rimfolk's own visits and camps, RimMarket's and Hospitality's lords, raiders and everything else.</summary>
        public static GuestGroup GroupOf(Lord lord)
        {
            var job = lord?.LordJob;
            if (job == null || lord.faction == null || lord.faction.IsPlayer) return GuestGroup.None;
            string ns = job.GetType().Namespace ?? "";
            if (ns.StartsWith("RimMarket") || ns.StartsWith("Hospitality")) return GuestGroup.None;
            if (job is LordJob_TradeWithColony) return GuestGroup.Caravan;
            if (job is LordJob_VisitColony) return GuestGroup.Visit;
            if (job is LordJob_EnvoyStay) return GuestGroup.Envoys;
            if (job is LordJob_FeastGuests) return GuestGroup.Feast;
            return GuestGroup.None;
        }

        /// <summary>The group is at its stay: a visit or a caravan at its spot (not walking in, not gathering to go, not carrying off its wounded), envoys at their stay, a feast's guests at the feast.</summary>
        private static bool AtStay(Lord lord, GuestGroup g)
        {
            var toil = lord?.CurLordToil;
            switch (g)
            {
                case GuestGroup.Visit:
                case GuestGroup.Caravan:
                    return toil is LordToil_DefendPoint;
                case GuestGroup.Envoys:
                    return toil is LordToil_EnvoyStay;
                case GuestGroup.Feast:
                    return toil is LordToil_FeastGuests;
                default:
                    return false;
            }
        }

        /// <summary>The trader of the group: a caravan's, a visit's small trader, a people's factor with goods. Null when it has none.</summary>
        private static Pawn TraderOf(Lord lord)
        {
            if (lord == null) return null;
            foreach (var x in lord.ownedPawns)
                if (x != null && !x.Dead && (x.trader?.traderKind != null || x.kindDef != null && x.kindDef.trader))
                    return x;
            return null;
        }

        private static GuestRole RoleOf(Pawn p, GuestGroup g)
        {
            if (g == GuestGroup.Caravan)
            {
                switch (p.GetTraderCaravanRole())
                {
                    case TraderCaravanRole.Trader: return GuestRole.Trader;
                    case TraderCaravanRole.Carrier: return GuestRole.Carrier;
                    case TraderCaravanRole.Chattel: return GuestRole.Chattel;
                }
            }
            return p.trader?.traderKind != null || p.kindDef != null && p.kindDef.trader ? GuestRole.Trader : GuestRole.Plain;
        }

        /// <summary>A people's leader, one with a title, or a notable the colony knows by name.</summary>
        private static bool IsNotable(Pawn p) =>
            p.Faction?.leader == p || p.royalty?.MostSeniorTitle != null || (NotableBook.Instance?.notables.Any(n => n?.pawn == p) ?? false);

        /// <summary>What they are, for what they look for and say: a trader's people, an envoy, a notable, a feast's guests, their people's calling, or a traveller.</summary>
        private static string WhoOf(Pawn p, GuestGroup g, Pawn trader)
        {
            if (trader != null) return VisitorTradeMath.Caravan;
            if (g == GuestGroup.Envoys) return VisitorTradeMath.Envoy;
            if (IsNotable(p)) return VisitorTradeMath.Notable;
            if (g == GuestGroup.Feast) return VisitorTradeMath.Guests;
            string calling = Peoples.CallingOf(p.Faction);
            return calling.NullOrEmpty() ? VisitorTradeMath.Travellers : calling;
        }

        /// <summary>
        /// Something holds them where they are: Hospitality's stay or a quest; an answer the colony still owes them (a
        /// question's letter about them waiting); a caller's work or a story of their own under way (Callers, a happening's
        /// visit); the gods' affairs; a talk, a trade or a scene with the colony's people; their hands full.
        /// </summary>
        private static bool Held(Pawn p)
        {
            if (Strangers.IsHospitalityGuest(p) || p.IsQuestLodger() || QuestUtility.IsReservedByQuestOrQuestBeingGenerated(p)) return true;
            if (p.carryTracker?.CarriedThing != null || Outsiders.TradingNow(p) || OutsiderTalk.Busy(p)) return true;
            var letters = Find.LetterStack?.LettersListForReading;
            if (letters != null)
                foreach (var l in letters)
                    if (l is ChoiceLetter_Dilemma d && !d.resolved && d.ctx != null && (d.ctx.stranger == p || d.ctx is HappeningContext h && h.group != null && h.group.Contains(p)))
                        return true;
            var tasks = Callers.Instance?.tasks;
            if (tasks != null && tasks.Any(t => t != null && (t.x == p || t.people != null && t.people.Contains(p)))) return true;
            var visits = CouncilGameComponent.Instance?.happenings?.visits;
            if (visits != null && visits.Any(v => v != null && !v.done && (v.x == p || v.group != null && v.group.Contains(p)))) return true;
            if (GodsOnMapBook.Instance?.stranger?.x == p) return true;
            var doings = GodWorldBook.Instance?.doings;
            return doings != null && doings.Any(d => d != null && !d.done && (d.pawn == p || d.who == p));
        }

        /// <summary>What decides whether this visitor may walk over now (VisitorTradeMath.WhyNot); <paramref name="anyHour"/> for the dev tools, the market's hours aside.</summary>
        private static GuestFacts FactsFor(Pawn p, Lord lord, GuestGroup g, bool anyHour = false)
        {
            var map = p.Map;
            var f = new GuestFacts
            {
                group = g,
                role = RoleOf(p, g),
                person = p.RaceProps.Humanlike,
                grown = !p.DevelopmentalStage.Juvenile(),
                up = !p.Downed && !p.InMentalState && !p.Drafted && (p.health?.capacities?.CapableOf(PawnCapacityDefOf.Moving) ?? false),
                awake = p.Awake(),
                atStay = AtStay(lord, g),
                leaving = Outsiders.Leaving(p),
                atPeace = !lord.faction.HostileTo(Faction.OfPlayer) && !p.HostileTo(Faction.OfPlayer),
                quiet = map != null && !MapFacts.ThreatOn(map) && map.dangerWatcher.DangerRating == StoryDanger.None,
                hours = anyHour || map != null && VisitorTradeMath.ShopHour(GenLocalDate.HourOfDay(map), RimMarketStall.ShopFrom, RimMarketStall.ShopTo)
            };
            // The rest only matters once the rest allows it.
            if (VisitorTradeMath.WhyNot(f) != null) return f;
            f.pressed = PlayOuts.NeedsPress(p) || VisitorWork.WantsSleep(p) || HealthAIUtility.ShouldSeekMedicalRest(p);
            f.held = Held(p);
            return f;
        }

        /// <summary>Why this visitor wouldn't walk over to the market now, or null (the dev tools' log).</summary>
        public static string GuestWhyNot(Pawn p)
        {
            var lord = p?.GetLord();
            var g = GroupOf(lord);
            return g == GuestGroup.None ? "group" : VisitorTradeMath.WhyNot(FactsFor(p, lord, g));
        }

        /// <summary>
        /// Dev mode: this visitor walks over now if they may, their group's turn and the market's hours aside (everything
        /// else as always, their coin too). Why not in a few words, or null when they set off.
        /// </summary>
        public static string GuestNow(Pawn p)
        {
            if (!GuestsOn) return "the setting or the deeper game hooks are off, or RimMarket's sale wasn't found";
            var lord = p?.GetLord();
            var g = GroupOf(lord);
            if (g == GuestGroup.None) return "not of a group that shops this way (" + (lord?.LordJob?.GetType().Name ?? "no group") + ")";
            string why = VisitorTradeMath.WhyNot(FactsFor(p, lord, g, true));
            if (why != null) return why;
            if (RimMarketStall.OpenStalls(p.Map).Count == 0) return "no colony stall is open for sale";
            var call = CallOf(lord, true);
            var trader = TraderOf(lord);
            if (OtherCarries(p, lord, trader)) return "someone else of theirs carries their coin";
            string who = WhoOf(p, g, trader);
            if (!GuestCoin(p, lord, g, call, who, trader)) return "no coin (their trader's is short or in a trade, or their coin for the road is spent)";
            var job = ShopJob(p, trader != null ? WantsOfTrader(trader) : VisitorTradeMath.Wants(who), VisitorTradeMath.IsFine(who));
            if (job == null) return "nothing on an open counter for them (" + who + ") that they can pay for, or no way there";
            call.next = Now + VisitorTradeMath.RetryHours * GenDate.TicksPerHour;
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            return null;
        }

        /// <summary>Dev mode: each group of visitors on the map that shops this way, its turns, and why each of them would or wouldn't go now.</summary>
        public static List<string> GuestReport(Map map)
        {
            var lines = new List<string>();
            if (map == null) return lines;
            foreach (var lord in map.lordManager.lords)
            {
                var g = GroupOf(lord);
                if (g == GuestGroup.None) continue;
                var call = CallOf(lord, false);
                var trader = TraderOf(lord);
                lines.Add($"{lord.faction.Name}: {g} ({lord.LordJob.GetType().Name}, {lord.CurLordToil?.GetType().Name}), at its stay {AtStay(lord, g)}"
                          + (call == null ? ", not looked yet" : $", bought {call.shopped} on day {call.day}, next {(call.next < 0 ? "-" : (call.next - Now) / (float)GenDate.TicksPerHour + "h")}, coin for the road {(call.purse ? "spent" : "kept")}")
                          + (trader != null ? ", trader " + trader.LabelShort + " (" + trader.trader?.traderKind?.defName + "): " + string.Join(", ", WantsOfTrader(trader)) : ""));
                foreach (var x in lord.ownedPawns)
                    if (x != null && x.RaceProps.Humanlike)
                        lines.Add($"  {x.LabelShort}: {WhoOf(x, g, trader)}, {RimMarketStall.SilverIn(x)} silver, {GuestWhyNot(x) ?? "may go"}");
            }
            return lines;
        }

        /// <summary>A visitor whose group has set off (or who is leaving themselves) while at the market: their walk there ends and they go with their group.</summary>
        public static bool GuestGoing(Pawn p)
        {
            var lord = p?.GetLord();
            var g = GroupOf(lord);
            return g != GuestGroup.None && (!AtStay(lord, g) || Outsiders.Leaving(p));
        }

        // ---------------- Their turn ----------------

        /// <summary>
        /// One of a visitor group, about to wander near their spot for want of anything else to do, walks over to the
        /// market instead when it's their group's turn (VisitorTradeMath.Turn) and they may (VisitorTradeMath.WhyNot), with
        /// coin for it. Null otherwise.
        /// </summary>
        public static Job GuestJob(Pawn p)
        {
            if (!GuestsOn || p?.Map == null || !p.Spawned) return null;
            var lord = p.GetLord();
            var g = GroupOf(lord);
            if (g == GuestGroup.None || !MayShop(p.Map, lord.faction) || !AtStay(lord, g)) return null;
            var call = CallOf(lord, true);
            if (call == null) return null;
            int now = Now;
            int today = now / GenDate.TicksPerDay;
            int shopped = VisitorTradeMath.ShoppedToday(call.shopped, call.day, today);
            bool oneThere = AnyShopping(lord.ownedPawns, p);
            var turn = VisitorTradeMath.Turn(shopped, call.next, now, oneThere, 0);
            if (turn == MarketTurn.Go) turn = VisitorTradeMath.Turn(shopped, call.next, now, oneThere, AtStalls(p.Map));
            if (turn == MarketTurn.FirstSeen)
            {
                call.next = now + (int)(Rand.Range(VisitorTradeMath.FirstLookMin, VisitorTradeMath.FirstLookMax) * GenDate.TicksPerHour);
                return null;
            }
            if (turn != MarketTurn.Go || VisitorTradeMath.WhyNot(FactsFor(p, lord, g)) != null) return null;
            var trader = TraderOf(lord);
            // Someone else of theirs carries the coin: the turn waits for them.
            if (OtherCarries(p, lord, trader)) return null;
            call.next = now + VisitorTradeMath.RetryHours * GenDate.TicksPerHour;
            if (RimMarketStall.OpenStalls(p.Map).Count == 0) return null;
            string who = WhoOf(p, g, trader);
            if (!GuestCoin(p, lord, g, call, who, trader)) return null;
            return ShopJob(p, trader != null ? WantsOfTrader(trader) : VisitorTradeMath.Wants(who), VisitorTradeMath.IsFine(who));
        }

        /// <summary>The visitor's group's wants, as the purchase is made (VisitorTrade.Buy): null for anyone this doesn't cover.</summary>
        private static TradeGoods[] GuestWants(Pawn p, out MarketCall call, out string who, out bool fine, out int people, out int animals)
        {
            call = null;
            who = null;
            fine = false;
            people = 1;
            animals = 0;
            var lord = p?.GetLord();
            var g = GroupOf(lord);
            if (g == GuestGroup.None) return null;
            call = CallOf(lord, false);
            var trader = TraderOf(lord);
            who = WhoOf(p, g, trader);
            fine = VisitorTradeMath.IsFine(who);
            people = Math.Max(1, lord.ownedPawns.Count(x => x != null && x.RaceProps.Humanlike && (g != GuestGroup.Caravan || x.GetTraderCaravanRole() != TraderCaravanRole.Chattel)));
            animals = lord.ownedPawns.Count(x => x != null && x.RaceProps.Animal && !x.Dead);
            return trader != null ? WantsOfTrader(trader) : VisitorTradeMath.Wants(who);
        }

        /// <summary>Everyone on the map at a stall or on the way to one (campers, visits, market days, every other visitor).</summary>
        private static int AtStalls(Map map)
        {
            int n = 0;
            foreach (var x in map.mapPawns.AllPawnsSpawned)
                if (x.CurJobDef == TradeDefOf.Rimfolk_MarketBuy)
                    n++;
            return n;
        }

        // ---------------- Their coin ----------------

        /// <summary>The buyer has no coin of their own and someone else grown of theirs (not their trader, whose silver is the trade's) has.</summary>
        private static bool OtherCarries(Pawn p, Lord lord, Pawn trader)
        {
            if (RimMarketStall.SilverIn(p) >= VisitorTradeMath.LeastSilver) return false;
            foreach (var x in lord.ownedPawns)
                if (x != null && x != p && x != trader && !x.Dead && x.RaceProps.Humanlike && !x.DevelopmentalStage.Juvenile() && RimMarketStall.SilverIn(x) >= VisitorTradeMath.LeastSilver)
                    return true;
            return false;
        }

        /// <summary>
        /// Coin to buy with in the buyer's pack: their own; a trader's people take it from their trader's silver (never
        /// while the colony is trading with them); anyone else counts out their coin for the road, once a visit. False when
        /// there's none.
        /// </summary>
        private static bool GuestCoin(Pawn p, Lord lord, GuestGroup g, MarketCall call, string who, Pawn trader)
        {
            if (RimMarketStall.SilverIn(p) >= VisitorTradeMath.LeastSilver) return true;
            if (trader != null) return FromTrader(p, lord, g, trader, who);
            if (call.purse) return false;
            call.purse = true;
            var (min, max) = VisitorTradeMath.Purse(who, false);
            if (max < VisitorTradeMath.LeastSilver) return false;
            GivePurse(p, Rand.RangeInclusive(Math.Max(min, VisitorTradeMath.LeastSilver), max));
            return true;
        }

        /// <summary>Coin from the trader's silver (theirs and their pack beasts'): what their people go to market with is the trade's own.</summary>
        private static bool FromTrader(Pawn p, Lord lord, GuestGroup g, Pawn trader, string who)
        {
            if (Outsiders.TradingNow(trader) || TradeSession.Active && TradeSession.trader == trader) return false;
            var holders = lord.ownedPawns.Where(x => x != null && !x.Dead && x.inventory != null
                                                     && (x == trader || g == GuestGroup.Caravan && x.GetTraderCaravanRole() == TraderCaravanRole.Carrier)).ToList();
            int have = holders.Sum(x => RimMarketStall.SilverIn(x));
            var (min, max) = VisitorTradeMath.Purse(who, false);
            int take = Math.Min(have, Rand.RangeInclusive(min, max));
            if (take < VisitorTradeMath.LeastSilver) return false;
            int left = take;
            foreach (var h in holders)
            {
                // A pack beast is never emptied: one carrying nothing would count as one of the caravan's beasts for sale.
                int spare = RimMarketStall.SilverIn(h) - (h != trader && h.inventory.innerContainer.All(t => t.def == ThingDefOf.Silver) ? 1 : 0);
                int n = Math.Min(left, spare);
                if (n <= 0) continue;
                h.inventory.RemoveCount(ThingDefOf.Silver, n);
                left -= n;
                if (left <= 0) break;
            }
            GivePurse(p, take - left);
            return take - left >= VisitorTradeMath.LeastSilver;
        }

        // ---------------- What a trader deals in ----------------

        private static readonly Dictionary<TraderKindDef, TradeGoods[]> traderWants = new Dictionary<TraderKindDef, TradeGoods[]>();
        private static Dictionary<TradeGoods, int> kindsAll;

        /// <summary>
        /// What a trader's people look for: the kinds of goods their trader deals in (the game's own test of what the trader
        /// takes, over every article of the game and the mods), the biggest shares first (VisitorTradeMath.TraderWants).
        /// Worked out once a trader kind for the session.
        /// </summary>
        private static TradeGoods[] WantsOfTrader(Pawn trader)
        {
            var k = trader?.trader?.traderKind;
            if (k == null) return VisitorTradeMath.Wants(VisitorTradeMath.Caravan);
            if (traderWants.TryGetValue(k, out var w)) return w;
            try
            {
                var all = kindsAll == null ? new Dictionary<TradeGoods, int>() : null;
                var handled = new Dictionary<TradeGoods, int>();
                var kinds = (TradeGoods[])Enum.GetValues(typeof(TradeGoods));
                foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
                {
                    // Articles, and art that stands (a sculpture is sold in its crate).
                    if (def == null || def.category != ThingCategory.Item && !(def.IsArt && def.Minifiable) || def.tradeability == Tradeability.None || def == ThingDefOf.Silver) continue;
                    var g = FactsOf(def);
                    if (g == TradeGoods.None) continue;
                    bool takes = k.WillTrade(def);
                    foreach (var one in kinds)
                    {
                        if (one == TradeGoods.None || (g & one) == 0) continue;
                        if (all != null) all[one] = all.TryGetValue(one, out int a) ? a + 1 : 1;
                        if (takes) handled[one] = handled.TryGetValue(one, out int h) ? h + 1 : 1;
                    }
                }
                if (all != null) kindsAll = all;
                w = VisitorTradeMath.TraderWants(handled, kindsAll);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("market: what a trader deals in", e);
                w = VisitorTradeMath.Wants(VisitorTradeMath.Caravan);
            }
            traderWants[k] = w;
            return w;
        }

        // ---------------- The groups' records ----------------

        /// <summary>The group's record, made when asked to (the records of groups gone from every map dropped then, and the oldest past the cap).</summary>
        private static MarketCall CallOf(Lord lord, bool make)
        {
            var tracker = VisitorTracker.Instance;
            if (tracker == null || lord == null) return null;
            foreach (var c in tracker.calls)
                if (c.lord == lord.loadID)
                    return c;
            if (!make) return null;
            var live = new HashSet<int>();
            foreach (var m in Find.Maps)
                foreach (var l in m.lordManager.lords)
                    live.Add(l.loadID);
            tracker.calls.RemoveAll(c => c == null || !live.Contains(c.lord));
            while (tracker.calls.Count >= MaxCalls) tracker.calls.Remove(tracker.calls.MinBy(c => c.made));
            var call = new MarketCall { lord = lord.loadID, made = Now };
            tracker.calls.Add(call);
            return call;
        }
    }

    /// <summary>
    /// A visitor at the colony about to wander near their spot for want of anything to do may walk over to the market
    /// instead (VisitorTrade.GuestJob, with RimMarket). Only ever in place of that idle wander: fighting, eating, sleeping
    /// and whatever their duty has for them come first, and their duty itself is left as it is.
    /// </summary>
    [HarmonyPatch(typeof(ThinkNode_Duty), nameof(ThinkNode_Duty.TryIssueJobPackage))]
    public static class Patch_VisitorsShop
    {
        public static bool Prepare() => ModsConfig.IsActive(RimMarketStall.PackageId);

        public static string StandsDownWhy() => ModsConfig.IsActive(RimMarketStall.PackageId) ? "" : "RimMarket isn't loaded";

        public static void Postfix(Pawn pawn, ref ThinkResult __result)
        {
            var job = __result.Job;
            if (job == null || job.def != JobDefOf.GotoWander && job.def != JobDefOf.Wait_Wander) return;
            if (pawn?.Faction == null || pawn.Faction.IsPlayer || !VisitorTrade.GuestsOn) return;
            try
            {
                var shop = VisitorTrade.GuestJob(pawn);
                if (shop == null) return;
                shop.lord = job.lord;
                shop.source = job.source;
                shop.dutyTag = job.dutyTag;
                __result = new ThinkResult(shop, __result.SourceNode, JobTag.Misc, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a visitor going to the market", e);
            }
        }
    }
}
