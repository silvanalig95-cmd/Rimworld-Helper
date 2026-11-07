using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    public static class NightWalkDefOf
    {
        public static JobDef Rimfolk_Sleepwalk;
        public static JobDef Rimfolk_RestlessWalk;

        static NightWalkDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(NightWalkDefOf));
    }

    /// <summary>
    /// Harm, part 5: walks at night (Harm/HarmFive.cs is the hub; each colonist's hour), and the game's predators that a
    /// lone walker or watcher outside the walls can draw.
    /// <list type="bullet">
    /// <item>Sleepwalking after a nightmare (3.12.1): one nightmare in 15 that left someone shaken walks them out of bed
    /// in the small hours of a later night, across the yard in their nightclothes; in the cold (freezing out and colder
    /// than comfortable) it ends in frostnip, otherwise in a stumble half the time (a bruise of 1 to 3, the mildest), and
    /// whoever of the colony is up leads them back. A door bolted for the night (the sleepwalker's answer) keeps them in,
    /// and a roommate wakes them first. With the dreams.</item>
    /// <item>A restless walk at dusk (3.3.3): restlessness at its height (their strongest feeling, at 1.6 times its
    /// threshold) walks them out alone at dusk to the edge of the colony's ground, about once a season; a friend up and
    /// about goes along (the walk becomes company). Beyond the walls (the edge of the map reachable without a closed door)
    /// at nightfall, with a hungry predator in reach, one lone walk in four draws it. With the heart.</item>
    /// </list>
    /// A predator comes by the game's own hunt, its own warning told by the game; it stops once they're down (it doesn't
    /// kill the downed), the cap counts it as a hurt, and the colony can drive it off.
    /// </summary>
    public static class NightWalks
    {
        private static int Now => Find.TickManager.TicksGame;

        /// <summary>Dev mode: the next walk's predator comes for them, past its rate (never past the cap).</summary>
        public static bool DebugPredator;

        public static void Reset()
        {
            rolled.Clear();
            dusk.Clear();
            alone.Clear();
        }

        public static void Hour(Pawn p, int now)
        {
            if (!Harm.On || !HarmFour.Ours(p)) return;
            Sleep(p, now);
            Dusk(p, now);
        }

        // ================= Predators =================

        /// <summary>Outside the walls: the edge of the map can be reached from here without passing a closed door.</summary>
        public static bool OutsideWalls(Map map, IntVec3 c)
        {
            try
            {
                return map != null && c.IsValid && c.InBounds(map) && map.reachability.CanReachMapEdge(c, TraverseParms.For(TraverseMode.NoPassClosedDoors, Danger.Deadly));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>A wild predator in reach that is hungry and would take them as prey (the game's own reckoning), nearest first; null: none.</summary>
        public static Pawn PredatorFor(Pawn prey)
        {
            if (prey?.Map == null) return null;
            Pawn best = null;
            float bestD = float.MaxValue;
            foreach (var a in prey.Map.mapPawns.AllPawnsSpawned)
            {
                if (a == null || a.Faction != null || a.RaceProps == null || !a.RaceProps.predator || a.Dead || a.Downed || a.InMentalState || !RestUtility.Awake(a)) continue;
                if (!a.Position.InHorDistOf(prey.Position, HarmFiveMath.PredatorRange)) continue;
                var food = a.needs?.food;
                if (food != null && food.CurLevelPercentage > HarmFiveMath.PredatorHungry) continue;
                float d = a.Position.DistanceToSquared(prey.Position);
                if (d >= bestD) continue;
                try
                {
                    if (!FoodUtility.IsAcceptablePreyFor(a, prey) || !a.CanReach(prey, PathEndMode.Touch, Danger.Deadly)) continue;
                }
                catch
                {
                    continue;
                }
                best = a;
                bestD = d;
            }
            return best;
        }

        /// <summary>The predator's own hunt on them, stopping once they're down; counted as their hurt. False when it didn't start.</summary>
        public static bool Hunt(Pawn predator, Pawn prey, HarmKind kind)
        {
            try
            {
                if (predator?.jobs == null || prey == null) return false;
                var job = JobMaker.MakeJob(JobDefOf.PredatorHunt, prey);
                job.killIncappedTarget = false;
                predator.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                if (predator.CurJobDef != JobDefOf.PredatorHunt) return false;
                Harm.Note(prey, kind);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm five: a predator's hunt", e);
                return false;
            }
        }

        // ================= Sleepwalking after a nightmare (3.12.1) =================

        private static HediffDef shaken;

        /// <summary>The nightmares already looked at (the shaken hediff's loadID): one roll each, the same after a reload.</summary>
        private static readonly HashSet<int> rolled = new HashSet<int>();

        private static void Sleep(Pawn p, int now)
        {
            if (p.DevelopmentalStage.Baby() || RestUtility.Awake(p) || !p.InBed() || p.Map.dangerWatcher.DangerRating != StoryDanger.None) return;
            if (!HarmFiveMath.SmallHours(GenLocalDate.HourOfDay(p.Map))) return;
            shaken ??= HarmFour.Def("Council_Dream_Shaken");
            var h = shaken != null ? p.health.hediffSet.GetFirstHediffOfDef(shaken) : null;
            if (h == null || !Harm.SystemOn(HarmKinds.Sleepwalk.system) || !rolled.Add(h.loadID)) return;
            if (rolled.Count > 500) rolled.Clear();
            // The nightmare's own roll, the same after a reload; the cap asked now so a walk only comes where its hurt could.
            if (!Harm.May(p, HurtClass.Mild) || Rand.ValueSeeded(h.loadID * 7919 + 59) >= Harm.Chance(HarmKinds.Sleepwalk, HarmFiveMath.SleepwalkChance)) return;
            Sleepwalk(p);
        }

        /// <summary>The sleepwalker stirs: a bolted door keeps them in, a roommate wakes them, or they walk out to the yard.</summary>
        private static void Sleepwalk(Pawn p)
        {
            var map = p.Map;
            if (OnMapTimed.Instance?.kept.Any(k => k != null && k.kind == TimedKind.Bolt && k.pawn == p && k.on) ?? false)
            {
                // apart: the sleepwalker at their own bolted door, to nobody.
                HarmFour.Say(p, "Rimfolk_Pool_SleepwalkDoor");
                return;
            }
            var room = RestUtility.CurrentBed(p)?.GetRoom();
            Pawn mate = null;
            if (room != null && !room.PsychologicallyOutdoors)
                foreach (var bed in room.ContainedBeds)
                    foreach (var o in bed.CurOccupants)
                        if (o != p && HarmFour.Ours(o) && !o.DevelopmentalStage.Baby())
                            mate = o;
            if (mate != null)
            {
                // together: the two share the bedroom; the roommate wakes at the stirring and settles them.
                mate.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                HarmFour.Say(mate, "Rimfolk_Pool_SleepwalkRoommate", p);
                return;
            }
            if (!CellFinder.TryFindRandomCellNear(p.Position, map, 18, c => c.Standable(map) && !c.Roofed(map) && (c.GetRoom(map)?.PsychologicallyOutdoors ?? true)
                    && !c.IsForbidden(p) && p.CanReach(c, PathEndMode.OnCell, Danger.Some), out var cell)) return;
            var job = JobMaker.MakeJob(NightWalkDefOf.Rimfolk_Sleepwalk, cell);
            job.count = 600;
            job.locomotionUrgency = LocomotionUrgency.Amble;
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>The sleepwalk's end, out in the yard: the cold or a stumble, then someone up leads them back to bed.</summary>
        private static void SleepwalkEnded(Pawn p)
        {
            float outdoor = p.Map.mapTemperature.OutdoorTemp;
            bool chilled = p.AmbientTemperature < p.ComfortableTemperatureRange().min;
            int end = HarmFiveMath.SleepwalkEnds(outdoor, chilled, Rand.Value);
            if (end == 2)
            {
                var frostnip = HarmFour.Def("Council_Ail_Frostnip");
                var info = frostnip != null ? AilmentTracker.InfoOf(frostnip) : null;
                var h = frostnip != null && !p.health.hediffSet.HasHediff(frostnip) && Harm.SystemOn(HarmKinds.Sleepwalk.system) && AilmentTracker.On
                    ? Harm.Ail(p, frostnip, info?.startSeverity.RandomInRange ?? 0.3f, HarmKinds.Sleepwalk, HurtClass.Mild) : null;
                if (h != null)
                {
                    if (info != null) AilmentTracker.Instance?.Notify_Caught(p, h);
                    HarmFour.Say(p, "Rimfolk_Pool_SleepwalkHurt", null, "frostbit");
                    HarmFour.TellHurt(p, "CC_HarmFive_SleepwalkFrost", p.Named("PAWN"), AilmentTracker.NameOf(frostnip).Named("ILLNESS"), (AilmentTracker.DoctorsNote(frostnip) ?? "").Named("NOTE"));
                }
                else HarmFour.Say(p, "Rimfolk_Pool_SleepwalkWakes");
            }
            else if (end == 1 && Harm.Injure(p, HurtKind.Bruise, HurtWhere.Leg, HarmFiveMath.StumbleLow, HarmFiveMath.StumbleHigh, HarmKinds.Sleepwalk, HurtClass.Mild) != null)
            {
                HarmFour.SitDown(p, 200);
                HarmFour.Say(p, "Rimfolk_Pool_SleepwalkHurt", null, "stumbled");
                HarmFour.Mote(p, "CC_HarmFive_Mote_Stumble");
            }
            else HarmFour.Say(p, "Rimfolk_Pool_SleepwalkWakes");
            // Someone of the colony up and near leads them back.
            var by = p.Map.mapPawns.FreeColonistsSpawned.Where(o => o != p && Together.Can(o, p) && !o.DevelopmentalStage.Baby()).FirstOrDefault();
            // together: Together.Can, the two in talking range in the yard.
            if (by != null) HarmFour.Say(by, "Rimfolk_Pool_SleepwalkLed", p);
            var bed = p.ownership?.OwnedBed;
            if (bed != null && bed.Spawned && bed.Map == p.Map) p.jobs?.jobQueue?.EnqueueFirst(JobMaker.MakeJob(JobDefOf.LayDown, bed));
        }

        /// <summary>Dev mode: this colonist sleepwalks now (in bed or not), past the nightmare and its rate.</summary>
        public static string DebugSleepwalk(Pawn p)
        {
            Sleepwalk(p);
            return p.CurJobDef == NightWalkDefOf.Rimfolk_Sleepwalk ? p.LabelShort + " walks out across the yard in their sleep."
                : p.LabelShort + " doesn't walk (a bolted door, a roommate who woke them, or nowhere outdoors in reach): their line says which.";
        }

        // ================= A restless walk at dusk (3.3.3) =================

        /// <summary>A colonist's thingIDNumber: the day their dusk was looked at.</summary>
        private static readonly Dictionary<int, int> dusk = new Dictionary<int, int>();
        /// <summary>Walkers out alone now (thingIDNumber): the walk's end looks for a predator.</summary>
        private static readonly HashSet<int> alone = new HashSet<int>();

        private static FeelingDef restless;
        private static bool debugDusk;

        /// <summary>Restlessness at its height: their strongest feeling now, at 1.6 times its threshold.</summary>
        private static bool RestlessNow(Pawn p)
        {
            restless ??= DefDatabase<FeelingDef>.GetNamedSilentFail("Council_Feeling_Restless");
            var h = Heart.Of(p);
            if (restless == null || h?.feelings == null || h.feelings.Count == 0) return false;
            var f = h.feelings[0];
            return f.def == restless && HarmFiveMath.RestlessAtHeight(true, f.intensity, restless.threshold);
        }

        private static void Dusk(Pawn p, int now)
        {
            if (HarmFour.Child(p) || !Together.Able(p) || !Together.FreeTime(p) || p.Map.dangerWatcher.DangerRating != StoryDanger.None) return;
            bool debug = debugDusk;
            if (!debug && !HarmFiveMath.Dusk(GenLocalDate.HourOfDay(p.Map))) return;
            int day = now / HarmMath.DayTicks;
            if (!debug && dusk.TryGetValue(p.thingIDNumber, out int d) && d == day) return;
            if (dusk.Count > 200) dusk.Clear();
            dusk[p.thingIDNumber] = day;
            if (!Harm.SystemOn(HarmKinds.RestlessWalk.system) || !debug && !RestlessNow(p)) return;
            // About once a season: the dusk's own roll, the same after a reload.
            if (!debug && Rand.ValueSeeded(p.thingIDNumber * 31 + day * 7) >= HarmFiveMath.RestlessWalkChance) return;
            debugDusk = false;
            var route = PlayVigils.Route(p.Map, p, 0, 1);
            if (route.Count == 0) return;
            var cell = route.RandomElement();
            var friend = p.Map.mapPawns.FreeColonistsSpawned.FirstOrDefault(o => o != p && !HarmFour.Child(o) && Together.Can(p, o) && Together.FreeTime(o)
                                                                                 && (p.relations?.OpinionOf(o) ?? 0) >= 20 && (o.relations?.OpinionOf(p) ?? 0) >= 20);
            if (!Walk(p, cell)) return;
            if (friend != null && CellFinder.TryFindRandomCellNear(cell, p.Map, 2, c => c.Standable(p.Map) && friend.CanReach(c, PathEndMode.OnCell, Danger.Some), out var beside) && Walk(friend, beside))
            {
                // together: the friend is beside them (Together.Can) as they set out, and walks with them.
                HarmFour.Say(friend, "Rimfolk_Pool_RestlessFriend", p);
                return;
            }
            alone.Add(p.thingIDNumber);
            // apart: said to nobody, setting out alone.
            HarmFour.Say(p, "Rimfolk_Pool_RestlessWalk");
        }

        private static bool Walk(Pawn p, IntVec3 cell)
        {
            if (!p.CanReach(cell, PathEndMode.OnCell, Danger.Some)) return false;
            var job = JobMaker.MakeJob(NightWalkDefOf.Rimfolk_RestlessWalk, cell);
            job.count = HarmFiveMath.WalkTicks;
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            return p.CurJobDef == NightWalkDefOf.Rimfolk_RestlessWalk;
        }

        /// <summary>At the edge of the colony's ground as night falls, alone and outside the walls: a hungry predator in reach may come.</summary>
        private static void RestlessArrived(Pawn p)
        {
            if (!alone.Remove(p.thingIDNumber)) return;
            bool outside = OutsideWalls(p.Map, p.Position);
            var predator = outside ? PredatorFor(p) : null;
            bool debug = DebugPredator && predator != null;
            float chance = HarmFiveMath.RestlessPredator(true, outside, predator != null);
            if (!debug && chance <= 0f) return;
            DebugPredator = false;
            if (!(debug ? Harm.May(p, HurtClass.Injury) : Harm.Roll(p, HarmKinds.RestlessWalk, chance, HurtClass.Injury))) return;
            if (!Hunt(predator, p, HarmKinds.RestlessWalk)) return;
            HarmFour.Say(p, "Rimfolk_Pool_SomethingOut");
            p.jobs?.EndCurrentJob(JobCondition.InterruptForced);
        }

        /// <summary>Dev mode: this colonist takes the restless walk now, alone (past the feeling and its rate).</summary>
        public static string DebugRestless(Pawn p)
        {
            debugDusk = true;
            Dusk(p, Now);
            debugDusk = false;
            return p.CurJobDef == NightWalkDefOf.Rimfolk_RestlessWalk ? p.LabelShort + " walks out to the edge of the colony's ground" + (alone.Contains(p.thingIDNumber) ? " alone." : " with a friend.")
                : p.LabelShort + " can't go now (not free, danger, or nowhere to walk to).";
        }

        // ================= The walk's driver =================

        /// <summary>The walk reached its place (JobDriver_HarmWalk): a restless walker alone looks out into the dark.</summary>
        public static void Arrived(Pawn p, JobDef def)
        {
            try
            {
                if (def == NightWalkDefOf.Rimfolk_RestlessWalk) RestlessArrived(p);
                else HarmSix.Arrived(p, def);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm five: a walk's end", e);
            }
        }

        /// <summary>The walk is over (JobDriver_HarmWalk): a sleepwalker's end.</summary>
        public static void Ended(Pawn p, JobDef def)
        {
            try
            {
                if (def == NightWalkDefOf.Rimfolk_Sleepwalk && p.Spawned) SleepwalkEnded(p);
                else if (p.Spawned) HarmSix.Ended(p, def);
                alone.Remove(p.thingIDNumber);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm five: a walk's end", e);
            }
        }
    }

    /// <summary>A walk at night: out to its place (target A), a while there (job.count ticks), and its end (NightWalks).</summary>
    public class JobDriver_HarmWalk : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            var go = Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            yield return go;
            var stand = ToilMaker.MakeToil("HarmWalkStand");
            stand.initAction = () => NightWalks.Arrived(pawn, job.def);
            stand.defaultCompleteMode = ToilCompleteMode.Delay;
            stand.defaultDuration = job.count > 0 ? job.count : 600;
            stand.handlingFacing = true;
            yield return stand;
            var end = ToilMaker.MakeToil("HarmWalkEnd");
            end.initAction = () => NightWalks.Ended(pawn, job.def);
            end.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return end;
        }
    }
}
