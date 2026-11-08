using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Anyone going that way takes them (docs/design/post-and-letters.md, step 1, points 2 and 5): every 250 ticks while a
    /// home has letters in its outbox or mail waiting, the outsiders on its map are looked at. A group (any lord of a people
    /// at peace with the colony: traders, visitors and Hospitality's guests, a camp, pilgrims, travellers passing, a people's
    /// messengers) seen for the first time brings the mail that comes from their way; one turning to leave (its lord's exit
    /// toil, an exit duty or a job that leaves the map: Outsiders.Leaving) takes the letters for places on its way, and one of
    /// them walks to the mailbag for them (Rimfolk_PostTake) before going on. A lone outsider leaving does the same. The
    /// colony's own caravan forming takes the letters its way (one of its people fetches them while the goods are gathered),
    /// and they are on the road once the caravan has set out. The post rider's own group is Post/PostRider.cs's.
    /// </summary>
    public static class MailWatch
    {
        private static int Now => Find.TickManager.TicksGame;

        private class Seen
        {
            public float entryHeading;
            public bool entryKnown;
            public int first;
            public bool took, brought;
        }

        /// <summary>Groups seen on a home map (by the lord's id) and lone leavers asked (by the pawn's id); not saved: a load starts afresh.</summary>
        private static readonly Dictionary<int, Seen> groups = new Dictionary<int, Seen>();
        private static readonly HashSet<int> lone = new HashSet<int>();
        /// <summary>Who is about to carry which way, for the job that fetches the letters (rebuilt from the pawn after a load).</summary>
        private static readonly Dictionary<int, MailCarrier> carriers = new Dictionary<int, MailCarrier>();

        public static void Reset()
        {
            groups.Clear();
            lone.Clear();
            carriers.Clear();
            formingAsked.Clear();
            PostReach.Reset();
        }

        /// <summary>Something to watch: a letter in an outbox, mail in, a letter packed by a caravan, or the post rider on a map.</summary>
        public static bool Wants => PostBook.On && (Outbox.AnyAnywhere || (PostBook.Instance?.incoming.Count ?? 0) > 0 || PostRider.OnAnyMap);

        public static void Rare(int now)
        {
            if (!PostBook.On) return;
            foreach (var map in Find.Maps)
            {
                if (map == null || !map.IsPlayerHome) continue;
                try
                {
                    Look(map, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("post: the watch", e);
                }
            }
            if (groups.Count > 200) groups.Clear();
            if (lone.Count > 200) lone.Clear();
        }

        private static void Look(Map map, int now)
        {
            PostRider.Rare(map, now);
            Packed(map);
            Unreserve(map);
            bool outbox = Outbox.Any(map), mail = MailIn.AnyDue(map);
            if (!outbox && !mail) return;
            bool calm = map.dangerWatcher.DangerRating == StoryDanger.None;
            foreach (var lord in map.lordManager.lords.ToList())
            {
                if (lord?.LordJob == null || lord.faction == null) continue;
                if (lord.LordJob is LordJob_PostRider) continue;
                if (lord.faction.IsPlayer)
                {
                    if (outbox && calm && lord.LordJob is LordJob_FormAndSendCaravan form) Forming(map, lord, form);
                    continue;
                }
                if (lord.faction.HostileTo(Faction.OfPlayer) || !PostMath.CarriesMail(lord.LordJob.GetType().Name)) continue;
                var people = lord.ownedPawns.Where(Able).ToList();
                if (people.Count == 0) continue;
                if (!groups.TryGetValue(lord.loadID, out var seen))
                {
                    seen = new Seen { first = now };
                    var p0 = people[0];
                    // Seen first near an edge: they came in from that way.
                    if (p0.Position.CloseToEdge(map, 20))
                    {
                        seen.entryKnown = true;
                        seen.entryHeading = Outbox.CellHeading(map, p0.Position);
                    }
                    groups[lord.loadID] = seen;
                }
                bool leaving = people.Any(Outsiders.Leaving);
                if (!leaving && !seen.brought && mail && calm && now - seen.first < GenDate.TicksPerHour * 8)
                {
                    var items = MailIn.For(CarrierOf(lord, people, map), seen.entryKnown, seen.entryHeading);
                    if (items.Count > 0 && Bring(map, people, items)) seen.brought = true;
                }
                if (leaving && !seen.took && outbox && calm)
                {
                    seen.took = true;
                    var c = CarrierOf(lord, people, map);
                    var heading = ExitHeading(people[0]);
                    c.knowEdge = true;
                    c.edgeHeading = heading;
                    var letters = Outbox.For(c);
                    if (letters.Count > 0) Fetch(map, people, letters, c);
                }
            }
            // A lone outsider walking off the map.
            if (outbox && calm)
                foreach (var p in map.mapPawns.AllHumanlikeSpawned.ToList())
                {
                    if (p.GetLord() != null || !Able(p) || p.Faction == null || p.Faction.IsPlayer || p.HostileTo(Faction.OfPlayer)) continue;
                    if (!(p.CurJob?.exitMapOnArrival ?? false) || !lone.Add(p.thingIDNumber)) continue;
                    var c = new MailCarrier
                    {
                        kind = PostCarrier.Travellers, people = p.Faction, map = map, knowEdge = true, edgeHeading = ExitHeading(p),
                        means = PostMath.MeansOf(Ages.Of(p.Faction), false), name = PostWords.CarrierName(PostCarrier.Travellers, p.Faction, p)
                    };
                    var letters = Outbox.For(c);
                    if (letters.Count > 0) Fetch(map, new List<Pawn> { p }, letters, c);
                }
        }

        /// <summary>Up, a person, at peace with the colony, free to walk over (not held, not in a mental state, not carried).</summary>
        public static bool Able(Pawn p) =>
            p != null && p.Spawned && !p.Dead && !p.Downed && p.RaceProps?.Humanlike == true && !p.IsPrisoner && !p.IsSlave && !p.InMentalState
            && !p.DevelopmentalStage.Juvenile() && !p.HostileTo(Faction.OfPlayer) && p.Faction != null && !p.Faction.IsPlayer;

        /// <summary>The way they leave: the cell their job takes them off the map at, else from where they stand.</summary>
        private static float ExitHeading(Pawn p)
        {
            var map = p.Map;
            var job = p.CurJob;
            if (job != null && job.exitMapOnArrival && job.targetA.IsValid) return Outbox.CellHeading(map, job.targetA.Cell);
            if (RCellFinder.TryFindBestExitSpot(p, out var spot, TraverseMode.ByPawn, false)) return Outbox.CellHeading(map, spot);
            return Outbox.CellHeading(map, p.Position);
        }

        /// <summary>Who a group is and how they travel, for the letters' way and words.</summary>
        private static MailCarrier CarrierOf(Lord lord, List<Pawn> people, Map map)
        {
            var kind = lord.LordJob is LordJob_Courier ? PostCarrier.Messengers : PostMath.CarrierOf(lord.LordJob.GetType().Name);
            // A trade caravan walks beside its pack beasts; anyone else with a beast to ride rides it.
            bool mounted = kind != PostCarrier.Traders && lord.ownedPawns.Any(x => x != null && x.RaceProps.Animal && OwnRiders.Ridable(x));
            return new MailCarrier
            {
                kind = kind, people = lord.faction, map = map, means = PostMath.MeansOf(Ages.Of(lord.faction), mounted),
                name = PostWords.CarrierName(kind, lord.faction, people.FirstOrDefault())
            };
        }

        // ---------------- Taking the letters ----------------

        /// <summary>One of them (not their trader, the nearest the mailbag) walks over for the letters going their way, and the letters are kept for them.</summary>
        private static void Fetch(Map map, List<Pawn> people, List<NeighbourLetter> letters, MailCarrier c)
        {
            var spot = Outbox.Spot(map);
            if (!spot.IsValid) return;
            var who = people.Where(p => p.TraderKind == null && p.CanReach(spot, PathEndMode.Touch, Danger.Some))
                          .OrderBy(p => p.Position.DistanceToSquared(spot)).FirstOrDefault()
                      ?? people.Where(p => p.CanReach(spot, PathEndMode.Touch, Danger.Some)).OrderBy(p => p.Position.DistanceToSquared(spot)).FirstOrDefault();
            if (who == null) return;
            foreach (var l in letters) l.packedBy = who;
            carriers[who.thingIDNumber] = c;
            var job = JobMaker.MakeJob(PostDefOf.Rimfolk_PostTake, spot);
            job.locomotionUrgency = LocomotionUrgency.Walk;
            job.expiryInterval = GenDate.TicksPerHour * 3;
            job.checkOverrideOnExpire = true;
            who.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            NotableBook.Changed();
        }

        /// <summary>One carrier (the post rider) fetches these letters.</summary>
        public static void FetchFor(Map map, Pawn who, List<NeighbourLetter> letters, MailCarrier c) => Fetch(map, new List<Pawn> { who }, letters, c);

        /// <summary>The carrier is at the mailbag (from the job): the written letters kept for them go with them; spoken ones wait for a colonist's words.</summary>
        public static void Took(Pawn p)
        {
            var book = NotableBook.Instance;
            if (book == null || p == null) return;
            var c = CarrierFor(p);
            var mine = book.letters.Where(l => l.stage == LetterStage.Outbox && l.packedBy == p).ToList();
            if (mine.Count == 0) return;
            if (c.kind == PostCarrier.OwnCaravan)
            {
                // The colony's own carry spoken words in their heads.
                foreach (var l in mine) l.packed = true;
                // apart: one of the caravan takes the letters at the mailbag; told as they do.
                Notify.Message("CC_Mail_Msg_Packed".Translate(p.Named("PAWN"), mine.Count.Named("COUNT")).Resolve(), new LookTargets(p), MessageTypeDefOf.NeutralEvent,
                    Importance.Notable);
                NotableBook.Changed();
                return;
            }
            foreach (var l in mine.Where(l => !l.spoken)) Outbox.Carried(l, c, p);
            PostWords.Say(p, "take", null);
            NotableBook.Changed();
        }

        /// <summary>The letters a carrier at the mailbag still waits for a colonist's spoken words for.</summary>
        public static NeighbourLetter SpokenFor(Pawn carrier) =>
            NotableBook.Instance?.letters.FirstOrDefault(l => l.stage == LetterStage.Outbox && l.spoken && l.packedBy == carrier);

        /// <summary>
        /// A carrier waits at the mailbag for a spoken message (from the job, every so often): the one who has the words, or
        /// anyone free who can talk, walks over and says them.
        /// </summary>
        public static void CallSpeaker(Pawn carrier)
        {
            var l = SpokenFor(carrier);
            var map = carrier?.Map;
            if (l == null || map == null) return;
            if (map.mapPawns.FreeColonistsSpawned.Any(c => c.CurJobDef == PostDefOf.Rimfolk_PostSpeak && c.CurJob.targetA.Thing == carrier)) return;
            bool Free(Pawn c) => c.Spawned && c.Map == map && Together.Able(c) && PlayOuts.CanJoin(c) && PlayOuts.Interruptible(c) && !c.Drafted
                                 && c.CanReach(carrier, PathEndMode.Touch, Danger.Some);
            var by = l.writer != null && Free(l.writer) ? l.writer : NeighbourLetters.Writers(map).Where(Free).OrderBy(c => c.Position.DistanceToSquared(carrier.Position)).FirstOrDefault();
            if (by == null) return;
            var job = JobMaker.MakeJob(PostDefOf.Rimfolk_PostSpeak, carrier);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            by.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>The colonist said the words to the carrier (from the job, the two face to face): the message goes with them.</summary>
        public static void Spoke(Pawn by, Pawn carrier)
        {
            var l = SpokenFor(carrier);
            if (l == null) return;
            PostWords.Say(carrier, "heard", null);
            var c = CarrierFor(carrier);
            if (c.kind == PostCarrier.OwnCaravan) l.packed = true;
            else Outbox.Carried(l, c, carrier);
        }

        /// <summary>The carrier gave up waiting (from the job): the spoken message stays for the next.</summary>
        public static void Waited(Pawn carrier)
        {
            var book = NotableBook.Instance;
            if (book == null) return;
            foreach (var l in book.letters.Where(l => l.stage == LetterStage.Outbox && l.packedBy == carrier && !l.packed)) l.packedBy = null;
            NotableBook.Changed();
        }

        /// <summary>The carrier this pawn is (kept as the job was given; after a load, worked out again from them).</summary>
        public static MailCarrier CarrierFor(Pawn p)
        {
            if (p != null && carriers.TryGetValue(p.thingIDNumber, out var c)) return c;
            var map = p?.Map;
            if (p?.Faction != null && p.Faction.IsPlayer)
                return new MailCarrier { kind = PostCarrier.OwnCaravan, map = map, name = p.LabelShort, means = PostMath.MeansOf(Ages.Ours, false) };
            var lord = p?.GetLord();
            if (lord?.LordJob != null && map != null) return CarrierOf(lord, new List<Pawn> { p }, map);
            return new MailCarrier
            {
                kind = PostCarrier.Travellers, people = p?.Faction, map = map, means = PostMath.MeansOf(Ages.Of(p?.Faction), false),
                name = PostWords.CarrierName(PostCarrier.Travellers, p?.Faction, p)
            };
        }

        /// <summary>Letters kept for a carrier who isn't fetching them (called away, gone): back to waiting.</summary>
        private static void Unreserve(Map map)
        {
            var book = NotableBook.Instance;
            if (book == null) return;
            foreach (var l in book.letters)
            {
                if (l.stage != LetterStage.Outbox || l.map != map || l.packedBy == null || l.packed) continue;
                var p = l.packedBy;
                if (p.Spawned && p.Map == map && !p.Dead && (p.CurJobDef == PostDefOf.Rimfolk_PostTake || p.CurJobDef == PostDefOf.Rimfolk_RiderSetOut)) continue;
                l.packedBy = null;
                NotableBook.Changed();
            }
        }

        // ---------------- Bringing the mail ----------------

        /// <summary>One of a group coming in walks the mail to the mailbag (Rimfolk_PostDrop), and it opens when they set it down.</summary>
        public static bool Bring(Map map, List<Pawn> people, List<PostItem> items)
        {
            var spot = Outbox.Spot(map);
            if (!spot.IsValid) return false;
            var who = people.Where(p => p.TraderKind == null && !Outsiders.Leaving(p) && p.CanReach(spot, PathEndMode.Touch, Danger.Some))
                .OrderBy(p => p.Position.DistanceToSquared(spot)).FirstOrDefault();
            if (who == null) return false;
            foreach (var i in items) i.carrier = who;
            var job = JobMaker.MakeJob(PostDefOf.Rimfolk_PostDrop, spot);
            job.locomotionUrgency = LocomotionUrgency.Walk;
            job.expiryInterval = GenDate.TicksPerHour * 3;
            job.checkOverrideOnExpire = true;
            who.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            PostWords.Say(who, "bring", null);
            return true;
        }

        // ---------------- The colony's own caravan ----------------

        private static readonly HashSet<int> formingAsked = new HashSet<int>();

        /// <summary>A caravan of the colony forming: one of its people fetches the letters going the way it leaves, while the goods are gathered.</summary>
        private static void Forming(Map map, Lord lord, LordJob_FormAndSendCaravan form)
        {
            if (formingAsked.Contains(lord.loadID) || !form.GatheringItemsNow) return;
            formingAsked.Add(lord.loadID);
            if (formingAsked.Count > 100) formingAsked.Clear();
            var exit = form.ExitSpot;
            var c = new MailCarrier
            {
                kind = PostCarrier.OwnCaravan, map = map, knowEdge = exit.IsValid, edgeHeading = exit.IsValid ? Outbox.CellHeading(map, exit) : 0f,
                means = PostMath.MeansOf(Ages.Ours, lord.ownedPawns.Any(x => x.RaceProps.Animal && OwnRiders.Ridable(x)))
            };
            if (!c.knowEdge) return;
            var letters = Outbox.For(c);
            if (letters.Count == 0) return;
            var spot = Outbox.Spot(map);
            var who = lord.ownedPawns.Where(p => p.IsColonist && !p.Downed && p.Spawned && p.carryTracker?.CarriedThing == null && p.CanReach(spot, PathEndMode.Touch, Danger.Some))
                .OrderBy(p => p.Position.DistanceToSquared(spot)).FirstOrDefault();
            if (who == null) return;
            c.name = "CC_Mail_Carrier_OwnCaravan".Translate(who.Named("PAWN")).Resolve();
            foreach (var l in letters) l.packedBy = who;
            carriers[who.thingIDNumber] = c;
            var job = JobMaker.MakeJob(PostDefOf.Rimfolk_PostTake, spot);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.expiryInterval = GenDate.TicksPerHour;
            job.checkOverrideOnExpire = true;
            who.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>
        /// Letters packed by one of a caravan: on the road once they're out on the world in the colony's caravan (its
        /// destination counted when it has one); back in the outbox if the caravan never left (called off) or they're lost.
        /// </summary>
        private static void Packed(Map map)
        {
            var book = NotableBook.Instance;
            if (book == null) return;
            foreach (var l in book.letters.Where(x => x.stage == LetterStage.Outbox && x.map == map && x.packed).ToList())
            {
                var p = l.packedBy;
                var caravan = p?.GetCaravan();
                if (caravan != null && caravan.Faction == Faction.OfPlayer)
                {
                    var c = CarrierFor(p);
                    c.kind = PostCarrier.OwnCaravan;
                    c.name ??= "CC_Mail_Carrier_OwnCaravan".Translate(p.Named("PAWN")).Resolve();
                    c.means = PostMath.MeansOf(Ages.Ours, caravan.PawnsListForReading.Any(x => x.RaceProps.Animal && OwnRiders.Ridable(x)));
                    Outbox.Carried(l, c, null);
                    continue;
                }
                bool forming = p != null && p.Spawned && p.Map == map && p.GetLord()?.LordJob is LordJob_FormAndSendCaravan;
                if (forming) continue;
                l.packed = false;
                l.packedBy = null;
                NotableBook.Changed();
            }
        }
    }
}
