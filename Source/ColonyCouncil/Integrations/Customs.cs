using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// How the colony's beliefs shape the council itself. An ideoligion that puts a ruler above the vote (Vanilla
    /// Ideology Expanded's aristocratic, royal or god-emperor memes: the "appointed" tag group) doesn't hold elections:
    /// its leader names the officers. Every other colony votes, as before.
    /// </summary>
    public static class CouncilCustoms
    {
        /// <summary>The tag (a TagGroupDef) for beliefs whose leader appoints the council.</summary>
        public const string AppointedTag = "appointed";

        /// <summary>
        /// Who names the officers this time: the leader of the colony's own ideoligion, when its beliefs say so and they
        /// are among <paramref name="voters"/>. Null for an ordinary election.
        /// </summary>
        public static Pawn Appointer(List<Pawn> voters)
        {
            if (!ModsConfig.IdeologyActive || voters == null) return null;
            try
            {
                var ideo = Faction.OfPlayerSilentFail?.ideos?.PrimaryIdeo;
                if (ideo == null) return null;
                Pawn leader = null;
                foreach (var role in ideo.RolesListForReading)
                {
                    if (role?.def != PreceptDefOf.IdeoRole_Leader) continue;
                    leader = role.ChosenPawnSingle();
                    break;
                }
                if (leader == null || !voters.Contains(leader) || leader.Ideo != ideo) return null;
                return PeopleTags.Has(leader, AppointedTag) ? leader : null;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("council customs", e);
                return null;
            }
        }
    }
}
