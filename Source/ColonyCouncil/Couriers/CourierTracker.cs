using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Courier trips waiting to come, and those on the map (see <see cref="Couriers"/>). Every 250 ticks each trip
    /// looks at the hour, at danger and at its people; the people's group (<see cref="LordJob_Courier"/>) and their jobs
    /// (<see cref="JobGiver_CourierWork"/>) do the rest. Nothing runs when no trip is waiting.
    /// </summary>
    public class CourierTracker : GameComponent
    {
        public List<CourierTrip> trips = new List<CourierTrip>();
        private int nextId = 1;

        private static CourierTracker cached;
        private static Game cachedGame;

        public static CourierTracker Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<CourierTracker>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public CourierTracker(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
        }

        private static int Now => Find.TickManager.TicksGame;

        public CourierTrip Get(int id)
        {
            foreach (var t in trips)
                if (t.id == id)
                    return t;
            return null;
        }

        /// <summary>The courier waiting for (or coming to) an errand.</summary>
        public CourierTrip ForPlay(int playId)
        {
            if (playId < 0) return null;
            foreach (var t in trips)
                if (t.playId == playId && t.state != CourierState.Done)
                    return t;
            return null;
        }

        /// <summary>The trip someone is on (by their group).</summary>
        public CourierTrip TripOf(Pawn p) => p?.GetLord()?.LordJob is LordJob_Courier j ? Get(j.tripId) : null;

        public void Add(CourierTrip t)
        {
            t.id = nextId++;
            t.state = CourierState.Pending;
            trips.Add(t);
            // Too many at once: the oldest still waiting is settled the old way now.
            if (trips.Count(x => x.state != CourierState.Done) > CourierMath.MaxTrips)
            {
                var old = trips.FirstOrDefault(x => x.state == CourierState.Pending && x != t);
                if (old != null) SettleNow(old, CourierEnd.NoWay, true);
            }
        }

        // ---------------- The clock ----------------

        public override void GameComponentTick()
        {
            if (trips.Count == 0) return;
            int now = Now;
            if (!Sched.Every(now, Sched.RareTicks, Sched.RareCouriers)) return;
            foreach (var t in trips.ToList())
            {
                try
                {
                    Step(t, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("courier trip " + t.kind, e);
                    try
                    {
                        End(t, CourierEnd.Lost);
                    }
                    catch (Exception e2)
                    {
                        CouncilLog.ErrorOnce("courier trip end", e2);
                        t.settled = true;
                        t.state = CourierState.Done;
                    }
                }
            }
            trips.RemoveAll(t => t.state == CourierState.Done && (t.endTick < 0 || now - t.endTick > CourierMath.MaxKeepTicks || AllGone(t)));
        }

        private void Step(CourierTrip t, int now)
        {
            switch (t.state)
            {
                case CourierState.Pending:
                {
                    bool mapThere = t.MapThere;
                    int hour = mapThere ? GenLocalDate.HourOfDay(t.map) : 12;
                    // An errand's courier comes when the errand may start, whatever the hour.
                    if (t.kind == CourierKind.Await) hour = CourierMath.DayFrom;
                    var verdict = CourierMath.Verdict(now, t.due, t.giveUp, hour, mapThere && Safe(t.map), Couriers.On, mapThere);
                    switch (verdict)
                    {
                        case CourierWait.Drop:
                            End(t, CourierEnd.MapGone);
                            return;
                        case CourierWait.Settle:
                            SettleNow(t, Couriers.On ? CourierEnd.NoWay : CourierEnd.SettingOff, !Couriers.On);
                            return;
                        case CourierWait.Go:
                            if (!SetOut(t)) SettleNow(t, CourierEnd.NoWay, false);
                            return;
                    }
                    return;
                }
                case CourierState.Coming:
                case CourierState.Working:
                    StepHere(t, now);
                    return;
                case CourierState.Leaving:
                case CourierState.Done:
                    ClearGone(t);
                    if (t.state == CourierState.Leaving && AllGone(t))
                    {
                        t.state = CourierState.Done;
                        if (t.endTick < 0) t.endTick = now;
                    }
                    return;
            }
        }

        /// <summary>No danger on the map, and weather people would walk in.</summary>
        public static bool Safe(Map map)
        {
            if (map == null || PlayOuts.WhyNotNow(map, false) != null) return false;
            float temp = map.mapTemperature.OutdoorTemp;
            if (temp < -25f || temp > 45f) return false;
            return !map.gameConditionManager.ConditionIsActive(GameConditionDefOf.ToxicFallout);
        }

        private void StepHere(CourierTrip t, int now)
        {
            if (!t.MapThere)
            {
                End(t, CourierEnd.MapGone);
                return;
            }
            ClearGone(t);
            if (t.harmed)
            {
                End(t, CourierEnd.Harmed);
                return;
            }
            if (t.faction == null || t.faction.HostileTo(Faction.OfPlayer))
            {
                End(t, CourierEnd.Hostile);
                return;
            }
            if (!Couriers.On)
            {
                End(t, CourierEnd.SettingOff);
                return;
            }
            var here = t.Here;
            if (here.Count == 0 || t.lord == null || !t.map.lordManager.lords.Contains(t.lord))
            {
                End(t, CourierEnd.Lost);
                return;
            }
            if (t.lord.CurLordToil is LordToil_ExitMapAndDefendSelf)
            {
                // The group turned to fight its way out (hurt, or their people turned on the colony).
                End(t, t.faction.HostileTo(Faction.OfPlayer) ? CourierEnd.Hostile : CourierEnd.Harmed);
                return;
            }
            if (t.map.dangerWatcher.DangerRating != StoryDanger.None)
            {
                End(t, CourierEnd.Danger);
                return;
            }
            if (t.state == CourierState.Coming)
            {
                if (t.lord.CurLordToil is LordToil_CourierWork)
                {
                    t.state = CourierState.Working;
                    t.arrived = now;
                    var first = t.Bearers.FirstOrDefault();
                    if (first != null) CourierWords.Say(first, t, t.kind == CourierKind.Await ? "await" : "arrive");
                }
                else if (now - t.created > CourierMath.GiveUpTicks * 2 || (t.arrived < 0 && now - t.due > CourierMath.TicksPerDay))
                {
                    End(t, CourierEnd.TimedOut);
                }
                return;
            }
            // At the gate: is the business done?
            if (BusinessDone(t))
            {
                if (t.doneTick < 0)
                {
                    t.doneTick = now;
                    CourierWords.Done(t);
                }
                if (now - t.doneTick >= CourierMath.LingerTicks) Leave(t, CourierEnd.Done);
                return;
            }
            int limit = t.kind == CourierKind.Await && !t.received ? CourierMath.AwaitTicks : CourierMath.WorkTicks;
            if (CourierMath.WorkOver(now, t.arrived, limit)) End(t, CourierEnd.TimedOut);
        }

        private static bool BusinessDone(CourierTrip t)
        {
            if (t.Brings && t.StillCarrying) return false;
            if (t.kind == CourierKind.Await) return t.received;
            if (t.Takes)
            {
                // Taken all, or the colony has none of it left at all.
                int stock = t.things.Count > 0 ? t.things.Count(x => x != null && x.Spawned && x.Map == t.map) : PlayCarry.Have(t.map, t.what, t.def);
                if (t.things.Count > 0 && stock == 0) return true;
                return CourierMath.CollectDone(t.amount, t.taken, stock);
            }
            return true;
        }

        // ---------------- Setting out ----------------

        /// <summary>The people (and the pack animal) are made, given what they bring, and walk in from the edge.</summary>
        private bool SetOut(CourierTrip t)
        {
            var map = t.map;
            var f = Couriers.Choose(t.preferred, map);
            if (f == null) return false;
            var spot = PlaySpots.GateCell(map, null);
            bool gate = spot.IsValid;
            if (!spot.IsValid) spot = DropCellFinder.TradeDropSpot(map);
            if (!spot.IsValid || !spot.InBounds(map)) return false;
            var tp = TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false);
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Friendly, false,
                    c => map.reachability.CanReach(c, spot, PathEndMode.OnCell, tp))) return false;
            var personKind = Couriers.PersonKind(f);
            if (personKind == null) return false;
            float bulk = Bulk(t);
            var animalKind = Couriers.AnimalKind(f, map);
            bool animal = t.kind != CourierKind.Await && CourierMath.PackAnimal(bulk, animalKind != null);
            int n = t.kind == CourierKind.Await ? 1 : CourierMath.PeopleWithAnimal(bulk, animal);
            var people = new List<Pawn>();
            Pawn beast = null;
            try
            {
                for (int i = 0; i < n; i++)
                {
                    var p = Arrivals.Make(personKind, f);
                    if (p == null) continue;
                    // Their own things stay at home: what they carry here is the trip's.
                    p.inventory?.DestroyAll();
                    people.Add(p);
                }
                if (animal)
                {
                    beast = PawnGenerator.GeneratePawn(new PawnGenerationRequest(animalKind, f, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
                    beast?.inventory?.DestroyAll();
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("courier people", e);
            }
            if (people.Count == 0)
            {
                if (beast != null) Arrivals.Discard(beast);
                return false;
            }
            var party = new List<Pawn>(people);
            if (beast != null) party.Add(beast);
            foreach (var p in party) GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(entry, map, 3), map);
            t.faction = f;
            t.spot = spot;
            t.atGate = gate;
            t.people = people;
            t.animal = beast;
            if (!t.oldSpot.IsValid) t.oldSpot = DropCellFinder.TradeDropSpot(map);
            Pack(t, party);
            try
            {
                t.lord = LordMaker.MakeNewLord(f, new LordJob_Courier(t.id, spot), map, party);
            }
            catch (Exception e)
            {
                // No group to walk in with: what they carry is set down where they stand, and they go.
                CouncilLog.ErrorOnce("courier group", e);
                foreach (var p in party) DropAll(t, p, p.Position);
                t.goods.TryDropAll(spot, map, ThingPlaceMode.Near);
                Arrivals.SendAway(people);
                t.settled = true;
                t.state = CourierState.Done;
                t.endTick = Now;
                return true;
            }
            t.state = CourierState.Coming;
            CourierWords.Arrive(t);
            return true;
        }

        /// <summary>The trip's load, in bulk: what they bring or what they take, whichever is more.</summary>
        private static float Bulk(CourierTrip t)
        {
            float bring = 0f;
            foreach (var thing in t.goods)
                bring += CourierMath.Bulk(thing.GetStatValue(StatDefOf.Mass) * thing.stackCount, thing.stackCount / (float)Mathf.Max(1, thing.def.stackLimit));
            float take = 0f;
            if (t.things.Count > 0)
            {
                foreach (var thing in t.things.Where(x => x != null && !x.Destroyed))
                    take += CourierMath.Bulk(thing.GetStatValue(StatDefOf.Mass) * thing.stackCount, thing.stackCount / (float)Mathf.Max(1, thing.def.stackLimit));
            }
            else if (t.amount > 0)
            {
                var d = t.def ?? PlayCarry.Stock(t.map, t.what, null).FirstOrDefault()?.def;
                if (d != null) take = CourierMath.Bulk(d.GetStatValueAbstract(StatDefOf.Mass) * t.amount, t.amount / (float)Mathf.Max(1, d.stackLimit));
                else take = t.amount * 0.1f;
            }
            return Mathf.Max(bring, take);
        }

        /// <summary>What they bring goes in their packs, shared out by how much each can carry (the animal most).</summary>
        private static void Pack(CourierTrip t, List<Pawn> party)
        {
            if (t.goods.Count == 0) return;
            var room = party.Select(p => Mathf.Max(1f, MassUtility.Capacity(p))).ToList();
            var load = new float[party.Count];
            foreach (var thing in t.goods.OrderByDescending(x => x.GetStatValue(StatDefOf.Mass) * x.stackCount).ToList())
            {
                int left = thing.stackCount;
                float unit = Mathf.Max(0.001f, thing.GetStatValue(StatDefOf.Mass));
                while (left > 0 && !thing.Destroyed)
                {
                    int best = 0;
                    for (int i = 1; i < party.Count; i++)
                        if (room[i] - load[i] > room[best] - load[best]) best = i;
                    int fits = Mathf.Max(1, Mathf.FloorToInt((room[best] - load[best]) / unit));
                    int n = Mathf.Min(left, fits);
                    // The last bearer with room takes the rest, rather than split hairs over a few.
                    if (party.Count == 1 || room.Select((r, i) => r - load[i]).Count(r => r > unit) <= 1) n = left;
                    var inv = party[best].inventory?.innerContainer;
                    int moved = inv == null ? 0 : t.goods.TryTransferToContainer(thing, inv, n, true);
                    if (moved <= 0) break;
                    load[best] += moved * unit;
                    left -= moved;
                    if (!t.toUnload.Contains(party[best])) t.toUnload.Add(party[best]);
                }
            }
        }

        // ---------------- Their business ----------------

        /// <summary>
        /// Someone of the trip sets down what <paramref name="from"/> (themselves, or the pack animal beside them) carries
        /// for the colony, by <paramref name="at"/>. What's set down is the colony's.
        /// </summary>
        public void Unload(CourierTrip t, Pawn by, Pawn from, IntVec3 at)
        {
            if (t == null || from == null || !t.toUnload.Contains(from)) return;
            int n = DropAll(t, from, at);
            t.toUnload.Remove(from);
            t.unloaded += n;
            if (by != null && n > 0) CourierWords.Say(by, t, "deliver");
        }

        /// <summary>Everything in someone's pack set down near a cell (theirs, for a courier, is all the trip's); how many things.</summary>
        private static int DropAll(CourierTrip t, Pawn p, IntVec3 at)
        {
            var inv = p?.inventory?.innerContainer;
            var map = p?.MapHeld ?? t.map;
            if (inv == null || inv.Count == 0 || map == null) return 0;
            if (!at.IsValid || !at.InBounds(map)) at = p.Spawned ? p.Position : t.oldSpot;
            int n = 0;
            foreach (var thing in inv.ToList())
            {
                int count = thing.stackCount;
                if (inv.TryDrop(thing, at, map, ThingPlaceMode.Near, count, out var lying))
                {
                    lying?.SetForbidden(false, false);
                    n += count;
                }
            }
            return n;
        }

        /// <summary>One of them took <paramref name="n"/> from the stores.</summary>
        public void Notify_Loaded(CourierTrip t, Pawn by, int n)
        {
            if (t == null || n <= 0) return;
            bool first = t.taken == 0;
            t.taken += n;
            if (first && by != null) CourierWords.Say(by, t, "collect");
        }

        // ---------------- Ending ----------------

        /// <summary>A trip that never came (or couldn't): what it brought is set down and what it was to take is taken, the old way, now.</summary>
        private void SettleNow(CourierTrip t, CourierEnd end, bool quiet)
        {
            t.quiet = t.quiet || quiet;
            End(t, end);
        }

        /// <summary>
        /// It's over, early or not: the rest of the deal settled (or not) as CourierMath says, once; the group sent on its
        /// way if they're still about.
        /// </summary>
        private void End(CourierTrip t, CourierEnd end)
        {
            if (t.state == CourierState.Done || t.state == CourierState.Leaving)
            {
                if (!t.settled) Settle(t, end);
                return;
            }
            bool wasHere = t.state == CourierState.Coming || t.state == CourierState.Working;
            Settle(t, end);
            if (end == CourierEnd.Harmed || end == CourierEnd.Hostile || !wasHere)
            {
                // A group that fights its way out (or never came) goes on its own.
                t.state = wasHere ? CourierState.Leaving : CourierState.Done;
                t.endTick = Now;
                return;
            }
            Leave(t, end);
        }

        /// <summary>They walk off (their business done, or given up on).</summary>
        private void Leave(CourierTrip t, CourierEnd end)
        {
            if (!t.settled) Settle(t, end);
            t.end = end;
            t.state = CourierState.Leaving;
            t.endTick = Now;
            try
            {
                if (t.lord != null && t.MapThere && t.map.lordManager.lords.Contains(t.lord)) t.lord.ReceiveMemo(LordJob_Courier.MemoLeave);
                else Arrivals.SendAway(t.people);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("courier leave", e);
            }
            if (end == CourierEnd.Done)
            {
                var last = t.Bearers.FirstOrDefault();
                if (last != null && Rand.Chance(0.5f)) CourierWords.Say(last, t, "leave");
            }
        }

        /// <summary>The rest of the deal, once: goods still owed to the colony set down, goods still owed by it taken.</summary>
        private void Settle(CourierTrip t, CourierEnd end)
        {
            if (t.settled) return;
            t.settled = true;
            t.end = end;
            var map = t.MapThere ? t.map : null;
            var look = new List<Thing>();
            bool incoming = false, outgoing = false;
            // Owed to the colony.
            if (map != null && CourierMath.SettleIncoming(end))
            {
                foreach (var p in t.toUnload.ToList())
                {
                    if (p == null || p.Dead || !p.Spawned || p.Map != map)
                    {
                        // Gone with it (or lying with them): nothing to set down here.
                        if (p != null && !p.Dead && !p.Spawned) t.goods.TryAddRangeOrTransfer(p.inventory?.innerContainer?.ToList() ?? new List<Thing>(), true, true);
                        continue;
                    }
                    // They set it down where they stand (if the colony can get to it), else where traders leave goods.
                    var at = p.Position.InBounds(map) && map.reachability.CanReachColony(p.Position) ? p.Position : t.oldSpot;
                    if (DropAll(t, p, at) > 0) incoming = true;
                    look.Add(p);
                }
                t.toUnload.Clear();
                if (t.goods.Count > 0)
                {
                    var at = t.oldSpot.IsValid && t.oldSpot.InBounds(map) ? t.oldSpot : DropCellFinder.TradeDropSpot(map);
                    foreach (var thing in t.goods.ToList())
                        if (t.goods.TryDrop(thing, at, map, ThingPlaceMode.Near, thing.stackCount, out var lying))
                        {
                            lying?.SetForbidden(false, false);
                            if (lying != null) look.Add(lying);
                            incoming = true;
                        }
                }
            }
            if (t.goods.Count > 0) t.goods.ClearAndDestroyContents();
            // Owed by the colony.
            if (map != null && t.Takes && CourierMath.SettleOutgoing(end))
            {
                int left = t.LeftToTake;
                if (left > 0)
                {
                    int got;
                    if (t.things.Count > 0)
                    {
                        got = 0;
                        foreach (var thing in t.things.Where(x => x != null && !x.Destroyed && x.Spawned).ToList())
                        {
                            got += thing.stackCount;
                            thing.Destroy();
                        }
                    }
                    else got = PlayCarry.Take(map, t.what, t.def, left);
                    if (got > 0) outgoing = true;
                    t.taken += got;
                }
            }
            if (!t.quiet && (incoming || outgoing)) CourierWords.Settled(t, end, incoming, outgoing, look);
        }

        /// <summary>One of them was lost to the group (from its LordJob).</summary>
        public void Notify_Lost(int tripId, Pawn p, PawnLostCondition cond)
        {
            var t = Get(tripId);
            if (t == null || p == null) return;
            try
            {
                switch (cond)
                {
                    case PawnLostCondition.MadePrisoner:
                    case PawnLostCondition.MadeSlave:
                        t.harmed = true;
                        if (!t.settled && t.state != CourierState.Pending) End(t, CourierEnd.Harmed);
                        break;
                    case PawnLostCondition.ExitedMap:
                        // Off the map before their business was settled (the group's own clock): settle it first, from
                        // what they still carry, then clear their packs.
                        if (!t.settled) End(t, t.harmed ? CourierEnd.Harmed : CourierEnd.Lost);
                        Clear(p);
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("courier lost", e);
            }
        }

        public void Notify_Harmed(int tripId)
        {
            var t = Get(tripId);
            if (t != null) t.harmed = true;
        }

        /// <summary>What a courier carries off the map is gone with them, not kept by the world's people.</summary>
        private static void Clear(Pawn p)
        {
            try
            {
                if (p?.inventory?.innerContainer != null && p.inventory.innerContainer.Count > 0) p.inventory.DestroyAll();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("courier clear", e);
            }
        }

        /// <summary>Those who have gone off the map (alive) have their packs cleared.</summary>
        private static void ClearGone(CourierTrip t)
        {
            foreach (var p in t.Party)
                if (!p.Dead && !p.Spawned && !p.Discarded && p.Faction != Faction.OfPlayer && !p.IsPrisoner) Clear(p);
        }

        private static bool AllGone(CourierTrip t) => t.Party.All(p => p.Dead || p.Discarded || !p.Spawned || p.Map != t.map || p.Faction == Faction.OfPlayer);

        // ---------------- Saving ----------------

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref trips, "trips", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (trips == null) trips = new List<CourierTrip>();
                // A group lost in loading (a mod gone, an old save) is noticed on the trip's next turn: settled, and let go.
                trips.RemoveAll(t => t == null);
            }
        }
    }
}
