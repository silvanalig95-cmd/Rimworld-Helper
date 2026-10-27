using ColonyCouncil;

/// <summary>
/// The family's life together (Source/ColonyCouncil/Social/FamilyLifeMath.cs): who is glad of a new couple and who
/// doubts it, whether a child on the way is wanted, which birthdays the family marks, the family names offered for a
/// baby, and who a child was named after.
/// </summary>
public static class FamilyLifeTests
{
    public static int Run(Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "family life: " + what);
        }

        // ---------- How a new couple is taken ----------
        Check(FamilyLifeMath.Take(true, 0, 0) == MatchTake.Glad && FamilyLifeMath.Take(true, -50, 10) == MatchTake.Glad, "family are glad, whatever they think of their own");
        Check(FamilyLifeMath.Take(true, 80, FamilyLifeMath.DoubtOpinion) == MatchTake.Doubt && FamilyLifeMath.Take(true, 80, -60) == MatchTake.Doubt, "family doubt one they can't stand");
        Check(FamilyLifeMath.Take(true, 80, FamilyLifeMath.DoubtOpinion + 1) == MatchTake.Glad, "a little dislike isn't doubt");
        Check(FamilyLifeMath.Take(false, FamilyLifeMath.FriendOpinion, 0) == MatchTake.Glad, "a good friend is glad");
        Check(FamilyLifeMath.Take(false, FamilyLifeMath.FriendOpinion - 1, 50) == MatchTake.None, "an acquaintance says nothing");
        Check(FamilyLifeMath.Take(false, 90, -1) == MatchTake.None, "a friend who dislikes the partner keeps quiet");
        for (int o = -100; o <= 100; o += 10)
            for (int q = -100; q <= 100; q += 10)
                Check(FamilyLifeMath.Take(false, o, q) != MatchTake.Doubt, $"only family doubt ({o}, {q})");

        // ---------- A child on the way ----------
        Check(FamilyLifeMath.Wanted(false, false), "wanted unless something says otherwise");
        Check(!FamilyLifeMath.Wanted(true, false) && !FamilyLifeMath.Wanted(false, true) && !FamilyLifeMath.Wanted(true, true), "not wanted: the mother unhappy or the couple avoiding it");

        // ---------- Birthdays the family marks ----------
        Check(FamilyLifeMath.Marked(7, true) && FamilyLifeMath.Marked(13, true), "the game's growth moments");
        Check(FamilyLifeMath.Marked(FamilyLifeMath.AdultAge, false), "coming of age");
        Check(!FamilyLifeMath.Marked(8, false) && !FamilyLifeMath.Marked(19, false) && !FamilyLifeMath.Marked(30, false), "an ordinary birthday");

        // ---------- Names for a baby ----------
        var fam = new List<NameSource>
        {
            new NameSource { name = "Toni", rank = 1, dead = false, fits = true },
            new NameSource { name = "Hector", rank = 1, dead = false, fits = false },
            new NameSource { name = "Morgan", rank = 0, dead = false, fits = true },
            new NameSource { name = "Ada", rank = 3, dead = true, fits = true },
            new NameSource { name = "Toni", rank = 2, dead = true, fits = true },
            new NameSource { name = "Harper", rank = 2, dead = false, fits = true },
            new NameSource { name = "", rank = 1, dead = false, fits = true }
        };
        var names = FamilyLifeMath.Suggestions(fam, new[] { "Harper" });
        Check(names.Count == 3, "fitting, distinct, not a living sibling's, none empty: " + string.Join(",", names));
        Check(names.Count > 0 && (names[0] == "Toni" || names[0] == "Ada") && names.IndexOf("Morgan") == names.Count - 1, "the remembered dead first, then the closest");
        Check(!names.Contains("Hector") && !names.Contains("Harper"), "not of the other gender, not a sibling's");
        var many = Enumerable.Range(0, 20).Select(i => new NameSource { name = "N" + i, rank = i % 4, fits = true }).ToList();
        Check(FamilyLifeMath.Suggestions(many, null).Count == FamilyLifeMath.MaxSuggested, "capped");
        Check(FamilyLifeMath.Suggestions(null, null).Count == 0, "no family, no names");

        // ---------- Named after ----------
        var rel = new List<(string, int, bool)> { ("Toni", 2, true), ("toni", 1, true), ("Toni", 0, false), ("Ada", 1, true) };
        Check(FamilyLifeMath.NamedAfter("Toni", rel) == 1, "the closest older relative of that name");
        Check(FamilyLifeMath.NamedAfter("Morgan", rel) == -1 && FamilyLifeMath.NamedAfter("", rel) == -1 && FamilyLifeMath.NamedAfter("Toni", null) == -1, "nobody of that name");
        Check(FamilyLifeMath.NamedAfter("Toni", new List<(string, int, bool)> { ("Toni", 0, false) }) == -1, "not after someone born later");

        return cases;
    }
}
