using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A clean sweep put to work: while it's in force, every colonist who can clean puts cleaning before all other work
    /// except what keeps people alive (<see cref="Essential"/>), which still comes first, as do sleep, meals and washing,
    /// which aren't work. Those not set to clean clean too. Read each time a colonist looks for work
    /// (Patch_CleanSweepWork); with the deeper game hooks off, the work order is the player's own.
    /// </summary>
    public static class CleanSweepWork
    {
        /// <summary>Work that still comes before the scrubbing: fires, the sick and wounded, children, prisoners, meals.</summary>
        private static readonly HashSet<string> Essential = new HashSet<string>
        {
            "Firefighter", "Patient", "Doctor", "PatientBedRest", "Childcare", "BasicWorker", "Warden", "Cooking",
            // Allow Tool's urgent hauling: what the player marked to be carried in at once.
            "HaulingUrgent"
        };

        private static DecreeDef def;
        private static bool looked;
        private static int checkedTick = -1;
        private static bool on;
        // One list per colonist, refilled on each look for work, so the sweep allocates nothing while it lasts.
        private static readonly Dictionary<Pawn, List<WorkGiver>> lists = new Dictionary<Pawn, List<WorkGiver>>();
        private static readonly List<WorkGiver> cleaners = new List<WorkGiver>();

        /// <summary>Whether a clean sweep is in force (asked at most once a tick).</summary>
        public static bool On
        {
            get
            {
                var ticks = Find.TickManager;
                if (ticks == null) return false;
                int now = ticks.TicksGame;
                if (now == checkedTick) return on;
                checkedTick = now;
                if (!looked)
                {
                    looked = true;
                    def = DefDatabase<DecreeDef>.GetNamedSilentFail("Council_Decree_CleanSweep");
                }
                on = def != null && (CouncilGameComponent.Instance?.decrees?.IsActive(def) ?? false);
                if (!on && lists.Count > 0) lists.Clear();
                return on;
            }
        }

        /// <summary>
        /// The work this colonist looks through, in order: the game's own list, or during a clean sweep the essentials
        /// first, then cleaning, then the rest as the player set it.
        /// </summary>
        public static List<WorkGiver> WorkGivers(Pawn_WorkSettings settings, Pawn pawn)
        {
            var source = settings.WorkGiversInOrderNormal;
            if (!RimfolkPatches.On || source == null) return source;
            try
            {
                if (!On) return source;
                var cleaning = WorkTypeDefOf.Cleaning;
                if (pawn == null || cleaning == null || !pawn.RaceProps.Humanlike || !pawn.IsColonist || pawn.IsQuestLodger()
                    || pawn.WorkTypeIsDisabled(cleaning)) return source;
                if (!lists.TryGetValue(pawn, out var list)) lists[pawn] = list = new List<WorkGiver>(source.Count + 4);
                cleaners.Clear();
                foreach (var gd in cleaning.workGiversByPriority)
                    if (gd?.Worker != null && !pawn.WorkTagIsDisabled(gd.workTags)) cleaners.Add(gd.Worker);
                DecreeMath.SweepOrder(source, cleaners, g => g?.def?.workType != null && Essential.Contains(g.def.workType.defName),
                    g => g?.def?.workType == cleaning, list);
                return list;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("clean sweep work", e);
                return source;
            }
        }
    }
}
