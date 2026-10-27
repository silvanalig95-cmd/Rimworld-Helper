using System;

namespace ColonyCouncil
{
    /// <summary>Where a relative is, for someone's family tree (Social/Family.cs).</summary>
    public enum KinWhere
    {
        /// <summary>One of the colony, on the same map.</summary>
        Here,
        /// <summary>One of the colony, at another of its settlements.</summary>
        Elsewhere,
        /// <summary>One of the colony, away with a caravan.</summary>
        Away,
        /// <summary>A prisoner of the colony.</summary>
        Prisoner,
        /// <summary>A slave of the colony.</summary>
        Slave,
        /// <summary>On the colony's map as a guest or a visitor.</summary>
        Visiting,
        /// <summary>Carried off by another people.</summary>
        Taken,
        /// <summary>Living with another people.</summary>
        WithFaction,
        /// <summary>Out in the world, with no people of their own.</summary>
        World,
        /// <summary>Dead, and buried in one of the colony's graves.</summary>
        Buried,
        Dead
    }

    /// <summary>
    /// The family as Rimfolk sees it (Social/Family.cs): which of the game's relations are family by marriage (a
    /// spouse's parents, a child's spouse, a step-parent or step-child), which come first on a family tree, and where a
    /// relative is from what's known of them. No game types, so Tools/Tests checks it.
    /// </summary>
    public static class FamilyMath
    {
        /// <summary>How many relatives a family tree names one by one; the rest are counted.</summary>
        public const int TreeShown = 12;

        /// <summary>Family by marriage or by a parent's partner (the game's relations by their defName).</summary>
        public static bool ByMarriage(string relation) =>
            relation == "ParentInLaw" || relation == "ChildInLaw" || relation == "Stepparent" || relation == "Stepchild";

        /// <summary>A partner of theirs now (not one who was).</summary>
        public static bool Partner(string relation) => relation == "Spouse" || relation == "Fiance" || relation == "Lover";

        /// <summary>
        /// Where a relation comes on a family tree, closest first: partner, parents, brothers and sisters, children,
        /// grandparents, grandchildren, step-family, in-laws, great-grandparents and great-grandchildren. The rest
        /// (uncles, cousins and further) aren't named one by one: -1.
        /// </summary>
        public static int TreeRank(string relation)
        {
            switch (relation)
            {
                case "Spouse": return 0;
                case "Fiance": return 1;
                case "Lover": return 2;
                case "Parent": return 3;
                case "Sibling": return 4;
                case "HalfSibling": return 5;
                case "Child": return 6;
                case "Grandparent": return 7;
                case "Grandchild": return 8;
                case "Stepparent": return 9;
                case "Stepchild": return 10;
                case "ParentInLaw": return 11;
                case "ChildInLaw": return 12;
                case "GreatGrandparent": return 13;
                case "GreatGrandchild": return 14;
                default: return -1;
            }
        }

        /// <summary>Where a relative is, from what's known of them; the first that holds, in this order.</summary>
        public static KinWhere Where(bool dead, bool buriedHere, bool ofColony, bool sameMap, bool inCaravan, bool prisoner, bool slave,
            bool onOurMap, bool taken, bool hasFaction)
        {
            if (dead) return buriedHere ? KinWhere.Buried : KinWhere.Dead;
            if (prisoner) return KinWhere.Prisoner;
            if (slave) return KinWhere.Slave;
            if (ofColony) return inCaravan ? KinWhere.Away : sameMap ? KinWhere.Here : KinWhere.Elsewhere;
            if (taken) return KinWhere.Taken;
            if (onOurMap) return KinWhere.Visiting;
            return hasFaction ? KinWhere.WithFaction : KinWhere.World;
        }

        /// <summary>
        /// A reunion is owed when someone joined in the last day and a relative of theirs has been one of the colony
        /// for longer: the relative goes to find them.
        /// </summary>
        public static bool ReunionDue(float newcomerDays, float relativeDays) => newcomerDays < 1f && relativeDays >= 1f;
    }
}
