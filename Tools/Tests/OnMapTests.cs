using ColonyCouncil;

/// <summary>
/// Things an answer says now exist (PlayOut/OnMapMath): how many trees are felled for the wood a guest cut, how the logs
/// are shared among the stumps, which of a list of things fits the colony's era, how many saplings the children plant,
/// what counts as a visitor's dog, and when an animal is in decline.
/// </summary>
public static class OnMapTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "things made real: " + what);
        }

        // ---------------- Felling for the wood ----------------

        C(OnMapMath.TreesToFell(new List<int> { 25, 25, 25 }, 20) == 1, "one grown tree is enough for twenty logs");
        C(OnMapMath.TreesToFell(new List<int> { 12, 12, 12, 12 }, 40) == 4, "smaller trees: as many as it takes");
        C(OnMapMath.TreesToFell(new List<int> { 10, 0, 10 }, 15) == 3, "a tree with no wood in it is passed over, the next one counts");
        C(OnMapMath.TreesToFell(Enumerable.Repeat(5, 20).ToList(), 100) == OnMapMath.MaxTrees, "never more than a handful of trees, however much wood");
        C(OnMapMath.TreesToFell(new List<int> { 30 }, 0) == 0 && OnMapMath.TreesToFell(null, 20) == 0, "no wood wanted, or no trees: nothing felled");
        C(OnMapMath.TreesToFell(new List<int>(), 20) == 0, "no trees near: none felled (the wood is set down instead)");
        C(OnMapMath.TreesToFell(new List<int> { 8 }, 20) == 1, "too little wood near: what there is is felled");

        var s = OnMapMath.Shares(25, 2);
        C(s.Count == 2 && s.Sum() == 25 && s[0] == 13 && s[1] == 12, "twenty-five logs by two stumps: thirteen and twelve");
        C(OnMapMath.Shares(3, 5).Count == 3, "never more piles than logs");
        C(OnMapMath.Shares(40, 0).Sum() == 40 && OnMapMath.Shares(40, 0).Count == 1, "no stumps: one pile, all the wood");
        C(OnMapMath.Shares(0, 3).Count == 0, "no wood: no piles");
        var many = OnMapMath.Shares(41, 4);
        C(many.Sum() == 41 && many.Max() - many.Min() <= 1, "shared as evenly as can be");

        // ---------------- The colony's era ----------------

        const int Neolithic = 2, Medieval = 3, Industrial = 4;
        C(OnMapMath.FirstFitting(new List<int> { -1, Industrial, Industrial }, Industrial) == 1, "a thing this game lacks is skipped (a DLC's top hat without the DLC)");
        C(OnMapMath.FirstFitting(new List<int> { Industrial, Industrial }, Medieval) == -1, "nothing from beyond the colony's era: a medieval colony gets no bowler hat");
        C(OnMapMath.FirstFitting(new List<int> { 0 }, Neolithic) == 0, "a thing of no era (a carving) fits any colony");
        C(OnMapMath.FirstFitting(new List<int> { Industrial, Medieval }, Medieval) == 1, "the first that fits, in the order given");
        C(OnMapMath.FirstFitting(null, Industrial) == -1, "no list: nothing");

        // ---------------- Saplings ----------------

        C(OnMapMath.Saplings(0) == 1, "the child who asked plants one even if no other child is counted");
        C(OnMapMath.Saplings(3) == 3, "one for each child");
        C(OnMapMath.Saplings(12) == OnMapMath.MaxSaplings, "never more than six at once");

        // ---------------- A visitor's dog ----------------

        C(OnMapMath.DogKind("Husky", "husky"), "the game's husky is a dog");
        C(OnMapMath.DogKind("LabradorRetriever", "labrador retriever") && OnMapMath.DogKind("YorkshireTerrier", "yorkshire terrier"), "the labrador and the terrier too");
        C(OnMapMath.DogKind("AEXP_Dachshund", "sausage dog") && OnMapMath.DogKind("Mod_Hound", "hunting hound"), "other mods' dogs, by name");
        C(!OnMapMath.DogKind("Wolf_Timber", "timber wolf") && !OnMapMath.DogKind("Fox_Fennec", "fennec fox") && !OnMapMath.DogKind("Warg", "warg"), "not a wolf, a fox or a warg");
        C(!OnMapMath.DogKind("Muffalo", "muffalo") && !OnMapMath.DogKind("", null), "a pack muffalo isn't a dog, and nothing isn't either");

        // ---------------- An animal in decline ----------------

        C(OnMapMath.Fading(10f, 12f, false), "old for its kind: in decline");
        C(!OnMapMath.Fading(3f, 12f, false), "young and well: not in decline");
        C(OnMapMath.Fading(2f, 12f, true), "a lasting condition: in decline, whatever its age");
        C(!OnMapMath.Fading(5f, 0f, false), "no life expectancy known and nothing wrong: not in decline");

        return n;
    }
}
