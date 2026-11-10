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
    /// A regular post, by the colony's age and its region's peoples' (docs/design/post-and-letters.md, step 1, point 3): no
    /// post for a tribal colony (travellers, camps and its own rider only); a post rider on the nearest road every 7 to 14
    /// days in the medieval age, a mail run every 3 to 7 in the industrial, a shuttle every 2 to 4 in the spacer (the post's
    /// age is what the colony and the best of its neighbours share: PostMath.PostAge; the pace is a setting). The rider is
    /// one of the post's people's lighter caravan hands, with a mount of their people's caravans (or a horse) where the land
    /// allows: with Giddy-Up 2 they ride it, mounted as they come the way Giddy-Up brings its own riders in; otherwise they lead it. A spacer post comes by drop
    /// pod at the mailbag. They come by day when the map is safe, set down the mail for the colony at the mailbag, take what
    /// goes out on the post, and ride on.
    /// </summary>
    public static class PostRider
    {
        private static int Now => Find.TickManager.TicksGame;

        private static PostBook Book => PostBook.Instance;

        /// <summary>The peoples a home's post could be run by: at peace, with a town within the region's reach, nearest first.</summary>
        private static List<(Faction f, float tiles)> Peoples(Map map)
        {
            var list = new List<(Faction, float)>();
            foreach (var f in Find.FactionManager.AllFactionsListForReading)
            {
                if (f == null || f.IsPlayer || f.Hidden || f.defeated || f.temporary || f.def == null || !f.def.humanlikeFaction || f.HostileTo(Faction.OfPlayer)) continue;
                var town = WorldSituationWorker.NearestTown(f, map);
                if (town == null) continue;
                float d = WorldSituationWorker.Distance(map, town);
                if (d > NotableMath.ReachTiles) continue;
                list.Add((f, d));
            }
            return list.OrderBy(x => x.Item2).ToList();
        }

        private static readonly Dictionary<int, (WorldAge age, float at)> ages = new Dictionary<int, (WorldAge, float)>();

        /// <summary>The post's age for a home: the colony's and its best neighbour's, the lower of the two (looked at every few seconds a map).</summary>
        public static WorldAge AgeOf(Map map)
        {
            if (map == null) return WorldAge.Tribal;
            float t = Time.realtimeSinceStartup;
            if (ages.TryGetValue(map.uniqueID, out var a0) && t - a0.at < 10f && t >= a0.at) return a0.age;
            var age = AgeNow(map);
            ages[map.uniqueID] = (age, t);
            return age;
        }

        private static WorldAge AgeNow(Map map)
        {
            var best = WorldAge.Tribal;
            foreach (var (f, _) in Peoples(map))
            {
                var a = Ages.Of(f);
                if (a > best) best = a;
            }
            return PostMath.PostAge(Ages.Ours, best);
        }

        private static readonly Dictionary<int, (bool runs, float at)> runs = new Dictionary<int, (bool, float)>();

        /// <summary>A home has a regular post (looked at every few seconds a map).</summary>
        public static bool Runs(Map map)
        {
            if (map == null || !PostBook.On) return false;
            float t = Time.realtimeSinceStartup;
            if (runs.TryGetValue(map.uniqueID, out var r) && t - r.at < 10f && t >= r.at) return r.runs;
            bool yes = PostMath.HasPost(AgeOf(map)) && PeopleFor(map, AgeOf(map)) != null;
            runs[map.uniqueID] = (yes, t);
            return yes;
        }

        /// <summary>Whose post it is: the nearest people at peace of the post's age or later.</summary>
        public static Faction PeopleFor(Map map, WorldAge age) => Peoples(map).Where(x => Ages.Of(x.f) >= age).Select(x => x.f).FirstOrDefault();

        public static bool OnAnyMap => Book?.rounds.Any(r => r.lord != null) ?? false;

        // ---------------- The hour ----------------

        public static void Hourly(int now)
        {
            var book = Book;
            if (book == null || !PostBook.On) return;
            foreach (var map in Find.Maps.Where(m => m != null && m.IsPlayerHome).ToList())
            {
                try
                {
                    Hour(book, map, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("post: the post's hour", e);
                }
            }
        }

        private static void Hour(PostBook book, Map map, int now)
        {
            var age = AgeOf(map);
            if (!PostMath.HasPost(age)) return;
            var r = book.RoundOf(map);
            if (r.lord != null && !map.lordManager.lords.Contains(r.lord)) r.lord = null;
            if (r.next < 0) r.next = now + Mathf.RoundToInt(PostMath.NextPostDays(age, PostBook.Pace, Rand.Value) * GenDate.TicksPerDay * 0.5f);
            if (now < r.next || r.lord != null) return;
            if (!PostMath.ByDay(GenLocalDate.HourOfDay(map)) || !CourierTracker.Safe(map)) return;
            if (SetOut(map, r, age))
                r.next = now + Mathf.RoundToInt(PostMath.NextPostDays(age, PostBook.Pace, Rand.Value) * GenDate.TicksPerDay);
            else r.next = now + GenDate.TicksPerDay / 2;
        }

        // ---------------- Setting out ----------------

        /// <summary>The post comes now (also the dev action): the rider and their mount ride in from a road at the edge, or drop by pod in the spacer age.</summary>
        public static bool SetOut(Map map, PostRound r, WorldAge age)
        {
            var f = PeopleFor(map, age);
            if (f == null || map == null) return false;
            var spot = Outbox.Spot(map);
            if (!spot.IsValid) return false;
            var kind = Couriers.PersonKind(f);
            if (kind == null) return false;
            Pawn rider = null, mount = null;
            try
            {
                rider = Arrivals.Make(kind, f);
                if (rider == null) return false;
                rider.inventory?.DestroyAll();
                var mountKind = age == WorldAge.Spacer ? null : MountKind(f, map);
                if (mountKind != null)
                {
                    mount = PawnGenerator.GeneratePawn(new PawnGenerationRequest(mountKind, f, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
                    mount?.inventory?.DestroyAll();
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("post: the rider", e);
            }
            if (rider == null) return false;
            var party = new List<Pawn> { rider };
            if (mount != null) party.Add(mount);
            if (age == WorldAge.Spacer)
            {
                DropPodUtility.DropThingsNear(spot, map, party.Cast<Thing>(), 110, false, false, false, false, true, f);
            }
            else
            {
                var tp = TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false);
                if (!RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Always, false, c => map.reachability.CanReach(c, spot, PathEndMode.OnCell, tp)))
                {
                    foreach (var p in party) Arrivals.Discard(p);
                    return false;
                }
                foreach (var p in party) GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(entry, map, 2), map);
            }
            try
            {
                r.lord = LordMaker.MakeNewLord(f, new LordJob_PostRider(spot), map, party);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("post: the rider's group", e);
                Arrivals.SendAway(party);
                return false;
            }
            r.people = f;
            r.arrived = -1;
            r.brought = r.took = false;
            r.doneAt = -1;
            r.last = Now;
            if (mount != null && rider.Spawned) Ride(rider, mount);
            // apart: the post on its way up the road, told as it comes.
            Notify.Message(PostWords.PostComing(age, f), new LookTargets(rider), MessageTypeDefOf.NeutralEvent, Importance.Notable);
            return true;
        }

        /// <summary>
        /// A mount for the rider: a beast of their people's caravans big enough to ride (body size 1.2 or more, Giddy-Up's
        /// own default) that the land and season allow, else a horse where the season allows; null: on foot.
        /// </summary>
        public static PawnKindDef MountKind(Faction f, Map map)
        {
            try
            {
                var biome = map.Biome;
                var opts = f?.def?.pawnGroupMakers?.Where(g => g?.carriers != null).SelectMany(g => g.carriers)
                    .Where(o => o?.kind?.race?.race != null && o.kind.RaceProps.Animal && o.kind.RaceProps.baseBodySize >= OwnRiders.MinMountSize && !o.kind.RaceProps.predator
                                && (biome == null || biome.IsPackAnimalAllowed(o.kind.race)) && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(o.kind.race, 0f))
                    .ToList();
                if (opts != null && opts.Count > 0) return opts.RandomElementByWeight(o => Mathf.Max(0.01f, o.selectionWeight)).kind;
                var horse = DefDatabase<PawnKindDef>.GetNamedSilentFail("Horse");
                if (horse?.race != null && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(horse.race, 0f)) return horse;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("post: a mount", e);
            }
            return null;
        }

        /// <summary>With Giddy-Up 2, the rider sits on the mount as they come (Integrations/GiddyUpMounts.cs); without it, they lead the animal.</summary>
        public static void Ride(Pawn rider, Pawn mount) => GiddyUpMounts.MountUp(rider, mount);

        // ---------------- At the gate ----------------

        /// <summary>
        /// Every 250 ticks while the rider is on the map: at the mailbag, they set down the mail for the colony, take what goes
        /// on the post, and ride on a little after (an hour at most once their business is done).
        /// </summary>
        public static void Rare(Map map, int now)
        {
            var book = Book;
            var r = book?.RoundOf(map, false);
            if (r?.lord == null) return;
            var lord = r.lord;
            if (!map.lordManager.lords.Contains(lord))
            {
                r.lord = null;
                return;
            }
            if (!(lord.CurLordToil is LordToil_PostWork)) return;
            var rider = lord.ownedPawns.FirstOrDefault(MailWatch.Able);
            if (rider == null) return;
            if (r.arrived < 0)
            {
                r.arrived = now;
                PostWords.Say(rider, "rider", null);
            }
            if (rider.CurJobDef == PostDefOf.Rimfolk_PostDrop || rider.CurJobDef == PostDefOf.Rimfolk_PostTake) return;
            var c = Carrier(map, r, rider);
            if (!r.brought)
            {
                r.brought = true;
                var items = MailIn.For(c, false, 0f);
                if (items.Count > 0 && MailWatch.Bring(map, new List<Pawn> { rider }, items)) return;
            }
            if (!r.took)
            {
                r.took = true;
                var letters = Outbox.For(c);
                if (letters.Count > 0)
                {
                    PostTake(map, rider, letters, c);
                    return;
                }
            }
            if (r.doneAt < 0) r.doneAt = now;
            if (now - r.doneAt > GenDate.TicksPerHour / 2)
            {
                PostWords.Say(rider, "rideon", null);
                lord.ReceiveMemo(LordJob_PostRider.MemoLeave);
            }
        }

        private static MailCarrier Carrier(Map map, PostRound r, Pawn rider) =>
            new MailCarrier
            {
                kind = PostCarrier.PostRider, people = r.people, map = map, means = PostMath.MeansOf(AgeOf(map), r.lord.ownedPawns.Any(x => x.RaceProps.Animal)),
                name = PostWords.CarrierName(PostCarrier.PostRider, r.people, rider)
            };

        private static void PostTake(Map map, Pawn rider, List<NeighbourLetter> letters, MailCarrier c) => MailWatch.FetchFor(map, rider, letters, c);

        /// <summary>The post's group (from the dev action): it comes now, whatever the day.</summary>
        public static bool ComeNow(Map map)
        {
            var book = Book;
            if (book == null || map == null) return false;
            var r = book.RoundOf(map);
            if (r.lord != null && map.lordManager.lords.Contains(r.lord)) return false;
            var age = AgeOf(map);
            if (!PostMath.HasPost(age)) age = WorldAge.Medieval;
            return SetOut(map, r, age);
        }
    }

    /// <summary>
    /// The post rider's group: they ride in to the mailbag, do their business there (Post/PostRider.cs gives the jobs: the
    /// mail set down, the letters taken), and ride on. Hurt by the colony or their people turned hostile, they leave at once,
    /// defending themselves; four hours at the gate at most.
    /// </summary>
    public class LordJob_PostRider : LordJob
    {
        public IntVec3 spot;

        public const string MemoLeave = "PostRideOn";

        public LordJob_PostRider()
        {
        }

        public LordJob_PostRider(IntVec3 spot)
        {
            this.spot = spot;
        }

        public override bool AddFleeToil => false;

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var travel = new LordToil_TravelOnErrand(spot);
            graph.StartingToil = travel;
            var work = new LordToil_PostWork(spot);
            graph.AddToil(work);
            var leave = new LordToil_ExitMap(Pace.Errand, false, true);
            graph.AddToil(leave);
            var flee = new LordToil_ExitMapAndDefendSelf();
            graph.AddToil(flee);
            var arrived = new Transition(travel, work);
            arrived.AddTrigger(new Trigger_Memo("TravelArrived"));
            arrived.AddTrigger(new Trigger_TicksPassed(GenDate.TicksPerHour * 5));
            graph.AddTransition(arrived);
            var goes = new Transition(work, leave);
            goes.AddSource(travel);
            goes.AddTrigger(new Trigger_Memo(MemoLeave));
            graph.AddTransition(goes);
            var late = new Transition(work, leave);
            late.AddTrigger(new Trigger_TicksPassed(GenDate.TicksPerHour * 4));
            graph.AddTransition(late);
            var hurt = new Transition(travel, flee);
            hurt.AddSources(work, leave);
            hurt.AddTrigger(new Trigger_PawnHarmed(1f, true, Faction.OfPlayer, null, null));
            hurt.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(hurt);
            var war = new Transition(travel, flee);
            war.AddSources(work, leave);
            war.AddTrigger(new Trigger_BecamePlayerEnemy());
            war.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(war);
            return graph;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref spot, "spot");
        }
    }

    /// <summary>At the mailbag: they keep close to it (the mount too) while Post/PostRider.cs gives them their jobs.</summary>
    public class LordToil_PostWork : LordToil
    {
        private readonly IntVec3 spot;

        public LordToil_PostWork(IntVec3 spot)
        {
            this.spot = spot;
        }

        public override IntVec3 FlagLoc => spot;

        public override bool AllowSatisfyLongNeeds => false;

        public override void UpdateAllDuties()
        {
            foreach (var p in lord.ownedPawns) p.mindState.duty = new PawnDuty(DutyDefOf.WanderClose, spot, 4f);
        }
    }
}
