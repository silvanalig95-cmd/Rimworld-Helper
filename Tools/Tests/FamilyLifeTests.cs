using ColonyCouncil;

/// <summary>
/// The family's life together (Source/ColonyCouncil/Social/FamilyLifeMath.cs): who is glad of a new couple and who
/// doubts it, whether a child on the way is wanted, which birthdays the family marks, how much family weighs in who
/// inherits, the family names offered for a baby, who a child was named after, what a family is called and what its
/// name stands for, who sits down to a family supper, how grown siblings get on, and how often an ageing parent is
/// looked in on.
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

        // ---------- A widowed parent's new partner ----------
        Check(FamilyLifeMath.NewPartnerTake(0) == MatchTake.Glad && FamilyLifeMath.NewPartnerTake(60) == MatchTake.Glad, "a child glad for their parent");
        Check(FamilyLifeMath.NewPartnerTake(-1) == MatchTake.Doubt && FamilyLifeMath.NewPartnerTake(-80) == MatchTake.Doubt, "hurt when they dislike the one in their late parent's place");
        for (int o = -100; o <= 100; o += 5) Check(FamilyLifeMath.NewPartnerTake(o) != MatchTake.None, $"a child always takes it one way or the other ({o})");

        // ---------- A child on the way ----------
        Check(FamilyLifeMath.Wanted(false, false), "wanted unless something says otherwise");
        Check(!FamilyLifeMath.Wanted(true, false) && !FamilyLifeMath.Wanted(false, true) && !FamilyLifeMath.Wanted(true, true), "not wanted: the mother unhappy or the couple avoiding it");

        // ---------- Birthdays the family marks ----------
        Check(FamilyLifeMath.Marked(7, true) && FamilyLifeMath.Marked(13, true), "the game's growth moments");
        Check(FamilyLifeMath.Marked(FamilyLifeMath.AdultAge, false), "the eighteenth");
        Check(FamilyLifeMath.ComingOfAgeAge < FamilyLifeMath.AdultAge && FamilyLifeMath.Marked(FamilyLifeMath.ComingOfAgeAge, true), "coming of age is a growth moment, before the eighteenth");
        Check(!FamilyLifeMath.Marked(8, false) && !FamilyLifeMath.Marked(19, false) && !FamilyLifeMath.Marked(30, false), "an ordinary birthday");

        // ---------- What the dead leave: by closeness, never blood first (A30, 20 November 2026) ----------
        float C(int opinion, float web = 0f, bool partner = false, bool kin = false) => FamilyLifeMath.Closeness(opinion, web, partner, kin);
        Check(C(50, 10f, true, false) == VendettaMath.Closeness(new VendettaMath.Bond(50, 10f, true, false)) && C(30, 0f, false, true) == VendettaMath.Closeness(new VendettaMath.Bond(30, 0f, false, true)),
            "the same measure as a vendetta passed on");
        Check(C(40, 0f, true) > C(40, 0f, false, true) && C(40, 0f, false, true) > C(40), "at the same opinion a partner, then a relative by blood, then a friend");
        Check(C(90, 25f) > C(10, 0f, false, true), "a close friend before a brother who was never close");
        Check(C(70, 20f) > C(20, 0f, true), "a partner is one part among the others, not first whatever they felt");
        int Pick(float atLeast, params float[] c) => FamilyLifeMath.Heir(c, atLeast);
        Check(Pick(FamilyLifeMath.KeepsakeAtLeast, C(10, 0f, false, true), C(90, 25f), C(40)) == 1, "the keepsake to the closest, here a friend over a distant cousin");
        Check(Pick(FamilyLifeMath.KeepsakeAtLeast, C(5), C(-30, 0f, false, true), C(10)) == -1, "nobody close enough: the keepsake stays where it is");
        Check(Pick(FamilyLifeMath.KeepsakeAtLeast, C(0, 0f, false, true)) == 0 && Pick(FamilyLifeMath.KeepsakeAtLeast, C(0, 0f, true)) == 0,
            "a relative or a partner with nothing against them is close enough for a keepsake");
        Check(Pick(FamilyLifeMath.RoomAtLeast, C(30, 0f, false, true), C(50, 15f)) == 1 && Pick(FamilyLifeMath.RoomAtLeast, C(30, 0f, false, true)) == -1,
            "the room to a close friend; a relative who wasn't close gets no room for being blood");
        Check(Pick(FamilyLifeMath.RoomAtLeast, C(60), C(60)) == 0 && Pick(FamilyLifeMath.RoomAtLeast) == -1 && FamilyLifeMath.Heir(null, 0f) == -1 && Pick(0f, float.NaN) == -1,
            "two as close: the first; none, nobody");
        Check(FamilyLifeMath.RoomAtLeast == VendettaMath.AtLeast && FamilyLifeMath.KeepsakeAtLeast < FamilyLifeMath.RoomAtLeast, "a room asks as much closeness as a vendetta, a keepsake less");

        // ---------- Who raises a child with nobody of their own: the closest, never family first (27 November 2026) ----------
        Check(FamilyLifeMath.RaiserCloseness(80, 60, 15f, false) > FamilyLifeMath.RaiserCloseness(10, 20, 0f, true),
            "a grown-up who is close to the child before an aunt who never was");
        Check(FamilyLifeMath.RaiserCloseness(40, 40, 0f, true) > FamilyLifeMath.RaiserCloseness(40, 40, 0f, false), "as close otherwise: a relative by blood a little ahead");
        Check(FamilyLifeMath.RaiserCloseness(40, 80, 0f, false) > FamilyLifeMath.RaiserCloseness(40, 0, 0f, false), "the child's own liking counts, at half");

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


        // ---------- A family's name ----------
        Check(FamilyLifeMath.FamilyPlural("Boyd") == "Boyds" && FamilyLifeMath.FamilyPlural("Kennedy") == "Kennedys", "a family by its name");
        Check(FamilyLifeMath.FamilyPlural("Jones") == "Joneses" && FamilyLifeMath.FamilyPlural("Fox") == "Foxes" && FamilyLifeMath.FamilyPlural("Church") == "Churches"
              && FamilyLifeMath.FamilyPlural("Bush") == "Bushes" && FamilyLifeMath.FamilyPlural("Diaz") == "Diazes", "names that end in a hiss take -es");
        Check(FamilyLifeMath.FamilyPlural(" Boyd ") == "Boyds", "spaces round a name don't count");
        Check(FamilyLifeMath.FamilyPlural(null) == null && FamilyLifeMath.FamilyPlural("  ") == null, "no name, no family name");
        Check(FamilyLifeMath.SameName("Boyd", "boyd") && !FamilyLifeMath.SameName("Boyd", "Boyds"), "the same name, case aside");
        Check(!FamilyLifeMath.SameName("", "") && !FamilyLifeMath.SameName(null, "Boyd"), "blanks are nobody's name");

        // ---------- Who belongs together ----------
        var groups = FamilyLifeMath.Clusters(6, new[] { (0, 2), (2, 4), (1, 5), (9, 0), (-1, 3) });
        Check(groups.Count == 3, "three groups: linked, linked through another, and alone");
        Check(groups[0].SequenceEqual(new[] { 0, 2, 4 }) && groups[1].SequenceEqual(new[] { 1, 5 }) && groups[2].SequenceEqual(new[] { 3 }), "each group in order, smallest first");
        Check(FamilyLifeMath.Clusters(4, new[] { (3, 2), (1, 0), (2, 1) }).Count == 1, "a chain is one family");
        Check(FamilyLifeMath.Clusters(0, null).Count == 0 && FamilyLifeMath.Clusters(3, null).Count == 3, "nobody, or nobody linked");
        var rnd = new Random(7);
        for (int t = 0; t < 40; t++)
        {
            int n = rnd.Next(1, 12);
            var links = Enumerable.Range(0, rnd.Next(0, 15)).Select(_ => (rnd.Next(n), rnd.Next(n))).ToList();
            var cl = FamilyLifeMath.Clusters(n, links);
            Check(cl.SelectMany(g => g).OrderBy(i => i).SequenceEqual(Enumerable.Range(0, n)), $"everyone in exactly one group ({t})");
            Check(links.All(l => cl.Any(g => g.Contains(l.Item1) && g.Contains(l.Item2))), $"the linked share a group ({t})");
        }

        // ---------- What a family's name stands for ----------
        var kind = new float[RepMath.Facets];
        kind[(int)RepFacet.Kindness] = 20f;
        kind[(int)RepFacet.Temper] = 8f;
        var plain = new float[RepMath.Facets];
        plain[(int)RepFacet.Kindness] = 4f;
        plain[(int)RepFacet.Diligence] = 14f;
        var famName = FamilyLifeMath.FamilyStanding(new List<float[]> { kind, plain, null });
        Check(famName != null && Math.Abs(famName[(int)RepFacet.Kindness] - 12f) < 0.01f && Math.Abs(famName[(int)RepFacet.Diligence] - 7f) < 0.01f, "each side averaged over the family");
        Check(FamilyLifeMath.FamilyStanding(new List<float[]>()) == null && FamilyLifeMath.FamilyStanding(null) == null, "no family, no name");
        var sides = FamilyLifeMath.FamilySides(famName);
        Check(sides.Count == 2 && sides[0] == (RepFacet.Kindness, true) && sides[1] == (RepFacet.Diligence, true), "the sides that show, strongest first");
        Check(!sides.Any(x => x.facet == RepFacet.Temper), "a temper only one of them has doesn't show for them all");
        var loud = new float[RepMath.Facets];
        for (int f = 0; f < RepMath.Facets; f++) loud[f] = RepMath.Strong;
        Check(FamilyLifeMath.FamilySides(loud).Count == 3 && FamilyLifeMath.FamilySides(loud, 1).Count == 1, "at most a few sides");
        Check(FamilyLifeMath.FamilySides(new float[RepMath.Facets]).Count == 0 && FamilyLifeMath.FamilySides(null).Count == 0, "nothing said, nothing shown");
        Check(FamilyLifeMath.GoodSide(RepFacet.Kindness, true) && !FamilyLifeMath.GoodSide(RepFacet.Kindness, false), "kind is good, cruel isn't");
        Check(!FamilyLifeMath.GoodSide(RepFacet.Temper, true) && FamilyLifeMath.GoodSide(RepFacet.Temper, false), "a temper speaks against them, a level head for");
        Check(FamilyLifeMath.FamilyGossipChance > 0f && FamilyLifeMath.FamilyGossipChance < 0.5f, "the family is brought into a story now and then, not always");

        // ---------- A family supper ----------
        Check(FamilyLifeMath.Supper(3, 3) && FamilyLifeMath.Supper(5, 9), "three or more at home, and a meal each");
        Check(!FamilyLifeMath.Supper(2, 10), "two isn't a family supper");
        Check(!FamilyLifeMath.Supper(4, 3) && !FamilyLifeMath.Supper(3, 0), "not without a meal for each of them");

        // ---------- Grown brothers and sisters ----------
        Check(FamilyLifeMath.Siblings(FamilyLifeMath.SiblingClose, 40) == SiblingTake.Close, "close when each thinks well of the other");
        Check(FamilyLifeMath.Siblings(60, FamilyLifeMath.SiblingClose - 1) == SiblingTake.None, "fond on one side only is nothing to remark on");
        Check(FamilyLifeMath.Siblings(FamilyLifeMath.SiblingRival, 80) == SiblingTake.Rival && FamilyLifeMath.Siblings(50, -60) == SiblingTake.Rival, "rivals when either can't stand the other");
        Check(FamilyLifeMath.Siblings(0, 0) == SiblingTake.None && FamilyLifeMath.Siblings(FamilyLifeMath.SiblingRival + 1, 0) == SiblingTake.None, "a little coolness is nothing");
        for (int ab = -100; ab <= 100; ab += 10)
            for (int ba = -100; ba <= 100; ba += 10)
                Check(FamilyLifeMath.Siblings(ab, ba) == FamilyLifeMath.Siblings(ba, ab), $"it's the same both ways ({ab}, {ba})");

        // ---------- An ageing parent ----------
        Check(FamilyLifeMath.LookInChance(FamilyLifeMath.ElderAge - 1f, true) == 0f, "not before they're old");
        Check(FamilyLifeMath.LookInChance(62f, false) > 0f && FamilyLifeMath.LookInChance(80f, false) > FamilyLifeMath.LookInChance(62f, false), "more often as they age");
        Check(FamilyLifeMath.LookInChance(62f, true) > FamilyLifeMath.LookInChance(90f, false), "most when they're ill");
        Check(FamilyLifeMath.LookInChance(120f, true) <= 1f, "a chance");
        Check(FamilyLifeMath.LookInGapDays(true) < FamilyLifeMath.LookInGapDays(false) && FamilyLifeMath.LookInGapDays(true) >= 1, "sooner again when they're ill");

        return cases;
    }
}
