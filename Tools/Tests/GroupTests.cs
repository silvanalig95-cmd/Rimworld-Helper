using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Groups (Social/Groups/GroupMath.cs): who pulls together (affinity), which clusters become groups and who joins,
/// the roles people settle into (slowly: a change waits for days, an incumbent keeps a unique role against a near
/// rival), cohesion, the rules for splits and merges, newcomers finding their way in, rivalries between groups, and
/// the ways and views a group shares. Then the defs: every kind, habit, way and moment names what exists.
/// </summary>
public static class GroupTests
{
    private static float[,] Matrix(int n, float baseline = 0f)
    {
        var m = new float[n, n];
        for (int i = 0; i < n; i++)
        for (int j = 0; j < n; j++)
            m[i, j] = i == j ? 0f : baseline;
        return m;
    }

    private static void Set(float[,] m, int a, int b, float v)
    {
        m[a, b] = v;
        m[b, a] = v;
    }

    private static void Clique(float[,] m, float v, params int[] who)
    {
        foreach (int a in who)
        foreach (int b in who)
            if (a != b)
                m[a, b] = v;
    }

    private static bool[] All(int n, bool v = true) => Enumerable.Repeat(v, n).ToArray();

    private static RoleFacts Person(float warmth = 0, float nerve = 0, float temper = 0, float humour = 0, float trust = 0, float pride = 0,
        float openness = 0, float outlook = 0, int social = 6, float tieShare = 0f, int mended = 0, bool newcomer = false) =>
        new RoleFacts
        {
            warmth = warmth, nerve = nerve, temper = temper, humour = humour, trust = trust, pride = pride, openness = openness, outlook = outlook,
            social = social, tieShare = tieShare, mended = mended, newcomer = newcomer
        };

    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "groups: " + what);
        }

        // ---------- Who pulls together ----------
        float friends = GroupMath.Affinity(new PairFacts { friends = 60f, minOpinion = 40, likeness = 0.75f });
        float workmates = GroupMath.Affinity(new PairFacts { minOpinion = 20, sharedWork = 1f, likeness = 0.7f });
        float strangers = GroupMath.Affinity(new PairFacts { likeness = 0.65f });
        float rivals = GroupMath.Affinity(new PairFacts { rivals = 40f, minOpinion = -30, likeness = 0.6f });
        Check(friends > workmates && workmates > strangers && strangers > rivals, $"friends > workmates > strangers > rivals ({friends:0.00}, {workmates:0.00}, {strangers:0.00}, {rivals:0.00})");
        Check(friends >= GroupMath.FormAt && rivals < 0f, "good friends are enough for a group; rivals pull apart");
        float hunters = GroupMath.Affinity(new PairFacts { minOpinion = 25, sharedWork = 0.9f, likeness = 0.7f, nightShift = false });
        float nightHunters = GroupMath.Affinity(new PairFacts { minOpinion = 25, sharedWork = 0.9f, likeness = 0.7f, nightShift = true });
        Check(nightHunters > hunters && hunters >= GroupMath.JoinAt, "work shared on good terms is enough to join; the night shift binds more");
        Check(GroupMath.Affinity(new PairFacts { comrades = 100f, likeness = 0.65f }) > GroupMath.Affinity(new PairFacts { comrades = 20f, likeness = 0.65f }), "more battles together, more pull");
        Check(GroupMath.Affinity(new PairFacts { bothOld = true, likeness = 0.65f }) > strangers && GroupMath.Affinity(new PairFacts { sameFaith = true, likeness = 0.65f }) > strangers
              && GroupMath.Affinity(new PairFacts { arrivedTogether = true, likeness = 0.65f }) > strangers, "age, faith and arriving together each pull a little");

        // ---------- Clusters ----------
        {
            var m = Matrix(9, 0.05f);
            Clique(m, 0.7f, 0, 1, 2, 3);
            Clique(m, 0.55f, 4, 5, 6);
            var c = GroupMath.Cluster(m, 9, All(9), new List<List<int>>());
            Check(c != null && c.SequenceEqual(new[] { 0, 1, 2, 3 }), "the closest knot becomes a group first: " + (c == null ? "none" : string.Join(",", c)));
            var c2 = GroupMath.Cluster(m, 9, All(9), new List<List<int>> { new List<int> { 0, 1, 2, 3 } });
            Check(c2 != null && c2.SequenceEqual(new[] { 4, 5, 6 }), "a knot that is already a group isn't found again; the next one is");
            var free = All(9);
            free[0] = free[1] = free[2] = free[3] = false;
            var c3 = GroupMath.Cluster(m, 9, free, new List<List<int>>());
            Check(c3 != null && c3.SequenceEqual(new[] { 4, 5, 6 }), "people with no room for another group aren't in a new one");
            Set(m, 6, 4, -0.2f);
            var c4 = GroupMath.Cluster(m, 9, free, new List<List<int>>());
            Check(c4 == null, "a rift inside a knot of three stops it forming");
            var big = Matrix(10, 0.8f);
            var c5 = GroupMath.Cluster(big, 10, All(10), new List<List<int>>());
            Check(c5 != null && c5.Count == GroupMath.MaxSize, "a group starts at most " + GroupMath.MaxSize + " strong");
            var two = Matrix(4, 0f);
            Set(two, 0, 1, 0.9f);
            Check(GroupMath.Cluster(two, 4, All(4), new List<List<int>>()) == null, "two close friends are a bond, not a group");
            var loose = Matrix(5, 0.3f);
            Check(GroupMath.Cluster(loose, 5, All(5), new List<List<int>>()) == null, "people who only get along don't form a group");
        }

        // ---------- Joining and attachment ----------
        {
            var m = Matrix(6, 0f);
            Clique(m, 0.6f, 0, 1, 2);
            Set(m, 3, 0, 0.5f);
            Set(m, 3, 1, 0.45f);
            Set(m, 3, 2, 0.4f);
            var groups = new List<List<int>> { new List<int> { 0, 1, 2 } };
            Check(GroupMath.JoinTarget(m, 3, groups) == 0, "someone who holds to a group joins it");
            Check(GroupMath.JoinTarget(m, 3, groups, new[] { true }) == -1, "but not a group they left");
            Check(GroupMath.LeaveBelow < GroupMath.JoinAt - 0.2f, "leaving takes far less pull than joining: nobody flickers in and out");
            Check(GroupMath.JoinTarget(m, 4, groups) == -1, "someone who doesn't, doesn't");
            Set(m, 3, 2, -0.1f);
            Check(GroupMath.JoinTarget(m, 3, groups) == -1, "nobody joins a group with a rival of theirs in it");
            Check(Math.Abs(GroupMath.Attachment(m, 0, new List<int> { 0, 1, 2 }) - 0.6f) < 0.001f, "attachment is the mean pull to the others");
            var full = new List<List<int>> { Enumerable.Range(0, GroupMath.MaxSize).ToList() };
            var fm = Matrix(GroupMath.MaxSize + 1, 0.9f);
            Check(GroupMath.JoinTarget(fm, GroupMath.MaxSize, full) == -1, "a full group takes nobody more");
        }

        // ---------- Roles ----------
        {
            var facts = new[]
            {
                Person(warmth: 50, pride: 30, social: 12, tieShare: 1f),
                Person(humour: 70),
                Person(warmth: 40, temper: -40, mended: 3),
                Person(temper: 60, pride: 40),
                Person(outlook: -60, warmth: 40),
                Person(humour: -40, warmth: -20, social: 2),
                Person(newcomer: true, humour: 90)
            };
            var start = facts.Select(_ => new RoleState()).ToArray();
            var roles = GroupMath.AssignRoles(facts, start);
            var want = new[] { GroupRole.Anchor, GroupRole.Joker, GroupRole.Peacemaker, GroupRole.Firebrand, GroupRole.Worrier, GroupRole.Quiet, GroupRole.Newcomer };
            for (int i = 0; i < want.Length; i++) Check(roles[i].role == want[i], $"character gives the role: {want[i]} (got {roles[i].role})");
            Check(GroupMath.AssignRoles(new[] { Person() }, new[] { new RoleState() })[0].role == GroupRole.None, "an even-tempered, ordinary person is just one of them");

            // Two jokers: one joker to a group, and the other takes their next best part.
            var twoJokers = new[] { Person(humour: 80), Person(humour: 60, temper: 50, pride: 30) };
            var r2 = GroupMath.AssignRoles(twoJokers, new RoleState[2]);
            Check(r2[0].role == GroupRole.Joker && r2[1].role == GroupRole.Firebrand, $"one joker to a group ({r2[0].role}, {r2[1].role})");

            // The incumbent keeps a unique role against someone who fits it only a little better.
            var incumbent = new[] { Person(humour: 60), Person(humour: 66) };
            var held = new[] { new RoleState { role = GroupRole.Joker }, new RoleState { role = GroupRole.None } };
            var r3 = GroupMath.AssignRoles(incumbent, held);
            Check(r3[0].role == GroupRole.Joker && r3[1].role != GroupRole.Joker, "a joker isn't unseated by someone barely funnier");

            // Hysteresis: someone who has become hot-tempered stays the joker for a few days before becoming the firebrand.
            var changed = new[] { Person(humour: 35, temper: 80, pride: 50) };
            var state = new[] { new RoleState { role = GroupRole.Joker } };
            int days = 0;
            while (state[0].role == GroupRole.Joker && days < 10)
            {
                state = GroupMath.AssignRoles(changed, state);
                days++;
            }
            Check(state[0].role == GroupRole.Firebrand && days == GroupMath.RolePendingDays, $"a role changes only after {GroupMath.RolePendingDays} days of fitting better (took {days})");
            var flicker = new[] { new RoleState { role = GroupRole.Joker } };
            flicker = GroupMath.AssignRoles(changed, flicker);
            flicker = GroupMath.AssignRoles(new[] { Person(humour: 70) }, flicker);
            flicker = GroupMath.AssignRoles(changed, flicker);
            Check(flicker[0].role == GroupRole.Joker, "a day back in character resets the wait: no flip-flopping");
            var fresh = GroupMath.AssignRoles(new[] { Person(humour: 70) }, new[] { new RoleState { role = GroupRole.Newcomer } });
            Check(fresh[0].role == GroupRole.Joker, "a newcomer whose time is up takes their part at once");
            Check(GroupMath.AssignRoles(new[] { Person(warmth: 60, social: 15, tieShare: 1f, newcomer: true) }, new RoleState[1])[0].role == GroupRole.Newcomer,
                "a newcomer is the newcomer, however warm");
        }

        // ---------- Cohesion ----------
        {
            float c = 50f;
            float kept = GroupMath.NextCohesion(c, new CohesionDay { attachment = 0.3f, habits = 1, memories = 2 });
            float quarrels = GroupMath.NextCohesion(c, new CohesionDay { attachment = 0.3f, quarrels = 2 });
            float death = GroupMath.NextCohesion(c, new CohesionDay { attachment = 0.3f, died = 1 });
            float apart = GroupMath.NextCohesion(c, new CohesionDay { attachment = 0.3f, apart = true, apartDays = 5 });
            float plain = GroupMath.NextCohesion(c, new CohesionDay { attachment = 0.3f });
            Check(kept > plain && quarrels < plain && death < quarrels && apart < plain, $"habits and memories raise it; quarrels, a loss and separation lower it ({kept:0.0}/{plain:0.0}/{quarrels:0.0}/{death:0.0}/{apart:0.0})");
            Check(GroupMath.NextCohesion(99f, new CohesionDay { attachment = 1f, habits = 2, memories = 4, mends = 3 }) <= 100f
                  && GroupMath.NextCohesion(1f, new CohesionDay { attachment = -1f, quarrels = 4, died = 2 }) >= 0f, "cohesion stays within 0-100");
            float settle = 80f;
            for (int d = 0; d < 120; d++) settle = GroupMath.NextCohesion(settle, new CohesionDay { attachment = 0.1f });
            Check(Math.Abs(settle - (25f + 0.1f * 90f)) < 2f, $"left alone, cohesion settles where the members' attachment holds it ({settle:0.0})");
            Check(GroupMath.StartCohesion(0.6f) > GroupMath.StartCohesion(0.43f), "a closer knot starts closer");
        }

        // ---------- Splits ----------
        {
            Check(GroupMath.ShouldSplit(20f, GroupMath.SplitDays, 5, -0.3f), "a loose group with a rift splits");
            Check(!GroupMath.ShouldSplit(20f, GroupMath.SplitDays - 1, 5, -0.3f), "not on the first bad day");
            Check(!GroupMath.ShouldSplit(40f, GroupMath.SplitDays, 5, -0.3f), "not while it holds together");
            Check(!GroupMath.ShouldSplit(20f, GroupMath.SplitDays, 5, 0.1f), "not without a rift");
            Check(!GroupMath.ShouldSplit(20f, GroupMath.SplitDays, 3, -0.3f), "a group of three doesn't split, it loses someone");
            var m = Matrix(6, 0.2f);
            Set(m, 0, 5, -0.5f);
            Clique(m, 0.6f, 0, 1, 2);
            Clique(m, 0.6f, 3, 4, 5);
            GroupMath.Cut(m, new List<int> { 0, 1, 2, 3, 4, 5 }, out var a, out var b);
            Check(a.Contains(0) && b.Contains(5), "the two who pull apart most end up on different sides");
            Check(a.OrderBy(x => x).SequenceEqual(new[] { 0, 1, 2 }) && b.OrderBy(x => x).SequenceEqual(new[] { 3, 4, 5 }), $"everyone goes with the side they hold to ({string.Join(",", a)} | {string.Join(",", b)})");
        }

        // ---------- Merges ----------
        {
            Check(GroupMath.ShouldMerge(new[] { 1, 2, 3, 4 }, new[] { 3, 4, 5 }, 0f, 30f, 30f, false), "two groups sharing most of the smaller merge");
            Check(!GroupMath.ShouldMerge(new[] { 1, 2, 3, 4 }, new[] { 3, 4, 5 }, 0f, 30f, 30f, true), "rivals never merge");
            Check(!GroupMath.ShouldMerge(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 6, 7, 8, 9, 10 }, 0.6f, 80f, 80f, false), "not past nine people");
            Check(GroupMath.ShouldMerge(new[] { 1, 2, 3 }, new[] { 4, 5, 6 }, 0.5f, 60f, 60f, false), "two close groups that pull together merge");
            Check(!GroupMath.ShouldMerge(new[] { 1, 2, 3 }, new[] { 4, 5, 6 }, 0.5f, 30f, 60f, false), "not while one of them is loose");
            Check(!GroupMath.ShouldMerge(new[] { 1, 2, 3 }, new[] { 4, 5, 6 }, 0.2f, 60f, 60f, false), "not when they don't pull together");
        }

        // ---------- Newcomers ----------
        {
            float open = GroupMath.Welcome(40f, 40f, 0.25f, 60f);
            float closed = GroupMath.Welcome(-50f, -30f, -0.3f, 60f);
            Check(!GroupMath.ClosesRanks(open) && GroupMath.ClosesRanks(closed), $"a warm, trusting group welcomes; a wary, cold one closes ranks ({open:0.00}, {closed:0.00})");
            var day = new IntegrationDay { opinion = 10f, sharedWork = 0.5f };
            float pOpen = 0f, pClosed = 0f;
            int dOpen = 0, dClosed = 0;
            while (pOpen < GroupMath.Accepted && dOpen < 100) { pOpen = GroupMath.NextIntegration(pOpen, day, open); dOpen++; }
            while (pClosed < GroupMath.Accepted && dClosed < 100) { pClosed = GroupMath.NextIntegration(pClosed, day, closed); dClosed++; }
            Check(dOpen < dClosed && dOpen <= 20, $"an open group takes a newcomer in within weeks, a closed one far later ({dOpen} vs {dClosed} days)");
            float sponsored = GroupMath.NextIntegration(0f, new IntegrationDay { opinion = 10f, sponsor = true, welcomes = 1 }, open);
            Check(sponsored > GroupMath.NextIntegration(0f, day, open), "a sponsor and a welcome speed the way in");
            Check(GroupMath.NextIntegration(50f, new IntegrationDay { opinion = -50f }, open) < 50f, "being disliked sets a newcomer back");
        }

        // ---------- Between groups ----------
        {
            float r = 0f;
            for (int d = 0; d < 30; d++) r = GroupMath.NextRivalry(r, 2, 60f, 0f, 0f, 0);
            Check(r >= GroupMath.RivalryShows, $"feuds across two groups set them at odds ({r:0})");
            float cool = r;
            for (int d = 0; d < 40; d++) cool = GroupMath.NextRivalry(cool, 0, 0f, 0f, 0f, 0);
            Check(cool < GroupMath.RivalryEnds, $"with the feuds over, it cools ({cool:0})");
            Check(GroupMath.NextRivalry(10f, 0, 0f, 0f, 0f, 2) > GroupMath.NextRivalry(10f, 0, 0f, 0f, 0f, 0), "spats between them add to it");
            Check(GroupMath.NextRivalry(0f, 0, 0f, 1f, 1f, 0) > 0f, "views and ways at odds set groups against each other too");
        }

        // ---------- Ways and views ----------
        {
            Check(!GroupMath.NormHolds(false, 24f, 25f, true) && GroupMath.NormHolds(false, 25f, 25f, true), "a way takes its line to start");
            Check(GroupMath.NormHolds(true, 21f, 25f, true) && !GroupMath.NormHolds(true, 18f, 25f, true), "and lapses only a margin below it");
            Check(GroupMath.NormHolds(false, -30f, -25f, false) && GroupMath.NormHolds(true, -21f, -25f, false), "the same for a way on the low side");
            Check(GroupMath.SharedView(new[] { 0.6f, 0.5f, 0.4f }, 4) == 1, "most of them leaning for something is a shared view");
            Check(GroupMath.SharedView(new[] { -0.6f, -0.5f, -0.4f, -0.5f }, 5) == -1, "against, too");
            Check(GroupMath.SharedView(new[] { 0.6f, -0.5f, 0.4f }, 3) == 0, "a divided group shares no view");
            Check(GroupMath.SharedView(new[] { 0.9f }, 5) == 0, "one person's view isn't the group's");
            Check(GroupMath.Conform(0f, 0.6f, 0.02f) == 0.02f && GroupMath.Conform(0.59f, 0.6f, 0.02f) == 0.6f, "conformity is a small step, never past the view");
        }

        // ---------- The defs ----------
        var docs = Directory.GetFiles(Path.Combine(root, "Defs", "GroupDefs"), "*.xml").Select(f => XDocument.Load(f)).ToList();
        var all = docs.SelectMany(d => d.Root!.Elements()).ToList();
        var habits = all.Where(e => e.Name.LocalName == "ColonyCouncil.GroupHabitDef").Select(e => (string)e.Element("defName")).ToHashSet();
        var norms = all.Where(e => e.Name.LocalName == "ColonyCouncil.GroupNormDef").Select(e => (string)e.Element("defName")).ToHashSet();
        var kinds = all.Where(e => e.Name.LocalName == "ColonyCouncil.GroupKindDef").ToList();
        var pools = new HashSet<string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories))
        {
            try
            {
                foreach (var e in XDocument.Load(f).Root!.Elements("ColonyCouncil.FlavorPoolDef")) pools.Add((string)e.Element("defName"));
            }
            catch
            {
                // other tests report broken files
            }
        }
        Check(kinds.Count >= 20 && habits.Count >= 8 && norms.Count >= 15, $"kinds, habits and ways ({kinds.Count}, {habits.Count}, {norms.Count})");
        foreach (var k in kinds)
        {
            string name = (string)k.Element("defName");
            foreach (var h in k.Element("habits")?.Elements("li") ?? Enumerable.Empty<XElement>()) Check(habits.Contains(h.Value), $"{name}: habit {h.Value} exists");
            foreach (var n in k.Element("norms")?.Elements("li") ?? Enumerable.Empty<XElement>()) Check(norms.Contains(n.Value), $"{name}: way {n.Value} exists");
            var names = k.Element("names")?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
            Check(names.Count >= 3, $"{name}: at least three names to choose from");
            Check(names.Any(x => !x.StartsWith("[")), $"{name}: a name for any era");
            if ((string)k.Element("binds") == "Friends") Check(names.All(x => x.Contains("{ANCHOR}")), $"{name}: named after its anchor");
        }
        foreach (var e in all.Where(e => e.Name.LocalName == "ColonyCouncil.GroupNormDef"))
        {
            string c = (string)e.Element("clashes");
            if (!string.IsNullOrEmpty(c)) Check(norms.Contains(c), $"{(string)e.Element("defName")}: clashes with a way that exists ({c})");
            string h = (string)e.Element("habit");
            if (!string.IsNullOrEmpty(h)) Check(habits.Contains(h), $"{(string)e.Element("defName")}: brings a habit that exists ({h})");
        }
        foreach (var e in all.Where(e => e.Name.LocalName == "ColonyCouncil.GroupHabitDef"))
        {
            string pool = (string)e.Element("pool");
            Check(pools.Contains(pool), $"{(string)e.Element("defName")}: pool {pool} exists");
            // Spoken aloud (and said at gatherings, which don't fill in names): no names in the lines.
            var lines = docs.SelectMany(d => d.Root!.Elements("ColonyCouncil.FlavorPoolDef")).Where(p => (string)p.Element("defName") == pool)
                .SelectMany(p => p.Descendants("li")).Select(l => l.Value).ToList();
            Check(lines.Count >= 15 && lines.All(l => !l.Contains("{")), $"{pool}: fifteen lines or more, none with names");
        }
        var moments = all.Where(e => e.Name.LocalName == "InteractionDef" && e.Attribute("Abstract") == null).ToList();
        Check(moments.Count >= 10, $"group moments ({moments.Count})");
        foreach (var e in moments)
        {
            var ext = e.Descendants("li").FirstOrDefault(x => (string)x.Attribute("Class") == "ColonyCouncil.GroupMomentExtension");
            Check(ext != null, $"{(string)e.Element("defName")}: has its group conditions");
            int lines = e.Descendants("li").Count(x => x.Value.StartsWith("r_logentry->"));
            Check(lines >= 5, $"{(string)e.Element("defName")}: five ways to tell it at least ({lines})");
        }
        return cases;
    }
}
