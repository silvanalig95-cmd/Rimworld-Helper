using ColonyCouncil;

/// <summary>
/// What the world's peoples remember (MemoryMath): deeds fade, the big ones slower; a deed done again soon adds to the
/// first within its cap; the settled and the faint make way first when a people's list is full; how long ago reads
/// right; and how a people's ledger reads as a tone.
/// </summary>
public static class MemoryTests
{
    public static int Run(Action<bool, string> check)
    {
        int cases = 0;
        const int year = MemoryMath.TicksPerYear;
        const int day = MemoryMath.TicksPerDay;

        // Fading: full strength now, half after one half-life, never below zero, slower for bigger deeds.
        foreach (int w in new[] { 1, 3, 6, 12, 20, 45, 60 })
        {
            cases++;
            check(Math.Abs(MemoryMath.Strength(w, 0) - w) < 0.001f, $"a deed of {w} is whole when new");
            int half = (int)(MemoryMath.HalfLifeYears(w) * year);
            check(Math.Abs(MemoryMath.Strength(w, half) - w / 2f) < 0.01f * w, $"a deed of {w} is half remembered after its half-life");
            check(MemoryMath.Strength(w, -5000) == w, $"a deed of {w} isn't stronger from the future");
            check(MemoryMath.Strength(w, year) < w && MemoryMath.Strength(w, year) > 0f, $"a deed of {w} fades in a year but isn't gone");
            float yrs = MemoryMath.YearsRemembered(w);
            if (w > 1) check(MemoryMath.Strength(w, (int)(yrs * year) + day) < MemoryMath.Forgotten + 0.01f, $"a deed of {w} is forgotten after {yrs:0.0} years");
        }
        check(MemoryMath.HalfLifeYears(45) > MemoryMath.HalfLifeYears(12) && MemoryMath.HalfLifeYears(12) > MemoryMath.HalfLifeYears(3), "big deeds last longer");
        check(MemoryMath.YearsRemembered(45) > 15f && MemoryMath.YearsRemembered(6) < 5f, "a town burned is remembered for decades, a small gift for a few years");
        check(MemoryMath.Strength(0, year) == 0f, "nothing stays nothing");

        // Merging: what's left of the first plus the new one, within the kind's cap and the overall cap.
        check(MemoryMath.Merged(6, 0, 6) == 12, "two gifts the same day add up");
        check(MemoryMath.Merged(8, 0, 1, 8) == 8, "a run of trades stops at its cap");
        check(MemoryMath.Merged(50, 0, 50) == MemoryMath.MaxWeight, "nothing goes past the overall cap");
        check(MemoryMath.Merged(10, year * 2, 3) < 13, "an old deed adds less than a fresh one");
        check(MemoryMath.Merged(3, 0, 0) >= 1, "a merge never comes out empty");

        // Making way: settled first, then the faintest.
        check(MemoryMath.Weakest(new List<float>(), new List<bool>()) == -1, "an empty list has nothing to drop");
        check(MemoryMath.Weakest(new List<float> { 5f, 2f, 9f }, new List<bool> { false, false, false }) == 1, "the faintest makes way");
        check(MemoryMath.Weakest(new List<float> { 5f, 2f, 9f }, new List<bool> { false, false, true }) == 2, "a settled deed makes way before an owed one");
        check(MemoryMath.Weakest(new List<float> { 5f, 2f, 9f }, new List<bool> { true, false, true }) == 0, "of the settled, the faintest");
        check(MemoryMath.Weakest(new List<float> { 4f, 3f }, null) == 1, "without settled flags, the faintest");

        // How long ago.
        check(MemoryMath.AgoOf(0, out _) == MemoryMath.Ago.Lately && MemoryMath.AgoOf(14 * day, out _) == MemoryMath.Ago.Lately, "lately within a quadrum");
        check(MemoryMath.AgoOf(40 * day, out int n) == MemoryMath.Ago.Days && n == 40, "forty days ago");
        check(MemoryMath.AgoOf(70 * day, out _) == MemoryMath.Ago.LastYear, "last year");
        check(MemoryMath.AgoOf(3 * year + day, out n) == MemoryMath.Ago.Years && n == 3, "three years ago");
        check(MemoryMath.AgoOf(-day, out _) == MemoryMath.Ago.Lately, "a deed from the future is lately");

        // Tone.
        check(MemoryMath.Tone(0f, 0f, 0f) == 0, "nothing remembered");
        check(MemoryMath.Tone(5f, 0f, 0f) == 1, "a little gratitude");
        check(MemoryMath.Tone(20f, 2f, 0f) == 2, "in the colony's debt");
        check(MemoryMath.Tone(1f, 6f, 0f) == 3, "wary");
        check(MemoryMath.Tone(5f, 30f, 0f) == 4, "a grudge");
        check(MemoryMath.Tone(20f, 20f, 0f) == 5, "both");
        check(MemoryMath.Tone(0f, 0f, 20f) == 6, "the colony owes them");
        check(MemoryMath.Tone(2f, 0f, 5f) == 6, "a small debt of the colony's still shows");
        for (float g = 0f; g <= 40f; g += 4f)
        for (float b = 0f; b <= 40f; b += 4f)
        {
            cases++;
            int t = MemoryMath.Tone(g, b, 0f);
            check(t >= 0 && t <= 6, $"tone in range ({g},{b})");
            if (b > g && b >= 15f && g < 15f) check(t == 4, $"a big grudge outweighing gratitude reads as a grudge ({g},{b})");
        }

        // Single colonists: the one who set theirs free, the one who tended their wounded.
        cases++;
        check(MemoryMath.KeepNote(0) && MemoryMath.KeepNote(MemoryMath.NoteDays * day), "a note of who tended them is kept until they go home");
        check(!MemoryMath.KeepNote(MemoryMath.NoteDays * day + 1), "a note is let go after a month");
        check(!MemoryMath.KeepNote(-day), "a note from the future (another game's) is let go");
        check(MemoryMath.BestFriend(new List<float>(), new List<bool>()) == -1, "nobody to remember");
        check(MemoryMath.BestFriend(new List<float> { 12f, 20f }, new List<bool> { true, true }) == 1, "the bigger debt names its colonist");
        check(MemoryMath.BestFriend(new List<float> { 12f, 20f }, new List<bool> { true, false }) == 0, "someone no longer with the colony isn't asked for");
        check(MemoryMath.BestFriend(new List<float> { 5f }, new List<bool> { true }) == -1, "a faded good turn names nobody");
        check(MemoryMath.BestFriend(new List<float> { MemoryMath.NamedFriend }, new List<bool> { true }) == 0, "just strong enough still names them");
        check(MemoryMath.BestFriend(new List<float> { 12f }, null) == -1, "without knowing who's here, nobody");
        check(MemoryMath.BestFriend(new List<float> { 12f, 9f }, new List<bool> { true }) == 0, "a short list of who's here only reads what it has");
        check(MemoryMath.Greeting(0f) == 0 && MemoryMath.Greeting(MemoryMath.NamedFriend - 0.1f) == 0, "faded: no greeting");
        check(MemoryMath.Greeting(MemoryMath.NamedFriend) == 1 && MemoryMath.Greeting(12f) == 1, "greeted by name");
        check(MemoryMath.Greeting(MemoryMath.DearFriend) == 2 && MemoryMath.Greeting(45f) == 2, "a big debt: greeted like family");
        // A release (12) names its colonist for a while, then fades below a name, before it's forgotten altogether.
        float namedYears = 0f;
        for (int y = 0; y <= 40; y++)
            if (MemoryMath.Strength(12, y * year / 4) >= MemoryMath.NamedFriend) namedYears = y / 4f;
        check(namedYears >= 1.5f && namedYears < MemoryMath.YearsRemembered(12), $"a freed prisoner's people name the colonist for about {namedYears:0.0} years, less than they remember the deed");
        check(MemoryMath.Strength(10, 0) >= MemoryMath.NamedFriend, "a wounded guest tended is named from the start");
        check(MemoryMath.Strength(18, 3 * year) >= MemoryMath.NamedFriend, "a lost child found is named for years");
        for (int w = 1; w <= 60; w += 7)
        {
            cases++;
            int last = 3;
            for (int age = 0; age <= 10 * year; age += year / 2)
            {
                int g = MemoryMath.Greeting(MemoryMath.Strength(w, age));
                check(g <= last, $"a greeting never warms as a deed of {w} fades");
                last = g;
            }
        }
        return cases;
    }
}
