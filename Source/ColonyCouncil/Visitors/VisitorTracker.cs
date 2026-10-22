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
    /// Visits waiting to come and those on the map (see <see cref="Visitors"/>). Every 250 ticks each one looks at the
    /// hour, at danger, at the weather, at their letter and at their people; their group (<see cref="LordJob_RimfolkVisit"/>)
    /// does the walking. Nothing runs when nobody is visiting.
    /// </summary>
    public class VisitorTracker : GameComponent
    {
        public List<VisitorTrip> trips = new List<VisitorTrip>();
        private int nextId = 1;

        private static VisitorTracker cached;
        private static Game cachedGame;

        public static VisitorTracker Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<VisitorTracker>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public VisitorTracker(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
        }

        private static int Now => Find.TickManager.TicksGame;

        public VisitorTrip Get(int id)
        {
            foreach (var t in trips)
                if (t.id == id)
                    return t;
            return null;
        }

        /// <summary>The visit someone is on, or null.</summary>
        public VisitorTrip TripOf(Pawn p)
        {
            if (p == null) return null;
            if (p.GetLord()?.LordJob is LordJob_RimfolkVisit j) return Get(j.tripId);
            return null;
        }

        /// <summary>Those at the gate (or on their way to it) with this question: by its X, else by the letter it came with.</summary>
        public VisitorTrip WaitingFor(DilemmaContext ctx)
        {
            if (ctx == null) return null;
            foreach (var t in trips)
                if ((t.state == VisitState.Coming || t.state == VisitState.Here) && ctx.stranger != null && t.people.Contains(ctx.stranger))
                    return t;
            foreach (var t in trips)
                if ((t.state == VisitState.Coming || t.state == VisitState.Here) && t.ctxRef == ctx)
                    return t;
            return null;
        }

        // ---------------- Adding ----------------

        /// <summary>A visit to come later (at its hour, when it's safe).</summary>
        public void Add(VisitorTrip t)
        {
            t.id = nextId++;
            t.state = VisitState.Pending;
            trips.Add(t);
            // Too many at once: the oldest still waiting won't come after all (its letter tells it).
            if (trips.Count(x => x.state != VisitState.Done) > VisitorMath.MaxTrips)
            {
                var old = trips.FirstOrDefault(x => x.state == VisitState.Pending && x != t);
                if (old != null) Finish(old, VisitEnd.NoWay);
            }
        }

        /// <summary>A visit that comes now (someone at the gate with a letter): false if they can't, and the letter alone tells it.</summary>
        public bool ComeNow(VisitorTrip t)
        {
            if (t?.map == null || !Visitors.On || !Visitors.Safe(t.map) || !VisitorMath.Room(Visitors.OnMap(t.map))) return false;
            if (trips.Count(x => x.state != VisitState.Done) >= VisitorMath.MaxTrips) return false;
            t.id = nextId++;
            t.state = VisitState.Pending;
            try
            {
                if (!SetOut(t)) return false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitor come now", e);
                return false;
            }
            trips.Add(t);
            if (t.ctxRef != null) lettersToFind++;
            return true;
        }

        // ---------------- The clock ----------------

        private int lettersToFind;

        public override void GameComponentUpdate()
        {
            // A letter that came with someone at the gate is looked for at once (even paused, so an answer given at once
            // is still theirs).
            if (lettersToFind <= 0) return;
            lettersToFind = 0;
            foreach (var t in trips)
                if (t.letterId == -1 && t.ctxRef != null)
                    FindLetter(t);
        }

        public override void GameComponentTick()
        {
            if (trips.Count == 0) return;
            int now = Now;
            if (!Sched.Every(now, Sched.RareTicks, Sched.RareVisitors)) return;
            foreach (var t in trips.ToList())
            {
                try
                {
                    Step(t, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("visitor trip " + t.kind, e);
                    try
                    {
                        Leave(t, VisitEnd.Lost, false);
                    }
                    catch (Exception e2)
                    {
                        CouncilLog.ErrorOnce("visitor trip end", e2);
                        t.state = VisitState.Done;
                        t.endTick = now;
                    }
                }
            }
            trips.RemoveAll(t => t.state == VisitState.Done && (t.endTick < 0 || now - t.endTick > VisitorMath.MaxKeepTicks || AllGone(t)));
        }

        private void Step(VisitorTrip t, int now)
        {
            if (t.state != VisitState.Done && t.state != VisitState.Leaving) HoldBeat(t, now);
            switch (t.state)
            {
                case VisitState.Pending:
                {
                    bool mapThere = t.MapThere;
                    int hour = mapThere ? GenLocalDate.HourOfDay(t.map) : 12;
                    bool ok = mapThere && Visitors.Safe(t.map) && VisitorMath.Room(Visitors.OnMap(t.map)) && Visitors.Usable(t.faction);
                    switch (VisitorMath.Verdict(now, t.due, t.giveUp, hour, t.hourFrom, t.hourTo, ok, Visitors.On, mapThere))
                    {
                        case VisitWait.Drop:
                            Finish(t, mapThere ? VisitEnd.NoWay : VisitEnd.MapGone);
                            return;
                        case VisitWait.Go:
                            if (!SetOut(t)) Finish(t, VisitEnd.NoWay);
                            return;
                    }
                    return;
                }
                case VisitState.Coming:
                case VisitState.Here:
                    StepHere(t, now);
                    return;
                case VisitState.Leaving:
                    if (AllGone(t))
                    {
                        t.state = VisitState.Done;
                        if (t.endTick < 0) t.endTick = now;
                    }
                    return;
            }
        }

        private void StepHere(VisitorTrip t, int now)
        {
            if (!t.MapThere)
            {
                Finish(t, VisitEnd.MapGone);
                return;
            }
            if (t.harmed)
            {
                Gone(t, VisitEnd.Harmed);
                return;
            }
            if (t.faction == null || t.faction.HostileTo(Faction.OfPlayer))
            {
                Gone(t, VisitEnd.Hostile);
                return;
            }
            if (!Visitors.On)
            {
                Leave(t, VisitEnd.SettingOff, false);
                return;
            }
            var here = t.Here;
            if (here.Count == 0 || t.lord == null || !t.map.lordManager.lords.Contains(t.lord) || t.Standing.Count == 0)
            {
                Leave(t, VisitEnd.Lost, false);
                return;
            }
            if (t.lord.CurLordToil is LordToil_ExitMapAndDefendSelf)
            {
                Gone(t, t.faction.HostileTo(Faction.OfPlayer) ? VisitEnd.Hostile : VisitEnd.Harmed);
                return;
            }
            if (t.map.dangerWatcher.DangerRating != StoryDanger.None || MapFacts.ThreatOn(t.map))
            {
                Leave(t, VisitEnd.Danger, true);
                return;
            }
            if (t.Standing.Any(Visitors.Suffering))
            {
                Leave(t, VisitEnd.Weather, true);
                return;
            }
            if (t.state == VisitState.Coming)
            {
                if (t.lord.CurLordToil is LordToil_VisitHere)
                {
                    t.state = VisitState.Here;
                    t.arrived = now;
                    // Come to stay the night (a herd on the far meadow): only if the night out won't hurt them.
                    if (t.kind == VisitKind.Stay && t.leaveAt < 0)
                    {
                        if (!Visitors.SafeOvernight(t.Here, t.spot, t.map))
                        {
                            Leave(t, VisitEnd.Weather, false);
                            return;
                        }
                        t.leaveAt = now + VisitorMath.HoursToDawn(GenLocalDate.HourOfDay(t.map), Math.Max(1, t.stayNights)) * VisitorMath.TicksPerHour
                                    + Rand.Range(0, VisitorMath.TicksPerHour * 2);
                    }
                    VisitorWords.Say(t.Standing.FirstOrDefault(), t, t.kind == VisitKind.Pass ? "pass" : "arrive");
                }
                else if (t.setOut >= 0 && now - t.setOut > VisitorMath.TicksPerHour * 8)
                {
                    Leave(t, VisitEnd.TimedOut, false);
                }
                return;
            }
            switch (t.kind)
            {
                case VisitKind.Wait:
                {
                    if (t.letterId == -1) FindLetter(t);
                    bool hasLetter = t.letterId >= 0;
                    bool open = hasLetter && Visitors.LetterOpen(t.letterId);
                    if (hasLetter && !open && t.answeredAt < 0)
                    {
                        t.answeredAt = now;
                        // Given a bed for the night: they stay.
                        if (t.stayNights > 0 && BeginStay(t, now)) return;
                    }
                    if (!t.saidWait && now - t.arrived > VisitorMath.TicksPerHour * 2 && (open || !hasLetter))
                    {
                        t.saidWait = true;
                        if (Rand.Chance(0.6f)) VisitorWords.Say(t.Standing.RandomElementWithFallback(), t, "wait");
                    }
                    if (VisitorMath.WaitOver(now, t.arrived, hasLetter, open, t.answeredAt, Visitors.ErrandComing(t), Mathf.RoundToInt(t.hours * VisitorMath.TicksPerHour)))
                        Leave(t, now - t.arrived >= VisitorMath.MaxWaitTicks ? VisitEnd.TimedOut : VisitEnd.Done, false);
                    return;
                }
                case VisitKind.Stay:
                    if (t.leaveAt < 0) t.leaveAt = now;
                    if (now >= t.leaveAt && (!Visitors.ErrandComing(t) || now - t.leaveAt > VisitorMath.ErrandWaitTicks)) Leave(t, VisitEnd.Done, false);
                    return;
                default:
                    if (now - t.arrived >= Mathf.RoundToInt(t.hours * VisitorMath.TicksPerHour)) Leave(t, VisitEnd.Done, false);
                    return;
            }
        }

        /// <summary>The beat that tells of this visit afterward comes no sooner than an hour after they've gone.</summary>
        private static void HoldBeat(VisitorTrip t, int now)
        {
            if (t.holdBeat.NullOrEmpty() || t.situationId < 0) return;
            var bs = WorldSituations.Instance?.Get(t.situationId)?.beats?.FirstOrDefault(b => b.key == t.holdBeat);
            if (bs != null && bs.due < now + VisitorMath.TicksPerHour) bs.due = now + VisitorMath.TicksPerHour;
        }

        // ---------------- Letters ----------------

        private static void FindLetter(VisitorTrip t)
        {
            try
            {
                var l = t.ctxRef != null ? Visitors.LetterOf(t.ctxRef) : Visitors.LetterFor(t);
                t.letterId = l?.ID ?? -2;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitor letter", e);
                t.letterId = -2;
            }
        }

        // ---------------- Staying ----------------

        /// <summary>The answer gives those waiting a bed for <paramref name="nights"/> nights (VisitEffect_Stay).</summary>
        public void Stay(VisitorTrip t, int nights, VisitSpot spot)
        {
            if (t == null) return;
            t.stayNights = Mathf.Clamp(nights, 1, VisitorMath.MaxNights);
            t.staySpot = spot;
            // Already answered (the letter was found closed): they stay from now.
            if (t.state == VisitState.Here && t.answeredAt >= 0 && t.kind == VisitKind.Wait) BeginStay(t, Now);
        }

        /// <summary>
        /// They stay until first light after their nights, where they are (by the gate), if a night out there won't hurt
        /// them; else they go on now, as people do when there's no room.
        /// </summary>
        private bool BeginStay(VisitorTrip t, int now)
        {
            var at = t.spot;
            if (t.staySpot != VisitSpot.Gate)
            {
                var c = Visitors.SpotFor(t.staySpot, t.map, t.spot);
                if (c.IsValid && c.InBounds(t.map) && t.Standing.All(p => p.CanReach(c, PathEndMode.OnCell, Danger.Some))) at = c;
            }
            if (!Visitors.SafeOvernight(t.Here, at, t.map)) return false;
            if (at != t.spot && !Move(t, at)) return false;
            t.kind = VisitKind.Stay;
            int hour = GenLocalDate.HourOfDay(t.map);
            t.leaveAt = now + VisitorMath.HoursToDawn(hour, t.stayNights) * VisitorMath.TicksPerHour + Rand.Range(0, VisitorMath.TicksPerHour * 2);
            VisitorWords.Say(t.Standing.FirstOrDefault(), t, "stay");
            return true;
        }

        /// <summary>They go over to another spot to stay (a new group with the same people, walking there).</summary>
        private bool Move(VisitorTrip t, IntVec3 to)
        {
            var party = t.Here;
            if (party.Count == 0) return false;
            try
            {
                foreach (var p in party) t.lord.RemovePawn(p);
                t.spot = to;
                t.lord = LordMaker.MakeNewLord(t.faction, new LordJob_RimfolkVisit(t.id, to, t.exit, t.animals.Count > 2 ? 9f : 6f), t.map, party);
                t.state = VisitState.Coming;
                t.setOut = Now;
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitor move", e);
                Arrivals.SendAway(party);
                t.state = VisitState.Leaving;
                t.endTick = Now;
                return false;
            }
        }

        // ---------------- Setting out ----------------

        /// <summary>The people (and animals) are made and walk in from the edge to their spot.</summary>
        private bool SetOut(VisitorTrip t)
        {
            var map = t.map;
            var f = t.faction;
            if (map == null || !Visitors.Usable(f)) return false;
            var tp = TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false);
            var gate = PlaySpots.GateCell(map, null);
            if (!gate.IsValid) gate = DropCellFinder.TradeDropSpot(map);
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Friendly, false,
                    c => !gate.IsValid || map.reachability.CanReach(c, gate, PathEndMode.OnCell, tp))) return false;
            var spot = t.spotKind == VisitSpot.Gate ? gate : Visitors.SpotFor(t.spotKind, map, entry);
            if (!spot.IsValid || !map.reachability.CanReach(entry, spot, PathEndMode.OnCell, tp)) return false;
            var people = Visitors.MakePeople(t, f, map);
            if (people.Count == 0) return false;
            var animals = Visitors.MakeAnimals(t, f, map);
            var party = new List<Pawn>(people);
            party.AddRange(animals);
            foreach (var p in party) GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(entry, map, 3), map);
            t.people = people;
            t.animals = animals;
            t.spot = spot;
            t.exit = t.onward ? Visitors.FarEdge(map, entry, spot) : IntVec3.Invalid;
            float radius = t.spotKind == VisitSpot.Gate ? 4f : animals.Count > 2 ? 9f : 6f;
            try
            {
                t.lord = LordMaker.MakeNewLord(f, new LordJob_RimfolkVisit(t.id, spot, t.exit, radius), map, party);
            }
            catch (Exception e)
            {
                // No group to walk in with: they turn round.
                CouncilLog.ErrorOnce("visitor group", e);
                Arrivals.SendAway(people);
                foreach (var a in animals.Where(a => a.Spawned)) Away(a);
                t.state = VisitState.Done;
                t.endTick = Now;
                return true;
            }
            t.state = VisitState.Coming;
            t.setOut = Now;
            VisitorWords.Coming(t);
            return true;
        }

        // ---------------- Ending ----------------

        /// <summary>They walk off: back the way they came, or on past the colony; at a jog if there's danger.</summary>
        private void Leave(VisitorTrip t, VisitEnd end, bool hurry)
        {
            bool wasHere = t.state == VisitState.Coming || t.state == VisitState.Here;
            t.end = end;
            t.state = VisitState.Leaving;
            t.endTick = Now;
            Visitors.Forget(t);
            try
            {
                if (t.lord != null && t.MapThere && t.map.lordManager.lords.Contains(t.lord))
                    t.lord.ReceiveMemo(hurry ? LordJob_RimfolkVisit.MemoHurry : LordJob_RimfolkVisit.MemoLeave);
                else
                {
                    Arrivals.SendAway(t.people);
                    foreach (var a in t.animals.Where(a => a != null && a.Spawned && !a.Dead && a.Faction != Faction.OfPlayer)) Away(a);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitor leave", e);
            }
            if (!wasHere) return;
            if (end == VisitEnd.Done && Rand.Chance(0.5f)) VisitorWords.Say(t.Standing.FirstOrDefault(), t, "leave");
            if (end == VisitEnd.Weather) VisitorWords.LeftForWeather(t);
        }

        /// <summary>They're leaving on their own (hurt, or their people turned hostile): the group fights its way out.</summary>
        private void Gone(VisitorTrip t, VisitEnd end)
        {
            t.end = end;
            t.state = VisitState.Leaving;
            t.endTick = Now;
            Visitors.Forget(t);
        }

        /// <summary>It's over without anyone (left) on the map: never came, the map gone.</summary>
        private void Finish(VisitorTrip t, VisitEnd end)
        {
            t.end = end;
            t.state = VisitState.Done;
            t.endTick = Now;
            Visitors.Forget(t);
        }

        /// <summary>An animal of theirs left on its own walks off the map.</summary>
        private static void Away(Pawn a)
        {
            try
            {
                a.GetLord()?.RemovePawn(a);
                if (!a.Downed && ReachabilityUtility.CanReachMapEdge(a))
                    LordMaker.MakeNewLord(a.Faction, new LordJob_ExitMapBest(LocomotionUrgency.Walk, false, false), a.Map, new List<Pawn> { a });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitor animal away", e);
            }
        }

        /// <summary>The colony took them in (Visitors.TakeIn): the visit is over; anyone of it not taken in goes home.</summary>
        public void Joined(VisitorTrip t, List<Pawn> joined)
        {
            if (t == null) return;
            t.people.RemoveAll(joined.Contains);
            t.animals.RemoveAll(joined.Contains);
            t.end = VisitEnd.Joined;
            t.endTick = Now;
            if (t.Here.Count > 0)
            {
                t.state = VisitState.Leaving;
                t.lord?.ReceiveMemo(LordJob_RimfolkVisit.MemoLeave);
            }
            else t.state = VisitState.Done;
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
                        break;
                    case PawnLostCondition.ExitedMap:
                        // What they were given goes with them, not kept by the world's people.
                        if (p.inventory?.innerContainer != null && p.inventory.innerContainer.Count > 0) p.inventory.DestroyAll();
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("visitor lost", e);
            }
        }

        public void Notify_Harmed(int tripId)
        {
            var t = Get(tripId);
            if (t != null) t.harmed = true;
        }

        private static bool AllGone(VisitorTrip t) => t.Party.All(p => p.Dead || p.Discarded || p.MapHeld != t.map || p.Faction == Faction.OfPlayer || p.GetLord() != t.lord);

        // ---------------- Saving ----------------

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref trips, "trips", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (trips == null) trips = new List<VisitorTrip>();
                trips.RemoveAll(t => t == null);
            }
        }
    }
}
