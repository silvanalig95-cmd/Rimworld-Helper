using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Giddy-Up 2 (the player's "Giddy-Up 2 - Continued", MemeGoddess.GiddyUp), softly: an outsider of Rimfolk's who comes
    /// with a mount (the post rider, a people's porters) sits on it from the moment they come on the map, the way Giddy-Up
    /// brings its own riders in: the mount's tameness and obedience trained by the rider and <c>GiddyUp.MountUtility.GoMount(rider,
    /// animal, GiveJobMethod.Instant)</c>, found by name. Before (0.171.0 to 0.202.1) the rider was given Giddy-Up's
    /// "Mount" job, which the group's own duties could cut off before it finished, so riders often walked beside their
    /// beasts (the player: "porters still frequently appear to come without mounts and being on them"). Where the instant
    /// way can't be found, the "Mount" job by name as before; without Giddy-Up, nobody rides (and porters bring no mounts).
    /// <para>Since 10 November 2026 (the player: "for the message they unmount and then they walk off map without
    /// remounting"): the mount learns tameness too, which Giddy-Up asks before it lets a visitor back on (without it, its own
    /// "mount up as the caravan heads out" never took); setting mail or goods down at the mailbag and taking letters are
    /// jobs Giddy-Up lets a rider do in the saddle (<c>JobDriver_Mounted.SetAllowedJob</c>, by name), so a rider no longer
    /// rides off to park their beast in the colony's pen first; a rider who must go on foot gets off where they stand
    /// (<see cref="GetOff"/>), and the leaving group puts them back in the saddle (Couriers/CarrierLords.cs).</para>
    /// </summary>
    public static class GiddyUpMounts
    {
        private static bool looked;
        private static MethodInfo goMount;
        private static object instant;
        private static JobDef mountJob;
        private static JobDef mountedJob;
        private static HashSet<ushort> mountable;
        private static bool allowedSet;

        /// <summary>Jobs done from the saddle at the mailbag (registered with Giddy-Up once it is found).</summary>
        private static readonly string[] SaddleJobs = { "Council_CourierDrop", "Rimfolk_PostDrop", "Rimfolk_PostTake" };

        private static void Look()
        {
            if (looked) return;
            looked = true;
            try
            {
                var util = GenTypes.GetTypeInAnyAssembly("GiddyUp.MountUtility");
                var method = util?.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => m.Name == "GoMount" && m.GetParameters().Length >= 3
                    && m.GetParameters()[0].ParameterType == typeof(Pawn) && m.GetParameters()[1].ParameterType == typeof(Pawn) && m.GetParameters()[2].ParameterType.IsEnum);
                if (method != null)
                {
                    var kind = method.GetParameters()[2].ParameterType;
                    if (Enum.GetNames(kind).Contains("Instant"))
                    {
                        instant = Enum.Parse(kind, "Instant");
                        goMount = method;
                    }
                }
                var d = DefDatabase<JobDef>.GetNamedSilentFail("Mount");
                if (d?.driverClass?.Namespace != null && d.driverClass.Namespace.StartsWith("GiddyUp", StringComparison.Ordinal)) mountJob = d;
                var m2 = DefDatabase<JobDef>.GetNamedSilentFail("Mounted");
                if (m2?.driverClass?.Namespace != null && m2.driverClass.Namespace.StartsWith("GiddyUp", StringComparison.Ordinal)) mountedJob = m2;
            }
            catch (Exception e)
            {
                goMount = null;
                CouncilLog.ErrorOnce("Giddy-Up: finding its way to mount", e);
            }
            try
            {
                // The beasts the player lets be ridden, in Giddy-Up's settings (a set of race short hashes).
                var settings = GenTypes.GetTypeInAnyAssembly("GiddyUp.ModSettings_GiddyUp");
                var f = settings?.GetField("MountableCache", BindingFlags.Public | BindingFlags.Static);
                if (f?.GetValue(null) is HashSet<ushort> set) mountable = set;
            }
            catch (Exception e)
            {
                mountable = null;
                CouncilLog.ErrorOnce("Giddy-Up: its mount list", e);
            }
            AllowInSaddle();
        }

        /// <summary>Giddy-Up is in the game, so a rider can sit on their mount.</summary>
        public static bool On
        {
            get
            {
                Look();
                return goMount != null || mountJob != null;
            }
        }

        /// <summary>Giddy-Up lets this beast be ridden (its own list where it can be read; else any beast big enough, as its default).</summary>
        public static bool Ridable(ThingDef race)
        {
            Look();
            if (race == null) return false;
            if (mountable == null || mountable.Count == 0) return true;
            return mountable.Contains(race.shortHash);
        }

        /// <summary>
        /// Setting mail or goods down at the mailbag and taking letters are done from the saddle: told to Giddy-Up once, so a
        /// rider heading for the bag with one of them doesn't ride off to park their beast first (in the colony's pen, the
        /// "awkward routine" the player saw). Without the method nothing changes: the leaving group puts them back on.
        /// </summary>
        public static void AllowInSaddle()
        {
            if (allowedSet || !On) return;
            allowedSet = true;
            try
            {
                var driver = GenTypes.GetTypeInAnyAssembly("GiddyUp.Jobs.JobDriver_Mounted");
                var set = driver?.GetMethod("SetAllowedJob", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(JobDef), typeof(bool) }, null);
                if (set == null) return;
                foreach (var name in SaddleJobs)
                {
                    var def = DefDatabase<JobDef>.GetNamedSilentFail(name);
                    // false: Giddy-Up needn't look at the job's target cell as well.
                    if (def != null) set.Invoke(null, new object[] { def, false });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("Giddy-Up: jobs done in the saddle", e);
            }
        }

        /// <summary>The rider is in the saddle of this beast (it carries them: Giddy-Up's "Mounted" job, with them as its rider).</summary>
        public static bool Riding(Pawn rider, Pawn animal)
        {
            Look();
            if (rider == null || animal == null || animal.Dead || !animal.Spawned) return false;
            var job = animal.CurJob;
            return job != null && (mountedJob != null ? job.def == mountedJob : job.def?.defName == "Mounted") && job.targetA.Thing == rider;
        }

        /// <summary>A beast carrying someone (Giddy-Up's "Mounted" job): its job is never cut short by the group.</summary>
        public static bool CarryingRider(Pawn animal)
        {
            Look();
            var job = animal?.CurJob;
            return job != null && (mountedJob != null ? job.def == mountedJob : job.def?.defName == "Mounted");
        }

        /// <summary>The rider is on their way to get on (Giddy-Up's "Mount" job): never cut short by the group either.</summary>
        public static bool Mounting(Pawn rider)
        {
            Look();
            return rider?.CurJob != null && mountJob != null && rider.CurJob.def == mountJob;
        }

        /// <summary>
        /// The rider gets off where they stand, to go on foot (into the colony's stores, to fetch someone): the beast's
        /// "Mounted" job ends, and Giddy-Up's own end of it sets the rider down; the beast then keeps to its group's duty
        /// (waiting by the mailbag). True when they were riding it.
        /// </summary>
        public static bool GetOff(Pawn rider, Pawn animal)
        {
            if (!Riding(rider, animal)) return false;
            try
            {
                animal.jobs.EndCurrentJob(JobCondition.InterruptForced);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("Giddy-Up: getting off", e);
                return false;
            }
        }

        /// <summary>
        /// The rider sits on the animal now, as Giddy-Up's own arriving riders do; else they are given the "Mount" job.
        /// True when the instant way took. Both must be spawned on the same map. The beast learns its tameness and the
        /// rider's commands first, as Giddy-Up's own riders' beasts know them: Giddy-Up asks for tameness before it lets a
        /// visitor back on.
        /// </summary>
        public static bool MountUp(Pawn rider, Pawn animal)
        {
            if (rider == null || animal == null || !rider.Spawned || !animal.Spawned || rider.Map != animal.Map || rider.Dead || animal.Dead) return false;
            if (MountNow(rider, animal)) return true;
            if (mountJob == null) return false;
            try
            {
                Train(rider, animal);
                rider.jobs.StartJob(JobMaker.MakeJob(mountJob, animal), JobCondition.InterruptForced, null, false, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("Giddy-Up: the mount job", e);
            }
            return false;
        }

        /// <summary>
        /// The rider gets on now, the way Giddy-Up sets its arriving riders in the saddle (its instant way only): at the edge
        /// as they come, or beside their beast as they leave. False: not to be had (the caller gives them the walk to it).
        /// </summary>
        public static bool MountNow(Pawn rider, Pawn animal)
        {
            Look();
            if (goMount == null || rider == null || animal == null || !rider.Spawned || !animal.Spawned || rider.Map != animal.Map || rider.Dead || animal.Dead) return false;
            Train(rider, animal);
            try
            {
                var args = goMount.GetParameters().Select(p => p.HasDefaultValue ? p.DefaultValue : null).ToArray();
                args[0] = rider;
                args[1] = animal;
                args[2] = instant;
                goMount.Invoke(null, args);
                return true;
            }
            catch (Exception e)
            {
                goMount = null;
                CouncilLog.ErrorOnce("Giddy-Up: mounting an arriving rider", e);
                return false;
            }
        }

        /// <summary>The rider walks over to the beast and gets on (Giddy-Up's own "Mount" job: the beast stands for them). Null without it.</summary>
        public static Job WalkAndMount(Pawn rider, Pawn animal)
        {
            Look();
            if (mountJob == null || rider == null || animal == null) return null;
            Train(rider, animal);
            var job = JobMaker.MakeJob(mountJob, animal);
            job.count = 1;
            job.locomotionUrgency = Pace.Errand;
            return job;
        }

        /// <summary>The beast knows tameness and its rider's commands (Giddy-Up's own riders' beasts do).</summary>
        private static void Train(Pawn rider, Pawn animal)
        {
            try
            {
                if (animal.playerSettings == null) animal.playerSettings = new Pawn_PlayerSettings(animal);
                if (animal.training == null) return;
                if (TrainableDefOf.Tameness != null && !animal.training.HasLearned(TrainableDefOf.Tameness))
                    animal.training.Train(TrainableDefOf.Tameness, null, true);
                if (TrainableDefOf.Obedience != null && !animal.training.HasLearned(TrainableDefOf.Obedience) && animal.training.CanAssignToTrain(TrainableDefOf.Obedience).Accepted)
                    animal.training.Train(TrainableDefOf.Obedience, rider, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("Giddy-Up: a mount's training", e);
            }
        }
    }
}
