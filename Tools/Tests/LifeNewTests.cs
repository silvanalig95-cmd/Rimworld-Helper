using System;
using System.Collections.Generic;
using System.Linq;
using ColonyCouncil;

/// <summary>
/// The newer wishes and life moments (Lives/LifeNewMath.cs): the crop someone misses from home stays theirs, two cups
/// in the same hour, what counts as coffee or tea, a slip back to the drug, books counted once each, the way from a bed
/// to the table found round walls, and its open steps kept as text.
/// </summary>
public static class LifeNewTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "life moments: " + what);
        }

        // ---------- A crop from home ----------
        var keys = new List<int> { 101, 202, 303, 404, 505 };
        int pick = LifeNewMath.Favourite(77, keys);
        C(pick >= 0 && pick < keys.Count, "a favourite is one of those offered");
        C(LifeNewMath.Favourite(77, keys) == pick && LifeNewMath.Favourite(77, keys.AsEnumerable().Reverse().ToList()) == keys.Count - 1 - pick,
            "the same person picks the same crop every time, whatever order they're listed in");
        var others = Enumerable.Range(1, 40).Select(seed => LifeNewMath.Favourite(seed, keys)).Distinct().Count();
        C(others >= 3, $"different people miss different crops ({others} of 5 picked by 40 people)");
        C(LifeNewMath.Favourite(5, new List<int>()) == -1 && LifeNewMath.Favourite(5, null) == -1, "nothing to miss, nothing picked");

        // ---------- The morning cup ----------
        C(LifeNewMath.SameHour(1000, 3400) && LifeNewMath.SameHour(3400, 1000), "two cups within the hour, either way round");
        C(!LifeNewMath.SameHour(0, LifeNewMath.HourTicks + 1), "a cup more than an hour later is another hour");
        C(LifeNewMath.IsCupWord("coffee") && LifeNewMath.IsCupWord("VBE_Coffee") && LifeNewMath.IsCupWord("green tea") && LifeNewMath.IsCupWord("Chai"),
            "coffee and tea by name, modded names too");
        C(!LifeNewMath.IsCupWord("steak") && !LifeNewMath.IsCupWord("beer") && !LifeNewMath.IsCupWord("teapot") && !LifeNewMath.IsCupWord(null),
            "whole words only: steak isn't tea, nor beer coffee");

        // ---------- Getting clean ----------
        C(LifeNewMath.Relapsed(true, false), "still addicted and out of withdrawal: they've had it again");
        C(!LifeNewMath.Relapsed(true, true) && !LifeNewMath.Relapsed(false, false), "still in withdrawal, or clean: no slip");

        // ---------- A reader's year ----------
        var read = new List<string>();
        C(LifeNewMath.AddRead(read, "Ann", "The Wind's Song") && !LifeNewMath.AddRead(read, "Ann", "The Wind's Song"), "a book counts once for each reader");
        C(LifeNewMath.AddRead(read, "Bo", "The Wind's Song"), "the same book counts for someone else");
        LifeNewMath.AddRead(read, "Ann", "A Field Guide");
        C(LifeNewMath.ReadCount(read, "Ann") == 2 && LifeNewMath.ReadCount(read, "Bo") == 1 && LifeNewMath.ReadCount(read, "Cy") == 0, "different books counted per reader");
        var capped = new List<string>();
        for (int i = 0; i < 12; i++) LifeNewMath.AddRead(capped, "Ann", "Book " + i, 10);
        C(capped.Count == 10 && !capped.Contains("Ann|Book 0") && capped.Contains("Ann|Book 11"), "kept to its cap, the oldest dropped first");
        C(!LifeNewMath.AddRead(read, "", "x") && !LifeNewMath.AddRead(read, "Ann", null), "no reader or no title, nothing counted");

        // ---------- The way between a bed and the table ----------
        // 7 x 5, a wall down column 3 with a gap at the top: the way goes round.
        const int W = 7, H = 5;
        bool Walk(int i) => !(i % W == 3 && i / W < 4);
        int start = 2 * W + 1, goal = 2 * W + 5;
        var way = LifeNewMath.Way(W, H, Walk, start, goal);
        C(way.Count > 0 && way[0] == start && way[way.Count - 1] == goal, "the way runs from the bed to the table");
        C(way.Contains(4 * W + 3) && way.All(i => i == goal || Walk(i)), "round the wall through its gap, never through it");
        bool Adjacent(int a, int b) => Math.Abs(a % W - b % W) <= 1 && Math.Abs(a / W - b / W) <= 1;
        C(way.Zip(way.Skip(1), Adjacent).All(x => x), "one step at a time");
        C(way.Count == 7, $"the shortest way, round the corners ({way.Count} cells)");
        C(LifeNewMath.Way(W, H, i => i % W != 3, start, goal).Count == 0, "a wall all the way across: no way at all");
        C(LifeNewMath.Way(W, H, Walk, start, start).SequenceEqual(new[] { start }), "already there");
        // Diagonals don't cut a corner between two walls.
        bool Corner(int i) => i != 1 && i != W;
        var diag = LifeNewMath.Way(W, H, Corner, 0, W + 1);
        C(diag.Count == 0, "no squeezing diagonally between two walls");
        C(LifeNewMath.WorthARoof(LifeNewMath.MinOpen) && LifeNewMath.WorthARoof(LifeNewMath.MaxOpen) && !LifeNewMath.WorthARoof(2) && !LifeNewMath.WorthARoof(LifeNewMath.MaxOpen + 1),
            "a few open steps are worth a roof, a long road isn't");
        var cells = new List<(int, int)> { (12, 40), (13, 40), (0, 7) };
        string text = LifeNewMath.Encode(cells);
        C(text == "12.40 13.40 0.7" && LifeNewMath.Decode(text).SequenceEqual(cells), "the open steps kept as text and read back");
        C(LifeNewMath.Decode("").Count == 0 && LifeNewMath.Decode("x 3.4 .5").SequenceEqual(new List<(int, int)> { (3, 4) }), "text that isn't a cell is skipped");
        return n;
    }
}
