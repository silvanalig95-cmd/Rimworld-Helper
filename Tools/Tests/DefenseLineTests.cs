using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Defense lines (Source/ColonyCouncil/Defense/DefenseLineMath.cs and the step ladder in Defs/DefenseStepDefs): which way
/// a line faces and what lies left and right of its gate; whether raiders can walk round a line to the beds when its gate
/// is shut, and where (a wall that stops short of the rock, open country, a long way round taken only then); the doors
/// between a gate and the beds; how far back the cover goes; which step comes next; the strength and its parts; the
/// lessons a raid leaves; and that every step def is complete and points at pages and plans that exist.
/// </summary>
public static class DefenseLineTests
{
    /// <summary>
    /// A map from rows of characters, the top row north: # rock, ~ deep water, W wall, D door, x something else in the
    /// way, h home, c where people live (home too), . open ground.
    /// </summary>
    private static DefenseMath.MapGrid Map(int w, int h, Func<int, int, char> at)
    {
        var m = new DefenseMath.MapGrid(w, h);
        for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                int i = m.Index(x, z);
                char c = at(x, z);
                m.kind[i] = c switch
                {
                    '#' => DefenseMath.Rock,
                    '~' => DefenseMath.Water,
                    'W' => DefenseMath.Wall,
                    'D' => DefenseMath.Door,
                    'x' => DefenseMath.Other,
                    _ => DefenseMath.Open
                };
                if (c == 'h' || c == 'c') m.home[i] = true;
                if (c == 'c') m.core[i] = true;
            }
        return m;
    }

    public static int Run(string root, Action<bool, string> check)
    {
        int checks = 0;
        void Check(bool ok, string what)
        {
            checks++;
            check(ok, "defense lines: " + what);
        }

        // ---------- Directions ----------
        for (int f = 0; f < 4; f++)
        {
            DefenseLineMath.Outward(f, out int ox, out int oz);
            DefenseLineMath.Side(f, out int sx, out int sz);
            Check(Math.Abs(ox) + Math.Abs(oz) == 1 && Math.Abs(sx) + Math.Abs(sz) == 1 && ox * sx + oz * sz == 0, $"facing {f}: outward and side are unit steps at right angles");
            Check(DefenseLineMath.Ahead(ox * 3, oz * 3, f) == 3 && DefenseLineMath.Lateral(ox * 3, oz * 3, f) == 0, $"facing {f}: three steps out are three ahead");
            Check(DefenseLineMath.FacingOf(ox, oz) == f, $"facing {f}: the facing of its own outward step");
        }
        // Looking north, the right hand is east; looking west, it's north.
        DefenseLineMath.Side(0, out int nsx, out int nsz);
        Check(nsx == 1 && nsz == 0, "facing north, right is east");
        DefenseLineMath.Side(3, out int wsx, out int wsz);
        Check(wsx == 0 && wsz == 1, "facing west, right is north");
        Check(DefenseLineMath.Lateral(-4, 0, 0) == -4, "facing north, four to the west is four to the left");
        Check(DefenseLineMath.Ahead(0, -5, 0) == -5, "facing north, five south is five behind");

        // ---------- A pass closed with a wall and a gate ----------
        // A valley 9 wide (x 16..24) from the north edge down to a meadow; the line across it at z = 32.
        DefenseMath.MapGrid Valley(int wallFrom, int wallTo, bool door = true) => Map(41, 50, (x, z) =>
        {
            if (z == 32 && x >= wallFrom && x <= wallTo) return x == 20 && door ? 'D' : 'W';
            if (x >= 16 && x <= 24 && z >= 25) return '.';
            if (x >= 5 && x <= 35 && z >= 5 && z <= 24)
            {
                if (x >= 18 && x <= 22 && z >= 10 && z <= 12) return 'c';
                if (x >= 14 && x <= 26 && z >= 8 && z <= 14) return 'h';
                return '.';
            }
            return '#';
        });
        {
            var m = Valley(16, 24);
            var left = DefenseLineMath.ScanRow(m, 20, 32, 0, -1, 20);
            var right = DefenseLineMath.ScanRow(m, 20, 32, 0, 1, 20);
            Check(left.built == 4 && left.open == 0 && left.closed && left.end == DefenseMath.Rock, $"closed pass: the wall runs four cells left to the rock ({left.built} built, {left.open} open, end {left.end})");
            Check(right.built == 4 && right.open == 0 && right.closed, "closed pass: and four right");
            Check(DefenseLineMath.LineCells(m, 20, 32, 0, 20).Count == 9, "closed pass: the line is nine cells, the gate among them");
            var leak = DefenseLineMath.FindLeak(m, 20, 32, 0);
            Check(leak.front >= 0 && leak.direct > 0, $"closed pass: a way on through the open gate ({leak.direct})");
            Check(leak.sealedShut && !leak.found, "closed pass: with the gate shut there's no way round");
            Check(m.kind[m.Index(20, 32)] == DefenseMath.Door && m.kind[m.Index(17, 32)] == DefenseMath.Wall, "closed pass: the grid is put back afterwards");
            var span = DefenseLineMath.SpanAcross(m, 20, 34, 0, 14);
            Check(span.Count == 9, $"closed pass: nine cells across the valley in front of it ({span.Count})");
        }
        {
            // The wall stops two cells short of the rock on the left (x 16 and 17 open).
            var m = Valley(18, 24);
            var left = DefenseLineMath.ScanRow(m, 20, 32, 0, -1, 20);
            Check(left.built == 2 && left.open == 2 && left.closed && left.end == DefenseMath.Rock, $"short wall: two walls, then two open cells to the rock ({left.built}/{left.open})");
            Check(left.firstOpen == m.Index(18 - 1, 32), "short wall: the first open cell is where the wall would carry on");
            var leak = DefenseLineMath.FindLeak(m, 20, 32, 0);
            Check(leak.found && !leak.sealedShut, "short wall: raiders walk round it");
            Check(leak.lateral < 0 && leak.lateral >= -4, $"short wall: they cross the line on the left, by the wall's end ({leak.lateral})");
            Check(!leak.viaDoor && leak.path.Count > 0, "short wall: over open ground");
            Check(leak.path[0] == leak.front && m.core[leak.path[^1]], "short wall: the way round runs from in front of the gate to the beds");
        }
        {
            // A gap left, no gate at all: shut, the line is a wall; open, the gate cell is still a wall.
            var m = Valley(16, 24, false);
            var leak = DefenseLineMath.FindLeak(m, 20, 32, 0);
            Check(leak.noWayOn && !leak.found, "walled shut: no way on at all, open or shut");
        }

        // ---------- A long way round isn't this line's leak ----------
        {
            // Two passes into a meadow, joined outside by a long corridor along the north and east edges.
            var m = Map(70, 70, (x, z) =>
            {
                if (z >= 64 || x >= 64) return '.';
                if (x >= 20 && x <= 22 && z >= 30) return z == 40 ? (x == 21 ? 'D' : 'W') : '.';
                if (z >= 16 && z <= 18 && x >= 30) return '.';
                if (x >= 10 && x <= 30 && z >= 10 && z <= 30) return x >= 18 && x <= 22 && z >= 18 && z <= 22 ? (x == 20 && z == 20 ? 'c' : 'h') : '.';
                return '#';
            });
            var leak = DefenseLineMath.FindLeak(m, 21, 40, 0);
            Check(leak.detour > 0 && !leak.found, $"long way round: the detour by the other pass ({leak.detour} against {leak.direct}) is too long to be this line's leak");
            Check(leak.longWay && leak.path.Count > 0 && leak.cross >= 0, "long way round: still the way they take with the gate shut, and where it crosses the line is known");
            Check(DefenseLineMath.LeakSlack(130) == 35 * DefenseMath.Ortho && DefenseLineMath.LeakSlack(3000) == 1500, "the slack is 35 cells, or half the way on when that's longer");
        }

        // ---------- Open country: a short wall with a gate ----------
        {
            var m = Map(50, 50, (x, z) =>
            {
                if (z == 30 && x >= 22 && x <= 28) return x == 25 ? 'D' : 'W';
                if (x >= 22 && x <= 28 && z >= 15 && z <= 21) return x == 25 && z == 18 ? 'c' : 'h';
                return '.';
            });
            var leak = DefenseLineMath.FindLeak(m, 25, 30, 0);
            Check(leak.found, "open country: a short wall is walked round");
            var left = DefenseLineMath.ScanRow(m, 25, 30, 0, -1, 14);
            Check(left.built == 3 && !left.closed && left.end == DefenseMath.Edge, $"open country: nothing closes the row within reach ({left.built}/{left.open}/{left.end})");
        }

        // ---------- Doors behind the gate ----------
        {
            char House(int x, int z, bool inner)
            {
                bool houseRing = (x == 20 || x == 26) && z >= 20 && z <= 26 || (z == 20 || z == 26) && x >= 20 && x <= 26;
                if (houseRing) return x == 23 && z == 26 ? 'D' : 'W';
                if (x > 20 && x < 26 && z > 20 && z < 26) return 'c';
                bool ring = (x == 12 || x == 37) && z >= 12 && z <= 37 || (z == 12 || z == 37) && x >= 12 && x <= 37;
                if (ring) return x == 24 && z == 37 ? 'D' : 'W';
                if (inner)
                {
                    bool ring2 = (x == 16 || x == 32) && z >= 16 && z <= 32 || (z == 16 || z == 32) && x >= 16 && x <= 32;
                    if (ring2) return x == 24 && z == 32 ? 'D' : 'W';
                }
                return x >= 10 && x <= 39 && z >= 10 && z <= 39 ? 'h' : '.';
            }
            var one = Map(50, 50, (x, z) => House(x, z, false));
            var toCore = DefenseMath.Distances(one, DefenseLineMath.Targets(one));
            Check(DefenseLineMath.DoorsBehind(one, toCore, 24, 37, 0) == 0, "doors behind: the bedroom's own door doesn't count");
            var two = Map(50, 50, (x, z) => House(x, z, true));
            var toCore2 = DefenseMath.Distances(two, DefenseLineMath.Targets(two));
            Check(DefenseLineMath.DoorsBehind(two, toCore2, 24, 37, 0) == 1, "doors behind: an inner ring is a line to fall back to");
            var leak = DefenseLineMath.FindLeak(one, 24, 37, 0);
            Check(leak.sealedShut, "a walled colony with one gate: shut, nothing gets round");
        }

        // ---------- Distance and range ----------
        Check(DefenseLineMath.CoverDistance(0f) == 6, "no ranged weapon: cover six cells back");
        Check(DefenseLineMath.CoverDistance(24f) == 7, "bows: seven cells back");
        Check(DefenseLineMath.CoverDistance(31f) == 9, "rifles: nine cells back");
        Check(DefenseLineMath.CoverDistance(44.9f) == 10, "sniper rifles: at most ten");
        Check(DefenseLineMath.ReachOf(3f, 31f) == DefenseLineMath.Reach.TooClose, "three cells is too close");
        Check(DefenseLineMath.ReachOf(9f, 31f) == DefenseLineMath.Reach.Good, "nine cells with rifles is good");
        Check(DefenseLineMath.ReachOf(20f, 24f) == DefenseLineMath.Reach.Far, "twenty cells with bows is far");
        Check(DefenseLineMath.ReachOf(40f, 31f) == DefenseLineMath.Reach.TooFar, "forty cells with rifles is out of reach");

        // ---------- Steps ----------
        Check(DefenseLineMath.Progress(3, 4) == 0.75f && DefenseLineMath.Progress(0, 0) == 0f && DefenseLineMath.Progress(9, 4) == 1f, "progress is the share standing, at most all of it");
        Check(DefenseLineMath.Band(0.9f) == StepState.Done && DefenseLineMath.Band(0.4f) == StepState.Partly && DefenseLineMath.Band(0f) == StepState.Open, "done, partly, open");
        var states = new List<StepState> { StepState.Done, StepState.Skipped, StepState.Later, StepState.NotYet, StepState.Partly, StepState.Open };
        Check(DefenseLineMath.Current(states) == 4, "the first half-done or open step comes first");
        Check(DefenseLineMath.Current(new List<StepState> { StepState.Done, StepState.Later, StepState.NotYet }) == 2, "then one that waits on something");
        Check(DefenseLineMath.Current(new List<StepState> { StepState.Done, StepState.Later }) == 1, "then one for later");
        Check(DefenseLineMath.Current(new List<StepState> { StepState.Done, StepState.Skipped, StepState.NotApplicable }) == -1, "none when all are done or set aside");
        DefenseLineMath.Tally(states, out int done, out int counted);
        Check(done == 1 && counted == 5, $"skipped steps don't count ({done} of {counted})");

        // ---------- Strength ----------
        {
            var s = new DefenseLineMath.StrengthIn { fighters = new List<float> { 100f, 120f, 80f } };
            var bare = DefenseLineMath.Strength(s);
            Check(Math.Abs(bare.total - 300f) < 0.01f, $"three fighters in the open count as they are ({bare.total})");
            s.positions = 2;
            var cover = DefenseLineMath.Strength(s);
            Check(Math.Abs(cover.total - ((120f + 100f) * 1.6f + 80f)) < 0.01f && cover.atPosts == 2, $"the two best take the two firing positions ({cover.total})");
            s.positions = 9;
            Check(DefenseLineMath.Strength(s).atPosts == 3, "more positions than fighters: everyone at a post");
            s.closed = true;
            var closed = DefenseLineMath.Strength(s);
            s.leak = true;
            var leaky = DefenseLineMath.Strength(s);
            Check(closed.total > leaky.total && Math.Abs(leaky.factor - 0.6f) < 0.001f, "a way round costs more than a closed line gains");
            s.leak = false;
            s.killbox = true;
            Check(Math.Abs(DefenseLineMath.Strength(s).factor - 1.8f) < 0.001f, "a closed killbox: half again, and a fifth");
            s.obstacles = 40;
            Check(Math.Abs(DefenseLineMath.Strength(s).factor - 1.8f * 0.75f) < 0.001f, "cover in the field takes at most a quarter");
            s.obstacles = 0;
            s.traps = 50;
            Check(Math.Abs(DefenseLineMath.Strength(s).traps - 500f) < 0.01f, "twenty traps at most");
            s.turretWorth = 240f;
            Check(DefenseLineMath.Strength(s).total > DefenseLineMath.Strength(new DefenseLineMath.StrengthIn { fighters = s.fighters, positions = 9, closed = true, killbox = true, traps = 50 }).total, "turrets add");
        }
        Check(DefenseLineMath.Rounded(1234f) == 1250 && DefenseLineMath.Rounded(137f) == 140 && DefenseLineMath.Rounded(-5f) == 0, "strengths are said to the nearest 50 (10 below 200)");
        Check(DefenseLineMath.Verdict(1600f, 1000f) == 2 && DefenseLineMath.Verdict(1100f, 1000f) == 1 && DefenseLineMath.Verdict(800f, 1000f) == 0 && DefenseLineMath.Verdict(500f, 1000f) == -1, "the verdict bands");

        // ---------- Lessons ----------
        {
            var t = new DefenseLineMath.RaidTally { through = 9, roundLeft = 3, downedCoverLeft = 1 };
            var l = DefenseLineMath.Lessons(t);
            Check(l.Count == 2 && l[0] == DefenseLineMath.Lesson.Leak && l[1] == DefenseLineMath.Lesson.CoverLeft, $"went round, one downed on the left: close the leak first ({string.Join(",", l)})");
            var held = DefenseLineMath.Lessons(new DefenseLineMath.RaidTally { through = 7 });
            Check(held.Count == 1 && held[0] == DefenseLineMath.Lesson.Held, "all through the gate, nobody down: it held");
            var many = DefenseLineMath.Lessons(new DefenseLineMath.RaidTally { roundRight = 1, broke = 4, downedFront = 2, inside = 3, downedCoverRight = 1 });
            Check(many.Count == 3, "at most three lessons");
            Check(DefenseLineMath.Lessons(new DefenseLineMath.RaidTally()).Count == 0, "a raid that never came near says nothing");
            Check(t.Raiders == 12 && t.Downed == 1 && t.Round == 3, "the tally adds up");
        }
        Check(DefenseLineMath.PassedNear(0, 10, 0, -10, 1, 0, 2f), "a walk from ten out to ten in passes the gate");
        Check(!DefenseLineMath.PassedNear(8, 10, 8, -10, 0, 0, 3f), "a walk eight cells to the side doesn't");
        Check(DefenseLineMath.PassedNear(3, 3, 3, 3, 3, 4, 1.5f), "standing still beside it counts");

        // ---------- The step ladder ----------
        checks += StepDefs(root, Check);
        return checks;
    }

    private static int StepDefs(string root, Action<bool, string> check)
    {
        int n = 0;
        string dir = Path.Combine(root, "Defs", "DefenseStepDefs");
        var steps = Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.DefenseStepDef")).ToList()
            : new List<XElement>();
        check(steps.Count >= 8, $"steps: the ladder has its eight steps ({steps.Count})");
        n++;
        // Pages and plans that exist now (the step names newer ones first and falls back on these).
        var pages = Directory.GetFiles(Path.Combine(root, "Defs", "AlmanacArticleDefs"), "*.xml")
            .SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.AlmanacArticleDef")).Select(e => e.Element("defName")?.Value).ToHashSet();
        var plans = Directory.GetFiles(Path.Combine(root, "Defs", "DefenseTemplateDefs"), "*.xml")
            .SelectMany(f => XDocument.Load(f).Root!.Elements("ColonyCouncil.DefenseTemplateDef")).Select(e => e.Element("defName")?.Value).ToHashSet();
        string src = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Source", "ColonyCouncil", "Defense"), "*.cs").Select(File.ReadAllText));
        var aims = new HashSet<string> { "gate", "killbox", "turrets", "archers" };
        var places = new HashSet<string> { "line", "behind", "front", "between" };
        var orders = new HashSet<int>();
        foreach (var e in steps)
        {
            string name = e.Element("defName")?.Value ?? "?";
            void C(bool ok, string what)
            {
                n++;
                check(ok, $"steps: {name}: {what}");
            }
            C(!string.IsNullOrWhiteSpace(e.Element("label")?.Value), "has a label");
            C(int.TryParse(e.Element("order")?.Value, out int order) && orders.Add(order), "has an order of its own");
            C(!string.IsNullOrWhiteSpace(e.Element("whyNow")?.Value), "says why now");
            string how = e.Element("howItWorks")?.Value ?? "";
            int sentences = Regex.Matches(how, @"[.?](\s|$)").Count;
            C(sentences >= 2 && sentences <= 4, $"explains how it works in two to four sentences ({sentences})");
            C((e.Element("mistakes")?.Elements("li").Count() ?? 0) >= 2, "lists common mistakes");
            var articles = e.Element("articles")?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
            C(articles.Any(pages.Contains), "links an Almanac page that exists");
            string era = e.Element("era")?.Value;
            C(era == null || era == "lowtech" || era == "industrial", "era is lowtech, industrial or none");
            foreach (var a in e.Element("aims")?.Elements("li").Select(x => x.Value) ?? Enumerable.Empty<string>()) C(aims.Contains(a), $"aim {a} is known");
            string place = e.Element("placeAt")?.Value;
            C(place == null || places.Contains(place), $"placeAt {place} is known");
            var templates = e.Element("templates")?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
            if (templates.Count > 0) C(templates.Any(plans.Contains), "names a defense plan that exists");
            string worker = e.Element("workerClass")?.Value;
            C(worker != null && src.Contains("class " + worker.Replace("ColonyCouncil.", "") + " "), $"its worker {worker} exists");
            foreach (var ao in e.Element("orderFor")?.Elements("li") ?? Enumerable.Empty<XElement>())
                C(aims.Contains(ao.Element("aim")?.Value ?? "") && int.TryParse(ao.Element("order")?.Value, out _), "orderFor names an aim and an order");
        }
        return n;
    }
}
