using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Ducat and what the colony owes (C8 of docs/content-update-proposals.md). Ducat's lore says he watches debts, and the
    /// world ledger keeps what the colony owes: a scenario's silver or an enclave's dues (Tie.Kind.Debt from a colony), and the
    /// good turns owed to a people (debts of honour, FavoursOwed). When one of those moves (WorldLedger.Set):
    /// <list type="bullet">
    /// <item>Paid when due (kept): the next morning one gold coin lies on the doorstep of the one who carried the payment
    /// (the debt's own colonist where it names one at home, else the quartermaster, else the colony's best talker), found by
    /// whoever walks by first (OwnHappenings.Found), and the next trader at the gate stays half a day longer than they meant
    /// to (their lord's own timer, GodsAmongMath.LongerTicks).</item>
    /// <item>Let lapse (broken: not met in time, or refused): the next trader at the gate drives a hard bargain for the day
    /// (their prices a little worse, GodsAmongMath.HardBargain), said in a plain line in the trade window and a plain
    /// message.</item>
    /// </list>
    /// Ducat is never named in any of it: the coin is a coin someone found, the trader a trader; the book keeps the coin as
    /// whoever found it took it, which may be for nobody. Not every debt is followed so (GodsAmongMath.PaidChance). No harm.
    /// </summary>
    public static class DucatDebts
    {
        private static int Now => GodsAmongBook.Now;

        private static bool Ours(Ref r) => r.kind == RefKind.Colony || r.kind == RefKind.People && Refs.Faction(r)?.IsPlayer == true;

        /// <summary>A tie of the ledger moved (WorldLedger.Set): a debt the colony owed, paid or let lapse.</summary>
        internal static void Moved(Tie t)
        {
            var b = GodsAmongBook.Book;
            if (b == null || t == null || OwnDucat.God == null) return;
            bool colonyOwes = t.kind == Tie.Kind.Debt ? Ours(t.from) : t.kind == Tie.Kind.Favour && t.origin == FavoursOwed.Origin && FavoursOwed.ColonyOwes(t);
            var turn = GodsAmongMath.Turn(colonyOwes, t.state == Tie.State.Kept, t.state == Tie.State.Broken);
            if (turn == GodsAmongMath.DebtTurn.None) return;
            var home = t.from.kind == RefKind.Colony ? Diplomacy.HomeOf(t.from) : FavoursOwed.HomeOf(t);
            if (home == null || !Homes.IsHome(home)) return;
            int now = Now;
            if (turn == GodsAmongMath.DebtTurn.Paid)
            {
                if (!Rand.Chance(GodsAmongMath.PaidChance)) return;
                b.coinFor = Carrier(t, home);
                b.coinMap = b.coinFor != null ? home : null;
                b.coinAfter = b.coinFor != null ? now : -1;
                b.longerMap = home;
                b.longerUntil = now + GodsAmongMath.WaitDays * GenDate.TicksPerDay;
                b.ClearHard();
            }
            else
            {
                if (!Rand.Chance(GodsAmongMath.LapsedChance)) return;
                b.hardMap = home;
                b.hardUntil = now + GodsAmongMath.WaitDays * GenDate.TicksPerDay;
                b.ClearLonger();
            }
        }

        /// <summary>The one who carried the payment: the debt's own colonist at home, else the quartermaster, else the best talker.</summary>
        private static Pawn Carrier(Tie t, Map home)
        {
            bool Here(Pawn p) => p != null && !p.Dead && p.IsFreeNonSlaveColonist && p.MapHeld == home;
            var about = WorldLedger.PawnOf(t.terms.about);
            if (Here(about)) return about;
            var keeper = Interplay.Officer("Council_Office_Quartermaster", home);
            if (Here(keeper)) return keeper;
            return home.mapPawns.FreeColonistsSpawned.Where(p => !p.DevelopmentalStage.Juvenile() && !p.skills.GetSkill(SkillDefOf.Social).TotallyDisabled)
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Social).Level).FirstOrDefault();
        }

        /// <summary>The cell outside their bedroom's door (<paramref name="own"/>), else beside the colony's gate, else near them.</summary>
        public static IntVec3 Doorstep(Pawn p, Map map, out bool own)
        {
            own = false;
            try
            {
                var room = p.ownership?.OwnedBed?.GetRoom();
                if (room != null && !room.PsychologicallyOutdoors && p.ownership.OwnedBed.Map == map)
                    foreach (var door in room.ContainedAndAdjacentThings.OfType<Building_Door>())
                        foreach (var c in GenAdj.CellsAdjacentCardinal(door))
                            if (c.InBounds(map) && c.Standable(map) && c.GetRoom(map) != room && c.GetFirstItem(map) == null)
                            {
                                own = true;
                                return c;
                            }
                var gate = RoomNeighbours.GateCell(map);
                if (gate.IsValid) return CellFinder.StandableCellNear(gate, map, 3f);
                return p.Spawned && p.Map == map ? CellFinder.StandableCellNear(p.Position, map, 3f) : IntVec3.Invalid;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods among the people: a doorstep", e);
                return IntVec3.Invalid;
            }
        }

        // ================= The hour =================

        internal static void Hourly(GodsAmongBook b, int now)
        {
            if (b.coinFor != null && b.coinMap != null) Coin(b, now);
            if (b.longerMap != null && b.longerUntil >= 0) Longer(b, now);
            if (b.hardMap != null && b.hardUntil >= 0) Hard(b, now);
        }

        internal static void Step(GodsAmongBook b, int now)
        {
        }

        /// <summary>The morning after the debt was paid: one gold coin on the carrier's doorstep, and whoever walks by finds it.</summary>
        private static void Coin(GodsAmongBook b, int now)
        {
            var map = b.coinMap;
            var p = b.coinFor;
            if (!Homes.IsHome(map) || p.Dead || now - b.coinAfter > 3 * GenDate.TicksPerDay)
            {
                b.ClearCoin();
                return;
            }
            int hour = GenLocalDate.HourOfDay(map);
            if (hour < GodsAmongMath.CoinFrom || hour >= GodsAmongMath.CoinTo || GenDate.DaysPassed <= GenDate.DaysPassedAt(b.coinAfter)) return;
            var at = Doorstep(p, map, out bool own);
            b.ClearCoin();
            if (!at.IsValid) return;
            var coin = ThingMaker.MakeThing(ThingDefOf.Gold);
            coin.stackCount = 1;
            if (!GenPlace.TryPlaceThing(coin, at, map, ThingPlaceMode.Near)) return;
            var book = OwnHappenings.Book;
            var g = OwnDucat.God;
            if (book != null && g != null)
                OwnHappenings.Found(book, map, coin.Position, "debtcoin", g, coin.LabelNoCount, GodActKind.Blessing, "CC_GA_Saw_DebtCoin", "PAWN", p.LabelShort);
            Tell.Minor(NewsKind.Colony, Importance.Flavor, map, (own ? "CC_GA_DebtCoin" : "CC_GA_DebtCoinGate").Translate(p.Named("PAWN")).Resolve(), coin, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>The next trader at the gate after a debt paid: once they have settled at their spot, their stay runs half a day longer.</summary>
        private static void Longer(GodsAmongBook b, int now)
        {
            var map = b.longerMap;
            if (!Homes.IsHome(map) || now > b.longerUntil)
            {
                b.ClearLonger();
                return;
            }
            foreach (var lord in map.lordManager.lords.ToList())
            {
                // Settled at their spot (the game's own toil for it is internal: by its name).
                if (!(lord?.LordJob is LordJob_TradeWithColony) || lord.CurLordToil?.GetType().Name != "LordToil_DefendTraderCaravan") continue;
                if (!Lengthen(lord)) continue;
                b.ClearLonger();
                var trader = lord.ownedPawns.FirstOrDefault(x => x?.trader?.traderKind != null) ?? lord.ownedPawns.FirstOrDefault();
                if (trader != null)
                    Tell.Minor(NewsKind.Colony, Importance.Flavor, map, "CC_GA_TraderStays".Translate(trader.Named("TRADER"), (trader.Faction?.Name ?? "").Named("PEOPLE")).Resolve(), trader,
                        MessageTypeDefOf.NeutralEvent);
                return;
            }
        }

        /// <summary>The lord's timers out of its present stay put back by half a day: true when there was one to put back.</summary>
        public static bool Lengthen(Lord lord)
        {
            bool any = false;
            try
            {
                foreach (var tr in lord.Graph.transitions)
                {
                    if (tr?.sources == null || !tr.sources.Contains(lord.CurLordToil) || tr.triggers == null) continue;
                    foreach (var trig in tr.triggers)
                        if (trig is Trigger_TicksPassed && trig.data is TriggerData_TicksPassed d)
                        {
                            d.ticksPassed = GodsAmongMath.Lengthened(d.ticksPassed, GodsAmongMath.LongerTicks);
                            any = true;
                        }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods among the people: a trader's stay", e);
            }
            return any;
        }

        /// <summary>The next trader at the gate after a debt let lapse: they drive a hard bargain for the day.</summary>
        private static void Hard(GodsAmongBook b, int now)
        {
            var map = b.hardMap;
            if (!Homes.IsHome(map) || now > b.hardUntil)
            {
                b.ClearHard();
                return;
            }
            var trader = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p?.trader?.traderKind != null && p.Faction != null && !p.Faction.IsPlayer && !p.HostileTo(Faction.OfPlayer));
            if (trader == null) return;
            b.ClearHard();
            b.hardTrader = trader;
            b.hardTraderUntil = now + GodsAmongMath.HardTicks;
            Tell.Minor(NewsKind.Colony, Importance.Flavor, map, "CC_GA_HardBargain".Translate(trader.Named("TRADER")).Resolve(), trader, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>The colony's prices with this trader now: a hard bargain while it lasts (the postfix below).</summary>
        public static float Offset(Pawn trader)
        {
            var b = GodsAmongBook.Book;
            return b != null && trader != null && b.hardTrader == trader && Now < b.hardTraderUntil ? GodsAmongMath.HardBargain : 0f;
        }

        /// <summary>The trade window's plain line for this trader ("drives a hard bargain today"), or null.</summary>
        public static string WindowNote(ITrader trader) => trader is Pawn p && Offset(p) < 0f ? "CC_GA_HardBargainWindow".Translate().Resolve() : null;

        // ================= Dev =================

        public static string DevPaid(Map map)
        {
            var b = GodsAmongBook.Book;
            if (b == null || map == null) return "the gods are off.";
            b.coinFor = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            b.coinMap = map;
            b.coinAfter = Now - GenDate.TicksPerDay;
            b.longerMap = map;
            b.longerUntil = Now + GodsAmongMath.WaitDays * GenDate.TicksPerDay;
            b.ClearHard();
            return "a debt paid: the coin on " + (b.coinFor?.LabelShort ?? "nobody") + "'s doorstep in the morning (5 to 10 o'clock), the next trader to stay longer.";
        }

        public static string DevLapsed(Map map)
        {
            var b = GodsAmongBook.Book;
            if (b == null || map == null) return "the gods are off.";
            b.hardMap = map;
            b.hardUntil = Now + GodsAmongMath.WaitDays * GenDate.TicksPerDay;
            b.ClearLonger();
            return "a debt let lapse: the next trader at the gate drives a hard bargain for a day.";
        }
    }

    /// <summary>A trader after a debt let lapse drives a hard bargain for the day (Gods/Among/DucatDebts.cs). Off with the deeper game hooks.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.TradePriceImprovementOffsetForPlayer), MethodType.Getter)]
    public static class Patch_DucatHardBargain
    {
        public static void Postfix(Pawn __instance, ref float __result)
        {
            if (!RimfolkPatches.On || __instance?.Faction == null || __instance.Faction.IsPlayer) return;
            try
            {
                __result += DucatDebts.Offset(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods among the people: a hard bargain", e);
            }
        }
    }
}
