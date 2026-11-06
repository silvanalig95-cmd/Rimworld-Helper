using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Phase 4 of the review, part 2: a fairer council (the player's decisions 8, 9, 10 and 13 of 6 November 2026). The seats'
/// names by the colony's age (Offices/OfficeNamesMath.cs): every seat named in every age, a shipped line said with the
/// age's name and its article put right, and nothing else in it touched.
/// </summary>
public static class FairCouncilTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "fair council: " + what);
        }

        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f).Root!).ToList();
        var keyed = Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements())
            .GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => g.First().Value);
        Names(defs, Check);
        Records(root, defs, keyed, Check);
        Goals(root, Check);
        Groups(root, Check);
        Decrees(root, defs, keyed, Check);
        return cases;
    }

    // ---------------- Decision 13: groups around real divides ----------------

    private static void Groups(string root, Action<bool, string> Check)
    {
        Check(GroupMath.MaxFor(12) == 5 && GroupMath.MaxFor(6) == GroupMath.MinSize && GroupMath.MaxFor(20) == GroupMath.MaxSize, "a group holds well under half the colony");
        var flat = new float[3, 3];
        for (int i = 0; i < 3; i++)
        for (int j = 0; j < 3; j++)
            flat[i, j] = i == j ? 0f : 0.6f;
        var d = GroupMath.Distinct(flat, 3);
        Check(Math.Abs(d[0, 1] - 0.6f * (1f - GroupMath.CommonShare)) < 0.001f && d[1, 1] == 0f, "what everyone shares binds nobody in particular");
        var cold = new float[2, 2] { { 0f, -0.4f }, { -0.4f, 0f } };
        Check(GroupMath.Distinct(cold, 2)[0, 1] == -0.4f, "a colony at odds isn't pulled further apart");
        Check(GroupMath.WhyOutside(false, true, 3, false) == GroupMath.Outside.AtOdds && GroupMath.WhyOutside(false, true, 0, true) == GroupMath.Outside.New
              && GroupMath.WhyOutside(false, true, 0, false) == GroupMath.Outside.Own, "outside every group: at odds, new, or their own");
        Check(GroupMath.WhyOutside(true, true, 3, false) == GroupMath.Outside.None && GroupMath.WhyOutside(false, false, 3, false) == GroupMath.Outside.None,
            "nobody is outside when they're in one, or when there are none");

        // The player's save, day 128: twelve who all fought side by side and think well of each other; four close friends,
        // three more; Barr at odds with three of them.
        const int n = 12, barr = 11;
        var aff = new float[n, n];
        var a = new HashSet<int> { 0, 1, 2, 3 };
        var b = new HashSet<int> { 4, 5, 6 };
        var feud = new HashSet<int> { 0, 4, 5 };
        for (int i = 0; i < n; i++)
        for (int j = i + 1; j < n; j++)
        {
            var f = new PairFacts { comrades = 70f, minOpinion = 40, likeness = 0.7f };
            if (a.Contains(i) && a.Contains(j)) f.friends = 80f;
            if (b.Contains(i) && b.Contains(j)) f.friends = 70f;
            if (j == barr && feud.Contains(i))
            {
                f.rivals = 40f;
                f.minOpinion = -20;
            }
            aff[i, j] = aff[j, i] = GroupMath.Affinity(f);
        }
        var free = Enumerable.Repeat(true, n).ToArray();
        var before = GroupMath.Cluster(aff, n, free, null);
        Check(before != null && before.Count == GroupMath.MaxSize, $"the old rule: everyone's comradeship makes one group of seven ({before?.Count})");
        var distinct = GroupMath.Distinct(aff, n);
        var now = GroupMath.Cluster(distinct, n, free, null, GroupMath.MaxFor(n));
        Check(now != null && now.Count <= GroupMath.MaxFor(n) && now.All(a.Contains), $"now: the close friends form a group of their own ({string.Join(",", now ?? new List<int>())})");
        var groups = new List<List<int>> { now ?? new List<int>() };
        var free2 = free.Select((_, i) => !(now ?? new List<int>()).Contains(i)).ToArray();
        var second = GroupMath.Cluster(distinct, n, free2, groups, GroupMath.MaxFor(n));
        Check(second != null && second.All(b.Contains), $"and the other friends theirs ({string.Join(",", second ?? new List<int>())})");
        if (second != null) groups.Add(second);
        Check(GroupMath.JoinTarget(distinct, barr, groups, null, GroupMath.MaxFor(n)) == -1, "Barr joins neither: at odds with some in each, outside both");
        Check(Enumerable.Range(7, 4).All(i => GroupMath.JoinTarget(distinct, i, groups, null, GroupMath.MaxFor(n)) == -1),
            "those who only fought beside them don't join a group of close friends");

        string life = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Social", "Groups", "GroupLife.cs"));
        Check(life.Contains("GroupMath.Cluster(f.distinct") && life.Contains("GroupMath.JoinTarget(f.distinct") && life.Contains("GroupMath.Attachment(f.distinct"),
            "groups form, take people and keep them by what binds some more than the rest");
        Check(life.Contains("\"CC_Group_Regroup\"") && life.Contains("t.Regrouping"), "an older save's groups regroup quietly, told once");
        string table = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "UI", "PeopleTable.cs"));
        string bonds = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "UI", "SocialPanel.cs"));
        Check(table.Contains("CC_Group_Outside_Short") && bonds.Contains("CC_Social_OutsideAll"), "someone outside every group shows as outside on the People table and the Bonds page");
    }

    // ---------------- Decision 13: decrees that mean distinct things ----------------

    private static void Decrees(string root, List<XElement> defs, Dictionary<string, string> keyed, Action<bool, string> Check)
    {
        Check(DecreeMath.FormNow(DecreeMath.Drills, true, false, false) == DecreeMath.Militia && DecreeMath.FormNow(DecreeMath.Militia, false, false, false) == DecreeMath.Drills,
            "the drills take the age's form: the muster for a tribe or a village, drills for the rest");
        Check(DecreeMath.FormNow(DecreeMath.Rationing, false, true, false) == DecreeMath.FamineBread && DecreeMath.FormNow(DecreeMath.FamineBread, false, true, true) == DecreeMath.Rationing
              && DecreeMath.FormNow(DecreeMath.FamineBread, true, false, false) == DecreeMath.Rationing,
            "rationing shares with the hungry while a famine lies over the region, unless the colony's own stores are low");
        Check(DecreeMath.FormNow("Council_Decree_Tithes", true, true, true) == "Council_Decree_Tithes", "a decree of its own is itself");
        Check(DecreeMath.Excludes(DecreeMath.Drills, DecreeMath.Militia) && DecreeMath.Excludes("Council_Decree_FeastDay", "Council_Decree_Festival")
              && DecreeMath.Excludes("Council_Decree_Curfew", "Council_Decree_OnGuard") && DecreeMath.Excludes("Council_Decree_Rest", "Council_Decree_QuietHours"),
            "an idea's two forms never run together, nor two that overlap");
        Check(!DecreeMath.Excludes("Council_Decree_SickBay", "Council_Decree_Quarantine") && !DecreeMath.Excludes(DecreeMath.Drills, DecreeMath.Drills)
              && !DecreeMath.Excludes("Council_Decree_Austerity", DecreeMath.Rationing), "rest when ill and quarantine may run together: one heals, the other keeps the well");
        Check(DecreeMath.CarriedEnd(1000, 1000 + 10 * 60000, 5f, 60000) == 1000 + 5 * 60000 && DecreeMath.CarriedEnd(1000, 1000 + 2 * 60000, 5f, 60000) == 1000 + 2 * 60000,
            "an older save's militia carries on as drills for what was left, never longer than drills run");
        var decrees = defs.SelectMany(x => x.Elements("ColonyCouncil.DecreeDef")).Select(x => x.Element("defName")!.Value).ToHashSet();
        foreach (var idea in DecreeMath.Ideas.Concat(DecreeMath.Apart))
            Check(idea.All(decrees.Contains), $"the decrees named are there: {string.Join(", ", idea)}");
        var drills = defs.SelectMany(x => x.Elements("ColonyCouncil.DecreeDef")).Where(x => x.Element("defName")!.Value == DecreeMath.Drills || x.Element("defName")!.Value == DecreeMath.Militia).ToList();
        Check(drills.Count == 2 && drills.All(x => x.Element("hurts") != null), "both forms of the drills keep their risk (the drills' bouts, the militia's muster)");
        Check(keyed.ContainsKey("CC_Decree_Overlaps"), "a decree refused for an overlap says which");
        string work = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Decrees", "DecreeWork.cs"));
        string trk = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Decrees", "Decrees.cs"));
        Check(work.Contains("if (FormNow(d, Home) != d.defName) return true;") && trk.Contains("DecreeMath.Excludes(a.def.defName, def.defName)") && trk.Contains("CarryOn(comp);"),
            "only the form of the moment is listed, overlaps are refused, an older save's carry on");
    }

    // ---------------- Decision 8: life goals that last ----------------

    private static void Goals(string root, Action<bool, string> Check)
    {
        Check(GoalMath.MayChangeHeart(25, 0.1f, false, false, "loss") && GoalMath.MayChangeHeart(25, 0.1f, false, false, "downed"), "a loss or a brush with death may change a goal held a while");
        Check(!GoalMath.MayChangeHeart(25, 0.1f, false, false, "battle") && !GoalMath.MayChangeHeart(25, 0.1f, false, false, "thought:AteFineMeal"), "a fight or a fine meal never does");
        Check(!GoalMath.MayChangeHeart(12, 0.1f, false, false, "loss"), "a goal holds its first weeks whatever happens (it was ten days)");
        Check(!GoalMath.MayChangeHeart(25, 0.5f, false, false, "loss"), "a goal well under way is theirs");
        Check(!GoalMath.MayChangeHeart(25, 0.1f, false, true, "loss") && !GoalMath.MayChangeHeart(25, 0.1f, true, false, "loss"),
            "once per goal, and never a goal taken up for someone who died");
        Check(GoalMath.SparkPull("battle") < GoalMath.SparkPull("loss") && GoalMath.SparkPull("battle") < GoalMath.SparkPull("thought:AttendedWedding")
              && GoalMath.SparkPull(null) == 1f, "a fight pulls less than the rest of life");
        Check(GoalMath.SparkPull("battle") == 0.7f, "a fight pulls 0.7 (it was 0.4: the player's word of 6 November 2026, \"not 0\")");
        Check(GoalMath.Shared(0) == 1f && GoalMath.Shared(1) == 0.5f && GoalMath.Shared(2) < 0.34f, "a goal others hold weighs less");
        Check(GoalMath.FightMaySpark("battle", 0) && !GoalMath.FightMaySpark("battle", 1) && GoalMath.FightMaySpark("loss", 2) && GoalMath.FightMaySpark(null, 1),
            "a fight sparks no goal someone else at home took up after a fight and is still at; any other spark may");

        // Those who fought hardest: the three who brought down the most, anyone downed in it, whoever carried someone out.
        var none = new bool[6];
        Check(GoalMath.Hardest(new[] { 0, 3, 1, 3, 2, 1 }, none, none).SequenceEqual(new[] { 1, 3, 4 }), "the three who brought down the most, in their order");
        Check(GoalMath.Hardest(new[] { 1, 1, 1, 1, 0, 0 }, none, none).SequenceEqual(new[] { 0, 1, 2 }), "of equals, the first noted");
        Check(GoalMath.Hardest(new[] { 0, 0, 0, 0, 0, 0 }, none, none).Count == 0 && GoalMath.Hardest(new int[0], null, null).Count == 0,
            "nobody for bringing down nobody");
        Check(GoalMath.Hardest(new[] { 2, 0, 0, 1, 0, 0 }, new[] { false, false, true, false, false, false }, new[] { false, false, false, false, false, true })
              .SequenceEqual(new[] { 0, 2, 3, 5 }), "and anyone downed in it, and whoever carried someone out, though they brought down nobody");
        Check(GoalMath.Hardest(new[] { 4, 3, 2, 1, 0, 0 }, new[] { true, false, false, false, false, false }, none).SequenceEqual(new[] { 0, 1, 2 }),
            "one downed among the three counts once");

        // A colony of twelve over the save's 88 days, fighting as often as the save's: the rules before 0.112.0, 0.112.0's
        // (one fighter, pulling 0.4) and now, the same luck for each, fifty runs. Before 0.112.0 fighting goals were 23 of
        // the save's 47 (25 of its first 38), and one colonist changed hers four times in 25 days; 0.112.0's left none.
        const int runs = 50;
        var before = SimulateMany(GoalRules.Before, true, runs);
        var one = SimulateMany(GoalRules.OneFighter, true, runs);
        var now = SimulateMany(GoalRules.Now, true, runs);
        var fresh = SimulateMany(GoalRules.Now, false, runs);
        string Share(GoalRun r) => $"{r.battle} of {r.all} ({100f * r.battle / Math.Max(1, r.all):0}%)";
        Console.WriteLine($"  life goals, twelve people, 88 days, a fight every six days, {runs} runs: from fights {Share(before)} before 0.112.0, {Share(one)} on 0.112.0's rule, "
                          + $"{Share(now)} now; a fresh colony now {Share(fresh)}; changes of heart {before.changes / (float)runs:0.0} a run before, {now.changes / (float)runs:0.0} now");
        Check(before.battle * 5 >= before.all * 2, $"the rules before 0.112.0 fill the colony with fighting goals ({Share(before)})");
        Check(one.battle * 20 <= one.all, $"0.112.0's rule left next to none from fights ({Share(one)})");
        Check(now.battle * 6 >= now.all && now.battle * 4 <= now.all + now.all / 5,
            $"now about a fifth to a quarter of goals come from fights in a colony under way ({Share(now)})");
        Check(fresh.battle > 0 && fresh.battle * 4 <= fresh.all, $"a fresh colony's first goals mostly come before its first fight ({Share(fresh)})");
        Check(now.sameAfterAFight <= 1 && now.heldAfterAFight <= 1 && before.heldAfterAFight >= 3,
            $"one fighting goal is never chosen by several at once (now at most {now.heldAfterAFight} holding one from a fight, before {before.heldAfterAFight})");
        Check(before.mostChanges >= 2 && now.mostChanges <= 1 && now.changes * 2 <= runs * 3,
            $"changes of heart stay rare: in any 25 days at most {now.mostChanges} for one colonist (before {before.mostChanges}), {now.changes} in {runs} runs (before {before.changes})");

        string src = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Social", "SocialWeb.cs"));
        Check(src.Contains("GoalMath.Hardest(") && src.Contains("foreach (var p in hardest) comp.aspirations.Notify_Event(p, \"battle\");")
              && src.Contains("comp.aspirations.HappenedSince(p, \"downed\", since)") && src.Contains("FightSides.Carried(p)"),
            "a fight's spark goes to those who fought hardest, the downed and those who carried someone out read before the fight is cleared");
        Check(src.IndexOf("HardestIn(comp, b, list, began, map)", StringComparison.Ordinal) < src.IndexOf("if (!AnyBattle) FightSides.Clear();", StringComparison.Ordinal),
            "who carried whom is read before it is cleared");
        string asp = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Aspirations", "Aspirations.cs"));
        Check(asp.Contains("GoalMath.MayChangeHeart(") && asp.Contains("b.turned = true;") && asp.Contains("GoalMath.Shared(") && asp.Contains("GoalMath.FightMaySpark(spark, othersAfterAFight)")
              && asp.Contains("if (GoalMath.FightSpark(a.spark)) afterAFight[a.def]"), "the tracker follows these rules (a fight's spark, or a war's, People4)");
    }

    /// <summary>The rules a model of the colony's life goals runs on: before 0.112.0, 0.112.0's, and now.</summary>
    private enum GoalRules
    {
        Before,
        OneFighter,
        Now
    }

    /// <summary>What a model of the colony's life goals counted, over one run or several.</summary>
    private struct GoalRun
    {
        /// <summary>Goals chosen after a fight, and all goals chosen.</summary>
        public int battle, all;
        /// <summary>Changes of heart, and the most one colonist had within 25 days.</summary>
        public int changes, mostChanges;
        /// <summary>The most who took up one goal after the same fight, and the most holding one goal from fights at once.</summary>
        public int sameAfterAFight, heldAfterAFight;
    }

    private static GoalRun SimulateMany(GoalRules rules, bool underway, int runs)
    {
        var sum = new GoalRun();
        for (int seed = 0; seed < runs; seed++)
        {
            var r = Simulate(rules, underway, seed);
            sum.battle += r.battle;
            sum.all += r.all;
            sum.changes += r.changes;
            sum.mostChanges = Math.Max(sum.mostChanges, r.mostChanges);
            sum.sameAfterAFight = Math.Max(sum.sameAfterAFight, r.sameAfterAFight);
            sum.heldAfterAFight = Math.Max(sum.heldAfterAFight, r.heldAfterAFight);
        }
        return sum;
    }

    /// <summary>
    /// Twelve people, 88 days, fighting as often as the player's save (21 fights in its 128 days, so one every six days):
    /// nine fight, each bringing down none to two, one is downed and another carries them out; a death every 40 days
    /// (three grieve); and the rest of life's sparks now and then (a wedding, a fine meal, left out: one in ten days for
    /// each, which with these rules before 0.112.0 gives the save's share, half the goals from fights and a third from the
    /// rest of life). A spark is looked at four days. A goal is reached in 15 to 90 days, then 20 days pass before the next
    /// (TryChoose). Twelve goals: four for fights, four for the rest of life, four for a loss or a brush with death. A colony
    /// under way (each of the twelve holding a goal part done, as on the save) or a fresh one (nobody holding one yet).
    /// </summary>
    private static GoalRun Simulate(GoalRules rules, bool underway, int seed)
    {
        var rng = new Random(seed);
        const int people = 12, days = 88, fightEvery = 6, fighters = 9;
        bool before = rules == GoalRules.Before;
        // Goal kinds: 0-3 fights (spark battle), 4-7 the rest of life (spark life), 8-11 big things (spark loss/downed).
        string SparkOf(int g) => g < 4 ? "battle" : g < 8 ? "life" : "loss";
        float Pull(string spark) => before || spark == null ? 1f : rules == GoalRules.OneFighter && spark == "battle" ? 0.4f : GoalMath.SparkPull(spark);
        var goal = new int[people];
        var why = new string[people];
        var started = new int[people];
        var length = new int[people];
        var doneAt = new int[people];
        var turned = new bool[people];
        var lastEvent = new Dictionary<(int, string), int>();
        var changesAt = new List<int>[people];
        for (int i = 0; i < people; i++)
        {
            goal[i] = -1;
            doneAt[i] = -999;
            changesAt[i] = new List<int>();
            if (!underway) continue;
            length[i] = rng.Next(15, 91);
            started[i] = -rng.Next(0, length[i]);
            goal[i] = rng.Next(0, 12);
        }
        var run = new GoalRun();
        var afterFight = new Dictionary<(int fight, int goal), int>();
        int lastFight = -1;
        for (int day = 1; day <= days; day++)
        {
            if (day % fightEvery == 0)
            {
                var kills = Enumerable.Range(0, fighters).Select(_ => rng.Next(0, 3)).ToList();
                int down = rng.Next(0, fighters), carrier = (down + 1 + rng.Next(0, fighters - 1)) % fighters;
                var downed = Enumerable.Range(0, fighters).Select(i => i == down).ToList();
                var carried = Enumerable.Range(0, fighters).Select(i => i == carrier).ToList();
                IEnumerable<int> sparked = rules switch
                {
                    GoalRules.Before => Enumerable.Range(0, fighters),
                    // 0.112.0: only the one who brought down the most, the first of equals.
                    GoalRules.OneFighter => kills.Max() > 0 ? new[] { kills.IndexOf(kills.Max()) } : new int[0],
                    _ => GoalMath.Hardest(kills, downed, carried)
                };
                foreach (int i in sparked) lastEvent[(i, "battle")] = day;
                lastEvent[(down, "loss")] = day;
                lastFight = day;
            }
            if (day % 40 == 0)
                for (int k = 0; k < 3; k++) lastEvent[(rng.Next(0, people), "loss")] = day;
            for (int i = 0; i < people; i++)
            {
                if (rng.NextDouble() < 0.1) lastEvent[(i, "life")] = day;
                bool Recent(string s) => lastEvent.TryGetValue((i, s), out int d) && day - d < 4;
                // Others at home holding a goal, and holding it after a fight.
                int Held(int g) => Enumerable.Range(0, people).Count(j => j != i && goal[j] == g);
                int HeldAfterAFight(int g) => Enumerable.Range(0, people).Count(j => j != i && goal[j] == g && why[j] == "battle");
                bool May(int g) => before || rules == GoalRules.OneFighter || GoalMath.FightMaySpark(SparkOf(g), HeldAfterAFight(g));
                var options = Enumerable.Range(0, 12).Where(g => g != goal[i]).Select(g => (g, spark: Recent(SparkOf(g)) && May(g) ? SparkOf(g) : null)).ToList();
                float Weight((int g, string spark) o, bool own)
                {
                    float w = before ? 1f : Pull(o.spark) * GoalMath.Shared(Held(o.g));
                    // In their own time, a goal sparked lately is six times likelier (times its pull).
                    return own && o.spark != null ? w * 6f * Pull(o.spark) : w;
                }
                (int g, string spark) Pick(List<(int g, string spark)> from, bool own)
                {
                    float total = from.Sum(o => Weight(o, own)), r = (float)rng.NextDouble() * total;
                    foreach (var o in from)
                    {
                        r -= Weight(o, own);
                        if (r <= 0f) return o;
                    }
                    return from[from.Count - 1];
                }
                void Begin((int g, string spark) o)
                {
                    goal[i] = o.g;
                    why[i] = o.spark;
                    started[i] = day;
                    length[i] = rng.Next(15, 91);
                    run.all++;
                    if (o.spark != "battle") return;
                    run.battle++;
                    var key = (lastFight, o.g);
                    afterFight[key] = (afterFight.TryGetValue(key, out int n) ? n : 0) + 1;
                    run.sameAfterAFight = Math.Max(run.sameAfterAFight, afterFight[key]);
                    run.heldAfterAFight = Math.Max(run.heldAfterAFight, 1 + HeldAfterAFight(o.g));
                }
                var sparkedNow = options.Where(o => o.spark != null).ToList();
                if (goal[i] < 0)
                {
                    if (day - doneAt[i] < 20) continue;
                    float chance = (before ? 0.6f : GoalMath.SparkedChooseChance) * (sparkedNow.Count == 0 ? 0f : sparkedNow.Max(o => Pull(o.spark)));
                    if (sparkedNow.Count > 0 && rng.NextDouble() < chance) Begin(Pick(sparkedNow, false));
                    else if (rng.NextDouble() < 0.2) Begin(Pick(options, true));
                    continue;
                }
                int held = day - started[i];
                if (held >= length[i])
                {
                    goal[i] = -1;
                    why[i] = null;
                    doneAt[i] = day;
                    turned[i] = false;
                    continue;
                }
                float progress = held / (float)length[i];
                // Before 0.112.0 any spark changed a goal ten days old; now only a big one, once, after twenty days.
                var from = before
                    ? (held >= 10 && progress < 0.35f ? sparkedNow : new List<(int g, string spark)>())
                    : sparkedNow.Where(o => GoalMath.MayChangeHeart(held, progress, false, turned[i], o.spark)).ToList();
                if (from.Count == 0 || rng.NextDouble() >= (before ? 0.25 : GoalMath.ChangeChance)) continue;
                Begin(Pick(from, false));
                turned[i] = !before;
                changesAt[i].Add(day);
                run.changes++;
            }
        }
        foreach (var c in changesAt)
            for (int k = 0; k < c.Count; k++)
                run.mostChanges = Math.Max(run.mostChanges, c.Count(d => d >= c[k] && d < c[k] + 25));
        return run;
    }

    // ---------------- Decision 9: elections on a record ----------------

    private static void Records(string root, List<XElement> defs, Dictionary<string, string> keyed, Action<bool, string> Check)
    {
        // Judging a promise: from what the map showed then and shows now.
        Check(RecordMath.Judge(PromiseTest.Lost, 3, 3, 50) == PromiseVerdict.Kept && RecordMath.Judge(PromiseTest.Lost, 3, 5, 50) == PromiseVerdict.Broken,
            "a proper watch: nobody lost since is kept, two lost is broken");
        Check(RecordMath.Judge(PromiseTest.Lost, 3, 5, 6) == PromiseVerdict.None, "a promise made days ago isn't judged yet");
        Check(RecordMath.Judge(PromiseTest.Food, 2, 9, 50) == PromiseVerdict.Kept && RecordMath.Judge(PromiseTest.Food, 6, 2, 50) == PromiseVerdict.Broken
              && RecordMath.Judge(PromiseTest.Food, 6, 4, 50) == PromiseVerdict.None, "meals put by: the stores held, ran low, or nothing clear");
        Check(RecordMath.Judge(PromiseTest.Trust, 10, 9.5f, 50) == PromiseVerdict.Kept && RecordMath.Judge(PromiseTest.Trust, 10, 6, 50) == PromiseVerdict.Broken
              && RecordMath.Judge(PromiseTest.Trust, 10, 8, 50) == PromiseVerdict.None, "trust held, fell, or barely moved");
        Check(RecordMath.Judge(PromiseTest.Quarrels, 2, 2, 50) == PromiseVerdict.Kept && RecordMath.Judge(PromiseTest.Quarrels, 2, 3, 50) == PromiseVerdict.Broken, "quarrels");
        Check(RecordMath.Judge(PromiseTest.Sickbeds, 0, 2, 50) == PromiseVerdict.Kept && RecordMath.Judge(PromiseTest.Sickbeds, 0, 0, 50) == PromiseVerdict.Broken, "a proper place for the sick");
        Check(RecordMath.Judge(PromiseTest.Work, 10, 33, 50) == PromiseVerdict.Kept && RecordMath.Judge(PromiseTest.Work, 10, 10, 50) == PromiseVerdict.None,
            "the seat's work: some is kept, none says nothing (no trader may have come)");
        Check(RecordMath.Judge(PromiseTest.None, 0, 0, 50) == PromiseVerdict.None, "words only are never judged");
        Check(RecordMath.Lean(PromiseVerdict.Kept) > 0f && RecordMath.Lean(PromiseVerdict.Broken) < 0f && RecordMath.Lean(PromiseVerdict.None) == 0f, "a kept promise helps, a broken one costs");
        Check(RecordMath.Lean(PromiseVerdict.Kept) < ElectionMath.VoteNoise && -RecordMath.Lean(PromiseVerdict.Broken) < ElectionMath.VoteNoise,
            "within the luck of the day: a record weighs, it doesn't decide alone");
        Check(RecordMath.EvidenceKey(PromiseTest.Lost, PromiseVerdict.Broken) == "CC_Promise_Broken_Lost" && RecordMath.EvidenceKey(PromiseTest.Food, PromiseVerdict.None) == null, "the evidence's words");
        Check(RecordMath.EvidenceCount(PromiseTest.Lost, 3, 5) == 2 && RecordMath.EvidenceCount(PromiseTest.Food, 3, 5) == 0, "the evidence counts the lost and the work");

        // The values kept in the history, back again.
        var v = RecordMath.Values("Council_Office_Marshal", 0, "watch", PromiseTest.Lost, 4f, 17f, "a proper watch at night");
        Check(RecordMath.Get(v, RecordMath.OfficeAt) == "Council_Office_Marshal" && RecordMath.TestOf(v) == PromiseTest.Lost && RecordMath.Number(v, RecordMath.ReadingAt) == 4f
              && RecordMath.Number(v, RecordMath.WorkAt) == 17f && RecordMath.Get(v, RecordMath.PromiseAt) == "a proper watch at night" && RecordMath.SeatOf(v) == 0, "a promise's values read back");
        Check(RecordMath.TestOf(new List<string>()) == PromiseTest.None && RecordMath.Get(null, 2) == null, "no values, nothing judged");

        // The record a candidate runs on.
        Check(RecordMath.Record(true, 23, 140, true, true) == RecordMath.RecordKind.InSeat, "the holder runs on their work in the seat");
        Check(RecordMath.Record(true, 0, 140, true, true) == RecordMath.RecordKind.ForColony && RecordMath.Record(false, 23, 140, false, false) == RecordMath.RecordKind.ForColony,
            "else on the seat's kind of work for the colony");
        Check(RecordMath.Record(false, -1, 3, true, true) == RecordMath.RecordKind.Name && RecordMath.Record(false, -1, 3, false, true) == RecordMath.RecordKind.Goal
              && RecordMath.Record(false, -1, 3, false, false) == RecordMath.RecordKind.None, "else a name the colony gave them, a life goal reached, or nothing");
        Check(RecordMath.WorkLeanOf(true, 5) > 0f && RecordMath.WorkLeanOf(false, 5) == 0f && RecordMath.WorkLeanOf(true, 0) == 0f, "work in the seat helps its holder");

        // A seat's promise and its judgement are the holder's record, never the chronicle's.
        var promise = new HistoryEntry { kind = RecordMath.KindPromise, line = "Alo took the seat of doctor on a promise.", weight = HistoryMath.Milestone,
            actors = new List<HistoryActor> { new HistoryActor(Ref.Person(5), HistoryRole.About) } };
        Check(!ChronicleMath.Shows(promise, r => true), "a promise isn't in the chronicle");
        Check(!ChronicleMath.Shows(new HistoryEntry { kind = RecordMath.KindBroken, line = "x", weight = HistoryMath.Milestone }, r => true), "nor a promise judged");

        // The defs: every promise a seat can run on says what it promises, and how it's judged has its words.
        var words = defs.SelectMany(d => d.Elements("ColonyCouncil.OfficeWordsDef")).ToList();
        int planks = 0;
        foreach (var o in defs.SelectMany(d => d.Elements("ColonyCouncil.OfficeDef")))
        {
            string name = o.Element("defName")!.Value;
            var w = words.First(x => (string?)x.Element("office") == name);
            var own = o.Element("planks")?.Elements("li").Select(p => p.Element("key")!.Value).ToList() ?? new List<string>();
            var promised = w.Element("promises")?.Elements("li").ToDictionary(p => p.Element("key")!.Value) ?? new Dictionary<string, XElement>();
            foreach (var k in own) Check(promised.ContainsKey(k), $"{name}: its own plank {k} says what it promises");
            var added = w.Element("planks")?.Elements("li").ToList() ?? new List<XElement>();
            var all = promised.Values.Concat(added).ToList();
            planks += own.Count + added.Count;
            int always = o.Element("planks")!.Elements("li").Count(p => (p.Element("when")?.Value ?? "Always") == "Always")
                         + added.Count(p => (p.Element("when")?.Value ?? "Always") == "Always");
            Check(always >= 3, $"{name}: three promises or more always fit, so three candidates never make the same one ({always})");
            foreach (var p in all)
            {
                string pr = p.Element("promise")?.Value ?? "", test = p.Element("test")?.Value ?? "None";
                Check(pr.Length > 0 && pr == pr.TrimEnd('.') && char.IsLower(pr[0]) && !pr.Contains('!'), $"{name}: \"{pr}\" reads after \"promised\"");
                Check(Enum.TryParse<PromiseTest>(test, out var t), $"{name}: the test {test} is known");
                if (t == PromiseTest.None) continue;
                Check(keyed.ContainsKey("CC_Promise_Kept_" + test), $"{name}: a kept {test} has its words");
                if (t != PromiseTest.Work) Check(keyed.ContainsKey("CC_Promise_Broken_" + test), $"{name}: a broken {test} has its words");
                if (t == PromiseTest.Work) Check(w.Element("measure") != null, $"{name}: a promise judged by the seat's work needs the seat's measure");
            }
        }
        Check(planks >= 50, $"more promises to run on ({planks}, from 30)");
        foreach (var k in keyed.Keys.Where(k => k.StartsWith("CC_Promise_") && (k.Contains("_Kept_") || k.Contains("_Broken_"))))
            Check(!keyed[k].EndsWith(".") && (char.IsLower(keyed[k][0]) || keyed[k].StartsWith("{") || keyed[k].StartsWith("I ")), $"{k}: a clause after \"and\"");
        var pools = defs.SelectMany(d => d.Elements("ColonyCouncil.FlavorPoolDef")).ToDictionary(p => p.Element("defName")!.Value, p => p.Descendants("li").Select(li => li.Value).ToList());
        foreach (var pool in new[] { "Rimfolk_Pool_HustingsKept", "Rimfolk_Pool_HustingsBroken" })
        {
            Check(pools.TryGetValue(pool, out var lines) && lines.Count >= 4, $"{pool} has lines");
            foreach (var l in lines ?? new List<string>())
                Check(l.Contains("{PROMISE}") && l.Contains("{EVIDENCE}") && l.IndexOf(". ", StringComparison.Ordinal) < 0 && !l.Contains('!'), $"{pool}: one sentence that says the promise and what came of it: {l}");
        }
        string src = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Offices", "Offices.cs"));
        Check(src.Contains("+ RecordLean(office, voter, candidate)"), "the record enters the vote as one term");
        string run = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "Offices", "OfficesElection.cs"));
        Check(run.Contains("if (!byElection) JudgeRace(race);") && run.Contains("Judged(sc.race);") && run.Contains("Promised(sc.race.office"),
            "the yearly election judges the last promise, the results tell it and keep the new one");
    }

    // ---------------- Decision 10: office names by the colony's age ----------------

    private static void Names(List<XElement> defs, Action<bool, string> Check)
    {
        var offices = defs.SelectMany(d => d.Elements("ColonyCouncil.OfficeDef")).Select(o => o.Element("defName")!.Value).ToList();
        var words = defs.SelectMany(d => d.Elements("ColonyCouncil.OfficeWordsDef")).ToList();
        foreach (var o in offices)
        {
            var w = words.FirstOrDefault(x => (string?)x.Element("office") == o);
            Check(w != null, $"{o} has its words");
            if (w == null) continue;
            var names = new[] { "tribal", "medieval", "industrial", "spacer" }.Select(a => (string?)w.Element(a) ?? "").ToList();
            Check(names.All(n => n.Length > 0 && n == n.ToLowerInvariant() && !n.Contains('!')), $"{o} has a name in every age, plain and lower case ({string.Join(", ", names)})");
            Check(names[0] != names[3] || o.EndsWith("Chaplain"), $"{o}: a tribal colony and a spacer one don't call the seat the same");
            string label = defs.SelectMany(d => d.Elements("ColonyCouncil.OfficeDef")).First(x => x.Element("defName")!.Value == o).Element("label")!.Value;
            Check(names[1] == label, $"{o}: the medieval name is the seat's own label, so a medieval colony reads what it always did");
        }
        var spacer = words.Select(w => (string?)w.Element("spacer")).ToList();
        Check(!spacer.Contains("reeve"), "a spacer colony doesn't elect a reeve");
        Check(spacer.Distinct().Count() == spacer.Count, "no two seats share a name in the spacer age");
        var industrial = words.Select(w => (string?)w.Element("industrial")).ToList();
        Check(industrial.Distinct().Count() == industrial.Count, "no two seats share a name in the industrial age");
        var tribal = words.Select(w => (string?)w.Element("tribal")).ToList();
        Check(tribal.Distinct().Count() == tribal.Count, "no two seats share a name in the tribal age");

        var names4 = new List<string> { "seed-keeper", "reeve", "farm foreman", "agronomist" };
        Check(OfficeNamesMath.NameIn(names4, WorldAge.Spacer, "reeve") == "agronomist" && OfficeNamesMath.NameIn(names4, WorldAge.Tribal, "reeve") == "seed-keeper",
            "the age's name");
        Check(OfficeNamesMath.NameIn(new List<string> { "", "reeve", "farm foreman", "" }, WorldAge.Spacer, "x") == "farm foreman", "an age with no name takes the nearest age's");
        Check(OfficeNamesMath.NameIn(null, WorldAge.Spacer, "reeve") == "reeve", "a seat with no words keeps its own label");

        var pairs = new List<(string, string)> { ("reeve", "agronomist"), ("speaker", "coordinator"), ("keeper of days", "ceremonies officer"), ("elder", "senior councillor"), ("chaplain", "chaplain") };
        Check(OfficeNamesMath.Rename("Next time I'll be reeve, and there will be a plan.", pairs) == "Next time I'll be agronomist, and there will be a plan.", "a seat's old name in a line takes the age's");
        Check(OfficeNamesMath.Rename("I'd make a good reeve.", pairs) == "I'd make a good agronomist.", "a word between the article and the seat is left alone");
        Check(OfficeNamesMath.Rename("I'd make a reeve of anyone.", pairs) == "I'd make an agronomist of anyone.", "a, an: the article fits the new name");
        Check(OfficeNamesMath.Rename("Reeve. Good.", pairs) == "Agronomist. Good.", "a capital stays a capital");
        Check(OfficeNamesMath.Rename("{PAWN_nameDef}, one of the colony's elders, sat down.", pairs) == "{PAWN_nameDef}, one of the colony's senior councillors, sat down.", "the plural too");
        Check(OfficeNamesMath.Rename("The keeper of days says so.", pairs) == "The ceremonies officer says so.", "a name of several words, whole");
        Check(OfficeNamesMath.Rename("The speakers' corner, the loudspeaker, {SPEAKER_nameDef} and the chaplain.", pairs)
              == "The coordinators' corner, the loudspeaker, {SPEAKER_nameDef} and the chaplain.", "only whole words: never part of a word or a placeholder");
        Check(OfficeNamesMath.Rename("A reeve came.", pairs) == "An agronomist came.", "a capital article fits too");
        Check(OfficeNamesMath.Rename("The reeve's friends eat better.", pairs) == "The agronomist's friends eat better.", "a seat's possessive");
        Check(OfficeNamesMath.Plural("captain of the guard") == "captains of the guard" && OfficeNamesMath.Plural("head stockman") == "head stockmen"
              && OfficeNamesMath.Plural("security chief") == "security chiefs", "plurals of the seats' names");
        Check(OfficeNamesMath.Article("elder") == "an" && OfficeNamesMath.Article("reeve") == "a", "a and an");
    }
}
