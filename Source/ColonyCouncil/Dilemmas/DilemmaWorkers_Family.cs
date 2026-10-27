using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Who B is to A, for <see cref="DilemmaWorker_Family"/>.</summary>
    public enum FamilyTie
    {
        /// <summary>One of A's parents (by blood or by adoption).</summary>
        Parent,
        /// <summary>One of A's children (by blood or by adoption).</summary>
        Child,
        /// <summary>Anyone related to A by blood.</summary>
        Kin,
        /// <summary>A's parent if one is here, else the grown-up who is fondest of A (a child's guardian in all but name).</summary>
        Guardian
    }

    /// <summary>On a dilemma def with <see cref="DilemmaWorker_Family"/>: who B must be to A (and whether there must be one).</summary>
    public class FamilyRole : DefModExtension
    {
        public FamilyTie b = FamilyTie.Guardian;
        /// <summary>A guardian (when there is no parent) must think at least this well of A.</summary>
        public int minOpinion = 10;
    }

    /// <summary>
    /// A dilemma about a family: A as its roles say (a child, someone old), B their parent, child, kin or guardian, as
    /// the def's <see cref="FamilyRole"/> says (and B's role, if the def gives one, must also fit). Children are only
    /// there with Biotech, so a dilemma whose A is a child simply never comes up without it.
    /// </summary>
    public class DilemmaWorker_Family : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var role = def.GetModExtension<FamilyRole>() ?? new FamilyRole();
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger()).ToList();
            if (pawns.Count < def.minColonists) return false;
            foreach (var a in pawns.Where(p => def.roleA.Allows(p, null)).InRandomOrder())
            {
                var b = Pick(role, a, pawns);
                if (b == null) continue;
                ctx = new DilemmaContext { def = def, map = map, a = a, b = b, count = def.thingCount };
                if (!Extra(ctx)) continue;
                return true;
            }
            ctx = null;
            return false;
        }

        private Pawn Pick(FamilyRole role, Pawn a, List<Pawn> pawns)
        {
            if (a?.relations == null) return null;
            bool Ok(Pawn b) => b != null && b != a && pawns.Contains(b) && (def.roleB == null || def.roleB.Allows(b, a)) && b.Spawned && !b.Downed && !b.InMentalState;
            // Parents and children by adoption count as by blood (Social/FamilyGuardians.cs).
            var parents = Family.ParentsOf(a).Where(x => !x.Dead).ToList();
            switch (role.b)
            {
                case FamilyTie.Parent:
                    return parents.Where(Ok).RandomElementWithFallback();
                case FamilyTie.Child:
                    return Family.ChildrenOf(a).Where(c => !c.Dead && Ok(c)).RandomElementWithFallback();
                case FamilyTie.Kin:
                    return pawns.Where(b => Ok(b) && ArcUtil.Kin(a, b)).RandomElementWithFallback();
                default:
                {
                    var parent = parents.Where(Ok).OrderByDescending(p => ArcUtil.Opinion(a, p)).FirstOrDefault();
                    if (parent != null) return parent;
                    return pawns.Where(b => Ok(b) && !b.DevelopmentalStage.Juvenile() && Flavor.HumanAge(b) >= 18f && ArcUtil.Opinion(b, a) >= role.minOpinion)
                        .OrderByDescending(b => ArcUtil.Opinion(b, a) + ArcUtil.Opinion(a, b)).FirstOrDefault();
                }
            }
        }
    }
}
