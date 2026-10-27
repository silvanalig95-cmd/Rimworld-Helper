using ColonyCouncil;

/// <summary>
/// Family beyond blood (Source/ColonyCouncil/Social/FamilyMath.cs): which relations are family by marriage, the order of
/// a family tree, where a relative is from what's known of them, and when a reunion is owed.
/// </summary>
public static class FamilyTests
{
    public static int Run(Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "family: " + what);
        }

        // ---------- Family by marriage ----------
        foreach (var r in new[] { "ParentInLaw", "ChildInLaw", "Stepparent", "Stepchild" })
            Check(FamilyMath.ByMarriage(r), $"{r} is family by marriage");
        foreach (var r in new[] { "Parent", "Child", "Sibling", "Spouse", "ExSpouse", "Lover", "Cousin", "Bond", "", null })
            Check(!FamilyMath.ByMarriage(r), $"{r ?? "nothing"} isn't family by marriage");
        Check(FamilyMath.Partner("Spouse") && FamilyMath.Partner("Fiance") && FamilyMath.Partner("Lover") && !FamilyMath.Partner("ExSpouse") && !FamilyMath.Partner("ExLover"),
            "a partner now, not one who was");

        // ---------- The family tree's order ----------
        var order = new[] { "Spouse", "Parent", "Sibling", "Child", "Grandparent", "Grandchild", "Stepparent", "ParentInLaw", "GreatGrandparent" };
        bool ordered = true;
        for (int i = 1; i < order.Length; i++)
            if (FamilyMath.TreeRank(order[i - 1]) >= FamilyMath.TreeRank(order[i])) ordered = false;
        Check(ordered, "partner first, then parents, siblings, children, grandparents, grandchildren, step-family, in-laws, great-grandparents");
        foreach (var r in new[] { "UncleOrAunt", "Cousin", "NephewOrNiece", "ExSpouse", "Bond", "Kin" })
            Check(FamilyMath.TreeRank(r) < 0, $"{r} isn't named one by one");
        Check(FamilyMath.TreeShown >= 8 && FamilyMath.TreeShown <= 16, "a tree names a dozen or so, and counts the rest");

        // ---------- Where they are ----------
        KinWhere W(bool dead = false, bool buried = false, bool ours = false, bool sameMap = false, bool caravan = false, bool prisoner = false,
            bool slave = false, bool onMap = false, bool taken = false, bool faction = false) =>
            FamilyMath.Where(dead, buried, ours, sameMap, caravan, prisoner, slave, onMap, taken, faction);
        Check(W(dead: true, buried: true, ours: true) == KinWhere.Buried && W(dead: true, faction: true) == KinWhere.Dead, "the dead: buried here, or dead");
        Check(W(ours: true, sameMap: true) == KinWhere.Here && W(ours: true) == KinWhere.Elsewhere && W(ours: true, sameMap: true, caravan: true) == KinWhere.Away,
            "one of the colony: here, at another settlement, or away with a caravan");
        Check(W(prisoner: true, onMap: true, faction: true) == KinWhere.Prisoner && W(slave: true, onMap: true) == KinWhere.Slave, "in the colony's hands");
        Check(W(taken: true, faction: true) == KinWhere.Taken, "carried off counts before the people who hold them");
        Check(W(onMap: true, faction: true) == KinWhere.Visiting && W(faction: true) == KinWhere.WithFaction && W() == KinWhere.World,
            "visiting, with another people, or out in the world");

        // ---------- Reunions ----------
        Check(FamilyMath.ReunionDue(0.3f, 40f), "a relative here for weeks meets one who came today");
        Check(!FamilyMath.ReunionDue(0.3f, 0.5f), "two who came together (or the founders on the first day) aren't reunited");
        Check(!FamilyMath.ReunionDue(2f, 40f), "nobody is met as a newcomer after their first day");
        return cases;
    }
}
