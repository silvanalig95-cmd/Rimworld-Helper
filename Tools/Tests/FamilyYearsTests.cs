using ColonyCouncil;

/// <summary>
/// Families across the years (Source/ColonyCouncil/Social/FamilyYearsMath.cs): years spent in cryptosleep, how many
/// generations a relative is above or below by the game's relation, ages the wrong way round in the direct line only,
/// and when a pair may speak of it again. The family from the player's save is one of the cases.
/// </summary>
public static class FamilyYearsTests
{
    public static int Run(Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "family years: " + what);
        }

        // ---------- Years asleep ----------
        Check(FamilyYearsMath.Slept(57, 81) == 24, "57 years lived of 81: 24 asleep");
        Check(FamilyYearsMath.Slept(40, 39) == 0, "never fewer than none asleep");
        Check(!FamilyYearsMath.HasSlept(30, 31) && FamilyYearsMath.HasSlept(30, 32), "a year in a pod isn't worth a word, two are");

        // ---------- Generations in the direct line ----------
        var pairs = new[] { ("Parent", "Child"), ("Grandparent", "Grandchild"), ("GreatGrandparent", "GreatGrandchild") };
        foreach (var (up, down) in pairs)
            Check(FamilyYearsMath.Generations(up) > 0 && FamilyYearsMath.Generations(up) == -FamilyYearsMath.Generations(down),
                $"{up} and {down} are the same distance up and down");
        Check(FamilyYearsMath.Generations("Parent") == 1 && FamilyYearsMath.Generations("Grandparent") == 2, "a parent one up, a grandparent two");
        foreach (var side in new[] { "Sibling", "HalfSibling", "Cousin", "UncleOrAunt", "NephewOrNiece", "Spouse", "ParentInLaw", "Stepparent", "", null })
            Check(FamilyYearsMath.Generations(side) == 0, $"{side ?? "nothing"} isn't in the direct line");

        // ---------- The player's family: Toni 36 (162 by the calendar), her daughter Morgan 31 (126), Morgan's son Hector 57 (81), Toni's son Harper 21 (54) ----------
        Check(FamilyYearsMath.UpsideDown(FamilyYearsMath.Generations("Parent"), 57, 31), "Hector's mother Morgan is younger than him: the wrong way round");
        Check(FamilyYearsMath.UpsideDown(FamilyYearsMath.Generations("Grandchild"), 36, 57), "Toni's grandson Hector is older than her: the wrong way round");
        Check(FamilyYearsMath.UpsideDown(FamilyYearsMath.Generations("Grandparent"), 57, 36), "and the same from Hector's side");
        Check(!FamilyYearsMath.UpsideDown(FamilyYearsMath.Generations("UncleOrAunt"), 57, 21), "an uncle younger than his nephew is only a late brother, not worth a word");
        Check(!FamilyYearsMath.UpsideDown(FamilyYearsMath.Generations("Child"), 36, 31) && !FamilyYearsMath.UpsideDown(FamilyYearsMath.Generations("Parent"), 31, 36),
            "Toni and her daughter Morgan are the right way round");
        Check(FamilyYearsMath.Slept(31, 126) == 95 && FamilyYearsMath.Slept(36, 162) == 126, "Morgan slept 95 years, Toni 126");

        // ---------- Either side says the same ----------
        bool symmetric = true, ordinary = true;
        foreach (int g in new[] { -3, -2, -1, 1, 2, 3 })
            for (int a = 0; a <= 90; a += 3)
                for (int b = 0; b <= 90; b += 3)
                {
                    if (FamilyYearsMath.UpsideDown(g, a, b) != FamilyYearsMath.UpsideDown(-g, b, a)) symmetric = false;
                    // Someone above in the line who has lived at least as long is never the wrong way round.
                    if (g > 0 && b >= a && FamilyYearsMath.UpsideDown(g, a, b)) ordinary = false;
                    if (g < 0 && b <= a && FamilyYearsMath.UpsideDown(g, a, b)) ordinary = false;
                }
        Check(symmetric, "the wrong way round from one side is the wrong way round from the other");
        Check(ordinary, "a family the right way round never counts");
        Check(!FamilyYearsMath.UpsideDown(1, 40, 40) && !FamilyYearsMath.UpsideDown(0, 80, 20), "the same age, or no line between them, never counts");

        // ---------- Who speaks as what ----------
        Check(FamilyYearsMath.Side(1) == "younger" && FamilyYearsMath.Side(2) == "younger", "speaking to a mother or grandmother: the one below");
        Check(FamilyYearsMath.Side(-1) == "elder" && FamilyYearsMath.Side(-2) == "elder", "speaking to a son or grandson: the one above");

        // ---------- When they come back to it ----------
        const int day = 60000;
        Check(FamilyYearsMath.RemarkDue(-1, 5 * day, day), "never spoken of: due");
        Check(!FamilyYearsMath.RemarkDue(0, 9 * day, day) && FamilyYearsMath.RemarkDue(0, 10 * day, day), "ten days apart at the soonest");
        Check(FamilyYearsMath.RemarkDue(20 * day, 5 * day, day), "a time from the future (another save) doesn't hold it back");
        Check(FamilyYearsMath.PairKey(12, 7) == FamilyYearsMath.PairKey(7, 12), "one key a pair, whichever way round");
        Check(FamilyYearsMath.RemarkChance > 0f && FamilyYearsMath.RemarkChance < 0.5f, "a word now and then, not every day");
        return cases;
    }
}
