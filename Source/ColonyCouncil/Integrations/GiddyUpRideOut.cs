using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Colonists ride out to far work and back, with Giddy-Up 2 (the player, 15 November 2026: "pawns should really use
    /// mounts whenever they walk away out of the base range ... avila did not mount up despite going really far to mine. it
    /// should mount to go there, and mount back up aftr finishing work. same for all other activities where they walk out
    /// further out of the base"; 16 November 2026: "it's not really working for all movement orders via giddy up default
    /// settings").
    /// <para>Giddy-Up looks for a mount only when the straight line from where a colonist stands to their new job (plus a
    /// haul's carry) is longer than its "Find a mount if trip is longer than..." setting, 120 cells by default, about half
    /// the player's 275-cell map: Avila's steel was about 70 cells from the base, so it never looked. A drafted colonist it
    /// never mounts by itself (only the right-click "Mount" or its "Mount" button). DESIGN.md has the whole of its rules.</para>
    /// <para>Rimfolk adds the trips it leaves out, as the job is chosen (after Giddy-Up's own look, so where it mounts
    /// someone nothing changes): a free colonist whose job lies 40 cells or more away (<see cref="RideOutMath.TripMin"/>)
    /// with the work, where a haul goes, or the colonist themselves 10 cells or more past the home area's edge
    /// (<see cref="RideOutMath.OutMin"/>), and the player's own movement orders to a drafted colonist the same, gets on the
    /// beast that gets them there soonest, Giddy-Up's own way (<see cref="GiddyUpMounts.MountFirst"/>): out to work whenever
    /// a beast is free no further from them than the work and no slower than they walk (since 19 November 2026; the player:
    /// "WHY DOES bautista still walk halfway across the map to mine without mounting up first? then the mount should wait
    /// for him until he's done"); back from out there and on a movement order when riding is no slower than walking by
    /// <see cref="RideOutMath.Slack"/>. Which beasts may be ridden stays Giddy-Up's to say (<see cref="GiddyUpRules"/>: its
    /// list of animals to ride, tameness, size and weight, health, hunger and rest, the Animals tab's automount column, the
    /// Assign tab's "allowed to ride"); Rimfolk also passes over a beast that is working, with a group, with young, kept for
    /// another rider (while it waits for them, carries them or they are getting on; an older mark of Giddy-Up's is left
    /// aside) or for the colony's own post rider. Giddy-Up then does the rest as it always does: the rider parks the beast
    /// near the work (its mount spots, a pen, or hitched nearby), the beast waits, and Rimfolk keeps it waiting while its
    /// rider is still at work out there (Giddy-Up's own timer from now, looked at every 250 ticks and at each job they
    /// choose, until a day after the last job they began), so it is there for the ride back. A handler out past the base
    /// sent to lead one of the colony's beasts home to its pen rides it there instead. Inside the base nothing changes.</para>
    /// </summary>
    public static class GiddyUpRideOut
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>On: the setting (Your other mods, Giddy-Up 2), the deeper game hooks, and Giddy-Up's own calls found.</summary>
        public static bool On => (ColonyCouncilMod.Settings?.rideOut ?? true) && RimfolkPatches.On && GiddyUpMounts.CanMountFirst && GiddyUpRules.Found;

        // Not saved: a colonist's last try to get on (the beast and the tick), beasts passed over after a failed try, and each
        // colonist's last decision on a trip of 40 cells or more (for the dev action).
        private static readonly Dictionary<int, (int animal, int tick)> tries = new Dictionary<int, (int, int)>();
        private static readonly Dictionary<long, int> refused = new Dictionary<long, int>();
        private static readonly Dictionary<int, RideOutNote> notes = new Dictionary<int, RideOutNote>();

        /// <summary>Colonists' last decisions kept at most (the oldest go first).</summary>
        private const int MaxNotes = 64;

        private static long Key(Pawn rider, Pawn animal) => ((long)rider.thingIDNumber << 32) | (uint)animal.thingIDNumber;

        /// <summary>A game loaded or begun: nothing of the last one's tries and decisions is kept.</summary>
        public static void Reset()
        {
            tries.Clear();
            refused.Clear();
            notes.Clear();
        }

        /// <summary>This colonist's last decision on a trip of 40 cells or more since the game was loaded, or null.</summary>
        internal static RideOutNote LastOf(Pawn p) => p != null && notes.TryGetValue(p.thingIDNumber, out var n) ? n : null;

        /// <summary>What Rimfolk would do about a colonist's chosen job: ride (on which beast), and the sums.</summary>
        internal sealed class Plan
        {
            public RideTrip kind;
            public Pawn animal;
            public float trip, carry, back, walkSeconds, rideSeconds;
            public int riderOut, targetOut, finalOut;
            public IntVec3 first = IntVec3.Invalid, final = IntVec3.Invalid;
            /// <summary>The beast is ridden to its pen in place of being led there on a rope.</summary>
            public IntVec3 penCell = IntVec3.Invalid;
            public string why;
            /// <summary>The quickest beast found that may be ridden (ridden or not), for the dev action.</summary>
            public Pawn best;
            public readonly List<string> beasts = new List<string>();
            public bool Ride => animal != null && why == null;
        }

        // ---------------- As a job is chosen ----------------

        /// <summary>
        /// A colonist has just chosen their next job (Pawn_JobTracker.DetermineNextJob, with nothing under way): getting on a
        /// beast first, Giddy-Up's own way, when the trip is one Rimfolk rides; and their waiting beast kept waiting while they
        /// are still out there.
        /// </summary>
        public static void NextJob(Pawn pawn, ref ThinkResult result)
        {
            var job = result.Job;
            if (job == null || pawn?.Map == null || !On) return;
            int now = Now;
            Settle(pawn, now);
            if (GiddyUpMounts.OwnJob(job.def)) return;
            var plan = Decide(pawn, job, now, false);
            var note = Note(pawn, job, plan, now);
            if (!plan.Ride) return;
            Job then = job;
            if (plan.penCell.IsValid)
            {
                // Ridden to its pen and got off there, in place of being led there on foot: the rope job is let go.
                then = JobMaker.MakeJob(GiddyUpMounts.DismountJob, plan.penCell);
                then.count = 1;
            }
            var mounted = GiddyUpMounts.MountFirst(pawn, plan.animal, result, then);
            if (note != null) note.mount = mounted == null ? RideMount.NotTaken : RideMount.Sent;
            if (mounted == null) return;
            result = mounted.Value;
            tries[pawn.thingIDNumber] = (plan.animal.thingIDNumber, now);
        }

        /// <summary>
        /// Keeps a colonist's decision for the dev action when it was about a trip of 40 cells or more (or a ride), except
        /// while they are in the saddle already; the note, or null when it isn't kept.
        /// </summary>
        private static RideOutNote Note(Pawn pawn, Job job, Plan plan, int now)
        {
            if (!plan.Ride && (!plan.first.IsValid || plan.trip + plan.carry < RideOutMath.TripMin || GiddyUpRules.Mounted(pawn))) return null;
            if (notes.Count >= MaxNotes && !notes.ContainsKey(pawn.thingIDNumber))
                notes.Remove(notes.OrderBy(kv => kv.Value.tick).First().Key);
            var note = new RideOutNote
            {
                tick = now,
                job = job.def?.defName,
                kind = plan.kind,
                cells = plan.trip + plan.carry,
                why = plan.why,
                beast = (plan.animal ?? plan.best)?.LabelShort,
                rideSeconds = plan.rideSeconds,
                walkSeconds = plan.walkSeconds
            };
            notes[pawn.thingIDNumber] = note;
            return note;
        }

        /// <summary>
        /// The last try to get on: they are in the saddle now (it took), or not (the beast is passed over by them for an hour).
        /// </summary>
        private static void Settle(Pawn pawn, int now)
        {
            if (tries.Count == 0 && refused.Count == 0) return;
            if (tries.TryGetValue(pawn.thingIDNumber, out var t))
            {
                tries.Remove(pawn.thingIDNumber);
                bool on = GiddyUpRules.Mounted(pawn);
                if (!on && now >= t.tick && now - t.tick < RideOutMath.RefusedTicks)
                    refused[((long)pawn.thingIDNumber << 32) | (uint)t.animal] = now + RideOutMath.RefusedTicks;
                if (notes.TryGetValue(pawn.thingIDNumber, out var note) && note.tick == t.tick && note.mount == RideMount.Sent)
                    note.mount = on ? RideMount.InSaddle : RideMount.DidNotGetOn;
            }
            if (refused.Count > 64)
                foreach (var k in refused.Where(kv => kv.Value <= now || kv.Value - now > RideOutMath.RefusedTicks).Select(kv => kv.Key).ToList())
                    refused.Remove(k);
        }

        /// <summary>The whole decision for a colonist and a job; <paramref name="explain"/> lists every beast and why.</summary>
        internal static Plan Decide(Pawn pawn, Job job, int now, bool explain)
        {
            var plan = new Plan();
            var map = pawn?.Map;
            if (job == null || map == null)
            {
                plan.why = "no job, or not on a map";
                return plan;
            }
            // The plain checks first: most choices end here, before anything is asked of Giddy-Up.
            plan.why = RiderWhyNot(pawn, job, out bool order);
            if (plan.why != null) return plan;
            plan.why = JobWhyNot(job, order);
            if (plan.why != null) return plan;
            if (!Targets(job, out var first, out var second))
            {
                plan.why = "the job has no place to go";
                return plan;
            }
            bool haul = second.IsValid && (job.def == JobDefOf.HaulToCell || job.def == JobDefOf.HaulToContainer);
            plan.first = first;
            plan.final = haul ? second : first;
            plan.trip = pawn.Position.DistanceTo(first);
            plan.carry = haul ? first.DistanceTo(second) : 0f;
            plan.riderOut = BaseEdge.CellsOut(map, pawn.Position);
            plan.targetOut = BaseEdge.CellsOut(map, first);
            plan.finalOut = haul ? BaseEdge.CellsOut(map, second) : plan.targetOut;

            // Their beast waiting by the work out there keeps waiting while they are still at it (the job chosen now is their last).
            if (!order && (RideOutMath.FarOut(plan.riderOut) || RideOutMath.FarOut(plan.targetOut))) KeepWaiting(pawn, GiddyUpRules.ReservedMount(pawn), now, now);

            // A handler out past the base sent to lead a beast home to its pen on a rope: they ride it there instead.
            if (job.def == JobDefOf.RopeToPen && job.targetA.Thing is Pawn led && RopedHome(pawn, job, led, plan, now)) return plan;

            plan.kind = RideOutMath.Kind(plan.trip + plan.carry, plan.riderOut, plan.targetOut, plan.finalOut, order);
            if (plan.kind == RideTrip.None)
            {
                plan.why = plan.trip + plan.carry < RideOutMath.TripMin
                    ? "a short trip (" + (int)(plan.trip + plan.carry) + " cells, under " + (int)RideOutMath.TripMin + ")"
                    : "inside the base (the work " + plan.targetOut + " and they " + plan.riderOut + " cells past the home area, under " + RideOutMath.OutMin + ")";
                return plan;
            }
            plan.why = RiderGiddyWhyNot(pawn);
            if (plan.why != null)
            {
                plan.kind = RideTrip.None;
                return plan;
            }
            if (GiddyUpRules.BadSpot(first))
            {
                plan.why = "Giddy-Up found no place to leave a beast near this work before";
                return plan;
            }
            plan.back = plan.kind == RideTrip.Out ? plan.final.DistanceTo(pawn.Position) : 0f;
            float walk = pawn.GetStatValue(StatDefOf.MoveSpeed);
            plan.walkSeconds = RideOutMath.WalkSeconds(plan.trip, plan.carry, plan.back, walk, plan.kind);
            int skill = pawn.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0;
            var reservations = GiddyUpRules.Reservations(map);
            // Out to work: any beast no further from them than the work (and no slower than they walk), the quickest taken.
            bool outTrip = plan.kind == RideTrip.Out;
            float reach = plan.trip + plan.carry;
            Pawn best = null;
            float bestSeconds = float.MaxValue;
            foreach (var a in map.mapPawns.SpawnedColonyAnimals)
            {
                float toAnimal = pawn.Position.DistanceTo(a.Position);
                string no = outTrip && !RideOutMath.NearEnough(toAnimal, reach)
                    ? "further from them than the work (" + (int)toAnimal + " cells against " + (int)reach + ")"
                    : AnimalWhyNot(pawn, a, reservations, now);
                float seconds = float.MaxValue;
                if (no == null)
                {
                    float speed = GiddyUpRules.RidingSpeed(a, pawn);
                    if (outTrip && !RideOutMath.FastEnough(speed, walk))
                        no = "slower under them than they walk (" + speed.ToString("0.0") + " cells a second against " + walk.ToString("0.0") + ")";
                    else
                    {
                        float wild = a.kindDef?.race != null ? a.kindDef.race.GetStatValueAbstract(StatDefOf.Wildness) : 0f;
                        seconds = RideOutMath.RideSeconds(toAnimal, a.Position.DistanceTo(first), plan.carry, plan.back, walk, speed,
                            RideOutMath.MountSeconds(a.BodySize, wild, skill), plan.kind);
                        if (seconds < bestSeconds)
                        {
                            bestSeconds = seconds;
                            best = a;
                        }
                    }
                }
                if (explain && a.RaceProps.Animal)
                {
                    var stale = StaleKeeper(a, pawn);
                    plan.beasts.Add("  " + a.LabelShort + " (" + a.KindLabel + ", " + (int)toAnimal + " cells away): "
                                    + (no ?? "may be ridden, " + seconds.ToString("0") + " s riding")
                                    + (stale != null ? "; Giddy-Up's mark keeping it for " + stale.LabelShort + " is old (it neither waits for, carries nor is being got on by them), so it is left aside" : ""));
                }
            }
            plan.best = best;
            plan.rideSeconds = bestSeconds;
            if (best == null)
            {
                plan.why = outTrip ? "no beast Giddy-Up lets them ride is free, no further from them than the work and no slower than they walk"
                    : "no beast Giddy-Up lets them ride is free";
                return plan;
            }
            if (!RideOutMath.Rides(plan.kind, bestSeconds, plan.walkSeconds))
            {
                plan.why = "walking is quicker (" + plan.walkSeconds.ToString("0") + " s on foot against " + bestSeconds.ToString("0") + " s on " + best.LabelShort + ")";
                return plan;
            }
            plan.animal = best;
            return plan;
        }

        /// <summary>
        /// A handler's job to lead one of the colony's beasts home to its pen on a rope (the game's own, with the pen named),
        /// the handler out past the base and the pen far off: they ride it to the pen and get off there instead (Giddy-Up's
        /// own "Dismount" job), when Giddy-Up and Rimfolk let them ride it. Giddy-Up's own rope job for a beast that stopped
        /// waiting names no pen, and is left as it is.
        /// </summary>
        private static bool RopedHome(Pawn pawn, Job job, Pawn led, Plan plan, int now)
        {
            var cell = job.targetB.IsValid ? job.targetB.Cell : IntVec3.Invalid;
            if (!cell.IsValid || job.targetC.Thing == null || led.Faction != Faction.OfPlayer || GiddyUpMounts.DismountJob == null) return false;
            if (!RideOutMath.FarOut(plan.riderOut) || pawn.Position.DistanceTo(cell) < RideOutMath.TripMin) return false;
            if (RiderGiddyWhyNot(pawn) != null || AnimalWhyNot(pawn, led, GiddyUpRules.Reservations(pawn.Map), now) != null) return false;
            plan.kind = RideTrip.Back;
            plan.animal = led;
            plan.penCell = cell;
            plan.why = null;
            return true;
        }

        /// <summary>
        /// Giddy-Up's waiting beast stays for the ride back while its rider is still at work out past the base: its wait runs
        /// Giddy-Up's own timer from now, until a day after the rider's last job began at most (<paramref name="lastJob"/>,
        /// <see cref="RideOutMath.WaitExpiry"/>). Called as they choose a job and every 250 ticks (<see cref="Rare"/>).
        /// </summary>
        private static void KeepWaiting(Pawn rider, Pawn beast, int lastJob, int now)
        {
            try
            {
                if (rider == null || beast == null || !beast.Spawned || beast.Map != rider.Map || !GiddyUpMounts.WaitingFor(beast, rider)) return;
                if (beast.Position.DistanceTo(rider.Position) > 60f) return;
                var wait = beast.CurJob;
                wait.expiryInterval = RideOutMath.WaitExpiry(wait.startTick, now, wait.expiryInterval, GiddyUpRules.WaitTimer, lastJob);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("riding out: keeping a beast waiting", e);
            }
        }

        /// <summary>
        /// Every 250 ticks (<see cref="Sched.RareRideOut"/>, with riding out on): each of the colony's beasts waiting for its
        /// rider (Giddy-Up's "WaitForRider") keeps waiting while that rider is at work out past the base, so a long stretch of
        /// work out there between two job choices doesn't send it home.
        /// </summary>
        internal static void Rare(int now)
        {
            if (GiddyUpMounts.WaitJob == null) return;
            var maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                var map = maps[m];
                foreach (var beast in map.mapPawns.SpawnedColonyAnimals)
                {
                    var job = beast.CurJob;
                    if (job == null || job.def != GiddyUpMounts.WaitJob || !(job.targetA.Thing is Pawn rider)) continue;
                    if (!OutThere(rider, map)) continue;
                    KeepWaiting(rider, beast, rider.CurJob.startTick, now);
                }
            }
        }

        /// <summary>The rider is up, one of the colony's, and at work out past the base (they, or where their job takes them).</summary>
        private static bool OutThere(Pawn rider, Map map)
        {
            if (rider.Dead || rider.Downed || !rider.Spawned || rider.Map != map || rider.Faction != Faction.OfPlayer || rider.CurJob == null) return false;
            if (RideOutMath.FarOut(BaseEdge.CellsOut(map, rider.Position))) return true;
            return Targets(rider.CurJob, out var first, out _) && RideOutMath.FarOut(BaseEdge.CellsOut(map, first));
        }

        // ---------------- Who, what and which beast ----------------

        /// <summary>Why this colonist doesn't ride out for this job, or null; <paramref name="order"/>: a drafted colonist's movement order.</summary>
        internal static string RiderWhyNot(Pawn p, Job job, out bool order)
        {
            order = false;
            if (p == null || !p.Spawned || p.Map == null) return "not on a map";
            if (!p.IsColonistPlayerControlled || !p.IsFreeNonSlaveColonist || p.IsQuestLodger()) return "not one of the colony's own free colonists";
            if (p.Downed || p.InMentalState || p.Dead) return "down or not in their right mind";
            if (p.Drafted)
            {
                if (job == null || job.def != JobDefOf.Goto || !job.playerForced) return "drafted, and this isn't a movement order";
                if (!GiddyUpRules.BattleMounts) return "drafted, and Giddy-Up's battle mounts are off in its settings";
                order = true;
            }
            else if (!GiddyUpRules.RideAndRoll) return "Giddy-Up's Ride and Roll is off in its settings";
            if (p.GetLord() != null) return "with a group (a caravan forming, a ritual, a party)";
            if (p.IsBorrowedByAnyFaction()) return "lent to another people";
            if (p.carryTracker?.CarriedThing != null) return "carrying something";
            if (p.Map.Biome?.inVacuum ?? false) return "in a vacuum";
            if (GiddyUpMounts.Mounting(p)) return "getting on already";
            if (PostBook.Instance?.RideOf(p) != null) return "on the colony's own rider's errand";
            return null;
        }

        /// <summary>Why Giddy-Up wouldn't have this colonist ride at all, or null: in the saddle already, not allowed to ride, too young.</summary>
        internal static string RiderGiddyWhyNot(Pawn p)
        {
            if (GiddyUpRules.Mounted(p)) return "already riding";
            if (!GiddyUpRules.MayRide(p)) return "not allowed to ride (Giddy-Up's column on the Assign tab)";
            if (!GiddyUpRules.CapableOfRiding(p)) return "too young to handle animals, or in a wheelchair (Giddy-Up's rule)";
            return null;
        }

        /// <summary>Why this job isn't ridden to, or null.</summary>
        internal static string JobWhyNot(Job job, bool order)
        {
            var d = job?.def;
            if (d == null) return "no job";
            if (GiddyUpMounts.OwnJob(d)) return "one of Giddy-Up's own jobs";
            if (order) return null;
            if (d == JobDefOf.Flee || d == JobDefOf.FleeAndCower || d == JobDefOf.Vomit || d.isIdle) return "fleeing, sick or idle";
            if (job.exitMapOnArrival) return "leaving the map";
            if (d == PostDefOf.Rimfolk_RiderSetOut) return "the colony's own rider setting out (they bring their beast along)";
            return null;
        }

        /// <summary>Why this colonist can't ride this beast now, or null: Giddy-Up's rules first, then Rimfolk's.</summary>
        internal static string AnimalWhyNot(Pawn rider, Pawn a, object reservations, int now)
        {
            if (a == null || a == rider || a.Dead) return "gone";
            if (a.Downed) return "downed";
            if (!a.Spawned || a.Map != rider.Map) return "not here";
            if (a.GetLord() != null) return "with a group (a caravan, a ritual)";
            if (a.roping != null && a.roping.IsRopedByPawn) return "led on a rope";
            if (GiddyUpMounts.CarryingRider(a)) return "carrying a rider";
            var keeper = GiddyUpRules.ReservedBy(a);
            if (keeper != null && keeper != rider && !keeper.Dead && keeper.Spawned && KeptFor(a, keeper))
                return "kept for " + keeper.LabelShort + " (waiting for them, carrying them, or they are getting on)";
            if (Working(a)) return "at work (" + (a.CurJobDef?.label ?? "busy") + ")";
            if (WithYoung(a)) return "with young";
            if (PostBook.Instance?.rides?.Any(r => r?.mount == a) ?? false) return "kept for the colony's own rider";
            if (refused.TryGetValue(Key(rider, a), out int until) && until > now && until - now <= RideOutMath.RefusedTicks) return "a try to get on it failed a little while ago";
            string giddy = GiddyUpRules.WhyNotMountable(a, rider, reservations);
            if (giddy != null) return "Giddy-Up: " + giddy;
            if (!GiddyUpRules.Allowed(rider, a)) return "Giddy-Up: its automount column on the Animals tab doesn't allow " + rider.LabelShort;
            if (!rider.CanReach(a, PathEndMode.Touch, Danger.Some)) return "out of reach";
            return null;
        }

        /// <summary>
        /// Giddy-Up's mark keeping this beast for that rider still holds: it waits for them, carries them, or they are getting
        /// on it (or have that next in their queue). Otherwise the mark is old (in the player's save of 18 November 2026 a
        /// horse still kept for Soot and a titanis for Wyninn, all of them at home) and left aside.
        /// </summary>
        private static bool KeptFor(Pawn a, Pawn keeper) =>
            RideOutMath.KeptFor(GiddyUpMounts.WaitingFor(a, keeper), GiddyUpMounts.Riding(keeper, a), GiddyUpMounts.MountingOnto(keeper, a));

        /// <summary>The rider an old mark of Giddy-Up's keeps this beast for (left aside), or null.</summary>
        private static Pawn StaleKeeper(Pawn a, Pawn rider)
        {
            var keeper = GiddyUpRules.ReservedBy(a);
            return keeper != null && keeper != rider && !keeper.Dead && keeper.Spawned && !KeptFor(a, keeper) ? keeper : null;
        }

        /// <summary>
        /// At work: hauling, rescuing, fighting or hunting, mating, laying, a job a work giver gave it, or anything else its
        /// trainer or the game set it to that isn't standing about, wandering, lying down, eating or following someone.
        /// </summary>
        private static bool Working(Pawn a)
        {
            var job = a.CurJob;
            var d = job?.def;
            if (d == null) return false;
            if (job.workGiverDef != null) return true;
            return d == JobDefOf.HaulToCell || d == JobDefOf.HaulToContainer || d == JobDefOf.Rescue || d == JobDefOf.Capture || d == JobDefOf.AttackMelee
                   || d == JobDefOf.AttackStatic || d == JobDefOf.PredatorHunt || d == JobDefOf.Hunt || d == JobDefOf.Mate || d == JobDefOf.LayEgg
                   || d == JobDefOf.Flee || d == JobDefOf.FleeAndCower || d == JobDefOf.Wait_Downed;
        }

        /// <summary>Caring for young: the mother of a baby of the colony's on this map (still in its first stage of life).</summary>
        private static bool WithYoung(Pawn a)
        {
            if (a.gender != Gender.Female || a.Map == null) return false;
            foreach (var y in a.Map.mapPawns.SpawnedColonyAnimals)
                if (y != a && !y.Dead && y.ageTracker != null && y.ageTracker.CurLifeStageIndex == 0 && y.GetMother() == a) return true;
            return false;
        }

        /// <summary>
        /// Where the job takes them, Giddy-Up's own way (DistanceUtility.DetermineTargets): for tending, refuelling and
        /// repairs the second target first, for a bill its first ingredient, else the first target (the first of a queue).
        /// </summary>
        internal static bool Targets(Job job, out IntVec3 first, out IntVec3 second)
        {
            var d = job.def;
            if (d == JobDefOf.TendPatient || d == JobDefOf.Refuel || d == JobDefOf.FixBrokenDownBuilding)
            {
                first = FirstOf(job, TargetIndex.B);
                second = FirstOf(job, TargetIndex.A);
            }
            else if (d == JobDefOf.DoBill && !job.targetQueueB.NullOrEmpty())
            {
                first = job.targetQueueB[0].Cell;
                second = FirstOf(job, TargetIndex.A);
            }
            else
            {
                first = FirstOf(job, TargetIndex.A);
                second = FirstOf(job, TargetIndex.B);
            }
            return first.IsValid;
        }

        private static IntVec3 FirstOf(Job job, TargetIndex i)
        {
            // The queue read as it is (the game's GetTargetQueue makes an empty one where there is none).
            var queue = i == TargetIndex.A ? job.targetQueueA : i == TargetIndex.B ? job.targetQueueB : null;
            return queue != null && queue.Count != 0 ? queue[0].Cell : job.GetTarget(i).Cell;
        }

        // ---------------- For the dev action ----------------

        /// <summary>Why a colonist rides out for the job they are doing now, or not, with every beast and the sums (for the log).</summary>
        internal static string Explain(Pawn pawn)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Riding out with Giddy-Up: " + (pawn?.LabelShort ?? "nobody"));
            if (pawn == null) return sb.ToString();
            sb.AppendLine("  Rimfolk's part " + (On ? "on" : "off (the setting, the deeper game hooks, or Giddy-Up's calls not found)") + "; Giddy-Up's rules "
                          + (GiddyUpRules.Found ? "read" : "not found") + ".");
            sb.AppendLine("  Giddy-Up's own: Ride and Roll " + (GiddyUpRules.RideAndRoll ? "on" : "off") + ", battle mounts " + (GiddyUpRules.BattleMounts ? "on" : "off")
                          + ", \"Find a mount if trip is longer than\" " + GiddyUpRules.MinDistance + " cells, \"Wait for rider\" " + GiddyUpRules.WaitTimer + " ticks.");
            var last = LastOf(pawn);
            sb.AppendLine("  " + (last != null ? last.Describe(Now) : "No decision on a trip of " + (int)RideOutMath.TripMin + " cells or more since the game was loaded."));
            var job = pawn.CurJob;
            sb.AppendLine("  Their job now: " + (job?.def?.defName ?? "none") + (job != null && job.playerForced ? " (ordered)" : "")
                          + (GiddyUpRules.Mounted(pawn) ? ", in the saddle" : ""));
            if (job == null) return sb.ToString();
            var plan = Decide(pawn, job, Now, true);
            if (plan.first.IsValid)
            {
                sb.AppendLine("  Trip " + (int)plan.trip + " cells" + (plan.carry > 0f ? " and a carry of " + (int)plan.carry : "") + "; past the home area: they "
                              + plan.riderOut + ", the work " + plan.targetOut + (plan.final != plan.first ? ", where it goes " + plan.finalOut : "") + " cells.");
                sb.AppendLine("  Giddy-Up's own look: " + (pawn.Drafted ? "never for a drafted colonist"
                    : plan.trip + plan.carry > GiddyUpRules.MinDistance ? "over its distance, so it looks for a mount itself" : "under its distance, so it doesn't look") + ".");
            }
            if (plan.kind != RideTrip.None)
                sb.AppendLine("  Rimfolk's trip: " + plan.kind + "; " + plan.walkSeconds.ToString("0") + " s on foot. "
                              + (plan.kind == RideTrip.Out
                                  ? "Out to work they ride the quickest free beast no further from them than the work (" + (int)(plan.trip + plan.carry) + " cells) and no slower than they walk."
                                  : "They ride only when it takes no longer than " + RideOutMath.Slack(plan.kind).ToString("0.##") + " times walking."));
            foreach (var line in plan.beasts) sb.AppendLine(line);
            sb.AppendLine(plan.Ride
                ? "  Rimfolk: they ride " + plan.animal.LabelShort + (plan.penCell.IsValid ? " home to its pen" : "") + "."
                : "  Rimfolk: no ride, " + plan.why + ".");
            return sb.ToString();
        }

        /// <summary>
        /// For "Rimfolk: check myself": riding out's state this session, with Giddy-Up loaded (null without it), and whether
        /// it needs a look (it switched itself off, or a part of Giddy-Up it can't ride without wasn't found).
        /// </summary>
        internal static (string text, bool warn) CheckLine()
        {
            if (!GiddyUpMounts.On) return (null, false);
            var off = new List<string>();
            bool warn = false;
            if (!(ColonyCouncilMod.Settings?.rideOut ?? true)) off.Add("its setting is off");
            if (!RimfolkPatches.On) off.Add("the deeper game hooks are off");
            if (Patch_RideOutNextJob.Broken)
            {
                off.Add("it switched itself off for this session after an error (the log has it)");
                warn = true;
            }
            if (GiddyUpMounts.InjectFailed)
            {
                off.Add("Giddy-Up's way to put getting on before a job (GoMount with Inject) failed with an error and is left alone for this session (the log has it)");
                warn = true;
            }
            else if (!GiddyUpMounts.InjectFound || GiddyUpMounts.MountJob == null)
            {
                off.Add("Giddy-Up's way to put getting on before a job (GoMount with Inject, and its Mount job) wasn't found");
                warn = true;
            }
            if (!GiddyUpRules.Found)
            {
                off.Add("Giddy-Up's rules for riding weren't all found");
                warn = true;
            }
            if (!RimfolkPatches.Applied.Contains(nameof(Patch_RideOutNextJob)))
            {
                off.Add("its game hook on choosing a job didn't apply");
                warn = true;
            }
            var nobody = new List<string>();
            if (!GiddyUpRules.CanRideFound) nobody.Add("\"allowed to ride\" (canRide)");
            if (!GiddyUpRules.CapableFound) nobody.Add("IsCapableOfRiding");
            if (nobody.Count > 0) warn = true;
            var missing = GiddyUpRules.Missing();
            string text = "Riding out with Giddy-Up 2: " + (off.Count == 0 ? "on" : "off, as " + string.Join("; ", off)) + "."
                          + (nobody.Count > 0 ? " Giddy-Up's " + string.Join(" and ", nobody) + (nobody.Count > 1 ? " weren't" : " wasn't") + " found, so Rimfolk lets nobody ride out." : "")
                          + (missing.Count > 0 ? " Not found, done without: " + string.Join(", ", missing) + "." : "");
            return (text, warn);
        }
    }

    /// <summary>
    /// How far each cell lies past the home area's edge (0 inside it), in steps any of the eight ways, up to 255; per map,
    /// worked out again when the home area changes or after an hour.
    /// </summary>
    internal static class BaseEdge
    {
        public const int Far = 255;

        private sealed class Field
        {
            public int count, at, cells;
            public byte[] steps;
        }

        private static readonly Dictionary<int, Field> fields = new Dictionary<int, Field>();

        public static int CellsOut(Map map, IntVec3 c)
        {
            if (map == null || !c.InBounds(map)) return Far;
            var home = map.areaManager?.Home;
            if (home == null || home.TrueCount == 0) return Far;
            int now = Find.TickManager?.TicksGame ?? 0, n = map.cellIndices.NumGridCells;
            if (!fields.TryGetValue(map.uniqueID, out var f) || f.count != home.TrueCount || f.cells != n || now < f.at || now - f.at > Sched.HourTicks)
            {
                if (fields.Count > 8) fields.Clear();
                f = Build(map, home);
                f.at = now;
                fields[map.uniqueID] = f;
            }
            return f.steps[map.cellIndices.CellToIndex(c)];
        }

        private static Field Build(Map map, Area home)
        {
            int w = map.Size.x, h = map.Size.z, n = w * h;
            var steps = new byte[n];
            for (int i = 0; i < n; i++) steps[i] = Far;
            var queue = new int[n];
            int head = 0, tail = 0;
            foreach (var c in home.ActiveCells)
            {
                int i = map.cellIndices.CellToIndex(c);
                if (steps[i] == 0) continue;
                steps[i] = 0;
                queue[tail++] = i;
            }
            while (head < tail)
            {
                int i = queue[head++], d = steps[i];
                if (d >= Far - 1) continue;
                int x = i % w, z = i / w;
                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int nx = x + dx, nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                    int j = nz * w + nx;
                    if (steps[j] != Far) continue;
                    steps[j] = (byte)(d + 1);
                    queue[tail++] = j;
                }
            }
            return new Field { count = home.TrueCount, cells = n, steps = steps };
        }
    }

    /// <summary>
    /// Giddy-Up 2's own rules for riding, read by name (soft: none of it without the mod): who may ride a beast
    /// (IsMountableUtility.IsMountable, its automount column, the rider's "allowed to ride" and age), its settings (Ride
    /// and Roll, battle mounts, the auto-mount distance, the wait timer), a rider's data (in the saddle, the beast kept for
    /// them), a beast's speed under a rider, and the places it found no room to leave a beast. Anything not found reads as
    /// "no" where it decides riding, so Rimfolk never lets someone ride that Giddy-Up wouldn't.
    /// </summary>
    internal static class GiddyUpRules
    {
        private static bool looked;
        private static MethodInfo isMountable, isAllowed, capable, dataOf, ridingSpeed, reservedAnimals;
        private static PropertyInfo mountProp, reservedMountProp, reservedByProp, singletonProp, badSpotsProp;
        private static FieldInfo canRideField, mountedSetField, rideAndRollField, battleMountsField, waitTimerField, minDistanceField;

        private static bool Look()
        {
            if (looked) return isMountable != null;
            looked = true;
            try
            {
                const BindingFlags S = BindingFlags.Public | BindingFlags.Static;
                const BindingFlags I = BindingFlags.Public | BindingFlags.Instance;
                var utility = GenTypes.GetTypeInAnyAssembly("GiddyUp.IsMountableUtility");
                isMountable = utility?.GetMethods(S).FirstOrDefault(m => m.Name == "IsMountable" && m.GetParameters().Length == 7 && m.GetParameters()[1].IsOut);
                isAllowed = utility?.GetMethods(S).FirstOrDefault(m => m.Name == "IsAllowed" && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType == typeof(Pawn));
                capable = utility?.GetMethods(S).FirstOrDefault(m => m.Name == "IsCapableOfRiding" && m.GetParameters().Length == 2);
                reservedAnimals = utility?.GetMethods(S).FirstOrDefault(m => m.Name == "FetchReservedAnimals" && m.GetParameters().Length == 1);
                var storage = GenTypes.GetTypeInAnyAssembly("GiddyUp.StorageUtility");
                dataOf = storage?.GetMethods(S).FirstOrDefault(m => m.Name == "GetExtendedPawnData" && m.GetParameters().Length == 1);
                var data = GenTypes.GetTypeInAnyAssembly("GiddyUp.ExtendedPawnData");
                mountProp = data?.GetProperty("Mount", I);
                reservedMountProp = data?.GetProperty("ReservedMount", I);
                reservedByProp = data?.GetProperty("ReservedBy", I);
                canRideField = data?.GetField("canRide", I);
                var store = GenTypes.GetTypeInAnyAssembly("GiddyUp.ExtendedDataStorage");
                singletonProp = store?.GetProperty("Singleton", S);
                badSpotsProp = store?.GetProperty("BadSpots", I);
                mountedSetField = store?.GetField("isMounted", S);
                var settings = GenTypes.GetTypeInAnyAssembly("GiddyUp.ModSettings_GiddyUp");
                rideAndRollField = settings?.GetField("rideAndRollEnabled", S);
                battleMountsField = settings?.GetField("battleMountsEnabled", S);
                waitTimerField = settings?.GetField("waitForRiderTimer", S);
                minDistanceField = settings?.GetField("minAutoMountDistance", S);
                var stat = GenTypes.GetTypeInAnyAssembly("GiddyUp.StatPart_Riding");
                ridingSpeed = stat?.GetMethod("GetRidingSpeed", S, null, new[] { typeof(float), typeof(Pawn), typeof(Pawn_SkillTracker) }, null);
            }
            catch (Exception e)
            {
                isMountable = null;
                CouncilLog.ErrorOnce("Giddy-Up: its rules for riding", e);
            }
            return isMountable != null;
        }

        /// <summary>Giddy-Up's rules that decide riding were all found.</summary>
        public static bool Found => Look() && isAllowed != null && dataOf != null && mountProp != null && reservedByProp != null && rideAndRollField != null;

        /// <summary>Its "allowed to ride" (canRide) was found; without it Rimfolk reads everyone as not allowed.</summary>
        public static bool CanRideFound => Look() && canRideField != null;

        /// <summary>Its IsCapableOfRiding was found; without it Rimfolk reads everyone as not able to ride.</summary>
        public static bool CapableFound => Look() && capable != null;

        /// <summary>The parts of Giddy-Up looked for by name and not found, in a few words each with what is done instead (for the self-check).</summary>
        public static List<string> Missing()
        {
            var list = new List<string>();
            Look();
            void Need(object member, string what)
            {
                if (member == null) list.Add(what);
            }
            Need(isMountable, "IsMountable (no beast may be ridden)");
            Need(isAllowed, "IsAllowed (no beast may be ridden)");
            Need(dataOf, "GetExtendedPawnData (no rider's data)");
            Need(mountProp, "Mount");
            Need(reservedByProp, "ReservedBy");
            Need(rideAndRollField, "rideAndRollEnabled (read as off)");
            Need(battleMountsField, "battleMountsEnabled (read as off)");
            Need(reservedMountProp, "ReservedMount (the wait isn't kept up as a job is chosen)");
            Need(reservedAnimals, "FetchReservedAnimals (Giddy-Up looks the map's reservations up itself)");
            Need(ridingSpeed, "GetRidingSpeed (each beast's own speed is used)");
            Need(waitTimerField, "waitForRiderTimer (10000 ticks is used)");
            Need(minDistanceField, "minAutoMountDistance (120 cells is shown)");
            if (singletonProp == null || badSpotsProp == null) list.Add("BadSpots (places with no room to leave a beast aren't known)");
            return list;
        }

        private static T Setting<T>(FieldInfo f, T fallback)
        {
            try
            {
                return f?.GetValue(null) is T v ? v : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        public static bool RideAndRoll => Look() && Setting(rideAndRollField, false);
        public static bool BattleMounts => Look() && Setting(battleMountsField, false);
        public static int WaitTimer => Look() ? Setting(waitTimerField, 10000) : 10000;
        public static int MinDistance => Look() ? Setting(minDistanceField, 120) : 120;

        private static object Data(Pawn p)
        {
            if (!Look() || dataOf == null || p == null) return null;
            try
            {
                return dataOf.Invoke(null, new object[] { p });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("Giddy-Up: a pawn's riding data", e);
                return null;
            }
        }

        private static Pawn PawnOf(PropertyInfo prop, object data)
        {
            try
            {
                return data == null || prop == null ? null : prop.GetValue(data) as Pawn;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>In the saddle (Giddy-Up's own quick list of riders, else their data).</summary>
        public static bool Mounted(Pawn p)
        {
            if (p == null || !Look()) return false;
            try
            {
                if (mountedSetField?.GetValue(null) is HashSet<int> set) return set.Contains(p.thingIDNumber);
            }
            catch
            {
                // their data below
            }
            return PawnOf(mountProp, Data(p)) != null;
        }

        /// <summary>The beast kept for this rider (the one they got off, waiting for them).</summary>
        public static Pawn ReservedMount(Pawn rider) => PawnOf(reservedMountProp, Data(rider));

        /// <summary>The rider this beast is kept for (waiting for them, or they are getting on).</summary>
        public static Pawn ReservedBy(Pawn animal) => PawnOf(reservedByProp, Data(animal));

        /// <summary>Allowed to ride: Giddy-Up's column on the Assign tab.</summary>
        public static bool MayRide(Pawn p)
        {
            var data = Data(p);
            if (data == null || canRideField == null) return false;
            try
            {
                return canRideField.GetValue(data) is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Old enough to handle animals and not in a wheelchair (Giddy-Up's IsCapableOfRiding).</summary>
        public static bool CapableOfRiding(Pawn p)
        {
            if (!Look() || capable == null) return false;
            try
            {
                return capable.Invoke(null, new object[] { p, null }) is bool b && b;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("Giddy-Up: who is able to ride", e);
                return false;
            }
        }

        /// <summary>The map's reservations on animals, once per choice (Giddy-Up's own list for IsMountable).</summary>
        public static object Reservations(Map map)
        {
            if (!Look() || reservedAnimals == null || map == null) return null;
            try
            {
                return reservedAnimals.Invoke(null, new object[] { map });
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Why Giddy-Up won't let this rider on this beast now, in plain words, or null: its list of animals to ride, grown,
        /// tame, not too heavy for it, not busy, roped, ridden or reserved, not hurt, hungry or tired, its research done.
        /// </summary>
        public static string WhyNotMountable(Pawn animal, Pawn rider, object reservations)
        {
            if (!Look()) return "its rules weren't found";
            try
            {
                var args = new object[] { animal, null, rider, true, true, true, reservations };
                if (isMountable.Invoke(null, args) is bool ok && ok) return null;
                return Reason(args[1]?.ToString());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("Giddy-Up: whether a beast may be ridden", e);
                return "its rules couldn't be asked";
            }
        }

        private static string Reason(string r)
        {
            switch (r)
            {
                case "NotInModOptions": return "not on its list of animals to ride (its mod settings)";
                case "NotFullyGrown": return "not grown";
                case "NeedsTraining": return "not tame";
                case "TooHeavy": return "the rider is too heavy for it";
                case "IsPoorCondition": return "hurt, hungry or tired";
                case "IsBusy": return "busy";
                case "IsRoped": return "led on a rope";
                case "MountedByAnother": return "ridden by someone else";
                case "IsReserved": return "reserved by someone";
                case "IsBusyWithCaravan": return "with a caravan";
                case "MissingResearch": return "its research isn't done";
                case "WrongFaction": return "not the colony's";
                case "NotAnimal": return "not an animal";
                default: return r ?? "it says no";
            }
        }

        /// <summary>The Animals tab's automount column lets this rider take it (Giddy-Up's IsAllowed).</summary>
        public static bool Allowed(Pawn rider, Pawn animal)
        {
            if (!Look() || isAllowed == null) return false;
            try
            {
                return isAllowed.Invoke(null, new object[] { rider, animal }) is bool b && b;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("Giddy-Up: the automount column", e);
                return false;
            }
        }

        /// <summary>The beast's speed under this rider (Giddy-Up's StatPart_Riding.GetRidingSpeed), else its own speed.</summary>
        public static float RidingSpeed(Pawn animal, Pawn rider)
        {
            float speed = animal.GetStatValue(StatDefOf.MoveSpeed);
            if (!Look() || ridingSpeed == null) return speed;
            try
            {
                return ridingSpeed.Invoke(null, new object[] { speed, animal, rider.skills }) is float f && f > 0f ? f : speed;
            }
            catch
            {
                return speed;
            }
        }

        /// <summary>Giddy-Up found no place to leave a beast near this cell before (its "bad spots").</summary>
        public static bool BadSpot(IntVec3 cell)
        {
            if (!Look() || singletonProp == null || badSpotsProp == null) return false;
            try
            {
                var store = singletonProp.GetValue(null);
                return store != null && badSpotsProp.GetValue(store) is IEnumerable<IntVec3> spots && spots.Contains(cell);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// A colonist has chosen their next job with nothing under way (their last one ended, or the player gave them one):
    /// after Giddy-Up's own look, Rimfolk's ride out (Integrations/GiddyUpRideOut.cs). Never the game's regular look for a
    /// better job while one is under way. Only with Giddy-Up.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_JobTracker), "DetermineNextJob")]
    [HarmonyAfter("GiddyUp")]
    public static class Patch_RideOutNextJob
    {
        private static bool broken;

        /// <summary>Riding out switched itself off for this session after an error (the self-check says so).</summary>
        internal static bool Broken => broken;

        public static bool Prepare() => GiddyUpMounts.On;

        public static string StandsDownWhy() => GiddyUpMounts.On ? "" : "Giddy-Up isn't loaded";

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Pawn_JobTracker __instance, Pawn ___pawn, ref ThinkResult __result)
        {
            if (broken || __result.Job == null || __instance?.curJob != null) return;
            var pawn = ___pawn;
            if (pawn == null || pawn.Faction != Faction.OfPlayer || pawn.RaceProps == null || !pawn.RaceProps.Humanlike) return;
            try
            {
                GiddyUpRideOut.NextJob(pawn, ref __result);
            }
            catch (Exception e)
            {
                broken = true;
                CouncilLog.ErrorOnce("riding out with Giddy-Up", e);
            }
        }
    }

    /// <summary>
    /// Riding out's pass every 250 ticks at <see cref="Sched.RareRideOut"/> (Integrations/GiddyUpRideOut.cs, with riding
    /// out on): beasts waiting for a rider at work out past the base keep waiting. Nothing saved; a game loaded or begun
    /// starts with no tries or decisions remembered.
    /// </summary>
    public class GameComponent_RideOut : GameComponent
    {
        public GameComponent_RideOut(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            GiddyUpRideOut.Reset();
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (!Sched.Every(now, Sched.RareTicks, Sched.RareRideOut) || !GiddyUpRideOut.On) return;
            using var clock = PassClock.Time(Pass.RareRideOut);
            try
            {
                GiddyUpRideOut.Rare(now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("riding out: keeping beasts waiting", e);
            }
        }
    }

    /// <summary>Dev mode: why a colonist rides out for the job they are doing, or not, and their last decision (Debug actions, "Rimfolk").</summary>
    public static class GiddyUpRideOutDebug
    {
        [LudeonTK.DebugAction("Rimfolk", "Riding out: why a colonist rides or not (log)...", allowedGameStates = LudeonTK.AllowedGameStates.PlayingOnMap)]
        private static void DebugExplain()
        {
            var list = (Find.CurrentMap?.mapPawns.FreeColonistsSpawned ?? new List<Pawn>())
                .Select(p => new FloatMenuOption(p.LabelShort + " (" + (p.CurJobDef?.label ?? "idle") + ")", () =>
                {
                    Log.Message("[Rimfolk] " + GiddyUpRideOut.Explain(p));
                    Messages.Message("Riding out: " + p.LabelShort + "'s reasons are in the log.", MessageTypeDefOf.SilentInput, false);
                })).ToList();
            if (list.Count == 0) list.Add(new FloatMenuOption("(no colonists here)", null));
            Find.WindowStack.Add(new FloatMenu(list));
        }
    }
}
