using ColonyCouncil;

/// <summary>
/// Work parties, walks together, vigils, searches, errands and hunts (PlayOutMath): how many go and when there are
/// enough, who counts as having taken part, the stages of a walk, where searchers fan out to and where people stand
/// round a grave, how much an errand takes from the stores and carries in one trip, and the caps that keep any of it
/// from running on.
/// </summary>
public static class PlayOutGroupTests
{
    public static int Run(Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "play out groups: " + what);
        }

        // How many go: those free, no more than asked, no more than the cap (and never more than eight).
        Check(PlayOutMath.GroupSize(10, 3, 6) == 3 && PlayOutMath.GroupSize(2, 3, 6) == 2, "as many as asked, or as many as are free");
        Check(PlayOutMath.GroupSize(10, 0, 6) == 6 && PlayOutMath.GroupSize(10, 20, 6) == 6, "the cap when none is asked, and over it");
        Check(PlayOutMath.GroupSize(30, 30, 30) == PlayOutMath.MaxGroup, "never more than eight");
        Check(PlayOutMath.GroupSize(0, 3, 6) == 0 && PlayOutMath.GroupSize(-1, 3, 6) == 0, "nobody free: nobody goes");
        for (int able = 0; able <= 12; able++)
            for (int wanted = 0; wanted <= 10; wanted++)
                for (int cap = 1; cap <= 9; cap++)
                {
                    int n = PlayOutMath.GroupSize(able, wanted, cap);
                    Check(n >= 0 && n <= able && n <= Math.Min(cap, PlayOutMath.MaxGroup), $"size {able}/{wanted}/{cap} in range");
                }

        // Enough to set out: one for an errand of one, both of a pair, two for anything bigger.
        Check(PlayOutMath.GroupReady(1, 1) && !PlayOutMath.GroupReady(0, 1), "one alone");
        Check(PlayOutMath.GroupReady(2, 2) && !PlayOutMath.GroupReady(1, 2), "both of a pair");
        Check(PlayOutMath.GroupReady(2, 6) && !PlayOutMath.GroupReady(1, 6), "two of a bigger party");
        Check(!PlayOutMath.GroupReady(5, 0), "nobody wanted: never");

        // Who took part.
        int night = PlayOutMath.TicksPerHour * 6;
        Check(!PlayOutMath.VigilCounts(PlayOutMath.TicksPerHour, night) && PlayOutMath.VigilCounts(PlayOutMath.TicksPerHour * 2, night), "a vigil: two hours of a long night");
        Check(PlayOutMath.VigilCounts(PlayOutMath.TicksPerHour, PlayOutMath.TicksPerHour * 3) && !PlayOutMath.VigilCounts(600, PlayOutMath.TicksPerHour * 3), "a vigil: a third of a short one");
        Check(PlayOutMath.WorkCounts(1, 0, PlayOutMath.TicksPerHour * 2), "a work party: one job done is enough");
        Check(PlayOutMath.WorkCounts(0, PlayOutMath.TicksPerHour / 2, PlayOutMath.TicksPerHour * 2) && !PlayOutMath.WorkCounts(0, 400, PlayOutMath.TicksPerHour * 2),
            "a work party: a quarter of it at work");
        Check(PlayOutMath.WorkCounts(0, PlayOutMath.TicksPerHour, PlayOutMath.TicksPerHour * 8), "a work party: never more than an hour asked");
        int stay = PlayOutMath.WalkStay(PlayOutMath.TicksPerHour * 2);
        Check(stay == PlayOutMath.TicksPerHour && PlayOutMath.WalkStay(500) == PlayOutMath.TicksPerHour / 2, "a walk's stay: half its length, at least half an hour");
        Check(PlayOutMath.WalkCounts(stay / 2, stay) && !PlayOutMath.WalkCounts(stay / 2 - 1, stay), "a walk: half the stay at the far end");
        Check(PlayOutMath.SearchCounts(1, 2) && PlayOutMath.SearchCounts(2, 3) && !PlayOutMath.SearchCounts(1, 3) && !PlayOutMath.SearchCounts(0, 0), "a search: half the places");

        // The stages of a walk: everyone come moves it on; most, after a wait; anyone, after a long wait.
        int h = PlayOutMath.TicksPerHour;
        Check(PlayOutMath.NextWalkPhase(0, 4, 4, 0, h) == 1, "all gathered: off they go");
        Check(PlayOutMath.NextWalkPhase(0, 3, 4, 0, h) == 0 && PlayOutMath.NextWalkPhase(0, 3, 4, h / 4, h) == 1, "most gathered: after a quarter hour");
        Check(PlayOutMath.NextWalkPhase(0, 1, 4, h / 4, h) == 0 && PlayOutMath.NextWalkPhase(0, 0, 4, h / 2, h) == 1, "the stragglers aren't waited for past half an hour");
        Check(PlayOutMath.NextWalkPhase(1, 2, 4, h, h) == 1 && PlayOutMath.NextWalkPhase(1, 3, 4, h / 2, h) == 2 && PlayOutMath.NextWalkPhase(1, 0, 4, h * 2, h) == 2, "walking out");
        Check(PlayOutMath.NextWalkPhase(2, 4, 4, h - 1, h) == 2 && PlayOutMath.NextWalkPhase(2, 0, 4, h, h) == 3, "the stay lasts its time");
        Check(PlayOutMath.NextWalkPhase(3, 4, 4, 0, h) == 4 && PlayOutMath.NextWalkPhase(3, 1, 4, h, h) == 4 && PlayOutMath.NextWalkPhase(3, 1, 4, h - 1, h) == 3, "back home");
        Check(PlayOutMath.NextWalkPhase(1, 0, 0, 0, h) == 4 && PlayOutMath.NextWalkPhase(9, 1, 1, 0, h) == 4, "nobody left: over");
        // From any stage, waiting long enough always moves it on: no walk hangs.
        for (int ph = 0; ph < 4; ph++)
            for (int going = 1; going <= 8; going++)
                Check(PlayOutMath.NextWalkPhase(ph, 0, going, h * 3, h) > ph, $"stage {ph} with {going} moves on in time");

        // Searchers fan out: each their own arc, places in order outward, bearings within the compass.
        for (int n = 1; n <= 4; n++)
        {
            int stops = PlayOutMath.SearchStops(n);
            Check(stops == (n == 1 ? 3 : 2), $"{n} searchers: {stops} places each");
            for (int seed = -400; seed <= 400; seed += 137)
                for (int s = 0; s < n; s++)
                {
                    float arc = 360f / n;
                    float start = ((seed % 360) + 360) % 360 + arc * s;
                    for (int k = 0; k < stops; k++)
                    {
                        float b = PlayOutMath.SearchBearing(s, n, k, stops, seed);
                        Check(b >= 0f && b < 360f, $"bearing in the compass ({n},{s},{k},{seed})");
                        float rel = ((b - start) % 360f + 360f) % 360f;
                        Check(rel > 0f && rel < arc, $"searcher {s} of {n} keeps to their own arc ({k},{seed})");
                    }
                }
            float last = -1f;
            for (int k = 0; k < stops; k++)
            {
                float r = PlayOutMath.SearchRadius(k, stops, 16f, 34f);
                Check(r >= 16f && r <= 34f && r > last, $"places step outward ({n},{k})");
                last = r;
            }
        }
        Check(Math.Abs(PlayOutMath.SearchRadius(0, 1, 10f, 20f) - 15f) < 0.01f, "one place: halfway out");
        Check(PlayOutMath.SearchWait(PlayOutMath.TicksPerHour * 3, 3) >= 400 && PlayOutMath.SearchWait(PlayOutMath.TicksPerHour * 30, 1) <= PlayOutMath.TicksPerHour / 2, "looking about: a few minutes to half an hour");

        // Where people stand round a grave: never on it, each place once, the four sides first, then the corners.
        var seen = new HashSet<(int, int)>();
        for (int i = 0; i < PlayOutMath.RingCount; i++)
        {
            PlayOutMath.RingOffset(i, out int dx, out int dz);
            Check(dx != 0 || dz != 0, $"place {i} isn't on the spot itself");
            Check(Math.Max(Math.Abs(dx), Math.Abs(dz)) <= 3 && Math.Max(Math.Abs(dx), Math.Abs(dz)) >= 2, $"place {i} a step back from it");
            Check(seen.Add((dx, dz)), $"place {i} not taken twice");
        }
        for (int i = 0; i < 4; i++)
        {
            PlayOutMath.RingOffset(i, out int dx, out int dz);
            Check(dx == 0 || dz == 0, $"the first four are the sides ({i})");
        }
        for (int i = 0; i < 8; i += 2)
        {
            PlayOutMath.RingOffset(i, out int ax, out int az);
            PlayOutMath.RingOffset(i + 1, out int bx, out int bz);
            Check(ax == -bx && az == -bz, $"two by two, facing each other across it ({i})");
        }
        PlayOutMath.RingOffset(PlayOutMath.RingCount + 2, out int wx, out int wz);
        PlayOutMath.RingOffset(2, out int ox, out int oz);
        PlayOutMath.RingOffset(-1, out int nx, out int nz);
        Check(wx == ox && wz == oz && seen.Contains((nx, nz)), "the places wrap round");

        // What an errand takes, as the answer's own taking would.
        Check(PlayOutMath.CarryAmount(20, 99, 1f, 1f, false) == 20 && PlayOutMath.CarryAmount(0, 12, 1f, 1f, false) == 12, "the answer's number, else the letter's");
        Check(PlayOutMath.CarryAmount(20, 0, 1f, 0.75f, false) == 15 && PlayOutMath.CarryAmount(20, 0, 0.5f, 1f, false) == 10, "the quartermaster's saving and a share");
        Check(PlayOutMath.CarryAmount(0, 0, 1f, 1f, false) == 1, "never nothing");
        Check(PlayOutMath.CarryAmount(0, 137, 1f, 0.75f, true) == 140 && PlayOutMath.CarryAmount(0, 3, 1f, 1f, true) == 10, "a sum of silver: to tens, at least ten, no saving");
        Check(PlayOutMath.CarryAmount(0, 200, 0.5f, 1f, true) == 100, "half a sum");
        Check(PlayOutMath.CarryCount(40, 75, 75) == 40 && PlayOutMath.CarryCount(40, 25, 75) == 25 && PlayOutMath.CarryCount(100, 75, 30) == 30, "one trip: what the stack and the hands hold");
        Check(PlayOutMath.CarryCount(0, 50, 50) == 0 && PlayOutMath.CarryCount(10, 0, 50) == 0 && PlayOutMath.CarryCount(-5, 50, 50) == 0, "nothing to carry");

        // A hunt ends when the game is down or the time is up.
        Check(PlayOutMath.HuntOver(0, 0, h * 3) && PlayOutMath.HuntOver(2, h * 3, h * 3) && !PlayOutMath.HuntOver(1, h, h * 3), "the hunt's end");

        // Caps: nothing runs on.
        Check(PlayOutMath.CapHours(12f, true) == PlayOutMath.MaxVigilHours && PlayOutMath.CapHours(12f, false) == PlayOutMath.MaxGroupHours, "lengths capped");
        Check(PlayOutMath.CapHours(0f, false) == 0.25f && PlayOutMath.CapHours(3f, false) == 3f, "short ones kept, at least a quarter hour");
        Check(PlayOutMath.RunCap(h * 2) == h * 5, "let go three hours past its length");
        Check(PlayOutMath.MaxGroupsRunning >= 1 && PlayOutMath.MaxGroupsRunning <= 3, "only a couple under way at once");
        return cases;
    }
}
