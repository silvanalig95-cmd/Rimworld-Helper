using System;
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
    /// brings its own riders in: the mount's obedience trained by the rider and <c>GiddyUp.MountUtility.GoMount(rider,
    /// animal, GiveJobMethod.Instant)</c>, found by name. Before (0.171.0 to 0.202.1) the rider was given Giddy-Up's
    /// "Mount" job, which the group's own duties could cut off before it finished, so riders often walked beside their
    /// beasts (the player: "porters still frequently appear to come without mounts and being on them"). Where the instant
    /// way can't be found, the "Mount" job by name as before; without Giddy-Up, nobody rides (and porters bring no mounts).
    /// </summary>
    public static class GiddyUpMounts
    {
        private static bool looked;
        private static MethodInfo goMount;
        private static object instant;
        private static JobDef mountJob;

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
            }
            catch (Exception e)
            {
                goMount = null;
                CouncilLog.ErrorOnce("Giddy-Up: finding its way to mount", e);
            }
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

        /// <summary>
        /// The rider sits on the animal now, as Giddy-Up's own arriving riders do; else they are given the "Mount" job.
        /// True when the instant way took. Both must be spawned on the same map.
        /// </summary>
        public static bool MountUp(Pawn rider, Pawn animal)
        {
            Look();
            if (rider == null || animal == null || !rider.Spawned || !animal.Spawned || rider.Map != animal.Map || rider.Dead || animal.Dead) return false;
            if (goMount != null)
            {
                try
                {
                    // As Giddy-Up does for the riders it brings in: the beast knows its rider's commands first.
                    if (animal.playerSettings == null) animal.playerSettings = new Pawn_PlayerSettings(animal);
                    if (TrainableDefOf.Obedience != null && animal.training != null && animal.training.CanAssignToTrain(TrainableDefOf.Obedience).Accepted)
                        animal.training.Train(TrainableDefOf.Obedience, rider, true);
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
                }
            }
            if (mountJob == null) return false;
            try
            {
                rider.jobs.StartJob(JobMaker.MakeJob(mountJob, animal), JobCondition.InterruptForced, null, false, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("Giddy-Up: the mount job", e);
            }
            return false;
        }
    }
}
