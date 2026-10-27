using System;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A once-in-a-lifetime good memory (a life goal reached) counts in full. Put on its ThoughtDef: the colonist's
    /// standards don't shrink it (CouncilGameComponent.GiveThought), good times don't shorten it (GoodTimes), and it
    /// stays out of the good moods that add up slowly (SmallJoys), so it never pushes the others down either. The reward
    /// strength setting still applies.
    /// </summary>
    public class Milestone : DefModExtension
    {
        public static bool Is(ThoughtDef def) => def != null && def.HasModExtension<Milestone>();

        /// <summary>
        /// On loading a game: such a memory given before this rule, shrunk by standards or shortened for good times,
        /// is set right: the reward strength setting alone, and its full time.
        /// </summary>
        public static void Restore()
        {
            float full = ColonyCouncilMod.Settings.rewardFactor;
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)
            {
                var memories = p.needs?.mood?.thoughts?.memories?.Memories;
                if (memories == null) continue;
                foreach (var m in memories)
                {
                    if (!Is(m.def) || m.def.stages.NullOrEmpty() || m.def.stages[0].baseMoodEffect <= 0f) continue;
                    if (m.moodPowerFactor > 0f && m.moodPowerFactor < full) m.moodPowerFactor = full;
                    if (m.durationTicksOverride >= 0 && m.durationTicksOverride < m.def.DurationTicks) m.durationTicksOverride = -1;
                    if (m is Thought_CouncilSocial s) s.moodTicks = -1;
                }
            }
        }
    }
}
