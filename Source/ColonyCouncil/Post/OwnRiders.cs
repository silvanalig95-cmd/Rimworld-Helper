using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's own rider (docs/design/post-and-letters.md, step 1, point 4): from the letter's dialog or the mailbag,
    /// "send it with our own rider", the supplies for the road chosen with it (Post/RiderPacks.cs). A chosen colonist takes
    /// the letter from the mailbag, gathers what was packed (with the colony animal big enough to ride, if there is one) the
    /// way the colony's caravans gather theirs, and sets out as a caravan of their own on the game's roads, straight to the
    /// town however far (the range doesn't bind a rider; the road's length does); there they hand it over, wait for the
    /// answer (a few hours), and bring it home, where it opens and what is left of the supplies goes back into the stores.
    /// They eat from their pack on the road and at the town, by the game's own caravan rules; going short, the game's own
    /// hunger. The away time is real, and the road's dangers are the game's own caravan events (nothing rolled here). As
    /// the colony's envoy abroad (Diplomacy/EnvoysAbroad.cs).
    /// </summary>
    public static class OwnRiders
    {
        private static int Now => Find.TickManager.TicksGame;

        private static PostBook Book => PostBook.Instance;

        /// <summary>Big enough to ride: Giddy-Up 2's own default size.</summary>
        public const float MinMountSize = 1.2f;

        /// <summary>An animal someone could ride: grown, big enough, up and able to walk.</summary>
        public static bool Ridable(Pawn a) =>
            a != null && !a.Dead && !a.Downed && a.RaceProps != null && a.RaceProps.Animal && a.RaceProps.baseBodySize >= MinMountSize
            && (a.ageTracker?.Adult ?? false) && (a.health?.capacities?.CapableOf(PawnCapacityDefOf.Moving) ?? false);

        private static readonly Dictionary<int, (Pawn mount, float at)> mounts = new Dictionary<int, (Pawn, float)>();

        /// <summary>The colony's best animal to ride at a home (the fastest; not one already out with a rider), or null; looked at every few seconds.</summary>
        public static Pawn MountFor(Map home, Pawn rider)
        {
            if (home == null) return null;
            float t = Time.realtimeSinceStartup;
            if (rider == null && mounts.TryGetValue(home.uniqueID, out var m) && t - m.at < 5f && t >= m.at && (m.mount == null || m.mount.Spawned)) return m.mount;
            Pawn best = null;
            try
            {
                best = home.mapPawns.SpawnedColonyAnimals.Where(a => Ridable(a) && a.GetLord() == null && !a.roping.IsRoped && (Book?.rides.All(r => r.mount != a) ?? true)
                                                                       && (rider == null || rider.CanReach(a, PathEndMode.Touch, Danger.Some)))
                    .OrderByDescending(a => a.GetStatValue(StatDefOf.MoveSpeed)).FirstOrDefault();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("post: a mount of the colony's", e);
            }
            if (rider == null) mounts[home.uniqueID] = (best, t);
            return best;
        }

        /// <summary>Who could ride out: grown colonists at the home, free to go, able to walk; the best with animals first.</summary>
        public static List<Pawn> Candidates(Map home)
        {
            if (home == null) return new List<Pawn>();
            return home.mapPawns.FreeColonistsSpawned.Where(p => p != null && !p.Dead && !p.Downed && !p.InMentalState && !p.Drafted && !p.IsQuestLodger()
                                                                 && !p.DevelopmentalStage.Juvenile() && (p.health?.capacities?.CapableOf(PawnCapacityDefOf.Moving) ?? false)
                                                                 && Book?.RideOf(p) == null && !EnvoysAbroad.Holds(p))
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0).ToList();
        }

        /// <summary>Why the colony's own rider can't take a letter to this notable's town now, or null.</summary>
        public static string WhyNot(Notable n, Map home)
        {
            if (!PostBook.On) return PostWords.P("CC_Mail_Why_Off");
            var town = n != null ? NotableBook.TownOf(n.town) : null;
            if (home == null || town == null) return PostWords.P("CC_Mail_Why_NoTown");
            if (NotableBook.Hostile(n)) return PostWords.P("CC_Mail_Why_RiderWar", (n.faction?.Name ?? "").Named("PEOPLE"));
            if (!Find.WorldReachability.CanReach(home.Tile, town.Tile)) return PostWords.P("CC_Mail_Why_NoRoad", town.Name.Named("TOWN"));
            if (home.mapPawns.FreeColonistsSpawnedCount < 2) return PostWords.P("CC_Mail_Why_Alone");
            if (Candidates(home).Count == 0) return PostWords.P("CC_Mail_Why_NoRider");
            return null;
        }

        /// <summary>
        /// The letter goes with the colony's own rider and the supplies chosen for them (the letter's window sets them for the
        /// letter it sends: RiderPacks.ForNext): when it is in the outbox they set out (at once, if it is already there; one
        /// still in its writer's hand on the way to the mailbag, as it goes in: Post/Outbox.cs).
        /// </summary>
        public static void Send(NeighbourLetter l, Pawn rider, RiderPack pack = null)
        {
            if (l == null || rider == null) return;
            pack ??= RiderPacks.TakeNext();
            l.ownRider = true;
            l.rider = rider;
            l.carrier = PostCarrier.OwnRider;
            if (pack != null) RiderPacks.Keep(l.id, pack);
            NotableBook.Changed();
            if (l.stage == LetterStage.Outbox && !l.bringing) SetOut(l);
        }

        /// <summary>The rider fetches the letter, then gathers the supplies with the mount following (Gather), and sets out.</summary>
        public static bool SetOut(NeighbourLetter l)
        {
            var book = Book;
            var p = l?.rider;
            var map = l?.map;
            var town = Outbox.TownOf(l, out _);
            if (book == null || p == null || map == null || town == null || !p.Spawned || p.Map != map) return Fail(l);
            if (!RCellFinder.TryFindBestExitSpot(p, out var edge, TraverseMode.ByPawn)) return Fail(l);
            var spot = Outbox.Spot(map);
            if (!spot.IsValid) spot = p.Position;
            var mount = MountFor(map, p);
            var ride = new OwnRide
            {
                letterId = l.id, rider = p, mount = mount, home = map, town = town.ID, townName = town.Name, riderName = p.LabelShort, stage = RideStage.SettingOut, since = Now
            };
            book.rides.Add(ride);
            // Sent without choosing (an older save, a letter already on its way to the bag): the journey planner's supplies.
            if (RiderPacks.For(l.id) == null) RiderPacks.Keep(l.id, RiderPacks.DefaultFor(map, p, mount, town));
            l.packedBy = p;
            var job = JobMaker.MakeJob(PostDefOf.Rimfolk_RiderSetOut, spot, edge);
            job.locomotionUrgency = Pace.Errand;
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            if (mount != null)
            {
                try
                {
                    var follow = JobMaker.MakeJob(JobDefOf.FollowClose, p);
                    follow.followRadius = 3f;
                    follow.expiryInterval = GenDate.TicksPerHour * 2;
                    mount.jobs.StartJob(follow, JobCondition.InterruptForced, null, false, true);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("post: the mount follows", e);
                }
            }
            NotableBook.Changed();
            // apart: the rider gets ready on the map; the message looks at them.
            Notify.Message(PostWords.P(mount != null ? "CC_Mail_Msg_RiderReady" : "CC_Mail_Msg_RiderReadyFoot", p.Named("PAWN"), town.Name.Named("TOWN"),
                (mount?.LabelShort ?? "").Named("MOUNT")), new LookTargets(p), MessageTypeDefOf.NeutralEvent, Importance.Notable);
            return true;
        }

        private static bool Fail(NeighbourLetter l)
        {
            if (l != null)
            {
                RiderPacks.Drop(l.id);
                l.ownRider = false;
                l.rider = null;
                l.packedBy = null;
                if (l.carrier == PostCarrier.OwnRider) l.carrier = PostCarrier.None;
            }
            return false;
        }

        /// <summary>At the mailbag (from the job): the letter is in the rider's hands.</summary>
        public static void TookLetter(Pawn p)
        {
            var ride = Book?.RideOf(p);
            var l = ride != null ? NeighbourLetters.Get(ride.letterId) : null;
            if (l == null || l.stage != LetterStage.Outbox) return;
            l.stage = LetterStage.Riding;
            l.stageTick = Now;
            l.packedBy = null;
            NotableBook.Changed();
            PostWords.Say(p, "setout", l);
        }

        /// <summary>At the map's edge (from the job): a caravan of their own (with their mount if it kept up), on the road to the town.</summary>
        public static void AtEdge(Pawn p)
        {
            var ride = Book?.RideOf(p);
            var map = ride?.home;
            var town = ride != null ? NotableBook.TownOf(ride.town) : null;
            if (ride == null || map == null || town == null || !p.Spawned) return;
            var pawns = new List<Pawn> { p };
            var m = ride.mount;
            if (m != null && m.Spawned && m.Map == p.Map && !m.Downed && m.Position.InHorDistOf(p.Position, 15f)) pawns.Add(m);
            else ride.mount = null;
            try
            {
                var c = CaravanExitMapUtility.ExitMapAndCreateCaravan(pawns, Faction.OfPlayer, map.Tile, map.Tile, PlanetTile.Invalid, false);
                c?.pather?.StartPath(town.Tile, null, true);
                ride.stage = RideStage.Out;
                ride.since = Now;
                NotableBook.Changed();
                OutMessage(ride, p, c);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("post: the rider's caravan", e);
            }
        }

        // ---------------- The supplies gathered ----------------

        /// <summary>Getting ready in the game's own caravan forming (gathering the supplies, the beast, waiting to go).</summary>
        public static bool Forming(Pawn p) => p?.GetLord()?.LordJob is LordJob_FormAndSendCaravan;

        /// <summary>
        /// With the letter in hand (from the job): the supplies packed for them are gathered the way the colony's caravans
        /// gather theirs (Journeys/JourneyRun.cs, Depart): the game's own caravan forming, with the rider and their beast, the
        /// packed lines not yet in their packs as its goods, gathered at the gate and out by the edge toward the town, where
        /// the game makes their caravan and sends it on its way (Giddy-Up puts riders in the saddle as it heads out). False
        /// when it can't (no way out): they set out as before.
        /// </summary>
        public static bool Gather(Pawn p)
        {
            var book = Book;
            var ride = book?.RideOf(p);
            var l = ride != null ? NeighbourLetters.Get(ride.letterId) : null;
            var map = p?.Map;
            var town = ride != null ? NotableBook.TownOf(ride.town) : null;
            if (ride == null || l == null || map == null || town == null || !p.Spawned || p.Downed) return false;
            var mount = ride.mount;
            if (mount != null && (!mount.Spawned || mount.Map != map || mount.Dead || mount.Downed || mount.InMentalState || mount.GetLord() != null)) mount = ride.mount = null;
            var pack = RiderPacks.For(l.id);
            // The beast's feed only with the beast.
            var plan = RiderPacks.Shadow(map, p, mount, town, pack?.lines.Where(x => mount != null || x.kind != "feed").ToList());
            plan.exit = JourneyPack.Exit(plan);
            if (!plan.exit.IsValid) return false;
            plan.gate = JourneyPack.Gate(plan, plan.exit);
            if (!plan.gate.IsValid) plan.gate = p.Position;
            var start = JourneyPack.StartTile(plan);
            if (!start.Valid) return false;
            var going = new List<Pawn> { p };
            if (mount != null) going.Add(mount);
            var shortOf = new List<string>();
            var goods = pack != null ? JourneyPack.Missing(plan, going, shortOf) : new List<TransferableOneWay>();
            CaravanFormingUtility.StartFormingCaravan(going, new List<Pawn>(), Faction.OfPlayer, goods, plan.gate, plan.exit, start, town.Tile);
            if (!Forming(p)) return false;
            NotableBook.Changed();
            // apart: the rider gathering on the map; the message looks at them.
            if (shortOf.Count > 0)
                Notify.Message(PostWords.P("CC_Mail_Msg_RiderStoresShort", p.Named("PAWN"), town.Name.Named("TOWN"), TextUtil.JoinAnd(shortOf).Named("WHAT")), new LookTargets(p),
                    MessageTypeDefOf.CautionInput, Importance.Notable);
            return true;
        }

        /// <summary>Out on the world map (the game's caravan forming sent them): on the road to the town.</summary>
        private static void Departed(OwnRide ride, Pawn p, Caravan c, int now)
        {
            ride.stage = RideStage.Out;
            ride.since = now;
            if (ride.mount != null && !c.PawnsListForReading.Contains(ride.mount)) ride.mount = null;
            var town = NotableBook.TownOf(ride.town);
            if (town != null && !c.pather.Moving) c.pather.StartPath(town.Tile, null, true);
            NotableBook.Changed();
            OutMessage(ride, p, c);
        }

        /// <summary>
        /// As they set out: with the letter and food for so many days against the road there and back (the game's own days of
        /// food in their caravan, the road by its own estimate), or short of it, or with none: then they go hungry on the way.
        /// </summary>
        private static void OutMessage(OwnRide ride, Pawn p, Caravan c)
        {
            float food = -1f, trip = RiderPacks.For(ride.letterId)?.tripDays ?? 0f;
            try
            {
                if (c != null)
                {
                    food = c.DaysWorthOfFood.days;
                    int ticks = c.pather.Moving ? CaravanArrivalTimeEstimator.EstimatedTicksToArrive(c, false) : -1;
                    if (ticks > 0) trip = RiderPackMath.TripDays(ticks / (float)GenDate.TicksPerDay);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("post: the rider's food on the road", e);
            }
            // Nobody in the caravan needs food (the game counts it as endless), or it can't be read: said plainly.
            string key = food < 0f || food >= 500f ? "CC_Mail_Msg_RiderOut" : RiderPackMath.OutKey(food, trip);
            bool hungry = key == "CC_Mail_Msg_RiderOutShort" || key == "CC_Mail_Msg_RiderOutNoFood";
            // apart: the rider's caravan is out on the world map; the message looks at it.
            Notify.Message(PostWords.P(key, p.Named("PAWN"), (ride.townName ?? "?").Named("TOWN"), RiderPackMath.Days(food).Named("FOOD"), RiderPackMath.Days(trip).Named("ROAD")),
                c != null ? new LookTargets(c) : LookTargets.Invalid, hungry ? MessageTypeDefOf.CautionInput : MessageTypeDefOf.NeutralEvent, Importance.Notable);
        }

        // ---------------- Every few seconds ----------------

        /// <summary>A rider getting ready, or one just home: watched on the post's own 250-tick pass (Post/MailWatch.cs).</summary>
        public static bool Watching => Book?.rides.Any(Watched) ?? false;

        private static bool Watched(OwnRide r) => r != null && (r.stage == RideStage.SettingOut || r.stage == RideStage.Back && (r.rider?.Spawned ?? false));

        /// <summary>Their setting out and their homecoming told as they happen, not on the next hour.</summary>
        public static void Rare(int now)
        {
            var book = Book;
            if (book == null || book.rides.Count == 0) return;
            foreach (var ride in book.rides.Where(Watched).ToList())
            {
                try
                {
                    Step(book, ride, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("post: a rider's watch", e);
                }
            }
        }

        // ---------------- The hour ----------------

        public static void Hourly(int now)
        {
            var book = Book;
            if (book == null) return;
            RiderPacks.Prune();
            if (book.rides.Count == 0) return;
            foreach (var ride in book.rides.ToList())
            {
                try
                {
                    Step(book, ride, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("post: a rider's hour", e);
                }
            }
        }

        private static void Step(PostBook book, OwnRide ride, int now)
        {
            var p = ride.rider;
            var l = NeighbourLetters.Get(ride.letterId);
            var town = NotableBook.TownOf(ride.town);
            if (p == null || p.Dead || p.Discarded || People.Kidnapped(p))
            {
                End(book, ride, l, "CC_Mail_Msg_RiderLost");
                return;
            }
            switch (ride.stage)
            {
                case RideStage.SettingOut:
                {
                    // Out on the world map: the game's caravan forming sent them.
                    var c = p.GetCaravan();
                    if (c != null)
                    {
                        Departed(ride, p, c, now);
                        return;
                    }
                    // Called to other work before they left, or the gathering called off: the letter waits in the outbox again,
                    // and what they gathered goes back into the stores (the game's own unloading).
                    if (p.Spawned && p.CurJobDef != PostDefOf.Rimfolk_RiderSetOut && !Forming(p))
                    {
                        if (l != null && l.stage == LetterStage.Riding) l.stage = LetterStage.Outbox;
                        if (l != null) Fail(l);
                        else RiderPacks.Drop(ride.letterId);
                        book.rides.Remove(ride);
                        NotableBook.Changed();
                    }
                    return;
                }
                case RideStage.Out:
                {
                    var c = p.GetCaravan();
                    if (c == null)
                    {
                        // Home again without getting there (the player turned them back): the letter is back in the outbox.
                        if (p.Spawned && Homes.IsHome(p.Map))
                        {
                            if (l != null && l.stage == LetterStage.Riding)
                            {
                                l.stage = LetterStage.Outbox;
                                l.stageTick = now;
                                Fail(l);
                            }
                            book.rides.Remove(ride);
                            NotableBook.Changed();
                        }
                        return;
                    }
                    if (town == null || town.Destroyed || l == null || !l.Open)
                    {
                        Homeward(ride, c);
                        return;
                    }
                    bool there = c.Tile == town.Tile || !c.pather.Moving && Find.WorldGrid.ApproxDistanceInTiles(c.Tile, town.Tile) <= 1.01f;
                    if (there) Arrived(ride, l, c, now);
                    else if (!c.pather.Moving && !c.pather.Paused) c.pather.StartPath(town.Tile, null, true);
                    return;
                }
                case RideStage.AtTown:
                {
                    var c = p.GetCaravan();
                    if (c == null) return;
                    if (now >= ride.until) Homeward(ride, c);
                    return;
                }
                case RideStage.Back:
                {
                    if (p.Spawned && p.Map == ride.home)
                    {
                        Home(book, ride, l, p);
                        return;
                    }
                    var c = p.GetCaravan();
                    if (c != null && !c.pather.Moving && !c.pather.Paused && ride.home?.Parent != null)
                        c.pather.StartPath(ride.home.Tile, new CaravanArrivalAction_Enter(ride.home.Parent), true);
                    return;
                }
            }
        }

        /// <summary>At the town: they hand the letter over, it is read, and they wait a few hours for the answer.</summary>
        private static void Arrived(OwnRide ride, NeighbourLetter l, Caravan c, int now)
        {
            c.pather.StopDead();
            ride.stage = RideStage.AtTown;
            ride.until = now + Mathf.RoundToInt(RiderPackMath.TownWaitDays(Rand.Value) * GenDate.TicksPerDay);
            NeighbourLetters.ReadByRider(l, ride.rider);
            NotableBook.Changed();
        }

        /// <summary>The answer, carried home by the rider (from NeighbourLetters' reading): what it sends rides in their pack.</summary>
        public static void Answered(NeighbourLetter l, string label, string text, LetterDef def, string chronicle, List<Thing> things)
        {
            var ride = Book?.RideFor(l?.id ?? -1);
            if (ride == null) return;
            ride.answered = true;
            ride.answerLabel = label;
            ride.answerText = text;
            ride.answerDef = def;
            ride.chronicle = chronicle;
            var c = ride.rider?.GetCaravan();
            foreach (var t in things ?? new List<Thing>())
            {
                if (t == null || t.Destroyed) continue;
                if (c != null) c.AddPawnOrItem(t, true);
                else if (ride.rider?.inventory != null) ride.rider.inventory.innerContainer.TryAdd(t);
            }
        }

        /// <summary>On the road home, with the answer (or with none).</summary>
        private static void Homeward(OwnRide ride, Caravan c)
        {
            ride.stage = RideStage.Back;
            if (ride.home?.Parent != null) c.pather.StartPath(ride.home.Tile, new CaravanArrivalAction_Enter(ride.home.Parent), true);
            // apart: the rider's caravan on the world map, turning for home.
            Notify.Message(PostWords.P("CC_Mail_Msg_RiderHomeward", ride.rider.Named("PAWN"), (ride.townName ?? "?").Named("TOWN")), new LookTargets(c),
                MessageTypeDefOf.NeutralEvent, Importance.Notable);
        }

        /// <summary>Home with the answer: it opens now, the rider in its look; what it sent is in their pack, and what is left of the supplies goes back into the stores.</summary>
        private static void Home(PostBook book, OwnRide ride, NeighbourLetter l, Pawn p)
        {
            book.rides.Remove(ride);
            Unpack(ride, p);
            if (ride.answered && !ride.answerText.NullOrEmpty())
            {
                PostWords.Say(p, "home", l);
                Tell.Letter(NewsKind.World, Importance.Notable, ride.home, ride.answerLabel, ride.answerText + "\n\n" + PostWords.P("CC_Mail_RiderBrought", p.Named("PAWN")),
                    ride.answerDef ?? LetterDefOf.NeutralEvent, new LookTargets(p));
                if (!ride.chronicle.NullOrEmpty()) CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, ride.chronicle, p));
            }
            else if (l != null && l.Open && l.passed && l.stage == LetterStage.Travelling)
                // apart: the rider home; the one they gave it to took it on to the head, whose answer comes in the post (World/LetterRoutes.cs).
                Notify.LetterMessage("CC_Route_Msg_RiderHome".Translate(p.Named("PAWN"), (ride.townName ?? "?").Named("TOWN"),
                    (NotableBook.Instance?.Get(l.notable)?.Name ?? "?").Named("NAME"), (NotableBook.Instance?.Get(l.passTo)?.Name ?? "?").Named("HEAD")).Resolve(), l.id,
                    new LookTargets(p), MessageTypeDefOf.NeutralEvent, Importance.Notable);
            else if (l != null && l.Open)
                // apart: the rider home without an answer, told as they come in.
                Notify.Message(PostWords.P("CC_Mail_Msg_RiderNoAnswer", p.Named("PAWN"), (ride.townName ?? "?").Named("TOWN")), new LookTargets(p),
                    MessageTypeDefOf.NeutralEvent, Importance.Notable);
            if (l != null) NeighbourLetters.AnswerIn(l.id);
            NotableBook.Changed();
        }

        /// <summary>
        /// What is left of the supplies, in the rider's and the beast's packs as they come home, goes back into the stores:
        /// the game's own unloading of a caravan come home (set again here), said once.
        /// </summary>
        private static void Unpack(OwnRide ride, Pawn p)
        {
            var pack = RiderPacks.For(ride.letterId);
            RiderPacks.Drop(ride.letterId);
            if (pack == null || p == null || !p.Spawned) return;
            var mount = ride.mount != null && ride.mount.Spawned && ride.mount.Map == p.Map ? ride.mount : null;
            var left = RiderPacks.Left(pack, p, mount);
            if (left.Count == 0) return;
            if (p.inventory != null) p.inventory.UnloadEverything = true;
            if (mount?.inventory != null) mount.inventory.UnloadEverything = true;
            // apart: the rider home on the map; the message looks at them.
            Notify.Message(PostWords.P("CC_Mail_Msg_PackLeft", p.Named("PAWN"), TextUtil.JoinAnd(left).Named("LEFT")), new LookTargets(p), MessageTypeDefOf.NeutralEvent,
                Importance.Notable);
        }

        /// <summary>The rider lost on the road (dead, carried off): the letter, and any answer they carried, with them.</summary>
        private static void End(PostBook book, OwnRide ride, NeighbourLetter l, string why)
        {
            book.rides.Remove(ride);
            RiderPacks.Drop(ride.letterId);
            if (l != null && l.Open)
            {
                if (l.stage == LetterStage.Riding) NeighbourLetters.Cancel(l, null);
                else NeighbourLetters.AnswerIn(l.id);
                // apart: what became of the letter, told in a message.
                Notify.Message(PostWords.P(why, (ride.riderName ?? "?").Named("PAWN"), (ride.townName ?? "?").Named("TOWN")), LookTargets.Invalid,
                    MessageTypeDefOf.NegativeEvent, Importance.Important);
            }
            NotableBook.Changed();
        }
    }
}
